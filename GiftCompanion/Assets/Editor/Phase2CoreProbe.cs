using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using GiftCompanion.Validation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Logger=GiftCompanion.Core.Logging.Logger;

namespace GiftCompanion.Editor
{
    public static class Phase2CoreProbe
    {
        [MenuItem("Gift Companion/Phase 2/Probe Shared Memory")]
        public static async void Run()
        {
            string reports=Path.GetFullPath("Docs/Validation/Phase2");Directory.CreateDirectory(reports);
            string data=Path.GetFullPath("UserData/Validation/phase2-real-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
            bool passed=false;string stage="initialize";
            try
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));
                using var logger=new Logger(Path.Combine(data,"Logs"));
                var config=new ConfigService(Path.GetFullPath("UserData"),File.ReadAllText("Assets/Characters/MainCharacter/Config/identity.json"),logger);
                await config.InitializeAsync(timeout.Token);
                string key=LocalSecrets.ReadDeepSeekKey(config.UserDataPath);
                using(var database=new DatabaseService(Path.Combine(data,"memory.db"),config.Identity.Id,logger))
                using(var llm=new LLMService(key,config.Configuration.Model,config.Configuration.NetworkTimeoutSeconds))
                {
                    await database.InitializeAsync(timeout.Token);await database.MigrateAsync(timeout.Token);await database.RecoverConversationsAsync(timeout.Token);
                    using var memory=new MemoryService(database,llm,logger,timeout.Token);await memory.InitializeAsync(timeout.Token);
                    var core=new CharacterCore(config.Identity,new CharacterState(),database,llm,memory,logger,timeout.Token);
                    stage="write";
                    var write=await Phase2AcceptanceRunner.WriteAsync(core,database,database,m=>core.SwitchModeAsync(m,timeout.Token),timeout.Token);
                    File.WriteAllText(Path.Combine(reports,"core_write.json"),JsonConvert.SerializeObject(write,Formatting.Indented));
                    if(!(bool)write["passed"])throw new InvalidOperationException("Write acceptance failed.");
                    await memory.StopAsync(timeout.Token);await database.EndSessionAndSaveStateAsync(core.State,"closed",timeout.Token);
                }
                stage="restore";await Restore(data,config,key,true,timeout.Token);
                stage="nokey";await Restore(data,config,null,false,timeout.Token);
                passed=true;
                File.WriteAllText(Path.Combine(reports,"core_acceptance.json"),JsonConvert.SerializeObject(new {passed,stage="complete",dataPath=data,
                    schema=DatabaseService.CurrentSchemaVersion,unity=Application.unityVersion,verifiedAtUtc=DateTimeOffset.UtcNow.ToString("O")},Formatting.Indented));
            }
            catch(Exception error)
            {
                File.WriteAllText(Path.Combine(reports,"core_acceptance.json"),JsonConvert.SerializeObject(new {passed=false,stage,errorType=error.GetType().Name,dataPath=data},Formatting.Indented));
            }
            if(Application.isBatchMode)EditorApplication.Exit(passed?0:1);
        }
        private static async Task Restore(string data,ConfigService config,string key,bool configured,CancellationToken ct)
        {
            using var db=new DatabaseService(Path.Combine(data,"memory.db"),config.Identity.Id);await db.InitializeAsync(ct);await db.MigrateAsync(ct);await db.RecoverConversationsAsync(ct);
            using var llm=new LLMService(key,config.Configuration.Model,config.Configuration.NetworkTimeoutSeconds);
            using var memory=new MemoryService(db,llm);await memory.InitializeAsync(ct);
            var core=new CharacterCore(config.Identity,await db.LoadCharacterStateAsync(ct),db,llm,memory);
            var result=await Phase2AcceptanceRunner.RestoreAsync(core,db,configured,m=>core.SwitchModeAsync(m,ct),ct);
            File.WriteAllText("Docs/Validation/Phase2/core_"+(configured?"restore":"nokey")+".json",JsonConvert.SerializeObject(result,Formatting.Indented));
            if(!(bool)result["passed"])throw new InvalidOperationException("Restore acceptance failed.");
            await memory.StopAsync(ct);await db.EndSessionAndSaveStateAsync(core.State,"closed",ct);
        }
    }
}
