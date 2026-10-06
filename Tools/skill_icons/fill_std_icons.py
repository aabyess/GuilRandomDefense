"""맵 밖(워크3 표준 BTN) 아이콘을 웹에서 구한 모음으로 채운다(2026-10-06, blender 세션 — 사장님 「웹에서 찾아서 써도 된다」).
   출처: https://github.com/Wc3ReforegIcons/Wc3ReforegIcons.github.io — btn<이름>.jpg 1,503장(256×256, 워크3 리포지드 HD 아이콘). 파일 이름이 원작 BTN 이름과 1:1이라 이름 일치로만 붙인다.
   ⚠️ 클래식 맵 아이콘이 아니라 같은 소재의 리포지드 HD판이다(그림 결은 다름). 블리자드 소유 이미지 — 원작 맵 아이콘과 같은 지위(비공개 베타용, 공개 전 교체).
   다운로드물은 ~/GRD_skill_icons_dl/reforge/ (새 폴더에 git clone --depth 1; 거기서 코드를 돌리지 않는다. 이 스크립트는 그림만 읽는다).
   /usr/bin/python3 Tools/skill_icons/fill_std_icons.py  → ~/GRD_skill_icons/std_<이름>.png + ability_icons.csv·item_icons.csv의 맵밖 행 png·출처 칸
   (extract_icons.py를 다시 돌리면 이 칸이 지워지니, 돌린 뒤 이 스크립트를 한 번 더 돌릴 것)
"""
import csv, os, re, sys
from PIL import Image

SRC = os.path.expanduser('~/GRD_skill_icons_dl/reforge')
OUT = os.path.expanduser('~/GRD_skill_icons')
have = {f[:-4].lower(): f for f in os.listdir(SRC) if f.lower().endswith('.jpg')}


def base(p):
    b = p.replace('/', '\\').split('\\')[-1].lower()
    return re.sub(r'\.(blp|tga|dds)$', '', b)


def lookup(p):
    b = base(p)
    if b in have: return have[b], '웹(리포지드 HD, 이름 일치)'
    if b.startswith('pasbtn') and 'btn' + b[6:] in have: return have['btn' + b[6:]], '웹(PASBTN→BTN 같은 그림, 패시브 틀만 다름)'
    return None, ''


def run(path, pathcol, pngcol):
    rows = list(csv.reader(open(path, encoding='utf-8-sig')))
    head = rows[0]
    if '출처' not in head: head.append('출처')
    ci = head.index('출처'); pi = head.index(pngcol); ki = head.index(pathcol); di = head.index('맵안/밖')
    got = miss = 0
    for r in rows[1:]:
        while len(r) < len(head): r.append('')
        if r[di] == '맵안': r[ci] = '원작 맵 안'
        if r[di] != '맵밖': continue
        f, how = lookup(r[ki])
        if f:
            name = 'std_%s.png' % os.path.splitext(f)[0]
            dest = os.path.join(OUT, name)
            if not os.path.exists(dest): Image.open(os.path.join(SRC, f)).convert('RGBA').save(dest)
            r[pi] = name; r[ci] = how; got += 1
        else:
            r[ci] = '못 구함'; miss += 1
    csv.writer(open(path, 'w', encoding='utf-8-sig', newline='')).writerows(rows)
    return got, miss


a = run('Tools/skill_icons/ability_icons.csv', '아이콘경로', 'png파일(~/GRD_skill_icons/)')
i = run('Tools/skill_icons/item_icons.csv', 'iico경로', 'png파일')
print('능력 행: 채움 %d · 못 구함 %d | 아이템 행: 채움 %d · 못 구함 %d' % (a + i))
uniq = len([f for f in os.listdir(OUT) if f.startswith('std_')])
print('std_ PNG 고유 장수', uniq)
