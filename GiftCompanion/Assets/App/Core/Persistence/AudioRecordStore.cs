using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService : IAudioRecordStore
    {
        private const string AudioSchema = @"CREATE TABLE audio_records(
            message_id TEXT NOT NULL REFERENCES messages(id),segment_index INTEGER NOT NULL CHECK(segment_index>=0),
            segment_count INTEGER NOT NULL CHECK(segment_count>0),path TEXT NOT NULL UNIQUE,duration REAL NOT NULL CHECK(duration>0),
            display_text TEXT NOT NULL,profile_id TEXT NOT NULL,created_at TEXT NOT NULL,
            PRIMARY KEY(message_id,segment_index));";
        public Task SaveAudioRecordsAsync(string requestId, IReadOnlyList<AudioRecord> records, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(requestId) || records == null || records.Count < 1 || records.Count > 1024 ||
                records.Select((r, i) => r == null || r.SegmentIndex != i || r.SegmentCount != records.Count ||
                    string.IsNullOrEmpty(r.Path) || r.Path.Length > 250 || r.DisplayText == null || r.DisplayText.Length > 1000 ||
                    string.IsNullOrEmpty(r.ProfileId) || r.ProfileId.Length > 100 || double.IsNaN(r.Duration) || double.IsInfinity(r.Duration) || r.Duration <= 0).Any(x => x))
                throw new InvalidDataException("Audio records invalid.");
            return Serialized(() =>
            {
                RequireReady();
                Transaction(() =>
                {
                    string messageId;
                    using (var find = connection.Prepare(@"SELECT m.id FROM messages m JOIN sessions s ON s.id=m.session_id
                        WHERE m.request_id=?1 AND m.role='assistant' AND m.status='completed' AND s.character_id=?2;"))
                    { find.Bind(1, requestId); find.Bind(2, characterId); if (!find.Next()) throw new InvalidDataException("Only committed assistant messages own audio."); messageId = find.Text(0); }
                    using (var existing = connection.Prepare("SELECT count(*) FROM audio_records WHERE message_id=?1;"))
                    { existing.Bind(1, messageId); existing.Row(); if (existing.Integer(0) > 0) throw new InvalidDataException("Audio is already cached."); }
                    foreach (var record in records)
                    {
                        using var insert = connection.Prepare("INSERT INTO audio_records VALUES(?1,?2,?3,?4,?5,?6,?7,?8);");
                        insert.Bind(1, messageId); insert.Bind(2, record.SegmentIndex.ToString(CultureInfo.InvariantCulture)); insert.Bind(3, records.Count.ToString(CultureInfo.InvariantCulture));
                        insert.Bind(4, record.Path); insert.Bind(5, record.Duration.ToString("R", CultureInfo.InvariantCulture)); insert.Bind(6, record.DisplayText);
                        insert.Bind(7, record.ProfileId); insert.Bind(8, DateTimeOffset.UtcNow.ToString("O")); insert.Done();
                    }
                }, ct);
            }, ct);
        }
        public Task<IReadOnlyList<AudioRecord>> ReadAudioRecordsAsync(string messageId, CancellationToken ct) => Serialized<IReadOnlyList<AudioRecord>>(() =>
        {
            RequireReady(); var records = new List<AudioRecord>();
            using var q = connection.Prepare(@"SELECT a.segment_index,a.segment_count,a.path,a.duration,a.display_text,a.profile_id FROM audio_records a
                JOIN messages m ON m.id=a.message_id JOIN sessions s ON s.id=m.session_id
                WHERE a.message_id=?1 AND s.character_id=?2 AND m.role='assistant' AND m.status='completed' ORDER BY a.segment_index LIMIT 1025;");
            q.Bind(1, messageId ?? ""); q.Bind(2, characterId);
            while (q.Next()) records.Add(new AudioRecord { MessageId = messageId, SegmentIndex = (int)q.Integer(0), SegmentCount = (int)q.Integer(1), Path = q.Text(2), Duration = q.Real(3), DisplayText = q.Text(4), ProfileId = q.Text(5) });
            return records;
        }, ct);
        public Task<IReadOnlyList<string>> ReadAllAudioPathsAsync(CancellationToken ct) => Serialized<IReadOnlyList<string>>(() =>
        {
            RequireReady(); var paths = new List<string>();
            using var q = connection.Prepare(@"SELECT a.path FROM audio_records a JOIN messages m ON m.id=a.message_id JOIN sessions s ON s.id=m.session_id WHERE s.character_id=?1;");
            q.Bind(1, characterId); while (q.Next()) paths.Add(q.Text(0)); return paths;
        }, ct);
        public Task ClearAudioRecordsAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); Transaction(() =>
            { using var q = connection.Prepare("DELETE FROM audio_records WHERE message_id IN(SELECT m.id FROM messages m JOIN sessions s ON s.id=m.session_id WHERE s.character_id=?1);"); q.Bind(1, characterId); q.Done(); }, ct);
        }, ct);
    }
}
