using System;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Logging;

namespace GiftCompanion.Core.Memory
{
    public interface IMemoryService
    {
        bool IsAvailable { get; }
        Task InitializeAsync(CancellationToken ct);
    }

    public interface ISharedMemoryService : IMemoryService, IDisposable
    {
        IDisposable EnterForeground();
        void NotifyWork();
        Task RetryFailedAsync(CancellationToken ct);
        Task StopAsync(CancellationToken ct);
        Task<PreparedDialogue> PrepareDialogueAsync(GiftCompanion.Core.Character.CharacterIdentity identity,string message,
            GiftCompanion.Core.Character.InteractionSource source,CancellationToken ct,GiftCompanion.Core.Character.CompanionDialoguePolicy policy=null);
        Task ForgetRecordAsync(string kind,string id,CancellationToken ct);
        Task ForgetTurnAsync(string requestId,CancellationToken ct);
        Task<System.Collections.Generic.IReadOnlyList<MemoryRecord>> ListActiveAsync(string kind,CancellationToken ct);
    }

    public sealed class MemoryService : ISharedMemoryService
    {
        private readonly IMemoryStore store;
        private readonly IMemoryLLMService llm;
        private readonly bool configured;
        private readonly ILogger logger;
        private readonly CancellationTokenSource lifetime;
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0,1);
        private readonly Func<CancellationToken,Task> refreshExports;
        private readonly object gate = new object();
        private Task worker = Task.CompletedTask;
        private CancellationTokenSource background;
        private TaskCompletionSource<bool> backgroundEnded;
        private int foreground;
        private bool initialized, disposed;
        public bool IsAvailable => initialized && store != null && !disposed;

        // Parameterless instance is retained solely for isolated legacy Core tests/probes.
        public MemoryService(IMemoryStore store = null, ILLMService llm = null, ILogger logger = null, CancellationToken applicationToken = default, Func<CancellationToken,Task> refreshExports=null)
        {
            this.store=store; this.llm=llm as IMemoryLLMService; configured=llm?.IsConfigured==true;
            this.logger=logger; lifetime=CancellationTokenSource.CreateLinkedTokenSource(applicationToken);
            this.refreshExports=refreshExports;
        }
        public async Task InitializeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(initialized) return;
            if(store!=null) await store.RecoverMemoryJobsAsync(ct).ConfigureAwait(false);
            initialized=true;
            if(store!=null && configured && llm!=null)
            {
                worker=Task.Run(WorkLoopAsync); NotifyWork();
            }
        }
        public IDisposable EnterForeground()
        {
            lock(gate) { foreground++; background?.Cancel(); }
            return new ForegroundLease(this);
        }
        public async Task<IDisposable> PauseAndDrainAsync(CancellationToken ct)
        {
            IDisposable lease; Task pending;
            lock (gate)
            {
                foreground++; lease = new ForegroundLease(this); background?.Cancel();
                pending = backgroundEnded?.Task ?? Task.CompletedTask;
            }
            try
            {
                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (ct.Register(() => canceled.TrySetCanceled(ct))) await await Task.WhenAny(pending, canceled.Task).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested(); return lease;
            }
            catch { lease.Dispose(); throw; }
        }
        private void ExitForeground()
        { lock(gate) foreground--; NotifyWork(); }
        public void NotifyWork()
        {
            lock(gate)
            {
                if(disposed || lifetime.IsCancellationRequested) return;
                if(signal.CurrentCount==0) signal.Release();
            }
        }
        public async Task RetryFailedAsync(CancellationToken ct)
        {
            if(store==null) throw new InvalidOperationException("Memory is unavailable.");
            await store.RetryFailedExtractionsAsync(ct).ConfigureAwait(false); NotifyWork();
        }
        public async Task<PreparedDialogue> PrepareDialogueAsync(GiftCompanion.Core.Character.CharacterIdentity identity,string message,
            GiftCompanion.Core.Character.InteractionSource source,CancellationToken ct,GiftCompanion.Core.Character.CompanionDialoguePolicy policy=null)
        {
            if(!IsAvailable) throw new InvalidOperationException("Memory is unavailable.");
            var context=await store.RetrieveContextAsync(message,ct).ConfigureAwait(false);
            return PromptBuilder.Build(identity,message,source,context,DateTimeOffset.Now,policy);
        }
        public async Task ForgetRecordAsync(string kind,string id,CancellationToken ct)
        {
            using var foreground=EnterForeground();
            await store.ForgetRecordAsync(kind,id,ct).ConfigureAwait(false);
        }
        public async Task ForgetTurnAsync(string requestId,CancellationToken ct)
        {
            using var foreground=EnterForeground();
            await store.ForgetTurnAsync(requestId,ct).ConfigureAwait(false);
        }
        public Task<System.Collections.Generic.IReadOnlyList<MemoryRecord>> ListActiveAsync(string kind,CancellationToken ct)=>store.LoadMemoriesAsync(kind,true,20,ct);
        private async Task WorkLoopAsync()
        {
            try
            {
                while(true)
                {
                    await signal.WaitAsync(lifetime.Token).ConfigureAwait(false);
                    while(!lifetime.IsCancellationRequested)
                    {
                        CancellationTokenSource taskToken;
                        lock(gate)
                        {
                            if(foreground>0) break;
                            background=taskToken=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                            backgroundEnded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        }
                        ExtractionJob job=null;
                        SummaryJob summary=null;
                        bool pauseCycle=false;
                        try
                        {
                            job=await store.ClaimExtractionAsync(taskToken.Token).ConfigureAwait(false);
                            if(job==null)
                            {
                                summary=await store.ClaimSummaryAsync(taskToken.Token).ConfigureAwait(false);
                                if(summary==null)break;
                                string response=await llm.CompleteJsonAsync(SessionSummarizer.Instruction,SessionSummarizer.Input(summary),1024,taskToken.Token).ConfigureAwait(false);
                                string text=SessionSummarizer.Parse(response);
                                bool saved=await store.ApplySummaryAsync(summary,text,taskToken.Token).ConfigureAwait(false);
                                logger?.Info($"memory summary result={(saved?"completed":"stale")} turns={summary.Turns.Count}");
                                continue;
                            }
                            var facts=await store.LoadMemoriesAsync("fact",true,20,taskToken.Token).ConfigureAwait(false);
                            var loops=await store.LoadMemoriesAsync("loop",true,15,taskToken.Token).ConfigureAwait(false);
                            string json=await llm.CompleteJsonAsync(MemoryExtractor.Instruction,MemoryExtractor.Input(job.Turn,facts,loops),1024,taskToken.Token).ConfigureAwait(false);
                            var extraction=MemoryExtractor.Parse(json,job.Turn,loops);
                            taskToken.Token.ThrowIfCancellationRequested();
                            bool applied=await store.ApplyExtractionAsync(job,extraction,taskToken.Token).ConfigureAwait(false);
                            if (applied && refreshExports != null) await refreshExports(taskToken.Token).ConfigureAwait(false);
                            logger?.Info($"memory extraction id={job.Turn.RequestId} result={(applied?"completed":"stale")} items={extraction.Items.Count}");
                        }
                        catch(Exception error)
                        {
                            if(job==null&&summary==null) pauseCycle=true;
                            if(job!=null)
                            {
                                try
                                {
                                    using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                    await store.SetExtractionStatusAsync(job.Turn.RequestId,taskToken.IsCancellationRequested?"pending":"failed",cleanup.Token).ConfigureAwait(false);
                                }
                                catch(Exception saveError) { logger?.Error("memory task status save failed",saveError); }
                            }
                            if(summary!=null)
                            {
                                try
                                {
                                    using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3));
                                    await store.SetSummaryStatusAsync(summary.SessionId,taskToken.IsCancellationRequested?"pending":"failed",cleanup.Token).ConfigureAwait(false);
                                }
                                catch(Exception saveError){logger?.Error("summary task status save failed",saveError);}
                            }
                            if(!taskToken.IsCancellationRequested) logger?.Error("memory extraction failed",error);
                        }
                        finally
                        {
                            lock(gate) { if(background==taskToken) { background=null; backgroundEnded?.TrySetResult(true); backgroundEnded=null; } taskToken.Dispose(); }
                        }
                        if(pauseCycle) break;
                    }
                }
            }
            catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
            catch(Exception error) { logger?.Error("memory worker stopped",error); }
        }
        public async Task StopAsync(CancellationToken ct)
        {
            lifetime.Cancel();
            var canceled=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using(ct.Register(()=>canceled.TrySetCanceled(ct))) await await Task.WhenAny(worker,canceled.Task).ConfigureAwait(false);
        }
        public void Dispose()
        {
            lock(gate) { if(disposed)return;disposed=true;lifetime.Cancel(); }
            if(!worker.Wait(2500)) { logger?.Info("memory shutdown timed_out=true"); return; }
            lifetime.Dispose(); signal.Dispose();
        }
        private sealed class ForegroundLease : IDisposable
        {
            private MemoryService owner;
            public ForegroundLease(MemoryService owner)=>this.owner=owner;
            public void Dispose()=>Interlocked.Exchange(ref owner,null)?.ExitForeground();
        }
    }
}
