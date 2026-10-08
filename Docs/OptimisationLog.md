# Museum world optimization log

> **Room furniture P3 — 8 October 2026:** [Image-based object placement guide](WorldBuilder/Room_Object_Placement.md) now defines the furniture/exhibit coordinates for all fifteen spaces. It supersedes the earlier cover/decor proposals; shell v3.2 stays unchanged. Placement is planned, not applied to Unity.

Current target: [museum v3.2](WorldBuilder/README.md), fifteen spaces and 916 m² nominal floor area. The five-room extension adds 280 m²; it does not establish acceptable performance. No runtime measurements have been recorded by this documentation update.

## Planned checks — Student 1

- Reuse gallery materials and prefabs; track texture resolutions and platform compression. Avoid unnecessarily large maps for small props.
- Use simple box colliders for walls and fixed displays. Avoid Rigidbodies on static shell geometry and colliders on tiny visual decoration.
- Record triangle counts and draw calls using actual in-engine tools; use simpler meshes/LODs where measurement justifies them.
- Limit shadow-casting realtime lights; test fixed baked lighting with moving characters using appropriate probes.
- Rebake navigation after geometry/cover changes. Include the new northern extent Z=30 when using manually bounded collection; exclude ceilings and the inaccessible open-air gap.
- Test the full original and new loops in a build, including busy enemy encounters. Record resolution, quality settings, hardware and repeatable test route.

## Actual results

Fill this table only after testing. Keep comparable before/after conditions and explain any visual tradeoff.

| Date / commit | Scene and test conditions | Change | Before | After | Decision / evidence |
|---|---|---|---|---|---|
| Not tested | Museum_Main / v3.2 extension pending | No measurement yet | — | — | Complete extension and establish baseline |

Coordinate validation in WorldBuilder/Plan_Validation.json is a mathematical planning check, not an FPS, physics or Unity NavMesh result.

> P3 toilet update (9 October 2026): follow Room_Object_Placement.md for the two cubicles and fixed-open damaged door panels. The old open-divider proposal is superseded. Shell v3.2 is unchanged.


> Furniture correction P4 (9 October 2026): use the current Room_Object_Placement.md/CSV. Egypt and Natural History graphics now avoid doors; the Security monitor strip clears its window; Egypt gains a west-wall photo. P3 cubicles remain, with the W08 glazing junction clarified in the guide. Shell unchanged.
