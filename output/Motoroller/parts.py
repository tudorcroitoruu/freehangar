import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Source.blend')
o=next(o for o in bpy.context.scene.objects if o.type=='MESH');bpy.context.view_layer.objects.active=o;o.select_set(True)
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
rows=[]
for i,o in enumerate(sorted(bpy.context.scene.objects,key=lambda o:len(o.data.vertices),reverse=True)):
 o.name='Part_%03d'%i
 pts=[o.matrix_world@v.co for v in o.data.vertices]
 rows.append(dict(id=i,verts=len(pts),min=[round(min(p[a] for p in pts),5) for a in range(3)],max=[round(max(p[a] for p in pts),5) for a in range(3)]))
open(r'F:/Projects/freehangar/output/Motoroller/parts.json','w').write(json.dumps(rows,indent=2))
print(json.dumps(rows[:60]))
bpy.ops.wm.save_as_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Loose.blend')
