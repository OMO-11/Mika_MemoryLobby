using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Companion.Bgm;
using GiftCompanion.Companion.Voice;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GiftCompanion.Tests
{
    public sealed class VoicePlaybackTests
    {
        private string folder;
        private DatabaseService database;
        private AudioCache cache;
        private GameObject host;
        private CompanionVoicePlayer player;
        private BgmManager music;
        private FakeVoice fake;
        private Task setup;
        [SetUp] public void Begin()
        {
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../UserData/Validation", "voice-" + Guid.NewGuid().ToString("N")));
            database = new DatabaseService(Path.Combine(folder, "memory.db"), "main-character"); setup = Open();
            host = new GameObject("Voice test"); music = host.AddComponent<BgmManager>(); music.Configure(Array.Empty<AudioClip>(), .7f, false);
            player = host.AddComponent<CompanionVoicePlayer>(); fake = new FakeVoice();
            player.Configure(fake, cache, new VoiceRuntimeSettings(), music, null, null, .2f);
        }
        private async Task Open() { await database.InitializeAsync(default); await database.MigrateAsync(default); cache = new AudioCache(database, folder); }
        private void Configure(IVoiceSynthesizer provider) => player.Configure(provider, cache, new VoiceRuntimeSettings(), music, null, null, .2f);
        private async Task<string> Commit(string id, string body)
        {
            var turn = await database.BeginTurnAsync(id, "老师原文", InteractionSource.Companion, default);
            await database.CompleteTurnAsync(turn, body, new CharacterState(), default);
            return (await database.LoadTurnsAsync(10, true, default)).Single(x => x.RequestId == id).AssistantMessageId;
        }
        [UnityTest] public IEnumerator FirstSentencePlaysBeforeFinalTextAndFullCacheCanReplayWithoutSynthesizer()
        {
            yield return Await(setup); Configure(fake); player.Begin(1);
            yield return Await(player.AppendAsync(1, "老师，今天也一起度过吧。"));
            yield return Until(() => player.IsPlaying);
            Assert.That(player.HasLiveSession, Is.True); Assert.That(player.Completion.IsCompleted, Is.False);
            var message = Commit("stream", "老师，今天也一起度过吧。后面还有一些话。"); yield return Await(message);
            yield return Await(player.AppendAsync(1, "后面还有一些话。", true, "stream")); yield return Await(player.Completion);
            Assert.That(player.PlayedSegments, Is.EqualTo(2));
            var cached = cache.ReadPlayableAsync(message.Result, default); yield return Await(cached); Assert.That(cached.Result.Count, Is.EqualTo(2));
            int calls = fake.Calls; Configure(null); var replay = player.ReplayAsync(message.Result, default); yield return Await(replay);
            Assert.That(replay.Result, Is.True); yield return Await(player.Completion); Assert.That(fake.Calls, Is.EqualTo(calls)); Assert.That(player.PlayedSegments, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator InterruptionStopsNowAndLateOldInferenceCannotSpeakOrPublishCache()
        {
            yield return Await(setup); Configure(fake); fake.HoldFirst = true; int oldPlays = 0;
            player.SegmentPlaying += (id, _, __, ___) => { if (id == 10) oldPlays++; };
            player.Begin(10); yield return Await(player.AppendAsync(10, "老师，这是旧回复的第一句。"));
            yield return Until(() => fake.Calls == 1); player.StopSpeech(); Assert.That(player.IsPlaying, Is.False);
            player.Begin(11); var message = Commit("new", "老师，这是新的完整回复。"); yield return Await(message);
            yield return Await(player.AppendAsync(11, "老师，这是新的完整回复。", true, "new"));
            yield return Await(player.Completion); fake.Release.TrySetResult(true); yield return Await(player.DrainAsync());
            var cached = cache.ReadPlayableAsync(message.Result, default); yield return Await(cached);
            Assert.That(oldPlays, Is.EqualTo(0)); Assert.That(cached.Result.Count, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(cache.Folder, "*.wav", SearchOption.AllDirectories).Length, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator DuckingRestoresWithoutChangingPlayerVolumeOrUnmuting()
        {
            yield return Await(setup);
            music.SetVolume(.7f, true); music.DuckHoldSeconds = .1f; music.DuckReleaseSeconds = .2f;
            music.SetDucked(true); yield return Delay(.4f); Assert.That(music.DuckLevel, Is.LessThan(.6f));
            Assert.That(music.Volume, Is.EqualTo(.7f)); Assert.That(music.Muted, Is.True);
            music.SetDucked(false); yield return Delay(.5f); Assert.That(music.DuckLevel, Is.EqualTo(1).Within(.01));
            Assert.That(music.Volume, Is.EqualTo(.7f)); Assert.That(music.Muted, Is.True);
        }
        [UnityTest] public IEnumerator SynthesisFailureFallsBackAndDoesNotLeaveReplayablePartialReply()
        {
            yield return Await(setup); fake.Fail = true; Configure(fake); bool fallback = false;
            player.Ended += (_, failed, __) => fallback |= failed;
            player.Begin(21); yield return Await(player.AppendAsync(21, "老师，这句话暂时无法朗读。", true, "never-committed"));
            yield return Await(player.Completion); Assert.That(fallback, Is.True); Assert.That(player.IsPlaying, Is.False);
            var paths = database.ReadAllAudioPathsAsync(default); yield return Await(paths); Assert.That(paths.Result.Count, Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator JapaneseUsesLocalizedSpeechAndChineseSubtitlesWithOfflineReplayAndReadableAudit()
        {
            yield return Await(setup); var translator = new TestLocalizer(); string subtitle = null;
            player.Configure(fake, cache, new VoiceRuntimeSettings(), music, null, null, .2f, translator, "ja");
            player.SegmentPlaying += (_, text, __, ___) => subtitle = text;
            const string chinese = "老师，我们今天一起听音乐吧。"; var message = Commit("localized", chinese); yield return Await(message);
            Assert.That(player.Begin(30), Is.True); yield return Await(player.AppendAsync(30, chinese, true, "localized"));
            yield return Await(player.Completion); Assert.That(fake.Text, Is.EqualTo(TestLocalizer.Japanese)); Assert.That(subtitle, Is.EqualTo(chinese));
            var records = cache.ReadPlayableAsync(message.Result, default); yield return Await(records);
            var audit = GiftCompanion.Core.Config.AtomicDocument.Read<SpeechAudit>(cache.AuditPath(records.Result.Single().Path));
            Assert.That(audit.DisplayText, Is.EqualTo(chinese)); Assert.That(audit.SpokenText, Is.EqualTo(TestLocalizer.Japanese));
            Assert.That(audit.Language, Is.EqualTo("ja"));
            Configure(null); var replay = player.ReplayAsync(message.Result, default); yield return Await(replay); yield return Await(player.Completion);
            Assert.That(replay.Result, Is.True); Assert.That(fake.Calls, Is.EqualTo(1)); Assert.That(translator.Calls, Is.EqualTo(1));
            yield return Await(cache.ClearAsync(default)); Assert.That(File.Exists(cache.AuditPath(records.Result.Single().Path)), Is.False);
            var turns = database.LoadTurnsAsync(10, true, default); yield return Await(turns); Assert.That(turns.Result.Single().AssistantText, Is.EqualTo(chinese));
        }
        [UnityTest] public IEnumerator MissingOrFailedJapaneseLocalizationNeverFallsThroughToChineseSynthesis()
        {
            yield return Await(setup);
            player.Configure(fake, cache, new VoiceRuntimeSettings(), music, null, null, .2f, null, "ja"); Assert.That(player.Begin(31), Is.False);
            player.Configure(fake, cache, new VoiceRuntimeSettings(), music, null, null, .2f, new TestLocalizer { Fail = true }, "ja");
            player.Begin(32); yield return Await(player.AppendAsync(32, "老师，这句话先看中文就好。", true, "failed-ja")); yield return Await(player.Completion);
            Assert.That(fake.Calls, Is.EqualTo(0)); Assert.That(player.IsPlaying, Is.False);
            var paths = database.ReadAllAudioPathsAsync(default); yield return Await(paths); Assert.That(paths.Result, Is.Empty);
        }
        [UnityTest] public IEnumerator SentenceBreathIsMeasuredFromActualAudioEndAndStopsImmediatelyOnCancel()
        {
            yield return Await(setup); Configure(fake);
            var starts = new System.Collections.Generic.List<double>();
            player.SegmentPlaying += (_, __, ___, ____) => starts.Add(Time.realtimeSinceStartupAsDouble);
            const string body = "老师，这是有呼吸的第一句话。老师，这是下一句轻轻的回复。";
            var message = Commit("breath", body); yield return Await(message);
            player.Begin(41); yield return Await(player.AppendAsync(41, body, true, "breath")); yield return Await(player.Completion);
            Assert.That(starts.Count, Is.EqualTo(2)); Assert.That(starts[1] - starts[0], Is.GreaterThanOrEqualTo(.5 + .45 - .025));
            starts.Clear(); player.Begin(42); yield return Await(player.AppendAsync(42, body));
            yield return Until(() => player.PlayedSegments == 1 && !player.IsPlaying);
            player.StopSpeech(); yield return Await(player.DrainAsync()); Assert.That(player.PlayedSegments, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator VoiceIntentReachesTranslationSynthesisAndAuditSeparatelyFromMood()
        {
            yield return Await(setup); var translator = new IntentLocalizer(); var styled = new IntentVoice();
            player.Configure(styled, cache, new VoiceRuntimeSettings(), music, null, null, .2f, translator, "ja");
            var message = Commit("voice-intent", "老师，我还没有想好呢。"); yield return Await(message);
            player.Begin(51); player.SetEmotion(51, "concern"); player.SetVoiceIntent(51, "hesitant");
            yield return Await(player.AppendAsync(51, "老师，我还没有想好呢。", true, "voice-intent")); yield return Await(player.Completion);
            Assert.That(translator.Intent, Is.EqualTo("hesitant")); Assert.That(styled.Intent, Is.EqualTo("hesitant"));
            var records = cache.ReadPlayableAsync(message.Result, default); yield return Await(records);
            var audit = GiftCompanion.Core.Config.AtomicDocument.Read<SpeechAudit>(cache.AuditPath(records.Result.Single().Path));
            Assert.That(audit.Emotion, Is.EqualTo("concern")); Assert.That(audit.VoiceIntent, Is.EqualTo("hesitant"));
        }
        private sealed class IntentLocalizer : IExpressiveSpeechLocalizer
        {
            public string Intent;
            public Task<LocalizedSpeech> LocalizeAsync(string text, CancellationToken ct) => throw new InvalidOperationException("Intent must be supplied");
            public Task<LocalizedSpeech> LocalizeAsync(string text, string intent, CancellationToken ct)
            { Intent = intent; return Task.FromResult(new LocalizedSpeech { Language = "ja", Text = "先生、まだ決めていないんだ。" }); }
        }
        private sealed class IntentVoice : IStyledVoiceSynthesizer
        {
            public string Intent; public string ProfileId => "ja:intent-test";
            public Task<byte[]> SynthesizeAsync(string text, CancellationToken ct) => throw new InvalidOperationException("Intent must be supplied");
            public Task<VoiceSynthesisResult> SynthesizeWithStyleAsync(string text, string intent, CancellationToken ct)
            { Intent = intent; return Task.FromResult(new VoiceSynthesisResult { Wave = Wav(), ReferenceId = "ja-hesitant", SpeedFactor = .93f }); }
        }
        private sealed class TestLocalizer : ISpeechLocalizer
        {
            public const string Japanese = "先生、今日は一緒に音楽を聴こうね。";
            public int Calls; public bool Fail;
            public Task<LocalizedSpeech> LocalizeAsync(string chinese, CancellationToken ct)
            {
                Calls++; if (Fail) throw new IOException("Translation unavailable");
                return Task.FromResult(new LocalizedSpeech { Text = Japanese, Language = "ja", Model = "test" });
            }
        }
        private sealed class FakeVoice : IVoiceSynthesizer
        {
            public string ProfileId => "zh:play-test";
            public int Calls; public bool HoldFirst, Fail; public string Text;
            public readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>();
            public async Task<byte[]> SynthesizeAsync(string text, CancellationToken ct)
            {
                Text = text; int call = ++Calls; if (HoldFirst && call == 1) await Release.Task;
                await Task.Delay(30); if (Fail) throw new IOException("Test unavailable");
                // Deliberately returns a late result after cancellation to exercise generation filtering.
                return Wav();
            }
        }
        internal static byte[] Wav()
        {
            const int frames = 24000; using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
            for (int i = 0; i < frames; i++) writer.Write((short)(Math.Sin(i * Math.PI * 2 * 440 / 48000) * 500)); return bytes.ToArray();
        }
        private static IEnumerator Await(Task task)
        { float end = Time.realtimeSinceStartup + 10; while (!task.IsCompleted) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end)); yield return null; } if (task.IsFaulted) throw task.Exception; Assert.That(task.IsCanceled, Is.False); }
        private static IEnumerator Until(Func<bool> condition)
        { float end = Time.realtimeSinceStartup + 5; while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end)); yield return null; } }
        private static IEnumerator Delay(float seconds)
        { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        [UnityTearDown] public IEnumerator End()
        {
            fake?.Release.TrySetResult(true); player?.StopSpeech(); if (player != null) yield return Await(player.DrainAsync());
            if (host != null) Object.Destroy(host); yield return null; database?.Dispose();
            string boundary = Path.GetFullPath(Path.Combine(Application.dataPath, "../UserData/Validation")) + Path.DirectorySeparatorChar;
            if (!folder.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Test cleanup boundary");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
