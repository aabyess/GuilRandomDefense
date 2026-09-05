#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""더미 채널 52행 배선 (2026-09-06 새벽, 리서치담당 발견) — 2채널 표
(ORIGINAL_SKILL_DAMAGE_TABLE.csv)의 `더미채널판정`이 "해당없음(더미채널·RRD 아님)"
이라 지금까지 아무도 안 본 86행 중 52행이 실제로 피해였다(나머지는 버프/디버프
등 피해 아님, 또는 시전 명령을 못 찾아 [미확인]).

## 배정 방법
⑤(채널 통합)가 아직 안 끝나 있어 새 분산을 만들지 않도록, **원작 유닛ID가 이미
어딘가(06번①/게이트·회수/1채널/2채널)에 배정돼 있으면 그 로스터 유닛에만 추가**한다
(PM 지시) — `translate_hidden_recipes.build_master_uid_map()`을 그대로 재사용(우선순위
06번①>게이트/회수>1채널>2채널, 오늘 밤 내내 쓴 것과 동일). 대응이 없는 uid는 이번엔
없었다(41개 전부 대응 있음).

## 게이지 충돌
전부 OnHitChance(게이트 정보가 없어 triggerChance=1.0, 항상 발동으로 근사)라 게이지
축을 안 써서 충돌 여지가 없다 — PM이 걱정한 "마나125+마나85" 류 충돌은 애초에
발생하지 않는다.

## 신뢰도 표시
표본이 1~4개뿐인 필드(더미 명령 종류별)는 리서치담당이 "자릿수 판정이라 근거가 약함"
이라고 명시했다 — 그 필드에 걸린 행은 description에 "[추정: 자릿수 판정]"을 남긴다.
AOws(23개)·ACca(12개)는 표본이 충분해 이 표시를 안 붙인다.

## 공격/피해 타입
전부 [미확인]이라 1차/2차 채널과 같은 fallback을 쓴다 — 대상 로스터 유닛 자신의
평타 attackType을 그대로 쓰고 damageType은 AD로 통일한다.
"""
import csv
import hashlib
import re
import sys
from collections import defaultdict

sys.path.insert(0, 'Tools')
import translate_hidden_recipes as th
import generate_unit_skills_by_gate as gen_gate

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

LOW_SAMPLE_FIELDS = {'AHtb', 'AHtc', 'AEfk', 'ACbz', 'AOcl', 'ANst', 'ANbr', 'ANpi', 'AIfb', 'AOww'}


def guid_for(name):
    return hashlib.md5(('guilrd/dummychannel/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def field_of(row):
    m = re.search(r'— (A\w+)', row['더미채널판정'])
    return m.group(1) if m else '?'


def roster_attack_type(base):
    path = f'{ROSTER_DIR}/{base}.asset'
    text = open(path, encoding='utf-8').read()
    m = re.search(r'^  attackType: (\d+)', text, re.M)
    return int(m.group(1)) if m else 1


def add_to_skills(roster_path, guid):
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
        assert existing_skill, roster_path
        entries = []
        if existing_skill.group(1) != '0':
            entries.append(f"  - {{fileID: 11400000, guid: {existing_skill.group(2)}, type: 2}}\n")
        entries.append(f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n")
        text = text[:existing_skill.start()] + "  skill: {fileID: 0}\n  skills:\n" + \
            ''.join(entries) + text[existing_skill.end():]
    open(roster_path, 'w', encoding='utf-8').write(text)


def main():
    uid_map = th.build_master_uid_map()
    rows = list(csv.DictReader(open('Docs/reference/ORIGINAL_SKILL_DAMAGE_TABLE.csv', encoding='utf-8-sig')))
    target = [r for r in rows if r['더미채널판정'].startswith('⭕')]

    by_uid = defaultdict(list)
    for r in target:
        by_uid[r['유닛ID']].append(r)

    made = 0
    skipped_no_map = []
    for uid, rs in by_uid.items():
        if uid not in uid_map:
            skipped_no_map.append(uid)
            continue
        base, channel = uid_map[uid]
        at = roster_attack_type(base)

        effect_blocks = []
        desc_lines = []
        for r in rs:
            value = float(r['더미_피해값'].replace(',', ''))
            low = field_of(r) in LOW_SAMPLE_FIELDS
            tag = ' [추정: 자릿수 판정]' if low else ''
            desc_lines.append(f"  · {r['출처']}: {value:.0f}{tag}")
            effect_blocks.append(
                "    - kind: 0\n"
                "      basis: 0\n"
                "      target: 3\n"
                "      damageType: 1\n"
                f"      attackType: {at}\n"
                f"      multiplier: {value}\n"
                "      bonus: 0.0\n"
                "      chance: 1\n"
                "      hitCount: 1\n"
                "      duration: 0\n"
                "      casterBuffCountFactor: 0\n"
            )

        name = f'SkillData_더미채널_{base}'
        guid = guid_for(name)
        path = f'{SKILL_DIR}/{name}.asset'
        description = (
            f"더미 채널 배선(2026-09-06, ORIGINAL_SKILL_DAMAGE_TABLE.csv 더미채널판정=⭕) — "
            f"원작 {rs[0]['유닛이름']}({uid})의 스톡 더미 능력 {len(rs)}개. 대응 채널={channel}. "
            "게이트 정보가 없어 OnHitChance triggerChance=1.0(항상 발동)으로 근사했다 — "
            "게이지를 안 써 다른 채널 스킬과 충돌 여지가 없다. 공격/피해타입이 전부 "
            "[미확인]이라 1·2채널과 같은 fallback(이 유닛 자신의 평타 타입, damageType=AD)을 "
            "썼다. [추정: 자릿수 판정] 표시는 같은 더미 명령 표본이 1~4개뿐이라 신뢰도가 "
            "낮다는 리서치담당 판정이다(AOws 23개·ACca 12개는 표본 충분이라 표시 없음)." +
            ''.join(desc_lines)
        )
        body = (
            HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
            + f"  skillName: {rs[0]['출처'].split(' → ')[0]}\n"
            + f"  description: {description}\n"
            + "  triggerType: 0\n"
            + "  levels:\n"
            + "  - cooldown: 0\n"
            + "    triggerChance: 1.0\n"
            + "    range: 0\n"
            + "    hitCountThreshold: 0\n"
            + "    resetTo: 0\n"
            + "    gaugeKind: 0\n"
            + "    effects:\n"
            + ''.join(effect_blocks)
        )
        open(path, 'w', encoding='utf-8').write(body)
        write_meta(path, guid)
        add_to_skills(f'{ROSTER_DIR}/{base}.asset', guid)
        made += 1

    print(f'배선 완료: {made}개 원작 유닛(스킬 {sum(len(r) for r in by_uid.values())}행)')
    print(f'대응 없음(건너뜀): {skipped_no_map}')


if __name__ == '__main__':
    main()
