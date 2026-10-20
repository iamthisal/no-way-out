# Museum loot containers

The eleven crates in Museum_Main and the Greece/Rome coin chest use `LootContainer`.
This includes all eight exhibit-room crates and the three storage crates.

Look at a crate and press **E** (gamepad **X**) to open it. After its lid finishes
opening, press the same button on an item or the crate body to collect one item.
The body offers the next item so tall crate walls do not make loot unreachable.
Each press collects one item. An emptied crate can be closed and reopened.
Use **Tab** to inspect the existing loot inventory.

Closed crates protect their contents. Collection uses the existing player sight,
reach, cursor, and health checks. Collected objects, including their lid trim,
disappear and cannot be collected twice. Reopening does not refill a crate;
reloading the encounter restores its original contents.

The eleven large crates each contain a clay vessel ($150) and a blue vase ($200).
The existing coin chest keeps its original gold coin ($500). Container contents
and values are serialized on each scene object's `LootContainer` component.

Covered crates move their cloth and light-colored trim with the hinged lid.
The two shared wooden crate meshes have open tops and visible interior faces.
Imported storage props use matching hollow wooden shells. Runtime setup creates
the lids, item colliders, and collectible visuals when the scene loads.

`LootContainerTests` covers opening before collection, individual collection,
duplicate prevention, empty reopening, sight/reach/health restrictions, independent
containers, original chest-coin collection, and all eight exhibit crate models.
