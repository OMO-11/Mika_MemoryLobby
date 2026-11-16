using System.Collections.Generic;
using Spine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public sealed class CompanionPointerInteraction : MonoBehaviour {
    public CompanionBehaviorProfile profile;
    public Camera renderCamera;
    public CompanionLocalMotion motion;
    public CompanionExpressionOverlay expression;
    public bool Pressed { get; private set; }
    public bool Patting { get; private set; }
    public int StartedGestures { get; private set; }
    float held,distance,cooldown,dragThreshold,headWidth;
    Vector2 previous,pressOrigin;
    Skeleton cachedSkeleton;
    Bone cachedHead;
    sealed class Region {public Slot slot;public float[] vertices;}
    readonly List<Region> regions=new List<Region>();
    void CacheRegions() {
        var skeleton=motion.Character.Skeleton;if(cachedSkeleton==skeleton)return;
        cachedSkeleton=skeleton;cachedHead=skeleton.FindBone(profile.headBone);regions.Clear();
        foreach(string name in profile.headHitSlots) {
            var slot=skeleton.FindSlot(name);var mesh=slot==null?null:slot.Attachment as MeshAttachment;
            if(mesh!=null)regions.Add(new Region {slot=slot,vertices=new float[mesh.WorldVerticesLength]});
        }
    }
    bool ToLocal(Vector2 screen,out Vector3 local) {
        Ray ray=renderCamera.ScreenPointToRay(screen);float t;local=Vector3.zero;
        if(!new Plane(transform.forward,transform.position).Raycast(ray,out t))return false;
        local=transform.InverseTransformPoint(ray.GetPoint(t));return true;
    }
    static float Cross(float ax,float ay,float bx,float by){return ax*by-ay*bx;}
    static bool InTriangle(float x,float y,float[] v,int a,int b,int c) {
        a*=2;b*=2;c*=2;
        if(Mathf.Abs(Cross(v[b]-v[a],v[b+1]-v[a+1],v[c]-v[a],v[c+1]-v[a+1]))<.0000001f)return false;
        float ab=Cross(v[b]-v[a],v[b+1]-v[a+1],x-v[a],y-v[a+1]);
        float bc=Cross(v[c]-v[b],v[c+1]-v[b+1],x-v[b],y-v[b+1]);
        float ca=Cross(v[a]-v[c],v[a+1]-v[c+1],x-v[c],y-v[c+1]);
        return (ab>=-.00001f&&bc>=-.00001f&&ca>=-.00001f)||(ab<=.00001f&&bc<=.00001f&&ca<=.00001f);
    }
    MeshAttachment UpdateRegion(Region region) {
        var mesh=region.slot.Attachment as MeshAttachment;if(mesh==null||region.slot.A<=0)return null;
        if(region.vertices.Length!=mesh.WorldVerticesLength)region.vertices=new float[mesh.WorldVerticesLength];
        mesh.ComputeWorldVertices(region.slot,0,region.vertices.Length,region.vertices,0,2);return mesh;
    }
    public bool HitHead(Vector2 screen,float tolerance=1) {
        if(motion==null||motion.Character==null)return false;
        if(screen.x<0||screen.y<0||screen.x>=Screen.width||screen.y>=Screen.height)return false;
        Vector3 local;if(!ToLocal(screen,out local))return false;CacheRegions();
        foreach(var region in regions) {
            var mesh=UpdateRegion(region);if(mesh==null)continue;var v=region.vertices;
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            for(int i=0;i<v.Length;i+=2){minX=Mathf.Min(minX,v[i]);maxX=Mathf.Max(maxX,v[i]);minY=Mathf.Min(minY,v[i+1]);maxY=Mathf.Max(maxY,v[i+1]);}
            float cx=(minX+maxX)*.5f,cy=(minY+maxY)*.5f;
            float x=cx+(local.x-cx)/tolerance,y=cy+(local.y-cy)/tolerance;
            if(x<minX||x>maxX||y<minY||y>maxY)continue;
            var triangles=mesh.Triangles;
            for(int i=0;i<triangles.Length;i+=3)if(InTriangle(x,y,v,triangles[i],triangles[i+1],triangles[i+2]))return true;
        }
        if(regions.Count>0||cachedHead==null)return false;
        float hx,hy;cachedHead.WorldToLocal(local.x,local.y,out hx,out hy);
        hx=(hx-profile.headHitCenter.x)/(profile.headHitRadius.x*tolerance);hy=(hy-profile.headHitCenter.y)/(profile.headHitRadius.y*tolerance);
        return hx*hx+hy*hy<=1;
    }
    float HeadScreenWidth() {
        float minimum=float.MaxValue,maximum=float.MinValue;
        foreach(var region in regions) {
            if(UpdateRegion(region)==null)continue;
            for(int i=0;i<region.vertices.Length;i+=2) {
                var p=renderCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(region.vertices[i],region.vertices[i+1],0)));
                minimum=Mathf.Min(minimum,p.x);maximum=Mathf.Max(maximum,p.x);
            }
        }
        if(maximum>minimum)return maximum-minimum;
        return Screen.height*.45f;
    }
    void Update() {
        var mouse=Mouse.current;
        if(mouse==null||!Application.isFocused||(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())){Cancel();return;}
        Process(mouse.position.ReadValue(),mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed,mouse.leftButton.wasReleasedThisFrame,Time.unscaledDeltaTime);
    }
    public void Process(Vector2 position,bool down,bool heldButton,bool up,float dt) {
        cooldown=Mathf.Max(0,cooldown-dt);
        if(down&&cooldown<=0&&HitHead(position)) {
            Pressed=true;Patting=false;held=0;distance=0;previous=pressOrigin=position;StartedGestures++;
            headWidth=HeadScreenWidth();dragThreshold=Mathf.Max(5,headWidth*.04f);
        }
        if(!Pressed)return;
        held+=dt;distance+=(position-previous).magnitude;previous=position;
        if(!HitHead(position,1.12f)){Cancel();return;}
        if(heldButton&&!Patting&&held>=.25f&&distance>=dragThreshold)Patting=expression.BeginTouch();
        if(Patting)expression.SetTouchDirection((position-pressOrigin)/Mathf.Max(1,headWidth*.16f));
        if(up||!heldButton) {
            if(Patting)expression.EndTouch();
            else if(held<=.25f&&distance<dragThreshold)expression.BeginTouch(true);
            Pressed=false;Patting=false;cooldown=.25f;
        }
    }
    public void Cancel(){if(Patting)expression.EndTouch();Pressed=false;Patting=false;}
    void OnDisable(){Cancel();}
    void OnApplicationFocus(bool focused){if(!focused)Cancel();}
}
