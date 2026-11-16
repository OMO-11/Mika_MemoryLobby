"""Replace iris compression with deformation of the existing skin rim and lashes."""
import bpy,json,pathlib
from mathutils import Vector
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
bpy.ops.chddd.expression(emotion='neutral')
body=bpy.data.objects['CH0069_Body']; keys=body.data.shape_keys.key_blocks
base=keys['Basis']; blink=keys['Pet_Blink']; wide=keys['Pet_EyeWide']
for i,v in enumerate(base.data):
    blink.data[i].co=v.co;wide.data[i].co=v.co
upper=[(.0256,-.3901),(.0352,-.3891),(.0366,-.3962),(.0450,-.3995),(.0588,-.4009),(.0739,-.3988),(.0839,-.3952),(.0939,-.3885),(.0974,-.3843),(.1108,-.3967)]
lower=[(.0256,-.3901),(.0352,-.3774),(.0376,-.3662),(.0410,-.3615),(.0450,-.3594),(.0582,-.3576),(.0715,-.3560),(.0788,-.3580),(.0851,-.3621),(.0900,-.3672),(.0944,-.3744),(.0974,-.3843),(.1108,-.3967)]
def interp(samples,x):
    if x<=samples[0][0]:return samples[0][1]
    for (a,u),(b,v) in zip(samples,samples[1:]):
        if x<=b:return u+(v-u)*(x-a)/(b-a)
    return samples[-1][1]
changed=[];iris=[]
for v in body.data.vertices:
    groups=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups]
    if any('Xtra_eye' in n and 'blow' not in n for n,w in groups):iris.append(v.index);continue
    upper_weight=sum(w for n,w in groups if n.startswith('Bip001 eye_'))
    lower_weight=sum(w for n,w in groups if n.startswith('Bip001 bone_eye_D_'))
    if max(upper_weight,lower_weight)<.18:continue
    co=base.data[v.index].co;s=abs(co.z)
    if co.x<.015 or not .023<s<.119:continue
    top=interp(upper,s);bottom=interp(lower,s)
    delta=max(0,bottom-top)
    # Move the whole upper rim, skin duplicates and lash strips together.
    # The sclera/iris stay at their original dimensions behind this skin.
    # Flatten the lash arch as it closes, while allowing the skin edge to overlap
    # the lower rim. Keeping the open-eye lash height makes sharp U-shaped ends.
    if upper_weight>=.18 and co.y<(top+bottom)/2:
        offset=co.y-top
        flatten=-min(0,offset)*.65
        movement=delta*.78+flatten
    elif lower_weight>=.18:
        movement=-delta*.25
    else:continue
    blink.data[v.index].co=co+Vector((0,movement,0))
    changed.append(v.index)
for name in ['Pet_BrowSad','Pet_BrowAnnoyed','Pet_BrowSleepy']:
    key=keys.get(name) or body.shape_key_add(name=name)
    for i,v in enumerate(base.data):key.data[i].co=v.co
for v in body.data.vertices:
    if not any('eyeblow' in body.vertex_groups[g.group].name for g in v.groups):continue
    co=base.data[v.index].co;t=max(0,min(1,(abs(co.z)-.0248)/.0747))
    # Inner brow rises for sadness; inner brow falls for annoyance.
    keys['Pet_BrowSad'].data[v.index].co=co+Vector((.001,-(.022*(1-t)-.025*t),0))
    keys['Pet_BrowAnnoyed'].data[v.index].co=co+Vector((.001,.030*(1-t)-.012*t,0))
    keys['Pet_BrowSleepy'].data[v.index].co=co+Vector((.001,.007+.004*(1-t),0))
manifest=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
for emotion,s in manifest['expressions'].items():
    s['blendShapes'].update(Pet_BrowSad=1 if emotion=='sad' else .55 if emotion=='concern' else 0,
                           Pet_BrowAnnoyed=1 if emotion=='annoyed' else 0,
                           Pet_BrowSleepy=1 if emotion=='sleepy' else 0)
    s['blendShapes']['Pet_EyeWide']=0
    if emotion=='sad':s['blendShapes']['Pet_Blink']=.18
    if emotion=='annoyed':s['blendShapes']['Pet_Blink']=.42
    if emotion=='sleepy':s['blendShapes']['Pet_Blink']=1
manifest['blendShapes']=list(keys.keys())[1:]
manifest['eyelidFix']={'method':'Upper/lower skin rim and lash deformation; full-size iris retained',
                      'irisVerticesUnchanged':len(iris),'eyelidVertices':len(changed),
                      'browShapes':['Pet_BrowSad','Pet_BrowAnnoyed','Pet_BrowSleepy']}
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.ops.chddd.expression(emotion='sleepy')
print(json.dumps(manifest['eyelidFix']))
