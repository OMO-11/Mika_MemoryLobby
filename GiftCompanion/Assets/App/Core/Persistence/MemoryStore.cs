using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService
    {
        private const string MemorySchema = @"
CREATE TABLE memories (
 id TEXT PRIMARY KEY,character_id TEXT NOT NULL,content TEXT NOT NULL,type TEXT NOT NULL,
 importance REAL NOT NULL,confidence REAL NOT NULL,source_message_id TEXT NOT NULL REFERENCES messages(id),
 source_mode TEXT NOT NULL,created_at TEXT NOT NULL,status TEXT NOT NULL,dedup_key TEXT NOT NULL,evidence TEXT NOT NULL,
 source_order INTEGER NOT NULL);
CREATE TABLE user_facts (
 id TEXT PRIMARY KEY,character_id TEXT NOT NULL,key TEXT NOT NULL,value TEXT NOT NULL,confidence REAL NOT NULL,
 source_message_id TEXT NOT NULL REFERENCES messages(id),created_at TEXT NOT NULL,updated_at TEXT NOT NULL,
 status TEXT NOT NULL,superseded_by TEXT REFERENCES user_facts(id),dedup_key TEXT NOT NULL,evidence TEXT NOT NULL,
 source_order INTEGER NOT NULL,source_mode TEXT NOT NULL);
CREATE TABLE open_loops (
 id TEXT PRIMARY KEY,character_id TEXT NOT NULL,content TEXT NOT NULL,importance REAL NOT NULL,
 created_at TEXT NOT NULL,status TEXT NOT NULL,resolved_at TEXT,source_message_id TEXT NOT NULL REFERENCES messages(id),
 updated_at TEXT NOT NULL,confidence REAL NOT NULL,source_mode TEXT NOT NULL,dedup_key TEXT NOT NULL,evidence TEXT NOT NULL,
 source_order INTEGER NOT NULL);
CREATE UNIQUE INDEX facts_active_key ON user_facts(character_id,dedup_key) WHERE status='active';
CREATE UNIQUE INDEX memories_active_key ON memories(character_id,dedup_key) WHERE status='active';
CREATE UNIQUE INDEX loops_active_key ON open_loops(character_id,dedup_key) WHERE status='active';
CREATE TABLE memory_sources(kind TEXT NOT NULL,record_id TEXT NOT NULL,message_id TEXT NOT NULL REFERENCES messages(id),
 PRIMARY KEY(kind,record_id,message_id));
CREATE TABLE memory_meta(character_id TEXT PRIMARY KEY,revision INTEGER NOT NULL DEFAULT 0);
CREATE TABLE excluded_messages(message_id TEXT PRIMARY KEY REFERENCES messages(id),reason TEXT NOT NULL);
CREATE TABLE prompt_dependencies(assistant_message_id TEXT NOT NULL REFERENCES messages(id),source_message_id TEXT NOT NULL REFERENCES messages(id),
 PRIMARY KEY(assistant_message_id,source_message_id));";

        public Task RecoverMemoryJobsAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady();
            using var q = connection.Prepare(@"UPDATE messages SET extraction_status='pending' WHERE role='user'
                AND extraction_status='running' AND session_id IN (SELECT id FROM sessions WHERE character_id=?1);");
            q.Bind(1,characterId); q.Done();
            using(var summaries=connection.Prepare("UPDATE sessions SET summary_status='pending' WHERE character_id=?1 AND summary_status='running';"))
            {summaries.Bind(1,characterId);summaries.Done();}
            PropagateExclusions();SuppressExcludedTasks();
        },ct);
        public Task<ExtractionJob> ClaimExtractionAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); ExtractionJob result = null;
            Transaction(() =>
            {
                string requestId = null;
                using (var q=connection.Prepare(@"SELECT u.request_id FROM messages u JOIN sessions s ON s.id=u.session_id
                    WHERE s.character_id=?1 AND u.role='user' AND u.status='completed' AND u.extraction_status='pending'
                    AND NOT EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=u.id) ORDER BY u.rowid LIMIT 1;"))
                { q.Bind(1,characterId); if(q.Next()) requestId=q.Text(0); }
                if(requestId==null) return;
                result=new ExtractionJob { Turn=FindTurn(requestId), Revision=ReadRevision() };
                UpdateExtraction(requestId,"running");
            },ct);
            return result;
        },ct);

        public Task<IReadOnlyList<MemoryRecord>> LoadMemoriesAsync(string kind, bool activeOnly, int limit, CancellationToken ct)
        {
            string table=MemoryTable(kind);
            if(limit<1||limit>200) throw new ArgumentOutOfRangeException(nameof(limit));
            return Serialized<IReadOnlyList<MemoryRecord>>(() =>
            {
                RequireReady(); var result=new List<MemoryRecord>();
                using var q=connection.Prepare(RecordSelect(kind,table)+" WHERE character_id=?1"+
                    (activeOnly?" AND status='active'":"")+" ORDER BY source_order DESC,id LIMIT ?2;");
                q.Bind(1,characterId);q.Bind(2,limit.ToString(CultureInfo.InvariantCulture));
                while(q.Next()) result.Add(ReadRecord(q,kind)); return result;
            },ct);
        }
        public Task<bool> ApplyExtractionAsync(ExtractionJob job, MemoryExtraction extraction, CancellationToken ct)
        {
            if(job?.Turn==null||extraction==null) throw new ArgumentException("Invalid extraction job.");
            return Serialized(() =>
            {
                RequireReady(); bool applied=false;
                Transaction(() =>
                {
                    var turn=FindTurn(job.Turn.RequestId)??throw new InvalidDataException("Turn is missing.");
                    if(turn.Status!="completed" || turn.ExtractionStatus!="running") return;
                    if(IsExcluded(turn.UserMessageId)||IsExcluded(turn.AssistantMessageId)) { UpdateExtraction(turn.RequestId,"skipped");return; }
                    if(job.Revision!=ReadRevision()) { UpdateExtraction(turn.RequestId,"pending");return; }
                    long order=MessageOrder(turn.UserMessageId);
                    if(extraction.Items.Count>9 || extraction.ResolvedLoops.Count>3) throw new InvalidDataException("Too many memory items.");
                    foreach(var item in extraction.Items) ValidateItem(item,turn);
                    if(extraction.Items.Select(x=>x.Kind+":"+NormalizeMemory(x.Kind=="fact"?x.Key:x.Content)).Distinct().Count()!=extraction.Items.Count)
                        throw new InvalidDataException("Duplicate extraction key.");
                    foreach(var item in extraction.Items)
                    {
                        SaveMemoryItem(item,turn,order);
                    }
                    foreach(string id in extraction.ResolvedLoops.Distinct())
                    {
                        using(var existing=connection.Prepare("SELECT id FROM open_loops WHERE id=?1 AND character_id=?2 AND status='active';"))
                        {existing.Bind(1,id);existing.Bind(2,characterId);if(!existing.Next())throw new InvalidDataException("Unknown active open loop.");}
                        using var q=connection.Prepare("UPDATE open_loops SET status='resolved',resolved_at=?1,updated_at=?1 WHERE id=?2 AND character_id=?3 AND status='active' AND source_order<=?4;");
                        q.Bind(1,DateTimeOffset.UtcNow.ToString("O"));q.Bind(2,id);q.Bind(3,characterId);q.Bind(4,order.ToString());q.Done();
                        LinkSource("loop",id,turn.UserMessageId);
                        RemoveSearchRecord("loop",id);
                    }
                    UpdateExtraction(turn.RequestId,"succeeded");applied=true;
                },ct);
                return applied;
            },ct);
        }
        public Task SetExtractionStatusAsync(string requestId,string status,CancellationToken ct)
        {
            if(status!="pending"&&status!="failed"&&status!="skipped") throw new ArgumentException("Invalid extraction status.");
            return Serialized(() =>
            {
                RequireReady();
                using var q=connection.Prepare(@"UPDATE messages SET extraction_status=?1 WHERE request_id=?2 AND role='user'
                    AND extraction_status IN ('pending','running') AND session_id IN (SELECT id FROM sessions WHERE character_id=?3);");
                q.Bind(1,status);q.Bind(2,requestId);q.Bind(3,characterId);q.Done();
            },ct);
        }
        public Task RetryFailedExtractionsAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady();
            using var q=connection.Prepare(@"UPDATE messages SET extraction_status='pending' WHERE extraction_status='failed'
                AND role='user' AND session_id IN (SELECT id FROM sessions WHERE character_id=?1)
                AND NOT EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=messages.id);");
            q.Bind(1,characterId);q.Done();
            SuppressExcludedTasks();
            using var summaries=connection.Prepare("UPDATE sessions SET summary_status='pending' WHERE character_id=?1 AND summary_status='failed';");
            summaries.Bind(1,characterId);summaries.Done();
        },ct);

        private void SaveMemoryItem(MemoryRecord item,ConversationTurn turn,long order)
        {
            string table=MemoryTable(item.Kind), key=NormalizeMemory(item.Kind=="fact"?item.Key:item.Content);
            string oldId=null,oldContent=null; long oldOrder=0;
            using(var q=connection.Prepare("SELECT id,"+(item.Kind=="fact"?"value":"content")+",source_order FROM "+table+" WHERE character_id=?1 AND dedup_key=?2 AND status='active';"))
            { q.Bind(1,characterId);q.Bind(2,key);if(q.Next()){oldId=q.Text(0);oldContent=q.Text(1);oldOrder=q.Integer(2);} }
            if(oldId!=null && oldOrder>order) return;
            string now=DateTimeOffset.UtcNow.ToString("O");
            if(oldId!=null && (item.Kind=="fact" ? oldContent==item.Content : NormalizeMemory(oldContent)==NormalizeMemory(item.Content)))
            {
                LinkSource(item.Kind,oldId,turn.UserMessageId);
                using var update=connection.Prepare("UPDATE "+table+" SET source_order=?1 WHERE id=?2;");
                update.Bind(1,order.ToString());update.Bind(2,oldId);update.Done(); return;
            }
            string id=Guid.NewGuid().ToString("N");
            if(oldId!=null)
            {
                using var update=connection.Prepare("UPDATE "+table+" SET status='superseded' WHERE id=?1;");
                update.Bind(1,oldId);update.Done();
                RemoveSearchRecord(item.Kind,oldId);
                InvalidateSources(item.Kind,oldId,"superseded");
            }
            string sql=item.Kind=="fact"
                ? @"INSERT INTO user_facts(id,character_id,key,value,confidence,source_message_id,created_at,updated_at,status,dedup_key,evidence,source_order,source_mode)
                    VALUES(?1,?2,?3,?4,?5,?6,?7,?7,'active',?8,?9,?10,?11);"
                : "INSERT INTO "+table+@"(id,character_id,content,importance,confidence,source_message_id,created_at,status,dedup_key,evidence,source_order,source_mode,"+
                    (item.Kind=="memory"?"type":"updated_at")+@") VALUES(?1,?2,?4,?12,?5,?6,?7,'active',?8,?9,?10,?11,"+(item.Kind=="memory"?"'event'":"?7")+");";
            using(var insert=connection.Prepare(sql))
            {
                insert.Bind(1,id);insert.Bind(2,characterId);
                if(item.Kind=="fact")insert.Bind(3,item.Key);
                insert.Bind(4,item.Content);insert.Bind(5,item.Confidence.ToString(CultureInfo.InvariantCulture));
                insert.Bind(6,turn.UserMessageId);insert.Bind(7,now);insert.Bind(8,key);insert.Bind(9,item.Evidence);
                insert.Bind(10,order.ToString());insert.Bind(11,SourceName(turn.Source));
                if(item.Kind!="fact")insert.Bind(12,item.Importance.ToString(CultureInfo.InvariantCulture));
                insert.Done();
            }
            LinkSource(item.Kind,id,turn.UserMessageId);
            IndexRecord(item.Kind,id);
            if(oldId!=null && item.Kind=="fact")
            {
                using var update=connection.Prepare("UPDATE user_facts SET superseded_by=?1,updated_at=?2 WHERE id=?3;");
                update.Bind(1,id);update.Bind(2,now);update.Bind(3,oldId);update.Done();
            }
        }
        private static void ValidateItem(MemoryRecord item,ConversationTurn turn)
        {
            MemoryTable(item.Kind);
            if(item.SourceMessageId!=turn.UserMessageId || string.IsNullOrWhiteSpace(item.Content) || item.Content.Length>500 ||
                string.IsNullOrWhiteSpace(item.Evidence) || item.Evidence.Length>500 || !turn.UserText.Contains(item.Evidence) ||
                !item.Evidence.Contains(item.Content) || item.Confidence<.75 || item.Confidence>1 || double.IsNaN(item.Confidence) ||
                item.Importance<0 || item.Importance>1 || double.IsNaN(item.Importance) ||
                (item.Kind=="fact"&&(string.IsNullOrWhiteSpace(item.Key)||item.Key.Length>64)))
                throw new InvalidDataException("Unsupported memory item.");
        }
        private void LinkSource(string kind,string id,string messageId)
        {
            using var q=connection.Prepare("INSERT OR IGNORE INTO memory_sources(kind,record_id,message_id) VALUES(?1,?2,?3);");
            q.Bind(1,kind);q.Bind(2,id);q.Bind(3,messageId);q.Done();
        }
        private void InvalidateSources(string kind,string id,string reason)
        {
            ExcludeRecordReplies(kind,id,reason);
            using(var q=connection.Prepare(@"INSERT OR IGNORE INTO excluded_messages SELECT message_id,?3 FROM memory_sources WHERE kind=?1 AND record_id=?2;"))
            { q.Bind(1,kind);q.Bind(2,id);q.Bind(3,reason);q.Done(); }
            InvalidateRelatedRecords(kind,id,reason);
            PropagateExclusions();SuppressExcludedTasks();BumpRevision();InvalidateAllSummaries();
        }
        private void BumpRevision()
        {
            using var q=connection.Prepare("INSERT INTO memory_meta(character_id,revision) VALUES(?1,1) ON CONFLICT(character_id) DO UPDATE SET revision=revision+1;");
            q.Bind(1,characterId);q.Done();
        }
        private long ReadRevision()
        {using var q=connection.Prepare("SELECT revision FROM memory_meta WHERE character_id=?1;");q.Bind(1,characterId);return q.Next()?q.Integer(0):0;}
        private bool IsExcluded(string id)
        {using var q=connection.Prepare("SELECT 1 FROM excluded_messages WHERE message_id=?1;");q.Bind(1,id);return q.Next();}
        private long MessageOrder(string id)
        {using var q=connection.Prepare("SELECT rowid FROM messages WHERE id=?1;");q.Bind(1,id);q.Row();return q.Integer(0);}
        private void UpdateExtraction(string id,string status)
        {using var q=connection.Prepare("UPDATE messages SET extraction_status=?1 WHERE request_id=?2 AND role='user';");q.Bind(1,status);q.Bind(2,id);q.Done();}
        internal static string NormalizeMemory(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant(),@"\s+"," ");
        private static string MemoryTable(string kind)
        {switch(kind){case "fact":return "user_facts";case "memory":return "memories";case "loop":return "open_loops";default:throw new ArgumentException("Unknown memory kind.");}}
        private static string RecordSelect(string kind,string table) => "SELECT id,"+(kind=="fact"?"key,value":"'',content")+
            ",evidence,source_message_id,source_mode,status,"+(kind=="fact"?"0.7":"importance")+",confidence,created_at FROM "+table;
        private static MemoryRecord ReadRecord(SqliteConnection.Statement q,string kind) => new MemoryRecord {
            Id=q.Text(0),Kind=kind,Key=q.Text(1),Content=q.Text(2),Evidence=q.Text(3),SourceMessageId=q.Text(4),SourceMode=q.Text(5),Status=q.Text(6),
            Importance=q.Real(7),Confidence=q.Real(8),CreatedAt=DateTimeOffset.Parse(q.Text(9))
        };
    }
}
