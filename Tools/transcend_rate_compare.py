#!/usr/bin/python3
"""초월 스킬 발동 빈도·피해 수치를 원작(w3u·war3map.j 평타 트리거)과 나란히 놓는다 — 읽기 전용, 에셋을 안 고친다. 사장님 10-08 「초월이 약하다」 근거표(ⓗ).
출력: Docs/research/TRANSCEND_SKILL_RATE_2026-10-08.md
  §1 평타(공격력·공속) 원작 대응 유닛과 비교
  §2 게이지(마나/체력) 스킬: 문턱·평타당 충전·재생 → 발동 간격(초) 우리 vs 원작, 배율
  §3 확률형 스킬: 스킬 이름에 박힌 원작 확률(1/N · N%) vs 에셋 triggerChance
  §4 피해 수치: 이름에 박힌 원작 피해 숫자 vs 에셋 효과 값(공격력 비례 포함)
  §5 어긋난 것만(우리가 느리거나 약한 칸)
원작은 능력 w3a가 아니라 **평타 트리거**(HashAttack → Trig_<이름>_Actions, 호출되는 트리거·조건 함수까지)의 숫자를 읽는다(memory skill-damage-lives-in-triggers).
함정: 사장님 사양 스킬(에셋 이름 SkillData_사장님_*)의 문턱은 사장님이 정한 값이라 원작 같은 종류 값을 「참고」로만 적는다. 숫자가 없는 능력은 꼬리표일 수 있다(0이라고 죽은 게 아니다).
"""
import re,glob,os,sys,csv,collections,yaml
R='/Users/sang/GitHub/GuilRandomDefense'
sys.path.insert(0,R+'/Tools/w3x')
import w3u
U={u['id']:u for u in w3u.parse(R+'/Tools/w3x/원본/war3map_new.w3u')}
J=open(R+'/Tools/w3x/원본/war3map_new.j',encoding='utf-8',errors='replace').read()
HASH=collections.defaultdict(list)
for m in re.finditer(r"SaveTriggerHandle\(\w+,'(\w{4})',\d+,gg_trg_(\w+)\)",J): HASH[m.group(1)].append(m.group(2))
rmap=collections.defaultdict(list)
for r in csv.DictReader(open(R+'/Docs/reference/MASTER_UID_ROSTER_MAP.csv',encoding='utf-8')): rmap[r['로스터']].append(r['유닛ID'])
def body(fn):
    p=J.find('function %s takes'%fn)
    return J[p:J.index('endfunction',p)] if p>=0 else ''
def conds(t): return ''.join(body(m.group(0).split()[1]) for m in re.finditer(r'function Trig_%s_Func\w+C'%re.escape(t),J))
def trig_text(t):
    b=body('Trig_%s_Actions'%t); extra=conds(t)
    for callee in re.findall(r'TriggerExecute\(gg_trg_(\w+)\)',b):
        extra+=body('Trig_%s_Actions'%callee)+conds(callee)
    return b+extra
def ofacts(uid):
    mo=U[uid]['mods']
    txt=''.join(trig_text(t) for t in HASH.get(uid,[]))
    f=lambda k: float(mo[k]) if mo.get(k) not in (None,'') else None
    return dict(uid=uid,ua1b=f('ua1b'),ua1c=f('ua1c'),umpm=f('umpm'),umpr=f('umpr') or 0.0,uhpm=f('uhpm'),uhpr=f('uhpr') or 0.0,
        mana_eq=sorted({float(x) for x in re.findall(r'UNIT_STATE_MANA,GetAttacker\(\)\)(?:==|>=)([\d.]+)',txt)}),
        mana_gain=sorted({float(x) for x in re.findall(r'SetUnitManaBJ\(GetAttacker\(\),\(GetUnitStateSwap\(UNIT_STATE_MANA,GetAttacker\(\)\)\+([\d.]+)\)\)',txt)}),
        life_eq=sorted({float(x) for x in re.findall(r'UNIT_STATE_LIFE,GetAttacker\(\)\)(?:==|>=|>)([\d.]+)',txt)}),
        life_gain=sorted({float(x) for x in re.findall(r'SetUnitLifeBJ\(GetAttacker\(\),\(GetUnitStateSwap\(UNIT_STATE_LIFE,GetAttacker\(\)\)\+([\d.]+)\)\)',txt)}),
        triggers=HASH.get(uid,[]))
def load(p):
    return yaml.safe_load(open(p,encoding='utf-8').read().split('\n',3)[3])['MonoBehaviour']
guid={}
for m in glob.glob(R+'/Assets/Data/**/*.meta',recursive=True):
    g=re.search(r'^guid: (\w+)',open(m,encoding='utf-8').read(),re.M)
    if g: guid[g.group(1)]=m[:-5]
TRIG={0:'확률',1:'쿨',2:'오라',3:'N타째',4:'접근',5:'버튼'}
def nums_in(name):
    out=[]
    for m in re.finditer(r'(\d[\d,]*\.?\d*)',name):
        s=m.group(1).replace(',','')
        try: out.append(float(s))
        except: pass
    return out
def skills_of(u):
    gs=[]
    for k in ('skill',):
        v=u.get(k)
        if isinstance(v,dict) and v.get('guid') in guid: gs.append(v['guid'])
    for v in u.get('skills') or []:
        if isinstance(v,dict) and v.get('guid') in guid: gs.append(v['guid'])
    seen=[]; 
    for g in gs:
        if g not in seen: seen.append(g)
    return [(g,load(guid[g]),os.path.basename(guid[g])) for g in seen]
def dmg_ours(lv,atk):
    best=0.0
    for e in lv.get('effects') or []:
        if e.get('kind')!=0: continue
        b=e.get('basis',0); mult=float(e.get('multiplier',0)); bonus=float(e.get('bonus',0))
        v=mult if b==0 else (mult*atk+bonus if b==3 else None)
        if v: best=max(best,abs(v))
    return best
units=sorted(glob.glob(R+'/Assets/Data/Units/Roster/초월_*.asset'))
sec6=[]
md=[]; sec1=[]; sec2=[]; sec3=[]; sec4=[]; bad=[]
for p in units:
    ro=os.path.basename(p)[:-6]; u=load(p)
    AS=float(u['attackSpeed']); atk=float(u['attackPower'])
    uids=[x for x in rmap.get(ro,[]) if x in U and not x.startswith('h04') and x not in ('h09J','h0AG')]
    of=[ofacts(x) for x in uids]
    # §1 평타
    for f in of:
        if f['ua1c']:
            asr=1/f['ua1c']
            sec1.append((ro,f['uid'],f['ua1b'],atk,asr,AS))
    mana_orig=[f for f in of if f['mana_eq']]; life_orig=[f for f in of if f['life_eq']]
    ours_list=[]
    for g0,sk0,fn0 in skills_of(u):
        l0=(sk0.get('levels') or [None])[0]
        if not l0: continue
        t0=sk0.get('triggerType')
        if t0==0: ours_list.append('1/%.0f'%(1/max(1e-9,float(l0.get('triggerChance',1)))) if float(l0.get('triggerChance',1))<1 else '100%')
        elif t0==3: ours_list.append(('M' if int(l0.get('gaugeKind') or 0)==0 else 'L')+str(int(l0.get('hitCountThreshold') or l0.get('hitCountFloor') or 0)))
        elif t0==2: ours_list.append('오라')
        elif t0==1: ours_list.append('쿨%.0f'%float(l0.get('cooldown') or 0))
        elif t0==5: ours_list.append('버튼')
        elif t0==4: ours_list.append('접근')
    orig_gates=[]
    for f in of:
        txt=''.join(trig_text(t) for t in f['triggers'])
        rn=sorted({int(x) for x in re.findall(r'GetRandomInt\(1,(\d+)\)',txt) if int(x)<=200})
        orig_gates.append((f['uid'],rn,f['mana_eq'],f['life_eq']))
    sec6.append((ro,len(ours_list),ours_list,orig_gates))
    for g,sk,fn in skills_of(u):
        lvs=sk.get('levels') or []
        if not lvs: continue
        lv=lvs[0]; name=str(sk.get('skillName','')).replace('\n',' '); trig=TRIG.get(sk.get('triggerType'),'?')
        custom='사장님' in fn
        # §2 게이지
        if sk.get('triggerType')==3:
            T=int(lv.get('hitCountThreshold') or 0) or int(lv.get('hitCountFloor') or 0)
            kind='마나' if int(lv.get('gaugeKind') or 0)==0 else '체력'
            if kind=='마나':
                pm=float(u.get('manaGaugePerMana') or 0); regen=float(u.get('manaRegenPerSecond') or 0)*(pm or 1)
                ours=T/(1.0*AS+regen) if AS+regen>0 else float('inf')
                cand=[f for f in mana_orig]
                ref=cand[0] if cand else None
                N=None
                mm=re.search(r'MANA\s*게이지\s*(\d+)',name) or re.search(r'MANA\s*(\d+)',name)
                if mm: N=float(mm.group(1))
                elif ref: N=ref['mana_eq'][0]
                if ref and N:
                    x=(ref['mana_gain'] or [1.0])[0]; asr=1/ref['ua1c']; reg=ref['umpr']
                    orig=N/(x*asr+reg)
                else: orig=None; x=None; N=N
                sec2.append((ro,name[:42],kind,custom,T,1.0,regen,ours,N,x,(ref['umpr'] if ref else None),(1/ref['ua1c'] if ref else None),orig,(ref['uid'] if ref else '-')))
            else:
                hg=1.0
                if u.get('lifeGaugeCustomHitGain'): hg=float(u.get('lifeGaugeHitGain') or 0) or 1e-9
                regen=float(u.get('lifeGaugeRegenPerSecond') or 0)
                ours=T/(hg*AS+regen)
                ref=life_orig[0] if life_orig else None
                N=ref['life_eq'][0] if ref else None
                mm=re.search(r'LIFE\s*게이지\s*(\d+)',name) or re.search(r'체력\s*게이지\s*(\d+)',name)
                if mm and not custom: N=float(mm.group(1))
                if ref and N:
                    x=(ref['life_gain'] or [1.0])[0]; asr=1/ref['ua1c']; reg=ref['uhpr']
                    orig=N/(x*asr+reg)
                else: orig=None; x=None
                sec2.append((ro,name[:42],kind,custom,T,hg,regen,ours,N,x,(ref['uhpr'] if ref else None),(1/ref['ua1c'] if ref else None),orig,(ref['uid'] if ref else '-')))
        # §3 확률
        if sk.get('triggerType')==0 and not custom:
            m=re.search(r'(?<![\d.])1/(\d+)',name) 
            m2=re.search(r'(\d+(?:\.\d+)?)\s*%',name)
            exp=None
            if m: exp=1.0/float(m.group(1))
            elif m2: exp=float(m2.group(1))/100
            if exp is not None:
                ch=float(lv.get('triggerChance',1))
                sec3.append((ro,name[:50],exp,ch,ch/exp if exp else 0))
        # §4 피해
        if not custom:
            d=dmg_ours(lv,atk)
            big=[n for n in nums_in(name) if n>=1000]
            if d>0 and big:
                best=min(big,key=lambda n:abs(n-d)); sec4.append((ro,name[:46],d,max(big),d/max(big)))
def fmt(x,n=1): return '-' if x is None else ('∞' if x==float('inf') else f'{x:.{n}f}')
md.append('# 초월 스킬 발동 빈도·피해: 우리 vs 원작 (2026-10-08, 구현담당1)\n')
md.append(__doc__.split('출력:')[0].strip().replace('\n','\n')+'\n')
md.append('생성: `/usr/bin/python3 Tools/transcend_rate_compare.py` · 고친 것 없음 — 근거·차이 배율·제안만.\n')
md.append('''## 요약(구현담당1 판독 — 표 아래가 근거)

1. **평타는 원작과 같다.** 대응 uid가 하나인 유닛은 공격력·공속 배율 1.000(25종 중 20종). 어긋나는 것은 ⓐ 원작 uid 여럿이 한 로스터로 합쳐진 4종(강주혁·구주호·황준석·최상호_AP: 공격력이 uid 중 하나가 아니라 섞인 값) ⓑ 두유찬 공속 3.000(원작 1.266, 2.37배 — 우리가 더 빠름, 상한/특성 영향 확인 필요).
2. **게이지 발동 간격은 대체로 원작과 같다(배율 1.00).** 문턱이 사장님 사양(✎)이어도 원작 같은 종류 문턱과 같은 칸이 많다(김민준 135·배성령 115·이태훈 140·강주혁 145·조성진 140). 느린 칸은 **강재규 체력 40(×1.36: 원작은 uhpr 0.5 재생이 있다 — 우리 lifeGaugeRegenPerSecond 0)** 하나뿐. 빠른 칸: 양재모(50 vs 원작 250)·최상호_AP 마나(125 vs 150).
3. **확률·피해 숫자**는 이름에 박힌 원작 값과 에셋이 일치한다(엔마 623,522 vs 이름 200,000은 원작 Hbh2×Hbh3 등 이름에 안 적힌 항이 합산된 값으로 보이나 이 표는 거기까지 검증 못 했다 — §4 ⚠️, 원작 트리거 확인 필요).
4. **「약하다」의 후보 원인은 발동 문(스킬 수)이다(§5-B).** 원작 평타 트리거의 난수·게이지 문이 우리 스킬로 다 안 옮겨진 유닛이 14종 — 김만경·황준석·유재헌·김경현은 우리 스킬이 오라·버튼뿐이라 **평타에 붙는 피해 문이 0**(원작은 3~4개). 구주호는 uid 3개 합본이라 원작 문 8개 중 3개만 남았다. 사장님 10-06 재사양으로 구성이 바뀐 유닛은 의도된 차이일 수 있어 **고치지 않고** 사장님 판단 재료로 둔다.
5. 제안(승인 전 적용 안 함): ⓐ 강재규 체력 게이지 재생 0.5 반영(`lifeGaugeRegenPerSecond`) ⓑ 문 0인 4종은 사장님께 「피해 스킬을 더할지」 질문 ⓒ 구주호 합본 uid 3개 중 어느 스킬 묶음을 쓸지 확인 ⓓ 두유찬 공속 3.0 출처 확인.
''')
md.append('## 1. 평타 (공격력 · 공속) — 원작 대응 유닛(uid)과\n')
md.append('| 유닛 | 원작 uid | 원작 ua1b | 우리 공격력 | 원작 공속 1/ua1c | 우리 공속 | 공격력 배율 | 공속 배율 |'); md.append('|---|---|---:|---:|---:|---:|---:|---:|')
for ro,uid,ub,atk,asr,AS in sec1:
    md.append(f'| {ro} | {uid} | {ub:,.0f} | {atk:,.1f} | {asr:.3f} | {AS:.3f} | {atk/ub:.3f} | {AS/asr:.3f} |')
md.append('\n## 2. 게이지(마나·체력) 스킬 — 발동 간격(초) = 문턱 ÷ (평타당 충전 × 공속 + 재생)\n')
md.append('우리: 카운터는 평타 1타에 +1(체력형은 lifeGaugeHitGain 예외), 마나 재생 = manaRegenPerSecond×게이지/마나. 원작: 평타 트리거의 `마나 +x` · uhpr/umpr. **원작 N**: 스킬 이름에 박힌 「MANA게이지N」(없으면 같은 유닛 원작 평타 트리거의 `MANA==N`). 사장님 사양(✎)은 문턱이 사장님 값이라 원작은 참고.\n')
md.append('| 유닛 | 스킬 | 종류 | 우리 문턱 | 충전/타 | 우리 간격(초) | 원작 N | 원작 x/타 | 원작 간격(초) | 우리÷원작 | 비고 |'); md.append('|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|')
for ro,name,kind,custom,T,hg,regen,ours,N,x,reg,asr,orig,uid in sec2:
    ratio=(ours/orig) if (orig and ours!=float('inf')) else None
    note=('✎사장님 사양 ' if custom else '')+(f'원작 {uid}' if uid!='-' else '원작 대응 없음')
    if ratio and ratio>1.05: bad.append(('게이지',ro,name,f'우리 {ours:.1f}초 vs 원작 {orig:.1f}초',ratio,custom))
    md.append(f'| {ro} | {name} | {kind} | {T} | {hg:.2f} | {fmt(ours)} | {fmt(N,0)} | {fmt(x,2)} | {fmt(orig)} | {fmt(ratio,2)} | {note} |')
md.append('\n## 3. 확률형 — 스킬 이름에 박힌 원작 확률 vs 에셋 triggerChance (사장님 사양 제외)\n')
md.append('| 유닛 | 스킬 | 이름의 확률 | 에셋 확률 | 에셋÷이름 |'); md.append('|---|---|---:|---:|---:|')
for ro,name,exp,ch,r in sec3:
    flag='' if 0.95<=r<=1.05 else ' ⚠️'
    if not(0.95<=r<=1.05): bad.append(('확률',ro,name,f'이름 {exp:.4f} vs 에셋 {ch:.4f}',r,False))
    md.append(f'| {ro} | {name} | {exp:.4f} | {ch:.4f} | {r:.2f}{flag} |')
md.append('\n## 4. 피해 수치 — 스킬 이름에 박힌 원작 피해(가장 큰 숫자) vs 에셋 효과 값(Flat multiplier · 공격력비례 multiplier×공격력+bonus의 최댓값, 사장님 사양 제외)\n')
md.append('이름의 큰 숫자는 피해뿐 아니라 범위·% 등일 수 있어 **거친 필터**다 — 배율이 1에서 크게 벗어난 것만 보고 §5에서 사람이 원작 트리거로 확인한다.\n')
md.append('| 유닛 | 스킬 | 우리 값 | 이름의 최대 숫자 | 우리÷이름 |'); md.append('|---|---|---:|---:|---:|')
for ro,name,d,big,r in sec4:
    flag='' if 0.9<=r<=1.1 else ' ⚠️'
    if not(0.9<=r<=1.1) and r<0.9: bad.append(('피해',ro,name,f'우리 {d:,.0f} vs 이름 {big:,.0f}',r,False))
    md.append(f'| {ro} | {name} | {d:,.0f} | {big:,.0f} | {r:.2f}{flag} |')
md.append('\n## 6. 스킬 구성(발동 문 개수) — 원작 평타 트리거의 게이트 vs 우리 스킬(트리거별 문턱·확률)\n')
md.append('원작: `1/N` 난수 게이트(N≤200)·`MANA==N`·`LIFE==N`. 우리: 확률 1/N · M=마나 문턱 · L=체력 문턱. 개수·크기를 눈으로 비교 — 원작 게이트가 우리 스킬로 다 옮겨졌는지(문 하나가 사라지면 그만큼 약하다).\n')
md.append('| 유닛 | 우리 스킬 수 | 우리 스킬(트리거) | 원작 uid · 난수 게이트 · 마나/체력 문턱 |'); md.append('|---|---:|---|---|')
for ro,n,ol,og in sec6:
    md.append(f'| {ro} | {n} | {", ".join(ol)} | ' + ' ; '.join(f'{uid}: 1/{"·1/".join(map(str,rn)) if rn else "-"} · M{",".join("%d"%m for m in me) or "-"} · L{",".join("%d"%l for l in le) or "-"}' for uid,rn,me,le in og) + ' |')
md.append('\n## 5. 어긋난 것만 (우리가 느리거나 약한 칸)\n')
md.append('### 5-A 빈도·수치(§2~§4)\n')
md.append('| 구분 | 유닛 | 스킬 | 비교 | 우리÷원작 | 사장님 사양 |'); md.append('|---|---|---|---|---:|---|')
for k,ro,name,cmp,r,custom in sorted(bad,key=lambda b:(b[0],-b[4])):
    md.append(f'| {k} | {ro} | {name} | {cmp} | {r:.2f} | {"✎" if custom else ""} |')
md.append('\n### 5-B 발동 문(게이트) 개수 — 원작보다 적은 유닛\n')
md.append('원작 문 = 평타 트리거의 난수 게이트 1/N(2≤N≤50) 종류 수 + 마나 문턱 종류 + 체력 문턱 종류(여러 uid가 합쳐진 유닛은 합집합). 우리 문 = 확률 스킬(트리거 확률<1) + 게이지 스킬 수. **거친 근사**(원작 난수 중엔 스킬이 아닌 것도 있다) — 차이가 큰 유닛만 원작 트리거를 사람이 열어 확인할 것. 사장님 재사양(10-06)으로 스킬 구성이 바뀐 유닛은 의도된 차이일 수 있다.\n')
md.append('| 유닛 | 원작 문 | 우리 문 | 차이 | 우리 스킬(트리거) | 원작 게이트 |'); md.append('|---|---:|---:|---:|---|---|')
rows5b=[]
for ro,n,ol,og in sec6:
    rn=set(); me=set(); le=set()
    for uid,r,m,l in og:
        rn|={x for x in r if 2<=x<=50}; me|=set(m); le|=set(l)
    og_n=len(rn)+len(me)+len(le)
    our_n=sum(1 for x in ol if x.startswith('1/') or x[0] in 'ML' and x[1:].isdigit())
    if og: rows5b.append((og_n-our_n,ro,og_n,our_n,ol,sorted(rn),sorted(me),sorted(le)))
for d,ro,a,b,ol,rn,me,le in sorted(rows5b,reverse=True):
    if d>0: md.append(f'| {ro} | {a} | {b} | **−{d}** | {", ".join(ol)} | 1/{"·1/".join(map(str,rn)) or "-"} M{",".join("%d"%x for x in me) or "-"} L{",".join("%d"%x for x in le) or "-"} |')
open(R+'/Docs/research/TRANSCEND_SKILL_RATE_2026-10-08.md','w',encoding='utf-8').write('\n'.join(md)+'\n')
print(len(sec1),len(sec2),len(sec3),len(sec4),len(bad))
