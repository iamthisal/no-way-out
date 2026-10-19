"""Build the museum's rigged low-poly guard hound. Run with Blender --background.

The mesh, palette, rig and idle action are generated locally, without downloads.
Blender coordinates: +Y is the muzzle direction, +Z is up; units are metres.
"""
import bpy
import json
import math
from mathutils import Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Models/Hound"
PREVIEW = ROOT / "Temp/HoundVerification"
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30
scene.frame_start = 0
scene.frame_end = 72

PALETTE = {
    "Hound_Tan": ((0.40, 0.235, 0.115, 1), 0.82),
    "Hound_TanLight": ((0.58, 0.38, 0.19, 1), 0.80),
    "Hound_Saddle": ((0.095, 0.062, 0.043, 1), 0.88),
    "Hound_Black": ((0.025, 0.022, 0.020, 1), 0.40),
    "Hound_Amber": ((0.66, 0.32, 0.065, 1), 0.28),
    "Hound_Glint": ((0.78, 0.80, 0.72, 1), 0.22),
    "Hound_Collar": ((0.075, 0.105, 0.080, 1), 0.75),
    "Hound_Brass": ((0.40, 0.31, 0.16, 1), 0.32),
}
materials = {}
for name, (color, roughness) in PALETTE.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.5 if name == "Hound_Brass" else 0.0
    materials[name] = mat

def ellipsoid(name, position, scale, material="Hound_Tan", subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[material])
    return obj

def tube(name, points, radii, material="Hound_Tan", sides=10):
    verts, faces = [], []
    points = [Vector(p) for p in points]
    for i, point in enumerate(points):
        direction = (points[min(i+1,len(points)-1)] - points[max(0,i-1)]).normalized()
        axis = Vector((1,0,0))
        if abs(direction.dot(axis)) > 0.9:
            axis = Vector((0,1,0))
        right = (axis - direction * direction.dot(axis)).normalized()
        up = direction.cross(right).normalized()
        rx, ry = radii[i] if isinstance(radii[i], tuple) else (radii[i], radii[i])
        for j in range(sides):
            angle = j * 2 * math.pi / sides
            verts.append(tuple(point + right * math.cos(angle) * rx + up * math.sin(angle) * ry))
    faces.append(tuple(reversed(range(sides))))
    for i in range(len(points)-1):
        for j in range(sides):
            a, b = i*sides+j, i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(range((len(points)-1)*sides,len(points)*sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(materials[material])
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj

def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj

parts = []
for name,pos,scale in [
    ("Ribcage",(0,-0.07,0.67),(0.22,0.49,0.25)),
    ("Chest",(0,0.24,0.68),(0.21,0.25,0.29)),
    ("Loin",(0,-0.37,0.65),(0.175,0.25,0.21)),
    ("Rump",(0,-0.51,0.65),(0.20,0.22,0.23)),
    ("Skull",(0,0.61,0.995),(0.137,0.18,0.15)),
    ("Cheeks",(0,0.70,0.948),(0.13,0.15,0.115)),
    ("LowerJaw",(0,0.85,0.868),(0.087,0.18,0.05)),
]: parts.append(ellipsoid(name,pos,scale))
parts.append(tube("Neck",[(0,0.30,0.67),(0,0.40,0.84),(0,0.54,0.99)],[(0.17,0.17),(0.155,0.16),(0.125,0.13)]))
parts.append(tube("Muzzle",[(0,0.70,0.945),(0,0.85,0.925),(0,0.98,0.91)],[(0.11,0.092),(0.085,0.075),(0.066,0.055)]))
leg_segments = {}
for side, sign in [("L",1),("R",-1)]:
    x = sign * 0.17
    front = [(x,0.30,0.67),(x,0.29,0.39),(x,0.33,0.13),(x,0.44,0.075)]
    hind = [(x,-0.48,0.66),(sign*0.19,-0.31,0.40),(sign*0.19,-0.57,0.21),(sign*0.19,-0.49,0.075)]
    parts.append(ellipsoid("Shoulder_"+side,(x,0.27,0.65),(0.10,0.16,0.22)))
    parts.append(tube("Foreleg_"+side,front[:3],[0.073,0.047,0.034]))
    parts.append(ellipsoid("FrontPaw_"+side,(x,0.40,0.065),(0.065,0.115,0.055)))
    parts.append(ellipsoid("Thigh_"+side,(x,-0.45,0.56),(0.11,0.17,0.22)))
    parts.append(tube("Hindleg_"+side,hind[:3],[0.095,0.065,0.037]))
    parts.append(tube("Hock_"+side,hind[2:],[0.038,0.033]))
    parts.append(ellipsoid("HindPaw_"+side,(sign*0.19,-0.46,0.06),(0.063,0.105,0.05)))
    for prefix, points in [("Front",front),("Hind",hind)]:
        for suffix, a, b in zip(["Upper","Lower","Paw"],points[:-1],points[1:]):
            leg_segments[prefix+suffix+"_"+side] = (Vector(a),Vector(b))
body = join(parts,"Hound_Mesh")
remesh = body.modifiers.new("Connected anatomy", "REMESH")
remesh.mode = "VOXEL"
remesh.voxel_size = 0.022
remesh.use_smooth_shade = False
bpy.ops.object.modifier_apply(modifier=remesh.name)
smooth = body.modifiers.new("Soften anatomical joins", "SMOOTH")
smooth.factor = 0.7
smooth.iterations = 3
bpy.ops.object.modifier_apply(modifier=smooth.name)
decimate = body.modifiers.new("Low-poly silhouette", "DECIMATE")
decimate.ratio = 0.23
bpy.ops.object.modifier_apply(modifier=decimate.name)
body.data.materials.clear()
for key in ["Hound_Tan","Hound_TanLight","Hound_Saddle","Hound_Black"]: body.data.materials.append(materials[key])
for poly in body.data.polygons:
    p = sum((body.data.vertices[v].co for v in poly.vertices), Vector()) / len(poly.vertices)
    poly.material_index = 0
    if -0.64 < p.y < 0.26 and p.z > 0.73 - 0.10 * abs(p.x) / 0.22:
        poly.material_index = 2
    if p.y > 0.77 and p.z > 0.855:
        poly.material_index = 3
    if (p.y > 0.26 and 0.40 < p.z < 0.72) or p.z < 0.115:
        poly.material_index = 1
    poly.use_smooth = False

# The rig is generic: named limbs/head/tail are ready for later animation work.
bpy.ops.object.armature_add(enter_editmode=True, location=(0,0,0))
rig = bpy.context.object
rig.name = "Hound_Rig"
rig.data.name = "Hound_Skeleton"
edit = rig.data.edit_bones
edit.remove(edit[0])
bones = {}
segments = {}
def bone(name,a,b,parent=None):
    item = edit.new(name)
    item.head, item.tail = a,b
    if parent: item.parent = bones[parent]
    bones[name] = item
    segments[name] = (Vector(a),Vector(b))
bone("Root",(0,0,0),(0,0,0.15))
bone("Hips",(0,-0.48,0.65),(0,-0.15,0.65),"Root")
bone("Spine",(0,-0.15,0.65),(0,0.22,0.72),"Hips")
bone("Chest",(0,0.22,0.72),(0,0.39,0.78),"Spine")
bone("Neck",(0,0.39,0.78),(0,0.56,0.97),"Chest")
bone("Head",(0,0.56,0.97),(0,0.97,0.94),"Neck")
bone("TailBase",(0,-0.62,0.74),(0.07,-0.93,0.70),"Hips")
bone("TailTip",(0.07,-0.93,0.70),(0.14,-1.16,0.50),"TailBase")
for side,sign in [("L",1),("R",-1)]:
    bone("Ear_"+side,(sign*0.11,0.54,1.08),(sign*0.22,0.66,0.84),"Head")
    for prefix,parent in [("Front","Chest"),("Hind","Hips")]:
        for suffix in ["Upper","Lower","Paw"]:
            name = prefix+suffix+"_"+side
            bone(name,*leg_segments[name],parent)
            parent = name
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True
for name in segments: body.vertex_groups.new(name=name)

def distance_to_segment(p,a,b):
    ab = b-a
    return (p-(a+ab*max(0,min(1,(p-a).dot(ab)/ab.length_squared)))).length

for vertex in body.data.vertices:
    p = vertex.co
    if p.z < 0.54 and abs(p.x) > 0.075:
        side = "L" if p.x > 0 else "R"
        prefix = "Front" if p.y > -0.02 else "Hind"
        candidates = [prefix+part+"_"+side for part in ["Upper","Lower","Paw"]]
    elif p.y > 0.60: candidates = ["Head","Neck"]
    elif p.y > 0.32: candidates = ["Neck","Chest"]
    else: candidates = ["Hips","Spine","Chest"]
    ranked = sorted((distance_to_segment(p,*segments[name]),name) for name in candidates)[:2]
    weights = [1/(d+0.035)**4 for d,_ in ranked]
    total = sum(weights)
    for (_,name),weight in zip(ranked,weights): body.vertex_groups[name].add([vertex.index],weight/total,"REPLACE")

details = []
def detail(obj, bone_name="Head"):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))),1.0,"REPLACE")
    details.append(obj)
    return obj

for side,sign in [("L",1),("R",-1)]:
    ear = ellipsoid("FoldedEar_"+side,(sign*0.17,0.595,0.99),(0.045,0.10,0.15),"Hound_Saddle")
    ear.rotation_euler.y = -sign*0.20
    detail(ear,"Ear_"+side)
    detail(ellipsoid("EyeRim_"+side,(sign*0.119,0.713,1.025),(0.026,0.027,0.025),"Hound_Black"))
    detail(ellipsoid("AmberEye_"+side,(sign*0.124,0.730,1.027),(0.016,0.014,0.015),"Hound_Amber"))
    detail(ellipsoid("Pupil_"+side,(sign*0.125,0.742,1.028),(0.006,0.004,0.010),"Hound_Black"))
    detail(ellipsoid("EyeGlint_"+side,(sign*0.120,0.745,1.033),(0.003,0.002,0.003),"Hound_Glint",1))
    detail(ellipsoid("Brow_"+side,(sign*0.112,0.710,1.057),(0.039,0.026,0.017),"Hound_TanLight"))
    detail(tube("Mouth_"+side,[(sign*0.082,0.76,0.872),(sign*0.079,0.88,0.865),(sign*0.055,0.985,0.882)],
        [0.0045,0.004,0.003],"Hound_Black",6))
detail(ellipsoid("Nose",(0,1.007,0.916),(0.071,0.036,0.050),"Hound_Black"))
detail(tube("Tail",[(0,-0.61,0.74),(0.035,-0.83,0.76),(0.085,-1.02,0.64),(0.14,-1.16,0.50)],
    [0.065,0.05,0.032,0.008],"Hound_Saddle",10),"TailBase")
# Give the tail's distal vertices a second deform group.
tail = details[-1]
tail_group = tail.vertex_groups.new(name="TailTip")
for vertex in tail.data.vertices:
    amount = max(0,min(1,(-vertex.co.y-0.81)/0.30))
    tail.vertex_groups["TailBase"].add([vertex.index],1-amount,"REPLACE")
    tail_group.add([vertex.index],amount,"REPLACE")

axis = Vector((0,0.63,0.77)).normalized()
right = Vector((1,0,0))
forward = axis.cross(right).normalized()
center = Vector((0,0.424,0.835))
verts, faces = [], []
for longitudinal in [-1,1]:
    for radial in [0.96,1.02]:
        for j in range(20):
            angle = j*math.tau/20
            verts.append(tuple(center+axis*longitudinal*0.028 + right*math.cos(angle)*0.174*radial + forward*math.sin(angle)*0.150*radial))
for ring in range(4):
    next_ring = {0:2,2:3,3:1,1:0}[ring]
    for j in range(20): faces.append((ring*20+j,ring*20+(j+1)%20,next_ring*20+(j+1)%20,next_ring*20+j))
mesh = bpy.data.meshes.new("CollarBand")
mesh.from_pydata(verts,[],faces)
mesh.materials.append(materials["Hound_Collar"])
collar = bpy.data.objects.new("LeatherCollar",mesh)
scene.collection.objects.link(collar)
detail(collar,"Neck")
detail(ellipsoid("CollarTag",(0,0.573,0.705),(0.029,0.012,0.033),"Hound_Brass"),"Neck")
detail(tube("TagRing",[(0,0.557,0.755),(0,0.572,0.730)],[0.008,0.008],"Hound_Brass",8),"Neck")
body = join([body]+details,"Hound_Mesh")
body.parent = rig
modifier = body.modifiers.new("Hound skin", "ARMATURE")
modifier.object = rig
for polygon in body.data.polygons: polygon.use_smooth = False

for pose in rig.pose.bones: pose.rotation_mode = "XYZ"
for frame in range(0,73,6):
    phase = frame/72*math.tau
    chest = rig.pose.bones["Chest"]
    chest.scale = (1+0.012*math.sin(phase),1,1+0.010*math.sin(phase))
    chest.keyframe_insert(data_path="scale",frame=frame)
    head = rig.pose.bones["Head"]
    head.rotation_euler = (0.006*math.sin(phase),0,0.014*math.sin(phase))
    head.keyframe_insert(data_path="rotation_euler",frame=frame)
    for name,amplitude,offset in [("TailBase",0.10,0),("TailTip",0.13,0.4)]:
        pose = rig.pose.bones[name]
        pose.rotation_euler = (0,0,amplitude*math.sin(phase+offset))
        pose.keyframe_insert(data_path="rotation_euler",frame=frame)
    for side in ["L","R"]:
        pose = rig.pose.bones["Ear_"+side]
        pose.rotation_euler = (0,0,0.008*math.sin(phase))
        pose.keyframe_insert(data_path="rotation_euler",frame=frame)
rig.animation_data.action.name = "Hound_Idle"
scene.frame_set(0)

# Export only the character; presentation cameras/lights never enter Unity.
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUT/"MuseumHound.fbx"),use_selection=True,
    object_types={"ARMATURE","MESH"},axis_forward="-Z",axis_up="Y",global_scale=1,
    apply_unit_scale=True,add_leaf_bones=False,use_armature_deform_only=True,
    bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0.0)

# Save an editable source beside the generator, outside Unity's import directory.
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Blender/Hound/MuseumHound.blend"))
triangles = sum(len(p.vertices)-2 for p in body.data.polygons)
stats = {"vertices":len(body.data.vertices),"triangles":triangles,"bones":len(rig.data.bones),
    "materials":list(PALETTE),"idle_seconds":2.4,"forward":"Blender +Y; Unity import checked by builder"}
(PREVIEW/"model-stats.json").write_text(json.dumps(stats,indent=2))
print("HOUND_STATS",json.dumps(stats))

ground_mat = bpy.data.materials.new("PreviewGround")
ground_mat.diffuse_color = (0.12,0.145,0.14,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,0))
ground = bpy.context.object
ground.data.materials.append(ground_mat)
for name,pos,energy,size in [("Key",(3,2,5),450,4),("Fill",(-3,2,3),240,4),("Rim",(0,-3,4),500,3)]:
    data = bpy.data.lights.new(name,"AREA")
    data.energy, data.shape, data.size = energy,"DISK",size
    light = bpy.data.objects.new(name,data)
    scene.collection.objects.link(light)
    light.location = pos
    light.rotation_euler = (Vector((0,0,0.6))-light.location).to_track_quat("-Z","Y").to_euler()
camera_data = bpy.data.cameras.new("HoundPreview")
camera = bpy.data.objects.new("HoundPreview",camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.render.resolution_x = 1000
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.world.color = (0.13,0.13,0.13)
for name,pos,target in [("hound-three-quarter",(2.3,3.0,1.8),(0,0,0.6)),
                        ("hound-side",(3.0,0.2,1.1),(0,-0.05,0.6)),
                        ("hound-front",(0,3.3,1.35),(0,0.25,0.68))]:
    camera.location = pos
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat("-Z","Y").to_euler()
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.7
    scene.render.filepath = str(PREVIEW/(name+".png"))
    bpy.ops.render.render(write_still=True)
