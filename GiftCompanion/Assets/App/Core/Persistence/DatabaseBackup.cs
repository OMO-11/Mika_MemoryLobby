using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GiftCompanion.Core.Persistence
{
    public sealed partial class DatabaseService
    {
        public Task BackupAsync(string destination, CancellationToken ct) => Serialized(() =>
        { RequireReady(); ct.ThrowIfCancellationRequested(); connection.Backup(destination); }, ct);

        public static void VerifyBackup(string path, int schema, string characterId)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 100) throw new InvalidDataException("备份数据库缺失。");
            using var db = new SqliteConnection(path, true);
            if (schema < 1 || schema > CurrentSchemaVersion || db.Version != schema) throw new InvalidDataException("数据库版本不兼容，请使用对应的新版本程序。");
            if (schema == CurrentSchemaVersion) ValidateSchema(db);
            using (var check = db.Prepare("PRAGMA integrity_check;"))
                if (!check.Next() || check.Text(0) != "ok" || check.Next()) throw new InvalidDataException("备份数据库完整性校验未通过。");
            using (var check = db.Prepare("PRAGMA foreign_key_check;"))
                if (check.Next()) throw new InvalidDataException("备份数据引用不完整。");
            using (var check = db.Prepare("SELECT character_id FROM app_state;"))
            {
                bool found = false;
                while (check.Next()) { if (check.Text(0) != characterId) throw new InvalidDataException("备份角色不匹配。"); found = true; }
                if (!found) throw new InvalidDataException("备份缺少角色状态。");
            }
        }
    }
}
