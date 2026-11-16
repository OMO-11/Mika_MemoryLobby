using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GiftCompanion.Validation
{
    public sealed class VoicePolishPlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-voicepolish-probe");
            if (at < 0) { enabled = false; return; }
            if (at + 2 >= args.Length || Array.IndexOf(args, "-validation-data") < 0) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[at + 1]), mode = args[at + 2], stage = "startup", failure = null;
            var flags = new Dictionary<string, bool>(); var timings = new Dictionary<string, double>(); bool passed = false;
            Directory.CreateDirectory(reports); var root = AppRoot.Instance;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken); deadline.CancelAfter(TimeSpan.FromMinutes(9)); var ct = deadline.Token;
                await root.Initialization;
                string boundary = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiftCompanion/Validation")) + Path.DirectorySeparatorChar;
                if (!root.Config.UserDataPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("IsolationRequired");
                flags["isolatedData"] = true; flags["schemaNinePreserved"] = root.Database.SchemaVersion == 9;
                await Until(() => !root.Controller.IsSwitching, ct); await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                var screen = FindFirstObjectByType<CompanionScreen>(); string ids = Path.Combine(root.Config.UserDataPath, "voicepolish-probe.json");
                if (mode == "Write")
                {
                    stage = "japaneseDefault";
                    flags["defaultJapanese"] = root.Config.Configuration.VoiceLanguage == "ja" && root.Config.Configuration.VoicePreferencesVersion == 2;
                    await Until(() => root.Voice.State == VoiceRuntimeState.Ready, ct);
                    timings["japaneseStartupMs"] = root.Voice.LastStartupMilliseconds;
                    using var foreground = (root.Memory as ISharedMemoryService)?.EnterForeground();
                    screen.OpenVoiceSettings(); await Task.Yield(); await Capture(reports, "voice_settings"); screen.Back(); screen.Back();
                    stage = "synchronizedDialogue";
                    var starts = new List<double>(); var durations = new List<double>(); string playingText = "";
                    screen.VoicePlayer.SegmentPlaying += (_, text, duration, replay) => { if (!replay) { starts.Add(Time.realtimeSinceStartupAsDouble); durations.Add(duration); playingText = text; } };
                    bool noPreview = true, matched = true; double began = Time.realtimeSinceStartupAsDouble;
                    var turnTask = screen.SendAsync("请开心地邀请老师在忙完之后一起听音乐。用两句自然中文，每句十五到二十个字，用句号分隔；不列举，不添加共同经历。");
                    while (!turnTask.IsCompleted || !screen.VoicePlayer.Completion.IsCompleted)
                    {
                        ct.ThrowIfCancellationRequested(); if (starts.Count == 0 && screen.SubtitleText.Length > 0) noPreview = false;
                        if (screen.VoicePlayer.IsPlaying && screen.SubtitleText != playingText) matched = false;
                        await Task.Yield();
                    }
                    await turnTask; await screen.VoicePlayer.Completion;
                    var turn = (await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct)).Last();
                    var records = await root.AudioCache.ReadPlayableAsync(turn.AssistantMessageId, ct);
                    flags["noPrematureFullReply"] = noPreview; flags["subtitlesFollowActualChineseSegment"] = matched;
                    flags["completeChineseHistoryAndJapaneseAudio"] = screen.LastDialogueResult.Success && turn.AssistantText == screen.LastDialogueResult.Text && records.Count >= 2 && records.Count == starts.Count && records.All(r => r.ProfileId.StartsWith("ja:", StringComparison.Ordinal));
                    double minGap = double.MaxValue;
                    for (int i = 1; i < starts.Count; i++) minGap = Math.Min(minGap, starts[i] - starts[i - 1] - durations[i - 1]);
                    flags["sentenceBreathObserved"] = starts.Count >= 2 && minGap >= .42;
                    timings["minimumSentenceGapMs"] = minGap == double.MaxValue ? -1 : minGap * 1000;
                    timings["firstAudioMs"] = starts.Count == 0 ? -1 : (starts[0] - began) * 1000;
                    var audits = records.Select(r => AtomicDocument.Read<SpeechAudit>(root.AudioCache.AuditPath(r.Path))).ToArray();
                    flags["emotionCarriedIntoTranslationAndVoice"] = audits.Length > 0 && audits.All(a => a.Emotion == "happy" && a.ReferenceId == "ja-bright");
                    WriteConversation(Path.Combine(reports, "ja_conversation.wav"), records.Select(r => WaveAudio.Decode(File.ReadAllBytes(root.AudioCache.Resolve(r.Path)))).ToArray(), root.VoiceSettings.SentencePauseSeconds);
                    stage = "expressiveReferences";
                    string chinese = audits[0].DisplayText; var localized = new List<object>();
                    foreach (string emotion in new[] { "happy", "shy" })
                    {
                        var text = await ((IExpressiveSpeechLocalizer)root.SpeechLocalizer).LocalizeAsync(SpeechSegmenter.CleanForSpeech(chinese), emotion, ct);
                        var voice = await root.Voice.SynthesizeWithStyleAsync(text.Text, emotion, ct);
                        File.WriteAllBytes(Path.Combine(reports, "ja_" + emotion + ".wav"), voice.Wave);
                        flags[emotion + "ReferenceSelected"] = voice.ReferenceId == (emotion == "happy" ? "ja-bright" : "ja-shy") && WaveAudio.Decode(voice.Wave).Samples.Any(x => Math.Abs(x) > .005f);
                        localized.Add(new { chinese, japanese = text.Text, emotion, voice.ReferenceId, voice.SpeedFactor });
                    }
                    AtomicDocument.WriteJson(Path.Combine(reports, "sample_texts.json"), localized, ct);
                    await screen.OpenHistoryAsync(); await Capture(reports, "japanese_history");
                    stage = "chineseSoftReference";
                    var japaneseRuntime = root.Voice; screen.OpenVoiceSettings(); Click("Language"); Click("Save");
                    await Until(() => !ReferenceEquals(japaneseRuntime, root.Voice) && root.Voice.State == VoiceRuntimeState.Ready, ct);
                    var cn = await root.Voice.SynthesizeWithStyleAsync(SpeechSegmenter.CleanForSpeech(chinese), "neutral", ct);
                    File.WriteAllBytes(Path.Combine(reports, "zh_revised.wav"), cn.Wave);
                    flags["chineseUsesSoftReference"] = cn.ReferenceId == "zh-soft" && cn.SpeedFactor < 1 && WaveAudio.Decode(cn.Wave).Samples.Any(x => Math.Abs(x) > .005f);
                    flags["singleOwnedRuntimeAfterSwitch"] = japaneseRuntime.OwnedProcessId == 0 && root.Voice.OwnedProcessId > 0;
                    screen.OpenVoiceSettings(); Click("Language"); Click("Enabled"); Click("Save");
                    await Until(() => root.Config.Configuration.VoiceLanguage == "ja" && !root.Config.Configuration.VoiceEnabled && root.Voice.OwnedProcessId == 0, ct);
                    AtomicDocument.WriteJson(ids, new { messageId = turn.AssistantMessageId }, ct);
                    stage = "diary";
                    var diary = await root.PlayerWorkAsync(token => root.Diary.GenerateAsync(null, token), ct);
                    string diaryFile = Path.Combine(root.Diary.Folder, diary.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd_HHmmss'Z'") + "_" + diary.Id + ".md");
                    flags["diaryMarkdownMatchesDatabase"] = File.ReadAllText(diaryFile).Contains(diary.Body) && (await root.Diary.ListAsync(ct)).Any(d => d.Id == diary.Id && d.Body == diary.Body);
                    File.WriteAllText(Path.Combine(reports, "sample_diary.md"), File.ReadAllText(diaryFile));
                }
                else if (mode == "NoKey")
                {
                    stage = "offlineReplay";
                    flags["noKeyAndJapaneseChoiceRestored"] = !root.LLM.IsConfigured && root.Config.Configuration.VoiceLanguage == "ja";
                    string message = JObject.Parse(File.ReadAllText(ids)).Value<string>("messageId");
                    flags["offlineReplay"] = await screen.VoicePlayer.ReplayAsync(message, ct); await screen.VoicePlayer.Completion;
                    flags["noTranslationOrRuntimeForReplay"] = ((SpeechLocalizer)root.SpeechLocalizer).NetworkCalls == 0 && root.Voice.OwnedProcessId == 0;
                }
                else throw new InvalidDataException("UnknownProbeMode");
                stage = "shutdown"; await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); flags["homeReleasesVoice"] = root.Voice.OwnedProcessId == 0;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded; passed = flags.Values.All(x => x);
            }
            catch (Exception error) { failure = error.GetType().Name; try { await root.StopVoiceAsync(); } catch { } }
            File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed, stage, flags, timings, errorType = failure, verifiedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
        private static void WriteConversation(string path, WaveAudio[] waves, float pause)
        {
            if (waves.Length == 0 || waves.Any(w => w.Frequency != 48000 || w.Channels != 1)) throw new InvalidDataException("Unexpected sample format.");
            int silence = (int)(48000 * pause), frames = waves.Sum(w => w.Samples.Length) + Math.Max(0, waves.Length - 1) * silence;
            using var writer = new BinaryWriter(File.Create(path)); writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
            for (int i = 0; i < waves.Length; i++) { if (i > 0) for (int n = 0; n < silence; n++) writer.Write((short)0); foreach (float value in waves[i].Samples) writer.Write((short)(Mathf.Clamp(value, -1, 1) * 32767)); }
        }
        private static void Click(string name) => FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == name).onClick.Invoke();
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        { var limit = DateTimeOffset.UtcNow.AddMinutes(3); while (!condition()) { ct.ThrowIfCancellationRequested(); if (DateTimeOffset.UtcNow > limit) throw new TimeoutException(); await Task.Yield(); } }
        private Task Capture(string folder, string name) { var done = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(folder, name, done)); return done.Task; }
        private IEnumerator CaptureFrame(string folder, string name, TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame(); Texture2D frame = null;
            try { frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false); frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); frame.Apply(); File.WriteAllBytes(Path.Combine(folder, name + ".png"), frame.EncodeToPNG()); done.TrySetResult(true); }
            catch (Exception error) { done.TrySetException(error); }
            finally { if (frame != null) Destroy(frame); }
        }
#endif
    }
}
