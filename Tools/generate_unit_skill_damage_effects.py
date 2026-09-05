#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""2차 채널(트리거·더미 위임 피해) 195행을 등급별 DPS 순위로 로스터에 쌓는다.

PM 지시(2026-09-05) — 1차(Docs/reference/ORIGINAL_UNIT_ABILITIES.csv, uabi 능력ID 격자)와는
다른 채널이다. 원작이 `유닛ID → 트리거 해시테이블`로 평타 스킬 대부분을 처리해서 능력ID로는
안 보이던 208행을 리서치담당이 전수로 뽑았다(Docs/reference/ORIGINAL_SKILL_DAMAGE_TABLE.csv,
커밋 9d9ad64) — 그중 13행(basis대응="❌ 스키마에 없음")은 건너뛰고 195행만 쓴다.

배정 방법은 1차와 같다 — 등급 안에서 원작은 능력 기대값 내림차순, 우리는 로스터 유닛
DPS(공격력×공속) 내림차순으로 정렬해 순서대로 짝짓는다. 06번①(스킬승급형·능력교체형)이
선점한 15종은 이번에도 제외한다.

⚠️ 1차와 겹치는 유닛이 나온다(원작은 유닛 하나가 평균 4.5개 능력을 가진다) — 그 경우
기존 `SkillData.levels[0].effects`에 새 효과를 **추가**한다(1차 것을 덮지 않는다). 1차가
없는 유닛은 1차와 같은 이름 규칙(`SkillData_원작능력_<유닛파일명>`)으로 새로 만든다 —
guid도 1차와 같은 함수로 나오므로 재실행해도 고정이다.

## 컬럼 → 스키마 대응 (basis대응 그대로 사용)
  ✅ Flat / ✅ Flat(값 그대로)                         → basis=Flat, multiplier=값, bonus=0
  ✅ TargetMaxHpPercent / TargetCurrentHpPercent      → basis=그대로, multiplier=계수, bonus=상수항
     (계산식이 "상수+HP×계수" 또는 "HP×계수+상수" 꼴이면 우리 스키마의
      multiplier×basis+bonus에 그대로 들어맞는다 — 별도 분해가 필요 없다)
  ✅ …+계수(A11S식)                                    → A11S(대상별 스킬피해 감수성 레벨)를
     보스 기준(레벨16 → 0.20+0.05×16 = ×1.00)으로 고정해 계수만 뽑는다. 원작은 잡몹×0.90·
     소환체×0.25까지 이 값을 벌려놨는데(BOSS_COMBAT_RESEARCH.md) 우리는 대상별 감수성 축
     자체가 없어서 보스 기준(가장 관대한 값, ×1.00)으로 근사한다 — 잡몹·소환체 대상에겐
     실제보다 최대 4배 과다 계상될 수 있다. 지어낸 축을 새로 만들지 않고 근사만 남긴다.
  ✅ TargetMaxHpPercent+TargetCurrentHpPercent (1건, h030) → 계산식이
     "2450000+((최대체력-현재체력)×0.04)"라 basis 하나로 못 담는다 — Flat(2,450,000) +
     TargetMaxHpPercent(0.04) + TargetCurrentHpPercent(-0.04) 세 효과로 손으로 분해했다
     (합쳐지면 원래 식과 동일).

## 공격타입/피해타입
이 CSV는 1차와 달리 행마다 실제 공격타입이 있다(NORMAL/SIEGE/HERO/CHAOS/MELEE/MAGIC).
MAGIC은 damageType=AP, 나머지는 전부 AD로 옮긴다(MELEE는 우리 AttackType에 없어 Normal로
근사). **더미 행 86개는 공격타입이 전부 [미확인]**이다 — 1차와 같은 fallback(이 유닛 자신의
평타 타입, damageType=AD)을 쓰고 description에 불확실성을 남긴다.

⚠️ '피해타입'(UNIVERSAL/NORMAL/UNKNOWN) 열은 옮기지 않는다 — 원작 WC3의 "방어력 무시(참피해)"
축인데 우리 DamageTable/방어 감쇄 체계엔 대응하는 자리가 없다(관통방어 미구현과 같은 종류의
갭). 지어내지 않고 이 스크립트 설명에만 남긴다 — 스키마를 늘리지 않는다(PM 지시).

⚠️ 더미 행의 스톡 필드 값(예: Oae1=-0.05)은 슬로우·스턴·버프처럼 애초에 피해가 아닐 수
있다(필드뜻 자체가 [미확인]) — 1차의 "피해 0" 케이스와 같은 이유로, 지어내지 않고 값만
옮기되 description에 "필드뜻 미확인 — 피해가 아닌 다른 효과였을 수 있다"를 남긴다.

재실행해도 같은 유닛엔 같은 효과가 다시 안 쌓이도록, 이미 이 스크립트가 붙인 자리는
description의 마커 문자열로 걸러 건너뛴다.
"""
import csv
import glob
import hashlib
import re

SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'  # SkillData.cs

CSV_PATH = 'Docs/reference/ORIGINAL_SKILL_DAMAGE_TABLE.csv'
ROSTER_DIR = 'Assets/Data/Units/Roster'
OUT_DIR = 'Assets/Data/UnitSkills'

GRADE_ENUM = {
    '흔함': 0, '특별함': 2, '희귀함': 3, '전설적인': 5, '제한됨': 6,
    '초월함': 7, '불멸의': 8, '영원한': 9, '랜덤전용': 10, '특수함': 12, '변화된': 14,
}

ATTACK_TYPE_ENUM = {
    'NORMAL': 1, 'PIERCE': 2, 'SIEGE': 3, 'HERO': 4, 'CHAOS': 5, 'MAGIC': 6, 'SPELLS': 7,
    'MELEE': 1,  # 우리 AttackType에 근접 전용 분류가 없다 — Normal로 근사.
}

REFERENCE_HP = 1_000_000.0  # 정렬 전용 임시 기준값 — 저장값엔 안 쓴다.
STACK_MARKER = '2차 배정(트리거·더미 채널)'

HP_TERM_SRC = r'GetUnitStateSwap\(UNIT_STATE_(?:MAX_LIFE|LIFE),\s*Get\w+Unit\(\)\)'
A11S_TERM_SRC = r"I2R\(GetUnitAbilityLevelSwapped\('A11S',\s*Get\w+Unit\(\)\)\)"
HP_PATTERN = re.compile(
    r'^\(*(?:(?P<bonus_pre>[0-9.]+)\+\(*)?' + HP_TERM_SRC +
    r'\*\(?(?P<coef>[0-9.]+)\)?\)*'
    r'(?:\*\(?(?P<extra>[0-9.]+)\)?)?\)*'
    r'(?:\+\(?(?P<bonus_post>[0-9.]+)\)?)?\)*$'
)
A11S_HP_PATTERN = re.compile(
    r'^\(*' + HP_TERM_SRC + r'\*\(?(?P<coef>[0-9.]+)\)?\)*'
    r'\*\(0\.\d+\+\(0\.\d+\*' + A11S_TERM_SRC + r'\)\)\)*$'
)
A11S_FLAT_PATTERN = re.compile(
    r'^\(\(?(?P<coef>[0-9.]+)\)?\*\(0\.\d+\+\(0\.\d+\*' + A11S_TERM_SRC + r'\)\)\)$'
)
DUMMY_FIELD_PATTERN = re.compile(r'=\s*(-?[0-9.]+)')

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


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def extract_effects(row):
    """returns list of (basis:int, multiplier:float, bonus:float)."""
    b = row['basis대응']
    calc = row['계산식'].strip()

    if b == '✅ Flat':
        return [(0, float(calc), 0.0)]

    if b == '✅ Flat(값 그대로)':
        m = DUMMY_FIELD_PATTERN.search(calc)
        assert m, calc
        return [(0, float(m.group(1)), 0.0)]

    if b in ('✅ TargetMaxHpPercent', '✅ TargetCurrentHpPercent'):
        m = HP_PATTERN.match(calc)
        assert m, calc
        coef = float(m.group('coef')) * float(m.group('extra') or 1)
        bonus = float(m.group('bonus_pre') or m.group('bonus_post') or 0)
        basis = 1 if b == '✅ TargetMaxHpPercent' else 2
        return [(basis, coef, bonus)]

    if b in ('✅ TargetCurrentHpPercent+계수(A11S식)', '✅ TargetMaxHpPercent+계수(A11S식)'):
        m = A11S_HP_PATTERN.match(calc)
        assert m, calc
        basis = 2 if 'Current' in b else 1
        return [(basis, float(m.group('coef')), 0.0)]  # A11S=16(보스) 가정, 계수 1.0 흡수

    if b == '✅ 계수(A11S식)':
        m = A11S_FLAT_PATTERN.match(calc)
        assert m, calc
        return [(0, float(m.group('coef')), 0.0)]

    if b == '✅ TargetMaxHpPercent+TargetCurrentHpPercent':
        # h030 전용 — "2450000+((최대체력-현재체력)*0.04)"를 세 효과로 분해.
        return [(0, 2450000.0, 0.0), (1, 0.04, 0.0), (2, -0.04, 0.0)]

    raise ValueError(f'처리 못한 basis대응: {b} ({row["유닛ID"]})')


def score(row):
    total = 0.0
    for basis, mult, bonus in extract_effects(row):
        total += (mult + bonus) if basis == 0 else (mult * REFERENCE_HP + bonus)
    return total


def resolve_attack_type(row, unit_attack_type):
    """returns (attackType:int, damageType:int, uncertain:bool)."""
    csv_type = row['공격타입'].strip()
    if csv_type in ATTACK_TYPE_ENUM:
        damage_type = 2 if csv_type == 'MAGIC' else 1
        return ATTACK_TYPE_ENUM[csv_type], damage_type, False
    return unit_attack_type, 1, True


def describe(row, uncertain_attack):
    origin_note = (
        f"{row['출처']}" if row['회수경로'] == '트리거' else f"더미 경로 {row['출처']}"
    )
    dummy_note = ''
    if row['basis대응'] == '✅ Flat(값 그대로)':
        dummy_note = ' ⚠️ 스톡 필드 값 그대로 — 필드 뜻이 미확인이라 피해가 아니라 다른 효과(슬로우·스턴·버프 등)였을 수 있다.'
    attack_note = ''
    if uncertain_attack:
        attack_note = ' 공격타입 원본이 [미확인]이라 이 유닛 자신의 평타 타입으로 대신했다.'
    a11s_note = ''
    if 'A11S' in row['basis대응']:
        a11s_note = ' A11S(대상별 스킬피해 감수성) 계수는 보스 기준(×1.00)으로 고정 — 잡몹·소환체 대상엔 과다 계상될 수 있다.'

    return (
        f" | {STACK_MARKER}: 원작 {row['등급']} {row['유닛이름']}({origin_note})."
        f"{dummy_note}{attack_note}{a11s_note}"
    )


def render_effect_block(basis, multiplier, bonus, attack_type, damage_type):
    return (
        "    - kind: 0\n"
        f"      basis: {basis}\n"
        "      target: 3\n"
        f"      damageType: {damage_type}\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(multiplier, 4)}\n"
        f"      bonus: {round(bonus, 4)}\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
    )


def build_new_asset(unit_base, row, attack_type, damage_type, uncertain_attack):
    name = f'SkillData_원작능력_{unit_base}'
    guid = guid_for(name)
    origin_label = row['출처'] if row['회수경로'] == '트리거' else f"더미:{row['출처']}"
    description = (
        f"원작 트리거·더미 피해표(ORIGINAL_SKILL_DAMAGE_TABLE.csv) DPS 순위 배정 — "
        f"원작 {row['등급']} {row['유닛이름']}({origin_label})의 피해를 옮겼다. 1차(uabi "
        f"능력ID) CSV엔 없던 능력이다 — 원작이 유닛ID→트리거 해시테이블로 처리해서 "
        f"능력ID로는 안 보였다.{describe(row, uncertain_attack).replace(f' | {STACK_MARKER}: 원작', ' 개별 대응 아님(이름매핑 불가) — 원작')}"
    )
    effects = ''.join(
        render_effect_block(basis, mult, bonus, attack_type, damage_type)
        for basis, mult, bonus in extract_effects(row)
    )
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {row['출처']}\n"
        + f"  description: {description}\n"
        + "  triggerType: 0\n"
        + "  levels:\n"
        + "  - cooldown: 0\n"
        + "    triggerChance: 1.0\n"
        + "    range: 0\n"
        + "    effects:\n"
        + effects
    )
    return name, guid, body


def append_to_existing(path, row, attack_type, damage_type, uncertain_attack):
    text = open(path, encoding='utf-8').read()
    if STACK_MARKER in text:
        return False  # 재실행 — 이미 붙어 있다.

    effects = ''.join(
        render_effect_block(basis, mult, bonus, attack_type, damage_type)
        for basis, mult, bonus in extract_effects(row)
    )
    # 기존 effects 리스트 맨 뒤에 새 항목을 이어붙인다 — 다음 필드(다른 level 없음, 파일 끝)
    # 전까지 들여쓰기 "    - kind:"로 시작하는 블록들 뒤에 삽입.
    text = text.rstrip('\n') + '\n' + effects
    text = re.sub(
        r'^(  description: .*)$',
        lambda m: m.group(1) + describe(row, uncertain_attack),
        text, count=1, flags=re.M,
    )
    open(path, 'w', encoding='utf-8').write(text)
    return True


def load_csv():
    with open(CSV_PATH, encoding='utf-8') as f:
        rows = list(csv.DictReader(f))
    skipped = [r for r in rows if r['basis대응'] == '❌ 스키마에 없음']
    included = [r for r in rows if r['basis대응'] != '❌ 스키마에 없음']

    by_grade = {}
    for r in included:
        by_grade.setdefault(r['등급'], []).append(r)
    for g in by_grade:
        by_grade[g].sort(key=score, reverse=True)
    return by_grade, skipped


def load_roster():
    pass1_names = set()
    for p in glob.glob(f'{OUT_DIR}/SkillData_원작능력_*.asset'):
        base = p.split('/')[-1][len('SkillData_원작능력_'):-len('.asset')]
        pass1_names.add(base)

    roster = []
    for path in sorted(glob.glob(f'{ROSTER_DIR}/*.asset')):
        text = open(path, encoding='utf-8').read()
        grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
        ap = float(re.search(r'^  attackPower: ([\d.]+)', text, re.M).group(1))
        aspd = float(re.search(r'^  attackSpeed: ([\d.]+)', text, re.M).group(1))
        at_match = re.search(r'^  attackType: (\d+)', text, re.M)
        attack_type = int(at_match.group(1)) if at_match else 0
        has_skill = not re.search(r'^  skill: \{fileID: 0\}', text, re.M)
        base = path.split('/')[-1][:-len('.asset')]
        has_pass1 = base in pass1_names
        claimed_by_other = has_skill and not has_pass1  # 06번① 등 이 스크립트 밖에서 채운 것

        roster.append(dict(path=path, text=text, grade=grade, dps=ap * aspd,
                            attack_type=attack_type, has_pass1=has_pass1,
                            claimed=claimed_by_other, base=base))
    return roster


def main():
    by_grade, skipped = load_csv()
    roster = load_roster()

    stacked = 0
    created = 0

    for gname, genum in GRADE_ENUM.items():
        ability_rows = by_grade.get(gname, [])
        eligible = sorted(
            [u for u in roster if u['grade'] == genum and not u['claimed']],
            key=lambda u: -u['dps'],
        )
        n = min(len(ability_rows), len(eligible))

        for i in range(n):
            unit = eligible[i]
            row = ability_rows[i]
            attack_type, damage_type, uncertain = resolve_attack_type(row, unit['attack_type'])

            if unit['has_pass1']:
                path = f"{OUT_DIR}/SkillData_원작능력_{unit['base']}.asset"
                if append_to_existing(path, row, attack_type, damage_type, uncertain):
                    stacked += 1
            else:
                name, guid, body = build_new_asset(unit['base'], row, attack_type, damage_type, uncertain)
                open(f'{OUT_DIR}/{name}.asset', 'w', encoding='utf-8').write(body)
                write_meta(f'{OUT_DIR}/{name}.asset', guid)

                new_text = re.sub(
                    r'^  skill: \{fileID: 0\}$',
                    f'  skill: {{fileID: 11400000, guid: {guid}, type: 2}}',
                    unit['text'], count=1, flags=re.M,
                )
                open(unit['path'], 'w', encoding='utf-8').write(new_text)
                created += 1

    print(f"2차 채널: 기존 스킬에 효과 추가 {stacked}건, 새 SkillData 생성 {created}건")
    print(f"건너뛴 13행(스키마에 없음):")
    for r in skipped:
        print(f"  {r['유닛ID']} {r['등급']} {r['유닛이름']} | {r['출처']} | basis축={r['basis축']}")


if __name__ == '__main__':
    main()
