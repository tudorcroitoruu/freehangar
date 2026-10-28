import bpy,json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='F:/Projects/freehangar/output/Rocket/Inspection.blend')
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 vs=[o.matrix_world@v.co for v in o.data.vertices]
 lo=Vector(tuple(min(v[k] for v in vs) for k in range(3)));hi=Vector(tuple(max(v[k] for v in vs) for k in range(3)))
 if lo.x>-.7 and hi.x<.7 and lo.y<-1.2 and hi.z<3.6:
  print(o.name,len(o.data.polygons),tuple(round(x,3) for x in lo),tuple(round(x,3) for x in hi))
