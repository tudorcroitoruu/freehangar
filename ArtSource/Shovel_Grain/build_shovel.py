"""Create a grain scoop variant from the imported Shovel_1 handle in Blender."""
import bpy
import bmesh
import math
import os
from mathutils import Vector, Quaternion

source = bpy.data.objects['Shovel_002']
scene = bpy.context.scene
asset = bpy.data.collections.new('Shovel_Grain | Game Asset')
scene.collection.children.link(asset)
reference = bpy.data.collections.new('Source | Shovel_1 (preserved)')
scene.collection.children.link(reference)
for collection in list(source.users_collection):
    collection.objects.unlink(source)
reference.objects.link(source)
source.hide_render = True
source.hide_set(True)

def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    return mat

steel = material('GrainShovel_DarkSteel', (.105, .113, .12), .65, .64)
edge = material('GrainShovel_EdgeSteel', (.20, .215, .23), .72, .46)
orange = material('GrainShovel_OrangeGrip', (.44, .072, .009), .0, .63)
orange_dark = material('GrainShovel_GripEnd', (.26, .035, .005), .0, .68)

def mesh_object(name, vertices, faces, mat):
    mesh = bpy.data.meshes.new(name + '_Mesh')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    asset.objects.link(obj)
    mesh.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    return obj

def bevel(obj, width, segments=1):
    mod = obj.modifiers.new('Small edge chamfers', 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod = obj.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True

# Reuse the actual connected shaft mesh, including its slight bend and UVs.
shaft = source.copy()
shaft.data = source.data.copy()
asset.objects.link(shaft)
shaft.name = 'Shovel_Grain_Shaft_OriginalGeometry'
shaft.hide_set(False)
shaft.hide_render = False
bm = bmesh.new()
bm.from_mesh(shaft.data)
bm.verts.ensure_lookup_table()
seed = max(bm.verts, key=lambda v: (shaft.matrix_world @ v.co).z)
keep = set()
stack = [seed]
while stack:
    v = stack.pop()
    if v in keep:
        continue
    keep.add(v)
    stack.extend(e.other_vert(v) for e in v.link_edges)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context='VERTS')
for v in bm.verts:
    v.co = shaft.matrix_world @ v.co
    # A wider orange hand grip, with the existing taper retained.
    grip_factor = 1.05 if v.co.z < .64 else 1.60
    v.co.x = (v.co.x + .0029) * grip_factor
    v.co.y = (v.co.y - .008) * grip_factor + .035
bm.to_mesh(shaft.data)
bm.free()
shaft.matrix_world.identity()
shaft.data.materials.clear()
for mat in [steel, orange, orange_dark]:
    shaft.data.materials.append(mat)
for face in shaft.data.polygons:
    z = sum(shaft.data.vertices[i].co.z for i in face.vertices) / len(face.vertices)
    face.material_index = 2 if z > 1.035 else (1 if z > .66 else 0)
    face.use_smooth = False
shaft['derived_from'] = 'Assets/Models/Models New/Shovel/Shovel_1.fbx; original shaft connected component'

# Five cross sections: open cutting mouth, curved floor, tall flared sides.
rows = [(-.08, .170, -.010, .180, -.018),
        (-.043, .182, .000, .203, -.060),
        (.175, .155, .012, .193, -.143),
        (.275, .120, .030, .158, -.158),
        (.325, .103, .043, .136, -.155)]
columns = [-1, -.78, 0, .78, 1]
vertices = []
for z, w, y, outer_w, rim_y in rows:
    for u in columns:
        vertices.append((w*u, y - .019*abs(u)**2, z))
faces = []
for r in range(len(rows)-1):
    for j in range(4):
        a = r*5+j
        faces.append((a,a+1,a+6,a+5))
left_rim, right_rim = [], []
for r,(z,w,y,outer_w,rim_y) in enumerate(rows):
    left_rim.append(len(vertices));vertices.append((-outer_w,rim_y,z))
    right_rim.append(len(vertices));vertices.append((outer_w,rim_y,z))
for r in range(4):
    faces.append((r*5,(r+1)*5,left_rim[r+1],left_rim[r]))
    faces.append((r*5+4,right_rim[r],right_rim[r+1],(r+1)*5+4))
# Closed rear wall, with chamfered top corners.
back = []
for u in columns:
    back.append(len(vertices))
    vertices.append((.136*u,-.155,.353 + .017*(1-abs(u))))
for j in range(4):
    faces.append((20+j,20+j+1,back[j+1],back[j]))
faces += [(20,back[0],left_rim[-1]),(24,right_rim[-1],back[-1])]
scoop = mesh_object('Shovel_Grain_DeepScoop', vertices, faces, steel)
solid = scoop.modifiers.new('5 mm folded steel', 'SOLIDIFY')
solid.thickness = .005
solid.offset = 0
solid.use_even_offset = True
bevel(scoop,.0022,2)
scoop['design'] = 'Wide grain scoop, open front, flared side walls and closed raised back, based on supplied reference'

def tube(name, points, radius, mat, sides=6):
    verts, faces = [], []
    for i,point in enumerate(points):
        p = Vector(point)
        tangent = Vector(points[min(i+1,len(points)-1)]) - Vector(points[max(i-1,0)])
        tangent.normalize()
        axis = tangent.cross(Vector((0,1,0))).normalized()
        other = tangent.cross(axis).normalized()
        for j in range(sides):
            a=2*math.pi*j/sides
            verts.append(tuple(p+radius*(axis*math.cos(a)+other*math.sin(a))))
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    return mesh_object(name,verts,faces,mat)

# A continuous folded rim catches highlights along the scoop silhouette.
rim_path = [vertices[i] for i in left_rim] + [vertices[i] for i in back] + [vertices[i] for i in reversed(right_rim)]
rim = tube('Shovel_Grain_FoldedRim',rim_path,.0034,edge)
cutting = tube('Shovel_Grain_CuttingEdge',[(-.170,-.012,-.081),(0,-.010,-.083),(.170,-.012,-.081)],.0040,edge,6)

# Thick socket behind the scoop transfers the load into the reused shaft.
socket = tube('Shovel_Grain_Socket',[(0,.036,.265),(0,.036,.315),(0,.035,.397)],.032,steel,8)
bevel(socket,.001,1)

def rivet(name, x, y, z):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=1, location=(x,y,z))
    o=bpy.context.object
    o.name=name
    for c in list(o.users_collection):c.objects.unlink(o)
    asset.objects.link(o)
    o.scale=(.005,.0025,.005)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(edge)
    return o

rivet('Shovel_Grain_SocketRivet',0,.002,.385)

# One root at the same working origin as the source for reliable Unity import.
root=bpy.data.objects.new('Shovel_Grain',None)
asset.objects.link(root)
root.empty_display_size=.08
root['source_asset']='Shovel_1.fbx'
root['reference']='User supplied farm grain scoop screenshot'
for o in list(asset.objects):
    if o!=root:o.parent=root

# UVs for editable and exportable geometry, preserving the shaft's original UVs.
for o in list(asset.objects):
    if o.type!='MESH' or o==shaft:continue
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025)
    bpy.ops.object.mode_set(mode='OBJECT')

bpy.context.view_layer.update()
cam=scene.camera
cam.location=(.80,-1.8,.95)
q=(Vector((0,0,.49))-cam.location).to_track_quat('-Z','Y')
cam.rotation_euler=(q @ Quaternion((0,0,1),math.radians(142))).to_euler()
cam.data.ortho_scale=1.65
scene.render.resolution_x=1300
scene.render.resolution_y=1000
scene.cycles.samples=48
scene.render.film_transparent=False
scene.world.color=(.085,.085,.085)
scene.view_settings.look='AgX - Medium High Contrast'
scene.view_settings.exposure=-.35
scene.render.filepath=r'F:\Projects\freehangar\ArtSource\Shovel_Grain\Shovel_Grain_Preview.png'
bpy.ops.object.select_all(action='DESELECT')
for o in asset.objects:o.select_set(True)
bpy.context.view_layer.objects.active=scoop
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.shading.type='MATERIAL'

bpy.ops.export_scene.fbx(filepath=r'F:\Projects\freehangar\Assets\Models\Models New\Shovel\Shovel_Grain.fbx',use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_UNITS',path_mode='AUTO')
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=r'F:\Projects\freehangar\ArtSource\Shovel_Grain\Shovel_Grain.blend')
bpy.ops.render.render(write_still=True)
result={'blend':bpy.data.filepath,'preview':scene.render.filepath,'parts':[o.name for o in asset.objects]}
