import bpy,numpy as np,json
P='D:/Projects/freehangar/';bpy.ops.wm.open_mainfile(filepath=P+'output/Bicycle/Loose.blend')
o=bpy.data.objects['rama_low.064'];v=np.array([tuple(v.co) for v in o.data.vertices]);axis=np.array([0,-.3572176944,.9340211538]);d=v@axis;pts=v[d<d.min()+.0001];print('axis',axis,'capcenter',pts.mean(0),'count',len(pts),'minprojection',d.min())
