#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""히든 레시피 2건(코알라·레베카)의 h08B 자리를 대체 재료로 채운다.

사장님 확정 09번(2026-09-06) 「다른 유닛으로 대체해라」.

**왜 translate_hidden_recipes.py를 다시 안 돌리는가** — 돌려봤고, 되돌렸다.
그 스크립트는 22종을 전부 다시 쓰는데, `used_material_bases`가 반복 도중 누적되는
공유 상태라 **중간에 재료가 2개 늘면 그 뒤 레시피의 fallback 배정이 통째로 밀린다.**
실제로 22개 파일 중 20개가 우리가 안 건드린 자리에서 바뀌었다(재료 유닛이 조용히 교체됨).
뿌리 ⑱ — 생성기가 에셋 트리를 스캔하면 멱등하지 않다.

그래서 이 스크립트는 **커밋된 상태를 정답으로 삼고** 두 파일만 쓴다:
  · 이미 쓰인 재료 = 커밋된 히든 레시피 20종에서 읽는다(두 대상 레시피 자신은 제외)
  · 두 레시피의 재료만 새로 정하고, 나머지 20종은 건드리지 않는다

**대체 재료의 등급을 정한 근거**(원작 히든 23종 재료 등급 구성, Tier 흔함0…전설4):
  3종 레시피 Tier합 6~9 — 다섯 개가 희귀함×3 = 9
  4종 레시피 Tier합 6~11 — 최빈값 10
  · rebecca = 희귀함+희귀함 = 6(3종) → **희귀함**을 더해 9. 희귀함×3 다섯 레시피와 같아진다
  · koalla  = 희귀함+전설적인+안흔함 = 8(4종) → **특별함**을 더해 10. 4종 최빈값에 앉는다
    (희귀함이면 11로 최상위권, 안흔함이면 9로 하위권이 된다)

**어느 유닛인가는 우리가 안 정한다** — h08B는 원작의 DLC 잠금 자리라 원작 쪽 순위 신호가
없다(다른 19종 fallback이 쓴 「원작 표 등장순」조차 없다). 그래서 등급만 위처럼 계산하고,
유닛은 기존 fallback 규칙 그대로 **같은 등급 미사용 유닛 중 DPS 1위**를 쓴다.
규칙을 두 개 만들지 않기 위해서다.
"""
import os, re, sys, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROSTER_DIR = os.path.join(ROOT, 'Assets/Data/Units/Roster')
RECIPE_DIR = os.path.join(ROOT, 'Assets/Data/Recipes')

GRADE_NAME_TO_ENUM = {'흔함': 0, '안흔함': 1, '특별함': 2, '희귀함': 3, '전설적인': 4}

# 원작 재료 (ORIGINAL_HIDDEN_RECIPES.csv 그대로)
TARGETS = {
    '히든_서승혁': dict(trigger='Hidden_koalla', sub_grade='특별함', materials=[
        ('h01O', '비비', '희귀함'), ('h03G', '징베', '전설적인'),
        ('h00O', '니코 로빈', '안흔함'), ('h08B', '확장팩 자리', None)]),
    '히든_여은서': dict(trigger='Hidden_rebecca', sub_grade='희귀함', materials=[
        ('h01V', '바제스', '희귀함'), ('h027', '슈가', '희귀함'),
        ('h08B', '확장팩 자리', None)]),
}

def load_roster():
    import generate_unit_skills_by_gate as gen_gate  # CLAIMED_BASES
    roster = {}
    for f in glob.glob(os.path.join(ROSTER_DIR, '*.asset')):
        base = os.path.basename(f)[:-6]
        t = open(f, encoding='utf-8').read()
        gm = re.search(r'^  grade: (\d+)', t, re.M)
        if not gm:
            continue
        def num(k):
            m = re.search(rf'^  {k}: ([-\d.eE+]+)', t, re.M)
            return float(m.group(1)) if m else 0.0
        guid = re.search(r'guid: ([0-9a-f]{32})',
                         open(f + '.meta', encoding='utf-8').read()).group(1)
        roster[base] = dict(base=base, grade=int(gm.group(1)),
                            dps=num('attackPower') * num('attackSpeed'),
                            claimed=base in gen_gate.CLAIMED_BASES, guid=guid)
    return roster

def main():
    roster = load_roster()
    guid2base = {u['guid']: b for b, u in roster.items()}

    # 커밋된 히든 레시피에서 이미 쓰인 재료 — 대상 2종은 제외(이번에 다시 쓰므로)
    used = set()
    for f in glob.glob(os.path.join(RECIPE_DIR, '히든_*.asset')):
        name = os.path.basename(f)[:-6]
        if name in TARGETS:
            continue
        t = open(f, encoding='utf-8').read()
        if 'ingredients:' not in t:
            continue
        for g in re.findall(r'guid: ([0-9a-f]{32})', t.split('ingredients:')[1]):
            if g in guid2base:
                used.add(guid2base[g])

    # uid → 로스터 (MASTER_UID_ROSTER_MAP.csv)
    uid_map = {}
    with open(os.path.join(ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8') as fh:
        next(fh)
        for line in fh:
            p = line.rstrip('\n').split(',')
            if len(p) >= 2 and p[1]:
                uid_map.setdefault(p[0], p[1])

    def pool_for(grade_name):
        genum = GRADE_NAME_TO_ENUM[grade_name]
        cand = [b for b, u in roster.items()
                if u['grade'] == genum and not u['claimed'] and b not in used]
        return sorted(cand, key=lambda b: -roster[b]['dps'])

    report = []
    for recipe_name, spec in sorted(TARGETS.items()):
        ings, rows = [], []
        for uid, char, grade in spec['materials']:
            if uid == 'h08B':
                grade = spec['sub_grade']
                p = pool_for(grade)
                if not p:
                    print(f'  ⚠️ {recipe_name}: {grade} 등급 가용 로스터 소진 — 중단')
                    return 1
                base, src = p[0], f'⚠️ 창작 — h08B(확장팩 자리) 대체. 등급 {grade}는 원작 재료 구성에서 계산, 유닛은 등급 안 DPS 1위'
            elif uid in uid_map:
                base, src = uid_map[uid], '원작 대응(MASTER_UID_ROSTER_MAP)'
            else:
                p = pool_for(grade)
                if not p:
                    print(f'  ⚠️ {recipe_name}: {grade} 등급 가용 로스터 소진 — 중단')
                    return 1
                base, src = p[0], '등급 안 순위 배정(원작 대응 없음)'
            used.add(base)
            ings.append(base)
            rows.append((uid, char, grade, base, src))

        path = os.path.join(RECIPE_DIR, recipe_name + '.asset')
        t = open(path, encoding='utf-8').read()
        head, tail = t.split('  ingredients:\n', 1)
        tail = re.sub(r'\A(  - kind: 0\n(?:    .*\n)+)+', '', tail)
        blocks = ''.join(
            '  - kind: 0\n'
            f'    unit: {{fileID: 11400000, guid: {roster[b]["guid"]}, type: 2}}\n'
            '    item: {fileID: 0}\n'
            '    wildcardGrade: 0\n'
            '    count: 1\n' for b in ings)
        open(path, 'w', encoding='utf-8').write(head + '  ingredients:\n' + blocks + tail)
        report.append((recipe_name, spec['trigger'], rows))

    for name, trig, rows in report:
        print(f'\n{name}  ({trig})  재료 {len(rows)}종')
        for uid, char, grade, base, src in rows:
            print(f'  {uid:<5} {char:<12} {str(grade):<5} → {base:<28} {src}')
    return 0

if __name__ == '__main__':
    sys.exit(main())
