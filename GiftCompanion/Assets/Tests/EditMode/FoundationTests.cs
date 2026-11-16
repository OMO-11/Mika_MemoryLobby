using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class FoundationTests
    {
        private string directory;
        private const string Identity = "{\"id\":\"test\",\"displayName\":\"Test\",\"persona\":\"\",\"relationship\":\"\"}";
        [SetUp] public void Setup() => directory = Path.GetFullPath("UserData/Validation/foundation-" + Guid.NewGuid().ToString("N"));
        private DatabaseService Database() => new DatabaseService(Path.Combine(directory, "memory.db"), "test");
        private static async Task Open(DatabaseService database)
        {
            await database.InitializeAsync(default); await database.MigrateAsync(default);
        }

        [Test] public void ValidationPathsAreIsolatedAndOrdinaryPlayerStartupIgnoresOverrides()
        {
            string normal = UserDataPaths.Resolve(false, directory, Array.Empty<string>());
            Assert.That(UserDataPaths.Resolve(true, directory, Array.Empty<string>()), Is.EqualTo(Path.Combine(directory, "UserData")));
            Assert.That(UserDataPaths.Resolve(false, directory, new[] { "-validation-data", directory }), Is.EqualTo(normal));
            Assert.Throws<InvalidDataException>(() => UserDataPaths.Resolve(false, directory, new[] { "-phase1-probe", "-validation-data", directory }));
            string isolated = Path.Combine(normal, "Validation", "test");
            Assert.That(UserDataPaths.Resolve(false, directory, new[] { "-phase1-probe", "-validation-data", isolated }), Is.EqualTo(isolated));
            Assert.That(UserDataPaths.Resolve(false, directory, new[] { "-phase3plus-probe", "-validation-data", isolated }), Is.EqualTo(isolated));
            Assert.Throws<InvalidDataException>(() => UserDataPaths.Resolve(false, directory, new[] { "-phase3plus-probe" }));
            Assert.Throws<InvalidDataException>(() => UserDataPaths.Resolve(false, directory, new[] { "-phase3plus-probe", "-validation-data", directory }));
        }

        [Test] public async Task SettingsPersistAndCorruptOriginalSurvivesDefaultFallbackAndSave()
        {
            var first = new ConfigService(directory, Identity);
            await first.InitializeAsync(default);
            await first.SaveAsync(new AppConfiguration { NetworkTimeoutSeconds = 17, BgmVolume=.24f, BgmMuted=true, SubtitleFontSize=29 }, default);
            var reopened = new ConfigService(directory, Identity);
            await reopened.InitializeAsync(default);
            Assert.That(reopened.Configuration.NetworkTimeoutSeconds, Is.EqualTo(17));
            Assert.That(reopened.Configuration.BgmVolume, Is.EqualTo(.24f)); Assert.That(reopened.Configuration.BgmMuted, Is.True); Assert.That(reopened.Configuration.SubtitleFontSize, Is.EqualTo(29));
            Assert.ThrowsAsync<InvalidDataException>(() => reopened.SaveAsync(new AppConfiguration { BgmVolume=float.NaN }, default));
            string path = Path.Combine(directory, "Config/settings.json");
            File.WriteAllText(path, "{broken-settings");
            var corrupt = new ConfigService(directory, Identity);
            await corrupt.InitializeAsync(default);
            Assert.That(corrupt.UsedDefaultsAfterInvalidFile, Is.True);
            Assert.That(corrupt.Configuration.NetworkTimeoutSeconds, Is.EqualTo(30));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{broken-settings"));
            await corrupt.SaveAsync(new AppConfiguration(), default);
            Assert.That(File.ReadAllText(Directory.GetFiles(Path.GetDirectoryName(path), "*.bak").Single()), Is.EqualTo("{broken-settings"));
            Assert.ThrowsAsync<InvalidDataException>(() => corrupt.SaveAsync(new AppConfiguration { NetworkTimeoutSeconds = 0 }, default));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path), "*.tmp"), Is.Empty);
        }

        [Test] public async Task StatePersistsDetachedAcrossReopenAndCanceledWriteDoesNotReplaceIt()
        {
            var state = new CharacterState { CurrentTopic = "京都 中文 🌙 ' SQL", Energy = .42f,
                Mood = CharacterEmotion.Shy, LastMode = CharacterMode.Companion, LastInteractionAt = DateTimeOffset.UtcNow };
            using (var database = Database())
            {
                await Open(database);
                Assert.That(database.IsPersistent, Is.True);
                Assert.That(database.SchemaVersion, Is.EqualTo(DatabaseService.CurrentSchemaVersion));
                await database.SaveCharacterStateAsync(state, default);
                state.CurrentTopic = "later mutation";
                using var canceled = new CancellationTokenSource(); canceled.Cancel();
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), () => database.SaveCharacterStateAsync(state, canceled.Token));
            }
            using (var reopened = Database())
            {
                await Open(reopened);
                var saved = await reopened.LoadCharacterStateAsync(default);
                Assert.That(saved.CurrentTopic, Is.EqualTo("京都 中文 🌙 ' SQL"));
                Assert.That(saved.Energy, Is.EqualTo(.42f));
                Assert.That(saved.Mood, Is.EqualTo(CharacterEmotion.Shy));
                Assert.That(saved.LastMode, Is.EqualTo(CharacterMode.Companion));
                Assert.That(saved.LastInteractionAt, Is.EqualTo(state.LastInteractionAt));
                saved.CurrentTopic = "reader mutation";
                Assert.That((await reopened.LoadCharacterStateAsync(default)).CurrentTopic, Is.EqualTo("京都 中文 🌙 ' SQL"));
            }
        }

        [Test] public async Task MigrationFailureRollsBackVersionAndBacksUpExistingDatabase()
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "memory.db");
            using (var seed = new SqliteConnection(path)) seed.Execute("CREATE TABLE app_state(conflicting_column TEXT); PRAGMA user_version=0;");
            using (var database = Database())
            {
                await database.InitializeAsync(default);
                Assert.ThrowsAsync<IOException>(() => database.MigrateAsync(default));
                Assert.That(database.IsPersistent, Is.False);
            }
            using (var check = new SqliteConnection(path)) Assert.That(check.Version, Is.Zero);
            string backup = Directory.GetFiles(directory, "*.bak").Single();
            using var checkBackup = new SqliteConnection(backup);
            Assert.That(checkBackup.Version, Is.Zero);
        }

        [Test] public void NewerSchemaCorruptionAndUnwritablePathFailWithoutRamFallback()
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "memory.db");
            using (var seed = new SqliteConnection(path)) seed.Execute("PRAGMA user_version=99;");
            using (var database = Database())
            {
                Assert.ThrowsAsync<InvalidDataException>(() => database.InitializeAsync(default));
                Assert.That(database.IsPersistent, Is.False);
            }
            using (var check = new SqliteConnection(path)) Assert.That(check.Version, Is.EqualTo(99));
            File.Delete(path); File.WriteAllText(path, "not a sqlite database");
            using (var database = Database()) Assert.ThrowsAsync<IOException>(() => database.InitializeAsync(default));
            string blocker = Path.Combine(directory, "file-not-directory"); File.WriteAllText(blocker, "block");
            using (var database = new DatabaseService(Path.Combine(blocker, "memory.db"), "test"))
                Assert.ThrowsAsync<IOException>(() => database.InitializeAsync(default));
        }

        [TearDown] public void Cleanup()
        {
            string validationRoot = Path.GetFullPath("UserData/Validation") + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(validationRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected cleanup path.");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
