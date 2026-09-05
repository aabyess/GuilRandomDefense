#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""06번①(스킬승급형·능력교체형) 23종 중 값이 나온 만큼만 levels[*].effects를 채운다.

⚠️ 대기 중 — 아직 실행하지 마라 (2026-09-05).
지금은 두 가지가 다 있어야 한 유닛을 채울 수 있는데, 발동확률표가 아직 없다:
  1. 피해값 — ORIGINAL_SKILL_DAMAGE_TABLE.csv (basis축·basis대응 그대로 사용, 2차 채널
     생성기 generate_unit_skill_damage_effects.py의 extract_effects()와 같은 파서를 쓴다).
  2. 발동확률 — 이 23종은 OnHitChance로 정정됐지만(커밋 0bad78b) 원작 발동확률이
     Hbh1(uabi 능력표)에 없다. 트리거의 GetRandomInt 게이트에 있는데, 3개 CSV
     (ORIGINAL_UNIT_ABILITIES.csv / ORIGINAL_SKILL_DAMAGE_TABLE.csv /
     ORIGINAL_TRAIT_LEVEL2.csv·ORIGINAL_TRAIT_FLAG_CHANNEL.csv) 어디에도 없어서
     리서치담당이 별도로 뽑는 중이다(PM 지시, 2026-09-05). **그 표가 오기 전까지
     이 스크립트를 실행하지 마라** — triggerChance를 100%(현재 기본값)로 둔 채 effects를
     채우면 매 타 발동한다(초당 100회 다음으로 나쁜 상태).

## 표가 오면 할 일
`TRIGGER_CHANCE_CSV` 상수에 리서치담당이 주는 파일 경로를 채우고, 예고된 컬럼 형식
(유닛ID · 트리거 · 게이트 원문 · 확률(0~1) · 레벨1/레벨2 각각)에 맞춰
`load_trigger_chance(unit_id) -> (chance_level0, chance_level1)`을 구현한다. 그다음
`ITEMS`의 각 항목에 `trigger_chance_level0`/`trigger_chance_level1`을 채워 넣고
`main()`이 그 값을 `triggerChance`에 쓰게 한다(지금은 자리만 있고 채우는 코드가 없다 —
표 형식이 실제로 오기 전에 추측으로 파서를 먼저 못 박지 않는다).

## 채우는 순서 (PM 지시)
1. levels[0] 먼저 — 불멸 6종(h04E·h04G·h04C·h04D·h05C, 그리고 h049 제외) + 사다리 역전
   해소가 목적.
2. levels[1]은 그다음 — 특성 승급분(ORIGINAL_TRAIT_LEVEL2.csv·ORIGINAL_TRAIT_FLAG_CHANNEL.csv
   참고, 아직 안 읽어봄).

## ⚠️ h049(레일리)는 이번에도 뺐다
더미 행 하나에 스톡필드가 넷 겹쳐 있다(Ucs3=1100·Ucs1=300000·Ucs2=7e+07·Ucs4=487) —
어느 게 피해량인지 리서치담당도 확정 못 했다("필드 뜻 미확인"). 지어내지 않고 계속
비워둔다(PM 지시, 2026-09-05).

## ⚠️ 핸콕(h05C) 역방향 스케일 주의
PM이 확인한 표에 핸콕의 계수가 "0.03+0.05/레벨"로 적혀 있다 — **레벨이 오르면 값이
작아지는 게 강화**다(무슨 축인지는 미확인이지만 방향이 뒤집혀 있다는 것만은 표에 ⚠⚠로
박혀 있다). 레벨1/레벨2 값을 그대로 옮기되 순서를 뒤집지 않도록 각별히 확인할 것 —
"레벨업 = 더 큰 수"라는 상식적 가정으로 정렬하면 안 된다.

각 항목은 (SkillData 파일, 대상 유닛 파일 — attackType 폴백용, 2차 CSV 유닛ID,
levels[0]에 넣을 효과 목록)이다. levels[1](승급분)은 별도 표가 필요해 아직 비워둔다.
"""
import re

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'

# TODO(대기 중): 리서치담당이 발동확률표를 주면 실제 경로로 채운다.
TRIGGER_CHANCE_CSV = None


def load_trigger_chance(unit_id):
    """returns (chance_level0, chance_level1) or (None, None) if not yet available."""
    if TRIGGER_CHANCE_CSV is None:
        return None, None
    raise NotImplementedError('TRIGGER_CHANCE_CSV 형식이 확정되면 여기를 구현한다.')


# basis: 0=Flat, 1=TargetMaxHpPercent, 2=TargetCurrentHpPercent
# attack_type/damage_type: None이면 이 유닛 자신의 attackType으로 폴백(damageType=AD)
# 값 자체는 ORIGINAL_SKILL_DAMAGE_TABLE.csv에서 그대로 옮긴 것 — generate_unit_skill_damage_effects.py
# 작성 당시(2026-09-05) 확인된 수치, 재확인 없이 재사용.
ITEMS = [
    dict(
        skill_file='SkillData_원작021_h04E.asset',
        unit_file='불멸_이이삭.asset',
        source_unit_id='h04E',
        two_level=True,
        effects=[
            dict(basis=2, mult=0.02, bonus=400000.0, attack_type=1, note=''),
            dict(basis=2, mult=0.015, bonus=300000.0, attack_type=1, note=''),
            dict(basis=0, mult=427500.0, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Wrs1=427500) 값 그대로 — 필드뜻 미확인이라 피해가 아닐 수 있다.'),
        ],
    ),
    dict(
        skill_file='SkillData_원작022_h04G.asset',
        unit_file='불멸_박은석.asset',
        source_unit_id='h04G',
        two_level=True,
        effects=[
            dict(basis=1, mult=0.025, bonus=0.0, attack_type=1, note=''),
        ],
    ),
    dict(
        skill_file='SkillData_원작023_h04C.asset',
        unit_file='불멸_이승우.asset',
        source_unit_id='h04C',
        two_level=True,
        effects=[
            dict(basis=0, mult=0.45, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Blo1=0.45) 값 그대로 — 값이 작아 피해가 아니라 배율성 버프/디버프였을 가능성이 있다(필드뜻 미확인).'),
        ],
    ),
    dict(
        skill_file='SkillData_원작024_h04D.asset',
        unit_file='불멸_정준영.asset',
        source_unit_id='h04D',
        two_level=True,
        effects=[
            dict(basis=0, mult=250000.0, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Wrs1=250000) 값 그대로 — 필드뜻 미확인이라 피해가 아닐 수 있다.'),
        ],
    ),
    dict(
        skill_file='SkillData_원작025_h04B.asset',
        unit_file='불멸_신지우.asset',  # 능력교체형 — Trait_불멸_신지우.replacementSkill 대상
        source_unit_id='h04B',
        two_level=False,
        effects=[
            dict(basis=0, mult=-7.0, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Had1=-7) 값 그대로 — 음수라 피해가 아니라 디버프(방어력 등) 감소량이었을 가능성이 크다(필드뜻 미확인).'),
            dict(basis=0, mult=400000.0, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Wrs1=400000) 값 그대로(같은 행의 arlv=6은 능력레벨로 추정 — 용도 불명이라 반영 안 함) — 필드뜻 미확인이라 피해가 아닐 수 있다.'),
        ],
    ),
    dict(
        skill_file='SkillData_원작028_h05C.asset',
        unit_file='영원_조세민.asset',
        source_unit_id='h05C',
        two_level=True,
        # ⚠️ 핸콕 역방향 스케일 주의(파일 상단 설명 참고) — 레벨업 시 이 값이 작아지는 게
        # 정상일 수 있다. 표가 오기 전엔 levels[1]을 안 건드리므로 지금은 영향 없다.
        effects=[
            dict(basis=0, mult=-0.15, bonus=0.0, attack_type=None,
                 note=' ⚠️ 더미 스톡필드(Blo1=-0.15) 값 그대로 — 값이 작고 음수라 피해가 아니라 배율성 디버프였을 가능성이 있다(필드뜻 미확인).'),
        ],
    ),
    # h049(레일리)는 의도적으로 뺐다 — 파일 상단 설명 참고.
]


def render_effect(e, fallback_attack_type):
    attack_type = e['attack_type'] if e['attack_type'] is not None else fallback_attack_type
    fallback_note = '' if e['attack_type'] is not None else ' 공격타입 원본이 [미확인]이라 이 유닛 자신의 평타 타입으로 대신했다.'
    return (
        "    - kind: 0\n"
        f"      basis: {e['basis']}\n"
        "      target: 3\n"
        "      damageType: 1\n"
        f"      attackType: {attack_type}\n"
        f"      multiplier: {round(e['mult'], 4)}\n"
        f"      bonus: {round(e['bonus'], 4)}\n"
        "      chance: 1\n"
        "      hitCount: 1\n"
        "      duration: 0\n"
    ), e['note'] + fallback_note


def main():
    if TRIGGER_CHANCE_CSV is None:
        print("발동확률표가 아직 없다 — 실행을 멈춘다(파일 상단 설명 참고). 아무것도 안 고침.")
        return

    filled = 0
    for item in ITEMS:
        chance0, chance1 = load_trigger_chance(item['source_unit_id'])
        if chance0 is None:
            print(f"{item['skill_file']}: 발동확률 없음 — 건너뜀")
            continue

        skill_path = f"{SKILL_DIR}/{item['skill_file']}"
        unit_path = f"{ROSTER_DIR}/{item['unit_file']}"

        unit_text = open(unit_path, encoding='utf-8').read()
        at_match = re.search(r'^  attackType: (\d+)', unit_text, re.M)
        fallback_attack_type = int(at_match.group(1))

        text = open(skill_path, encoding='utf-8').read()

        blocks = []
        notes = []
        for e in item['effects']:
            block, note = render_effect(e, fallback_attack_type)
            blocks.append(block)
            if note:
                notes.append(note)

        replacement = (
            " 2차 채널(ORIGINAL_SKILL_DAMAGE_TABLE.csv) 실값 + 발동확률표로 levels[0]을 채웠다"
            + (''.join(notes))
            + (
                " 레벨2(승급 후) 수치는 별도 표에서 아직 못 읽어 levels[1]은 비워뒀다 — "
                "특성을 사기 전까지는 levels[0]을 그대로 쓴다."
                if item['two_level'] else
                " 능력교체형이라 레벨 개념이 없다 — 트레잇이 unlock되면 이 스킬 전체로 바뀐다."
            )
        )
        text, n = re.subn(
            r' 수치 미상 — (?:두 레벨 모두 )?effects가 비어 있다\. 사장님 콘텐츠 배정 시 채울 것\.',
            replacement, text, count=1,
        )
        assert n == 1, f"{skill_path}: 치환 대상 문구를 못 찾음"

        text, n = re.subn(r'    effects: \[\]', '    effects:\n' + ''.join(blocks), text, count=1)
        assert n == 1, f"{skill_path}: effects: [] 자리를 못 찾음"

        text, n = re.subn(r'^(    triggerChance: )1$', rf'\g<1>{chance0}', text, count=1, flags=re.M)
        assert n == 1, f"{skill_path}: triggerChance 자리를 못 찾음"

        open(skill_path, 'w', encoding='utf-8').write(text)
        filled += 1
        print(f"{item['skill_file']} — 효과 {len(item['effects'])}개, triggerChance={chance0} 채움")

    print(f"\n총 {filled}종 채움.")


if __name__ == '__main__':
    main()
