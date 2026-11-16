using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.Validation
{
    public sealed class Phase3PlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-phase3-probe");
            if (index < 0) { enabled = false; return; }
            if (index + 2 >= args.Length) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[index + 1]), mode = args[index + 2]; Directory.CreateDirectory(reports);
            bool real = mode == "Write", passed = false; string failure = null;
            var flags = new Dictionary<string, bool>();
            try
            {
                var root = AppRoot.Instance; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(3)); var ct = timeout.Token; await root.Initialization;
                await Until(() => !root.Controller.IsSwitching && SceneManager.GetActiveScene().name == "Home", ct); await Frames(4, ct);
                await Capture(reports, "home_" + mode.ToLowerInvariant());
                await root.Controller.SwitchModeAsync(CharacterMode.Companion, ct);
                await Until(() => FindFirstObjectByType<CompanionScreen>()?.IsReady == true, ct);
                var screen = FindFirstObjectByType<CompanionScreen>(); var visual = FindFirstObjectByType<CompanionPresentation>();
                if (mode == "Visual")
                {
                    await Frames(12, ct);
                    var materials = visual.Loader.Character.GetComponent<MeshRenderer>().sharedMaterials.Distinct().ToArray();
                    string mainShader = materials[0].shader.name;
                    visual.Loader.Motion.enabled = false;
                    visual.Loader.Expression.enabled = false;
                    visual.Loader.Character.GetComponent<CompanionPointerInteraction>().enabled = false;
                    visual.Loader.Character.timeScale = 0;
                    visual.Loader.Character.AnimationState.GetCurrent(0).TrackTime = 1.25f;
                    visual.Loader.Character.Update(0); visual.Loader.Character.LateUpdate();
                    foreach (var material in materials) { material.shader = Shader.Find("Spine/Skeleton"); material.SetFloat("_StraightAlphaInput", 1); material.EnableKeyword("_STRAIGHT_ALPHA_INPUT"); }
                    await Frames(3, ct); await Capture(reports, "release_original_shader");
                    var before = materials.Select(m => new { shader = m.shader.name, keywords = m.shaderKeywords, straightAlpha = m.GetFloat("_StraightAlphaInput") }).ToArray();
                    foreach (var material in materials) material.shader = Shader.Find("GiftCompanion/SpineStraightAlpha");
                    await Frames(3, ct); await Capture(reports, "release_explicit_alpha");
                    visual.Loader.renderCamera.allowHDR = false; await Frames(3, ct); await Capture(reports, "release_ldr");
                    File.WriteAllText(Path.Combine(reports, "release_shader_audit.json"), JsonConvert.SerializeObject(new { mainShader, sameAnimationFrame = 1.25f, before, explicitShaderAvailable = Shader.Find("GiftCompanion/SpineStraightAlpha") != null }, Formatting.Indented));
                    await root.ShutdownAsync(); Application.Quit(0); return;
                }
                flags["startsImmersive"] = !screen.IsInputOpen && !screen.IsMenuOpen && screen.ActivePanel == null;
                flags["sceneAlphaShader"] = visual.Loader.Character.SkeletonDataAsset.atlasAssets.SelectMany(a => a.Materials).All(m => m.shader.name == "GiftCompanion/SpineStraightAlpha");
                flags["developmentPanelHidden"] = !root.GetComponent<Phase2DevelopmentPanel>().enabled;
                screen.OpenInput(); flags["inputCapturesTouch"] = !visual.Loader.Character.GetComponent<CompanionPointerInteraction>().enabled;
                screen.Back(); flags["captureReleased"] = visual.Loader.Character.GetComponent<CompanionPointerInteraction>().enabled;
                await screen.SendAsync("老师来看看你，请用一两句中文回应。"); ct.ThrowIfCancellationRequested();
                flags["dialogue"] = real ? screen.LastDialogueResult?.Success == true : screen.LastDialogueResult?.ErrorCode == "LLMNotConfigured";
                flags["noControlText"] = !screen.SubtitleText.Contains("<META");
                flags["noSyntheticMouth"] = !visual.Loader.Speech.voice.isPlaying && !visual.Loader.Expression.SpeechActive;
                flags["emptyBgmSilent"] = !screen.HasBgm;
                if (real)
                {
                    var turns = await ((IConversationStore)root.Database).LoadTurnsAsync(10, true, ct);
                    flags["rawAndProjection"] = turns.Count > 0 && turns.Last().RawAssistantText.StartsWith("<META", StringComparison.Ordinal) &&
                        turns.Last().AssistantText == screen.LastDialogueResult.Text && screen.LastDialogueResult.Presentation != null;
                }
                await Frames(20, ct); await Capture(reports, "reply_" + mode.ToLowerInvariant());
                screen.OpenMenu(); flags["menuCapturesTouch"] = !visual.Loader.Character.GetComponent<CompanionPointerInteraction>().enabled;
                await Frames(3, ct); await Capture(reports, "menu_" + mode.ToLowerInvariant());
                await screen.OpenHistoryAsync(); flags["historyReadable"] = screen.VisibleHistoryCount > 0; await Frames(4, ct); await Capture(reports, "history_" + mode.ToLowerInvariant());
                screen.OpenSettings(); await Frames(3, ct); await Capture(reports, "settings_" + mode.ToLowerInvariant()); screen.Back(); screen.Back();
                Screen.SetResolution(1024, 768, FullScreenMode.Windowed); await Frames(25, ct); await Capture(reports, "resized_" + mode.ToLowerInvariant());
                flags["resize"] = Screen.width == 1024 && Screen.height == 768;
                await root.Controller.SwitchModeAsync(CharacterMode.Home, ct); await Frames(4, ct);
                flags["sceneReleased"] = screen == null && visual == null && FindObjectsByType<CompanionPointerInteraction>(FindObjectsSortMode.None).Length == 0;
                flags["singleCore"] = FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length == 1;
                flags["schemaCurrent"] = root.Database.SchemaVersion == DatabaseService.CurrentSchemaVersion;
                await root.ShutdownAsync(); flags["shutdownSaved"] = root.ShutdownSucceeded;
                passed = flags.Values.All(x => x);
            }
            catch (Exception error) { failure = error.GetType().Name; }
            File.WriteAllText(Path.Combine(reports, "ui_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed, mode, flags, errorType = failure, verifiedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented));
            Application.Quit(passed ? 0 : 1);
        }
        private static async Task Until(Func<bool> condition, CancellationToken ct)
        { while (!condition()) { ct.ThrowIfCancellationRequested(); await Task.Yield(); } }
        private static async Task Frames(int count, CancellationToken ct)
        { for (int i = 0; i < count; i++) { ct.ThrowIfCancellationRequested(); await Task.Yield(); } }
        private Task Capture(string folder, string name)
        { var done = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(folder, name, done)); return done.Task; }
        private IEnumerator CaptureFrame(string folder, string name, TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame(); Texture2D screenshot = null;
            try
            {
                screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); screenshot.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), screenshot.EncodeToPNG()); done.TrySetResult(true);
            }
            catch (Exception error) { done.TrySetException(error); }
            finally { if (screenshot != null) Destroy(screenshot); }
        }
#endif
    }
}
