using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Agent;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class AgentToolTests
    {
        private string folder, path;
        private DatabaseService database;
        private AgentSettingsService settings;
        [SetUp] public async Task Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "GiftAgent-" + Guid.NewGuid().ToString("N")); path = Path.Combine(folder, "memory.db");
            settings = new AgentSettingsService(folder); settings.Load(default);
            database = new DatabaseService(path, "test"); await database.InitializeAsync(default); await database.MigrateAsync(default);
        }
        [TearDown] public void Cleanup() { database?.Dispose(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        private sealed class Platform : IAgentPlatform
        {
            public bool Sensitive, Fail; public int Calls;
            public AgentPreparedAction Prepare(string tool, JObject args) => new AgentPreparedAction { Tool = tool, Arguments = (JObject)args.DeepClone(), Sensitive = Sensitive, Description = "实际目标" };
            public Task<AgentResult> ExecuteAsync(AgentPreparedAction action, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(Fail ? AgentResult.Fail("Missing", "没有找到目标，尚未打开。") : AgentResult.Ok("已发起打开。", new JObject { ["observation"] = "launch_requested" })); }
        }
        private sealed class Model : IAgentModel
        {
            public int Calls; public bool FinalFails; public List<AgentCall> Actions = new List<AgentCall> { new AgentCall { Id = "c1", Name = "get_current_time", Arguments = "{}" } };
            public Task<AgentReply> CompleteAsync(JArray messages, JArray tools, bool finalOnly, Action<string> delta, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested(); Calls++;
                if (!finalOnly) { var reply = new AgentReply { Text = "已经完成（这句必须被拦住）" }; reply.Calls.AddRange(Actions); return Task.FromResult(reply); }
                Assert.That(messages.OfType<JObject>().Any(m => (string)m["role"] == "tool"), Is.True);
                if (FinalFails) throw new IOException("network failure after side effect"); delta?.Invoke("已发起"); delta?.Invoke("打开。"); return Task.FromResult(new AgentReply { Text = "已发起打开。" });
            }
        }
        private async Task<string> Run(Model model, Platform platform, AgentTurnContext turn = null)
        {
            var agent = new AgentConversation(model, platform, database, settings, folder); var text = new StringBuilder();
            await foreach (var chunk in agent.StreamAsync(new DialogueRequest("打开目标", InteractionSource.AgentPet), "request", turn ?? new AgentTurnContext(), default)) text.Append(chunk.Text); return text.ToString();
        }
        [Test] public async Task CompleteNativeCallsExecuteBeforeStreamingAndAuditContainsNoArguments()
        {
            var model = new Model(); var platform = new Platform(); string text = await Run(model, platform);
            Assert.That(text, Is.EqualTo("已发起打开。")); Assert.That(platform.Calls, Is.EqualTo(1)); Assert.That(model.Calls, Is.EqualTo(2));
            Assert.That((await database.ReadToolsAsync(default)).Single().Status, Is.EqualTo("succeeded"));
            string audit = File.ReadAllText(Path.Combine(folder, "Logs", "tool_audit.jsonl")); Assert.That(audit, Does.Not.Contain("实际目标").And.Not.Contain("Arguments").And.Not.Contain("已经完成"));
        }
        [Test] public async Task DenialAndMissingConfirmationNeverExecuteOrClaimSuccess()
        {
            var platform = new Platform { Sensitive = true }; var model = new Model(); string text = await Run(model, platform);
            Assert.That(platform.Calls, Is.Zero); Assert.That(model.Calls, Is.EqualTo(1)); Assert.That(text, Does.Contain("取消").And.Not.Contain("已经完成")); Assert.That((await database.ReadToolsAsync(default)).Single().Status, Is.EqualTo("denied"));
            Assert.That((await database.ReadToolsAsync(default)).Single().Permission, Is.EqualTo("denied"));
        }
        [Test] public async Task ExactSensitivePermissionIsRequiredAndAllowedOnlyOnce()
        {
            int prompts = 0; var platform = new Platform { Sensitive = true };
            await Run(new Model(), platform, new AgentTurnContext { Confirm = (p, ct) => { prompts++; Assert.That(p.Description, Is.EqualTo("实际目标")); return Task.FromResult(true); } });
            Assert.That(prompts, Is.EqualTo(1)); Assert.That(platform.Calls, Is.EqualTo(1));
            Assert.That((await database.ReadToolsAsync(default)).Single().Permission, Is.EqualTo("allowed"));
        }
        [Test] public async Task IdenticalCallsWithDifferentIdsDoNotRepeatSideEffects()
        {
            var model = new Model(); model.Actions.Add(new AgentCall { Id = "c2", Name = "get_current_time", Arguments = "{}" }); var platform = new Platform();
            await Run(model, platform); Assert.That(platform.Calls, Is.EqualTo(1)); Assert.That((await database.ReadToolsAsync(default)).Count, Is.EqualTo(2));
        }
        [Test] public async Task PostExecutionNetworkFailureRetainsActualResultAndNeverRetries()
        {
            var platform = new Platform(); string text = await Run(new Model { FinalFails = true }, platform);
            Assert.That(text, Does.Contain("已发起打开").And.Contain("不会重做")); Assert.That(platform.Calls, Is.EqualTo(1)); Assert.That((await database.ReadToolsAsync(default)).Single().Status, Is.EqualTo("succeeded"));
        }
        [Test] public async Task ExecutionFailureUsesObservedFailureInsteadOfModelSuccess()
        {
            var model = new Model(); string text = await Run(model, new Platform { Fail = true }); Assert.That(text, Does.Contain("没有找到").And.Not.Contain("已经完成")); Assert.That(model.Calls, Is.EqualTo(1));
        }
        [Test] public async Task OversizeBatchIsRejectedBeforeAnyExecution()
        {
            var model = new Model(); model.Actions = Enumerable.Range(0, 5).Select(i => new AgentCall { Id = "c" + i, Name = "get_current_time", Arguments = "{}" }).ToList(); var platform = new Platform();
            await Run(model, platform); Assert.That(platform.Calls, Is.Zero); Assert.That(await database.ReadToolsAsync(default), Is.Empty);
        }
        [TestCase("shell", "{}")]
        [TestCase("get_current_time", "{\"command\":\"anything\"}")]
        [TestCase("set_volume", "{\"percent\":\"100\"}")]
        [TestCase("set_clipboard", "{\"text\":\"one\",\"text\":\"two\"}")]
        public void UnknownAndMalformedArgumentsAreRejected(string tool, string json) => Assert.Throws<ArgumentException>(() => AgentToolCatalog.ValidateArguments(tool, json, settings.Value));
        [Test] public void FragmentedToolCallsAreOnlyCompleteAfterNormalToolFinish()
        {
            var parser = new AgentSseAccumulator(false);
            parser.Feed(JObject.Parse("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"call1\",\"type\":\"function\",\"function\":{\"name\":\"open_application\",\"arguments\":\"{\\\"name\\\":\"}}]}}]}"));
            Assert.Throws<EndOfStreamException>(() => parser.Finish());
            parser.Feed(JObject.Parse("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"记事本\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}"));
            var call = parser.Finish().Calls.Single(); Assert.That(call.Id, Is.EqualTo("call1")); Assert.That((string)JObject.Parse(call.Arguments)["name"], Is.EqualTo("记事本"));
        }
        [Test] public async Task CrashRecoveryMarksUnknownWithoutReplayingAndIdsAreUnique()
        {
            Assert.That(await database.BeginToolAsync("r", "c", "open_application", default), Is.True); Assert.That(await database.BeginToolAsync("r", "c", "open_application", default), Is.False);
            await database.RecoverToolsAsync(default); var row = (await database.ReadToolsAsync(default)).Single(); Assert.That(row.Status, Is.EqualTo("interrupted")); Assert.That(row.ErrorCode, Is.EqualTo("OutcomeUnknown"));
        }
        [Test] public async Task ToolTurnsRemainInHistoryButAreExcludedFromAutomaticMemoryAndDiary()
        {
            var turn = await database.BeginTurnAsync("tool", "临时剪贴板", InteractionSource.AgentPet, default);
            await database.CompleteTurnAsync(turn, "临时结果", new CharacterState(), default, suppressMemory:true);
            var history = (await database.LoadTurnsAsync(10, true, default)).Single(); Assert.That(history.ExcludedFromMemory, Is.True); Assert.That(history.AssistantText, Is.EqualTo("临时结果"));
            Assert.That((await database.RetrieveContextAsync("临时剪贴板", default)).Recent, Is.Empty);
            Assert.That((await database.SnapshotDiaryAsync(default)).Count, Is.Zero);
        }
        [Test] public async Task V9MigrationPreservesHistoryAndUsesNativeBackup()
        {
            var turn = await database.BeginTurnAsync("old", "旧聊天", InteractionSource.Companion, default); await database.CompleteTurnAsync(turn, "保留", new CharacterState(), default); database.Dispose();
            using (var sql = new SqliteConnection(path)) sql.Execute("DROP TABLE agent_tool_runs; PRAGMA user_version=9;");
            database = new DatabaseService(path, "test"); await database.InitializeAsync(default); await database.MigrateAsync(default);
            Assert.That((await database.LoadTurnsAsync(10, true, default)).Single().UserText, Is.EqualTo("旧聊天"));
            using var backup = new SqliteConnection(Directory.GetFiles(folder, "*.pre-v10-*.bak").Single()); Assert.That(backup.Version, Is.EqualTo(9));
        }
        private sealed class CancelFinalModel : IAgentModel
        {
            public readonly TaskCompletionSource<bool> FinalEntered=new TaskCompletionSource<bool>();
            public async Task<AgentReply> CompleteAsync(JArray messages,JArray tools,bool finalOnly,Action<string> delta,CancellationToken ct)
            {
                if(!finalOnly){var reply=new AgentReply();reply.Calls.Add(new AgentCall{Id="cancel-call",Name="get_current_time",Arguments="{}"});return reply;}
                FinalEntered.TrySetResult(true);await Task.Delay(Timeout.Infinite,ct);return new AgentReply();
            }
        }
        [Test] public async Task CancelAfterExecutionRetainsOutcomeAndCommittedLedger()
        {
            var model=new CancelFinalModel();var platform=new Platform();string observed=null;using var scope=new CancellationTokenSource();
            var agent=new AgentConversation(model,platform,database,settings,folder);
            async Task Consume(){await foreach(var unused in agent.StreamAsync(new DialogueRequest("打开目标",InteractionSource.AgentPet),"cancel-request",new AgentTurnContext{OutcomeObserved=value=>observed=value},scope.Token)){} }
            Task request=Consume();await model.FinalEntered.Task;scope.Cancel();
            try{await request;Assert.Fail("Canceled request completed.");}catch(OperationCanceledException){}
            Assert.That(platform.Calls,Is.EqualTo(1));Assert.That(observed,Does.Contain("已发起打开"));Assert.That((await database.ReadToolsAsync(default)).Single().Status,Is.EqualTo("succeeded"));
        }
        [TestCase("cmd.exe")][TestCase("powershell.exe")][TestCase("python.exe")][TestCase("wscript.exe")][TestCase("anything.cmd")]
        public void ApplicationRegistryRejectsCommandAndScriptTargets(string name) => Assert.That(AgentPaths.IsApplication(Path.Combine(folder, name)), Is.False);
        [Test] public void DraggingContinuesWhenPointerReturnsToItsOrigin()
        {
            var gesture=new PetGesture();gesture.Begin(false,100,100,0);
            Assert.That(gesture.Move(120,100,.1f,9,.5f),Is.EqualTo(PetGestureEvent.DragStart));
            Assert.That(gesture.Move(100,100,.2f,9,.5f),Is.EqualTo(PetGestureEvent.DragMove));
            Assert.That(gesture.Release(false),Is.EqualTo(PetGestureEvent.DragEnd));
        }
    }
}
