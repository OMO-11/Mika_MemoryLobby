import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Matrix
OUT=Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
exec(compile((OUT/'CHDDD_BodyControls_v1.py').read_text(encoding='utf-8'),'body_controls','exec'))
scene=bpy.context.scene;arm=bpy.data.objects['Bip001']; body=bpy.data.objects['CH0069_Body']
mapping=json.loads((OUT/'Unity/body_action_map_v1.json').read_text())
original=json.loads((OUT/'character_manifest_before_actions_v1.json').read_text(encoding='utf-8'))
current=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
facial=[n for n in original['faceBones'] if n!='Bip001 Head']
entries=[];poses={}; errors=[]
for entry in mapping['actions']:
    preview_body(scene,entry['name'],play=False)
    action=bpy.data.actions[entry['clip']]
    curves=[c for l in action.layers for s in l.strips for b in s.channelbags for c in b.fcurves]
    no_face=all(not any('"'+n+'"' in c.data_path for n in facial) for c in curves)
    start,end=entry['frames']; collected={}; feet=[]; geometry_finite=True
    for f in [start,start+1,(start+end)//2,end-1,end]:
        scene.frame_set(f);bpy.context.view_layer.update()
        collected[f]={b.name:b.matrix_basis.copy() for b in arm.pose.bones if b.name not in facial}
        feet.append([list(arm.matrix_world@arm.pose.bones['Bip001 '+side+' Foot'].head) for side in ('L','R')])
        evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        geometry_finite &= all(math.isfinite(v) for vertex in evaluated.data.vertices for v in vertex.co)
    first,last=collected[start],collected[end]
    endpoint=max(abs(first[n][i][j]-last[n][i][j]) for n in first for i in range(4) for j in range(4))
    # Quaternion antipodes are not matrix differences: check actual sampled pose matrices.
    velocity_error=max(abs((collected[start+1][n][i][j]-first[n][i][j])-(last[n][i][j]-collected[end-1][n][i][j])) for n in first for i in range(4) for j in range(4))
    angular_velocity_error=0;linear_velocity_error=0
    for n in first:
        q0,q1,qp,qe=[p[n].to_quaternion() for p in (first,collected[start+1],collected[end-1],last)]
        da=q0.rotation_difference(q1);db=qp.rotation_difference(qe)
        if da.w<0:da.negate()
        if db.w<0:db.negate()
        angular_velocity_error=max(angular_velocity_error,(da.axis*da.angle-db.axis*db.angle).length)
        va=collected[start+1][n].translation-first[n].translation
        vb=last[n].translation-collected[end-1][n].translation
        linear_velocity_error=max(linear_velocity_error,(va-vb).length)
    foot_range=max(max(p[side][axis] for p in feet)-min(p[side][axis] for p in feet) for side in range(2) for axis in range(3))
    okay=geometry_finite and no_face and (not entry['loop'] or (endpoint<2e-5 and angular_velocity_error<.015 and linear_velocity_error<.001))
    if entry['name'] in ('Thinking','Working','HeadPat','Sleep'): okay &= foot_range<1e-5
    entries.append({'name':entry['name'],'loop':entry['loop'],'endpointMatrixError':endpoint,
                    'sampledVelocityDifferencePerFrame':velocity_error,'sampledFootRangeModelUnits':foot_range,
                    'angularVelocityDifferenceRadiansPerFrame':angular_velocity_error,'linearVelocityDifferenceModelUnitsPerFrame':linear_velocity_error,
                    'noFacialBoneCurves':no_face,'geometryFinite':geometry_finite,'passed':okay})
    poses[entry['name']]=(first,last)
    if not okay: errors.append(entry['name'])

connections=[]
for name in ('HeadPat','Dragging','Sleep'):
    for a,ai,b,bi in [('Idle',0,name+'_Enter',0),(name+'_Enter',1,name,0),(name,1,name+'_Exit',0),(name+'_Exit',1,'Idle',0)]:
        pa,pb=poses[a][ai],poses[b][bi]
        difference=max(abs(pa[n][i][j]-pb[n][i][j]) for n in pa for i in range(4) for j in range(4))
        connections.append({'from':a,'to':b,'matrixError':difference,'passed':difference<2e-5})
for name in ('Wave','Success','Confused','HeadClick','BodyClick'):
    for ai in (0,1):
        pa,pb=poses[name][ai],poses['Idle'][0]
        difference=max(abs(pa[n][i][j]-pb[n][i][j]) for n in pa for i in range(4) for j in range(4))
        connections.append({'from':name,'endpoint':ai,'to':'Idle','matrixError':difference,'passed':difference<2e-5})

preserved=all(current.get(k)==original.get(k) for k in ('expressions','faceBones','blendShapes','transitions','mouthAtlas'))
original_file_unchanged=hashlib.sha256((OUT/'CHDDD.blend').read_bytes()).digest()==hashlib.sha256((OUT/'CHDDD_before_actions_v1.blend').read_bytes()).digest()
source_hash=hashlib.sha256((OUT.parent/'CH0069/Model/CH0069.glb').read_bytes()).hexdigest()
halo=bpy.data.objects['HaloRoot']
report={'bodyClips':len(entries),'loops':sum(e['loop'] for e in entries),'bodyActions':entries,'connections':connections,
        'expressionsPreserved':preserved,'acceptedBlendUnchanged':original_file_unchanged,
        'sourceUnchanged':source_hash=='57816e04521d12a9615cb5081fc7db90bb7fe0ae8f36d3cec3ed04dfad9b67a0',
        'weaponHidden':all(o.hide_render for o in scene.objects if o.type=='MESH' and o.name.startswith('CH0069_Weapon')),
        'haloAttachedToHead':halo.parent==arm and halo.parent_bone=='Bip001 Head','haloRaiseModelUnits':halo.get('body_v1_height_adjustment'),
        'visualReview':'First draft: still and motion preview review; future artistic refinement remains expected',
        'velocityNote':'Sampled finite differences are diagnostic, not a proof of continuous derivatives. Source clips retain source motion; custom standing loops use sinusoidal motion.'}
report['passed']=not errors and all(c['passed'] for c in connections) and all(report[k] for k in ('expressionsPreserved','acceptedBlendUnchanged','sourceUnchanged','weaponHidden','haloAttachedToHead'))
(OUT/'body_v1_blender_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
preview_body(scene,'Idle',play=False)
print(json.dumps({'passed':report['passed'],'errors':errors,'failedConnections':[c for c in connections if not c['passed']],'actions':entries}))
assert report['passed']
