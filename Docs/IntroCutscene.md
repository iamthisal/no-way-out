# Intro cutscene

**Runtime:** Unity game.

**Start game** on the main menu plays a 28.6 s break-in before the run begins: a van parks behind the museum, the
thief gets out, raises a ladder, climbs onto the roof, crosses it, jumps into the courtyard garden and hides behind
the flower pot. The last shot ends at the player's eyes, and the run starts there.

It plays live in the real museum, not as a video file, so the building, light and garden always match the game.

## Rules

- **Space, Enter or Esc** (gamepad South or Start) skips it.
- It plays only after **Start game**. **Restart**, and pressing Play in `Museum_Main`, go straight to the garden.
- Until it ends, every scene root except the museum building and its light is inactive: player, enemies, HUD and spawn
  director have not started yet, so nothing can see or hurt the player.
- The set has no colliders and is removed when the intro ends. It is never part of the NavMesh.

## Player start

The player now starts at (1.6, 0.05, 18.75), behind the flower pot and facing the locked glass door D19. Breaking that
door (two pistol shots) is the way in. Until then the closed door cuts the garden off, so no enemy can reach it and the
spawn director finds no reachable spawn point.

## Shots

| Time (s) | Shot |
|---|---|
| 0.0 - 2.9 | The van arrives along the road |
| 2.9 - 4.7 | From the roof: it parks behind the museum |
| 4.7 - 7.1 | The door slides open and the thief steps out |
| 7.1 - 9.0 | Close-up: a look both ways |
| 9.0 - 11.9 | The ladder is pushed up against the roof |
| 11.9 - 15.3 | Climbing, seen from below |
| 15.3 - 18.4 | Onto the roof |
| 18.4 - 22.0 | Sneaking across to the courtyard |
| 22.0 - 23.7 | Crouching at the edge |
| 23.7 - 25.6 | The jump and landing, seen from the flowers |
| 25.6 - 28.6 | Creeping behind the pot, into the player's view |

## How it is made

Everything is original. The sources are in `Blender/Intro/` and run in background Blender 5.2, in this order:

| Script | Makes |
|---|---|
| `intro_layout.py` | Shared positions and timing, in Unity museum coordinates |
| `build_thief.py` | The thief: 18 bones, 7,544 triangles |
| `animate_thief.py` | The performance (858 frames), `MuseumThief.fbx` and `intro_events.json` |
| `build_van.py` | `HeistVan.fbx` (18,900 triangles, door, wheels, suspension) and `HeistLadder.fbx` |
| `build_soundtrack.py` | `IntroSoundtrack.wav`, synthesised, with footsteps and impacts at the animated times |

The thief is animated by **contacts**: each foot and hand has timed moves between points in the museum, and the hips
follow keyed positions. Two-bone IK bakes every frame, so feet stay planted and hands stay on the rungs. Actors are
animated in museum coordinates and placed at the origin.

In Unity, `Tools > Lockdown > Intro` has three steps, all safe to run again:

1. **Import Intro Models**: materials, textures and import settings. Animation compression is off to keep contacts exact.
2. **Build Intro Cutscene** (with `Museum_Main` open): the outdoor set, camera clip, Timeline and prefab.
3. **Place Intro in Museum**: adds the prefab, lists the scenery that stays on and moves the player.

`Animations/Intro/IntroTimeline.playable` can be scrubbed in the Timeline window. `Runtime/Intro/IntroCutscene.cs` plays
it, holds the game back, and draws the fades, bars and skip prompt.

If the museum roofs, courtyard or flower pot move, update `intro_layout.py` and the shots, then run the steps again.

## Validation

`Lockdown.Tests.Intro` (Play Mode, 6 tests, 25 November 2026): it starts from the menu with gameplay held back, skips,
ends by itself, does not replay on Restart, the thief touches the ladder, roof and garden, and the player can leave the
garden once the door breaks. `CaptureFrames` (explicit) saves frames to `Temp/Intro` for a visual check.

The intro uses the museum's current daylight. A build has not been tested.
