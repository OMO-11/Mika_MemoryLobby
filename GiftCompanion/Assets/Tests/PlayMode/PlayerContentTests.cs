using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Companion.Bgm;
using GiftCompanion.Core.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GiftCompanion.Tests
{
    public sealed class PlayerContentTests
    {
        [SetUp] public void Isolate() => PlayTestData.Begin();
        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            float deadline = Time.realtimeSinceStartup + 60;
            while (AppRoot.Instance?.IsReady != true || AppRoot.Instance.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Home")
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.Companion));
            while (Object.FindFirstObjectByType<CompanionScreen>()?.IsReady != true)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
        [UnityTest] public IEnumerator PlayerCardAppliesWithoutReplacingCoreAndImportedSpaceKeepsSharedState()
        {
            yield return Boot(); var root = AppRoot.Instance; var core = root.Character; var state = core.State;
            yield return Await(root.SaveCharacterCardAsync(new CharacterIdentity(core.Identity.Id, "未花", "修改后的语气", "老师"), default));
            Assert.That(core.Identity.Persona, Is.EqualTo("修改后的语气")); Assert.That(root.PlayerWorkBusy, Is.False);
            var screen = Object.FindFirstObjectByType<CompanionScreen>(); screen.OpenSpaces();
            var import = root.Library.ImportSpaceAsync(System.IO.Path.Combine(Application.streamingAssetsPath, "CH0069_Source"), CompanionPresentation.ValidateSpaceRuntime, default); yield return Await(import);
            yield return Await(screen.SelectSpaceAsync(import.Result.Id)); yield return null;
            var visual = Object.FindFirstObjectByType<CompanionPresentation>();
            Assert.That(visual.ActiveSpaceId, Is.EqualTo(import.Result.Id)); Assert.That(visual.Loader.Character.Skeleton.Bones.Count, Is.EqualTo(743));
            Assert.That(visual.Loader.Motion, Is.Null, "Unadapted packages get safe base Idle, not invented gestures.");
            Assert.That(root.Character, Is.SameAs(core)); Assert.That(core.State, Is.SameAs(state));
            yield return Await(screen.SelectSpaceAsync(root.Library.Spaces.Spaces.Single(x => x.Id == "mika-memory").Id)); yield return null;
            Assert.That(visual.Loader.Motion, Is.Not.Null); screen.OpenLibrary(); Assert.That(screen.ActivePanel, Is.EqualTo("Library"));
        }
        [UnityTest] public IEnumerator MultipleSongsStayOnSelectedTrackAndCrossfadeNeverExceedsUserVolume()
        {
            yield return Boot(); var player = Object.FindFirstObjectByType<BgmManager>();
            var a = AudioClip.Create("loop A", 19200, 1, 48000, false); var b = AudioClip.Create("loop B", 24000, 1, 48000, false);
            player.FadeSeconds = .2f; player.Configure(new[] {a,b}, .2f, false);
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until) { Assert.That(player.GetComponents<AudioSource>().Sum(s => s.volume), Is.LessThanOrEqualTo(.201f)); yield return null; }
            Assert.That(player.RepeatCount, Is.GreaterThan(1)); Assert.That(player.CurrentTrackIndex, Is.EqualTo(0)); Assert.That(player.Source.clip, Is.SameAs(a));
            player.SelectTrack(1); until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until) { Assert.That(player.GetComponents<AudioSource>().Sum(s => s.volume), Is.LessThanOrEqualTo(.201f)); yield return null; }
            Assert.That(player.CurrentTrackIndex, Is.EqualTo(1)); Assert.That(player.Source.clip, Is.SameAs(b));
            yield return Await(player.FadeOutAsync(default)); Assert.That(player.GetComponents<AudioSource>().All(s => !s.isPlaying), Is.True);
            Object.Destroy(a); Object.Destroy(b);
        }
        private static IEnumerator Await(Task task) { while (!task.IsCompleted) yield return null; if (task.IsFaulted) throw task.Exception; Assert.That(task.IsCanceled, Is.False); }
        [UnityTearDown] public IEnumerator End() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; PlayTestData.End(); }
    }
}
