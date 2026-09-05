#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""TRAIT_GATE_BLOCKED4.md 정정 반영 — 트리거를 최상위 if 블록 단위로 다시 자른 결과.

PM 지시(2026-09-05) — "같은 트리거 안에 있으니 같은 스킬"로 읽은 게 원인이었다.
드래곤·제트·시키·핸콕·센고쿠 5종 전부 그 오독의 영향을 받았다:

- **드래곤(h04D)**: 되돌린다. A08V(내가 넣었던 값)는 A0FS 본체가 아니라 1/10 블록의
  별개 스킬이었다. A0FS의 진짜 게이트는 "버프 B00J 미보유"라 우리 스키마(확률·쿨다운·
  게이지)로 표현할 방법이 없다 — 값은 있지만(레벨1: 1,000,000×2회) 게이트를 못 담아
  비워둔다.
- **센고쿠(h04E)**: 게이트(74타 게이지)는 맞았지만 효과가 틀렸다 — 내가 넣었던 3개
  (TargetCurrentHpPercent 2개 + 더미 A0PL)는 전부 "GetRandomInt(1,10)==6" 블록의
  별개 스킬이었다. 진짜 A0D8 본체는 Seongoku_Skill_4 = 500,000 + 2,000,000(Flat).
- **핸콕(h05C)**: 게이트(175타 게이지)는 맞았지만 효과가 틀렸다 — 더미 e0E3(Blo1=-0.15)는
  "1/15==6" 블록의 별개 스킬이었다. 진짜 A0IN 본체(hancock_skill_Mana)의 수치는 아직
  못 찾았다 — 지어내지 않고 비워둔다.
- **제트(h04G)**: 이미 비어 있었다(대응 미해소로 안 채웠음) — 이번에 게이트가
  "버프 B00M 보유"로 확정됐지만 여전히 우리 스키마 밖이라 그대로 비워두고, 실제 값
  (레벨×100,000+200,000, 레벨1=300,000/레벨2=400,000, +고정 1,250,000)만 description에
  남긴다.
- **시키(h04B)**: 이미 비어 있었다 — 게이트가 "능력 A0T4 레벨==1"(보유 자체)로 확정됐지만
  더미 h07R의 수치가 리서치담당 조사에도 없어 값 자체가 없다. 게이트·구조만 남긴다.

버프 보유·능력 보유 게이트는 SkillTriggerType에 없다 — 늘릴지는 사장님 판단 대기
(PM 지시, 2026-09-05). 새 축을 여기서 지어내지 않는다.
"""
import re

SKILL_DIR = 'Assets/Data/UnitSkills'

CORRECTION_NOTE = ' 2026-09-05 정정(TRAIT_GATE_BLOCKED4.md): 트리거를 최상위 if 블록 단위로 다시 잘라보니'


def render_effect(basis, mult, bonus, attack_type):
    return (
        "    - kind: 0\n"
        f"      basis: {basis}\n"
        "      target: 3\n"
        "      damageType: 1\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(mult, 4)}\n"
        f"      bonus: {round(bonus, 4)}\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
    )


def replace_level0_effects(text, new_effects_yaml):
    """levels[0]의 effects 블록(비어있든 채워져있든)을 통째로 new_effects_yaml로 바꾼다."""
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
    # ── 드래곤(h04D) — 되돌린다: OnHitChance(0)로, effects 비움 ──────────────
    path = f'{SKILL_DIR}/SkillData_원작024_h04D.asset'
    text = open(path, encoding='utf-8').read()
    text, n = re.subn(r'^  triggerType: 3$', '  triggerType: 0', text, count=1, flags=re.M)
    assert n == 1
    text = replace_level0_effects(text, '    effects: []\n')
    text = append_description(
        text,
        f"{CORRECTION_NOTE} A0FS의 게이트는 게이지가 아니라 「버프 B00J 미보유」였다 — "
        "OnHitCount로 채웠던 값(더미 A08V, 250,000)은 「GetRandomInt(1,10)==6」 블록의 "
        "별개 스킬(A08V 불멸 드래곤스턴2)이었다. 되돌린다. 실제 A0FS 값(레벨1: "
        "Dragon_Skill_1 = 1,000,000 × 2회 [NORMAL/UNIVERSAL], 레벨2: Dragon_Skill_1_T = "
        "1,250,000 + 최대체력×1.50 + 최대체력×0.02)은 버프 보유 여부를 읽을 축이 "
        "우리에 없어 옮기지 못한다 — 새 축을 지어내지 않고 여기 기록만 남긴다."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('드래곤(h04D) — 되돌림, effects 비움')

    # ── 센고쿠(h04E) — 게이트는 유지, 효과만 교체 ────────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작021_h04E.asset'
    text = open(path, encoding='utf-8').read()
    unit_text = open('Assets/Data/Units/Roster/불멸_이이삭.asset', encoding='utf-8').read()
    fallback_at = int(re.search(r'^  attackType: (\d+)', unit_text, re.M).group(1))
    new_effects = (
        render_effect(0, 500000.0, 0.0, fallback_at)
        + render_effect(0, 2000000.0, 0.0, fallback_at)
    )
    text = replace_level0_effects(text, new_effects)
    text = append_description(
        text,
        f"{CORRECTION_NOTE} 기존에 넣었던 효과 3개(TargetCurrentHpPercent 2개 + 더미 A0PL)는 "
        "「GetRandomInt(1,10)==6」 블록의 별개 스킬(Sengoku_Skill_Item)이었다 — A0D8 본체가 "
        "아니었다. 진짜 A0D8 본체(Seongoku_Skill_4)는 500,000 + 2,000,000(Flat, 공격타입 "
        "미확인이라 이 유닛 자신의 평타 타입으로 대신함)으로 교체한다. 게이트(74타 게이지, "
        "리셋1)는 그대로 맞았다."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('센고쿠(h04E) — 효과 교체(500,000 + 2,000,000)')

    # ── 핸콕(h05C) — 게이트는 유지, 효과는 비움(진짜 값 아직 없음) ───────────
    path = f'{SKILL_DIR}/SkillData_원작028_h05C.asset'
    text = open(path, encoding='utf-8').read()
    text = replace_level0_effects(text, '    effects: []\n')
    text = append_description(
        text,
        f"{CORRECTION_NOTE} 기존에 넣었던 더미 e0E3(Blo1=-0.15)는 「1/15==6」 블록의 "
        "별개 스킬이었다 — A0IN 본체가 아니었다. 게이트(175타 게이지)는 A0IN이 맞지만, "
        "진짜 본체(hancock_skill_Mana, +arrow_h2·hancock_skill_9 동시 실행)의 수치는 "
        "아직 못 찾아 비운다 — 지어내지 않는다."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('핸콕(h05C) — 효과 제거(진짜 값 미확보), 게이트만 유지')

    # ── 제트(h04G) — 이미 비어 있음, 근거 노트만 추가 ────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작022_h04G.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text,
        f"{CORRECTION_NOTE} A09E(차지 버스터)의 게이트는 확률이 아니라 「버프 B00M 보유」다"
        "(대상 최대체력×0.025는 A09E가 아니라 「대상이 버프 B06B 보유」 게이트의 별개 스킬"
        "이었다 — 이전에 검토했던 값). A09E 실값: 레벨×100,000+200,000(레벨1=300,000, "
        "레벨2=400,000) + 고정 1,250,000. 버프 보유 축이 우리에 없어 못 채운다."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('제트(h04G) — 변경 없음(계속 비움), 근거 노트만 추가')

    # ── 시키(h04B) — 이미 비어 있음, 근거 노트만 추가 ────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작025_h04B.asset'
    text = open(path, encoding='utf-8').read()
    text = append_description(
        text,
        f"{CORRECTION_NOTE} A0T4(!함대)의 게이트는 확률이 아니라 「능력 A0T4 레벨==1(보유 "
        "자체)」다 — 더미 h07R ×2가 본체이지만 리서치담당 조사에도 수치가 없어 값 자체가 "
        "없다. 게이트·구조만 남기고 못 채운다."
    )
    open(path, 'w', encoding='utf-8').write(text)
    print('시키(h04B) — 변경 없음(계속 비움), 근거 노트만 추가')


if __name__ == '__main__':
    main()
