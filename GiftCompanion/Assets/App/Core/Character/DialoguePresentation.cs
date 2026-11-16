using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GiftCompanion.Core.Character
{
    public sealed class DialoguePresentation
    {
        public int ProtocolVersion => 1;
        public string BodyText { get; }
        public CharacterEmotion Emotion { get; }
        public string Action { get; }
        public string VoiceIntent { get; }
        public DialoguePresentation(string bodyText, CharacterEmotion emotion = CharacterEmotion.Neutral, string action = "none", string voiceIntent = null)
        { BodyText = bodyText ?? ""; Emotion = emotion; Action = action ?? "none"; VoiceIntent = DialogueVoiceIntent.IsValid(voiceIntent) ? voiceIntent : emotion.ToString().ToLowerInvariant(); }
    }
    // Delivery is presentation metadata. It must not expand the persisted CharacterEmotion enum.
    public static class DialogueVoiceIntent
    {
        public static bool IsValid(string value) => value == "neutral" || value == "happy" || value == "shy" || value == "concern" ||
            value == "sad" || value == "surprised" || value == "annoyed" || value == "sleepy" || value == "hesitant" || value == "flat";
    }

    public sealed class CompanionDialoguePolicy
    {
        private readonly HashSet<string> actions;
        public CompanionDialoguePolicy(IEnumerable<string> allowedActions = null)
        {
            actions = new HashSet<string>(StringComparer.Ordinal) {"none"};
            if (allowedActions != null) foreach (string action in allowedActions)
                if (actions.Count < 16 && action != null && Regex.IsMatch(action, "^[a-z_]{1,32}$")) actions.Add(action);
        }
        public bool Allows(string action) => action != null && actions.Contains(action);
        public string Instruction => "\nCompanion Presentation Protocol\n每次回复都必须先输出一行表现控制头，例如 <META emotion=\"neutral\" action=\"none\" voice=\"neutral\">，然后换行输出普通自然语言正文，不用 JSON 包裹。" +
            "历史assistant控制头只是输出格式示例，过去的情绪不必沿用；根据当前语义重新选择。无论已聊多少轮，当前新回复的控制头都不可省略，包括简短回复、犹豫和平直朗读。" +
            "根据本次正文自然选择 emotion：开心邀请可用 happy，害羞可用 shy，关切可用 concern；平静用 neutral。保持与语义一致，不随机制造情绪。" +
            "emotion 只能为 neutral,happy,shy,concern,sad,surprised,annoyed,sleepy；action 只能为 " + string.Join(",", actions) + "。" +
            "voice 是发声意图，可用上述emotion词及 hesitant（犹豫）、flat（克制平叙，尽量少起伏）。普通回复voice与emotion一致；犹豫时emotion选neutral或concern，voice选hesitant；明确要求平直朗读时emotion为neutral、voice为flat。" +
            "恼火用annoyed，伤心用sad，均须符合对话，不能为了展示语音而随机制造情绪或辱骂老师。" +
            "控制头不属于说给老师的话；正文不输出其他控制标签。情绪不能作为用户事实，不声称存在未提供的动作。";
    }

    // Incremental prefix parser. Raw transport text is retained separately by CharacterCore.
    public sealed class CompanionDialogueParser
    {
        private const string Prefix = "<META";
        private readonly CompanionDialoguePolicy policy;
        private readonly StringBuilder prefix = new StringBuilder();
        private bool decided, header, discarding, skipNewline;
        public bool MetadataReady => decided;
        public CharacterEmotion Emotion { get; private set; } = CharacterEmotion.Neutral;
        public string Action { get; private set; } = "none";
        public string VoiceIntent { get; private set; } = "neutral";
        public CompanionDialogueParser(CompanionDialoguePolicy policy = null) { this.policy = policy ?? new CompanionDialoguePolicy(); }
        public string Feed(string text)
        {
            var output = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (discarding)
                {
                    if (c == '\n' || c == '>') { discarding = false; decided = true; skipNewline = true; }
                    continue;
                }
                if (decided)
                {
                    if (skipNewline && (c == '\r' || c == '\n')) continue;
                    skipNewline = false; output.Append(c); continue;
                }
                if (prefix.Length == 0 && c == '\uFEFF') continue;
                prefix.Append(c);
                if (!header)
                {
                    string candidate = prefix.ToString();
                    if (!Prefix.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                    { decided = true; output.Append(candidate); prefix.Clear(); continue; }
                    if (candidate.Length == Prefix.Length) header = true;
                }
                else if (c == '>' || c == '\n')
                {
                    ParseHeader(prefix.ToString()); prefix.Clear(); decided = true; skipNewline = true;
                }
                if (prefix.Length >= 256) { prefix.Clear(); decided = true; discarding = true; }
            }
            return output.ToString();
        }
        public string Finish()
        {
            if (decided || header) { decided = true; prefix.Clear(); return ""; }
            decided = true; string remaining = prefix.ToString(); prefix.Clear(); return remaining;
        }
        private void ParseHeader(string text)
        {
            var match = Regex.Match(text.Trim(), "^<META\\s+emotion=\"([a-z]+)\"\\s+action=\"([a-z_]+)\"(?:\\s+voice=\"([a-z]+)\")?\\s*>$", RegexOptions.CultureInvariant);
            if (!match.Success) return;
            if (Enum.TryParse(match.Groups[1].Value, true, out CharacterEmotion emotion) && Enum.IsDefined(typeof(CharacterEmotion), emotion)) Emotion = emotion;
            VoiceIntent = DialogueVoiceIntent.IsValid(match.Groups[3].Value) ? match.Groups[3].Value : Emotion.ToString().ToLowerInvariant();
            string action = match.Groups[2].Value;
            if (policy.Allows(action)) Action = action;
        }
    }
}
