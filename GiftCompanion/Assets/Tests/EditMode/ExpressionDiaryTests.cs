using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Memory;
using Newtonsoft.Json;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class ExpressionDiaryTests
    {
        [TestCase("annoyed")][TestCase("sad")][TestCase("hesitant")][TestCase("flat")]
        public void VoiceIntentSurvivesEveryStreamSplitWithoutExpandingPersistentMood(string intent)
        {
            string wire = "<META emotion=\"neutral\" action=\"none\" voice=\"" + intent + "\">\n老师，测试正文。";
            for (int split = 0; split <= wire.Length; split++)
            {
                var parser = new CompanionDialogueParser();
                Assert.That(parser.Feed(wire.Substring(0, split)) + parser.Feed(wire.Substring(split)) + parser.Finish(), Is.EqualTo("老师，测试正文。"));
                Assert.That(parser.VoiceIntent, Is.EqualTo(intent)); Assert.That(parser.Emotion, Is.EqualTo(CharacterEmotion.Neutral));
            }
            Assert.That(Enum.GetValues(typeof(CharacterEmotion)).Length, Is.EqualTo(8));
            var legacy = new CompanionDialogueParser(); legacy.Feed("<META emotion=\"happy\" action=\"none\">\n正文"); Assert.That(legacy.VoiceIntent, Is.EqualTo("happy"));
            var invalid = new CompanionDialogueParser(); invalid.Feed("<META emotion=\"shy\" action=\"none\" voice=\"invented\">\n正文"); Assert.That(invalid.VoiceIntent, Is.EqualTo("shy"));
        }
        [Test] public void CompanionHistoryShowsBoundedProtocolExamplesWithoutChangingStoredOrAgentPetBody()
        {
            var context = new MemoryContext();
            string legacy = "<META emotion=\"annoyed\" action=\"none\" voice=\"flat\">\n旧版原样文字";
            context.Recent.Add(new ConversationTurn { Source = InteractionSource.Companion, UserText = "先做什么？", AssistantText = "还没想好。",
                RawAssistantText = "<META emotion=\"concern\" action=\"none\" voice=\"hesitant\">\n还没想好。", UserMessageId = "u1", AssistantMessageId = "a1" });
            context.Recent.Add(new ConversationTurn { Source = InteractionSource.Companion, UserText = "旧消息", AssistantText = legacy, RawAssistantText = legacy, UserMessageId = "u2", AssistantMessageId = "a2" });
            context.Recent.Add(new ConversationTurn { Source = InteractionSource.AgentPet, UserText = "桌面消息", AssistantText = "收到。", RawAssistantText = "收到。", UserMessageId = "u3", AssistantMessageId = "a3" });
            var identity = new CharacterIdentity("test", "Test", "", "");
            var companion = PromptBuilder.Build(identity, "继续", InteractionSource.Companion, context, DateTimeOffset.Now);
            for (int i = 0; i < context.Recent.Count; i++)
            {
                var parser = new CompanionDialogueParser();
                Assert.That(parser.Feed(companion.Request.History[i * 2 + 1].Content) + parser.Finish(), Is.EqualTo(context.Recent[i].AssistantText));
                Assert.That(parser.VoiceIntent, Is.EqualTo(i == 0 ? "hesitant" : "neutral"));
            }
            var pet = PromptBuilder.Build(identity, "继续", InteractionSource.AgentPet, context, DateTimeOffset.Now);
            Assert.That(pet.Request.History.Where(m => m.Role == "assistant").Select(m => m.Content), Is.EqualTo(context.Recent.Select(t => t.AssistantText)));
            Assert.That(context.Recent[0].AssistantText, Is.EqualTo("还没想好。"));
            Assert.That(context.Recent[1].AssistantText, Is.EqualTo(legacy));
            Assert.That(companion.SourceMessageIds.Count, Is.EqualTo(6));
        }
        [Test] public void CompanionProtocolExamplesConsumePromptBudgetAndCannotSplitHistoryPairs()
        {
            var identity = new CharacterIdentity("test", "Test", "", ""); var now = DateTimeOffset.Now; var context = new MemoryContext();
            var baseline = PromptBuilder.Build(identity, "继续", InteractionSource.Companion, context, now);
            int remaining = PromptBuilder.CharacterBudget - baseline.Request.SystemPrompt.Length - baseline.Request.Message.Length;
            var turn = new ConversationTurn { UserText = "问", AssistantText = new string('字', remaining - 1), UserMessageId = "u", AssistantMessageId = "a" };
            context.Recent.Add(turn);
            var excluded = PromptBuilder.Build(identity, "继续", InteractionSource.Companion, context, now);
            Assert.That(excluded.Request.History, Is.Empty); Assert.That(excluded.SourceMessageIds, Is.Empty);
            turn.AssistantText = turn.AssistantText.Substring(200);
            var included = PromptBuilder.Build(identity, "继续", InteractionSource.Companion, context, now);
            Assert.That(included.Request.History.Count, Is.EqualTo(2)); Assert.That(included.SourceMessageIds.Count, Is.EqualTo(2));
            Assert.That(included.Request.SystemPrompt.Length + included.Request.Message.Length + included.Request.History.Sum(m => m.Content.Length), Is.LessThanOrEqualTo(PromptBuilder.CharacterBudget));
        }
        [Test] public void LongStructuredAndLegacyDiariesAreSplitWithoutRewritingContentOrBreakingSurrogates()
        {
            var policy = new DiaryPolicy(); string original = string.Concat(Enumerable.Repeat("我们聊过音乐，也说过窗边的花。现在回看这些话，我仍觉得很温柔。", 25));
            string body = DiaryTextLayout.Read(JsonConvert.SerializeObject(new { paragraphs = new[] { original, "这段就先记到这里。(´▽｀)" } }), policy);
            Assert.That(body.Split(new[] { "\n\n" }, StringSplitOptions.None).All(s => s.Length <= policy.MaximumParagraphCharacters), Is.True);
            Assert.That(Regex.Replace(body, @"\s", ""), Is.EqualTo(Regex.Replace(original + "这段就先记到这里。(´▽｀)", @"\s", "")));
            string emojiText = new string('字', 259) + "🌙" + new string('字', 300);
            string formatted = DiaryTextLayout.Read(JsonConvert.SerializeObject(new { diary = emojiText }), policy);
            Assert.That(formatted.Replace("\n", ""), Is.EqualTo(emojiText));
            Assert.That(formatted.Split(new[] { "\n\n" }, StringSplitOptions.None).All(s => !char.IsHighSurrogate(s.Last()) && !char.IsLowSurrogate(s.First())), Is.True);
            Assert.That(new DiaryPolicy().MaximumKaomoji, Is.EqualTo(4));
        }
        [TestCase("{\"paragraphs\":[]}")][TestCase("{\"paragraphs\":[12]}")][TestCase("{\"paragraphs\":[\"\"]}")][TestCase("{\"paragraphs\":[\"<META>\"]}")]
        public void InvalidParagraphProtocolCannotCreateDiary(string json) => Assert.Throws<InvalidDataException>(() => DiaryTextLayout.Read(json, new DiaryPolicy()));

        [Test] public void BuiltInVoiceFallbackUpgradesAndPlayerDefinedMappingSurvives()
        {
            string root = Path.Combine(Path.GetTempPath(), "gift-expression-" + Guid.NewGuid().ToString("N"));
            try
            {
                string defaults = Path.Combine(root, "defaults"); Directory.CreateDirectory(defaults);
                var seed = new VoiceDeliverySettings();
                foreach (var pair in new[] { ("annoyed", 1.02f), ("sad", .92f), ("hesitant", .93f), ("flat", 1f) })
                    seed.Styles.Add(new VoiceDeliveryStyle { Id = "ja-" + pair.Item1, Language = "ja", Emotions = new[] { pair.Item1 }, SpeedFactor = pair.Item2 });
                AtomicDocument.WriteJson(Path.Combine(defaults, "voice_delivery.json"), seed, default);
                var legacy = new VoiceDeliverySettings { Version = 1 };
                legacy.Styles.Add(new VoiceDeliveryStyle { Id = "ja-soft-original", Language = "ja", Emotions = new[] { "concern", "sad", "sleepy", "annoyed" }, SpeedFactor = .94f });
                legacy.Styles.Add(new VoiceDeliveryStyle { Id = "player-annoyed", Language = "ja", Emotions = new[] { "annoyed" }, SpeedFactor = .97f });
                AtomicDocument.WriteJson(Path.Combine(root, "Config/voice_delivery.json"), legacy, default);
                VoiceDeliverySettings.Seed(root, defaults, default); var loaded = VoiceDeliverySettings.Load(root);
                Assert.That(loaded.Version, Is.EqualTo(2)); Assert.That(loaded.Select("ja", "annoyed").Id, Is.EqualTo("player-annoyed"));
                Assert.That(loaded.Select("ja", "sad").Id, Is.EqualTo("ja-sad")); Assert.That(loaded.Select("ja", "flat").SpeedFactor, Is.EqualTo(1));
                Assert.That(File.Exists(Path.Combine(root, "Config/voice_delivery.json.previous.bak")), Is.True);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
