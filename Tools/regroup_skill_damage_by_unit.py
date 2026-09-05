#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""③-b (PM 지시, TEAM_RULES 뿌리 ⑮): generate_unit_skill_damage_effects.py가 195행을
「행 단위」로 등급 안 순위매칭해서 원작 유닛 하나(예: 벤 베크만 Trig_Unique21의 계산식
2줄)가 서로 다른 두 로스터 유닛에 쪼개지는 버그(32유닛→77슬롯, DUPLICATE_ASSIGNMENT_
AUDIT.md)를 고친다. 원칙: **한 원작 유닛의 행 전부가 같은 로스터 유닛으로 간다.**

## 1단계 — 되돌리기
기존 2차 채널 출력을 지우고 원상복구한다. 이 스크립트는 "행 1개 → 로스터 1개"로 동작해서
(같은 등급 안에서 ability_rows[i]가 eligible[i]에 정확히 하나씩 짝지어진다) 한 유닛이
2차 채널로부터 받는 효과는 항상 정확히 "그 자리에 배정된 행 하나"뿐이었다 — 그래서
"이 파일이 2차 채널로 받은 효과 개수"는 곧 그 자리를 차지한 행의 extract_effects() 길이
하나뿐이다(h030의 3효과 특례 제외 — h030은 실제로 어디에도 배정되지 않았음을 확인했다).
→ 각 파일에서 마지막 1개(대부분) 효과 블록만 잘라내고, description의 " | 2차 배정(트리거·
더미 채널): ..." 꼬리를 지운다. 1차만 있던 파일이면 이걸로 순수 1차 상태로 돌아간다.
2차가 만든 새 파일(1차가 아예 없던 유닛)은 통째로 지우고 로스터의 skill을 {fileID: 0}으로
되돌린다.

## 2단계 — 유닛 단위로 재배정
195행을 유닛ID로 묶어(by_unit), 유닛당 총점(score 합)으로 등급 안 순위를 매기고, 등급 안
가용 로스터(claimed 제외, 되돌리기 이후 재계산)와 다시 1:1로 짝짓는다. 짝지어진 유닛의
행 전부를 그 유닛 하나에(같은 SkillData, 여러 effects) 통째로 넣는다 — 이 채널엔 게이트
컬럼이 없어(원래도 게이트 미인식 채널) 게이트별로 더 쪼갤 근거가 없다.

## 검증
되돌리기 전/후로 "원작 능력 → 로스터 유닛" 대응표를 각각 뽑아 diff를 남긴다(리뷰 자료).
"""
import csv
import glob
import re
import sys
from collections import defaultdict

sys.path.insert(0, 'Tools')
import generate_unit_skill_damage_effects as gen2
import generate_unit_skills_by_gate as gen_gate

OUT_DIR = gen2.OUT_DIR
ROSTER_DIR = gen2.ROSTER_DIR
STACK_MARKER = gen2.STACK_MARKER


def snapshot_current_mapping():
    """현재(되돌리기 전) uid -> [roster_base,...] 매핑을 마커 텍스트로 복원한다."""
    files = {f: open(f, encoding='utf-8').read() for f in glob.glob(f'{OUT_DIR}/SkillData_원작능력_*.asset')}
    by_grade, _ = gen2.load_csv()
    included = [r for g in by_grade.values() for r in g]

    mapping = defaultdict(list)
    for r in included:
        origin_note = r['출처'] if r['회수경로'] == '트리거' else f"더미 경로 {r['출처']}"
        marker_stack = f"원작 {r['등급']} {r['유닛이름']}({origin_note})."
        origin_label = r['출처'] if r['회수경로'] == '트리거' else f"더미:{r['출처']}"
        marker_fresh = f"원작 {r['등급']} {r['유닛이름']}({origin_label})의 피해를 옮겼다."
        hits = [f.split('/')[-1] for f, text in files.items() if marker_stack in text or marker_fresh in text]
        key = r['유닛ID']
        for h in hits:
            base = h[len('SkillData_원작능력_'):-len('.asset')]
            if base not in mapping[key]:
                mapping[key].append(base)
    return mapping


def revert():
    by_grade, _ = gen2.load_csv()
    included = [r for g in by_grade.values() for r in g]

    reverted_stacked = 0
    reverted_fresh = 0

    for r in included:
        origin_note = r['출처'] if r['회수경로'] == '트리거' else f"더미 경로 {r['출처']}"
        marker_stack = f"원작 {r['등급']} {r['유닛이름']}({origin_note})."
        origin_label = r['출처'] if r['회수경로'] == '트리거' else f"더미:{r['출처']}"
        marker_fresh_prefix = "원작 트리거·더미 피해표(ORIGINAL_SKILL_DAMAGE_TABLE.csv) DPS 순위 배정"
        marker_fresh = f"원작 {r['등급']} {r['유닛이름']}({origin_label})의 피해를 옮겼다."

        n_effects = len(gen2.extract_effects(r))

        for f in glob.glob(f'{OUT_DIR}/SkillData_원작능력_*.asset'):
            text = open(f, encoding='utf-8').read()
            is_fresh = marker_fresh_prefix in text and marker_fresh in text
            is_stacked = (STACK_MARKER in text) and (f' | {STACK_MARKER}: {marker_stack}' in text)

            if is_fresh:
                base = f.split('/')[-1][len('SkillData_원작능력_'):-len('.asset')]
                roster_path = f'{ROSTER_DIR}/{base}.asset'
                meta = open(f + '.meta', encoding='utf-8').read()
                guid = re.search(r'guid: (\w+)', meta).group(1)
                rtext = open(roster_path, encoding='utf-8').read()
                rtext = re.sub(
                    r'^  skill: \{fileID: 11400000, guid: ' + re.escape(guid) + r'[^\n]*\}$',
                    '  skill: {fileID: 0}', rtext, count=1, flags=re.M,
                )
                open(roster_path, 'w', encoding='utf-8').write(rtext)
                import os
                os.remove(f)
                os.remove(f + '.meta')
                reverted_fresh += 1
                break

            if is_stacked:
                # 마지막 n_effects개 효과 블록을 잘라낸다.
                idxs = [m.start() for m in re.finditer(r'^    - kind: \d+\n', text, re.M)]
                cut_at = idxs[-n_effects]
                text = text[:cut_at]
                # description 꼬리(" | 2차 배정...") 제거
                text = re.sub(
                    r'( \| ' + re.escape(STACK_MARKER) + r': .*?)(\n  triggerType:)',
                    r'\2', text, count=1, flags=re.S,
                )
                open(f, 'w', encoding='utf-8').write(text)
                reverted_stacked += 1
                break

    print(f'되돌림: 기존 스킬에 붙었던 것 {reverted_stacked}건, 새로 만들어졌던 것 {reverted_fresh}건 삭제')


def regenerate():
    by_grade, skipped = gen2.load_csv()
    included = [r for g in by_grade.values() for r in g]
    by_unit = defaultdict(list)
    for r in included:
        by_unit[r['유닛ID']].append(r)

    unit_groups = defaultdict(list)
    for uid, rows in by_unit.items():
        unit_groups[rows[0]['등급']].append((uid, rows))

    def unit_total_score(rows):
        return sum(gen2.score(r) for r in rows)

    roster = gen2.load_roster()  # claimed는 되돌리기 이후 상태 반영

    stacked = 0
    created = 0
    dropped_uids = []

    for gname, genum in gen2.GRADE_ENUM.items():
        originals = sorted(unit_groups.get(gname, []), key=lambda x: -unit_total_score(x[1]))
        eligible = sorted([u for u in roster if u['grade'] == genum and not u['claimed']],
                           key=lambda u: -u['dps'])
        n = min(len(originals), len(eligible))
        if len(originals) > n:
            dropped_uids.extend(uid for uid, _ in originals[n:])

        for i in range(n):
            uid, rows = originals[i]
            unit = eligible[i]

            # ⚠️ 2026-09-06 수정: gen2.append_to_existing()은 STACK_MARKER가 이미 있으면
            # (재실행 안전장치) 조용히 no-op한다 — 그런데 uid 하나가 행 여러 개면 이 배치
            # 안에서 같은 파일에 여러 번 호출하게 되고, 첫 호출 뒤로는 전부 무시돼 h025
            # (3행)처럼 2·3번째 행이 통째로 사라졌었다(직접 확인). 그래서 uid의 행 전부를
            # 한 번에 모아 effects/description을 만들고 딱 한 번만 쓴다.
            effect_blocks = []
            desc_suffix_parts = []
            for row in rows:
                attack_type, damage_type, uncertain = gen2.resolve_attack_type(row, unit['attack_type'])
                for basis, mult, bonus in gen2.extract_effects(row):
                    effect_blocks.append(gen2.render_effect_block(basis, mult, bonus, attack_type, damage_type))
                desc_suffix_parts.append(gen2.describe(row, uncertain))
            effects_text = ''.join(effect_blocks)

            if unit['has_pass1']:
                path = f"{OUT_DIR}/SkillData_원작능력_{unit['base']}.asset"
                text = open(path, encoding='utf-8').read()
                if gen2.STACK_MARKER in text:
                    pass  # 실제 재실행 — 이미 이번 채널이 붙어 있다, 손대지 않는다.
                else:
                    text = text.rstrip('\n') + '\n' + effects_text
                    text = re.sub(
                        r'^(  description: .*)$',
                        lambda m: m.group(1) + ''.join(desc_suffix_parts),
                        text, count=1, flags=re.M,
                    )
                    open(path, 'w', encoding='utf-8').write(text)
                    stacked += len(rows)
                    unit['text'] = text
            else:
                name = f'SkillData_원작능력_{unit["base"]}'
                guid = gen2.guid_for(name)
                path = f'{OUT_DIR}/{name}.asset'
                first_row = rows[0]
                origin_label = first_row['출처'] if first_row['회수경로'] == '트리거' else f"더미:{first_row['출처']}"
                description = (
                    f"원작 트리거·더미 피해표(ORIGINAL_SKILL_DAMAGE_TABLE.csv) DPS 순위 배정 — "
                    f"원작 {first_row['등급']} {first_row['유닛이름']}({origin_label})의 피해를 옮겼다. 1차(uabi "
                    f"능력ID) CSV엔 없던 능력이다 — 원작이 유닛ID→트리거 해시테이블로 처리해서 "
                    f"능력ID로는 안 보였다. 개별 대응 아님(이름매핑 불가) — 원작 유닛 하나(행 {len(rows)}개)를 "
                    f"통째로 이 유닛에 배정했다(2026-09-06 유닛 단위 재배정, TEAM_RULES 뿌리 ⑮)."
                    + ''.join(desc_suffix_parts)
                )
                body = (
                    gen2.HEAD.replace('__SCRIPT__', gen2.SKILL_SCRIPT_GUID).replace('__NAME__', name)
                    + f"  skillName: {first_row['출처']}\n"
                    + f"  description: {description}\n"
                    + "  triggerType: 0\n"
                    + "  levels:\n"
                    + "  - cooldown: 0\n"
                    + "    triggerChance: 1.0\n"
                    + "    range: 0\n"
                    + "    effects:\n"
                    + effects_text
                )
                open(path, 'w', encoding='utf-8').write(body)
                gen2.write_meta(path, guid)
                # ⚠️ gen2.load_roster()의 claimed 판정은 skill 필드만 본다 — 이 유닛이
                # 이미 skills 리스트(by-gate 96프로젝트 등)를 갖고 있으면 skill 필드에
                # 직접 쓰는 순간 skill+skills 동시 채움 충돌이 난다(검사 #11). 이번
                # 회귀에서 실제로 7건 걸렸다 — skills가 비어있지 않으면 그쪽에 추가한다.
                if re.search(r'^  skills:\n(?:  - .*\n)+', unit['text'], re.M):
                    new_text = gen_gate.add_to_skills_list(unit['text'], guid)
                else:
                    new_text = re.sub(
                        r'^  skill: \{fileID: 0\}$',
                        f'  skill: {{fileID: 11400000, guid: {guid}, type: 2}}',
                        unit['text'], count=1, flags=re.M,
                    )
                unit['text'] = new_text
                open(unit['path'], 'w', encoding='utf-8').write(new_text)
                created += len(rows)

    print(f"재생성: 기존 스킬에 효과 추가 {stacked}건, 새 SkillData 생성 {created}건")
    print(f"등급 슬롯 부족으로 못 넣은 원작 유닛: {len(dropped_uids)}개 — {dropped_uids}")
    print(f"건너뛴 13행(스키마에 없음):")
    for r in skipped:
        print(f"  {r['유닛ID']} {r['등급']} {r['유닛이름']} | {r['출처']} | basis축={r['basis축']}")


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'snapshot':
        mapping = snapshot_current_mapping()
        for uid, bases in sorted(mapping.items()):
            print(uid, '->', bases)
    elif len(sys.argv) > 1 and sys.argv[1] == 'revert':
        revert()
    elif len(sys.argv) > 1 and sys.argv[1] == 'regenerate':
        regenerate()
    else:
        print('usage: snapshot | revert | regenerate')
