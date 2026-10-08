# Abandoned Museum — Student 1 World Builder manual

**Plan v3.2 · 6 October 2026 · 15 named spaces · Current stage: extend the existing greybox**

**Approved five-gallery loop extension.** Keep the ten-space v2 skeleton. Add five northern galleries and replace the Exhibition north-wall and Exit north-wall assemblies to turn W01/W03 into D13/D18. W02 becomes the locked glass door D19; W09 becomes the closed exit door D20. Start with [Extension_Build_Steps.md](Extension_Build_Steps.md); the full appendices below also support a fresh build. The Unity scene has not been changed by this documentation update.

This is a complete reference for rebuilding the proposed World Builder environment. Read Phase 1 now; use later phases when we reach them together. Coordinates are an original, dimensioned proposal based on your supplied blueprint's connections, not measurements recovered from its pixels. Geometry is specified exactly; lighting, movement and performance settings are starting points that require testing in the team's actual Unity project.

The user has already built the original greybox skeleton in Unity. This documentation update does not build the extension or perform runtime tests. A checked coordinate plan is not proof of successful player movement, a connected baked NavMesh, or good frame rate.

## Sources, scope and decisions

- Assignment source: `D:/Year 3 Sem 1/GV/Assignment/Assignmnet_GV.pdf`, pages 1–3. Source code and a 3-minute demo video are required. The stated deadline is 21 October 2026; confirm any subsequent lecturer announcements separately.
- Original blueprint: `D:/Year 3 Sem 1/GV/Assignment/Abandoned Museum Skeleton Blueprint.png`. Red means doors; blue means windows. The current approved v3.1 construction drawing and tables supersede the original sketch and the earlier straight-row extension preview.
- Exception: the blue opening labelled Main Entrance is treated as a glazed entrance door, not an ordinary window. Its collision remains closed for the initial indoor prototype. The player starts inside.
- The image's `(0, -50)` is not a calibrated scale. This plan deliberately replaces it with a compact metre-based coordinate system. Do not mix the two systems.
- The Emergency Exit Vestibule is an indoor destination. An escape trigger or victory condition belongs to the gameplay team. D20 is the closed north exit door; arrival in this room can represent reaching safety if the team chooses. No outdoor environment, basement, stairs or second floor is required.
- Existing root README and `Docs/DesignDoc.md` describe an older warehouse concept and some different ownership rules. This document records the museum proposal and your requested roles. Reconcile the shared documents with teammates before integration; do not silently treat old warehouse features as museum requirements.
- Local files confirm Unity **6000.6.3f1**, URP **17.6.0**, and AI Navigation **2.0.14**. Keep these versions consistent across the team; no upgrade is part of this extension.

## Ownership and marking

| Work | Owner | Your boundary |
|---|---|---|
| Room layout, walls, floors, fixed cover, materials, lighting, initial NavMesh | Student 1: you | Build, document and test the environment |
| Player controller, opening doors, movable barricades, throwing and physics scripts | Student 2 | Supply clear openings, collision and size requirements; test together |
| At least two original Blender/Maya models, topology and UVs | Student 3 | Request models and integrate their deliverables; downloaded props do not satisfy their modelling requirement |
| Enemy movement, rotation and animation following calculated paths | Student 4 | Supply walkable space and navigation evidence; do not replace their assessed path logic |
| Shooting, objectives, health, win/lose logic | Team allocation | These are not added to your World Builder scope |

The rubric assigns Visual Cohesion 15, Build Stability 15, Role-Specific Quality 20, Git & Cleanliness 10, Workflow & Tools 20, and Design & Optimization Justification 20 marks. For your role, it explicitly includes layout, NavMesh baking, lighting and texturing. Evidence and an understandable workflow matter alongside appearance; this guide does not guarantee a grade.

## First-use vocabulary

- **Scene:** a saved collection of objects, such as the museum environment.
- **GameObject:** a named object in a scene. It can be a wall, light, camera, or an empty organiser.
- **Component:** a capability attached to a GameObject, such as a light or collider.
- **Transform:** the Position, Rotation and Scale of an object. Position places it, Rotation turns it, Scale resizes it.
- **Hierarchy:** the scene's object list. A child inherits its parent's Transform. All organisers in this plan stay at Position `(0,0,0)`, Rotation `(0,0,0)`, Scale `(1,1,1)`.
- **Mesh:** the triangles forming a visible 3D shape. A **polygon/triangle count** measures geometric complexity.
- **Greybox:** the complete playable layout made from plain shapes before detailed art.
- **Collider:** an invisible physics boundary that prevents passing through a surface. It does not create a visible object. A Cube starts with a Box Collider.
- **Rigidbody:** a physics component for objects that move through simulation. Fixed building pieces do not need one.
- **Material:** instructions for how a surface looks, including colour, roughness/smoothness and optional images.
- **Texture:** an image used by a material, such as marble colour or surface bumps.
- **UVs:** coordinates mapping a texture onto a mesh. Lightmap UVs are a separate layout used to store baked lighting.
- **Prefab:** a reusable saved object template. Editing its source can update repeated instances.
- **Static:** an editor flag describing particular ways an object will remain fixed. It does not add collision. Use relevant flags deliberately; moving doors and characters must not be marked static for baking.
- **NavMesh:** baked data describing where an agent of a specified size can walk. It does not by itself create or animate enemies.
- **Baking:** calculating data ahead of play. NavMesh baking calculates navigation space; light baking calculates lighting. They are separate operations.
- **Baked lighting/lightmap:** lighting stored in textures for fixed surfaces, reducing lighting work during play. Moving characters need an appropriate light-probe system and/or real-time lighting.
- **Light probe:** a sample of lighting used to illuminate moving objects. A reflection probe captures surrounding reflections for materials.
- **Git commit:** a named snapshot of your changes; take small, honest snapshots after working milestones.

## Phase 1 — Approved fifteen-space loop plan

**Owner: Student 1. Plan v3.2, approved 6 October 2026.** The current drawing is `Museum_FloorPlan.png`. The previous ten-room Unity skeleton is retained. Read `Extension_Build_Steps.md` before extending it; do not rebuild the whole museum.

### Layout and circulation

The existing Lobby, Central Hall, Artifact Exhibition, Art Gallery, Temporary Exhibition, Security, Toilet, Storage, L-shaped Staff Corridor and Exit Vestibule retain their bounds. The corridor remains one named space with two slabs.

Add five rooms, each 7 × 8 m between wall centres:

| Room | X bounds | Z bounds | Placement and purpose |
|---|---|---|---|
| Ancient Egypt | -10.5 to -3.5 | 14 to 22 | Southwest arm of the new wing; enters from Exhibition through D13 |
| Stone Age | -10.5 to -3.5 | 22 to 30 | Northwest corner; connects Egypt to Greece/Rome |
| Greece / Rome | -3.5 to 3.5 | 22 to 30 | Northern middle room; connects the two sides |
| Asian Heritage | 3.5 to 10.5 | 22 to 30 | Northeast corner; connects Greece/Rome to Natural History |
| Natural History | 3.5 to 10.5 | 14 to 22 | Southeast arm; returns to Exhibition through D18 |

**New loop:** Exhibition → Egypt → Stone Age → Greece/Rome → Asian Heritage → Natural History → Exhibition. Doors D13–D18 are all **2.8 m wide × 2.5 m high**. The player can travel either direction without forced backtracking. No one-way gameplay gate is specified.

**Existing public loop:** Central → Exhibition → Temporary → Art → Central. **Existing staff loop:** Lobby → Security → Staff Corridor → Storage → Central → Lobby. Toilet remains a side room; Exit Vestibule remains an indoor endpoint, not an outdoor escape route.

Replace north-wall windows **W01 and W03** and their surrounding wall sections with D13 and D18. D19 replaces W02 at X=-1 to 1, Z=14; D20 replaces W09 at X=14.5 to 16.5, Z=12. The open-air gap X=-3.5 to 3.5, Z=14 to 22 has visual-only ground but no playable floor, roof or NavMesh; D19 stays locked. It is not a sixteenth room. Keep the D19 glass door collidable. Any later outdoor scenery is optional visual detail.

Total nominal floor area: **916 m² = 636 existing + 280 new**, approximately 44% larger. There are **15 named spaces, 16 slabs, 18 interior openings, 5 fixed windows and three closed boundary doors (D00, D19, D20)**. Wall-centre envelope: X=-16 to 17, Z=-11 to 30 (33 × 41 m); this envelope includes outdoor gaps. Clear room dimensions are approximately 6.8 × 7.8 m in the new galleries.

### Construction and scope

Floor top Y=0; floor thickness 0.2 m; walls 0.2 m thick and 3.5 m tall; ceiling underside Y=3.5. X points east, Z north, Y up; one Unity unit is one metre. Parent Transforms remain identity. Existing D01–D06 stay 2.4 m wide, D07–D12 and entrance D00 stay 2 m wide. All openings are 2.5 m high.

Use one shared public museum material palette. Distinguish themes through signage and a few exhibits, reusing cases, plinths and frames. Do not add five unique gameplay systems. One optional fixed pedestal and one provisional point light per new room are listed in the appendices. Keep primary paths at least 1.5 m clear, targeting 2 m near busy cover; test with real player/enemy bodies. These are gameplay targets, not building-code claims.

Student 1 creates the shell, static cover, surfaces and lighting, then rebakes navigation. Student 2 owns working doors and player physics; Student 3 supplies original models; Student 4 tests enemy paths. Ceilings may stay inactive for editing but must be enabled for final movement/lighting tests.

**Evidence:** capture the approved plan, then actual Unity top and eye-level views after construction. A diagram is not evidence of a tested scene.

**Suggested documentation commit:** `docs(world): plan five-gallery museum loop extension`. Stage the reviewed documentation files explicitly; tools and AI_HANDOFF.md remain optional/local according to your existing preference. No automatic commit or push.

---

## Phase 2 — Project and folders (reference for our next stage)

**Owner: Student 1 with team agreement on editor and project settings.**

1. Ask the team for the exact editor version, renderer, target platform and existing project location. In Unity, **Help > About Unity** shows the version. If a valid team project already exists, use it; do not create a competing project.
2. If starting fresh, in Unity Hub choose **Projects > New project**, choose the agreed Unity 6 editor, and select **Universal 3D** (the URP template; template wording can vary). Name it `NoWayOut`. Create it in a new empty sibling location, not on top of this non-empty repository. Then close Unity and copy its `Assets`, `Packages` and `ProjectSettings` into the repository, merging the existing folder scaffold without overwriting team files. Reopen the repository via **Hub > Add > Add project from disk**. Do not copy `Library`, `Temp`, `Logs` or `UserSettings`.
3. Review the existing `.gitignore`; do not replace it wholesale. Exclude Library, Temp, Obj, Logs, UserSettings and build/IDE outputs. Track Assets with their .meta files, Packages and ProjectSettings. Review settings changes before staging.
4. Open **Edit > Project Settings > Editor**; verify **Asset Serialization: Force Text**. If **Version Control > Mode** is exposed there or in its own settings page, use **Visible Meta Files**. `.meta` files hold stable asset identities; losing them can break references. Move/rename imported assets inside Unity's Project window.
5. In the **Project** panel use the existing `Assets/_Project` structure. Right-click the appropriate folder > **Create > Folder** to add `Materials/World`, `Textures/World`, `Models/Environment`, `Lighting/Museum`, `Navigation`, and `Prefabs/Environment/Museum`. Retain `Scenes`. Third-party packages stay under `Assets/_ThirdParty` where practical; do not move packages that rely on fixed paths.
6. Open the existing `Assets/_Project/Scenes/Museum/Museum_Main.unity` scene. Save it before extending. For a fresh rebuild only, create a basic URP scene and save to that path; never overwrite an existing scene without a backup. This is the current museum scene, not the legacy Env.unity placeholder.
7. In the **Hierarchy** right-click > **Create Empty**, name it `World_Museum`; use the Transform component's menu > **Reset**. Repeat for the children listed below. Keep every organiser at identity Transform.

```text
World_Museum
  Floors
  Walls
  Windows
  Ceilings
  FixedCover
  Decoration
  Lighting
  Markers
```

8. In **Edit > Project Settings > Tags and Layers**, create a free user layer named `WorldSolid`. Apply it to floors, walls, blocking window panes and fixed cover; do not rename layers already used by teammates. Layers let navigation include collision geometry without including cameras, markers or characters.
9. Create `Docs/WorldBuilder/Evidence` and keep a log there with date, change, screenshot, test result and commit hash. Track these manually as work occurs; do not label planned checks as passed.

**Check:** scene saves and reopens; Console has no errors; everyone uses the same editor. **Screenshot:** Project folders and Hierarchy. **Commit:** `chore(world): organize museum scene and environment folders`.

## Phase 3 — Greybox the complete museum

**Owner: Student 1. Build all rooms before art.** Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build. Existing v2 builders should use Extension_Build_Steps.md instead of duplicating existing objects. The following instructions also support a fresh build.

### Exactly how to create one table entry

1. Make sure the Play button at the top is OFF. Changes made during Play usually disappear when Play stops.
2. In Hierarchy select `Floors`. Right-click > **3D Object > Cube**. Rename it `Floor_Central`.
3. In Inspector find **Transform**. Enter Position X `0`, Y `-0.1`, Z `0`; Rotation X/Y/Z `0`; Scale X `10`, Y `0.2`, Z `10`.
4. Keep its **Box Collider**, with **Is Trigger** unchecked. Set its layer to `WorldSolid`. Do not add a Rigidbody.
5. Select it and press **F** while the pointer is over Scene view to focus it. The top surface is now Y=0 because the cube is centred 0.1 m below zero and is 0.2 m thick.
6. Repeat this process for each floor row in Appendix A. Then create wall rows under `Walls`, and window blocking panes under `Windows`. Names, positions and scales are literal Inspector entries. All table geometry uses Rotation `(0,0,0)`.
7. You can use **Ctrl+D** to duplicate a selected cube, but immediately change its name and enter every Position and Scale value from the next row. Copying an object does not move it automatically.
8. Create ceiling rows under `Ceilings`; deactivate that parent using the checkbox beside its name at the top of Inspector while working on the greybox. Their layer stays Default and they are excluded from the initial navigation bake. Enable later for final movement and lighting checks.
9. Save with **Ctrl+S**. Use the Scene orientation gizmo's Y/top face for a top-down view; click the central gizmo cube if needed to switch between perspective and orthographic projection.

**Important:** door rows in Appendix A are markers/openings, not solid cubes. The wall tables already split around them. Do not create one long full wall behind the split pieces. Window rows include sill and header geometry plus a pane that blocks escape. Create every shared wall once, not once per room.

To colour the greybox: right-click `Materials/World` > **Create > Material**, name `M_Greybox`, choose shader **Universal Render Pipeline/Lit**, set Base Map colour to a mid-grey, and drag it onto the cubes. Use one shared material. No detailed textures yet.

**Check:** all slabs meet at openings, no duplicate walls seal doors, no gaps let the player fall outside, no double floor surfaces flicker. Wall corner overlaps are intentional, small and coplanar-safe. **Screenshot:** top view with ceilings off and one eye-level view. **Commit:** `feat(world): create abandoned museum greybox`.

## Phase 4 — Test scale and movement

**Owner: Student 1 tests; Student 2 supplies the actual controller.**

1. Under `Markers`, create a **3D Object > Capsule**, name `REF_PlayerScale`, Position `(0,1,-9.5)`, Rotation `(0,0,0)`, Scale `(0.6,0.9,0.6)`. Unity's default capsule is 2 units high and 1 unit wide: this gives a 1.8 m high, 0.6 m wide visual reference. Remove its Collider using the component menu > **Remove Component**; it is a reference, not a controller.
2. Create an empty `Spawn_Player` at `(0,0,-9.5)` with Rotation `(0,0,0)` (facing +Z). This is a **floor/feet marker**. Student 2 must offset their controller according to its own pivot; do not assume all controller prefab roots are at the feet.
3. For a static eye-level preview, select a temporary camera, Position `(0,1.65,-9.5)`, Rotation `(0,0,0)`. This camera cannot walk. Keep only one active main camera and Audio Listener in the integrated scene.
4. Ask Student 2 to place their tested controller at this marker. Deactivate `REF_PlayerScale` before playing. Your starting assumptions are standing height 1.8 m, radius 0.3 m and eye height 1.65 m; replace these with the actual agreed controller dimensions if different.
5. Press Play. Walk the full Phase 1 route in both directions. Try every door from its centre and both edges. Test against windows, wall corners and floor seams. Enable ceilings and repeat. Stop Play before editing.
6. Record clipping, snagging, missing collision and camera clearance. Fix the environment; let Student 2 fix controller physics. Do not write a second competing player controller.

**Gate:** no claim of playable scale until an actual controller test passes. **Evidence:** short walk-through recording and issue log. **Commit if geometry changes:** `fix(world): refine museum scale and movement clearance`.

## Phase 5 — Combat space and fixed cover

**Owner: Student 1 places fixed geometry; team checks combat.**

Create the Appendix B cubes under `FixedCover` using the Phase 3 recipe, including Box Colliders and `WorldSolid`. These are fixed display bases, benches, crates, reception/security desks and restroom fixture placeholders. The fixture cubes establish scale; replace them with suitable simple art in Phase 8. The open-ended toilet privacy divider is fixed cover, not an additional room or a scripted cubicle door. Low cover is roughly 1–1.2 m high; whether it protects a standing/crouching player depends on the actual shooting and posture systems. Do not claim crouch gameplay until it exists.

Leave the centre lane in the hall free. Exhibition islands allow left/right movement around displays. Gallery benches keep painting walls visible. Storage shelves stay at the north edge and southwest corner, leaving the new south service door clear. Temporary exhibition pedestals stay out of its two door approaches. Keep both security door approaches and the restroom entrance free. Keep maintenance empty of combat cover: its job is navigation and pacing.

Test with Student 2's controller and Student 4's enemy-sized reference. Measure actual free space between collider faces; a visual gap can still be too narrow. Avoid blind enemies directly beside doorways and do not decide final enemy counts before testing. Doors remain open holes at this stage; no moving barricades are part of your fixed-cover pass.

**Evidence:** overhead annotated cover view. **Commit:** `feat(world): add museum combat cover layout`.

## Phase 6 — Initial NavMesh

**Owner: Student 1. NavMesh is a geometry deliverable, not a replacement for assessed IS algorithms.**

1. Open **Window > Package Manager** (some versions place this under **Window > Package Management**). Select **Unity Registry**, search **AI Navigation**, install the version compatible with the agreed editor. Record its resolved version; do not install a preview just because it is newer.
2. Select `World_Museum`. In Inspector choose **Add Component**, search `NavMesh Surface`, and add it. Collect **Current Object Hierarchy** (called Children in some package versions), **Use Geometry: Physics Colliders**, and include only the `WorldSolid` layer. Default Area is Walkable. Leave automatic link generation off; this layout needs no jumps or stairs.
3. Open the package's **Navigation** window, commonly **Window > AI > Navigation**, and its **Agents** settings. Create or select a team-approved agent type `MuseumHumanoid`. Starting values: Radius `0.35`, Height `1.8`, Step Height `0.2`, Max Slope `45`. Select this same type on the Surface. These values describe the expected navigation body; confirm them with Student 4. They do not automatically resize an enemy model or collider.
4. On fixed cover, add a **NavMesh Modifier**, enable **Override Area**, select **Not Walkable**. If all `FixedCover` children are solid obstacles, put this modifier on the parent and apply to children. Mark windows Not Walkable too if the bake produces walkable sill islands. Keep roofs/ceilings outside `WorldSolid`.
5. Ensure floor and wall colliders are enabled, door holes are empty, reference capsules and players are excluded by layer, and no moving door is baked as permanent architecture. Save the scene. Click **Bake** on the Surface. Start with default voxel settings; only reduce voxel size after a reproducible clearance issue and record why.
6. Inspect the blue navigation overlay: each interior door joins its rooms, fixed cover has clearance around it, and no floor outside the building appears as a playable destination. An isolated blue patch on a shelf is not acceptable just because the main floor is blue.
7. Save scene and generated NavMesh data. Unity stores a referenced bake asset; locate it using the Surface's NavMesh Data field. Commit that asset and its `.meta` wherever the installed package saves it. Do not casually relocate generated data through Explorer.

The 2 m service openings leave a theoretical 1.3 m agent-centre width after a 0.35 m radius allowance on both sides; voxelization can shrink this. Only the actual bake/path test confirms connectivity. Documentation: [Unity AI Navigation Surface reference](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/NavMeshSurface.html).

**Evidence:** overhead NavMesh overlay and Surface settings. **Commit:** `feat(world): configure museum navigation mesh`.

## Phase 7 — Enemy navigation test

**Owner: Student 1 validates space with Student 4; Student 4 implements motion/path following.**

Supply the exact floor-level route markers from Appendix A to Student 4. They now cover all fifteen spaces, both corridor legs and the gallery loop. Empty markers have no renderer or collider; they are not enemy spawn scripts.

Request tests across every interior opening D01–D18 in both directions. Traverse both the original gallery loop and the new D13–D18 loop clockwise and counterclockwise, the lobby/security/corridor/storage/hall loop, both storage service doors and the endpoint route. With one gallery connection temporarily unavailable, verify that the remaining route works under the team’s actual path/door-state system. Check complete paths, doorway clearance, obstacle avoidance, facing and animation with the real enemy body. Two agents meeting in a doorway can still congest even if one agent passes; test this together. A NavMesh Agent alone will not start moving without code supplying a destination. Student 4 may provide a temporary NavMeshAgent diagnostic, but it must not replace the required custom IS path/animation work.

Record route, agent dimensions/type, result and any fix. Re-bake after geometry changes. **Evidence:** short navigation clip and route log. **Commit when environment fixes are made:** `fix(world): resolve museum navigation clearance`.

## Phase 8 — Import environment assets

**Owner: Student 1 selects/integrates environment art; Student 3 authors the two custom models.**

Start with a small kit: one display case, one pedestal/artifact set, one painting frame, one bench, one shelving unit and one crate. Reuse them across all public rooms. Add one desk reused at reception/security, one static monitor, one simple sink and one toilet model for the support rooms. These can be suitable licensed props; Student 3 still authors at least two distinct original models. No custom CCTV or plumbing system is part of this art pass. Prefer assets with a shared realistic or lightly stylised style and URP materials. Keep a source/license log; record which objects are Student 3 originals separately.

For an FBX or texture file, use **Assets > Import New Asset**, select the file and import; then organise inside the Project window. Store your own models under `Models/Environment`, image maps under `Textures/World`, and vendor content in `_ThirdParty` as practical. Select a model and inspect its preview, scale and materials before replacing any geometry. Pink surfaces usually indicate a shader/pipeline issue; fix that before importing more.

Place a model inside an empty wrapper at an Appendix B position; resize the model child to fit the documented bounding box. Imported model pivots vary: align its base to Y=0 rather than blindly copying a centre-height position to an arbitrary model root. Keep one simple collider on the wrapper or a suitable child and remove overlapping placeholder/asset colliders. Do not leave the opaque grey cube inside a glass case. Retain the greybox shell unless an art replacement is explicitly matched to its dimensions.

**Evidence:** before/after from the same viewpoint plus source log. **Commit:** `feat(world): add museum environment assets`.

## Phase 9 — Reusable prefabs

**Owner: Student 1 for environment objects.**

Select the finished display-case wrapper in Hierarchy and drag it into `Prefabs/Environment/Museum`. Name it `PF_DisplayCase`. Repeat for `PF_Bench`, `PF_Painting`, `PF_Shelf` and `PF_CrateFixed`. Drag a prefab from Project into Hierarchy to create another instance. Enter the planned position on each instance.

Double-click a prefab in Project to edit its source, then save and return to the scene using the back arrow. An instance-specific change is an override; review Overrides before applying changes to all instances. Keep wrapper scale `(1,1,1)` when possible; scale the model child for asset conversion. Prefabs reduce inconsistent manual edits, but do not automatically reduce draw calls. Movable/interactive crates and doors belong to Student 2's prefabs, not `PF_CrateFixed`.

**Evidence:** one prefab and repeated instances. **Commit:** `refactor(world): create reusable museum environment prefabs`.

## Phase 10 — Materials and textures

**Owner: Student 1.**

In `Materials/World`, right-click > **Create > Material** for each name below. Use **Universal Render Pipeline/Lit**. Click Base Map's colour swatch to enter a hex value. These values describe plain starting colours before texture maps, under neutral test lighting.

| Name | Base colour | Metallic | Smoothness | Use |
|---|---|---:|---:|---|
| M_Plaster | #B8B0A0 | 0 | 0.15 | Public room walls |
| M_Marble | #AAA79E | 0 | 0.35 | Lobby, central and artifact exhibition floors |
| M_Wood | #544034 | 0 | 0.25 | Art and temporary gallery floors, benches and dark timber |
| M_Concrete | #777873 | 0 | 0.1 | Storage, maintenance, exit floor/walls |
| M_Metal | #555A59 | 0.8 | 0.25 | Shelving and service fixtures |
| M_Brass | #89704C | 0.75 | 0.35 | Restrained public-area trim |
| M_Tile | #B6BCAF | 0 | 0.2 | Toilet floor and walls; restrained ceramic/tile |
| M_Office | #888478 | 0 | 0.15 | Security floor; plaster walls and dark desk |
| M_Ceiling | #B0ABA1 | 0 | 0.1 | Ceilings |
| M_WindowGreybox | #597A8A | 0 | 0.25 | Opaque blue-grey blocking pane initially |

1. Drag materials onto objects, or into **Mesh Renderer > Materials**. Shared walls may remain neutral plaster in the first pass. A Unity Cube uses one material for all faces; do not expect separate room finishes on opposite faces without a different mesh or thin facing panels later.
2. Select an imported colour texture in Project. Start with Max Size `1024` for ordinary props, `2048` only for a visibly large/important surface; use platform-appropriate compression and click **Apply**. Keep mipmaps enabled for ordinary 3D world textures. These are starting budgets, not assessed requirements.
3. Drag the colour texture into a material's **Base Map** slot. For a normal/bump map select its asset, set Texture Type **Normal map**, Apply, then place it in the material's Normal Map slot. Use supplied maps correctly rather than dropping any image into any slot.
4. For a simple Cube floor top using a texture representing a 2 m × 2 m patch, start Tiling at floor width/2 and depth/2. Thus the central slab uses `(5,5)` and exhibition `(7,4.5)`. Use separate material variants when room tiling differs; changing one shared material changes all its users. Inspect a checker/grid because imported UVs and cube face orientations can differ. Wall cubes with one tiled material cannot keep every face's texel size identical; keep narrow edge faces plain or use a properly UV-mapped modular wall later.
5. Inspect at player distance: surfaces must not look stretched, unusually shiny or from incompatible art styles. Avoid unique 4K maps for small repeated props.
6. Actual glass is optional polish. Duplicate the window material, use Transparent surface type and modest alpha, then check sorting and reflections. Its physics collider remains enabled even when it looks transparent. Window glass never serves as a doorway.

**Evidence:** material Inspector and consistent room views. **Commit:** `feat(world): add museum materials and textures`.

## Phase 11 — Museum lighting

**Owner: Student 1.**

Enable all ceilings first. Turn off any template Directional Light for the initial interior lighting pass. Use Appendix C positions: select `Lighting`, right-click > **Light > Point Light**, rename it, then enter position, colour, range and intensity. Point lights need no rotation. Keep scale `(1,1,1)`. These values assume the standard URP Light Inspector with ordinary intensity values; do not copy them blindly into an inspector using lumens or another physical unit.

Begin with these as Realtime lights for placement, Shadows **None**, and no extreme exposure/post-processing. Use warm public lighting and a slightly cool service area, not pitch-black navigation. Once placements work, Phase 12 changes fixed lights to Baked. Reserve real-time shadow casting for a small number of meaningful lights such as a player light supplied by Student 2. A luminous material does not automatically give useful baked illumination unless emission/GI is configured.

Check the actual Game view with the player camera: enemies, door edges and cover must remain legible. A bright editor Scene view with its lighting toggle disabled is not evidence of the final result. Window light beams and atmospheric fog are optional and outside the minimum build.

**Evidence:** same camera before/after and light settings. **Commit:** `feat(world): implement museum lighting`.

## Phase 12 — Bake appropriate lighting

**Owner: Student 1; verify moving characters with Students 2 and 4.**

1. Save `Museum_Main.unity`. Enable ceilings and final fixed geometry. On fixed floor/wall/ceiling/cover renderers enable **Contribute Global Illumination** through the Static flags or Renderer settings, and **Receive Global Illumination: Lightmaps** where available. Do not mark players, enemies, opening doors or movable crates as lighting-static.
2. For imported static meshes needing it: select FBX > **Model > Generate Lightmap UVs > Apply**. This does not fix all bad topology or overlapping custom UV layouts; inspect warnings and coordinate with Student 3.
3. Select the fixed point lights, set **Mode: Baked**. Open **Window > Rendering > Lighting**. Create/assign a Lighting Settings asset if the scene has none. Enable Baked Global Illumination. Choose a supported Progressive lightmapper; CPU is a practical fallback when the GPU backend is unavailable. Start at Lightmap Resolution `10` texels/unit and Max Lightmap Size `1024` for iteration. Raise resolution only where the visible result warrants it; settings vary by editor version.
4. Use the probe system configured by the team's URP version. If using classic Light Probe Groups, create **GameObject > Light > Light Probe Group**, enter Edit Light Probes, and duplicate/move probes around walkable room centres, door approaches and lanes at roughly Y=0.5 and Y=1.8. Keep probes in open space and on both sides of doors, not inside walls or cover. Use the room/door coordinates in Appendix A as horizontal anchors. This is a coverage starting pattern, not an exact final probe solution. If the URP project already uses Adaptive Probe Volumes, retain and configure that system with the team instead of assuming the classic group applies.
5. Click **Generate Lighting**. Wait for completion. Inspect dark patches, seams, leaks, stretched shadows and moving character brightness. Baked lights do not automatically give moving characters real-time cast shadows. Add only the limited real-time/mixed lighting needed and test the project's supported shadow mode.
6. Add baked Reflection Probes only if glass/metal visibly needs them: centre them within relevant rooms and size their boxes to those room bounds. Bake them after the geometry and lighting are stable.
7. Save the scene and include referenced Lighting Settings, Lighting Data, lightmaps/probe data and `.meta` files in the commit. Re-bake after changing fixed geometry/material GI/light placement; a previous bake is no longer evidence of the new layout.

Reference: [Unity lighting bake workflow](https://docs.unity.com/en-us/engine/6000.5/manual/lighting-overview/direct-and-indirect-lighting/lightmapping/baking-before-runtime/bake). Menu details should be matched to your installed version.

**Evidence:** Lighting settings, finished bake and a moving-character view. **Commit:** `feat(world): bake museum lighting and probe coverage`.

## Phase 13 — Abandoned details

**Owner: Student 1 for fixed decoration.**

Use Appendix D as a deliberately small decoration pass. A few tilted paintings, crates, displaced furniture and damaged display replacements communicate abandonment without filling every lane. Give public areas related materials; age storage with concrete and metal. Start with one consistent damage style.

Create decorative paintings from thin cubes/prefabs; no Collider is needed for small wall-hung visual details that do not affect movement. Broken glass can be a visual material/decal or a few non-colliding pieces, not dozens of simulated shards. Crates large enough to obstruct movement need simple colliders and must enter the navigation bake. Keep intentional interactive objects separate for Student 2.

After each cluster, walk its room and repeat relevant enemy routes. Re-bake navigation if collision geometry changed, and lighting when static bake contributors changed. **Evidence:** matched before/after views. **Commit:** `feat(world): add abandoned museum environment details`.

## Phase 14 — Optimization with evidence

**Owner: Student 1 environment; coordinate whole-game targets.**

Agree a target machine, resolution and frame-rate goal; for example 60 FPS at 1080p is a proposed target, not a promise or rubric requirement. Use the same scene, camera route, enemies, build and settings for before/after measurements.

| Concern | Simple first action | Record |
|---|---|---|
| Polygon cost | Reuse modest-detail props; remove hidden geometry from imported art where appropriate; use LODs only for assets that justify them | Triangle count from a fixed view before/after |
| Texture memory | Reduce oversized maps, enable suitable compression/mipmaps, reuse material families | Import settings, texture memory and close-up quality |
| Physics | Box/compound primitive colliders for fixed props; no Rigidbody on architecture; no collision on tiny decoration | Collider choice/count and physics timing |
| Lighting | Bake fixed lights, limit real-time shadows, avoid overlapping oversized light ranges | Lighting settings and frame timing |
| Rendering | Share materials, verify URP SRP Batcher compatibility; consider static batching only for truly fixed objects and measure memory tradeoff | Batches/SetPass/frame timings before/after |
| Culling | Try occlusion culling only if profiling shows rooms behind walls are expensive enough to justify it | A reproducible gain; omit if negligible |

Open **Window > Analysis > Profiler** for timing and use Game view **Stats** for a quick rendering snapshot. A frame spike or an average alone can mislead: repeat the same short route and record frame-time range and obvious stutters. Measure a standalone build where possible; editor overhead is different. Change one main factor at a time. Do not describe an unmeasured optimization as a proven improvement.

**Evidence:** entries in `Docs/OptimisationLog.md`, hardware, resolution, route and before/after captures. **Commit:** `perf(world): optimize museum environment`.

## Phase 15 — Integrate team work

**Owner: Student 1's environment plus each teammate's own component.**

1. Agree the museum concept, scene ownership and version changes in the shared project docs. The older repository assigns some interactables to S3; this assignment/request places interaction physics with S2 and original modelling with S3. Make this explicit together.
2. Supply `Museum_Main.unity`, its referenced prefabs/materials/textures, NavMesh and lighting data, markers and this guide. Every additive scene must share the same origin and metre scale. Do not move `World_Museum` to make a teammate's offset scene appear aligned.
3. Student 2 places doors and the player; Student 4 places agents; Student 3 supplies custom models. If the existing multi-scene workflow is retained, ensure Bootstrap actually loads the required scenes. Opening Env alone does not prove integrated gameplay works.
4. Match door-leaf size/pivot to each opening; allow space for swing. Keep moving doors out of the static navigation bake. The team must decide how closed doors block navigation (for example coordinated obstacle carving/state logic) and how agents wait/open/replan. Do not call a closed physical door traversable just because the static mesh connects through it.
5. Re-test nav after model/collider changes, light bake after fixed art changes, and camera/listener count after scene loading. Keep a single enabled environment NavMesh surface/data set for this initial layout.
6. Review your diff before each commit and use the team's branch/PR rules. The repository describes branches from `develop` with review before merging. A suitable work branch name is `codex/museum-world-plan` if one is needed; do not recreate or switch branches blindly if there is ongoing work.

**Evidence:** integrated walkthrough, named contributions, model credits and issue resolution. **Commit:** `feat(world): integrate museum environment with team systems`.

## Phase 16 — Final testing and polish

**Owner: Student 1 environment checks; build stability belongs to the group.**

Open **File > Build Profiles** in Unity 6 (older editors use Build Settings). Use the agreed target platform and verify the entry/Bootstrap scene is included. Add environment scenes if loaded at runtime by the team's approach. Build to an ignored `Builds` location, run the executable and test the complete game; editor-only play is insufficient.

- [ ] Start point is safe and the camera does not spawn inside a floor/prop.
- [ ] All fifteen spaces and D01–D18 work in both directions with ceilings active; both corridor legs join without a hidden dividing wall.
- [ ] Both loops work in the integrated game; door-state/path logic handles unavailable connections.
- [ ] Windows and boundaries stop unintended escape; no falling through floor seams.
- [ ] Real player, enemy and interactive door collisions work together.
- [ ] Navigation handles all planned rooms; no paths through closed doors or fixed cover.
- [ ] No missing/pink materials, missing scripts, duplicate geometry/cameras or build errors.
- [ ] Final lighting is baked where intended, visible in the build, and moving characters remain readable.
- [ ] Performance is recorded on the agreed machine, not claimed from guesswork.
- [ ] Team originals, imported asset sources and your own contributions are identifiable.
- [ ] Source includes required `.meta`, package/settings, referenced baked data and documentation.
- [ ] Team records a 3-minute demo; your part shows layout, visual cohesion, navigation and lighting clearly.

**Evidence:** final build clip, console/build results and completed checklist. **Commit:** `fix(world): polish and validate museum environment`.

## Git routine at each milestone

Save the scene and assets. Review changes in your chosen Git client; stage only your intentional work and its necessary dependencies. Write the relevant milestone message above, commit, and push your work branch according to team policy. Record the resulting hash beside the evidence. Do not make empty commits merely to satisfy a count, commit `Library`, or include unrelated teammate changes.

The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. The commands below exclude tools and AI_HANDOFF.md, following your earlier preference. Review the root README and DesignDoc carefully because they already had local changes before this update. For this documentation-only milestone, a terminal alternative from the repository root is:

```sh
git diff -- Docs/WorldBuilder
git status --short
git add Docs/WorldBuilder/README.md Docs/WorldBuilder/Extension_Build_Steps.md Docs/WorldBuilder/Museum_FloorPlan.png Docs/WorldBuilder/Museum_Extension_Preview.png Docs/WorldBuilder/Museum_Geometry.csv Docs/WorldBuilder/Plan_Validation.json
git add README.md Docs/DesignDoc.md Docs/ArtBible.md Docs/OptimisationLog.md
git diff --cached --stat
git commit -m "docs(world): plan five-gallery museum loop extension"
```

`git diff` does not show untracked file contents; open those files directly before staging. Check that the staged list contains only the intended documentation. Pushing depends on your existing branch and remote arrangement; don't push to main by assumption.

## Viva prompts: explain decisions in your own words

| Prompt | What to demonstrate |
|---|---|
| Why a central hall? | It gives an identifiable hub; the public gallery loop reduces forced backtracking while security and storage connect a separate staff route |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why fifteen spaces? | Five themed galleries extend the existing ten-space museum as a continuous loop; shared assets limit the extra workload; the open-air gap is not a room |
| Why greybox first? | You test scale, openings and cover before spending time on art that might need moving |
| Why this coordinate convention? | Ground at Y=0 and identity parents make dimensions predictable and allow teammates to align scenes |
| Why broad door openings? | Bodies need collider and navigation clearance; visual width alone is insufficient |
| What is your NavMesh contribution? | Geometry selection, agent-size agreement, bake, inspection and navigation test evidence; distinguish this from custom path algorithms |
| Why baked lighting? | Fixed museum geometry can use precomputed light; moving characters need probes/appropriate dynamic lighting |
| How did you keep a consistent style? | A shared palette, material family, similar model detail and controlled abandonment |
| What did you optimize? | Show a real before/after measurement and explain the quality/performance tradeoff |
| What did teammates do? | Identify controller/interaction scripts, original models and enemy path/animation work honestly |

## Build appendices

The following coordinate tables and the accompanying PNG are generated from the same geometry specification. `Museum_Geometry.csv` provides the same physical cube rows for filtering or manual checking; it is not a Unity importer. The original blueprint has been revised into the approved fifteen-space v3.1 loop layout on a metre grid. All geometry values below are intentional design values, not values mandated by the assignment.


## Revision 3.1 — Convert W02 and W09 into doors

These instructions apply to the already-built v3 shell, not the v2 rebuild procedure. Work only under the active Museum root; keep Museum_Test disabled. Stop Play mode and save before editing. Rotation is (0,0,0) for every listed object. Position is the cube centre; Scale is its full size. Keep existing Walls/Windows parents at identity transforms. Retain enabled, non-trigger Box Colliders on walls and closed doors, WorldSolid layer, no Rigidbody.

### A. Locked courtyard glass door D19 (formerly W02)

1. Under Museum > Walls, delete Wall_42_W02_Sill. This removes the low wall beneath the old window.
2. Rename Wall_42_Before_W02 to Wall_42_Before_D19; its Transform stays unchanged.
3. Rename Wall_42_W02_Header to Wall_42_D19_Header and enter the values below.
4. Under Museum > Windows, rename Wall_42_W02_Pane to Courtyard_LockedGlassDoor and enter the values below. Keep its Box Collider so the player cannot pass through.
5. Leave Wall_42_End unchanged. The Walls/Windows organiser names are retained to minimize hierarchy changes; the new panel is a door, not a window.

| Object | Parent | Position X,Y,Z | Scale X,Y,Z |
|---|---|---|---|
| Wall_42_Before_D19 | Walls | (-2.25, 1.75, 14) | (2.5, 3.5, 0.2) |
| Wall_42_D19_Header | Walls | (0, 3, 14) | (2, 1, 0.2) |
| Wall_42_End | Walls | (2.25, 1.75, 14) | (2.5, 3.5, 0.2) |
| Courtyard_LockedGlassDoor | Windows | (0, 1.25, 14) | (2, 2.5, 0.1) |

In Assets/_Project/Materials/World, create a Material named M_CourtyardGlass. Choose Universal Render Pipeline/Lit, Surface Type Transparent, and a pale grey Base Map colour with alpha about 0.2 (about 51 on a 0–255 alpha slider). Start with Metallic 0 and Smoothness 0.7. Apply it to Courtyard_LockedGlassDoor. Material transparency controls visibility; it does not disable the Box Collider. This is a simple glass-door placeholder; decorative frame/handle art can be added later without narrowing the opening.

### B. Closed exit door D20 (formerly W09)

Delete Wall_18_W09_Sill under Walls. Rename and resize the other four objects using the table. The opening widens from 1.6 m to 2 m. Wall_59 is the documented replacement name for this boundary; it is not an additional overlapping wall.

| Existing object | New name | Position X,Y,Z | Scale X,Y,Z |
|---|---|---|---|
| Wall_18_Before_W09 | Wall_59_Before_D20 | (14.2, 1.75, 12) | (0.6, 3.5, 0.2) |
| Wall_18_W09_Header | Wall_59_D20_Header | (15.5, 3, 12) | (2, 1, 0.2) |
| Wall_18_End | Wall_59_End | (16.8, 1.75, 12) | (0.6, 3.5, 0.2) |
| Wall_18_W09_Pane | Exit_ClosedDoorPlaceholder | (15.5, 1.25, 12) | (2, 2.5, 0.1) |

Keep the first three under Walls and the panel under Windows. Apply an opaque grey/metal material to the exit panel. It remains closed and collidable for now. Student 2 owns future interaction; the team defines level completion. Do not remove its collision or let the player walk out until an exterior landing or a transition is implemented and tested. No outdoor gameplay area is specified here.

### C. Visual courtyard ground and navigation

Under Museum > Decoration, create a Cube named Courtyard_VisualGround: Position (0,-0.1,18), Rotation (0,0,0), Scale (7,0.2,8). Remove its Box Collider using the component menu > Remove Component. Set Layer to Default, use an opaque concrete material, and do not add a Rigidbody. It covers the gap visually, including a small intentional overlap beneath surrounding walls. It is scenery, not a seventeenth playable floor slab, and is recorded in Appendix D rather than the physical-collision CSV.

For the planned NavMesh Surface, collect WorldSolid using Physics Colliders; exclude Default scenery. If the team uses render-mesh collection instead, explicitly exclude this ground or add an Ignore From Build NavMesh Modifier. Verify no blue NavMesh appears in the courtyard. Do not add a roof or Off-Mesh Link into it. Keep D19 locked permanently for this version.

Optional empty door markers: D19 at (0,0,14), D20 at (15.5,0,12), both Rotation (0,0,0), Scale (1,1,1), no renderer/collider. These do not open the doors.

Save with Ctrl+S. Take one eye-level screenshot of each door. With the actual controller, confirm the glass is see-through but blocks movement, the exit blocks movement, and all D01–D18 interior passages still work. Re-bake navigation/lighting after the changes when those stages are reached. Suggested scene commit after verification: feat(world): add exit and locked courtyard doors. Student 1 owns this geometry/material work; no interaction code is required here.


## Appendix A — Exact v3.2 shell geometry

**Use these v3.2 values for the completed extension. Existing v2 builders must follow Extension_Build_Steps.md to preserve their work.** Positions are object centres. Cube Scale is full size, not half size. All physical rows use Rotation `(0,0,0)`, enabled non-trigger Box Colliders and no Rigidbody. All organiser parents must be Position `(0,0,0)`, Rotation `(0,0,0)`, Scale `(1,1,1)` so the listed local positions equal world positions. Ceilings use Default layer and are initially inactive through their parent; all other construction cubes use WorldSolid.

### Room/slab bounds

Each range is a wall-centre coordinate range. The two Maintenance rows form one L-shaped space. No other room has multiple slabs.

| Floor ID | Named space | X range | Z range | Width × depth |
| --- | --- | --- | --- | --- |
| Lobby | Entrance Lobby / Reception | -5 to 5 | -11 to -5 | 10 × 6 |
| Central | Central Hall | -5 to 5 | -5 to 5 | 10 × 10 |
| Exhibition | Artifact Exhibition Hall | -7 to 7 | 5 to 14 | 14 × 9 |
| ArtGallery | Painting / Art Gallery | -16 to -5 | -3 to 5 | 11 × 8 |
| Temporary | Temporary Exhibition | -16 to -7 | 5 to 11 | 9 × 6 |
| Security | Security Office | 5 to 11 | -11 to -6 | 6 × 5 |
| Toilet | Public Toilet | -10 to -5 | -11 to -6 | 5 × 5 |
| Storage | Storage / Archive | 5 to 14 | -3 to 5 | 9 × 8 |
| MaintenanceSouth | Maintenance / Staff Corridor | 5 to 14 | -6 to -3 | 9 × 3 |
| MaintenanceEast | Maintenance / Staff Corridor | 14 to 17 | -6 to 8 | 3 × 14 |
| Exit | Emergency Exit Vestibule | 14 to 17 | 8 to 12 | 3 × 4 |
| Egypt | Ancient Egypt | -10.5 to -3.5 | 14 to 22 | 7 × 8 |
| StoneAge | Stone Age | -10.5 to -3.5 | 22 to 30 | 7 × 8 |
| GreeceRome | Greece / Rome | -3.5 to 3.5 | 22 to 30 | 7 × 8 |
| AsianHeritage | Asian Heritage | 3.5 to 10.5 | 22 to 30 | 7 × 8 |
| NaturalHistory | Natural History | 3.5 to 10.5 | 14 to 22 | 7 × 8 |

### Floors and ceilings

Floors: top Y=0, bottom Y=-0.2. Ceilings: underside Y=3.5, top Y=3.7. Floor slabs meet without overlapping; do not add a big rectangular plane under exterior gaps. The two corridor floors meet at X=14, Z=-6 to -3: do not insert a wall there.

| Name | Parent | Position X,Y,Z | Scale X,Y,Z |
| --- | --- | --- | --- |
| Floor_Lobby | Floors | (0, -0.1, -8) | (10, 0.2, 6) |
| Ceiling_Lobby | Ceilings | (0, 3.6, -8) | (10, 0.2, 6) |
| Floor_Central | Floors | (0, -0.1, 0) | (10, 0.2, 10) |
| Ceiling_Central | Ceilings | (0, 3.6, 0) | (10, 0.2, 10) |
| Floor_Exhibition | Floors | (0, -0.1, 9.5) | (14, 0.2, 9) |
| Ceiling_Exhibition | Ceilings | (0, 3.6, 9.5) | (14, 0.2, 9) |
| Floor_ArtGallery | Floors | (-10.5, -0.1, 1) | (11, 0.2, 8) |
| Ceiling_ArtGallery | Ceilings | (-10.5, 3.6, 1) | (11, 0.2, 8) |
| Floor_Temporary | Floors | (-11.5, -0.1, 8) | (9, 0.2, 6) |
| Ceiling_Temporary | Ceilings | (-11.5, 3.6, 8) | (9, 0.2, 6) |
| Floor_Security | Floors | (8, -0.1, -8.5) | (6, 0.2, 5) |
| Ceiling_Security | Ceilings | (8, 3.6, -8.5) | (6, 0.2, 5) |
| Floor_Toilet | Floors | (-7.5, -0.1, -8.5) | (5, 0.2, 5) |
| Ceiling_Toilet | Ceilings | (-7.5, 3.6, -8.5) | (5, 0.2, 5) |
| Floor_Storage | Floors | (9.5, -0.1, 1) | (9, 0.2, 8) |
| Ceiling_Storage | Ceilings | (9.5, 3.6, 1) | (9, 0.2, 8) |
| Floor_MaintenanceSouth | Floors | (9.5, -0.1, -4.5) | (9, 0.2, 3) |
| Ceiling_MaintenanceSouth | Ceilings | (9.5, 3.6, -4.5) | (9, 0.2, 3) |
| Floor_MaintenanceEast | Floors | (15.5, -0.1, 1) | (3, 0.2, 14) |
| Ceiling_MaintenanceEast | Ceilings | (15.5, 3.6, 1) | (3, 0.2, 14) |
| Floor_Exit | Floors | (15.5, -0.1, 10) | (3, 0.2, 4) |
| Ceiling_Exit | Ceilings | (15.5, 3.6, 10) | (3, 0.2, 4) |
| Floor_Egypt | Floors | (-7, -0.1, 18) | (7, 0.2, 8) |
| Ceiling_Egypt | Ceilings | (-7, 3.6, 18) | (7, 0.2, 8) |
| Floor_StoneAge | Floors | (-7, -0.1, 26) | (7, 0.2, 8) |
| Ceiling_StoneAge | Ceilings | (-7, 3.6, 26) | (7, 0.2, 8) |
| Floor_GreeceRome | Floors | (0, -0.1, 26) | (7, 0.2, 8) |
| Ceiling_GreeceRome | Ceilings | (0, 3.6, 26) | (7, 0.2, 8) |
| Floor_AsianHeritage | Floors | (7, -0.1, 26) | (7, 0.2, 8) |
| Ceiling_AsianHeritage | Ceilings | (7, 3.6, 26) | (7, 0.2, 8) |
| Floor_NaturalHistory | Floors | (7, -0.1, 18) | (7, 0.2, 8) |
| Ceiling_NaturalHistory | Ceilings | (7, 3.6, 18) | (7, 0.2, 8) |

### Wall pieces, headers and sills

Create each row exactly once under Walls. Numbered names group pieces on a single boundary interval. A header/lintel is over an opening; a sill is under a window. Shared room boundaries are represented once. Small perpendicular end overlaps close wall corners; do not add an extra wall per room. A blank doorway is intentional.

| Name | Position X,Y,Z | Scale X,Y,Z |
| --- | --- | --- |
| Wall_01_Before_W08 | (-9.3, 1.75, -11) | (1.6, 3.5, 0.2) |
| Wall_01_W08_Sill | (-7.75, 0.9, -11) | (1.5, 1.8, 0.2) |
| Wall_01_W08_Header | (-7.75, 3.1, -11) | (1.5, 0.8, 0.2) |
| Wall_01_End | (-6, 1.75, -11) | (2, 3.5, 0.2) |
| Wall_02_Before_D00 | (-3, 1.75, -11) | (4, 3.5, 0.2) |
| Wall_02_D00_Header | (0, 3, -11) | (2, 1, 0.2) |
| Wall_02_End | (3, 1.75, -11) | (4, 3.5, 0.2) |
| Wall_03_Before_W07 | (6, 1.75, -11) | (2, 3.5, 0.2) |
| Wall_03_W07_Sill | (8, 0.6, -11) | (2, 1.2, 0.2) |
| Wall_03_W07_Header | (8, 3.1, -11) | (2, 0.8, 0.2) |
| Wall_03_End | (10.05, 1.75, -11) | (2.1, 3.5, 0.2) |
| Wall_04_Solid | (-7.55, 1.75, -6) | (5.1, 3.5, 0.2) |
| Wall_05_Before_D09 | (6, 1.75, -6) | (2, 3.5, 0.2) |
| Wall_05_D09_Header | (8, 3, -6) | (2, 1, 0.2) |
| Wall_05_End | (10, 1.75, -6) | (2, 3.5, 0.2) |
| Wall_06_Solid | (12.5, 1.75, -6) | (3, 3.5, 0.2) |
| Wall_07_Solid | (15.55, 1.75, -6) | (3.1, 3.5, 0.2) |
| Wall_08_Before_D01 | (-3.1, 1.75, -5) | (3.8, 3.5, 0.2) |
| Wall_08_D01_Header | (0, 3, -5) | (2.4, 1, 0.2) |
| Wall_08_End | (3.1, 1.75, -5) | (3.8, 3.5, 0.2) |
| Wall_09_Solid | (-10.55, 1.75, -3) | (11.1, 3.5, 0.2) |
| Wall_10_Before_D08 | (6.75, 1.75, -3) | (3.5, 3.5, 0.2) |
| Wall_10_D08_Header | (9.5, 3, -3) | (2, 1, 0.2) |
| Wall_10_End | (12.3, 1.75, -3) | (3.6, 3.5, 0.2) |
| Wall_11_Before_D04 | (-14.1, 1.75, 5) | (3.8, 3.5, 0.2) |
| Wall_11_D04_Header | (-11, 3, 5) | (2.4, 1, 0.2) |
| Wall_11_End | (-8.4, 1.75, 5) | (2.8, 3.5, 0.2) |
| Wall_12_Solid | (-6, 1.75, 5) | (2, 3.5, 0.2) |
| Wall_13_Before_D03 | (-3.1, 1.75, 5) | (3.8, 3.5, 0.2) |
| Wall_13_D03_Header | (0, 3, 5) | (2.4, 1, 0.2) |
| Wall_13_End | (3.1, 1.75, 5) | (3.8, 3.5, 0.2) |
| Wall_14_Solid | (6, 1.75, 5) | (2, 3.5, 0.2) |
| Wall_15_Solid | (10.5, 1.75, 5) | (7, 3.5, 0.2) |
| Wall_16_Before_D12 | (14.25, 1.75, 8) | (0.5, 3.5, 0.2) |
| Wall_16_D12_Header | (15.5, 3, 8) | (2, 1, 0.2) |
| Wall_16_End | (16.75, 1.75, 8) | (0.5, 3.5, 0.2) |
| Wall_17_Solid | (-11.55, 1.75, 11) | (9.1, 3.5, 0.2) |
| Wall_59_Before_D20 | (14.2, 1.75, 12) | (0.6, 3.5, 0.2) |
| Wall_59_D20_Header | (15.5, 3, 12) | (2, 1, 0.2) |
| Wall_59_End | (16.8, 1.75, 12) | (0.6, 3.5, 0.2) |
| Wall_40_Solid | (-8.8, 1.75, 14) | (3.6, 3.5, 0.2) |
| Wall_41_Before_D13 | (-6.75, 1.75, 14) | (0.5, 3.5, 0.2) |
| Wall_41_D13_Header | (-5.1, 3, 14) | (2.8, 1, 0.2) |
| Wall_41_End | (-3.6, 1.75, 14) | (0.2, 3.5, 0.2) |
| Wall_42_Before_D19 | (-2.25, 1.75, 14) | (2.5, 3.5, 0.2) |
| Wall_42_D19_Header | (0, 3, 14) | (2, 1, 0.2) |
| Wall_42_End | (2.25, 1.75, 14) | (2.5, 3.5, 0.2) |
| Wall_43_Before_D18 | (3.6, 1.75, 14) | (0.2, 3.5, 0.2) |
| Wall_43_D18_Header | (5.1, 3, 14) | (2.8, 1, 0.2) |
| Wall_43_End | (6.75, 1.75, 14) | (0.5, 3.5, 0.2) |
| Wall_44_Solid | (8.8, 1.75, 14) | (3.6, 3.5, 0.2) |
| Wall_45_Before_D14 | (-9.45, 1.75, 22) | (2.1, 3.5, 0.2) |
| Wall_45_D14_Header | (-7, 3, 22) | (2.8, 1, 0.2) |
| Wall_45_End | (-4.55, 1.75, 22) | (2.1, 3.5, 0.2) |
| Wall_46_Solid | (0, 1.75, 22) | (7, 3.5, 0.2) |
| Wall_47_Before_D17 | (4.55, 1.75, 22) | (2.1, 3.5, 0.2) |
| Wall_47_D17_Header | (7, 3, 22) | (2.8, 1, 0.2) |
| Wall_47_End | (9.45, 1.75, 22) | (2.1, 3.5, 0.2) |
| Wall_48_Solid | (-7.05, 1.75, 30) | (7.1, 3.5, 0.2) |
| Wall_49_Solid | (0, 1.75, 30) | (7, 3.5, 0.2) |
| Wall_50_Solid | (7.05, 1.75, 30) | (7.1, 3.5, 0.2) |
| Wall_20_Before_W04 | (-16, 1.75, -2.25) | (0.2, 3.5, 1.5) |
| Wall_20_W04_Sill | (-16, 0.6, -0.75) | (0.2, 1.2, 1.5) |
| Wall_20_W04_Header | (-16, 3.1, -0.75) | (0.2, 0.8, 1.5) |
| Wall_20_Before_W05 | (-16, 1.75, 1) | (0.2, 3.5, 2) |
| Wall_20_W05_Sill | (-16, 0.6, 2.75) | (0.2, 1.2, 1.5) |
| Wall_20_W05_Header | (-16, 3.1, 2.75) | (0.2, 0.8, 1.5) |
| Wall_20_End | (-16, 1.75, 4.25) | (0.2, 3.5, 1.5) |
| Wall_21_Before_W06 | (-16, 1.75, 6) | (0.2, 3.5, 2) |
| Wall_21_W06_Sill | (-16, 0.6, 8) | (0.2, 1.2, 2) |
| Wall_21_W06_Header | (-16, 3.1, 8) | (0.2, 0.8, 2) |
| Wall_21_End | (-16, 1.75, 10) | (0.2, 3.5, 2) |
| Wall_51_Solid | (-10.5, 1.75, 18) | (0.2, 3.5, 8) |
| Wall_52_Solid | (-10.5, 1.75, 26) | (0.2, 3.5, 8) |
| Wall_22_Solid | (-10, 1.75, -8.5) | (0.2, 3.5, 5) |
| Wall_23_Before_D05 | (-7, 1.75, 5.9) | (0.2, 3.5, 1.8) |
| Wall_23_D05_Header | (-7, 3, 8) | (0.2, 1, 2.4) |
| Wall_23_End | (-7, 1.75, 10.1) | (0.2, 3.5, 1.8) |
| Wall_24_Solid | (-7, 1.75, 12.5) | (0.2, 3.5, 3) |
| Wall_25_Before_D11 | (-5, 1.75, -10.25) | (0.2, 3.5, 1.5) |
| Wall_25_D11_Header | (-5, 3, -8.5) | (0.2, 1, 2) |
| Wall_25_End | (-5, 1.75, -6.75) | (0.2, 3.5, 1.5) |
| Wall_26_Solid | (-5, 1.75, -5.5) | (0.2, 3.5, 1) |
| Wall_27_Solid | (-5, 1.75, -4) | (0.2, 3.5, 2) |
| Wall_28_Before_D02 | (-5, 1.75, -2.1) | (0.2, 3.5, 1.8) |
| Wall_28_D02_Header | (-5, 3, 0) | (0.2, 1, 2.4) |
| Wall_28_End | (-5, 1.75, 3.1) | (0.2, 3.5, 3.8) |
| Wall_53_Solid | (-3.5, 1.75, 18) | (0.2, 3.5, 8) |
| Wall_54_Before_D15 | (-3.5, 1.75, 23.3) | (0.2, 3.5, 2.6) |
| Wall_54_D15_Header | (-3.5, 3, 26) | (0.2, 1, 2.8) |
| Wall_54_End | (-3.5, 1.75, 28.7) | (0.2, 3.5, 2.6) |
| Wall_55_Solid | (3.5, 1.75, 18) | (0.2, 3.5, 8) |
| Wall_56_Before_D16 | (3.5, 1.75, 23.3) | (0.2, 3.5, 2.6) |
| Wall_56_D16_Header | (3.5, 3, 26) | (0.2, 1, 2.8) |
| Wall_56_End | (3.5, 1.75, 28.7) | (0.2, 3.5, 2.6) |
| Wall_29_Before_D10 | (5, 1.75, -10.25) | (0.2, 3.5, 1.5) |
| Wall_29_D10_Header | (5, 3, -8.5) | (0.2, 1, 2) |
| Wall_29_End | (5, 1.75, -6.75) | (0.2, 3.5, 1.5) |
| Wall_30_Solid | (5, 1.75, -5.5) | (0.2, 3.5, 1) |
| Wall_31_Solid | (5, 1.75, -4) | (0.2, 3.5, 2) |
| Wall_32_Before_D06 | (5, 1.75, -2.1) | (0.2, 3.5, 1.8) |
| Wall_32_D06_Header | (5, 3, 0) | (0.2, 1, 2.4) |
| Wall_32_End | (5, 1.75, 3.1) | (0.2, 3.5, 3.8) |
| Wall_33_Solid | (7, 1.75, 9.5) | (0.2, 3.5, 9) |
| Wall_57_Solid | (10.5, 1.75, 18) | (0.2, 3.5, 8) |
| Wall_58_Solid | (10.5, 1.75, 26) | (0.2, 3.5, 8) |
| Wall_34_Solid | (11, 1.75, -8.5) | (0.2, 3.5, 5) |
| Wall_35_Before_D07 | (14, 1.75, -1.5) | (0.2, 3.5, 3) |
| Wall_35_D07_Header | (14, 3, 1) | (0.2, 1, 2) |
| Wall_35_End | (14, 1.75, 3.5) | (0.2, 3.5, 3) |
| Wall_36_Solid | (14, 1.75, 6.5) | (0.2, 3.5, 3) |
| Wall_37_Solid | (14, 1.75, 10) | (0.2, 3.5, 4) |
| Wall_38_Solid | (17, 1.75, 1) | (0.2, 3.5, 14) |
| Wall_39_Solid | (17, 1.75, 10) | (0.2, 3.5, 4) |

### Window and main-entrance collision panes

Create these cubes under Windows. Initially use opaque M_WindowGreybox; later use glass where appropriate. Keep collision. W08 is frosted, with a higher sill of 1.8 m; other windows start at 1.2 m. All window heads are 2.7 m. Entrance_ClosedGlassPlaceholder represents D00 and stays closed for the indoor prototype. D19 and D20 also have closed panels in this table. D01–D18 remain open. Windows is the organiser for these boundary panels, even when a panel represents a door.

| Name | Position X,Y,Z | Scale X,Y,Z |
| --- | --- | --- |
| Wall_01_W08_Pane | (-7.75, 2.25, -11) | (1.5, 0.9, 0.05) |
| Wall_03_W07_Pane | (8, 1.95, -11) | (2, 1.5, 0.05) |
| Wall_20_W04_Pane | (-16, 1.95, -0.75) | (0.05, 1.5, 1.5) |
| Wall_20_W05_Pane | (-16, 1.95, 2.75) | (0.05, 1.5, 1.5) |
| Wall_21_W06_Pane | (-16, 1.95, 8) | (0.05, 1.5, 2) |
| Entrance_ClosedGlassPlaceholder | (0, 1.25, -11) | (2, 2.5, 0.05) |
| Courtyard_LockedGlassDoor | (0, 1.25, 14) | (2, 2.5, 0.1) |
| Exit_ClosedDoorPlaceholder | (15.5, 1.25, 12) | (2, 2.5, 0.1) |

### Door openings and empty floor markers

Create **empty GameObjects** under Markers for these rows. Scale `(1,1,1)`; no renderer or collider. Do not create solid door cubes. D01–D06 are 2.4 m wide; D13–D18 are 2.8 m wide; D00, D07–D12, D19 and D20 are 2 m wide. All are 2.5 m tall. Marker Y=0 is floor height; working door hinge pivots are Student 2’s separate responsibility. Facing follows the connection arrow, except D00 which faces into Lobby.

| Marker | Position X,Y,Z | Rotation X,Y,Z | Width | Connection |
| --- | --- | --- | --- | --- |
| D00 | (0, 0, -11) | (0, 0, 0) | 2 | Outside → Lobby |
| D01 | (0, 0, -5) | (0, 0, 0) | 2.4 | Lobby → Central |
| D02 | (-5, 0, 0) | (0, -90, 0) | 2.4 | Central → ArtGallery |
| D03 | (0, 0, 5) | (0, 0, 0) | 2.4 | Central → Exhibition |
| D04 | (-11, 0, 5) | (0, 0, 0) | 2.4 | ArtGallery → Temporary |
| D05 | (-7, 0, 8) | (0, 90, 0) | 2.4 | Temporary → Exhibition |
| D06 | (5, 0, 0) | (0, 90, 0) | 2.4 | Central → Storage |
| D07 | (14, 0, 1) | (0, 90, 0) | 2 | Storage → MaintenanceEast |
| D08 | (9.5, 0, -3) | (0, 180, 0) | 2 | Storage → MaintenanceSouth |
| D09 | (8, 0, -6) | (0, 0, 0) | 2 | Security → MaintenanceSouth |
| D10 | (5, 0, -8.5) | (0, 90, 0) | 2 | Lobby → Security |
| D11 | (-5, 0, -8.5) | (0, -90, 0) | 2 | Lobby → Toilet |
| D12 | (15.5, 0, 8) | (0, 0, 0) | 2 | MaintenanceEast → Exit |
| D13 | (-5.1, 0, 14) | (0, 0, 0) | 2.8 | Exhibition → Egypt |
| D14 | (-7, 0, 22) | (0, 0, 0) | 2.8 | Egypt → StoneAge |
| D15 | (-3.5, 0, 26) | (0, 90, 0) | 2.8 | StoneAge → GreeceRome |
| D16 | (3.5, 0, 26) | (0, 90, 0) | 2.8 | GreeceRome → AsianHeritage |
| D17 | (7, 0, 22) | (0, 180, 0) | 2.8 | AsianHeritage → NaturalHistory |
| D18 | (5.1, 0, 14) | (0, 180, 0) | 2.8 | NaturalHistory → Exhibition |
| D19 | (0, 0, 14) | (0, 0, 0) | 2 | Exhibition → CourtyardClosed |
| D20 | (15.5, 0, 12) | (0, 0, 0) | 2 | Exit → OutsideClosed |

### Route markers

Empty GameObjects under Markers, rotation `(0,0,0)`, scale `(1,1,1)`. These are navigation test targets, not enemy spawn logic. Spawn_Player is a feet marker; offset any actual controller according to its pivot.

| Name | Position X,Y,Z |
| --- | --- |
| Spawn_Player | (0, 0, -9.5) |
| Route_Central | (0, 0, 0) |
| Route_Art | (-10, 0, 1) |
| Route_Temporary | (-11, 0, 8) |
| Route_Exhibition | (0, 0, 10) |
| Route_Storage | (9, 0, 1) |
| Route_StaffSouth | (12, 0, -4.5) |
| Route_StaffCorner | (15.5, 0, -4.5) |
| Route_StaffEast | (15.5, 0, 1) |
| Route_Exit | (15.5, 0, 10) |
| Route_Security | (8, 0, -8.5) |
| Route_Toilet | (-7, 0, -8.5) |
| Route_Egypt | (-7, 0, 18) |
| Route_StoneAge | (-7, 0, 26) |
| Route_GreeceRome | (0, 0, 26) |
| Route_AsianHeritage | (7, 0, 26) |
| Route_NaturalHistory | (7, 0, 18) |

## Appendix B — Fixed cover and fixture placeholders

Create under FixedCover, Rotation `(0,0,0)`, Box Collider enabled, WorldSolid. Apply a Not Walkable NavMesh Modifier to the parent for all children. Sizes are literal Cube scales for the greybox and target outer bounds for later imported art. Most pieces stand on Y=0; the sink is a wall-mounted proxy. Toilet partition is an open-ended privacy divider, not a fully modelled closed cubicle. Keep it noninteractive.

| Name | Position X,Y,Z | Scale X,Y,Z |
| --- | --- | --- |
| Cover_HallWest | (-2.5, 0.6, 2) | (2, 1.2, 1.2) |
| Cover_HallEast | (2.5, 0.6, -2) | (2, 1.2, 1.2) |
| Cover_ExhibitWest | (-3, 0.6, 10) | (2.4, 1.2, 2) |
| Cover_ExhibitEast | (3, 0.6, 10.5) | (2.4, 1.2, 2) |
| Bench_ArtSouth | (-10.5, 0.5, -1.6) | (3, 1, 1) |
| Bench_ArtNorthWest | (-14, 0.5, 2.5) | (2, 1, 1) |
| Pedestal_TemporaryWest | (-13.7, 0.6, 8.5) | (1.4, 1.2, 1.4) |
| Crate_TemporaryNorth | (-9.3, 0.5, 10) | (1.2, 1, 1) |
| Shelf_StorageNorth | (9.5, 1.2, 4.2) | (4, 2.4, 0.8) |
| Shelf_StorageSouthWest | (6.6, 1.2, -2.3) | (2, 2.4, 0.8) |
| Crate_Storage | (11, 0.6, 2.6) | (1.2, 1.2, 1.2) |
| Desk_Reception | (-2.8, 0.6, -6.3) | (2.4, 1.2, 0.8) |
| Desk_Security | (9.4, 0.6, -10) | (2, 1.2, 0.8) |
| Partition_Toilet | (-8, 1, -10) | (0.1, 2, 1.6) |
| Bowl_Toilet_Proxy | (-9, 0.35, -10) | (0.6, 0.7, 0.8) |
| Sink_Toilet_Proxy | (-6.5, 0.85, -6.5) | (1, 0.3, 0.5) |
| Pedestal_Egypt | (-9.4, 0.6, 20.8) | (1.2, 1.2, 1.2) |
| Pedestal_StoneAge | (-9.4, 0.6, 28.8) | (1.2, 1.2, 1.2) |
| Pedestal_GreeceRome | (-2.4, 0.6, 28.8) | (1.2, 1.2, 1.2) |
| Pedestal_AsianHeritage | (4.6, 0.6, 28.8) | (1.2, 1.2, 1.2) |
| Pedestal_NaturalHistory | (4.6, 0.6, 20.8) | (1.2, 1.2, 1.2) |

## Appendix C — Initial point lights

Create under Lighting; Rotation `(0,0,0)`, Scale `(1,1,1)`. Ranges are metres. Intensities are provisional values for the ordinary URP Light Inspector, not universal physical units. Start with Shadows None; use Realtime for placement, then Baked for the fixed-light bake. Keep ceilings active while evaluating. These positions cover all fifteen spaces and both corridor legs; tune and record actual brightness in Unity.

| Name | Position X,Y,Z | Range | Initial intensity | Colour |
| --- | --- | --- | --- | --- |
| L_LobbyWest | (-2.5, 3, -8) | 6 | 1.2 | #FFE2BA |
| L_LobbyEast | (2.5, 3, -8) | 6 | 1.2 | #FFE2BA |
| L_HallSouth | (0, 3, -2.5) | 7 | 1.4 | #FFE2BA |
| L_HallNorth | (0, 3, 2.5) | 7 | 1.4 | #FFE2BA |
| L_ExhibitWest | (-3.5, 3, 9.5) | 7 | 1.5 | #FFE2BA |
| L_ExhibitEast | (3.5, 3, 9.5) | 7 | 1.5 | #FFE2BA |
| L_ArtWest | (-13, 3, 1) | 6 | 1.3 | #FFE2BA |
| L_ArtEast | (-8, 3, 1) | 6 | 1.3 | #FFE2BA |
| L_Temporary | (-11.5, 3, 8) | 7 | 1.4 | #FFE2BA |
| L_Storage | (9.5, 3, 1) | 7 | 1.5 | #D7E4EE |
| L_Security | (8, 3, -8.5) | 5 | 1.1 | #D7E4EE |
| L_Toilet | (-7.5, 3, -8.5) | 5 | 1.1 | #E2E8DD |
| L_StaffWest | (8, 3, -4.5) | 5 | 1 | #D7E4EE |
| L_StaffCorner | (14, 3, -4.5) | 5 | 1 | #D7E4EE |
| L_StaffEast | (15.5, 3, 1) | 5 | 1 | #D7E4EE |
| L_StaffNorth | (15.5, 3, 6) | 4 | 1 | #D7E4EE |
| L_Exit | (15.5, 3, 10) | 4 | 1 | #D7E4EE |
| L_Egypt | (-7, 3, 18) | 7 | 1.4 | #FFE2BA |
| L_StoneAge | (-7, 3, 26) | 7 | 1.4 | #FFE2BA |
| L_GreeceRome | (0, 3, 26) | 7 | 1.4 | #FFE2BA |
| L_AsianHeritage | (7, 3, 26) | 7 | 1.4 | #FFE2BA |
| L_NaturalHistory | (7, 3, 18) | 7 | 1.4 | #FFE2BA |

## Appendix D — Minimal fixed decoration

Add these Cube proxies/prefabs in Phase 13 under Decoration, **without colliders** on the Default layer. Sizes are target bounds for imported art; positions assume a centre pivot. Painting tilts use Z rotation in degrees; fit image/frame faces toward the room. Reception/security desk and toilet fixtures are already in Appendix B: do not duplicate them here. Monitor/mirror are visual props only. Re-bake lighting after adding fixed GI contributors.

| Name | Position X,Y,Z | Scale / target bounds | Rotation X,Y,Z | Notes |
| --- | --- | --- | --- | --- |
| Painting_ArtNorthWest | (-14, 2.1, 4.84) | (1.5, 1.1, 0.08) | (0, 0, 5) | M_Wood; face toward -Z |
| Painting_ArtNorthEast | (-7, 2.1, 4.84) | (1.5, 1.1, 0.08) | (0, 0, -4) | M_Wood; face toward -Z |
| Painting_ArtSouth | (-10, 2.1, -2.84) | (1.6, 1.1, 0.08) | (0, 0, 3) | M_Wood; face toward +Z |
| Monitor_Security | (9.4, 1.45, -10) | (0.7, 0.5, 0.1) | (0, 0, 0) | M_Metal; on desk; visual only |
| Mirror_Toilet | (-6.5, 1.8, -6.16) | (0.9, 0.8, 0.04) | (0, 0, 0) | Dull glass-like material, not a real-time mirror |
| Sign_Exit | (15.5, 2.9, 7.84) | (0.8, 0.25, 0.04) | (0, 0, 0) | Green sign; face toward -Z in corridor |
| Courtyard_VisualGround | (0, -0.1, 18) | (7, 0.2, 8) | (0, 0, 0) | M_Concrete; visual only; remove Box Collider; Default layer; exclude from NavMesh; no roof |

## Appendix E — Evidence log

Copy a row for each real milestone. Record actual results rather than marking planned checks as passed.

| Date | Phase/change | Screenshot/clip | Actual result | Decision/why | Commit hash |
| --- | --- | --- | --- | --- | --- |
| YYYY-MM-DD | v3.2 corner revision / later milestone | Evidence/01-plan-v3.2.png | Plan check only; Unity pending | Gallery loop and support rooms | After committing |

## Plan validation and outstanding work

`Plan_Validation.json` records document-generation checks. The plan has fifteen named spaces, sixteen non-overlapping floor slabs, eighteen interior doorways, three closed boundary doors and five fixed windows. A conservative 2D model checks positive sizes, unique cube names, door ownership between the correct adjacent rooms, reachable route markers, direct routes restricted to each door's two adjacent rooms, and alternate routes with each of the four original public-loop doors and six new gallery-loop doors blocked individually. It uses a 0.25 m grid and 0.35 m body radius with fixed cover included.

This is **not** a Unity NavMesh/physics test. Actual agent dimensions, voxelization, controller skin width, dynamic doors, animation, shooting, lighting, visibility and build performance still need in-engine validation. The simplified model does not establish accessibility/building-code compliance or guarantee tactical AI behaviour.

The optional `tools/generate_plan.py` is the shared source for the PNG, CSV, coordinate appendices and validation report. It requires Python and Pillow, and is not required to build the level manually. It does not create/import a Unity scene. If the plan changes, update that specification and the explanatory phases together; do not edit only the PNG or only one coordinate table.

## Corner corrections — revision 3.2

Updated 7 October 2026. These 13 approved wall changes close the 12 identified outward footprint corners and the exposed storage/staff junction at (14,-3). This is not a claim that every possible decorative seam has been inspected. No filler cubes are added. Each horizontal wall grows 0.1 m at one end and its centre moves 0.05 m towards that end, keeping its other end and the adjoining door/window opening fixed. Room footprints, floor slabs and door widths are unchanged.

Edit under Museum > Walls only. Keep Position Y=1.75, Rotation=(0,0,0), Scale Y=3.5 and Scale Z=0.2. Enter final values below, not increments. Keep WorldSolid and enabled non-trigger Box Colliders. Save, inspect at eye level, and rebake NavMesh if already baked. This documentation update does not modify or certify the current Unity scene.

| Name | Position X,Y,Z | Scale X,Y,Z |
| --- | --- | --- |
| Wall_01_Before_W08 | (-9.3, 1.75, -11) | (1.6, 3.5, 0.2) |
| Wall_03_End | (10.05, 1.75, -11) | (2.1, 3.5, 0.2) |
| Wall_04_Solid | (-7.55, 1.75, -6) | (5.1, 3.5, 0.2) |
| Wall_07_Solid | (15.55, 1.75, -6) | (3.1, 3.5, 0.2) |
| Wall_09_Solid | (-10.55, 1.75, -3) | (11.1, 3.5, 0.2) |
| Wall_10_End | (12.3, 1.75, -3) | (3.6, 3.5, 0.2) |
| Wall_17_Solid | (-11.55, 1.75, 11) | (9.1, 3.5, 0.2) |
| Wall_59_Before_D20 | (14.2, 1.75, 12) | (0.6, 3.5, 0.2) |
| Wall_59_End | (16.8, 1.75, 12) | (0.6, 3.5, 0.2) |
| Wall_40_Solid | (-8.8, 1.75, 14) | (3.6, 3.5, 0.2) |
| Wall_44_Solid | (8.8, 1.75, 14) | (3.6, 3.5, 0.2) |
| Wall_48_Solid | (-7.05, 1.75, 30) | (7.1, 3.5, 0.2) |
| Wall_50_Solid | (7.05, 1.75, 30) | (7.1, 3.5, 0.2) |

At the storage junction change Wall_10_End only; keep Wall_35_Before_D07 unchanged. Wall_10_End retains X=10.5 at D08 and now ends at X=14.1. The exit-side changes preserve D20 at X=14.5 to 16.5.
