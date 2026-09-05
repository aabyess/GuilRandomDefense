#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""⑤-0 (CHANNEL_UNIFICATION_DESIGN_2026-09-06.md): generate_unit_ability_skills.py(1채널)가
195건과 같은 방식으로 「행 단위」로 등급 안 순위매칭해서 원작 유닛 하나(uid)의 능력이
여러 개면 그 개수만큼 서로 다른 로스터 유닛에 쪼개지던 버그(뿌리 ⑮ 세 번째 얼굴,
104 uid 중 22개가 능력 2개 이상)를 고친다 — 2채널을 고친 regroup_skill_damage_
by_unit.py(ca753e0)와 같은 패턴: 되돌리기 → uid 단위로 재배정.

## 1단계 — 되돌리기
옛(행 단위) generate_unit_ability_skills.py가 만든 description은 row 하나만의
순수 함수였다(대상 유닛을 안 본다) — legacy_single_row_description(row)로 그
문자열을 정확히 재현해서, 지금 디스크에 있는 SkillData_원작능력_*.asset들의
description과 prefix로 대조한다(재스캔·재시뮬레이션이 아니라 파일 자체가
근거 — 뿌리 ⑱과 같은 사고를 피한다). 매칭되면:
  - effects 블록이 1개뿐이면(2채널이 안 얹은 순수 1채널 파일) 파일을 통째로
    지우고 로스터 skill/skills 참조를 원복한다.
  - 2개 이상이면(2채널이 뒤에 stack) 첫 블록만 잘라내고 description에서 1채널
    몫만 제거해 2채널 몫을 그대로 남긴다.

## 2단계 — uid 단위로 재배정
되돌리기 이후의 로스터 claimed 상태로 generate_unit_ability_skills.main()을
다시 부른다(이제 uid 그룹 단위 배정으로 고쳐져 있다).

## 재실행 규칙
generate_unit_ability_skills.legacy_single_row_description(row)가 만드는
문자열 집합이 서로 겹치면(이론상 있을 수 있는 사고) 안전을 위해 즉시 중단한다
— 잘못 매칭해서 남의 파일을 지우는 것보다 낫다.
"""
import glob
import os
import re
import sys

sys.path.insert(0, 'Tools')
import generate_unit_ability_skills as gen1

OUT_DIR = gen1.OUT_DIR
ROSTER_DIR = gen1.ROSTER_DIR
STACK_MARKER_2CH = '2차 배정(트리거·더미 채널)'


def _all_rows():
    by_grade, _, _ = gen1.load_csv_rows()
    return [r for g in by_grade.values() for r in g]


def _build_desc_lookup():
    """옛(행 단위) description 문자열 집합을 만든다. revert()는 이 문자열이 파일의
    description prefix인지만 보고, 어느 CSV 행이었는지는 안 쓴다(1채널 legacy
    포맷은 대상 유닛을 안 담아 파일 자체로는 재구성 불가) — 그래서 두 행이 우연히
    같은 텍스트를 만들어도(카이도 A0SN 등, 서로 다른 uid가 등급·이름·능력·수치가
    전부 같은 경우) 안전하다: set이라 자연히 합쳐지고, 그래도 판정 결과는 같다."""
    rows = _all_rows()
    return {gen1.legacy_single_row_description(r) for r in rows}


def _remove_guid_from_roster(roster_text, guid):
    # skill 필드에 있으면 비우고, skills 리스트에 있으면 그 줄만 지운다.
    if f'guid: {guid},' in roster_text and re.search(
            rf'^  skill: \{{fileID: 11400000, guid: {guid}[^\n]*\}}$', roster_text, re.M):
        return re.sub(
            rf'^  skill: \{{fileID: 11400000, guid: {guid}[^\n]*\}}$',
            '  skill: {fileID: 0}', roster_text, count=1, flags=re.M)
    new_text = re.sub(rf'^  - \{{fileID: 11400000, guid: {guid}, type: 2\}}\n', '',
                       roster_text, count=1, flags=re.M)
    if new_text == roster_text:
        raise SystemExit(f'로스터에서 guid {guid}를 못 찾음 — skill/skills 어느 쪽에도 없다')
    return new_text


def revert():
    lookup = _build_desc_lookup()
    deleted = 0
    trimmed = 0
    untouched = 0

    for path in sorted(glob.glob(f'{OUT_DIR}/SkillData_원작능력_*.asset')):
        text = open(path, encoding='utf-8').read()
        desc_m = re.search(r'^  description: (.*)$', text, re.M)
        if not desc_m:
            continue
        desc = desc_m.group(1)

        matches = [d for d in lookup if desc.startswith(d)]
        if not matches:
            untouched += 1
            continue
        if len(matches) > 1:
            raise SystemExit(f'{path}: description이 옛 행 여러 개와 동시에 매칭됨 — 중단')
        row_desc = matches[0]

        block_starts = [m.start() for m in re.finditer(r'^    - kind: \d+\n', text, re.M)]
        if not block_starts:
            raise SystemExit(f'{path}: 1채널 마커는 있는데 effects 블록이 없음 — 중단')
        if '\n      basis: 3\n' not in text[block_starts[0]:block_starts[0] + 300]:
            raise SystemExit(f'{path}: 첫 블록이 1채널 서명(basis: 3)이 아님 — 중단')

        meta_text = open(path + '.meta', encoding='utf-8').read()
        guid = re.search(r'guid: (\w+)', meta_text).group(1)

        if len(block_starts) == 1:
            # 순수 1채널 파일 — 통째로 지우고 로스터 참조를 원복한다.
            roster_path = f'{ROSTER_DIR}/{path.split("/")[-1][len("SkillData_원작능력_"):-len(".asset")]}.asset'
            roster_text = open(roster_path, encoding='utf-8').read()
            roster_text = _remove_guid_from_roster(roster_text, guid)
            open(roster_path, 'w', encoding='utf-8').write(roster_text)
            os.remove(path)
            os.remove(path + '.meta')
            deleted += 1
        else:
            # 2채널이 뒤에 stack돼 있다 — 첫 블록(1채널 몫)만 잘라내고 guid는 유지한다
            # (2채널 쪽 효과가 이 guid를 그대로 참조하고 있으므로).
            second_block_start = block_starts[1]
            new_text = text[:block_starts[0]] + text[second_block_start:]

            remainder = desc[len(row_desc):]
            remainder = remainder.lstrip()
            prefix = f'| {STACK_MARKER_2CH}: '
            if remainder.startswith(prefix):
                remainder = remainder[len(prefix):]
            new_text = new_text.replace(f'  description: {desc}', f'  description: {remainder}', 1)
            open(path, 'w', encoding='utf-8').write(new_text)
            trimmed += 1

    print(f'되돌림: 순수 1채널 파일 삭제 {deleted}개, 2채널 stack에서 1채널 몫만 제거 {trimmed}개, '
          f'1채널 마커 없어 안 건드림 {untouched}개')


def regenerate():
    gen1.main()


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'revert':
        revert()
    elif len(sys.argv) > 1 and sys.argv[1] == 'regenerate':
        regenerate()
    else:
        print('usage: revert | regenerate')
