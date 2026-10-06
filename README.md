# No Way Out

> **Current World Builder plan: abandoned museum v3.1 (15 spaces).** Use [the build manual](Docs/WorldBuilder/README.md), [extension-only steps](Docs/WorldBuilder/Extension_Build_Steps.md) and [floor plan](Docs/WorldBuilder/Museum_FloorPlan.png). Five northern themed galleries form a loop back to the Exhibition Hall. Existing room footprints stay; W01/W03 become wider connecting doorways W02 becomes locked courtyard door D19; W09 becomes exit door D20. Current scene: `Assets/_Project/Scenes/Museum/Museum_Main.unity`. This is an approved documentation plan; this update changes documents only; confirm current scene progress separately. The warehouse concept and architecture below remain legacy proposals, not current museum construction instructions.

> A 3D stealth-action game set inside a rogue automated warehouse. Escape before the blast doors seal — if the AI lets you.

A tense, industrial-styled 3D stealth-action game set inside a vast automated warehouse that has entered emergency lockdown. Security systems have gone rogue, autonomous patrol units are hunting intruders, and the only way out is through. Navigate narrow aisles, use your ally drone Rex to scout and distract, disable security override terminals, and escape before the blast doors seal permanently.

Joint project for **SE3032 Graphics & Visualisation** and **SE3062 Intelligent Systems**.

---

## Team

| Student | GV Role | IS Agent | AI Architecture |
|---|---|---|---|
| S1 | World Builder | **Sentinel** — Armoured ground patrol unit | Hierarchical FSM + A\* pathfinding with suspicion decay |
| S2 | Systems Engineer | **Wasp** — Aerial reconnaissance drone | Behaviour Tree + 3D Euclidean A\* with obstacle clearance buffers |
| S3 | Core Developer | **Stalker** — Stealth ambush predator | Utility AI with shadow-cost path weighting and stealth angle heuristic |
| S4 | Agent Controller | **Rex** — Ally companion drone | Utility scoring + cover evaluation + distraction orchestration |

---

## Scenes

| Scene | Purpose | Owner |
|---|---|---|
| `Bootstrap.unity` | Game lifecycle, persistent managers, input | S2 — Systems Engineer |
| `Env.unity` | Warehouse geometry, baked lighting, audio zones | S1 — World Builder |
| `Interactables.unity` | Terminals, doors, crates, conveyor switches | S3 — Core Developer |
| `Agents.unity` | AI spawn anchors, patrol paths, perception grids | S4 — Agent Controller |
| `ModelShowcase.unity` | Asset preview and lighting test sandbox | S1 — World Builder |

> **Scene rule:** Do not edit `Env.unity`, `Interactables.unity`, or `Agents.unity` without pinging the scene owner first. See [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Branching Strategy

Two long-lived branches, everything else is short-lived:

```
main          ← always builds; demo / submission branch
└── develop   ← team integration; merges here before main
    ├── feature/<what>    e.g. feature/rex-patrol-fsm
    ├── bugfix/<what>     e.g. bugfix/sentinel-detection-cone
    ├── refactor/<what>   e.g. refactor/blackboard-events
    └── chore/<what>      e.g. chore/update-lfs-tracking
```

- Branch off `develop`, not `main`.
- All PRs into `develop` require **1 approving review** before merge.
- `develop` → `main` only when the project builds cleanly.

---

## Assembly Dependency Graph

```
Lockdown.AI.Agents
   ├── Lockdown.AI.Core
   ├── Lockdown.Interfaces
   └── Lockdown.Runtime
          └── Lockdown.Interfaces

Lockdown.Tests.EditMode
   └── Lockdown.AI.Core
```

---

## Getting Started

```bash
# Clone with Git LFS
git clone https://github.com/iamthisal/no-way-out.git
git lfs pull

# Switch to integration branch
git checkout develop
```

Open the project in Unity (URP). The project targets Unity 6 or later. Read [CONTRIBUTING.md](CONTRIBUTING.md) before touching any shared scene.

---

## Documentation

| Document | Contents |
|---|---|
| [`Docs/DesignDoc.md`](Docs/DesignDoc.md) | Architecture decisions, module boundaries, dependency rules |
| [`Docs/ArtBible.md`](Docs/ArtBible.md) | Colour palette, prop style, lighting mood reference |
| [`Docs/AI/Rex.md`](Docs/AI/Rex.md) | Rex — ally agent specification |
| [`Docs/AI/Sentinel.md`](Docs/AI/Sentinel.md) | Sentinel — heavy security agent specification |
| [`Docs/AI/Wasp.md`](Docs/AI/Wasp.md) | Wasp — aerial drone agent specification |
| [`Docs/AI/Stalker.md`](Docs/AI/Stalker.md) | Stalker — stealth hunter agent specification |
| [`Docs/AIPerformanceLog.md`](Docs/AIPerformanceLog.md) | Benchmarking log: latency, GC alloc, draw calls |
| [`Docs/OptimisationLog.md`](Docs/OptimisationLog.md) | Optimisation entries with before/after metrics |
