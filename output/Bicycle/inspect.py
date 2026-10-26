import bpy,json
bpy.ops.import_scene.fbx(filepath=r'D:/Projects/freehangar/Assets/Models/bicycle/bicycle.fbx')
r=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  o.data.calc_loop_triangles()
  r.append(dict(name=o.name,verts=len(o.data.vertices),tris=len(o.data.loop_triangles),dims=list(o.dimensions),loc=list(o.location),rot=list(o.rotation_euler),materials=[m.name if m else '' for m in o.data.materials],bounds=[list(o.matrix_world@__import__('mathutils').Vector(v)) for v in o.bound_box]))
open(r'D:/Projects/freehangar/output/Bicycle/source.json','w').write(json.dumps(r,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=r'D:/Projects/freehangar/output/Bicycle/Source.blend')
