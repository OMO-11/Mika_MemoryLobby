using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>Actual Unity skinned rendering at 24 FPS; each pose spans editor frames.</summary>
public static class CHDDDReadabilityV5Frames {
    static readonly string[] names={"Confused","Success","Wave","HeadClick","BodyClick"};
    const string root="W:/Machine/VSCode/Mika_Gift/Material/CHDDD";
    static GameObject instance;static Animator animator;static CHDDDBodyControllerV2 body;
    static CHDDDActionFaceControllerV2 face;static CHDDDBodyControllerV2.ActionMap map;
    static Camera camera;static RenderTexture target;static Texture2D pixels;
    static int actionIndex,frameIndex,phase,count,total;
    public static void Run() {
        try {
            Directory.CreateDirectory(root+"/Previews/ActionsV2/UnityMotion");
            map=JsonUtility.FromJson<CHDDDBodyControllerV2.ActionMap>(File.ReadAllText(root+"/Unity/body_action_map_v2.json"));
            if(map.revision!=7) throw new Exception("Expected revision 7");
            instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHDDD/Unity/CHDDD_Actions_v2.prefab"));
            animator=instance.GetComponent<Animator>();body=instance.GetComponent<CHDDDBodyControllerV2>();
            face=instance.GetComponent<CHDDDActionFaceControllerV2>();var manual=instance.GetComponent<CHDDDExpressionController>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            animator.Rebind();animator.Update(0);
            typeof(CHDDDExpressionController).GetMethod("Awake",flags).Invoke(manual,null);
            typeof(CHDDDActionFaceControllerV2).GetMethod("Awake",flags).Invoke(face,null);
            typeof(CHDDDBodyControllerV2).GetMethod("Awake",flags).Invoke(body,null);
            face.enabled=false;body.enabled=false;manual.enabled=false;face.faceRenderer.updateWhenOffscreen=true;
            camera=new GameObject("Small Size Review Camera").AddComponent<Camera>();camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.29f,.34f,.42f,0);
            camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.orthographicSize=.71f;
            var light=new GameObject("Small Size Review Light").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(30,-25,0);light.intensity=1;
            RenderSettings.ambientLight=Color.white;RenderSettings.ambientIntensity=1;
            target=new RenderTexture(480,580,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;target.Create();
            pixels=new Texture2D(480,580,TextureFormat.RGBA32,false);
            var head=instance.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 Head");
            var eye=instance.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 Xtra_eyeL");
            var front=head.position-eye.position;front.y=0;front.Normalize();if(front.sqrMagnitude<.5f)front=Vector3.forward;
            var aim=new Vector3(0,.53f,0);camera.transform.position=aim+front*3+Vector3.up*.06f;camera.transform.LookAt(aim);
            actionIndex=frameIndex=phase=total=0;EditorApplication.update+=Step;EditorApplication.QueuePlayerLoopUpdate();
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(3);}
    }
    static void Step(){
        try{
            var entry=map.actions.First(e=>e.name==names[actionIndex]);count=Mathf.RoundToInt(entry.durationSeconds*24);
            if(phase==0){
                float t=frameIndex/(float)count;body.PlayAction(entry.name,0);
                animator.Play(entry.clip,0,t);animator.Play(entry.faceClip,1,t);animator.Update(0);face.Advance(0);
                phase=1;EditorApplication.QueuePlayerLoopUpdate();return;
            }
            if(phase<3){phase++;EditorApplication.QueuePlayerLoopUpdate();return;}
            camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,580),0,0);pixels.Apply();
            File.WriteAllBytes(root+"/Previews/ActionsV2/UnityMotion/"+entry.name+"_motion_"+frameIndex.ToString("000")+".png",pixels.EncodeToPNG());RenderTexture.active=null;
            total++;frameIndex++;phase=0;
            if(frameIndex>=count){Debug.Log("UNITY_SMALL_SIZE "+entry.name+" frames="+count);frameIndex=0;actionIndex++;}
            if(actionIndex>=names.Length){
                EditorApplication.update-=Step;
                File.WriteAllText(root+"/body_v2_revision7_unity_frames.json","{\"passed\":true,\"revision\":7,\"fps\":24,\"actions\":5,\"frames\":"+total+",\"unityVersion\":\""+Application.unityVersion+"\"}");
                Debug.Log("UNITY_SMALL_SIZE_COMPLETE frames="+total);EditorApplication.Exit(0);
            }else EditorApplication.QueuePlayerLoopUpdate();
        }catch(Exception e){EditorApplication.update-=Step;Debug.LogException(e);EditorApplication.Exit(3);}
    }
}
