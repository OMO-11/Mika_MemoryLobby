using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.SharedUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiftCompanion.Validation
{
    public sealed class Phase6PlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs(); int marker = Array.IndexOf(args, "-phase6-probe");
            if (marker < 0) { enabled = false; return; }
            if (marker + 2 >= args.Length) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[marker + 1]), mode = args[marker + 2], stage = "startup", errorCode = null, errorSite = null;
            var flags = new Dictionary<string, bool>(); var metrics = new Dictionary<string, double>();
            var root = AppRoot.Instance; var watch = Stopwatch.StartNew();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken); timeout.CancelAfter(TimeSpan.FromMinutes(25)); var ct = timeout.Token;
                await root.Initialization; await Until(() => root.IsReady && !root.Controller.IsSwitching && SceneManager.GetActiveScene().name == "Home", ct);
                metrics["startupMilliseconds"] = watch.ElapsedMilliseconds;
                flags["singleRoot"] = FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length == 1;
                var core = root.Character;
                if (mode == "Offline")
                {
                    stage = "onboarding"; await Until(() => FindFirstObjectByType<PlayerMaintenance>() != null, ct); await Task.Delay(250, ct);
                    var panel = FindFirstObjectByType<PlayerMaintenance>();
                    flags["maskedKeyInput"] = panel.GetComponentInChildren<TMPro.TMP_InputField>().contentType == TMPro.TMP_InputField.ContentType.Password;
                    flags["offlineNoKey"] = !root.LLM.IsConfigured;
                    Directory.CreateDirectory(reports); await Capture(reports, "onboarding");
                    panel.GetComponentsInChildren<Button>().Single(b => b.name == "Close").onClick.Invoke(); await Task.Delay(100, ct);
                    stage = "backup";
                    string note = Path.Combine(root.Config.UserDataPath, "Diary", "player-note.md"); Directory.CreateDirectory(Path.GetDirectoryName(note)); File.WriteAllText(note, "快照时的手工批注\n\n第二段 ☆");
                    Directory.CreateDirectory(Path.Combine(root.Config.UserDataPath, "Content")); File.WriteAllText(Path.Combine(root.Config.UserDataPath, "Content", "fixture.txt"), "imported data");
                    var settings = root.Config.Configuration.Copy(); settings.BgmVolume = .23f; await root.Config.SaveAsync(settings, ct);
                    string archive = Path.Combine(root.Config.UserDataPath, "Backups", "native.mikabackup");
                    var m = await root.BackupAsync(archive, false, ct); flags["nativeSnapshot"] = m.Files.Any(f => f.Path == "memory.db") && !m.Files.Any(f => f.Path.StartsWith("Secrets/"));
                    var preview = await root.Backups.InspectAsync(archive, ct); flags["restorePreview"] = !preview.Manifest.IncludesAudio && preview.Description.Contains("遗忘");
                    File.WriteAllText(note, "changed after snapshot"); settings.BgmVolume = .87f; await root.Config.SaveAsync(settings, ct);
                    string point = await root.RestoreAsync(preview, ct); flags["currentRestorePoint"] = File.Exists(point); flags["restoreScheduled"] = File.Exists(Path.Combine(root.Config.UserDataPath, "pending_restore.json"));
                }
                else if (mode == "Restored")
                {
                    stage = "reboot-restore";
                    flags["startupRestored"] = root.RestoreNotice?.Contains("资料已恢复") == true;
                    flags["configRestored"] = Math.Abs(root.Config.Configuration.BgmVolume - .23f) < .001f;
                    flags["mdAnnotationsRestored"] = File.ReadAllText(Path.Combine(root.Config.UserDataPath, "Diary", "player-note.md")).Contains("第二段");
                    flags["contentRestored"] = File.Exists(Path.Combine(root.Config.UserDataPath, "Content", "fixture.txt"));
                    flags["permissionsReset"] = root.AgentSettings.Applications.Count == 0 && root.AgentSettings.Value.SearchRoots.Count == 0 && root.AgentSettings.Value.Enabled.All(p => !p.Value);
                    flags["noKeyMigrated"] = !root.LLM.IsConfigured;
                    flags["restorePointPreserved"] = Directory.GetDirectories(Path.Combine(root.Config.UserDataPath, "RestorePoints")).Length > 0;
                    foreach (var panel in FindObjectsByType<PlayerMaintenance>(FindObjectsSortMode.None)) Destroy(panel.gameObject); await Task.Delay(100, ct);
                    await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct); await Task.Delay(1000, ct);
                    flags["companionOpensAfterRestore"] = SceneManager.GetActiveScene().name == "Companion";
                    await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); await Task.Delay(500, ct);
                    foreach (var panel in FindObjectsByType<PlayerMaintenance>(FindObjectsSortMode.None)) Destroy(panel.gameObject);
                    await root.Controller.SwitchModeAsync(CharacterMode.AgentPet, ct); await Task.Delay(1500, ct);
                    flags["sameCoreAfterRestore"] = ReferenceEquals(core, root.Character); flags["petNoVoice"] = root.Voice.OwnedProcessId == 0;
                    await root.Controller.SwitchModeAsync(CharacterMode.Home, ct);
                }
                else if (mode == "Refinement")
                {
                    stage = "refinement";
                    await V1RefinementProbe.RunAsync(root, flags, name => Capture(reports, name), ct);
                }
                else if (mode == "Voice")
                {
                    stage = "voice";
                    foreach (var language in new[] { "ja", "zh" })
                    {
                        var settings = root.Config.Configuration.Copy(); settings.VoiceLanguage = language; await root.Config.SaveAsync(settings, ct);
                        flags[language + "Configured"] = root.CreateVoiceRuntime(); await root.Voice.EnsureReadyAsync(ct);
                        var bytes = await root.Voice.SynthesizeAsync(language == "ja" ? "先生、おかえり。今日は何を話そうかな。" : "老师，欢迎回来。今天想聊些什么呢？", ct);
                        flags[language + "Synthesized"] = bytes.Length > 1000; metrics[language + "StartupMs"] = root.Voice.LastStartupMilliseconds; metrics[language + "InferenceMs"] = root.Voice.LastInferenceMilliseconds;
                        Directory.CreateDirectory(reports); File.WriteAllBytes(Path.Combine(reports, language + "-standalone.wav"), bytes);
                        await root.StopVoiceAsync(); flags[language + "OwnedStopped"] = root.Voice.OwnedProcessId == 0;
                    }
                    flags["runtimeYamlWrittenToUserData"] = File.Exists(Path.Combine(root.Config.UserDataPath, "RuntimeCache", "tts_infer-ja.yaml"));
                }
                else if (mode == "Stability")
                {
                    stage = "cycles";
                    foreach (var panel in FindObjectsByType<PlayerMaintenance>(FindObjectsSortMode.None)) Destroy(panel.gameObject);
                    for (int i = 0; i < 12; i++)
                    {
                        await root.Controller.SwitchModeAsync(i % 2 == 0 ? CharacterMode.Companion : CharacterMode.AgentPet, ct); await Task.Delay(1500, ct);
                        await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); await Task.Delay(250, ct);
                    }
                    flags["twelveCyclesSingleCore"] = ReferenceEquals(core, root.Character); flags["voiceStopped"] = root.Voice.OwnedProcessId == 0;
                    stage = "continuous-observation"; var process = Process.GetCurrentProcess(); process.Refresh(); long start = process.PrivateMemorySize64; var cpu = process.TotalProcessorTime; var elapsed = Stopwatch.StartNew();
                    for (int i = 0; i < 120; i++) { await Task.Delay(5000, ct); if (i % 12 == 0) { Directory.CreateDirectory(reports); AtomicDocument.WriteJson(Path.Combine(reports, "stability-progress.json"), new { elapsedSeconds = elapsed.Elapsed.TotalSeconds, cycles = 12 }, ct, false); } }
                    process.Refresh(); metrics["observationSeconds"] = elapsed.Elapsed.TotalSeconds; metrics["privateStartMiB"] = start / 1048576d; metrics["privateEndMiB"] = process.PrivateMemorySize64 / 1048576d; metrics["cpuMachinePercent"] = (process.TotalProcessorTime - cpu).TotalSeconds / elapsed.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
                    flags["observationCompleted"] = elapsed.Elapsed.TotalSeconds >= 600; flags["dataStillPersistent"] = root.Database.IsPersistent;
                }
                else throw new InvalidDataException("Unknown Phase6 probe mode.");
                stage = "shutdown"; await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded;
            }
            catch (Exception e) { errorCode = e.GetType().Name; var method = new StackTrace(e).GetFrame(0)?.GetMethod(); errorSite = method?.DeclaringType?.FullName + "." + method?.Name; }
            bool passed = errorCode == null && flags.Count > 0 && flags.Values.All(v => v);
            Directory.CreateDirectory(reports); AtomicDocument.WriteJson(Path.Combine(reports, mode + ".json"), new { passed, stage, errorCode, errorSite, flags, metrics }, CancellationToken.None, false);
            await root.ShutdownAsync(); Application.Quit(passed ? 0 : 1);
        }
        private static async Task Until(Func<bool> condition, CancellationToken ct) { while (!condition()) await Task.Delay(50, ct); }
        private Task Capture(string folder, string name)
        { var done = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(folder, name, done)); return done.Task; }
        private System.Collections.IEnumerator CaptureFrame(string folder, string name, TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame(); Texture2D frame = null;
            try { frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false); frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); frame.Apply(); File.WriteAllBytes(Path.Combine(folder, name + ".png"), frame.EncodeToPNG()); done.TrySetResult(true); }
            catch (Exception error) { done.TrySetException(error); }
            finally { if (frame != null) Destroy(frame); }
        }
#endif
    }
}
