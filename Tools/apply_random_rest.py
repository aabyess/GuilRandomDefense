"""랜덤전용 나머지 — RANDOM_FILL_LIST §0-4·5·6·10·11·12 + k' 공속 버프(2026-09-30 구현담당1).

「T랜덤유닛강화」를 사야 열리는 블록은 구조 칸이라 넣지 않는다(있던 것은 뺀다). §0-3(주인 다름)은 보고 대상이라 그대로. 다시 돌려도 같은 결과.
안 넣은 것: §0-7 손오공(유카리 — 마나 145·1/10·1/25·열차 통째) · §0-8 미도리야(LIFE 85·B05N 게이트) · §0-9 이민형(타츠마키 LIFE 50·1/7) · 나루토 회오리/stomp 범위화 · 이치고 장풍.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg  # noqa: E402

SELF, ENEMIES, SINGLE = 0, 2, 3
DAMAGE, STUN, SPEED, SLOW = 0, 1, 12, 13
AD, AP = 1, 2
HERO, SPELLS = 4, 7
CURHP = 2
PV_LT = 1
TAG = '[랜덤 나머지 09-30]'


def note(asset, text):
    m = re.search(r'^  description: (.*)$', asset.head, re.M)
    old = m.group(1) if m else ''
    if TAG in old:
        return False
    add = ' %s %s' % (TAG, text)
    new = old[:-1] + add.replace("'", "''") + "'" if old.startswith("'") and old.endswith("'") else sat.yaml_scalar(old + add)
    if m:
        asset.head = asset.head.replace(m.group(0), '  description: ' + new)
    else:
        asset.head = re.sub(r'^(  skillName: .*\n)', lambda k: k.group(1) + '  description: ' + new + '\n', asset.head, count=1, flags=re.M)
    return True


def main():
    changed = []

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_random_rest.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def hits(n, duration):
        return lambda a: sat.set_effects(a, 0, [retarget(b, hitCount=n, duration=duration) if re.search(r'^\s*-? ?kind: 0$', b, re.M) else b
                                               for b in sat.get_effect_blocks(a, 0)])

    # §0-4 미나토 h09W → 이타도리_유지: Minato_Mana — 500 범위 775000 8회 + stomp 50000·스턴 2.75초
    def minato(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [retarget(b[0], hitCount=8, duration=0.84), dmg(ENEMIES, AP, SPELLS, 50000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.75)])
    edit('SkillData_게이트_랜덤_이타도리_유지_a8343962', '§0-4 Minato_Mana: 775000 8회(0.12초 간격 — 1회였다, 8배 과소) + stomp 50000·스턴 2.75초. 대상 근처 무작위 지점은 대상 중심으로 근사. ', minato)
    # §0-5 키쿄우 h0BF → 이즈미_신이치: kikoyou_mana — 525 범위 (300000 + 100000) 10회
    edit('SkillData_게이트_랜덤_이즈미_신이치_61743e88', '§0-5 kikoyou_mana: 300000 CHAOS/NORMAL + 100000 NORMAL/UNIVERSAL 10회(1회였다, 10배 과소). ', hits(10, 1.0))

    # §0-6 부릉냐 h09N → 호시노_아이: BronyaMotar_R — 500 범위 1000000 5회 → 2500000 + PV<200 최대 6%
    def bronya(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [retarget(b[0], hitCount=5, duration=0.8)] + b[1:])
    edit('SkillData_게이트_랜덤_호시노_아이_55b5cff0', '§0-6 BronyaMotar_R: 1000000은 5회(1회였다). ', bronya)
    # §0-12 hitCount 2 → 1
    edit('SkillData_게이트_랜덤_이즈미_신이치_41677101', '§0-12 kikoyou_skill_1: 350000은 1회씩(hitCount 2였다 — 2배 과다). ', hits(1, 0.0))
    edit('SkillData_게이트_랜덤_호시노_아이_3a13fd3d', '§0-12 BronyaMotar_laser: 250000·225000은 1회씩(hitCount 2였다 — 2배 과다). ', hits(1, 0.0))

    # §0-10 라이토 h073 → 이민형: Light1 — 1/10: 대상 PV<200 현재체력 30%×A11S계수뿐. 350000×1~3·현재 1%는 강화(A0L7) 뒤 1/19 갈래
    def light(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.1)
        sat.set_effects(a, 0, [sat.effect(kind=DAMAGE, basis=CURHP, target=SINGLE, damageType=AP, attackType=HERO, multiplier=0.3, targetCondition=PV_LT, targetConditionValue=200.0)])
        sat.set_head_field(a, 'skillName', 'Light1 데스노트 — 1/10(PV<200 대상 현재체력 30%×A11S계수)')
    edit('SkillData_원작능력_랜덤_이민형', '§0-10 Light1: GetRandomInt(1,10)==5 → 대상 PV<200이면 현재체력 30%×A11S계수 HERO/UNIVERSAL뿐. '
         '350000 · 350000×1~3 · 30%×1~3 · 1%×1~3은 「A0L7(T랜덤유닛강화) 보유 & 1/19」 갈래가 섞인 것 — 강화 구조가 없어 뺐다. 확률 0.14 → 0.1. ', light)

    # §0-11 츠바사 h072 → 이타도리_유지: cat1 — 1/10, A0L8 보유(기본)면 천둥박수 500 범위 150000·이속 −70% 3초. (A0L8이 빠졌을 때만 600 범위 350000·−85%)
    def cat(a):
        sat.set_level_field(a, 0, 'range', 500.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 150000.0), sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.3, duration=3.0)])
        sat.set_head_field(a, 'skillName', 'cat1 할퀴기 — 1/10(500 범위 150000 · 이속 −70% 3초)')
    edit('SkillData_더미채널_랜덤_이타도리_유지_1', '§0-11 cat1: A0L8 보유(uabi 기본) 갈래만 — 500 범위 150000 · 이속 −70% 3초. 단일에 두 갈래(150000 + 350000, 이감 둘)가 같이 들어 있었다. ', cat)

    # k' h06U → 호시노_아이: 1/25 자기 공속 +400% 4초(A0QK, B042)
    stem = 'SkillData_원작트리거_랜덤_호시노_아이_k1_공속'
    path = os.path.join(sat.SKILL_DIR, stem + '.asset')
    if not os.path.exists(path):
        sat.new_asset(stem, "k' — 1/25 자기 공속 +400% 4초(B042)",
                      "원작 k' h06U 평타 트리거 k1: GetRandomInt(1,25) → 자기에게 A0QK(Ablo, Blo1 +4.0 공속, adur 4.0, abuf B042). 2026-09-30 구현담당1(RANDOM_FILL_LIST §2).", 0,
                      [sat.level_block(triggerChance=0.04, effects=[sat.effect(kind=SPEED, target=SELF, multiplier=4.0, duration=4.0, buffId='B042')])])
        changed += [path, path + '.meta']
    if sat.add_skill_to_unit('랜덤_호시노_아이', sat.guid_of(path)):
        changed.append(os.path.join(sat.ROSTER_DIR, '랜덤_호시노_아이.asset'))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
