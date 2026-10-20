# Mummies, sarcophagi and canopic jars

Branch: `feature/canopic-mummies`. Four painted sarcophagi stand in the corners of
the Ancient Egypt gallery, each holding a mummy. Opening a sarcophagus (**E**)
swings its lid aside, and the mummy walks out and hunts the player through the
museum. Each mummy is bound to one of four canopic jars spread across the museum.
A mummy that is put down rises again after 10 seconds; once its jar is broken it
crumbles to dust instead and drops its amulet as loot.

## The curse

| God | Sarcophagus | Jar lid | Colour | Jar location |
|---|---|---|---|---|
| Imsety | South-west corner | Human head | Turquoise | Stone Age, west wall |
| Hapi | North-west corner | Baboon | Lapis blue | Greece / Rome, south wall |
| Duamutef | North-east corner | Jackal | Carnelian red | Natural History, south wall |
| Qebehsenuef | East wall by the south door | Falcon | Gold | Art Gallery, west wall |

The four sons of Horus guarded the organs of the dead in Egyptian burials, so a
set of exactly four jars fits the museum. Each god's colour appears on the
sarcophagus headdress and collar, the jar's glaze, and the mummy's scarab amulet,
so players can match them up.

The south-east corner is the south doorway (D13), so Qebehsenuef's sarcophagus
stands on the east wall just north of the door. Placement moves three decor props
out of the corners within the same gallery: the plant, the column and the corner
feature. The pyramid display, wall case, covered crate and the original standing
sarcophagus are unchanged.

1. **Sealed jars.** A jar is sealed while its mummy is still in its sarcophagus.
   Its glaze pulses, hits only make a dull knock, and the HUD says to open the
   mummy's sarcophagus first.
2. **Releasing.** Opening a sarcophagus grinds the lid open over 1.2 s and starts
   a dust puff. The mummy walks out (1.8 s), and its jar unseals.
3. **Fighting.** A mummy has 60 health: three pistol shots or one hammer blow. With
   its jar intact, a mummy at zero health falls (0.8 s), lies for 10 s, and rises
   (1.6 s) with full health. While down it cannot be hurt, and the player can step
   over it.
4. **Breaking the jar.** An unsealed jar has 40 health: one hammer blow or two
   pistol shots. It shatters into a base, twelve shards and the lid, with a spirit
   puff in its colour. The mummy's amulet goes dark. If the mummy is lying down at
   that moment, it crumbles at once.
5. **Laying it to rest.** A mummy whose jar is broken collapses at zero health. Its
   wrappings crumble to dust from the head down, and its amulet falls to the floor.
   Picking it up (**E**) adds **Amulet of \<God\>, $750** to the player's loot.
   Each amulet carries a `MummyAmulet` marker with its god's name, ready for later
   story beats.

`MummyCurse` shows a "Canopic jars broken: N / 4" counter once a mummy is free. Its
messages explain each rule the first time it matters: a released mummy, a sealed
jar, a mummy rising again, a jar breaking, a mummy crumbling, and the curse lifting.

## Behaviour

`MummyBrain` (in `Lockdown.AI.Core`) implements the [Pursuer](AI/Sentinel.md)
design as a separate agent; the team's `PursuerAgent` is unchanged. States are
`Dormant` (in the sarcophagus), `Patrol`, `Chase`, `Investigate`, `Search`,
`Down` and `Dead`.

- **Patrol** loops through gallery route markers; each mummy has its own loop.
  After an interruption it rejoins the loop at the nearest point.
- **Chase** starts on sight (16 m; a 120-degree view while patrolling, all round
  while chasing). The first mummy to spot the player alerts every other standing
  mummy within 20 m.
- Out of sight, a chasing mummy follows the **last known position**. After 2 s it
  **searches** there for 14 s, then returns to patrol.
- **Noise:** pistol shots are heard within 18 m; being shot also reveals the shooter.
- **Bite:** within 1.35 m a mummy lunges (0.6 s). The bite lands 0.3 s in, for 15
  damage, if the player is still within 1.75 m in front of it. It bites at most once
  every 1.25 s.
- A mummy that rises after a knockdown searches from where it last saw the player.

`MummyAgent` shambles at 1 m/s on patrol and chases at 2.9 m/s, slower than the
player's 4 m/s walk. Several chasing mummies take separate approach lanes 1.1 m
apart, so they close in from several sides. After a bite or a flinch, the animator
returns to the walk and run blend.

## Assets

| Asset | Location |
|---|---|
| Mummy prefab | `Assets/_Project/Prefabs/Characters/Mummy.prefab` |
| Jar, sarcophagus and amulet prefabs | `Assets/_Project/Prefabs/Props/CanopicJar_*`, `Sarcophagus_*`, `Amulet_*` |
| Models and clip table | `Assets/_Project/Models/Mummy/` (`Mummy.fbx`, `CanopicJars.fbx`, `Sarcophagi.fbx`) |
| Blender source and generator | `Blender/Mummy/Mummy.blend`, `Blender/Mummy/build_mummy.py` |
| FBX round-trip check | `Blender/Mummy/verify_fbx.py` |
| Sound generator and sounds | `Blender/Mummy/generate_audio.py`, `Assets/_Project/Audio/Mummy/` |
| Animator controller | `Assets/_Project/Animations/Mummy/Mummy.controller` |
| URP materials | `Assets/_Project/Materials/Enemies/Mummy/` |

The mummy has **21 wrapped rigid pieces** on **21 bones**, about 5,900 triangles.
Each jar is about 1,650 triangles and each sarcophagus about 1,170. A sarcophagus
is a hollow case (cut with a boolean) plus a lid hinged on one side. All models,
animations and sounds were generated locally; nothing was downloaded.

| Clip | Frames (30 fps) | Loops | Use |
|---|---|---|---|
| Idle | 0-60 | yes | Standing, arms reaching |
| Shamble | 70-118 | yes | Patrol, one foot dragging |
| Chase | 130-162 | yes | Faster lurch |
| Bite | 170-188 | no | Lunge and snap |
| Hit | 200-210 | no | Flinch |
| Fall | 220-244 | no | Collapse onto its back |
| Down | 250-280 | yes | Lying still |
| Rise | 290-338 | no | Sit up, kneel, stand |
| Entombed | 350-410 | yes | Arms crossed inside the sarcophagus |
| WalkOut | 420-474 | no | Steps down and out of the sarcophagus |

The clips animate the imported model's root node, so the prefab turns and grounds
the model on a separate `Facing` parent; otherwise every frame would undo the turn.

## Building and placing

1. Optional: regenerate the models and sounds:
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python Blender/Mummy/build_mummy.py
   ```
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python Blender/Mummy/verify_fbx.py
   ```
   ```bash
   python3 Blender/Mummy/generate_audio.py
   ```
2. In Unity: **Tools > Lockdown > Build Mummies and Canopic Jars**.
3. Open `Museum_Main` and use **Tools > Lockdown > Place Mummies and Canopic Jars**.
   - It moves the three corner props out of the way.
   - It checks each sarcophagus spot is clear with walkable floor in front, and
     places the sarcophagus with its mummy at the exit spot.
   - Each jar goes against a wall (never a doorway) in its gallery.
   - Each mummy gets its jar, amulet, patrol loop and chase lane.
   - Placement is skipped if `EgyptCurse` already exists. Save the scene afterwards.

## Validation

- `Lockdown.Tests.Mummy.EditMode` (9 tests): dormancy, patrol loop and rejoining,
  sighting and alerts, following the last known position, the 14 s search,
  noise investigation, rising after a knockdown, and death.
- `Lockdown.Tests.Mummy` (Play Mode):
  - prefab structure, clips, the `Facing` pivot, safe jar stands, sarcophagus
    colliders and lid placement, and amulet loot;
  - sealed jars rejecting hits, then breaking from two pistol shots or one hammer blow;
  - rising again while the jar is intact;
  - crumbling and dropping the amulet;
  - a fallen mummy turning to dust when its jar breaks;
  - in the museum, opening a sarcophagus releases only that mummy and unseals only
    its jar. The mummy walks forwards with swinging legs and bites for 15.
- `Blender/Mummy/verify_fbx.py`: all 21 pieces re-import on their bones with no
  offset.
