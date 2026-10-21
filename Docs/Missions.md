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

### Testing (step 1)

`Lockdown.Tests.Missions` checks that a lock opens only for its own key and that
its reader light changes colour. In the museum it checks that all three Storage
doors refuse to open, that the keycard in Stone Age can be taken, that each door
then opens, and that the hammer is still inside. `DoorTests` gives its test player
the keycard, so its sweep of every interior door still covers Storage.

## Step 2: the guard patrol and the alarm

The museum's wandering enemy is now the toy soldier guard. It still uses the
placeholder model.

**His route.** He walks a 13-stop loop:

| | Stops |
|---|---|
| Out | Central Hall, Artifact Exhibition, Natural History, Asian Heritage, Greece / Rome, Stone Age |
| Back | Greece / Rome, Asian Heritage, Natural History, Artifact Exhibition |
| West side | Temporary, Art Gallery, Lobby |

At each stop he pauses for 2.5 s and sweeps his gaze 50 degrees left and right.

- **Where he can't go.** He skips Egypt, which will be barred. Closed doors carve
  the NavMesh, so the Storage and Security rooms are out of reach, and so are the
  toilets once their doors are shut. A closed toilet makes a hiding place.
- **His first move.** He starts in Central Hall and heads north, away from the
  player in the lobby. He can't spot anyone in the first 2 s.

**How he spots you.** He sees 10 m ahead in a 110-degree cone, at eye height. A
glimpse of your head or chest counts, but walls and display cases block his view.

- **The "?" meter.** While he can see you, a "?" over his head fills up. It takes
  about 0.35 s up close and 1.4 s at the edge of his sight, and drains when he
  loses sight of you, so you can duck back into cover.
- **When it fills.** It turns to a red "!". He blows his whistle, raises the alarm
  and chases at 3 m/s. You walk at 4 m/s, so you can get away.
- **The cap rifle.** He only fires it once he has spotted you: 10 damage within
  4 m, every 1.2 s.
- **Being shot.** Shooting him gives you away at once.
- **Giving up.** If he hasn't seen you for 8 s, he goes back to the nearest stop
  on his route.

**The alarm.** `MuseumAlarm` sits on the `Missions` root. Once raised, it plays a
siren loop and flashes **ALARM** on the HUD's top row. For now it stays on; in
step 3 the Security computer turns it off, and breaking glass raises it too.

**How it's built.**

- `WanderingEnemy` has an optional patrol route. Without one, he still wanders
  around his post.
- `GuardSenses` can take over his legs with `Pursue` and hand them back with
  `ResumePatrol`.
- `EnemyShooting.HoldFire` keeps the rifle quiet until he has spotted you.
- If a test switches the walker off, `GuardSenses` steps aside and the rifle
  behaves as it always did.

**Building it.** **Tools > Lockdown > Missions > Place Guard Patrol and Alarm**
checks that every stop is on the NavMesh and connected to the next, then sets the
route, adds the whistle and adds the alarm. `Blender/Missions/generate_audio.py`
synthesises the siren and the whistle.

### Testing (step 2)

`GuardPatrolTests` runs most checks in a small arena of its own:

- the meter fills before the alarm sounds, and he holds fire until then;
- he misses a player behind him or behind a wall;
- he gives up after losing you, while the alarm stays on;
- a shot gives you away at once.

In the museum it checks that his route reaches Stone Age and the lobby, that it
avoids Egypt, Storage and Security, and that he sets off north without spotting
the player at the start. `WanderingEnemyTests` now accepts a patrol stop as his
destination.
