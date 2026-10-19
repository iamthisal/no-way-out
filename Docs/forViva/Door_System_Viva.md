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
