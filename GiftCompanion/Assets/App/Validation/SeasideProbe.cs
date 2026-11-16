using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
#endif
namespace GiftCompanion.Validation {
 public sealed class SeasideProbe:MonoBehaviour {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
  readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();string output;
  void Check(bool yes,string name){checks[name]=yes;if(!yes)throw new Exception(name);}
  async void Start(){var args=Environment.GetCommandLineArgs();int marker=Array.IndexOf(args,"-seaside-probe");if(marker<0)return;
   output=args[marker+1];Directory.CreateDirectory(output);string error=null;var root=GetComponent<AppRoot>();
   try {await root.Initialization;CompanionScreen screen=null;for(int i=0;i<1200;i++){screen=FindFirstObjectByType<CompanionScreen>();if(screen?.IsReady==true&&!root.Controller.IsSwitching)break;await Task.Delay(50);}
    Check(screen?.IsReady==true,"ready");var view=FindFirstObjectByType<CompanionPresentation>();var core=root.Character;
    var sea=root.Library.Spaces.Spaces.Single(s=>s.Id=="mika-seaside");var original=root.Library.Spaces.Spaces.Single(s=>s.Id=="mika-memory");
    if(args.Contains("-seaside-resume")){
     Check(root.Library.Spaces.SelectedSpaceId==sea.Id&&view.ActiveSpaceId==sea.Id,"selectedSeaRestoredOnReopen");
     Check(view.Loader.Character.GetComponent<SeasideSpaceView>()!=null,"seaRuntimeRecreated");
     File.WriteAllText(Path.Combine(output,"native.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,checks}));Application.Quit(0);return;
    }
    Check(root.Library.Spaces.SelectedSpaceId=="mika-memory","originalDefaultPreserved");
    screen.OpenSpaces();await Task.Delay(100);
    await screen.SelectSpaceAsync(sea.Id);await Task.Delay(250);var runtime=view.Loader.Character.GetComponent<SeasideSpaceView>();
    Check(runtime!=null&&runtime.IsEntrancePlaying,"entranceStarted");await Capture("01_海边入场.png");
    Check(screen.ActivePanel==null&&!screen.IsMenuOpen&&screen.WindowView.AtCharacter&&!runtime.MenuVisible,"switchHidesMenuDuringEntrance");
    var clock=FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).First(t=>t.name=="SeasideClock");
    Check(clock.font==CompanionVisualFoundation.Font&&clock.transform.parent.GetComponent<UnityEngine.Canvas>()!=null,"seaClockTransparentLolita");
    clock.ForceMeshUpdate();Check(clock.textInfo.characterInfo.Any(c=>c.isVisible),"seaClockDigitsActuallyVisible");
    var corners=new Vector3[4];clock.rectTransform.GetWorldCorners(corners);
    File.WriteAllText(Path.Combine(output,"clock.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{text=clock.text,active=clock.gameObject.activeInHierarchy,corners=corners.Select(c=>c.ToString()).ToArray(),color=clock.color.ToString(),alpha=clock.canvasRenderer.GetInheritedAlpha()}));
    await Task.Delay(14000);Check(!runtime.IsEntrancePlaying&&!screen.IsMenuOpen&&screen.ActivePanel==null,"entranceEndsAtCharacterWithoutMenu");await Capture("06_海边透明时钟.png");
    screen.OpenMenu();await Task.Delay(200);Check(screen.IsMenuOpen&&screen.WindowView.AtWindow,"instantMenu");Check(runtime.MenuVisible,"glassVisible");
    await Task.Delay(14000);Check(!runtime.IsEntrancePlaying,"entranceCompletesBehindMenu");
    Check(runtime.MappingFailures==0,"backgroundMeshMapping");
    float trackTime=view.Loader.Character.AnimationState.GetCurrent(0).TrackTime;await Task.Delay(600);Check(view.Loader.Character.AnimationState.GetCurrent(0).TrackTime>trackTime,"idleKeepsRunningBehindMenu");
    await Capture("02_海边菜单.png");
    screen.Back();await Task.Delay(200);Check(!screen.IsMenuOpen&&screen.WindowView.AtCharacter,"escReturnsCharacter");
    Check(runtime.BeginPat(),"patBegins");runtime.EndPat();await Task.Delay(1000);Check(!runtime.IsPatting,"patEnds");
    await Capture("03_海边待机.png");
    var driver=view.Loader.Speech;var audio=AudioClip.Create("isolated voice envelope",24000,1,24000,false);var pcm=new float[24000];for(int i=0;i<pcm.Length;i++)pcm[i]=Mathf.Sin(i*.04f)*.2f;audio.SetData(pcm,0);
    driver.Play(audio);await Task.Delay(300);Check(driver.voice.isPlaying&&driver.OpenFrames>0,"realAudioEnvelope");Check(runtime.MouthReady,"mouthAttachmentResolved_"+runtime.MouthSlot);driver.Stop();
    var live=screen.gameObject.AddComponent<CompanionSpeechDriver>();live.voice=screen.gameObject.AddComponent<AudioSource>();live.MouthChanged=driver.MouthChanged;live.Play(audio);await Task.Delay(300);
    Check(runtime.MouthIsOpen,"segmentPlayerMouthBridge");live.Pause();await Task.Delay(100);Check(!runtime.MouthIsOpen,"pausedVoiceClosesMouth");live.Stop();Destroy(live.voice);Destroy(live);Destroy(audio);
    screen.OpenSpaces();await Task.Delay(350);Check(screen.ActivePanel=="Spaces","spacePage");await Capture("04_空间选择.png");screen.Back();screen.Back();
    foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(1920,1080),new Vector2Int(1440,648)}){
     Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);await Task.Delay(700);
     Check(view.Loader.renderCamera.rect==new Rect(0,0,1,1),"fullViewport"+size);await Capture("尺寸_"+size.x+"x"+size.y+".png");}
    for(int i=0;i<10;i++){await screen.SelectSpaceAsync(original.Id);await Task.Delay(100);await screen.SelectSpaceAsync(sea.Id);await Task.Delay(100);Check(ReferenceEquals(root.Character,core),"sameCore"+i);}
    Check(FindObjectsByType<SeasideSpaceView>(FindObjectsSortMode.None).Length==1,"singleSeaInstance");
    Check(view.Loader.Character.GetComponent<SeasideSpaceView>().MappingFailures==0,"mappingAfterSwitches");
    screen.OpenSpaces();await Task.Delay(100);await screen.SelectSpaceAsync(original.Id);await Task.Delay(200);
    Check(screen.WindowView.HasWindow,"originalWindowRestored");
    Check(view.Loader.IsPlayingIntro&&!view.Loader.Motion.enabled&&!view.Loader.Expression.enabled,"loftNativeIntroOwnsSkeleton");
    Check(!screen.IsMenuOpen&&screen.ActivePanel==null&&screen.WindowView.AtCharacter,"loftSwitchStartsWithoutMenu");
    await Capture("07_阁楼入场_起点.png");await Task.Delay(4800);await Capture("08_阁楼入场_5秒.png");
    await Task.Delay(5000);await Capture("09_阁楼入场_10秒.png");await Task.Delay(11000);
    Check(!view.Loader.IsPlayingIntro&&view.Loader.Motion.enabled&&view.Loader.Expression.enabled,"loftIntroCompletesAndRestoresInteraction");
    Check(!screen.IsMenuOpen&&screen.ActivePanel==null&&screen.WindowView.AtCharacter,"loftIntroEndsAtCharacter");await Capture("10_阁楼入场结束.png");screen.WindowView.ReduceMotion=true;screen.OpenMenu();await Task.Delay(300);await Capture("05_原大厅回归.png");
    float ambient=screen.WindowView.AmbientTime;await Task.Delay(300);Check(screen.WindowView.AmbientTime>ambient,"originalAmbientContinues");
    await screen.SelectSpaceAsync(sea.Id);await Task.Delay(250);Check(root.Library.Spaces.SelectedSpaceId==sea.Id,"selectionSaved");
   }catch(Exception e){error=e.ToString();}
   File.WriteAllText(Path.Combine(output,"native.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=error==null,checks,error},Newtonsoft.Json.Formatting.Indented));Application.Quit(error==null?0:1);
  }
  Task Capture(string name){var t=new TaskCompletionSource<bool>();StartCoroutine(Save(name,t));return t.Task;}
  IEnumerator Save(string name,TaskCompletionSource<bool> done){yield return new WaitForEndOfFrame();Texture2D t=null;try{t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();var pixels=t.GetPixels32();int w=t.width,h=t.height;int[] dark=new int[4];bool Black(Color32 c)=>c.r<24&&c.g<24&&c.b<24;
    for(int i=0;i<h;i++){if(Black(pixels[i*w]))dark[0]++;if(Black(pixels[i*w+w-1]))dark[1]++;}
    for(int i=0;i<w;i++){if(Black(pixels[i]))dark[2]++;if(Black(pixels[(h-1)*w+i]))dark[3]++;}
    Check(dark[0]<h*.98f&&dark[1]<h*.98f&&dark[2]<w*.98f&&dark[3]<w*.98f,name+"_noBlackStrips");
    File.WriteAllBytes(Path.Combine(output,name),t.EncodeToPNG());done.SetResult(true);}catch(Exception e){done.SetException(e);}finally{if(t!=null)Destroy(t);}}
#endif
 }
}
