# Player pistol shots

Unity implementation (`Assets/_Project/Scripts/Runtime/Weapons/FirstPersonWeapon.cs`).
A shot is an instant ray from the camera along the crosshair, up to 60 m.

## What the player sees

- **Muzzle flash**: a short orange flash at the barrel, shown for 0.06 s.
- **No tracer line**: the yellow line from the muzzle to the hit point was removed.
  Enemy guns keep their own tracers.
- **Bullet hole**: a small matte dark disc (3.5 cm, `BulletHole.cs`) on the surface
  that was hit. It faces out of the surface and sits 2 mm off it to avoid flicker.
  It follows that surface (doors, enemies) without being stretched by scaled walls,
  then shrinks away after **5 s** (Inspector: **Lifetime**, **Shrink Time** on the
  `PistolImpact` prefab).

## Self-hit fix

Shots skip the player's own colliders, like the hammer and the interaction check.
Before, walking backwards and firing could hit the player's own capsule:
`CharacterController.Move` moves the transform at once, but the physics scene keeps
the capsule at its old position until the next sync, so the camera ended up behind
that stale capsule.

## Builders

`Tools > Lockdown > Update Pistol Shot Effects` updates the existing assets in place:
it removes the pistol's `LineRenderer` and rebuilds `PistolImpact.prefab` (same path
and GUID) as the dark bullet hole with the `BulletHole` material. `Build Prototype
Pistol` creates the same effects for a fresh build.

## Verification

On 21 November 2026 these Play Mode tests in `Lockdown.Tests.Weapons` passed:

- `PistolShotEffectsTests`: muzzle flash shown then hidden, no tracer, one dark
  hole on the wall's surface facing out, the hole follows a moved wall, and it is
  gone after its lifetime.
- `WeaponSelfHitTests`: stepping back 0.3 m and firing in the same frame never
  damages the player. It failed on the old code (100 -> 75 health).
- `FirstPersonWeaponTests` and `AimDownSightsTests`.

`PlayerEquipmentTests` fails at line 92. It fails the same way on `develop` without
these changes.
