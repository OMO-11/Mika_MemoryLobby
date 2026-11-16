using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GiftCompanion.Core.Memory
{
    public sealed class MemoryContextChangedException : Exception
    {
        public MemoryContextChangedException() : base("Memory context changed.") { }
    }
    public sealed class MemoryRecord
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Key { get; set; }
        public string Content { get; set; }
        public string Evidence { get; set; }
        public string SourceMessageId { get; set; }
        public string SourceMode { get; set; }
        public string Status { get; set; }
        public double Importance { get; set; }
        public double Confidence { get; set; }
        public double Score { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
    public sealed class MemoryReference
    {
        public string Kind { get; }
        public string Id { get; }
        public MemoryReference(string kind,string id){Kind=kind;Id=id;}
    }
    public sealed class ExtractionJob
    {
        public ConversationTurn Turn { get; set; }
        public long Revision { get; set; }
    }
    public sealed class MemoryExtraction
    {
        public List<MemoryRecord> Items { get; } = new List<MemoryRecord>();
        public List<string> ResolvedLoops { get; } = new List<string>();
    }
    public sealed class SummaryJob
    {
        public string SessionId { get; set; }
        public string PreviousSummary { get; set; }
        public long ThroughBefore { get; set; }
        public long ThroughAfter { get; set; }
        public long SessionRevision { get; set; }
        public long MemoryRevision { get; set; }
        public List<ConversationTurn> Turns { get; } = new List<ConversationTurn>();
        public List<string> SourceIds { get; } = new List<string>();
    }
    public interface IMemoryStore
    {
        Task RecoverMemoryJobsAsync(CancellationToken ct);
        Task<ExtractionJob> ClaimExtractionAsync(CancellationToken ct);
        Task<IReadOnlyList<MemoryRecord>> LoadMemoriesAsync(string kind, bool activeOnly, int limit, CancellationToken ct);
        Task<bool> ApplyExtractionAsync(ExtractionJob job, MemoryExtraction extraction, CancellationToken ct);
        Task SetExtractionStatusAsync(string requestId, string status, CancellationToken ct);
        Task RetryFailedExtractionsAsync(CancellationToken ct);
        Task<MemoryContext> RetrieveContextAsync(string text,CancellationToken ct);
        Task<SummaryJob> ClaimSummaryAsync(CancellationToken ct);
        Task<bool> ApplySummaryAsync(SummaryJob job,string summary,CancellationToken ct);
        Task SetSummaryStatusAsync(string sessionId,string status,CancellationToken ct);
        Task ForgetRecordAsync(string kind,string id,CancellationToken ct);
        Task ForgetTurnAsync(string requestId,CancellationToken ct);
    }
}
