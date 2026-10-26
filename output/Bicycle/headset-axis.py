import bpy,numpy as np,json
P='D:/Projects/freehangar/'
bpy.ops.wm.open_mainfile(filepath=P+'output/Bicycle/Loose.blend')
r=[]
for name in ['rama_low.063','rama_low.064','rama_low.065','rama_low.067']:
 o=bpy.data.objects[name];v=np.array([tuple(v.co) for v in o.data.vertices]);mean=v.mean(0);w,e=np.linalg.eigh(np.cov(v.T));axis=e[:,-1];axis=axis if axis[2]>0 else -axis
 r.append(dict(name=name,mean=mean.tolist(),axis=axis.tolist(),min=v.min(0).tolist(),max=v.max(0).tolist()))
open(P+'output/Bicycle/headset-axis.json','w').write(json.dumps(r,indent=2));print(r)
