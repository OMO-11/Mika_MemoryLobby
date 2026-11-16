using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
#endif
namespace GiftCompanion.Validation
{
    public sealed class V2FinalProbe:MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        readonly List<float> frames=new List<float>();bool sampling;
        void Update(){if(sampling)frames.Add(Time.unscaledDeltaTime);}
        async void Start()
        {
            var args=Environment.GetCommandLineArgs();int marker=Array.IndexOf(args,"-v2-final-probe");if(marker<0||marker+2>=args.Length)return;
            string output=Path.GetFullPath(args[marker+1]),mode=args[marker+2],error=null;Directory.CreateDirectory(output);var root=GetComponent<AppRoot>();var flags=new Dictionary<string,bool>();var metrics=new List<object>();
            void Check(bool yes,string label){flags[label]=yes;if(!yes)throw new Exception(label);}
            using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(4));var ct=deadline.Token;
            try
            {
                await root.Initialization;CompanionScreen screen=null;
                while(screen==null||screen.WindowView==null||root.Controller.IsSwitching){ct.ThrowIfCancellationRequested();screen=FindFirstObjectByType<CompanionScreen>();await Task.Delay(25,ct);}
                await Task.Delay(300,ct);Check(root.Character.CurrentMode==CharacterMode.Companion,"companionDefault");Check(!root.EntrySettings.PetEntryEnabled,"petDisabled");
                if(mode=="Prepare")
                {
                    var cfg=root.Config.Configuration.Copy();cfg.VoiceEnabled=false;cfg.SubtitleFontSize=36;await root.Config.SaveAsync(cfg,ct);
                    var display=root.Display.Value.Copy();display.DiaryFontSize=30;display.DiaryLineSpacing=20;root.Display.Save(display,ct);
                    await root.SaveCharacterCardAsync(new CharacterIdentity(root.Character.Identity.Id,"隔离收尾验收",root.Character.Identity.Persona,root.Character.Identity.Relationship),ct);
                    var store=(IConversationStore)root.Database;var turn=await store.BeginTurnAsync("v2-final-persist","隔离验证：重启保存检查。",InteractionSource.Companion,ct);
                    await store.CompleteTurnAsync(turn,"隔离验证：保存后重新打开，仍可查看。",root.Character.State,ct);await ((IMemoryStore)root.Database).SetExtractionStatusAsync(turn.RequestId,"skipped",ct);
                    string secretRoot=Path.Combine(root.Config.UserDataPath,"CredentialCheck");LocalSecrets.SaveDeepSeekKey(secretRoot,"isolated-not-a-real-api-key",ct);
                    Check(LocalSecrets.ReadDeepSeekKey(secretRoot,false)=="isolated-not-a-real-api-key","dpapiSaveAndRead");
                    Check(!File.ReadAllText(Path.Combine(secretRoot,"Secrets/deepseek.key.dpapi")).Contains("isolated-not-a-real-api-key"),"keyCiphertextOnly");
                }
                else if(mode=="Persist")
                {
                    Check(root.Config.Configuration.SubtitleFontSize==36&&root.Display.Value.DiaryFontSize==30,"maxFontsPersisted");
                    Check(root.Character.Identity.DisplayName=="隔离收尾验收","identityPersisted");
                    Check((await root.History.ReadPageAsync(null,ct)).Turns.Count==1,"historyPersisted");
                    Check(LocalSecrets.ReadDeepSeekKey(Path.Combine(root.Config.UserDataPath,"CredentialCheck"),false)=="isolated-not-a-real-api-key","dpapiAcrossProcessRestart");Check(!root.LLM.IsConfigured,"noRealApiKeyLoaded");
                }
                else if(mode=="Performance")
                {
                    var core=root.Character;var view=screen.WindowView;var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();long initial=process.PrivateMemorySize64;
                    for(int i=0;i<6;i++){screen.OpenMenu();view.Focused=true;await Task.Delay(3300,ct);screen.Back();view.Focused=true;await Task.Delay(2500,ct);}Check(root.Character==core,"sixRoundTripsSameCore");
                    for(int phase=0;phase<3;phase++)
                    {
                        if(phase==1){screen.OpenMenu();view.Focused=true;await Task.Delay(3400,ct);}if(phase==2){screen.OpenSettings();await Task.Delay(400,ct);}
                        float ambient=view.AmbientTime;frames.Clear();sampling=true;await Task.Delay(20000,ct);sampling=false;
                        var values=frames.OrderBy(v=>v).ToArray();Check(values.Length>100,"sampleCount"+phase);Check(view.AmbientTime>ambient+18,"ambientContinues"+phase);
                        metrics.Add(new{phase=phase==0?"character":phase==1?"window":"settings",seconds=values.Sum(),frames=values.Length,meanFps=values.Length/values.Sum(),p95Milliseconds=values[(int)(values.Length*.95)]*1000,p99Milliseconds=values[(int)(values.Length*.99)]*1000});
                    }
                    process.Refresh();metrics.Add(new{privateStartMiB=initial/1048576d,privateEndMiB=process.PrivateMemorySize64/1048576d,screenWidth=Screen.width,screenHeight=Screen.height,vsync=QualitySettings.vSyncCount,targetFrameRate=Application.targetFrameRate});
                }
                else throw new Exception("Unknown final probe mode.");
                Check(root.Voice.OwnedProcessId==0,"noVoiceProcess");await root.ShutdownAsync();Check(root.ShutdownSucceeded,"savedShutdown");
            }
            catch(Exception e){error=e.ToString();}
            sampling=false;bool passed=error==null&&flags.Values.All(v=>v);AtomicDocument.WriteJson(Path.Combine(output,mode+".json"),new{passed,error,flags,metrics,graphics=SystemInfo.graphicsDeviceName,unity=Application.unityVersion},default,false);
            if(root!=null)await root.ShutdownAsync();Application.Quit(passed?0:1);
        }
#endif
    }
}
