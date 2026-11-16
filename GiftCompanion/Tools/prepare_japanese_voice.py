"""Stage newly supplied Japanese weights without changing the Mandarin default."""
import hashlib
import io
import json
from pathlib import Path
import wave
import zipfile
from prepare_voice_assets import decoded

PROJECT=Path(__file__).resolve().parents[1]
SOURCE=PROJECT.parent/'Material/GPT-SoVITS'
RUNTIME=PROJECT/'Runtime/GPT-SoVITS'
REPORT=PROJECT/'Docs/Validation/Phase0'


def run():
    package=SOURCE/'未花-jp.zip'
    records=[]
    with zipfile.ZipFile(package) as archive:
        for suffix, name in [('.ckpt','gpt.ckpt'),('.pth','sovits.pth'),('.wav','reference.wav')]:
            entries=[i for i in archive.infolist() if i.filename.endswith(suffix)]
            if len(entries)!=1:raise ValueError(f'Expected one Japanese {suffix}')
            info=entries[0];data=archive.read(info)
            target=RUNTIME/'models/mika-ja'/name
            if target.exists() and target.read_bytes()!=data:raise ValueError('Changed Japanese target preserved.')
            target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
            records.append({'sourceEntry':decoded(info),'target':target.relative_to(RUNTIME).as_posix(),
                            'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
            if suffix=='.wav':
                prompt=Path(decoded(info)).stem.removeprefix('【默认】')
                with wave.open(io.BytesIO(data)) as audio:
                    wav={'seconds':audio.getnframes()/audio.getframerate(),'sampleRate':audio.getframerate(),'channels':audio.getnchannels()}
    voice={'schemaVersion':1,'language':'ja','modelVersionDeclaredBySource':'v4','apiBase':'http://127.0.0.1:9880',
           'referenceAudio':'models/mika-ja/reference.wav','promptText':prompt,'promptLanguage':'ja','textLanguage':'ja',
           'mediaType':'wav','streamingMode':False,'runtimeVerified':False}
    (RUNTIME/'voice-ja.json').write_text(json.dumps(voice,ensure_ascii=False,indent=2)+'\n','utf-8')
    config=(RUNTIME/'tts_infer.yaml').read_text('utf-8').replace('models/mika/','models/mika-ja/')
    (RUNTIME/'tts_infer-ja.yaml').write_text(config,'utf-8')
    hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in SOURCE.glob('*.zip')}
    receipt={'scope':'japanese-voice-staging','modelArchivesSHA256':hashes,'modelArchivesAreIdentical':len(set(hashes.values()))==1,
             'defaultLanguageUnchanged':'zh','assets':records,'referenceWav':wav,'runtimeVerified':False}
    (REPORT/'japanese_voice_import.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf-8')
    print(f'Japanese model staged; reference={wav["seconds"]:.2f}s. Mandarin remains default.')


if __name__=='__main__':run()
