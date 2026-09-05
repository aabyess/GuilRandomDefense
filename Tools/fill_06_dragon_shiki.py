#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""드래곤(h04D)·시키(h04B) levels[0] 채우기 — PM이 원문(.j/.w3a) 직접 대조 후 확정.

드래곤 A0FS: "N절대쿨 ○○" 패턴(21개 동형) — B00J는 A08S(frostarmor 기반, Ufa1=8.5초)의
자기 버프다. 확률이 아니라 "쿨다운 8.5초" — OnHitChance에 cooldown을 얹는 특이 케이스다
(구현담당3이 OnHitChance용 쿨다운 잠금 코드를 넣는 중, 데이터는 먼저 채운다). Dragon_Skill_1의
"×2회"는 다단히트가 아니라 RRD(대상 1회, SingleTarget) + ForGroup(범위 475, Enemies) 두
효과다 — 같은 대상이면 두 번 다 맞는다. levels[1](Dragon_Skill_1_T)은 각 항이 단일/범위
어느 쪽인지 리서치담당 확인 대기라 비워둔다.

시키 A0T4: 리서치담당 1차 조사가 바깥 게이트(능력 보유)만 보고 안쪽 GetRandomInt(1,33)==3을
놓쳤다(.j 18919행) — 진짜 게이트는 확률 1/33이다. 더미 h07R×2 소환이라 효과는 여전히
못 채운다(소환 축 없음, Shiki_ship2 피해값은 리서치담당 조사 중).

⚠️ 드래곤은 지금 당장은(구현담당3의 쿨다운 잠금 코드가 아직 없어서) OnHitChance가
cooldown을 안 읽는다 — triggerChance=1.0이라 잠금 코드가 들어오기 전까지는 매 타
2,000,000이 나간다. PM이 알고 있고 "데이터 먼저 채워도 된다"고 확인했다.
"""
import re

SKILL_DIR = 'Assets/Data/UnitSkills'


def replace_level0_effects(text, new_effects_yaml):
    levels = text.split('\n  - cooldown: ')
    assert len(levels) == 3, "레벨이 2개가 아님"
    level0 = levels[1]
    level0, n = re.subn(
        r'    effects:.*?(?=\n  - cooldown: |\Z)', 'EFFECTS_PLACEHOLDER\n', level0, count=1, flags=re.S,
    )
    if n == 0:
        level0, n = re.subn(r'    effects: \[\]\n?', 'EFFECTS_PLACEHOLDER\n', level0, count=1)
    assert n == 1, "level0 effects 자리를 못 찾음"
    level0 = level0.replace('EFFECTS_PLACEHOLDER\n', new_effects_yaml, 1)
    return '\n  - cooldown: '.join([levels[0], level0, levels[2]])


def append_description(text, note):
    text, n = re.subn(r'^(  description: .*)$', lambda m: m.group(1) + note, text, count=1, flags=re.M)
    assert n == 1, "description 자리를 못 찾음"
    return text


def main():
    # ── 드래곤(h04D) ─────────────────────────────────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작024_h04D.asset'
    text = open(path, encoding='utf-8').read()

    # levels[0]: cooldown 8.5, triggerChance 1.0, range 475(2번 효과의 Enemies 반경 —
    # SingleTarget 효과는 primaryTarget이 이미 있어 range를 안 읽는다), 남은
    # hitCountThreshold(지난 OnHitCount 가설의 잔재)는 0으로 되돌린다.
    text, n = re.subn(r'(\n  - cooldown: )0(\n    triggerChance: )1(\n    range: )0',
                       r'\g<1>8.5\g<2>1.0\g<3>475', text, count=1)
    assert n == 1, "level0 cooldown/triggerChance/range 자리를 못 찾음"
    text, n = re.subn(r'(\n    hitCountThreshold: )160(\n    resetTo: )0\n', r'\g<1>0\g<2>0\n', text, count=1)
    assert n == 1, "level0 hitCountThreshold 자리를 못 찾음"

    effects = (
        "    effects:\n"
        "    - kind: 0\n"
        "      basis: 0\n"
        "      target: 3\n"
        "      damageType: 1\n"
        "      attackType: 1\n"
        "      multiplier: 1000000.0\n"
        "      bonus: 0.0\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
        "    - kind: 0\n"
        "      basis: 0\n"
        "      target: 2\n"
        "      damageType: 1\n"
        "      attackType: 1\n"
        "      multiplier: 1000000.0\n"
        "      bonus: 0.0\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
    )
    text = replace_level0_effects(text, effects)
    text = append_description(
        text,
        " 2026-09-05 재정정(PM, .j/.w3a 직접 대조): 「버프 B00J 미보유」는 확률 게이트가 "
        "아니라 쿨다운이었다 — B00J=A08S(5절대쿨 드래곤, frostarmor 기반 Ufa1=8.5초)의 "
        "자기 잠금 버프. OnHitChance에 cooldown=8.5를 얹는 특이 케이스(구현담당3이 "
        "OnHitChance용 잠금 코드 작업 중 — 그 전까지는 triggerChance=1.0이라 매 타 발동한다). "
        "Dragon_Skill_1의 「×2회」는 다단히트가 아니라 RRD(SingleTarget 1회) + "
        "ForGroup(range 475, Enemies 1회) — 같은 대상이면 둘 다 맞는다. levels[1]"
        "(Dragon_Skill_1_T, 1,250,000+최대체력×1.50+최대체력×0.02)은 각 항의 단일/범위 "
        "구분이 아직 안 나와 비워둔다."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('드래곤(h04D) — OnHitChance, cooldown 8.5, 효과 2개(SingleTarget+Enemies475)')

    # ── 시키(h04B) ───────────────────────────────────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작025_h04B.asset'
    text = open(path, encoding='utf-8').read()
    text, n = re.subn(r'(\n    triggerChance: )1\n', r'\g<1>0.0303\n', text, count=1)
    assert n == 1, "triggerChance 자리를 못 찾음"
    text = append_description(
        text,
        " 2026-09-05 재정정(PM, .j 18919행 직접 대조): 1차 조사가 바깥 게이트(능력 보유)만 "
        "보고 안쪽 GetRandomInt(1,33)==3을 놓쳤다 — 진짜 게이트는 확률 1/33"
        "(triggerChance=0.0303)이다. effects는 여전히 비워둔다 — 더미 h07R 2기(2.25초, "
        "attack 명령) 소환이라 소환 축이 없고, 피해값(Shiki_ship2 트리거)은 리서치담당 조사 중."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('시키(h04B) — OnHitChance, triggerChance 0.0303(1/33), effects 계속 비움')


if __name__ == '__main__':
    main()
