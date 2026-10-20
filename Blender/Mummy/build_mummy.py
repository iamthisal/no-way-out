"""Build the Egypt gallery mummy and its four canopic jars. Run with Blender --background.

The mummy is made of rigid wrapped pieces, one per rig bone, sharing the skeleton
archer's proportions. Each canopic jar is pre-split into shards so Unity can
shatter it. Everything is generated locally, without downloads.
Blender coordinates: +Y is forward, +Z is up, +X is the mummy's right; metres.
Pass `-- --no-render` to skip previews, or `-- --quick` for fast Workbench previews.
"""
import bpy
import json
import math
import random
import sys
from mathutils import Euler, Matrix, Quaternion, Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/_Project/Models/Mummy"
PREVIEW = ROOT / "Temp/MummyVerification"
RENDER = "--no-render" not in sys.argv
QUICK = "--quick" in sys.argv
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30
rng = random.Random(1922)

# The four sons of Horus: jar name, lid head, linear glaze colour shared with the bound mummy's amulet.
GODS = [("Imsety", "human", (0.06, 0.45, 0.42, 1)), ("Hapi", "baboon", (0.04, 0.10, 0.45, 1)),
        ("Duamutef", "jackal", (0.50, 0.08, 0.04, 1)), ("Qebehsenuef", "falcon", (0.75, 0.50, 0.10, 1))]
# name: (linear colour, roughness, metallic, emission strength)
PALETTE = {
    "Mummy_Linen": ((0.46, 0.40, 0.28, 1), 0.85, 0.0, 0.0),
    "Mummy_LinenDark": ((0.26, 0.21, 0.14, 1), 0.9, 0.0, 0.0),
    "Mummy_Resin": ((0.03, 0.022, 0.015, 1), 0.6, 0.0, 0.0),
    "Mummy_EyeGlow": ((0.55, 0.95, 0.30, 1), 0.3, 0.0, 4.0),
    "Mummy_Amulet": (GODS[0][2], 0.25, 0.1, 0.6),
    "Mummy_Gold": ((0.75, 0.55, 0.15, 1), 0.35, 0.8, 0.0),
    "Mummy_Teeth": ((0.55, 0.48, 0.32, 1), 0.6, 0.0, 0.0),
    "Jar_Alabaster": ((0.72, 0.66, 0.54, 1), 0.4, 0.0, 0.0),
    "Jar_Inner": ((0.35, 0.31, 0.25, 1), 0.8, 0.0, 0.0),
    "Jar_Dark": ((0.04, 0.04, 0.06, 1), 0.5, 0.0, 0.0),
    "Jar_Gold": ((0.78, 0.56, 0.16, 1), 0.35, 0.7, 0.0),
    "Jar_Skin": ((0.42, 0.24, 0.12, 1), 0.6, 0.0, 0.0),
    "Jar_Fur": ((0.30, 0.30, 0.24, 1), 0.85, 0.0, 0.0),
    "Jar_Black": ((0.03, 0.03, 0.035, 1), 0.45, 0.0, 0.0),
    "Jar_Feather": ((0.30, 0.18, 0.08, 1), 0.8, 0.0, 0.0),
    "Jar_Wood": ((0.10, 0.06, 0.035, 1), 0.6, 0.0, 0.0),
    "Jar_Brass": ((0.40, 0.31, 0.16, 1), 0.38, 0.5, 0.0),
}
for name, _, color in GODS: PALETTE["Jar_" + name] = (color, 0.35, 0.0, 0.0)
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

CLIPS = [("Idle", 0, 60, True), ("Shamble", 70, 118, True), ("Chase", 130, 162, True), ("Bite", 170, 188, False),
         ("Hit", 200, 210, False), ("Fall", 220, 244, False), ("Down", 250, 280, True), ("Rise", 290, 338, False),
         ("Entombed", 350, 410, True), ("WalkOut", 420, 474, False)]
# The sarcophagus case stands this far behind the mummy's root; WalkOut ends exactly on the root.
TOMB_DEPTH = 0.95
TOMB_FLOOR = 0.12
scene.frame_start = 0
scene.frame_end = CLIPS[-1][2]

def V(*values): return Vector(values)

def mirror(p): return (-p[0], p[1], p[2])

# ---------------------------------------------------------------- rig (shared proportions with the skeleton archer)
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
rig = bpy.context.object
rig.name = "Mummy_Rig"
rig.data.name = "Mummy_Skeleton"
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
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = True
rest = {bone.name: bone.matrix_local.copy() for bone in rig.data.bones}

# ---------------------------------------------------------------- mesh helpers
def ellipsoid(name, position, scale, material="Mummy_Linen", subdivisions=1, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(materials[material])
    return obj

def box(name, position, size, material="Mummy_Linen", rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(materials[material])
    return obj

def mesh_object(name, verts, faces, material_names, face_materials=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    for material in material_names: mesh.materials.append(materials[material])
    if face_materials:
        for polygon, index in zip(mesh.polygons, face_materials): polygon.material_index = index
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj

def tube(name, points, radii, material="Mummy_Linen", sides=8, closed=False):
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
        for j in range(sides):
            angle = j * math.tau / sides
            verts.append(tuple(point + right * math.cos(angle) * radii[i] + up * math.sin(angle) * radii[i]))
    segments = count if closed else count - 1
    for i in range(segments):
        a, b = i * sides, ((i + 1) % count) * sides
        for j in range(sides): faces.append((a + j, a + (j + 1) % sides, b + (j + 1) % sides, b + j))
    if not closed:
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple(range((count - 1) * sides, count * sides)))
    return mesh_object(name, verts, faces, [material])

def band(name, centre, rx, ry, tilt=(0, 0, 0), wire=0.007, material="Mummy_LinenDark", points=12):
    """A tilted elliptical bandage loop around a body part."""
    rotation = Euler(tilt).to_matrix()
    loop = [Vector(centre) + rotation @ V(math.cos(a) * rx, math.sin(a) * ry, 0) for a in [i * math.tau / points for i in range(points)]]
    return tube(name, loop, wire, material, 4, closed=True)

def limb(name, a, b, radii, bands=3):
    """A wrapped limb: a tapered core with tilted bandage bands along it."""
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    parts = [tube(name, [a, a.lerp(b, 0.5), b], radii, "Mummy_Linen", 8)]
    # Tilt each band around an axis perpendicular to the limb.
    side = axis.cross(V(0, 0, 1) if abs(axis.z) < 0.9 else V(1, 0, 0)).normalized()
    base = axis.to_track_quat("Z", "Y").to_euler()
    for k in range(bands):
        t = (k + 0.5) / bands
        radius = radii[0] + (radii[-1] - radii[0]) * t
        tilt = Euler((base.x + rng.uniform(-0.35, 0.35), base.y + rng.uniform(-0.35, 0.35), base.z))
        parts.append(band(name + "Band%d" % k, a.lerp(b, t), radius * 1.06, radius * 1.06, tilt,
                          material="Mummy_LinenDark" if k % 2 else "Mummy_Linen"))
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

# ---------------------------------------------------------------- head, jaw and neck
head_centre = V(0, 0.01, 1.71)
head = [ellipsoid("Skull", head_centre, (0.098, 0.112, 0.118), subdivisions=2),
        ellipsoid("EyeGap", (0, 0.098, 1.722), (0.078, 0.022, 0.017), "Mummy_Resin"),
        ellipsoid("MouthGap", (0, 0.085, 1.645), (0.05, 0.03, 0.02), "Mummy_Resin")]
for sign in (-1, 1): head.append(ellipsoid("Eye", (sign * 0.032, 0.112, 1.722), (0.011, 0.006, 0.008), "Mummy_EyeGlow"))
for k, (height, tilt) in enumerate([(-0.07, 0.25), (-0.035, -0.2), (0.0, 0.3), (0.035, -0.15), (0.07, 0.2), (0.095, -0.3)]):
    scale = math.sqrt(max(0.05, 1 - (height / 0.118) ** 2))
    head.append(band("HeadWrap%d" % k, head_centre + V(0, 0, height), 0.104 * scale, 0.118 * scale,
                     (tilt, rng.uniform(-0.2, 0.2), 0), 0.008, "Mummy_LinenDark" if k % 2 else "Mummy_Linen"))
piece("Head", "Head", head)
jaw = [ellipsoid("LowerFace", (0, 0.055, 1.615), (0.07, 0.07, 0.042)),
       band("ChinWrap", (0, 0.06, 1.61), 0.072, 0.072, (0.4, 0, 0), 0.007)]
for k in range(4): jaw.append(box("Tooth", ((k - 1.5) * 0.014, 0.112, 1.628), (0.009, 0.006, 0.012), "Mummy_Teeth"))
piece("Jaw", "Jaw", jaw)
piece("Neck", "Neck", [tube("NeckCore", [(0, -0.01, 1.49), (0, -0.005, 1.56), (0, 0, 1.63)], [0.05, 0.046, 0.05], "Mummy_Linen", 8),
                       band("NeckWrap", (0, -0.005, 1.55), 0.052, 0.052, (0.3, 0, 0), 0.007)])

# ---------------------------------------------------------------- torso
torso = [ellipsoid("Chest", (0, 0.0, 1.36), (0.165, 0.118, 0.17), subdivisions=2)]
for k, (height, tilt_x, tilt_y) in enumerate([(-0.12, 0.1, 0.35), (-0.06, -0.1, -0.35), (0.0, 0.15, 0.3), (0.06, -0.1, -0.3), (0.11, 0.1, 0.2)]):
    scale = math.sqrt(max(0.1, 1 - (height / 0.17) ** 2))
    torso.append(band("ChestWrap%d" % k, V(0, 0, 1.36 + height), 0.172 * scale, 0.124 * scale, (tilt_x, tilt_y, 0), 0.009,
                      "Mummy_LinenDark" if k % 2 else "Mummy_Linen"))
piece("Torso", "Chest", torso)
amulet = [ellipsoid("Scarab", (0, 0.124, 1.395), (0.032, 0.012, 0.04), "Mummy_Amulet"),
          ellipsoid("ScarabHead", (0, 0.127, 1.442), (0.016, 0.009, 0.012), "Mummy_Amulet")]
for sign in (-1, 1):
    amulet.append(ellipsoid("Wing", (sign * 0.05, 0.118, 1.405), (0.042, 0.007, 0.02), "Mummy_Amulet", rotation=(0, sign * 0.35, 0)))
amulet.append(tube("Cord", [(-0.06, 0.06, 1.5), (-0.03, 0.115, 1.455), (0, 0.124, 1.445), (0.03, 0.115, 1.455), (0.06, 0.06, 1.5)],
                   0.004, "Mummy_Gold", 4))
amulet.append(band("Setting", (0, 0.122, 1.395), 0.036, 0.044, (math.pi / 2, 0, 0), 0.004, "Mummy_Gold"))
piece("Amulet", "Chest", amulet)
abdomen = [ellipsoid("Belly", (0, -0.005, 1.16), (0.138, 0.102, 0.11))]
for k in range(3):
    abdomen.append(band("BellyWrap%d" % k, V(0, -0.005, 1.10 + k * 0.055), 0.142, 0.106, (rng.uniform(-0.2, 0.2), rng.uniform(-0.25, 0.25), 0), 0.008,
                        "Mummy_LinenDark" if k % 2 else "Mummy_Linen"))
piece("Abdomen", "Spine", abdomen)
pelvis = [ellipsoid("Hips", (0, 0, 0.98), (0.155, 0.112, 0.095)),
          band("Sash", (0, 0, 1.0), 0.16, 0.118, (0.1, 0, 0), 0.01, "Mummy_LinenDark"),
          box("FrontFlap", (0, 0.105, 0.87), (0.15, 0.018, 0.2), "Mummy_LinenDark", (0.12, 0, 0)),
          box("BackFlap", (0, -0.1, 0.88), (0.17, 0.018, 0.18), "Mummy_LinenDark", (-0.1, 0, 0)),
          tube("LooseEnd", [(0.12, -0.07, 0.95), (0.14, -0.1, 0.88), (0.13, -0.11, 0.8)], [0.014, 0.012, 0.006], "Mummy_LinenDark", 4)]
piece("Pelvis", "Hips", pelvis)

# ---------------------------------------------------------------- limbs
for side, sign in (("L", -1), ("R", 1)):
    shoulder, elbow = bone_point("UpperArm_" + side, 0), bone_point("UpperArm_" + side, 1)
    wrist, fingers = bone_point("Forearm_" + side, 1), bone_point("Hand_" + side, 1)
    piece("Shoulder_" + side, "Clavicle_" + side, [ellipsoid("Deltoid", shoulder + V(sign * 0.01, 0, 0.01), (0.062, 0.06, 0.06)),
                                                   tube("Collar", [(sign * 0.03, 0.05, 1.47), shoulder], 0.03, "Mummy_Linen", 6)])
    piece("UpperArm_" + side, "UpperArm_" + side, limb("Arm", shoulder, elbow, [0.052, 0.046, 0.042], 3))
    forearm = limb("Forearm", elbow, wrist, [0.043, 0.037, 0.032], 3)
    if side == "L":
        loose = elbow.lerp(wrist, 0.6) + V(0, -0.035, 0)
        forearm.append(tube("LooseEnd", [loose, loose + V(0.01, -0.03, -0.06), loose + V(0.0, -0.02, -0.13)], [0.013, 0.011, 0.005], "Mummy_LinenDark", 4))
    piece("Forearm_" + side, "Forearm_" + side, forearm)
    hand = [tube("Mitt", [wrist, wrist.lerp(fingers, 0.55), fingers], [0.032, 0.034, 0.022], "Mummy_Linen", 8),
            band("HandWrap", wrist.lerp(fingers, 0.3), 0.036, 0.036, (0.3, 0.2, 0), 0.006)]
    for k in range(4):
        tip = fingers + V(sign * (-0.015 + k * 0.01), 0.008 - abs(k - 1.5) * 0.004, -0.004)
        hand.append(tube("Finger", [tip + V(0, 0, 0.03), tip, tip + V(0, 0.012, -0.022)], [0.0065, 0.006, 0.004], "Mummy_Resin", 5))
    piece("Hand_" + side, "Hand_" + side, hand)
    hip, knee = bone_point("Thigh_" + side, 0), bone_point("Thigh_" + side, 1)
    ankle, toe = bone_point("Shin_" + side, 1), bone_point("Foot_" + side, 1)
    piece("Thigh_" + side, "Thigh_" + side, limb("Thigh", hip + V(0, 0, 0.02), knee, [0.085, 0.072, 0.062], 4))
    piece("Shin_" + side, "Shin_" + side, limb("Shin", knee, ankle - V(0, 0, 0.01), [0.06, 0.05, 0.044], 3))
    piece("Foot_" + side, "Foot_" + side, [ellipsoid("FootWrap", ankle.lerp(toe, 0.45) + V(0, -0.005, -0.012), (0.048, 0.105, 0.045)),
                                           band("FootBand", ankle.lerp(toe, 0.45) + V(0, 0, -0.03), 0.05, 0.05, (0, math.pi / 2, 0.2), 0.006)])

# ---------------------------------------------------------------- posing (same armature-axis conventions as the archer)
P = rig.pose.bones
for pose in P: pose.rotation_mode = "QUATERNION"

def upd(): bpy.context.view_layer.update()

def reset_pose():
    for pose in P:
        pose.rotation_quaternion = (1, 0, 0, 0)
        pose.location = (0, 0, 0)
    upd()

def fk(name, x=0.0, y=0.0, z=0.0):
    """Rotate in armature axes relative to the parent: +X swings a hanging limb forward, tilts an upright bone back."""
    b = rest[name].to_quaternion()
    r = Euler((math.radians(x), math.radians(y), math.radians(z)), "XYZ").to_quaternion()
    P[name].rotation_quaternion = b.inverted() @ r @ b

def shift(offset):
    P["Hips"].location = rest["Hips"].to_3x3().inverted() @ Vector(offset)

def ease(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)

def apply(pose):
    for name, value in pose.items():
        if name == "Hips@": shift(value)
        else: fk(name, *value)

def mix(a, b, t):
    out = {}
    for key in set(a) | set(b):
        va, vb = a.get(key, (0, 0, 0)), b.get(key, (0, 0, 0))
        out[key] = tuple(x + (y - x) * t for x, y in zip(va, vb))
    return out

def keyed(keys, t):
    """Blend through (time, pose) keys with eased segments."""
    for (t0, a), (t1, b) in zip(keys, keys[1:]):
        if t <= t1: return mix(a, b, ease((t - t0) / (t1 - t0)) if t1 > t0 else 1.0)
    return keys[-1][1]

def with_(base, **changes):
    pose = dict(base)
    for key, value in changes.items(): pose[key.replace("__", "@")] = value
    return pose

def ground(weight=1.0):
    """Shift the pelvis so the lowest ankle rests at its standing height."""
    upd()
    lowest = min(P["Foot_L"].head.z, P["Foot_R"].head.z)
    P["Hips"].location += rest["Hips"].to_3x3().inverted() @ V(0, 0, (rest["Foot_L"].translation.z - lowest) * weight)

STAND = {"Spine": (-8, 0, 0), "Chest": (-6, 0, 0), "Neck": (8, 0, 0), "Head": (6, 0, 0), "Jaw": (-3, 0, 0),
         "UpperArm_L": (85, 6, 0), "Forearm_L": (12, 0, 0), "Hand_L": (-8, 0, 0),
         "UpperArm_R": (88, -6, 0), "Forearm_R": (8, 0, 0), "Hand_R": (-6, 0, 0),
         "Thigh_L": (4, 3, 0), "Shin_L": (-8, 0, 0), "Foot_L": (4, 0, 0),
         "Thigh_R": (2, -3, 0), "Shin_R": (-6, 0, 0), "Foot_R": (4, 0, 0)}
BUCKLE = with_(STAND, Hips__=(0, 0, -0.22), Hips=(10, 0, 0), Spine=(-14, 0, 0), Head=(14, 0, 0),
               Thigh_L=(35, 3, 0), Shin_L=(-78, 0, 0), Foot_L=(33, 0, 0), Thigh_R=(30, -3, 0), Shin_R=(-72, 0, 0), Foot_R=(32, 0, 0),
               UpperArm_L=(40, 22, 0), UpperArm_R=(45, -22, 0))
TOPPLE = with_(STAND, Hips__=(0, -0.2, -0.55), Hips=(55, 0, 0), Spine=(-5, 0, 0), Chest=(0, 0, 0), Head=(12, 0, 0),
               Thigh_L=(30, 3, 0), Shin_L=(-40, 0, 0), Thigh_R=(26, -3, 0), Shin_R=(-35, 0, 0),
               UpperArm_L=(110, 30, 0), UpperArm_R=(110, -30, 0), Jaw=(-20, 0, 0))
LYING = {"Hips@": (0, -0.35, -0.84), "Hips": (90, 0, 6), "Spine": (2, 0, 3), "Chest": (0, 0, -4), "Neck": (-4, 0, 0),
         "Head": (-10, 8, 12), "Jaw": (-8, 0, 0), "UpperArm_L": (12, 28, 0), "Forearm_L": (8, 0, 0), "Hand_L": (0, 0, 0),
         "UpperArm_R": (8, -32, 0), "Forearm_R": (14, 0, 0), "Hand_R": (0, 0, 0),
         "Thigh_L": (0, 6, 0), "Shin_L": (-4, 0, 0), "Foot_L": (0, 0, 0), "Thigh_R": (4, -5, 0), "Shin_R": (-8, 0, 0), "Foot_R": (0, 0, 0)}
SIT = {"Hips@": (0, -0.3, -0.80), "Hips": (85, 0, 0), "Spine": (-60, 0, 0), "Chest": (-25, 0, 0), "Neck": (5, 0, 0),
       "Head": (0, 0, 0), "Jaw": (-10, 0, 0), "UpperArm_L": (-30, 25, 0), "Forearm_L": (10, 0, 0),
       "UpperArm_R": (-30, -25, 0), "Forearm_R": (10, 0, 0), "Thigh_L": (0, 6, 0), "Shin_L": (-10, 0, 0),
       "Thigh_R": (-5, -6, 0), "Shin_R": (-25, 0, 0)}
KNEEL = {"Hips@": (0, 0.0, -0.50), "Hips": (25, 0, 0), "Spine": (-35, 0, 0), "Chest": (-10, 0, 0), "Neck": (10, 0, 0),
         "Head": (10, 0, 0), "UpperArm_L": (45, 12, 0), "Forearm_L": (20, 0, 0), "UpperArm_R": (50, -12, 0), "Forearm_R": (20, 0, 0),
         "Thigh_L": (65, 6, 0), "Shin_L": (-118, 0, 0), "Foot_L": (28, 0, 0), "Thigh_R": (55, -6, 0), "Shin_R": (-105, 0, 0), "Foot_R": (25, 0, 0)}
CROUCH = with_(STAND, Hips__=(0, 0, -0.2), Hips=(10, 0, 0), Spine=(-18, 0, 0), Chest=(-8, 0, 0),
               Thigh_L=(40, 3, 0), Shin_L=(-70, 0, 0), Foot_L=(20, 0, 0), Thigh_R=(36, -3, 0), Shin_R=(-64, 0, 0), Foot_R=(18, 0, 0),
               UpperArm_L=(55, 10, 0), UpperArm_R=(58, -10, 0))
LUNGE = with_(STAND, Hips__=(0, 0.22, -0.06), Spine=(-22, 0, 0), Chest=(-14, 0, 0), Neck=(12, 0, 0), Head=(-4, 0, 0), Jaw=(-30, 0, 0),
              UpperArm_L=(88, -8, 0), Forearm_L=(35, 0, 0), UpperArm_R=(88, 8, 0), Forearm_R=(35, 0, 0),
              Thigh_L=(25, 3, 0), Shin_L=(-30, 0, 0), Thigh_R=(-12, -3, 0), Shin_R=(-10, 0, 0))

def pose_idle(t):
    phase = t * math.tau
    pose = with_(STAND, Chest=(-6, 2 * math.sin(phase), 4 * math.sin(phase)), Spine=(-8, 2 * math.sin(phase), 0),
                 Head=(6, 6 * math.sin(phase + 0.6), 5 * math.sin(phase)),
                 UpperArm_L=(85 + 3 * math.sin(phase), 6, 0), UpperArm_R=(88 - 3 * math.sin(phase), -6, 0),
                 Jaw=(-3 - 6 * max(0.0, math.sin(2 * phase)) ** 3, 0, 0))
    apply(pose)

def pose_walk(t, chase):
    phase = t * math.tau
    lean = -12 if chase else 0
    pose = dict(STAND)
    pose["Hips"] = (0, 5 * math.sin(phase), 8 * math.sin(phase))
    pose["Spine"] = (-8 + lean, -4 * math.sin(phase), 0)
    pose["Chest"] = (-6 + lean * 0.5, 0, -6 * math.sin(phase))
    pose["Head"] = (6 - lean * 0.4, 8 * math.sin(phase + 0.5), 5 * math.sin(phase))
    pose["Jaw"] = (-3 - 4 * max(0.0, math.sin(2 * phase)), 0, 0)
    reach = 92 if chase else 85
    pose["UpperArm_L"] = (reach + 5 * math.sin(phase), 6, 0)
    pose["UpperArm_R"] = (reach + 3 - 5 * math.sin(phase), -6, 0)
    for side, offset, drag in (("L", 0.0, False), ("R", math.pi, not chase)):
        p = phase + offset
        bend = max(0.0, math.cos(p + math.pi / 6)) ** 1.5
        swing = (24 if chase else 18) * (0.6 if drag else 1.0)
        thigh = swing * math.sin(p)
        shin = -((6 + 10 * bend) if drag else (6 + (55 if chase else 32) * bend))
        foot = -(thigh + shin) + (10 if drag else -8 * bend)
        pose["Thigh_" + side] = (thigh, 3 if side == "L" else -3, 0)
        pose["Shin_" + side] = (shin, 0, 0)
        pose["Foot_" + side] = (foot, 0, 0)
    apply(pose)
    ground()

def pose_bite(t):
    lunge_open = with_(LUNGE, Jaw=(-32, 0, 0))
    lunge_shut = with_(LUNGE, Jaw=(-2, 0, 0), Forearm_L=(55, 0, 0), Forearm_R=(55, 0, 0))
    apply(keyed([(0, STAND), (0.3, lunge_open), (0.5, lunge_shut), (1.0, STAND)], t))
    ground()

def pose_hit(t):
    k = math.sin(min(1.0, t / 0.35) * math.pi * 0.5) if t < 0.35 else 1 - ease((t - 0.35) / 0.65)
    apply(mix(STAND, with_(STAND, Spine=(-2, 0, 0), Chest=(8, 0, 0), Head=(26, 0, -10), Jaw=(-20, 0, 0),
                           UpperArm_L=(50, 20, 0), UpperArm_R=(45, -20, 0)), k))
    ground()

def pose_fall(t):
    apply(keyed([(0, STAND), (0.35, BUCKLE), (0.7, TOPPLE), (1.0, LYING)], t))
    if t < 0.35: ground(1 - t / 0.35)

def pose_down(t):
    phase = t * math.tau
    apply(with_(LYING, Head=(-10, 8 + 3 * math.sin(phase), 12 + 2 * math.sin(2 * phase)),
                Hand_L=(6 * max(0.0, math.sin(3 * phase)) ** 4, 0, 0), Jaw=(-8 - 3 * math.sin(phase), 0, 0)))

def pose_rise(t):
    apply(keyed([(0, LYING), (0.3, SIT), (0.6, KNEEL), (0.85, CROUCH), (1.0, STAND)], t))
    if t > 0.6: ground((t - 0.6) / 0.4)

# Standing inside the sarcophagus in the Osiris pose, arms crossed over the chest.
ENTOMBED = {"Hips@": (0, -TOMB_DEPTH, TOMB_FLOOR), "Spine": (0, 0, 0), "Chest": (0, 0, 0), "Neck": (0, 0, 0), "Head": (-6, 0, 0),
            "Jaw": (0, 0, 0), "UpperArm_L": (20, -16, -72), "Forearm_L": (116, 0, 0), "Hand_L": (-6, 0, 0),
            "UpperArm_R": (20, 16, 72), "Forearm_R": (106, 0, 0), "Hand_R": (-6, 0, 0),
            "Thigh_L": (0, 2, 0), "Shin_L": (0, 0, 0), "Foot_L": (0, 0, 0), "Thigh_R": (0, -2, 0), "Shin_R": (0, 0, 0), "Foot_R": (0, 0, 0)}
STIR = with_(ENTOMBED, Head=(10, 0, 8), Jaw=(-18, 0, 0), UpperArm_L=(55, -10, 0), Forearm_L=(55, 0, 0),
             UpperArm_R=(60, 10, 0), Forearm_R=(50, 0, 0), Spine=(-6, 0, 0))
STEP_DOWN = with_(STAND, Hips__=(0, -0.62, 0.08), Spine=(-14, 0, 0), Chest=(-8, 0, 0),
                  Thigh_L=(38, 3, 0), Shin_L=(-35, 0, 0), Foot_L=(-8, 0, 0), Thigh_R=(-8, -3, 0), Shin_R=(-10, 0, 0),
                  UpperArm_L=(80, 8, 0), UpperArm_R=(70, -8, 0), Jaw=(-12, 0, 0))
STEP_OUT = with_(STAND, Hips__=(0, -0.28, -0.02), Spine=(-12, 0, 0), Chest=(-8, 0, 0),
                 Thigh_L=(-14, 3, 0), Shin_L=(-12, 0, 0), Thigh_R=(30, -3, 0), Shin_R=(-30, 0, 0), Foot_R=(-4, 0, 0),
                 UpperArm_L=(84, 6, 0), UpperArm_R=(88, -6, 0))

def pose_entombed(t):
    phase = t * math.tau
    apply(with_(ENTOMBED, Chest=(1.2 * math.sin(phase), 0, 0), Head=(-6, 0, 3 * math.sin(phase)),
                Jaw=(-2 * max(0.0, math.sin(2 * phase)) ** 2, 0, 0)))

def pose_walkout(t):
    apply(keyed([(0, ENTOMBED), (0.22, STIR), (0.5, STEP_DOWN), (0.75, STEP_OUT), (1.0, STAND)], t))
    if t > 0.75: ground((t - 0.75) / 0.25)

POSES = {"Idle": pose_idle, "Entombed": pose_entombed, "WalkOut": pose_walkout, "Shamble": lambda t: pose_walk(t, False), "Chase": lambda t: pose_walk(t, True),
         "Bite": pose_bite, "Hit": pose_hit, "Fall": pose_fall, "Down": pose_down, "Rise": pose_rise}

previous = {}
for clip, first, last, loop in CLIPS:
    for frame in range(first, last + 1):
        t = (frame - first) / (last - first)
        reset_pose()
        POSES[clip](t)
        upd()
        for pose in P:
            q = pose.rotation_quaternion.copy()
            if pose.name in previous and q.dot(previous[pose.name]) < 0:
                q = Quaternion((-q.w, -q.x, -q.y, -q.z))
                pose.rotation_quaternion = q
            previous[pose.name] = q
            pose.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            if pose.name == "Hips": pose.keyframe_insert(data_path="location", frame=frame)
rig.animation_data.action.name = "Mummy_Takes"
scene.frame_set(0)

expected = {}
for name, (obj, _) in pieces.items():
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    expected[name] = list(sum(corners, Vector()) / 8)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
for obj, _ in pieces.values(): obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=str(OUT / "Mummy.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
    axis_forward="-Z", axis_up="Y", global_scale=1, apply_unit_scale=True, add_leaf_bones=False,
    use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)

# ---------------------------------------------------------------- canopic jars
def profile(h):
    """Jar radius at height h (body is 0.33 m tall)."""
    points = [(0.0, 0.075), (0.02, 0.085), (0.08, 0.115), (0.16, 0.125), (0.24, 0.11), (0.29, 0.088), (0.32, 0.078), (0.33, 0.078)]
    for (h0, r0), (h1, r1) in zip(points, points[1:]):
        if h <= h1: return r0 + (r1 - r0) * (h - h0) / (h1 - h0)
    return points[-1][1]

def glaze_index(h, angle):
    """0 alabaster, 1 god glaze band, 2 dark hieroglyph column on the front, 3 gold ring."""
    if 0.118 <= h <= 0.128 or 0.198 <= h <= 0.208: return 3
    if 0.128 < h < 0.198: return 1
    if abs(math.atan2(math.cos(angle), math.sin(angle))) < 0.22 and 0.035 < h < 0.11: return 2
    return 0

def shard(name, origin, a0, a1, h0, h1, god, steps_a=4, steps_h=4, thickness=0.011):
    """One curved wall fragment between two angles and two heights, with thickness."""
    verts, faces, mats = [], [], []
    def ring_verts(inner):
        for i in range(steps_h + 1):
            h = h0 + (h1 - h0) * i / steps_h
            r = profile(h) - (thickness if inner else 0)
            for j in range(steps_a + 1):
                a = a0 + (a1 - a0) * j / steps_a
                verts.append(tuple(origin + V(math.sin(a) * r, math.cos(a) * r, h)))
    ring_verts(False)
    ring_verts(True)
    n = (steps_h + 1) * (steps_a + 1)
    def at(i, j, inner=False): return (n if inner else 0) + i * (steps_a + 1) + j
    for i in range(steps_h):
        for j in range(steps_a):
            h = h0 + (h1 - h0) * (i + 0.5) / steps_h
            a = a0 + (a1 - a0) * (j + 0.5) / steps_a
            faces.append((at(i, j), at(i + 1, j), at(i + 1, j + 1), at(i, j + 1))); mats.append(glaze_index(h, a))
            faces.append((at(i, j, True), at(i, j + 1, True), at(i + 1, j + 1, True), at(i + 1, j, True))); mats.append(4)
    for i in range(steps_h):
        for j in (0, steps_a):
            quad = (at(i, j), at(i, j, True), at(i + 1, j, True), at(i + 1, j))
            faces.append(quad if j == 0 else tuple(reversed(quad))); mats.append(4)
    for j in range(steps_a):
        for i in (0, steps_h):
            quad = (at(i, j), at(i, j + 1), at(i, j + 1, True), at(i, j, True))
            faces.append(quad if i == 0 else tuple(reversed(quad))); mats.append(4)
    obj = mesh_object(name, verts, faces, ["Jar_Alabaster", "Jar_" + god, "Jar_Dark", "Jar_Gold", "Jar_Inner"], mats)
    return obj

def lid_head(kind, top, god):
    h = top.z
    o = V(top.x, top.y, 0)
    parts = [tube("LidRim", [o + V(0, 0, h), o + V(0, 0, h + 0.02)], [0.086, 0.08], "Jar_Alabaster", 16),
             band("LidGlaze", o + V(0, 0, h + 0.012), 0.087, 0.087, (0, 0, 0), 0.005, "Jar_" + god, 16)]
    c = o + V(0, 0, h + 0.075)
    if kind == "human":
        parts += [ellipsoid("Face", c + V(0, 0.01, 0), (0.048, 0.05, 0.062), "Jar_Skin"),
                  ellipsoid("Nemes", c + V(0, -0.015, 0.008), (0.066, 0.058, 0.07), "Jar_" + god),
                  band("Brow", c + V(0, 0.002, 0.035), 0.06, 0.056, (0.2, 0, 0), 0.006, "Jar_Gold"),
                  tube("Beard", [c + V(0, 0.04, -0.045), c + V(0, 0.048, -0.085)], [0.011, 0.008], "Jar_Dark", 6)]
        for sign in (-1, 1):
            parts += [box("Lappet", c + V(sign * 0.05, 0.02, -0.06), (0.024, 0.014, 0.07), "Jar_" + god),
                      ellipsoid("Eye", c + V(sign * 0.018, 0.052, 0.012), (0.008, 0.004, 0.005), "Jar_Dark")]
    elif kind == "baboon":
        parts += [ellipsoid("Mane", c + V(0, -0.012, 0.0), (0.066, 0.06, 0.064), "Jar_" + god),
                  ellipsoid("Head", c + V(0, 0.01, 0.005), (0.05, 0.05, 0.052), "Jar_Fur"),
                  ellipsoid("Muzzle", c + V(0, 0.055, -0.018), (0.03, 0.04, 0.026), "Jar_Fur"),
                  ellipsoid("Brow", c + V(0, 0.045, 0.022), (0.038, 0.014, 0.01), "Jar_Fur")]
        for sign in (-1, 1):
            parts += [ellipsoid("Ear", c + V(sign * 0.05, 0.0, 0.015), (0.012, 0.008, 0.016), "Jar_Fur"),
                      ellipsoid("Eye", c + V(sign * 0.016, 0.053, 0.012), (0.007, 0.004, 0.005), "Jar_Dark"),
                      ellipsoid("Nostril", c + V(sign * 0.008, 0.093, -0.016), (0.004, 0.003, 0.003), "Jar_Dark")]
    elif kind == "jackal":
        parts += [ellipsoid("Head", c, (0.044, 0.05, 0.048), "Jar_Black"),
                  tube("Snout", [c + V(0, 0.03, 0.0), c + V(0, 0.075, -0.012), c + V(0, 0.105, -0.02)], [0.026, 0.016, 0.007], "Jar_Black", 8),
                  band("Collar", c + V(0, 0, -0.045), 0.05, 0.05, (0.15, 0, 0), 0.008, "Jar_" + god)]
        for sign in (-1, 1):
            parts += [tube("Ear", [c + V(sign * 0.022, -0.005, 0.03), c + V(sign * 0.032, -0.01, 0.075), c + V(sign * 0.036, -0.012, 0.11)],
                           [0.016, 0.01, 0.001], "Jar_Black", 6),
                      ellipsoid("Eye", c + V(sign * 0.02, 0.045, 0.012), (0.007, 0.004, 0.005), "Jar_Gold")]
    else:
        parts += [ellipsoid("Head", c, (0.048, 0.054, 0.054), "Jar_Feather"),
                  tube("Beak", [c + V(0, 0.045, 0.004), c + V(0, 0.07, -0.006), c + V(0, 0.076, -0.03)], [0.014, 0.008, 0.002], "Jar_Gold", 6),
                  band("Collar", c + V(0, 0, -0.045), 0.052, 0.052, (0.1, 0, 0), 0.008, "Jar_" + god)]
        for sign in (-1, 1):
            parts += [ellipsoid("Eye", c + V(sign * 0.022, 0.044, 0.012), (0.009, 0.005, 0.007), "Jar_Dark"),
                      tube("Marking", [c + V(sign * 0.022, 0.044, 0.004), c + V(sign * 0.026, 0.04, -0.03)], [0.004, 0.002], "Jar_Dark", 4)]
    return parts

jar_roots = []
for index, (god, kind, _) in enumerate(GODS):
    origin = V(7.0 + index * 0.9, 0, 0.9)
    root = bpy.data.objects.new("Jar_" + god, None)
    scene.collection.objects.link(root)
    root.location = (origin.x, origin.y, 0)
    jar_roots.append(root)
    plinth = join([box("Stand", (origin.x, origin.y, 0.42), (0.42, 0.42, 0.84), "Jar_Wood"),
                   box("Cap", (origin.x, origin.y, 0.87), (0.46, 0.46, 0.06), "Jar_Wood"),
                   box("Trim", (origin.x, origin.y, 0.835), (0.44, 0.44, 0.012), "Jar_Brass"),
                   box("Label", (origin.x, origin.y + 0.212, 0.7), (0.16, 0.006, 0.06), "Jar_Brass")], "Plinth_" + god)
    fragments = [plinth, join([tube("BaseDisk", [origin, origin + V(0, 0, 0.02)], [0.075, 0.085], "Jar_Alabaster", 16)], "Base_" + god)]
    for band_index, (h0, h1) in enumerate(((0.02, 0.16), (0.16, 0.33))):
        offset = 0.5 if band_index else 0.0
        for k in range(6):
            a0 = (k + offset) * math.tau / 6
            frag = shard("Shard", origin, a0, a0 + math.tau / 6, h0, h1, god)
            fragments.append(join([frag], "Shard_%s_%d" % (god, band_index * 6 + k)))
    fragments.append(join(lid_head(kind, origin + V(0, 0, 0.33), god), "Lid_" + god))
    for fragment in fragments:
        world = fragment.matrix_world.copy()
        fragment.parent = root
        fragment.matrix_world = world

bpy.ops.object.select_all(action="DESELECT")
for root in jar_roots:
    root.select_set(True)
    for child in root.children: child.select_set(True)
bpy.context.view_layer.objects.active = jar_roots[0]
bpy.ops.export_scene.fbx(filepath=str(OUT / "CanopicJars.fbx"), use_selection=True, object_types={"EMPTY", "MESH"},
    axis_forward="-Z", axis_up="Y", global_scale=1, apply_unit_scale=True, bake_anim=False)


# ---------------------------------------------------------------- sarcophagi
for name, color, roughness, metallic in [("Sarc_Wood", (0.07, 0.045, 0.03, 1), 0.6, 0.0), ("Sarc_Inner", (0.16, 0.05, 0.03, 1), 0.9, 0.0),
                                          ("Sarc_Gold", (0.80, 0.58, 0.18, 1), 0.3, 0.85)]:
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    materials[name] = mat

# Right half of the anthropoid coffin silhouette (x, z), feet to crown.
OUTER_HALF = [(0.30, 0.0), (0.33, 0.6), (0.38, 1.2), (0.42, 1.55), (0.40, 1.75), (0.35, 1.95), (0.30, 2.08), (0.22, 2.18), (0.12, 2.24), (0.0, 2.26)]
INNER_HALF = [(0.23, TOMB_FLOOR), (0.26, 0.6), (0.31, 1.2), (0.35, 1.55), (0.33, 1.75), (0.28, 1.95), (0.23, 2.05), (0.15, 2.13), (0.06, 2.18), (0.0, 2.19)]
CASE_BACK, CASE_FRONT, LID_FRONT = -TOMB_DEPTH - 0.32, -TOMB_DEPTH + 0.30, -TOMB_DEPTH + 0.38

def outline(half): return half + [(-x, z) for x, z in reversed(half[:-1])]

def prism(name, half, y0, y1, material, offset):
    loop = outline(half)
    n = len(loop)
    verts = [tuple(offset + V(x, y0, z)) for x, z in loop] + [tuple(offset + V(x, y1, z)) for x, z in loop]
    faces = [tuple(range(n)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    obj = mesh_object(name, verts, faces, [material])
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj

sarcophagus_roots = []
for index, (god, _, _) in enumerate(GODS):
    o = V(-2.2 * index, 0, 0)
    root = bpy.data.objects.new("Sarcophagus_" + god, None)
    scene.collection.objects.link(root)
    root.location = tuple(o)
    sarcophagus_roots.append(root)
    case = prism("CaseShell", OUTER_HALF, CASE_BACK, CASE_FRONT, "Sarc_Wood", o)
    cavity = prism("Cavity", INNER_HALF, CASE_BACK + 0.06, CASE_FRONT + 0.1, "Sarc_Inner", o)
    cut = case.modifiers.new("Hollow", "BOOLEAN")
    cut.operation = "DIFFERENCE"
    cut.object = cavity
    cut.solver = "EXACT"
    cut.material_mode = "TRANSFER"
    bpy.context.view_layer.objects.active = case
    bpy.ops.object.modifier_apply(modifier=cut.name)
    bpy.data.objects.remove(cavity)
    rim = [o + V(x, CASE_FRONT, z) for x, z in outline(OUTER_HALF)]
    case_parts = [case, tube("Rim", rim, 0.014, "Sarc_Gold", 5, closed=True)]
    join(case_parts, "SarcCase_" + god)
    face_y = LID_FRONT
    lid = [prism("LidSlab", OUTER_HALF, CASE_FRONT, LID_FRONT, "Sarc_Wood", o),
           tube("LidRim", [o + V(x, LID_FRONT, z) for x, z in outline(OUTER_HALF)], 0.012, "Sarc_Gold", 5, closed=True),
           ellipsoid("Nemes", o + V(0, face_y, 1.9), (0.2, 0.025, 0.21), "Jar_" + god),
           ellipsoid("Mask", o + V(0, face_y + 0.012, 1.87), (0.1, 0.03, 0.13), "Sarc_Gold", subdivisions=2),
           ellipsoid("Collar", o + V(0, face_y, 1.56), (0.31, 0.02, 0.1), "Jar_" + god),
           box("Column", o + V(0, face_y + 0.004, 0.72), (0.09, 0.008, 0.82), "Jar_Dark"),
           band("AnkleBand", o + V(0, face_y, 0.18), 0.3, 0.012, (math.pi / 2, 0, 0), 0.01, "Jar_" + god)]
    for sign in (-1, 1):
        lid += [ellipsoid("Eye", o + V(sign * 0.035, face_y + 0.04, 1.9), (0.02, 0.008, 0.01), "Jar_Dark"),
                box("Lappet", o + V(sign * 0.15, face_y + 0.004, 1.68), (0.07, 0.016, 0.22), "Jar_" + god),
                tube("Arm", [o + V(sign * 0.27, face_y + 0.01, 1.47), o + V(sign * 0.05, face_y + 0.03, 1.36), o + V(-sign * 0.13, face_y + 0.03, 1.3)],
                     [0.038, 0.034, 0.03], "Jar_Skin", 6),
                tube("Sceptre", [o + V(-sign * 0.13, face_y + 0.05, 1.18), o + V(-sign * 0.15, face_y + 0.05, 1.45),
                                 o + V(-sign * 0.2, face_y + 0.05, 1.55)], 0.012, "Sarc_Gold", 5)]
    for k in range(4): lid.append(box("Stripe", o + V(0, face_y + 0.02, 1.98 - k * 0.07), (0.34 - k * 0.02, 0.006, 0.014), "Sarc_Gold"))
    for k in range(9): lid.append(box("Glyph", o + V(0, face_y + 0.009, 0.4 + k * 0.08), (0.03, 0.006, 0.03 if k % 2 else 0.02), "Sarc_Gold"))
    lid_obj = join(lid, "SarcLid_" + god)
    # Hinge on the coffin's right edge so the lid swings open like a door.
    scene.cursor.location = o + V(OUTER_HALF[3][0], (CASE_FRONT + LID_FRONT) * 0.5, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    scene.cursor.location = (0, 0, 0)
    for child in (bpy.data.objects["SarcCase_" + god], lid_obj):
        world = child.matrix_world.copy()
        child.parent = root
        child.matrix_world = world

bpy.ops.object.select_all(action="DESELECT")
for root in sarcophagus_roots:
    root.select_set(True)
    for child in root.children: child.select_set(True)
bpy.context.view_layer.objects.active = sarcophagus_roots[0]
bpy.ops.export_scene.fbx(filepath=str(OUT / "Sarcophagi.fbx"), use_selection=True, object_types={"EMPTY", "MESH"},
    axis_forward="-Z", axis_up="Y", global_scale=1, apply_unit_scale=True, bake_anim=False)
sarc_tris = [sum(sum(len(p.vertices) - 2 for p in child.data.polygons) for child in root.children) for root in sarcophagus_roots]
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "Blender/Mummy/Mummy.blend"))
mummy_tris = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj, _ in pieces.values())
jar_tris = [sum(sum(len(p.vertices) - 2 for p in child.data.polygons) for child in root.children if child.type == "MESH") for root in jar_roots]
stats = {"mummy_pieces": len(pieces), "mummy_triangles": mummy_tris, "bones": len(rig.data.bones), "jar_triangles": jar_tris, "sarcophagus_triangles": sarc_tris,
         "gods": [{"name": g, "head": k, "colour": list(c[:3])} for g, k, c in GODS],
         "clips": [{"name": n, "first": a, "last": b, "loop": l} for n, a, b, l in CLIPS],
         "piece_bones": {name: bone for name, (_, bone) in pieces.items()}, "expected_centres": expected}
(PREVIEW / "model-stats.json").write_text(json.dumps(stats, indent=2))
(OUT / "Mummy.clips.json").write_text(json.dumps({"fps": scene.render.fps, "clips": stats["clips"]}, indent=2))
print("MUMMY_STATS", json.dumps({k: stats[k] for k in ("mummy_pieces", "mummy_triangles", "bones", "jar_triangles", "sarcophagus_triangles")}))

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
    camera_data = bpy.data.cameras.new("MummyPreview")
    camera = bpy.data.objects.new("MummyPreview", camera_data)
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
    shots = [("mummy-idle", 0, (2.6, 3.2, 1.5), (0, 0, 0.9), 2.2), ("mummy-face", 0, (0.5, 1.2, 1.75), (0, 0, 1.6), 0.6),
             ("mummy-shamble", 76, (4, 0.3, 1.0), (0, 0, 0.9), 2.2), ("mummy-chase", 134, (4, 0.3, 1.0), (0, 0, 0.9), 2.2),
             ("mummy-bite", 176, (3.0, 2.0, 1.4), (0, 0.2, 1.1), 2.2), ("mummy-lying", 244, (2.8, 1.5, 2.2), (0, 0, 0.3), 2.6),
             ("mummy-rise-sit", 304, (3.2, 1.0, 1.2), (0, -0.2, 0.5), 2.4), ("mummy-rise-kneel", 319, (3.2, 1.0, 1.2), (0, 0, 0.6), 2.4),
             ("mummy-entombed", 360, (2.2, 2.6, 1.6), (0, -0.95, 1.1), 2.6), ("mummy-walkout-step", 443, (3.2, 1.2, 1.3), (0, -0.5, 1.0), 2.8),
             ("sarcophagi-closed", 0, (-3.3, 5.5, 1.6), (-3.3, -0.95, 1.1), 9.0), ("canopic-jars", 0, (8.35, 3.0, 1.2), (8.35, 0, 1.05), 4.0), ("canopic-jar-heads", 0, (8.35, 1.6, 1.45), (8.35, 0, 1.33), 3.6)]
    open_lid = bpy.data.objects["SarcLid_" + GODS[0][0]]
    for name, frame, pos, target, scale in shots:
        open_lid.rotation_euler.z = 0.0 if name.startswith("sarcophagi") else math.radians(-105)
        scene.frame_set(frame)
        camera.location = pos
        camera.rotation_euler = (V(*target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera_data.ortho_scale = scale
        scene.render.filepath = str(PREVIEW / (name + ".png"))
        bpy.ops.render.render(write_still=True)
    scene.frame_set(0)
