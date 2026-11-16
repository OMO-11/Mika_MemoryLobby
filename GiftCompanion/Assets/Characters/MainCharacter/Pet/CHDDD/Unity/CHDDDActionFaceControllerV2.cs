using System;
using UnityEngine;

/// <summary>Sample action-specific eyelids, eyebrows and mouth after skeletal animation evaluation.</summary>
[DefaultExecutionOrder(90)]
public sealed class CHDDDActionFaceControllerV2 : MonoBehaviour
{
    [Serializable] public class Sample {
        public float blink,sad,annoyed,sleepy,gazeX,gazeY,browLift;
        public float[] mouthWeights;
    }
    [Serializable] public class Profile { public string name; public float durationSeconds; public bool loop; public Sample[] samples; }
    [Serializable] public class Profiles { public float fps; public int[] mouthTiles; public Profile[] profiles; }
    public TextAsset profileData;
    public Animator animator;
    public SkinnedMeshRenderer faceRenderer;
    public CHDDDBodyControllerV2 bodyController;
    public string CurrentProfile { get; private set; }
    public float[] MouthWeights => output.mouthWeights;
    Profiles data; MaterialPropertyBlock block; int mouthSlot=-1;
    readonly int[] indices=new int[5];
    readonly Sample current=Empty(),next=Empty(),output=Empty(),start=Empty();
    float elapsed,blendSeconds; bool blending;
    static Sample Empty() { return new Sample { mouthWeights=new float[7] }; }
    void Awake() {
        if(animator==null) animator=GetComponent<Animator>();
        if(bodyController==null) bodyController=GetComponent<CHDDDBodyControllerV2>();
        if(profileData!=null) data=JsonUtility.FromJson<Profiles>(profileData.text);
        int mouthCount=data?.mouthTiles?.Length ?? 7;
        if(mouthCount<1 || mouthCount>8) throw new InvalidOperationException("Mouth atlas supports 1 to 8 cells");
        foreach(var sample in new[]{current,next,output,start}) sample.mouthWeights=new float[mouthCount];
        if(faceRenderer==null) foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(r.sharedMesh!=null && r.sharedMesh.GetBlendShapeIndex("Pet_Blink")>=0) { faceRenderer=r;break; }
        var names=new[]{"Pet_Blink","Pet_EyeWide","Pet_BrowSad","Pet_BrowAnnoyed","Pet_BrowSleepy"};
        for(int i=0;i<5;i++) indices[i]=faceRenderer!=null ? faceRenderer.sharedMesh.GetBlendShapeIndex(names[i]) : -1;
        block=new MaterialPropertyBlock();output.mouthWeights[0]=1;
        if(faceRenderer!=null) for(int i=0;i<faceRenderer.sharedMaterials.Length;i++)
            if(faceRenderer.sharedMaterials[i]!=null && faceRenderer.sharedMaterials[i].HasProperty("_MouthTile")) { mouthSlot=i;break; }
    }
    Profile Find(int hash) {
        if(data?.profiles==null) return null;
        foreach(var p in data.profiles) if(Animator.StringToHash("PetV2_"+p.name)==hash) return p;
        return null;
    }
    static void Mix(Sample a,Sample b,float t,Sample target) {
        target.blink=Mathf.Lerp(a.blink,b.blink,t);target.sad=Mathf.Lerp(a.sad,b.sad,t);
        target.annoyed=Mathf.Lerp(a.annoyed,b.annoyed,t);target.sleepy=Mathf.Lerp(a.sleepy,b.sleepy,t);
        for(int i=0;i<target.mouthWeights.Length;i++) target.mouthWeights[i]=Mathf.Lerp(a.mouthWeights[i],b.mouthWeights[i],t);
    }
    static void Evaluate(Profile p,float time,Sample target) {
        float phase=p.loop ? time-Mathf.Floor(time) : Mathf.Clamp01(time);
        float x=phase*(p.samples.Length-1);int i=Mathf.Min(Mathf.FloorToInt(x),p.samples.Length-2);
        Mix(p.samples[i],p.samples[i+1],x-i,target);
    }
    public void BeginTransition(float seconds) {
        if(block==null) Awake();
        if(faceRenderer!=null) {
            start.blink=indices[0]>=0 ? faceRenderer.GetBlendShapeWeight(indices[0])/100 : 0;
            start.sad=indices[2]>=0 ? faceRenderer.GetBlendShapeWeight(indices[2])/100 : 0;
            start.annoyed=indices[3]>=0 ? faceRenderer.GetBlendShapeWeight(indices[3])/100 : 0;
            start.sleepy=indices[4]>=0 ? faceRenderer.GetBlendShapeWeight(indices[4])/100 : 0;
            if(mouthSlot>=0) {
                faceRenderer.GetPropertyBlock(block,mouthSlot);
                Vector4 a=block.GetVector("_MouthWeights0"),b=block.GetVector("_MouthWeights1");
                Vector4 ta=block.GetVector("_MouthTiles0"),tb=block.GetVector("_MouthTiles1");
                float total=0;
                for(int i=0;i<output.mouthWeights.Length;i++) {
                    float value=0;
                    for(int j=0;j<8;j++) if(Mathf.Abs((j<4 ? ta[j] : tb[j-4])-data.mouthTiles[i])<.1f) value+=j<4 ? a[j] : b[j-4];
                    start.mouthWeights[i]=value;total+=value;
                }
                if(total<.001f) Array.Copy(output.mouthWeights,start.mouthWeights,output.mouthWeights.Length);
            } else Array.Copy(output.mouthWeights,start.mouthWeights,output.mouthWeights.Length);
        } else Mix(output,output,0,start);
        blendSeconds=Mathf.Max(0,seconds);elapsed=0;blending=blendSeconds>0;
    }
    void LateUpdate() { Advance(Time.deltaTime); }
    /// <summary>Use after Animator.Update with a custom clock; do not also tick the enabled component.</summary>
    public void Advance(float deltaSeconds) {
        if(animator==null || data==null || bodyController==null || !bodyController.useActionExpressions) return;
        var state=animator.GetCurrentAnimatorStateInfo(0);var p=Find(state.shortNameHash);
        if(p==null) return;
        CurrentProfile=p.name;Evaluate(p,state.normalizedTime,current);
        Mix(current,current,0,output);
        if(animator.IsInTransition(0)) {
            var ns=animator.GetNextAnimatorStateInfo(0);var np=Find(ns.shortNameHash);
            if(np!=null) {
                CurrentProfile=np.name;Evaluate(np,ns.normalizedTime,next);
                Mix(current,next,Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime),output);
            }
        }
        if(blending) {
            elapsed+=Mathf.Max(0,deltaSeconds);float w=Mathf.Clamp01(elapsed/blendSeconds);w=w*w*(3-2*w);
            // Target the new profile directly, so interruption preserves the currently visible face.
            Mix(start,animator.IsInTransition(0) ? next : current,w,output);
            if(elapsed>=blendSeconds) blending=false;
        }
        var values=new[]{output.blink,0,output.sad,output.annoyed,output.sleepy};
        if(faceRenderer!=null) for(int i=0;i<5;i++) if(indices[i]>=0) faceRenderer.SetBlendShapeWeight(indices[i],Mathf.Clamp01(values[i])*100);
        float sum=0;for(int i=0;i<output.mouthWeights.Length;i++) { output.mouthWeights[i]=Mathf.Clamp01(output.mouthWeights[i]);sum+=output.mouthWeights[i]; }
        if(sum<.001f) { output.mouthWeights[0]=1;sum=1; }
        for(int i=0;i<output.mouthWeights.Length;i++) output.mouthWeights[i]/=sum;
        if(faceRenderer!=null && mouthSlot>=0) {
            faceRenderer.GetPropertyBlock(block,mouthSlot);block.SetFloat("_MouthBlendEnabled",1);
            block.SetVector("_MouthTiles0",new Vector4(data.mouthTiles[0],data.mouthTiles[1],data.mouthTiles[2],data.mouthTiles[3]));
            block.SetVector("_MouthTiles1",new Vector4(data.mouthTiles[4],data.mouthTiles[5],data.mouthTiles[6],data.mouthTiles.Length>7 ? data.mouthTiles[7] : 0));
            block.SetVector("_MouthWeights0",new Vector4(output.mouthWeights[0],output.mouthWeights[1],output.mouthWeights[2],output.mouthWeights[3]));
            block.SetVector("_MouthWeights1",new Vector4(output.mouthWeights[4],output.mouthWeights[5],output.mouthWeights[6],output.mouthWeights.Length>7 ? output.mouthWeights[7] : 0));
            faceRenderer.SetPropertyBlock(block,mouthSlot);
        }
    }
}
