using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class MemoryExtractionTests
    {
        private string directory;
        private DatabaseService db;
        [SetUp] public async Task Setup()
        {
            directory=Path.GetFullPath("UserData/Validation/extraction-"+Guid.NewGuid().ToString("N"));
            db=new DatabaseService(Path.Combine(directory,"memory.db"),"test");
            await db.InitializeAsync(default);await db.MigrateAsync(default);
        }
        private async Task<ConversationTurn> Turn(string text)
        {
            var turn=await db.BeginTurnAsync(Guid.NewGuid().ToString("N"),text,InteractionSource.Companion,default);
            await db.CompleteTurnAsync(turn,"完整回答",new CharacterState {LastInteractionAt=DateTimeOffset.UtcNow},default);return turn;
        }
        private static JObject Empty()=>new JObject { ["memories"]=new JArray(),["facts"]=new JArray(),["open_loops"]=new JArray(),["resolved_loops"]=new JArray() };
        private static JObject Fact(ConversationTurn t,string key,string value)=>new JObject { ["key"]=key,["value"]=value,
            ["confidence"]=.95,["source_message_id"]=t.UserMessageId,["evidence"]=t.UserText };
        private async Task ExtractFact(ConversationTurn turn,string key,string value)
        {
            var root=Empty();((JArray)root["facts"]).Add(Fact(turn,key,value));
            var job=await db.ClaimExtractionAsync(default);
            Assert.That(job.Turn.RequestId,Is.EqualTo(turn.RequestId));
            var extraction=MemoryExtractor.Parse(root.ToString(),job.Turn,Array.Empty<MemoryRecord>());
            Assert.That(await db.ApplyExtractionAsync(job,extraction,default),Is.True);
            Assert.That(await db.ApplyExtractionAsync(job,extraction,default),Is.False);
        }
        [Test] public async Task ExtractorRejectsUnsupportedEvidenceAssistantSourceAndUnknownLoopIds()
        {
            var turn=await Turn("我喜欢茉莉花茶");var root=Empty();
            var fact=Fact(turn,"饮品偏好","意式咖啡");((JArray)root["facts"]).Add(fact);
            Assert.Throws<InvalidDataException>(()=>MemoryExtractor.Parse(root.ToString(),turn,Array.Empty<MemoryRecord>()));
            fact["value"]="茉莉花茶";fact["source_message_id"]="assistant-id";
            Assert.Throws<InvalidDataException>(()=>MemoryExtractor.Parse(root.ToString(),turn,Array.Empty<MemoryRecord>()));
            fact["source_message_id"]=turn.UserMessageId;fact["confidence"]=1.1;
            Assert.Throws<InvalidDataException>(()=>MemoryExtractor.Parse(root.ToString(),turn,Array.Empty<MemoryRecord>()));
            root=Empty();((JArray)root["resolved_loops"]).Add(new JObject { ["id"]="unknown",["source_message_id"]=turn.UserMessageId,["evidence"]=turn.UserText });
            Assert.Throws<InvalidDataException>(()=>MemoryExtractor.Parse(root.ToString(),turn,Array.Empty<MemoryRecord>()));
            Assert.That(await db.LoadMemoriesAsync("fact",true,20,default),Is.Empty);
        }
        [Test] public async Task DuplicateFactsAreLinkedAndCorrectionsRetainHistoryWithOneActiveValue()
        {
            await ExtractFact(await Turn("我喜欢茉莉花茶"),"饮品偏好","茉莉花茶");
            await ExtractFact(await Turn("我喜欢茉莉花茶"),"饮品偏好","茉莉花茶");
            Assert.That((await db.LoadMemoriesAsync("fact",false,20,default)).Count,Is.EqualTo(1));
            await ExtractFact(await Turn("现在改为喜欢桂花乌龙茶"),"饮品偏好","桂花乌龙茶");
            var all=await db.LoadMemoriesAsync("fact",false,20,default);
            Assert.That(all.Count,Is.EqualTo(2));Assert.That(all.Count(x=>x.Status=="active"),Is.EqualTo(1));
            Assert.That(all.Single(x=>x.Status=="active").Content,Is.EqualTo("桂花乌龙茶"));
            Assert.That(all.Single(x=>x.Status=="superseded").Content,Is.EqualTo("茉莉花茶"));
        }
        [Test] public async Task OpenLoopRequiresAnExistingCandidateToResolve()
        {
            var turn=await Turn("明天整理书架");var root=Empty();
            ((JArray)root["open_loops"]).Add(new JObject { ["content"]=turn.UserText,["type"]="open_loop",["importance"]=.8,
                ["confidence"]=.95,["source_message_id"]=turn.UserMessageId,["evidence"]=turn.UserText });
            var job=await db.ClaimExtractionAsync(default);
            await db.ApplyExtractionAsync(job,MemoryExtractor.Parse(root.ToString(),job.Turn,Array.Empty<MemoryRecord>()),default);
            var loops=await db.LoadMemoriesAsync("loop",true,15,default);Assert.That(loops.Count,Is.EqualTo(1));
            turn=await Turn("书架已经整理完成");root=Empty();
            ((JArray)root["resolved_loops"]).Add(new JObject { ["id"]=loops[0].Id,["source_message_id"]=turn.UserMessageId,["evidence"]=turn.UserText });
            job=await db.ClaimExtractionAsync(default);
            await db.ApplyExtractionAsync(job,MemoryExtractor.Parse(root.ToString(),job.Turn,loops),default);
            Assert.That(await db.LoadMemoriesAsync("loop",true,15,default),Is.Empty);
            Assert.That((await db.LoadMemoriesAsync("loop",false,15,default)).Single().Status,Is.EqualTo("resolved"));
        }
        [Test]public async Task CaseSensitiveFactValueChangeCreatesANewActiveVersion()
        {
            await ExtractFact(await Turn("我的识别码是Moon7"),"识别码","Moon7");
            await ExtractFact(await Turn("识别码更正为MOON7"),"识别码","MOON7");
            var all=await db.LoadMemoriesAsync("fact",false,20,default);
            Assert.That(all.Count,Is.EqualTo(2));
            Assert.That(all.Single(x=>x.Status=="active").Content,Is.EqualTo("MOON7"));
            Assert.That(all.Single(x=>x.Status=="superseded").Content,Is.EqualTo("Moon7"));
        }
        [Test] public async Task BackgroundFailureIsRetryableAndForegroundCancelsOnlyTheBackgroundJob()
        {
            var first=await Turn("我喜欢茶");var fake=new BackgroundLLM { Block=true };
            using var memory=new MemoryService(db,fake);
            await memory.InitializeAsync(default);
            await fake.Started.Task;
            using(memory.EnterForeground())
            {
                await fake.Canceled.Task;
                await WaitFor(async()=> (await db.LoadTurnsAsync(10,true,default))[0].ExtractionStatus=="pending");
                Assert.That(await db.LoadMemoriesAsync("fact",true,20,default),Is.Empty);
                fake.Block=false;fake.Invalid=true;
            }
            await WaitFor(async()=> (await db.LoadTurnsAsync(10,true,default))[0].ExtractionStatus=="failed");
            fake.Invalid=false;
            await memory.RetryFailedAsync(default);
            await WaitFor(async()=> (await db.LoadTurnsAsync(10,true,default))[0].ExtractionStatus=="succeeded");
            Assert.That((await db.LoadTurnsAsync(10,true,default))[0].UserText,Is.EqualTo(first.UserText));
            await memory.StopAsync(default);
        }
        [Test] public async Task MaintenanceLeaseWaitsForCanceledBackgroundCleanupBeforeReturning()
        {
            await Turn("维护快照测试");
            var fake = new BackgroundLLM { Block = true, CleanupGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            using var memory = new MemoryService(db, fake);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await memory.InitializeAsync(timeout.Token); await fake.Started.Task;
            try
            {
                var paused = memory.PauseAndDrainAsync(timeout.Token); await fake.Canceled.Task;
                Assert.That(paused.IsCompleted, Is.False, "Cancellation alone must not grant a consistent snapshot lease.");
                fake.CleanupGate.TrySetResult(true);
                using (await paused)
                {
                    Assert.That((await db.LoadTurnsAsync(10, true, timeout.Token))[0].ExtractionStatus, Is.EqualTo("pending"));
                    fake.Block = false;
                }
                await memory.StopAsync(timeout.Token);
            }
            finally { fake.CleanupGate.TrySetResult(true); }
        }
        private sealed class BackgroundLLM : ILLMService, IMemoryLLMService
        {
            public bool IsConfigured=>true;
            public bool Block,Invalid;
            public TaskCompletionSource<bool> CleanupGate;
            public readonly TaskCompletionSource<bool> Started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource<bool> Canceled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public async Task<string> CompleteJsonAsync(string instruction,string input,int maxTokens,CancellationToken ct)
            {
                Started.TrySetResult(true);
                if(Block) {try {await Task.Delay(Timeout.Infinite,ct);}catch(OperationCanceledException){Canceled.TrySetResult(true); if (CleanupGate != null) await CleanupGate.Task.ConfigureAwait(false); throw;} }
                return Invalid?"invalid":Empty().ToString();
            }
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct)
            {await Task.Yield();yield return new LLMChunk("reply");}
        }
        private static async Task WaitFor(Func<Task<bool>> condition)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while(!await condition()) await Task.Delay(10,timeout.Token);
        }
        [TearDown] public void Cleanup()
        {
            db?.Dispose();
            if(!directory.StartsWith(Path.GetFullPath("UserData/Validation")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
