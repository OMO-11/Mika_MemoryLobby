import {spawn} from 'node:child_process';
import {mkdtemp,readFile,writeFile,readdir} from 'node:fs/promises';
import os from 'node:os';import path from 'node:path';
const root=path.resolve('GiftCompanion/Assets/App/Companion/Resources/MikaVisual/Icons');
const profile=await mkdtemp(path.join(os.tmpdir(),'MikaV21Icons-'));
const proc=spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--disable-gpu','--no-first-run','--disable-background-networking','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
let socket; const pending=new Map();let serial=0;const delay=ms=>new Promise(r=>setTimeout(r,ms));
function call(method,params={}){return new Promise((resolve,reject)=>{const id=++serial;const timer=setTimeout(()=>{pending.delete(id);reject(Error('timeout '+method))},10000);pending.set(id,{resolve,reject,timer});socket.send(JSON.stringify({id,method,params}));})}
try{let port;for(let i=0;i<100;i++){try{port=(await readFile(path.join(profile,'DevToolsActivePort'),'utf8')).split('\n')[0];break}catch{await delay(100)}}
const target=await(await fetch('http://127.0.0.1:'+port+'/json/new?about:blank',{method:'PUT'})).json();socket=new WebSocket(target.webSocketDebuggerUrl);await new Promise((r,j)=>{socket.onopen=r;socket.onerror=j});socket.onmessage=e=>{const m=JSON.parse(e.data),p=pending.get(m.id);if(p){clearTimeout(p.timer);pending.delete(m.id);m.error?p.reject(m.error):p.resolve(m.result)}};
await call('Page.enable');await call('Emulation.setDeviceMetricsOverride',{width:256,height:256,deviceScaleFactor:1,mobile:false});await call('Emulation.setDefaultBackgroundColorOverride',{color:{r:0,g:0,b:0,a:0}});
for(const file of await readdir(root)){if(!file.endsWith('.svg.txt'))continue;const svg=await readFile(path.join(root,file),'utf8');await call('Page.navigate',{url:'data:text/html;charset=utf-8,'+encodeURIComponent('<style>html,body{margin:0;background:transparent}svg{width:256px;height:256px}</style>'+svg)});await delay(150);const r=await call('Page.captureScreenshot',{format:'png'});await writeFile(path.join(root,file.replace('.svg.txt','.png')),Buffer.from(r.data,'base64'));}console.log('Seven transparent icon textures generated');
}finally{try{await call('Browser.close')}catch{}socket?.close();if(proc.exitCode===null)proc.kill()}
