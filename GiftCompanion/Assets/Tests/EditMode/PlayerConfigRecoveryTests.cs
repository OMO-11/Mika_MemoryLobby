using System;
using System.IO;
using GiftCompanion.Core.Config;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class PlayerConfigRecoveryTests
    {
        private string root, file;
        [SetUp] public void Setup()
        {
            root = Path.GetFullPath("UserData/Validation/config-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Config")); file = Path.Combine(root, "Config", "settings.json");
            File.WriteAllText(file, "current"); File.WriteAllText(file + ".previous.bak", "previous"); File.WriteAllText(file + ".pending", "incomplete");
        }
        [Test] public void ChangingOrClosingSelectionCancelsOldConfirmation()
        {
            var s = new ConfigResetSelection(); s.Select("a.json"); Assert.That(s.ConfirmOrArm(), Is.False);
            s.Select("b.json"); Assert.That(s.ConfirmOrArm(), Is.False); Assert.That(s.ConfirmOrArm(), Is.True);
            s.Select("a.json"); s.ConfirmOrArm(); s.Cancel(); Assert.That(s.ConfirmOrArm(), Is.False);
        }
        [Test] public void ResetRetainsCurrentPreviousAndPendingWithoutResettingOtherFiles()
        {
            string other = Path.Combine(root, "Config", "other.json"); File.WriteAllText(other, "unchanged");
            string saved = new PlayerConfigRecovery(root).Reset("settings.json", default);
            Assert.That(File.ReadAllText(Path.Combine(saved, "settings.json")), Is.EqualTo("current"));
            Assert.That(File.ReadAllText(Path.Combine(saved, "settings.json.previous.bak")), Is.EqualTo("previous"));
            Assert.That(File.ReadAllText(Path.Combine(saved, "settings.json.pending")), Is.EqualTo("incomplete"));
            Assert.That(File.Exists(file), Is.False); Assert.That(File.Exists(file + ".pending"), Is.False);
            Assert.That(File.ReadAllText(other), Is.EqualTo("unchanged"));
        }
        [Test] public void LockedSidecarPreventsDeletionOfAnyOriginal()
        {
            using var locked = new FileStream(file + ".pending", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Throws<IOException>(() => new PlayerConfigRecovery(root).Reset("settings.json", default));
            Assert.That(File.ReadAllText(file), Is.EqualTo("current"));
            Assert.That(File.ReadAllText(file + ".previous.bak"), Is.EqualTo("previous"));
        }
        [Test] public void CancellationAndMissingMainFileDoNotRemoveSidecars()
        {
            using var cancel = new System.Threading.CancellationTokenSource(); cancel.Cancel();
            Assert.Throws<OperationCanceledException>(() => new PlayerConfigRecovery(root).Reset("settings.json", cancel.Token));
            File.Delete(file);
            Assert.Throws<FileNotFoundException>(() => new PlayerConfigRecovery(root).Reset("settings.json", default));
            Assert.That(File.Exists(file + ".previous.bak"), Is.True); Assert.That(File.Exists(file + ".pending"), Is.True);
        }
        [Test] public void InvalidPreviousIsRejectedBeforeChangingCurrentAndValidPreviousRetainsCurrent()
        {
            string card = Path.Combine(root, "Config", "character_card.json"); File.WriteAllText(card, "current"); File.WriteAllText(card + ".previous.bak", "previous");
            var service = new PlayerConfigRecovery(root);
            Assert.Throws<InvalidDataException>(() => service.RestorePrevious("character_card.json", _ => throw new InvalidDataException(), default));
            Assert.That(File.ReadAllText(card), Is.EqualTo("current"));
            string saved = service.RestorePrevious("character_card.json", s => Assert.That(s, Is.EqualTo("previous")), default);
            Assert.That(File.ReadAllText(card), Is.EqualTo("previous")); Assert.That(File.ReadAllText(Path.Combine(saved, "character_card.json")), Is.EqualTo("current"));
        }
    }
}
