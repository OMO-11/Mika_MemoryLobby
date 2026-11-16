using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        private RectTransform libraryPanel, musicPanel, spacePanel, cardPanel, diaryPanel, maintenancePanel, musicContent, spaceContent, diaryContent;
        private TMP_Text musicNote, spaceNote, cardNote, diaryNote, maintenanceNote;
        private TMP_InputField cardName, cardPersona, cardRelationship;
        private List<DiaryEntry> diaries = new List<DiaryEntry>();
        private int diaryIndex;
        private ScrollRect diaryScroll;
        private readonly Dictionary<string, float> diaryPositions = new Dictionary<string, float>();
        private string renderedDiaryKey, selectedDiaryId;
        private bool libraryBusy;
        private bool diaryGenerating;
        private Task diaryWork=Task.CompletedTask;
        public string CurrentMusicId => libraryMusic?.CurrentId;
        public string ActiveSpaceId => visual?.ActiveSpaceId;
        public int MusicRepeatCount => music?.RepeatCount ?? 0;
        private void BuildLibraryPanels()
        {
            libraryPanel = BuildPanel("Library", "资料与回顾");
            UI.Button(libraryPanel, "Music", "背景音乐", new Vector2(-200, 135), new Vector2(330, 65), OpenMusic);
            UI.Button(libraryPanel, "Spaces", "陪伴空间", new Vector2(200, 135), new Vector2(330, 65), OpenSpaces);
            UI.Button(libraryPanel, "CharacterCard", "人物设定", new Vector2(-200, 35), new Vector2(330, 65), OpenCharacterCard);
            UI.Button(libraryPanel, "Diary", "我们的相处日记", new Vector2(200, 35), new Vector2(330, 65), () => _ = OpenDiaryAsync());
            UI.Button(libraryPanel, "Maintenance", "资料文件与维护", new Vector2(0, -80), new Vector2(400, 58), OpenMaintenance);
            UI.Text(libraryPanel, "Hint", "音乐、空间和人物卡都保存在独立文件中。\n平时用这里的按钮管理即可，无需编辑代码。", new Vector2(0, -205), new Vector2(800, 90), 21);
            musicPanel = BuildPanel("Music", "背景音乐");
            musicContent = BuildScroll(musicPanel, "Songs", new Vector2(0, -8), new Vector2(840, 390), out _);
            UI.Button(musicPanel, "Previous", "上一首", new Vector2(-260, 238), new Vector2(150, 38), () => _ = StepMusicAsync(-1));
            UI.Button(musicPanel, "Add", "添加音乐", new Vector2(0, 238), new Vector2(180, 38), AddMusic);
            UI.Button(musicPanel, "Next", "下一首", new Vector2(260, 238), new Vector2(150, 38), () => _ = StepMusicAsync(1));
            musicNote = UI.Text(musicPanel, "Note", "", new Vector2(0, -252), new Vector2(830, 65), 18);
            spacePanel = BuildPanel("Spaces", "陪伴空间");
            spaceContent = BuildScroll(spacePanel, "Spaces list", new Vector2(0, -8), new Vector2(840, 390), out _);
            UI.Button(spacePanel, "Add", "添加空间资源包", new Vector2(0, 238), new Vector2(300, 38), AddSpace);
            spaceNote = UI.Text(spacePanel, "Note", "", new Vector2(0, -252), new Vector2(830, 65), 18);
            cardPanel = BuildPanel("CharacterCard", "人物设定");
            cardName = CardField("Name", "名字", 220, 180, 44, 64);
            cardPersona = CardField("Persona", "性格与说话方式", 125, 40, 125, 8000);
            cardRelationship = CardField("Relationship", "与老师的关系与边界", -45, -125, 105, 4000);
            UI.Button(cardPanel, "Reload", "重新读取文件", new Vector2(-190, -215), new Vector2(270, 42), () => _ = ReloadCardAsync());
            UI.Button(cardPanel, "Save", "保存并应用", new Vector2(190, -215), new Vector2(270, 42), () => _ = SaveCardAsync());
            cardNote = UI.Text(cardPanel, "Note", "", new Vector2(0, -270), new Vector2(830, 45), 17);
            diaryPanel = BuildPanel("Diary", "我们的相处日记");
            diaryContent = BuildScroll(diaryPanel, "Diary pages", new Vector2(0, -8), new Vector2(840, 390), out diaryScroll);
            UI.Button(diaryPanel, "Previous", "上一篇", new Vector2(-285, 238), new Vector2(140, 38), () => { if (diaryIndex + 1 < diaries.Count) { diaryIndex++; RenderDiary(); } });
            UI.Button(diaryPanel, "Generate", "回望从相识到现在", new Vector2(0, 238), new Vector2(310, 38), () => _ = GenerateDiaryAsync());
            UI.Button(diaryPanel, "Next", "下一篇", new Vector2(285, 238), new Vector2(140, 38), () => { if (diaryIndex > 0) { diaryIndex--; RenderDiary(); } });
            UI.Button(diaryPanel, "Folder", "打开日记文件夹", new Vector2(-190, -250), new Vector2(270, 38), OpenCurrentDiaryFile);
            UI.Button(diaryPanel, "Cancel", "停止整理", new Vector2(190, -250), new Vector2(200, 38), () => _ = CancelDiaryGenerationAsync());
            diaryNote = UI.Text(diaryPanel, "Note", "", new Vector2(0, -215), new Vector2(820, 35), 17);
            maintenancePanel = BuildPanel("Maintenance", "资料文件与维护");
            maintenanceNote = UI.Text(maintenancePanel, "Details", "", new Vector2(0, 60), new Vector2(810, 290), 20, TextAlignmentOptions.TopLeft);
            UI.Button(maintenancePanel, "Folder", "打开资料文件夹", new Vector2(-205, -120), new Vector2(340, 46), () => PlayerFiles.OpenFolder(root.Config.UserDataPath));
            UI.Button(maintenancePanel, "Reload", "重新读取资料", new Vector2(205, -120), new Vector2(340, 46), () => _ = ReloadLibraryAsync());
            UI.Button(maintenancePanel, "Check", "检查并保存报告", new Vector2(-205, -190), new Vector2(340, 46), CheckLibrary);
            UI.Button(maintenancePanel, "Guide", "查看使用说明", new Vector2(205, -190), new Vector2(340, 46), () => PlayerFiles.ReadText(Path.Combine(root.Config.UserDataPath, "使用说明.md")));
        }
        private TMP_InputField CardField(string name, string title, float titleY, float inputY, float height, int limit)
        {
            var label = UI.Text(cardPanel, name + "Label", title, new Vector2(0, titleY), new Vector2(800, 30), 18, TextAlignmentOptions.Left); label.color = UI.Accent;
            var panel = UI.Panel(cardPanel, name, new Vector2(0, inputY), new Vector2(810, height), new Color(.13f, .17f, .26f));
            var viewport = UI.Rect(panel.transform, "Viewport", Vector2.zero, new Vector2(785, height - 8)); viewport.gameObject.AddComponent<RectMask2D>();
            var text = UI.Text(viewport, "Text", "", Vector2.zero, new Vector2(785, height - 8), 20, TextAlignmentOptions.TopLeft); text.overflowMode = TextOverflowModes.Overflow;
            var field = panel.gameObject.AddComponent<TMP_InputField>(); field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)text;
            field.fontAsset = UI.Chinese; field.richText = false; field.lineType = limit == 64 ? TMP_InputField.LineType.SingleLine : TMP_InputField.LineType.MultiLineNewline; field.characterLimit = limit;
            field.onSelect.AddListener(_ => Keyboard.current?.SetIMEEnabled(true)); field.onDeselect.AddListener(_ => Keyboard.current?.SetIMEEnabled(false)); return field;
        }
        private void CloseLibraryPanels()
        { RememberDiaryPosition(); foreach (var panel in new[] {libraryPanel, musicPanel, spacePanel, cardPanel, diaryPanel, maintenancePanel}) if (panel != null) RetirePanel(panel); }
        public void OpenLibrary() { if(!TryNavigate(OpenLibrary))return;ShowPanel("Library", libraryPanel); }
        public void OpenMusic() { if(!TryNavigate(OpenMusic))return;ShowPanel("Music", musicPanel); RenderMusic(); }
        private void RenderMusic()
        {
            ClearRows(musicContent);
            foreach (var track in root.Library.Music.Tracks)
            {
                var row = UI.Rect(musicContent, "Track", Vector2.zero, new Vector2(580, 64)); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                var title=UI.Text(row,"Title",track.Title,new Vector2(-115,0),new Vector2(340,54),20,TextAlignmentOptions.MidlineLeft);StyleInk(title,20);
                var play=UI.Button(row,"Select",track.Id==CurrentMusicId?(music.Paused?"已暂停":"播放中"):"播放",new Vector2(120,0),new Vector2(100,44),()=>_ = SelectMusicAsync(track.Id));FloatButton(play,17);
                var remove=UI.Button(row,"Remove","移出歌单",new Vector2(230,0),new Vector2(115,44),()=>ConfirmAction("将《"+track.Title+"》移出歌单？原音频文件仍保留。",()=>_ = RemoveMusicAsync(track.Id)));FloatButton(remove,16);
            }
            musicNote.text = libraryMusic.LastError ?? "当前歌曲单曲循环；仅选歌或上下首会切换。\n添加 MP3、OGG、WAV，原文件不会被移动。";
        }
        private async Task LibraryAction(Func<CancellationToken, Task> action, TMP_Text note, CancellationToken ct)
        {
            if (libraryBusy) { note.text = "刚才的操作还在收尾，请稍等。"; return; }
            libraryBusy = true;
            try { await action(ct); }
            catch (OperationCanceledException) { if (this != null && note != null && !ct.IsCancellationRequested) note.text = "操作已取消。"; }
            catch (Exception error)
            { root.Logger.Error("player library action failed", error); if (this != null && !ct.IsCancellationRequested) note.text = "刚才没能完成，当前可用资料已保留。请检查文件内容和权限后再试一次。"; }
            finally { libraryBusy = false; }
        }
        public Task SelectMusicAsync(string id)
        {
            var ct = panelCancellation?.Token ?? destroyCancellationToken;
            return LibraryAction(async token => { await libraryMusic.SelectAsync(id, token); if (this != null && !token.IsCancellationRequested && panelName == "Music") RenderMusic(); }, musicNote, ct);
        }
        private Task StepMusicAsync(int step)
        {
            var tracks = root.Library.Music.Tracks; if (tracks.Count == 0) return Task.CompletedTask;
            int index = tracks.FindIndex(t => t.Id == CurrentMusicId); return SelectMusicAsync(tracks[(index + step + tracks.Count) % tracks.Count].Id);
        }
        private void AddMusic() { string file = PlayerFiles.MusicFile(); if (file != null) _ = ImportMusicAsync(file); else musicNote.text = "已取消添加音乐，歌单保持原样。"; }
        public Task ImportMusicAsync(string file)
        {
            var ct = panelCancellation?.Token ?? destroyCancellationToken;
            return LibraryAction(async token =>
            {
                var check = await Bgm.BgmFilePlayer.LoadAsync(file, token, root.Library.Music.MaximumDurationMinutes); Destroy(check);
                await root.Library.ImportMusicAsync(file, token);
                if (this != null && !token.IsCancellationRequested && panelName=="Music") { RenderMusic(); musicNote.text = "歌曲已添加，点击播放即可切换。"; }
            }, musicNote, ct);
        }
        private Task RemoveMusicAsync(string id)
        {
            var ct = panelCancellation.Token;
            return LibraryAction(async token =>
            {
                var next = root.Library.Music; next.Tracks.RemoveAll(t => t.Id == id);
                if (next.SelectedTrackId == id) next.SelectedTrackId = next.Tracks.FirstOrDefault()?.Id;
                await root.Library.SaveMusicAsync(next, token);
                if (id == CurrentMusicId) { if (next.Tracks.Count > 0) await libraryMusic.SelectAsync(next.SelectedTrackId, token); else await music.FadeOutAsync(token); }
                if (this != null && !token.IsCancellationRequested && panelName=="Music") RenderMusic();
            }, musicNote, ct);
        }
        public void OpenSpaces() { if(!TryNavigate(OpenSpaces))return;ShowPanel("Spaces", spacePanel); RenderSpaces();RefreshSpacePreview(); }
        private void RenderSpaces()
        {
            ClearRows(spaceContent);
            foreach (var space in root.Library.Spaces.Spaces)
            {
                var row = UI.Rect(spaceContent, "Space", Vector2.zero, new Vector2(580, 44)); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
                var title=UI.Text(row,"Title",space.Title,new Vector2(-90,0),new Vector2(390,54),21,TextAlignmentOptions.MidlineLeft);StyleInk(title,21);
                var select=UI.Button(row,"Select",space.Id==ActiveSpaceId?"当前空间":"进入",new Vector2(215,0),new Vector2(140,44),()=>_ = SelectSpaceAsync(space.Id));FloatButton(select,17);select.interactable=space.Id!=ActiveSpaceId;
            }
            spaceNote.text = visual.SpaceError ?? "选择空间只更换画面，人物与共同记忆保持连续。";
        }
        private void AddSpace() { string folder = PlayerFiles.SpaceFolder(); if (folder != null) _ = ImportSpaceAsync(folder); else spaceNote.text = "已取消添加空间，当前空间保持原样。"; }
        public Task ImportSpaceAsync(string folder)
        {
            var ct = panelCancellation?.Token ?? destroyCancellationToken;
            return LibraryAction(async token =>
            {
                await root.Library.ImportSpaceAsync(folder, CompanionPresentation.ValidateSpaceRuntime, token);
                if (this != null && !token.IsCancellationRequested && panelName=="Spaces") { RenderSpaces(); spaceNote.text = "空间已添加，点击进入即可切换。"; }
            }, spaceNote, ct);
        }
        public async Task SelectSpaceAsync(string id)
        {
            if(id==ActiveSpaceId)return;
            bool committed=false;
            var ct = panelCancellation?.Token ?? destroyCancellationToken;
            await LibraryAction(async token =>
            {
                await StopDialogueAsync();
                await visual.SwitchSpaceAsync(root.Library.Spaces.Spaces.Single(s => s.Id == id), token);
                committed=true;
            }, spaceNote, ct);
            if(this==null||!committed)return;
            // Selection has committed: retire the old UI before binding the new room.
            // The outgoing view still owns the shared camera. Detach it before UI cleanup
            // can reapply its zoom to the incoming room.
            if(WindowView!=null){WindowView.gameObject.SetActive(false);Destroy(WindowView.gameObject);WindowView=null;}
            windowLoader=null;
            spacePanel.gameObject.SetActive(false);CloseAdditionalPanels();
            panelName=null;menuOpen=false;queuedWindowInput=false;menuPanel.gameObject.SetActive(false);
            transitionPanel=null;UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            UpdateWindowView();WindowView?.GoCharacter();ResolveVisual();
        }
        public void OpenCharacterCard()
        {
            if(!TryNavigate(OpenCharacterCard))return;
            ShowPanel("CharacterCard", cardPanel); FillCard();
            cardNote.text = root.Config.CharacterCardIssue ?? "只由老师主动修改，保存后应用；共同记忆不会重置。";
        }
        private void FillCard()
        { var value = root.Config.Identity; cardName.text = value.DisplayName; cardPersona.text = value.Persona; cardRelationship.text = value.Relationship; }
        private Task SaveCardAsync()
        {
            if (!ActiveTurn.IsCompleted) { cardNote.text = "等这次聊完，再保存人物卡。"; return Task.CompletedTask; }
            if(string.IsNullOrWhiteSpace(cardName.text)){cardNote.text="请填写名字后再保存。";cardName.Select();return Task.CompletedTask;}
            var ct = panelCancellation.Token;
            return LibraryAction(async token =>
            {
                await root.SaveCharacterCardAsync(new CharacterIdentity(root.Config.Identity.Id, cardName.text.Trim(), cardPersona.text, cardRelationship.text), token);
                if (this != null && !token.IsCancellationRequested) { subtitle.transform.parent.Find("Speaker").GetComponent<TMP_Text>().text = root.Character.Identity.DisplayName; FillCard();cardNote.text = "人物卡已保存并应用。上一版文件保留为备份。"; }
            }, cardNote, ct);
        }
        private Task ReloadCardAsync()
        {
            if (!ActiveTurn.IsCompleted) { cardNote.text = "等这次聊完，再读取人物卡。"; return Task.CompletedTask; }
            var ct = panelCancellation.Token;
            return LibraryAction(async token => { await root.ReloadCharacterCardAsync(token); if (this != null && !token.IsCancellationRequested) { FillCard(); cardNote.text = root.Config.CharacterCardIssue ?? "已重新读取可用的人物卡。"; } }, cardNote, ct);
        }
        public async Task OpenDiaryAsync()
        {
            if(!TryNavigate(()=>_ = OpenDiaryAsync()))return;
            var ct = ShowPanel("Diary", diaryPanel);
            try { await root.Diary.SyncExportsAsync(ct); diaries = (await root.Diary.ListAsync(ct)).ToList(); ct.ThrowIfCancellationRequested(); diaryIndex = Math.Max(0, diaries.FindIndex(d => d.Id == selectedDiaryId)); RenderDiary(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("diary open failed", error); if (this != null && !ct.IsCancellationRequested) diaryNote.text = "回顾暂时没能打开，请再试一次。"; }
        }
        private void RenderDiary()
        {
            RememberDiaryPosition();
            ClearRows(diaryContent);
            string body = "这里会留下我们从相识到现在的回顾。\n老师想回望时，再点击上方的按钮。";
            if (diaries.Count > 0)
            {
                var entry = diaries[diaryIndex]; body = entry.Status == "active" ? entry.Body : "这篇回顾因记忆更正或遗忘而收起，可以重新整理。";
                if(diaryMeta!=null)diaryMeta.text="回望 "+entry.FirstAt.ToLocalTime().ToString("yyyy.MM.dd")+" → "+entry.LastAt.ToLocalTime().ToString("yyyy.MM.dd")+"   ·   "+entry.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd");
                selectedDiaryId = entry.Id;
                // Invalidated entries get a fresh reading position; old text is never reloaded for navigation.
                renderedDiaryKey = entry.Id + ":" + entry.Status;
            }
            else { selectedDiaryId = renderedDiaryKey = null;if(diaryMeta!=null)diaryMeta.text="开始相伴 → 现在"; }
            var display = root.Display.Value;
            float width=Mathf.Max(200,diaryScroll.viewport.rect.width-48);
            var text = UI.Text(diaryContent, "Entry", body, Vector2.zero, new Vector2(width, 100), display.DiaryFontSize, TextAlignmentOptions.TopLeft);StyleInk(text,display.DiaryFontSize); text.overflowMode = TextOverflowModes.Overflow;
            text.lineSpacing = display.DiaryLineSpacing; text.paragraphSpacing = 12;
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = text.GetPreferredValues(body, width, 0).y + 40;
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(diaryContent);
            diaryScroll.StopMovement(); diaryScroll.verticalNormalizedPosition = renderedDiaryKey != null && diaryPositions.TryGetValue(renderedDiaryKey, out float position) ? position : 1;
            diaryNote.text = diaries.Count == 0 ? "想回望时，点击『回望至今』。" : "第 " + (diaryIndex + 1) + " / " + diaries.Count + " 篇 · 可以继续上次的阅读。";
            RefreshDiaryActions();
        }
        private void RefreshDiaryActions(){if(diaryPanel==null)return;diaryPanel.Find("Cancel").gameObject.SetActive(diaryGenerating);diaryPanel.Find("Generate").GetComponent<Button>().interactable=!diaryGenerating;}
        private void RememberDiaryPosition()
        {
            if (renderedDiaryKey != null && diaryScroll != null && diaryPanel != null && diaryPanel.gameObject.activeSelf)
                diaryPositions[renderedDiaryKey] = diaryScroll.verticalNormalizedPosition;
        }
        public async Task GenerateDiaryAsync()
        {
            if (!ActiveTurn.IsCompleted || root.PlayerWorkBusy) { diaryNote.text = "等这次聊完，再一起回望过去。"; return; }
            if (panelCancellation == null || panelName != "Diary") return; var ct = panelCancellation.Token;
            diaryWork=LibraryAction(async token =>
            {
                diaryNote.text = "正在翻阅过去的聊天，随时可以停止整理。";
                diaryGenerating=true;RefreshDiaryActions();
                var progress = new Progress<long>(count => { if (this != null && !token.IsCancellationRequested && panelName == "Diary") diaryNote.text = "正在翻阅过去的聊天 · " + count + " 轮"; });
                try
                {
                    await root.PlayerWorkAsync(inner => root.Diary.GenerateAsync(progress, inner), token);
                    token.ThrowIfCancellationRequested(); diaries = (await root.Diary.ListAsync(token)).ToList(); diaryIndex = 0; RenderDiary();
                }
                catch (InvalidOperationException error) when (error.Message == "DiaryEmpty") { diaryNote.text = "还没有完整的共同聊天，等留下些回忆再来吧。"; }
                catch (LLMServiceException error) { root.Logger.Error("diary generation failed code=" + error.Code, error); if(this!=null&&!token.IsCancellationRequested&&panelName=="Diary")diaryNote.text = "这次没能整理好，请检查连接后再试。"; }
                finally{diaryGenerating=false;if(this!=null)RefreshDiaryActions();}
            }, diaryNote, ct);
            await diaryWork;
        }
        private async Task CancelDiaryGenerationAsync()
        {
            if(!diaryGenerating||panelCancellation==null)return;
            var previous=panelCancellation;previous.Cancel();
            try{await diaryWork;}catch(OperationCanceledException){}
            if(this!=null&&panelName=="Diary"&&ReferenceEquals(panelCancellation,previous))await OpenDiaryAsync();
        }
        public void OpenMaintenance()
        {
            if(!TryNavigate(OpenMaintenance))return;
            ShowPanel("Maintenance", maintenancePanel);
            maintenanceNote.text = "背景音乐：Config/bgm_playlist.json\n陪伴空间：Config/companion_spaces.json\n人物设定：Config/character_card.json\n相处日记：Diary/\n\n资料位置：\n" + root.Config.UserDataPath + "\n\n导入后选中即可使用；人物设定保存即生效，历史和记忆保留。\n上一版恢复：设置 → 资料 → 资料检查。";
        }
        private Task ReloadLibraryAsync()
        {
            if (!ActiveTurn.IsCompleted) { maintenanceNote.text = "等这次聊完，再重新读取资料。"; return Task.CompletedTask; }
            var ct = panelCancellation.Token;
            return LibraryAction(async token =>
            {
                root.Library.Reload(token); await root.ReloadCharacterCardAsync(token);
                if (root.Library.Music.Tracks.Count > 0) await libraryMusic.SelectAsync(root.Library.Music.SelectedTrackId, token, true);
                else await music.FadeOutAsync(token);
                var selected = root.Library.Spaces.Spaces.Find(s => s.Id == root.Library.Spaces.SelectedSpaceId);
                if (selected != null) await visual.SwitchSpaceAsync(selected, token, true);
                await root.Diary.SyncExportsAsync(token);
                if (this != null && !token.IsCancellationRequested) maintenanceNote.text = root.Library.Warnings.Count == 0 && root.Config.CharacterCardIssue == null ? "资料已重新读取。人物、历史和记忆保持连续。" : string.Join("\n", root.Library.Warnings) + "\n" + root.Config.CharacterCardIssue;
            }, maintenanceNote, ct);
        }
        private void CheckLibrary()
        {
            if (panelCancellation == null) return;
            try
            {
                string text = root.Library.MaintenanceReport() + "\n数据库版本：" + root.Database.SchemaVersion + "\n程序：" + Application.version + " / Unity " + Application.unityVersion + "\n";
                text += "\n人物卡：" + (root.Config.CharacterCardIssue ?? "可用") + "\n语音：" + (root.VoiceConfigurationIssue ?? root.Voice?.State.ToString() ?? "不可用") + "\n音频缓存目录：AudioCache/\n\n## 文件指纹\n\n用于交接时核对文件版本，不包含人物卡正文或 Key。\n\n";
                foreach (string source in new[] {root.Library.MusicFile, root.Library.SpacesFile, root.Config.CharacterCardPath, Path.Combine(root.Config.UserDataPath, "Config", "voice_runtime.json"), typeof(CharacterCore).Assembly.Location, typeof(CompanionScreen).Assembly.Location})
                {
                    if (!File.Exists(source)) continue;
                    using var input = File.OpenRead(source); using var hash = SHA256.Create();
                    text += "- " + Path.GetFileName(source) + "：" + BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant() + "\n";
                }
                string file = Path.Combine(root.Config.UserDataPath, "资料检查.md"); AtomicDocument.Write(file, text, panelCancellation.Token);
                maintenanceNote.text = "检查报告已保存为资料文件夹中的《资料检查.md》。\n可查看文件状态、路径和维护说明；不会读取或显示 API Key。";
            }
            catch (Exception error) { root.Logger.Error("library inspection failed", error); maintenanceNote.text = "检查报告暂时没能保存，请再试一次。"; }
        }
    }
}
