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
  await call('Emulation.setDeviceMetricsOverride',{width:1440,height:1050,deviceScaleFactor:1,mobile:false});
  await call('Emulation.setDeviceMetricsOverride',{width:1440,height:1050,deviceScaleFactor:1,mobile:false});
  await call('Network.enable');await call('Network.setBlockedURLs',{urls:['http://*','https://*']});
  await call('Page.navigate',{url:pathToFileURL(path.join(root,'preview.html')).href});
  for(let i=0;i<100;i++){await delay(100);if(await evaluate("document.readyState==='complete'&&!!window.previewReady"))break;}
  await evaluate('window.previewReady');
  const font=await evaluate("Array.from(document.fonts).some(f=>f.family==='MikaLolita'&&f.status==='loaded')");if(!font)throw new Error('Archived Lolita missing');
  await evaluate("document.querySelector('[data-section=diary]').click()");
  if(!await evaluate("page==='diary-air'&&!document.querySelector('[data-view=diary-paper]')&&getComputedStyle(document.querySelector('.diary-surface')).backgroundImage==='none'"))throw new Error('Floating diary was not the sole selected route');
  const clip=await evaluate("(()=>{const r=document.getElementById('frame').getBoundingClientRect();return{x:r.x,y:r.y,width:r.width,height:r.height,scale:1}})()");
  let shot=await call('Page.captureScreenshot',{format:'png',clip,captureBeyondViewport:false});await writeFile(path.join(root,'验证','归档浮字日记.png'),Buffer.from(shot.data,'base64'));
  await evaluate("show('diary-paper')");if(!await evaluate("page==='diary-air'&&content.className===''"))throw new Error('Old alternative route still active');
  for(const view of ['settings','history','persona','spaces'])await evaluate(`show(${JSON.stringify(view)})`);
  const resources=await evaluate("performance.getEntriesByType('resource').map(x=>x.name)");const boundary=path.resolve(root,'..').toLowerCase()+path.sep;
  if(!resources.every(x=>x.startsWith('file:')&&fileURLToPath(x).toLowerCase().startsWith(boundary)))throw new Error('Dependency outside total archive');
  await call('Page.navigate',{url:pathToFileURL(path.join(root,'内页设计总结.html')).href});for(let i=0;i<80;i++){await delay(100);if(await evaluate("document.readyState==='complete'"))break;}
  if(await evaluate('document.documentElement.scrollWidth>innerWidth'))throw new Error('Summary horizontal overflow');
  shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(root,'验证','内页总结首页.png'),Buffer.from(shot.data,'base64'));
  if(errors.length)throw new Error('Page exceptions');
  await writeFile(path.join(root,'验证','归档浏览器报告.json'),JSON.stringify({date:'2026-11-02',passed:true,fontLoaded:font,selectedDiary:'diary-air',oldPaperRouteRedirects:true,networkBlocked:true,allDependenciesInsideTotalArchive:true,resources,errors},null,2));
  console.log('Approved inner pages load offline; floating diary is default and sole active diary.');
} catch(error){console.error(error.stack);process.exitCode=1;} finally {
  try { if(socket?.readyState===WebSocket.OPEN) await call('Browser.close'); } catch {}
  socket?.close();
  await delay(500);
  if(browser.exitCode===null) browser.kill();
  const resolved = await realpath(profile), boundary = await realpath(tmpdir());
  if(path.dirname(resolved).toLowerCase()!==boundary.toLowerCase()||!path.basename(resolved).startsWith('MikaVisualCheck-')||(await lstat(profile)).isSymbolicLink()) throw new Error('Unexpected browser profile path');
  await rm(resolved,{recursive:true,force:true,maxRetries:8,retryDelay:250});
}
