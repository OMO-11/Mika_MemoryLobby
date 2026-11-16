using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Companion.Bgm;
using GiftCompanion.Core.Character;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GiftCompanion.Tests
{
    public sealed class MaintenanceScreenTests
    {
        [SetUp] public void Isolate() => PlayTestData.Begin();
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        static Slider Slider(RectTransform panel, string name) => panel.GetComponentsInChildren<Slider>(true).Single(s => s.name == name);
        static Button Button(RectTransform panel, string name) => panel.GetComponentsInChildren<Button>(true).Single(s => s.name == name);
        static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            float deadline = Time.realtimeSinceStartup + 40;
            while (AppRoot.Instance?.IsReady != true || AppRoot.Instance.Controller.IsSwitching || AppRoot.Instance.Character.CurrentMode != CharacterMode.Home)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.Companion));
            while (Object.FindFirstObjectByType<CompanionScreen>()?.IsReady != true)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
        [UnityTest] public IEnumerator UnsavedSoundAndDisplayRequireChoiceAndDiscardRestoresAllPreviews()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            screen.OpenSettings(); var sound = Field<RectTransform>(screen, "voicePanel");
            var music = Object.FindFirstObjectByType<BgmManager>(); var original = root.Config.Configuration.Copy();
            Slider(sound, "Volume").value = .13f; Slider(sound, "VoiceVolume").value = .24f;
            Assert.That(music.Volume, Is.EqualTo(.13f).Within(.001));
            Assert.That(screen.HandleWindowClose(),Is.True);Assert.That(screen.ConfirmationVisible,Is.True);Assert.That(root.Database.IsPersistent,Is.True);screen.Back();
            screen.OpenDisplaySettings(); Assert.That(screen.ActivePanel, Is.EqualTo("Voice")); Assert.That(screen.ConfirmationVisible, Is.True);
            screen.Back(); Assert.That(screen.ConfirmationVisible, Is.False); Assert.That(Slider(sound, "Volume").value, Is.EqualTo(.13f).Within(.001));
            screen.OpenDisplaySettings(); Button(Field<RectTransform>(screen, "editPrompt"), "Confirm").onClick.Invoke();
            Assert.That(screen.ActivePanel, Is.EqualTo("Display")); Assert.That(root.Config.Configuration.BgmVolume, Is.EqualTo(original.BgmVolume));
            Assert.That(music.Volume, Is.EqualTo(original.BgmVolume));
            var display = Field<RectTransform>(screen, "displayPanel"); Slider(display, "Exposure").value = .8f; Slider(display, "SubtitleSize").value = 34;
            Button(display, "ReduceMotion").onClick.Invoke();
            screen.Back(); Assert.That(screen.ConfirmationVisible, Is.True); Assert.That(root.Display.Value.Exposure, Is.Not.EqualTo(.8f));
            Button(Field<RectTransform>(screen, "editPrompt"), "Confirm").onClick.Invoke();
            Assert.That(screen.ActivePanel, Is.Null); Assert.That(Object.FindFirstObjectByType<CompanionTone>().exposure, Is.EqualTo(root.Display.Value.Exposure));
            Assert.That(Field<TMP_Text>(screen, "subtitle").fontSize, Is.EqualTo(original.SubtitleFontSize));
            Assert.That(screen.WindowView.ReduceMotion, Is.EqualTo(root.VisualSettings.Value.ReduceMotion));
        }
        [UnityTest] public IEnumerator FailedDisplaySaveRollsBackEarlierFileAndKeepsDraftForRetry()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            screen.OpenDisplaySettings(); var display = Field<RectTransform>(screen, "displayPanel");
            var before = root.Config.Configuration.Copy(); var beforeDisplay = root.Display.Value.Copy();
            Slider(display, "Exposure").value = .82f; Slider(display, "SubtitleSize").value = 33;
            Task save;
            using (var locked = new FileStream(root.Display.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                save = (Task)typeof(CompanionScreen).GetMethod("SaveDisplayDraftAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
                yield return Await(save);
                Assert.That(root.Config.Configuration.SubtitleFontSize, Is.EqualTo(before.SubtitleFontSize));
                Assert.That(root.Display.Value.Exposure, Is.EqualTo(beforeDisplay.Exposure));
                Assert.That(Field<TMP_Text>(screen, "displayNote").text, Does.Contain("没能保存"));
                Assert.That(Slider(display, "SubtitleSize").value, Is.EqualTo(33));
            }
            save = (Task)typeof(CompanionScreen).GetMethod("SaveDisplayDraftAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null); yield return Await(save);
            Assert.That(root.Config.Configuration.SubtitleFontSize, Is.EqualTo(33)); Assert.That(root.Display.Value.Exposure, Is.EqualTo(.82f).Within(.001));
            screen.Back(); Assert.That(screen.ConfirmationVisible, Is.False); Assert.That(screen.ActivePanel, Is.Null);
        }
        static IEnumerator Await(Task task) { while (!task.IsCompleted) yield return null; if (task.IsFaulted) throw task.Exception; Assert.That(task.IsCanceled, Is.False); }
        [UnityTearDown] public IEnumerator End() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; PlayTestData.End(); }
    }
}
