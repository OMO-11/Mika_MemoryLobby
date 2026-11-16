using System;
using System.Collections.Generic;
using GiftCompanion.Core.Config;
using GiftCompanion.SharedUI;
using Spine;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GiftCompanion.Companion
{
    // Only presentation changes: one skeleton, one AnimationState, independently transformed mesh parts.
    [DefaultExecutionOrder(100)]
    public sealed class CompanionWindowView : MonoBehaviour, ICanvasRaycastFilter
    {
        CompanionValidationBootstrap loader;
        CompanionVisualSettings settings;
        VisualSpace space;
        SkeletonRenderSeparator separator;
        readonly List<int> groups=new List<int>();
        readonly List<Slot> boundaries=new List<Slot>();
        Camera camera;
        Canvas worldCanvas;
        CanvasGroup uiGroup;
        RectTransform menu;
        TMP_Text timeText,dateText;
        Vector3 cameraCenter;
        float unit,position,target;
        float ambientTime;
        bool focused=true,disposed,reduceMotion;
        Action<int> selected;
        Material atmosphere;
        GameObject effects;
        MaterialPropertyBlock blurBlock;
        public float Progress => Mathf.SmoothStep(0,1,position);
        public bool AtWindow => position==1 && target==1;
        public bool AtCharacter => position==0 && target==0;
        public bool Moving => position!=target;
        public float AmbientTime => ambientTime;
        public bool HasWindow => space!=null;
        public int PartCount => separator==null?0:separator.partsRenderers.Count;
        public int ForegroundParts { get; private set; }
        public string LastOcclusionPart { get; private set; }
        public bool PanelOpen { get; set; }
        public float PanelBlend { get; private set; }
        public bool ReduceMotion { get => reduceMotion; set { reduceMotion=value; if(value)position=target; Apply(); } }
        public bool Focused { get => focused; set => focused=value; }
        public event Action CharacterReached;
        public void Bind(CompanionValidationBootstrap value,CompanionVisualSettings config,string id,Action<int> callback)
        {
            loader=value; settings=config; reduceMotion=config.ReduceMotion; space=config.FindSpace(id); selected=callback;
            blurBlock=new MaterialPropertyBlock();
            camera=loader.renderCamera;cameraCenter=camera.transform.position;unit=camera.orthographicSize*2/720;
            if(space!=null)
            {
                if(!loader.IsPlayingIntro)BuildSeparation();
                worldCanvas=new GameObject("Window world UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(GraphicRaycaster)).GetComponent<Canvas>();
                worldCanvas.transform.SetParent(transform,false);worldCanvas.renderMode=RenderMode.WorldSpace;worldCanvas.worldCamera=camera;worldCanvas.overrideSorting=true;worldCanvas.sortingOrder=500;
                ((RectTransform)worldCanvas.transform).sizeDelta=new Vector2(1280,720);
                worldCanvas.gameObject.AddComponent<WindowOcclusionFilter>().Owner=this;
                uiGroup=worldCanvas.GetComponent<CanvasGroup>();
                menu=CompanionVisualFoundation.CreateMenu(worldCanvas.transform,config,id,0,OnMenu);
                timeText=MakeText("Time",new Vector2(space.ClockX,space.ClockY),config.Theme.ClockSize,new Vector2(0,0));
                dateText=MakeText("Date",new Vector2(space.ClockX,space.ClockY),config.Theme.DateSize,new Vector2(1,-38));
                BuildAtmosphere();
            }
            else
            {
                worldCanvas=new GameObject("Fallback window UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
                worldCanvas.transform.SetParent(transform,false);worldCanvas.renderMode=RenderMode.ScreenSpaceOverlay;worldCanvas.sortingOrder=15;
                var scaler=worldCanvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
                menu=CompanionVisualFoundation.CreateMenu(worldCanvas.transform,config,id,0,OnMenu);
            }
            Apply();
        }
        void BuildSeparation()
        {
            var skeleton=loader.Character.Skeleton;int last=-1;
            loader.Character.separatorSlots.Clear();
            foreach(var slot in skeleton.DrawOrder)
            {
                string path=PathOf(slot.Attachment);
                if(path==null)continue;
                int group=Group(path);
                if(group!=last){if(last!=-1){loader.Character.separatorSlots.Add(slot);boundaries.Add(slot);}groups.Add(group);last=group;}
            }
            separator=SkeletonRenderSeparator.AddToSkeletonRenderer(loader.Character,0,0,1,0);
            separator.copyMeshRendererFlags=false;
            separator.OnMeshAndMaterialsUpdated+=AfterMesh;
            loader.Character.updateWhenInvisible=UpdateMode.FullUpdate;
            for(int i=0;i<separator.partsRenderers.Count;i++)
            {int group=groups[Mathf.Min(i,groups.Count-1)];separator.partsRenderers[i].MeshRenderer.sortingOrder=group*1000+i;if(group>0)ForegroundParts++;}
        }
        static string PathOf(Attachment attachment) => attachment is MeshAttachment mesh?mesh.Path:attachment is RegionAttachment region?region.Path:null;
        static int Group(string path) => path.IndexOf("Curtain",StringComparison.OrdinalIgnoreCase)>=0?1:path.StartsWith("BG/",StringComparison.Ordinal)||path.StartsWith("BG2/",StringComparison.Ordinal)||path.StartsWith("BG_",StringComparison.Ordinal)?0:2;
        TMP_Text MakeText(string name,Vector2 anchor,float size,Vector2 offset)
        {
            var root=CompanionUIFactory.Rect(worldCanvas.transform,name+" anchor",new Vector2(anchor.x-640,360-anchor.y),Vector2.zero);root.localRotation=Quaternion.Euler(0,0,-space.UiAngle);
            var text=CompanionUIFactory.Text(root,name,"",offset,new Vector2(300,70),size,TextAlignmentOptions.TopLeft);
            text.rectTransform.pivot=new Vector2(0,1);text.rectTransform.anchoredPosition=offset;
            CompanionVisualFoundation.Style(text,settings.Theme,size);text.color=CompanionVisualFoundation.Color(settings.Theme.ClockInk,settings.Theme.ClockAlpha);text.overflowMode=TextOverflowModes.Overflow;return text;
        }
        void OnMenu(int index){if(!AtWindow||PanelOpen)return;if(index==5)GoCharacter();else selected?.Invoke(index);}
        public void GoWindow(){if(disposed)return;loader.SkipIntro();target=1;if(space==null||reduceMotion)position=target;loader.Character.GetComponent<CompanionPointerInteraction>()?.Cancel();Apply();}
        public void GoCharacter(){if(disposed)return;target=0;if(space==null||reduceMotion){position=0;CharacterReached?.Invoke();}Apply();}
        public void Advance(float delta)
        {
            if(disposed)return;
            // The room keeps breathing even while the camera is stationary or unfocused.
            ambientTime+=Mathf.Max(0,delta);
            UpdateAtmosphere();
            if(!focused)return;
            bool wasCharacter=AtCharacter;
            position=Mathf.MoveTowards(position,target,Mathf.Max(0,delta)/(target==1?(space?.ForwardSeconds??3.2f):(space?.ReturnSeconds??2.4f)));
            PanelBlend=reduceMotion?(PanelOpen?1:0):Mathf.MoveTowards(PanelBlend,PanelOpen?1:0,delta*5);
            Apply();if(!wasCharacter&&AtCharacter)CharacterReached?.Invoke();
        }
        void Update()
        {
            if(loader==null||disposed)return;
            if(reduceMotion)loader.SkipIntro();
            if(space!=null&&!loader.IsPlayingIntro&&separator==null)BuildSeparation();
            Advance(Time.unscaledDeltaTime);
            if(space!=null){timeText.text=DateTime.Now.ToString("HH : mm");dateText.text=DateTime.Now.ToString("yyyy.MM.dd");}
            if(Application.isFocused&&AtCharacter&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&!(EventSystem.current?.IsPointerOverGameObject()??false)&&HitClock(Mouse.current.position.ReadValue()))GoWindow();
        }
        public bool HitClock(Vector2 screen)
        {
            if(space==null||loader.IsPlayingIntro||IsOccluded(screen))return false;
            if(!camera.pixelRect.Contains(screen))return false;
            var viewport=CompanionVisualFoundation.SceneViewport(Screen.width,Screen.height);
            var logical=new Vector2((screen.x-viewport.x)/viewport.width*1280,720-(screen.y-viewport.y)/viewport.height*720);
            var point=CompanionVisualFoundation.Unproject(logical,space,Progress)-new Vector2(space.ClockX,space.ClockY);
            float angle=-space.UiAngle*Mathf.Deg2Rad;
            var local=new Vector2(Mathf.Cos(angle)*point.x-Mathf.Sin(angle)*point.y,Mathf.Sin(angle)*point.x+Mathf.Cos(angle)*point.y);
            return local.x>=-5&&local.x<=150&&local.y>=0&&local.y<=65;
        }
        public bool IsRaycastLocationValid(Vector2 screen,Camera eventCamera)=>AtWindow&&!PanelOpen&&!IsOccluded(screen);
        public bool IsOccluded(Vector2 screen)
        {
            LastOcclusionPart=null;
            if(separator==null)return false;
            for(int part=0;part<separator.partsRenderers.Count;part++)
            {
                if(groups[Mathf.Min(part,groups.Count-1)]==0)continue;
                var renderer=separator.partsRenderers[part];var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;if(mesh==null)continue;
                var vertices=mesh.vertices;var uv=mesh.uv;var colors=mesh.colors32;var mats=renderer.MeshRenderer.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var triangles=mesh.GetTriangles(sub);var texture=sub<mats.Length?mats[sub].mainTexture as Texture2D:null;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];
                        Vector2 a=camera.WorldToScreenPoint(renderer.transform.TransformPoint(vertices[ia])),b=camera.WorldToScreenPoint(renderer.transform.TransformPoint(vertices[ib])),c=camera.WorldToScreenPoint(renderer.transform.TransformPoint(vertices[ic]));
                        float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<.00001f)continue;
                        float u=((b.y-c.y)*(screen.x-c.x)+(c.x-b.x)*(screen.y-c.y))/det,v=((c.y-a.y)*(screen.x-c.x)+(a.x-c.x)*(screen.y-c.y))/det,w=1-u-v;
                        if(u<0||v<0||w<0)continue;
                        float alpha=colors.Length>ic?(colors[ia].a*u+colors[ib].a*v+colors[ic].a*w)/255f:1;
                        if(texture!=null&&texture.isReadable){var coord=uv[ia]*u+uv[ib]*v+uv[ic]*w;alpha*=texture.GetPixelBilinear(coord.x,coord.y).a;}
                        if(alpha>.6f){LastOcclusionPart=part+":"+groups[Mathf.Min(part,groups.Count-1)]+" alpha="+alpha;return true;}
                    }
                }
            }
            return false;
        }
        void AfterMesh(SkeletonRenderer renderer)=>ApplyParts();
        void Apply()
        {
            if(loader==null||disposed)return;
            var fill=CompanionVisualFoundation.SceneViewport(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            camera.rect=new Rect(0,0,1,1);
            camera.orthographicSize=loader.Character.GetComponent<SeasideSpaceView>()!=null?10.46f:unit*360*Screen.height/fill.height;
            if(space==null){worldCanvas.gameObject.SetActive(AtWindow&&!PanelOpen);return;}
            worldCanvas.gameObject.SetActive(!loader.IsPlayingIntro);
            ApplyParts();
            worldCanvas.transform.position=new Vector3(cameraCenter.x,cameraCenter.y,0)+Rotate(new Vector3((640-Mathf.Lerp(640,space.CenterX,Progress))*unit,(Mathf.Lerp(360,space.CenterY,Progress)-360)*unit,0),Progress)*Mathf.Lerp(1,space.Zoom,Progress);
            worldCanvas.transform.localRotation=Quaternion.Euler(0,0,-space.Rotation*Progress);worldCanvas.transform.localScale=Vector3.one*unit*Mathf.Lerp(1,space.Zoom,Progress);
            uiGroup.blocksRaycasts=AtWindow&&!PanelOpen;uiGroup.interactable=AtWindow&&!PanelOpen;menu.gameObject.SetActive(!PanelOpen);
            foreach(var text in menu.GetComponentsInChildren<TMP_Text>())text.color=CompanionVisualFoundation.Color(settings.Theme.Ink);
            UpdateAtmosphere();
        }
        void UpdateAtmosphere()
        {
            if(atmosphere==null)return;
            atmosphere.SetFloat("_EffectTime",ambientTime);
            atmosphere.SetFloat("_Strength",reduceMotion?0:settings.EffectStrength);
        }
        Vector3 Rotate(Vector3 point,float p)=>Quaternion.Euler(0,0,-space.Rotation*p)*point;
        Vector3 TransformPoint(Vector3 local,float depth,bool character)
        {
            var center=loader.Character.transform.InverseTransformPoint(new Vector3(cameraCenter.x,cameraCenter.y,0));
            var shift=new Vector3((640-Mathf.Lerp(640,space.CenterX,Progress))*unit,(Mathf.Lerp(360,space.CenterY,Progress)-360)*unit,0);
            if(character)shift+=new Vector3(space.CharacterOffsetX,-space.CharacterOffsetY)*Progress*Progress*unit;
            return center+Rotate(local-center+shift,Progress)*Mathf.Lerp(1,space.Zoom,Progress)*(1+(depth-1)*Progress);
        }
        void ApplyParts()
        {
            if(separator==null)return;
            for(int i=0;i<separator.partsRenderers.Count;i++)
            {
                int group=groups[Mathf.Min(i,groups.Count-1)];var part=separator.partsRenderers[i];float depth=group==0?1:group==1?space.CurtainDepth:space.CharacterDepth;
                part.transform.localPosition=TransformPoint(Vector3.zero,depth,group==2);part.transform.localRotation=Quaternion.Euler(0,0,-space.Rotation*Progress);part.transform.localScale=Vector3.one*Mathf.Lerp(1,space.Zoom,Progress)*(1+(depth-1)*Progress);
                blurBlock.SetFloat("_VisualBlur",group==2&&!reduceMotion&&settings.NearBlur?Mathf.Sin(Progress*Mathf.PI)*settings.EffectStrength:0);part.MeshRenderer.SetPropertyBlock(blurBlock);
            }
        }
        void BuildAtmosphere()
        {
            var template=Resources.Load<Material>("MikaVisual/WindowAtmosphere");if(template==null)return;
            atmosphere=new Material(template);atmosphere.SetVector("_Layers",new Vector4(settings.Moonlight?1:0,settings.Sheer?1:0,settings.Meteors?1:0,0));effects=new GameObject("Window atmosphere",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));effects.transform.SetParent(worldCanvas.transform,false);
            for(int i=0;i<4;i++)atmosphere.SetVector("_Pane"+i,new Vector4(space.Pane[i*2],space.Pane[i*2+1],0,0));
            var rect=(RectTransform)effects.transform;rect.sizeDelta=new Vector2(1280,720);var image=effects.GetComponent<RawImage>();image.material=atmosphere;image.raycastTarget=false;effects.transform.SetAsFirstSibling();
        }
        void OnApplicationFocus(bool value)=>focused=value;
        void OnDestroy()
        {
            disposed=true;if(separator!=null)separator.OnMeshAndMaterialsUpdated-=AfterMesh;
            if(worldCanvas!=null)Destroy(worldCanvas.gameObject);if(atmosphere!=null)Destroy(atmosphere);
        }
    }
    public sealed class WindowOcclusionFilter : MonoBehaviour,ICanvasRaycastFilter
    {public CompanionWindowView Owner;public bool IsRaycastLocationValid(Vector2 point,Camera camera)=>Owner!=null&&Owner.IsRaycastLocationValid(point,camera);}
}
