# Dynamic Spawning Agent

Status: selected fourth IS agent. The HTML prototype supplies an event-driven **fixed-wave baseline**, not adaptive difficulty. Unity integration and any adaptive policy are pending.

References: [room-controller.js](../../game_html/src/room-controller.js), [room-config.js](../../game_html/src/room-config.js) and `spawnWave` in [main.js](../../game_html/src/main.js).

## Existing Baseline

Each room controller observes activation, live enemy count, current wave and an inter-wave timer. Entering a combat room starts its first configured wave once. When no enemies remain, it waits for the configured pause and creates the next wave. Clearing the final wave unlocks the lever. The safe start room has no waves. The game loop stops controller updates while paused or ended.

Mixes/counts and spawn points come from `ROOM_CONFIG`; `spawnWave` creates the configured enemies. Destroyed enemies remain dead instead of individually respawning. Spawning is separate from enemy combat decisions.

The prototype's final room still lists Wasp. The selected roster permits Pursuer, Ambusher and Hound only; remove aerial entries when implementing that roster in runtime code.

## Planned Agent Work

Agree a decision policy before presenting spawning as adaptive AI. Define its observations, alternatives and the rules/scoring that select population, mix, position and delay. Player health, encounter time or existing population are possible inputs, not implemented adaptation.

The Unity implementation should use museum encounter boundaries and reachable spawn points, maintain population limits, avoid repeated activation, respect pause/end states and report decisions for evaluation. Adaptive rules require evidence beyond the fixed-wave lifecycle.

## Validation

Check safe-room behavior, single activation, live counts, waits, final unlock, pause behavior, containment and valid spawn positions. Evaluate added policies under repeatable scenarios and record results in [the AI performance log](../AIPerformanceLog.md).

Reference check: `node scripts/room-controller-test.js` from `game_html/`.
