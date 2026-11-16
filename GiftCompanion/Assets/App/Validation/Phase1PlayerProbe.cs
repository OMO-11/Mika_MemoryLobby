using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.Validation
{
    // Explicit developer entry point; no ordinary startup request or permanent diagnostic panel.
    public sealed class Phase1PlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-phase1-probe");
            if (index < 0) { enabled = false; return; }
            if (index + 2 >= args.Length) { Application.Quit(2); return; }
            string reports = Path.GetFullPath(args[index + 1]);
            string mode = args[index + 2];
            Directory.CreateDirectory(reports);
            bool passed = false;
            try
            {
                var root = AppRoot.Instance;
                await root.Initialization;
                var deadline = Stopwatch.StartNew();
                while (root.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Home")
                {
                    if (deadline.Elapsed.TotalSeconds > 30) throw new TimeoutException();
                    await Task.Yield();
                }
                var core = root.Character;
                var state = core.State;
                bool persistent = root.Database.IsPersistent && root.Database.SchemaVersion == GiftCompanion.Core.Persistence.DatabaseService.CurrentSchemaVersion;
                bool restored = state.CurrentTopic == "phase1 validation" && state.Energy == .42f && state.Mood == CharacterEmotion.Happy && state.LastInteractionAt.HasValue;
                bool startedHome = core.CurrentMode == CharacterMode.Home;
                bool shared = true, petPassed = true, homePassed = true;
                var windowChecks = new System.Collections.Generic.List<object>();
                bool llmPassed = mode != "Write";
                int chunks = 0, characters = 0;
                long? firstDeltaMs = null, elapsedMs = null;
                if (mode == "Write")
                {
                    state.CurrentTopic = "phase1 validation"; state.Energy = .42f; state.Mood = CharacterEmotion.Happy;
                    for (int cycle = 0; cycle < 3; cycle++)
                    {
                        foreach (var target in new[] { CharacterMode.Companion, CharacterMode.Home, CharacterMode.AgentPet, CharacterMode.Home })
                        {
                            await root.Controller.SwitchModeAsync(target);
                            if (target != CharacterMode.Companion)
                            {
                                bool pet = target == CharacterMode.AgentPet;
                                var windowWait = Stopwatch.StartNew();
                                while (!WindowMatches(pet) && windowWait.Elapsed.TotalSeconds < 8) await Task.Delay(100);
                                windowChecks.Add(new { cycle, target = target.ToString(), width = Screen.width, height = Screen.height,
                                    transparent = IsTransparent(), borderless = IsBorderless(), topmost = IsTopmost(), elapsedMs = windowWait.ElapsedMilliseconds });
                            }
                            shared &= ReferenceEquals(core, root.Character) && ReferenceEquals(state, root.Character.State) &&
                                FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length == 1 && core.CurrentMode == target;
                            if (target == CharacterMode.AgentPet)
                                petPassed &= IsTransparent() && IsBorderless() && IsTopmost() && Screen.width == 600 && Screen.height == 500;
                            if (target == CharacterMode.Home)
                                homePassed &= !IsTransparent() && !IsBorderless() && !IsTopmost() && Screen.width == 1280 && Screen.height == 720;
                            if (cycle == 0 && target == CharacterMode.Companion)
                            {
                                var watch = Stopwatch.StartNew();
                                await foreach (var chunk in core.StreamMessageAsync("请仅回复：连接成功。", InteractionSource.Companion, root.Controller.ActiveModeToken))
                                {
                                    if (chunk.IsFinal) llmPassed = chunk.Result.Success;
                                    else { firstDeltaMs ??= watch.ElapsedMilliseconds; chunks++; characters += chunk.Text.Length; }
                                }
                                elapsedMs = watch.ElapsedMilliseconds;
                            }
                        }
                    }
                    llmPassed &= characters > 0 && state.LastInteractionAt.HasValue;
                }
                if (mode == "NoKey")
                {
                    llmPassed = !root.LLM.IsConfigured && (await core.SendMessageAsync("validation", InteractionSource.Companion, default)).ErrorCode == "LLMNotConfigured";
                    await root.Controller.SwitchModeAsync(CharacterMode.Companion);
                    await root.Controller.SwitchModeAsync(CharacterMode.Home);
                    await root.Controller.SwitchModeAsync(CharacterMode.AgentPet);
                    await root.Controller.SwitchModeAsync(CharacterMode.Home);
                }
                string dataPath = root.Config.UserDataPath;
                await root.ShutdownAsync();
                passed = persistent && startedHome && shared && petPassed && homePassed && llmPassed && root.ShutdownSucceeded && (mode != "Restore" || restored);
                File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new {
                    passed, mode, persistent, startedHome, shared, petPassed, homePassed, llmPassed, restored,
                    chunks, characters, firstDeltaMs, elapsedMs, windowChecks, shutdownSaved = root.ShutdownSucceeded, dataPath,
                    unity = Application.unityVersion, verifiedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    scope = "Formal Windows AppRoot/Core/services in isolated Player data; no credentials or reply text recorded."
                }, Formatting.Indented));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(reports, "player_" + mode.ToLowerInvariant() + ".json"), JsonConvert.SerializeObject(new { passed = false, errorType = error.GetType().Name }));
            }
            if (AppRoot.Instance != null) await AppRoot.Instance.ShutdownAsync();
            Application.Quit(passed ? 0 : 1);
        }
        private static bool WindowMatches(bool pet) => IsTransparent() == pet && IsBorderless() == pet && IsTopmost() == pet &&
            Screen.width == (pet ? 600 : 1280) && Screen.height == (pet ? 500 : 720);
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsTransparent();
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsBorderless();
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsTopmost();
#endif
    }
}
