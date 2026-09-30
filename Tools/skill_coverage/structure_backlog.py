"""구조 칸 백로그 집계 — 원작 스킬 중 「우리 SkillData 축으로 안 담기는 구조」를 종류별로 센다(2026-09-30 구현담당1, 읽기 전용).

문서가 아니라 원문(war3map_new.j·w3a·w3u)에서 센다:
  유닛(대응표 MASTER_UID_ROSTER_MAP.csv에 있는 uid)마다 뿌리 트리거 = HashAttack 평타 트리거 + 제 uabi 능력을 GetSpellAbilityId로 받는 시전 트리거,
  거기서 TriggerExecute·함수 호출 폐쇄(jparse.closure)를 따라가 함수 본문에서 아래 표식을 찾는다.
  유닛 절반 이상의 폐쇄에 드는 함수(공용 도우미)는 표식에서 뺀다.
표식은 「그 구조가 있을 법하다」는 신호다 — 개수는 상한에 가깝다(같은 트리거가 두 종류에 걸릴 수 있고, 표식이 있어도 실효 없는 줄일 수 있다).
사용(저장소 루트에서): python3 Tools/skill_coverage/structure_backlog.py [--md]
"""
import collections
import csv
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, 'Tools/w3x')
import w3a  # noqa: E402
import w3u  # noqa: E402
from jparse import ACT, COND, FN, HASH, J, closure  # noqa: E402

A = {a['id']: a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
U = {u['id']: u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
BASE = lambda code: A[code]['base'] if code in A else code

MORPH = {'AEme', 'Abrf', 'Amrf', 'ANcr', 'Arav', 'Astn', 'ANrg', 'Aspx', 'AEvi', 'Abur', 'Abu2', 'Abu3', 'Asb1', 'Asb2', 'Asb3', 'Aetf', 'Acpf', 'Ahrl'}
LINE = {'AOsh', 'AUcs', 'ANbf', 'ACbf', 'ACsh', 'ACca', 'ACbc', 'ANfb', 'ACcv', 'AOs2'}
ZONE = {'AHbz', 'ACbz', 'ANrf', 'ACrf', 'AHfs', 'ACfs', 'ANvc', 'AEsf', 'ANst', 'ANcs', 'AOeq', 'ANmo', 'Aclf', 'ANbh', 'ANto', 'AEtq', 'ANlm', 'AUls'}
IMMO = {'AEim', 'ACim', 'ANpi', 'Apig', 'ANak', 'AIcf'}
DOT = {'AEsh', 'Apoi', 'ANab', 'ANso', 'ANpa', 'Aspo', 'Apo2', 'ACvs', 'AHfa', 'AIpm', 'Aliq', 'ANfa', 'ANba'}
AURA = {'AHab', 'AOae', 'ACat', 'Aasl', 'AUau', 'AEar', 'AHad', 'AOr2', 'ACav', 'AUav', 'AEah', 'AOac', 'ACac', 'Aabr', 'Aakb', 'AIcd', 'Aap1', 'Aap2', 'Aap3', 'Aap4', 'Apts', 'ANbl'} | IMMO
MULTI = {'Aroc', 'Aflk', 'ANmm'}
STACKS = {'Aegr', 'AIsr', 'A11S', 'AId1', 'AIsx', 'AIas'}
ORDER_LINE = {'carrionswarm', 'shockwave', 'breathoffire', 'breathoffrost'}
ORDER_ZONE = {'blizzard', 'rainoffire', 'flamestrike', 'starfall', 'stampede', 'clusterrockets', 'volcano', 'monsoon', 'earthquake', 'locustswarm', 'tornado'}
ORDER_MORPH = {'metamorphosis', 'bearform', 'unbearform', 'chemicalrage', 'ravenform', 'robogoblin', 'stoneform'}

KINDS = collections.OrderedDict([
    ('변신', '형태 전환·각성(변신 능력·ReplaceUnit·변신 명령)'),
    ('소환', '소환체·분신(공격하는 유닛을 만든다)'),
    ('직선', '장풍 직선(선 모양 스톡 주문 더미 · 움직이는 더미)'),
    ('이동더미', '움직이는 더미(단계마다 SetUnitX·이동 명령 — 장풍·회오리·걸어가는 오라)'),
    ('지대', '주기 피해 지대·채널 다발(눈보라·화염·스탬피드·클러스터 로켓·더미 이몰레이션)'),
    ('더미오라', '시간제 더미 오라(만든 더미가 오라 능력을 가짐)'),
    ('주기피해', '대상 주기 피해(독·쉐도우 스트라이크·애시드 밤 계열 더미)'),
    ('레벨', '레벨·해금·T강화 갈래(능력 레벨을 조건으로 읽거나 올린다)'),
    ('토글', '토글·bool 갈래(udg bool 변수로 가지가 갈린다)'),
    ('배타', '배타 분기 한 굴림(무작위 조건의 else 가지에 실효 줄)'),
    ('근접', '「적이 근처에 오면」(TriggerRegisterUnitInRange)'),
    ('다중평타', '평타 다중 대상(Aroc 등)'),
    ('능력치', '영웅 능력치 항(GetHeroStr/Agi/Int — 축은 있음, 값 크기 미확인)'),
    ('주변수', '주변 적 수 계수(CountUnitsInGroup을 피해 식에)'),
    ('지연', '지연·단계 피해(SleepForStage·타이머 뒤 피해 — 즉발 근사 가능한지 봐야 함)'),
])


def codes(body, first):
    return set(re.findall(r"'(%s\w{3})'" % first, body))


def created(body):
    out = set()
    for m in re.finditer(r"CreateNUnitsAtLoc(?:FacingLocBJ)?\(\s*\d+\s*,\s*'(\w{4})'|CreateUnit(?:AtLoc)?\([^']*?'(\w{4})'", body):
        out.add(m.group(1) or m.group(2))
    return out


def uabi(uid):
    return [x for x in str(U[uid]['mods'].get('uabi', '')).split(',') if x]


def random_else(fn_name, body):
    """무작위 조건 if의 else 가지에 call이 있나(DoNothing 제외)."""
    stack, hit = [], False
    for line in re.split(r'(?=\b(?:if|else|elseif|endif)\b)', body):
        head = line.lstrip()
        if head.startswith('if'):
            m = re.match(r'if\s*\(?\s*\(?\s*(\w+)\(\)', head)
            rnd = bool(m and 'GetRandom' in FN.get(m.group(1), '')) or 'GetRandom' in head.split('then')[0]
            stack.append(rnd)
        elif head.startswith('elseif'):
            if stack and stack[-1]:
                hit = True
        elif head.startswith('else'):
            if stack and stack[-1] and re.search(r'\bcall (?!DoNothing)', head):
                hit = True
        elif head.startswith('endif'):
            if stack:
                stack.pop()
    return hit


def main(md):
    rmap = collections.defaultdict(list)
    for r in csv.DictReader(open('Docs/reference/MASTER_UID_ROSTER_MAP.csv', encoding='utf-8-sig')):
        if r['유닛ID'] in U and os.path.exists('Assets/Data/Units/Roster/%s.asset' % r['로스터']) and r['유닛ID'] not in rmap[r['로스터']]:
            rmap[r['로스터']].append(r['유닛ID'])
    uids = sorted({u for v in rmap.values() for u in v})
    use = collections.Counter(a for u in U for a in set(uabi(u)))
    cast_trg = collections.defaultdict(list)  # 능력 → 그걸 시전 조건으로 받는 트리거
    for t, fns in COND.items():
        for f in fns:
            for a in re.findall(r"GetSpellAbilityId\(\)\s*==\s*'(\w{4})'", FN.get(f, '')):
                cast_trg[a].append(t)
    clos, roots = {}, {}
    for u in uids:
        r = [t for _, t in HASH.get(u, [])] + [t for a in uabi(u) if use[a] <= 20 for t in cast_trg.get(a, [])]
        roots[u] = r
        clos[u] = closure(start_trgs=r)[0] if r else set()
    freq = collections.Counter(f for u in uids for f in clos[u])
    common = {f for f, n in freq.items() if n > len(uids) / 2}

    # 근접 등록은 폐쇄 밖(조합 트리거가 유닛을 만들며 등록)이고 주인은 udg 변수로만 이어진다 — 등록 5곳을 손으로 읽어 적는다(j 직접, 2026-09-30).
    # H08Z(샹크스 영웅형 「마린포드 정상해전 종결자」)는 대응표에 없다(h04U → 초월_황준석_ADAP의 영웅형으로 보임).
    inrange = {'h05E': {'enel_thunder 950'}, 'h035': {'Legend10_shanks_pegi 1049'}, 'h07I': {'kata_04_pegi 850'},
               'h05C': {'Legend14han_petrification 950'}, 'H08Z': {'Shanks_ET_pegi 1049(대응표 밖)'}}
    assert len(set(re.findall(r'TriggerRegisterUnitInRangeSimple\((udg_\w+)', J))) == 5
    hits = collections.defaultdict(lambda: collections.defaultdict(set))  # 종류 → uid → 근거
    for u in uids:
        mine = set(uabi(u))
        for a in mine:
            b = BASE(a)
            if b in MORPH:
                hits['변신'][u].add('uabi %s(%s)' % (a, b))
            if b in MULTI:
                hits['다중평타'][u].add('uabi %s(%s)' % (a, b))
        if u in inrange:
            hits['근접'][u].update(inrange[u])
        for f in clos[u] - common:
            b = FN[f]
            ab = codes(b, 'A')
            for a in ab:
                base = BASE(a)
                if base in MORPH:
                    hits['변신'][u].add('%s: %s(%s)' % (f, a, base))
                if base in LINE:
                    hits['직선'][u].add('%s: %s(%s)' % (f, a, base))
                if base in ZONE:
                    hits['지대'][u].add('%s: %s(%s)' % (f, a, base))
                if base in DOT:
                    hits['주기피해'][u].add('%s: %s(%s)' % (f, a, base))
            orders = set(re.findall(r'Order\w*\([^"\n]*?"([a-z]+)"', b))
            if orders & ORDER_MORPH or 'ReplaceUnitBJ' in b:
                hits['변신'][u].add('%s: %s' % (f, ' '.join(orders & ORDER_MORPH) or 'ReplaceUnitBJ'))
            if orders & ORDER_LINE:
                hits['직선'][u].add('%s: %s' % (f, ' '.join(orders & ORDER_LINE)))
            if orders & ORDER_ZONE:
                hits['지대'][u].add('%s: %s' % (f, ' '.join(orders & ORDER_ZONE)))
            for c in created(b):
                if c not in U:
                    continue
                cu = U[c]['mods']
                if c[0] in 'hHnNoOuU' and c != u and (cu.get('ua1b') or 0) > 0 and cu.get('uaen', 1) != 0:
                    hits['소환'][u].add('%s: %s' % (f, c))
                for a in str(cu.get('uabi', '')).split(','):
                    if BASE(a) in AURA:
                        hits['더미오라' if BASE(a) not in IMMO else '지대'][u].add('%s: 더미 %s의 %s(%s)' % (f, c, a, BASE(a)))
            # 적에게 쌓는 기존 축(Aegr·AIsr·A11S·AId1 계열 스택)은 뺀다 — 남는 것이 시전자 쪽 능력 유무·레벨 갈래.
            lv = {a for a in re.findall(r"(?:[GS]etUnitAbilityLevel\w*\(|IncUnitAbilityLevel\w*\()[^)]*?'(A\w{3})'", b)
                  if a not in STACKS and BASE(a) not in STACKS}
            if lv:
                hits['레벨'][u].add('%s: %s' % (f, ' '.join(sorted(lv))))
            bools = set(re.findall(r'\b(udg_\w*[Bb]ool\w*)', b))
            if bools:
                hits['토글'][u].add('%s: %s' % (f, ' '.join(sorted(bools))))
            if random_else(f, b):
                hits['배타'][u].add(f)
            if 'GetHeroStatBJ' in b:
                hits['능력치'][u].add(f)
            if any('CountUnitsInGroup' in seg for seg in re.findall(r'call RRD\((.*?)(?=call |endif|else|$)', b, re.S)):
                hits['주변수'][u].add(f)
            if re.search(r'SleepForStage|TimerStart|PolledWait|TriggerSleepAction', b) and re.search(r'\bRRD\(|UnitDamage', b):
                hits['지연'][u].add(f)
            if re.search(r'SetUnitX|SetUnitPosition|"move"', b) and 'PolarProjection' in b and re.search(r'SleepForStage|TimerStart', b):
                hits['이동더미'][u].add(f)

    back = collections.defaultdict(list)
    for ro, us in rmap.items():
        for u in us:
            back[u].append(ro)
    rows = []
    for k, label in KINDS.items():
        us = hits[k]
        ros = sorted({ro for u in us for ro in back[u]})
        rows.append((k, label, sum(len(v) for v in us.values()), len(us), ros))
    if md:
        print('| 구조 | 표식 수(함수·능력) | 원작 유닛 | 로스터 |')
        print('|---|---|---|---|')
        for k, label, n, nu, ros in rows:
            print('| %s | %d | %d | %d |' % (label, n, nu, len(ros)))
        for k, label, n, nu, ros in rows:
            print('\n### %s\n' % label)
            for u in sorted(hits[k], key=lambda x: back[x]):
                print('- %s(%s): %s' % (' · '.join(back[u]), u, ' / '.join(sorted(hits[k][u]))[:400]))
    else:
        print('유닛 %d · 뿌리 없는 유닛 %d · 공용 함수 %d' % (len(uids), sum(1 for u in uids if not roots[u]), len(common)))
        for k, label, n, nu, ros in rows:
            print('%-6s 표식 %3d · 유닛 %3d · 로스터 %3d  %s' % (k, n, nu, len(ros), ' '.join(ros[:6])))


if __name__ == '__main__':
    main('--md' in sys.argv)
