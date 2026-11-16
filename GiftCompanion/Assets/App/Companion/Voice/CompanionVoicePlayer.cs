using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;
using ILogger = GiftCompanion.Core.Logging.ILogger;
using UnityEngine;

namespace GiftCompanion.Companion.Voice
{
    // Scene-owned playback; the shared runtime manager owns the single GPU inference gate.
    public sealed class CompanionVoicePlayer : MonoBehaviour
    {
        private sealed class Session
        {
            public readonly string Folder = Guid.NewGuid().ToString("N");
            public long Id;
            public bool Replay, Final, ProducerDone, Saved, Failed;
            public string RequestId;
            public IVoiceSynthesizer Synthesizer;
            public ISpeechLocalizer Localizer;
            public string Language;
            public string Emotion = "neutral";
            public string VoiceIntent = "neutral";
            public SpeechSegmenter Segmenter;
            public readonly CancellationTokenSource Lifetime = new CancellationTokenSource();
            public readonly Queue<SpeechSegment> Requests = new Queue<SpeechSegment>();
            public readonly Queue<AudioRecord> Ready = new Queue<AudioRecord>();
            public readonly List<AudioRecord> Records = new List<AudioRecord>();
            public readonly SemaphoreSlim RequestPulse = new SemaphoreSlim(0), ReadyPulse = new SemaphoreSlim(0);
            public SemaphoreSlim RequestSlots, ReadySlots;
            public Task Producer, Playback, Completion;
        }
        private Session current;
        private readonly List<Task> pending = new List<Task>();
        private IVoiceSynthesizer synthesizer;
        private ISpeechLocalizer localizer;
        private string language = "zh";
        private AudioCache cache;
        private VoiceRuntimeSettings settings;
        private ILogger logger;
        private Bgm.BgmManager music;
        private Func<CompanionSpeechDriver> speechDriver;
        private CompanionSpeechDriver playingDriver;
        private AudioSource source;
        private long replayId;
        public event Action<long, string, double, bool> SegmentPlaying;
        public event Action<long, bool, bool> Ended;
        public bool HasLiveSession => current != null && !current.Replay && current.Completion?.IsCompleted != true && !current.Failed && !current.Lifetime.IsCancellationRequested;
        public bool IsPlaying => source != null && source.isPlaying;
        public bool IsReplaying => current != null && current.Replay && current.Completion?.IsCompleted != true && !current.Lifetime.IsCancellationRequested;
        public Task Completion => current?.Completion ?? Task.CompletedTask;
        public int PlayedSegments { get; private set; }
        public long FirstAudioMilliseconds { get; private set; }
        private double begunAt;
        public void Configure(IVoiceSynthesizer synthesizer, AudioCache cache, VoiceRuntimeSettings settings, Bgm.BgmManager music,
            Func<CompanionSpeechDriver> speechDriver, ILogger logger, float volume, ISpeechLocalizer localizer = null, string language = "zh")
        {
            this.synthesizer = synthesizer; this.cache = cache; this.settings = settings; this.music = music;
            this.speechDriver = speechDriver; this.logger = logger;
            if (language != "zh" && language != "ja") throw new ArgumentException("Voice language unsupported.");
            this.localizer = localizer; this.language = language;
            if (source == null)
            {
                var audio = new GameObject("Companion Voice Audio"); audio.transform.SetParent(transform, false);
                source = audio.AddComponent<AudioSource>();
            }
            source.playOnAwake = false; source.spatialBlend = 0; source.loop = false; source.dopplerLevel = 0;
            SetVolume(volume);
        }
        public void SetVolume(float volume) { if (source != null) source.volume = Mathf.Clamp01(volume); }
        public bool Begin(long id)
        {
            StopSpeech();
            if (synthesizer == null || cache == null || language == "ja" && localizer == null) return false;
            // Model loading can take tens of seconds. Keep this turn readable as text;
            // later turns use voice once the separately-started service becomes ready.
            if (synthesizer is VoiceRuntimeManager runtime && (runtime.State != VoiceRuntimeState.Ready || runtime.OwnedProcessId == 0)) return false;
            var session = NewSession(id, false); current = session; PlayedSegments = 0; FirstAudioMilliseconds = 0; begunAt = Time.realtimeSinceStartupAsDouble;
            session.Producer = ProduceAsync(session); session.Playback = PlayAsync(session); Track(session); return true;
        }
        private Session NewSession(long id, bool replay) => new Session { Id = id, Replay = replay, Segmenter = new SpeechSegmenter(settings),
            Synthesizer = synthesizer, Localizer = localizer, Language = language,
            RequestSlots = new SemaphoreSlim(settings.MaximumQueuedSegments), ReadySlots = new SemaphoreSlim(settings.MaximumBufferedAudio) };
        private void Track(Session session)
        { session.Completion = FinishAsync(session); pending.Add(session.Completion); pending.RemoveAll(task => task.IsCompleted); }
        public void SetEmotion(long id, string emotion)
        { if (current?.Id == id && SpeechEmotion.IsValid(emotion)) { current.Emotion = emotion; current.VoiceIntent = emotion; } }
        public void SetVoiceIntent(long id, string intent)
        { if (current?.Id == id && SpeechEmotion.IsValid(intent)) current.VoiceIntent = intent; }
        public async Task AppendAsync(long id, string body, bool final = false, string requestId = null)
        {
            var session = current;
            if (session == null || session.Id != id || session.Replay || session.Failed || session.Lifetime.IsCancellationRequested) return;
            try
            {
                foreach (var segment in session.Segmenter.Append(body, final))
                {
                    if (!await session.RequestSlots.WaitAsync(0, session.Lifetime.Token))
                    { Fail(session, new InvalidOperationException("Speech queue full; keep streaming text.")); return; }
                    session.Requests.Enqueue(segment); session.RequestPulse.Release();
                }
                if (final) { session.RequestId = requestId; session.Final = true; session.RequestPulse.Release(); }
            }
            catch (OperationCanceledException) { }
        }
        private async Task ProduceAsync(Session session)
        {
            var ct = session.Lifetime.Token;
            try
            {
                while (true)
                {
                    await session.RequestPulse.WaitAsync(ct);
                    if (session.Requests.Count == 0) { if (session.Final) break; continue; }
                    var segment = session.Requests.Dequeue(); session.RequestSlots.Release();
                    LocalizedSpeech localized = null;
                    var timing = System.Diagnostics.Stopwatch.StartNew();
                    string emotion = session.VoiceIntent;
                    if (session.Language == "ja") localized = session.Localizer is IExpressiveSpeechLocalizer expressive
                        ? await expressive.LocalizeAsync(segment.SpokenText, emotion, ct) : await session.Localizer.LocalizeAsync(segment.SpokenText, ct);
                    ct.ThrowIfCancellationRequested();
                    logger?.Info("speech localization_ms=" + timing.ElapsedMilliseconds + " localized=" + (session.Language == "ja"));
                    string spoken = localized?.Text ?? segment.SpokenText;
                    if (session.Language == "ja" && (localized?.Language != "ja" || string.IsNullOrWhiteSpace(spoken)))
                        throw new InvalidDataException("Japanese speech requires localized text.");
                    timing.Restart();
                    var synthesis = session.Synthesizer is IStyledVoiceSynthesizer styled ? await styled.SynthesizeWithStyleAsync(spoken, emotion, ct)
                        : new VoiceSynthesisResult { Wave = await session.Synthesizer.SynthesizeAsync(spoken, ct), ReferenceId = "original" };
                    ct.ThrowIfCancellationRequested();
                    logger?.Info("speech synthesis_ms=" + timing.ElapsedMilliseconds);
                    var audit = new SpeechAudit { DisplayText = segment.DisplayText, SpokenText = spoken, Language = session.Language,
                        ProfileId = session.Synthesizer.ProfileId, TranslationModel = localized?.Model, InstructionHash = localized?.InstructionHash,
                        Emotion = session.Emotion, VoiceIntent = emotion, ReferenceId = synthesis.ReferenceId, SpeedFactor = synthesis.SpeedFactor };
                    var record = await cache.StageAsync(session.Folder, segment, synthesis.Wave, session.Synthesizer.ProfileId, ct, audit);
                    await session.ReadySlots.WaitAsync(ct);
                    session.Records.Add(record); session.Ready.Enqueue(record); session.ReadyPulse.Release();
                }
                ct.ThrowIfCancellationRequested();
                if (session.Records.Count > 0) { await cache.CommitAsync(session.RequestId, session.Records, ct); session.Saved = true; }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Fail(session, error); }
            finally { session.ProducerDone = true; session.ReadyPulse.Release(); }
        }
        private void Fail(Session session, Exception error)
        {
            session.Failed = true; logger?.Error("voice playback text_fallback=true", error);
            session.Lifetime.Cancel();
            if (ReferenceEquals(current, session)) { StopSource(); Ended?.Invoke(session.Id, true, session.Replay); }
        }
        private async Task PlayAsync(Session session)
        {
            var ct = session.Lifetime.Token;
            double nextAllowed = 0;
            try
            {
                while (true)
                {
                    await session.ReadyPulse.WaitAsync(ct);
                    if (session.Ready.Count == 0) { if (session.ProducerDone) break; continue; }
                    var record = session.Ready.Dequeue(); session.ReadySlots.Release(); ct.ThrowIfCancellationRequested();
                    byte[] bytes = await Task.Run(() => File.ReadAllBytes(cache.Resolve(record.Path)), ct);
                    var wave = WaveAudio.Decode(bytes); ct.ThrowIfCancellationRequested();
                    // Count time already spent waiting for synthesis as breathing time, too.
                    while (Time.realtimeSinceStartupAsDouble < nextAllowed) { ct.ThrowIfCancellationRequested(); await Task.Yield(); }
                    if (!ReferenceEquals(current, session) || this == null) return;
                    var clip = AudioClip.Create("Companion speech", wave.Samples.Length / wave.Channels, wave.Channels, wave.Frequency, false);
                    try
                    {
                        if (!clip.SetData(wave.Samples, 0)) throw new InvalidDataException("Voice PCM unavailable.");
                        var template = speechDriver?.Invoke();
                        if (template != null)
                        {
                            // The validation driver's envelope cache has scene lifetime. A short-lived driver
                            // reuses that proven behavior while bounding cached PCM to the current segment.
                            playingDriver = gameObject.AddComponent<CompanionSpeechDriver>();
                            playingDriver.voice = source; playingDriver.expression = template.expression; playingDriver.motion = template.motion;
                            playingDriver.MouthChanged = template.MouthChanged;
                            playingDriver.Play(clip);
                        }
                        else { source.clip = clip; source.Play(); }
                        if (!source.isPlaying) throw new InvalidOperationException("Voice audio did not start.");
                        music?.SetDucked(true); PlayedSegments++;
                        if (PlayedSegments == 1)
                        {
                            FirstAudioMilliseconds = (long)((Time.realtimeSinceStartupAsDouble - begunAt) * 1000);
                            logger?.Info("speech first_playback_ms=" + FirstAudioMilliseconds + " replay=" + session.Replay);
                        }
                        SegmentPlaying?.Invoke(session.Id, record.DisplayText, wave.Duration, session.Replay);
                        while (source != null && source.isPlaying)
                        { ct.ThrowIfCancellationRequested(); if (!ReferenceEquals(current, session)) return; await Task.Yield(); }
                    }
                    finally
                    {
                        if (ReferenceEquals(current, session)) StopSource(false);
                        if (clip != null) Destroy(clip);
                    }
                    nextAllowed = Time.realtimeSinceStartupAsDouble + settings.PauseAfter(record.DisplayText);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { Fail(session, error); }
        }
        private async Task FinishAsync(Session session)
        {
            try
            {
                await Task.WhenAll(session.Producer, session.Playback);
                if (!session.Replay && !session.Saved) await cache.DiscardSessionAsync(session.Folder);
            }
            catch (Exception error) { logger?.Error("voice cleanup pending=true", error); }
            finally
            {
                if (ReferenceEquals(current, session) && this != null)
                { StopSource(); Ended?.Invoke(session.Id, session.Failed || !session.Replay && session.Records.Count == 0, session.Replay); }
                // Session tokens remain readable by late append callbacks; bounded by the current screen lifetime.
            }
        }
        public async Task<bool> ReplayAsync(string messageId, CancellationToken ct)
        {
            if (HasLiveSession) return false;
            var records = await cache.ReadPlayableAsync(messageId, ct); ct.ThrowIfCancellationRequested();
            if (records.Count == 0 || this == null || HasLiveSession) return false;
            StopSpeech(); var session = NewSession(--replayId, true); current = session; session.Saved = true;
            PlayedSegments = 0; begunAt = Time.realtimeSinceStartupAsDouble;
            session.Producer = LoadReplayAsync(session, records); session.Playback = PlayAsync(session); Track(session); return true;
        }
        private async Task LoadReplayAsync(Session session, IReadOnlyList<AudioRecord> records)
        {
            try
            {
                foreach (var record in records)
                { await session.ReadySlots.WaitAsync(session.Lifetime.Token); session.Ready.Enqueue(record); session.ReadyPulse.Release(); }
            }
            catch (OperationCanceledException) { }
            finally { session.ProducerDone = true; session.ReadyPulse.Release(); }
        }
        public void StopSpeech()
        { current?.Lifetime.Cancel(); StopSource(); }
        private void StopSource(bool releaseDuck = true)
        {
            if (playingDriver != null)
            {
                playingDriver.Stop(); playingDriver.voice = null; playingDriver.expression = null; playingDriver.motion = null;
                Destroy(playingDriver); playingDriver = null;
            }
            if (source != null) { source.Stop(); source.clip = null; }
            if (releaseDuck) music?.SetDucked(false);
        }
        public async Task DrainAsync() { await Task.WhenAll(pending.ToArray()); pending.RemoveAll(task => task.IsCompleted); }
        private void OnDisable() { StopSpeech(); }
    }
}
