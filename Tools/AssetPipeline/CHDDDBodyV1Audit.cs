using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class CHDDDBodyV1Audit
{
    [Serializable] public class Result {
        public bool passed,genericAvatarValid,weaponsAbsent,haloAttached,faceMaskExcludesHead,faceShapesPresent;
        public bool loopSettingsValid,runtimePlaybackVerified,faceDoesNotOverrideHead,rootMotionDisabled;
        public bool automaticTransitionsVerified,multipleLoopPlaybackVerified,faceBlendPersistsDuringBodyPlayback,shaderErrors;
        public string unityVersion; public int bodyClips,faceClips; public string[] clips;
    }
    static bool FacialPath(string path) {
        string name=path.Split('/').Last();return name=="bone_Mouth" || name.ToLowerInvariant().Contains("eye");
    }
    public static void Run() {
        try {
            const string root="Assets/CHDDD/Unity";
            const string model=root+"/CHDDD_Actions_v1.fbx";
            string output="W:/Machine/VSCode/Mika_Gift/Material/CHDDD";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var map=JsonUtility.FromJson<CHDDDBodyControllerV1.ActionMap>(File.ReadAllText(root+"/body_action_map_v1.json"));
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName="bone_root";
            importer.importBlendShapes=true;importer.importAnimation=true;importer.isReadable=true;
            var settings=importer.defaultClipAnimations;
            foreach(var item in settings) {
                item.name=item.name.Split('|').Last();
                var entry=map.actions.FirstOrDefault(a=>a.clip==item.name);
                item.loopTime=entry!=null ? entry.loop : item.name.StartsWith("PetV1_Face_");
                item.loopPose=false;
            }
            importer.clipAnimations=settings;importer.SaveAndReimport();
            var assets=AssetDatabase.LoadAllAssetsAtPath(model);
            var clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var avatar=assets.OfType<Avatar>().First();
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model));instance.name="CHDDD_Actions_v1";
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) {
                    string name=materials[i].name.Replace("CHDDD_","").Split('.')[0];
                    var restored=AssetDatabase.LoadAssetAtPath<Material>(root+"/Materials/CHDDD_"+name+".mat");
                    if(restored==null) throw new Exception("Missing accepted material "+name);
                    materials[i]=restored;
                }
                renderer.sharedMaterials=materials;
            }
            const string animFolder=root+"/AnimationsV1";
            if(!AssetDatabase.IsValidFolder(animFolder)) AssetDatabase.CreateFolder(root,"AnimationsV1");
            var motions=new System.Collections.Generic.Dictionary<string,AnimationClip>();
            foreach(var source in clips) {
                var clip=UnityEngine.Object.Instantiate(source);clip.name=source.name;
                bool face=clip.name.StartsWith("PetV1_Face_");
                // Exported FBX clips are fully baked. Keep only channels belonging to this layer.
                foreach(var binding in AnimationUtility.GetCurveBindings(clip)) {
                    bool facial=FacialPath(binding.path);
                    bool blend=binding.propertyName.StartsWith("blendShape.");
                    if(blend || (face ? !facial : facial)) AnimationUtility.SetEditorCurve(clip,binding,null);
                }
                string path=animFolder+"/"+clip.name+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(existing!=null) { EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing; }
                else AssetDatabase.CreateAsset(clip,path);
                motions.Add(clip.name,clip);
            }
            string controllerPath=root+"/CHDDD_Actions_v1.controller";
            if(AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)!=null) AssetDatabase.DeleteAsset(controllerPath);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var layers=controller.layers;layers[0].name="Body";controller.layers=layers;
            var states=new System.Collections.Generic.Dictionary<string,AnimatorState>();
            foreach(var entry in map.actions) {
                var state=controller.layers[0].stateMachine.AddState(entry.clip);state.motion=motions[entry.clip];
                states.Add(entry.name,state);
            }
            controller.layers[0].stateMachine.defaultState=states[map.defaultAction];
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.nextAction))) {
                var transition=states[entry.name].AddTransition(states[entry.nextAction]);
                transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;
                transition.duration=.10f;transition.interruptionSource=TransitionInterruptionSource.SourceThenDestination;
            }
            string maskPath=root+"/CHDDD_Face_v1.mask";
            if(AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath)!=null) AssetDatabase.DeleteAsset(maskPath);
            var mask=new AvatarMask();mask.AddTransformPath(instance.transform,true);
            for(int i=0;i<mask.transformCount;i++) mask.SetTransformActive(i,FacialPath(mask.GetTransformPath(i)));
            AssetDatabase.CreateAsset(mask,maskPath);
            controller.AddLayer("Emotion");layers=controller.layers;
            layers[1].avatarMask=mask;layers[1].defaultWeight=1;
            var emotions=JsonUtility.FromJson<CHDDDExpressionController.ExpressionMap>(File.ReadAllText(root+"/expression_map_v1.json"));
            foreach(var item in emotions.expressions) {
                var state=layers[1].stateMachine.AddState(item.clip);state.motion=motions[item.clip];
                if(item.name=="neutral") layers[1].stateMachine.defaultState=state;
            }
            controller.layers=layers;
            var animator=instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var faceRenderer=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.sharedMesh.GetBlendShapeIndex("Pet_Blink")>=0);
            var expression=instance.AddComponent<CHDDDExpressionController>();expression.animator=animator;expression.faceRenderer=faceRenderer;
            expression.expressionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/expression_map_v1.json");
            var playback=instance.AddComponent<CHDDDBodyControllerV1>();playback.animator=animator;playback.expressions=expression;
            playback.actionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/body_action_map_v1.json");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            animator.Rebind();animator.Update(0);
            typeof(CHDDDExpressionController).GetMethod("Awake",flags).Invoke(expression,null);
            typeof(CHDDDBodyControllerV1).GetMethod("Awake",flags).Invoke(playback,null);
            bool playbackOkay=true;
            foreach(var entry in map.actions) {
                playbackOkay &= playback.PlayAction(entry.name,0);animator.Update(.03f);
                string expected=string.IsNullOrEmpty(entry.enterClip) ? entry.clip : entry.enterClip;
                playbackOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(expected);
            }
            bool automaticOkay=true,loopsOkay=true;
            foreach(var entry in map.actions.Where(a=>a.loop)) {
                animator.Play(entry.clip,0,0);animator.Update(0);
                for(int tick=0;tick<Mathf.CeilToInt(entry.durationSeconds*2.25f/.02f);tick++) animator.Update(.02f);
                loopsOkay &= !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(entry.clip);
            }
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.nextAction))) {
                playback.PlayAction(entry.name,0);animator.Update(0);
                for(int tick=0;tick<Mathf.CeilToInt((entry.durationSeconds+.20f)/.02f);tick++) animator.Update(.02f);
                automaticOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(map.actions.First(a=>a.name==entry.nextAction).clip);
            }
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.enterClip))) {
                playback.PlayAction(entry.name,0);animator.Update(.02f);playback.ReturnToIdle();
                for(int tick=0;tick<100;tick++) animator.Update(.02f);
                automaticOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(map.actions.First(a=>a.name==map.defaultAction).clip);
            }
            expression.SetEmotionImmediate("neutral");expression.SetEmotion("sleepy",.5f);expression.AdvanceTransition(.25f);
            int blink=faceRenderer.sharedMesh.GetBlendShapeIndex("Pet_Blink");float before=faceRenderer.GetBlendShapeWeight(blink);
            playback.followSuggestedEmotion=false;playback.PlayAction("Thinking",0);animator.Update(.2f);
            bool faceBlendOkay=Mathf.Abs(before-faceRenderer.GetBlendShapeWeight(blink))<.01f && before>0;
            expression.AdvanceTransition(.25f);faceBlendOkay &= Mathf.Abs(faceRenderer.GetBlendShapeWeight(blink)-100)<.01f;
            // Verify the face layer leaves the body's actual head pose intact at the same playback time.
            var head=instance.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bip001 Head");
            playback.followSuggestedEmotion=false;animator.SetLayerWeight(1,0);
            animator.Play("PetV1_Sleep",0,.25f);animator.Update(0);var bodyHead=head.localRotation;
            animator.SetLayerWeight(1,1);expression.SetEmotionImmediate("sleepy");
            animator.Play("PetV1_Sleep",0,.25f);animator.Update(0);var layeredHead=head.localRotation;
            bool headIntact=Quaternion.Angle(bodyHead,layeredHead)<.01f;
            playback.followSuggestedEmotion=true;playback.PlayAction("Idle",0);expression.SetEmotionImmediate("neutral");animator.Update(0);
            var halo=instance.GetComponentsInChildren<MeshFilter>(true).First(m=>m.name=="CHDDD_Halo_Restored");
            bool maskOkay=true;
            for(int i=0;i<mask.transformCount;i++) if(mask.GetTransformPath(i).EndsWith("Bip001 Head")) maskOkay &= !mask.GetTransformActive(i);
            var result=new Result {
                unityVersion=Application.unityVersion,genericAvatarValid=avatar.isValid&&!avatar.isHuman,
                bodyClips=map.actions.Count(a=>motions.ContainsKey(a.clip)),faceClips=emotions.expressions.Length,
                clips=motions.Keys.ToArray(),weaponsAbsent=!instance.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("CH0069_Weapon")),
                haloAttached=halo.GetComponentsInParent<Transform>().Any(t=>t.name=="Bip001 Head"),faceMaskExcludesHead=maskOkay,
                faceShapesPresent=faceRenderer.sharedMesh.blendShapeCount==5,
                loopSettingsValid=map.actions.All(a=>motions[a.clip].isLooping==a.loop),
                runtimePlaybackVerified=playbackOkay,faceDoesNotOverrideHead=headIntact,rootMotionDisabled=!animator.applyRootMotion,
                automaticTransitionsVerified=automaticOkay,multipleLoopPlaybackVerified=loopsOkay,faceBlendPersistsDuringBodyPlayback=faceBlendOkay,
                shaderErrors=ShaderUtil.ShaderHasError(Shader.Find("CHDDD/Toon"))||ShaderUtil.ShaderHasError(Shader.Find("CHDDD/MouthAtlas"))
            };
            result.passed=result.genericAvatarValid&&result.bodyClips==18&&result.faceClips==9&&result.weaponsAbsent&&result.haloAttached&&result.faceMaskExcludesHead&&result.faceShapesPresent&&result.loopSettingsValid&&result.runtimePlaybackVerified&&result.faceDoesNotOverrideHead&&result.rootMotionDisabled&&automaticOkay&&loopsOkay&&faceBlendOkay&&!result.shaderErrors;
            PrefabUtility.SaveAsPrefabAsset(instance,root+"/CHDDD_Actions_v1.prefab");AssetDatabase.SaveAssets();
            File.WriteAllText(output+"/body_v1_unity_validation.json",JsonUtility.ToJson(result,true));
            if(result.passed) AssetDatabase.ExportPackage(root+"/CHDDD_Actions_v1.prefab",output+"/CHDDD_Actions_v1.unitypackage",ExportPackageOptions.IncludeDependencies);
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("CHDDD_BODY_V1 "+JsonUtility.ToJson(result));EditorApplication.Exit(result.passed ? 0 : 2);
        } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(3); }
    }
}
