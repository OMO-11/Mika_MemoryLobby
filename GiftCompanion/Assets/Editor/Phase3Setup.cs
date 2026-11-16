using System;
using System.IO;
using System.Linq;
using GiftCompanion.Companion;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GiftCompanion.Editor
{
    public static class Phase3Setup
    {
        public const string Reports = "Docs/Validation/Phase3";
        public static void FinalVisualAudit()
        {
            try
            {
                Directory.CreateDirectory(Reports);
                var result = Capture(true, "3E.editor.final.png");
                File.WriteAllText(Reports + "/3E.editor.visual.json", JsonConvert.SerializeObject(result, Formatting.Indented));
                EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void Install()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                const string prefabPath = "Assets/App/Bootstrap/AppRoot.prefab";
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                var root = new SerializedObject(prefab.GetComponent<GiftCompanion.Bootstrap.AppRoot>());
                root.FindProperty("characterManifestAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Characters/MainCharacter/Config/character_manifest.json");
                root.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); PrefabUtility.UnloadPrefabContents(prefab);
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Companion.unity");
                var view = new SerializedObject(Object.FindFirstObjectByType<CompanionPresentation>());
                view.FindProperty("expressionMapAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Characters/MainCharacter/Config/expression_map.json");
                view.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.SaveScene(scene);
                var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
                var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
                for (int i = shaders.arraySize - 1; i >= 0; i--)
                {
                    var shader = shaders.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                    if (shader != null && (shader.name == "Spine/Blend Modes/Skeleton PMA Multiply" || shader.name == "Spine/Blend Modes/Skeleton PMA Screen"))
                    { shaders.DeleteArrayElementAtIndex(i); if (i < shaders.arraySize && shaders.GetArrayElementAtIndex(i).objectReferenceValue == null) shaders.DeleteArrayElementAtIndex(i); }
                }
                graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
                Directory.CreateDirectory(Reports); File.WriteAllText(Reports + "/installation.json", "{\"passed\":true}");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void VisualAudit()
        {
            try
            {
                Directory.CreateDirectory(Reports);
                foreach (string shaderName in new[] {"Hidden/GiftCompanion/CompanionTone", "GiftCompanion/SpineStraightAlpha"})
                {
                    var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
                    var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
                    var shader = Shader.Find(shaderName);
                    if (shader == null) throw new InvalidDataException("Blend shader missing.");
                    if (!Enumerable.Range(0, shaders.arraySize).Any(i => shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader))
                    { int i = shaders.arraySize++; shaders.GetArrayElementAtIndex(i).objectReferenceValue = shader; settings.ApplyModifiedPropertiesWithoutUndo(); }
                }
                object baseline = Capture(false, "3A.before.png");
                object corrected = Capture(true, "3A.after.png");
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/App/SharedUI/Resources/Fonts/NotoSansCJKsc-Regular.otf");
                if (font == null) throw new InvalidDataException("Chinese font missing.");
                var tmp = TMP_FontAsset.CreateFontAsset(font);
                bool chinese = tmp.TryAddCharacters("未花老师陪伴空间来到桌面设置退出今天想在哪里见面？记忆音乐", out string missing);
                Object.DestroyImmediate(tmp);
                File.WriteAllText(Reports + "/3A.visual.json", JsonConvert.SerializeObject(new { passed = chinese,
                    baseline, corrected, chineseFont = chinese, missingGlyphs = missing, unity = Application.unityVersion,
                    colorSpace = QualitySettings.activeColorSpace.ToString(), capturedAtUtc = DateTimeOffset.UtcNow }, Formatting.Indented));
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
                EditorApplication.Exit(chinese ? 0 : 1);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static object Capture(bool correct, string file)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Companion.unity");
            var loader = Object.FindFirstObjectByType<CompanionPresentation>().Loader;
            loader.Initialize();
            foreach (var atlas in loader.Character.SkeletonDataAsset.atlasAssets)
                foreach (var material in atlas.Materials) material.shader = Shader.Find("GiftCompanion/SpineStraightAlpha");
            if (correct) loader.renderCamera.gameObject.AddComponent<CompanionTone>();
            loader.Character.Update(1.25f); loader.Character.LateUpdate();
            var blends = loader.Character.Skeleton.Data.Slots.GroupBy(s => s.BlendMode.ToString()).ToDictionary(g => g.Key, g => g.Count());
            var camera = loader.renderCamera;
            var rt = new RenderTexture(1280, 720, 24); var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                var previous = RenderTexture.active;
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(Reports + "/" + file, texture.EncodeToPNG());
                camera.targetTexture = null; RenderTexture.active = previous;
                var pixels = texture.GetPixels32();
                return new { blendModes = blends, corrected = correct,
                    clippedPixels = pixels.Count(p => p.r >= 250 && p.g >= 250 && p.b >= 250),
                    meanBrightness = pixels.Average(p => (p.r + p.g + p.b) / 765.0),
                    replacementMaterials = loader.Character.SkeletonDataAsset.blendModeMaterials.multiplyMaterials.Count + loader.Character.SkeletonDataAsset.blendModeMaterials.screenMaterials.Count };
            }
            finally { Object.DestroyImmediate(texture); Object.DestroyImmediate(rt); }
        }
    }
}
