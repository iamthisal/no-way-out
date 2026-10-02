const assert = require('node:assert/strict');
const fs = require('node:fs');
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

async function main() {
  const pages = await (await fetch('http://127.0.0.1:9222/json/list')).json();
  const page = pages.find(page => page.type === 'page');
  assert.ok(page, 'Chromium page available');
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }));
  let id = 0;
  const pending = new Map(), errors = [];
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
    if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.text);
    if (message.method === 'Fetch.requestPaused') {
      const requestId = message.params.requestId;
      try {
        const response = await send('Fetch.getResponseBody', { requestId });
        let body = response.base64Encoded ? Buffer.from(response.body, 'base64').toString() : response.body;
        body += '\nglobalThis.__roomTest = {THREE,mats,createEnemy,player,camera,enemies,keys,renderer,scene,controllers,doors,interactables,ROOM_CONFIG,RECOVERY_CONFIG,contains,updateRooms,updateRecovery,setRecoveryDelay:value=>{recoveryDelay=value;},updateEnemies,updateInteractions,updateDoorAndProps,updatePlayer,updateHud,canOccupy,findPath,blaster};';
        await send('Fetch.fulfillRequest', { requestId, responseCode: 200,
          responseHeaders: [{ name: 'Content-Type', value: 'application/javascript' }],
          body: Buffer.from(body).toString('base64') });
      } catch (error) { errors.push(error.message); await send('Fetch.continueRequest', { requestId }); }
    }
  });
  const evaluate = async expression => {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  };
  try {
    await send('Runtime.enable');
    await send('Fetch.enable', { patterns: [{ urlPattern: '*/src/main.js*', requestStage: 'Response' }] });
    await send('Page.navigate', { url: 'http://127.0.0.1:4173/' });
    for (let attempt=0;attempt<30;attempt++) {
      await wait(300);
      if(await evaluate("typeof __roomTest !== 'undefined'")) break;
      if(attempt===29) throw Error('Game did not initialize: '+JSON.stringify(errors));
    }
    const flow = await evaluate(`(() => {
      const t = __roomTest, results = [];
      const check = (condition, message) => { if (!condition) throw Error(message); results.push(message); };
      t.updateRooms(20); t.updateEnemies(20, 20);
      check(t.enemies.length === 0 && t.player.health === 100, 'Safe start');
      t.player.health=40;t.setRecoveryDelay(t.RECOVERY_CONFIG.delay);
      t.updateRecovery(4.9);check(t.player.health===40,'Recovery waits after damage');
      t.updateRecovery(1.1);check(Math.abs(t.player.health-46)<0.001,'Recovery rate after delay');
      t.updateHud(20);check(document.querySelector('#recoveryText').textContent==='Recovering','Recovery HUD');
      t.setRecoveryDelay(5);t.updateRecovery(1);check(Math.abs(t.player.health-46)<0.001,'New damage interrupts recovery');
      t.setRecoveryDelay(0);t.player.health=99;t.updateRecovery(1);check(t.player.health===100,'Recovery capped at 100');
      t.player.health=0;t.updateRecovery(10);check(t.player.health===0,'Recovery cannot revive dead player');
      t.player.health=100;
      check(!t.canOccupy(-24,0), 'Closed door blocks occupancy');
      check(t.findPath({x:-24,z:4},{x:-24,z:-4}).length === 0, 'Closed door blocks EasyStar');
      const probe = t.createEnemy({kind:'Sentinel',roomId:1,pos:new t.THREE.Vector3(-24,0,-4),color:t.mats.sentinel,patrol:[new t.THREE.Vector3(-24,0,-4)]});
      t.enemies.push(probe);
      t.camera.position.set(-24,1.4,4); t.camera.lookAt(-24,1.2,-4);
      t.blaster.setTrigger(true); t.blaster.update(0.31); t.blaster.setTrigger(false);
      check(probe.health===100,'Closed door blocks blaster');
      t.player.body.position.set(-24,1.08,2); t.keys.add('KeyW');
      for(let i=0;i<60;i++) t.updatePlayer(1/60,i/60);
      t.keys.clear();
      check(t.player.body.position.z > 0.8, 'Closed door blocks player physics');
      for(let roomId=0;roomId<4;roomId++) {
        const config=t.ROOM_CONFIG[roomId], c=t.controllers[roomId];
        if(roomId>0) {
          const [x,z]=config.spawns[0]; t.player.body.position.set(x,1.08,z); t.updateRooms(0);
          check(c.wave===1 && c.alive>0, 'Room '+roomId+' activates on entry');
          const lever=t.interactables.find(i=>i.roomId===roomId);
          if(roomId<3) {
            t.player.body.position.set(lever.x,1.08,lever.z); t.updateInteractions(0);
            check(document.querySelector('#prompt').textContent==='Locked: clear the room','Locked lever '+roomId);
          } else check(!lever.mesh.visible,'Final lever initially hidden');
          for(let wave=1;wave<=config.waves.length;wave++) {
            check(c.wave===wave, 'Wave '+roomId+'.'+wave);
            check(c.alive===Object.values(config.waves[wave-1]).reduce((a,b)=>a+b,0),'Wave count '+roomId+'.'+wave);
            for(const enemy of c.enemies) check(t.contains(config,enemy.group.position,0.8)&&t.canOccupy(enemy.group.position.x,enemy.group.position.z),'Valid '+enemy.kind+' spawn');
            for(let i=0;i<100;i++) { t.player.health=100; t.updateEnemies(0.05,i*0.05); }
            check(c.enemies.every(e=>t.contains(config,e.group.position,0.8)),'AI stays inside room '+roomId);
            const dead=c.enemies.slice(); dead.forEach(e=>{e.health=0;});
            t.updateEnemies(9,10); check(dead.every(e=>e.health<=0),'No individual respawn');
            t.player.health=40;t.updateRooms(0);
            const expectedHealth=wave===config.waves.length?100:65;
            check(t.player.health===expectedHealth,'Clear reward '+roomId+'.'+wave);
            t.updateRooms(0);check(t.player.health===expectedHealth,'Clear reward happens once');
            if(wave<config.waves.length) {
              t.updateRooms(config.pause-0.1); check(c.wave===wave,'Wave pause'); t.updateRooms(0.11);
            }
          }
          check(c.leverUnlocked,'Room '+roomId+' cleared');
          check(t.scene.fog.color.getHex()===config.fog,'Room atmosphere '+roomId);
        }
        const lever=t.interactables.find(i=>i.roomId===roomId);
        t.player.body.position.set(lever.x,1.08,lever.z); t.updateInteractions(0);
        check(document.querySelector('#prompt').textContent==='Press E','Unlocked lever '+roomId);
        t.keys.add('KeyE'); t.updateInteractions(0); t.keys.clear();
        if(roomId<3) {
          const d=t.doors[roomId]; check(d.open,'Door '+(roomId+1)+' opening');
          for(let i=0;i<90;i++) t.updateDoorAndProps(1/60,i/60);
          check(d.cleared && t.canOccupy(d.obstacle.x,d.obstacle.z),'Door '+(roomId+1)+' opened');
          const [x,z,axis]=config.door;
          const a=axis==='z'?{x,z:z+4}:{x:x-4,z},b=axis==='z'?{x,z:z-4}:{x:x+4,z};
          check(t.findPath(a,b).length>0,'Opened door updates EasyStar');
          if(roomId===0) {
            t.camera.position.set(-24,1.4,4); t.camera.lookAt(-24,1.2,-4);
            t.blaster.setTrigger(true); t.blaster.update(0.31); t.blaster.setTrigger(false);
            check(probe.health===66,'Opened door allows blaster shots');
            t.enemies.splice(t.enemies.indexOf(probe),1); t.scene.remove(probe.group);
          }
        }
      }
      check(document.querySelector('#endTitle').textContent==='You Won'&&document.querySelector('#endPanel').classList.contains('show'),'Victory screen');
      return results;
    })()`);
    console.log('Integration:', flow.length, 'checks passed');
    for (const roomId of [1,2,3]) {
      await evaluate(`(() => {
        const t=__roomTest, room=t.ROOM_CONFIG[${roomId}];
        document.querySelector('#centerPanel').classList.remove('show');
        document.querySelector('#endPanel').classList.remove('show');
        t.scene.fog.color.setHex(room.fog); t.scene.background.setHex(room.fog);
        t.player.body.position.set(${roomId===1?-24:24},1.08,${roomId===3?4:-26});
        t.updateRooms(0); t.updateHud(20);
        t.camera.position.set(${roomId===1?-24:24},1.72,${roomId===3?4:-26});
        t.camera.lookAt(${roomId===1?-18:18},1.5,${roomId===3?20:-12});
        for(const e of t.controllers[${roomId}].enemies) {e.health=e.maxHealth;e.group.visible=true;}
      })()`);
      await wait(100);
      const screenshot=await send('Page.captureScreenshot',{format:'png'});
      fs.writeFileSync('room-flow-room'+roomId+'.png',Buffer.from(screenshot.data,'base64'));
    }
    await send('Fetch.disable');
    for (const [name,width,height] of [['desktop',1440,900],['mobile',390,844]]) {
      await send('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:name==='mobile'});
      await send('Page.reload',{ignoreCache:true});
      await wait(1300);
      await evaluate("document.querySelector('#centerPanel').classList.remove('show')");
      await wait(100);
      const image = await send('Page.captureScreenshot',{format:'png'});
      fs.writeFileSync('room-flow-'+name+'.png',Buffer.from(image.data,'base64'));
      const pixels=await send('Runtime.evaluate',{awaitPromise:true,returnByValue:true,expression:`(async()=>{
        const img=new Image(); img.src='data:image/png;base64,${image.data}'; await img.decode();
        const c=document.createElement('canvas');c.width=img.width;c.height=img.height;
        const context=c.getContext('2d');context.drawImage(img,0,0);
        const data=context.getImageData(Math.floor(c.width*.2),Math.floor(c.height*.35),Math.floor(c.width*.6),Math.floor(c.height*.45)).data;
        const colors=new Set();for(let i=0;i<data.length;i+=160)colors.add(data[i]+','+data[i+1]+','+data[i+2]);return colors.size;
      })()`});
      assert.ok(pixels.result.value>8,name+' scene pixels are nonblank');
    }
    assert.deepEqual(errors, []);
    console.log('Desktop/mobile screenshots captured; no browser exceptions.');
  } finally { socket.close(); }
}
main().catch(error => { console.error(error); process.exitCode=1; });
