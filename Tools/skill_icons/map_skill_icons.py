"""우리 SkillData(Assets/Data/UnitSkills) → 원작 능력 코드 → 아이콘 png 대응표(2026-10-06, blender 세션 · 읽기 전용).
   먼저 extract_icons.py를 돌려 ability_icons.csv·~/GRD_skill_icons/를 만든 뒤 돌린다.  /usr/bin/python3 필수(yaml·PIL).
   찾는 순서: ① 파일명·skillName·description에 적힌 원작 능력 코드(w3a에 실제 있는 것만) ② 코드에 아이콘이 없으면(더미 능력 등)
   그 에셋의 원작 트리거/유닛(uid)의 「대표 능력」 아이콘 ③ 사장님 신규 초월 스킬은 설계표의 「참고한 원작 능력」 수동 표.
   쓰는 곳: Tools/skill_icons/skill_icon_map.csv
"""
import sys, os, re, csv, glob, collections, yaml
sys.path.insert(0, 'Tools/w3x')
import w3a, w3u, wts

S = wts.parse('Tools/w3x/원본/war3map_new.wts')
A = w3a.parse('Tools/w3x/원본/war3map_new.w3a')
U = w3u.parse('Tools/w3x/원본/war3map_new.w3u')
J = open('Tools/w3x/원본/war3map_new.j', encoding='utf-8', errors='replace').read()
strip = lambda n: re.sub(r'\|[cC][0-9a-fA-F]{8}|\|r', '', n)

# 능력: 코드 → {name, icon(aart)}
AB = {}
for a in A:
    aid = a['id'] if '\x00' not in a['id'] else None
    if not aid: continue
    f = {}
    for m in a['mods']: f.setdefault(m['field'], m['value'])
    AB[aid] = dict(name=strip(wts.resolve(str(f.get('anam', '')), S)).replace('\n', ' '), icon=f.get('aart', ''))
# 아이콘 표(extract_icons.py 결과): 경로 → (분류, png)
ICON = {}
for r in csv.DictReader(open('Tools/skill_icons/ability_icons.csv', encoding='utf-8-sig')):
    if r['아이콘종류'] == '기본': ICON.setdefault(r['아이콘경로'], (r['맵안/밖'], r['png파일(~/GRD_skill_icons/)'], r.get('출처', '')))
UNIT = {u['id']: dict(name=strip(wts.resolve(str(u['mods'].get('unam', '')), S)), uabi=[x for x in str(u['mods'].get('uabi', '')).split(',') if x]) for u in U}
TRIG2UID = {}
for uid, t in re.findall(r"SaveTriggerHandle\(udg_HashAttack,'(\w{4})',\d+,gg_trg_(\w+)\)", J):
    TRIG2UID.setdefault(t, uid)

def icon_of(code):
    p = AB.get(code, {}).get('icon', '')
    if not p: return None
    cat, png, src = ICON.get(p, ('?', '', ''))
    return dict(path=p, cat=cat, png=png, src=src)

FREQ = collections.Counter(c for u in UNIT.values() for c in set(u['uabi']))

def rep_ability(uid):
    """유닛의 대표 능력: [조합]·스톡을 뺀 첫 커스텀 능력 중 아이콘이 맵 안에 있는 것 → 아이콘 있는 것 → [조합] 초상."""
    best = None; cands = []
    for c in UNIT.get(uid, {}).get('uabi', []):
        if c not in AB or FREQ[c] >= 5 or AB[c]['name'].startswith('T특성') or '특성강화' in AB[c]['name']: continue      # 5유닛 이상이 같이 가진 공용 능력(특성강화 등)은 대표가 못 된다
        ic = icon_of(c)
        if not ic: continue
        cands.append((c, ic))
    for pred in (lambda c, ic: ic['cat'] == '맵안' and '[조합]' not in AB[c]['name'],
                 lambda c, ic: ic['cat'] == '맵안',
                 lambda c, ic: True):
        for c, ic in cands:
            if pred(c, ic): return c
    return None

def _unq(v):
    v = v.strip()
    if v.startswith('"'):
        v = re.sub(r'\\U([0-9A-Fa-f]{8})', lambda m: chr(int(m.group(1), 16)), v)
        v = re.sub(r'\\u([0-9A-Fa-f]{4})', lambda m: chr(int(m.group(1), 16)), v)
        v = re.sub(r'\\x([0-9A-Fa-f]{2})', lambda m: chr(int(m.group(1), 16)), v)
    return re.sub(r'\s*\n\s*', ' ', v).strip('"\'')

def ydec(path):
    """유니티 YAML을 yaml 라이브러리로 읽으면 따옴표 없는 줄에서 터지는 에셋이 있어, skillName·description만 직접 잘라 읽는다."""
    t = open(path, encoding='utf-8').read()
    i = t.find('\n  skillName:'); j = t.find('\n  description:', i); k = t.find('\n  triggerType:', j)
    return {'skillName': _unq(t[i + 13:j]), 'description': _unq(t[j + 15:k])}

CODE = re.compile(r'(?<![A-Za-z0-9])([Aa][0-9A-Z][0-9A-Z][0-9A-Z])(?![A-Za-z0-9])')
def codes_in(text):
    out = []
    for m in CODE.finditer(text):
        c = m.group(1)
        if c in AB and c not in out: out.append(c)
    return out

# 사장님 신규 초월 스킬 수동 표: 에셋 이름 일부 → (원작 능력 코드 또는 'unit:uid', 설계표 근거, 확신도)
def find_unit(sub, grade):
    for k, v in UNIT.items():
        if sub in v['name'] and grade in v['name'] and k[0] in 'hH': return k
MANUAL = {}
tasi = TRIG2UID['Tashigi_Attack']; sabo = TRIG2UID['Sabo_Attack']; dople = TRIG2UID['DP_Attack']; jimbe = TRIG2UID['jinbe_Attack']; robin = TRIG2UID['Robin_Attack']
MANUAL.update({
    '초월_최상호_AD_상호파의수장': ('A0GQ', '구일 설계: 이감 = 원작 A0GQ 귀기-조초(적 이속 −30% 오라) · 공증 오라 참고 A0V8', '높음'),
    '초월_최상호_AD_그분의후계자_부여': ('unit:%s' % tasi, '구일 설계: 아머브레이크 = 원작 타시기(초월) Tasigi_02 — 능력 코드 없는 트리거라 타시기 대표 능력', '보통'),
    '초월_최상호_AD_그분의후계자': ('unit:%s' % tasi, '구일 설계: 원작 타시기(초월) Tasigi_02 평타 9% AId1 +5 — 대표 능력', '보통'),
    '초월_최상호_AD_상호파집결': ('unit:%s' % sabo, '구일 설계: 소환(원작 초월 사보 1/10 기준) — 사보(초월) 대표 능력', '보통'),
    '초월_최상호_AD_왕의자질': ('unit:%s' % jimbe, '보잡 = 원작 근거 없음 → 구일이 교체한 원작 징베(초월) 대표 능력을 제안', '낮음'),
    '초월_노태현_AP_돌발행동': ('unit:%s' % dople, '노태현 설계: 깡딜 = 원작 도플라밍고(초월) DP_Skill_2 1/10 — 대표 능력', '보통'),
    '초월_노태현_AP_가리지않는수단과방법_체력스킬': ('unit:%s' % dople, '노태현 설계: 체력스킬 = 도플 일반 25%·김만경/키드 LIFE50 — 도플 대표 능력', '보통'),
    '초월_노태현_AP_가리지않는수단과방법_보잡': ('unit:%s' % dople, '보잡 = 원작 근거 없음 → 같은 스킬 묶음의 도플 아이콘 제안', '낮음'),
    '초월_노태현_AP_하체부실': ('unit:H096', '노태현 설계: 이동속도감소 = 원작 초월 양재모 AD 「로키포트 사건의 주모자」 1/8(H096) — 대표 능력', '보통'),
    '초월_노태현_AP_시너지폭발': ('unit:%s' % jimbe, '폭발증폭은 원작 근거 없음 — 스플래시 300은 최상호 구일(징베 교체)과 같은 반경이라 징베 대표 능력', '낮음'),
    '초월_노태현_AP_반사회적인격': ('A04D', '디버프(아군 이속 감소)는 원작 근거 없음 — 이 유닛 원작 초월 노태현의 대표 능력 A04D 소울 솔리드', '낮음'),
})

# 에셋은 있는데 원작 대응 능력이 없는 사장님 신규 스킬·공용 디버프: 효과 이름에 맞는 웹 표준 BTN을 직접 고른다(2026-10-06, 확신도 낮음 = 모양만 맞춘 임시 그림)
STD_MANUAL = {
    '공용_디버프_01': ('btncripple', '공격력↓'), '공용_디버프_문': ('btnslow', '공속↓'), '공용_디버프_외동': ('btnscrollofhaste', '적 이속↑'), '공용_디버프_조씨': ('btnhardenedskin', '적 방어↑'),
    '강재규_AP_3대악질': ('btncleavingattack', '평타 확률 추가피해'), '강재규_AP_간잽이': ('btnstormbolt', '스턴'), '강재규_AP_강약약강_끝딜': ('btnvorpalblades', '끝딜'),
    '강재규_AP_강약약강_디버프비례': ('btnunholystrength', '디버프 비례'), '강재규_AP_만성피로': ('btnsleep', '피로'),
    '박민석_ADAP_공복상태': ('btnlifedrain', '체력스킬'), '박민석_ADAP_불가항력': ('btngenericspellimmunity', '보잡'), '박민석_ADAP_외동LvDevil': ('btndoom', '대상 한 기'), '박민석_ADAP_흑인': ('btnshadowpact', '이감 발동'),
    '박민수_AD_무시무시한성장속도': ('btnstatup', '시간비례 피해 증가'), '박민수_AD_인싸Lv4': ('btnfirebolt', '현재체력 단일 피해'), '박민수_AD_체육특기생_억제기': ('btnwarstomp', '범위 스턴'),
    '양재모_AD_간호학과대표': ('btnregenerationaura', '체력회복 오라'), '양재모_AD_다한증': ('btnslow', '이속감소'), '양재모_AD_상호파의최강자': ('btnstormhammer', '평타 확률 피해'),
    '임장혁_AD_가스라이팅': ('btncharm', '아군 스킬 피해 오라'), '임장혁_AD_고충해소': ('btndisenchant', '디버프 해제'), '임장혁_AD_신나는연주': ('btnreplenishmana', '마나 오라'),
    '임장혁_AD_악보완성': ('btndrum', '아군 공속'), '임장혁_AD_이간질': ('btncurse', '아군 공격력 디버프'),
    '최상호_AP_분노조절장애': ('btnberserkfortrolls', '공속 증가'), '최상호_AP_절대공격': ('btnmanadrain', '마나스킬 깡딜'), '최상호_AP_절대방어': ('btndefend', '체력스킬 스턴'),
}

rows = []
def add(path, kind, code, how, conf, alt=''):
    ic = icon_of(code) if code else None
    if ic and not ic['png'] and code == 'A0GQ' and os.path.exists(os.path.expanduser('~/GRD_skill_icons/std_btnshade.png')):   # PASBTNShadeTrueSight는 못 구함 → 같은 그림 결의 BTNShade(유령)로 임시 대체
        ic = dict(ic, png='std_btnshade.png', src='웹 모음(대체: BTNShade)')
    rows.append([path, kind, code or '', AB.get(code, {}).get('name', '') if code else '', how, (ic or {}).get('path', ''), (ic or {}).get('cat', '아이콘없음'), (ic or {}).get('png', ''), (ic or {}).get('src', ''), alt, conf])

for f in sorted(glob.glob('Assets/Data/UnitSkills/*.asset')):
    base = os.path.basename(f)[:-6]
    d = ydec(f)
    name = str(d.get('skillName', '')); desc = str(d.get('description', ''))
    kind = base.split('_')[1] if base.startswith('SkillData_') else ''
    rel = f
    std = next((v for k, v in STD_MANUAL.items() if base.endswith(k) or k in base), None)
    if std and not any(k in base for k in MANUAL):
        png = 'std_%s.png' % std[0]
        if os.path.exists(os.path.expanduser('~/GRD_skill_icons/' + png)):
            rows.append([rel, kind, '', '', '사장님 신규 스킬·공용 디버프: 원작 대응 없음 → 효과(%s)에 맞춘 웹 표준 BTN 수동 지정' % std[1], 'BTN\\' + std[0], '맵밖', png, '웹 모음(수동 선택)', '', '낮음'])
            continue
    man = next((v for k, v in MANUAL.items() if k in base), None)
    if man:
        code, why, conf = man
        if code.startswith('unit:'):
            uid = code[5:]; rc = rep_ability(uid)
            add(rel, kind, rc, '사장님 설계표: %s [유닛 %s %s]' % (why, uid, UNIT.get(uid, {}).get('name', '')[:20]), conf)
        else:
            add(rel, kind, code, '사장님 설계표: ' + why, conf)
        continue
    fc = codes_in(base)        # 파일명 코드
    nc = codes_in(name)
    dc = codes_in(desc)
    ordered = []
    for c in fc + nc + dc:
        if c not in ordered: ordered.append(c)
    pick = None
    for c in ordered:
        if icon_of(c): pick = c; break
    if pick:
        src = '파일명' if pick in fc else ('skillName' if pick in nc else 'description')
        conf = '높음' if pick in fc or pick in nc else ('높음' if len(ordered) == 1 else '보통')
        alt = ' | '.join('%s(%s)' % (c, icon_of(c)['cat']) for c in ordered if c != pick and icon_of(c))[:160]
        add(rel, kind, pick, '코드 %s에서' % src, conf, alt)
        continue
    # 코드에 아이콘이 없거나 코드가 없음 → 트리거/유닛 uid의 대표 능력
    uid = None; how = ''
    mu = re.search(r'(?<![A-Za-z0-9])([hH][0-9A-Z]{3})(?![A-Za-z0-9])', base + ' ' + name + ' ' + desc)
    trigs = re.findall(r'Trig_(\w+?)(?:[ )→,·.]|$)', desc)
    if kind == '원작트리거':
        t = base.split('_', 3)[-1] if base.count('_') >= 3 else ''
        trigs = [t] + trigs
    for t in trigs:
        for cand in (t, t.split('_')[0]):
            if cand in TRIG2UID: uid = TRIG2UID[cand]; how = '트리거 ' + cand; break
        if uid: break
    if not uid and mu and mu.group(1) in UNIT: uid = mu.group(1); how = '유닛 uid ' + uid
    if uid:
        rc = rep_ability(uid)
        add(rel, kind, rc, '아이콘 있는 코드 없음 → %s(%s)의 대표 능력' % (how, UNIT[uid]['name'][:16]), '낮음' if rc else '없음')
    else:
        add(rel, kind, None, '코드·트리거·uid를 못 찾음', '없음')

# 아직 에셋이 없는 계획(바지사장·강재규 설계표) — 에셋 생기면 연결할 제안
PLAN = [  # 바지사장·강재규·양재모 에셋이 생겨 STD_MANUAL로 옮김
]
for nm, code, why, conf in PLAN:
    if code and code.startswith('unit:'):
        uid = code[5:]; rc = rep_ability(uid) if uid != 'None' else None
        add(nm, '계획', rc, why + ' [유닛 %s]' % uid, conf)
    else:
        add(nm, '계획', code, why, conf)

with open('Tools/skill_icons/skill_icon_map.csv', 'w', encoding='utf-8-sig', newline='') as fh:
    w = csv.writer(fh)
    w.writerow(['에셋 경로', '종류', '원작 능력 코드', '원작 능력 이름', '찾은 방법', '아이콘 경로(원작)', '맵안/밖', 'png(~/GRD_skill_icons/)', '그림 출처', '대체 후보', '확신도'])
    w.writerows(rows)
n = len(rows)
print('행', n, '| 분류', dict(collections.Counter(r[6] for r in rows)), '| 확신도', dict(collections.Counter(r[10] for r in rows)))
print('에셋 중 png 있음(맵 안)', sum(1 for r in rows if r[7]), '| 종류별', dict(collections.Counter(r[1] for r in rows if r[7])))
