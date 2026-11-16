import json, hashlib
from pathlib import Path
from datetime import datetime, timezone
import xml.etree.ElementTree as ET
P=Path(__file__).resolve().parents[1]; R=P/'Docs/Validation/V1Closeout'; D=P/'Releases/Gift-V1-Closeout'
def read(n): return json.loads((R/n).read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
tests={}
for mode in ('EditMode','PlayMode'):
    path=R/f'Entry.{mode}.xml'; xml=ET.parse(path).getroot()
    tests[mode]={'passed':xml.attrib['result']=='Passed','count':int(xml.attrib['passed']),'sha256':sha(path)}
names=['GiftCompanion.exe','GiftCompanion_Data/Managed/GiftCompanion.Core.dll','GiftCompanion_Data/Managed/GiftCompanion.Runtime.dll']
native=read('Native/native.json'); package=read('package.json'); build=read('player_build.json')
checks={'buildPassed':build['passed'],'nativePassed':native['passed'],'packagePassed':package['passed'],'manifestMatches':sha(D/'Release/release_manifest.json')==package['manifestSha256'],
        'packagedProgramsMatchValidatedBuild':all(sha(D/n)==sha(P/'Builds/V1Closeout'/n) for n in names)}
result={'passed':all(checks.values()) and all(t['passed'] for t in tests.values()),'createdAtUtc':datetime.now(timezone.utc).isoformat(),'playerAcceptance':'pending',
        'schema':10,'backupFormat':1,'startupMode':'companion','petEntryEnabled':False,'tests':tests,'native':native,'package':package,'checks':checks,
        'artifacts':[{'path':n,'sha256':sha(D/n)} for n in names],
        'limits':['This round validates entry changes with silent isolated data, not new real API requests or voice quality.','Historical multi-mode tests explicitly opt in via isolated app_entry.json.','No database migration or model changes; previous device and long-use limitations remain.']}
(R/'acceptance.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'passed':result['passed'],'tests':tests,'checks':checks},ensure_ascii=False))
if not result['passed']: raise RuntimeError('Incomplete closeout acceptance')
