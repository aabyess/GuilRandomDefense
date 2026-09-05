#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""06번① levels[1](승급분) 마무리 — ORIGINAL_TRAIT_LEVEL2.csv 기준.

사장님 결정(2026-09-05): "모든 것 원작 따라간다" — 보류했던 축소·근사 판단 없이
원작 값을 그대로 옮긴다. 이번 4건:

- 센고쿠(h04E) levels[1]: 값 없음(더미 e0K6 추가라고만 나오고 수치가 없다) —
  description에 꼬리표만 남기고 effects는 계속 비운다.
- 거프(h04C): levels[1]은 이미 GARP_MANA2_STRUCTURE.md로 채워져 있다(2 effects) —
  ORIGINAL_TRAIT_LEVEL2.csv가 추가로 언급하는 "대상 버프 개수×0.03" 가산분은 누적
  스택 비례라 우리 축 밖 — description에 꼬리표만 추가.
- 레일리(h049): 새 값 확보 — Had1(방어력 감소류 스톡 필드) L1=-20 → L2=-25. 지금까지
  levels[0]도 Ucs1~4 모호성 때문에 완전히 비어 있었는데, 이 Had1은 그 넷과 별개
  필드라 지금 levels[0]/[1] 둘 다 채운다. kind=ArmorBreak — UnitAttacker.ApplyToEnemy
  주석에 "값 의미가 없다... 조용히 무시"라고 명시돼 있어 지금은 순수 데이터 자리다
  (읽는 코드가 아직 없다, Damage/Stun/ArmorBonus만 실제로 읽는다). Ucs1~4는 여전히
  미확인이라 별개로 비워둔다.
- 핸콕(h05C) levels[1]: ⚠⚠ 원작이 "간격 0.03+0.05/레벨"이라 레벨업 시 간격이 짧아지는
  게 강화(더 자주 때림)다. 우리 SkillEffect엔 "간격" 축이 없지만 hitCount/duration
  (간격=duration/hitCount)으로 다단히트를 표현하는 기존 메커니즘이 있다 — 그걸로
  "발동 빈도"를 옮긴다. levels[0]는 hitCount=1(그대로, 이미 리뷰 통과)로 두고,
  levels[1]은 levels[0]과 같은 3효과를 hitCount=2·duration=0.11(=2×0.055, L2 간격)로
  펼쳐 "레벨2가 레벨1보다 반드시 세지게" 만든다 — 정확한 간격 비율(0.08/0.055≈1.45배)의
  정밀한 재현은 아니고 "더 자주 터진다"는 방향성만 보존한 근사다(description에 명시).
"""
import re

SKILL_DIR = 'Assets/Data/UnitSkills'


def set_level_field(text, level_index, field, value):
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    level, n = re.subn(rf'(^|\n)( +){field}: [^\n]*', lambda m: f'{m.group(1)}{m.group(2)}{field}: {value}',
                        level, count=1)
    if n == 0 and field == 'cooldown':
        level, n = re.subn(r'^[^\n]*', str(value), level, count=1)
    assert n == 1, f"{field} 자리를 못 찾음"
    levels[1 + level_index] = level
    return '\n  - cooldown: '.join(levels)


def replace_level_effects(text, level_index, new_effect_blocks):
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    replacement = '    effects: []\n' if not new_effect_blocks else '    effects:\n' + new_effect_blocks
    level, n = re.subn(r'    effects:.*?(?=\n  - cooldown: |\Z)', lambda m: replacement,
                        level, count=1, flags=re.S)
    if n == 0:
        level, n = re.subn(r'    effects: \[\]\n?', lambda m: replacement, level, count=1)
    assert n == 1, "effects 자리를 못 찾음"
    levels[1 + level_index] = level
    return '\n  - cooldown: '.join(levels)


def get_level_effect_blocks(text, level_index):
    """이미 채워진 level의 effect 블록들을 그대로 문자열로 뽑아온다(복사용)."""
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    m = re.search(r'    effects:\n(.*?)(?=\n  - cooldown: |\Z)', level, re.S)
    assert m, "복사할 effects를 못 찾음"
    return m.group(1) + ('\n' if not m.group(1).endswith('\n') else '')


def effect_yaml(kind, basis, mult, bonus, target, attack_type, damage_type=1, hit_count=1, duration=0):
    return (
        f"    - kind: {kind}\n"
        f"      basis: {basis}\n"
        f"      target: {target}\n"
        f"      damageType: {damage_type}\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(mult, 4)}\n"
        f"      bonus: {round(bonus, 4)}\n"
        "      chance: 1\n"
        f"      hitCount: {hit_count}\n"
        f"      duration: {duration}\n"
    )


def append_description(text, note):
    text, n = re.subn(r'^(  description: .*)$', lambda m: m.group(1) + note, text, count=1, flags=re.M)
    assert n == 1, "description 자리를 못 찾음"
    return text


def cleanup(path, text):
    text = re.sub(r'\n\n+', '\n', text)
    if not text.endswith('\n'):
        text += '\n'
    open(path, 'w', encoding='utf-8').write(text)


def main():
    # ── 센고쿠(h04E) — 값 없음, 꼬리표만 ─────────────────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작021_h04E.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text,
        " 2026-09-05 levels[1] 확인(ORIGINAL_TRAIT_LEVEL2.csv): 레벨==2 분기에서 더미 "
        "e0K6(부처의 심판2) 추가 생성뿐, 수치가 없다 — 지어내지 않고 levels[1]은 계속 "
        "비워둔다."
    )
    cleanup(path, text)
    print('센고쿠(h04E) — 꼬리표만 추가, levels[1] 계속 비움')

    # ── 거프(h04C) — 이미 채워진 levels[1]에 축 밖 가산분 꼬리표만 ───────────
    path = f'{SKILL_DIR}/SkillData_원작023_h04C.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text,
        " 2026-09-05 levels[1] 확인(ORIGINAL_TRAIT_LEVEL2.csv): 원작은 레벨==2 분기에서 "
        "추가 효과 크기 = 대상 버프 개수×0.03(누적 스택 비례)이 더 붙는데, 그 축이 우리에 "
        "없어 못 옮긴다 — GARP_MANA2_STRUCTURE.md 기준 2효과만 유지."
    )
    cleanup(path, text)
    print('거프(h04C) — 꼬리표만 추가')

    # ── 레일리(h049) — Had1 -20→-25, levels[0]/[1] 둘 다 채움 ────────────────
    path = f'{SKILL_DIR}/SkillData_원작026_h049.asset'
    text = open(path, encoding='utf-8').read()
    text = set_level_field(text, 1, 'hitCountThreshold', 115)  # threshold=0 위험 회피
    l0 = effect_yaml(kind=2, basis=0, mult=20.0, bonus=0.0, target=3, attack_type=4)  # ArmorBreak, HERO(영웅유닛 근사)
    l1 = effect_yaml(kind=2, basis=0, mult=25.0, bonus=0.0, target=3, attack_type=4)
    text = replace_level_effects(text, 0, l0)
    text = replace_level_effects(text, 1, l1)
    text = append_description(
        text,
        " 2026-09-05 정정: Ucs1~4(더미 스톡필드 4개 중첩)와 별개로 Had1(방어력 감소류 "
        "스톡 필드) 값을 확보했다 — L1=-20, L2=-25(ORIGINAL_TRAIT_LEVEL2.csv). "
        "kind=ArmorBreak로 넣었다 — UnitAttacker.ApplyToEnemy 주석에 명시된 대로 지금은 "
        "읽는 코드가 없어 순수 데이터 자리다(값을 넣어도 게임 동작은 안 바뀐다). "
        "attackType은 원본 표기가 없어 이 유닛 자신의 평타 타입으로 근사. Ucs1~4는 "
        "필드뜻이 여전히 미확인이라 별개로 비워둔다."
    )
    cleanup(path, text)
    print('레일리(h049) — Had1 ArmorBreak로 levels[0]/[1] 채움(현재 읽는 코드 없음)')

    # ── 핸콕(h05C) — levels[1]에 hitCount=2/duration=0.11로 발동빈도 반영 ────
    path = f'{SKILL_DIR}/SkillData_원작028_h05C.asset'
    text = open(path, encoding='utf-8').read()
    level0_blocks = get_level_effect_blocks(text, 0)
    level1_blocks = level0_blocks.replace('hitCount: 1', 'hitCount: 2').replace('duration: 0', 'duration: 0.11')
    text = set_level_field(text, 1, 'hitCountThreshold', 175)  # threshold=0 위험 회피
    text = set_level_field(text, 1, 'range', 760)  # level0의 Enemies range(760)를 복사 —
    # range=0으로 두면 "range>0f" 검사 자체가 스킵돼 무제한 범위가 되어 더 위험하다.
    text = replace_level_effects(text, 1, level1_blocks)
    text = append_description(
        text,
        " 2026-09-05 levels[1] 확인(ORIGINAL_TRAIT_LEVEL2.csv): ⚠⚠ 원작이 "
        "\"간격=0.03+0.05/레벨\"이라 L1=0.08초, L2=0.055초 — 값이 작아지는 게 강화다"
        "(더 자주 때린다). 우리 스키마엔 \"간격\" 축이 없어 hitCount/duration(간격="
        "duration/hitCount)으로 발동 빈도를 옮겼다 — levels[0]과 같은 3효과를 "
        "hitCount=2·duration=0.11(=2×0.055)로 펼쳐 레벨2가 레벨1보다 반드시 세지게 "
        "했다. 실제 간격 비율(0.08/0.055≈1.45배)의 정밀한 재현은 아니고 방향성"
        "(더 자주 터진다)만 보존한 근사다."
    )
    cleanup(path, text)
    print('핸콕(h05C) — levels[1] hitCount=2/duration=0.11로 발동빈도 반영')


if __name__ == '__main__':
    main()
