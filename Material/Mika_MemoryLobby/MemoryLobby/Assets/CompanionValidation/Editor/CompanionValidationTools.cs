using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CompanionValidationTools {
    const string Folder = "Assets/CompanionValidation";
    public static string OutputDirectory {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../Docs/Audits/Spine/MemoryLobby/RuntimeBehavior")); }
    }
    [MenuItem("Companion Validation/Create Scene")]
    public static void CreateScene() {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the current scene before creating the validation scene.");
        var profile = AssetDatabase.LoadAssetAtPath<CompanionBehaviorProfile>(Folder + "/CH0069Behavior.asset");
        if (profile == null) {
            profile = ScriptableObject.CreateInstance<CompanionBehaviorProfile>();
            profile.resourceFolder = "CH0069_Source";
            profile.skeletonFile = "CH0069_home.skel.bytes";
            profile.atlasFile = "CH0069_home.atlas.txt";
            profile.textureFiles = new[] {"CH0069_home.png", "CH0069_home2.png", "CH0069_home3.png", "CH0069_home4.png"};
            profile.idleAnimation = "Idle_01";
            profile.gazeBone = "Touch_Eye_Key";
            profile.headBone = "Head_Rot";
            profile.mouthSlot = "Mouse_normal_01";
            profile.closedMouth = "Mouse_normal_close";
            profile.openMouth = "Mouse_normal_01";
            AssetDatabase.CreateAsset(profile, Folder + "/CH0069Behavior.asset");
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Companion Validation");
        var boot = root.AddComponent<CompanionValidationBootstrap>();
        boot.profile = profile;
        var cameraObject = new GameObject("Main Camera");
        cameraObject.transform.SetParent(root.transform, false);
        boot.renderCamera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";
        var lightObject = new GameObject("Directional Light");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.AddComponent<Light>().type = LightType.Directional;
        EditorSceneManager.SaveScene(scene, Folder + "/CompanionValidation.unity");
        AssetDatabase.SaveAssets();
    }
    public static string Capture(string fileName) {
        var boot = UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        if (boot == null || boot.Character == null) throw new InvalidOperationException("Validation scene must be playing.");
        // Manual pose checks can run before Unity's normal LateUpdate; render the current mesh.
        boot.Character.LateUpdate();
        Directory.CreateDirectory(OutputDirectory);
        string output = Path.Combine(OutputDirectory, Path.GetFileName(fileName));
        var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
        var old = RenderTexture.active;
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try {
            target.Create();
            RenderPipeline.SubmitRenderRequest(boot.renderCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(output, texture.EncodeToPNG());
        } finally {
            RenderTexture.active = old;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
        return output;
    }
    public static string CaptureLocalPose(string fileName, float x, float y, float head) {
        var boot=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        boot.Character.GetComponent<CompanionLocalMotion>().SetReviewPose(new Vector2(x,y),head);
        return Capture(fileName);
    }
    public static void ConfigureCandidates() {
        var boot=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        var profile=boot.profile;
        var data=boot.Character.Skeleton.Data;
        profile.touchExpressions=new[]{
            Sample(data,"pat_closed_eyes","Pat_01_A",0,"Mouse_Depressed_02_close",true),
            Sample(data,"pat_playful","Talk_01_A",2,"Mouse_doya_03_close",false),
            Sample(data,"pat_shy","Talk_03_A",12,"Mouse_shy_close",false)};
        profile.touchExpressions[2].gazeBias=new Vector2(-.12f,.04f);
        profile.thinkingExpressions=new[]{Sample(data,"thinking_neutral","Talk_03_A",.7f,"Mouse_normal_close",false)};
        profile.thinkingExpressions[0].enabled=true;
        profile.blinkAttachments=profile.touchExpressions[0].attachments.Where(a=>a.slot!=profile.mouthSlot).ToArray();
        boot.Expression.Bind(boot.Character);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }
    public static void ConfigureImprovements() {
        var profile=AssetDatabase.LoadAssetAtPath<CompanionBehaviorProfile>(Folder+"/CH0069Behavior.asset");
        profile.eyesOnlyIdle=true;profile.gazeCouplingConstraint="Touch_Eye_Key";profile.touchPointBone="Touch_Point_Key";
        profile.touchMotionAnimation="Pat_01_M";profile.touchReleaseAnimation="PatEnd_01_M";
        profile.headHitSlots=new[]{"Face","Ball_Hair_01","F_Hair_01","F_Hair_02"};
        profile.expressionBlendSeconds=.16f;profile.mouthBlendSeconds=.065f;
        profile.pointerAttentionSeconds=2.2f;
        profile.touchExpressions[0].openMouth="Mouse_Depressed_02";
        profile.touchExpressions[1].openMouth="Mouse_doya_03";
        profile.touchExpressions[2].openMouth="Mouse_shy";
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
    static CompanionBehaviorProfile.ExpressionSetting Sample(Spine.SkeletonData data,string id,string animation,float time,string mouth,bool closedEyes) {
        var scratch=new Spine.Skeleton(data);
        data.FindAnimation("Idle_01").Apply(scratch,-1,0,false,null,1,Spine.MixBlend.Replace,Spine.MixDirection.In);
        var original=data.FindAnimation(animation);
        original.Apply(scratch,-1,time,false,null,1,Spine.MixBlend.Replace,Spine.MixDirection.In);
        var indices=original.Timelines.OfType<Spine.AttachmentTimeline>().Select(t=>t.SlotIndex).Distinct().ToArray();
        var settings=indices.Select(index=>scratch.Slots.Items[index]).Where(s=>System.Text.RegularExpressions.Regex.IsMatch(s.Data.Name,"eye|mouse",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(s=>new CompanionBehaviorProfile.AttachmentSetting {slot=s.Data.Name,attachment=s.Data.Name=="Mouse_normal_01"?mouth:s.Attachment==null?"":s.Attachment.Name}).ToArray();
        return new CompanionBehaviorProfile.ExpressionSetting {id=id,sourceAnimation=animation,sourceTime=time,closedEyes=closedEyes,attachments=settings,enabled=false};
    }
    [Serializable] public class Inventory {
        public string version;
        public int bones, slots, animations;
        public SlotInfo[] facialSlots;
        public PoseInfo[] poses;
    }
    [Serializable] public class SlotInfo { public string name, bone, setup; public string[] attachments; }
    [Serializable] public class PoseInfo { public string name; public float duration; public string[] properties; }
    public static string ExportInventory() {
        var boot = UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        var d = boot.Character.Skeleton.Data;
        var inventory = new Inventory {version=d.Version, bones=d.Bones.Count, slots=d.Slots.Count, animations=d.Animations.Count,
            facialSlots=d.Slots.Where(s=>System.Text.RegularExpressions.Regex.IsMatch(s.Name,"eye|mouse|face",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Select(s=>new SlotInfo {name=s.Name,bone=s.BoneData.Name,setup=s.AttachmentName,
                    attachments=d.DefaultSkin.Attachments.Where(e=>e.SlotIndex==s.Index).Select(e=>e.Name).ToArray()}).ToArray(),
            poses=d.Animations.Select(a=>new PoseInfo {name=a.Name,duration=a.Duration,
                properties=a.Timelines.SelectMany(t=>t.PropertyIds).Distinct().ToArray()}).ToArray()};
        Directory.CreateDirectory(OutputDirectory);
        string output=Path.Combine(OutputDirectory,"source_inventory.json");
        File.WriteAllText(output,JsonUtility.ToJson(inventory,true));
        return output;
    }
}
