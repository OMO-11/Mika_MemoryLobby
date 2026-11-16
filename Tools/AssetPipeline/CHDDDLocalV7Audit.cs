using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class CHDDDLocalV7Audit {
    public static void Run() {
        GameObject instance=null;
        try {
            instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHDDD/Unity/CHDDD_Actions_v2.prefab"));
            var animator=instance.GetComponent<Animator>();var body=instance.GetComponent<CHDDDBodyControllerV2>();
            var face=instance.GetComponent<CHDDDActionFaceControllerV2>();var manual=instance.GetComponent<CHDDDExpressionController>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            animator.Rebind();animator.Update(0);
            typeof(CHDDDExpressionController).GetMethod("Awake",flags).Invoke(manual,null);
            typeof(CHDDDActionFaceControllerV2).GetMethod("Awake",flags).Invoke(face,null);
            typeof(CHDDDBodyControllerV2).GetMethod("Awake",flags).Invoke(body,null);
            face.enabled=false;body.enabled=false;manual.enabled=false;
            Action<string,float> pose=(name,t)=> {body.PlayAction(name,0);animator.Play("PetV2_"+name,0,t);animator.Play("PetV2_FaceProcess_"+name,1,t);animator.Update(0);face.Advance(0);};
            Func<float> blink=()=>face.faceRenderer.GetBlendShapeWeight(face.faceRenderer.sharedMesh.GetBlendShapeIndex("Pet_Blink"));
            pose("HeadClick",.20f);
            if(face.MouthWeights.Length!=8 || face.MouthWeights[7]<.999f || blink()<99.9f) throw new Exception("Closed-eye triangle startle missing in actual Unity playback");
            var block=new MaterialPropertyBlock();int slot=Array.FindIndex(face.faceRenderer.sharedMaterials,m=>m!=null&&m.HasProperty("_MouthTile"));face.faceRenderer.GetPropertyBlock(block,slot);
            if(Mathf.Abs(block.GetVector("_MouthTiles1").w-200)>.01f || block.GetVector("_MouthWeights1").w<.999f) throw new Exception("Eighth mouth cell not sent to shader");
            body.PlayAction("Wave",.25f);animator.Update(0);face.Advance(0);
            if(face.MouthWeights[7]<.999f) throw new Exception("Interrupting triangle face lost visible eighth-slot weight");
            for(int i=0;i<20;i++){animator.Update(1/60f);face.Advance(1/60f);if(Mathf.Abs(face.MouthWeights.Sum()-1)>.0001f)throw new Exception("Interrupted mouth weights not normalized");}
            pose("HeadClick",.65f);
            if(face.MouthWeights[1]>.0001f || face.MouthWeights[2]>.0001f || face.MouthWeights[3]<.99f)throw new Exception("Recognizing user should use frown, no smile");
            pose("Success",.42f);if(blink()<99.9f)throw new Exception("Success must close eyes after raising arms");
            var legacy=new TextAsset(File.ReadAllText("W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Checkpoints/V2_revision6_before_local/Unity/action_face_profiles_v2.json"));
            face.profileData=legacy;typeof(CHDDDActionFaceControllerV2).GetMethod("Awake",flags).Invoke(face,null);
            pose("Wave",.42f);if(face.MouthWeights.Length!=7 || Mathf.Abs(face.MouthWeights.Sum()-1)>.0001f)throw new Exception("Seven-cell legacy profiles no longer work");
            UnityEngine.Object.DestroyImmediate(legacy);
            File.WriteAllText("W:/Machine/VSCode/Mika_Gift/Material/CHDDD/body_v2_revision7_unity_local_validation.json","{\"passed\":true,\"revision\":7,\"triangleShaderSlotVerified\":true,\"triangleInterruptionContinuous\":true,\"headClickNoSmileVerified\":true,\"successClosedEyesVerified\":true}");
            UnityEngine.Object.DestroyImmediate(instance);instance=null;
            CHDDDReadabilityV5Frames.Run();
        }catch(Exception e){if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);Debug.LogException(e);EditorApplication.Exit(3);}
    }
}
