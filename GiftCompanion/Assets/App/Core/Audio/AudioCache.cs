using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;

namespace GiftCompanion.Core.Audio
{
    public sealed class AudioRecord
    {
        public string MessageId { get; set; }
        public int SegmentIndex { get; set; }
        public int SegmentCount { get; set; }
        public string Path { get; set; }
        public double Duration { get; set; }
        public string DisplayText { get; set; }
        public string ProfileId { get; set; }
    }
    public interface IAudioRecordStore
    {
        Task SaveAudioRecordsAsync(string requestId, IReadOnlyList<AudioRecord> records, CancellationToken ct);
        Task<IReadOnlyList<AudioRecord>> ReadAudioRecordsAsync(string messageId, CancellationToken ct);
        Task<IReadOnlyList<string>> ReadAllAudioPathsAsync(CancellationToken ct);
        Task ClearAudioRecordsAsync(CancellationToken ct);
    }
    public sealed class AudioCache
    {
        private readonly IAudioRecordStore store;
        private readonly string userRoot;
        private readonly SemaphoreSlim operations = new SemaphoreSlim(1, 1);
        public string Folder { get; }
        public AudioCache(IAudioRecordStore store, string userRoot)
        { this.store = store; this.userRoot = Path.GetFullPath(userRoot); Folder = Path.Combine(this.userRoot, "AudioCache"); }
        public string Resolve(string relative)
        {
            string normalized = (relative ?? "").Replace('\\', '/');
            if (!Regex.IsMatch(normalized, @"^AudioCache/[a-f0-9]{32}/[0-9]{4}\.wav$")) throw new InvalidDataException("Invalid cached audio path.");
            string full = Path.GetFullPath(Path.Combine(userRoot, normalized));
            if (!full.StartsWith(Folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Audio path escapes cache.");
            EnsureNoLinks(Path.GetDirectoryName(full)); return full;
        }
        private static void EnsureNoLinks(string path)
        {
            var entry = new DirectoryInfo(path);
            while (entry != null)
            { if (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked audio folders are unsupported."); entry = entry.Parent; }
        }
        public string AuditPath(string relativeWav) => System.IO.Path.ChangeExtension(Resolve(relativeWav), ".speech.json");
        public async Task<AudioRecord> StageAsync(string session, SpeechSegment segment, byte[] wav, string profileId, CancellationToken ct, SpeechAudit audit = null)
        {
            if (!Regex.IsMatch(session ?? "", "^[a-f0-9]{32}$") || segment.Index < 0 || segment.Index > 9999) throw new ArgumentException("Invalid audio session.");
            var decoded = WaveAudio.Decode(wav);
            string relative = "AudioCache/" + session + "/" + segment.Index.ToString("D4") + ".wav";
            string full = Resolve(relative);
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested(); Directory.CreateDirectory(Path.GetDirectoryName(full));
                    using var output = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    output.Write(wav, 0, wav.Length); output.Flush(true);
                    if (audit != null)
                    {
                        ct.ThrowIfCancellationRequested();
                        byte[] json = System.Text.Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(audit, Newtonsoft.Json.Formatting.Indented));
                        using var provenance = new FileStream(AuditPath(relative), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        provenance.Write(json, 0, json.Length); provenance.Flush(true);
                    }
                }, ct).ConfigureAwait(false);
            }
            finally { operations.Release(); }
            return new AudioRecord { Path = relative, SegmentIndex = segment.Index, DisplayText = segment.DisplayText, Duration = decoded.Duration, ProfileId = profileId };
        }
        public async Task CommitAsync(string requestId, IReadOnlyList<AudioRecord> records, CancellationToken ct)
        {
            if (records.Count == 0) return;
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                for (int i = 0; i < records.Count; i++)
                { if (records[i].SegmentIndex != i || !File.Exists(Resolve(records[i].Path))) throw new InvalidDataException("Audio cache incomplete."); records[i].SegmentCount = records.Count; }
                await store.SaveAudioRecordsAsync(requestId, records, ct).ConfigureAwait(false);
            }
            finally { operations.Release(); }
        }
        public async Task<IReadOnlyList<AudioRecord>> ReadPlayableAsync(string messageId, CancellationToken ct)
        {
            var records = await store.ReadAudioRecordsAsync(messageId, ct).ConfigureAwait(false);
            if (records.Count == 0 || records.Any((r) => r.SegmentCount != records.Count)) return Array.Empty<AudioRecord>();
            for (int i = 0; i < records.Count; i++)
                if (records[i].SegmentIndex != i || !File.Exists(Resolve(records[i].Path))) return Array.Empty<AudioRecord>();
            return records;
        }
        public async Task RecoverAsync(CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var referenced = new HashSet<string>(await store.ReadAllAudioPathsAsync(ct).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase);
                DeleteOwnedFiles(path => !referenced.Contains(path), ct);
            }
            finally { operations.Release(); }
        }
        public async Task ClearAsync(CancellationToken ct)
        {
            await operations.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Remove index first. A crash leaves only recoverable orphan files, never an advertised half-deleted reply.
                await store.ClearAudioRecordsAsync(ct).ConfigureAwait(false); DeleteOwnedFiles(_ => true, ct);
            }
            finally { operations.Release(); }
        }
        public async Task DiscardSessionAsync(string session)
        {
            if (!Regex.IsMatch(session ?? "", "^[a-f0-9]{32}$")) return;
            await operations.WaitAsync().ConfigureAwait(false);
            try
            {
                var referenced = new HashSet<string>(await store.ReadAllAudioPathsAsync(default).ConfigureAwait(false), StringComparer.OrdinalIgnoreCase);
                DeleteOwnedFiles(path => path.StartsWith("AudioCache/" + session + "/", StringComparison.Ordinal) && !referenced.Contains(path), default);
            }
            finally { operations.Release(); }
        }
        public long SizeBytes()
        {
            if (!Directory.Exists(Folder)) return 0;
            return OwnedFiles().Sum(file => new FileInfo(file).Length);
        }
        private IEnumerable<string> OwnedFiles()
        {
            EnsureNoLinks(Folder); if (!Directory.Exists(Folder)) yield break;
            foreach (string dir in Directory.EnumerateDirectories(Folder))
            {
                if (!Regex.IsMatch(Path.GetFileName(dir), "^[a-f0-9]{32}$")) continue;
                EnsureNoLinks(dir);
                foreach (string file in Directory.EnumerateFiles(dir))
                    if (Regex.IsMatch(Path.GetFileName(file), @"^[0-9]{4}\.(wav|speech\.json)$") && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) yield return file;
            }
        }
        private void DeleteOwnedFiles(Func<string, bool> eligible, CancellationToken ct)
        {
            foreach (string full in OwnedFiles().ToArray())
            {
                ct.ThrowIfCancellationRequested(); string relative = "AudioCache/" + Path.GetFileName(Path.GetDirectoryName(full)) + "/" + Path.GetFileName(full);
                string wavRelative = Regex.Replace(relative, @"\.speech\.json$", ".wav");
                if (!eligible(wavRelative)) continue;
                // Resolve and verify each final path before deletion; never recursively delete a player folder.
                string checkedWav = Resolve(wavRelative);
                File.Delete(wavRelative == relative ? checkedWav : Path.Combine(Path.GetDirectoryName(checkedWav), Path.GetFileName(full)));
                string directory = Path.GetDirectoryName(full);
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory, false);
            }
        }
    }
}
