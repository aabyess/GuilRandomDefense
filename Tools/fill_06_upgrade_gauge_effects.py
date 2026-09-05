#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""06번① levels[0] — OnHitCount(게이지형) 확정/추정 5종을 채운다.

PM 지시(2026-09-05) — ORIGINAL_TRAIT_GATE_MAPPING.csv·ORIGINAL_TRAIT_GAUGE_RESET.csv
확인 후: 드래곤·센고쿠는 확정(A08V=A0FS, A0D8 특성 스킬 둘 다 게이지형으로 판명 —
앞서 채우려던 확률형 3개는 "다른 스킬 것"이었다), 거프·핸콕·레일리는 추정(그 능력을
읽는 트리거가 하나뿐이라 대상은 맞지만 완전히 닫힌 건 아님). 제트(대응 미해소)·시키
(대응 못 정함)는 이번에도 뺀다.

리셋값(GAUGE_RESET.csv) — 체력(LIFE) 게이지는 0으로 두면 유닛이 죽어서 1로 리셋한다
(실주기 = 임계−1), 마나(MANA) 게이지는 0 그대로.

레일리(h049)는 게이트만 채우고 effects는 비워둔다 — Ucs1~4 필드뜻이 여전히 미확인이라
피해 효과를 못 정한다(2026-09-05 감사 원칙: 지어내지 않는다).

triggerType은 SkillData 전체 필드(레벨별이 아니다) — OnHitChance(0)에서 OnHitCount(3)로
바꾼다. hitCountThreshold/resetTo는 SkillLevel 필드라 각 level에 넣는다. levels[1]
(승급분)은 이번 범위 밖이라 threshold/resetTo만 0으로 맞춰두고(Unity가 실제로 그렇게
직렬화한다) effects는 그대로 비워둔다.
"""
import re

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'

ON_HIT_COUNT = 3

# basis: 0=Flat, 1=TargetMaxHpPercent, 2=TargetCurrentHpPercent
ITEMS = [
    dict(
        skill_file='SkillData_원작021_h04E.asset',
        unit_file='불멸_이이삭.asset',
        hit_count_threshold=75, reset_to=1,  # LIFE 게이지, 체력형이라 1로 리셋(실주기 74타)
        confidence='확정(ORIGINAL_TRAIT_GATE_MAPPING.csv — 특성 스킬 Seongoku_Skill_4는 74타 게이지. '
                   '앞서 검토했던 확률 게이트 3개(1/10·1/20·1/12)는 이 능력이 아니라 다른 스킬 것이었다.)',
        effects=[
            dict(basis=2, mult=0.02, bonus=400000.0, attack_type=1, note=''),
            dict(basis=2, mult=0.015, bonus=300000.0, attack_type=1, note=''),
            dict(basis=0, mult=427500.0, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Wrs1=427500) 값 그대로 — 필드뜻 미확인이라 피해가 아닐 수 있다.'),
        ],
    ),
    dict(
        skill_file='SkillData_원작022_h04G.asset',
        unit_file='불멸_박은석.asset',
        skip='제트는 대응 미해소 — 최대체력×0.025(Trig_Z_Attack) vs 레벨×100,000+200,000'
             '(TRAIT_LEVEL2_SOURCES.md) 두 문서가 다른 식을 적어 리서치담당이 확인 중.',
    ),
    dict(
        skill_file='SkillData_원작023_h04C.asset',
        unit_file='불멸_이승우.asset',
        hit_count_threshold=160, reset_to=0,  # MANA 게이지
        confidence='추정(A0GY를 읽는 트리거가 Garp_Mana2뿐 — ORIGINAL_TRAIT_GATE_MAPPING.csv)',
        effects=[
            dict(basis=0, mult=0.45, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Blo1=0.45) 값 그대로 — 값이 작아 피해가 아니라 배율성 버프/디버프였을 가능성이 있다(필드뜻 미확인).'),
        ],
    ),
    dict(
        skill_file='SkillData_원작024_h04D.asset',
        unit_file='불멸_정준영.asset',
        hit_count_threshold=160, reset_to=0,  # MANA 게이지
        confidence='확정(A08V=A0FS 본체 확인됨, ORIGINAL_TRAIT_GATE_MAPPING.csv — Dragon_Skill_Mana 160타)',
        effects=[
            dict(basis=0, mult=250000.0, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Wrs1=250000) 값 그대로 — 필드뜻 미확인이라 피해가 아닐 수 있다.'),
        ],
    ),
    dict(
        skill_file='SkillData_원작025_h04B.asset',
        unit_file='불멸_신지우.asset',
        skip='시키는 대응 못 정함(ORIGINAL_TRAIT_GATE_MAPPING.csv 미대응) — 능력교체형이라 게이트가 다른 곳에 있다.',
    ),
    dict(
        skill_file='SkillData_원작026_h049.asset',
        unit_file='불멸_정윤식.asset',
        hit_count_threshold=115, reset_to=0,  # MANA 게이지
        confidence='추정(A0ES를 읽는 트리거가 LaillySkill3뿐 — ORIGINAL_TRAIT_GATE_MAPPING.csv). '
                   '게이트만 채우고 효과는 비워둔다 — 더미 스톡필드 4개(Ucs1~4) 중 어느 게 '
                   '피해량인지 여전히 확정 근거가 없다(지어내지 않는다).',
        effects=[],  # 의도적으로 비움
    ),
    dict(
        skill_file='SkillData_원작028_h05C.asset',
        unit_file='영원_조세민.asset',
        hit_count_threshold=175, reset_to=0,  # MANA 게이지
        confidence='추정(A0IN을 읽는 트리거가 이것뿐 — ORIGINAL_TRAIT_GATE_MAPPING.csv). '
                   '⚠️⚠️ 레벨업 시 간격이 0.03+0.05/레벨로 작아지는 게 강화다(역방향 스케일) — '
                   'levels[1] 채울 때 "레벨업=더 큰 값"으로 정렬하면 뒤집힌다. 이번엔 levels[0]만 다룬다.',
        effects=[
            dict(basis=0, mult=-0.15, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Blo1=-0.15) 값 그대로 — 값이 작고 음수라 피해가 아니라 배율성 디버프였을 가능성이 있다(필드뜻 미확인).'),
        ],
    ),
]


def render_effect(e, fallback_attack_type):
    attack_type = e['attack_type'] if e['attack_type'] is not None else fallback_attack_type
    fallback_note = '' if e['attack_type'] is not None else ' 공격타입 원본이 [미확인]이라 이 유닛 자신의 평타 타입으로 대신했다.'
    return (
        "    - kind: 0\n"
        f"      basis: {e['basis']}\n"
        "      target: 3\n"
        "      damageType: 1\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(e['mult'], 4)}\n"
        f"      bonus: {round(e['bonus'], 4)}\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
    ), e['note'] + fallback_note


def main():
    filled = 0
    skipped = []

    for item in ITEMS:
        if 'skip' in item:
            skipped.append((item['skill_file'], item['skip']))
            continue

        skill_path = f"{SKILL_DIR}/{item['skill_file']}"
        unit_path = f"{ROSTER_DIR}/{item['unit_file']}"

        unit_text = open(unit_path, encoding='utf-8').read()
        at_match = re.search(r'^  attackType: (\d+)', unit_text, re.M)
        fallback_attack_type = int(at_match.group(1))

        text = open(skill_path, encoding='utf-8').read()

        # 1) triggerType: OnHitChance(0) → OnHitCount(3). SkillData 전체 필드다.
        text, n = re.subn(r'^  triggerType: 0$', f'  triggerType: {ON_HIT_COUNT}', text, count=1, flags=re.M)
        assert n == 1, f"{skill_path}: triggerType 자리를 못 찾음"

        # 2) description에 게이지 정정 근거를 남긴다.
        note = (
            f" 2026-09-05 정정: 확률형이 아니라 게이지형(OnHitCount)이었다 — {item['confidence']}"
        )
        text, n = re.subn(r'^(  description: .*)$', lambda m: m.group(1) + note, text, count=1, flags=re.M)
        assert n == 1, f"{skill_path}: description 자리를 못 찾음"

        # 3) 두 레벨 모두에 hitCountThreshold/resetTo를 끼워 넣는다(Unity가 실제로 그렇게
        #    직렬화한다) — level0만 실제 값, level1은 0(승급분은 이번 범위 밖이라 나중에).
        levels = text.split('\n  - cooldown: ')
        assert len(levels) in (2, 3), f"{skill_path}: 레벨 개수가 예상과 다름({len(levels)-1}개)"

        new_levels = [levels[0]]
        for i, level_body in enumerate(levels[1:]):
            threshold = item['hit_count_threshold'] if i == 0 else 0
            reset_to = item['reset_to'] if i == 0 else 0
            level_body, n = re.subn(
                r'(\n    range: 0)\n',
                rf'\g<1>\n    hitCountThreshold: {threshold}\n    resetTo: {reset_to}\n',
                level_body, count=1,
            )
            assert n == 1, f"{skill_path}: level {i} range 자리를 못 찾음"

            if i == 0 and item['effects']:
                blocks = []
                for e in item['effects']:
                    block, _ = render_effect(e, fallback_attack_type)
                    blocks.append(block)
                level_body, n = re.subn(r'    effects: \[\]', '    effects:\n' + ''.join(blocks), level_body, count=1)
                assert n == 1, f"{skill_path}: level {i} effects: [] 자리를 못 찾음"

            new_levels.append(level_body)

        text = '\n  - cooldown: '.join(new_levels)
        open(skill_path, 'w', encoding='utf-8').write(text)
        filled += 1
        eff_desc = f"효과 {len(item['effects'])}개" if item['effects'] else "효과 없음(게이트만)"
        print(f"{item['skill_file']} — OnHitCount, threshold={item['hit_count_threshold']}, "
              f"resetTo={item['reset_to']}, {eff_desc}")

    print(f"\n총 {filled}종 채움.")
    for name, reason in skipped:
        print(f"건너뜀: {name} — {reason}")


if __name__ == '__main__':
    main()
