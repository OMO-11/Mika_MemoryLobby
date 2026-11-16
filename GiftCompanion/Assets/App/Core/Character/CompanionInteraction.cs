namespace GiftCompanion.Core.Character
{
    public enum CharacterVisualState { Idle, Listening, Thinking, Speaking, Touched, SpecialAction, MomoTalk, Menu }
    public sealed class CompanionInputGuard
    {
        private bool composing;
        private int lastCompositionFrame = -100;
        public void Observe(string composition, int frame)
        { bool next = !string.IsNullOrEmpty(composition); if (composing || next) lastCompositionFrame = frame; composing = next; }
        public bool CanSubmit(int frame) => !composing && frame > lastCompositionFrame + 1;
    }
}
