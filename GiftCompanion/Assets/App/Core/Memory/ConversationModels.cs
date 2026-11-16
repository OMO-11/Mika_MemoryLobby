using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;

namespace GiftCompanion.Core.Memory
{
    public sealed class ConversationTurn
    {
        public string RequestId { get; set; }
        public string SessionId { get; set; }
        public string UserMessageId { get; set; }
        public string AssistantMessageId { get; set; }
        public InteractionSource Source { get; set; }
        public string UserText { get; set; }
        public string AssistantText { get; set; }
        public string RawAssistantText { get; set; }
        public bool ExcludedFromMemory { get; set; }
        public string Status { get; set; }
        public string ExtractionStatus { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    // Implemented by the same DatabaseService/connection that owns app_state.
    public interface IConversationStore
    {
        Task RecoverConversationsAsync(CancellationToken ct);
        Task<ConversationTurn> BeginTurnAsync(string requestId, string text, InteractionSource source, CancellationToken ct);
        Task CompleteTurnAsync(ConversationTurn turn, string reply, CharacterState state, CancellationToken ct,
            IReadOnlyList<string> contextSources = null, long contextRevision = -1,IReadOnlyList<MemoryReference> memoryReferences = null,DialoguePresentation presentation=null,bool suppressMemory=false);
        Task FailTurnAsync(string requestId, string status, CancellationToken ct);
        Task<IReadOnlyList<ConversationTurn>> LoadTurnsAsync(int limit, bool completedOnly, CancellationToken ct);
        Task EndSessionAndSaveStateAsync(CharacterState state, string reason, CancellationToken ct);
    }
}
