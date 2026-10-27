import bpy,json
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'F:/Projects/freehangar/Assets/Models/motorcycle-old/source/Motoroller_001.fbx')
rows=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 pts=[o.matrix_world@Vector(c) for c in o.bound_box]
 rows.append(dict(name=o.name,verts=len(o.data.vertices),tris=sum(len(p.vertices)-2 for p in o.data.polygons),min=[min(p[i] for p in pts) for i in range(3)],max=[max(p[i] for p in pts) for i in range(3)],materials=[m.name for m in o.data.materials]))
print(json.dumps(rows,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Source.blend')
