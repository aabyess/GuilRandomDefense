"""전설적인 남은 범위화·스턴·빠진 블록(2026-09-30 구현담당1, PM 지시 — LEGEND_FILL_LIST §2의 ⚠️·❌ 가운데 지금 축으로 담기는 것).

트리거(Legend12·9·15·5·51·6·10·4·32·30)와 더미 능력 필드를 다시 열어 봤다. 다시 돌려도 같은 결과.
안 넣은 것: 징베 마나 145의 스턴 1.0(출처 없는 스턴 — 보고) · 전설적인_최상호의 30% 스턴(주인 다름 — 보고) · 히루루크 · 소환체(시키 부유물·시노부 분신) ·
  시간제 더미 오라 · 라분 6타 보장 카운터 · 핸콕 키스 0.4초 공속 · 카르가라 시전 능력 ·
  **코비 「용기의 외침」**(A0O1 ANht, Roa1 −0.2, 대상 아군) — 함성의 Roa1 음수가 아군 공격력 −20%인지 +20%인지 엔진 동작이라 미확정.
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
HERO, SPELLS = 4, 7
ATK = 3
PV_LT, PV_EQ, PV_GE = 1, 2, 3
ON_HIT = 0
TAG = '[전설 범위화 09-30]'


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

    def new(roster, stem, name, desc, level):
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, '원작 ' + desc + ' 2026-09-30 구현담당1.', ON_HIT, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_legend_rest2.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def add(a, extra):
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + extra)

    def cond(a, c, v=200.0):
        sat.set_effects(a, 0, [retarget(b, targetCondition=c, targetConditionValue=v) for b in sat.get_effect_blocks(a, 0)])

    # 라분 h02Q → 이승우: 박치기 stomp A07V 600 범위 스턴 2.25초(피해 0)
    edit('SkillData_원작능력_전설적인_이승우_Legend12_25', '박치기: stomp A07V(600 범위, 피해 0, adur 2.25) 스턴 추가(575로). 「6타째 보장」 카운터는 미반영. ',
         lambda a: add(a, [sat.effect(kind=STUN, target=ENEMIES, duration=2.25)]))
    # 조로 h02S → 정준영: 사자의 노래 스턴 1.0 · A0PQ는 PV 200 대상만
    edit('SkillData_게이트_전설적인_정준영_3a13fd3d', '사자의 노래: 대상 파이어볼트 A0UQ 스턴 1.0초 추가. ', lambda a: add(a, [sat.effect(kind=STUN, target=SINGLE, duration=1.0)]))
    edit('SkillData_원작능력_전설적인_정준영_A0PQ', 'A0PQ atar air,ancient,sapper,… — ancient(= PV 200) 대상만. 일반 몹에 30% +500000·스턴 1.0초가 걸리고 있었다. ', lambda a: cond(a, PV_EQ))
    # 바르토로메오 h02Z → 박민수: Legend15 — 1/10 배리어, 아니면 1/30 배리어불스
    new('전설적인_박민수', 'SkillData_원작트리거_전설적인_박민수_Legend15_배리어', '바르토로메오 — 배리어 1/10(550 범위 스턴 2.75초 + 225 범위 150000 + 1500×PV)',
        '바르토로메오 h02Z Legend15 GetRandomInt(1,10)==3: stomp A096(550 범위, 피해 0, adur 2.75) + 대상 중심 225 범위 (150000 + 1500×맞는 적 PV) NORMAL/UNIVERSAL. '
        '한 레벨에 반경이 하나라 550으로(피해 범위 225가 넓어진다 — 피해는 작은 쪽). PV 항은 <200은 100 · 200 · ≥300으로.',
        sat.level_block(triggerChance=0.1, range=550.0, effects=[
            sat.effect(kind=STUN, target=ENEMIES, duration=2.75),
            dmg(ENEMIES, AP, SPELLS, 300000.0, targetCondition=PV_LT, targetConditionValue=200.0),
            dmg(ENEMIES, AP, SPELLS, 450000.0, targetCondition=PV_EQ, targetConditionValue=200.0),
            dmg(ENEMIES, AP, SPELLS, 600000.0, targetCondition=PV_GE, targetConditionValue=300.0)]))
    new('전설적인_박민수', 'SkillData_원작트리거_전설적인_박민수_Legend15_배리어불스', '바르토로메오 — 배리어불스 (9/10)×1/30(225 범위 350000)',
        '바르토로메오 h02Z Legend15: 배리어가 아닐 때 GetRandomInt(1,30)==3 → 0.75초 뒤 대상 중심 225 범위 350000 NORMAL/UNIVERSAL.',
        sat.level_block(triggerChance=0.03, range=225.0, effects=[dmg(ENEMIES, AP, SPELLS, 350000.0)]))

    # 쿠마 h030 · 흰수염 h03B → 최상호
    def kuma(a):
        b = sat.get_effect_blocks(a, 0)
        sat.set_level_field(a, 0, 'range', 500.0)
        sat.set_effects(a, 0, [retarget(b[0], target=ENEMIES), retarget(b[2], target=ENEMIES)])
    edit('SkillData_더미채널_전설적인_최상호_1', '쿠마 압력포: stomp A07K는 500 범위 200000·스턴 0.6초 한 번 — 단일 200000 ×2였다. ', kuma)
    new('전설적인_최상호', 'SkillData_원작트리거_전설적인_최상호_Legend5_지진', '흰수염(전설) — 지진 1/9(600 범위 200000 · 이속 −30% 3초)',
        '흰수염 h03B Legend5 GetRandomInt(1,9)==3: 천둥박수 A07G(ACt2, Ctc1 200000, aare 600, Ctc3 0.3, adur 3.0). 여진 −15%는 미반영.',
        sat.level_block(triggerChance=round(1.0 / 9.0, 6), range=600.0, effects=[dmg(ENEMIES, AP, SPELLS, 200000.0), sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.7, duration=3.0)]))

    def ed51(a):
        b = sat.get_effect_blocks(a, 0)
        single = [i for i, x in enumerate(b) if re.search(r'^\s*target: 3$', x, re.M)]
        assert len(single) == 1
        b[single[0]] = retarget(b[single[0]], multiplier=0.045, bonus=1875000.0)
        sat.set_effects(a, 0, b + [sat.effect(kind=STUN, target=ENEMIES, duration=STOCK_AOWS_ADUR)])
    edit('SkillData_게이트_전설적인_최상호_355f5d39', 'Legend51(LIFE 115): 대상분은 같은 식 ×1.5(1875000 + 최대체력 4.5%) · stomp A098(625 범위, 피해 0, adur 빈칸 = 스톡 3.0 · ahdu 1.65) 스턴 추가. 연구 R01V 계수 미반영. ', ed51)

    # 샹크스 h035 → 백기현: 패기 1/10 stomp 600 범위 125000·스턴 2.0 / A03X는 따로 20% ×10 + 250000·스턴 0.75
    def shanks(a):
        b = sat.get_effect_blocks(a, 0)
        keep = [x for x in b if re.search(r'multiplier: 125000', x) or re.search(r'^\s*-? ?kind: 1$', x, re.M)]
        assert len(keep) == 2
        sat.set_level_field(a, 0, 'range', 600.0)
        sat.set_effects(a, 0, [retarget(x, target=ENEMIES) for x in keep])
    edit('SkillData_원작능력_전설적인_백기현', '패기(Legend10 1/10): stomp A07T는 600 범위 125000·스턴 2.0초 — 단일이었다. 같이 들어 있던 A03X(×10 + 250000)는 별도 강타(20%)라 에셋을 뗐다(0.1로 나가 2배 과소였다). ', shanks)
    new('전설적인_백기현', 'SkillData_원작능력_전설적인_백기현_A03X', 'A03X !패기의 검술 — 20% ×10 + 250000 · 스턴 0.75초',
        '샹크스 h035 uabi A03X(ACbh, Hbh1 20, Hbh2 10, Hbh3 250000, adur 0.75).',
        sat.level_block(triggerChance=0.2, effects=[sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=3, multiplier=10.0, bonus=250000.0),
                                                     sat.effect(kind=STUN, target=SINGLE, duration=0.75)]))

    # 시키 h039 → 신문철: 바닷물 가두기 1/10 stomp A07E 525 범위 145000·스턴 2.75
    def shiki(a):
        sat.set_level_field(a, 0, 'triggerChance', 0.1)
        sat.set_level_field(a, 0, 'range', 525.0)
        sat.set_effects(a, 0, [dmg(ENEMIES, AP, SPELLS, 145000.0), sat.effect(kind=STUN, target=ENEMIES, duration=2.75)])
        sat.set_head_field(a, 'skillName', 'Legend4 바닷물 가두기 — 1/10(525 범위 145000·스턴 2.75초)')
    edit('SkillData_원작능력_전설적인_신문철', 'Legend4: GetRandomInt(1,10)==5 → stomp A07E(525 범위 145000, adur 2.75) — 확률 0.2·단일·스턴 없음이었다. ', shiki)

    # 네코마무시 h09Z → 진연서: neko1은 LIFE 33 & 대상 PV==200일 때만 + 대상 스턴 1.0
    def neko(a):
        sat.set_effects(a, 0, [retarget(b, targetCondition=PV_EQ, targetConditionValue=200.0) for b in sat.get_effect_blocks(a, 0)]
                        + [sat.effect(kind=STUN, target=SINGLE, duration=1.0, targetCondition=PV_EQ, targetConditionValue=200.0)])
    edit('SkillData_게이트_전설적인_진연서_483e0464', 'neko1: 체력==33 그리고 대상 PV==200일 때만 — 효과마다 PV==200 조건 + 대상 파이어볼트 스턴 1.0초. '
         '⚠️ 게이지는 PV 200이 아닌 대상에서도 소모된다(스킬 단위 대상 조건 축 없음 — 범위 효과의 조건은 맞는 적마다라 곁의 PV 200 적만 맞는다). ', neko)

    # 시노부 h042 → 이일중: 1/14 대상 파이어볼트 A0VV 스턴 1.75초(분신은 미반영)
    new('전설적인_이일중', 'SkillData_원작트리거_전설적인_이일중_Legend30_스턴', '시노부(전설) — 1/14 대상 스턴 1.75초',
        '시노부 h042 Legend30 GetRandomInt(1,14)==4: 대상 파이어볼트 A0VV 스턴 1.75초(피해 0). 같이 나오는 분신 h085(2초 소환체)와 B06B 대상 1/4 추가 굴림은 미반영.',
        sat.level_block(triggerChance=round(1.0 / 14.0, 6), effects=[sat.effect(kind=STUN, target=SINGLE, duration=1.75)]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
