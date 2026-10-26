from pathlib import Path
s=Path('output/Bicycle/optimize.py').read_text()
s=s[:s.index('# red paint texture')]
s=s.replace("'RightPedal']}","'RightPedal','RearControlCable']}")
s=s.replace("if mat==6:key=", "if o.name=='rama_low.203':key='RearControlCable'\n elif mat==6:key=")
s=s.replace("elif (c.y>.48 and c.z>.10) or (mat==0 and lo.y>.7):key='Steering'","elif (c.y>.48 and c.z>.10) or c.y>.75 or (mat==2 and c.y>.7):key='Steering'")
s=s.replace("'RightPedal':(.12,.453,-.50)}","'RightPedal':(.12,.453,-.50),'RearControlCable':(0,.414,-.239)}")
s=s.replace('Bicycle_Red.fbx','Bicycle_Rigged.fbx')
s+='''
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
'''
Path('output/Bicycle/build-steering-rig.py').write_text(s)
