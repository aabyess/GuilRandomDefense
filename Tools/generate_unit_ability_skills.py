#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""원작 유닛×능력표의 수치 있는 132행(dedup 후 131행)을 등급별 DPS 순위로 로스터에 배정한다.

PM 지시(2026-09-05) — "오늘 스탯·사거리에 쓴 방식 그대로": 이름매핑은 이미 불가로 닫힌
문제라([[name-mapping-impossible]]) 개별 대응이 아니라 분포 보존으로 우회한다.
  - 원작 쪽: 등급 안에서 "능력 기대값"(발동확률 × (배수×기준공격력 + 추가피해)) 내림차순
  - 우리 쪽: 같은 등급 유닛을 DPS(attackPower×attackSpeed) 내림차순
  둘을 순서대로 짝짓는다. 원작 유닛ID·능력ID·이름은 전부 생성된 SkillData 에셋
  description에 그대로 남겨 사장님이 나중에 재배정할 수 있게 한다(9f1ff34·0753a23와 같은 관례).

Docs/reference/ORIGINAL_UNIT_ABILITIES.csv의 세 열이 우리 스키마에 그대로 대응한다:
  발동확률(Hbh1) -> SkillLevel.triggerChance   (없으면 우리 필드 기본값 1.0 그대로 둔다)
  피해배수(Hbh2) -> SkillEffect.multiplier     (없으면 0 — "필드 없음=0"과 같은 원칙)
  추가피해(Hbh3) -> SkillEffect.bonus          (없으면 0)
basis=CasterAttackPower(시전자 평타 공격력×배수+추가피해), triggerType=OnHitChance,
target=SingleTarget(TryCastOnHitSkill이 이미 골라준 "방금 맞은 대상"을 그대로 쓴다 —
Enemies로 하면 사거리 안 전체를 때려 원작에 없는 광역기가 된다).

damageType은 AD로 통일한다(PM 지시) — 원작 능력이 마법인지 물리인지 이 CSV엔 없다
(리서치담당이 트리거에서 별도 조사 중). attackType은 이 유닛 자신의 평타 타입을 그대로
쓴다("평타와 같은 값이 기본") — DamageTable이 물리 상성 행을 탈 때 그 유닛의 실제 평타
종류(normal/pierce/siege/hero)와 어긋나지 않게 하려는 것이다.

⚠️ 이미 06번①(스킬승급형·능력교체형, `Assets/Data/UnitSkills/SkillData_원작0XX_*.asset`
23개)이 선점한 유닛은 건너뛴다 — 스킬승급형(직접 `UnitData.skill` 참조, 15종)은 덮어쓰면
그 트랙 전체가 끊긴다. 능력교체형(트레잇의 `replacementSkill`, 8종)은 아직 base skill이
비어 있어 이번 배정 대상에 넣었다 — 원작 구조상 "능력교체형"은 원래 능력이 있다가
트레잇으로 갈아끼워지는 것이라, 이번에 받는 기본 능력과 트레잇의 교체 능력이 같은 층위에
공존하는 게 맞다(교체 전/후).

CSV에 (유닛ID, 능력ID) 완전 중복 행이 1개 있었다(h00F 하찌 A06P, 흔함 등급) — 중복을
빼서 132행이 아니라 131행으로 배정한다(PM 보고분과 흔함 카운트가 7→6으로 하나 달라지는
이유).

⚠️ 37행은 발동확률만 있고 피해배수·추가피해가 둘 다 0이다(능력이름을 보면 "!순속"·
"!바닷물 가두기"처럼 이동/구속형이라 애초에 피해 능력이 아닌 경우가 많다) — 그래도 PM이
지정한 132행 범위 안이라 배정하되, 생성된 에셋 description에 "피해 0 — 원작이 비피해
효과였을 수 있다"를 명시한다. 지어내지 않고 그대로 옮긴다.

재실행해도 guid는 안 바뀐다(우리 유닛 파일명 기준 고정) — 이미 있으면 덮어쓰되 guid 유지.
"""
import csv
import glob
import hashlib
import re

SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'  # SkillData.cs

CSV_PATH = 'Docs/reference/ORIGINAL_UNIT_ABILITIES.csv'
ROSTER_DIR = 'Assets/Data/Units/Roster'
OUT_DIR = 'Assets/Data/UnitSkills'

GRADE_ENUM = {
    '흔함': 0, '특별함': 2, '희귀함': 3, '전설적인': 5, '제한됨': 6,
    '초월함': 7, '불멸의': 8, '영원한': 9, '랜덤전용': 10, '특수함': 12, '변화된': 14,
}

REFERENCE_ATTACK = 50.0  # 원작 능력 랭킹용 임시 기준값 — 실제 저장값엔 안 쓴다, 정렬 전용.

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


def guid_for(name):
    return hashlib.md5(('guilrd/unitabilityskills/' + name).encode()).hexdigest()


def write_asset(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def has(v):
    return v.strip() != ''


def nz(v):
    v = v.strip()
    if v == '':
        return False
    try:
        return float(v) != 0.0
    except ValueError:
        return True


def fnum(v):
    v = v.strip()
    return float(v) if v else 0.0


def ability_score(row):
    trig = fnum(row['발동확률']) / 100.0 if has(row['발동확률']) else 1.0
    return trig * (REFERENCE_ATTACK * fnum(row['피해배수']) + fnum(row['추가피해']))


def load_csv_rows():
    with open(CSV_PATH, encoding='utf-8') as f:
        rows = list(csv.DictReader(f))

    filtered = [r for r in rows if nz(r['발동확률']) or nz(r['피해배수']) or nz(r['추가피해'])]

    seen = set()
    uniq = []
    dup_count = 0
    for r in filtered:
        key = (r['유닛ID'], r['능력ID'])
        if key in seen:
            dup_count += 1
            continue
        seen.add(key)
        uniq.append(r)

    by_grade = {}
    for r in uniq:
        by_grade.setdefault(r['등급'], []).append(r)
    for g in by_grade:
        by_grade[g].sort(key=ability_score, reverse=True)

    return by_grade, dup_count, len(uniq)


def load_roster():
    roster = []
    for path in sorted(glob.glob(f'{ROSTER_DIR}/*.asset')):
        text = open(path, encoding='utf-8').read()
        grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
        ap = float(re.search(r'^  attackPower: ([\d.]+)', text, re.M).group(1))
        aspd = float(re.search(r'^  attackSpeed: ([\d.]+)', text, re.M).group(1))
        # 초월위습(grade 13)처럼 전투 곡선 대상이 아닌 유닛은 attackType 자체가 없다 —
        # 어차피 GRADE_ENUM 대상이 아니라 고를 일이 없으니 0(Unassigned)으로 흘려보낸다.
        attack_type_match = re.search(r'^  attackType: (\d+)', text, re.M)
        attack_type = int(attack_type_match.group(1)) if attack_type_match else 0
        claimed = not re.search(r'^  skill: \{fileID: 0\}', text, re.M)
        roster.append(dict(path=path, text=text, grade=grade, dps=ap * aspd,
                            attack_type=attack_type, claimed=claimed))
    return roster


def build_skill_asset(unit_path, row):
    base = unit_path.split('/')[-1][:-len('.asset')]
    name = f'SkillData_원작능력_{base}'
    guid = guid_for(name)

    trig_present = has(row['발동확률'])
    # round(): 원본 CSV 추출 과정의 float32 잔여 오차(예: 3.1500000953674316)를 정리한다 —
    # 값 자체를 바꾸는 게 아니라 표기만 정리한다.
    trigger_chance = round(fnum(row['발동확률']) / 100.0, 4) if trig_present else 1.0
    multiplier = round(fnum(row['피해배수']), 4)
    bonus = round(fnum(row['추가피해']), 4)
    no_damage = multiplier == 0.0 and bonus == 0.0
    note = (
        " ⚠️ 피해배수·추가피해가 둘 다 0 — 원작 능력 자체가 피해가 아니라 다른 효과"
        "(이동·구속 등)였을 수 있다 — 지어내지 않고 그대로 옮겼다."
    ) if no_damage else ''

    description = (
        f"원작 능력표(ORIGINAL_UNIT_ABILITIES.csv) DPS 순위 배정 — "
        f"원작 {row['등급']} {row['유닛이름']}({row['능력ID']} {row['능력이름']})의 "
        f"발동확률·피해배수·추가피해를 그대로 옮겼다. 개별 대응 아님(이름매핑 불가, "
        f"순위 배정으로 우회) — 등급 안에서 원작은 능력 기대값, 우리는 DPS(공격력×공속) "
        f"내림차순으로 정렬해 순서대로 짝지었다. damageType은 AD로 통일(원작 스킬의 "
        f"마법 여부는 CSV에 없음, 리서치담당 조사 중) — attackType은 이 유닛의 평타 "
        f"타입을 그대로 썼다.{note}"
    )

    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {row['능력ID']} {row['능력이름']}\n"
        + f"  description: {description}\n"
        + "  triggerType: 0\n"
        + "  levels:\n"
        + "  - cooldown: 0\n"
        + f"    triggerChance: {trigger_chance}\n"
        + "    range: 0\n"
        + "    effects:\n"
        + "    - kind: 0\n"
        + "      basis: 3\n"
        + "      target: 3\n"
        + "      damageType: 1\n"
        + f"      attackType: {row['_attackType']}\n"
        + f"      multiplier: {multiplier}\n"
        + f"      bonus: {bonus}\n"
        + "      chance: 1\n"
        + "      hitCount: 1\n"
        + "      duration: 0\n"
    )
    return name, guid, body


def main():
    by_grade, dup_count, uniq_count = load_csv_rows()
    roster = load_roster()

    made = 0
    skipped_grades = []

    for gname, genum in GRADE_ENUM.items():
        ability_rows = by_grade.get(gname, [])
        eligible = sorted(
            [u for u in roster if u['grade'] == genum and not u['claimed']],
            key=lambda u: -u['dps'],
        )
        n = min(len(ability_rows), len(eligible))
        if n == 0:
            skipped_grades.append(gname)
            continue

        for i in range(n):
            unit = eligible[i]
            row = dict(ability_rows[i])
            row['_attackType'] = unit['attack_type']

            name, guid, body = build_skill_asset(unit['path'], row)
            write_asset(f'{OUT_DIR}/{name}.asset', body, guid)

            new_text = re.sub(
                r'^  skill: \{fileID: 0\}$',
                f'  skill: {{fileID: 11400000, guid: {guid}, type: 2}}',
                unit['text'], count=1, flags=re.M,
            )
            open(unit['path'], 'w', encoding='utf-8').write(new_text)
            made += 1

    print(f"CSV 원본 필터 132행 -> 중복 {dup_count}건 제거 -> 고유 {uniq_count}행")
    print(f"SkillData {made}개 생성, 로스터 {made}종에 skill 배선 완료 ({OUT_DIR}/)")
    if skipped_grades:
        print(f"배정 0건 등급(가용 유닛 또는 능력 없음): {', '.join(skipped_grades)}")


if __name__ == '__main__':
    main()
