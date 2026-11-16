using System;
using System.Linq;
using GiftCompanion.AgentPet.Window;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GiftCompanion.AgentPet
{
    public sealed partial class PetScreen
    {
        private readonly PetGesture gesture = new PetGesture();
        private PetPresentationSettings presentation = new PetPresentationSettings();
        private Collider headHit, bodyHit;
        private Transform headBone;
        private Quaternion gaze = Quaternion.identity;
        private Vector2 cursorOrigin, windowOrigin;
        private bool wasDown, previousBackground;
        private float reactionUntil;
        public event Action<PetGestureEvent> Interaction;
        private InputSettings.BackgroundBehavior previousInputBackground;
        partial void BuildPetInteraction()
        {
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            previousInputBackground = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try { presentation = PetPresentationSettings.Load(root.Config.UserDataPath, root.ApplicationToken); }
            catch (Exception error) { root.Logger.Error("pet presentation invalid original_preserved=true", error); }
            var renderers = visual.Body.GetComponentsInChildren<SkinnedMeshRenderer>(); if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            headBone = visual.Body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == presentation.HeadBone);
            if (headBone != null)
            {
                var head = new GameObject("Pet Head Hit"); head.transform.position = headBone.position; head.transform.SetParent(headBone, true);
                var sphere = head.AddComponent<SphereCollider>(); sphere.radius = bounds.size.y * presentation.HeadRadiusHeightRatio; sphere.isTrigger = true; headHit = sphere;
            }
            var body = new GameObject("Pet Body Hit"); body.transform.position = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * .42f, bounds.center.z); body.transform.SetParent(visual.Body.transform, true);
            var box = body.AddComponent<BoxCollider>(); box.size = new Vector3(bounds.size.x * .5f, bounds.size.y * .4f, Mathf.Max(.1f,bounds.size.z)); box.isTrigger = true; bodyHit = box;
        }
        public bool HitPet(Vector2 point, out bool head)
        {
            head = false; if (Camera.main == null) return false;
            var ray = Camera.main.ScreenPointToRay(point);
            if (headHit != null && headHit.Raycast(ray, out _, 10000)) { head = true; return true; }
            return bodyHit != null && bodyHit.Raycast(ray, out _, 10000);
        }
        private Vector2 CursorPoint()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return PetNative.ClientCursor();
#else
            return Mouse.current?.position.ReadValue() ?? Vector2.zero;
#endif
        }
        partial void UpdatePetInteraction()
        {
            if (headBone != null) headBone.localRotation *= Quaternion.Inverse(gaze); // remove previous procedural offset before animation sampling
            Vector2 point = CursorPoint(); bool down = Mouse.current?.leftButton.isPressed == true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            down = PetNative.LeftDown;
#endif
            if (down && !wasDown && !PointerOnBubble() && !editing && HitPet(point, out bool head))
            {
                bool ourWindow = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                ourWindow = PetNative.PointerIsOurs();
#endif
                if (ourWindow) { cursorOrigin = point;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                    var cursor = PetNative.Cursor; cursorOrigin = new Vector2(cursor.X,cursor.Y); var bounds = PetNative.Bounds; windowOrigin = new Vector2(bounds.Left,bounds.Top);
#endif
                    gesture.Begin(head, cursorOrigin.x, cursorOrigin.y, Time.unscaledTime);
                }
            }
            if (down && gesture.IsHeld)
            {
                Vector2 move = point;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var cursor = PetNative.Cursor; move = new Vector2(cursor.X,cursor.Y);
#endif
                // Begin uses the same coordinate system as held movement.
                var signal = gesture.Move(move.x, move.y, Time.unscaledTime, root.PetSettings.Value.DragPixels, root.PetSettings.Value.LongPressSeconds);
                if (signal != PetGestureEvent.None && signal != PetGestureEvent.DragMove) Interaction?.Invoke(signal);
                if (signal == PetGestureEvent.DragStart) { visual.ReleaseInteraction(); Play("Dragging"); SetDragging(true); host.SetPointerCaptured(true); }
                if (signal == PetGestureEvent.DragStart || signal == PetGestureEvent.DragMove)
                {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                    var delta = move - cursorOrigin; PetNative.Move((int)(windowOrigin.x + delta.x), (int)(windowOrigin.y + delta.y));
#endif
                }
                if (signal == PetGestureEvent.HeadPatStart) { Play("HeadPat"); lastActivity = Time.unscaledTime; }
            }
            if (!down && wasDown) FinishGesture(false);
            wasDown = down;
            if (Time.unscaledTime > reactionUntil && reactionUntil > 0 && !gesture.IsHeld) { reactionUntil = 0; if (!turn.IsCompleted) Play(taskAction); else visual.ReleaseInteraction(); }
        }
        private void Play(string logical) => visual.PlayAction(presentation.Actions.TryGetValue(logical, out var mapped) ? mapped : "Idle");
        private void FinishGesture(bool cancelled)
        {
            var signal = gesture.Release(cancelled);
            if (signal != PetGestureEvent.None) Interaction?.Invoke(signal);
            if (signal == PetGestureEvent.DragEnd) { host.SetPointerCaptured(false);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                PetNative.Clamp(false);
#endif
                SetDragging(false); if (!turn.IsCompleted) Play(taskAction); else visual.ReleaseInteraction(); }
            else if (signal == PetGestureEvent.HeadPatEnd) { if (!turn.IsCompleted) Play(taskAction); else visual.ReleaseInteraction(); }
            else if (signal == PetGestureEvent.HeadClick || signal == PetGestureEvent.BodyClick)
            { Play(signal.ToString()); reactionUntil = Time.unscaledTime + 1.8f; if (!open) Say("老师，今天需要我做什么？", 12); }
        }
        partial void ReleasePetInteraction() { FinishGesture(true); wasDown = false; }
        private void LateUpdate()
        {
            if (headBone == null || !Ready) return;
            var point = CursorPoint(); var origin = Camera.main.WorldToScreenPoint(headBone.position);
            bool follow = !Dragging && !gesture.IsHeld && visual.Body.CurrentAction == "Idle";
            var desired = follow ? Quaternion.Euler(Mathf.Clamp((point.y-origin.y)/Screen.height,-1,1)*presentation.LookPitch,
                -Mathf.Clamp((point.x-origin.x)/Screen.width,-1,1)*presentation.LookYaw,0) : Quaternion.identity;
            gaze = Quaternion.Slerp(gaze, desired, Time.unscaledDeltaTime*presentation.LookSpeed); headBone.localRotation *= gaze;
        }
    }
}
