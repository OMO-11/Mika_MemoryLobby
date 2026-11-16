using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GiftCompanion.Core.Config
{
    public static class AtomicDocument
    {
        public static T Read<T>(string path)
        {
            Recover(path);
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Configuration is too large.");
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8), new JsonSerializerSettings { MaxDepth = 32 });
        }
        public static void Write(string path, string content, CancellationToken ct, bool keepBackup = true)
        {
            ct.ThrowIfCancellationRequested(); Recover(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false)); ct.ThrowIfCancellationRequested();
                if (File.Exists(path)) Replace(temporary, path, keepBackup);
                else File.Move(temporary, path);
                if (File.Exists(path + ".pending")) File.Delete(path + ".pending");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Replace(string temporary, string path, bool keepBackup)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            { File.Replace(temporary, path, keepBackup ? path + ".previous.bak" : null); return; }
            // ReplaceFile's metadata merge fails on some encrypted user directories.
            // Same-directory MoveFileEx changes the filename atomically without that merge.
            if (keepBackup)
            {
                string backupTemporary = temporary + ".backup.tmp";
                try { File.Copy(path, backupTemporary); Rename(backupTemporary, path + ".previous.bak"); }
                finally { if (File.Exists(backupTemporary)) File.Delete(backupTemporary); }
            }
            Rename(temporary, path);
        }
        private static void Rename(string source, string destination)
        {
            Recover(destination);
            if (MoveFileEx(source, destination, 0x1 | 0x8)) return;
            int error = Marshal.GetLastWin32Error();
            if (error != 17) throw new System.ComponentModel.Win32Exception(error);
            // Some redirected/encrypted file providers report a cross-device move even
            // within one directory. Keep a checksummed write-ahead copy before overwriting.
            byte[] bytes = File.ReadAllBytes(source);
            var record = new PendingDocument { Version = 1, BytesBase64 = Convert.ToBase64String(bytes), Sha256 = Digest(bytes) };
            DurableBytes(destination + ".pending", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record)));
            DurableBytes(destination, bytes);
            File.Delete(destination + ".pending"); File.Delete(source);
        }
        private sealed class PendingDocument { public int Version; public string BytesBase64, Sha256; }
        private static string Digest(byte[] bytes)
        { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        private static void DurableBytes(string path, byte[] bytes)
        {
            using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            output.Write(bytes, 0, bytes.Length); output.Flush(true);
        }
        public static void Recover(string path)
        {
            string pending = path + ".pending"; if (!File.Exists(pending)) return;
            if (new FileInfo(pending).Length > 4L * 1024 * 1024) throw new InvalidDataException("Pending document is too large.");
            PendingDocument record; byte[] bytes;
            try
            {
                record = JsonConvert.DeserializeObject<PendingDocument>(File.ReadAllText(pending), new JsonSerializerSettings { MaxDepth = 8 });
                if (record == null || record.Version != 1) return;
                bytes = Convert.FromBase64String(record.BytesBase64 ?? "");
                if (Digest(bytes) != record.Sha256) return;
            }
            catch (Exception error) when (error is JsonException || error is FormatException) { return; }
            DurableBytes(path, bytes); File.Delete(pending);
        }
        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(string source, string destination, uint flags);
        public static void WriteJson(string path, object value, CancellationToken ct, bool keepBackup = true) => Write(path, JsonConvert.SerializeObject(value, Formatting.Indented) + "\n", ct, keepBackup);
    }
    public sealed class MusicTrack
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
    }
    public sealed class MusicCatalog
    {
        public int Version { get; set; } = 1;
        public string Repeat { get; set; } = "one";
        public double FadeSeconds { get; set; } = 1.5;
        public int MaximumDurationMinutes { get; set; } = 180;
        public string SelectedTrackId { get; set; }
        public List<MusicTrack> Tracks { get; set; } = new List<MusicTrack>();
    }
    public sealed class SpaceEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Definition { get; set; }
        public bool Builtin { get; set; }
    }
    public sealed class SpaceCatalog
    {
        public int Version { get; set; } = 1;
        public string SelectedSpaceId { get; set; }
        public List<SpaceEntry> Spaces { get; set; } = new List<SpaceEntry>();
    }
    public sealed class SpaceDefinition
    {
        public int Version { get; set; } = 1;
        public string Runtime { get; set; } = "4.2";
        public string Skeleton { get; set; }
        public string Atlas { get; set; }
        public string[] Textures { get; set; } = Array.Empty<string>();
        public string Idle { get; set; } = "Idle_01";
        public float Scale { get; set; } = .01f;
        public bool StraightAlpha { get; set; } = true;
        public bool FitToBounds { get; set; } = true;
        public float CameraX { get; set; }
        public float CameraY { get; set; } = 9.7f;
        public float CameraSize { get; set; } = 9.5f;
    }

    // File-backed player content, independent of Unity, HTTP and the memory database.
    public sealed class ContentLibrary
    {
        private readonly SemaphoreSlim writing = new SemaphoreSlim(1, 1);
        private readonly string root, bundle;
        private MusicCatalog music;
        private SpaceCatalog spaces;
        public string MusicFile => System.IO.Path.Combine(root, "Config", "bgm_playlist.json");
        public string SpacesFile => System.IO.Path.Combine(root, "Config", "companion_spaces.json");
        public string DataDirectory => root;
        public List<string> Warnings { get; } = new List<string>();
        public MusicCatalog Music => Clone(music);
        public SpaceCatalog Spaces => Clone(spaces);
        private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        public ContentLibrary(string userDataPath, string bundledContentPath)
        { root = System.IO.Path.GetFullPath(userDataPath); bundle = System.IO.Path.GetFullPath(bundledContentPath); }
        public Task InitializeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Reload(ct); return Task.CompletedTask;
        }
        public void Reload(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Warnings.Clear();
            music = Load(MusicFile, "bgm_playlist.json", new MusicCatalog(), ValidateMusic, ct);
            spaces = Load(SpacesFile, "companion_spaces.json", new SpaceCatalog(), ValidateSpaces, ct);
            // Merge newly shipped spaces without replacing the player catalog or selection.
            var seedSpacesFile=System.IO.Path.Combine(bundle,"Defaults","companion_spaces.json");
            var bundledSpaces = File.Exists(seedSpacesFile)?AtomicDocument.Read<SpaceCatalog>(seedSpacesFile):new SpaceCatalog();
            bool added = false;
            foreach(var entry in bundledSpaces.Spaces.Where(s=>s.Builtin))
                if(!spaces.Spaces.Any(s=>s.Id==entry.Id)){spaces.Spaces.Add(entry);added=true;}
            if(added && Warnings.Count==0) { ValidateSpaces(spaces); AtomicDocument.WriteJson(SpacesFile,spaces,ct); }
            string guide = System.IO.Path.Combine(bundle, "Defaults", "PlayerGuide.md");
            string playerGuide = System.IO.Path.Combine(root, "使用说明.md");
            if (File.Exists(guide) && !File.Exists(playerGuide)) AtomicDocument.Write(playerGuide, File.ReadAllText(guide, Encoding.UTF8), ct);
        }
        private T Load<T>(string path, string seedName, T fallback, Action<T> validate, CancellationToken ct)
        {
            string seed = System.IO.Path.Combine(bundle, "Defaults", seedName);
            AtomicDocument.Recover(path);
            T defaults = File.Exists(seed) ? AtomicDocument.Read<T>(seed) : fallback;
            validate(defaults);
            if (!File.Exists(path)) { AtomicDocument.WriteJson(path, defaults, ct); return defaults; }
            try { var value = AtomicDocument.Read<T>(path); validate(value); return value; }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException)
            { Warnings.Add(System.IO.Path.GetFileName(path) + " 格式有误，原文件已保留，暂用默认资料。"); return defaults; }
        }
        public string Resolve(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) throw new InvalidDataException("Content path missing.");
            bool bundled = reference.StartsWith("bundle:", StringComparison.Ordinal);
            string relative = bundled ? reference.Substring(7) : reference;
            string boundary = (bundled ? bundle : root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (System.IO.Path.IsPathRooted(relative)) throw new InvalidDataException("Use a relative content path.");
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(boundary, relative));
            if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Content path escapes the library.");
            return full;
        }
        private static void Required(string value, int max)
        { if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new InvalidDataException("Invalid library field."); }
        public void ValidateMusic(MusicCatalog value)
        {
            if (value == null || value.Version != 1 || value.Repeat != "one" || value.Tracks == null || value.Tracks.Count > 128 || value.MaximumDurationMinutes < 1 || value.MaximumDurationMinutes > 1440 ||
                double.IsNaN(value.FadeSeconds) || value.FadeSeconds < .2 || value.FadeSeconds > 8) throw new InvalidDataException("Invalid music catalog.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var track in value.Tracks)
            { if (track == null) throw new InvalidDataException(); Required(track.Id, 80); Required(track.Title, 120); if (!ids.Add(track.Id) || !IsAudio(Resolve(track.Path))) throw new InvalidDataException("Invalid music entry."); }
            if (value.Tracks.Count > 0 && !ids.Contains(value.SelectedTrackId ?? "")) throw new InvalidDataException("Selected music missing.");
        }
        public void ValidateSpaces(SpaceCatalog value)
        {
            if (value == null || value.Version != 1 || value.Spaces == null || value.Spaces.Count > 64) throw new InvalidDataException("Invalid spaces catalog.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in value.Spaces)
            { if (entry == null) throw new InvalidDataException(); Required(entry.Id, 80); Required(entry.Title, 120); if (!ids.Add(entry.Id) || !Resolve(entry.Definition).EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid space entry."); }
            if (value.Spaces.Count > 0 && !ids.Contains(value.SelectedSpaceId ?? "")) throw new InvalidDataException("Selected space missing.");
        }
        public static bool IsAudio(string path) => new[] { ".ogg", ".wav", ".mp3" }.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
        public async Task SaveMusicAsync(MusicCatalog value, CancellationToken ct)
        {
            var copy = Clone(value); ValidateMusic(copy); await writing.WaitAsync(ct);
            try { AtomicDocument.WriteJson(MusicFile, copy, ct); music = copy; } finally { writing.Release(); }
        }
        public async Task SaveSpacesAsync(SpaceCatalog value, CancellationToken ct)
        {
            var copy = Clone(value); ValidateSpaces(copy); await writing.WaitAsync(ct);
            try { AtomicDocument.WriteJson(SpacesFile, copy, ct); spaces = copy; } finally { writing.Release(); }
        }
        public async Task SelectMusicAsync(string id, CancellationToken ct)
        {
            await writing.WaitAsync(ct);
            try
            {
                if (!music.Tracks.Any(t => t.Id == id)) throw new InvalidDataException("Selected music missing.");
                if (music.SelectedTrackId == id) return;
                var next = Music; next.SelectedTrackId = id; ValidateMusic(next);
                AtomicDocument.WriteJson(MusicFile, next, ct); music = next;
            }
            finally { writing.Release(); }
        }
        public async Task SelectSpaceAsync(string id, CancellationToken ct)
        {
            await writing.WaitAsync(ct);
            try
            {
                if (!spaces.Spaces.Any(s => s.Id == id)) throw new InvalidDataException("Selected space missing.");
                if (spaces.SelectedSpaceId == id) return;
                var next = Spaces; next.SelectedSpaceId = id; ValidateSpaces(next);
                AtomicDocument.WriteJson(SpacesFile, next, ct); spaces = next;
            }
            finally { writing.Release(); }
        }
        public async Task<MusicTrack> ImportMusicAsync(string source, CancellationToken ct)
        {
            if (!File.Exists(source) || !IsAudio(source) || new FileInfo(source).Length > 200L * 1024 * 1024) throw new InvalidDataException("请选择 200 MB 以内的 MP3、OGG 或 WAV 音乐。");
            string id = Guid.NewGuid().ToString("N"), relative = "Content/Music/" + id + System.IO.Path.GetExtension(source).ToLowerInvariant();
            string destination = Resolve(relative); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
            using (var input = File.OpenRead(source)) using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, 81920, ct);
            var track = new MusicTrack { Id = id, Title = System.IO.Path.GetFileNameWithoutExtension(source), Path = relative };
            if (track.Title.Length > 120) track.Title = track.Title.Substring(0, 120);
            await writing.WaitAsync(ct);
            try
            {
                var next = Music; next.Tracks.Add(track); if (next.Tracks.Count == 1) next.SelectedTrackId = id;
                ValidateMusic(next); AtomicDocument.WriteJson(MusicFile, next, ct); music = next;
            }
            finally { writing.Release(); }
            return track;
        }
        public SpaceDefinition ReadSpace(SpaceEntry entry) => ReadSpaceAt(Resolve(entry.Definition));
        public static SpaceDefinition ReadSpaceAt(string manifest)
        {
            var definition = AtomicDocument.Read<SpaceDefinition>(manifest);
            ValidateSpaceDefinition(definition, System.IO.Path.GetDirectoryName(manifest)); return definition;
        }
        private static string PackageFile(string folder, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != System.IO.Path.GetFileName(name) || name.Contains("/") || name.Contains("\\")) throw new InvalidDataException("Space package uses a non-local filename.");
            string path = System.IO.Path.Combine(folder, name);
            if (!File.Exists(path) || new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidDataException("Space package file missing or too large.");
            return path;
        }
        public static void ValidateSpaceDefinition(SpaceDefinition value, string folder)
        {
            if (value == null || value.Version != 1 || value.Runtime != "4.2" || value.Textures == null || value.Textures.Length < 1 || value.Textures.Length > 16 ||
                float.IsNaN(value.Scale) || value.Scale <= 0 || value.Scale > 10 || float.IsNaN(value.CameraX) || float.IsInfinity(value.CameraX) ||
                float.IsNaN(value.CameraY) || float.IsInfinity(value.CameraY) || float.IsNaN(value.CameraSize) || value.CameraSize <= .01f || value.CameraSize > 10000) throw new InvalidDataException("Unsupported Spine space definition.");
            Required(value.Idle, 120); PackageFile(folder, value.Skeleton); PackageFile(folder, value.Atlas);
            foreach (string file in value.Textures) { PackageFile(folder, file); if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Use PNG atlas pages."); }
            var pages = File.ReadAllLines(PackageFile(folder, value.Atlas)).Select(x => x.Trim()).Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (pages.Length == 0 || pages.Any(x => !value.Textures.Contains(x, StringComparer.Ordinal))) throw new InvalidDataException("Atlas pages are incomplete.");
        }
        public static SpaceDefinition InspectPackage(string folder)
        {
            string manifest = System.IO.Path.Combine(folder, "space.json");
            if (File.Exists(manifest)) return ReadSpaceAt(manifest);
            var all = Directory.GetFiles(folder);
            var skeletons = all.Where(x => x.EndsWith(".skel", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase)).ToArray();
            var atlases = all.Where(x => x.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".atlas.txt", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (skeletons.Length != 1 || atlases.Length != 1) throw new InvalidDataException("请选择包含一个 Spine 二进制骨架和一个 atlas 的文件夹，或附带 space.json。");
            var definition = new SpaceDefinition { Skeleton = System.IO.Path.GetFileName(skeletons[0]), Atlas = System.IO.Path.GetFileName(atlases[0]),
                Textures = File.ReadAllLines(atlases[0]).Select(x => x.Trim()).Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray() };
            ValidateSpaceDefinition(definition, folder); return definition;
        }
        public async Task<SpaceEntry> ImportSpaceAsync(string folder, Action<SpaceDefinition, string> validateRuntime, CancellationToken ct)
        {
            var definition = InspectPackage(folder); validateRuntime?.Invoke(definition, folder); ct.ThrowIfCancellationRequested();
            string id = Guid.NewGuid().ToString("N"), relative = "Content/Spaces/" + id;
            string destination = Resolve(relative + "/space.json"); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
            foreach (string name in definition.Textures.Concat(new[] { definition.Skeleton, definition.Atlas }).Distinct())
            {
                using var input = File.OpenRead(PackageFile(folder, name));
                using var output = new FileStream(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination), name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output, 81920, ct);
            }
            AtomicDocument.WriteJson(destination, definition, ct);
            var entry = new SpaceEntry { Id = id, Title = new DirectoryInfo(folder).Name, Definition = relative + "/space.json" };
            if (entry.Title.Length > 120) entry.Title = entry.Title.Substring(0, 120);
            await writing.WaitAsync(ct);
            try
            {
                var next = Spaces; next.Spaces.Add(entry); if (next.Spaces.Count == 1) next.SelectedSpaceId = entry.Id;
                ValidateSpaces(next); AtomicDocument.WriteJson(SpacesFile, next, ct); spaces = next;
            }
            finally { writing.Release(); }
            return entry;
        }
        public string MaintenanceReport()
        {
            var text = new StringBuilder("# 陪伴资料检查\n\n检查时间：" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n");
            foreach (var warning in Warnings) text.AppendLine("- " + warning);
            foreach (var track in music.Tracks) text.AppendLine("- 音乐「" + track.Title + "」：" + (File.Exists(Resolve(track.Path)) ? "文件存在" : "文件缺失，请重新添加"));
            foreach (var entry in spaces.Spaces)
            {
                try { ReadSpace(entry); text.AppendLine("- 空间「" + entry.Title + "」：文件齐全"); }
                catch { text.AppendLine("- 空间「" + entry.Title + "」：请检查资源包"); }
            }
            text.AppendLine("\n歌单：Config/bgm_playlist.json\n空间清单：Config/companion_spaces.json\n人物卡：Config/character_card.json\n回忆日记：Diary/\n\n这些文件可由菜单管理。修改后点击重新读取；上一次保存保留为 .previous.bak。检查只核对本机资料，不替代源代码审计。\n");
            text.AppendLine("声音语言 / 开关 / 音量：Config/settings.json\n语音运行参数：Config/voice_runtime.json\n日语本地化规则：Config/speech_localization.json（重启生效）\n语音与中日朗读记录：AudioCache/*.wav 及同目录 *.speech.json\n双语声音说明：双语声音说明.md\n声音设置可通过设置菜单管理，历史复听保留原语言。\n");
            return text.ToString();
        }
    }
}
