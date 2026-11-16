using System;
using System.Collections.Generic;
using System.IO;
using Spine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Review actual Pat10 rendering and restore every temporary review setting.</summary>
public static class CompanionPat10Review {
    [Serializable] public sealed class Report {
        public string createdAt;
        public string[] passed,failed;
        public bool allPassed;
        public int sourceSlots,sourceAnimations,activeDrawSlots,regressionChecks;
        public float patRotation,dragRotationRange,maximumRestoreError;
        public float eyeSize,handSideEyeScale;
    }
    public static string Output {get{return Path.GetFullPath(Path.Combine(CompanionValidationTools.OutputDirectory,"../Pat10Trial"));}}
    static void Capture(CompanionValidationBootstrap b,string name,bool crop=false) {
        b.Character.LateUpdate();Directory.CreateDirectory(Output);
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        var rect=crop?new Rect(925,265,430,410):new Rect(0,0,1600,900);
        var texture=new Texture2D((int)rect.width,(int)rect.height,TextureFormat.RGB24,false);
        try {
            target.Create();RenderPipeline.SubmitRenderRequest(b.renderCamera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            RenderTexture.active=target;texture.ReadPixels(rect,0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());
        } finally {RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
    }
    [MenuItem("Companion Validation/Review 10.png Pat (Play Mode)")]
    static void RunFromMenu(){Debug.Log(JsonUtility.ToJson(Run(),true));}
    public static Report Run() {
        var b=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        if(!EditorApplication.isPlaying||b==null||b.Pat10==null||!b.Pat10.Ready)throw new InvalidOperationException("Play the configured validation scene first.");
        b.SkipIntro();
        var c=b.Character;var m=b.Motion;var e=b.Expression;var trial=b.Pat10;
        var pointer=c.GetComponent<CompanionPointerInteraction>();bool use=trial.UseForTouch;
        var passed=new List<string>();var failed=new List<string>();
        Action<bool,string> check=(ok,name)=>{if(ok)passed.Add(name);else failed.Add(name);};
        var report=new Report {createdAt=DateTime.UtcNow.ToString("o"),sourceSlots=c.Skeleton.Data.Slots.Count,sourceAnimations=c.Skeleton.Data.Animations.Count,
            eyeSize=trial.eyeSize,handSideEyeScale=trial.handSideEyeScale};
        b.Speech.Stop();pointer.enabled=false;e.CancelTouch();m.SetState(CompanionLocalMotion.PresentationState.Idle);m.SetReviewPose(Vector2.zero,0);
        try {
            Capture(b,"01_idle.png");float baselineFace=c.Skeleton.FindBone("Face").Rotation;
            trial.UseForTouch=true;bool started=e.BeginTouch();int selection=e.TouchSelectionCount;
            for(int i=0;i<20;i++){m.Advance(1f/60);e.Advance(1f/60);c.Update(0);}
            check(started&&e.Pat10Selected&&e.TouchId=="pat_10_trial","HeadPat selects 10.png exactly once in trial mode");
            check(trial.Ready&&trial.VisibleParts==5,"Five independent original-pixel parts are rendered");
            check(report.sourceSlots==294&&report.sourceAnimations==52&&c.Skeleton.Slots.Count==294,"Source skeleton slots and animations remain unchanged");
            report.activeDrawSlots=c.Skeleton.DrawOrder.Count;
            bool noDuplicateEyes=true;
            foreach(var slot in c.Skeleton.DrawOrder)if(slot.Data.Name.Contains("_Eye_")||slot.Data.Name.Contains("_Eyebrow"))noDuplicateEyes &= slot.A<.001f;
            check(noDuplicateEyes,"Original iris and lash layers are hidden beneath Pat10 rather than doubled");
            report.patRotation=Mathf.Abs(c.Skeleton.FindBone("Face").Rotation-baselineFace);
            check(report.patRotation>1,"Pat10 preserves the original Pat head response");
            Capture(b,"02_pat10.png");
            e.SetTouchDirection(Vector2.right);m.Advance(.3f);e.Advance(.3f);c.Update(0);float right=c.Skeleton.FindBone("Face").Rotation;
            Capture(b,"03_pat10_right.png");
            e.SetTouchDirection(Vector2.left);m.Advance(.3f);e.Advance(.3f);c.Update(0);
            report.dragRotationRange=Mathf.Abs(right-c.Skeleton.FindBone("Face").Rotation);
            check(report.dragRotationRange>.5f&&e.TouchSelectionCount==selection,"Light drag changes head response without resampling the expression");
            Capture(b,"04_pat10_left.png");
            e.SetSpeech(true,.7f);for(int i=0;i<40;i++){e.Advance(1f/60);c.Update(0);}
            check(trial.VisibleParts==4&&c.Skeleton.FindSlot(b.profile.mouthSlot).Attachment.Name==e.SpeechOpenAttachment&&c.Skeleton.FindSlot(b.profile.mouthSlot).A>.99f,
                "Speaking keeps Pat10 eyes while the native speech mouth takes control");
            Capture(b,"05_pat10_speech.png");
            e.SetSpeech(true,0f);for(int i=0;i<14;i++){e.Advance(1f/60);c.Update(0);}
            check(c.Skeleton.FindSlot(b.profile.mouthSlot).Attachment.Name==e.SpeechClosedAttachment,"Speech pause closes the native mouth during Pat10");
            e.SetSpeech(false,0f);e.EndTouch();e.Advance(.08f);c.Update(0);Capture(b,"06_release_mid.png");
            for(int i=0;i<60;i++){e.Advance(1f/60);m.Advance(1f/60);c.Update(0);}
            check(!e.TouchActive&&trial.VisibleParts==0&&c.Skeleton.DrawOrder.Count==294,"Release removes all trial parts and source fades");
            Capture(b,"07_released.png");
            e.ReviewPat10();for(int i=0;i<20;i++){e.Advance(1f/60);c.Update(0);}trial.enabled=false;c.Update(0);
            check(!trial.Ready&&trial.VisibleParts==0&&c.Skeleton.DrawOrder.Count==294,"Disabling the trial removes its render parts immediately");
            trial.enabled=true;e.CancelTouch();e.Advance(.3f);c.Update(0);
            check(trial.Ready,"Re-enabling rebuilds original-pixel trial resources");
            trial.UseForTouch=false;e.BeginTouch();check(!e.Pat10Selected,"Trial off restores original expression selection");
            e.CancelTouch();e.Advance(.3f);c.Update(0);
            trial.UseForTouch=true;e.BeginTouch();e.Advance(.3f);c.Update(0);c.Initialize(true);c.Update(0);
            check(trial.Ready&&!e.TouchActive&&trial.VisibleParts==0&&c.Skeleton.DrawOrder.Count==294,"Skeleton rebuild clears the gesture and rebinds trial resources");
            trial.UseForTouch=false;
            var regression=CompanionValidationSmoke.Run("pat10");report.regressionChecks=regression.passed.Length;
            report.maximumRestoreError=regression.maximumBaselineError;
            check(regression.allPassed,"Existing 25 behavior checks still pass with trial switched off");
        } finally {
            b.Speech.Stop();e.CancelTouch();e.SetSpeech(false,0f);trial.UseForTouch=use;
            m.ReviewGaze=null;m.allowPointer=true;m.wander=true;c.timeScale=1;pointer.enabled=true;c.Update(0);
        }
        report.passed=passed.ToArray();report.failed=failed.ToArray();report.allPassed=failed.Count==0;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"pat10_review_report.json"),JsonUtility.ToJson(report,true));
        return report;
    }
    static EditorApplication.CallbackFunction recording;
    [MenuItem("Companion Validation/Record 10.png Pat Preview (Play Mode)")]
    public static void RecordPreview() {
        if(recording!=null)throw new InvalidOperationException("Pat10 recording already running.");
        var b=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        if(!EditorApplication.isPlaying||b==null||b.Pat10==null||!b.Pat10.Ready)throw new InvalidOperationException("Play the configured validation scene first.");
        var c=b.Character;var m=b.Motion;var e=b.Expression;var trial=b.Pat10;
        var pointer=c.GetComponent<CompanionPointerInteraction>();
        bool previousPointer=pointer.enabled,previousAllow=m.allowPointer,previousWander=m.wander,previousUse=trial.UseForTouch;
        var previousGaze=m.ReviewGaze;
        var oldState=m.State;float oldTimeScale=c.timeScale;
        b.Speech.Stop();e.CancelTouch();pointer.enabled=false;m.allowPointer=false;m.wander=false;m.ReviewGaze=Vector2.zero;trial.UseForTouch=true;c.timeScale=1;
        double start=EditorApplication.timeSinceStartup,next=start;int frame=0;bool touched=false,spoke=false,released=false;
        Directory.CreateDirectory(Path.Combine(Output,"LiveFrames"));
        recording=()=>{
            bool aborted=false;
            try {
                double age=EditorApplication.timeSinceStartup-start;
                if(!EditorApplication.isPlaying||b==null)throw new InvalidOperationException("Play stopped during preview.");
                if(age>=1&&!touched){e.BeginTouch();touched=true;}
                if(touched&&!released)e.SetTouchDirection(new Vector2(Mathf.Sin((float)age*3)*.95f,0));
                if(age>=3&&!spoke){b.Speech.Play(b.sampleVoices[1]);spoke=true;}
                if(age>=6.5&&!released){b.Speech.Stop();e.EndTouch();released=true;}
                if(EditorApplication.timeSinceStartup>=next&&frame<64){Capture(b,"LiveFrames/frame_"+frame.ToString("D3")+".png",true);frame++;next=EditorApplication.timeSinceStartup+.125;}
                if(frame<64)return;
                File.WriteAllText(Path.Combine(Output,"live_preview.json"),"{\"frames\":"+frame+",\"realAudioSourcePlayed\":"+(spoke?"true":"false")+",\"scriptedGesture\":true,\"gifHasAudio\":false}");
            } catch(Exception error){aborted=true;Debug.LogException(error);} 
            finally {
                if(aborted||frame>=64||!EditorApplication.isPlaying||b==null) {
                    EditorApplication.update-=recording;recording=null;
                    if(b!=null){b.Speech.Stop();e.CancelTouch();trial.UseForTouch=previousUse;m.allowPointer=previousAllow;m.wander=previousWander;m.ReviewGaze=previousGaze;m.SetState(oldState);c.timeScale=oldTimeScale;pointer.enabled=previousPointer;c.Update(0);}
                }
            }
        };
        EditorApplication.update+=recording;
    }
}
