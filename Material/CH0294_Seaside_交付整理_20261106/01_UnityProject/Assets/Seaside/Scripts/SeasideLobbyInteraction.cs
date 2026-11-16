using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mika.Seaside
{
    public sealed partial class SeasideLobbyController
    {
        public enum IdleExpression { None, PlayerGlance, ClosedEyeSmile, ThoughtfulLook }
        private enum PatPhase { None, Feedback, Ending }

        // Head_Rot's local X points towards the crown; local Y runs across the face.
        // This rounded region covers the forehead and crown, excluding the eyes/cheeks.
        [SerializeField] private Rect headPatArea = new Rect(2.4f, -3.3f, 4.5f, 6.6f);
        [SerializeField, Min(0f)] private float minimumPatFeedback = 0.35f;
        [SerializeField, Min(0f)] private float pointerExitGrace = 0.14f;
        [SerializeField] private bool enableIdleExpressions = true;
        [SerializeField] private Vector2 idleExpressionInterval = new Vector2(8f, 13f);

        private PatPhase patPhase;
        private float patElapsed;
        private bool patReleaseRequested;
        private bool pointerCaptured;
        private bool pointerHeldLastFrame;
        private float outsideHeadTime;
        private IdleExpression expression;
        private float expressionAge;
        private float expressionDuration;
        private float expressionWeight;
        private bool expressionEnding;
        private float idleDelay = 8f;
        private int expressionSequence;
        private uint randomState = 29417;
        private Skeleton expressionSkeleton;
        private readonly List<BoneDelta> expressionBones = new List<BoneDelta>();
        private bool offsetsApplied;

        public bool IsEntrancePlaying => entranceTransitionActive || Graphic.AnimationState.GetCurrent(0)?.Animation.Name == "Start_Idle_01";
        public bool IsPatting => patPhase != PatPhase.None;
        public bool IsPatFeedbackPlaying => patPhase == PatPhase.Feedback;
        public bool IsPatEnding => patPhase == PatPhase.Ending;
        public bool PlayEntranceOnStart => playEntranceOnStart;
        public IdleExpression CurrentIdleExpression => expression;
        public float IdleExpressionWeight => expressionWeight;
        public Rect HeadPatArea => headPatArea;
        public bool AmbientExpressionsEnabled { get => enableIdleExpressions; set => enableIdleExpressions = value; }

        private sealed class BoneDelta
        {
            public Bone bone;
            public float x, y, rotation;
            public void Apply() { bone.X += x; bone.Y += y; bone.Rotation += rotation; }
            public void Remove() { bone.X -= x; bone.Y -= y; bone.Rotation -= rotation; }
        }

        private void EnableInteractionHooks()
        {
            Graphic.BeforeApply += RemoveExpressionOffsets;
            Graphic.UpdateLocal += ApplyExpressionOffsets;
        }

        private void DisableInteractionHooks()
        {
            Graphic.BeforeApply -= RemoveExpressionOffsets;
            Graphic.UpdateLocal -= ApplyExpressionOffsets;
            RemoveExpressionOffsets(Graphic);
            pointerCaptured = false;
            pointerHeldLastFrame = false;
        }

        private void ResetInteractiveState()
        {
            RemoveExpressionOffsets(Graphic);
            patPhase = PatPhase.None;
            patElapsed = 0;
            patReleaseRequested = false;
            pointerCaptured = false;
            pointerHeldLastFrame = false;
            outsideHeadTime = 0;
            expression = IdleExpression.None;
            expressionWeight = 0;
            expressionEnding = false;
            idleDelay = NextExpressionDelay();
        }

        private float NextExpressionDelay()
        {
            randomState = randomState * 1664525u + 1013904223u;
            float min = Mathf.Max(3f, idleExpressionInterval.x);
            return Mathf.Lerp(min, Mathf.Max(min, idleExpressionInterval.y), (randomState & 65535) / 65535f);
        }

        private void CancelInteractiveOverlay()
        {
            patPhase = PatPhase.None;
            pointerCaptured = false;
            patReleaseRequested = false;
            StopIdleExpression();
            idleDelay = NextExpressionDelay();
        }

        /// <summary>Start the native feedback once and hold its last pose, without a reset or a short-loop seam.</summary>
        public bool BeginPatFeedback()
        {
            AttachEvents();
            if (subscribedState == null || IsEntrancePlaying) return false;
            if (patPhase == PatPhase.Feedback)
            {
                patReleaseRequested = false;
                return true;
            }
            StopIdleExpression();
            if (subscribedState.GetCurrent(0) == null) subscribedState.SetAnimation(0, "Idle_01", true);
            FadeOverlayIn(1, "Pat_01_M", Mathf.Max(0.3f, mixDuration));
            FadeOverlayIn(2, "Pat_01_A", Mathf.Max(0.3f, mixDuration));
            patPhase = PatPhase.Feedback;
            patElapsed = 0;
            patReleaseRequested = false;
            outsideHeadTime = 0;
            return true;
        }

        // Setting MixDuration on a first entry alone does not blend from lower tracks.
        // Spine's empty -> queued animation pattern gives a real blend-in from Idle.
        private TrackEntry FadeOverlayIn(int track, string animation, float duration)
        {
            TrackEntry entry;
            if (subscribedState.GetCurrent(track) == null)
            {
                subscribedState.SetEmptyAnimation(track, 0f);
                entry = subscribedState.AddAnimation(track, animation, false, 0f);
                entry.Delay = 0f;
            }
            else entry = subscribedState.SetAnimation(track, animation, false);
            entry.MixDuration = duration;
            entry.AlphaAttachmentThreshold = 0.5f;
            entry.MixAttachmentThreshold = 0.5f;
            return entry;
        }

        public void RequestEndPat()
        {
            if (patPhase != PatPhase.Feedback) return;
            patReleaseRequested = true;
            if (patElapsed >= minimumPatFeedback) BeginPatEnding();
        }

        private void BeginPatEnding()
        {
            if (patPhase != PatPhase.Feedback || subscribedState == null) return;
            patPhase = PatPhase.Ending;
            subscribedState.SetAnimation(1, "PatEnd_01_M", false).MixDuration = 0.25f;
            subscribedState.SetAnimation(2, "PatEnd_01_A", false).MixDuration = mixDuration;
            subscribedState.AddEmptyAnimation(1, 0.3f, 0f);
            subscribedState.AddEmptyAnimation(2, 0.3f, 0f);
        }

        public bool ContainsHeadPatPoint(Vector2 screenPoint)
        {
            if (viewport == null || Graphic.Skeleton == null || Graphic.canvas == null) return false;
            Camera camera = Graphic.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Graphic.canvas.worldCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, screenPoint, camera)) return false;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Graphic.rectTransform, screenPoint, camera, out local)) return false;
            Bone head = Graphic.Skeleton.FindBone("Head_Rot");
            if (head == null || Graphic.MeshScale <= 0f) return false;
            float x, y;
            head.WorldToLocal(local.x / Graphic.MeshScale, local.y / Graphic.MeshScale, out x, out y);
            float nx = (x - headPatArea.center.x) / (headPatArea.width * 0.5f);
            float ny = (y - headPatArea.center.y) / (headPatArea.height * 0.5f);
            return nx * nx + ny * ny <= 1f;
        }

        // One input path is shared by the live Input System and deterministic interaction checks.
        public void ProcessHeadPatPointer(Vector2 position, bool pressed, bool held, bool released, float deltaTime)
        {
            bool inside = ContainsHeadPatPoint(position);
            if (pressed && inside && BeginPatFeedback()) pointerCaptured = true;
            if (!pointerCaptured) return;
            if (released || !held)
            {
                pointerCaptured = false;
                RequestEndPat();
                return;
            }
            if (inside)
            {
                outsideHeadTime = 0;
                if (patPhase != PatPhase.Feedback) BeginPatFeedback();
            }
            else
            {
                outsideHeadTime += Mathf.Max(0, deltaTime);
                if (outsideHeadTime >= pointerExitGrace) RequestEndPat();
            }
        }

        private void PollHeadPatPointer(float deltaTime)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
            {
                SubmitPointerSample(touch.position.ReadValue(), touch.press.wasPressedThisFrame,
                    touch.press.isPressed, touch.press.wasReleasedThisFrame, deltaTime);
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
                SubmitPointerSample(mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.isPressed, mouse.leftButton.wasReleasedThisFrame, deltaTime);
#elif ENABLE_LEGACY_INPUT_MANAGER
            SubmitPointerSample(Input.mousePosition, Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0), deltaTime);
#endif
        }

        private void SubmitPointerSample(Vector2 position, bool pressed, bool held, bool released, float dt)
        {
            // Held-state edges also cover focus/update boundaries where Input System's
            // per-update press/release flags may no longer be set when this script runs.
            bool down = pressed || (held && !pointerHeldLastFrame);
            bool up = released || (!held && pointerHeldLastFrame);
            pointerHeldLastFrame = held;
            ProcessHeadPatPointer(position, down, held, up, dt);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { pointerCaptured = false; pointerHeldLastFrame = false; RequestEndPat(); }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) { pointerCaptured = false; pointerHeldLastFrame = false; RequestEndPat(); }
        }

        /// <summary>Advance interaction timers before SkeletonGraphic.Update; does not advance Spine itself.</summary>
        public void AdvanceInteraction(float deltaTime)
        {
            if (Graphic.Skeleton == null) return;
            float dt = Mathf.Max(0, deltaTime);
            AdvanceEntranceTransition(dt);
            if (patPhase == PatPhase.Feedback)
            {
                patElapsed += dt;
                if (patReleaseRequested && patElapsed >= minimumPatFeedback) BeginPatEnding();
            }
            else if (patPhase == PatPhase.Ending && Graphic.AnimationState.GetCurrent(1) == null && Graphic.AnimationState.GetCurrent(2) == null)
            {
                patPhase = PatPhase.None;
                idleDelay = NextExpressionDelay();
            }

            bool available = !IsEntrancePlaying && !IsPatting &&
                Graphic.AnimationState.GetCurrent(0)?.Animation.Name == "Idle_01" &&
                Graphic.AnimationState.GetCurrent(1) == null && Graphic.AnimationState.GetCurrent(2) == null;
            if (expression != IdleExpression.None)
            {
                expressionAge += dt;
                if (!available || expressionAge >= expressionDuration || !enableIdleExpressions) StopIdleExpression();
                expressionWeight = Mathf.MoveTowards(expressionWeight, expressionEnding ? 0f : 1f, dt / 0.35f);
                if (expressionEnding && expressionWeight <= 0f)
                {
                    expression = IdleExpression.None;
                    idleDelay = NextExpressionDelay();
                }
            }
            else if (available && enableIdleExpressions)
            {
                idleDelay -= dt;
                if (idleDelay <= 0)
                {
                    var kind = (IdleExpression)(1 + expressionSequence++ % 3);
                    PlayIdleExpression(kind);
                }
            }
        }

        public bool PlayIdleExpression(IdleExpression kind)
        {
            AttachEvents();
            if (kind == IdleExpression.None || IsEntrancePlaying || IsPatting ||
                subscribedState.GetCurrent(1) != null || subscribedState.GetCurrent(2) != null) return false;
            if (subscribedState.GetCurrent(0)?.Animation.Name != "Idle_01") return false;
            if (expression != IdleExpression.None) StopIdleExpression();
            expression = kind;
            expressionAge = 0;
            expressionEnding = false;
            expressionWeight = 0;
            expressionDuration = kind == IdleExpression.ClosedEyeSmile ? 2.5f : kind == IdleExpression.ThoughtfulLook ? 4f : 3f;
            if (kind == IdleExpression.ClosedEyeSmile)
            {
                var eyes = FadeOverlayIn(3, "Eye_Close_01", 0.3f);
                eyes.TrackTime = 1f / 6f;
                eyes.TimeScale = 0f;
            }
            return true;
        }

        private void StopIdleExpression()
        {
            if (expression == IdleExpression.None || expressionEnding) return;
            expressionEnding = true;
            if (Graphic.AnimationState.GetCurrent(3) != null) Graphic.AnimationState.SetEmptyAnimation(3, 0.3f);
        }

        private void CacheExpressionBones()
        {
            if (expressionSkeleton == Graphic.Skeleton) return;
            expressionSkeleton = Graphic.Skeleton;
            expressionBones.Clear();
            offsetsApplied = false;
            foreach (string name in new[] { "Head_Rot", "L_eye_1_1", "R_eye_1_1" })
            {
                Bone bone = expressionSkeleton?.FindBone(name);
                if (bone != null) expressionBones.Add(new BoneDelta { bone = bone });
            }
        }

        private void RemoveExpressionOffsets(ISkeletonAnimation animated)
        {
            if (expressionSkeleton != Graphic.Skeleton) { offsetsApplied = false; return; }
            if (!offsetsApplied) return;
            foreach (BoneDelta delta in expressionBones) delta.Remove();
            offsetsApplied = false;
        }

        private void ApplyExpressionOffsets(ISkeletonAnimation animated)
        {
            CacheExpressionBones();
            if (expression == IdleExpression.None || expressionWeight <= 0) return;
            float weight = Mathf.SmoothStep(0f, 1f, expressionWeight);
            float tilt = expression == IdleExpression.ThoughtfulLook ? -1.25f : expression == IdleExpression.ClosedEyeSmile ? 1.1f : 0.75f;
            float eye = expression == IdleExpression.ThoughtfulLook ? -0.075f : expression == IdleExpression.PlayerGlance ? 0.045f : 0f;
            foreach (BoneDelta delta in expressionBones)
            {
                delta.x = delta.y = delta.rotation = 0;
                if (delta.bone.Data.Name == "Head_Rot") delta.rotation = tilt * weight;
                else { delta.x = eye * weight; delta.y = expression == IdleExpression.ThoughtfulLook ? 0.025f * weight : 0f; }
                delta.Apply();
            }
            offsetsApplied = true;
        }

        private void OnDrawGizmosSelected()
        {
            if (Graphic.Skeleton == null) return;
            Bone head = Graphic.Skeleton.FindBone("Head_Rot");
            if (head == null) return;
            Gizmos.color = new Color(0.2f, 0.95f, 0.8f, 0.85f);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                float x, y;
                head.LocalToWorld(headPatArea.center.x + Mathf.Cos(angle) * headPatArea.width * 0.5f,
                    headPatArea.center.y + Mathf.Sin(angle) * headPatArea.height * 0.5f, out x, out y);
                Vector3 point = Graphic.transform.TransformPoint(new Vector3(x, y, 0) * Graphic.MeshScale);
                if (i > 0) Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
