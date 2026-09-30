"""로스터 영웅 스탯(기초값·레벨당 성장·주스탯)을 원작 w3u 대응 유닛 값으로 맞춘다 — 2026-09-30 구현담당1(PM 지시).

배경: c80c5318(01번 실제 점화)은 이름 매핑이 불가능하던 때라 원작 분포(STR7:AGI8:INT10)를 DPS 순위로 나눠
초월함 25기에 성장치를 배정했다. 지금은 Docs/reference/MASTER_UID_ROSTER_MAP.csv가 로스터 ↔ 원작 유닛을 잇는다
→ 대조하니 초월 21기 중 11기가 원작과 어긋났다(김만경 H095: 원작 STR 0.85인데 우리 INT 0.85 등).

규칙:
  - 원작 값: w3u ustr/uagi/uint(기초) · ustp/uagp/uinp(레벨당) · upra(주스탯). 필드가 비면 기반 유닛 스톡 기본값
    (hrif=비영웅이라 0, Hvwd의 upra=AGI).
  - 로스터에 대응 원작 유닛이 여럿이고 그중 스탯을 가진 게 둘 이상이면: 지금 값이 그중 하나와 같으면 그대로 둔다
    (강주혁 H08V·구주호 H0BE처럼 이미 골라 둔 것), 아무것과도 안 맞으면 바꾸지 않고 「모호」로 적는다.
  - 원작 대응 유닛에 스탯이 없는데 우리가 가진 경우는 건드리지 않고 목록만 낸다(스탯 비례 스킬이 읽을 수 있음).
사용: python3 Tools/sync_hero_stats_from_w3u.py [--dry]
"""
import collections
import csv
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import w3u  # noqa: E402

STOCK_UPRA = {'Hvwd': 'AGI'}              # 스톡 영웅 기반의 주스탯 기본값(쓰이는 것만)
PRIMARY = {'STR': 1, 'AGI': 2, 'INT': 3}
FIELDS = ['baseStrength', 'baseAgility', 'baseIntelligence', 'strengthPerLevel', 'agilityPerLevel', 'intelligencePerLevel',
          'primaryStat']


def orig_stats(u):
    mo = u['mods']
    if not any(k in mo for k in ('ustr', 'uagi', 'uint', 'ustp', 'uagp', 'uinp', 'upra')):
        return None
    f = lambda k: round(float(mo.get(k, 0) or 0), 4)
    upra = mo.get('upra') or STOCK_UPRA.get(u['base'])
    return [f('ustr'), f('uagi'), f('uint'), f('ustp'), f('uagp'), f('uinp'), PRIMARY.get(upra, 0)]


def main(dry):
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        rmap[r['로스터']].append(r['유닛ID'])
    changed, ambiguous, ours_only, same = [], [], [], 0
    for ro, uids in sorted(rmap.items()):
        p = os.path.join(ROOT, 'Assets/Data/Units/Roster', ro + '.asset')
        if not os.path.exists(p):
            continue
        text = open(p, encoding='utf-8').read()

        def get(k):
            m = re.search(r'^  %s: (\S+)' % k, text, re.M)
            return round(float(m.group(1)), 4) if m else 0.0
        ours = [get(k) for k in FIELDS]
        cands = [(uid, s) for uid in uids if uid in U for s in [orig_stats(U[uid])] if s]
        if not cands:
            if any(ours[3:6]):
                ours_only.append((ro, ours))
            continue
        if any(abs(a - b) < 1e-3 for s in [c[1] for c in cands] for a, b in [(0, 0)]) and any(
                all(abs(a - b) < 1e-3 for a, b in zip(ours, s)) for _, s in cands):
            same += 1
            continue
        if len({tuple(s) for _, s in cands}) > 1:
            ambiguous.append((ro, ours, cands))
            continue
        uid, target = cands[0]
        changed.append((ro, uid, ours, target))
        if dry:
            continue
        for k, v in zip(FIELDS, target):
            s = str(int(v)) if k == 'primaryStat' or float(v).is_integer() else repr(v)
            if re.search(r'^  %s: ' % k, text, re.M):
                text = re.sub(r'^(  %s:) .*$' % k, lambda m: m.group(1) + ' ' + s, text, count=1, flags=re.M)
            else:
                text = text.rstrip('\n') + '\n  %s: %s\n' % (k, s)
        open(p, 'w', encoding='utf-8').write(text)
    print('일치 %d · 바꿈 %d · 모호 %d · 원작에 스탯 없는데 우리만 가짐 %d' % (same, len(changed), len(ambiguous), len(ours_only)))
    for ro, uid, a, b in changed:
        print('  바꿈 %-18s %s  %s → %s' % (ro, uid, a, b))
    for ro, a, c in ambiguous:
        print('  모호 %-18s 우리 %s  원작 %s' % (ro, a, c))
    for ro, a in ours_only:
        print('  우리만 %-18s %s' % (ro, a))


if __name__ == '__main__':
    main('--dry' in sys.argv)
