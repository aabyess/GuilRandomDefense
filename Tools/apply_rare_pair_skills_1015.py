"""희귀함 임시 스킬 9기 중 마젤란·와이퍼·슈가·센토마루 실제 구현(사장님 10-15 확정 짝) — 구현담당1.
짝: 김경현=마젤란 H01Z · 고어진=와이퍼 H024 · 강재규=슈가 H027 · 구주호=센토마루 H028.
근거(w3a/w3u/j 직접 디코드): 마젤란 A04T(Aspo Spo1 2500/초·adur 4) + Trig_Unique16 1/10 → e058 A09V(Apxf pxf1 1250·쿨 0.09·aare 1000·수명≈3초)
  와이퍼 Trig_Unique7 1/8 → e03K ACfl 오더 7000(A0D9 툴팁: 12.5%·최대 9명·600범위·마법) · 슈가 Trig_Unique13 1/12 → h06O selfdestruct → e0AW A0PK(450범위, 툴팁 A0IY 10000 물리) + A0IW 마나재생 aura 0.6·700
  센토마루 h028 ua1w msplash·ua1f 260(평타 스플래시) — A06B는 Hbh 값이 0(툴팁 껍데기, 트리거 없음)이라 스킬 없음.
"""
import sys,re,os
sys.path.insert(0,'Tools'); import skill_asset_tool as sat
E=sat.effect
def level(**kw): return sat.level_block(**kw)
made={}
def mk(stem,name,desc,trig,lv): 
    a=sat.new_asset(stem,name,desc,trig,[lv]); made[stem]=sat.guid_of(a); return made[stem]
SPELLS=7
g_pois=mk('원작능력_희귀함_김경현_A04T','A04T &독독열매 — 평타 독 초당 2500·4초',
 '원작 마젤란 h01Z uabi A04T(Aspo, Spo1 2500/초 · Spo2 0 · adur 4초 · ahdu 2). 평타가 맞을 때마다 대상에 4초 독(초당 2500, 같은 대상에 다시 걸면 끝 시각만 늘어남, A08M과 같은 구현). 2026-10-15 구현담당1.',
 0,level(triggerChance=1.0,effects=[E(kind=26,target=3,damageType=2,attackType=SPELLS,multiplier=2500,duration=4)]))
g_hyd=mk('원작능력_희귀함_김경현_A09V','A09V !독룡 히드라 — 1/10 약 3초 1250×(0.09초)',
 '원작 마젤란 h01Z Trig_Unique16(GetRandomInt(1,10)==3 → e058 소환) + A09V(Apxf, pxf1 1250 · acdn 0.09 · aare 1000, e058 uhpm 3·재생 −1 → 약 3초 생존). 대상 하나에 0.09초 간격 약 33발 = 3초에 걸쳐 1250×33(주 대상 기준 근사, 원작은 사거리 1000 안 적을 자동 사격). 2026-10-15 구현담당1.',
 0,level(triggerChance=0.1,range=1000.0,effects=[E(kind=0,target=3,damageType=2,attackType=SPELLS,multiplier=1250,hitCount=33,duration=3.0)]))
g_wip=mk('원작능력_희귀함_고어진_A0D9','A0D9 !리젝트 다이얼 — 12.5% 600범위 최대 9명 7000(마법)',
 '원작 와이퍼 h024 Trig_Unique7(GetRandomInt(1,8)==3 → e03K ACfl forkedlightning 오더) + A0D9 툴팁(발동 12.5%·최대 9명·7000 마법데미지, aare 600). ACfl 실제 피해 필드는 맵에 없어(스톡) 툴팁 값을 썼다. 2026-10-15 구현담당1.',
 0,level(triggerChance=0.125,range=600.0,effects=[E(kind=0,target=2,damageType=2,attackType=SPELLS,multiplier=7000,maxTargets=9)]))
g_sug=mk('원작능력_희귀함_강재규_A0IY','A0IY !하비하비열매 — 1/12 장난감 인형 450범위 10000(물리)',
 '원작 슈가 h027 Trig_Unique13(GetRandomInt(1,12)==3 → h06O 장난감 3.5초 소환 → selfdestruct → e0AW A0PK 폭발 aare 450). 피해는 A0IY 툴팁 10000 물리(A0PK Eim1은 3000/0.7초 — 둘이 다르다, 툴팁 쪽을 따름). 2026-10-15 구현담당1.',
 0,level(triggerChance=1/12,range=450.0,effects=[E(kind=0,target=2,damageType=1,attackType=1,multiplier=10000)]))
def set_skills(ro,guids):
    p=sat.ROSTER_DIR+'/%s.asset'%ro; t=open(p,encoding='utf-8').read()
    body='  skills:\n'+''.join('  - {fileID: 11400000, guid: %s, type: 2}\n'%g for g in guids) if guids else '  skills: []\n'
    t,n=re.subn(r'(?m)^  skills:(?: \[\])?\n(?:  - .*\n)*',lambda m:body,t,count=1); assert n==1,ro
    t,n=re.subn(r'(?m)^  skill: .*$','  skill: {fileID: 0}',t,count=1); assert n==1
    open(p,'w',encoding='utf-8').write(t)
def set_field(ro,k,v):
    p=sat.ROSTER_DIR+'/%s.asset'%ro; t=open(p,encoding='utf-8').read()
    t,n=re.subn(r'(?m)^  %s: .*$'%k,'  %s: %s'%(k,v),t,count=1); assert n==1,(ro,k)
    open(p,'w',encoding='utf-8').write(t)
set_skills('희귀함_김경현',[g_pois,g_hyd])
set_skills('희귀함_고어진',[g_wip])
set_skills('희귀함_강재규',[g_sug])
set_field('희귀함_강재규','manaAuraRegenPerSecond','0.6'); set_field('희귀함_강재규','manaAuraRange','700'); set_field('희귀함_강재규','manaAuraBuffId','B02H')
set_skills('희귀함_구주호',[]); set_field('희귀함_구주호','attackSplashRadius','260')
print(made)
