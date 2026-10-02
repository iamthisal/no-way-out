import assert from "node:assert/strict";
import { RoomController } from "../src/room-controller.js";
import { ROOM_CONFIG } from "../src/room-config.js";

for (const config of ROOM_CONFIG) {
  let spawns = 0;
  const room = new RoomController(config, (_, wave) => {
    spawns++;
    return Array.from({ length: Object.values(wave).reduce((a, b) => a + b, 0) }, () => ({ health: 100 }));
  });
  room.update(20);
  assert.equal(spawns, 0);
  room.enter();
  room.enter();
  assert.equal(spawns, config.id === 0 ? 0 : 1);
  if (config.id === 0) { assert.equal(room.leverUnlocked, true); continue; }
  for (let wave = 1; wave <= config.waves.length; wave++) {
    assert.equal(room.wave, wave);
    room.update(20);
    assert.equal(room.wave, wave, "Living enemies block progression");
    room.enemies.forEach(enemy => { enemy.health = 0; });
    room.update(0);
    if (wave < config.waves.length) {
      room.update(config.pause - 0.1);
      assert.equal(room.wave, wave, "Pause before next wave");
      room.update(0.11);
      assert.equal(room.leverUnlocked, false);
    }
  }
  assert.equal(room.leverUnlocked, true);
  room.update(100);
  assert.equal(spawns, config.waves.length);
}
console.log("Room lifecycle checks passed for all four rooms.");
