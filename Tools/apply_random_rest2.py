"""랜덤전용[제한됨] 통째로 빠져 있던 셋 — 유카리(손오공)·히그마(미도리야)·타츠마키(이민형)(2026-09-30 구현담당1 — RANDOM_FILL_LIST §0-7·8·9).

평타 트리거(Yukari_Attack · Higma_Attack · Tatsumaki_Attack)와 거기서 부르는 트리거의 게이트 줄·RRD 줄, 더미 능력 필드를 다시 열어 봤다. 다시 돌려도 같은 결과.
돌린 뒤 `python3 Tools/sync_mana_regen_from_w3u.py`로 이민형 LIFE 게이지(타츠마키 h0BC)를 채운다.
안 넣은 것: 유카리 열차의 「량 수」 배수(한 줄 11틱만) · 히그마 LIFE 85의 돈 보상(경제) · 즉사 뒤 노획물.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg, STOCK_AOWS_ADUR  # noqa: E402

SELF, ENEMIES, SINGLE, RANDOM_ENEMY = 0, 2, 3, 4
DAMAGE, STUN, ARMOR_BREAK, SPEED = 0, 1, 2, 12
AD, AP = 1, 2
CHAOS, SPELLS = 5, 7
CURHP = 2
PV_LT, PV_GE = 1, 3
ON_HIT, GAUGE = 0, 3
MANA, LIFE = 0, 1
CASTER = 1
TAG = '[랜덤 통째 09-30]'


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
        if note(a, text + '(Tools/apply_random_rest2.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def kill(target, **kw):
        # 즉사 = 현재체력 100%(기존 근사 — 절대쿨_불멸_정윤식과 같은 꼴). PV<200만.
        return sat.effect(kind=DAMAGE, basis=CURHP, target=target, damageType=AP, attackType=SPELLS, multiplier=1.0,
                          targetCondition=PV_LT, targetConditionValue=200.0, skipDamageTakenMultiplier=1, **kw)

    # ── 유카리 h0A6 → 랜덤_손오공
    R = '랜덤_손오공'
    new(R, 'SkillData_원작트리거_랜덤_손오공_Yukari_D2', '유카리 — MANA게이지145(600 범위 2000000 → 3000000)',
        '유카리 h0A6 Yukari_Attack: 마나==145 → Yukari_D2: 대상 중심 600 범위 2000000, 0.45초 뒤 600 범위 3000000 NORMAL/UNIVERSAL.', GAUGE,
        sat.level_block(range=600.0, hitCountThreshold=145, gaugeKind=MANA, effects=[dmg(ENEMIES, AP, SPELLS, 2000000.0), dmg(ENEMIES, AP, SPELLS, 3000000.0)]))
    new(R, 'SkillData_원작트리거_랜덤_손오공_Yukari_D2_즉사', '유카리 — MANA게이지145 즉사(475 범위 PV<200 적 하나)',
        '유카리 h0A6 Yukari_D2: 대상 중심 475 범위의 PV<200 적 하나 KillUnit — 무작위 적 하나에 현재체력 100%(PV<200 조건)로 근사.', GAUGE,
        sat.level_block(range=475.0, hitCountThreshold=145, gaugeKind=MANA, effects=[kill(RANDOM_ENEMY)]))
    new(R, 'SkillData_원작트리거_랜덤_손오공_Yukari_W', '유카리 — Yukari_W 1/10(450 범위 3200000)',
        '유카리 h0A6 Yukari_Attack GetRandomInt(1,10)==3 → Yukari_W: 대상 중심 450 범위 3200000 NORMAL/UNIVERSAL.', ON_HIT,
        sat.level_block(triggerChance=0.1, range=450.0, effects=[dmg(ENEMIES, AP, SPELLS, 3200000.0)]))
    new(R, 'SkillData_원작트리거_랜덤_손오공_스키마', '유카리 — 스키마 1/25(PV<200 대상 즉사 / 그 밖 300000×0.8~1.5)',
        '유카리 h0A6 Yukari_Attack GetRandomInt(1,25)==3: 대상 PV<200이면 KillUnit, 아니면 300000×0.8~1.5 NORMAL/UNIVERSAL.', ON_HIT,
        sat.level_block(triggerChance=0.04, effects=[kill(SINGLE), dmg(SINGLE, AP, SPELLS, 300000.0, randMin=0.8, randMax=1.5, targetCondition=PV_GE, targetConditionValue=200.0)]))
    edit('SkillData_게이트_랜덤_손오공_948ad1b7', 'Yukari_R2 열차: 395 범위 300000이 5스테이지마다 한 번, 스테이지 15~65 = 11틱 — 1회였다(≥11배 과소). 열차 량 수(최대 다섯)만큼 더 맞는 것은 미반영. ',
         lambda a: sat.set_effects(a, 0, [retarget(b, hitCount=11, duration=2.5) for b in sat.get_effect_blocks(a, 0)]))

    # ── 히그마 h0AC → 랜덤_미도리야_이즈쿠: Higma_Attack — LIFE==85: 대상 5000000 / 1/20 Higma_Q / 1/12 공속 버프 B05N / B05N 동안 매 타 Higma_W
    R = '랜덤_미도리야_이즈쿠'

    def higma_w(a):
        a.head = re.sub(r'^  triggerType: \d+$', '  triggerType: 0', a.head, flags=re.M)
        sat.set_level_field(a, 0, 'hitCountThreshold', 0)
        sat.set_level_field(a, 0, 'gaugeKind', 0)
        sat.set_effects(a, 0, [retarget(b, hitCount=2, duration=0.1) for b in sat.get_effect_blocks(a, 0)])
        sat.set_head_field(a, 'skillName', '히그마(랜덤) — Higma_W(B05N 동안 매 타: 400 범위 175000 ×2)')
    edit('SkillData_게이트_랜덤_미도리야_이즈쿠_079f80a5', '§0-8: Higma_W는 「LIFE 85 AND B05N」이 아니라 B05N 동안 매 타(별도 최상위 if) — 발동 방식 OnHitCount(LIFE 85) → 평타 확률 1 + requiredBuffId B05N. 175000 ×2. ', higma_w)
    edit('SkillData_버프게이트_랜덤_미도리야_이즈쿠_B05N', '§0-8: B05N = 공속 +400% 1.15초 버프(더미 e0MO의 bloodlust) — 「2타 충전」 ApplyBuff → AttackSpeedBuffPercent 4.0·1.15초·buffId B05N. ',
         lambda a: sat.set_effects(a, 0, [sat.effect(kind=SPEED, target=SELF, multiplier=4.0, duration=1.15, buffId='B05N')]))

    def higma_q(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [retarget(b[0], hitCount=22, duration=3.2)])
        sat.set_head_field(a, 'skillName', '히그마(랜덤) — Higma_Q 1/20(375 범위 100000 ×22)')
    edit('SkillData_게이트_랜덤_미도리야_이즈쿠_2c7de21c', '§0-8: Higma_Q는 375 범위 100000이 ≈22회(스테이지 33까지, 스테이지마다 그룹 호출 둘 중 하나) — 효과 둘 × 2회 = 4회였다(≈5배 과소). 효과 하나 hitCount 22로. ', higma_q)
    new(R, 'SkillData_원작트리거_랜덤_미도리야_이즈쿠_Higma_LIFE85', '히그마(랜덤) 체력 스킬 — LIFE게이지85(대상 5000000)',
        '히그마 h0AC Higma_Attack: 체력==85 → 대상 5000000 NORMAL/UNIVERSAL(+ 2/5로 돈 500~1000 — 경제, 미반영).', GAUGE,
        sat.level_block(hitCountThreshold=85, resetTo=1, gaugeKind=LIFE, effects=[dmg(SINGLE, AP, SPELLS, 5000000.0)]))

    # ── 타츠마키 h0BC → 랜덤_이민형
    R = '랜덤_이민형'
    edit('SkillData_게이트_랜덤_이민형_4266b6e3', '타츠마키 운석(마나 85): 범위 적 AId1 +10(피해 전에) 추가. stomp A12O는 반경이 달라 따로 에셋. ',
         lambda a: sat.set_effects(a, 0, [sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=10.0)] + sat.get_effect_blocks(a, 0)))
    new(R, 'SkillData_원작트리거_랜덤_이민형_Tatsumaki_T_stomp', '타츠마키 운석 stomp A12O — MANA게이지85(625 범위 1500000·스턴)',
        '타츠마키 h0BC Tatsumaki_T_Explosion: stomp A12O(AOws, Wrs1 1500000, aare 625, adur 빈칸 = 스톡 3.0 · ahdu 3.0).', GAUGE,
        sat.level_block(range=625.0, hitCountThreshold=85, gaugeKind=MANA, effects=[dmg(ENEMIES, AP, SPELLS, 1500000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    new(R, 'SkillData_원작트리거_랜덤_이민형_Tatsumaki_R', '타츠마키 체력 스킬 — LIFE게이지50(600 범위 3000000 + AId1 +7 · stomp 275000·스턴 1.75초)',
        '타츠마키 h0BC Tatsumaki_Attack: 체력==50 → Tatsumaki_R_Attack: stomp A12K(525 범위 275000, adur 1.75) → 600 범위 AId1 +7 + 3000000 CHAOS/NORMAL(stomp는 600으로).', GAUGE,
        sat.level_block(range=600.0, hitCountThreshold=50, resetTo=1, gaugeKind=LIFE, effects=[
            sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=7.0), dmg(ENEMIES, AP, SPELLS, 275000.0), sat.effect(kind=STUN, target=ENEMIES, duration=1.75),
            dmg(ENEMIES, AD, CHAOS, 3000000.0)]))
    new(R, 'SkillData_원작트리거_랜덤_이민형_Tatsumaki_Q2', '타츠마키 — Q2 1/7(stomp 525 범위 275000·스턴 1.75초)',
        '타츠마키 h0BC Tatsumaki_Attack GetRandomInt(1,7)==3 → Tatsumaki_Q2_Attack: stomp A12K(AOws, Wrs1 275000, aare 525, adur 1.75).', ON_HIT,
        sat.level_block(triggerChance=round(1.0 / 7.0, 6), range=525.0, effects=[dmg(ENEMIES, AP, SPELLS, 275000.0), sat.effect(kind=STUN, target=ENEMIES, duration=1.75)]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
