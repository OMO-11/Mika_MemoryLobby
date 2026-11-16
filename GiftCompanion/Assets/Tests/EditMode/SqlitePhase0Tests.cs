using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class SqlitePhase0Tests
    {
        private string path;
        [SetUp] public void Setup()
        {
            path = Path.GetFullPath(Path.Combine("UserData/Validation", "sqlite-" + Guid.NewGuid().ToString("N") + ".db"));
        }

        [Test]
        public void OriginalMessagesPersistAcrossReopenAndFts5SearchesThem()
        {
            const string original = "京都 Mika's memory; DROP TABLE messages; — 你好 🌙";
            using (var database = new SqlitePhase0Store(path))
            {
                database.SaveMessage("session-1", "user", original);
                database.SaveMessage("session-2", "assistant", "separate session");
            }
            using (var database = new SqlitePhase0Store(path))
            {
                Assert.That(database.ReadMessages("session-1").Single().Content, Is.EqualTo(original));
                Assert.That(database.Search("京都").Single().Content, Is.EqualTo(original));
                Assert.That(database.ReadMessages("session-2").Count, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task ConcurrentWritesAreSerializedAndCancellationDoesNotAddAMessage()
        {
            using var database = new SqlitePhase0Store(path);
            await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => database.SaveMessage("parallel", "user", "message " + i))));
            Assert.That(database.ReadMessages("parallel").Count, Is.EqualTo(24));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Throws<OperationCanceledException>(() => database.SaveMessage("parallel", "user", "canceled", canceled.Token));
            Assert.That(database.ReadMessages("parallel").Count, Is.EqualTo(24));
        }

        [TearDown] public void Cleanup()
        {
            foreach (string suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
}
