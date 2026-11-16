using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Persistence;
using GiftCompanion.SharedUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GiftCompanion.Tests
{
    public sealed class SceneLifecycleTests
    {
        [SetUp] public void IsolateData() => PlayTestData.Begin();
        [UnityTest] public IEnumerator DefaultStartupOpensCompanionAndClosedPetCannotBeEntered()
        {
            System.IO.File.Delete(System.IO.Path.Combine(AppRoot.EditorValidationDataPath, "Config", "app_entry.json"));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            float until = UnityEngine.Time.realtimeSinceStartup + 20;
            while (AppRoot.Instance == null || !AppRoot.Instance.IsReady || AppRoot.Instance.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Companion")
            { Assert.That(UnityEngine.Time.realtimeSinceStartup, Is.LessThan(until)); yield return null; }
            var root = AppRoot.Instance; var core = root.Character;
            Assert.That(root.VisualSettings, Is.Not.Null);
            Assert.That(root.VisualSettings.Value.FindSpace("mika-memory"), Is.Not.Null);
            Assert.That(System.IO.File.Exists(root.VisualSettings.FilePath), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => root.Controller.SwitchModeAsync(CharacterMode.AgentPet));
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Home)); yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "AgentPet" || b.name == "ResetPet"), Is.False);
            Assert.That(root.Character, Is.SameAs(core));
        }
        [UnityTest]public IEnumerator DevelopmentPanelRequiresExplicitEnablement()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");yield return UntilReady();
            var panel=AppRoot.Instance.GetComponent<GiftCompanion.Validation.Phase2DevelopmentPanel>();
            Assert.That(panel,Is.Not.Null);Assert.That(panel.enabled,Is.False);
            panel.Open();yield return null;Assert.That(panel.enabled,Is.True);
            panel.enabled=false;
        }
        [UnityTest] public IEnumerator MaintenanceExcludesSceneSwitchUntilTheSnapshotOperationFinishes()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity"); yield return UntilReady();
            var root = AppRoot.Instance; var entered = new TaskCompletionSource<bool>(); var release = new TaskCompletionSource<bool>();
            var work = root.MaintenanceAsync(async ct => { entered.TrySetResult(true); await release.Task; return true; }, root.ApplicationToken);
            yield return Await(entered.Task);
            Assert.That(root.PlayerWorkBusy, Is.True);
            try { Assert.Throws<System.InvalidOperationException>(() => root.Controller.SwitchModeAsync(CharacterMode.AgentPet)); }
            finally { release.TrySetResult(true); }
            yield return Await(work); Assert.That(root.PlayerWorkBusy, Is.False);
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Companion));
        }
        [UnityTest]
        public IEnumerator RepeatedSwitchesKeepOneRootAndTheSameSharedServices()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            yield return UntilReady();
            var root = AppRoot.Instance;
            var character = root.Character;
            var database = root.Database;
            var memory = root.Memory;
            var llm = root.LLM;
            var state = character.State;
            state.CurrentTopic = "shared across bodies";

            for (int cycle = 0; cycle < 3; cycle++)
            {
                var sequence = new[] { CharacterMode.Companion, CharacterMode.Home,
                    CharacterMode.AgentPet, CharacterMode.Home };
                foreach (var mode in sequence)
                {
                    Task switchTask = root.Controller.SwitchModeAsync(mode);
                    yield return Await(switchTask);
                    yield return null; // allow destroyed scene objects and Start callbacks to settle
                    Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppController.ScenePath(mode)));
                    Assert.That(character.CurrentMode, Is.EqualTo(mode));
                    Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    Assert.That(Object.FindObjectsByType<ModeSceneController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                    Assert.That(root.Character, Is.SameAs(character));
                    Assert.That(root.Database, Is.SameAs(database));
                    Assert.That(root.Memory, Is.SameAs(memory));
                    Assert.That(root.LLM, Is.SameAs(llm));
                    Assert.That(character.State, Is.SameAs(state));
                    Assert.That(state.CurrentTopic, Is.EqualTo("shared across bodies"));
                }
            }

            // Reloading Bootstrap must reuse the existing root/core.
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            yield return UntilReady();
            Assert.That(AppRoot.Instance, Is.SameAs(root));
            Assert.That(AppRoot.Instance.Character, Is.SameAs(character));

            Object.Instantiate(root.gameObject);
            yield return null;
            Assert.That(AppRoot.Instance, Is.SameAs(root));
            Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

            var oldModeToken = root.Controller.ActiveModeToken;
            var first = root.Controller.SwitchModeAsync(CharacterMode.Companion);
            var repeatedClick = root.Controller.SwitchModeAsync(CharacterMode.AgentPet);
            yield return Await(first);
            yield return Await(repeatedClick);
            Assert.That(character.CurrentMode, Is.EqualTo(CharacterMode.Companion));
            Assert.That(oldModeToken.IsCancellationRequested, Is.True);
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Home));

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsAsync(Is.InstanceOf<System.OperationCanceledException>(), () =>
                    root.Controller.SwitchModeAsync(CharacterMode.Companion, cancellation.Token));
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppController.ScenePath(CharacterMode.Home)));
            }

            // Test the persistent button wiring used by the development UI itself.
            FindButton("Companion").onClick.Invoke();
            yield return WaitForMode(CharacterMode.Companion);
            var companionScreen=Object.FindFirstObjectByType<GiftCompanion.Companion.CompanionScreen>();
            while(companionScreen.WindowView==null)yield return null;
            companionScreen.OpenDataSettings();
            yield return new WaitForSeconds(.3f);
            FindButton("Home").onClick.Invoke();
            yield return WaitForMode(CharacterMode.Home);
            while(Object.FindFirstObjectByType<GiftCompanion.SharedUI.PlayerMaintenance>()==null)yield return null;
            yield return null;Object.FindFirstObjectByType<GiftCompanion.SharedUI.PlayerMaintenance>().SendMessage("Close");yield return null;
            FindButton("AgentPet").onClick.Invoke();
            yield return WaitForMode(CharacterMode.AgentPet);
            Object.FindFirstObjectByType<GiftCompanion.AgentPet.PetScreen>().OpenMenu();
            FindButton("ReturnHome").onClick.Invoke();
            yield return WaitForMode(CharacterMode.Home);
        }

        [UnityTest]
        public IEnumerator EnteringAModeSceneDirectlyBootstrapsSafely()
        {
            if (AppRoot.Instance != null)
            {
                Object.Destroy(AppRoot.Instance.gameObject);
                yield return null;
            }
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/AgentPet.unity");
            yield return UntilReady();
            Assert.That(AppRoot.Instance.Character.CurrentMode, Is.EqualTo(CharacterMode.Home));
            Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
            PlayTestData.End();
            SceneManager.CreateScene("TestCleanup");
            foreach (var controller in Object.FindObjectsByType<ModeSceneController>(FindObjectsSortMode.None))
                Object.Destroy(controller.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadFailureRecoversHomeAndShutdownSavesBeforeReleasingServices()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            yield return UntilReady();
            var root = AppRoot.Instance;
            var core = root.Character;
            root.Controller.SceneLoaderOverride = path => path == AppController.ScenePath(CharacterMode.Companion)
                ? Task.FromException(new System.IO.IOException("simulated load failure"))
                : LoadForTest(path);
            var failure = root.Controller.SwitchModeAsync(CharacterMode.Companion);
            while (!failure.IsCompleted) yield return null;
            Assert.That(failure.IsFaulted, Is.True);
            Assert.That(core.CurrentMode, Is.EqualTo(CharacterMode.Home));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Home"));
            root.Controller.SceneLoaderOverride = null;
            core.State.CurrentTopic = "shutdown checkpoint";
            string path = System.IO.Path.Combine(root.Config.UserDataPath, "memory.db");
            string id = core.Identity.Id;
            yield return Await(root.ShutdownAsync());
            Assert.That(root.ShutdownSucceeded, Is.True);
            Assert.That(root.IsReady, Is.False);
            Assert.That(root.Database.IsPersistent, Is.False);
            using var reopened = new DatabaseService(path, id);
            yield return Await(reopened.InitializeAsync(default));
            yield return Await(reopened.MigrateAsync(default));
            var loaded = reopened.LoadCharacterStateAsync(default);
            yield return Await(loaded);
            Assert.That(loaded.Result.CurrentTopic, Is.EqualTo("shutdown checkpoint"));
            Assert.That(loaded.Result.LastMode, Is.EqualTo(CharacterMode.Home));
        }
        private static async Task LoadForTest(string path)
        {
            var operation = SceneManager.LoadSceneAsync(path);
            while (!operation.isDone) await Task.Yield();
        }

        [UnityTest] public IEnumerator SwitchingCancelsAModeBoundRequestBeforeItCanComplete()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            yield return UntilReady();
            var root = AppRoot.Instance;
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Companion));
            var blocked = new BlockingLLM();
            var core = new CharacterCore(root.Character.Identity, new CharacterState(), root.Database, blocked, root.Memory);
            var request = core.SendMessageAsync("test", InteractionSource.Companion, root.Controller.ActiveModeToken);
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Home));
            yield return Await(request);
            Assert.That(request.Result.ErrorCode, Is.EqualTo("Canceled"));
            Assert.That(core.State.LastInteractionAt, Is.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Home"));
        }
        private sealed class BlockingLLM : GiftCompanion.Core.LLM.ILLMService
        {
            public bool IsConfigured => true;
            public async System.Collections.Generic.IAsyncEnumerable<GiftCompanion.Core.LLM.LLMChunk> StreamDialogueAsync(
                GiftCompanion.Core.LLM.DialogueRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                await Task.Delay(Timeout.Infinite, ct);
                yield return new GiftCompanion.Core.LLM.LLMChunk("must not reach the next scene");
            }
        }

        private static Button FindButton(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.gameObject.name == name);

        private static IEnumerator UntilReady()
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (AppRoot.Instance == null || !AppRoot.Instance.IsReady ||
                   AppRoot.Instance.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Home")
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Bootstrap timed out.");
                yield return null;
            }
        }

        private static IEnumerator Await(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!task.IsCompleted)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Scene transition timed out.");
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }

        private static IEnumerator WaitForMode(CharacterMode mode)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (AppRoot.Instance.Controller.IsSwitching || SceneManager.GetActiveScene().path != AppController.ScenePath(mode))
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Button navigation timed out.");
                yield return null;
            }
            yield return null;
        }
    }
}
