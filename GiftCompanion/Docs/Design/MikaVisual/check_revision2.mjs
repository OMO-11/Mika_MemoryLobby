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
  await call('Page.navigate',{url:pathToFileURL(path.join(root,'revision2.html')).href});
  for(let n=0;n<100;n++){await delay(100);if(await evaluate("document.readyState==='complete'&&typeof show==='function'"))break;}
  await evaluate('document.fonts.ready.then(()=>true)');await delay(300);
  await move(900,950);
  await check('Default music controls hidden',"getComputedStyle(document.getElementById('musicActions')).visibility==='hidden'");
  await move(240,220);
  await check('Pointer proximity reveals four controls',"getComputedStyle(document.getElementById('musicActions')).opacity==='1'&&document.querySelectorAll('.hub-button').length===4");
  await click('nextTrack');
  await check('Next updates current song',"document.getElementById('trackTitle').textContent==='You are my princess'");
  await click('previousTrack');
  await check('Previous updates current song',"document.getElementById('trackTitle').textContent==='Daily Routine 247'");
  await click('pauseTrack');
  await check('Pause changes to resume',"document.getElementById('playUse').getAttribute('href')==='#play'&&document.getElementById('pauseTrack').getAttribute('aria-label')==='继续播放'");
  await click('pauseTrack');
  await click('volumeButton');
  await check('Volume button opens slider',"getComputedStyle(document.getElementById('volumePopover')).visibility==='visible'");
  await evaluate("(()=>{let r=document.getElementById('hubVolume');r.value=62;r.dispatchEvent(new Event('input',{bubbles:true}));})()");
  await check('Volume label follows adjustment',"document.getElementById('hubVolumeValue').textContent==='62%'");
  await evaluate("document.getElementById('hubVolume').value=35;document.getElementById('hubVolume').dispatchEvent(new Event('input',{bubbles:true}))");
  await click('changeEmblem');
  await check('Emblem switches to Swiss roll',"document.getElementById('emblemUse').getAttribute('href')==='#swiss-roll'");
  await screenshot('r2-swiss-roll');
  await click('changeEmblem');
  await move(900,950);
  await check('Controls hide again after mouse use',"getComputedStyle(document.getElementById('musicActions')).visibility==='hidden'");
  await check('Clock transparent, no secondary text',"getComputedStyle(document.querySelector('.corner-time')).backgroundColor==='rgba(0, 0, 0, 0)'&&!document.querySelector('.corner-time small')");
  await check('Removed independent music view and dialogue input',"!document.querySelector('[data-page=music],.mini-music,.playlist,.sleeve,input[type=text],textarea')&&!Array.from(document.querySelectorAll('.menu-row')).some(x=>x.textContent.includes('背景音乐'))");
  for(const view of ['idle','controls','dialogue','menu','diary','settings','home']) {
    await evaluate(`show(${JSON.stringify(view)})`);await move(900,950);
    if(view==='controls') await check('Controls review state visible', "(()=>{let s=getComputedStyle(document.getElementById('musicActions'));return s.visibility==='visible'&&s.opacity==='1'?true:{className:document.getElementById('scene').className,visibility:s.visibility,opacity:s.opacity}})()");
    await screenshot('r2-'+view);
  }
  await call('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  await call('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  await evaluate("show('idle');document.getElementById('trackTitle').focus()");await delay(250);
  await check('Keyboard focus reveals controls',"getComputedStyle(document.getElementById('musicActions')).visibility==='visible'");
  await call('Emulation.setDeviceMetricsOverride',{width:1000,height:850,deviceScaleFactor:1,mobile:false});
  await evaluate("show('dialogue');fit()");await delay(250);
  await check('Small window fits scene horizontally',"document.getElementById('scene').getBoundingClientRect().right<=innerWidth&&!document.documentElement.scrollLeft");
  await screenshot('r2-small');
  if(errors.length) throw new Error('Page exceptions detected');
  console.log(JSON.stringify({passed:true,checks},null,2));
} catch(error) {
  console.error(error.stack);process.exitCode=1;
} finally {
  await writeFile(path.join(root,'revision2_checks.json'), JSON.stringify({date:'2026-10-30',passed:!process.exitCode,checks,errors},null,2));
  try { if(socket?.readyState===WebSocket.OPEN) await call('Browser.close'); } catch {}
  socket?.close();
  await delay(500);
  if(browser.exitCode===null) browser.kill();
  const resolved = await realpath(profile), boundary = await realpath(tmpdir());
  if(path.dirname(resolved).toLowerCase()!==boundary.toLowerCase()||!path.basename(resolved).startsWith('MikaVisualCheck-')||(await lstat(profile)).isSymbolicLink()) throw new Error('Unexpected browser profile path');
  await rm(resolved,{recursive:true,force:true,maxRetries:8,retryDelay:250});
}
