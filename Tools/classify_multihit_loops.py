#!/usr/bin/env python3
"""다단히트 후보를 「진짜 반복 피해」와 「발사체 이동 애니메이션」으로 가른다.

왜 필요한가
-----------
2026-09-07 조사는 "`if Stage==N` 블록 안에 같은 N으로 `SleepForStage`가 있으면 자기루프"
라는 텍스트 근접성 휴리스틱으로 39건을 뽑았는데, 표본을 열어 보니 대부분이
"발사체가 0.02초 간격으로 위치만 갱신하며 날아가는" 이동 애니메이션이었다.
오탐이 너무 많아 폐기됐고 117건이 [미확인]으로 남았다.

이 스크립트가 다르게 하는 것
---------------------------
텍스트 근접성 대신 **호출 그래프를 펼쳐서** 판정한다.

  ① 액션 함수마다 스테이지 분기(`if Stage==N then ... endif`)를 잘라낸다.
  ② 그 블록에서 도달 가능한 모든 함수를 재귀로 펼친다(헬퍼 `_FuncXXX`까지).
  ③ 그 안에 피해 함수 `RRD`(또는 `UnitDamageTarget`)가 있는지 본다.
  ④ 같은 블록이 자기 스테이지 N으로 되돌아가는 `SleepForStage`를 가지면 자기루프다.

  자기루프 + 피해 있음  → 진짜 반복 피해 (hitCount 축이 필요)
  자기루프 + 피해 없음  → 발사체 이동 (위치 갱신만, hitCount와 무관)

이 맵의 JASS는 조건을 `_FuncXXXC` 헬퍼로 잘게 쪼개 놓아서 텍스트만으로는 안 갈린다.
호출 그래프를 펼치는 것이 그 문제를 정면으로 푸는 방법이다.

쓰는 법
-------
    python3 Tools/classify_multihit_loops.py            # 요약
    python3 Tools/classify_multihit_loops.py --csv out.csv
"""

import argparse
import csv
import re
import sys
from pathlib import Path

SOURCE = Path("Tools/w3x/원본/war3map_new.j")

# 피해를 실제로 주는 함수. RRD가 이 맵의 표준 피해 래퍼고, UnitDamageTarget은 네이티브.
DAMAGE_CALLS = ("RRD", "UnitDamageTarget")

# 위치만 바꾸는 함수 — 발사체 이동의 표지.
MOVE_CALLS = ("SetUnitX", "SetUnitY", "SetUnitPosition", "SetUnitPositionLoc", "KnockBack")

FUNC_RE = re.compile(r"function (\w+) takes .*?returns \w+\n(.*?)\nendfunction", re.S)
CALL_RE = re.compile(r"\b(?:call|=|,|\()\s*(\w+)\s*\(")

# 스테이지 분기 조건. 실제 형태는 괄호 없이 이렇게 생겼다:
#     if s__TrigVariables_Stage[GlobalTV]==1 then
#     elseif s__TrigVariables_Stage[GlobalTV]==2 then
#     if s__TrigVariables_Stage[GlobalTV]>=23 and s__TrigVariables_Stage[GlobalTV]<=31 then
STAGE_COND_RE = re.compile(r"Stage\[[^\]]*\]\s*(==|>=|<=|>|<)\s*(\d+)")

# SleepForStage(tv, dur, stage) — 명시 이동. Next는 +1, Prev는 -1, Add는 +n.
SLEEP_EXPLICIT_RE = re.compile(r"SleepForStage\s*\(([^\n]*)")
SLEEP_NEXT_RE = re.compile(r"SleepForStage(Next|Prev|Add)\s*\(([^\n]*)")


def load_functions(path):
    src = path.read_text(encoding="utf-8", errors="replace")
    return src, {m.group(1): m.group(2) for m in FUNC_RE.finditer(src)}


def reachable_body(name, funcs, seen=None, depth=0):
    """name에서 도달 가능한 모든 함수 본문을 이어 붙여 돌려준다."""
    if seen is None:
        seen = set()
    if name in seen or depth > 12 or name not in funcs:
        return ""
    seen.add(name)
    body = funcs[name]
    out = [body]
    for callee in set(CALL_RE.findall(body)):
        if callee in funcs and callee not in seen:
            out.append(reachable_body(callee, funcs, seen, depth + 1))
    return "\n".join(out)


def inline_conditions(body, funcs):
    """`if(Trig_X_Func002C())then` 의 조건 함수를 본문에 펼쳐 넣는다.

    🔴 이게 2026-09-07 조사가 막힌 지점이다. 이 맵의 JASS는 스테이지 판정을
       `_FuncXXXC` 헬퍼로 빼놔서, 액션 함수 본문만 보면 `Stage==N`이 **한 줄도 없다**
       (Tasigi_03이 정확히 그랬다 — 그래서 텍스트 근접성 휴리스틱이 못 잡았다).
       헬퍼는 `if(not(<조건>))then return false endif return true` 꼴이라 조건을 되뽑을 수 있다.
    """
    def condition_of(name, depth=0):
        if depth > 6 or name not in funcs:
            return None
        m = re.search(r"if\(not\((.*?)\)\)then", funcs[name], re.S)
        if m:
            inner = m.group(1)
            # 조건이 또 다른 헬퍼면 한 번 더 들어간다
            nested = re.fullmatch(r"\s*(\w+C)\(\)\s*", inner)
            if nested:
                return condition_of(nested.group(1), depth + 1)
            return inner
        m = re.search(r"if\(not\s+(\w+C)\(\)\)then", funcs[name])
        if m:
            return condition_of(m.group(1), depth + 1)
        return None

    def repl(m):
        cond = condition_of(m.group(1))
        return f"if {cond} then" if cond else m.group(0)

    return re.sub(r"if\((\w+C)\(\)\)then", repl, body)


def split_stage_blocks(body):
    """if/elseif/else/endif를 제대로 세어 스테이지 분기 블록을 자른다.

    돌려주는 것: [(스테이지번호들, 블록텍스트)]
    범위 조건(>=23 and <=31)이면 그 구간 전체를 한 블록으로 본다.
    """
    lines = body.split("\n")
    blocks = []
    depth = 0            # if 중첩 깊이
    open_at = None       # 현재 스테이지 블록이 열린 깊이
    stages = None
    buf = []

    for line in lines:
        stripped = line.strip()
        is_if = stripped.startswith("if ") or stripped.startswith("if(")
        is_elseif = stripped.startswith("elseif")
        is_else = stripped == "else"
        is_endif = stripped == "endif"

        # 열려 있는 블록을 닫아야 하는가
        if open_at is not None and (
            (is_endif and depth == open_at) or
            ((is_elseif or is_else) and depth == open_at)
        ):
            blocks.append((stages, "\n".join(buf)))
            open_at, stages, buf = None, None, []

        if is_if:
            depth += 1
        elif is_endif:
            depth -= 1

        # 새 스테이지 블록이 열리는가
        if (is_if or is_elseif) and open_at is None:
            found = STAGE_COND_RE.findall(stripped)
            if found:
                nums = []
                for op, num in found:
                    n = int(num)
                    if op == "==":
                        nums.append(n)
                    elif op in (">=", ">"):
                        nums.append(n if op == ">=" else n + 1)
                    elif op in ("<=", "<"):
                        nums.append(n if op == "<=" else n - 1)
                stages = sorted(set(nums))
                open_at = depth if is_if else depth
                buf = [line]
                continue

        if open_at is not None:
            buf.append(line)

    if open_at is not None:
        blocks.append((stages, "\n".join(buf)))
    return blocks


def loop_target(block_text, stages):
    """이 블록이 자기 자신 또는 더 앞 스테이지로 되돌아가면 그 번호를 돌려준다."""
    lo = min(stages) if stages else None
    if lo is None:
        return None

    for m in SLEEP_EXPLICIT_RE.finditer(block_text):
        # SleepForStageNext/Prev/Add는 다른 정규식이 잡는다 — 여기선 건너뛴다.
        if re.match(r"SleepForStage(Next|Prev|Add)", block_text[m.start():m.start() + 24]):
            continue
        parts = [p.strip() for p in m.group(1).split(",")]
        if len(parts) < 3:
            continue
        target = parts[2].rstrip(")").strip()
        if target.isdigit() and int(target) <= max(stages):
            return int(target)
        # Stage[this]+0 / Stage[this] 처럼 제자리
        if re.search(r"Stage\[[^\]]*\]\s*(\+\s*0)?\s*$", target):
            return lo

    for m in SLEEP_NEXT_RE.finditer(block_text):
        if m.group(1) == "Prev":
            return lo - 1
        if m.group(1) == "Add":
            parts = [p.strip() for p in m.group(2).split(",")]
            if len(parts) >= 3 and parts[2].rstrip(")").strip() == "0":
                return lo
    return None


def walk_with_branch(text):
    """(줄, 조건경로)를 내놓는다. `else` 안이면 조건 뒤에 " [아님]"이 붙는다.

    🔴 이게 이 도구의 핵심이다. `else`를 구분하지 않으면 결론이 **정확히 뒤집힌다** —
       2026-09-08 실측: 넷 중 셋에서 RRD가 루프 **밖(exit 분기)**에 있었는데,
       else를 안 보면 "루프 안에서 6번·10번 반복"으로 읽힌다.
    """
    stack = []
    for line in text.split("\n"):
        st = line.strip()
        if st.startswith("if ") or st.startswith("if("):
            stack.append([st[3:].rstrip(" then")[:70], True])
            continue
        if st.startswith("elseif"):
            if stack:
                stack[-1] = [st[7:].rstrip(" then")[:70], True]
            continue
        if st == "else":
            if stack:
                stack[-1][1] = False
            continue
        if st == "endif":
            if stack:
                stack.pop()
            continue
        yield st, [(c, v) for c, v in stack]


def damage_repeats(text, stage, funcs):
    """이 스테이지 블록에서 RRD가 **루프 안에** 있는지, 루프 밖(한 번)인지 가른다.

    돌려주는 것: ("반복피해"|"단발피해"|"발사체이동"|"판정불가", 루프조건 문자열)
    """
    inlined = inline_conditions(text, funcs)

    loop_conds = []      # 루프로 되돌아가는 sleep을 감싼 조건 경로
    damage_conds = []    # RRD를 감싼 조건 경로
    has_move = False

    for st, path in walk_with_branch(inlined):
        if "SleepForStage" in st and re.search(r",\s*%d\s*\)" % stage, st):
            loop_conds.append(path)
        if re.search(r"\b(?:%s)\s*\(" % "|".join(DAMAGE_CALLS), st):
            damage_conds.append(path)
        if any(re.search(r"\b%s\s*\(" % c, st) for c in MOVE_CALLS):
            has_move = True

    if not damage_conds:
        return ("발사체이동" if has_move else "판정불가"), ""

    if not loop_conds:
        return "판정불가", ""

    # 루프 복귀를 감싼 조건과 RRD를 감싼 조건이 **같은 분기**에 있으면 반복 피해다.
    # 서로 반대 분기면(하나가 [아님]) RRD는 루프를 빠져나온 뒤 한 번만 터진다.
    def key(path):
        return {(c, v) for c, v in path}

    for dpath in damage_conds:
        for lpath in loop_conds:
            conflict = False
            dmap = dict(dpath)
            for cond, val in lpath:
                if cond in dmap and dmap[cond] != val:
                    conflict = True
                    break
            if not conflict:
                # ⚠️ 루프 안에 있어도 "카운터가 특정 값일 때만" 때리면 한 번이다.
                #    실측: Dragon_Skill_1_T는 6번 도는 루프 안에서 `integerA==2`일 때만 RRD →
                #    반복이 아니라 "6틱 중 2틱째에 한 방". 이걸 안 가르면 6배로 부풀린다.
                if any(re.search(r"integer[A-Z]\([^)]*\)\s*==\s*\d+", c) and v for c, v in dpath):
                    return "단발피해", "카운터 특정값 게이트: " + "; ".join(
                        f"{c}{'' if v else ' [아님]'}" for c, v in dpath)
                return "반복피해", "; ".join(f"{c}{'' if v else ' [아님]'}" for c, v in lpath)
    return "단발피해", "; ".join(f"{c}{'' if v else ' [아님]'}" for c, v in loop_conds[0])


def classify(path, verbose=False):
    src, funcs = load_functions(path)
    action_names = [n for n in funcs if n.startswith("Trig_") and n.endswith("_Actions")]

    rows = []
    for name in sorted(action_names):
        body = inline_conditions(funcs[name], funcs)
        blocks = split_stage_blocks(body)
        if not blocks:
            continue
        for stages, text in blocks:
            target = loop_target(text, stages)
            if target is None:
                continue
            stage = min(stages)
            # 이 블록에서 호출되는 함수까지 펼쳐서 피해/이동을 본다.
            expanded = [text]
            for callee in set(CALL_RE.findall(text)):
                if callee in funcs:
                    expanded.append(reachable_body(callee, funcs))
            full = "\n".join(expanded)

            damage = sum(len(re.findall(r"\b%s\s*\(" % c, full)) for c in DAMAGE_CALLS)
            move = sum(len(re.findall(r"\b%s\s*\(" % c, full)) for c in MOVE_CALLS)
            verdict_branch, loop_cond = damage_repeats(text, stage, funcs)

            # 반복 상한이 상수인지 가변(레벨 스케일링)인지 — C형 판정의 핵심
            cap = ""
            capm = re.search(r"integer[A-Z]\(GlobalTV\)\s*<\s*\(?([^)\n]{1,60})", inline_conditions(text, funcs))
            if capm:
                cap = capm.group(1).strip()

            verdict = verdict_branch

            rows.append(dict(trigger=name, stage=stage, verdict=verdict,
                             damage=damage, move=move, cap=cap,
                             loop_cond=loop_cond, size=len(text)))
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv")
    ap.add_argument("--source", default=str(SOURCE))
    args = ap.parse_args()

    path = Path(args.source)
    if not path.exists():
        sys.exit(f"원본이 없습니다: {path}  (Tools/w3x/원본/ 은 gitignore 대상입니다)")

    rows = classify(path)

    counts = {}
    for r in rows:
        counts[r["verdict"]] = counts.get(r["verdict"], 0) + 1

    print(f"자기루프 스테이지 블록 {len(rows)}건")
    for k in ("반복피해", "단발피해", "발사체이동", "판정불가"):
        print(f"  {k:<10} {counts.get(k, 0)}")

    triggers = {}
    for r in rows:
        triggers.setdefault(r["trigger"], set()).add(r["verdict"])
    real = sorted(t for t, v in triggers.items() if "반복피해" in v)
    print(f"\n반복 피해를 담은 트리거 {len(real)}개:")
    for t in real:
        print("  ", t)

    if args.csv:
        with open(args.csv, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=["trigger", "stage", "verdict", "damage", "move", "cap", "loop_cond", "size"])
            w.writeheader()
            w.writerows(rows)
        print(f"\n{args.csv} 에 {len(rows)}행을 썼습니다.")


if __name__ == "__main__":
    main()
