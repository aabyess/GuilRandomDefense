#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ORIGINAL_DAMAGE_TYPING.csv로 06번①(7종) 이미 채운 효과의 attackType/damageType 정정.

정책(PM 지시, 2026-09-05): attackType은 NORMAL→Spells(7)·MELEE→Normal(1)·나머지 1:1,
damageType은 UNIVERSAL→AP(2)·NORMAL→AD(1). MELEE 예외 18건은 h067·h084/h086/h042/h085·
h026·h03E·h05W·h05I·h08H에만 있고 이번 7종(h04B~h05C, h049)엔 없어 해당 없다.

⚉ 손으로 하나씩 대조했다(자동 매칭이 아니다) — 트리거명 표기가 두 조사(2차 채널
CSV·이번 타이핑표)마다 달라서(예: "Trig_Sengoku_Attack" vs "Sengoku_Attack") 문자열
매칭이 불안정하다. 대신 유닛ID + 계산식 값(리터럴 숫자)으로 사람이 확인했다.

⚠️ 「더미 스톡필드」로 넣은 값(Wrs1·Blo1·Had1 등)은 이 표의 대상이 아니다 — 이 표는
RRD/UnitDamageTarget **스크립트 호출**만 추적한 것이고, 스톡필드는 소환 유닛의
오브젝트에디터 속성 상속이라 근본적으로 다른 채널이다. 매칭 안 되는 게 정상이며
지어내지 않고 그대로 둔다:
  - h04E 3번째 효과(더미 A0PL, Wrs1=427500) — 대상 아님
  - h04B 효과(h07R 자체 평타, 200,000) — 대상 아님(RRD 호출이 아니라 소환 유닛 평타)
  - h049 전체(Had1, ArmorBreak) — 대상 아님

대응표(유닛ID, 트리거, RRD순번, 계산식 리터럴, 공격타입→피해타입 판정):
  h04E Sengoku_Attack   1  400000  NORMAL/UNIVERSAL → Spells/AP  (기존 효과 1)
  h04E Sengoku_Attack   2  300000  NORMAL/UNIVERSAL → Spells/AP  (기존 효과 2)
  h04G Z_Skill_1        1  (레벨식) NORMAL/UNIVERSAL → Spells/AP (기존 효과 1, L0/L1 둘 다)
  h04G Z_Skill_1        2  1250000 NORMAL/UNIVERSAL → Spells/AP (기존 효과 2, L0/L1 둘 다)
  h04C Garp_Mana2       1~3      HERO/NORMAL → 변화 없음(이미 Hero/AD)
  h04D Dragon_Skill_1   1,2      NORMAL/UNIVERSAL → Spells/AP (levels[0] 둘 다)
  h04D Dragon_Skill_1_T 1,2,3    NORMAL/UNIVERSAL → Spells/AP (levels[1] 셋 다)
  h05C hancock_skill_Mana 3  300000              NORMAL/UNIVERSAL → Spells/AP (효과1)
  h05C hancock_skill_Mana 2  (%maxHP식)           NORMAL/UNIVERSAL → Spells/AP (효과2)
  h05C hancock_skill_Mana 1  (연구비례식)          CHAOS/NORMAL → 변화 없음(이미 Chaos/AD) (효과3)
  (h05C levels[1]은 levels[0]과 같은 3효과 복사본이라 같은 규칙 적용)
"""
import re

SKILL_DIR = 'Assets/Data/UnitSkills'

SPELLS = 7
AP = 2


def set_field_in_effect_block(block, field, value):
    block2, n = re.subn(rf'(\n {{6}}{field}: )[^\n]*', rf'\g<1>{value}', block, count=1)
    assert n == 1, f"{field} 자리를 못 찾음: {block[:80]!r}"
    return block2


def patch_effect_by_index(text, level_index, effect_index, attack_type=None, damage_type=None):
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    blocks = re.findall(r'    - kind: 0\n(?:      .*\n)+', level)
    assert effect_index < len(blocks), f"level {level_index}에 effect {effect_index}가 없음"
    old_block = blocks[effect_index]
    new_block = old_block
    if attack_type is not None:
        new_block = set_field_in_effect_block(new_block, 'attackType', attack_type)
    if damage_type is not None:
        new_block = set_field_in_effect_block(new_block, 'damageType', damage_type)
    level = level.replace(old_block, new_block, 1)
    levels[1 + level_index] = level
    return '\n  - cooldown: '.join(levels)


def append_description(text, note):
    text, n = re.subn(r'^(  description: .*)$', lambda m: m.group(1) + note, text, count=1, flags=re.M)
    assert n == 1
    return text


def cleanup(path, text):
    text = re.sub(r'\n\n+', '\n', text)
    if not text.endswith('\n'):
        text += '\n'
    open(path, 'w', encoding='utf-8').write(text)


TYPING_NOTE = (
    " 2026-09-05 타입 정정(ORIGINAL_DAMAGE_TYPING.csv, 60b9797): 원작 ATTACK_TYPE_NORMAL은 "
    "이름과 달리 Spells 행이고 DAMAGE_TYPE_UNIVERSAL은 방어력을 뚫는다(우리 AP에 대응) — "
    "이 효과는 표에서 유닛ID+계산식으로 직접 확인해 attackType/damageType을 정정했다."
)


def main():
    # h04E — 효과 1,2 정정, 효과 3(더미 A0PL)은 표 대상 아님
    path = f'{SKILL_DIR}/SkillData_원작021_h04E.asset'
    text = open(path, encoding='utf-8').read()
    text = patch_effect_by_index(text, 0, 0, attack_type=SPELLS, damage_type=AP)
    text = patch_effect_by_index(text, 0, 1, attack_type=SPELLS, damage_type=AP)
    text = append_description(
        text, TYPING_NOTE + " 효과 3(더미 A0PL, Wrs1 스톡필드)은 RRD 호출이 아니라 "
        "이 표의 대상이 아니다 — 그대로 둔다."
    )
    cleanup(path, text)
    print('h04E — 효과 1,2 Spells/AP로 정정 (효과 3은 표 대상 아님, 유지)')

    # h04G — levels[0]/[1] 각 2효과 전부 정정
    path = f'{SKILL_DIR}/SkillData_원작022_h04G.asset'
    text = open(path, encoding='utf-8').read()
    for lvl in (0, 1):
        for eff in (0, 1):
            text = patch_effect_by_index(text, lvl, eff, attack_type=SPELLS, damage_type=AP)
    text = append_description(text, TYPING_NOTE)
    cleanup(path, text)
    print('h04G — levels[0]/[1] 전부 Spells/AP로 정정')

    # h04C — Garp_Mana2 전부 HERO/NORMAL, 이미 Hero/AD라 변화 없음. 꼬리표만.
    path = f'{SKILL_DIR}/SkillData_원작023_h04C.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text, " 2026-09-05 타입 확인(ORIGINAL_DAMAGE_TYPING.csv): Garp_Mana2 RRD 1~3 전부 "
        "HERO/NORMAL(방어무시 X)이라 이미 넣은 Hero/AD가 맞다 — 변경 없음."
    )
    cleanup(path, text)
    print('h04C — 확인만, 변경 없음(이미 Hero/AD 정확)')

    # h04D — levels[0] 2효과, levels[1] 3효과 전부 정정
    path = f'{SKILL_DIR}/SkillData_원작024_h04D.asset'
    text = open(path, encoding='utf-8').read()
    for eff in (0, 1):
        text = patch_effect_by_index(text, 0, eff, attack_type=SPELLS, damage_type=AP)
    for eff in (0, 1, 2):
        text = patch_effect_by_index(text, 1, eff, attack_type=SPELLS, damage_type=AP)
    text = append_description(text, TYPING_NOTE)
    cleanup(path, text)
    print('h04D — levels[0]/[1] 전부 Spells/AP로 정정')

    # h04B — h07R 자체 평타(RRD 아님), 표 대상 아님. 꼬리표만.
    path = f'{SKILL_DIR}/SkillData_원작025_h04B.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text, " 2026-09-05 타입 확인(ORIGINAL_DAMAGE_TYPING.csv): 이 효과(h07R 소환 유닛 "
        "자체 평타)는 RRD/UnitDamageTarget 스크립트 호출이 아니라 표의 대상이 아니다 — "
        "그대로 둔다(attackType Normal 근사 유지)."
    )
    cleanup(path, text)
    print('h04B — 확인만, 변경 없음(표 대상 아님)')

    # h049 — Had1(ArmorBreak, 스톡필드), 표 대상 아님. 꼬리표만.
    path = f'{SKILL_DIR}/SkillData_원작026_h049.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text, " 2026-09-05 타입 확인(ORIGINAL_DAMAGE_TYPING.csv): Had1(ArmorBreak) 효과는 "
        "스톡 능력 필드 상속이라 RRD 호출이 아니다 — 이 표의 대상이 아니다."
    )
    cleanup(path, text)
    print('h049 — 확인만, 변경 없음(표 대상 아님)')

    # h05C — levels[0]/[1] 각 3효과 중 1,2번만 정정(3번은 이미 Chaos/AD 정확)
    path = f'{SKILL_DIR}/SkillData_원작028_h05C.asset'
    text = open(path, encoding='utf-8').read()
    for lvl in (0, 1):
        text = patch_effect_by_index(text, lvl, 0, attack_type=SPELLS, damage_type=AP)  # Flat 300,000
        text = patch_effect_by_index(text, lvl, 1, attack_type=SPELLS, damage_type=AP)  # TargetMaxHpPercent×0.16
        # 효과 2(index 2, ResearchLevel)는 CHAOS/NORMAL이라 이미 Chaos/AD 정확 — 그대로 둔다.
    text = append_description(
        text, TYPING_NOTE + " 효과 3(ResearchLevel×연구횟수)은 CHAOS/NORMAL(방어무시 X)이라 "
        "이미 넣은 Chaos/AD가 맞다 — 변경 없음."
    )
    cleanup(path, text)
    print('h05C — levels[0]/[1] 효과 1,2 Spells/AP로 정정, 효과 3은 변경 없음')


if __name__ == '__main__':
    main()
