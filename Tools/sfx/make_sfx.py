"""효과음 고르기·다듬기(2026-10-06, blender 세션 · 사장님 「자잘자잘한 효과음」 · Assets엔 안 씀, 연결은 PM).
   재료: ① 원작 맵 mp3 ~/GRD_original_sounds/ ② Kenney CC0 팩 ~/GRD_sfx/_src/<팩>/(Impact Sounds · UI Audio · Interface Sounds · RPG Audio · Casino Audio · Digital Audio — 전부 CC0, 팩의 License.txt가 거기 있다)
   각 소리: 첫 소리 3ms 앞에서 자름(시작 무음 제거) → 최대 길이로 자르거나 3% 아래로 떨어지면 거기서 끝 → 끝 페이드 → 모노 44.1k → 피크 −1 dBFS → 16bit wav.
   산출: ~/GRD_sfx/<자리>/<자리>_NN.wav · README.md · _audition_all.wav(자리 순서대로 이어 붙인 듣기용) · 들어보기_목록.md
   /usr/bin/python3 Tools/sfx/make_sfx.py      (numpy 필요 · ffmpeg는 PATH)
"""
import os, glob, subprocess, wave, json
import numpy as np

HOME = os.path.expanduser('~')
ORIG = HOME + '/GRD_original_sounds/'
SRC = HOME + '/GRD_sfx/_src/'
OUT = HOME + '/GRD_sfx/'
SR = 44100
PACK = {'impact': 'impact-sounds', 'ui': 'ui-audio', 'iface': 'interface-sounds', 'rpg': 'rpg-audio', 'casino': 'casino-audio', 'digital': 'digital-audio'}

# 자리: [(출처, 파일 이름(확장자 없이), 최대 길이 초, 메모)] — 출처 'o'=원작 맵, 나머지=Kenney 팩
SLOTS = {
    'hit_melee': [('o', 'hit_012', .30, '원작 hit'), ('o', 'hit_011', .30, '원작 hit'), ('o', 'hit_014', .32, '원작 hit'), ('o', 'hit-gan4', .30, '원작 hit-gan')],
    'hit_ranged': [('o', 'arrow002', .28, '원작 화살'), ('o', 'arrow001', .28, '원작 화살'), ('o', 'Sound_GunFire2', .26, '원작 총'), ('impact', 'impactWood_light_001', .22, 'Kenney 나무 박힘')],
    'hit_magic': [('o', 'thunder_22', .40, '원작 번개'), ('o', 'ice_sound3', .38, '원작 얼음'), ('o', 'Kid EffectB', .40, '원작 이펙트'), ('o', 'dark_effect2', .40, '원작 암흑')],
    'enemy_death': [('impact', 'impactSoft_medium_000', .20, 'Kenney 부드러운 퍽'), ('impact', 'impactSoft_medium_001', .20, 'Kenney 부드러운 퍽'), ('impact', 'impactGeneric_light_001', .18, 'Kenney 가벼운 타격'), ('o', 'stone', .22, '원작 돌')],
    'boss_death': [('o', 'Perona_kamikazeboom2', 1.4, '원작 폭발'), ('o', 'Buster_Exp', 1.3, '원작 폭발'), ('o', 'bigmam_2', 1.4, '원작 빅맘')],
    'ui_click': [('iface', 'click_001', .2, 'Kenney'), ('ui', 'click1', .2, 'Kenney UI'), ('ui', 'click3', .2, 'Kenney UI'), ('iface', 'select_001', .25, 'Kenney')],
    'ui_error': [('iface', 'error_001', .5, 'Kenney'), ('iface', 'error_003', .5, 'Kenney'), ('iface', 'error_005', .5, 'Kenney')],
    'combine': [('iface', 'confirmation_001', .8, 'Kenney'), ('iface', 'confirmation_003', .8, 'Kenney'), ('impact', 'impactMetal_light_001', .6, 'Kenney 금속 맞물림')],
    'gacha': [('casino', 'cards-pack-open-1', 1.0, 'Kenney 카드 팩 뜯기'), ('casino', 'cards-pack-open-2', 1.0, 'Kenney 카드 팩 뜯기'), ('casino', 'card-fan-1', .9, 'Kenney 카드 펼치기')],
    'gold': [('o', 'coinsound', .8, '원작 코인(이미 게임에 있음 — 비교용)'), ('rpg', 'handleCoins', .8, 'Kenney 동전'), ('rpg', 'handleCoins2', .8, 'Kenney 동전'), ('casino', 'chips-handle-2', .6, 'Kenney 칩')],
    'round_start': [('impact', 'impactBell_heavy_000', 1.2, 'Kenney 큰 종'), ('impact', 'impactBell_heavy_002', 1.2, 'Kenney 큰 종'), ('o', 'charge', 1.2, '원작 charge')],
    'boss_appear': [('o', 'th1', 1.6, '원작 천둥'), ('o', 'kaido_bong2', 1.3, '원작 카이도'), ('impact', 'impactMetal_heavy_000', 1.3, 'Kenney 무거운 금속'), ('o', 'bigmom_howling4', 1.6, '원작 빅맘 포효')],
    'wood': [('rpg', 'chop', .5, 'Kenney 도끼질'), ('impact', 'impactWood_medium_001', .4, 'Kenney 나무'), ('impact', 'impactWood_light_002', .35, 'Kenney 나무')],
}


def find(src, name):
    if src == 'o':
        c = glob.glob(ORIG + name + '.*')
    else:
        c = glob.glob(SRC + PACK[src] + '/**/' + name + '.*', recursive=True)
        c = [x for x in c if x.lower().endswith(('.ogg', '.wav', '.mp3'))]
    assert c, f'없음: {src} {name}'
    return c[0]


def load(path):
    raw = subprocess.run(['ffmpeg', '-v', 'quiet', '-i', path, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def shape(a, maxlen):
    pk = np.abs(a).max()
    on = int(np.argmax(np.abs(a) > pk * .02))
    a = a[max(0, on - int(.003 * SR)):]
    a = a[:int(maxlen * SR)]
    env = np.abs(a); lastloud = np.where(env > env.max() * .03)[0]
    mn = int(min(maxlen, .06 if maxlen <= .5 else .5) * SR)          # 3% 아래로 떨어져도 너무 짧아지지 않게 최소 길이(소스가 그보다 짧으면 소스 길이)
    if len(lastloud): a = a[:max(lastloud[-1] + int(.01 * SR), mn)]
    n = len(a)
    fi = min(int(.002 * SR), n); a[:fi] *= np.linspace(0, 1, fi)
    fo = min(max(int(.03 * SR), int(n * .15)), n); a[n - fo:] *= np.linspace(1, 0, fo) ** 1.5
    a = a / (np.abs(a).max() + 1e-9) * (10 ** (-1 / 20))
    return a


def write_wav(path, a):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(a, -1, 1) * 32767).astype('<i2').tobytes())


def main():
    rows = []; aud = []; order = []
    for slot, items in SLOTS.items():
        for i, (src, name, mx, note) in enumerate(items, 1):
            p = find(src, name)
            a = shape(load(p), mx)
            f = f'{OUT}{slot}/{slot}_{i:02d}.wav'
            write_wav(f, a)
            lic = '원작 맵(게임 내부 자산)' if src == 'o' else 'CC0 · Kenney ' + PACK[src]
            rows.append((slot, i, os.path.relpath(p, HOME), lic, len(a) / SR, 20 * np.log10(np.abs(a).max()), note))
            aud.append(a); aud.append(np.zeros(int(.5 * SR), np.float32)); order.append(f'{slot}_{i:02d}')
    write_wav(OUT + '_audition_all.wav', np.concatenate(aud))
    with open(OUT + 'README.md', 'w', encoding='utf-8') as fh:
        fh.write('# GRD 효과음 (2026-10-06, blender · Tools/sfx/make_sfx.py)\n\n44.1kHz 모노 16bit wav · 시작 무음 제거 · 피크 −1 dBFS(자리끼리 크기를 맞춘 게 아니라 전부 같은 피크다 → 게임에서 자리별 볼륨은 PM이: 권장 enemy_death −8dB · ui_click −6dB · hit_* −4dB · boss_* 0dB).\n')
        fh.write('출처: ① 원작 맵 mp3(~/GRD_original_sounds, 게임 안 자산이라 저장소 밖 배포물엔 넣을 때 확인 — Docs/REPLACE_BEFORE_PUBLIC.md 참고) ② Kenney 팩(CC0 — 크레딧 불필요, 팩 License.txt는 ~/GRD_sfx/_src/<팩>/License.txt).\n\n')
        fh.write('| 자리 | 번호 | 출처 파일 | 라이선스 | 길이(초) | 피크(dBFS) | 메모 |\n|---|---|---|---|---|---|---|\n')
        for r in rows:
            fh.write('| %s | %02d | %s | %s | %.2f | %.1f | %s |\n' % r)
    with open(OUT + '들어보기_목록.md', 'w', encoding='utf-8') as fh:
        fh.write('# 효과음 들어보기 (한 장)\n`_audition_all.wav` 하나를 처음부터 들으면 아래 순서대로 나온다(소리 사이 0.5초 무음). 각 칸에 ○△×.\n\n| # | 파일 | 길이 | 출처 | 느낌 |\n|---|---|---|---|---|\n')
        for k, (r, o) in enumerate(zip(rows, order), 1):
            fh.write('| %d | %s | %.2f | %s | ☐ |\n' % (k, o, r[4], r[6]))
    print('만든 소리', len(rows), '· 자리', len(SLOTS), '· 듣기용 길이 %.1f초' % (sum(len(x) for x in aud) / SR))


if __name__ == '__main__':
    main()
