"""Build the Natural History skeleton archer. Run with Blender --background.

Every bone is a separate rigid piece parented to one rig bone, so Unity can
detach the pieces with physics when he is defeated, or gather them from a pile.
The mesh, palette, rig and animation takes are generated locally, without downloads.
Blender coordinates: +Y is forward, +Z is up, +X is the archer's right; metres.
Pass `-- --no-render` to skip the preview images, or `-- --quick` for fast Workbench previews.
"""
import bpy
import json
import math
import sys
from mathutils import Euler, Matrix, Quaternion, Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Models/SkeletonArcher"
PREVIEW = ROOT / "Temp/SkeletonArcherVerification"
RENDER = "--no-render" not in sys.argv
QUICK = "--quick" in sys.argv  # fast Workbench previews while iterating
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30

# name: (linear colour, roughness, metallic, emission strength)
PALETTE = {
    "Skeleton_Bone": ((0.55, 0.47, 0.33, 1), 0.72, 0.0, 0.0),
    "Skeleton_BoneDark": ((0.30, 0.235, 0.15, 1), 0.80, 0.0, 0.0),
    "Skeleton_Socket": ((0.018, 0.014, 0.011, 1), 0.90, 0.0, 0.0),
    "Skeleton_EyeGlow": ((0.25, 0.85, 0.95, 1), 0.30, 0.0, 4.0),
    "Skeleton_Brass": ((0.40, 0.31, 0.16, 1), 0.38, 0.5, 0.0),
    "Skeleton_Tag": ((0.62, 0.58, 0.46, 1), 0.85, 0.0, 0.0),
    "Skeleton_Wood": ((0.10, 0.055, 0.03, 1), 0.60, 0.0, 0.0),
    "Skeleton_Shaft": ((0.36, 0.24, 0.12, 1), 0.70, 0.0, 0.0),
    "Skeleton_Leather": ((0.13, 0.07, 0.035, 1), 0.75, 0.0, 0.0),
    "Skeleton_String": ((0.55, 0.50, 0.40, 1), 0.80, 0.0, 0.0),
    "Skeleton_Iron": ((0.09, 0.09, 0.10, 1), 0.40, 0.7, 0.0),
    "Skeleton_Fletch": ((0.38, 0.06, 0.04, 1), 0.85, 0.0, 0.0),
}
materials = {}
for name, (color, roughness, metallic, emission) in PALETTE.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission > 0:
        shader.inputs["Emission Color"].default_value = color
        shader.inputs["Emission Strength"].default_value = emission
    materials[name] = mat

# Animation takes share one timeline; Unity splits it with these frame ranges.
CLIPS = [("Idle", 0, 60, True), ("Walk", 70, 102, True), ("Run", 110, 134, True),
         ("Crouch", 140, 180, True), ("Draw", 190, 211, False), ("AimHold", 220, 250, True),
         ("Release", 260, 269, False), ("Nock", 280, 298, False), ("Hit", 310, 320, False)]
scene.frame_start = 0
scene.frame_end = CLIPS[-1][2]

def V(*values): return Vector(values)

def mirror(p): return (-p[0], p[1], p[2])

# ---------------------------------------------------------------- rig
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
rig = bpy.context.object
rig.name = "SkeletonArcher_Rig"
rig.data.name = "SkeletonArcher_Skeleton"
edit = rig.data.edit_bones
edit.remove(edit[0])
BONES = [
    ("Root", (0, 0, 0), (0, 0.12, 0), None),
    ("Hips", (0, 0, 0.96), (0, 0, 1.07), "Root"),
    ("Spine", (0, -0.01, 1.07), (0, -0.02, 1.25), "Hips"),
    ("Chest", (0, -0.02, 1.25), (0, -0.01, 1.50), "Spine"),
    ("Neck", (0, -0.01, 1.50), (0, 0.0, 1.61), "Chest"),
    ("Head", (0, 0.0, 1.61), (0, 0.0, 1.81), "Neck"),
    ("Jaw", (0, 0.015, 1.635), (0, 0.10, 1.592), "Head"),
]
LIMBS = [
    ("Clavicle", (-0.02, 0.04, 1.47), (-0.185, 0.0, 1.455), "Chest"),
    ("UpperArm", (-0.19, -0.01, 1.45), (-0.235, -0.015, 1.15), "Clavicle"),
    ("Forearm", (-0.235, -0.015, 1.15), (-0.265, 0.01, 0.905), "UpperArm"),
    ("Hand", (-0.265, 0.01, 0.905), (-0.275, 0.02, 0.77), "Forearm"),
    ("Thigh", (-0.09, 0.0, 0.93), (-0.10, 0.015, 0.51), "Hips"),
    ("Shin", (-0.10, 0.015, 0.51), (-0.10, -0.01, 0.085), "Thigh"),
    ("Foot", (-0.10, -0.01, 0.085), (-0.11, 0.15, 0.02), "Shin"),
]
for side in ["L", "R"]:
    for name, head, tail, parent in LIMBS:
        if side == "R": head, tail = mirror(head), mirror(tail)
        BONES.append((name + "_" + side, head, tail, parent if parent in ("Chest", "Hips") else parent + "_" + side))
for name, head, tail, parent in BONES:
    bone = edit.new(name)
    bone.head, bone.tail = head, tail
    if parent: bone.parent = edit[parent]
    bone.use_connect = False
    bone.align_roll(V(0, 0, 1) if name in ("Root", "Jaw") or name.startswith("Foot") else V(0, 1, 0))

def fist_centre(hand):
    return edit[hand].head + (edit[hand].tail - edit[hand].head).normalized() * 0.055

grip = fist_centre("Hand_L")
draw_fist = fist_centre("Hand_R")
bow = edit.new("Bow")
bow.head, bow.tail = grip, grip + edit["Hand_L"].z_axis * 0.12
bow.parent = edit["Hand_L"]
bow.align_roll(-edit["Hand_L"].y_axis)
BOW_AXIS = edit["Hand_L"].z_axis.copy()     # vertical when the bow arm aims
STRING_SIDE = -edit["Hand_L"].y_axis.copy()  # toward the archer when aiming
BRACE = 0.14
nock = edit.new("StringNock")
nock.head = grip + STRING_SIDE * BRACE
nock.tail = nock.head + STRING_SIDE * 0.05
nock.parent = bow
arrow_bone = edit.new("Arrow")
arrow_bone.head, arrow_bone.tail = draw_fist, draw_fist + V(0, 0.12, 0)
arrow_bone.parent = edit["Hand_R"]
arrow_bone.align_roll(V(0, 0, 1))
for item in (bow, nock, arrow_bone): item.use_connect = False
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True
rest = {bone.name: bone.matrix_local.copy() for bone in rig.data.bones}

# ---------------------------------------------------------------- mesh helpers
def ellipsoid(name, position, scale, material="Skeleton_Bone", subdivisions=1, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(materials[material])
    return obj

def box(name, position, size, material="Skeleton_Bone", rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(materials[material])
    return obj

def tube(name, points, radii, material="Skeleton_Bone", sides=6, closed=False):
    points = [Vector(p) for p in points]
    if not isinstance(radii, (list, tuple)): radii = [radii] * len(points)
    verts, faces = [], []
    count = len(points)
    for i, point in enumerate(points):
        if closed: direction = (points[(i + 1) % count] - points[i - 1]).normalized()
        else: direction = (points[min(i + 1, count - 1)] - points[max(0, i - 1)]).normalized()
        axis = V(1, 0, 0) if abs(direction.x) < 0.9 else V(0, 1, 0)
        right = (axis - direction * direction.dot(axis)).normalized()
        up = direction.cross(right).normalized()
        radius = radii[i]
        for j in range(sides):
            angle = j * math.tau / sides
            verts.append(tuple(point + right * math.cos(angle) * radius + up * math.sin(angle) * radius))
    segments = count if closed else count - 1
    for i in range(segments):
        a, b = i * sides, ((i + 1) % count) * sides
        for j in range(sides): faces.append((a + j, a + (j + 1) % sides, b + (j + 1) % sides, b + j))
    if not closed:
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple(range((count - 1) * sides, count * sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(materials[material])
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj

def ring(name, centre, axis, radius, wire=0.0025, material="Skeleton_Brass"):
    axis = Vector(axis).normalized()
    side = (V(1, 0, 0) if abs(axis.x) < 0.9 else V(0, 1, 0))
    u = (side - axis * axis.dot(side)).normalized()
    w = axis.cross(u)
    pts = [Vector(centre) + (u * math.cos(a) + w * math.sin(a)) * radius for a in [i * math.tau / 10 for i in range(10)]]
    return tube(name, pts, wire, material, 4, closed=True)

def vane(name, base, direction, outward, length=0.095, height=0.016):
    d, o = Vector(direction).normalized(), Vector(outward).normalized()
    b = Vector(base)
    quad = [b, b + d * length, b + d * (length - 0.015) + o * height, b + d * 0.01 + o * height * 0.8]
    verts = [tuple(p + o * 0.004) for p in quad]
    mesh = bpy.data.meshes.new(name)
    # A single quad: Unity renders the fletching material double-sided.
    mesh.from_pydata(verts, [], [(0, 1, 2, 3)])
    mesh.materials.append(materials["Skeleton_Fletch"])
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj

def arrow_parts(prefix, nock_end, direction, length=0.80):
    d = Vector(direction).normalized()
    start = Vector(nock_end)
    side = (V(1, 0, 0) if abs(d.x) < 0.9 else V(0, 1, 0))
    u = (side - d * d.dot(side)).normalized()
    w = d.cross(u)
    parts = [tube(prefix + "Shaft", [start, start + d * (length - 0.06)], 0.0045, "Skeleton_Shaft", 5),
             tube(prefix + "Head", [start + d * (length - 0.065), start + d * length], [0.011, 0.0006], "Skeleton_Iron", 4),
             tube(prefix + "Nock", [start - d * 0.004, start + d * 0.02], 0.0055, "Skeleton_Leather", 5)]
    for k in range(3):
        angle = k * math.tau / 3
        parts.append(vane(prefix + "Vane%d" % k, start + d * 0.025, d, u * math.cos(angle) + w * math.sin(angle)))
    return parts

def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1: bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # Pivot at the piece centre so detached bones tumble naturally.
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    for polygon in obj.data.polygons: polygon.use_smooth = False
    return obj

def bone_point(bone, t):
    b = rig.data.bones[bone]
    return b.head_local.lerp(b.tail_local, t)

pieces = {}
def piece(name, bone, parts):
    obj = join(parts, "Piece_" + name)
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = world
    pieces[obj.name] = (obj, bone)
    return obj

# ---------------------------------------------------------------- skull and jaw
skull = [ellipsoid("Cranium", (0, -0.005, 1.715), (0.085, 0.105, 0.098), subdivisions=2),
         ellipsoid("Occiput", (0, -0.075, 1.695), (0.062, 0.045, 0.062)),
         ellipsoid("Maxilla", (0, 0.062, 1.664), (0.058, 0.052, 0.05)),
         ellipsoid("Brow", (0, 0.074, 1.737), (0.072, 0.026, 0.019)),
         ellipsoid("NasalCavity", (0, 0.108, 1.668), (0.013, 0.012, 0.022), "Skeleton_Socket")]
for sign in (-1, 1):
    skull += [ellipsoid("Cheek", (sign * 0.056, 0.055, 1.675), (0.024, 0.036, 0.02)),
              ellipsoid("Socket", (sign * 0.033, 0.083, 1.704), (0.026, 0.014, 0.025), "Skeleton_Socket"),
              ellipsoid("Eye", (sign * 0.033, 0.092, 1.702), (0.009, 0.007, 0.009), "Skeleton_EyeGlow")]
for k in range(8):
    angle = math.radians(-58 + k * 116 / 7)
    skull.append(box("UpperTooth", (math.sin(angle) * 0.046, 0.068 + math.cos(angle) * 0.043, 1.627),
                     (0.009, 0.006, 0.014), rotation=(0, 0, -angle)))
piece("Skull", "Head", skull)
jaw_path = [(-0.056, 0.0, 1.652), (-0.05, 0.045, 1.613), (-0.029, 0.084, 1.6), (0, 0.097, 1.597),
            (0.029, 0.084, 1.6), (0.05, 0.045, 1.613), (0.056, 0.0, 1.652)]
jaw = [tube("Mandible", jaw_path, 0.012, sides=6), ellipsoid("Chin", (0, 0.097, 1.596), (0.021, 0.012, 0.015))]
for k in range(8):
    angle = math.radians(-55 + k * 110 / 7)
    jaw.append(box("LowerTooth", (math.sin(angle) * 0.042, 0.064 + math.cos(angle) * 0.039, 1.614),
                   (0.008, 0.006, 0.012), rotation=(0, 0, -angle)))
piece("Jaw", "Jaw", jaw)

def vertebra(name, centre, radius, height, material="Skeleton_BoneDark"):
    c = Vector(centre)
    return [tube(name, [c - V(0, 0, height / 2), c + V(0, 0, height / 2)], radius, material, 8),
            box(name + "Process", c + V(0, -radius - 0.012, 0), (0.008, 0.026, height * 0.8))]

neck = []
for k, z in enumerate((1.522, 1.556, 1.59)): neck += vertebra("Cervical%d" % k, (0, -0.012, z), 0.02, 0.022)
neck.append(ring("NeckWire", (0, -0.012, 1.539), (0, 0, 1), 0.028))
piece("Neck", "Neck", neck)

# ---------------------------------------------------------------- ribcage and spine
chest = [ellipsoid("Sternum", (0, 0.108, 1.375), (0.021, 0.011, 0.088))]
for k in range(6): chest += vertebra("Thoracic%d" % k, (0, -0.072, 1.27 + k * 0.042), 0.022, 0.03)
RIB_WIDTH = [0.085, 0.11, 0.125, 0.134, 0.134, 0.124]
RIB_DEPTH = [0.075, 0.09, 0.10, 0.105, 0.105, 0.10]
RIB_END = [1.30, 1.30, 1.25, 1.2, 0.95, 0.65]
for i in range(6):
    z_back = 1.465 - i * 0.04
    for sign in (-1, 1):
        pts = []
        for s in range(9):
            t = -1.45 + (RIB_END[i] + 1.45) * s / 8
            pts.append((sign * RIB_WIDTH[i] * math.cos(t), 0.008 + RIB_DEPTH[i] * math.sin(t),
                        z_back - 0.075 * (t + 1.45) / 2.75 - 0.012 * math.cos(t)))
        chest.append(tube("Rib", pts, [0.0085] * 8 + [0.0065], sides=5))
piece("Ribcage", "Chest", chest)
spine = []
for k in range(4): spine += vertebra("Lumbar%d" % k, (0, -0.045, 1.085 + k * 0.042), 0.027, 0.03)
piece("Spine", "Spine", spine)

for side, sign in (("L", -1), ("R", 1)):
    piece("Clavicle_" + side, "Clavicle_" + side, [
        tube("Collarbone", [(sign * 0.02, 0.07, 1.476), (sign * 0.09, 0.065, 1.474), (sign * 0.17, 0.005, 1.466)], 0.009),
        ellipsoid("Scapula", (sign * 0.105, -0.095, 1.405), (0.055, 0.012, 0.072), rotation=(0, 0, -sign * 0.35)),
        ellipsoid("ScapulaSpine", (sign * 0.13, -0.08, 1.44), (0.05, 0.012, 0.01), rotation=(0, 0, -sign * 0.5))])

# ---------------------------------------------------------------- pelvis, belt and dagger
pelvis = [ellipsoid("Sacrum", (0, -0.06, 1.0), (0.04, 0.025, 0.062), "Skeleton_BoneDark"),
          tube("PubicArch", [(-0.085, 0.0, 0.975), (-0.06, 0.05, 0.935), (0, 0.075, 0.918),
                             (0.06, 0.05, 0.935), (0.085, 0.0, 0.975)], 0.014)]
for sign in (-1, 1):
    pelvis += [ellipsoid("Ilium", (sign * 0.088, 0.0, 1.03), (0.07, 0.028, 0.062), rotation=(0, sign * 0.45, sign * 0.55)),
               ellipsoid("HipSocket", (sign * 0.092, 0.005, 0.935), (0.026, 0.026, 0.026))]
piece("Pelvis", "Hips", pelvis)
belt_loop = [(math.cos(a) * 0.128, -0.005 + math.sin(a) * 0.112, 1.04) for a in [i * math.tau / 16 for i in range(16)]]
sheath_top, sheath_tip = V(0.138, 0.03, 1.02), V(0.158, 0.05, 0.80)
piece("Belt", "Hips", [tube("Band", belt_loop, 0.012, "Skeleton_Leather", 5, closed=True),
                       box("Buckle", (0, 0.11, 1.04), (0.032, 0.01, 0.028), "Skeleton_Brass"),
                       tube("Sheath", [sheath_top, sheath_top.lerp(sheath_tip, 0.6), sheath_tip],
                            [0.019, 0.016, 0.008], "Skeleton_Leather", 6)])
blade_dir = (sheath_tip - sheath_top).normalized()
piece("Dagger", "Hips", [tube("Grip", [sheath_top - blade_dir * 0.012, sheath_top - blade_dir * 0.095], 0.011, "Skeleton_Wood", 6),
                         box("Guard", sheath_top - blade_dir * 0.012, (0.062, 0.012, 0.01), "Skeleton_Brass"),
                         ellipsoid("Pommel", sheath_top - blade_dir * 0.1, (0.015, 0.015, 0.015), "Skeleton_Brass"),
                         tube("Blade", [sheath_top, sheath_top + blade_dir * 0.2], [0.013, 0.002], "Skeleton_Iron", 4)])

# ---------------------------------------------------------------- limbs
def fist(hand_bone, sign):
    head, tail = bone_point(hand_bone, 0), bone_point(hand_bone, 1)
    f = head + (tail - head).normalized() * 0.055
    outward, down = V(sign, 0, 0), V(0, 0, -1)
    parts = [ellipsoid("Carpals", head + V(0, 0, -0.012), (0.02, 0.026, 0.014))]
    for i in range(4):
        y = -0.027 + i * 0.018
        arc = [f + outward * (0.021 * math.cos(a)) + down * (0.021 * math.sin(a)) + V(0, y, 0)
               for a in [math.radians(-40 + k * 240 / 6) for k in range(7)]]
        parts.append(tube("Metacarpal", [head + V(0, y * 0.4, -0.004) + outward * 0.008, arc[0]], 0.0062, sides=5))
        parts.append(tube("Finger", arc, [0.0065] * 6 + [0.005], sides=5))
    parts.append(tube("Thumb", [head - outward * 0.012 + V(0, 0.02, -0.012), f - outward * 0.016 + V(0, 0.035, 0.012),
                                f + V(0, 0.042, 0.022), f + outward * 0.012 + V(0, 0.04, 0.02)], 0.007, sides=5))
    return parts

for side, sign in (("L", -1), ("R", 1)):
    shoulder, elbow = bone_point("UpperArm_" + side, 0), bone_point("UpperArm_" + side, 1)
    wrist = bone_point("Forearm_" + side, 1)
    piece("UpperArm_" + side, "UpperArm_" + side, [
        tube("Humerus", [shoulder, shoulder.lerp(elbow, 0.2), shoulder.lerp(elbow, 0.8), elbow], [0.019, 0.014, 0.015, 0.02]),
        ellipsoid("HumerusHead", shoulder + V(sign * 0.006, 0, 0.004), (0.027, 0.027, 0.027)),
        ellipsoid("Condyle", elbow, (0.03, 0.02, 0.018))])
    forearm = [tube("Ulna", [elbow + V(0, -0.012, 0), wrist + V(0, -0.008, 0)], [0.016, 0.009]),
               tube("Radius", [elbow + V(0, 0.01, -0.01), wrist + V(0, 0.01, 0)], [0.009, 0.015]),
               ellipsoid("Olecranon", elbow + V(0, -0.02, 0.01), (0.017, 0.017, 0.022)),
               ring("WristWire", elbow.lerp(wrist, 0.88), wrist - elbow, 0.023)]
    if side == "R":
        wire = elbow.lerp(wrist, 0.88) + V(0.022, 0, 0)
        forearm += [tube("TagWire", [wire, wire + V(0.012, 0, -0.016)], 0.0015, "Skeleton_Brass", 4),
                    box("AccessionTag", wire + V(0.016, 0, -0.04), (0.003, 0.03, 0.045), "Skeleton_Tag", rotation=(0, 0.1, 0))]
    piece("Forearm_" + side, "Forearm_" + side, forearm)
    piece("Hand_" + side, "Hand_" + side, fist("Hand_" + side, sign))
    hip, knee = bone_point("Thigh_" + side, 0), bone_point("Thigh_" + side, 1)
    ankle, toe = bone_point("Shin_" + side, 1), bone_point("Foot_" + side, 1)
    piece("Thigh_" + side, "Thigh_" + side, [
        tube("Femur", [hip, hip.lerp(knee, 0.2), hip.lerp(knee, 0.8), knee], [0.022, 0.017, 0.017, 0.025]),
        ellipsoid("FemurHead", hip + V(-sign * 0.008, 0, 0.012), (0.026, 0.026, 0.026)),
        ellipsoid("Trochanter", hip + V(sign * 0.022, -0.005, -0.03), (0.018, 0.02, 0.026)),
        ellipsoid("KneeCondyle", knee + V(0, -0.004, 0.012), (0.034, 0.028, 0.022))])
    piece("Shin_" + side, "Shin_" + side, [
        tube("Tibia", [knee, knee.lerp(ankle, 0.2), ankle], [0.024, 0.017, 0.02]),
        tube("Fibula", [knee + V(sign * 0.022, -0.012, -0.03), ankle + V(sign * 0.02, -0.006, 0.01)], [0.008, 0.007]),
        ellipsoid("Patella", knee + V(0, 0.03, 0.005), (0.021, 0.01, 0.025)),
        ring("AnkleWire", knee.lerp(ankle, 0.9), ankle - knee, 0.025)])
    foot = [ellipsoid("Tarsals", ankle + V(0, 0.01, -0.03), (0.03, 0.05, 0.022)),
            ellipsoid("Heel", ankle + V(0, -0.035, -0.055), (0.022, 0.03, 0.022))]
    for k in range(5):
        x = sign * (-0.025 + k * 0.0125)
        foot.append(tube("Metatarsal", [ankle + V(x * 0.6, 0.04, -0.045), toe + V(x, -0.025, -0.002), toe + V(x, 0.005, -0.008)],
                         [0.0065, 0.0055, 0.0045], sides=5))
    piece("Foot_" + side, "Foot_" + side, foot)

# ---------------------------------------------------------------- bow, string, arrows and quiver
def bow_point(u, half=0.70):
    offset = 0.20 * abs(u) ** 1.7 - 0.02
    if abs(u) > 0.82: offset -= 0.04 * ((abs(u) - 0.82) / 0.18) ** 2
    return grip + BOW_AXIS * (u * half) + STRING_SIDE * offset

samples = [-1 + 2 * k / 16 for k in range(17)]
bow_radii = [0.006 + 0.011 * (1 - abs(u)) ** 0.8 for u in samples]
top_tip, bottom_tip = bow_point(1), bow_point(-1)
piece("Bow", "Bow", [tube("Limbs", [bow_point(u) for u in samples], bow_radii, "Skeleton_Wood", 6),
                     tube("GripWrap", [bow_point(-0.08), bow_point(0), bow_point(0.08)], 0.0205, "Skeleton_Leather", 6),
                     ring("TopNock", top_tip, BOW_AXIS, 0.008, 0.002), ring("BottomNock", bottom_tip, BOW_AXIS, 0.008, 0.002)])
string_rest = grip + STRING_SIDE * (0.20 - 0.02 - 0.04)
string = tube("BowString", [top_tip, string_rest, bottom_tip], 0.0035, "Skeleton_String", 4)
for group in ("Bow", "StringNock"): string.vertex_groups.new(name=group)
for vertex in string.data.vertices:
    ring_index = vertex.index // 4
    string.vertex_groups["StringNock" if ring_index == 1 else "Bow"].add([vertex.index], 1.0, "REPLACE")
string.parent = rig
modifier = string.modifiers.new("Bow string", "ARMATURE")
modifier.object = rig
# The nock bone must sit exactly on the braced string's midpoint.
assert (string_rest - rig.data.bones["StringNock"].head_local).length < 1e-4

piece("NockedArrow", "Arrow", arrow_parts("Nocked", draw_fist, V(0, 1, 0)))
quiver_bottom, quiver_top = V(-0.085, -0.145, 1.13), V(0.10, -0.15, 1.56)
quiver_dir = (quiver_top - quiver_bottom).normalized()
quiver = [tube("QuiverBody", [quiver_bottom, quiver_bottom.lerp(quiver_top, 0.5), quiver_top], [0.04, 0.046, 0.05], "Skeleton_Leather", 8),
          ring("QuiverRim", quiver_top, quiver_dir, 0.051, 0.004),
          tube("Strap", [(0.10, -0.12, 1.52), (0.125, 0.0, 1.5), (0.06, 0.13, 1.39), (-0.06, 0.135, 1.23),
                         (-0.11, 0.03, 1.12), (-0.085, -0.12, 1.13)], 0.0075, "Skeleton_Leather", 5)]
for k, (dx, dy) in enumerate(((-0.018, 0.012), (0.016, 0.014), (0.0, -0.018), (0.02, -0.01))):
    tip = quiver_top + V(dx, dy, 0) + quiver_dir * 0.13
    quiver += arrow_parts("Quivered%d" % k, tip, -quiver_dir, 0.38)
piece("Quiver", "Chest", quiver)

# ---------------------------------------------------------------- posing
P = rig.pose.bones
for pose in P: pose.rotation_mode = "QUATERNION"
LOCATED = ("Hips", "StringNock", "Arrow")

def upd(): bpy.context.view_layer.update()

def reset_pose():
    for pose in P:
        pose.rotation_quaternion = (1, 0, 0, 0)
        pose.location = (0, 0, 0)
    upd()

def fk(name, x=0.0, y=0.0, z=0.0):
    """Rotate in armature axes relative to the parent: +X swings a hanging limb forward."""
    b = rest[name].to_quaternion()
    r = Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ").to_quaternion()
    P[name].rotation_quaternion = b.inverted() @ r @ b

def shift(name, offset):
    P[name].location = rest[name].to_3x3().inverted() @ Vector(offset)

def set_matrix(name, head, y_dir, z_hint):
    y = Vector(y_dir).normalized()
    z = Vector(z_hint) - y * y.dot(Vector(z_hint))
    z = z.normalized() if z.length > 1e-6 else y.orthogonal().normalized()
    x = y.cross(z)
    P[name].matrix = Matrix(((x.x, y.x, z.x, head.x), (x.y, y.y, z.y, head.y), (x.z, y.z, z.z, head.z), (0, 0, 0, 1)))
    upd()

def aim(name, y_dir, z_hint): set_matrix(name, P[name].head.copy(), y_dir, z_hint)

def place(name, head):
    m = P[name].matrix.copy()
    m.translation = head
    P[name].matrix = m
    upd()

def ik2(upper, lower, target, pole, z_sign):
    a = P[upper].head.copy()
    l1, l2 = rig.data.bones[upper].length, rig.data.bones[lower].length
    d = target - a
    dist = min(max(d.length, abs(l1 - l2) + 1e-3), (l1 + l2) * 0.999)
    dn = d.normalized()
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    p = pole - a
    p = p - dn * dn.dot(p)
    p = p.normalized() if p.length > 1e-6 else V(0, 0, 1)
    elbow = a + dn * (l1 * cos_a) + p * (l1 * math.sqrt(max(0.0, 1 - cos_a * cos_a)))
    aim(upper, elbow - a, p * z_sign)
    aim(lower, a + dn * dist - elbow, p * z_sign)

def chest_point(rest_point):
    return P["Chest"].matrix @ rest["Chest"].inverted() @ Vector(rest_point)

def ease(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)

def lerp(a, b, t): return a.lerp(b, t) if isinstance(a, Vector) else a + (b - a) * t

def carry_arms(swing_l=0.0, swing_r=0.0, bend_l=14.0, bend_r=16.0):
    fk("UpperArm_L", x=swing_l, y=2); fk("Forearm_L", x=bend_l); fk("Hand_L", x=34)
    fk("UpperArm_R", x=swing_r, y=-3); fk("Forearm_R", x=bend_r); fk("Hand_R", x=10)

def pose_idle(t):
    phase = t * math.tau
    fk("Spine", x=-2); fk("Chest", x=-4 + 1.2 * math.sin(phase))
    fk("Neck", x=-3); fk("Head", x=-6 + 1.5 * math.sin(2 * phase), z=5 * math.sin(phase))
    fk("Jaw", x=-2 - 4 * max(0.0, math.sin(2 * phase)) ** 3)
    carry_arms(1.5 * math.sin(phase), -1.5 * math.sin(phase))
    for side, sign in (("L", 1), ("R", -1)):
        fk("Thigh_" + side, x=2, y=3 * sign); fk("Shin_" + side, x=-4); fk("Foot_" + side, x=2)

def pose_cycle(t, run):
    phase = t * math.tau
    swing, knee, base, lean, arm = (34, 95, 14, -14, 26) if run else (21, 55, 5, -4, 15)
    fk("Hips", z=4 * math.sin(phase)); fk("Spine", x=lean * 0.5); fk("Chest", x=lean * 0.5, z=-5 * math.sin(phase))
    fk("Neck", x=-lean * 0.4); fk("Head", x=-6 - lean * 0.3)
    fk("Jaw", x=-3 - 3 * max(0.0, math.sin(2 * phase)))
    for side, offset in (("L", 0.0), ("R", math.pi)):
        p = phase + offset
        # Thigh forward = foot contact; the knee folds most just after toe-off.
        swing_bend = max(0.0, math.cos(p + math.pi / 6)) ** 1.5
        thigh = swing * math.sin(p)
        shin = -(base + knee * swing_bend)
        fk("Thigh_" + side, x=thigh); fk("Shin_" + side, x=shin); fk("Foot_" + side, x=-(thigh + shin) - 12 * swing_bend)
    if run:
        fk("UpperArm_L", x=20 + 8 * math.sin(phase)); fk("Forearm_L", x=55); fk("Hand_L", x=20)
        fk("UpperArm_R", x=arm * math.sin(phase), y=-6); fk("Forearm_R", x=70); fk("Hand_R", x=10)
    else: carry_arms(-8 * math.sin(phase), arm * math.sin(phase))
    upd()
    # Lower the pelvis until the supporting foot rests on the floor; running adds a short flight.
    lowest = min(P["Foot_L"].head.z, P["Foot_R"].head.z)
    flight = 0.03 * max(0.0, math.cos(2 * phase)) if run else 0.0
    shift("Hips", (0, 0, rest["Foot_L"].translation.z - lowest + flight))

def plant_feet(ankles):
    for side, target in ankles.items():
        hip = P["Thigh_" + side].head
        ik2("Thigh_" + side, "Shin_" + side, Vector(target), hip + V(0.12 if side == "R" else -0.12, 0.6, 0), 1)
        rest_dir = rest["Foot_" + side].to_3x3() @ V(0, 1, 0)
        aim("Foot_" + side, rest_dir, V(0, 0, 1))

def pose_crouch(t):
    phase = t * math.tau
    shift("Hips", (0, -0.03, -0.40 + 0.006 * math.sin(phase)))
    fk("Spine", x=-24); fk("Chest", x=-10 + 1.5 * math.sin(phase)); fk("Neck", x=12)
    fk("Head", x=16, z=9 * math.sin(phase)); fk("Jaw", x=-2)
    fk("UpperArm_L", x=38, y=6); fk("Forearm_L", x=55); fk("Hand_L", x=8)
    fk("UpperArm_R", x=30, y=-4); fk("Forearm_R", x=58); fk("Hand_R", x=12)
    upd()
    plant_feet({"L": (-0.13, 0.07, 0.085), "R": (0.13, -0.10, 0.085)})

def stance(lean=0.0):
    fk("Hips", z=-35); fk("Spine", x=-2, z=-10); fk("Chest", x=2 + lean, z=-15)
    fk("Neck", z=25); fk("Head", x=-4, y=-6, z=33); fk("Jaw", x=-1)
    fk("Thigh_L", x=8, y=5); fk("Shin_L", x=-6); fk("Thigh_R", x=-8, y=-5); fk("Shin_R", x=-4); fk("Foot_R", x=6)
    upd()

def bow_arm(reach):
    """reach 0 = bow held close while nocking, 1 = fully extended at the target."""
    sh = P["UpperArm_L"].head.copy()
    ready = V(0.05, sh.y + 0.34, sh.z - 0.10)
    full = V(0.02, sh.y + 0.585, sh.z + 0.035)
    g = ready.lerp(full, reach)
    ik2("UpperArm_L", "Forearm_L", g - V(0, 0.055, 0), sh + V(-0.3, 0.15, -0.25), -1)
    aim("Hand_L", V(0, 1, 0), V(0, 0, 1))
    return g, full

def string_hand(point, wrist_offset, elbow_pole=V(0.45, -0.35, 0.08)):
    sh = P["UpperArm_R"].head.copy()
    ik2("UpperArm_R", "Forearm_R", point + wrist_offset, sh + elbow_pole, -1)
    aim("Hand_R", point - P["Hand_R"].head, V(0, 0, 1))

DRAW_WRIST = V(0.035, -0.05, -0.005)

def anchor(full): return V(0.025, full.y - 0.70, full.z + 0.05)

def pose_draw(t):
    stance()
    e = ease(t)
    g, full = bow_arm(e)
    rest_nock = P["StringNock"].head.copy()
    a = rest_nock.lerp(anchor(full), e)
    string_hand(a, DRAW_WRIST)
    set_matrix("Arrow", a, g - a, V(0, 0, 1))
    place("StringNock", a)

def pose_aim(t):
    stance()
    tremble = V(0.002 * math.sin(t * math.tau * 3), 0, 0.002 * math.sin(t * math.tau * 2))
    g, full = bow_arm(1.0)
    a = anchor(full) + tremble
    string_hand(a, DRAW_WRIST)
    set_matrix("Arrow", a, g - a, V(0, 0, 1))
    place("StringNock", a)

FOLLOW = V(0.07, -0.12, 0.03)

def pose_release(t):
    stance()
    g, full = bow_arm(1.0 - 0.06 * ease(t))
    rest_nock = P["StringNock"].head.copy()
    a = anchor(full)
    if t < 0.12: place("StringNock", a.lerp(rest_nock, t / 0.12))
    string_hand(a + FOLLOW * ease(t * 1.6), DRAW_WRIST)

def pose_nock(t):
    stance()
    g, full = bow_arm(0.94 * (1 - ease((t - 0.35) / 0.65)))
    rest_nock = P["StringNock"].head.copy()
    follow = anchor(full) + FOLLOW
    quiver = chest_point(quiver_top + quiver_dir * 0.03)
    mid = P["UpperArm_R"].head + V(-0.04, 0.24, 0.06)
    reach = V(0.0, -0.02, 0.075)
    if t < 0.4:
        s = ease(t / 0.4)
        point, wrist, pole = follow.lerp(quiver, s), DRAW_WRIST.lerp(reach, s), V(0.35, -0.2, 0.35)
    elif t < 0.7:
        s = ease((t - 0.4) / 0.3)
        point, wrist, pole = quiver.lerp(mid, s), reach.lerp(DRAW_WRIST, s), V(0.4, -0.25, 0.25)
    else:
        s = ease((t - 0.7) / 0.3)
        point, wrist, pole = mid.lerp(rest_nock, s), DRAW_WRIST, V(0.45, -0.35, 0.08)
    string_hand(point, wrist, pole)
    if t >= 0.4:
        down = -chest_point(quiver_top) + chest_point(quiver_bottom)
        toward = g - point
        direction = down.normalized().lerp(toward.normalized(), ease((t - 0.4) / 0.35))
        set_matrix("Arrow", point, direction, V(0, 0, 1))

def pose_hit(t):
    k = math.sin(min(1.0, t / 0.35) * math.pi * 0.5) if t < 0.35 else 1 - ease((t - 0.35) / 0.65)
    pose_idle(0)
    fk("Spine", x=-2 + 6 * k); fk("Chest", x=-4 + 12 * k); fk("Head", x=-6 + 16 * k, z=-8 * k)
    fk("Jaw", x=-2 - 14 * k)
    carry_arms(-10 * k, -14 * k, 14 + 18 * k, 16 + 22 * k)

POSES = {"Idle": pose_idle, "Walk": lambda t: pose_cycle(t, False), "Run": lambda t: pose_cycle(t, True),
         "Crouch": pose_crouch, "Draw": pose_draw, "AimHold": pose_aim, "Release": pose_release,
         "Nock": pose_nock, "Hit": pose_hit}

previous = {}
for clip, first, last, loop in CLIPS:
    for frame in range(first, last + 1):
        t = (frame - first) / (last - first)
        reset_pose()
        POSES[clip](t)
        upd()
        for pose in P:
            q = pose.rotation_quaternion.copy()
            # Keep neighbouring keys in the same hemisphere so sampled curves never flip.
            if pose.name in previous and q.dot(previous[pose.name]) < 0:
                q = Quaternion((-q.w, -q.x, -q.y, -q.z))
                pose.rotation_quaternion = q
            previous[pose.name] = q
            pose.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            if pose.name in LOCATED: pose.keyframe_insert(data_path="location", frame=frame)
rig.animation_data.action.name = "SkeletonArcher_Takes"
scene.frame_set(0)

# ---------------------------------------------------------------- export
expected = {}
for name, (obj, _) in pieces.items():
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    expected[name] = list(sum(corners, Vector()) / 8)

bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
string.select_set(True)
for obj, _ in pieces.values(): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUT / "SkeletonArcher.fbx"), use_selection=True,
    object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y", global_scale=1,
    apply_unit_scale=True, add_leaf_bones=False, use_armature_deform_only=False,
    bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)

# The projectile is a separate static mesh: tip at the origin, shaft trailing along -Y.
projectile = join(arrow_parts("Flight", V(0, -0.80, 0), V(0, 1, 0)), "SkeletonArrow")
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
bpy.ops.object.select_all(action="DESELECT")
projectile.select_set(True)
bpy.context.view_layer.objects.active = projectile
bpy.ops.export_scene.fbx(filepath=str(OUT / "SkeletonArrow.fbx"), use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", global_scale=1, apply_unit_scale=True, bake_anim=False)
projectile.hide_render = True
projectile.hide_set(True)

bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "Blender/SkeletonArcher/SkeletonArcher.blend"))
meshes = [obj for obj, _ in pieces.values()] + [string]
triangles = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in meshes)
stats = {"pieces": len(pieces), "triangles": triangles, "bones": len(rig.data.bones),
         "materials": list(PALETTE), "fps": scene.render.fps,
         "clips": [{"name": n, "first": a, "last": b, "loop": l} for n, a, b, l in CLIPS],
         "piece_bones": {name: bone for name, (_, bone) in pieces.items()}, "expected_centres": expected}
(PREVIEW / "model-stats.json").write_text(json.dumps(stats, indent=2))
(OUT / "SkeletonArcher.clips.json").write_text(json.dumps({"fps": scene.render.fps, "clips": stats["clips"]}, indent=2))
print("SKELETON_STATS", json.dumps({k: stats[k] for k in ("pieces", "triangles", "bones")}))

if RENDER:
    ground_mat = bpy.data.materials.new("PreviewGround")
    ground_mat.diffuse_color = (0.12, 0.11, 0.10, 1)
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    bpy.context.object.data.materials.append(ground_mat)
    for name, pos, energy, size in [("Key", (3, 2.5, 4), 500, 4), ("Fill", (-3, 2, 3), 220, 4), ("Rim", (0, -3, 4), 450, 3)]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = pos
        light.rotation_euler = (V(0, 0, 0.9) - light.location).to_track_quat("-Z", "Y").to_euler()
    camera_data = bpy.data.cameras.new("ArcherPreview")
    camera = bpy.data.objects.new("ArcherPreview", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = "ORTHO"
    if QUICK:
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.color_type = "MATERIAL"
        scene.display.shading.light = "STUDIO"
    else:
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 24
        scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 900, 1000
    scene.world.color = (0.12, 0.12, 0.12)
    shots = [("archer-front", 0, (0, 4, 1.0), (0, 0, 0.9), 2.2), ("archer-three-quarter", 0, (2.6, 3.2, 1.5), (0, 0, 0.9), 2.2),
             ("archer-side", 0, (4, 0, 1.0), (0, 0, 0.9), 2.2), ("archer-skull", 0, (0.6, 1.2, 1.8), (0, 0, 1.66), 0.5),
             ("archer-full-draw", 211, (2.2, 3.4, 1.6), (0, 0.2, 1.0), 2.4), ("archer-full-draw-top", 211, (0, 0.3, 5), (0, 0.3, 1.0), 2.0),
             ("archer-nock", 287, (2.6, 2.0, 1.6), (0, 0, 1.0), 2.2), ("archer-crouch", 150, (2.6, 3.0, 1.2), (0, 0, 0.6), 2.0),
             ("archer-walk", 78, (4, 0.3, 1.0), (0, 0, 0.9), 2.2), ("archer-run", 116, (4, 0.3, 1.0), (0, 0, 0.9), 2.2)]
    for name, frame, pos, target, scale in shots:
        scene.frame_set(frame)
        camera.location = pos
        camera.rotation_euler = (V(*target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera_data.ortho_scale = scale
        scene.render.filepath = str(PREVIEW / (name + ".png"))
        bpy.ops.render.render(write_still=True)
    scene.frame_set(0)
