"""Re-import Mummy.fbx into an empty scene and compare every wrapped piece's
position with the build script's record. Run after build_mummy.py:
Blender --background --factory-startup --python Blender/Mummy/verify_fbx.py
"""
import bpy
import json
import sys
from mathutils import Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
stats = json.loads((ROOT / "Temp/MummyVerification/model-stats.json").read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT / "Assets/_Project/Models/Mummy/Mummy.fbx"))
bpy.context.scene.frame_set(0)
bpy.context.view_layer.update()

failures, worst = [], 0.0
for name, expected in stats["expected_centres"].items():
    obj = bpy.data.objects.get(name)
    if obj is None:
        failures.append(name + " missing")
        continue
    if obj.parent is None or obj.parent.type != "ARMATURE" or obj.parent_bone != stats["piece_bones"][name]:
        failures.append("%s parented to %s/%s" % (name, obj.parent and obj.parent.name, obj.parent_bone))
    centre = sum((obj.matrix_world @ Vector(c) for c in obj.bound_box), Vector()) / 8
    error = (centre - Vector(expected)).length
    worst = max(worst, error)
    if error > 0.005: failures.append("%s moved %.4f m" % (name, error))

rig = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
action = rig.animation_data.action if rig.animation_data else None
frames = action.frame_range if action else (0, 0)
last = stats["clips"][-1]["last"]
if action is None or int(round(frames[1])) < last: failures.append("animation ends at %s, expected %d" % (tuple(frames), last))
print("VERIFY pieces=%d worst_offset_m=%.5f frames=%s bones=%d" % (len(stats["expected_centres"]), worst, tuple(frames), len(rig.data.bones)))
for failure in failures: print("VERIFY_FAIL", failure)
print("VERIFY_RESULT", "FAIL" if failures else "PASS")
sys.exit(1 if failures else 0)
