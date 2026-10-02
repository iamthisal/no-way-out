export const RECOVERY_CONFIG = {
  delay: 5,
  healthPerSecond: 6,
  waveClearHealth: 25,
  roomClearHealth: 100
};

export const ROOM_CONFIG = [
  { id: 0, name: "Start room", bounds: [-37, -1, 1, 29], lever: [-27, 3],
    door: [-24, 0, "z"], waves: [], pause: 2, color: 0xffc078, fog: 0x211c18, intensity: 1.25 },
  { id: 1, name: "Room 1", bounds: [-37, -1, -29, -1], lever: [-3, -24],
    door: [0, -20, "x"], waves: [{ Sentinel: 2 }, { Sentinel: 3 }, { Sentinel: 3 }],
    pause: 2.5, spawns: [[-32, -24], [-8, -8], [-18, -24], [-32, -12]],
    color: 0x96cbff, fog: 0x171e27, intensity: 2 },
  { id: 2, name: "Room 2", bounds: [1, 37, -29, -1], lever: [27, -3],
    door: [24, 0, "z"], waves: [{ Sentinel: 1, Stalker: 2 }, { Sentinel: 1, Stalker: 3 }, { Sentinel: 2, Stalker: 3 }],
    pause: 3, spawns: [[8, -24], [30, -24], [8, -8], [30, -8], [20, -24]],
    color: 0xd6adff, fog: 0x221b2c, intensity: 1.8 },
  { id: 3, name: "Room 3", bounds: [1, 37, 1, 29], lever: [30, 24],
    waves: [{ Sentinel: 2, Stalker: 2, Wasp: 1, Hound: 1 },
      { Sentinel: 2, Stalker: 3, Wasp: 1, Hound: 1 },
      { Sentinel: 3, Stalker: 3, Wasp: 2, Hound: 1 }],
    pause: 3.5, spawns: [[8, 24], [30, 24], [8, 8], [30, 8], [20, 24], [20, 8], [32, 16], [5, 16], [20, 16]],
    color: 0xff9999, fog: 0x291617, intensity: 1.6 }
];

export function contains(room, point, margin = 0) {
  const [minX, maxX, minZ, maxZ] = room.bounds;
  return point.x >= minX + margin && point.x <= maxX - margin
    && point.z >= minZ + margin && point.z <= maxZ - margin;
}
