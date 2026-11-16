using System;
using System.IO;
using GiftCompanion.Core.Config;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class PetSettingsTests
    {
        [TestCase("Ctrl+Alt+Space", true)][TestCase("alt+shift+M", true)][TestCase("Ctrl+F11", true)]
        [TestCase("Alt+Space", false)][TestCase("F12", false)][TestCase("Ctrl+Ctrl+M", false)][TestCase("Win+M", false)][TestCase("Shift+M", false)]
        public void HotkeyRequiresAnExplicitSupportedNonReservedCombination(string value, bool expected)
        { Assert.That(PetHotkey.TryParse(value, out _), Is.EqualTo(expected)); }
        [Test] public void DragOverridesAnAlreadyStartedHeadPatAndCancellationCannotClick()
        {
            var gesture = new Core.Character.PetGesture(); gesture.Begin(true,100,100,0);
            Assert.That(gesture.Move(100,100,1,9,.5f),Is.EqualTo(Core.Character.PetGestureEvent.HeadPatStart));
            Assert.That(gesture.Move(120,100,1.1f,9,.5f),Is.EqualTo(Core.Character.PetGestureEvent.DragStart));
            Assert.That(gesture.Release(false),Is.EqualTo(Core.Character.PetGestureEvent.DragEnd));
            gesture.Begin(false,100,100,2);Assert.That(gesture.Release(true),Is.EqualTo(Core.Character.PetGestureEvent.None));
        }
        [Test] public void PetConfigurationRoundTripsAndInvalidFileIsPreserved()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gift-pet-settings-" + Guid.NewGuid().ToString("N"));
            try
            {
                var service = new PetSettingsService(folder); service.Load(default);
                var value = service.Value.Copy(); value.HasPosition = true; value.Left = -1400; value.Top = 80; service.Save(value, default);
                var other = new PetSettingsService(folder); other.Load(default); Assert.That(other.Value.Left, Is.EqualTo(-1400));
                File.WriteAllText(service.FilePath, "{\"Version\":98}"); other.Load(default);
                Assert.That(other.Issue, Is.Not.Null); Assert.That(File.ReadAllText(service.FilePath), Does.Contain("98")); Assert.That(other.Value.Left, Is.EqualTo(-1400));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
