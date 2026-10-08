#!/usr/bin/python3
"""연출 대본 자동 검사 드라이버(10-09): 대본마다 반입 → 게임 판에서 재생 → 6점 캡처 → 픽셀 판정 → 통과/실패 표.
사용: /usr/bin/python3 Tools/cinematics/auto_check.py [id ...]   (id 없으면 ~/GRD_scenes/scripts 전부)
브리지(ClaudeBridge/inbox·outbox, 접두 g2_)로 에디터를 부른다 — 에디터 순번을 가진 세션에서만 돌릴 것.
판정(한 대본 6프레임, 같은 판의 재생 전 기준 사진 대비 HUD를 뺀 가운데 영역):
  보임 ≥ 1% 픽셀 변화 · 화면 덮음 ≤ 55%(변화 픽셀 비율 최대) · 검은 판 ≤ 6%(기준에선 밝았는데 어두워진 픽셀).
결과: Docs/research/CINEMATIC_CHECK.tsv(덮어씀) · 통과 id 목록 Docs/research/CINEMATIC_PASSED.txt
"""
import os, sys, time, glob, json
from PIL import Image

ROOT = os.getcwd()
BRIDGE = os.path.join(ROOT, 'ClaudeBridge')
SCRIPTS = os.path.expanduser('~/GRD_scenes/scripts')
CROP = (260, 60, 1660, 780)
COVER_MAX, VISIBLE_MIN, BLACK_MAX = 0.55, 0.01, 0.06

def bridge(name, line, wait_for='✅', timeout=240):
    out = os.path.join(BRIDGE, 'outbox', name + '.txt')
    if os.path.exists(out): os.remove(out)
    with open(os.path.join(BRIDGE, 'inbox', name + '.txt'), 'w', encoding='utf-8') as f: f.write(line + '\n')
    t0 = time.time()
    while time.time() - t0 < timeout:
        time.sleep(3)
        if os.path.exists(out):
            txt = open(out, encoding='utf-8').read()
            if wait_for is None or wait_for in txt or '❌' in txt: return txt
    return ''

def diff_stats(base, img):
    b = Image.open(base).convert('RGB').crop(CROP).resize((350, 180)); i = Image.open(img).convert('RGB').crop(CROP).resize((350, 180))
    bp, ip = b.load(), i.load(); n = 350 * 180; changed = dark = 0
    for y in range(180):
        for x in range(350):
            r0, g0, b0 = bp[x, y]; r1, g1, b1 = ip[x, y]
            if max(abs(r0 - r1), abs(g0 - g1), abs(b0 - b1)) > 40: changed += 1
            if (r0 + g0 + b0) / 3 > 60 and (r1 + g1 + b1) / 3 < 28: dark += 1
    return changed / n, dark / n

def check(sid):
    open(os.path.join(BRIDGE, 'g2_scene.txt'), 'w').write(sid)
    imp = bridge('g2_ac_imp', 'call CinematicImporter.ImportOne')
    if '❌' in imp or 'ImportOne →' not in imp: return sid, 'FAIL', f'반입 실패: {imp[:120]}', 0, 0, 0
    snaps = ' '.join(f'wait:0.5 snap:g2_ac_{i}.png' for i in range(1, 7))
    cmd = f'gameshot g2_ac.png 1 1920x1080 click?:보통 wait:1.5 call:CinematicProbe.Setup wait:1.2 snap:g2_ac_base.png call:CinematicProbe.Fire {snaps}'
    res = bridge('g2_ac_shot', cmd, wait_for='gameshot 결과')
    if '❌' in res and 'gameshot 결과: ✅' not in res: return sid, 'FAIL', '촬영 실패: ' + res[-160:], 0, 0, 0
    shots = os.path.join(BRIDGE, 'shots')
    base = os.path.join(shots, 'g2_ac_base.png'); cover = vis = dark = 0.0
    for i in range(1, 7):
        c, d = diff_stats(base, os.path.join(shots, f'g2_ac_{i}.png'))
        cover = max(cover, c); dark = max(dark, d); vis = max(vis, c)
        os.replace(os.path.join(shots, f'g2_ac_{i}.png'), os.path.join(shots, f'ac_{sid}_{i}.png'))
    reasons = []
    if vis < VISIBLE_MIN: reasons.append(f'안 보임({vis:.3f})')
    if cover > COVER_MAX: reasons.append(f'화면 덮음({cover:.2f})')
    if dark > BLACK_MAX: reasons.append(f'검은 판({dark:.3f})')
    return sid, ('FAIL' if reasons else 'PASS'), ', '.join(reasons), vis, cover, dark

def main():
    ids = sys.argv[1:] or sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(SCRIPTS + '/*.json'))
    rows = ['id\t판정\t사유\t변화최대\t덮음\t검은판']; passed = []
    for sid in ids:
        r = check(sid); print(r, flush=True)
        rows.append('\t'.join(str(x) if not isinstance(x, float) else f'{x:.3f}' for x in r))
        if r[1] == 'PASS': passed.append(sid)
    open(os.path.join(ROOT, 'Docs/research/CINEMATIC_CHECK.tsv'), 'w', encoding='utf-8').write('\n'.join(rows) + '\n')
    prev = set(open(os.path.join(ROOT, 'Docs/research/CINEMATIC_PASSED.txt'), encoding='utf-8').read().split()) if os.path.exists(os.path.join(ROOT, 'Docs/research/CINEMATIC_PASSED.txt')) else set()
    open(os.path.join(ROOT, 'Docs/research/CINEMATIC_PASSED.txt'), 'w', encoding='utf-8').write('\n'.join(sorted(prev | set(passed))) + '\n')

if __name__ == '__main__': main()
