using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GiftCompanion.Core.Memory
{
    public sealed class HistoryCursor
    {
        public string CreatedAt { get; }
        public string UserMessageId { get; }
        public HistoryCursor(string createdAt, string userMessageId) { CreatedAt = createdAt; UserMessageId = userMessageId; }
    }
    public sealed class HistoryPage
    {
        public IReadOnlyList<ConversationTurn> Turns { get; }
        public HistoryCursor Next { get; }
        public HistoryPage(IReadOnlyList<ConversationTurn> turns, HistoryCursor next) { Turns = turns; Next = next; }
    }
    public interface IConversationHistoryStore
    { Task<HistoryPage> ReadHistoryAsync(HistoryCursor cursor, int pageSize, CancellationToken ct); }
    public sealed class ConversationHistoryService
    {
        private readonly IConversationHistoryStore store;
        public ConversationHistoryService(IConversationHistoryStore store) { this.store = store ?? throw new ArgumentNullException(nameof(store)); }
        public Task<HistoryPage> ReadPageAsync(HistoryCursor cursor, CancellationToken ct, int pageSize = 30)
        { if (pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize)); return store.ReadHistoryAsync(cursor, pageSize, ct); }
    }
}
