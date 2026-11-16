using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        private RectTransform historyPanel, settingsPanel, memoryPanel, historyContent, memoryContent;
        private ScrollRect historyScroll;
        private TMP_Text historyNote, settingsNote, memoryNote, volumeLabel, sizeLabel;
        private Button olderButton, newerButton, muteButton;
        private CancellationTokenSource panelCancellation;
        private HistoryPage historyPage;
        private HistoryCursor historyCursor;
        private readonly List<HistoryCursor> historyBack = new List<HistoryCursor>();
        private AppConfiguration settingsDraft;
        private Bgm.BgmManager music;
        private Bgm.BgmFilePlayer libraryMusic;
        private bool historyLoading, memoryLoading;
        public bool HasBgm => root?.Library?.Music.Tracks.Count > 0 && music != null && music.HasPlaylist;
        public int VisibleHistoryCount => historyPage?.Turns.Count ?? 0;

        partial void BuildAdditionalPanels()
        {
            subtitle.fontSize = root.Config.Configuration.SubtitleFontSize;
            music = gameObject.AddComponent<Bgm.BgmManager>();
            music.Configure(Array.Empty<AudioClip>(), root.Config.Configuration.BgmVolume, root.Config.Configuration.BgmMuted);
            libraryMusic = gameObject.AddComponent<Bgm.BgmFilePlayer>();
            _ = libraryMusic.InitializeAsync(root.Library, music, root.Logger, destroyCancellationToken);
            historyPanel = BuildPanel("MomoTalk", "MomoTalk");
            historyNote = UI.Text(historyPanel, "Note", "", new Vector2(0, -260), new Vector2(800, 30), 17);
            historyContent = BuildScroll(historyPanel, "History", new Vector2(0, -8), new Vector2(840, 420), out historyScroll);
            olderButton = UI.Button(historyPanel, "Older", "更早记录", new Vector2(-220, 238), new Vector2(150, 38), () => _ = OlderAsync());
            newerButton = UI.Button(historyPanel, "Newer", "较新记录", new Vector2(220, 238), new Vector2(150, 38), () => _ = NewerAsync());
            settingsPanel = BuildPanel("Settings", "设置");
            volumeLabel = UI.Text(settingsPanel, "BgmVolume", "", new Vector2(0, 200), new Vector2(650, 35), 22);
            AddSlider(settingsPanel, "Volume", new Vector2(0, 158), 0, 1, value => { if (settingsDraft == null) return; settingsDraft.BgmVolume = value; PreviewSettings(); });
            muteButton = UI.Button(settingsPanel, "Mute", "", new Vector2(0, 102), new Vector2(250, 42), () => { settingsDraft.BgmMuted = !settingsDraft.BgmMuted; PreviewSettings(); });
            sizeLabel = UI.Text(settingsPanel, "TextSize", "", new Vector2(0, 49), new Vector2(650, 35), 22);
            AddSlider(settingsPanel, "SubtitleSize", new Vector2(0, 7), 20, 36, value => { if (settingsDraft == null) return; settingsDraft.SubtitleFontSize = Mathf.Round(value); PreviewSettings(); });
            UI.Button(settingsPanel, "Memories", "记住的内容", new Vector2(-155, -62), new Vector2(250, 44), () => _ = OpenMemoryAsync());
            UI.Button(settingsPanel, "Save", "保存设置", new Vector2(155, -62), new Vector2(250, 44), () => _ = SaveSettingsAsync());
            settingsNote = UI.Text(settingsPanel, "Note", "", new Vector2(0, -240), new Vector2(800, 50), 17);
            UI.Button(settingsPanel, "Maintenance", "资料备份 / 联网配置", new Vector2(0, -190), new Vector2(450, 42), PlayerMaintenance.OpenFromMode);
            memoryPanel = BuildPanel("Memories", "记住的内容");
            memoryContent = BuildScroll(memoryPanel, "Memory", new Vector2(0, 10), new Vector2(840, 425), out _);
            memoryNote = UI.Text(memoryPanel, "Note", "移除后不再用于回忆，原始聊天记录仍保留。", new Vector2(0, -255), new Vector2(800, 36), 17);
            openHistory = () => _ = OpenHistoryAsync(); openSettings = OpenSettings;
            BuildLibraryPanels();
            BuildVoicePanel();
            BuildDisplayPanel();
            UI.Button(settingsPanel, "Display", "画面与阅读", new Vector2(165, -126), new Vector2(290, 42), OpenDisplaySettings);
            historyButton.interactable = settingsButton.interactable = true;
            // Use the same navigation route after the scene-owned music fades.
            var home = menuPanel.GetComponentsInChildren<Button>().Single(b => b.name == "ReturnHome"); home.onClick.RemoveAllListeners();
            home.onClick.AddListener(() => _ = LeaveAsync(false));
            var exit = menuPanel.GetComponentsInChildren<Button>().Single(b => b.name == "Exit"); exit.onClick.RemoveAllListeners(); exit.onClick.AddListener(() => _ = LeaveAsync(true));
        }
        private RectTransform BuildPanel(string name, string caption)
        {
            var panel = UI.Panel(canvas.transform, name, Vector2.zero, new Vector2(950, 620)).rectTransform;
            UI.Text(panel, "Title", caption, new Vector2(0, 280), new Vector2(650, 40), 27);
            UI.Button(panel, "Close", "返回", new Vector2(393, 280), new Vector2(100, 38), Back);
            panel.gameObject.SetActive(false); return panel;
        }
        private static RectTransform BuildScroll(Transform parent, string name, Vector2 position, Vector2 size, out ScrollRect scroll)
        {
            var view = UI.Panel(parent, name, position, size, new Color(.035f,.05f,.09f,.65f)).rectTransform;
            view.gameObject.AddComponent<RectMask2D>(); scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var content = UI.Rect(view, "Content", Vector2.zero, new Vector2(size.x - 20, 0));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(-20, 0);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(14, 14, 14, 14); layout.spacing = 12;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = view; scroll.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }
        private CancellationToken ShowPanel(string name, RectTransform panel)
        {
            CloseAdditionalPanels(); UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null); if (inputOpen) CloseInput(); menuOpen = false; menuPanel.gameObject.SetActive(false);
            if(WindowView!=null)WindowView.GoWindow();
            panelName = name; panel.gameObject.SetActive(true);
            BeginPanelTransition(panel);
            panelCancellation = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken, root.Controller.ActiveModeToken, destroyCancellationToken);
            ResolveVisual(); return panelCancellation.Token;
        }
        partial void CloseAdditionalPanels()
        {
            if(voicePlayer?.IsReplaying==true)voicePlayer.StopSpeech();
            panelCancellation?.Cancel(); panelCancellation?.Dispose(); panelCancellation = null;
            historyLoading = memoryLoading = false;
            if (historyPanel != null) RetirePanel(historyPanel);
            if (settingsPanel != null) RetirePanel(settingsPanel);
            if (memoryPanel != null) RetirePanel(memoryPanel);
            if (voicePanel != null) RetirePanel(voicePanel);
            if (displayPanel != null) RetirePanel(displayPanel);
            if (displayDraft != null) { ApplyDisplay(root.Display.Value); displayDraft = null; }
            if (settingsDraft != null && music != null)
            { music.SetVolume(root.Config.Configuration.BgmVolume, root.Config.Configuration.BgmMuted); subtitle.fontSize = root.Config.Configuration.SubtitleFontSize; }
            settingsDraft = null;
            motionDraft=null;if(WindowView!=null)WindowView.ReduceMotion=root.VisualSettings.Value.ReduceMotion;
            voicePlayer?.SetVolume(root.Config.Configuration.VoiceVolume);
            CloseLibraryPanels();
        }
        partial void ReleaseAdditionalPanels()
        { panelCancellation?.Cancel(); panelCancellation?.Dispose(); panelCancellation = null; }
        public async Task OpenHistoryAsync()
        {
            if(!TryNavigate(()=>_ = OpenHistoryAsync()))return;
            var ct = ShowPanel("MomoTalk", historyPanel); historyCursor = null; historyBack.Clear(); await LoadHistoryAsync(ct);
        }
        private async Task OlderAsync()
        {
            if (historyLoading || historyPage?.Next == null || panelCancellation == null) return;
            historyBack.Add(historyCursor); historyCursor = historyPage.Next; await LoadHistoryAsync(panelCancellation.Token);
        }
        private async Task NewerAsync()
        {
            if (historyLoading || historyBack.Count == 0 || panelCancellation == null) return;
            historyCursor = historyBack[historyBack.Count - 1]; historyBack.RemoveAt(historyBack.Count - 1); await LoadHistoryAsync(panelCancellation.Token);
        }
        private async Task LoadHistoryAsync(CancellationToken ct)
        {
            historyLoading = true; olderButton.interactable = newerButton.interactable = false;
            try
            {
                var page = await root.History.ReadPageAsync(historyCursor, ct);
                ct.ThrowIfCancellationRequested(); if (this == null || panelName != "MomoTalk") return;
                historyPage = page; ClearRows(historyContent);
                foreach (var turn in page.Turns.Reverse())
                {
                    AddHistoryText("老师", turn.UserText, turn.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm"), turn.Status == "completed" ? "" : "未完成");
                    if (!string.IsNullOrEmpty(turn.AssistantText)) AddHistoryText(root.Character.Identity.DisplayName, turn.AssistantText, "", "");
                    float width=Mathf.Max(200,historyScroll.viewport.rect.width-40);
                    var row = UI.Rect(historyContent, "Round controls", Vector2.zero, new Vector2(width, 34)); row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
                    var forget = UI.Button(row, "Forget", turn.ExcludedFromMemory ? "本轮不参与回忆" : "不记住本轮", new Vector2(width/2-105, 0), new Vector2(210, 32), () => _ = ForgetRoundAsync(turn.RequestId));FloatButton(forget,14);
                    forget.interactable = !turn.ExcludedFromMemory;
                    if (!string.IsNullOrEmpty(turn.AssistantText))
                    {
                        var audio = await root.AudioCache.ReadPlayableAsync(turn.AssistantMessageId, ct);
                        ct.ThrowIfCancellationRequested(); if (this == null || panelName != "MomoTalk") return;
                        var replay = UI.Button(row, "Replay", audio.Count > 0 ? (audio[0].ProfileId.StartsWith("ja:", StringComparison.Ordinal) ? "▷ 日语复听" : "▷ 中文复听") : "无缓存", new Vector2(-width/2+70, 0), new Vector2(140, 32), () => _ = ReplayHistoryAsync(turn.AssistantMessageId, ct));FloatButton(replay,15);
                        replay.interactable = audio.Count > 0;
                    }
                }
                historyNote.text = page.Turns.Count == 0 ? "还没有聊天记录。" : "";
                Canvas.ForceUpdateCanvases(); historyScroll.verticalNormalizedPosition = 0;
                olderButton.interactable = page.Next != null; newerButton.interactable = historyBack.Count > 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("history read failed", error); if (this != null && !ct.IsCancellationRequested) historyNote.text = "记录暂时没能打开，请再试一次。"; }
            finally { if (!ct.IsCancellationRequested) historyLoading = false; }
        }
        private void AddHistoryText(string speaker, string body, string time, string status)
        {
            float width=Mathf.Max(200,historyScroll.viewport.rect.width-50),indent=speaker=="老师"?24:0;
            var row=UI.Rect(historyContent,speaker,Vector2.zero,new Vector2(width,60));
            var label=UI.Text(row,"Message",body,Vector2.zero,new Vector2(width-indent,60),22,TextAlignmentOptions.TopLeft);StyleInk(label,22);label.overflowMode=TextOverflowModes.Overflow;
            float height=label.GetPreferredValues(body,width-indent,0).y+45;row.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(0,1);label.rectTransform.pivot=new Vector2(0,1);label.rectTransform.anchoredPosition=new Vector2(indent,-32);label.rectTransform.sizeDelta=new Vector2(width-indent,height-32);
            var heading=UI.Text(row,"Message heading",speaker+(time.Length>0?" · "+time:"")+(status.Length>0?" · "+status:""),Vector2.zero,new Vector2(width,30),15,TextAlignmentOptions.Left);StyleInk(heading,15,.72f);
            heading.rectTransform.anchorMin=heading.rectTransform.anchorMax=new Vector2(0,1);heading.rectTransform.pivot=new Vector2(0,1);heading.rectTransform.anchoredPosition=new Vector2(indent,0);
        }
        private async Task ForgetRoundAsync(string requestId)
        {
            if (panelCancellation == null || historyLoading) return; var ct = panelCancellation.Token; historyLoading = true;
            try { await root.Character.ForgetTurnAsync(requestId, ct); ct.ThrowIfCancellationRequested(); await LoadHistoryAsync(ct); ct.ThrowIfCancellationRequested(); historyNote.text = "本轮不再用于回忆，历史记录仍保留。"; }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("history forget failed", error); if (this != null && !ct.IsCancellationRequested) historyNote.text = "刚才没能保存，请再试一次。"; }
            finally { if (!ct.IsCancellationRequested) historyLoading = false; }
        }
        public void OpenSettings() => OpenVoiceSettings();
        private void PreviewSettings()
        {
            music.SetVolume(settingsDraft.BgmVolume, settingsDraft.BgmMuted); subtitle.fontSize = settingsDraft.SubtitleFontSize;
            volumeLabel.text = "背景音乐音量  " + Mathf.RoundToInt(settingsDraft.BgmVolume * 100) + "%";
            sizeLabel.text = "字幕文字大小  " + settingsDraft.SubtitleFontSize;
            muteButton.GetComponentInChildren<TMP_Text>().text = settingsDraft.BgmMuted ? "音乐：静音" : "音乐：开启";
        }
        private async Task SaveSettingsAsync()
        {
            if (settingsDraft == null || panelCancellation == null) return; var ct = panelCancellation.Token;
            try { await root.Config.SaveAsync(settingsDraft.Copy(), ct); if (this != null && !ct.IsCancellationRequested) settingsNote.text = "设置已保存。"; }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("settings save failed", error); if (this != null && !ct.IsCancellationRequested) settingsNote.text = "设置没能保存，请再试一次。"; }
        }
        private async Task OpenMemoryAsync()
        {
            if(!TryNavigate(()=>_ = OpenMemoryAsync()))return;
            var ct = ShowPanel("Memories", memoryPanel); await LoadMemoryAsync(ct);
        }
        private async Task LoadMemoryAsync(CancellationToken ct)
        {
            memoryLoading = true;
            try
            {
                var records = new List<MemoryRecord>();
                foreach (string kind in new[] {"fact", "memory", "loop"}) records.AddRange(await ((ISharedMemoryService)root.Memory).ListActiveAsync(kind, ct));
                ct.ThrowIfCancellationRequested(); if (this == null || panelName != "Memories") return; ClearRows(memoryContent);
                foreach (var record in records)
                {
                    string content = (record.Kind == "fact" ? record.Key + "：" : "") + record.Content;
                    var row = UI.Rect(memoryContent, "Memory", Vector2.zero, new Vector2(580, 90));
                    var text = UI.Text(row, "Content", content, new Vector2(-70, 0), new Vector2(405, 80), 21, TextAlignmentOptions.MidlineLeft);
                    float height = Mathf.Max(90, text.GetPreferredValues(content, 405, 0).y + 16);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
                    text.rectTransform.sizeDelta = new Vector2(405, height - 16); text.overflowMode = TextOverflowModes.Overflow;
                    var forget=UI.Button(row,"Forget","移除",new Vector2(235,0),new Vector2(105,44),()=>ConfirmAction("移除这条记忆？\n"+(content.Length>100?content.Substring(0,100)+"…":content)+"\n原始聊天仍保留，此条不再参与回忆。",()=>_ = ForgetRecordAsync(record.Kind,record.Id)));FloatButton(forget,17);StyleInk(text,20);
                }
                memoryNote.text = records.Count == 0 ? "还没有记住的内容。" : "移除后不再用于回忆，原始聊天记录仍保留。";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("memory list failed", error); if (this != null && !ct.IsCancellationRequested) memoryNote.text = "暂时没能打开，请再试一次。"; }
            finally { if (!ct.IsCancellationRequested) memoryLoading = false; }
        }
        private async Task ForgetRecordAsync(string kind, string id)
        {
            if (memoryLoading || panelCancellation == null) return; var ct = panelCancellation.Token; memoryLoading = true;
            try { await root.Character.ForgetMemoryAsync(kind, id, ct); ct.ThrowIfCancellationRequested(); await LoadMemoryAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("memory forget failed", error); if (this != null && !ct.IsCancellationRequested) memoryNote.text = "刚才没能保存，请再试一次。"; }
            finally { if (!ct.IsCancellationRequested) memoryLoading = false; }
        }
        private static void ClearRows(Transform content)
        { foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        private static void AddSlider(Transform parent, string name, Vector2 position, float min, float max, Action<float> changed)
        {
            var track = UI.Panel(parent, name, position, new Vector2(550, 22), new Color(.16f,.22f,.34f));
            var handle = UI.Panel(track.transform, "Handle", Vector2.zero, new Vector2(22, 34), UI.Accent);
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.onValueChanged.AddListener(value => changed(value));
        }
        public Task LeaveForMaintenanceAsync() => LeaveAsync(false);
        private async Task LeaveAsync(bool quit)
        {
            if (leaving || root.Controller.IsSwitching) return;
            if(!TryNavigate(()=>_ = LeaveAsync(quit)))return;
            leaving = true;
            try
            {
                await FlushHubVolumeAsync();
                await StopDialogueAsync();
                libraryMusic.CancelPendingLoad();
                await music.FadeOutAsync(destroyCancellationToken);
                if (this == null) return; if (quit) root.RequestQuit(); else await root.Controller.SwitchModeAsync(GiftCompanion.Core.Character.CharacterMode.Home, destroyCancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("companion leave failed", error); if (this != null) { ShowFeedback("刚才没能离开，请再试一次。"); music.Resume(); if (music.Source?.clip == null && root.Library.Music.Tracks.Count > 0) _ = libraryMusic.InitializeAsync(root.Library, music, root.Logger, destroyCancellationToken); } }
            finally { if (this != null) leaving = false; }
        }
    }
}
