#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""원작 스킬 173행(판정=확정, ORIGINAL_UNLISTED_SKILL_EFFECTS.csv d2c5093)을
원작 유닛 단위로 통째로 배정한다.

PM 지시(2026-09-05) — 설계 원칙 둘:
1. 원작 유닛 단위로 묶어서 통째로 준다(사보 스타일 게이트 공유가 낱개 배정으로
   깨지지 않게) — 이 CSV 자체가 이미 유닛ID로 그룹 지을 수 있게 나온다.
2. 등급 안 순위 대응 — 원작은 그 유닛의 총 화력(이 스크립트가 직접 계산) 내림차순,
   우리는 로스터 DPS(공격력×공속) 내림차순으로 정렬해 순서대로 짝짓는다.

## 컬럼 → 스키마 대응
`basis` 문자열은 "Flat"/"대상최대체력"/"대상현재체력" 셋뿐이다(이 173행 범위엔
CasterAttackPower·ResearchLevel이 없다 — 그건 06번①/1차 채널 전용 basis다).

⚠️ Flat 행은 **값이 `bonus` 컬럼에 들어 있고 `multiplier`는 비어 있다**(리서치담당의
표 관례 — "bonus=상수항 전체"). 하지만 우리 코드(`UnitAttacker.ResolveSkillEffectValue`)는
`Flat` basis에서 **`multiplier`만 읽는다**(`bonus`는 의도적으로 안 읽는다, PM 지시
2026-09-05 버그 수정 커밋 참고) — 그래서 Flat 행을 옮길 때는 **표의 bonus 값을 우리
schema의 multiplier에** 넣는다(뒤바뀐 게 아니라 표와 코드의 "어느 컬럼이 상수인가"
관례가 다른 것뿐이다). %체력 두 basis는 표의 multiplier/bonus를 그대로(계수/상수항)
옮긴다 — 이제 코드가 그 bonus를 더한다(버그 수정 완료 확인).

`attackType`/`damageType`은 표에 이미 고친 이름으로 들어 있다(NORMAL→Spells 등 이미
반영됨) — 그대로 우리 enum에 매핑만 한다.

## range 충돌
한 원작 유닛이 여러 `Enemies` 대상 스킬을 갖는데 스킬마다 반경이 다른 경우가 11종
있다(예: h04B 450/700, h05C 600/450) — `SkillLevel.range`는 레벨 하나에 하나뿐이라
전부 담을 수 없다. **그 유닛의 Enemies 스킬 중 가장 작은 반경**을 그 유닛 전체에
적용한다(더 넓게 잡으면 원작보다 많은 대상을 맞히는 쪽으로 사고가 나는데, 좁게
잡으면 "덜 맞는" 쪽으로만 어긋나 상대적으로 안전하다) — description에 어느 스킬이
원래 어떤 반경이었는지, 그리고 이 근사로 인해 반경이 줄어든 스킬이 어느 것인지
전부 남긴다.

## hitCount
표의 `hitCount`는 "같은 RRD 식이 몇 번 반복 등장하는가"이지 우리 스키마의 "시간에
걸쳐 나눠 때리는 다단히트" 개념이 아니다(PM 지시로 표에 단서가 붙어 있음). 그래도
총 피해량 관점에서는 "같은 값이 N번 들어간다"가 동등하므로 hitCount만 그대로
옮기고 duration=0으로 둔다(우리 엔진은 duration=0이면 즉시 연속으로 N번 때린다 —
"동시에 N번"에 가장 가까운 근사) — 원작의 실제 타이밍(스킬마다 다를 수 있음)과는
다를 수 있다는 것을 description에 남긴다.

## 제외
- `range_판정=범위확정(비수치)` 7행(그룹변수라 숫자가 아님) — 이번엔 건너뛰고 목록으로 보고.
- `판정≠확정`(축밖 79·미확인 50) — 안 건드림.
- 06번① 스킬승급형 15종(CLAIMED_BASES) — 이미 채운 유닛은 배정 풀에서 제외.

재실행해도 STACK_MARKER로 중복 추가를 막는다(2차 채널 스크립트와 같은 관례).
"""
import csv
import glob
import hashlib
import re
from collections import defaultdict

CSV_PATH = 'Docs/reference/ORIGINAL_UNLISTED_SKILL_EFFECTS.csv'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_DIR = 'Assets/Data/UnitSkills'
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'  # SkillData.cs

STACK_MARKER = '96유닛 배정(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv)'
REFERENCE_HP = 1_000_000.0  # 정렬 전용 — 저장값엔 안 쓴다(2차 채널 스크립트와 같은 관례).

GRADE_ENUM = {
    '흔함': 0, '특별함': 2, '희귀함': 3, '히든': 4, '전설적인': 5, '제한됨': 6,
    '초월함': 7, '불멸의': 8, '영원한': 9, '랜덤전용': 10, '특수함': 12, '변화된': 14,
}

BASIS_MAP = {'Flat': 0, '대상최대체력': 1, '대상현재체력': 2}
TARGET_MAP = {'SingleTarget': 3, 'Enemies': 2}
ATTACK_TYPE_MAP = {'Normal': 1, 'Pierce': 2, 'Siege': 3, 'Hero': 4, 'Chaos': 5, 'Magic': 6, 'Spells': 7}
DAMAGE_TYPE_MAP = {'AD': 1, 'AP(방어무시)': 2}

# 06번①이 base skill을 직접 채운 스킬승급형 15종 — 배정 풀에서 뺀다.
CLAIMED_BASES = {
    '불멸_이이삭', '불멸_이승우', '불멸_박은석', '불멸_정준영', '불멸_정윤식', '영원_조세민',
    '초월_김만경_AD', '초월_박기찬_AD', '초월_유재헌_ADAP', '초월_임채민_AP', '초월_이태훈_AP',
    '초월_조성진_AD', '초월_임장혁_AD', '초월_최상호_AP', '초월_황준석_ADAP',
}


def fnum(s):
    s = s.strip()
    return float(s) if s else 0.0


def guid_for(name):
    return hashlib.md5(('guilrd/unlistedskills/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def load_confirmed_groups():
    with open(CSV_PATH, encoding='utf-8') as f:
        rows = list(csv.DictReader(f))

    excluded_non_numeric = [
        r for r in rows if r['판정'] == '확정' and r['range_판정'] == '범위확정(비수치)'
    ]
    confirmed = [
        r for r in rows if r['판정'] == '확정' and r['range_판정'] != '범위확정(비수치)'
    ]

    by_unit = defaultdict(list)
    for r in confirmed:
        by_unit[r['유닛ID']].append(r)

    return by_unit, excluded_non_numeric


def unit_score(rows):
    total = 0.0
    for r in rows:
        basis = r['basis']
        if basis == 'Flat':
            total += fnum(r['bonus'])  # Flat 값은 표의 bonus 컬럼에 있다.
        else:
            total += fnum(r['multiplier']) * REFERENCE_HP + fnum(r['bonus'])
    return total


def resolve_range(rows):
    enemy_rows = [r for r in rows if r['target'] == 'Enemies']
    ranges = sorted({fnum(r['range']) for r in enemy_rows})
    if len(ranges) <= 1:
        return ranges[0] if ranges else 0.0, None
    chosen = ranges[0]
    note = (
        f" ⚠️ 이 유닛의 Enemies 스킬 반경이 여럿({', '.join(str(int(x)) for x in ranges)})이라 "
        f"가장 작은 {int(chosen)}로 통일했다(넓게 잡으면 원작보다 많이 맞는 쪽으로 어긋나서, "
        f"좁게 잡는 쪽이 상대적으로 안전하다) — 반경이 원래 더 넓었던 스킬은 실제보다 "
        f"좁게 적용된다."
    )
    return chosen, note


def render_effect(row, range_note_applies):
    basis = BASIS_MAP[row['basis']]
    target = TARGET_MAP[row['target']]
    attack_type = ATTACK_TYPE_MAP[row['attackType']]
    damage_type = DAMAGE_TYPE_MAP[row['damageType']]
    hit_count = int(float(row['hitCount'])) if row['hitCount'].strip() else 1

    if basis == 0:  # Flat — 표의 bonus 컬럼이 값, 우리 schema는 multiplier에 넣는다.
        multiplier = fnum(row['bonus'])
        bonus = 0.0
    else:
        multiplier = fnum(row['multiplier'])
        bonus = fnum(row['bonus'])

    return (
        "    - kind: 0\n"
        f"      basis: {basis}\n"
        f"      target: {target}\n"
        f"      damageType: {damage_type}\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(multiplier, 4)}\n"
        f"      bonus: {round(bonus, 4)}\n"
        "      chance: 1\n"
        f"      hitCount: {hit_count}\n"
        "      duration: 0\n"
        "      casterBuffCountFactor: 0\n"
    )


def describe_unit(unit_id, unit_name, rows, range_note):
    lines = [
        f"{STACK_MARKER}: 원작 {unit_name}({unit_id})의 미수록 스킬 {len(rows)}개를 통째로 "
        f"이 유닛에 배정했다(개별 대응 아님, 등급 안 DPS 순위 매칭)."
    ]
    for r in rows:
        lines.append(
            f"  · {r['스킬트리거']}#{r['RRD순번']}({r['gate'] or '게이트 없음'}): "
            f"계산식 {r['계산식'][:60]}"
        )
    if range_note:
        lines.append(range_note)
    lines.append(
        " hitCount>1인 항목은 \"같은 식이 원작에서 N번 반복\"이지 우리 스키마의 시간차 "
        "다단히트가 아니다 — duration=0(즉시 연속 N회)으로 근사했다."
    )
    return ' '.join(lines)


HEAD = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __SCRIPT__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def load_roster():
    roster = []
    for path in sorted(glob.glob(f'{ROSTER_DIR}/*.asset')):
        text = open(path, encoding='utf-8').read()
        grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
        ap = float(re.search(r'^  attackPower: ([\d.]+)', text, re.M).group(1))
        aspd = float(re.search(r'^  attackSpeed: ([\d.]+)', text, re.M).group(1))
        base = path.split('/')[-1][:-len('.asset')]
        has_skill = not re.search(r'^  skill: \{fileID: 0\}', text, re.M)
        claimed = base in CLAIMED_BASES
        roster.append(dict(path=path, text=text, base=base, grade=grade, dps=ap * aspd,
                            has_skill=has_skill, claimed=claimed))
    return roster


def skill_asset_path(unit_base):
    name = f'SkillData_원작능력_{unit_base}'
    return f'{SKILL_DIR}/{name}.asset', name


def append_to_existing(path, effects_yaml, note):
    text = open(path, encoding='utf-8').read()
    if STACK_MARKER in text:
        return False
    text = text.rstrip('\n') + '\n' + effects_yaml
    text = re.sub(r'^(  description: .*)$', lambda m: m.group(1) + ' ' + note, text, count=1, flags=re.M)
    open(path, 'w', encoding='utf-8').write(text)
    return True


def build_new_asset(unit_base, effects_yaml, note, unit_name, range_value):
    path, name = skill_asset_path(unit_base)
    guid = guid_for(name)
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {unit_name} (미수록 스킬 묶음)\n"
        + f"  description: {note}\n"
        + "  triggerType: 0\n"
        + "  levels:\n"
        + "  - cooldown: 0\n"
        + "    triggerChance: 1.0\n"
        + f"    range: {range_value}\n"
        + "    effects:\n"
        + effects_yaml
    )
    open(path, 'w', encoding='utf-8').write(body)
    write_meta(path, guid)
    return guid


def main():
    by_unit, excluded_non_numeric = load_confirmed_groups()
    roster = load_roster()

    grade_groups = defaultdict(list)
    for uid, rows in by_unit.items():
        grade_groups[rows[0]['등급']].append((uid, rows))
    for g in grade_groups:
        grade_groups[g].sort(key=lambda item: unit_score(item[1]), reverse=True)

    created, stacked = 0, 0
    skipped_grades = []
    range_conflicts_with_existing = []

    for gname, genum in GRADE_ENUM.items():
        originals = grade_groups.get(gname, [])
        eligible = sorted(
            [u for u in roster if u['grade'] == genum and not u['claimed']],
            key=lambda u: -u['dps'],
        )
        n = min(len(originals), len(eligible))
        if n == 0:
            if originals:
                skipped_grades.append((gname, len(originals), len(eligible)))
            continue

        for i in range(n):
            uid, rows = originals[i]
            unit = eligible[i]
            unit_name = rows[0]['유닛이름']

            range_value, range_note = resolve_range(rows)
            effects_yaml = ''
            for r in rows:
                effects_yaml += render_effect(r, range_note is not None)

            note = describe_unit(uid, unit_name, rows, range_note)

            path, _ = skill_asset_path(unit['base'])
            if unit['has_skill']:
                # range를 기존 레벨에 맞출 수 없으면(다른 range가 이미 있으면) 새 값으로
                # 덮지 않는다 — 기존 효과가 그 range로 의미가 있을 수 있어서다. 대신
                # range가 필요한 Enemies 효과가 있는데 기존 range가 0(SingleTarget 전용
                # 상태)이면 새 range로 채운다. 기존 range가 이미 있고 이번 값과 다르면
                # 조용히 덮지 않고 목록으로 남긴다.
                existing_text = open(path, encoding='utf-8').read()
                existing_range = re.search(r'\n    range: ([\d.]+)\n', existing_text)
                existing_val = float(existing_range.group(1)) if existing_range else 0.0
                if existing_val == 0.0 and range_value > 0:
                    existing_text = re.sub(
                        r'(\n    range: )[\d.]+\n', rf'\g<1>{range_value}\n', existing_text, count=1,
                    )
                    open(path, 'w', encoding='utf-8').write(existing_text)
                elif range_value > 0 and existing_val != range_value:
                    range_conflicts_with_existing.append((unit['base'], existing_val, range_value))
                if append_to_existing(path, effects_yaml, note):
                    stacked += 1
            else:
                guid = build_new_asset(unit['base'], effects_yaml, note, unit_name, range_value)
                new_roster_text = re.sub(
                    r'^  skill: \{fileID: 0\}$',
                    f'  skill: {{fileID: 11400000, guid: {guid}, type: 2}}',
                    unit['text'], count=1, flags=re.M,
                )
                open(unit['path'], 'w', encoding='utf-8').write(new_roster_text)
                created += 1

    total_skills = sum(len(rows) for units in grade_groups.values() for _, rows in units)
    print(f"기존 스킬에 효과 추가 {stacked}종, 새 SkillData 생성 {created}종 "
          f"(합계 {stacked + created}종, 원작 스킬 {total_skills}개 중 매칭분)")
    print(f"\n제외(range 그룹변수, 비수치) {len(excluded_non_numeric)}행:")
    for r in excluded_non_numeric:
        print(f"  {r['유닛ID']} {r['스킬트리거']}#{r['RRD순번']} — {r['range']}")
    if skipped_grades:
        print("\n등급 불일치(원작은 있는데 가용 유닛 없음):")
        for gname, no, ne in skipped_grades:
            print(f"  {gname}: 원작 {no}종 / 가용 {ne}종")
    if range_conflicts_with_existing:
        print("\n⚠️ 기존 range와 충돌(안 덮음, 새 Enemies 효과가 기존 range를 씀):")
        for base, existing_val, new_val in range_conflicts_with_existing:
            print(f"  {base}: 기존 {existing_val} vs 이번 {new_val}")


if __name__ == '__main__':
    main()
