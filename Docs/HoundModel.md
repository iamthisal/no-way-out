# Museum hound model

Branch: `feature/hound-model`. A low-poly tan/dark guard hound is placed in the
central hall at approximately **(2.4, 0, 0.5)**, facing toward the lobby. This
milestone supplies the dog model and idle presentation.

## Assets

| Asset | Location |
|---|---|
| Reusable dog prefab | `Assets/_Project/Prefabs/Characters/MuseumHound.prefab` |
| Imported skinned model | `Assets/_Project/Models/Hound/MuseumHound.fbx` |
| Editable Blender model | `Blender/Hound/MuseumHound.blend` |
| Reproducible generator | `Blender/Hound/build_hound.py` |
| Generic idle controller | `Assets/_Project/Animations/Hound/HoundIdle.controller` |
| Eight URP materials | `Assets/_Project/Materials/Enemies/Hound/` |

The model was procedurally generated in Blender for this project, with no downloaded
dog asset or texture dependency. Its tan coat, dark saddle and muzzle, folded ears,
amber eyes, green collar and brass tag use a flat material palette. The joined
character has **5,580 triangles**, **22 rig bones**, and one skinned mesh renderer
with eight material slots. The rig has named head, ears, spine, tail, and three
segments for each leg.

The **2.4-second idle loop** adds gentle breathing, small head/ear movement and
tail movement. The Animator uses a generic avatar, loops the idle state, and has
root motion disabled. A horizontal body capsule prevents walking straight through
the dog. The prefab faces Unity +Z; the scene instance is rotated toward the lobby.

The editable `.blend` stays outside `Assets` so Unity imports the explicit FBX
rather than depending on its automatic Blender importer.

## Rebuilding

Run the generator using Blender's background mode, for example:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python Blender/Hound/build_hound.py
```

This regenerates the `.blend`, the FBX, and three preview images under
`Temp/HoundVerification/`. Preserve the FBX's Unity `.meta` file when regenerating.

In Unity, use **Tools > Lockdown > Build Museum Hound Model**. The editor tool
configures the generic import and loop, creates missing URP materials and the idle
controller, corrects model facing/ground offset, and saves the prefab. Existing
hound materials are reused, so their manual adjustments are preserved.

Use **Tools > Lockdown > Place Museum Hound** in `Museum_Main` to place it. The
tool chooses a clear central-hall location on the NavMesh and raycasts to the
physical floor to avoid floating at the NavMesh's height offset. It skips placement
if the named dog already exists. Save the scene after using the placement menu.

## Validation and scope

`Lockdown.Tests.HoundPresentation` checks the imported rig, eight URP materials,
metre scale, grounding, forward direction, body collider, looping tail animation,
root-motion stability, and exactly one placed dog. The existing hammer/glass and
door tests are included in the verification run.

The Play Mode run on 19 October 2026 passed all 17 tests with zero failures.

Blender renders and a Unity museum screenshot were visually checked. Local
screenshots and exported test results are under `Temp/HoundVerification/`; Temp is
not version-controlled.

This prefab has no custom gameplay scripts. Hearing, investigation, movement,
attacks, and damage reactions are later integration work. The existing AI/Hound
source files are unchanged. The rig provides a starting point for future movement
animation, but this asset currently ships only the idle clip.
