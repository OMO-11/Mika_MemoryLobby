"""Derive short references from supplied OGG/transcript pairs; never modify source or weights.

Run with the existing runtime Python (soundfile/numpy), from the workspace root.
The cuts sit inside visible silence runs and follow sentence boundaries in the supplied text.
This is transcript/silence alignment, not an ASR or a claim of subjective voice acceptance.
"""
from pathlib import Path
import hashlib
import json
import soundfile as sf

PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parent / 'Material/GPT-SoVITS'
DEST = PROJECT / 'Assets/StreamingAssets/Voice'
SPECS = [
    ('ja-bright', '日文参考音频/CH0069_Lobby_1.ogg', 9.85, 'ん～!今日もいい天気だね！こんな日に仕事ばかりなんて、勿体なくない？', 'ja', ['neutral', 'happy', 'surprised'], 1.0),
    ('ja-shy', '日文参考音频/CH0069_Lobby_2.ogg', None, 'あのさ…当番って、こういうのなの？べ、別に何か期待してるわけじゃ…。', 'ja', ['shy'], .96),
    ('zh-soft', '中文参考音频/ch0069_exweapon_get.ogg', 6.9, '啊……老师对我这么好，我当然很开心……', 'zh', ['neutral', 'shy', 'concern', 'sad', 'sleepy'], .96),
    ('zh-bright', '中文参考音频/ch0069_gachaget.ogg', 4.4, '圣园未花，终于登场啦~是这种感觉吧？', 'zh', ['happy', 'surprised'], .98),
]


def main():
    (DEST / 'References').mkdir(parents=True, exist_ok=True)
    styles, evidence = [], []
    for name, relative, end, transcript, language, emotions, speed in SPECS:
        source = SOURCE / relative
        audio, rate = sf.read(str(source))
        if audio.ndim > 1:
            audio = audio.mean(axis=1)
        if end is not None:
            audio = audio[:round(end * rate)]
        assert 3 <= len(audio) / rate <= 10
        target = DEST / 'References' / (name + '.wav')
        sf.write(str(target), audio, rate, subtype='PCM_16')
        styles.append(dict(Id=name, Language=language, Emotions=emotions, ReferenceAudio='VoiceReferences/' + name + '.wav', PromptText=transcript, SpeedFactor=speed))
        evidence.append(dict(source=relative, sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(), output=target.relative_to(PROJECT).as_posix(), bytes=target.stat().st_size, seconds=len(audio)/rate, promptText=transcript, sha256=hashlib.sha256(target.read_bytes()).hexdigest(), alignment='provided_transcript_and_silence_boundary'))
    styles.append(dict(Id='ja-soft-original', Language='ja', Emotions=['concern', 'sleepy'], ReferenceAudio=None, PromptText=None, SpeedFactor=.94))
    shy = next(s for s in styles if s['Id'] == 'ja-shy')
    for intent, speed, reference, prompt in [('annoyed', 1.02, None, None), ('sad', .92, None, None), ('hesitant', .93, shy['ReferenceAudio'], shy['PromptText']), ('flat', 1.0, None, None)]:
        styles.append(dict(Id='ja-'+intent, Language='ja', Emotions=[intent], ReferenceAudio=reference, PromptText=prompt, SpeedFactor=speed))
    (DEST / 'voice_delivery.json').write_text(json.dumps(dict(Version=2, DefaultJapanese='ja-bright', DefaultChinese='zh-soft', Styles=styles), ensure_ascii=False, indent=2), encoding='utf-8')
    reports = PROJECT / 'Docs/Validation/ExpressionDiary'
    reports.mkdir(parents=True, exist_ok=True)
    (reports / 'reference_assets.json').write_text(json.dumps(dict(originalSourcesUnchanged=True, references=evidence, addedBytes=sum(x['bytes'] for x in evidence)), ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(references=len(evidence), addedBytes=sum(x['bytes'] for x in evidence))))


if __name__ == '__main__':
    main()
