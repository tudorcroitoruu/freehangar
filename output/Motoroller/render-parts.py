import bpy,math
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Loose.blend')
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='RANDOM';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True
cam=bpy.data.objects.new('Camera',bpy.data.cameras.new('Camera'));s.collection.objects.link(cam);cam.location=(2,0,.3);cam.rotation_euler=(Vector((0,0,.25))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.95;s.camera=cam
s.render.resolution_x=1400;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.render.filepath=r'F:/Projects/freehangar/output/Motoroller/parts.png';bpy.ops.render.render(write_still=True)
