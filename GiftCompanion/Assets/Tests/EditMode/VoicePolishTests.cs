using System;
using System.IO;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Config;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class VoicePolishTests
    {
        private string root;
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "gift-polish-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "Config")); }
        [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        [Test] public async Task JapaneseDefaultMigrationPreservesVolumesAndLaterExplicitChineseChoice()
        {
            const string identity = "{\"id\":\"mika\",\"displayName\":\"Mika\",\"persona\":\"中文\",\"relationship\":\"老师\"}";
            string file = Path.Combine(root, "Config/settings.json"); File.WriteAllText(file, "{\"VoiceLanguage\":\"zh\",\"VoiceVolume\":0.3,\"BgmVolume\":0.2}");
            var service = new ConfigService(root, identity); await service.InitializeAsync(default);
            Assert.That(service.Configuration.VoiceLanguage, Is.EqualTo("ja")); Assert.That(service.Configuration.VoicePreferencesVersion, Is.EqualTo(2));
            Assert.That(service.Configuration.VoiceVolume, Is.EqualTo(.3f)); Assert.That(service.Configuration.BgmVolume, Is.EqualTo(.2f));
            var changed = service.Configuration.Copy(); changed.VoiceLanguage = "zh"; await service.SaveAsync(changed, default);
            var reloaded = new ConfigService(root, identity); await reloaded.InitializeAsync(default); Assert.That(reloaded.Configuration.VoiceLanguage, Is.EqualTo("zh"));
        }
        [Test] public void BuiltInLocalizationUpgradesButCustomInstructionIsPreserved()
        {
            string file = Path.Combine(root, "Config/speech_localization.json");
            AtomicDocument.WriteJson(file, new SpeechLocalizationSettings { Version = 1, Instruction = SpeechLocalizationSettings.LegacyInstruction }, default);
            var upgraded = SpeechLocalizationSettings.Load(root, default); Assert.That(upgraded.Version, Is.EqualTo(3)); Assert.That(upgraded.Instruction, Does.Contain("常体"));
            AtomicDocument.WriteJson(file, new SpeechLocalizationSettings { Version = 1, Instruction = "player custom rule" }, default);
            Assert.That(SpeechLocalizationSettings.Load(root, default).Instruction, Is.EqualTo("player custom rule"));
        }
        [Test] public void EmotionReferenceSelectionAndBreathAreIndependentFilesAndInvalidPathsAreRejected()
        {
            var settings = new VoiceDeliverySettings { DefaultJapanese = "bright" };
            settings.Styles.Add(new VoiceDeliveryStyle { Id = "bright", Language = "ja", Emotions = new[] { "happy" } });
            settings.Styles.Add(new VoiceDeliveryStyle { Id = "shy", Language = "ja", Emotions = new[] { "shy" }, SpeedFactor = .96f });
            settings.Validate(root); Assert.That(settings.Select("ja", "shy").Id, Is.EqualTo("shy")); Assert.That(settings.Select("ja", "neutral").Id, Is.EqualTo("bright"));
            Assert.Throws<InvalidDataException>(() => VoiceDeliverySettings.ResolveReference(root, "VoiceReferences/../../memory.db"));
            var runtime = new VoiceRuntimeSettings(); Assert.That(runtime.PauseAfter("一起听吧。"), Is.EqualTo(.45f)); Assert.That(runtime.PauseAfter("老师，"), Is.EqualTo(.18f));
            runtime.SentencePauseSeconds = float.NaN; Assert.Throws<InvalidDataException>(runtime.Validate);
        }
    }
}
