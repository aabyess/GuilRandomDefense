#!/usr/bin/env python3
"""원작 마나 보유 유닛 표 생성 — 초상화 아래 마나 줄을 「원작에서 마나가 있는 유닛만」 보이려는 데이터.

근거: Tools/w3x/원본/war3map_new.w3u의 `umpm`(최대 마나) > 0인 유닛 ID → Docs/reference/MASTER_UID_ROSTER_MAP.csv(원작 유닛ID→우리 로스터)로 옮긴다.
출력: Assets/Scripts/Data/UnitManaTable.cs (UnitData 에셋 이름 집합). 재생성: python3 Tools/ui/gen_mana_table.py
"""
import csv, os, sys
ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import w3u

W3U = os.path.join(ROOT, 'Tools', 'w3x', '원본', 'war3map_new.w3u')
if not os.path.exists(W3U):   # git worktree엔 원본(.gitignore)이 없다 — 주 저장소에서 찾는다
    W3U = os.path.expanduser('~/GitHub/GuilRandomDefense/Tools/w3x/원본/war3map_new.w3u')
mana_ids = {u['id'] for u in w3u.parse(W3U) if (u['mods'].get('umpm') or 0) > 0}
roster_dir = os.path.join(ROOT, 'Assets', 'Data', 'Units', 'Roster')
assets = {f[:-6] for f in os.listdir(roster_dir) if f.endswith('.asset')}
names = set()
missing = []
for row in csv.DictReader(open(os.path.join(ROOT, 'Docs', 'reference', 'MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
    if row['유닛ID'] in mana_ids:
        (names if row['로스터'] in assets else missing).add(row['로스터'])
out = os.path.join(ROOT, 'Assets', 'Scripts', 'Data', 'UnitManaTable.cs')
with open(out, 'w', encoding='utf-8') as f:
    f.write('using System.Collections.Generic;\n\n')
    f.write('// 자동 생성(Tools/ui/gen_mana_table.py) — 원작에서 마나(umpm>0)가 있는 유닛에 대응하는 우리 로스터 에셋 이름. 손으로 고치지 말 것.\n')
    f.write('public static class UnitManaTable\n{\n    static readonly HashSet<string> Names = new HashSet<string>\n    {\n')
    for n in sorted(names):
        f.write(f'        "{n}",\n')
    f.write('    };\n\n    public static bool HasMana(UnitData data) => data != null && Names.Contains(data.name);\n}\n')
print(f'마나 있는 원작 유닛 {len(mana_ids)}종 → 로스터 대응 {len(names)}개 (에셋 없음 {len(missing)}: {sorted(missing)[:5]})')
