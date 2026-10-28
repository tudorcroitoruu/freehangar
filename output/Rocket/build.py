import bpy, os
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='F:/Projects/freehangar/output/Rocket/Inspection.blend')
groups={'Hull':[],'Entrance Stairs':[],'Entrance Door':[]}
for o in list(bpy.context.scene.objects):
 if o.type!='MESH':continue
 vs=[o.matrix_world@v.co for v in o.data.vertices]
 lo=Vector(tuple(min(v[k] for v in vs) for k in range(3)));hi=Vector(tuple(max(v[k] for v in vs) for k in range(3)))
 group='Hull'
 if lo.x>-.61 and hi.x<.61 and hi.y<-1.40 and hi.z<1.70:group='Entrance Stairs'
 elif lo.x>-.49 and hi.x<.49 and hi.y<-1.26 and lo.z>1.80 and hi.z<3.41:group='Entrance Door'
 groups[group].append(o)
for name,objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 origin=Vector((-.481,-1.70,1.80)) if name=='Entrance Door' else Vector((0,-1.404,1.61)) if name=='Entrance Stairs' else Vector((0,0,0))
 bpy.context.scene.cursor.location=origin;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 print(name,len(o.data.polygons),tuple(o.dimensions))
# Cut an actual arched passage through the underlying hull, behind the separate hatch.
outline=[(-.465,1.78),(.465,1.78),(.465,2.93)]
import math
outline += [(math.cos(a)*.465,2.93+math.sin(a)*.465) for a in [math.pi*k/12 for k in range(1,13)]]
verts=[(x,y,z) for y in (-2.3,-.75) for x,z in outline];n=len(outline)
faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(k,(k+1)%n,(k+1)%n+n,k+n) for k in range(n)]
me=bpy.data.meshes.new('Doorway cut');me.from_pydata(verts,[],faces);me.update()
import bmesh
bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
cut=bpy.data.objects.new('Doorway cut',me);bpy.context.collection.objects.link(cut)
hull=bpy.data.objects['Hull'];bpy.context.view_layer.objects.active=hull
bm=bmesh.new();bm.from_mesh(hull.data)
for co,normal in [((-.465,0,0),(1,0,0)),((.465,0,0),(1,0,0)),((0,0,1.78),(0,0,1)),((0,0,3.30),(0,0,1)),((0,-.75,0),(0,1,0))]:
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=co,plane_no=normal,clear_inner=False,clear_outer=False)
remove=[f for f in bm.faces if abs(f.calc_center_median().x)<.46499 and f.calc_center_median().y<-.75 and 1.78001<f.calc_center_median().z<3.29999]
bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(hull.data);bm.free();bpy.data.objects.remove(cut,do_unlink=True)
print('Doorway ray',hull.ray_cast(Vector((0,-5,2.4)),Vector((0,1,0)))[:2])
os.makedirs('F:/Projects/freehangar/Assets/Models/Rocket Modular',exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath='F:/Projects/freehangar/Assets/Models/Rocket Modular/Rocket separated.fbx',use_selection=True,object_types={'MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
bpy.ops.wm.save_as_mainfile(filepath='F:/Projects/freehangar/output/Rocket/Rocket separated.blend')
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.show_cavity=True
bpy.data.objects['Entrance Door'].hide_render=True
bpy.ops.object.camera_add(location=(0,-5.4,2.8));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,2.6))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=26;scene.camera=cam
scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100;scene.render.filepath='F:/Projects/freehangar/output/Rocket/OpenDoor.png';bpy.ops.render.render(write_still=True)
