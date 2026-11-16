using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;
using Newtonsoft.Json;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService
    {
        private const string SearchSchema = @"CREATE VIRTUAL TABLE memory_search USING fts5(
            character_id UNINDEXED,kind UNINDEXED,record_id UNINDEXED,body,terms, tokenize='unicode61 remove_diacritics 2');";
        public Task<MemoryContext> RetrieveContextAsync(string text,CancellationToken ct) => Serialized(() =>
        {
            RequireReady();var context=new MemoryContext {Revision=ReadRevision()};
            var ranked=SearchRecords(text);
            context.Facts.AddRange(SelectRecords("fact",20,ranked));
            context.Memories.AddRange(ranked.Where(x=>x.Kind=="memory").Take(5));
            context.Loops.AddRange(SelectRecords("loop",5,ranked));
            using(var recent=connection.Prepare(TurnSelect+@" WHERE s.character_id=?1 AND u.role='user' AND u.status='completed'
                AND NOT EXISTS(SELECT 1 FROM excluded_messages e WHERE e.message_id=u.id OR e.message_id=a.id)
                ORDER BY u.rowid DESC LIMIT 6;"))
            {recent.Bind(1,characterId);while(recent.Next())context.Recent.Add(ReadTurn(recent));context.Recent.Reverse();}
            if(activeSessionId!=null)
            {
                using var summary=connection.Prepare("SELECT summary,summary_sources FROM sessions WHERE id=?1 AND summary_memory_revision=?2;");
                summary.Bind(1,activeSessionId);summary.Bind(2,context.Revision.ToString());
                if(summary.Next())
                {
                    var ids=JsonConvert.DeserializeObject<List<string>>(summary.Text(1))??new List<string>();
                    if(!ids.Any(IsExcluded)){context.Summary=summary.Text(0);context.SummarySources.AddRange(ids);}
                }
            }
            return context;
        },ct);
        private List<MemoryRecord> SearchRecords(string text)
        {
            string expression=SearchExpression(text);var found=new List<MemoryRecord>();var rawRanks=new Dictionary<string,double>();
            if(expression.Length==0) return found;
            using(var q=connection.Prepare(@"SELECT kind,record_id,bm25(memory_search) FROM memory_search
                WHERE memory_search MATCH ?1 AND character_id=?2 ORDER BY bm25(memory_search),record_id LIMIT 120;"))
            {
                q.Bind(1,expression);q.Bind(2,characterId);
                while(q.Next())
                {
                    string kind=q.Text(0),id=q.Text(1);double rank=q.Real(2);
                    using var record=connection.Prepare(RecordSelect(kind,MemoryTable(kind))+" WHERE id=?1 AND character_id=?2 AND status='active';");
                    record.Bind(1,id);record.Bind(2,characterId);
                    if(record.Next()){found.Add(ReadRecord(record,kind));rawRanks[id]=Math.Max(0,-rank);}
                }
            }
            double max=rawRanks.Count==0?1:Math.Max(double.Epsilon,rawRanks.Values.Max());
            var now=DateTimeOffset.UtcNow;
            foreach(var item in found)
            {
                double relevance=rawRanks[item.Id]/max;
                double recency=Math.Exp(-Math.Max(0,(now-item.CreatedAt).TotalDays)/30.0);
                item.Score=.60*relevance+.25*item.Importance+.15*recency;
            }
            return found.OrderByDescending(x=>x.Score).ThenByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
        }
        private List<MemoryRecord> SelectRecords(string kind,int cap,List<MemoryRecord> ranked)
        {
            var result=ranked.Where(x=>x.Kind==kind).Take(cap).ToList();
            using var q=connection.Prepare(RecordSelect(kind,MemoryTable(kind))+" WHERE character_id=?1 AND status='active' ORDER BY source_order DESC,id LIMIT ?2;");
            q.Bind(1,characterId);q.Bind(2,cap.ToString());
            while(q.Next()&&result.Count<cap) {var record=ReadRecord(q,kind);if(result.All(x=>x.Id!=record.Id))result.Add(record);}
            return result;
        }
        private void RebuildSearchIndex()
        {
            var records=new List<MemoryRecord>();
            foreach(string kind in new[]{"fact","memory","loop"})
            {
                using var q=connection.Prepare(RecordSelect(kind,MemoryTable(kind))+" WHERE status='active';");
                while(q.Next())records.Add(ReadRecord(q,kind));
            }
            foreach(var record in records) IndexRecord(record.Kind,record.Id);
        }
        private void IndexRecord(string kind,string id)
        {
            RemoveSearchRecord(kind,id);
            using var q=connection.Prepare(RecordSelect(kind,MemoryTable(kind))+" WHERE id=?1 AND status='active';");q.Bind(1,id);
            if(!q.Next())return;
            var record=ReadRecord(q,kind);
            string owner;
            using(var row=connection.Prepare("SELECT character_id FROM "+MemoryTable(kind)+" WHERE id=?1;"))
            {row.Bind(1,id);row.Row();owner=row.Text(0);}
            string body=record.Key+" "+record.Content;
            using var insert=connection.Prepare("INSERT INTO memory_search(character_id,kind,record_id,body,terms) VALUES(?1,?2,?3,?4,?5);");
            insert.Bind(1,owner);insert.Bind(2,kind);insert.Bind(3,id);insert.Bind(4,body);insert.Bind(5,string.Join(" ",SearchTokens(body,true)));insert.Done();
        }
        private void RemoveSearchRecord(string kind,string id)
        {using var q=connection.Prepare("DELETE FROM memory_search WHERE kind=?1 AND record_id=?2;");q.Bind(1,kind);q.Bind(2,id);q.Done();}
        internal static string SearchExpression(string text) => string.Join(" OR ",SearchTokens(text.Length>256?text.Substring(0,256):text,false).Take(24).Select(x=>"\""+x.Replace("\"","\"\"")+"\""));
        private static IEnumerable<string> SearchTokens(string text,bool index)
        {
            var result=new HashSet<string>(StringComparer.Ordinal);
            foreach(Match match in Regex.Matches(NormalizeMemory(text),@"[\p{L}\p{N}]+"))
            {
                // Preserve Latin identifiers embedded directly in a Chinese/Japanese sentence.
                foreach(string word in Regex.Split(match.Value,@"([\u3040-\u9fff\uf900-\ufaff]+)"))
                {
                    if(word.Length==0)continue;
                    bool cjk=word.Any(c=>(c>=0x3040&&c<=0x9fff)||(c>=0xf900&&c<=0xfaff));
                    if(!cjk) {result.Add(word);continue;}
                    if(word.Length==1||index) foreach(char c in word) result.Add(c.ToString());
                    for(int i=0;i+1<word.Length;i++) result.Add(word.Substring(i,2));
                }
            }
            return result.OrderBy(x=>x,StringComparer.Ordinal);
        }
    }
}
