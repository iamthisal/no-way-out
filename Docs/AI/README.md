# Current AI Scope

The joint GV/IS project uses **three ground enemy agents and a dynamic spawning agent**, with the HTML prototype as the behavior reference. The old lineup containing Rex and Wasp is superseded.

| Selected agent | Reference under `game_html/src/` | Existing decision approach |
|---|---|---|
| [Sentinel / Pursuer](Sentinel.md) | `enemy-ai.js`, `Pursuer` | State-based patrol, investigation, chase, search and return |
| [Stalker / Ambusher](Stalker.md) | `enemy-ai.js`, `Ambusher` | Cover/peek/retreat states with scored cover selection in `main.js` |
| [Hound](Hound.md) | `hound-ai.js`, `Hound` | Sound utility scoring, decaying memory and wander/track/sniff states |
| [Dynamic Spawning Agent](DynamicSpawner.md) | `room-controller.js`, `room-config.js`, `spawnWave` in `main.js` | Event-driven configured waves; adaptive policy pending |

Sentinel and Stalker are retained as prototype names for Pursuer and Ambusher. They do not imply the earlier proposed hierarchical FSM or shadow-weighted pathfinding algorithms. Record the actual implementation used for assessment.

## Implementation Status

- The HTML prototype implements the three enemy behaviors and a fixed-wave spawning baseline.
- Its runtime still creates Rex and Wasp, and Room 3 still has Wasp wave entries. Both are excluded from selected scope; runtime removal has not been performed by this documentation update.
- Unity's museum pursuer implements a seven-state FSM with sight, hearing, ally alerts, investigation, remembered pursuit, warned burst attacks, bounded search, return, formation slots and death. See [the Unity pursuer implementation and limits](Sentinel.md). Other agents and the spawning policy must be assessed against their own runtime implementations.
- The spawning agent is the fourth IS responsibility. Its baseline reacts to room entry, enemy deaths and timers. Performance-based difficulty adjustment or other adaptive decisions require an explicit policy and implementation.
- Student-to-agent ownership must be agreed by the team. The previous role mapping is no longer authoritative.

## Integration

Unity agent folders under `Assets/_Project/Scripts/AI/Agents/` are `Pursuer/`, `Ambusher/`, `Hound/` and `DynamicSpawner/`. Pursuer and Hound contain runtime implementations; check each specification against the Unity scripts before claiming the full prototype feature set. The former Sentinel, Stalker, Rex and Wasp folders were renamed with their Unity `.meta` files to preserve folder GUIDs; existing folder references now resolve to the new names.

Use museum room boundaries and validated navigation/spawn markers when porting behavior. Keep decisions separate from movement, rendering and damage execution. Validate reachable cover, sound delivery, closed-door blocking, enemy counts and pause behavior in Unity.

Rex/Wasp files remain as retirement notices for old links. Use the four selected specifications above for current implementation and assessment documentation.
