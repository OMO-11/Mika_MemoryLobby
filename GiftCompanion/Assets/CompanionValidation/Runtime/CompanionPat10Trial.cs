using System;
using System.Collections.Generic;
using System.IO;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>Reversible local proof of the original 10.png. Source pixels and SkeletonData stay intact.</summary>
public sealed class CompanionPat10Trial : MonoBehaviour {
    public bool UseForTouch = true;
    public bool Ready { get; private set; }
    public int VisibleParts { get; private set; }
    public Color tint = new Color(.80f,.86f,1f,1f);
    public float mouthSize = .8f;
    public float eyeSize = 1.9f;
    // The supporting-hand side needs its own correction in the reclining face perspective.
    public float handSideEyeScale = 1.55f;
    struct Hidden {public Slot slot;public float alpha,r,g,b;}
    sealed class Part {public Slot slot;public MeshAttachment mesh;public bool mouth;}
    readonly List<Hidden> hidden=new List<Hidden>();
    readonly List<Part> parts=new List<Part>();
    SkeletonAnimation character,attached;
    Skeleton drawnSkeleton;
    CompanionExpressionOverlay expression;
    Texture2D texture;
    Material material;
    AtlasPage page;
    float lastClock,mouthBlend=1;
    public void Bind(SkeletonAnimation value,CompanionExpressionOverlay facial) {
        Detach();attached=value;character=value;expression=facial;expression.Pat10Trial=this;
        try {
            string path=Path.Combine(Application.streamingAssetsPath,"ExpressionTrials/10.png");
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false) {name="Pat10 original pixels",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            if(!texture.LoadImage(File.ReadAllBytes(path)))throw new InvalidDataException("10.png failed to decode.");
            material=new Material(character.GetComponent<MeshRenderer>().sharedMaterial) {name="Pat10 trial",mainTexture=texture};
            material.SetFloat("_StraightAlphaInput",1);material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            page=new AtlasPage {name="10.png",width=texture.width,height=texture.height,rendererObject=material};
            Calibrate();lastClock=expression.Clock;mouthBlend=1;
            character.UpdateComplete+=Apply;character.OnAnimationRebuild+=Rebuild;Ready=true;
        } catch(Exception error) {
            Debug.LogWarning("Pat10 trial unavailable; original expressions retained. "+error.Message);
            Detach();
        }
    }
    static Vector2 Center(Skeleton skeleton,string name,out Vector2[] points) {
        var slot=skeleton.FindSlot(name);
        var mesh=skeleton.GetAttachment(slot.Data.Index,name) as MeshAttachment;
        var vertices=new float[mesh.WorldVerticesLength];
        mesh.ComputeWorldVertices(slot,0,vertices.Length,vertices,0,2);
        points=new Vector2[vertices.Length/2];var sum=Vector2.zero;
        for(int i=0;i<points.Length;i++){points[i]=new Vector2(vertices[i*2],vertices[i*2+1]);sum+=points[i];}
        return sum/points.Length;
    }
    static float Span(Vector2[] points,Vector2 axis) {
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        foreach(var point in points){float v=Vector2.Dot(point,axis);min=Mathf.Min(min,v);max=Mathf.Max(max,v);}
        return max-min;
    }
    void Calibrate() {
        var reference=new Skeleton(character.Skeleton.Data);
        reference.Data.FindAnimation(expression.profile.idleAnimation).Apply(reference,-1,0,true,null,1,MixBlend.Replace,MixDirection.In);
        reference.UpdateWorldTransform(Skeleton.Physics.Pose);
        Vector2[] leftVertices,rightVertices,mouthVertices;
        // Image left corresponds to the character's R eye in this reclining composition.
        var left=Center(reference,"R_Eye_idle_01",out leftVertices);
        var right=Center(reference,"L_Eye_idle_01",out rightVertices);
        var mouth=Center(reference,"Mouse_normal_01",out mouthVertices);
        var u=(right-left).normalized;var down=new Vector2(-u.y,u.x);
        if(Vector2.Dot(mouth-(left+right)*.5f,down)<0)down=-down;
        var sourceU=new Vector2(73,30).normalized;var sourceDown=new Vector2(-sourceU.y,sourceU.x);
        float scale=Vector2.Distance(left,right)/new Vector2(73,30).magnitude;
        var rootReference=reference.FindBone(expression.profile.headBone);
        var root=character.Skeleton.FindBone(expression.profile.headBone);
        Build("left_eye",new Rect(0,32,50,31),new Vector2(25,48),left,
            Span(leftVertices,u)/49f*eyeSize,u,down,sourceU,sourceDown,rootReference,root,false);
        // x=81 excludes the isolated nose highlight; no nose image is overlaid.
        Build("right_eye",new Rect(81,59,38,37),new Vector2(98,78),right,
            Span(rightVertices,u)/43f*eyeSize*handSideEyeScale,u,down,sourceU,sourceDown,rootReference,root,false);
        Build("left_brow",new Rect(14,0,60,14),new Vector2(25,48),left,
            scale,u,down,sourceU,sourceDown,rootReference,root,false);
        Build("right_brow",new Rect(97,17,31,33),new Vector2(98,78),right,
            scale,u,down,sourceU,sourceDown,rootReference,root,false);
        Build("mouth",new Rect(29,87,42,29),new Vector2(50,102),mouth,
            scale*mouthSize,u,down,sourceU,sourceDown,rootReference,root,true);
    }
    void Build(string name,Rect rectangle,Vector2 sourceCenter,Vector2 targetCenter,float scale,
        Vector2 u,Vector2 down,Vector2 sourceU,Vector2 sourceDown,Bone reference,Bone root,bool isMouth) {
        var region=new AtlasRegion {name="Pat10/"+name,page=page,width=(int)rectangle.width,height=(int)rectangle.height,
            originalWidth=(int)rectangle.width,originalHeight=(int)rectangle.height,x=(int)rectangle.x,y=(int)rectangle.y,
            u=rectangle.x/texture.width,u2=rectangle.xMax/texture.width,
            v=1-rectangle.y/texture.height,v2=1-rectangle.yMax/texture.height};
        var mesh=new MeshAttachment("Pat10/"+name) {Region=region,WorldVerticesLength=8,Vertices=new float[8],
            RegionUVs=new[]{0f,0f,1f,0f,1f,1f,0f,1f},Triangles=new[]{0,1,2,2,3,0},HullLength=8};
        var corners=new[]{new Vector2(rectangle.x,rectangle.y),new Vector2(rectangle.xMax,rectangle.y),
            new Vector2(rectangle.xMax,rectangle.yMax),new Vector2(rectangle.x,rectangle.yMax)};
        for(int i=0;i<4;i++) {
            Vector2 delta=corners[i]-sourceCenter;
            Vector2 world=targetCenter+scale*(u*Vector2.Dot(delta,sourceU)+down*Vector2.Dot(delta,sourceDown));
            float x,y;reference.WorldToLocal(world.x,world.y,out x,out y);mesh.Vertices[i*2]=x;mesh.Vertices[i*2+1]=y;
        }
        mesh.UpdateRegion();
        var slot=new Slot(new SlotData(0,"Pat10/"+name,root.Data),root) {Attachment=mesh};
        parts.Add(new Part {slot=slot,mesh=mesh,mouth=isMouth});
    }
    public void Restore() {
        foreach(var entry in hidden){entry.slot.A=entry.alpha;entry.slot.R=entry.r;entry.slot.G=entry.g;entry.slot.B=entry.b;}
        hidden.Clear();
        if(drawnSkeleton!=null)foreach(var part in parts)drawnSkeleton.DrawOrder.Remove(part.slot);
        drawnSkeleton=null;VisibleParts=0;
    }
    void Apply(ISkeletonAnimation _) {
        // The main facial writer restores these changes before its own source snapshot.
        Restore();if(!Ready||expression==null)return;
        float visibility=expression.Pat10Visibility;
        float dt=Mathf.Max(0,expression.Clock-lastClock);lastClock=expression.Clock;
        mouthBlend=Mathf.Lerp(mouthBlend,expression.SpeechActive?0:1,1-Mathf.Exp(-dt/.065f));
        if(visibility<=.001f)return;
        drawnSkeleton=character.Skeleton;
        var order=drawnSkeleton.DrawOrder;
        float mouthAlpha=visibility*mouthBlend;
        foreach(var slot in order) {
            string name=slot.Data.Name;
            bool eye=name.IndexOf("_Eye_",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("_Eyebrow",StringComparison.OrdinalIgnoreCase)>=0;
            bool mouth=name==expression.profile.mouthSlot;
            bool fill=name.IndexOf("_Eyelid_01_fill",StringComparison.OrdinalIgnoreCase)>=0;
            if(!eye&&!mouth&&!fill)continue;
            hidden.Add(new Hidden {slot=slot,alpha=slot.A,r=slot.R,g=slot.G,b=slot.B});
            if(fill){slot.G*=Mathf.Lerp(1,.96f,visibility);slot.B*=Mathf.Lerp(1,.98f,visibility);continue;}
            slot.A*=1-(eye?visibility:mouthAlpha);
        }
        // Before foreground fringe; source eyelid skin fills remain beneath the face cutouts.
        int insertion=order.IndexOf(drawnSkeleton.FindSlot("F_Hair_02"));
        if(insertion<0)insertion=order.IndexOf(drawnSkeleton.FindSlot(expression.profile.mouthSlot))+1;
        foreach(var part in parts) {
            float alpha=part.mouth?mouthAlpha:visibility;if(alpha<=.001f)continue;
            part.slot.A=alpha;part.mesh.R=tint.r;part.mesh.G=tint.g;part.mesh.B=tint.b;
            order.Insert(insertion++,part.slot);VisibleParts++;
        }
    }
    public void Recalibrate(){Restore();parts.Clear();if(Ready)Calibrate();}
    void Rebuild(ISkeletonAnimation _){if(attached!=null&&expression!=null)Bind(attached,expression);}
    void OnEnable(){if(attached!=null&&!Ready&&expression!=null)Bind(attached,expression);}
    void OnDisable(){Detach();}
    void Detach() {
        Restore();Ready=false;
        if(character!=null){character.UpdateComplete-=Apply;character.OnAnimationRebuild-=Rebuild;}
        parts.Clear();character=null;
        if(material!=null)Destroy(material);if(texture!=null)Destroy(texture);
        material=null;texture=null;page=null;
    }
}
