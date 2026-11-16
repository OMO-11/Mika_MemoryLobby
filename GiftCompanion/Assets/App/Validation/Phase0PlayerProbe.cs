using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.Validation
{
    // Runs only with an explicit command line flag, writes evidence, then exits the probe player.
    public sealed class Phase0PlayerProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private string reportDirectory;
        private float deadline;
        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-phase0-window-probe");
            if (index < 0) { enabled = false; yield break; }
            if (index + 1 >= arguments.Length) { Exit(2); yield break; }
            reportDirectory = Path.GetFullPath(arguments[index + 1]);
            Directory.CreateDirectory(reportDirectory);
            deadline = Time.realtimeSinceStartup + 90;
            yield return WaitForHome();
            if (Time.realtimeSinceStartup >= deadline) { Fail("Startup timed out."); yield break; }

            var root = AppRoot.Instance;
            var transition = root.Controller.SwitchModeAsync(CharacterMode.AgentPet);
            while (!transition.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            if (transition.IsFaulted || !transition.IsCompleted) { Fail("Pet transition failed."); yield break; }
            yield return new WaitForSecondsRealtime(3);
            var camera = Camera.main;
            var pet = new {
                active = IsActive(), transparent = IsTransparent(), borderless = IsBorderless(), topmost = IsTopmost(),
                width = Screen.width, height = Screen.height, cameraAlpha = camera ? camera.backgroundColor.a : -1,
                roots = FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length,
                mode = root.Character.CurrentMode.ToString(),
                renderer = SystemInfo.graphicsDeviceType.ToString()
            };
            bool petPassed = pet.active && pet.transparent && pet.borderless && pet.topmost &&
                pet.width == 600 && pet.height == 500 && pet.cameraAlpha == 0 && pet.roots == 1 && pet.renderer == "Direct3D11";
            File.WriteAllText(Path.Combine(reportDirectory, "window_pet_ready.json"), JsonConvert.SerializeObject(pet, Formatting.Indented));
            // Allows the companion test script to verify composition over its own temporary backdrop.
            float captureDeadline = Time.realtimeSinceStartup + 25;
            while (!File.Exists(Path.Combine(reportDirectory, "window_capture_done.flag")) && Time.realtimeSinceStartup < captureDeadline)
                yield return null;

            bool sqlitePassed = false;
            string sqliteVersion = null;
            try
            {
                string databasePath = Path.Combine(reportDirectory, "player_messages.db");
                using (var database = new SqlitePhase0Store(databasePath))
                {
                    sqliteVersion = database.NativeVersion;
                    database.SaveMessage("phase0-player", "user", "京都 中文 🌙");
                }
                using (var database = new SqlitePhase0Store(databasePath))
                    sqlitePassed = database.Search("京都").Count > 0 && database.ReadMessages("phase0-player").Count > 0;
            }
            catch (Exception exception) { Debug.LogException(exception); }

            transition = root.Controller.SwitchModeAsync(CharacterMode.Home);
            while (!transition.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(2);
            var home = new { transparent = IsTransparent(), borderless = IsBorderless(), topmost = IsTopmost(),
                width = Screen.width, height = Screen.height, cameraAlpha = Camera.main ? Camera.main.backgroundColor.a : -1,
                mode = root.Character.CurrentMode.ToString(), switching = root.Controller.IsSwitching };
            bool restored = transition.IsCompleted && !transition.IsFaulted && !IsTransparent() && !IsBorderless() && !IsTopmost() &&
                Screen.width == 1280 && Screen.height == 720 && root.Character.CurrentMode == CharacterMode.Home && Camera.main && Camera.main.backgroundColor.a > .99f;
            bool passed = petPassed && restored && sqlitePassed;
            File.WriteAllText(Path.Combine(reportDirectory, "player_probe.json"), JsonConvert.SerializeObject(new {
                passed, petPassed, homeRestored = restored, sqlitePassed, sqliteVersion, pet, home,
                scope = "Windows Player native window state and SQLite persistence; desktop composition recorded separately."
            }, Formatting.Indented));
            Exit(passed ? 0 : 1);
        }

        private IEnumerator WaitForHome()
        {
            while (Time.realtimeSinceStartup < deadline)
            {
                var root = AppRoot.Instance;
                if (root && root.IsReady && !root.Controller.IsSwitching && SceneManager.GetActiveScene().path == AppController.ScenePath(CharacterMode.Home))
                    yield break;
                yield return null;
            }
        }

        private void Fail(string reason)
        {
            File.WriteAllText(Path.Combine(reportDirectory, "player_probe.json"), JsonConvert.SerializeObject(new { passed = false, reason }));
            Exit(1);
        }
        private async void Exit(int code)
        {
            if (AppRoot.Instance != null) await AppRoot.Instance.ShutdownAsync();
            Application.Quit(code);
        }
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsActive();
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsTransparent();
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsBorderless();
        [DllImport("LibUniWinC")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsTopmost();
#endif
    }
}
