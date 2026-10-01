#!/usr/bin/env python3
"""원작 상시 오라 표 만들기 — Docs/research/SPHERE_ART_BY_ROSTER.csv → Assets/Resources/Effects/SphereArtTable.txt

한 줄 = 로스터 ⇥ 아트 키 ⇥ 붙는 곳 ⇥ [위치 x,y,z] ⇥ [회전 x,y,z] ⇥ [크기] ⇥ [항상|공격|스킬]
 · 아트 키 = 원작 파일 이름에서 폴더·확장자를 떼고 소문자(HandsAura2.mdx → handsaura2). Resources/Effects/Sphere/<키>.prefab이 있으면 그 모델.
 · 등급 규칙에 걸리는 행은 뺀다: 접두어 초월_ 의 HandsAura2(A07O), 히든_ 의 BlightwalkerAura(A07N)는 UnitSphereArt가 grade로 붙인다.
   (A07O를 가진 영원·제한 로스터 하나씩은 등급 규칙에 안 걸리므로 그대로 둔다.)
 · 한 능력이 아트 둘·붙는 곳 둘이면 짝지어(0↔0, 1↔1) 두 줄. 아트가 하나뿐이면 붙는 곳 둘 다에 같은 아트.
 · extra.tsv(같은 폴더, 있으면)의 줄은 그대로 덧붙인다 — Blender 부가 이펙트 부품(위치·회전·크기·때)을 손으로 얹는 자리.
쓰기: python3 Tools/sphere_art/build_table.py [출력경로]
"""
import csv, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(ROOT, "Docs", "research", "SPHERE_ART_BY_ROSTER.csv")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Assets", "Resources", "Effects", "SphereArtTable.txt")
EXTRA = os.path.join(HERE, "extra.tsv")

GRADE_RULES = {("A07O", "초월"), ("A07N", "히든")}


def art_key(name):
    base = re.split(r"[\\/]", name.strip())[-1]
    return re.sub(r"\.(mdx|mdl)$", "", base, flags=re.I).lower()


rows, seen = [], set()
for r in csv.DictReader(open(SRC, encoding="utf-8")):
    roster = r["로스터"].strip()
    if (r["능력"], roster.split("_")[0]) in GRADE_RULES:
        continue
    arts = [a for a in r["아트(atat)"].split(",") if a.strip()]
    spots = [s for s in (r["붙는곳0"].strip(), r["붙는곳1"].strip()) if s]
    # 「hand,right」처럼 쉼표가 든 붙는 곳은 CSV 칸에 그대로 들어 있다(칸이 따옴표로 묶임) — 위에서 칸 단위로 읽었으므로 안전.
    for i, spot in enumerate(spots):
        art = arts[i] if i < len(arts) else arts[0]
        row = (roster, art_key(art), spot)
        if row not in seen:
            seen.add(row)
            rows.append(row)

lines = ["# 원작 상시 오라 표 — 생성: Tools/sphere_art/build_table.py (손으로 고치지 말 것, 손으로 얹을 줄은 Tools/sphere_art/extra.tsv)",
         "# 로스터\t아트 키\t붙는 곳\t위치\t회전\t크기\t때"]
lines += ["\t".join(r) for r in rows]
if os.path.exists(EXTRA):
    lines += [l.rstrip("\n") for l in open(EXTRA, encoding="utf-8") if l.strip() and not l.startswith("#")]
os.makedirs(os.path.dirname(OUT), exist_ok=True)
open(OUT, "w", encoding="utf-8").write("\n".join(lines) + "\n")
print(f"{len(rows)}줄 → {OUT}")
