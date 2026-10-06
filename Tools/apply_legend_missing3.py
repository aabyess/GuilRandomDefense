#!/usr/bin/python3
# 전설 미반영 3건(PM 10-06 결정) — 2026-10-06 구현담당1. 다시 돌려도 안전(있으면 건너뜀).
#  ① 징베 스탬피드 A001 → 정윤식: 쿨 7초 자동 시전, 2초 동안 10발 낙하·발당 150,000·반경 225 → 해석: 반경 225 안 적에게 초당 750,000 DoT 2초(=1,500,000, 중심 적 기준 상한).
#     + 마나 145 블록의 스턴 1.0(원문에 없는 우리쪽 스턴) 삭제.
#  ② 레이쥬 투사체 → 임건웅 거품광선 게이트(1/19)에 더한다: 불새 A0PS 50,000·반경 220 / 제물 A08L 30,000·반경 195(0.15초 주기는 한 번으로 줄임 — 해석).
#  ③ 보아 A084 피스톨키스 → 이현주: 해석 — 평타 7.9% 확률로 +50,000 추가 피해 + 0.4초 공속 +47%(원문 「화살당」 발동 조건이 모호해 PM이 이 해석으로 확정).
import os, re, sys
sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, SPEED, DOT = 0, 1, 12, 26
ON_HIT, COOLDOWN = 0, 1
AP, SPELLS = 2, 7
R = lambda n: '전설적인_' + n

def make(stem, name, desc, trig, roster, level):
    path = os.path.join(sat.SKILL_DIR, 'SkillData_' + stem + '.asset')
    if not os.path.exists(path):
        sat.new_asset(stem, name, desc, trig, [level])
        print('새로', stem)
    sat.add_skill_to_unit(roster, sat.guid_of(path))

# ① 징베
make('원작능력_전설적인_정윤식_A001', 'A001 스탬피드 — 2초 다발 낙하(쿨 7초·반경 225)',
     '원작 징베 h03G A001(스탬피드): 2초 동안 10발 낙하 · 발당 150,000 · 반경 225 · 쿨 7초. 낙하 위치 분포 원문 없음 → 해석: 쿨다운마다 반경 225 안 적에게 초당 750,000 DoT 2초(=1,500,000, 중심 적 기준 상한). 2026-10-06 구현담당1.',
     COOLDOWN, R('정윤식'),
     sat.level_block(cooldown=7.0, range=225.0, effects=[
         sat.effect(kind=DOT, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=750000.0, duration=2.0)]))
a = sat.load('SkillData_원작능력_전설적인_정윤식')
blocks = sat.get_effect_blocks(a, 0)
keep = [b for b in blocks if not re.search(r'^    - kind: 1$', b, re.M)]
if len(keep) != len(blocks):
    sat.set_effects(a, 0, keep); sat.save(a); print('정윤식 마나 145 블록 스턴 1.0 삭제')

# ② 레이쥬 투사체
g = sat.load('SkillData_게이트_전설적인_임건웅_87ed0ff5')
if len(sat.get_effect_blocks(g, 0)) == 1:
    extra = [sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=50000.0, targetCondition=1, targetConditionValue=200.0),
             sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=30000.0, targetCondition=1, targetConditionValue=200.0)]
    sat.set_effects(g, 0, sat.get_effect_blocks(g, 0) + extra)
    sat.set_level_field(g, 0, 'range', 220.0)
    sat.save(g); print('레이쥬 거품광선에 투사체 둘 추가')

# ③ 보아
make('원작능력_전설적인_이현주_A084', 'A084 피스톨키스 — 7.9% +50,000·0.4초 공속 +47%',
     '원작 보아 핸콕 h032 A084(AHfa, Hfa1 50,000 · 마나 5 · 키스 7.9% 때 B00D 0.4초 + 마나 50). 「화살당」 발동 조건이 원문에 모호 → 해석(PM 10-06): 평타 7.9% 확률로 맞은 적에게 +50,000 추가 피해 + 0.4초 자기 공속 +47%(핸콕 주기 0.76÷1.47). 2026-10-06 구현담당1.',
     ON_HIT, R('이현주'),
     sat.level_block(triggerChance=0.079, effects=[
         sat.effect(kind=DAMAGE, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=50000.0),
         sat.effect(kind=SPEED, target=SELF, multiplier=0.47, duration=0.4, buffId='A084_KISS')]))
print('ok')
