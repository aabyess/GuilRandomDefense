#!/usr/bin/env python3
"""수치 회귀를 잡는 불변식 검사 — **유니티를 안 켜고** 돈다.

왜 이런 형태인가
----------------
2026-09-07에 감속 축(`SlowOnHit`)을 넣다가 적 95종의 이동속도가 전부 22배가 될 뻔했다.
워크3의 절대 하한값 220을 그대로 가져왔는데 우리 `moveSpeed` 단위가 0/7/10이라
`Max(220, 10)`이 220이 돼버린 것이다. **푸시 직전에 눈으로 잡았지 테스트가 잡은 게 아니다.**

EditMode 하네스는 유니티 에디터를 닫아야 돌릴 수 있어서(사장님이 쓰시는 동안 못 돈다)
"고치고 바로 확인"이 안 된다. 그래서 회귀를 잡는 데 필요한 최소한을
**에셋 파일과 소스 텍스트만 읽어서** 검사한다. 언제든 돌릴 수 있는 게 핵심이다.

각 검사는 「무엇이 틀리면 무엇이 망가지는가」를 함께 출력한다.

쓰는 법
-------
    python3 Tools/check_numeric_invariants.py
    python3 Tools/check_numeric_invariants.py --verbose
종료 코드 0=통과, 1=실패.
"""

import argparse
import glob
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

failures = []
notes = []


def read(path):
    return Path(path).read_text(encoding="utf-8", errors="replace")


def field(text, name):
    m = re.search(rf"^\s*{name}:\s*(\S+)", text, re.M)
    return m.group(1) if m else None


def check(ok, title, detail, breaks):
    if ok:
        notes.append(f"✅ {title}")
    else:
        failures.append(f"🔴 {title}\n   실측: {detail}\n   망가지는 것: {breaks}")


# ──────────────────────────────────────────────────────────────────────
def check_move_speed_floor():
    """이동속도 하한은 **비율**이어야 한다 — 절대값을 쓰면 적 전체가 폭주한다."""
    src = read(ROOT / "Assets/Scripts/Units/EnemyDummy.cs")

    m = re.search(r"MoveSpeedFloorRatio\s*=\s*([^;]+);", src)
    if not m:
        check(False, "이동속도 하한 상수", "MoveSpeedFloorRatio를 못 찾음",
              "감속이 하한 없이 0까지 내려가거나, 절대값 하한이 들어와 적이 폭주한다")
        return

    expr = m.group(1).strip()
    try:
        value = eval(expr.replace("f", ""))  # 220f/522f 꼴
    except Exception:
        value = None

    check(value is not None and 0.0 < value < 1.0,
          "이동속도 하한이 비율이다(0~1)",
          f"{expr} = {value}",
          "1보다 크면 Max()가 그 값을 그대로 써서 적 이동속도가 원본(0/7/10)을 덮어쓴다 "
          "— 2026-09-07에 220이 들어와 22배가 될 뻔했다")

    # 실제 데이터로 최악의 경우를 계산한다.
    speeds = set()
    for p in glob.glob(str(ROOT / "Assets/Data/Enemies/**/*.asset"), recursive=True):
        v = field(read(p), "moveSpeed")
        if v is not None:
            try:
                speeds.add(float(v))
            except ValueError:
                pass

    if speeds and value is not None:
        worst = max(speeds)
        # 감속을 100% 걸어도 원본보다 빨라지면 안 된다.
        check(worst * value <= worst,
              "감속을 최대로 걸어도 원본보다 빨라지지 않는다",
              f"적 moveSpeed 종류 {sorted(speeds)} · 하한비율 {value:.4f} → "
              f"최저 {worst * value:.3f} (원본 최대 {worst})",
              "감속이 가속으로 뒤집힌다")


def check_hit_counts():
    """`hitCount`는 1 이상이어야 하고, 터무니없이 크면 안 된다."""
    bad_zero, huge = [], []
    for p in glob.glob(str(ROOT / "Assets/Data/UnitSkills/*.asset")):
        text = read(p)
        for v in re.findall(r"hitCount:\s*(-?\d+)", text):
            n = int(v)
            if n < 1:
                bad_zero.append((Path(p).name, n))
            elif n > 64:
                huge.append((Path(p).name, n))

    check(not bad_zero, "hitCount가 전부 1 이상이다",
          f"{len(bad_zero)}건: {bad_zero[:3]}",
          "0이면 그 효과가 영영 발동하지 않는다(조용한 실패)")
    check(not huge, "hitCount가 64 이하다",
          f"{len(huge)}건: {huge[:3]}",
          "원작 최대 반복은 32회(byakuya_Chan)다 — 그보다 크면 오타를 의심한다")


def check_trigger_chance():
    """`triggerChance`는 0~1 확률이다. 백분율(7 = 7%)을 넣으면 항상 발동한다."""
    bad = []
    for p in glob.glob(str(ROOT / "Assets/Data/UnitSkills/*.asset")):
        text = read(p)
        # ⚠️ 지수 표기(9.5e-05)를 놓치면 "9.5"로 읽혀 오탐이 난다. 유니티는 아주 작은
        #    확률을 이 꼴로 쓴다 — 실제로 1/10×1/33×1/16×1/2 = 9.47e-05 자산이 있다.
        for v in re.findall(r"triggerChance:\s*([\d.]+(?:[eE][-+]?\d+)?)", text):
            f = float(v)
            if f > 1.0:
                bad.append((Path(p).name, f))
    check(not bad, "triggerChance가 전부 0~1이다",
          f"{len(bad)}건: {bad[:3]}",
          "1을 넘으면 확률 판정이 항상 참이라 그 스킬이 매 타 터진다")


def check_buff_gates_have_openers():
    """`requiredBuffId`를 요구하는 스킬은 그 버프를 **거는** 스킬이 있어야 한다.

    없으면 그 스킬은 영영 안 돈다. 아무 오류도 안 난다 — 조용한 실패다.
    """
    required, applied = {}, set()
    for p in glob.glob(str(ROOT / "Assets/Data/UnitSkills/*.asset")):
        text = read(p)
        name = Path(p).name
        # ⚠️ 빈 필드(`requiredBuffId:` 뒤에 아무것도 없음)는 다음 줄의 키 이름을 집는다.
        #    유니티 YAML은 빈 문자열을 값 없이 쓴다 — 버프 ID 모양(영문+숫자)만 받는다.
        def ids(pattern):
            return [b for b in re.findall(pattern, text) if re.fullmatch(r"[A-Za-z]\w{2,}", b)]

        for b in ids(r"requiredBuffId:\s*(\S+)"):
            required.setdefault(b, []).append(name)
        for b in ids(r"\bbuffId:\s*(\S+)"):
            applied.add(b)
        for b in ids(r"selfBuffId:\s*(\S+)"):
            applied.add(b)

    orphans = {b: v for b, v in required.items() if b not in applied}
    check(not orphans, "요구하는 버프마다 거는 쪽이 있다",
          f"{len(orphans)}건: {list(orphans.items())[:3]}",
          "거는 쪽이 없으면 그 게이트를 통과할 방법이 없어 스킬이 영영 안 돈다")


def check_upgrade_multipliers():
    """업그레이드 배수는 1 이상이어야 한다(강화인데 약해지면 안 된다)."""
    bad = []
    for p in glob.glob(str(ROOT / "Assets/Data/UnitUpgrades/*.asset")):
        text = read(p)
        for v in re.findall(r"(?:multiplier|attackMultiplier):\s*([\d.]+)", text):
            f = float(v)
            if 0 < f < 1.0:
                bad.append((Path(p).name, f))
    check(not bad, "업그레이드 배수가 1 이상이다",
          f"{len(bad)}건: {bad[:3]}",
          "1보다 작으면 돈을 내고 약해진다")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    for fn in (check_move_speed_floor, check_hit_counts, check_trigger_chance,
               check_buff_gates_have_openers, check_upgrade_multipliers):
        try:
            fn()
        except Exception as e:
            failures.append(f"🔴 {fn.__name__} 자체가 터졌다: {e}")

    if args.verbose:
        for n in notes:
            print(n)
    else:
        print(f"통과 {len(notes)}건")

    for f in failures:
        print(f)

    print(f"\n{'실패 %d건' % len(failures) if failures else '전부 통과'}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
