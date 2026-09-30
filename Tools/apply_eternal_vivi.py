"""영원한 채우기 C — 비비(h057 → 영원_문필환) vivi_Skill_Double · vivi_Skill_3_Triple (2026-09-30 구현담당1, PM 승인).

원문 Trig_ViVi_Attack(마나≠150 갈래): 버프 B034가 없으면 더미 e0D3가 B034(A0LY 「5절대쿨 비비」 Ufa1 6초)와
B06G(A121 「3연사공속버프」 공속 +400%·0.2초)를 같이 건다 → B06G가 있으면 3연사(Triple) → 아니면 1/4로 더블샷(Double).
창 0.2초가 덮는 평타: 비비 ua1c 0.74 ÷ (1+4.0) = 0.148초 → 한 타(둘째는 0.296초라 창 밖). 즉 「6초 절대쿨마다 3연사 한 번」.
(공속 버프가 도는 중인 쿨에 바로 먹는다는 건 엔진 지식 — 가설. 제작자 툴팁 A0M1 「절대쿨다운 … 3회」가 그렇게 쓰여 있다.)
우리 축: 3연사 = OnHitChance 확률 1 + cooldown 6 + selfBuffId B034(창을 연 그 평타에서 바로 시전 — 0.148초 뒤 평타를 합침),
더블샷 = 확률 0.25 + requiredBuffId B034. 로스터 skills 순서는 더블샷이 먼저여야 한다(창을 연 타엔 더블샷이 안 나간다).
값(레벨 1): 3연사 한 발 = 100000×A0LZ레벨(1), 대상 PV<200이면 + 최대체력×(0.12+0.015×레벨) — CHAOS/UNIVERSAL, 3발(A0M1 레벨 2면 4발 — 레벨 축 없음).
더블샷 = 폭풍망치 A0LV(AHtb) 750000 + 스턴 1초(adur). ⚠️ 0.15초 뒤 912 안 무작위 적 하나에게 같은 것(A0LW) 한 번 더는
「무작위 적」 대상 축이 없어 미반영.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

ROSTER = '영원_문필환'
SINGLE = 3
NOTE = ' 2026-09-30 구현담당1(ETERNAL_FILL_LIST §2).'


def main():
    changed = []
    specs = [
        ('SkillData_원작트리거_영원_문필환_vivi_Skill_Double', 'vivi_Skill_Double — 더블샷 1/4(절대쿨 B034 중)',
         '원작 비비 h057 Trig_ViVi_Attack → vivi_Skill_Double: 버프 B034가 있는 동안(3연사 창이 아닐 때) 1/4로 대상에게 폭풍망치 더미 e0CW(A0LV AHtb) 750000 + 스턴 1초. '
         '⚠️ 0.15초 뒤 912 안 무작위 적 하나에게 한 번 더(e0CY A0LW)는 무작위 대상 축이 없어 미반영.' + NOTE,
         sat.level_block(triggerChance=0.25, requiredBuffId='B034', effects=[
             sat.effect(kind=0, target=SINGLE, damageType=2, attackType=7, multiplier=750000.0),
             sat.effect(kind=1, target=SINGLE, duration=1.0)])),
        ('SkillData_원작트리거_영원_문필환_vivi_Skill_3_Triple', 'vivi_Skill_3_Triple — 3연사(절대쿨 6초 B034)',
         '원작 비비 h057 Trig_ViVi_Attack → vivi_Skill_3_Triple: B034(6초 절대쿨)가 없을 때 창(B06G 0.2초·공속 +400%)이 열리고 그 안의 다음 평타 하나가 3연사. '
         '한 발 = 100000×A0LZ 레벨(1) (+ 대상 PV<200이면 최대체력×(12%+1.5%×레벨)) CHAOS/UNIVERSAL, 3발. 창을 연 평타에서 바로 시전하는 것으로 합쳤다. '
         'A0M1 레벨 2의 4발·A0LZ 레벨 성장은 레벨 축이 없어 레벨 1 기준.' + NOTE,
         sat.level_block(cooldown=6, triggerChance=1.0, selfBuffId='B034', effects=[
             sat.effect(kind=0, basis=1, target=SINGLE, damageType=2, attackType=5, multiplier=0.135, bonus=100000.0, hitCount=3,
                        targetCondition=1, targetConditionValue=200, skipDamageTakenMultiplier=1),
             sat.effect(kind=0, target=SINGLE, damageType=2, attackType=5, multiplier=100000.0, hitCount=3,
                        targetCondition=3, targetConditionValue=200, skipDamageTakenMultiplier=1)])),
    ]
    for stem, name, desc, level in specs:
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc, 0, [level])
            changed += [path, path + '.meta']
        if sat.add_skill_to_unit(ROSTER, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, ROSTER + '.asset'))
    print('바꾼 파일 %d' % len(changed))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
