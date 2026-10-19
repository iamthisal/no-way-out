# Storage hammer and breakable display

Branch: `feature/hammer-glass-looting`. This extends the existing museum scene,
player equipment, interaction contract, and damage system.

## Playing the sequence

1. Find the existing hammer on the storage-room table. Look at it within the
   player's interaction reach and press **E**. Pickup automatically equips it.
2. Use **1** for hands, **2** for the pistol, and **3** for the owned hammer.
   Gamepad selection uses D-pad left, right, and up respectively.
3. Approach the east exhibition display containing the armored helmet. Aim at its
   glass and **left-click** (gamepad right trigger) to swing.
4. A hammer impact within **1.9 m** breaks the display glass. Look at the exposed
   helmet and press **E** to collect **Armored Helmet, $500**.

The hammer is equipment, so collecting it does not increase loot count or value.
It cannot be equipped before pickup. Holding Attack does not repeat swings, and
equipping while holding Attack requires releasing the button before swinging.

## Implementation

- `HammerPickup` implements `IInteractable`. It validates the player's current
  target, grants ownership through `PlayerEquipment`, then hides the world pickup.
- `PlayerEquipment` keeps the existing hands/pistol APIs and adds an optional hammer
  reference, ownership, and a third selection slot. Pistol ammunition survives
  switching.
- `FirstPersonHammer` animates a short swing, resolves one camera raycast after a
  0.16-second impact delay, and deals 100 damage to the first solid target that
  implements `IDamageable`. The swing lasts 0.55 seconds with a 0.65-second interval.
  Walls block impacts. Holstering, losing gameplay input, or dying cancels pending
  impacts. Holstering does not reset the cooldown.
- `BreakableGlass` listens to the existing `Health.OnDeath` event. It hides only
  designated glass renderers, disables their blockers, plays a particle burst and
  synthesized shatter sound, and invokes `OnBroken` once. The display's base, frame,
  and loot stay intact. This display has 50 health, so one hammer hit or two default
  pistol hits can break it.
- `LootItem` and `PlayerInteraction` provide the existing collection behavior. The
  intact glass collider blocks the camera ray from selecting the helmet.

## Editor setup

`Tools > Lockdown > Configure Storage Hammer` configures the existing `hammer_low`
pickup and saves a held version with a grip hand into the player prefab. The held
model reuses the scene hammer's materials and has no colliders. Its orientation
and scale are adjusted for first-person viewing.

`Tools > Lockdown > Configure First Breakable Display` configures
`P_Exhibition_CaseEast`. It replaces the solid placeholder's collision with glass
and frame collision and adds loot behavior to the existing `Homem De Ferro`
helmet model. Both tools skip their already-configured scene targets. Save the museum
scene after using them. Original imported model assets are preserved.

The audio builder creates a deterministic 0.5-second shatter WAV. There is no
external audio dependency. Glass breaking is audible; enemy hearing integration
has not been added. Only this first display is configured to break and yield loot.

Restarting the encounter reloads the scene: hammer ownership and loot reset, the
storage pickup reappears, and the display glass becomes intact again.

## Verification

On 19 October 2026, the seven hammer/glass Play Mode tests and five door regression
tests passed together: **12 passed, 0 failed** in Unity 6000.6.3f1. Storage pickup,
held hammer, and intact/broken display screenshots were visually checked.

The `Lockdown.Tests.Hammer` Play Mode assembly covers pickup restrictions, equipment
selection, ammunition preservation, held-button input, cooldown, range, walls,
single-target impacts, cancellation, glass effects, and the museum sequence with
restart. Its screenshots and XML results are written to `Temp/HammerVerification/`.

The pre-existing missing exterior `Exit_MuseumDoubleDoor` prefab remains outside
this feature. Museum integration tests expect only its specific scene-load errors
when that asset is absent. Other unexpected error logs still fail the tests.
