#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""2차 채널(122종) 이미 배정된 효과의 attackType/damageType을 ORIGINAL_DAMAGE_TYPING.csv로 정정.

정책(PM 지시, 2026-09-05): attackType은 NORMAL→Spells(7)·MELEE→Normal(1)·나머지 1:1,
damageType은 UNIVERSAL→AP(2)·NORMAL→AD(1). MELEE 예외 18건(h067·h084/h086/h042/h085·
h026·h03E·h05W·h05I·h08H)은 이미 Normal이면 그대로 둔다.

## 대응 방법
generate_unit_skill_damage_effects.py의 배정 로직(등급별 랭킹→1:1 매칭)을 그대로
다시 돌려서(파일은 안 씀) 각 대상 유닛이 정확히 어느 CSV 행(유닛ID·계산식)을
받았는지 재구성한다 — 이게 그 스크립트가 실제로 만든 것과 100% 같은 순서다
(입력 파일이 그대로면 결정적이다). 그 (유닛ID, 계산식) 쌍을 ORIGINAL_DAMAGE_TYPING.csv에서
**유닛ID로 좁힌 뒤 계산식 접두어로** 찾는다 — 트리거 이름 표기가 두 조사마다 달라서
(예: "Trig_Sengoku_Attack" vs "Sengoku_Attack") 트리거명 매칭은 안 쓴다.

⚠️ 매칭이 안 되거나 여럿 걸리면 절대 추측하지 않고 목록에 남긴다("대충 맞춰 넣기가
제일 나쁘다" — PM 지시).

⚠️ "더미 스톡필드"로 넣은 값(Wrs1·Blo1 등)은 이 표의 대상이 아니다(RRD 호출이
아니라 오브젝트에디터 필드 상속이라 채널 자체가 다르다) — 계산식이
"필드코드=값"(예: "Wrs1=427500") 형태인 행은 애초에 매칭 시도조차 하지 않고
"표 대상 아님"으로 분류한다.
"""
import csv
import glob
import re
import sys

sys.path.insert(0, 'Tools')
import generate_unit_skill_damage_effects as gen  # noqa: E402

SKILL_DIR = 'Assets/Data/UnitSkills'
TYPING_CSV = 'Docs/reference/ORIGINAL_DAMAGE_TYPING.csv'

SPELLS = 7
NORMAL_AT = 1
AP = 2
AD = 1

MELEE_EXCEPTION_UNITS = {'h067', 'h084', 'h086', 'h042', 'h085', 'h026', 'h03E', 'h05W', 'h05I', 'h08H'}

DUMMY_FIELD_PATTERN = re.compile(r'^[A-Za-z][A-Za-z0-9]*\d?=')


def load_typing_table():
    with open(TYPING_CSV, encoding='utf-8') as f:
        rows = list(csv.DictReader(f))
    by_unit = {}
    for r in rows:
        uid = r['유닛ID'].strip()
        if not uid:
            continue
        by_unit.setdefault(uid, []).append(r)
    return by_unit


def find_typing_match(typing_by_unit, unit_id, calc):
    candidates = typing_by_unit.get(unit_id, [])
    calc = calc.strip()
    matches = [r for r in candidates if calc.startswith(r['계산식앞'].strip()) or r['계산식앞'].strip().startswith(calc)]
    return matches


def policy_attack_damage(attack_type_str, damage_type_str, unit_id):
    """returns (new_attack_type_int, new_damage_type_int) or None if no change needed for that axis."""
    ATTACK_MAP = {
        'NORMAL': SPELLS, 'MELEE': NORMAL_AT, 'SIEGE': 3, 'HERO': 4, 'CHAOS': 5, 'MAGIC': 6, 'PIERCE': 2,
    }
    if attack_type_str == 'MELEE' and unit_id in MELEE_EXCEPTION_UNITS:
        new_attack = NORMAL_AT  # 예외 — 이미 Normal이면 그대로(변화 없음으로 처리)
    else:
        new_attack = ATTACK_MAP.get(attack_type_str)

    if damage_type_str == 'UNIVERSAL':
        new_damage = AP
    elif damage_type_str == 'NORMAL':
        new_damage = AD
    else:
        new_damage = None  # ENHANCED/UNKNOWN — 안 건드림

    return new_attack, new_damage


def patch_effect_block(block, attack_type, damage_type):
    if attack_type is not None:
        block, n = re.subn(r'(\n      attackType: )\d+', rf'\g<1>{attack_type}', block, count=1)
        assert n == 1
    if damage_type is not None:
        block, n = re.subn(r'(\n      damageType: )\d+', rf'\g<1>{damage_type}', block, count=1)
        assert n == 1
    return block


def get_effect_blocks(level_text):
    return re.findall(r'    - kind: \d\n(?:      .*\n)+', level_text)


def replace_nth_effect_block(text, level_index, effect_pos, new_block):
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    blocks = get_effect_blocks(level)
    old_block = blocks[effect_pos]
    level = level.replace(old_block, new_block, 1)
    levels[1 + level_index] = level
    return '\n  - cooldown: '.join(levels)


def get_nth_effect_block(text, level_index, effect_pos):
    levels = text.split('\n  - cooldown: ')
    level = levels[1 + level_index]
    blocks = get_effect_blocks(level)
    return blocks[effect_pos]


def append_description(text, note):
    text, n = re.subn(r'^(  description: .*)$', lambda m: m.group(1) + note, text, count=1, flags=re.M)
    assert n == 1
    return text


def cleanup(path, text):
    text = re.sub(r'\n\n+', '\n', text)
    if not text.endswith('\n'):
        text += '\n'
    open(path, 'w', encoding='utf-8').write(text)


def reconstruct_assignment_plan():
    """generate_unit_skill_damage_effects.py의 배정을 파일 안 건드리고 다시 계산한다."""
    by_grade, _skipped = gen.load_csv()
    roster = gen.load_roster()

    plan = []  # (unit_dict, row, effect_count)
    for gname, genum in gen.GRADE_ENUM.items():
        ability_rows = by_grade.get(gname, [])
        eligible = sorted(
            [u for u in roster if u['grade'] == genum and not u['claimed']],
            key=lambda u: -u['dps'],
        )
        n = min(len(ability_rows), len(eligible))
        for i in range(n):
            unit = eligible[i]
            row = dict(ability_rows[i])
            row['_attackType'] = unit['attack_type']
            effects = gen.extract_effects(row)
            plan.append((unit, row, len(effects)))
    return plan


def main():
    typing_by_unit = load_typing_table()
    plan = reconstruct_assignment_plan()

    changed_files = {}
    stats = dict(attack_changed=0, damage_ad_to_ap=0, no_match=0, not_typing_target=0, unchanged_ok=0)
    no_match_report = []

    for unit, row, effect_count in plan:
        unit_id = row['유닛ID']
        calc = row['계산식'].strip()

        if DUMMY_FIELD_PATTERN.match(calc):
            stats['not_typing_target'] += effect_count
            continue

        matches = find_typing_match(typing_by_unit, unit_id, calc)
        # 계산식이 같은 값으로 두 번 이상 걸려도, 그 후보들이 전부 같은
        # (공격타입,피해타입)이면 실제로는 모호하지 않다 — 결과가 갈릴 때만 보고한다.
        distinct_outcomes = {(m['공격타입'], m['피해타입']) for m in matches}
        if len(matches) == 0 or len(distinct_outcomes) != 1:
            stats['no_match'] += effect_count
            no_match_report.append((unit['base'], unit_id, calc[:50], len(matches)))
            continue

        typing_row = matches[0]
        new_attack, new_damage = policy_attack_damage(
            typing_row['공격타입'], typing_row['피해타입'], unit_id,
        )

        path = f"{SKILL_DIR}/SkillData_원작능력_{unit['base']}.asset"
        text = changed_files.get(path) or open(path, encoding='utf-8').read()

        # ⚠️ has_pass1을 지금 다시 계산하면 못 믿는다 — pass2가 새로 만든 파일도 pass1과
        # 똑같은 이름 규칙(SkillData_원작능력_*)을 쓰므로, "파일이 있다"와 "pass1이
        # 만들었다"가 지금 시점엔 구분이 안 된다. 대신 pass2는 항상 그 파일의 effects
        # 리스트 "맨 뒤"에 붙였다(새로 만들 때든 기존에 이어붙일 때든) — 그래서 뒤에서부터
        # effect_count개를 그 유닛의 pass2 몫으로 삼는다.
        total_blocks = len(get_effect_blocks(text.split('\n  - cooldown: ')[1]))
        offset = total_blocks - effect_count
        for k in range(effect_count):
            pos = offset + k
            old_block = get_nth_effect_block(text, 0, pos)
            new_block = patch_effect_block(old_block, new_attack, new_damage)
            if new_block != old_block:
                text = replace_nth_effect_block(text, 0, pos, new_block)
                stats['attack_changed'] += 1 if new_attack is not None else 0
                stats['damage_ad_to_ap'] += 1 if new_damage == AP else 0
            else:
                stats['unchanged_ok'] += 1

        changed_files[path] = text

    marker_note = (
        " 2026-09-05 타입 정정(ORIGINAL_DAMAGE_TYPING.csv, 60b9797): 원작 ATTACK_TYPE_NORMAL은 "
        "이름과 달리 Spells 행이고 DAMAGE_TYPE_UNIVERSAL은 방어력을 뚫는다(우리 AP 대응) — "
        "generate_unit_skill_damage_effects.py의 배정을 재계산해 유닛ID+계산식으로 표와 "
        "대조, attackType/damageType을 정정했다."
    )
    for path, text in changed_files.items():
        text = append_description(text, marker_note)
        cleanup(path, text)

    print(f"파일 {len(changed_files)}개 수정")
    print(f"attackType 변경: {stats['attack_changed']}건")
    print(f"damageType AD→AP 변경: {stats['damage_ad_to_ap']}건")
    print(f"표 대상 아님(더미 스톡필드): {stats['not_typing_target']}건")
    print(f"대응 실패(0개 또는 복수 매칭): {stats['no_match']}건")
    print(f"매칭됐지만 변화 없음(이미 정확): {stats['unchanged_ok']}건")
    if no_match_report:
        print("\n대응 실패 목록:")
        for base, uid, calc, nmatch in no_match_report:
            print(f"  {base:35s} {uid:6s} 매칭 {nmatch}개  계산식: {calc}")


if __name__ == '__main__':
    main()
