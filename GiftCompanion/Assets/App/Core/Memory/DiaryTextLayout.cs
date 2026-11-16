using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.Memory
{
    // Format once before committing: the database, UI and Markdown share this exact body.
    public static class DiaryTextLayout
    {
        public static string Read(string json, DiaryPolicy policy)
        {
            if (json == null || json.Length > 65536) throw new InvalidDataException("Invalid diary response.");
            var value = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(json, new Newtonsoft.Json.JsonSerializerSettings { MaxDepth = 8 });
            var parts = new List<string>();
            if (value?["paragraphs"] != null)
            {
                if (!(value["paragraphs"] is JArray array) || array.Count < 1 || array.Count > 40 || array.Any(t => t.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)t)))
                    throw new InvalidDataException("Invalid diary paragraphs.");
                parts.AddRange(array.Select(t => (string)t));
            }
            else if (value?["diary"]?.Type == JTokenType.String) parts.Add(value.Value<string>("diary")); // Legacy response compatibility.
            else throw new InvalidDataException("Diary paragraphs missing.");
            string raw = string.Join("\n\n", parts).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > policy.DiaryCharacters || raw.Contains("<META")) throw new InvalidDataException("Invalid diary response.");
            var paragraphs = new List<string>();
            foreach (string block in Regex.Split(raw, @"\n\s*\n"))
            {
                string remaining = block.Trim();
                while (remaining.Length > policy.MaximumParagraphCharacters)
                {
                    int cut = FindBoundary(remaining, policy.PreferredParagraphCharacters, policy.MaximumParagraphCharacters);
                    paragraphs.Add(remaining.Substring(0, cut).Trim()); remaining = remaining.Substring(cut).Trim();
                }
                if (remaining.Length > 0) paragraphs.Add(remaining);
            }
            // Only whitespace is added. No second LLM call, paraphrasing, truncation or invented content.
            return string.Join("\n\n", paragraphs);
        }
        private static int FindBoundary(string text, int preferred, int maximum)
        {
            int last = 0, quoteDepth = 0, brackets = 0;
            for (int i = 0; i < Math.Min(text.Length, maximum); i++)
            {
                char c = text[i];
                if (c == '“' || c == '「' || c == '『') quoteDepth++;
                if (c == '”' || c == '」' || c == '』') quoteDepth = Math.Max(0, quoteDepth - 1);
                if (c == '(' || c == '（') brackets++;
                if (c == ')' || c == '）') brackets = Math.Max(0, brackets - 1);
                if (quoteDepth == 0 && brackets == 0 && "。！？!?；;\n”」』".IndexOf(c) >= 0)
                { last = i + 1; if (last >= preferred) return last; }
            }
            if (last >= preferred / 2) return last;
            int cut = maximum; if (char.IsHighSurrogate(text[cut - 1])) cut--; return cut;
        }
    }
}
