using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GiftCompanion.Companion
{
    public interface ICompanionVisualController
    {
        void SetState(CharacterVisualState state);
        void SetEmotion(CharacterEmotion emotion);
        void PlayAction(string action);
    }
    public sealed partial class CompanionPresentation : MonoBehaviour, ICompanionVisualController
    {
        [SerializeField] private CompanionValidationBootstrap loader;
        [SerializeField] private TextAsset expressionMapAsset;
        private JObject expressions;
        public CompanionValidationBootstrap Loader => loader;
        public CharacterEmotion RequestedEmotion { get; private set; }
        public CharacterEmotion ResolvedEmotion { get; private set; }
        public bool UsedEmotionFallback { get; private set; }
        public CharacterVisualState CurrentState { get; private set; }
        public void SetEmotion(CharacterEmotion emotion)
        {
            RequestedEmotion = emotion;
            var mapping = expressions?[emotion.ToString().ToLowerInvariant()];
            // Base is the only validated persistent visual in this first release.
            UsedEmotionFallback = emotion != CharacterEmotion.Neutral || (bool?)mapping?["supported"] != true || (string)mapping?["visual"] != "base";
            ResolvedEmotion = CharacterEmotion.Neutral;
        }
        public void PlayAction(string action) { if (action != "none") SetState(CharacterVisualState.Idle); }
        public void SetState(CharacterVisualState state)
        {
            CurrentState = state;
            loader.Character?.GetComponent<SeasideSpaceView>()?.SetState(state);
            if(state==CharacterVisualState.Thinking||state==CharacterVisualState.Speaking)loader.SkipIntro();
            if(loader.IsPlayingIntro)return;
            if (loader.Motion == null) return;
            loader.Motion.SetState(state == CharacterVisualState.Thinking ? CompanionLocalMotion.PresentationState.Thinking :
                state == CharacterVisualState.Speaking ? CompanionLocalMotion.PresentationState.Speaking : CompanionLocalMotion.PresentationState.Idle);
        }
        public void SetUiCapture(bool captured)
        {
            if (loader.Character == null) return;
            var pointer = loader.Character.GetComponent<CompanionPointerInteraction>();
            if (pointer != null) pointer.enabled = !captured&&!loader.IsPlayingIntro;
            if (loader.Motion != null) loader.Motion.allowPointer = !captured;
            loader.Character.GetComponent<SeasideSpaceView>()?.Capture(captured);
            if (captured) CancelInteraction();
        }

        private async void Start()
        {
            defaultProfile = loader.profile;
            loader.enabled = false; // The sample's F8/OnGUI driver stays outside the main program flow.
            loader.diagnosticsVisible = false;
            loader.Initialize();
            // Materials are created and owned by the scene loader, never imported assets.
            // Avoid the runtime shader-feature branch that differs in the Windows build.
            var alphaShader = Shader.Find("GiftCompanion/SpineStraightAlpha");
            if (alphaShader == null) throw new System.InvalidOperationException("Companion alpha shader missing.");
            foreach (var atlas in loader.Character.SkeletonDataAsset.atlasAssets)
                foreach (var material in atlas.Materials) material.shader = alphaShader;
            if (expressionMapAsset != null) expressions = JObject.Parse(expressionMapAsset.text)["emotions"] as JObject;
            if (loader.renderCamera.GetComponent<CompanionTone>() == null) loader.renderCamera.gameObject.AddComponent<CompanionTone>();
            var root = AppRoot.Instance;
            ActiveSpaceId = root?.Library.Spaces.Spaces.Find(s => s.Builtin)?.Id;
            if (root?.Library.Spaces.SelectedSpaceId is string selected)
            {
                var entry = root.Library.Spaces.Spaces.Find(s => s.Id == selected);
                try { if (entry != null) await SwitchSpaceAsync(entry, destroyCancellationToken, true); }
                catch (System.OperationCanceledException) { }
                catch (System.Exception error) { SpaceError = "这个空间暂时没能打开，先保留记忆大厅。"; root.Logger.Error("space selection fallback=true", error); }
            }
            Ready = true;
            root?.Logger?.Info("companion presentation ready");
        }

        public void SetThinking(bool thinking)
        {
            if(thinking)loader.SkipIntro();
            if (loader.Motion == null||loader.IsPlayingIntro) return;
            loader.Motion.SetState(thinking ? CompanionLocalMotion.PresentationState.Thinking : CompanionLocalMotion.PresentationState.Idle);
        }

        public void CancelInteraction()
        {
            if (loader == null || loader.Character == null) return;
            loader.Character.GetComponent<CompanionPointerInteraction>()?.Cancel();
            loader.Expression?.CancelTouch();
            loader.Character.GetComponent<SeasideSpaceView>()?.EndPat();
        }

        private void OnDisable()
        {
            CancelInteraction();
            loader.Speech?.Stop();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelInteraction();
        }
    }
}
