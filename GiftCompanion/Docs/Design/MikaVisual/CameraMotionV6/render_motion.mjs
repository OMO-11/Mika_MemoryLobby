// Local Edge/CDP review. No dependencies, network services or player data.
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, writeFile, realpath, lstat, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const root = path.dirname(fileURLToPath(import.meta.url));
const profile = await mkdtemp(path.join(tmpdir(), 'MikaVisualCheck-'));
const browser = spawn('C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe', [
  '--headless=new', '--disable-gpu', '--no-first-run', '--disable-background-networking',
  '--disable-component-update', '--disable-sync', '--hide-scrollbars', '--allow-file-access-from-files',
  '--remote-debugging-port=0', '--window-size=1440,1050', '--user-data-dir=' + profile,
  'about:blank'
], { windowsHide: true, stdio: 'ignore' });
const delay = ms => new Promise(r => setTimeout(r, ms));
let socket, serial = 0;
const pending = new Map(), errors = [], checks = [];
function call(method, params = {}) {
  const id = ++serial;
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('CDP timeout: ' + method)); }, 10000);
    pending.set(id, {resolve, reject, timer});
    socket.send(JSON.stringify({id, method, params}));
  });
}
async function evaluate(expression) {
  const result = await call('Runtime.evaluate', {expression, returnByValue:true, awaitPromise:true});
  if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
  return result.result.value;
}
async function check(name, expression) {
  const result = await evaluate(expression);
  checks.push({name, passed:result === true, actual:result});
  if (result !== true) throw new Error('Failed: ' + name + ' => ' + JSON.stringify(result));
}
async function move(x,y) { await call('Input.dispatchMouseEvent', {type:'mouseMoved', x,y}); await delay(250); }
async function click(id) {
  const p = await evaluate(`(()=>{const r=document.getElementById(${JSON.stringify(id)}).getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()`);
  await move(p.x,p.y);
  await call('Input.dispatchMouseEvent',{type:'mousePressed',...p,button:'left',clickCount:1});
  await call('Input.dispatchMouseEvent',{type:'mouseReleased',...p,button:'left',clickCount:1});
  await delay(200);
}
async function screenshot(name) {
  await evaluate('document.fonts.ready.then(()=>true)');
  await delay(300);
  const r = await call('Page.captureScreenshot', {format:'png', captureBeyondViewport:false});
  await writeFile(path.join(root,name+'.png'), Buffer.from(r.data,'base64'));
}
try {
  let port;
  for(let n=0;n<100;n++) {
    try { port = (await readFile(path.join(profile,'DevToolsActivePort'),'utf8')).split('\n')[0]; break; }
    catch { await delay(100); }
  }
  if(!port) throw new Error('Edge did not start');
  const target = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:'PUT'})).json();
  socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve,reject)=>{socket.addEventListener('open',resolve,{once:true});socket.addEventListener('error',reject,{once:true});});
  socket.addEventListener('message', e => {
    const m = JSON.parse(e.data);
    if(m.id && pending.has(m.id)) {
      const p=pending.get(m.id);pending.delete(m.id);clearTimeout(p.timer);
      m.error?p.reject(new Error(JSON.stringify(m.error))):p.resolve(m.result);
    }
    if(m.method==='Runtime.exceptionThrown') errors.push(m.params.exceptionDetails);
  });
  await call('Runtime.enable'); await call('Page.enable');
  await call('Emulation.setDeviceMetricsOverride',{width:1440,height:1050,deviceScaleFactor:1,mobile:false});
  await call('Emulation.setDeviceMetricsOverride',{width:960,height:540,deviceScaleFactor:1,mobile:false});
  await call('Page.navigate',{url:pathToFileURL(path.join(root,'preview.html')).href});
  for(let i=0;i<100;i++){await delay(100);if(await evaluate("document.readyState==='complete'&&!!window.ready"))break;}
  await evaluate("window.ready.then(()=>{window.setCaptureMode();const s=document.createElement('style');s.textContent='html,body,main{margin:0!important;padding:0!important;width:960px!important;height:540px!important;overflow:hidden}h1,p,button,#status{display:none!important}canvas{width:960px!important;height:540px!important;border-radius:0!important}';document.head.append(s);return true})");
  const fontState=await evaluate("Array.from(document.fonts).map(f=>({family:f.family,status:f.status})).filter(f=>f.family==='MikaLolita')");
  if(!fontState.some(f=>f.status==='loaded'))throw new Error('User-provided Lolita font was not loaded');
  await writeFile(path.join(root,'font-report.json'),JSON.stringify({passed:true,fonts:fontState},null,2));
  const clockLabels=await evaluate('({reference:clockLabels(true),interactive:clockLabels(false)})');
  if(!/^\d{2} : \d{2}$/.test(clockLabels.reference.time)||!/^\d{4}\.\d{2}\.\d{2}$/.test(clockLabels.reference.date))throw new Error('Unexpected clock/date format');
  await writeFile(path.join(root,'clock-report.json'),JSON.stringify({passed:true,...clockLabels},null,2));
  const stills=[[0,'01-companion'],[2.1,'02-pass-character'],[3.1,'03-approach-window'],[5.45,'04-window-meteor'],[7.15,'05-second-meteor']];
  for(const [time,name] of stills){await evaluate(`window.renderFrame(${time})`);const r=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(root,name+'.png'),Buffer.from(r.data,'base64'));}
  if(!process.argv.includes('--stills')){
    const fs=await import('node:fs/promises');await fs.mkdir(path.join(root,'Frames'),{recursive:true});
    for(let i=0;i<174;i++){
      await evaluate(`window.renderFrame(${i/15})`);
      const r=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});
      await writeFile(path.join(root,'Frames',String(i).padStart(4,'0')+'.png'),Buffer.from(r.data,'base64'));
      if(i%45===0)console.log('Rendered '+i+'/174');
    }
  }
  const uiStates=[];
  for(const t of [0,2.1,4.2,7.15,9.4,11.5])uiStates.push(await evaluate(`window.renderFrame(${t})`));
  if(!uiStates.every(s=>s.uiInWorld&&s.clockAlpha===.76&&s.menuAlpha===.78))throw new Error('Time/menu opacity changed during travel');
  await writeFile(path.join(root,'world-ui-report.json'),JSON.stringify({passed:true,states:uiStates},null,2));
  if(errors.length)throw new Error('Page exceptions: '+JSON.stringify(errors));
  console.log('Motion rendering complete.');
} catch(error){console.error(error.stack);process.exitCode=1;} finally {
  await writeFile(path.join(root,'render-report.json'),JSON.stringify({date:'2026-11-02',passed:!process.exitCode,errors,stillsOnly:process.argv.includes('--stills')},null,2));
  try { if(socket?.readyState===WebSocket.OPEN) await call('Browser.close'); } catch {}
  socket?.close();
  await delay(500);
  if(browser.exitCode===null) browser.kill();
  const resolved = await realpath(profile), boundary = await realpath(tmpdir());
  if(path.dirname(resolved).toLowerCase()!==boundary.toLowerCase()||!path.basename(resolved).startsWith('MikaVisualCheck-')||(await lstat(profile)).isSymbolicLink()) throw new Error('Unexpected browser profile path');
  await rm(resolved,{recursive:true,force:true,maxRetries:8,retryDelay:250});
}
