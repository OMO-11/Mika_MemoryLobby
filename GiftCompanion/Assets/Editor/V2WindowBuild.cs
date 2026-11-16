using UnityEditor;
using UnityEngine;
namespace GiftCompanion.Editor
{
    public static class V2WindowBuild
    {
        public static void Prepare()
        {
            const string path="Assets/App/Companion/Resources/MikaVisual/WindowAtmosphere.mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)AssetDatabase.CreateAsset(new Material(Shader.Find("GiftCompanion/WindowAtmosphere")),path);
            AssetDatabase.SaveAssets();
        }
        public static void Run(){Prepare();Phase0Build.Build("V2Window");}
        public static void PrepareOnly(){Prepare();EditorApplication.Exit(0);}
    }
}
