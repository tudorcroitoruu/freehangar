import bpy, math, json
from mathutils import Vector
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=r'F:\Projects\freehangar\Assets\Models\rocket.fbm\rocket.fbx')
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in objects:
 print('MODEL',o.name,tuple(o.dimensions),tuple(o.rotation_euler),tuple(o.scale))
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
parts=[]
for o in [o for o in bpy.context.scene.objects if o.type=='MESH']:
 coords=[o.matrix_world@Vector(v) for v in o.bound_box]
 lo=[min(v[k] for v in coords) for k in range(3)];hi=[max(v[k] for v in coords) for k in range(3)]
 parts.append(dict(name=o.name,faces=len(o.data.polygons),lo=lo,hi=hi))
print('PARTS',json.dumps(parts))
bpy.ops.wm.save_as_mainfile(filepath=r'F:\Projects\freehangar\output\Rocket\Inspection.blend')
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
coords=[o.matrix_world@Vector(v) for o in scene.objects if o.type=='MESH' for v in o.bound_box]
center=sum(coords,Vector())/len(coords);extent=max(max(v[k] for v in coords)-min(v[k] for v in coords) for k in range(3))
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=extent*1.3
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
for name,direction in [('front',(1,-2,1)),('back',(-1,2,1))]:
 cam.location=center+Vector(direction).normalized()*extent*2;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath='F:/Projects/freehangar/output/Rocket/'+name+'.png';bpy.ops.render.render(write_still=True)
