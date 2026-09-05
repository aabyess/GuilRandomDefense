#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""원작 스킬 196건(96개 원작 유닛 단위) 배정 — 설계·표 준비 단계, 아직 실행 금지.

⚠️⚠️ 2026-09-05: PM 지시로 대기 중이다. 리서치담당이 (a) 상세 피해표의 열 순서,
(b) ATTACK_TYPE_NORMAL이 실제로 Spells인지를 검증하는 중이고, 그 결과에 따라
196건 전체의 attackType 값이 통째로 바뀐다. **결과가 오기 전엔 이 스크립트를
돌리지 않는다** — main()은 지금 dry-run(배정 미리보기만, 파일 쓰기 없음)만 한다.

## 설계 원칙 (PM 지시, 사장님 확정 "모든 것 원작 따라간다")

1. **원작 유닛 단위로 묶어서 통째로 준다.** 사보의 스킬 4개가 마나 125 게이지를
   공유하는 것처럼, 한 원작 유닛의 여러 스킬을 우리 유닛 여러 명에 낱개로 흩으면
   그 공유 게이트가 깨진다 — Docs/reference/UNLISTED_SKILLS_BY_UNIT.csv(96행,
   유닛당 미수록 스킬 전부를 이미 묶어놨다)의 한 행 = 우리 유닛 한 명.
2. **등급 안 순위 대응** — 지금까지와 같은 방식: 원작은 그 유닛의 "화력"(Flat합계,
   %MaxHp%·%CurHp%·buf 태그는 순위 정렬에선 무시하고 숫자만 본다) 내림차순, 우리는
   로스터 유닛 DPS(공격력×공속) 내림차순으로 정렬해 순서대로 짝짓는다.
3. 06번①이 이미 skill을 채운 15종(스킬승급형)·8종(능력교체형 대상 유닛 자체는
   포함하되 그 유닛의 UnitData.skill은 이미 pass1/2로 찼을 수 있다 — 아래 CLAIMED_BASES는
   "스킬승급형 15종"만 배제한다, 능력교체형 8종은 이미 pass1/2로 base skill이 찼을
   수도 안 찼을 수도 있어 일반 "has_pass1/has_other" 판정으로 자연히 걸러진다)는
   제외 대상 그대로 유지한다.
4. 이미 pass1/2 능력이 있는 유닛은 **효과를 추가**한다(SkillData.levels[0].effects에
   append) — 덮지 않는다. 없는 유닛은 새 SkillData 생성.

## 아직 못 채우는 이유 (표 준비 단계에서 멈추는 지점)

`UNLISTED_SKILLS_BY_UNIT.csv`는 **유닛 단위 집계**다 — 실제 배정에 필요한
개별 스킬의 계산식·게이트·attackType은 없다(스킬목록 컬럼에 이름만 있다). 그
상세표(예: `ORIGINAL_UNLISTED_SKILLS_DETAIL.csv` 같은 것 — 아직 안 옴)가 와야
`extract_effects_for_skill()`을 실제로 채울 수 있다. 지금은 그 자리를
NotImplementedError로 비워뒀다 — 상세표 형식을 보지 않고 추측으로 파서부터 짓지
않는다(2026-09-05 06번① 작업에서 겪은 실패 패턴: 트리거 이름만 보고 다른 블록의
효과를 잘못 붙인 사고가 여러 번 났다 — 이번엔 미리 막는다).

## 실행 순서 (표가 오면)

1. `DETAIL_CSV` 상수에 경로를 채운다.
2. `extract_effects_for_skill(skill_name, detail_rows)` 구현 — 06번①/2차 채널
   스크립트(`generate_unit_skill_damage_effects.py`)의 파서를 재사용할 수 있으면
   재사용한다(같은 basis축·basis대응 관례일 가능성이 높다).
3. `ATTACK_TYPE_POLICY`를 리서치담당 결론에 맞춰 채운다(지금은 자리만).
4. `main(dry_run=False)`로 실행.
"""
import csv
import glob
import re
from collections import defaultdict

CSV_PATH = 'Docs/reference/UNLISTED_SKILLS_BY_UNIT.csv'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_DIR = 'Assets/Data/UnitSkills'

# ⚠️ 리서치담당 검증 대기 — 결과 오면 채운다. 지금은 아무 매핑도 확정하지 않는다.
DETAIL_CSV = None
ATTACK_TYPE_POLICY = None  # 예: {'NORMAL': 1, ...} — Spells 여부 결론 나오면 채움

GRADE_ENUM = {
    '흔함': 0, '특별함': 2, '희귀함': 3, '히든': 4, '전설적인': 5, '제한됨': 6,
    '초월함': 7, '불멸의': 8, '영원한': 9, '랜덤전용': 10, '특수함': 12, '변화된': 14,
}

# 06번①이 base skill을 직접 채운 스킬승급형 15종 — 이 유닛들의 UnitData.skill을
# 건드리면 그 레벨 트랙(levels[0]/[1])이 끊긴다. 능력교체형 8종의 대상 유닛(신지우 등)은
# 여기 없다 — 그쪽은 base skill이 pass1/2로 이미 찼을 수도 있어 has_pass1 판정으로
# 자연히 걸러진다.
CLAIMED_BASES = {
    '불멸_이이삭', '불멸_이승우', '불멸_박은석', '불멸_정준영', '불멸_정윤식', '영원_조세민',
    '초월_김만경_AD', '초월_박기찬_AD', '초월_유재헌_ADAP', '초월_임채민_AP', '초월_이태훈_AP',
    '초월_조성진_AD', '초월_임장혁_AD', '초월_최상호_AP', '초월_황준석_ADAP',
}


def flat_sort_key(flat_summary):
    """'9,056,250+buf' 같은 문자열에서 정렬용 숫자만 뽑는다. +%MaxHp%·+buf 태그는 무시."""
    m = re.match(r'([\d,]+)', flat_summary)
    return int(m.group(1).replace(',', '')) if m else 0


def load_unit_groups():
    with open(CSV_PATH, encoding='utf-8') as f:
        rows = list(csv.DictReader(f))
    by_grade = defaultdict(list)
    for r in rows:
        by_grade[r['원작등급']].append(r)
    for g in by_grade:
        by_grade[g].sort(key=lambda r: flat_sort_key(r['Flat합계']), reverse=True)
    return by_grade


def load_roster():
    pass1_names = set()
    for p in glob.glob(f'{SKILL_DIR}/SkillData_원작능력_*.asset'):
        base = p.split('/')[-1][len('SkillData_원작능력_'):-len('.asset')]
        pass1_names.add(base)

    roster = []
    for path in sorted(glob.glob(f'{ROSTER_DIR}/*.asset')):
        text = open(path, encoding='utf-8').read()
        grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
        ap = float(re.search(r'^  attackPower: ([\d.]+)', text, re.M).group(1))
        aspd = float(re.search(r'^  attackSpeed: ([\d.]+)', text, re.M).group(1))
        base = path.split('/')[-1][:-len('.asset')]
        has_skill = not re.search(r'^  skill: \{fileID: 0\}', text, re.M)
        has_pass1 = base in pass1_names
        claimed = (has_skill and not has_pass1) or base in CLAIMED_BASES
        roster.append(dict(path=path, base=base, grade=grade, dps=ap * aspd,
                            has_pass1=has_pass1, claimed=claimed))
    return roster


def extract_effects_for_skill(skill_name, detail_rows):
    """⚠️ 상세표가 와야 구현 가능 — 지금은 자리만."""
    raise NotImplementedError(
        '상세 피해표(개별 스킬 계산식·게이트·attackType)가 아직 없다. '
        'DETAIL_CSV를 채우고 이 함수를 구현할 것 — 06번①에서 겪은 실수'
        '(트리거 이름만 보고 다른 블록 효과를 잘못 붙임)를 반복하지 않도록, '
        '반드시 최상위 if 블록 단위로 개별 스킬 이름과 효과를 1:1 확인한 표를 쓸 것.'
    )


def preview_matching():
    """읽기 전용 — 원작 유닛과 우리 유닛의 매칭만 미리 보여준다. 아무 것도 안 고침."""
    by_grade = load_unit_groups()
    roster = load_roster()

    total_units_matched = 0
    total_skills_matched = 0

    for gname, genum in GRADE_ENUM.items():
        originals = by_grade.get(gname, [])
        eligible = sorted(
            [u for u in roster if u['grade'] == genum and not u['claimed']],
            key=lambda u: -u['dps'],
        )
        n = min(len(originals), len(eligible))
        if n == 0:
            continue

        print(f"=== {gname} (원작 유닛 {len(originals)} / 가용 {len(eligible)} / 배정 {n}) ===")
        for i in range(n):
            o, u = originals[i], eligible[i]
            print(f"  {u['base']:32s} <- {o['유닛ID']} {o['유닛이름'][:24]:24s} "
                  f"(스킬 {o['미수록스킬수']}개, {o['Flat합계']})")
            total_units_matched += 1
            total_skills_matched += int(o['미수록스킬수'])
        print()

    print(f"미리보기 합계: 원작 유닛 {total_units_matched}종 매칭, 스킬 {total_skills_matched}건 "
          f"(전체 96종/196건 중)")


def main(dry_run=True):
    if not dry_run:
        if DETAIL_CSV is None or ATTACK_TYPE_POLICY is None:
            print("DETAIL_CSV·ATTACK_TYPE_POLICY가 아직 없다 — 실행을 멈춘다. 아무것도 안 고침.")
            return
        raise NotImplementedError('실제 배정 로직은 상세표가 온 뒤에 작성한다.')

    preview_matching()


if __name__ == '__main__':
    main(dry_run=True)
