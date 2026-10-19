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

## 5. How does the player interact with it?

Files:

- `Assets/_Project/Scripts/Runtime/Player/PlayerInteraction.cs`
- `Assets/_Project/Scripts/Interfaces/IInteractable.cs`

The player script already handles **E** and the gamepad's west button, displayed
as **X** by the existing UI. Each new button press calls `TryInteract()`.

The detection process is:

1. Check whether the active, living player can interact. This also uses the
   controller's existing gameplay-input gate, `CanUseWeapon`.
2. Cast a ray from the player camera along its forward direction, with a default
   reach of 2.5 metres.
3. Sort the ray hits by distance and ignore colliders belonging to the player.
4. Use the first remaining collider. `GetComponentInParent<IInteractable>()`
   finds its interaction component on that object or a parent.
5. Display the target's `InteractionLabel` and call its `TryInteract()` when E is
   pressed.

The first solid world collider blocks the ray even if it is not interactive. That
prevents selecting a door behind a wall.

`IInteractable` defines two members:

```csharp
string InteractionLabel { get; }
bool TryInteract(GameObject interactor);
```

The player knows this common contract. `Door` supplies opening/closing behavior;
`LootItem` supplies collection behavior. This is an example of **interface-based
polymorphism**: the same player call invokes the target object's implementation.

## 6. What does Door.cs do during gameplay?

File: `Assets/_Project/Scripts/Runtime/World/Door.cs`.

`Door` is a `MonoBehaviour` attached to the hinge and implements `IInteractable`.
It has three Inspector settings:

| Setting | Default | Meaning |
|---|---|---|
| `openAngle` | 95 degrees | Rotation relative to the closed pose; negative reverses swing |
| `degreesPerSecond` | 180 | Angular movement speed |
| `startsOpen` | false | Initial requested state |

### Awake: remember the starting pose

`Awake()` stores the hinge's initial local rotation as `closedRotation`. It sets
`IsOpen` from `startsOpen` and applies the initial angle.

Saving the closed rotation means the same script works on doors facing different
directions. It does not assume the world-space closed rotation is zero.

### TryInteract: change the target state

`TryInteract()` rejects a disabled door or null interactor. It also checks that
the player's current valid interaction target is this exact door. A direct method
call therefore does not bypass the player's reach and sight checks.

Then it toggles:

```csharp
IsOpen = !IsOpen;
```

`IsOpen` describes the **requested state**, not proof that the animation has already
finished. The prompt changes to `Close door` when opening is requested and to
`Open door` when closing is requested. `IsMoving` reports whether the current angle
still differs from the target.

### Update: move smoothly toward the target

`TargetAngle` is either zero or `openAngle`. Each frame:

```csharp
currentAngle = Mathf.MoveTowards(currentAngle, TargetAngle,
    Mathf.Max(1f, degreesPerSecond) * Time.deltaTime);
transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
```

`MoveTowards` advances the angle and reaches the target without overshooting.
Multiplying speed by `Time.deltaTime` makes movement depend on elapsed time rather
than the number of frames. At the defaults, a full swing takes approximately
95 / 180 = **0.53 seconds** of game time.

`Quaternion.Euler` creates the local Y-axis swing. Combining it with the stored
closed rotation applies that swing relative to the original pose.

Pressing again mid-swing changes the target, so the panel reverses from its current
angle. Holding E does not repeatedly toggle it because the player script uses
`WasPressedThisFrame()`.

## 7. What are the collider and navigation obstacle for?

The **BoxCollider** represents the moving panel for physics queries and physical
blocking. When closed, it blocks player passage and raycasts. When the panel
swings away, its collider moves away with it.

The **NavMeshObstacle** represents the panel to Unity navigation. Its box matches
the collider, carving is enabled, and `carveOnlyStationary` is enabled. A stationary
panel can therefore carve blocked space in the NavMesh after Unity's stationary
threshold. The obstacle moves with the door.

These components serve different systems. A collider alone does not guarantee that
a NavMesh agent plans a route around a door. A navigation obstacle is not a
replacement for the player's physical collider.

Carving configuration was checked. Enemy route behavior, carving timing, and AI
performance were not measured, so do not claim those were proven by the door tests.
