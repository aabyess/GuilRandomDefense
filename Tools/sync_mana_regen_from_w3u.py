"""로스터 마나 재생(원작 umpr)·최대 마나(umpm)·게이지 환산을 원작 w3u 대응 유닛 값으로 맞춘다 — 2026-09-30 구현담당1(PM 지시).

배경: 원작 게이지 스킬은 「평타 때 마나 검사」(HashAttack 트리거에서 `if 마나==N … else 마나+x`)인데 마나는 평타 +x 말고도
초당 재생(umpr, 영웅은 + INT×0.08 — war3mapMisc IntRegenBonus)으로 찬다. 우리 게이지(OnHitCount Mana)는 타격 수만 셌다.
UnitData에 쓰는 값(없으면 0 = 지금 동작):
  manaRegenPerSecond — 원작 umpr(마나/초). 비어 있으면 기반 유닛 스톡 기본값 — hrif(라이플맨, 마나 없는 유닛)는 0으로 둔다(가정).
  manaMax            — 원작 umpm(최대 마나). 게이지가 이 값(× 환산)에서 멈춘다.
  manaGaugePerMana   — 게이지 한 칸이 마나 몇에 해당하나의 역수(게이지 칸/마나). 평타 트리거가 마나 +x를 매 타 더하면
                       우리 게이지는 매 타 +1이라 1/x. 평타 +x가 없으면(우타처럼 확률 사건에서만 +2) 그 로스터 마나 게이지 에셋의
                       문턱 ÷ umpm(예: 우타 384칸 ≈ 마나 100 → 3.84).
  lifeGaugeRegenPerSecond·lifeGaugeMax — 원작 두 번째 유닛의 마나를 Life 카운터로 세어 둔 에셋(skillName에 「MANA」가 든
                       Life 게이지 — 영원_최상호 미호크 175처럼 한 로스터에 마나 유닛이 둘일 때)용. 게이지 칸 단위.
                       ⚠️ 진짜 체력 게이지의 재생(uhpr)은 여기서 안 채운다(hrif 스톡 기본 체력 재생 미확인 — PM 확인 대기).
로스터에 대응 원작 유닛이 여럿이면 umpm×환산이 그 로스터 Mana 게이지 에셋 문턱과 맞는 유닛을 쓴다.
사용: python3 Tools/sync_mana_regen_from_w3u.py [--dry]
"""
import collections
import csv
import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
sys.path.insert(0, os.path.join(ROOT, 'Tools'))
import w3u  # noqa: E402
import skill_asset_tool as sat  # noqa: E402

FIELDS = ['manaRegenPerSecond', 'manaMax', 'manaGaugePerMana', 'lifeGaugeRegenPerSecond', 'lifeGaugeMax']


def main(dry):
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    J = open(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.j'), encoding='utf-8', errors='replace').read()
    HASH = collections.defaultdict(list)
    for m in re.finditer(r"SaveTriggerHandle\(\w+,'(\w{4})',\d+,gg_trg_(\w+)\)", J):
        HASH[m.group(1)].append(m.group(2))
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        rmap[r['로스터']].append(r['유닛ID'])
    guid_to_asset = {re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1): p
                     for p in glob.glob(os.path.join(ROOT, 'Assets/Data/UnitSkills/*.asset'))}

    def trigger_mana(uid):
        """(평타 트리거가 매 타 더하는 마나 x 또는 None, 마나 검사 있음)"""
        for t in HASH.get(uid, []):
            p = J.find('function Trig_%s_Actions takes' % t)
            if p < 0:
                continue
            b = J[p:J.index('endfunction', p)]
            checks = bool(re.search(r'UNIT_STATE_MANA,GetAttacker\(\)\)(==|>=)', b))
            # 맨 바깥(조건 없는) +x: `else` 갈래의 +x — 검사 if의 else에서 더하는 표준 모양
            m = re.search(r'UNIT_STATE_MANA,GetAttacker\(\)\)(?:==|>=)[\d.]+ then.*?else\s*call SetUnitManaBJ\(GetAttacker\(\),\(GetUnitStateSwap\(UNIT_STATE_MANA,GetAttacker\(\)\)\+([\d.]+)\)\)', b, re.S)
            if m:
                return float(m.group(1)), checks
            m = re.match(r'function \w+ takes nothing returns nothing\s*call SetUnitManaBJ\(GetAttacker\(\),\(GetUnitStateSwap\(UNIT_STATE_MANA,GetAttacker\(\)\)\+([\d.]+)\)\)', b)
            if m:
                return float(m.group(1)), checks
            return None, checks
        return None, False

    changed, notes, same = [], [], 0
    for ro, uids in sorted(rmap.items()):
        p = os.path.join(ROOT, 'Assets/Data/Units/Roster', ro + '.asset')
        if not os.path.exists(p):
            continue
        cands = []
        for uid in uids:
            mo = U.get(uid, {}).get('mods', {})
            if not mo.get('umpm'):
                continue
            per_hit, checks = trigger_mana(uid)
            cands.append((checks, uid, float(mo.get('umpm')), float(mo.get('umpr') or 0.0), per_hit))
        if not cands:
            continue
        text = open(p, encoding='utf-8').read()
        # 로스터 게이지 에셋: (종류, 문턱, skillName)
        gauges = []
        for g in re.findall(r'guid: (\w+), type: 2', text.split('  trait:')[0]):
            ap = guid_to_asset.get(g)
            if not ap:
                continue
            a = sat.load(ap)
            if not re.search(r'^  triggerType: 3', a.head, re.M):
                continue
            sk = (re.search(r'^  skillName: (.*)$', a.head, re.M) or [None, ''])[1]
            for li in range(len(a.levels)):
                t = int(sat.get_level_field(a, li, 'hitCountThreshold') or 0) or int(sat.get_level_field(a, li, 'hitCountFloor') or 0)
                if t:
                    gauges.append((int(sat.get_level_field(a, li, 'gaugeKind') or 0), t, sk))
        mana_thr = [t for k, t, _ in gauges if k == 0]
        life_mana = [(t, sk) for k, t, sk in gauges if k == 1 and 'MANA' in sk.upper()]

        def gpm_of(c):
            return 1.0 / c[4] if c[4] else ((max(mana_thr) / c[2]) if mana_thr else 1.0)
        # Mana 게이지: umpm×환산이 문턱과 맞는 유닛 우선, 없으면 마나 검사하는 첫 유닛
        match = lambda c: mana_thr and any(abs(c[2] * gpm_of(c) - t) < 0.5 for t in mana_thr)
        pick = ([c for c in cands if c[4] and match(c)] or [c for c in cands if match(c)]
                or [c for c in cands if c[0]] or cands)   # 평타 +x를 읽은 유닛 먼저(환산을 문턱에서 역산하면 아무 유닛이나 맞는다)
        checks, uid, umpm, umpr, per_hit = pick[0]
        gpm = gpm_of(pick[0])
        if not per_hit:
            notes.append('%s(%s): 평타 +x 없음 → 게이지 환산 %.3f (마나 게이지 문턱 %s ÷ umpm %g)' % (ro, uid, gpm, mana_thr, umpm))
        if mana_thr and max(mana_thr) > umpm * gpm + 0.5:
            notes.append('⚠️ %s: 마나 게이지 문턱 %s > 상한 %g — 상한을 문턱으로 올림' % (ro, mana_thr, umpm * gpm))
            umpm = max(mana_thr) / gpm
        # Life 카운터로 센 두 번째 마나 유닛
        lrg, lmax = 0.0, 0.0
        for t, sk in life_mana:
            other = [c for c in cands if c[1] != uid and abs(c[2] - t) < 0.5]
            if other:
                lrg, lmax = other[0][3], other[0][2]
                notes.append('%s: Life 카운터 마나 %s ← %s(umpr %g · umpm %g)' % (ro, sk[:30], other[0][1], lrg, lmax))
        target = [round(umpr, 4), round(umpm, 4), round(gpm, 4), round(lrg, 4), round(lmax, 4)]

        def get(k):
            m = re.search(r'^  %s: (\S+)' % k, text, re.M)
            return round(float(m.group(1)), 4) if m else None
        if [get(k) for k in FIELDS] == target:
            same += 1
            continue
        changed.append((ro, uid, target))
        if dry:
            continue
        for k, v in zip(FIELDS, target):
            s = repr(float(v))
            if re.search(r'^  %s: ' % k, text, re.M):
                text = re.sub(r'^(  %s:) .*$' % k, lambda m: m.group(1) + ' ' + s, text, count=1, flags=re.M)
            else:
                text = text.rstrip('\n') + '\n  %s: %s\n' % (k, s)
        open(p, 'w', encoding='utf-8').write(text)
    print('같음 %d · 바꿈 %d' % (same, len(changed)))
    for ro, uid, t in changed:
        print('  %-24s %s  umpr %g · umpm %g · 게이지/마나 %g · Life카운터 재생 %g/상한 %g' % (ro, uid, *t))
    for n in notes:
        print('  참고', n)


if __name__ == '__main__':
    main('--dry' in sys.argv)
