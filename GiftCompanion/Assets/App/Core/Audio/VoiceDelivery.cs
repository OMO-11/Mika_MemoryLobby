using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Config;

namespace GiftCompanion.Core.Audio
{
    public sealed class VoiceDeliveryStyle
    {
        public string Id { get; set; }
        public string Language { get; set; }
        public string[] Emotions { get; set; } = Array.Empty<string>();
        // An empty reference keeps the original model's reference; it never changes weights.
        public string ReferenceAudio { get; set; }
        public string PromptText { get; set; }
        public float SpeedFactor { get; set; } = 1;
    }
    public sealed class VoiceDeliverySettings
    {
        public int Version { get; set; } = 2;
        public string DefaultJapanese { get; set; }
        public string DefaultChinese { get; set; }
        public List<VoiceDeliveryStyle> Styles { get; set; } = new List<VoiceDeliveryStyle>();
        public void Validate(string root)
        {
            if (Version != 1 && Version != 2 || Styles == null || Styles.Count > 24 || Styles.Any(s => s == null) ||
                Styles.Select(s => s.Id).Distinct().Count() != Styles.Count) throw new InvalidDataException("Voice delivery settings invalid.");
            foreach (var style in Styles)
            {
                if (string.IsNullOrWhiteSpace(style.Id) || style.Id.Length > 64 || style.Language != "zh" && style.Language != "ja" ||
                    float.IsNaN(style.SpeedFactor) || style.SpeedFactor < .85f || style.SpeedFactor > 1.1f || style.Emotions == null || style.Emotions.Length > 10 ||
                    style.Emotions.Any(e => !SpeechEmotion.IsValid(e))) throw new InvalidDataException("Voice delivery style invalid.");
                if (string.IsNullOrEmpty(style.ReferenceAudio)) continue;
                if (string.IsNullOrWhiteSpace(style.PromptText) || style.PromptText.Length > 1000) throw new InvalidDataException("Reference transcript unavailable.");
                var wave = WaveAudio.Decode(File.ReadAllBytes(ResolveReference(root, style.ReferenceAudio)));
                if (wave.Duration < 3 || wave.Duration > 10) throw new InvalidDataException("Reference must be 3 to 10 seconds.");
            }
            if (DefaultJapanese != null && !Styles.Any(s => s.Id == DefaultJapanese && s.Language == "ja") ||
                DefaultChinese != null && !Styles.Any(s => s.Id == DefaultChinese && s.Language == "zh")) throw new InvalidDataException("Default voice style unavailable.");
        }
        public static string ResolveReference(string root, string relative)
        {
            string normalized = (relative ?? "").Replace('\\', '/');
            if (!System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^VoiceReferences/[a-z0-9_-]{1,64}\.wav$")) throw new InvalidDataException("Reference path invalid.");
            string full = Path.GetFullPath(Path.Combine(root, normalized));
            for (var dir = new DirectoryInfo(Path.GetDirectoryName(full)); dir != null; dir = dir.Parent)
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked reference directories unsupported.");
            if (!File.Exists(full) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Reference file unavailable.");
            return full;
        }
        public VoiceDeliveryStyle Select(string language, string emotion)
        {
            string fallback = language == "ja" ? DefaultJapanese : DefaultChinese;
            return Styles.FirstOrDefault(s => s.Language == language && s.Emotions.Contains(emotion)) ?? Styles.FirstOrDefault(s => s.Language == language && s.Id == fallback)
                ?? new VoiceDeliveryStyle { Id = "original", Language = language };
        }
        public static VoiceDeliverySettings Load(string root)
        {
            string path = Path.Combine(root, "Config", "voice_delivery.json"); AtomicDocument.Recover(path);
            var result = File.Exists(path) ? AtomicDocument.Read<VoiceDeliverySettings>(path) : new VoiceDeliverySettings();
            if (result == null) throw new InvalidDataException("Voice delivery settings empty."); result.Validate(root); return result;
        }
        public static void Seed(string root, string defaultsFolder, CancellationToken ct)
        {
            string file = Path.Combine(root, "Config", "voice_delivery.json"); AtomicDocument.Recover(file);
            var seed = AtomicDocument.Read<VoiceDeliverySettings>(Path.Combine(defaultsFolder, "voice_delivery.json"));
            if (File.Exists(file))
            {
                // Upgrade only the old built-in fallback. Preserve player-defined voice mappings.
                try
                {
                    var existing = AtomicDocument.Read<VoiceDeliverySettings>(file);
                    if (existing == null || existing.Version != 1) return;
                    existing.Validate(root);
                    var legacy = existing.Styles.FirstOrDefault(s => s.Id == "ja-soft-original" && s.Language == "ja" && string.IsNullOrEmpty(s.ReferenceAudio) &&
                        string.IsNullOrEmpty(s.PromptText) && Math.Abs(s.SpeedFactor - .94f) < .0001f && s.Emotions.SequenceEqual(new[] { "concern", "sad", "sleepy", "annoyed" }));
                    if (legacy != null) legacy.Emotions = new[] { "concern", "sleepy" };
                    foreach (var style in seed.Styles.Where(s => s.Emotions.Any(e => e == "annoyed" || e == "sad" || e == "hesitant" || e == "flat")))
                        if (!existing.Styles.Any(s => s.Language == style.Language && s.Emotions.Intersect(style.Emotions).Any()) && !existing.Styles.Any(s => s.Id == style.Id)) existing.Styles.Add(style);
                    existing.Version = 2; existing.Validate(root); AtomicDocument.WriteJson(file, existing, ct);
                }
                catch (Exception error) when (error is Newtonsoft.Json.JsonException || error is InvalidDataException || error is IOException) { /* Creation reports invalid voice data; preserve the original here. */ }
                return;
            }
            Directory.CreateDirectory(Path.Combine(root, "VoiceReferences"));
            foreach (var style in seed.Styles.Where(s => !string.IsNullOrEmpty(s.ReferenceAudio)).GroupBy(s => s.ReferenceAudio).Select(g => g.First()))
            {
                string name = Path.GetFileName(style.ReferenceAudio);
                string target = Path.Combine(root, "VoiceReferences", name);
                if (!File.Exists(target)) File.Copy(Path.Combine(defaultsFolder, "References", name), target);
            }
            seed.Validate(root); AtomicDocument.WriteJson(file, seed, ct);
        }
    }
    public static class SpeechEmotion
    {
        public static bool IsValid(string value) => GiftCompanion.Core.Character.DialogueVoiceIntent.IsValid(value);
    }
    public sealed class VoiceSynthesisResult
    {
        public byte[] Wave { get; set; }
        public string ReferenceId { get; set; }
        public float SpeedFactor { get; set; } = 1;
    }
    public interface IStyledVoiceSynthesizer : IVoiceSynthesizer
    {
        Task<VoiceSynthesisResult> SynthesizeWithStyleAsync(string text, string emotion, CancellationToken ct);
    }
}
