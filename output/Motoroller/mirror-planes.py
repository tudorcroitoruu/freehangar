import bpy,collections
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Loose.blend');o=bpy.data.objects['Part_031'];n=Vector((-.3983,.9172,-.00967));bins=collections.defaultdict(lambda:[0,0])
for p in o.data.polygons:
 if p.normal.dot(n)>.999:
  k=round(p.center.dot(n),5);bins[k][0]+=1;bins[k][1]+=p.area
print(dict(bins))
