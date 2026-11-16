using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Agent;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService : IAgentToolStore
    {
        private const string AgentSchema = @"CREATE TABLE agent_tool_runs(
 character_id TEXT NOT NULL,request_id TEXT NOT NULL,call_id TEXT NOT NULL,tool TEXT NOT NULL,
 status TEXT NOT NULL,permission TEXT NOT NULL DEFAULT 'not_required',error_code TEXT,elapsed_ms INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL,
 PRIMARY KEY(request_id,call_id)); CREATE INDEX agent_tool_character ON agent_tool_runs(character_id,created_at);";
        public Task<bool> BeginToolAsync(string requestId, string callId, string tool, CancellationToken ct) => Serialized(() =>
        {
            RequireReady();
            using (var exists = connection.Prepare("SELECT 1 FROM agent_tool_runs WHERE request_id=?1 AND call_id=?2;")) { exists.Bind(1, requestId); exists.Bind(2, callId); if (exists.Next()) return false; }
            using var insert = connection.Prepare("INSERT INTO agent_tool_runs(character_id,request_id,call_id,tool,status,created_at) VALUES(?1,?2,?3,?4,'started',?5);");
            insert.Bind(1, characterId); insert.Bind(2, requestId); insert.Bind(3, callId); insert.Bind(4, tool); insert.Bind(5, DateTimeOffset.UtcNow.ToString("O")); insert.Done(); return true;
        }, ct);
        public Task SetToolPermissionAsync(string requestId, string callId, string permission, CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); if (permission != "requested" && permission != "allowed" && permission != "denied" && permission != "reused_result") throw new ArgumentException("Invalid permission decision.");
            using var update = connection.Prepare("UPDATE agent_tool_runs SET permission=?1 WHERE character_id=?2 AND request_id=?3 AND call_id=?4 AND status='started';");
            update.Bind(1, permission); update.Bind(2, characterId); update.Bind(3, requestId); update.Bind(4, callId); update.Done();
        }, ct);
        public Task FinishToolAsync(string requestId, string callId, string status, string code, long milliseconds, CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); if (status != "succeeded" && status != "failed" && status != "denied" && status != "interrupted") throw new ArgumentException("Invalid tool status.");
            using var update = connection.Prepare("UPDATE agent_tool_runs SET status=?1,error_code=?2,elapsed_ms=?3 WHERE character_id=?4 AND request_id=?5 AND call_id=?6 AND status='started';");
            update.Bind(1, status); update.Bind(2, code ?? ""); update.Bind(3, milliseconds.ToString()); update.Bind(4, characterId); update.Bind(5, requestId); update.Bind(6, callId); update.Done();
        }, ct);
        public Task RecoverToolsAsync(CancellationToken ct) => Serialized(() =>
        {
            RequireReady(); using var update = connection.Prepare("UPDATE agent_tool_runs SET status='interrupted',error_code='OutcomeUnknown' WHERE character_id=?1 AND status='started';"); update.Bind(1, characterId); update.Done();
        }, ct);
        public Task<IReadOnlyList<AgentAudit>> ReadToolsAsync(CancellationToken ct) => Serialized<IReadOnlyList<AgentAudit>>(() =>
        {
            RequireReady(); var values = new List<AgentAudit>(); using var query = connection.Prepare("SELECT request_id,call_id,tool,status,error_code,elapsed_ms,created_at,permission FROM agent_tool_runs WHERE character_id=?1 ORDER BY rowid DESC LIMIT 200;"); query.Bind(1, characterId);
            while (query.Next()) values.Add(new AgentAudit { RequestId = query.Text(0), CallId = query.Text(1), Tool = query.Text(2), Status = query.Text(3), ErrorCode = query.Text(4), ElapsedMs = query.Integer(5), At = query.Text(6), Permission = query.Text(7) }); return values;
        }, ct);
    }
}
