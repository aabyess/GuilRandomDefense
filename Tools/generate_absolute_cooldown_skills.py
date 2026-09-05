#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ORIGINAL_GATED_SKILLS_14.csv의 "미수록" 행 중 4건(뱌쿠야·나미·레일리·사보)을 만든다.

PM 지시(2026-09-06) — "5절대쿨" 계열(버프 미보유 + N초 쿨다운, 드래곤 h04D와 같은
메커니즘)의 "미수록"(표 어디에도 없는) 20행 중:
  - 뱌쿠야(Byakuya_E)·나미(Nami_Skill_4)·레일리(Kick_1) = 단순한 OnHitChance+cooldown
    패턴(드래곤과 동일).
  - 사보(Sabo_Skill_1/3/4, 8행 중 4행): 절대쿨(12.5초)+공유마나게이지125+중첩확률
    3중 게이트라 처음엔 보류했으나, PM이 계산으로 절대쿨을 무해 제거할 근거를 줬다
    (마나게이지125=평타125타 필요=최소31초·현실60~80초 > 절대쿨12.5초 — 게이지가
    다시 찰 때쯤 쿨은 이미 풀려 있어 절대쿨이 항상 무효). 그래서 절대쿨은 버리고
    OnHitCount(마나125)+triggerChance(1/10, 1/60)만으로 3개 게이트(Skill_1/3/4)를
    채운다. 8행 중 realD·UnitCountBuffsExBJ(버프개수)·능력레벨계수가 얽힌 truncated
    수식 4행(Skill_1의 2행, Skill_4의 2행)은 지어내지 않고 뺐다.
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


def build_asset(name, skill_name, description, cooldown, range_value, effects_yaml,
                 trigger_type=0, trigger_chance=1.0, hit_count_threshold=0, reset_to=0,
                 gauge_kind=0):
    guid = guid_for(name)
    path = f'{SKILL_DIR}/{name}.asset'
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {skill_name}\n"
        + f"  description: {description}\n"
        + f"  triggerType: {trigger_type}\n"
        + "  levels:\n"
        + f"  - cooldown: {cooldown}\n"
        + f"    triggerChance: {trigger_chance}\n"
        + f"    range: {range_value}\n"
        + f"    hitCountThreshold: {hit_count_threshold}\n"
        + f"    resetTo: {reset_to}\n"
        + f"    gaugeKind: {gauge_kind}\n"
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
        'B00K는 스킬이 자기에게 거는 잠금 버프(드래곤 B00J와 같은 패턴) — '
        'SkillLevel.cooldown=14.0이 이미 그 잠금을 정확히 표현하므로 forbiddenBuffId는 '
        '따로 안 썼다(PM 지시 2026-09-06, 379cfec: 같은 잠금을 두 번 걸지 않는다).',
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
        'B012는 스킬 자기잠금 버프 — SkillLevel.cooldown=3.5가 이미 그 잠금을 표현하므로 '
        'forbiddenBuffId는 따로 안 썼다(PM 지시 2026-09-06, 379cfec).',
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
        'B070은 스킬 자기잠금 버프 — SkillLevel.cooldown=4.75가 이미 그 잠금을 표현하므로 '
        'forbiddenBuffId는 따로 안 썼다(PM 지시 2026-09-06, 379cfec). '
        '⚠️ 표에 완전히 같은 값이 "GetRandomInt(1,50)==1" 추가게이트로도 한 번 더 '
        '나오는데(중복인지 진짜 1/50 보너스인지 불확실) 화력이 2배가 될 위험이 있어 '
        '이번엔 "(절대쿨만)" 세트만 넣었다 — PM 확인(레일리 1/50 중복은 확정 전엔 보류 '
        '유지가 맞다고 재확인, 2026-09-06). '
        'skill을 skills[0]으로 옮기고(06번① Had1, ArmorBreak) 이 스킬을 추가했다.',
        cooldown=4.75, range_value=400, effects_yaml=effects,
    )
    add_to_skills('Assets/Data/Units/Roster/불멸_정윤식.asset', guid)
    print('레일리 → 불멸_정윤식 배선 완료(skill→skills[0] 이동 포함)')

    # ── 사보 (PM 지시, 2026-09-06: 절대쿨은 계산상 무의미해 버리고 게이지+확률만) ──────
    # 마나게이지125 = 평타 125타 필요. 사보 공격속도로도 최소 31초(현실 60~80초) 걸리는데
    # 절대쿨은 12.5초 — 게이지가 다시 차기 훨씬 전에 쿨이 풀려 있으니 절대쿨은 사실상
    # 무효(근사가 아니라 무해 제거, PM 계산 확인). OnHitCount(마나125)만으로 게이팅한다.
    #
    # Sabo_Skill_1(게이트: MANA게이지==125.00만) — 3행 중 row1(Flat 3,250,000, NORMAL→
    # Spells/UNIVERSAL→AP)만 파싱 가능. row2·3은 realD·UnitCountBuffsExBJ(버프개수)가
    # 얽힌 수식이 CSV에서 잘려(truncated) 남아 있어 못 옮긴다 — 지어내지 않고 뺀다.
    guid1 = build_asset(
        'SkillData_절대쿨_초월_두유찬_AD_1',
        '사보 — 게이지(Sabo_Skill_1)',
        '5절대쿨 계열(ORIGINAL_GATED_SKILLS_14.csv, 우리표커버=미수록) 신규 배정 — '
        '원작 H092 사보(Sabo_Skill_1), 버프 B00N. 게이트=MANA게이지==125.00. '
        '⚠️ PM 계산: 원작 절대쿨 12.5초 — 마나게이지125는 평타 125타가 필요해(사보 공격'
        '속도로도 최소 31초, 현실 60~80초) 게이지 주기가 절대쿨보다 항상 길다 → 절대쿨은 '
        '재충전 시점에 이미 풀려 있어 사실상 무효, 생략해도 동작이 거의 안 바뀐다(근사가 '
        '아니라 무해 제거). OnHitCount(마나125, gaugeKind=Mana)만으로 게이팅. '
        'Flat 3,250,000(Enemies range 475, NORMAL→Spells, UNIVERSAL→AP)만 옮겼다 — '
        '같은 게이트의 나머지 2행(RRD#2 HERO/NORMAL, RRD#3 CHAOS/UNIVERSAL)은 계산식에 '
        'realD와 UnitCountBuffsExBJ(버프개수)가 얽혀 있는데 표 자체가 식 중간에서 '
        '잘려(truncated) 완전한 수식이 없다 — 지어내지 않고 뺐다. '
        'B00N(절대쿨 자기잠금 버프)은 절대쿨 자체를 버렸으므로 forbiddenBuffId도 '
        '안 썼다 — 애초에 표현할 잠금이 없다(PM 지시 2026-09-06, 379cfec).',
        cooldown=0, range_value=475,
        effects_yaml=effect(0, 3250000.0, 0.0, target=2, attack_type=7, damage_type=2),
        trigger_type=3, trigger_chance=1.0, hit_count_threshold=125, reset_to=1, gauge_kind=0,
    )
    add_to_skills('Assets/Data/Units/Roster/초월_두유찬_AD.asset', guid1)

    # Sabo_Skill_3(게이트: MANA125 AND 1/10) — 1행, 완전히 파싱 가능.
    guid3 = build_asset(
        'SkillData_절대쿨_초월_두유찬_AD_3',
        '사보 — 게이지+확률(Sabo_Skill_3)',
        '5절대쿨 계열(ORIGINAL_GATED_SKILLS_14.csv, 우리표커버=미수록) 신규 배정 — '
        '원작 H092 사보(Sabo_Skill_3), 버프 B00N. 게이트=MANA게이지==125.00 AND '
        'GetRandomInt(1,10)==5. ⚠️ 절대쿨 생략 근거는 SkillData_절대쿨_초월_두유찬_AD_1 '
        '참고(게이지 주기(≥31초) > 절대쿨(12.5초), 사실상 무효). '
        'OnHitCount(마나125)+triggerChance=1/10. Flat 1,150,000(Enemies range 415, '
        'CHAOS 그대로/NORMAL(damage)→AD).',
        cooldown=0, range_value=415,
        effects_yaml=effect(0, 1150000.0, 0.0, target=2, attack_type=5, damage_type=1),
        trigger_type=3, trigger_chance=round(1 / 10, 6), hit_count_threshold=125, reset_to=1,
        gauge_kind=0,
    )
    add_to_skills('Assets/Data/Units/Roster/초월_두유찬_AD.asset', guid3)

    # Sabo_Skill_4(게이트: MANA125 AND 1/10 AND 1/6 = 1/60) — 4행 중 2행만 파싱 가능
    # (row a: Flat 500,000 / row d: 대상최대체력×0.01). row b·c는 마찬가지로
    # UnitCountBuffsExBJ·능력레벨계수가 얽힌 truncated 수식이라 뺀다.
    effects4 = (
        effect(0, 500000.0, 0.0, target=2, attack_type=5, damage_type=1)
        + effect(1, 0.01, 0.0, target=3, attack_type=4, damage_type=1)
    )
    guid4 = build_asset(
        'SkillData_절대쿨_초월_두유찬_AD_4',
        '사보 — 게이지+확률×2(Sabo_Skill_4)',
        '5절대쿨 계열(ORIGINAL_GATED_SKILLS_14.csv, 우리표커버=미수록) 신규 배정 — '
        '원작 H092 사보(Sabo_Skill_4), 버프 B00N. 게이트=MANA게이지==125.00 AND '
        'GetRandomInt(1,10)==5 AND GetRandomInt(1,6)==4(확률은 수학적으로 동치인 '
        '1/10×1/6=1/60으로 합쳤다). ⚠️ 절대쿨 생략 근거는 _1과 동일. '
        'OnHitCount(마나125)+triggerChance=1/60. 4행 중 2행만 옮겼다 — '
        'RRD#1 Flat 500,000(Enemies range 415, CHAOS/NORMAL(damage)→AD)과 '
        'RRD#4 대상최대체력×0.01(SingleTarget, HERO/NORMAL(damage)→AD). '
        'RRD#2(UNIVERSAL, 버프개수 얽힌 truncated 수식)와 RRD#3(HERO, 능력레벨계수+'
        '버프개수 이중으로 얽힌 truncated 수식, %MaxHp)은 지어내지 않고 뺐다.',
        cooldown=0, range_value=415,
        effects_yaml=effects4,
        trigger_type=3, trigger_chance=round(1 / 60, 6), hit_count_threshold=125, reset_to=1,
        gauge_kind=0,
    )
    add_to_skills('Assets/Data/Units/Roster/초월_두유찬_AD.asset', guid4)
    print('사보(3게이트: Skill_1/3/4) → 초월_두유찬_AD skills에 추가 완료')

    print('\n건너뜀: 카이도(피해값이 레지스터라 수치 없음, 리서치담당 확인 중) '
          '· 레일리 1/50 중복 세트(확정 전 보류) · 페로나·뱌쿠야 더미(스톡 능력 필드, RRD 아님)')


if __name__ == '__main__':
    main()
