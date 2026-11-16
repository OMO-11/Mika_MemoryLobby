using System;
using System.IO;
using System.Collections.Generic;
using Spine;
using UnityEditor;
using UnityEngine;

public static class CompanionV02Review {
    [Serializable] public sealed class Report {
        public string createdAt;
        public string[] passed,failed;
        public bool allPassed;
        public float protectedHeadMaximumError,irisDisplacement,patTilt,dragRotationRange,mouthWidthLow,mouthWidthHigh;
        public int midpointDrawSlots,baselineSlots;
    }
    [Serializable] public sealed class HitGeometry {public Region[] regions;}
    [Serializable] public sealed class Region {public string name;public float[] pixels;public int[] triangles;}
    [MenuItem("Companion Validation/Review v0.2 (Play Mode)")]
    static void RunFromMenu(){Debug.Log(JsonUtility.ToJson(Run(),true));}
    public static Report Run() {
        var b=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        if(!EditorApplication.isPlaying||b==null||b.Character==null)throw new InvalidOperationException("Play the validation scene first.");
        b.SkipIntro();
        var c=b.Character;var m=b.Motion;var e=b.Expression;var p=c.GetComponent<CompanionPointerInteraction>();
        b.Speech.Stop();p.enabled=false;e.Bind(c);m.Bind(c);m.SetState(CompanionLocalMotion.PresentationState.Idle);
        m.SetReviewPose(Vector2.zero,0);
        var passed=new List<string>();var failed=new List<string>();
        Action<bool,string> check=(ok,name)=>{if(ok)passed.Add(name);else failed.Add(name);};
        var report=new Report {createdAt=DateTime.UtcNow.ToString("o"),baselineSlots=c.Skeleton.Slots.Count};
        var reference=new Skeleton(c.Skeleton.Data);
        c.Skeleton.Data.FindAnimation(b.profile.idleAnimation).Apply(reference,-1,0,true,null,1,MixBlend.Replace,MixDirection.In);
        reference.UpdateWorldTransform(Skeleton.Physics.Pose);
        m.ReviewGaze=new Vector2(.45f,.2f);
        for(int i=0;i<60;i++){m.Advance(1f/30);e.Advance(1f/30);c.Update(0);}
        foreach(string name in new[]{"Face","Head_Rot","Head_back","Ball_Hair_01","L_ForeArm_01_pos","F_Hair_03"}) {
            var actual=c.Skeleton.FindBone(name);var original=reference.FindBone(name);
            report.protectedHeadMaximumError=Mathf.Max(report.protectedHeadMaximumError,Mathf.Abs(actual.WorldX-original.WorldX),Mathf.Abs(actual.WorldY-original.WorldY));
        }
        var iris=c.Skeleton.FindBone("L_Eye_idle_01");var originalIris=reference.FindBone("L_Eye_idle_01");
        report.irisDisplacement=Vector2.Distance(new Vector2(iris.WorldX,iris.WorldY),new Vector2(originalIris.WorldX,originalIris.WorldY));
        check(report.protectedHeadMaximumError<.0001f&&Mathf.Abs(m.AppliedHead)<.00001f,"Idle attention leaves head, hair and supporting arm at original pose");
        check(report.irisDisplacement>.01f,"Idle attention visibly moves the original iris controls");
        CompanionValidationTools.Capture("v02_idle_eyes_only.png");
        m.SetReviewPose(Vector2.zero,0);float baselineFace=c.Skeleton.FindBone("Face").Rotation;
        e.ReviewExpression(0);e.SetTouchDirection(Vector2.right);m.Advance(.3f);e.Advance(.3f);c.Update(0);
        float right=c.Skeleton.FindBone("Face").Rotation;report.patTilt=baselineFace-right;
        CompanionValidationTools.Capture("v02_pat_right.png");
        e.SetTouchDirection(Vector2.left);m.Advance(.3f);e.Advance(.3f);c.Update(0);
        float left=c.Skeleton.FindBone("Face").Rotation;report.dragRotationRange=Mathf.Abs(right-left);
        CompanionValidationTools.Capture("v02_pat_left.png");
        check(Mathf.Abs(report.patTilt)>1,"Original Pat moves Face, rather than only changing facial attachments");
        check(report.dragRotationRange>.5f,"Drag direction changes the original Pat response");
        e.EndTouch();e.Advance(.4f);m.Advance(.4f);c.Update(0);
        check(!e.TouchActive&&Mathf.Abs(c.Skeleton.FindBone("Face").Rotation-baselineFace)<.0001f,"Original PatEnd releases back to the current Idle pose");
        e.Bind(c);c.Update(0);e.ReviewExpression(1);c.Update(0);e.Advance(.08f);c.Update(0);
        report.midpointDrawSlots=c.Skeleton.DrawOrder.Count;
        check(e.ActiveFadeCount>0&&report.midpointDrawSlots>report.baselineSlots,"Expression midpoint has a real fade in the source draw order");
        CompanionValidationTools.Capture("v02_expression_mid.png");
        for(int i=0;i<16;i++){e.Advance(1f/60);c.Update(0);}
        check(c.Skeleton.DrawOrder.Count==report.baselineSlots,"Completed expression fade removes temporary draw slots");
        foreach(float amount in new[]{.15f,.45f,.9f}) {
            e.SetSpeech(true,amount);c.Update(0);e.Advance(.12f);c.Update(0);
            CompanionValidationTools.Capture("v02_mouth_"+Mathf.RoundToInt(amount*100)+".png");
        }
        check(c.Skeleton.FindSlot(b.profile.mouthSlot).Attachment.Name==e.SpeechOpenAttachment,"Pat speech retains the selected expression's mouth family");
        e.CancelTouch();e.SetSpeech(true,.15f);c.Update(0);e.Advance(.1f);c.Update(0);
        report.mouthWidthLow=c.Skeleton.FindBone("Mouse_normal").ScaleX;
        e.SetSpeech(true,.9f);c.Update(0);report.mouthWidthHigh=c.Skeleton.FindBone("Mouse_normal").ScaleX;
        check(report.mouthWidthHigh-report.mouthWidthLow>.1f,"Speech energy produces continuous mouth width variation");
        e.SetSpeech(true,0f);c.Update(0);e.Advance(.025f);c.Update(0);
        check(e.ActiveFadeCount>0,"Closing the mouth blends rather than replacing it in one frame");
        CompanionValidationTools.Capture("v02_mouth_closing.png");
        e.SetSpeech(false,0f);c.Update(0);e.Advance(.2f);c.Update(0);e.enabled=false;
        check(c.Skeleton.DrawOrder.Count==report.baselineSlots,"Disabling facial control restores source draw order without leftover fades");
        e.enabled=true;m.SetReviewPose(Vector2.zero,0);c.Update(0);
        var geometry=new List<Region>();int tested=0;bool allHit=true;
        foreach(string name in b.profile.headHitSlots) {
            var slot=c.Skeleton.FindSlot(name);var mesh=slot.Attachment as MeshAttachment;
            var vertices=new float[mesh.WorldVerticesLength];mesh.ComputeWorldVertices(slot,0,vertices.Length,vertices,0,2);
            var pixels=new float[vertices.Length];
            for(int i=0;i<vertices.Length;i+=2) {
                var viewport=b.renderCamera.WorldToViewportPoint(c.transform.TransformPoint(new Vector3(vertices[i],vertices[i+1],0)));
                pixels[i]=viewport.x*1600;pixels[i+1]=(1-viewport.y)*900;
            }
            geometry.Add(new Region {name=name,pixels=pixels,triangles=mesh.Triangles});
            for(int i=0;i<mesh.Triangles.Length;i+=Mathf.Max(3,(mesh.Triangles.Length/12/3)*3)) {
                int a=mesh.Triangles[i]*2,d=mesh.Triangles[i+1]*2,f=mesh.Triangles[i+2]*2;
                var local=new Vector3((vertices[a]+vertices[d]+vertices[f])/3,(vertices[a+1]+vertices[d+1]+vertices[f+1])/3,0);
                var screen=b.renderCamera.WorldToScreenPoint(c.transform.TransformPoint(local));
                allHit&=p.HitHead(screen);tested++;
            }
        }
        check(allHit&&tested>20,"Visible face, scalp and bun mesh samples are included in the head hit area");
        check(!p.HitHead(new Vector2(Screen.width*.1f,Screen.height*.1f)),"Background remains outside the head hit area");
        CompanionValidationTools.Capture("v02_hit_base.png");
        File.WriteAllText(Path.Combine(CompanionValidationTools.OutputDirectory,"v02_head_hit_geometry.json"),JsonUtility.ToJson(new HitGeometry {regions=geometry.ToArray()},true));
        m.ReviewGaze=null;m.allowPointer=true;m.wander=true;c.timeScale=1;p.enabled=true;e.CancelTouch();
        report.passed=passed.ToArray();report.failed=failed.ToArray();report.allPassed=failed.Count==0;
        File.WriteAllText(Path.Combine(CompanionValidationTools.OutputDirectory,"v02_improvement_report.json"),JsonUtility.ToJson(report,true));
        return report;
    }
}
