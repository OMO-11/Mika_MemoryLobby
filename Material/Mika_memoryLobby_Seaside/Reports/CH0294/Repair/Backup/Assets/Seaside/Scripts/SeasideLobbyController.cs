using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Mika.Seaside
{
    /// <summary>Frames the packed lobby and layers attachment tracks over motion tracks.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonGraphic))]
    public sealed class SeasideLobbyController : MonoBehaviour
    {
        [Serializable]
        public sealed class VoiceBinding
        {
            public string eventName;
            public AudioClip clip;
        }

        [SerializeField] private RectTransform viewport;
        // Skeleton units after SkeletonDataAsset.scale (0.01), before Canvas PPU.
        [SerializeField] private Rect safeFrame = new Rect(-16f, -0.2f, 32f, 18f);
        [SerializeField] private float portraitFocusX = 3.5f;
        [SerializeField] private bool chineseTimeline = true;
        [SerializeField, Min(0f)] private float mixDuration = 0.2f;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private List<VoiceBinding> voices = new List<VoiceBinding>();

        private SkeletonGraphic graphic;
        private Spine.AnimationState subscribedState;
        private Vector2 previousViewport;
        private float previousPixelsPerUnit;

        public Rect SafeFrame => safeFrame;
        public int VoiceBindingCount => voices.Count;

        private SkeletonGraphic Graphic
        {
            get
            {
                if (graphic == null) graphic = GetComponent<SkeletonGraphic>();
                return graphic;
            }
        }

        private void OnEnable()
        {
            FitViewport();
            if (Application.isPlaying) AttachEvents();
        }

        private void Start()
        {
            if (Application.isPlaying) ResetToIdle();
        }

        private void LateUpdate()
        {
            if (viewport == null) return;
            float ppu = Graphic.canvas == null ? 100f : Graphic.canvas.referencePixelsPerUnit;
            if (viewport.rect.size != previousViewport || ppu != previousPixelsPerUnit) FitViewport();
            if (Application.isPlaying) AttachEvents();
        }

        private void OnDisable()
        {
            if (subscribedState != null) subscribedState.Event -= OnSpineEvent;
            subscribedState = null;
        }

        private void OnValidate()
        {
            if (safeFrame.width <= 0f || safeFrame.height <= 0f)
                safeFrame = new Rect(-16f, -0.2f, 32f, 18f);
            mixDuration = Mathf.Max(0f, mixDuration);
            previousViewport = Vector2.zero;
        }

        public void Configure(RectTransform targetViewport, AudioSource source)
        {
            viewport = targetViewport;
            voiceSource = source;
            FitViewport();
        }

        /// <summary>Uniform cover fit; keep the top of the head and crop inside the safe rectangle.</summary>
        public void FitViewport()
        {
            if (viewport == null || Graphic == null) return;
            Vector2 size = viewport.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float ppu = Graphic.canvas == null ? 100f : Graphic.canvas.referencePixelsPerUnit;
            ppu = Mathf.Max(0.001f, ppu);
            float scale = Mathf.Max(size.x / (safeFrame.width * ppu), size.y / (safeFrame.height * ppu));
            Vector2 visible = size / (scale * ppu);
            float centerX = Mathf.Clamp(portraitFocusX, safeFrame.xMin + visible.x * 0.5f,
                safeFrame.xMax - visible.x * 0.5f);
            float centerY = safeFrame.yMax - visible.y * 0.5f;
            RectTransform rect = Graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = -new Vector2(centerX, centerY) * ppu * scale;
            previousViewport = size;
            previousPixelsPerUnit = ppu;
        }

        private void AttachEvents()
        {
            Graphic.Initialize(false);
            var state = Graphic.AnimationState;
            if (state == subscribedState) return;
            if (subscribedState != null) subscribedState.Event -= OnSpineEvent;
            subscribedState = state;
            if (subscribedState != null) subscribedState.Event += OnSpineEvent;
        }

        [ContextMenu("Playback/Reset to Idle")]
        public void ResetToIdle()
        {
            AttachEvents();
            if (subscribedState == null) return;
            subscribedState.ClearTracks();
            Graphic.Skeleton.SetToSetupPose();
            subscribedState.SetAnimation(0, "Idle_01", true);
            Graphic.Update(0f);
            Graphic.LateUpdate();
            if (voiceSource != null) voiceSource.Stop();
        }

        [ContextMenu("Playback/Play Entrance")]
        public void PlayEntrance()
        {
            ResetToIdle();
            subscribedState.SetAnimation(0, "Start_Idle_01", false);
            subscribedState.AddAnimation(0, "Idle_01", true, 0f);
        }

        public void PlayDialogue(int index)
        {
            if (index < 1 || index > 5) throw new ArgumentOutOfRangeException(nameof(index));
            string suffix = chineseTimeline ? "_CN" : string.Empty;
            PlayPair("Talk_" + index.ToString("00") + "_M" + suffix,
                "Talk_" + index.ToString("00") + "_A" + suffix);
        }

        private void PlayPair(string motion, string attachments)
        {
            AttachEvents();
            if (subscribedState == null) return;
            if (Graphic.Skeleton.Data.FindAnimation(motion) == null ||
                Graphic.Skeleton.Data.FindAnimation(attachments) == null) return;
            if (subscribedState.GetCurrent(0) == null) subscribedState.SetAnimation(0, "Idle_01", true);
            subscribedState.SetAnimation(1, motion, false).MixDuration = mixDuration;
            subscribedState.SetAnimation(2, attachments, false).MixDuration = mixDuration;
            subscribedState.AddEmptyAnimation(1, mixDuration, 0f);
            subscribedState.AddEmptyAnimation(2, mixDuration, 0f);
        }

        [ContextMenu("Playback/Play Dialogue 1")]
        public void PlayDialogue1() => PlayDialogue(1);
        [ContextMenu("Playback/Play Dialogue 2")]
        public void PlayDialogue2() => PlayDialogue(2);
        [ContextMenu("Playback/Play Dialogue 3")]
        public void PlayDialogue3() => PlayDialogue(3);
        [ContextMenu("Playback/Play Dialogue 4")]
        public void PlayDialogue4() => PlayDialogue(4);
        [ContextMenu("Playback/Play Dialogue 5")]
        public void PlayDialogue5() => PlayDialogue(5);
        [ContextMenu("Playback/Play Pat")]
        public void PlayPat() => PlayPair("Pat_01_M", "Pat_01_A");
        [ContextMenu("Playback/End Pat")]
        public void EndPat() => PlayPair("PatEnd_01_M", "PatEnd_01_A");
        [ContextMenu("Playback/End Look")]
        public void EndLook() => PlayPair("LookEnd_01_M", "LookEnd_01_A");

        private void OnSpineEvent(TrackEntry entry, Spine.Event ev)
        {
            if (voiceSource == null) return;
            string name = ev.Data.Name;
            // CN timelines emit both Sound/*.wav and CH0294_*.ogg at the same time.
            // Route only one language family to avoid playing the same line twice.
            if (chineseTimeline)
            {
                if (!name.StartsWith("CH0294_", StringComparison.Ordinal)) return;
            }
            else
            {
                if (!name.StartsWith("Sound/CH0294_", StringComparison.Ordinal)) return;
                name = name.Substring("Sound/".Length);
            }
            foreach (var binding in voices)
            {
                if (binding == null || binding.clip == null || binding.eventName != name) continue;
                voiceSource.clip = binding.clip;
                voiceSource.Play();
                break;
            }
        }
    }
}
