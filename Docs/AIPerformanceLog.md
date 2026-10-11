# AI Performance and Evaluation Log

Current scope: Sentinel/Pursuer, Stalker/Ambusher, Hound and Dynamic Spawning Agent. See [AI scope](AI/README.md). No performance measurements have been recorded in this log yet.

Identify runtime (HTML or Unity), commit, hardware, room/scene, population and repeatable scenario for each result. HTML measurements are not Unity evidence; fixed waves are not adaptive difficulty.

| Date / commit | Runtime and conditions | Agent / scenario | Decision or path cost | Frame / allocation impact | Behavior result and evidence |
|---|---|---|---|---|---|
| Not measured | Baseline pending | Selected four-agent scope | Pending | Pending | Record actual checks and measurements |

## Evaluation Scenarios

- Pursuer: patrol, detection, pursuit, loss-of-contact search and coordinated attack positions.
- Ambusher: cover/peek selection, conflicting cover requests, incoming-fire retreat and stalled routes.
- Hound: competing sounds, quiet movement, aging memory, target switching and unreachable sounds.
- Spawning: activation, wave completion, population limits, pauses and final unlock. For future adaptive policies, record observations, decisions and comparison with the fixed-wave baseline.

Record observed outcomes, failures and capture/test output. Measure rendering separately when attributing AI cost. World optimization has its own [log](OptimisationLog.md).
