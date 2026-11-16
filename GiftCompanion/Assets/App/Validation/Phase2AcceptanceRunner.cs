using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;

namespace GiftCompanion.Validation
{
    // Shared test runner. Ordinary startup never calls this entry point.
    public static class Phase2AcceptanceRunner
    {
        public static async Task<Dictionary<string,object>> WriteAsync(ICharacterCore core,IMemoryStore memory,IConversationStore conversation,
            Func<CharacterMode,Task> navigate,CancellationToken ct)
        {
            var result=new Dictionary<string,object>();
            string codeA="MikaA"+Guid.NewGuid().ToString("N").Substring(0,8);
            string codeB="MikaB"+Guid.NewGuid().ToString("N").Substring(0,8);
            string codeNew="MikaC"+Guid.NewGuid().ToString("N").Substring(0,8);
            await navigate(CharacterMode.Companion);
            var first=await Send(core,"请记住：我的行李识别码是"+codeA+"。之后会问你。请简短回复。",ct);
            await navigate(CharacterMode.Home);await navigate(CharacterMode.AgentPet);
            result["companionToPet"]= (await Send(core,"我的行李识别码是什么？只回复识别码。",ct)).Text.Contains(codeA);
            await WaitExtraction(memory,conversation,first.RequestId,ct);
            await FillRecent(core,conversation,memory,ct);
            var context=await memory.RetrieveContextAsync("行李识别码",ct);
            result["outsideRecent"]=context.Recent.All(x=>!x.UserText.Contains(codeA)&&!x.AssistantText.Contains(codeA));
            result["storedFactA"]=context.Facts.Any(x=>x.Content.Contains(codeA));
            result["longTermRecall"]=(await Send(core,"我的行李识别码是什么？只回复识别码。",ct)).Text.Contains(codeA);
            var second=await Send(core,"请记住：我的书签识别码是"+codeB+"。之后会问你。请简短回复。",ct);
            await navigate(CharacterMode.Home);await navigate(CharacterMode.Companion);
            result["petToCompanion"]=(await Send(core,"我的书签识别码是什么？只回复识别码。",ct)).Text.Contains(codeB);
            await WaitExtraction(memory,conversation,second.RequestId,ct);
            var correction=await Send(core,"请更新：之前的行李识别码已经作废，现在的行李识别码是"+codeNew+"。只保留新的有效值。",ct);
            await WaitExtraction(memory,conversation,correction.RequestId,ct);
            result["correctedRecall"]=(await Send(core,"我目前有效的行李识别码是什么？只回复识别码。",ct)).Text.Contains(codeNew);
            var facts=await memory.LoadMemoriesAsync("fact",true,20,ct);
            var target=facts.SingleOrDefault(x=>x.Content.Contains(codeNew));
            if(target==null)throw new InvalidOperationException("Corrected fact was not extracted.");
            int before=(await conversation.LoadTurnsAsync(1000,true,ct)).Count;
            await core.ForgetMemoryAsync("fact",target.Id,ct);
            var forgotten=await memory.RetrieveContextAsync("行李识别码",ct);
            var assembled=PromptBuilder.Build(core.Identity,"行李识别码？",InteractionSource.Companion,forgotten,DateTimeOffset.Now).Request;
            string transmitted=assembled.SystemPrompt+string.Concat(assembled.History.Select(x=>x.Content));
            result["forgottenExcluded"]=!transmitted.Contains(codeA)&&!transmitted.Contains(codeNew);
            result["rawHistoryRetained"]=(await conversation.LoadTurnsAsync(1000,true,ct)).Count==before;
            result["independentFactRetained"]=(await memory.LoadMemoriesAsync("fact",true,20,ct)).Any(x=>x.Content.Contains(codeB));
            await FillRecent(core,conversation,memory,ct);
            // An independent restore oracle stays local in app_state, never in the model prompt.
            core.State.CurrentTopic=codeB;
            await navigate(CharacterMode.Home);
            result["passed"]=result.Values.All(x=>x is bool b&&b);
            return result;
        }
        public static async Task<Dictionary<string,object>> RestoreAsync(ICharacterCore core,IMemoryStore memory,bool configured,
            Func<CharacterMode,Task> navigate,CancellationToken ct)
        {
            var result=new Dictionary<string,object> { ["startedHome"]=core.CurrentMode==CharacterMode.Home };
            string expected=core.State.CurrentTopic;
            await navigate(CharacterMode.AgentPet);
            if(!configured)await FillRecent(core,(IConversationStore)memory,memory,ct);
            var context=await memory.RetrieveContextAsync("书签识别码",ct);
            result["persistentFact"]=expected.StartsWith("MikaB",StringComparison.Ordinal)&&context.Facts.Any(x=>x.Content.Contains(expected));
            result["outsideRecent"]=context.Recent.All(x=>!x.UserText.Contains(expected)&&!x.AssistantText.Contains(expected));
            var reply=await core.SendMessageAsync("我的书签识别码是什么？只回复识别码。",InteractionSource.AgentPet,ct);
            result["replyVerified"]=configured?reply.Success&&reply.Text.Contains(expected):!reply.Success&&reply.ErrorCode=="LLMNotConfigured";
            await navigate(CharacterMode.Home);
            result["passed"]=result.Values.All(x=>x is bool b&&b);return result;
        }
        private static async Task<DialogueResult> Send(ICharacterCore core,string text,CancellationToken ct)
        {
            var source=core.CurrentMode==CharacterMode.AgentPet?InteractionSource.AgentPet:InteractionSource.Companion;
            var result=await core.SendMessageAsync(text,source,ct);
            if(!result.Success)throw new InvalidOperationException("Dialogue failed: "+result.ErrorCode);
            return result;
        }
        private static async Task WaitExtraction(IMemoryStore memory,IConversationStore conversation,string id,CancellationToken ct)
        {
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(55));
            while(true)
            {
                var turn=(await conversation.LoadTurnsAsync(1000,true,deadline.Token)).Single(x=>x.RequestId==id);
                if(turn.ExtractionStatus=="succeeded")return;
                if(turn.ExtractionStatus=="failed"||turn.ExtractionStatus=="skipped")throw new InvalidOperationException("Fact extraction did not complete.");
                await Task.Delay(100,deadline.Token);
            }
        }
        private static async Task FillRecent(ICharacterCore core,IConversationStore conversation,IMemoryStore memory,CancellationToken ct)
        {
            using var lease=((ISharedMemoryService)core.Memory).EnterForeground();
            var source=core.CurrentMode==CharacterMode.AgentPet?InteractionSource.AgentPet:InteractionSource.Companion;
            for(int i=0;i<8;i++)
            {
                var turn=await conversation.BeginTurnAsync(Guid.NewGuid().ToString("N"),"验收用普通闲聊 "+i,source,ct);
                await conversation.CompleteTurnAsync(turn,"已收到闲聊。",core.State,ct);
                await memory.SetExtractionStatusAsync(turn.RequestId,"skipped",ct);
            }
        }
    }
}
