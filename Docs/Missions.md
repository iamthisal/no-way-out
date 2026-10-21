# Missions

The museum heist runs as a chain of missions, built one small step at a time on
`feature/missions`.

## The plan

1. **Hammer and keycard.** Start in the lobby. Storage is open from Central Hall,
   and the hammer waits on its workbench. Sneak past the toy soldier guard to the
   back of Stone Age for the staff keycard. It opens Storage's back doors into the
   Staff Corridor.
2. **Alarm.** Breaking glass, or being spotted by the guard, sets off the alarm.
   Glass also calls the security hounds, and the alarm sends two rolling robots
   that hunt by sound and fire lasers. A guard who spots you whistles and chases
   you briefly with a cap rifle, then goes back to patrolling. Breaking glass
   always brings the guard over to look.
3. **Security room.** Open its lobby door with the staff keycard, or break in
   from the Staff Corridor with the hammer. The computer switches the alarm system
   off for good, which sends the robots back to their chargers. After that,
   breaking glass brings only the guard and the hounds, never the robots. The gun
   from the Security room locker defeats the guard.
4. **Dinosaur room.** Looting Natural History wakes two skeletons. Defeating them
   reveals a skeleton key that opens the barred gates into Egypt.
5. **Egypt.** Complete the mummy curse. The goal is the four necklaces, and the
   treasure you gather is ranked at the end.

Dying sends you back to the lobby with every finished mission's items and your
loot, with the alarm off.

## Mission doors

`MissionBuilder.DoorLocks` lists every mission door. **Tools > Lockdown > Missions >
Place Staff Keycard and Door Locks** brings the scene in line with it, and is safe
to run again.

| Door | Between | Lock |
|---|---|---|
| D06 | Central Hall and Storage | None |
| D07, D08 | Storage and Staff Corridor | Staff keycard |
| D09 | Staff Corridor and Security | Breaks with the hammer |
| D10 | Lobby and Security | Staff keycard |

- **Prompts.** "Locked. Needs the staff keycard", then "Unlock with the staff
  keycard" once you carry it. A breakable lock reads "Locked. Break the lock with
  the hammer". A sealed door reads "This door is sealed".
- **Card readers.** Each locked door has a reader on the side you come from. It
  shows red while locked, green once unlocked, and goes dark when smashed.
  Placement reuses the World Builder's readers and adds one only where that side
  has none.
- **How it works.**
  - `DoorLock` sits on the door's hinge beside `Door`, and `Door` asks it before
    moving.
  - Keys are ids held by `PlayerKeys` on the player.
  - A breakable lock takes damage from blows to the door leaf: one hammer blow
    (100), or four pistol shots.
  - `Unlock()` releases any lock from a script.

## Step 1: the staff keycard

- **Where it is.** The keycard stands on a walnut plinth in the north-west nook of
  Stone Age, between the west wall and the wall case. The standing stones hide it
  from the Greece / Rome doorway.
- **Taking it.** Press **E**. `KeyPickup` adds `StaffKeycard` to the player's keys.
- **The hammer.** It stays on the Storage workbench, open to anyone.
- **Building it.** **Tools > Lockdown > Missions > Build Staff Keycard** builds the
  prefab.

### Testing (step 1)

`StaffKeycardTests`:

- a lock opens only for its own key, and its reader light changes colour;
- in the museum, Storage opens from Central Hall and the hammer is inside;
- the back doors refuse to open until the keycard in Stone Age is taken, then
  open.

`DoorTests` releases every mission lock before its sweep of every door.

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
  the NavMesh, so he can't pass any closed door. A shut toilet makes a hiding
  place.
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

**His hearing.** Breaking glass within 30 m brings him hurrying over to the spot.
He turns about, looking, for 5 s, then goes back to his route. If he sees you on
the way, the usual "?" and chase follow.

**The alarm.** `MuseumAlarm` sits on the `Missions` root. Once raised, it plays a
siren loop and flashes **ALARM** on the HUD's top row. Breaking glass and the
guard spotting you raise it.

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

## Step 3: the Security room

- **Getting in.** Open D10 from the lobby with the staff keycard, or reach D09
  from the Staff Corridor and break its lock with one hammer blow. Breaking it is
  quiet.
- **The computer.** It is the desk computer (`AlarmConsole` on
  `P_Security_DeskMonitor`). Its screen shows blue while the system is armed and
  red during the alarm.
- **Switching off.** Press **E** at it ("Turn off the alarm", or "Switch off the
  alarm system" before anything has gone off). The system goes offline for good
  and the screen goes dark.
- **After that.** `MuseumAlarm.Disable` keeps anything from raising the alarm
  again. Breaking glass still calls the hounds and brings the guard over to look,
  but never the robots.
- **The skeleton archer is out.** It is removed from the museum for now; its code
  and prefab stay. The dinosaur-room step will bring skeletons back.
- **Building it.** **Tools > Lockdown > Missions > Place Security Console** wires
  the computer and removes the archer. The door locks come from the mission door
  table.

### Testing (step 3)

`SecurityRoomTests`:

- **Lock rules:** a breakable lock gives way to one hammer blow but not to a pistol
  shot. Keycard and sealed locks ignore damage.
- **Getting in:** in the museum the hammer quietly breaks D09's lock. D10 ignores
  the hammer but opens and closes with the staff keycard.
- **The computer:** it switches the alarm off for good, so neither the guard nor
  glass can raise it again.
- **Glass:** breaking display glass raises the alarm. Once the alarm is off, glass
  brings the guard over to look.
- **The archer:** it is gone.

## Step 4: the pistol

- **Where it is.** The pistol waits in the Security room locker (`P_Security_Locker`).
  The museum's player starts without it, so **2** does nothing until it is found.
- **Taking it.** Look at the locker and press **E** ("Take the pistol (2 to
  equip)"). The pistol goes straight into your hands, and the locker has nothing
  more to offer.
- **How it works.** `PlayerEquipment.ownsPistol` is on by default, so the player
  prefab and any test arena still start with a pistol. The museum's player turns
  it off. `PistolPickup` calls `TryCollectPistol`.
- **Building it.** **Tools > Lockdown > Missions > Place Pistol Locker** adds the
  pickup and takes the pistol off the museum's player.

### Testing (step 4)

`SecurityRoomTests` checks that the player starts unarmed, that the locker hands
over the pistol and equips it, and that the empty locker offers nothing more. The
museum tests that shoot (`EnemyShootingTests`, `WanderingEnemyTests`,
`LootPickupTests`, `PlayerEquipmentTests`) collect the locker pistol first.

## Step 5: the security robots

Two small robots that roll on their sphere bellies rest on glowing chargers in
far-apart corners of the museum: the north-west corner of the Art Gallery and the
north-west corner of Asian Heritage.

- **Waking up.** When the alarm sounds, from broken glass or the guard's whistle,
  their eyes turn red. After a 0.2 s chirp they head straight for where you are at
  that moment, not for the broken case, so they close in from opposite ends of the
  museum.
- **Hunting.** They roll at 2.6 m/s, which is slower than your walk, and search
  around the spot they were sent to. Every new alarm report sends them to where
  you are again.
- **The laser.** They see 12 m in a 160-degree cone. On sight they close to about
  6 m and fire an eye laser that does 8 damage, every 1.3 s. Before each shot the
  eye glows white for 0.45 s with its aim locked, so stepping aside makes it miss.
  The beam only hurts the player; it doesn't break glass.
- **Losing you.** If they lose sight of you for 1.5 s, they hunt your last known
  position.
- **Going home.** When the Security computer switches the alarm off, they roll
  back to their own corners. Since the alarm can never ring again, they stay
  docked.
- **Fighting back.** Each robot has 75 health: three pistol shots or one hammer
  blow. A destroyed robot slumps, its eye goes dark, and it stays down. You can
  also catch them on their chargers before the alarm ever sounds.

**How it's built.**

- **`SecurityRobot`:** the robot's states, movement and sight.
- **`RobotLaser`:** the telegraphed shot.
- **`PlayerSight`:** the cone and line-of-sight check shared by the guard and the
  robots.
- **Alarm reports:** each report comes through `MuseumAlarm.Alerted`, and the
  robots answer it by heading for the player.

**Building it.**

- **Tools > Lockdown > Missions > Build Security Robot** builds the placeholder
  model: a white shell with orange bands that show it rolling, a dark dome head and
  a glowing eye.
- **Tools > Lockdown > Missions > Place Security Robots** puts a charger and a robot
  at each corner in `MissionBuilder.RobotDocks`. It is safe to run again; adding a
  row adds a robot.
- `Blender/Missions/generate_audio.py` synthesises the hum, power-up, laser and
  breakdown sounds.

### Testing (step 5)

`SecurityRobotTests` runs most checks in a small arena of its own:

- the alarm wakes a robot, which then hits a player in sight;
- sidestepping during the charge makes the shot miss;
- three pistol shots destroy a robot for good;
- switching the alarm off sends it home to its charger, and it can't be woken
  again.

In the museum it checks that the robots wait in far-apart corners. When a distant
case breaks, both set off at once toward the player rather than the case, and they
dock again once the alarm is off. Glass broken after the switch-off leaves them on
their chargers.
