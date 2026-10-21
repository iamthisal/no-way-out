# Sentinel / Pursuer

The Unity museum pursuer is an **arrest-first guard**. It approaches a thief without firing, attempts handcuffs at close range, and chases/intercepts a fleeing thief. Only an attributed player hit authorizes armed retaliation. Once attacked, it advances while firing and may briefly use nearby forward cover. The HTML prototype is an earlier reference and does not implement this Unity arrest policy.

## Code ownership and review map

Decision-making lives in [`Scripts/AI/Agents/Pursuer`](../../Assets/_Project/Scripts/AI/Agents/Pursuer), assembly `Lockdown.AI.Agents`:

| File | Responsibility |
|---|---|
| `PursuerAgent.cs` | FSM, priorities, evidence memory and choosing actions. |
| `PursuerPerception.cs` | Sight, hearing, clue memory and ally alerts. |
| `PursuerArrest.cs` | Continuous-contact arrest timing and cancellation. |
| `PursuerInterception.cs` | Visible motion observations, expiring predictions and route-cost comparison. |
| `PursuerCornerSearch.cs` | Ranks reachable forward/corner hypotheses using observed heading, occlusion and route length. |
| `PursuerAdvanceCover.cs` | Bounded selection of reachable forward cover after a hit. |
| `PursuerInvestigation.cs`, `PursuerSearch.cs` | Investigation timing and bounded search candidates. |
| `PursuerAttackCycle.cs` | Warning, burst and cooldown decisions. |
| `PursuerFormation.cs` | Selecting distinct reachable approach positions during retaliation. |
| `PursuerSettings.cs` | Configurable decision parameters. |
| `PursuerStimulusRelay.cs` | Gameplay integration: weapon, movement and glass events become AI inputs. |

Execution lives in [`Scripts/Runtime/Agents/Pursuer`](../../Assets/_Project/Scripts/Runtime/Agents/Pursuer), namespace `Lockdown.Agents.Pursuer`:

| File | Responsibility |
|---|---|
| `PursuerController.cs` | Move, stop, turn, fire, arrest reach/capture, feedback, attributed-hit events and body shutdown. |
| `PursuerNavigation.cs` | NavMesh path queries, movement, physical obstacle checks and progress. |
| `PursuerLocomotion.cs` | Animation from actual velocity. |
| `PursuerPresentation.cs` | Warning sound, arrest/status HUD, shot/hit/death animation feedback. |

Dependencies point from AI to Runtime. Runtime never references the AI assembly or chooses FSM states. Shared `Runtime/Enemies/EnemyShooting.cs` and `Runtime/Combat/Health.cs` execute shooting and damage. `Runtime/Player/PlayerVitals.cs` owns capture, control lock and restart. `FirstPersonWeapon.cs` supplies the actual player as damage instigator; other `IDamageable` implementations keep their existing damage API. Builders and verification remain under `Scripts/Editor`.

Script GUIDs and original enum values are preserved. New states are appended for serialized compatibility. The gameplay prefab explicitly contains its runtime controller.

## Decisions and transitions

| State | Behavior | Exit condition |
|---|---|---|
| Patrol | Loop through the four reachable waypoints at 2 m/s. | Sight starts Approach; noise/alert starts Investigate. |
| Approach | Close at 2.8 m/s with an arrest warning; never fire. | Clear reach starts Arrest; observed fleeing starts Chase/Intercept; an attributed hit authorizes retaliation. |
| Arrest | Stop within 1.6 m and maintain unobstructed reach for 1.5 seconds; show an escape warning and progress bar. | Leaving reach or breaking sight cancels progress; completion captures the actual player; a hit interrupts arrest. |
| Chase | Run at 4 m/s toward the last confirmed location. Running alone never authorizes fire. | Useful prediction starts Intercept; clear reach starts Arrest; three seconds without evidence starts Search. |
| Intercept | Navigate to a predicted ahead position using a complete NavMesh route. After losing sight, close on a frozen likely escape area. | Replan from visible movement; after three seconds of lost contact, continue in Search instead of discarding the area. |
| Attack | After a player hit, close at 2.8 m/s to a 2.8 m standoff while firing warned three-shot bursts. | Lost sight cancels fire; range or obstacles require pursuit. |
| AdvanceCover | After a hit, use a reachable forward cover position for at most 1.1 seconds; fire only with a clear shot. | Resume armed pressure; six-second selection cooldown; no retreat positions. |
| Investigate | Walk to a clue snapshot, then inspect for 1.5 seconds. | Sight interrupts; arrival/timeout or blocked route starts Search. |
| Search | Travel to ranked escape areas, scan at arrivals, then check nearby positions within 4 m. | Sight interrupts; a separate 12-second travel limit bounds unreachable areas. The 18-second inspection budget starts at first arrival and then ends in Return. |
| Return | Navigate to the nearest reachable patrol waypoint. | Reaching the route resumes Patrol; evidence can interrupt. |
| Caught | Stop pursuing the captured thief; player controls are disabled without lethal damage. | Player restarts the encounter. |
| Dead | Stop navigation/fire, disable colliders, release formation slot and play death animation. | Terminal for this instance. |

Death has highest priority. Sight overrides clues. A confirmed player hit interrupts arrest and enables retaliation for this guard's lifetime. Fleeing, merely equipping a gun, shooting elsewhere, and unattributed environmental damage do not authorize shooting. Restart creates a fresh non-hostile guard.

## Interception and aggressive cover

`PursuerInterception` estimates planar velocity from consecutive visible positions. Reacquisition after hidden movement, large observation gaps and teleports reset the estimate. Every 0.5 seconds, and immediately when fleeing begins, it compares three ahead positions up to 1.5 seconds ahead. It measures complete route lengths from guard to player, player to prediction and guard to prediction. A candidate must save at least 0.6 m against following the player's route and have a plausible arrival time (no more than 0.75 seconds late). The score penalizes late arrival. Unreachable or unhelpful predictions fall back to direct pursuit.

Predictions are guesses, not guaranteed exit choices. Live interception requires recent visible observations. On losing sight, the last observed movement is instead frozen into bounded search hypotheses; those guesses are not discarded merely because the two-second live-prediction freshness window expires. Hidden player transforms never update them. This is a custom interception decision over Unity's built-in NavMesh pathfinding, not a separately implemented A* algorithm.

`PursuerCornerSearch` considers 15 candidates: three distances up to 6 m along the observed heading and directions 45 or 90 degrees to either side. It rejects incomplete routes, excessive detours from the last sighting and destinations beyond the travel budget. Heading agreement, occlusion behind solid geometry and practical guard-route length rank the candidates. At most three separated hypotheses are retained, followed by the actual last-known position as a fallback. The guard takes a complete NavMesh route directly to each selected area, allowing a shorter available route instead of replaying the player's footsteps.

The guard closes on the first hypothesis during its three-second contact-memory period, then continues travelling in Search if necessary. Travel time no longer consumes the full inspection budget. After reaching the area, it gets 18 seconds to inspect, including 1.5-second full-turn scans at arrivals and movement to other hypotheses or nearby search points. Later arrivals do not reset that budget indefinitely. Each later route has a five-second movement limit; unreachable/stuck points are skipped. If every route is blocked, the 12-second travel budget permits a bounded return to patrol.

Cover candidates must move at least 0.3 m closer to the threat, have a complete route of at most 4.5 m, and be screened by solid geometry. No suitable candidate means keep advancing. Brief cover and a cooldown prevent persistent hiding. Additional guards may reserve distinct reachable flank slots during retaliation; the museum currently has one guard.

`PursuerNavigation` validates complete baked-NavMesh routes and nearby walkable samples. Physical capsule sweeps stop at solid doors while carving catches up. Routes can fail or time out instead of repeatedly pushing through geometry.

## Evidence and combat

Sight acquires a target within 20 m and a 100-degree horizontal cone. Tracking an established contact ignores the acquisition cone; walls still block sight. Trigger colliders and the characters' own colliders are ignored. Inactive/dead targets are not visible. A player hit supplies one impact-time attacker-position snapshot, including a hit from behind; it never grants later access to hidden movement. Confirmed evidence is retained for three seconds before searching.

The existing sound relay and hearing code remain: glass, footsteps and pistol shots publish snapshots; hearing applies range, age and wall attenuation. Active pursuit/search do not redirect to lower-priority noise. **Player-reported unreliable gunshot hearing remains a separate unresolved issue.** Retaliation uses attributed bullet damage and does not depend on that hearing path. Nearby confirmed sightings broadcast snapshots without recursive alert loops.

`EnemyShooting` is explicitly controlled by the brain through `PursuerController`. After provocation, a 0.65-second warning precedes up to three 10-damage shots spaced 0.18 seconds apart, with a 1.5-second cooldown. Both body and muzzle sightlines must be clear. Losing sight cancels the burst. The guard has 100 health. The legacy wandering enemy is absent from Museum_Main; its prefab remains for regression fixtures.

Capture shows **Caught - handcuffed**, releases the cursor, and disables movement, equipment and weapons. Restart reloads the encounter with restored health, controls and a non-hostile guard. The arrest attempt is represented by a timed HUD; a dedicated handcuff animation/prop has not been authored. Existing imported locomotion, firing, hit and death animations remain.

## Setup and validation

Open Museum_Main in Edit mode. **Tools > Lockdown > Connect Pursuer AI in Museum** rebuilds the gameplay prefab, assigns the actual player and four patrol points, and connects the existing signal relay. Dirty scene work is backed up under `Assets/_Recovery`. Routine tactics updates preserve prefab/scene references rather than rebuilding the environment.

Tune `PursuerSettings` on the brain. The gameplay prefab needs a player target and patrol points outside museum setup. The Inspector exposes state, decision reason, visibility, last confirmed position, retaliation, arrest progress, interception prediction, search destination and whether the search area has been reached.

The decision suite covers attack timing, investigation, bounded search, sight, formations, continuous arrest cancellation, observed-motion validity, route savings/arrival feasibility, forward-only cover, arrival-based inspection timing and ranked corner hypotheses. The museum verifier exercises an actual occluded museum corner, direct navigation to the selected area, persistence after arrival, eventual give-up, hidden-position isolation, NavMesh interception, no-fire first contact, arrest/escape, a real pistol hit from behind, retaliation, pressure, wall blocking, death, capture and restart. Temporary shortened timers and test objects are never saved. Legacy combat tests instantiate test-only guards.

Results and mixed-scenario decision timings are recorded in [the encounter report](../Characters/PursuerAIVerification.txt), [NUnit results](../Characters/PursuerDecisionTests.xml) and [performance log](../AIPerformanceLog.md). These samples are not a steady-state or multi-agent benchmark. Run the museum verifier via the editor request hook or `-executeMethod Lockdown.Editor.PursuerAIVerification.RunBatch` in a separate Unity batch session, without `-quit`.

## Scope limits

No punching/melee attack, door opening, adaptive learning, dynamic spawning, bespoke handcuff animation, or reliable player gunshot-hearing fix is claimed. This is an explicit FSM with evidence memory, bounded search, route-based prediction and tactical forward-cover selection. It is not a behavior tree, hierarchical FSM, learned policy or general utility-scoring system. Interception does not guarantee catching a sprinting player and does not know their intended destination.
