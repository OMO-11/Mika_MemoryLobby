using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.Audio
{
    public interface IVoiceSynthesizer
    {
        string ProfileId { get; }
        Task<byte[]> SynthesizeAsync(string text, CancellationToken ct);
    }
    public enum VoiceRuntimeState { Stopped, Starting, Ready, Unavailable, Stopping }
    public sealed class VoiceRuntimeManager : IStyledVoiceSynthesizer, IDisposable
    {
        private readonly SemaphoreSlim lifecycle = new SemaphoreSlim(1, 1), inference = new SemaphoreSlim(1, 1);
        private readonly HttpClient client;
        private readonly string installationRoot, hostFile, logs;
        private readonly ILogger logger;
        private Process process;
        private CancellationTokenSource processLifetime;
        private string instance;
        private VoiceProfile profile;
        private readonly string userDataPath;
        private readonly VoiceDeliverySettings delivery;
        private bool disposed;
        public VoiceRuntimeSettings Settings { get; }
        private VoiceRuntimeState state;
        public VoiceRuntimeState State
        {
            get => state == VoiceRuntimeState.Ready && OwnedProcessId == 0 ? VoiceRuntimeState.Unavailable : state;
            private set => state = value;
        }
        public string ProfileId { get; private set; } = "unavailable";
        public int OwnedProcessId
        {
            get { var owner = process; try { return owner != null && !owner.HasExited ? owner.Id : 0; } catch (InvalidOperationException) { return 0; } }
        }
        public long LastStartupMilliseconds { get; private set; }
        public long LastInferenceMilliseconds { get; private set; }
        public int InferenceStartedCount { get; private set; }
        public int ActiveInferenceCount { get; private set; }
        public int MaximumActiveInferenceCount { get; private set; }
        public VoiceRuntimeManager(string installationRoot, string hostFile, string userDataPath, VoiceRuntimeSettings settings, ILogger logger)
        {
            this.installationRoot = installationRoot; this.hostFile = hostFile; this.logger = logger;
            this.userDataPath = userDataPath; delivery = VoiceDeliverySettings.Load(userDataPath);
            logs = Path.Combine(userDataPath, "Logs"); Settings = settings; settings.Validate();
            client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
        }
        private string Endpoint => "http://127.0.0.1:" + Settings.Port;
        public async Task EnsureReadyAsync(CancellationToken ct)
        {
            await lifecycle.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (disposed) throw new ObjectDisposedException(nameof(VoiceRuntimeManager));
                if (State == VoiceRuntimeState.Ready && process != null && !process.HasExited) return;
                KillOwned();
                processLifetime?.Dispose(); processLifetime = new CancellationTokenSource();
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(ct, processLifetime.Token);
                startup.CancelAfter(TimeSpan.FromSeconds(Settings.StartupTimeoutSeconds));
                State = VoiceRuntimeState.Starting; var watch = Stopwatch.StartNew();
                try
                {
                    string runtime = Environment.GetEnvironmentVariable("GIFT_VOICE_RUNTIME_ROOT");
                    if (File.Exists(Path.Combine(installationRoot, "Release", "release_manifest.json"))) runtime = null;
                    runtime = Path.GetFullPath(string.IsNullOrWhiteSpace(runtime) ? Path.Combine(installationRoot, Settings.RuntimeDirectory) : runtime);
                    string python = Path.Combine(runtime, "runtime", "python.exe"), yaml = Path.Combine(runtime, Settings.InferenceConfig);
                    if (!File.Exists(python) || !File.Exists(yaml) || !File.Exists(hostFile) || !File.Exists(Path.Combine(runtime, "api_v2.py")))
                        throw new FileNotFoundException("Voice runtime incomplete.");
                    profile = AtomicDocument.Read<VoiceProfile>(Path.Combine(runtime, Settings.Profile)); profile?.Validate(runtime);
                    if (profile == null) throw new InvalidDataException("Voice profile missing.");
                    if (profile.Language != (Settings.Profile == "voice-ja.json" ? "ja" : "zh"))
                        throw new InvalidDataException("Voice profile language does not match selection.");
                    using (var hash = SHA256.Create())
                    {
                        string identity = File.ReadAllText(Path.Combine(runtime, Settings.Profile)) + File.ReadAllText(yaml) + JsonConvert.SerializeObject(delivery);
                        foreach (var reference in delivery.Styles.Where(s => !string.IsNullOrEmpty(s.ReferenceAudio)).Select(s => s.ReferenceAudio).Distinct())
                            identity += BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(VoiceDeliverySettings.ResolveReference(userDataPath, reference))));
                        ProfileId = profile.Language + ":" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
                    }
                    using (var portCheck = new TcpClient())
                    {
                        var connect = portCheck.ConnectAsync("127.0.0.1", Settings.Port);
                        if (await Task.WhenAny(connect, Task.Delay(500, startup.Token)).ConfigureAwait(false) == connect)
                        { try { await connect.ConfigureAwait(false); } catch (SocketException) { } }
                        if (portCheck.Connected) throw new InvalidOperationException("Voice port belongs to another listener.");
                    }
                    startup.Token.ThrowIfCancellationRequested(); instance = Guid.NewGuid().ToString("N");
                    string writable = Path.Combine(userDataPath, "RuntimeCache"); Directory.CreateDirectory(writable); Directory.CreateDirectory(logs);
                    // Upstream persists selected weights to its inference YAML. Keep that write outside the install directory.
                    string writableYaml = Path.Combine(writable, Settings.InferenceConfig); File.Copy(yaml, writableYaml, true);
                    var info = new ProcessStartInfo(python, "-B -u -s " + Quote(hostFile) + " -c " + Quote(writableYaml) + " -a 127.0.0.1 -p " + Settings.Port)
                    { WorkingDirectory = runtime, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
                    info.EnvironmentVariables["PATH"] = Path.Combine(runtime, "runtime") + ";" + (info.EnvironmentVariables["PATH"] ?? "");
                    info.EnvironmentVariables["HF_HUB_OFFLINE"] = "1"; info.EnvironmentVariables["TRANSFORMERS_OFFLINE"] = "1";
                    info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    info.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
                    foreach (string variable in new[] { "HF_HOME", "TORCH_HOME", "XDG_CACHE_HOME", "NUMBA_CACHE_DIR", "MPLCONFIGDIR", "TEMP", "TMP" })
                    { string cache = Path.Combine(writable, variable); Directory.CreateDirectory(cache); info.EnvironmentVariables[variable] = cache; }
                    info.EnvironmentVariables["GIFT_VOICE_INSTANCE"] = instance;
                    info.EnvironmentVariables["GIFT_VOICE_PARENT_PID"] = Process.GetCurrentProcess().Id.ToString();
                    info.EnvironmentVariables["GIFT_VOICE_STATUS_FILE"] = Path.Combine(logs, "voice-host-status.json");
                    // Do not pass the cloud credential to an inference child process.
                    info.EnvironmentVariables.Remove("GIFT_DEEPSEEK_API_KEY");
                    process = new Process { StartInfo = info };
                    // Drain model output without copying conversation text into diagnostic files.
                    process.OutputDataReceived += (_, __) => { }; process.ErrorDataReceived += (_, __) => { };
                    if (!process.Start()) throw new InvalidOperationException("Voice process did not start.");
                    process.BeginOutputReadLine(); process.BeginErrorReadLine();
                    Directory.CreateDirectory(logs);
                    AtomicDocument.WriteJson(Path.Combine(logs, "voice-process.json"), new { pid = process.Id, startTimeUtc = process.StartTime.ToUniversalTime(), pythonPath = python, profile = Settings.Profile, port = Settings.Port }, startup.Token);
                    while (true)
                    {
                        startup.Token.ThrowIfCancellationRequested();
                        if (process.HasExited) throw new InvalidOperationException("Voice process exited during startup.");
                        try
                        {
                            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(startup.Token); timeout.CancelAfter(1000);
                            using var request = Request(HttpMethod.Get, "/gift/health");
                            using var reply = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
                            if (reply.IsSuccessStatusCode)
                            {
                                string json = await reply.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (json.Length < 4096)
                                {
                                    var health = JObject.Parse(json);
                                    if ((string)health["instance"] == instance && (int?)health["pid"] == process.Id && (bool?)health["ready"] == true) break;
                                }
                            }
                        }
                        catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException || error is JsonException) { }
                        await Task.Delay(200, startup.Token).ConfigureAwait(false);
                    }
                    LastStartupMilliseconds = watch.ElapsedMilliseconds; State = VoiceRuntimeState.Ready;
                    logger?.Info($"voice ready profile={profile.Language} startup_ms={LastStartupMilliseconds}");
                }
                catch (Exception error)
                {
                    State = VoiceRuntimeState.Unavailable;
                    try { KillOwned(); } catch (Exception cleanup) { logger?.Error("voice startup cleanup pending=true", cleanup); }
                    logger?.Error("voice unavailable text_fallback=true", error); throw;
                }
            }
            finally { lifecycle.Release(); }
        }
        public async Task<byte[]> SynthesizeAsync(string text, CancellationToken ct) => (await SynthesizeWithStyleAsync(text, "neutral", ct).ConfigureAwait(false)).Wave;
        public async Task<VoiceSynthesisResult> SynthesizeWithStyleAsync(string text, string emotion, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 500) throw new ArgumentException("Voice segment invalid.");
            if (!SpeechEmotion.IsValid(emotion)) throw new ArgumentException("Voice emotion invalid.");
            await inference.WaitAsync(ct).ConfigureAwait(false);
            Process owner = null; bool responseCompleted = false, started = false;
            try
            {
                await EnsureReadyAsync(ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested(); owner = process;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(processLifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(Settings.RequestTimeoutSeconds));
                using var request = Request(HttpMethod.Post, "/gift/tts");
                var style = delivery.Select(profile.Language, emotion);
                string reference = string.IsNullOrEmpty(style.ReferenceAudio) ? profile.ReferenceAudio : VoiceDeliverySettings.ResolveReference(userDataPath, style.ReferenceAudio);
                request.Content = new StringContent(JsonConvert.SerializeObject(new { text, text_lang = profile.TextLanguage, ref_audio_path = reference,
                    prompt_text = string.IsNullOrEmpty(style.ReferenceAudio) ? profile.PromptText : style.PromptText, prompt_lang = profile.PromptLanguage,
                    speed_factor = style.SpeedFactor, text_split_method = "cut0", batch_size = 1, streaming_mode = 0, media_type = "wav" }), Encoding.UTF8, "application/json");
                var watch = Stopwatch.StartNew();
                InferenceStartedCount++; ActiveInferenceCount++; started = true;
                MaximumActiveInferenceCount = Math.Max(MaximumActiveInferenceCount, ActiveInferenceCount);
                // User interruption discards the result, but keeps the inference gate until the GPU request finishes.
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException("Voice synthesis failed.");
                if (response.Content.Headers.ContentLength > WaveAudio.MaximumBytes) throw new InvalidDataException("Voice audio too large.");
                using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false); using var output = new MemoryStream();
                byte[] buffer = new byte[16384]; int size;
                while ((size = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                { if (output.Length + size > WaveAudio.MaximumBytes) throw new InvalidDataException("Voice audio too large."); output.Write(buffer, 0, size); }
                LastInferenceMilliseconds = watch.ElapsedMilliseconds;
                logger?.Info($"voice inference_ms={LastInferenceMilliseconds} bytes={output.Length}");
                responseCompleted = true;
                ct.ThrowIfCancellationRequested(); return new VoiceSynthesisResult { Wave = output.ToArray(), ReferenceId = style.Id, SpeedFactor = style.SpeedFactor };
            }
            catch (Exception error)
            {
                // A canceled consumer after a completed response does not poison the model.
                // Transport errors may leave an unknown running request; retire that exact owned process before releasing the gate.
                if (!(responseCompleted && error is OperationCanceledException && ct.IsCancellationRequested))
                    await RetireAsync(owner).ConfigureAwait(false);
                throw;
            }
            finally { if (started) ActiveInferenceCount--; inference.Release(); }
        }
        private async Task RetireAsync(Process owner)
        {
            await lifecycle.WaitAsync().ConfigureAwait(false);
            try { if (owner != null && ReferenceEquals(owner, process)) { State = VoiceRuntimeState.Unavailable; KillOwned(); } }
            finally { lifecycle.Release(); }
        }
        private HttpRequestMessage Request(HttpMethod method, string path)
        { var request = new HttpRequestMessage(method, Endpoint + path); request.Headers.Add("X-Gift-Instance", instance); return request; }
        private static string Quote(string value)
        { if (value.Contains("\"") || value.Contains("\n") || value.Contains("\r")) throw new InvalidDataException("Invalid voice argument."); return "\"" + value + "\""; }
        public async Task StopAsync()
        {
            processLifetime?.Cancel();
            await lifecycle.WaitAsync().ConfigureAwait(false);
            try
            {
                State = VoiceRuntimeState.Stopping;
                if (process != null && !process.HasExited)
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(700); using var request = Request(HttpMethod.Post, "/gift/shutdown");
                        using var reply = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
                        await Task.Run(() => process.WaitForExit(1500)).ConfigureAwait(false);
                    }
                    catch (Exception error) { logger?.Error("voice graceful_stop fallback=true", error); }
                }
                KillOwned(); State = VoiceRuntimeState.Stopped;
                logger?.Info("voice stopped");
            }
            finally { lifecycle.Release(); }
        }
        private void KillOwned()
        {
            if (process == null) return;
            // Process.Start can fail before assigning a native process. That object owns no PID.
            try { _ = process.Id; }
            catch (InvalidOperationException) { process.Dispose(); process = null; return; }
            if (!process.HasExited) { process.Kill(); if (!process.WaitForExit(5000)) throw new TimeoutException("Owned voice process did not exit."); }
            process.Dispose(); process = null;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; State = VoiceRuntimeState.Stopped; processLifetime?.Cancel();
            // Abrupt Unity destruction cannot await; only this Process object is eligible for termination.
            try { KillOwned(); } catch (Exception error) { logger?.Error("voice dispose failed", error); }
            client.Dispose();
        }
    }
}
