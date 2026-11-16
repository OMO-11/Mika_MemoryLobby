using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class CHDDDBodyV2Audit
{
    [Serializable] public class Result {
        public bool passed,genericAvatarValid,weaponsAbsent,haloAttached,faceMaskExcludesHead,faceShapesPresent;
        public bool loopSettingsValid,runtimePlaybackVerified,faceDoesNotOverrideHead,rootMotionDisabled;
        public bool automaticTransitionsVerified,multipleLoopPlaybackVerified,faceBlendPersistsDuringBodyPlayback,shaderErrors;
        public bool actionFacesVary,interruptedFaceContinuous,mouthNormalized,faceLayersSynchronized,original29SourceConfirmed;
        public bool configuredTransitionsVerified;
        public string unityVersion; public int revision,bodyClips,faceClips,actionFaceClips; public string[] clips;
    }
    static bool FacialPath(string path) {
        string name=path.Split('/').Last();return name=="bone_Mouth" || name.ToLowerInvariant().Contains("eye");
    }
    public static void Run() {
        try {
            const string root="Assets/CHDDD/Unity";
            const string model=root+"/CHDDD_Actions_v2.fbx";
            string output="W:/Machine/VSCode/Mika_Gift/Material/CHDDD";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var map=JsonUtility.FromJson<CHDDDBodyControllerV2.ActionMap>(File.ReadAllText(root+"/body_action_map_v2.json"));
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.motionNodeName="bone_root";
            importer.importBlendShapes=true;importer.importAnimation=true;importer.isReadable=true;
            var settings=importer.defaultClipAnimations;
            foreach(var item in settings) {
                item.name=item.name.Split('|').Last();
                var entry=map.actions.FirstOrDefault(a=>a.clip==item.name || a.faceClip==item.name);
                item.loopTime=entry!=null ? entry.loop : item.name.StartsWith("PetV2_Face");
                item.loopPose=false;
            }
            importer.clipAnimations=settings;importer.SaveAndReimport();
            var assets=AssetDatabase.LoadAllAssetsAtPath(model);
            var clips=assets.OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var avatar=assets.OfType<Avatar>().First();
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model));instance.name="CHDDD_Actions_v2";
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
            const string animFolder=root+"/AnimationsV2";
            if(!AssetDatabase.IsValidFolder(animFolder)) AssetDatabase.CreateFolder(root,"AnimationsV2");
            var motions=new System.Collections.Generic.Dictionary<string,AnimationClip>();
            foreach(var source in clips) {
                var clip=UnityEngine.Object.Instantiate(source);clip.name=source.name;
                bool face=clip.name.StartsWith("PetV2_Face");
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
            string controllerPath=root+"/CHDDD_Actions_v2.controller";
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
            string maskPath=root+"/CHDDD_Face_v2.mask";
            if(AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath)!=null) AssetDatabase.DeleteAsset(maskPath);
            var mask=new AvatarMask();mask.AddTransformPath(instance.transform,true);
            for(int i=0;i<mask.transformCount;i++) mask.SetTransformActive(i,FacialPath(mask.GetTransformPath(i)));
            AssetDatabase.CreateAsset(mask,maskPath);
            controller.AddLayer("Emotion");layers=controller.layers;
            layers[1].avatarMask=mask;layers[1].defaultWeight=1;
            var emotions=JsonUtility.FromJson<CHDDDExpressionController.ExpressionMap>(File.ReadAllText(root+"/expression_map_v2.json"));
            foreach(var item in emotions.expressions) {
                var state=layers[1].stateMachine.AddState(item.clip);state.motion=motions[item.clip];
                if(item.name=="neutral") layers[1].stateMachine.defaultState=state;
            }
            var faceStates=new System.Collections.Generic.Dictionary<string,AnimatorState>();
            foreach(var entry in map.actions) {
                var fs=layers[1].stateMachine.AddState(entry.faceClip);fs.motion=motions[entry.faceClip];faceStates[entry.name]=fs;
            }
            layers[1].stateMachine.defaultState=faceStates[map.defaultAction];
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.nextAction))) {
                var tr=faceStates[entry.name].AddTransition(faceStates[entry.nextAction]);tr.hasExitTime=true;tr.exitTime=1;
                tr.hasFixedDuration=true;tr.duration=.10f;tr.interruptionSource=TransitionInterruptionSource.SourceThenDestination;
            }
            controller.layers=layers;
            var animator=instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.avatar=avatar;animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var faceRenderer=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.sharedMesh.GetBlendShapeIndex("Pet_Blink")>=0);
            var expression=instance.AddComponent<CHDDDExpressionController>();expression.animator=animator;expression.faceRenderer=faceRenderer;
            expression.expressionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/expression_map_v2.json");
            var playback=instance.AddComponent<CHDDDBodyControllerV2>();playback.animator=animator;playback.expressions=expression;
            playback.actionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/body_action_map_v2.json");
            var actionFaces=instance.AddComponent<CHDDDActionFaceControllerV2>();
            actionFaces.animator=animator;actionFaces.faceRenderer=faceRenderer;actionFaces.bodyController=playback;
            actionFaces.profileData=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/action_face_profiles_v2.json");
            playback.actionFaces=actionFaces;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            animator.Rebind();animator.Update(0);
            typeof(CHDDDExpressionController).GetMethod("Awake",flags).Invoke(expression,null);
            typeof(CHDDDActionFaceControllerV2).GetMethod("Awake",flags).Invoke(actionFaces,null);
            typeof(CHDDDBodyControllerV2).GetMethod("Awake",flags).Invoke(playback,null);
            bool playbackOkay=true,faceSync=true,automaticOkay=true,loopsOkay=true;
            foreach(var entry in map.actions) {
                playbackOkay &= playback.PlayAction(entry.name,0);animator.Update(.03f);actionFaces.Advance(.03f);
                string expected=string.IsNullOrEmpty(entry.enterClip) ? entry.clip : entry.enterClip;
                playbackOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(expected);
                faceSync &= animator.GetCurrentAnimatorStateInfo(1).shortNameHash==Animator.StringToHash(map.actions.First(a=>a.clip==expected).faceClip);
            }
            playbackOkay &= playback.PlayAction("UnavailableAction",0);animator.Update(0);
            playbackOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("PetV2_Idle");
            bool configuredTransitionsOkay=true;
            foreach(string click in new[]{"HeadClick","BodyClick"}) {
                playback.PlayAction("Idle",0);animator.Update(0);actionFaces.Advance(0);
                var entry=map.actions.First(a=>a.name==click);
                playback.PlayAction(click);animator.Update(.001f);actionFaces.Advance(.001f);
                configuredTransitionsOkay &= entry.transitionSeconds>0 && entry.transitionSeconds<=.12f;
                configuredTransitionsOkay &= animator.IsInTransition(0) && animator.IsInTransition(1);
                configuredTransitionsOkay &= Mathf.Abs(animator.GetAnimatorTransitionInfo(0).duration-entry.transitionSeconds)<.001f;
                configuredTransitionsOkay &= Mathf.Abs(animator.GetAnimatorTransitionInfo(1).duration-entry.transitionSeconds)<.001f;
            }
            playback.PlayAction("Idle",0);animator.Update(0);playback.PlayAction("HeadClick",.24f);animator.Update(.001f);
            configuredTransitionsOkay &= Mathf.Abs(animator.GetAnimatorTransitionInfo(0).duration-.24f)<.001f;
            foreach(var entry in map.actions.Where(a=>a.loop)) {
                animator.Play(entry.clip,0,0);animator.Play(entry.faceClip,1,0);animator.Update(0);
                for(int tick=0;tick<Mathf.CeilToInt(entry.durationSeconds*2.25f/.02f);tick++) { animator.Update(.02f);actionFaces.Advance(.02f); }
                loopsOkay &= !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(entry.clip);
                faceSync &= animator.GetCurrentAnimatorStateInfo(1).shortNameHash==Animator.StringToHash(entry.faceClip);
            }
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.nextAction))) {
                playback.PlayAction(entry.name,0);animator.Update(0);
                for(int tick=0;tick<Mathf.CeilToInt((entry.durationSeconds+.25f)/.02f);tick++) { animator.Update(.02f);actionFaces.Advance(.02f); }
                var target=map.actions.First(a=>a.name==entry.nextAction);
                automaticOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(target.clip);
                faceSync &= animator.GetCurrentAnimatorStateInfo(1).shortNameHash==Animator.StringToHash(target.faceClip);
            }
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.enterClip))) {
                playback.PlayAction(entry.name,0);animator.Update(.02f);playback.ReturnToIdle();
                for(int tick=0;tick<100;tick++) { animator.Update(.02f);actionFaces.Advance(.02f); }
                automaticOkay &= animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash(map.actions.First(a=>a.name==map.defaultAction).clip);
            }
            int blink=faceRenderer.sharedMesh.GetBlendShapeIndex("Pet_Blink");
            bool varied=true,normalized=true;
            foreach(var entry in map.actions) {
                float min=100,max=0,mouthTravel=0;float[] last=null;
                for(int i=0;i<31;i++) {
                    float t=i/30f;animator.Play(entry.clip,0,t);animator.Play(entry.faceClip,1,t);animator.Update(0);actionFaces.Advance(0);
                    float b=faceRenderer.GetBlendShapeWeight(blink);min=Mathf.Min(min,b);max=Mathf.Max(max,b);
                    var weights=actionFaces.MouthWeights;
                    normalized &= Mathf.Abs(weights.Sum()-1)<.001f && weights.All(w=>w>=0 && w<=1);
                    normalized &= Mathf.Abs(faceRenderer.GetBlendShapeWeight(faceRenderer.sharedMesh.GetBlendShapeIndex("Pet_EyeWide")))<.01f;
                    if(last!=null) mouthTravel+=weights.Zip(last,(x,y)=>Mathf.Abs(x-y)).Sum();
                    last=(float[])weights.Clone();
                }
                if(entry.name!="Sleep" && entry.name!="Walk") {
                    bool change=max-min>2 && mouthTravel>.2f;varied &= change;
                    if(!change) Debug.Log("FACE_VARIATION_FAILURE "+entry.name+" blinkRange="+(max-min)+" mouthTravel="+mouthTravel+" profile="+actionFaces.CurrentProfile);
                }
            }
            playback.PlayAction("Wave",0);animator.Play("PetV2_Wave",0,.42f);animator.Play("PetV2_FaceProcess_Wave",1,.42f);animator.Update(0);actionFaces.Advance(0);
            float beforeBlink=faceRenderer.GetBlendShapeWeight(blink);var beforeMouth=(float[])actionFaces.MouthWeights.Clone();
            playback.PlayAction("Confused",.35f);animator.Update(0);actionFaces.Advance(0);
            bool continuous=Mathf.Abs(beforeBlink-faceRenderer.GetBlendShapeWeight(blink))<.01f && beforeMouth.Zip(actionFaces.MouthWeights,(a,b)=>Mathf.Abs(a-b)).Sum()<.001f;
            for(int tick=0;tick<25;tick++) { animator.Update(.02f);actionFaces.Advance(.02f); }
            continuous &= actionFaces.CurrentProfile=="Confused";
            // Manual emotions remain available; they must not be reset by body playback.
            playback.SetManualEmotion("sleepy",0);float manual=faceRenderer.GetBlendShapeWeight(blink);
            playback.PlayAction("Thinking",0);animator.Update(.2f);actionFaces.Advance(.2f);
            bool faceBlendOkay=Mathf.Abs(manual-faceRenderer.GetBlendShapeWeight(blink))<.01f && manual>99;
            playback.ResumeActionExpressions(0);animator.Update(0);actionFaces.Advance(0);
            var head=instance.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bip001 Head");
            animator.SetLayerWeight(1,0);animator.Play("PetV2_Sleep",0,.25f);animator.Update(0);var bodyHead=head.localRotation;
            animator.SetLayerWeight(1,1);animator.Play("PetV2_Sleep",0,.25f);animator.Play("PetV2_FaceProcess_Sleep",1,.25f);animator.Update(0);actionFaces.Advance(0);
            bool headIntact=Quaternion.Angle(bodyHead,head.localRotation)<.01f;
            playback.PlayAction("Idle",0);animator.Update(0);actionFaces.Advance(0);
            var halo=instance.GetComponentsInChildren<MeshFilter>(true).First(m=>m.name=="CHDDD_Halo_Restored");
            bool maskOkay=true;
            for(int i=0;i<mask.transformCount;i++) if(mask.GetTransformPath(i).EndsWith("Bip001 Head")) maskOkay &= !mask.GetTransformActive(i);
            var result=new Result {
                revision=map.revision,unityVersion=Application.unityVersion,genericAvatarValid=avatar.isValid&&!avatar.isHuman,
                bodyClips=map.actions.Count(a=>motions.ContainsKey(a.clip)),faceClips=emotions.expressions.Length,actionFaceClips=map.actions.Count(a=>motions.ContainsKey(a.faceClip)),
                clips=motions.Keys.ToArray(),weaponsAbsent=!instance.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("CH0069_Weapon")),
                haloAttached=halo.GetComponentsInParent<Transform>().Any(t=>t.name=="Bip001 Head"),faceMaskExcludesHead=maskOkay,
                faceShapesPresent=faceRenderer.sharedMesh.blendShapeCount==5,
                loopSettingsValid=map.actions.All(a=>motions[a.clip].isLooping==a.loop && motions[a.faceClip].isLooping==a.loop),
                runtimePlaybackVerified=playbackOkay,faceDoesNotOverrideHead=headIntact,rootMotionDisabled=!animator.applyRootMotion,
                automaticTransitionsVerified=automaticOkay,multipleLoopPlaybackVerified=loopsOkay,faceBlendPersistsDuringBodyPlayback=faceBlendOkay,
                actionFacesVary=varied,interruptedFaceContinuous=continuous,mouthNormalized=normalized,faceLayersSynchronized=faceSync,
                original29SourceConfirmed=map.actions.First(a=>a.name=="Dragging").source.Contains("#29") && Mathf.Abs(map.actions.First(a=>a.name=="Dragging").durationSeconds-337/30f)<.001f,
                configuredTransitionsVerified=configuredTransitionsOkay,
                shaderErrors=ShaderUtil.ShaderHasError(Shader.Find("CHDDD/Toon"))||ShaderUtil.ShaderHasError(Shader.Find("CHDDD/MouthAtlas"))
            };
            result.passed=result.genericAvatarValid&&result.bodyClips==18&&result.faceClips==9&&result.actionFaceClips==18&&varied&&continuous&&normalized&&faceSync&&result.original29SourceConfirmed&&result.weaponsAbsent&&result.haloAttached&&result.faceMaskExcludesHead&&result.faceShapesPresent&&result.loopSettingsValid&&result.runtimePlaybackVerified&&result.faceDoesNotOverrideHead&&result.rootMotionDisabled&&automaticOkay&&loopsOkay&&faceBlendOkay&&!result.shaderErrors;
            PrefabUtility.SaveAsPrefabAsset(instance,root+"/CHDDD_Actions_v2.prefab");AssetDatabase.SaveAssets();
            result.passed &= configuredTransitionsOkay;
            File.WriteAllText(output+"/body_v2_unity_validation.json",JsonUtility.ToJson(result,true));
            if(result.passed) AssetDatabase.ExportPackage(root+"/CHDDD_Actions_v2.prefab",output+"/CHDDD_Actions_v2.unitypackage",ExportPackageOptions.IncludeDependencies);
            UnityEngine.Object.DestroyImmediate(instance);
            Debug.Log("CHDDD_BODY_V2 "+JsonUtility.ToJson(result));EditorApplication.Exit(result.passed ? 0 : 2);
        } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(3); }
    }
}
