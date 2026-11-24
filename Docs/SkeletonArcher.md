# Skeleton archer (Ambusher)

Branch: `feature/skeleton-archer`. An escaped Natural History exhibit: a skeleton
archer that keeps his distance, peeks out of cover to loose two arrows, ducks back,
and runs when you get close. He is the Unity implementation of the selected
[Stalker / Ambusher](AI/Stalker.md) agent. He starts as a pile of bones beside the
stegosaurus and falls apart again when defeated.

## Assets

| Asset | Location |
|---|---|
| Archer prefab | `Assets/_Project/Prefabs/Characters/SkeletonArcher.prefab` |
| Arrow projectile prefab | `Assets/_Project/Prefabs/Characters/SkeletonArrow.prefab` |
| Imported model and clip table | `Assets/_Project/Models/SkeletonArcher/` |
| Editable Blender model | `Blender/SkeletonArcher/SkeletonArcher.blend` |
| Reproducible model generator | `Blender/SkeletonArcher/build_skeleton_archer.py` |
| FBX round-trip check | `Blender/SkeletonArcher/verify_fbx.py` |
| Sound generator and sounds | `Blender/SkeletonArcher/generate_audio.py`, `Assets/_Project/Audio/SkeletonArcher/` |
| Animator controller | `Assets/_Project/Animations/SkeletonArcher/SkeletonArcher.controller` |
| Twelve URP materials | `Assets/_Project/Materials/Enemies/SkeletonArcher/` |

The model, animation and sounds were generated locally for this project; no
downloaded models, textures, motion capture or recorded audio are used.

**Model.** About 5,700 triangles in **25 rigid pieces** on a **24-bone** rig: skull,
jaw, neck, ribcage, spine, pelvis, collarbones, upper arms, forearms, hands, thighs,
shins and feet, plus bow, quiver, belt, dagger and a nocked arrow. Each piece is a
separate mesh parented to one bone, not a skinned surface, because real bones do not
bend. That choice also makes both death effects simple: Unity detaches the pieces
with physics, or flies them in from a pile. Only the bowstring is skinned, between
the `Bow` and `StringNock` bones, so the string follows the drawing hand.

The look follows the [Art Bible](ArtBible.md) museum palette: aged ivory bone, dull
brass mounting wire at the neck, wrists and ankles, a paper catalogue tag on the
right wrist, dark timber bow and leather quiver. The eyes glow cool cyan so he
stands out against the warm gallery lighting.

**Animation.** Nine clips are keyed on one timeline and split in Unity using
`SkeletonArcher.clips.json`:

| Clip | Frames (30 fps) | Loops | Use |
|---|---|---|---|
| Idle | 0-60 | yes | Standing with the bow carried low |
| Walk | 70-102 | yes | Moving between cover |
| Run | 110-134 | yes | Retreating |
| Crouch | 140-180 | yes | Hiding behind cover |
| Draw | 190-211 | no | 0.7 s draw: the warning before each shot |
| AimHold | 220-250 | yes | Full draw |
| Release | 260-269 | no | Bowstring snaps forward |
| Nock | 280-298 | no | Takes the next arrow from the quiver |
| Hit | 310-320 | no | Flinch |

The archery poses are solved with two-bone inverse kinematics in the generator, so
the bow arm points at the target, the drawing hand reaches the anchor under the
jaw, and the arrow lies between them. The walk and run cycles lower the pelvis
until the supporting foot is on the floor.

## Behaviour

`AmbusherBrain` (in `Lockdown.AI.Core`) ports the prototype Ambusher and adds a
hunt for the museum's small rooms. Its states are `Unaware`, `Hide`, `Peek`,
`Retreat`, `Hunt` and `Dead`:

- On first sight in the open he takes an opening volley, then hides.
- Hide and peek timers count only after he reaches his post. He crouches for 0.5 s
  before his first peek and 1.2 s after a burst. A peek lasts up to 4 s and ends
  after a **two-arrow burst**.
- **Retreat:** a player within 4 m (heard even unseen) or 5 m in view, or one
  closing faster than 2.5 m/s within 8 m, makes him retreat. So does being shot.
  He finishes an arrow already drawn. A retreat only needs to gain 2.5 m of
  distance. If he can see the player when he arrives, he shoots from there at
  once; otherwise he ducks for just 0.4 s before peeking.
- **Hunt:** if the player is out of sight for 1 s at a peek, he circles the last
  known position, trying one side and then the other. He looks for a spot about
  70 degrees round and 5.5 m away that can see past the player's cover. After
  three flanks he walks to the last known position itself. The moment he sees the
  player again he stops and shoots, already holding a new hiding place. With
  nothing found after 12 s he settles back into cover.
- A stalled route records a failed post and replans; a 2.5 s cooldown stops him
  replanning a retreat every frame.

`AmbusherCoverScorer` chooses cover/peek pairs using the prototype's weights,
rescaled for 7 x 8 m museum rooms (ideal firing range 9 m). Cover must hide a
crouched archer from the player's eye line. A peek point within 1.6 m must give
a standing shot. Reserved cover, posts outside his territory (14 m around his
starting point) and, when fleeing, posts that do not gain distance are penalised.
If no cover gains distance, he runs to the farthest reachable escape point. The
same scorer ranks hunt flanks. In both cases the twelve best candidates are
re-scored by NavMesh walking distance, so a post just behind a wall does not beat
one in the same room.

`SkeletonArcherAgent` answers the brain's queries by sampling the NavMesh (every
1.5 m for cover, in rings around the last known position for flanks) and testing
sight lines with raycasts. It walks at 2 m/s, hunts at 2.8 m/s and retreats at
4.5 m/s, slower than the player's 6.5 m/s sprint. The Natural History gallery is
small, so his long sight lines run through the north and south doorways into
Asian Heritage and the Exhibition hall.

## Combat

- **Arrows** (`ArrowProjectile`) fly at 24 m/s with slight drop, so they can be
  sidestepped at range. They deal 18 damage to the player only, stick in whatever
  else they hit, and the 16 most recent stay in the walls. The bow aims for the
  drop, leads a moving player by up to 0.4 s, and stops tracking 0.12 s before
  release so a late dodge works.
- **Warning:** every shot starts with a 0.7 s draw and a creaking bowstring.
- **Hit locations** (`SkeletonHitbox`): head x2, gear (bow, quiver, belt, dagger)
  x0.35. A hit to the bow arm spoils his aim for 4 s (7 degree spread instead of
  1.2). Health is 70: three body shots or two headshots with the pistol.
- **Falling apart** (`SkeletonBody`): on death the animator stops, the bowstring
  snaps, and every piece becomes a physics body tumbling away from the player.
  After a killing headshot the skull flies off first and the headless body stands
  for 0.22 s before it crumbles. Shooting loose bones knocks them around.
- **Bone pile** (`SkeletonDormancy`): he starts as a pile. Breaking glass in
  Natural History, coming within 2.4 m, or shooting the pile pulls the bones
  together, feet first and skull last, over 1.6 s. The AI only starts then.
- **Rattle** (`SkeletonRattle`): a looping bone rattle grows louder with his
  speed, so you can track him by ear; he is silent while he aims.

## Building and placing

1. Optional: regenerate the model and sounds:
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python Blender/SkeletonArcher/build_skeleton_archer.py
   ```
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python Blender/SkeletonArcher/verify_fbx.py
   ```
   ```bash
   python3 Blender/SkeletonArcher/generate_audio.py
   ```
   On Windows use the Blender path from [HoundModel.md](HoundModel.md). Pass
   `-- --quick` for fast Workbench previews; images go to
   `Temp/SkeletonArcherVerification/`.
2. In Unity: **Tools > Lockdown > Build Skeleton Archer**. This configures the
   imports and clips, creates the materials, controller and both prefabs. Existing
   materials are reused, so manual adjustments survive; the controller is
   regenerated.
3. Open `Museum_Main` and use **Tools > Lockdown > Place Skeleton Archer**. It finds
   a clear NavMesh spot beside the stegosaurus, links every breakable glass pane in
   Natural History as a wake trigger, and skips placement if he already exists.
   Save the scene.

## Validation

- `Lockdown.Tests.Ambusher.EditMode`: brain states, burst, retreat triggers and
  the snap shot after a retreat, cooldown, the hunt around the last known
  position, stalled routes, cover and flank scoring with walking distance.
- `Lockdown.Tests.SkeletonArcher` (Play Mode): prefab structure and materials,
  clips and timings, full-draw aim, the shot cycle, arrow damage and sticking,
  hit locations, falling apart, the headshot, the bone pile and the rattle.
- `Blender/SkeletonArcher/verify_fbx.py`: re-imports the FBX and checks that all 25
  pieces keep their bones and positions (zero offset on 20 October 2026).

## Not yet built

The **dagger attack** is planned: when the player's pistol clicks empty or they
start reloading, he hears it and charges with the dagger already on his belt. The
dagger is a separate piece so it can move to his hand later. The pistol already
exposes `IsReloading` and `CurrentAmmo`.
