using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;
using Newtonsoft.Json;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService
    {
        private const string SummarySchema=@"
ALTER TABLE sessions ADD COLUMN summary_sources TEXT NOT NULL DEFAULT '[]';
ALTER TABLE sessions ADD COLUMN summary_memory_revision INTEGER NOT NULL DEFAULT 0;
CREATE INDEX memory_sources_message ON memory_sources(message_id);
CREATE INDEX prompt_dependencies_source ON prompt_dependencies(source_message_id);";

        private void ConsiderSummary(string sessionId,bool force)
        {
            long count,length;
            using(var q=connection.Prepare(@"SELECT count(*),coalesce(sum(length(u.content)+length(a.content)),0) FROM messages u
                JOIN messages a ON a.request_id=u.request_id AND a.role='assistant' JOIN sessions s ON s.id=u.session_id
                WHERE u.session_id=?1 AND u.role='user' AND u.status='completed' AND u.rowid>s.summary_through
                AND NOT EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=u.id OR e.message_id=a.id);"))
            {q.Bind(1,sessionId);q.Row();count=q.Integer(0);length=q.Integer(1);}
            if(count==0||(!force&&count<10&&length<8000))return;
            using var update=connection.Prepare("UPDATE sessions SET summary_status='pending' WHERE id=?1 AND summary_status IN ('none','succeeded','pending');");
            update.Bind(1,sessionId);update.Done();
        }
        public Task<SummaryJob> ClaimSummaryAsync(CancellationToken ct)=>Serialized(() =>
        {
            RequireReady();SummaryJob result=null;
            Transaction(() =>
            {
                while(true)
                {
                    var job=new SummaryJob {MemoryRevision=ReadRevision()};
                    using(var q=connection.Prepare(@"SELECT id,summary,summary_through,summary_revision,summary_sources FROM sessions
                        WHERE character_id=?1 AND summary_status='pending' ORDER BY started_at,id LIMIT 1;"))
                    {
                        q.Bind(1,characterId);if(!q.Next())break;
                        job.SessionId=q.Text(0);job.PreviousSummary=q.Text(1);job.ThroughBefore=q.Integer(2);job.SessionRevision=q.Integer(3);
                        job.SourceIds.AddRange(JsonConvert.DeserializeObject<List<string>>(q.Text(4))??new List<string>());
                    }
                    int size=job.PreviousSummary.Length;
                    using(var q=connection.Prepare(TurnSelect+@" WHERE s.id=?1 AND u.role='user' AND u.status='completed' AND u.rowid>?2
                        AND NOT EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=u.id OR e.message_id=a.id)
                        ORDER BY u.rowid LIMIT 20;"))
                    {
                        q.Bind(1,job.SessionId);q.Bind(2,job.ThroughBefore.ToString());
                        while(q.Next())
                        {
                            var turn=ReadTurn(q);
                            turn.UserText=SummaryExcerpt(turn.UserText);turn.AssistantText=SummaryExcerpt(turn.AssistantText);
                            int added=turn.UserText.Length+turn.AssistantText.Length;
                            if(size+added>8000&&job.Turns.Count>0)break;
                            size+=added;job.Turns.Add(turn);job.ThroughAfter=MessageOrder(turn.UserMessageId);
                            job.SourceIds.Add(turn.UserMessageId);job.SourceIds.Add(turn.AssistantMessageId);
                        }
                    }
                    if(job.Turns.Count==0)
                    {
                        using var empty=connection.Prepare("UPDATE sessions SET summary_status=CASE WHEN summary='' THEN 'none' ELSE 'succeeded' END WHERE id=?1;");
                        empty.Bind(1,job.SessionId);empty.Done();continue;
                    }
                    UpdateSummaryStatus(job.SessionId,"running");result=job;break;
                }
            },ct);return result;
        },ct);
        public Task<bool> ApplySummaryAsync(SummaryJob job,string summary,CancellationToken ct)
        {
            if(job==null||string.IsNullOrWhiteSpace(summary)||summary.Length>1000)throw new ArgumentException("Invalid summary.");
            return Serialized(() =>
            {
                RequireReady();bool applied=false;
                Transaction(() =>
                {
                    string status;long revision,through;bool closed;
                    using(var q=connection.Prepare("SELECT summary_status,summary_revision,summary_through,status FROM sessions WHERE id=?1 AND character_id=?2;"))
                    {q.Bind(1,job.SessionId);q.Bind(2,characterId);q.Row();status=q.Text(0);revision=q.Integer(1);through=q.Integer(2);closed=q.Text(3)!="active";}
                    if(status!="running")return;
                    if(revision!=job.SessionRevision||through!=job.ThroughBefore||ReadRevision()!=job.MemoryRevision||job.SourceIds.Any(IsExcluded))
                    {UpdateSummaryStatus(job.SessionId,"pending");return;}
                    using(var q=connection.Prepare(@"UPDATE sessions SET summary=?1,summary_through=?2,summary_sources=?3,
                        summary_memory_revision=?4,summary_status='succeeded' WHERE id=?5;"))
                    {
                        q.Bind(1,summary);q.Bind(2,job.ThroughAfter.ToString());q.Bind(3,JsonConvert.SerializeObject(job.SourceIds.Distinct()));
                        q.Bind(4,job.MemoryRevision.ToString());q.Bind(5,job.SessionId);q.Done();
                    }
                    ConsiderSummary(job.SessionId,closed);applied=true;
                },ct);return applied;
            },ct);
        }
        public Task SetSummaryStatusAsync(string sessionId,string status,CancellationToken ct)
        {
            if(status!="pending"&&status!="failed")throw new ArgumentException("Invalid summary status.");
            return Serialized(() =>
            {
                RequireReady();using var q=connection.Prepare("UPDATE sessions SET summary_status=?1 WHERE id=?2 AND character_id=?3 AND summary_status='running';");
                q.Bind(1,status);q.Bind(2,sessionId);q.Bind(3,characterId);q.Done();
            },ct);
        }
        private void UpdateSummaryStatus(string id,string status)
        {using var q=connection.Prepare("UPDATE sessions SET summary_status=?1 WHERE id=?2;");q.Bind(1,status);q.Bind(2,id);q.Done();}
        private static string SummaryExcerpt(string text)
        {
            if(text.Length<=3000)return text;
            int count=char.IsHighSurrogate(text[2999])?2999:3000;
            return text.Substring(0,count)+"[原文后续未纳入本次摘要]";
        }
        private void InvalidateAllSummaries()
        {
            InvalidateDiaries();
            var sessions=new List<string>();
            using(var q=connection.Prepare("SELECT id FROM sessions WHERE character_id=?1;"))
            {q.Bind(1,characterId);while(q.Next())sessions.Add(q.Text(0));}
            using(var q=connection.Prepare(@"UPDATE sessions SET summary='',summary_sources='[]',summary_status='none',summary_through=0,
                summary_revision=summary_revision+1 WHERE character_id=?1;"))
            {q.Bind(1,characterId);q.Done();}
            foreach(string id in sessions)ConsiderSummary(id,true);
        }
    }
}
