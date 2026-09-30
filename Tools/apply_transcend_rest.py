"""초월함 나머지 — 묶음 A(타시기·시라호시·나미·루치·후지토라·프랑키)의 「지금 축으로 넣을 것」(2026-09-30 구현담당1 — TRANSCEND_FILL_LIST §1~2).

묶음 A는 포크가 쓴 절이라 넣는 블록마다 war3map_new.j·w3a를 다시 열어 봤다. 포크와 달랐던 것:
  - 타시기 Tasigi_03 반복은 `integerB < 11 + Aamk 레벨` — H05N uabi에 Aamk가 없고 j에 부여 자리도 없다 → **11회**(문서의 12회가 아니라 지금 값이 맞다).
규칙은 apply_immortal_blocks.py 머리 주석과 같다. 다시 돌려도 같은 결과.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg, STOCK_AOWS_ADUR  # noqa: E402

SELF, ENEMIES, SINGLE = 0, 2, 3
DAMAGE, STUN, ARMOR_BREAK, SPEED, SLOW = 0, 1, 2, 12, 13
AD, AP = 1, 2
HERO, CHAOS, MAGIC, SPELLS = 4, 5, 6, 7
ATK, STR, AGI = 3, 9, 10
ON_HIT, AURA, GAUGE = 0, 2, 3
STOCK_AURA_RADIUS = 900
TAG = '[초월 나머지 09-30]'


def note(asset, text):
    m = re.search(r'^  description: (.*)$', asset.head, re.M)
    old = m.group(1) if m else ''
    if TAG in old:
        return False
    add = ' %s %s' % (TAG, text)
    new = old[:-1] + add.replace("'", "''") + "'" if old.startswith("'") and old.endswith("'") else sat.yaml_scalar(old + add)
    if m:
        asset.head = asset.head.replace(m.group(0), '  description: ' + new)
    else:
        asset.head = re.sub(r'^(  skillName: .*\n)', lambda k: k.group(1) + '  description: ' + new + '\n', asset.head, count=1, flags=re.M)
    return True


def main():
    changed = []

    def new(roster, stem, name, desc, trigger, level):
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, '원작 ' + desc + ' 2026-09-30 구현담당1.', trigger, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_transcend_rest.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def add(a, extra, front=False):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, extra + b if front else b + extra)

    # 타시기 H05N → 초월_김민준_AP: Tasigi_03(마나 135) 525 범위 셋
    def tasigi(a):
        sat.set_level_field(a, 0, 'range', 525.0)
        add(a, [sat.effect(kind=DAMAGE, basis=AGI, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=5000.0, bonus=400000.0),
                sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AP, attackType=MAGIC, multiplier=3500.0, bonus=380000.0, hitCount=11, duration=1.1),
                sat.effect(kind=DAMAGE, basis=AGI, target=ENEMIES, damageType=AP, attackType=MAGIC, multiplier=5000.0, bonus=350000.0)])
    edit('SkillData_게이트_초월_김민준_AP_2a646778', 'Tasigi_03 범위 셋 추가(대상 중심 525): 시작 (400000 + AGI×5000) CHAOS/UNIVERSAL · 반복마다 (380000 + STR×3500) MAGIC/UNIVERSAL ×11 · 끝 (350000 + AGI×5000) MAGIC/UNIVERSAL. '
         '반복 수는 integerB < 11 + Aamk 레벨 — H05N엔 Aamk가 없어 11회(지금 값 그대로). 「대상이 죽으면 무작위 적으로」·A11S 계수 (0.25+0.15L)는 미반영. ', tasigi)

    # 시라호시 H08U → 초월_유재헌_ADAP
    new('초월_유재헌_ADAP', 'SkillData_원작능력_초월_유재헌_ADAP_A0HN', 'A0HN !인어공주 — 17% ×3.0 + 250000',
        '시라호시 H08U uabi A0HN(ACbh, Hbh1 17, Hbh2 3.0, Hbh3 250000, 스턴 0).', ON_HIT,
        sat.level_block(triggerChance=0.17, effects=[sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=HERO, multiplier=3.0, bonus=250000.0)]))
    edit('SkillData_게이트_초월_유재헌_ADAP_Sirahoshi_skill_Mana', 'stomp A0BD(700 범위, 피해 0, adur 빈칸 = 스톡 3.0 · ahdu 3.0) 스턴 추가(피해 범위 800으로). ',
         lambda a: add(a, [sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))

    # 나미 H08V → 초월_강주혁_AP
    new('초월_강주혁_AP', 'SkillData_원작트리거_초월_강주혁_AP_Nami_Skill_1', '나미 — Nami_Skill_1 헤비레인 1/24(400 범위 300000×1~1.5 ×8)',
        '나미 H08V Nami_Attack 1/24 → Nami_Skill_1: 대상 근처 무작위 지점 400 범위 (300000 + INT×3500)×1~1.5 NORMAL/UNIVERSAL 8회(integerA==8까지) — 대상 중심으로 근사, '
        'INT 항은 H08V가 hrif 기반(영웅 아님)이라 0. T특성 끝 타(450000)·이감 오라 A0FV는 미반영.', ON_HIT,
        sat.level_block(triggerChance=1.0 / 24.0, range=400.0, effects=[dmg(ENEMIES, AP, SPELLS, 300000.0, randMax=1.5, hitCount=8, duration=3.0)]))
    new('초월_강주혁_AP', 'SkillData_원작오라_초월_강주혁_AP_A0GZ', 'A0GZ 천재항해사 — 자기 공속 +12%',
        '나미 H08V uabi A0GZ(AOae, Oae2 +0.12, atar invulnerable,self = 자기만, abuf B02C).', AURA,
        sat.level_block(range=850.0, effects=[sat.effect(kind=SPEED, target=SELF, multiplier=0.12, buffId='B02C')]))

    # 루치 H08W → 초월_임장혁_AD
    new('초월_임장혁_AD', 'SkillData_원작능력_초월_임장혁_AD_A0R5', 'A0R5 !육식 — 25% +500000',
        '루치 H08W uabi A0R5(ACbh, Hbh1 25, Hbh3 500000, adur 빈칸 · ahdu 1.0 — 스턴은 ACbh 스톡 adur 미확인이라 미반영).', ON_HIT,
        sat.level_block(triggerChance=0.25, effects=[sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=HERO, bonus=500000.0)]))

    # 후지토라 H08X → 초월_이태훈_AP
    new('초월_이태훈_AP', 'SkillData_원작오라_초월_이태훈_AP_A0FW', 'A0FW 중력중력열매 — 적 이속 −55% 오라',
        '후지토라 H08X uabi A0FW(AOae, Oae1 −0.55, aare 빈칸 = 스톡 900, abuf B022).', AURA,
        sat.level_block(range=float(STOCK_AURA_RADIUS), effects=[sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.45)]))
    edit('SkillData_게이트_초월_이태훈_AP_Huji_03_Mana140', 'Huji_03: 범위 적 AId1 +9(피해 전에) 추가. 씽크홀 이감 −99%(시간제 더미 오라)는 미반영. ',
         lambda a: add(a, [sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=9.0)], front=True))
    edit('SkillData_게이트_초월_이태훈_AP_Huji01_1of7', 'Huji01 1/7: stomp A0OW(475 범위, 피해 0) 스턴 adur 2.59초 추가(485로). ',
         lambda a: add(a, [sat.effect(kind=STUN, target=ENEMIES, duration=2.59)]))

    # 프랑키 H08Y → 초월_박기찬_AD
    edit('SkillData_게이트_초월_박기찬_AD_b3948c74', 'Franky_misiile_re는 스테이지 2·4에서 2회 — hitCount 1 → 2(2배 과소였다). 곁의 써니호 수만큼 늘어나는 것은 미반영. ',
         lambda a: sat.set_effects(a, 0, [retarget(b, hitCount=2, duration=0.2) for b in sat.get_effect_blocks(a, 0)]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
