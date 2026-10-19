# Hammer pickup, swinging, and breakable display: viva notes

## A short explanation to say first

> The storage room already had a hammer model, and the exhibition area already
> had a glass display with an armored helmet. I reused those models and added the
> gameplay sequence. E collects the hammer as equipment. The equipment system
> switches between hands, pistol, and hammer. A short-range hammer strike damages
> the glass through the existing damage interface. When the glass health reaches
> zero, it removes its visual and collision barrier, allowing the player to collect
> the helmet through the existing loot system.

## 1. Existing work and new work

The hammer mesh, its storage-room placement, the display model, and the helmet
model were already present in `Museum_Main`. The helmet's imported object is named
`Homem De Ferro`; its gameplay loot label is **Armored Helmet**, worth **$500**.
This feature does not claim authorship of those existing models.

Existing systems reused:

- `PlayerInteraction` and `IInteractable` for camera targeting, the E prompt,
  reach/sight restrictions, and interaction calls.
- `PlayerEquipment` for hands/pistol selection, extended with hammer ownership
  and a third slot.
- `IDamageable` and `Health` for damage and the zero-health event.
- `LootItem` and `LootInventory` for collecting the exposed helmet once.

New runtime components are `HammerPickup`, `FirstPersonHammer`, and
`BreakableGlass`. `HammerBuilder` and `GlassDisplayBuilder` configure the existing
scene and player prefab in the editor.

## 2. How pickup and equipment work

`HammerPickup` implements `IInteractable`. The player looks at its collider and
presses E. The pickup validates that it is the player's actual current interaction
target, then calls `PlayerEquipment.TryCollectHammer()`.

The equipment component checks that the player can use controls, does not already
own the hammer, and has a configured held hammer. Successful pickup sets
`HasHammer`, equips the hammer, and hides the world pickup. It does not add money
or an entry to the loot inventory: this object is a tool.

The controls are 1 for hands, 2 for pistol, and 3 for the owned hammer. Selection
activates one held object and hides the others. Switching preserves pistol ammo.
The hammer cannot be selected before pickup.

`HammerBuilder` copies the existing hammer's appearance into the player prefab,
fits and rotates it for the camera, adds a grip hand, and removes held-model
colliders. The world pickup retains its collider. A held collider could otherwise
interfere with the player's raycasts or physics.

## 3. How a swing becomes an impact

`FirstPersonHammer` handles mouse left-click and gamepad right trigger. It requires
an active hammer, a living player, and active gameplay controls. Holding the button
does not repeat attacks, and equipping while already holding it requires a release.

`TrySwing()` begins the animation and records the cooldown. Default timings are:

| Setting | Default |
|---|---|
| Reach | 1.9 m |
| Damage | 100 |
| Impact delay | 0.16 s |
| Swing duration | 0.55 s |
| Swing interval | 0.65 s |

During Update, the held model moves through a downstroke and recovery using the
elapsed game time. After the impact delay, the script casts a ray from the camera
along its current forward direction. It sorts hits by distance, ignores player
colliders, and handles only the first solid world surface.

If that surface implements `IDamageable`, the script applies damage. The strike
stops at that surface even if it breaks, so a single swing does not damage an
additional object behind it. A wall blocks the strike, and a distant object is
outside its reach.

This is an aimed melee raycast with a visual swing, rather than a collision-driven
simulation of the hammer head. Holstering, releasing gameplay input, or dying
cancels a pending impact. Holstering does not reset the cooldown.

## 4. How glass breaking exposes the loot

The configured display is `P_Exhibition_CaseEast`. Its original solid placeholder
collider is disabled because it would continue blocking interaction after glass
breaking. The glass mesh gets its own collider, Health, and BreakableGlass.
Separate mesh colliders preserve the wooden base and thin frame.

Health implements `IDamageable`. BreakableGlass subscribes to `Health.OnDeath`.
When that event fires, it:

1. Marks the glass broken, with a guard against repeating the operation.
2. Hides only the assigned glass renderer.
3. Disables only the assigned glass blocker.
4. Plays a short particle burst and a synthesized shattering sound.
5. Invokes `OnBroken` for possible future extensions.

The helmet remains active. Its `LootItem` uses the existing E interaction. Before
breaking, the glass is the first solid raycast hit and blocks the helmet. After
breaking, the ray can reach the helmet, so normal collection becomes available.

The display has 50 health. A 100-damage hammer strike breaks it in one hit; the
existing 25-damage pistol can also break it in two hits. The hammer provides a way
to save ammunition. The glass sound is audible, but enemy hearing has not been
connected to this event yet.

## 5. A useful bug to explain

The first museum integration test found that glass could break immediately on
scene load. The component checked `Health.IsDead` in OnEnable before Health's
Awake had initialized its current value.

The correction keeps event subscription in OnEnable but defers the initial health
check until Start. Unity finishes the initial scene Awake calls before Start.
Later re-enables can still check for damage that occurred while the glass
component was disabled.

This illustrates why a test in the actual scene can find a lifecycle problem that
tests adding components to already-active objects may miss. The correction was
committed separately so the history shows the issue and its resolution.

## 6. Testing and demonstration

Six runtime tests cover pickup restrictions, ownership, equipment keys, ammo
preservation, held-button input, cooldown, range, walls, one-target impacts,
cancelled strikes, and breaking glass once without hiding child loot.

The museum test checks the actual world hammer, the actual glass display, one
mouse strike without consuming pistol ammo, collecting the $500 helmet once,
and encounter restart. Restart reloads the scene, restoring the pickup, intact
glass, and empty player inventory. Ownership is not persisted across encounters.

The door regression suite also runs alongside this feature. The known missing
exterior-door prefab remains a separate scene issue; museum tests explicitly
expect its specific load errors only when that asset is absent.

A demonstration sequence:

1. Show the existing hammer and display models in the scene hierarchy.
2. Show HammerPickup and the player's held hammer, then explain tool ownership.
3. In Play mode, show that 3 cannot equip the hammer before pickup.
4. Collect it with E and switch among 1, 2, and 3.
5. Show that intact glass blocks helmet collection.
6. Swing with left-click, then collect the exposed helmet with E.
7. Show the tests and the incremental commits for each working milestone.

Only this first display is configured for the sequence. Additional cases can reuse
the components. Enemy sound reactions, weapon durability, saved ownership, and a
physical hammer-head sweep are possible extensions, not implemented features.
