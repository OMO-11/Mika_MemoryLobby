using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace GiftCompanion.Core.Config
{
    public sealed class ConfigResetSelection
    {
        public string Selected { get; private set; }
        public string Armed { get; private set; }
        public void Select(string file) { Selected = file; Cancel(); }
        public void Cancel() => Armed = null;
        public bool ConfirmOrArm()
        {
            if (Selected == null) return false;
            if (Armed == Selected) { Cancel(); return true; }
            Armed = Selected; return false;
        }
    }

    // Preserve every original before removing anything. Recovery copies are never loaded automatically.
    public sealed class PlayerConfigRecovery
    {
        private readonly string root;
        public PlayerConfigRecovery(string dataRoot) => root = Path.GetFullPath(dataRoot);
        public string RecoveryFolder => Path.Combine(root, "ConfigRecovery");
        public static bool CanRestorePrevious(string file) => file == "character_card.json" || file == "bgm_playlist.json" || file == "companion_spaces.json";
        public static string Label(string file)
        {
            switch (file)
            {
                case "character_card.json": return "人物设定";
                case "app_entry.json": return "启动方式与桌宠入口";
                case "companion_display.json": return "画面与日记显示";
                case "companion_visual.json": return "窗边动态与视觉布局";
                case "bgm_playlist.json": return "背景音乐";
                case "companion_spaces.json": return "陪伴空间";
                case "settings.json": return "音量、字幕与基本设置";
                case "voice_runtime.json": return "声音运行设置";
                case "voice_delivery.json": return "声音情绪与表达";
                case "speech_localization.json": return "日语表达设置";
                case "diary_policy.json": return "日记整理设置";
                case "backup_policy.json": return "备份偏好";
                case "pet_settings.json": return "桌宠显示与位置";
                case "agent_tools.json": return "桌面操作权限";
                case "known_apps.json": return "允许打开的应用";
                case "onboarding.json": return "首次使用提示";
                default: return "其他配置（" + file + "）";
            }
        }
        public string[] List() => Directory.GetFiles(Path.Combine(root, "Config"), "*.json").Select(Path.GetFileName).OrderBy(n => n).ToArray();
        private string Source(string file)
        {
            if (string.IsNullOrEmpty(file) || Path.GetFileName(file) != file || !file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("请选择资料目录中的配置文件。");
            string path = Path.Combine(root, "Config", file);
            foreach (string p in new[] { root, Path.GetDirectoryName(path), path })
                if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("请勿通过链接重置配置。");
            return path;
        }
        public string Reset(string file, CancellationToken ct)
        {
            string source = Source(file);
            var paths = new[] { source, source + ".previous.bak", source + ".pending" }.Where(File.Exists).ToArray();
            string directory = Path.Combine(RecoveryFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var originals = new List<FileStream>();
            try
            {
                // Hold all originals read-only until every copy has been flushed and checked.
                foreach (string path in paths)
                {
                    ct.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("配置副本不能是链接。");
                    originals.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
                }
                for (int i = 0; i < paths.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string copy = Path.Combine(directory, Path.GetFileName(paths[i]));
                    using (var output = new FileStream(copy, FileMode.CreateNew, FileAccess.Write)) { originals[i].CopyTo(output); output.Flush(true); }
                    originals[i].Position = 0;
                    using var hash = SHA256.Create(); using var saved = File.OpenRead(copy);
                    if (!hash.ComputeHash(originals[i]).SequenceEqual(hash.ComputeHash(saved))) throw new IOException("配置保留副本校验失败。");
                }
                AtomicDocument.Write(Path.Combine(directory, "恢复说明.md"), "# 配置重置原件\n\n项目：" + file + "\n时间：" + DateTimeOffset.Now.ToString("O") +
                    "\n\n主文件为重置前原件；.previous.bak为更早版本；.pending可能未写完整，不要直接采用。\n关闭应用后，可将需要的主文件复制回资料目录Config。此操作不重置聊天与记忆。\n", ct, false);
                ct.ThrowIfCancellationRequested();
                foreach (var input in originals) input.Dispose(); originals.Clear();
                try { foreach (string path in paths) File.Delete(path); }
                catch
                {
                    foreach (string path in paths) if (!File.Exists(path)) File.Copy(Path.Combine(directory, Path.GetFileName(path)), path);
                    throw;
                }
                return directory;
            }
            finally { foreach (var input in originals) input.Dispose(); }
        }
        public string RestorePrevious(string file, Action<string> validate, CancellationToken ct)
        {
            if (!CanRestorePrevious(file)) throw new InvalidDataException("此项请使用完整备份恢复；人物、音乐和空间可单独恢复上一版。");
            string source = Source(file), previous = source + ".previous.bak";
            if (!File.Exists(previous)) throw new InvalidDataException("还没有上一版可恢复。当前资料未修改。");
            if (new FileInfo(previous).Length > 1048576 || (File.GetAttributes(previous) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("上一版副本无法使用，当前资料未修改。");
            string text = File.ReadAllText(previous); validate(text); ct.ThrowIfCancellationRequested();
            string saved = Reset(file, ct);
            try { AtomicDocument.Write(source, text, ct, false); }
            catch
            {
                foreach (string suffix in new[] { "", ".previous.bak", ".pending" })
                    if (File.Exists(Path.Combine(saved, file + suffix))) File.Copy(Path.Combine(saved, file + suffix), source + suffix, true);
                throw;
            }
            return saved;
        }
    }
}
