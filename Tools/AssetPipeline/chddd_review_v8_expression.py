""">o< first test: accepted closed skin, independent stylized eyelash shape."""
keys=body.data.shape_keys.key_blocks;base=keys['Basis'];blink=keys['Pet_Blink']
squeeze=keys.get('Pet_TestSqueeze_Review') or body.shape_key_add(name='Pet_TestSqueeze_Review',from_mix=False)
# The visible lashes are islands in the face atlas, NOT EyeMouth material.
# UV~(.967,.039) is the closed skin cover and must remain untouched.
lash=set()
for p in body.data.polygons:
    if p.material_index!=1:continue
    for li in p.loop_indices:
        uv=body.data.uv_layers.active.data[li].uv
        if uv.y>.70 or .10<uv.y<.20:lash.add(body.data.loops[li].vertex_index)
for i in range(len(base.data)):squeeze.data[i].co=base.data[i].co
upper=[(.0256,-.3901),(.0352,-.3891),(.0366,-.3962),(.0450,-.3995),(.0588,-.4009),(.0739,-.3988),(.0839,-.3952),(.0939,-.3885),(.0974,-.3843)]
lower=[(.0256,-.3901),(.0352,-.3774),(.0376,-.3662),(.0410,-.3615),(.0450,-.3594),(.0582,-.3576),(.0715,-.3560),(.0788,-.3580),(.0851,-.3621),(.0900,-.3672),(.0944,-.3744),(.0974,-.3843)]
changed=[];iris=[]
for v in body.data.vertices:
    groups=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups]
    if any('Xtra_eye' in n and 'blow' not in n for n,w in groups):iris.append(v.index);continue
    if v.index not in lash:continue
    up=sum(w for n,w in groups if n.startswith('Bip001 eye_'))
    down=sum(w for n,w in groups if n.startswith('Bip001 bone_eye_D_'))
    if max(up,down)<.18:continue
    co=base.data[v.index].co;s=abs(co.z);u=max(0,min(1,(s-.030)/.067))
    top=float(np.interp(s,[x[0] for x in upper],[x[1] for x in upper]));bottom=float(np.interp(s,[x[0] for x in lower],[x[1] for x in lower]))
    is_upper=up>=.18 and (down<.18 or co.y<(top+bottom)/2)
    edge=top if is_upper else bottom
    y=-.377+(-.022 if is_upper else .020)*u+(co.y-edge)*.55
    # Additive over Blink. Skin and eyeballs keep the accepted closed-eye shape.
    target=Vector((co.x+.0015,y,co.z))
    squeeze.data[v.index].co=co+target-blink.data[v.index].co
    changed.append(v.index)

count=73
def test_values(t):
    close=ease5_v7((t-.10)/.15)*ease5_v7((.96-t)/.14)
    squeeze_w=ease5_v7((t-.25)/.15)*ease5_v7((.80-t)/.13)
    mouth_w=ease5_v7((t-.18)/.18)*ease5_v7((.86-t)/.15)
    return close,squeeze_w,mouth_w
shape_action,bag=new_action('Review8_TestSqueeze_Shapes',body.data.shape_keys)
for key in keys:
    if key.name=='Basis':continue
    values=[]
    for i in range(count):
        blink_w,sq,mouth_w=test_values(i/(count-1))
        values.append(blink_w if key.name=='Pet_Blink' else sq if key.name=='Pet_TestSqueeze_Review' else .30*sq if key.name=='Pet_BrowHappy_Review' else 0)
    curve(bag,key.path_from_id('value'),0,values,key.name)
tree=bpy.data.materials['CHDDD_MouthAtlas'].node_tree;ma,bag=new_action('Review8_TestSqueeze_Mouth',tree)
for tile in MOUTH:
    values=[(test_values(i/(count-1))[2] if tile==605 else 1-test_values(i/(count-1))[2] if tile==300 else 0) for i in range(count)]
    curve(bag,tree.nodes['MouthWeight_'+str(tile)].outputs[0].path_from_id('default_value'),0,values,'Mouth')
config={'name':'TestSqueeze','label':'>o<','status':'First draft, awaiting user review','durationSeconds':2.4,'frames':[1,73],'shapeAction':shape_action.name,'mouthAction':ma.name,'eyelashVertices':len(changed),'irisVerticesUnchanged':len(iris),'skinClosure':'Existing Pet_Blink; new squeeze affects only separate eyeliner surfaces','blendOrder':'Close eyelids, form chevrons, hold, remove chevrons, reopen'}
(DRAFT/'expression_test_v1.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')

# Preview operators must clear experimental values before every standard clip.
controls=(DRAFT/'CHDDD_BodyControls_v2.py').read_text(encoding='utf-8')
controls=controls.replace("keys=body.data.shape_keys;keys.animation_data_create();", "keys=body.data.shape_keys\n    for key in keys.key_blocks:key.value=0\n    keys.animation_data_create();")
controls+='''
def preview_review8_expression(scene,play=False):
    entry=preview_body_v2(scene,'Idle')
    body=bpy.data.objects['CH0069_Body'];tree=bpy.data.materials['CHDDD_MouthAtlas'].node_tree
    body.data.shape_keys.animation_data.action=bpy.data.actions['Review8_TestSqueeze_Shapes']
    tree.animation_data.action=bpy.data.actions['Review8_TestSqueeze_Mouth']
    scene.frame_start=1;scene.frame_end=73;scene.frame_set(1);scene['body_v2_action']='TestSqueeze'
    if play and bpy.context.screen:
        scene['transition_preview_stop_frame']=73;bpy.ops.screen.animation_play()
    return {'name':'TestSqueeze','durationSeconds':2.4,'frames':[1,73],'loop':False}
class CHDDD_OT_Review8Expression(bpy.types.Operator):
    bl_idname='chddd.review8_expression';bl_label='Test >o< first draft'
    def execute(self,context):
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
        preview_review8_expression(context.scene,True);return {'FINISHED'}
old=getattr(bpy.types,CHDDD_OT_Review8Expression.__name__,None)
if old:bpy.utils.unregister_class(old)
bpy.utils.register_class(CHDDD_OT_Review8Expression)
'''
controls=controls.replace("self.layout.label(text='身体 · 发衣跟随 · 独立表情过程')", "self.layout.label(text='V8 第一版 · 待人工审查')\n        self.layout.operator('chddd.review8_expression',text='>o< 第一版测试',icon='PLAY')")
(DRAFT/'CHDDD_BodyControls_v2.py').write_text(controls,encoding='utf-8')
