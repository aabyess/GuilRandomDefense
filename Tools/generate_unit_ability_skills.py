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
import os
import re
import sys

sys.path.insert(0, 'Tools')
import original_unit_matching as oum
import generate_unit_skills_by_gate as gen_gate_mod
# ⚠️ translate_hidden_recipes는 여기서 top-level import 안 함 — 그쪽이 이 모듈을
# gen1로 import해서 순환 참조가 된다. main() 안에서 늦게 import한다.

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
    return oum.load_roster(ROSTER_DIR)


# ⚠️ 이 함수는 옛(행 단위) 배정이 만든 파일의 description과 정확히 같은 문자열을
# 만든다 — Tools/regroup_unit_ability_skills.py의 revert()가 "이 파일이 어느
# 원작 행에서 왔는지"를 파일 재스캔(ground truth)으로 알아낼 때 이 문자열을
# prefix로 대조한다. ⑤-0 이후 새로 만드는 파일은 이 형식을 안 쓴다 —
# build_skill_asset()이 새 형식(uid당 여러 능력 번들)을 쓴다. 지우지 말 것.
def legacy_single_row_description(row):
    trig_present = has(row['발동확률'])
    multiplier = round(fnum(row['피해배수']), 4)
    bonus = round(fnum(row['추가피해']), 4)
    no_damage = multiplier == 0.0 and bonus == 0.0
    note = (
        " ⚠️ 피해배수·추가피해가 둘 다 0 — 원작 능력 자체가 피해가 아니라 다른 효과"
        "(이동·구속 등)였을 수 있다 — 지어내지 않고 그대로 옮겼다."
    ) if no_damage else ''

    return (
        f"원작 능력표(ORIGINAL_UNIT_ABILITIES.csv) DPS 순위 배정 — "
        f"원작 {row['등급']} {row['유닛이름']}({row['능력ID']} {row['능력이름']})의 "
        f"발동확률·피해배수·추가피해를 그대로 옮겼다. 개별 대응 아님(이름매핑 불가, "
        f"순위 배정으로 우회) — 등급 안에서 원작은 능력 기대값, 우리는 DPS(공격력×공속) "
        f"내림차순으로 정렬해 순서대로 짝지었다. damageType은 AD로 통일(원작 스킬의 "
        f"마법 여부는 CSV에 없음, 리서치담당 조사 중) — attackType은 이 유닛의 평타 "
        f"타입을 그대로 썼다.{note}"
    )


def _effect_block(row, attack_type):
    multiplier = round(fnum(row['피해배수']), 4)
    bonus = round(fnum(row['추가피해']), 4)
    return (
        "    - kind: 0\n"
        "      basis: 3\n"
        "      target: 3\n"
        "      damageType: 1\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {multiplier}\n"
        f"      bonus: {bonus}\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
    )


def _row_bullet(row):
    multiplier = round(fnum(row['피해배수']), 4)
    bonus = round(fnum(row['추가피해']), 4)
    trig_present = has(row['발동확률'])
    trigger_chance = round(fnum(row['발동확률']) / 100.0, 4) if trig_present else 1.0
    no_damage = multiplier == 0.0 and bonus == 0.0
    tag = ' [피해 0 — 비피해 효과였을 수 있다]' if no_damage else ''
    return (
        f"  · {row['능력ID']} {row['능력이름']}: 발동{trigger_chance} 배수{multiplier} "
        f"추가{bonus}{tag}"
    )


def build_skill_asset(unit_path, uid, rows, attack_type):
    """rows: 같은 uid의 능력 전부(⑤-0, TEAM_RULES 뿌리 ⑮ 세 번째 얼굴 수정) —
    uid 하나가 로스터 유닛 하나에 통째로 간다. 개별 능력을 게이트로 더 쪼갤 근거가
    이 채널엔 없다(트리거 이름이 CSV에 없다, 전부 OnHitChance).
    """
    base = unit_path.split('/')[-1][:-len('.asset')]
    name = f'SkillData_원작능력_{base}'
    guid = guid_for(name)

    first = rows[0]
    # 발동확률은 uid 안에서 가장 자주 쓰인/대표적인 값을 못 고를 이유가 없어
    # 능력마다 다를 수 있는데, 우리 SkillLevel은 레벨 하나에 triggerChance 하나뿐이다
    # — 능력마다 발동확률이 다르면 가장 높은 쪽을 대표값으로 쓴다(더 자주 검증
    # 가능한 쪽, 과소 계상보다 안전한 근사).
    trigger_chances = [round(fnum(r['발동확률']) / 100.0, 4) if has(r['발동확률']) else 1.0 for r in rows]
    trigger_chance = max(trigger_chances)

    bullets = ''.join('\n' + _row_bullet(r) for r in rows)
    multi_note = f' 이 원작 유닛의 능력 {len(rows)}개를 전부 이 유닛 하나에 배정했다(uid 단위 배정, 뿌리 ⑮ 세 번째 얼굴 수정).' if len(rows) > 1 else ''
    description = (
        f"원작 능력표(ORIGINAL_UNIT_ABILITIES.csv) DPS 순위 배정 — "
        f"원작 {first['등급']} {first['유닛이름']}({uid})의 발동확률·피해배수·추가피해를 "
        f"그대로 옮겼다. 개별 대응 아님(이름매핑 불가, 순위 배정으로 우회) — 등급 안에서 "
        f"원작은 uid 그룹 총점(능력 기대값 합), 우리는 DPS(공격력×공속) 내림차순으로 "
        f"정렬해 순서대로 짝지었다. damageType은 AD로 통일(원작 스킬의 마법 여부는 CSV에 "
        f"없음, 리서치담당 조사 중) — attackType은 이 유닛의 평타 타입을 그대로 썼다."
        f"{multi_note} 능력마다 발동확률이 다르면 가장 높은 값을 대표로 썼다(SkillLevel "
        f"하나엔 triggerChance 하나뿐이라서 — 과소 계상보다 안전한 근사)."
        f"{bullets}"
    )

    effects_text = ''.join(_effect_block(r, attack_type) for r in rows)
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {first['능력ID']} {first['능력이름']}\n"
        + f"  description: {description}\n"
        + "  triggerType: 0\n"
        + "  levels:\n"
        + "  - cooldown: 0\n"
        + f"    triggerChance: {trigger_chance}\n"
        + "    range: 0\n"
        + "    effects:\n"
        + effects_text
    )
    return name, guid, body


def _has_gauge_conflict(roster_text, new_uses_gauge=False, new_gauge_kind=None, new_threshold=None):
    """이 로스터가 이미 OnHitCount 스킬을 갖고 있는데 새로 붙이려는 것도 같은
    gaugeKind에 다른 threshold를 쓰면 충돌이다(먼저 찬 쪽이 리셋해 나머지가 영영
    안 닿는다 — UnitAttacker.cs의 유닛 공유 게이지 주석 참고). 1채널은 지금
    triggerType이 항상 0(OnHitChance)이라 gaugeKind을 아예 안 써서 이 함수는
    현재 데이터로는 항상 False다 — 그래도 PM 지시(2026-09-06)대로 검사 자체는
    남겨둔다(1채널이 나중에 게이지를 쓰게 되면 바로 걸리게).
    """
    if not new_uses_gauge:
        return False
    guids = re.findall(r'guid: (\w+), type: 2\}', roster_text)
    for guid in guids:
        for f in glob.glob(f'{OUT_DIR}/*.asset'):
            meta = f + '.meta'
            if not os.path.exists(meta):
                continue
            if guid not in open(meta, encoding='utf-8').read():
                continue
            text = open(f, encoding='utf-8').read()
            if 'triggerType: 3' not in text:
                continue
            gk_m = re.search(r'gaugeKind: (\d+)', text)
            th_m = re.search(r'hitCountThreshold: (\d+)', text)
            if gk_m and int(gk_m.group(1)) == new_gauge_kind and th_m and int(th_m.group(1)) != new_threshold:
                return True
    return False


def _append_ability_rows_to_existing_file(path, uid, rows, attack_type):
    """대상 로스터에 SkillData_원작능력_{base}.asset이 이미 있다 — 다른 uid가 먼저
    거기 배정됐거나(같은 파일명 규칙을 쓰는 2채널 fresh 파일 포함) 이번 배치의
    앞선 uid가 이미 만들었을 수 있다. 파일명이 uid가 아니라 로스터 base라서
    생기는 충돌 위험(뿌리 ⑱ 형제)을 덮어쓰기 대신 이어붙이기로 피한다."""
    text = open(path, encoding='utf-8').read()
    marker = f'({uid})'
    if marker in text:
        return False  # 이미 이 uid가 반영돼 있다 — 재실행 멱등성.

    bullets = ''.join('\n' + _row_bullet(r) for r in rows)
    first = rows[0]
    addition = (
        f" | 1채널 우선 배정(원작 {first['등급']} {first['유닛이름']}({uid})의 능력 "
        f"{len(rows)}개): 다른 채널이 이미 이 유닛을 차지하고 있어 순위 배정 대신 "
        f"그 유닛에 그대로 얹었다(우선순위 06번①/게이트·회수/2채널이 1채널보다 "
        f"먼저, 2026-09-06 PM 지시)."
        f"{bullets}"
    )
    effects_text = ''.join(_effect_block(r, attack_type) for r in rows)
    text = text.rstrip('\n') + '\n' + effects_text
    text = re.sub(r'^(  description: .*)$', lambda m: m.group(1) + addition, text, count=1, flags=re.M)
    open(path, 'w', encoding='utf-8').write(text)
    return True


def main():
    import translate_hidden_recipes as th  # 늦은 import — th가 이 모듈을 gen1으로 문다(순환 참조 방지)

    by_grade, dup_count, uniq_count = load_csv_rows()
    roster = load_roster()
    roster_by_base = {u['path'].split('/')[-1][:-len('.asset')]: u for u in roster}

    # 1단계(PM 지시, 2026-09-06) — "원작 uid를 키로 먼저 매칭한다. 이미 어느
    # 채널에서든 배정된 원작 유닛이면 순위 배정을 건너뛰고 그 로스터의 skills에
    # 추가한다." 06번①/게이트·회수/2채널만 본다(1채널 자체를 지금 고치는 중이라
    # 그 항목은 순환 참조 — scan_non_1chan_assignments() 주석 참고).
    prior = th.scan_non_1chan_assignments()

    direct = 0
    ranked = 0
    skipped_grades = []
    all_dropped = []
    gauge_conflicts = []

    for gname, genum in GRADE_ENUM.items():
        ability_rows = by_grade.get(gname, [])
        uid_groups = oum.group_by_uid(ability_rows)

        for uid in list(uid_groups.keys()):
            if uid not in prior:
                continue
            base, channel = prior[uid]
            unit = roster_by_base.get(base)
            if unit is None:
                continue
            rows = uid_groups[uid]

            if _has_gauge_conflict(unit['text']):
                gauge_conflicts.append((uid, base))
                continue

            path = f'{OUT_DIR}/SkillData_원작능력_{base}.asset'
            if os.path.exists(path):
                if _append_ability_rows_to_existing_file(path, uid, rows, unit['attack_type']):
                    direct += 1
                del uid_groups[uid]
                continue

            name, guid, body = build_skill_asset(unit['path'], uid, rows, unit['attack_type'])
            write_asset(path, body, guid)
            new_text = gen_gate_mod.add_to_skills_list(unit['text'], guid)
            unit['text'] = new_text
            open(unit['path'], 'w', encoding='utf-8').write(new_text)
            direct += 1
            del uid_groups[uid]

        # 2단계 — 남은(어디에도 없는) uid만 claimed 안 된 슬롯에 등급 안 순위 배정.
        eligible = sorted(
            [u for u in roster if u['grade'] == genum and not u['claimed']],
            key=lambda u: -u['dps'],
        )
        matched, dropped = oum.match_uid_groups_to_roster(
            uid_groups, eligible, lambda rows: sum(ability_score(r) for r in rows))
        all_dropped.extend((gname, uid) for uid in dropped)
        if not matched and not uid_groups:
            skipped_grades.append(gname)

        for uid, unit, rows in matched:
            name, guid, body = build_skill_asset(unit['path'], uid, rows, unit['attack_type'])
            write_asset(f'{OUT_DIR}/{name}.asset', body, guid)

            new_text = re.sub(
                r'^  skill: \{fileID: 0\}$',
                f'  skill: {{fileID: 11400000, guid: {guid}, type: 2}}',
                unit['text'], count=1, flags=re.M,
            )
            open(unit['path'], 'w', encoding='utf-8').write(new_text)
            unit['claimed'] = True
            ranked += 1

    print(f"CSV 원본 필터 132행 -> 중복 {dup_count}건 제거 -> 고유 {uniq_count}행")
    print(f"1단계(이미 배정된 uid, skills에 얹음): {direct}개 / 2단계(신규 순위 배정): {ranked}개")
    if skipped_grades:
        print(f"배정 0건 등급(가용 유닛 또는 능력 없음): {', '.join(skipped_grades)}")
    if gauge_conflicts:
        print(f"게이지 충돌로 1단계에서 건너뛴 uid {len(gauge_conflicts)}개: {gauge_conflicts}")
    if all_dropped:
        print(f"등급 슬롯 부족으로 못 넣은 uid {len(all_dropped)}개: {all_dropped}")


if __name__ == '__main__':
    main()
