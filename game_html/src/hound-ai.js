export const HOUND_SOUND = {
  gunshot: { loudness: 12, radius: 48 },
  sprint: { loudness: 5, radius: 30 },
  walk: { loudness: 0.8, radius: 10 },
  crouch: { loudness: 0.08, radius: 3 },
  distraction: { loudness: 6, radius: 24 }
};

const TRACK_THRESHOLD = 1.2;
const MEMORY_FLOOR = 0.08;

export class Hound {
  constructor() { this.reset(); }

  reset() {
    this.sounds = [];
    this.target = null;
    this.wanderGoal = null;
    this.rescoreTimer = 0;
    this.sniffTimer = 0;
    this.biteTimer = 1;
  }

  hear(enemy, point, kind) {
    const sound = HOUND_SOUND[kind];
    if (!sound || enemy.health <= 0 || enemy.group.position.distanceTo(point) > sound.radius) return;
    this.sounds.push({ point: point.clone(), loudness: sound.loudness, age: 0 });
    if (this.sounds.length > 64) this.sounds.shift();
  }

  score(enemy, sound) {
    return sound.loudness * Math.exp(-sound.age * 0.55)
      / (1 + enemy.group.position.distanceTo(sound.point) / 5);
  }

  forget(sound) {
    this.sounds = this.sounds.filter(item => item !== sound);
    if (this.target === sound) this.target = null;
  }

  sniff(enemy) {
    enemy.state = "sniff";
    enemy.path = [];
    enemy.navGoal = null;
    this.sniffTimer = 3;
  }

  chooseSound(enemy, ctx) {
    this.sounds = this.sounds.filter(sound => this.score(enemy, sound) >= MEMORY_FLOOR);
    if (this.target && !this.sounds.includes(this.target)) this.target = null;
    const currentScore = this.target ? this.score(enemy, this.target) : 0;
    const candidates = this.sounds.slice().sort((a, b) => this.score(enemy, b) - this.score(enemy, a));
    for (const sound of candidates) {
      const score = this.score(enemy, sound);
      if (score < TRACK_THRESHOLD) break;
      if (sound === this.target || (this.target && score < currentScore * 1.3)) continue;
      if (!ctx.findPath(enemy.group.position, sound.point).length) { this.forget(sound); continue; }
      this.target = sound;
      enemy.state = "track";
      enemy.path = [];
      enemy.repath = 0;
      enemy.navGoal = null;
      enemy.navigationStalled = 0;
      return;
    }
    if (enemy.state === "track" && (!this.target || currentScore < TRACK_THRESHOLD)) this.sniff(enemy);
  }

  update(enemy, dt, ctx) {
    for (const sound of this.sounds) sound.age += dt;
    this.biteTimer = Math.max(0, this.biteTimer - dt);
    this.rescoreTimer -= dt;
    if (this.rescoreTimer <= 0) {
      this.rescoreTimer = 0.5;
      this.chooseSound(enemy, ctx);
    }
    if (!["wander", "track", "sniff"].includes(enemy.state)) enemy.state = "wander";
    if (enemy.state === "track") {
      if (!this.target) this.sniff(enemy);
      else if (ctx.distance(enemy, this.target.point) < 0.9) this.sniff(enemy);
      else {
        ctx.navigate(enemy, this.target.point, enemy.chaseSpeed, dt);
        if (!enemy.path.length || enemy.navigationStalled > 0.8) {
          this.forget(this.target);
          this.sniff(enemy);
          this.rescoreTimer = 0;
        }
      }
    } else if (enemy.state === "sniff") {
      enemy.group.rotation.y += dt * Math.PI * 0.8;
      this.sniffTimer -= dt;
      if (this.sniffTimer <= 0) {
        this.forget(this.target);
        enemy.state = "wander";
        this.wanderGoal = null;
      }
    } else {
      if (!this.wanderGoal || ctx.distance(enemy, this.wanderGoal) < 1 || enemy.navigationStalled > 0.8) {
        this.wanderGoal = ctx.wanderPoint(enemy);
        enemy.path = []; enemy.repath = 0; enemy.navGoal = null;
        enemy.navigationStalled = 0;
      }
      if (this.wanderGoal) ctx.navigate(enemy, this.wanderGoal, enemy.speed, dt);
    }
    // Proximity permits a bite, but never supplies a distant pursuit target.
    if (enemy.state !== "wander" && ctx.playerPos && this.biteTimer === 0
      && ctx.distance(enemy, ctx.playerPos) < 1.6 && ctx.clearAt(enemy.group.position, ctx.playerPos)) {
      ctx.bite(enemy);
      this.biteTimer = 1.2;
    }
  }
}
