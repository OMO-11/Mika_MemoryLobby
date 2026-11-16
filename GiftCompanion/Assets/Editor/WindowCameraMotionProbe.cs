using System;
using System.IO;
using System.Linq;
using GiftCompanion.Companion;
using Spine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace GiftCompanion.Editor {
// Visual reference only: unsaved temporary scene; source attachments are never edited.
public static class WindowCameraMotionProbe {
 const string Output="Docs/Design/MikaVisual/CameraMotionV1/Layers";
 public static void Capture(){try{
 Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
 var camera=new GameObject("Temporary motion probe camera").AddComponent<Camera>();camera.aspect=1280f/720;
 var loader=new GameObject("Temporary source Spine").AddComponent<CompanionValidationBootstrap>();loader.enabled=false;
 loader.profile=AssetDatabase.LoadAssetAtPath<CompanionBehaviorProfile>("Assets/CompanionValidation/CH0069Behavior.asset");loader.renderCamera=camera;loader.enableLocalBehavior=false;loader.Initialize();
 foreach(var atlas in loader.Character.SkeletonDataAsset.atlasAssets)foreach(var material in atlas.Materials)material.shader=Shader.Find("GiftCompanion/SpineStraightAlpha");
 camera.gameObject.AddComponent<CompanionTone>();loader.Character.Update(1.25f);loader.Character.LateUpdate();
 var slots=loader.Character.Skeleton.Slots;var saved=slots.ToDictionary(s=>s,s=>s.Attachment);
 Render(camera,"scene.png");camera.backgroundColor=Color.clear;
 foreach(string layer in new[]{"room","character","curtains"}){
 foreach(var slot in slots){string path=PathOf(saved[slot]);bool curtain=path.IndexOf("Curtain",StringComparison.OrdinalIgnoreCase)>=0;
 bool bg=path.StartsWith("BG/",StringComparison.Ordinal)||path.StartsWith("BG2/",StringComparison.Ordinal)||path.StartsWith("BG_",StringComparison.Ordinal);
 bool keep=layer=="curtains"?curtain:layer=="room"?bg&&!curtain:!bg&&!curtain;
 slot.Attachment=keep?saved[slot]:null;}
 loader.Character.LateUpdate();Render(camera,layer+".png");
 }
 foreach(var slot in slots)slot.Attachment=saved[slot];EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static string PathOf(Attachment a)=>a is MeshAttachment m?m.Path:a is RegionAttachment r?r.Path:"";
 static void Render(Camera camera,string name){var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
 try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name,pixels.EncodeToPNG());}
 finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);}}
}}
