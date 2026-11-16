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
  await call('Page.navigate',{url:pathToFileURL(path.join(root,'revision8.html')).href});
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
  await screenshot('r8-swiss-roll');
  await click('changeEmblem');
  await move(900,950);
  await check('Controls hide again after mouse use',"getComputedStyle(document.getElementById('musicActions')).visibility==='hidden'");
  await check('Clock projected and masked in sky',"document.querySelectorAll('.time-plane text').length===1&&!document.querySelector('foreignObject')&&getComputedStyle(document.querySelector('.sky-clock')).maskImage.includes('foreground.png')&&getComputedStyle(document.querySelector('.sky-clock')).pointerEvents==='none'");
  await check('Music controls have no enclosing plate',"['musicActions','volumePopover','pauseTrack'].every(id=>getComputedStyle(document.getElementById(id)).backgroundColor==='rgba(0, 0, 0, 0)')");
  await check('Complete clock fits measured window with clearance',"(()=>{const p=calibratedClock.pane;return window.clockFit.corners.every(c=>p.every((a,i)=>{const b=p[(i+1)%p.length],x=b[0]-a[0],y=b[1]-a[1];return (x*(c[1]-a[1])-y*(c[0]-a[0]))/Math.hypot(x,y)>=20}))})()");
  await check('Four digits without separator',"document.getElementById('skyTime').textContent==='2106'");
  await check('Clock has equal top and bottom widths and four right angles',"(()=>{const q=window.clockFit.corners,d=(a,b)=>[b[0]-a[0],b[1]-a[1]],edges=q.map((p,i)=>d(p,q[(i+1)%4]));return Math.abs(Math.hypot(...edges[0])-Math.hypot(...edges[2]))<1e-6&&edges.every((e,i)=>Math.abs(e[0]*edges[(i+1)%4][0]+e[1]*edges[(i+1)%4][1])<1e-6)})()");
  await check('Clock preserves width and tilt with modest height increase',"Math.abs(calibratedClock.clockHeight-229.39274828645496)<1e-6&&Math.abs(calibratedClock.clockWidth-383.5008977725711)<1e-6&&window.clockFit.verticalScale===1&&window.clockFit.horizontalScale>0&&Math.abs(window.clockFit.angleDegrees-26.816574824784176)<1e-6&&!document.querySelector('[style*=matrix3d]')");
  const quietMenuColor=await evaluate("getComputedStyle(document.getElementById('sceneMenuIdle')).color");
  const menuPoint=await evaluate("(()=>{const r=document.getElementById('sceneMenuIdle').getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2}})()");
  await move(menuPoint.x,menuPoint.y);
  await check('Wall menu lights up on approach',`getComputedStyle(document.getElementById('sceneMenuIdle')).color!==${JSON.stringify(quietMenuColor)}&&getComputedStyle(document.getElementById('sceneMenuIdle')).backgroundColor==='rgba(0, 0, 0, 0)'`);
  await screenshot('r8-menu-actual-hover');
  await click('sceneMenuIdle');
  await check('Projected menu entry opens directory',"document.querySelector('[data-page=menu]').classList.contains('active')");
  await evaluate("show('idle')");await move(900,950);
  await check('Wall menu dims after mouse leaves',`getComputedStyle(document.getElementById('sceneMenuIdle')).color===${JSON.stringify(quietMenuColor)}`);
  await check('Removed independent music view and dialogue input',"!document.querySelector('[data-page=music],.mini-music,.playlist,.sleeve,input[type=text],textarea')&&!Array.from(document.querySelectorAll('.menu-row')).some(x=>x.textContent.includes('背景音乐'))");
  for(const view of ['idle','controls','dialogue','menuhover','menu','diary','settings','home','windowcheck']) {
    await evaluate(`show(${JSON.stringify(view)})`);await move(900,950);
    if(view==='controls') await check('Controls review state visible', "(()=>{let s=getComputedStyle(document.getElementById('musicActions'));return s.visibility==='visible'&&s.opacity==='1'?true:{className:document.getElementById('scene').className,visibility:s.visibility,opacity:s.opacity}})()");
    if(view==='home') await check('Home has no scene clock or music overlay',"getComputedStyle(document.querySelector('.sky-clock')).display==='none'&&getComputedStyle(document.getElementById('musicHub')).display==='none'");
    await screenshot('r8-'+view);
  }
  await evaluate("show('windowcheck');document.querySelector('.sky-clock').style.visibility='hidden'");
  await screenshot('r8-window-clock-off');
  await evaluate("document.querySelector('.sky-clock').style.visibility='visible'");
  await screenshot('r8-window-clock-on');
  await writeFile(path.join(root,'window_clock_screen_v8.json'),JSON.stringify(await evaluate("({scene:document.getElementById('scene').getBoundingClientRect().toJSON(),fit:window.clockFit,pane:calibratedClock.pane,safe:calibratedClock.safePolygon})"),null,2));
  await call('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  await call('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  await evaluate("show('idle');document.getElementById('trackTitle').focus()");await delay(250);
  await check('Keyboard focus reveals controls',"getComputedStyle(document.getElementById('musicActions')).visibility==='visible'");
  await call('Emulation.setDeviceMetricsOverride',{width:1000,height:850,deviceScaleFactor:1,mobile:false});
  await evaluate("show('dialogue');fit()");await delay(250);
  await check('Small window fits scene horizontally',"document.getElementById('scene').getBoundingClientRect().right<=innerWidth&&!document.documentElement.scrollLeft");
  await screenshot('r8-small');
  // Isolate the actual fitted SVG at 1:1 pixels. This avoids unrelated anti-aliasing
  // changes in the review frame/guide when Chromium changes compositing layers.
  await evaluate("window.removeEventListener('resize',fit);document.activeElement?.blur()");
  await delay(100); // Let the music-hub focusout callback settle before replacing review DOM.
  await call('Emulation.setDeviceMetricsOverride',{width:1280,height:720,deviceScaleFactor:1,mobile:false});
  await evaluate(`(()=>{const svg=document.querySelector('.sky-clock').outerHTML;document.body.innerHTML='<style>html,body{margin:0;background:#000;overflow:hidden}svg.sky-clock{position:fixed;inset:0;width:1280px;height:720px;opacity:1;visibility:visible;mask-image:none;pointer-events:none}</style>'+svg;})()`);
  await evaluate("document.querySelector('.time-plane').parentElement.removeAttribute('clip-path')");
  await screenshot('r8-window-ink-on');
  await evaluate("document.querySelector('.sky-clock').style.visibility='hidden'");
  await screenshot('r8-window-ink-off');
  await writeFile(path.join(root,'window_clock_ink_screen_v8.json'),JSON.stringify(await evaluate("({scene:{x:0,y:0,width:1280,height:720},fit:window.clockFit,pane:calibratedClock.pane,safe:calibratedClock.safePolygon})"),null,2));
  if(errors.length) throw new Error('Page exceptions detected');
  console.log(JSON.stringify({passed:true,checks},null,2));
} catch(error) {
  console.error(error.stack);process.exitCode=1;
  try {console.error(await evaluate("JSON.stringify({hub:document.getElementById('musicHub').getBoundingClientRect().toJSON(),hover:document.getElementById('musicHub').matches(':hover'),active:getComputedStyle(document.getElementById('musicActions')).opacity,buttons:document.querySelectorAll('.hub-button').length,errors:document.readyState})"));await screenshot('r8-check-failure');} catch {}
} finally {
  await writeFile(path.join(root,'revision8_checks.json'), JSON.stringify({date:'2026-10-31',passed:!process.exitCode,checks,errors},null,2));
  try { if(socket?.readyState===WebSocket.OPEN) await call('Browser.close'); } catch {}
  socket?.close();
  await delay(500);
  if(browser.exitCode===null) browser.kill();
  const resolved = await realpath(profile), boundary = await realpath(tmpdir());
  if(path.dirname(resolved).toLowerCase()!==boundary.toLowerCase()||!path.basename(resolved).startsWith('MikaVisualCheck-')||(await lstat(profile)).isSymbolicLink()) throw new Error('Unexpected browser profile path');
  await rm(resolved,{recursive:true,force:true,maxRetries:8,retryDelay:250});
}
