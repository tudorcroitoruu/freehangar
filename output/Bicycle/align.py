import bpy,math
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'D:/Projects/freehangar/output/Bicycle/Source.blend')
o=bpy.data.objects['rama_low'];o.location=(0,0,0);o.rotation_euler=(0,0,0);o.scale=(1,1,1)
a=math.atan2(2.109-.342*-1,5.016) # corrected below
a=math.atan2(2.110-(-.342),5.016)
c=math.cos(a);s=math.sin(a)
for v in o.data.vertices:
 x,y,z=v.co;v.co=(z*.2,-(c*x+s*y)*.2,(s*x-c*y)*.2)
bpy.context.view_layer.objects.active=o;o.select_set(True)
# camera side
bpy.ops.object.camera_add(location=(4, .45, .4));cam=bpy.context.object;cam.rotation_euler=(Vector((0,.45,.4))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.5
sc=bpy.context.scene;sc.camera=cam;sc.render.engine='BLENDER_WORKBENCH';sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.render.resolution_x=1000;sc.render.resolution_y=600;sc.render.resolution_percentage=100;sc.render.filepath=r'D:/Projects/freehangar/output/Bicycle/source.png';bpy.data.objects['Cube'].hide_render=True
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=r'D:/Projects/freehangar/output/Bicycle/Aligned.blend')

