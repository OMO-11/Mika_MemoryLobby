using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService : IDiaryStore
    {
        private const string DiarySchema = @"CREATE TABLE relationship_journals(
            id TEXT PRIMARY KEY,character_id TEXT NOT NULL,created_at TEXT NOT NULL,first_at TEXT NOT NULL,last_at TEXT NOT NULL,
            turn_count INTEGER NOT NULL,revision INTEGER NOT NULL,fingerprint TEXT NOT NULL,body TEXT NOT NULL,
            status TEXT NOT NULL,export_status TEXT NOT NULL);
            CREATE INDEX journal_character ON relationship_journals(character_id,created_at);
            CREATE TABLE journal_sources(journal_id TEXT NOT NULL REFERENCES relationship_journals(id),message_id TEXT NOT NULL REFERENCES messages(id),PRIMARY KEY(journal_id,message_id));";
        private const string DiaryEligible = @" s.character_id=?1 AND u.role='user' AND u.status='completed'
            AND NOT EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=u.id OR e.message_id=a.id)";
        public Task<DiarySnapshot> SnapshotDiaryAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); var snapshot = new DiarySnapshot { Revision = ReadRevision() };
            using var q = connection.Prepare(@"SELECT coalesce(max(u.rowid),0),count(*),min(u.created_at),max(u.created_at)
                FROM messages u JOIN sessions s ON s.id=u.session_id JOIN messages a ON a.request_id=u.request_id AND a.role='assistant' WHERE " + DiaryEligible);
            q.Bind(1, characterId); q.Row(); snapshot.Through = q.Integer(0); snapshot.Count = q.Integer(1);
            if (snapshot.Count > 0) { snapshot.FirstAt = DateTimeOffset.Parse(q.Text(2)); snapshot.LastAt = DateTimeOffset.Parse(q.Text(3)); }
            return snapshot;
        }, ct);
        public Task<DiaryBatch> ReadDiaryBatchAsync(DiarySnapshot snapshot, long after, CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); if (ReadRevision() != snapshot.Revision) throw new MemoryContextChangedException();
            var batch = new DiaryBatch();
            using var q = connection.Prepare(TurnSelect + " WHERE " + DiaryEligible + " AND u.rowid>?2 AND u.rowid<=?3 ORDER BY u.rowid LIMIT 20;");
            q.Bind(1, characterId); q.Bind(2, after.ToString()); q.Bind(3, snapshot.Through.ToString());
            while (q.Next()) { var turn = ReadTurn(q); batch.Turns.Add(turn); batch.Through = MessageOrder(turn.UserMessageId); }
            return batch;
        }, ct);
        private const string DiarySelect = "SELECT id,body,status,fingerprint,created_at,first_at,last_at,turn_count,export_status FROM relationship_journals";
        private static DiaryEntry ReadDiary(SqliteConnection.Statement q) => new DiaryEntry { Id = q.Text(0), Body = q.Text(1), Status = q.Text(2), Fingerprint = q.Text(3),
            CreatedAt = DateTimeOffset.Parse(q.Text(4)), FirstAt = DateTimeOffset.Parse(q.Text(5)), LastAt = DateTimeOffset.Parse(q.Text(6)), TurnCount = q.Integer(7), ExportPending = q.Text(8) == "pending" };
        public Task<DiaryEntry> FindDiaryAsync(string fingerprint, CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); using var q = connection.Prepare(DiarySelect + " WHERE character_id=?1 AND fingerprint=?2 AND status='active' ORDER BY created_at DESC LIMIT 1;");
            q.Bind(1, characterId); q.Bind(2, fingerprint); return q.Next() ? ReadDiary(q) : null;
        }, ct);
        public Task<IReadOnlyList<DiaryEntry>> ListDiariesAsync(CancellationToken ct) => Serialized<IReadOnlyList<DiaryEntry>>(() =>
        {
            RequireReady(); var entries = new List<DiaryEntry>(); using var q = connection.Prepare(DiarySelect + " WHERE character_id=?1 ORDER BY created_at DESC LIMIT 100;");
            q.Bind(1, characterId); while (q.Next()) entries.Add(ReadDiary(q)); return entries;
        }, ct);
        public Task<bool> CommitDiaryAsync(DiarySnapshot snapshot, DiaryEntry entry, IReadOnlyList<string> sources, CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); bool saved = false;
            Transaction(() =>
            {
                if (ReadRevision() != snapshot.Revision || sources.Count != snapshot.Count * 2 || sources.Any(IsExcluded)) return;
                using (var q = connection.Prepare(@"INSERT INTO relationship_journals VALUES(?1,?2,?3,?4,?5,?6,?7,?8,?9,'active','pending');"))
                {
                    q.Bind(1, entry.Id); q.Bind(2, characterId); q.Bind(3, entry.CreatedAt.ToString("O")); q.Bind(4, entry.FirstAt.ToString("O")); q.Bind(5, entry.LastAt.ToString("O"));
                    q.Bind(6, entry.TurnCount.ToString()); q.Bind(7, snapshot.Revision.ToString()); q.Bind(8, entry.Fingerprint); q.Bind(9, entry.Body); q.Done();
                }
                foreach (string source in sources) { using var q = connection.Prepare("INSERT INTO journal_sources VALUES(?1,?2);"); q.Bind(1, entry.Id); q.Bind(2, source); q.Done(); }
                saved = true;
            }, ct); return saved;
        }, ct);
        public Task ExportDiariesAsync(Action<DiaryEntry> write, CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); var pending = new List<DiaryEntry>();
            using (var q = connection.Prepare(DiarySelect + " WHERE character_id=?1;"))
            { q.Bind(1, characterId); while (q.Next()) pending.Add(ReadDiary(q)); }
            foreach (var entry in pending)
            {
                ct.ThrowIfCancellationRequested(); write(entry);
                if (!entry.ExportPending) continue;
                using var q = connection.Prepare("UPDATE relationship_journals SET export_status='succeeded' WHERE id=?1;"); q.Bind(1, entry.Id); q.Done();
            }
        }, ct);
        private void InvalidateDiaries()
        {
            using var q = connection.Prepare("UPDATE relationship_journals SET status='needs_review',body='',export_status='pending' WHERE character_id=?1 AND status='active';");
            q.Bind(1, characterId); q.Done();
        }
    }
}
