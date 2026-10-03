import * as THREE from "three";

export class Pursuer {
  update(enemy, dt, ctx) {
    const sees = ctx.canSee(enemy);
    if (followDistraction(enemy, dt, ctx, sees)) return;
    enemy.lostTimer += dt;
    enemy.tacticalTimer += dt;
    if (sees) {
      if (enemy.state !== "chase") enemy.path = [];
      enemy.state = "chase";
      enemy.lastKnown.copy(ctx.playerPos);
      enemy.alerted = true;
      enemy.lostTimer = 0;
      ctx.raiseAlarm(enemy);
    } else if (ctx.alert && !["chase", "search"].includes(enemy.state) && enemy.lastAlert !== ctx.alert.revision) {
      enemy.lastAlert = ctx.alert.revision;
      enemy.alerted = true;
      enemy.state = "investigate";
      enemy.investigatePoint = ctx.alert.point.clone();
      enemy.path = [];
      enemy.repath = 0;
    } else if (ctx.noise && !["chase", "search"].includes(enemy.state) && ctx.distance(enemy, ctx.noise) < ctx.noise.strength) {
      enemy.state = "investigate";
      enemy.investigatePoint = new THREE.Vector3(ctx.noise.x, 0, ctx.noise.z);
    } else if (enemy.state === "chase" && enemy.lostTimer > 2) {
      beginSearch(enemy, enemy.lastKnown);
    }
    if (enemy.state === "search") {
      enemy.searchTimer -= dt;
      if (enemy.searchTimer <= 0) { enemy.state = "return"; enemy.path = []; enemy.repath = 0; }
    }
    const goal = enemy.state === "chase" ? (sees ? ctx.combatPosition(enemy) : enemy.lastKnown)
      : enemy.state === "search" ? ctx.searchPoint(enemy)
      : enemy.state === "investigate" ? enemy.investigatePoint : enemy.patrol[enemy.patrolIndex];
    const distance = ctx.distance(enemy, ctx.playerPos);
    ctx.navigate(enemy, goal, ["chase", "search"].includes(enemy.state) || (enemy.state === "investigate" && enemy.alerted) ? enemy.chaseSpeed : enemy.speed, dt);
    if (sees) ctx.face(enemy, ctx.playerPos, dt);
    if (ctx.distance(enemy, goal) < 1.1) {
      if (enemy.state === "patrol") enemy.patrolIndex = (enemy.patrolIndex + 1) % enemy.patrol.length;
      if (enemy.state === "investigate") beginSearch(enemy, enemy.investigatePoint);
      else if (enemy.state === "search") { enemy.searchIndex++; enemy.path = []; enemy.repath = 0; }
      else if (enemy.state === "return") enemy.state = "patrol";
    }
    ctx.combat.aimAndFire(enemy, sees && distance < 26, dt, ctx.playerTarget, ctx.playerVelocity);
  }
}

function beginSearch(enemy, point) {
  enemy.state = "search";
  enemy.lastKnown.copy(point);
  enemy.searchTimer = 14;
  enemy.searchIndex = 0;
  enemy.path = [];
  enemy.repath = 0;
}

function followDistraction(enemy, dt, ctx, sees) {
  enemy.distractionTimer = Math.max(0, (enemy.distractionTimer || 0) - dt);
  if (sees) { enemy.distractionTimer = 0; return false; }
  if (enemy.distractionTimer <= 0) return false;
  enemy.state = "investigate";
  ctx.navigate(enemy, enemy.investigatePoint, enemy.speed, dt);
  ctx.face(enemy, enemy.investigatePoint, dt);
  ctx.combat.aimAndFire(enemy, false, dt, ctx.playerTarget);
  return true;
}

export class Ambusher {
  update(enemy, dt, ctx) {
    const sees = ctx.canSee(enemy);
    if (followDistraction(enemy, dt, ctx, sees)) return;
    enemy.relocateTimer = Math.max(0, enemy.relocateTimer - dt);
    enemy.lostTimer += dt;
    const firstContact = sees && !enemy.alerted;
    if (sees) {
      enemy.lastKnown.copy(ctx.playerPos);
      enemy.lostTimer = 0;
      enemy.alerted = true;
      ctx.raiseAlarm(enemy);
      enemy.lastAlert = ctx.alert?.revision ?? enemy.lastAlert;
    } else if (ctx.alert && enemy.lastAlert !== ctx.alert.revision) {
      enemy.lastAlert = ctx.alert.revision;
      enemy.alerted = true;
      enemy.lastKnown.copy(ctx.alert.point);
      if (enemy.state === "hide" && enemy.relocateTimer <= 0 && (!ctx.hiddenAt(enemy.cover, enemy.lastKnown) || !ctx.clearAt(enemy.peek, enemy.lastKnown))) this.reposition(enemy, ctx, false);
    }
    const distance = ctx.distance(enemy, enemy.lastKnown);
    const closing = sees ? ctx.playerVelocity.dot(enemy.group.position.clone().sub(ctx.playerPos).normalize()) : 0;
    const committedAttack = enemy.state === "peek" && (enemy.aimTime > 0 || enemy.burstLeft > 0);
    const nearby = ctx.distance(enemy, ctx.playerPos) < 5;
    if (nearby) enemy.lastKnown.copy(ctx.playerPos);
    if ((enemy.incomingFire > 0 || nearby || (sees && (distance < 7 || (distance < 13 && closing > 2 && !committedAttack))))
      && (enemy.state !== "retreat" || enemy.relocateTimer <= 0)) {
      enemy.incomingFire = 0;
      this.reposition(enemy, ctx, true);
    } else if (firstContact && enemy.state !== "retreat" && !ctx.hiddenAt(enemy.cover, enemy.lastKnown)) {
      this.reposition(enemy, ctx, false);
      // Take an opening burst before withdrawing from an exposed position.
      enemy.peek.copy(enemy.group.position);
      enemy.state = "peek";
      enemy.coverTimer = 3;
      enemy.attackStart = enemy.shotsFired;
    }
    if (enemy.state === "investigate") {
      ctx.navigate(enemy, enemy.investigatePoint, enemy.speed, dt);
      ctx.combat.aimAndFire(enemy, false, dt, ctx.playerTarget);
      if (ctx.distance(enemy, enemy.investigatePoint) < 1.2) this.reposition(enemy, ctx, false);
      return;
    }
    if (!["hide", "peek", "retreat"].includes(enemy.state)) enemy.state = "hide";
    if (enemy.navigationStalled > 0.8) {
      enemy.failedPost = (enemy.state === "peek" ? enemy.peek : enemy.cover).clone();
      enemy.navigationStalled = 0;
      this.reposition(enemy, ctx, enemy.state === "retreat" || nearby);
    }
    const goal = enemy.state === "peek" ? enemy.peek : enemy.cover;
    ctx.navigate(enemy, goal, enemy.state === "retreat" ? 7.7 : 4.2, dt);
    const arrived = ctx.distance(enemy, goal) < 0.45;
    if (enemy.state === "retreat") {
      ctx.combat.aimAndFire(enemy, false, dt, ctx.playerTarget);
      if (arrived) { enemy.state = "hide"; enemy.coverTimer = 1.6; }
      return;
    }
    // Hiding and firing windows count only after the robot reaches its post.
    if (arrived) enemy.coverTimer -= dt;
    if (enemy.state === "hide") {
      ctx.combat.aimAndFire(enemy, false, dt, ctx.playerTarget);
      if (arrived && enemy.coverTimer <= 0) {
        enemy.state = "peek"; enemy.coverTimer = 3;
        enemy.attackStart = enemy.shotsFired;
        enemy.path = []; enemy.repath = 0;
      }
      return;
    }
    ctx.face(enemy, enemy.alerted ? enemy.lastKnown : ctx.playerPos, dt);
    const clearShot = ctx.canSee(enemy);
    ctx.combat.aimAndFire(enemy, clearShot, dt, ctx.playerTarget, ctx.playerVelocity);
    if (enemy.shotsFired - enemy.attackStart >= 2) {
      enemy.state = "hide";
      enemy.coverTimer = 1.6; enemy.path = []; enemy.repath = 0;
      ctx.combat.aimAndFire(enemy, false, 0, ctx.playerTarget);
    } else if (arrived && enemy.coverTimer <= 0) {
      if (enemy.alerted && !clearShot) this.reposition(enemy, ctx, false);
      else { enemy.state = "hide"; enemy.coverTimer = 1; enemy.path = []; enemy.repath = 0; }
    }
  }

  reposition(enemy, ctx, flee) {
    let post = ctx.guerrillaPost(enemy.lastKnown, enemy, flee);
    if (flee && (post.cover.distanceTo(enemy.group.position) < 2 || post.cover.distanceTo(enemy.lastKnown) < enemy.group.position.distanceTo(enemy.lastKnown) + 3)) {
      const escape = ctx.escapePoint(enemy, enemy.lastKnown);
      if (escape) post = { cover: escape, peek: escape.clone() };
    }
    enemy.cover.copy(post.cover); enemy.peek.copy(post.peek);
    enemy.state = flee ? "retreat" : "hide";
    enemy.coverTimer = 1;
    enemy.relocateTimer = 2.5;
    enemy.path = []; enemy.repath = 0; enemy.navGoal = null;
    enemy.attackStart = enemy.shotsFired;
    ctx.combat.aimAndFire(enemy, false, 0, ctx.playerTarget);
  }
}

export function createEnemyCombat({ scene, blockers, playTone, damagePlayer }) {
  const bolts = [];
  const ray = new THREE.Raycaster();
  const sphere = new THREE.Sphere();
  const boltGeometry = new THREE.SphereGeometry(0.12, 6, 4);
  const boltMaterial = new THREE.MeshBasicMaterial({ color: 0xff613d });

  function muzzle(enemy) {
    enemy.group.updateWorldMatrix(true, true);
    return enemy.muzzle.getWorldPosition(new THREE.Vector3());
  }

  return {
    bolts,
    aimAndFire(enemy, canFire, dt, target, velocity = new THREE.Vector3()) {
      if (!enemy.muzzle) return;
      enemy.attackTimer = Math.max(0, enemy.attackTimer - dt);
      enemy.flashTimer = Math.max(0, enemy.flashTimer - dt);
      enemy.flash.visible = enemy.flashTimer > 0;
      if (!canFire || enemy.attackTimer > 0) {
        enemy.aimTime = 0;
        enemy.aimLine.visible = false;
        if (!canFire) enemy.burstLeft = 0;
        return;
      }
      const origin = muzzle(enemy);
      if (enemy.aimTime === 0) {
        if (!enemy.burstLeft) enemy.burstLeft = enemy.stalker ? 2 : 3;
        enemy.shotTarget.copy(target);
        playTone(enemy.stalker ? 850 : 530, 0.12, "sine", 0.025);
      }
      enemy.aimTime += dt;
      const warning = enemy.burstLeft === (enemy.stalker ? 2 : 3) ? (enemy.stalker ? 0.55 : 0.45) : 0.2;
      // Track during the warning, then lock aim briefly so a last-second dodge works.
      if (enemy.aimTime < warning - 0.12) {
        enemy.shotTarget.copy(target).addScaledVector(velocity, Math.min(0.4, origin.distanceTo(target) / 26));
      }
      enemy.aimLine.visible = true;
      enemy.aimLine.geometry.setFromPoints([origin, enemy.shotTarget]);
      if (enemy.aimTime < warning) return;
      const direction = enemy.shotTarget.clone().sub(origin).normalize();
      ray.set(origin, direction);
      ray.far = origin.distanceTo(enemy.shotTarget);
      if (!ray.intersectObjects(blockers, false).length) {
        const mesh = new THREE.Mesh(boltGeometry, boltMaterial);
        mesh.position.copy(origin);
        scene.add(mesh);
        bolts.push({ mesh, velocity: direction.multiplyScalar(26), damage: enemy.damage, life: 3 });
        enemy.shotsFired++;
        enemy.flashTimer = 0.1;
        enemy.flash.visible = true;
        playTone(enemy.stalker ? 230 : 130, 0.13, "sawtooth", 0.055);
      }
      enemy.burstLeft--;
      enemy.attackTimer = enemy.burstLeft > 0 ? 0.16 : (enemy.stalker ? 1 : 0.8);
      enemy.aimTime = 0;
      enemy.aimLine.visible = false;
    },
    update(dt, target) {
      sphere.center.copy(target);
      sphere.center.y -= 0.25;
      sphere.radius = 0.48;
      scene.updateMatrixWorld(true);
      for (let i = bolts.length - 1; i >= 0; i--) {
        const bolt = bolts[i];
        const step = bolt.velocity.clone().multiplyScalar(dt);
        ray.set(bolt.mesh.position, step.clone().normalize());
        ray.far = step.length();
        const wall = ray.intersectObjects(blockers, false)[0];
        const hit = ray.ray.intersectSphere(sphere, new THREE.Vector3());
        const hitDistance = hit ? hit.distanceTo(bolt.mesh.position) : Infinity;
        const hitsPlayer = hitDistance <= ray.far && (!wall || hitDistance < wall.distance);
        bolt.life -= dt;
        if (wall || hitsPlayer || bolt.life <= 0) {
          if (hitsPlayer) damagePlayer(bolt.damage);
          scene.remove(bolt.mesh);
          bolts.splice(i, 1);
        } else bolt.mesh.position.add(step);
      }
    }
  };
}
