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
    public sealed class RelationshipDiaryTests
    {
        private string folder;
        private DatabaseService database;
        private static CharacterIdentity Card() => new CharacterIdentity("main-character", "Mika", "温和语气", "老师");
        [SetUp] public async Task Begin()
        {
            folder = Path.Combine(Path.GetTempPath(), "GiftDiary-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            database = new DatabaseService(Path.Combine(folder, "memory.db"), "main-character"); await database.InitializeAsync(default); await database.MigrateAsync(default);
        }
        [TearDown] public void End() { database?.Dispose(); Directory.Delete(folder, true); }
        private async Task Seed(int count, int start = 0)
        {
            for (int i = start; i < start + count; i++)
            {
                var turn = await database.BeginTurnAsync("diary-" + i, "共同话题编号" + i + new string('字', 200), i % 2 == 0 ? InteractionSource.Companion : InteractionSource.AgentPet, default);
                await database.CompleteTurnAsync(turn, "回应编号" + i, new CharacterState(), default);
            }
        }
        private sealed class DiaryLLM : IMemoryLLMService
        {
            public int Calls;
            public readonly List<string> Inputs = new List<string>();
            public readonly List<string> Instructions = new List<string>();
            public Func<Task> OnCall;
            public async Task<string> CompleteJsonAsync(string instruction, string input, int maxTokens, CancellationToken ct)
            {
                Calls++; Inputs.Add(input); Instructions.Add(instruction); if (OnCall != null) await OnCall(); ct.ThrowIfCancellationRequested();
                return instruction.Contains("\"paragraphs\"") ? "{\"diary\":\"我们聊过共同话题，也慢慢熟悉彼此说话的方式。\"}" : "{\"summary\":\"按时间回顾共同话题，保留明确表达的感受。\"}";
            }
        }
        private sealed class DiaryTransport : System.Net.Http.HttpMessageHandler
        {
            public readonly List<int> Budgets = new List<int>();
            protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken ct)
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(await request.Content.ReadAsStringAsync()); Budgets.Add((int)json["max_tokens"]);
                string instruction = (string)json["messages"][0]["content"];
                string reply = instruction.Contains("\"paragraphs\"") ? "{\"diary\":\"我们聊过窗边的花。\"}" : "{\"summary\":\"老师与角色聊过窗边的花。\"}";
                string body = Newtonsoft.Json.JsonConvert.SerializeObject(new { choices = new[] { new { finish_reason = "stop", message = new { content = reply } } } });
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json") };
            }
        }
        [Test] public async Task DiaryUsesTheSharedHttpTransportWithItsLargerBoundedOutputBudget()
        {
            await Seed(1); var handler = new DiaryTransport(); using var llm = new LLMService("fake-key", handler: handler);
            var service = new RelationshipDiary(database, llm, Card, folder); await service.InitializeAsync(default);
            var entry = await service.GenerateAsync(null, default);
            Assert.That(entry.Body, Is.EqualTo("我们聊过窗边的花。")); Assert.That(handler.Budgets, Is.EqualTo(new[] { 2800, 5000 }));
            Assert.That(Directory.GetFiles(service.Folder, "*.md").Length, Is.EqualTo(1));
        }
        [Test] public async Task EntireHistoryIsPagedExportedAndReusedAndForgettingRedactsDerivedFiles()
        {
            await Seed(45); var llm = new DiaryLLM(); var service = new RelationshipDiary(database, llm, Card, folder); await service.InitializeAsync(default);
            var entry = await service.GenerateAsync(null, default); Assert.That(entry.TurnCount, Is.EqualTo(45));
            Assert.That(llm.Calls, Is.GreaterThan(2)); Assert.That(llm.Inputs.Any(x => x.Contains("共同话题编号0")), Is.True); Assert.That(llm.Inputs.Any(x => x.Contains("共同话题编号44")), Is.True);
            string file = Directory.GetFiles(Path.Combine(folder, "Diary"), "*.md").Single(); Assert.That(File.ReadAllText(file), Does.Contain(entry.Body));
            int calls = llm.Calls; var again = await service.GenerateAsync(null, default); Assert.That(again.Id, Is.EqualTo(entry.Id)); Assert.That(llm.Calls, Is.EqualTo(calls));
            File.AppendAllText(file, "\n玩家自己保留的批注"); await service.SyncExportsAsync(default);
            Assert.That(File.ReadAllText(file), Does.Contain("玩家自己保留的批注"));
            File.Delete(file); await service.GenerateAsync(null, default);
            Assert.That(File.ReadAllText(file), Does.Contain(entry.Body)); Assert.That(llm.Calls, Is.EqualTo(calls));
            await database.ForgetTurnAsync("diary-0", default); await service.SyncExportsAsync(default);
            Assert.That((await service.ListAsync(default)).Single().Status, Is.EqualTo("needs_review")); Assert.That(File.ReadAllText(file), Does.Not.Contain(entry.Body));
            Assert.That(Directory.GetFiles(Path.Combine(folder, "Diary"), "*.bak"), Is.Empty);
            Assert.That((await database.LoadTurnsAsync(100, false, default)).First().UserText, Does.Contain("共同话题编号0"));
        }
        [Test] public async Task ForgettingDuringGenerationRejectsOldResultAndCancellationLeavesNoDiary()
        {
            await Seed(1); var llm = new DiaryLLM(); bool forgotten = false;
            llm.OnCall = async () => { if (!forgotten) { forgotten = true; await database.ForgetTurnAsync("diary-0", default); } };
            var service = new RelationshipDiary(database, llm, Card, folder); await service.InitializeAsync(default);
            Assert.ThrowsAsync<MemoryContextChangedException>(() => service.GenerateAsync(null, default)); Assert.That(await service.ListAsync(default), Is.Empty);
            await Seed(1, 1); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => service.GenerateAsync(null, cancellation.Token)); Assert.That(await service.ListAsync(default), Is.Empty);
        }
        [Test] public async Task FailedMarkdownExportCanBeRepairedWithoutRegeneratingOrLosingDatabaseEntry()
        {
            await Seed(1); var llm = new DiaryLLM(); var service = new RelationshipDiary(database, llm, Card, folder); await service.InitializeAsync(default);
            File.WriteAllText(service.Folder, "blocks directory creation");
            Assert.CatchAsync<IOException>(() => service.GenerateAsync(null, default)); Assert.That((await service.ListAsync(default)).Count, Is.EqualTo(1));
            int calls = llm.Calls; File.Delete(service.Folder); await service.SyncExportsAsync(default);
            Assert.That(Directory.GetFiles(service.Folder, "*.md").Length, Is.EqualTo(1)); await service.GenerateAsync(null, default); Assert.That(llm.Calls, Is.EqualTo(calls));
        }
        [Test] public async Task KaomojiPolicyAffectsNextRequestedDiaryAndPreservesPreviouslyExportedDiary()
        {
            await Seed(1); var llm = new DiaryLLM(); var service = new RelationshipDiary(database, llm, Card, folder); await service.InitializeAsync(default);
            var first = await service.GenerateAsync(null, default); Assert.That(llm.Instructions.Last(), Does.Contain("最多使用 4 个"));
            string file = Directory.GetFiles(service.Folder, "*.md").Single(); string previous = File.ReadAllText(file);
            var policy = GiftCompanion.Core.Config.AtomicDocument.Read<DiaryPolicy>(service.PolicyFile); policy.MaximumKaomoji = 0;
            GiftCompanion.Core.Config.AtomicDocument.WriteJson(service.PolicyFile, policy, default);
            var second = await service.GenerateAsync(null, default);
            Assert.That(second.Id, Is.Not.EqualTo(first.Id)); Assert.That(llm.Instructions.Last(), Does.Contain("最多使用 0 个"));
            Assert.That(File.ReadAllText(file), Is.EqualTo(previous)); Assert.That((await service.ListAsync(default)).Count, Is.EqualTo(2));
        }
        [Test] public async Task VersionSevenMigrationKeepsConversationsAndBacksUpBeforeAddingDiaries()
        {
            await Seed(1); database.Dispose(); database = null;
            using (var sql = new SqliteConnection(Path.Combine(folder, "memory.db"))) sql.Execute("DROP TABLE agent_tool_runs;DROP TABLE audio_records;DROP TABLE journal_sources;DROP TABLE relationship_journals;PRAGMA user_version=7;");
            database = new DatabaseService(Path.Combine(folder, "memory.db"), "main-character"); await database.InitializeAsync(default); await database.MigrateAsync(default);
            Assert.That(database.SchemaVersion, Is.EqualTo(DatabaseService.CurrentSchemaVersion)); Assert.That((await database.LoadTurnsAsync(10, false, default)).Count, Is.EqualTo(1));
            Assert.That(await database.ListDiariesAsync(default), Is.Empty); Assert.That(Directory.GetFiles(folder, "*.pre-v" + DatabaseService.CurrentSchemaVersion + "-*.bak").Length, Is.GreaterThan(0));
        }
    }
}
