using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Persistence;
using GiftCompanion.Core.Memory;
using System;
using System.Collections.Generic;

namespace GiftCompanion.Tests
{
    // Only tests use RAM. Production always opens the native SQLite service.
    internal class TestDatabase : IDatabaseService, IConversationStore
    {
        private CharacterState snapshot;
        public bool IsPersistent => false;
        public int SchemaVersion => 0;
        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task MigrateAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<CharacterState> LoadCharacterStateAsync(CancellationToken ct) => Task.FromResult(snapshot?.Snapshot());
        public virtual Task SaveCharacterStateAsync(CharacterState state, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); snapshot = state.Snapshot(); return Task.CompletedTask;
        }
        public void Dispose() { }
        public virtual Task<ConversationTurn> BeginTurnAsync(string requestId, string text, InteractionSource source, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new ConversationTurn { RequestId=requestId, UserMessageId=Guid.NewGuid().ToString("N"),
                SessionId="test", UserText=text, Source=source, Status="pending" });
        }
        public virtual Task CompleteTurnAsync(ConversationTurn turn, string reply, CharacterState state, CancellationToken ct,
            IReadOnlyList<string> contextSources = null, long contextRevision = -1,IReadOnlyList<MemoryReference> memoryReferences = null,DialoguePresentation presentation=null,bool suppressMemory=false) => SaveCharacterStateAsync(state, ct);
        public Task FailTurnAsync(string requestId, string status, CancellationToken ct) => Task.CompletedTask;
        public Task RecoverConversationsAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<ConversationTurn>> LoadTurnsAsync(int limit, bool completedOnly, CancellationToken ct) => Task.FromResult<IReadOnlyList<ConversationTurn>>(Array.Empty<ConversationTurn>());
        public Task EndSessionAndSaveStateAsync(CharacterState state, string reason, CancellationToken ct) => SaveCharacterStateAsync(state, ct);
    }
}
