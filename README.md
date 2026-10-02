# Warehouse Lockdown

Warehouse Lockdown is a Unity 6 project scaffold for a tense, industrial-styled 3D stealth-action game set inside an automated warehouse under emergency lockdown.

This repository currently contains the planned folder structure, documentation, and assembly-definition setup. It does not include a finished Unity game, completed scenes, models, prefabs, or imported gameplay assets. Use it as the base structure for building the game in Unity.

Joint project for **SE3032 Graphics & Visualisation** and **SE3062 Intelligent Systems**.

---

## Team

| Student | GV Role | IS Agent | AI Architecture |
|---|---|---|---|
| S1 | World Builder | **Sentinel** - Armoured ground patrol unit | Hierarchical FSM + A* pathfinding with suspicion decay |
| S2 | Systems Engineer | **Wasp** - Aerial reconnaissance drone | Behaviour Tree + 3D Euclidean A* with obstacle clearance buffers |
| S3 | Core Developer | **Stalker** - Stealth ambush predator | Utility AI with shadow-cost path weighting and stealth angle heuristic |
| S4 | Agent Controller | **Rex** - Ally companion drone | Utility scoring + cover evaluation + distraction orchestration |

---

## Scenes

| Scene | Purpose | Owner |
|---|---|---|
| `Bootstrap.unity` | Game lifecycle, persistent managers, input | S2 - Systems Engineer |
| `Env.unity` | Warehouse geometry, baked lighting, audio zones | S1 - World Builder |
| `Interactables.unity` | Terminals, doors, crates, conveyor switches | S3 - Core Developer |
| `Agents.unity` | AI spawn anchors, patrol paths, perception grids | S4 - Agent Controller |
| `ModelShowcase.unity` | Asset preview and lighting test sandbox | S1 - World Builder |

> **Scene rule:** Do not edit `Env.unity`, `Interactables.unity`, or `Agents.unity` without pinging the scene owner first. See [CONTRIBUTING.md](CONTRIBUTING.md).
>
> These scene files are planned targets. Create them under `Assets/_Project/Scenes/` when the Unity project is opened.

---

## Branching Strategy

Two long-lived branches, everything else is short-lived:

```text
main                 always builds; demo / submission branch
develop              team integration; merges here before main
feature/<what>       e.g. feature/rex-patrol-fsm
bugfix/<what>        e.g. bugfix/sentinel-detection-cone
refactor/<what>      e.g. refactor/blackboard-events
chore/<what>         e.g. chore/update-lfs-tracking
```

- Branch off `develop`, not `main`.
- All PRs into `develop` require **1 approving review** before merge.
- Merge `develop` into `main` only when the project builds cleanly.

---

## Assembly Dependency Graph

```text
Lockdown.AI.Agents
  -> Lockdown.AI.Core
  -> Lockdown.Interfaces
  -> Lockdown.Runtime
       -> Lockdown.Interfaces

Lockdown.Tests.EditMode
  -> Lockdown.AI.Core
```

---

## Getting Started

This repo targets **Unity 6.6.3f1** (`6000.6.3f1`), matching the installed Unity version shown in the project screenshot.

```bash
# Clone with Git LFS
git clone <repo-url>
git lfs pull

# Switch to integration branch
git checkout develop
```

Open the repository folder in Unity Hub using Unity `6000.6.3f1`. Unity will generate local folders such as `Library/`, `Temp/`, `Logs/`, and `UserSettings/`; these are intentionally ignored by Git.

Recommended first Unity steps:

1. Open this folder as the project root.
2. Let Unity import packages and generate `.meta` files.
3. Create the planned scenes in `Assets/_Project/Scenes/`.
4. Commit source assets, scripts, scenes, prefabs, settings, and `.meta` files.
5. Do not commit `Library/`, `Temp/`, build output, or user-specific files.

Read [CONTRIBUTING.md](CONTRIBUTING.md) before touching any shared scene.

---

## Documentation

| Document | Contents |
|---|---|
| [`Docs/DesignDoc.md`](Docs/DesignDoc.md) | Architecture decisions, module boundaries, dependency rules |
| [`Docs/ArtBible.md`](Docs/ArtBible.md) | Colour palette, prop style, lighting mood reference |
| [`Docs/AI/Rex.md`](Docs/AI/Rex.md) | Rex - ally agent specification |
| [`Docs/AI/Sentinel.md`](Docs/AI/Sentinel.md) | Sentinel - heavy security agent specification |
| [`Docs/AI/Wasp.md`](Docs/AI/Wasp.md) | Wasp - aerial drone agent specification |
| [`Docs/AI/Stalker.md`](Docs/AI/Stalker.md) | Stalker - stealth hunter agent specification |
| [`Docs/AIPerformanceLog.md`](Docs/AIPerformanceLog.md) | Benchmarking log: latency, GC alloc, draw calls |
| [`Docs/OptimisationLog.md`](Docs/OptimisationLog.md) | Optimisation entries with before/after metrics |
