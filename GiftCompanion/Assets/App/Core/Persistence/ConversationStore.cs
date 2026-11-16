using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService
    {
        private string activeSessionId;
        private InteractionSource activeSource;
        private const string ConversationSchema = @"
CREATE TABLE sessions (
 id TEXT PRIMARY KEY, character_id TEXT NOT NULL, source TEXT NOT NULL CHECK(source IN ('companion','agent_pet')),
 started_at TEXT NOT NULL, ended_at TEXT, status TEXT NOT NULL DEFAULT 'active',
 summary TEXT NOT NULL DEFAULT '', summary_through INTEGER NOT NULL DEFAULT 0,
 summary_status TEXT NOT NULL DEFAULT 'none', summary_revision INTEGER NOT NULL DEFAULT 0);
CREATE TABLE messages (
 id TEXT PRIMARY KEY, session_id TEXT NOT NULL REFERENCES sessions(id), request_id TEXT NOT NULL,
 role TEXT NOT NULL CHECK(role IN ('user','assistant')), content TEXT NOT NULL,
 source TEXT NOT NULL CHECK(source IN ('companion','agent_pet')), created_at TEXT NOT NULL,
 status TEXT NOT NULL, extraction_status TEXT NOT NULL DEFAULT 'none',
 UNIQUE(request_id,role));
CREATE INDEX messages_session ON messages(session_id,created_at);
CREATE INDEX messages_extraction ON messages(extraction_status,role);
CREATE INDEX sessions_character ON sessions(character_id,status);";

        public Task RecoverConversationsAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady();
            Transaction(() =>
            {
                using (var command = connection.Prepare(@"UPDATE messages SET status='interrupted' WHERE status='pending'
                    AND session_id IN (SELECT id FROM sessions WHERE character_id=?1);"))
                { command.Bind(1, characterId); command.Done(); }
                CloseSessions("interrupted");
            }, ct);
            activeSessionId = null;
        }, ct);

        public Task<ConversationTurn> BeginTurnAsync(string requestId, string text, InteractionSource source, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(text) || text.Length > 12000)
                throw new ArgumentException("Invalid dialogue input.");
            string origin = SourceName(source);
            return Serialized(() =>
            {
                RequireReady();
                var existing = FindTurn(requestId);
                if (existing != null)
                {
                    if (existing.UserText != text || existing.Source != source) throw new InvalidDataException("Request ID conflict.");
                    return existing;
                }
                var turn = new ConversationTurn { RequestId = requestId, SessionId = activeSessionId,
                    UserMessageId = Guid.NewGuid().ToString("N"), Source = source, UserText = text,
                    CreatedAt = DateTimeOffset.UtcNow, Status = "pending", ExtractionStatus = "none" };
                bool newSession = activeSessionId == null || activeSource != source;
                if (newSession) turn.SessionId = Guid.NewGuid().ToString("N");
                Transaction(() =>
                {
                    if (newSession)
                    {
                        CloseSessions("closed");
                        using var session = connection.Prepare("INSERT INTO sessions(id,character_id,source,started_at) VALUES(?1,?2,?3,?4);");
                        session.Bind(1, turn.SessionId); session.Bind(2, characterId); session.Bind(3, origin);
                        session.Bind(4, turn.CreatedAt.ToString("O")); session.Done();
                    }
                    using var message = connection.Prepare(@"INSERT INTO messages(id,session_id,request_id,role,content,source,created_at,status)
                        VALUES(?1,?2,?3,'user',?4,?5,?6,'pending');");
                    message.Bind(1, turn.UserMessageId); message.Bind(2, turn.SessionId); message.Bind(3, requestId);
                    message.Bind(4, text); message.Bind(5, origin); message.Bind(6, turn.CreatedAt.ToString("O")); message.Done();
                }, ct);
                activeSessionId = turn.SessionId; activeSource = source;
                return turn;
            }, ct);
        }

        public Task CompleteTurnAsync(ConversationTurn turn, string reply, CharacterState state, CancellationToken ct,
            IReadOnlyList<string> contextSources = null, long contextRevision = -1,IReadOnlyList<MemoryReference> memoryReferences = null,DialoguePresentation presentation=null,bool suppressMemory=false)
        {
            if (turn == null || string.IsNullOrWhiteSpace(reply)) throw new ArgumentException("Invalid complete turn.");
            var snapshot = state.Snapshot(); Validate(snapshot);
            return Serialized(() =>
            {
                RequireReady();
                Transaction(() =>
                {
                    var saved = FindTurn(turn.RequestId) ?? throw new InvalidDataException("Turn is missing.");
                    if (saved.Status == "completed")
                    {
                        if (saved.RawAssistantText != reply) throw new InvalidDataException("Completion conflict.");
                        return;
                    }
                    if (saved.Status != "pending" || saved.UserMessageId != turn.UserMessageId)
                        throw new InvalidDataException("Turn cannot complete.");
                    if (contextRevision >= 0 && contextRevision != ReadRevision()) throw new MemoryContextChangedException();
                    string assistantId = Guid.NewGuid().ToString("N");
                    using (var message = connection.Prepare(@"INSERT INTO messages(id,session_id,request_id,role,content,source,created_at,status)
                        VALUES(?1,?2,?3,'assistant',?4,?5,?6,'completed');"))
                    {
                        message.Bind(1, assistantId); message.Bind(2, saved.SessionId);
                        message.Bind(3, saved.RequestId); message.Bind(4, reply); message.Bind(5, SourceName(saved.Source));
                        message.Bind(6, DateTimeOffset.UtcNow.ToString("O")); message.Done();
                    }
                    if (presentation != null)
                    {
                        if (string.IsNullOrWhiteSpace(presentation.BodyText) || !Enum.IsDefined(typeof(CharacterEmotion), presentation.Emotion))
                            throw new InvalidDataException("Invalid presentation.");
                        using var p = connection.Prepare("INSERT INTO assistant_presentations VALUES(?1,1,?2,?3,?4);");
                        p.Bind(1, assistantId); p.Bind(2, presentation.BodyText); p.Bind(3, presentation.Emotion.ToString().ToLowerInvariant()); p.Bind(4, presentation.Action); p.Done();
                    }
                    using (var update = connection.Prepare("UPDATE messages SET status='completed',extraction_status='pending' WHERE id=?1;"))
                    { update.Bind(1, saved.UserMessageId); update.Done(); }
                    if (suppressMemory)
                    {
                        using (var skip = connection.Prepare("UPDATE messages SET extraction_status='skipped' WHERE id=?1;")) { skip.Bind(1, saved.UserMessageId); skip.Done(); }
                        foreach (string id in new[] { saved.UserMessageId, assistantId })
                        { using var excluded = connection.Prepare("INSERT OR IGNORE INTO excluded_messages(message_id,reason) VALUES(?1,'desktop_tool');"); excluded.Bind(1, id); excluded.Done(); }
                    }
                    WriteState(snapshot);
                    LinkPromptSource(assistantId, saved.UserMessageId);
                    if (contextSources != null)
                        foreach (string sourceId in contextSources) LinkPromptSource(assistantId, sourceId);
                    if(memoryReferences!=null)foreach(var reference in memoryReferences)
                    {
                        using(var exists=connection.Prepare("SELECT 1 FROM "+MemoryTable(reference.Kind)+" WHERE id=?1 AND character_id=?2 AND status='active';"))
                        {exists.Bind(1,reference.Id);exists.Bind(2,characterId);if(!exists.Next())throw new MemoryContextChangedException();}
                        using var link=connection.Prepare("INSERT OR IGNORE INTO prompt_record_dependencies(assistant_message_id,kind,record_id) VALUES(?1,?2,?3);");
                        link.Bind(1,assistantId);link.Bind(2,reference.Kind);link.Bind(3,reference.Id);link.Done();
                    }
                    ConsiderSummary(saved.SessionId,false);
                }, ct);
            }, ct);
        }
        private void LinkPromptSource(string assistantId, string sourceId)
        {
            using var command = connection.Prepare(@"INSERT OR IGNORE INTO prompt_dependencies(assistant_message_id,source_message_id)
                SELECT ?1,m.id FROM messages m JOIN sessions s ON s.id=m.session_id WHERE m.id=?2 AND s.character_id=?3;");
            command.Bind(1,assistantId);command.Bind(2,sourceId);command.Bind(3,characterId);command.Done();
        }

        public Task FailTurnAsync(string requestId, string status, CancellationToken ct)
        {
            if (status != "failed" && status != "canceled" && status != "interrupted") throw new ArgumentException("Invalid turn status.");
            return Serialized(() =>
            {
                RequireReady();
                using var command = connection.Prepare(@"UPDATE messages SET status=?1 WHERE request_id=?2 AND role='user'
                    AND status='pending' AND session_id IN (SELECT id FROM sessions WHERE character_id=?3);");
                command.Bind(1, status); command.Bind(2, requestId); command.Bind(3, characterId); command.Done();
            }, ct);
        }

        public Task<IReadOnlyList<ConversationTurn>> LoadTurnsAsync(int limit, bool completedOnly, CancellationToken ct)
        {
            if (limit < 1 || limit > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
            return Serialized<IReadOnlyList<ConversationTurn>>(() =>
            {
                RequireReady();
                var result = new List<ConversationTurn>();
                using var query = connection.Prepare(TurnSelect + @" WHERE s.character_id=?1 AND u.role='user'"
                    + (completedOnly ? " AND u.status='completed'" : "") + " ORDER BY u.rowid DESC LIMIT ?2;");
                query.Bind(1, characterId); query.Bind(2, limit.ToString());
                while (query.Next()) result.Add(ReadTurn(query));
                result.Reverse(); return result;
            }, ct);
        }

        public Task EndSessionAndSaveStateAsync(CharacterState state, string reason, CancellationToken ct)
        {
            if (reason != "closed" && reason != "interrupted") throw new ArgumentException("Invalid session status.");
            var snapshot = state.Snapshot(); Validate(snapshot);
            return Serialized(() =>
            {
                RequireReady();
                Transaction(() => { WriteState(snapshot); CloseSessions(reason); }, ct);
                activeSessionId = null;
            }, ct);
        }
        private void CloseSessions(string reason)
        {
            var closing=new List<string>();
            using(var query=connection.Prepare("SELECT id FROM sessions WHERE character_id=?1 AND status='active';"))
            {query.Bind(1,characterId);while(query.Next())closing.Add(query.Text(0));}
            using var close = connection.Prepare("UPDATE sessions SET status=?1,ended_at=?2 WHERE character_id=?3 AND status='active';");
            close.Bind(1, reason); close.Bind(2, DateTimeOffset.UtcNow.ToString("O")); close.Bind(3, characterId); close.Done();
            foreach(string id in closing)ConsiderSummary(id,true);
        }
        private const string TurnSelect = @"SELECT u.request_id,u.session_id,u.id,a.id,u.content,a.content,u.source,
            u.status,u.extraction_status,u.created_at,coalesce(p.body_text,a.content),
            EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=u.id OR e.message_id=a.id) FROM messages u JOIN sessions s ON s.id=u.session_id
            LEFT JOIN messages a ON a.request_id=u.request_id AND a.role='assistant'
            LEFT JOIN assistant_presentations p ON p.message_id=a.id";
        private ConversationTurn FindTurn(string requestId)
        {
            using var query = connection.Prepare(TurnSelect + " WHERE u.request_id=?1 AND u.role='user' AND s.character_id=?2;");
            query.Bind(1, requestId); query.Bind(2, characterId);
            return query.Next() ? ReadTurn(query) : null;
        }
        private static ConversationTurn ReadTurn(SqliteConnection.Statement q) => new ConversationTurn {
            RequestId=q.Text(0), SessionId=q.Text(1), UserMessageId=q.Text(2), AssistantMessageId=q.Text(3),
            UserText=q.Text(4), RawAssistantText=q.Text(5), AssistantText=q.Text(10), Source=q.Text(6)=="companion" ? InteractionSource.Companion : InteractionSource.AgentPet,
            Status=q.Text(7), ExtractionStatus=q.Text(8), CreatedAt=DateTimeOffset.Parse(q.Text(9)), ExcludedFromMemory=q.Integer(11)!=0
        };
        internal static string SourceName(InteractionSource source)
        {
            switch (source) { case InteractionSource.Companion: return "companion"; case InteractionSource.AgentPet: return "agent_pet";
                default: throw new ArgumentOutOfRangeException(nameof(source)); }
        }
    }
}
