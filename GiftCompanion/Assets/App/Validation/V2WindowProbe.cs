using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Config;
#endif
namespace GiftCompanion.Validation
{
    public sealed class V2WindowProbe:MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        async void Start()
        {
            var args=Environment.GetCommandLineArgs();int marker=Array.IndexOf(args,"-v2-window-probe");if(marker<0||marker+1>=args.Length)return;
            string output=Path.GetFullPath(args[marker+1]);Directory.CreateDirectory(output);Directory.CreateDirectory(output+"/Frames");
            var flags=new Dictionary<string,bool>();var times=new List<float>();string error=null;var root=GetComponent<AppRoot>();
            var recording=new List<object>();var timer=System.Diagnostics.Stopwatch.StartNew();int frameIndex=0;
            async Task MovieFrame(){string file="Frames/"+(frameIndex++).ToString("D4")+".png";await Capture(output+"/"+file);recording.Add(new{file,milliseconds=timer.ElapsedMilliseconds});}
            async Task RecordFor(int milliseconds){long end=timer.ElapsedMilliseconds+milliseconds;while(timer.ElapsedMilliseconds<end){await MovieFrame();await Task.Delay(50);}}
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));var ct=timeout.Token;
            try
            {
                await root.Initialization;
                CompanionScreen screen=null;
                while(screen==null||screen.WindowView==null){ct.ThrowIfCancellationRequested();screen=FindFirstObjectByType<CompanionScreen>();await Task.Delay(30,ct);}
                var core=root.Character;var view=screen.WindowView;var visual=FindFirstObjectByType<CompanionPresentation>();var animation=visual.Loader.Character.AnimationState;
                flags["oneSkeleton"]=FindObjectsByType<Spine.Unity.SkeletonAnimation>(FindObjectsSortMode.None).Length==1;
                flags["realDepthParts"]=view.PartCount>=3&&view.ForegroundParts>0;
                flags["atmosphereMaterialIncluded"]=Resources.Load<Material>("MikaVisual/WindowAtmosphere")!=null;
                flags["voiceNotStarted"]=root.Voice.OwnedProcessId==0;
                await Capture(output+"/人物起点.png");
                await MovieFrame();
                screen.OpenMenu();
                while(!view.AtWindow){ct.ThrowIfCancellationRequested();view.Focused=true;times.Add(Time.unscaledDeltaTime);await MovieFrame();await Task.Delay(50,ct);}
                await Capture(output+"/窗边终点.png");float holdTime=view.AmbientTime;
                await RecordFor(10000);flags["explicitWindowHold"]=view.AtWindow;
                flags["ambientContinuesAtEndpoint"]=view.AmbientTime>holdTime+9;
                await Capture(output+"/终点持续动态.png");
                screen.OpenSpaces();float panelTime=view.AmbientTime;await RecordFor(500);flags["innerPage"]=screen.ActivePanel=="Spaces";flags["ambientContinuesInsidePanel"]=view.AmbientTime>panelTime+.3f;
                await Capture(output+"/空间内页.png");screen.Back();await RecordFor(250);
                flags["sameCoreAndAnimator"]=ReferenceEquals(core,root.Character)&&ReferenceEquals(animation,visual.Loader.Character.AnimationState);
                screen.SetReducedWindowMotion(true);screen.Back();flags["reducedReturn"]=view.AtCharacter;screen.OpenMenu();flags["reducedForward"]=view.AtWindow;
                screen.SetReducedWindowMotion(false);view.GoCharacter();
                float returnTime=view.AmbientTime;
                while(!view.AtCharacter){ct.ThrowIfCancellationRequested();view.Focused=true;await MovieFrame();await Task.Delay(50,ct);}
                await MovieFrame();
                flags["ambientNeverResetsOnReturn"]=view.AmbientTime>=returnTime;
                await Capture(output+"/返回人物.png");
                Screen.SetResolution(1100,800,FullScreenMode.Windowed);await Task.Delay(400,ct);view.Advance(0);
                var viewport=visual.Loader.renderCamera.pixelRect;flags["resizedViewportFillsWindow"]=Mathf.Abs(viewport.width-Screen.width)<1&&Mathf.Abs(viewport.height-Screen.height)<1;
                await Capture(output+"/窗口缩放铺满.png");Screen.SetResolution(1280,720,FullScreenMode.Windowed);await Task.Delay(300,ct);
                flags["schemaUnchanged"]=root.DataSchemaVersion==10;
                await root.ShutdownAsync();flags["savedShutdown"]=root.ShutdownSucceeded;
            }
            catch(Exception e){error=e.ToString();}
            bool passed=error==null&&flags.Count>=10&&flags.Values.All(value=>value);
            AtomicDocument.WriteJson(output+"/recording.json",recording,default,false);
            AtomicDocument.WriteJson(output+"/native.json",new{passed,error,flags,graphics=SystemInfo.graphicsDeviceName,meanSampleFrameMs=times.Count>0?times.Average()*1000:0,scope="D3D11 isolated silent build, sampled frame intervals during forward travel; not universal FPS or voice acceptance"},default,false);
            if(root!=null)await root.ShutdownAsync();Application.Quit(passed?0:1);
        }
        Task Capture(string path){var result=new TaskCompletionSource<bool>();StartCoroutine(CaptureFrame(path,result));return result.Task;}
        System.Collections.IEnumerator CaptureFrame(string path,TaskCompletionSource<bool> result)
        {
            yield return new WaitForEndOfFrame();Texture2D frame=null;
            try{frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();File.WriteAllBytes(path,frame.EncodeToPNG());result.TrySetResult(true);}
            catch(Exception e){result.TrySetException(e);}finally{if(frame!=null)Destroy(frame);}
        }
#endif
    }
}
