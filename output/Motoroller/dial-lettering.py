import bpy,json,math
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
vertices=[];triangles=[]
for value in range(0,61,10):
 a=math.radians(135-value/60*270)
 x=-math.sin(a)*.026;y=math.cos(a)*.026
 c=bpy.data.curves.new('Number','FONT');c.body=str(value);c.align_x='CENTER';c.align_y='CENTER';c.size=.006;c.resolution_u=3
 o=bpy.data.objects.new('Number',c);bpy.context.collection.objects.link(o);o.location=(x,y,0)
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.data.calc_loop_triangles();base=len(vertices)
 vertices += [list(o.matrix_world@v.co) for v in o.data.vertices]
 triangles += [base+i for t in o.data.loop_triangles for i in reversed(t.vertices)]
 bpy.data.objects.remove(o,do_unlink=True)
c=bpy.data.curves.new('Units','FONT');c.body='km/h';c.align_x='CENTER';c.align_y='CENTER';c.size=.0045;c.resolution_u=3
o=bpy.data.objects.new('Units',c);bpy.context.collection.objects.link(o);o.location=(0,-.021,0);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.data.calc_loop_triangles();base=len(vertices)
vertices += [list(o.matrix_world@v.co) for v in o.data.vertices];triangles += [base+i for t in o.data.loop_triangles for i in reversed(t.vertices)]
open(r'F:/Projects/freehangar/output/Motoroller/dial-lettering.json','w').write(json.dumps({'vertices':[{'x':v[0],'y':v[1],'z':v[2]} for v in vertices],'triangles':triangles}))
