# -*- coding: utf-8 -*-
"""초월 조합식의 마지막 재료 '박은석'을 초월위습 전용 유닛으로 다시 연결한다.

왜 필요한가
-----------
초월 25종 중 24종의 **마지막** 재료가 '박은석'인데, 이건 일반 유닛 박은석이 아니라
초월 조합 전용 재료다(원작의 "쿠마 초월함 위습"). 사장님 확정:
"박은석 초월위습(원랜디의 쿠마초월함위습)".

그런데 조합식을 처음 생성할 때 generate_recipe_assets.py의 unit_ref()가 이름만 보고
'박은석' → 전설적인_박은석(일반 유닛)으로 이어버렸다. 그래서 24개 초월 조합식이
전부 일반 유닛을 요구하는 상태로 남아 있다. 그걸 여기서 갈아끼운다.

주의: 초월_이태훈_AP
-------------------
이 레시피만 재료 목록에 '박은석'이 **두 번** 들어간다
(박은석 / 서승혁 / 박도진 / 송형성 / 김용태 / 박은석).
생성기가 Counter로 묶어서 `전설적인_박은석 count: 2` 한 줄이 됐는데,
앞의 하나는 일반 유닛이고 뒤의 하나가 초월위습이다.
그래서 이 파일만 count를 1로 줄이고 초월위습 재료 한 줄을 따로 추가한다.

건드리지 않는 것
---------------
같은 GUID를 쓰는 `불멸_박은석`(×1)과 `영원_문필환`(×2)은 사장님 확인 대기 중이라
그대로 둔다. 파일 이름이 초월_로 시작하는 것만 대상으로 삼는 이유가 이것이다.

여러 번 돌려도 안전하다 — 이미 바뀐 파일은 건너뛴다.
"""
import os, re, glob

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
UNITS = os.path.join(ROOT, "Assets/Data/Units/Roster")
RECS = os.path.join(ROOT, "Assets/Data/Recipes")

LEGEND_ASSET = "전설적인_박은석"   # 일반 유닛 (은석가족두목)
WISP_ASSET = "초월위습_박은석"     # 초월 조합 전용 재료

# 재료가 두 번 적힌 레시피 → {일반 유닛으로 남길 개수}. 나머지는 전부 초월위습으로 간다.
KEEP_LEGEND_COUNT = {"초월_이태훈_AP": 1}


def guid_of(asset_name):
    path = os.path.join(UNITS, asset_name + ".asset.meta")
    if not os.path.exists(path):
        raise SystemExit(f"⚠️ {asset_name}.asset.meta 가 없습니다. 에셋을 먼저 만들어야 합니다.")
    return re.search(r"guid: (\w+)", open(path, encoding="utf-8").read()).group(1)


INGREDIENT = re.compile(
    r"  - kind: 0\n"
    r"    unit: \{fileID: 11400000, guid: (\w+), type: 2\}\n"
    r"    item: \{fileID: 0\}\n"
    r"    wildcardGrade: 0\n"
    r"    count: (\d+)\n"
)


def block(guid, count):
    return ("  - kind: 0\n"
            f"    unit: {{fileID: 11400000, guid: {guid}, type: 2}}\n"
            "    item: {fileID: 0}\n"
            "    wildcardGrade: 0\n"
            f"    count: {count}\n")


def main():
    legend, wisp = guid_of(LEGEND_ASSET), guid_of(WISP_ASSET)

    changed, split, skipped = [], [], []

    for path in sorted(glob.glob(os.path.join(RECS, "초월_*.asset"))):
        name = os.path.basename(path)[:-6]
        text = open(path, encoding="utf-8").read()

        if wisp in text:
            skipped.append(name)
            continue

        match = next((m for m in INGREDIENT.finditer(text) if m.group(1) == legend), None)
        if match is None:
            skipped.append(name)   # 유재헌만 박은석을 안 쓴다
            continue

        total = int(match.group(2))
        keep = KEEP_LEGEND_COUNT.get(name, 0)

        if keep >= total:
            raise SystemExit(f"⚠️ {name}: 일반 유닛으로 남길 개수({keep})가 전체({total}) 이상입니다.")

        replacement = block(wisp, total - keep)
        if keep:
            # 원래 자리 순서를 지킨다 — 조합식 표가 재료를 적힌 순서대로 그린다.
            replacement = block(legend, keep) + replacement
            split.append(f"{name} (일반 {keep} + 위습 {total - keep})")
        else:
            changed.append(name)

        open(path, "w", encoding="utf-8").write(
            text[:match.start()] + replacement + text[match.end():])

    print(f"초월위습으로 교체: {len(changed)}개")
    print(f"일반 유닛과 분리: {len(split)}개" + (" — " + ", ".join(split) if split else ""))
    print(f"건너뜀(이미 반영됐거나 박은석을 안 씀): {len(skipped)}개"
          + (" — " + ", ".join(skipped) if skipped else ""))


if __name__ == "__main__":
    main()
