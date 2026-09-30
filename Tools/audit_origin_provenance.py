"""원작능력_* 스킬 에셋의 효과 값이 그 유닛의 원작에서 왔는지 기계로 대조한다 — 2026-09-30 구현담당1(PM 지시).

배경: 「원작능력_*」 에셋은 1채널 순위 배정·2차 배정이 강타 하나의 확률 레벨에 남의 몫을 몰아 넣은 경우가 많았다
(불멸_신지우·제한_전법규·불멸_고도현 — 53a27828). 효과 값마다 출처를 찾아 표로 낸다.

대조 범위(그 에셋을 쓰는 로스터 유닛 → Docs/reference/MASTER_UID_ROSTER_MAP.csv의 원작 유닛들):
  - 원작 유닛 uabi/uabh 능력의 w3a 수치 필드(모든 레벨), ACbh는 Hbh1/100도
  - 그 유닛에 걸린 트리거(HashAttack 등록 + 원문에서 그 유닛 ID를 직접 쓰는 트리거) → ConditionalTriggerExecute로
    불리는 트리거까지(닫힘) — 함수 본문의 숫자 리터럴 전부
  - 그 트리거들이 만드는 더미 유닛(e…)의 능력 w3a 수치
값: multiplier·bonus(0 아님)·duration(0 아님)·randMax(1 아님). 상대 오차 1e-4로 같으면 찾음.
못 찾으면 원문 전체(모든 트리거 숫자·모든 w3a 수치)에서 그 값을 가진 곳을 적는다.
⚠️ 원문 식에서 곱·합으로 유도한 값(예: (x×1.75+900000)×1.25 → 2.1875)은 「없음」으로 나온다 — 사람이 원문을 볼 것.

사용: python3 Tools/audit_origin_provenance.py [출력.csv]
"""
import collections
import csv
import glob
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import skill_asset_tool as sat  # noqa: E402
import w3a  # noqa: E402
import w3u  # noqa: E402

J = open(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.j'), encoding='utf-8', errors='replace', newline='').read()
AB = {a['id']: (a['base'], w3a.fields_by_level(a)) for a in w3a.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3a'))}
UN = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}

FUNCS = [(m.start(), m.group(1)) for m in re.finditer(r'function (\w+) takes', J)]
BODY = {}
for k, (p, fn) in enumerate(FUNCS):
    e = FUNCS[k + 1][0] if k + 1 < len(FUNCS) else len(J)
    BODY[fn] = J[p:e]


def stem_of(fn):
    m = re.match(r'Trig_(.+?)_(Actions|Conditions|Func\w+)$', fn)
    return m.group(1) if m else None


STEM_FUNCS = collections.defaultdict(list)
for fn in BODY:
    s = stem_of(fn)
    if s:
        STEM_FUNCS[s].append(fn)

NUM = re.compile(r'(?<![\w.])(\d+\.\d+|\d+)(?![\w.])')


def nums(text):
    return {float(x) for x in NUM.findall(text)}


def ability_nums(aid):
    out = set()
    if aid not in AB:
        return out
    base, f = AB[aid]
    for k, lv in f.items():
        for v in lv.values():
            if isinstance(v, (int, float)) and not isinstance(v, bool):
                out.add(float(v))
                if k == 'Hbh1':
                    out.add(float(v) / 100.0)
    return out


def unit_abilities(uid):
    m = UN[uid]['mods'] if uid in UN else {}
    return [x for k in ('uabi', 'uabh') for x in str(m.get(k, '')).split(',') if x]


# 트리거 → 원작 유닛: HashAttack 등록 + 본문에 유닛 ID 리터럴
HASH = collections.defaultdict(set)
for m in re.finditer(r"SaveTriggerHandle\(\w+,'(\w{4})',\d+,gg_trg_(\w+)\)", J):
    HASH[m.group(1)].add(m.group(2))


def triggers_for_unit(uid):
    stems = set(HASH.get(uid, ()))
    for s, fns in STEM_FUNCS.items():
        if any("'%s'" % uid in BODY[f] for f in fns):
            stems.add(s)
    # 닫힘: ConditionalTriggerExecute / TriggerExecute로 부르는 트리거
    todo = list(stems)
    while todo:
        s = todo.pop()
        for f in STEM_FUNCS.get(s, ()):
            for c in re.findall(r'TriggerExecute\w*\(gg_trg_(\w+)\)', BODY[f]):
                if c not in stems:
                    stems.add(c)
                    todo.append(c)
    return stems


def stem_nums_and_dummies(stem):
    ns, dummies = set(), set()
    for f in STEM_FUNCS.get(stem, ()):
        ns |= nums(BODY[f])
        dummies |= set(re.findall(r"'(e\w{3})'", BODY[f]))
    return ns, dummies


# 원문 전체 역색인(없을 때 어디 있는지)
GLOBAL_STEM = collections.defaultdict(set)
for s, fns in STEM_FUNCS.items():
    for f in fns:
        for v in nums(BODY[f]):
            GLOBAL_STEM[round(v, 4)].add(s)
GLOBAL_ABIL = collections.defaultdict(set)
for aid in AB:
    for v in ability_nums(aid):
        GLOBAL_ABIL[round(v, 4)].add(aid)


def close(v, pool):
    for p in pool:
        if p == v or (p != 0 and abs(p - v) / abs(p) < 1e-4):
            return True
    return False


def main(out_path):
    roster_map = collections.defaultdict(list)
    for r in csv.DictReader(open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        roster_map[r['로스터']].append(r['유닛ID'])
    guid_to_asset = {}
    for p in glob.glob(os.path.join(ROOT, 'Assets/Data/UnitSkills/*.asset')):
        guid_to_asset[re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1)] = p
    asset_units = collections.defaultdict(set)
    for u in glob.glob(os.path.join(ROOT, 'Assets/Data/Units/Roster/*.asset')):
        t = open(u, encoding='utf-8').read().split('  trait:')[0]
        for g in re.findall(r'guid: (\w+), type: 2', t):
            if g in guid_to_asset:
                asset_units[guid_to_asset[g]].add(os.path.basename(u)[:-6])

    cache = {}

    def unit_pool(uid):
        if uid in cache:
            return cache[uid]
        abil, trig, stems = set(), set(), triggers_for_unit(uid)
        for a in unit_abilities(uid):
            abil |= ability_nums(a)
        dummy_abil = set()
        for s in stems:
            n, d = stem_nums_and_dummies(s)
            trig |= n
            for e in d:
                for a in unit_abilities(e):
                    dummy_abil |= ability_nums(a)
        cache[uid] = (abil, trig, dummy_abil, stems)
        return cache[uid]

    # 같은 로스터 유닛의 다른 에셋에 같은 효과(kind·basis·multiplier·bonus)가 있으면 사본 의심
    unit_assets = collections.defaultdict(set)
    for ap, us in asset_units.items():
        for u in us:
            unit_assets[u].add(ap)
    sig_index = collections.defaultdict(set)   # (unit, sig) -> assets
    for ap in guid_to_asset.values():
        if not asset_units.get(ap):
            continue
        aa = sat.load(ap)
        for li in range(len(aa.levels)):
            for b in sat.get_effect_blocks(aa, li):
                d = dict(re.findall(r'(\w+): (\S*)', b))
                try:
                    sig = (d.get('kind'), d.get('basis'), round(float(d.get('multiplier', '0') or 0), 4), round(float(d.get('bonus', '0') or 0), 4))
                except ValueError:
                    continue
                for u in asset_units[ap]:
                    sig_index[(u, sig)].add(os.path.basename(ap)[:-6].replace('SkillData_', ''))

    rows = []
    for p in sorted(glob.glob(os.path.join(ROOT, 'Assets/Data/UnitSkills/SkillData_원작능력_*.asset'))):
        a = sat.load(p)
        units = sorted(asset_units.get(p, ()))
        # 원작 유닛 = 대응표 ∪ 에셋 설명의 「원작 …(hXXX)」 ∪ skillName 능력 ID의 소유 유닛(1채널 순위 배정은 대응표에 없는 유닛일 수 있다)
        origs = {o for u in units for o in roster_map.get(u, [])}
        origs |= set(re.findall(r'\((h\w{3}|H\w{3})\)', a.head)) & set(UN)
        m2 = re.search(r'^  skillName: \'?(A\w{3})\b', a.head, re.M)
        if m2:
            origs |= {uid for uid in UN if m2.group(1) in unit_abilities(uid) and uid[0] in 'hH'}
        origs = sorted(origs)
        pools = [(o,) + unit_pool(o) for o in origs]
        for li in range(len(a.levels)):
            for ei, b in enumerate(sat.get_effect_blocks(a, li)):
                d = dict(re.findall(r'(\w+): (\S*)', b))
                try:
                    sig = (d.get('kind'), d.get('basis'), round(float(d.get('multiplier', '0') or 0), 4), round(float(d.get('bonus', '0') or 0), 4))
                except ValueError:
                    sig = None
                me = os.path.basename(p)[:-6].replace('SkillData_', '')
                dups = sorted({x for u in units for x in sig_index.get((u, sig), ()) if x != me}) if sig and (sig[2] or sig[3]) else []
                vals = []
                for k in ('multiplier', 'bonus', 'duration', 'randMax'):
                    try:
                        v = float(d.get(k, '0') or 0)
                    except ValueError:
                        continue
                    if (k in ('multiplier', 'bonus', 'duration') and v != 0) or (k == 'randMax' and v != 1):
                        vals.append((k, v))
                for k, v in vals:
                    where = []
                    for o, abil, trig, dab, stems in pools:
                        if close(v, abil):
                            where.append('%s:w3a' % o)
                        if close(v, trig):
                            where.append('%s:트리거' % o)
                        if close(v, dab):
                            where.append('%s:더미' % o)
                    others = ''
                    if not where:
                        others = ' '.join(sorted(GLOBAL_STEM.get(round(v, 4), ()))[:6])
                        ab = sorted(GLOBAL_ABIL.get(round(v, 4), ()))[:6]
                        if ab:
                            others += ' | w3a:' + ' '.join(ab)
                    rows.append({
                        'asset': os.path.basename(p)[:-6].replace('SkillData_', ''),
                        'units': ' '.join(units), 'orig_units': ' '.join(origs),
                        'level': li, 'effect': ei, 'kind': d.get('kind'), 'basis': d.get('basis'),
                        'field': k, 'value': v,
                        'verdict': '찾음' if where else ('다른 곳에 있음' if others.strip() else '어디에도 없음'),
                        'found_in': ' '.join(where), 'elsewhere': others.strip(),
                        'same_effect_in_other_asset': ' '.join(dups),
                    })
    with open(out_path, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    c = collections.Counter(r['verdict'] for r in rows)
    print(len(rows), dict(c))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'Docs/research/ORIGIN_ABILITY_PROVENANCE.csv'))
