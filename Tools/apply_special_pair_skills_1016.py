"""특수함 6기 ↔ 원작 특수함 6기 짝 스킬·능력치(사장님 10-16, PM 지시) — 구현담당1.
짝: 장진희=모건 h05H(완료) · 황길라=아이스버그 h05O(A0IL 완료·A0Q3 조선공은 건조 능력이라 전투 효과 없음) · BJ 율희=블고리 h05Q A0EF ·
    임재현=페루 h05R A0PE+A0PD · 장명자=요정 h05P(공격력 14+1d3=16·쿨 1.0 — ua1b 비어 베이스 hrif 값) · 헬로우먼=베티 h08N A0X6+A0WO+A0WQ.
근거(w3a 직접 디코드): A0EF AHad Had1 −30·aare 850·B01P / A0PE ACac Cac1 0.6·aare 650·B03O / A0PD Aroa Roa1 0.6·aare 900·adur 40·acdn 40·B03N
  A0WO AOae Oae2 0.11·aare 850·B054 / A0X6 AUau Uau2 2.0·aare 850·B057 / A0WQ AIba Hab1 1.25·aare 850·B053
"""
import sys,re
sys.path.insert(0,'Tools'); import skill_asset_tool as sat
E=sat.effect
made={}
def mk(stem,name,desc,trig,lv):
    a=sat.new_asset(stem,name,desc,trig,[lv]); made[stem]=sat.guid_of(a); return made[stem]
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
AURA,CD=2,1
g_ef=mk('원작오라_특수함_BJ_율희_A0EF','A0EF B간수 — 적 방어 −30 오라',
 '원작 블고리 h05Q uabi A0EF(AHad, Had1 −30, aare 850, abuf B01P, atar air,enemies,ground). 2026-10-16 구현담당1.',
 AURA,sat.level_block(range=850.0,effects=[E(kind=4,target=2,multiplier=-30,buffId='B01P')]))
g_pe=mk('원작오라_특수함_임재현_A0PE','A0PE #왕가의 수호신 — 아군 공격력 +60% 오라',
 '원작 페루 h05R uabi A0PE(ACac, Cac1 0.6, aare 650, abuf B03O, atar self+friend). 2026-10-16 구현담당1.',
 AURA,sat.level_block(range=650.0,effects=[E(kind=14,target=0,multiplier=0.6,buffId='B03O'),E(kind=14,target=1,multiplier=0.6,buffId='B03O')]))
g_pd=mk('원작능력_특수함_임재현_A0PD','A0PD !격려-페루 — 쿨 40초 공격력 +60%(40초)',
 '원작 페루 h05R uabi A0PD(Aroa, Roa1 0.6, aare 900(툴팁 500), adur 40, acdn 40, amcs 0, abuf B03N). 지속=쿨 40초라 상시. 2026-10-16 구현담당1.',
 CD,sat.level_block(cooldown=40.0,range=900.0,effects=[E(kind=14,target=0,multiplier=0.6,duration=40.0,buffId='B03N'),E(kind=14,target=1,multiplier=0.6,duration=40.0,buffId='B03N')]))
g_wo=mk('원작오라_특수함_헬로우먼_A0WO','A0WO $군대장 — 아군 공속 +11% 오라',
 '원작 베티 h08N uabi A0WO(AOae, Oae2 0.11, aare 850, abuf B054, atar self+friend). 2026-10-16 구현담당1.',
 AURA,sat.level_block(range=850.0,effects=[E(kind=12,target=0,multiplier=0.11,buffId='B054'),E(kind=12,target=1,multiplier=0.11,buffId='B054')]))
set_skills('특수함_BJ_율희',[g_ef])
set_skills('특수함_임재현',[g_pe,g_pd])
set_skills('특수함_헬로우먼',[g_wo])
set_field('특수함_헬로우먼','lifeAuraRegenPerSecond','2'); set_field('특수함_헬로우먼','lifeAuraRange','850'); set_field('특수함_헬로우먼','lifeAuraBuffId','B057'); set_field('특수함_헬로우먼','lifeAuraIncludesSelf','1')
set_field('특수함_헬로우먼','manaAuraRegenPerSecond','1.25'); set_field('특수함_헬로우먼','manaAuraRange','850'); set_field('특수함_헬로우먼','manaAuraBuffId','B053'); set_field('특수함_헬로우먼','manaAuraIncludesSelf','1')
set_skills('특수함_장명자',[]); set_field('특수함_장명자','attackPower','16'); set_field('특수함_장명자','attackSpeed','1')
print(made)
