using System;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class CharacterCoreTests
    {
        private static CharacterCore Core(IDatabaseService database) => new CharacterCore(
            new CharacterIdentity("test", "Test", "", ""), new CharacterState(),
            database, new LLMService(), new MemoryService());

        [Test]
        public async Task ModeSwitchSavesDetachedSnapshotBeforeChangingState()
        {
            var database = new TestDatabase();
            var core = Core(database);
            core.State.CurrentTopic = "same character";
            await core.SwitchModeAsync(CharacterMode.Companion);
            var saved = await database.LoadCharacterStateAsync(CancellationToken.None);
            Assert.That(saved.LastMode, Is.EqualTo(CharacterMode.Companion));
            core.State.CurrentTopic = "changed later";
            Assert.That(saved.CurrentTopic, Is.EqualTo("same character"));
            Assert.That(core.State.LastMode, Is.EqualTo(CharacterMode.Companion));
        }

        [Test]
        public void SaveFailureLeavesModeAndStateUnchanged()
        {
            var core = Core(new FailingDatabase());
            Assert.ThrowsAsync<InvalidOperationException>(() => core.SwitchModeAsync(CharacterMode.Companion));
            Assert.That(core.CurrentMode, Is.EqualTo(CharacterMode.Home));
            Assert.That(core.State.LastMode, Is.EqualTo(CharacterMode.Home));
        }

        [Test]
        public async Task ActiveModesMustPassThroughHome()
        {
            var core = Core(new TestDatabase());
            await core.SwitchModeAsync(CharacterMode.Companion);
            Assert.ThrowsAsync<InvalidOperationException>(() => core.SwitchModeAsync(CharacterMode.AgentPet));
            Assert.That(core.CurrentMode, Is.EqualTo(CharacterMode.Companion));
        }

        [Test]
        public void CanceledSwitchDoesNotChangeState()
        {
            var core = Core(new TestDatabase());
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () =>
                    core.SwitchModeAsync(CharacterMode.Companion, cancellation.Token));
                Assert.That(core.CurrentMode, Is.EqualTo(CharacterMode.Home));
            }
        }

        [Test]
        public async Task UnconfiguredLLMDoesNotPretendToAnswer()
        {
            var result = await Core(new TestDatabase()).SendMessageAsync("hello",
                InteractionSource.Companion, CancellationToken.None);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("LLMNotConfigured"));
            Assert.That(result.Text, Is.Empty);
        }

        private sealed class FailingDatabase : IDatabaseService
        {
            public void Dispose() { }
            public bool IsPersistent => false;
            public int SchemaVersion => 0;
            public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
            public Task MigrateAsync(CancellationToken ct) => Task.CompletedTask;
            public Task<CharacterState> LoadCharacterStateAsync(CancellationToken ct) => Task.FromResult<CharacterState>(null);
            public Task SaveCharacterStateAsync(CharacterState state, CancellationToken ct) =>
                Task.FromException(new InvalidOperationException("simulated save failure"));
        }

        [Test] public async Task CancellationAfterPersistenceCommitStillCommitsTheMode()
        {
            using var cancellation = new CancellationTokenSource();
            var core = Core(new CancelAfterCommitDatabase(cancellation));
            var originalState = core.State;
            await core.SwitchModeAsync(CharacterMode.Companion, cancellation.Token);
            Assert.That(cancellation.IsCancellationRequested, Is.True);
            Assert.That(core.CurrentMode, Is.EqualTo(CharacterMode.Companion));
            Assert.That(core.State.LastMode, Is.EqualTo(CharacterMode.Companion));
            Assert.That(core.State, Is.SameAs(originalState));
        }
        private sealed class CancelAfterCommitDatabase : TestDatabase
        {
            private readonly CancellationTokenSource cancellation;
            public CancelAfterCommitDatabase(CancellationTokenSource cancellation) => this.cancellation = cancellation;
            public override async Task SaveCharacterStateAsync(CharacterState state, CancellationToken ct)
            {
                await base.SaveCharacterStateAsync(state, ct);
                cancellation.Cancel();
            }
        }
    }
}
