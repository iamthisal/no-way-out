import test from "node:test";
import assert from "node:assert/strict";
import * as THREE from "three";
import { Hound } from "../src/hound-ai.js";
import { updateRespawn } from "../src/enemy-life.js";

function setup() {
  const behavior = new Hound();
  const enemy = { behavior, hound: true, health: 60, maxHealth: 60, state: "wander",
    group: new THREE.Group(), pos: new THREE.Vector3(), patrol: [new THREE.Vector3(8, 0, 0)],
    lastKnown: new THREE.Vector3(), path: [], speed: 2.4, chaseSpeed: 6.8, navigationStalled: 0 };
  let bites = 0;
  const ctx = {
    playerPos: new THREE.Vector3(20, 0, 0),
    distance: (agent, point) => agent.group.position.distanceTo(point),
    findPath: (from, to) => [to.clone()],
    navigate: (agent, goal) => { agent.path = [goal.clone()]; },
    wanderPoint: () => new THREE.Vector3(8, 0, 0),
    clearAt: () => true,
    bite: () => { bites++; }
  };
  return { behavior, enemy, ctx, bites: () => bites };
}

test("sound utility favors loud, fresh and close sounds", () => {
  const { behavior, enemy } = setup();
  const sound = { point: new THREE.Vector3(5, 0, 0), loudness: 5, age: 0 };
  const score = behavior.score(enemy, sound);
  assert.ok(behavior.score(enemy, { ...sound, loudness: 12 }) > score);
  assert.ok(behavior.score(enemy, { ...sound, age: 3 }) < score);
  assert.ok(behavior.score(enemy, { ...sound, point: new THREE.Vector3(15, 0, 0) }) < score);
});

test("gunshots track; walking and crouching cannot refresh a loud trail", () => {
  const { behavior, enemy, ctx } = setup();
  behavior.hear(enemy, new THREE.Vector3(8, 0, 0), "gunshot");
  behavior.update(enemy, 0.1, ctx);
  assert.equal(enemy.state, "track");
  for (let i = 0; i < 120; i++) {
    behavior.hear(enemy, new THREE.Vector3(1, 0, 0), i % 2 ? "walk" : "crouch");
    behavior.update(enemy, 0.1, ctx);
  }
  assert.equal(enemy.state, "wander");
  assert.equal(behavior.target, null);
});

test("rescoring is throttled and requires a 30 percent improvement", () => {
  const { behavior, enemy, ctx } = setup();
  behavior.hear(enemy, new THREE.Vector3(5, 0, 0), "sprint");
  behavior.update(enemy, 0.01, ctx);
  const original = behavior.target;
  behavior.hear(enemy, new THREE.Vector3(4.9, 0, 0), "sprint");
  behavior.chooseSound(enemy, ctx);
  assert.equal(behavior.target, original);
  behavior.hear(enemy, new THREE.Vector3(4, 0, 0), "gunshot");
  behavior.update(enemy, 0.1, ctx);
  assert.equal(behavior.target, original);
  behavior.update(enemy, 0.5, ctx);
  assert.notEqual(behavior.target, original);
});

test("unreachable sounds are dropped in favor of another reachable sound", () => {
  const { behavior, enemy, ctx } = setup();
  ctx.findPath = (from, to) => to.x === 2 ? [] : [to.clone()];
  behavior.hear(enemy, new THREE.Vector3(2, 0, 0), "gunshot");
  behavior.hear(enemy, new THREE.Vector3(5, 0, 0), "sprint");
  behavior.update(enemy, 0.1, ctx);
  assert.equal(behavior.target.point.x, 5);
  assert.equal(behavior.sounds.length, 1);
});

test("arrival sniffs for three seconds, then resumes wandering", () => {
  const { behavior, enemy, ctx } = setup();
  behavior.hear(enemy, new THREE.Vector3(0.5, 0, 0), "sprint");
  behavior.update(enemy, 0.1, ctx);
  assert.equal(enemy.state, "sniff");
  behavior.update(enemy, 2.9, ctx);
  assert.equal(enemy.state, "sniff");
  assert.notEqual(enemy.group.rotation.y, 0);
  behavior.update(enemy, 0.11, ctx);
  assert.equal(enemy.state, "wander");
});

test("blocked movement drops its sound and selects another", () => {
  const { behavior, enemy, ctx } = setup();
  behavior.hear(enemy, new THREE.Vector3(2, 0, 0), "gunshot");
  behavior.hear(enemy, new THREE.Vector3(5, 0, 0), "sprint");
  behavior.update(enemy, 0.1, ctx);
  enemy.navigationStalled = 1;
  behavior.update(enemy, 0.1, ctx);
  assert.equal(behavior.target, null);
  enemy.navigationStalled = 0;
  behavior.update(enemy, 0.1, ctx);
  assert.equal(behavior.target.point.x, 5);
});

test("bites require close range, clear line and a cooldown", () => {
  const { behavior, enemy, ctx, bites } = setup();
  ctx.playerPos.set(1, 0, 0);
  behavior.hear(enemy, new THREE.Vector3(1, 0, 0), "sprint");
  ctx.clearAt = () => false;
  behavior.update(enemy, 1, ctx);
  assert.equal(bites(), 0);
  ctx.clearAt = () => true;
  behavior.update(enemy, 0.1, ctx);
  assert.equal(bites(), 1);
  behavior.update(enemy, 0.1, ctx);
  assert.equal(bites(), 1);
  ctx.playerPos.set(20, 0, 0);
  behavior.update(enemy, 1.3, ctx);
  assert.equal(bites(), 1);
});

test("memory stays bounded, expires, and respawn clears previous sounds", () => {
  const { behavior, enemy, ctx } = setup();
  for (let i = 0; i < 200; i++) behavior.hear(enemy, new THREE.Vector3(5, 0, 0), "sprint");
  assert.equal(behavior.sounds.length, 64);
  behavior.update(enemy, 20, ctx);
  assert.equal(behavior.sounds.length, 0);
  behavior.hear(enemy, new THREE.Vector3(5, 0, 0), "gunshot");
  enemy.health = 0;
  enemy.respawnTimer = 0;
  updateRespawn(enemy, 0.1, { canSpawn: () => true, alert: null });
  assert.equal(enemy.state, "wander");
  assert.equal(enemy.health, 60);
  assert.equal(behavior.sounds.length, 0);
  assert.equal(behavior.target, null);
});
