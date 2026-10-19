# Interactive museum doors

Unity implementation in `Museum_Main`. Look at a door within the player's existing
2.5 m interaction reach and press **E** (gamepad **X**) to open or close it. Holding
the button toggles once; pressing again can reverse a moving door. Interactions
use the existing sight, cursor, player-health, and controller checks.

Configured interior doors: D06, D07, D08 (storage), D09, D10 (security), D11
(toilet), and D21 (guest toilet). Exterior boundary doors and decorative cubicle
panels retain their existing behavior.

`Assets/_Project/Scripts/Runtime/World/Door.cs` implements `IInteractable` on an
unscaled `Hinge` object. Its child panel rotates around that hinge, with a moving
box collider and a carving `NavMeshObstacle`. The two washroom frames remain
outside their hinges and use mesh colliders to preserve their openings.

Select a `Hinge` in the Inspector to change **Open Angle** (negative reverses the
swing), **Degrees Per Second**, or **Starts Open**. Defaults are 95 degrees,
180 degrees per second, and closed. The controller moves the panel directly;
it does not simulate a physical hinge or detect an occupant in the swing arc.

For another door, create an unscaled hinge at the panel's edge, parent the moving
model beneath it, and attach `Door` plus a panel-sized collider. Keep fixed frames
outside that hinge. The existing player requires no additional input bindings.

`Tools > Lockdown > Configure Museum Doors` configures the seven named models in
the active museum scene, skips doors already configured, and groups its changes
into one Undo operation. Save the scene after running it. The washroom model
instances are unpacked so their panel and frame can have different parents; the
source model assets are preserved.
