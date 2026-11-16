using System.IO;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class RestoreSpaceEstimateTests
    {
        [Test] public void IncludesBackupSnapshotDatabaseAndReplacementWithoutCountingSourceArchiveAsNewCopy()
        {
            var e = RestoreSpaceEstimate.Calculate(100, 200, 50, long.MaxValue, "test:");
            Assert.That(e.RequiredBytes, Is.EqualTo(450 + 64L * 1048576)); e.EnsureAvailable();
            var scheduled = RestoreSpaceEstimate.Calculate(100, 200, 50, long.MaxValue, "test:", false);
            Assert.That(scheduled.RequiredBytes, Is.EqualTo(300 + 64L * 1048576));
        }
        [TestCase(0L)] [TestCase(null)] public void InsufficientOrUnknownFreeSpaceStopsWithReadableReason(long? free)
        {
            var e = RestoreSpaceEstimate.Calculate(100, 200, 50, free, "test:");
            var error = Assert.Throws<InvalidDataException>(() => e.EnsureAvailable()); Assert.That(error.Message, Does.Contain("当前资料未替换"));
        }
    }
}
