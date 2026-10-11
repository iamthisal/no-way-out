# System Architecture & Technical Design

## Current Scope

No Way Out is a joint GV and IS project. Unity contains the museum game; `game_html/` contains a warehouse prototype used as an AI behavior reference. Identify which runtime a change targets.

The selected IS agents are **Sentinel/Pursuer, Stalker/Ambusher, Hound, and Dynamic Spawning Agent**. Rex and Wasp are excluded. See [AI scope](AI/README.md) for implementation status.

The environment specification is [museum v3.2, fifteen spaces](WorldBuilder/README.md). Use `Assets/_Project/Scenes/Museum/Museum_Main.unity`. Preserve the room footprints, gallery loop, doorway clearance, P3 toilet cubicles and recorded P4 corrections. The previously referenced `WorldBuilder/Room_Object_Placement.md` is absent from this checkout; confirm placement against the team's current plans. The HTML map is not a replacement for the museum plan. Validate construction and navigation in Unity.

## HTML Reference

| Module under `game_html/src/` | Responsibility |
|---|---|
| `enemy-ai.js` | Pursuer and Ambusher states; shared ranged combat |
| `hound-ai.js` | Sound memory, scoring, tracking, sniffing and biting |
| `room-controller.js` | Entry-triggered waves, alive counts, waits and lever unlocks |
| `room-config.js` | Room bounds, wave mixes, spawn points, pauses and recovery settings |
| `main.js` | Three.js/Cannon integration, EasyStar navigation, perception, cover queries and enemy creation |

The prototype uses Three.js rendering, Cannon ES physics and EasyStar grid A* navigation. Navigation, perception and sound delivery are restricted to the enemy's room. Closed doors block movement, shots and sight. Runtime callbacks supply perception, movement and combat to behavior modules.

Spawning is deterministic: room entry starts a configured wave, enemy deaths permit the next wave after a pause, and the final wave unlocks the room lever. Player-performance adaptation is not implemented. Rex and Wasp remain in the prototype runtime.

## Unity Module Boundaries

The assembly layout is the integration structure, not evidence that the selected AI has already been ported:

```text
Lockdown.AI.Agents
  -> Lockdown.AI.Core
  -> Lockdown.Interfaces
  -> Lockdown.Runtime
Lockdown.Runtime
  -> Lockdown.Interfaces
Lockdown.Tests.EditMode
  -> Lockdown.AI.Core
```

| Assembly | Intended responsibility |
|---|---|
| `Lockdown.Interfaces` | Shared damage/interaction contracts and agreed perception/spawning interfaces |
| `Lockdown.AI.Core` | Engine-independent decisions, scoring, memory and spawning policy where practical |
| `Lockdown.Runtime` | Player, health, weapons, world interactions and movement execution |
| `Lockdown.AI.Agents` | Unity integration of the three ground behaviors and spawning agent |
| `Lockdown.Tests.EditMode` | Focused checks for implemented decisions and transitions |

AI folders currently contain assembly definitions; existing enemy runtime scripts are separate from the selected HTML behaviors. Implement and validate Unity equivalents before describing the four-agent integration as complete.

Keep dependencies acyclic. Player/world systems should communicate through shared contracts/events. Keep physics/rendering outside core decision algorithms. HTML JavaScript is a behavioral reference, not code Unity can execute directly.

## Integration Requirements

- Agree enemy dimensions, navigation clearance, spawn markers and room boundaries with the museum builder. Rebuild/test navigation after geometry changes.
- Supply grounded movement, facing, animation, perception and damage through Unity runtime adapters.
- Preserve sound position, kind and age for Hound; continuous player-position knowledge must not replace sound tracking.
- Supply reachable cover/peek positions and retreat routes for Ambusher, and pursuit/search destinations for Pursuer.
- Give the spawning agent live counts, encounter activation and validated spawn positions. Keep population limits and pause behavior explicit.
- Define/test any adaptive spawning policy separately from the fixed-wave baseline. Its inputs, decisions and IS evaluation evidence remain to be implemented.

The former additive `Bootstrap`, `Env`, `Interactables`, `Agents` and `ModelShowcase` scene layout was a proposal. Check actual scene loading and agree ownership before adopting it for the museum; it does not require replacing `Museum_Main`.
