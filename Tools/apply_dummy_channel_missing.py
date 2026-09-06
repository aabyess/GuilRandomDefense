#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""DUMMY_CHANNEL_MISSING.csv 79행 이식 (2026-09-06, 리서치담당 a0893bd) — 기존 더미 채널
52행(`fill_dummy_channel_damage.py`) 이식 뒤에도 트리거가 만드는 더미 유닛 1,162종 중
95종이 그물에 안 걸렸고(뿌리 ㉓ — 유닛 `uabi`에서 출발한 스캔이라 런타임에 붙는 능력이
부르는 2차 더미를 못 봄), 그중 79행이 이번 이식 대상이다.

## 이번 배치가 52행과 다른 점 셋 (PM 지시, 2026-09-06)
1. **uid 컬럼이 없다.** 52행의 원본(`ORIGINAL_SKILL_DAMAGE_TABLE.csv`)엔 `유닛ID`가
   있었지만 이 CSV는 `부모트리거`·`더미이름`뿐이다 — 캐릭터·등급을 직접 역추적해야
   했다(아래 CHAR_TARGET, PM에 보고하고 승인받은 결과). **79행 중 56행만 대상이 있다**
   (h05J·h0BH 두 행은 이미 uid 형태로 들어와 있어 그대로 매핑). 나머지 23행은 그
   등급-캐릭터 조합의 uid 자체가 우리 데이터 어디에도 없다 —
   `Docs/reference/UID_UNMATCHED_23.csv`로 남기고 스킵한다(로스터를 지어내지 않는다).
2. **게이트를 모른다.** 52행 때(`ORIGINAL_SKILL_DAMAGE_TABLE.csv`)는 구조적으로 게이트
   컬럼 자체가 없어서 전부 triggerChance=1.0으로 근사했다. 이번 79행은 소스 트리거가
   해당 캐릭터의 "본체 능력 발동 트리거"라 확률을 모르는데, 값이 최대 1,000,000짜리라
   1.0으로 넣으면 145배 과다 사고(`0-AA`)가 재현된다. **PM 지시: triggerChance = 0으로
   잠근다** — `UnitAttacker.cs:734`의 `Random.value >= level.triggerChance` 판정에서
   0이면 `Random.value >= 0`이 항상 참이라 확실히 발동 안 한다(밸런스 기여 0). 게이트가
   나오면 이 숫자 하나만 고치면 된다(재이식 아님) — 그래서 값·범위·대상·타입은
   원작 그대로 다 채운다.
   ⚠️ description 맨 앞에 "이건 피해가 없다는 뜻이 아니다"를 반드시 적는다(뿌리 ⑯ —
   오늘 세 번째 재발: 0을 "효과 없음"으로 잘못 읽는 사고).
3. **파일명 충돌 방지(뿌리 ⑱ 형제).** 여러 uid가 같은 로스터 유닛 하나로 몰린다
   (예: 나미·조로·야마토가 전부 `초월_강주혁_AP`/`초월_구주호_AD` 등으로). 파일명을
   `SkillData_더미채널_{로스터}` 하나로 쓰면 뒤에 오는 uid가 앞선 파일을 덮어쓴다
   (예전에 h04F 가반이 그렇게 사라졌다). 그래서 **로스터 하나당 파일 하나, 그 안에
   effects를 여러 개 append**하고, 파일명에 `_79행` 접미사를 붙여 기존
   `SkillData_더미채널_{로스터}`(52행 계열, 있다면)와도 안 겹치게 한다.

## uid 매핑 근거(CHAR_TARGET) — 전부 커밋된 CSV·현재 로스터 description에서 인용
`ORIGINAL_UNIT_ABILITIES.csv`·`ORIGINAL_UNIT_NAMES.csv`·`UNLISTED_SKILLS_BY_UNIT.csv`·
`ORIGINAL_HERO_STATS.csv`(야마토 H0BD/H0BE)에서 (캐릭터,등급)→uid를 찾고,
`translate_hidden_recipes.build_master_uid_map()`(read-only, 뿌리 ⑱ 고정판)으로
uid→(로스터,채널)을 얻었다. 같은 캐릭터가 uid 여러 개(정상형/각성형, 영웅/비영웅
변형)로 갈리는 경우 **우선순위(06번①>게이트/회수>1채널>2채널)가 가장 높은 쪽을
그 캐릭터의 대표 배선처로 썼다** — 예: 루피는 h04R(1채널, 초월_황준석_ADAP)과
H099(게이트/회수, 초월_신문철_AP) 둘 다 있는데 후자가 우선한다.
"""
import csv
import hashlib
import re
import sys
from collections import defaultdict

sys.path.insert(0, 'Tools')

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'
CSV_PATH = 'Docs/reference/DUMMY_CHANNEL_MISSING.csv'

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
"""

# ── 등급 접두부호(더미이름 첫 글자) — DUMMY_CHANNEL_GATES.csv 기존 표기와 동일 ──────
GRADE_SYMBOL = {'@': '초월함', '!': '전설적인', 'E': '영원함', '#': '불멸',
                'S': '특별함', 'U': '희귀함', 'H': '히든'}

CHARS = ['로우', '로쿠규', '야마토', '미호크', '아오키지', '키자루', '핸콕', '징베',
         '레베카', '검은수염', '이치고', '아카이누', '마르코', '에넬', '흰수염', '시키',
         '레일리', '상디', '조로', '나미', '브룩', '루피', '키드', '루치', '도플', '에이스',
         '제트']


def has_word(text, word):
    for m in re.finditer(re.escape(word), text):
        before = text[m.start() - 1] if m.start() > 0 else ' '
        if not ('가' <= before <= '힣'):
            return True
    return False


def parse_dummy_name(name):
    name = name.strip()
    if '제한변화' in name or '제한됨변화' in name:
        return ['제한됨', '변화됨'], re.sub(r'^%?제한됨?변화더미?\s*', '', name)
    sym = name[0] if name and name[0] in GRADE_SYMBOL else None
    if sym:
        grade = GRADE_SYMBOL[sym]
        rest = re.sub(r'^.\s*(초월|전설|영원|불멸|특별함|희귀함|히든)?\s*', '', name)
        return [grade], rest
    return [], name


def guess_char(rest):
    if 'Z ' in rest or rest.startswith('Z'):
        return '제트'
    hits = [c for c in CHARS if has_word(rest, c)]
    return hits[0] if hits else None


# ── PM 승인 완료(2026-09-06) — 캐릭터+등급 → 로스터 대표 배선처 ────────────────
CHAR_TARGET = {
    ('루피', '초월함'): '초월_신문철_AP',
    ('도플', '초월함'): '초월_최상호_AP',
    ('검은수염', '초월함'): '초월_임채민_AP',
    ('나미', '초월함'): '초월_강주혁_AP',
    ('조로', '초월함'): '초월_강주혁_AP',
    ('야마토', '초월함'): '초월_구주호_AD',
    ('로쿠규', '초월함'): '초월_구주호_AD',
    ('키자루', '초월함'): '초월_황준석_ADAP',
    ('징베', '초월함'): '초월_황준석_ADAP',
    ('브룩', '초월함'): '초월_황준석_ADAP',
    ('키드', '초월함'): '초월_황준석_ADAP',
    ('미호크', '영원함'): '영원_최상호',
    ('핸콕', '영원함'): '영원_조세민',
    ('레베카', '제한됨'): '제한_최영민',
    ('마르코', '전설적인'): '전설적인_임채민',
    ('아카이누', '히든'): '히든_호치킨',
    ('흰수염', '불멸'): '불멸_고도현',
    ('시키', '불멸'): '불멸_고도현',
    ('레일리', '불멸'): '불멸_정윤식',
    ('제트', '불멸'): '불멸_박은석',
    ('도플', '변화됨'): '변화됨_최상호',
    ('에이스', '변화됨'): '변화됨_박은석',
}

# 스킵 대상(uid 자체가 없음, PM 승인 — UID_UNMATCHED_23.csv로 기록)
SKIP_CHAR_GRADE = {
    ('로우', '초월함'), ('에넬', '희귀함'), ('에넬', '특별함'), ('상디', '초월함'),
    ('아오키지', '초월함'), ('미호크', '히든'), ('이치고', '전설적인'),
    ('루치', '제한됨'), ('루치', '변화됨'),
}


def roster_attack_type(base):
    path = f'{ROSTER_DIR}/{base}.asset'
    text = open(path, encoding='utf-8').read()
    m = re.search(r'^  attackType: (\d+)', text, re.M)
    return int(m.group(1)) if m else 1


def guid_for(name):
    return hashlib.md5(('guilrd/dummychannel79/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


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


# 더미이름 자체엔 캐릭터 힌트가 없고 부모트리거에만 있는 예외 2건(트리거명 영단어 매칭).
TRIGGER_CHAR_HINT = {'Ed_Skill': '흰수염', 'Rokugu': '로쿠규'}


def resolve_row(row):
    """returns (roster_base or None, char, grade or None). h05J·h0BH는 main()에서
    직접 처리하므로 여기로 안 온다."""
    grades, rest = parse_dummy_name(row['더미이름'])
    char = guess_char(rest)
    if char is None:
        for hint, canon in TRIGGER_CHAR_HINT.items():
            if hint in row['부모트리거']:
                char = canon
                break
    if char is None:
        return None, None, None
    for g in (grades or list(GRADE_SYMBOL.values())):
        if (char, g) in CHAR_TARGET:
            return CHAR_TARGET[(char, g)], char, g
    for g in (grades or ['?']):
        if (char, g) in SKIP_CHAR_GRADE:
            return None, char, g
    return None, char, (grades[0] if grades else '?')


def main():
    rows = list(csv.DictReader(open(CSV_PATH, encoding='utf-8-sig')))

    by_roster = defaultdict(list)  # roster_base -> [(row, char, grade)]
    unmatched = []

    for row in rows:
        dummy_id = row['더미유닛ID']
        if dummy_id == 'h05J':
            by_roster['불멸_정윤식'].append((row, '레일리', '불멸'))
            continue
        if dummy_id == 'h0BH':
            by_roster['불멸_고도현'].append((row, '시키', '불멸'))
            continue
        base, char, grade = resolve_row(row)
        if base:
            by_roster[base].append((row, char, grade))
        else:
            unmatched.append((row, char, grade))

    made_rows = 0
    for base, entries in sorted(by_roster.items()):
        at = roster_attack_type(base)
        name = f'SkillData_더미채널_{base}_79행'
        guid = guid_for(name)
        path = f'{SKILL_DIR}/{name}.asset'

        desc_lines = [
            "⚠️ [게이트 미확인]이라 triggerChance=0으로 잠갔다. 피해 없음이 아니다 — "
            "0이면 UnitAttacker.cs:734(Random.value >= triggerChance)가 항상 참이라 "
            "발동을 확실히 막는다(145배 과다 사고 0-AA 재현 방지, PM 지시 2026-09-06). "
            "게이트가 확인되면 triggerChance 숫자만 고치면 된다(재이식 아님). "
            "값·범위·대상·타입은 원작 그대로 채워뒀다. 출처: DUMMY_CHANNEL_MISSING.csv"
            "(리서치담당 a0893bd)."
        ]
        effect_blocks = []
        for row, char, grade in entries:
            value = float(row['값'].replace(',', ''))
            desc_lines.append(
                f"  · {row['더미유닛ID']}({row['더미이름']}) {row['능력']} {row['능력이름']} "
                f"[{row['필드의미']}]: {value:.0f} (원작 {char}·{grade}, 부모트리거={row['부모트리거']})"
            )
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

        description = ''.join(desc_lines)
        body = (
            HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
            + "  m_EditorClassIdentifier:\n"
            + f"  skillName: {entries[0][0]['능력']} {entries[0][0]['능력이름']}\n"
            + f"  description: {description}\n"
            + "  triggerType: 0\n"
            + "  levels:\n"
            + "  - cooldown: 0\n"
            + "    triggerChance: 0.0\n"
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
        made_rows += len(entries)
        print(f'{base}: {len(entries)}행 -> {path}')

    # ── 잠긴 행 목록(리서치담당 작업 목록) ──────────────────────────────────
    with open('Docs/reference/GATE_LOCKED_ROWS.csv', 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['더미유닛ID', '능력', '능력이름', '값', '부모트리거', '원작캐릭터', '등급', '배선로스터'])
        for base, entries in sorted(by_roster.items()):
            for row, char, grade in entries:
                w.writerow([row['더미유닛ID'], row['능력'], row['능력이름'], row['값'],
                            row['부모트리거'], char, grade, base])

    # ── uid 매칭 실패 23행 ──────────────────────────────────────────────────
    with open('Docs/reference/UID_UNMATCHED_23.csv', 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['더미유닛ID', '더미이름', '능력', '능력이름', '값', '부모트리거',
                    '추정캐릭터', '추정등급', '스킵사유'])
        for row, char, grade in unmatched:
            w.writerow([row['더미유닛ID'], row['더미이름'], row['능력'], row['능력이름'],
                        row['값'], row['부모트리거'], char or '?', grade or '?',
                        '해당 캐릭터+등급 조합의 uid가 현재 CSV 카탈로그에 없음'])

    print(f'\n배선 완료: {made_rows}행 -> {len(by_roster)}개 로스터 파일')
    print(f'uid 매칭 실패(스킵): {len(unmatched)}행 -> Docs/reference/UID_UNMATCHED_23.csv')
    print(f'잠긴 행 전체 목록: Docs/reference/GATE_LOCKED_ROWS.csv')


if __name__ == '__main__':
    main()
