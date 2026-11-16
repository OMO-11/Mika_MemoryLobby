using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Kirurobo;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.AgentPet.Window
{
    // The existing Unity window changes shape; no second chat window or character core.
    public sealed class DesktopWindowPrototype : MonoBehaviour
    {
        public event Action InputRequested;
        public bool IsPetActive { get; private set; }
        public string HotkeyIssue { get; private set; }
        public void RequestInput() => InputRequested?.Invoke();
        public void ApplyTopmost(bool value)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IsPetActive) window.isTopmost = value;
#endif
        }
        public void SetInputFocus(bool active)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IsPetActive) { window.isHitTestEnabled = false; window.isClickThrough = false; PetNative.InputFocus(active); }
#endif
        }
        public void SetPointerCaptured(bool captured)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            pointerCaptured = captured; if (IsPetActive) { window.isHitTestEnabled = false; window.isClickThrough = false; }
#endif
        }
        public void ResetPosition()
        {
            var root = Bootstrap.AppRoot.Instance; if (root?.PetSettings == null) return;
            var value = root.PetSettings.Value.Copy(); value.HasPosition = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IsPetActive) PetNative.Clamp(true);
#endif
            root.PetSettings.Save(value, root.ApplicationToken);
        }
        public void SavePosition()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var root = Bootstrap.AppRoot.Instance; if (!IsPetActive || root?.PetSettings == null || root.ApplicationToken.IsCancellationRequested) return;
            var rect = PetNative.Bounds; var value = root.PetSettings.Value.Copy(); value.HasPosition = true; value.Left = rect.Left; value.Top = rect.Top;
            root.PetSettings.Save(value, root.ApplicationToken);
#endif
        }
        public async Task<bool> ChangeHotkeyAsync(string value, CancellationToken ct)
        {
            if (!PetHotkey.TryParse(value, out var parsed)) { HotkeyIssue = "请使用Ctrl或Alt搭配字母、空格或F1–F11。"; return false; }
            var root = Bootstrap.AppRoot.Instance; string previous = root.Config.Configuration.PetHotkey;
            bool equivalent = PetHotkey.TryParse(previous, out var before) && before.Modifiers == parsed.Modifiers && before.Key == parsed.Key && HotkeyIssue == null;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (IsPetActive && !equivalent && !await hotkeys.RegisterAsync(parsed)) { HotkeyIssue = "这个快捷键已被占用，原快捷键仍保留。"; return false; }
#endif
            try { var next = root.Config.Configuration.Copy(); next.PetHotkey = value; await root.Config.SaveAsync(next, ct); HotkeyIssue = null; return true; }
            catch
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                PetHotkey.TryParse(previous, out var old); await hotkeys.RegisterAsync(IsPetActive ? old : (PetHotkey?)null);
#endif
                throw;
            }
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private UniWindowController window;
        private Coroutine transition;
        private PetHotkeyHost hotkeys;
        private PetNative.Rect homeBounds;
        private bool hasHomeBounds;
        private bool pointerCaptured;
        private Texture2D hitPixel;
        private void Awake()
        {
            hotkeys = new PetHotkeyHost();
            window = gameObject.AddComponent<UniWindowController>();
            window.forceWindowed = true;
            window.SetTransparentType(UniWindowController.TransparentType.Alpha);
            // Use the same Win32 client coordinates as gesture input, including after a DPI change.
            window.hitTestType = UniWindowController.HitTestType.None;
            window.opacityThreshold = .1f;
            window.isHitTestEnabled = false;
            hitPixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            StartCoroutine(HitTest());
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsPetActive) { try { SavePosition(); } catch (Exception error) { Bootstrap.AppRoot.Instance?.Logger?.Error("pet position save failed", error); } }
            IsPetActive = false; pointerCaptured = false; _ = hotkeys.RegisterAsync(null);
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(ApplyScene(scene));
        }

        private IEnumerator ApplyScene(Scene scene)
        {
            bool pet = scene.path == Bootstrap.AppController.ScenePath(CharacterMode.AgentPet);
            var root = Bootstrap.AppRoot.Instance; var settings = root?.PetSettings?.Value ?? new PetSettings();
            if (pet) { homeBounds = PetNative.Bounds; hasHomeBounds = true; }
            // Restore the old camera before selecting the new scene camera.
            window.isTransparent = false;
            window.isHitTestEnabled = false;
            window.isClickThrough = false;
            window.SetCamera(Camera.main);
            Screen.SetResolution(pet ? settings.Width : 1280, pet ? settings.Height : 720, FullScreenMode.Windowed);
            // Unity applies resolution at the end of the frame. Change native styles afterwards.
            yield return null;
            yield return null;
            window.isTopmost = pet && settings.Topmost;
            window.isTransparent = pet;
            yield return null;
            PetNative.SetClientSize(pet ? settings.Width : 1280, pet ? settings.Height : 720);
            yield return null;
            window.isHitTestEnabled = false;
            PetNative.InputFocus(!pet);
            if (pet)
            {
                if (settings.HasPosition) PetNative.Move(settings.Left, settings.Top);
                PetNative.Clamp(!settings.HasPosition); IsPetActive = true;
                PetHotkey.TryParse(root.Config.Configuration.PetHotkey, out var parsed);
                var registration = hotkeys.RegisterAsync(parsed); while (!registration.IsCompleted) yield return null;
                HotkeyIssue = registration.Result ? null : "快捷键暂时被占用，可以点击我，或在设置中更换。";
            }
            else { if (hasHomeBounds) PetNative.Move(homeBounds.Left, homeBounds.Top); PetNative.Clamp(false); }
            transition = null;
        }
        private void Update() { if (IsPetActive && hotkeys.Consume()) InputRequested?.Invoke(); }
        private IEnumerator HitTest()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (!IsPetActive) continue;
                if (pointerCaptured) { window.isClickThrough = false; continue; }
                Vector2 point = PetNative.ClientCursor(); bool opaque = false;
                if (point.x >= 0 && point.y >= 0 && point.x < Screen.width && point.y < Screen.height)
                {
                    hitPixel.ReadPixels(new Rect((int)point.x, (int)point.y, 1, 1), 0, 0, false);
                    opaque = hitPixel.GetPixel(0, 0).a >= .1f;
                }
                window.isClickThrough = !opaque;
            }
        }
        private void OnApplicationQuit() { hotkeys?.Dispose(); }
        private void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; hotkeys?.Dispose(); if (hitPixel != null) Destroy(hitPixel); }
#endif
    }
}
