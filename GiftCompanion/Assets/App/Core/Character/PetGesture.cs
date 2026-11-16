namespace GiftCompanion.Core.Character
{
    public enum PetGestureEvent { None, HeadClick, BodyClick, HeadPatStart, HeadPatEnd, DragStart, DragMove, DragEnd }
    public sealed class PetGesture
    {
        private bool down, head, pat, drag;
        private float x, y, began;
        public bool IsDragging => drag;
        public bool IsHeld => down;
        public void Begin(bool onHead, float left, float top, float time) { down = true; head = onHead; pat = drag = false; x = left; y = top; began = time; }
        public PetGestureEvent Move(float left, float top, float time, float threshold, float hold)
        {
            if (!down) return PetGestureEvent.None;
            float dx = left - x, dy = top - y;
            if (drag || dx * dx + dy * dy >= threshold * threshold) { bool first = !drag; drag = true; pat = false; return first ? PetGestureEvent.DragStart : PetGestureEvent.DragMove; }
            if (!drag && head && !pat && time - began >= hold) { pat = true; return PetGestureEvent.HeadPatStart; }
            return PetGestureEvent.None;
        }
        public PetGestureEvent Release(bool cancelled)
        {
            if (!down) return PetGestureEvent.None;
            down = false; var result = drag ? PetGestureEvent.DragEnd : pat ? PetGestureEvent.HeadPatEnd : cancelled ? PetGestureEvent.None : head ? PetGestureEvent.HeadClick : PetGestureEvent.BodyClick;
            drag = pat = false; return result;
        }
    }
}
