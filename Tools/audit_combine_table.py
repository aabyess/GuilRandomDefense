# -*- coding: utf-8 -*-
"""조합표 이미지의 재료 색과 조합식 에셋의 재료 등급을 대조한다.

이름이 아니라 색이 등급을 정한다(RECIPES_LOW.md). 이름만 옮겨 적으면 "같은 이름 다른 등급"에
그대로 걸린다 — 박은석은 넷, 최상호는 둘이다. 실제로 초월 24종이 전부 엉뚱한 박은석을
가리키고 있었다.

무엇을 비교하나
--------------
줄 하나에서 **쓰인 색의 집합** vs 그 조합식이 참조하는 **유닛 등급의 집합**.
개수(multiset)가 아니라 집합(set)으로 비교하는 이유: 이미지 판독이 한 이름을 둘로 쪼개는
일이 있어서 개수는 못 믿지만, 없는 색을 만들어내지는 않으므로 집합은 믿을 수 있다.
개수·순서·이름 대조는 사람이 확대본을 보고 한다(combine_table_reader.py가 띠를 떨궈준다).

판정 기준
--------
색상각(H)과 밝기(L)로 분류한다. 실측 335개 덩어리가 아래 7계열로 깨끗하게 갈렸다.
**어디에도 안 맞는 색은 억지로 가까운 등급에 밀어 넣지 않고 '판정불가'로 남긴다.**
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from combine_table_reader import IMAGES, read_image  # noqa: E402

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
UNITS = os.path.join(ROOT, "Assets/Data/Units/Roster")
RECIPES = os.path.join(ROOT, "Assets/Data/Recipes")

GRADE_NAME = {0: "흔함", 1: "안흔함", 2: "특별함", 3: "희귀함", 4: "히든", 5: "전설적인",
              6: "제한됨", 7: "초월함", 8: "불멸", 9: "영원함", 10: "랜덤유닛",
              11: "다른세계", 12: "특수함", 13: "초월위습"}

# (색상각 중심, 밝기 범위) → 등급. RECIPES_LOW.md의 색표에 실측값을 붙인 것.
# 하늘색/남색은 색표의 "푸른색=히든" 하나에 둘이 몰려서 어느 쪽인지 확정할 수 없다 → 판정불가.
PALETTE = [
    ("전설적인", (330, 30), (0.25, 0.45)),
    ("특별함", (20, 60), (0.50, 0.75)),
    ("희귀함", (285, 315), (0.15, 0.42)),
    ("변화됨?", (285, 315), (0.55, 0.80)),   # 밝은 핑크 — 색표상 '변화됨'(미정의 등급)
    ("안흔함·흔함", (100, 140), (0.20, 0.55)),
    ("푸른색A(하늘)", (180, 210), (0.55, 0.80)),
    ("푸른색B(남색)", (225, 255), (0.20, 0.45)),
]

UNRESOLVED = {"변화됨?", "푸른색A(하늘)", "푸른색B(남색)"}


def hsl(rgb):
    r, g, b = [v / 255 for v in rgb]
    mx, mn = max(r, g, b), min(r, g, b)
    lightness = (mx + mn) / 2
    if mx == mn:
        return 0.0, 0.0, lightness
    d = mx - mn
    s = d / (2 - mx - mn) if lightness > 0.5 else d / (mx + mn)
    if mx == r:
        h = ((g - b) / d) % 6
    elif mx == g:
        h = (b - r) / d + 2
    else:
        h = (r - g) / d + 4
    return h * 60, s, lightness


def classify(rgb):
    h, _, l = hsl(rgb)
    for name, (h0, h1), (l0, l1) in PALETTE:
        inside = (h0 <= h <= h1) if h0 <= h1 else (h >= h0 or h <= h1)   # 빨강은 0도를 넘나든다
        if inside and l0 <= l <= l1:
            return name
    return None


def recipe_grades(asset_path):
    """조합식 에셋이 재료로 참조하는 유닛들의 등급 이름 집합."""
    text = open(asset_path, encoding="utf-8").read()
    body = text.split("ingredients:", 1)[1].split("goldCost:", 1)[0] if "ingredients:" in text else ""

    grades = []
    for guid in re.findall(r"unit: \{fileID: 11400000, guid: (\w+),", body):
        path = guid_to_asset.get(guid)
        if path is None:
            grades.append("?")
            continue
        m = re.search(r"^  grade: (\d+)", open(path, encoding="utf-8").read(), re.M)
        grades.append(GRADE_NAME.get(int(m.group(1)), m.group(1)) if m else "?")
    return grades


guid_to_asset = {}
for fn in os.listdir(UNITS):
    if not fn.endswith(".asset.meta"):
        continue
    guid = re.search(r"guid: (\w+)", open(os.path.join(UNITS, fn), encoding="utf-8").read()).group(1)
    guid_to_asset[guid] = os.path.join(UNITS, fn[:-5])


# 이미지 줄 순서 → 조합식 에셋. 영원은 이미지가 Save 5회부터 시작하고 0회가 맨 아래라
# recipes_data 순서와 다르다. 그래서 직접 적는다.
ROW_TO_ASSET = {
    "불멸": ["불멸_정윤식", "불멸_김용태", "불멸_정준영", "불멸_박은석",
             "불멸_신지우", "불멸_이승우", "불멸_고도현", "불멸_이이삭"],
    "영원": ["영원_김영원", "영원_조세민", "영원_이지원", "영원_문필환",
             "영원_서민성", "영원_김정래", "영원_윤현모", "영원_최상호"],
    "제한": ["제한_이충민", "제한_최영민", "제한_김강민", "제한_이유범", "제한_전법규",
             "제한_김민규", "제한_임준성", "제한_강보명", "제한_박성호"],
}


def main():
    total = matched = unresolved_rows = 0
    print("=== 줄별 색 집합 vs 에셋 재료 등급 집합 ===\n")

    for image in sorted(IMAGES):
        rows, _ = read_image(image)
        print(f"[{image}]")
        for block, i, band, segs in rows:
            if not segs:
                continue
            names = [classify(c) for c, _ in segs]
            colors = sorted({n for n in names if n})
            unknown = sum(1 for n in names if n is None)

            assets = ROW_TO_ASSET.get(block)
            if assets is None or i >= len(assets):
                print(f"  {block} #{i:<2} 색={colors}"
                      + (f" (분류불가 {unknown}개)" if unknown else "") + "   [에셋 대조 생략]")
                continue

            path = os.path.join(RECIPES, assets[i] + ".asset")
            if not os.path.exists(path):
                print(f"  {block} #{i:<2} ⚠️ 에셋 없음: {assets[i]}")
                continue

            grades = sorted(set(recipe_grades(path)))
            total += 1
            hard = [c for c in colors if c not in UNRESOLVED]
            ok = set(hard) <= set(grades)
            if any(c in UNRESOLVED for c in colors):
                unresolved_rows += 1
            if ok:
                matched += 1
            mark = "OK " if ok else "⚠️ "
            print(f"  {mark}{assets[i]:<14} 이미지색={colors}  에셋등급={grades}")
        print()

    print(f"대조한 줄 {total}개 / 확정 색이 전부 맞는 줄 {matched}개 / "
          f"판정불가 색이 섞인 줄 {unresolved_rows}개")


if __name__ == "__main__":
    main()
