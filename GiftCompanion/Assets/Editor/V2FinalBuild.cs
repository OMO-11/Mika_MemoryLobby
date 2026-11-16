using UnityEditor;
namespace GiftCompanion.Editor
{ public static class V2FinalBuild { public static void Run(){ V2WindowBuild.Prepare();PlayerSettings.bundleVersion="2.0.0";Phase0Build.Build("V2Final"); } } }
