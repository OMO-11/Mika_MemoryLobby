using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Logging;
using GiftCompanion.Core.Memory;
using Newtonsoft.Json;

namespace GiftCompanion.Core.Persistence
{
    public interface IDatabaseService : IDisposable
    {
        bool IsPersistent { get; }
        int SchemaVersion { get; }
        Task InitializeAsync(CancellationToken ct);
        Task MigrateAsync(CancellationToken ct);
        Task<CharacterState> LoadCharacterStateAsync(CancellationToken ct);
        Task SaveCharacterStateAsync(CharacterState state, CancellationToken ct);
    }

    public sealed partial class DatabaseService : IDatabaseService, IConversationStore, IMemoryStore
    {
        public const int CurrentSchemaVersion = 10;
        private readonly SemaphoreSlim queue = new SemaphoreSlim(1, 1);
        private readonly string path, characterId;
        private readonly ILogger logger;
        private SqliteConnection connection;
        private bool ready, disposed;
        public bool IsPersistent => ready && !disposed;
        public int SchemaVersion { get; private set; }

        public DatabaseService(string path, string characterId, ILogger logger = null)
        {
            this.path = Path.GetFullPath(path);
            if (string.IsNullOrWhiteSpace(characterId)) throw new ArgumentException("Character ID is required.");
            this.characterId = characterId; this.logger = logger;
        }

        public Task InitializeAsync(CancellationToken ct) => Serialized(() =>
        {
            if (connection != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            connection = new SqliteConnection(path);
            try
            {
                SchemaVersion = connection.Version;
                if (SchemaVersion > CurrentSchemaVersion) throw new InvalidDataException("Database schema is newer than supported.");
            }
            catch { connection.Dispose(); connection = null; throw; }
        }, ct);

        public Task MigrateAsync(CancellationToken ct) => Serialized(() =>
        {
            if (connection == null) throw new InvalidOperationException("Database is not initialized.");
            connection.Execute("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;");
            if (SchemaVersion < CurrentSchemaVersion)
            {
                // Native backup includes committed WAL pages; do not copy a live .db alone.
                if (new FileInfo(path).Length > 0)
                    connection.Backup(path + ".pre-v" + CurrentSchemaVersion + "-" + Guid.NewGuid().ToString("N") + ".bak");
                int previousVersion = SchemaVersion;
                connection.Execute("BEGIN IMMEDIATE;");
                try
                {
                    if (previousVersion == 0) connection.Execute(@"CREATE TABLE app_state (
                        character_id TEXT PRIMARY KEY NOT NULL,
                        state_version INTEGER NOT NULL,
                        state_json TEXT NOT NULL,
                        updated_at TEXT NOT NULL);");
                    if (previousVersion < 2) connection.Execute(ConversationSchema);
                    if (previousVersion < 3) connection.Execute(MemorySchema);
                    if (previousVersion < 4) { connection.Execute(SearchSchema); RebuildSearchIndex(); }
                    if (previousVersion < 5) connection.Execute(SummarySchema);
                    if(previousVersion<6)connection.Execute(@"CREATE TABLE prompt_record_dependencies(
                        assistant_message_id TEXT NOT NULL REFERENCES messages(id),kind TEXT NOT NULL,record_id TEXT NOT NULL,
                        PRIMARY KEY(assistant_message_id,kind,record_id));CREATE INDEX prompt_record_sources ON prompt_record_dependencies(kind,record_id);");
                    if (previousVersion < 7) connection.Execute(@"CREATE TABLE assistant_presentations(
                        message_id TEXT PRIMARY KEY REFERENCES messages(id), protocol_version INTEGER NOT NULL CHECK(protocol_version=1),
                        body_text TEXT NOT NULL, emotion TEXT NOT NULL, action TEXT NOT NULL);
                        CREATE INDEX history_cursor ON messages(created_at,id) WHERE role='user';");
                    if (previousVersion < 8) connection.Execute(DiarySchema);
                    if (previousVersion < 9) connection.Execute(AudioSchema);
                    if (previousVersion < 10) connection.Execute(AgentSchema);
                    ValidateSchema();
                    connection.Execute("PRAGMA user_version=" + CurrentSchemaVersion + ";");
                    ct.ThrowIfCancellationRequested();
                    connection.Execute("COMMIT;");
                    SchemaVersion = CurrentSchemaVersion;
                    logger?.Info($"database migration from={previousVersion} to={SchemaVersion} result=completed");
                }
                catch { connection.Execute("ROLLBACK;"); throw; }
            }
            ValidateSchema();
            connection.Execute("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000; PRAGMA synchronous=FULL;");
            ready = true;
        }, ct);

        public async Task<CharacterState> LoadCharacterStateAsync(CancellationToken ct)
        {
            CharacterState result = null;
            await Serialized(() =>
            {
                RequireReady();
                using var query = connection.Prepare("SELECT state_version,state_json FROM app_state WHERE character_id=?1;");
                query.Bind(1, characterId);
                if (!query.Next()) return;
                if (query.Integer(0) != 1) throw new InvalidDataException("Unsupported character state version.");
                var record = JsonConvert.DeserializeObject<StateRecord>(query.Text(1), new JsonSerializerSettings { MaxDepth = 32 });
                if (record == null) throw new InvalidDataException("Character state is invalid.");
                result = record.ToState(); Validate(result);
            }, ct);
            return result;
        }

        public Task SaveCharacterStateAsync(CharacterState state, CancellationToken ct)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var snapshot = state.Snapshot(); Validate(snapshot);
            return Serialized(() =>
            {
                RequireReady();
                Transaction(() => WriteState(snapshot), ct);
            }, ct);
        }
        private void WriteState(CharacterState snapshot)
        {
            using var command = connection.Prepare(@"INSERT INTO app_state(character_id,state_version,state_json,updated_at)
                VALUES(?1,1,?2,?3) ON CONFLICT(character_id) DO UPDATE SET
                state_version=1,state_json=excluded.state_json,updated_at=excluded.updated_at;");
            command.Bind(1, characterId); command.Bind(2, JsonConvert.SerializeObject(new StateRecord(snapshot)));
            command.Bind(3, DateTimeOffset.UtcNow.ToString("O")); command.Done();
        }
        private void Transaction(Action action, CancellationToken ct)
        {
            connection.Execute("BEGIN IMMEDIATE;");
            try { action(); ct.ThrowIfCancellationRequested(); connection.Execute("COMMIT;"); }
            catch { connection.Execute("ROLLBACK;"); throw; }
        }
        private void ValidateSchema() => ValidateSchema(connection);
        private static void ValidateSchema(SqliteConnection connection)
        {
            using (connection.Prepare("SELECT request_id,call_id,status,permission FROM agent_tool_runs LIMIT 0;")) { }
            using (connection.Prepare("SELECT character_id,state_version,state_json,updated_at FROM app_state LIMIT 0;")) { }
            using (connection.Prepare("SELECT id,character_id,source,status,summary,summary_through FROM sessions LIMIT 0;")) { }
            using (connection.Prepare("SELECT id,session_id,request_id,role,content,source,status,extraction_status FROM messages LIMIT 0;")) { }
            using (connection.Prepare("SELECT id,content,status,source_message_id FROM memories LIMIT 0;")) { }
            using (connection.Prepare("SELECT id,key,value,status,superseded_by FROM user_facts LIMIT 0;")) { }
            using (connection.Prepare("SELECT id,content,status,resolved_at FROM open_loops LIMIT 0;")) { }
            using (connection.Prepare("SELECT record_id,body,terms FROM memory_search LIMIT 0;")) { }
            using (connection.Prepare("SELECT summary_sources,summary_memory_revision FROM sessions LIMIT 0;")) { }
            using(connection.Prepare("SELECT assistant_message_id,kind,record_id FROM prompt_record_dependencies LIMIT 0;")){}
            using(connection.Prepare("SELECT message_id,protocol_version,body_text,emotion,action FROM assistant_presentations LIMIT 0;")){}
            using(connection.Prepare("SELECT id,character_id,body,status,export_status FROM relationship_journals LIMIT 0;")){}
            using(connection.Prepare("SELECT journal_id,message_id FROM journal_sources LIMIT 0;")){}
            using(connection.Prepare("SELECT message_id,segment_index,segment_count,path,duration,display_text,profile_id,created_at FROM audio_records LIMIT 0;")){}
        }
        private async Task<T> Serialized<T>(Func<T> operation, CancellationToken ct)
        {
            T result = default;
            await Serialized(() => { result = operation(); }, ct).ConfigureAwait(false);
            return result;
        }
        private async Task Serialized(Action operation, CancellationToken ct)
        {
            await queue.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(DatabaseService));
                await Task.Run(() => { ct.ThrowIfCancellationRequested(); operation(); }).ConfigureAwait(false);
            }
            finally { queue.Release(); }
        }
        private void RequireReady() { if (!ready) throw new InvalidOperationException("Database migration is not complete."); }
        private static void Validate(CharacterState state)
        {
            if (!Enum.IsDefined(typeof(CharacterEmotion), state.Mood) || !Enum.IsDefined(typeof(CharacterMode), state.LastMode) ||
                float.IsNaN(state.Energy) || float.IsInfinity(state.Energy) || state.Energy < 0 || state.Energy > 1 || state.CurrentTopic == null)
                throw new InvalidDataException("Character state values are invalid.");
        }
        public void Dispose()
        {
            queue.Wait();
            try { if (disposed) return; disposed = true; ready = false; connection?.Dispose(); connection = null; }
            finally { queue.Release(); }
        }
        private sealed class StateRecord
        {
            public CharacterEmotion Mood { get; set; }
            public float Energy { get; set; }
            public string CurrentTopic { get; set; }
            public CharacterMode LastMode { get; set; }
            public DateTimeOffset? LastInteractionAt { get; set; }
            public StateRecord() { }
            public StateRecord(CharacterState state)
            {
                Mood = state.Mood; Energy = state.Energy; CurrentTopic = state.CurrentTopic;
                LastMode = state.LastMode; LastInteractionAt = state.LastInteractionAt;
            }
            public CharacterState ToState() => new CharacterState {
                Mood = Mood, Energy = Energy, CurrentTopic = CurrentTopic, LastMode = LastMode, LastInteractionAt = LastInteractionAt
            };
        }
    }
}
