using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GiftCompanion.Companion.Bgm
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class BgmManager : MonoBehaviour
    {
        private AudioSource active, incoming;
        private AudioClip[] clips = Array.Empty<AudioClip>();
        private readonly HashSet<AudioClip> owned = new HashSet<AudioClip>();
        private AudioClip pending;
        private bool stopping, transitioning;
        private float level, transitionDuration;
        private double transitionAt;
        private float previousTime;
        private int current = -1;
        private bool ducked;
        private float duckLevel = 1, duckUntil;
        public float DuckFactor { get; set; } = .5f;
        public float DuckAttackSeconds { get; set; } = .25f;
        public float DuckReleaseSeconds { get; set; } = .8f;
        public float DuckHoldSeconds { get; set; } = .4f;
        public float DuckLevel => duckLevel;
        public void SetDucked(bool value) { ducked = value; if (value) duckUntil = Time.unscaledTime + DuckHoldSeconds; }
        public float Volume { get; private set; } = .35f;
        public bool Muted { get; private set; }
        public bool Paused { get; private set; }
        private double pausedAt;
        public float FadeSeconds { get; set; } = 1.5f;
        public bool HasPlaylist => clips.Length > 0 || active?.clip != null || pending != null;
        public AudioSource Source => active;
        public bool Transitioning => transitioning;
        public int RepeatCount { get; private set; }
        public int CurrentTrackIndex => current;
        private void EnsureSources()
        {
            if (active != null) return;
            active = GetComponent<AudioSource>(); incoming = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] {active, incoming})
            { source.playOnAwake = false; source.spatialBlend = 0; source.loop = true; source.volume = 0; source.dopplerLevel = 0; }
        }
        public void Configure(IEnumerable<AudioClip> playlist, float volume, bool muted)
        {
            EnsureSources(); ReleaseAll(); clips = (playlist ?? Array.Empty<AudioClip>()).Where(c => c != null).Take(128).ToArray();
            current = clips.Length == 0 ? -1 : 0; RepeatCount = 0; stopping = false; level = 0;
            SetVolume(volume, muted);
            if (current >= 0) PlayClip(clips[current]);
        }
        public void SetVolume(float volume, bool muted) { Volume = Mathf.Clamp01(volume); Muted = muted; }
        public void SelectTrack(int index) { if (index >= 0 && index < clips.Length) { current = index; PlayClip(clips[index]); } }
        public void PlayClip(AudioClip clip, bool takeOwnership = false)
        {
            if (clip == null) return; EnsureSources();
            if (Paused) SetPaused(false);
            if (takeOwnership) owned.Add(clip);
            if (pending != null && pending != clip && pending != active.clip && pending != incoming.clip) ReleaseOwned(pending);
            pending = clip; stopping = false;
        }
        public void SetPaused(bool value)
        {
            EnsureSources();if(Paused==value)return;Paused=value;
            if(value){pausedAt=AudioSettings.dspTime;active.Pause();incoming.Pause();}
            else{transitionAt+=AudioSettings.dspTime-pausedAt;active.UnPause();incoming.UnPause();}
        }
        public void Resume() { stopping = false; if(Paused){SetPaused(false);return;} if (active != null && active.clip != null && !active.isPlaying) active.Play(); }
        private float CrossfadeLength(AudioClip next) => Mathf.Max(.01f, Mathf.Min(Mathf.Clamp(FadeSeconds, .2f, 8), active.clip.length * .25f, next.length * .25f));
        private void Begin(AudioClip next)
        {
            if (active.clip == null) { active.clip = next; active.volume = 0; previousTime = 0; active.Play(); return; }
            incoming.clip = next; incoming.volume = 0; incoming.time = 0; incoming.Play();
            transitionDuration = CrossfadeLength(next); transitionAt = AudioSettings.dspTime; transitioning = true;
        }
        private void Update()
        {
            if (active == null || Paused) return;
            if (active.clip == null && pending == null) level = 0;
            level = Mathf.MoveTowards(level, stopping || Muted || (active.clip == null && pending == null) ? 0 : Volume, Time.unscaledDeltaTime * Mathf.Max(.001f, Volume) / Mathf.Max(.2f, FadeSeconds));
            if (!stopping && !transitioning && pending != null)
            { var next = pending; pending = null; if (next != active.clip || !active.isPlaying) Begin(next); }
            if (active.clip != null && active.isPlaying)
            {
                if (previousTime > active.time + .02f) RepeatCount++;
                previousTime = active.time;
            }
            float blend = transitioning ? Mathf.Clamp01((float)(AudioSettings.dspTime - transitionAt) / transitionDuration) : 0;
            if (ducked) duckUntil = Time.unscaledTime + Mathf.Max(0, DuckHoldSeconds);
            float targetDuck = ducked || Time.unscaledTime < duckUntil ? Mathf.Clamp(DuckFactor, .1f, 1) : 1;
            duckLevel = Mathf.MoveTowards(duckLevel, targetDuck, Time.unscaledDeltaTime / Mathf.Max(.05f, targetDuck < duckLevel ? DuckAttackSeconds : DuckReleaseSeconds));
            active.volume = level * duckLevel * (1 - blend) * LoopEnvelope(active); incoming.volume = level * duckLevel * blend * LoopEnvelope(incoming);
            if (transitioning && blend >= 1)
            {
                var old = active; active = incoming; incoming = old; transitioning = false;
                var clip = incoming.clip; incoming.Stop(); incoming.clip = null; incoming.volume = 0;
                if (clip != active.clip && clip != pending) ReleaseOwned(clip);
                previousTime = active.time;
            }
        }
        private float LoopEnvelope(AudioSource source)
        {
            if (source.clip == null) return 0;
            float fade = Mathf.Min(Mathf.Clamp(FadeSeconds, .2f, 8), source.clip.length * .25f);
            return Mathf.Clamp01(Mathf.Min(source.time, source.clip.length - source.time) / Mathf.Max(.01f, fade));
        }
        public async Task FadeOutAsync(CancellationToken ct)
        {
            stopping = true;
            if(Paused){active.Stop();incoming.Stop();level=0;return;}
            while (this != null && active != null && level > .001f) { ct.ThrowIfCancellationRequested(); await Task.Yield(); }
            if (active != null) { active.Stop(); incoming.Stop(); active.volume = incoming.volume = 0; }
        }
        private void ReleaseOwned(AudioClip clip) { if (clip != null && owned.Remove(clip)) Destroy(clip); }
        private void ReleaseAll()
        {
            if (active != null) { active.Stop(); incoming.Stop(); active.clip = incoming.clip = null; active.volume = incoming.volume = 0; }
            foreach (var clip in owned) if (clip != null) Destroy(clip); owned.Clear();
            pending = null; transitioning = false; level = 0; ducked = false; duckLevel = 1; duckUntil = 0;
            Paused=false;
        }
        private void OnDisable() { ReleaseAll(); }
    }
}
