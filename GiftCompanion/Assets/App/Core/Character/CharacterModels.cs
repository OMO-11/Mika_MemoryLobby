using System;

namespace GiftCompanion.Core.Character
{
    public enum CharacterMode { Home, Companion, AgentPet }
    public enum InteractionSource { Companion, AgentPet }
    public enum CharacterEmotion { Neutral, Happy, Shy, Concern, Sad, Surprised, Annoyed, Sleepy }

    // Immutable card snapshot. Only explicit player edits replace it; Memory cannot mutate it.
    public sealed class CharacterIdentity
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Persona { get; }
        public string Relationship { get; }

        public CharacterIdentity(string id, string displayName, string persona, string relationship)
        {
            Id = id;
            DisplayName = displayName;
            Persona = persona;
            Relationship = relationship;
        }
    }

    public sealed class CharacterState
    {
        public CharacterEmotion Mood { get; set; } = CharacterEmotion.Neutral;
        public float Energy { get; set; } = 0.7f;
        public string CurrentTopic { get; set; } = string.Empty;
        public CharacterMode LastMode { get; internal set; } = CharacterMode.Home;
        public DateTimeOffset? LastInteractionAt { get; set; }

        public CharacterState Snapshot() => new CharacterState
        {
            Mood = Mood,
            Energy = Energy,
            CurrentTopic = CurrentTopic,
            LastMode = LastMode,
            LastInteractionAt = LastInteractionAt
        };
    }

    public sealed class DialogueResult
    {
        public bool Success { get; }
        public string Text { get; }
        public string ErrorCode { get; }
        public string RequestId { get; }
        public DialoguePresentation Presentation { get; }

        private DialogueResult(bool success, string text, string errorCode,string requestId=null,DialoguePresentation presentation=null)
        {
            Success = success;
            Text = text;
            ErrorCode = errorCode;
            RequestId = requestId;
            Presentation = presentation;
        }

        public static DialogueResult Unavailable() =>
            new DialogueResult(false, string.Empty, "LLMNotConfigured");
        public static DialogueResult Completed(string text,string requestId=null,DialoguePresentation presentation=null) => new DialogueResult(true, text, null,requestId,presentation);
        public static DialogueResult Failed(string code) => new DialogueResult(false, string.Empty, code);
    }
}
