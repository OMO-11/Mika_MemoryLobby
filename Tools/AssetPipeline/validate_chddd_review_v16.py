"""Verify preservation, clean switching and continuous closed-eye coverage."""
import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(bpy.data.filepath).parent
PREV=ROOT.parent/'V15_ChibiEyeReview'
scene=bpy.context.scene;body=bpy.data.objects['CH0069_Body'];cards=bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
checks={}
def curves(action):
    return [(c.data_path,c.array_index,[(tuple(p.co),p.interpolation) for p in c.keyframe_points]) for layer in action.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]
with bpy.data.libraries.load(str(PREV/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    names=tuple(n for n in src.actions if n.startswith(('PetV2_','Review9_TestSqueeze_')))
    dst.actions=list(names);dst.objects=['CH0069_Body','CHDDD_TestSqueeze_EyeLines']
checks['existing_body_face_squeeze_curves_exact']=all(curves(bpy.data.actions[n])==curves(a) for n,a in zip(names,dst.actions))
old=dst.objects[0].data;keys=body.data.shape_keys.key_blocks
checks['all_existing_shape_geometry_exact']=all(all((p.co-keys[k.name].data[i].co).length==0 for i,p in enumerate(k.data)) for k in old.shape_keys.key_blocks)
checks['body_topology_exact']=(len(old.vertices),len(old.polygons))==(len(body.data.vertices),len(body.data.polygons))
checks['squeeze_v15_cards_exact']=all(a.co==b.co for a,b in zip(cards.data.vertices,dst.objects[1].data.vertices))
image=next(n.image for n in cards.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE')
checks['squeeze_v15_image_retained']=image.name=='mika_squeeze_eyes_chibi_v15.png' and bool(image.packed_file)
mapping=json.loads((ROOT/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))
checks['all_18_action_entries_exact']=len(mapping['actions'])==18 and mapping['actions']==json.loads((PREV/'Blender/body_action_map_v2.json').read_text(encoding='utf-8'))['actions']
checks['face_profiles_exact']=(ROOT/'Blender/action_face_profiles_v2.json').read_bytes()==(PREV/'Blender/action_face_profiles_v2.json').read_bytes()
eyes=bpy.data.objects['CHDDD_TestOmega_Eyes'];v=eyes.data.vertices;half=len(v)//2
checks['omega_eyes_exact_mirror']=all((v[i].co-Vector((v[i+half].co.x,v[i+half].co.y,-v[i+half].co.z))).length<1e-7 for i in range(half))
checks['new_parts_head_bound']=all(len(o.vertex_groups)==1 and o.vertex_groups[0].name=='Bip001 Head' and any(m.type=='ARMATURE' and m.object.name=='Bip001' for m in o.modifiers) for o in (eyes,bpy.data.objects['CHDDD_TestOmega_Mouth']))
exec(compile((ROOT/'CHDDD_Controls.py').read_text(encoding='utf-8'),'static_controls','exec'))
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'body_controls','exec'))
def node_value(mat,name):return bpy.data.materials[mat].node_tree.nodes[name].outputs[0].default_value
def clear():return all(node_value(m,n)==0 for m,n in (('Review16_OmegaEyeLine','Review16_EyeOpacity'),('Review16_OmegaMouth','Review16_MouthOpacity'),('CHDDD_MouthAtlas','Review16_HideOriginalMouth')))
preview_review16_omega(scene)
checks['closed_lids_cover_eye_sockets']=True
for frame in range(1,74):
    scene.frame_set(frame)
    if node_value('Review16_OmegaEyeLine','Review16_EyeOpacity')>.001 and keys['Pet_Blink'].value<.99:checks['closed_lids_cover_eye_sockets']=False
    if keys['Pet_EyeWide'].value!=0:checks['closed_lids_cover_eye_sockets']=False
scene.frame_set(37)
checks['omega_peak_complete']=all(node_value(m,n)>.999 for m,n in (('Review16_OmegaEyeLine','Review16_EyeOpacity'),('Review16_OmegaMouth','Review16_MouthOpacity'),('CHDDD_MouthAtlas','Review16_HideOriginalMouth')))
checks['transition_endpoints_neutral']=True
for frame in (1,73):
    scene.frame_set(frame)
    if not clear() or keys['Pet_Blink'].value!=0:checks['transition_endpoints_neutral']=False
checks['all_actions_clear_omega']=True
for entry in mapping['actions']:
    preview_review16_omega(scene);scene.frame_set(37);preview_body_v2(scene,entry['name']);scene.frame_set(21)
    if not clear():checks['all_actions_clear_omega']=False
checks['standard_emotions_clear_omega']=True
manifest=json.loads((ROOT/'character_manifest.json').read_text(encoding='utf-8'))
for emotion in manifest['expressions']:
    preview_review16_omega(scene);scene.frame_set(37);apply_expression(scene,emotion)
    if not clear():checks['standard_emotions_clear_omega']=False
preview_review16_omega(scene);scene.frame_set(37);preview_review9_expression(scene);scene.frame_set(37)
checks['squeeze_test_clears_omega']=clear() and node_value('Review9_SqueezeEyeLine','Review9_EyeLineOpacity')>.99
preview_review16_omega(scene);scene.frame_set(37)
checks['omega_clears_squeeze']=node_value('Review9_SqueezeEyeLine','Review9_EyeLineOpacity')==0
checks['embedded_controls_match']=all(bpy.data.texts[n].as_string()==(ROOT/n).read_text(encoding='utf-8') for n in ('CHDDD_Controls.py','CHDDD_BodyControls_v2.py'))
result=dict(revision=16,checks=checks,passed=all(checks.values()),visualApproval='Pending user review',unityRevalidated=False)
(ROOT/'recovery_validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('V16_VALIDATION',json.dumps(result),flush=True)
if not result['passed']:raise RuntimeError('Omega experiment verification failed')
