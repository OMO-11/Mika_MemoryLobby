using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.Persistence
{
    public sealed class BackupPolicy
    {
        public int Version { get; set; } = 1;
        public bool IncludeAudio { get; set; }
    }
    public sealed class BackupFile
    {
        public string Path { get; set; }
        public long Bytes { get; set; }
        public string Sha256 { get; set; }
    }
    public sealed class BackupManifest
    {
        public int Version { get; set; } = 1;
        public string Application { get; set; } = "GiftCompanion-Phase6";
        public int Schema { get; set; }
        public string CharacterId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IncludesAudio { get; set; }
        public List<BackupFile> Files { get; set; } = new List<BackupFile>();
        public string[] Dependencies { get; set; } = { "Runtime/GPT-SoVITS/runtime/python.exe", "Runtime/GPT-SoVITS/voice.json", "Runtime/GPT-SoVITS/voice-ja.json", "Runtime/GPT-SoVITS/tts_infer.yaml", "Runtime/GPT-SoVITS/tts_infer-ja.yaml" };
    }
    public sealed class BackupPreview
    {
        public string StageId { get; set; }
        public BackupManifest Manifest { get; set; }
        public int MissingDependencies { get; set; }
        public int ExternalPaths { get; set; }
        public RestoreSpaceEstimate Space { get; set; }
        public string BackupVolume { get; set; }
        public string Description => $"备份时间：{Manifest.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm}　来源盘：{BackupVolume}\n{Manifest.Files.Count}个文件，{Manifest.Files.Sum(f => f.Bytes) / 1048576d:F1} MiB；" +
            (Manifest.IncludesAudio ? "含历史语音。" : "不含历史语音，部分旧记录无法复听，不会自动补生成。") +
            $"\n缺失安装依赖：{MissingDependencies}；需核对外部路径：{ExternalPaths}。\n将替换当前资料并回到备份时刻，包括当时的遗忘状态。当前资料先保留恢复点。Key不迁移；应用、搜索范围与桌面工具需重新设置。\n" + Space?.Description;
    }

    // Only data roots below are eligible. Secrets, Logs, Validation, Runtime and backups never enter a snapshot.
    public sealed class PlayerBackup
    {
        public static readonly string[] DataRoots = { "memory.db", "Config", "Content", "VoiceReferences", "Diary", "AudioCache" };
        internal static readonly string[] SwitchRoots = DataRoots.Concat(new[] { "memory.db-wal", "memory.db-shm" }).ToArray();
        private const int MaximumFiles = 30000;
        private const long MaximumBytes = 32L * 1024 * 1024 * 1024, MaximumFileBytes = 2L * 1024 * 1024 * 1024;
        private readonly string root, installation, characterId;
        private readonly Func<string, long?> availableSpace;
        public PlayerBackup(string dataRoot, string installRoot, string character, Func<string, long?> availableSpace = null)
        { root = Path.GetFullPath(dataRoot); installation = Path.GetFullPath(installRoot); characterId = character; this.availableSpace = availableSpace ?? RestoreSpaceEstimate.Available; }
        public BackupPolicy LoadPolicy(CancellationToken ct)
        {
            string path = Path.Combine(root, "Config", "backup_policy.json");
            if (!File.Exists(path)) AtomicDocument.WriteJson(path, new BackupPolicy(), ct);
            try { var p = AtomicDocument.Read<BackupPolicy>(path); return p?.Version == 1 ? p : new BackupPolicy(); }
            catch (Exception e) when (e is IOException || e is JsonException) { return new BackupPolicy(); }
        }
        public void SavePolicy(bool audio, CancellationToken ct) => AtomicDocument.WriteJson(Path.Combine(root, "Config", "backup_policy.json"), new BackupPolicy { IncludeAudio = audio }, ct);
        public long AudioBytes() => Directory.Exists(Path.Combine(root, "AudioCache")) ? Enumerate(Path.Combine(root, "AudioCache")).Sum(p => new FileInfo(p).Length) : 0;

        // Caller owns the maintenance lease: core paused, background drained, voice stopped, state saved.
        public async Task<BackupManifest> CreateAsync(string output, bool audio, DatabaseService db, CancellationToken ct)
        {
            output = Path.GetFullPath(output);
            if (!output.EndsWith(".mikabackup", StringComparison.OrdinalIgnoreCase) || File.Exists(output)) throw new InvalidDataException("请选择新的 .mikabackup 文件名，已有备份不会被覆盖。");
            foreach (string item in DataRoots.Concat(new[] { "Secrets", "RestoreStaging" }))
                if (Within(output, Path.Combine(root, item)) || string.Equals(output, Path.Combine(root, item), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("请把备份存到资料范围之外。");
            string work = OwnPath("BackupWork", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
            string partial = output + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                string database = Path.Combine(work, "memory.db");
                await db.BackupAsync(database, ct).ConfigureAwait(false);
                DatabaseService.VerifyBackup(database, db.SchemaVersion, characterId);
                return await Task.Run(() =>
                {
                    var manifest = new BackupManifest { Schema = db.SchemaVersion, CharacterId = characterId, CreatedAt = DateTimeOffset.UtcNow, IncludesAudio = audio };
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                    {
                        Add(zip, database, "memory.db", manifest, ct);
                        foreach (string folder in DataRoots.Skip(1).Where(n => n != "AudioCache" || audio))
                        {
                            string directory = Path.Combine(root, folder); if (!Directory.Exists(directory)) continue;
                            foreach (string path in Enumerate(directory))
                            {
                                string relative = folder + "/" + path.Substring(directory.Length + 1).Replace('\\', '/');
                                if (relative.EndsWith(".pending", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(".previous.bak", StringComparison.OrdinalIgnoreCase) || relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                                ValidateRelative(relative, audio); Add(zip, path, relative, manifest, ct);
                            }
                        }
                        var entry = zip.CreateEntry("backup_manifest.json", CompressionLevel.Fastest);
                        using var writer = new StreamWriter(entry.Open()); writer.Write(JsonConvert.SerializeObject(manifest, Formatting.Indented));
                    }
                    ct.ThrowIfCancellationRequested(); File.Move(partial, output); return manifest;
                }, ct).ConfigureAwait(false);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); DeleteOwned(work, Path.Combine(root, "BackupWork")); }
        }
        private static void Add(ZipArchive zip, string source, string relative, BackupManifest manifest, CancellationToken ct)
        {
            NoLinks(source); var before = new FileInfo(source); long size = before.Length; DateTime stamp = before.LastWriteTimeUtc;
            if (size > MaximumFileBytes || manifest.Files.Count >= MaximumFiles || manifest.Files.Sum(f => f.Bytes) > MaximumBytes - size) throw new InvalidDataException("资料超过首版备份上限（单文件2GiB、总32GiB、3万个文件）。");
            var entry = zip.CreateEntry(relative, relative.StartsWith("AudioCache/") ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = entry.Open(); using var sha = SHA256.Create();
            Copy(input, output, sha, size, ct); string hash = Hex(sha.Hash);
            var after = new FileInfo(source);
            if (after.Length != size || after.LastWriteTimeUtc != stamp) throw new IOException("资料在备份过程中被其他程序修改，请稍后重试。");
            manifest.Files.Add(new BackupFile { Path = relative, Bytes = size, Sha256 = hash });
        }
        public Task<BackupPreview> InspectAsync(string archive, CancellationToken ct) => Task.Run(() => Inspect(archive, ct), ct);
        private BackupPreview Inspect(string archive, CancellationToken ct)
        {
            string id = Guid.NewGuid().ToString("N"), stage = OwnPath("RestoreStaging", id); Directory.CreateDirectory(stage);
            try
            {
                using var input = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var zip = new ZipArchive(input, ZipArchiveMode.Read);
                if (zip.Entries.Count > MaximumFiles + 1) throw new InvalidDataException("备份文件数量过多。");
                var me = zip.GetEntry("backup_manifest.json");
                if (me == null || me.Length > 8 * 1048576) throw new InvalidDataException("备份清单缺失或过大。");
                BackupManifest m; using (var reader = new StreamReader(me.Open())) m = JsonConvert.DeserializeObject<BackupManifest>(reader.ReadToEnd(), new JsonSerializerSettings { MaxDepth = 16, MissingMemberHandling = MissingMemberHandling.Error });
                ValidateManifest(m);
                RestoreSpaceEstimate.Calculate(0, m.Files.Sum(f => f.Bytes), 0, availableSpace(root), Path.GetPathRoot(root), false).EnsureAvailable();
                if (zip.Entries.Count != m.Files.Count + 1 || zip.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != zip.Entries.Count) throw new InvalidDataException("备份包含重复或未登记文件。");
                string data = Path.Combine(stage, "data"); Directory.CreateDirectory(data);
                foreach (var f in m.Files)
                {
                    ct.ThrowIfCancellationRequested(); var entry = zip.GetEntry(f.Path);
                    if (entry == null || entry.Length != f.Bytes || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("备份文件大小或类型不符。");
                    string target = Path.Combine(data, f.Path.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using var stream = entry.Open(); using var file = new FileStream(target, FileMode.CreateNew); using var sha = SHA256.Create();
                    Copy(stream, file, sha, f.Bytes, ct);
                    if (!string.Equals(Hex(sha.Hash), f.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("备份文件哈希校验失败。");
                }
                DatabaseService.VerifyBackup(Path.Combine(data, "memory.db"), m.Schema, characterId);
                var card = AtomicDocument.Read<GiftCompanion.Core.Character.CharacterIdentity>(Path.Combine(data, "Config", "character_card.json"));
                if (card?.Id != characterId) throw new InvalidDataException("人物卡角色与备份不匹配。");
                AtomicDocument.WriteJson(Path.Combine(stage, "manifest.json"), m, ct, false);
                int external = 0;
                foreach (string path in Directory.Exists(Path.Combine(data, "Config")) ? Enumerate(Path.Combine(data, "Config")).Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) : Enumerable.Empty<string>())
                {
                    if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("配置文件过大。");
                    var json = JToken.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    // Prompts can contain newlines/quotes: never pass arbitrary prose to Windows path APIs.
                    external += json.SelectTokens("$..*").OfType<JValue>().Count(v => v.Type == JTokenType.String && LooksLikeExternalPath((string)v.Value));
                }
                var preview = new BackupPreview { StageId = id, Manifest = m, BackupVolume = Path.GetPathRoot(Path.GetFullPath(archive)), ExternalPaths = external, MissingDependencies = m.Dependencies.Count(p => !File.Exists(Path.Combine(installation, p))) };
                preview.Space = EstimateRestore(preview, ct); return preview;
            }
            catch { DeleteOwned(stage, Path.Combine(root, "RestoreStaging")); throw; }
        }
        internal void ValidateManifest(BackupManifest m)
        {
            if (m == null || m.Version != 1 || m.Application != "GiftCompanion-Phase6" || m.Schema < 1 || m.Schema > DatabaseService.CurrentSchemaVersion || m.CharacterId != characterId || m.Files == null || m.Files.Count < 1 || m.Files.Count > MaximumFiles || m.Dependencies == null || !m.Dependencies.SequenceEqual(new BackupManifest().Dependencies)) throw new InvalidDataException("备份格式、角色或版本不兼容。");
            if (m.Files.Any(f => f == null || f.Bytes < 0 || f.Bytes > MaximumFileBytes || f.Sha256 == null || !System.Text.RegularExpressions.Regex.IsMatch(f.Sha256, "^[a-fA-F0-9]{64}$")) || m.Files.Sum(f => f.Bytes) > MaximumBytes || m.Files.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != m.Files.Count || !m.Files.Any(f => f.Path == "memory.db")) throw new InvalidDataException("备份清单内容无效。");
            foreach (var f in m.Files) ValidateRelative(f.Path, m.IncludesAudio);
            if (!m.Files.Any(f => f.Path == "Config/character_card.json")) throw new InvalidDataException("备份缺少人物卡。");
        }
        internal static void ValidateRelative(string path, bool audio)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 220 || path.Contains('\\') || path.StartsWith("/") || path.Split('/').Any(p => p.Length == 0 || p == "." || p == ".." || p.EndsWith(".") || p.EndsWith(" ") || p.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || System.Text.RegularExpressions.Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) throw new InvalidDataException("备份路径无效。");
            string top = path.Split('/')[0];
            if (path == "memory.db") return;
            if (!DataRoots.Skip(1).Contains(top) || !path.Contains('/') || top == "AudioCache" && !audio) throw new InvalidDataException("备份包含不允许的资料范围。");
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string[] allowed = top == "Config" ? new[] { ".json" } : top == "Diary" ? new[] { ".md" } : top == "VoiceReferences" || top == "AudioCache" ? new[] { ".wav", ".json", ".mp3", ".ogg" } : new[] { ".json", ".atlas", ".txt", ".skel", ".bytes", ".png", ".jpg", ".jpeg", ".wav", ".mp3", ".ogg" };
            if (!allowed.Contains(ext)) throw new InvalidDataException("备份包含首版不支持的文件类型：" + ext);
        }
        public void Discard(BackupPreview preview) { if (preview != null) DeleteOwned(OwnPath("RestoreStaging", preview.StageId), Path.Combine(root, "RestoreStaging")); }
        public void Schedule(BackupPreview preview, CancellationToken ct)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            VerifyStage(preview.StageId, ct);
            EstimateRestore(preview, ct, false).EnsureAvailable();
            AtomicDocument.WriteJson(Path.Combine(root, "pending_restore.json"), new RestoreRequest { StageId = preview.StageId }, ct, false);
        }
        public RestoreSpaceEstimate EstimateRestore(BackupPreview preview, CancellationToken ct, bool includeBackup = true)
        {
            long current = 0, database = 0;
            foreach (string item in SwitchRoots)
            {
                string path = Path.Combine(root, item);
                if (File.Exists(path)) { NoLinks(path); long bytes = new FileInfo(path).Length; current = checked(current + bytes); if (item.StartsWith("memory.db", StringComparison.Ordinal)) database = checked(database + bytes); }
                else if (Directory.Exists(path)) foreach (string file in Enumerate(path)) { ct.ThrowIfCancellationRequested(); current = checked(current + new FileInfo(file).Length); }
            }
            return RestoreSpaceEstimate.Calculate(current, preview.Manifest.Files.Sum(f => f.Bytes), database, availableSpace(root), Path.GetPathRoot(root), includeBackup);
        }
        internal void VerifyStage(string id, CancellationToken ct)
        {
            string stage = OwnPath("RestoreStaging", id); NoLinks(stage);
            string manifestPath = Path.Combine(stage, "manifest.json");
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 8 * 1048576) throw new InvalidDataException("恢复清单缺失或过大。");
            var m = JsonConvert.DeserializeObject<BackupManifest>(File.ReadAllText(manifestPath), new JsonSerializerSettings { MaxDepth = 16, MissingMemberHandling = MissingMemberHandling.Error }); ValidateManifest(m);
            string data = Path.Combine(stage, "data");
            if (Enumerate(data).Count() != m.Files.Count) throw new InvalidDataException("恢复暂存内容已变化。");
            foreach (var f in m.Files)
            {
                ct.ThrowIfCancellationRequested(); string p = Path.Combine(data, f.Path); NoLinks(p);
                if (!File.Exists(p) || new FileInfo(p).Length != f.Bytes || Hash(p) != f.Sha256.ToLowerInvariant()) throw new InvalidDataException("恢复暂存校验失败。");
            }
            DatabaseService.VerifyBackup(Path.Combine(data, "memory.db"), m.Schema, characterId);
        }
        internal string OwnPath(string folder, string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("维护记录编号无效。");
            string p = Path.Combine(root, folder, id); CheckAncestors(p, root); return p;
        }
        internal static IEnumerable<string> Enumerate(string directory)
        {
            NoLinks(directory);
            foreach (var f in Directory.EnumerateFiles(directory)) { NoLinks(f); yield return f; }
            foreach (var d in Directory.EnumerateDirectories(directory)) foreach (var f in Enumerate(d)) yield return f;
        }
        internal static bool Within(string path, string directory) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static bool LooksLikeExternalPath(string value) => value != null && (value.StartsWith(@"\\", StringComparison.Ordinal) || value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'));
        internal static void NoLinks(string path) { if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("资料路径不能是符号链接或目录联接。"); }
        internal static void CheckAncestors(string path, string boundary)
        {
            if (!Within(path, boundary)) throw new InvalidDataException("维护路径超出资料目录。");
            for (string p = path; p != null && (Within(p, boundary) || string.Equals(p, boundary, StringComparison.OrdinalIgnoreCase)); p = Path.GetDirectoryName(p)) NoLinks(p);
        }
        internal static void DeleteOwned(string path, string boundary)
        { CheckAncestors(path, boundary); if (Directory.Exists(path)) { foreach (string f in Enumerate(path)) { } Directory.Delete(path, true); } }
        internal static string Hash(string path) { using var sha = SHA256.Create(); using var f = File.OpenRead(path); return Hex(sha.ComputeHash(f)); }
        private static string Hex(byte[] hash) => BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        private static void Copy(Stream input, Stream output, HashAlgorithm hash, long expected, CancellationToken ct)
        {
            var buffer = new byte[81920]; long total = 0; int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) != 0)
            { ct.ThrowIfCancellationRequested(); total += n; if (total > expected) throw new InvalidDataException("备份解压大小超出清单。"); output.Write(buffer, 0, n); hash.TransformBlock(buffer, 0, n, buffer, 0); }
            if (total != expected) throw new InvalidDataException("备份文件长度不符。"); hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        }
    }
    internal sealed class RestoreRequest { public string StageId { get; set; } }
    internal sealed class RestoreJournal
    {
        public string StageId { get; set; }
        public string PointId { get; set; }
        public bool Committed { get; set; }
        public bool CopySnapshotComplete { get; set; }
        public string[] OriginalRoots { get; set; }
    }
    public static class PlayerRestore
    {
        // Runs before opening any database/logger/config. An unfinished transaction is rolled back first.
        public static string ApplyPending(string root, string install, string character)
        {
            root = Path.GetFullPath(root); Directory.CreateDirectory(root);
            var backup = new PlayerBackup(root, install, character);
            string journalFile = Path.Combine(root, "restore_transaction.json"), pendingFile = Path.Combine(root, "pending_restore.json");
            AtomicDocument.Recover(journalFile); AtomicDocument.Recover(pendingFile);
            if (File.Exists(journalFile))
            {
                var old = AtomicDocument.Read<RestoreJournal>(journalFile);
                if (old == null) throw new InvalidDataException("恢复事务记录损坏，请保留资料并联系维护者。");
                if (!old.Committed) Rollback(root, backup, old);
                else DiscardCommittedStage(backup, old.StageId);
                if (File.Exists(pendingFile)) File.Delete(pendingFile);
                File.Delete(journalFile);
            }
            if (!File.Exists(pendingFile)) return null;
            RestoreJournal journal = null;
            try
            {
                var request = AtomicDocument.Read<RestoreRequest>(pendingFile);
                backup.VerifyStage(request?.StageId, CancellationToken.None);
                var stagedManifest = AtomicDocument.Read<BackupManifest>(Path.Combine(backup.OwnPath("RestoreStaging", request.StageId), "manifest.json"));
                backup.EstimateRestore(new BackupPreview { Manifest = stagedManifest }, CancellationToken.None, false).EnsureAvailable();
                string stage = Path.Combine(backup.OwnPath("RestoreStaging", request.StageId), "data");
                ResetMachinePermissions(stage);
                var prepared = new RestoreJournal { StageId = request.StageId, PointId = Guid.NewGuid().ToString("N"), OriginalRoots = PlayerBackup.SwitchRoots.Where(n => Exists(Path.Combine(root, n))).ToArray(), CopySnapshotComplete = true };
                string previous = Path.Combine(backup.OwnPath("RestorePoints", prepared.PointId), "previous"); Directory.CreateDirectory(previous);
                // Some encrypted/redirected Windows data directories reject Directory.Move.
                // Finish durable, verified copies BEFORE publishing a journal or altering any current root.
                foreach (string item in prepared.OriginalRoots)
                {
                    string source = Path.Combine(root, item); PlayerBackup.CheckAncestors(source, root);
                    Copy(source, Path.Combine(previous, item));
                }
                journal = prepared;
                AtomicDocument.WriteJson(journalFile, journal, CancellationToken.None, false);
                foreach (string item in PlayerBackup.SwitchRoots)
                {
                    string current = Path.Combine(root, item), old = Path.Combine(previous, item), replacement = Path.Combine(stage, item);
                    PlayerBackup.CheckAncestors(current, root);
                    if (Exists(current)) Remove(current, root);
                    if (Exists(replacement)) Copy(replacement, current);
                }
                var committed = new RestoreJournal { StageId = journal.StageId, PointId = journal.PointId, OriginalRoots = journal.OriginalRoots, CopySnapshotComplete = true, Committed = true };
                AtomicDocument.WriteJson(journalFile, committed, CancellationToken.None, false); journal.Committed = true;
                File.Delete(pendingFile); File.Delete(journalFile);
                DiscardCommittedStage(backup, request.StageId);
                return "资料已恢复；本机Key保留。桌面应用、搜索范围和工具已重置，请重新确认。恢复前资料保留于RestorePoints。";
            }
            catch (Exception error)
            {
                if (journal != null && !journal.Committed) Rollback(root, backup, journal);
                if (File.Exists(pendingFile)) File.Delete(pendingFile);
                if (File.Exists(journalFile)) File.Delete(journalFile);
                try
                {
                    var method = new System.Diagnostics.StackTrace(error).GetFrame(0)?.GetMethod();
                    AtomicDocument.WriteJson(Path.Combine(root, "Diagnostics", "restore-failure.json"), new { errorType = error.GetType().Name, code = error.HResult, site = method?.DeclaringType?.FullName + "." + method?.Name, currentDataPreserved = true }, CancellationToken.None, false);
                }
                catch (Exception) { /* Keep the recoverable data even when diagnostics cannot be written. */ }
                return "这份备份没有完成恢复，当前资料已保留。请重新选择有效备份。";
            }
        }
        private static void ResetMachinePermissions(string stage)
        {
            string config = Path.Combine(stage, "Config"); Directory.CreateDirectory(config);
            var tools = new GiftCompanion.Core.Agent.AgentSettings();
            foreach (string key in tools.Enabled.Keys.ToArray()) tools.Enabled[key] = false;
            AtomicDocument.WriteJson(Path.Combine(config, "agent_tools.json"), tools, CancellationToken.None, false);
            AtomicDocument.WriteJson(Path.Combine(config, "known_apps.json"), Array.Empty<object>(), CancellationToken.None, false);
            string pet = Path.Combine(config, "pet_settings.json");
            if (File.Exists(pet)) { var json = AtomicDocument.Read<JObject>(pet); json["HasPosition"] = false; AtomicDocument.WriteJson(pet, json, CancellationToken.None, false); }
        }
        private static void DiscardCommittedStage(PlayerBackup backup, string id)
        {
            try { backup.Discard(new BackupPreview { StageId = id }); }
            catch (IOException) { /* A locked temporary stage may be removed later; never roll back committed data for cleanup. */ }
            catch (UnauthorizedAccessException) { }
        }
        private static void Rollback(string root, PlayerBackup backup, RestoreJournal j)
        {
            if (j.OriginalRoots == null || j.OriginalRoots.Any(n => !PlayerBackup.SwitchRoots.Contains(n))) throw new InvalidDataException("恢复回滚范围无效。");
            string previous = Path.Combine(backup.OwnPath("RestorePoints", j.PointId), "previous");
            if (j.CopySnapshotComplete && j.OriginalRoots.Any(n => !Exists(Path.Combine(previous, n)))) throw new InvalidDataException("原资料恢复点不完整，请保留事务记录并联系维护者。");
            foreach (string name in PlayerBackup.SwitchRoots)
            {
                string current = Path.Combine(root, name), old = Path.Combine(previous, name);
                PlayerBackup.CheckAncestors(current, root); PlayerBackup.CheckAncestors(old, root);
                if (Exists(old)) { Remove(current, root); if (j.CopySnapshotComplete) Copy(old, current); else Move(old, current); }
                else if (!j.OriginalRoots.Contains(name)) Remove(current, root);
            }
        }
        private static bool Exists(string p) => File.Exists(p) || Directory.Exists(p);
        private static void Move(string from, string to) { if (Directory.Exists(from)) Directory.Move(from, to); else File.Move(from, to); }
        private static void Copy(string source, string target)
        {
            PlayerBackup.NoLinks(source);
            if (Directory.Exists(source))
            {
                Directory.CreateDirectory(target);
                foreach (string file in PlayerBackup.Enumerate(source)) Copy(file, Path.Combine(target, file.Substring(source.Length + 1)));
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { input.CopyTo(output, 81920); output.Flush(true); }
            if (PlayerBackup.Hash(source) != PlayerBackup.Hash(target)) throw new IOException("恢复点文件复制校验失败。");
        }
        private static void Remove(string p, string root) { if (Directory.Exists(p)) PlayerBackup.DeleteOwned(p, root); else if (File.Exists(p)) File.Delete(p); }
    }
}
