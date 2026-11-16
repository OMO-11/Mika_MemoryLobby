using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class SpeechLocalizationTests
    {
        private sealed class Fake : IMemoryLLMService
        {
            public int Calls, Active, Maximum;
            public string Input, Instruction, Response = "{\"text\":\"先生、今日も一緒に音楽を聴こうね。\"}";
            public TaskCompletionSource<bool> Hold;
            public async Task<string> CompleteJsonAsync(string instruction, string input, int maxTokens, CancellationToken ct)
            {
                Calls++; Active++; Maximum = Math.Max(Maximum, Active); Input = input; Instruction = instruction;
                try { if (Hold != null) await Hold.Task; return Response; }
                finally { Active--; }
            }
        }
        [Test] public async Task TranslationUsesOnlySuppliedChineseAndBoundedCacheWithoutChangingSource()
        {
            var fake = new Fake(); var cfg = new SpeechLocalizationSettings { CacheEntries = 1 };
            using var localizer = new SpeechLocalizer(fake, cfg); cfg.Instruction = "later edit";
            string chinese = "老师，今天也一起听音乐吧。";
            var first = await localizer.LocalizeAsync(chinese, default);
            Assert.That(JObject.Parse(fake.Input).Value<string>("text"), Is.EqualTo(chinese));
            Assert.That(JObject.Parse(fake.Input).Count, Is.EqualTo(2)); Assert.That(fake.Instruction, Does.Not.Contain("later edit"));
            Assert.That(first.Language, Is.EqualTo("ja")); Assert.That(first.Model, Is.EqualTo("deepseek-flash"));
            Assert.That(first.InstructionHash.Length, Is.EqualTo(64));
            first.Text = "mutated caller";
            Assert.That((await localizer.LocalizeAsync(chinese, default)).Text, Does.Contain("先生")); Assert.That(fake.Calls, Is.EqualTo(1));
            await localizer.LocalizeAsync("第二段中文。", default); await localizer.LocalizeAsync(chinese, default);
            Assert.That(fake.Calls, Is.EqualTo(3));
        }
        [TestCase("{\"text\":\"中文回复\"}")]
        [TestCase("{\"text\":\"hello\"}")]
        [TestCase("{\"text\":\"先生だよ<META>\"}")]
        [TestCase("{\"text\":\"先生だよ\",\"role\":\"assistant\"}")]
        [TestCase("{\"text\":\"先生だよ\",\"text\":\"別の言葉だよ\"}")]
        [TestCase("{\"text\":\"先生だよ\"} trailing")]
        public void InvalidTranslationCannotReachTtsOrEnterCache(string response)
        {
            var fake = new Fake { Response = response }; using var localizer = new SpeechLocalizer(fake, new SpeechLocalizationSettings());
            Assert.ThrowsAsync<InvalidDataException>(() => localizer.LocalizeAsync("原中文", default));
        }
        [Test] public async Task CanceledLateTranslationDoesNotReleaseGateEarlyOrPopulateCache()
        {
            var fake = new Fake { Hold = new TaskCompletionSource<bool>() }; using var localizer = new SpeechLocalizer(fake, new SpeechLocalizationSettings());
            using var ct = new CancellationTokenSource(); var old = localizer.LocalizeAsync("旧中文", ct.Token); ct.Cancel();
            var next = localizer.LocalizeAsync("新中文", default); Assert.That(fake.Calls, Is.EqualTo(1));
            fake.Hold.TrySetResult(true); Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await old); await next;
            await localizer.LocalizeAsync("旧中文", default); Assert.That(fake.Calls, Is.EqualTo(3)); Assert.That(fake.Maximum, Is.EqualTo(1));
        }
        [Test] public void DefaultIsJapaneseAndLanguageProfilesArePairedWithoutMutatingFileSettings()
        {
            var legacy = JsonConvert.DeserializeObject<AppConfiguration>("{\"VoiceVolume\":0.2}"); Assert.That(legacy.VoiceLanguage, Is.EqualTo("ja"));
            var runtime = new VoiceRuntimeSettings(); var japanese = runtime.ForLanguage("ja");
            Assert.That(japanese.Profile, Is.EqualTo("voice-ja.json")); Assert.That(japanese.InferenceConfig, Is.EqualTo("tts_infer-ja.yaml"));
            Assert.That(runtime.Profile, Is.EqualTo("voice.json")); Assert.That(japanese.ForLanguage("zh").InferenceConfig, Is.EqualTo("tts_infer.yaml"));
            Assert.Throws<InvalidDataException>(() => runtime.ForLanguage("xx"));
        }
        [Test] public async Task EmotionalTranslationsHaveSeparateCacheEntriesAndReceiveOnlyTextAndEmotion()
        {
            var fake = new Fake(); using var service = new SpeechLocalizer(fake, new SpeechLocalizationSettings());
            await service.LocalizeAsync("老师，一起听音乐吧。", "happy", default);
            Assert.That(JObject.Parse(fake.Input).Value<string>("emotion"), Is.EqualTo("happy"));
            await service.LocalizeAsync("老师，一起听音乐吧。", "shy", default);
            Assert.That(fake.Calls, Is.EqualTo(2)); await service.LocalizeAsync("老师，一起听音乐吧。", "happy", default); Assert.That(fake.Calls, Is.EqualTo(2));
        }
    }
}
