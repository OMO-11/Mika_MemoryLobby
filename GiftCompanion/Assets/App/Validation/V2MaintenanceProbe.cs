using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Companion.Bgm;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine.UI;
#endif

namespace GiftCompanion.Validation
{
    public sealed class V2MaintenanceProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        readonly Dictionary<string, bool> checks = new Dictionary<string, bool>();
        string output;
        void Check(bool value, string name) { checks[name] = value; if (!value) throw new Exception(name); }
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        static Button Button(Component parent, string name) => parent.GetComponentsInChildren<Button>(true).Single(b => b.name == name && b.gameObject.activeInHierarchy);
        static Slider Slider(Component parent, string name) => parent.GetComponentsInChildren<Slider>(true).Single(s => s.name == name);
        static Task InvokeTask(object owner, string name) => (Task)owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null);
        void Confirm(CompanionScreen screen) => Button(Field<RectTransform>(screen, "editPrompt"), "Confirm").onClick.Invoke();
        async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int marker = Array.IndexOf(args, "-v2-maintenance-probe");
            if (marker < 0 || marker + 1 >= args.Length) return;
            output = Path.GetFullPath(args[marker + 1]); Directory.CreateDirectory(output);
            var root = GetComponent<AppRoot>(); string error = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(110)); var ct = timeout.Token;
            try
            {
                await root.Initialization; var screen = await Ready(ct); var core = root.Character; var database = root.Database;
                screen.OpenSettings(); await Settled(screen, ct);
                var sound = Field<RectTransform>(screen, "voicePanel"); var saved = root.Config.Configuration.Copy();
                Check(screen.ActivePanel == "Voice" && sound.GetComponent<Image>().color.a == 0, "settingsOpensFloatingSoundTab");
                Check(sound.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Settings tab ")) == 3, "threeSettingsTabs");
                await Capture("设置_声音.png");
                var music = FindFirstObjectByType<BgmManager>(); Slider(sound, "Volume").value = .17f; Slider(sound, "VoiceVolume").value = .22f;
                screen.OpenDisplaySettings(); Check(screen.ConfirmationVisible && screen.ActivePanel == "Voice", "unsavedTabSwitchRequiresChoice"); await Capture("未保存确认.png");
                screen.Back(); Check(!screen.ConfirmationVisible && Math.Abs(music.Volume - .17f) < .01f, "continueEditingKeepsDraftAndPreview");
                screen.OpenDisplaySettings(); Confirm(screen); Check(screen.ActivePanel == "Display" && music.Volume == saved.BgmVolume, "discardRestoresSoundPreview");
                Check(root.Config.Configuration.VoiceVolume == saved.VoiceVolume, "cancelDoesNotSaveSound");
                var display = Field<RectTransform>(screen, "displayPanel"); var beforeDisplay = root.Display.Value.Copy();
                Slider(display, "Exposure").value = .81f; Slider(display, "SubtitleSize").value = 32; Button(display, "ReduceMotion").onClick.Invoke();
                Check(Math.Abs(FindFirstObjectByType<CompanionTone>().exposure - .81f) < .01f, "displayDraftPreviewsImmediately");
                Check(root.Display.Value.Exposure == beforeDisplay.Exposure && root.Config.Configuration.SubtitleFontSize == saved.SubtitleFontSize, "previewDoesNotPersist");
                screen.Back(); Confirm(screen); Check(FindFirstObjectByType<CompanionTone>().exposure == beforeDisplay.Exposure && screen.WindowView.ReduceMotion == root.VisualSettings.Value.ReduceMotion, "discardRestoresDisplayAndMotion");
                screen.OpenDisplaySettings(); Slider(display, "Exposure").value = .89f; Slider(display, "SubtitleSize").value = 29;
                Slider(display, "DiarySize").value = 28; Slider(display, "DiarySpacing").value = 15;
                await InvokeTask(screen, "SaveDisplayDraftAsync"); Check(root.Config.Configuration.SubtitleFontSize == 29 && Math.Abs(root.Display.Value.Exposure - .89f) < .01f && root.Display.Value.DiaryFontSize == 28, "displayGroupActuallySaves");
                Button(display, "Defaults").onClick.Invoke(); Check(root.Display.Value.DiaryFontSize == 28, "defaultsRemainDraftUntilSave");
                Button(display, "TakeBack").onClick.Invoke(); Check(Slider(display, "SubtitleSize").value == 29, "takeBackUsesSavedValues");
                await Settled(screen, ct); await Capture("设置_画面与阅读.png");
                screen.OpenSettings(); Slider(sound, "Volume").value = .21f; Slider(sound, "VoiceVolume").value = .33f;
                await InvokeTask(screen, "SaveVoiceSettingsAsync"); Check(root.Config.Configuration.BgmVolume == .21f && root.Config.Configuration.VoiceVolume == .33f && root.Config.Configuration.SubtitleFontSize == 29, "soundSavePreservesReadingSettings");
                screen.OpenDataSettings(); await Settled(screen, ct); var files = Field<RectTransform>(screen, "settingsPanel");
                Check(new[] {"Files", "Connection", "Backup", "Health", "Memories", "MusicFiles", "Home", "Exit"}.All(n => files.GetComponentsInChildren<Button>().Any(b => b.name == n)), "allMaintenanceEntrypointsDiscoverable");
                await Capture("设置_资料.png");
                screen.OpenCharacterCard(); await Settled(screen, ct); var card = Field<RectTransform>(screen, "cardPanel");
                var name = Field<TMP_InputField>(screen, "cardName"); var persona = Field<TMP_InputField>(screen, "cardPersona"); var relationship = Field<TMP_InputField>(screen, "cardRelationship");
                var original = root.Config.Identity; Check(name.text == original.DisplayName && persona.text == original.Persona && relationship.text == original.Relationship, "cardLoadsExactSavedFields");
                Check(name.GetComponent<Image>().color.a == 0 && name.transform.Find("Field underline") != null, "underlinedCharacterFields");
                await Capture("人物设定.png"); name.text = "隔离验证未花"; screen.OpenSpaces(); Check(screen.ConfirmationVisible && screen.ActivePanel == "CharacterCard", "cardUnsavedNavigationProtected"); screen.Back();
                name.text = ""; await InvokeTask(screen, "SaveCardAsync"); Check(root.Config.Identity.DisplayName == original.DisplayName, "invalidCardDoesNotReplaceIdentity");
                name.text = "隔离验证未花"; persona.text = original.Persona + "\n隔离验证补充，非玩家设定。"; await InvokeTask(screen, "SaveCardAsync");
                Check(root.Character == core && core.Identity.DisplayName == "隔离验证未花" && root.Config.Identity.Relationship == original.Relationship, "savedCardAppliesWithoutCoreOrRelationshipRewrite");
                Check(File.Exists(root.Config.CharacterCardPath + ".previous.bak"), "previousCardPreserved");
                persona.text = "未保存的临时草稿"; Button(card, "Reload").onClick.Invoke(); Confirm(screen); await WaitIdle(screen, ct);
                Check(persona.text == root.Config.Identity.Persona, "cardTakeBackReadsSavedFile");
                screen.OpenSpaces(); await Settled(screen, ct); var spaces = Field<RectTransform>(screen, "spacePanel");
                Check(Field<RawImage>(screen, "spacePreview").texture != null, "actualSpacePreviewRendered"); await Capture("陪伴空间.png");
                string previousSpace = screen.ActiveSpaceId; int spaceCount = root.Library.Spaces.Spaces.Count;
                string invalid = Path.Combine(root.Config.UserDataPath, "无效验证空间"); Directory.CreateDirectory(invalid);
                await screen.ImportSpaceAsync(invalid); Check(screen.ActiveSpaceId == previousSpace && root.Library.Spaces.Spaces.Count == spaceCount, "invalidImportKeepsCurrentSpace");
                await screen.ImportSpaceAsync(Path.Combine(Application.streamingAssetsPath, "CH0069_Source")); var imported = root.Library.Spaces.Spaces.Last();
                Check(root.Library.Spaces.Spaces.Count == spaceCount + 1, "validSpaceImportUpdatesCatalog");
                await screen.SelectSpaceAsync(imported.Id); await Task.Delay(200, ct);
                Check(screen.ActiveSpaceId == imported.Id && root.Character == core && root.Database == database, "spaceSwapKeepsSharedServices");
                Check(!screen.WindowView.HasWindow, "unadaptedSpaceHasMenuFallback"); await Capture("导入空间回退.png");
                await screen.SelectSpaceAsync(previousSpace); await Task.Delay(200, ct); Check(screen.ActiveSpaceId == previousSpace && screen.WindowView.HasWindow, "returnToBuiltinRestoresWindowNavigation");
                screen.OpenDataSettings(); Button(Field<RectTransform>(screen, "settingsPanel"), "MusicFiles").onClick.Invoke(); await Settled(screen, ct);
                var musicPanel = Field<RectTransform>(screen, "musicPanel"); Check(screen.ActivePanel == "Music" && musicPanel.GetComponent<Image>().color.a == 0, "musicManagementIsFloatingSecondaryPage");
                int songs = root.Library.Music.Tracks.Count; await screen.ImportMusicAsync(Path.Combine(invalid, "missing.wav")); Check(root.Library.Music.Tracks.Count == songs, "invalidMusicImportKeepsCatalog");
                musicPanel.GetComponentsInChildren<Button>().First(b=>b.name=="Remove").onClick.Invoke(); Check(screen.ConfirmationVisible && root.Library.Music.Tracks.Count == songs, "removeSongRequiresNamedConfirmation"); await Capture("移出音乐确认.png"); screen.Back();
                await Capture("音乐文件管理.png");
                var removedTrack=root.Library.Music.Tracks.First();string retainedAudio=root.Library.Resolve(removedTrack.Path);
                musicPanel.GetComponentsInChildren<Button>().First(b=>b.name=="Remove").onClick.Invoke();Confirm(screen);await WaitIdle(screen,ct);
                Check(root.Library.Music.Tracks.Count==songs-1&&File.Exists(retainedAudio),"confirmedSongRemovalKeepsSourceFile");
                screen.Back(); Check(screen.ActivePanel == "Settings", "secondaryBackReturnsToFiles");
                var conversations=(IConversationStore)root.Database;var store=(IMemoryStore)root.Database;
                var turn=await conversations.BeginTurnAsync("v2-maintenance-memory","隔离验证：我喜欢桂花乌龙茶。",InteractionSource.Companion,ct);
                await conversations.CompleteTurnAsync(turn,"隔离验证：记住这一条测试资料。",core.State,ct);
                var job=await store.ClaimExtractionAsync(ct);var extracted=new MemoryExtraction();
                extracted.Items.Add(new MemoryRecord{Kind="fact",Key="隔离验证饮品",Content="喜欢桂花乌龙茶",Evidence="我喜欢桂花乌龙茶",SourceMessageId=turn.UserMessageId,Confidence=.95,Importance=.7});
                Check(await store.ApplyExtractionAsync(job,extracted,ct),"isolatedMemoryFixtureSaved");
                await InvokeTask(screen,"OpenMemoryAsync");await Settled(screen,ct);var memories=Field<RectTransform>(screen,"memoryPanel");
                Button(memories,"Forget").onClick.Invoke();Check(screen.ConfirmationVisible&&Field<RectTransform>(screen,"editPrompt").Find("Message").GetComponent<TMP_Text>().text.Contains("桂花乌龙茶"),"memoryRemovalNamesExactContent");
                await Capture("记忆移除确认.png");screen.Back();Check((await store.LoadMemoriesAsync("fact",true,20,ct)).Count==1,"cancelMemoryRemovalKeepsRecord");
                Button(memories,"Forget").onClick.Invoke();Confirm(screen);while(Field<bool>(screen,"memoryLoading"))await Task.Delay(20,ct);
                Check((await store.LoadMemoriesAsync("fact",true,20,ct)).Count==0&&(await root.History.ReadPageAsync(null,ct)).Turns.Count==1,"confirmedMemoryRemovalKeepsOriginalHistory");
                screen.Back();screen.OpenSettings();Button(Field<RectTransform>(screen,"voicePanel"),"ClearCache").onClick.Invoke();
                Check(screen.ConfirmationVisible&&Field<RectTransform>(screen,"editPrompt").Find("Message").GetComponent<TMP_Text>().text.Contains("无法复听"),"cacheClearExplainsLossBeforeConfirmation");screen.Back();screen.OpenDataSettings();

                string backupPath = Path.Combine(root.Config.UserDataPath, "v2-maintenance-test.mikabackup"); var backup = await root.BackupAsync(backupPath, false, ct);
                Check(backup.Files.Count > 0 && File.Exists(backupPath), "realBackupCreatesValidatedArchive");
                var preview = await root.Backups.InspectAsync(backupPath, ct); Check(preview.Description.Length > 0, "realBackupInspectionProducesPreview"); root.Backups.Discard(preview);
                foreach (var page in new[] {PlayerMaintenance.Page.Connection, PlayerMaintenance.Page.Backup, PlayerMaintenance.Page.Health})
                {
                    screen.OpenDataSettings(); Button(Field<RectTransform>(screen, "settingsPanel"), page.ToString()).onClick.Invoke();
                    PlayerMaintenance maintenance = null;
                    while (maintenance == null || root.Controller.IsSwitching) { ct.ThrowIfCancellationRequested(); await Task.Delay(20, ct); maintenance = FindFirstObjectByType<PlayerMaintenance>(); }
                    await Task.Delay(120, ct);
                    string expected = page == PlayerMaintenance.Page.Connection ? "SaveKey" : page == PlayerMaintenance.Page.Backup ? "InspectBackup" : "ResetConfig";
                    Check(maintenance.GetComponentsInChildren<Button>().Any(b => b.name == expected), "safeRouteOpens" + page);
                    if (page == PlayerMaintenance.Page.Connection) Check(Field<TMP_InputField>(maintenance, "key").contentType == TMP_InputField.ContentType.Password, "keyFieldRemainsMasked");
                    if (page == PlayerMaintenance.Page.Backup)
                    {
                        Check(new[] {"RestorePoints", "OriginalData", "ConfigOriginals"}.All(n => maintenance.GetComponentsInChildren<Button>().Any(b => b.name == n)), "recoveryLocationsRemainReachable");
                        var inspected = await root.Backups.InspectAsync(backupPath, ct);
                        typeof(PlayerMaintenance).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(maintenance, inspected);
                        maintenance.SendMessage("RenderPreview"); Check(maintenance.GetComponentsInChildren<Button>().Any(b => b.name == "ConfirmRestore"), "restoreHasExplicitPreviewAndConfirmation");
                        await Capture("备份恢复预览.png"); maintenance.SendMessage("Backup");
                    }
                    if (page == PlayerMaintenance.Page.Health)
                    {
                        Button(maintenance, "ResetConfig").onClick.Invoke(); var selection = Field<ConfigResetSelection>(maintenance, "resetSelection");
                        Check(selection.Armed == selection.Selected && Field<TMP_Text>(maintenance, "note").text.Contains(selection.Selected), "configResetConfirmationNamesExactFile");
                        Button(maintenance, "SelectConfig").onClick.Invoke(); Check(selection.Armed == null, "changingResetTargetDisarmsConfirmation");
                        Button(maintenance, "Diagnostic").onClick.Invoke(); await Task.Delay(150, ct);
                        string diagnostic = File.ReadAllText(Directory.GetFiles(Path.Combine(root.Config.UserDataPath, "Diagnostics"), "diagnostic-*.json").Last());
                        Check(!diagnostic.Contains(root.Config.UserDataPath) && !diagnostic.Contains(original.Persona) && !diagnostic.Contains("未保存的临时草稿"), "diagnosticsExcludeIdentityAndPrivatePaths");
                    }
                    await Capture("安全维护_" + page + ".png"); maintenance.SendMessage("Close"); await Task.Delay(80, ct);
                    await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct); screen = await Ready(ct);
                }
                Check(root.Character == core && root.Database == database, "maintenanceRoundTripsReuseSingleCoreAndDatabase");
                screen.OpenDisplaySettings();await Settled(screen,ct);Screen.SetResolution(960,540,FullScreenMode.Windowed);await Task.Delay(350,ct);
                Check(Screen.width==960&&Screen.height==540,"actualSmallWindowResolution");await Capture("小窗口设置.png");
                Check(Field<RectTransform>(screen, "displayPanel").GetComponentsInChildren<Slider>().All(s => s.gameObject.activeInHierarchy), "smallWindowKeepsSettingsScrollable");
                screen.Back(); Check(root.Voice.OwnedProcessId == 0, "noRealVoiceProcessStarted");
                await root.ShutdownAsync(); Check(root.ShutdownSucceeded, "savedShutdown");
            }
            catch (Exception exception) { error = exception.ToString(); }
            bool passed = error == null && checks.Values.All(v => v);
            AtomicDocument.WriteJson(Path.Combine(output, "native.json"), new { passed, error, checks, graphics = SystemInfo.graphicsDeviceName, scope = "Isolated settings, identity and imports; real UI/config/backup/Spine; no real API, voice model or committed restore/restart" }, default, false);
            if (root != null) await root.ShutdownAsync(); Application.Quit(passed ? 0 : 1);
        }
        async Task<CompanionScreen> Ready(CancellationToken ct)
        {
            CompanionScreen screen = null;
            while (screen == null || !screen.IsReady || screen.WindowView == null || GetComponent<AppRoot>().Controller.IsSwitching)
            { ct.ThrowIfCancellationRequested(); screen = FindFirstObjectByType<CompanionScreen>(); await Task.Delay(20, ct); }
            return screen;
        }
        static async Task WaitIdle(CompanionScreen screen, CancellationToken ct)
        { while (Field<bool>(screen, "libraryBusy") || Field<bool>(screen, "voiceUiBusy")) await Task.Delay(20, ct); }
        static async Task Settled(CompanionScreen screen, CancellationToken ct)
        { screen.WindowView.Focused = true; while(!screen.WindowView.AtWindow || screen.WindowView.PanelBlend<.99f){ct.ThrowIfCancellationRequested();screen.WindowView.Focused=true;await Task.Delay(20,ct);} await Task.Delay(160, ct); }
        Task Capture(string name) { var completion = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(Path.Combine(output, name), completion)); return completion.Task; }
        System.Collections.IEnumerator CaptureFrame(string path, TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame(); Texture2D image = null;
            try { image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); done.TrySetResult(true); }
            catch (Exception exception) { done.TrySetException(exception); }
            finally { if (image != null) Destroy(image); }
        }
#endif
    }
}
