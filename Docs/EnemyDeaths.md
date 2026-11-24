# Enemy deaths

Unity game. Covers what happens after the hound, the guard or the skeleton archer is killed.

## Removal

`CorpseRemoval` (`Runtime/Combat`) is on the `InvestigatingHound`, `Pursuer` and `SkeletonArcher` prefabs. When
`Health.OnDeath` fires, the body stays for **5 s**, sinks 0.9 m into the floor over **1.5 s** and the GameObject is
destroyed. The values are in the Inspector. Paused time does not count.

Before sinking, loose rigidbodies become kinematic and all colliders are switched off, so the archer's scattered
bones sink with it instead of resting on the floor. `GlassHoundResponse` drops destroyed dogs from its pack.

## Death animations

| Enemy | Death |
|---|---|
| Hound | `Hound_Death`: 48 frames at 30 fps (1.6 s), plays once and holds the last pose |
| Guard | Imported `Death` clip, unchanged |
| Archer | Falls apart into physics pieces, unchanged |

`Hound_Death` is hand-keyed in `Blender/Hound/MuseumHound.blend`: hit (frame 3), knees buckle (9), tips over to the
right (15), body lands (21), small bounce (25), settles (30), relaxed (40-48). The body height is keyed from the
deformed mesh, so it touches the floor throughout. `HoundLocomotion` sets the `Death` trigger; the Animator has no
transition back to `Locomotion`.

## Export rule for the hound

`Hound_Idle` keys only the chest, head, ears and tail. Bones it does not key keep whatever pose was last set, and the
FBX export bakes that in. An earlier export therefore shipped an idle with gallop legs. Before exporting, make
`Hound_Idle` active and clear the pose of every bone (location and rotation 0).

## Builders

- **Tools > Lockdown > Import Hound Death Clip**: adds the clip to the FBX import list; other clips are kept.
- **Tools > Lockdown > Connect Hound Death Animation**: adds the trigger, state and transition.
- **Tools > Lockdown > Add Corpse Removal to Enemies**: adds the component to the three prefabs.

The guard and archer builders also add removal. Run the last menu item again after rebuilding the hound response.

## Validation

`Lockdown.Tests.EnemyDeaths` (Play Mode, 7 tests, 24 November 2026): the hound plays and holds its death, all three
enemies are destroyed after their delay, no loose archer bones remain, placed museum enemies have removal, pausing
stops the countdown, and breaking glass after the dog is gone causes no errors. Museum screenshots were checked
visually. Sounds for the hound's death and blood effects are not implemented.
