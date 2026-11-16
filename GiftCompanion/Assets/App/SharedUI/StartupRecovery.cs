using System;
using System.IO;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Persistence;
using TMPro;
using UnityEngine;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.SharedUI
{
    public sealed class StartupRecovery : MonoBehaviour
    {
        private TMP_Text note;
        private bool busy;
        private AppRoot root;
        private void Start()
        {
            root = AppRoot.Instance;
            var canvas = UI.Canvas("启动恢复");
            UI.Panel(canvas.transform, "Background", Vector2.zero, new Vector2(3000, 1800));
            UI.Text(canvas.transform, "Title", "资料暂时没有准备好", new Vector2(0, 250), new Vector2(900, 60), 32);
            note = UI.Text(canvas.transform, "Details", "原资料会保留。请检查磁盘空间和文件权限；若刚切换程序版本，请使用与资料库对应的版本。\n可以打开资料文件夹查看，或选择此前的完整备份恢复。", new Vector2(0, 65), new Vector2(950, 290), 20, TMPro.TextAlignmentOptions.TopLeft);
            UI.Button(canvas.transform, "Files", "打开资料文件夹", new Vector2(-235, -110), new Vector2(380, 46), () => { if (!string.IsNullOrEmpty(root.DataDirectory)) PlayerFiles.OpenFolder(root.DataDirectory); });
            UI.Button(canvas.transform, "Restore", "选择备份恢复…", new Vector2(235, -110), new Vector2(380, 46), () => _ = RestoreAsync());
            UI.Button(canvas.transform, "Quit", "关闭程序", new Vector2(0, -255), new Vector2(200, 45), root.RequestQuit);
        }
        private async Task RestoreAsync()
        {
            if (busy || string.IsNullOrEmpty(root.DataDirectory)) return; busy = true;
            try
            {
                var backups = root.Backups ?? new PlayerBackup(root.DataDirectory, Path.GetFullPath(Path.Combine(Application.dataPath, "..")), root.FixedCharacterId);
                string path = PlayerFiles.BackupFile(false); if (path == null) return;
                var preview = await backups.InspectAsync(path, root.ApplicationToken); note.text = preview.Description;
                UI.Button(note.transform.parent, "Confirm", "确认恢复并重启（原目录保留）", new Vector2(0, -180), new Vector2(750, 48), () => _ = ConfirmAsync(backups, preview));
            }
            catch (Exception error) { note.text = error is InvalidDataException ? error.Message : "备份未通过校验，当前资料没有替换。请换一份备份或联系维护者。"; }
            finally { busy = false; }
        }
        private async Task ConfirmAsync(PlayerBackup backups, BackupPreview preview)
        {
            if (busy) return; busy = true;
            try { await Task.Run(() => backups.Schedule(preview, root.ApplicationToken), root.ApplicationToken); await root.RestartAsync(); }
            catch (Exception) { note.text = "尚未完成恢复，请关闭后重试；原资料仍保留。"; busy = false; }
        }
    }
}
