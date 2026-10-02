export class RoomController {
  constructor(config, spawn) {
    this.config = config;
    this.spawn = spawn;
    this.wave = 0;
    this.enemies = [];
    this.entered = false;
    this.leverUnlocked = config.waves.length === 0;
    this.wait = null;
  }

  get alive() { return this.enemies.filter(enemy => enemy.health > 0).length; }

  enter() {
    if (this.entered) return;
    this.entered = true;
    if (!this.leverUnlocked) this.nextWave();
  }

  nextWave() {
    this.enemies = this.spawn(this.config, this.config.waves[this.wave]);
    this.wave++;
    this.wait = null;
  }

  update(dt) {
    if (!this.entered || this.leverUnlocked || this.alive > 0) return;
    if (this.wave === this.config.waves.length) {
      this.leverUnlocked = true;
      return;
    }
    if (this.wait === null) this.wait = this.config.pause;
    this.wait = Math.max(0, this.wait - dt);
    if (this.wait === 0) this.nextWave();
  }
}
