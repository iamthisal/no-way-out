# Contributing

This repository contains both GV game work and IS agent work. Commit related code and documentation together; keep unrelated Unity package/settings changes in separate commits.

## Scope and Ownership

The selected agents are Sentinel/Pursuer, Stalker/Ambusher, Hound and Dynamic Spawning Agent. Rex and Wasp are excluded from current scope. Read [AI scope](Docs/AI/README.md) before changing agent code. Confirm student-to-agent ownership with the team; the superseded README allocation no longer applies.

Coordinate shared scene, prefab and navigation edits with their owner. Use [the museum manual](Docs/WorldBuilder/README.md) for environment plans. Preserve Unity `.meta` files and use Git LFS for assets covered by `.gitattributes`.

## Changes and Reviews

Branch from `develop`; use names such as `feature/hound-sound-tracking` or `feature/dynamic-spawning-agent`. PRs into `develop` require one approving review. Keep `main` buildable for demos/submission.

Explain which runtime changed: Unity, HTML prototype, or both. Document agent inputs, decisions and observed behavior. Distinguish implemented algorithms from proposals, and fixed waves from adaptive spawning. HTML checks do not validate a Unity port.

## Verification

For HTML changes, run relevant checks from `game_html/`:

```bash
node scripts/hound-test.js
node scripts/room-controller-test.js
npm run build
```

Browser checks have separate server/Chromium requirements in [the prototype README](game_html/README.md). For Unity changes, run relevant Unity tests and verify movement, perception, door blocking and spawning in the museum. Record measured results in [the AI performance log](Docs/AIPerformanceLog.md).
