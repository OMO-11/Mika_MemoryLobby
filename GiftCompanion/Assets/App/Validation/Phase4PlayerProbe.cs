using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Companion.Bgm;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiftCompanion.Validation
{
    public sealed class Phase4PlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-phase4-probe");
            if (index < 0) { enabled = false; return; }
            if (index + 2 >= args.Length || Array.IndexOf(args, "-validation-data") < 0) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[index + 1]), mode = args[index + 2]; Directory.CreateDirectory(reports);
            bool passed = false; string stage = "startup", failure = null; var flags = new Dictionary<string, bool>(); var timings = new Dictionary<string, double>();
            var root = AppRoot.Instance;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken); timeout.CancelAfter(TimeSpan.FromMinutes(9)); var ct = timeout.Token;
                await root.Initialization;
                string boundary = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiftCompanion", "Validation")) + Path.DirectorySeparatorChar;
                if (!root.Config.UserDataPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("ProbeIsolationRequired");
                flags["isolatedData"] = true; flags["schemaNine"] = root.Database.SchemaVersion == 9;
                await Until(() => !root.Controller.IsSwitching && SceneManager.GetActiveScene().name == "Home", ct);
                await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                var screen = FindFirstObjectByType<CompanionScreen>(); var visual = FindFirstObjectByType<CompanionPresentation>(); var music = FindFirstObjectByType<BgmManager>();
                var core = root.Character; var state = core.State;
                flags["immersiveAndSingleCore"] = !screen.IsInputOpen && !screen.IsMenuOpen && ReferenceEquals(core, root.Character);
                string idsFile = Path.Combine(root.Config.UserDataPath, "phase4-probe-message.json");
                if (mode == "Write")
                {
                    stage = "runtime";
                    await root.Voice.EnsureReadyAsync(ct); flags["ownedRuntimeReady"] = root.Voice.OwnedProcessId > 0;
                    timings["runtimeStartupMs"] = root.Voice.LastStartupMilliseconds;
                    using var foreground = (root.Memory as ISharedMemoryService)?.EnterForeground();
                    stage = "realCancellation";
                    using (var canceled = new CancellationTokenSource())
                    {
                        int before = root.Voice.InferenceStartedCount;
                        var old = root.Voice.SynthesizeAsync("老师，今天也一起度过吧。", canceled.Token);
                        await Until(() => root.Voice.ActiveInferenceCount == 1, ct); canceled.Cancel();
                        var next = root.Voice.SynthesizeAsync("老师，我会认真听你说的。", ct);
                        await Task.Delay(100, ct);
                        flags["cancellationKeepsInferenceGate"] = old.IsCompleted || root.Voice.InferenceStartedCount == before + 1;
                        try { await old; } catch (OperationCanceledException) { }
                        byte[] wav = await next; var decoded = WaveAudio.Decode(wav);
                        flags["realChineseAudio"] = decoded.Frequency == 48000 && decoded.Samples.Any(x => Math.Abs(x) > .005f) && root.Voice.ProfileId.StartsWith("zh:", StringComparison.Ordinal);
                        flags["canceledResultDiscarded"] = old.IsCanceled && root.Voice.MaximumActiveInferenceCount == 1;
                        timings["lastStandaloneInferenceMs"] = root.Voice.LastInferenceMilliseconds;
                        File.WriteAllBytes(Path.Combine(reports, "real_chinese.wav"), wav);
                    }
                    stage = "dialogue";
                    var cfg = root.Config.Configuration.Copy(); cfg.VoiceVolume = .15f; cfg.BgmVolume = .12f; await root.Config.SaveAsync(cfg, ct);
                    screen.VoicePlayer.SetVolume(cfg.VoiceVolume); music.SetVolume(cfg.BgmVolume, false);
                    bool llmComplete = false, overlapObserved = false; int segments = 0, mouthFrames = 0; double firstAudioAt = 0, startedAt = Time.realtimeSinceStartupAsDouble;
                    screen.VoicePlayer.SegmentPlaying += (_, __, ___, replay) =>
                    { if (!replay) { segments++; if (firstAudioAt == 0) { firstAudioAt = Time.realtimeSinceStartupAsDouble; overlapObserved = !llmComplete; } } };
                    var dialogue = screen.SendAsync("这是语音链路验收。请用三句自然中文聊聊轻柔的音乐，每句十五到二十五个汉字，用句号分隔，不要列举，不要添加共同经历。");
                    async Task ObserveDialogue()
                    { await dialogue; llmComplete = true; timings["llmCompleteMs"] = (Time.realtimeSinceStartupAsDouble - startedAt) * 1000; }
                    var observedDialogue = ObserveDialogue();
                    await Until(() => screen.VoicePlayer.IsPlaying || screen.VoicePlayer.Completion.IsCompleted && dialogue.IsCompleted, ct);
                    flags["actualVoiceStarted"] = screen.VoicePlayer.IsPlaying;
                    if (screen.VoicePlayer.IsPlaying)
                    {
                        double mouthDeadline = Time.realtimeSinceStartupAsDouble + 1.5; bool duckObserved = false;
                        while (Time.realtimeSinceStartupAsDouble < mouthDeadline && screen.VoicePlayer.IsPlaying)
                        {
                            mouthFrames += FindObjectsByType<CompanionSpeechDriver>(FindObjectsSortMode.None).Count(d => d.MouthOpen);
                            duckObserved |= music.DuckLevel < .95f && Math.Abs(music.Volume - cfg.BgmVolume) < .001f;
                            await Task.Yield();
                        }
                        flags["speechDucksBgm"] = duckObserved;
                        await Capture(reports, "speaking");
                    }
                    await observedDialogue;
                    await screen.VoicePlayer.Completion;
                    timings["firstAudioMs"] = firstAudioAt == 0 ? -1 : (firstAudioAt - startedAt) * 1000;
                    timings["actualOverlapObserved"] = overlapObserved ? 1 : 0;
                    // Transport speed can finish a short reply before TTS. Controlled streaming overlap is tested separately.
                    flags["pcmMouthObserved"] = mouthFrames > 0;
                    flags["realDialogueCommitted"] = screen.LastDialogueResult?.Success == true && segments > 0;
                    var turns = await ((IConversationStore)root.Database).LoadTurnsAsync(20, true, ct);
                    var turn = turns.Single(); var cached = await root.AudioCache.ReadPlayableAsync(turn.AssistantMessageId, ct);
                    flags["allSegmentsCached"] = cached.Count == segments && cached.Count > 0;
                    flags["bodyProjectionPreserved"] = !turn.AssistantText.Contains("<META") && turn.RawAssistantText.StartsWith("<META", StringComparison.Ordinal);
                    AtomicDocument.WriteJson(idsFile, new { messageId = turn.AssistantMessageId }, ct);

                    stage = "offlineReplay";
                    await root.StopVoiceAsync(); int count = root.Voice.InferenceStartedCount;
                    flags["runtimeStoppedBeforeReplay"] = root.Voice.OwnedProcessId == 0;
                    flags["replayStarted"] = await screen.VoicePlayer.ReplayAsync(turn.AssistantMessageId, ct);
                    await Until(() => screen.VoicePlayer.IsPlaying, ct); await screen.OpenHistoryAsync(); await Frames(4, ct); await Capture(reports, "history_replay");
                    await screen.VoicePlayer.Completion;
                    flags["replayNeverCallsTts"] = root.Voice.InferenceStartedCount == count && root.Voice.OwnedProcessId == 0;
                    await screen.VoicePlayer.ReplayAsync(turn.AssistantMessageId, ct); await Until(() => screen.VoicePlayer.IsPlaying, ct);
                    screen.VoicePlayer.StopSpeech(); flags["explicitStopImmediate"] = !screen.VoicePlayer.IsPlaying;
                    await screen.VoicePlayer.DrainAsync();

                    stage = "voiceFailureTextFallback";
                    screen.OpenVoiceSettings(); await Frames(4, ct); await Capture(reports, "voice_settings");
                    string configFile = Path.Combine(root.Config.UserDataPath, "Config", "voice_runtime.json"), original = File.ReadAllText(configFile);
                    File.WriteAllText(configFile, "{\"Port\":0}");
                    flags["badConfigPreserved"] = !root.CreateVoiceRuntime() && File.ReadAllText(configFile) == "{\"Port\":0}";
                    screen.Back(); screen.Back();
                    await screen.SendAsync("请只用一句话说：老师，可以先继续文字聊天。");
                    flags["voiceFailureKeepsRealTextDialogue"] = screen.LastDialogueResult?.Success == true && !screen.VoicePlayer.IsPlaying;
                    await screen.VoicePlayer.DrainAsync(); File.WriteAllText(configFile, original);
                    flags["voiceConfigReloaded"] = root.CreateVoiceRuntime();
                    stage = "disableAndPersist";
                    cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = false; await root.Config.SaveAsync(cfg, ct); await root.StopVoiceAsync();
                    flags["sameCoreAndState"] = ReferenceEquals(root.Character, core) && ReferenceEquals(root.Character.State, state);
                }
                else if (mode == "Faults")
                {
                    stage = "missingRuntime";
                    string actualRuntime = Environment.GetEnvironmentVariable("GIFT_VOICE_RUNTIME_ROOT");
                    try
                    {
                        Environment.SetEnvironmentVariable("GIFT_VOICE_RUNTIME_ROOT", Path.Combine(root.Config.UserDataPath, "not-installed"));
                        try { await root.Voice.EnsureReadyAsync(ct); } catch (FileNotFoundException) { flags["missingRuntimeDetected"] = true; }
                        flags["missingRuntimeKeepsCore"] = root.IsReady && root.Voice.OwnedProcessId == 0;
                    }
                    finally { Environment.SetEnvironmentVariable("GIFT_VOICE_RUNTIME_ROOT", actualRuntime); }
                    stage = "foreignPort";
                    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, root.VoiceSettings.Port); listener.Start();
                    try
                    {
                        try { await root.Voice.EnsureReadyAsync(ct); } catch (InvalidOperationException) { flags["foreignPortRejected"] = true; }
                        using var portCheck = new System.Net.Sockets.TcpClient(); await portCheck.ConnectAsync("127.0.0.1", root.VoiceSettings.Port);
                        flags["foreignListenerUntouched"] = portCheck.Connected && root.Voice.OwnedProcessId == 0;
                    }
                    finally { listener.Stop(); }
                    stage = "ownedCrashAndRetry";
                    var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = true; await root.Config.SaveAsync(cfg, ct);
                    await root.Voice.EnsureReadyAsync(ct); var originalManager = root.Voice; int oldPid = originalManager.OwnedProcessId;
                    string expectedPython = Path.GetFullPath(Path.Combine(actualRuntime, "runtime", "python.exe"));
                    using (var owner = System.Diagnostics.Process.GetProcessById(oldPid))
                    {
                        if (!string.Equals(owner.MainModule.FileName, expectedPython, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected crash target");
                        owner.Kill(); await Task.Run(() => owner.WaitForExit(5000), ct);
                    }
                    flags["ownedCrashDetected"] = originalManager.OwnedProcessId == 0 && root.IsReady;
                    screen.OpenVoiceSettings();
                    FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == "Retry").onClick.Invoke();
                    await Until(() => !ReferenceEquals(root.Voice, originalManager) && root.Voice?.State == VoiceRuntimeState.Ready, ct);
                    flags["retryRestartsOwnedRuntime"] = root.Voice.OwnedProcessId > 0 && root.Voice.OwnedProcessId != oldPid;
                    Screen.SetResolution(1024, 768, FullScreenMode.Windowed); await Frames(30, ct); await Capture(reports, "voice_ready_resized");
                    flags["voicePanelResized"] = Screen.width == 1024 && Screen.height == 768;
                    stage = "leaveDuringStartup";
                    await root.StopVoiceAsync(); var starting = root.Voice.EnsureReadyAsync(ct);
                    await Until(() => root.Voice.State == VoiceRuntimeState.Starting, ct);
                    await root.Controller.SwitchModeAsync(CharacterMode.Home, ct);
                    try { await starting; } catch (OperationCanceledException) { }
                    flags["leaveCancelsPendingStartup"] = root.Voice.OwnedProcessId == 0 && SceneManager.GetActiveScene().name == "Home";
                    await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                    await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                    await root.Controller.SwitchModeAsync(CharacterMode.Home, ct);
                    flags["rapidReentryLeavesNoRuntime"] = root.Voice.OwnedProcessId == 0 && ReferenceEquals(root.Character, core) && ReferenceEquals(root.Character.State, state);
                }
                else
                {
                    stage = "restoredOfflineReplay";
                    string messageId = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(idsFile)).Value<string>("messageId");
                    flags["voiceSettingsPersisted"] = !root.Config.Configuration.VoiceEnabled && Math.Abs(root.Config.Configuration.VoiceVolume - .15f) < .001f;
                    flags["noServiceAtEntry"] = root.Voice.OwnedProcessId == 0;
                    var cached = await root.AudioCache.ReadPlayableAsync(messageId, ct);
                    flags["cacheRestored"] = cached.Count > 0;
                    flags["offlineReplay"] = await screen.VoicePlayer.ReplayAsync(messageId, ct);
                    await Until(() => screen.VoicePlayer.IsPlaying, ct); await screen.OpenHistoryAsync(); await Capture(reports, "restored_" + mode.ToLowerInvariant());
                    await screen.VoicePlayer.Completion; flags["noServiceDuringReplay"] = root.Voice.OwnedProcessId == 0;
                    if (mode == "NoKey")
                    {
                        stage = "noKeyAndDeletedCache";
                        screen.Back(); screen.Back(); await screen.SendAsync("无 Key 验收请求");
                        flags["noKeyNaturalFailure"] = screen.LastDialogueResult?.ErrorCode == "LLMNotConfigured" && !screen.VoicePlayer.IsPlaying;
                        File.Delete(root.AudioCache.Resolve(cached[0].Path));
                        flags["missingCacheNotPlayable"] = (await root.AudioCache.ReadPlayableAsync(messageId, ct)).Count == 0;
                        await screen.OpenHistoryAsync();
                        flags["missingReplayDisabled"] = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(b => b.name == "Replay").All(b => !b.interactable);
                        int historyCount = (await ((IConversationStore)root.Database).LoadTurnsAsync(20, false, ct)).Count;
                        await root.AudioCache.ClearAsync(ct);
                        flags["clearKeepsHistory"] = (await ((IConversationStore)root.Database).LoadTurnsAsync(20, false, ct)).Count == historyCount && (await ((IAudioRecordStore)root.Database).ReadAllAudioPathsAsync(ct)).Count == 0;
                        flags["noImplicitResynthesis"] = root.Voice.InferenceStartedCount == 0 && root.Voice.OwnedProcessId == 0;
                    }
                }
                stage = "shutdown";
                await root.Controller.SwitchModeAsync(CharacterMode.Home, ct);
                flags["homeReleasesVoice"] = screen == null && root.Voice?.OwnedProcessId != null && root.Voice.OwnedProcessId == 0;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded;
                passed = flags.Values.All(x => x);
            }
            catch (Exception error)
            {
                failure = error.GetType().Name;
                try { await root.StopVoiceAsync(); } catch { }
            }
            File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new {passed, mode, stage, flags, timings, errorType=failure, verifiedAtUtc=DateTimeOffset.UtcNow}, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        { var deadline = DateTimeOffset.UtcNow.AddMinutes(3); while (!condition()) { ct.ThrowIfCancellationRequested(); if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException(); await Task.Yield(); } }
        private static async Task Frames(int count, CancellationToken ct) { for (int i = 0; i < count; i++) { ct.ThrowIfCancellationRequested(); await Task.Yield(); } }
        private Task Capture(string folder, string name)
        { var done = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(folder, name, done)); return done.Task; }
        private IEnumerator CaptureFrame(string folder, string name, TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame(); Texture2D screenshot = null;
            try
            {
                screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false); screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); screenshot.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), screenshot.EncodeToPNG()); done.TrySetResult(true);
            }
            catch (Exception error) { done.TrySetException(error); }
            finally { if (screenshot != null) Destroy(screenshot); }
        }
#endif
    }
}
