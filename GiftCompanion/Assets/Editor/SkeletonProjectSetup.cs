using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiftCompanion.Editor
{
    public static class SkeletonProjectSetup
    {
        private const string PrefabPath = "Assets/App/Bootstrap/AppRoot.prefab";
        private static readonly string[] sceneNames = { "Bootstrap", "Home", "Companion", "AgentPet" };

        // Safe to rerun: existing scenes/prefabs are preserved, including future asset work.
        [MenuItem("Gift Companion/Set Up Skeleton")]
        public static async void Run()
        {
            try
            {
                Directory.CreateDirectory("Assets/Scenes");
                AssetDatabase.Refresh();
                await EnsureTmpEssentialsAsync();
                EnsureAppRootPrefab();
                foreach (string name in sceneNames)
                {
                    string path = $"Assets/Scenes/{name}.unity";
                    if (!File.Exists(path))
                    {
                        if (name == "Bootstrap") CreateBootstrap(path);
                        else CreateModeScene((CharacterMode)Enum.Parse(typeof(CharacterMode), name), path);
                    }
                    RepairMissingReferences(path, name);
                }

                EditorBuildSettings.scenes = sceneNames
                    .Select(name => new EditorBuildSettingsScene($"Assets/Scenes/{name}.unity", true)).ToArray();
                PlayerSettings.companyName = "GiftCompanion";
                PlayerSettings.productName = "GiftCompanion";
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                    new[] { GraphicsDeviceType.Direct3D11 });
                EditorSettings.serializationMode = SerializationMode.ForceText;
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
                Debug.Log("GiftCompanion skeleton setup complete. External capabilities remain unconnected.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static async Task EnsureTmpEssentialsAsync()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset") != null) return;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            string essentials = Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            var completed = new TaskCompletionSource<bool>();
            AssetDatabase.ImportPackageCallback onComplete = name => completed.TrySetResult(true);
            AssetDatabase.ImportPackageFailedCallback onFailure = (name, error) =>
                completed.TrySetException(new InvalidOperationException(error));
            AssetDatabase.ImportPackageCallback onCancel = name =>
                completed.TrySetException(new InvalidOperationException("TMP import canceled."));
            AssetDatabase.importPackageCompleted += onComplete;
            AssetDatabase.importPackageFailed += onFailure;
            AssetDatabase.importPackageCancelled += onCancel;
            try
            {
                AssetDatabase.ImportPackage(essentials, false);
                if (await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(45))) != completed.Task)
                    throw new TimeoutException("TMP essentials import timed out.");
                await completed.Task;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset") == null)
                    throw new InvalidDataException("TMP default font was not imported.");
            }
            finally
            {
                AssetDatabase.importPackageCompleted -= onComplete;
                AssetDatabase.importPackageFailed -= onFailure;
                AssetDatabase.importPackageCancelled -= onCancel;
            }
        }

        private static void EnsureAppRootPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return;
            var gameObject = new GameObject("AppRoot");
            var root = gameObject.AddComponent<AppRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty("identityAsset").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Characters/MainCharacter/Config/identity.json");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(gameObject);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateBootstrap(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var entry = new GameObject("BootstrapEntry").AddComponent<BootstrapEntry>();
            var serialized = new SerializedObject(entry);
            serialized.FindProperty("appRootPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<AppRoot>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void RepairMissingReferences(string path, string name)
        {
            var scene = EditorSceneManager.OpenScene(path);
            bool changed = false;
            if (name == "Bootstrap")
            {
                var entry = scene.GetRootGameObjects().SelectMany(go =>
                    go.GetComponentsInChildren<BootstrapEntry>(true)).Single();
                var serialized = new SerializedObject(entry);
                var reference = serialized.FindProperty("appRootPrefab");
                if (reference.objectReferenceValue == null)
                {
                    reference.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<AppRoot>();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            foreach (var label in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<TMP_Text>(true)))
            {
                // TMP can assign its default font on scene load. Persist that fallback too,
                // so generated scenes have explicit font references rather than relying on Awake.
                if (label.font == null) label.font = font;
                EditorUtility.SetDirty(label);
                changed = true;
            }
            if (changed) EditorSceneManager.SaveScene(scene, path);
        }

        private static void CreateModeScene(CharacterMode mode, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1, -6);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.21f);

            var canvasObject = new GameObject("NavigationCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var controller = new GameObject("ModeSceneController").AddComponent<ModeSceneController>();
            var buttons = new System.Collections.Generic.List<Button>();
            if (mode == CharacterMode.Home)
            {
                Label(canvasObject.transform, "CharacterName", "Main Character", new Vector2(0, 95), 34);
                Label(canvasObject.transform, "Invitation", "Where shall we meet today?", new Vector2(0, 35), 24);
                buttons.Add(Button(canvasObject.transform, "Companion", "Companion Space",
                    new Vector2(-155, -45), controller.OpenCompanion));
                buttons.Add(Button(canvasObject.transform, "AgentPet", "On Your Desktop",
                    new Vector2(155, -45), controller.OpenAgentPet));
                buttons.Add(Button(canvasObject.transform, "Quit", "Exit", new Vector2(0, -140), controller.Quit));
            }
            else
            {
                Label(canvasObject.transform, "ModeTitle", mode == CharacterMode.Companion
                    ? "Companion Space" : "On Your Desktop", new Vector2(0, 255), 30);
                buttons.Add(Button(canvasObject.transform, "ReturnHome", "Return Home",
                    new Vector2(0, -245), controller.ReturnHome));
                // A simple temporary mesh keeps the scene usable without character assets.
                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                placeholder.name = "CharacterPlaceholder";
                placeholder.transform.position = new Vector3(0, 0.7f, 0);
                var light = new GameObject("Placeholder Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
            }

            var feedback = Label(canvasObject.transform, "Feedback", string.Empty, new Vector2(0, -310), 18);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("sceneMode").enumValueIndex = (int)mode;
            serialized.FindProperty("feedback").objectReferenceValue = feedback;
            var navigation = serialized.FindProperty("navigationButtons");
            navigation.arraySize = buttons.Count;
            for (int index = 0; index < buttons.Count; index++)
                navigation.GetArrayElementAtIndex(index).objectReferenceValue = buttons[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, path);
        }

        private static TMP_Text Label(Transform parent, string name, string text, Vector2 position, int fontSize)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(700, 60);
            rect.anchoredPosition = position;
            var label = gameObject.GetComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Button Button(Transform parent, string name, string text, Vector2 position, UnityAction action)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 56);
            rect.anchoredPosition = position;
            gameObject.GetComponent<Image>().color = new Color(0.25f, 0.33f, 0.42f);
            var button = gameObject.GetComponent<Button>();
            button.targetGraphic = gameObject.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = Label(gameObject.transform, "Label", text, Vector2.zero, 22);
            label.rectTransform.sizeDelta = rect.sizeDelta;
            return button;
        }
    }
}
