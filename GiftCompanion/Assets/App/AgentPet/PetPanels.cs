using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.AgentPet
{
    public sealed partial class PetScreen
    {
        private List<GameObject> chatParts;
        private RectTransform menu, settingsPanel, readerPanel;
        private TMP_InputField hotkeyInput;
        private TMP_Text settingsNote, reader;
        private HistoryCursor historyCursor;
        private bool reading, settingsBusy;
        private long panelGeneration;
        private void BuildMenu()
        {
            chatParts = new List<GameObject>(); foreach (Transform child in bubble) if (child.name != "Name" && child.name != "Menu") chatParts.Add(child.gameObject);
            menu = UI.Rect(bubble, "Pet Menu", Vector2.zero, new Vector2(310, 270));
            UI.Button(menu, "Chat", "继续说话", new Vector2(-74, 101), new Vector2(140, 35), OpenInput);
            UI.Button(menu, "Stop", "停止回复", new Vector2(74, 101), new Vector2(140, 35), () => { BackToChat(); StopCurrent(); });
            UI.Button(menu, "History", "MomoTalk", new Vector2(-74, 54), new Vector2(140, 35), () => _ = OpenHistoryAsync(true));
            UI.Button(menu, "Diary", "相处日记", new Vector2(74, 54), new Vector2(140, 35), () => _ = OpenDiaryAsync());
            UI.Button(menu, "Settings", "桌面设置", new Vector2(-74, 7), new Vector2(140, 35), OpenSettings);
            UI.Button(menu, "Tools", "桌面工具", new Vector2(74, 7), new Vector2(140, 35), () => OpenTools());
            UI.Button(menu, "Files", "打开资料文件夹", new Vector2(0, -40), new Vector2(288, 35), () => PlayerFiles.OpenFolder(root.Config.UserDataPath));
            UI.Button(menu, "ReturnHome", "返回 Home", new Vector2(-74, -87), new Vector2(140, 35), () => _ = LeaveAsync());
            UI.Button(menu, "Exit", "退出", new Vector2(74, -87), new Vector2(140, 35), () => root.RequestQuit());
            UI.Button(menu, "Close", "收起", new Vector2(0, -144), new Vector2(140, 34), Hide);
            settingsPanel = UI.Rect(bubble, "Pet Settings", Vector2.zero, new Vector2(310, 285));
            UI.Text(settingsPanel, "Label", "唤醒快捷键", new Vector2(0, 105), new Vector2(280, 25), 19);
            hotkeyInput = Field(settingsPanel, "Hotkey", new Vector2(0, 62), new Vector2(280, 36));
            UI.Button(settingsPanel, "Save", "保存快捷键", new Vector2(0, 14), new Vector2(280, 35), () => _ = SaveHotkeyAsync());
            UI.Button(settingsPanel, "Topmost", "切换置顶", new Vector2(-74, -30), new Vector2(138, 35), ToggleTopmost);
            UI.Button(settingsPanel, "Reset", "找回位置", new Vector2(74, -30), new Vector2(138, 35), () => { host.ResetPosition(); Reposition(); settingsNote.text = "位置已重置。"; });
            settingsNote = UI.Text(settingsPanel, "Note", "", new Vector2(0, -83), new Vector2(286, 62), 16, TextAlignmentOptions.TopLeft);
            UI.Button(settingsPanel, "Back", "返回", new Vector2(-74, -144), new Vector2(138, 34), OpenMenu);
            UI.Button(settingsPanel, "Guide", "资料与连接", new Vector2(74, -144), new Vector2(138, 34), PlayerMaintenance.OpenFromMode);
            readerPanel = UI.Rect(bubble, "Pet Reader", Vector2.zero, new Vector2(310, 285));
            reader = UI.Text(readerPanel, "Text", "", new Vector2(0, 15), new Vector2(290, 225), 18, TextAlignmentOptions.TopLeft); reader.overflowMode = TextOverflowModes.Page;
            UI.Button(readerPanel, "Previous", "‹", new Vector2(-105, -116), new Vector2(50, 32), () => reader.pageToDisplay = Mathf.Max(1, reader.pageToDisplay - 1));
            UI.Button(readerPanel, "Next", "›", new Vector2(-45, -116), new Vector2(50, 32), () => reader.pageToDisplay = Mathf.Min(reader.textInfo.pageCount, reader.pageToDisplay + 1));
            UI.Button(readerPanel, "More", "更早聊天", new Vector2(70, -116), new Vector2(130, 32), () => _ = OpenHistoryAsync(false));
            UI.Button(readerPanel, "Back", "返回", new Vector2(-74, -155), new Vector2(138, 30), OpenMenu);
            UI.Button(readerPanel, "Write", "写一篇回顾", new Vector2(74, -155), new Vector2(138, 30), () => _ = WriteDiaryAsync());
            ClosePetPanels();
        }
        private static TMP_InputField Field(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var background = UI.Panel(parent, name, position, size, new Color(.13f, .18f, .27f));
            var viewport = UI.Rect(background.transform, "Viewport", Vector2.zero, size - new Vector2(12, 6)); viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var value = UI.Text(viewport, "Value", "", Vector2.zero, size - new Vector2(16, 8), 18, TextAlignmentOptions.Left);
            var field = background.gameObject.AddComponent<TMP_InputField>(); field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)value; field.fontAsset = UI.Chinese; field.richText = false; field.characterLimit = 64; return field;
        }
        private void ClosePetPanels() { menu?.gameObject.SetActive(false); settingsPanel?.gameObject.SetActive(false); readerPanel?.gameObject.SetActive(false); CloseToolsPanel(); }
        partial void CloseToolsPanel();
        private void SelectPanel(string name)
        { panelGeneration++; EndEditing(); ClosePetPanels(); panel = name; open = true; foreach (var part in chatParts) part.SetActive(name == "chat"); }
        private void BackToChat() { SelectPanel("chat"); showUntil = Time.unscaledTime + root.PetSettings.Value.ReplySeconds; }
        public void OpenMenu() { if (!Ready) return; SelectPanel("menu"); menu.gameObject.SetActive(true); }
        public void OpenSettings()
        {
            SelectPanel("settings"); settingsPanel.gameObject.SetActive(true); hotkeyInput.text = root.Config.Configuration.PetHotkey;
            settingsNote.text = host.HotkeyIssue ?? root.PetSettings.Issue ?? (root.PetSettings.Value.Topmost ? "当前置顶。" : "当前不置顶。"); host.SetInputFocus(true);
        }
        private async Task SaveHotkeyAsync()
        {
            if (settingsBusy) return; settingsBusy = true;
            try { bool ok = await host.ChangeHotkeyAsync(hotkeyInput.text, destroyCancellationToken); if (this != null) settingsNote.text = ok ? "已保存，可以用新快捷键叫我。" : host.HotkeyIssue; }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("pet hotkey save failed", error); if (this != null) settingsNote.text = "没能保存，原设置仍保留。"; }
            finally { settingsBusy = false; }
        }
        private void ToggleTopmost()
        {
            try { var next = root.PetSettings.Value.Copy(); next.Topmost = !next.Topmost; root.PetSettings.Save(next, root.ApplicationToken); host.ApplyTopmost(next.Topmost); settingsNote.text = next.Topmost ? "已置顶。" : "已取消置顶。"; }
            catch (Exception error) { root.Logger.Error("pet settings save failed", error); settingsNote.text = "没能保存，原设置仍保留。"; }
        }
        private void ShowReader(string value) { SelectPanel("reader"); readerPanel.gameObject.SetActive(true); reader.text = value; reader.pageToDisplay = 1; }
        private async Task OpenHistoryAsync(bool reset)
        {
            if (reading) return; reading = true; long requested = panelGeneration;
            try
            {
                if (reset) historyCursor = null; else if (historyCursor == null) return;
                var page = await root.History.ReadPageAsync(historyCursor, destroyCancellationToken, 10);
                if (this == null || requested != panelGeneration) return; historyCursor = page.Next; var value = new StringBuilder("MomoTalk\n\n");
                foreach (var item in page.Turns) value.AppendLine("老师：" + item.UserText).AppendLine(root.Character.Identity.DisplayName + "：" + item.AssistantText).AppendLine();
                if (page.Turns.Count == 0) value.Append("我们还没有留下聊天。"); ShowReader(value.ToString());
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("pet history failed", error); if (this != null && requested == panelGeneration) ShowReader("回忆暂时没能打开，请再试一次。"); }
            finally { reading = false; }
        }
        private async Task OpenDiaryAsync()
        {
            if (reading) return; reading = true; long requested = panelGeneration;
            try { var diaries = await root.Diary.ListAsync(destroyCancellationToken); if (this != null && requested == panelGeneration) ShowReader(diaries.Count == 0 ? "老师想回望时，可以写下从相识到现在的回顾。" : diaries[0].Body); }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("pet diary failed", error); }
            finally { reading = false; }
        }
        private async Task WriteDiaryAsync()
        {
            if (reading) return; reading = true; ShowReader("让我把我们的回忆整理一下……"); long requested = panelGeneration;
            try { var entry = await root.PlayerWorkAsync(ct => root.Diary.GenerateAsync(null, ct), destroyCancellationToken); if (this != null && requested == panelGeneration) ShowReader(entry.Body); }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("pet diary generation failed", error); if (this != null && requested == panelGeneration) ShowReader("这次没能整理完成，老师可以稍后再试。"); }
            finally { reading = false; }
        }
        private async Task LeaveAsync()
        {
            if (leaving) return; leaving = true;
            try { await root.Controller.SwitchModeAsync(Core.Character.CharacterMode.Home, root.ApplicationToken); }
            catch (Exception error) { root.Logger.Error("pet leave failed", error); if (this != null) { leaving = false; Say("暂时没能回去，请再试一次。"); } }
        }
    }
}
