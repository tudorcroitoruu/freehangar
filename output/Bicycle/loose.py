import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'D:/Projects/freehangar/output/Bicycle/Aligned.blend')
o=bpy.data.objects['rama_low'];bpy.context.view_layer.objects.active=o;bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
r=[]
for o in bpy.context.selected_objects:
 if o.type!='MESH' or not o.data.polygons:continue
 vs=o.data.vertices;lo=[min(v.co[j] for v in vs) for j in range(3)];hi=[max(v.co[j] for v in vs) for j in range(3)]
 r.append(dict(name=o.name,n=len(vs),lo=lo,hi=hi,mat=max(set(p.material_index for p in o.data.polygons),key=lambda k:sum(p.material_index==k for p in o.data.polygons))))
open(r'D:/Projects/freehangar/output/Bicycle/parts.json','w').write(json.dumps(r,indent=1))
bpy.ops.wm.save_as_mainfile(filepath=r'D:/Projects/freehangar/output/Bicycle/Loose.blend')

