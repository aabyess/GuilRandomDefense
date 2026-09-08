#!/usr/bin/env python3
"""스킬 밀도 — **양쪽을 같은 자로** 재는 단 하나의 도구.

왜 이 파일이 있나
-----------------
밀도 비교표가 **일곱 번 뒤집혔다.** 매번 원인이 같았다 — 「무엇을 세는가」가 흔들렸다.
2026-09-08에는 이런 것까지 나왔다: 문서가 세는 법을 적어 놨는데(「description에 인용된
능력ID 수」) **그 법으로 그 숫자가 안 나온다**(안흔함 문서값 10 vs 재현 1).

그래서 사람이 세는 걸 그만둔다. 이 스크립트가 유일한 자다.
숫자를 인용할 때는 **이걸 돌린 출력만** 쓴다.

무엇을 세는가 — 딱 세 축, 전부 재현 가능
----------------------------------------
  ① 로스터    : 그 등급의 유닛 수
  ② 보유      : 스킬 자산이 하나라도 배선된 유닛 수
  ③ 효과      : 배선된 자산의 levels[0] 안 effect 개수 (중복 자산은 한 번만)

「효과」를 분자로 쓰는 이유: 능력ID는 상위 등급 자산에만 적혀 있고 하위 셋에는 아예
없다(실측: 희귀함 49자산 중 능력ID 16개, 안흔함 8자산 중 1개). 다섯 등급을 나란히
놓을 수 있는 축은 effect뿐이다.

⚠️ 원작 쪽은 이 스크립트가 안 센다. 원작은 `Tools/w3x/원본/`을 읽어야 하고 세는 법도
다르다(능력ID + 트리거 스킬함수). 원작 값은
`Docs/reference/UPPER_GRADE_RECOUNT_2026-09-08.md`를 보라 — **그 값과 이 출력을 곧바로
나누면 안 된다. 두 축이 다르다.** 배수를 내려면 원작도 effect 상당 단위로 다시 세야 한다.

쓰는 법
-------
    python3 Tools/measure_skill_density.py
    python3 Tools/measure_skill_density.py --detail 안흔함
"""

import argparse
import glob
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ROSTER = ROOT / "Assets/Data/Units/Roster"
SKILLS = ROOT / "Assets/Data/UnitSkills"

# 로스터 파일명 접두어. 이름 문자열이 곧 등급이다(원작도 unam에 등급을 박아 뒀다).
GRADES = ["흔함", "안흔함", "특별함", "희귀함", "전설적인", "초월", "변화됨", "불멸", "영원", "제한", "랜덤"]


def read(p):
    return Path(p).read_text(encoding="utf-8", errors="replace")


def skill_index():
    """스킬 자산 guid → 경로."""
    out = {}
    for q in glob.glob(str(SKILLS / "*.asset")):
        meta = q + ".meta"
        if not os.path.exists(meta):
            continue
        m = re.search(r"guid:\s*([0-9a-f]{32})", read(meta))
        if m:
            out[m.group(1)] = q
    return out


def first_level_effects(text):
    """levels[0]의 effect 개수.

    레벨2는 같은 능력의 강화판이라 따로 안 센다 — 안 그러면 레벨이 있는 스킬만
    두 배로 부풀어 등급 간 비교가 깨진다.
    """
    m = re.search(r"^  levels:\n(.*?)(?=\n  [a-zA-Z]|\Z)", text, re.S | re.M)
    block = m.group(1) if m else ""
    # 레벨 항목은 "  - cooldown:"으로 시작한다. 첫 항목만 남긴다.
    parts = re.split(r"\n  - cooldown:", block)
    first = parts[1] if len(parts) > 1 else block
    return len(re.findall(r"^\s*- kind:", first, re.M))


def measure(grade, idx, detail=False):
    units = sorted(p for p in glob.glob(str(ROSTER / "*.asset"))
                   if os.path.basename(p).startswith(grade + "_"))
    held = 0
    assets = set()
    rows = []
    for p in units:
        t = read(p)
        gs = [g for g in re.findall(r"guid:\s*([0-9a-f]{32})", t) if g in idx]
        if gs:
            held += 1
        assets |= {idx[g] for g in gs}
        rows.append((os.path.basename(p)[:-6], len(gs)))

    effects = sum(first_level_effects(read(a)) for a in assets)

    if detail:
        print(f"\n  [{grade}] 유닛별 배선")
        for n, c in rows:
            print(f"    {n:<26} 스킬 {c}")
        print(f"  [{grade}] 자산별 효과")
        for a in sorted(assets):
            print(f"    {os.path.basename(a)[:58]:<58} {first_level_effects(read(a))}")

    return len(units), held, len(assets), effects


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--detail", help="이 등급의 내역을 펼친다")
    args = ap.parse_args()

    idx = skill_index()
    print(f"  스킬 자산 {len(idx)}개를 색인했다.\n")
    print(f"  {'등급':<8}{'로스터':>6}{'보유':>6}{'자산':>6}{'효과':>6}{'유닛당 효과':>12}")
    print("  " + "─" * 46)

    total = [0, 0, 0, 0]
    for g in GRADES:
        u, h, a, e = measure(g, idx, detail=(args.detail == g))
        if u == 0:
            continue
        total = [total[0] + u, total[1] + h, total[2] + a, total[3] + e]
        print(f"  {g:<8}{u:>6}{h:>6}{a:>6}{e:>6}{(e / u if u else 0):>12.2f}")

    print("  " + "─" * 46)
    print(f"  {'합계':<8}{total[0]:>6}{total[1]:>6}{total[2]:>6}{total[3]:>6}")
    print("\n  ⚠️ 이 숫자를 원작 값과 곧바로 나누지 말 것 — 원작은 다른 축으로 세어져 있다.")
    print("     원작 값: Docs/reference/UPPER_GRADE_RECOUNT_2026-09-08.md")


if __name__ == "__main__":
    main()
