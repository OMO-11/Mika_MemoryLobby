using GiftCompanion.Bootstrap;
using GiftCompanion.Validation;
using UnityEditor;

namespace GiftCompanion.Editor
{
    public static class Phase2DevelopmentMenu
    {
        [MenuItem("Gift Companion/Phase 2/Open Development Panel")]
        public static void Open()
        {
            if(!EditorApplication.isPlaying||AppRoot.Instance?.IsReady!=true)return;
            AppRoot.Instance.GetComponent<Phase2DevelopmentPanel>().Open();
        }
    }
}
