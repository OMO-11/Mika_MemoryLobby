#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using GiftCompanion.SharedUI;
using UnityEngine;
using UnityEngine.UI;

namespace GiftCompanion.Validation
{
    internal static class V1RefinementProbe
    {
        public static async Task RunAsync(AppRoot root, Dictionary<string, bool> flags, Func<string, Task> capture, CancellationToken ct)
        {
            var args = Environment.GetCommandLineArgs();
            string isolated = Core.Config.UserDataPaths.Resolve(false, "", args);
            if (!isolated.Contains(System.IO.Path.DirectorySeparatorChar + "Validation" + System.IO.Path.DirectorySeparatorChar)) throw new InvalidOperationException("Isolated data required");
            Screen.SetResolution(1280, 720, false); await Task.Delay(300, ct);
            foreach (var old in UnityEngine.Object.FindObjectsByType<PlayerMaintenance>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
            await Task.Delay(100, ct); PlayerMaintenance.Open(); await Task.Delay(300, ct);
            var panel = UnityEngine.Object.FindFirstObjectByType<PlayerMaintenance>();
            await capture("connection");
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "Health").onClick.Invoke(); await Task.Delay(100, ct); await capture("maintenance");
            flags["configResetButtonPresent"] = panel.GetComponentsInChildren<Button>().Any(b => b.name == "ResetConfig");
            var originals = root.ConfigRecovery.List();
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "ResetConfig").onClick.Invoke();
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "SelectConfig").onClick.Invoke();
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "ResetConfig").onClick.Invoke();
            flags["changingResetTargetRequiresNewConfirmation"] = originals.All(f => System.IO.File.Exists(System.IO.Path.Combine(root.DataDirectory, "Config", f)));
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "Backup").onClick.Invoke(); await Task.Delay(100, ct); await capture("backup");
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "Close").onClick.Invoke(); await Task.Delay(100, ct);
            var settings = root.Config.Configuration.Copy(); settings.VoiceEnabled = false; await root.Config.SaveAsync(settings, ct);
            await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
            CompanionScreen screen;
            while ((screen = UnityEngine.Object.FindFirstObjectByType<CompanionScreen>())?.IsReady != true) await Task.Delay(50, ct);
            await Task.Delay(300, ct); await capture("space");
            screen.OpenSettings(); await Task.Delay(100, ct); await capture("settings");
            var settingsPanel = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Single(r => r.name == "Settings" && r.gameObject.activeInHierarchy);
            var settingsButtons = settingsPanel.GetComponentsInChildren<Button>();
            var voiceRect = settingsButtons.Single(b => b.name == "Voice").GetComponent<RectTransform>();
            var maintenanceRect = settingsButtons.Single(b => b.name == "Maintenance").GetComponent<RectTransform>();
            flags["settingsButtonsDoNotOverlap"] = Mathf.Abs(voiceRect.anchoredPosition.y - maintenanceRect.anchoredPosition.y) > (voiceRect.sizeDelta.y + maintenanceRect.sizeDelta.y) / 2;
            if (settingsButtons.Any(b => b.name == "Display"))
            {
                screen.OpenDisplaySettings(); await Task.Delay(100, ct); await capture("display");
                var display = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Single(r => r.name == "Display" && r.gameObject.activeInHierarchy);
                flags["displayLabelsActuallyVisible"] = display.GetComponentsInChildren<TMPro.TMP_Text>().Where(t => t.name.EndsWith("Label", StringComparison.Ordinal)).All(t =>
                { t.ForceMeshUpdate(); return t.textInfo.characterInfo.Take(t.textInfo.characterCount).Any(c => c.isVisible); });
                display.GetComponentsInChildren<Button>().Single(b => b.name == "OriginalTone").onClick.Invoke();
                screen.OpenMenu(); screen.Back(); await Task.Delay(100, ct); await capture("space_saved_after_unsaved_preview");
                var tone = UnityEngine.Object.FindFirstObjectByType<CompanionTone>();
                flags["unsavedToneRestoresSavedValue"] = Math.Abs(tone.exposure - root.Display.Value.Exposure) < .001f;
            }
            screen.OpenLibrary(); await Task.Delay(100, ct); await capture("library");
            var store = (IDiaryStore)root.Database; var conversations = (IConversationStore)root.Database;
            var turn = await conversations.BeginTurnAsync("refinement-reading", "仅供隔离验证的聊天", InteractionSource.Companion, ct);
            await conversations.CompleteTurnAsync(turn, "这是用于排版检查的合成回复。", root.Character.State, ct);
            var turns = await conversations.LoadTurnsAsync(10, true, ct); var snapshot = await store.SnapshotDiaryAsync(ct);
            var entry = new DiaryEntry { Id = Guid.NewGuid().ToString("N"), Fingerprint = Guid.NewGuid().ToString("N"), Status = "active", CreatedAt = DateTimeOffset.UtcNow,
                FirstAt = snapshot.FirstAt.Value, LastAt = snapshot.LastAt.Value, TurnCount = snapshot.Count,
                Body = string.Join("\n\n", Enumerable.Repeat("老师，这是用于阅读检查的合成日记段落。这里可以回望聊过的话题，也可以慢慢往下翻，离开后再回来继续读。它不代表真实共同经历。(´▽｀)", 12)) };
            var sources = turns.SelectMany(t => new[] { t.UserMessageId, t.AssistantMessageId }).ToArray();
            flags["syntheticDiarySaved"] = await store.CommitDiaryAsync(snapshot, entry, sources, ct);
            await screen.OpenDiaryAsync(); await Task.Delay(100, ct); await capture("diary");
            flags["diaryOpen"] = screen.ActivePanel == "Diary";
            flags["noVoiceProcess"] = root.Voice.OwnedProcessId == 0;
            flags["displayFilePresent"] = System.IO.File.Exists(root.Display.FilePath);
            await root.Controller.SwitchModeAsync(CharacterMode.Home, ct);
            string archive = System.IO.Path.Combine(root.DataDirectory, "Backups", "refinement-preview.mikabackup");
            await root.BackupAsync(archive, false, ct); var preview = await root.Backups.InspectAsync(archive, ct);
            flags["restoreSpacePreviewPresent"] = preview.Space != null && preview.Space.AvailableBytes.HasValue;
            PlayerMaintenance.Open(); await Task.Delay(100, ct); panel = UnityEngine.Object.FindFirstObjectByType<PlayerMaintenance>();
            var type = typeof(PlayerMaintenance); var binding = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            type.GetField("preview", binding).SetValue(panel, preview); type.GetMethod("RenderPreview", binding).Invoke(panel, null);
            await Task.Delay(100, ct); await capture("restore_preview");
            Screen.SetResolution(900, 700, false); await Task.Delay(300, ct); await capture("restore_preview_small");
            var window = panel.transform.Find("Window").GetComponent<RectTransform>(); var corners = new Vector3[4]; window.GetWorldCorners(corners);
            flags["smallWindowFitsMaintenance"] = corners.All(p => p.x >= -1 && p.y >= -1 && p.x <= Screen.width + 1 && p.y <= Screen.height + 1);
            Screen.SetResolution(1280, 720, false);
            panel.GetComponentsInChildren<Button>().Single(b => b.name == "Close").onClick.Invoke(); await Task.Delay(100, ct);
        }
    }
}
#endif
