# -*- coding: utf-8 -*-
"""상위 등급(초월/히든/불멸/영원/제한/다른세계) 레시피의 재료를
임시 유닛(기본_)에서 실제 등급 유닛으로 다시 연결한다.

사용자 안내: "전설 위 등급은 대부분 전설이지만 희귀함이나 히든 등도 섞여있다"
→ 전설적인을 최우선으로 하고, 없으면 아래 순서로 내려간다.
결과를 전부 출력하니 틀린 것은 사용자가 지적해 수정한다.
"""
import os, re, glob

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
UNITS = os.path.join(ROOT, "Assets/Data/Units/Roster")
RECS  = os.path.join(ROOT, "Assets/Data/Recipes")

PREF = ["전설적인","희귀함","히든","특수함","특별함","안흔함","흔함","랜덤","초월","불멸","영원","제한","다른세계"]
UPPER = ("초월_","히든_","불멸_","영원_","제한_","다른세계_")

# guid → (grade, name) / name → {grade: guid}
guid_of, by_name, guid_info = {}, {}, {}
for f in glob.glob(f"{UNITS}/*.asset"):
    base = os.path.basename(f)[:-6]
    m = re.match(r'^(기본|흔함|안흔함|특별함|희귀함|전설적인|특수함|히든|랜덤|초월|불멸|영원|제한|다른세계)_(.+)$', base)
    if not m: continue
    grade, name = m.group(1), m.group(2)
    name = re.sub(r'_(AD|AP|ADAP)$', '', name)          # 초월 AD/AP 접미사 제거
    g = re.search(r'guid: (\w+)', open(f + ".meta", encoding="utf-8").read()).group(1)
    guid_info[g] = (grade, name)
    by_name.setdefault(name, {})[grade] = g
    # 별칭으로도 찾을 수 있게 한다 — 상위 레시피가 재료를 별칭으로 적는 경우가 있다
    # (예: 히든 황정기의 재료 '윤식파의두뇌' = 희귀함 최상호).
    m2 = re.search(r'^  unitName: (.+)$', open(f, encoding="utf-8").read(), re.M)
    if m2:
        alias = m2.group(1).strip()
        if alias and alias != name:
            by_name.setdefault(alias, {})[grade] = g

changed_files, report, unresolved = 0, [], []

for f in sorted(glob.glob(f"{RECS}/*.asset")):
    base = os.path.basename(f)
    if not base.startswith(UPPER): continue
    txt = open(f, encoding="utf-8").read()
    orig = txt
    for g in re.findall(r'guid: (\w+), type: 2', txt):
        info = guid_info.get(g)
        if not info or info[0] != "기본": continue
        name = info[1]
        cands = by_name.get(name, {})
        pick = next((p for p in PREF if p in cands), None)
        if pick is None:
            unresolved.append((base, name)); continue
        txt = txt.replace(f"guid: {g}, type: 2", f"guid: {cands[pick]}, type: 2")
        report.append((base.replace(".asset",""), name, pick))
    if txt != orig:
        open(f, "w", encoding="utf-8").write(txt)
        changed_files += 1

print(f"수정된 레시피 파일: {changed_files}개")
print(f"재연결된 재료: {len(report)}건\n")

from collections import defaultdict
per_grade = defaultdict(set)
for _, name, grade in report:
    per_grade[grade].add(name)

print("=== 재료를 어느 등급으로 해석했는지 (사용자 확인용) ===")
for grade in PREF:
    if grade in per_grade:
        print(f"\n[{grade}]  {len(per_grade[grade])}종")
        print("  " + ", ".join(sorted(per_grade[grade])))

if unresolved:
    print(f"\n⚠️ 해석 실패 {len(unresolved)}건:")
    for b, n in sorted(set(unresolved)):
        print(f"   {b} ← {n}")
