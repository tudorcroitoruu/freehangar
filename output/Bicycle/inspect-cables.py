import bpy
from mathutils import Vector
P='D:/Projects/freehangar/';bpy.ops.wm.open_mainfile(filepath=P+'output/Bicycle/Loose.blend')
for o in bpy.context.scene.objects:
 if not o.name.startswith('rama_low') or o.type!='MESH':o.hide_render=True;continue
 for v in o.data.vertices:x,y,z=v.co;v.co=(x,.414-y,z+.239)
 o.color=(1,.02,.02,1) if o.name=='rama_low.203' else (.05,.2,1,1) if o.name=='rama_low.204' else (.7,.7,.7,1)
bpy.ops.object.camera_add(location=(4,0,0));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=2.1
sc=bpy.context.scene;sc.camera=cam;sc.render.engine='BLENDER_WORKBENCH';sc.display.shading.light='STUDIO';sc.display.shading.color_type='OBJECT';sc.render.resolution_x=1200;sc.render.resolution_y=800;sc.render.resolution_percentage=100;sc.render.filepath=P+'output/Bicycle/cable-identification.png';bpy.ops.render.render(write_still=True)
