# Security Hound — Behaviour Design

## Purpose

The security hound adds a sound-driven threat to the museum. Loud actions should attract attention, while quiet movement gives the thief an opportunity to avoid detection. This document describes the design and its validation criteria; it is not a dated development log or a test-results report.

## Information available to the hound

Each noise records its position, type, loudness, hearing radius, and emission time. The hound investigates that recorded position rather than continuously following the thief's current position. A nearby combat check may use the player's position to decide whether a bite is possible.

Noise categories include walking, sprinting, crouching, gunshots, distractions, and custom events. AI noise events are separate from audible sound effects: playing an audio clip alone does not notify the hound.

## Sound memory and selection

The memory is bounded to 64 clues. Older and more distant sounds receive lower scores:

`score = loudness × exp(-0.55 × ageSeconds) / (1 + distanceMetres / 5)`

Clues expire after 12 seconds or when their retained score becomes too small. Decisions are evaluated approximately every 0.5 seconds. The tracking threshold is 1.2, and switching to a competing clue normally requires a score 1.3 times the current clue's score. These limits reduce repeated target switching and prevent memory from growing indefinitely.

| Noise | Relative loudness | Hearing radius |
| --- | ---: | ---: |
| Walking | 0.8 | 10 m |
| Sprinting | 5 | 30 m |
| Crouching | 0.08 | 3 m |
| Gunshot | 12 | 48 m |
| Distraction | 6 | 24 m |

Walking and crouching alone fall below the tracking threshold with these settings. Hearing also checks obstructions. The current obstruction multiplier is zero, so a blocking wall prevents hearing along the direct path. This is a simplified visibility-style sound check, not an acoustic simulation around corners.

## Behaviour states

| State | Behaviour | Typical next step |
| --- | --- | --- |
| Wander | Move between nearby reachable positions, with pauses. | Track when a useful sound is accepted. |
| Track | Navigate towards the remembered sound location. | Sniff on arrival; abandon an invalid or blocked pursuit. |
| Sniff | Pause and investigate the location for about 3 seconds. | Search, unless a stronger clue changes the decision. |
| Search | Investigate nearby positions for about 6 seconds. | Return to Wander if no useful clue remains. |
| Dead | Stop movement, hearing, and attacks. | Remain inactive. |

New useful sounds can interrupt investigation. The agent should not acquire distant live player coordinates merely because an earlier noise was heard.

## Navigation and combat

Unity NavMesh provides pathfinding; this design does not implement a custom A* search. The hound should reject unreachable clues and detect obstructed or stuck movement. A closed physical door must prevent passage even if an older navigation bake still contains a route through its opening.

Default movement speeds are 2.4 m/s while wandering and 4.8 m/s while tracking. An alerted hound may bite within 1.45 m, dealing 12 damage with a 1.2-second cooldown. Obstructions must block bites. Damage and death use the project's health system.

## Integration boundaries

- The player-noise adapter connects actual movement and successful shots to noise events without requiring changes to the player and weapon source files.
- A noise emitter supports explicit distractions and optional collision-triggered events.
- Door opening, rummaging, and item interactions require explicit event integration; they are not automatically detected from their audio.
- Crouch-noise support does not itself implement a crouching player controller.
- Door interaction and navigation updates must be coordinated with the systems engineer.
- The isolated scene `Assets/Scenes/Museum_Dog_Test.unity` is used for hound testing before integration into the main museum scene.

## Validation checklist

- Standing still does not emit repeated footsteps.
- Quiet walking does not start tracking under the default settings.
- Nearby sprinting and successful shots create useful clues when unobstructed.
- A single test noise produces tracking, investigation, searching, and a return to wandering.
- The dog follows the recorded noise position rather than silently tracking a distant moving player.
- Walls block hearing and bites according to the configured settings.
- Unreachable destinations and closed doors do not cause movement through solid geometry.
- Bites apply damage at the configured interval.
- Death stops navigation, hearing, and attacks.
- The Console has no new errors during testing.

Record actual test outcomes separately, including the scene, configuration, observed result, and any remaining integration work.
