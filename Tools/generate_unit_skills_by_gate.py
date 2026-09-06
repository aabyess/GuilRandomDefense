#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""원작 스킬(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv 판정=확정, 지금 236행)을 게이트 단위로
쪼개 UnitData.skills 리스트에 배정한다.

PM 지시(2026-09-06) — "합쳐 넣으면 게이트가 뭉개진다"(2a8cb51의 166건은 전부 게이트별로
다시 쪼개야 한다는 걸 발견): **원작 능력(=게이트) 하나 → SkillData 하나 → skills 리스트
항목 하나.** 같은 게이트를 공유하는 RRD 여러 개는 한 SkillData의 effects 안에 그대로
둔다(원작도 같은 게이트라 맞다) — 게이트가 다르면 반드시 별도 SkillData.

기존 단일 `skill` 필드는 절대 안 건드린다(하위호환, 06번①·1차·2차 채널이 그 자리를
쓴다) — 새로 배정하는 건 전부 `skills`로.

## gate 컬럼 파싱
`" AND "`로 쪼개면 다섯 원소 중 하나로 갈린다:
  - `(게이트 없음)` → OnHitChance, triggerChance=1.0
  - `숫자/숫자` (확률) → 그 분수
  - `Real<=숫자` → 숫자/100
  - `(MANA|LIFE)게이지숫자` → OnHitCount, hitCountThreshold=숫자, gaugeKind
  - `버프X==true` → 우리 축 밖, 조건을 버리고 description에 남긴다

**게이지 AND 확률은 이제 근사가 아니라 그대로 담는다**(PM 지시, 2026-09-06) —
구현담당3이 OnHitCount에도 임계 도달 후 `triggerChance` 검사를 추가하는 중이다(작업
시작 시점엔 아직 미착수 — 데이터는 먼저 채우고 커밋 직전에 코드 상태를 재확인한다).
확률에 실패해도 게이지는 리셋된다(원작 순서) — 그게 `resetTo`가 이미 하는 일이라
추가로 손댈 코드는 없다. 순수 확률과 확률이 AND로 묶이면(예: `1/10 AND 1/33`) 곱해서
단일 `triggerChance`로 담는다 — 근사가 아니라 수학적으로 동치.

`버프X==true`는 여전히 못 담는다 — 그 조건만 버리고 나머지(게이지·확률)는 그대로
채운 뒤 description에 "버프 조건 무시, 실제보다 자주 발동할 수 있음" 꼬리표를 남긴다.

## gaugeKind 명시
⚠️ 지금까지 어떤 자산도 `gaugeKind`를 직렬화한 적이 없어(항상 생략) 전부 C# 기본값
Mana(0)에 기댔다 — 체력 게이지 스킬엔 반드시 `gaugeKind: 1`을 명시로 써야 한다.

## 배정 방법 (원작 유닛 단위 매칭은 그대로)
1. 유닛ID로 그룹 지어 등급 안 DPS 순위로 우리 유닛에 매칭(2a8cb51과 같은 원칙).
2. 매칭된 원작 유닛의 확정 행을 다시 `gate` 문자열로 서브그룹 지어 SkillData를 나눈다.
3. 06번① 스킬승급형 15종은 배정 풀에서 제외. 기존 skill 필드는 안 건드린다.
"""
import csv
import glob
import hashlib
import re
from collections import defaultdict

CSV_PATH = 'Docs/reference/ORIGINAL_UNLISTED_SKILL_EFFECTS.csv'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_DIR = 'Assets/Data/UnitSkills'
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'

REFERENCE_HP = 1_000_000.0

GRADE_ENUM = {
    '흔함': 0, '특별함': 2, '희귀함': 3, '히든': 4, '전설적인': 5, '제한됨': 6,
    '초월함': 7, '불멸의': 8, '영원한': 9, '랜덤전용': 10, '특수함': 12, '변화된': 14,
}

TARGET_MAP = {'SingleTarget': 3, 'Enemies': 2}
ATTACK_TYPE_MAP = {'Normal': 1, 'Pierce': 2, 'Siege': 3, 'Hero': 4, 'Chaos': 5, 'Magic': 6, 'Spells': 7}
DAMAGE_TYPE_MAP = {'AD': 1, 'AP(방어무시)': 2}

# basis 문자열 → (우리 SkillEffectBasis 정수, "근사" 메모 또는 None).
# ⚠️ 2026-09-06 이름 정정(PM 지시, 리서치담당 9250903 확인) — "×계수"는 예전에
# "A11S식 레벨/연구 비례"라고 불렀는데, **그건 비유가 아니라 틀린 이름이었다.**
# 대부분은 진짜 `A11S`(대상별 스킬피해 감수성 — 원작 §⑦ 스펙: 보스×1.00·잡몹×0.90·
# 광폭화 소환체×0.25) 그 자체다 — "레벨/연구"가 아니라 **대상(적)의 A11S 레벨**이
# 축이었다. 우리에 그 축(대상별 감수성 배율)이 없어서 값이 곱해지는 형태를 못 담는다.
# 능력레벨계수·영웅능력치는 여전히 다른 축(스킬 레벨·히어로 스탯)이 없는 별개 문제다.
# 그 곱해지는 항을 버리고 상수항(bonus)만 Flat으로 남긴다 — 실제보다 "덜 세게"는
# 될지언정 "더 세게"는 절대 안 되는 쪽으로 근사한다(과다 계상보다 과소 계상이
# 안전하다는 이번 세션 전체의 원칙).
# (basis정수, note_template 또는 None, a11s_scale 여부)
# ⚠️ 2026-09-06 PM 정정 — "곱해지는 항을 버리면 과소 계상만 된다"는 계수가 항상 1
# 이상일 때만 참이다. A11S 계수(A+B×레벨)는 레벨1 기준으로도 0.25~0.40 등 1 미만인
# 사례가 실제로 17건 있었다 — 버리면 그 반대(최대 4배 과다 계상)가 된다. 그래서
# ×계수(A11S) 세 종류는 "버리는" 대신 "레벨1 기준 계수를 곱해서 넣는다"(원작
# 레벨1과 정확히 같음 — 대상별 감수성 축 자체가 없다는 것 말고는 근사가 아니다).
# 능력레벨계수·영웅능력치는 덧셈형(상수+비례항)이라 비례항이 0 이상이면 버려도
# 과소만 되므로 그대로 상수항만 남긴다.
BASIS_DISPATCH = {
    'Flat': (0, None, False),
    '대상최대체력': (1, None, False),
    '대상현재체력': (2, None, False),
    '연구': (4, None, False),  # ResearchLevel — 2026-09-06 연구소 연결 완료(8f4c347), 실제 연구 레벨을 읽는다
    'ReceivedDamage': (5, ' ⚠️ ReceivedDamage는 읽는 코드가 아직 없다(SkillData.cs 주석) — 지금은 순수 데이터 자리, 발동해도 피해 0.', False),
    '대상최대체력×계수': (1, ' A11S(대상별 스킬피해 감수성) 계수({note})를 레벨1(=보스 기준) 값으로 고정해 곱했다 — 대상별 감수성 축이 우리에 없다.', True),
    '대상현재체력×계수': (2, ' A11S(대상별 스킬피해 감수성) 계수({note})를 레벨1(=보스 기준) 값으로 고정해 곱했다 — 대상별 감수성 축이 우리에 없다.', True),
    'Flat×계수': (0, ' A11S(대상별 스킬피해 감수성) 계수({note})를 레벨1(=보스 기준) 값으로 고정해 곱했다 — 대상별 감수성 축이 우리에 없다.', True),
    '능력레벨계수': (0, ' ⚠️ 능력 레벨 비례항(multiplier={mult}×레벨)은 레벨 축이 없어 빼고 상수항(bonus)만 Flat으로 옮겼다 — 실제보다 약할 수 있다.', False),
    '영웅능력치': (0, ' ⚠️ 영웅 능력치 비례항(multiplier={mult}×스탯)은 우리에 히어로 스탯 개념이 없어 빼고 상수항(bonus)만 Flat으로 옮겼다 — 실제보다 약할 수 있다.', False),
}


A11S_MEMO_RE = re.compile(r'계수 ([\d.]+)\+([\d.]+)×레벨')


def a11s_level1_baseline(memo):
    m = A11S_MEMO_RE.match(memo.strip())
    assert m, f"A11S 계수 메모 파싱 실패: {memo!r}"
    a, b = float(m.group(1)), float(m.group(2))
    return a + b * 1  # 레벨1 기준

CLAIMED_BASES = {
    '불멸_이이삭', '불멸_이승우', '불멸_박은석', '불멸_정준영', '불멸_정윤식', '영원_조세민',
    '초월_김만경_AD', '초월_박기찬_AD', '초월_유재헌_ADAP', '초월_임채민_AP', '초월_이태훈_AP',
    '초월_조성진_AD', '초월_임장혁_AD', '초월_최상호_AP', '초월_황준석_ADAP',
}


def fnum(s):
    s = s.strip()
    return float(s) if s else 0.0


def guid_for(name):
    return hashlib.md5(('guilrd/gateskills/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def resolve_gate(gate_str):
    """returns dict(kind='chance'|'gauge', triggerChance=, hitCountThreshold=, resetTo=,
    gaugeKind=) plus 'dropped' list of ignored sub-conditions (buff)."""
    gate_str = gate_str.strip()
    if gate_str == '(게이트 없음)':
        return dict(kind='chance', triggerChance=1.0), []

    parts = [p.strip() for p in gate_str.split(' AND ')]
    gauge = None
    prob_factors = []
    dropped = []

    for p in parts:
        m_gauge = re.match(r'(MANA|LIFE)게이지([\d.]+)$', p)
        m_frac = re.match(r'(\d+)/(\d+)$', p)
        m_real = re.match(r'Real<=([\d.]+)$', p)
        m_buff = re.match(r'버프(\w+)==true$', p)
        if m_gauge:
            # ⚠️ 2026-09-06 수정: 게이지 조건이 두 개 이상(예: LIFE게이지50 AND
            # MANA게이지85) 나오면 예전엔 마지막 것으로 덮어써서 앞의 게이지 조건이
            # 경고도 없이 통째로 사라졌다(h0BF 키쿄우에서 실제로 터짐 — LIFE50이 없어져
            # MANA85만 남아 원작보다 더 자주 발동하는 데이터가 나갔었다). 우리 스키마는
            # 게이지 축이 하나뿐이라 여전히 다 담을 순 없지만, 최소한 처음 나온 걸
            # 유지하고 나머지는 dropped에 남겨 description에서 보이게 한다.
            if gauge is None:
                gauge = (m_gauge.group(1), float(m_gauge.group(2)))
            else:
                dropped.append(p)
        elif m_frac:
            prob_factors.append(int(m_frac.group(1)) / int(m_frac.group(2)))
        elif m_real:
            prob_factors.append(float(m_real.group(1)) / 100.0)
        elif m_buff:
            dropped.append(p)
        else:
            raise ValueError(f'게이트 파싱 실패: {p!r} (전체: {gate_str!r})')

    combined_chance = 1.0
    for f in prob_factors:
        combined_chance *= f

    if gauge:
        gauge_kind = 0 if gauge[0] == 'MANA' else 1  # SkillGaugeKind: Mana=0, Life=1
        reset_to = 0 if gauge[0] == 'MANA' else 1  # 체력형은 0으로 두면 죽어서 1로 리셋
        return dict(kind='gauge', hitCountThreshold=int(gauge[1]), resetTo=reset_to,
                    gaugeKind=gauge_kind, triggerChance=round(combined_chance, 6)), dropped

    return dict(kind='chance', triggerChance=round(combined_chance, 6)), dropped


def unit_score(rows):
    total = 0.0
    for r in rows:
        if r['basis'] == 'Flat':
            total += fnum(r['bonus'])
        else:
            total += fnum(r['multiplier']) * REFERENCE_HP + fnum(r['bonus'])
    return total


def resolve_range(rows):
    enemy_rows = [r for r in rows if r['target'] == 'Enemies']
    ranges = sorted({fnum(r['range']) for r in enemy_rows})
    if len(ranges) <= 1:
        return (ranges[0] if ranges else 0.0), None
    chosen = ranges[0]
    note = (f" ⚠️ 이 게이트의 Enemies 스킬 반경이 여럿({', '.join(str(int(x)) for x in ranges)})이라 "
            f"가장 작은 {int(chosen)}로 통일했다.")
    return chosen, note


def render_effect(row):
    """returns (yaml_block, approximation_note_or_None)."""
    if row['basis'] not in BASIS_DISPATCH:
        raise ValueError(f"처리 못한 basis: {row['basis']!r} ({row['유닛ID']} {row['스킬트리거']}#{row['RRD순번']})")
    basis, note_template, a11s_scale = BASIS_DISPATCH[row['basis']]

    target = TARGET_MAP[row['target']]
    attack_type = ATTACK_TYPE_MAP[row['attackType']]
    damage_type = DAMAGE_TYPE_MAP[row['damageType']]
    hit_count = int(float(row['hitCount'])) if row['hitCount'].strip() else 1

    if basis == 0:
        multiplier = fnum(row['bonus'])
        bonus = 0.0
    else:
        multiplier = fnum(row['multiplier'])
        bonus = fnum(row['bonus'])

    if a11s_scale:
        scale = a11s_level1_baseline(row['축밖메모'])
        multiplier *= scale
        bonus *= scale

    note = None
    if note_template:
        note = note_template.format(note=row['축밖메모'], mult=row['multiplier'])
        note = f" [{row['스킬트리거']}#{row['RRD순번']}]" + note

    block = (
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
    return block, note


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


def build_skill_asset(name, gate_str, gate_resolved, dropped, rows, unit_name, unit_id, range_note):
    guid = guid_for(name)
    path = f'{SKILL_DIR}/{name}.asset'

    lines = [
        f"게이트별 배정(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv, 2026-09-06): 원작 {unit_name}"
        f"({unit_id})의 게이트 \"{gate_str}\" 능력 {len(rows)}개를 이 SkillData 하나로 묶었다."
    ]
    for r in rows:
        lines.append(f"  · {r['스킬트리거']}#{r['RRD순번']}: 계산식 {r['계산식'][:60]}")
    if dropped:
        lines.append(f" ⚠️ 조건({', '.join(dropped)})은 우리 축 밖(버프) 또는 축이 하나뿐(게이지 중복)이라 뺐다 — "
                      "실제보다 자주 발동할 수 있다.")
    if range_note:
        lines.append(range_note)
    lines.append(
        " hitCount>1은 \"같은 RRD 식의 반복\"이지 시간차 다단히트가 아니다 — duration=0"
        "(즉시 연속 N회)으로 근사했다."
    )

    effect_blocks = []
    for r in rows:
        block, note = render_effect(r)
        effect_blocks.append(block)
        if note:
            lines.append(note)
    description = ' '.join(lines)

    effects_yaml = ''.join(effect_blocks)
    range_value, _ = resolve_range(rows)

    if gate_resolved['kind'] == 'gauge':
        trigger_type = 3
        cooldown = 0
        trigger_chance = gate_resolved['triggerChance']
        hit_threshold = gate_resolved['hitCountThreshold']
        reset_to = gate_resolved['resetTo']
        gauge_kind = gate_resolved['gaugeKind']
    else:
        trigger_type = 0
        cooldown = 0
        trigger_chance = gate_resolved['triggerChance']
        hit_threshold = 0
        reset_to = 0
        gauge_kind = 0

    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {unit_name} — {gate_str}\n"
        + f"  description: {description}\n"
        + f"  triggerType: {trigger_type}\n"
        + "  levels:\n"
        + f"  - cooldown: {cooldown}\n"
        + f"    triggerChance: {trigger_chance}\n"
        + f"    range: {range_value}\n"
        + f"    hitCountThreshold: {hit_threshold}\n"
        + f"    resetTo: {reset_to}\n"
        + f"    gaugeKind: {gauge_kind}\n"
        + "    effects:\n"
        + effects_yaml
    )
    open(path, 'w', encoding='utf-8').write(body)
    write_meta(path, guid)
    return guid


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


def load_roster():
    roster = []
    for path in sorted(glob.glob(f'{ROSTER_DIR}/*.asset')):
        text = open(path, encoding='utf-8').read()
        grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
        ap = float(re.search(r'^  attackPower: ([\d.]+)', text, re.M).group(1))
        aspd = float(re.search(r'^  attackSpeed: ([\d.]+)', text, re.M).group(1))
        base = path.split('/')[-1][:-len('.asset')]
        claimed = base in CLAIMED_BASES
        roster.append(dict(path=path, text=text, base=base, grade=grade, dps=ap * aspd, claimed=claimed))
    return roster


def add_to_skills_list(roster_text, guid):
    # ⚠️ 2026-09-06 PM 지시(구현담당3 검사 #11이 실전 충돌 1건을 잡음) — skills가 하나라도
    # 있으면 UnitData.SkillCount/SkillAt이 skill을 아예 안 읽는다("skills가 비었을 때만"
    # 폴백한다). skill과 skills가 동시에 차 있으면 skill 쪽이 조용히 죽는다 — 그래서
    # skills를 처음 만드는 순간 기존 skill을 skills[0]으로 옮기고 skill은 반드시 비운다.
    if re.search(r'^  skills:\n', roster_text, re.M):
        m = re.search(r'^  skills:\n((?:  - .*\n)*)', roster_text, re.M)
        block = m.group(1)
        if f'guid: {guid},' in block:
            return roster_text  # 이미 들어있다 — 재실행 멱등성(중복 추가 방지).
        new_block = block + f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
        roster_text = roster_text[:m.start(1)] + new_block + roster_text[m.end(1):]
    else:
        existing_skill = re.search(r'^  skill: \{fileID: (\d+)(?:, guid: (\w+))?[^\n]*\}\n', roster_text, re.M)
        assert existing_skill, "skill: 필드를 못 찾음"
        existing_file_id = existing_skill.group(1)
        existing_guid = existing_skill.group(2)

        skills_entries = []
        if existing_file_id != '0':
            assert existing_guid, f"skill 필드가 채워져 있는데 guid가 없음: {existing_skill.group(0)!r}"
            skills_entries.append(f"  - {{fileID: 11400000, guid: {existing_guid}, type: 2}}\n")
        skills_entries.append(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n")

        roster_text = roster_text[:existing_skill.start()] + \
            "  skill: {fileID: 0}\n  skills:\n" + ''.join(skills_entries) + \
            roster_text[existing_skill.end():]
    return roster_text


def main():
    by_unit, excluded_non_numeric = load_confirmed_groups()
    roster = load_roster()

    grade_groups = defaultdict(list)
    for uid, rows in by_unit.items():
        grade_groups[rows[0]['등급']].append((uid, rows))
    for g in grade_groups:
        grade_groups[g].sort(key=lambda item: unit_score(item[1]), reverse=True)

    skills_added = 0
    units_touched = 0
    max_skills_per_unit = 0
    skipped_grades = []
    gate_parse_errors = []

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

            by_gate = defaultdict(list)
            for r in rows:
                by_gate[r['gate']].append(r)

            roster_text = unit['text']
            gate_count_this_unit = 0
            for gate_str, gate_rows in by_gate.items():
                try:
                    gate_resolved, dropped = resolve_gate(gate_str)
                except ValueError as e:
                    gate_parse_errors.append((uid, gate_str, str(e)))
                    continue

                range_value, range_note = resolve_range(gate_rows)
                gate_hash = hashlib.md5(gate_str.encode()).hexdigest()[:8]
                name = f'SkillData_게이트_{unit["base"]}_{gate_hash}'

                guid = build_skill_asset(name, gate_str, gate_resolved, dropped, gate_rows,
                                          unit_name, uid, range_note)
                roster_text = add_to_skills_list(roster_text, guid)
                skills_added += 1
                gate_count_this_unit += 1

            open(unit['path'], 'w', encoding='utf-8').write(roster_text)
            units_touched += 1
            max_skills_per_unit = max(max_skills_per_unit, gate_count_this_unit)

    print(f"유닛 {units_touched}종 처리, SkillData(게이트) {skills_added}개 생성")
    print(f"유닛당 최대 게이트 수(이번 배정분만): {max_skills_per_unit}")
    if excluded_non_numeric:
        print(f"\n제외(range 그룹변수) {len(excluded_non_numeric)}행:")
        for r in excluded_non_numeric:
            print(f"  {r['유닛ID']} {r['스킬트리거']}#{r['RRD순번']}")
    if gate_parse_errors:
        print(f"\n게이트 파싱 실패 {len(gate_parse_errors)}건:")
        for uid, g, e in gate_parse_errors:
            print(f"  {uid}: {g!r} — {e}")
    if skipped_grades:
        print("\n등급 불일치:")
        for gname, no, ne in skipped_grades:
            print(f"  {gname}: 원작 {no}종 / 가용 {ne}종")


if __name__ == '__main__':
    main()
