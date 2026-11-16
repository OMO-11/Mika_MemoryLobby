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
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class CompanionProtocolTests
    {
        [Test] public void CompositionAndItsCommitFrameDoNotSubmit()
        {
            var guard = new CompanionInputGuard(); Assert.That(guard.CanSubmit(1), Is.True);
            guard.Observe("老师", 2); Assert.That(guard.CanSubmit(2), Is.False);
            guard.Observe("", 3); Assert.That(guard.CanSubmit(3), Is.False); Assert.That(guard.CanSubmit(4), Is.False);
            Assert.That(guard.CanSubmit(5), Is.True);
        }
        [Test] public void EverySplitPreservesBodyAndAppliesOnlyInitialMetadata()
        {
            const string wire = "<META emotion=\"happy\" action=\"none\">\r\n老师，中文 🌙\n正文里的 <META emotion=\"sad\" action=\"none\"> 只是文字。";
            string body = wire.Substring(wire.IndexOf('\n') + 1);
            for (int split = 0; split <= wire.Length; split++)
            {
                var parser = new CompanionDialogueParser();
                Assert.That(parser.Feed(wire.Substring(0, split)) + parser.Feed(wire.Substring(split)) + parser.Finish(), Is.EqualTo(body));
                Assert.That(parser.Emotion, Is.EqualTo(CharacterEmotion.Happy));
            }
            var chars = new CompanionDialogueParser();
            Assert.That(string.Concat(wire.Select(c => chars.Feed(c.ToString()))) + chars.Finish(), Is.EqualTo(body));
        }
        [Test] public void InvalidUnknownAndOversizedHeadersFallBackWithoutLeakingControlText()
        {
            foreach (string wire in new[] {"<META emotion=\"invented\" action=\"shell\">\n正文", "<META broken>\n正文", "<META" + new string('x', 400) + "\n正文", "<META broken\n正文", "正文"})
            {
                var parser = new CompanionDialogueParser();
                Assert.That(parser.Feed(wire) + parser.Finish(), Is.EqualTo("正文"));
                Assert.That(parser.Emotion, Is.EqualTo(CharacterEmotion.Neutral)); Assert.That(parser.Action, Is.EqualTo("none"));
            }
            var literal = new CompanionDialogueParser(); Assert.That(literal.Feed("<hello>正常正文") + literal.Finish(), Is.EqualTo("<hello>正常正文"));
        }
        [Test] public async Task CoreCommitsRawBodyMoodAndDependenciesTogetherAndProjectsBackgroundInputs()
        {
            string folder = Path.GetFullPath("UserData/Validation/protocol-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                using var db = new DatabaseService(Path.Combine(folder, "memory.db"), "test"); await db.InitializeAsync(default); await db.MigrateAsync(default);
                var core = new CharacterCore(new CharacterIdentity("test", "Mika", "", ""), new CharacterState(), db, new Reply(), new MemoryService());
                var result = await core.SendMessageAsync("测试原文", InteractionSource.Companion, default);
                Assert.That(result.Success, Is.True); Assert.That(result.Text, Is.EqualTo("老师，测试回复。")); Assert.That(core.State.Mood, Is.EqualTo(CharacterEmotion.Shy));
                var turn = (await db.LoadTurnsAsync(10, true, default)).Single();
                Assert.That(turn.RawAssistantText, Does.StartWith("<META")); Assert.That(turn.AssistantText, Is.EqualTo(result.Text));
                Assert.That((await db.ClaimExtractionAsync(default)).Turn.AssistantText, Is.EqualTo(result.Text));
                Assert.That((await db.RetrieveContextAsync("测试", default)).Recent.Single().AssistantText, Is.EqualTo(result.Text));
                Assert.That((await db.LoadCharacterStateAsync(default)).Mood, Is.EqualTo(CharacterEmotion.Shy));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        [Test] public async Task V6MigrationRetainsLiteralLegacyHeadersAndBacksUpBeforeAddingProjection()
        {
            string folder = Path.GetFullPath("UserData/Validation/protocol-upgrade-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "memory.db"); const string literal = "<META 这是旧的普通正文>";
            try
            {
                using (var db = new DatabaseService(path, "test"))
                { await db.InitializeAsync(default); await db.MigrateAsync(default); var turn = await db.BeginTurnAsync("legacy", "旧原文", InteractionSource.Companion, default); await db.CompleteTurnAsync(turn, literal, new CharacterState(), default); }
                using (var sql = new SqliteConnection(path)) sql.Execute("DROP TABLE agent_tool_runs;DROP TABLE audio_records;DROP TABLE journal_sources; DROP TABLE relationship_journals; DROP TABLE assistant_presentations; DROP INDEX history_cursor; PRAGMA user_version=6;");
                using (var db = new DatabaseService(path, "test"))
                { await db.InitializeAsync(default); await db.MigrateAsync(default); Assert.That((await db.LoadTurnsAsync(10, true, default)).Single().AssistantText, Is.EqualTo(literal)); }
                using var backup = new SqliteConnection(Directory.GetFiles(folder, "*.pre-v" + DatabaseService.CurrentSchemaVersion + "-*.bak").Single()); Assert.That(backup.Version, Is.EqualTo(6));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        [TestCase(false)] [TestCase(true)] public async Task FailedOrCanceledPresentationCannotChangeSavedMoodOrCreateAssistant(bool cancel)
        {
            string directory = Path.GetFullPath("UserData/Validation/presentation-fault-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "memory.db");
            try
            {
                using var db = new DatabaseService(path, "test"); await db.InitializeAsync(default); await db.MigrateAsync(default);
                var state = new CharacterState { Mood = CharacterEmotion.Happy }; await db.SaveCharacterStateAsync(state, default);
                if (!cancel) { using var sql = new SqliteConnection(path); sql.Execute("CREATE TRIGGER presentation_fault BEFORE INSERT ON app_state BEGIN SELECT RAISE(ABORT,'fault'); END;"); }
                var core = new CharacterCore(new CharacterIdentity("test", "Mika", "", ""), state, db, new Reply(), new MemoryService());
                using var cancellation = new CancellationTokenSource(); DialogueResult result = null;
                await foreach (var chunk in core.StreamMessageAsync("故障验收", InteractionSource.Companion, cancellation.Token))
                { if (!chunk.IsFinal && chunk.Text.Length > 0 && cancel) cancellation.Cancel(); if (chunk.IsFinal) result = chunk.Result; }
                Assert.That(result.Success, Is.False); Assert.That(result.ErrorCode, Is.EqualTo(cancel ? "Canceled" : "StorageFailed"));
                Assert.That(core.State.Mood, Is.EqualTo(CharacterEmotion.Happy)); Assert.That((await db.LoadCharacterStateAsync(default)).Mood, Is.EqualTo(CharacterEmotion.Happy));
                Assert.That((await db.LoadTurnsAsync(10, false, default)).Single().AssistantText, Is.Empty);
                using var check = new SqliteConnection(path); using var count = check.Prepare("SELECT count(*) FROM assistant_presentations;"); count.Row(); Assert.That(count.Integer(0), Is.Zero);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
        private sealed class Reply : ILLMService
        {
            public bool IsConfigured => true;
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            { yield return new LLMChunk("<META emotion=\"shy\" action=\"none\">\n老师，"); await Task.Yield(); yield return new LLMChunk("测试回复。"); }
        }
    }
}
