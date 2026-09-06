#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""리서치담당 19f86f7/5b7ed24 — 79행 중 43행은 더미가 생성만 되고 오더가 안 내려가
피해가 시전되지 않는다(순수 시각 이펙트). 그 피해는 같은 트리거의 RRD가 이미
`ORIGINAL_UNLISTED_SKILL_EFFECTS.csv`에 세어져 있어, 더미 쪽 w3a 값을 같이 넣으면
이중 계상이다.

PM 지시: 지우지 말고 기록을 남긴다 — description에 원래 값과 "오더 없음, RRD가
이미 낸다"를 적고, 실제 효과(피해 기여)만 제거한다. 71acf64/c0af3bd로 이미 들어간
56행 중 31행이 여기 해당한다(작동 25 vs 작동 안 함 31).

unlock_dummy_channel_gates.py가 만든 파일들은 description에 행마다
"· {더미ID}({더미이름}) {능력} ...: {값} (...) 진입게이트: ..." 불릿을 effects와
같은 순서로 하나씩 붙였다 — 그 순서 그대로 매칭해서 "작동 안 함" 행의 effect만
빼낸다. 전부 빠지면 파일 자체(과 로스터 참조)를 지운다 — 빈 껍데기를 남기지
않는다(이미 기록은 REMOVED_NONFUNCTIONAL_DUMMY_ROWS.csv에 남는다).
"""
import csv
import glob
import os
import re

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'

BULLET_RE = re.compile(
    r'  · (\S+)\(([^)]*)\) (\S+) ([^\[]*)\[([^\]]*)\]: (-?[\d.]+) \(원작 ([^)]*)\) 진입게이트: (.*?)(?=  · |\Z)',
    re.S)


def read(p):
    return open(p, encoding='utf-8').read()


def write(p, t):
    open(p, 'w', encoding='utf-8').write(t)


def load_status():
    status = {}
    for r in csv.DictReader(open('Docs/reference/DUMMY_CHANNEL_MISSING.csv', encoding='utf-8-sig')):
        status[(r['더미유닛ID'], r['능력'])] = r['실제작동']
    return status


def find_roster_owning_guid(guid):
    for rp in glob.glob(f'{ROSTER_DIR}/*.asset'):
        if guid in read(rp):
            return rp
    return None


def remove_from_skills(roster_path, guid):
    text = read(roster_path)
    text2 = re.sub(rf'  - \{{fileID: 11400000, guid: {guid}, type: 2\}}\n', '', text)
    assert text2 != text, (roster_path, guid)
    write(roster_path, text2)


def main():
    status = load_status()
    removed_rows = []  # for CSV record
    files_deleted = []
    files_trimmed = []

    for path in sorted(glob.glob(f'{SKILL_DIR}/SkillData_더미채널_*_79행_*.asset')):
        text = read(path)
        desc_m = re.search(r'^  description: (.*)$', text, re.M)
        desc = desc_m.group(1)
        bullets = BULLET_RE.findall(desc)
        if not bullets:
            continue  # 잠긴(lock) 그룹 등 — 이번 배치 우려 대상 아님(별도 확인 필요시 수동)

        level_starts = [m.start() for m in re.finditer(r'^  - cooldown: ', text, re.M)]
        level0_end = level_starts[1] if len(level_starts) > 1 else len(text)
        level0 = text[level_starts[0]:level0_end]
        effect_blocks = re.findall(r'^    - kind: \d+\n(?:      .*\n)+', level0, re.M)
        assert len(effect_blocks) == len(bullets), (path, len(effect_blocks), len(bullets))

        keep_bullets, keep_effects, dropped = [], [], []
        for (dummy_id, dummy_name, ability, ability_name, field_mean, value, char_grade, gate), eff in \
                zip(bullets, effect_blocks):
            st = status.get((dummy_id, ability), '')
            if st == '작동':
                keep_bullets.append((dummy_id, dummy_name, ability, ability_name, field_mean, value, char_grade, gate))
                keep_effects.append(eff)
            else:
                dropped.append((dummy_id, dummy_name, ability, ability_name.strip(), field_mean, value, char_grade, gate.strip(), st))

        if not dropped:
            continue

        for d in dropped:
            removed_rows.append((path, *d))

        if not keep_bullets:
            # 이 파일 전체가 무효 — 로스터 참조까지 지운다.
            meta_guid = re.search(r'guid: ([0-9a-f]+)', read(path + '.meta')).group(1)
            roster_path = find_roster_owning_guid(meta_guid)
            if roster_path:
                remove_from_skills(roster_path, meta_guid)
            os.remove(path)
            os.remove(path + '.meta')
            files_deleted.append(path)
            continue

        # 일부만 무효 — description 재작성 + effects 재작성.
        head_line_m = re.match(r'^(.*?)(?=  · )', desc, re.S)
        head_line = head_line_m.group(1) if head_line_m else desc
        new_bullets_text = ''.join(
            f"  · {d}({n}) {a} {an}[{fm}]: {v} (원작 {cg}) 진입게이트: {g}\n"
            for d, n, a, an, fm, v, cg, g in keep_bullets
        )
        removed_note = "제거된 행(오더 없음 — 시각 이펙트, 리서치담당 19f86f7/5b7ed24): " + ' / '.join(
            f"{d}({n}) {a} {an.strip()}: {v}(원작 값, 참고용 — 실제로는 안 쏨. 피해는 같은 트리거의 RRD가 "
            f"ORIGINAL_UNLISTED_SKILL_EFFECTS.csv에 이미 셈, 이중 계상 방지)"
            for d, n, a, an, fm, v, cg, g, st in dropped
        )
        new_desc = head_line.rstrip() + '\n' + new_bullets_text + '  ' + removed_note

        new_text = text[:desc_m.start()] + f"  description: {new_desc}" + text[desc_m.end():]
        # effects 재작성
        old_effects_block = ''.join(effect_blocks)
        new_effects_block = ''.join(keep_effects)
        new_text = new_text.replace(old_effects_block, new_effects_block, 1)
        write(path, new_text)
        files_trimmed.append((path, len(dropped), len(keep_bullets)))

    # ⚠️ 이 스크립트는 재실행해도 안전해야 한다(효과가 이미 빠진 파일은 다시 안 걸림) —
    # 그런데 그 "안전"이 함정이었다: 새로 뺄 게 0건인 재실행에서 이 CSV를 빈 걸로 덮어써
    # 이전 실행 기록(32건)을 실제로 날린 적이 있다(DUMMY_CHANNEL_MISSING.csv의 실제작동
    # 원본 데이터로 복구함). 그래서 기존 행을 읽어 합치고 안 지운다.
    out_path = 'Docs/reference/REMOVED_NONFUNCTIONAL_DUMMY_ROWS.csv'
    existing = []
    try:
        with open(out_path, encoding='utf-8') as f:
            r = csv.reader(f)
            next(r, None)
            existing = list(r)
    except FileNotFoundError:
        pass
    existing_keys = {(row[0], row[1]) for row in existing}  # (파일, 더미유닛ID)
    merged = existing + [row for row in removed_rows if (row[0], row[1]) not in existing_keys]

    with open(out_path, 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['파일', '더미유닛ID', '더미이름', '능력', '능력이름', '필드의미', '값', '원작캐릭터등급', '진입게이트', '실제작동'])
        for row in merged:
            w.writerow(row)

    print(f'제거된 행: {len(removed_rows)}건 -> Docs/reference/REMOVED_NONFUNCTIONAL_DUMMY_ROWS.csv')
    print(f'전부 무효라 파일 삭제: {len(files_deleted)}개')
    for p in files_deleted:
        print('  -', p)
    print(f'일부만 제거(파일 유지): {len(files_trimmed)}개')
    for p, n_dropped, n_kept in files_trimmed:
        print(f'  - {p}: 제거 {n_dropped} / 유지 {n_kept}')


if __name__ == '__main__':
    main()
