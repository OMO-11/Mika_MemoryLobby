using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class ConversationHistoryTests
    {
        [Test] public async Task EqualTimeCursorPagesBothModesAndPreservesForgottenHistoryAndBodyProjection()
        {
            string directory = Path.GetFullPath("UserData/Validation/history-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "memory.db");
            try
            {
                using var db = new DatabaseService(path, "test"); await db.InitializeAsync(default); await db.MigrateAsync(default);
                for (int i = 0; i < 35; i++)
                {
                    var turn = await db.BeginTurnAsync("round-"+i, "用户"+i, i % 2 == 0 ? InteractionSource.Companion : InteractionSource.AgentPet, default);
                    await db.CompleteTurnAsync(turn, "<META emotion=\"neutral\" action=\"none\">\n回复"+i, new CharacterState(), default, presentation:new DialoguePresentation("回复"+i));
                }
                using (var sql = new SqliteConnection(path)) sql.Execute("UPDATE messages SET created_at='2026-10-14T00:00:00.0000000+00:00';");
                await db.ForgetTurnAsync("round-3", default);
                var service = new ConversationHistoryService(db); var first = await service.ReadPageAsync(null, default); var second = await service.ReadPageAsync(first.Next, default);
                Assert.That(first.Turns.Count, Is.EqualTo(30)); Assert.That(second.Turns.Count, Is.EqualTo(5)); Assert.That(second.Next, Is.Null);
                var all = first.Turns.Concat(second.Turns).ToArray(); Assert.That(all.Select(t => t.RequestId).Distinct().Count(), Is.EqualTo(35));
                Assert.That(all.Select(t => t.Source).Distinct().Count(), Is.EqualTo(2)); Assert.That(all.All(t => !t.AssistantText.Contains("META") && t.RawAssistantText.Contains("META")), Is.True);
                Assert.That(all.Single(t => t.RequestId == "round-3").ExcludedFromMemory, Is.True);
                using var reopened = new DatabaseService(path, "test"); await reopened.InitializeAsync(default); await reopened.MigrateAsync(default);
                var after = await new ConversationHistoryService(reopened).ReadPageAsync(first.Next, default);
                Assert.That(after.Turns.Select(t => t.RequestId), Is.EqualTo(second.Turns.Select(t => t.RequestId)));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
