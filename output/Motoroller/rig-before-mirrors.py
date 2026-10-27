import bpy,json,os
from mathutils import Vector
P='F:/Projects/freehangar/'
bpy.ops.wm.open_mainfile(filepath=P+'output/Motoroller/Loose.blend')
groups={k:[] for k in ['Body','SteeringAssembly','FrontWheel','RearWheel']}
steer={1,2,8,9,10,11,21,22,25,31,32,33,36,37,38,39,40,42,43,44,45}
for o in list(bpy.context.scene.objects):
 i=int(o.name.split('_')[-1]);key='Body'
 if i in {12,13,17} or 48<=i<=63:key='FrontWheel'
 elif i in {14,15,18} or 64<=i<=79:key='RearWheel'
 elif i in steer:key='SteeringAssembly'
 groups[key].append(o)
# meters: centered between axles at ground level, Blender -Y / Unity +Z forward.
scale=2.5;mid=(-.3+.219305)/2;ground=-.000263
origins={'Body':(0,mid,ground),'SteeringAssembly':(0,-.112,.4),'FrontWheel':(0,-.3,.098575),'RearWheel':(0,.219305,.098575)}
report=[]
for key,objs in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objs:o.select_set(True)
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=bpy.context.object;o.name=key
 before=sum(len(p.vertices)-2 for p in o.data.polygons)
 mod=o.modifiers.new('Preserve silhouette reduction','DECIMATE');mod.ratio=.38 if 'Wheel' not in key else .65
 bpy.ops.object.modifier_apply(modifier=mod.name)
 origin=Vector(origins[key])
 for v in o.data.vertices:v.co=(v.co-origin)*scale
 o.location=((origin.x)*scale,(origin.y-mid)*scale,(origin.z-ground)*scale)
 # Remove unused material slots so wheels and body do not issue empty submesh draws.
 bpy.context.view_layer.objects.active=o;bpy.ops.object.material_slot_remove_unused()
 report.append(dict(part=key,before=before,after=sum(len(p.vertices)-2 for p in o.data.polygons),pivot=list(o.location)))
os.makedirs(P+'Assets/Models/motorcycle-old/Optimized',exist_ok=True);os.makedirs(P+'ArtSource/Motoroller',exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=P+'Assets/Models/motorcycle-old/Optimized/Motoroller_Rigged.fbx',use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)
# Editable rig: common inclined steering parent carries the wheel axle and assembly.
root=bpy.data.objects.new('Motoroller_Root',None);bpy.context.collection.objects.link(root)
pivot=bpy.data.objects.new('SteeringPivot',None);bpy.context.collection.objects.link(pivot);pivot.parent=root;pivot.location=bpy.data.objects['SteeringAssembly'].location;pivot.rotation_mode='QUATERNION';pivot.rotation_quaternion=Vector((0,0,1)).rotation_difference(Vector((0,.43,.903)))
bpy.context.view_layer.update()
for key in groups:
 o=bpy.data.objects[key];world=o.matrix_world.copy();o.parent=pivot if key in ['FrontWheel','SteeringAssembly'] else root;o.matrix_world=world
for m in bpy.data.materials:
 if not m.use_nodes:continue
 bs=m.node_tree.nodes.get('Principled BSDF')
 if not bs:continue
 tex=m.node_tree.nodes.new('ShaderNodeTexImage');kind='Wheels' if 'Wheels' in m.name else 'Body';tex.image=bpy.data.images.load(P+'Assets/Models/motorcycle-old/textures/map_'+kind+'M05_BaseColor.png',check_existing=True);m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
bpy.ops.wm.save_as_mainfile(filepath=P+'ArtSource/Motoroller/Motoroller_Rigged.blend')
open(P+'output/Motoroller/optimization.json','w').write(json.dumps(report,indent=2));print(json.dumps(report))
