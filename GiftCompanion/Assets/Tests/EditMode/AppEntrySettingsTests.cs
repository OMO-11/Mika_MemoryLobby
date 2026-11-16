using System;
using System.IO;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Character;
using NUnit.Framework;
namespace GiftCompanion.Tests
{
    public sealed class AppEntrySettingsTests
    {
        [Test] public void NewAndInvalidFilesKeepCompanionAsDefaultAndPetClosed()
        {
            string root = Path.GetFullPath("UserData/Validation/entry-" + Guid.NewGuid().ToString("N"));
            var entry = AppEntrySettings.Load(root, default); Assert.That(entry.InitialMode, Is.EqualTo(CharacterMode.Companion)); Assert.That(entry.PetEntryEnabled, Is.False);
            string path = Path.Combine(root, "Config/app_entry.json"); File.WriteAllText(path, "{\"Version\":99,\"PetEntryEnabled\":true}");
            Assert.That(AppEntrySettings.Load(root, default).PetEntryEnabled, Is.False); Assert.That(File.ReadAllText(path), Does.Contain("99"));
            File.WriteAllText(path, "{\"Version\":1,\"StartupMode\":\"home\",\"PetEntryEnabled\":true}");
            Assert.That(AppEntrySettings.Load(root, default).InitialMode, Is.EqualTo(CharacterMode.Home));
        }
    }
}
