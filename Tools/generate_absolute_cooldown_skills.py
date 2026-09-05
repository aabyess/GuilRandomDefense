#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ORIGINAL_GATED_SKILLS_14.csv의 "미수록" 행 중 단순한 3건(뱌쿠야·나미·레일리)을 만든다.

PM 지시(2026-09-06) — "5절대쿨" 계열(버프 미보유 + N초 쿨다운, 드래곤 h04D와 같은
메커니즘)의 "미수록"(표 어디에도 없는) 20행 중:
  - 뱌쿠야(Byakuya_E)·나미(Nami_Skill_4)·레일리(Kick_1) = 이 스크립트가 채운다
    (단순한 OnHitChance+cooldown 패턴, 드래곤과 동일).
  - 사보(Sabo_Skill_1/3/4, 8행) = **뺐다** — 마나게이지125(공유 게이지) AND 중첩
    확률(1/10, 1/10×1/6)이 절대쿨 위에 또 얹혀 3중 게이트라, 우리 스키마(게이지 하나
    또는 확률 하나 + 쿨다운)로 못 담는다. 추측으로 하나를 버리지 않고 PM 판단 대기.
  - 카이도(Kaido_Skill_1_8, 2행) = **뺐다** — 피해값이 상수가 아니라
    `s__TrigVariables__get_realD(GlobalTV)`(레지스터 참조)뿐이라 원문 수치 자체가 없다.
    지어낼 수 없다.
  - 페로나(더미:e095)·뱌쿠야 더미(e0LR) = **뺐다** — "[미확인](스톡주문)" 즉 RRD
    호출이 아니라 오브젝트에디터 스톡 능력 필드라 이 파이프라인 대상이 아니다(이번
    세션 내내 반복된 구분, h04B/h049 Had1과 같은 이유).

## 대상 유닛
- 뱌쿠야(h08I) → 랜덤_한마_바키 — 이 유닛이 96유닛 프로젝트에서 h08I의 다른(게이지·
  확률) 능력을 이미 받았다(SkillData_게이트_랜덤_한마_바키_*.asset 2개). skills에 추가.
- 나미(H08V) → 초월_강주혁_AP — 마찬가지로 96유닛 프로젝트에서 H08V를 이미 받은 유닛.
- 레일리(h049) → 불멸_정윤식 — 06번①에서 이미 확정된 이름매핑(h049=레일리, Had1
  ArmorBreak을 그 유닛의 skill 필드로 이미 받았다). skill을 skills[0]으로 옮기고
  skill은 비운 뒤 이 SkillData를 skills에 추가한다(구현담당3 검사 #11 규칙).

## 레일리 Kick_1 — 중복 행 처리
표에 완전히 같은 값(1,750,000 / 최대체력×0.15 / 787,500)이 "추가게이트=(절대쿨만)"과
"추가게이트=GetRandomInt(1,50)==1"으로 **두 번** 나온다. 어느 쪽이 진짜인지(둘 다인지,
하나가 조사 중복인지) 확실치 않아 **"(절대쿨만)" 3행만 쓴다** — 1/50 세트는 빼고
description에 남긴다. 둘 다 넣으면 화력이 2배가 될 위험이 있다.
"""
import hashlib

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'

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
    return hashlib.md5(('guilrd/absolutecooldown/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def effect(basis, mult, bonus, target, attack_type, damage_type=1, hit_count=1):
    return (
        "    - kind: 0\n"
        f"      basis: {basis}\n"
        f"      target: {target}\n"
        f"      damageType: {damage_type}\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(mult, 4)}\n"
        f"      bonus: {round(bonus, 4)}\n"
        "      chance: 1\n"
        f"      hitCount: {hit_count}\n"
        "      duration: 0\n"
        "      casterBuffCountFactor: 0\n"
    )


def build_asset(name, skill_name, description, cooldown, range_value, effects_yaml):
    guid = guid_for(name)
    path = f'{SKILL_DIR}/{name}.asset'
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {skill_name}\n"
        + f"  description: {description}\n"
        + "  triggerType: 0\n"
        + "  levels:\n"
        + f"  - cooldown: {cooldown}\n"
        + "    triggerChance: 1.0\n"
        + f"    range: {range_value}\n"
        + "    hitCountThreshold: 0\n"
        + "    resetTo: 0\n"
        + "    gaugeKind: 0\n"
        + "    effects:\n"
        + effects_yaml
    )
    open(path, 'w', encoding='utf-8').write(body)
    write_meta(path, guid)
    return guid


def add_to_skills(roster_path, guid):
    import re
    text = open(roster_path, encoding='utf-8').read()
    if re.search(r'^  skills:\n', text, re.M):
        m = re.search(r'^  skills:\n((?:  - .*\n)*)', text, re.M)
        block = m.group(1)
        if f'guid: {guid},' in block:
            return  # 멱등
        new_block = block + f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
        text = text[:m.start(1)] + new_block + text[m.end(1):]
    else:
        existing_skill = re.search(r'^  skill: \{fileID: (\d+)(?:, guid: (\w+))?[^\n]*\}\n', text, re.M)
        assert existing_skill
        entries = []
        if existing_skill.group(1) != '0':
            entries.append(f"  - {{fileID: 11400000, guid: {existing_skill.group(2)}, type: 2}}\n")
        entries.append(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n")
        text = text[:existing_skill.start()] + "  skill: {fileID: 0}\n  skills:\n" + \
            ''.join(entries) + text[existing_skill.end():]
    open(roster_path, 'w', encoding='utf-8').write(text)


def main():
    # ── 뱌쿠야 ────────────────────────────────────────────────────────────
    effects = effect(0, 4500000.0, 0.0, target=2, attack_type=7, damage_type=2)  # NORMAL→Spells, UNIVERSAL→AP
    guid = build_asset(
        'SkillData_절대쿨_랜덤_한마_바키',
        '뱌쿠야 — 5절대쿨',
        '5절대쿨 계열(ORIGINAL_GATED_SKILLS_14.csv, 우리표커버=미수록) 신규 배정 — '
        '원작 h08I 쿠치키 뱌쿠야(Byakuya_E), 버프 B00K 미보유 + 14.0초 절대쿨. '
        'Flat 4,500,000(Enemies range 925, NORMAL→Spells, UNIVERSAL→AP). '
        '버프 게이트는 축 밖이라 뺐다 — OnHitChance+cooldown=14.0으로만 근사'
        '(드래곤 h04D와 같은 패턴).',
        cooldown=14.0, range_value=925, effects_yaml=effects,
    )
    add_to_skills('Assets/Data/Units/Roster/랜덤_한마_바키.asset', guid)
    print('뱌쿠야 → 랜덤_한마_바키 배선 완료')

    # ── 나미 ──────────────────────────────────────────────────────────────
    effects = effect(0, 350000.0, 0.0, target=2, attack_type=7, damage_type=2)
    guid = build_asset(
        'SkillData_절대쿨_초월_강주혁_AP',
        '나미 — 5절대쿨',
        '5절대쿨 계열(ORIGINAL_GATED_SKILLS_14.csv, 우리표커버=미수록) 신규 배정 — '
        '원작 H08V 나미(Nami_Skill_4), 버프 B012 미보유 + 3.5초 절대쿨. '
        'Flat 350,000(Enemies range 500, NORMAL→Spells, UNIVERSAL→AP). '
        '버프 게이트는 축 밖이라 뺐다 — OnHitChance+cooldown=3.5로만 근사.',
        cooldown=3.5, range_value=500, effects_yaml=effects,
    )
    add_to_skills('Assets/Data/Units/Roster/초월_강주혁_AP.asset', guid)
    print('나미 → 초월_강주혁_AP 배선 완료')

    # ── 레일리 (Kick_1, "(절대쿨만)" 3행만 — 1/50 중복 세트는 뺌) ────────────
    effects = (
        effect(0, 1750000.0, 0.0, target=2, attack_type=5, damage_type=1)  # CHAOS→그대로, NORMAL→AD
        + effect(2, 0.15, 0.0, target=3, attack_type=5, damage_type=1)     # 대상현재체력(단일)
        + effect(0, 787500.0, 0.0, target=3, attack_type=5, damage_type=1)
    )
    guid = build_asset(
        'SkillData_절대쿨_불멸_정윤식',
        '레일리 — 5절대쿨(Kick_1)',
        '5절대쿨 계열(ORIGINAL_GATED_SKILLS_14.csv, 우리표커버=미수록) 신규 배정 — '
        '원작 h049 실버즈 레일리(Kick_1), 버프 B070 미보유 + 4.75초 절대쿨. '
        '3효과: Flat 1,750,000(Enemies range 400) + 대상현재체력×0.15(SingleTarget) '
        '+ Flat 787,500(SingleTarget). 전부 CHAOS/NORMAL→AD 그대로. '
        '⚠️ 표에 완전히 같은 값이 "GetRandomInt(1,50)==1" 추가게이트로도 한 번 더 '
        '나오는데(중복인지 진짜 1/50 보너스인지 불확실) 화력이 2배가 될 위험이 있어 '
        '이번엔 "(절대쿨만)" 세트만 넣었다 — PM 확인 대기. '
        'skill을 skills[0]으로 옮기고(06번① Had1, ArmorBreak) 이 스킬을 추가했다.',
        cooldown=4.75, range_value=400, effects_yaml=effects,
    )
    add_to_skills('Assets/Data/Units/Roster/불멸_정윤식.asset', guid)
    print('레일리 → 불멸_정윤식 배선 완료(skill→skills[0] 이동 포함)')

    print('\n건너뜀: 사보(3중 게이트, PM 확인 대기) · 카이도(피해값이 레지스터라 수치 없음) '
          '· 페로나·뱌쿠야 더미(스톡 능력 필드, RRD 아님)')


if __name__ == '__main__':
    main()
