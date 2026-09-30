"""주기 피해 지대(SkillEffect.zoneTickInterval) 넣기 — 구조 칸 백로그 8번(2026-09-30 구현담당1, Docs/research/SKILL_STRUCTURE_BACKLOG.md).

원작: 트리거가 만든 더미가 ANpi(영구 이몰레이션)를 가진다. 값은 전부 원본에서 유도한다(표에는 「어느 에셋에 어느 더미를 몇 개」만 적는다):
  틱당 피해 = Eim1 · 틱 간격 = adur · 반경 = aare(더미 uabi 중 기반 ANpi인 능력, war3map_new.w3a)
  수명     = uhpm ÷ |uhpr| − DEATH_LIFE(더미는 체력 uhpm에 재생 −1/초로 스스로 죽는다, war3map_new.w3u)
⚠️ 가정 둘(엔진 동작, 맵 밖 근거 — 틀리면 상수 한 줄):
  · DEATH_LIFE = 0.405: 워크3 유닛은 체력이 0.405 아래로 내려가면 죽는다 → 체력 2짜리 더미 수명 1.595초(2초가 아니다).
  · 첫 틱은 한 간격 뒤(틱 수 = floor(수명 ÷ 간격)). 세우자마자 한 번 친다면 틱이 하나 더 많다(1.595초·0.2초 지대 7 → 8틱).
피해 타입: 더미 능력 피해라 AP·Spells(관례), RRD를 안 거치므로 A11S 감수성 없음(지대 코드가 안 곱한다).
같은 자리에 같은 능력 더미가 여럿이면 hitCount(겹친 지대 수) — 주인만 다른 두 더미(Player(4)·시전자 주인)도 각자 태운다고 본다.
다시 돌려도 같은 결과(설명 꼬리표).
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.dirname(__file__))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import skill_asset_tool as sat  # noqa: E402
import w3a  # noqa: E402
import w3u  # noqa: E402

DEATH_LIFE = 0.405
ENEMIES, AP, SPELLS = 2, 2, 7
TAG = '[지대 09-30]'
TARGET, CASTER = 0, 1

# (에셋, 레벨 → [(더미, 겹친 수, 자리, 간격)], 지울 옛 근사 효과의 multiplier 또는 None, 근거)
TABLE = [
    ('원작017_H095', {0: [('e04F', 6, TARGET, 0)], 1: [('e04F', 7, TARGET, 0)]}, None,
     'Akainu_01_Shoot: 유성(5 + A0HG 레벨)마다 낙하점에 e04F — 낙하점이 대상에서 ±700 안이고 지대 반경이 700이라 대상은 전부에 든다'),
    ('회수_초월_김만경_AD_0ac0451e', {0: [('e0OD', 2, TARGET, 0)]}, None, 'Akainu_02: 대상 자리에 e0OD + e04J(둘 다 A09L)'),
    ('원작트리거_초월_김만경_AD_Akainu_02_LIFE', {0: [('e0OD', 2, TARGET, 0)]}, None, 'Akainu_02: 대상 자리에 e0OD + e04J'),
    ('원작트리거_초월_김만경_AD_Akainu_03', {0: [('e0K4', 2, TARGET, 0)]}, None, 'Akainu_03: 대상 자리에 e0K4 + e046(둘 다 A09L)'),
    ('원작024_h04D', {0: [('e02R', 1, TARGET, 0)], 1: [('e0IS', 3, TARGET, 0), ('e02R', 1, TARGET, 0)]}, None,
     'Dragon_Skill_1: 대상 자리 e02R / _T(레벨 2): e0IS 셋 + e02R'),
    ('게이트_랜덤_한마_바키_355f5d39', {0: [('e0LP', 4, CASTER, 0)]}, None, 'byakuya_Chan: 시전자 300 안 무작위 네 곳에 e0LP(반경 415라 겹친 것으로)'),
    ('게이트_랜덤_한마_바키_9ffa9c45', {0: [('e0LP', 4, CASTER, 0)]}, None, 'byakuya_Chan: e0LP 넷'),
    ('절대쿨_랜덤_한마_바키', {0: [('e0LP', 4, CASTER, 0)]}, None, 'byakuya_Chan: e0LP 넷'),
    ('게이트_변화됨_박은석_8eab1c6f', {0: [('e02Z', 1, TARGET, 0)]}, None, 'Transpom_AceMana: 대상 자리 e02Z'),
    ('게이트_영원_윤현모_d319dd11', {0: [('e0DZ', 1, TARGET, 0)]}, None, 'ace_skill_3: 대상 자리 e0DZ'),
    ('게이트_영원_윤현모_aa9cf1bd', {0: [('e0AM', 1, CASTER, 0)]}, None, 'ace_skill_5: 시전자 자리 e0AM'),
    ('더미채널_영원_윤현모_1', {0: [('e0AI', 3, TARGET, 340)]}, 120000.0,
     'Ace_Attack: 시전자 앞 340·680·1020에 e0AI 셋 — 옛 근사(460 범위 120000 한 번)를 지대로 바꿈'),
    ('게이트_전설적인_임건웅_9ffa9c45', {0: [('e0PU', 3, TARGET, 0)]}, None, 'Legend19_0: 대상 자리 e0PU 둘 + e0PX 하나(셋 다 A08L)'),
    ('게이트_히든_호치킨_21297197', {0: [('e0OD', 1, TARGET, 0)]}, None, 'Akainu_02_hidden: 대상 자리 e0OD'),
]


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


def main(dry):
    A = {a['id']: a for a in w3a.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3a'))}
    U = {u['id']: u for u in w3u.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}

    def zone(eid):
        mo = U[eid]['mods']
        ab = [a for a in str(mo.get('uabi', '')).split(',') if a in A and A[a]['base'] == 'ANpi']
        assert len(ab) == 1, (eid, ab)
        f = {}
        for m in A[ab[0]]['mods']:
            f.setdefault(m['field'], m['value'])
        assert mo.get('uhrt') == 'always' and mo['uhpr'] < 0, eid
        life = mo['uhpm'] / -mo['uhpr'] - DEATH_LIFE
        return ab[0], float(f['Eim1']), round(float(f['adur']), 4), float(f['aare']), round(life, 3)

    changed = []
    for name, levels, drop, why in TABLE:
        a = sat.load(name)
        parts = []
        for lv, zs in sorted(levels.items()):
            blocks = sat.get_effect_blocks(a, lv)
            if drop is not None:
                keep = [b for b in blocks if not re.search(r'^      multiplier: %s$' % re.escape(sat.num(drop)), b, re.M)]
                assert len(keep) == len(blocks) - 1, (name, '옛 근사 효과를 못 찾음')
                blocks = keep
            for eid, count, where, spacing in zs:
                ab, dmg, tick, radius, life = zone(eid)
                blocks.append(sat.effect(kind=0, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=dmg, hitCount=count, duration=life,
                                         zoneTickInterval=tick, zoneRadius=radius, zoneSpacing=spacing, zoneAtCaster=1 if where == CASTER else 0,
                                         skipDamageTakenMultiplier=1))
                ticks = int(life / tick + 1e-6)
                parts.append('L%d %s×%d(%s %g/%.2f초·반경 %g·수명 %.3f초 = %d틱, 지대당 %s)' % (lv, eid, count, ab, dmg, tick, radius, life, ticks, format(int(dmg * ticks), ',')))
            sat.set_effects(a, lv, blocks)
        text = '지대: ' + ' / '.join(parts) + ' — ' + why + '(Tools/apply_damage_zones.py)'
        if note(a, text):
            print('%-44s %s' % (name, ' / '.join(parts)))
            if not dry:
                sat.save(a)
            changed.append(a.path)
    print('바꾼 에셋 %d' % len(changed))


if __name__ == '__main__':
    main('--dry' in sys.argv)
