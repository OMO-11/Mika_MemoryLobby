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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiftCompanion.Validation
{
    public sealed class BilingualPlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-bilingual-probe");
            if (at < 0) { enabled = false; return; }
            if (at + 2 >= args.Length || Array.IndexOf(args, "-validation-data") < 0) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[at + 1]), mode = args[at + 2], stage = "startup", failure = null;
            var flags = new Dictionary<string, bool>(); var timings = new Dictionary<string, double>(); bool passed = false;
            Directory.CreateDirectory(reports); var root = AppRoot.Instance;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken); deadline.CancelAfter(TimeSpan.FromMinutes(8)); var ct = deadline.Token;
                await root.Initialization;
                string boundary = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiftCompanion/Validation")) + Path.DirectorySeparatorChar;
                if (!root.Config.UserDataPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("IsolationRequired");
                flags["isolatedData"] = true; flags["schemaNinePreserved"] = root.Database.SchemaVersion == 9;
                await Until(() => !root.Controller.IsSwitching && SceneManager.GetActiveScene().name == "Home", ct);
                await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                var screen = FindFirstObjectByType<CompanionScreen>(); string ids = Path.Combine(root.Config.UserDataPath, "bilingual-probe.json");
                using var foreground = (root.Memory as ISharedMemoryService)?.EnterForeground();
                if (mode == "Write")
                {
                    stage = "chinese";
                    await Until(() => root.Voice.State == VoiceRuntimeState.Ready, ct); var chineseRuntime = root.Voice;
                    flags["entryAutomaticallyPreparesVoice"] = root.Voice.OwnedProcessId > 0;
                    timings["chineseStartupMs"] = root.Voice.LastStartupMilliseconds;
                    await screen.SendAsync("这是语音验收，请只用一句简短中文邀请老师一起听音乐，不要列举，不要添加共同经历。");
                    await screen.VoicePlayer.Completion;
                    var cnTurn = (await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct)).Last();
                    var cnAudio = await root.AudioCache.ReadPlayableAsync(cnTurn.AssistantMessageId, ct);
                    flags["chineseDialogueAndAudio"] = screen.LastDialogueResult.Success && cnAudio.Count > 0 && cnAudio.All(a => a.ProfileId.StartsWith("zh:", StringComparison.Ordinal));
                    var cnAudit = AtomicDocument.Read<SpeechAudit>(root.AudioCache.AuditPath(cnAudio[0].Path));
                    File.Copy(root.AudioCache.Resolve(cnAudio[0].Path), Path.Combine(reports, "comparison_zh.wav"), true);
                    flags["chineseBypassesTranslation"] = ((SpeechLocalizer)root.SpeechLocalizer).NetworkCalls == 0 && cnAudit.TranslationModel == null;
                    stage = "switchThroughMenu";
                    screen.OpenVoiceSettings(); Click("Language"); Click("Save");
                    await Until(() => !ReferenceEquals(root.Voice, chineseRuntime) && root.Voice?.State == VoiceRuntimeState.Ready, ct);
                    flags["menuSwitchAndOneRuntime"] = root.Config.Configuration.VoiceLanguage == "ja" && chineseRuntime.OwnedProcessId == 0 && root.Voice.OwnedProcessId > 0;
                    timings["japaneseStartupMs"] = root.Voice.LastStartupMilliseconds;
                    await Capture(reports, "japanese_settings"); screen.Back(); screen.Back();
                    stage = "sameChineseComparison";
                    var localized = await root.SpeechLocalizer.LocalizeAsync(SpeechSegmenter.CleanForSpeech(cnAudit.DisplayText), ct);
                    byte[] jpWav = await root.Voice.SynthesizeAsync(localized.Text, ct);
                    var wave = WaveAudio.Decode(jpWav);
                    flags["japaneseCloneFromTranslatedChinese"] = wave.Frequency == 48000 && wave.Samples.Any(x => Math.Abs(x) > .005f) && root.Voice.ProfileId.StartsWith("ja:", StringComparison.Ordinal);
                    File.WriteAllBytes(Path.Combine(reports, "comparison_ja.wav"), jpWav);
                    AtomicDocument.WriteJson(Path.Combine(reports, "comparison_text.json"), new { chinese = cnAudit.DisplayText, japanese = localized.Text,
                        source = "main_llm_chinese_reply_first_segment", translationModel = localized.Model, localized.InstructionHash, userQualityAccepted = false }, ct);
                    stage = "japaneseDialogue";
                    bool chineseBeforeAudio = false; int segments = 0; double begun = Time.realtimeSinceStartupAsDouble, firstAudio = 0;
                    screen.VoicePlayer.SegmentPlaying += (_, __, ___, replay) => { if (!replay) { segments++; if (firstAudio == 0) firstAudio = Time.realtimeSinceStartupAsDouble; } };
                    var turn = screen.SendAsync("请用一句简短中文，告诉老师忙碌之后也可以稍微休息，不要提到刚才的测试。");
                    while (!turn.IsCompleted || !screen.VoicePlayer.IsPlaying && !screen.VoicePlayer.Completion.IsCompleted)
                    {
                        ct.ThrowIfCancellationRequested();
                        chineseBeforeAudio |= !screen.VoicePlayer.IsPlaying && screen.SubtitleText.Length > 0;
                        await Task.Yield();
                    }
                    await turn;
                    if (screen.VoicePlayer.IsPlaying) await Capture(reports, "japanese_chinese_subtitle");
                    await screen.VoicePlayer.Completion;
                    timings["japaneseFirstAudioMs"] = firstAudio == 0 ? -1 : (firstAudio - begun) * 1000;
                    var jpTurn = (await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct)).Last();
                    var jpAudio = await root.AudioCache.ReadPlayableAsync(jpTurn.AssistantMessageId, ct);
                    flags["frontendChineseBeforeAudio"] = chineseBeforeAudio;
                    flags["japaneseReplyCompleteAndCached"] = screen.LastDialogueResult.Success && jpAudio.Count == segments && segments > 0 && jpAudio.All(a => a.ProfileId.StartsWith("ja:", StringComparison.Ordinal));
                    flags["chineseHistoryUnchanged"] = jpTurn.AssistantText == screen.LastDialogueResult.Text && !System.Text.RegularExpressions.Regex.IsMatch(jpTurn.AssistantText, "[ぁ-ゖァ-ヺ]");
                    flags["readableTranslationAudit"] = jpAudio.All(a => { var audit = AtomicDocument.Read<SpeechAudit>(root.AudioCache.AuditPath(a.Path)); return audit.Language == "ja" && audit.DisplayText == a.DisplayText && audit.TranslationModel == "deepseek-flash" && audit.SpokenText != audit.DisplayText; });
                    flags["oneGpuInference"] = root.Voice.MaximumActiveInferenceCount == 1;
                    flags["oldChineseCacheStillPlayable"] = (await root.AudioCache.ReadPlayableAsync(cnTurn.AssistantMessageId, ct)).Count == cnAudio.Count;
                    await screen.OpenHistoryAsync(); await Capture(reports, "bilingual_history");
                    AtomicDocument.WriteJson(ids, new { chinese = cnTurn.AssistantMessageId, japanese = jpTurn.AssistantMessageId }, ct);
                    var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = false; await root.Config.SaveAsync(cfg, ct); await root.StopVoiceAsync();
                }
                else if (mode == "NoKey")
                {
                    stage = "offlineReplay";
                    flags["noKey"] = !root.LLM.IsConfigured; flags["languageChoiceRestored"] = root.Config.Configuration.VoiceLanguage == "ja";
                    var saved = JObject.Parse(File.ReadAllText(ids)); int played = 0;
                    foreach (string name in new[] { "chinese", "japanese" })
                    {
                        bool replay = await screen.VoicePlayer.ReplayAsync(saved.Value<string>(name), ct);
                        await screen.VoicePlayer.Completion; if (replay && screen.VoicePlayer.PlayedSegments > 0) played++;
                    }
                    flags["bothLanguagesReplayOffline"] = played == 2;
                    flags["noTranslationOrRuntimeForReplay"] = ((SpeechLocalizer)root.SpeechLocalizer).NetworkCalls == 0 && root.Voice.OwnedProcessId == 0 && root.Voice.InferenceStartedCount == 0;
                    var turns = await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct);
                    await root.AudioCache.ClearAsync(ct);
                    flags["cacheClearRemovesSpeechSidecars"] = !Directory.EnumerateFiles(root.AudioCache.Folder, "*.speech.json", SearchOption.AllDirectories).Any();
                    flags["cacheClearKeepsChineseHistory"] = (await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct)).Count == turns.Count;
                }
                else throw new InvalidDataException("UnknownProbeMode");
                stage = "shutdown"; await root.Controller.SwitchModeAsync(CharacterMode.Home, ct);
                flags["homeReleasesVoice"] = root.Voice.OwnedProcessId == 0;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded; passed = flags.Values.All(x => x);
            }
            catch (Exception error) { failure = error.GetType().Name; try { await root.StopVoiceAsync(); } catch { } }
            File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed, stage, flags, timings, errorType = failure, verifiedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
        private static void Click(string name) => FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == name).onClick.Invoke();
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        { var deadline = DateTimeOffset.UtcNow.AddMinutes(3); while (!condition()) { ct.ThrowIfCancellationRequested(); if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException(); await Task.Yield(); } }
        private Task Capture(string folder, string name)
        { var done = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(folder, name, done)); return done.Task; }
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
