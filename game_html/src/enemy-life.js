export const RESPAWN_DELAY = 8;

export function updateRespawn(enemy, dt, { canSpawn, alert }) {
  if (enemy.health > 0) return false;
  enemy.group.visible = false;
  if (enemy.aimLine) enemy.aimLine.visible = false;
  if (enemy.flash) enemy.flash.visible = false;
  enemy.respawnTimer = Math.max(0, (enemy.respawnTimer ?? RESPAWN_DELAY) - dt);
  if (enemy.respawnTimer > 0) return true;
  const candidates = enemy.stalker ? [enemy.spawnCover] : [enemy.pos, ...enemy.patrol];
  const spawn = candidates.find(canSpawn);
  if (!spawn) { enemy.respawnTimer = 1; return true; }
  enemy.group.position.copy(spawn);
  enemy.group.rotation.set(0, Math.PI, 0);
  enemy.group.visible = true;
  enemy.health = enemy.maxHealth;
  enemy.state = enemy.hound ? "wander" : enemy.stalker ? "hide" : "patrol";
  if (enemy.hound) enemy.behavior.reset();
  enemy.path = [];
  enemy.pathIndex = 0;
  enemy.repath = 0;
  enemy.lostTimer = 0;
  enemy.attackTimer = 1;
  enemy.aimTime = 0;
  enemy.flashTimer = 0;
  enemy.coverTimer = 1.5;
  enemy.alerted = false;
  enemy.lastAlert = -1;
  enemy.searchTimer = 0;
  enemy.searchIndex = 0;
  enemy.tacticalTimer = 0;
  enemy.relocateTimer = 0;
  enemy.incomingFire = 0;
  enemy.burstLeft = 0;
  enemy.shotsFired = 0;
  enemy.attackStart = 0;
  enemy.navigationStalled = 0;
  enemy.failedPost = null;
  enemy.distractionTimer = 0;
  enemy.navGoal = null;
  enemy.investigatePoint = null;
  enemy.lastKnown.copy(spawn);
  if (enemy.stalker) {
    enemy.cover.copy(enemy.spawnCover);
    enemy.peek.copy(enemy.spawnPeek);
  }
  enemy.respawnTimer = null;
  if (alert) enemy.lastKnown.copy(alert.point);
  return false;
}

export function pickCombatPost(posts, point, enemy, enemies = [], relocate = false) {
  const score = post => post.peek.distanceToSquared(point)
    + (relocate && enemy?.cover.distanceTo(post.cover) < 1 ? 1200 : 0)
    + (enemies.some(other => other !== enemy && other.health > 0 && other.stalker && other.cover.distanceTo(post.cover) < 1) ? 500 : 0);
  return posts.reduce((best, post) => score(post) < score(best) ? post : best);
}
