import bpy,math,json,os
from mathutils import Vector
P='D:/Projects/freehangar/'
bpy.ops.wm.open_mainfile(filepath=P+'output/Bicycle/Loose.blend')
groups={k:[] for k in ['Frame','Steering','FrontWheel','RearWheel','Crank','LeftPedal','RightPedal','RearControlCable']}
for o in list(bpy.context.scene.objects):
 if o.type!='MESH' or not o.name.startswith('rama_low') or not o.data.polygons:continue
 vs=o.data.vertices;lo=Vector([min(v.co[j] for v in vs) for j in range(3)]);hi=Vector([max(v.co[j] for v in vs) for j in range(3)]);c=(lo+hi)/2;d=hi-lo
 mat=max(set(p.material_index for p in o.data.polygons),key=lambda k:sum(p.material_index==k for p in o.data.polygons))
 key='Frame'
 if o.name=='rama_low.203':key='RearControlCable'
 elif mat==6:key='FrontWheel' if c.y>.4 else 'RearWheel'
 elif mat==3 and .20<c.y<.43:key='Crank'
 elif abs(c.x)>.08 and .12<c.y<.48 and c.z<-.28 and d.y<.12:key='LeftPedal' if c.x<0 else 'RightPedal'
 elif (c.y>.48 and c.z>.10) or c.y>.75 or (mat==2 and c.y>.7):key='Steering'
 groups[key].append(o)
for o in bpy.context.scene.objects:o.select_set(False)
result=[]
# center pivot height of bounding box; custom animation origins
origins={'Frame':(0,.414,-.239),'Steering':(0,.69,.07),'FrontWheel':(0,.972,-.399),'RearWheel':(0,-.145,-.399),'Crank':(0,.311,-.410),'LeftPedal':(-.115,.172,-.32),'RightPedal':(.12,.453,-.50),'RearControlCable':(0,.414,-.239)}
for key,objects in groups.items():
 if not objects:continue
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=key
 # decimate joined geometry retaining uv and material borders
 mod=o.modifiers.new('Gameplay reduction','DECIMATE');mod.ratio=.19 if 'Wheel' in key else .16
 bpy.ops.object.modifier_apply(modifier=mod.name)
 origin=Vector(origins[key])
 for v in o.data.vertices:
  q=v.co-origin;v.co=(q.x,-q.y,q.z)
 o.location=(origin.x,.414-origin.y,origin.z+.239)
 for p in o.data.polygons:p.use_smooth=True
 o.data.calc_loop_triangles();result.append({'name':key,'triangles':len(o.data.loop_triangles),'origin':list(o.location)})
# remove non-bike only from export via selection, keep source in blend
bpy.ops.object.select_all(action='DESELECT')
for k in groups:
 if bpy.data.objects.get(k):bpy.data.objects[k].select_set(True)
os.makedirs(P+'Assets/Models/bicycle/Optimized',exist_ok=True)
bpy.ops.export_scene.fbx(filepath=P+'Assets/Models/bicycle/Optimized/Bicycle_Rigged.fbx',use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False)

# Authoring hierarchy mirrors the common steering axis used by the game rig.
from mathutils import Quaternion
root=bpy.data.objects.new('Bicycle_Root',None);bpy.context.collection.objects.link(root)
for key in groups:
 o=bpy.data.objects.get(key)
 if o:o.parent=root
pivot=bpy.data.objects.new('Headset steering pivot',None);bpy.context.collection.objects.link(pivot);pivot.parent=root
pivot.location=(.00185097,-.3051303,.34900263);pivot.rotation_mode='QUATERNION';pivot.rotation_quaternion=Vector((0,0,1)).rotation_difference(Vector((0,.3572176944,.9340211538)))
bpy.context.view_layer.update()
for key in ['Steering','FrontWheel']:
 o=bpy.data.objects[key];world=o.matrix_world.copy();o.parent=pivot;o.matrix_world=world
for o in list(bpy.data.objects):
 if o.type=='MESH' and o.name not in groups:bpy.data.objects.remove(o,do_unlink=True)
for m in bpy.data.materials:
 if m.name not in {'frame','detail','brake','gear','seater','other','whell'}:continue
 n=m.node_tree.nodes;bs=n.get('Principled BSDF');tex=n.new('ShaderNodeTexImage')
 path=P+('Assets/Models/bicycle/Optimized/Bicycle_Red_BaseColor.png' if m.name=='frame' else 'Assets/Models/bicycle/texture/bicycle_'+m.name+'_BaseColor.png')
 tex.image=bpy.data.images.load(path,check_existing=True);m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);m.diffuse_color=(.6,.025,.015,1) if m.name=='frame' else (.12,.12,.12,1)
bpy.ops.wm.save_as_mainfile(filepath=P+'ArtSource/Bicycle/Bicycle_Rigged.blend')
open(P+'output/Bicycle/rigged-optimization.json','w').write(json.dumps(result,indent=2));print(result)
