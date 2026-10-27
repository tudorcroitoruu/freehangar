import bpy,json
bpy.ops.wm.open_mainfile(filepath=r'F:/Projects/freehangar/output/Motoroller/Loose.blend')
o=bpy.data.objects['Part_031'];print(json.dumps([{'center':list(p.center),'normal':list(p.normal),'area':p.area} for p in sorted([p for p in o.data.polygons if p.normal.y>.7],key=lambda p:p.area,reverse=True)[:12]]))

