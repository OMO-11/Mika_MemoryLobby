using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>Original Pat motion and one facial writer; fades preserve the source draw order.</summary>
[DefaultExecutionOrder(-30)]
public sealed class CompanionExpressionOverlay : MonoBehaviour {
    public CompanionBehaviorProfile profile;
    public bool TouchActive { get; private set; }
    public int TouchSelectionCount { get; private set; }
    public int TouchIndex { get; private set; } = -1;
    public float AppliedPatWeight { get; private set; }
    public int ActiveFadeCount { get; private set; }
    public Vector2 TouchDirection { get; private set; }
    public CompanionPat10Trial Pat10Trial { get; set; }
    public bool Pat10Selected { get; private set; }
    public float Clock { get { return clock; } }
    public bool SpeechActive { get { return speechActive; } }
    public float Pat10Visibility { get { return TouchActive&&Pat10Selected ?
        Mathf.SmoothStep(0,1,(clock-touchStart-.06f)/.12f)*(releaseAt<0?1:1-Mathf.SmoothStep(0,1,(clock-releaseAt)/.18f)) : 0; } }
    public string TouchId { get { return TouchIndex<0?"":Pat10Selected?"pat_10_trial":profile.touchExpressions[TouchIndex].id; } }
    public Vector2 TouchGaze { get { return TouchIndex<0?Vector2.zero:profile.touchExpressions[TouchIndex].gazeBias; } }
    public string SpeechOpenAttachment { get { var f=SelectedFamily();return f==null?"":f.open.Name; } }
    public string SpeechClosedAttachment { get { var f=SelectedFamily();return f==null?"":f.closed.Name; } }
    sealed class SlotBinding {
        public Slot slot, ghost;
        public Attachment original, desired, from, to;
        public float originalAlpha, originalR, originalG, originalB, fadeAt;
        public bool captured, initialized, inserted;
        public readonly ExposedList<float> originalDeform=new ExposedList<float>();
        public readonly ExposedList<float> fromDeform=new ExposedList<float>();
    }
    sealed class BoneBinding { public Bone bone; public float x,y,rotation,scaleX,scaleY; public bool captured; }
    sealed class SettingBinding { public SlotBinding slot; public Attachment attachment; public bool mouth, eye; }
    sealed class MouthFamily { public Attachment closed, open; public BoneBinding bone; }
    readonly Dictionary<string,SlotBinding> slots=new Dictionary<string,SlotBinding>();
    readonly Dictionary<int,BoneBinding> bones=new Dictionary<int,BoneBinding>();
    readonly List<int> eligible=new List<int>();
    SettingBinding[][] touch, thought;
    SettingBinding[] blink;
    int displayedExpression=-1,requestedExpression=-1;
    float blinkStarted=-1;
    MouthFamily defaultMouth;
    MouthFamily[] touchMouths;
    SkeletonAnimation character, attachedCharacter;
    CompanionLocalMotion motion;
    SlotBinding mouthSlot;
    Spine.Animation pat, patEnd;
    System.Random random=new System.Random();
    float clock,touchStart,releaseAt=-1,tapEnd=-1,speechAmount;
    bool speechActive;
    public void Bind(SkeletonAnimation value) {
        Unbind();attachedCharacter=value;character=value;
        motion=GetComponent<CompanionLocalMotion>();
        touch=BindSettings(profile.touchExpressions);thought=BindSettings(profile.thinkingExpressions);
        blink=BindSettings(new[]{new CompanionBehaviorProfile.ExpressionSetting {attachments=profile.blinkAttachments}})[0];
        mouthSlot=GetSlot(profile.mouthSlot);defaultMouth=BindMouth(profile.closedMouth,profile.openMouth);
        touchMouths=new MouthFamily[touch.Length];
        for(int i=0;i<touch.Length;i++) {
            string closed=profile.closedMouth;
            foreach(var setting in touch[i])if(setting.mouth&&setting.attachment!=null)closed=setting.attachment.Name;
            string open=profile.touchExpressions[i].openMouth;
            touchMouths[i]=string.IsNullOrEmpty(open)?defaultMouth:BindMouth(closed,open);
            if(profile.touchExpressions[i].enabled&&touch[i].Length>0)eligible.Add(i);
        }
        pat=string.IsNullOrEmpty(profile.touchMotionAnimation)?null:character.Skeleton.Data.FindAnimation(profile.touchMotionAnimation);
        patEnd=string.IsNullOrEmpty(profile.touchReleaseAnimation)?null:character.Skeleton.Data.FindAnimation(profile.touchReleaseAnimation);
        CacheMotion(pat);CacheMotion(patEnd);
        character.BeforeApply+=Restore;character.UpdateLocal+=Apply;character.OnAnimationRebuild+=Rebuild;
    }
    void CacheMotion(Spine.Animation animation) {
        if(animation==null)return;
        foreach(var timeline in animation.Timelines) {
            var bone=timeline as IBoneTimeline;if(bone!=null)GetBone(bone.BoneIndex);
            var slot=timeline as ISlotTimeline;if(slot!=null)GetSlot(character.Skeleton.Slots.Items[slot.SlotIndex].Data.Name);
        }
    }
    BoneBinding GetBone(int index) {
        BoneBinding value;if(bones.TryGetValue(index,out value))return value;
        value=new BoneBinding {bone=character.Skeleton.Bones.Items[index]};bones.Add(index,value);return value;
    }
    MouthFamily BindMouth(string closed,string open) {
        if(mouthSlot==null)return null;
        var family=new MouthFamily {closed=character.Skeleton.GetAttachment(mouthSlot.slot.Data.Index,closed),
            open=character.Skeleton.GetAttachment(mouthSlot.slot.Data.Index,open)};
        if(family.closed==null||family.open==null)return defaultMouth;
        var mesh=family.open as MeshAttachment;
        if(mesh!=null&&mesh.Bones!=null&&mesh.Bones.Length>1) {
            int index=mesh.Bones[1];bool single=true;
            for(int i=0;i<mesh.Bones.Length;) {int count=mesh.Bones[i++];for(int j=0;j<count;j++)single&=mesh.Bones[i++]==index;}
            if(single)family.bone=GetBone(index);
        }
        return family;
    }
    MouthFamily SelectedFamily(){return TouchActive&&TouchIndex>=0?touchMouths[TouchIndex]:defaultMouth;}
    SlotBinding GetSlot(string name) {
        SlotBinding value;if(slots.TryGetValue(name,out value))return value;
        var slot=character.Skeleton.FindSlot(name);
        if(slot==null){Debug.LogWarning("Missing profile slot: "+name);return null;}
        value=new SlotBinding {slot=slot,ghost=new Slot(slot,slot.Bone)};slots.Add(name,value);return value;
    }
    SettingBinding[][] BindSettings(CompanionBehaviorProfile.ExpressionSetting[] settings) {
        var result=new SettingBinding[settings.Length][];
        for(int i=0;i<settings.Length;i++) {
            var list=new List<SettingBinding>();
            foreach(var item in settings[i].attachments) {
                var slot=GetSlot(item.slot);if(slot==null)continue;
                var attachment=string.IsNullOrEmpty(item.attachment)?null:character.Skeleton.GetAttachment(slot.slot.Data.Index,item.attachment);
                if(!string.IsNullOrEmpty(item.attachment)&&attachment==null){Debug.LogWarning("Missing profile attachment: "+item.attachment);continue;}
                list.Add(new SettingBinding {slot=slot,attachment=attachment,mouth=item.slot==profile.mouthSlot,
                    eye=item.slot.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0&&item.slot.IndexOf("T_Eyebrow",StringComparison.OrdinalIgnoreCase)<0});
            }
            result[i]=list.ToArray();
        }
        return result;
    }
    public bool BeginTouch(bool tap=false) {
        if((TouchActive&&releaseAt<0)||eligible.Count==0)return false;
        Pat10Selected=Pat10Trial!=null&&Pat10Trial.Ready&&Pat10Trial.UseForTouch;
        TouchIndex=Pat10Selected?0:eligible[random.Next(eligible.Count)];TouchSelectionCount++;
        TouchActive=true;touchStart=clock;releaseAt=-1;tapEnd=tap?clock+.28f:-1;TouchDirection=Vector2.zero;return true;
    }
    public void SetTouchDirection(Vector2 normalized){TouchDirection=Vector2.ClampMagnitude(normalized,1);}
    public void EndTouch(){if(TouchActive&&releaseAt<0)releaseAt=clock;}
    public void SetSeed(int seed){random=new System.Random(seed);}
    public void ReviewExpression(int index){Pat10Selected=false;TouchIndex=Mathf.Clamp(index,0,touch.Length-1);TouchActive=true;touchStart=clock-1;releaseAt=-1;tapEnd=-1;}
    public bool ReviewPat10(){if(Pat10Trial==null||!Pat10Trial.Ready)return false;ReviewExpression(0);Pat10Selected=true;return true;}
    public void SetSpeech(bool active,bool open){SetSpeech(active,open?1f:0f);}
    public void SetSpeech(bool active,float amount){speechActive=active;speechAmount=Mathf.Clamp01(amount);}
    public void Advance(float dt) {
        clock+=dt;if(tapEnd>=0&&clock>=tapEnd)EndTouch();
        float duration=patEnd==null?.333f:patEnd.Duration;
        if(releaseAt>=0&&clock-releaseAt>=duration)CancelTouch();
    }
    void Update(){Advance(Time.unscaledDeltaTime);}
    static void CopyDeform(ExposedList<float> source,ExposedList<float> destination){destination.Resize(source.Count);Array.Copy(source.Items,destination.Items,source.Count);}
    void Restore(ISkeletonAnimation _) {
        if(Pat10Trial!=null)Pat10Trial.Restore();
        foreach(var binding in slots.Values) {
            if(binding.inserted&&character!=null){character.Skeleton.DrawOrder.Remove(binding.ghost);binding.inserted=false;}
            if(!binding.captured)continue;
            binding.slot.Attachment=binding.original;binding.slot.A=binding.originalAlpha;
            binding.slot.R=binding.originalR;binding.slot.G=binding.originalG;binding.slot.B=binding.originalB;
            CopyDeform(binding.originalDeform,binding.slot.Deform);binding.captured=false;
        }
        foreach(var binding in bones.Values)if(binding.captured) {
            binding.bone.X=binding.x;binding.bone.Y=binding.y;binding.bone.Rotation=binding.rotation;
            binding.bone.ScaleX=binding.scaleX;binding.bone.ScaleY=binding.scaleY;binding.captured=false;
        }
    }
    void Capture() {
        foreach(var binding in slots.Values) {
            binding.original=binding.slot.Attachment;binding.desired=binding.original;binding.originalAlpha=binding.slot.A;
            binding.originalR=binding.slot.R;binding.originalG=binding.slot.G;binding.originalB=binding.slot.B;
            CopyDeform(binding.slot.Deform,binding.originalDeform);binding.captured=true;
        }
        foreach(var binding in bones.Values) {
            var bone=binding.bone;binding.x=bone.X;binding.y=bone.Y;binding.rotation=bone.Rotation;
            binding.scaleX=bone.ScaleX;binding.scaleY=bone.ScaleY;binding.captured=true;
        }
    }
    bool OriginalBlink() {
        foreach(var binding in slots.Values)if(binding.original!=null&&binding.original.Name.IndexOf("eyeclose",StringComparison.OrdinalIgnoreCase)>=0)return true;
        return false;
    }
    void Choose(SettingBinding[] setting,bool preserveBlink) {
        foreach(var item in setting)if(!(speechActive&&item.mouth)&&!(preserveBlink&&item.eye))item.slot.desired=item.attachment;
    }
    void Apply(ISkeletonAnimation _) {
        Capture();bool baseBlink=OriginalBlink();AppliedPatWeight=0;
        if(TouchActive&&TouchIndex>=0) {
            float entry=Mathf.SmoothStep(0,1,(clock-touchStart)/.18f);
            if(releaseAt<0) {
                AppliedPatWeight=entry*(tapEnd>=0?1:Mathf.Clamp(.8f+TouchDirection.x*.2f,.6f,1));
                if(pat!=null)pat.Apply(character.Skeleton,-1,0,false,null,AppliedPatWeight,MixBlend.Replace,MixDirection.In);
            } else {
                float age=clock-releaseAt,duration=patEnd==null?.333f:patEnd.Duration;
                AppliedPatWeight=1-Mathf.SmoothStep(0,1,age/duration);
                if(patEnd!=null)patEnd.Apply(character.Skeleton,-1,Mathf.Min(age,duration),false,null,AppliedPatWeight,MixBlend.Replace,MixDirection.In);
            }
        }
        int expression=TouchActive&&TouchIndex>=0&&(releaseAt<0||clock-releaseAt<.08f)?TouchIndex:
            motion!=null&&motion.State==CompanionLocalMotion.PresentationState.Thinking&&thought.Length>0&&profile.thinkingExpressions[0].enabled?100:-1;
        if(expression!=requestedExpression){requestedExpression=expression;blinkStarted=clock;}
        float blinkAge=blinkStarted<0?1:clock-blinkStarted;
        if(blinkAge>=.06f)displayedExpression=requestedExpression;
        if(displayedExpression>=0&&displayedExpression<touch.Length)
            Choose(touch[displayedExpression],baseBlink&&!profile.touchExpressions[displayedExpression].closedEyes);
        else if(displayedExpression==100)Choose(thought[0],baseBlink);
        // Switch iris families only beneath opaque original closed-eye covers. No doubled pupils.
        if(blinkAge<.11f)Choose(blink,false);
        if(speechActive&&mouthSlot!=null) {
            var family=SelectedFamily();
            if(family!=null) {
                mouthSlot.desired=speechAmount>.01f?family.open:family.closed;
                if(speechAmount>.01f&&family.bone!=null) {
                    family.bone.bone.ScaleX=family.bone.scaleX*Mathf.Lerp(.72f,1,Mathf.Sqrt(speechAmount));
                    family.bone.bone.ScaleY=family.bone.scaleY*Mathf.Lerp(.94f,1,speechAmount);
                }
            }
        }
        ActiveFadeCount=0;
        foreach(var binding in slots.Values) {
            string name=binding.slot.Data.Name;
            bool iris=name.IndexOf("_Eye_",StringComparison.OrdinalIgnoreCase)>=0;
            bool cover=name.IndexOf("_Eyebrow_idle",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("_Eyelid",StringComparison.OrdinalIgnoreCase)>=0;
            Fade(binding,iris?0:binding==mouthSlot?profile.mouthBlendSeconds:cover?.055f:profile.expressionBlendSeconds);
        }
    }
    void Fade(SlotBinding binding,float duration) {
        float alpha=binding.slot.A;
        if(duration<=0){binding.from=binding.to=binding.desired;binding.initialized=true;binding.slot.Attachment=binding.desired;binding.slot.A=alpha;return;}
        if(!binding.initialized){binding.from=binding.to=binding.original;binding.initialized=true;}
        if(binding.desired!=binding.to) {
            float prior=Mathf.SmoothStep(0,1,(clock-binding.fadeAt)/Mathf.Max(.001f,duration));
            binding.from=prior<.5f&&binding.from!=binding.to?binding.from:binding.to;
            CopyDeform(binding.original==binding.from?binding.originalDeform:binding.slot.Deform,binding.fromDeform);
            binding.to=binding.desired;binding.fadeAt=clock;
        }
        float blend=Mathf.SmoothStep(0,1,(clock-binding.fadeAt)/Mathf.Max(.001f,duration));
        binding.slot.Attachment=binding.to;
        if(binding.from==binding.to||blend>=1){binding.slot.A=alpha;return;}
        binding.slot.A=alpha*blend;
        if(binding.from!=null) {
            var ghost=binding.ghost;ghost.Attachment=binding.from;ghost.R=binding.slot.R;ghost.G=binding.slot.G;ghost.B=binding.slot.B;
            ghost.A=alpha*(1-blend);ghost.R2=binding.slot.R2;ghost.G2=binding.slot.G2;ghost.B2=binding.slot.B2;
            CopyDeform(binding.fromDeform,ghost.Deform);
            var order=character.Skeleton.DrawOrder;int index=order.IndexOf(binding.slot);
            if(index>=0){order.Insert(index,ghost);binding.inserted=true;}
        }
        ActiveFadeCount++;
    }
    public void CancelTouch(){TouchActive=false;Pat10Selected=false;TouchIndex=-1;releaseAt=-1;tapEnd=-1;TouchDirection=Vector2.zero;}
    void Unbind() {
        Restore(null);
        if(character!=null){character.BeforeApply-=Restore;character.UpdateLocal-=Apply;character.OnAnimationRebuild-=Rebuild;}
        slots.Clear();bones.Clear();eligible.Clear();character=null;CancelTouch();speechActive=false;defaultMouth=null;
        displayedExpression=requestedExpression=-1;blinkStarted=-1;
    }
    void Rebuild(ISkeletonAnimation _){if(attachedCharacter!=null)Bind(attachedCharacter);}
    void OnEnable(){if(attachedCharacter!=null&&character==null)Bind(attachedCharacter);}
    void OnDisable(){Unbind();}
}
