using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CH0069UnityAudit
{
    [Serializable] public class ClipCheck { public string name; public float length; public int curves; }
    [Serializable] public class Probe {
        public string file; public bool avatarValid; public bool avatarHuman;
        public int skinnedMeshes; public int vertices; public int triangles;
        public ClipCheck[] clips; public float sampledHeadAngle; public float headAngleOnOriginalModel;
    }
    [Serializable] public class Report { public string unityVersion; public Probe[] probes; public bool passed; }

    public static void Run()
    {
        try {
            string output = Environment.GetEnvironmentVariable("CH0069_AUDIT_OUTPUT");
            var probes = new[] { "CH0069_original_probe.fbx", "CH0069_custom_probe.fbx" }
                .Select(file => Check(file)).ToArray();
            var customClip = AssetDatabase.LoadAllAssetsAtPath("Assets/CH0069_custom_probe.fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => c.name.Contains("Audit_CustomHeadTurn"));
            if (customClip != null) {
                var original = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CH0069_original_probe.fbx"));
                probes[1].headAngleOnOriginalModel = SampleHead(original, customClip);
                UnityEngine.Object.DestroyImmediate(original);
            }
            var report = new Report { unityVersion = Application.unityVersion, probes = probes,
                passed = probes.All(p => p.avatarValid && !p.avatarHuman && p.skinnedMeshes == 3)
                    && probes[0].clips.Length >= 33 && probes[1].clips.Any(c => c.name.Contains("Audit_CustomHeadTurn"))
                    && probes[1].sampledHeadAngle > 1f && probes[1].headAngleOnOriginalModel > 1f };
            File.WriteAllText(Path.Combine(output, "unity_audit.json"), JsonUtility.ToJson(report, true));
            Debug.Log("CH0069_AUDIT " + JsonUtility.ToJson(report));
            EditorApplication.Exit(report.passed ? 0 : 2);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(3); }
    }

    static Probe Check(string file)
    {
        string path = "Assets/" + file;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.motionNodeName = "bone_root";
        importer.isReadable = true;
        importer.SaveAndReimport();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var instance = UnityEngine.Object.Instantiate(prefab);
        var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var probe = new Probe { file = file, avatarValid = avatar != null && avatar.isValid,
            avatarHuman = avatar != null && avatar.isHuman, skinnedMeshes = renderers.Length,
            vertices = renderers.Sum(r => r.sharedMesh.vertexCount),
            triangles = renderers.Sum(r => r.sharedMesh.triangles.Length / 3),
            clips = clips.Select(c => new ClipCheck { name = c.name, length = c.length,
                curves = AnimationUtility.GetCurveBindings(c).Length }).ToArray() };
        var custom = clips.FirstOrDefault(c => c.name.Contains("Audit_CustomHeadTurn"));
        if (custom != null) {
            probe.sampledHeadAngle = SampleHead(instance, custom);
        }
        UnityEngine.Object.DestroyImmediate(instance);
        return probe;
    }

    static float SampleHead(GameObject instance, AnimationClip clip)
    {
        var head = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip001 Head");
        clip.SampleAnimation(instance, 0f);
        var first = head.localRotation;
        clip.SampleAnimation(instance, clip.length / 2f);
        return Quaternion.Angle(first, head.localRotation);
    }
}
