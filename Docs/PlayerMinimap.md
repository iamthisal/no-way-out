# Player minimap

Unity implementation in `Museum_Main`. A small floor plan of the museum sits in the
top-left corner of the screen and shows where the player is in real time.

- The map is **north-up and never rotates**. World +Z is up on the map and +X is
  to the right.
- A yellow arrow marks the player. It moves with the player's position and turns
  with their heading (yaw); height is ignored.
- Walls are light, furniture and display cases mid-grey, floors dark. Areas outside
  the museum are transparent. Doors are not on the WorldSolid layer, so doorways
  read as gaps in the walls.
- The map is about a third of the screen height and keeps the plan's aspect ratio.

## Implementation

`Assets/_Project/Scripts/Runtime/Player/PlayerMinimap.cs` (namespace
`Lockdown.Player`) draws the map with `OnGUI`, like the other HUD elements. It holds
the player transform, the floor-plan texture and the world X/Z rectangle that the
texture covers. `WorldToMap` converts a world position to map coordinates, and the
arrow is rotated around its centre by the player's yaw. If no player is assigned it
finds the `FirstPersonController` on `Awake`.

`Tools > Lockdown > Build Museum Minimap` (`Scripts/Editor/MinimapBuilder.cs`) bakes
the plan from the museum's WorldSolid colliders at 10 px per metre:

| Probe | Result |
|---|---|
| Solid at 2.4 m | Wall |
| Solid at 0.9 m only | Furniture / prop |
| Floor within 0.5 m below | Floor |
| None of these | Transparent |

It writes `Assets/_Project/Textures/UI/MuseumMinimap.png`, then creates or updates
the `HUD_Minimap` scene object and links it to the player. **Re-run it and save the
scene after changing museum geometry**, otherwise the map will be out of date.
Tall props that reach 2.4 m are drawn as walls.

## Verification

On 21 November 2026, the four `PlayerMinimapTests` and the existing player tests
passed (6/6, Play Mode assembly `Lockdown.Tests.Player`). In Play mode the arrow
was checked in the Lobby facing north-east and in the Asian Heritage gallery after
moving the player there, with the map staying north-up.
