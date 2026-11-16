"""Verify original-art provenance, preserved actions and test reset boundaries."""
import bpy,json,hashlib
from pathlib import Path
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V10_TextureReview')
PREV=ROOT.parent/'V9_SecondReview'
scene=bpy.context.scene;body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001']
def curves(a):return [(c.data_path,c.array_index,[(tuple(p.co),p.interpolation) for p in c.keyframe_points]) for layer in a.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]
checks={};measurements={}
with bpy.data.libraries.load(str(PREV/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    names=tuple(n for n in src.actions if n.startswith(('PetV2_','Review9_TestSqueeze_')));dst.actions=list(names);dst.objects=['CH0069_Body']
checks['all_body_face_and_test_curves_exact']=len(dst.actions)==len(names) and all(curves(bpy.data.actions[n])==curves(a) for n,a in zip(names,dst.actions))
old=dst.objects[0].data;keys=body.data.shape_keys.key_blocks
checks['all_existing_shape_keys_exact']=all(all((v.co-keys[k.name].data[i].co).length==0 for i,v in enumerate(k.data)) for k in old.shape_keys.key_blocks)
checks['body_topology_exact']=len(old.vertices)==len(body.data.vertices) and len(old.polygons)==len(body.data.polygons)
checks['face_profiles_exact']=(ROOT/'Unity/action_face_profiles_v2.json').read_bytes()==(PREV/'Unity/action_face_profiles_v2.json').read_bytes()
crop=json.loads((ROOT/'texture_crop_manifest.json').read_text(encoding='utf-8'));src=Path(crop['source'])
checks['original_texture_unchanged']=hashlib.sha256(src.read_bytes()).hexdigest()==crop['sourceSHA256']
line_mat=bpy.data.materials['Review9_SqueezeEyeLine'];texture=next(n.image for n in line_mat.node_tree.nodes if n.type=='TEX_IMAGE')
checks['test_uses_cropped_source_texture']=texture.name.startswith('mika_squeeze_eyes') and bool(texture.packed_file)
decal=bpy.data.objects['CHDDD_TestSqueeze_EyeLines']
checks['decal_skinned_to_head']=len(decal.vertex_groups)==1 and decal.vertex_groups[0].name=='Bip001 Head' and any(m.type=='ARMATURE' and m.object==arm for m in decal.modifiers)
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'review10_controls','exec'))
preview_review9_expression(scene)
checks['test_skin_closed_while_texture_visible']=True
for f in range(1,74):
    scene.frame_set(f);opacity=line_mat.node_tree.nodes['Review9_EyeLineOpacity'].outputs[0].default_value
    if opacity>.001 and keys['Pet_Blink'].value<.99:checks['test_skin_closed_while_texture_visible']=False
    if f in (1,37,73):measurements['frame_'+str(f)]={'opacity':opacity,'blink':keys['Pet_Blink'].value,'roundMouth':keys['Pet_TestRoundMouth_Review'].value}
preview_body_v2(scene,'Success');scene.frame_set(21)
checks['success_early_blink_preserved']=keys['Pet_Blink'].value>.99
checks['test_resets_on_standard_action']=line_mat.node_tree.nodes['Review9_EyeLineOpacity'].outputs[0].default_value==0 and keys['Pet_TestRoundMouth_Review'].value==0 and bpy.data.materials['CHDDD_Face'].node_tree.nodes['Review9_OriginalEyeLineMask'].outputs[0].default_value==0
result={'revision':10,'reviewIteration':3,'checks':checks,'measurements':measurements,'passed':all(checks.values()),'visualApproval':'Pending user review','unityRevalidated':False}
(ROOT/'review_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result),flush=True)
if not result['passed']:raise RuntimeError('Third texture review verification failed')
