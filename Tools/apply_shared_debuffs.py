#!/usr/bin/python3
# 공용 디버프 4종(사장님 10-06 확정): 외동 · 01 · 조씨 · 문 — 이름·원문 설명·효과. 쓰는 유닛은 skills 목록에 이 에셋을 그대로 넣는다(능력이 바뀌면 에셋만 고치면 모두 바뀐다).
#   외동 : 주변 600 적 이동속도 +15%(Slow 배수 1.15)        「누구나 엮이기 싫어 도망칩니다.」
#   01   : 이 유닛 공격력 −15%(자기 단점)                    「나이가 어려 때리기 어렵습니다.」
#   조씨 : 주변 600 적 방어력 +5(ArmorBonus +5)               「해롭기 때문에 적은 만반의 준비를 합니다.」
#   문   : 이 유닛 공속 −15%(자기 단점)                      「말로 상대하기 벅찹니다.」
# 다시 돌려도 안전(있으면 효과·설명만 다시 쓴다).
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat

SELF, ENEMIES = 0, 2
SLOW, SPEED, ARMOR_BONUS, PERCENT = 13, 12, 4, 14
AURA = 2
RADIUS = 600.0

DEBUFFS = [
    ('공용_디버프_외동', '외동', '누구나 엮이기 싫어 도망칩니다.', RADIUS, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=1.15, buffId='DEBUFF_외동')]),
    ('공용_디버프_01', '01', '나이가 어려 때리기 어렵습니다.', 0.0, [sat.effect(kind=PERCENT, target=SELF, multiplier=-0.15, buffId='DEBUFF_01')]),
    ('공용_디버프_조씨', '조씨', '해롭기 때문에 적은 만반의 준비를 합니다.', RADIUS, [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=5.0, buffId='DEBUFF_조씨')]),
    ('공용_디버프_문', '문', '말로 상대하기 벅찹니다.', 0.0, [sat.effect(kind=SPEED, target=SELF, multiplier=-0.15, buffId='DEBUFF_문')]),
]
for stem, name, desc, radius, effects in DEBUFFS:
    path = os.path.join(sat.SKILL_DIR, 'SkillData_' + stem + '.asset')
    if not os.path.exists(path):
        sat.new_asset(stem, name, desc, AURA, [sat.level_block(range=radius, effects=effects)])
        print('만듦', stem)
    else:
        a = sat.load(stem)
        sat.set_head_field(a, 'skillName', name)
        sat.set_head_field(a, 'description', desc)
        sat.set_level_field(a, 0, 'range', radius)
        sat.set_effects(a, 0, effects)
        sat.save(a)
        print('고침', stem)
