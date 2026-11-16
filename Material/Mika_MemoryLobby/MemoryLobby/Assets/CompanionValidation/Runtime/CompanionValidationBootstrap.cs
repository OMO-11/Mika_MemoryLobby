using System;
using System.IO;
using Spine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Isolated art validation scene; loads untouched source exports, no dialogue session.</summary>
public sealed class CompanionValidationBootstrap : MonoBehaviour {
    public CompanionBehaviorProfile profile;
    public Camera renderCamera;
    public bool enableLocalBehavior;
    public AudioClip[] sampleVoices;
    public bool diagnosticsVisible;
    public CompanionLocalMotion Motion { get; private set; }
    public CompanionExpressionOverlay Expression { get; private set; }
    public CompanionSpeechDriver Speech { get; private set; }
    public CompanionPat10Trial Pat10 { get; private set; }
    public SkeletonAnimation Character { get; private set; }
    public int IdleLoops { get; private set; }
    public float StartedAt { get; private set; }
    public bool IsPlayingIntro { get; private set; }
    Material template;
    SpineAtlasAsset atlas;
    SkeletonDataAsset data;
    Texture2D[] textures;
    TextAsset skeletonBytes, atlasText;
    Spine.AnimationState observedState;
    bool previousBackground;
    void Start() { Initialize(); }
    public void Initialize() {
        if (Character != null) return;
        if (profile == null) throw new InvalidOperationException("Behavior profile is missing.");
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        string dir = Path.Combine(Application.streamingAssetsPath, profile.resourceFolder);
        textures = new Texture2D[profile.textureFiles.Length];
        for (int i = 0; i < textures.Length; i++) {
            textures[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            textures[i].name = Path.GetFileNameWithoutExtension(profile.textureFiles[i]);
            if (!textures[i].LoadImage(File.ReadAllBytes(Path.Combine(dir, profile.textureFiles[i]))))
                throw new InvalidDataException("Invalid texture: " + profile.textureFiles[i]);
            textures[i].wrapMode = TextureWrapMode.Clamp;
        }
        var shader = Shader.Find("Spine/Skeleton");
        if (shader == null) throw new InvalidOperationException("Spine/Skeleton shader is missing.");
        template = new Material(shader);
        template.SetFloat("_StraightAlphaInput", 1);
        template.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
        atlasText = new TextAsset(File.ReadAllText(Path.Combine(dir, profile.atlasFile)));
        atlas = SpineAtlasAsset.CreateRuntimeInstance(atlasText, textures, template, true, null, true);
        skeletonBytes = new TextAsset(File.ReadAllBytes(Path.Combine(dir, profile.skeletonFile)));
        skeletonBytes.name = profile.skeletonFile;
        data = SkeletonDataAsset.CreateRuntimeInstance(skeletonBytes, atlas, true, profile.skeletonScale);
        Character = SkeletonAnimation.NewSkeletonAnimationGameObject(data);
        Character.name = "Companion Character";
        Character.transform.SetParent(transform, false);
        Character.OnAnimationRebuild += RestoreBase;
        RestoreBase(null);
        if (enableLocalBehavior) {
            Motion = Character.gameObject.AddComponent<CompanionLocalMotion>();
            Motion.profile = profile;
            Motion.renderCamera = renderCamera;
            Motion.Bind(Character);
            Expression=Character.gameObject.AddComponent<CompanionExpressionOverlay>();
            Expression.profile=profile;Expression.Bind(Character);
            if(profile.enablePat10Trial) {
                Pat10=Character.gameObject.AddComponent<CompanionPat10Trial>();
                Pat10.Bind(Character,Expression);
                Pat10.UseForTouch=true;
            }
            var pointer=Character.gameObject.AddComponent<CompanionPointerInteraction>();
            pointer.profile=profile;pointer.renderCamera=renderCamera;pointer.motion=Motion;pointer.expression=Expression;
            Speech=Character.gameObject.AddComponent<CompanionSpeechDriver>();
            Speech.voice=Character.gameObject.AddComponent<AudioSource>();
            Speech.voice.playOnAwake=false;Speech.voice.spatialBlend=0;
            Speech.expression=Expression;Speech.motion=Motion;
        }
        StartedAt = Time.realtimeSinceStartup;
        renderCamera.orthographic = true;
        renderCamera.transform.position = new Vector3(0, 9.7f, -100);
        renderCamera.orthographicSize = 9.5f;
        renderCamera.nearClipPlane = .1f;
        renderCamera.farClipPlane = 200;
        renderCamera.backgroundColor = new UnityEngine.Color(.04f, .05f, .07f, 1);
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        if(profile.playIntroOnStartup)PlayIntro();
    }
    void OnComplete(Spine.TrackEntry entry) {
        if (entry.TrackIndex == 0 && entry.Animation.Name == profile.idleAnimation) IdleLoops++;
    }
    void RestoreBase(ISkeletonAnimation _) {
        if(observedState!=null){observedState.Complete-=OnComplete;observedState.Start-=OnTrackStart;}
        if(Speech!=null)Speech.Stop();
        if(Expression!=null)Expression.CancelTouch();
        var pointer=Character.GetComponent<CompanionPointerInteraction>();
        if(pointer!=null)pointer.Cancel();
        IsPlayingIntro=false;
        Character.AnimationState.SetAnimation(0,profile.idleAnimation,true);
        observedState=Character.AnimationState;
        observedState.Complete+=OnComplete;
        observedState.Start+=OnTrackStart;
        SetLocalBehaviorEnabled(true);
    }
    void OnTrackStart(Spine.TrackEntry entry) {
        if(IsPlayingIntro&&entry.TrackIndex==0&&entry.Animation.Name==profile.idleAnimation) {
            IsPlayingIntro=false;SetLocalBehaviorEnabled(true);
        }
    }
    void SetLocalBehaviorEnabled(bool active) {
        // Native entrance owns the entire skeleton, including its close-up framing.
        var pointer=Character==null?null:Character.GetComponent<CompanionPointerInteraction>();
        if(!active) {
            if(pointer!=null){pointer.Cancel();pointer.enabled=false;}
            if(Speech!=null){Speech.Stop();Speech.enabled=false;}
            if(Expression!=null)Expression.CancelTouch();
            if(Pat10!=null)Pat10.enabled=false;
            if(Expression!=null)Expression.enabled=false;
            if(Motion!=null)Motion.enabled=false;
        } else {
            if(Motion!=null)Motion.enabled=true;
            if(Expression!=null)Expression.enabled=true;
            if(Pat10!=null)Pat10.enabled=true;
            if(Speech!=null)Speech.enabled=true;
            if(pointer!=null)pointer.enabled=true;
        }
    }
    public bool PlayIntro() {
        if(Character==null)return false;
        var intro=string.IsNullOrEmpty(profile.introAnimation)?null:Character.Skeleton.Data.FindAnimation(profile.introAnimation);
        if(intro==null||intro.Duration<=0){SkipIntro();return false;}
        SetLocalBehaviorEnabled(false);IsPlayingIntro=true;
        Character.timeScale=1;
        Character.AnimationState.ClearTracks();Character.Skeleton.SetToSetupPose();
        Character.AnimationState.SetAnimation(0,intro.Name,false);
        Character.AnimationState.AddAnimation(0,profile.idleAnimation,true,0);
        return true;
    }
    public void SkipIntro() {
        if(Character==null||!IsPlayingIntro)return;
        IsPlayingIntro=false;
        Character.AnimationState.ClearTracks();Character.Skeleton.SetToSetupPose();
        Character.AnimationState.SetAnimation(0,profile.idleAnimation,true);
        SetLocalBehaviorEnabled(true);
    }
    void Update() {
        if(Keyboard.current!=null&&Keyboard.current.f8Key.wasPressedThisFrame)diagnosticsVisible=!diagnosticsVisible;
    }
    void OnGUI() {
        if(!diagnosticsVisible||Motion==null)return;
        GUILayout.BeginArea(new Rect(12,12,300,390),GUI.skin.box);
        if(IsPlayingIntro) {
            GUILayout.Label("Original entrance · "+profile.introAnimation);
            if(GUILayout.Button("Skip entrance"))SkipIntro();
            GUILayout.EndArea();return;
        }
        if(GUILayout.Button("Replay entrance"))PlayIntro();
        GUILayout.Label("Art validation · "+Motion.State+" · Idle loops "+IdleLoops);
        GUILayout.Label("Touch: "+Expression.TouchId+" / selections "+Expression.TouchSelectionCount);
        if(Pat10!=null) {
            bool use=GUILayout.Toggle(Pat10.UseForTouch,"10.png pat trial");
            if(use!=Pat10.UseForTouch){Expression.CancelTouch();Pat10.UseForTouch=use;}
            if(GUILayout.Button("Preview 10.png pat"))Expression.ReviewPat10();
            if(GUILayout.Button("Release pat"))Expression.EndTouch();
        }
        if(GUILayout.Button("Idle")){Speech.Stop();Motion.SetState(CompanionLocalMotion.PresentationState.Idle);}
        if(GUILayout.Button("Thinking")){Speech.Stop();Motion.SetState(CompanionLocalMotion.PresentationState.Thinking);}
        if(sampleVoices!=null)for(int i=0;i<sampleVoices.Length;i++){var clip=sampleVoices[i];if(clip!=null&&GUILayout.Button("Voice: "+clip.name))Speech.Play(clip);}
        if(GUILayout.Button("Stop voice"))Speech.Stop();
        if(GUILayout.Button(Speech.Paused?"Resume voice":"Pause voice")){if(Speech.Paused)Speech.Resume();else Speech.Pause();}
        Speech.voice.mute=GUILayout.Toggle(Speech.voice.mute,"Mute voice");
        GUILayout.Label("F8 hides this art review panel");
        GUILayout.EndArea();
    }
    void OnDestroy() {
        Application.runInBackground = previousBackground;
        if(observedState!=null){observedState.Complete-=OnComplete;observedState.Start-=OnTrackStart;}
        if(Character!=null)Character.OnAnimationRebuild-=RestoreBase;
        if (atlas != null && atlas.Materials != null)
            foreach (var material in atlas.Materials) if (material != null) Destroy(material);
        if (textures != null) foreach (var texture in textures) if (texture != null) Destroy(texture);
        if (template != null) Destroy(template);
        if (data != null) Destroy(data);
        if (atlas != null) Destroy(atlas);
        if (skeletonBytes != null) Destroy(skeletonBytes);
        if (atlasText != null) Destroy(atlasText);
    }
}
