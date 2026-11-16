using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Spine;
class Loader : TextureLoader {
 public void Load(AtlasPage page,string path){page.rendererObject=page.name;}
 public void Unload(object texture){}
}
class ProbeSpine {
 static void Main(string[] args){
  var atlas=new Atlas(new StringReader(File.ReadAllText(args[0])),"",new Loader());
  var reader=new SkeletonBinary(atlas);reader.Scale=.01f;
  SkeletonData data;using(var file=File.OpenRead(args[1]))data=reader.ReadSkeletonData(file);
  var skeleton=new Skeleton(data);var state=new AnimationState(new AnimationStateData(data));
  state.SetAnimation(0,args.Length>3?args[3]:"Idle_01",true);
  float duration=args.Length>4?float.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture):.5f;
  for(float time=0;time<duration;time+=1f/60){state.Update(1f/60);state.Apply(skeleton);skeleton.Update(1f/60);skeleton.UpdateWorldTransform(Skeleton.Physics.Update);}
  var rows=new List<object>();
  foreach(var slot in skeleton.DrawOrder){
   if(!slot.Bone.Active || slot.A==0)continue;
   var mesh=slot.Attachment as MeshAttachment;var region=slot.Attachment as RegionAttachment;
   if(mesh!=null){var v=new float[mesh.WorldVerticesLength];mesh.ComputeWorldVertices(slot,0,v.Length,v,0,2);var ar=(AtlasRegion)mesh.Region;
    rows.Add(new {slot=slot.Data.Name,blend=slot.Data.BlendMode.ToString(),attachment=mesh.Name,kind="mesh",vertices=v,uvs=mesh.UVs,regionUVs=mesh.RegionUVs,triangles=mesh.Triangles,page=ar.page.name,region=ar.name,degrees=ar.degrees,bounds=new[]{ar.x,ar.y,ar.width,ar.height},offsets=new[]{ar.offsetX,ar.offsetY,(float)ar.originalWidth,(float)ar.originalHeight},color=new[]{slot.R*mesh.R,slot.G*mesh.G,slot.B*mesh.B,slot.A*mesh.A}});
   }else if(region!=null){var v=new float[8];region.ComputeWorldVertices(slot,v,0,2);var ar=(AtlasRegion)region.Region;
    rows.Add(new {slot=slot.Data.Name,blend=slot.Data.BlendMode.ToString(),attachment=region.Name,kind="region",vertices=v,uvs=region.UVs,triangles=new[]{0,1,2,2,3,0},page=ar.page.name,region=ar.name,degrees=ar.degrees,bounds=new[]{ar.x,ar.y,ar.width,ar.height},offsets=new[]{ar.offsetX,ar.offsetY,(float)ar.originalWidth,(float)ar.originalHeight},color=new[]{slot.R*region.R,slot.G*region.G,slot.B*region.B,slot.A*region.A}});
   }
  }
  var json=new JavaScriptSerializer();json.MaxJsonLength=int.MaxValue;File.WriteAllText(args[2],json.Serialize(rows));Console.WriteLine(rows.Count+" draw slots");
 }
}
