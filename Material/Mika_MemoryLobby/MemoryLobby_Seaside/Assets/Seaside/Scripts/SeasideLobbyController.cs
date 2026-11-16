using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Mika.Seaside
{
    /// <summary>Frames the packed lobby and layers attachment tracks over motion tracks.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonGraphic))]
    public sealed partial class SeasideLobbyController : MonoBehaviour
    {
        [Serializable]
        public sealed class VoiceBinding
        {
            public string eventName;
            public AudioClip clip;
        }

        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform oceanEdgeUnderlay;
        // Skeleton units after SkeletonDataAsset.scale (0.01), before Canvas PPU.
        // Reference framing: 1652x1080 screenshot, cropped inside the animated sky/sea.
        [SerializeField] private Rect safeFrame = new Rect(-15.5f, -1.27f, 32f, 20.92f);
        [SerializeField] private float portraitFocusX = 3.5f;
        [SerializeField] private bool chineseTimeline = true;
        [SerializeField, Min(0f)] private float mixDuration = 0.2f;
        [SerializeField] private bool playEntranceOnStart = true;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private List<VoiceBinding> voices = new List<VoiceBinding>();

        private SkeletonGraphic graphic;
        private Spine.AnimationState subscribedState;
        private Vector2 previousViewport;
        private float previousPixelsPerUnit;
        private SkeletonRendererInstruction backgroundInstructions;
        private int backgroundSubmesh;
        private readonly List<BackgroundVertexRange> backgroundRanges = new List<BackgroundVertexRange>();
        private float backgroundWidthScale = 1f;
        private struct BackgroundVertexRange { public int start, count; }

        public Rect SafeFrame => safeFrame;
        public int VoiceBindingCount => voices.Count;
        public Rect VisibleFrame { get; private set; }
        public float BackgroundWidthScale => backgroundWidthScale;
        public int BackgroundMappingFailures { get; private set; }
        public string LastBackgroundMappingFailure { get; private set; }

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
            Canvas.preWillRenderCanvases += OnCanvasWillRender;
            Graphic.OnInstructionsPrepared += PrepareBackgroundMesh;
            Graphic.OnPostProcessVertices += FitBackgroundWidth;
            EnableInteractionHooks();
            FitViewport();
            if (Application.isPlaying) AttachEvents();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                Canvas.ForceUpdateCanvases();
                FitViewport();
                if (playEntranceOnStart) PlayEntrance();
                else ResetToIdle();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            PollHeadPatPointer(Time.unscaledDeltaTime);
            AdvanceInteraction(Time.unscaledDeltaTime);
        }

        private void OnCanvasWillRender()
        {
            if (viewport == null) return;
            float ppu = Graphic.canvas == null ? 100f : Graphic.canvas.referencePixelsPerUnit;
            if (viewport.rect.size != previousViewport || ppu != previousPixelsPerUnit) FitViewport();
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
            CancelEntranceTransition();
            Canvas.preWillRenderCanvases -= OnCanvasWillRender;
            Graphic.OnInstructionsPrepared -= PrepareBackgroundMesh;
            Graphic.OnPostProcessVertices -= FitBackgroundWidth;
            DisableInteractionHooks();
            if (subscribedState != null) subscribedState.Event -= OnSpineEvent;
            subscribedState = null;
        }

        private void OnValidate()
        {
            if (safeFrame.width <= 0f || safeFrame.height <= 0f)
                safeFrame = new Rect(-15.5f, -1.27f, 32f, 20.92f);
            mixDuration = Mathf.Max(0f, mixDuration);
            previousViewport = Vector2.zero;
        }

        public void Configure(RectTransform targetViewport, AudioSource source)
        {
            viewport = targetViewport;
            voiceSource = source;
            FitViewport();
        }

        /// <summary>Preserve the GIF reference's vertical field of view at every aspect ratio.</summary>
        public void FitViewport()
        {
            if (viewport == null || Graphic == null) return;
            Vector2 size = viewport.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float ppu = Graphic.canvas == null ? 100f : Graphic.canvas.referencePixelsPerUnit;
            ppu = Mathf.Max(0.001f, ppu);
            float scale = size.y / (safeFrame.height * ppu);
            Vector2 visible = size / (scale * ppu);
            float centerX = visible.x >= safeFrame.width ? safeFrame.center.x :
                Mathf.Clamp(portraitFocusX, safeFrame.xMin + visible.x * 0.5f,
                    safeFrame.xMax - visible.x * 0.5f);
            float centerY = safeFrame.center.y;
            VisibleFrame = new Rect(centerX - visible.x * 0.5f, centerY - visible.y * 0.5f, visible.x, visible.y);
            // The wave meshes have slanted transparent edges. Leave a small horizontal
            // overscan margin when the role no longer hides those edges on wider views.
            backgroundWidthScale = visible.x > safeFrame.width + 0.001f ? visible.x / safeFrame.width * 1.08f : 1f;
            RectTransform rect = Graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = -new Vector2(centerX, centerY) * ppu * scale;
            if (oceanEdgeUnderlay != null)
            {
                oceanEdgeUnderlay.gameObject.SetActive(backgroundWidthScale > 1.00001f);
                oceanEdgeUnderlay.anchorMin = oceanEdgeUnderlay.anchorMax = oceanEdgeUnderlay.pivot = new Vector2(0.5f, 0.5f);
                oceanEdgeUnderlay.localRotation = Quaternion.Euler(0, 0, 90);
                oceanEdgeUnderlay.sizeDelta = new Vector2(size.y * 0.5f, size.x);
                oceanEdgeUnderlay.anchoredPosition = new Vector2(0, -size.y * 0.25f);
            }
            previousViewport = size;
            previousPixelsPerUnit = ppu;
        }

        private void PrepareBackgroundMesh(SkeletonRendererInstruction instructions)
        {
            backgroundInstructions = instructions;
            backgroundSubmesh = 0;
        }

        // BG's bone subtree also contains character bones in this asset. Modify only
        // vertices belonging to BG_* slots, after Spine has generated each page mesh.
        // This pack has no clipping attachments; a vertex-count guard fails closed
        // if a future pack changes that assumption. Shared role vertices stay intact.
        private void FitBackgroundWidth(MeshGeneratorBuffers buffers)
        {
            int index = backgroundSubmesh++;
            if (backgroundWidthScale <= 1.00001f || backgroundInstructions == null) return;
            if (!Graphic.allowMultipleCanvasRenderers || index >= backgroundInstructions.submeshInstructions.Count) return;
            SubmeshInstruction instruction = backgroundInstructions.submeshInstructions.Items[index];
            int vertex = 0;
            backgroundRanges.Clear();
            for (int slotIndex = instruction.startSlot; slotIndex < instruction.endSlot; slotIndex++)
            {
                Slot slot = instruction.skeleton.DrawOrder.Items[slotIndex];
                // Matches this project's MeshGenerator SLOT_ALPHA_DISABLES_ATTACHMENT.
                if (!slot.Bone.Active || slot.A == 0f) continue;
                if (slot.Attachment is ClippingAttachment) { BackgroundMappingFailures++; return; }
                int count = 0;
                if (slot.Attachment is RegionAttachment) count = 4;
                else if (slot.Attachment is MeshAttachment mesh) count = mesh.WorldVerticesLength / 2;
                if (count == 0) continue;
                if (slot.Data.Name.StartsWith("BG_", StringComparison.Ordinal))
                    backgroundRanges.Add(new BackgroundVertexRange { start = vertex, count = count });
                vertex += count;
            }
            if (vertex != buffers.vertexCount)
            {
                BackgroundMappingFailures++;
                LastBackgroundMappingFailure = $"page={index} slots={instruction.startSlot}..{instruction.endSlot} expected={vertex} actual={buffers.vertexCount}";
                return;
            }
            float center = safeFrame.center.x * Graphic.MeshScale + Graphic.MeshOffset.x;
            foreach (BackgroundVertexRange range in backgroundRanges)
            {
                for (int i = range.start; i < range.start + range.count; i++)
                    buffers.vertexBuffer[i].x = center + (buffers.vertexBuffer[i].x - center) * backgroundWidthScale;
            }
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
            CancelEntranceTransition();
            AttachEvents();
            if (subscribedState == null) return;
            ResetInteractiveState();
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
            if (subscribedState == null) return;
            BeginEntranceTransition();
        }

        public void PlayDialogue(int index)
        {
            if (index < 1 || index > 5) throw new ArgumentOutOfRangeException(nameof(index));
            CancelInteractiveOverlay();
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
        public void PlayPat() => BeginPatFeedback();
        [ContextMenu("Playback/End Pat")]
        public void EndPat() => RequestEndPat();
        [ContextMenu("Playback/End Look")]
        public void EndLook()
        {
            CancelInteractiveOverlay();
            PlayPair("LookEnd_01_M", "LookEnd_01_A");
        }

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
