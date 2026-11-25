"""Builds and animates the getaway van and the ladder for the intro cutscene. Unity game.

Run:  blender --background --factory-startup --python Blender/Intro/build_van.py
Writes HeistVan.blend and HeistLadder.blend, and exports both FBX files with their animation.

Model space is the same as the thief: the van faces -Y, its left side is +X and it stands on Z = 0.
"""
import bmesh
import bpy
import os
import sys
from math import cos, pi, radians, sin
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from intro_layout import *  # noqa: E402

MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Models", "Intro"))
MATERIALS = {
    "Van_Paint": (0.075, 0.095, 0.115), "Van_Trim": (0.02, 0.02, 0.022), "Van_Glass": (0.015, 0.02, 0.025), "Van_Tyre": (0.012, 0.012, 0.012),
    "Van_Rim": (0.30, 0.30, 0.31), "Van_Lamp": (0.95, 0.90, 0.75), "Van_Tail": (0.45, 0.02, 0.02), "Van_Interior": (0.05, 0.045, 0.04),
    "Van_Plate": (0.60, 0.52, 0.12), "Ladder_Metal": (0.50, 0.51, 0.52), "Ladder_Foot": (0.03, 0.03, 0.03),
}
HALF_WIDTH, LENGTH_M = 0.95, 5.0
# Side profile (distance forward of the centre, height). The nose is at +2.5.
PROFILE = [(-2.48, 0.42), (-2.50, 0.60), (-2.46, 1.95), (-2.36, 2.18), (-2.10, 2.24), (0.95, 2.24), (1.22, 2.16), (1.92, 1.36),
           (2.34, 1.16), (2.46, 1.04), (2.50, 0.62), (2.46, 0.42)]
AXLES = (1.60, -1.55)
WHEEL_R = 0.34
DOOR = (-0.95, 0.25)                # opening, forward of the centre: DOOR_X is 0.35 m behind the van centre
SILL, DOOR_TOP = VAN_FLOOR - 0.04, 1.98


def material(name):
    found = bpy.data.materials.get(name)
    if found: return found
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    made.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*MATERIALS[name], 1)
    made.diffuse_color = (*MATERIALS[name], 1)
    return made


def make(name, build, slots, bevel=0.0):
    bm = bmesh.new()
    build(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for slot in slots: mesh.materials.append(material(slot))
    o = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(o)
    if bevel:
        m = o.modifiers.new("Bevel", "BEVEL")
        m.width, m.segments, m.limit_method, m.angle_limit = bevel, 2, "ANGLE", radians(28)
        m.harden_normals = False
    return o


def box(bm, low, high, slot=0):
    made = bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((Vector(low) + Vector(high)) / 2)
                                 @ Matrix.Diagonal((*(Vector(high) - Vector(low)), 1)))
    for f in {f for v in made["verts"] for f in v.link_faces}: f.material_index = slot


def tube(bm, start, end, radius, slot=0, segments=10):
    start, end = Vector(start), Vector(end)
    matrix = Matrix.Translation((start + end) / 2) @ (end - start).to_track_quat("Z", "Y").to_matrix().to_4x4()
    made = bmesh.ops.create_cone(bm, segments=segments, radius1=radius, radius2=radius, depth=(end - start).length, cap_ends=True, matrix=matrix)
    for f in {f for v in made["verts"] for f in v.link_faces}:
        f.material_index = slot
        f.smooth = len(f.verts) == 4


def prism(bm, outline, low, high, slot=0):
    """Extrudes a side outline of (forward, height) points across the van."""
    rows = [[bm.verts.new((x, -f, z)) for f, z in outline] for x in (low, high)]
    n = len(outline)
    faces = [bm.faces.new(rows[0]), bm.faces.new(list(reversed(rows[1])))]
    for i in range(n): faces.append(bm.faces.new((rows[0][i], rows[1][i], rows[1][(i + 1) % n], rows[0][(i + 1) % n])))
    for f in faces: f.material_index = slot


def cut(target, tool):
    m = target.modifiers.new("Cut", "BOOLEAN")
    m.operation, m.object, m.solver = "DIFFERENCE", tool, "EXACT"
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(tool)


def apply(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    for p in o.data.polygons: p.use_smooth = True
    with bpy.context.temp_override(object=o, selected_objects=[o], selected_editable_objects=[o], active_object=o):
        bpy.ops.object.shade_auto_smooth(angle=radians(35))
    weighted = o.modifiers.new("Normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)


def build_van():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    w = HALF_WIDTH
    body = make("Van_Body", lambda bm: prism(bm, PROFILE, -w, w), ["Van_Paint", "Van_Interior"])
    # The cabin and cargo space are hollow; the cargo door opening is cut through the left side.
    inner = [(-2.38, VAN_FLOOR), (-2.36, 1.92), (-2.28, 2.10), (-2.08, 2.16), (0.90, 2.16), (1.16, 2.09), (1.62, 1.52), (1.62, VAN_FLOOR)]
    cut(body, make("Inner", lambda bm: prism(bm, inner, -w + 0.07, w - 0.07, 0), ["Van_Interior"]))
    cut(body, make("Opening", lambda bm: box(bm, (w - 0.2, -DOOR[1], SILL + 0.04), (w + 0.2, -DOOR[0], DOOR_TOP)), ["Van_Interior"]))
    for forward in AXLES:
        for s in (1, -1):
            cut(body, make("Arch", lambda bm: tube(bm, (s * (w - 0.30), -forward, WHEEL_R), (s * (w + 0.1), -forward, WHEEL_R), WHEEL_R + 0.085, 0, 24), ["Van_Interior"]))
    m = body.modifiers.new("Bevel", "BEVEL")
    m.width, m.segments, m.limit_method, m.angle_limit = 0.035, 3, "ANGLE", radians(35)

    def trim(bm):
        # Bumpers, sills, grille, mirrors, handles, roof rack and lamps. Slots follow the list below.
        box(bm, (-w + 0.02, -2.58, 0.36), (w - 0.02, -2.40, 0.60), 0)
        box(bm, (-w + 0.02, 2.42, 0.36), (w - 0.02, 2.57, 0.58), 0)
        for s in (1, -1):
            box(bm, (s * w - 0.02, -1.14, 0.34), (s * w + 0.02, 1.10, 0.44), 0)
            box(bm, (min(s * w, s * (w + 0.20)), -1.60, 1.30), (max(s * w, s * (w + 0.20)), -1.50, 1.36), 0)
            box(bm, (min(s * (w + 0.10), s * (w + 0.22)), -1.66, 1.28), (max(s * (w + 0.10), s * (w + 0.22)), -1.56, 1.58), 0)
            box(bm, (min(s * 0.52, s * 0.86), -2.515, 0.80), (max(s * 0.52, s * 0.86), -2.44, 0.98), 1)
            box(bm, (min(s * 0.76, s * 0.90), 2.44, 0.95), (max(s * 0.76, s * 0.90), 2.515, 1.45), 2)
            box(bm, (s * w - 0.01, -1.02, 1.05), (s * w + 0.02, -0.88, 1.09), 0)
            tube(bm, (s * 0.80, -0.85, 2.33), (s * 0.80, 2.15, 2.33), 0.02, 0)
        for y in (-0.6, 0.65, 1.9):
            tube(bm, (-0.80, y, 2.33), (0.80, y, 2.33), 0.018, 0)
            for s in (1, -1): tube(bm, (s * 0.80, y, 2.22), (s * 0.80, y, 2.33), 0.018, 0)
        for k in range(4): box(bm, (-0.46, -2.515, 0.72 + k * 0.075), (0.46, -2.46, 0.755 + k * 0.075), 0)
        box(bm, (-0.26, -2.60, 0.43), (0.26, -2.57, 0.54), 3)
        box(bm, (-0.26, 2.56, 0.43), (0.26, 2.585, 0.54), 3)
        box(bm, (-0.012, 2.46, 0.65), (0.012, 2.505, 2.05), 0)
        tube(bm, (-0.30, 2.50, 0.30), (-0.30, 2.66, 0.28), 0.03, 0)
    trim_o = make("Van_Trim", trim, ["Van_Trim", "Van_Lamp", "Van_Tail", "Van_Plate"], 0.008)

    def glass(bm):
        # Windscreen lies on the profile between (1.22, 2.16) and (1.92, 1.36).
        def on(u, out): return Vector((0, -(1.22 + 0.70 * u), 2.16 - 0.80 * u)) + Vector((0, -0.75, 0.66)) * out
        a, c = on(0.09, 0.006), on(0.93, 0.006)
        vs = [bm.verts.new((s * width, p.y, p.z)) for p, width, s in ((a, 0.80, -1), (a, 0.80, 1), (c, 0.86, 1), (c, 0.86, -1))]
        bm.faces.new(vs)
        for s in (1, -1):
            x = s * (w + 0.005)
            side = [(0.42, 1.98), (1.12, 1.98), (1.68, 1.36), (0.42, 1.36)]
            bm.faces.new([bm.verts.new((x, -f, z)) for f, z in side])
            x0, x1 = s * 0.10, s * 0.80
            bm.faces.new([bm.verts.new((x, 2.488, z)) for x, z in ((x0, 1.40), (x1, 1.40), (x1, 1.92), (x0, 1.92))])
    glass_o = make("Van_Glass", glass, ["Van_Glass"])
    m = glass_o.modifiers.new("Solid", "SOLIDIFY")
    m.thickness, m.offset = 0.012, 0

    def door(bm):
        box(bm, (w - 0.055, -DOOR[1] - 0.02, SILL), (w + 0.012, -DOOR[0] + 0.02, DOOR_TOP + 0.03), 0)
        box(bm, (w + 0.008, -DOOR[1] + 0.08, 1.06), (w + 0.04, -DOOR[1] + 0.24, 1.10), 1)
        box(bm, (w + 0.006, -DOOR[1] + 0.03, 0.80), (w + 0.018, -DOOR[0] - 0.03, 0.83), 1)
    door_o = make("Van_Door", door, ["Van_Paint", "Van_Trim"], 0.015)

    def seats(bm):
        for s in (1, -1):
            box(bm, (s * 0.45 - 0.25, -1.05, VAN_FLOOR), (s * 0.45 + 0.25, -0.50, 0.95), 0)
            box(bm, (s * 0.45 - 0.25, -0.62, 0.95), (s * 0.45 + 0.25, -0.46, 1.65), 0)
        for s in (1, -1):                  # covers over the wheel arches
            for forward in AXLES:
                box(bm, (min(s * (w - 0.40), s * (w - 0.04)), -forward - 0.47, 0.40), (max(s * (w - 0.40), s * (w - 0.04)), -forward + 0.47, 0.82), 0)
        box(bm, (-0.45, 1.55, VAN_FLOOR), (0.0, 2.2, 1.05), 1)
        box(bm, (0.04, 1.75, VAN_FLOOR), (0.48, 2.2, 0.86), 1)
    seats_o = make("Van_Seats", seats, ["Van_Interior", "Van_Trim"], 0.02)

    def spare_ladder(bm): ladder_mesh(bm, Vector((0, -0.95, 2.40)), Vector((0, 1, 0)), Vector((1, 0, 0)), 3.3)
    rack_o = make("Van_RackLadder", spare_ladder, ["Ladder_Metal", "Ladder_Foot"])

    wheels = []
    for i, forward in enumerate(AXLES):
        for s in (1, -1):
            def wheel(bm, s=s, forward=forward):
                c = Vector((s * (w - 0.13), -forward, WHEEL_R))
                side = Vector((s, 0, 0))
                tube(bm, c - side * 0.11, c + side * 0.11, WHEEL_R, 0, 28)
                tube(bm, c + side * 0.08, c + side * 0.118, 0.215, 1, 20)
                tube(bm, c + side * 0.10, c + side * 0.130, 0.06, 0, 12)
                for k in range(5):
                    a = 2 * pi * k / 5
                    p = c + Vector((0, cos(a), sin(a))) * 0.145
                    tube(bm, p + side * 0.10, p + side * 0.124, 0.036, 0, 10)
            wheels.append((("Front" if i == 0 else "Rear") + ("_L" if s > 0 else "_R"), make("Van_Wheel_%d%d" % (i, s), wheel, ["Van_Tyre", "Van_Rim"], 0.012),
                           Vector((s * (w - 0.13), -forward, WHEEL_R))))
    parts = [body, trim_o, glass_o, door_o, seats_o, rack_o] + [o for _, o, _ in wheels]
    for o in parts: apply(o)

    data = bpy.data.armatures.new("Van_Rig")
    rig = bpy.data.objects.new("Van_Rig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    places = {"Van": Vector((0, 0, 0)), "Body": Vector((0, 0, 0.5)), "Door": Vector((w, 0.35, 1.2))}
    for name, _, centre in wheels: places["Wheel_" + name] = centre
    for name, at in places.items():
        bone = data.edit_bones.new(name)
        bone.head, bone.tail, bone.roll = at, at + Vector((0, 0, 0.3)), 0
        if name != "Van": bone.parent = data.edit_bones["Body" if name == "Door" else "Van"]
    bpy.ops.object.mode_set(mode="OBJECT")

    def bind(o, bone):
        o.vertex_groups.new(name=bone).add(range(len(o.data.vertices)), 1.0, "REPLACE")
        o.modifiers.new("Armature", "ARMATURE").object = rig
        o.parent = rig
    for o in parts[:6]: bind(o, "Door" if o is door_o else "Body")
    for name, o, _ in wheels: bind(o, "Wheel_" + name)
    animate_van(rig)
    save_and_export("HeistVan", rig, parts)


def animate_van(rig):
    start_x, stop_x = 27.0, VAN_STOP[0]
    stop_time, total = 3.7, 240
    scene = bpy.context.scene
    scene.render.fps, scene.frame_start, scene.frame_end = FPS, 0, total
    action = bpy.data.actions.new("Van_Intro")
    action.use_fake_user = True
    rig.animation_data_create().action = action
    bones = rig.pose.bones
    for bone in bones: bone.rotation_mode = "XYZ"
    travel = start_x - stop_x

    def distance(t):
        # Constant speed, then steady braking to a stop.
        brake = 1.9
        speed = travel / (stop_time - brake / 2)
        if t <= stop_time - brake: return speed * t
        u = min(t, stop_time) - (stop_time - brake)
        return speed * (stop_time - brake) + speed * u - 0.5 * speed / brake * u * u

    def door(t):
        u = max(0.0, min(1.0, (t - 4.75) / 0.75))
        return u * u * (3 - 2 * u)
    # Facing -X in Unity: yaw 270, which is +90 about Blender Z. Bone axes: local Y is up.
    for frame in range(total + 1):
        t = frame / FPS
        x = start_x - distance(t)
        where = unity(x, 0, VAN_STOP[2])
        turn = Matrix.Rotation(radians(-270), 4, "Z")
        rest = rig.data.bones["Van"].matrix_local
        basis = rest.inverted() @ Matrix.Translation(where) @ turn @ rest
        bones["Van"].location = basis.translation
        bones["Van"].rotation_euler = basis.to_euler("XYZ")
        # The nose dips while braking and rocks back once it stops.
        dip = 0.0
        if stop_time - 1.9 < t <= stop_time: dip = 1.5 * min(1.0, (t - stop_time + 1.9) / 0.4)
        elif t > stop_time: dip = 1.5 * cos((t - stop_time) * 8.0) * 2.718 ** (-(t - stop_time) * 3.5)
        sway = 0.9 * sin(max(0.0, t - 5.9) * 9.0) * 2.718 ** (-max(0.0, t - 5.9) * 4.0) if t > 5.9 else 0.0
        local = rig.data.bones["Body"].matrix_local.to_3x3()
        tilt = local.inverted() @ Matrix.Rotation(radians(dip), 3, "X") @ Matrix.Rotation(radians(sway), 3, "Y") @ local
        bones["Body"].rotation_euler = tilt.to_euler("XYZ")
        bones["Body"].location = local.inverted() @ Vector((0, 0, 0.006 * sin(t * 31.0) if t < stop_time else 0))
        slide = door(t)
        rest3 = rig.data.bones["Door"].matrix_local.to_3x3()
        bones["Door"].location = rest3.inverted() @ Vector((0.085 * min(1.0, slide * 6), 1.18 * max(0.0, slide - 0.06) / 0.94, 0))
        roll = distance(t) / WHEEL_R
        for name in ("Front_L", "Front_R", "Rear_L", "Rear_R"):
            r = rig.data.bones["Wheel_" + name].matrix_local.to_3x3()
            spin = r.inverted() @ Matrix.Rotation(roll, 3, "X") @ r
            bones["Wheel_" + name].rotation_mode = "QUATERNION"
            bones["Wheel_" + name].rotation_quaternion = spin.to_quaternion()
            bones["Wheel_" + name].keyframe_insert("rotation_quaternion", frame=frame)
        for name in ("Van", "Body", "Door"):
            bones[name].keyframe_insert("location", frame=frame)
            if name != "Door": bones[name].keyframe_insert("rotation_euler", frame=frame)
    linear(action)


def ladder_mesh(bm, base, up, side, length, outward=None):
    for s in (1, -1):
        rail = base + side * (0.215 * s)
        tube(bm, rail, rail + up * length, 0.024, 0, 8)
        tube(bm, rail - up * 0.02, rail + up * 0.07, 0.034, 1, 8)
        tube(bm, rail + up * (length - 0.06), rail + up * (length + 0.005), 0.030, 1, 8)
    for k in range(1, int((length - 0.08) / RUNG) + 1):
        tube(bm, base + up * (k * RUNG) - side * 0.215, base + up * (k * RUNG) + side * 0.215, 0.017, 0, 8)


def build_ladder():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    o = make("Ladder_Mesh", lambda bm: ladder_mesh(bm, Vector((0, 0, 0)), Vector((0, 0, 1)), Vector((1, 0, 0)), LADDER_LENGTH), ["Ladder_Metal", "Ladder_Foot"])
    data = bpy.data.armatures.new("Ladder_Rig")
    rig = bpy.data.objects.new("Ladder_Rig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    bone = data.edit_bones.new("Ladder")
    bone.head, bone.tail, bone.roll = (0, 0, 0), (0, 0, 0.5), 0
    bpy.ops.object.mode_set(mode="OBJECT")
    o.vertex_groups.new(name="Ladder").add(range(len(o.data.vertices)), 1.0, "REPLACE")
    o.modifiers.new("Armature", "ARMATURE").object = rig
    o.parent = rig
    total = 390
    scene = bpy.context.scene
    scene.render.fps, scene.frame_start, scene.frame_end = FPS, 0, total
    action = bpy.data.actions.new("Ladder_Intro")
    action.use_fake_user = True
    rig.animation_data_create().action = action
    pose = rig.pose.bones["Ladder"]
    pose.rotation_mode = "QUATERNION"
    rest = data.bones["Ladder"].matrix_local
    for frame in range(total + 1):
        t = frame / FPS
        # Positive angle tips the top north (Unity +Z), which is Blender -Y.
        world = Matrix.Translation(unity(*LADDER_BASE)) @ Matrix.Rotation(radians(ladder_angle(t)), 4, "X")
        basis = rest.inverted() @ world @ rest
        pose.location, pose.rotation_quaternion = basis.translation, basis.to_quaternion()
        pose.keyframe_insert("location", frame=frame)
        pose.keyframe_insert("rotation_quaternion", frame=frame)
    linear(action)
    save_and_export("HeistLadder", rig, [o])


def linear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points: key.interpolation = "LINEAR"


def save_and_export(name, rig, meshes):
    print(name, "triangles", sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, name + ".blend"))
    os.makedirs(MODELS, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in [rig] + meshes: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, use_mesh_modifiers=True)


build_van()
build_ladder()
