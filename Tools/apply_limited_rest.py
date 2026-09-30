"""제한됨 나머지 — LIMITED_FILL_LIST U4~U7 · L-10~L-12 · L-14 · L-16 · L-20 · L-21(2026-09-30 구현담당1).

트리거 본문(RedAttack · Rebeca_Skill_1·3 · Enel_Mana · Enel_skill_2 · King_skill_2 · Sinobu_Skill_hp2)과 능력 필드를 다시 열어 봤다. 다시 돌려도 같은 결과.
안 넣은 것: X5·X7·X8(출처 없는 스턴·주인 다름 — 보고 대상) · X6(대상 조건별 확률 축 없음) · L-13·L-15(ACbh adur 빈칸) · L-17~L-19·L-22·L-23(형태·분신·산성폭탄·Aliq) · §4 구조.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg, STOCK_AOWS_ADUR  # noqa: E402

ENEMIES, SINGLE = 2, 3
STUN, ARMOR_BREAK = 1, 2
AP = 2
SPELLS = 7
TAG = '[제한 나머지 09-30]'


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
        if note(a, text + '(Tools/apply_limited_rest.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def add(a, extra, front=False):
        b = sat.get_effect_blocks(a, 0)
        sat.set_effects(a, 0, extra + b if front else b + extra)

    # U4 강보명 RedAttack 3/4는 독립 if(GetRandomInt(1,4)>1)
    edit('SkillData_게이트_제한_강보명_RedAttack_3of4', 'U4: GetRandomInt(1,4)>1은 앞의 if/elseif와 무관한 독립 굴림 — 0.7159 → 0.75. ',
         lambda a: sat.set_level_field(a, 0, 'triggerChance', 0.75))

    # U5·U6·U7 최영민(레베카)
    def rebeca3(a):
        sat.set_level_field(a, 0, 'range', 850.0)
        sat.set_level_field(a, 0, 'aoeCenter', 1)
        sat.set_effects(a, 0, [retarget(b, target=ENEMIES) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_더미채널_제한_최영민_79행_00833', 'U5: 천둥박수 A0QX는 시전자 중심 850 범위(100000 · 이속 −70% 2.5초) — 단일이었다. ', rebeca3)

    def rebeca1clap(a):
        sat.set_level_field(a, 0, 'range', 460.0)
        sat.set_effects(a, 0, [retarget(b, target=ENEMIES) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_더미채널_제한_최영민_79행_02000', 'U6: 천둥박수 A0QY는 대상 중심 460 범위 50000 — 단일이었다. ', rebeca1clap)

    def lvl1(pairs):
        def fn(a):
            out = []
            for b in sat.get_effect_blocks(a, 0):
                for old, new in pairs:
                    if re.search(r'multiplier: %s\b' % re.escape(old), b):
                        b = retarget(b, multiplier=new)
                out.append(b)
            sat.set_effects(a, 0, out)
        return fn
    edit('SkillData_게이트_제한_최영민_33bd8aa4', 'U7: 「전설의 후예」 A0QW는 레벨 1에서 시작 — 600000 + 25000×1 = 625000 · 2100000 + 87500×1 = 2187500(레벨 0 값이었다). 레벨 성장(1/45)은 미반영. ',
         lvl1([('600000.0', 625000.0), ('2100000.0', 2187500.0)]))
    edit('SkillData_게이트_제한_최영민_30f704bf', 'U7: Rebeca_Skill_3 800000 + 30000×A0QW 레벨(1) = 830000. ', lvl1([('800000.0', 830000.0)]))

    # L-10·L-14·L-16: 피해 0 · 스턴만 있는 강타 — 다른 에셋 설명에 이름만 적혀 있고 효과는 없었다(apply_uabi_passives는 이름이 보이면 건너뛴다)
    def bash(roster, ab, name, desc, chance, stun):
        stem = 'SkillData_원작능력_%s_%s' % (roster, ab)
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, '원작 ' + desc + ' 2026-09-30 구현담당1(LIMITED_FILL_LIST §3-2).', 0,
                          [sat.level_block(triggerChance=chance, effects=[sat.effect(kind=STUN, target=SINGLE, duration=stun)])])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))
    bash('제한_이충민', 'A0RQ', 'A0RQ !폭발탄 사격 — 25% 스턴 1.75초', '아인 uabi A0RQ(ACbh, Hbh1 25, 피해 0, adur 1.75 · ahdu 0.88).', 0.25, 1.75)
    bash('제한_최영민', 'A0IV', 'A0IV !트루에노 바스타드 — 33% 스턴 3.0초', '레베카 h05I uabi A0IV(ACbh, Hbh1 33, 피해 0, adur 3.0 · ahdu 1.5).', 0.33, 3.0)
    bash('제한_최영민', 'A0GD', 'A0GD !숲숲 열매 — 20% 스턴 1.75초', '아라마키 h08Q uabi A0GD(ACbh, Hbh1 20, 피해 0, adur 1.75).', 0.2, 1.75)

    # L-11·L-12 전법규(에넬)
    edit('SkillData_게이트_제한_전법규_Enel_Mana', 'L-11: stomp A0PN(시전자 주인 더미, 대상 중심 800 범위, Wrs1 4500000, adur 빈칸 = 스톡 3.0 · ahdu 0.45) 추가 — 고정 피해 450만이 통째로 빠져 있었다. ',
         lambda a: add(a, [dmg(ENEMIES, AP, SPELLS, 4500000.0), sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)]))
    edit('SkillData_게이트_제한_전법규_9ffa9c45', 'L-12: Enel_skill_2는 4회(integerA==4까지, 0.1초 간격) — 1회였다(4배 과소). ',
         lambda a: sat.set_effects(a, 0, [retarget(b, hitCount=4, duration=0.3) for b in sat.get_effect_blocks(a, 0)]))

    # L-20 이유범(시노부) LIFE 50: 대상 파이어볼트 A0VV 스턴 1.75초
    edit('SkillData_게이트_제한_이유범_351a0f00', 'L-20: Sinobu_Skill_hp2 — 대상 파이어볼트 A0VV(adur 1.75, 피해 0) 스턴 추가. ',
         lambda a: add(a, [sat.effect(kind=STUN, target=SINGLE, duration=1.75)]))

    # L-21 임준성(킹) King_skill_2: 대상 AId1 +5
    edit('SkillData_게이트_제한_임준성_355f5d39', 'L-21: King_skill_2 — 대상 AId1 +5 추가(이 스택이 50을 넘어야 처형 갈래 King_skill_2_tr가 열린다). ',
         lambda a: add(a, [sat.effect(kind=ARMOR_BREAK, target=SINGLE, multiplier=5.0)], front=True))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
