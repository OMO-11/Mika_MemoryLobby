// Read-only audit of the delivered asset pack using its bundled Spine core.
// Run from the repository root: node Tools/AssetPipeline/audit_memorylobby_spine.cjs
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
const pack = path.join(root, 'Material/Mika_MemoryLobby_Spine');
const out = path.join(root, 'Docs/Audits/Spine/MemoryLobby');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const bundle = fs.readFileSync(path.join(pack, 'preview/player.bundle.js'), 'utf8');
const marker = '// player/player.mjs';
if (!bundle.includes(marker)) throw Error('Runtime boundary missing');
const context = vm.createContext({console, Math, Date, performance});
vm.runInContext(bundle.slice(0, bundle.indexOf(marker)) +
  '\nglobalThis.auditRuntime = {TextureAtlas, AtlasAttachmentLoader, SkeletonBinary, Skeleton, AnimationState, AnimationStateData, Physics};', context);
const R = context.auditRuntime;
const report = {
  auditedAt: new Date().toISOString(), standard: '项目总宪法.md',
  assetPack: 'Material/Mika_MemoryLobby_Spine',
  method: 'Execute only bundled runtime in Node VM; parse actual binary and atlas; simulate every animation at 30 Hz. No canvas rendering, GPU, browser, or Unity playback.',
  runtime: {source: 'preview/player.bundle.js', sha256: hash(Buffer.from(bundle)), claimedVersion: 'spine-core 4.2.120 (delivery documentation; no independent package version metadata)'},
  checksums: [], manifestFiles: [], models: {}
};
for (const line of fs.readFileSync(path.join(pack, 'Diagnostics/checksums.sha256'), 'utf8').trim().split(/\r?\n/)) {
  const match = line.match(/^([0-9a-f]{64})\s+(.+)$/);
  if (!match) throw Error('Invalid checksum line');
  const file = path.resolve(pack, match[2]);
  if (!file.startsWith(pack + path.sep)) throw Error('Checksum path escapes pack');
  const exists = fs.existsSync(file);
  const actual = exists ? hash(fs.readFileSync(file)) : null;
  report.checksums.push({file: match[2], exists, expected: match[1], actual, pass: actual === match[1]});
}
const manifest = JSON.parse(fs.readFileSync(path.join(pack, 'manifest.json')));
for (const files of Object.values(manifest.files)) for (const item of files) {
  const file = path.resolve(pack, item.path);
  if (!file.startsWith(pack + path.sep)) throw Error('Manifest path escapes pack');
  const bytes = fs.readFileSync(file);
  report.manifestFiles.push({file: item.path, pass: bytes.length === item.bytes && hash(bytes) === item.sha256});
}
for (const name of ['CH0069_home', 'CH0294_home']) {
  const dir = path.join(pack, 'spine', name);
  const atlas = new R.TextureAtlas(fs.readFileSync(path.join(dir, name + '.atlas'), 'utf8'));
  const pages = atlas.pages.map(page => {
    const bytes = fs.readFileSync(path.join(dir, page.name));
    if (bytes.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a') throw Error('Invalid PNG signature');
    const width = bytes.readUInt32BE(16), height = bytes.readUInt32BE(20);
    // Texture handle only; PNG decoding is checked independently with Pillow.
    page.setTexture({setFilters() {}, setWraps() {}});
    return {name: page.name, width, height, atlasWidth: page.width, atlasHeight: page.height,
      dimensionsMatch: width === page.width && height === page.height, pmaDeclared: page.pma};
  });
  const data = new R.SkeletonBinary(new R.AtlasAttachmentLoader(atlas)).readSkeletonData(new Uint8Array(fs.readFileSync(path.join(dir, name + '.skel'))));
  const attachmentTypes = {}, missingRegions = [];
  for (const skin of data.skins) for (const entry of skin.getAttachments()) {
    const att = entry.attachment, type = att.constructor.name;
    attachmentTypes[type] = (attachmentTypes[type] || 0) + 1;
    if ('region' in att && !att.region) missingRegions.push({slotIndex: entry.slotIndex, name: entry.name});
  }
  const blendModes = {};
  for (const slot of data.slots) blendModes[slot.blendMode] = (blendModes[slot.blendMode] || 0) + 1;
  const animations = [];
  for (const animation of data.animations) {
    const skeleton = new R.Skeleton(data);
    skeleton.setSkinByName(data.skins[0].name);
    skeleton.setToSetupPose();
    const state = new R.AnimationState(new R.AnimationStateData(data));
    const events = [];
    state.addListener({event: (_entry, event) => events.push({name: event.data.name, time: event.time})});
    state.setAnimation(0, animation.name, false);
    const frameCount = Math.max(2, Math.ceil(animation.duration * 30) + 2);
    let finite = true, changed = false, initial = null;
    for (let frame = 0; frame < frameCount; frame++) {
      const dt = frame === 0 ? 0 : 1 / 30;
      skeleton.update(dt); state.update(dt); state.apply(skeleton);
      skeleton.updateWorldTransform(R.Physics.update);
      const values = skeleton.bones.flatMap(b => [b.a, b.b, b.c, b.d, b.worldX, b.worldY]);
      finite = finite && values.every(Number.isFinite);
      if (!initial) initial = values;
      else changed = changed || values.some((value, i) => Math.abs(value - initial[i]) > 1e-6);
    }
    const timelineTypes = {};
    for (const timeline of animation.timelines) timelineTypes[timeline.constructor.name] = (timelineTypes[timeline.constructor.name] || 0) + 1;
    animations.push({name: animation.name, duration: animation.duration, frameCount, finiteBoneTransforms: finite,
      boneTransformsChanged: changed, timelineTypes, events});
  }
  const counts = Object.fromEntries(['bones', 'slots', 'skins', 'events', 'animations', 'ikConstraints', 'transformConstraints', 'pathConstraints', 'physicsConstraints'].map(key => [key, data[key].length]));
  counts.atlasPages = atlas.pages.length; counts.atlasRegions = atlas.regions.length;
  const metadata = JSON.parse(fs.readFileSync(path.join(pack, 'metadata', name + '.metadata.json')));
  report.models[name] = {version: data.version, referenceScale: data.referenceScale, fps: data.fps,
    bounds: {x: data.x, y: data.y, width: data.width, height: data.height}, counts,
    metadataCountsMatch: Object.keys(counts).every(key => counts[key] === metadata.counts[key]),
    pages, uncompressedRGBA32MiB: pages.reduce((sum, page) => sum + page.width * page.height * 4, 0) / 1024 ** 2,
    attachmentTypes, missingRegions, blendModes, animations, events: data.events.map(event => event.name),
    explicitlyNamedThinkingAnimations: data.animations.filter(a => /think/i.test(a.name)).map(a => a.name)};
}
const config = JSON.parse(fs.readFileSync(path.join(root, 'GiftCompanion/Assets/Characters/MainCharacter/Config/character_manifest.json')));
report.project = {unityVersion: fs.readFileSync(path.join(root, 'GiftCompanion/ProjectSettings/ProjectVersion.txt'), 'utf8').trim(),
  characterManifestStatus: config.status, companion: config.companion};
report.summary = {checksumEntries: report.checksums.length, checksumFailures: report.checksums.filter(x => !x.pass),
  manifestEntries: report.manifestFiles.length, manifestFailures: report.manifestFiles.filter(x => !x.pass),
  animationsSimulated: Object.values(report.models).reduce((sum, m) => sum + m.animations.length, 0),
  nonfiniteAnimations: Object.entries(report.models).flatMap(([model, m]) => m.animations.filter(a => !a.finiteBoneTransforms).map(a => model + '/' + a.name))};
fs.mkdirSync(out, {recursive: true});
fs.writeFileSync(path.join(out, 'runtime_audit.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify({summary: report.summary, models: Object.fromEntries(Object.entries(report.models).map(([name, m]) => [name,
  {version: m.version, counts: m.counts, attachmentTypes: m.attachmentTypes, missingRegions: m.missingRegions,
   metadataCountsMatch: m.metadataCountsMatch, blendModes: m.blendModes, idle: m.animations.find(a => a.name === 'Idle_01'),
   thinking: m.explicitlyNamedThinkingAnimations}]))}, null, 2));
