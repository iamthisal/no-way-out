# Door system: viva explanation

## A short answer you can say first

> The museum already contained door models placed by the world-building work. I
> added interaction and movement to those existing models. I used DoorBuilder as
> an editor setup tool to find seven named interior doors, create hinge pivots,
> and attach collision, navigation obstacles, and the Door component. During the
> game, PlayerInteraction detects the door the player is looking at. Pressing E
> calls the door through IInteractable, toggles its state, and smoothly rotates
> its hinge. The configured setup is saved in the scene, so the builder does not
> need to run during gameplay.

Understand the steps below so you can explain the implementation in your own words.

## 1. What existed before this feature?

The world already had visible door models, their scene objects, and their positions,
rotations, and scales. Existing imported models included:

- `Assets/_ThirdParty/Environment/Storage_Door_low.fbx`
- `Assets/_ThirdParty/Environment/Security Door_5.fbx`
- `Assets/_ThirdParty/Environment/door_washroom.fbx`

The seven interior door hierarchies were already placed in `Museum_Main.unity`.
Their inspected models had no colliders and no opening/closing controller. They
were visual objects at that point.

The player also already had `PlayerInteraction`, the `IInteractable` interface,
and loot interaction. These were reused for doors.

The new files for this feature were `Door.cs`, `DoorBuilder.cs`, and the door test
assembly. The museum scene was updated to store the configured hinges and components.

**Correction to remember:** the door models already existed. The gameplay script
`Door.cs` did not exist before this feature. Adding a script to an existing model
gives it behavior; it does not create a new 3D model. Do not describe these reused
models as models you created for the door feature.

## 2. Does DoorBuilder contain door names and locations?

It contains the names of the seven scene objects to configure:

| Existing scene object | Area |
|---|---|
| `Door_D06_Storage` | Storage |
| `Door_D07_Storage` | Storage |
| `Door_D08_Storage` | Storage |
| `Door_D09_Security` | Security |
| `Door_D10_Security` | Security |
| `Door_D11_Toilet` | Toilet |
| `Door_D21_GuestToilet` | Guest toilet |

**It does not contain a hard-coded list of their world coordinates.** The
`DoorNames` array stores names. `Find()` walks through the active scene's root
objects and their descendants to find the matching Transforms.

The positions come from those existing Transforms. The builder also measures each
panel to work out where its hinge and collider should go.

For example, the hinge position uses:

```csharp
root.position + orientation *
    new Vector3(bounds.min.x, bounds.center.y, bounds.center.z)
```

In words: start at the existing door root, find the panel's edge in the chosen
orientation, and place the hinge there. Those values are calculated from the model,
so they are not manually entered coordinates for each room.

The setup does depend on these exact names. Renaming a root can make a later builder
run fail to find it. An already configured door continues working at runtime because
`Door.cs` does not look it up by name.

## 3. What does DoorBuilder.cs do?

File: `Assets/_Project/Scripts/Editor/DoorBuilder.cs`.

It is an **editor setup tool**. The `[MenuItem]` attribute exposes:

`Tools > Lockdown > Configure Museum Doors`

It belongs in the `Editor` folder and uses `UnityEditor` APIs. It does not handle
player input or animate the doors during a built game.

Its main steps are:

1. **Validate the context.** It requires `Museum_Main` to be open in Edit mode.
2. **Find and validate the targets.** It finds all seven roots and their panel
   Renderers before starting to configure them. Missing objects produce an error.
3. **Skip configured doors.** If a root already contains `Door`, it skips that door,
   so rerunning the tool does not add duplicate hinges.
4. **Calculate the panel bounds.** `PanelBounds()` transforms the eight corners of
   the renderer's local bounding box into the chosen door coordinate system. This
   accounts for the model's placement, rotation, and scale.
5. **Create a hinge.** It creates an empty `Hinge` GameObject at the panel's edge.
   The new hinge has unit scale. The moving panel becomes its child while retaining
   its world placement.
6. **Allow movement.** It clears static editor flags on the moving hierarchy.
7. **Attach components.** It adds `BoxCollider`, `NavMeshObstacle`, and `Door` to
   the hinge. Their sizes come from the calculated panel bounds.
8. **Support editing.** It groups changes into one Undo operation and marks the
   scene as modified. The menu tool itself does not save the scene; save afterward.

The saved scene contains the result. There is no need to rerun this tool every time
Play starts. It also does not generate a new reusable door prefab asset.

### Why are washroom doors handled separately?

The washroom model contains two meshes: `Cube` is the moving panel, and `Cube.001`
is the fixed frame. Its panel faces a different local direction, so the builder
uses an additional 90-degree orientation adjustment when calculating its setup.

The builder unpacks the washroom prefab instance so the panel can be reparented
separately. It moves only the panel under the hinge and keeps the frame outside
it. The original source model asset is preserved. A `MeshCollider` is added to the
fixed frame so its opening is represented without filling the whole opening with
a solid box.

The resulting structure is approximately:

```text
Door_D11_Toilet                 existing world placement
|-- Visuals
|   `-- door_washroom
|       `-- Cube.001            stationary frame + MeshCollider
`-- Hinge                      Door + BoxCollider + NavMeshObstacle
    `-- Cube                   moving panel model
```

Storage and security models are placed beneath their own hinges. The reusable
behavior is the same; the washroom asset needs different setup because of its mesh
structure and orientation.

## 4. Why use an empty hinge object?

A Transform rotates around its own origin. A model's original origin might be in
the middle or at an inconvenient point. Rotating there could make it spin instead
of swing like a door.

An empty hinge at the edge gives us a controlled pivot without editing the model's
vertices. Unity's parent-child transforms make the child panel follow the hinge's
rotation. The collider follows the same hinge, so the visible panel and collision
move together.

This is a transform-controlled door. It does not use a Rigidbody, HingeJoint,
Animator Controller, or an animation clip to produce the swing.
