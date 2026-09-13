"""포탈 마법진 3종 v3 — 순수한 빛의 다층 마법진(PM 2026-09-13, 사장님 「장송의 프리렌 느낌 나게, 디테일하게」). blender 세션.
🔴 저작권: 특정 작품의 마법진을 옮기지 않는다 — 가는 빛 선 여러 겹·문자 띠·다각별·위성 원·떠 있는 층이라는 문법만 빌려 문양은 새로 짠다.
v2(돌판에 굵은 선)는 방향이 달라 폐기.

화면 없이(정본):  blender --background --factory-startup --python gen_magic_circles.py [-- 포탈_마법진_스토리 ...]
사장님 창(보여 주기만): import gen_magic_circles as mc; mc.build_in_window(이름, 컬렉션, 텍스처 폴더, 위치)

규격(PM, 유니티 코드와 약속)
| 파일 | 색 | 쓰임 |
| 포탈_마법진_스토리 | 흰 금빛 | 레인→스토리존 |
| 포탈_마법진_뽑기   | 흰 보랏빛 | 뽑기 섬(지름 6.5로 작게 수십 개) |
| 포탈_마법진_복귀   | 흰 하늘빛 | 스토리존→레인(가장 화려) |
- 지름 10(게임 단위), 원점 바닥 가운데. 최상위 = 빈 오브젝트(파일 이름), 자식 메시는 피벗 (0,0,0):
    마법진_바닥_문자띠_회전_시계        z 0.03  r 3.55~5.00  문자 띠 2줄(복귀 3줄)·이중 테두리·눈금·마디 원        텍스처 4096
    마법진_바닥_기하_회전_반시계        z 0.03  r 0~3.55     8각별·7각별 겹침·교차 호·기준선·위성 원(안에 작은 문양) 텍스처 2048
    마법진_중심                        z 0.05  r 0~1.00     안 도는 가운데 문양                                   텍스처 1024
    마법진_떠있는고리_아래_회전_반시계  z 0.35  r 2.95~4.25  문자 띠·이중 테두리·마디                             텍스처 4096
    마법진_떠있는고리_위_회전_시계      z 0.80  r 1.95~2.85  작은 문자·위성 점·눈금                               텍스처 2048
    빛_자리_01                          (0, 0, 1.0)
- 재질 `포탈_마법진_<색>_<층>_발광_잎카드`(알파 컷 + 발광). 유니티 임포터는 알파 컷만 — 부드러운 번짐 대신 선 옆에 더 넓고
  어두운 색 발광 띠(halo)를 한 번 더 그린다. 선 가운데는 거의 흰빛, 번짐 띠에만 색이 확실히.
- 🔸 게임 거리(마법진 폭 화면 약 120px): 머리카락 선은 사라져도 되지만 원·별·문자 띠 윤곽은 읽혀야 한다 → 윤곽 요소만 굵은 halo를 준다.
- 삼각형 파일당 1,000 이하, 전체 높이 ≤ 1.2. 층마다 UV = 위에서 본 평면 투영(그 층 바깥 반지름에 맞춤).
좌표는 게임 단위로 짓고 1/11.4로 줄여 m(FBX global_scale 11.4). 문양은 numpy로 그린다.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")
RADIUS = 5.0
TRI_LIMIT = 1000
HEIGHT_LIMIT = 1.2
SOCKET = ("빛_자리_01", (0.0, 0.0, 1.0))
# 층: 자식 이름 → (재질 꼬리, z, 안 반지름, 바깥 반지름, 조각 수, 고리 수, 텍스처 크기)
LAYERS = {
    "마법진_바닥_문자띠_회전_시계": ("문자띠", 0.03, 3.55, 5.00, 64, 1, 4096),
    "마법진_바닥_기하_회전_반시계": ("기하", 0.03, 0.00, 3.55, 48, 2, 2048),
    "마법진_중심": ("중심", 0.05, 0.00, 1.00, 32, 1, 1024),
    "마법진_떠있는고리_아래_회전_반시계": ("고리아래", 0.35, 2.95, 4.25, 64, 1, 4096),
    "마법진_떠있는고리_위_회전_시계": ("고리위", 0.80, 1.95, 2.85, 48, 1, 2048),
}
VARIANTS = {
    "포탈_마법진_스토리": dict(color_key="금빛", tint=(1.00, 0.78, 0.40), seed=11, sats=6, bands=2, glyphs=(132, 96, 0), ring_glyphs=84,
                           emblem="eye", level=0),
    "포탈_마법진_뽑기": dict(color_key="보랏빛", tint=(0.76, 0.52, 1.00), seed=23, sats=7, bands=2, glyphs=(140, 100, 0), ring_glyphs=90,
                          emblem="squares", level=1),
    "포탈_마법진_복귀": dict(color_key="하늘빛", tint=(0.50, 0.80, 1.00), seed=37, sats=8, bands=3, glyphs=(150, 108, 72), ring_glyphs=100,
                          emblem="spiral", level=2),
}
# 선 굵기(게임 단위). 머리카락 HAIR는 가까이서만, 윤곽 MAIN + 번짐 HALO는 멀리서도 남는다.
HAIR, MAIN, GLYPH = 0.010, 0.026, 0.013
# 🔴 1차(0.15·0.030·0.034, 번짐 색 = 색×0.58)는 가까이서 번짐 띠가 굵은 파스텔 관으로 보여 「굵은 네온」으로 돌아갔다 —
#    폭을 줄이고 번짐 색을 어둡고 짙게. 0.11도 게임 거리(폭 120px)에서 윤곽 한 픽셀 남짓이라 원·별은 읽힌다.
HALO_MAIN, HALO_HAIR, HALO_GLYPH = 0.11, 0.022, 0.022


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


class Layer:
    """반지름 R 정사각 캔버스(게임 단위 좌표). core = 흰 가운데 선, halo = 색 번짐 띠."""

    def __init__(self, radius, n):
        self.R, self.n = radius, n
        self.px = 2.0 * radius / n
        c = ((np.arange(n, dtype=np.float32) + 0.5) * self.px - radius)
        self.xs = c
        self.rad = np.hypot(c[None, :], c[:, None]).astype(np.float32)
        self.core = np.zeros((n, n), dtype=np.float32)
        self.halo = np.zeros((n, n), dtype=np.float32)

    def _put(self, sel, dist, width, halo):
        if width:
            np.maximum(self.core[sel], _smooth(width / 2 + self.px, width / 2 - self.px, dist), out=self.core[sel])
        if halo:
            np.maximum(self.halo[sel], _smooth(halo / 2 + self.px, halo / 2 - self.px, dist), out=self.halo[sel])

    def ring(self, r0, width=HAIR, halo=HALO_HAIR):
        pad = max(width, halo) / 2 + 2 * self.px
        lo, hi = max(r0 - pad, 0.0), r0 + pad
        rows = np.nonzero((np.abs(self.xs) <= hi))[0]
        if len(rows) == 0:
            return
        sel = (slice(rows[0], rows[-1] + 1), slice(rows[0], rows[-1] + 1))
        d = np.abs(self.rad[sel] - r0)
        self._put(sel, d, width, halo)

    def polyline(self, pts, width=HAIR, halo=HALO_HAIR, closed=False):
        pts = np.asarray(pts, dtype=np.float32)
        if closed:
            pts = np.vstack([pts, pts[:1]])
        pad = max(width, halo) / 2 + 2 * self.px
        x0, y0 = pts.min(0) - pad
        x1, y1 = pts.max(0) + pad
        c0 = max(int((x0 + self.R) / self.px), 0)
        c1 = min(int((x1 + self.R) / self.px) + 1, self.n)
        r0 = max(int((y0 + self.R) / self.px), 0)
        r1 = min(int((y1 + self.R) / self.px) + 1, self.n)
        if c1 <= c0 or r1 <= r0:
            return
        sel = (slice(r0, r1), slice(c0, c1))
        X = self.xs[c0:c1][None, :]
        Y = self.xs[r0:r1][:, None]
        d = np.full((r1 - r0, c1 - c0), 1e9, dtype=np.float32)
        a, b = pts[:-1], pts[1:]
        for (ax, ay), (bx, by) in zip(a, b):
            dx, dy = bx - ax, by - ay
            L2 = max(dx * dx + dy * dy, 1e-12)
            t = np.clip(((X - ax) * dx + (Y - ay) * dy) / L2, 0.0, 1.0)
            np.minimum(d, np.hypot(X - (ax + t * dx), Y - (ay + t * dy)), out=d)
        self._put(sel, d, width, halo)

    def circle(self, cx, cy, r, width=HAIR, halo=HALO_HAIR, steps=None):
        steps = steps or max(12, int(r * 90))
        th = np.linspace(0, math.tau, steps + 1)
        self.polyline(np.stack([cx + r * np.cos(th), cy + r * np.sin(th)], 1), width, halo)

    def dot(self, x, y, r, halo=HALO_HAIR):
        self.polyline([(x, y), (x, y)], 2 * r, halo)

    def rgba(self, tint):
        tint = np.asarray(tint, dtype=np.float32)
        core_col = np.float32(0.80) + np.float32(0.20) * tint             # 가운데는 거의 흰빛에 색 기운만
        halo_col = tint ** np.float32(1.5) * np.float32(0.5)                # 번짐은 어둡고 채도를 올려 색이 확실히
        core = self.core[..., None]
        col = halo_col[None, None, :] * (1 - core) + core_col[None, None, :] * core
        alpha = np.maximum(self.core, self.halo)
        return np.dstack([col, alpha]).astype(np.float32)


# ──────────────────────────────────────────────────────────── 문양 조각

def _bez(p0, p1, p2, n=9):
    t = np.linspace(0, 1, n)[:, None]
    return (1 - t) ** 2 * np.asarray(p0) + 2 * (1 - t) * t * np.asarray(p1) + t * t * np.asarray(p2)


def glyph(rng):
    """가상의 필기체 한 글자(글자 칸 u∈[−½,½], v∈[−½,½]) — 휜 줄기 + 갈고리·곡선 가지 1~2 + 가끔 작은 고리·점. 실제 문자 아님."""
    strokes, dots = [], []
    u0 = rng.uniform(-0.18, 0.18)
    lean = rng.uniform(-0.25, 0.25)
    strokes.append(_bez((u0 - lean * 0.5, -0.5), (u0 + rng.uniform(-0.35, 0.35), 0.0), (u0 + lean * 0.5, 0.5)))
    for _ in range(rng.integers(1, 3)):
        v = rng.uniform(-0.35, 0.35)
        side = rng.choice((-1.0, 1.0))
        strokes.append(_bez((u0, v), (u0 + side * 0.45, v + rng.uniform(-0.4, 0.4)), (u0 + side * rng.uniform(0.15, 0.45), v + rng.uniform(-0.45, 0.45))))
    if rng.random() < 0.35:
        cv_ = rng.choice((-0.42, 0.42))
        th = np.linspace(0, math.tau, 13)
        strokes.append(np.stack([u0 + 0.12 * np.cos(th), cv_ + 0.1 * np.sin(th)], 1))
    if rng.random() < 0.45:
        dots.append((u0 + rng.uniform(-0.3, 0.3), rng.choice((-0.62, 0.62))))
    return strokes, dots


def text_band(layer, r_mid, height, count, seed, inward=False, width=GLYPH):
    rng = np.random.default_rng(seed)
    step = math.tau / count
    gw = min(height * 0.55, r_mid * step * 0.8)
    for i in range(count):
        th = i * step
        strokes, dots = glyph(rng)
        rad = np.array((math.cos(th), math.sin(th)))
        tan = np.array((-math.sin(th), math.cos(th)))
        sgn = -1.0 if inward else 1.0

        def place(uv):
            uv = np.atleast_2d(uv)
            return rad * (r_mid + sgn * uv[:, 1:2] * height) + tan * (sgn * uv[:, 0:1] * gw)

        for s in strokes:
            layer.polyline(place(s), width, HALO_GLYPH)
        for d in dots:
            p = place(np.array(d))[0]
            layer.dot(p[0], p[1], width * 0.9, HALO_GLYPH)


def double_border(layer, r_out, gap, main=True):
    w, h = (MAIN, HALO_MAIN) if main else (HAIR, HALO_HAIR)
    layer.ring(r_out, w, h)
    layer.ring(r_out - gap, HAIR, HALO_HAIR)


def ticks(layer, r0, r1, count, long_every=10, width=HAIR):
    for i in range(count):
        th = math.tau * i / count
        rr1 = r1 + (r1 - r0) * 0.6 if long_every and i % long_every == 0 else r1
        layer.polyline([(r0 * math.cos(th), r0 * math.sin(th)), (rr1 * math.cos(th), rr1 * math.sin(th))], width, 0)


def star(layer, r, n, k, rot, width=MAIN, halo=HALO_MAIN):
    pts = [(r * math.cos(rot + math.tau * i / n), r * math.sin(rot + math.tau * i / n)) for i in range(n)]
    for i in range(n):
        layer.polyline([pts[i], pts[(i + k) % n]], width, halo)


def satellite(layer, cx, cy, r, rng, level):
    layer.circle(cx, cy, r, MAIN, HALO_MAIN * 0.6)
    layer.circle(cx, cy, r * 0.78, HAIR, 0)
    kind = rng.integers(0, 3)
    if kind == 0:                                           # 작은 삼각별
        pts = [(cx + 0.55 * r * math.cos(a), cy + 0.55 * r * math.sin(a)) for a in np.linspace(math.pi / 2, math.pi / 2 + math.tau, 4)]
        layer.polyline(pts, HAIR, 0)
        layer.polyline([(cx + 0.55 * r * math.cos(a), cy + 0.55 * r * math.sin(a)) for a in np.linspace(-math.pi / 2, -math.pi / 2 + math.tau, 4)], HAIR, 0)
    elif kind == 1:                                         # 십자 + 원
        layer.polyline([(cx - 0.6 * r, cy), (cx + 0.6 * r, cy)], HAIR, 0)
        layer.polyline([(cx, cy - 0.6 * r), (cx, cy + 0.6 * r)], HAIR, 0)
        layer.circle(cx, cy, r * 0.32, HAIR, 0)
    else:                                                   # 점 고리
        for a in np.linspace(0, math.tau, 9)[:-1]:
            layer.dot(cx + 0.5 * r * math.cos(a), cy + 0.5 * r * math.sin(a), 0.012, 0)
    layer.dot(cx, cy, 0.02, HALO_HAIR)
    if level >= 2:                                          # 복귀: 위성마다 눈금 한 겹
        for a in np.linspace(0, math.tau, 17)[:-1]:
            layer.polyline([(cx + 0.86 * r * math.cos(a), cy + 0.86 * r * math.sin(a)), (cx + 0.95 * r * math.cos(a), cy + 0.95 * r * math.sin(a))], HAIR * 0.8, 0)


def draw_text_layer(cfg, n):
    L = Layer(5.0, n)
    rng_seed = cfg["seed"]
    double_border(L, 4.96, 0.07)                            # 가장 바깥 이중 테두리(좁은 띠)
    ticks(L, 4.74, 4.83, 360, 10)
    for i in range(8):                                      # 테두리 마디 원
        th = math.tau * i / 8 + math.pi / 8
        L.circle(4.91 * math.cos(th), 4.91 * math.sin(th), 0.085, HAIR, HALO_HAIR)
        L.dot(4.91 * math.cos(th), 4.91 * math.sin(th), 0.02)
    double_border(L, 4.68, 0.04)
    text_band(L, 4.39, 0.36, cfg["glyphs"][0], rng_seed)     # 바깥 문자 띠(120자 이상, 바로 선 글자)
    double_border(L, 4.10, 0.035)
    text_band(L, 3.88, 0.20, cfg["glyphs"][1], rng_seed + 1, inward=True)   # 둘째 띠: 작고 거꾸로
    L.ring(3.72, MAIN, HALO_MAIN)
    if cfg["bands"] >= 3:                                   # 복귀: 셋째 띠 대신 점·짧은 획 줄
        for i in range(cfg["glyphs"][2]):
            th = math.tau * (i + 0.5) / cfg["glyphs"][2]
            r = 3.63
            if i % 3:
                L.dot(r * math.cos(th), r * math.sin(th), 0.014)
            else:
                L.polyline([((r - 0.05) * math.cos(th), (r - 0.05) * math.sin(th)), ((r + 0.05) * math.cos(th), (r + 0.05) * math.sin(th))], HAIR, 0)
    L.ring(3.58, HAIR, 0)
    return L


def draw_geometry_layer(cfg, n):
    L = Layer(3.55, n)
    rng = np.random.default_rng(cfg["seed"] + 5)
    star(L, 3.45, 8, 3, math.pi / 8)                        # 8각별
    star(L, 3.20, 7, 3, math.pi / 2, MAIN * 0.8, HALO_MAIN * 0.7)   # 7각별 겹침
    star(L, 3.20, 7, 2, math.pi / 2, HAIR, 0)
    L.ring(3.45, HAIR, 0)
    L.ring(3.20, HAIR, 0)
    for i in range(16):                                     # 기준선
        th = math.tau * i / 16
        L.polyline([(1.12 * math.cos(th), 1.12 * math.sin(th)), (3.40 * math.cos(th), 3.40 * math.sin(th))], HAIR * 0.8, 0)
    ns = cfg["sats"]
    for i in range(ns):                                     # 교차 호: 원점을 지나는 원 여럿
        th = math.tau * i / ns
        L.circle(1.75 * math.cos(th), 1.75 * math.sin(th), 1.75, HAIR, 0, steps=220)
    for i in range(ns):                                     # 위성 원
        th = math.tau * i / ns + math.pi / ns
        satellite(L, 2.72 * math.cos(th), 2.72 * math.sin(th), 0.42, rng, cfg["level"])
    double_border(L, 1.36, 0.045)
    ticks(L, 1.14, 1.24, 96, 8)
    L.ring(1.06, HAIR, HALO_HAIR)
    return L


def draw_center_layer(cfg, n):
    L = Layer(1.0, n)
    L.ring(0.96, MAIN, HALO_MAIN * 0.6)
    L.ring(0.90, HAIR, 0)
    e = cfg["emblem"]
    if e == "eye":                                          # 스토리: 두 호로 된 눈 + 눈동자 원 + 햇살 12
        for sgn in (1, -1):
            L.polyline(_bez((-0.62, 0.0), (0.0, sgn * 0.52), (0.62, 0.0), 40), MAIN, HALO_MAIN * 0.5)
        L.circle(0, 0, 0.2, HAIR, HALO_HAIR)
        L.dot(0, 0, 0.07)
        for i in range(12):
            th = math.tau * i / 12
            L.polyline([(0.70 * math.cos(th), 0.70 * math.sin(th)), (0.84 * math.cos(th), 0.84 * math.sin(th))], HAIR, 0)
    elif e == "squares":                                    # 뽑기: 엇갈린 사각 둘 + 마름모 + 네 점
        for rot in (0.0, math.pi / 4):
            pts = [(0.62 * math.cos(rot + a), 0.62 * math.sin(rot + a)) for a in np.linspace(math.pi / 4, math.pi / 4 + math.tau, 5)]
            L.polyline(pts, MAIN * 0.8, HALO_MAIN * 0.4)
        L.polyline([(0, 0.34), (0.34, 0), (0, -0.34), (-0.34, 0), (0, 0.34)], HAIR, HALO_HAIR)
        for x, y in ((0, 0.5), (0.5, 0), (0, -0.5), (-0.5, 0)):
            L.dot(x, y, 0.035)
        L.dot(0, 0, 0.06)
    else:                                                   # 복귀: 세 갈래 소용돌이 + 점 고리 + 육각별
        for i in range(3):
            base = math.tau * i / 3
            t = np.linspace(0, 1, 40)
            L.polyline(np.stack([(0.04 + 0.55 * t) * np.cos(base + 2.6 * t), (0.04 + 0.55 * t) * np.sin(base + 2.6 * t)], 1), MAIN, HALO_MAIN * 0.4)
        for i in range(24):
            th = math.tau * i / 24
            L.dot(0.78 * math.cos(th), 0.78 * math.sin(th), 0.018)
        star(L, 0.68, 6, 2, math.pi / 2, HAIR, 0)
    return L


def draw_ring_low(cfg, n):
    L = Layer(4.25, n)
    double_border(L, 4.20, 0.05)
    double_border(L, 3.05, -0.05)
    text_band(L, 3.62, 0.30, cfg["ring_glyphs"], cfg["seed"] + 7)
    for i in range(12):
        th = math.tau * i / 12
        L.circle(4.05 * math.cos(th), 4.05 * math.sin(th), 0.07, HAIR, HALO_HAIR)
        L.dot(3.20 * math.cos(th), 3.20 * math.sin(th), 0.02)
    ticks(L, 3.92, 3.98, 180, 15)
    return L


def draw_ring_high(cfg, n):
    L = Layer(2.85, n)
    L.ring(2.80, MAIN, HALO_MAIN)
    L.ring(2.74, HAIR, 0)
    L.ring(2.02, MAIN, HALO_MAIN * 0.7)
    text_band(L, 2.38, 0.18, 64 + 16 * cfg["level"], cfg["seed"] + 9, inward=True, width=GLYPH * 0.9)
    count = cfg["sats"] + (8 if cfg["level"] >= 2 else 0)
    for i in range(count):
        th = math.tau * i / count
        L.circle(2.62 * math.cos(th), 2.62 * math.sin(th), 0.05, HAIR, HALO_HAIR)
    ticks(L, 2.08, 2.13, 120, 0)
    return L


DRAW = {"문자띠": draw_text_layer, "기하": draw_geometry_layer, "중심": draw_center_layer, "고리아래": draw_ring_low, "고리위": draw_ring_high}


# ──────────────────────────────────────────────────────────── 텍스처·재질·메시

def save_png(name, rgba, folder):
    os.makedirs(folder, exist_ok=True)
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, rgba.shape[1], rgba.shape[0], alpha=True)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(folder, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def material(name, img):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for node in list(nt.nodes):
        if node.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(node)
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    cut = nt.nodes.new("ShaderNodeMath")
    cut.operation = "GREATER_THAN"
    cut.inputs[1].default_value = 0.5
    nt.links.new(tex.outputs["Alpha"], cut.inputs[0])
    nt.links.new(cut.outputs["Value"], bsdf.inputs["Alpha"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 2.5
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 1.0
    mat.use_backface_culling = False
    return mat


def _annulus(r0, r1, segments, rings, z, uv_radius):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    radii = [r0 + (r1 - r0) * i / rings for i in range(rings + 1)]
    rows = []
    for r in radii:
        rows.append([bm.verts.new((0.0, 0.0, z))] if r == 0.0 else
                    [bm.verts.new((r * math.cos(math.tau * k / segments), r * math.sin(math.tau * k / segments), z)) for k in range(segments)])

    def face(vs):
        f = bm.faces.new(vs)
        for loop in f.loops:
            loop[uv].uv = (loop.vert.co.x / (2 * uv_radius) + 0.5, loop.vert.co.y / (2 * uv_radius) + 0.5)

    for a, b in zip(rows, rows[1:]):
        for k in range(segments):
            k1 = (k + 1) % segments
            if len(a) == 1:
                face([a[0], b[k], b[k1]])
            else:
                face([a[k], b[k], b[k1]])
                face([a[k], b[k1], a[k1]])
    bm.normal_update()
    assert all(f.normal.z > 0.99 for f in bm.faces), "마법진 면이 위를 안 본다"
    return bm


def assemble(name, collection, folder, location=(0.0, 0.0, 0.0), tex_scale=1.0):
    cfg = VARIANTS[name]
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.2
    root.location = Vector(location) / UNITS
    collection.objects.link(root)
    info = {"tris": {}, "z": {}, "textures": []}
    for child, (tail, z, r0, r1, segs, rings, size) in LAYERS.items():
        n = max(256, int(size * tex_scale))
        layer = DRAW[tail](cfg, n)
        mat_name = f"포탈_마법진_{cfg['color_key']}_{tail}_발광_잎카드"
        mat = material(mat_name, save_png(mat_name, layer.rgba(cfg["tint"]), folder))
        info["textures"].append(f"{mat_name}.png ({n})")
        del layer
        bm = _annulus(r0, r1, segs, rings, z, r1)
        info["tris"][child] = sum(len(f.verts) - 2 for f in bm.faces)
        info["z"][child] = z
        bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
        mesh = bpy.data.meshes.new(f"{name}_{child}")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(child, mesh)
        collection.objects.link(obj)
        obj.parent = root
    marker = bpy.data.objects.new(SOCKET[0], None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 0.1
    marker.location = Vector(SOCKET[1]) / UNITS
    collection.objects.link(marker)
    marker.parent = root
    total = sum(info["tris"].values())
    assert total <= TRI_LIMIT, f"{name} 삼각형 {total} > {TRI_LIMIT}"
    assert max(info["z"].values()) <= HEIGHT_LIMIT and max(l[3] for l in LAYERS.values()) <= RADIUS + 1e-9
    zs = [info["z"][c] for c in LAYERS if "떠있는" in c]
    dirs = [c.rsplit("_", 1)[1] for c in LAYERS if "떠있는" in c]
    assert len(set(dirs)) == len(dirs), "떠 있는 층끼리 도는 방향이 같다"
    return root, info


def build_in_window(name, collection, folder, location, tex_scale=1.0):
    return assemble(name, collection, folder, location, tex_scale)


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(VARIANTS)
    folder = os.path.join(OUT, "Textures")
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_마법진")
        bpy.context.scene.collection.children.link(col)
        root, info = assemble(name, col, folder)
        names = sorted(c.name for c in root.children)
        assert names == sorted(list(LAYERS) + [SOCKET[0]]), f"자식 이름이 밀렸다: {names}"
        assert {o.name for o in bpy.context.scene.objects} == {root.name, *LAYERS, SOCKET[0]}
        path = os.path.join(OUT, name + ".fbx")
        # 🔴 v2: 선택으로 고르면 헤드리스에서 빈 FBX(4KB) — 빈 장면에 이 마법진만 있으니 장면 전체를 내보낸다
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={"MESH", "EMPTY"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {name}  지름 {2 * RADIUS:.1f}  층 z {info['z']}  삼각형 {info['tris']} (합 {sum(info['tris'].values())})  "
              f"텍스처 {info['textures']}  소켓 {SOCKET}  → {os.path.normpath(path)}")


if __name__ == "__main__":
    main()
