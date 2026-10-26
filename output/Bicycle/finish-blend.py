import bpy, math
from mathutils import Quaternion,Vector
P='D:/Projects/freehangar/'
bpy.ops.wm.open_mainfile(filepath=P+'ArtSource/Bicycle/Bicycle_Optimized.blend')
parts={'Frame','Steering','FrontWheel','RearWheel','Crank','LeftPedal','RightPedal'}
for o in list(bpy.data.objects):
 if o.name not in parts:bpy.data.objects.remove(o,do_unlink=True)
root=bpy.data.objects.new('Bicycle_Root',None);bpy.context.collection.objects.link(root)
for o in bpy.context.scene.objects:
 if o.name in parts:o.parent=root
for m in bpy.data.materials:
 if m.name not in {'frame','detail','brake','gear','seater','other','whell'}:continue
 m.use_nodes=True;n=m.node_tree.nodes;bs=n.get('Principled BSDF');bs.inputs['Roughness'].default_value=.4
 tex=n.new('ShaderNodeTexImage');path=P+('Assets/Models/bicycle/Optimized/Bicycle_Red_BaseColor.png' if m.name=='frame' else 'Assets/Models/bicycle/texture/bicycle_'+m.name+'_BaseColor.png');tex.image=bpy.data.images.load(path,check_existing=True);m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);m.diffuse_color=(.6,.025,.015,1) if m.name=='frame' else (.12,.12,.12,1)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_distance=2.6;area.spaces.active.region_3d.view_location=(0,0,.05);area.spaces.active.region_3d.view_rotation=Quaternion((.83,.44,.17,.28)).normalized();area.spaces.active.shading.type='SOLID';area.spaces.active.shading.color_type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=P+'ArtSource/Bicycle/Bicycle_Optimized.blend')

