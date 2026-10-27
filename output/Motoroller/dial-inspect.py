import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Loose.blend')
o=bpy.data.objects['Part_001']
rows=[]
for p in o.data.polygons:
 c=p.center
 if abs(c.x)<.023 and p.normal.z>.90 and p.normal.y>.40 and p.normal.y<.45:rows.append({'center':list(c),'normal':list(p.normal),'area':p.area})
print(json.dumps(sorted(rows,key=lambda x:x['area'],reverse=True)[:55]))


