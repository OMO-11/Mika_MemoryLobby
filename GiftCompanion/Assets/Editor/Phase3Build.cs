using UnityEditor;
namespace GiftCompanion.Editor
{
    public static class Phase3Build
    {
        [MenuItem("Gift Companion/Phase 3/Build Windows Companion")]
        public static void Run() => Phase0Build.Build("Phase3");
    }
}
