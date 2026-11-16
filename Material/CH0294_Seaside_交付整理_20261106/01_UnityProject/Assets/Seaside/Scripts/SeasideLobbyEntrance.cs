using UnityEngine;
using UnityEngine.UI;

namespace Mika.Seaside
{
    public sealed partial class SeasideLobbyController
    {
        [Header("Entrance transition")]
        [SerializeField, Range(1.2f, 3f)] private float entranceSpeed = 1.25f;
        [SerializeField] private Image entranceFlash;
        private const float FlashRiseDuration = 0.35f;
        private const float FlashHoldDuration = 0.08f;
        private const float FlashFadeDuration = 0.65f;
        private bool entranceTransitionActive;
        private bool revealingIdle;
        private float revealElapsed;

        public float EntranceSpeed => entranceSpeed;
        public float EntranceFlashAlpha => entranceFlash == null ? 0f : entranceFlash.color.a;

        private void EnsureEntranceFlash()
        {
            if (entranceFlash == null && viewport != null)
            {
                var overlay = new GameObject("Entrance white fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay.transform.SetParent(viewport, false);
                entranceFlash = overlay.GetComponent<Image>();
                entranceFlash.color = new Color(1f, 1f, 1f, 0f);
            }
            if (entranceFlash == null) return;
            RectTransform rect = entranceFlash.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
            entranceFlash.raycastTarget = false;
        }

        private void SetEntranceFlash(float alpha)
        {
            if (entranceFlash == null) return;
            entranceFlash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            entranceFlash.enabled = alpha > 0f;
        }

        private void BeginEntranceTransition()
        {
            EnsureEntranceFlash();
            entranceTransitionActive = true;
            revealingIdle = false;
            revealElapsed = 0f;
            SetEntranceFlash(0f);
            var intro = subscribedState.SetAnimation(0, "Start_Idle_01", false);
            intro.MixDuration = 0f;
            // Only the entrance track speeds up. Idle, patting and expressions retain their timing.
            intro.TimeScale = Mathf.Clamp(entranceSpeed, 1.2f, 3f);
            intro.AnimationEnd = FindEntrancePoseCut(intro.Animation);
        }

        private float FindEntrancePoseCut(Spine.Animation animation)
        {
            // This export swaps the side-face artwork to the front pose at 16.6667 s,
            // followed by five seconds of front-pose hold. Cover that internal cut,
            // rather than adding a flash after the hold has already exposed the cut.
            foreach (var timeline in animation.Timelines)
            {
                var attachments = timeline as Spine.AttachmentTimeline;
                if (attachments == null || Graphic.Skeleton.Data.Slots.Items[attachments.SlotIndex].Name != "00_face") continue;
                bool sideFaceSeen = false;
                for (int i = 0; i < attachments.Frames.Length; i++)
                {
                    if (attachments.AttachmentNames[i] != null) sideFaceSeen = true;
                    else if (sideFaceSeen) return attachments.Frames[i];
                }
            }
            return animation.Duration;
        }

        private void AdvanceEntranceTransition(float dt)
        {
            if (!entranceTransitionActive) return;
            if (!revealingIdle)
            {
                var intro = subscribedState.GetCurrent(0);
                if (intro == null || intro.Animation.Name != "Start_Idle_01")
                {
                    CancelEntranceTransition();
                    return;
                }
                float remaining = Mathf.Max(0f, intro.AnimationEnd - intro.TrackTime) / intro.TimeScale;
                SetEntranceFlash(1f - Mathf.SmoothStep(0f, 1f, remaining / FlashRiseDuration));
                // Cover the next Spine update before it can cross the discrete attachment key.
                // A resumed Editor can report a large unscaled delta; it must not skip the intro.
                if (remaining > Mathf.Min(dt, 0.1f)) return;

                // The native close-up and idle use different attachments/poses.
                // Replace them only while fully covered, then reveal a clean idle pose.
                SetEntranceFlash(1f);
                subscribedState.ClearTracks();
                Graphic.Skeleton.SetToSetupPose();
                subscribedState.SetAnimation(0, "Idle_01", true).MixDuration = 0f;
                revealingIdle = true;
                revealElapsed = 0f;
                return;
            }
            revealElapsed += dt;
            float fade = (revealElapsed - FlashHoldDuration) / FlashFadeDuration;
            SetEntranceFlash(1f - Mathf.SmoothStep(0f, 1f, fade));
            if (revealElapsed >= FlashHoldDuration + FlashFadeDuration) CancelEntranceTransition();
        }

        private void CancelEntranceTransition()
        {
            entranceTransitionActive = false;
            revealingIdle = false;
            revealElapsed = 0f;
            SetEntranceFlash(0f);
        }
    }
}
