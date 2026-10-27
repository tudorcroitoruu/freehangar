import bpy,bmesh,json
from mathutils import Vector
P='F:/Projects/freehangar/'
bpy.ops.wm.open_mainfile(filepath=P+'output/Motoroller/Loose.blend')
parts=[]
for side,ids in [('Left',(21,31,42)),('Right',(22,32,43))]:
 head=bpy.data.objects['Part_%03d'%ids[1]]
 axis=Vector((-.3983 if side=='Left' else .3983,.9172,-.00967)).normalized()
 for kind in ['Stem','Housing','Glass']:
  objs=[]
  sources=[bpy.data.objects['Part_%03d'%i] for i in (ids[0],ids[2])] if kind=='Stem' else [head]
  for src in sources:
   o=src.copy();o.data=src.data.copy();bpy.context.collection.objects.link(o)
   if kind!='Stem':
    bm=bmesh.new();bm.from_mesh(o.data)
    remove=[f for f in bm.faces if (f.normal.dot(axis)>.999 and f.calc_center_median().dot(axis)<-.127)!=(kind=='Glass')]
    bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(o.data);bm.free()
   objs.append(o)
  bpy.ops.object.select_all(action='DESELECT')
  for o in objs:o.select_set(True)
  bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();o=bpy.context.object;o.name=side+'Mirror'+kind
  if kind!='Glass':
   mod=o.modifiers.new('Reduced mirror detail','DECIMATE');mod.ratio=.38;bpy.ops.object.modifier_apply(modifier=mod.name)
  o.data.calc_loop_triangles();v=[];n=[];uv=[];tri=[]
  for t in o.data.loop_triangles:
   for li in t.loops:
    loop=o.data.loops[li];co=o.data.vertices[loop.vertex_index].co;normal=loop.normal
    v.append({'x':co.x*2.5,'y':(co.z+.000263)*2.5,'z':-(co.y+.0403475)*2.5})
    n.append({'x':normal.x,'y':normal.z,'z':-normal.y});tex=o.data.uv_layers.active.data[li].uv;uv.append({'x':tex.x,'y':tex.y});tri.append(len(tri))
  center=[sum(p[a] for p in v)/len(v) for a in ('x','y','z')]
  parts.append({'name':side+'Mirror'+kind,'side':side,'kind':kind,'vertices':v,'normals':n,'uv':uv,'triangles':tri,'center':{'x':center[0],'y':center[1],'z':center[2]}})
open(P+'output/Motoroller/mirror-parts.json','w').write(json.dumps({'parts':parts}))
print([(p['name'],len(p['triangles'])//3) for p in parts])
# Keep a dedicated editable source containing only the separated mirror components.
for o in list(bpy.context.scene.objects):
 if o.name not in [p['name'] for p in parts]:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=P+'ArtSource/Motoroller/MirrorParts.blend')

