using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace GiftCompanion.Editor
{
    public static class Phase0Build
    {
        [MenuItem("Gift Companion/Phase 0/Build Windows Probe")]
        public static void Run()
        {
            Build("Phase0");
        }

        public static void Build(string phase)
        {
            bool release = phase == "Phase6" || phase == "V1Refinement" || phase == "V1Closeout" || phase == "V2Window" || phase == "V2Reading" || phase == "V2Maintenance" || phase == "V2Final" || phase == "V2Seaside" || phase == "V2SeasideReview";
            int exitCode = 1;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
                PlayerSettings.useFlipModelSwapchain = false;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.allowFullscreenSwitch = false;
                PlayerSettings.runInBackground = true;
                string destination = Path.GetFullPath($"Builds/{phase}/GiftCompanion.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = destination, target = BuildTarget.StandaloneWindows64,
                    options = (release || phase.StartsWith("Phase3", StringComparison.Ordinal) || (phase == "Phase4" || phase == "Phase5") || (phase == "Bilingual" || (phase == "VoicePolish" || phase == "ExpressionDiary")) ? BuildOptions.None : BuildOptions.Development) |
                        (release || phase == "Phase1" || phase == "Phase2" || phase.StartsWith("Phase3", StringComparison.Ordinal) || (phase == "Phase4" || phase == "Phase5") || (phase == "Bilingual" || (phase == "VoicePolish" || phase == "ExpressionDiary")) ? BuildOptions.CleanBuildCache : BuildOptions.None)
                });
                bool passed = report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0;
                if (passed && (release || phase.StartsWith("Phase3", StringComparison.Ordinal) || (phase == "Phase4" || phase == "Phase5") || (phase == "Bilingual" || (phase == "VoicePolish" || phase == "ExpressionDiary"))))
                {
                    string licenses = Path.Combine(Path.GetDirectoryName(destination), "Licenses"); Directory.CreateDirectory(licenses);
                    File.Copy("Assets/App/SharedUI/Resources/Fonts/OFL.txt", Path.Combine(licenses, "NotoSansCJK-OFL.txt"), true);
                    File.Copy("Docs/Licenses/NotoSansCJK-Notice.txt", Path.Combine(licenses, "NotoSansCJK-Notice.txt"), true);
                    File.Copy("Docs/Licenses/Spine-Runtimes-License.txt", Path.Combine(licenses, "Spine-Runtimes-License.txt"), true);
                }
                Directory.CreateDirectory($"Docs/Validation/{phase}");
                File.WriteAllText($"Docs/Validation/{phase}/player_build.json", JsonConvert.SerializeObject(new {
                    passed, result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                    bytes = report.summary.totalSize, seconds = report.summary.totalTime.TotalSeconds,
                    unity = Application.unityVersion, renderer = "Direct3D11", flipModelSwapchain = false
                }, Formatting.Indented));
                exitCode = passed ? 0 : 1;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }
}
