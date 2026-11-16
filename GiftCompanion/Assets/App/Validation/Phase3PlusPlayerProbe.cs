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
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiftCompanion.Validation
{
    // Opt-in published-build verification. Uses synthetic conversations and a bounded Validation directory.
    public sealed class Phase3PlusPlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-phase3plus-probe");
            if (index < 0) { enabled = false; return; }
            if (index + 2 >= args.Length || Array.IndexOf(args, "-validation-data") < 0) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[index + 1]), mode = args[index + 2]; Directory.CreateDirectory(reports);
            bool real = mode == "Write", passed = false; string failure = null, stage = "startup";
            var flags = new Dictionary<string, bool>();
            try
            {
                var root = AppRoot.Instance; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(4)); var ct = timeout.Token; await root.Initialization;
                string boundary = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiftCompanion", "Validation")) + Path.DirectorySeparatorChar;
                if (!root.Config.UserDataPath.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("ProbeIsolationRequired");
                flags["isolatedData"] = true;
                await Until(() => !root.Controller.IsSwitching && SceneManager.GetActiveScene().name == "Home", ct);
                await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                var screen = FindFirstObjectByType<CompanionScreen>(); var visual = FindFirstObjectByType<CompanionPresentation>();
                var music = FindFirstObjectByType<BgmManager>(); music.SetVolume(0, false);
                await Until(() => music.Source?.clip != null && !music.Transitioning, ct);
                var core = root.Character; var state = core.State;
                flags["startsImmersive"] = !screen.IsInputOpen && !screen.IsMenuOpen && screen.ActivePanel == null;
                flags["developmentPanelHidden"] = !root.GetComponent<Phase2DevelopmentPanel>().enabled;
                flags["filesSeeded"] = new[] {root.Library.MusicFile, root.Library.SpacesFile, root.Config.CharacterCardPath, root.Diary.PolicyFile, Path.Combine(root.Config.UserDataPath,"使用说明.md")}.All(File.Exists);
                flags["singleTrackPolicy"] = root.Library.Music.Repeat == "one";
                flags["schemaCurrent"] = root.Database.SchemaVersion == DatabaseService.CurrentSchemaVersion;
                if (real)
                {
                    stage = "music";
                    flags["firstLongAudioDecoded"] = music.Source.clip != null && music.Source.clip.length > 4000;
                    screen.OpenMusic(); await Frames(4, ct); await Capture(reports, "music");
                    await screen.SelectMusicAsync("you-are-my-princess");
                    await Until(() => !music.Transitioning && music.Source.clip?.length > 5900, ct);
                    flags["secondLongAudioDecoded"] = screen.CurrentMusicId == "you-are-my-princess";
                    int repeat = music.RepeatCount; music.Source.time = music.Source.clip.length - .15f;
                    await Until(() => music.RepeatCount > repeat, ct);
                    flags["longTrackRepeatsWithoutSwitch"] = screen.CurrentMusicId == "you-are-my-princess" && music.Source.loop;
                    string wave = Path.Combine(root.Config.UserDataPath, "probe-song.wav"); WriteWave(wave);
                    await screen.ImportMusicAsync(wave);
                    var added = root.Library.Music.Tracks.Single(t => t.Id != "daily-routine-247" && t.Id != "you-are-my-princess");
                    flags["addingDoesNotSwitch"] = screen.CurrentMusicId == "you-are-my-princess" && root.Library.Music.SelectedTrackId == "you-are-my-princess";
                    await screen.SelectMusicAsync(added.Id);
                    await Until(() => !music.Transitioning && music.Source.clip?.length < 3, ct);
                    repeat = music.RepeatCount; await Until(() => music.RepeatCount > repeat, ct);
                    flags["importedTrackLoops"] = screen.CurrentMusicId == added.Id && root.Library.Music.SelectedTrackId == added.Id;

                    stage = "card";
                    await root.SaveCharacterCardAsync(new CharacterIdentity(core.Identity.Id, "未花", core.Identity.Persona + "\n验收用：回答简短而温和。", core.Identity.Relationship), ct);
                    flags["cardAppliedToSameCore"] = ReferenceEquals(root.Character, core) && core.Identity.DisplayName == "未花";
                    screen.OpenCharacterCard(); await Frames(3, ct); await Capture(reports, "character_card");

                    stage = "spaces";
                    screen.OpenSpaces(); await screen.ImportSpaceAsync(Path.Combine(Application.streamingAssetsPath, "CH0069_Source"));
                    var imported = root.Library.Spaces.Spaces.Single(s => !s.Builtin);
                    await screen.SelectSpaceAsync(imported.Id);
                    flags["genericSpaceKeepsCoreAndState"] = screen.ActiveSpaceId == imported.Id && visual.Loader.Motion == null && ReferenceEquals(root.Character, core) && ReferenceEquals(core.State, state);
                    await Frames(4, ct); await Capture(reports, "spaces");
                    await screen.SelectSpaceAsync(root.Library.Spaces.Spaces.Single(s => s.Id == "mika-memory").Id);
                    flags["builtinSpaceRestored"] = visual.Loader.Motion != null;

                    stage = "dialogue";
                    screen.Back(); screen.Back();
                    await screen.SendAsync("这是一段验收用的共同聊天：今天老师和你聊了窗边的花和轻柔的音乐。请只用一两句中文回应，不添加其他共同经历。");
                    flags["realDialogue"] = screen.LastDialogueResult?.Success == true;
                    var turns = await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct);
                    flags["rawAndProjection"] = turns.Count == 1 && turns[0].RawAssistantText.StartsWith("<META", StringComparison.Ordinal) && !turns[0].AssistantText.Contains("<META");
                    if (!flags["realDialogue"] || turns.Count == 0) throw new InvalidOperationException("DialogueFailed");
                    await Frames(4, ct); await Capture(reports, "reply");

                    stage = "diary";
                    string cardBeforeDiary = File.ReadAllText(root.Config.CharacterCardPath);
                    await screen.OpenDiaryAsync(); await screen.GenerateDiaryAsync();
                    var entries = await root.Diary.ListAsync(ct); var diary = entries.Single();
                    string markdown = Directory.GetFiles(root.Diary.Folder, "*.md").Single();
                    flags["diaryDualStorage"] = diary.Status == "active" && diary.TurnCount == 1 && File.ReadAllText(markdown).Contains(diary.Body) && !diary.Body.Contains("<META");
                    var again = await root.PlayerWorkAsync(t => root.Diary.GenerateAsync(null, t), ct);
                    flags["diaryReused"] = again.Id == diary.Id;
                    File.Delete(markdown); await root.Diary.SyncExportsAsync(ct);
                    flags["missingMarkdownRepaired"] = File.Exists(markdown) && File.ReadAllText(markdown).Contains(diary.Body);
                    flags["diaryDoesNotRewriteCard"] = File.ReadAllText(root.Config.CharacterCardPath) == cardBeforeDiary;
                    await Frames(4, ct); await Capture(reports, "diary");
                    await core.ForgetTurnAsync(turns[0].RequestId, ct);
                    var redacted = (await root.Diary.ListAsync(ct)).Single();
                    flags["forgetRedactsDatabaseAndMarkdown"] = redacted.Status == "needs_review" && redacted.Body == "" && !File.ReadAllText(markdown).Contains(diary.Body) && Directory.GetFiles(root.Diary.Folder,"*.bak").Length == 0;
                    flags["forgetKeepsAuditHistory"] = (await ((IConversationStore)root.Database).LoadTurnsAsync(10, false, ct)).Count == 1;
                    await screen.OpenDiaryAsync(); await Frames(3, ct); await Capture(reports, "diary_after_forget");
                }
                else
                {
                    stage = "restore";
                    flags["cardPersisted"] = root.Character.Identity.DisplayName == "未花" && root.Character.Identity.Persona.Contains("验收用");
                    flags["musicPersisted"] = root.Library.Music.Tracks.Count == 3 && screen.CurrentMusicId == root.Library.Music.SelectedTrackId && music.Source.clip?.length < 3;
                    flags["spacesPersisted"] = root.Library.Spaces.Spaces.Count == 2 && screen.ActiveSpaceId == root.Library.Spaces.SelectedSpaceId;
                    var entries = await root.Diary.ListAsync(ct);
                    flags["diaryPersisted"] = entries.Count == 1 && entries[0].Status == "needs_review" && Directory.GetFiles(root.Diary.Folder, "*.md").Length == 1;
                    await screen.OpenHistoryAsync(); flags["historyReadableWithoutKey"] = screen.VisibleHistoryCount == 1;
                    if (mode == "NoKey")
                    {
                        screen.Back(); screen.Back(); await screen.SendAsync("无 Key 验收请求");
                        flags["missingKeyNaturalFailure"] = screen.LastDialogueResult?.ErrorCode == "LLMNotConfigured" && !screen.SubtitleText.Contains("<META");
                    }
                    await screen.OpenDiaryAsync(); await Frames(3, ct); await Capture(reports, "restored_" + mode.ToLowerInvariant());
                }
                stage = "maintenance";
                screen.OpenMaintenance(); await Frames(3, ct); await Capture(reports, "maintenance");
                var check = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(b => b.name == "Check"); check.onClick.Invoke();
                flags["maintenanceReport"] = File.Exists(Path.Combine(root.Config.UserDataPath,"资料检查.md"));
                Screen.SetResolution(1024, 768, FullScreenMode.Windowed); await Frames(30, ct); await Capture(reports, "resized_" + mode.ToLowerInvariant());
                flags["resized"] = Screen.width == 1024 && Screen.height == 768;
                await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); await Frames(4, ct);
                flags["singleCoreAndReleasedScene"] = screen == null && visual == null && FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length == 1;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded;
                passed = flags.Values.All(x => x);
            }
            catch (Exception error) { failure = error.GetType().Name; }
            File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed, mode, stage, flags, errorType = failure, verifiedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
        private static void WriteWave(string path)
        {
            const int rate = 24000, samples = rate * 2;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            for (int i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * 2 * Math.PI * 220 / rate) * 300));
        }
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(35);
            while (!condition()) { ct.ThrowIfCancellationRequested(); if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException(); await Task.Yield(); }
        }
        private static async Task Frames(int count, CancellationToken ct)
        { for (int i = 0; i < count; i++) { ct.ThrowIfCancellationRequested(); await Task.Yield(); } }
        private Task Capture(string folder, string name)
        { var done = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(folder, name, done)); return done.Task; }
        private IEnumerator CaptureFrame(string folder, string name, TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame(); Texture2D screenshot = null;
            try
            {
                screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); screenshot.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), screenshot.EncodeToPNG()); done.TrySetResult(true);
            }
            catch (Exception error) { done.TrySetException(error); }
            finally { if (screenshot != null) Destroy(screenshot); }
        }
#endif
    }
}
