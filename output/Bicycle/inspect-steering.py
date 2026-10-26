import bpy
from mathutils import Vector
P='D:/Projects/freehangar/'
bpy.ops.wm.open_mainfile(filepath=P+'ArtSource/Bicycle/Bicycle_Optimized.blend')
for o in bpy.context.scene.objects:
 o.color=(1,.05,.05,1) if o.name=='Steering' else (.1,.8,.1,1) if o.name=='FrontWheel' else (.7,.7,.7,1)
bpy.ops.object.camera_add(location=(4,0,0));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.1
sc=bpy.context.scene;sc.camera=cam;sc.render.engine='BLENDER_WORKBENCH';sc.display.shading.light='STUDIO';sc.display.shading.color_type='OBJECT';sc.render.resolution_x=1400;sc.render.resolution_y=900;sc.render.resolution_percentage=100;sc.render.filepath=P+'output/Bicycle/steering-before-side.png';bpy.ops.render.render(write_still=True)
