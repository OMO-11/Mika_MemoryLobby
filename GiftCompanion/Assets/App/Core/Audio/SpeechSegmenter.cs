using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GiftCompanion.Core.Audio
{
    public sealed class SpeechSegment
    {
        public int Index { get; }
        public string DisplayText { get; }
        public string SpokenText { get; }
        public SpeechSegment(int index, string displayText, string spokenText)
        { Index = index; DisplayText = displayText; SpokenText = spokenText; }
    }
    // Consumes body deltas only. Display text and original persisted reply are never rewritten.
    public sealed class SpeechSegmenter
    {
        private readonly StringBuilder pending = new StringBuilder();
        private readonly int minimum, preferred, maximum;
        private int index;
        public SpeechSegmenter(VoiceRuntimeSettings settings)
        { settings.Validate(); minimum = settings.MinimumSegmentCharacters; preferred = settings.PreferredSegmentCharacters; maximum = settings.MaximumSegmentCharacters; }
        public IReadOnlyList<SpeechSegment> Append(string text, bool final = false)
        {
            pending.Append(text); var result = new List<SpeechSegment>();
            while (pending.Length > 0)
            {
                int cut = FindCut(final);
                if (cut <= 0) break;
                string display = pending.ToString(0, cut).Trim(); pending.Remove(0, cut);
                string spoken = CleanForSpeech(display);
                if (spoken.Length > 0) result.Add(new SpeechSegment(index++, display, spoken));
            }
            return result;
        }
        private int FindCut(bool final)
        {
            int soft = -1, bound = Math.Min(pending.Length, maximum);
            for (int i = 0; i < bound; i++)
            {
                char c = pending[i];
                bool decimalPoint = c == '.' && i > 0 && char.IsDigit(pending[i-1]) && (i + 1 >= pending.Length || char.IsDigit(pending[i+1]));
                if ("。！？!?\n".IndexOf(c) >= 0 || c == '.' && !decimalPoint || c == '…' && i + 1 < pending.Length && pending[i+1] == '…')
                {
                    int end = i + 1;
                    while (end < Math.Min(pending.Length, maximum) && "。！？!?…\"”’」』".IndexOf(pending[end]) >= 0) end++;
                    // A boundary at the end of a delta is safe; later punctuation is kept with the next segment.
                    if (end >= minimum) return end;
                }
                if ("，,；;：: \t".IndexOf(c) >= 0) soft = i + 1;
                if (i + 1 >= preferred && soft >= minimum && pending.Length > preferred) return soft;
            }
            if (pending.Length >= maximum)
            {
                int cut = soft >= minimum ? soft : maximum;
                if (cut > 0 && char.IsHighSurrogate(pending[cut-1])) cut--;
                return cut;
            }
            return final ? pending.Length : 0;
        }
        public static string CleanForSpeech(string text)
        {
            string value = Regex.Replace(text ?? "", @"<META\b[^>]*>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"!?\[([^\]]*)\]\([^)]*\)", "$1");
            value = Regex.Replace(value, @"https?://\S+", "链接");
            value = Regex.Replace(value, @"[`*_#>|~]", "");
            value = Regex.Replace(value, @"[\p{Cc}\p{Cf}]", " ");
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return Regex.IsMatch(value, @"[\p{L}\p{N}]") ? value : "";
        }
    }
}
