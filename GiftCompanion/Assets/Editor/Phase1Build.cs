using UnityEditor;
namespace GiftCompanion.Editor
{
    public static class Phase1Build
    {
        [MenuItem("Gift Companion/Phase 1/Build Windows Core")]
        public static void Run() => Phase0Build.Build("Phase1");
    }
}
