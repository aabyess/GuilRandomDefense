#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""마법 평타 유닛에 `attackType: Magic`을 넣는다.

**판단이 아니라 귀결이다.** 원작 배율표에 마법 행은 둘뿐이고(`Magic`·`Spells`),
`Spells`는 능력 피해용이다(도움소가 그 행을 탄다). 따라서 **평타가 AP인 유닛이 탈 행은
`Magic` 하나뿐**이다.

⚠️ 물리(`AD`)와 겸용(`AD+AP`)은 **건드리지 않는다.**
  · `AD`  — 물리 5종(normal/pierce/siege/hero/chaos) 중 무엇인지 아직 안 정했다.
            원작에서 못 가져온다(`UNIT_STATS_RESEARCH.md` §⑤).
  · `AD+AP` — 지금 물리 경로로 흐르므로(`EnemyDummy`가 `type == AP`일 때만 마법 취급)
            물리 행이어야 하는데, 그 물리 행이 위와 같은 이유로 미정이다.
            `Magic`을 넣으면 `DamageTable.RowMatches`가 짝이 안 맞는다고 경고한다.

`Unassigned`는 배율 1.0이라 **정할 때까지 동작이 그대로다.**

⚠️ `generate_unit_stats.py`는 `attackType`을 모른다. 알게 하지 마라 —
`damageType`이 그렇게 덮여서 사고가 났다.
"""
import re, os, sys, collections

AD, AP = 1, 2
MAGIC = 6          # AttackType.Magic

ROSTER = 'Assets/Data/Units/Roster'


def main():
    written = already = skipped = 0
    by_type = collections.Counter()

    for name in sorted(os.listdir(ROSTER)):
        if not name.endswith('.asset'):
            continue
        path = os.path.join(ROSTER, name)
        text = open(path, encoding='utf-8').read()

        m = re.search(r'^  damageType: (\d+)$', text, re.M)
        if not m:
            print(f'  ⚠ {name}: damageType 줄이 없다', file=sys.stderr)
            skipped += 1
            continue
        damage = int(m.group(1))
        by_type[damage] += 1

        if damage != AP:            # 순수 마법만 — 위 주석의 이유
            skipped += 1
            continue
        if 'attackType:' in text:
            already += 1
            continue

        # damageType 바로 다음 줄에 둔다 — 두 필드는 짝으로 읽어야 한다.
        open(path, 'w', encoding='utf-8').write(
            text.replace(m.group(0), f'{m.group(0)}\n  attackType: {MAGIC}', 1))
        written += 1

    print(f'기록 {written}개 / 이미 있음 {already}개 / 대상 아님 {skipped}개')
    print('damageType 분포:', {0: by_type[0], 1: by_type[1], 2: by_type[2], 3: by_type[3]})


if __name__ == '__main__':
    main()
