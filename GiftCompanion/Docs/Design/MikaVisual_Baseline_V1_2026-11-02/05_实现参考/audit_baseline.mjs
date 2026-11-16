// Local Edge/CDP review. No dependencies, network services or player data.
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, writeFile, realpath, lstat, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
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
  const checkDir=path.join(root,'06_验证记录','归档检查');const fs=await import('node:fs/promises');await fs.mkdir(checkDir,{recursive:true});
  await call('Network.enable');await call('Network.setBlockedURLs',{urls:['http://*','https://*']});
  await call('Page.navigate',{url:pathToFileURL(path.join(root,'01_确认效果','preview.html')).href});
  for(let i=0;i<100;i++){await delay(100);if(await evaluate("document.readyState==='complete'&&!!window.ready"))break;}
  await evaluate("window.ready.then(()=>{window.setCaptureMode();const s=document.createElement('style');s.textContent='html,body,main{margin:0!important;padding:0!important;width:960px!important;height:540px!important;overflow:hidden}h1,p,button,#status{display:none!important}canvas{width:960px!important;height:540px!important;border-radius:0!important}';document.head.append(s);return true})");
  const fonts=await evaluate("Array.from(document.fonts).filter(f=>f.family==='MikaLolita').map(f=>({family:f.family,status:f.status}))");
  if(!fonts.some(x=>x.status==='loaded'))throw new Error('Archived font did not load');
  const state=await evaluate('window.renderFrame(5.45)');if(!state.atWindow||!state.uiInWorld)throw new Error('Archived endpoint not rendered');
  const resources=await evaluate("performance.getEntriesByType('resource').map(x=>x.name)");
  const archivePath=path.resolve(root).toLowerCase()+path.sep;
  if(!resources.every(x=>x.startsWith('file:')&&fileURLToPath(x).toLowerCase().startsWith(archivePath)))throw new Error('Preview dependency escaped archive');
  let shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(checkDir,'归档终点.png'),Buffer.from(shot.data,'base64'));
  const report={date:'2026-11-02',passed:true,offlineNetworkBlocked:true,allPreviewDependenciesInsideArchive:true,fonts,endpoint:state,resources,errors};
  await call('Emulation.setDeviceMetricsOverride',{width:1440,height:1050,deviceScaleFactor:1,mobile:false});
  for(const [file,output] of [['开始阅读.html','阅读入口.png'],['设计介绍报告.html','设计报告首页.png']]){
    await call('Page.navigate',{url:pathToFileURL(path.join(root,file)).href});
    for(let i=0;i<80;i++){await delay(100);if(await evaluate("document.readyState==='complete'"))break;}
    await evaluate('document.fonts.ready');await delay(250);
    const overflow=await evaluate('document.documentElement.scrollWidth>innerWidth');if(overflow)throw new Error('Report overflows viewport');
    shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await writeFile(path.join(checkDir,output),Buffer.from(shot.data,'base64'));
  }
  if(errors.length)throw new Error('Page exception in archive');
  await writeFile(path.join(checkDir,'browser-report.json'),JSON.stringify(report,null,2));console.log('Offline archive preview and report rendering passed.');
} catch(error){console.error(error.stack);process.exitCode=1;} finally {
  try { if(socket?.readyState===WebSocket.OPEN) await call('Browser.close'); } catch {}
  socket?.close();
  await delay(500);
  if(browser.exitCode===null) browser.kill();
  const resolved = await realpath(profile), boundary = await realpath(tmpdir());
  if(path.dirname(resolved).toLowerCase()!==boundary.toLowerCase()||!path.basename(resolved).startsWith('MikaVisualCheck-')||(await lstat(profile)).isSymbolicLink()) throw new Error('Unexpected browser profile path');
  await rm(resolved,{recursive:true,force:true,maxRetries:8,retryDelay:250});
}
