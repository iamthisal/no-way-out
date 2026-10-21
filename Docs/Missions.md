# Missions

The museum heist runs as a chain of missions, built one small step at a time on
`feature/missions`.

## The plan

1. **Hammer.** Start in the lobby. Sneak past the toy soldier guard, take the
   keycard from the back of Stone Age, and sneak back to unlock the Storage room,
   where the hammer waits on the workbench.
2. **Alarm.** Breaking glass, or being spotted by the guard, sets off the alarm.
   Glass still calls the security hounds, and the alarm also sends two rolling
   robots that hunt by sound and fire lasers. A guard who spots you whistles and
   chases you briefly with a cap rifle, then goes back to patrolling.
3. **Security room.** Break the Security room lock with the hammer. Its computer
   turns the alarm off, which sends the robots back to their chargers. The gun in
   the Security room defeats the guard.
4. **Dinosaur room.** Looting Natural History wakes two skeletons. Defeating them
   reveals a skeleton key that opens the barred gates into Egypt.
5. **Egypt.** Complete the mummy curse. The goal is the four necklaces, and the
   treasure you gather is ranked at the end.

Dying sends you back to the lobby with every finished mission's items and your
loot, with the alarm off.

## Step 1: the Storage keycard

- **Locked doors.** All three Storage doors (D06 from Central Hall, D07 and D08
  from the Staff Corridor) start locked. While locked, the prompt reads
  "Locked. Needs the Storage keycard" and the door does not move.
- **Card readers.** Each door has a card reader whose light shows red while locked
  and green once unlocked. Placement reuses the readers the World Builder already
  mounted by D07 and D08, and adds readers outside D06 and D07.
- **The keycard.** It stands on a walnut plinth in the north-west nook of Stone
  Age, between the west wall and the wall case. It is hidden from the Greece / Rome
  doorway by the standing stones. Press **E** to take it.
- **Unlocking.** With the keycard, the prompt reads "Unlock with the Storage
  keycard". Pressing **E** unlocks the door and opens it, and it then behaves like
  any other door.
- **The hammer.** It stays on the Storage workbench.
- **How it works.** `DoorLock` sits on a door's hinge beside `Door`, and `Door`
  asks it before moving. Keys are ids held by `PlayerKeys` on the player.
  `KeyPickup` adds its id when taken.
- **Building it.** **Tools > Lockdown > Missions > Build Storage Keycard** builds
  the prefab. **Tools > Lockdown > Missions > Place Storage Keycard and Locks**
  adds the `Missions` root, the keycard, the readers and the locks.

### Testing

`Lockdown.Tests.Missions` checks that a lock opens only for its own key and that
its reader light changes colour. In the museum it checks that all three Storage
doors refuse to open, that the keycard in Stone Age can be taken, that each door
then opens, and that the hammer is still inside. `DoorTests` gives its test player
the keycard, so its sweep of every interior door still covers Storage.
