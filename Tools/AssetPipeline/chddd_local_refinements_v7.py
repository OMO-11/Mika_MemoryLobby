"""Requested local acting changes; executed inside the established rig namespace."""
FLOW_ACTIONS.add('Wave')
PERFORMANCE_INTENTS_ZH.update({
 'Confused':'停下、后缩、歪头，掌心向内轻挠头两次，再缓慢收手恢复。',
 'Success':'保留原有蓄势与挺胸踮脚，双手向头顶举高，闭眼抬眉开心停留，再缓冲回落。',
 'Wave':'探身挥手，同时轻扇翅膀；最后一挥直接接上收手。',
 'HeadClick':'闭眼张三角嘴受惊，认出用户后皱眉、抿嘴佯装生气，再恢复平静。',
 'BodyClick':'保留后缩、收肩、双手近胸和回望，尾段翅膀轻轻抖动后收稳。'})
for t,v in STAGES['Success']:
    if t in (.30,.48):v.update(left=(.095,-.065,.94),right=(-.18,-.065,.94),shoulder=(22,22),palm=1,fist=.65,rightFist=.65)
for t,v in STAGES['Confused']:
    if t in (.40,.58):v.update(shoulder=(14,2),palm=.8)

def ease5_v7(t):
    t=max(0,min(1,t));return t**3*(10-15*t+6*t*t)
def window_v7(t,a,b,c,d):return ease5_v7((t-a)/(b-a))*ease5_v7((d-t)/(d-c))

def inward_palm_v7(strength=1):
    hand='Bip001 L Hand';w=world_head(hand)
    finger=world_head('Bip001 L Finger1')-w
    across=world_head('Bip001 L Finger2')-world_head('Bip001 L Finger0')
    normal=across.cross(finger)
    if normal.length<1e-8:return
    normal.normalize();axis=finger.normalized()
    desired=world_head('Bip001 Head')-w;desired-=axis*desired.dot(axis)
    normal-=axis*normal.dot(axis)
    if desired.length<1e-8 or normal.length<1e-8:return
    desired.normalize();normal.normalize()
    angle=math.atan2(axis.dot(normal.cross(desired)),normal.dot(desired))
    rotate(hand,axis,angle*strength)

def local_details_v7(name,poses):
    if name not in {'Confused','Success','Wave','BodyClick'}:return poses
    for i,p in enumerate(poses):
        t=i/(len(poses)-1);apply_pose(p)
        if name=='Confused':
            grip=sample_controls(name,t)['cheek'];inward_palm_v7(grip)
            scratch=window_v7(t,.38,.42,.60,.65)*math.sin(4*math.pi*(t-.40)/.20)
            rotate('Bip001 L Forearm',(0,1,0),.035*scratch)
            rotate('Bip001 L Hand',(0,0,1),.070*scratch);curl('L',.16*grip)
        if name=='Success':
            # Local shoulder-cape clearance, baked into existing bones.
            cape_names=[n for n in p if n.startswith(('bone_cape_l_','bone_cape_r_','bone_cape_bl','bone_cape_br'))]
            if i==0:shoulder_baseline=[]
            shoulder_baseline.append({n:[list(row) for row in p[n]] for n in cape_names})
            opening=window_v7(t,.10,.28,.55,.78)
            for side in ('L','R'):
                s=side.lower();sign=1 if side=='L' else -1
                rise=world_head('Bip001 '+side+' Hand').z-world_head('Bip001 '+side+' UpperArm').z
                w=ease5_v7((rise-.025)/.13)
                for suffix,angle in (('01',.38),('02',.20)):rotate('bone_cape_'+s+'_'+suffix,(0,1,0),-sign*(angle*w+.15*opening*(1 if suffix=='01' else .5)))
                shift('bone_cape_'+s+'_01',(sign*(.004*w+.006*opening),0,.004*w))
                rotate('bone_cape_b'+s,(0,1,0),-sign*.12*w)
        if name in ('Wave','BodyClick'):
            if name=='Wave':
                envelope=window_v7(t,.19,.28,.68,.84);cycle=4*math.pi*(t-.24)/.47;amp=.18
            else:
                envelope=window_v7(t,.68,.74,.86,.96);cycle=5*math.pi*(t-.70)/.24;amp=.045
            for side,sign in (('l',1),('r',-1)):
                for suffix,gain,delay in (('01',1,0),('02',.65,.28),('03',.32,.5),('04',.22,.68),('05',.32,.5)):
                    flap=amp*gain*envelope*math.sin(cycle-delay)
                    rotate('bone_wing_'+side+'_'+suffix,(0,1,0),-sign*flap)
        changed=([n for n in body_names if n in ('Bip001 L Forearm','Bip001 L Hand') or n.startswith('Bip001 L Finger')] if name=='Confused'
                 else [n for n in body_names if n.startswith('bone_cape')] if name=='Success'
                 else [n for n in body_names if n.startswith('bone_wing')])
        for n in changed:p[n]=arm.pose.bones[n].matrix_basis.copy()
        poses[i]=p
    if name=='Success':(ROOT/'body_v2_revision7_shoulder_uncorrected.json').write_text(json.dumps(shoulder_baseline),encoding='utf-8')
    return poses

def ensure_triangle_mouth_v7():
    mat=bpy.data.materials['CHDDD_MouthAtlas'];nodes=mat.node_tree.nodes;links=mat.node_tree.links
    if nodes.get('MouthWeight_200'):return
    atlas=next(n.image for n in nodes if n.type=='TEX_IMAGE')
    # Original seven cells stay in the same order; source cell 200 is slot 8.
    if mat.node_tree.animation_data:mat.node_tree.animation_data.action=None
    nodes.clear();uv=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(.5,.5,1);links.new(uv.outputs['UV'],scale.inputs[0])
    cs=al=None
    for index,tile in enumerate((300,706,705,604,605,305,401,200)):
        off=nodes.new('ShaderNodeVectorMath');off.operation='ADD';off.name='MouthTileOffset' if index==0 else 'MouthTileOffset_'+str(tile);off.inputs[1].default_value=(tile%100/8,tile//100/8,0);links.new(scale.outputs[0],off.inputs[0])
        tex=nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.name='MouthTile_'+str(tile);links.new(off.outputs[0],tex.inputs[0])
        w=nodes.new('ShaderNodeValue');w.name='MouthWeight_'+str(tile);w.outputs[0].default_value=1 if tile==300 else 0
        a=nodes.new('ShaderNodeMath');a.operation='MULTIPLY';links.new(tex.outputs['Alpha'],a.inputs[0]);links.new(w.outputs[0],a.inputs[1])
        c=nodes.new('ShaderNodeMixRGB');c.blend_type='MULTIPLY';c.inputs[0].default_value=1;links.new(tex.outputs['Color'],c.inputs[1]);links.new(a.outputs[0],c.inputs[2])
        if cs is None:cs=c.outputs[0];al=a.outputs[0]
        else:
            add=nodes.new('ShaderNodeMixRGB');add.blend_type='ADD';add.inputs[0].default_value=1;links.new(cs,add.inputs[1]);links.new(c.outputs[0],add.inputs[2]);cs=add.outputs[0]
            add=nodes.new('ShaderNodeMath');add.operation='ADD';links.new(al,add.inputs[0]);links.new(a.outputs[0],add.inputs[1]);al=add.outputs[0]
    e=nodes.new('ShaderNodeMath');e.operation='MAXIMUM';e.inputs[1].default_value=1e-6;links.new(al,e.inputs[0])
    c=nodes.new('ShaderNodeMixRGB');c.blend_type='DIVIDE';c.inputs[0].default_value=1;links.new(cs,c.inputs[1]);links.new(e.outputs[0],c.inputs[2])
    emission=nodes.new('ShaderNodeEmission');links.new(c.outputs[0],emission.inputs[0]);trans=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader');links.new(al,mix.inputs[0]);links.new(trans.outputs[0],mix.inputs[1]);links.new(emission.outputs[0],mix.inputs[2]);out=nodes.new('ShaderNodeOutputMaterial');links.new(mix.outputs[0],out.inputs[0])
    mat['mouth_blend_tiles']=json.dumps([300,706,705,604,605,305,401,200])

ensure_triangle_mouth_v7()
