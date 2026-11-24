# Dynamic Spawning Agent

Status: **implemented in Unity** as an adaptive, utility-based director, alongside the HTML prototype's **fixed-wave baseline**. A measured comparison between the two has not been done.

## Unity director

The director is an unseen stage manager. It spawns enemies at hidden points and quietly removes its own idle, distant ones. It never moves a visible enemy and never tells an enemy where the player is. The director observes the whole museum; the enemies keep their own partial senses.

### Code

| Part | Location | Responsibility |
|---|---|---|
| Decision logic | `AI/Core/Director` | Pure C#: stress, intensity, pacing, filters, scores, utility, log. No physics or NavMesh. |
| Unity side | `AI/Agents/DynamicSpawner` | Percepts, spawn point survey, spawning, removal, overlay. |
| Alarm link | `AI/Agents/Pursuer/AlarmStimulusRelay.cs` | Turns the glass alarm into a sound and one radio call. |
| Markers | `Runtime/World/SpawnPoint.cs` | Describes a place: room, zone, allowed types. |
| Builder | `Editor/SpawnDirectorBuilder.cs` | **Tools > Lockdown > Place Spawn Points** and **Connect Spawn Director in Museum**. |

`AI/Core/Director/IDirectorWorld.cs` is the only question the brain asks of the world.

### Percepts and internal state

- Percepts: recent damage, health, engaged enemies within 25 m, being seen, noise, loot fraction, unanswered alarm, enemy census.
- Stress `S = 0.35 damage + 0.25 injury + 0.25 engaged + 0.15 seen`.
- Intensity moves towards stress every 0.25 s: rate 1.5/s rising, 0.08/s falling.
- Phases: Build-up (target `0.35 + 0.30 x loot`), Peak from 0.75, Relax below 0.6 or after 20 s, Build-up again after at least 25 s and below 0.3. Nothing spawns in Peak or Relax.

### Decision, every 2 s

`U = (target - intensity) x free share x (0.4 threat + 0.35 fit + 0.25 point)`, compared with waiting at 0.15. The best option wins.

- *Free share* is `1 - active enemies / 6`. It was added to the original design, so that each enemy already present makes another one worth less.
- An unanswered alarm adds `0.2 + 0.25 x point` to guards only.
- Fit: hounds for noisy players, guards for open galleries and loot, archers for corridors.
- Point score: `0.4 distance + 0.3 novelty + 0.3 relevance` (near the alarm, or on the way out).
- Hard filters: type allowed, not the safe start room, outside the camera view with a margin **and** no line from the player's eyes, a complete route of 8-40 m, clear floor, not between a player under 30% health and the exit.
- Caps: 6 in total, 2 guards, 2 archers, 4 hounds. They include enemies placed in the scene. 20 s opening grace, 10 s between spawns.

### Glass alarm

`GlassAlarm.IsRinging` becomes true when a connected case breaks, and `AlarmRaised` gives the position of the glass. The relay emits an `Alarm` sound (loudness 10, radius 40 m) and radios the free guard with the shortest complete route. Glass sits inside a case, so the guard is sent to a reachable spot beside it. If nobody can go, for example because the guard is behind closed doors, the alarm stays *unanswered* for 30 s; the relay retries every 2 s, and the director may send a guard in from a hidden point near the glass.

### Spawned enemies

- Guard: patrols the three nearest spawn points, or answers the alarm.
- Hound: investigates a recent alarm, otherwise wanders.
- Archer: assembles from its bones and uses its own brain.

`GlassHoundResponse` keeps its behaviour, but asks the director before each dog, so caps and phases apply.

The director removes only its own spawns: more than 45 m of walking (or unreachable), unseen for 20 s, not engaged and not in a Peak.

### Debugging and evidence

F9 shows the director overlay. F10 writes `DirectorLog.csv` to the persistent data folder. `DirectorScenarioReport` (explicit test) runs three 90 s scenarios and writes to `Logs/Director`. Results are in [the AI performance log](../AIPerformanceLog.md).

### Validation

EditMode tests (`Lockdown.Tests.Director.EditMode`, 43) cover the decision logic. Play Mode tests (`Lockdown.Tests.Director.PlayMode`, 13) cover alarm timing on test glass, and in the real museum: guard response, hidden and reachable spawns, caps, capture, damage, glass hounds and removal.

### Limits

- Spawn points in closed rooms are unused until the player opens the doors.
- Weights and thresholds are hand-tuned. One 227 ms frame was measured in the Editor scenario runs; its cause was not profiled.
- Slow steady damage does not reach the Peak phase: in the attack scenario intensity stayed at 0.30.
- The alarm siren lasts 15 s. Flashing lights from the original design are not implemented.

## HTML fixed-wave baseline

Each room controller observes activation, live enemy count, current wave and an inter-wave timer. Entering a combat room starts its first configured wave once. When no enemies remain, it waits for the configured pause and creates the next wave. Clearing the final wave unlocks the lever. The safe start room has no waves. The game loop stops controller updates while paused or ended.

Mixes/counts and spawn points come from `ROOM_CONFIG`; `spawnWave` creates the configured enemies. Destroyed enemies remain dead instead of individually respawning. Spawning is separate from enemy combat decisions.

The prototype's final room still lists Wasp. The selected roster permits Pursuer, Ambusher and Hound only; remove aerial entries when implementing that roster in runtime code.

References: [room-controller.js](../../game_html/src/room-controller.js), [room-config.js](../../game_html/src/room-config.js) and `spawnWave` in [main.js](../../game_html/src/main.js).

Reference check: `node scripts/room-controller-test.js` from `game_html/`.
