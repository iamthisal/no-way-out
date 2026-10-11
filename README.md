# No Way Out

Joint project for **SE3032 Graphics & Visualisation (GV)** and **SE3062 Intelligent Systems (IS)**. Unity game work and AI work live together in this repository.

The current Unity environment is an abandoned museum. `game_html/` is a playable warehouse prototype and the reference for the selected enemy behaviors; its map is not the museum floor plan.

## Current Agent Scope

The IS scope consists of **three ground enemy agents and one dynamic spawning agent**. Student-to-agent ownership should be confirmed by the team; the old Rex/Wasp allocation is superseded.

| Agent | Role | Existing HTML reference |
|---|---|---|
| Sentinel (Pursuer) | Patrol, pursuit, coordinated ranged attacks and search | `src/enemy-ai.js`: `Pursuer` |
| Stalker (Ambusher) | Cover selection, peek-and-fire attacks and retreat | `src/enemy-ai.js`: `Ambusher` |
| Hound | Sound memory, target scoring, tracking and close-range attack | `src/hound-ai.js`: `Hound` |
| Dynamic Spawning Agent | Encounter population, wave timing and progression | `src/room-controller.js`, `room-config.js`, and `spawnWave` in `main.js` |

These references are under `game_html/`. Rex (ally drone) and Wasp (aerial drone) are excluded from the current project scope. The HTML prototype still contains both, including Wasp wave entries and Rex controls. They are prototype-only, not part of the selected four agents.

The HTML spawner creates configured waves in response to room entry and enemy deaths. Adaptive difficulty or utility-based spawn selection is not implemented there. See [AI scope and implementation status](Docs/AI/README.md) before claiming an algorithm or Unity integration as complete.

## Unity Environment

Current scene: `Assets/_Project/Scenes/Museum/Museum_Main.unity`.

Use [the World Builder manual](Docs/WorldBuilder/README.md) and [floor plan](Docs/WorldBuilder/Museum_FloorPlan.png). Museum shell v3.2 has fifteen spaces; five northern galleries connect back to the Exhibition Hall. Revision 3.2 includes thirteen wall-end closure adjustments. Door D19 closes access to the courtyard and D20 is the exit.

Furniture correction P4 (9 October 2026) supersedes earlier placement proposals. It preserves the P3 toilet cubicles and clarifies the W08 glazing junction. Earlier documentation references `Extension_Build_Steps.md` and `Room_Object_Placement.md`, but those files are absent from this checkout. Verify placement against the team's current plans and the actual scene; documentation alone does not establish implementation or navigation results.

GV responsibilities cover environment construction, player/interaction systems, original models, and enemy movement/animation. IS responsibilities cover agent decisions and spawning. Agree room, door, collision and navigation contracts when integrating them.

## Getting Started

```bash
git clone https://github.com/iamthisal/no-way-out.git
git lfs pull
git checkout develop
```

Open the repository in Unity using the version in `ProjectSettings/ProjectVersion.txt`. Read [CONTRIBUTING.md](CONTRIBUTING.md) before editing shared scenes.

For the behavior reference:

```bash
cd game_html
npm install
npm run dev
```

Open http://127.0.0.1:4173/. See [the prototype README](game_html/README.md) for current controls and checks.

## Branching

`main` is the buildable demo/submission branch; `develop` is the integration branch. Branch from `develop` using `feature/<what>`, `bugfix/<what>`, `refactor/<what>`, or `chore/<what>`, for example `feature/hound-sound-tracking` or `feature/dynamic-spawning-agent`.

PRs into `develop` require one approving review. Merge `develop` into `main` when the relevant checks and Unity build pass.

## Documentation

| Document | Contents |
|---|---|
| [Design document](Docs/DesignDoc.md) | Architecture, module boundaries and integration status |
| [AI scope](Docs/AI/README.md) | Selected agents and prototype references |
| [Sentinel](Docs/AI/Sentinel.md) | Pursuer behavior reference |
| [Stalker](Docs/AI/Stalker.md) | Ambusher behavior reference |
| [Hound](Docs/AI/Hound.md) | Sound-tracking behavior reference |
| [Dynamic Spawning Agent](Docs/AI/DynamicSpawner.md) | Encounter lifecycle and planned spawning work |
| [Art Bible](Docs/ArtBible.md) | Museum visual direction |
| [AI performance log](Docs/AIPerformanceLog.md) | Agent evaluation and measurements |
| [Optimisation log](Docs/OptimisationLog.md) | World optimization checks and measurements |
