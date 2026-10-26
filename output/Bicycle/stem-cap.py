import bpy,json
P='D:/Projects/freehangar/';bpy.ops.wm.open_mainfile(filepath=P+'output/Bicycle/Loose.blend')
o=bpy.data.objects['rama_low.064'];r=[]
for p in o.data.polygons:
 if p.center.z<.15 and abs(p.normal.x)<.1:r.append(dict(area=p.area,center=list(p.center),normal=list(p.normal)))
print(sorted(r,key=lambda p:-p['area'])[:20])
