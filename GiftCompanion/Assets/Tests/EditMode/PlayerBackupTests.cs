using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class PlayerBackupTests
    {
        private string root, archive;
        private const string Identity = "{\"id\":\"test\",\"displayName\":\"Test\",\"persona\":\"\",\"relationship\":\"\"}";
        [SetUp] public void Setup()
        { root = Path.GetFullPath("UserData/Validation/backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); archive = Path.Combine(root, "Backups", "snapshot.mikabackup"); }
        private async Task<DatabaseService> Seed()
        {
            var config = new ConfigService(root, Identity); await config.InitializeAsync(default);
            var db = new DatabaseService(Path.Combine(root, "memory.db"), "test"); await db.InitializeAsync(default); await db.MigrateAsync(default);
            await db.SaveCharacterStateAsync(new CharacterState { CurrentTopic = "快照前" }, default);
            Write("Diary/notes.md", "第一段\n\n玩家的手工批注 (•̀ᴗ•́)و"); Write("Content/Music/sample.ogg", "imported"); Write("VoiceReferences/reference.wav", "reference"); Write("AudioCache/clip.wav", "cached");
            Write("Secrets/not-for-backup.txt", "private-test-marker"); Write("Validation/excluded.json", "test");
            Write("Config/settings.json.0123456789abcdef0123456789abcdef.tmp", "interrupted atomic write");
            return db;
        }
        private void Write(string relative, string value) { string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value); }
        [TestCase(false)] [TestCase(true)]
        public async Task WalSnapshotRoundTripPreservesDataAndAnnotationsButNeverSecrets(bool audio)
        {
            var backup = new PlayerBackup(root, root, "test"); BackupPreview preview;
            using (var db = await Seed())
            {
                var manifest = await backup.CreateAsync(archive, audio, db, default);
                Assert.That(manifest.Files.Any(f => f.Path.StartsWith("Secrets/") || f.Path.StartsWith("Validation/") || f.Path.StartsWith("Backups/")), Is.False);
                Assert.That(manifest.Files.Any(f => f.Path.EndsWith(".tmp")), Is.False);
                Assert.That(manifest.Files.Any(f => f.Path == "AudioCache/clip.wav"), Is.EqualTo(audio));
                preview = await backup.InspectAsync(archive, default);
                await db.SaveCharacterStateAsync(new CharacterState { CurrentTopic = "快照后" }, default);
                Write("Diary/notes.md", "changed"); Write("Config/known_apps.json", "[{\"Name\":\"old\",\"Path\":\"C:\\\\old.exe\"}]");
                backup.Schedule(preview, default);
            }
            string message = PlayerRestore.ApplyPending(root, root, "test"); Assert.That(message, Does.Contain("资料已恢复"));
            Assert.That(File.ReadAllText(Path.Combine(root, "Diary/notes.md")), Does.Contain("玩家的手工批注"));
            Assert.That(File.ReadAllText(Path.Combine(root, "Secrets/not-for-backup.txt")), Is.EqualTo("private-test-marker"));
            Assert.That(File.ReadAllText(Path.Combine(root, "Config/known_apps.json")), Does.Not.Contain("old"));
            Assert.That(File.Exists(Path.Combine(root, "AudioCache/clip.wav")), Is.EqualTo(audio));
            Assert.That(Directory.GetDirectories(Path.Combine(root, "RestorePoints")).Length, Is.EqualTo(1));
            Assert.That(Directory.GetDirectories(Path.Combine(root, "RestoreStaging")), Is.Empty, "A committed restore must release its temporary full-data copy.");
            using var reopened = new DatabaseService(Path.Combine(root, "memory.db"), "test"); await reopened.InitializeAsync(default); await reopened.MigrateAsync(default);
            Assert.That((await reopened.LoadCharacterStateAsync(default)).CurrentTopic, Is.EqualTo("快照前"));
        }
        [TestCase(0L)] [TestCase(null)] public async Task MissingSpaceStopsBeforeExtractionAndLeavesCurrentDatabaseAndDiary(long? free)
        {
            using var db = await Seed(); var backup = new PlayerBackup(root, root, "test"); await backup.CreateAsync(archive, false, db, default);
            var constrained = new PlayerBackup(root, root, "test", _ => free);
            Assert.ThrowsAsync<InvalidDataException>(() => constrained.InspectAsync(archive, default));
            Assert.That(Directory.GetDirectories(Path.Combine(root, "RestoreStaging")), Is.Empty);
            Assert.That(File.Exists(Path.Combine(root, "pending_restore.json")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(root, "Diary", "notes.md")), Does.Contain("玩家的手工批注"));
            Assert.That((await db.LoadCharacterStateAsync(default)).CurrentTopic, Is.EqualTo("快照前"));
        }
        [Test] public async Task PreviewTreatsNormalPromptProseAsTextAndCountsOnlyRealAbsolutePaths()
        {
            using var db = await Seed();
            AtomicDocument.WriteJson(Path.Combine(root, "Config", "text_policy.json"), new { prompt = "第一行\n请返回\"JSON\"：<自然表达>？", location = @"C:\Users\Player\Music", url = "https://example.org/" }, default);
            var backup = new PlayerBackup(root, root, "test"); await backup.CreateAsync(archive, false, db, default);
            var preview = await backup.InspectAsync(archive, default); Assert.That(preview.ExternalPaths, Is.EqualTo(1)); backup.Discard(preview);
        }
        [Test] public async Task TamperedFileCannotBeScheduledOrOverwriteCurrentDatabase()
        {
            using var db = await Seed(); var backup = new PlayerBackup(root, root, "test"); await backup.CreateAsync(archive, false, db, default);
            var preview = await backup.InspectAsync(archive, default);
            File.AppendAllText(Path.Combine(root, "RestoreStaging", preview.StageId, "data", "Diary", "notes.md"), "tamper");
            Assert.Throws<InvalidDataException>(() => backup.Schedule(preview, default));
            Assert.That(File.Exists(Path.Combine(root, "pending_restore.json")), Is.False);
            Assert.That((await db.LoadCharacterStateAsync(default)).CurrentTopic, Is.EqualTo("快照前"));
        }
        [TestCase("../Secrets/key.json")] [TestCase("Config/../../escape.json")] [TestCase("Config/test.json:stream")] [TestCase("Config/CON.json")] [TestCase("Runtime/run.py")] [TestCase("Content/run.exe")]
        public async Task ArchiveRejectsUntrustedPathsBeforeExtraction(string bad)
        {
            using var db = await Seed(); var backup = new PlayerBackup(root, root, "test"); await backup.CreateAsync(archive, false, db, default);
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
            {
                var me = zip.GetEntry("backup_manifest.json"); BackupManifest m; using (var r = new StreamReader(me.Open())) m = JsonConvert.DeserializeObject<BackupManifest>(r.ReadToEnd()); me.Delete();
                m.Files[0].Path = bad; using var w = new StreamWriter(zip.CreateEntry("backup_manifest.json").Open()); w.Write(JsonConvert.SerializeObject(m));
            }
            Assert.ThrowsAsync<InvalidDataException>(() => backup.InspectAsync(archive, default));
            Assert.That(File.Exists(Path.Combine(root, "pending_restore.json")), Is.False);
        }
        [Test] public async Task NewerSchemaAndWrongCharacterAreRejectedWithoutTouchingLiveData()
        {
            using var db = await Seed(); var backup = new PlayerBackup(root, root, "test"); await backup.CreateAsync(archive, false, db, default);
            Assert.ThrowsAsync<InvalidDataException>(() => new PlayerBackup(root, root, "another").InspectAsync(archive, default));
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
            {
                var me = zip.GetEntry("backup_manifest.json"); BackupManifest m; using (var r = new StreamReader(me.Open())) m = JsonConvert.DeserializeObject<BackupManifest>(r.ReadToEnd()); me.Delete(); m.Schema = 999;
                using var w = new StreamWriter(zip.CreateEntry("backup_manifest.json").Open()); w.Write(JsonConvert.SerializeObject(m));
            }
            Assert.ThrowsAsync<InvalidDataException>(() => backup.InspectAsync(archive, default));
            Assert.That((await db.LoadCharacterStateAsync(default)).CurrentTopic, Is.EqualTo("快照前"));
        }
        [Test] public async Task CanceledAndDuplicateBackupsLeaveOriginalAndNoPartialProduct()
        {
            using var db = await Seed(); var backup = new PlayerBackup(root, root, "test"); await backup.CreateAsync(archive, false, db, default); byte[] original = File.ReadAllBytes(archive);
            Assert.ThrowsAsync<InvalidDataException>(() => backup.CreateAsync(archive, false, db, default));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => backup.CreateAsync(Path.Combine(root, "cancel.mikabackup"), false, db, cancel.Token));
            Assert.That(File.ReadAllBytes(archive), Is.EqualTo(original)); Assert.That(Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories), Is.Empty);
        }
        [Test] public async Task CrashJournalRollsBackPartialDirectorySwitchBeforeBootstrap()
        {
            using (var db = await Seed()) { }
            string point = Guid.NewGuid().ToString("N"); string prev = Path.Combine(root, "RestorePoints", point, "previous"); Directory.CreateDirectory(prev);
            Directory.Move(Path.Combine(root, "Config"), Path.Combine(prev, "Config")); Write("Config/changed.json", "new");
            AtomicDocument.WriteJson(Path.Combine(root, "restore_transaction.json"), new { StageId = Guid.NewGuid().ToString("N"), PointId = point, Committed = false, OriginalRoots = new[] { "memory.db", "Config", "Content", "VoiceReferences", "Diary", "AudioCache" } }, default, false);
            PlayerRestore.ApplyPending(root, root, "test");
            Assert.That(File.Exists(Path.Combine(root, "Config/character_card.json")), Is.True); Assert.That(File.Exists(Path.Combine(root, "Config/changed.json")), Is.False);
        }
        [Test] public async Task CopiedRestorePointCanRollBackAgainAfterAnInterruptedRollback()
        {
            using (var db = await Seed()) { }
            string id = Guid.NewGuid().ToString("N"), old = Path.Combine(root, "RestorePoints", id, "previous"); Directory.CreateDirectory(old);
            File.Copy(Path.Combine(root, "memory.db"), Path.Combine(old, "memory.db"));
            Directory.CreateDirectory(Path.Combine(old, "Config")); File.Copy(Path.Combine(root, "Config/character_card.json"), Path.Combine(old, "Config/character_card.json"));
            var journal = new { StageId = Guid.NewGuid().ToString("N"), PointId = id, Committed = false, CopySnapshotComplete = true, OriginalRoots = new[] { "memory.db", "Config" } };
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Write("Config/changed.json", "partly installed");
                AtomicDocument.WriteJson(Path.Combine(root, "restore_transaction.json"), journal, default, false);
                PlayerRestore.ApplyPending(root, root, "test");
                Assert.That(File.Exists(Path.Combine(root, "Config/changed.json")), Is.False);
                Assert.That(File.Exists(Path.Combine(old, "Config/character_card.json")), Is.True, "A rollback must not consume its recovery copy.");
            }
        }
        [Test] public void CurrentVersionWithMissingApplicationTablesCannotPassRestoreValidation()
        {
            string broken = Path.Combine(root, "incomplete.db");
            using (var db = new SqliteConnection(broken)) db.Execute("CREATE TABLE app_state(character_id TEXT); INSERT INTO app_state VALUES('test'); PRAGMA user_version=10;");
            Assert.Throws<IOException>(() => DatabaseService.VerifyBackup(broken, 10, "test"));
        }
        [Test] public void DpapiRoundTripNeverWritesPlaintextAndReleaseIgnoresEnvironment()
        {
            const string value = "test-key-private-marker-123456";
            LocalSecrets.SaveDeepSeekKey(root, value, default);
            Assert.That(LocalSecrets.ReadDeepSeekKey(root, false), Is.EqualTo(value));
            Assert.That(File.ReadAllText(Path.Combine(root, "Secrets/deepseek.key.dpapi")), Does.Not.Contain(value));
            string old = Environment.GetEnvironmentVariable("GIFT_DEEPSEEK_API_KEY");
            try { Environment.SetEnvironmentVariable("GIFT_DEEPSEEK_API_KEY", "environment-marker"); Assert.That(LocalSecrets.ReadDeepSeekKey(root, false), Is.EqualTo(value)); }
            finally { Environment.SetEnvironmentVariable("GIFT_DEEPSEEK_API_KEY", old); }
        }
    }
}
