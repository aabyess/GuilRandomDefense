"""불멸의 등급 평타 트리거 블록 채우기·정정(2026-09-30 구현담당1 — Docs/research/IMMORTAL_FILL_LIST.md §2·§6, Blender §V 정정 반영).

행마다 war3map_new.j 트리거 본문의 RRD/UnitDamage 줄·게이트와 더미 능력 필드(w3a)를 다시 열어 봤다. 다시 돌려도 같은 결과.
공통 규칙(기존 관례):
  - ATTACK_TYPE_NORMAL/DAMAGE_TYPE_UNIVERSAL → AP·Spells, CHAOS/NORMAL → AD·Chaos, HERO/NORMAL → AD·Hero, MAGIC/UNIVERSAL → AP·Magic.
  - 더미 stomp·천둥박수·파이어볼트 피해는 AP·Spells(apply_eternal_blocks의 핸콕 stomp와 같게).
  - 스턴은 adur(일반 지속). ⚠️ adur 빈칸인 stomp(AOws) = 스톡 3.0으로 본다(STOCK_AOWS_ADUR) — 맵 안 근거 둘:
    ① AOws 파생 58개의 adur 명시값(0.6~3.5)에 2.75·2.85·3.5는 있는데 3.0만 0건(에디터는 기본값과 같은 값을 저장하지 않는다)
    ② 제작자는 ahdu를 adur의 15% 또는 55%로 맞춰 적는다(2.5 → 0.37 여섯 · 2.75 → 0.41 셋 · 2.75 → 1.5 · 2.0 → 1.1). adur 빈칸 stomp의
       ahdu 0.45 = 3.0 × 0.15, 1.65 = 3.0 × 0.55. slk 미확인. (ahdu는 영웅·저항 피부(ACrk) 적에게 걸리는 지속 — 우리에 그 축은 없다.)
  - 한 레벨에 반경·중심이 하나라, 반경이나 중심이 다른 묶음은 같은 게이지의 에셋을 따로 만든다(같은 타에 같이 터진다).
  - ⚠️ 진짜 체력 게이지 에셋의 skillName에 「Mana」를 넣지 말 것 — sync_mana_regen_from_w3u가 「MANA가 든 Life 게이지 = 마나를 Life 카운터로 센 것」으로 읽는다.
  - 레벨 2(T특성강화) 갈래·연구·아이템 갈래·소환체·형태 전환은 안 넣는다(목록 §3·§4에 그대로).
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

SELF, ENEMIES, SINGLE = 0, 2, 3
DAMAGE, STUN, ARMOR_BREAK, ARMOR_BONUS, AISR, SPEED, SLOW = 0, 1, 2, 4, 9, 12, 13
AD, AP = 1, 2
HERO, CHAOS, MAGIC, SPELLS = 4, 5, 6, 7
MAXHP, CURHP, RECEIVED = 1, 2, 5
PV_LT, PV_EQ, PV_GE, PV_NE = 1, 2, 3, 4
CASTER = 1
ON_HIT, GAUGE = 0, 3
MANA, LIFE = 0, 1
STOCK_AOWS_ADUR = 3.0   # ⚠️ slk 미확인, 머리 주석
TAG = '[불멸 블록 09-30]'


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


def retarget(block, **fields):
    for k, v in fields.items():
        s = v if isinstance(v, str) else sat.num(v)
        if re.search(r'^\s*-? ?%s:' % k, block, re.M):
            block = re.sub(r'^(\s*-? ?%s:).*$' % k, lambda m: m.group(1) + ' ' + s, block, count=1, flags=re.M)
        else:
            block = block.rstrip('\n') + '\n      %s: %s\n' % (k, s)
    return block


def dmg(target, dt, at, amount, **kw):
    return sat.effect(kind=DAMAGE, target=target, damageType=dt, attackType=at, multiplier=float(amount), **kw)


def main():
    changed = []

    def new(roster, stem, name, desc, trigger, level):
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc + ' 2026-09-30 구현담당1(IMMORTAL_FILL_LIST §2).', trigger, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_immortal_blocks.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    # ── 레일리 h049 → 불멸_정윤식 ──
    # B1 LaillySkill3(마나 115): 대상 중심 750 — 2750000×1~1.5 CHAOS/NORMAL + 범위 적 AId1 +5 + 1/3로 대상(B06B 없음·PV<200) 즉사.
    #    옛 효과 「ArmorBreak 20 단일」은 오라 A0ES(−20)를 스킬 자리에 넣은 것 — 오라는 apply_immortal_auras가 따로 넣는다.
    def b1(a):
        for i, amount in enumerate([2750000.0, 3250000.0]):
            if i >= len(a.levels):
                break
            sat.set_level_field(a, i, 'range', 750.0)
            sat.set_effects(a, i, [
                dmg(ENEMIES, AD, CHAOS, amount, randMax=1.5),
                sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=5.0),
                sat.effect(kind=DAMAGE, basis=CURHP, target=SINGLE, damageType=AD, attackType=CHAOS, multiplier=1.0, chance=1.0 / 3.0,
                           forbiddenTargetBuffId='B06B', targetCondition=PV_LT, targetConditionValue=200.0, skipDamageTakenMultiplier=1)])
        sat.set_head_field(a, 'skillName', 'LaillySkill3 — MANA게이지115(750 범위 2750000×1~1.5 + AId1 +5 + 1/3 즉사)')
    edit('SkillData_원작026_h049', 'B1: 마나 115 본체를 원문대로 — 750 범위 2750000(레벨 2: 3250000)×1~1.5 CHAOS/NORMAL · 범위 적 AId1 +5 · 1/3 즉사(B06B 없음·PV<200, 현재체력 100% 근사). '
         '옛 「ArmorBreak 20 단일」은 오라 A0ES였다(오라 에셋으로 옮김). ', b1)
    new('불멸_정윤식', 'SkillData_원작트리거_불멸_정윤식_LaillySkill3_stomp', 'LaillySkill3 stomp A081 — MANA게이지115(500 범위 1250000·스턴)',
        '원작 레일리 h049 LaillySkill3 레벨 1 갈래: 시전자 주인 더미 e01H가 대상 자리에서 stomp A081(AOws, Wrs1 1250000, aare 500, adur 빈칸 = 스톡 3.0 · ahdu 0.45).',
        GAUGE, sat.level_block(range=500.0, hitCountThreshold=115, gaugeKind=MANA,
                               effects=[dmg(ENEMIES, AP, SPELLS, 1250000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    # B2 A0EM 스턴 0.6초(adur 0.6)
    edit('SkillData_원작능력_불멸_정윤식_A0EM', 'B2: 스턴 0.6초(adur 0.6) 추가. ',
         lambda a: sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=STUN, target=SINGLE, duration=0.6)]))

    # ── 흰수염 h04A → 불멸_고도현 ──
    # B3 Ed_Skill_Mana(LIFE 115 → 1): 대상 중심 625 — 2750000×1~1.5 HERO/NORMAL 2회(0.2초 간격) / 시전자 중심 stomp A111 625: 500000·스턴.
    new('불멸_고도현', 'SkillData_원작트리거_불멸_고도현_Ed_Skill_Mana', '흰수염 체력 스킬 — LIFE게이지115(625 범위 2750000×1~1.5 ×2)',
        '원작 흰수염 h04A Trig_Ed_Attack: 체력==115 → 체력 1로 + Ed_Skill_Mana: 대상 중심 625 범위 2750000×1~1.5 HERO/NORMAL 두 번(0.2초 간격).',
        GAUGE, sat.level_block(range=625.0, hitCountThreshold=115, resetTo=1, gaugeKind=LIFE,
                               effects=[dmg(ENEMIES, AD, HERO, 2750000.0, randMax=1.5, hitCount=2, duration=0.2)]))
    new('불멸_고도현', 'SkillData_원작트리거_불멸_고도현_Ed_Skill_Mana_stomp', '흰수염 체력 스킬 stomp A111 — LIFE게이지115(시전자 중심 625 범위 500000·스턴)',
        '원작 흰수염 h04A Ed_Skill_Mana: Player(4) 더미 e0NF(UserData = 주인)가 시전자 자리에서 stomp A111(AOws, Wrs1 500000, aare 625, adur 빈칸 = 스톡 3.0 · ahdu 1.65).',
        GAUGE, sat.level_block(range=625.0, hitCountThreshold=115, resetTo=1, gaugeKind=LIFE, aoeCenter=CASTER,
                               effects=[dmg(ENEMIES, AP, SPELLS, 500000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    # B4 Ed_Skill_2sc: (21/22)×1/8, 파도 둘(A0OZ 350000씩) + 천둥박수 A0C7 600: 200000·이감 45% 3.5초 + 600 범위 600000 HERO/NORMAL
    def b4(a):
        sat.set_level_field(a, 0, 'triggerChance', round(21.0 / 22.0 / 8.0, 6))
        sat.set_level_field(a, 0, 'range', 600.0)
        blocks = [retarget(b, hitCount=2) for b in sat.get_effect_blocks(a, 0)]
        sat.set_effects(a, 0, blocks + [dmg(ENEMIES, AD, HERO, 600000.0), dmg(ENEMIES, AP, SPELLS, 200000.0),
                                        sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.55, duration=3.5)])
    edit('SkillData_더미채널_불멸_고도현_79행_01250', 'B4 Ed_Skill_2sc: 확률 1/8 → (21/22)×1/8(1/22 갈래의 elseif) · 파도 둘(350000 ×2 — 직선 장풍을 대상 2타로 근사) · '
         '대상 중심 600: 600000 HERO/NORMAL + 천둥박수 A0C7 200000·이감 45%(Ctc3 0.45) 3.5초. ', b4)
    # B5 Ed_Skill_1 stomp A098(피해 0, 625, adur 빈칸)
    edit('SkillData_게이트_불멸_고도현_caa364ec', 'B5: stomp A098(625 범위, 피해 0, adur 빈칸 = 스톡 3.0 · ahdu 1.65) 스턴 추가. 연구 R01V 계수·여진 이감(A100)·아이템 갈래는 미반영. ',
         lambda a: sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))

    # ── 시키 h04B → 불멸_고도현 ──
    # B6 1/10 stomp e026: A08O 600 범위 400000·스턴 + 방깎 −7 오라(A13U 615, 더미 수명 0.51초)
    def b6(a):
        sat.set_level_field(a, 0, 'range', 600.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 400000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR),
                               sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-7.0, duration=0.51, buffId='B06V')])
        sat.set_head_field(a, 'skillName', 'Shiki_Attack 1/10 — stomp A08O(600 범위 400000·스턴) + 방깎 −7 0.51초')
    edit('SkillData_더미채널_불멸_고도현_1', 'B6: 단일 400000 → 대상 중심 600 범위 400000(A08O Wrs1)·스턴(adur 빈칸 = 스톡 3.0 · ahdu 0.45) + 방깎 −7(A13U AHad, 더미 수명 0.51초 동안 — 반경 615는 600으로). ', b6)
    # B7 Shiki_champa2: 독립 1/16, 대상 중심 450: 400000 MAGIC/UNIVERSAL + 이동 지점 400 범위 250000 CHAOS/NORMAL ×3
    def b7(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.0625)
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [dmg(ENEMIES, AD, CHAOS, 250000.0, hitCount=3, duration=0.26)])
    edit('SkillData_회수_불멸_고도현_a80b907f', 'B7 Shiki_champa2: 확률 1/10×1/16 → 1/16(독립 if — 10배 과소였다) · 250000 CHAOS/NORMAL 3회 추가(원문은 시전자→대상 사이 이동 지점 400 범위 — 대상 중심 450으로 근사). ', b7)
    # B8 Shiki_Lion: 범위 적 AIsr +15 + stomp A08N 700 스턴(피해 0)
    edit('SkillData_회수_불멸_고도현_8eab1c6f', 'B8 Shiki_Lion: 범위 적 AIsr +15 · stomp A08N(700 범위, 피해 0, adur 빈칸 = 스톡 3.0 · ahdu 0.45) 스턴 추가. ',
         lambda a: sat.set_effects(a, 0, [sat.effect(kind=AISR, target=ENEMIES, multiplier=15.0)] + sat.get_effect_blocks(a, 0)
                                   + [sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))

    # ── 거프 h04C → 불멸_이승우 ──
    # B9 Garp_Mana2(레벨 1): 파이어볼트 A05Q(레벨 2로 올려 쏨, Htb1 99999·adur 3.0) → stomp A08U(500, 스턴 2.5) + 650 범위 (6000000+최대체력 5%)×(1+0.03n)
    def b9(a):
        blocks = [retarget(b, casterBuffCountFactor=0.03) for b in sat.get_effect_blocks(a, 0)]
        sat.set_effects(a, 0, blocks + [dmg(SINGLE, AP, SPELLS, 99999.0), sat.effect(kind=STUN, target=SINGLE, duration=3.0)])
    edit('SkillData_원작023_h04C', 'B9 Garp_Mana2 레벨 1: 버프 개수 계수 ×(1+0.03n) · 대상 파이어볼트 A05Q 99999·스턴 3초 추가(stomp A08U 스턴은 반경이 달라 따로 에셋). ', b9)
    new('불멸_이승우', 'SkillData_원작트리거_불멸_이승우_Garp_Mana2_stomp', 'Garp_Mana2 stomp A08U — MANA게이지160(500 범위 스턴 2.5초)',
        '원작 거프 h04C Garp_Mana2: 대상 자리 stomp A08U(AOws, Wrs1 0, aare 500, adur 2.5 · ahdu 0.41).',
        GAUGE, sat.level_block(range=500.0, hitCountThreshold=160, gaugeKind=MANA, effects=[sat.effect(kind=STUN, target=ENEMIES, duration=2.5)]))

    # ── 센고쿠 h04E → 불멸_이이삭 ──
    # B10 Seongoku_Skill_4(LIFE 75): 450 범위 500000 ×4 + 600 범위 2000000 NORMAL/UNIVERSAL
    def b10(a):
        sat.set_level_field(a, 0, 'range', 450.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 500000.0, hitCount=4, duration=0.36)])
        sat.set_head_field(a, 'skillName', 'Seongoku_Skill_4 — LIFE게이지75(450 범위 500000 ×4)')
    edit('SkillData_원작021_h04E', 'B10 Seongoku_Skill_4: 단일 500000 ×1 → 대상 중심 450 범위 500000 4회(0.12초 간격). 끝의 2000000은 600 범위라 따로 에셋. ', b10)
    new('불멸_이이삭', 'SkillData_원작트리거_불멸_이이삭_Seongoku_Skill_4_끝', 'Seongoku_Skill_4 끝 — LIFE게이지75(600 범위 2000000)',
        '원작 센고쿠 h04E Seongoku_Skill_4 스테이지 3: 대상 중심 600 범위 2000000 NORMAL/UNIVERSAL(옛 에셋은 단일이었다).',
        GAUGE, sat.level_block(range=600.0, hitCountThreshold=75, gaugeKind=LIFE, effects=[dmg(ENEMIES, AP, SPELLS, 2000000.0)]))
    # B11 1/10 stomp e02B: 525 범위 427500·스턴 2.85
    def b11(a):
        sat.set_level_field(a, 0, 'range', 525.0)
        sat.set_effects(a, 0, [retarget(b, target=ENEMIES) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_더미채널_불멸_이이삭_1', 'B11: 단일 → 대상 중심 525 범위(stomp e02B) 427500·스턴 2.85초. ', b11)
    # B12 부처의 일격: (9/10)×1/20, 레벨 1 = 450 범위 300000 + 현재체력 1.5% NORMAL/UNIVERSAL(레벨 2 값 400000 + 2%·495는 배타 — 뺀다)
    def b12(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.045)
        sat.set_level_field(a, 0, 'range', 450.0)
        sat.set_effects(a, 0, [sat.effect(kind=DAMAGE, basis=CURHP, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=0.015, bonus=300000.0,
                                          skipDamageTakenMultiplier=1)])
    edit('SkillData_원작능력_불멸_이이삭', 'B12 부처의 일격: 확률 1/20 → (9/10)×1/20(1/10 stomp의 elseif) · 단일 → 450 범위 · 레벨 1 값(300000 + 현재체력 1.5%)만 — '
         '레벨 2 값(400000 + 2%, 495 범위)은 A0D8 레벨 2일 때의 배타 갈래라 뺐다(둘이 같이 나가고 있었다). NORMAL/UNIVERSAL. ', b12)

    # ── 가반 h04F → 불멸_신지우 ──
    # A0ET: atar sapper(PV≥200) 전용
    edit('SkillData_원작능력_불멸_신지우_A0ET', '과다 정정: A0ET atar가 air,sapper,enemies,ground — sapper(= PV≥200, utyp 전수) 대상에만. ',
         lambda a: sat.set_effects(a, 0, [retarget(b, targetCondition=PV_GE, targetConditionValue=200.0) for b in sat.get_effect_blocks(a, 0)]))
    # B13 A0EJ·A120(AIcb, 평타 맞은 적 방깎 200초): −60(atar nonancient = PV≠200) / −70(atar ancient·sapper = PV 200). 같은 버프는 안 겹친다(갱신).
    new('불멸_신지우', 'SkillData_원작능력_불멸_신지우_A0EJ_A120', 'A0EJ·A120 가반 방깎 — 평타 맞은 적 방어 −60(PV≠200)/−70(PV 200) 200초',
        '원작 가반 h04F uabi A0EJ(AIcb, Iarp 60, adur 200, atar nonancient)·A120(AIcb, Iarp 70, adur 200, atar ancient·sapper). ancient = PV 200(utyp 전수). '
        '같은 능력은 겹치지 않고 갱신(버프 ID별 최댓값). AId1(상한 −75)과 다른 능력이라 그 상한 밖.',
        ON_HIT, sat.level_block(effects=[
            sat.effect(kind=ARMOR_BONUS, target=SINGLE, multiplier=-60.0, duration=200.0, buffId='A0EJ', targetCondition=PV_NE, targetConditionValue=200.0),
            sat.effect(kind=ARMOR_BONUS, target=SINGLE, multiplier=-70.0, duration=200.0, buffId='A120', targetCondition=PV_EQ, targetConditionValue=200.0)]))
    # 카이도 life100 넷째 효과: 용형 melee_1의 6/7 갈래는 K×0.35(×1.56 없음)
    def kaido_035(a):
        blocks = sat.get_effect_blocks(a, 0)
        hit = [i for i, b in enumerate(blocks) if re.search(r'multiplier: 0\.546\b', b)]
        assert len(hit) == 1, hit
        blocks[hit[0]] = retarget(blocks[hit[0]], multiplier=0.35)
        sat.set_effects(a, 0, blocks)
    edit('SkillData_카이도_불멸_신지우_life100', '§V-1: 용형 melee_1의 6/7 갈래는 K×0.35(1.56이 안 붙는다) — 0.546 → 0.35. ⚠️ 게이트(LIFE 100에 용형 평타 블록이 몰려 있음)는 형태 전환 구조(D5)가 없어 그대로. ', kaido_035)
    # B20 Kaido_Mana(LIFE 100): stomp A0TI 900 범위 2850000·스턴 3.5초 / 대상 중심 800: 999999×1~1.5 HERO/NORMAL 9회
    new('불멸_신지우', 'SkillData_원작트리거_불멸_신지우_Kaido_Mana', '카이도 체력 스킬 — LIFE게이지100(800 범위 999999×1~1.5 ×9)',
        '원작 카이도 h07M Kaido_Mana(체력==100): 대상 중심 800 범위 999999×1~1.5 HERO/NORMAL 9회(integerA<10, 0.1초 간격). A0TK·A0VI 스택(별도 AId1 능력)은 미반영.',
        GAUGE, sat.level_block(range=800.0, hitCountThreshold=100, gaugeKind=LIFE,
                               effects=[dmg(ENEMIES, AD, HERO, 999999.0, randMax=1.5, hitCount=9, duration=0.8)]))
    new('불멸_신지우', 'SkillData_원작트리거_불멸_신지우_Kaido_Mana_stomp', '카이도 체력 스킬 stomp A0TI — LIFE게이지100(900 범위 2850000·스턴 3.5초)',
        '원작 카이도 h07M Kaido_Mana: 시전자 주인 더미 e0GS의 stomp A0TI(AOws, Wrs1 2850000, aare 900, adur 3.5).',
        GAUGE, sat.level_block(range=900.0, hitCountThreshold=100, gaugeKind=LIFE,
                               effects=[dmg(ENEMIES, AP, SPELLS, 2850000.0), sat.effect(kind=STUN, target=ENEMIES, duration=3.5)]))

    # ── 제트 h04G → 불멸_박은석 ──
    edit('SkillData_버프게이트_불멸_박은석_B00M', '§V-12: B00M은 A0S4 0.6초 창 — 3타 충전은 과다, 다음 1타(buffHitCharges 1). ',
         lambda a: sat.set_effects(a, 0, [retarget(b, buffHitCharges=1) for b in sat.get_effect_blocks(a, 0)]))
    edit('SkillData_회수_불멸_박은석_b8d2fd85', '과다 정정: Z_skill_3의 직접 1000000은 대상 PV==200일 때만(장풍 1000000은 별도 에셋). ',
         lambda a: sat.set_effects(a, 0, [retarget(b, targetCondition=PV_EQ, targetConditionValue=200.0) for b in sat.get_effect_blocks(a, 0)]))
    # B14 Z_Skill_Mana stomp A0A8: 대상 자리 500 범위 200000·스턴(adur 빈칸 = 스톡 3.0, ahdu 3.0)
    new('불멸_박은석', 'SkillData_원작트리거_불멸_박은석_Z_Skill_Mana_stomp', 'Z_Skill_Mana stomp A0A8 — MANA게이지160(500 범위 200000·스턴)',
        '원작 제트 h04G Z_Skill_Mana: 시전자 주인 더미 e068이 대상 자리에서 stomp A0A8(AOws, Wrs1 200000, aare 500, adur 빈칸 = 스톡 3.0 · ahdu 3.0).',
        GAUGE, sat.level_block(range=500.0, hitCountThreshold=160, gaugeKind=MANA,
                               effects=[dmg(ENEMIES, AP, SPELLS, 200000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))

    # ── 로저 h04J → 불멸_김용태 ──
    # B15 roger_Mana 천둥박수 A0OY: 800 범위 1750000·이감(Ctc3 2.0 → 최저 이속) 3.25초
    new('불멸_김용태', 'SkillData_원작트리거_불멸_김용태_roger_Mana_천둥박수', 'roger_Mana 천둥박수 A0OY — MANA게이지150(800 범위 1750000·이감 3.25초)',
        '원작 로저 h04J roger_Mana: 시전자 주인 더미의 천둥박수 A0OY(ACt2, Ctc1 1750000, aare 800, Ctc3 2.0 = 이속 감소 200% → 최저 이속, adur 3.25 · ahdu 1.62).',
        GAUGE, sat.level_block(range=800.0, hitCountThreshold=150, gaugeKind=MANA,
                               effects=[dmg(ENEMIES, AP, SPELLS, 1750000.0), sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.0, duration=3.25)]))

    # ── 드래곤 h04D → 불멸_정준영 ──
    # B16 Dragon_Skill_Mana(마나 160): 대상 근처(90~360) 무작위 지점 425 범위 300000×1~2 NORMAL/UNIVERSAL 11회
    new('불멸_정준영', 'SkillData_원작트리거_불멸_정준영_Dragon_Skill_Mana', 'Dragon_Skill_Mana — MANA게이지160(425 범위 300000×1~2 ×11)',
        '원작 드래곤 h04D Dragon_Skill_Mana(마나==160): 대상에서 90~360 떨어진 무작위 지점 425 범위 300000×1~2 NORMAL/UNIVERSAL 11회(0.1~0.4초 간격) — 대상 중심으로 근사.',
        GAUGE, sat.level_block(range=425.0, hitCountThreshold=160, gaugeKind=MANA,
                               effects=[dmg(ENEMIES, AP, SPELLS, 300000.0, randMax=2.0, hitCount=11, duration=2.5)]))
    # B17 1/10 stomp e02L: A08V 525 범위 250000·스턴
    def b17(a):
        sat.set_level_field(a, 0, 'range', 525.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 250000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)])
        sat.set_head_field(a, 'skillName', 'Dragon_Attack 1/10 — stomp A08V(525 범위 250000·스턴)')
    edit('SkillData_더미채널_불멸_정준영_1', 'B17: 비어 있던 효과를 채움 — 시전자 주인 더미 e02L의 stomp A08V(AOws, Wrs1 250000, aare 525, adur 빈칸 = 스톡 3.0 · ahdu 0.45). 삭풍 h08H 소환체는 미반영. ', b17)

    # ── 빅맘 h04Q → 불멸_정준영 ──
    # B18 Bigmam_Attack 매 타: 5/6 대상 중심 450 (평타 피해×0.66 + 333333) NORMAL/UNIVERSAL / 1/6 600 범위 (×1.98 + 999999) CHAOS/UNIVERSAL + 대상 절반 추가 + A14S 스턴 1.0
    new('불멸_정준영', 'SkillData_원작트리거_불멸_정준영_Bigmam_Attack_5of6', 'Bigmam_Attack 5/6 — 450 범위 (평타 피해×0.66 + 333333)',
        '원작 빅맘 h04Q Bigmam_Attack: GetRandomInt(1,6)이 4가 아니면 대상 중심 450 범위 (GetEventDamage×0.66 + 333333) NORMAL/UNIVERSAL. '
        'Eat·hp가 터진 타엔 없다(그 배타는 미반영 — 독립 굴림 5/6).',
        ON_HIT, sat.level_block(triggerChance=round(5.0 / 6.0, 6), range=450.0,
                                effects=[sat.effect(kind=DAMAGE, basis=RECEIVED, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=0.66, bonus=333333.0)]))
    new('불멸_정준영', 'SkillData_원작트리거_불멸_정준영_Bigmam_Attack_1of6', 'Bigmam_Attack 1/6 — 600 범위 (평타 피해×1.98 + 999999) + 대상 절반 + 스턴 1초',
        '원작 빅맘 h04Q Bigmam_Attack: GetRandomInt(1,6)==4 → 대상 중심 600 범위 (GetEventDamage×1.98 + 999999) CHAOS/UNIVERSAL + 대상에 같은 식 ×0.5 + 파이어볼트 A14S 스턴(adur 1.0 · ahdu 0.5).',
        ON_HIT, sat.level_block(triggerChance=round(1.0 / 6.0, 6), range=600.0, effects=[
            sat.effect(kind=DAMAGE, basis=RECEIVED, target=ENEMIES, damageType=AP, attackType=CHAOS, multiplier=1.98, bonus=999999.0),
            sat.effect(kind=DAMAGE, basis=RECEIVED, target=SINGLE, damageType=AP, attackType=CHAOS, multiplier=0.99, bonus=499999.5),
            sat.effect(kind=STUN, target=SINGLE, duration=1.0)]))
    # B19 Bigmam_hp(LIFE 115): 자기 중심 900 — (900000 + 대상 최대체력 1%) NORMAL/UNIVERSAL 7회 + 자기 공속 +175% 2초(A14P Blo1 1.75, abuf B01F)
    new('불멸_정준영', 'SkillData_원작트리거_불멸_정준영_Bigmam_hp', 'Bigmam_hp — LIFE게이지115(자기 중심 900 범위 (900000 + 최대체력 1%) ×7 + 공속 +175% 2초)',
        '원작 빅맘 h04Q Bigmam_hp(체력==115): 시전자 중심 900 범위에 realD = 900000 + 평타 대상 최대체력×1% NORMAL/UNIVERSAL 7회(integerC>5까지, 0.15초 간격) + '
        '자기에게 A14P(Ablo, Blo1 +175% 공속, adur 2.0, abuf B01F). ⚠️ 원문의 1%는 평타 대상 하나의 최대체력 — 맞는 적마다 제 최대체력으로 근사. 절대쿨 B074는 안 걸린다(§V-5).',
        GAUGE, sat.level_block(range=900.0, hitCountThreshold=115, resetTo=1, gaugeKind=LIFE, aoeCenter=CASTER, effects=[
            sat.effect(kind=DAMAGE, basis=MAXHP, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=0.01, bonus=900000.0, hitCount=7, duration=0.9,
                       skipDamageTakenMultiplier=1),
            sat.effect(kind=SPEED, target=SELF, multiplier=1.75, duration=2.0, buffId='B01F')]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
