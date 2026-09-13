"""포탈 마법진 3종 — 돌 아치 포탈을 대체하는 바닥 마법진(PM 2026-09-13, 사장님 「밑에 마법진 같은 걸로 이동」). blender 세션.
원작 WC3 Circle of Power(바닥 룬 원) 계열, 사실적 C 화풍: 바닥에 새긴 돌 홈 + 홈 안에서 빛나는 룬.

화면 없이(정본):  blender --background --factory-startup --python gen_magic_circles.py [-- 포탈_마법진_스토리 ...]
사장님 창(보여 주기만): import gen_magic_circles as mc; mc.build_in_window(이름, 컬렉션, 텍스처 폴더, 위치)

규격(PM)
| 파일 | 색(발광) | 쓰임 |
| 포탈_마법진_스토리 | 호박 1.0,0.72,0.35 | 레인→스토리존 |
| 포탈_마법진_뽑기   | 보라 0.72,0.45,1.0 | 뽑기 섬 |
| 포탈_마법진_복귀   | 청록 0.35,0.95,0.85 | 스토리존→레인(한 겹 더 화려) |
- 지름 10(게임 단위) — 유니티가 판정 원판 지름(6.5·9·15·24)에 맞춰 배율. 원점 바닥 가운데, 정면 없음.
- 최상위 = 빈 오브젝트(파일 이름). 자식: 마법진_바닥 · 마법진_고리_밖_회전_시계 · 마법진_고리_안_회전_반시계 · 마법진_중심 · 빛_자리_01(가운데 높이 1.0).
- 높이: 바닥 돌판은 z 0.03 한 장(0~0.08), 발광 층은 그 위 0.02(0.05·0.055·0.06으로 살짝 엇갈려 z-fighting 방지), 전체 ≤ 0.15.
- 재질: 바닥 `포탈_마법진_<색>_바닥_잎카드`(가장자리 알파로 흙에 스며듦), 발광 셋 `포탈_마법진_<색>_발광_잎카드`(선에만 알파·발광).
- 삼각형 1,000 이하 — 문양은 메시가 아니라 텍스처(2048). UV는 위에서 본 평면 투영(u = x/10+½) — 네 메시가 한 장의 원을 나눠 쓴다.
- 🔸 도는 고리와 돌 홈이 어긋나지 않게: 도는 두 고리의 룬·별은 돌에 판 **둥근 홈 띠** 안에서 돌고, 돌에 모양 그대로 새긴 건 안 도는 가운데 문양뿐.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m(FBX global_scale 11.4). 문양 텍스처는 numpy로 직접 그림(가상의 룬 — 실제 언어 글자 아님).
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
TEX = 2048
FLOOR_Z, GLOW_Z = 0.03, (0.05, 0.055, 0.06)            # 바닥 · 밖 고리 · 안 고리 · 가운데(아래 BANDS 순서)
TRI_LIMIT = 1000
# 반지름은 원판 반지름(5) = 1로 정규화. 메시 띠: 밖 고리 0.69~0.99 · 안 고리 0.30~0.69 · 가운데 0~0.30
BANDS = {"밖": (0.69, 0.99), "안": (0.30, 0.69), "중심": (0.0, 0.30)}
VARIANTS = {
    "포탈_마법진_스토리": dict(color_key="호박", glow=(1.0, 0.72, 0.35), runes=24, star=(6, 2), seed=11, extra=False, emblem="crescent"),
    "포탈_마법진_뽑기": dict(color_key="보라", glow=(0.72, 0.45, 1.0), runes=30, star=(7, 3), seed=23, extra=False, emblem="diamond"),
    "포탈_마법진_복귀": dict(color_key="청록", glow=(0.35, 0.95, 0.85), runes=36, star=(8, 3), seed=37, extra=True, emblem="spiral"),
}
CHILDREN = ("마법진_바닥", "마법진_고리_밖_회전_시계", "마법진_고리_안_회전_반시계", "마법진_중심")
SOCKET = ("빛_자리_01", (0.0, 0.0, 1.0))


# ──────────────────────────────────────────────────────────── 그리기 도구(정규화 좌표 X,Y ∈ [−1,1], 한 픽셀 = 2/TEX)

def _grid(n):
    c = (np.arange(n) + 0.5) / n * 2.0 - 1.0
    X, Y = np.meshgrid(c, c)
    return X, Y, np.hypot(X, Y), np.arctan2(Y, X)


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def _cover(dist, half, px):
    """거리장 → 덮임(0~1), 가장자리 한 픽셀 부드럽게."""
    return _smooth(half + px, half - px, dist)


def _seg_dist(X, Y, a, b):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    L2 = max(dx * dx + dy * dy, 1e-12)
    t = np.clip(((X - ax) * dx + (Y - ay) * dy) / L2, 0.0, 1.0)
    return np.hypot(X - (ax + t * dx), Y - (ay + t * dy))


def _blur(a, radius):
    """상자 흐림 세 번(≈가우스) — 누적합."""
    out = a
    for _ in range(3):
        for axis in (0, 1):
            c = np.cumsum(np.pad(out, [(radius + 1, radius)] if False else [((radius + 1, radius) if ax == axis else (0, 0)) for ax in (0, 1)], mode="edge"), axis=axis)
            hi = np.take(c, np.arange(2 * radius + 1, c.shape[axis]), axis=axis)
            lo = np.take(c, np.arange(0, c.shape[axis] - 2 * radius - 1), axis=axis)
            out = (hi - lo) / (2 * radius + 1)
    return out


def _fbm(n, rng, base=6, octaves=6):
    out = np.zeros((n, n))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        k = base * 2 ** o
        g = rng.random((k + 1, k + 1))
        idx = np.linspace(0, k, n)
        i0 = np.floor(idx).astype(int).clip(0, k - 1)
        f = idx - i0
        f = f * f * (3 - 2 * f)
        row = g[i0] * (1 - f)[:, None] + g[i0 + 1] * f[:, None]
        layer = row[:, i0] * (1 - f)[None, :] + row[:, i0 + 1] * f[None, :]
        out += amp * layer
        total += amp
        amp *= 0.5
    return out / total


class Canvas:
    """덮임을 모아 둔다: glow(빛나는 선), groove(돌에 판 홈), rng로 가상의 룬."""

    def __init__(self, n=TEX):
        self.n = n
        self.px = 2.0 / n
        self.X, self.Y, self.R, self.T = _grid(n)
        self.glow = np.zeros((n, n))
        self.groove = np.zeros((n, n))
        self.channels = np.zeros((n, n))

    def ring(self, r0, width, glow=True, groove=True, extra_groove=0.012):
        d = np.abs(self.R - r0)
        if glow:
            self.glow = np.maximum(self.glow, _cover(d, width / 2, self.px))
        if groove:
            self.groove = np.maximum(self.groove, _cover(d, width / 2 + extra_groove, self.px * 2))

    def channel(self, r0, r1, depth=0.75):
        """도는 고리가 들어가는 둥근 홈 띠 — 테두리는 비스듬히."""
        inside = _smooth(r0 - 0.006, r0 + 0.006, self.R) * _smooth(r1 + 0.006, r1 - 0.006, self.R)
        self.groove = np.maximum(self.groove, depth * inside)
        self.channels = np.maximum(self.channels, inside)

    def segments(self, segs, width, glow=True, groove=True, extra_groove=0.012, box=None):
        if box is None:
            sel = (slice(None), slice(None))
        else:
            x0, x1, y0, y1 = box
            c0 = max(int((x0 + 1) / self.px) - 4, 0)
            c1 = min(int((x1 + 1) / self.px) + 4, self.n)
            r0 = max(int((y0 + 1) / self.px) - 4, 0)
            r1 = min(int((y1 + 1) / self.px) + 4, self.n)
            if c1 <= c0 or r1 <= r0:
                return
            sel = (slice(r0, r1), slice(c0, c1))
        X, Y = self.X[sel], self.Y[sel]
        d = np.full(X.shape, 9.0)
        for a, b in segs:
            d = np.minimum(d, _seg_dist(X, Y, a, b))
        if glow:
            self.glow[sel] = np.maximum(self.glow[sel], _cover(d, width / 2, self.px))
        if groove:
            self.groove[sel] = np.maximum(self.groove[sel], _cover(d, width / 2 + extra_groove, self.px * 2))

    def dot(self, x, y, radius, glow=True, groove=True):
        box = (x - radius - 0.02, x + radius + 0.02, y - radius - 0.02, y + radius + 0.02)
        self.segments([((x, y), (x, y))], 2 * radius, glow, groove, box=box)

    def arc(self, cx, cy, r, a0, a1, width, steps=24, **kw):
        pts = [(cx + r * math.cos(a0 + (a1 - a0) * i / steps), cy + r * math.sin(a0 + (a1 - a0) * i / steps)) for i in range(steps + 1)]
        self.segments(list(zip(pts, pts[1:])), width, box=(cx - r - 0.02, cx + r + 0.02, cy - r - 0.02, cy + r + 0.02), **kw)


def _rune_strokes(rng):
    """가상의 룬 한 글자 — 3×5 기준점 격자에서 세로 줄기 하나 + 가지 2~4개(대각·갈고리·짧은 가로), 가끔 점 하나."""
    cols, rows = (-0.5, 0.0, 0.5), (-1.0, -0.5, 0.0, 0.5, 1.0)
    spine_x = rng.choice(cols)
    strokes = [((spine_x, -1.0), (spine_x, 1.0))]
    for _ in range(rng.integers(2, 5)):
        y0 = float(rng.choice(rows))
        x1 = float(rng.choice([c for c in cols if c != spine_x]))
        y1 = float(np.clip(y0 + rng.choice([-1.0, -0.5, 0.0, 0.5, 1.0]), -1.0, 1.0))
        strokes.append(((spine_x, y0), (x1, y1)))
    dot = (float(rng.choice(cols)), float(rng.choice((-1.35, 1.35)))) if rng.random() < 0.35 else None
    return strokes, dot


def draw_runes(cv, r_mid, height, count, seed, width):
    rng = np.random.default_rng(seed)
    step = math.tau / count
    for i in range(count):
        th = i * step
        strokes, dot = _rune_strokes(rng)
        radial = (math.cos(th), math.sin(th))
        tangent = (-math.sin(th), math.cos(th))
        sx, sy = height * 0.32, height * 0.5              # 글자 폭·높이(정규화)

        def place(p):
            u, v = p
            return (radial[0] * (r_mid + v * sy) + tangent[0] * u * sx, radial[1] * (r_mid + v * sy) + tangent[1] * u * sx)

        segs = [(place(a), place(b)) for a, b in strokes]
        cx, cy = radial[0] * r_mid, radial[1] * r_mid
        box = (cx - height, cx + height, cy - height, cy + height)
        cv.segments(segs, width, box=box, extra_groove=0.0, groove=False)
        if dot:
            px, py = place(dot)
            cv.dot(px, py, width * 0.9, groove=False)


def star_segments(r, n, k, rot=0.0):
    pts = [(r * math.cos(rot + math.tau * i / n), r * math.sin(rot + math.tau * i / n)) for i in range(n)]
    return [(pts[i], pts[(i + k) % n]) for i in range(n)]


def draw_pattern(cfg):
    cv = Canvas()
    w = 0.010                                             # 발광 선 굵기(정규화 0.010 ≈ 0.05 게임 단위)
    # ── 밖 고리(돈다): 테두리 두 줄 + 룬 띠. 돌에는 둥근 홈 띠만.
    cv.channel(0.715, 0.965)
    cv.ring(0.955, w, groove=False)
    cv.ring(0.725, w, groove=False)
    draw_runes(cv, 0.84, 0.13, cfg["runes"], cfg["seed"], w * 0.85)
    if cfg["extra"]:                                      # 복귀: 바깥에 눈금 한 겹 더
        for i in range(72):
            th = math.tau * i / 72
            r0, r1 = (0.968, 0.985) if i % 2 else (0.962, 0.99)
            cv.segments([((r0 * math.cos(th), r0 * math.sin(th)), (r1 * math.cos(th), r1 * math.sin(th)))], w * 0.6,
                        groove=False, box=(math.cos(th) * 0.97 - 0.03, math.cos(th) * 0.97 + 0.03, math.sin(th) * 0.97 - 0.03, math.sin(th) * 0.97 + 0.03))
        cv.ring(0.975, w * 0.5, groove=False)
    # ── 안 고리(반대로 돈다): 동심원 + 다각형 별
    cv.channel(0.315, 0.68, depth=0.6)
    cv.ring(0.67, w, groove=False)
    cv.ring(0.325, w, groove=False)
    n, k = cfg["star"]
    cv.segments(star_segments(0.655, n, k, math.pi / 2), w, groove=False)
    cv.ring(0.50, w * 0.7, groove=False)
    for i in range(n):
        th = math.pi / 2 + math.tau * i / n
        cv.dot(0.655 * math.cos(th), 0.655 * math.sin(th), 0.018, groove=False)
    if cfg["extra"]:
        cv.segments(star_segments(0.47, n, 1, math.pi / 2 + math.pi / n), w * 0.7, groove=False)
        for i in range(n):
            th = math.pi / 2 + math.pi / n + math.tau * i / n
            cv.arc(0.575 * math.cos(th), 0.575 * math.sin(th), 0.03, 0, math.tau, w * 0.6, groove=False)
    # ── 가운데(안 돈다): 돌에 모양 그대로 새김
    cv.ring(0.27, w)
    cv.ring(0.245, w * 0.6)
    e = cfg["emblem"]
    if e == "crescent":
        cv.arc(0.0, 0.0, 0.15, math.radians(40), math.radians(320), w * 1.2)
        cv.arc(0.045, 0.0, 0.12, math.radians(60), math.radians(300), w)
        cv.dot(0.07, 0.0, 0.028)
        for i in range(6):
            th = math.tau * i / 6
            cv.segments([((0.19 * math.cos(th), 0.19 * math.sin(th)), (0.235 * math.cos(th), 0.235 * math.sin(th)))], w)
    elif e == "diamond":
        d = 0.19
        cv.segments([((0, d), (d, 0)), ((d, 0), (0, -d)), ((0, -d), (-d, 0)), ((-d, 0), (0, d))], w * 1.1)
        s = 0.085
        cv.segments([((-s, -s), (s, -s)), ((s, -s), (s, s)), ((s, s), (-s, s)), ((-s, s), (-s, -s))], w * 0.8)
        for x, y in ((0.0, 0.12), (0.12, 0.0), (0.0, -0.12), (-0.12, 0.0)):
            cv.dot(x * 1.0, y * 1.0, 0.018)
        cv.dot(0.0, 0.0, 0.03)
    else:
        for i in range(3):                               # 세 갈래 소용돌이
            base = math.tau * i / 3
            pts = [((0.02 + 0.17 * t) * math.cos(base + 2.4 * t), (0.02 + 0.17 * t) * math.sin(base + 2.4 * t)) for t in np.linspace(0, 1, 28)]
            cv.segments(list(zip(pts, pts[1:])), w * 1.1, box=(-0.26, 0.26, -0.26, 0.26))
        for i in range(12):
            th = math.tau * i / 12
            cv.dot(0.215 * math.cos(th), 0.215 * math.sin(th), 0.012)
        cv.dot(0.0, 0.0, 0.025)
    return cv


# ──────────────────────────────────────────────────────────── 텍스처

def floor_texture(cv, cfg):
    """닳은 돌 원판(가운데 둥근 판 + 바깥 쐐기돌 16) · 판 홈(어둡고 비스듬한 빛) · 홈에 남은 옅은 빛 얼룩 · 가장자리 흙으로 흩어짐(알파 컷)."""
    rng = np.random.default_rng(cfg["seed"] + 100)
    n = cv.n
    R, T = cv.R, cv.T
    noise = _fbm(n, rng, 5, 7)
    fine = _fbm(n, rng, 40, 3)
    stone = np.array((0.50, 0.47, 0.43)) + (np.array((0.66, 0.63, 0.57)) - np.array((0.50, 0.47, 0.43))) * noise[..., None]
    stone *= (0.88 + 0.24 * fine)[..., None]
    joints = _cover(np.abs(R - 0.69), 0.004, cv.px) * (R < 0.99)
    wedge = np.abs(((T / (math.tau / 16)) % 1.0) - 0.5) * (math.tau / 16) * R
    joints = np.maximum(joints, _cover(np.abs(wedge - (math.tau / 32) * R), 0.0035, cv.px) * _smooth(0.69, 0.70, R))
    stone *= (1.0 - 0.45 * joints)[..., None]
    g = cv.groove
    gy, gx = np.gradient(g)
    bevel = np.clip(-(gx * -0.7 + gy * 0.7) * n * 0.012, -1.0, 1.0)      # 빛은 왼쪽 위에서
    col = stone * (1.0 - 0.5 * g)[..., None] + (0.18 * bevel)[..., None]
    glow = np.array(cfg["glow"])
    # 홈에 번진 빛(발광 아님) — 안 도는 가운데는 문양을 흐린 테두리 빛, 도는 두 띠는 홈 띠 전체에 옅게.
    # 🔴 1차는 선만 하얗게 떠 스티커 같았다 — 돌이 빛을 받아 물든 것처럼 보여야 새긴 홈 안의 빛으로 읽힌다.
    halo = _blur(cv.glow * (R < 0.30), 10) * 1.6 + 0.35 * cv.channels
    col = col + glow * (0.22 * np.clip(halo, 0, 1) * (0.7 + 0.3 * noise))[..., None]
    soil = np.array((0.36, 0.28, 0.19)) * (0.8 + 0.4 * noise)[..., None]
    edge = 0.965 + 0.025 * (_fbm(n, rng, 12, 3) - 0.5) * 2
    dirt = _smooth(0.86, edge, R)
    col = col * (1.0 - 0.55 * dirt)[..., None] + soil * (0.55 * dirt)[..., None]
    crumbs = (fine > 0.62) & (R < edge + 0.03) & (R < 0.999)
    alpha = ((R < edge) | crumbs).astype(float)
    col = np.where((R >= edge)[..., None], soil, col)
    return np.dstack([np.clip(col, 0, 1), alpha])


def glow_texture(cv, cfg):
    """선 색 = PM 지정 발광 색 그대로(채도 유지), 가운데만 살짝 밝게(0.12), 선을 따라 세기 흔들림 — 🔴 1차(흰색 0.4 섞음)는 낮에 거의 흰 선."""
    rng = np.random.default_rng(cfg["seed"] + 200)
    glow = np.array(cfg["glow"])
    core = _smooth(0.6, 1.0, cv.glow)
    flick = 0.78 + 0.22 * _fbm(cv.n, rng, 16, 3)
    col = glow[None, None, :] * flick[..., None] + (0.12 * core)[..., None]
    return np.dstack([np.clip(col, 0, 1), cv.glow])


def save_png(name, rgba, folder):
    os.makedirs(folder, exist_ok=True)
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, rgba.shape[1], rgba.shape[0], alpha=True)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(folder, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def material(name, img, glow_color=None):
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
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.85
    if glow_color is not None:
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 2.0
    mat.use_backface_culling = False
    return mat


# ──────────────────────────────────────────────────────────── 메시

def _disc_mesh(r0, r1, segments, rings, z, mat_index):
    """r0~r1 띠(r0=0이면 가운데 점부터) — 위(+Z)를 보는 한 장, UV는 위에서 본 평면 투영."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    radii = [r0 + (r1 - r0) * i / rings for i in range(rings + 1)]
    ringverts = []
    for r in radii:
        if r == 0.0:
            ringverts.append([bm.verts.new((0.0, 0.0, z))])
        else:
            ringverts.append([bm.verts.new((r * math.cos(math.tau * k / segments), r * math.sin(math.tau * k / segments), z))
                              for k in range(segments)])

    def face(vs):
        f = bm.faces.new(vs)
        f.material_index = mat_index
        for loop in f.loops:
            loop[uv].uv = (loop.vert.co.x / (2 * RADIUS) + 0.5, loop.vert.co.y / (2 * RADIUS) + 0.5)

    for a, b in zip(ringverts, ringverts[1:]):
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


def assemble(name, collection, folder, location=(0.0, 0.0, 0.0)):
    cfg = VARIANTS[name]
    cv = draw_pattern(cfg)
    key = cfg["color_key"]
    floor_name, glow_name = f"포탈_마법진_{key}_바닥_잎카드", f"포탈_마법진_{key}_발광_잎카드"
    floor_mat = material(floor_name, save_png(floor_name, floor_texture(cv, cfg), folder))
    glow_mat = material(glow_name, save_png(glow_name, glow_texture(cv, cfg), folder), cfg["glow"])
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.2
    root.location = Vector(location) / UNITS if location else Vector()
    collection.objects.link(root)
    parts = {
        "마법진_바닥": (_disc_mesh(0.0, RADIUS, 48, 2, FLOOR_Z, 0), floor_mat),
        "마법진_고리_밖_회전_시계": (_disc_mesh(BANDS["밖"][0] * RADIUS, BANDS["밖"][1] * RADIUS, 64, 1, GLOW_Z[0], 0), glow_mat),
        "마법진_고리_안_회전_반시계": (_disc_mesh(BANDS["안"][0] * RADIUS, BANDS["안"][1] * RADIUS, 48, 1, GLOW_Z[1], 0), glow_mat),
        "마법진_중심": (_disc_mesh(0.0, BANDS["중심"][1] * RADIUS, 32, 1, GLOW_Z[2], 0), glow_mat),
    }
    info = {"tris": {}, "z": [9.0, -9.0], "radius": 0.0}
    objs = []
    for child, (bm, mat) in parts.items():
        info["tris"][child] = sum(len(f.verts) - 2 for f in bm.faces)
        zs = [v.co.z for v in bm.verts]
        info["z"] = [min(info["z"][0], min(zs)), max(info["z"][1], max(zs))]
        info["radius"] = max(info["radius"], max(math.hypot(v.co.x, v.co.y) for v in bm.verts))
        bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
        mesh = bpy.data.meshes.new(f"{name}_{child}")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(child, mesh)
        collection.objects.link(obj)
        obj.parent = root
        objs.append(obj)
    marker = bpy.data.objects.new(SOCKET[0], None)
    marker.empty_display_type = "PLAIN_AXES"
    marker.empty_display_size = 0.1
    marker.location = Vector(SOCKET[1]) / UNITS
    collection.objects.link(marker)
    marker.parent = root
    assert sum(info["tris"].values()) <= TRI_LIMIT, f"{name} 삼각형 {sum(info['tris'].values())} > {TRI_LIMIT}"
    assert info["z"][1] <= 0.15 and info["z"][0] >= 0.0 and FLOOR_Z <= 0.08, f"{name} 높이 {info['z']}"
    assert GLOW_Z[0] - FLOOR_Z >= 0.02 - 1e-9, "발광 층이 바닥에서 0.02 안 뜬다"
    assert abs(info["radius"] - RADIUS) < 1e-3, f"{name} 반지름 {info['radius']}"
    return root, objs + [marker], info


def build_in_window(name, collection, folder, location):
    return assemble(name, collection, folder, location)


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(VARIANTS)
    folder = os.path.join(OUT, "Textures")
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_마법진")
        bpy.context.scene.collection.children.link(col)
        root, children, info = assemble(name, col, folder)
        names = sorted(c.name for c in root.children)
        assert names == sorted(list(CHILDREN) + [SOCKET[0]]), f"자식 이름이 밀렸다: {names}"
        path = os.path.join(OUT, name + ".fbx")
        # 🔴 선택으로 고르면 헤드리스에서 아무것도 안 골려 빈 FBX(4KB·0.0003초)가 나왔다 — 빈 장면에 이 마법진만 있으니 장면 전체를 내보낸다
        assert {o.name for o in bpy.context.scene.objects} == {root.name, *CHILDREN, SOCKET[0]}, [o.name for o in bpy.context.scene.objects]
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={"MESH", "EMPTY"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {name}  지름 {2 * info['radius']:.2f}  z {info['z'][0]:.3f}~{info['z'][1]:.3f}  삼각형 {info['tris']} "
              f"(합 {sum(info['tris'].values())})  자식 {names}  소켓 {SOCKET[0]} {SOCKET[1]}  → {os.path.normpath(path)}")


if __name__ == "__main__":
    main()
