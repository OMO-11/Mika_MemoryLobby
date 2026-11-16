using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Memory;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService : IConversationHistoryStore
    {
        public Task<HistoryPage> ReadHistoryAsync(HistoryCursor cursor, int pageSize, CancellationToken ct)
        {
            if (pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
            if (cursor != null && (string.IsNullOrEmpty(cursor.CreatedAt) || string.IsNullOrEmpty(cursor.UserMessageId))) throw new ArgumentException("Invalid history cursor.");
            return Serialized(() =>
            {
                RequireReady(); var rows = new List<ConversationTurn>();
                using var query = connection.Prepare(TurnSelect + @" WHERE s.character_id=?1 AND u.role='user'" +
                    (cursor == null ? "" : " AND (u.created_at<?2 OR (u.created_at=?2 AND u.id<?3))") +
                    " ORDER BY u.created_at DESC,u.id DESC LIMIT ?4;");
                query.Bind(1, characterId); if (cursor != null) { query.Bind(2, cursor.CreatedAt); query.Bind(3, cursor.UserMessageId); }
                query.Bind(4, (pageSize + 1).ToString()); while (query.Next()) rows.Add(ReadTurn(query));
                HistoryCursor next = null;
                if (rows.Count > pageSize)
                { rows.RemoveAt(rows.Count - 1); var last = rows[rows.Count - 1]; next = new HistoryCursor(last.CreatedAt.ToString("O"), last.UserMessageId); }
                return new HistoryPage(rows, next);
            }, ct);
        }
    }
}
