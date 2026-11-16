using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Rendering;
using UnityEngine;

public static class CHDDDRestorationAudit
{
    [Serializable] public class Result {
        public string unityVersion; public bool passed; public bool genericAvatarValid;
        public int animationClips; public int originalClips; public int emotionClips;
        public int haloVertices; public bool haloAttachedToHead; public int blendShapes; public string[] shapeNames;
        public bool shaderErrors; public bool runtimeMouthVerified; public float sleepyBlinkWeight;
        public bool runtimeBrowShapesVerified; public bool irisSizePreserved;
        public int transitionClips; public bool runtimeTransitionsVerified; public bool interruptedTransitionContinuous; public bool mouthCrossfadeVerified;
        public string[] clips;
    }

    public static void Run()
    {
        try {
            const string root="Assets/CHDDD";
            const string modelPath=root+"/Unity/CHDDD.fbx";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName="bone_root";
            importer.importBlendShapes=true;importer.importAnimation=true;importer.isReadable=true;
            var clipSettings=importer.defaultClipAnimations;
            foreach(var clip in clipSettings) {
                clip.name=clip.name.Split('|').Last();
                if(clip.name.Contains("--")) clip.name=clip.name.Substring(0,clip.name.IndexOf("--",StringComparison.Ordinal));
                clip.loopTime=clip.name.StartsWith("Pet_Emotion_") || clip.name.Contains("Idle") || clip.name=="CH0069_Cafe_Walk" || clip.name=="CH0069_Move_Ing";
            }
            importer.clipAnimations=clipSettings;
            importer.SaveAndReimport();
            var assets=AssetDatabase.LoadAllAssetsAtPath(modelPath);
            var avatar=assets.OfType<Avatar>().FirstOrDefault();
            var clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance=UnityEngine.Object.Instantiate(prefab);
            instance.name="CHDDD";
            var toon=Shader.Find("CHDDD/Toon");var mouthShader=Shader.Find("CHDDD/MouthAtlas");
            if(toon==null || mouthShader==null) throw new Exception("Restored shaders not imported");
            string materialsRoot=root+"/Unity/Materials";
            if(!AssetDatabase.IsValidFolder(materialsRoot)) AssetDatabase.CreateFolder(root+"/Unity","Materials");
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) {
                    string name=materials[i].name;
                    string part=name.Replace("CHDDD_","").Split('.')[0];
                    string assetPath=materialsRoot+"/CHDDD_"+part+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if(material==null) {
                        material=new Material(part=="MouthAtlas" ? mouthShader : toon);
                        string textureName=part=="MouthAtlas" ? "Character_Mouth" : part=="Eyebrow" ? "CH0069_Face" : "CH0069_"+part;
                        material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/"+textureName+".png"));
                        if(part=="MouthAtlas") material.SetFloat("_MouthTile",300);
                        else {
                            var maskTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/CH0069_"+part+"_Mask.png");
                            if(maskTexture!=null) material.SetTexture("_MaskTex",maskTexture);
                            if(part=="Hair") { material.SetTexture("_SpecTex",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/Textures/CH0069_Hair_Spec.png")); material.SetFloat("_SpecStrength",.5f); }
                            if(part=="Halo" || part=="EyeMouth" || part=="Eyebrow") { material.SetFloat("_Unlit",1);material.SetFloat("_OutlineWidth",0); }
                            if(part=="Halo") material.SetFloat("_Emission",1.2f);
                            if(part=="Face") material.SetColor("_ShadowColor",new Color(1,.788f,.788f));
                            if(part=="Hair") material.SetColor("_ShadowColor",new Color(1,.693f,.86f));
                        }
                        AssetDatabase.CreateAsset(material,assetPath);
                    }
                    materials[i]=material;
                    if(part=="Eyebrow") { material.SetFloat("_Unlit",0);material.SetFloat("_OutlineWidth",0);material.SetColor("_ShadowColor",new Color(.80f,.75f,.72f));EditorUtility.SetDirty(material); }
                }
                renderer.sharedMaterials=materials;
            }
            string controllerPath=root+"/Unity/CHDDD.controller";
            var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach(string name in new[]{"CH0069_Cafe_Idle","CH0069_Cafe_Walk"}) {
                var clip=clips.FirstOrDefault(c=>c.name==name);
                if(clip!=null) controller.layers[0].stateMachine.AddState(name).motion=clip;
            }
            var mask=new AvatarMask(); mask.AddTransformPath(instance.transform,true);
            var map=JsonUtility.FromJson<CHDDDExpressionController.ExpressionMap>(File.ReadAllText(root+"/Unity/expression_map.json"));
            string[] faceBones={"Bip001 Head","bone_Mouth"};
            for(int i=0;i<mask.transformCount;i++) {
                string name=mask.GetTransformPath(i).Split('/').Last();
                mask.SetTransformActive(i,faceBones.Contains(name)||name.ToLowerInvariant().Contains("eye"));
            }
            AssetDatabase.CreateAsset(mask,root+"/Unity/CHDDD_Face.mask");
            controller.AddLayer("Emotion");
            var layers=controller.layers;layers[1].avatarMask=mask;layers[1].defaultWeight=1;
            foreach(var entry in map.expressions) {
                var clip=clips.First(c=>c.name==entry.clip);
                var state=layers[1].stateMachine.AddState(entry.clip);state.motion=clip;
                if(entry.name=="neutral") layers[1].stateMachine.defaultState=state;
            }
            controller.layers=layers;
            var animator=instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var face=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.sharedMesh.blendShapeCount>=2);
            var expression=instance.AddComponent<CHDDDExpressionController>();
            expression.expressionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/Unity/expression_map.json");
            expression.faceRenderer=face;expression.animator=animator;
            // Initialize as a live instance, then exercise the actual runtime helper.
            var awake=typeof(CHDDDExpressionController).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            awake.Invoke(expression,null);expression.SetEmotionImmediate("sleepy");
            var block=new MaterialPropertyBlock();
            int mouthIndex=Array.FindIndex(face.sharedMaterials,m=>m.HasProperty("_MouthTile"));
            face.GetPropertyBlock(block,mouthIndex);
            float tile=block.GetFloat("_MouthTile");
            int blinkIndex=face.sharedMesh.GetBlendShapeIndex("Pet_Blink");
            float blinkWeight=blinkIndex>=0 ? face.GetBlendShapeWeight(blinkIndex) : -1;
            bool brows=true;
            foreach(var entry in map.expressions) {
                expression.SetEmotionImmediate(entry.name);
                string[] names={"Pet_BrowSad","Pet_BrowAnnoyed","Pet_BrowSleepy"};
                float[] values={entry.browSad,entry.browAnnoyed,entry.browSleepy};
                for(int i=0;i<names.Length;i++) {
                    int index=face.sharedMesh.GetBlendShapeIndex(names[i]);
                    brows &= index>=0 && Mathf.Abs(face.GetBlendShapeWeight(index)-values[i]*100)<.01f;
                }
            }
            var vertices=face.sharedMesh.vertices;
            var dx=new Vector3[vertices.Length];var dn=new Vector3[vertices.Length];var dt=new Vector3[vertices.Length];
            face.sharedMesh.GetBlendShapeFrameVertices(blinkIndex,0,dx,dn,dt);
            var weights=face.sharedMesh.boneWeights;bool irisPreserved=true;int irisVertices=0;
            for(int i=0;i<weights.Length;i++) {
                var w=weights[i];int[] indices={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                float[] amounts={w.weight0,w.weight1,w.weight2,w.weight3};
                bool iris=false;
                for(int j=0;j<4;j++) if(amounts[j]>.01f) {
                    string bone=face.bones[indices[j]].name;
                    iris |= bone=="Bip001 Xtra_eyeL" || bone=="Bip001 Xtra_eyeR";
                }
                if(iris) { irisVertices++;irisPreserved &= dx[i].sqrMagnitude<1e-12f; }
            }
            irisPreserved &= irisVertices>=68;
            bool transitions=true;
            foreach(var entry in map.expressions.Where(e=>e.name!="neutral")) {
                expression.SetEmotionImmediate("neutral");expression.SetEmotion(entry.name,.5f);
                transitions &= expression.IsTransitioning && Mathf.Abs(face.GetBlendShapeWeight(blinkIndex))<.01f;
                expression.AdvanceTransition(.25f);
                transitions &= Mathf.Abs(face.GetBlendShapeWeight(blinkIndex)-entry.blink*50)<.01f;
                expression.AdvanceTransition(.25f);
                transitions &= !expression.IsTransitioning && Mathf.Abs(face.GetBlendShapeWeight(blinkIndex)-entry.blink*100)<.01f;
            }
            expression.SetEmotionImmediate("neutral");expression.SetEmotion("sleepy",.5f);expression.AdvanceTransition(.25f);
            face.GetPropertyBlock(block,mouthIndex);
            var mouthBeforeA=block.GetVector("_MouthWeights0");var mouthBeforeB=block.GetVector("_MouthWeights1");
            bool mouthBlend=Mathf.Abs(mouthBeforeA.x-.5f)<.01f&&Mathf.Abs(mouthBeforeB.y-.5f)<.01f;
            float blinkBefore=face.GetBlendShapeWeight(blinkIndex);
            expression.SetEmotion("sad",.5f);face.GetPropertyBlock(block,mouthIndex);
            bool continuous=Mathf.Abs(face.GetBlendShapeWeight(blinkIndex)-blinkBefore)<.001f
                &&Vector4.Distance(mouthBeforeA,block.GetVector("_MouthWeights0"))<.001f
                &&Vector4.Distance(mouthBeforeB,block.GetVector("_MouthWeights1"))<.001f;
            expression.AdvanceTransition(.25f);face.GetPropertyBlock(block,mouthIndex);
            var blendA=block.GetVector("_MouthWeights0");var blendB=block.GetVector("_MouthWeights1");
            mouthBlend &= Mathf.Abs(blendA.x-.25f)<.01f&&Mathf.Abs(blendA.w-.5f)<.01f&&Mathf.Abs(blendB.y-.25f)<.01f;
            continuous &= Mathf.Abs(face.GetBlendShapeWeight(blinkIndex)-34)<.01f;
            expression.AdvanceTransition(.25f);expression.SetEmotionImmediate("neutral");
            PrefabUtility.SaveAsPrefabAsset(instance,root+"/Unity/CHDDD.prefab");
            var halo=instance.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m=>m.name=="CHDDD_Halo_Restored");
            var result=new Result {
                unityVersion=Application.unityVersion,genericAvatarValid=avatar!=null&&avatar.isValid&&!avatar.isHuman,
                animationClips=clips.Length,originalClips=clips.Count(c=>c.name.StartsWith("CH0069_")),
                emotionClips=clips.Count(c=>c.name.StartsWith("Pet_Emotion_")),
                haloVertices=halo!=null ? halo.sharedMesh.vertexCount:0,blendShapes=face.sharedMesh.blendShapeCount,
                haloAttachedToHead=halo!=null&&halo.GetComponentsInParent<Transform>().Any(t=>t.name=="Bip001 Head"),
                shapeNames=Enumerable.Range(0,face.sharedMesh.blendShapeCount).Select(face.sharedMesh.GetBlendShapeName).ToArray(),
                shaderErrors=ShaderUtil.ShaderHasError(toon)||ShaderUtil.ShaderHasError(mouthShader),
                runtimeMouthVerified=Mathf.Abs(tile-305)<.01f,sleepyBlinkWeight=blinkWeight,
                runtimeBrowShapesVerified=brows,irisSizePreserved=irisPreserved,clips=clips.Select(c=>c.name).ToArray()
                ,transitionClips=clips.Count(c=>c.name.StartsWith("Pet_Transition_NeutralTo")),
                runtimeTransitionsVerified=transitions,interruptedTransitionContinuous=continuous,mouthCrossfadeVerified=mouthBlend
            };
            result.passed=result.genericAvatarValid&&result.originalClips==33&&result.emotionClips==9&&result.haloVertices>=94&&result.haloAttachedToHead
                &&result.blendShapes==5&&!result.shaderErrors&&result.runtimeMouthVerified&&Mathf.Abs(blinkWeight-100)<.01f
                &&brows&&irisPreserved&&result.transitionClips==8&&transitions&&continuous&&mouthBlend;
            string output=Environment.GetEnvironmentVariable("CHDDD_AUDIT_OUTPUT");
            File.WriteAllText(Path.Combine(output,"restoration_validation.json"),JsonUtility.ToJson(result,true));
            AssetDatabase.SaveAssets();
            if(result.passed) AssetDatabase.ExportPackage(root,Path.Combine(output,"CHDDD.unitypackage"),ExportPackageOptions.Recurse);
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("CHDDD_RESTORATION "+JsonUtility.ToJson(result));
            EditorApplication.Exit(result.passed?0:2);
        } catch(Exception error) { Debug.LogException(error);EditorApplication.Exit(3); }
    }
}
