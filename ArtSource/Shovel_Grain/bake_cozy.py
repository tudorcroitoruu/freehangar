"""Bake an independent game mesh, leaving the procedural source editable."""
import bpy, math, os
scene=bpy.context.scene
asset=bpy.data.collections['Shovel_Grain | Game Asset']
root=bpy.data.objects['Shovel_Grain']
texdir=r'F:\Projects\freehangar\Assets\Models\Models New\Shovel\Textures\GrainShovel'

scene.cycles.samples=12
for name,power in [('Key',90),('Fill',45),('Rim',75)]:bpy.data.objects[name].data.energy=power
scene.view_settings.exposure=-.1

col=bpy.data.collections.new('Shovel_Grain | Textured Export')
scene.collection.children.link(col)
bpy.ops.object.select_all(action='DESELECT')
deps=bpy.context.evaluated_depsgraph_get()
copies=[]
for o in list(asset.objects):
    if o.type!='MESH':continue
    data=bpy.data.meshes.new_from_object(o.evaluated_get(deps),depsgraph=deps)
    new=bpy.data.objects.new(o.name+'_Baked',data)
    col.objects.link(new);new.matrix_world=o.matrix_world.copy()
    new.select_set(True);copies.append(new)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.object.join()
game=bpy.context.object;game.name='Shovel_Grain_Textured';game.data.name='Shovel_Grain_Textured_Mesh'
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
while len(game.data.uv_layers):game.data.uv_layers.remove(game.data.uv_layers[0])
game.data.uv_layers.new(name='UVMap')
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012)
bpy.ops.object.mode_set(mode='OBJECT')
for i,m in enumerate(list(game.data.materials)):
    game.data.materials[i]=m.copy()
for o in asset.objects:
    o.hide_render=True;o.hide_set(True)
scene.render.bake.use_pass_direct=False
scene.render.bake.use_pass_indirect=False
scene.render.bake.use_pass_color=True
scene.render.bake.margin=12
scene.render.bake.use_selected_to_active=False
scene.render.bake.target='IMAGE_TEXTURES'
game['revision']='Cozy finish; straight comfortable handle; raised exterior mount'
game['editable_source_collection']=asset.name
result={'bake_ready':game.name,'vertices':len(game.data.vertices),'materials':[m.name for m in game.data.materials]}
