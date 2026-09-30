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

    # ════ 통째로 빠져 있던 게이지 스킬·보충(§0 순위 10 · 「통째로 없는 게이지 스킬」) ════
    # 우솝 H09B → 초월_조성진_AD: Usop_Skill_Mana — 시전자 주인 더미 stomp A0S3 600 범위 2500000·스턴(adur 빈칸)
    edit('SkillData_게이트_초월_조성진_AD_29846395', 'Usop_Skill_Mana: 시전자 주인 더미의 stomp A0S3(AOws, Wrs1 2500000, aare 600, adur 빈칸 = 스톡 3.0 · ahdu 0.45) 추가 — 고정 피해가 1000000뿐이었다(3.5배 과소). ',
         lambda a: add(a, [dmg(ENEMIES, AP, SPELLS, 2500000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    # 브룩 H09I → 초월_노태현_AP: Brook_Skill_Mana(마나 115) — stomp A0AO 600 범위 3000000·스턴 + 525 범위 AIsr +10
    new('초월_노태현_AP', 'SkillData_원작트리거_초월_노태현_AP_Brook_Skill_Mana', '브룩 — MANA게이지115(600 범위 3000000·스턴 + AIsr +10)',
        '브룩 H09I BrookAttack: 마나==115 → Brook_Skill_Mana: 시전자 주인 더미 e06V의 stomp A0AO(AOws, Wrs1 3000000, aare 600, adur 빈칸 = 스톡 3.0 · ahdu 0.45) + 대상 중심 525 적 AIsr +10(600으로). '
        '음표 더미(A0R1 보유 시 아군 공속 오라)는 미반영.', GAUGE,
        sat.level_block(range=600.0, hitCountThreshold=115, effects=[sat.effect(kind=9, target=ENEMIES, multiplier=10.0), dmg(ENEMIES, AP, SPELLS, 3000000.0),
                                                                    sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    # 염왕 조로 H0BT → 초월_강주혁_AP: Zoro_Samchun2(마나 145 — H0BT는 uabi에 A0GT가 있어 항상 이 갈래)
    new('초월_강주혁_AP', 'SkillData_원작트리거_초월_강주혁_AP_Zoro_Samchun2', '염왕 — MANA게이지145(600 범위 (1500000 + STR×15000)×1~1.5 ×3 + 2500000×1~2.5 · 스턴)',
        '염왕 조로 H0BT Zoro_Attack_enma: 마나==145 → Zoro_Samchun2: 대상 중심 600 범위 (1500000 + STR×15000)×1~1.5 CHAOS/NORMAL 3회(integerA==3까지) + 2500000×1~2.5 NORMAL/UNIVERSAL + '
        'stomp A0O4 스턴(adur 빈칸 = 스톡 3.0 · ahdu 3.0). 소환체 h07G는 미반영.', GAUGE,
        sat.level_block(range=600.0, hitCountThreshold=145, effects=[
            sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=15000.0, bonus=1500000.0, randMax=1.5, hitCount=3, duration=0.18),
            dmg(ENEMIES, AP, SPELLS, 2500000.0, randMax=2.5), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    edit('SkillData_회수_초월_강주혁_AP_b7ae9b41', 'Zoro_enfor_3dragon 첫 발 추가: 405 범위 (535000 + STR×21000) CHAOS/NORMAL(425로) — 둘째 발 532500만 있어 절반이었다. ',
         lambda a: add(a, [sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=21000.0, bonus=535000.0)], front=True))
    # 징베 H09A → 초월_최상호_AD: Jimbe_Mu(마나 115) — AId1 +5 먼저, stomp A0X8 600 범위 스턴(피해 0)
    edit('SkillData_게이트_초월_최상호_AD_3c5f8bd9', 'Jimbe_Mu: 범위 적 AId1 +5(피해보다 먼저) · stomp A0X8(600 범위, 피해 0, adur 빈칸 = 스톡 3.0 · ahdu 0.45) 스턴 추가. ',
         lambda a: sat.set_effects(a, 0, [sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=5.0)] + sat.get_effect_blocks(a, 0)
                                   + [sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))

    # ════ 묶음 B·C에서 값이 분명한 것(로빈·징베·상디) ════
    SIEGE = 3
    edit('SkillData_게이트_초월_강재규_AP_85d0117d', 'Robine_skill_2: stomp A09I(525 범위, 피해 0) 스턴 adur 2.85초 추가. ',
         lambda a: add(a, [sat.effect(kind=STUN, target=ENEMIES, duration=2.85)]))
    edit('SkillData_게이트_초월_강재규_AP_fcba0856', 'Robine_skill_3: 대상 파이어볼트 A0V9 120000·스턴 3.0초 추가. ',
         lambda a: add(a, [dmg(SINGLE, AP, SPELLS, 120000.0), sat.effect(kind=STUN, target=SINGLE, duration=3.0)]))
    edit('SkillData_게이트_초월_최상호_AD_b8d2fd85', 'Jimbe(1/16): 범위 적 AId1 +3(피해보다 먼저) · 천둥박수 A0WZ(AHtc, Htc1 500000, aare 625) 추가. 이감은 Htc3 빈칸(스톡 미확인)이라 미반영. ',
         lambda a: sat.set_effects(a, 0, [sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=3.0)] + sat.get_effect_blocks(a, 0) + [dmg(ENEMIES, AP, SPELLS, 500000.0)]))
    new('초월_최상호_AD', 'SkillData_원작트리거_초월_최상호_AD_Jimbe_jingak', '징베 — Jimbe_jingak 1/7(450 범위 1000000 + STR×17500, 그 뒤 AId1 +1)',
        '징베 H09A jinbe_Attack GetRandomInt(1,7)==2 → Jimbe_jingak: 대상 중심 450 범위 (1000000 + STR×17500 + 맞는 적 AId1 레벨×10000) CHAOS/NORMAL, 그 뒤 AId1 +1. AId1 레벨 항은 미반영(하한).', ON_HIT,
        sat.level_block(triggerChance=1.0 / 7.0, range=450.0, effects=[
            sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=17500.0, bonus=1000000.0),
            sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=1.0)]))
    new('초월_최상호_AD', 'SkillData_원작트리거_초월_최상호_AD_jinbe_추가타', '징베 — 평타 추가타 1/4(대상 1000000)',
        '징베 H09A jinbe_Attack GetRandomInt(1,4)==2: 대상 (1000000 + 대상 AId1 레벨×10000) SIEGE/NORMAL. AId1 레벨 항은 미반영(하한).', ON_HIT,
        sat.level_block(triggerChance=0.25, effects=[dmg(SINGLE, AD, SIEGE, 1000000.0)]))

    def sandi(a):
        sat.set_level_field(a, 0, 'range', 475.0)
        add(a, [dmg(ENEMIES, AP, SPELLS, 582500.0)])
    edit('SkillData_게이트_초월_배성령_AD_18aa2343', 'Sandi_skill_1: 0.11초 뒤 대상 근처 475 범위 582500 NORMAL/UNIVERSAL(UnitDamagePointLoc) 추가. ', sandi)

    # ════ 티치·아카이누·조로의 빠진 평타 블록 ════
    # 티치 H090 → 초월_임채민_AP: Tichi_Attack 1/10 안에서 1/TR_AddInt(기본 6)이면 강진(_tr), 아니면 지진 — 5/6·1/6 배타를 확률 곱으로
    edit('SkillData_더미채널_초월_임채민_AP_79행_01000', 'Tichi_skill_4(지진)는 1/10 중 5/6(나머지 1/6은 강진 Tichi_skill_4_tr) — 확률 0.1 → 0.0833. ',
         lambda a: sat.set_level_field(a, 0, 'triggerChance', round(0.1 * 5.0 / 6.0, 6)))
    new('초월_임채민_AP', 'SkillData_원작트리거_초월_임채민_AP_Tichi_skill_4_tr', '티치 — 강진 1/10 × 1/6(시전자 중심 700 범위 400000·이감 75% + 250000 + STR×3000)',
        '티치 H090 Tichi_Attack: 1/10 안에서 GetRandomInt(1,TR_AddInt=6)==3 → Tichi_skill_4_tr: 시전자 중심 — 천둥박수 A0Q6(AHtc, Htc1 400000, aare 700, Htc3 0.75, adur 3.0) + '
        '700 범위 (250000 + STR×3000) NORMAL/UNIVERSAL. T특성 뒤 1/3은 미반영.', ON_HIT,
        sat.level_block(triggerChance=round(0.1 / 6.0, 6), range=700.0, aoeCenter=1, effects=[
            dmg(ENEMIES, AP, SPELLS, 400000.0), sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.25, duration=3.0),
            sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=3000.0, bonus=250000.0)]))
    # 아카이누 H095 → 초월_김만경_AD: Akainu_03 용암분출 — 마나≠135 & 1/10(B06B 없는 대상이면 02가 안 터졌을 때 → 0.925 × 0.1)
    new('초월_김만경_AD', 'SkillData_원작트리거_초월_김만경_AD_Akainu_03', '아카이누 — Akainu_03 용암분출 0.0925(450 범위 300000 + 대상 스턴 2.25초)',
        '아카이누 H095 Akainu_Attack: 02(7.5%)가 안 터졌을 때 GetRandomInt(1,10)==5 → Akainu_03: 대상 파이어볼트 A0UX 스턴 2.25초 + 대상 위치 450 범위 300000 NORMAL/UNIVERSAL(UnitDamagePointLoc). '
        '지대(A09L 150000/0.2초 2초)·이속 −10% 더미는 미반영(주기 피해 지대 구조).', ON_HIT,
        sat.level_block(triggerChance=0.0925, range=450.0, effects=[dmg(ENEMIES, AP, SPELLS, 300000.0), sat.effect(kind=STUN, target=SINGLE, duration=2.25)]))
    # 조로 H09F → 초월_박민수_AD: Zoro_saza1 1/6 · Zoro_tiger 1/33
    new('초월_박민수_AD', 'SkillData_원작트리거_초월_박민수_AD_Zoro_saza1', '조로 — Zoro_saza1 1/6(405 범위 (602500 + STR×6000)×1~1.5 + 대상 (903750 + STR×60000)×1~1.5 · 스턴 1초)',
        '조로 H09F Zoro_Attack_re GetRandomInt(1,6)==2 → Zoro_saza1: 대상 근처 405 범위 (602500 + STR×6000)×1~1.5 CHAOS/NORMAL → 대상 (903750 + STR×60000)×1~1.5 + 파이어볼트 A0UQ 스턴 1.0초. '
        'A0GQ 레벨 2 이상의 STR 계수(7500·75000)는 미반영.', ON_HIT,
        sat.level_block(triggerChance=round(1.0 / 6.0, 6), range=405.0, effects=[
            sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=6000.0, bonus=602500.0, randMax=1.5),
            sat.effect(kind=DAMAGE, basis=STR, target=SINGLE, damageType=AD, attackType=CHAOS, multiplier=60000.0, bonus=903750.0, randMax=1.5),
            sat.effect(kind=STUN, target=SINGLE, duration=1.0)]))
    new('초월_박민수_AD', 'SkillData_원작트리거_초월_박민수_AD_Zoro_tiger', '조로 — Zoro_tiger 1/33(500 범위 2500000 + STR×50000 · stomp 150000·스턴 2.5초)',
        '조로 H09F Zoro_Attack_re GetRandomInt(1,33)==10 → Zoro_tiger: stomp A0AW(500 범위 150000, adur 2.5 · ahdu 0.37) + 대상 중심 500 범위 (2500000 + STR×50000) CHAOS/NORMAL.', ON_HIT,
        sat.level_block(triggerChance=round(1.0 / 33.0, 6), range=500.0, effects=[
            dmg(ENEMIES, AP, SPELLS, 150000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.5),
            sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=50000.0, bonus=2500000.0)]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
