using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GiftCompanion.Companion;
using Newtonsoft.Json;
using Spine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GiftCompanion.Editor
{
    // Editor-only capture in an unsaved, temporary scene. Never saves source assets or scenes.
    public static class WindowClockVisualProbe
    {
        private const string Output = "Docs/Design/MikaVisual/WindowProbe";
        public static void Capture()
        {
            try
            {
                Directory.CreateDirectory(Output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Temporary window probe camera").AddComponent<Camera>();
                camera.aspect = 1280f / 720;
                var loader = new GameObject("Temporary source Spine").AddComponent<CompanionValidationBootstrap>();
                loader.enabled = false;
                loader.profile = AssetDatabase.LoadAssetAtPath<CompanionBehaviorProfile>("Assets/CompanionValidation/CH0069Behavior.asset");
                loader.renderCamera = camera;
                loader.enableLocalBehavior = false;
                loader.Initialize();
                foreach (var atlas in loader.Character.SkeletonDataAsset.atlasAssets)
                    foreach (var material in atlas.Materials) material.shader = Shader.Find("GiftCompanion/SpineStraightAlpha");
                camera.gameObject.AddComponent<CompanionTone>();
                loader.Character.Update(1.25f);
                loader.Character.LateUpdate();
                var skeleton = loader.Character.Skeleton;
                var saved = skeleton.Slots.ToDictionary(s => s, s => s.Attachment);
                var records = new List<object>();
                foreach (var slot in skeleton.Slots)
                {
                    string attachmentPath = PathOf(slot.Attachment);
                    float[] vertices = null;
                    if (slot.Attachment is MeshAttachment mesh)
                    {
                        vertices = new float[mesh.WorldVerticesLength];
                        mesh.ComputeWorldVertices(slot, 0, vertices.Length, vertices, 0);
                    }
                    else if (slot.Attachment is RegionAttachment region)
                    {
                        vertices = new float[8]; region.ComputeWorldVertices(slot, vertices, 0);
                    }
                    var screen = new List<object>();
                    if (vertices != null)
                        for (int i = 0; i < vertices.Length; i += 2)
                        {
                            var point = camera.WorldToViewportPoint(loader.Character.transform.TransformPoint(new Vector3(vertices[i], vertices[i + 1], 0)));
                            screen.Add(new { x = point.x * 1280, y = (1 - point.y) * 720 });
                        }
                    records.Add(new { slot = slot.Data.Name, attachment = slot.Attachment?.Name,
                        path = attachmentPath, background = IsBackground(attachmentPath), vertices = screen,
                        uv = (slot.Attachment as MeshAttachment)?.RegionUVs });
                }
                Render(camera, "scene.png");
                foreach (var slot in skeleton.Slots)
                    if (!IsBackground(PathOf(slot.Attachment))) slot.Attachment = null;
                loader.Character.LateUpdate();
                Render(camera, "window-without-character-curtains.png");
                // Include window trim and flowers so the prototype uses source occlusion, not a guessed head outline.
                foreach (var slot in skeleton.Slots)
                {
                    string value = PathOf(saved[slot]);
                    slot.Attachment = !IsBackground(value) || value.StartsWith("BG2/Window_", StringComparison.Ordinal)
                        ? saved[slot] : null;
                }
                camera.backgroundColor = UnityEngine.Color.clear;
                loader.Character.LateUpdate();
                Render(camera, "foreground.png");
                foreach (var slot in skeleton.Slots) slot.Attachment = saved[slot];
                File.WriteAllText(Output + "/geometry.json", JsonConvert.SerializeObject(new {
                    unity = Application.unityVersion, width = 1280, height = 720,
                    temporaryScene = true, assetsSaved = false, slots = records
                }, Formatting.Indented));
                // Closing the process discards this scene; no SaveScene/SaveAssets calls.
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static string PathOf(Attachment attachment) => attachment is MeshAttachment mesh ? mesh.Path
            : attachment is RegionAttachment region ? region.Path : "";
        private static bool IsBackground(string value) => !string.IsNullOrEmpty(value)
            && (value.StartsWith("BG/", StringComparison.Ordinal) || value.StartsWith("BG2/", StringComparison.Ordinal)
                || value.StartsWith("BG_", StringComparison.Ordinal))
            && value.IndexOf("Curtain", StringComparison.OrdinalIgnoreCase) < 0;
        private static void Render(Camera camera, string filename)
        {
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(Output + "/" + filename, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(pixels); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }
}
