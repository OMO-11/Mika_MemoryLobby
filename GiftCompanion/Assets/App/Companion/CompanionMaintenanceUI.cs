using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        RectTransform editPrompt;
        Action promptAction;
        GameObject promptPreviousFocus;
        bool navigationApproved, displaySaving, maintenanceUiBuilt;
        bool? motionDraft;
        Image fallbackReadingShade;
        RawImage spacePreview;
        Texture2D spacePreviewTexture;
        TMP_Text activeSpaceTitle;
        public bool ConfirmationVisible => editPrompt != null && editPrompt.gameObject.activeSelf;
        bool EditorBusy => displaySaving || voiceUiBusy || (libraryBusy && !(panelName=="Diary"&&diaryGenerating));

        void BuildMaintenanceUI()
        {
            BuildSettingsSurface(voicePanel, voiceNote, 0);
            BuildSettingsSurface(displayPanel, displayNote, 1);
            BuildSettingsSurface(settingsPanel, settingsNote, 2);
            foreach (Transform child in settingsPanel)
                if (new[] {"BgmVolume", "Volume", "Mute", "TextSize", "SubtitleSize", "Memories", "Save", "Maintenance", "Voice", "Display", "MusicFiles"}.Contains(child.name))
                    child.gameObject.SetActive(false);

            var sound = MaintenanceForm(voicePanel, "Sound form", 444);
            MoveForm(voiceStatus.rectTransform, sound, 0, 0, 610, 40); StyleInk(voiceStatus, 17, .8f);
            MoveForm(voiceVolumeLabel.rectTransform, sound, 0, 58, 240, 40); StyleInk(voiceVolumeLabel, 19);
            MoveSlider(voicePanel.Find("VoiceVolume"), sound, 254, 72, 326);
            MoveForm(voiceEnabledButton.GetComponent<RectTransform>(), sound, 0, 111, 200, 44); FloatButton(voiceEnabledButton, 19);
            MoveForm(voiceLanguageButton.GetComponent<RectTransform>(), sound, 215, 111, 390, 44); FloatButton(voiceLanguageButton, 19);
            MoveForm(volumeLabel.rectTransform, sound, 0, 173, 240, 40); StyleInk(volumeLabel, 19);
            MoveSlider(settingsPanel.Find("Volume"), sound, 254, 187, 326);
            MoveForm(muteButton.GetComponent<RectTransform>(), sound, 0, 218, 220, 44); FloatButton(muteButton, 19);
            MoveButton(voicePanel, "Retry", sound, 0, 274, 180);
            MoveButton(voicePanel, "Stop", sound, 205, 274, 355);
            MoveForm(voiceCacheLabel.rectTransform, sound, 0, 330, 300, 36); StyleInk(voiceCacheLabel, 17, .8f);
            MoveButton(voicePanel, "ClearCache", sound, 315, 326, 270);
            MoveButton(voicePanel, "Guide", sound, 0, 386, 200);
            SettingsFooter(voicePanel, "Save", () => _ = SaveVoiceSettingsAsync(), ResetSoundDraft, ReloadSoundDraft);

            var display = MaintenanceForm(displayPanel, "Display form", 430);
            DisplayRow(displayPanel, display, exposureLabel, "Exposure", 0);
            DisplayRow(displayPanel, display, compressionLabel, "Compression", 59);
            DisplayRow(displayPanel, display, diarySizeLabel, "DiarySize", 118);
            DisplayRow(displayPanel, display, diarySpacingLabel, "DiarySpacing", 177);
            MoveForm(sizeLabel.rectTransform, display, 0, 236, 240, 40); StyleInk(sizeLabel, 19);
            MoveSlider(settingsPanel.Find("SubtitleSize"), display, 254, 250, 326);
            reducedMotionButton = UI.Button(display, "ReduceMotion", "", Vector2.zero, new Vector2(440, 44), () => PreviewMotion(!motionDraft.GetValueOrDefault(root.VisualSettings.Value.ReduceMotion)));
            FormAt(reducedMotionButton.GetComponent<RectTransform>(), 0, 295, 440, 44); FloatButton(reducedMotionButton, 19);
            MoveButton(displayPanel, "OriginalTone", display, 0, 358, 220);
            displayPanel.Find("DefaultDisplay").gameObject.SetActive(false);
            SettingsFooter(displayPanel, "SaveDisplay", () => _ = SaveDisplayDraftAsync(), ResetDisplayDraft, ReloadDisplayDraft);

            var files = MaintenanceForm(settingsPanel, "Files form", 734);
            DataLink(files, "Files", "资料文件夹 ↗", "人物卡、日记与导入素材", 0, () => PlayerFiles.OpenFolder(root.Config.UserDataPath));
            DataLink(files, "Connection", "连接设置", "配置 Key、测试连接", 66, () => OpenSafeMaintenance(PlayerMaintenance.Page.Connection));
            DataLink(files, "Backup", "备份与恢复", "校验预览、确认恢复与恢复点", 132, () => OpenSafeMaintenance(PlayerMaintenance.Page.Backup));
            DataLink(files, "Health", "资料检查", "配置原件、上一版与脱敏诊断", 198, () => OpenSafeMaintenance(PlayerMaintenance.Page.Health));
            DataLink(files, "Memories", "记住的内容", "查看与移除具体记忆", 264, () => _ = OpenMemoryAsync());
            DataLink(files, "MusicFiles", "音乐文件管理", "添加、选择、移出歌曲与单曲循环", 330, OpenMusic);
            DataLink(files, "LibraryCheck", "素材与日记文件", "重读清单、导出检查报告", 396, OpenMaintenance);
            DataLink(files, "RestorePoints", "恢复点与保留原件", "在备份与恢复中查看三个资料位置", 462, () => OpenSafeMaintenance(PlayerMaintenance.Page.Backup));
            DataLink(files, "Home", "应用维护", "进入维护 Home，陪伴状态先保存", 528, () => OpenSafeMaintenance(PlayerMaintenance.Page.Health));
            DataLink(files, "Exit", "退出应用", "保存状态并结束本机自有语音进程", 610, () => RequestNavigation(() => _ = LeaveAsync(true)));

            BuildCardSurface();
            BuildSpaceSurface();
            BuildReadingPage(musicPanel, "音乐文件管理", 4, musicContent.parent.GetComponent<ScrollRect>(), musicNote);
            Place(musicContent.parent.GetComponent<RectTransform>(), 548, 350, 620, 245);
            Place(musicPanel.Find("Previous") as RectTransform, 548, 302, 130, 40); FloatButton(musicPanel.Find("Previous").GetComponent<Button>(), 17);
            Place(musicPanel.Find("Add") as RectTransform, 750, 302, 170, 40); FloatButton(musicPanel.Find("Add").GetComponent<Button>(), 17);
            Place(musicPanel.Find("Next") as RectTransform, 1010, 302, 130, 40); FloatButton(musicPanel.Find("Next").GetComponent<Button>(), 17);
            SetupSubpage(memoryPanel, "记住的内容", memoryNote, memoryContent.parent.GetComponent<ScrollRect>());
            SetupSubpage(maintenancePanel, "素材与日记文件", maintenanceNote, null);
            var maintenance = MaintenanceForm(maintenancePanel, "Maintenance form", 590);
            MoveForm(maintenanceNote.rectTransform, maintenance, 0, 0, 600, 340); StyleInk(maintenanceNote, 17);
            maintenanceNote.overflowMode = TextOverflowModes.Overflow;
            MoveButton(maintenancePanel, "Folder", maintenance, 0, 360, 260);
            MoveButton(maintenancePanel, "Reload", maintenance, 295, 360, 260);
            MoveButton(maintenancePanel, "Check", maintenance, 0, 420, 260);
            MoveButton(maintenancePanel, "Guide", maintenance, 295, 420, 260);
            BuildEditPrompt();
            fallbackReadingShade=UI.Panel(canvas.transform,"Fallback reading shade",Vector2.zero,new Vector2(1280,720),new Color(.02f,.03f,.07f,.62f),false);
            fallbackReadingShade.transform.SetAsFirstSibling();fallbackReadingShade.gameObject.SetActive(false);
            UI.Stretch(fallbackReadingShade.rectTransform);
            maintenanceUiBuilt = true;
        }

        void BuildSettingsSurface(RectTransform panel, TMP_Text note, int selected)
        {
            BuildFloatingPage(panel, "设置", 4, note);
            string[] names = {"声音", "画面与阅读", "资料"};
            for (int i = 0; i < names.Length; i++)
            {
                int tab = i;
                var button = UI.Button(panel, "Settings tab " + i, names[i], Vector2.zero, new Vector2(180, 44), () => OpenSettingsTab(tab));
                Place(button.GetComponent<RectTransform>(), 548 + i * 200, 298, 180, 44); FloatButton(button, 20);
                if (selected == i)
                {
                    var line = UI.Panel(panel, "Selected tab", Vector2.zero, new Vector2(150, 1), CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Accent), false);
                    Place(line.rectTransform, 560 + i * 200, 339, 150, 1);
                }
            }
        }
        void OpenSettingsTab(int tab)
        {
            if (tab == 0 && panelName != "Voice") OpenVoiceSettings();
            else if (tab == 1 && panelName != "Display") OpenDisplaySettings();
            else if (tab == 2 && panelName != "Settings") OpenDataSettings();
        }
        public void OpenDataSettings()
        {
            if (!TryNavigate(OpenDataSettings)) return;
            ShowPanel("Settings", settingsPanel);
            settingsNote.text = "需要维护时再打开相应项目；聊天与共同记忆继续保留。";
        }
        void OpenSafeMaintenance(PlayerMaintenance.Page page)
        { RequestNavigation(() => PlayerMaintenance.OpenFromMode(page)); }
        void SetupSubpage(RectTransform panel, string title, TMP_Text note, ScrollRect scroll)
        {
            if (scroll == null) BuildFloatingPage(panel, title, 4, note);
            else BuildReadingPage(panel, title, 4, scroll, note);
            var back = UI.Button(panel, "BackToFiles", "‹  资料", Vector2.zero, new Vector2(140, 40), OpenDataSettings);
            Place(back.GetComponent<RectTransform>(), 548, 302, 140, 40); FloatButton(back, 18);
            if (scroll != null) Place(scroll.GetComponent<RectTransform>(), 548, 350, 620, 260);
        }
        RectTransform MaintenanceForm(RectTransform panel, string name, float height)
        {
            var view = UI.Panel(panel, name, Vector2.zero, new Vector2(620, 260), Color.clear).rectTransform;
            Place(view, 548, 350, 620, 250); view.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 8);
            var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = UI.Rect(view, "Form content", Vector2.zero, new Vector2(600, height));
            FormAt(content, 8, 0, 600, height); scroll.content = content; scroll.viewport = view;
            var track=UI.Panel(view,"Reading scrollbar",Vector2.zero,new Vector2(3,230),CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Ink,.18f));
            FormAt(track.rectTransform,615,10,3,230);
            var handle=UI.Panel(track.transform,"Handle",Vector2.zero,new Vector2(3,40),CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Ink,.65f));
            var scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.handleRect=handle.rectTransform;scrollbar.targetGraphic=handle;scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        static void FormAt(RectTransform rect, float x, float y, float width, float height)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
        static void MoveForm(RectTransform rect, RectTransform parent, float x, float y, float width, float height)
        { rect.SetParent(parent, false); rect.gameObject.SetActive(true); FormAt(rect, x, y, width, height); }
        void MoveSlider(Transform source, RectTransform parent, float x, float y, float width)
        {
            var rect = source.GetComponent<RectTransform>(); MoveForm(rect, parent, x, y, width, 4);
            rect.GetComponent<Image>().color = CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Ink, .32f);
            var slider = rect.GetComponent<Slider>(); slider.handleRect.sizeDelta = new Vector2(10, 22);
            // Preserve a generous hit region around the thin visible track.
            var hit = UI.Panel(rect, "Slider hit area", Vector2.zero, new Vector2(width + 12, 44), Color.clear).rectTransform;
            hit.SetAsFirstSibling(); slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        }
        void MoveButton(RectTransform panel, string name, RectTransform content, float x, float y, float width)
        { var button = panel.Find(name).GetComponent<Button>(); MoveForm(button.GetComponent<RectTransform>(), content, x, y, width, 44); FloatButton(button, 17); }
        void DisplayRow(RectTransform panel, RectTransform content, TMP_Text label, string slider, float y)
        { MoveForm(label.rectTransform, content, 0, y, 245, 40); StyleInk(label, 19); MoveSlider(panel.Find(slider), content, 254, y + 14, 326); }
        void SettingsFooter(RectTransform panel, string saveName, Action save, Action defaults, Action reload)
        {
            var button = panel.Find(saveName).GetComponent<Button>(); button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => save());
            button.GetComponentInChildren<TMP_Text>(true).text = "保存"; Place(button.GetComponent<RectTransform>(), 1010, 613, 160, 44); FloatButton(button, 20);
            var reset = UI.Button(panel, "Defaults", "恢复默认", Vector2.zero, new Vector2(145, 44), defaults); Place(reset.GetComponent<RectTransform>(), 548, 613, 145, 44); FloatButton(reset, 17);
            var takeback = UI.Button(panel, "TakeBack", "取回已保存值", Vector2.zero, new Vector2(170, 44), reload); Place(takeback.GetComponent<RectTransform>(), 710, 613, 170, 44); FloatButton(takeback, 17);
        }
        void DataLink(RectTransform content, string name, string label, string description, float y, Action clicked)
        {
            var button = UI.Button(content, name, label, Vector2.zero, new Vector2(570, 44), clicked); FormAt(button.GetComponent<RectTransform>(), 0, y, 570, 44); FloatButton(button, 21);
            button.GetComponentInChildren<TMP_Text>(true).alignment = TextAlignmentOptions.Left;
            var detail = UI.Text(content, name + " detail", description, Vector2.zero, new Vector2(570, 26), 14, TextAlignmentOptions.Left);
            FormAt(detail.rectTransform, 8, y + 39, 570, 26); StyleInk(detail, 14, .7f);
        }

        void BuildCardSurface()
        {
            BuildFloatingPage(cardPanel, "人物设定", 3, cardNote);
            var content = MaintenanceForm(cardPanel, "Character form", 440);
            Place(content.parent.GetComponent<RectTransform>(),548,314,620,286);
            StyleCardField(cardName, content, "名字", 0, 44);
            StyleCardField(cardRelationship, content, "对你的称呼\n与关系基调", 69, 86);
            StyleCardField(cardPersona, content, "性格与说话方式", 181, 210);
            var reload = cardPanel.Find("Reload").GetComponent<Button>(); reload.GetComponentInChildren<TMP_Text>(true).text = "取回文件中版本";
            reload.onClick.RemoveAllListeners(); reload.onClick.AddListener(() => ConfirmAction("取回文件中已保存的人物设定？本页未保存的文字将被替换。", () => _ = ReloadCardAsync()));
            Place(reload.GetComponent<RectTransform>(), 548, 613, 235, 44); FloatButton(reload, 17);
            var previous = UI.Button(cardPanel, "PreviousCard", "上一版与原件…", Vector2.zero, new Vector2(200, 44), () => OpenSafeMaintenance(PlayerMaintenance.Page.Health));
            Place(previous.GetComponent<RectTransform>(), 792, 613, 200, 44); FloatButton(previous, 16);
            var save = cardPanel.Find("Save").GetComponent<Button>(); Place(save.GetComponent<RectTransform>(), 1010, 613, 160, 44); FloatButton(save, 20);
        }
        void StyleCardField(TMP_InputField field, RectTransform content, string label, float y, float height)
        {
            var heading = cardPanel.Find(field.name + "Label").GetComponent<TMP_Text>(); heading.text = label;
            MoveForm(heading.rectTransform, content, 0, y+6, 155, 68); StyleInk(heading, 18, .8f);heading.alignment=TextAlignmentOptions.TopLeft;
            MoveForm(field.GetComponent<RectTransform>(), content, 166, y, 414, height);
            field.GetComponent<Image>().color = Color.clear;
            field.fontAsset = CompanionVisualFoundation.Font; field.textComponent.font = CompanionVisualFoundation.Font;
            field.textComponent.fontSize = 21; field.textComponent.color = CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Ink);
            field.onFocusSelectAll = false; field.customCaretColor = true; field.caretColor = Color.white;
            field.selectionColor = new Color(1, .85f, .9f, .25f); field.restoreOriginalTextOnEscape = false;
            UI.Stretch(field.textViewport, 4, 4, 4, 4); UI.Stretch(field.textComponent.rectTransform);
            var line = UI.Panel(field.transform, "Field underline", Vector2.zero, new Vector2(414, 1), CompanionVisualFoundation.Color(root.VisualSettings.Value.Theme.Ink, .35f), false);
            FormAt(line.rectTransform, 0, height, 414, 1);
        }
        void BuildSpaceSurface()
        {
            BuildFloatingPage(spacePanel, "陪伴空间", 2, spaceNote);
            spacePreview = UI.Rect(spacePanel, "Current space preview", Vector2.zero, new Vector2(230, 130)).gameObject.AddComponent<RawImage>();
            Place(spacePreview.rectTransform, 548, 312, 230, 130); spacePreview.raycastTarget = false;
            activeSpaceTitle = UI.Text(spacePanel, "Active space title", "", Vector2.zero, new Vector2(350, 100), 26, TextAlignmentOptions.TopLeft);
            Place(activeSpaceTitle.rectTransform, 808, 314, 350, 100); StyleInk(activeSpaceTitle, 24);
            var caption = UI.Text(spacePanel, "Preview caption", "当前取景 · 人物与记忆在这里继续", Vector2.zero, new Vector2(610, 30), 15, TextAlignmentOptions.Left);
            Place(caption.rectTransform, 548, 452, 610, 30); StyleInk(caption, 15, .72f);
            var scroll = spaceContent.parent.GetComponent<ScrollRect>(); Place(scroll.GetComponent<RectTransform>(), 548, 491, 620, 112); scroll.GetComponent<Image>().color = Color.clear;
            var layout = spaceContent.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(8, 16, 3, 12); layout.spacing = 8;
            var add = spacePanel.Find("Add").GetComponent<Button>(); Place(add.GetComponent<RectTransform>(), 548, 613, 260, 44); FloatButton(add, 19);
            add.GetComponentInChildren<TMP_Text>(true).text = "+  添加陪伴空间";
            var list = UI.Button(spacePanel, "Manifest", "打开空间清单 ↗", Vector2.zero, new Vector2(250, 44), () => PlayerFiles.ReadText(root.Library.SpacesFile));
            Place(list.GetComponent<RectTransform>(), 912, 613, 250, 44); FloatButton(list, 17);
        }
        void RefreshSpacePreview()
        {
            if (activeSpaceTitle == null || visual?.Loader?.renderCamera == null) return;
            activeSpaceTitle.text = root.Library.Spaces.Spaces.Find(s => s.Id == ActiveSpaceId)?.Title ?? "当前空间";
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera = visual.Loader.renderCamera; var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(460, 260, 24);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                if (spacePreviewTexture != null) Destroy(spacePreviewTexture);
                spacePreviewTexture = new Texture2D(460, 260, TextureFormat.RGB24, false);
                spacePreviewTexture.ReadPixels(new Rect(0, 0, 460, 260), 0, 0); spacePreviewTexture.Apply(); spacePreview.texture = spacePreviewTexture;
            }
            catch (Exception error) { root.Logger?.Info("space preview unavailable type=" + error.GetType().Name); }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(target); }
        }

        bool HasUnsavedEdits()
        {
            var saved = root.Config.Configuration;
            if (panelName == "Voice" && settingsDraft != null)
                return settingsDraft.BgmVolume != saved.BgmVolume || settingsDraft.BgmMuted != saved.BgmMuted || settingsDraft.VoiceVolume != saved.VoiceVolume || settingsDraft.VoiceEnabled != saved.VoiceEnabled || settingsDraft.VoiceLanguage != saved.VoiceLanguage;
            if (panelName == "Display" && displayDraft != null)
            {
                var value = root.Display.Value;
                return displayDraft.Exposure != value.Exposure || displayDraft.HighlightCompression != value.HighlightCompression || displayDraft.DiaryFontSize != value.DiaryFontSize || displayDraft.DiaryLineSpacing != value.DiaryLineSpacing || settingsDraft.SubtitleFontSize != saved.SubtitleFontSize || motionDraft != root.VisualSettings.Value.ReduceMotion;
            }
            if (panelName == "CharacterCard")
                return cardName.text != root.Config.Identity.DisplayName || cardPersona.text != root.Config.Identity.Persona || cardRelationship.text != root.Config.Identity.Relationship;
            return false;
        }
        bool TryNavigate(Action next)
        {
            if (!maintenanceUiBuilt || navigationApproved) return true;
            if (ConfirmationVisible) return false;
            if(hubVolumePending)_ = FlushHubVolumeAsync();
            if(hubSaving){CurrentEditNote("正在保存音乐音量，请稍等再离开。");return false;}
            if (EditorBusy) { CurrentEditNote("正在完成刚才的操作，请稍等。完成后再离开。"); return false; }
            if (!HasUnsavedEdits()) return true;
            ConfirmAction("本页还有未保存的修改。放弃后会恢复已保存的内容与预览。", () =>
            { navigationApproved = true; try { next(); } finally { navigationApproved = false; } }, "放弃修改并返回");
            return false;
        }
        void RequestNavigation(Action next) { if (TryNavigate(next)) next(); }
        void CurrentEditNote(string text)
        { var note = panelName == "Voice" ? voiceNote : panelName == "Display" ? displayNote : panelName == "CharacterCard" ? cardNote : panelName == "Spaces" ? spaceNote : panelName == "Music" ? musicNote : settingsNote; if (note != null) note.text = text; }
        void BuildEditPrompt()
        {
            editPrompt = UI.Panel(canvas.transform, "Edit confirmation", Vector2.zero, new Vector2(1280, 720), new Color(.02f, .03f, .07f, .82f)).rectTransform;
            UI.Stretch(editPrompt);
            var message = UI.Text(editPrompt, "Message", "", Vector2.zero, new Vector2(690, 120), 24); StyleInk(message, 24); message.overflowMode = TextOverflowModes.Overflow;
            var cancel = UI.Button(editPrompt, "Cancel", "继续编辑", new Vector2(-170, -130), new Vector2(300, 48), CancelConfirmation); FloatButton(cancel, 20);
            var confirm = UI.Button(editPrompt, "Confirm", "确认", new Vector2(170, -130), new Vector2(300, 48), () => { var action = promptAction; CancelConfirmation(); action?.Invoke(); }); FloatButton(confirm, 20);
            editPrompt.gameObject.SetActive(false);
        }
        void ConfirmAction(string message, Action action, string confirmLabel = "确认")
        {
            if (ConfirmationVisible || EditorBusy) return;
            promptAction = action; promptPreviousFocus = EventSystem.current?.currentSelectedGameObject;
            editPrompt.Find("Message").GetComponent<TMP_Text>().text = message;
            var confirm = editPrompt.Find("Confirm").GetComponent<Button>(); confirm.GetComponentInChildren<TMP_Text>(true).text = confirmLabel;
            editPrompt.Find("Cancel").GetComponentInChildren<TMP_Text>(true).text = confirmLabel == "放弃修改并返回" ? "继续编辑" : "取消";
            editPrompt.SetAsLastSibling(); editPrompt.gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(editPrompt.Find("Cancel").gameObject);
        }
        void CancelConfirmation()
        { if (editPrompt == null) return; editPrompt.gameObject.SetActive(false); promptAction = null; EventSystem.current?.SetSelectedGameObject(promptPreviousFocus != null && promptPreviousFocus.activeInHierarchy ? promptPreviousFocus : null); }
        void PreviewMotion(bool value)
        { motionDraft = value; if (WindowView != null) WindowView.ReduceMotion = value; RefreshReducedMotionLabel(); }
        void ResetSoundDraft()
        {
            if (EditorBusy || settingsDraft == null) return;
            var defaults = new AppConfiguration(); settingsDraft.BgmVolume = defaults.BgmVolume; settingsDraft.BgmMuted = defaults.BgmMuted;
            settingsDraft.VoiceVolume = defaults.VoiceVolume; settingsDraft.VoiceEnabled = defaults.VoiceEnabled; settingsDraft.VoiceLanguage = defaults.VoiceLanguage;
            RefreshSoundSliders(); voiceNote.text = "已预览默认值，点击保存后保留。";
        }
        void ReloadSoundDraft()
        { if (EditorBusy) return; settingsDraft = root.Config.Configuration.Copy(); RefreshSoundSliders(); voiceNote.text = "已取回已保存的声音设置。"; }
        void RefreshSoundSliders()
        {
            voicePanel.GetComponentsInChildren<Slider>(true).Single(s => s.name == "VoiceVolume").SetValueWithoutNotify(settingsDraft.VoiceVolume);
            voicePanel.GetComponentsInChildren<Slider>(true).Single(s => s.name == "Volume").SetValueWithoutNotify(settingsDraft.BgmVolume);
            PreviewSettings(); UpdateVoiceDraft();
        }
        void ResetDisplayDraft()
        { if (EditorBusy || displayDraft == null) return; displayDraft = new CompanionDisplaySettings(); settingsDraft.SubtitleFontSize = new AppConfiguration().SubtitleFontSize; PreviewMotion(false); RefreshDisplaySliders(); displayNote.text = "已预览默认值，点击保存后保留。"; }
        void ReloadDisplayDraft()
        { if (EditorBusy) return; displayDraft = root.Display.Value.Copy(); settingsDraft = root.Config.Configuration.Copy(); PreviewMotion(root.VisualSettings.Value.ReduceMotion); RefreshDisplaySliders(); displayNote.text = "已取回已保存的画面与阅读设置。"; }
        async Task SaveDisplayDraftAsync()
        {
            if (EditorBusy || displayDraft == null || panelCancellation == null) return;
            displaySaving = true; var ct = panelCancellation.Token;
            var oldConfig = root.Config.Configuration.Copy(); var oldDisplay = root.Display.Value.Copy(); var oldVisual = root.VisualSettings.Value;
            bool configSaved = false, displaySaved = false;
            displayNote.text = "正在保存画面与阅读设置…";
            try
            {
                displayDraft.Validate();
                var next = oldConfig.Copy(); next.SubtitleFontSize = settingsDraft.SubtitleFontSize;
                var visualDraft = Newtonsoft.Json.JsonConvert.DeserializeObject<CompanionVisualSettings>(Newtonsoft.Json.JsonConvert.SerializeObject(oldVisual));
                visualDraft.ReduceMotion = motionDraft ?? oldVisual.ReduceMotion; visualDraft.Validate();
                await root.Config.SaveAsync(next, ct); configSaved = true;
                root.Display.Save(displayDraft, ct); displaySaved = true;
                root.VisualSettings.Save(visualDraft, ct);
                displayNote.text = "已保存。画面与字幕立即生效，日记再次打开时应用阅读设置。";
            }
            catch (Exception error)
            {
                root.Logger?.Info("reading settings save failed type=" + error.GetType().Name);
                bool rollback = true;
                try { if (displaySaved) root.Display.Save(oldDisplay, root.ApplicationToken); if (configSaved) await root.Config.SaveAsync(oldConfig, root.ApplicationToken); }
                catch (Exception restoreError) { rollback = false; root.Logger?.Info("reading settings rollback failed type=" + restoreError.GetType().Name); }
                if (this != null) displayNote.text = rollback ? "暂时没能保存，已保存设置保持原样；本页草稿仍在，可以重试或取回。" : "部分设置已保存，请取回已保存值核对，再尝试保存。";
            }
            finally { displaySaving = false; }
        }
        public bool HandleWindowClose()
        { if(leaving)return false;RequestNavigation(()=>_ = LeaveAsync(true));return true; }
        void UpdateMaintenanceUI()
        {
            if (!maintenanceUiBuilt) return;
            fallbackReadingShade.gameObject.SetActive(panelName!=null&&WindowView!=null&&!WindowView.HasWindow);
            fallbackReadingShade.color=visual?.ActiveSpaceId=="mika-seaside"?new Color(.02f,.03f,.07f,.14f):new Color(.02f,.03f,.07f,.62f);
            // Unity's keyboard submit/directional navigation stays intact; Tab follows page order.
            if (panelName != null && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                Transform scope = ConfirmationVisible ? editPrompt : transitionPanel;
                if (scope == null) return;
                var controls = scope.GetComponentsInChildren<Selectable>().Where(s => s.IsActive() && s.IsInteractable()).ToArray();
                var selected = EventSystem.current?.currentSelectedGameObject;
                int index = Array.FindIndex(controls, s => s.gameObject == selected);
                if (controls.Length > 0)
                {
                    var target = controls[(index + (Keyboard.current.shiftKey.isPressed ? -1 : 1) + controls.Length) % controls.Length];
                    EventSystem.current?.SetSelectedGameObject(target.gameObject);
                    RevealFormControl(target.GetComponent<RectTransform>());
                }
            }
        }
        void RevealFormControl(RectTransform target)
        {
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
            float delta = bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y : bounds.min.y < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bounds.min.y : 0;
            if (delta != 0) { scroll.StopMovement(); scroll.content.anchoredPosition += new Vector2(0, delta); }
        }
    }
}
