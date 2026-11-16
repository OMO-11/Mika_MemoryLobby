using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.Validation
{
    public sealed class Phase2PlayerProbe:MonoBehaviour
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private async void Start()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-phase2-probe");
            if(index<0){enabled=false;return;}
            if(index+2>=args.Length){Application.Quit(2);return;}
            string reports=Path.GetFullPath(args[index+1]),mode=args[index+2];Directory.CreateDirectory(reports);
            bool passed=false;var elapsed=Stopwatch.StartNew();
            var result=new Dictionary<string,object>();
            try
            {
                var root=AppRoot.Instance;using var timeout=CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(5));await root.Initialization;
                while(root.Controller.IsSwitching||SceneManager.GetActiveScene().name!="Home")
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    if(elapsed.Elapsed.TotalSeconds>30)throw new TimeoutException();
                    await System.Threading.Tasks.Task.Yield();
                }
                var database=(IMemoryStore)root.Database;
                if(mode=="Write")result=await Phase2AcceptanceRunner.WriteAsync(root.Character,database,(IConversationStore)root.Database,
                    m=>root.Controller.SwitchModeAsync(m,timeout.Token),timeout.Token);
                else if(mode=="Restore"||mode=="NoKey")result=await Phase2AcceptanceRunner.RestoreAsync(root.Character,database,mode=="Restore",
                    m=>root.Controller.SwitchModeAsync(m,timeout.Token),timeout.Token);
                else throw new ArgumentException("Unknown probe mode.");
                result["schemaCurrent"]=root.Database.SchemaVersion==DatabaseService.CurrentSchemaVersion;
                result["singleRoot"]=FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length==1;
                await root.ShutdownAsync();result["shutdownSaved"]=root.ShutdownSucceeded;
                passed=(bool)result["passed"]&&(bool)result["schemaCurrent"]&&(bool)result["singleRoot"]&&root.ShutdownSucceeded;
            }
            catch(Exception error){result["errorType"]=error.GetType().Name;}
            result["passed"]=passed;result["elapsedMs"]=elapsed.ElapsedMilliseconds;
            File.WriteAllText(Path.Combine(reports,"player_"+mode.ToLowerInvariant()+".json"),JsonConvert.SerializeObject(result,Formatting.Indented));
            Application.Quit(passed?0:1);
        }
#endif
    }
}
