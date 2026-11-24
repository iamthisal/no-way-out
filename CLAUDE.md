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

## Hound animation pipeline (implemented)

The Hound's movement is keyframed animation driven by NavMesh speed. The earlier procedural leg-swinging code was removed.

- **Source of truth: `Blender/Hound/MuseumHound.blend`** (Blender 5.2). `Hound_Rig` (22 bones, XYZ Euler; on every leg bone **+X rotation swings the leg forward**) holds three hand-keyed actions, each with Fake User enabled:
  - `Hound_Idle`: frames 0–72 (2.4 s loop; chest, head, ears and tail only).
  - `Hound_Walk`: frames 0–32, a 4-beat lateral walk keyed every 8 frames, with Spine −2° on frames 8 and 24 so the front paws stay planted. Planted paws travel about 0.28 m per 16 frames, which matches **≈0.53 m/s**.
  - `Hound_Run`: frames 0–12, a gallop keyed every 2 frames. The body is lowered and bobbed with `Root` **Location Y** (the Root bone's Y axis is world up), −0.037 to −0.056 m. Paws travel about 0.49 m per 4 frames, which matches **≈3.7 m/s**.
- **`Blender/Hound/build_hound.py` predates these clips.** Running it regenerates the `.blend` and FBX from scratch and deletes the walk and run actions, so don't run it.
- **Export:** select `Hound_Rig` and `Hound_Mesh` in Object Mode, then File → Export → FBX **directly to `Assets/_Project/Models/Hound/MuseumHound.fbx`**, keeping its `.meta`. Settings: Selected Objects; Armature and Mesh only; scale 1 with Apply Unit Scale; Forward −Z, Up Y; no leaf bones; only deform bones; Bake Animation with **All Actions** on, NLA Strips off, Force Start/End Keying on, Simplify 0. FBX files under `Blender/` are git-ignored.
- **Import (`MuseumHound.fbx.meta`):** Generic rig with Import Animation on. Clips: `Hound_Rig|Hound_Idle` (0–72), `Hound_Rig|Hound_Walk` (0–32) and `Hound_Run` (0–12), all with Loop Time and Loop Pose. A new action exported later must be added as a clip with **+**, because the clip list is explicit. No root node is set, so Root Transform / Bake Into Pose options don't appear and root motion is unused.
- **Animator (`Animations/Hound/HoundIdle.controller`):** the default state is `Locomotion`, a 1D blend tree on the `Speed` float with thresholds idle 0, walk 0.53 and run 3.7. The state's speed multiplier is the `AnimSpeed` float. Defaults are Speed 0 and AnimSpeed 1.
- **`Runtime/Enemies/HoundLocomotion.cs`** sets `Speed` (damped 0.1 s) from `NavMeshAgent.velocity`. Above `topClipSpeed` it sets `AnimSpeed = min(speed / topClipSpeed, maxPlaybackBoost)` to reduce foot sliding, and it forces `applyRootMotion = false`. `InvestigatingHound.prefab` (the dog `GlassHoundResponse` spawns) serializes topClipSpeed 3.7 and maxPlaybackBoost 1.5. The script defaults are still 0.53 and 2.
- **Gaits vs AI speeds:** `Museum_HoundSettings` uses wanderSpeed 2.4, which blends walk and run, and trackingSpeed 4.8, which plays the run at about 1.3×. No trot clip exists.
- **Do not run `Tools/Lockdown/Build Museum Hound Model` (`HoundModelBuilder`).** It keeps only the first imported clip, renames it `HoundIdle`, and resets the controller's default state to that clip, which undoes all of the above. Update the builder first if it's needed again.
- `Docs/HoundModel.md` still describes the earlier idle-only milestone.


## Conventions

- Preserve Unity `.meta` files, script GUIDs and existing enum values; **append** new enum states to keep serialized data compatible. Renamed folders kept their `.meta` files for this reason.
- Commit related code and docs together. Keep unrelated package or ProjectSettings changes in separate commits.
- Branch from `develop` using `feature/`, `bugfix/`, `refactor/` or `chore/`. PRs into `develop` need one approval, and `main` must stay buildable for the demo/submission.
- Coordinate edits to shared scenes, prefabs and navigation. Rebuild and test navigation after changing museum geometry. The museum layout follows `Docs/WorldBuilder/README.md` (v3.2, fifteen spaces). Some placement docs it references are missing from the checkout, so verify placement against the actual scene.
- Record measured results in `Docs/AIPerformanceLog.md` (agents) or `Docs/OptimisationLog.md` (world).
- Per-feature specs live in `Docs/` (`AI/*.md`, `SkeletonArcher.md`, `PlayerDoors.md`, `HammerAndGlass.md`, ...), and `Docs/forViva/` holds viva explanations. Update the relevant spec when behaviour changes.
