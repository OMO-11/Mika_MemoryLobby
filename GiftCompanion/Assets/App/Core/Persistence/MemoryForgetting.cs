using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService
    {
        public Task ForgetRecordAsync(string kind,string id,CancellationToken ct)
        {
            string table=MemoryTable(kind);
            return Serialized(() =>
            {
                RequireReady();Transaction(() =>
                {
                    string key;
                    using(var q=connection.Prepare("SELECT dedup_key FROM "+table+" WHERE id=?1 AND character_id=?2;"))
                    {q.Bind(1,id);q.Bind(2,characterId);if(!q.Next())throw new InvalidDataException("Memory target is missing.");key=q.Text(0);}
                    var ids=new List<string>();
                    using(var q=connection.Prepare("SELECT id FROM "+table+" WHERE dedup_key=?1 AND character_id=?2;"))
                    {q.Bind(1,key);q.Bind(2,characterId);while(q.Next())ids.Add(q.Text(0));}
                    foreach(string target in ids)
                    {
                        MarkRecord(kind,target,"forgotten");
                        InvalidateSources(kind,target,"forgotten");
                    }
                },ct);
            },ct);
        }
        public Task ForgetTurnAsync(string requestId,CancellationToken ct)=>Serialized(() =>
        {
            RequireReady();Transaction(() =>
            {
                var turn=FindTurn(requestId)??throw new InvalidDataException("Turn target is missing.");
                var affected=new List<Tuple<string,string>>();
                using(var q=connection.Prepare("SELECT kind,record_id FROM memory_sources WHERE message_id=?1;"))
                {q.Bind(1,turn.UserMessageId);while(q.Next())affected.Add(Tuple.Create(q.Text(0),q.Text(1)));}
                foreach(var record in affected){MarkRecord(record.Item1,record.Item2,"forgotten");InvalidateSources(record.Item1,record.Item2,"forgotten");}
                ExcludeMessage(turn.UserMessageId,"forgotten");
                PropagateExclusions();SuppressExcludedTasks();BumpRevision();InvalidateAllSummaries();
            },ct);
        },ct);
        private void MarkRecord(string kind,string id,string status)
        {
            using var q=connection.Prepare("UPDATE "+MemoryTable(kind)+" SET status=?1 WHERE id=?2 AND character_id=?3;");
            q.Bind(1,status);q.Bind(2,id);q.Bind(3,characterId);q.Done();RemoveSearchRecord(kind,id);
            ExcludeRecordReplies(kind,id,status);
        }
        private void ExcludeRecordReplies(string kind,string id,string reason)
        {
            using var q=connection.Prepare(@"INSERT OR IGNORE INTO excluded_messages(message_id,reason)
                SELECT assistant_message_id,?3 FROM prompt_record_dependencies WHERE kind=?1 AND record_id=?2;");
            q.Bind(1,kind);q.Bind(2,id);q.Bind(3,reason);q.Done();
        }
        private void ExcludeMessage(string id,string reason)
        {using var q=connection.Prepare("INSERT OR IGNORE INTO excluded_messages(message_id,reason) VALUES(?1,?2);");q.Bind(1,id);q.Bind(2,reason);q.Done();}
        private void PropagateExclusions()
        {
            connection.Execute(@"WITH RECURSIVE affected(id,reason) AS (
                SELECT message_id,reason FROM excluded_messages UNION
                SELECT p.assistant_message_id,a.reason FROM prompt_dependencies p JOIN affected a ON p.source_message_id=a.id)
                INSERT OR IGNORE INTO excluded_messages(message_id,reason) SELECT id,reason FROM affected;");
        }
        private void SuppressExcludedTasks()
        {
            using var q=connection.Prepare(@"UPDATE messages SET extraction_status='skipped' WHERE role='user'
                AND extraction_status IN ('pending','running','failed') AND session_id IN (SELECT id FROM sessions WHERE character_id=?1)
                AND (EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=messages.id)
                OR EXISTS(SELECT 1 FROM messages a JOIN excluded_messages e ON e.message_id=a.id WHERE a.request_id=messages.request_id AND a.role='assistant'));");
            q.Bind(1,characterId);q.Done();
        }
        private void InvalidateRelatedRecords(string kind,string id,string reason)
        {
            string oldContent;
            using(var q=connection.Prepare("SELECT "+(kind=="fact"?"value":"content")+" FROM "+MemoryTable(kind)+" WHERE id=?1;"))
            {q.Bind(1,id);q.Row();oldContent=q.Text(0);}
            var pending=new Queue<string>();var visited=new HashSet<string>();
            void AddSources(string recordKind,string recordId)
            {
                using var q=connection.Prepare("SELECT message_id FROM memory_sources WHERE kind=?1 AND record_id=?2;");
                q.Bind(1,recordKind);q.Bind(2,recordId);while(q.Next())if(visited.Add(q.Text(0)))pending.Enqueue(q.Text(0));
            }
            AddSources(kind,id);
            while(pending.Count>0)
            {
                string sourceId=pending.Dequeue();ExcludeMessage(sourceId,reason);
                foreach(string otherKind in new[]{"fact","memory","loop"})
                {
                    var affected=new List<string>();
                    using(var q=connection.Prepare(RecordSelect(otherKind,MemoryTable(otherKind))+@" WHERE character_id=?1 AND status='active' AND id IN (
                        SELECT record_id FROM memory_sources WHERE message_id=?2 AND kind=?3);"))
                    {
                        q.Bind(1,characterId);q.Bind(2,sourceId);q.Bind(3,otherKind);
                        while(q.Next())
                        {
                            var record=ReadRecord(q,otherKind);
                            if(record.Id!=id&&(otherKind=="memory"||record.Content.Contains(oldContent)||oldContent.Contains(record.Content)))affected.Add(record.Id);
                        }
                    }
                    foreach(string related in affected){MarkRecord(otherKind,related,reason);AddSources(otherKind,related);}
                }
            }
        }
    }
}
