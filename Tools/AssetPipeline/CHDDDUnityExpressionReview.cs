using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class CHDDDUnityExpressionReview
{
    const string Assets="Assets/Review";
    const string Output="W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/UnityExpressionV17";
    [Serializable] public class Frame {public float time;public float[] shapes,mouth;public float eyeSkinMask,oldEyeMask,squeeze,omegaEye,omegaMouth,hideMouth;}
    [Serializable] public class Expression {public string name;public int revision;public Frame[] frames;}
    [Serializable] public class Data {public int blenderRevision;public string sourceSHA256,sourceBlend;public float fps,durationSeconds;public string[] shapeNames;public int[] mouthTiles;public Expression[] expressions;}
    [Serializable] public class Result {public bool passed,shaderErrors,masksImported,sourceBlendUnchanged;public string unityVersion,graphicsDevice,graphicsAPI,colorSpace,sourceSHA256;public int blenderRevision,frames,blendShapes,actualMsaa;public string[] nativeResolutions;}
    static Data data;static GameObject instance;static AnimationClip clip;static SkinnedMeshRenderer body;
    static Camera camera;static Material face,eyeMouth,mouth,squeeze,omegaEyes,omegaMouth;
    static SkinnedMeshRenderer eyeCards;
    static List<Material> materials=new List<Material>();
    static int expressionIndex,viewIndex,frameIndex,phase,total;static bool keyOnly;
    static readonly string[] views={"Face","Full160","Full240"};
    static readonly Dictionary<string,RenderTexture> targets=new Dictionary<string,RenderTexture>();
    static Texture2D pixels;static RenderTexture currentTarget;

    [MenuItem("CHDDD Review/Render expressions")]
    public static void Run()
    {
        try {
            keyOnly=Environment.GetCommandLineArgs().Contains("-reviewKeyOnly");
            Directory.CreateDirectory(Output+"/Preview");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            data=JsonUtility.FromJson<Data>(File.ReadAllText(Assets+"/expression_samples.json"));
            foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{Assets+"/Textures"}).Select(AssetDatabase.GUIDToAssetPath)) {
                var texture=(TextureImporter)AssetImporter.GetAtPath(path);
                texture.textureCompression=TextureImporterCompression.Uncompressed;texture.mipmapEnabled=false;texture.filterMode=FilterMode.Bilinear;
                texture.wrapMode=TextureWrapMode.Clamp;texture.alphaIsTransparency=false;texture.SaveAndReimport();
            }
            string modelPath=Assets+"/Model/CHDDD_ExpressionReview.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importBlendShapes=true;importer.importAnimation=true;importer.isReadable=true;importer.optimizeGameObjects=false;
            importer.importNormals=ModelImporterNormals.Import;importer.importBlendShapeNormals=ModelImporterNormals.Import;
            importer.SaveAndReimport();
            clip=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            instance.name="CHDDD_UnityExpressionReview";
            var animator=instance.GetComponent<Animator>();if(animator)animator.enabled=false;
            if(!AssetDatabase.IsValidFolder(Assets+"/RuntimeMaterials"))AssetDatabase.CreateFolder(Assets,"RuntimeMaterials");
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)) {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++) {
                    string name=slots[i].name;
                    Material material;
                    if(name.StartsWith("CHDDD_Face")) {material=FromBase("CHDDD_Face","CHDDD/ReviewFace");face=material;}
                    else if(name.StartsWith("CHDDD_EyeMouth")) {material=NewMaterial("ReviewEyeMouth","CHDDD/ReviewEyeMouth");material.mainTexture=Texture("CH0069_EyeMouth");eyeMouth=material;}
                    else if(name.StartsWith("CHDDD_MouthAtlas")) {material=NewMaterial("ReviewMouthAtlas","CHDDD/ReviewMouthAtlas");material.mainTexture=Texture("Character_Mouth");mouth=material;}
                    else if(name.StartsWith("Review9_SqueezeEyeLine")) {material=NewMaterial("ReviewSqueeze","CHDDD/ReviewOverlay");material.mainTexture=Texture("mika_squeeze_eyes_review");squeeze=material;}
                    else if(name.StartsWith("Review16_OmegaEyeLine")) {
                        material=NewMaterial("ReviewOmegaEyes","CHDDD/ReviewOverlay");material.mainTexture=Texture("CH0069_Face");
                        material.SetFloat("_ApplyLinearTint",1);material.SetVector("_LinearTint",new Vector4(.4f,.32f,.34f,1));omegaEyes=material;
                    } else if(name.StartsWith("Review16_OmegaMouth")) {
                        material=NewMaterial("ReviewOmegaMouth","CHDDD/ReviewOverlay");material.SetColor("_Color",new Color(.39f,.17f,.14f,1).gamma);omegaMouth=material;
                    } else {
                        string baseName=name.Split('.')[0];material=AssetDatabase.LoadAssetAtPath<Material>(Assets+"/BaseMaterials/"+baseName+".mat");
                        if(!material)throw new Exception("Missing base Unity material "+name);
                    }
                    slots[i]=material;
                }
                renderer.sharedMaterials=slots;
                var skinned=renderer as SkinnedMeshRenderer;
                if(skinned){skinned.updateWhenOffscreen=true;skinned.quality=SkinQuality.Bone4;if(skinned.sharedMesh.blendShapeCount>0)body=skinned;}
                if(skinned&&renderer.name=="CHDDD_TestSqueeze_EyeLines")eyeCards=skinned;
            }
            if(body==null||face==null||eyeMouth==null||mouth==null||squeeze==null||omegaEyes==null||omegaMouth==null)throw new Exception("Missing body or expression renderers/materials");
            mouth.SetFloat("_MouthBlendEnabled",1);
            mouth.SetVector("_MouthTiles0",new Vector4(300,706,705,604));mouth.SetVector("_MouthTiles1",new Vector4(605,305,401,200));
            camera=new GameObject("Unity expression review camera").AddComponent<Camera>();camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(56/255f,60/255f,72/255f,0);
            camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.allowHDR=false;camera.allowMSAA=true;
            var light=new GameObject("Review directional light").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(30,-25,0);light.intensity=1;
            RenderSettings.ambientLight=Color.white;RenderSettings.ambientIntensity=1;QualitySettings.antiAliasing=8;
            foreach(string view in views) {
                int width=view=="Face"?480:view=="Full160"?160:240;
                int height=view=="Face"?400:Mathf.RoundToInt(width*580/480f);
                var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=8};target.Create();targets.Add(view,target);
            }
            expressionIndex=viewIndex=frameIndex=phase=total=0;
            if(keyOnly)frameIndex=29;
            EditorApplication.update+=Step;EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log("UNITY_EXPRESSION_BEGIN "+Application.unityVersion+" "+SystemInfo.graphicsDeviceType+" "+SystemInfo.graphicsDeviceName+" clip="+clip.name+" length="+clip.length);
        }catch(Exception error){Fail(error);}
    }
    static Texture2D Texture(string name){var t=AssetDatabase.LoadAssetAtPath<Texture2D>(Assets+"/Textures/"+name+".png");if(!t)throw new Exception("Missing texture "+name);return t;}
    static Material NewMaterial(string name,string shader) {
        var s=Shader.Find(shader);if(!s)throw new Exception("Missing shader "+shader);
        string path=Assets+"/RuntimeMaterials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material)material.shader=s;else{material=new Material(s);AssetDatabase.CreateAsset(material,path);}
        material.name=name;materials.Add(material);return material;
    }
    static Material FromBase(string name,string shader){var material=NewMaterial("Review"+name,shader);material.CopyPropertiesFromMaterial(AssetDatabase.LoadAssetAtPath<Material>(Assets+"/BaseMaterials/"+name+".mat"));return material;}
    static Transform Find(string name){return instance.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);}
    static void Pose(Frame f) {
        clip.SampleAnimation(instance,f.time);
        for(int i=0;i<data.shapeNames.Length;i++){int index=body.sharedMesh.GetBlendShapeIndex(data.shapeNames[i]);if(index<0)throw new Exception("Lost blendshape "+data.shapeNames[i]);body.SetBlendShapeWeight(index,f.shapes[i]*100);}
        face.SetFloat("_ReviewEyeMask",f.eyeSkinMask);eyeMouth.SetFloat("_ReviewEyeMask",f.oldEyeMask);
        var cardVertices=eyeCards.sharedMesh.vertices;var skin=eyeCards.bones[eyeCards.sharedMesh.boneWeights[0].boneIndex0].localToWorldMatrix*eyeCards.sharedMesh.bindposes[eyeCards.sharedMesh.boneWeights[0].boneIndex0];
        Func<int,Vector3> vertex=index=>skin.MultiplyPoint3x4(cardVertices[index]);
        var observer=(Find("UnityReview_FaceCamera").position-Find("UnityReview_FaceTarget").position).normalized;
        var normalA=Vector3.Cross(vertex(1)-vertex(0),vertex(2)-vertex(0)).normalized;
        var normalB=Vector3.Cross(vertex(5)-vertex(4),vertex(6)-vertex(4)).normalized;
        if(Vector3.Dot(normalA,observer)<0)normalA=-normalA;if(Vector3.Dot(normalB,observer)<0)normalB=-normalB;
        face.SetVector("_ReviewFaceNormal",(normalA+normalB).normalized);
        squeeze.SetFloat("_Opacity",f.squeeze);omegaEyes.SetFloat("_Opacity",f.omegaEye);omegaMouth.SetFloat("_Opacity",f.omegaMouth);
        mouth.SetFloat("_ReviewHide",f.hideMouth);
        mouth.SetVector("_MouthWeights0",new Vector4(f.mouth[0],f.mouth[1],f.mouth[2],f.mouth[3]));
        mouth.SetVector("_MouthWeights1",new Vector4(f.mouth[4],f.mouth[5],f.mouth[6],f.mouth[7]));
    }
    static void View(string view) {
        bool close=view=="Face";currentTarget=targets[view];camera.targetTexture=currentTarget;
        camera.transform.position=Find(close?"UnityReview_FaceCamera":"UnityReview_FullCamera").position;
        camera.transform.LookAt(Find(close?"UnityReview_FaceTarget":"UnityReview_FullTarget").position,Vector3.up);
        // Blender ortho_scale is the width when the render is wider than tall.
        camera.orthographicSize=close?.43f*.5f*400/480f:1.42f*.5f;
        if(pixels)UnityEngine.Object.DestroyImmediate(pixels);
        pixels=new Texture2D(currentTarget.width,currentTarget.height,TextureFormat.RGBA32,false);
    }
    static void Step() {
        try {
            if(phase==0){Pose(data.expressions[expressionIndex].frames[frameIndex]);View(views[viewIndex]);phase=1;EditorApplication.QueuePlayerLoopUpdate();return;}
            if(phase<3){phase++;EditorApplication.QueuePlayerLoopUpdate();return;}
            camera.Render();RenderTexture.active=currentTarget;pixels.ReadPixels(new Rect(0,0,currentTarget.width,currentTarget.height),0,0);pixels.Apply();RenderTexture.active=null;
            string name=data.expressions[expressionIndex].name+"_"+views[viewIndex]+"_"+frameIndex.ToString("000");
            File.WriteAllBytes(Output+"/Preview/"+name+".png",pixels.EncodeToPNG());total++;
            phase=0;frameIndex++;
            if(keyOnly||frameIndex>=58){Debug.Log("UNITY_EXPRESSION_RENDERED "+data.expressions[expressionIndex].name+" "+views[viewIndex]);frameIndex=keyOnly?29:0;viewIndex++;}
            if(viewIndex>=views.Length){viewIndex=0;expressionIndex++;}
            if(expressionIndex>=data.expressions.Length){Finish();return;}
            EditorApplication.QueuePlayerLoopUpdate();
        }catch(Exception error){Fail(error);}
    }
    static void Finish() {
        EditorApplication.update-=Step;
        Pose(data.expressions[1].frames[29]);View("Full240");camera.targetTexture=null;
        foreach(var material in materials)EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Assets+"/UnityExpressions.unity");
        var colors=body.sharedMesh.colors;
        bool masks=colors.Length==body.sharedMesh.vertexCount&&colors.Any(c=>c.r>.9f)&&colors.Any(c=>c.g>.9f)&&colors.Any(c=>c.b>.9f);
        bool shaderErrors=materials.Any(m=>ShaderUtil.ShaderHasError(m.shader));
        string hash;
        using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(data.sourceBlend))).Replace("-","").ToLowerInvariant();
        var result=new Result{passed=masks&&!shaderErrors&&hash==data.sourceSHA256,shaderErrors=shaderErrors,masksImported=masks,sourceBlendUnchanged=hash==data.sourceSHA256,
            unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,graphicsAPI=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString(),
            sourceSHA256=hash,blenderRevision=data.blenderRevision,frames=total,blendShapes=body.sharedMesh.blendShapeCount,actualMsaa=targets["Face"].antiAliasing,
            nativeResolutions=new[]{"480x400","160x193","240x290"}};
        File.WriteAllText(Output+"/unity_validation.json",JsonUtility.ToJson(result,true));Debug.Log("UNITY_EXPRESSION_COMPLETE "+JsonUtility.ToJson(result));
        if(Application.isBatchMode)EditorApplication.Exit(result.passed?0:3);
    }
    static void Fail(Exception error){EditorApplication.update-=Step;Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(3);}
}
