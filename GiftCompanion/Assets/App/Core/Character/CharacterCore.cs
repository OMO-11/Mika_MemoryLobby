using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using GiftCompanion.Core.Logging;
using GiftCompanion.Core.Agent;

namespace GiftCompanion.Core.Character
{
    public interface ICharacterCore
    {
        CharacterIdentity Identity { get; }
        CharacterState State { get; }
        CharacterMode CurrentMode { get; }
        IMemoryService Memory { get; }
        Task<DialogueResult> SendMessageAsync(string message, InteractionSource source, CancellationToken ct);
        IAsyncEnumerable<LLMChunk> StreamMessageAsync(string message, InteractionSource source, CancellationToken ct, AgentTurnContext agentTurn=null);
        Task PauseAndCancelAsync(CancellationToken ct);
        void ResumeRequests();
        Task RecoverHomeAsync();
        Task SwitchModeAsync(CharacterMode mode, CancellationToken ct = default);
        Task ForgetMemoryAsync(string kind,string id,CancellationToken ct);
        Task ForgetTurnAsync(string requestId,CancellationToken ct);
    }

    public sealed class CharacterCore : ICharacterCore
    {
        private readonly IDatabaseService database;
        private readonly ILLMService llm;
        private readonly ILogger logger;
        private readonly CancellationToken applicationToken;
        private readonly CompanionDialoguePolicy companionPolicy;
        private readonly Func<CancellationToken, Task> memoryChanged;
        private readonly IAgentConversation agent;
        private readonly object requestGate = new object();
        private CancellationTokenSource activeRequest;
        private TaskCompletionSource<bool> requestEnded;
        private TaskCompletionSource<bool> memoryCommandEnded;
        private bool paused, memoryChanging;
        public CharacterIdentity Identity { get; private set; }
        public CharacterState State { get; }
        public CharacterMode CurrentMode { get; private set; } = CharacterMode.Home;
        public IMemoryService Memory { get; }

        public CharacterCore(CharacterIdentity identity, CharacterState state,
            IDatabaseService database, ILLMService llm, IMemoryService memory, ILogger logger = null, CancellationToken applicationToken = default, CompanionDialoguePolicy companionPolicy=null, Func<CancellationToken,Task> memoryChanged=null, IAgentConversation agent=null)
        {
            Identity = identity;
            State = state;
            this.database = database;
            this.llm = llm;
            Memory = memory;
            this.logger = logger;
            this.applicationToken = applicationToken;
            this.companionPolicy = companionPolicy ?? new CompanionDialoguePolicy();
            this.memoryChanged = memoryChanged;
            this.agent = agent;
        }

        public async Task SwitchModeAsync(CharacterMode mode, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(CharacterMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (CurrentMode == mode) return;
            if (CurrentMode != CharacterMode.Home && mode != CharacterMode.Home)
                throw new InvalidOperationException("Active modes must switch through Home.");

            // Saving must succeed before mode/state changes become visible.
            var next = State.Snapshot();
            next.LastMode = mode;
            if (CurrentMode != CharacterMode.Home && database is IConversationStore conversations)
                await conversations.EndSessionAndSaveStateAsync(next, "closed", ct);
            else await database.SaveCharacterStateAsync(next, ct);
            // Persistence crossed its commit barrier. Cancellation cannot undo the saved mode.
            CurrentMode = mode;
            State.LastMode = mode;
            (Memory as ISharedMemoryService)?.NotifyWork();
        }

        public async Task RecoverHomeAsync()
        {
            var next = State.Snapshot(); next.LastMode = CharacterMode.Home;
            if (database is IConversationStore conversations)
                await conversations.EndSessionAndSaveStateAsync(next, "closed", CancellationToken.None);
            else await database.SaveCharacterStateAsync(next, CancellationToken.None);
            CurrentMode = CharacterMode.Home; State.LastMode = CharacterMode.Home;
        }

        public async Task<DialogueResult> SendMessageAsync(string message, InteractionSource source, CancellationToken ct)
        {
            await foreach (var chunk in StreamMessageAsync(message, source, ct))
                if (chunk.IsFinal) return chunk.Result;
            return DialogueResult.Failed("StreamTruncated");
        }

        public async IAsyncEnumerable<LLMChunk> StreamMessageAsync(string message, InteractionSource source,
            [EnumeratorCancellation] CancellationToken ct, AgentTurnContext agentTurn=null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is empty.", nameof(message));
            if (!Enum.IsDefined(typeof(InteractionSource), source)) throw new ArgumentOutOfRangeException(nameof(source));
            if (message.Length > 12000) { yield return LLMChunk.Complete(DialogueResult.Failed("InputTooLong")); yield break; }
            if (agentTurn != null && (source != InteractionSource.AgentPet || CurrentMode != CharacterMode.AgentPet || agent == null)) { yield return LLMChunk.Complete(DialogueResult.Failed("ToolsUnavailable")); yield break; }
            if (!llm.IsConfigured) { yield return LLMChunk.Complete(DialogueResult.Unavailable()); yield break; }
            CancellationTokenSource request = null;
            string refusal = null;
            lock (requestGate)
            {
                if (paused || memoryChanging || applicationToken.IsCancellationRequested) refusal = "Unavailable";
                else if (activeRequest != null) refusal = "Busy";
                else
                {
                    request = CancellationTokenSource.CreateLinkedTokenSource(ct, applicationToken);
                    activeRequest = request;
                    requestEnded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            if (refusal != null) { yield return LLMChunk.Complete(DialogueResult.Failed(refusal)); yield break; }
            string requestId = Guid.NewGuid().ToString("N");
            var elapsed = Stopwatch.StartNew();
            var text = new StringBuilder();
            var body = new StringBuilder();
            var parser = source == InteractionSource.Companion ? new CompanionDialogueParser(companionPolicy) : null;
            bool metadataSent = false;
            DialoguePresentation presentation = null;
            int chunks = 0;
            string resultCode = "Canceled";
            var conversations = database as IConversationStore;
            ConversationTurn turn = null;
            PreparedDialogue prepared = null;
            bool committed = false;
            using var memoryLease = (Memory as ISharedMemoryService)?.EnterForeground();
            logger?.Info($"llm start id={requestId} source={source}");
            try
            {
                try
                {
                    if (conversations == null) throw new InvalidOperationException("Conversation storage is required.");
                    turn = await conversations.BeginTurnAsync(requestId, message, source, request.Token);
                    if (Memory.IsAvailable && Memory is ISharedMemoryService sharedMemory)
                        prepared = await sharedMemory.PrepareDialogueAsync(Identity, message, source, request.Token, companionPolicy);
                    else prepared = new PreparedDialogue { Request = new DialogueRequest(message, source,
                        $"角色名字：{Identity.DisplayName}\n{Identity.Persona}\n{Identity.Relationship}" +
                        (source == InteractionSource.Companion ? companionPolicy.Instruction : "")) };
                }
                catch (Exception error)
                {
                    resultCode = request.IsCancellationRequested ? "Canceled" :
                        error is ArgumentException && error.Message == "InputTooLong" ? "InputTooLong" : "StorageFailed";
                    logger?.Error("dialogue begin failed", error);
                }
                if (turn == null || prepared == null) { yield return LLMChunk.Complete(DialogueResult.Failed(resultCode)); yield break; }
                var stream = agentTurn == null ? llm.StreamDialogueAsync(prepared.Request, request.Token) : agent.StreamAsync(prepared.Request, requestId, agentTurn, request.Token);
                await using var iterator = stream.GetAsyncEnumerator(request.Token);
                while (true)
                {
                    bool next;
                    try { next = await iterator.MoveNextAsync(); }
                    catch (Exception error)
                    {
                        resultCode = error is LLMServiceException failure ? failure.Code : LLMService.ErrorCode(error, request.Token);
                        break;
                    }
                    if (request.IsCancellationRequested) { resultCode = "Canceled"; break; }
                    if (!next) { resultCode = text.Length > 0 ? "Completed" : "EmptyResponse"; break; }
                    if (text.Length + iterator.Current.Text.Length > 32000) { resultCode = "ResponseTooLong"; break; }
                    text.Append(iterator.Current.Text); chunks++;
                    string delta = parser == null ? iterator.Current.Text : parser.Feed(iterator.Current.Text);
                    if (parser != null && parser.MetadataReady && !metadataSent)
                    { metadataSent = true; yield return LLMChunk.Metadata(new DialoguePresentation("", parser.Emotion, parser.Action, parser.VoiceIntent)); }
                    if (!string.IsNullOrEmpty(delta)) { body.Append(delta); yield return new LLMChunk(delta); }
                }
                if (resultCode == "Completed")
                {
                    string tail = parser?.Finish() ?? "";
                    if (tail.Length > 0) { body.Append(tail); yield return new LLMChunk(tail); }
                    if (string.IsNullOrWhiteSpace(body.ToString())) resultCode = "EmptyResponse";
                    if (parser != null) presentation = new DialoguePresentation(body.ToString(), parser.Emotion, parser.Action, parser.VoiceIntent);
                }
                if (resultCode == "Completed")
                {
                    var snapshot = State.Snapshot(); snapshot.LastInteractionAt = DateTimeOffset.UtcNow;
                    if (presentation != null) snapshot.Mood = presentation.Emotion;
                    try
                    {
                        await conversations.CompleteTurnAsync(turn, text.ToString(), snapshot, request.Token, prepared.SourceMessageIds, prepared.Revision,prepared.MemoryReferences,presentation,agentTurn!=null);
                        committed = true;
                        // Pause waits for this request; cancellation cannot undo a DB commit.
                        lock (requestGate) { State.LastInteractionAt = snapshot.LastInteractionAt; State.Mood = snapshot.Mood; }
                        (Memory as ISharedMemoryService)?.NotifyWork();
                    }
                    catch (Exception error)
                    {
                        resultCode = request.IsCancellationRequested ? "Canceled" : error is MemoryContextChangedException ? "MemoryContextChanged" : "StorageFailed";
                        logger?.Error("dialogue complete failed", error);
                    }
                }
                yield return LLMChunk.Complete(resultCode == "Completed" ? DialogueResult.Completed(body.ToString(),requestId,presentation) : DialogueResult.Failed(resultCode));
            }
            finally
            {
                if (turn != null && !committed)
                {
                    try
                    {
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await conversations.FailTurnAsync(requestId, resultCode == "Canceled" ? "canceled" : "failed", cleanup.Token);
                    }
                    catch (Exception error) { logger?.Error("dialogue failure status save failed", error); }
                }
                logger?.Info($"llm end id={requestId} source={source} result={resultCode} chunks={chunks} characters={text.Length} elapsedMs={elapsed.ElapsedMilliseconds}");
                lock (requestGate)
                {
                    activeRequest = null;
                    request.Dispose();
                    requestEnded.TrySetResult(true);
                }
            }
        }

        public async Task PauseAndCancelAsync(CancellationToken ct)
        {
            Task ending;
            lock (requestGate)
            {
                paused = true;
                activeRequest?.Cancel();
                ending = Task.WhenAll(requestEnded?.Task ?? Task.CompletedTask,memoryCommandEnded?.Task ?? Task.CompletedTask);
            }
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => canceled.TrySetCanceled(ct)))
                await await Task.WhenAny(ending, canceled.Task);
            ct.ThrowIfCancellationRequested();
        }
        public void ResumeRequests() { lock (requestGate) paused = false; }
        public void UsePlayerCharacterCard(CharacterIdentity identity)
        {
            lock (requestGate)
            {
                if (!paused || activeRequest != null || memoryChanging || identity == null || identity.Id != Identity.Id)
                    throw new InvalidOperationException("Character card changes require an idle paused core with the same character ID.");
                Identity = identity;
            }
        }
        public Task ForgetMemoryAsync(string kind,string id,CancellationToken ct)=>ChangeMemoryAsync((m,token)=>m.ForgetRecordAsync(kind,id,token),ct);
        public Task ForgetTurnAsync(string requestId,CancellationToken ct)=>ChangeMemoryAsync((m,token)=>m.ForgetTurnAsync(requestId,token),ct);
        private async Task ChangeMemoryAsync(Func<ISharedMemoryService,CancellationToken,Task> change,CancellationToken ct)
        {
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,applicationToken);
            linked.Token.ThrowIfCancellationRequested();
            if(!(Memory is ISharedMemoryService shared)||!shared.IsAvailable)throw new InvalidOperationException("Memory is unavailable.");
            Task ending;
            lock(requestGate)
            {
                if(memoryChanging)throw new InvalidOperationException("Memory command is busy.");
                memoryCommandEnded=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                memoryChanging=true;activeRequest?.Cancel();ending=requestEnded?.Task??Task.CompletedTask;
            }
            try
            {
                var canceled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using(linked.Token.Register(()=>canceled.TrySetCanceled(linked.Token)))await await Task.WhenAny(ending,canceled.Task);
                linked.Token.ThrowIfCancellationRequested();await change(shared,linked.Token);
                if (memoryChanged != null) await memoryChanged(linked.Token);
            }
            finally {lock(requestGate){memoryChanging=false;memoryCommandEnded.TrySetResult(true);}}
        }
    }
}
