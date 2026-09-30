"""에셋 전수 기계 점검 — 등급별 채울 목록(2026-09-30)에서 되풀이된 오류 꼴 다섯을 센다. 읽기 전용.

  python3 Tools/skill_coverage/pattern_audit.py            # Docs/research/SKILL_PATTERN_AUDIT.csv 에 쓴다
  python3 Tools/skill_coverage/pattern_audit.py 출력.csv

꼴(열 「꼴」):
  1_대상분류   w3a atar에 ancient/sapper/nonancient/nonsapper 제한이 있는 능력인데, 그 능력의 우리 에셋 효과에
               targetCondition이 0(없음)인 것.  ancient = PV 200 · sapper = PV≥200 (IMMORTAL_FILL_LIST §V-13)
  2_게이지AND  게이지(OnHitCount) 에셋인데 triggerChance가 0<p<1 — 원문의 else 가지(게이지 타가 아닐 때의 확률 블록)를
               「게이지 AND 확률」로 읽었을 수 있다. 또는 설명문 게이트에 「게이지… AND」가 적힌 것.
  3_확률곱     설명문 게이트에 확률 항이 둘 이상(「1/7 AND 1/10」)이고 triggerChance가 그 곱과 같은 것 —
               원문에서 두 if가 독립인지 중첩인지는 사람이 본문으로 확인해야 한다(중첩이면 정상).
  4_중복효과   한 로스터 안에서 (종류·기준·대상·값·횟수·난수·지속)이 같은 효과가 둘 이상.
               「조건만 다름」 = 대상 버프 유·무/PV 조건만 다른 쌍(합치면 무조건 1회와 같음 — 셋째가 또 있으면 과다).
  5_비피해필드 고정 피해 값이 그 로스터 원작 유닛의 능력·더미 능력의 「피해가 아닌 필드」(거리·반경·파 수·배율·지속)
               값과 같고, 피해 필드·트리거 본문의 어떤 숫자와도 안 같은 것. + 확률 1.0인 고정 피해 중 원문에 값이 없는 것.

한계: 능력↔에셋 대응은 파일 이름의 `_AXXX`(확실) 또는 설명문의 `· AXXX` 언급(추정)으로 잡는다 — 열 「근거」에 적는다.
      로스터에 대응 uid가 없으면(히든 19 · 다른세계 등) 1·5는 못 본다. 효과는 레벨 1(levels[0])만 본다.
"""
import sys, re, glob, os, csv, unicodedata
sys.path.insert(0, 'Tools/w3x')
import w3a, w3u

N = lambda s: unicodedata.normalize('NFC', s)
OUT = sys.argv[1] if len(sys.argv) > 1 else 'Docs/research/SKILL_PATTERN_AUDIT.csv'
A = {a['id']: a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
U = {u['id']: u for u in w3u.parse('Tools/w3x/원본/war3map_new.w3u')}
J = open('Tools/w3x/원본/war3map_new.j', encoding='utf-8', errors='replace').read()

DAMAGE_FIELDS = {'Hbh3', 'Wrs1', 'Ucs1', 'Ctc1', 'Htc1', 'Htb1', 'Eim1', 'Efk1', 'Idam', 'Hbz2', 'Ocl1', 'Nst3', 'Hfa1',
                 'Nab4', 'Nab5', 'Spo1', 'liq1', 'Oww1', 'pxf1', 'War2', 'Ocr3', 'Iatt', 'Nbr1', 'Ncs1'}
KIND = ['Damage', 'Stun', 'ArmorBreak', 'ExtraProjectile', 'ArmorBonus', 'HealOverTime', 'ApplyBuff', 'RemoveBuff',
        'AegrStack', 'AisrStack', 'A11SStack', 'AtkFlat', 'ASpd%', 'Slow', 'Atk%']
TT = ['OnHitChance', 'CooldownAuto', 'Aura', 'OnHitCount']


def fields(aid, level=1):
    a = A.get(aid)
    if not a: return {}
    return {m['field']: m['value'] for m in a['mods'] if m['level'] <= level}


# ---------- 원작 쪽: uid → 평타 트리거 폐쇄 → 능력·숫자 ----------
def body(t):
    q = J.find('function Trig_%s_Actions takes' % t)
    if q < 0: return ''
    b = J[q:J.index('endfunction', q)]
    for m in re.finditer(r'function Trig_%s_Func\w+ takes' % re.escape(t), J):
        b += J[m.start():J.index('endfunction', m.start())]
    for fn in set(re.findall(r'function (\w+_Filterfunc\w+)\)', b)):
        q2 = J.find('function %s takes' % fn)
        if q2 >= 0: b += J[q2:J.index('endfunction', q2)]
    return b


_cache = {}
def origin(uid):
    """(능력 ID 집합, 피해 필드 값 집합, 비피해 필드 값 {값: 'AXXX.필드'}, 본문 숫자 집합)"""
    if uid in _cache: return _cache[uid]
    seen = re.findall(r"SaveTriggerHandle\(udg_HashAttack,'%s',\d+,gg_trg_(\w+)\)" % uid, J)
    i = 0; text = ''
    while i < len(seen):
        b = body(seen[i]); text += b
        for x in re.findall(r'gg_trg_(\w+)', b):
            if x not in seen: seen.append(x)
        i += 1
    abil = set(str(U.get(uid, {}).get('mods', {}).get('uabi', '')).split(','))
    for code in set(re.findall(r"'(\w{4})'", text)):
        if code in A: abil.add(code)
        if code in U: abil |= set(str(U[code]['mods'].get('uabi', '')).split(','))
    abil = {x for x in abil if x in A}
    dmg, non = set(), {}
    for aid in abil:
        for k, v in fields(aid, 9).items():
            if isinstance(v, (int, float)) and v not in (0, 1):
                if k in DAMAGE_FIELDS: dmg.add(round(float(v), 4))
                else: non.setdefault(round(float(v), 4), '%s.%s' % (aid, k))
    nums = {round(float(x), 4) for x in re.findall(r'(?<![\w.])(\d+\.?\d*)(?![\w.])', text)}
    ud = U.get(uid, {}).get('mods', {})
    for k in ('ua1b',):
        if isinstance(ud.get(k), (int, float)): nums.add(round(float(ud[k]), 4))
    _cache[uid] = (abil, dmg, non, nums)
    return _cache[uid]


# ---------- 우리 쪽 ----------
G2F = {}
for m in glob.glob('Assets/Data/UnitSkills/*.asset.meta'):
    G2F[re.search(r'guid: (\w+)', open(m).read()).group(1)] = m[:-5]


def load(path):
    t = open(path, encoding='utf-8').read()
    d = re.search(r'\n  description: (.*?)\n  triggerType', t, re.S)
    tt = int(re.search(r'\n  triggerType: (\d+)', t).group(1))
    lv = t[t.find('\n  levels:'):]
    levels = re.split(r'\n  - cooldown:', lv)[1:]
    head, effs = {}, []
    if levels:
        L = 'cooldown:' + levels[0]
        h, _, e = L.partition('effects:')
        head = dict(re.findall(r'(\w+): ([^\n]*)', h))
        for x in re.split(r'\n    - kind:', e)[1:]:
            effs.append(dict(re.findall(r'(\w+): ([^\n]*)', ' kind:' + x)))
    return dict(name=N(os.path.basename(path))[10:-6], desc=(d.group(1) if d else '').replace('\n', ' '), tt=tt, head=head, effs=effs)


def fnum(d, k, default=0.0):
    try: return float(str(d.get(k, default)).strip("'\""))
    except ValueError: return default


MAP = {}
for r in csv.DictReader(open('Docs/reference/MASTER_UID_ROSTER_MAP.csv', encoding='utf-8-sig')):
    MAP.setdefault(r['로스터'], []).append(r['유닛ID'])

rows = []
def add(kind, roster, asset, detail, basis, sev):
    rows.append({'꼴': kind, '로스터': roster, '에셋': asset, '내용': detail, '근거': basis, '세기': sev})


for rp in sorted(glob.glob('Assets/Data/Units/Roster/*.asset')):
    roster = N(os.path.basename(rp))[:-6]
    t = open(rp, encoding='utf-8').read()
    i = t.find('\n  skills:'); j = t.find('\n  trait:', i)
    gs = re.findall(r'guid: (\w+)', t[i:j]) if i >= 0 else []
    one = re.search(r'^  skill: \{fileID: 11400000, guid: (\w+)', t, re.M)
    if not gs and one: gs = [one.group(1)]
    skills = [load(G2F[g]) for g in gs if g in G2F]
    if not skills: continue
    uids = MAP.get(roster, [])
    aspd = fnum(dict(re.findall(r'\n  (attackSpeed): ([^\n]*)', t)), 'attackSpeed', 0)

    # ---- 1. 대상 분류 ----
    for uid in uids:
        for aid in str(U.get(uid, {}).get('mods', {}).get('uabi', '')).split(','):
            f = fields(aid)
            atar = set(str(f.get('atar', '')).split(','))
            need = None
            if 'ancient' in atar and 'nonancient' not in atar: need = 'PV==200(ancient)'
            elif 'nonancient' in atar and 'ancient' not in atar: need = 'PV≠200(nonancient)'
            elif 'sapper' in atar and 'nonsapper' not in atar: need = 'PV≥200(sapper)'
            elif 'nonsapper' in atar and 'sapper' not in atar: need = 'PV<200(nonsapper)'
            if not need or 'none' in atar: continue
            for s in skills:
                how = '파일 이름' if s['name'].endswith('_' + aid) else ('설명문 언급(추정)' if re.search(r'[·(]\s*%s\b' % aid, s['desc']) else None)
                if not how: continue
                if how != '파일 이름' and re.search(r'_A\w{3}$', s['name']): continue      # 다른 능력 하나로 쪼갠 에셋 — 설명문은 옛 목록을 그대로 들고 있다
                real = [e for e in s['effs'] if fnum(e, 'multiplier') or fnum(e, 'bonus') or fnum(e, 'duration')]
                bad = [e for e in real if int(fnum(e, 'targetCondition')) == 0]
                if how.startswith('설명문') and len(s['effs']) > 3: how += '·에셋에 능력 여럿'
                if bad:
                    stun = max([fnum(e, 'duration') for e in bad if int(fnum(e, 'kind')) == 1] or [0])
                    p = fnum(s['head'], 'triggerChance', 1)
                    sev = '높음' if (stun and aspd and p >= 0.99 and stun >= 1 / aspd) else ('보통' if how == '파일 이름' else '낮음')
                    add('1_대상분류', roster, s['name'], '%s(%s, 원작 %s) atar=%s → %s 필요, 효과 %d개 조건 없음(확률 %.4g%s)' % (
                        aid, str(f.get('anam', ''))[:16], uid, ','.join(sorted(atar & {'ancient', 'nonancient', 'sapper', 'nonsapper'})), need, len(bad), p,
                        ' · 스턴 %.2g초 ≥ 평타 주기 %.2f초 = 영구 스턴' % (stun, 1 / aspd) if sev == '높음' else ''), how, sev)

    # ---- 2. 게이지 AND 확률 / 3. 확률 곱 ----
    for s in skills:
        p = fnum(s['head'], 'triggerChance', 1)
        gate = re.search(r'게이트 "([^"]*)"', s['desc'])
        g = gate.group(1) if gate else ''
        if s['tt'] == 3 and 0 < p < 0.999:
            add('2_게이지AND', roster, s['name'], 'OnHitCount(게이지 %s) × 확률 %.6g — 원문에서 그 확률 블록이 게이지 타의 else 가지인지 확인. 게이트 설명: %s' % (
                s['head'].get('hitCountThreshold', '?'), p, g[:80]), '발동 방식+확률', '보통')
        elif re.search(r'(MANA|LIFE)게이지[\d.]+\s+AND', g) and s['effs']:
            add('2_게이지AND', roster, s['name'], '설명문 게이트 「%s」 — 지금 발동 방식 %s · 확률 %.6g' % (g[:80], TT[s['tt']], p), '설명문', '낮음')
        terms = re.findall(r'(?<![\d.])1/(\d+)', g)
        if len(terms) >= 2:
            prod = 1.0
            for x in terms: prod /= int(x)
            same = abs(prod - p) <= max(1e-6, prod * 0.02)
            add('3_확률곱', roster, s['name'], '게이트 「%s」 — 항 %d개, 곱 %.6g, 지금 확률 %.6g%s' % (g[:80], len(terms), prod, p, ' (곱과 같음 — 두 if가 독립이면 과소)' if same else ''),
                '설명문', '보통' if same else '낮음')
        elif 0 < p < 0.02 and s['tt'] == 0 and s['effs']:
            add('3_확률곱', roster, s['name'], '확률 %.6g(= 1/%.1f) — 아주 낮다. 곱으로 만든 값인지 확인' % (p, 1 / p), '확률 값', '낮음')

    # ---- 4. 중복 효과 ----
    seen = {}
    for s in skills:
        for e in s['effs']:
            k = int(fnum(e, 'kind'))
            if not (fnum(e, 'multiplier') or fnum(e, 'bonus')): continue
            if k not in (0, 2, 8, 9, 10): continue          # 피해·방깎·스택만(스턴·버프는 같은 값이 흔하다)
            key = (k, int(fnum(e, 'basis')), int(fnum(e, 'target')), fnum(e, 'multiplier'), fnum(e, 'bonus'), fnum(e, 'hitCount', 1),
                   fnum(e, 'randMin', 1), fnum(e, 'randMax', 1), fnum(e, 'duration'),
                   fnum(e, 'lineLength'), fnum(e, 'zoneTickInterval'))   # 모양이 다르면(원·선·지대) 같은 값이어도 다른 효과(2026-09-30)
            cond = (e.get('requiredTargetBuffId', '').strip(), e.get('forbiddenTargetBuffId', '').strip(), int(fnum(e, 'targetCondition')), fnum(e, 'targetConditionValue'))
            seen.setdefault(key, []).append((s['name'], cond, fnum(s['head'], 'triggerChance', 1), s['tt']))
    for key, L in seen.items():
        if len(L) < 2: continue
        assets = sorted({x[0] for x in L})
        conds = [x[1] for x in L]
        within = len(assets) == 1
        if not within and key[0] != 0: continue
        if not within and len({(x[2], x[3]) for x in L}) > 1 and key[3] < 1000 and key[1] != 0: continue   # 다른 게이트의 같은 % 값은 흔하다
        uncond = sum(1 for c in conds if c == ('', '', 0, 0.0))
        pair = any(c[0] for c in conds) and any(c[1] for c in conds)
        if within and uncond >= 2: note, sev = '같은 에셋 안에 조건 없는 같은 효과 %d개 — %d배' % (uncond, uncond), '높음'
        elif within and pair and uncond >= 1: note, sev = '버프 유·무 쌍 + 무조건 %d개 — 2배' % uncond, '높음'
        elif within and pair: note, sev = '조건만 다름(버프 유·무 쌍 — 합치면 무조건 1회와 같다)', '낮음'
        elif within: note, sev = '같은 에셋 안 같은 값, 조건 다름', '낮음'
        else: note, sev = '다른 에셋에 같은 값(%s)' % ' / '.join(assets)[:120], '보통' if uncond >= 2 else '낮음'
        add('4_중복효과', roster, assets[0], '%s 기준%d 대상%d 값 %.10g%s 횟수 %g ×%d개 — %s' % (
            KIND[key[0]], key[1], key[2], key[3], (' +%.10g' % key[4]) if key[4] else '', key[5], len(L), note), '효과 값 일치', sev)

    # ---- 5. 비피해 필드를 피해로 ----
    if uids:
        dmg, non, nums = set(), {}, set()
        for uid in uids:
            _, d2, n2, m2 = origin(uid)
            dmg |= d2; nums |= m2
            for k, v in n2.items(): non.setdefault(k, v)
        for s in skills:
            p = fnum(s['head'], 'triggerChance', 1)
            for e in s['effs']:
                if int(fnum(e, 'kind')) != 0 or int(fnum(e, 'basis')) != 0: continue
                m = round(fnum(e, 'multiplier'), 4)
                if m in (0, 1): continue
                if m in non and m not in dmg and m not in nums:
                    add('5_비피해필드', roster, s['name'], '고정 피해 %.10g = %s(피해 필드 아님) · 확률 %.4g' % (m, non[m], p), '필드 값 일치', '높음' if p >= 0.99 else '보통')
                elif p >= 0.99 and s['tt'] == 0 and m not in dmg and m not in nums and not s['head'].get('requiredBuffId', '').strip() and fnum(s['head'], 'cooldown') == 0:
                    add('5_비피해필드', roster, s['name'], '확률 1.0(매 타) 고정 피해 %.10g — 대응 uid(%s)의 능력 피해 필드·트리거 본문 어디에도 없는 값' % (m, ','.join(uids)), '원문에 값 없음', '보통')

rows.sort(key=lambda r: (r['꼴'], {'높음': 0, '보통': 1, '낮음': 2}[r['세기']], r['로스터'], r['에셋']))
os.makedirs(os.path.dirname(OUT) or '.', exist_ok=True)
with open(OUT, 'w', encoding='utf-8-sig', newline='') as f:
    w = csv.DictWriter(f, fieldnames=['꼴', '세기', '로스터', '에셋', '내용', '근거'])
    w.writeheader(); w.writerows(rows)
from collections import Counter
c = Counter((r['꼴'], r['세기']) for r in rows)
for k in sorted({r['꼴'] for r in rows}):
    print(k, '높음 %d · 보통 %d · 낮음 %d' % (c[(k, '높음')], c[(k, '보통')], c[(k, '낮음')]))
print('합계', len(rows), '→', OUT)
