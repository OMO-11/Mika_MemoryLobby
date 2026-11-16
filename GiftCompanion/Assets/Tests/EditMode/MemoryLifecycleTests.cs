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
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class MemoryLifecycleTests
    {
        private string directory,path;
        private DatabaseService db;
        [SetUp] public async Task Setup()
        {
            directory=Path.GetFullPath("UserData/Validation/lifecycle-"+Guid.NewGuid().ToString("N"));path=Path.Combine(directory,"memory.db");
            await Open();
        }
        private async Task Open(){db=new DatabaseService(path,"test");await db.InitializeAsync(default);await db.MigrateAsync(default);}
        private async Task<ConversationTurn> Turn(string text,string reply="完整回复",IReadOnlyList<string> sources=null)
        {
            var turn=await db.BeginTurnAsync(Guid.NewGuid().ToString("N"),text,InteractionSource.Companion,default);
            await db.CompleteTurnAsync(turn,reply,new CharacterState(),default,sources);return turn;
        }
        private MemoryRecord Item(ConversationTurn turn,string value,string key="测试事实")=>new MemoryRecord {
            Kind="fact",Key=key,Content=value,Evidence=turn.UserText,SourceMessageId=turn.UserMessageId,Confidence=.95,Importance=.7
        };
        private async Task Extract(ConversationTurn turn,params MemoryRecord[] items)
        {
            var job=await db.ClaimExtractionAsync(default);Assert.That(job.Turn.RequestId,Is.EqualTo(turn.RequestId));
            var extracted=new MemoryExtraction();extracted.Items.AddRange(items);await db.ApplyExtractionAsync(job,extracted,default);
        }
        [Test] public async Task SummaryThresholdAndExitRetainRawTextAndProvenanceAcrossReopen()
        {
            for(int i=0;i<9;i++)await Turn("普通事件 "+i);
            Assert.That(await db.ClaimSummaryAsync(default),Is.Null);
            await Turn("第十轮发生了明确变化");var job=await db.ClaimSummaryAsync(default);
            Assert.That(job.Turns.Count,Is.EqualTo(10));Assert.That(job.SourceIds.Count,Is.EqualTo(20));
            Assert.That(await db.ApplySummaryAsync(job,"本会话进行了十轮交流。",default),Is.True);
            Assert.That((await db.RetrieveContextAsync("交流",default)).Summary,Does.Contain("十轮"));
            await Turn("追加一轮");await db.EndSessionAndSaveStateAsync(new CharacterState(),"closed",default);
            job=await db.ClaimSummaryAsync(default);Assert.That(job.PreviousSummary,Does.Contain("十轮"));
            Assert.That(job.Turns.Count,Is.EqualTo(1));
            Assert.That(await db.ApplySummaryAsync(job,"本会话进行了十一轮交流。",default),Is.True);
            db.Dispose();await Open();
            Assert.That((await db.LoadTurnsAsync(20,true,default)).Count,Is.EqualTo(11));
            using var sql=new SqliteConnection(path);using var q=sql.Prepare("SELECT summary,summary_through,summary_sources FROM sessions;");q.Row();
            Assert.That(q.Text(0),Does.Contain("十一轮"));Assert.That(q.Integer(1),Is.GreaterThan(0));Assert.That(q.Text(2),Does.Contain(job.SourceIds[0]));
        }
        [Test] public async Task ForgetExcludesDependentRepliesSummariesAndOldTasksButKeepsIndependentFactAndRawHistory()
        {
            var first=await Turn("饮品是茉莉花茶；城市是京都","记住茉莉花茶和京都");
            await Extract(first,Item(first,"茉莉花茶","饮品"),Item(first,"京都","城市"));
            var facts=await db.LoadMemoriesAsync("fact",true,20,default);
            var context=await db.RetrieveContextAsync("饮品",default);
            var reply=await Turn("我的饮品是什么？","茉莉花茶",new[]{first.UserMessageId});
            var stale=await db.ClaimExtractionAsync(default);
            await db.EndSessionAndSaveStateAsync(new CharacterState(),"closed",default);
            var summary=await db.ClaimSummaryAsync(default);await db.ApplySummaryAsync(summary,"饮品是茉莉花茶，城市是京都。",default);
            await db.ForgetRecordAsync("fact",facts.Single(x=>x.Key=="饮品").Id,default);
            Assert.That(await db.ApplyExtractionAsync(stale,new MemoryExtraction(),default),Is.False);
            var after=await db.RetrieveContextAsync("茉莉花茶",default);
            Assert.That(after.Facts.Count,Is.EqualTo(1));Assert.That(after.Facts[0].Content,Is.EqualTo("京都"));
            Assert.That(after.Recent,Is.Empty);Assert.That(after.Summary,Is.Empty);
            Assert.That((await db.LoadTurnsAsync(20,true,default)).Count,Is.EqualTo(2));
            Assert.That((await db.LoadTurnsAsync(20,true,default))[1].ExtractionStatus,Is.EqualTo("skipped"));
            using var sql=new SqliteConnection(path);using var q=sql.Prepare("SELECT count(*) FROM memory_search WHERE body LIKE '%茉莉花茶%';");q.Row();Assert.That(q.Integer(0),Is.Zero);
        }
        [Test] public async Task ForgetPendingTurnAndRestartCannotReviveItButANewStatementCanBeRemembered()
        {
            var old=await Turn("代号是月光七号");var oldJob=await db.ClaimExtractionAsync(default);
            await db.ForgetTurnAsync(old.RequestId,default);
            var data=new MemoryExtraction();data.Items.Add(Item(old,"月光七号"));
            Assert.That(await db.ApplyExtractionAsync(oldJob,data,default),Is.False);
            db.Dispose();await Open();await db.RecoverMemoryJobsAsync(default);await db.RetryFailedExtractionsAsync(default);
            Assert.That(await db.ClaimExtractionAsync(default),Is.Null);
            var fresh=await Turn("请重新记住代号是月光七号");await Extract(fresh,Item(fresh,"月光七号"));
            Assert.That((await db.LoadMemoriesAsync("fact",true,20,default)).Single().Content,Is.EqualTo("月光七号"));
        }
        [Test] public async Task CorrectionInvalidatesOldAssistantRepetitionAndInFlightSummary()
        {
            var old=await Turn("饮品是茉莉花茶","茉莉花茶");await Extract(old,Item(old,"茉莉花茶","饮品"));
            var repeat=await Turn("记得吗","记得茉莉花茶",new[]{old.UserMessageId});await Extract(repeat);
            await db.EndSessionAndSaveStateAsync(new CharacterState(),"closed",default);var stale=await db.ClaimSummaryAsync(default);
            var next=await Turn("改喝桂花乌龙茶");await Extract(next,Item(next,"桂花乌龙茶","饮品"));
            Assert.That(await db.ApplySummaryAsync(stale,"还喜欢茉莉花茶",default),Is.False);
            var context=await db.RetrieveContextAsync("饮品",default);
            Assert.That(context.Facts.Single().Content,Is.EqualTo("桂花乌龙茶"));
            Assert.That(context.Recent.All(t=>!t.AssistantText.Contains("茉莉花茶")),Is.True);
        }
        [Test] public async Task ClosingModeDoesNotWaitForSummaryAndCanceledSummaryRecoversAsPending()
        {
            var fake=new SummaryLLM();using(var memory=new MemoryService(db,fake))
            {
                await memory.InitializeAsync(default);
                var core=new CharacterCore(new CharacterIdentity("test","Test","",""),new CharacterState(),db,fake,memory);
                await core.SwitchModeAsync(CharacterMode.Companion);
                await core.SendMessageAsync("今天读了一本书",InteractionSource.Companion,default);
                await core.SwitchModeAsync(CharacterMode.Home);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Task.WhenAny(fake.Started.Task,Task.Delay(Timeout.Infinite,timeout.Token));
                Assert.That(fake.Started.Task.IsCompleted,Is.True);Assert.That(core.CurrentMode,Is.EqualTo(CharacterMode.Home));
                await memory.StopAsync(timeout.Token);
            }
            db.Dispose();await Open();await db.RecoverMemoryJobsAsync(default);
            Assert.That(await db.ClaimSummaryAsync(default),Is.Not.Null);
        }
        [Test] public void SummaryRejectsExtraFieldsOversizedTextAndMalformedJson()
        {
            Assert.That(SessionSummarizer.Parse("{\"summary\":\"明确事件\"}"),Is.EqualTo("明确事件"));
            Assert.Throws<InvalidDataException>(()=>SessionSummarizer.Parse("{\"summary\":\"a\",\"extra\":1}"));
            Assert.Throws<InvalidDataException>(()=>SessionSummarizer.Parse("{\"summary\":\""+new string('长',1001)+"\"}"));
        }
        [Test]public async Task CoreForgetCancelsForegroundBeforeMutationAndAllowsNextRequest()
        {
            var original=await Turn("饮品是茉莉花茶");await Extract(original,Item(original,"茉莉花茶","饮品"));
            var target=(await db.LoadMemoriesAsync("fact",true,20,default)).Single();
            var fake=new BlockedDialogue();using var memory=new MemoryService(db,fake);await memory.InitializeAsync(default);
            var core=new CharacterCore(new CharacterIdentity("test","Test","",""),new CharacterState(),db,fake,memory);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var pending=core.SendMessageAsync("正在等待的请求",InteractionSource.Companion,timeout.Token);
            await fake.Started.Task;await core.ForgetMemoryAsync("fact",target.Id,timeout.Token);
            Assert.That((await pending).ErrorCode,Is.EqualTo("Canceled"));
            Assert.That(await db.LoadMemoriesAsync("fact",true,20,default),Is.Empty);
            fake.Block=false;
            Assert.That((await core.SendMessageAsync("下一轮",InteractionSource.Companion,timeout.Token)).Success,Is.True);
            await core.PauseAndCancelAsync(timeout.Token);await memory.StopAsync(timeout.Token);
        }
        [Test]public async Task V4UpgradeRetainsFactsAndRawMessagesAndCreatesNativeV4Backup()
        {
            var original=await Turn("代号是月光七号");await Extract(original,Item(original,"月光七号"));
            db.Dispose();
            using(var old=new SqliteConnection(path))old.Execute(@"ALTER TABLE sessions DROP COLUMN summary_sources;
                ALTER TABLE sessions DROP COLUMN summary_memory_revision;DROP INDEX memory_sources_message;DROP INDEX prompt_dependencies_source;
                DROP TABLE agent_tool_runs;DROP TABLE prompt_record_dependencies;DROP TABLE audio_records;DROP TABLE journal_sources;DROP TABLE relationship_journals;DROP TABLE assistant_presentations;DROP INDEX history_cursor;PRAGMA user_version=4;");
            await Open();
            Assert.That((await db.LoadTurnsAsync(20,true,default)).Single().UserText,Is.EqualTo(original.UserText));
            Assert.That((await db.RetrieveContextAsync("月光七号",default)).Facts.Single().Content,Is.EqualTo("月光七号"));
            string backup=Directory.GetFiles(directory,"*.pre-v"+DatabaseService.CurrentSchemaVersion+"-*.bak").Single();using var previous=new SqliteConnection(backup);
            Assert.That(previous.Version,Is.EqualTo(4));
        }
        private sealed class BlockedDialogue:ILLMService
        {
            public bool IsConfigured=>true;public bool Block=true;
            public readonly TaskCompletionSource<bool> Started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct)
            {Started.TrySetResult(true);if(Block)await Task.Delay(Timeout.Infinite,ct);yield return new LLMChunk("完成");}
        }
        [Test]public async Task IndependentFactReplySurvivesRepeatedExclusionPropagationAndCanLaterBeForgotten()
        {
            var original=await Turn("饮品茉莉花茶；城市京都");await Extract(original,Item(original,"茉莉花茶","饮品"),Item(original,"京都","城市"));
            var facts=await db.LoadMemoriesAsync("fact",true,20,default);
            await db.ForgetRecordAsync("fact",facts.Single(x=>x.Key=="饮品").Id,default);
            var context=await db.RetrieveContextAsync("城市",default);
            var prepared=PromptBuilder.Build(new CharacterIdentity("test","Test","",""),"城市是什么？",InteractionSource.Companion,context,DateTimeOffset.Now);
            var fresh=await db.BeginTurnAsync("independent-reply","城市是什么？",InteractionSource.Companion,default);
            await db.CompleteTurnAsync(fresh,"京都",new CharacterState(),default,prepared.SourceMessageIds,prepared.Revision,prepared.MemoryReferences);
            await db.RecoverMemoryJobsAsync(default);
            Assert.That((await db.RetrieveContextAsync("城市",default)).Recent.Any(x=>x.RequestId==fresh.RequestId),Is.True);
            await db.ForgetRecordAsync("fact",facts.Single(x=>x.Key=="城市").Id,default);
            Assert.That((await db.RetrieveContextAsync("城市",default)).Recent,Is.Empty);
        }
        private sealed class SummaryLLM:ILLMService,IMemoryLLMService
        {
            public bool IsConfigured=>true;
            public readonly TaskCompletionSource<bool> Started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public async Task<string> CompleteJsonAsync(string instruction,string input,int maxTokens,CancellationToken ct)
            {
                if(instruction==SessionSummarizer.Instruction){Started.TrySetResult(true);await Task.Delay(Timeout.Infinite,ct);}
                return "{\"memories\":[],\"facts\":[],\"open_loops\":[],\"resolved_loops\":[]}";
            }
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct)
            {await Task.Yield();yield return new LLMChunk("收到");}
        }
        [TearDown]public void Cleanup()
        {
            db.Dispose();if(!directory.StartsWith(Path.GetFullPath("UserData/Validation")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
