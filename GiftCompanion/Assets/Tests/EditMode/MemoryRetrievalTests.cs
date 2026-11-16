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
    public sealed class MemoryRetrievalTests
    {
        private string directory,path;
        private DatabaseService db;
        [SetUp] public async Task Setup()
        {
            directory=Path.GetFullPath("UserData/Validation/retrieval-"+Guid.NewGuid().ToString("N"));path=Path.Combine(directory,"memory.db");
            db=new DatabaseService(path,"test");await db.InitializeAsync(default);await db.MigrateAsync(default);
        }
        private async Task<ConversationTurn> Turn(string text,InteractionSource source=InteractionSource.Companion)
        {
            var turn=await db.BeginTurnAsync(Guid.NewGuid().ToString("N"),text,source,default);
            await db.CompleteTurnAsync(turn,"完整回答",new CharacterState(),default);return turn;
        }
        private async Task Store(string text,string kind="memory",string key="",double importance=.7)
        {
            var turn=await Turn(text);var job=await db.ClaimExtractionAsync(default);
            var result=new MemoryExtraction();result.Items.Add(new MemoryRecord {Kind=kind,Key=key,Content=text,Evidence=text,
                SourceMessageId=turn.UserMessageId,Confidence=.95,Importance=importance});
            Assert.That(await db.ApplyExtractionAsync(job,result,default),Is.True);
        }
        [Test] public async Task NativeFtsFindsNaturalChineseJapaneseAndSingleCharacterWithoutQueryInjection()
        {
            await Store("昨天在京都买了樱花点心");await Store("京都の桜を見に行きました");await Store("我喜欢喝茶");
            await Store("我的行李识别码是Mika77Star");
            Assert.That((await db.RetrieveContextAsync("还记得京都的点心吗？",default)).Memories.Any(x=>x.Content.Contains("点心")),Is.True);
            Assert.That((await db.RetrieveContextAsync("京都の桜はどうでしたか",default)).Memories.Any(x=>x.Content.Contains("桜")),Is.True);
            Assert.That((await db.RetrieveContextAsync("茶",default)).Memories.Any(x=>x.Content.Contains("茶")),Is.True);
            Assert.That((await db.RetrieveContextAsync("Mika77Star",default)).Memories.Any(x=>x.Content.Contains("Mika77Star")),Is.True);
            var punctuation=await db.RetrieveContextAsync("\" OR ( * ' ); DROP TABLE messages; --",default);
            Assert.That(punctuation.Memories.Count,Is.LessThanOrEqualTo(5));
            Assert.That((await db.LoadTurnsAsync(20,true,default)).Count,Is.EqualTo(4));
        }
        [Test] public async Task LongTermEventIsRetrievedAfterItLeavesRecentContextAndAcrossReopen()
        {
            await Store("在月见山顶发现了银色风铃");
            for(int i=0;i<8;i++)await Turn("普通闲聊轮次 "+i,InteractionSource.AgentPet);
            var first=await db.RetrieveContextAsync("银色风铃在哪里发现的？",default);
            Assert.That(first.Recent.Count,Is.EqualTo(6));
            Assert.That(first.Recent.All(x=>!x.UserText.Contains("银色风铃")),Is.True);
            Assert.That(first.Memories.Single().Content,Does.Contain("月见山顶"));
            db.Dispose();db=new DatabaseService(path,"test");await db.InitializeAsync(default);await db.MigrateAsync(default);
            var reopened=await db.RetrieveContextAsync("银色风铃",default);
            Assert.That(reopened.Memories.Count,Is.EqualTo(1));
            Assert.That(reopened.Memories[0].Score,Is.InRange(0.0,1.0));
        }
        [Test] public async Task RankingUsesImportanceWhenRelevanceMatchesAndInactiveFactsLeaveSearch()
        {
            await Store("共同暗号是蓝色月光",importance:.1);
            await Store("共同暗号为蓝色星辰",importance:.95);
            var ranked=await db.RetrieveContextAsync("共同暗号",default);
            Assert.That(ranked.Memories.First().Content,Does.Contain("星辰"));
            await Store("喜欢茉莉花茶","fact","饮品偏好");await Store("改喝桂花乌龙茶","fact","饮品偏好");
            var corrected=await db.RetrieveContextAsync("茉莉花茶",default);
            Assert.That(corrected.Facts.Count,Is.EqualTo(1));
            Assert.That(corrected.Facts.Single().Content,Does.Contain("桂花乌龙茶"));
            Assert.That(corrected.Recent.All(x=>!x.UserText.Contains("茉莉花茶")),Is.True);
            using var inspect=new SqliteConnection(path);
            using var q=inspect.Prepare("SELECT count(*) FROM memory_search WHERE body LIKE '%茉莉花茶%';");q.Row();Assert.That(q.Integer(0),Is.Zero);
        }
        [Test] public void PromptBudgetPreservesIdentityRolesAndOneCurrentMessage()
        {
            var context=new MemoryContext {Revision=7,Summary=new string('摘',1000)};
            for(int i=0;i<20;i++)context.Facts.Add(new MemoryRecord {Key="事实"+i,Content=new string('实',450),SourceMessageId="fact"+i});
            for(int i=0;i<5;i++) {context.Memories.Add(new MemoryRecord {Content=new string('事',450),SourceMessageId="event"+i});context.Loops.Add(new MemoryRecord {Content=new string('待',450),SourceMessageId="loop"+i});}
            for(int i=0;i<6;i++)context.Recent.Add(new ConversationTurn {UserText="user"+i+new string('用',500),AssistantText="assistant"+i+new string('回',500),UserMessageId="u"+i,AssistantMessageId="a"+i});
            var identity=new CharacterIdentity("test","名字","只读人设","只读关系");
            var built=PromptBuilder.Build(identity,"UNIQUE_CURRENT_MESSAGE",InteractionSource.AgentPet,context,DateTimeOffset.Parse("2026-10-13T12:00:00+08:00"));
            Assert.That(built.Request.SystemPrompt,Does.Contain("只读人设").And.Contain("只读关系").And.Contain("2026-10-13"));
            int total=built.Request.Message.Length+built.Request.SystemPrompt.Length+built.Request.History.Sum(x=>x.Content.Length);
            Assert.That(total,Is.LessThanOrEqualTo(PromptBuilder.CharacterBudget));
            Assert.That(built.Request.SystemPrompt,Does.Not.Contain("UNIQUE_CURRENT_MESSAGE"));
            Assert.That(built.Request.History.All(x=>!x.Content.Contains("UNIQUE_CURRENT_MESSAGE")),Is.True);
            Assert.That(built.Request.History.Count%2,Is.Zero);
            for(int i=0;i<built.Request.History.Count;i+=2) {Assert.That(built.Request.History[i].Role,Is.EqualTo("user"));Assert.That(built.Request.History[i+1].Role,Is.EqualTo("assistant"));}
            Assert.Throws<ArgumentException>(()=>PromptBuilder.Build(identity,new string('长',12000),InteractionSource.Companion,context,DateTimeOffset.Now));
        }
        [Test] public async Task SharedCoreKeepsUnextractedRecentTurnAfterSwitchAndPassesActualContextToLlm()
        {
            var fake=new RecordingLLM();using var memory=new MemoryService(db,fake);await memory.InitializeAsync(default);
            var core=new CharacterCore(new CharacterIdentity("test","Test","",""),new CharacterState(),db,fake,memory);
            await core.SwitchModeAsync(CharacterMode.Companion);
            Assert.That((await core.SendMessageAsync("我的行李代号是霞光七号",InteractionSource.Companion,default)).Success,Is.True);
            await core.SwitchModeAsync(CharacterMode.Home);await core.SwitchModeAsync(CharacterMode.AgentPet);
            Assert.That((await core.SendMessageAsync("你记得我的行李代号吗？",InteractionSource.AgentPet,default)).Success,Is.True);
            Assert.That(fake.Requests.Count,Is.EqualTo(2));
            Assert.That(fake.Requests[1].History[0].Content,Does.Contain("霞光七号"));
            Assert.That(fake.Requests[1].Source,Is.EqualTo(InteractionSource.AgentPet));
            Assert.That((await db.LoadTurnsAsync(10,true,default)).All(x=>x.ExtractionStatus=="pending"),Is.True);
            await memory.StopAsync(default);
        }
        private sealed class RecordingLLM : ILLMService
        {
            public bool IsConfigured=>true;
            public readonly List<DialogueRequest> Requests=new List<DialogueRequest>();
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,[System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct)
            {Requests.Add(request);await Task.Yield();yield return new LLMChunk("已收到");}
        }
        [Test] public async Task ContextRevisionChangeRejectsAStaleReplyWithoutPartialCommit()
        {
            await Store("喜欢茉莉花茶","fact","饮品偏好");
            var context=await db.RetrieveContextAsync("我的饮品偏好",default);
            var pending=await db.BeginTurnAsync("stale-request","你记得我的饮品偏好吗？",InteractionSource.Companion,default);
            await Store("改喝桂花乌龙茶","fact","饮品偏好");
            Assert.ThrowsAsync<MemoryContextChangedException>(()=>db.CompleteTurnAsync(pending,"依据旧事实的回复",new CharacterState(),default,
                new[]{context.Facts[0].SourceMessageId},context.Revision));
            var saved=(await db.LoadTurnsAsync(20,false,default)).Single(x=>x.RequestId==pending.RequestId);
            Assert.That(saved.AssistantText,Is.Empty);Assert.That(saved.Status,Is.EqualTo("pending"));
            Assert.That((await db.RetrieveContextAsync("饮品偏好",default)).Facts.Single().Content,Does.Contain("桂花乌龙茶"));
        }
        [TearDown] public void Cleanup()
        {
            db?.Dispose();
            if(!directory.StartsWith(Path.GetFullPath("UserData/Validation")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}
