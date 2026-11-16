using System;
using UnityEngine;

/// <summary>Coordinated face transitions with continuous morph and mouth-atlas blending.</summary>
public sealed class CHDDDExpressionController : MonoBehaviour
{
    [Serializable] public class Expression { public string name; public string clip; public int mouthTile; public float blink; public float eyeWide; public float browSad; public float browAnnoyed; public float browSleepy; }
    [Serializable] public class ExpressionMap { public Expression[] expressions; }
    public TextAsset expressionMap;
    public SkinnedMeshRenderer faceRenderer;
    public Animator animator;
    public int emotionLayer = 1;
    [Range(0,3)] public float transitionDuration = .5f;
    public bool useUnscaledTime = true;
    public bool IsTransitioning { get; private set; }
    public float TransitionProgress { get; private set; } = 1;
    private ExpressionMap map;
    private MaterialPropertyBlock block;
    private int mouthSlot = -1;
    private static readonly string[] ShapeNames = { "Pet_Blink", "Pet_EyeWide", "Pet_BrowSad", "Pet_BrowAnnoyed", "Pet_BrowSleepy" };
    private readonly int[] shapeIndices = new int[5];
    private readonly float[] startShapes = new float[5], targetShapes = new float[5];
    private readonly float[] mouthTiles = new float[8], mouthWeights = new float[8], startMouth = new float[8], targetMouth = new float[8];
    private int tileCount;
    private float elapsed, duration;

    void Awake() {
        block = new MaterialPropertyBlock();
        if (expressionMap != null) map = JsonUtility.FromJson<ExpressionMap>(expressionMap.text);
        if (faceRenderer == null)
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && renderer.sharedMesh.GetBlendShapeIndex("Pet_Blink") >= 0) { faceRenderer=renderer; break; }
        if (animator == null) animator=GetComponentInChildren<Animator>();
        for (int i=0;i<shapeIndices.Length;i++) shapeIndices[i]=faceRenderer!=null && faceRenderer.sharedMesh!=null ? faceRenderer.sharedMesh.GetBlendShapeIndex(ShapeNames[i]) : -1;
        if (faceRenderer != null) {
            var materials=faceRenderer.sharedMaterials;
            for (int i=0;i<materials.Length;i++) if (materials[i]!=null && materials[i].HasProperty("_MouthTile")) { mouthSlot=i; break; }
        }
        if (map!=null && map.expressions!=null)
            foreach (var expression in map.expressions) FindMouthCell(expression.mouthTile);
        SetEmotionImmediate("neutral");
    }

    void Update() { AdvanceTransition(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }

    private Expression FindExpression(string emotion) {
        if (map==null || map.expressions==null) return null;
        return Array.Find(map.expressions,e=>e.name==emotion) ?? Array.Find(map.expressions,e=>e.name=="neutral");
    }

    public void SetEmotion(string emotion) { SetEmotion(emotion,transitionDuration); }

    public void SetEmotion(string emotion,float seconds) {
        var item=FindExpression(emotion);if (item==null) return;
        if (seconds<=0) { SetEmotionImmediate(emotion); return; }
        duration=Mathf.Clamp(seconds,.01f,3);elapsed=0;TransitionProgress=0;
        var values=new[]{item.blink,item.eyeWide,item.browSad,item.browAnnoyed,item.browSleepy};
        for (int i=0;i<5;i++) {
            startShapes[i]=faceRenderer!=null && shapeIndices[i]>=0 ? faceRenderer.GetBlendShapeWeight(shapeIndices[i])/100 : 0;
            targetShapes[i]=Mathf.Clamp01(values[i]);
        }
        Array.Copy(mouthWeights,startMouth,8);Array.Clear(targetMouth,0,8);
        targetMouth[FindMouthCell(item.mouthTile)]=1;
        IsTransitioning=true;
        WriteMouth(item.mouthTile);
        PlayPose(item.clip,duration);
    }

    public void SetEmotionImmediate(string emotion) {
        var item=FindExpression(emotion);if (item==null) return;
        IsTransitioning=false;TransitionProgress=1;
        var values=new[]{item.blink,item.eyeWide,item.browSad,item.browAnnoyed,item.browSleepy};
        for (int i=0;i<5;i++) SetShape(i,values[i]);
        SetMouthTile(item.mouthTile);PlayPose(item.clip,0);
    }

    /// <summary>Advance the same runtime transition used by Update; useful with a custom pet clock.</summary>
    public void AdvanceTransition(float deltaSeconds) {
        if (!IsTransitioning) return;
        elapsed+=Mathf.Max(0,deltaSeconds);TransitionProgress=Mathf.Clamp01(elapsed/duration);
        float weight=TransitionProgress*TransitionProgress*(3-2*TransitionProgress);
        for (int i=0;i<5;i++) SetShape(i,Mathf.Lerp(startShapes[i],targetShapes[i],weight));
        for (int i=0;i<8;i++) mouthWeights[i]=Mathf.Lerp(startMouth[i],targetMouth[i],weight);
        WriteMouth();
        if (TransitionProgress>=1) IsTransitioning=false;
    }

    private void SetShape(int shape,float value) {
        if (faceRenderer!=null && shapeIndices[shape]>=0) faceRenderer.SetBlendShapeWeight(shapeIndices[shape],Mathf.Clamp01(value)*100);
    }

    private void PlayPose(string clip,float seconds) {
        if (animator==null || emotionLayer<0 || emotionLayer>=animator.layerCount) return;
        int state=Animator.StringToHash(clip);if (!animator.HasState(emotionLayer,state)) return;
        if (seconds>0) animator.CrossFadeInFixedTime(state,seconds,emotionLayer,0);
        else animator.Play(state,emotionLayer,0);
    }

    private int FindMouthCell(int tile) {
        for (int i=0;i<tileCount;i++) if (Mathf.Abs(mouthTiles[i]-tile)<.1f) return i;
        if (tileCount>=8) throw new InvalidOperationException("Expression map supports up to eight distinct mouth tiles.");
        mouthTiles[tileCount]=tile;return tileCount++;
    }

    public void SetMouthTile(int tile) {
        // External lip-sync events may take over the mouth during an eye/brow transition.
        int index=-1;
        for (int i=0;i<tileCount;i++) if (Mathf.Abs(mouthTiles[i]-tile)<.1f) index=i;
        if (index<0) {
            index=7;mouthTiles[index]=tile;tileCount=Math.Max(tileCount,8);
        }
        Array.Clear(mouthWeights,0,8);mouthWeights[index]=1;
        Array.Copy(mouthWeights,startMouth,8);Array.Copy(mouthWeights,targetMouth,8);
        WriteMouth(tile);
    }

    private void WriteMouth(int tile=-1) {
        if (faceRenderer==null || mouthSlot<0 || block==null) return;
        faceRenderer.GetPropertyBlock(block,mouthSlot);
        if (tile>=0) block.SetFloat("_MouthTile",tile);
        block.SetFloat("_MouthBlendEnabled",1);
        block.SetVector("_MouthTiles0",new Vector4(mouthTiles[0],mouthTiles[1],mouthTiles[2],mouthTiles[3]));
        block.SetVector("_MouthTiles1",new Vector4(mouthTiles[4],mouthTiles[5],mouthTiles[6],mouthTiles[7]));
        block.SetVector("_MouthWeights0",new Vector4(mouthWeights[0],mouthWeights[1],mouthWeights[2],mouthWeights[3]));
        block.SetVector("_MouthWeights1",new Vector4(mouthWeights[4],mouthWeights[5],mouthWeights[6],mouthWeights[7]));
        faceRenderer.SetPropertyBlock(block,mouthSlot);
    }
}
