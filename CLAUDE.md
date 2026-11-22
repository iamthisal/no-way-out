# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

"No Way Out" is a joint SE3032 Graphics & Visualisation (GV) + SE3062 Intelligent Systems (IS) project with two runtimes. Every change must state which one it targets:

- **Unity game** (Unity 6000.6.3f1, URP, Input System): an abandoned museum. Main scene: `Assets/_Project/Scenes/Museum/Museum_Main.unity`. All project code is under `Assets/_Project/Scripts`.
- **`game_html/`**: a Three.js + Cannon ES + EasyStar warehouse prototype. It is the *behaviour reference* for the enemies, not the museum map. Unity cannot run its JS, and passing HTML checks does not validate a Unity port.

IS scope is exactly four agents: **Sentinel/Pursuer, Stalker/Ambusher (the Unity Skeleton Archer), Hound, and the Dynamic Spawning Agent**. Rex and Wasp are out of scope. They still run in the HTML prototype, and `Docs/AI/Rex.md` and `Docs/AI/Wasp.md` remain only as retirement notices. In code and docs, distinguish what is implemented from what is proposed, and fixed-wave spawning from adaptive spawning. The adaptive spawning policy is not implemented yet.

## Commands

Unity tests (Unity Test Framework / NUnit):
- With the editor open, use the Test Runner window or the UnityMCP `run_tests` tool, filtered to one assembly, class or test name.
- Command-line runs only work while the project is **closed** in the editor:
  ```
  "C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Unity.exe" -batchmode -projectPath . -runTests \
    -testPlatform EditMode -testFilter "Lockdown.Tests.Hound" -testResults Logs/TestResults.xml
  ```
  Assemblies with `includePlatforms: ["Editor"]` are EditMode; the rest (e.g. `Hound/PlayMode`) run as PlayMode.
- Pursuer decision tests: menu `Tools/Lockdown/Verify Pursuer Decision Tests` (`PursuerRegressionRunner`).
- Pursuer museum encounter verifier: `-executeMethod Lockdown.Editor.PursuerAIVerification.RunBatch` in a separate batch session **without `-quit`**. The report goes to `Logs/PursuerAIVerification.txt`.

HTML prototype (run from `game_html/`):
```
npm install && npm run dev          # http://127.0.0.1:4173/
npm run build
node scripts/hound-test.js
node scripts/room-controller-test.js
node scripts/room-flow-test.cjs     # needs dev server + Chromium debugging on port 9222
```

## Unity architecture

Each assembly definition is a module boundary, and dependencies must stay acyclic:

```
Lockdown.AI.Agents -> Lockdown.AI.Core, Lockdown.Runtime, Lockdown.Interfaces
Lockdown.Runtime   -> Lockdown.Interfaces, Unity.InputSystem
Lockdown.AI.Core   -> (none)
```

- **`AI/Core`** holds decision logic (scoring, memory, A*, brains). It uses only UnityEngine math types (`Vector3`, `Mathf`): no MonoBehaviours, physics, raycasts or NavMesh. It asks for world facts through query interfaces, and the Unity agent answers them with NavMesh and raycasts (e.g. `IAmbusherWorld` in `AmbusherBrain.cs`). That separation is what makes the EditMode tests possible.
- **`AI/Agents`** contains the MonoBehaviours that perceive, run the FSM and pick actions (Pursuer, Hound, `Ambusher/SkeletonArcherAgent`).
- **`Runtime`** executes actions and owns gameplay systems: movement, navigation, animation, shooting, `Combat/Health`, player, weapons, and world objects such as doors, glass and loot. **Runtime never references the AI assembly or chooses FSM states.** Example: `AI/Agents/Pursuer/PursuerAgent` decides, while `Runtime/Agents/Pursuer/PursuerController` (namespace `Lockdown.Agents.Pursuer`) moves, fires and arrests. Gameplay events reach AI through relays such as `PursuerStimulusRelay` and `HoundNoiseBus`.
- **`Interfaces`** holds shared contracts (`IDamageable`, `IInteractable`). Player and world systems communicate through contracts and events, not direct AI references.
- **`Editor`** contains builders: menu items under `Tools/Lockdown/...` that generate or configure prefabs and place them in the museum, for example `Build Skeleton Archer`, `Connect Pursuer AI in Museum` and `Configure Museum Doors`. Prefabs and scene setup come from these builders, so change the builder and re-run it rather than only hand-editing the generated objects.
- **`Tests`** has one assembly per feature (Hound, Pursuer, SkeletonArcher, Ambusher, Doors, Loot, Weapons, Player, Hammer, ...).
- Namespaces follow `Lockdown.<Area>`, e.g. `Lockdown.AI.Core.Hound`, `Lockdown.Enemies`, `Lockdown.World`, `Lockdown.Editor`.

Hound requirement: it must track via sound events (position, kind and age, with decaying memory). Never give it continuous knowledge of the player's position.

## Conventions

- Preserve Unity `.meta` files, script GUIDs and existing enum values; **append** new enum states to keep serialized data compatible. Renamed folders kept their `.meta` files for this reason.
- Commit related code and docs together. Keep unrelated package or ProjectSettings changes in separate commits.
- Branch from `develop` using `feature/`, `bugfix/`, `refactor/` or `chore/`. PRs into `develop` need one approval, and `main` must stay buildable for the demo/submission.
- Coordinate edits to shared scenes, prefabs and navigation. Rebuild and test navigation after changing museum geometry. The museum layout follows `Docs/WorldBuilder/README.md` (v3.2, fifteen spaces). Some placement docs it references are missing from the checkout, so verify placement against the actual scene.
- Record measured results in `Docs/AIPerformanceLog.md` (agents) or `Docs/OptimisationLog.md` (world).
- Per-feature specs live in `Docs/` (`AI/*.md`, `SkeletonArcher.md`, `PlayerDoors.md`, `HammerAndGlass.md`, ...), and `Docs/forViva/` holds viva explanations. Update the relevant spec when behaviour changes.
