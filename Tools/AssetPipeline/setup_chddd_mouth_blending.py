"""Fixed atlas cells and animated weights preserve interrupted transitions."""
import bpy,json,pathlib
OUT=pathlib.Path('W:/Machine/VSCode/Mika_Gift/Material/CHDDD')
manifest=json.loads((OUT/'character_manifest.json').read_text(encoding='utf-8'))
tiles=list(dict.fromkeys(s['mouth_tile'] for s in manifest['expressions'].values()))
material=bpy.data.materials['CHDDD_MouthAtlas'];nodes=material.node_tree.nodes;links=material.node_tree.links
atlas=next(n.image for n in nodes if n.bl_idname=='ShaderNodeTexImage')
if material.node_tree.animation_data:material.node_tree.animation_data.action=None
nodes.clear()
uv=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY';scale.inputs[1].default_value=(.5,.5,1)
links.new(uv.outputs['UV'],scale.inputs[0]);color_sum=None;alpha_sum=None
for index,tile in enumerate(tiles):
    offset=nodes.new('ShaderNodeVectorMath');offset.operation='ADD';offset.name='MouthTileOffset' if index==0 else 'MouthTileOffset_'+str(tile)
    offset.inputs[1].default_value=(tile%100/8,tile//100/8,0);links.new(scale.outputs[0],offset.inputs[0])
    tex=nodes.new('ShaderNodeTexImage');tex.image=atlas;tex.name='MouthTile_'+str(tile);links.new(offset.outputs[0],tex.inputs[0])
    weight=nodes.new('ShaderNodeValue');weight.name='MouthWeight_'+str(tile);weight.outputs[0].default_value=1 if index==0 else 0
    alpha=nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY';links.new(tex.outputs['Alpha'],alpha.inputs[0]);links.new(weight.outputs[0],alpha.inputs[1])
    color=nodes.new('ShaderNodeMixRGB');color.blend_type='MULTIPLY';color.inputs[0].default_value=1
    links.new(tex.outputs['Color'],color.inputs[1]);links.new(alpha.outputs[0],color.inputs[2])
    if color_sum is None:color_sum=color.outputs[0];alpha_sum=alpha.outputs[0]
    else:
        cs=nodes.new('ShaderNodeMixRGB');cs.blend_type='ADD';cs.inputs[0].default_value=1
        links.new(color_sum,cs.inputs[1]);links.new(color.outputs[0],cs.inputs[2]);color_sum=cs.outputs[0]
        asum=nodes.new('ShaderNodeMath');asum.operation='ADD';links.new(alpha_sum,asum.inputs[0]);links.new(alpha.outputs[0],asum.inputs[1]);alpha_sum=asum.outputs[0]
epsilon=nodes.new('ShaderNodeMath');epsilon.operation='MAXIMUM';epsilon.inputs[1].default_value=1e-6;links.new(alpha_sum,epsilon.inputs[0])
straight=nodes.new('ShaderNodeMixRGB');straight.blend_type='DIVIDE';straight.inputs[0].default_value=1;links.new(color_sum,straight.inputs[1]);links.new(epsilon.outputs[0],straight.inputs[2])
emission=nodes.new('ShaderNodeEmission');links.new(straight.outputs[0],emission.inputs[0])
transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader')
links.new(alpha_sum,mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(emission.outputs[0],mix.inputs[2])
output=nodes.new('ShaderNodeOutputMaterial');links.new(mix.outputs[0],output.inputs[0])
material['mouth_blend_tiles']=json.dumps(tiles)
manifest['transitions']={'defaultDurationSeconds':.5,'easing':'smoothstep (3t^2 - 2t^3)',
                         'mouthTiles':tiles,'interruption':'restart from current pose, morph and atlas weights',
                         'unity':'SetEmotion blends; SetEmotionImmediate applies a static pose',
                         'blender':'Eight neutral-to-expression presets, with bone/shape/mouth actions'}
(OUT/'character_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Mouth atlas blending: '+str(tiles))
