using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;
using ILogger = GiftCompanion.Core.Logging.ILogger;
using UnityEngine;
using UnityEngine.Networking;

namespace GiftCompanion.Companion.Bgm
{
    public sealed class BgmFilePlayer : MonoBehaviour
    {
        private ContentLibrary library;
        private BgmManager player;
        private ILogger logger;
        private CancellationTokenSource loading;
        private long generation;
        public bool Ready { get; private set; }
        public string CurrentId { get; private set; }
        public string LastError { get; private set; }
        public async Task InitializeAsync(ContentLibrary library, BgmManager player, ILogger logger, CancellationToken ct)
        {
            this.library = library; this.player = player; this.logger = logger;
            try { if (library.Music.Tracks.Count > 0) await SelectAsync(library.Music.SelectedTrackId, ct); }
            catch (OperationCanceledException) { }
            catch (Exception error) { LastError = "当前音乐没能打开，请另选歌曲。"; logger.Error("bgm initial file unavailable", error); }
            finally { Ready = true; }
        }
        public async Task SelectAsync(string id, CancellationToken ct, bool force = false)
        {
            var track = library.Music.Tracks.Single(t => t.Id == id);
            if (!force && CurrentId == id && player.Source?.clip != null)
            { await library.SelectMusicAsync(id, ct); return; }
            long request = ++generation; loading?.Cancel();
            using var scope = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken); loading = scope;
            AudioClip clip = null;
            try
            {
                clip = await LoadAsync(library.Resolve(track.Path), scope.Token, library.Music.MaximumDurationMinutes);
                scope.Token.ThrowIfCancellationRequested(); if (this == null || request != generation) return;
                await library.SelectMusicAsync(id, scope.Token); var catalog = library.Music;
                player.FadeSeconds = (float)catalog.FadeSeconds; player.PlayClip(clip, true); clip = null;
                CurrentId = id; LastError = null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { LastError = "这首音乐暂时没能打开，原来的音乐会继续。"; logger?.Error("bgm file failed", error); throw; }
            finally { if (clip != null) Destroy(clip); if (ReferenceEquals(loading, scope)) loading = null; }
        }
        public static async Task<AudioClip> LoadAsync(string path, CancellationToken ct, int maximumMinutes = 180)
        {
            if (!File.Exists(path) || !ContentLibrary.IsAudio(path)) throw new InvalidDataException("Audio file unavailable.");
            string extension = Path.GetExtension(path).ToLowerInvariant();
            AudioType type = extension == ".ogg" ? AudioType.OGGVORBIS : extension == ".mp3" ? AudioType.MPEG : AudioType.WAV;
            using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri, type);
            ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;
            request.timeout = 30; var operation = request.SendWebRequest();
            try
            {
                while (!operation.isDone) { ct.ThrowIfCancellationRequested(); await Task.Yield(); }
                ct.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success) throw new InvalidDataException("Audio decode failed.");
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || clip.length < .1f || clip.length > maximumMinutes * 60) { if (clip != null) Destroy(clip); throw new InvalidDataException("音乐时长超出歌单设置的范围。"); }
                return clip;
            }
            catch { request.Abort(); throw; }
        }
        public void CancelPendingLoad() { generation++; loading?.Cancel(); }
        private void OnDestroy() { CancelPendingLoad(); }
    }
}
