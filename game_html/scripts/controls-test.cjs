const fs = require('node:fs');
const assert = require('node:assert/strict');
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

async function main() {
  const pages = await (await fetch('http://127.0.0.1:9222/json/list')).json();
  const page = pages.find(item => item.type === 'page');
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }));
  let id = 0;
  const pending = new Map();
  const errors = [];
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    pending.set(++id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
  socket.addEventListener('message', async event => {
    const message = JSON.parse(event.data);
    if (message.id) {
      const request = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) request.reject(new Error(JSON.stringify(message.error)));
      else request.resolve(message.result);
    }
    if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.exception?.description || message.params.exceptionDetails.text);
    if (message.method === 'Fetch.requestPaused') {
      const { requestId } = message.params;
      try {
        const response = await send('Fetch.getResponseBody', { requestId });
        let body = response.base64Encoded ? Buffer.from(response.body, 'base64').toString() : response.body;
        body = body.replace('  const ctx = {', '  const ctx = globalThis.__enemyContext = {');
        // Instrument only this browser response; the shipped game has no test globals.
        body += '\nglobalThis.__gameTest = {player,camera,enemies,blaster,keys,world,scene,renderer,blockers,canSeePlayer,clearLine,updatePlayer,updateEnemies,updateRex,rex,updateHud,setPaused,enemyCombat,findPath,canOccupy,updateRespawn,broadcastAlert,resetAlert:()=>{securityAlert=null;soundPing=null;}};';
        await send('Fetch.fulfillRequest', { requestId, responseCode: 200,
          responseHeaders: [{ name: 'Content-Type', value: 'application/javascript' }],
          body: Buffer.from(body).toString('base64') });
      } catch (error) { errors.push(error.message); }
    }
  });
  const evaluate = async expression => {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception.description);
    return result.result.value;
  };
  await send('Runtime.enable');
  await send('Network.enable');
  await send('Network.setCacheDisabled', { cacheDisabled: true });
  await send('Fetch.enable', { patterns: [{ urlPattern: '*/src/main.js*', requestStage: 'Response' }] });
  await send('Page.navigate', { url: 'http://127.0.0.1:4175/' });
  for (let n = 0; n < 60; n++) {
    if (await evaluate('Boolean(globalThis.__gameTest)')) break;
    await wait(100);
  }
  assert(await evaluate('Boolean(globalThis.__gameTest)'), 'game loaded: '+JSON.stringify(errors));
  if (process.argv.includes('--rex-only')) {
    const rexCheck = await evaluate(`(() => {
      const g=__gameTest;g.resetAlert();g.keys.clear();
      for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;e.group.visible=false;}
      const enemy=g.enemies[0],dead=g.enemies[3];dead.group.position.set(-22,0,2);dead.state='disabled';enemy.health=100;enemy.group.visible=true;enemy.state='patrol';enemy.group.position.set(-22,0,2);enemy.path=[];enemy.repath=0;
      g.player.body.position.set(-22,1.08,14);g.updatePlayer(1/60,0);
      for(let i=0;i<120;i++)g.updateRex(1/60);
      const follows=g.rex.mesh.position.distanceTo(g.rex.target)<0.05;
      g.rex.cooldown=0;g.keys.add('KeyQ');g.updateRex(1/60);g.keys.delete('KeyQ');
      const activated=g.rex.cooldown===16;const safeTarget=g.canOccupy(enemy.investigatePoint.x,enemy.investigatePoint.z);
      const deadUnaffected=dead.state!=='investigate';
      const initial=enemy.investigatePoint.clone();g.broadcastAlert(new g.camera.position.constructor(30,0,24));g.updateEnemies(1/60,0);
      const survivesAlarm=enemy.state==='investigate'&&enemy.investigatePoint.distanceTo(initial)<.01;
      g.keys.add('KeyQ');g.updateRex(.5);g.keys.delete('KeyQ');const cooldownBlocks=g.rex.cooldown<16;
      g.rex.cooldown=0;g.updateRex(1/60);enemy.state='chase';g.keys.add('KeyQ');g.updateRex(1/60);g.keys.delete('KeyQ');g.updateEnemies(1/60,0);
      const hiddenChaserDistracted=enemy.state==='investigate';
      g.updateRex(1/60);const visibleBeacon=g.rex.beacon.visible&&g.rex.distractionTime>0;
      for(let i=0;i<1100;i++)g.updateRex(1/60);
      const cooldownExpires=g.rex.cooldown===0;
      g.keys.add('KeyQ');g.updateRex(1/60);for(let i=0;i<1100;i++)g.updateRex(1/60);const heldDoesNotRepeat=g.rex.cooldown===0;
      g.keys.clear();g.updateRex(1/60);g.updateHud(0);const readyHud=document.querySelector('#rexText').textContent==='Rex ready';
      enemy.group.position.set(-22,0,20);enemy.group.rotation.y=0;enemy.state='chase';enemy.distractionTimer=0;g.scene.updateMatrixWorld(true);
      g.keys.add('KeyQ');g.updateRex(1/60);g.keys.delete('KeyQ');const visibleChaserNotFooled=enemy.state==='chase'&&enemy.distractionTimer===0;
      const ambush=g.enemies[2];ambush.health=60;ambush.group.position.set(-22,0,2);ambush.state='peek';g.updateRex(1/60);g.rex.cooldown=0;
      g.keys.add('KeyQ');g.updateRex(1/60);g.keys.delete('KeyQ');g.updateEnemies(1/60,0);
      const ambusherDistracted=ambush.state==='investigate'&&ambush.distractionTimer>0;
      g.resetAlert();for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;}
      enemy.health=100;enemy.state='patrol';enemy.group.position.set(-22,0,2);enemy.path=[];enemy.repath=0;enemy.navGoal=null;enemy.distractionTimer=0;
      g.keys.clear();g.rex.phase='follow';g.rex.distractionTime=0;g.rex.cooldown=0;g.rex.qHeld=false;g.player.body.position.set(-22,1.08,14);g.player.body.velocity.set(0,0,0);g.updatePlayer(1/60,0);
      for(let i=0;i<120;i++)g.updateRex(1/60);
      g.keys.add('KeyQ');g.updateRex(1/60);g.keys.delete('KeyQ');
      let travelFrames=0;while(g.rex.phase==='outbound'&&travelFrames<180){g.updateRex(1/60);g.updateEnemies(1/60,0);travelFrames++;}
      const fullDurationOnArrival=g.rex.phase==='distract'&&g.rex.distractionTime===6;
      for(let i=0;i<330;i++){g.player.body.position.x=22;g.player.body.position.z=14+i/60;g.updateRex(1/60);g.updateEnemies(1/60,0);}
      g.updateHud(0);const staysAtDestination=g.rex.phase==='distract'&&g.rex.mesh.position.distanceTo(g.rex.target)<0.35&&g.rex.distractionTime>0.45;
      const sustainedEnemyDistraction=enemy.state==='investigate'&&enemy.distractionTimer>0;
      const activeCountdown=document.querySelector('#rexText').textContent.startsWith('Rex distracting');
      for(let i=0;i<40;i++)g.updateRex(1/60);
      const returnsAfterDuration=g.rex.distractionTime===0&&!g.rex.beacon.visible&&['return','follow'].includes(g.rex.phase);
      for(let i=0;i<120;i++)g.updateRex(1/60);const followsAgain=g.rex.phase==='follow'&&g.rex.mesh.position.distanceTo(g.rex.target)<0.05;
      return {follows,activated,safeTarget,deadUnaffected,survivesAlarm,cooldownBlocks,hiddenChaserDistracted,visibleBeacon,cooldownExpires,heldDoesNotRepeat,readyHud,visibleChaserNotFooled,ambusherDistracted,fullDurationOnArrival,staysAtDestination,sustainedEnemyDistraction,activeCountdown,returnsAfterDuration,followsAgain};
    })()`);
    console.log(JSON.stringify({ rexCheck, errors }, null, 2));socket.close();
    for(const [name,passed] of Object.entries(rexCheck))assert(passed,name);
    assert.equal(errors.length,0);return;
  }
  const results = await evaluate(`(() => {
    const g = __gameTest;
    const reset = () => {g.keys.clear();g.player.body.position.set(-31,1.08,24);g.player.body.velocity.set(0,0,0);};
    const move = code => {reset();for(let i=0;i<240;i++)g.updatePlayer(1/60,i/60);const before=g.player.body.position.clone();g.keys.add(code);for(let i=0;i<45;i++)g.updatePlayer(1/60,4+i/60);g.keys.clear();return Math.hypot(g.player.body.position.x-before.x,g.player.body.position.z-before.z);};
    const movement = Object.fromEntries(['KeyW','KeyA','KeyS','KeyD'].map(code=>[code,move(code)]));
    reset();g.player.body.position.x=-28;g.keys.add('KeyW');for(let i=0;i<360;i++)g.updatePlayer(1/60,i/60);g.keys.clear();
    const blockedAt=g.player.body.position.z;
    g.camera.position.set(-34,1.3,24);g.camera.rotation.set(0,0,0);const enemy=g.enemies[0];
    enemy.group.position.set(-34,0,19);g.scene.updateMatrixWorld(true);
    const health=enemy.health;g.blaster.setTrigger(true);g.blaster.update(.31);g.blaster.setTrigger(false);
    const damage=health-enemy.health;
    g.camera.position.set(-22,1.72,10);g.camera.rotation.set(0,0,0);enemy.group.position.set(-22,0,2);g.scene.updateMatrixWorld(true);
    const behind=enemy.health;g.blaster.setTrigger(true);g.blaster.update(.31);g.blaster.setTrigger(false);
    const shotBlocked=enemy.health===behind;
    const losBlocked=!g.canSeePlayer(enemy,new g.camera.position.constructor(-22,0,10));
    enemy.group.position.set(-34,0,19);g.camera.position.set(-34,1.3,24);g.scene.updateMatrixWorld(true);
    g.blaster.setTrigger(true);for(let i=0;i<3;i++)g.blaster.update(.31);g.blaster.setTrigger(false);
    const disabled=enemy.health<=0&&!enemy.group.visible;
    reset();g.camera.position.set(-31,1.72,24);g.scene.updateMatrixWorld(true);g.renderer.render(g.scene,g.camera);
    const gl=g.renderer.getContext();const pixels=new Uint8Array(4*120*80);gl.readPixels(0,0,120,80,gl.RGBA,gl.UNSIGNED_BYTE,pixels);
    let brightness=0;for(let i=0;i<pixels.length;i+=4)brightness+=(pixels[i]+pixels[i+1]+pixels[i+2])/3;
    return {movement,blockedAt,damage,shotBlocked,losBlocked,disabled,brightness:brightness/(120*80)};
  })()`);
  for (const distance of Object.values(results.movement)) assert(distance > 1, 'movement after idle');
  assert(results.blockedAt > 15, 'crate collision blocks movement');
  assert.equal(results.damage, 34);
  assert(results.shotBlocked && results.losBlocked && results.disabled);
  assert(results.brightness > 25, 'visible canvas pixels');
  const combat = await evaluate(`(() => {
    const g=__gameTest;
    for(const e of g.enemies){e.health=0;e.group.visible=false;e.respawnTimer=Infinity;}
    const reset=e=>{g.resetAlert();e.respawnTimer=null;e.health=100;e.group.visible=true;e.attackTimer=0;e.aimTime=0;e.path=[];e.repath=0;e.lostTimer=0;g.player.health=100;g.player.alarm=0;};
    const clearBolts=()=>{for(const b of g.enemyCombat.bolts)g.scene.remove(b.mesh);g.enemyCombat.bolts.length=0;};
    const pursuer=g.enemies[0];reset(pursuer);pursuer.state='patrol';pursuer.group.position.set(-34,0,10);pursuer.group.rotation.y=Math.PI;
    g.player.body.position.set(-34,1.08,24);const startDistance=14;
    let damage=0, warning=false, unsafe=false;
    for(let i=0;i<360;i++){g.player.health=100;g.updateEnemies(1/60,i/60);damage+=100-g.player.health;warning ||= pursuer.aimLine.visible;unsafe ||= !g.canOccupy(pursuer.group.position.x,pursuer.group.position.z);}
    const endDistance=Math.hypot(pursuer.group.position.x+34,pursuer.group.position.z-24);
    const chased=pursuer.state==='chase'&&endDistance<startDistance-4;
    clearBolts();reset(pursuer);pursuer.state='chase';pursuer.group.position.set(-22,0,2);pursuer.lastKnown.set(-22,0,10);g.player.body.position.set(-22,1.08,10);
    let coveredDamage=0;
    for(let i=0;i<30;i++){g.player.health=100;g.updateEnemies(1/60,i/60);coveredDamage+=100-g.player.health;}
    const route=g.findPath(pursuer.group.position,new g.camera.position.constructor(-22,0,10));
    const routeAroundShelf=route.length>3&&route.every(p=>g.canOccupy(p.x,p.z));
    for(let i=0;i<900;i++){g.player.health=100;g.updateEnemies(1/60,i/60);unsafe ||= !g.canOccupy(pursuer.group.position.x,pursuer.group.position.z);}
    const reacquired=pursuer.state==='chase'&&g.canSeePlayer(pursuer,new g.camera.position.constructor(-22,0,10));
    pursuer.health=0;pursuer.respawnTimer=Infinity;clearBolts();const ambusher=g.enemies[2];reset(ambusher);ambusher.state='hide';ambusher.group.position.copy(ambusher.cover);ambusher.coverTimer=0;ambusher.alerted=true;g.player.body.position.set(-31,1.08,26);g.player.body.velocity.set(0,0,0);
    const states=new Set();let ambushDamage=0, ambushWarning=false;
    for(let i=0;i<900;i++){g.player.health=100;g.updateEnemies(1/60,i/60);ambushDamage+=100-g.player.health;states.add(ambusher.state);ambushWarning ||= ambusher.aimLine.visible;unsafe ||= !g.canOccupy(ambusher.group.position.x,ambusher.group.position.z);}
    return {chased,endDistance,damage,warning,coveredDamage,routeAroundShelf,reacquired,ambushDamage,ambushWarning,states:[...states],unsafe};
  })()`);
  console.log(JSON.stringify({ combat }, null, 2));
  assert(combat.chased && combat.damage > 0 && combat.warning, 'pursuer chases and fires');
  assert.equal(combat.coveredDamage, 0, 'shelves block enemy fire');
  assert(combat.routeAroundShelf && combat.reacquired && !combat.unsafe, 'navigation routes around obstacles');
  assert(combat.ambushDamage > 0 && combat.ambushWarning && combat.states.includes('hide') && combat.states.includes('peek'), 'ambusher fires and returns to cover');
  const reinforcements = await evaluate(`(() => {
    const g=__gameTest;g.resetAlert();
    for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;e.group.visible=false;}
    const far=g.enemies[3],hidden=g.enemies[4];
    for(const e of [far,hidden]){e.health=e.maxHealth;e.respawnTimer=null;e.group.visible=true;e.group.position.copy(e.pos);e.state=e.stalker?'hide':'patrol';e.lastAlert=-1;e.path=[];e.repath=0;}
    g.player.body.position.set(-31,1.08,24);g.player.health=100;
    const original=far.group.position.clone();const oldCover=hidden.cover.clone();
    g.broadcastAlert(new g.camera.position.constructor(-31,0,24));
    for(let i=0;i<180;i++){g.player.health=100;g.updateEnemies(1/60,i/60);}
    const remoteReacted=far.alerted&&far.group.position.distanceTo(original)>4;
    const ambusherRelocated=hidden.alerted&&hidden.cover.distanceTo(oldCover)>20;
    g.resetAlert();for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;}
    const robot=g.enemies[0];robot.health=0;robot.respawnTimer=8;robot.state='disabled';robot.group.visible=false;
    g.player.body.position.set(34,1.08,26);g.camera.position.set(34,1.72,26);g.camera.rotation.set(0,0,0);
    for(let i=0;i<420;i++)g.updateEnemies(1/60,i/60);
    const delayed=robot.health<=0&&!robot.group.visible;
    for(let i=0;i<70;i++)g.updateEnemies(1/60,7+i/60);
    const restored=robot.health===robot.maxHealth&&robot.group.visible&&robot.state!=='disabled';
    robot.health=0;robot.respawnTimer=8;g.updateRespawn(robot,8,{canSpawn:()=>false,alert:null});
    const waitsForSafeSpawn=robot.health<=0&&!robot.group.visible;
    g.updateRespawn(robot,1,{canSpawn:()=>true,alert:null});const repeatRespawn=robot.health===robot.maxHealth;
    const travel=(shift,crouch=false)=>{g.keys.clear();g.player.body.position.set(-34,1.08,24);g.player.body.velocity.set(0,0,0);g.player.stamina=100;g.keys.add('KeyW');if(shift)g.keys.add('ShiftLeft');if(crouch)g.keys.add('KeyC');for(let i=0;i<60;i++)g.updatePlayer(1/60,i/60);g.keys.clear();return {distance:24-g.player.body.position.z,stamina:g.player.stamina};};
    const walk=travel(false),sprint=travel(true),crouch=travel(true,true);
    return {remoteReacted,ambusherRelocated,delayed,restored,waitsForSafeSpawn,repeatRespawn,walk,sprint,crouch};
  })()`);
  console.log(JSON.stringify({ reinforcements }, null, 2));
  assert(reinforcements.remoteReacted && reinforcements.ambusherRelocated, 'distant enemies respond to alarms');
  assert(reinforcements.delayed && reinforcements.restored && reinforcements.waitsForSafeSpawn && reinforcements.repeatRespawn, 'respawn delay, safe placement, and repeated respawns');
  assert(reinforcements.walk.distance>4 && reinforcements.sprint.distance>reinforcements.walk.distance*1.6 && reinforcements.sprint.stamina<100 && reinforcements.crouch.distance<reinforcements.walk.distance, 'faster movement, Shift sprint, stamina and crouch');
  const tactics = await evaluate(`(() => {
    const g=__gameTest;g.resetAlert();
    for(const b of g.enemyCombat.bolts)g.scene.remove(b.mesh);g.enemyCombat.bolts.length=0;
    for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;}
    const reset=e=>{e.health=e.maxHealth;e.respawnTimer=null;e.group.visible=true;e.state='patrol';e.path=[];e.repath=0;e.navGoal=null;e.lostTimer=0;e.lastAlert=-1;e.alerted=false;e.tacticalTimer=0;e.burstLeft=0;e.attackTimer=0;e.aimTime=0;e.searchTimer=0;};
    const front=g.enemies[0],flank=g.enemies[3];reset(front);reset(flank);
    front.group.position.set(-10,0,14);flank.group.position.set(-2,0,16);front.group.rotation.y=flank.group.rotation.y=Math.PI;
    g.player.body.position.set(-10,1.08,24);g.player.body.velocity.set(3,0,0);g.player.health=100;
    g.updateEnemies(1/60,0);
    const separateAngles=front.state==='chase'&&flank.state==='chase'&&front.navGoal.distanceTo(flank.navGoal)>4;
    const leadingShot=front.shotTarget.x>-9.5;
    flank.health=0;flank.respawnTimer=Infinity;g.resetAlert();front.state='chase';front.group.position.set(-22,0,2);front.lastKnown.set(-22,0,2);front.lostTimer=0;front.path=[];front.repath=0;front.navGoal=null;
    g.player.body.position.set(34,1.08,26);g.player.body.velocity.set(0,0,0);
    for(let i=0;i<180;i++)g.updateEnemies(1/60,i/60);
    const searching=front.state==='search'&&front.searchTimer>10;
    const remembersOnlySeen=front.lastKnown.x===-22&&front.lastKnown.z===2;
    let unsafe=false;for(let i=0;i<1200;i++){g.updateEnemies(1/60,3+i/60);unsafe ||= !g.canOccupy(front.group.position.x,front.group.position.z);}
    const eventuallyReturns=['return','patrol'].includes(front.state);
    front.health=0;front.respawnTimer=Infinity;g.resetAlert();const ambush=g.enemies[2];reset(ambush);ambush.cover.copy(ambush.spawnCover);ambush.peek.copy(ambush.spawnPeek);ambush.group.position.copy(ambush.cover);ambush.state='peek';ambush.coverTimer=2;ambush.relocateTimer=0;ambush.lastKnown.set(-31,0,24);ambush.incomingFire=1;
    g.player.body.position.set(-31,1.08,24);const oldCover=ambush.cover.clone();g.updateEnemies(1/60,0);
    const retreatsWhenHit=ambush.state==='retreat'&&ambush.cover.distanceTo(oldCover)>2&&ambush.relocateTimer>0;
    return {separateAngles,leadingShot,searching,remembersOnlySeen,eventuallyReturns,retreatsWhenHit,unsafe};
  })()`);
  console.log(JSON.stringify({ tactics }, null, 2));
  assert(tactics.separateAngles && tactics.leadingShot, 'coordinated flanking and aimed bursts');
  assert(tactics.searching && tactics.remembersOnlySeen && tactics.eventuallyReturns && !tactics.unsafe, 'persistent search without wall tracking');
  assert(tactics.retreatsWhenHit, 'ambusher relocates when hit');
  const guerrilla = await evaluate(`(() => {
    const g=__gameTest;g.resetAlert();
    for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;}
    for(const b of g.enemyCombat.bolts)g.scene.remove(b.mesh);g.enemyCombat.bolts.length=0;
    const e=g.enemies[2];e.health=e.maxHealth;e.respawnTimer=null;e.group.visible=true;e.cover.set(-26,0,7.5);e.peek.set(-32,0,7.5);e.group.position.copy(e.peek);e.group.rotation.y=Math.PI;
    e.state='peek';e.coverTimer=3;e.path=[];e.navGoal=null;e.repath=0;e.attackTimer=0;e.burstLeft=0;e.aimTime=0;e.shotsFired=0;e.attackStart=0;e.alerted=false;e.distractionTimer=0;e.incomingFire=0;e.relocateTimer=0;
    g.player.body.position.set(-32,1.08,-27);g.player.body.velocity.set(0,0,0);g.player.health=100;g.scene.updateMatrixWorld(true);
    const spotsAtLongRange=g.canSeePlayer(e,new g.camera.position.constructor(-32,0,-27));
    let frames=0;while(e.shotsFired<2&&frames<480){g.player.health=100;g.updateEnemies(1/60,frames/60);frames++;}
    const firesAtLongRange=e.shotsFired>=2;const hidesAfterBurst=e.state==='hide';
    let actuallyCovered=false;for(let i=0;i<300;i++){g.player.health=100;g.updateEnemies(1/60,i/60);if(e.state==='hide'&&e.group.position.distanceTo(e.cover)<.5&&!g.clearLine(e.group.position,new g.camera.position.constructor(-32,0,-27))){actuallyCovered=true;break;}}
    const oldCover=e.cover.clone();g.player.body.position.set(e.group.position.x+3,1.08,e.group.position.z+6);g.player.body.velocity.set(-2,0,-4);g.player.health=100;
    const before=Math.hypot(e.group.position.x-g.player.body.position.x,e.group.position.z-g.player.body.position.z);g.updateEnemies(1/60,0);
    const fleesWhenChased=e.state==='retreat'&&e.cover.distanceTo(oldCover)>2;
    let unsafe=false;for(let i=0;i<180;i++){g.player.health=100;g.updateEnemies(1/60,i/60);unsafe ||= !g.canOccupy(e.group.position.x,e.group.position.z);}
    const separation=Math.hypot(e.group.position.x-g.player.body.position.x,e.group.position.z-g.player.body.position.z);
    const opensDistance=separation>before+4;
    e.group.position.set(-22,0,2);g.player.body.position.set(-22,1.08,10);g.scene.updateMatrixWorld(true);
    const wallsStillBlockSight=!g.canSeePlayer(e,new g.camera.position.constructor(-22,0,10));
    return {spotsAtLongRange,firesAtLongRange,hidesAfterBurst,actuallyCovered,fleesWhenChased,opensDistance,separation,wallsStillBlockSight,unsafe};
  })()`);
  console.log(JSON.stringify({ guerrilla }, null, 2));
  assert(guerrilla.spotsAtLongRange && guerrilla.firesAtLongRange && guerrilla.hidesAfterBurst && guerrilla.actuallyCovered, 'long-range hit-and-hide attacks');
  assert(guerrilla.fleesWhenChased && guerrilla.opensDistance && !guerrilla.unsafe && guerrilla.wallsStillBlockSight, 'retreat from pursuit with valid obstacle navigation');
  const purpleRegression = await evaluate(`(() => {
    const g=__gameTest;g.resetAlert();for(const e of g.enemies){e.health=0;e.respawnTimer=Infinity;}
    for(const b of g.enemyCombat.bolts)g.scene.remove(b.mesh);g.enemyCombat.bolts.length=0;
    const e=g.enemies[2];e.health=e.maxHealth;e.respawnTimer=null;e.group.visible=true;e.group.position.set(-34,0,10);e.cover.copy(e.group.position);e.peek.copy(e.group.position);
    Object.assign(e,{state:'hide',coverTimer:.2,alerted:false,path:[],navGoal:null,repath:0,attackTimer:0,burstLeft:0,aimTime:0,shotsFired:0,attackStart:0,distractionTimer:0,incomingFire:0,relocateTimer:0,navigationStalled:0,failedPost:null});
    g.player.body.position.set(-34,1.08,26);g.player.body.velocity.set(0,0,-4.8);
    for(let i=0;i<120;i++){g.player.body.position.z=26-i*.0125;g.player.health=100;g.updateEnemies(1/60,i/60);}
    const shootsWhileApproached=e.shotsFired>=2;
    g.resetAlert();e.group.position.set(-34,0,10);e.state='hide';e.relocateTimer=0;e.path=[];e.navGoal=null;e.repath=0;e.failedPost=null;
    g.player.body.position.set(-34,1.08,13);g.player.body.velocity.set(0,0,-4.8);g.player.health=100;g.updateEnemies(1/60,0);
    const ctx=globalThis.__enemyContext;const originalSelector=ctx.guerrillaPost;
    ctx.guerrillaPost=()=>({cover:e.group.position.clone(),peek:e.group.position.clone()});e.lastKnown.copy(ctx.playerPos);e.behavior.reposition(e,ctx,true);ctx.guerrillaPost=originalSelector;
    const fallbackEscapes=e.state==='retreat'&&e.cover.distanceTo(e.group.position)>3&&e.cover.distanceTo(e.lastKnown)>e.group.position.distanceTo(e.lastKnown)+3;
    e.navigationStalled=1;g.player.health=100;g.updateEnemies(1/60,0);
    const replansWhenStuck=e.failedPost!==null&&e.navigationStalled<.1;
    const before=e.group.position.clone();let unsafe=false;for(let i=0;i<150;i++){g.player.health=100;g.updateEnemies(1/60,i/60);unsafe ||= !g.canOccupy(e.group.position.x,e.group.position.z);}
    const actuallyRuns=e.group.position.distanceTo(before)>4;
    return {shootsWhileApproached,fallbackEscapes,replansWhenStuck,actuallyRuns,unsafe};
  })()`);
  console.log(JSON.stringify({ purpleRegression }, null, 2));
  assert(purpleRegression.shootsWhileApproached && purpleRegression.fallbackEscapes && purpleRegression.replansWhenStuck && purpleRegression.actuallyRuns && !purpleRegression.unsafe, 'purple attacks during approach and escapes stalled cover routes');
  await evaluate('document.querySelector("#startButton").click()');
  await wait(150);
  await evaluate('__gameTest.keys.add("KeyW");__gameTest.setPaused(true)');
  assert(await evaluate('__gameTest.keys.size===0 && document.querySelector("#pausePanel").classList.contains("show")'));
  await evaluate('document.querySelector("#pausePanel").classList.remove("show");document.querySelector("#centerPanel").classList.remove("show")');
  await evaluate(`(() => {
    const g=__gameTest;
    for(const e of g.enemies){e.health=100;e.group.visible=true;e.group.position.copy(e.pos);e.state=e.stalker?'hide':'patrol';e.attackTimer=0;e.aimTime=0;e.path=[];e.repath=0;}
    g.enemies[0].group.position.set(-34,0,15);g.enemies[0].group.rotation.y=Math.PI;
    g.enemies[2].group.position.copy(g.enemies[2].peek);g.enemies[2].state='peek';g.enemies[2].coverTimer=3;
    g.player.body.position.set(-31,1.08,24);g.player.health=100;
    for(let i=0;i<25;i++)g.updateEnemies(1/60,i/60);
    g.camera.position.set(-31,1.72,24);g.camera.rotation.set(0,.17,0);g.renderer.render(g.scene,g.camera);
  })()`);
  const screenshot = await send('Page.captureScreenshot', { format: 'png' });
  fs.writeFileSync('warehouse-controls-test.png', Buffer.from(screenshot.data, 'base64'));
  console.log(JSON.stringify({ ...results, pauseClearsKeys: true, errors }, null, 2));
  socket.close();
  assert.equal(errors.length, 0);
}
main().catch(error => { console.error(error);process.exit(1); });
