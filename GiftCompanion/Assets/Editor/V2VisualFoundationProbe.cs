using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using GiftCompanion.Companion;
using GiftCompanion.Core.Config;
using GiftCompanion.SharedUI;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GiftCompanion.Editor
{
    public static class V2VisualFoundationProbe
    {
        const string Output = "Docs/Design/V2_Visual_Implementation/02_执行记录/V2-1";
        static readonly List<string> checks = new List<string>();
        static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks.Add(name); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            try
            {
                foreach (var id in new[] {"room","character","curtains"})
                {
                    string path = "Assets/App/Companion/Resources/MikaVisual/"+id+".png";
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048; importer.alphaIsTransparency=true; importer.SaveAndReimport();
                }
                foreach (string path in Directory.GetFiles("Assets/App/Companion/Resources/MikaVisual/Icons", "*.png"))
                { var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/')); importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single; importer.alphaIsTransparency=true; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
                var data = Path.GetFullPath(Output+"/IsolatedData/"+Guid.NewGuid().ToString("N"));
                var service = new CompanionVisualService(data, CompanionVisualFoundation.DefaultJson);
                service.Load(CancellationToken.None); Check(!File.Exists(service.FilePath),"defaults do not write user files");
                var space=service.Value.FindSpace("mika-memory"); Check(space != null && service.Value.FindSpace("unknown-import") == null,"space-specific lookup and unknown-space fallback");
                service.Save(service.Value,CancellationToken.None);
                var reloaded=new CompanionVisualService(data,CompanionVisualFoundation.DefaultJson); reloaded.Load(CancellationToken.None); Check(reloaded.Value.Spaces[0].Zoom==2.3f,"atomic save and reload");
                File.WriteAllText(service.FilePath,"{broken"); reloaded.Load(CancellationToken.None); Check(reloaded.Issue!=null && File.ReadAllText(service.FilePath)=="{broken" && reloaded.Value.Spaces[0].Zoom==2.3f,"broken JSON preserves original and last valid value");
                File.WriteAllText(service.FilePath,"{\"Version\":99}"); reloaded.Load(CancellationToken.None); Check(reloaded.Issue!=null,"unsupported version fallback");
                File.WriteAllText(service.FilePath,CompanionVisualFoundation.DefaultJson); reloaded.Load(CancellationToken.None); Check(reloaded.Issue==null,"reload recovers after correction");
                var invalid=JsonConvert.DeserializeObject<CompanionVisualSettings>(CompanionVisualFoundation.DefaultJson); invalid.Spaces.Add(invalid.Spaces[0]); bool rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"duplicate space rejected");
                invalid=JsonConvert.DeserializeObject<CompanionVisualSettings>(CompanionVisualFoundation.DefaultJson);invalid.Spaces[0].Zoom=float.NaN;rejected=false;try{invalid.Validate();}catch(InvalidDataException){rejected=true;}Check(rejected,"nonfinite camera rejected");
                var cancel=new CancellationToken(true);rejected=false;try{service.Save(service.Value,cancel);}catch(OperationCanceledException){rejected=true;}Check(rejected,"canceled save rejected");
                foreach (float progress in new[]{0f,.35f,1f}) foreach(var point in new[]{new Vector2(0,0),new Vector2(1060,190),new Vector2(1280,720)})
                    Check(Vector2.Distance(point,CompanionVisualFoundation.Unproject(CompanionVisualFoundation.Project(point,space,progress),space,progress))<.001f,"projection roundtrip "+progress+" "+point);
                foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(2560,1080),new Vector2(800,800)})
                {var rect=CompanionVisualFoundation.Viewport(size.x,size.y);Check(rect.width<=size.x&&rect.height<=size.y&&Mathf.Abs(rect.width/rect.height-1280f/720)<.001f,"letterbox "+size);}
                var font=CompanionVisualFoundation.Font;
                string sample="聊天记录日记陪伴空间人物设定设置回到未花老师 こんにちは、未花です。！？……☆(*´▽`*)";
                bool covered=font.HasCharacters(sample,out uint[] missing,true,true);
                File.WriteAllText(Output+"/font_coverage.json",JsonConvert.SerializeObject(new{covered,missing,font=font.name,fallback=CompanionUIFactory.Chinese.name},Formatting.Indented));
                Check(covered,"Chinese Japanese punctuation and kaomoji glyph coverage");
                foreach(var icon in new[]{"double-star","swiss-roll","next","prev","play","pause","volume"}) Check(CompanionVisualFoundation.Icon("icon-"+icon)!=null,"sprite "+icon);
                Capture(service.Value,space,0,"人物起点.png"); Capture(service.Value,space,1,"窗边终点.png");Capture(service.Value,null,0,"未适配空间菜单回退.png");
                File.WriteAllText(Output+"/validation.json",JsonConvert.SerializeObject(new{passed=true,unity=Application.unityVersion,checks,scope="Edit-time foundation/static reference layers; no production motion, conversation or audio tests"},Formatting.Indented));
                AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(Output+"/validation.json",JsonConvert.SerializeObject(new{passed=false,checks,error=e.ToString()},Formatting.Indented));EditorApplication.Exit(1);}
        }
        static void Capture(CompanionVisualSettings settings, VisualSpace space,float progress,string filename)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Static foundation camera").AddComponent<Camera>(); camera.orthographic=true;camera.orthographicSize=360;camera.aspect=1280f/720;camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=CompanionVisualFoundation.Color("#232943");camera.clearFlags=CameraClearFlags.SolidColor;
            var canvas=new GameObject("Static foundation canvas",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)canvas.transform).sizeDelta=new Vector2(1280,720);
            Layer(canvas.transform,"room",space,progress,1);
            var theme=settings.Theme;
            if(space!=null)
            {
                float scale=Mathf.Lerp(1,space.Zoom,progress),angle=-(space.UiAngle+space.Rotation*progress);
                CompanionVisualFoundation.CreateMenu(canvas.transform,settings,space.Id,progress,null);
                var pos=CompanionVisualFoundation.Project(new Vector2(space.ClockX,space.ClockY),space,progress);
                var clock=new GameObject("Clock",typeof(RectTransform)).GetComponent<RectTransform>();clock.SetParent(canvas.transform,false);clock.anchoredPosition=new Vector2(pos.x-640,360-pos.y);clock.localRotation=Quaternion.Euler(0,0,angle);
                Label(clock,"21 : 06",Vector2.zero,theme.ClockSize*scale,theme.ClockAlpha,theme.ClockInk);
                Label(clock,"2026.11.02",new Vector2(scale,-38*scale),theme.DateSize*scale,theme.ClockAlpha,theme.ClockInk);
            }
            Layer(canvas.transform,"curtains",space,progress,1.05f);Layer(canvas.transform,"character",space,progress,2.5f);
            if(space==null){int called=-1;var menu=CompanionVisualFoundation.CreateMenu(canvas.transform,settings,"unknown-import",0,index=>called=index);menu.GetComponentsInChildren<Button>()[4].onClick.Invoke();Check(called==4,"unknown-space fallback settings callback");}
            Canvas.ForceUpdateCanvases(); foreach(var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))text.ForceMeshUpdate();
            var rt=new RenderTexture(1280,720,24);var previous=RenderTexture.active;var pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+filename,pixels.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Label(Transform parent,string value,Vector2 pos,float size,float alpha,string color)
        {var text=CompanionUIFactory.Text(parent,value,value,pos,new Vector2(420,90),size,TextAlignmentOptions.TopLeft);var rect=(RectTransform)text.transform;rect.pivot=new Vector2(0,1);rect.anchoredPosition=pos;text.font=CompanionVisualFoundation.Font;text.color=CompanionVisualFoundation.Color(color,alpha);text.overflowMode=TextOverflowModes.Overflow;}
        static void Layer(Transform parent,string id,VisualSpace space,float p,float depth)
        {
            var rect=CompanionUIFactory.Rect(parent,id,Vector2.zero,new Vector2(1280,720));var image=rect.gameObject.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>("MikaVisual/"+id);image.raycastTarget=false;
            if(space==null)return;
            Vector2 center=new Vector2(640,360);if(id=="character")center+=new Vector2(-700,300)*p*p;
            var projected=CompanionVisualFoundation.Project(center,space,p,depth);rect.anchoredPosition=new Vector2(projected.x-640,360-projected.y);rect.localScale=Vector3.one*Mathf.Lerp(1,space.Zoom,p)*(1+(depth-1)*p);rect.localRotation=Quaternion.Euler(0,0,-space.Rotation*p);
        }
    }
}
