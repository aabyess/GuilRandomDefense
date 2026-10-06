#!/usr/bin/python3
"""초월 25종 합계 DPS 표(평타 + 스킬 기대값, 방어 반영) — 에셋만 읽는다(에디터 안 씀). 사장님 10-07 「원작 재이식 뒤 신 난이도 합계 DPS」.
모델(근사 — 틀릴 수 있는 곳은 결과 표 머리글에 적는다):
  표적: 신 난이도 R60 보스(체력 179.8M·방어 317.6) · R50 보스(87.5M·265.9) · R45 몹(4.9M·48.2) — Docs/research/TRANSCEND_GOD_BALANCE_AUDIT_2026-10-06.md §1·방어 절
  방어 계수 1/(1+0.02×방어): AD 피해(평타·AD 스킬)만 탄다, AP·방어 무시(armorIgnoreRatio)·유닛 attackArmorIgnoreRatio 반영
  발동/초: OnHitChance = 공속×확률 · OnHitCount = 공속÷문턱 · CooldownAutoCast = 1÷쿨 · ActiveButton = 1÷쿨(계속 누른다고 가정, 쿨 0이면 제외)
  피해: Damage 효과만 — Flat(multiplier) · CasterAttackPower(multiplier×공격력+bonus) · 최대체력%·현재체력%(표적이 평균 50%라 가정)·잃은체력%(50% 가정) · hitCount·난수 평균 반영
  단일 표적 기준(범위 효과도 1기). 스턴·방깍·오라·버프·소환·DoT·체력 게이지/액티브 연출 등 피해가 아닌 것은 제외 → 지원형은 낮게 나온다.
"""
import re,glob,os,sys,yaml,statistics
R='/Users/sang/GitHub/GuilRandomDefense'
TRIG={0:'OnHitChance',1:'CooldownAutoCast',2:'Aura',3:'OnHitCount',4:'OnEnemyEnterRange',5:'ActiveButton'}
BASIS={0:'Flat',1:'MaxHp',2:'CurHp',3:'Atk',13:'MissHp'}
TARGETS={'R60보스':(179.8e6,317.6),'R50보스':(87.5e6,265.9),'R45몹':(4.9e6,48.2)}
def load(p):
    t=open(p,encoding='utf-8').read().split('\n',3)[3]
    return yaml.safe_load(t)['MonoBehaviour']
guid={}
for m in glob.glob(R+'/Assets/Data/**/*.meta',recursive=True):
    g=re.search(r'^guid: (\w+)',open(m,encoding='utf-8').read(),re.M)
    if g: guid[g.group(1)]=m[:-5]
def armor_factor(a,ign=0.0): return 1.0/(1.0+0.02*max(0.0,a*(1.0-ign)))
def skill_dps(sk,unit,aps,atk,hp,armor,uign):
    lv=(sk.get('levels') or [None])[0]
    if not lv: return 0.0,''
    trig=TRIG.get(sk.get('triggerType'),'?')
    if trig=='OnHitChance': rate=aps*float(lv.get('triggerChance',1))
    elif trig=='OnHitCount': rate=aps/max(1,int(lv.get('hitCountThreshold') or 1))
    elif trig in ('CooldownAutoCast','ActiveButton'):
        cd=float(lv.get('cooldown') or 0); rate=(1.0/cd) if cd>=2.0 else 0.0   # 쿨 2초 미만 연타 버튼(폭탄제조=목재가 한계)은 제외
    else: return 0.0,''
    total=0.0
    for e in lv.get('effects') or []:
        if e.get('kind')==16 and e.get('summonUnits'):   # SummonUnit: 소환수 평타 DPS × 수명(쿨 안에서 차지하는 비율)
            life=float(e.get('summonLifetime') or 0); cdv=float(lv.get('cooldown') or 0)
            frac=min(1.0,life/cdv) if cdv>0 else 1.0
            for su in e['summonUnits']:
                g=su.get('guid') if isinstance(su,dict) else None
                if g in guid:
                    d=load(guid[g]); total+=frac*float(d['attackPower'])*float(d['attackSpeed'])*armor_factor(armor,0.0)/max(rate,1e-9)*rate if rate>0 else 0.0
            continue
        if e.get('kind')!=0: continue
        b=e.get('basis',0); mult=float(e.get('multiplier',0)); bonus=float(e.get('bonus',0))
        if b==0: dmg=mult
        elif b==3: dmg=mult*atk+bonus
        elif b==1: dmg=mult*hp
        elif b==2: dmg=mult*hp*0.5
        elif b==13: dmg=mult*hp*0.5
        else: continue
        rmin=float(e.get('randMin',1) or 1); rmax=float(e.get('randMax',1) or 1)
        if rmin==0 and rmax==0: rmin=rmax=1
        dmg*=(rmin+rmax)/2*max(1,int(e.get('hitCount',1) or 1))*float(e.get('chance',1) or 1)
        dt=e.get('damageType',0)
        ign=float(e.get('armorIgnoreRatio',0) or 0)
        if dt==1: dmg*=ign+(1.0-ign)*armor_factor(armor,uign)   # 효과 방어 무시 비율은 피해를 둘로 갈라 한쪽만 무시
        total+=dmg
    return rate*total,trig
rows=[]
for p in sorted(glob.glob(R+'/Assets/Data/Units/Roster/초월_*.asset')):
    u=load(p); name=os.path.basename(p)[:-6]
    if '위습' in name: continue
    ap=float(u['attackPower']); sp=float(u['attackSpeed']); uign=float(u.get('attackArmorIgnoreRatio',0) or 0)
    skills=[]
    for s in u.get('skills') or []:
        g=s.get('guid') if isinstance(s,dict) else None
        if g in guid: skills.append(load(guid[g]))
    res={'unit':name,'ap':ap,'sp':sp}
    for tn,(hp,arm) in TARGETS.items():
        basic=ap*sp*armor_factor(arm,uign)
        sk=0.0; parts=[]
        for s in skills:
            d,tr=skill_dps(s,u,sp,ap,hp,arm,uign)
            if d>0: sk+=d; parts.append((s.get('skillName','?').split('—')[0].strip(),d))
        res[tn]=(basic,sk,parts)
    rows.append(res)
for tn in ('R60보스','R45몹'):
    tot=sorted((r[tn][0]+r[tn][1] for r in rows)); q25=tot[len(tot)//4]
    print(f'## {tn} 기준 (체력 {TARGETS[tn][0]/1e6:.1f}M · 방어 {TARGETS[tn][1]}) — 하위 25% 선 = 합계 {q25:,.0f}/초 · 중앙 {statistics.median(tot):,.0f}')
    print('| 순위 | 유닛 | 공격력 | 공속 | 평타 DPS | 스킬 DPS(기대) | 합계 DPS | 주요 스킬(DPS) | 하위25% |')
    print('|---:|---|---:|---:|---:|---:|---:|---|---|')
    for i,r in enumerate(sorted(rows,key=lambda r:-(r[tn][0]+r[tn][1])),1):
        b,s,parts=r[tn]; tot_=b+s
        top=', '.join(f'{n} {d:,.0f}' for n,d in sorted(parts,key=lambda x:-x[1])[:3])
        print(f"| {i} | {r['unit']} | {r['ap']:,.0f} | {r['sp']:.3f} | {b:,.0f} | {s:,.0f} | {tot_:,.0f} | {top or '—(피해 스킬 없음/오라형)'} | {'**하위 25%**' if tot_<=q25 else ''} |")
    print()
