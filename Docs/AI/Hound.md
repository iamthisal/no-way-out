# Hound

Status: selected ground enemy; implemented in [hound-ai.js](../../game_html/src/hound-ai.js), with sound delivery/navigation in `main.js`. Unity integration is pending.

## Behavior Reference

The agent hears gunshots, sprinting, walking, crouching and distraction sounds. Events carry position, loudness and age. Runtime sound delivery is restricted to its room and an unobstructed line; `hear` also checks radius and enemy health.

```text
score = loudness * exp(-age * 0.55) / (1 + distance / 5)
```

It retains at most 64 events and re-scores every 0.5 seconds. Tracking requires a score of at least 1.2; target switching requires a candidate at least 1.3 times the current score. Unreachable candidates and memories below 0.08 are discarded.

States are `wander`, `track` and `sniff`. Reaching a sound location, losing useful memory or failing a route starts a three-second sniff. It then forgets that target and resumes wandering. Close proximity permits a bite while tracking/sniffing, with a 1.2-second cooldown; player position does not supply a distant pursuit target.

## Unity Validation

Check loud/quiet selection, memory decay, target switching, unreachable sounds, room/door restrictions, sniff duration, wandering and bite range/cooldown. Preserve sound-driven pursuit when connecting Unity player/weapon events.

Reference check: `node scripts/hound-test.js` from `game_html/`.
