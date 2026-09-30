"""전설적인 남은 셋(2026-09-30 구현담당1 — LEGEND_FILL_LIST §0-4 울티 · §0-9 마르코 LIFE 25 · §0-10 센고쿠) + 루치 A048 스턴.

트리거 본문(Legend35ulti·_trg·_trg3 · Legend17_Marcodamage·_MacroLifeSkill · Legend8)과 능력 필드를 다시 열어 봤다. 다시 돌려도 같은 결과.
돌린 뒤 `python3 Tools/sync_mana_regen_from_w3u.py`로 LIFE 게이지 필드(임채민 = 마르코 h02T uhpm 25·uhpr 0.5)를 채운다.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg  # noqa: E402

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, ARMOR_BREAK, PERCENT = 0, 1, 2, 14
AD, AP = 1, 2
HERO, CHAOS, SPELLS = 4, 5, 7
RECEIVED = 5
PV_LT, PV_EQ, PV_GE = 1, 2, 3
ON_HIT, GAUGE = 0, 3
LIFE = 1
TAG = '[전설 남은 것 09-30]'


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
        if note(a, text + '(Tools/apply_legend_rest.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    # §0-4 울티 h03S → 전설적인_임채현: Legend35ulti — LIFE==35면 trg3(울두건), 아니면 15%로 trg
    def ulti15(a):
        a.head = re.sub(r'^  triggerType: \d+$', '  triggerType: 0', a.head, flags=re.M)
        sat.set_level_field(a, 0, 'hitCountThreshold', 0)
        sat.set_level_field(a, 0, 'triggerChance', 0.15)
        sat.set_head_field(a, 'skillName', '울티(전설) — 15%(400 범위 평타 피해×0.85 + 115000)')
    edit('SkillData_게이트_전설적인_임채현_03847fe6', '§0-4: 이 블록(400 범위 K×0.85 + 115000 CHAOS/NORMAL)은 LIFE 35 스킬이 아니라 「LIFE 타가 아닐 때 15%」(elseif GetRandomInt(1,100)<=15) — 발동 방식 OnHitCount(LIFE 35) → 평타 확률 0.15(≈5배 과소였다). ', ulti15)
    new('전설적인_임채현', 'SkillData_원작트리거_전설적인_임채현_Legend35ulti_trg3', '울티(전설) 울두건 — LIFE게이지35(450 범위 230000×PV/100 + AId1 +1)',
        '울티 h03S Legend35ulti: 체력==35 → trg3: 대상 중심 450 범위 ((GetEventDamage×1.7) + 230000)×(PV/100) CHAOS/UNIVERSAL + AId1 +1. '
        '피해 이벤트 밖이라 GetEventDamage 항은 0으로 본다(확신 보통). PV 배율은 <200 ×1 · 200 ×2 · ≥300 ×3으로.',
        GAUGE, sat.level_block(range=450.0, hitCountThreshold=35, resetTo=1, gaugeKind=LIFE, effects=[
            dmg(ENEMIES, AP, CHAOS, 230000.0, targetCondition=PV_LT, targetConditionValue=200.0),
            dmg(ENEMIES, AP, CHAOS, 460000.0, targetCondition=PV_EQ, targetConditionValue=200.0),
            dmg(ENEMIES, AP, CHAOS, 690000.0, targetCondition=PV_GE, targetConditionValue=300.0),
            sat.effect(kind=ARMOR_BREAK, target=ENEMIES, multiplier=1.0)]))

    # §0-9 마르코 h02T → 전설적인_임채민: Legend17_Marcodamage — 체력==25면 MacroLifeSkill(깃털폭풍), 아니면 체력 +1
    new('전설적인_임채민', 'SkillData_원작트리거_전설적인_임채민_Legend17_LifeSkill', '마르코(전설) 깃털폭풍 — LIFE게이지25(525 범위 500000 + 평타 피해×5 · 대상 400000 + ×2)',
        '마르코 h02T Legend17_Marcodamage: 체력==25 → Legend17_MacroLifeSkill: 대상 중심 525 범위 (500000 + K×5) CHAOS/NORMAL + 대상 (400000 + K×2) CHAOS/UNIVERSAL. '
        '부채꼴 칼날 A08J(AEfk, 1100 범위 400000·최대 7마리)는 대상 수 상한 축이 없어 미반영.',
        GAUGE, sat.level_block(range=525.0, hitCountThreshold=25, resetTo=1, gaugeKind=LIFE, effects=[
            sat.effect(kind=DAMAGE, basis=RECEIVED, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=5.0, bonus=500000.0),
            sat.effect(kind=DAMAGE, basis=RECEIVED, target=SINGLE, damageType=AP, attackType=CHAOS, multiplier=2.0, bonus=400000.0)]))
    edit('SkillData_원작능력_전설적인_임채민_A048', '루치 A048(ACbh, adur 0.6): 스턴 0.6초 추가. ',
         lambda a: sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [sat.effect(kind=STUN, target=SINGLE, duration=0.6)]))

    # §0-10 센고쿠 h036 → 전설적인_이유선: Legend8 — 1/10 충격파 · 1/10(독립) 인의 포효
    def shock(a):
        sat.set_level_field(a, 0, 'range', 500.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 232500.0), dmg(SINGLE, AD, HERO, 465000.0, randMax=1.25, targetCondition=PV_GE, targetConditionValue=200.0)])
        sat.set_head_field(a, 'skillName', 'Legend8 충격파 — 1/10(500 범위 232500 + PV≥200 대상 465000×1~1.25)')
    edit('SkillData_원작능력_전설적인_이유선', 'Legend8 충격파: 대상 중심 500 범위 232500 NORMAL/UNIVERSAL(UnitDamagePointLoc) + 대상 PV≥200이면 465000×1~1.25 HERO/NORMAL — '
         '범위 232500이 없고 465000이 조건 없이 두 번(+ 값 1.0짜리 찌꺼기)이었다. ', shock)
    new('전설적인_이유선', 'SkillData_원작트리거_전설적인_이유선_Legend8_인의', '센고쿠(전설) 인의 — 1/10(1900 범위 아군 공격력 +40% 6초)',
        '센고쿠 h036 Legend8 GetRandomInt(1,10)==4(독립): 더미가 roar A058(Aroa, Roa1 +0.4 공격력, aare 1900, adur 6, abuf B007). 자기 포함 아군. '
        '⚠️ A058의 Roa2 −18(방어)·atar에 enemies가 같이 있다 — 적 방어 −18이 걸리는지는 엔진 동작이라 미확정, 미반영.',
        ON_HIT, sat.level_block(triggerChance=0.1, range=1900.0, aoeCenter=1, effects=[
            sat.effect(kind=PERCENT, target=t, multiplier=0.4, duration=6.0, buffId='B007') for t in (SELF, ALLIES)]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
