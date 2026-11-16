using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>Separate editor frames prevent Camera.Render from caching the same skinned pose.</summary>
public static class CHDDDBodyV2Screenshots
{
    static readonly string[] names={"Dragging","Wave","Success","Confused","HeadClick","BodyClick"};
    static readonly string output="W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Previews/ActionsV2/Unity";
    static GameObject instance;static Animator animator;static CHDDDBodyControllerV2 body;
    static CHDDDActionFaceControllerV2 face;static Camera camera;static RenderTexture target;
    static Texture2D pixels;static Vector3 front;static int index,phase;
    public static void Run() {
        try {
            Directory.CreateDirectory(output);
            instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHDDD/Unity/CHDDD_Actions_v2.prefab"));
            animator=instance.GetComponent<Animator>();body=instance.GetComponent<CHDDDBodyControllerV2>();
            face=instance.GetComponent<CHDDDActionFaceControllerV2>();var manual=instance.GetComponent<CHDDDExpressionController>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            animator.Rebind();animator.Update(0);
            typeof(CHDDDExpressionController).GetMethod("Awake",flags).Invoke(manual,null);
            typeof(CHDDDActionFaceControllerV2).GetMethod("Awake",flags).Invoke(face,null);
            typeof(CHDDDBodyControllerV2).GetMethod("Awake",flags).Invoke(body,null);
            face.enabled=false;body.enabled=false;manual.enabled=false;face.faceRenderer.updateWhenOffscreen=true;
            camera=new GameObject("V2 Review Camera").AddComponent<Camera>();camera.orthographic=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.29f,.34f,.42f);
            camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.orthographicSize=.68f;
            var light=new GameObject("V2 Review Light").AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(30,-25,0);light.intensity=1;
            RenderSettings.ambientLight=Color.white;RenderSettings.ambientIntensity=1;
            target=new RenderTexture(480,580,24);camera.targetTexture=target;target.Create();
            pixels=new Texture2D(480,580,TextureFormat.RGB24,false);
            var head=instance.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 Head");
            var eye=instance.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 Xtra_eyeL");
            front=head.position-eye.position;front.y=0;front.Normalize();if(front.sqrMagnitude<.5f) front=Vector3.forward;
            index=0;phase=0;EditorApplication.update+=Step;EditorApplication.QueuePlayerLoopUpdate();
        } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(3); }
    }
    static void Step() {
        try {
            if(phase==0) {
                string name=names[index/5];float t=(index%5)/5f;
                body.PlayAction(name,0);animator.Play("PetV2_"+name,0,t);animator.Play("PetV2_FaceProcess_"+name,1,t);
                animator.Update(0);face.Advance(0);
                Vector3 aim=new Vector3(0,.49f,0);camera.transform.position=aim+front*3+Vector3.up*.05f;camera.transform.LookAt(aim);
                Debug.Log("V2 sample "+name+" "+t+" profile="+face.CurrentProfile+" time="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime+" blink="+face.faceRenderer.GetBlendShapeWeight(face.faceRenderer.sharedMesh.GetBlendShapeIndex("Pet_Blink")));
                phase=1;EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();return;
            }
            if(phase<3) { phase++;EditorApplication.QueuePlayerLoopUpdate();return; }
            camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,580),0,0);pixels.Apply();
            File.WriteAllBytes(output+"/"+names[index/5]+"_"+(index%5)+".png",pixels.EncodeToPNG());RenderTexture.active=null;
            index++;phase=0;
            if(index>=names.Length*5) { EditorApplication.update-=Step;Debug.Log("V2 Unity screenshot review complete");EditorApplication.Exit(0); }
            else EditorApplication.QueuePlayerLoopUpdate();
        } catch(Exception e) { EditorApplication.update-=Step;Debug.LogException(e);EditorApplication.Exit(3); }
    }
}
