"""SKILL_PATTERN_AUDIT ④ 중복효과 「높음」 13건 원문 대조 + 랜덤전용 🔴 둘(2026-09-30 구현담당1 — PM 지시).

13건 판정(원문 트리거의 RRD 줄 수·반복 조건으로 확인):
  진짜 중복(한 벌로):   강주혁 5c20b80f(나미 미라쥬 — 아이템 갈래 둘을 같이 더함) · 노태현 3a13fd3d(브룩 1회 + 40%로 한 번 더 — 4회였다) ·
                        박민수 b5c9b49a(조로 마나 145 — 두 갈래의 2500000을 같이) · 박민수 b7ae9b41(조로 raven 3회 — 4회였다) ·
                        임채민 79행_01000(티치 지진 — 두 갈래를 같이) · 조성진 33bd8aa4(우솝 262500 2회 — 4회였다) · 탄지로(1800000 세 벌)
  중복 아님(그대로):     신지우 life100(용형 Skill_2는 두 발) · 강주혁 79행_10000(블리자드 더미 둘) · 임채민 42844439(4회 — 효과 하나 hitCount 4로 합치기만, 동작 같음)
  과다가 아니라 과소:    미도리야 2c7de21c(원문 ≈22회, 지금 4회 — T강화 구조와 얽혀 안 건드림)
  죽은 에셋:            김정래 원작능력_영원_김정래 두 건(확률 0 — 안 돈다, 그대로)
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg, STOCK_AOWS_ADUR  # noqa: E402

ENEMIES, SINGLE = 2, 3
DAMAGE, STUN, SLOW = 0, 1, 13
AD, AP = 1, 2
PIERCE, CHAOS, SPELLS = 2, 5, 7
MAXHP, STR = 1, 9
PV_EQ, PV_GE = 2, 3
TAG = '[중복 점검 09-30]'


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

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_audit_dupes.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def cond(a, c, v=200.0):
        sat.set_effects(a, 0, [retarget(b, targetCondition=c, targetConditionValue=v) for b in sat.get_effect_blocks(a, 0)])

    # 🔴 랜덤_야사카_카나코 A0GL: atar ancient → PV 200 대상만
    edit('SkillData_원작능력_랜덤_야사카_카나코', 'A0GL(ACbh, Hbh1 100, Hbh2 3.0, adur 3.0) atar air,ancient,enemies,ground — PV 200 대상에게만. 모든 적에게 매 타 ×3 + 스턴 3.0초(영구 스턴)였다. ',
         lambda a: cond(a, PV_EQ))

    # 🔴 랜덤_카마도_탄지로(뱀파이어 h06T Trig_vampire): 1/10 → 500 범위 300000×1~3 CHAOS/UNIVERSAL. 1800000(600 범위) + AIsr +8은 「마나 115 & A0LA(T강화)」 블록 — 구조 칸이라 뺀다.
    def vampire(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.1)
        sat.set_level_field(a, 0, 'range', 500.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, CHAOS, 300000.0, randMax=3.0)])
        sat.set_head_field(a, 'skillName', 'Trig_vampire 1/10 — 500 범위 300000×1~3')
    edit('SkillData_원작능력_랜덤_카마도_탄지로', '확률 0.18에 단일 1800000 세 벌 + 300000 + AIsr 8이 들어 있었다(18%로 대상 ≈720만). 원문: GetRandomInt(1,10)==5 → 500 범위 300000×1~3 CHAOS/UNIVERSAL뿐. '
         '1800000(600 범위)·AIsr +8은 「마나==115 그리고 A0LA(T랜덤유닛강화) 보유」 블록 — 강화 구조가 없어 뺐다. ', vampire)

    # 강주혁(나미 Nami_Skill_2, 아이템 없음): 대상 중심 300 범위 400000(점 피해 — 대상 포함) + 대상 400000
    def nami(a):
        sat.set_level_field(a, 0, 'range', 300.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 400000.0), dmg(SINGLE, AP, SPELLS, 400000.0)])
    edit('SkillData_게이트_초월_강주혁_AP_5c20b80f', 'Nami_Skill_2(아이템 없음): 대상 중심 300 범위 400000 + 대상 400000 NORMAL/UNIVERSAL(대상 80만 · 곁 40만). 아이템 갈래(800000 묶음 · 둘째 400000 묶음)를 같이 더해 대상 240만이었다(3배 과다). ', nami)

    # 노태현(브룩 Brook_Skill_1): 360 범위 350000×1~1.5 1회 + GetRandomInt(1,5)가 4·5면(40%) 한 번 더
    def brook(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [retarget(b[0], hitCount=1), retarget(b[1], hitCount=1, chance=0.4)])
    edit('SkillData_회수_초월_노태현_AP_3a13fd3d', 'Brook_Skill_1: 1회 + 40%로 한 번 더(평균 1.4회) — 효과 둘 × hitCount 2 = 4회였다(2.86배 과다). ', brook)

    # 박민수(조로 Zoro_samchun1, 기본 갈래): 525 범위 2500000×1~2.5 한 번 + stomp A0O4 600 범위 스턴(adur 빈칸)
    def samchun(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [b[0], sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)])
    edit('SkillData_게이트_초월_박민수_AD_b5c9b49a', 'Zoro_samchun1(A0GT 없음 = 기본 갈래): 2500000×1~2.5 한 번 — 두 갈래 값을 같이 넣어 2배였다. stomp A0O4 스턴(adur 빈칸 = 스톡 3.0 · ahdu 3.0, 원문 600 범위 — 525로) 추가. ', samchun)

    # 박민수(조로 Zoro_raven): 500 범위 (350000 + STR×21000) → 350000 → 350000 (3회)
    def raven(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [retarget(b[0], hitCount=3, duration=0.35),
                               sat.effect(kind=DAMAGE, basis=STR, target=ENEMIES, damageType=AD, attackType=CHAOS, multiplier=21000.0)])
    edit('SkillData_게이트_초월_박민수_AD_b7ae9b41', 'Zoro_raven: 350000 3회(4회였다) + 첫 타의 STR×21000 추가. ', raven)

    # 임채민(티치 Tichi_skill_2_tr): 4회가 맞다 — 같은 효과 둘을 hitCount 4 하나로(동작 같음)
    def tichi_line(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, [retarget(b[0], hitCount=4, duration=0.18)])
    edit('SkillData_게이트_초월_임채민_AP_42844439', 'Tichi_skill_2_tr는 4회가 맞다(0.06초 간격) — 같은 효과 둘(hitCount 2)을 하나(hitCount 4)로 합침, 피해 총량 같음. ', tichi_line)

    # 임채민(티치 Tichi_skill_4 지진 — 기본 5/6 갈래): thunderclap A13V 650 범위 400000 · 이감 70%(Htc3 0.7) 3.0초
    def quake(a):
        sat.set_level_field(a, 0, 'range', 650.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 400000.0), sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.3, duration=3.0)])
    edit('SkillData_더미채널_초월_임채민_AP_79행_01000', 'Tichi_skill_4 지진(기본 갈래 A13V): 대상 중심 650 범위 400000 · 이감 70% 3초 한 벌 — 단일 400000 ×2 + 이감 둘(두 갈래 A13V·A0Q6을 같이)이었다. ', quake)

    # 조성진(우솝 Usop_Skill_2): 대상 PV≥200일 때만. 600 범위 262500 2회 + 대상 PV<300이면 최대체력×8.5/PV + 1050000/PV(PV 200 → 4.25% + 5250), PV≥300이면 350000
    def usop(a):
        b = sat.get_effect_blocks(a, 0)
        area = retarget(b[0], targetCondition=PV_GE, targetConditionValue=200.0)
        big = [x for x in b if re.search(r'multiplier: 350000', x)]
        sat.set_effects(a, 0, [area] + big + [
            sat.effect(kind=DAMAGE, basis=MAXHP, target=SINGLE, damageType=AP, attackType=PIERCE, multiplier=0.0425, bonus=5250.0,
                       targetCondition=PV_EQ, targetConditionValue=200.0, skipDamageTakenMultiplier=1)])
    edit('SkillData_게이트_초월_조성진_AD_33bd8aa4', 'Usop_Skill_2: 262500 2회(효과 둘 = 4회였다) · PV 200 갈래(최대체력 4.25% + 5250 PIERCE/UNIVERSAL) 추가. '
         '⚠️ 원문은 「평타 대상이 PV≥200일 때만」 도는 블록 — 범위 효과에 PV≥200 조건을 걸었으나 이건 맞는 적마다의 조건이라, 주 대상이 일반 몹이어도 곁의 PV≥200 적은 맞는다(스킬 단위 대상 조건 축 없음). ', usop)

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
