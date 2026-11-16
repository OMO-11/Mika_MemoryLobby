using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.AgentPet;
using GiftCompanion.AgentPet.Window;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Agent;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.Validation
{
    // Explicit, isolated published-build probe. Never reads the normal clipboard or changes system audio.
    public sealed class Phase5PlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-phase5-probe"); if (index < 0) { enabled = false; return; }
            if (index + 2 >= args.Length) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[index + 1]), mode = args[index + 2]; Directory.CreateDirectory(reports);
            string stage = "startup", failure = null; bool passed = false; var flags = new Dictionary<string, bool>();
            var root = AppRoot.Instance;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken); timeout.CancelAfter(TimeSpan.FromMinutes(5)); var ct = timeout.Token;
                await root.Initialization; await Until(() => root.IsReady && !root.Controller.IsSwitching && SceneManager.GetActiveScene().name == "Home", ct);
                var core = root.Character; var state = core.State; stage = "pet";
                await root.Controller.SwitchModeAsync(CharacterMode.AgentPet, ct); await Until(() => FindFirstObjectByType<PetScreen>()?.Ready == true && root.GetComponent<DesktopWindowPrototype>().IsPetActive, ct); await Task.Delay(1600, ct);
                var screen = FindFirstObjectByType<PetScreen>(); var host = root.GetComponent<DesktopWindowPrototype>();
                flags["singleSharedCore"] = ReferenceEquals(core, root.Character) && FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length == 1;
                flags["native600x500"] = Screen.width == 600 && Screen.height == 500;
                flags["transparentCamera"] = Camera.main.backgroundColor.a == 0;
                flags["hotkeyRegistered"] = host.HotkeyIssue == null;
                PetHotkey.TryParse(root.Config.Configuration.PetHotkey, out var defaultKey);
                using (var competitor = new PetHotkeyHost())
                {
                    flags["hotkeyConflictRejected"] = !await competitor.RegisterAsync(defaultKey);
                    flags["hotkeySwitch"] = await host.ChangeHotkeyAsync("Ctrl+Alt+F10", ct);
                    flags["oldHotkeyReleased"] = await competitor.RegisterAsync(defaultKey); await competitor.RegisterAsync(null);
                    flags["hotkeyRestore"] = await host.ChangeHotkeyAsync("Ctrl+Alt+Space", ct);
                }
                stage = "input"; screen.Hide();
                if (host.HotkeyIssue == null && !PetNative.LeftDown && (PetNative.GetAsyncKeyState(0x11) & 0x8000) == 0 && (PetNative.GetAsyncKeyState(0x12) & 0x8000) == 0)
                {
                    // This exact combination is registered to our own probe, including when another window has focus.
                    keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x20, 0, 0, UIntPtr.Zero);
                    keybd_event(0x20, 0, 2, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
                    await Task.Delay(500, ct); flags["physicalHotkeyWakesInput"] = screen.IsOpen && screen.IsEditing;
                }
                else flags["physicalHotkeyWakesInput"] = false;
                screen.Hide(); var pending = screen.ConfirmToolAsync(new AgentPermission { Tool = "get_clipboard", Description = "隔离验证：不读取剪贴板。" }, ct);
                flags["permissionWaits"] = !pending.IsCompleted; screen.Hide(); flags["hideDeclinesPermission"] = !await pending;
                host.ResetPosition(); var box = PetNative.Bounds; var work = PetNative.WorkArea; flags["resetVisible"] = box.Left >= work.Left && box.Top >= work.Top && box.Right <= work.Right && box.Bottom <= work.Bottom;
                flags["petNoVoice"] = root.Voice.OwnedProcessId == 0;
                var audioCheck = new GiftCompanion.AgentPet.Tools.WindowsAgentPlatform(root.AgentSettings).Prepare("set_volume", new Newtonsoft.Json.Linq.JObject { ["percent"] = 25 });
                flags["systemAudioReadOnlyInterop"] = audioCheck.Sensitive && audioCheck.Description.Contains("系统主音量");
                if (mode == "Live")
                {
                    stage = "tools"; flags["keyConfigured"] = root.LLM.IsConfigured;
                    string target = Path.Combine(root.Config.UserDataPath, "ControlledTarget.exe");
                    root.AgentSettings.SaveApplications(new List<KnownApplication> { new KnownApplication { Name = "验收应用", Path = target } }, ct);
                    var samples = new List<object>();
                    foreach (string message in new[] { "请查询一下当前本地时间。", "请打开已登记的验收应用。", "请打开这个文件夹：" + Path.Combine(root.Config.UserDataPath, "ToolFixture"), "请打开名叫绝对不存在的验收程序七九三的应用。" })
                    {
                        screen.SetAllowActions(true); await screen.SendAsync(message); flags["turn" + samples.Count + "Committed"] = screen.LastResult?.Success == true;
                        samples.Add(new { request = message, reply = screen.OutputText });
                    }
                    var runs = await ((IAgentToolStore)root.Database).ReadToolsAsync(ct);
                    foreach (string name in new[] { "get_current_time", "open_application", "open_folder" }) flags[name + "Succeeded"] = runs.Any(r => r.Tool == name && r.Status == "succeeded");
                    flags["missingAppRecordedFailure"] = runs.Any(r => r.Tool == "open_application" && r.Status == "failed");
                    flags["failedToolDoesNotClaimSuccess"] = screen.OutputText.Contains("没找到") || screen.OutputText.Contains("没有") || screen.OutputText.Contains("还没找到");
                    var turns = await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct); flags["toolTurnsExcludedFromMemory"] = turns.Count == 4 && turns.All(t => t.ExcludedFromMemory);
                    AtomicDocument.WriteJson(Path.Combine(reports, "tool_samples.json"), samples, ct);
                }
                else if (mode == "NoKey" || mode == "Window")
                {
                    flags["noKey"] = !root.LLM.IsConfigured; await screen.SendAsync("无Key草稿测试"); flags["noKeyKeepsDraft"] = screen.InputDraft == "无Key草稿测试";
                    if (mode == "NoKey") {
                        flags["noKeyCanReadHistory"] = (await root.History.ReadPageAsync(null, ct)).Turns.Count == 4;
                        flags["noToolReplayAfterRestart"] = (await ((IAgentToolStore)root.Database).ReadToolsAsync(ct)).Count > 0;
                    }
                }
                else throw new InvalidDataException("UnknownMode");
                stage = "capture"; screen.OpenInput(); File.WriteAllText(Path.Combine(reports, "capture_ready.flag"), "ready");
                await Until(() => File.Exists(Path.Combine(reports, "gesture_prepare.flag")), ct);
                stage = "gestures"; screen.Hide(); screen.Reposition(); await Task.Delay(300, ct);
                var interaction = new List<string>(); var traces = new List<object>();
                screen.Interaction += signal =>
                {
                    interaction.Add(signal.ToString());
                    object gesture = typeof(PetScreen).GetField("gesture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(screen);
                    object Field(string name) => gesture.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gesture);
                    traces.Add(new { signal = signal.ToString(), cursor = PetNative.Cursor, client = PetNative.ClientCursor().ToString(), originX = Field("x"), originY = Field("y"), bounds = PetNative.Bounds });
                };
                var heads = new List<object>(); var bodies = new List<object>();
                // Candidate pixels are on this pet's actual colliders. The external helper additionally checks native opacity hit ownership before clicking.
                for (int y = Screen.height / 2; y > 40; y -= 12) for (int x = 24; x < Screen.width - 24; x += 12)
                    if (screen.HitPet(new Vector2(x, y), out bool head)) { var point = new { x, y = Screen.height - y }; if (head && heads.Count < 100) heads.Add(point); else if (!head && bodies.Count < 100) bodies.Add(point); }
                for (int y = Screen.height / 2 + 12; y < Screen.height - 24; y += 12) for (int x = 24; x < Screen.width - 24; x += 12)
                    if (screen.HitPet(new Vector2(x, y), out bool head) && head && heads.Count < 100) heads.Add(new { x, y = Screen.height - y });
                AtomicDocument.WriteJson(Path.Combine(reports, "gesture_ready.json"), new { heads, bodies, width = Screen.width, height = Screen.height }, ct);
                await Until(() => File.Exists(Path.Combine(reports, "gesture_done.flag")), ct); await Task.Delay(300, ct);
                foreach (string expected in new[] { "HeadClick", "BodyClick", "HeadPatStart", "HeadPatEnd", "DragStart", "DragEnd" }) flags["native" + expected] = interaction.Contains(expected);
                flags["dragReleased"] = !screen.Dragging;
                AtomicDocument.WriteJson(Path.Combine(reports, "gesture_events_" + mode.ToLowerInvariant() + ".json"), interaction, ct);
                AtomicDocument.WriteJson(Path.Combine(reports, "gesture_trace_" + mode.ToLowerInvariant() + ".json"), traces, ct);
                stage = "capture"; screen.OpenInput(); File.WriteAllText(Path.Combine(reports, "gesture_observed.flag"), "ready");
                await Until(() => File.Exists(Path.Combine(reports, "capture_done.flag")), ct);
                stage = "active"; await Measure(Path.Combine(reports, "performance_active_" + mode.ToLowerInvariant() + ".json"), ct); screen.Hide();
                stage = "idle"; await Task.Delay(4000, ct); await Measure(Path.Combine(reports, "performance_" + mode.ToLowerInvariant() + ".json"), ct);
                stage = "switch"; await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); await Task.Delay(600, ct);
                using (var competitor = new PetHotkeyHost()) flags["homeReleasesHotkey"] = await competitor.RegisterAsync(defaultKey);
                flags["homeRestoresNormalWindow"] = !host.IsPetActive && Screen.width == 1280 && Screen.height == 720;
                AtomicDocument.WriteJson(Path.Combine(reports, "home_window_" + mode.ToLowerInvariant() + ".json"), new { active = host.IsPetActive, width = Screen.width, height = Screen.height, bounds = PetNative.Bounds, work = PetNative.WorkArea }, ct);
                await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct); await Until(() => FindFirstObjectByType<GiftCompanion.Companion.CompanionScreen>()?.IsReady == true, ct);
                flags["companionKeepsSameCore"] = ReferenceEquals(core, root.Character) && ReferenceEquals(state, core.State); flags["voiceStillOff"] = root.Voice.OwnedProcessId == 0;
                if (mode == "Live")
                {
                    stage = "voice-return"; byte[] wave = await root.Voice.SynthesizeAsync("先生、ただいま。", ct);
                    flags["companionVoiceWorksAfterPet"] = wave.Length > 44 && root.Voice.OwnedProcessId > 0 && core.CurrentMode == CharacterMode.Companion;
                    File.WriteAllBytes(Path.Combine(reports, "companion_resume.wav"), wave); await root.StopVoiceAsync();
                    flags["companionVoiceReleased"] = root.Voice.OwnedProcessId == 0;
                }
                await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); stage = "shutdown"; await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded; passed = flags.Values.All(v => v);
            }
            catch (Exception error) { failure = error.GetType().Name; }
            File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed, stage, flags, errorType = failure, verifiedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented)); Application.Quit(passed ? 0 : 1);
        }
        private static async Task Measure(string file, CancellationToken ct)
        {
            using var process = Process.GetCurrentProcess(); process.Refresh(); var start = process.TotalProcessorTime; var clock = Stopwatch.StartNew(); int frames = Time.frameCount; await Task.Delay(4000, ct); process.Refresh();
            var memory = new MemoryInfo { Size = (uint)Marshal.SizeOf<MemoryInfo>() }; bool measured = GetProcessMemoryInfo(new IntPtr(-1), ref memory, memory.Size);
            AtomicDocument.WriteJson(file, new { seconds = clock.Elapsed.TotalSeconds, cpuPercentWholeMachine = 100 * (process.TotalProcessorTime - start).TotalSeconds / clock.Elapsed.TotalSeconds / Environment.ProcessorCount, memoryMeasured = measured, workingSetMiB = memory.WorkingSet.ToUInt64() / 1048576.0, privateMiB = memory.Private.ToUInt64() / 1048576.0, fps = (Time.frameCount - frames) / clock.Elapsed.TotalSeconds, targetFrameRate = Application.targetFrameRate, gpu = SystemInfo.graphicsDeviceName, dpi = Screen.dpi }, ct);
        }
        [StructLayout(LayoutKind.Sequential)] private struct MemoryInfo { public uint Size, Faults; public UIntPtr PeakWorkingSet, WorkingSet, PeakPaged, Paged, PeakNonPaged, NonPaged, Pagefile, PeakPagefile, Private; }
        [DllImport("psapi.dll")] private static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryInfo counters, uint size);
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        { var deadline = DateTimeOffset.UtcNow.AddSeconds(45); while (!condition()) { ct.ThrowIfCancellationRequested(); if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException(); await Task.Delay(20, ct); } }
#endif
    }
}
