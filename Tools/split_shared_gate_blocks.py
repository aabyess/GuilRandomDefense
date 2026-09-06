#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""여러 원작 능력이 triggerChance 하나를 공유하던 파일을 원작 능력 하나당 파일 하나로
쪼갠다(PM 설계 결정, 2026-09-06 — 스키마 변경(b안) 대신 파일 분할(a안): 코드 무변경,
런타임이 이미 skills 리스트를 개별로 굴려서 쪼개는 순간 저절로 능력별 독립 게이트가
된다. 원작 밀도("한 스킬=한 이름의 능력/트리거")를 세는 단위와도 저절로 맞아떨어진다).

## 1단계: 「인라인 표기(· AXXX: 발동R 배수M 추가B) 개수 == 실제 effects 개수」인
깨끗한 파일만 다룬다 — bullets와 effects가 순서대로 1:1이라고 이미 검증된 파일만
기계적으로 쪼갠다. 그 외 11개(인라인이 실제 effects 일부만 주석 단 것 — "1채널
추가 배정 h067 능력 4개" 같은 무명 묶음이 섞여 있다)는 순서 매칭이 안전하지 않아
2단계로 미룬다(PM 지시 — 단계별 커밋).

## 규칙(전부 PM 지시, 하나씩만 씀)
- 원래 guid를 유지하는 능력 = "effects 개수가 가장 많은 것, 동률이면 먼저 나온 것".
- 나머지는 새 guid로 새 파일(파일명에 능력ID 포함, `_AXXX`).
- 각 파일의 triggerChance = 그 능력의 인라인 발동률.
- skillName은 이번에 같이 그 능력 하나로 맞춘다(PM: "쪼개는 김에 자연스럽다").
- 생성기 재실행 아님 — 커밋된 상태를 그대로 읽어 대상 파일만 고친다(뿌리 ⑱ 회피).
"""
import glob
import hashlib
import re
import sys

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'

INLINE_RE = re.compile(r'·\s*(A[0-9A-Za-z]{3})\s+([^:]*):\s*발동([\d.]+)\s*배수([-\d.]+)\s*추가([-\d.]+)')

# 1단계 대상 — bullets == effects 확인된 5개 중 4개(불멸_이승우 제외, 아래 참고).
# ⚠️ 불멸_이승우는 이 스크립트로 안 돌린다 — A0FU가 ACBH_ATAR_AUDIT.csv에서 이미
# atar=none(무효)로 확정됐다. 기계적으로 쪼개면 "A0FU 전용 파일(triggerChance=0.0)"이
# 생기는데, 그러면 근거 없는 0.0 파일이 되어 [게이트 미확인]과 "atar=none이라 원래
# 무효" 둘을 구분 못 한다 — 43행에서 배운 그대로다. 이승우는 별도로 손으로 처리한다.
PHASE1_FILES = [
    'SkillData_원작능력_불멸_정윤식',
    'SkillData_원작능력_불멸_정준영',
    'SkillData_원작능력_제한_강보명',
    'SkillData_원작능력_초월_황준석_ADAP',
]

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
SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'


def read(p):
    return open(p, encoding='utf-8').read()


def write(p, text):
    open(p, 'w', encoding='utf-8').write(text)


def guid_for(name):
    return hashlib.md5(('guilrd/splitgate/' + name).encode()).hexdigest()


def write_meta(path, guid):
    write(path + '.meta',
          "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
          "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
          "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def guid_of_meta(meta_path):
    return re.search(r'guid: ([0-9a-f]+)', read(meta_path)).group(1)


def find_roster_owning_guid(guid):
    for rp in glob.glob(f'{ROSTER_DIR}/*.asset'):
        if guid in read(rp):
            return rp
    return None


def add_to_skills(roster_path, guid):
    text = read(roster_path)
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
    write(roster_path, text)


def parse_file(base):
    path = f'{SKILL_DIR}/{base}.asset'
    text = read(path)

    block_m = re.search(r'^  description:.*?(?=^  triggerType:)', text, re.M | re.S)
    block = block_m.group(0)
    bullets = INLINE_RE.findall(block)  # [(code, name, rate, mult, bonus), ...] in order

    level_starts = [m.start() for m in re.finditer(r'^  - cooldown: ', text, re.M)]
    level0_end = level_starts[1] if len(level_starts) > 1 else len(text)
    level0 = text[level_starts[0]:level0_end]

    cooldown_m = re.search(r'^  - cooldown: ([-+0-9.eE]+)', level0, re.M)
    range_m = re.search(r'^    range: ([-+0-9.eE]+)', level0, re.M)
    htc_m = re.search(r'^    hitCountThreshold: (\d+)', level0, re.M)
    reset_m = re.search(r'^    resetTo: (\d+)', level0, re.M)
    gauge_m = re.search(r'^    gaugeKind: (\d+)', level0, re.M)

    effect_blocks = re.findall(r'^    - kind: \d+\n(?:      .*\n)+', level0, re.M)
    assert len(effect_blocks) == len(bullets), (base, len(effect_blocks), len(bullets))

    return dict(
        path=path, text=text, bullets=bullets, effect_blocks=effect_blocks,
        cooldown=cooldown_m.group(1) if cooldown_m else '0',
        range=range_m.group(1) if range_m else '0',
        htc=htc_m.group(1) if htc_m else None,
        reset=reset_m.group(1) if reset_m else None,
        gauge=gauge_m.group(1) if gauge_m else None,
        level0_span=(level_starts[0], level0_end),
    )


def group_by_code(parsed):
    groups = {}  # code -> dict(name, rate, effects=[...])
    order = []
    for (code, name, rate, mult, bonus), eff in zip(parsed['bullets'], parsed['effect_blocks']):
        if code not in groups:
            groups[code] = dict(name=name.strip(), rate=rate, effects=[])
            order.append(code)
        groups[code]['effects'].append(eff)
    return groups, order


def level_fields_text(triggerChance, parsed):
    lines = [f"  - cooldown: {parsed['cooldown']}", f"    triggerChance: {triggerChance}",
              f"    range: {parsed['range']}"]
    if parsed['htc'] is not None:
        lines.append(f"    hitCountThreshold: {parsed['htc']}")
    if parsed['reset'] is not None:
        lines.append(f"    resetTo: {parsed['reset']}")
    if parsed['gauge'] is not None:
        lines.append(f"    gaugeKind: {parsed['gauge']}")
    lines.append("    effects:")
    return '\n'.join(lines) + '\n'


def main():
    report = []
    for base in PHASE1_FILES:
        parsed = parse_file(base)
        groups, order = group_by_code(parsed)
        if len(groups) < 2:
            print(f'{base}: 그룹 1개뿐(스킵) — 재확인 필요')
            continue

        # 원래 guid를 유지할 능력 = effects 개수 최다, 동률이면 먼저 나온 것(=order 순서 그대로 최댓값 탐색)
        keep_code = max(order, key=lambda c: (len(groups[c]['effects']), -order.index(c)))

        roster_path = None
        orig_guid = guid_of_meta(parsed['path'] + '.meta')
        roster_path = find_roster_owning_guid(orig_guid)
        assert roster_path, f'{base}: 이 guid({orig_guid})를 참조하는 로스터를 못 찾음'

        # 원본 파일 갱신: keep_code만 남기고, triggerChance/skillName/description 갱신, 파일명에 능력ID 추가
        kg = groups[keep_code]
        new_desc = (
            f"쪼갬(2026-09-06, PM 설계 결정 — 원작 능력 하나=파일 하나): 이전엔 이 파일이 "
            f"{len(order)}개 원작 능력({', '.join(order)})의 효과를 triggerChance 하나로 "
            f"공유했다. {keep_code} !{kg['name']}(발동{kg['rate']})만 남기고 나머지는 새 "
            f"파일로 옮겼다(같은 로스터에 skills로 추가). 원래 설명: " +
            re.sub(r'^  description: ', '', re.search(r'^  description:.*?(?=^  triggerType:)', parsed['text'], re.M | re.S).group(0)).replace('\n', ' ')
        )
        new_name = f'{base}_{keep_code}'
        new_path = f'{SKILL_DIR}/{new_name}.asset'
        new_body = (
            HEAD.replace('__SCRIPT__', SCRIPT_GUID).replace('__NAME__', new_name)
            + f"  skillName: {keep_code} !{kg['name']}\n"
            + f"  description: {new_desc}\n"
            + "  triggerType: 0\n"
            + "  levels:\n"
            + level_fields_text(kg['rate'], parsed)
            + ''.join(kg['effects'])
        )
        write(new_path, new_body)
        write(new_path + '.meta', read(parsed['path'] + '.meta'))  # guid 그대로 이전
        import os
        os.remove(parsed['path'])
        os.remove(parsed['path'] + '.meta')

        made = [(keep_code, new_path, orig_guid, len(kg['effects']))]

        for code in order:
            if code == keep_code:
                continue
            g = groups[code]
            name = f'{base}_{code}'
            path2 = f'{SKILL_DIR}/{name}.asset'
            guid2 = guid_for(name)
            desc2 = (
                f"쪼갬(2026-09-06, PM 설계 결정 — 원작 능력 하나=파일 하나): {base}에서 "
                f"{code} !{g['name']}(발동{g['rate']}) 몫만 떼어낸 새 파일이다(원래 함께 "
                f"triggerChance 하나를 공유하던 능력 {len(order)}개 중 하나, 원래 파일:"
                f"{base}_{keep_code}). 원래 설명: " +
                re.sub(r'^  description: ', '', re.search(r'^  description:.*?(?=^  triggerType:)', parsed['text'], re.M | re.S).group(0)).replace('\n', ' ')
            )
            body2 = (
                HEAD.replace('__SCRIPT__', SCRIPT_GUID).replace('__NAME__', name)
                + f"  skillName: {code} !{g['name']}\n"
                + f"  description: {desc2}\n"
                + "  triggerType: 0\n"
                + "  levels:\n"
                + level_fields_text(g['rate'], parsed)
                + ''.join(g['effects'])
            )
            write(path2, body2)
            write_meta(path2, guid2)
            add_to_skills(roster_path, guid2)
            made.append((code, path2, guid2, len(g['effects'])))

        report.append((base, roster_path, made))

    for base, roster_path, made in report:
        print(f'\n{base}  (로스터: {roster_path})')
        for code, path, guid, n in made:
            print(f'   {code}: {path}  effects={n}  guid={guid}')


if __name__ == '__main__':
    main()
