#!/usr/bin/env python3
"""아이템 도박 체인이 끊긴 데 없이 이어져 있는지 검사한다.

왜 이 파일이 있나
-----------------
2026-09-09 감사에서 아이템 시스템이 **다섯 층 중 네 층**이 막혀 있었다. 그런데
층마다 막힌 이유가 달랐다 — 자산은 멀쩡하고, 배선도 정확하고, 재고 카운터도 도는데,
정작 "그 유닛을 주는 경로"가 0건이라 전체가 안 돌았다. 한 군데만 보고는 알 수 없었다.

층 하나를 고칠 때마다 나머지가 여전히 이어져 있는지 확인해야 해서 이 검사를 남긴다.
🔴 하나라도 빨간불이면 아이템은 **통째로** 안 돈다 — 사슬이라 중간이 끊기면 끝이다.

쓰는 법
-------
    python3 Tools/check_item_chain.py        # 0=정상, 1=끊긴 데 있음
"""

import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()


def guid_of(asset_rel):
    return re.search(r"guid: ([0-9a-f]{32})", read(asset_rel + ".meta")).group(1)


def main():
    rows = []

    def check(passed, label, detail=""):
        rows.append((bool(passed), label, detail))

    mg = read("Assets/Editor/MapGenerator.cs")
    unit_path = re.search(r'VoyageLogUnitPath = "([^"]+)"', mg).group(1)

    # ── 획득 층 ─────────────────────────────────────────────
    check(os.path.exists(os.path.join(ROOT, unit_path)),
          "항해일지가 팔 유닛 자산이 있다", os.path.basename(unit_path))
    check("BuildVoyageLogShop(root.transform" in mg,
          "MapGenerator가 레인마다 항해일지를 짓는다")
    check("LaneShopCount = 8" in mg, "상점 자리 8칸(7번=항해일지)")

    unit = read(unit_path)
    pool_guid = re.search(
        r"sellTriggersItemGamblePool: \{fileID: \d+, guid: ([0-9a-f]{32})", unit)
    pool_path = None
    if pool_guid:
        for meta in glob.glob(os.path.join(ROOT, "Assets/Data/**/*.asset.meta"), recursive=True):
            if pool_guid.group(1) in open(meta, encoding="utf-8").read():
                pool_path = os.path.relpath(meta[:-5], ROOT)
                break
    check(pool_path is not None, "그 유닛을 팔면 도박 풀이 돈다",
          os.path.basename(pool_path) if pool_path else "연결 없음")

    if pool_path:
        item_guids = {guid_of(os.path.relpath(p[:-5], ROOT))
                      for p in glob.glob(os.path.join(ROOT, "Assets/Data/Items/*.asset.meta"))}
        in_pool = item_guids & set(re.findall(r"guid: ([0-9a-f]{32})", read(pool_path)))
        # 원작 전체풀 22종. 축소풀 13종은 그 부분집합이라 유일 개수는 22가 맞다.
        check(len(in_pool) >= 22, "풀에 원작 22종이 다 들어 있다", f"{len(in_pool)}종")

    check("GrantItemGambleStock" in read("Assets/Scripts/Waves/RoundManager.cs"),
          "6·9라운드에 도박 횟수가 늘어난다")

    # ── 플레이어 분리 층 ────────────────────────────────────
    pc = read("Assets/Scripts/Units/PlayerContext.cs")
    check("ItemInventory itemInventory" in pc and "public ItemInventory ItemInventory" in pc,
          "PlayerContext에 플레이어별 인벤토리가 있다")
    check("EnsurePart<ItemInventory>" in mg, "맵 생성이 4명분 인벤토리를 붙인다")
    check("InventoryOf(context)" in read("Assets/Scripts/UI/GameHud.cs"),
          "당첨 아이템이 그 플레이어에게 들어간다")
    check("OwnerItems" in read("Assets/Scripts/Units/CombineSystem.cs"),
          "조합이 그 플레이어 인벤토리에서 재료를 뺀다")

    # ── 원작 대조 ───────────────────────────────────────────
    # 원작 조합 능력 255개 중 아이템을 재료로 요구하는 것은 0개다(w3a의 acat).
    gates = sum(len(re.findall(r"^  - kind: 1", read(os.path.relpath(p, ROOT)), re.M))
                for p in glob.glob(os.path.join(ROOT, "Assets/Data/Recipes/*.asset")))
    check(gates == 0, "아이템을 요구하는 조합식이 없다(원작도 0건)", f"{gates}건")

    count = len(glob.glob(os.path.join(ROOT, "Assets/Data/Items/*.asset")))
    check(count == 38, "아이템 자산이 원작 38종과 같다", f"{count}개")

    print("  아이템 도박 체인")
    print("  " + "─" * 60)
    for passed, label, detail in rows:
        print(f"  {'✅' if passed else '🔴'} {label:<40} {detail}")

    broken = [label for passed, label, _ in rows if not passed]
    if broken:
        print(f"\n  🔴 {len(broken)}곳이 끊겼습니다 — 아이템은 통째로 안 돕니다.")
        return 1

    print("\n  체인이 이어져 있습니다.")
    print("  ⚠️ 씬 반영은 별개다 — Tools > 맵 > 원랜디 맵 생성을 한 번 돌려야 한다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
