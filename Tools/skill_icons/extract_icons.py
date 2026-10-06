"""원작(ORD11.089.w3x) 능력·아이템 아이콘을 뽑는다(2026-10-06, blender 세션 · 읽기 전용 — Assets에 안 쓴다).
   w3a 능력마다 aart(기본 아이콘)·arar(연구)·auar(학습 해제)·aret, w3t 아이템마다 iico를 읽고,
   경로가 맵(MPQ) 안에 있으면 BLP → PNG로 ~/GRD_skill_icons/ 에 꺼낸다. 맵에 없으면(워크3 표준 BTN*.blp — War3.mpq 필요) 「맵 밖」으로만 표시.
   🔴 /usr/bin/python3 로 돌릴 것(mpyq·Pillow가 거기 있다).
   /usr/bin/python3 Tools/skill_icons/extract_icons.py            → ~/GRD_skill_icons/ + Tools/skill_icons/ability_icons.csv · item_icons.csv
"""
import sys, os, csv, re, collections
sys.path.insert(0, 'Tools/w3x')
import w3a, w3u, wts
from mpqread import Archive
from PIL import Image

OUT = os.path.expanduser('~/GRD_skill_icons')
os.makedirs(OUT, exist_ok=True)
src = open('Tools/w3x/원본/ORD11.089.w3x', 'rb').read()
open('/tmp/ord_icons.mpq', 'wb').write(src[512:])
ar = Archive('/tmp/ord_icons.mpq')
S = wts.parse('Tools/w3x/원본/war3map_new.wts')
A = w3a.parse('Tools/w3x/원본/war3map_new.w3a')
T = w3u.parse('Tools/w3x/원본/풀린것/war3map.w3t')

_cache = {}
def fetch(path):
    """경로 → (분류, 맵 안 실제 이름, 바이트). 분류: 맵안 / 맵밖(표준 War3) / 못찾음"""
    if path in _cache: return _cache[path]
    base = path.replace('/', '\\').split('\\')[-1]
    cands = [path, base]
    for p in list(cands):
        if not p.lower().endswith(('.blp', '.dds', '.tga')): cands.append(p + '.blp')
    cands.append('war3mapImported\\' + base)
    r = None
    for c in cands:
        try:
            d = ar.read(c)
            if d: r = ('맵안', c, d); break
        except Exception: pass
    if r is None:
        std = path.lower().startswith(('replaceabletextures', 'ui\\', 'abilities\\', 'units\\'))
        r = ('맵밖' if std else '못찾음', '', None)
    _cache[path] = r
    return r

def safe(s):
    return re.sub(r'[\\/:*?"<>|\s]+', '', re.sub(r'\|[cC][0-9a-fA-F]{8}|\|r', '', s))[:24] or 'noname'

def to_png(data, dest):
    from io import BytesIO
    im = Image.open(BytesIO(data)); im.load()
    im.convert('RGBA').save(dest)

done = {}   # path -> png 파일명(한 그림은 한 번만 저장: 이름은 처음 쓴 능력 기준)
rows = []
for a in A:
    aid = a['id'] if '\x00' not in a['id'] else a['base'] + '*'   # 새 ID가 비어 있는(NUL) 항목 = 원본 능력을 고친 것
    f = {}
    for m in a['mods']:
        f.setdefault(m['field'], m['value'])
    name = wts.resolve(str(f.get('anam', '')), S)
    for fld, kind in (('aart', '기본'), ('arar', '연구'), ('auar', '학습해제'), ('aret', '연구툴팁아이콘')):
        p = f.get(fld)
        if not p or not isinstance(p, str) or fld == 'aret': continue
        cat, real, data = fetch(p)
        png = ''
        if data is not None:
            if p not in done:
                fn = '%s_%s.png' % (aid.replace('*', 'x'), safe(name))
                if fld != 'aart': fn = fn.replace('.png', '_%s.png' % kind)
                try: to_png(data, os.path.join(OUT, fn)); done[p] = fn
                except Exception as e: done[p] = ''; cat = '디코드실패:' + str(e)[:30]
            png = done.get(p, '')
        rows.append([aid, name.replace('\n', ' '), kind, p, cat, real, png])

with open('Tools/skill_icons/ability_icons.csv', 'w', encoding='utf-8-sig', newline='') as fh:
    w = csv.writer(fh); w.writerow(['능력코드', '이름', '아이콘종류', '아이콘경로', '맵안/밖', '맵안실제이름', 'png파일(~/GRD_skill_icons/)']); w.writerows(rows)

irows = []
for t in T:
    f = t['mods']; p = f.get('iico', '')
    cat, real, data = fetch(p) if p else ('없음', '', None)
    png = ''
    if data is not None:
        if p not in done:
            fn = 'item_%s_%s.png' % (t['id'], safe(f.get('unam', '')))
            to_png(data, os.path.join(OUT, fn)); done[p] = fn
        png = done[p]
    irows.append([t['id'], f.get('unam', ''), p, cat, real, png])
with open('Tools/skill_icons/item_icons.csv', 'w', encoding='utf-8-sig', newline='') as fh:
    w = csv.writer(fh); w.writerow(['아이템코드', '이름', 'iico경로', '맵안/밖', '맵안실제이름', 'png파일']); w.writerows(irows)

c = collections.Counter(r[4] for r in rows)
print('능력 아이콘 행', len(rows), dict(c), '| 고유 그림', len(done))
print('아이템', len(irows), dict(collections.Counter(r[3] for r in irows)))
