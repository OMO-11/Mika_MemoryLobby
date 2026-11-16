"""Injected into the rig namespace before the first and only review bake."""
for t,v in STAGES['Success']:
    if t in (.30,.48):
        # Wide diagonal upper arms produce a Y silhouette, preserving torso/feet.
        v.update(left=(.245,-.045,.865),right=(-.325,-.045,.865),shoulder=(16,16),palm=1,fist=.20,rightFist=.20)
for t,v in STAGES['HeadClick']:
    if t in (.18,.30):
        v.update(left=(.065,.025,.795),right=(-.160,.015,.795),shoulder=(16,16),palm=.9,fist=.08,rightFist=.08)
    if t in (.55,.70):
        v.update(left=(.17,-.065,.43),right=(-.11,-.16,.43),leftWeight=.8,rightWeight=.8,palm=0)
PERFORMANCE_INTENTS_ZH['Success']='原有挺胸与踮脚不变，双臂向斜上方展开成Y形，闭眼、上弧笑眉，缓冲回落。'
PERFORMANCE_INTENTS_ZH['HeadClick']='闭眼受惊，双手捂住头顶两侧，放下双手后抿嘴、皱眉佯装生气，再恢复。'

# A dedicated upward arch, separate from all accepted eyebrow shape keys.
import numpy as np
keys=body.data.shape_keys.key_blocks;base=keys['Basis']
happy=keys.get('Pet_BrowHappy_Review') or body.shape_key_add(name='Pet_BrowHappy_Review',from_mix=False)
brows=[v.index for v in body.data.vertices if any('eyeblow' in body.vertex_groups[g.group].name and g.weight>.05 for g in v.groups)]
fit=np.polyfit([abs(base.data[i].co.z) for i in brows],[base.data[i].co.y for i in brows],3)
for i in brows:
    co=base.data[i].co;s=abs(co.z);u=max(0,min(1,(s-.0248)/.0747))
    center=-.447+.012*(2*u-1)**2
    happy.data[i].co=Vector((co.x,center+co.y-float(np.polyval(fit,s)),co.z))

previous_details_review8=local_details_v7
def local_details_v7(name,poses):
    poses=previous_details_review8(name,poses)
    if name!='HeadClick':return poses
    for i,p in enumerate(poses):
        t=i/(len(poses)-1);apply_pose(p)
        w=window_v7(t,0,.18,.30,.55)
        for side,sign in (('L',1),('R',-1)):
            hand='Bip001 '+side+' Hand';wrist=world_head(hand)
            # Spread fingers over the crown; palm normal points towards hair.
            finger=world_head('Bip001 '+side+' Finger1')-wrist
            direction=Vector((-sign*.55,0,1)).normalized()
            align(hand,finger,finger.lerp(direction*finger.length,w))
            finger=world_head('Bip001 '+side+' Finger1')-wrist
            across=world_head('Bip001 '+side+' Finger2')-world_head('Bip001 '+side+' Finger0')
            normal=across.cross(finger)*(1 if side=='L' else -1);axis=finger.normalized()
            desired=world_head('Bip001 Head')+Vector((0,0,.14))-wrist
            normal-=axis*normal.dot(axis);desired-=axis*desired.dot(axis)
            if normal.length>1e-8 and desired.length>1e-8:
                normal.normalize();desired.normalize()
                rotate(hand,axis,math.atan2(axis.dot(normal.cross(desired)),normal.dot(desired))*w)
        for n in p:
            if n in ('Bip001 L Hand','Bip001 R Hand') or n.startswith(('Bip001 L Finger','Bip001 R Finger')):p[n]=arm.pose.bones[n].matrix_basis.copy()
    return poses
