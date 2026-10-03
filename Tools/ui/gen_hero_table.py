#!/usr/bin/env python3
"""원작 영웅 유닛 표 생성 — 화면 왼쪽 아래 「영웅 단추」 대상(원작에서 영웅 클래스인 유닛에 대응하는 우리 로스터).

근거: war3map_new.w3u에서 유닛 ID가 대문자 H로 시작하고 베이스가 영웅 베이스(대문자 시작: Hvwd·Hpal)인 28종 →
Docs/reference/MASTER_UID_ROSTER_MAP.csv로 우리 로스터 에셋 이름. 출력 Assets/Scripts/Data/UnitHeroTable.cs. 재생성: python3 Tools/ui/gen_hero_table.py
"""
import csv, os, sys
ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'w3x'))
import w3u
W3U = os.path.join(ROOT, 'Tools', 'w3x', '원본', 'war3map_new.w3u')
if not os.path.exists(W3U):
    W3U = os.path.expanduser('~/GitHub/GuilRandomDefense/Tools/w3x/원본/war3map_new.w3u')
heroes = {u['id'] for u in w3u.parse(W3U) if u['id'][0] == 'H' and u['base'][0].isupper()}
assets = {f[:-6] for f in os.listdir(os.path.join(ROOT, 'Assets', 'Data', 'Units', 'Roster')) if f.endswith('.asset')}
names = set()
for row in csv.DictReader(open(os.path.join(ROOT, 'Docs', 'reference', 'MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
    if row['유닛ID'] in heroes and row['로스터'] in assets:
        names.add(row['로스터'])
out = os.path.join(ROOT, 'Assets', 'Scripts', 'Data', 'UnitHeroTable.cs')
with open(out, 'w', encoding='utf-8') as f:
    f.write('using System.Collections.Generic;\n\n// 자동 생성(Tools/ui/gen_hero_table.py) — 원작에서 영웅 클래스(H 아이디 + 영웅 베이스)인 유닛에 대응하는 로스터 에셋 이름. 손으로 고치지 말 것.\n')
    f.write('public static class UnitHeroTable\n{\n    static readonly HashSet<string> Names = new HashSet<string>\n    {\n')
    for n in sorted(names): f.write(f'        "{n}",\n')
    f.write('    };\n\n    public static bool IsHero(UnitData data) => data != null && Names.Contains(data.name);\n}\n')
print(f'원작 영웅 {len(heroes)}종 → 로스터 {len(names)}개')
