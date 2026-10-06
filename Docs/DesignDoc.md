# System Architecture & Technical Design Document

> **Current environment specification: museum v3.1, 15 spaces.** [WorldBuilder/README.md](WorldBuilder/README.md) contains the complete coordinate tables; [Extension_Build_Steps.md](WorldBuilder/Extension_Build_Steps.md) preserves the existing skeleton while adding the five-gallery loop. Use `Museum_Main.unity` under `Assets/_Project/Scenes/Museum`. D13–D18 are 2.8 m wide and 2.5 m high. W01/W03 become connections; W02 becomes locked courtyard door D19; W09 becomes closed exit door D20. Student 1 supplies static geometry and initial NavMesh, Student 2 supplies player/interactive physics, Student 3 original models, and Student 4 enemy movement/animation. Rebuild and test navigation after the extension. The legacy warehouse architecture below is not evidence of implemented systems or an instruction to replace the museum scene.

## Overview
**Warehouse Lockdown** is structured around strict separation of concerns using Unity Assembly Definitions (`.asmdef`). This architecture guarantees rapid compilation cycles, prevents circular dependencies, and establishes explicit domain boundaries between team roles.

---

## 1. Architecture Decisions

- **Multi-Scene Additive Workflow**:
  - `Bootstrap.unity`: Game lifecycle manager, global DI container, input management. Always loaded first.
  - `Env.unity`: Static world geometry, baked/realtime lighting, reflections, audio reverberation zones.
  - `Interactables.unity`: Doors, consoles, crates, conveyor switches, physics props.
  - `Agents.unity`: AI spawn anchors, waypoint paths, perception grids, blackboard state.
  - `ModelShowcase.unity`: Standalone testbed scene for world builders and modelers.
- **Data-Driven Blackboard**:
  - Global blackboard for security alert levels, lockdown stage, and active alarm hubs.
  - Local agent blackboards for tactical memory, target vectors, and cover evaluation.
- **Interface Segregation**:
  - Gameplay systems communicate across domain boundaries via pure interfaces defined in `Lockdown.Interfaces`.

---

## 2. Module Boundaries & Assembly Definitions

The project code is divided into five dedicated assemblies:

```
Lockdown.AI.Agents
   ├── references: Lockdown.AI.Core
   ├── references: Lockdown.Interfaces
   └── references: Lockdown.Runtime

Lockdown.Runtime
   └── references: Lockdown.Interfaces

Lockdown.AI.Core
   └── references: (none)

Lockdown.Interfaces
   └── references: (none)

Lockdown.Tests.EditMode
   └── references: Lockdown.AI.Core
```

### Module Responsibilities

1. **`Lockdown.Interfaces`**:
   - Contains pure C# contracts (e.g., `IAgent`, `IInteractable`, `IDamageable`, `IBlackboard`, `IPerceptionTarget`).
   - Zero project dependencies; serves as the central decoupling bridge.

2. **`Lockdown.AI.Core`**:
   - Foundational AI algorithms: state machines (FSM), blackboard data structures, A* / utility heuristics, spatial search algorithms, and perception math.
   - Zero project dependencies; independent of specific gameplay runtime implementations to allow pure headless testing.

3. **`Lockdown.Runtime`**:
   - Concrete runtime implementations for character movement, animation bridging, world managers, camera controllers, and debug utilities.
   - Depends solely on `Lockdown.Interfaces`.

4. **`Lockdown.AI.Agents`**:
   - Concrete agent logic and state trees for `Rex`, `Sentinel`, `Wasp`, and `Stalker`.
   - Depends on `Lockdown.AI.Core` (for algorithmic primitives), `Lockdown.Interfaces` (for interaction contracts), and `Lockdown.Runtime` (for physics and movement execution).

5. **`Lockdown.Tests.EditMode`**:
   - Unit tests running in the Unity Test Framework validating core data structures, heuristics, and state machine transitions.

---

## 3. Dependency Rules & Enforcement

- **Strict Upward Flow**: High-level modules may depend on low-level abstractions, never the reverse.
- **No Circular References**: Unity `.asmdef` files enforce acyclic graph dependencies at compile time.
- **Zero Engine Couplings in Core Math**: Heuristic and search algorithms in `Lockdown.AI.Core` should prioritize pure C# logic and mathematics where feasible, enabling rapid headless testing.
- **Cross-Domain Communication**:
  - The Player controller (`Lockdown.Runtime` or `Player/`) must never directly reference concrete enemy AI classes (`Lockdown.AI.Agents`). Interaction must occur via `IDamageable`, `IPerceptionTarget`, or events registered on shared interfaces.
