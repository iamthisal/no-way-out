# Hound model and presentation: viva notes

## What I built

I created an original low-poly museum guard dog, imported it into Unity, and
placed one instance in the central hall. This stage provides its appearance,
body collider, skeleton, and looping idle animation. Hearing and enemy behaviour
are separate work; the existing AI/Hound scripts were not changed.

## Where the model came from

`Blender/Hound/build_hound.py` builds the dog in Blender. It creates the body,
four legs and paws, muzzle, floppy ears, eyes, tail, collar, and tag. It also
creates the skeleton, assigns mesh vertices to bones, and animates an idle pose.
The editable source is `Blender/Hound/MuseumHound.blend`; the exported Unity asset
is `Assets/_Project/Models/Hound/MuseumHound.fbx`.

The dog has 5,580 triangles and 22 bones. Eight materials provide its tan coat,
dark saddle and muzzle, eyes, and collar details. The Blender source lives outside
Assets so Unity imports the exported FBX rather than also importing the blend file.

## Mesh, skeleton, and animation

The mesh describes the visible surface. The skeleton is a hierarchy of bones,
such as hips, chest, head, legs, ears, and tail. Skinning connects vertices to
those bones so rotating a bone deforms the corresponding part of the dog.

The idle clip lasts 2.4 seconds and loops. Small chest, head, ear, and tail
movements make the stationary dog look alive. It currently has no walking or
running animation. Root motion is disabled, so the idle does not move the dog
across the room.

## What HoundModelBuilder.cs does

`Assets/_Project/Scripts/Editor/HoundModelBuilder.cs` automates Unity setup. Its
Build Museum Hound Model menu configures the FBX as a Generic rig, imports the
looping idle, creates URP materials, creates the Animator Controller, and saves
`Assets/_Project/Prefabs/Characters/MuseumHound.prefab`.

The prefab combines a SkinnedMeshRenderer, Animator, and capsule collider. The
Animator plays the idle through its controller. The collider represents the
body for physical collisions; it does not provide health, damage, or attacks.
The builder also corrects facing and ground alignment for consistent placement.

## How it was placed

The Place Museum Hound menu checks candidate positions in the central hall
against the NavMesh, raycasts onto the actual floor, and checks that the space
is clear before placing the prefab. The scene instance is at (2.4, 0, 0.5),
with a Y rotation of 210 degrees. Using the NavMesh here only helps choose a
placement position; the dog has no navigation agent yet.

## How I verified it

Three presentation tests check the rig, materials, scale, ground alignment,
forward direction, collider, idle loop, tail movement, root-position stability,
and exactly one placed dog. The existing door and hammer/glass tests were also
run: all 17 Play Mode tests passed. Blender renders and a museum screenshot
were visually inspected.

## Short viva answer

“I built the dog as an original skinned model in Blender, exported an FBX, and
used an editor builder to configure its materials, Generic rig, idle Animator,
collider, and reusable Unity prefab. I placed it on the museum floor and tested
the presentation and existing interactions. Its appearance is ready; hearing,
movement, and combat behaviour will be integrated separately.”
