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
using Newtonsoft.Json;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class ConversationTests
    {
        private string directory, path;
        [SetUp] public void Setup()
        {
            directory = Path.GetFullPath("UserData/Validation/conversation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "memory.db");
        }
        private async Task<DatabaseService> Open()
        {
            var db = new DatabaseService(path, "test");
            await db.InitializeAsync(default); await db.MigrateAsync(default); return db;
        }
        private CharacterCore Core(IDatabaseService db, ILLMService llm) => new CharacterCore(
            new CharacterIdentity("test", "Test", "", ""), new CharacterState(), db, llm, new MemoryService());

        [Test] public async Task V1UpgradePreservesStateAndBackupAndRawTurnsSurviveReopen()
        {
            var state = new CharacterState { CurrentTopic="原始状态 🌙", Energy=.31f, LastMode=CharacterMode.AgentPet };
            using (var seed = new SqliteConnection(path))
            {
                seed.Execute("CREATE TABLE app_state(character_id TEXT PRIMARY KEY,state_version INTEGER,state_json TEXT,updated_at TEXT); PRAGMA user_version=1;");
                using var insert = seed.Prepare("INSERT INTO app_state VALUES('test',1,?1,?2);");
                insert.Bind(1, JsonConvert.SerializeObject(state)); insert.Bind(2, DateTimeOffset.UtcNow.ToString("O")); insert.Done();
            }
            using (var db = await Open())
            {
                Assert.That((await db.LoadCharacterStateAsync(default)).CurrentTopic, Is.EqualTo(state.CurrentTopic));
                Assert.That(Directory.GetFiles(directory, "*.bak").Length, Is.EqualTo(1));
                using var backup = new SqliteConnection(Directory.GetFiles(directory, "*.bak").Single());
                Assert.That(backup.Version, Is.EqualTo(1));
                var turn = await db.BeginTurnAsync("one", "京都で珈琲，中文 🌙 ' OR 1=1 --", InteractionSource.Companion, default);
                state.LastInteractionAt = DateTimeOffset.UtcNow;
                await db.CompleteTurnAsync(turn, "完整回复 🌸", state, default);
                await db.CompleteTurnAsync(turn, "完整回复 🌸", state, default);
                await db.EndSessionAndSaveStateAsync(state, "closed", default);
                var second = await db.BeginTurnAsync("two", "第二种形态", InteractionSource.AgentPet, default);
                await db.CompleteTurnAsync(second, "同一角色", state, default);
                Assert.That(second.SessionId, Is.Not.EqualTo(turn.SessionId));
            }
            using (var reopened = await Open())
            {
                var turns = await reopened.LoadTurnsAsync(20, true, default);
                Assert.That(turns.Count, Is.EqualTo(2));
                Assert.That(turns[0].UserText, Is.EqualTo("京都で珈琲，中文 🌙 ' OR 1=1 --"));
                Assert.That(turns[0].AssistantText, Is.EqualTo("完整回复 🌸"));
                Assert.That(turns[0].ExtractionStatus, Is.EqualTo("pending"));
                Assert.That(turns[1].Source, Is.EqualTo(InteractionSource.AgentPet));
                Assert.That((await reopened.LoadCharacterStateAsync(default)).LastInteractionAt, Is.EqualTo(state.LastInteractionAt));
            }
        }

        [Test] public async Task RecoveryRetainsInterruptedUserAndDoesNotReplayOrFabricateAssistant()
        {
            string oldSession;
            using (var db = await Open())
            {
                var turn = await db.BeginTurnAsync("pending", "未完成的原文", InteractionSource.Companion, default);
                oldSession = turn.SessionId;
                Assert.That((await db.BeginTurnAsync("pending", "未完成的原文", InteractionSource.Companion, default)).UserMessageId, Is.EqualTo(turn.UserMessageId));
                Assert.ThrowsAsync<InvalidDataException>(() => db.BeginTurnAsync("pending", "ID 冲突", InteractionSource.Companion, default));
            }
            using (var db = await Open())
            {
                await db.RecoverConversationsAsync(default);
                var saved = (await db.LoadTurnsAsync(10, false, default)).Single();
                Assert.That(saved.Status, Is.EqualTo("interrupted"));
                Assert.That(saved.AssistantText, Is.Empty);
                Assert.That(await db.LoadTurnsAsync(10, true, default), Is.Empty);
                var next = await db.BeginTurnAsync("new", "新会话", InteractionSource.Companion, default);
                Assert.That(next.SessionId, Is.Not.EqualTo(oldSession));
            }
        }

        [Test] public async Task FailureDuringCompletionRollsBackAssistantStateAndExtractionTogether()
        {
            using var db = await Open();
            using (var fault = new SqliteConnection(path))
                fault.Execute("CREATE TRIGGER fail_state BEFORE INSERT ON app_state BEGIN SELECT RAISE(ABORT,'fault'); END;");
            var llm = new Reply(); var core = Core(db, llm);
            var result = await core.SendMessageAsync("保留用户原文", InteractionSource.Companion, default);
            Assert.That(result.ErrorCode, Is.EqualTo("StorageFailed"));
            Assert.That(llm.Calls, Is.EqualTo(1));
            var turn = (await db.LoadTurnsAsync(10, false, default)).Single();
            Assert.That(turn.Status, Is.EqualTo("failed"));
            Assert.That(turn.AssistantText, Is.Empty);
            Assert.That(turn.ExtractionStatus, Is.EqualTo("none"));
            Assert.That(core.State.LastInteractionAt, Is.Null);
            Assert.That(await db.LoadCharacterStateAsync(default), Is.Null);
        }

        [Test] public async Task SaveBeforeHttpFailureAndTruncationNeverBecomeCompleteTurns()
        {
            var neverCalled = new Reply();
            var refused = await Core(new BeginFailure(), neverCalled).SendMessageAsync("input", InteractionSource.Companion, default);
            Assert.That(refused.ErrorCode, Is.EqualTo("StorageFailed"));
            Assert.That(neverCalled.Calls, Is.Zero);
            using var db = await Open();
            var core = Core(db, new Reply { Truncate=true });
            Assert.That((await core.SendMessageAsync("输入", InteractionSource.Companion, default)).ErrorCode, Is.EqualTo("StreamTruncated"));
            var turn = (await db.LoadTurnsAsync(10, false, default)).Single();
            Assert.That(turn.Status, Is.EqualTo("failed"));
            Assert.That(turn.AssistantText, Is.Empty);
            Assert.That(await db.LoadTurnsAsync(10, true, default), Is.Empty);
        }

        [Test] public async Task CanceledPersistenceAndFailedMigrationDoNotLeaveHalfACommit()
        {
            using (var db = await Open())
            {
                using var canceled = new CancellationTokenSource(); canceled.Cancel();
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () => db.BeginTurnAsync("cancel", "input", InteractionSource.AgentPet, canceled.Token));
                Assert.That(await db.LoadTurnsAsync(10, false, default), Is.Empty);
            }
            string otherPath = Path.Combine(directory, "conflict.db");
            using (var seed = new SqliteConnection(otherPath))
                seed.Execute("CREATE TABLE app_state(character_id TEXT,state_version INTEGER,state_json TEXT,updated_at TEXT); CREATE TABLE sessions(conflict TEXT); PRAGMA user_version=1;");
            using var conflict = new DatabaseService(otherPath, "test");
            await conflict.InitializeAsync(default);
            Assert.ThrowsAsync<IOException>(() => conflict.MigrateAsync(default));
            using var check = new SqliteConnection(otherPath);
            Assert.That(check.Version, Is.EqualTo(1));
            Assert.That(conflict.IsPersistent, Is.False);
        }
        private sealed class BeginFailure : TestDatabase
        {
            public override Task<ConversationTurn> BeginTurnAsync(string id, string text, InteractionSource source, CancellationToken ct) =>
                Task.FromException<ConversationTurn>(new IOException("fault"));
        }
        private sealed class Reply : ILLMService
        {
            public bool IsConfigured => true;
            public int Calls;
            public bool Truncate;
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                Calls++; await Task.Yield(); ct.ThrowIfCancellationRequested(); yield return new LLMChunk("回答");
                if (Truncate) throw new EndOfStreamException();
            }
        }
        [TearDown] public void Cleanup()
        {
            if (!directory.StartsWith(Path.GetFullPath("UserData/Validation") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected cleanup path.");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
