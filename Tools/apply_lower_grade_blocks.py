"""히든·전설적인·희귀함·특별함 큰 어긋남 정정 + 빠진 상시 오라(2026-09-30 구현담당1 —
Docs/research/HIDDEN_FILL_LIST.md · LEGEND_FILL_LIST.md §0-1·3·5·6·7·8 + §1 · RARE_FILL_LIST.md §0-1·2·4 + §1 · SPECIAL_FILL_LIST.md §0-1·2·3).

넣는 행마다 war3map_new.j의 게이트 줄·RRD 줄과 w3a 능력 필드를 다시 열어 봤다. 다시 돌려도 같은 결과. 규칙은 apply_immortal_blocks.py·apply_transcend_limited_blocks.py와 같다.
  - 「출처 없는 스턴」·「주인 다름」·히루루크 광역 50%는 건드리지 않는다(사장님 보고 대상).
  - 원문에 없는 값(장풍 능력의 Ucs3 거리 필드를 피해로 읽은 서민성 1050 · 최동준 925)은 디코드 오류라 뺀다.
  - PV 조건이 붙은 오라(흰수염 A0EI·A0EH)·크리(AIcs)·주기 피해 오라(ANpi)·독(Aspo)·평타 다중 대상(Aroc)·시전 능력은 안 넣었다.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg, STOCK_AOWS_ADUR  # noqa: E402

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, ARMOR_BREAK, ARMOR_BONUS, AISR, FLAT, SPEED, SLOW, PERCENT = 0, 1, 2, 4, 9, 11, 12, 13, 14
AD, AP = 1, 2
NORMAL_AT, SIEGE, HERO, CHAOS, SPELLS = 1, 3, 4, 5, 7
MAXHP, CURHP, ATK, RECEIVED = 1, 2, 3, 5
PV_LT, PV_EQ, PV_GE, PV_NE = 1, 2, 3, 4
ON_HIT, AURA = 0, 2
STOCK_ACBH_HBH1 = 0.15
STOCK_AURA_RADIUS = 900
TAG = '[하위 등급 블록 09-30]'


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
            sat.new_asset(stem, name, desc + ' 2026-09-30 구현담당1.', trigger, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_lower_grade_blocks.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def aura(roster, tail, name, desc, radius, effects):
        new(roster, 'SkillData_원작오라_%s_%s' % (roster, tail), name, '원작 ' + desc, AURA, sat.level_block(range=float(radius), effects=effects))

    def bash(roster, ab, name, desc, chance, mult=0.0, bonus=0.0, stun=0.0):
        effects = [sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=NORMAL_AT, multiplier=mult, bonus=bonus)] if (mult or bonus) else []
        if stun:
            effects.append(sat.effect(kind=STUN, target=SINGLE, duration=stun))
        new(roster, 'SkillData_원작능력_%s_%s' % (roster, ab), name, '원작 ' + desc, ON_HIT, sat.level_block(triggerChance=chance, effects=effects))

    def blocks_of(a):
        return sat.get_effect_blocks(a, 0)

    # ════ 히든 ════
    # 데켄 h03R → 히든_최윤서
    def deken1(a):
        sat.set_level_field(a, 0, 'range', 400.0)
        sat.set_effects(a, 0, blocks_of(a) + [dmg(ENEMIES, AP, SPELLS, 180000.0)])
    edit('SkillData_게이트_히든_최윤서_b8d2fd85', 'deken_skill_1: 대상 중심 400 범위 180000 NORMAL/UNIVERSAL(UnitDamagePointLoc — 대상 포함) 추가. 대상이 300000을 받아야 하는데 120000이었다. ', deken1)
    edit('SkillData_게이트_히든_최윤서_3c5f8bd9', 'deken_Mana: 대상 thunderbolt A0BF 스턴 2.0초 + stomp A0BG 800 범위 스턴(adur 빈칸 = 스톡 3.0 · ahdu 3.0) 추가. ',
         lambda a: sat.set_effects(a, 0, blocks_of(a) + [sat.effect(kind=STUN, target=SINGLE, duration=2.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    bash('히든_최윤서', 'A06N', 'A06N !어인 — 20% +150000 · 스턴 0.4초', '데켄 h03R uabi A06N(ACbh, Hbh1 20, Hbh3 150000, adur 0.4).', 0.2, bonus=150000.0, stun=0.4)

    # 코알라 h03V → 히든_황정기
    def koalla(a):
        b = blocks_of(a)
        assert len(b) == 3, len(b)
        sat.set_effects(a, 0, [sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=11.0), retarget(b[0], hitCount=1),
                               retarget(b[1], hitCount=5, duration=0.55), b[2],
                               dmg(ENEMIES, AP, SPELLS, 450000.0, hitCount=2, duration=0.6), sat.effect(kind=STUN, target=ENEMIES, duration=2.5)])
    edit('SkillData_게이트_히든_황정기_2a646778', 'koalla_skill_Mana: ① 1회(2회였다) · ② 5회(integerB, 0.11초 간격 — 2회였다) · 범위 적 AId1 +11 · stomp A0CE 450000·스턴 2.5초가 ①·③에서 한 번씩'
         '(원문은 시전자 앞 725 지점 650 범위 — 이 에셋 범위 625로 근사). ', koalla)
    bash('히든_황정기', 'A0II', 'A0II !혁명군대리사범 — 40% 스턴 0.5초', '코알라 h03V uabi A0II(ACbh, Hbh1 40, 피해 0, adur 0.5).', 0.4, stun=0.5)
    # 아카이누(히든) h03Z → 히든_호치킨
    edit('SkillData_게이트_히든_호치킨_21297197', 'Akainu_02_hidden: 대상 파이어볼트 A0VL 스턴 3.0초 추가(PV 200 대상일 때 — 피해 효과와 같은 조건). 「PV 200을 칠 때만 쿨 소모」는 미반영. ',
         lambda a: sat.set_effects(a, 0, blocks_of(a) + [sat.effect(kind=STUN, target=SINGLE, duration=3.0, targetCondition=PV_EQ, targetConditionValue=200.0)]))

    # ════ 전설적인 ════
    # §0-1 로우 h03C → 전설적인_신지우: Legend16sc — 1/22 분해(525) · 1/8 인젝션 샷(400) + 대상 B06B면 10000 + 현재체력 15%
    def law(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.125)
        sat.set_level_field(a, 0, 'range', 400.0)
        single = [retarget(b, requiredTargetBuffId='B06B', damageType=AP, attackType=SPELLS) for b in blocks_of(a)]
        sat.set_effects(a, 0, single + [
            sat.effect(kind=DAMAGE, basis=CURHP, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=0.0035, bonus=182500.0, targetCondition=PV_LT, targetConditionValue=200.0, skipDamageTakenMultiplier=1),
            sat.effect(kind=DAMAGE, basis=CURHP, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=0.00175, bonus=91250.0, targetCondition=PV_EQ, targetConditionValue=200.0, skipDamageTakenMultiplier=1)])
        sat.set_head_field(a, 'skillName', '로우(전설) — 인젝션 샷 1/8(400 범위 + 대상 B06B면 현재체력 15% + 10000)')
    edit('SkillData_원작능력_전설적인_신지우', '§0-1 Legend16sc 인젝션 샷: 「20% · 조건 없음」 → 1/8 · 현재체력 15% + 10000은 **대상이 B06B일 때만**(모든 적에게 상시 %피해였다). '
         '같은 블록의 400 범위 (182500 + 현재체력 0.35%)×(100/PV) CHAOS/UNIVERSAL 추가 — PV<200은 ×1, PV 200은 ×0.5로(PV 300대는 미반영, 원문은 주 대상 체력 기준 — 맞는 적마다 제 체력으로 근사). ', law)
    new('전설적인_신지우', 'SkillData_원작트리거_전설적인_신지우_Legend16sc_분해', '로우(전설) — 분해 1/22(525 범위 275000 + 현재체력 1%)',
        '원작 로우 h03C Legend16sc GetRandomInt(1,22)==7: 525 범위 (275000 + 주 대상 현재체력 1%)×(100/각 적 PV) CHAOS/UNIVERSAL — PV<200은 ×1, PV 200은 ×0.5로(PV 300대 미반영, 체력은 맞는 적마다 제 것으로 근사).',
        ON_HIT, sat.level_block(triggerChance=1.0 / 22.0, range=525.0, effects=[
            sat.effect(kind=DAMAGE, basis=CURHP, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=0.01, bonus=275000.0, targetCondition=PV_LT, targetConditionValue=200.0, skipDamageTakenMultiplier=1),
            sat.effect(kind=DAMAGE, basis=CURHP, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=0.005, bonus=137500.0, targetCondition=PV_EQ, targetConditionValue=200.0, skipDamageTakenMultiplier=1)]))

    # §0-3 핸콕 h032 → 전설적인_이현주: Legend14 — B00D 있으면 kiss_damagesc, **없으면** 1/10 Legend_han_1
    def han1(a):
        sat.set_level_field(a, 0, 'requiredBuffId', '')
        sat.set_level_field(a, 0, 'forbiddenBuffId', 'B00D')
        sat.set_head_field(a, 'skillName', '핸콕(전설) — Legend_han_1(B00D 없을 때 1/10)')
    edit('SkillData_게이트_전설적인_이현주_0e5cf375', '§0-3: Legend_han_1은 B00D **없을 때** 1/10 — requiredBuffId B00D → forbiddenBuffId B00D(게이트가 반대였다, ≈7배 과소). ', han1)
    new('전설적인_이현주', 'SkillData_원작트리거_전설적인_이현주_Legend_han_kiss_damagesc', '핸콕(전설) — 키스 피해(B00D 동안 매 타: 400 범위 평타 피해×1.25 + 80000)',
        '원작 핸콕 h032 Legend14: B00D 동안 매 타 Legend_han_kiss_damagesc — 400 범위 (K×1.25 + 80000) CHAOS/UNIVERSAL + PV<200: 최대체력 8.5%×A11S계수 / 그 밖 150000.',
        ON_HIT, sat.level_block(range=400.0, requiredBuffId='B00D', effects=[
            sat.effect(kind=DAMAGE, basis=RECEIVED, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=1.25, bonus=80000.0),
            sat.effect(kind=DAMAGE, basis=MAXHP, target=SINGLE, damageType=AP, attackType=CHAOS, multiplier=0.085, targetCondition=PV_LT, targetConditionValue=200.0),
            dmg(SINGLE, AP, SPELLS, 150000.0, targetCondition=PV_GE, targetConditionValue=200.0)]))

    def a05s(a):
        sat.set_level_field(a, 0, 'triggerChance', STOCK_ACBH_HBH1)
        sat.set_effects(a, 0, [retarget(b, duration=0.8) if re.search(r'^\s*-? ?kind: 1$', b, re.M) else b for b in blocks_of(a)])
        sat.set_head_field(a, 'skillName', 'A05S !매료매료 열매 — 15% +150000 · 스턴 0.8초')
    edit('SkillData_원작능력_전설적인_이현주', 'A05S(ACbh, Hbh1 빈칸 = 스톡 15%, Hbh3 150000, adur 0.8): 확률 0.2 → 0.15 · 스턴 0.3 → 0.8초. ', a05s)

    # §0-5 레이쥬 h033 → 전설적인_임건웅: 같은 효과가 두 벌·세 벌
    def dedupe(keep_from_dups=1):
        def fn(a):
            seen, out = {}, []
            for b in blocks_of(a):
                seen[b] = seen.get(b, 0) + 1
                if seen[b] <= keep_from_dups:
                    out.append(b)
            sat.set_effects(a, 0, out)
        return fn
    edit('SkillData_게이트_전설적인_임건웅_9ffa9c45', '§0-5 핑크 호넷: 현재체력 7% ×2가 두 벌(=4회) → 한 벌(원문 2회). ', dedupe())
    edit('SkillData_게이트_전설적인_임건웅_87ed0ff5', '§0-5 거품광선: 현재체력 5% ×3이 세 벌(=9회) → 한 벌(원문 3회). ', dedupe())
    edit('SkillData_게이트_전설적인_임장혁_3a13fd3d', '§0-7 Legend31_toki: 415 범위 125000 ×2가 두 벌(=4회) → 한 벌(원문 2회). ', dedupe())

    # §0-6 티치 h02U → 전설적인_홍인창: 크로우즈 1/7
    def crows(a):
        b = blocks_of(a)
        keep = [x for x in b if re.search(r'multiplier: 4000000', x)]
        assert len(keep) == 1
        sat.set_effects(a, 0, [sat.effect(kind=AISR, target=SINGLE, multiplier=8.0)] + keep + [
            dmg(SINGLE, AP, SPELLS, 300000.0, targetCondition=PV_GE, targetConditionValue=200.0),
            dmg(SINGLE, AP, SPELLS, 300000.0, forbiddenTargetBuffId='B06B', targetCondition=PV_LT, targetConditionValue=200.0)])
    edit('SkillData_게이트_전설적인_홍인창_fbb3c4ff', '§0-6 크로우즈: PV≥200 300000 ×1(×4였다) · PV<200은 B06B면 4000000, 아니면 300000 ×1(×2였다) · 대상 AIsr +8 추가. A11Q(받는 피해 +10% 300초)·퍼지는 미반영. ', crows)

    # §0-8 루피 기어2 h02X → 전설적인_박성호: Legend0 45% 개틀링은 375 범위 · A052는 따로 15%
    def gear2(a):
        b = blocks_of(a)
        gat = [x for x in b if re.search(r'multiplier: 47500', x)]
        assert len(gat) == 1 and len(b) == 2
        sat.set_level_field(a, 0, 'range', 375.0)
        sat.set_effects(a, 0, [retarget(gat[0], target=ENEMIES)])
        sat.set_head_field(a, 'skillName', 'Legend0 개틀링 — 45%(375 범위 47500×0.5~2)')
    edit('SkillData_원작능력_전설적인_박성호', '§0-8 Legend0: 개틀링은 대상 중심 375 범위(단일이었다). 같이 들어 있던 A052(×4.25 + 75000)는 별도 강타(15%)라 에셋을 뗐다 — 45%로 나가 3배 과다였다. ', gear2)
    bash('전설적인_박성호', 'A052', 'A052 !고무고무 열매 — 15% ×4.25 + 75000 · 스턴 0.7초', '루피 기어2 h02X uabi A052(ACbh, Hbh1 빈칸 = 스톡 15%, Hbh2 4.25, Hbh3 75000, adur 0.7).',
         STOCK_ACBH_HBH1, mult=4.25, bonus=75000.0, stun=0.7)

    # 전설 §1 상시 오라(uabi · j 참조 0)
    def slow(v, buff=''):
        return [sat.effect(kind=SLOW, target=ENEMIES, multiplier=round(1.0 + v, 4))]

    def armor(v, buff):
        return [sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=float(v), buffId=buff)]

    def both(kind, v, buff):
        return [sat.effect(kind=kind, target=t, multiplier=v, buffId=buff) for t in (SELF, ALLIES)]
    for roster, tail, name, desc, radius, effects in [
            ('전설적인_정윤식', 'A143', 'A143 그림자그림자열매 — 적 이속 −30% 오라', '모리아 h02N uabi A143(AOae, Oae1 −0.3, aare 850, abuf B06X).', 850, slow(-0.3)),
            ('전설적인_임장혁', 'A0DZ', 'A0DZ 아지랑이 — 적 방어 −33 오라', '에이스 h02O uabi A0DZ(AHad, Had1 −33, aare 850, abuf B01Y).', 850, armor(-33, 'B01Y')),
            ('전설적인_이승우', 'A0F8', 'A0F8 우정의 고래 — 아군 공속 +17% 오라', '라분 h02Q uabi A0F8(AOae, Oae2 +0.17, aare 800, atar …self…friend, abuf B01X).', 800, both(SPEED, 0.17, 'B01X')),
            ('전설적인_임채민', 'A0PW', 'A0PW 풍압 — 적 이속 −30% 오라', '마르코 h02T uabi A0PW(AOae, Oae1 −0.3, aare 875, abuf B03U).', 875, slow(-0.3)),
            ('전설적인_정준영', 'A0DC', 'A0DC 연기연기열매 — 적 이속 −50% 오라', '스모커 h02V uabi A0DC(AOae, Oae1 −0.5, aare 800, abuf B01A).', 800, slow(-0.5)),
            ('전설적인_박성호', 'A0TT', 'A0TT 개틀링 공속증가 — 자기 공속 +35%(상시)', '루피 기어2 h02X uabi A0TT(AIsx, Isx1 +0.35). 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=SPEED, target=SELF, multiplier=0.35, buffId='A0TT')]),
            ('전설적인_박민수', 'A16J', 'A16J 살벌함 — 적 방어 −12 오라', '바르토로메오 h02Z uabi A16J(AHad, Had1 −12, aare 850, abuf B07Q).', 850, armor(-12, 'B07Q')),
            ('전설적인_임채현', 'A0FX', 'A0FX 중력중력열매 — 적 이속 −24% 오라', '후지토라 h031 uabi A0FX(AOae, Oae1 −0.24, aare 850, abuf B022).', 850, slow(-0.24)),
            ('전설적인_임건웅', 'A0SW', 'A0SW 독기 — 적 이속 −35% 오라', '레이쥬 h033 uabi A0SW(AOae, Oae1 −0.35, aare 850, abuf B04G).', 850, slow(-0.35)),
            ('전설적인_진연서', 'A0ER', 'A0ER 명왕의 패기 — 적 방어 −15 오라', '레일리 h03A uabi A0ER(AHad, Had1 −15, aare 빈칸 = 스톡 900, abuf B01S).', STOCK_AURA_RADIUS, armor(-15, 'B01S')),
            ('전설적인_진연서', 'A03O', 'A03O 패기수련 — 아군 공속 +20% 오라', '레일리 h03A uabi A03O(AOae, Oae2 +0.2, aare 800, atar …self…friend, abuf B001).', 800, both(SPEED, 0.2, 'B001')),
            ('전설적인_박민석', 'A0YO', 'A0YO 샨디아의 대전사 — 적 방어 −15 오라', '카르가라 h03F uabi A0YO(AHad, Had1 −15, aare 825, abuf B05J).', 825, armor(-15, 'B05J')),
            ('전설적인_양문호', 'A0PJ', 'A0PJ 3장성 크래커 — 적 방어 −25 오라', '크래커 h03H uabi A0PJ(AHad, Had1 −25, aare 750, abuf B03R).', 750, armor(-25, 'B03R')),
            ('전설적인_이재윤', 'A0VG', 'A0VG 흩날리는 겨울의 벚꽃 — 아군 공격력 +55% 오라', '히루루크 h03K uabi A0VG(ACac, Cac1 +0.55, aare 850, atar …self…friend, abuf B02Q).', 850, both(PERCENT, 0.55, 'B02Q')),
            ('전설적인_임채현', 'A0FB', 'A0FB 공포 — 적 방어 −27 오라', '울티 h03S uabi A0FB(AHad, Had1 −27, aare 빈칸 = 스톡 900, abuf B01N).', STOCK_AURA_RADIUS, armor(-27, 'B01N')),
            ('전설적인_임장혁', 'A0UF', 'A0UF 영주 대리 — 아군 공속 +20% 오라(자기 제외)', '토키 h087 uabi A0UF(AOae, Oae2 +0.2, aare 800, atar에 self 없음, abuf B04N).', 800,
             [sat.effect(kind=SPEED, target=ALLIES, multiplier=0.2, buffId='B04N')]),
            ('전설적인_진연서', 'A0Y1', 'A0Y1 수호자 나리 — 아군 공격력 +25% 오라', '네코마무시 h09Z uabi A0Y1(ACac, Cac1 +0.25, aare 825, atar …self…friend, abuf B05F).', 825, both(PERCENT, 0.25, 'B05F')),
            ('전설적인_진연서', 'A0YT', 'A0YT 네코마무시 평타 — 자기 평타 +40000', '네코마무시 h09Z uabi A0YT(AIfb 오브, Idam 40000). 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=FLAT, target=SELF, multiplier=40000.0, buffId='A0YT')]),
            # 희귀함 §1
            ('희귀함_노수신', 'A0N4', 'A0N4 콧노래 — 아군 공속 +10% 오라', '브룩 h01N uabi A0N4(AOae, Oae2 +0.1, aare 800, atar …self…friend, abuf B039).', 800, both(SPEED, 0.1, 'B039')),
            ('희귀함_윤현모', 'A0D4', 'A0D4 빙빙열매 — 적 이속 −12% 오라', '쿠잔 h02B uabi A0D4(AOae, Oae1 −0.12, aare 825, abuf B019).', 825, slow(-0.12)),
            ('희귀함_두유찬', 'A0K5', 'A0K5 자석자석 열매 — 적 이속 −20% 오라', '키드 h02D uabi A0K5(AOae, Oae1 −0.2, aare 800, abuf B02O).', 800, slow(-0.2)),
            ('희귀함_두유찬', 'A0VM', 'A0VM 자석자석열매 — 자기 평타 +2500', '키드 h02D uabi A0VM(AIfb 오브, Idam 2500). 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=FLAT, target=SELF, multiplier=2500.0, buffId='A0VM')]),
            ('희귀함_이태훈', 'A0CH', 'A0CH 모래모래 열매 — 적 이속 −20% 오라', '크로커다일 h02H uabi A0CH(AOae, Oae1 −0.2, aare 800, abuf B014).', 800, slow(-0.2)),
            ('희귀함_조현규', 'A0GN', 'A0GN 애교 — 아군 공격력 +30% 오라', '쵸파 h02K uabi A0GN(ACac, Cac1 +0.3, aare 850, atar …self…friend, abuf B02A).', 850, both(PERCENT, 0.3, 'B02A')),
            ('희귀함_조현규', 'A17H', 'A17H 공속감소-혼포인트 — 자기 공속 −50%(손해, 상시)', '쵸파 h02K uabi A17H(AIsx, Isx1 −0.5). 손해 효과 — 원작대로. 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=SPEED, target=SELF, multiplier=-0.5, buffId='A17H')]),
            ('희귀함_서민성', 'A0TX', 'A0TX 주단 공속증가 — 자기 공속 +33%(상시)', '카쿠 h023 uabi A0TX(AIsx, Isx1 +0.33). 버프 ID가 없어 능력 코드를 키로.', 0,
             [sat.effect(kind=SPEED, target=SELF, multiplier=0.33, buffId='A0TX')])]:
        aura(roster, tail, name, desc, radius, effects)
    # 카르가라 A0VW(AIcb): 평타 맞은 PV 200 적 방어 −40 · 7초
    new('전설적인_박민석', 'SkillData_원작능력_전설적인_박민석_A0VW', 'A0VW 샨디아의 기운 방깎 — 평타 맞은 PV 200 적 방어 −40 · 7초',
        '원작 카르가라 h03F uabi A0VW(AIcb, Iarp 40, adur 7, atar ancient = PV 200). 같은 능력은 겹치지 않고 갱신.', ON_HIT,
        sat.level_block(effects=[sat.effect(kind=ARMOR_BONUS, target=SINGLE, multiplier=-40.0, duration=7.0, buffId='A0VW', targetCondition=PV_EQ, targetConditionValue=200.0)]))

    # ════ 희귀함 ════
    def kaku(a):
        keep = [b for b in blocks_of(a) if re.search(r'multiplier: 8000', b)]
        assert len(keep) == 1
        sat.set_level_field(a, 0, 'triggerChance', round(1.0 / 7.0, 6))
        sat.set_effects(a, 0, keep)
        sat.set_head_field(a, 'skillName', 'Unique14 1/7 — 장풍 A097 8000')
    edit('SkillData_원작능력_희귀함_서민성', '§0-1: 확률 1.0에 셋이 다 들어 매 타 14050이었다(7.4배 과다). 장풍 8000은 1/7(GetRandomInt(1,7)==4) · A047 +5000은 15%(별도 에셋) · 1050은 원문에 없는 값(A097의 Ucs3 거리 필드)이라 뺌. ', kaku)
    bash('희귀함_서민성', 'A047', 'A047 !육식 — 15% +5000', '카쿠 h023 uabi A047(ACbh, Hbh1 빈칸 = 스톡 15%, Hbh3 5000, 스턴 0).', STOCK_ACBH_HBH1, bonus=5000.0)

    def momonga(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.125)
        sat.set_level_field(a, 0, 'range', 375.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 7000.0), dmg(SINGLE, AP, SPELLS, 10000.0, randMax=10.0), sat.effect(kind=STUN, target=SINGLE, duration=1.5)])
        sat.set_head_field(a, 'skillName', 'Unique35 1/8 — 375 범위 7000 + 대상 10000~100000 · 스턴 1.5초')
    edit('SkillData_원작능력_희귀함_엄태웅', '§0-2: 확률 1.0 · 단일 7000(8배 과다) → 1/8: 375 범위 7000 NORMAL/UNIVERSAL + 대상 GetRandomInt(10000,100000) + thunderbolt 스턴 1.5초. ', momonga)

    def teach(a):
        sat.set_effects(a, 0, [sat.effect(kind=AISR, target=SINGLE, multiplier=3.0), dmg(SINGLE, AP, SPELLS, 15000.0)])
    edit('SkillData_게이트_희귀함_황준석_d1ddeea9', '§0-4 크로우즈 1/8: 대상 15000 ×1 + AIsr +3 — 15000 ×2가 두 벌(=4회)이었다(4배 과다). ', teach)

    # ════ 특별함 ════
    edit('SkillData_원작능력_특별함_최동준', '§0-1: 925는 원문에 없는 값(장풍 A0BI의 Ucs3 거리 필드를 피해로 읽은 것) — 매 타 925를 뺌(효과 0). 진짜 블록(1/7 장풍 850)은 더미채널 에셋에 있다. ',
         lambda a: sat.set_effects(a, 0, []))

    def a053(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.2)
        sat.set_effects(a, 0, blocks_of(a) + [sat.effect(kind=STUN, target=SINGLE, duration=0.25)])
        sat.set_head_field(a, 'skillName', 'A053 !고무고무 열매 — 20% +30 · 스턴 0.25초')
    edit('SkillData_원작능력_특별함_조도연', '§0-2 A053(ACbh, Hbh1 20, Hbh3 30, adur 0.25): 확률 0.6 → 0.2(0.6은 이나즈마 A0HZ 값) · 스턴 0.25초 추가. ', a053)
    new('특별함_조도연', 'SkillData_원작트리거_특별함_조도연_기어세컨드', '루피 기어 세컨드 — 1/8 & B00V 없음: 자기 공속 +360% 1.75초',
        '원작 루피 h00U 평타 트리거: GetRandomInt 1/8 & B00V 없음 → A0A0(Ablo, Blo1 +3.6 공속, adur 1.75, abuf B00V)를 자기에게.', ON_HIT,
        sat.level_block(triggerChance=0.125, forbiddenBuffId='B00V', effects=[sat.effect(kind=SPEED, target=SELF, multiplier=3.6, duration=1.75, buffId='B00V')]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
