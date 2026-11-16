using System;
using System.IO;
using GiftCompanion.Core.Config;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class CompanionDisplayTests
    {
        [Test] public void InvalidDisplayIsPreservedAndSavingValidSettingsCanRecoverIt()
        {
            string root = Path.GetFullPath("UserData/Validation/display-" + Guid.NewGuid().ToString("N"));
            var service = new CompanionDisplayService(root); service.Load(default);
            File.WriteAllText(service.FilePath, "{\"Version\":99,\"Exposure\":1000}"); service.Load(default);
            Assert.That(service.Issue, Is.Not.Null); Assert.That(File.ReadAllText(service.FilePath), Does.Contain("1000"));
            Assert.Throws<InvalidDataException>(() => service.Save(new CompanionDisplaySettings { DiaryFontSize = float.NaN }, default));
            service.Save(new CompanionDisplaySettings { DiaryFontSize = 28 }, default);
            var reopened = new CompanionDisplayService(root); reopened.Load(default); Assert.That(reopened.Value.DiaryFontSize, Is.EqualTo(28));
            Assert.That(File.Exists(service.FilePath + ".previous.bak"), Is.True);
        }
    }
}
