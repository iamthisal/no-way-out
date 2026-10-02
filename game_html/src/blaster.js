import * as THREE from "three";

export function createBlaster({ camera, scene, blockers, enemies, playTone, onNoise }) {
  const weapon = new THREE.Group();
  weapon.position.set(0.32, -0.28, -0.6);
  const metal = new THREE.MeshStandardMaterial({ color: 0x687c83, roughness: 0.4, metalness: 0.5 });
  const barrel = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.15, 0.5), metal);
  const grip = new THREE.Mesh(new THREE.BoxGeometry(0.12, 0.24, 0.14), metal);
  grip.position.set(0, -0.13, 0.12);
  const stripe = new THREE.Mesh(new THREE.BoxGeometry(0.145, 0.035, 0.3), new THREE.MeshBasicMaterial({ color: 0x58eaff }));
  stripe.position.y = 0.04;
  const flash = new THREE.Mesh(new THREE.OctahedronGeometry(0.1), new THREE.MeshBasicMaterial({ color: 0xb7fbff }));
  flash.position.z = -0.3;
  flash.visible = false;
  const light = new THREE.PointLight(0x70eeff, 0, 5);
  light.position.copy(flash.position);
  weapon.add(barrel, grip, stripe, flash, light);
  camera.add(weapon);
  const ray = new THREE.Raycaster();
  const direction = new THREE.Vector3();
  const shots = [];
  let held = false;
  let cooldown = 0;
  let recoil = 0;
  let hitTime = 0;
  const crosshair = document.querySelector("#crosshair");

  function fire() {
    scene.updateMatrixWorld(true);
    camera.getWorldDirection(direction);
    ray.set(camera.getWorldPosition(new THREE.Vector3()), direction);
    ray.far = 55;
    const targets = enemies.filter(enemy => enemy.health > 0).map(enemy => enemy.group);
    const hit = ray.intersectObjects([...blockers, ...targets], true)[0];
    const end = hit ? hit.point : ray.ray.at(55, new THREE.Vector3());
    if (hit) {
      const enemy = enemies.find(enemy => {
        let node = hit.object;
        while (node) { if (node === enemy.group) return true; node = node.parent; }
        return false;
      });
      if (enemy) {
        enemy.health -= 34;
        enemy.incomingFire = 1;
        hitTime = 0.15;
        crosshair.classList.add("hit");
        playTone(780, 0.07, "triangle", 0.045);
        if (enemy.health <= 0) {
          enemy.state = "disabled";
          enemy.group.visible = false;
          if (enemy.aimLine) enemy.aimLine.visible = false;
          if (enemy.flash) enemy.flash.visible = false;
          playTone(100, 0.25, "sawtooth", 0.05);
        } else if (!enemy.stalker && !enemy.hound && enemy.state !== "chase") {
          enemy.state = "investigate";
          enemy.investigatePoint = new THREE.Vector3(camera.position.x, 0, camera.position.z);
          enemy.path = [];
        }
      }
    }
    const start = flash.getWorldPosition(new THREE.Vector3());
    const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints([start, end]), new THREE.LineBasicMaterial({ color: 0x8ef9ff, transparent: true, opacity: 0.9 }));
    scene.add(line);
    shots.push({ line, life: 0.075 });
    cooldown = 0.3;
    recoil = 1;
    onNoise();
    playTone(170, 0.12, "sawtooth", 0.09);
    playTone(980, 0.06, "square", 0.03);
  }

  return {
    setTrigger(value) { held = value; },
    update(dt) {
      cooldown = Math.max(0, cooldown - dt);
      if (held && cooldown === 0) fire();
      recoil = Math.max(0, recoil - dt * 9);
      weapon.position.z = -0.6 + recoil * 0.1;
      weapon.rotation.x = recoil * 0.1;
      flash.visible = recoil > 0.65;
      light.intensity = flash.visible ? 12 : 0;
      hitTime = Math.max(0, hitTime - dt);
      if (!hitTime) crosshair.classList.remove("hit");
      for (let i = shots.length - 1; i >= 0; i--) {
        const shot = shots[i];
        shot.life -= dt;
        if (shot.life <= 0) {
          scene.remove(shot.line);
          shot.line.geometry.dispose();
          shot.line.material.dispose();
          shots.splice(i, 1);
        }
      }
    }
  };
}
