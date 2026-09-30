"""장풍 직선(SkillEffect.lineLength) 넣기 — 구조 칸 백로그 5번(2026-09-30 구현담당1, Docs/research/SKILL_STRUCTURE_BACKLOG.md).

원작: 평타 트리거가 시전자 자리에 더미를 세우고 carrionswarm(ACca)을 시전자가 보는 쪽(대상 쪽)으로 쏜다.
값은 더미 능력에서 유도한다: 피해 Ucs1 · 길이 Ucs3 · 시작 반경 aare · 끝 반경 Ucs4(war3map_new.w3a).
표에는 「어느 에셋의 어느 효과(피해 Ucs1로 찾는다)가 어느 능력인가」만 적는다 — 지금까지는 그 효과가 단일 대상이거나 대상 중심 원이었다.
⚠️ 가정(맵 밖 근거 — 틀리면 상수 한 줄):
  · STOCK_ACCA_DIST = 800: Ucs3이 빈칸인 능력(A0A1·A0PV)의 스톡 길이. slk 미확인(AUcs 스톡 거리로 기억하는 값).
  · aare·Ucs4는 폭이 아니라 반경으로 읽는다.
피해 타입은 있던 효과 것을 그대로 둔다(새 에셋 둘은 관례대로 AP·Spells). 최대 피해 Ucs2(7천만~7억)는 닿지 않는 상한이라 안 옮긴다.
안 넣는 것: 안흔함 A0A2(등급 미정) · 에드워드 Ed_Skill_2sc A0OZ(대상 둘레 여러 방향에서 안쪽으로 쏘는 꼴 — 선 하나가 아님) ·
  영원_조세민 lock 에셋(확률 0) · 키드 아이템 갈래 A17P · 히든 A0B5(대응 로스터 없음).
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

STOCK_ACCA_DIST = 800.0
ENEMIES, AP, SPELLS, ON_HIT = 2, 2, 7, 0
TAG = '[직선 09-30]'

# (에셋, 레벨, 능력) — 그 레벨에서 multiplier == Ucs1인 고정 피해 효과를 선으로 바꾼다(여럿이면 단일 대상 것 먼저).
EXISTING = [
    ('게이트_영원_김영원_86ef2902', 0, 'A0OK'),
    ('더미채널_변화됨_박은석_79행_01000', 0, 'A0C4'),
    ('더미채널_불멸_박은석_79행_00625', 0, 'A0S6'),
    ('더미채널_불멸_정윤식_1', 0, 'A002'),
    ('더미채널_영원_윤현모_1', 0, 'A0BL'),
    ('더미채널_영원_최상호_79행_10000', 0, 'A0B6'),
    ('더미채널_제한_강보명_1', 0, 'A0PV'),
    ('더미채널_초월_황준석_ADAP_79행_01000', 0, 'A10Z'),
    ('더미채널_특별함_최동준_1', 0, 'A0BI'),
    ('원작능력_특별함_조세민', 0, 'A0A1'),
    ('원작능력_희귀함_배병규', 0, 'A0BE'),
    ('원작능력_희귀함_서민성', 0, 'A097'),
]
# 빠져 있던 것 — (로스터, 새 에셋 또는 있는 에셋, 레벨, 확률, 능력, 이름, 근거)
MISSING_NEW = [
    ('초월_구주호_AD', '원작트리거_초월_구주호_AD_Kizaru_장풍', 0.1, 'A0BV', '키자루 — 장풍 1/10',
     '원작 키자루 H0B5 Kizaru_Attack: GetRandomInt(1,10)==2 → 시전자 자리 더미 e09R이 carrionswarm A0BV'),
    ('랜덤_모몬가', '원작트리거_랜덤_모몬가_ichigo2', 1.0 / 22, 'A093', '이치고 — 월아천충 1/22',
     '원작 이치고 h071 ichigo1: GetRandomInt(1,22)==3 → ichigo2가 더미 e0MT로 carrionswarm A093 한 번(뒤따르는 e0MT 여섯은 시각)'),
]
# 효과가 비어 있던 레벨에 넣는 것 — (에셋, 레벨, 능력, 근거)
MISSING_LEVEL = [
    ('게이트_초월_임장혁_AD_Luchi_Skill_1_Kick2', 0, 'A0R6', '루치 Luchi_Skill_1_Kick(T특성 없을 때 갈래): 더미 e0F4가 carrionswarm A0R6 — 레벨 1 자리가 비어 있었다'),
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


def setf(block, field, value):
    v = sat.num(value)
    new, n = re.subn(r'^(      %s:) ?.*$' % field, lambda m: m.group(1) + ' ' + v, block, count=1, flags=re.M)
    if n:
        return new
    # 효과 필드 순서에 맞춰 끼운다 — 뒤따르는 필드 중 이미 있는 첫 번째 앞, 없으면 끝.
    for f in sat.EFFECT_FIELDS[sat.EFFECT_FIELDS.index(field) + 1:]:
        m = re.search(r'^      %s:' % f, block, flags=re.M)
        if m:
            return block[:m.start()] + '      %s: %s\n' % (field, v) + block[m.start():]
    return block + '      %s: %s\n' % (field, v)


def main(dry):
    A = {a['id']: a for a in w3a.parse(os.path.join(ROOT, 'Tools/w3x/원본/war3map_new.w3a'))}

    def line(code):
        assert A[code]['base'] == 'ACca', code
        f = {}
        for m in A[code]['mods']:
            f.setdefault(m['field'], m['value'])
        stock = 'Ucs3' not in f
        return float(f['Ucs1']), float(f.get('Ucs3', STOCK_ACCA_DIST)), float(f['aare']), float(f['Ucs4']), stock

    def text(code):
        dmg, length, r0, r1, stock = line(code)
        return '%s 피해 %g · 길이 %g%s · 반경 %g→%g' % (code, dmg, length, '(빈칸 → 스톡 가정)' if stock else '', r0, r1)

    changed = []
    for name, lv, code in EXISTING:
        a = sat.load(name)
        dmg, length, r0, r1, _ = line(code)
        blocks = sat.get_effect_blocks(a, lv)
        g = lambda b, k: (re.search(r'^\s*-? ?%s: ?(.*)$' % k, b, re.M) or [None, '0'])[1].strip()
        cand = [i for i, b in enumerate(blocks) if g(b, 'kind') == '0' and g(b, 'basis') == '0' and abs(float(g(b, 'multiplier')) - dmg) < 0.5]
        assert cand, (name, code, dmg)
        cand.sort(key=lambda i: g(blocks[i], 'target') != '3')
        i = cand[0]
        was = g(blocks[i], 'target')
        b = re.sub(r'^(      target:) .*$', r'\1 %d' % ENEMIES, blocks[i], count=1, flags=re.M)
        for k, v in (('lineLength', length), ('lineStartRadius', r0), ('lineEndRadius', r1)):
            b = setf(b, k, v)
        blocks[i] = b
        sat.set_effects(a, lv, blocks)
        if note(a, '직선: %s — 전에는 %s(Tools/apply_line_skills.py)' % (text(code), '단일 대상' if was == '3' else '대상 중심 원')):
            print('%-44s %s (전: %s)' % (name, text(code), '단일 대상' if was == '3' else '원'))
            if not dry:
                sat.save(a)
            changed.append(a.path)

    def line_effect(code):
        dmg, length, r0, r1, _ = line(code)
        return sat.effect(kind=0, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=dmg, lineLength=length, lineStartRadius=r0, lineEndRadius=r1)

    for name, lv, code, why in MISSING_LEVEL:
        a = sat.load(name)
        if note(a, '직선: L%d %s — %s(Tools/apply_line_skills.py)' % (lv, text(code), why)):
            sat.set_effects(a, lv, sat.get_effect_blocks(a, lv) + [line_effect(code)])
            print('%-44s L%d %s (새 효과)' % (name, lv, text(code)))
            if not dry:
                sat.save(a)
            changed.append(a.path)

    for roster, stem, chance, code, title, why in MISSING_NEW:
        path = os.path.join(sat.SKILL_DIR, 'SkillData_' + stem + '.asset')
        if not os.path.exists(path):
            print('%-44s %s (새 에셋 → %s)' % (stem, text(code), roster))
            if dry:
                continue
            sat.new_asset(stem, '%s(%s)' % (title, text(code)), '%s. %s 2026-09-30 구현담당1(Tools/apply_line_skills.py).' % (why, TAG), ON_HIT,
                          [sat.level_block(triggerChance=round(chance, 6), effects=[line_effect(code)])])
            changed += [path, path + '.meta']
        if not dry and sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))
    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print('  ', os.path.relpath(c, ROOT))


if __name__ == '__main__':
    main('--dry' in sys.argv)
