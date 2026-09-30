"""스킬 에셋 한 레벨(한 굴림) 안의 두 가지 결함을 기계로 찾는다 — 2026-09-30 구현담당1(PM 지시).

배경: 원작능력_전설적인_홍인창이 Trig_Legend22의 세 분기(if PV==200 / elseif B06B / else)를 확률 1.0 한 굴림에
중복까지 넣어 매 타 냈다(R50 한 마리 2.9초, 2d439a05). 출처 검사(audit_origin_provenance)는 「값이 원작에 있나」만
보므로 이 모양을 못 잡는다.

  ① 중복: 같은 레벨에 똑같은 효과 블록(모든 필드 동일)이 두 번 이상. 확률 1.0(OnHitChance) 레벨은 따로 센다.
  ② 배타 분기: 같은 레벨의 두 효과 값이 원문에서 같은 if 사슬의 서로 다른 갈래(if/elseif/else)에만 나온다.
     - 원문 위치 = 그 에셋 원작 유닛(audit_origin_provenance와 같은 규칙)에 걸린 트리거의 RRD( … ) 인자 안 숫자.
     - ForGroup 콜백(function X를 넘기는 자리)과 TriggerExecute로 불린 트리거는 부른 자리의 갈래를 물려받는다.
     - Stage 분기(`Stage[GlobalTV]==n`, 시간 순서로 차례로 도는 것)는 배타로 치지 않는다.
     - 값이 여러 곳에 있으면 「같이 나올 수 있는 짝」이 하나라도 있으면 배타 아님(보수적으로).
     - 이미 우리 쪽에서 갈라 둔 짝은 뺀다: 같은 cascadeGroup(≠0), PV 조건이 서로 겹치지 않음, 같은 버프 required/forbidden.
⚠️ 기계 판정이라 사람이 원문을 볼 것 — 특히 값이 흔한 숫자(0.01 등)면 다른 식의 같은 숫자와 섞일 수 있다.

사용: python3 Tools/audit_bundle_exclusive.py [출력.csv]
"""
import collections
import csv
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audit_origin_provenance as P  # noqa: E402  (J·BODY·UN·유닛→트리거 규칙 재사용)
import skill_asset_tool as sat  # noqa: E402

ROOT = P.ROOT
TOK = re.compile(r"(?<![\w.])(elseif|else|endif|if)(?![\w])|function (\w+)|TriggerExecute\w*\(gg_trg_(\w+)\)|RRD\(")


def cond_is_stage(text):
    """if 조건이 Stage 비교면(시간 순서) True — 조건 함수 호출이면 그 함수 본문까지 본다."""
    if 'Stage[' in text:
        return True
    m = re.search(r'(Trig_\w+C)\(\)', text)
    return bool(m and m.group(1) in P.BODY and 'Stage[' in P.BODY[m.group(1)])


def rrd_numbers(body, start):
    """RRD( 부터 짝 맞는 ) 까지의 숫자 리터럴."""
    depth, i = 0, start
    while i < len(body):
        c = body[i]
        if c == '(':
            depth += 1
        elif c == ')':
            depth -= 1
            if depth == 0:
                break
        i += 1
    return P.nums(body[start:i]), i


_occ_cache = {}


def occurrences(fn, depth=0, seen=()):
    """함수 fn 안의 RRD 숫자마다 (값, 갈래 경로) — 경로는 ((if_id, 갈래번호), …)."""
    key = fn
    if key in _occ_cache:
        return _occ_cache[key]
    body = P.BODY.get(fn, '')
    out = []
    stack = []   # [(if_id, arm, is_stage)]
    counter = 0
    pos = 0
    while True:
        m = TOK.search(body, pos)
        if not m:
            break
        pos = m.end()
        kw, fref, texec = m.group(1), m.group(2), m.group(3)
        path = tuple((f'{fn}#{i}', a) for i, a, st in stack if not st)
        if kw == 'if':
            line_end = body.find('then', m.end())
            stack.append((counter, 0, cond_is_stage(body[m.end():line_end if line_end > 0 else m.end() + 200])))
            counter += 1
        elif kw == 'elseif' and stack:
            i, a, st = stack[-1]
            line_end = body.find('then', m.end())
            stack[-1] = (i, a + 1, st or cond_is_stage(body[m.end():line_end if line_end > 0 else m.end() + 200]))
        elif kw == 'else' and stack:
            i, a, st = stack[-1]
            stack[-1] = (i, a + 1, st)
        elif kw == 'endif' and stack:
            stack.pop()
        elif m.group(0) == 'RRD(':
            ns, end = rrd_numbers(body, m.start() + 3)
            out += [(v, path) for v in ns]
            pos = end
        elif fref and depth < 4 and fref in P.BODY and fref != fn and fref not in seen and not fref.endswith('C') and 'takes' not in body[m.end():m.end() + 7]:
            out += [(v, path + p) for v, p in occurrences(fref, depth + 1, seen + (fn,))]
        elif texec and depth < 4:
            for f in P.STEM_FUNCS.get(texec, ()):
                if f.endswith('_Actions') and f not in seen:
                    out += [(v, path + p) for v, p in occurrences(f, depth + 1, seen + (fn,))]
    _occ_cache[key] = out
    return out


def exclusive(p1, p2):
    """두 갈래 경로가 같은 if의 다른 갈래를 지나면 배타."""
    d1 = dict(p1)
    return any(k in d1 and d1[k] != a for k, a in p2)


def pv_disjoint(e1, e2):
    def rng(e):
        c, v = int(e.get('targetCondition', 0) or 0), float(e.get('targetConditionValue', 0) or 0)
        return {0: None, 1: ('lt', v), 2: ('eq', v), 3: ('ge', v), 4: ('ne', v)}.get(c)
    a, b = rng(e1), rng(e2)
    if not a or not b:
        return False
    probe = [0.0, 100.0, 199.0, 200.0, 250.0, 300.0, 1000.0]
    ok = lambda r, x: {'lt': x < r[1], 'eq': x == r[1], 'ge': x >= r[1], 'ne': x != r[1]}[r[0]]
    return not any(ok(a, x) and ok(b, x) for x in probe)


def already_split(e1, e2):
    g1, g2 = int(e1.get('cascadeGroup', 0) or 0), int(e2.get('cascadeGroup', 0) or 0)
    if g1 and g1 == g2:
        return True
    if pv_disjoint(e1, e2):
        return True
    for x, y in ((e1, e2), (e2, e1)):
        r = x.get('requiredTargetBuffId', '')
        if r and r == y.get('forbiddenTargetBuffId', ''):
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

    stem_occ = {}
    rows, n_dup_assets, n_dup_assets_tc1, n_exc_assets = [], set(), set(), set()
    for p in sorted(guid_to_asset.values()):
        a = sat.load(p)
        name = os.path.basename(p)[:-6].replace('SkillData_', '')
        trig_type = int(re.search(r'^  triggerType: (\d+)', a.head, re.M).group(1))
        units = sorted(asset_units.get(p, ()))
        origs = {o for u in units for o in roster_map.get(u, [])}
        origs |= set(re.findall(r'\((h\w{3}|H\w{3})\)', a.head)) & set(P.UN)
        m2 = re.search(r"^  skillName: '?(A\w{3})\b", a.head, re.M)
        if m2:
            origs |= {uid for uid in P.UN if m2.group(1) in P.unit_abilities(uid) and uid[0] in 'hH'}
        stems = set()
        for o in origs:
            stems |= P.triggers_for_unit(o)
        # 다른 트리거가 TriggerExecute로 부르는 트리거는 뿌리에서 뺀다 — 부른 자리의 갈래로 이미 들어온다
        called = {c for s in stems for f in P.STEM_FUNCS.get(s, ()) for c in re.findall(r'TriggerExecute\w*\(gg_trg_(\w+)\)', P.BODY[f])}
        occ = collections.defaultdict(list)   # 값 → [(stem, 경로)]
        for s in stems - called:
            if s not in stem_occ:
                stem_occ[s] = [x for f in P.STEM_FUNCS.get(s, ()) if f.endswith('_Actions') for x in occurrences(f)]
            for v, path in stem_occ[s]:
                occ[round(v, 6)].append((s, path))
        for li in range(len(a.levels)):
            tc = float(sat.get_level_field(a, li, 'triggerChance') or 0)
            blocks = sat.get_effect_blocks(a, li)
            effs = [dict(re.findall(r'(\w+): (\S*)', b)) for b in blocks]
            # ① 중복
            cnt = collections.Counter(b.strip() for b in blocks)
            for b, c in cnt.items():
                if c > 1:
                    d = dict(re.findall(r'(\w+): (\S*)', b))
                    tc1 = trig_type == 0 and tc >= 1.0
                    n_dup_assets.add(name)
                    if tc1:
                        n_dup_assets_tc1.add(name)
                    rows.append({'check': '중복', 'asset': name, 'units': ' '.join(units), 'level': li, 'triggerType': trig_type,
                                 'triggerChance': tc, 'effect_a': 'x%d' % c, 'effect_b': '', 'value_a': d.get('multiplier'),
                                 'value_b': d.get('bonus'), 'where': '확률1.0' if tc1 else '', 'note': 'kind %s basis %s target %s' % (d.get('kind'), d.get('basis'), d.get('target'))})
            # ② 배타 분기
            vals = []
            for ei, e in enumerate(effs):
                for k in ('multiplier', 'bonus'):
                    try:
                        v = float(e.get(k, '0') or 0)
                    except ValueError:
                        continue
                    if v and abs(v) != 1 and round(v, 6) in occ:
                        vals.append((ei, v))
            seen_pairs = set()
            for i in range(len(vals)):
                for j in range(i + 1, len(vals)):
                    (e1, v1), (e2, v2) = vals[i], vals[j]
                    if e1 == e2 or (e1, e2) in seen_pairs or already_split(effs[e1], effs[e2]):
                        continue
                    o1, o2 = occ[round(v1, 6)], occ[round(v2, 6)]
                    together = any(s1 != s2 or not exclusive(p1, p2) for s1, p1 in o1 for s2, p2 in o2)
                    if together:
                        continue
                    seen_pairs.add((e1, e2))
                    n_exc_assets.add(name)
                    rows.append({'check': '배타분기', 'asset': name, 'units': ' '.join(units), 'level': li, 'triggerType': trig_type,
                                 'triggerChance': tc, 'effect_a': e1, 'effect_b': e2, 'value_a': v1, 'value_b': v2,
                                 'where': ' '.join(sorted({s for s, _ in o1 + o2})), 'note': ''})
    with open(out_path, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['check', 'asset', 'units', 'level', 'triggerType', 'triggerChance', 'effect_a', 'effect_b',
                                          'value_a', 'value_b', 'where', 'note'])
        w.writeheader()
        w.writerows(rows)
    c = collections.Counter(r['check'] for r in rows)
    print('에셋 %d개 · 중복 %d행(%d에셋, 그중 확률1.0 %d에셋) · 배타분기 %d짝(%d에셋)' % (
        len(guid_to_asset), c['중복'], len(n_dup_assets), len(n_dup_assets_tc1), c['배타분기'], len(n_exc_assets)))


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'Docs/research/BUNDLE_EXCLUSIVE_AUDIT.csv'))
