#!/usr/bin/python3
# 초월 유닛 25종의 스킬을 효과 종류별로 모아 Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md를 만든다.
# 우리 값은 SkillData 에셋에서, 원작 값은 war3map_new.w3a를 w3a.parse로 직접 디코드해 같은 줄에 적는다(조사 문서는 근거로 안 쓴다).
import glob, re, sys, collections
sys.path.insert(0, 'Tools/w3x')
import w3a

KIND = {0:'Damage',1:'Stun',2:'ArmorBreak',3:'ExtraProjectile',4:'ArmorBonus',5:'HealOverTime',6:'ApplyBuff',7:'RemoveBuff',8:'AegrStack',9:'AisrStack',10:'A11SStack',11:'AttackPowerBuffFlat',12:'AttackSpeedBuffPercent',13:'Slow',14:'AttackPowerBuffPercent',15:'A0VJStack'}
TRIG = {0:'평타 확률(OnHitChance)',1:'쿨다운 자동(CooldownAutoCast)',2:'오라(Aura)',3:'N타째(OnHitCount)',4:'적 접근(OnEnemyEnterRange)'}
TARGET = {0:'Self',1:'Allies',2:'Enemies',3:'SingleTarget'}

abilities = {a['id']: a for a in w3a.parse('Tools/w3x/원본/war3map_new.w3a')}
guid_path = {}
for f in glob.glob('Assets/Data/**/*.asset', recursive=True):
    m = re.search(r'guid: (\w+)', open(f + '.meta', encoding='utf8').read())
    if m: guid_path[m.group(1)] = f

def unquote(s):
    s = s.strip()
    if s.startswith('"'):
        import json
        return json.loads(s)
    return s

def num(block, key, default=''):
    m = re.search(r'^\s*%s: ([-\d.eE+]+)' % key, block, re.M)
    return m.group(1) if m else default

units = {}
for f in sorted(glob.glob('Assets/Data/Units/Roster/초월_*.asset')):
    t = open(f, encoding='utf8').read()
    skills = re.search(r'^  skills:\n((?:  - .*\n)+)', t, re.M)
    gs = re.findall(r'guid: (\w+)', skills.group(1)) if skills else []
    units[f.split('/')[-1][:-6]] = gs

rows = collections.defaultdict(list)   # kind -> rows
for unit, gs in units.items():
    for g in gs:
        sf = guid_path.get(g)
        if not sf: continue
        s = open(sf, encoding='utf8').read()
        name = unquote(re.search(r'^  skillName: (.*)', s, re.M).group(1))
        trig = int(num(s, 'triggerType', '0'))
        code = re.match(r'^(A[0-9A-Za-z]{3}|[a-zA-Z][0-9A-Za-z]{3})\b', name)
        raw = ''
        if code and code.group(1) in abilities:
            a = abilities[code.group(1)]
            byf = w3a.fields_by_level(a)
            keys = [k for k in byf if k not in ('anam', 'atp1', 'aub1', 'auhk', 'aart', 'arar', 'ahky', 'aord', 'aeat', 'aeft')]
            raw = a['base'] + ' ' + ' '.join(f"{k}={byf[k].get(1, list(byf[k].values())[0])}" for k in keys[:9])
        lvl = re.search(r'  levels:\n(.*?)(?=\n  \w|\Z)', s, re.S)
        lv = lvl.group(1) if lvl else ''
        cooldown = num(lv, 'cooldown'); chance = num(lv, 'triggerChance'); rng = num(lv, 'range')
        for m in re.finditer(r'- kind: (\d+)\n((?:      .*\n)+)', s):
            k = int(m.group(1)); b = m.group(2)
            rows[KIND.get(k, str(k))].append((unit, name, TRIG.get(trig, str(trig)), TARGET.get(int(num(b, 'target', '0')), '?'),
                num(b, 'multiplier'), num(b, 'duration'), num(b, 'chance'), chance, cooldown, rng, raw))

out = ['# 원작 초월 스킬 수치 참고표 (2026-10-06, 구현담당1)', '',
       '초월 25종의 스킬을 효과 종류별로 모았다. **우리 값**은 SkillData 에셋(`Assets/Data/**/SkillData_*`)에서, **원작 값**은 `Tools/w3x/원본/war3map_new.w3a`를 `Tools/w3x/w3a.py`로 직접 디코드해 같은 줄에 붙였다(첫 레벨 값, 필드 ID 그대로). 조사 문서는 근거가 아니다.',
       '생성: `python3 -I Tools/transcend_skill_reference.py` (에셋이 바뀌면 다시 돌린다).', '',
       '열: 유닛 · 스킬 이름 · 트리거 · 대상 · multiplier(남는 속도 비율·%·고정값) · duration(초) · 효과 chance · 트리거 확률 · 쿨다운 · 범위 · 원작 w3a 필드.', '']
order = ['Slow', 'Stun', 'ArmorBreak', 'AttackPowerBuffPercent', 'AttackSpeedBuffPercent', 'AttackPowerBuffFlat', 'Damage', 'ExtraProjectile', 'ApplyBuff', 'HealOverTime', 'ArmorBonus']
for kind in order + [k for k in rows if k not in order]:
    rs = rows.get(kind)
    if not rs: continue
    out.append(f'## {kind} ({len(rs)}건)')
    out.append('')
    out.append('| 유닛 | 스킬 | 트리거 | 대상 | multiplier | duration | chance | 트리거확률 | 쿨 | 범위 | 원작 w3a |')
    out.append('|---|---|---|---|---:|---:|---:|---:|---:|---:|---|')
    for r in rs:
        out.append('| ' + ' | '.join(str(x).replace('|', '/').replace('\n', ' ') for x in r) + ' |')
    # 요약
    mults = sorted({float(r[4]) for r in rs if r[4] not in ('', None)})
    if mults: out.append(f'\n요약: multiplier 범위 {mults[0]:g} ~ {mults[-1]:g}, 서로 다른 값 {len(mults)}종.')
    out.append('')

# ── 종류별 요약(자동) ──
def fnum(x):
    try: return float(x)
    except: return None
slow = rows.get('Slow', [])
aura = [r for r in slow if r[2].startswith('오라')]
proc = [r for r in slow if not r[2].startswith('오라')]
out.append('## 요약: 이감(Slow)')
out.append('')
out.append(f'- 총 {len(slow)}건 = 상시 오라 {len(aura)}건(AOae 음수·양수) + 발동형 {len(proc)}건. **환산식: 우리 multiplier(남는 속도 비율) = 1 + Oae1**(예: Oae1 −0.30 → 0.70). 맵이 MinUnitSpeed 70으로 하한을 둔다.')
am = sorted({r[4] for r in aura}, key=lambda x: fnum(x) or 0)
out.append(f'- 오라 multiplier 값: {", ".join(am)} · 범위(w3a aare): ' + ', '.join(sorted({r[9] for r in aura}, key=lambda x: fnum(x) or 0)))
out.append('- 발동형(평타 확률 등): ' + ' / '.join(f"{r[0][3:]} {r[1][:18]} 확률 {float(r[7]):.3g} · 남는속도 {r[4]} · {r[5]}초" for r in proc if fnum(r[7]) is not None))
out.append('')
out.append('## 요약: 보스 상대 피해(보잡) 원작 근거')
out.append('')
out.append('- 원작에는 「보스에게 피해 +N%」 **전용 능력(패시브)이 없다**. 보스(PV=200) 분기는 **체력 비례 스킬 173호출·121트리거**에서만 나온다(BrookAttack 보스 75,000 고정 · Hidden1 보스 300,000+5,000,000 고정 등) — 일반몹은 %체력, 보스는 고정값/더 작은 %로 가는 분기이고 「보스가 더 아프다」가 아니다(근거: Docs/research/SKILL_BOSS_BRANCH.md 표를 j로 다시 확인).')
out.append('- 따라서 최상호 구일의 「보잡」은 **원작 근거 없음 — 제안값**: 보스(PV≥200) 상대 최종 피해 ×1.3.')
out.append('')
out.append('## 요약: 오라 강화·소환·아군 부여(원작 근거)')
out.append('')
out.append('- 「전설 이상 유닛 수 비례 오라 강화」: 원작 근거 없음(원작 오라는 고정 %: 공증 ACac 0.25(강재규 A0V8) · 불멸 김용태 0.6 · 정준영 −0.75(디버프)). 제안값: 기본 +20% + 전설 이상 1기당 +4%(상한 +40%).')
out.append('- 「전설 이상 아군에게 아머브레이크(발동) 부여」: 원작 근거 없음. 방깎 발동 값은 위 ArmorBreak 표(확률 4~14%, 방깎 2~9) 기준.')
out.append('- 소환수: 우리 코드엔 소환 효과가 없고 모리아 좀비 부활(raiseOnKill)과 TimedLife(지속)만 있다. 원작 초월 중 소환형은 로우 e0RR(MANA 135) 등 — 사장님 확정치(20초·동시 1기·쿨 10초·부채꼴)를 쓴다.')
out.append('')
open('Docs/research/TRANSCEND_SKILL_REFERENCE_2026-10-06.md', 'w', encoding='utf8').write('\n'.join(out) + '\n')
print({k: len(v) for k, v in rows.items()})
