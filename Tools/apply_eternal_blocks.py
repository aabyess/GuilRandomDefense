"""영원한 「반영」 트리거 블록 단위 정정(2026-09-30 구현담당1, PM 승인 — Docs/research/ETERNAL_FILL_LIST.md §10).

전부 war3map_new.j 원문 블록을 끝까지 읽고 넣었다(스크래치 show.py — 조건 함수를 본문에 끼워 출력). 다시 돌려도 같은 결과.

미호크 h058(영원_최상호) Trig_Mihawk_Attack → Mihawk_2(마나≠175 매 타): integerA = GetRandomInt(1,8)
  ==5(1/8): 평타가 맞으면 장풍 A0B6 750000 + 475 범위 500000 MAGIC/UNIVERSAL
  그 밖(7/8): 평타가 맞으면 대상 250000 MAGIC/UNIVERSAL, integerB = GetRandomInt(1,7)==6이면 500 범위 300000 두 번(7/8 × 1/7 = 1/8)
  → 있던 장풍 에셋은 확률 1.0(매 타)이었다 = 8배 과다. 배타 분기는 독립 굴림 둘로 근사(0.875 / 0.125 / 0.125).
  Mihawk_4(매 타 1/7): 450 범위 450000 MAGIC/UNIVERSAL(같이 생기는 e0PQ·e0PR·e0PP는 명령이 없어 겉모습).
핸콕 h05C hancock_skill_Mana(마나 175, 영원_조세민 A0IN): 타격이 16+4×레벨번(레벨 1 = 20번, 툴팁 「20회 타격」) 돈다 —
  매번 760 범위 360000+30000×연구, 첫 적에게 최대체력 16%(PV≥200이면 300000). 있던 에셋은 한 번만 쳤다(20분의 1).
  stomp A0QN(785 범위 400000·스턴 2.5초)은 첫 타·중간(스테이지 10)·끝 세 번. 레벨 반경이 하나라 760으로 같이.
핸콕 arrow_h2(영원_김영원): 버프 B03M이 있는 동안 매 타 — 있던 에셋은 「마나 175 AND B03M」(같이 설 수 없는 갈래)이라 안 나갔다.
  발동 방식을 평타(확률 1) + requiredBuffId B03M으로. 장풍 A0OK 400000도 같은 블록이라 같이(조세민의 확률 0 잠금 에셋은 그대로 둔다).
우타 h067(영원_김정래) Uta_skill_2(매 타 1/33): 500 범위 750000×1~1.5 NORMAL/UNIVERSAL 세 번(realB<3). 1/4 병사 소환은 소환 구조라 제외.
  Uta_skill_3_mana: 800 범위 적마다 AId1 +5 · Aegr +5 · A11S +5(피해보다 먼저) — 있던 에셋은 피해만.
  (Uta_skill_1의 「10%×(0.20+0.05×A11S)」은 A11S 항을 런타임 PercentDamageTakenMultiplier가 곱하므로 지금 값이 맞다 — 안 건드림.)
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

SINGLE, ENEMIES = 3, 2
MAGIC, SPELLS = 6, 7
TAG = '[블록정정 09-30]'


def note(asset, text):
    m = re.search(r'^  description: (.*)$', asset.head, re.M)
    old = m.group(1)
    if TAG in old:
        return False
    add = ' %s %s' % (TAG, text)
    new = old[:-1] + add.replace("'", "''") + "'" if old.startswith("'") and old.endswith("'") else sat.yaml_scalar(old + add)
    asset.head = asset.head.replace(m.group(0), '  description: ' + new)
    return True


def main():
    changed = []

    def new(roster, stem, name, desc, level):
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc + ' 2026-09-30 구현담당1(ETERNAL_FILL_LIST §10).', 0, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    # 1. 미호크 장풍: 매 타 → 1/8, 같은 블록의 475 범위 500000 추가
    a = sat.load('SkillData_더미채널_영원_최상호_79행_10000')
    if note(a, 'Mihawk_2 integerA==5(1/8) 갈래 — 확률 1.0 → 0.125, 같은 블록의 475 범위 500000 MAGIC/UNIVERSAL 추가(Tools/apply_eternal_blocks.py).'):
        sat.set_level_field(a, 0, 'triggerChance', 0.125)
        sat.set_level_field(a, 0, 'range', 475.0)
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=0, target=ENEMIES, damageType=2, attackType=MAGIC, multiplier=500000.0)])
        sat.save(a)
        changed.append(a.path)

    # 2~4. 미호크 빠진 블록
    new('영원_최상호', 'SkillData_원작트리거_영원_최상호_Mihawk_2_기본', 'Mihawk_2 — 평타마다 대상 250000(7/8)',
        '원작 미호크 h058 Mihawk_2: 장풍 갈래(1/8)가 아닐 때 평타가 맞으면 대상 250000 MAGIC/UNIVERSAL.',
        sat.level_block(triggerChance=0.875, effects=[sat.effect(kind=0, target=SINGLE, damageType=2, attackType=MAGIC, multiplier=250000.0)]))
    new('영원_최상호', 'SkillData_원작트리거_영원_최상호_Mihawk_2_연속베기', 'Mihawk_2 — 연속 베기 1/8(500 범위 300000 ×2)',
        '원작 미호크 h058 Mihawk_2: 7/8 갈래 안 GetRandomInt(1,7)==6 → 500 범위 300000 MAGIC/UNIVERSAL 두 번(0.08초 간격).',
        sat.level_block(triggerChance=0.125, range=500.0,
                        effects=[sat.effect(kind=0, target=ENEMIES, damageType=2, attackType=MAGIC, multiplier=300000.0, hitCount=2, duration=0.08)]))
    new('영원_최상호', 'SkillData_원작트리거_영원_최상호_Mihawk_4', 'Mihawk_4 — 1/7(450 범위 450000)',
        '원작 미호크 h058 Trig_Mihawk_Attack 바깥 GetRandomInt(1,7)==1 → Mihawk_4: 450 범위 450000 MAGIC/UNIVERSAL 한 번.',
        sat.level_block(triggerChance=1.0 / 7.0, range=450.0,
                        effects=[sat.effect(kind=0, target=ENEMIES, damageType=2, attackType=MAGIC, multiplier=450000.0)]))

    # 6. 핸콕 마나: 20타(레벨 2는 25타) + stomp 셋
    a = sat.load('SkillData_원작028_h05C')
    if note(a, 'hancock_skill_Mana는 타격이 16+4×레벨번(레벨 1 = 20, 레벨 2 = 24 — 툴팁은 20·25) 돈다 — 피해 셋의 hitCount 1 → 20/24, 간격 0.08/0.055초. '
               'stomp A0QN 400000·스턴 2.5초 세 번 추가(원작 반경 785는 760으로).'):
        for i, (hits, step) in enumerate([(20, 0.03 + 0.05), (24, 0.03 + 0.05 / 2)]):
            if i >= len(a.levels):
                break
            blocks = []
            for b in sat.get_effect_blocks(a, i):
                b = re.sub(r'^(      hitCount:) .*$', r'\1 %d' % hits, b, flags=re.M)
                b = re.sub(r'^(      duration:) .*$', r'\1 %s' % sat.num(round(hits * step, 3)), b, flags=re.M)
                blocks.append(b)
            blocks.append(sat.effect(kind=0, target=ENEMIES, damageType=2, attackType=SPELLS, multiplier=400000.0, hitCount=3, duration=round(hits * step, 3)))
            blocks.append(sat.effect(kind=1, target=ENEMIES, duration=2.5))
            sat.set_effects(a, i, blocks)
        sat.save(a)
        changed.append(a.path)

    # 7. 핸콕 슬레이브 애로우 게이트
    a = sat.load('SkillData_게이트_영원_김영원_86ef2902')
    if note(a, 'arrow_h2는 「B03M이 있는 동안 매 타」다(마나≠175 갈래) — 발동 방식 OnHitCount(마나 175) → OnHitChance 확률 1 + requiredBuffId B03M. 같은 블록의 장풍 A0OK 400000 추가.'):
        a.head = re.sub(r'^  triggerType: 3$', '  triggerType: 0', a.head, flags=re.M)
        sat.set_level_field(a, 0, 'hitCountThreshold', 0)
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=0, target=SINGLE, damageType=1, attackType=4, multiplier=400000.0)])
        sat.save(a)
        changed.append(a.path)

    # 8. 우타 1/33 + 마나 스킬의 스택 셋
    new('영원_김정래', 'SkillData_원작트리거_영원_김정래_Uta_skill_2', 'Uta_skill_2 — 늘임표 낙하 1/33(500 범위 750000×1~1.5 ×3)',
        '원작 우타 h067 Trig_Uta_Attack GetRandomInt(1,33)==8 → Uta_skill_2: 500 범위 750000×1~1.5 NORMAL/UNIVERSAL 세 번(0.4초 뒤부터 0.05초 간격). 1/4 음표 병사 소환은 소환 구조라 제외.',
        sat.level_block(triggerChance=1.0 / 33.0, range=500.0,
                        effects=[sat.effect(kind=0, target=ENEMIES, damageType=2, attackType=SPELLS, multiplier=750000.0, randMax=1.5, hitCount=3, duration=0.15)]))
    a = sat.load('SkillData_게이트_영원_김정래_a22567e4')
    if note(a, 'Uta_skill_3_mana는 800 범위 적마다 피해 전에 AId1 +5 · Aegr +5 · A11S +5를 올린다 — 스택 셋 추가.'):
        stacks = [sat.effect(kind=k, target=ENEMIES, multiplier=5.0) for k in (2, 8, 10)]
        sat.set_effects(a, 0, stacks + sat.get_effect_blocks(a, 0))
        sat.save(a)
        changed.append(a.path)

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
