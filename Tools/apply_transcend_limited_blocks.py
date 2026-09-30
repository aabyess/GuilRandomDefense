"""초월함·제한됨 등급 큰 어긋남 정정 + 받이 로스터 오라(2026-09-30 구현담당1 — Docs/research/TRANSCEND_FILL_LIST.md §0 순위 1~9 · §9, LIMITED_FILL_LIST.md X1~X4 · U1~U3).

PM 우선순위(배율 큰 것부터)만 넣었다. 블록마다 war3map_new.j 평타 트리거의 게이트 줄·RRD 줄과 더미 능력 필드(w3a)를 다시 열어 봤다. 다시 돌려도 같은 결과.
규칙은 apply_immortal_blocks.py 머리 주석과 같다(피해 타입 대응 · 스턴 = adur · 반경이 다른 묶음은 한 레벨 반경으로 뭉갬을 설명에 적음).
  - ACbh Hbh1 빈칸 = 스톡 15%(TRANSCEND_FILL_LIST §9-1 — 맵의 ACbh 명시값에 15만 0건, 툴팁 15%).
  - 「한 굴림의 배타 분기」(루피 17.5/82.5, 사보 1/10 → 아니면 1/6)는 독립 굴림 확률 곱으로 근사.
  - 도플라밍고 각성형(H09D)은 형태 전환 구조 없이 「마나 150 → 자기 버프 B00X 4.5초(A0AL ahdu)」 창으로 근사: 각성형 블록은 requiredBuffId, 일반형 블록은 forbiddenBuffId.
  - 「원작엔 없는데 우리엔 있음」 후보(이충민 매 타 15만 + 범위 75만 등)는 안 건드린다(사장님 보고 대상).
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg  # noqa: E402

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, ARMOR_BREAK, ARMOR_BONUS, APPLY_BUFF, FLAT, SPEED, SLOW, PERCENT = 0, 1, 2, 4, 6, 11, 12, 13, 14
AD, AP = 1, 2
PIERCE, HERO, CHAOS, MAGIC, SPELLS = 2, 4, 5, 6, 7
MAXHP, CURHP, ATK, RECEIVED, STR = 1, 2, 3, 5, 9
PV_LT, PV_EQ, PV_GE, PV_NE = 1, 2, 3, 4
ON_HIT, AURA, GAUGE = 0, 2, 3
STOCK_ACBH_HBH1 = 0.15   # ⚠️ slk 미확인, 머리 주석
STOCK_AURA_RADIUS = 900
PROPORTIONAL = 1000000.0  # casterBuffCountFactor는 「×(1 + 계수×n)」 꼴 — 값을 1/이만큼 줄이고 계수를 이만큼 키우면 「n에 정비례」가 된다(오차 1e-6)
TAG = '[초월·제한 블록 09-30]'


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


def to_on_hit(a, chance, **level_fields):
    a.head = re.sub(r'^  triggerType: \d+$', '  triggerType: 0', a.head, flags=re.M)
    sat.set_level_field(a, 0, 'hitCountThreshold', 0)
    sat.set_level_field(a, 0, 'triggerChance', chance)
    for k, v in level_fields.items():
        sat.set_level_field(a, 0, k, v)


def main():
    changed = []

    def new(roster, stem, name, desc, trigger, level):
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc + ' 2026-09-30 구현담당1.', trigger, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_transcend_limited_blocks.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def aura(roster, tail, name, desc, radius, effects):
        new(roster, 'SkillData_원작오라_%s_%s' % (roster, tail), name, desc, AURA, sat.level_block(range=float(radius), effects=effects))

    def cond(a, c, v=200.0):
        sat.set_effects(a, 0, [retarget(b, targetCondition=c, targetConditionValue=v) for b in sat.get_effect_blocks(a, 0)])

    # ════ 사보 H092 → 초월_두유찬_AD: Sabo_Attack — 마나==125면 Sabo_Mana, **아니면**(else) B00N 없음 → Skill_1 / 있음 → 1/10 Skill_3, 아니면 1/6 Skill_4 ════
    def sabo1(a):
        to_on_hit(a, 1.0, cooldown=12.5, selfBuffId='B00N')
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [
            dmg(ENEMIES, AD, HERO, 1000000.0 / PROPORTIONAL, casterBuffCountFactor=PROPORTIONAL),
            sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=HERO, multiplier=15000.0 / PROPORTIONAL, casterBuffCountFactor=PROPORTIONAL),
            dmg(SINGLE, AP, CHAOS, 2500000.0, casterBuffCountFactor=0.2),
            sat.effect(kind=STUN, target=ENEMIES, duration=0.85)])
        sat.set_head_field(a, 'skillName', '사보 — Sabo_Skill_1 화염용왕(절대쿨 12.5초 B00N)')
    edit('SkillData_절대쿨_초월_두유찬_AD_1', 'Sabo_Skill_1은 「마나==125」가 아니라 그 else 가지 — B00N(A08X AUfa, Ufa1 12.5초 절대쿨) 없으면 매 타. 발동 방식 OnHitCount(마나 125) → 평타 확률 1 + cooldown 12.5 + selfBuffId B00N. '
         '효과 추가: 475 범위 (1000000 + STR×15000)×시전자 버프 수 HERO/NORMAL · 대상 2500000 + 500000×버프 수 CHAOS/UNIVERSAL · stomp A0C1 스턴 0.85초(원문 600 범위 — 475로). ', sabo1)

    def sabo3(a):
        to_on_hit(a, 0.1, requiredBuffId='B00N')
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.7, duration=2.5)])
        sat.set_head_field(a, 'skillName', '사보 — Sabo_Skill_3 화권(B00N 동안 1/10)')
    edit('SkillData_절대쿨_초월_두유찬_AD_3', 'Sabo_Skill_3: 「마나 125 AND 1/10」 → B00N 동안 1/10(≈125배 과소였다). 천둥박수 A0PT 이감 30%(Htc3 0.3) 2.5초 추가(원문 500 범위 — 415로). ', sabo3)

    def sabo4(a):
        to_on_hit(a, 0.15, requiredBuffId='B00N')
        sat.set_head_field(a, 'skillName', '사보 — Sabo_Skill_4 작열(B00N 동안 9/10 × 1/6)')
    edit('SkillData_절대쿨_초월_두유찬_AD_4', 'Sabo_Skill_4: 「마나 125 AND 1/60」 → B00N 동안 (9/10)×(1/6) = 0.15(≈1100배 과소였다). ', sabo4)
    edit('SkillData_게이트_초월_두유찬_AD_8eab1c6f', 'Sabo_Mana: stomp A0C3(500 범위, 피해 0) 스턴 1.25초 추가. ',
         lambda a: sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=STUN, target=ENEMIES, duration=1.25)]))
    new('초월_두유찬_AD', 'SkillData_원작능력_초월_두유찬_AD_A0HA', 'A0HA !용의 숨결 — 15% ×4.0',
        '원작 사보 H092 uabi A0HA(ACbh, Hbh2 4.0, Hbh1 빈칸 = 스톡 15%, 피해·스턴 0).', ON_HIT,
        sat.level_block(triggerChance=STOCK_ACBH_HBH1, effects=[sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=HERO, multiplier=4.0)]))
    aura('초월_두유찬_AD', 'A0H2', 'A0H2 용조권 — 적 방어 −30 오라', '원작 사보 H092 uabi A0H2(AHad, Had1 −30, aare 850, abuf B01M).',
         850, [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-30.0, buffId='B01M')])
    aura('초월_두유찬_AD', 'A0E6', 'A0E6 투기 — 적 이속 −20% 오라', '원작 사보 H092 uabi A0E6(AOae, Oae1 −0.2, aare 800, abuf B01C).',
         800, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.8)])
    # Skill_3·4(B00N 필요)가 Skill_1(B00N을 거는 쪽)보다 먼저 판정되게 — 창을 연 타엔 3·4가 안 나간다(원문 if/else).
    p = os.path.join(sat.ROSTER_DIR, '초월_두유찬_AD.asset')
    t = open(p, encoding='utf-8').read()
    g1 = sat.guid_of(sat.load('SkillData_절대쿨_초월_두유찬_AD_1').path)
    line1 = '  - {fileID: 11400000, guid: %s, type: 2}\n' % g1
    rest = [sat.guid_of(sat.load('SkillData_절대쿨_초월_두유찬_AD_%d' % i).path) for i in (3, 4)]
    last = max(t.index(g) for g in rest)
    if line1 in t and t.index(line1) < last:
        t = t.replace(line1, '')
        end = t.index('\n', max(t.index(g) for g in rest)) + 1
        t = t[:end] + line1 + t[end:]
        open(p, 'w', encoding='utf-8').write(t)
        changed.append(p)

    # ════ 로우 H096 → 초월_양재모_AD ════
    def law5(a):
        sat.set_level_field(a, 0, 'requiredBuffId', '')
        blocks = [b for b in sat.get_effect_blocks(a, 0) if not re.search(r'^\s*-? ?kind: 7$', b, re.M)]
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 150000.0, hitCount=2, duration=0.1)] + blocks)
        sat.set_head_field(a, 'skillName', '로우 — Law_skill_5 라디오 나이프(1/20)')
    edit('SkillData_회수_초월_양재모_AD_f54a123f', 'Law_skill_5는 GetRandomInt(1,20)==6 독립 if — B03Z 조건·RemoveBuff B03Z는 이 블록 것이 아니다(룸 베기 몫) → 둘 다 뺌(≈17배 과소였다). '
         '앞의 550 범위 150000 NORMAL/UNIVERSAL 2회 추가(575로). 현재체력 1.25%는 원문이 주 대상 체력 기준 — 맞는 적마다 제 체력으로 근사한 채. ', law5)

    def room(a):
        sat.set_level_field(a, 0, 'range', 825.0)
        sat.set_level_field(a, 0, 'aoeCenter', 1)
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [
            sat.effect(kind=PERCENT, target=SELF, multiplier=0.2, duration=5.0, buffId='B008'),
            sat.effect(kind=PERCENT, target=ALLIES, multiplier=0.2, duration=5.0, buffId='B008'),
            sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.55, duration=5.0)])
    edit('SkillData_버프게이트_초월_양재모_AD_B03Z', '룸(마나 135): 더미 e03U(수명 5초)의 오라 둘 추가 — A0QD 아군(자기 포함) 공격력 +20%·825(B008) · A03V 적 이속 −45%·825. 시전 순간 825 안에 있던 대상에 5초(더미가 서 있는 동안 드나드는 것은 미반영). ', room)

    # ════ 루피 H099 → 초월_신문철_AP: Ruffy_AttackDamage — 1~1000 한 굴림, <176이면 425 범위 ×2.5 + 32500(+ 대상 한 번 더), 아니면 345 범위 ×0.8 + 25000 ════
    def ruffy(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.175)
        sat.set_level_field(a, 0, 'range', 425.0)
        sat.set_effects(a, 0, [sat.effect(kind=DAMAGE, basis=RECEIVED, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=2.5, bonus=32500.0)]
                        + sat.get_effect_blocks(a, 0))
        sat.set_head_field(a, 'skillName', '루피 — Ruffy_AttackDamage 제트 피스톨(17.5%: 425 범위 + 대상)')
    edit('SkillData_게이트_초월_신문철_AP_355f5d39', 'Ruffy_AttackDamage 17.5% 갈래: 확률 1.0 → 0.175(단일 ×2.5가 매 타 — 5.7배 과다였다) · 425 범위 (평타 피해×2.5 + 32500) CHAOS/NORMAL 추가(대상 한 번 더 CHAOS/UNIVERSAL은 있던 것). ', ruffy)
    new('초월_신문철_AP', 'SkillData_원작트리거_초월_신문철_AP_Ruffy_AttackDamage_기본', '루피 — Ruffy_AttackDamage 기본(82.5%: 345 범위 평타 피해×0.8 + 25000)',
        '원작 루피 H099 Ruffy_AttackDamage: 1~1000 굴림이 176 이상(82.5%)이면 대상 중심 345 범위 (GetEventDamage×0.8 + 25000) CHAOS/NORMAL. 17.5% 갈래와 한 굴림의 배타 — 독립 굴림으로 근사.',
        ON_HIT, sat.level_block(triggerChance=0.825, range=345.0,
                                effects=[sat.effect(kind=DAMAGE, basis=RECEIVED, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=0.8, bonus=25000.0)]))

    def ruffy_mana(a):
        blocks = sat.get_effect_blocks(a, 0)
        single = [i for i, b in enumerate(blocks) if re.search(r'^\s*target: 3$', b, re.M)]
        assert len(single) == 1, single
        blocks[single[0]] = retarget(blocks[single[0]], multiplier=0.045, bonus=2625000.0)
        sat.set_effects(a, 0, [sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=7.0)] + blocks + [
            dmg(SINGLE, AP, SPELLS, 5000.0), sat.effect(kind=STUN, target=SINGLE, duration=2.15),
            dmg(ENEMIES, AP, SPELLS, 100000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.5)])
    edit('SkillData_게이트_초월_신문철_AP_55ecbfb2', 'Ruffy_Mana 레드호크: 대상분은 같은 식 ×1.5(2625000 + 최대체력 4.5%) · 범위 적 AId1 +7 · 대상 파이어볼트 A13M 5000·스턴 2.15초 · stomp A09R 100000·스턴 2.5초(원문 600 범위 — 625로). 연구 R01V 계수 미반영. ', ruffy_mana)
    for ab, chance, mult, bonus, c, text in [
            ('A0KE', 0.0625, 0.0, 625000.0, PV_NE, 'A0KE !숙련된 패기 — 6.25% +625000(PV 200 아닌 적)'),
            ('A0KA', 0.1062, 0.0, 1062500.0, PV_EQ, 'A0KA !레드호크 — 10.62% +1062500(PV 200 대상만)'),
            ('A051', 0.18, 5.0, 0.0, 0, 'A051 !고무고무 열매 — 18% ×5.0')]:
        new('초월_신문철_AP', 'SkillData_원작능력_초월_신문철_AP_%s' % ab, text,
            '원작 루피 H099 uabi %s(ACbh). atar nonancient = PV≠200 · ancient = PV 200(utyp 전수). 스턴은 adur 빈칸(ACbh 스톡 미확인)이라 미반영(ahdu 1.0).' % ab, ON_HIT,
            sat.level_block(triggerChance=chance, effects=[sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=HERO, multiplier=mult, bonus=bonus,
                                                                      targetCondition=c, targetConditionValue=200.0 if c else 0.0)]))

    # ════ 도플라밍고 H09E(일반)·H09D(각성) → 초월_최상호_AP: 각성 창 = 자기 버프 B00X 4.5초 ════
    gak, normal = dict(requiredBuffId='B00X'), dict(forbiddenBuffId='B00X')

    def gate(fields, chance=None, name=None):
        def fn(a):
            for k, v in fields.items():
                sat.set_level_field(a, 0, k, v)
            if chance is not None:
                sat.set_level_field(a, 0, 'triggerChance', chance)
            if name:
                sat.set_head_field(a, 'skillName', name)
        return fn
    edit('SkillData_게이트_초월_최상호_AP_355f5d39', '각성형 H09D 전용 블록(DP_AttackDamagegaksung) — 상시 매 타로 돌고 있었다(원문 가동률 ≈4.6%, ≈22배 과다). requiredBuffId B00X(각성 창). ',
         gate(gak, name='도플라밍고 [각성] — 매 타 350 범위 평타 피해×0.85 + 85000'))
    edit('SkillData_회수_초월_최상호_AP_355f5d39', '일반형 H09E 전용 블록(DP_AttackDamage) — forbiddenBuffId B00X(각성 창 밖). ',
         gate(normal, name='도플라밍고 [일반] — 매 타 275 범위 평타 피해×0.5 + 50000'))
    edit('SkillData_게이트_초월_최상호_AP_18aa2343', 'DP_Skill_3 1/6은 각성형 확률 — requiredBuffId B00X. 일반형 1/8은 따로 에셋. ', gate(gak, name='도플라밍고 [각성] — DP_Skill_3 1/6'))
    edit('SkillData_게이트_초월_최상호_AP_5c20b80f', 'DP_Skill_2 1/9는 각성형 확률 — requiredBuffId B00X. 일반형 1/10은 따로 에셋. ', gate(gak, name='도플라밍고 [각성] — DP_Skill_2 1/9'))
    edit('SkillData_더미채널_초월_최상호_AP_79행_10000', '거미줄 1/7은 일반형(DP_Attack) 블록 — forbiddenBuffId B00X. ', gate(normal))
    new('초월_최상호_AP', 'SkillData_원작트리거_초월_최상호_AP_DP_Skill_3_일반', '도플라밍고 [일반] — DP_Skill_3 1/8(525 범위 750000 + STR×10000)',
        '원작 도플라밍고 H09E DP_Attack: GetRandomInt(1,8)==2 → DP_Skill_3(대상 중심 525: 750000 + STR×10000 PIERCE/NORMAL). 각성 창(B00X) 밖에서만.',
        ON_HIT, sat.level_block(triggerChance=0.125, range=525.0, forbiddenBuffId='B00X',
                                effects=[sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=PIERCE, multiplier=10000.0, bonus=750000.0)]))
    new('초월_최상호_AP', 'SkillData_원작트리거_초월_최상호_AP_DP_Skill_2_일반', '도플라밍고 [일반] — DP_Skill_2 1/10(대상 200000 + PV<200 현재체력 25%)',
        '원작 도플라밍고 H09E DP_Attack: GetRandomInt(1,10)==6 → DP_Skill_2(대상 200000 NORMAL/UNIVERSAL + PV<200이면 현재체력 25%×A11S 인자 CHAOS/NORMAL). 각성 창(B00X) 밖에서만.',
        ON_HIT, sat.level_block(triggerChance=0.1, forbiddenBuffId='B00X', effects=[
            dmg(SINGLE, AP, SPELLS, 200000.0),
            sat.effect(kind=DAMAGE, basis=CURHP, target=SINGLE, damageType=AD, attackType=CHAOS, multiplier=0.25, targetCondition=PV_LT, targetConditionValue=200.0)]))
    new('초월_최상호_AP', 'SkillData_원작트리거_초월_최상호_AP_DP_새장', '도플라밍고 [각성] — 새장 1/5(525 범위 150000·이감 4.5초 + 현재체력 1% + 10000 + 400000×1~1.5)',
        '원작 도플라밍고 H09D DP_Attack_gaksung: GetRandomInt(1,5)==2 → 시전자 주인 더미 e0IW의 천둥박수 A0AM(AHtc, Htc1 150000, aare 525, Htc3 2.5 = 최저 이속, adur 4.5) + '
        '대상 중심 525: (현재체력 1% + 10000) NORMAL/UNIVERSAL + 400000×1~1.5 CHAOS/NORMAL. 각성 창(B00X) 동안만. 더미의 −70% 오라(A0VB ≈2초)는 미반영.',
        ON_HIT, sat.level_block(triggerChance=0.2, range=525.0, requiredBuffId='B00X', effects=[
            dmg(ENEMIES, AP, SPELLS, 150000.0), sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.0, duration=4.5),
            sat.effect(kind=DAMAGE, basis=CURHP, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=0.01, bonus=10000.0, skipDamageTakenMultiplier=1),
            dmg(ENEMIES, AD, CHAOS, 400000.0, randMax=1.5)]))
    new('초월_최상호_AP', 'SkillData_원작트리거_초월_최상호_AP_각성창', '도플라밍고 — MANA게이지150 각성 창(B00X 4.5초)',
        '원작 도플라밍고 H09E DP_Attack: 마나==150 → metamorphosis(A0AL AEme, abuf B00X, ahdu 4.5초 — T특성 레벨 2는 6.0초)로 각성형 H09D. '
        '형태 전환 구조가 없어 자기 버프 B00X 4.5초로 근사(각성형의 평타 주기 0.59·공격 타입·마나 정지·방깎 오라 A0AJ −60은 미반영).',
        GAUGE, sat.level_block(hitCountThreshold=150, effects=[sat.effect(kind=APPLY_BUFF, target=SELF, duration=4.5, buffId='B00X')]))

    # ════ 받이 로스터 초월_황준석_ADAP — uabi 강타 정정(§9-1) + 상시 오라(§9-2) ════
    edit('SkillData_원작능력_초월_황준석_ADAP_A0G4', 'A0G4 atar air,ancient,enemies,ground — ancient(= PV 200) 대상만. 모든 적에게 매 타 스턴 1.0초(주기 0.58초 → 영구 스턴)였다. ', lambda a: cond(a, PV_EQ))

    def bash15(stun):
        def fn(a):
            sat.set_level_field(a, 0, 'triggerChance', STOCK_ACBH_HBH1)
            if stun:
                sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=STUN, target=SINGLE, duration=stun)])
        return fn
    edit('SkillData_원작능력_초월_황준석_ADAP_A0CS', 'Hbh1 빈칸 = 스톡 15% — 확률 1.0 → 0.15(6.7배 과다였다) · 스턴 adur 3.0초 추가. ', bash15(3.0))
    edit('SkillData_원작능력_초월_황준석_ADAP_A0HK', 'Hbh1 빈칸 = 스톡 15% — 확률 1.0 → 0.15 · 스턴 adur 5.0초 추가. ', bash15(5.0))
    edit('SkillData_원작능력_초월_황준석_ADAP_A0HA', 'Hbh1 빈칸 = 스톡 15% — 확률 1.0 → 0.15. ', bash15(0))
    edit('SkillData_원작능력_초월_황준석_ADAP_A0KE', 'atar nonancient — PV 200 아닌 적에게만. ', lambda a: cond(a, PV_NE))
    edit('SkillData_원작능력_초월_황준석_ADAP_A0KA', 'atar ancient — PV 200 대상에게만. ', lambda a: cond(a, PV_EQ))
    R = '초월_황준석_ADAP'
    for tail, name, desc, radius, effects in [
            ('A0WL', 'A0WL 의협 — 아군 공속 +20% 오라', '징베 h04K uabi A0WL(AOae, Oae2 +0.2, aare 850, atar …self…friend, abuf B052).', 850,
             [sat.effect(kind=SPEED, target=t, multiplier=0.2, buffId='B052') for t in (SELF, ALLIES)]),
            ('A0H2', 'A0H2 용조권 — 적 방어 −30 오라', '사보 h04N uabi A0H2(AHad, Had1 −30, aare 850, abuf B01M).', 850, [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-30.0, buffId='B01M')]),
            ('A0E6', 'A0E6 투기 — 적 이속 −20% 오라', '사보 h04N uabi A0E6(AOae, Oae1 −0.2, aare 800, abuf B01C).', 800, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.8)]),
            ('A0AJ', 'A0AJ 열매각성 방깍 — 적 방어 −60 오라', '각성 도플라밍고 h04P uabi A0AJ(AHad, Had1 −60, aare 850, abuf B01E).', 850, [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-60.0, buffId='B01E')]),
            ('A0Q7', 'A0Q7 자장가 프람 — 맵 전체 적 이속 −5% 오라', '브룩 h04T uabi A0Q7(AOae, Oae1 −0.05, aare 99999, abuf B03X).', 99999, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.95)]),
            ('A0SR', 'A0SR 오바드 꾸 드로와 — 적 이속 −15% 오라', '브룩 h04T uabi A0SR(AOae, Oae1 −0.15, aare 875, abuf B04D).', 875, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.85)]),
            ('A0I1', 'A0I1 카리스마 — 자기 공속 +20%', '샹크스 h04U uabi A0I1(AOae, Oae2 +0.2, atar invulnerable,self = 자기만, abuf B02L). 반경 빈칸 = 스톡 900.', STOCK_AURA_RADIUS,
             [sat.effect(kind=SPEED, target=SELF, multiplier=0.2, buffId='B02L')]),
            ('A0TH', 'A0TH 우솝갓 — 적 방어 −22 오라', '우솝 h04W·h04X uabi A0TH(AHad, Had1 −22, aare 1085, abuf B04L).', 1085, [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-22.0, buffId='B04L')]),
            ('A0PH', 'A0PH 우솝의 허풍 — 맵 전체 적 방어 −8 오라', '우솝 uabi A0PH(AHad, Had1 −8, aare 99999, abuf B03V).', 99999, [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-8.0, buffId='B03V')]),
            ('A0DT', 'A0DT 흉포한 광기 — 적 이속 −70% 오라', '빅맘 h09J uabi A0DT(AOae, Oae1 −0.7, aare 888, abuf B01B).', 888, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.3)]),
            ('A0QE', 'A0QE 상초강 공속 — 자기 공속 +50%(상시)', '빅맘 h09J uabi A0QE(AIsx, Isx1 +0.5 — 아이템 공속 능력이 유닛에 붙어 상시). 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=SPEED, target=SELF, multiplier=0.5, buffId='A0QE')]),
            ('A10D', 'A10D 자석자석 열매 — 적 이속 −33% 오라', '키드 h0AG uabi A10D(AOae, Oae1 −0.33, aare 1250, abuf B02O).', 1250, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.67)]),
            ('A0SB', 'A0SB 덩쿨 뿌리 — 적 이속 −12% 오라', 'h04Z uabi A0SB(AOae, Oae1 −0.12, aare 500, abuf B077).', 500, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.88)]),
            ('A102', 'A102 자석자석열매 — 자기 평타 +50000', '키드 h0AG uabi A102(AIfb 오브, Idam 50000 — 오뎅 A0YC와 같은 꼴). 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=FLAT, target=SELF, multiplier=50000.0, buffId='A102')])]:
        aura(R, tail, name, '원작 ' + desc + ' 이감은 겹치면 가장 강한 하나만(EnemyDummy), 방깎 오라는 버프 ID별 최댓값의 합.', radius, effects)

    # ════ 키자루 H0B5 → 초월_구주호_AD ════
    def kizaru(a):
        sat.set_effects(a, 0, [retarget(b, hitCount=16, duration=2.56) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_회수_초월_구주호_AD_c453ba09', '팔척경곡옥은 16발(integerB<16, 0.16초 간격) — hitCount 3 → 16(5.3배 과소였다). ⚠️ 원문 게이트 「1/17 그리고 마나>75(발동 시 −50)」의 마나 조건은 미반영 — '
         '이 로스터의 마나 게이지는 초록소(H0BL 115·가득 시작)와 같이 쓴다. 빈도가 원문(평균 ≈1/38타)보다 잦다. ', kizaru)
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Kizaru_빛의기둥', '키자루 — 빛의 기둥 1/12(500 범위 400000·스턴 2.75초)',
        '원작 키자루 H0B5 Kizaru_Attack GetRandomInt 1/12: 시전자 주인 더미 e0HD가 대상 자리에서 stomp A0BW(500 범위 400000, adur 2.75 · ahdu 1.5).',
        ON_HIT, sat.level_block(triggerChance=1.0 / 12.0, range=500.0, effects=[dmg(ENEMIES, AP, SPELLS, 400000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.75)]))
    new('초월_구주호_AD', 'SkillData_원작트리거_초월_구주호_AD_Kizaru_01', '키자루 — Kizaru_01 빛의 발차기 1/12(450 범위 350000 ×3 + 대상 ×3.25)',
        '원작 키자루 H0B5 Kizaru_01(1/12 독립): D = 350000 + AGI×3500, 짝수 박 3회 — 대상 중심 450 D NORMAL/UNIVERSAL + 대상 D×3.25. AGI 항은 미반영(하한).',
        ON_HIT, sat.level_block(triggerChance=1.0 / 12.0, range=450.0, effects=[
            dmg(ENEMIES, AP, SPELLS, 350000.0, hitCount=3, duration=0.54), dmg(SINGLE, AP, SPELLS, 1137500.0, hitCount=3, duration=0.54)]))
    new('초월_구주호_AD', 'SkillData_원작능력_초월_구주호_AD_A0CS', 'A0CS !빛빛열매 — 15% ×8.88 · 스턴 3.0초',
        '원작 키자루 H0B5 uabi A0CS(ACbh, Hbh2 8.88, Hbh1 빈칸 = 스톡 15%, adur 3.0 · ahdu 1.5).', ON_HIT,
        sat.level_block(triggerChance=STOCK_ACBH_HBH1, effects=[sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=HERO, multiplier=8.88),
                                                                sat.effect(kind=STUN, target=SINGLE, duration=3.0)]))

    # ════ 제한됨 ════
    edit('SkillData_원작능력_제한_강보명_A0GK', 'X1: A0GK atar ancient — PV 200 대상만(일반 몹에 매 타 +850000·스턴 1.5초 = 영구 스턴이었다). ', lambda a: cond(a, PV_EQ))
    edit('SkillData_원작능력_제한_김민규', 'X2: A0QQ atar sapper — PV≥200 대상만(일반 몹에 매 타 ×6.25였다). ', lambda a: cond(a, PV_GE))
    edit('SkillData_게이트_제한_박성호_874ea782', 'U1: Marco_S1은 1/10 독립 if — 1/7×1/10(0.0143) → 0.1(7배 과소였다). ', gate({}, chance=0.1))
    edit('SkillData_게이트_제한_김민규_136d09f9', 'U2: Kata_03은 B045 동안 매 타(1/7 아님 — 별도 최상위 if) — 확률 1/7 → 1.0(7배 과소였다). ', gate({}, chance=1.0))

    def kata_buff(a):
        sat.set_effects(a, 0, [sat.effect(kind=SPEED, target=SELF, multiplier=4.0, duration=1.25, buffId='B045')])
    edit('SkillData_버프게이트_제한_김민규_B045', 'U3: B045 = A0R7(Ablo, Blo1 공속 +400%, adur 1.25초) — 「4타 충전」 ApplyBuff → 공속 +400% 1.25초 버프(B045)로. Kata_03이 이 버프 동안 매 타. ', kata_buff)

    def ain(n, second):
        def fn(a):
            blocks = sat.get_effect_blocks(a, 0)
            area = [b for b in blocks if re.search(r'^\s*target: 2$', b, re.M)][0]
            area = re.sub(r'^\s*requiredTargetBuffId:.*\n', '      requiredTargetBuffId:\n', area, flags=re.M)
            sat.set_effects(a, 0, [area,
                                   dmg(SINGLE, AP, SPELLS, 3500000.0, requiredTargetBuffId='B06B'),
                                   dmg(SINGLE, AP, MAGIC, 350000.0, forbiddenTargetBuffId='B06B'),
                                   dmg(SINGLE, AP, MAGIC, 350000.0, hitCount=second, duration=0.13 * second)])
        return fn
    edit('SkillData_게이트_제한_이충민_18aa2343', 'X3 Ain_Skill_1: 대상 첫 타는 B06B 보유면 3500000 · 아니면 350000(둘 중 하나), 둘째 타 350000 — 3500000 무조건 + 350000 ×4였다(B06B 없는 대상 7배 과다). 범위 320000 ×2는 한 벌로. ', ain(2, 1))
    edit('SkillData_게이트_제한_이충민_8f2b5872', 'X4 Ain_Skill_2: 범위 240000 ×3 한 벌 · 대상 첫 타는 B06B면 3500000 · 아니면 350000, 둘째·셋째 350000 — 범위 두 벌·대상 3500000 무조건 + 350000 ×9였다. ', ain(3, 2))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
