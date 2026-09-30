"""「게이트 없음」 검사 — 우리 에셋은 매 타(확률 1.0·쿨다운·게이지·버프 게이트 없음)인데 원문 트리거에선 그 피해가
확률·게이지·버프 조건 안에서만 나는 경우를 찾는다. 2026-09-30 구현담당1(PM 지시).

배경: 게이트_*_355f5d39(「(게이트 없음)」) 에셋 몇이 원문에선 1/N·MANA==N 갈래 안이었다(타시기 16%/9%, 바키 MANA≥145).
판정: 에셋 레벨이 OnHitChance·triggerChance≥1·cooldown 0·requiredBuffId/forbiddenBuffId 빈 칸일 때, 그 레벨의 효과 값
(multiplier·bonus)이 원작 유닛 트리거 RRD·UnitDamage* 인자에 나오는 **모든** 자리가 게이트 조건(GetRandomInt·
UNIT_STATE_MANA·UNIT_STATE_LIFE·UnitHasBuff·쿨다운 버프) 아래면 표시. 경로·원작 유닛 규칙은 audit_bundle_exclusive와 같다.
⚠️ 기계 판정 — 원문 확인 후 고칠 것.
사용: python3 Tools/audit_ungated.py [출력.csv]
"""
import collections
import csv
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audit_bundle_exclusive as X  # noqa: E402
import audit_origin_provenance as P  # noqa: E402
import skill_asset_tool as sat  # noqa: E402

GATE = re.compile(r'GetRandomInt|GetRandomReal|UNIT_STATE_MANA|UNIT_STATE_LIFE|UnitHasBuff')


def gate_of(path):
    """경로(갈래 목록) 위 조건 중 게이트인 것들. 버프 「있음」 조건의 else 갈래(=버프 없는 평소 상태)는 게이트로 안 친다."""
    out = []
    for k, arm in path:
        c = X.COND.get(k, '')
        if not GATE.search(c):
            continue
        if arm > 0 and not re.search(r'GetRandom|UNIT_STATE_MANA|UNIT_STATE_LIFE', c):
            continue
        out.append(('else: ' if arm > 0 else '') + c.strip())
    return out


def main(out_path):
    ROOT = P.ROOT
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        rmap[r['로스터']].append(r['유닛ID'])
    guid_to_asset = {re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1): p
                     for p in glob.glob(os.path.join(ROOT, 'Assets/Data/UnitSkills/*.asset'))}
    asset_units = collections.defaultdict(set)
    for u in glob.glob(os.path.join(ROOT, 'Assets/Data/Units/Roster/*.asset')):
        t = open(u, encoding='utf-8').read().split('  trait:')[0]
        for g in re.findall(r'guid: (\w+), type: 2', t):
            if g in guid_to_asset:
                asset_units[guid_to_asset[g]].add(os.path.basename(u)[:-6])
    rows, stem_occ = [], {}
    for p in sorted(guid_to_asset.values()):
        a = sat.load(p)
        name = os.path.basename(p)[:-6].replace('SkillData_', '')
        if int(re.search(r'^  triggerType: (\d+)', a.head, re.M).group(1)) != 0:
            continue
        units = sorted(asset_units.get(p, ()))
        origs = {o for u in units for o in rmap.get(u, [])} | (set(re.findall(r'\((h\w{3}|H\w{3})\)', a.head)) & set(P.UN))
        stems = set()
        for o in origs:
            stems |= P.triggers_for_unit(o)
        called = {c for s in stems for f in P.STEM_FUNCS.get(s, ()) for c in re.findall(r'TriggerExecute\w*\(gg_trg_(\w+)\)', P.BODY[f])}
        occ = collections.defaultdict(list)
        for s in stems - called:
            if s not in stem_occ:
                stem_occ[s] = [x for f in P.STEM_FUNCS.get(s, ()) if f.endswith('_Actions') for x in X.occurrences(f)]
            for v, path in stem_occ[s]:
                occ[round(v, 6)].append((s, path))
        for li in range(len(a.levels)):
            g = lambda f: sat.get_level_field(a, li, f)
            if float(g('triggerChance') or 0) < 1.0 or float(g('cooldown') or 0) > 0 or (g('requiredBuffId') or '').strip("'") or (g('forbiddenBuffId') or '').strip("'"):
                continue
            for ei, b in enumerate(sat.get_effect_blocks(a, li)):
                d = dict(re.findall(r'(\w+): (\S*)', b))
                own_gate = (d.get('requiredTargetBuffId') or '') + (d.get('forbiddenTargetBuffId') or '')
                for k in ('multiplier', 'bonus'):
                    try:
                        v = float(d.get(k, '0') or 0)
                    except ValueError:
                        continue
                    if not v or abs(v) == 1 or round(v, 6) not in occ:
                        continue
                    places = occ[round(v, 6)]
                    gates = [gate_of(pth) for _, pth in places]
                    if own_gate:   # 효과에 대상 버프 게이트가 이미 있으면 그 버프 조건은 걸러진 것으로 본다
                        gates = [[c for c in gs if own_gate not in c] for gs in gates]
                    if all(gates):
                        rows.append({'asset': name, 'units': ' '.join(units), 'level': li, 'effect': ei, 'field': k, 'value': v,
                                     'stems': ' '.join(sorted({s for s, _ in places})),
                                     'original_gate': ' | '.join(sorted({' & '.join(x)[:160] for x in gates}))[:400]})
    with open(out_path, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['asset', 'units', 'level', 'effect', 'field', 'value', 'stems', 'original_gate'])
        w.writeheader()
        w.writerows(rows)
    print('원문엔 게이트가 있는데 우리는 매 타: %d값 · %d에셋' % (len(rows), len({r['asset'] for r in rows})))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(P.ROOT, 'Docs/research/UNGATED_AUDIT.csv'))
