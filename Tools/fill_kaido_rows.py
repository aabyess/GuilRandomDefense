#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""카이도 8행(2026-09-06 리서치담당 확정) — udg_Kaido_Damage=우리 ReceivedDamage
(recentAttackDamage)와 같은 자리임이 밝혀져(985150d) 그동안 축밖이던 6행을 확정
전환하고, 표에 없던 Kaido_Skill_1_8 2행을 새로 추가한 결과를 옮긴다.

## 대상 유닛
h07M(카이도, 인간형 트리거)이 이미 2채널 재배정(③-b)으로 불멸_신지우에 배정돼
있다 — 같은 캐릭터니 이 8행도 전부 불멸_신지우에 합류시킨다(h0AD는 별도 슬롯을
받은 적이 없다 — 같은 카이도이므로 갈라 배정하지 않는다).

## 게이트 3종
- LIFE게이지100.00(h0AD, Kaido_Dragon_Skill_2 + Kaido_Dragon_melee_1) — 파싱 가능한
  유일한 게이트. OnHitCount(Life, threshold=100, resetTo=1)로 정확히 담는다.
- Kaido_Skill_int>=10(용형 평타, h07M Kaido_Dragon_buster) 및 Kaido_Skill_1_8의
  두 경로(경로A: Kaido_Skill_int>=11 / 경로B: LIFE100아님 AND Kaido_Skill_int<10
  AND B05Q없음 AND 1/7) — Kaido_Skill_int는 "폼"이 아니라 카이도 평타에서만 오르는
  카운터다(PM 확인, 2026-09-06). 배타인 두 경로는 카운터<10이면 경로B(1/7), >=11이면
  영구히 경로A(무조건)로 넘어간다 — 유닛이 한 판에 수백 번 때리므로 초반 10타만
  지나면 상시 발동이 정상 상태다. 그래서 두 게이트 다 OnHitChance triggerChance=1.0이
  "실제보다 자주 발동"이 아니라 장기적으로 정확한 근사다(카운터==10인 원작의 한
  틱짜리 빈틈은 무시).

## 레인지 병합
Kaido_Dragon_Skill_2(range 515) + Kaido_Dragon_melee_1(range 500)이 같은 LIFE100
게이트라 SkillLevel.range 하나를 공유해야 한다 — 작은 쪽(500)으로 통일한다
(기존 resolve_range 규칙과 동일, 과다 범위보다 과소 범위가 안전).
"""
import hashlib

SKILL_DIR = 'Assets/Data/UnitSkills'
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
    return hashlib.md5(('guilrd/kaido/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def effect(mult, bonus, target, damage_type, rmin, rmax):
    return (
        "    - kind: 0\n"
        "      basis: 5\n"
        f"      target: {target}\n"
        f"      damageType: {damage_type}\n"
        "      attackType: 5\n"
        f"      multiplier: {mult}\n"
        f"      bonus: {bonus}\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
        f"      randMin: {rmin}\n"
        f"      randMax: {rmax}\n"
        "      casterBuffCountFactor: 0\n"
    )


def build_asset(name, skill_name, description, cooldown, trigger_type, trigger_chance,
                 range_value, hit_count_threshold, reset_to, gauge_kind, effects_yaml):
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
    m = re.search(r'^  skills:\n((?:  - .*\n)*)', text, re.M)
    if m:
        block = m.group(1)
        if f'guid: {guid},' in block:
            return
        new_block = block + f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n"
        text = text[:m.start(1)] + new_block + text[m.end(1):]
    else:
        existing_skill = re.search(r'^  skill: \{fileID: (\d+)(?:, guid: (\w+))?[^\n]*\}\n', text, re.M)
        entries = []
        if existing_skill.group(1) != '0':
            entries.append(f"  - {{fileID: 11400000, guid: {existing_skill.group(2)}, type: 2}}\n")
        entries.append(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n")
        text = text[:existing_skill.start()] + "  skill: {fileID: 0}\n  skills:\n" + \
            ''.join(entries) + text[existing_skill.end():]
    open(roster_path, 'w', encoding='utf-8').write(text)


TARGET_ROSTER = 'Assets/Data/Units/Roster/불멸_신지우.asset'


def main():
    # ── 게이트 A: LIFE100(h0AD) — Kaido_Dragon_Skill_2 + Kaido_Dragon_melee_1 ──────
    effects_a = (
        effect(0.5, 600000, target=2, damage_type=1, rmin=1, rmax=1)
        + effect(0.5, 600000, target=2, damage_type=1, rmin=1, rmax=1)
        + effect(0.8736, 0, target=2, damage_type=1, rmin=1, rmax=1)
        + effect(0.546, 0, target=2, damage_type=1, rmin=1, rmax=1)
    )
    guid_a = build_asset(
        'SkillData_카이도_불멸_신지우_life100',
        '카이도 — 용형 체력100%(Kaido_Dragon_Skill_2·melee_1)',
        '원작 h0AD 카이도(용형), 게이트=LIFE게이지100.00(리서치담당 확정, 2026-09-06 '
        '985150d — udg_Kaido_Damage=우리 ReceivedDamage(recentAttackDamage)와 같은 '
        '자리). OnHitCount(Life, threshold=100, resetTo=1). 4효과: '
        'ReceivedDamage×0.5+600,000(Enemies)를 Kaido_Dragon_Skill_2#1/#2로 2회, '
        'ReceivedDamage×0.8736/×0.546(Enemies, 고정항 0)을 Kaido_Dragon_melee_1#1/#2로. '
        '전부 CHAOS/NORMAL→AD. range는 두 트리거가 515/500으로 달라 작은 쪽(500)으로 '
        '통일했다(과다 범위보다 과소 범위가 안전). ⚠️ Kaido_Dragon_melee_1은 고정항이 '
        '0이라 100% 평타(recentAttackDamage) 비례다 — 방어 적용 전/후 오차가 최종 '
        '피해에 그대로 나간다(다른 카이도 항은 17~40% 고정항으로 오차가 희석되는데 '
        '이건 아니다) — 이 축을 정밀하게 갈 때 여기부터 볼 것.',
        cooldown=0, trigger_type=3, trigger_chance=1.0, range_value=500,
        hit_count_threshold=100, reset_to=1, gauge_kind=1, effects_yaml=effects_a,
    )
    add_to_skills(TARGET_ROSTER, guid_a)

    # ── 게이트 B: Kaido_Skill_int>=10(용형 평타, h07M) — 상태변수, 못 읽음 ──────────
    effects_b = (
        effect(2.1875, 1125000, target=2, damage_type=1, rmin=1, rmax=1)
        + effect(2.1875, 1125000, target=3, damage_type=2, rmin=2.58, rmax=2.58)
    )
    guid_b = build_asset(
        'SkillData_카이도_불멸_신지우_buster',
        '카이도 — 용형 평타(Kaido_Dragon_buster)',
        '원작 h07M 카이도(용형 평타), 게이트="Kaido_Skill_int>=10 (용형 평타)"(리서치담당 '
        '확정, 2026-09-06 985150d — udg_Kaido_Damage=ReceivedDamage). Kaido_Skill_int는 '
        '카이도 평타에서만 오르는 카운터라 우리 축에 없지만, 10을 넘으면 그 뒤로 영구히 '
        '조건을 만족한다(단조 증가) — 한 판에 수백 번 때리는 유닛 특성상 초반 10타만 '
        '지나면 사실상 상시 발동이라 OnHitChance triggerChance=1.0이 장기적으로 정확한 '
        '근사다(PM 확인, 2026-09-06). 2효과: '
        'ReceivedDamage×2.1875+1,125,000(Enemies range575, NORMAL→AD, rand1) / '
        '같은 식(SingleTarget, UNIVERSAL→AP, rand2.58 고정).',
        cooldown=0, trigger_type=0, trigger_chance=1.0, range_value=575,
        hit_count_threshold=0, reset_to=0, gauge_kind=0, effects_yaml=effects_b,
    )
    add_to_skills(TARGET_ROSTER, guid_b)

    # ── 게이트 C: Kaido_Skill_1_8 — OR-of-two-paths, 못 읽음, 과다계상 위험 명시 ────
    effects_c = (
        effect(5.9375, 2250000, target=2, damage_type=1, rmin=1, rmax=1)
        + effect(5.9375, 2250000, target=3, damage_type=2, rmin=2.58, rmax=2.58)
    )
    guid_c = build_asset(
        'SkillData_카이도_불멸_신지우_skill18',
        '카이도 — Kaido_Skill_1_8(신규 확정행)',
        '원작 h07M/h0AD 카이도, 표에 없던 행(2026-09-06 리서치담당 신규 추가). 게이트가 '
        '경로A(Kaido_Skill_int>=11) 또는 경로B(LIFE100아님 AND Kaido_Skill_int<10 AND '
        'B05Q없음 AND 1/7) 중 하나 — "폼"이 아니라 카운터로 갈리는 배타 경로이고, '
        'Kaido_Skill_int는 카이도 평타에서만 오른다(PM 확인, 2026-09-06). 카운터가 '
        '10 미만인 초반 평타 10타 구간만 경로B(1/7)고, 11 이상이 되면 영구히 경로A'
        '(무조건)로 넘어간다 — 유닛이 한 판에 수백 번 때리므로 정상 상태는 "무조건" '
        '쪽이고 초반 구간은 전체에서 무시할 만한 비중이다. 그래서 OnHitChance '
        'triggerChance=1.0이 장기적으로 정확한 근사다(7배 과다 계상이 아니다) — '
        '카운터가 정확히 10일 때 어느 경로도 안 맞는 원작의 한 틱짜리 빈틈은 무시했다. '
        '2효과: ReceivedDamage×5.9375+2,250,000(Enemies range575, '
        'NORMAL→AD, rand1, A0VI 1중첩 동반은 축 밖) / 같은 식(SingleTarget, '
        'UNIVERSAL→AP, rand2.58 고정).',
        cooldown=0, trigger_type=0, trigger_chance=1.0, range_value=575,
        hit_count_threshold=0, reset_to=0, gauge_kind=0, effects_yaml=effects_c,
    )
    add_to_skills(TARGET_ROSTER, guid_c)

    print('카이도 8행(3게이트) → 불멸_신지우 skills에 추가 완료')


if __name__ == '__main__':
    main()
