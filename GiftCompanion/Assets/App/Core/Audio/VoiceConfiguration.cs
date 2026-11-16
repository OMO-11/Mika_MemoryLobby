using System;
using System.IO;
using System.Threading;
using GiftCompanion.Core.Config;

namespace GiftCompanion.Core.Audio
{
    public sealed class VoiceRuntimeSettings
    {
        public int Version { get; set; } = 1;
        public string RuntimeDirectory { get; set; } = "Runtime/GPT-SoVITS";
        public string Profile { get; set; } = "voice.json";
        public string InferenceConfig { get; set; } = "tts_infer.yaml";
        public int Port { get; set; } = 9880;
        public int StartupTimeoutSeconds { get; set; } = 120;
        public int RequestTimeoutSeconds { get; set; } = 120;
        public int MaximumQueuedSegments { get; set; } = 32;
        public int MaximumBufferedAudio { get; set; } = 3;
        public int MinimumSegmentCharacters { get; set; } = 10;
        public int PreferredSegmentCharacters { get; set; } = 45;
        public int MaximumSegmentCharacters { get; set; } = 70;
        public float DuckFactor { get; set; } = .5f;
        public float DuckAttackSeconds { get; set; } = .25f;
        public float DuckReleaseSeconds { get; set; } = .8f;
        public float DuckHoldSeconds { get; set; } = .4f;
        public float SentencePauseSeconds { get; set; } = .45f;
        public float ClausePauseSeconds { get; set; } = .18f;
        public float PauseAfter(string text) => System.Text.RegularExpressions.Regex.IsMatch(text ?? "", "[。！？!?…][”’」』\\\"]*$") ? SentencePauseSeconds : ClausePauseSeconds;
        public VoiceRuntimeSettings ForLanguage(string language)
        {
            if (language != "zh" && language != "ja") throw new InvalidDataException("Voice language unsupported.");
            var copy = (VoiceRuntimeSettings)MemberwiseClone();
            copy.Profile = language == "ja" ? "voice-ja.json" : "voice.json";
            copy.InferenceConfig = language == "ja" ? "tts_infer-ja.yaml" : "tts_infer.yaml";
            copy.Validate(); return copy;
        }
        public void Validate()
        {
            if (Version != 1 || string.IsNullOrWhiteSpace(RuntimeDirectory) || Path.IsPathRooted(RuntimeDirectory) ||
                Profile != "voice.json" && Profile != "voice-ja.json" ||
                InferenceConfig != (Profile == "voice-ja.json" ? "tts_infer-ja.yaml" : "tts_infer.yaml") ||
                Port < 1024 || Port > 65535 || StartupTimeoutSeconds < 10 || StartupTimeoutSeconds > 300 ||
                RequestTimeoutSeconds < 10 || RequestTimeoutSeconds > 300 || MaximumQueuedSegments < 2 || MaximumQueuedSegments > 128 ||
                MaximumBufferedAudio < 1 || MaximumBufferedAudio > 8 || MinimumSegmentCharacters < 1 ||
                MinimumSegmentCharacters > PreferredSegmentCharacters || PreferredSegmentCharacters > MaximumSegmentCharacters ||
                MaximumSegmentCharacters > 180 || !FiniteRange(DuckFactor, .1f, 1) || !FiniteRange(DuckAttackSeconds, .05f, 3) ||
                !FiniteRange(DuckReleaseSeconds, .1f, 5) || !FiniteRange(DuckHoldSeconds, 0, 3) ||
                !FiniteRange(SentencePauseSeconds, 0, 2) || !FiniteRange(ClausePauseSeconds, 0, 1))
                throw new InvalidDataException("语音配置内容无效，原文件会保留。");
            string normalized = RuntimeDirectory.Replace('\\', '/');
            if (normalized.Split('/').Length > 8 || normalized.Contains("..") || normalized.Contains(":"))
                throw new InvalidDataException("Voice runtime path must be relative to installation.");
        }
        private static bool FiniteRange(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        public static VoiceRuntimeSettings Load(string userDataPath, CancellationToken ct)
        {
            string path = Path.Combine(userDataPath, "Config", "voice_runtime.json");
            AtomicDocument.Recover(path);
            if (!File.Exists(path)) AtomicDocument.WriteJson(path, new VoiceRuntimeSettings(), ct);
            var result = AtomicDocument.Read<VoiceRuntimeSettings>(path) ?? throw new InvalidDataException("Voice configuration is empty.");
            result.Validate();
            var document = AtomicDocument.Read<Newtonsoft.Json.Linq.JObject>(path);
            if (!document.TryGetValue("SentencePauseSeconds", StringComparison.OrdinalIgnoreCase, out _) ||
                !document.TryGetValue("ClausePauseSeconds", StringComparison.OrdinalIgnoreCase, out _))
                AtomicDocument.WriteJson(path, result, ct);
            return result;
        }
    }
    public sealed class VoiceProfile
    {
        public string Language { get; set; }
        public string ReferenceAudio { get; set; }
        public string PromptText { get; set; }
        public string PromptLanguage { get; set; }
        public string TextLanguage { get; set; }
        public void Validate(string runtime)
        {
            if (Language != "zh" && Language != "ja" || PromptLanguage != Language || TextLanguage != Language ||
                string.IsNullOrWhiteSpace(PromptText) || PromptText.Length > 2000 || string.IsNullOrWhiteSpace(ReferenceAudio))
                throw new InvalidDataException("Voice profile invalid.");
            string full = Path.GetFullPath(Path.Combine(runtime, ReferenceAudio));
            if (!full.StartsWith(Path.GetFullPath(runtime) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                throw new InvalidDataException("Voice reference unavailable.");
        }
    }
}
