#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""핸콕 levels[0]·드래곤 levels[1]·시키 근사·제트 levels[0/1]·거프 되돌림.

PM이 리서치담당 회신(3e55dc5) + 원문 대조로 확정한 값을 그대로 옮긴다. 관례:
UNIVERSAL→AD(1)/그 행 공격타입, CHAOS/NORMAL→AD/Chaos(5), HERO/NORMAL→AD/Hero(4).

- 핸콕(h05C): 게이트(175타)는 그대로, effects 3개(Flat 300,000 Enemies760 ·
  TargetMaxHpPercent×0.16 SingleTarget · ResearchLevel×30,000+360,000 Enemies760/Chaos).
- 드래곤(h04D) levels[1]: **대체**다 — 레벨2면 레벨1 효과가 안 나간다(원문 if/else).
  cooldown 8.5 동일, range 425. %체력은 EnemyData.takesPercentDamage 게이트가 보스에서
  런타임에 막으므로 그대로 채운다.
- 시키(h04B): Shiki_ship2엔 피해가 없고 소환되는 h07R 자체 평타(200,000/0.75초/2.25초
  생존/2기)가 피해원 — 소환 축이 없어 "2기×3타=6타"를 hitCount 6으로 근사, duration은
  실제 생존시간(2.25초)에 맞춘다. attackType은 h07R의 ua1t가 비어 스톡 상속이라 Normal로
  근사 — description에 근사 명시.
- 제트(h04G): 게이트가 확률이 아니라 "버프 B00M 보유(0.6초 창)"였는데, 그 창을 여는
  것 자체가 평타 10회 중 1회(1/10)라 "그 뒤 0.6초 안의 평타 수"로 유효확률을 근사한다.
  Δ(제트를 받는 우리 유닛의 공격 간격) = 1/attackSpeed. Δ>0.6이면 정수 낙수(floor)가 0이
  되므로 기대값(0.1×0.6/Δ)으로 대체 — 실제로 우리 유닛(박은석) Δ≈2.25초라 이 경로를 탄다.
  levels[0]/[1] 둘 다 이 확률을 쓴다(창 길이는 레벨과 무관).
- 거프(h04C): Blo1=0.45(더미 A08W)는 별개 스킬이었다 — 되돌린다(반쯤 의심했던 게 맞았다).
  게이트(OnHitCount 160/0)는 유지, 진짜 값(Garp_Mana2 RRD 3개)은 레벨1/2 대응과 realD가
  뭔지 리서치담당 확인 대기라 effects는 비운다.
"""
import math
import re

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'


def effect_yaml(basis, mult, bonus, target, attack_type, damage_type=1, hit_count=1):
    return (
        "    - kind: 0\n"
        f"      basis: {basis}\n"
        f"      target: {target}\n"
        f"      damageType: {damage_type}\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(mult, 4)}\n"
        f"      bonus: {round(bonus, 4)}\n"
        "      chance: 1\n"
        f"      hitCount: {hit_count}\n"
        "      duration: 0\n"
    )


def set_level_field(text, level_index, field, value):
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    level, n = re.subn(rf'(^|\n)( +){field}: [^\n]*', lambda m: f'{m.group(1)}{m.group(2)}{field}: {value}',
                        level, count=1)
    if n == 0 and field == 'cooldown':
        # level 문자열 자체가 "\n  - cooldown: " 뒤부터 시작해 첫 필드값만 남아있다.
        level, n = re.subn(r'^[^\n]*', str(value), level, count=1)
    assert n == 1, f"{field} 자리를 못 찾음"
    levels[1 + level_index] = level
    return '\n  - cooldown: '.join(levels)


def replace_level_effects(text, level_index, new_effect_blocks):
    """new_effect_blocks: '' for empty, or concatenated effect_yaml() blocks (no 'effects:' header)."""
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
    # ── 핸콕(h05C) — levels[0] effects 3개, range 760 ──────────────────────
    path = f'{SKILL_DIR}/SkillData_원작028_h05C.asset'
    text = open(path, encoding='utf-8').read()
    text = set_level_field(text, 0, 'range', 760)
    effects = (
        effect_yaml(0, 300000.0, 0.0, target=2, attack_type=1)       # Flat, Enemies, Normal/AD
        + effect_yaml(1, 0.16, 0.0, target=3, attack_type=1)         # TargetMaxHpPercent, SingleTarget
        + effect_yaml(4, 30000.0, 360000.0, target=2, attack_type=5)  # ResearchLevel, Enemies, Chaos/AD
    )
    text = replace_level_effects(text, 0, effects)
    text = append_description(
        text,
        " 2026-09-05 재정정(리서치담당 3e55dc5 + PM 원문 대조): 진짜 A0IN 본체"
        "(hancock_skill_Mana) 값 확보 — Flat 300,000(Enemies range 760) + "
        "TargetMaxHpPercent×0.16(SingleTarget) + ResearchLevel×30,000+360,000"
        "(Enemies range 760, R01T 연구횟수 비례, CHAOS/NORMAL→AD/Chaos 관례). "
        "range 760은 ForGroup 반경. levels[1] 역스케일은 계속 보류."
    )
    cleanup(path, text)
    print('핸콕(h05C) — levels[0] effects 3개 채움')

    # ── 드래곤(h04D) — levels[1] 전면 교체(레벨1 효과 대체, 추가 아님) ───────
    path = f'{SKILL_DIR}/SkillData_원작024_h04D.asset'
    text = open(path, encoding='utf-8').read()
    text = set_level_field(text, 1, 'cooldown', 8.5)
    text = set_level_field(text, 1, 'triggerChance', 1.0)
    text = set_level_field(text, 1, 'range', 425)
    effects = (
        effect_yaml(0, 1250000.0, 0.0, target=2, attack_type=1)   # Flat, Enemies
        + effect_yaml(1, 1.50, 0.0, target=3, attack_type=1)      # TargetMaxHpPercent, SingleTarget
        + effect_yaml(2, 0.02, 0.0, target=3, attack_type=1)      # TargetCurrentHpPercent, SingleTarget
    )
    text = replace_level_effects(text, 1, effects)
    text = append_description(
        text,
        " 2026-09-05 재정정: levels[1](Dragon_Skill_1_T)은 원문 if/else 구조상 "
        "레벨1 효과(1,000,000×2회)를 대체한다(추가 아님) — Flat 1,250,000(Enemies "
        "range 425) + TargetMaxHpPercent×1.50(SingleTarget) + "
        "TargetCurrentHpPercent×0.02(SingleTarget). cooldown 8.5는 레벨1과 동일. "
        "%체력 효과는 EnemyData.takesPercentDamage 게이트가 보스에서 런타임에 막으므로 "
        "그대로 채운다."
    )
    cleanup(path, text)
    print('드래곤(h04D) — levels[1] 전면 교체')

    # ── 시키(h04B) — 근사 효과 1개 ───────────────────────────────────────────
    path = f'{SKILL_DIR}/SkillData_원작025_h04B.asset'
    text = open(path, encoding='utf-8').read()
    effects = effect_yaml(0, 200000.0, 0.0, target=3, attack_type=1, hit_count=6)
    effects = effects.replace('duration: 0', 'duration: 2.25')
    text = replace_level_effects(text, 0, effects)
    text = append_description(
        text,
        " 2026-09-05 재정정(리서치담당 3e55dc5): Shiki_ship2엔 피해가 0건이고, 진짜 "
        "피해원은 소환되는 더미 h07R 자체 평타(200,000/0.75초 간격/2.25초 생존/2기 — "
        "기당 3타, 합 6타)다. 소환 축이 없어 「2기×3타=6타」를 hitCount=6·duration=2.25로 "
        "근사했다(실제 간격 0.75초와는 다르게 균등 분배된다) — ⚠️ 근사, 정밀하지 않음. "
        "attackType은 h07R의 ua1t 필드가 비어 스톡 상속이라 Normal로 근사."
    )
    cleanup(path, text)
    print('시키(h04B) — 근사 효과 1개(hitCount 6) 채움')

    # ── 제트(h04G) — 창 0.6초를 유효확률로 환산, levels[0]/[1] 둘 다 ─────────
    path = f'{SKILL_DIR}/SkillData_원작022_h04G.asset'
    text = open(path, encoding='utf-8').read()
    unit_text = open(f'{ROSTER_DIR}/불멸_박은석.asset', encoding='utf-8').read()
    attack_speed = float(re.search(r'^  attackSpeed: ([\d.]+)', unit_text, re.M).group(1))
    delta = 1.0 / attack_speed
    window = 0.6
    hits_in_window = math.floor(window / delta)
    if hits_in_window > 0:
        trigger_chance = round(0.1 * hits_in_window, 4)
        formula_note = f"0.1 × floor(0.6/Δ), Δ={delta:.4f}초 → floor(0.6/Δ)={hits_in_window}"
    else:
        trigger_chance = round(0.1 * (window / delta), 4)
        formula_note = (
            f"Δ={delta:.4f}초 > 0.6초라 floor(0.6/Δ)=0 — 기대값 0.1×(0.6/Δ)={trigger_chance}로 대체"
        )

    for level_idx in (0, 1):
        text = set_level_field(text, level_idx, 'triggerChance', trigger_chance)
        text = set_level_field(text, level_idx, 'range', 475)

    effects0 = (
        effect_yaml(0, 300000.0, 0.0, target=2, attack_type=1)
        + effect_yaml(0, 1250000.0, 0.0, target=3, attack_type=1)
    )
    effects1 = (
        effect_yaml(0, 400000.0, 0.0, target=2, attack_type=1)
        + effect_yaml(0, 1250000.0, 0.0, target=3, attack_type=1)
    )
    text = replace_level_effects(text, 0, effects0)
    text = replace_level_effects(text, 1, effects1)
    text = append_description(
        text,
        f" 2026-09-05 재정정(리서치담당 3e55dc5 + PM 원문 대조): A09E의 게이트는 확률이 "
        "아니라 「버프 B00M 보유(0.6초 창, 평타 1/10로 열림)」다 — 창을 연 그 타는 "
        "안 맞고 그 뒤 0.6초 안의 평타가 발동해서, 우리 유닛(박은석)의 타격 간격으로 "
        f"유효확률을 근사했다: {formula_note}. levels[0]=Flat 300,000(Enemies range "
        "475)+Flat 1,250,000(SingleTarget), levels[1]=Flat 400,000(Enemies range "
        "475)+Flat 1,250,000(SingleTarget) — 레벨×100,000+200,000 공식 그대로."
    )
    cleanup(path, text)
    print(f'제트(h04G) — levels[0]/[1] 채움, triggerChance={trigger_chance} ({formula_note})')

    # ── 거프(h04C) — GARP_MANA2_STRUCTURE.md 확정값(레벨1: 즉발 1개, 레벨2: 2개) ─
    path = f'{SKILL_DIR}/SkillData_원작023_h04C.asset'
    text = open(path, encoding='utf-8').read()
    effects0 = effect_yaml(1, 0.05, 6000000.0, target=2, attack_type=4)  # HERO/NORMAL→AD/Hero
    effects1 = (
        effect_yaml(1, 0.05, 6000000.0, target=2, attack_type=4)
        + effect_yaml(1, 0.06, 7250000.0, target=2, attack_type=4)
    )
    text = set_level_field(text, 0, 'range', 650)
    text = set_level_field(text, 1, 'range', 650)
    # levels[1]도 같은 게이지 게이트를 쓴다 — level0에만 threshold를 넣으면 level1은
    # threshold=0인 채로 effects가 채워져 "OnHitCount인데 threshold=0"(매 타 발동) 위험
    # 상태가 된다(check_required_fields.py #9).
    text = set_level_field(text, 1, 'hitCountThreshold', 160)
    text = replace_level_effects(text, 0, effects0)
    text = replace_level_effects(text, 1, effects1)
    text = append_description(
        text,
        " 2026-09-05 재정정(PM 원문 대조): Blo1=0.45(더미 A08W, 7%→e02M 경로)는 "
        "Trig_Garp_Attack 최상위 3블록 중 별개 스킬이었다 — 되돌린다. 진짜 A0GY 본체는 "
        "GARP_MANA2_STRUCTURE.md 확정값(리서치담당 4e726fa): levels[0]=TargetMaxHpPercent"
        "×0.05+6,000,000(Enemies range 650, HERO/NORMAL→AD/Hero, 원작은 1.17초 지연 후 "
        "한 방 — 지연은 시뮬에 무의미해 description만), levels[1]=같은 항 + "
        "TargetMaxHpPercent×0.06+7,250,000(Enemies range 650, 0.05초 뒤 두 번째, 지연 "
        "역시 description만). 원작은 세 식 모두 ×(1+0.03×시전자 버프개수)가 곱해지는데 "
        "그 축이 우리에 없어 ×1.0(버프 0개 기준)으로 생략했다 — 지어내지 않고 명시."
    )
    cleanup(path, text)
    print('거프(h04C) — GARP_MANA2_STRUCTURE.md 값으로 채움(levels[0] 1개, levels[1] 2개)')


if __name__ == '__main__':
    main()
