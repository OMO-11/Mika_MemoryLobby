using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Agent;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GiftCompanion.AgentPet.Tools
{
    public sealed partial class WindowsAgentPlatform : IAgentPlatform
    {
        private readonly AgentSettingsService settings;
        private readonly Dictionary<string, (IntPtr Window, uint Process)> visibleWindows = new Dictionary<string, (IntPtr, uint)>();
        public WindowsAgentPlatform(AgentSettingsService settings) => this.settings = settings;
        public AgentPreparedAction Prepare(string tool, JObject args)
        {
            var value = (JObject)args.DeepClone(); string description = AgentToolCatalog.Label(tool); bool sensitive = false;
            switch (tool)
            {
                case "open_application":
                    string target = ResolveApplication((string)args["name"]); value["resolved_path"] = target; description = "打开应用：" + (string)args["name"] + "\n" + target; break;
                case "open_folder":
                    string folder = CheckedPath((string)args["path"], true, false); value["path"] = folder; description = "打开文件夹：" + folder; break;
                case "open_file":
                    string file = CheckedPath((string)args["path"], false, true); value["path"] = file; description = "使用系统关联程序打开文档：\n" + file; sensitive = true; break;
                case "search_file":
                    string term = (string)args["name"]; if (string.IsNullOrWhiteSpace(term) || term.Length > 120 || term.IndexOfAny(new[] { '\\', '/', ':', '*', '?' }) >= 0) throw new ArgumentException("请给我一部分文件名，不需要通配符或路径。");
                    if (settings.Value.SearchRoots.Count == 0) throw new ArgumentException("老师，请先在桌面工具菜单选择允许查找的文件夹。"); break;
                case "open_url":
                    string url = (string)args["url"];
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || string.IsNullOrWhiteSpace(uri.Host) || uri.UserInfo.Length > 0 || url.Any(char.IsControl)) throw new ArgumentException("这里只能打开完整的 http 或 https 网页地址。");
                    value["url"] = uri.AbsoluteUri; description = "打开网页：" + uri.AbsoluteUri; break;
                case "focus_application":
                    string id = (string)args["window_id"];
                    if (!TryVisibleWindow(id, out _)) throw new ArgumentException("这个窗口已经变化，请重新查看当前应用。"); description = "切换到刚才查询的应用窗口"; break;
                case "open_system_setting":
                    string page = (string)args["page"]; if (!new[] { "display", "sound", "bluetooth", "network", "apps" }.Contains(page)) throw new ArgumentException("暂时没有接入这个设置页。"); break;
                case "get_clipboard": sensitive = true; description = "读取当前剪贴板的纯文本（最多4000字），并发送给 DeepSeek 用于本轮回答。不会后台监视，也不提取为长期记忆或日记。"; break;
                case "set_clipboard":
                    sensitive = true; string text = (string)args["text"]; description = "把以下文字写入剪贴板（会替换原有文字）：\n\n" + text; break;
                case "set_volume":
                    long percent = (long)args["percent"]; if (percent < 0 || percent > 100) throw new ArgumentException("系统音量应在0到100之间。");
                    sensitive = true; description = "将系统主音量从 " + ReadVolume().Percent + "% 调整到 " + percent + "%。这会影响其他应用。"; break;
                case "mute_volume": sensitive = true; description = (bool)args["muted"] ? "将系统主音量静音。" : "解除系统静音，恢复到当前 " + ReadVolume().Percent + "% 音量。"; break;
                case "get_current_time": case "get_running_apps": break;
                default: throw new ArgumentException("这个桌面操作尚未接入。");
            }
            return new AgentPreparedAction { Tool = tool, Arguments = value, Description = description, Sensitive = sensitive };
        }
        public async Task<AgentResult> ExecuteAsync(AgentPreparedAction action, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); var args = action.Arguments;
            switch (action.Tool)
            {
                case "get_current_time": return AgentResult.Ok("现在是 " + DateTimeOffset.Now.ToString("yyyy年M月d日 HH:mm:ss") + "。", new JObject { ["local_time"] = DateTimeOffset.Now.ToString("O"), ["timezone"] = TimeZoneInfo.Local.Id });
                case "open_application":
                    string exe = (string)args["resolved_path"]; AgentPaths.NoLinks(exe);
                    if (!File.Exists(exe) || !AgentPaths.IsApplication(exe)) return AgentResult.Fail("TargetChanged", "应用路径已经变化，没有打开。");
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
                    return Launched("已发起打开“" + (string)args["name"] + "”；还不能确认窗口已经准备好。");
                case "open_folder": ShellOpen(CheckedPath((string)args["path"], true, false)); return Launched("已发起打开指定文件夹。");
                case "open_file": ShellOpen(CheckedPath((string)args["path"], false, true)); return Launched("已交给系统关联程序打开指定文档；尚未确认加载完成。");
                case "open_url": ShellOpen((string)args["url"]); return Launched("已发起打开指定网页；尚未确认页面加载完成。");
                case "open_system_setting":
                    string page = (string)args["page"]; string nativePage = page == "network" ? "network-status" : page == "apps" ? "appsfeatures" : page;
                    ShellOpen("ms-settings:" + nativePage); return Launched("已发起打开系统设置页。");
                case "search_file": return await Task.Run(() => Search((string)args["name"], ct), ct);
                case "get_running_apps": return RunningApps();
                case "focus_application":
                    if (!TryVisibleWindow((string)args["window_id"], out var window)) return AgentResult.Fail("WindowMissing", "刚才的窗口已经不在了。");
                    if (IsIconic(window)) ShowWindow(window, 9); SetForegroundWindow(window);
                    return GetForegroundWindow() == window ? AgentResult.Ok("已经确认该应用窗口位于前台。", new JObject { ["observation"] = "focus_confirmed" }) : AgentResult.Fail("FocusDenied", "系统没有允许切换前台；窗口可能需要老师手动点击。");
                case "get_clipboard":
                    string clipboard = GUIUtility.systemCopyBuffer ?? "";
                    if (clipboard.Length > 4000) return AgentResult.Fail("ClipboardTooLong", "剪贴板文字超过4000字，这次没有发送。请先选取需要的部分。");
                    return AgentResult.Ok("已在本次确认后读取纯文本。", new JObject { ["text"] = clipboard, ["untrusted_data"] = true });
                case "set_clipboard": GUIUtility.systemCopyBuffer = (string)args["text"]; return GUIUtility.systemCopyBuffer == (string)args["text"] ? AgentResult.Ok("剪贴板文字已替换。", new JObject { ["verified"] = true }) : AgentResult.Fail("ClipboardNotVerified", "没能确认剪贴板已替换，请手动检查。");
                case "set_volume": return ChangeVolume((int)args["percent"], null);
                case "mute_volume": return ChangeVolume(null, (bool)args["muted"]);
                default: return AgentResult.Fail("UnknownTool", "这个操作尚未接入。");
            }
        }
        private static AgentResult Launched(string message) => AgentResult.Ok(message, new JObject { ["observation"] = "launch_requested" });
        private static void ShellOpen(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        private string CheckedPath(string value, bool directory, bool approved)
        {
            string path = AgentPaths.LocalPath(value); AgentPaths.NoLinks(path);
            if (directory ? !Directory.Exists(path) : !File.Exists(path)) throw new ArgumentException("这个目标不存在，请重新选择。");
            if (approved && !settings.Value.SearchRoots.Any(root => AgentPaths.Within(path, root))) throw new ArgumentException("请先把文档所在文件夹加入允许查找的范围，再打开这个文档。");
            if (!directory && !new[] { ".txt", ".md", ".pdf", ".docx", ".xlsx", ".pptx", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".wav", ".mp3", ".ogg", ".mp4" }.Contains(Path.GetExtension(path).ToLowerInvariant())) throw new ArgumentException("暂时只打开常见文档、图片、音视频，不打开程序、脚本或快捷方式。");
            return path;
        }
        public static void ValidateSearchRoot(string value)
        {
            string path = AgentPaths.LocalPath(value); AgentPaths.NoLinks(path);
            if (!Directory.Exists(path) || path.TrimEnd('\\', '/') == Path.GetPathRoot(path).TrimEnd('\\', '/')) throw new ArgumentException("请选择具体的资料文件夹，不选择整个磁盘。");
            foreach (var special in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.System, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            { string reserved = Environment.GetFolderPath(special); if (!string.IsNullOrEmpty(reserved) && AgentPaths.Within(path, reserved)) throw new ArgumentException("请选择自己的资料文件夹，不搜索系统和程序目录。"); }
        }
        private AgentResult Search(string term, CancellationToken ct)
        {
            var roots = settings.Value.SearchRoots.ToArray(); var results = new JArray(); var clock = Stopwatch.StartNew(); int visited = 0; bool limited = false;
            var folders = new Queue<(string path, int depth)>(); foreach (string root in roots) { ValidateSearchRoot(root); folders.Enqueue((root, 0)); }
            while (folders.Count > 0)
            {
                ct.ThrowIfCancellationRequested(); if (clock.ElapsedMilliseconds > 2500 || visited > 6000 || results.Count >= 20) { limited = true; break; }
                var folder = folders.Dequeue();
                try
                {
                    AgentPaths.NoLinks(folder.path);
                    foreach (string entry in Directory.EnumerateFileSystemEntries(folder.path))
                    {
                        ct.ThrowIfCancellationRequested(); if (++visited > 6000 || clock.ElapsedMilliseconds > 2500 || results.Count >= 20) { limited = true; break; }
                        var attributes = File.GetAttributes(entry); string name = Path.GetFileName(entry);
                        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.System)) != 0 || name.StartsWith(".", StringComparison.Ordinal) || new[] { "Secrets", "node_modules", "Library", "$RECYCLE.BIN", "Runtime", "Windows" }.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                        if ((attributes & FileAttributes.Directory) != 0) { if (folder.depth < 5) folders.Enqueue((entry, folder.depth + 1)); else limited = true; }
                        else if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) results.Add(new JObject { ["name"] = name, ["path"] = entry });
                    }
                }
                catch (IOException) { limited = true; } catch (UnauthorizedAccessException) { limited = true; }
            }
            return AgentResult.Ok(results.Count == 0 ? "在允许的范围内暂时没有找到；可调整文件名或文件夹。" : "在允许范围内找到 " + results.Count + " 个文件名。", new JObject { ["files"] = results, ["limited"] = limited, ["needs_followup"] = true, ["untrusted_data"] = true });
        }
        private string ResolveApplication(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name.IndexOfAny(new[] { '\\', '/', ':', '"' }) >= 0) throw new ArgumentException("请使用已登记的应用名称。");
            var known = settings.Applications.Where(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(a => a.Path).ToList();
            if (known.Count == 0)
            {
                foreach (var special in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
                {
                    string root = Environment.GetFolderPath(special); if (!Directory.Exists(root)) continue;
                    var queue = new Queue<(string path, int depth)>(); queue.Enqueue((root, 0)); int seen = 0;
                    while (queue.Count > 0 && seen < 2000)
                    {
                        var item = queue.Dequeue();
                        try
                        {
                            foreach (string entry in Directory.EnumerateFileSystemEntries(item.path))
                            {
                                if (++seen > 2000) break;
                                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) continue;
                                if (Directory.Exists(entry)) { if (item.depth < 5) queue.Enqueue((entry, item.depth + 1)); }
                                else if (Path.GetExtension(entry).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(entry).Equals(name, StringComparison.OrdinalIgnoreCase)) known.Add(entry);
                            }
                        }
                        catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                if (known.Count == 0 && new[] { "notepad", "记事本", "calculator", "计算器" }.Contains(name, StringComparer.OrdinalIgnoreCase))
                    known.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), name == "notepad" || name == "记事本" ? "notepad.exe" : "calc.exe"));
                if (known.Count == 0 && name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                    foreach (string root in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Take(64))
                        try { if (Path.IsPathRooted(root)) { string candidate = Path.Combine(root, name + ".exe"); if (File.Exists(candidate) && AgentPaths.IsApplication(candidate)) known.Add(candidate); } } catch (ArgumentException) { }
            }
            var targets = new List<string>();
            foreach (string candidate in known)
            {
                string target = ResolveShortcut(candidate);
                if (!AgentPaths.IsApplication(target) || !File.Exists(target)) continue; AgentPaths.NoLinks(target); targets.Add(Path.GetFullPath(target));
            }
            targets = targets.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (targets.Count == 0) throw new ArgumentException("还没找到这个应用。老师可以在桌面工具菜单选择程序并给它命名。");
            if (targets.Count > 1) throw new ArgumentException("找到了同名应用，暂时没有打开。请在桌面工具菜单登记老师想用的那一个。");
            return targets[0];
        }
    }
}
