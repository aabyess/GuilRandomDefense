"""작은 축 다섯으로 남아 있던 행 채우기(2026-09-30 구현담당1, PM 지시).

ⓐ 스킬 단위 대상 조건(SkillLevel.primaryTargetCondition) — 「PV 200을 칠 때만 터지고 그때까지 게이지를 들고 있는다」: 아카이누 초월 · 네코마무시 · 우솝 Usop_Skill_2(PV≥200).
ⓑ 대상 조건별 확률 — 레베카 Rebeca_Skill_1(대상 PV 200이면 1/5 · 대상 B06B면 1/5 · 그 밖 1/10): ⓐ + 기존 대상 버프 게이트로 에셋 셋으로 가른다(같은 게이트의 천둥박수 A0QY도).
ⓒ 적 이속을 올리는 손해 오라 — 야마토 A12P(+15%): apply_uabi_passives.py가 넣는다(Slow 배수 1 초과).
ⓓ 연쇄 번개(SkillTargetKind.ChainEnemies) — 희귀 키자루 A09A(AOcl, Ocl1 6000 · Ocl2 8마리 · Ocl3 −0.1 = 튈 때마다 +10% · aare 1250).
ⓔ 맞는 수 상한(SkillEffect.maxTargets) — 마르코 부채꼴 칼날 셋(AEfk Efk3 7): 전설 A08J 1100·400000 · 희귀 A09Y 1150·12000 · 특별 A09X 1150·1500, 시전자 중심.
다시 돌려도 같은 결과.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg  # noqa: E402

ENEMIES, SINGLE, CHAIN = 2, 3, 5
AP, SPELLS = 2, 7
PV_EQ, PV_GE, PV_NE = 2, 3, 4
CASTER = 1
ON_HIT, GAUGE = 0, 3
LIFE = 1
TAG = '[작은 축 09-30]'


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
        if note(a, text + '(Tools/apply_small_axes.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def link(roster, path):
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def primary(c, v=200.0):
        def fn(a):
            sat.set_level_field(a, 0, 'primaryTargetCondition', c)
            sat.set_level_field(a, 0, 'primaryTargetConditionValue', v)
        return fn

    # ⓐ
    edit('SkillData_원작트리거_초월_김만경_AD_Akainu_02_LIFE', 'ⓐ 평타 대상이 PV 200일 때만 판정(primaryTargetCondition) — 아니면 게이지(LIFE 50)를 안 쓰고 들고 있는다. ', primary(PV_EQ))
    # 히든 아카이누(게이트_히든_호치킨_21297197)는 레벨에 requiredTargetBuffId B06B가 걸린 「대상 B06B 갈래」라 PV 조건을 같이 걸지 않는다(걸었다가 되돌림 — 둘 다 채워야 해서 더 좁아졌다).
    edit('SkillData_게이트_전설적인_진연서_483e0464', 'ⓐ neko1: 평타 대상이 PV 200일 때만 판정 — 아니면 게이지(LIFE 33)를 들고 있는다. ', primary(PV_EQ))

    def usop(a):
        primary(PV_GE)(a)
        b = sat.get_effect_blocks(a, 0)
        b[0] = retarget(b[0], targetCondition=0, targetConditionValue=0.0)   # 600 범위 262500은 범위 안 모든 적(조건은 스킬 단위로 올라갔다)
        sat.set_effects(a, 0, b)
    edit('SkillData_게이트_초월_조성진_AD_33bd8aa4', 'ⓐ Usop_Skill_2: 평타 대상이 PV≥200일 때만 판정. 범위 262500에 걸어 뒀던 적별 PV 조건은 뗐다(원문은 범위 안 모든 적). ', usop)

    # ⓑ 레베카: 에셋 둘(Skill_1 본체 · 천둥박수 A0QY)을 각각 셋으로
    for stem, label in (('SkillData_게이트_제한_최영민_33bd8aa4', 'Rebeca_Skill_1'), ('SkillData_더미채널_제한_최영민_79행_02000', 'Rebeca_Skill_1 천둥박수 A0QY')):
        base = sat.load(stem)

        def first(a):
            primary(PV_EQ)(a)
            sat.set_level_field(a, 0, 'triggerChance', 0.2)
        edit(stem, 'ⓑ X6: 게이트가 대상에 따라 갈린다(PV 200이면 1/5 · B06B면 1/5 · 그 밖 1/10) — 이 에셋은 「대상 PV 200: 1/5」 갈래로, 나머지 둘은 _B06B·_일반 에셋. ', first)
        for tail, chance, gate, what in (('_B06B', 0.2, dict(requiredTargetBuffId='B06B'), '대상 B06B(PV 200 아님): 1/5'),
                                         ('_일반', 0.1, dict(forbiddenTargetBuffId='B06B'), '그 밖: 1/10')):
            new_stem = stem + tail
            path = os.path.join(sat.SKILL_DIR, new_stem + '.asset')
            if not os.path.exists(path):
                src = sat.load(stem)
                level = src.levels[0]
                a = sat.new_asset(new_stem, '레베카 %s — %s' % (label, what),
                                  '원작 레베카 h05I %s의 갈래 — %s. 효과는 SkillData_%s와 같다. 2026-09-30 구현담당1(LIMITED_FILL_LIST X6).' % (label, what, stem[10:]), ON_HIT, [level])
                sat.set_level_field(a, 0, 'triggerChance', chance)
                sat.set_level_field(a, 0, 'primaryTargetCondition', PV_NE)
                sat.set_level_field(a, 0, 'primaryTargetConditionValue', 200.0)
                for k, v in gate.items():
                    sat.set_level_field(a, 0, k, v)
                sat.save(a)
                changed += [path, path + '.meta']
            link('제한_최영민', path)

    # ⓓ 희귀 키자루 h01R → 박은석: 연쇄 번개 A09A
    def chain(a):
        sat.set_level_field(a, 0, 'range', 1250.0)
        sat.set_effects(a, 0, [retarget(b, target=CHAIN, maxTargets=8, chainDamageStep=0.1) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_더미채널_희귀함_박은석_1', 'ⓓ 연쇄 번개 A09A(AOcl, Ocl1 6000 · Ocl2 8마리 · Ocl3 −0.1 = 튈 때마다 +10% · aare 1250): 단일 → 연쇄 8마리. ', chain)

    # ⓔ 마르코 부채꼴 칼날(시전자 중심, 최대 7마리)
    def fan(radius):
        def fn(a):
            sat.set_level_field(a, 0, 'range', float(radius))
            sat.set_level_field(a, 0, 'aoeCenter', CASTER)
            sat.set_effects(a, 0, [retarget(b, target=ENEMIES, maxTargets=7) for b in sat.get_effect_blocks(a, 0)])
        return fn
    edit('SkillData_더미채널_희귀함_이승우_1', 'ⓔ 부채꼴 칼날 A09Y(AEfk, Efk1 12000 · aare 1150 · Efk3 7): 단일 → 시전자 중심 1150 범위, 가까운 순 최대 7마리. ', fan(1150))
    edit('SkillData_더미채널_특별함_최준우_1', 'ⓔ 부채꼴 칼날 A09X(AEfk, Efk1 1500 · aare 1150 · Efk3 7): 단일 → 시전자 중심 1150 범위, 가까운 순 최대 7마리. ', fan(1150))
    stem = 'SkillData_원작트리거_전설적인_임채민_Legend17_LifeSkill_칼날'
    path = os.path.join(sat.SKILL_DIR, stem + '.asset')
    if not os.path.exists(path):
        sat.new_asset(stem, '마르코(전설) 깃털폭풍 칼날 A08J — LIFE게이지25(시전자 중심 1100 범위 400000 · 최대 7마리)',
                      '원작 마르코 h02T Legend17_MacroLifeSkill: 더미가 fanofknives A08J(AEfk, Efk1 400000 · aare 1100 · Efk3 7). 2026-09-30 구현담당1(LEGEND_FILL_LIST §0-9).', GAUGE,
                      [sat.level_block(range=1100.0, hitCountThreshold=25, resetTo=1, gaugeKind=LIFE, aoeCenter=CASTER,
                                       effects=[dmg(ENEMIES, AP, SPELLS, 400000.0, maxTargets=7)])])
        changed += [path, path + '.meta']
    link('전설적인_임채민', path)

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
