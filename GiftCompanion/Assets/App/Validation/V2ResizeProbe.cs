using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Config;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#endif
namespace GiftCompanion.Validation
{
    public sealed class V2ResizeProbe : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
        readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
        readonly List<object> sizes=new List<object>();
        string output;
        void Check(bool value,string name){checks[name]=value;if(!value)throw new Exception(name);}
        async void Start()
        {
            var args=Environment.GetCommandLineArgs();int marker=Array.IndexOf(args,"-v2-resize-probe");if(marker<0||marker+1>=args.Length)return;
            output=Path.GetFullPath(args[marker+1]);Directory.CreateDirectory(output);var root=GetComponent<AppRoot>();string error=null;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(85));var ct=timeout.Token;
            try
            {
                await root.Initialization;CompanionScreen screen=null;
                while(screen==null||screen.WindowView==null||root.Controller.IsSwitching){ct.ThrowIfCancellationRequested();screen=FindFirstObjectByType<CompanionScreen>();await Task.Delay(30,ct);}
                await Task.Delay(300,ct);var view=screen.WindowView;var visual=FindFirstObjectByType<CompanionPresentation>();var camera=visual.Loader.renderCamera;var core=root.Character;float ambient=view.AmbientTime;
                var handle=System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                async Task Case(string tag)
                {
                    sizes.Add(new{tag,width=Screen.width,height=Screen.height});view.ReduceMotion=true;view.GoCharacter();view.ReduceMotion=false;await Task.Delay(120,ct);
                    Check(camera.rect==new Rect(0,0,1,1),tag+"_fullViewport");
                    var origin=camera.WorldToScreenPoint(camera.transform.position+Vector3.forward*100);
                    float x=(camera.WorldToScreenPoint(camera.transform.position+Vector3.forward*100+Vector3.right)-origin).magnitude;
                    float y=(camera.WorldToScreenPoint(camera.transform.position+Vector3.forward*100+Vector3.up)-origin).magnitude;
                    Check(Mathf.Abs(x-y)<.02f,tag+"_uniformScale");await Capture(tag+"_人物.png");
                    view.ReduceMotion=true;view.GoWindow();view.ReduceMotion=false;await Task.Delay(150,ct);
                    var space=root.VisualSettings.Value.FindSpace(visual.ActiveSpaceId);float angle=space.UiAngle*Mathf.Deg2Rad;
                    var local=new Vector2(80,25);var clock=new Vector2(space.ClockX,space.ClockY)+new Vector2(Mathf.Cos(angle)*local.x-Mathf.Sin(angle)*local.y,Mathf.Sin(angle)*local.x+Mathf.Cos(angle)*local.y);
                    var point=CompanionVisualFoundation.Project(clock,space,1);var rect=CompanionVisualFoundation.SceneViewport(Screen.width,Screen.height);
                    Check(view.HitClock(new Vector2(rect.x+point.x/1280*rect.width,rect.y+(720-point.y)/720*rect.height)),tag+"_clockHit");
                    var button=view.GetComponentsInChildren<Button>().Single(b=>b.name=="设置");var hitPoint=(Vector2)camera.WorldToScreenPoint(button.transform.TransformPoint(new Vector3(50,-8,0)));
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=hitPoint},hits);
                    Check(hits.Any(h=>h.gameObject==button.gameObject),tag+"_worldMenuHit");await Capture(tag+"_窗边.png");
                    button.onClick.Invoke();await Task.Delay(300,ct);Check(screen.ActivePanel=="Voice",tag+"_settingsOpen");await Capture(tag+"_设置.png");screen.Back();
                }
                var matrix=args.Contains("-v2-final-matrix");
                var targets=matrix?new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(960,540),new Vector2Int(1440,648),new Vector2Int(1100,800)}:new[]{new Vector2Int(1280,720),new Vector2Int(1440,750),new Vector2Int(1440,648),new Vector2Int(1100,800)};
                foreach(var size in targets)
                {ShowWindow(handle,9);await Task.Delay(200,ct);Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);await Task.Delay(400,ct);view.Focused=true;Check(Screen.width==size.x&&Screen.height==size.y,"actual_"+size.x+"x"+size.y+"_got_"+Screen.width+"x"+Screen.height);await Case(size.x+"x"+size.y);}
                if(matrix){Screen.SetResolution(2560,1440,FullScreenMode.FullScreenWindow);await Task.Delay(800,ct);Check(Screen.width==2560&&Screen.height==1440,"actual2560x1440Borderless");await Case("2560x1440无边框");Screen.SetResolution(1280,720,FullScreenMode.Windowed);await Task.Delay(600,ct);}
                ShowWindow(handle,3);await Task.Delay(600,ct);view.Focused=true;await Case("最大化");
                view.ReduceMotion=true;view.GoCharacter();view.ReduceMotion=false;view.GoWindow();view.Advance(.5f);float before=view.Progress;
                ShowWindow(handle,9);Screen.SetResolution(1280,720,FullScreenMode.Windowed);await Task.Delay(300,ct);view.Focused=true;
                Check(view.Progress>=before&&view.Progress<1,"resizeDuringTravelPreservesProgress");Check(view.AmbientTime>ambient,"ambientNeverResets");Check(root.Character==core,"sameCore");
                Check(root.Voice.OwnedProcessId==0,"noVoiceProcess");await root.ShutdownAsync();Check(root.ShutdownSucceeded,"savedShutdown");
            }
            catch(Exception e){error=e.ToString();}
            bool passed=error==null&&checks.Values.All(v=>v);AtomicDocument.WriteJson(Path.Combine(output,"native.json"),new{passed,error,checks,sizes,graphics=SystemInfo.graphicsDeviceName},default,false);
            if(root!=null)await root.ShutdownAsync();Application.Quit(passed?0:1);
        }
        Task Capture(string name){var done=new TaskCompletionSource<bool>();StartCoroutine(Frame(Path.Combine(output,name),done));return done.Task;}
        System.Collections.IEnumerator Frame(string path,TaskCompletionSource<bool> done)
        {
            yield return new WaitForEndOfFrame();Texture2D frame=null;
            try
            {
                frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);frame.Apply();
                var pixels=frame.GetPixels32();int w=frame.width,h=frame.height;int[] dark=new int[4];
                bool Black(Color32 c)=>c.r<3&&c.g<3&&c.b<3;
                for(int i=0;i<h;i++){if(Black(pixels[i*w]))dark[0]++;if(Black(pixels[i*w+w-1]))dark[1]++;}
                for(int i=0;i<w;i++){if(Black(pixels[i]))dark[2]++;if(Black(pixels[(h-1)*w+i]))dark[3]++;}
                Check(dark[0]<h*.98f&&dark[1]<h*.98f&&dark[2]<w*.98f&&dark[3]<w*.98f,Path.GetFileNameWithoutExtension(path)+"_noBlackStrips");
                File.WriteAllBytes(path,frame.EncodeToPNG());done.TrySetResult(true);
            }
            catch(Exception e){done.TrySetException(e);}finally{if(frame!=null)Destroy(frame);}
        }
#endif
    }
}
