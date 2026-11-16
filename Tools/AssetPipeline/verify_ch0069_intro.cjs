// Probe the actual delivered binary using the bundled Spine runtime; no Unity rendering.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'../..');
const base=path.join(root,'Material/Mika_MemoryLobby');
const dir=path.join(base,'MemoryLobby/Assets/StreamingAssets/CH0069_Source');
const bundle=fs.readFileSync(path.join(root,'Material/Mika_MemoryLobby_Spine/preview/player.bundle.js'),'utf8');
const marker=bundle.indexOf('// player/player.mjs');if(marker<0)throw Error('Runtime boundary missing');
const context=vm.createContext({console,Math,Date,performance});
vm.runInContext(bundle.slice(0,marker)+'\nglobalThis.R={TextureAtlas,AtlasAttachmentLoader,SkeletonBinary,Skeleton,AnimationState,AnimationStateData,Physics};',context);
const R=context.R,atlas=new R.TextureAtlas(fs.readFileSync(path.join(dir,'CH0069_home.atlas.txt'),'utf8'));
for(const page of atlas.pages)page.setTexture({setFilters(){},setWraps(){}});
const data=new R.SkeletonBinary(new R.AtlasAttachmentLoader(atlas)).readSkeletonData(new Uint8Array(fs.readFileSync(path.join(dir,'CH0069_home.skel.bytes'))));
const intro=data.findAnimation('Start_Idle_01');if(!intro||intro.duration<=0)throw Error('No intro');
const skeleton=new R.Skeleton(data),state=new R.AnimationState(new R.AnimationStateData(data));
state.setAnimation(0,'Start_Idle_01',false);state.addAnimation(0,'Idle_01',true,0);
const samples=[];let finite=true;
for(let frame=0;frame<=22*30;frame++){
 state.update(1/30);state.apply(skeleton);skeleton.updateWorldTransform(R.Physics.update);
 finite&&=skeleton.bones.every(b=>Number.isFinite(b.worldX)&&Number.isFinite(b.worldY));
 if([0,150,300,450,570,630].includes(frame))samples.push({seconds:(frame+1)/30,animation:state.getCurrent(0).animation.name,bones:skeleton.bones.filter(b=>/^(root|Root|ALL|All|PC|Face|Head_Rot)$/.test(b.data.name)).map(b=>({name:b.data.name,x:b.worldX,y:b.worldY,a:b.a,d:b.d}))});
}
const passed=finite&&state.getCurrent(0).animation.name==='Idle_01';if(!passed)throw Error('Intro queue failed');
const report={date:'2026-11-06',method:'Offline Spine JS binary/queue probe; no Unity visual playback',intro:intro.name,durationSeconds:intro.duration,timelines:intro.timelines.length,finite,queuedIdleReached:passed,samples};
fs.writeFileSync(path.join(base,'CH0069_intro_verification.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report));
