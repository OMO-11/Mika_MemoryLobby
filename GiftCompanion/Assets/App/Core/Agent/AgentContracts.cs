using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.LLM;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.Agent
{
    public sealed class AgentTurnContext
    {
        public Func<AgentPermission, CancellationToken, Task<bool>> Confirm { get; set; }
        public Action<string> StateChanged { get; set; }
        public Action<string> OutcomeObserved { get; set; }
    }
    public sealed class AgentPermission
    {
        public string Tool { get; set; }
        public string Description { get; set; }
    }
    public sealed class AgentCall
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Arguments { get; set; }
        public JObject ToJson() => new JObject { ["id"] = Id, ["type"] = "function", ["function"] = new JObject { ["name"] = Name, ["arguments"] = Arguments } };
    }
    public sealed class AgentReply
    {
        public string Text { get; set; } = "";
        public List<AgentCall> Calls { get; } = new List<AgentCall>();
    }
    public sealed class AgentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorCode { get; set; }
        public JObject Data { get; set; } = new JObject();
        public static AgentResult Ok(string message, JObject data = null) => new AgentResult { Success = true, Message = message, Data = data ?? new JObject() };
        public static AgentResult Fail(string code, string message) => new AgentResult { ErrorCode = code, Message = message };
        public JObject ToJson() => new JObject { ["success"] = Success, ["message"] = Message, ["errorCode"] = ErrorCode, ["data"] = Data };
    }
    public sealed class AgentPreparedAction
    {
        public string Tool { get; set; }
        public JObject Arguments { get; set; }
        public string Description { get; set; }
        public bool Sensitive { get; set; }
    }
    public interface IAgentPlatform
    {
        AgentPreparedAction Prepare(string tool, JObject arguments);
        Task<AgentResult> ExecuteAsync(AgentPreparedAction action, CancellationToken ct);
    }
    public interface IAgentModel
    {
        Task<AgentReply> CompleteAsync(JArray messages, JArray tools, bool finalOnly, Action<string> delta, CancellationToken ct);
    }
    public interface IAgentConversation
    {
        IAsyncEnumerable<LLMChunk> StreamAsync(DialogueRequest request, string requestId, AgentTurnContext turn, CancellationToken ct);
    }
    public sealed class AgentAudit
    {
        public string RequestId { get; set; }
        public string CallId { get; set; }
        public string Tool { get; set; }
        public string Status { get; set; }
        public string Permission { get; set; }
        public string ErrorCode { get; set; }
        public long ElapsedMs { get; set; }
        public string At { get; set; }
    }
    public interface IAgentToolStore
    {
        Task<bool> BeginToolAsync(string requestId, string callId, string tool, CancellationToken ct);
        Task SetToolPermissionAsync(string requestId, string callId, string permission, CancellationToken ct);
        Task FinishToolAsync(string requestId, string callId, string status, string code, long milliseconds, CancellationToken ct);
        Task RecoverToolsAsync(CancellationToken ct);
        Task<IReadOnlyList<AgentAudit>> ReadToolsAsync(CancellationToken ct);
    }
}
