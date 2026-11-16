using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.SharedUI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#endif
namespace GiftCompanion.Validation
{
    public sealed class V1CloseoutProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int marker = Array.IndexOf(args, "-v1-closeout-probe");
            if (marker < 0) { enabled = false; return; }
            if (marker + 1 >= args.Length) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[marker + 1]); var flags = new Dictionary<string, bool>(); string error = null;
            var root = AppRoot.Instance;
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var ct = deadline.Token;
                await root.Initialization;
                while (root.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Companion") { await Task.Delay(50, ct); }
                flags["directCompanionStartup"] = root.Character.CurrentMode == CharacterMode.Companion;
                flags["entryFileCreated"] = File.Exists(Path.Combine(root.DataDirectory, "Config", "app_entry.json"));
                flags["noForcedTechnicalPanel"] = FindFirstObjectByType<PlayerMaintenance>() == null;
                bool rejected = false; try { await root.Controller.SwitchModeAsync(CharacterMode.AgentPet, ct); } catch (InvalidOperationException) { rejected = true; }
                flags["petRouteRejected"] = rejected && root.Character.CurrentMode == CharacterMode.Companion;
                Directory.CreateDirectory(reports); await Capture(Path.Combine(reports, "direct_companion.png"));
                var core = root.Character; await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); await Task.Delay(200, ct);
                var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
                flags["petButtonsHidden"] = !buttons.Any(b => b.name == "AgentPet" || b.name == "ResetPet");
                flags["maintenanceStillAccessible"] = FindFirstObjectByType<PlayerMaintenance>() != null || buttons.Any(b => b.name == "Maintenance");
                foreach (var panel in FindObjectsByType<PlayerMaintenance>(FindObjectsSortMode.None)) Destroy(panel.gameObject);
                await Task.Delay(100, ct); await Capture(Path.Combine(reports, "home_without_pet.png"));
                await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                flags["sameCoreOnReturn"] = ReferenceEquals(core, root.Character);
                flags["schemaUnchanged"] = root.DataSchemaVersion == 10;
                flags["noVoiceInIsolatedSilentTest"] = root.Voice.OwnedProcessId == 0;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded;
            }
            catch (Exception e) { error = e.GetType().Name; }
            bool passed = error == null && flags.Count >= 10 && flags.Values.All(v => v);
            Directory.CreateDirectory(reports); AtomicDocument.WriteJson(Path.Combine(reports, "native.json"), new { passed, error, flags }, default, false);
            await root.ShutdownAsync(); Application.Quit(passed ? 0 : 1);
        }
        private Task Capture(string path)
        { var completion = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(path, completion)); return completion.Task; }
        private System.Collections.IEnumerator CaptureFrame(string path, TaskCompletionSource<bool> completion)
        {
            yield return new WaitForEndOfFrame(); Texture2D frame = null;
            try
            {
                frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); frame.Apply();
                File.WriteAllBytes(path, frame.EncodeToPNG()); completion.TrySetResult(true);
            }
            catch (Exception e) { completion.TrySetException(e); }
            finally { if (frame != null) Destroy(frame); }
        }
#endif
    }
}
