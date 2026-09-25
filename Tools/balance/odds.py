# 승산 재계산 모형 (구현담당1, 2026-09-25). 파일 값만 쓴다.
# 여유 = 공급 DPS × 가동률 ÷ 필요 DPS(라운드 적 HP 합 ÷ 라운드 길이). 09-24와 같은 정의.
# 공급: 시작 랜덤유닛 5 + 라운드마다 랜덤유닛 2(흔함 9종, 8회 천장) + 보스 라운드 안흔함 위습 1.
# 조합: 지정 유닛 레시피(에셋 참조 일치) — 탐욕: 결과 DPS가 재료 DPS 합보다 크면 한다, 높은 등급 먼저.
# 빠진 것(→ 하한): 스킬·특성·강화·골드/도박·아이템·스토리 보상·0.24% 해적선.
# 쓰는 법: python3 Tools/balance/odds.py [판 수=1000] [라운드=15] [흔함 가동률=0.90] [조합 결과 가동률=0.90] [보스 가동률=0.22]
#   조합 결과는 레인 가운데에 생긴다 — 옮기지 않으면 가동률이 0~25%다(09-25 i1_07). 보스 22%는 09-24 실측.
# 결과 해석: 09-25 보고 ~/.claude 기억 odds-2026-09-25. 모형 자체는 실측으로 검증된 적이 없다 — 판 결과와 견줄 것.
import os, re, glob, random, statistics, sys
R = '/Users/sang/GitHub/GuilRandomDefense/'
def g(p): return re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1)
def num(t, k, d=0.0):
    m = re.search(r'\n  ' + k + r': ([-\d.e]+)', t); return float(m.group(1)) if m else d

units = {}
for p in glob.glob(R + 'Assets/Data/Units/Roster/*.asset'):
    t = open(p).read()
    units[g(p)] = dict(name=os.path.basename(p)[:-6], grade=int(num(t, 'grade')), atk=num(t, 'attackPower'),
                       aps=num(t, 'attackSpeed'), at=int(num(t, 'attackType')), sys=int(num(t, 'isSystemUnit')))
enemies = {}
for p in glob.glob(R + 'Assets/Data/Enemies/*.asset'):
    t = open(p).read()
    enemies[g(p)] = dict(name=os.path.basename(p)[:-6], hp=num(t, 'hp'), armor=num(t, 'armor'),
                         arm=int(num(t, 'armorType')), boss=int(num(t, 'isBoss')))
dt = open(R + 'Assets/Data/DamageTable.asset').read()
rows = {}
for name in ['normal', 'pierce', 'siege', 'hero', 'chaos', 'magic', 'spells']:
    m = re.search(r'\n  ' + name + r':\n    vsLarge: ([\d.]+)\n    vsFort: ([\d.]+)\n    vsNormal: ([\d.]+)\n    vsHero: ([\d.]+)', dt)
    rows[name] = dict(zip(['large', 'fort', 'normal', 'hero'], map(float, m.groups())))
AT = {1: 'normal', 2: 'pierce', 3: 'siege', 4: 'hero', 5: 'chaos', 6: 'magic', 7: 'spells'}
ARM = {1: 'normal', 2: 'large', 3: 'fort', 4: 'hero'}
def mult(at, arm): return 1.0 if at not in AT or arm not in ARM else rows[AT[at]][ARM[arm]]
def armor_mult(a): return 1 - 0.02 * a / (1 + 0.02 * a) if a >= 0 else 2 - (0.98) ** (-a)

recipes = []
for p in glob.glob(R + 'Assets/Data/Recipes/**/*.asset', recursive=True):
    t = open(p).read()
    res = re.search(r'\n  result: \{fileID: \d+, guid: (\w+)', t)
    if not res or res.group(1) not in units: continue
    need, wild, ok = {}, {}, True
    for k, fid, gu, wg, c in re.findall(r'- kind: (\d)\n\s+unit: \{fileID: (\d+)(?:, guid: (\w+))?[^\n]*\n\s+item:[^\n]*\n\s+wildcardGrade: (\d+)\n\s+count: (\d+)', t):
        if k == '0':
            if gu not in units: ok = False
            need[gu] = need.get(gu, 0) + int(c)
        elif k == '2': wild[int(wg)] = wild.get(int(wg), 0) + int(c)
        else: ok = False   # 아이템 재료는 이 모형 밖
    minr, maxr = int(num(t, 'minRound')), int(num(t, 'maxRound'))
    if ok and int(num(t, 'requiredSaveCount')) == 0 and int(num(t, 'goldCost')) == 0 and 'resourceCosts:\n  - ' not in t:
        recipes.append(dict(res=res.group(1), need=need, wild=wild, minr=minr, maxr=maxr))

waves = {}
for p in glob.glob(R + 'Assets/Data/Waves/Wave_Round*.asset'):
    t = open(p).read(); r = int(num(t, 'roundNumber'))
    spawns = [(enemies[a], int(c)) for a, c in re.findall(r'enemyData: \{fileID: \d+, guid: (\w+)[^\n]*\n\s+count: (\d+)', t)]
    wr = re.findall(r'- wisp: \{fileID: \d+, guid: (\w+)[^\n]*\n\s+count: (\d+)', t)
    waves[r] = dict(spawns=spawns, boss=any(e['boss'] for e, _ in spawns), reward=wr)

gacha = open(R + 'Assets/Data/MainGachaTable.asset').read()
def pool(grade):
    m = re.search(r'- grade: ' + str(grade) + r'\n(.*?)(?=\n  - grade:|\Z)', gacha, re.S)
    return [x for x in re.findall(r'guid: (\w+)', m.group(1)) if x in units] if m else []
COMMON, UNCOMMON = pool(0), pool(1)
if not COMMON: COMMON = [k for k, u in units.items() if u['grade'] == 0 and not u['sys'] and u['name'].startswith('흔함_')]

def dps(u, e): return u['atk'] * u['aps'] * mult(u['at'], e['arm']) * armor_mult(e['armor'])
def dps_round(u, r):
    sp = waves[r]['spawns']; hp = sum(e['hp'] * c for e, c in sp)
    return sum(dps(u, e) * e['hp'] * c for e, c in sp) / hp   # HP 가중 평균

TIER = {0: 0, 1: 1, 2: 2, 3: 3, 12: 4, 4: 5, 5: 5, 10: 5, 11: 5, 6: 6, 7: 7, 8: 8, 9: 9}

def run(rounds, seed, uptime=0.90, boss_uptime=0.22, combined_uptime=None):
    if combined_uptime is None: combined_uptime = uptime
    rng = random.Random(seed); inv = {}; pity = [0, [0] * len(COMMON)]
    def add(k): inv[k] = inv.get(k, 0) + 1
    def roll_common():
        if pity[0] >= 7:
            i = min(range(len(COMMON)), key=lambda j: pity[1][j]); pity[0] = 0
        else:
            i = rng.randrange(len(COMMON)); pity[0] += 1
        pity[1][i] += 1; add(COMMON[i])
    for _ in range(5): roll_common()
    out = []
    for r in range(1, rounds + 1):
        # 조합 — 탐욕
        while True:
            best = None
            for rc in recipes:
                if rc['minr'] and r < rc['minr'] or rc['maxr'] and r > rc['maxr']: continue
                if any(inv.get(k, 0) < c for k, c in rc['need'].items()): continue
                left = dict(inv); [left.__setitem__(k, left[k] - c) for k, c in rc['need'].items()]
                wild_pick, fine = [], True
                for wg, c in rc['wild'].items():
                    cand = sorted([k for k, n in left.items() if n > 0 and units[k]['grade'] == wg for _ in range(n)], key=lambda k: dps_round(units[k], r))
                    if len(cand) < c: fine = False; break
                    for k in cand[:c]: left[k] -= 1; wild_pick.append(k)
                if not fine: continue
                used = [k for k, c in rc['need'].items() for _ in range(c)] + wild_pick
                gain = dps_round(units[rc['res']], r) - sum(dps_round(units[k], r) for k in used)
                key = (TIER.get(units[rc['res']]['grade'], 0), gain)
                if gain > 0 and (best is None or key > best[0]): best = (key, rc, used)
            if best is None: break
            for k in best[2]: inv[k] -= 1
            add(best[1]['res'])
        w = waves[r]; T = 75.4 if w['boss'] else (40.65 if r == 1 else 40.67)
        hp = sum(e['hp'] * c for e, c in w['spawns'])
        if w['boss']:
            supply = boss_uptime * sum(n * dps_round(units[k], r) for k, n in inv.items() if n > 0 and not units[k]['sys'])
        else:
            supply = sum(n * dps_round(units[k], r) * (uptime if units[k]['grade'] == 0 else combined_uptime)
                         for k, n in inv.items() if n > 0 and not units[k]['sys'])
        up = 1.0
        grades = {}
        for k, n in inv.items():
            if n > 0: grades[units[k]['grade']] = grades.get(units[k]['grade'], 0) + n
        out.append((supply * up / (hp / T), supply, hp / T, grades))
        # 라운드 넘어갈 때: 랜덤유닛 2 + 보스 보상
        for _ in range(2): roll_common()
        for wg, c in w['reward']:
            for _ in range(int(c)): add(rng.choice(UNCOMMON))
    return out

if __name__ == '__main__':
    N = int(sys.argv[1]) if len(sys.argv) > 1 else 1000; RN = int(sys.argv[2]) if len(sys.argv) > 2 else 15
    print(f'레시피 {len(recipes)}개(골드·자원·세이브·아이템 조건 없는 것) · 흔함 풀 {len(COMMON)} · 안흔함 풀 {len(UNCOMMON)}')
    a = [float(x) for x in sys.argv[3:6]] + [None] * 3
    uc, ub, bu = a[0] if a[0] is not None else 0.90, a[1], a[2] if a[2] is not None else 0.22
    print(f'가동률: 흔함 {uc} · 조합 결과 {ub if ub is not None else uc} · 보스 {bu}')
    runs = [run(RN, s, uptime=uc, boss_uptime=bu, combined_uptime=ub) for s in range(N)]
    GN = {0: '흔', 1: '안', 2: '특', 3: '희', 12: '수', 4: '히', 5: '전', 6: '제', 7: '초'}
    for i in range(RN):
        m = sorted(x[i][0] for x in runs); w = waves[i + 1]
        first = [x for x in runs[0][i][3].items()]
        avg = {}
        for x in runs:
            for gr, n in x[i][3].items(): avg[gr] = avg.get(gr, 0) + n / N
        print(f"R{i+1:2d}{'★' if w['boss'] else ' '} 여유 p10 {m[N//10]:.2f} · 중앙 {m[N//2]:.2f} · p90 {m[9*N//10]:.2f} · <1 {sum(v<1 for v in m)/N:4.0%} · 필요 {runs[0][i][2]:7.0f} · 평균 유닛 " +
              ' '.join(f"{GN.get(gr, gr)}{n:.1f}" for gr, n in sorted(avg.items(), key=lambda x: TIER.get(x[0], 0))))
