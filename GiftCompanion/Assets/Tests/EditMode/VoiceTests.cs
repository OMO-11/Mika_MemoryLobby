using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class VoiceTests
    {
        private string folder;
        private DatabaseService database;
        [SetUp] public void Begin() => folder = Path.Combine(Path.GetTempPath(), "gift-voice-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void End() { database?.Dispose(); database = null; if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        private async Task<AudioCache> Open()
        {
            database = new DatabaseService(Path.Combine(folder, "memory.db"), "main-character");
            await database.InitializeAsync(default); await database.MigrateAsync(default); return new AudioCache(database, folder);
        }
        private async Task<string> Complete(string id)
        {
            var turn = await database.BeginTurnAsync(id, "老师原文", InteractionSource.Companion, default);
            await database.CompleteTurnAsync(turn, "完整回复", new CharacterState(), default);
            return (await database.LoadTurnsAsync(10, true, default)).Single(x => x.RequestId == id).AssistantMessageId;
        }
        internal static byte[] Wav(int frames = 4800)
        {
            using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + frames * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
            for (int i = 0; i < frames; i++) writer.Write((short)(Math.Sin(i * Math.PI * 2 * 440 / 48000) * 1000));
            return bytes.ToArray();
        }
        [Test] public void SegmentsFollowSentenceBoundariesAndFlushTailAcrossArbitraryDeltas()
        {
            var segmenter = new SpeechSegmenter(new VoiceRuntimeSettings());
            Assert.That(segmenter.Append("老师，今天也一起").Count, Is.EqualTo(0));
            var first = segmenter.Append("度过吧。接下来还有一件事"); Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(first[0].DisplayText, Is.EqualTo("老师，今天也一起度过吧。"));
            var tail = segmenter.Append("", true); Assert.That(tail.Single().DisplayText, Is.EqualTo("接下来还有一件事")); Assert.That(tail[0].Index, Is.EqualTo(1));
        }
        [Test] public void LongUnpunctuatedUnicodeIsBoundedAndCleanupDoesNotExposeControlMarkup()
        {
            string text = new string('好', 69) + "🌙" + new string('呀', 120);
            var parts = new SpeechSegmenter(new VoiceRuntimeSettings()).Append(text, true);
            Assert.That(string.Concat(parts.Select(p => p.DisplayText)), Is.EqualTo(text));
            Assert.That(parts.All(p => p.DisplayText.Length <= 70 && !char.IsHighSurrogate(p.DisplayText.Last())), Is.True);
            Assert.That(SpeechSegmenter.CleanForSpeech("<META emotion=\"happy\">\n**老师**，看[这里](https://example.test)！\u0001"), Is.EqualTo("老师，看这里！"));
        }
        [Test] public void WaveDecoderChecksActualRiffLengthAndPcmFrames()
        {
            var wav = Wav(); var decoded = WaveAudio.Decode(wav);
            Assert.That(decoded.Frequency, Is.EqualTo(48000)); Assert.That(decoded.Channels, Is.EqualTo(1)); Assert.That(decoded.Duration, Is.EqualTo(.1).Within(.0001));
            Assert.That(decoded.Samples.Any(x => Math.Abs(x) > .01f), Is.True);
            Assert.Throws<InvalidDataException>(() => WaveAudio.Decode(wav.Take(wav.Length-1).ToArray()));
            wav[20] = 2; Assert.Throws<InvalidDataException>(() => WaveAudio.Decode(wav));
        }
        [Test] public async Task CacheBelongsToCommittedMessageAndMissingFileCannotAdvertiseReplay()
        {
            var cache = await Open(); string message = await Complete("cache"); string session = Guid.NewGuid().ToString("N");
            var one = await cache.StageAsync(session, new SpeechSegment(0, "第一句", "第一句"), Wav(), "zh:test", default);
            var two = await cache.StageAsync(session, new SpeechSegment(1, "第二句", "第二句"), Wav(), "zh:test", default);
            await cache.CommitAsync("cache", new[] {one, two}, default);
            Assert.That((await cache.ReadPlayableAsync(message, default)).Count, Is.EqualTo(2));
            File.Delete(cache.Resolve(two.Path)); Assert.That(await cache.ReadPlayableAsync(message, default), Is.Empty);
            Assert.That((await database.ReadAudioRecordsAsync(message, default)).Count, Is.EqualTo(2));
        }
        [Test] public async Task FailedTurnCannotOwnCacheAndRecoveryPreservesIndexedAudioAndUnknownFiles()
        {
            var cache = await Open(); string message = await Complete("saved");
            var saved = await cache.StageAsync(Guid.NewGuid().ToString("N"), new SpeechSegment(0, "已保存", "已保存"), Wav(), "zh:test", default);
            await cache.CommitAsync("saved", new[] {saved}, default);
            var orphan = await cache.StageAsync(Guid.NewGuid().ToString("N"), new SpeechSegment(0, "未完成", "未完成"), Wav(), "zh:test", default);
            Assert.ThrowsAsync<InvalidDataException>(() => cache.CommitAsync("missing", new[] {orphan}, default));
            string unknown = Path.Combine(cache.Folder, "玩家笔记.txt"); File.WriteAllText(unknown, "保留");
            await cache.RecoverAsync(default); Assert.That(File.Exists(cache.Resolve(orphan.Path)), Is.False);
            Assert.That((await cache.ReadPlayableAsync(message, default)).Count, Is.EqualTo(1)); Assert.That(File.ReadAllText(unknown), Is.EqualTo("保留"));
            await cache.ClearAsync(default); Assert.That(await cache.ReadPlayableAsync(message, default), Is.Empty);
            Assert.That((await database.LoadTurnsAsync(10, true, default)).Count, Is.EqualTo(1)); Assert.That(File.Exists(unknown), Is.True);
        }
        [Test] public async Task VersionEightMigrationBacksUpAndPreservesHistoryAndDiaryTables()
        {
            await Open(); await Complete("before"); database.Dispose(); database = null;
            using (var seed = new SqliteConnection(Path.Combine(folder, "memory.db"))) seed.Execute("DROP TABLE agent_tool_runs; DROP TABLE audio_records; PRAGMA user_version=8;");
            await Open(); Assert.That(database.SchemaVersion, Is.EqualTo(DatabaseService.CurrentSchemaVersion));
            Assert.That((await database.LoadTurnsAsync(10, true, default)).Single().RequestId, Is.EqualTo("before"));
            Assert.That(await database.ListDiariesAsync(default), Is.Empty);
            using var backup = new SqliteConnection(Directory.GetFiles(folder, "*.pre-v" + DatabaseService.CurrentSchemaVersion + "-*.bak").Single()); Assert.That(backup.Version, Is.EqualTo(8));
        }
        [Test] public void VoiceConfigurationFileIsIndependentAndInvalidOriginalIsPreserved()
        {
            var cfg = VoiceRuntimeSettings.Load(folder, default); Assert.That(cfg.Profile, Is.EqualTo("voice.json"));
            string file = Path.Combine(folder, "Config", "voice_runtime.json"); File.WriteAllText(file, "{\"Port\":0}");
            Assert.Throws<InvalidDataException>(() => VoiceRuntimeSettings.Load(folder, default)); Assert.That(File.ReadAllText(file), Is.EqualTo("{\"Port\":0}"));
            Assert.Throws<InvalidDataException>(() => new AudioCache(null, folder).Resolve("AudioCache/../../memory.db"));
        }
        [Test] public async Task InvalidInterpreterFailsWithoutLeavingAnUnstartedProcessObject()
        {
            string runtime = Path.Combine(folder, "Runtime/GPT-SoVITS"); Directory.CreateDirectory(Path.Combine(runtime, "runtime"));
            File.WriteAllText(Path.Combine(runtime, "runtime/python.exe"), "not an executable"); File.WriteAllText(Path.Combine(runtime, "api_v2.py"), "test");
            File.WriteAllText(Path.Combine(runtime, "tts_infer.yaml"), "test"); File.WriteAllText(Path.Combine(runtime, "reference.wav"), "test");
            File.WriteAllText(Path.Combine(runtime, "voice.json"), JsonConvert.SerializeObject(new VoiceProfile {Language="zh", PromptLanguage="zh", TextLanguage="zh", PromptText="老师", ReferenceAudio="reference.wav"}));
            string host = Path.Combine(folder, "host.py"); File.WriteAllText(host, "test");
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            using var manager = new VoiceRuntimeManager(folder, host, folder, new VoiceRuntimeSettings {Port=port}, null);
            Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(() => manager.EnsureReadyAsync(default));
            Assert.That(manager.State, Is.EqualTo(VoiceRuntimeState.Unavailable)); Assert.That(manager.OwnedProcessId, Is.EqualTo(0));
            await manager.StopAsync(); Assert.That(manager.State, Is.EqualTo(VoiceRuntimeState.Stopped));
        }
        [Test] public async Task OccupiedPortNeverAdoptsOrStopsForeignListener()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            try
            {
                string runtime = Path.Combine(folder, "Runtime/GPT-SoVITS"); Directory.CreateDirectory(Path.Combine(runtime, "runtime"));
                File.WriteAllText(Path.Combine(runtime, "runtime/python.exe"), "not executed"); File.WriteAllText(Path.Combine(runtime, "api_v2.py"), "not executed");
                File.WriteAllText(Path.Combine(runtime, "tts_infer.yaml"), "test"); File.WriteAllText(Path.Combine(runtime, "reference.wav"), "test");
                File.WriteAllText(Path.Combine(runtime, "voice.json"), JsonConvert.SerializeObject(new VoiceProfile {Language="zh", PromptLanguage="zh", TextLanguage="zh", PromptText="老师", ReferenceAudio="reference.wav"}));
                string host = Path.Combine(folder, "host.py"); File.WriteAllText(host, "not executed");
                using var manager = new VoiceRuntimeManager(folder, host, folder, new VoiceRuntimeSettings {Port=((IPEndPoint)listener.LocalEndpoint).Port}, null);
                Assert.ThrowsAsync<InvalidOperationException>(() => manager.EnsureReadyAsync(default));
                Assert.That(manager.OwnedProcessId, Is.EqualTo(0)); await manager.StopAsync();
                using var check = new TcpClient(); await check.ConnectAsync("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port); Assert.That(check.Connected, Is.True);
            }
            finally { listener.Stop(); }
        }
    }
}
