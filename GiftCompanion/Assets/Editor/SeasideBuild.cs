using UnityEngine;
using UnityEditor;
namespace GiftCompanion.Editor {
 public static class SeasideBuild {
  public static void Run(){V2WindowBuild.Prepare();const string path="Assets/App/Companion/Resources/MikaVisual/SeasideGlass.mat";
   if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)AssetDatabase.CreateAsset(new Material(Shader.Find("GiftCompanion/SeasideGlass")),path);
   AssetDatabase.SaveAssets();PlayerSettings.bundleVersion="2.0.2";Phase0Build.Build("V2SeasideReview");
  }
 }
}
