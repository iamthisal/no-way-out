const assert = require('node:assert/strict');
const fs = require('node:fs');
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

async function main() {
  const pages = await (await fetch('http://127.0.0.1:9223/json/list')).json();
  const page = pages.find(item => item.url.startsWith('http://127.0.0.1:4176/'));
  assert.ok(page, 'Game browser tab exists');
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
    if (message.method === 'Runtime.exceptionThrown') {
      const error = message.params.exceptionDetails.exception?.description || message.params.exceptionDetails.text;
      if (!error.includes('Pointer Lock')) errors.push(error);
    }
    if (message.method === 'Fetch.requestPaused') {
      const { requestId } = message.params;
      try {
        const response = await send('Fetch.getResponseBody', { requestId });
        let body = response.base64Encoded ? Buffer.from(response.body, 'base64').toString() : response.body;
        body += '\nglobalThis.__houndTest = {player,camera,enemies,keys,renderer,scene,updatePlayer,updateEnemies,emitHoundSound,blaster,canOccupy,findPath,updateRespawn};';
        await send('Fetch.fulfillRequest', { requestId, responseCode: 200,
          responseHeaders: [{ name: 'Content-Type', value: 'application/javascript' }],
          body: Buffer.from(body).toString('base64') });
      } catch (error) { errors.push(error.message); await send('Fetch.continueRequest', { requestId }); }
    }
  });
  try {
    await send('Runtime.enable');
    await send('Fetch.enable', { patterns: [{ urlPattern: '*/src/main.js*', requestStage: 'Response' }] });
    await send('Page.reload', { ignoreCache: true });
    await wait(1500);
    const evaluate = async expression => {
      const result = await send('Runtime.evaluate', { expression, returnByValue: true });
      if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
      return result.result.value;
    };
    const integration = await evaluate(`(() => {
      const t = __houndTest, h = t.enemies.find(e => e.hound);
      const result = {spawnValid:t.canOccupy(h.pos.x,h.pos.z), state:h.state};
      t.player.body.position.set(-14,1.08,24);
      t.emitHoundSound('gunshot');
      t.updateEnemies(0.1,0);
      result.afterShot = h.state;
      result.route = h.path.length;
      h.behavior.reset(); h.state='wander';
      t.keys.add('KeyW'); t.keys.add('ShiftLeft');
      t.updatePlayer(0.1,100);
      result.sprint = h.behavior.sounds.some(s=>s.loudness===5);
      t.keys.delete('ShiftLeft'); t.updatePlayer(0.1,101);
      result.walk = h.behavior.sounds.some(s=>s.loudness===0.8);
      t.keys.add('KeyC'); t.updatePlayer(0.1,102);
      result.crouch = h.behavior.sounds.some(s=>s.loudness===0.08);
      t.keys.clear(); h.behavior.reset(); h.state='wander';
      h.group.position.copy(h.pos);
      t.player.body.position.set(-14,1.08,21.5);
      t.emitHoundSound('sprint');
      const playerHealth=t.player.health;
      for(let i=0;i<12;i++) t.updateEnemies(0.1,1+i*0.1);
      result.biteDamage=playerHealth-t.player.health;
      h.group.position.copy(h.pos);
      t.player.body.position.set(-14,1.08,24);
      t.camera.position.set(-14,1.5,24); t.camera.lookAt(-14,0.65,22);
      t.scene.updateMatrixWorld(true);
      const health=h.health;
      t.blaster.update(0.1,{has:()=>false});
      t.blaster.setTrigger(true); t.blaster.update(0.31);
      t.blaster.setTrigger(false);
      result.blasterDamage=health-h.health;
      t.blaster.setTrigger(true); t.blaster.update(0.31);
      t.blaster.setTrigger(false);
      result.disabled=h.state==='disabled'&&h.health<=0;
      h.respawnTimer=0;
      t.updateRespawn(h,0.1,{canSpawn:()=>true,alert:null});
      result.respawn={state:h.state,health:h.health,memory:h.behavior.sounds.length};
      return result;
    })()`);
    assert.equal(integration.spawnValid, true);
    assert.equal(integration.afterShot, 'track');
    assert.ok(integration.route > 0);
    assert.equal(integration.sprint, true);
    assert.equal(integration.walk, true);
    assert.equal(integration.crouch, true);
    assert.equal(integration.biteDamage, 12);
    assert.equal(integration.blasterDamage, 34);
    assert.equal(integration.disabled, true);
    assert.deepEqual(integration.respawn, {state:'wander',health:60,memory:0});
    const frames = [];
    for (const [name, width, height] of [['desktop',1280,800],['mobile',390,844]]) {
      await send('Emulation.setDeviceMetricsOverride', {width,height,deviceScaleFactor:1,mobile:name==='mobile'});
      const pixels = await evaluate(`(() => {
        const t=__houndTest,h=t.enemies.find(e=>e.hound);
        document.querySelector('#centerPanel').classList.remove('show');
        t.renderer.setSize(${width},${height}); t.camera.aspect=${width}/${height}; t.camera.updateProjectionMatrix();
        t.camera.position.set(h.group.position.x+2,2.2,h.group.position.z+5);
        t.camera.lookAt(h.group.position.x,0.7,h.group.position.z);
        t.renderer.render(t.scene,t.camera);
        const gl=t.renderer.getContext(), data=new Uint8Array(4*64*64);
        gl.readPixels(Math.floor(gl.drawingBufferWidth/2)-32,Math.floor(gl.drawingBufferHeight/2)-32,64,64,gl.RGBA,gl.UNSIGNED_BYTE,data);
        let colored=0,red=0;
        for(let i=0;i<data.length;i+=4) {if(data[i]+data[i+1]+data[i+2]>90) colored++; if(data[i]>data[i+1]*1.4&&data[i]>data[i+2]*1.4) red++;}
        return {colored,red};
      })()`);
      assert.ok(pixels.colored>100, 'Canvas renders');
      assert.ok(pixels.red>20, 'Red Hound is visible');
      const screenshot = await send('Page.captureScreenshot', {format:'png'});
      fs.writeFileSync(`hound-${name}-test.png`,Buffer.from(screenshot.data,'base64'));
      frames.push({name,...pixels});
    }
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({integration,frames,errors},null,2));
  } finally { socket.close(); }
}

main().catch(error => { console.error(error); process.exitCode=1; });
