using UnityEditor;
namespace GiftCompanion.Editor
{
    public static class Phase2Build
    {
        [MenuItem("Gift Companion/Phase 2/Build Windows Memory")]
        public static void Run()=>Phase0Build.Build("Phase2");
    }
}
