"""Adapt the already verified import audit without changing the V1 delivery."""
from pathlib import Path
base=Path('W:/Machine/VSCode/Mika_Gift/Tools/AssetPipeline')
s=(base/'CHDDDBodyV1Audit.cs').read_text(encoding='utf-8').replace('V1','V2').replace('v1','v2').replace('PetV1_','PetV2_')
s=s.replace('public string unityVersion; public int bodyClips,faceClips;',
'''public bool actionFacesVary,interruptedFaceContinuous,mouthNormalized,faceLayersSynchronized,original29SourceConfirmed;
        public string unityVersion; public int bodyClips,faceClips,actionFaceClips;''')
s=s.replace('a=>a.clip==item.name','a=>a.clip==item.name || a.faceClip==item.name')
s=s.replace('StartsWith("PetV2_Face_")','StartsWith("PetV2_Face")')
s=s.replace('controller.layers=layers;\n            var animator=', '''var faceStates=new System.Collections.Generic.Dictionary<string,AnimatorState>();
            foreach(var entry in map.actions) {
                var fs=layers[1].stateMachine.AddState(entry.faceClip);fs.motion=motions[entry.faceClip];faceStates[entry.name]=fs;
            }
            layers[1].stateMachine.defaultState=faceStates[map.defaultAction];
            foreach(var entry in map.actions.Where(a=>!string.IsNullOrEmpty(a.nextAction))) {
                var tr=faceStates[entry.name].AddTransition(faceStates[entry.nextAction]);tr.hasExitTime=true;tr.exitTime=1;
                tr.hasFixedDuration=true;tr.duration=.10f;tr.interruptionSource=TransitionInterruptionSource.SourceThenDestination;
            }
            controller.layers=layers;
            var animator=''')
s=s.replace('playback.actionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/body_action_map_v2.json");',
'''playback.actionMap=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/body_action_map_v2.json");
            var actionFaces=instance.AddComponent<CHDDDActionFaceControllerV2>();
            actionFaces.animator=animator;actionFaces.faceRenderer=faceRenderer;actionFaces.bodyController=playback;
            actionFaces.profileData=AssetDatabase.LoadAssetAtPath<TextAsset>(root+"/action_face_profiles_v2.json");
            playback.actionFaces=actionFaces;''')
start=s.index('            var flags=System.Reflection.BindingFlags.')
end=s.index('            var halo=',start)
s=s[:start]+'''            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
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
''' + s[end:]
s=s.replace('faceClips=emotions.expressions.Length,','faceClips=emotions.expressions.Length,actionFaceClips=map.actions.Count(a=>motions.ContainsKey(a.faceClip)),')
s=s.replace('loopSettingsValid=map.actions.All(a=>motions[a.clip].isLooping==a.loop),','loopSettingsValid=map.actions.All(a=>motions[a.clip].isLooping==a.loop && motions[a.faceClip].isLooping==a.loop),')
s=s.replace('shaderErrors=ShaderUtil.', '''actionFacesVary=varied,interruptedFaceContinuous=continuous,mouthNormalized=normalized,faceLayersSynchronized=faceSync,
                original29SourceConfirmed=map.actions.First(a=>a.name=="Dragging").source.Contains("#29") && Mathf.Abs(map.actions.First(a=>a.name=="Dragging").durationSeconds-337/30f)<.001f,
                shaderErrors=ShaderUtil.''')
s=s.replace('&&result.faceClips==9','&&result.faceClips==9&&result.actionFaceClips==18&&varied&&continuous&&normalized&&faceSync&&result.original29SourceConfirmed')
(base/'CHDDDBodyV2Audit.cs').write_text(s,encoding='utf-8')
print('V2 Unity audit prepared')
