"""Builds the thief: an original low-poly character with a simple skeleton. Unity game.

Run:  blender --background --factory-startup --python Blender/Intro/build_thief.py
Writes Blender/Intro/MuseumThief.blend (model only). animate_thief.py adds the performance and exports it.
"""
import bmesh
import bpy
import os
from math import cos, pi, sin
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))

# Blender space: the character faces -Y, its left is +X.
BONES = {
    "Root": ((0, 0, 0), (0, 0, 0.2), None),
    "Hips": ((0, 0, 0.93), (0, 0, 1.05), "Root"),
    "Spine": ((0, 0, 1.05), (0, 0, 1.22), "Hips"),
    "Chest": ((0, 0, 1.22), (0, 0, 1.45), "Spine"),
    "Neck": ((0, 0, 1.45), (0, 0, 1.54), "Chest"),
    "Head": ((0, 0, 1.54), (0, 0, 1.78), "Neck"),
}
for side, s in (("L", 1), ("R", -1)):
    BONES["UpperArm_" + side] = ((s * 0.20, 0, 1.40), (s * 0.20, 0, 1.11), "Chest")
    BONES["LowerArm_" + side] = ((s * 0.20, 0, 1.11), (s * 0.20, 0, 0.84), "UpperArm_" + side)
    BONES["Hand_" + side] = ((s * 0.20, 0, 0.84), (s * 0.20, 0, 0.68), "LowerArm_" + side)
    BONES["UpperLeg_" + side] = ((s * 0.10, 0, 0.93), (s * 0.10, 0, 0.50), "Hips")
    BONES["LowerLeg_" + side] = ((s * 0.10, 0, 0.50), (s * 0.10, 0, 0.09), "UpperLeg_" + side)
    BONES["Foot_" + side] = ((s * 0.10, 0, 0.09), (s * 0.10, -0.17, 0.03), "LowerLeg_" + side)

MATERIALS = {
    "Thief_Jacket": (0.030, 0.040, 0.058), "Thief_Trousers": (0.045, 0.044, 0.048), "Thief_Mask": (0.012, 0.012, 0.014),
    "Thief_Skin": (0.42, 0.23, 0.14), "Thief_Gloves": (0.018, 0.015, 0.013), "Thief_Boots": (0.03, 0.02, 0.014),
    "Thief_Bag": (0.16, 0.10, 0.045), "Thief_Strap": (0.07, 0.045, 0.02), "Thief_Eye": (0.75, 0.75, 0.72),
    "Thief_Pupil": (0.01, 0.01, 0.01), "Thief_Metal": (0.35, 0.34, 0.30),
}


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.deform = self.bm.verts.layers.deform.verify()
        self.groups = [name for name in BONES if name != "Root"]
        self.slots = list(MATERIALS)

    def weigh(self, vert, weights):
        total = sum(weights.values())
        for bone, value in weights.items():
            if value > 0.001: vert[self.deform][self.groups.index(bone)] = value / total

    def loft(self, rings, weights, material, segments=14, caps=True):
        """rings: list of (centre, radius x, radius y[, axes]). weights(i) -> {bone: weight}."""
        rows = []
        for i, ring in enumerate(rings):
            centre, rx, ry = ring[:3]
            ax, ay = ring[3] if len(ring) > 3 else (Vector((1, 0, 0)), Vector((0, 1, 0)))
            row = []
            for k in range(segments):
                a = 2 * pi * k / segments
                v = self.bm.verts.new(Vector(centre) + ax * (rx * cos(a)) + ay * (ry * sin(a)))
                self.weigh(v, weights(i))
                row.append(v)
            rows.append(row)
        faces = []
        for a, b in zip(rows, rows[1:]):
            for k in range(segments):
                faces.append(self.bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k])))
        if caps:
            faces.append(self.bm.faces.new(list(reversed(rows[0]))))
            faces.append(self.bm.faces.new(rows[-1]))
        for f in faces:
            f.material_index = self.slots.index(material)
            f.smooth = True
        return faces

    def blob(self, centre, size, bone, material, u=16, v=10, rotation=None):
        matrix = Matrix.Translation(centre) @ (rotation or Matrix()) @ Matrix.Diagonal((*size, 1))
        made = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=1.0, matrix=matrix)
        faces = {f for vert in made["verts"] for f in vert.link_faces}
        for vert in made["verts"]: self.weigh(vert, {bone: 1.0})
        for f in faces:
            f.material_index = self.slots.index(material)
            f.smooth = True
        return faces

    def box(self, centre, size, bone, material, bevel=0.02, rotation=None):
        matrix = Matrix.Translation(centre) @ (rotation or Matrix()) @ Matrix.Diagonal((*size, 1))
        made = bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix)
        edges = {e for vert in made["verts"] for e in vert.link_edges}
        result = bmesh.ops.bevel(self.bm, geom=list(edges), offset=bevel, segments=2, affect="EDGES", profile=0.6)
        verts = set(result["verts"]) | {v for v in made["verts"] if v.is_valid}
        for f in {f for vert in verts for f in vert.link_faces}:
            f.material_index = self.slots.index(material)
            f.smooth = True
            for vert in f.verts:
                if not len(vert[self.deform]): self.weigh(vert, {bone: 1.0})


def blend(position, joints):
    """Weights along a limb. joints: [(bone, start)], blending over 5 cm around each start."""
    weights = {}
    for i, (bone, start) in enumerate(joints):
        enter = 1.0 if i == 0 else max(0.0, min(1.0, (position - start + 0.05) / 0.10))
        leave = 0.0 if i == len(joints) - 1 else max(0.0, min(1.0, (position - joints[i + 1][1] + 0.05) / 0.10))
        weights[bone] = enter * (1 - leave)
    return weights


def build_mesh(b):
    # Jacket: hem at the hips up to the collar.
    torso = [(0.81, 0.170, 0.125), (0.85, 0.190, 0.140), (0.92, 0.182, 0.134), (1.00, 0.166, 0.120), (1.08, 0.160, 0.114),
             (1.17, 0.172, 0.118), (1.26, 0.192, 0.126), (1.34, 0.206, 0.126), (1.395, 0.205, 0.114), (1.435, 0.172, 0.098),
             (1.465, 0.112, 0.086), (1.50, 0.078, 0.076)]
    b.loft([((0, 0.004, z), rx, ry) for z, rx, ry in torso],
           lambda i: blend(torso[i][0], [("Hips", 0), ("Spine", 1.05), ("Chest", 1.22), ("Neck", 1.49)]), "Thief_Jacket", 20)
    hips = [(0.80, 0.135, 0.10), (0.86, 0.165, 0.116), (0.93, 0.165, 0.116)]
    b.loft([((0, 0.004, z), rx, ry) for z, rx, ry in hips], lambda i: {"Hips": 1}, "Thief_Trousers", 20)
    # Turned-up collar and a ribbed hem.
    collar = [(1.440, 0.118, 0.098), (1.475, 0.100, 0.092), (1.515, 0.092, 0.090), (1.535, 0.088, 0.088), (1.535, 0.074, 0.074), (1.50, 0.066, 0.066)]
    b.loft([((0, 0.004, z), rx, ry) for z, rx, ry in collar], lambda i: {"Neck": 0.6, "Chest": 0.4} if i else {"Chest": 1}, "Thief_Jacket", 20)
    hem = [(0.795, 0.176, 0.132), (0.80, 0.196, 0.146), (0.845, 0.196, 0.146), (0.855, 0.184, 0.136)]
    b.loft([((0, 0.004, z), rx, ry) for z, rx, ry in hem], lambda i: {"Hips": 1}, "Thief_Strap", 20)
    # Head in a balaclava, with a skin-coloured eye slit.
    head = b.blob(Vector((0, -0.005, 1.665)), (0.103, 0.118, 0.135), "Head", "Thief_Mask", 24, 18)
    for f in head:
        c = f.calc_center_median()
        if c.y < -0.06 and 1.665 < c.z < 1.715 and abs(c.x) < 0.082: f.material_index = b.slots.index("Thief_Skin")
    b.loft([((0, 0, 1.46), 0.066, 0.066), ((0, 0, 1.52), 0.062, 0.064), ((0, -0.003, 1.58), 0.066, 0.070)],
           lambda i: {"Neck": 1}, "Thief_Mask", 14)
    b.blob(Vector((0, -0.115, 1.655)), (0.014, 0.016, 0.028), "Head", "Thief_Mask", 10, 8)
    for s in (1, -1):
        b.blob(Vector((s * 0.040, -0.098, 1.690)), (0.0165, 0.014, 0.0125), "Head", "Thief_Eye", 10, 8)
        b.blob(Vector((s * 0.040, -0.110, 1.690)), (0.0075, 0.005, 0.0085), "Head", "Thief_Pupil", 8, 6)
        b.box(Vector((s * 0.041, -0.108, 1.711)), (0.044, 0.014, 0.009), "Head", "Thief_Pupil", 0.003,
              Matrix.Rotation(s * -0.25, 4, "Y"))
    for side, s in (("L", 1), ("R", -1)):
        arm = [(0.00, 0.0640), (0.05, 0.0645), (0.12, 0.0605), (0.20, 0.0545), (0.27, 0.052), (0.29, 0.051), (0.32, 0.051),
               (0.38, 0.049), (0.48, 0.041), (0.545, 0.039), (0.56, 0.043), (0.575, 0.043)]
        b.blob(Vector((s * 0.20, 0, 1.402)), (0.0650, 0.0650, 0.0620), "UpperArm_" + side, "Thief_Jacket", 14, 10)
        b.loft([((s * 0.20, 0, 1.40 - d), r, r) for d, r in arm],
               lambda i: blend(arm[i][0], [("UpperArm_" + side, 0), ("LowerArm_" + side, 0.29)]), "Thief_Jacket", 12)
        hand = "Hand_" + side
        b.loft([((s * 0.20, 0, 0.85), 0.034, 0.034), ((s * 0.20, 0, 0.80), 0.026, 0.043), ((s * 0.20, -0.002, 0.75), 0.024, 0.046),
                ((s * 0.20, -0.004, 0.70), 0.020, 0.040), ((s * 0.20, -0.006, 0.67), 0.013, 0.026)],
               lambda i: {hand: 1}, "Thief_Gloves", 12)
        b.blob(Vector((s * 0.188, -0.050, 0.765)), (0.016, 0.017, 0.042), hand, "Thief_Gloves", 8, 8, Matrix.Rotation(0.45, 4, "X"))
        leg = [(0.00, 0.096), (0.06, 0.098), (0.16, 0.088), (0.28, 0.074), (0.38, 0.066), (0.43, 0.064), (0.48, 0.064),
               (0.56, 0.063), (0.66, 0.052), (0.74, 0.046), (0.77, 0.050)]
        b.loft([((s * 0.10, 0, 0.93 - d), r, r * 1.04) for d, r in leg],
               lambda i: blend(leg[i][0], [("UpperLeg_" + side, 0), ("LowerLeg_" + side, 0.43)]), "Thief_Trousers", 12)
        foot, shin = "Foot_" + side, "LowerLeg_" + side
        shaft = [(0.25, 0.056, 0.058), (0.22, 0.059, 0.062), (0.12, 0.052, 0.058), (0.06, 0.050, 0.058)]
        shares = [{shin: 1}, {shin: 1}, {foot: 0.7, shin: 0.3}, {foot: 1}]
        b.loft([((s * 0.10, 0.004, z), rx, ry) for z, rx, ry in shaft], lambda i: shares[i], "Thief_Boots", 12)
        across, up = Vector((1, 0, 0)), Vector((0, 0, 1))
        boot = [(0.066, 0.052, 0.042, 0.046), (0.03, 0.055, 0.056, 0.058), (-0.06, 0.056, 0.054, 0.056),
                (-0.12, 0.057, 0.042, 0.045), (-0.17, 0.050, 0.034, 0.037), (-0.200, 0.030, 0.022, 0.028)]
        b.loft([((s * 0.10, y, z), rx, rz, (across, up)) for y, rx, rz, z in boot], lambda i: {foot: 1}, "Thief_Boots", 12)
        b.box(Vector((s * 0.10, -0.062, 0.010)), (0.112, 0.262, 0.020), foot, "Thief_Pupil", 0.008)
        # Bag straps over the shoulders. Pairs are (forward, height).
        strap = [(0.10, 1.00), (0.120, 1.12), (0.126, 1.25), (0.122, 1.36), (0.075, 1.43), (0.0, 1.462), (-0.075, 1.425), (-0.122, 1.36)]
        for (y0, z0), (y1, z1) in zip(strap, strap[1:]):
            p0, p1 = Vector((s * 0.105, -y0 + 0.002, z0)), Vector((s * 0.105, -y1 + 0.002, z1))
            along = p1 - p0
            b.box((p0 + p1) / 2, (0.042, 0.014, along.length + 0.015), "Chest" if min(z0, z1) > 1.1 else "Spine", "Thief_Strap", 0.005,
                  along.to_track_quat("Z", "Y").to_matrix().to_4x4())
    # Empty loot bag on the back.
    b.box(Vector((0, 0.185, 1.20)), (0.30, 0.15, 0.38), "Chest", "Thief_Bag", 0.055)
    b.box(Vector((0, 0.255, 1.13)), (0.21, 0.06, 0.18), "Chest", "Thief_Bag", 0.025)
    b.box(Vector((0, 0.190, 1.375)), (0.27, 0.15, 0.07), "Chest", "Thief_Strap", 0.03)
    b.box(Vector((0, 0.290, 1.20)), (0.035, 0.012, 0.030), "Chest", "Thief_Metal", 0.004)
    b.blob(Vector((0, 0.20, 1.43)), (0.12, 0.045, 0.045), "Chest", "Thief_Bag", 14, 8)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    data = bpy.data.armatures.new("Thief_Rig")
    rig = bpy.data.objects.new("Thief_Rig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, parent) in BONES.items():
        bone = data.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent: bone.parent = data.edit_bones[parent]
        bone.roll = 0
        bone.use_deform = name != "Root"
    bpy.ops.object.mode_set(mode="OBJECT")

    b = Builder()
    build_mesh(b)
    bmesh.ops.recalc_face_normals(b.bm, faces=b.bm.faces)
    mesh = bpy.data.meshes.new("Thief_Mesh")
    b.bm.to_mesh(mesh)
    body = bpy.data.objects.new("Thief_Mesh", mesh)
    bpy.context.scene.collection.objects.link(body)
    for name, colour in MATERIALS.items():
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        shader = material.node_tree.nodes["Principled BSDF"]
        shader.inputs["Base Color"].default_value = (*colour, 1)
        shader.inputs["Roughness"].default_value = 0.75
        material.diffuse_color = (*colour, 1)
        mesh.materials.append(material)
    for name in b.groups: body.vertex_groups.new(name=name)
    body.parent = rig
    modifier = body.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    unweighted = [v.index for v in mesh.vertices if not v.groups]
    print("THIEF vertices", len(mesh.vertices), "triangles", sum(len(p.vertices) - 2 for p in mesh.polygons),
          "unweighted", len(unweighted))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "MuseumThief.blend"))


if __name__ == "__main__":
    main()
