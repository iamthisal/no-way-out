# Aiming down the pistol sights

Unity implementation. With the pistol equipped (key **2**), hold the **right mouse
button** (gamepad **left trigger**) to raise it to eye level. The top of the green
front post then sits exactly on the crosshair, centred in the notch of the rear
sight. Release to lower it again.

- Only guns aim. Aiming lives in `FirstPersonWeapon`, the pistol component. Empty
  hands and the melee hammer (`FirstPersonHammer`) ignore the right mouse button.
- Shots already travel along the camera's forward ray, which is the crosshair, so
  aiming lines the sights up with where bullets go. It does not change damage,
  range or accuracy.
- The sights rise or drop over 0.12 s. Aiming stops while reloading (and resumes if
  the button is still held), when the cursor is released, and when the pistol is put
  away. Recoil still applies while aiming.
- The crosshair fades to 45 % opacity while aiming so the front sight stays visible.
- The camera's field of view does not change (no zoom).

## Implementation

`Assets/_Project/Scripts/Runtime/Weapons/FirstPersonWeapon.cs` computes an aim pose
once in `Awake`. The pistol stays parallel to the camera, and the top of
`frontSight` is placed on the camera's forward axis at `sightDistance` (0.5 m). Each
frame it blends between the hip pose and the aim pose. Inspector fields:
**Front Sight**, **Sight Distance** and **Aim Time**. `IsAiming` and `AimBlend`
expose the state.

The pistol's rear sight is two posts with a 14 mm notch, their tops level with the
12 mm front post. `Tools > Lockdown > Update Pistol Sights` applies this to the
existing `PrototypePistol.prefab` in place and assigns the front sight. Running the
full `Build Prototype Pistol` builds the same sights.

## Verification

On 21 November 2026, `AimDownSightsTests` (Play Mode assembly
`Lockdown.Tests.Weapons`) passed. It checks the front sight's top is on the camera
axis (within 1 mm) with the sight line parallel to it, that reloading lowers and
restores the sights, that releasing returns the pistol to the hip, and that right
click does not move the hammer. `FirstPersonWeaponTests` also passed.

`PlayerEquipmentTests` fails at line 92 ("equipping while holding Attack must not
shoot"). It fails the same way on `develop` without these changes, so it was
failing before this feature.
