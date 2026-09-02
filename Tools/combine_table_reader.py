# -*- coding: utf-8 -*-
"""조합표 이미지에서 각 줄의 재료 색을 읽는다.

왜 색인가
--------
`Docs/reference/RECIPES_LOW.md`: **"조합표 이미지에서 재료 이름의 글자 색이 곧 그 재료의 등급"**.
이 프로젝트의 주된 함정이 "같은 이름 다른 등급"이라(박은석이 넷, 최상호가 둘) 이름만 옮겨 적으면
등급이 통째로 어긋난다. 실제로 초월 24종이 전부 잘못된 박은석을 가리키고 있었다.

이 스크립트는 이름을 읽지 않는다 — **재료가 몇 개이고 각각 무슨 색인지**만 뽑는다.
이름 대조는 사람이 눈으로 한다(이미지 확대본을 같이 떨궈준다).

읽는 법
------
1. 표의 가로 구분선을 찾아 줄 경계를 잡는다. 간격이 일정해서 빠진 선은 보간한다.
2. 각 줄의 **첫 텍스트 줄**이 재료 이름줄이다(그 아래는 작은 글씨의 특성 설명).
3. 이름줄을 x축으로 훑어 유채색 덩어리를 재료 하나로 끊는다.
   구분자 '/'와 "+ 나무 5개" 같은 부분은 검정이라 저절로 빠진다.
4. 덩어리마다 대표색을 뽑는다 — 안티앨리어싱으로 밝아진 테두리를 피하려고
   **진한 쪽 20%만** 평균 낸다.
"""
import os
from collections import Counter

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
IMAGE_DIR = os.path.join(ROOT, "Docs/reference/combine-table")

# 이미지마다 재료 칸의 x 범위가 다르다. 세로 구분선을 실측해 넣은 값.
# blocks: (블록 이름, 그 블록이 차지하는 줄 번호 범위) — 한 이미지에 등급이 여럿 들어있다.
IMAGES = {
    "01_초월.png": dict(mat_x=(352, 712), blocks=[("초월", 0, 25)]),
    "02_히든.png": dict(mat_x=(352, 712), blocks=[("히든", 0, None)]),
    "03_불멸_영원_제한.png": dict(mat_x=(348, 710), blocks=[("불멸", 0, 8), ("영원", 8, 16), ("제한", 16, None)]),
    # 위쪽 랜덤유닛 블록은 '재료'가 아니라 특성 설명이라 건너뛴다(랜덤유닛은 조합식이 없다).
    "04_랜덤유닛_다른세계.png": dict(mat_x=(380, 770), blocks=[("다른세계", 14, None)]),
}


def load(name):
    return Image.open(os.path.join(IMAGE_DIR, name)).convert("RGB")


def content_rows(im):
    """유채색 글자가 있는 y 범위 — 표 바깥의 회색 여백을 잘라낸다."""
    W, H = im.size
    ys = [y for y in range(H)
          if any(max(im.getpixel((x, y))) - min(im.getpixel((x, y))) > 60 for x in range(0, W, 3))]
    return min(ys), max(ys)


def horizontal_rules(im, x0, x1, y0, y1, frac=0.85):
    """표의 가로 구분선. 글자와 달리 '균일한 회색이 칸 폭 전체에 걸친 줄'이다."""
    hits = []
    for y in range(y0, y1):
        n = tot = 0
        for x in range(x0, x1, 2):
            r, g, b = im.getpixel((x, y))
            tot += 1
            if max(r, g, b) - min(r, g, b) < 25 and 90 <= (r + g + b) / 3 <= 215:
                n += 1
        if tot and n / tot >= frac:
            hits.append(y)

    merged = []
    for y in hits:
        if merged and y - merged[-1][1] <= 2:
            merged[-1][1] = y
        else:
            merged.append([y, y])
    return [(a + b) // 2 for a, b in merged]


def fill_missing_rules(rules, tolerance=0.35):
    """구분선이 글자에 가려 빠지는 자리가 있다. 간격이 일정해서 보간할 수 있다."""
    if len(rules) < 3:
        return rules, []

    gaps = sorted(rules[i + 1] - rules[i] for i in range(len(rules) - 1))
    pitch = gaps[len(gaps) // 2]

    out, added = [rules[0]], []
    for prev, cur in zip(rules, rules[1:]):
        span = cur - prev
        k = round(span / pitch)
        if k > 1 and abs(span - k * pitch) <= pitch * tolerance:
            for i in range(1, k):
                y = prev + round(span * i / k)
                out.append(y)
                added.append(y)
        out.append(cur)
    return out, added


def is_ink(px):
    r, g, b = px
    return (r + g + b) / 3 < 225


def is_colored(px):
    r, g, b = px
    mx, mn = max(r, g, b), min(r, g, b)
    return mx - mn >= 45 and mx >= 70


def name_line(im, x0, x1, top, bottom):
    """줄 안의 첫 텍스트 밴드 = 재료 이름줄. 그 아래는 작은 글씨의 특성 설명이다."""
    bands, cur = [], None
    for y in range(top + 2, bottom - 1):
        ink = sum(1 for x in range(x0, x1) if is_ink(im.getpixel((x, y))))
        if ink > 3:
            if cur is None:
                cur = [y, y]
            cur[1] = y
        else:
            if cur and cur[1] - cur[0] >= 5:
                bands.append(tuple(cur))
            cur = None
    if cur and cur[1] - cur[0] >= 5:
        bands.append(tuple(cur))
    return bands[0] if bands else None


def is_separator_ink(px):
    """무채색 잉크. 구분자 '/', "+ 나무 5개", 그리고 **검정으로 쓰인 재료 이름**이 전부 여기 걸린다."""
    r, g, b = px
    return max(r, g, b) - min(r, g, b) < 45 and (r + g + b) / 3 < 140


# 검정으로 쓰인 재료가 실제로 있다(초월_양재모의 최상호, 초월_신문철의 임장혁).
# 순수 검정이라 is_colored를 통과하지 못해 예전엔 덩어리 자체가 안 잡혔고, 그 줄을 재료 5개로 읽었다.
# 구분자와 가르는 기준은 **폭**이다 — '/'는 4~6px, 두 음절 이름도 24px는 넘는다.
BLACK_NAME_MIN_WIDTH = 24
BLACK_NAME_GAP = 5          # 한글은 음절 사이가 벌어진다. 그만큼은 이어붙인다.


def black_name_runs(im, x0, x1, ytop, ybot):
    """유채색이 전혀 없는 무채색 글자 덩어리 = 검정으로 쓰인 재료 이름."""
    cols = []
    for x in range(x0, x1):
        chromatic = black = False
        for y in range(ytop, ybot + 1):
            px = im.getpixel((x, y))
            if is_colored(px):
                chromatic = True
            elif is_separator_ink(px):
                black = True
        cols.append(black and not chromatic)

    runs, start, miss = [], None, 0
    for i, on in enumerate(cols):
        if on:
            if start is None:
                start = i
            miss = 0
        elif start is not None:
            miss += 1
            if miss > BLACK_NAME_GAP:
                end = i - miss
                if end - start + 1 >= BLACK_NAME_MIN_WIDTH:
                    runs.append((x0 + start, x0 + end + 1))
                start, miss = None, 0
    if start is not None:
        end = len(cols) - miss - 1
        if end - start + 1 >= BLACK_NAME_MIN_WIDTH:
            runs.append((x0 + start, x0 + end + 1))
    return runs


def segments(im, x0, x1, ytop, ybot, min_colored=8, min_sep=3):
    """이름줄을 재료 하나씩으로 끊는다.

    빈 칸(gap)으로 끊으면 안 된다 — 한글은 음절마다 사이가 벌어져서 '박은석'이 셋으로 쪼개진다.
    실제 경계는 검정으로 찍힌 구분자 '/'다. 그래서 **검정 구간에서만 자른다**.
    맨 뒤의 "+ 나무 5개"는 통째로 검정이라 유채색이 없어서 저절로 빠진다.
    """
    colored, black = [], []
    for x in range(x0, x1):
        c = k = False
        for y in range(ytop, ybot + 1):
            px = im.getpixel((x, y))
            if is_colored(px):
                c = True
            elif is_separator_ink(px):
                k = True
        colored.append(c)
        black.append(k)

    # 유채색이 전혀 없는 검정 구간 = 구분자
    cuts, run = [], None
    for i in range(len(colored)):
        if black[i] and not colored[i]:
            if run is None:
                run = [i, i]
            run[1] = i
        else:
            if run and run[1] - run[0] + 1 >= min_sep:
                cuts.append(tuple(run))
            run = None
    if run and run[1] - run[0] + 1 >= min_sep:
        cuts.append(tuple(run))

    bounds = [0] + [c[1] + 1 for c in cuts] + [len(colored)]
    starts = [0] + [c[0] for c in cuts]

    out = []
    for a, b in zip(bounds[:-1], starts[1:] + [len(colored)]):
        idx = [i for i in range(a, min(b, len(colored))) if colored[i]]
        if len(idx) >= min_colored:
            out.append((x0 + idx[0], x0 + idx[-1] + 1))
    return out


def core_color(im, xa, xb, ytop, ybot):
    """대표색. 안티앨리어싱된 밝은 테두리를 빼려고 진한 쪽 20%만 평균 낸다."""
    px = [im.getpixel((x, y)) for y in range(ytop, ybot + 1) for x in range(xa, xb)
          if is_colored(im.getpixel((x, y)))]
    if not px:
        return None
    px.sort(key=sum)
    keep = px[:max(1, len(px) // 5)]
    return tuple(sum(p[i] for p in keep) // len(keep) for i in range(3))


def read_image(name):
    """→ [(블록명, 줄번호, [(색, x범위), ...]), ...]"""
    cfg = IMAGES[name]
    im = load(name)
    x0, x1 = cfg["mat_x"]

    y0, y1 = content_rows(im)
    rules = horizontal_rules(im, x0, x1, y0 - 40, y1 + 20)
    rules, added = fill_missing_rules(rules)

    # 표 마지막 줄은 아래 테두리가 굵거나 잘려서 구분선으로 안 잡히는 일이 있다.
    # 마지막 선 아래에 아직 글자가 남아 있으면 한 칸 더 있는 것이다.
    if rules:
        pitch = sorted(rules[i + 1] - rules[i] for i in range(len(rules) - 1))[len(rules) // 2] if len(rules) > 2 else 47
        while rules[-1] + 6 < y1:
            nxt = rules[-1] + pitch
            if not any(is_ink(im.getpixel((x, y)))
                       for y in range(rules[-1] + 3, min(nxt, y1)) for x in range(x0, x1, 3)):
                break
            rules.append(min(nxt, y1 + 1))
            added.append(rules[-1])

    rows = []
    for idx, (top, bottom) in enumerate(zip(rules, rules[1:])):
        band = name_line(im, x0, x1, top, bottom)
        if band is None:
            rows.append((idx, None, []))
            continue
        ytop, ybot = band
        segs = [(core_color(im, a, b, ytop, ybot), (a, b))
                for a, b in segments(im, x0, x1, ytop, ybot)]
        segs = [s for s in segs if s[0]]

        # 검정 재료는 유채색 판정을 통과하지 못하므로 따로 찾아 끼워 넣는다. 걸러야 할 것이 셋 있다:
        #  · 유채색 재료가 하나도 없는 줄 — 표 머리글이나 조합식 없는 줄(히든 개 3종)의 설명글이다
        #  · 맨 뒤 "+ 나무 N개" — 마지막 재료보다 오른쪽에 있다
        #  · 유채색 덩어리와 겹치는 구간 — 좁은 구분자들이 이어붙어 생긴 가짜다
        if segs:
            right = segs[-1][1][1]
            for a, b in black_name_runs(im, x0, x1, ytop, ybot):
                if b > right:
                    continue
                if any(a < cb and ca < b for _, (ca, cb) in segs):
                    continue
                segs.append(((0, 0, 0), (a, b)))
            segs.sort(key=lambda s: s[1][0])

        rows.append((idx, (ytop, ybot), segs))

    out = []
    for block, start, end in cfg["blocks"]:
        for idx, band, segs in rows:
            if idx < start or (end is not None and idx >= end):
                continue
            out.append((block, idx - start, band, segs))
    return out, added


if __name__ == "__main__":
    for name in sorted(IMAGES):
        rows, added = read_image(name)
        print(f"\n=== {name} (보간한 구분선 {len(added)}개) ===")
        for block, i, band, segs in rows:
            colors = " ".join(f"{c[0]:3d},{c[1]:3d},{c[2]:3d}" for c, _ in segs)
            print(f"  {block} #{i:<2} y={band}  재료 {len(segs)}개  {colors}")
