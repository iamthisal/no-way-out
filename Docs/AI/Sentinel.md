# Sentinel / Pursuer

The Unity museum pursuer implements a finite-state machine with **Patrol, Investigate, Chase, Attack, Search, Return and Dead** states. Its inputs are sight, sound snapshots, ally alerts, navigation status, elapsed time, target health and its own health. The HTML [Pursuer](../../game_html/src/enemy-ai.js) remains the behavior reference.

## Code ownership and review map

Decision-making lives in [`Scripts/AI/Agents/Pursuer`](../../Assets/_Project/Scripts/AI/Agents/Pursuer):

| File | Responsibility |
|---|---|
| `PursuerAgent.cs` | FSM, priorities, remembered sightings and choosing actions. |
| `PursuerPerception.cs` | Sight, hearing, clue memory and ally alerts. |
| `PursuerInvestigation.cs`, `PursuerSearch.cs` | Investigation timing and bounded search candidates. |
| `PursuerAttackCycle.cs` | Warning, burst and cooldown decisions. |
| `PursuerFormation.cs` | Selecting distinct reachable approach positions. |
| `PursuerSettings.cs` | Configurable decision parameters. |
| `PursuerStimulusRelay.cs` | Gameplay integration: translates weapon, movement and glass events into AI inputs. |

Execution lives in [`Scripts/Runtime/Agents/Pursuer`](../../Assets/_Project/Scripts/Runtime/Agents/Pursuer), namespace `Lockdown.Agents.Pursuer`:

| File | Responsibility |
|---|---|
| `PursuerController.cs` | Command boundary: move, stop, turn, fire, warning feedback and body shutdown. |
| `PursuerNavigation.cs` | NavMesh paths, stopping distance, physical obstacle checks and movement progress. |
| `PursuerLocomotion.cs` | Drives animation from actual velocity. |
| `PursuerPresentation.cs` | Warning sound and shot, hit and death animation feedback. |

The dependency is `Lockdown.AI.Agents` → `Lockdown.Runtime`. Runtime controls do not reference the AI assembly or choose FSM states. The brain asks the controller to act and reads execution results. Shared `Runtime/Enemies/EnemyShooting.cs` and `Runtime/Combat/Health.cs` remain reusable execution components. Editor builders and verification stay in `Scripts/Editor`; they are not gameplay decision code.

Existing script GUIDs are preserved and moved components declare their previous namespace/assembly for Unity serialization. The gameplay prefab contains the controller explicitly. This separation changes code ownership, not gunshot/damage reaction policies.

## Decisions and transitions

| State | Behavior | Exit condition |
|---|---|---|
| Patrol | Loop through the four assigned reachable waypoints at 2 m/s. | Sight starts pursuit/combat; a noise or alert starts investigation. |
| Investigate | Walk to the clue location, then inspect for 1.5 seconds. | A sighting interrupts it; arrival/timeout or a blocked route starts a local search. |
| Chase | Run at 4 m/s toward the visible player, a validated flank position, or the last confirmed position. | A clear firing opportunity starts Attack; three seconds without sight starts Search. |
| Attack | Stop, face the target, warn for 0.65 seconds, fire up to three shots at 0.18-second intervals, then cool down for 1.5 seconds. | Lost sight cancels the burst; leaving firing conditions resumes Chase. |
| Search | Check up to 17 reachable positions around the clue/last sighting within a 3 m radius; scan when candidates are exhausted. | A sighting interrupts it; the 14-second search budget ends in Return. |
| Return | Navigate toward the nearest reachable patrol waypoint. | Reaching the route resumes Patrol; new evidence may interrupt. |
| Dead | Stop navigation and shooting, disable colliders, release the formation slot, and play death animation. | Terminal for this spawned character; restarting the encounter creates a fresh instance. |

Death has highest priority. Direct sight overrides clues. Pursuit and search are not redirected by lower-priority sounds. No utility scoring is required for these explicit FSM decisions.

## Perception, memory and communication

- A new player is acquired within 20 m and a 100-degree horizontal cone. While pursuing, navigation turns do not discard an unobstructed target.
- Solid colliders block sight. Trigger colliders and the two characters' own colliders are ignored. An inactive or dead target is not visible.
- Only confirmed sightings update the remembered player position. The pursuer follows that snapshot for three seconds after losing sight; it cannot follow unseen movements through a wall.
- One `PursuerStimulusRelay` connects broken glass, player footsteps/sprinting and fired pistol shots to the existing `HoundNoiseBus`. Audio playback alone is not a clue.
- Hearing respects the sound's radius, a 24 m maximum range, age, and wall attenuation. Gunshots outrank distractions, which outrank footsteps.
- Confirmed sightings broadcast position snapshots to nearby allies, at most once per second. Recipients investigate without rebroadcasting, preventing alert loops.

## Movement and cooperation

`PursuerNavigation` validates complete baked-NavMesh paths and samples nearby walkable destinations. Physical capsule sweeps stop movement at newly closed solid doors while obstacle carving catches up. Routes can fail or time out instead of repeatedly pushing through geometry.

The direct pursuer can attack as soon as firing conditions are met. Additional pursuers sharing a target select distinct flank slots, subject to complete-path and clear-shot validation. Unreachable flanks fall back to direct pursuit. Disable/death removes the character from formation membership.

The museum currently contains one pursuer. Distinct formation slots and unreachable-slot fallback are covered by automated component tests; a large-pack performance benchmark has not been performed.

## Weapon, damage and presentation

The existing `EnemyShooting` component runs in explicit AI-controlled mode on this prefab. The FSM owns warning, burst and cooldown timing; the weapon executes hits, tracers, muzzle flashes and sound. The legacy wandering enemy has been removed from Museum_Main; its reusable prefab retains autonomous shooting for regression fixtures.

Shots check both body visibility and the muzzle-to-player line; solid blockers and other characters can block damage. Each successful pursuer shot deals 10 damage. The pursuer uses the shared 100-health `Health` component and can be killed with the player's weapons.

Imported animations cover idle, walk, armed run, shots, hit reactions and death. A warning tone precedes firing. Root motion is disabled; navigation owns movement. The Inspector shows the current state, decision reason, visibility and last confirmed position in Play Mode.

## Setup and validation

Open `Museum_Main` in Edit mode and use **Tools > Lockdown > Connect Pursuer AI in Museum** to rebuild the gameplay prefab, reconnect the actual player, validate four patrol waypoints, and add the scene's single gameplay-signal relay. A dirty scene is copied to `Assets/_Recovery` before setup saves it. The presentation-only prefab remains separate.

Tune the serialized `PursuerSettings` on the agent. The gameplay prefab requires a player target and patrol points when instantiated outside the museum setup.

- Nine NUnit Edit Mode tests cover warning/burst timing, cancellation, failed shots, investigation timing, bounded search, sight/occlusion, and formation slots.
- Legacy combat tests instantiate their own wandering-guard prefab and isolate the pursuer. The saved museum contains only the pursuer.
- The editor-only `PursuerAIVerification` exercises the actual museum: patrol, chase, turning, occlusion, memory, search/return, noise investigation, real player damage, blocked shots, death cleanup and animation.
- Runtime checks temporarily shorten inspection/search timers for speed; the saved prefab retains 1.5-second inspection and 14-second search defaults. Test objects and player movements are never saved.
- Decision timing is recorded with `Pursuer.Decision`. See [the performance log](../AIPerformanceLog.md) and [verification report](../Characters/PursuerAIVerification.txt).
- Repeat the museum check with `-executeMethod Lockdown.Editor.PursuerAIVerification.RunBatch` in a Unity batch session, without `-quit`; the verifier exits with the result.

## Scope limits

This completes the planned pursuer FSM and its game integration. It does not implement melee, opening doors, adaptive learning, or dynamic spawning. Spawning remains a separate agent's responsibility. Firing uses the existing hitscan weapon rather than the HTML prototype's flying bolts; it does not reproduce that projectile-dodging model or its locked-aim window. The Unity investigation/chase timings are configurable and differ from the HTML reference's two-second loss timer.

A hierarchical FSM, behavior tree, utility-scoring pursuer, or learned policy must not be reported as implemented. The architecture here is an explicit FSM with perception, short-term memory, prioritized evidence, bounded local search and coordinated approach selection.
