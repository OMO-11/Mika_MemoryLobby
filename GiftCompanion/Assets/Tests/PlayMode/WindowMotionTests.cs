using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace GiftCompanion.Tests
{
    public sealed class WindowMotionTests
    {
        const string Output="Docs/Design/V2_Visual_Implementation/02_执行记录/V2-2";
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string label){Assert.That(ok,Is.True,label);checks.Add(label);}
        [UnityTest]public IEnumerator RealSkeletonRoundtripAndNavigation()
        {
            PlayTestData.Begin();Directory.CreateDirectory(Output+"/Frames");
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            float until=Time.realtimeSinceStartup+25;
            while(AppRoot.Instance==null||!AppRoot.Instance.IsReady||AppRoot.Instance.Controller.IsSwitching){Assert.That(Time.realtimeSinceStartup,Is.LessThan(until));yield return null;}
            var root=AppRoot.Instance;var core=root.Character;var db=root.Database;
            var switchTask=root.Controller.SwitchModeAsync(CharacterMode.Companion);while(!switchTask.IsCompleted)yield return null;switchTask.GetAwaiter().GetResult();
            CompanionScreen screen=null;
            while(screen==null||screen.WindowView==null){screen=Object.FindFirstObjectByType<CompanionScreen>();Assert.That(Time.realtimeSinceStartup,Is.LessThan(until));yield return null;}
            var view=screen.WindowView;var presentation=Object.FindFirstObjectByType<CompanionPresentation>();var loader=presentation.Loader;var skeleton=loader.Character;var state=skeleton.AnimationState;
            Check(loader.IsPlayingIntro,"native entrance precedes window presentation");loader.SkipIntro();yield return null;yield return null;
            Check(view.HasWindow&&view.PartCount>=3&&view.ForegroundParts>0,"real skeleton separated into scene depth groups");
            view.enabled=false;skeleton.enabled=false;
            Check(Object.FindObjectsByType<Spine.Unity.SkeletonAnimation>(FindObjectsSortMode.None).Length==1,"one skeleton animator");
            var head=skeleton.Skeleton.FindBone(loader.profile.headBone);
            var headPoint=(Vector2)loader.renderCamera.WorldToScreenPoint(skeleton.transform.TransformPoint(new Vector3(head.WorldX,head.WorldY,0)));
            skeleton.LateUpdate();
            Check(view.IsOccluded(headPoint),"foreground head blocks scene UI hit");
            Check(!view.HitClock(headPoint),"head tap cannot navigate through clock");
            Shot(loader.renderCamera,"人物起点.png");
            screen.OpenMenu();view.Advance(.7f);float initial=view.Progress;
            float ambientBefore=view.AmbientTime;view.Focused=false;view.Advance(.5f);Check(view.Progress==initial,"focus loss pauses motion");
            Check(view.AmbientTime>=ambientBefore+.49f,"ambient animation continues while camera is unfocused");view.Focused=true;
            view.GoCharacter();float before=view.Progress;view.Advance(.01f);Check(view.Progress<before&&view.Progress>0,"midflight reverse remains continuous");
            view.GoWindow();view.GoWindow();view.GoWindow();Check(view.Progress<1,"repeated clicks do not skip to destination");
            view.ReduceMotion=true;view.GoCharacter();Check(view.AtCharacter,"reduced motion reaches character immediately");view.ReduceMotion=false;
            skeleton.Update(.05f);skeleton.LateUpdate();
            int frame=0;var watch=Stopwatch.StartNew();float trackBefore=state.GetCurrent(0).TrackTime;
            view.GoWindow();
            for(int i=0;i<49;i++){view.Advance(1f/15);skeleton.Update(1f/15);skeleton.LateUpdate();Frame(loader.renderCamera,frame++);if(i==24)Shot(loader.renderCamera,"经过人物.png");}
            Check(view.AtWindow,"forward ends at window");Shot(loader.renderCamera,"窗边终点.png");
            float hold=view.Progress;float ambientAtHold=view.AmbientTime;for(int i=0;i<15;i++){view.Advance(1f/15);skeleton.Update(1f/15);skeleton.LateUpdate();Frame(loader.renderCamera,frame++);}
            Check(view.Progress==hold&&view.AtWindow,"window holds until explicit return");
            Check(view.AmbientTime>ambientAtHold+.9f,"ambient animation advances at fixed camera endpoint");
            Check(state.GetCurrent(0).TrackTime>trackBefore+3,"live animation advances through travel");
            var clockLocal=new Vector2(80,25);float angle=root.VisualSettings.Value.FindSpace("mika-memory").UiAngle*Mathf.Deg2Rad;
            var anchor=root.VisualSettings.Value.FindSpace("mika-memory");var worldClock=new Vector2(anchor.ClockX,anchor.ClockY)+new Vector2(Mathf.Cos(angle)*clockLocal.x-Mathf.Sin(angle)*clockLocal.y,Mathf.Sin(angle)*clockLocal.x+Mathf.Cos(angle)*clockLocal.y);
            var logical=CompanionVisualFoundation.Project(worldClock,anchor,1);var rect=CompanionVisualFoundation.SceneViewport(Screen.width,Screen.height);
            Check(view.HitClock(new Vector2(rect.x+logical.x/1280*rect.width,rect.y+(720-logical.y)/720*rect.height)),"window clock hit uses matching inverse mapping");
            view.enabled=true;skeleton.enabled=true;
            var spaceButton=view.GetComponentsInChildren<Button>().Single(button=>button.name=="陪伴空间");
            var buttonPoint=(Vector2)loader.renderCamera.WorldToScreenPoint(spaceButton.transform.TransformPoint(new Vector3(50,-8,0)));
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=buttonPoint};
            var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Any(hit=>hit.gameObject==spaceButton.gameObject),"visible window menu survives foreground-aware UI raycast");
            float ambientAtPanel=view.AmbientTime;spaceButton.onClick.Invoke();yield return null;yield return new WaitForSeconds(.3f);Check(screen.ActivePanel=="Spaces"&&view.PanelBlend>.95f,"world menu opens real inner page with continuous entrance");
            Check(view.AmbientTime>ambientAtPanel,"ambient animation continues during panel transition");
            screen.Back();yield return null;Check(screen.ActivePanel==null,"inner page returns to window without mode switch");
            Check(root.Character==core&&root.Database==db&&skeleton.AnimationState==state,"same core database and animation state throughout");
            screen.SetReducedWindowMotion(true);screen.Back();yield return null;Check(view.AtCharacter,"Esc uses reduced-motion return");screen.OpenMenu();yield return null;Check(view.AtWindow,"Esc menu uses reduced-motion forward");
            float ambientAtReturn=view.AmbientTime;screen.SetReducedWindowMotion(false);view.enabled=false;skeleton.enabled=false;view.GoCharacter();
            Check(view.AmbientTime>=ambientAtReturn,"switching views never resets ambient phase");
            for(int i=0;i<37;i++){view.Advance(1f/15);skeleton.Update(1f/15);skeleton.LateUpdate();Frame(loader.renderCamera,frame++);}
            Check(view.AtCharacter,"reverse ends at original composition");Shot(loader.renderCamera,"返回人物.png");
            Check(skeleton.AnimationState==state,"animation state never replaced by travel");
            watch.Stop();
            File.WriteAllText(Output+"/motion_validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,checks,frames=frame,frameRate=15,parts=view.PartCount,foregroundParts=view.ForegroundParts,captureMilliseconds=watch.ElapsedMilliseconds,graphics=SystemInfo.graphicsDeviceName,scope="Unity PlayMode, real skeleton; frame capture cost included, not player FPS benchmark"},Newtonsoft.Json.Formatting.Indented));
            view.enabled=true;skeleton.enabled=true;
        }
        static void Frame(Camera camera,int index)=>Capture(camera,Output+"/Frames/"+index.ToString("D4")+".png",960,540);
        static void Shot(Camera camera,string name)=>Capture(camera,Output+"/"+name,1280,720);
        static void Capture(Camera camera,string file,int width,int height)
        {
            Canvas.ForceUpdateCanvases();var previous=RenderTexture.active;var old=camera.targetTexture;var oldRect=camera.rect;float oldAspect=camera.aspect;var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try{camera.rect=new Rect(0,0,1,1);camera.aspect=1280f/720;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.rect=oldRect;camera.aspect=oldAspect;RenderTexture.active=previous;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);}
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            var root=AppRoot.Instance;if(root!=null){var task=root.ShutdownAsync();while(!task.IsCompleted)yield return null;task.GetAwaiter().GetResult();Object.Destroy(root.gameObject);yield return null;}
            PlayTestData.End();
        }
    }
}
