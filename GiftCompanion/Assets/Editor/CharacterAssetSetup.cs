using System;
using System.IO;
using System.Linq;
using GiftCompanion.AgentPet;
using GiftCompanion.Companion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GiftCompanion.Editor
{
    public static class CharacterAssetSetup
    {
        public const string PetPrefab = "Assets/Characters/MainCharacter/Pet/CHDDD/Unity/CHDDD_Actions_v2.prefab";

        [MenuItem("Gift Companion/Phase 0/Bind Delivered Characters")]
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                PlayerSettings.colorSpace = ColorSpace.Gamma;
                BindPet();
                BindCompanion();
                IncludeSpineShader();
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
                Debug.Log("Phase 0 character bindings created; external capabilities remain unverified.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void BindPet()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PetPrefab);
            if (prefab == null) throw new InvalidDataException("Frozen CHDDD prefab is missing.");
            var model = AssetImporter.GetAtPath(PetPrefab.Replace(".prefab", ".fbx")) as ModelImporter;
            if (model == null || model.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidDataException("CHDDD must use the delivered Generic rig.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/AgentPet.unity");
            if (Object.FindFirstObjectByType<PetPresentation>() == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Mika Pet (Unity V7)";
                var adapter = instance.AddComponent<PetPresentation>();
                Assign(adapter, "body", instance.GetComponent<CHDDDBodyControllerV2>());
                RemovePlaceholder();
                var camera = Object.FindFirstObjectByType<Camera>();
                var renderers = instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                if (camera == null || renderers.Length == 0) throw new InvalidDataException("Pet render bindings are missing.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.y * 1.2f, .5f);
                camera.transform.position = bounds.center + new Vector3(0, 0, Mathf.Max(bounds.size.magnitude * 2, 5));
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 1000;
                camera.backgroundColor = new Color(.12f, .14f, .19f);
                camera.clearFlags = CameraClearFlags.SolidColor;
            }
            // The delivered model faces +Z. Keep the validation view on its front.
            var pet = Object.FindFirstObjectByType<PetPresentation>();
            var petCamera = Object.FindFirstObjectByType<Camera>();
            if (pet != null && petCamera != null)
            {
                var renderers = pet.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                petCamera.transform.position = bounds.center + Vector3.forward * Mathf.Max(bounds.size.magnitude * 2, 5);
                petCamera.transform.LookAt(bounds.center);
            }
            EnsureAudioListener();
            EditorSceneManager.SaveScene(scene);
        }

        private static void BindCompanion()
        {
            var profile = AssetDatabase.LoadAssetAtPath<CompanionBehaviorProfile>("Assets/CompanionValidation/CH0069Behavior.asset");
            if (profile == null || profile.enablePat10Trial) throw new InvalidDataException("Spine profile missing or experimental Pat10 enabled.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Companion.unity");
            if (Object.FindFirstObjectByType<CompanionPresentation>() == null)
            {
                var host = new GameObject("Mika Companion");
                var loader = host.AddComponent<CompanionValidationBootstrap>();
                loader.enabled = false;
                loader.profile = profile;
                loader.renderCamera = Object.FindFirstObjectByType<Camera>();
                loader.enableLocalBehavior = true;
                loader.diagnosticsVisible = false;
                var adapter = host.AddComponent<CompanionPresentation>();
                Assign(adapter, "loader", loader);
                RemovePlaceholder();
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.DestroyImmediate(light.gameObject);
            }
            EnsureAudioListener();
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureAudioListener()
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera != null && camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        }

        private static void RemovePlaceholder()
        {
            var placeholder = GameObject.Find("CharacterPlaceholder");
            if (placeholder != null) Object.DestroyImmediate(placeholder);
        }

        private static void Assign(Object target, string field, Object value)
        {
            if (value == null) throw new InvalidDataException($"Missing {field} on {target.name}.");
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void IncludeSpineShader()
        {
            // Runtime loader uses Shader.Find; keep the shader in Player builds too.
            var shader = Shader.Find("Spine/Skeleton");
            if (shader == null) throw new InvalidDataException("Spine/Skeleton shader is missing.");
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included = settings.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < included.arraySize; i++) if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            int index = included.arraySize++;
            included.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
