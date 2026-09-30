"""초월함 남은 것 — 스네이크맨·키드·야마토·초록소·니카 + 아카이누 LIFE 50·우솝 1/9·티치 크로우즈(2026-09-30 구현담당1, PM 지시 — TRANSCEND_FILL_LIST §3~8).

포크가 쓴 절이라 넣는 블록마다 평타 트리거의 게이트 줄·RRD 줄·더미 능력 필드를 다시 열어 봤다. 규칙은 apply_immortal_blocks.py 머리 주석과 같다. 다시 돌려도 같은 결과.
확인만 하고 안 고친 것: 아카이누 유성 664,286 — 에셋 설명에 「흩어지는 낙하 지점의 대상 한 명 기준 기댓값」으로 적힌 의도된 보정(원문 1,000,000).
안 넣은 것: 형태 전환(블랙맘바 H0B3 · 초록소 H0BO · 야마토 H0BD) · T특성·아이템 갈래 · 「대상 주변 적 수」 계수(1 + 0.01n — n = 1로) · 야마토 뇌명의 이속 역비례 계수(이속 300 기준 1.13으로 고정) ·
  키드 충전 연타 · 초록소 A15I 주기 피해 · 우솝 1/7(주체 미확인) · 니카 B078 동안 공속 감소 · 티치 A11Q(받는 피해 +10%).
돌린 뒤 `python3 Tools/sync_mana_regen_from_w3u.py`로 LIFE 게이지 필드(황준석 = 키드 H0B4 · 김만경 = 아카이누 H095)를 채운다.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg  # noqa: E402

SELF, ENEMIES, SINGLE = 0, 2, 3
DAMAGE, STUN, AISR, FLAT, SPEED = 0, 1, 9, 11, 12
AD, AP = 1, 2
PIERCE, HERO, CHAOS, SPELLS = 2, 4, 5, 7
RECEIVED, MOVESPEED, STR = 5, 6, 9
PV_LT, PV_EQ, PV_GE = 1, 2, 3
ON_HIT, GAUGE = 0, 3
MANA, LIFE = 0, 1
CASTER = 1
TAG = '[초월 남은 것 09-30]'


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
        if note(a, text + '(Tools/apply_transcend_rest2.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def add(a, extra, front=False):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, extra + b if front else b + extra)

    def eff(basis, target, dt, at, mult, bonus=0.0, **kw):
        return sat.effect(kind=DAMAGE, basis=basis, target=target, damageType=dt, attackType=at, multiplier=mult, bonus=bonus, **kw)

    # ── 스네이크맨 H0B2 → 초월_김경현_AP: Snake_Attack 35% — 대상 중심 400: 75000 × (1 + 0.003×이속) ×1~2 HERO/NORMAL
    new('초월_김경현_AP', 'SkillData_원작트리거_초월_김경현_AP_Snake_Attack_35', '스네이크맨 — 평타 범위 35%(400 범위 75000 × (1 + 0.003×이속) ×1~2)',
        '스네이크맨 H0B2 Snake_Attack: GetRandomInt(1,100)<36 → 대상 중심 400 범위 75000×(1 + 0.01×이속×0.30) ×1~2 HERO/NORMAL. 블랙맘바 형태(112500)는 미반영.', ON_HIT,
        sat.level_block(triggerChance=0.35, range=400.0, effects=[eff(MOVESPEED, ENEMIES, AD, HERO, 225.0, 75000.0, randMax=2.0)]))

    # ── 키드 H0B4 → 초월_황준석_ADAP
    def repel(a):
        keep = [b for b in sat.get_effect_blocks(a, 0) if re.search(r'multiplier: 300000', b)]
        assert len(keep) == 1
        sat.set_level_field(a, 0, 'range', 450.0)
        sat.set_effects(a, 0, keep + [dmg(ENEMIES, AP, SPELLS, 300000.0)])
    edit('SkillData_더미채널_초월_황준석_ADAP_79행_01000', 'Kid_Skill_3(아이템 없음): 장풍 A10Z 300000(단일 근사) + 대상 중심 450 범위 300000 NORMAL/UNIVERSAL. '
         '600000은 아이템 갈래(Kid_Skill_3_item의 A17P)라 뺐다. ', repel)

    def punk(a):
        sat.set_level_field(a, 0, 'range', 500.0)
        sat.set_effects(a, 0, [eff(STR, ENEMIES, AP, SPELLS, 5000.0, 500000.0), eff(STR, SINGLE, AP, SPELLS, 10000.0, 250000.0),
                               dmg(SINGLE, AP, SPELLS, 99999.0), sat.effect(kind=STUN, target=SINGLE, duration=3.0)])
        sat.set_head_field(a, 'skillName', '키드 — Kid_Skill_Life2 펑크 1/16(500 범위 500000 + STR×5000 · 대상 (125000 + STR×5000)×2 · 스턴 3초)')
    edit('SkillData_더미채널_초월_황준석_ADAP_79행_10000', 'Kid_Skill_Life2(1/16): 단일 500000 → 대상 중심 500 범위 (500000 + STR×5000) + 대상 (125000 + STR×5000)×(1 + n)(n = 대상 125 안 적 수 — 1로) + '
         '파이어볼트 A10L 99999·스턴 3.0초. 이름표가 「키자루 레이저」로 잘못 붙어 있었다(값·확률은 키드 것). ', punk)
    new('초월_황준석_ADAP', 'SkillData_원작트리거_초월_황준석_ADAP_Kid_Skill_Mana', '키드 체력 스킬 — LIFE게이지50(500 범위 2000000·스턴 2초 + 대상 1000000 + STR×50000)',
        '키드 H0B4 Kid_Attack: 체력==50 → Kid_Skill_Mana(T특성 없음): stomp A110(AOws, Wrs1 2000000, aare 500, adur 2.0) + 대상 125 안 적에게 (1000000 + STR×50000)×(1 + 0.01n) NORMAL/UNIVERSAL(대상 하나로 근사).',
        GAUGE, sat.level_block(range=500.0, hitCountThreshold=50, resetTo=1, gaugeKind=LIFE, effects=[
            dmg(ENEMIES, AP, SPELLS, 2000000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.0), eff(STR, SINGLE, AP, SPELLS, 50000.0, 1000000.0)]))

    # ── 야마토 H0BE → 초월_구주호_AD
    def yamato_base(a):
        sat.set_level_field(a, 0, 'range', 325.0)
        sat.set_level_field(a, 0, 'forbiddenBuffId', 'B06N')
        sat.set_effects(a, 0, [retarget(b, hitCount=1) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_게이트_초월_구주호_AD_355f5d39', 'Yamato_Attack_Damage2 기본 갈래: B06N 없을 때 대상 중심 325 범위 K×0.75 + 15000 1회 — 425 범위·2회였다(2배 과다). forbiddenBuffId B06N. ', yamato_base)
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Yamato_Damage2_강화', '야마토 — 매 타(B06N 동안: 425 범위 K×0.75 + 15000 · K×0.20 + 15000)',
        '야마토 H0BE Yamato_Attack_Damage2: B06N 있음 → 대상 중심 425 범위 (K×0.75 + 15000) CHAOS/NORMAL + (K×0.20 + 15000) CHAOS/UNIVERSAL.', ON_HIT,
        sat.level_block(range=425.0, requiredBuffId='B06N', effects=[eff(RECEIVED, ENEMIES, AD, CHAOS, 0.75, 15000.0), eff(RECEIVED, ENEMIES, AP, CHAOS, 0.20, 15000.0)]))
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Yamato_공속', '야마토 — 1/4 자기 공속 +400% 1.25초(B06N)',
        '야마토 H0BE Yamato_Attack_insu: GetRandomInt(1,4)==4 → 자기에게 A12F(Ablo, Blo1 +4.0 공속, adur 1.25, abuf B06N).', ON_HIT,
        sat.level_block(triggerChance=0.25, effects=[sat.effect(kind=SPEED, target=SELF, multiplier=4.0, duration=1.25, buffId='B06N')]))
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Yamato_뇌명', '야마토 — 뇌명 15%(525 범위 (K×3.75 + 375000) × 이속 계수)',
        '야마토 H0BE Yamato_Attack_Damage2: GetRandomInt(1,100)<=15 → 대상 중심 525 범위 (K×3.75 + 375000)×(0.83 + clamp(90/이속, 0, 1.17)) CHAOS/UNIVERSAL. '
        '이속 역비례 계수는 축이 없어 원작 이속 300 기준 1.13으로 고정(느린 적일수록 최대 ×2.0인 것은 미반영).', ON_HIT,
        sat.level_block(triggerChance=0.15, range=525.0, effects=[eff(RECEIVED, ENEMIES, AP, CHAOS, round(3.75 * 1.13, 4), round(375000.0 * 1.13, 1))]))
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Yamato_Dash', '야마토 — Yamato_Dash 매 타(450 범위 K×1.15 + 215000 · 대상 ×1.25)',
        '야마토 H0BE Yamato_Dash(매 타): 대상 중심 450 범위 (직전 평타 피해×1.15 + 215000) NORMAL/UNIVERSAL + 대상에 같은 값 ×1.25. 「직전 타의 피해」는 같은 타의 피해로 근사.', ON_HIT,
        sat.level_block(range=450.0, effects=[eff(RECEIVED, ENEMIES, AP, SPELLS, 1.15, 215000.0), eff(RECEIVED, SINGLE, AP, SPELLS, 1.4375, 268750.0)]))

    # ── 초록소 H0BL → 초월_구주호_AD
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Rokugu_Tree_S', '초록소 — MANA게이지115(405 범위 2000000×1~1.5 ×5 + 자기 공속 +5%·공격력 +4000 6.85초)',
        '초록소 H0BL Rokugu_Attack: 마나≥115 → Rokugu_Tree_S: 대상 중심 405 범위 (2000000 + 충전×4000 + INT×5000)×1~1.5 CHAOS/NORMAL 5회 + 자기 A16S(공격력 +4000)·A15R(공속 +5%) 6.85초(충전 0 기준). '
        '⚠️ 이 로스터의 마나 게이지는 키자루(H0B5)와 같이 쓴다. H0BO 변신·1/12 추가 1회·충전 계수·퍼지는 고리 오라는 미반영.',
        GAUGE, sat.level_block(range=405.0, hitCountThreshold=115, gaugeKind=MANA, effects=[
            dmg(ENEMIES, AD, CHAOS, 2000000.0, randMax=1.5, hitCount=5, duration=1.6),
            sat.effect(kind=SPEED, target=SELF, multiplier=0.05, duration=6.85, buffId='A15R'),
            sat.effect(kind=FLAT, target=SELF, multiplier=4000.0, duration=6.85, buffId='A16S')]))
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Rokugu_흡수', '초록소 — 전방위 흡수 1/25(시전자 중심 925 범위 1000000×1~1.5)',
        '초록소 H0BL Rokugu_Attack: GetRandomInt(1,25)==2 → 시전자 중심 925 범위 (1000000 + 충전×1825)×1~1.5 CHAOS/NORMAL. 마나 +5·충전은 미반영.', ON_HIT,
        sat.level_block(triggerChance=0.04, range=925.0, aoeCenter=CASTER, effects=[dmg(ENEMIES, AD, CHAOS, 1000000.0, randMax=1.5)]))

    # ── 니카 H0BK → 영원_이지원
    edit('SkillData_게이트_영원_이지원_Mana150', '시전자 주인 더미 e0QK의 stomp A165(AOws, Wrs1 2500000, aare 600, adur 2.5) 추가(750 범위로). ',
         lambda a: add(a, [dmg(ENEMIES, AP, SPELLS, 2500000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.5)]))
    for stem in ('SkillData_게이트_영원_이지원_B078_18', 'SkillData_게이트_영원_이지원_Punch_4'):
        edit(stem, '더미 e0RB의 stomp A16F(AOws, Wrs1 150000, aare 500, adur 2.0) 추가. ',
             lambda a: add(a, [dmg(ENEMIES, AP, SPELLS, 150000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.0)]))

    # ── 아카이누 H095 → 초월_김만경_AD: LIFE==50 & 대상 PV==200 → Akainu_02(대분화). 체력은 재생(uhpr 0.5)으로만 찬다.
    new('초월_김만경_AD', 'SkillData_원작트리거_초월_김만경_AD_Akainu_02_LIFE', '아카이누 체력 스킬 — LIFE게이지50(PV 200 대상: 대분화)',
        '아카이누 H095 Akainu_Attack: 체력==50 & 대상 PV==200 → 체력 1 + Akainu_02: 대상 파이어볼트 A0VL 스턴 3.0초 + 대상 위치 600 범위 (725000 + STR×5000) + 대상 (7250000 + STR×50000)×1~2.5 NORMAL/UNIVERSAL. '
        '⚠️ 효과마다 PV==200 조건 — 게이지는 PV 200이 아닌 대상에서도 소모된다(원문은 PV 200을 칠 때까지 50을 들고 있는다, 스킬 단위 대상 조건 축 없음 — 히든 아카이누와 같은 근사). 지대는 미반영.',
        GAUGE, sat.level_block(range=600.0, hitCountThreshold=50, resetTo=1, gaugeKind=LIFE, effects=[
            eff(STR, ENEMIES, AP, SPELLS, 5000.0, 725000.0, targetCondition=PV_EQ, targetConditionValue=200.0),
            eff(STR, SINGLE, AP, SPELLS, 50000.0, 7250000.0, randMax=2.5, targetCondition=PV_EQ, targetConditionValue=200.0),
            sat.effect(kind=STUN, target=SINGLE, duration=3.0, targetCondition=PV_EQ, targetConditionValue=200.0)]))

    # ── 우솝 H09B → 초월_조성진_AD: 1/9 — 600 범위 600000×1~1.5 PIERCE/NORMAL + 대상 (450000 + 1500×PV)×1~1.5 NORMAL/UNIVERSAL
    new('초월_조성진_AD', 'SkillData_원작트리거_초월_조성진_AD_Usop_Attack_1of9', '우솝 — 1/9(600 범위 600000×1~1.5 + 대상 (450000 + 1500×PV)×1~1.5)',
        '우솝 H09B Usop_Attack: GetRandomInt(1,9)==2 → 대상 중심 600 범위 600000×1~1.5 PIERCE/NORMAL + 대상 (450000 + 1500×PV)×1~1.5 NORMAL/UNIVERSAL. '
        'PV 항은 <200은 100(600000) · 200(750000) · ≥300(900000)으로 나눠 상수로.', ON_HIT,
        sat.level_block(triggerChance=round(1.0 / 9.0, 6), range=600.0, effects=[
            dmg(ENEMIES, AD, PIERCE, 600000.0, randMax=1.5),
            dmg(SINGLE, AP, SPELLS, 600000.0, randMax=1.5, targetCondition=PV_LT, targetConditionValue=200.0),
            dmg(SINGLE, AP, SPELLS, 750000.0, randMax=1.5, targetCondition=PV_EQ, targetConditionValue=200.0),
            dmg(SINGLE, AP, SPELLS, 900000.0, randMax=1.5, targetCondition=PV_GE, targetConditionValue=300.0)]))

    # ── 티치 H090 → 초월_임채민_AP: Tichi_skill_1 크로우즈 — 대상 AIsr +10
    edit('SkillData_게이트_초월_임채민_AP_da9fb163', 'Tichi_skill_1: 대상 AIsr +10(피해 전에) 추가. A11Q(받는 피해 +10%)는 미반영. ',
         lambda a: add(a, [sat.effect(kind=AISR, target=SINGLE, multiplier=10.0)], front=True))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
