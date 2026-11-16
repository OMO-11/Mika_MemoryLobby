using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Agent;
using GiftCompanion.SharedUI;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.AgentPet
{
    public sealed partial class PetScreen
    {
        private RectTransform toolsPanel, permissionPanel;
        private TMP_Text toolNote, permissionText;
        private TMP_InputField appName;
        private Button[] toolToggles;
        private int toolPage;
        private string selectedApplication;
        private TaskCompletionSource<bool> permission;
        private void BuildToolsPanel()
        {
            toolsPanel = UI.Rect(bubble, "Desktop Tools", Vector2.zero, new Vector2(310, 285));
            toolToggles = new Button[4];
            for (int i = 0; i < 4; i++) { int row = i; toolToggles[i] = UI.Button(toolsPanel, "Tool" + i, "", new Vector2(0, 109 - i * 33), new Vector2(290, 29), () => ToggleTool(toolPage * 4 + row)); }
            UI.Button(toolsPanel, "Previous", "‹", new Vector2(-118, -23), new Vector2(45, 28), () => { toolPage = (toolPage + 3) % 4; RefreshTools(); });
            UI.Button(toolsPanel, "Next", "›", new Vector2(118, -23), new Vector2(45, 28), () => { toolPage = (toolPage + 1) % 4; RefreshTools(); });
            UI.Button(toolsPanel, "Applications", "登记应用", new Vector2(-73, -61), new Vector2(140, 30), PickApplication);
            UI.Button(toolsPanel, "SearchRoots", "选择查找范围", new Vector2(73, -61), new Vector2(140, 30), PickSearchRoot);
            UI.Button(toolsPanel, "List", "清单 / 移除", new Vector2(-73, -98), new Vector2(140, 30), ShowToolLists);
            UI.Button(toolsPanel, "Audit", "操作记录", new Vector2(73, -98), new Vector2(140, 30), () => _ = ShowAuditAsync());
            toolNote = UI.Text(toolsPanel, "Note", "", new Vector2(0, -134), new Vector2(290, 34), 14);
            UI.Button(toolsPanel, "Back", "返回", new Vector2(-73, -165), new Vector2(140, 28), OpenMenu);
            UI.Button(toolsPanel, "Restore", "恢复工具设置", new Vector2(73, -165), new Vector2(140, 28), () => ToolChange(() => root.AgentSettings.Save(new AgentSettings(), root.ApplicationToken)));
            permissionPanel = UI.Rect(bubble, "Permission", Vector2.zero, new Vector2(310, 285));
            permissionText = UI.Text(permissionPanel, "Description", "", new Vector2(0, 20), new Vector2(290, 220), 18, TextAlignmentOptions.TopLeft); permissionText.overflowMode = TextOverflowModes.Page;
            UI.Button(permissionPanel, "Previous", "‹", new Vector2(-40, -107), new Vector2(50, 28), () => permissionText.pageToDisplay = Mathf.Max(1, permissionText.pageToDisplay - 1));
            UI.Button(permissionPanel, "Next", "›", new Vector2(40, -107), new Vector2(50, 28), () => permissionText.pageToDisplay = Mathf.Min(permissionText.textInfo.pageCount, permissionText.pageToDisplay + 1));
            UI.Button(permissionPanel, "Cancel", "取消", new Vector2(-74, -151), new Vector2(138, 34), () => AnswerPermission(false));
            UI.Button(permissionPanel, "Allow", "本次允许", new Vector2(74, -151), new Vector2(138, 34), () => AnswerPermission(true));
            toolsPanel.gameObject.SetActive(false); permissionPanel.gameObject.SetActive(false);
        }
        partial void OpenTools()
        {
            if (toolsPanel == null) BuildToolsPanel(); SelectPanel("tools"); toolsPanel.gameObject.SetActive(true); RefreshTools();
        }
        private void RefreshTools()
        {
            for (int i = 0; i < toolToggles.Length; i++)
            {
                int index = toolPage * 4 + i; toolToggles[i].gameObject.SetActive(index < AgentToolCatalog.Names.Length);
                if (index >= AgentToolCatalog.Names.Length) continue;
                string name = AgentToolCatalog.Names[index]; bool on = root.AgentSettings.Value.Enabled.TryGetValue(name, out bool enabled) && enabled;
                toolToggles[i].GetComponentInChildren<TMP_Text>().text = (on ? "● " : "○ ") + AgentToolCatalog.Label(name);
            }
            toolNote.text = root.AgentSettings.Issue ?? "仅本次开启操作后使用；敏感操作仍需确认。";
        }
        private void ToggleTool(int index)
        {
            if (index >= AgentToolCatalog.Names.Length) return;
            ToolChange(() => { var next = JsonConvert.DeserializeObject<AgentSettings>(JsonConvert.SerializeObject(root.AgentSettings.Value)); string name = AgentToolCatalog.Names[index]; next.Enabled[name] = !next.Enabled.TryGetValue(name, out bool on) || !on; root.AgentSettings.Save(next, root.ApplicationToken); });
        }
        private void ToolChange(Action change)
        { try { change(); if (panel == "tools") RefreshTools(); } catch (Exception error) { root.Logger.Error("tool settings update failed", error); ShowReader(error is ArgumentException ? error.Message : "没能保存，原设置仍然保留。"); } }
        private void PickSearchRoot() => ToolChange(() =>
        {
            string folder = PlayerFiles.SelectFolder("选择允许按文件名查找的文件夹"); if (folder == null) return;
            Tools.WindowsAgentPlatform.ValidateSearchRoot(folder);
            var next = JsonConvert.DeserializeObject<AgentSettings>(JsonConvert.SerializeObject(root.AgentSettings.Value));
            if (!next.SearchRoots.Contains(folder, StringComparer.OrdinalIgnoreCase)) next.SearchRoots.Add(folder); root.AgentSettings.Save(next, root.ApplicationToken);
        });
        private void PickApplication() => ToolChange(() =>
        {
            string selected = PlayerFiles.ApplicationFile(); if (selected == null) return;
            selectedApplication = Tools.WindowsAgentPlatform.ResolveShortcut(selected);
            if (!AgentPaths.IsApplication(selectedApplication)) throw new ArgumentException("请选择能直接打开的应用程序，不登记命令或脚本解释器。");
            ShowReader("给这个应用取个方便称呼的名字：\n" + Path.GetFileName(selectedApplication));
            if (appName == null)
            {
                appName = Field(readerPanel, "Application name", new Vector2(0, -45), new Vector2(280, 34));
                UI.Button(readerPanel, "SaveApplication", "保存应用", new Vector2(0, -87), new Vector2(150, 30), SaveApplication);
            }
            appName.gameObject.SetActive(true); readerPanel.Find("SaveApplication").gameObject.SetActive(true); appName.text = Path.GetFileNameWithoutExtension(selectedApplication); host.SetInputFocus(true); ime?.SetIMEEnabled(true); appName.ActivateInputField();
        });
        private void SaveApplication() => ToolChange(() =>
        {
            if (selectedApplication == null) return;
            var apps = root.AgentSettings.Applications.Where(a => !a.Name.Equals(appName.text.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            apps.Add(new KnownApplication { Name = appName.text.Trim(), Path = selectedApplication }); root.AgentSettings.SaveApplications(apps, root.ApplicationToken); selectedApplication = null; OpenTools();
        });
        private void ShowToolLists()
        {
            // A small, paged maintenance panel provides explicit removal buttons without JSON editing.
            if (listPanel != null) Destroy(listPanel.gameObject);
            SelectPanel("lists"); listPanel = UI.Rect(bubble, "Registered targets", Vector2.zero, new Vector2(310, 285)); listPage = 0; RefreshLists();
        }
        private RectTransform listPanel;
        private int listPage;
        private void RefreshLists()
        {
            foreach (Transform child in listPanel) Destroy(child.gameObject);
            var apps = root.AgentSettings.Applications; var roots = root.AgentSettings.Value.SearchRoots; int total = apps.Count + roots.Count;
            for (int i = 0; i < 4; i++)
            {
                int index = listPage * 4 + i; if (index >= total) break;
                bool app = index < apps.Count; string label = app ? apps[index].Name : Path.GetFileName(roots[index - apps.Count]);
                UI.Text(listPanel, "Name" + i, (app ? "应用：" : "目录：") + label, new Vector2(-32, 101 - i * 54), new Vector2(220, 45), 17);
                UI.Button(listPanel, "Remove" + i, "移除", new Vector2(115, 101 - i * 54), new Vector2(60, 32), () => ToolChange(() =>
                {
                    if (app) root.AgentSettings.SaveApplications(apps.Where((a, n) => n != index).ToList(), root.ApplicationToken);
                    else { var next = JsonConvert.DeserializeObject<AgentSettings>(JsonConvert.SerializeObject(root.AgentSettings.Value)); next.SearchRoots.RemoveAt(index - apps.Count); root.AgentSettings.Save(next, root.ApplicationToken); }
                    listPage = 0; RefreshLists();
                }));
            }
            if (total == 0) UI.Text(listPanel, "Empty", "还没有登记应用或查找目录。", new Vector2(0, 10), new Vector2(290, 80), 18);
            UI.Button(listPanel, "Previous", "‹", new Vector2(-90, -130), new Vector2(60, 32), () => { listPage = Math.Max(0, listPage - 1); RefreshLists(); });
            UI.Button(listPanel, "Next", "›", new Vector2(90, -130), new Vector2(60, 32), () => { listPage = Math.Min(Math.Max(0, (total - 1) / 4), listPage + 1); RefreshLists(); });
            UI.Button(listPanel, "Back", "返回", new Vector2(0, -165), new Vector2(140, 28), OpenTools);
        }
        private async Task ShowAuditAsync()
        {
            long requested = panelGeneration;
            try { var rows = await ((IAgentToolStore)root.Database).ReadToolsAsync(destroyCancellationToken); if (this != null && requested == panelGeneration) ShowReader("最近操作\n\n" + (rows.Count == 0 ? "还没有执行记录。" : string.Join("\n\n", rows.Take(30).Select(r => DateTimeOffset.Parse(r.At).ToLocalTime().ToString("MM-dd HH:mm") + "\n" + AgentToolCatalog.Label(r.Tool) + " · " + AuditStatus(r.Status))))); }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("tool audit read failed", error); }
        }
        private static string AuditStatus(string status) => status == "succeeded" ? "已执行" : status == "denied" ? "本次已取消" : status == "failed" ? "没有完成" : status == "interrupted" ? "结果待核对，不会自动重做" : "处理中";
        public async Task<bool> ConfirmToolAsync(AgentPermission request, CancellationToken ct)
        {
            if (toolsPanel == null) BuildToolsPanel(); SelectPanel("permission"); permissionPanel.gameObject.SetActive(true);
            permissionText.text = request.Description; permissionText.pageToDisplay = 1;
            var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); permission = pending;
            using var registration = ct.Register(() => pending.TrySetCanceled(ct));
            try { return await pending.Task; }
            finally { if (ReferenceEquals(permission, pending)) { permission = null; if (this != null && !leaving && panel == "permission") BackToChat(); } }
        }
        public void AnswerPermission(bool allow) { permission?.TrySetResult(allow); }
        partial void CloseToolsPanel()
        {
            var declined = permission; permission = null; declined?.TrySetResult(false);
            toolsPanel?.gameObject.SetActive(false); permissionPanel?.gameObject.SetActive(false); listPanel?.gameObject.SetActive(false);
            appName?.gameObject.SetActive(false); if (readerPanel != null && readerPanel.Find("SaveApplication") != null) readerPanel.Find("SaveApplication").gameObject.SetActive(false);
        }
    }
}
