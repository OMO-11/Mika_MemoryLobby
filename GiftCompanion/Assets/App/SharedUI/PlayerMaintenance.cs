using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.SharedUI
{
    // A temporary Home overlay. Existing mode controllers keep sole ownership of navigation/lifetimes.
    public sealed class PlayerMaintenance : MonoBehaviour
    {
        private AppRoot root;
        private RectTransform body;
        private TMP_Text note;
        private TMP_InputField key;
        private BackupPreview preview;
        private bool busy, includeAudio;
        private CancellationTokenSource lifetime;
        private readonly ConfigResetSelection resetSelection = new ConfigResetSelection();
        private string restorePreviousArmed;
        public enum Page { Connection, Backup, Health }
        private static Page requestedPage;
        public static bool RequestedFromMode { get; private set; }
        public static void OpenFromMode()=>OpenFromMode(Page.Connection);
        public static async void OpenFromMode(Page page)
        {
            requestedPage=page;
            var app = AppRoot.Instance;
            if (app == null || !app.IsReady || app.PlayerWorkBusy) return;
            if (app.Character.CurrentMode == GiftCompanion.Core.Character.CharacterMode.Home) { Open(); return; }
            RequestedFromMode = true;
            try
            {
                var companion = FindFirstObjectByType<GiftCompanion.Companion.CompanionScreen>();
                if (companion != null) await companion.LeaveForMaintenanceAsync();
                else await app.Controller.SwitchModeAsync(GiftCompanion.Core.Character.CharacterMode.Home);
            }
            catch (Exception) { RequestedFromMode = false; app.Logger?.Info("maintenance navigation failed retry=true"); }
        }
        public static void Open()
        {
            RequestedFromMode = false;
            if (FindFirstObjectByType<PlayerMaintenance>() != null) return;
            var go = new GameObject("资料与连接", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PlayerMaintenance));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
        private void Start()
        {
            root = AppRoot.Instance; lifetime = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken);
            UI.Panel(transform, "Shade", Vector2.zero, new Vector2(6000, 6000), new Color(0, 0, 0, .8f));
            var panel = UI.Panel(transform, "Window", Vector2.zero, new Vector2(1100, 640)).rectTransform;
            var title = UI.Text(panel, "Title", "资料与连接", new Vector2(-230, 280), new Vector2(500, 46), 28, TextAlignmentOptions.Left); title.color = UI.Accent;
            UI.Button(panel, "Close", "返回", new Vector2(475, 280), new Vector2(100, 40), Close);
            UI.Button(panel, "Connection", "联网配置", new Vector2(-365, 220), new Vector2(210, 42), Connection);
            UI.Button(panel, "Backup", "备份与恢复", new Vector2(-120, 220), new Vector2(230, 42), Backup);
            UI.Button(panel, "Health", "资料检查", new Vector2(125, 220), new Vector2(210, 42), Health);
            UI.Button(panel, "Files", "打开资料文件夹", new Vector2(370, 220), new Vector2(240, 42), () => PlayerFiles.OpenFolder(root.Config.UserDataPath));
            body = UI.Rect(panel, "Body", new Vector2(0, 0), new Vector2(1000, 380));
            note = UI.Text(panel, "Status", root.RestoreNotice ?? "未配置Key也可以查看场景和已有资料。", new Vector2(0, -260), new Vector2(1000, 65), 18, TextAlignmentOptions.TopLeft); note.overflowMode = TextOverflowModes.Truncate;
            switch(requestedPage){case Page.Backup:Backup();break;case Page.Health:Health();break;default:Connection();break;}
            requestedPage=Page.Connection;
        }
        private bool Clear()
        {
            if (busy) return false;
            resetSelection.Cancel();
            restorePreviousArmed = null;
            if (key != null) key.text = ""; key = null;
            if (preview != null) { root.Backups.Discard(preview); preview = null; }
            foreach (Transform child in body) Destroy(child.gameObject);
            return true;
        }
        private void Connection()
        {
            if (!Clear()) return;
            UI.Text(body, "Instructions", (root.LLM.IsConfigured ? "已载入联网凭据。输入新Key可以替换。" : "老师，联网对话需要先配置你的 DeepSeek API Key。") + "\nKey只在本机加密保存；不会写入人物卡、备份或诊断。\n保存不会发起请求；测试连接会使用一次很短的联网请求。", new Vector2(0, 115), new Vector2(960, 120), 22, TextAlignmentOptions.TopLeft);
            var background = UI.Panel(body, "Key", new Vector2(0, 10), new Vector2(940, 50), new Color(.12f, .17f, .25f));
            var viewport = UI.Rect(background.transform, "Viewport", Vector2.zero, new Vector2(910, 44)); viewport.gameObject.AddComponent<RectMask2D>();
            var text = UI.Text(viewport, "Value", "", Vector2.zero, new Vector2(900, 42), 22, TextAlignmentOptions.Left);
            key = background.gameObject.AddComponent<TMP_InputField>(); key.textComponent = (TextMeshProUGUI)text; key.textViewport = viewport; key.fontAsset = UI.Chinese; key.contentType = TMP_InputField.ContentType.Password; key.characterLimit = 4096; key.richText = false;
            UI.Button(body, "SaveKey", "保存并重新启动", new Vector2(-255, -80), new Vector2(350, 48), () => _ = SaveKeyAsync());
            UI.Button(body, "TestKey", "测试连接", new Vector2(255, -80), new Vector2(350, 48), () => _ = Run(async ct =>
            {
                await root.TestConnectionAsync(key.text, ct);
                note.text = "连接成功，可以开始对话了。";
            }));
            UI.Text(body, "Offline", "可以稍后配置。换电脑或 Windows 账户时，需要重新输入Key。", new Vector2(0, -155), new Vector2(960, 40), 19);
        }
        private async Task SaveKeyAsync()
        {
            await Run(async ct =>
            {
                string value = key.text; key.text = "";
                await root.MaintenanceAsync(token => { LocalSecrets.SaveDeepSeekKey(root.Config.UserDataPath, value, token); return Task.FromResult(true); }, ct);
                value = null; note.text = "Key已加密保存，正在重新启动。";
            }, true);
        }
        private void Backup()
        {
            if (!Clear()) return;
            includeAudio = root.Backups.LoadPolicy(lifetime.Token).IncludeAudio;
            UI.Text(body, "Scope", "完整备份：对话与记忆、人物卡和清单、导入素材、参考音频、日记MD。\n本机Key、安装环境和诊断不包含在内。备份属于你的私人资料，请妥善保管。", new Vector2(0, 125), new Vector2(960, 95), 21, TextAlignmentOptions.TopLeft);
            Button audio = null;
            audio = UI.Button(body, "IncludeAudio", "", new Vector2(0, 45), new Vector2(900, 45), () => { includeAudio = !includeAudio; BackupRefresh(); });
            void BackupRefresh() { audio.GetComponentInChildren<TMP_Text>().text = includeAudio ? "历史语音：包含（点击改为精简备份）" : "历史语音：不包含（点击改为完整复听）"; }
            BackupRefresh();
            var size = UI.Text(body, "AudioSize", "正在统计历史语音…", new Vector2(0, -182), new Vector2(960, 28), 17);
            _ = ReadAudioSizeAsync(size);
            UI.Button(body, "CreateBackup", "备份资料…", new Vector2(-245, -25), new Vector2(410, 48), () => _ = Run(async ct =>
            {
                string path = PlayerFiles.BackupFile(true); if (path == null) return;
                root.Backups.SavePolicy(includeAudio, ct);
                var result = await root.BackupAsync(path, includeAudio, ct); note.text = $"已保存备份，共{result.Files.Count}个文件。可以在所选位置找到它。";
            }));
            UI.Button(body, "InspectBackup", "从备份恢复…", new Vector2(245, -25), new Vector2(410, 48), () => _ = InspectAsync());
            UI.Button(body, "RestorePoints", "恢复前备份（Backups）", new Vector2(-245, -100), new Vector2(410, 42), () => PlayerFiles.OpenFolder(Path.Combine(root.Config.UserDataPath, "Backups")));
            UI.Button(body, "Guide", "阅读备份与迁移说明", new Vector2(245, -100), new Vector2(410, 42), OpenGuide);
            UI.Button(body, "OriginalData", "原资料副本（RestorePoints）", new Vector2(-245, -146), new Vector2(410, 38), () => PlayerFiles.OpenFolder(Path.Combine(root.Config.UserDataPath, "RestorePoints")));
            UI.Button(body, "ConfigOriginals", "配置原件（ConfigRecovery）", new Vector2(245, -146), new Vector2(410, 38), () => PlayerFiles.OpenFolder(root.ConfigRecovery.RecoveryFolder));
        }
        private async Task ReadAudioSizeAsync(TMP_Text label)
        {
            try { long bytes = await Task.Run(() => root.Backups.AudioBytes(), lifetime.Token); if (label != null) label.text = $"历史语音占用 {bytes / 1048576d:F1} MiB；恢复先预览，确认后才替换并重启。"; }
            catch (Exception) { if (label != null) label.text = "暂时无法统计历史语音大小，请检查文件夹权限。"; }
        }
        private async Task InspectAsync()
        {
            await Run(async ct =>
            {
                string path = PlayerFiles.BackupFile(false); if (path == null) return;
                if (preview != null) root.Backups.Discard(preview);
                preview = await root.Backups.InspectAsync(path, ct);
            });
            if (this == null || preview == null) return;
            RenderPreview();
        }
        private void RenderPreview()
        {
            foreach (Transform child in body) Destroy(child.gameObject);
            UI.Text(body, "Preview", preview.Description, new Vector2(0, 20), new Vector2(960, 300), 19, TextAlignmentOptions.TopLeft);
            UI.Button(body, "ConfirmRestore", "确认替换当前资料并重启", new Vector2(0, -145), new Vector2(620, 50), () => _ = Run(async ct =>
            {
                await root.RestoreAsync(preview, ct); preview = null;
                note.text = "当前资料已留恢复点，正在关闭连接后恢复。";
            }, true));
        }
        private void Health()
        {
            if (!Clear()) return;
            string install = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool runtime = File.Exists(Path.Combine(install, "Runtime", "GPT-SoVITS", "runtime", "python.exe"));
            string issues = (root.Config.CharacterCardIssue ?? "") + " " + (root.VoiceConfigurationIssue ?? "") + " " + (root.LocalizationIssue ?? "") + " " + string.Join(" ", root.Library.Warnings) + " " + root.AgentSettings.Issue;
            UI.Text(body, "Report", $"程序：Phase6 / Unity {Application.unityVersion}　资料库：v{root.DataSchemaVersion}\n联网凭据：{(root.LLM.IsConfigured ? "已载入" : "未载入")}　包内语音环境：{(runtime ? "存在" : "不完整，文字仍可使用")}\n{issues}", new Vector2(0, 95), new Vector2(960, 160), 20, TextAlignmentOptions.TopLeft);
            UI.Button(body, "Reload", "重新读取资料（重启）", new Vector2(-320, -25), new Vector2(290, 42), () => _ = Run(ct => Task.CompletedTask, true));
            UI.Button(body, "RetryVoice", "重新读取语音配置", new Vector2(0, -25), new Vector2(290, 42), () => _ = Run(async ct => { await root.MaintenanceAsync(token => Task.FromResult(root.CreateVoiceRuntime()), ct); note.text = root.VoiceConfigurationIssue ?? "语音配置已读取，下次朗读时启动。"; }));
            UI.Button(body, "Diagnostic", "导出脱敏诊断", new Vector2(320, -25), new Vector2(290, 42), () => _ = Run(ct =>
            {
                string probe = Path.Combine(root.Config.UserDataPath, "write-check-" + Guid.NewGuid().ToString("N")); File.WriteAllText(probe, ""); File.Delete(probe);
                string path = Path.Combine(root.Config.UserDataPath, "Diagnostics", "diagnostic-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
                AtomicDocument.WriteJson(path, new { application = "GiftCompanion-Phase6", unity = Application.unityVersion, schema = root.DataSchemaVersion, os = Environment.OSVersion.VersionString, bits = Environment.Is64BitProcess ? 64 : 32, writable = true, configured = root.LLM.IsConfigured, runtimeFilesPresent = runtime, voiceState = root.Voice?.State.ToString(), scope = "仅版本、存在性与状态；无Key、聊天、剪贴板、路径清单或日志正文。" }, ct);
                note.text = "已导出到资料目录 Diagnostics，可用记事本查看范围后分享。"; return Task.CompletedTask;
            }));
            var files = root.ConfigRecovery.List();
            int index = 0; resetSelection.Select(files.FirstOrDefault());
            var choose = UI.Button(body, "SelectConfig", "选择：" + PlayerConfigRecovery.Label(resetSelection.Selected), new Vector2(-160, -95), new Vector2(610, 42), () => { if (files.Length > 0) { index = (index + 1) % files.Length; resetSelection.Select(files[index]); restorePreviousArmed = null; body.Find("SelectConfig").GetComponentInChildren<TMP_Text>().text = "选择：" + PlayerConfigRecovery.Label(resetSelection.Selected); note.text = "已选择" + PlayerConfigRecovery.Label(resetSelection.Selected) + "，文件：Config/" + resetSelection.Selected + "。之前的确认已取消。"; } });
            UI.Button(body, "ResetConfig", "保留原件并重置此项", new Vector2(330, -95), new Vector2(310, 42), () =>
            {
                if (busy || resetSelection.Selected == null) return;
                restorePreviousArmed = null;
                if (!resetSelection.ConfirmOrArm()) { note.text = "再次点击确认：重置" + PlayerConfigRecovery.Label(resetSelection.Selected) + "（" + resetSelection.Selected + "）。此项回到默认，原件及副本保留；聊天与记忆不变，随后重启。"; return; }
                string selected = resetSelection.Selected;
                _ = Run(async ct =>
                {
                    await root.ResetConfigAsync(selected, ct);
                }, true);
            });
            UI.Button(body, "PreviousConfig", "恢复所选项目上一版…", new Vector2(-245, -150), new Vector2(450, 40), () =>
            {
                if (busy || resetSelection.Selected == null) return;
                resetSelection.Cancel(); string selected = resetSelection.Selected;
                if (!PlayerConfigRecovery.CanRestorePrevious(selected)) { restorePreviousArmed = null; note.text = "人物设定、背景音乐和陪伴空间支持此操作。其他项目请用完整备份恢复。"; return; }
                if (restorePreviousArmed != selected) { restorePreviousArmed = selected; note.text = "再次点击确认：恢复" + PlayerConfigRecovery.Label(selected) + "上一版并重启；当前文件另行保留，聊天与记忆不变。"; return; }
                restorePreviousArmed = null; _ = Run(async ct => { await root.RestorePreviousConfigAsync(selected, ct); }, true);
            });
            UI.Button(body, "ConfigRecovery", "查看重置前保留的原件", new Vector2(245, -150), new Vector2(450, 40), () => PlayerFiles.OpenFolder(root.ConfigRecovery.RecoveryFolder));
            UI.Text(body, "Privacy", "诊断仅含版本与状态，不含聊天、Key或剪贴板。", new Vector2(0, -190), new Vector2(960, 28), 16);
        }
        private void OpenGuide() => PlayerFiles.ReadText(Path.Combine(root.Config.UserDataPath, "备份与迁移说明.md"));
        private async Task Run(Func<CancellationToken, Task> operation, bool restart = false)
        {
            if (busy) return; busy = true; note.text = "正在处理，请稍候…";
            foreach (var button in GetComponentsInChildren<Button>()) button.interactable = false;
            try { await operation(lifetime.Token); if (restart && !await root.RestartAsync()) note.text = "已保存；请关闭后重新打开程序使其生效。"; }
            catch (OperationCanceledException) { if (this != null) note.text = "操作已取消，未完成的备份不会留下成品。"; }
            catch (Exception e) { if (this != null) note.text = e is LLMServiceException ? "连接未成功（" + ((LLMServiceException)e).Code + "），请检查Key和网络后重试。" : e is InvalidDataException || e is ArgumentException ? e.Message : "暂时没能完成，当前资料已保留。请检查文件权限、磁盘空间后重试。"; root.Logger?.Info("maintenance operation failed type=" + e.GetType().Name); }
            finally { busy = false; if (this != null) foreach (var button in GetComponentsInChildren<Button>()) button.interactable = true; }
        }
        private void Close() { if (!busy) { resetSelection.Cancel(); AtomicDocument.WriteJson(Path.Combine(root.Config.UserDataPath, "Config", "onboarding.json"), new { Dismissed = true }, root.ApplicationToken); root.AcknowledgeRestoreNotice(); Destroy(gameObject); } }
        private void OnDestroy() { if (key != null) key.text = ""; lifetime?.Cancel(); lifetime?.Dispose(); if (preview != null) root?.Backups?.Discard(preview); }
    }
}
