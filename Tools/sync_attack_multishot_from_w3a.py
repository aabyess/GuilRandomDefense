"""로스터 평타 다중 대상(UnitData.attackExtraTargets·attackExtraTargetRadius)을 원작 Aroc 능력에서 맞춘다 — 2026-09-30 구현담당1(구조 칸 백로그 9번).

대응 유닛의 uabi 중 기반 Aroc(바라지 — 제작자 이름 「멀티샷」)이고 대상(atar)이 none이 아닌 능력:
  attackExtraTargets      = Efk3 + 1
  attackExtraTargetRadius = aare(공격자 중심 — 값이 그 유닛 사거리 ua1r과 거의 같다: 1100/1025 · 700/700 · 826/825 · 650/600)
⚠️ 「Efk3 + 1」의 근거는 맵 안 두 곳(엔진 동작 자체는 미확인):
  · 제작자 툴팁 「N명(의 적/유닛) 동시 공격」이 툴팁에 수가 적힌 여섯 능력 모두 N = Efk3 + 2(주 대상 포함)
  · 같은 유닛의 utc1(무기 최대 대상 수)이 적힌 넷 모두 Efk3 + 1
atar = none인 넷(A0U7·A0UA·A0DB·A03U)은 능력 이름·툴팁이 멀티샷이 아닌 다른 스킬의 설명 껍데기다 — 뺀다.
한 로스터에 대응 유닛이 여럿이면 그중 큰 값(합친 로스터는 합친 채, 출력에 표시).
안 옮기는 것: 로빈 A03T 툴팁의 「15%로 모든 멀티샷 대상에게 기본 공격력 2.5배」(트리거 쪽 — 따로).
사용: python3 Tools/sync_attack_multishot_from_w3a.py [--dry]
"""
import collections
import csv
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import w3a  # noqa: E402
import w3u  # noqa: E402

FIELDS = ['attackExtraTargets', 'attackExtraTargetRadius']


def main(dry):
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    AROC = {}
    for a in w3a.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3a')):
        if a['base'] != 'Aroc':
            continue
        f = {}
        for m in a['mods']:
            if m.get('level', 0) in (0, 1):
                f.setdefault(m['field'], m['value'])
        if f.get('atar') == 'none' or not f.get('aare'):
            continue
        AROC[a['id']] = (int(f.get('Efk3') or 0) + 1, float(f['aare']))
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8-sig')):
        if r['유닛ID'] in U and r['유닛ID'] not in rmap[r['로스터']]:
            rmap[r['로스터']].append(r['유닛ID'])
    changed = same = 0
    for ro, uids in sorted(rmap.items()):
        p = os.path.join(ROOT, 'Assets/Data/Units/Roster', ro + '.asset')
        if not os.path.exists(p):
            continue
        text = open(p, encoding='utf-8').read()
        found = sorted({AROC[ab] + (ab, uid) for uid in uids for ab in str(U[uid]['mods'].get('uabi', '')).split(',') if ab in AROC}, reverse=True)
        target = [found[0][0], found[0][1]] if found else [0, 0.0]

        def get(k):
            m = re.search(r'^  %s: (\S+)' % k, text, re.M)
            return float(m.group(1)) if m else 0.0

        if [get(k) for k in FIELDS] == [float(x) for x in target]:
            same += 1
            continue
        changed += 1
        f0 = found[0]
        print('  %-24s 추가 대상 %d · 반경 %g  ← %s(%s, 사거리 %s%s)' % (ro, target[0], target[1], f0[2], f0[3], U[f0[3]]['mods'].get('ua1r'),
                                                               ' · 대응 %d유닛 중' % len(uids) if len(uids) > 1 else ''))
        if dry:
            continue
        for k, v in zip(FIELDS, target):
            sv = str(int(v)) if k == 'attackExtraTargets' else repr(float(v))
            if re.search(r'^  %s: ' % k, text, re.M):
                text = re.sub(r'^(  %s:) .*$' % k, lambda m: m.group(1) + ' ' + sv, text, count=1, flags=re.M)
            else:
                text = text.rstrip('\n') + '\n  %s: %s\n' % (k, sv)
        open(p, 'w', encoding='utf-8').write(text)
    print('같음 %d · 바꿈 %d' % (same, changed))


if __name__ == '__main__':
    main('--dry' in sys.argv)
