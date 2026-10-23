import bpy, bmesh, math, os
from mathutils import Vector, Quaternion

scene=bpy.context.scene
asset=bpy.data.collections['Shovel_Grain | Game Asset']
root=bpy.data.objects['Shovel_Grain']
out=r'F:\Projects\freehangar\ArtSource\Shovel_Grain'
texdir=r'F:\Projects\freehangar\Assets\Models\Models New\Shovel\Textures\GrainShovel'
os.makedirs(texdir,exist_ok=True)

# Preserve the first version as editable geometry in a hidden collection.
archive=bpy.data.collections.new('Archive | Before cozy refinement')
scene.collection.children.link(archive)
for o in list(asset.objects):
    if o==root:continue
    old=o.copy()
    old.data=o.data.copy()
    archive.objects.link(old)
    old.name=o.name+'_BeforeCozy'
    old.parent=None
    old.hide_render=True
    old.hide_set(True)

for name in ['Shovel_Grain_Shaft_OriginalGeometry','Shovel_Grain_Socket','Shovel_Grain_SocketRivet']:
    bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)

def mat(name, low, high, metallic, roughness, grain=False):
    m=bpy.data.materials.new(name)
    m.use_nodes=True
    m.diffuse_color=(*[(a+b)/2 for a,b in zip(low,high)],1)
    n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF')
    p.inputs['Metallic'].default_value=metallic
    p.inputs['Roughness'].default_value=roughness
    coords=n.new('ShaderNodeTexCoord')
    mapping=n.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY'
    mapping.inputs[1].default_value=(110,110,9) if grain else (22,22,22)
    l.new(coords.outputs['Object'],mapping.inputs[0])
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=1
    noise.inputs['Detail'].default_value=2.0
    noise.inputs['Roughness'].default_value=.58
    l.new(mapping.outputs['Vector'],noise.inputs['Vector'])
    ramp=n.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position=.15
    ramp.color_ramp.elements[0].color=(*low,1)
    ramp.color_ramp.elements[1].position=.85
    ramp.color_ramp.elements[1].color=(*high,1)
    l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs['Color'],p.inputs['Base Color'])
    fine=n.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=240
    fine.inputs['Detail'].default_value=2
    l.new(coords.outputs['Object'],fine.inputs['Vector'])
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.13
    bump.inputs['Distance'].default_value=.0006
    l.new(fine.outputs['Fac'],bump.inputs['Height']);l.new(bump.outputs['Normal'],p.inputs['Normal'])
    rough=n.new('ShaderNodeMapRange');rough.inputs['From Min'].default_value=0
    rough.inputs['From Max'].default_value=1
    rough.inputs['To Min'].default_value=roughness-.05
    rough.inputs['To Max'].default_value=min(.95,roughness+.05)
    l.new(noise.outputs['Fac'],rough.inputs['Value']);l.new(rough.outputs['Result'],p.inputs['Roughness'])
    return m

stone=mat('GrainShovel_CozyWarmSlate',(.085,.074,.060),(.155,.139,.113),.12,.86)
edge=mat('GrainShovel_SoftWornEdges',(.145,.128,.100),(.205,.181,.142),.16,.77)
wood=mat('GrainShovel_HoneyOrangeWood',(.36,.087,.014),(.54,.168,.035),0,.77,True)

for name in ['Shovel_Grain_DeepScoop','Shovel_Grain_FoldedRim','Shovel_Grain_CuttingEdge']:
    o=bpy.data.objects[name]
    o.data.materials.clear();o.data.materials.append(stone if 'DeepScoop' in name else edge)
    for p in o.data.polygons:p.material_index=0
    for mod in o.modifiers:
        if mod.type=='BEVEL':mod.width=.004;mod.segments=3
        elif mod.type=='SOLIDIFY':mod.thickness=.006

def mesh(name,verts,faces,material):
    data=bpy.data.meshes.new(name+'_Mesh');data.from_pydata(verts,[],faces);data.update()
    bm=bmesh.new();bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    obj=bpy.data.objects.new(name,data);asset.objects.link(obj);obj.parent=root
    data.materials.append(material)
    return obj

def bevel(o,width):
    m=o.modifiers.new('Soft comfortable corners','BEVEL');m.width=width;m.segments=3
    m=o.modifiers.new('Broad weighted normals','WEIGHTED_NORMAL');m.keep_sharp=True

# Back wall runs from (y=.043,z=.325) to (y=-.155,z=.370).
# Mount lies outside that wall (+Z), higher along it toward the rim.
start=Vector((0,-.047,.359))
direction=Vector((0,.205,.979)).normalized()
right=Vector((1,0,0));up=direction.cross(right).normalized()
sections=[(0,.023,.023),(.045,.021,.021),(.305,.019,.019),
          (.333,.019,.019),(.342,.027,.025),(.360,.036,.031),
          (.387,.038,.033),(.695,.035,.032),(.724,.033,.030),
          (.738,.027,.025),(.742,.022,.020)]
verts=[];faces=[]
for s,w,d in sections:
    c=min(w,d)*.37
    cross=[(-w+c,-d),(w-c,-d),(w,-d+c),(w,d-c),(w-c,d),(-w+c,d),(-w,d-c),(-w,-d+c)]
    for x,y in cross:verts.append(tuple(start+s*direction+x*right+y*up))
for r in range(len(sections)-1):
    for j in range(8):faces.append((r*8+j,r*8+(j+1)%8,(r+1)*8+(j+1)%8,(r+1)*8+j))
faces.append(tuple(reversed(range(8))));faces.append(tuple((len(sections)-1)*8+j for j in range(8)))
handle=mesh('Shovel_Grain_StraightComfortHandle',verts,faces,stone)
handle.data.materials.append(wood)
for p in handle.data.polygons:
    s=sum((Vector(verts[i])-start).dot(direction) for i in p.vertices)/len(p.vertices)
    p.material_index=1 if s>.337 else 0
bevel(handle,.002)
handle['mount']='Outside rear wall, elevated toward rim; no shaft inside the scoop'

# A low, broad octagonal mounting shoe, following the sloped rear surface.
backnormal=Vector((0,.221,.975)).normalized()
backvertical=Vector((0,-.975,.221)).normalized()
center=Vector((0,-.047,.351))
outline=[(-.025,-.051),(.025,-.051),(.043,-.030),(.043,.034),(.027,.050),(-.027,.050),(-.043,.034),(-.043,-.030)]
verts=[]
for depth,scale in [(-.004,1),(.014,.86)]:
    for x,y in outline:verts.append(tuple(center+right*x*scale+backvertical*y*scale+backnormal*depth))
faces=[tuple(reversed(range(8))),tuple(range(8,16))]
for j in range(8):faces.append((j,(j+1)%8,(j+1)%8+8,j+8))
shoe=mesh('Shovel_Grain_ExteriorMount',verts,faces,stone);bevel(shoe,.0035)

# UV unwrap editable pieces. All texture variation is subtle and material-based.
for o in asset.objects:
    if o.type!='MESH':continue
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025)
    bpy.ops.object.mode_set(mode='OBJECT')

# Warm studio lighting without changing the actual texture colors.
scene.world.color=(.11,.095,.075)
for name,color,power in [('Key',(1,.89,.74),115),('Fill',(.86,.92,1),85),('Rim',(1,.81,.58),140)]:
    o=bpy.data.objects[name];o.data.color=color;o.data.energy=power
scene.view_settings.look='AgX - Medium High Contrast'
scene.view_settings.exposure=0
scene.cycles.samples=48
scene.camera.location=(.8,-1.8,.95)
q=(Vector((0,0,.52))-scene.camera.location).to_track_quat('-Z','Y')
scene.camera.rotation_euler=(q@Quaternion((0,0,1),math.radians(142))).to_euler()
scene.camera.data.ortho_scale=1.67
scene.render.resolution_x=1300;scene.render.resolution_y=1000
scene.render.filepath=out+'\\Shovel_Grain_Preview.png'
bpy.context.view_layer.update()
bpy.ops.render.render(write_still=True)
result={'preview':scene.render.filepath,'handle_start':list(start),'handle_end':list(start+.742*direction),'parts':[o.name for o in asset.objects]}
