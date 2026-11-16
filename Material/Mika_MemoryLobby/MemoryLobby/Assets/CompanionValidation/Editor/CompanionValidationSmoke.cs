using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Spine;
using UnityEngine;

public static class CompanionValidationSmoke {
    [UnityEditor.MenuItem("Companion Validation/Run Behavior Smoke (Play Mode)")]
    static void RunFromMenu() {
        var report=Run();Debug.Log("Behavior smoke: "+(report.allPassed?"passed":"failed")+". See smoke_report.json.");
    }
    [Serializable] public sealed class Report {
        public string createdAt, unityVersion;
        public string[] passed, failed;
        public int simulatedFrames, randomDraws;
        public float simulatedSeconds, maximumBaselineError;
        public float frameRateGazeDifference, frameRateHeadDifference;
        public string[] randomVariants;
        public int sourceAnimationCount;
        public bool allPassed;
    }
    public static Report Run(string outputPrefix="v02") {
        var b=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        if(!UnityEditor.EditorApplication.isPlaying || b==null || b.Motion==null)
            throw new InvalidOperationException("Play the configured validation scene first.");
        b.SkipIntro();
        var m=b.Motion;var e=b.Expression;var character=b.Character;
        b.Speech.Stop();e.CancelTouch();e.SetSpeech(false,false);
        var pointer=character.GetComponent<CompanionPointerInteraction>();pointer.enabled=false;
        m.allowPointer=false;m.wander=false;m.ReviewGaze=null;
        m.SetState(CompanionLocalMotion.PresentationState.Idle);
        character.timeScale=1;
        var passed=new List<string>();var failed=new List<string>();
        Action<bool,string> check=(condition,name)=>{if(condition)passed.Add(name);else failed.Add(name);};
        var report=new Report {createdAt=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,sourceAnimationCount=character.Skeleton.Data.Animations.Count};
        check(report.sourceAnimationCount==52,"Original 52 animations retained; no generated Spine animation");
        e.SetSeed(17);var ids=new HashSet<string>();
        for(int i=0;i<120;i++) {
            bool started=e.BeginTouch();ids.Add(e.TouchId);
            int count=e.TouchSelectionCount;e.Advance(.5f);
            checkNoRepeatedSample(e,count);
            if(!started)failed.Add("Touch random sample rejected");
            e.EndTouch();e.Advance(.4f);
        }
        report.randomDraws=120;report.randomVariants=ids.ToArray();
        check(ids.Count==3,"Random pool covers all three original expression variants");
        check(!e.TouchActive,"Release clears touch state");
        float x,y;var head=character.Skeleton.FindBone(b.profile.headBone);
        head.LocalToWorld(b.profile.headHitCenter.x,b.profile.headHitCenter.y,out x,out y);
        Vector2 center=b.renderCamera.WorldToScreenPoint(character.transform.TransformPoint(new Vector3(x,y,0)));
        check(pointer.HitHead(center),"Head hit area follows current bone pose");
        check(!pointer.HitHead(new Vector2(-500,-500)),"Outside head hit area rejected");
        Vector3 originalScale=character.transform.localScale;
        character.transform.localScale=new Vector3(-.8f,.8f,1);
        Vector2 mirroredCenter=b.renderCamera.WorldToScreenPoint(character.transform.TransformPoint(new Vector3(x,y,0)));
        check(pointer.HitHead(mirroredCenter),"Head hit mapping survives scaling and horizontal mirroring");
        character.transform.localScale=originalScale;
        int initialCount=e.TouchSelectionCount;
        pointer.Process(center,true,true,false,.01f);
        pointer.Process(center,false,false,true,.08f);
        check(e.TouchSelectionCount==initialCount+1&&!pointer.Patting,"Short click selects once without sustained pat");
        e.EndTouch();e.Advance(.4f);pointer.Process(center,false,false,false,.4f);
        pointer.Process(center,true,true,false,.01f);
        pointer.Process(center,false,true,false,.4f);
        check(!pointer.Patting,"Holding without dragging does not start pat");
        pointer.Process(center+new Vector2(28,0),false,true,false,.04f);
        int selectionCount=e.TouchSelectionCount;
        check(pointer.Patting,"Hold plus light drag starts pat");
        for(int i=0;i<20;i++)pointer.Process(center+new Vector2(i%2==0?24:28,0),false,true,false,.03f);
        check(e.TouchSelectionCount==selectionCount,"Continuous dragging retains one selected expression");
        pointer.Cancel();e.Advance(.4f);
        check(!pointer.Pressed&&!e.TouchActive,"Input cancellation releases pat");
        pointer.Process(center,false,false,false,.4f);
        pointer.Process(center,true,true,false,.01f);
        pointer.Process(center+new Vector2(28,0),false,true,false,.4f);
        pointer.SendMessage("OnApplicationFocus",false,SendMessageOptions.RequireReceiver);e.Advance(.4f);
        check(!pointer.Pressed&&!pointer.Patting&&!e.TouchActive,"Focus loss releases pointer capture and touch");
        pointer.Process(center,false,false,false,.4f);pointer.Process(center,true,true,false,.01f);
        pointer.Process(center+new Vector2(28,0),false,true,false,.4f);
        pointer.Process(new Vector2(-500,-500),false,true,false,.01f);e.Advance(.4f);
        check(!pointer.Pressed&&!pointer.Patting&&!e.TouchActive,"Dragging beyond head tolerance releases pat");
        int cancelledCount=e.TouchSelectionCount;
        pointer.Process(center,false,true,false,.4f);
        check(!pointer.Pressed&&e.TouchSelectionCount==cancelledCount,"Returning while still held does not restart a cancelled gesture");
        m.SetState(CompanionLocalMotion.PresentationState.Thinking);
        e.BeginTouch();e.EndTouch();e.Advance(.4f);
        check(m.State==CompanionLocalMotion.PresentationState.Thinking,"Touch release preserves active Thinking");
        m.SetState(CompanionLocalMotion.PresentationState.Speaking);
        e.BeginTouch();e.Advance(.2f);e.SetSpeech(true,true);character.Update(0);
        check(character.Skeleton.FindSlot(b.profile.mouthSlot).Attachment.Name==e.SpeechOpenAttachment,"Speech mouth wins during pat");
        e.SetSpeech(true,false);character.Update(0);
        check(character.Skeleton.FindSlot(b.profile.mouthSlot).Attachment.Name==e.SpeechClosedAttachment,"Paused speech can close mouth during pat");
        e.CancelTouch();e.SetSpeech(false,false);m.SetState(CompanionLocalMotion.PresentationState.Idle);
        m.Bind(character);m.SetState(CompanionLocalMotion.PresentationState.Thinking);
        for(int i=0;i<120;i++)m.Advance(1f/60);
        character.Update(0);float sideMagnitude=m.AppliedGaze.magnitude;
        for(int i=0;i<60;i++)m.Advance(1f/60);
        character.Update(0);
        check(sideMagnitude>.2f&&m.AppliedGaze.magnitude<sideMagnitude*.3f,"Thinking visibly looks aside then returns gaze toward user");
        float thinkingHead=m.AppliedHead;m.SetState(CompanionLocalMotion.PresentationState.Speaking);m.Advance(.25f);character.Update(0);
        check(m.AppliedHead<thinkingHead&&m.State==CompanionLocalMotion.PresentationState.Speaking,"Speaking interrupts Thinking without waiting for its cycle");
        m.SetState(CompanionLocalMotion.PresentationState.Idle);
        m.Bind(character);m.SetState(CompanionLocalMotion.PresentationState.Thinking);
        for(int i=0;i<15;i++)m.Advance(1f/15);
        character.Update(0);Vector2 gaze15=m.AppliedGaze;float head15=m.AppliedHead;
        m.Bind(character);m.SetState(CompanionLocalMotion.PresentationState.Idle);m.SetState(CompanionLocalMotion.PresentationState.Thinking);
        for(int i=0;i<60;i++)m.Advance(1f/60);
        character.Update(0);
        report.frameRateGazeDifference=(m.AppliedGaze-gaze15).magnitude;
        report.frameRateHeadDifference=Mathf.Abs(m.AppliedHead-head15);
        check(report.frameRateGazeDifference<.001f&&report.frameRateHeadDifference<.001f,"15 Hz and 60 Hz smoothing reach equivalent gaze and head targets");
        m.SetState(CompanionLocalMotion.PresentationState.Idle);
        const int frames=18000;const float dt=1f/30;
        bool finite=true;float maxError=0;
        for(int i=0;i<frames;i++) {
            m.ReviewGaze=new Vector2(Mathf.Sin(i*.017f)*.48f,Mathf.Cos(i*.013f)*.34f);
            m.Advance(dt);e.Advance(dt);character.Update(dt);
            if(i%900==0) {
                foreach(var bone in character.Skeleton.Bones)
                    finite &= !float.IsNaN(bone.WorldX)&&!float.IsInfinity(bone.WorldX)&&!float.IsNaN(bone.WorldY)&&!float.IsInfinity(bone.WorldY);
            }
        }
        report.simulatedFrames=frames;report.simulatedSeconds=frames*dt;
        m.enabled=false;character.Update(0);
        var reference=new Skeleton(character.Skeleton.Data);
        character.Skeleton.Data.FindAnimation(b.profile.idleAnimation).Apply(reference,-1,
            character.AnimationState.GetCurrent(0).TrackTime,true,null,1,MixBlend.Replace,MixDirection.In);
        reference.UpdateWorldTransform(Skeleton.Physics.Pose);
        for(int i=0;i<reference.Bones.Count;i++) {
            var actual=character.Skeleton.Bones.Items[i];var original=reference.Bones.Items[i];
            maxError=Mathf.Max(maxError,Mathf.Abs(actual.WorldX-original.WorldX),Mathf.Abs(actual.WorldY-original.WorldY));
        }
        report.maximumBaselineError=maxError;
        check(finite,"600 second / 30 Hz simulation has finite bone transforms");
        check(maxError<.0001f,"Disabling local control returns all bones to original Idle baseline");
        m.enabled=true;check(m.Character!=null,"Re-enabling local controller restores bindings");
        e.BeginTouch();e.Advance(.4f);b.Speech.Play(b.sampleVoices[0]);
        character.Initialize(true);character.Update(0);
        check(character.AnimationState.GetCurrent(0)!=null && character.AnimationState.GetCurrent(0).Animation.Name==b.profile.idleAnimation,
            "Skeleton rebuild restores original Idle playback");
        check(!e.TouchActive&&!b.Speech.MouthOpen&&b.Speech.voice.clip==null&&m.State==CompanionLocalMotion.PresentationState.Idle,
            "Skeleton rebuild clears old touch and speech state");
        m.ReviewGaze=null;m.allowPointer=true;m.wander=true;pointer.enabled=true;
        m.SetState(CompanionLocalMotion.PresentationState.Idle);
        report.passed=passed.ToArray();report.failed=failed.ToArray();report.allPassed=failed.Count==0;
        Directory.CreateDirectory(CompanionValidationTools.OutputDirectory);
        File.WriteAllText(Path.Combine(CompanionValidationTools.OutputDirectory,outputPrefix+"_smoke_report.json"),JsonUtility.ToJson(report,true));
        File.WriteAllText(Path.Combine(CompanionValidationTools.OutputDirectory,outputPrefix+"_behavior_profile_snapshot.json"),JsonUtility.ToJson(b.profile,true));
        return report;
    }
    static void checkNoRepeatedSample(CompanionExpressionOverlay expression,int count) {
        expression.BeginTouch();
        if(expression.TouchSelectionCount!=count)throw new InvalidOperationException("Repeated touch selection during held gesture.");
    }
}
