import "./style.css";
import * as THREE from "three";
import * as CANNON from "cannon-es";
import EasyStar from "easystarjs";
import { createBlaster } from "./blaster.js";
import { Pursuer, Ambusher, createEnemyCombat } from "./enemy-ai.js";
import { pickCombatPost } from "./enemy-life.js";
import { Hound } from "./hound-ai.js";
import { ROOM_CONFIG, RECOVERY_CONFIG, contains } from "./room-config.js";
import { RoomController } from "./room-controller.js";

const canvas = document.querySelector("#game");
const hud = {
  healthFill: document.querySelector("#healthFill"),
  healthText: document.querySelector("#healthText"),
  recoveryText: document.querySelector("#recoveryText"),
  staminaFill: document.querySelector("#staminaFill"),
  staminaText: document.querySelector("#staminaText"),
  alarmFill: document.querySelector("#alarmFill"),
  alarmText: document.querySelector("#alarmText"),
  terminalText: document.querySelector("#terminalText"),
  debug: document.querySelector("#roomDebug"),
  rexText: document.querySelector("#rexText"),
  prompt: document.querySelector("#prompt"),
  damage: document.querySelector("#damageVignette"),
  centerPanel: document.querySelector("#centerPanel"),
  pausePanel: document.querySelector("#pausePanel"),
  endPanel: document.querySelector("#endPanel"),
  endTitle: document.querySelector("#endTitle"),
  endCopy: document.querySelector("#endCopy"),
  soundToggle: document.querySelector("#soundToggle")
};

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x151719);
scene.fog = new THREE.Fog(0x151719, 30, 82);

const camera = new THREE.PerspectiveCamera(74, window.innerWidth / window.innerHeight, 0.05, 150);
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.75));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.35;
scene.add(camera);

const world = new CANNON.World({ gravity: new CANNON.Vec3(0, 0, 0) });
world.allowSleep = true;

const CELL = 2;
const GRID_W = 38;
const GRID_H = 30;
const ORIGIN_X = -GRID_W * CELL * 0.5;
const ORIGIN_Z = -GRID_H * CELL * 0.5;
const gridMatrix = Array.from({ length: GRID_H }, () => Array(GRID_W).fill(0));

const blockers = [];
const obstacleBoxes = [];
const terminals = [];
const conveyors = [];
const doors = [];
const enemies = [];
const controllers = ROOM_CONFIG.map(config => new RoomController(config, spawnWave));
let currentRoom = 0;
let ambientLight;
const interactables = [];
const keys = new Set();
const raycaster = new THREE.Raycaster();
const clock = new THREE.Clock();
const tmpVec = new THREE.Vector3();
const tmpVec2 = new THREE.Vector3();

let yaw = 0;
let pitch = 0;
let running = false;
let paused = false;
let gameEnded = false;
let interactTarget = null;
let lastFootstep = 0;
let lastDamageAt = -Infinity;
let recoveryDelay = 0;
let recovering = false;
let recoveryMessage = "";
let recoveryMessageUntil = 0;
const rewardedWaves = new Set();
let soundPing = null;
let audio = null;
let securityAlert = null;
let alertRevision = 0;

function emitHoundSound(kind, point = new THREE.Vector3(player.body.position.x, 0, player.body.position.z)) {
  for (const enemy of enemies) {
    if (enemy.hound && contains(ROOM_CONFIG[enemy.roomId], point) && clearLine(enemy.group.position, point)) enemy.behavior.hear(enemy, point, kind);
  }
}
const combatPosts = [
  { cover: new THREE.Vector3(-28, 0, 13.5), peek: new THREE.Vector3(-31, 0, 13.5) },
  { cover: new THREE.Vector3(-26, 0, 4.5), peek: new THREE.Vector3(-29, 0, 4.5) },
  { cover: new THREE.Vector3(-18, 0, 20.5), peek: new THREE.Vector3(-21, 0, 20.5) },
  { cover: new THREE.Vector3(26, 0, 4.5), peek: new THREE.Vector3(29, 0, 4.5) },
  { cover: new THREE.Vector3(-26, 0, -11.5), peek: new THREE.Vector3(-29, 0, -11.5) },
  { cover: new THREE.Vector3(12, 0, -19.5), peek: new THREE.Vector3(15, 0, -19.5) }
];

function broadcastAlert(point) {
  if (!securityAlert || securityAlert.point.distanceTo(point) > 6 || securityAlert.ttl < 8) {
    securityAlert = { point: point.clone(), ttl: 12, revision: ++alertRevision };
  } else securityAlert.ttl = 12;
}

const player = {
  health: 100,
  stamina: 100,
  alarm: 0,
  terminals: 0,
  crouch: 0,
  body: new CANNON.Body({
    mass: 1,
    shape: new CANNON.Sphere(0.43),
    position: new CANNON.Vec3(-31, 1.1, 24),
    fixedRotation: true,
    allowSleep: false,
    linearDamping: 0.7
  })
};
world.addBody(player.body);

const blaster = createBlaster({ camera, scene, blockers, enemies, playTone,
  onNoise: () => {
    emitHoundSound("gunshot");
    soundPing = { x: player.body.position.x, z: player.body.position.z, strength: 28, ttl: 1.5 };
    player.alarm = Math.min(100, player.alarm + 8);
    broadcastAlert(new THREE.Vector3(player.body.position.x, 0, player.body.position.z));
  }
});

const enemyCombat = createEnemyCombat({ scene, blockers, playTone, damagePlayer: damage => {
  if (gameEnded) return;
  player.health = Math.max(0, player.health - damage);
  lastDamageAt = clock.elapsedTime;
  recoveryDelay = RECOVERY_CONFIG.delay;
  recovering = false;
  playTone(150, 0.1, "square", 0.08);
  if (player.health <= 0) endGame(false);
}});

const rex = {
  cooldown: 0,
  mesh: null,
  light: null,
  target: new THREE.Vector3(),
  propeller: null,
  qHeld: false,
  distractionTime: 0,
  phase: "follow",
  pulseTimer: 0,
  distractedEnemies: new Set(),
  distractionPoint: new THREE.Vector3(),
  beacon: null
};

function makeMat(color, roughness = 0.9) {
  return new THREE.MeshStandardMaterial({ color, roughness, metalness: 0.08, flatShading: true });
}

const mats = {
  floor: makeMat(0x4b5050),
  wall: makeMat(0x262a2d),
  yellow: makeMat(0xf0c13c),
  red: makeMat(0xd23430),
  shelf: makeMat(0x65727a),
  shelfDark: makeMat(0x31383d),
  crateA: makeMat(0x9d7845),
  crateB: makeMat(0x577b82),
  conveyor: makeMat(0x1f2f36),
  belt: makeMat(0x202326),
  terminal: makeMat(0x2a404a),
  active: makeMat(0x39d784),
  door: makeMat(0x76828a),
  sentinel: makeMat(0x2f80ed),
  wasp: makeMat(0xffc233),
  stalker: makeMat(0x9d41d7),
  hound: makeMat(0xd94747),
  rex: makeMat(0x58d8ff)
};

function addBoxMesh({ x, y = 0, z, w, h, d, mat, shadow = true, block = false, body = false, name = "" }) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
  mesh.position.set(x, y + h * 0.5, z);
  mesh.castShadow = shadow;
  mesh.receiveShadow = true;
  mesh.name = name;
  scene.add(mesh);
  if (block) {
    blockers.push(mesh);
    obstacleBoxes.push({ x, z, w, d });
    markGridBlocked(x, z, w, d);
  }
  if (body) {
    const physics = new CANNON.Body({ mass: 0, shape: new CANNON.Box(new CANNON.Vec3(w * 0.5, h * 0.5, d * 0.5)) });
    physics.position.set(x, y + h * 0.5, z);
    world.addBody(physics);
    mesh.userData.physics = physics;
  }
  return mesh;
}

function worldToGrid(x, z) {
  return {
    gx: Math.floor((x - ORIGIN_X) / CELL),
    gz: Math.floor((z - ORIGIN_Z) / CELL)
  };
}

function gridToWorld(gx, gz) {
  return new THREE.Vector3(ORIGIN_X + gx * CELL + CELL * 0.5, 0, ORIGIN_Z + gz * CELL + CELL * 0.5);
}

function markGridBlocked(x, z, w, d) {
  const pad = 0.55;
  const min = worldToGrid(x - w * 0.5 - pad, z - d * 0.5 - pad);
  const max = worldToGrid(x + w * 0.5 + pad, z + d * 0.5 + pad);
  for (let gz = Math.max(0, min.gz); gz <= Math.min(GRID_H - 1, max.gz); gz++) {
    for (let gx = Math.max(0, min.gx); gx <= Math.min(GRID_W - 1, max.gx); gx++) {
      const center = gridToWorld(gx, gz);
      if (Math.abs(center.x - x) < w / 2 + pad && Math.abs(center.z - z) < d / 2 + pad) gridMatrix[gz][gx] = 1;
    }
  }
}

function buildWarehouse() {
  addLights();
  addBoxMesh({ x: 0, z: 0, w: 76, h: 0.18, d: 60, mat: mats.floor, shadow: false });

  for (let x = -34; x <= 34; x += 8) {
    addBoxMesh({ x, y: 0.02, z: -2, w: 0.32, h: 0.03, d: 53, mat: mats.yellow, shadow: false });
  }
  addBoxMesh({ x: 0, y: 0.03, z: 25.5, w: 66, h: 0.04, d: 0.35, mat: mats.yellow, shadow: false });
  addBoxMesh({ x: 29, y: 0.03, z: -25, w: 9, h: 0.04, d: 0.35, mat: mats.yellow, shadow: false });

  addWall(0, -30, 76, 1);
  addWall(0, 30, 76, 1);
  addWall(-38, 0, 1, 60);
  addWall(38, 0, 1, 60);

  // Clockwise route: southwest, northwest, northeast, southeast.
  addWall(-33, 0, 10, 1);
  addWall(0, 0, 40, 1);
  addWall(33, 0, 10, 1);
  addWall(0, -27, 1, 6);
  addWall(0, 7, 1, 46);
  combatPosts.length = 0;
  addCrateStack(-20, -16, 3);
  addCrateStack(-10, -16, 2);
  for (const x of [10, 26]) for (const z of [-18, -12]) addShelf(x, z);
  for (const [x, z] of [[18, -6], [18, -26], [12, 14], [26, 14]]) {
    addBoxMesh({ x, z, w: 1.5, h: 4.8, d: 1.5, mat: mats.shelfDark, block: true, body: true });
  }
  addShelf(16, 18);
  addCrateStack(28, 18, 3);
  for (const room of ROOM_CONFIG) {
    if (room.door) addRoomDoor(room);
    addRoomLever(room);
  }
  addRex();
  applyRoomLighting(0);
}

function addLights() {
  ambientLight = new THREE.HemisphereLight(0xe2efff, 0x73736b, 2.3);
  scene.add(ambientLight);
  const sun = new THREE.DirectionalLight(0xe8f0ff, 0.65);
  sun.position.set(-18, 36, 18);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  sun.shadow.camera.left = -45;
  sun.shadow.camera.right = 45;
  sun.shadow.camera.top = 45;
  sun.shadow.camera.bottom = -45;
  scene.add(sun);

  const lampMaterial = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xddecff, emissiveIntensity: 3 });
  for (const x of [-28, -12, 12, 28]) {
    for (const z of [-22, -6, 10, 23]) {
      addBoxMesh({ x, y: 5.3, z, w: 4.5, h: 0.12, d: 0.65, mat: lampMaterial, shadow: false });
      const room = ROOM_CONFIG.find(room => contains(room, { x, z }));
      const lamp = new THREE.PointLight(room?.color ?? 0xe5f1ff, room?.id === 0 ? 22 : 45, 22, 2);
      lamp.position.set(x, 5.1, z);
      scene.add(lamp);
    }
  }
  const flashlight = new THREE.SpotLight(0xfff4dd, 30, 22, Math.PI / 6, 0.55, 1.5);
  flashlight.position.set(0.15, -0.1, -0.1);
  const aim = new THREE.Object3D();
  aim.position.set(0, 0, -12);
  camera.add(flashlight, aim);
  flashlight.target = aim;

  for (const [x, z] of [[-24, -18], [8, -20], [26, 7], [-28, 18]]) {
    const alarm = new THREE.PointLight(0xff2424, 0.35, 18);
    alarm.position.set(x, 5, z);
    alarm.userData.alarm = true;
    scene.add(alarm);
  }
}

function addWall(x, z, w, d) {
  addBoxMesh({ x, z, w, h: 4.8, d, mat: mats.wall, block: true, body: true });
}

function addShelf(x, z) {
  for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
    combatPosts.push({ cover: new THREE.Vector3(x + sx * 4.2, 0, z + sz * 1.4), peek: new THREE.Vector3(x + sx * 6, 0, z + sz * 1.4) });
  }
  addBoxMesh({ x, z, w: 10, h: 3.4, d: 1.15, mat: mats.shelfDark, block: true, body: true, name: "Shelf blocker" });
  for (let i = -1; i <= 1; i++) {
    addBoxMesh({ x, y: 0.9 + i * 0.85, z, w: 10.4, h: 0.12, d: 1.45, mat: mats.shelf });
  }
  for (const ox of [-4.6, 0, 4.6]) {
    addBoxMesh({ x: x + ox, y: 0.2, z, w: 0.16, h: 3.1, d: 1.35, mat: mats.shelf });
  }
}

function addCrateStack(x, z, count) {
  for (let i = 0; i < count; i++) {
    const ox = (i % 2) * 1.25;
    const oz = Math.floor(i / 2) * 1.25;
    const size = 1.2 + (i % 3) * 0.1;
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
      combatPosts.push({ cover: new THREE.Vector3(x + ox, 0, z + oz + sz * (size / 2 + 0.8)),
        peek: new THREE.Vector3(x + ox + sx * (size / 2 + 1), 0, z + oz + sz * (size / 2 + 0.8)) });
    }
    addBoxMesh({ x: x + ox, z: z + oz, w: size, h: size, d: size, mat: i % 2 ? mats.crateA : mats.crateB, block: true, body: true });
  }
}

function addConveyor(x, z, w, d, dir) {
  const base = addBoxMesh({ x, z, w, h: 0.28, d, mat: mats.conveyor, shadow: false });
  const belt = addBoxMesh({ x, y: 0.18, z, w: w - 0.35, h: 0.05, d: d - 0.35, mat: mats.belt, shadow: false });
  const arrows = [];
  const count = Math.max(3, Math.floor((dir.x ? w : d) / 3));
  for (let i = 0; i < count; i++) {
    const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.32, 0.7, 3), mats.yellow);
    arrow.rotation.x = Math.PI * 0.5;
    arrow.rotation.z = dir.x > 0 ? -Math.PI * 0.5 : dir.z < 0 ? Math.PI : 0;
    arrow.position.set(x + (dir.x ? -w * 0.38 + i * 2.2 : 0), 0.48, z + (dir.z ? d * 0.38 - i * 2.2 : 0));
    scene.add(arrow);
    arrows.push(arrow);
  }
  conveyors.push({ x, z, w, d, dir, running: true, belt, arrows, base });
}

function addTerminal(x, z, label) {
  const group = new THREE.Group();
  const body = new THREE.Mesh(new THREE.BoxGeometry(1.15, 1.5, 0.7), mats.terminal);
  body.position.y = 0.75;
  const screen = new THREE.Mesh(new THREE.BoxGeometry(0.78, 0.44, 0.04), makeMat(0x1bd2ff));
  screen.position.set(0, 1.08, -0.37);
  group.add(body, screen);
  group.position.set(x, 0, z);
  scene.add(group);
  const terminal = { type: "terminal", label, group, screen, x, z, progress: 0, done: false };
  terminals.push(terminal);
  interactables.push(terminal);
}

function addSwitch(x, z) {
  const mesh = addBoxMesh({ x, z, w: 0.9, h: 1.1, d: 0.65, mat: makeMat(0x41555e) });
  const lever = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.75, 0.16), mats.yellow);
  lever.position.set(x, 1.1, z - 0.2);
  lever.rotation.x = 0.5;
  scene.add(lever);
  interactables.push({ type: "switch", x, z, mesh, lever });
}

function addDoor(x, z) {
  const left = addBoxMesh({ x: x - 1.7, z, w: 3.2, h: 4.5, d: 0.45, mat: mats.door, block: true, body: true });
  const right = addBoxMesh({ x: x + 1.7, z, w: 3.2, h: 4.5, d: 0.45, mat: mats.door, block: true, body: true });
  const marker = addBoxMesh({ x, y: 0.04, z: z + 2.6, w: 7, h: 0.04, d: 1.1, mat: mats.yellow, shadow: false });
  doors.push({ left, right, marker, open: false });
}

function addRoomDoor(room) {
  const [x, z, axis] = room.door;
  const mesh = addBoxMesh({ x, z, w: axis === "z" ? 8 : 1, h: 4.8,
    d: axis === "z" ? 1 : 8, mat: mats.door, block: true, body: true, name: `Door ${room.id + 1}` });
  const obstacle = obstacleBoxes[obstacleBoxes.length - 1];
  const marker = addBoxMesh({ x, z, y: 4.8, w: axis === "z" ? 8 : 1,
    h: 0.2, d: axis === "z" ? 1 : 8, mat: mats.red });
  doors.push({ mesh, obstacle, marker, open: false, cleared: false, roomId: room.id });
}

function addRoomLever(room) {
  const [x, z] = room.lever;
  const mesh = addBoxMesh({ x, z, w: 0.8, h: 1.2, d: 0.6, mat: mats.terminal });
  const lever = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.7, 0.16), mats.yellow);
  lever.position.set(x, 1.4, z);
  lever.rotation.x = 0.5;
  scene.add(lever);
  if (room.id === 3) mesh.visible = lever.visible = false;
  interactables.push({ type: "lever", x, z, mesh, lever, roomId: room.id, used: false });
}

function openRoomDoor(roomId) {
  const door = doors.find(door => door.roomId === roomId);
  if (door) door.open = true;
  playTone(430, 0.28, "sine", 0.08);
}

function rebuildGrid() {
  for (const row of gridMatrix) row.fill(0);
  for (const box of obstacleBoxes) markGridBlocked(box.x, box.z, box.w, box.d);
  for (const enemy of enemies) { enemy.path = []; enemy.repath = 0; }
}

function applyRoomLighting(id) {
  const config = ROOM_CONFIG[id];
  ambientLight.color.setHex(config.color);
  ambientLight.intensity = config.intensity;
  scene.background.setHex(config.fog);
  scene.fog.color.setHex(config.fog);
}

function updateRooms(dt) {
  const position = player.body.position;
  const room = ROOM_CONFIG.find(room => contains(room, position));
  if (room && (room.id === 0 || doors.find(door => door.roomId === room.id - 1)?.cleared)) {
    if (room.id !== currentRoom) {
      currentRoom = room.id;
      securityAlert = null;
      soundPing = null;
      applyRoomLighting(room.id);
    }
    controllers[room.id].enter();
  }
  for (const controller of controllers) {
    const rewardKey = `${controller.config.id}:${controller.wave}`;
    if (!gameEnded && player.health > 0 && controller.entered && controller.wave > 0
      && controller.alive === 0 && !rewardedWaves.has(rewardKey)) {
      rewardedWaves.add(rewardKey);
      const roomCleared = controller.wave === controller.config.waves.length;
      const previousHealth = player.health;
      player.health = Math.min(100, player.health + (roomCleared ? RECOVERY_CONFIG.roomClearHealth : RECOVERY_CONFIG.waveClearHealth));
      const restored = Math.round(player.health - previousHealth);
      recoveryMessage = roomCleared ? "Room cleared: health restored" : `Wave cleared: +${restored} health`;
      recoveryMessageUntil = clock.elapsedTime + 3;
      playTone(660, 0.18, "sine", 0.035);
    }
    controller.update(dt);
  }
  for (const item of interactables) {
    if (item.type !== "lever") continue;
    const unlocked = controllers[item.roomId].leverUnlocked;
    item.lever.material = unlocked ? mats.active : mats.yellow;
    if (item.roomId === 3) item.mesh.visible = item.lever.visible = unlocked;
  }
}

function spawnWave(room, wave) {
  const spawned = [];
  let index = 0;
  for (const [kind, count] of Object.entries(wave)) for (let n = 0; n < count; n++) {
    const [x, z] = room.spawns[index++ % room.spawns.length];
    const pos = new THREE.Vector3(x, 0, z);
    const stalker = kind === "Stalker", hound = kind === "Hound", flying = kind === "Wasp";
    const posts = combatPosts.filter(post => contains(room, post.cover, 0.8) && contains(room, post.peek, 0.8)
      && canOccupy(post.cover.x, post.cover.z) && canOccupy(post.peek.x, post.peek.z));
    const post = posts.length ? posts[n % posts.length] : { cover: pos, peek: pos };
    const enemy = createEnemy({ kind, roomId: room.id,
      color: mats[kind.toLowerCase()], pos,
      patrol: room.spawns.map(([px, pz]) => new THREE.Vector3(px, 0, pz)),
      cover: stalker ? post.cover.clone() : undefined, peek: stalker ? post.peek.clone() : undefined,
      speed: flying ? 2.7 : 2.1, chaseSpeed: hound ? 6.8 : 4.8,
      range: stalker ? 48 : 25, fov: stalker ? -1 : 0.25,
      damage: stalker ? 10 : hound ? 12 : 8, stalker, hound, flying, shooter: !hound && !flying });
    enemy.group.position.copy(stalker ? post.cover : pos);
    enemies.push(enemy);
    spawned.push(enemy);
  }
  return spawned;
}

function updateRecovery(dt) {
  recovering = false;
  if (gameEnded || player.health <= 0) return;
  const healingTime = Math.max(0, dt - recoveryDelay);
  recoveryDelay = Math.max(0, recoveryDelay - dt);
  if (healingTime > 0 && player.health < 100) {
    player.health = Math.min(100, player.health + healingTime * RECOVERY_CONFIG.healthPerSecond);
    recovering = player.health < 100;
  }
}

function addRobots() {
  enemies.push(createEnemy({ kind: "Hound", color: mats.hound,
    pos: new THREE.Vector3(-14, 0, 22),
    patrol: [new THREE.Vector3(-14, 0, 22), new THREE.Vector3(0, 0, 24), new THREE.Vector3(30, 0, 24)],
    speed: 2.4, chaseSpeed: 6.8, damage: 12, hound: true }));
  enemies.push(createEnemy({
    kind: "Sentinel",
    color: mats.sentinel,
    pos: new THREE.Vector3(-34, 0, 10),
    patrol: [new THREE.Vector3(-34, 0, 22), new THREE.Vector3(-34, 0, -20), new THREE.Vector3(-14, 0, -24), new THREE.Vector3(-14, 0, 22)],
    speed: 2.1,
    chaseSpeed: 5.4,
    range: 25,
    fov: 0.25,
    damage: 10,
    shooter: true
  }));
  enemies.push(createEnemy({
    kind: "Wasp",
    color: mats.wasp,
    pos: new THREE.Vector3(30, 0, -18),
    patrol: [new THREE.Vector3(28, 0, -22), new THREE.Vector3(28, 0, 20), new THREE.Vector3(4, 0, 20), new THREE.Vector3(4, 0, -22)],
    speed: 2.7,
    chaseSpeed: 4.7,
    range: 19,
    fov: 0.8,
    damage: 9,
    flying: true
  }));
  enemies.push(createEnemy({
    kind: "Stalker",
    color: mats.stalker,
    pos: new THREE.Vector3(-28, 0, 13.5),
    cover: new THREE.Vector3(-28, 0, 13.5),
    peek: new THREE.Vector3(-31, 0, 13.5),
    patrol: [new THREE.Vector3(-28, 0, 13.5)],
    speed: 2.6,
    chaseSpeed: 3.1,
    range: 48,
    fov: -1,
    damage: 14,
    stalker: true,
    shooter: true
  }));
  enemies.push(createEnemy({ kind: "Sentinel", color: mats.sentinel,
    pos: new THREE.Vector3(12, 0, 24),
    patrol: [new THREE.Vector3(30, 0, 24), new THREE.Vector3(30, 0, -22), new THREE.Vector3(0, 0, -24), new THREE.Vector3(0, 0, 24)],
    speed: 2.1, chaseSpeed: 5.4, range: 25, fov: 0.25, damage: 10, shooter: true, flanker: true }));
  enemies.push(createEnemy({ kind: "Stalker", color: mats.stalker,
    pos: new THREE.Vector3(26, 0, 4.5), cover: new THREE.Vector3(26, 0, 4.5), peek: new THREE.Vector3(29, 0, 4.5),
    patrol: [new THREE.Vector3(26, 0, 4.5)], speed: 2.6, chaseSpeed: 3.1, range: 48, fov: -1,
    damage: 14, stalker: true, shooter: true }));
}

function createEnemy(config) {
  const group = new THREE.Group();
  const baseH = 0.45;
  const body = new THREE.Mesh(new THREE.BoxGeometry(config.stalker ? 0.75 : 1.25, config.stalker ? 1.2 : 0.9, config.stalker ? 0.75 : 1.25), config.color);
  body.position.y = baseH + 0.45;
  const head = new THREE.Mesh(new THREE.BoxGeometry(config.stalker ? 0.5 : 0.8, 0.42, 0.45), makeMat(0x14191c));
  head.position.set(0, baseH + 1.05, -0.38);
  const eye = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.12, 0.05), makeMat(0xff3131));
  eye.position.set(0, baseH + 1.08, -0.63);
  group.add(body, head, eye);
  if (config.hound) {
    body.scale.set(0.65, 0.55, 1.5);
    body.position.y = 0.65;
    head.position.set(0, 0.8, -1);
    eye.position.set(0, 0.83, -1.25);
    for (const x of [-0.3, 0.3]) for (const z of [-0.55, 0.55]) {
      const leg = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.5, 0.2), config.color);
      leg.position.set(x, 0.25, z);
      group.add(leg);
    }
    for (const x of [-0.24, 0.24]) {
      const ear = new THREE.Mesh(new THREE.ConeGeometry(0.14, 0.35, 4), config.color);
      ear.position.set(x, 1.15, -0.9);
      group.add(ear);
    }
  }
  let muzzle, flash, aimLine;
  if (config.shooter) {
    const gun = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.22, config.stalker ? 1.25 : 0.8), makeMat(0x242b30));
    gun.position.set(0.42, 1.25, -0.45);
    muzzle = new THREE.Object3D();
    muzzle.position.set(0.42, 1.25, config.stalker ? -1.1 : -0.9);
    flash = new THREE.Mesh(new THREE.OctahedronGeometry(0.18), new THREE.MeshBasicMaterial({ color: 0xff7744 }));
    flash.visible = false;
    muzzle.add(flash);
    group.add(gun, muzzle);
    aimLine = new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3()]),
      new THREE.LineBasicMaterial({ color: config.stalker ? 0xff63dd : 0xff5341, transparent: true, opacity: 0.65 }));
    aimLine.visible = false;
    aimLine.raycast = () => {};
    scene.add(aimLine);
  }
  if (config.flying) {
    const rotor = new THREE.Mesh(new THREE.BoxGeometry(2.3, 0.05, 0.18), makeMat(0xe7e1b7));
    rotor.position.y = 1.65;
    group.add(rotor);
    const cone = new THREE.Mesh(new THREE.ConeGeometry(3.2, 9, 18, 1, true), new THREE.MeshBasicMaterial({ color: 0xfff2a4, transparent: true, opacity: 0.18, depthWrite: false }));
    cone.rotation.x = Math.PI;
    cone.position.set(0, 1.5, -4.2);
    cone.raycast = () => {};
    group.add(cone);
    group.userData.rotor = rotor;
  }
  group.position.copy(config.pos);
  group.rotation.y = Math.PI;
  scene.add(group);
  return {
    ...config,
    group,
    state: config.hound ? "wander" : config.stalker ? "hide" : "patrol",
    behavior: config.hound ? new Hound() : config.stalker ? new Ambusher() : new Pursuer(),
    muzzle, flash, aimLine,
    aimTime: 0, flashTimer: 0, coverTimer: 1.5, alerted: false,
    shotTarget: new THREE.Vector3(),
    lastKnown: config.pos.clone(),
    tacticalTimer: 0, searchTimer: 0, searchIndex: 0,
    shotsFired: 0, attackStart: 0,
    navigationStalled: 0, failedPost: null,
    relocateTimer: 0, incomingFire: 0, burstLeft: 0, navGoal: null, distractionTimer: 0,
    patrolIndex: 0,
    path: [],
    pathIndex: 0,
    investigatePoint: null,
    repath: 0,
    lostTimer: 0,
    attackTimer: 0,
    readablePing: 0,
    health: config.kind === "Sentinel" ? 100 : 60,
    maxHealth: config.kind === "Sentinel" ? 100 : 60,
    respawnTimer: null,
    lastAlert: -1,
    spawnCover: config.cover?.clone(),
    spawnPeek: config.peek?.clone()
  };
}

function addRex() {
  const group = new THREE.Group();
  const body = new THREE.Mesh(new THREE.OctahedronGeometry(0.48, 0), mats.rex);
  const eye = new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.12, 0.05), makeMat(0x101214));
  eye.position.set(0, 0.08, -0.43);
  const propeller = new THREE.Mesh(new THREE.BoxGeometry(1.35, 0.04, 0.12), makeMat(0xe8feff));
  propeller.position.y = 0.62;
  group.add(body, eye, propeller);
  scene.add(group);
  rex.mesh = group;
  group.position.set(player.body.position.x + 1.25, 2.2, player.body.position.z + 1.55);
  rex.propeller = propeller;
  rex.light = new THREE.PointLight(0x58d8ff, 0.8, 8);
  group.add(rex.light);
  rex.beacon = new THREE.Mesh(new THREE.OctahedronGeometry(0.25), new THREE.MeshBasicMaterial({ color: 0x58d8ff, transparent: true, opacity: 0.8 }));
  rex.beacon.visible = false;
  rex.beacon.raycast = () => {};
  scene.add(rex.beacon);
}

function setupAudio() {
  if (audio) return;
  const ctx = new AudioContext();
  audio = { ctx, enabled: true, hum: null, alarmOsc: null, alarmGain: null };
  const hum = ctx.createOscillator();
  const humGain = ctx.createGain();
  hum.type = "sawtooth";
  hum.frequency.value = 54;
  humGain.gain.value = 0.018;
  hum.connect(humGain).connect(ctx.destination);
  hum.start();
  audio.hum = humGain;

  const alarmOsc = ctx.createOscillator();
  const alarmGain = ctx.createGain();
  alarmOsc.type = "square";
  alarmOsc.frequency.value = 440;
  alarmGain.gain.value = 0;
  alarmOsc.connect(alarmGain).connect(ctx.destination);
  alarmOsc.start();
  audio.alarmOsc = alarmOsc;
  audio.alarmGain = alarmGain;
}

function playTone(freq, duration = 0.08, type = "square", gainValue = 0.04) {
  if (!audio || !audio.enabled) return;
  const now = audio.ctx.currentTime;
  const osc = audio.ctx.createOscillator();
  const gain = audio.ctx.createGain();
  osc.type = type;
  osc.frequency.setValueAtTime(freq, now);
  gain.gain.setValueAtTime(gainValue, now);
  gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
  osc.connect(gain).connect(audio.ctx.destination);
  osc.start(now);
  osc.stop(now + duration);
}

function updateAudio(time) {
  if (!audio) return;
  audio.enabled = hud.soundToggle.checked;
  const alarmGain = audio.enabled ? THREE.MathUtils.clamp(player.alarm / 100, 0, 1) * 0.055 : 0;
  audio.alarmGain.gain.setTargetAtTime(alarmGain, audio.ctx.currentTime, 0.08);
  audio.alarmOsc.frequency.setTargetAtTime(360 + Math.sin(time * 7) * 140, audio.ctx.currentTime, 0.03);
}

function startGame() {
  setupAudio();
  audio.ctx.resume();
  keys.clear();
  hud.centerPanel.classList.remove("show");
  running = true;
  paused = false;
  lockPointer();
}

function lockPointer() {
  canvas.focus();
  if (document.pointerLockElement !== canvas) canvas.requestPointerLock()?.catch(() => {});
}

function setPaused(value) {
  if (!running || gameEnded) return;
  paused = value;
  keys.clear();
  blaster.setTrigger(false);
  player.body.velocity.set(0, 0, 0);
  hud.pausePanel.classList.toggle("show", paused);
  if (!paused) lockPointer();
  else if (document.pointerLockElement === canvas) document.exitPointerLock();
}

function restart() {
  window.location.reload();
}

function endGame(victory) {
  gameEnded = true;
  keys.clear();
  blaster.setTrigger(false);
  running = false;
  document.exitPointerLock?.();
  hud.endTitle.textContent = victory ? "You Won" : "Lockdown Failed";
  hud.endCopy.textContent = victory ? "All rooms cleared. Warehouse lockdown lifted." : "The warehouse security robots overwhelmed you.";
  hud.endPanel.classList.add("show");
}

function updatePlayer(dt, time) {
  const sprinting = keys.has("ShiftLeft") || keys.has("ShiftRight");
  const crouching = keys.has("KeyC");
  player.crouch = THREE.MathUtils.damp(player.crouch, crouching ? 1 : 0, 12, dt);
  const forward = Number(keys.has("KeyW")) - Number(keys.has("KeyS"));
  const strafe = Number(keys.has("KeyD")) - Number(keys.has("KeyA"));
  const moving = Math.abs(forward) + Math.abs(strafe) > 0;
  let speed = crouching ? 2.5 : 4.8;
  if (sprinting && moving && player.stamina > 4 && !crouching) {
    speed = 8.5;
    player.stamina = Math.max(0, player.stamina - dt * 16);
    if (time - lastFootstep > 0.28) {
      lastFootstep = time;
      soundPing = { x: player.body.position.x, z: player.body.position.z, strength: 18, ttl: 1.2 };
      emitHoundSound("sprint");
      playTone(110, 0.035, "triangle", 0.035);
    }
  } else {
    player.stamina = Math.min(100, player.stamina + dt * (crouching ? 18 : 12));
    if (moving && !crouching && time - lastFootstep > 0.55) {
      lastFootstep = time;
      soundPing = { x: player.body.position.x, z: player.body.position.z, strength: 7, ttl: 0.6 };
      emitHoundSound("walk");
      playTone(88, 0.025, "triangle", 0.018);
    }
    if (moving && crouching && time - lastFootstep > 0.8) {
      lastFootstep = time;
      emitHoundSound("crouch");
    }
  }

  const move = new THREE.Vector3(strafe, 0, -forward);
  if (move.lengthSq() > 0) move.normalize().applyAxisAngle(new THREE.Vector3(0, 1, 0), yaw).multiplyScalar(speed);

  for (const conveyor of conveyors) {
    if (!conveyor.running) continue;
    if (Math.abs(player.body.position.x - conveyor.x) < conveyor.w * 0.5 && Math.abs(player.body.position.z - conveyor.z) < conveyor.d * 0.5) {
      move.addScaledVector(conveyor.dir, 1.45);
    }
  }

  player.body.velocity.x = move.x;
  player.body.velocity.z = move.z;
  player.body.position.y = 1.08;
  player.body.velocity.y = 0;
  player.body.wakeUp();
  world.step(1 / 60, dt, 3);
  camera.position.set(player.body.position.x, 1.72 - player.crouch * 0.45, player.body.position.z);
  camera.rotation.set(pitch, yaw, 0, "YXZ");

  if (soundPing) {
    soundPing.ttl -= dt;
    if (soundPing.ttl <= 0) soundPing = null;
  }
}

function updateInteractions(dt) {
  interactTarget = null;
  const px = player.body.position.x;
  const pz = player.body.position.z;
  let bestDist = 2.35;
  for (const item of interactables) {
    const dist = Math.hypot(px - item.x, pz - item.z);
    if (item.mesh?.visible !== false && item.roomId === currentRoom && dist < bestDist) {
      interactTarget = item;
      bestDist = dist;
    }
  }

  let prompt = "";
  if (interactTarget?.type === "lever") {
    const controller = controllers[interactTarget.roomId];
    prompt = interactTarget.used ? "Opened" : controller.leverUnlocked ? "Press E" : "Locked: clear the room";
    if (!interactTarget.used && controller.leverUnlocked && keys.has("KeyE")) {
      interactTarget.used = true;
      interactTarget.lever.rotation.x = -0.5;
      if (interactTarget.roomId === 3) endGame(true);
      else openRoomDoor(interactTarget.roomId);
    }
  }

  for (const item of interactables) {
    if (item.lock) item.lock = Math.max(0, item.lock - dt);
  }

  hud.prompt.textContent = prompt;
  hud.prompt.classList.toggle("show", Boolean(prompt));
}

function openExitDoor() {
  for (const door of doors) door.open = true;
  for (const door of doors) {
    for (const panel of [door.left, door.right]) {
      const index = blockers.indexOf(panel);
      if (index >= 0) blockers.splice(index, 1);
      if (panel.userData.physics) {
        world.removeBody(panel.userData.physics);
        panel.userData.physics = null;
      }
    }
  }
  playTone(180, 0.12, "sawtooth", 0.07);
  playTone(430, 0.28, "sine", 0.08);
}

function updateDoorAndProps(dt, time) {
  for (const door of doors) {
    if (!door.open) continue;
    door.mesh.position.y = Math.min(7.4, door.mesh.position.y + dt * 4);
    if (door.mesh.userData.physics) {
      door.mesh.userData.physics.position.y = door.mesh.position.y;
      door.mesh.userData.physics.aabbNeedsUpdate = true;
    }
    if (!door.cleared && door.mesh.position.y >= 7.2) {
      door.cleared = true;
      blockers.splice(blockers.indexOf(door.mesh), 1);
      obstacleBoxes.splice(obstacleBoxes.indexOf(door.obstacle), 1);
      world.removeBody(door.mesh.userData.physics);
      door.mesh.userData.physics = null;
      door.marker.material = mats.active;
      rebuildGrid();
    }
  }
  for (const conveyor of conveyors) {
    conveyor.belt.material.color.setHex(conveyor.running ? 0x202326 : 0x121415);
    for (const arrow of conveyor.arrows) {
      if (conveyor.running) arrow.position.addScaledVector(conveyor.dir, dt * 1.9);
      if (conveyor.dir.x && arrow.position.x > conveyor.x + conveyor.w * 0.4) arrow.position.x = conveyor.x - conveyor.w * 0.4;
      if (conveyor.dir.z < 0 && arrow.position.z < conveyor.z - conveyor.d * 0.4) arrow.position.z = conveyor.z + conveyor.d * 0.4;
      if (conveyor.dir.z > 0 && arrow.position.z > conveyor.z + conveyor.d * 0.4) arrow.position.z = conveyor.z - conveyor.d * 0.4;
    }
  }
  scene.traverse((obj) => {
    if (obj.isPointLight && obj.userData.alarm) obj.intensity = 0.25 + (player.alarm / 100) * (1.1 + Math.sin(time * 7) * 0.45);
  });
}

function updateRex(dt) {
  rex.cooldown = Math.max(0, rex.cooldown - dt);
  if (rex.phase === "distract") {
    rex.distractionTime = Math.max(0, rex.distractionTime - dt);
    rex.pulseTimer -= dt;
    if (rex.pulseTimer <= 0) {
      playTone(940, 0.1, "sine", 0.025);
      rex.pulseTimer = 0.8;
    }
    if (rex.distractionTime === 0) {
      rex.phase = "return";
      rex.distractedEnemies.clear();
    }
  }
  const side = new THREE.Vector3(1.25, 1.15, 1.55).applyAxisAngle(new THREE.Vector3(0, 1, 0), yaw);
  rex.target.set(player.body.position.x + side.x, 2.2 - player.crouch * 0.35, player.body.position.z + side.z);
  if (rex.distractionTime > 0) rex.target.set(rex.distractionPoint.x, 2.2, rex.distractionPoint.z);
  rex.mesh.position.lerp(rex.target, 1 - Math.exp(-dt * 4.5));
  if (rex.phase === "outbound" && rex.mesh.position.distanceTo(rex.target) < 0.35) rex.phase = "distract";
  if (rex.phase === "return" && rex.mesh.position.distanceTo(rex.target) < 0.35) rex.phase = "follow";
  rex.mesh.rotation.y += dt * 1.6;
  rex.propeller.rotation.y += dt * 22;
  rex.beacon.visible = rex.distractionTime > 0;
  rex.light.intensity = rex.distractionTime > 0 ? 2 + Math.sin(rex.distractionTime * 18) : 0.8;
  if (rex.distractionTime > 0) {
    rex.beacon.position.set(rex.distractionPoint.x, 0.6, rex.distractionPoint.z);
    rex.beacon.rotation.y += dt * 5;
    rex.beacon.scale.setScalar(1 + Math.sin(rex.distractionTime * 18) * 0.3);
    for (const enemy of rex.distractedEnemies) {
      if (enemy.health > 0 && enemy.distractionTimer > 0) {
        enemy.distractionTimer = rex.distractionTime + (rex.phase === "outbound" ? 1 : 0);
      }
    }
  }

  const pressed = keys.has("KeyQ") && !rex.qHeld;
  rex.qHeld = keys.has("KeyQ");
  if (pressed && rex.cooldown <= 0) {
    const forward = new THREE.Vector3(0, 0, -1).applyAxisAngle(new THREE.Vector3(0, 1, 0), yaw);
    const start = new THREE.Vector3(player.body.position.x, 0, player.body.position.z);
    const desired = start.clone().addScaledVector(forward, 8);
    const cell = nearestWalkable(worldToGrid(desired.x, desired.z));
    if (!cell) return;
    const point = canOccupy(desired.x, desired.z) ? desired : gridToWorld(cell.gx, cell.gz);
    if (!findPath(start, point).length) return;
    rex.cooldown = 16;
    rex.phase = "outbound";
    rex.distractionTime = 6;
    rex.pulseTimer = 0;
    rex.distractedEnemies.clear();
    rex.distractionPoint.copy(point);
    soundPing = { x: point.x, z: point.z, strength: 24, ttl: 2.2 };
    emitHoundSound("distraction", point);
    playTone(940, 0.1, "sine", 0.055);
    playTone(540, 0.18, "triangle", 0.04);
    for (const enemy of enemies) {
      if (enemy.hound) continue;
      if (enemy.health > 0 && contains(ROOM_CONFIG[enemy.roomId], point) && clearLine(enemy.group.position, point)
        && enemy.group.position.distanceTo(point) < 24 && !canSeePlayer(enemy, start)) {
        enemy.state = "investigate";
        enemy.investigatePoint = point.clone();
        enemy.path = [];
        enemy.repath = 0;
        enemy.navGoal = null;
        enemy.distractionTimer = 7;
        rex.distractedEnemies.add(enemy);
        enemy.lastAlert = securityAlert?.revision ?? enemy.lastAlert;
        enemy.aimTime = 0;
        enemy.burstLeft = 0;
        if (enemy.aimLine) enemy.aimLine.visible = false;
      }
    }
  }
}

function updateEnemies(dt, time) {
  const playerPos = new THREE.Vector3(player.body.position.x, 0, player.body.position.z);
  const playerTarget = playerPos.clone();
  playerTarget.y = 1.55 - player.crouch * 0.45;
  scene.updateMatrixWorld(true);
  const ctx = {
    playerPos, playerTarget, noise: soundPing, alarm: player.alarm, combat: enemyCombat,
    alert: securityAlert,
    findPath,
    wanderPoint: enemy => {
      for (let attempt = 0; attempt < 16; attempt++) {
        const point = gridToWorld(Math.floor(Math.random() * GRID_W), Math.floor(Math.random() * GRID_H));
        if (contains(ROOM_CONFIG[enemy.roomId], point, 0.8) && canOccupy(point.x, point.z) && point.distanceTo(enemy.group.position) > 3
          && findPath(enemy.group.position, point, enemy.roomId).length) return point;
      }
      return null;
    },
    bite: enemy => {
      if (gameEnded) return;
      player.health = Math.max(0, player.health - enemy.damage);
      lastDamageAt = clock.elapsedTime;
      recoveryDelay = RECOVERY_CONFIG.delay;
      recovering = false;
      playTone(95, 0.16, "sawtooth", 0.07);
      if (player.health <= 0) endGame(false);
    },
    combatPost: (point, enemy, relocate = false) => pickCombatPost(combatPosts.filter(post => contains(ROOM_CONFIG[enemy.roomId], post.cover, 0.8)), point, enemy, enemies, relocate),
    hiddenAt: (point, threat) => !clearLine(point, threat),
    clearAt: (point, threat) => clearLine(point, threat),
    guerrillaPost: (point, enemy, flee) => {
      const currentRange = enemy.group.position.distanceTo(point);
      const options = combatPosts.filter(post => contains(ROOM_CONFIG[enemy.roomId], post.cover, 0.8) && contains(ROOM_CONFIG[enemy.roomId], post.peek, 0.8)
        && canOccupy(post.cover.x, post.cover.z) && canOccupy(post.peek.x, post.peek.z)
        && post.cover.distanceTo(enemy.cover) > 1 && (!enemy.failedPost || post.cover.distanceTo(enemy.failedPost) > 2)
        && !clearLine(post.cover, point));
      const score = post => {
        const range = post.peek.distanceTo(point);
        return enemy.group.position.distanceTo(post.cover) * 0.65 + Math.abs(range - 24)
          + (!clearLine(post.peek, point) ? 65 : 0) + (range > 46 ? 80 : 0)
          + (flee && range < Math.max(16, currentRange + 4) ? 180 : 0)
          + (enemies.some(other => other !== enemy && other.health > 0 && other.stalker && other.cover.distanceTo(post.cover) < 3) ? 60 : 0);
      };
      options.sort((a, b) => score(a) - score(b));
      return options.slice(0, 12).find(post => findPath(enemy.group.position, post.cover, enemy.roomId).length)
        ?? { cover: enemy.cover.clone(), peek: enemy.peek.clone() };
    },
    escapePoint: (enemy, threat) => {
      const positions = [];
      const minimum = enemy.group.position.distanceTo(threat) + 3;
      for (const radius of [8, 12]) for (let i = 0; i < 8; i++) {
        const angle = i * Math.PI / 4;
        const cell = nearestWalkable(worldToGrid(enemy.group.position.x + Math.cos(angle) * radius, enemy.group.position.z + Math.sin(angle) * radius), ROOM_CONFIG[enemy.roomId]);
        if (!cell) continue;
        const point = gridToWorld(cell.gx, cell.gz);
        if (canOccupy(point.x, point.z) && point.distanceTo(threat) > minimum) positions.push(point);
      }
      positions.sort((a, b) => b.distanceTo(threat) - a.distanceTo(threat));
      return positions.find(point => findPath(enemy.group.position, point, enemy.roomId).length) ?? null;
    },
    playerVelocity: new THREE.Vector3(player.body.velocity.x, 0, player.body.velocity.z),
    searchPoint: enemy => {
      if (enemy.searchIndex === 0) return enemy.lastKnown;
      const angle = (enemy.searchIndex + (enemy.flanker ? 2 : 0)) * Math.PI / 2;
      const candidate = enemy.lastKnown.clone().add(new THREE.Vector3(Math.cos(angle) * 7, 0, Math.sin(angle) * 7));
      const cell = nearestWalkable(worldToGrid(candidate.x, candidate.z), ROOM_CONFIG[enemy.roomId]);
      return cell ? gridToWorld(cell.gx, cell.gz) : enemy.lastKnown;
    },
    combatPosition: enemy => {
      const outward = enemy.group.position.clone().sub(playerPos); outward.y = 0;
      const distance = outward.length(); outward.normalize();
      if (!enemy.shooter || distance > (enemy.flanker ? 18 : 10)) return enemy.lastKnown;
      const side = new THREE.Vector3(-outward.z, 0, outward.x);
      const coordinated = enemy.flanker && enemies.some(other => other !== enemy && other.health > 0 && other.shooter && other.state === "chase");
      const lateral = coordinated ? 6 : (Math.floor(enemy.tacticalTimer / 1.8) % 2 ? 2.5 : -2.5);
      const position = playerPos.clone().addScaledVector(outward, coordinated ? 4 : 6).addScaledVector(side, lateral);
      return canOccupy(position.x, position.z) ? position : enemy.lastKnown;
    },
    canSee: enemy => canSeePlayer(enemy, playerPos),
    distance: (enemy, point) => Math.hypot(enemy.group.position.x - point.x, enemy.group.position.z - point.z),
    raiseAlarm: enemy => {
      player.alarm = Math.min(100, player.alarm + dt * (enemy.flying ? 34 : 20));
      broadcastAlert(playerPos);
      ctx.alert = securityAlert;
    },
    face: (enemy, point, delta) => {
      const dir = point.clone().sub(enemy.group.position);
      enemy.group.rotation.y = dampAngle(enemy.group.rotation.y, Math.atan2(-dir.x, -dir.z), 10, delta);
    },
    navigate: (enemy, goal, speed, delta) => {
      if (!enemy.navGoal || enemy.navGoal.distanceToSquared(goal) > 4) {
        enemy.repath = 0;
        enemy.navGoal = goal.clone();
      }
      enemy.repath -= delta;
      if (enemy.repath <= 0) {
        enemy.path = findPath(enemy.group.position, goal, enemy.roomId);
        enemy.pathIndex = 0;
        enemy.repath = enemy.state === "chase" ? 0.45 : 1;
      }
      if (Math.hypot(enemy.group.position.x - goal.x, enemy.group.position.z - goal.z) < 0.35) { enemy.navigationStalled = 0; return; }
      const before = enemy.group.position.clone();
      followPath(enemy, speed, delta);
      enemy.navigationStalled = enemy.group.position.distanceToSquared(before) < 0.000001 ? (enemy.navigationStalled || 0) + delta : 0;
    }
  };
  for (const enemy of enemies) {
    if (enemy.health <= 0) {
      enemy.group.visible = false;
      if (enemy.aimLine) enemy.aimLine.visible = false;
      if (enemy.flash) enemy.flash.visible = false;
      continue;
    }
    ctx.findPath = (from, to) => findPath(from, to, enemy.roomId);
    ctx.noise = soundPing && contains(ROOM_CONFIG[enemy.roomId], soundPing) && clearLine(enemy.group.position, soundPing) ? soundPing : null;
    ctx.alert = securityAlert && contains(ROOM_CONFIG[enemy.roomId], securityAlert.point) ? securityAlert : null;
    enemy.behavior.update(enemy, dt, ctx);
    if (enemy.group.userData.rotor) enemy.group.userData.rotor.rotation.y += dt * 26;
    enemy.group.position.y = enemy.flying ? 2.2 + Math.sin(time * 2.3) * 0.18 : 0;
    if (enemy.stalker) {
      enemy.readablePing -= dt;
      if (enemy.readablePing <= 0 && ctx.distance(enemy, playerPos) < 18) {
        enemy.readablePing = 2.4;
        playTone(64, 0.11, "sawtooth", 0.018);
      }
    }
  }
  enemyCombat.update(dt, playerTarget);
  if (securityAlert) { securityAlert.ttl -= dt; if (securityAlert.ttl <= 0) securityAlert = null; }
  if (!enemies.some((enemy) => enemy.health > 0 && (enemy.state === "chase" || enemy.aimTime > 0))) {
    player.alarm = Math.max(0, player.alarm - dt * 5.5);
  }
}

function canSeePlayer(enemy, playerPos) {
  if (!contains(ROOM_CONFIG[enemy.roomId], playerPos)) return false;
  const epos = enemy.group.position.clone();
  epos.y += 1.25;
  const target = playerPos.clone();
  target.y = 1.25 - player.crouch * 0.4;
  const toPlayer = target.clone().sub(epos);
  const dist = toPlayer.length();
  const crouchPenalty = player.crouch * 4;
  const alarmBoost = player.alarm > 60 ? 3 : 0;
  if (dist > enemy.range - crouchPenalty + alarmBoost) return false;
  const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(enemy.group.quaternion);
  if (enemy.state !== "chase" && !enemy.alerted && dist > 4 && forward.dot(toPlayer.clone().normalize()) < enemy.fov) return false;
  raycaster.set(epos, toPlayer.normalize());
  raycaster.far = dist;
  return raycaster.intersectObjects(blockers, false).length === 0;
}

function clearLine(from, to) {
  const origin = new THREE.Vector3(from.x, 1.25, from.z);
  const target = new THREE.Vector3(to.x, 1.25, to.z);
  const direction = target.sub(origin);
  raycaster.set(origin, direction.clone().normalize());
  raycaster.far = direction.length();
  return raycaster.intersectObjects(blockers, false).length === 0;
}

function findPath(from, to, roomId = null) {
  const room = roomId === null ? null : ROOM_CONFIG[roomId];
  const goal = new THREE.Vector3(to.x, 0, to.z);
  if (room) {
    goal.x = THREE.MathUtils.clamp(goal.x, room.bounds[0] + 0.8, room.bounds[1] - 0.8);
    goal.z = THREE.MathUtils.clamp(goal.z, room.bounds[2] + 0.8, room.bounds[3] - 0.8);
  }
  const a = nearestWalkable(worldToGrid(from.x, from.z), room);
  const b = nearestWalkable(worldToGrid(goal.x, goal.z), room);
  if (!a || !b) return [];
  const easyStar = new EasyStar.js();
  const grid = gridMatrix.map((row) => row.slice());
  if (room) for (let z = 0; z < GRID_H; z++) for (let x = 0; x < GRID_W; x++) {
    if (!contains(room, gridToWorld(x, z), 0.8)) grid[z][x] = 1;
  }
  easyStar.setGrid(grid);
  easyStar.setAcceptableTiles([0]);
  easyStar.enableDiagonals();
  easyStar.disableCornerCutting();
  easyStar.enableSync();
  let raw = null;
  easyStar.findPath(a.gx, a.gz, b.gx, b.gz, (path) => {
    raw = path;
  });
  easyStar.calculate();
  if (!raw || !raw.length) return [];
  const points = raw.slice(1).map((node) => gridToWorld(node.x, node.y));
  if (canOccupy(goal.x, goal.z)) points.push(goal);
  return points;
}

function nearestWalkable(point, room = null) {
  for (let radius = 0; radius < 5; radius++) {
    for (let z = -radius; z <= radius; z++) for (let x = -radius; x <= radius; x++) {
      const cell = { gx: point.gx + x, gz: point.gz + z };
      if (insideGrid(cell) && gridMatrix[cell.gz][cell.gx] === 0 && (!room || contains(room, gridToWorld(cell.gx, cell.gz), 0.8))) return cell;
    }
  }
  return null;
}

function canOccupy(x, z) {
  if (Math.abs(x) > 36.7 || Math.abs(z) > 28.7) return false;
  return !obstacleBoxes.some(box => Math.abs(x - box.x) < box.w / 2 + 0.5 && Math.abs(z - box.z) < box.d / 2 + 0.5);
}

function insideGrid(p) {
  return p.gx >= 0 && p.gz >= 0 && p.gx < GRID_W && p.gz < GRID_H;
}

function followPath(enemy, speed, dt) {
  if (!enemy.path.length) return;
  const epos = enemy.group.position;
  const target = enemy.path[Math.min(enemy.pathIndex, enemy.path.length - 1)];
  const dir = tmpVec.subVectors(target, epos);
  dir.y = 0;
  if (dir.length() < 0.35) {
    enemy.pathIndex++;
    if (enemy.pathIndex >= enemy.path.length) enemy.path = [];
    return;
  }
  dir.normalize();
  const step = Math.min(speed * dt, Math.hypot(target.x - epos.x, target.z - epos.z));
  const nx = epos.x + dir.x * step;
  const nz = epos.z + dir.z * step;
  const room = ROOM_CONFIG[enemy.roomId];
  const valid = (x, z) => contains(room, { x, z }, 0.8) && canOccupy(x, z);
  if (valid(nx, nz)) epos.set(nx, epos.y, nz);
  else if (valid(nx, epos.z)) epos.x = nx;
  else if (valid(epos.x, nz)) epos.z = nz;
  else enemy.repath = 0;
  const targetYaw = Math.atan2(-dir.x, -dir.z);
  enemy.group.rotation.y = dampAngle(enemy.group.rotation.y, targetYaw, 8, dt);
}

function dampAngle(current, target, lambda, dt) {
  const delta = Math.atan2(Math.sin(target - current), Math.cos(target - current));
  return current + delta * (1 - Math.exp(-lambda * dt));
}

function updateHud(time) {
  const health = THREE.MathUtils.clamp(player.health, 0, 100);
  hud.healthFill.style.width = `${health}%`;
  hud.healthText.textContent = `${Math.ceil(health)}`;
  hud.healthFill.classList.toggle("recovering", recovering);
  hud.recoveryText.textContent = recovering ? "Recovering" : time < recoveryMessageUntil ? recoveryMessage : "";
  hud.recoveryText.hidden = !hud.recoveryText.textContent;
  hud.staminaFill.style.width = `${Math.ceil(player.stamina)}%`;
  hud.staminaText.textContent = `${Math.ceil(player.stamina)}`;
  hud.alarmFill.style.width = `${Math.ceil(player.alarm)}%`;
  hud.alarmText.textContent = `${Math.ceil(player.alarm)}`;
  const controller = controllers[currentRoom];
  hud.terminalText.textContent = currentRoom === 0 ? "Start room" : `Room ${currentRoom} - Wave ${controller.wave}/${controller.config.waves.length}`;
  hud.debug.textContent = `Room ${currentRoom} | Wave ${controller.wave}/${controller.config.waves.length} | Alive ${controller.alive}`
    + (controller.wait !== null ? ` | Next wave ${controller.wait.toFixed(1)}s` : "");
  hud.rexText.textContent = rex.phase === "outbound" ? "Rex deploying"
    : rex.phase === "distract" ? `Rex distracting ${Math.ceil(rex.distractionTime)}s`
    : rex.cooldown <= 0 ? "Rex ready" : `Rex ${Math.ceil(rex.cooldown)}s`;
  hud.damage.style.opacity = Math.max(0, 1 - (time - lastDamageAt) * 3);
}

function animate() {
  requestAnimationFrame(animate);
  const dt = Math.min(clock.getDelta(), 0.05);
  const time = clock.elapsedTime;
  if (running && !paused && !gameEnded) {
    updatePlayer(dt, time);
    updateRooms(dt);
    blaster.update(dt);
    updateInteractions(dt);
    updateRex(dt);
    updateEnemies(dt, time);
    updateRecovery(dt);
    updateDoorAndProps(dt, time);
    updateAudio(time);
    updateHud(time);
  }
  renderer.render(scene, camera);
}

function bindEvents() {
  document.querySelector("#startButton").addEventListener("click", startGame);
  document.querySelector("#resumeButton").addEventListener("click", () => setPaused(false));
  document.querySelector("#restartButton").addEventListener("click", restart);
  document.querySelector("#playAgainButton").addEventListener("click", restart);
  document.addEventListener("keydown", (event) => {
    if (["KeyW", "KeyA", "KeyS", "KeyD", "KeyC", "KeyE", "KeyQ", "ShiftLeft", "ShiftRight", "Space"].includes(event.code) && running && !paused) event.preventDefault();
    if (!running || paused || gameEnded) return;
    keys.add(event.code);
    if (event.code === "Escape" && running) setPaused(true);
  });
  document.addEventListener("keyup", (event) => keys.delete(event.code));
  canvas.addEventListener("mousedown", (event) => {
    if (event.button !== 0 || !running || paused || gameEnded) return;
    if (document.pointerLockElement !== canvas) { lockPointer(); return; }
    blaster.setTrigger(true);
  });
  document.addEventListener("mouseup", () => blaster.setTrigger(false));
  window.addEventListener("blur", () => setPaused(true));
  document.addEventListener("visibilitychange", () => { if (document.hidden) setPaused(true); });
  document.addEventListener("pointerlockerror", () => setPaused(true));
  document.addEventListener("mousemove", (event) => {
    if (document.pointerLockElement !== canvas || paused || gameEnded) return;
    yaw -= event.movementX * 0.0022;
    pitch -= event.movementY * 0.0022;
    pitch = THREE.MathUtils.clamp(pitch, -1.45, 1.45);
  });
  document.addEventListener("pointerlockchange", () => {
    if (running && !gameEnded && document.pointerLockElement !== canvas) setPaused(true);
  });
  window.addEventListener("resize", () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  });
}

buildWarehouse();
camera.position.set(player.body.position.x, 1.72, player.body.position.z);
camera.rotation.set(pitch, yaw, 0, "YXZ");
scene.updateMatrixWorld(true);
bindEvents();
updateHud(0);
animate();
