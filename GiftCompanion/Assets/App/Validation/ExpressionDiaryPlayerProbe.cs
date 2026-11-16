using System;
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

namespace GiftCompanion.Validation
{
    public sealed class ExpressionDiaryPlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-expressiondiary-probe");
            if (at < 0) { enabled = false; return; }
            if (at + 2 >= args.Length || Array.IndexOf(args, "-validation-data") < 0) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[at + 1]), mode = args[at + 2], stage = "startup", failure = null;
            bool passed = false; var flags = new Dictionary<string, bool>(); var samples = new List<object>();
            Directory.CreateDirectory(reports); var root = AppRoot.Instance;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken); deadline.CancelAfter(TimeSpan.FromMinutes(9)); var ct = deadline.Token;
                await root.Initialization;
                string boundary = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiftCompanion/Validation")) + Path.DirectorySeparatorChar;
                if (!root.Config.UserDataPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("IsolationRequired");
                flags["isolatedDataAndSchemaNine"] = root.Database.SchemaVersion == 9;
                await Until(() => !root.Controller.IsSwitching, ct); await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                var screen = FindFirstObjectByType<CompanionScreen>(); string saved = Path.Combine(root.Config.UserDataPath, "expressiondiary-probe.json");
                if (mode == "Write")
                {
                    await Until(() => root.Voice.State == VoiceRuntimeState.Ready, ct);
                    using var foreground = (root.Memory as ISharedMemoryService)?.EnterForeground();
                    flags["japaneseDefault"] = root.Config.Configuration.VoiceLanguage == "ja";
                    var delivery = VoiceDeliverySettings.Load(root.Config.UserDataPath);
                    flags["existingHappyAndShyPreserved"] = delivery.Select("ja", "happy").Id == "ja-bright" && delivery.Select("ja", "shy").Id == "ja-shy";
                    string lastMessage = null;
                    foreach (string intent in new[] { "annoyed", "sad", "hesitant", "flat" })
                    {
                        stage = "intent_" + intent;
                        string instruction = intent == "annoyed" ? "请用轻微恼火但不攻击人的语气，提醒老师不要连续打断你。这只是语气演示，不是已经发生的事。" :
                            intent == "sad" ? "请用伤心遗憾的语气，说假如散步临时取消会有点失落，不声称我们真的约过散步。" :
                            intent == "hesitant" ? "请用犹豫的语气，表示还没有想好先听音乐还是先读书。" :
                            "请用无情绪起伏的平直语气，确认已收到老师的消息，不作其他承诺。";
                        await screen.SendAsync(instruction + "用一句简短中文回复，约十五个汉字，按表现协议设置voice为" + intent + "，不要在正文提到协议。");
                        await screen.VoicePlayer.Completion;
                        var turn = (await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct)).Last();
                        var records = await root.AudioCache.ReadPlayableAsync(turn.AssistantMessageId, ct);
                        var audits = records.Select(r => AtomicDocument.Read<SpeechAudit>(root.AudioCache.AuditPath(r.Path))).ToArray();
                        flags[intent + "EndToEnd"] = screen.LastDialogueResult.Success && audits.Length > 0 && audits.All(a => a.Language == "ja" && a.VoiceIntent == intent && a.ReferenceId == "ja-" + intent);
                        flags[intent + "PreservesChineseHistory"] = turn.AssistantText == screen.LastDialogueResult.Text && turn.RawAssistantText.Contains("voice=\"" + intent + "\"");
                        if (audits.Length == 0) throw new InvalidDataException("NoAudioForIntent");
                        File.WriteAllBytes(Path.Combine(reports, "ja_" + intent + ".wav"), File.ReadAllBytes(root.AudioCache.Resolve(records[0].Path)));
                        samples.Add(new { intent, chinese = audits[0].DisplayText, japanese = audits[0].SpokenText, mood = audits[0].Emotion,
                            audits[0].ReferenceId, audits[0].SpeedFactor, qualityAccepted = false });
                        lastMessage = turn.AssistantMessageId;
                    }
                    flags["sharedMoodEnumUnchanged"] = Enum.GetValues(typeof(CharacterEmotion)).Length == 8 && Enum.IsDefined(typeof(CharacterEmotion), root.Character.State.Mood);
                    flags["singleGpuInference"] = root.Voice.MaximumActiveInferenceCount == 1;
                    AtomicDocument.WriteJson(Path.Combine(reports, "sample_texts.json"), samples, ct);
                    stage = "diary";
                    var diary = await root.PlayerWorkAsync(token => root.Diary.GenerateAsync(null, token), ct);
                    string file = Path.Combine(root.Diary.Folder, diary.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd_HHmmss'Z'") + "_" + diary.Id + ".md");
                    var paragraphs = diary.Body.Split(new[] { "\n\n" }, StringSplitOptions.None);
                    var policy = AtomicDocument.Read<DiaryPolicy>(root.Diary.PolicyFile);
                    flags["diaryParagraphsReadable"] = paragraphs.Length >= 2 && paragraphs.All(p => p.Length <= policy.MaximumParagraphCharacters);
                    flags["relaxedKaomojiPolicy"] = policy.MaximumKaomoji == 4 && policy.Version == 2;
                    flags["diaryDatabaseAndMarkdownMatch"] = File.ReadAllText(file).Contains(diary.Body) && (await root.Diary.ListAsync(ct)).Any(d => d.Id == diary.Id && d.Body == diary.Body);
                    File.WriteAllText(Path.Combine(reports, "sample_diary.md"), File.ReadAllText(file));
                    AtomicDocument.WriteJson(Path.Combine(reports, "diary_layout.json"), new { paragraphCount = paragraphs.Length, paragraphLengths = paragraphs.Select(p => p.Length).ToArray(), maximumKaomoji = policy.MaximumKaomoji }, ct);
                    AtomicDocument.WriteJson(saved, new { messageId = lastMessage }, ct);
                    var config = root.Config.Configuration.Copy(); config.VoiceEnabled = false; await root.Config.SaveAsync(config, ct); await root.StopVoiceAsync();
                }
                else if (mode == "NoKey")
                {
                    stage = "offlineReplay"; flags["noKey"] = !root.LLM.IsConfigured;
                    string messageId = JObject.Parse(File.ReadAllText(saved)).Value<string>("messageId");
                    flags["newIntentOfflineReplay"] = await screen.VoicePlayer.ReplayAsync(messageId, ct); await screen.VoicePlayer.Completion;
                    flags["noTranslationOrRuntimeForReplay"] = root.Voice.OwnedProcessId == 0 && ((SpeechLocalizer)root.SpeechLocalizer).NetworkCalls == 0;
                    flags["diaryRestoredOffline"] = (await root.Diary.ListAsync(ct)).Count > 0;
                }
                else throw new InvalidDataException("UnknownMode");
                stage = "shutdown"; await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); flags["homeReleasesVoice"] = root.Voice.OwnedProcessId == 0;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded; passed = flags.Values.All(x => x);
            }
            catch (Exception error) { failure = error.GetType().Name; try { await root.StopVoiceAsync(); } catch { } }
            File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed, stage, flags, errorType = failure, verifiedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        { var end = DateTimeOffset.UtcNow.AddMinutes(3); while (!condition()) { ct.ThrowIfCancellationRequested(); if (DateTimeOffset.UtcNow > end) throw new TimeoutException(); await Task.Yield(); } }
#endif
    }
}
