using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.AgentPet;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GiftCompanion.Tests
{
    public sealed class CharacterPlaybackTests
    {
        [SetUp] public void IsolateData() => PlayTestData.Begin();
        [UnityTest]
        public IEnumerator PetPlaysOriginalClipsAndReleasesAnInterruptedTouch()
        {
            yield return Boot();
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.AgentPet));
            yield return null;
            var view = Object.FindFirstObjectByType<PetPresentation>();
            Assert.That(view, Is.Not.Null);
            var body = view.Body;
            Assert.That(body.animator.applyRootMotion, Is.False);
            var map = JsonUtility.FromJson<CHDDDBodyControllerV2.ActionMap>(body.actionMap.text);
            var clips = body.animator.runtimeAnimatorController.animationClips;
            Assert.That(map.actions.Length, Is.EqualTo(18));
            foreach (var entry in map.actions)
            {
                Assert.That(body.animator.HasState(0, Animator.StringToHash(entry.clip)), Is.True, entry.clip);
                Assert.That(body.animator.HasState(1, Animator.StringToHash(entry.faceClip)), Is.True, entry.faceClip);
                var clip = clips.First(c => c.name == entry.clip);
                Assert.That(clip.length, Is.EqualTo(entry.durationSeconds).Within(.04f), entry.clip);
            }
            body.animator.Play("PetV2_Walk", 0, 0);
            body.animator.Update(0);
            var transforms = body.GetComponentsInChildren<Transform>();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            body.animator.Update(.25f);
            Assert.That(transforms.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > .1f).Any(), Is.True,
                "The source walk must actually animate bones.");
            body.PlayAction("HeadPat", 0);
            body.animator.Update(.1f);
            Assert.That(body.animator.GetCurrentAnimatorStateInfo(0).IsName("PetV2_HeadPat_Enter"), Is.True);
            view.ReleaseInteraction();
            body.animator.Update(.3f);
            var state = body.animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.IsName("PetV2_HeadPat_Exit"), Is.True, "Release during Enter must use Exit.");
            body.animator.Update(1.5f);
            body.animator.Update(.3f);
            Assert.That(body.animator.GetCurrentAnimatorStateInfo(0).IsName("PetV2_Idle"), Is.True);
            Assert.That(view.PlayAction("PetV2_Dragging_Enter"), Is.True); // internal names safely fall back
            body.animator.Update(.5f);
            Assert.That(body.animator.GetCurrentAnimatorStateInfo(0).IsName("PetV2_Idle"), Is.True);
            Capture("pet.png");
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.Home));
            yield return null;
            Assert.That(view == null, Is.True);
        }

        [UnityTest]
        public IEnumerator SpineLoadsTheFullLobbyAndKeepsOneIdleTrack()
        {
            yield return Boot();
            var core = AppRoot.Instance.Character;
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.Companion));
            yield return null;
            var view = Object.FindFirstObjectByType<CompanionPresentation>();
            Assert.That(view, Is.Not.Null);
            var loader = view.Loader;
            Assert.That(loader.Character, Is.Not.Null);
            Assert.That(loader.enabled, Is.False);
            Assert.That(loader.Pat10, Is.Null);
            var skeleton = loader.Character.Skeleton;
            Assert.That(skeleton.Bones.Count, Is.EqualTo(743));
            Assert.That(skeleton.Slots.Count, Is.EqualTo(294));
            Assert.That(skeleton.Data.Animations.Count, Is.EqualTo(52));
            Assert.That(loader.IsPlayingIntro,Is.True);
            Assert.That(loader.Character.AnimationState.GetCurrent(0).Animation.Name,Is.EqualTo("Start_Idle_01"));
            loader.SkipIntro();yield return null;
            var idle = loader.Character.AnimationState.GetCurrent(0);
            Assert.That(idle.Animation.Name, Is.EqualTo("Idle_01"));
            view.SetThinking(true);
            yield return null;
            view.SetThinking(false);
            Assert.That(loader.Character.AnimationState.GetCurrent(0), Is.SameAs(idle), "Thinking must not restart the base Idle.");
            Assert.That(loader.Expression.BeginTouch(), Is.True);
            view.CancelInteraction();
            Assert.That(loader.Expression.TouchActive, Is.False);
            loader.Character.Update(1f);
            yield return null;
            Assert.That(idle.TrackTime, Is.GreaterThan(0));
            Assert.That(loader.Character.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            Capture("companion.png");
            var character = loader.Character;
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.Home));
            yield return null;
            Assert.That(character == null, Is.True);
            Assert.That(AppRoot.Instance.Character, Is.SameAs(core));
        }

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            float deadline = Time.realtimeSinceStartup + 15;
            while (AppRoot.Instance == null || !AppRoot.Instance.IsReady || AppRoot.Instance.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Home")
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
            PlayTestData.End();
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception;
            Assert.That(task.IsCanceled, Is.False);
        }

        private static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var camera = Object.FindFirstObjectByType<Camera>();
            var previous = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Validation/Phase0", name));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(image);
                Object.Destroy(target);
            }
        }
    }
}
