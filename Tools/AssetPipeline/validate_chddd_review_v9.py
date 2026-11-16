"""Check prior review preservation, early blink, and expression transitions."""
import bpy,json,math
from pathlib import Path
ROOT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD/Reviews/V9_SecondReview')
PREV=ROOT.parent/'V8_FirstReview'
scene=bpy.context.scene;body=bpy.data.objects['CH0069_Body'];arm=bpy.data.objects['Bip001']
def curves(a):return [(c.data_path,c.array_index,[(tuple(p.co),p.interpolation) for p in c.keyframe_points]) for layer in a.layers for strip in layer.strips for bag in strip.channelbags for c in bag.fcurves]
with bpy.data.libraries.load(str(PREV/'CHDDD_actions_v2.blend'),link=False) as (src,dst):
    dst.actions=[a for a in src.actions if a.startswith('PetV2_')];dst.objects=['CH0069_Body']
checks={};measurements={}
body_actions=[a for a in dst.actions if a.name[:-4] in bpy.data.actions and not a.name.startswith(('PetV2_Face','PetV2_Shapes','PetV2_Mouth'))]
checks['every_body_clip_exact']=len(body_actions)==18 and all(curves(bpy.data.actions[a.name[:-4]])==curves(a) for a in body_actions)
checks['only_success_shape_timing_changed']=all(curves(bpy.data.actions[a.name[:-4]])==curves(a) for a in dst.actions if a.name[:-4] in bpy.data.actions and a.name[:-4]!='PetV2_Shapes_Success')
old=dst.objects[0].data;keys=body.data.shape_keys.key_blocks
checks['accepted_shape_data_exact']=all(all((v.co-keys[k.name].data[i].co).length==0 for i,v in enumerate(k.data)) for k in old.shape_keys.key_blocks)
checks['body_topology_exact']=len(old.vertices)==len(body.data.vertices) and len(old.polygons)==len(body.data.polygons)
exec(compile((ROOT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8'),'review9_controls','exec'))
preview_body_v2(scene,'Success');scene.frame_set(21)
measurements['success_blink_at_0_667_seconds']=keys['Pet_Blink'].value
checks['success_closed_while_rising']=keys['Pet_Blink'].value>.99
scene.frame_set(16);measurements['success_blink_at_lift_start']=keys['Pet_Blink'].value
face=bpy.data.materials['CHDDD_Face'].node_tree;lines=bpy.data.materials['Review9_SqueezeEyeLine'].node_tree
oldlash=bpy.data.materials['CHDDD_EyeMouth'].node_tree
iris=[v.index for v in body.data.vertices if any('Xtra_eye' in body.vertex_groups[g.group].name and 'blow' not in body.vertex_groups[g.group].name for g in v.groups)]
checks['test_uses_accepted_blink_not_failed_morph']=True
checks['test_keeps_skin_closed_while_chevrons_visible']=True
checks['test_keeps_iris_scale']=True
neutral=json.loads((ROOT/'character_manifest.json').read_text(encoding='utf-8'))['expressions']['neutral']['bone_pose']
preview_review9_expression(scene)
for f in range(1,74):
    scene.frame_set(f)
    sq=lines.nodes['Review9_EyeLineOpacity'].outputs[0].default_value
    if sq>0.001 and keys['Pet_Blink'].value<.99:checks['test_keeps_skin_closed_while_chevrons_visible']=False
    if abs(keys['Pet_TestSqueeze_Review'].value)>1e-8:checks['test_uses_accepted_blink_not_failed_morph']=False
    if any(abs(s-original)>1e-5 for n in ('Bip001 Xtra_eyeL','Bip001 Xtra_eyeR') for s,original in zip(arm.pose.bones[n].scale,neutral[n]['scale'])):checks['test_keeps_iris_scale']=False
    if f in (1,37,73):measurements['test_frame_'+str(f)]={'blink':keys['Pet_Blink'].value,'opacity':sq,'mask':face.nodes['Review9_OriginalEyeLineMask'].outputs[0].default_value}
preview_body_v2(scene,'HeadClick');scene.frame_set(13)
checks['normal_action_resets_test_materials']=face.nodes['Review9_OriginalEyeLineMask'].outputs[0].default_value==0 and lines.nodes['Review9_EyeLineOpacity'].outputs[0].default_value==0 and oldlash.nodes['Review9_OldEyeLineOpacity'].outputs[0].default_value==0
result={'reviewIteration':2,'revision':9,'checks':checks,'measurements':measurements,'passed':all(checks.values()),'visualApproval':'Pending user review','unityRevalidated':False}
(ROOT/'review_validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result),flush=True)
if not result['passed']:raise RuntimeError('Second review verification failed')
