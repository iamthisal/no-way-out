# Sentinel / Pursuer

Status: selected ground enemy; implemented as `Pursuer` in [enemy-ai.js](../../game_html/src/enemy-ai.js). Unity integration is pending.

## Behavior Reference

Inputs include visibility, room-local noise/alerts, last confirmed player position, patrol waypoints and runtime movement/combat queries.

The agent patrols until it sees the player or receives a noise/alert location. Visual contact starts pursuit and updates its last known position. Without contact, it investigates noise/alerts or follows the last confirmed location. After more than two seconds without sight during pursuit, it starts a fourteen-second search, then returns to patrol.

`main.js` supplies coordinated combat positions for pressing forward or flanking. Shared combat provides warned, aimed bursts and blocker-aware bolts. Navigation uses room-restricted EasyStar grid paths.

This is state-based logic. Hierarchical FSM and suspicion decay must not be reported as implemented without corresponding code and evidence.

## Unity Validation

Check patrol continuity, sight/door blocking, last-known-position updates, loss-of-contact search, coordinated positions, attack warnings and valid paths. Measure decision/path cost and simultaneous-agent behavior in the museum.
