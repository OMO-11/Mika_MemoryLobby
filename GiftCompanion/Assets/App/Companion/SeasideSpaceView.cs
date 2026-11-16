using System;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using GiftCompanion.Core.Character;

namespace GiftCompanion.Companion
{
    // Adapts the delivered framing/interaction to the existing world-space space loader.
    // One skeleton and one animation state; no second session or scene bootstrap.
    [DefaultExecutionOrder(-40)]
    public sealed class SeasideSpaceView : MonoBehaviour
    {
        CompanionValidationBootstrap loader;
        SkeletonAnimation role;
        Camera camera;
        SeasideGlass glass;
        bool captured,patting,released,intro;
        CharacterVisualState sceneState;
        float introCut,flash,reveal=-1,patAge,idleDelay=9,eyeUntil;
        public bool MenuVisible;
        public bool ReduceMotion;
        public bool IsEntrancePlaying=>intro;
        public bool IsPatting=>patting;
        public int MappingFailures {get;private set;}
        public float FlashAlpha=>flash;
        public void Bind(CompanionValidationBootstrap value,bool reduced)
        {
            loader=value;role=value.Character;camera=value.renderCamera;ReduceMotion=reduced;
            role.OnPostProcessVertices+=WidenBackground;
            role.UpdateLocal+=ApplyMouth;
            role.BeforeApply+=RemoveExpression;
            role.UpdateLocal+=ApplyExpression;
            loader.Speech.MouthChanged=SetMouth;
            glass=camera.GetComponent<SeasideGlass>();if(glass==null)glass=camera.gameObject.AddComponent<SeasideGlass>();
            var animation=role.Skeleton.Data.FindAnimation("Start_Idle_01");introCut=animation.Duration;
            foreach(var timeline in animation.Timelines)
                if(timeline is AttachmentTimeline a && role.Skeleton.Data.Slots.Items[a.SlotIndex].Name=="00_face")
                {bool seen=false;for(int i=0;i<a.Frames.Length;i++){if(a.AttachmentNames[i]!=null)seen=true;else if(seen){introCut=a.Frames[i];break;}}}
            if(!reduced){intro=true;var entry=role.AnimationState.SetAnimation(0,"Start_Idle_01",false);entry.TimeScale=1.25f;entry.AnimationEnd=introCut;}
            CreateOceanUnderlay();
        }
        Slot mouth;Attachment mouthClosed,mouthOpen;float speechOpenness;
        public bool MouthReady=>mouth!=null&&mouthOpen!=null;
        public string MouthSlot=>mouth?.Data.Name;
        public bool MouthIsOpen=>mouth!=null&&mouth.Attachment==mouthOpen&&mouthOpen!=null;
        void SetMouth(float value){speechOpenness=value;}
        void ApplyMouth(ISkeletonAnimation _)
        {
            if(mouth==null){
                // Find actual exported mouth attachments, never invent names or play a fixed line.
                foreach(var slot in role.Skeleton.Slots)
                    if(slot.Data.Name.IndexOf("mouth",StringComparison.OrdinalIgnoreCase)>=0){
                        mouth=slot;mouthClosed=slot.Data.AttachmentName==null?slot.Attachment:role.Skeleton.GetAttachment(slot.Data.Index,slot.Data.AttachmentName);
                        foreach(var animation in role.Skeleton.Data.Animations)
                            if(animation.Name=="Talk_01_A_CN")foreach(var timeline in animation.Timelines)
                                if(timeline is AttachmentTimeline a&&a.SlotIndex==slot.Data.Index)
                                    foreach(var name in a.AttachmentNames){var attachment=name==null?null:role.Skeleton.GetAttachment(a.SlotIndex,name);if(attachment!=null&&attachment!=mouthClosed){mouthOpen=attachment;break;}}
                        break;
                    }
            }
            if(!intro&&!patting&&mouth!=null&&mouthOpen!=null)
                mouth.Attachment=speechOpenness>.1f?mouthOpen:mouthClosed;
        }
        void CreateOceanUnderlay()
        {
            var go=new GameObject("Seaside ocean edge",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            var mesh=new Mesh();mesh.name="Seaside ocean underlay";
            mesh.vertices=new[]{new Vector3(-60,-1.27f,1),new Vector3(60,-1.27f,1),new Vector3(-60,9.19f,1),new Vector3(60,9.19f,1)};
            mesh.uv=new[]{new Vector2(.85449219f,.36523438f),new Vector2(.85449219f,.609375f),new Vector2(.78125f,.36523438f),new Vector2(.78125f,.609375f)};mesh.triangles=new[]{0,2,1,2,3,1};
            go.GetComponent<MeshFilter>().sharedMesh=mesh;underlay=mesh;
            foreach(var atlas in role.SkeletonDataAsset.atlasAssets)foreach(var material in atlas.Materials)
                if(material.mainTexture.name=="CH0294_home_3")go.GetComponent<MeshRenderer>().sharedMaterial=material;
            go.GetComponent<MeshRenderer>().sortingOrder=-10;
        }
        Mesh underlay;
        void WidenBackground(MeshGeneratorBuffers buffers)
        {
            float width=20.92f*camera.aspect,scale=width>32?width/32*1.08f:1;
            if(scale<=1)return;
            int vertex=0;
            foreach(var slot in role.Skeleton.DrawOrder){if(!slot.Bone.Active||slot.A==0)continue;
                int count=slot.Attachment is RegionAttachment?4:slot.Attachment is MeshAttachment m?m.WorldVerticesLength/2:0;
                if(slot.Attachment is ClippingAttachment){MappingFailures++;return;}
                vertex+=count;
            }
            if(vertex!=buffers.vertexCount){MappingFailures++;return;}
            vertex=0;
            foreach(var slot in role.Skeleton.DrawOrder){if(!slot.Bone.Active||slot.A==0)continue;
                int count=slot.Attachment is RegionAttachment?4:slot.Attachment is MeshAttachment m?m.WorldVerticesLength/2:0;
                if(slot.Data.Name.StartsWith("BG_",StringComparison.Ordinal))for(int i=vertex;i<vertex+count;i++)buffers.vertexBuffer[i].x=.5f+(buffers.vertexBuffer[i].x-.5f)*scale;
                vertex+=count;
            }
        }
        public void Capture(bool value){captured=value;if(value)EndPat();}
        public void SetState(CharacterVisualState state){sceneState=state;if(state==CharacterVisualState.Thinking||state==CharacterVisualState.Speaking){FinishEntrance();EndPat();idleDelay=10;}}
        public void FinishEntrance(){if(!intro)return;intro=false;reveal=-1;flash=0;role.AnimationState.SetAnimation(0,"Idle_01",true);}
        public bool HeadContains(Vector2 point)
        {
            var head=role.Skeleton.FindBone("Head_Rot");if(head==null)return false;
            Vector3 world=camera.ScreenToWorldPoint(new Vector3(point.x,point.y,100));var local=transform.InverseTransformPoint(world);
            head.WorldToLocal(local.x,local.y,out float x,out float y);
            return Mathf.Pow((x-4.65f)/2.25f,2)+Mathf.Pow(y/3.3f,2)<=1;
        }
        public bool BeginPat(){if(captured||intro||patting||sceneState==CharacterVisualState.Thinking||sceneState==CharacterVisualState.Speaking)return false;StopExpression();patting=true;released=false;patAge=0;
            Overlay(1,"Pat_01_M",false);Overlay(2,"Pat_01_A",false);return true;}
        void Overlay(int track,string name,bool loop){role.AnimationState.SetEmptyAnimation(track,0);var e=role.AnimationState.AddAnimation(track,name,loop,0);e.Delay=0;e.MixDuration=.2f;}
        public void EndPat(){if(patting)released=true;}
        void Update()
        {
            if(loader==null)return;
            glass.Visible=MenuVisible;glass.Flash=flash;
            float dt=Time.unscaledDeltaTime;
            // Keep the delivered fixed vertical field, while expanding only background geometry.
            camera.orthographicSize=10.46f;camera.transform.position=new Vector3(camera.aspect>=32/20.92f?.5f:Mathf.Clamp(3.5f,-15.5f+10.46f*camera.aspect,16.5f-10.46f*camera.aspect),9.19f,-100);
            if(intro){
                if(ReduceMotion)FinishEntrance();
                else if(reveal>=0){reveal+=dt;flash=1-Mathf.SmoothStep(0,1,(reveal-.08f)/.65f);if(reveal>=.73f){intro=false;flash=0;}}
                else {var entry=role.AnimationState.GetCurrent(0);float remaining=Mathf.Max(0,(introCut-entry.TrackTime)/1.25f);flash=1-Mathf.SmoothStep(0,1,remaining/.35f);
                    if(remaining<=Mathf.Min(dt,.1f)){flash=1;role.AnimationState.ClearTracks();role.Skeleton.SetToSetupPose();role.AnimationState.SetAnimation(0,"Idle_01",true);reveal=0;}}
            }
            var mouse=Mouse.current;
            if(!captured&&!intro&&Application.isFocused&&mouse!=null){var point=mouse.position.ReadValue();
                if(mouse.leftButton.wasPressedThisFrame&&!(EventSystem.current?.IsPointerOverGameObject()??false)&&HeadContains(point))BeginPat();
                if(patting&&(!mouse.leftButton.isPressed||!HeadContains(point)))EndPat();}
            if(patting){patAge+=dt;if(released&&patAge>=.35f){Overlay(1,"PatEnd_01_M",false);Overlay(2,"PatEnd_01_A",false);role.AnimationState.AddEmptyAnimation(1,.3f,0);role.AnimationState.AddEmptyAnimation(2,.3f,0);patting=false;idleDelay=9;}}
            bool available=!intro&&!patting&&!captured&&sceneState==CharacterVisualState.Idle;
            if(available){idleDelay-=dt;if(idleDelay<=0&&!ReduceMotion){
                expressionKind=1+expressionSequence++%3;eyeUntil=Time.unscaledTime+(expressionKind==2?2.5f:3.5f);idleDelay=11;
                if(expressionKind==2){var eyes=role.AnimationState.SetAnimation(3,"Eye_Close_01",false);eyes.TrackTime=1f/6;eyes.TimeScale=0;}
            }}
            if(expressionKind!=0&&(!available||ReduceMotion||Time.unscaledTime>=eyeUntil))StopExpression();
            expressionWeight=Mathf.MoveTowards(expressionWeight,expressionKind==0?0:1,dt/.35f);
        }
        int expressionKind,expressionSequence,lastExpressionKind;
        float expressionWeight,headDelta,eyeX,eyeY;
        Bone headBone,leftEye,rightEye;bool offsetsApplied;
        void StopExpression(){if(expressionKind==2)role.AnimationState.SetEmptyAnimation(3,.3f);expressionKind=0;}
        void RemoveExpression(ISkeletonAnimation _){if(!offsetsApplied)return;
            if(headBone!=null)headBone.Rotation-=headDelta;
            if(leftEye!=null){leftEye.X-=eyeX;leftEye.Y-=eyeY;}if(rightEye!=null){rightEye.X-=eyeX;rightEye.Y-=eyeY;}offsetsApplied=false;
        }
        void ApplyExpression(ISkeletonAnimation _){
            if(headBone==null){headBone=role.Skeleton.FindBone("Head_Rot");leftEye=role.Skeleton.FindBone("L_eye_1_1");rightEye=role.Skeleton.FindBone("R_eye_1_1");}
            if(expressionWeight<=0)return;if(expressionKind!=0)lastExpressionKind=expressionKind;
            float weight=Mathf.SmoothStep(0,1,expressionWeight);
            headDelta=(lastExpressionKind==3?-1.25f:lastExpressionKind==2?1.1f:.75f)*weight;
            eyeX=(lastExpressionKind==3?-.075f:lastExpressionKind==1?.045f:0)*weight;eyeY=(lastExpressionKind==3?.025f:0)*weight;
            if(headBone!=null)headBone.Rotation+=headDelta;if(leftEye!=null){leftEye.X+=eyeX;leftEye.Y+=eyeY;}if(rightEye!=null){rightEye.X+=eyeX;rightEye.Y+=eyeY;}offsetsApplied=true;
        }
        void OnApplicationFocus(bool focused){if(!focused)EndPat();}
        void OnDestroy(){if(role!=null){role.OnPostProcessVertices-=WidenBackground;role.UpdateLocal-=ApplyMouth;role.BeforeApply-=RemoveExpression;role.UpdateLocal-=ApplyExpression;RemoveExpression(role);loader.Speech.MouthChanged=null;}if(glass!=null){glass.Visible=false;glass.Flash=0;}if(underlay!=null)Destroy(underlay);}
    }
    public sealed class SeasideGlass:MonoBehaviour
    {
        public bool Visible;public float Flash;
        Material material;
        void OnRenderImage(RenderTexture source,RenderTexture destination){
            if(!Visible&&Flash<=0){Graphics.Blit(source,destination);return;}
            if(material==null){var template=Resources.Load<Material>("MikaVisual/SeasideGlass");if(template==null){Graphics.Blit(source,destination);return;}material=new Material(template);}
            material.SetFloat("_Blur",Visible?1:0);material.SetFloat("_Flash",Flash);
            var small=RenderTexture.GetTemporary(Mathf.Max(1,source.width/2),Mathf.Max(1,source.height/2),0);Graphics.Blit(source,small);Graphics.Blit(small,destination,material);RenderTexture.ReleaseTemporary(small);
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
