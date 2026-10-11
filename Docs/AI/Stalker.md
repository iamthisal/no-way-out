# Stalker / Ambusher

Status: selected ground enemy; implemented as `Ambusher` in [enemy-ai.js](../../game_html/src/enemy-ai.js), with cover queries in `main.js`. Unity integration is pending.

## Behavior Reference

Inputs include visibility, last confirmed player position, shared alerts, incoming fire, player proximity/approach, cover/peek locations, path reachability and navigation stalls.

The agent alternates hiding and peeking, fires a two-shot burst, then hides again. Hide/attack timers count after arrival at its post. Cover selection evaluates concealment, firing angle, distance and positions reserved by other Stalkers. It relocates when hit, approached too closely, or unable to maintain its firing angle. A reachable escape point provides a retreat fallback; stalled routes trigger replanning.

The reference uses explicit states and scored cover selection, with room-restricted EasyStar navigation. The earlier shadow-cost pathfinding and stealth-angle proposal must not be reported as implemented.

## Unity Validation

Check reachable cover/peek pairs, nonconflicting cover selection, two-shot attacks, timers after arrival, close-range retreat, hit reactions and stalled-route recovery. Museum exhibits must support both concealment and firing positions.
