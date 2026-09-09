#!/usr/bin/env python3
"""조합판·부스 인형이 서 있는지 **씬 파일만 읽어서** 검사한다.

왜 이 파일이 있나
-----------------
2026-09-09 사장님이 「조합판 코비가 누워있고 안흔함 박민수가 이상함」이라고 하셨다.
그런데 파일만 봐서는 아무 문제가 없었다 — 프리팹 회전 0, 임포트 설정 정상,
원본 FBX의 바인드 포즈도 assimp로 재 보면 멀쩡히 서 있었다(세로가 제일 길고 발이 y=0).

눕힌 것은 **리타게팅된 Idle 자세**였다. 그건 뼈에 구워져 씬에 저장되므로,
씬의 뼈 계층을 월드 좌표로 풀어야만 보인다. 이 스크립트가 그걸 한다.

⚠️ 함정 셋 — 셋 다 2026-09-09에 실제로 밟았다.
  1. **인형 루트**를 기준으로 재야 한다. 안쪽 모델 노드(예: `흔함_문필환`)부터 재면
     모델마다 로컬 스케일이 제각각(0.027 ~ 970배)이라 크기 비교가 통째로 무의미해진다.
     이걸 틀려서 「박민수가 작다」는 잘못된 진단을 냈다.
  2. **자리표시 큐브(`몸`)를 빼야** 한다. 유닛 프리팹에는 모델 말고 큐브도 들어 있어서,
     같이 재면 뼈가 한 점에 모여 있어도 세로가 4~5로 나와 정상처럼 보인다.
  3. **뼈가 실제로 퍼져 있는지 먼저 보라.** 씬에서 뼈가 한 점에 모여 읽히는 모델이 6종
     있는데(김경현·김수빈·신문철·쇼타·로이킴·볼보이), 그걸 그냥 재면 머리와 발 높이가
     같아서 **전부 「누움」으로 잘못 보고된다.** 그건 누운 게 아니라 **못 재는 것**이다.
     이 스크립트는 「⚪ 못 잼」으로 정직하게 표시한다 — 그 6종은 유니티 안에서 봐야 한다
     (Tools > 아트 > 스킨 방향 점검).

쓰는 법
-------
    python3 Tools/check_doll_orientation.py          # 누운 것만
    python3 Tools/check_doll_orientation.py --all    # 전부
"""

import argparse
import codecs
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCENE = os.path.join(ROOT, "Assets/Scenes/SampleScene.unity")

# 인형 루트의 이름 접두어. MapGenerator.PlaceRecipeSlot / BuildCommonBooths가 붙인다.
DOLL_PREFIXES = ("재료_", "결과_", "흔함선택_")


def unescape(s):
    return codecs.decode(s, "unicode_escape") if "\\u" in s else s


def load():
    src = open(SCENE, encoding="utf-8").read()
    names, tr_of_go, go_of_tr, children, trs = {}, {}, {}, {}, {}
    for m in re.finditer(r"--- !u!(\d+) &(\d+)\n(.*?)(?=\n--- |\Z)", src, re.S):
        cls, fid, body = m.group(1), m.group(2), m.group(3)
        if cls == "1":
            n = re.search(r"m_Name: (.*)", body)
            if n:
                names[fid] = unescape(n.group(1).strip().strip('"'))
        elif cls == "4":
            g = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            if g:
                tr_of_go[g.group(1)] = fid
                go_of_tr[fid] = g.group(1)
            cm = re.search(r"m_Children:(.*?)m_Father", body, re.S)
            children[fid] = re.findall(r"- \{fileID: (\d+)\}", cm.group(1)) if cm else []
            p = re.search(r"m_LocalPosition: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}", body)
            r = re.search(r"m_LocalRotation: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+), w: ([-\d.e+]+)\}", body)
            s = re.search(r"m_LocalScale: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}", body)
            if p and r and s:
                trs[fid] = ([float(v) for v in p.groups()],
                            [float(v) for v in r.groups()],
                            [float(v) for v in s.groups()])
    return names, tr_of_go, go_of_tr, children, trs


def qrot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    tx = 2 * (y * vz - z * vy)
    ty = 2 * (z * vx - x * vz)
    tz = 2 * (x * vy - y * vx)
    return [vx + w * tx + (y * tz - z * ty),
            vy + w * ty + (z * tx - x * tz),
            vz + w * tz + (x * ty - y * tx)]


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return [aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz]


def world_positions(root, names, go_of_tr, children, trs):
    """root 아래 모든 노드의 월드 위치 — 부모 TRS를 곱해 내려간다."""
    out = {}
    stack = [(root, [0, 0, 0], [0, 0, 0, 1], [1, 1, 1])]
    while stack:
        tr, wp, wq, ws = stack.pop()
        if tr not in trs:
            continue
        lp, lq, ls = trs[tr]
        rp = qrot(wq, [lp[i] * ws[i] for i in range(3)])
        p = [wp[i] + rp[i] for i in range(3)]
        q = qmul(wq, lq)
        s = [ws[i] * ls[i] for i in range(3)]
        out[names.get(go_of_tr.get(tr), "?")] = p
        for c in children.get(tr, []):
            stack.append((c, p, q, s))
    return out


def pick(bones, want, avoid=()):
    for name, pos in bones.items():
        low = name.lower()
        if any(a in low for a in avoid):
            continue
        if any(w in low for w in want):
            return name, pos
    return None, None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true", help="서 있는 인형까지 전부 보여준다")
    args = ap.parse_args()

    if not os.path.exists(SCENE):
        print(f"  씬 파일이 없습니다: {SCENE}")
        return 2

    names, tr_of_go, go_of_tr, children, trs = load()
    dolls = [(g, n) for g, n in names.items()
             if n.startswith(DOLL_PREFIXES) and not n.endswith("_표식") and g in tr_of_go]

    # 같은 모델이 여러 칸에 서므로 모델(=인형 이름 뒤쪽)별로 한 줄만 남긴다.
    by_model, order = {}, []
    for g, n in dolls:
        nodes = world_positions(tr_of_go[g], names, go_of_tr, children, trs)
        # 자리표시 큐브(`몸`)와 인형 루트 자신은 뼈가 아니다 — 같이 재면 세로가 부풀어
        # 뼈가 한 점에 모여 있어도 정상처럼 보인다.
        bones = {k: v for k, v in nodes.items()
                 if k != "몸" and not k.startswith(DOLL_PREFIXES)}
        if len(bones) < 5:
            continue
        ys = [p[1] for p in bones.values()]
        xs = [p[0] for p in bones.values()]
        zs = [p[2] for p in bones.values()]
        spread = max(max(ys) - min(ys), max(xs) - min(xs), max(zs) - min(zs))

        model = n.split("_")[-1]
        if model not in by_model:
            order.append(model)

        # 🔴 뼈가 퍼져 있지 않으면 방향을 잴 수 없다. 재려고 들면 머리와 발이 같은 높이로
        #    나와 「누움」으로 잘못 보고된다 — 못 재는 것과 누운 것은 다르다.
        if spread < 0.5:
            if by_model.get(model) is None:
                by_model[model] = (None, n, spread)
            continue

        _, head = pick(bones, ("head",), ("nub", "end"))
        _, foot = pick(bones, ("foot", "toe", "ankle"), ("nub", "end"))
        if head is None or foot is None:
            if by_model.get(model) is None:
                by_model[model] = (None, n, spread)
            continue

        ratio = (head[1] - foot[1]) / spread
        prev = by_model.get(model)
        if prev is None or prev[0] is None or ratio < prev[0]:
            by_model[model] = (ratio, n, spread)

    bad = []
    print(f"  조합판·부스 인형 {len(dolls)}개 · 모델 {len(by_model)}종")
    print("  ⚠️ 「머리-발」은 머리와 발의 높이 차를 몸 전체 크기로 나눈 값이다.")
    print("     서 있으면 0.55 이상, 누우면 0 근처(머리가 발보다 아래면 음수).\n")
    unmeasured = []
    print(f"  {'모델':<12}{'머리-발':>9}{'뼈퍼짐':>8}   판정        대표 인형")
    print("  " + "─" * 68)
    for model in sorted(order, key=lambda m: (by_model[m][0] is not None, by_model[m][0] or 0)):
        ratio, sample, spread = by_model[model]
        if ratio is None:
            unmeasured.append(model)
            if args.all:
                print(f"  {model:<12}{'-':>9}{spread:8.2f}   {'⚪ 못 잼':<11} {sample}")
            continue
        if ratio > 0.55:
            mark, ok = "✅ 서 있음", True
        elif ratio < 0.2:
            mark, ok = "🔴 누움", False
        else:
            mark, ok = "🟡 기울어짐", False
        if not ok:
            bad.append((model, ratio))
        if args.all or not ok:
            print(f"  {model:<12}{ratio:9.2f}{spread:8.2f}   {mark:<11} {sample}")

    if unmeasured:
        print(f"\n  ⚪ 뼈로 못 잰 {len(unmeasured)}종: {' · '.join(unmeasured)}")
        print("     씬에서 뼈가 한 점에 모여 읽힌다. **누운 게 아니라 못 재는 것**이다 —")
        print("     유니티 안에서 Tools > 아트 > 스킨 방향 점검으로 봐야 한다.")

    if bad:
        print(f"\n  🔴 {len(bad)}종이 똑바로 안 서 있습니다.")
        print("     고친 뒤에는 Tools > 아트 > 모델 배선 → Tools > 맵 > 원랜디 맵 생성 순서로 다시 돌리세요.")
        return 1

    print("\n  전부 서 있습니다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
