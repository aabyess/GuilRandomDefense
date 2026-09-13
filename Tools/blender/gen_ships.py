"""유닛 모델 — 고대의 배 · 해적선(PM 배정 2026-09-13). 레인에서 싸우는 유닛이라 구조물과 규칙이 다르다. blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX는 Assets/Art/Units/<유닛이름>/<유닛이름>.fbx.
🔴 파일명은 잠정 — 아래 SHIP_NAMES 한 곳만 바꾸면 FBX·메시·재질·뼈대 이름이 전부 따라온다.

화면 없이(정본):  blender --background --factory-startup --python gen_ships.py [-- ancient pirate]
사장님 창(보여 주기만): ns["build_in_window"]("ancient", 컬렉션, 창 텍스처 폴더) → (뼈대, 메시, 정보)

규격(PM):
- 선체 길이 약 24, 돛대 끝 높이 약 22(유니티가 가장 긴 축으로 크기를 다시 맞추니 비율이 중요). 원점 = 선체 바닥 가운데
  (땅 위를 다니는 유닛 — 물 기준 아님), 최저점 0, 뱃머리 −Y. 삼각형 6,000 이하, 뒷면 검사 둘 0건, 헤드리스 정본.
- 뼈대 `<이름>_뼈대`에 클립 `Idle_Bob` 하나: 8초(30fps·0~240) 동안 앞뒤·좌우로 천천히 흔들리고(각 2° 이내) 돛이 살짝
  부풀었다 가라앉기, 첫 = 끝. 유니티는 이름에 `Idle`이 든 클립을 반복한다.
- 돛·줄사다리는 `_잎카드`(양면·알파 컷). 돛·깃발은 그린 아틀라스 텍스처(2×2 칸)에 UV를 직접 편다 — 굽지 않는다.
뼈 구성: Root(바닥 원점) → Hull(흔들림 축, 바닥 위 3) → Sail_<돛대><단>(돛 가운데, −Y를 향함) · Flag(깃발 뿌리, +Y를 향함).
정점 가중치는 직접 준다(자동 가중치 아님 — 떨어진 부품이 많은 배는 엉킨다): 몸통은 Hull 1, 돛은 부푼 정도 f만큼 Sail·나머지 Hull,
깃발은 기둥에서 멀수록 Flag.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형, 굽기 DIFFUSE(Metallic 0).
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import gen_docks  # noqa: E402  (짓는 도구)
import gen_punk  # noqa: E402  (PNG 저장·그린 재질)
from gen_docks import box, cyl, lathe, prism  # noqa: E402
from shops_common import _link, _live_objects, _math, _mix, _node, _noise, _ramp, _select_only  # noqa: E402

SHIP_NAMES = {"ancient": "고대의배", "pirate": "해적선"}      # 🔴 잠정 파일명 — 여기만 바꾼다

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Units")   # 배마다 Units/<이름>/ 폴더(main 참고)
FRAMES, FPS = 240, 30
TRI_LIMIT = 6000

SPEC = {
    # 🔴 1·2차는 옆에서 둘 다 큰 목선이었다(PM): 뱃전선이 일자(가운데 높고 s²로 끝만 조금 듦), 이물이 뭉툭한 수직, 고물은
    # 선실 상자 하나. → 가운데를 낮추고 |s|^1.6로 이물·고물 +2~3, 이물을 앞으로 rake만큼 기울임(몸통 길이 = length − rake라
    # 전체 선체 길이는 그대로 약 24), 고물 누각(castle) + 뒷면 창 줄·고물등, 고대의배는 금박 회랑과 두 배 크기 독수리 선수상.
    "ancient": dict(
        style="ancient", length=24.0, rake=2.2, beam=7.0, keel_bow=1.6, keel_stern=0.9, sheer=4.0, sheer_bow=3.0, sheer_stern=2.0,
        decks=[(-1.0, -0.6, 4.3), (-0.6, 0.6, 3.3), (0.6, 1.0, 4.4)],
        masts=[dict(y=-6.2, top=19.0, yards=[(8.2, 9.6), (12.6, 7.6), (16.4, 5.4)]),
               dict(y=-0.6, top=22.0, yards=[(8.6, 11.0), (13.8, 8.8), (18.2, 6.2)]),
               dict(y=6.0, top=17.0, yards=[(9.2, 7.2), (13.2, 5.2)])],
        sail_gap=2.6, bowsprit=(4.5, 28.0), figurehead=3.4, hatches=[(-3.7, 1.8), (2.9, 1.6)],
        windows=[(4.7, 5.6), (6.4, 7.4)], stern_port_z=None,        # 선미 판 창 줄 + 누각 뒷면 창 줄
        flag=dict(mast=1, size=(4.2, 0.8), taper=0.85), cannons=0,
        castle=dict(y0=7.4, height=3.5, gallery=True, lantern_cap="선수상"),
        wind=(1.8, 1.2)),                        # (좌우 °, 앞뒤 °)
    "pirate": dict(
        # 🔴 1차(폭 5.8·뱃전 3.9·이물 +1.2)는 옆에서 통통한 큰 목선처럼 읽혔다 — 폭을 줄이고 뱃전을 낮추고 이물을 높이 들어
        # 날렵하게(용골도 이물에서 더 들린다). 누각은 낮고 수수하게, 창 대신 뒷면 포문 둘
        style="pirate", length=24.0, rake=2.6, beam=5.4, keel_bow=1.8, keel_stern=0.6, sheer=3.4, sheer_bow=2.6, sheer_stern=2.0,
        decks=[(-1.0, 0.6, 2.8), (0.6, 1.0, 3.4)],
        masts=[dict(y=-4.6, top=20.5, yards=[(7.8, 9.0), (12.8, 7.0), (16.8, 5.0)]),
               dict(y=2.6, top=22.0, yards=[(8.2, 10.0), (13.6, 7.8), (18.0, 5.6)])],
        sail_gap=2.4, bowsprit=(5.0, 24.0), figurehead=None, hatches=[(-1.0, 1.4), (5.2, 1.1)],
        windows=[], stern_port_z=4.3, flag=dict(mast=1, size=(2.9, 1.9), taper=0.0), cannons=5, cannon_z=2.3,
        castle=dict(y0=7.8, height=2.8, gallery=False, lantern_cap="쇠"),
        wind=(1.6, 1.0)),
}


class ShipMesh(gen_docks.DockMesh):
    """띠 속성 + 정점 가중치(deform) + 직접 편 UV."""

    def __init__(self, groups):
        super().__init__()
        self.deform = self.bm.verts.layers.deform.verify()
        self.groups = groups

    def vert(self, co, band=0.0, weights=None):
        v = super().vert(co, band)
        for g, w in (weights or {0: 1.0}).items():
            if w > 1e-4:
                v[self.deform][g] = w
        return v

    def face_uv(self, verts, mat, want, uvs):
        lookup = dict(zip(verts, uvs))
        f = self.face(verts, mat, want)
        for loop in f.loops:
            loop[self.uv].uv = lookup[loop.vert]
        return f


# ──────────────────────────────────────────────────────────── 그린 텍스처

def _fbm(h, w, rng, base=4, octaves=5):
    total, amp, norm = np.zeros((h, w)), 1.0, 0.0
    for o in range(octaves):
        cells = base * 2 ** o
        grid = rng.random((cells + 1, cells + 1))
        ys, xs = np.linspace(0, cells, h, endpoint=False), np.linspace(0, cells, w, endpoint=False)
        y0, x0 = ys.astype(int), xs.astype(int)
        fy, fx = ys - y0, xs - x0
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        a, b = grid[y0][:, x0], grid[y0][:, x0 + 1]
        c, d = grid[y0 + 1][:, x0], grid[y0 + 1][:, x0 + 1]
        top, bot = a + (b - a) * fx[None, :], c + (d - c) * fx[None, :]
        total += amp * (top + (bot - top) * fy[:, None])
        norm += amp
        amp *= 0.5
    return total / norm


def _skull(px, py):
    """해골과 엇갈린 뼈(흰 부분 True). px·py ∈ [−1, 1], py 위쪽."""
    head = ((px / 0.42) ** 2 + ((py - 0.18) / 0.4) ** 2 <= 1) | ((px / 0.27) ** 2 + ((py + 0.22) / 0.2) ** 2 <= 1)
    bones = np.zeros_like(px, dtype=bool)
    for ang in (math.radians(40), math.radians(-40)):
        c, s = math.cos(ang), math.sin(ang)
        qx, qy = px * c + (py + 0.12) * s, -px * s + (py + 0.12) * c
        bones |= (np.abs(qx) <= 0.8) & (np.abs(qy) <= 0.065)
        for ex in (-0.84, 0.84):
            for ey in (-0.07, 0.07):
                bones |= (qx - ex) ** 2 + (qy - ey) ** 2 <= 0.085 ** 2
    eyes = ((px - 0.16) ** 2 + (py - 0.12) ** 2 <= 0.12 ** 2) | ((px + 0.16) ** 2 + (py - 0.12) ** 2 <= 0.12 ** 2)
    nose = (py <= 0.0) & (py >= -0.12) & (np.abs(px) <= -py * 0.5)
    teeth = (py <= -0.22) & (py >= -0.38) & (np.abs(((px + 0.25) % 0.1) - 0.05) < 0.012) & (np.abs(px) < 0.22)
    return (head & ~(eyes | nose | teeth)) | (bones & ~head)


def sail_atlas(style, size=1024):
    """2×2 칸. 칸 안 좌표: u 0→1 가로, 돛 v 0(위 활대)→1(아래). 이미지 행은 아래부터라 v = 1 − 행비율."""
    rng = np.random.default_rng(7 if style == "ancient" else 11)
    cs = size // 2
    img = np.zeros((size, size, 4))
    yy, xx = np.mgrid[0:cs, 0:cs]
    u, v = (xx + 0.5) / cs, 1.0 - (yy + 0.5) / cs
    for cell in range(4):
        n1, n2 = _fbm(cs, cs, rng, base=4), _fbm(cs, cs, rng, base=14)
        seam = np.abs(((u * 8) % 1.0) - 0.5) > 0.46
        if style == "ancient":
            # 바랜 삼베 — 얼룩·곰팡이 낀 아랫단, 찢김·구멍, 너덜너덜한 아랫단(윗단 활대 쪽은 멀쩡)
            # 🔴 1차(n2>0.62에서 0.72배로 딱 자른 얼룩 + 솔기 0.8배)는 얼룩무늬 군복처럼 반점이 딱딱하고 솔기가 굵은 줄로 읽혔다
            # — 얼룩은 부드러운 번짐(연속값)으로 옅게, 솔기는 가늘고 옅게
            col = np.array((0.66, 0.6, 0.47)) + (np.array((0.52, 0.46, 0.35)) - np.array((0.66, 0.6, 0.47))) * n1[..., None]
            stain = np.clip((n2 - 0.5) / 0.25, 0, 1) ** 1.5
            col = col * (1 - 0.18 * stain)[..., None]
            mildew = np.clip((v - 0.62) / 0.38, 0, 1) * n1
            col = col + (np.array((0.4, 0.42, 0.33)) - col) * (mildew * 0.55)[..., None]
            thin_seam = np.abs(((u * 8) % 1.0) - 0.5) > 0.48
            col = np.where(thin_seam[..., None], col * 0.9, col)
            edge = np.clip((0.08 - np.minimum(u, 1 - u)) / 0.08, 0, 1)
            tear = (n2 + 0.38 * v ** 2 + 0.22 * edge) > 0.93
            ragged = v > 0.93 - 0.1 * n1
            alpha = ~(tear | ragged) | (v < 0.07)
        else:
            # 검은 돛 — 닳아 희끗한 결, 옅은 솔기, 아랫단만 조금 해짐. 0칸 = 흰 해골 돛, 3칸 = 해골 깃발(끝이 너덜)
            col = np.array((0.075, 0.07, 0.066)) + np.array((0.12, 0.115, 0.11)) * (n1 * 0.7)[..., None]
            col = np.where(seam[..., None], col * 1.35, col)
            alpha = ~(v > 0.965 - 0.05 * n2)
            if cell in (0, 3):
                sc, cx, cy = (0.52, 0.5, 0.5) if cell == 0 else (0.42, 0.44, 0.5)         # 돛 해골 1.6배(0.34 → 0.52)
                mark = _skull((u - cx) / sc, ((1 - v) - cy) / sc)
                col = np.where(mark[..., None], np.array((0.8, 0.78, 0.72)) * (0.85 + 0.15 * n1[..., None]), col)
            if cell == 3:
                alpha = alpha & ~(u > 0.93 - 0.09 * n2)
        r, c = divmod(cell, 2)
        img[r * cs:(r + 1) * cs, c * cs:(c + 1) * cs] = np.dstack([col, alpha.astype(np.float64)])
    return img


def rope_texture(size=512):
    """줄사다리 카드 — 세로 굵은 줄 넷(u) + 가로 발판 줄(v), 나머지 투명."""
    yy, xx = np.mgrid[0:size, 0:size]
    u, v = (xx + 0.5) / size, (yy + 0.5) / size
    lines = np.zeros((size, size), dtype=bool)
    for c in (0.08, 0.36, 0.64, 0.92):
        lines |= np.abs(u - c) < 0.02
    rungs = (np.abs(((v * 14) % 1.0) - 0.5) > 0.45) & (u > 0.06) & (u < 0.94)
    alpha = lines | rungs
    col = np.ones((size, size, 3)) * np.array((0.2, 0.16, 0.11))
    return np.dstack([col, alpha.astype(np.float64)])


def atlas_uv(u, v, cell, flip):
    uu = 1 - u if flip else u
    r, c = divmod(cell, 2)
    return ((c + 0.01 + 0.98 * uu) / 2, (r + 0.01 + 0.98 * (1 - v)) / 2)


# ──────────────────────────────────────────────────────────── 짓기

def build(key):
    cfg = SPEC[key]
    name = SHIP_NAMES[key]
    mat = {k: f"{name}_{k}" for k in ("선체", "갑판", "목재", "쇠", "선수상", "고물등_발광", "돛_잎카드", "밧줄_잎카드")}
    groups = ["Hull"] + [f"Sail_{mi + 1}{k + 1}" for mi, m in enumerate(cfg["masts"]) for k in range(len(m["yards"]))] + ["Flag"]
    mesh = ShipMesh(groups)
    L, W = cfg["length"] - cfg["rake"], cfg["beam"]          # 몸통 길이 — 기운 이물 끝까지 합치면 length
    cfg["stern_y"] = L / 2
    bones = [("Root", (0, 0, 0), (0, 0, 1.5), None), ("Hull", (0, 0, 3.0), (0, 0, 9.0), "Root")]

    def w(s):
        if s <= 0:
            return max(W / 2 * max(0.0, 1 - abs(s) ** 1.9) ** 0.62, 0.06)
        return W / 2 * (1 - 0.62 * s ** 3) ** 0.6

    def d(s):
        return cfg["keel_bow"] * abs(s) ** 3 if s < 0 else cfg["keel_stern"] * s ** 4

    def h(s):
        # 뱃전선 — 가운데가 가장 낮고 이물·고물로 갈수록 휜다(|s|^1.6: s²보다 가운데부터 곡선이 드러난다)
        return cfg["sheer"] + (cfg["sheer_bow"] if s < 0 else cfg["sheer_stern"]) * abs(s) ** 1.6

    def deck_z(s):
        for s0, s1, z in cfg["decks"]:
            if s0 <= s <= s1:
                return z
        return cfg["decks"][-1][2]

    def y_of(s):
        return s * L / 2

    def y_at(s, z):
        """기운 이물 — 앞 45%에서 위로 갈수록 앞(−Y)으로 나간다. 용골선(z = d)은 그대로."""
        bow = max(0.0, (-s - 0.55) / 0.45) ** 2
        t = min(max((z - d(s)) / (h(s) - d(s)), 0.0), 1.0)
        return y_of(s) - cfg["rake"] * bow * t ** 1.3

    NU = 14

    def outer(s, j):
        th = -math.pi / 2 + math.pi * j / NU
        t = min(1.0, 1 - math.cos(th))
        z = d(s) + (h(s) - d(s)) * t ** 1.15
        return Vector((w(s) * math.sin(th) * (1 - 0.1 * t ** 4), y_at(s, z), z))

    def half_width_at(s, z):
        t = min(max((z - d(s)) / (h(s) - d(s)), 0.0), 1.0) ** (1 / 1.15)
        return w(s) * math.sin(math.acos(1 - t)) * (1 - 0.1 * t ** 4)

    def x_in(s):
        return max(0.9 * w(s) - 0.28, 0.04)

    # ── 선체: 바깥 판 · 뱃전 윗면 · 안쪽 뱃전 벽 · 층진 갑판 · 칸막이 · 이물/고물 막음
    ss = sorted({round(-1 + 2 * i / 32, 6) for i in range(33)} | {b for s0, s1, _ in cfg["decks"] for b in (s0, s1)})
    # 🔴 1차는 띠를 윗줄 둘(j 0·1)에 줘서 뱃전 띠가 선체 높이의 ⅓까지 번졌다(고대의배 윗배 전체가 녹색) — 맨 윗줄만
    O = [[mesh.vert(outer(s, j), 1.0 if j in (0, NU) else 0.0) for j in range(NU + 1)] for s in ss]
    RT = [(mesh.vert((-x_in(s), y_at(s, h(s)), h(s))), mesh.vert((x_in(s), y_at(s, h(s)), h(s)))) for s in ss]
    hull_y = [min(v.co.y for row in O for v in row), max(v.co.y for row in O for v in row)]
    for i in range(len(ss) - 1):
        s0, s1 = ss[i], ss[i + 1]
        for j in range(NU):
            th = -math.pi / 2 + math.pi * (j + 0.5) / NU
            mesh.face([O[i][j], O[i + 1][j], O[i + 1][j + 1], O[i][j + 1]], mat["선체"], (math.sin(th), 0, -math.cos(th)))
        mesh.face([O[i][0], O[i + 1][0], RT[i + 1][0], RT[i][0]], mat["선체"], (0, 0, 1))
        mesh.face([O[i][NU], RT[i][1], RT[i + 1][1], O[i + 1][NU]], mat["선체"], (0, 0, 1))
        z = deck_z((s0 + s1) / 2)
        assert h(s0) - z >= 0.4 and h(s1) - z >= 0.4, f"{name} 뱃전이 갑판보다 낮다: s {s0:.2f}~{s1:.2f}"
        dl0, dr0 = mesh.vert((-x_in(s0), y_at(s0, z), z)), mesh.vert((x_in(s0), y_at(s0, z), z))
        dl1, dr1 = mesh.vert((-x_in(s1), y_at(s1, z), z)), mesh.vert((x_in(s1), y_at(s1, z), z))
        mesh.face([dl0, dr0, dr1, dl1], mat["갑판"], (0, 0, 1))
        mesh.face([RT[i][0], RT[i + 1][0], dl1, dl0], mat["선체"], (1, 0, 0))
        mesh.face([RT[i][1], dr0, dr1, RT[i + 1][1]], mat["선체"], (-1, 0, 0))
        if i == 0:
            mesh.face(list(O[0]), mat["선체"], (0, -1, 0))
            mesh.face([dl0, dr0, RT[0][1], RT[0][0]], mat["선체"], (0, 1, 0))
        if i == len(ss) - 2:
            mesh.face(list(O[-1]), mat["선체"], (0, 1, 0))
            mesh.face([dl1, dr1, RT[-1][1], RT[-1][0]], mat["선체"], (0, -1, 0))
        if i > 0:
            zp = deck_z((ss[i - 1] + s0) / 2)
            if abs(zp - z) > 1e-6:                                          # 층진 갑판 칸막이 — 낮은 갑판 쪽을 본다
                lo, hi = min(zp, z), max(zp, z)
                xs = x_in(s0)
                wall = [mesh.vert((-xs, y_at(s0, lo), lo)), mesh.vert((xs, y_at(s0, lo), lo)), mesh.vert((xs, y_at(s0, hi), hi)),
                        mesh.vert((-xs, y_at(s0, hi), hi))]
                mesh.face(wall, mat["갑판"], (0, -1 if zp < z else 1, 0))
    # ── 돛대 · 망대 · 활대 · 돛
    rigging_tops = []
    for mi, mast in enumerate(cfg["masts"]):
        ym = mast["y"]
        sm = ym / (L / 2)
        z0, top = deck_z(sm) - 0.2, mast["top"]
        zmid = z0 + (top - z0) * 0.58
        at = Matrix.Translation((0, ym, 0))
        lathe(mesh, [(0.42, z0), (0.34, zmid), (0.24, zmid + 0.3), (0.14, top)], 8, mat["목재"], at)
        lathe(mesh, [(0.3, zmid - 0.25), (1.25, zmid - 0.08), (1.25, zmid + 0.08)], 10, mat["목재"], at)
        rigging_tops.append(Vector((0, ym, top - 0.3)))
        yards = mast["yards"]
        for k, (zy, ln) in enumerate(yards):
            cyl(mesh, (-ln / 2, ym - 0.45, zy), (ln / 2, ym - 0.45, zy), 0.14, 8, mat["목재"], caps=(True, True))
            gname = f"Sail_{mi + 1}{k + 1}"
            gi = groups.index(gname)
            z_top = zy - 0.1
            z_bot = yards[k - 1][0] + 0.3 if k > 0 else deck_z(sm) + cfg["sail_gap"]
            w_top, w_bot = ln * 0.92, (yards[k - 1][1] if k > 0 else ln * 1.04) * 0.92
            y0, bulge = ym - 0.62, 0.1 * ln * 0.92
            if cfg["style"] == "pirate":
                # 해골 돛 = 큰 돛대·앞 돛대 아랫돛 둘(PM 2차: 게임 시점에서 해골이 작았다)
                cell, flip = (0, 0) if (mi, k) in ((1, 0), (0, 0)) else (1 + (mi + k) % 2, (mi + k) % 2)
            else:
                cell, flip = (mi * 3 + k) % 4, (mi + k) % 2
            cols, rows = 7, 8
            grid = []
            for r in range(rows + 1):
                v = r / rows
                row = []
                for c in range(cols + 1):
                    u = c / cols
                    f = math.sin(math.pi * u) ** 0.8 * math.sin(math.pi * v * 0.62)
                    p = Vector(((u - 0.5) * (w_top + (w_bot - w_top) * v), y0 - bulge * f, z_top + (z_bot - z_top) * v))
                    row.append((mesh.vert(p, weights={0: 1 - f, gi: f}), atlas_uv(u, v, cell, flip)))
                grid.append(row)
            for r in range(rows):
                for c in range(cols):
                    cells = [grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]]
                    mesh.face_uv([q[0] for q in cells], mat["돛_잎카드"], (0, -1, 0), [q[1] for q in cells])
            zc = (z_top + z_bot) / 2
            bones.append((gname, (0, y0 - bulge * 0.9, zc), (0, y0 - bulge * 0.9 - 1.5, zc), "Hull"))
        # 줄사다리 카드 — 뱃전 → 망대, 망대 가장자리 → 윗돛대
        yb = min(ym + 3.0, cfg["castle"]["y0"] - 0.2)                      # 뒷돛대 줄사다리가 고물 누각을 뚫지 않게
        for sx in (-1, 1):
            sb, sf = yb / (L / 2), (ym + 0.6) / (L / 2)
            lower = [Vector((sx * 0.9 * w(sf), ym + 0.6, h(sf))), Vector((sx * 0.9 * w(sb), yb, h(sb))),
                     Vector((sx * 0.4, ym + 0.25, zmid - 0.3)), Vector((sx * 0.4, ym - 0.05, zmid - 0.3))]
            upper = [Vector((sx * 1.15, ym - 0.3, zmid + 0.08)), Vector((sx * 1.15, ym + 0.9, zmid + 0.08)),
                     Vector((sx * 0.2, ym + 0.25, top - 1.4)), Vector((sx * 0.2, ym + 0.05, top - 1.4))]
            for quad in (lower, upper):
                _card(mesh, quad, mat["밧줄_잎카드"], (sx, 0, 0))
    # ── 갑판 승강구
    for yh, size in cfg["hatches"]:
        z = deck_z(yh / (L / 2))
        box(mesh, -size / 2, size / 2, yh - size / 2, yh + size / 2, z - 0.05, z + 0.3, mat["목재"], skip=("bottom",))
    # ── 고물 누각: 뒷 갑판 위 한 층(뱃전 안쪽 선을 따라 좁아짐). 뒷면은 선미 판 위로 솟아 창 줄이 들고, 지붕 난간·앞문
    ca = cfg["castle"]
    y0c, y1c = ca["y0"], L / 2 - 0.05
    s0c, s1c = y0c / (L / 2), y1c / (L / 2)
    zb, zr = deck_z(s0c) - 0.05, deck_z(s0c) + ca["height"]
    hx0, hx1 = x_in(s0c) - 0.05, x_in(s1c) - 0.05
    plan = [(-hx0, y0c), (hx0, y0c), (hx1, y1c), (-hx1, y1c)]
    # 🔴 3차는 벽을 한 칸(아래 띠 0 → 위 1)으로 지어 금박 띠가 벽 높이 절반까지 번져 누각이 카키색 상자로 보였다 —
    # 지붕 0.35 아래에 고리를 하나 더 둬 띠를 윗단에만
    lo = [mesh.vert((x, y, zb)) for x, y in plan]
    mid = [mesh.vert((x, y, zr - 0.35)) for x, y in plan]
    hi = [mesh.vert((x, y, zr), 1.0) for x, y in plan]                      # 윗단 띠 = 뱃전 띠 색
    for a, b, want in ((0, 1, (0, -1, 0)), (1, 2, (1, 0, 0)), (2, 3, (0, 1, 0)), (3, 0, (-1, 0, 0))):
        mesh.face([lo[a], lo[b], mid[b], mid[a]], mat["선체"], want)
        mesh.face([mid[a], mid[b], hi[b], hi[a]], mat["선체"], want)
    mesh.face(hi, mat["갑판"], (0, 0, 1))
    for k in range(4):
        (px, py), (qx, qy) = plan[k], plan[(k + 1) % 4]
        prism(mesh, (px, py, zr + 0.3), (qx, qy, zr + 0.3), 0.12, 0.6, mat["목재"])
    box(mesh, -0.45, 0.45, y0c - 0.06, y0c, zb + 0.05, zb + 1.9, mat["목재"], skip=("+y",))
    if ca["gallery"]:                                                        # 고대의배 — 금박 조각 테 두른 고물 회랑
        g = mat["선수상"]
        zg = h(1.0) + 0.05
        gy0, gy1, gx = y1c, L / 2 + 0.95, hx1 + 0.35
        box(mesh, -gx, gx, gy0, gy1, zg - 0.18, zg, mat["갑판"])
        box(mesh, -gx, gx, gy1 - 0.02, gy1 + 0.08, zg - 0.5, zg - 0.18, g)
        rail = zg + 0.85
        prism(mesh, (-gx, gy1, rail), (gx, gy1, rail), 0.14, 0.14, g)
        for sx in (-1, 1):
            prism(mesh, (sx * gx, gy0, rail), (sx * gx, gy1, rail), 0.14, 0.14, g)
        for x, y in [(-gx + 2 * gx * k / 6, gy1) for k in range(7)] + [(sx * gx, (gy0 + gy1) / 2) for sx in (-1, 1)]:
            prism(mesh, (x, y, zg), (x, y, rail), 0.1, 0.1, g, up=(0, 1, 0))
    lp = Vector((0, y1c - 0.3, zr))                                          # 고물등 — 누각 지붕 뒤 가운데 기둥 위
    cyl(mesh, lp, lp + Vector((0, 0, 1.1)), 0.07, 6, mat["목재"])
    lantern = Matrix.Translation(lp + Vector((0, 0, 1.1)))
    lathe(mesh, [(0.14, 0.0), (0.32, 0.12), (0.32, 0.62), (0.14, 0.72)], 8, mat["고물등_발광"], lantern, cap_bottom=True)
    lathe(mesh, [(0.38, 0.72), (0.07, 1.02)], 8, mat[ca["lantern_cap"]], lantern)
    if cfg.get("stern_port_z"):                                              # 해적선 — 뒷면 포문 둘에 고물 대포
        for sx in (-1, 1):
            cyl(mesh, (sx * 0.9, L / 2 - 0.3, cfg["stern_port_z"]), (sx * 0.9, L / 2 + 0.6, cfg["stern_port_z"]), 0.15, 10, mat["쇠"])
    # ── 이물 돛대(바우스프릿): 기운 이물 꼭대기에서 앞위로 · 이물 난간(앞 갑판 뱃전 위로 이물 끝에 모이는 V자) · 버팀줄
    stem_top = Vector((0, y_at(-1.0, h(-1.0)), h(-1.0)))
    length_b, angle_b = cfg["bowsprit"]
    bs0 = stem_top + Vector((0, 0.35, -0.35))
    bs1 = bs0 + Vector((0, -math.cos(math.radians(angle_b)), math.sin(math.radians(angle_b)))) * length_b
    cyl(mesh, bs0, bs1, 0.24, 8, mat["목재"])
    s_r = -0.62
    for sx in (-1, 1):
        a = Vector((sx * x_in(s_r), y_at(s_r, h(s_r)), h(s_r) + 0.75))
        b = stem_top + Vector((0, 0.5, 0.75))
        prism(mesh, a, b, 0.1, 0.1, mat["목재"])
        for t in (0.0, 0.35, 0.7):
            p = a.lerp(b, t)
            prism(mesh, p - Vector((0, 0, 0.8)), p, 0.1, 0.1, mat["목재"], up=(0, 1, 0))
    stay_points = [bs1] + rigging_tops + [Vector((0, y1c - 0.6, zr + 0.6))]
    for a, b in zip(stay_points, stay_points[1:]):
        prism(mesh, a, b, 0.08, 0.08, mat["목재"], up=(1, 0, 0))
    # ── 선수상(고대의배): 두 배 크기 금박 독수리 — 앞으로 기운 몸 · 머리 · 부리 · 짧게 펼친 날개(깃 셋씩)
    # 🔴 1·2차(몸 1.65·머리 0.34·접은 날개)는 게임·옆 시점에서 작은 덩어리라 형태가 안 읽혔다(PM)
    if cfg["figurehead"]:
        g = mat["선수상"]
        zf = cfg["figurehead"]
        base = Vector((0, y_at(-1.0, zf) - 0.1, zf))
        axis = Vector((0, -0.64, 0.77)).normalized()
        lathe(mesh, [(0.35, 0.0), (0.75, 0.7), (0.7, 1.6), (0.45, 2.3), (0.28, 2.7)], 8, g,
              Matrix.Translation(base) @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4())
        head_c = base + axis * 3.05
        head = bmesh.ops.create_icosphere(mesh.bm, subdivisions=1, radius=0.5, matrix=Matrix.Translation(head_c))["verts"]
        for v in head:
            v[mesh.deform][0] = 1.0
            v[mesh.band] = 0.0
        for f in {f for v in head for f in v.link_faces}:
            f.material_index = mesh.m(g)
        beak = Vector((0, -0.85, -0.35)).normalized()
        lathe(mesh, [(0.2, 0.0), (0.04, 0.6)], 6, g, Matrix.Translation(head_c + beak * 0.35) @ beak.to_track_quat("Z", "Y").to_matrix().to_4x4())
        shoulder = base + axis * 1.9
        for sx in (-1, 1):
            for k, (dx, dy, dz) in enumerate(((1.9, 0.5, 0.9), (2.1, 1.1, 0.1), (1.7, 1.5, -0.7))):
                prism(mesh, shoulder + Vector((sx * 0.4, 0.1 * k, 0)), shoulder + Vector((sx * dx, dy, dz)), 0.55 - 0.1 * k, 0.1, g,
                      up=(0, 1, 0))
    # ── 대포(해적선): 뱃전 구멍마다 한 문씩, 셰이더 포문 무늬와 같은 자리
    for k in range(cfg["cannons"]):
        yk = -6.0 + 2.4 * k
        sk, zc = yk / (L / 2), cfg["cannon_z"]
        xh = half_width_at(sk, zc)
        for sx in (-1, 1):
            cyl(mesh, (sx * (xh - 0.25), yk, zc), (sx * (xh + 0.75), yk, zc), 0.17, 10, mat["쇠"])
    # ── 깃발: 큰 돛대 꼭대기에서 고물 쪽(+Y)으로 나부낌
    fl = cfg["flag"]
    fm = cfg["masts"][fl["mast"]]
    fw, fh = fl["size"]
    gi = groups.index("Flag")
    cols, rows = 6, 3
    grid = []
    for r in range(rows + 1):
        v = r / rows
        row = []
        for c in range(cols + 1):
            u = c / cols
            span = fh * (1 - fl["taper"] * u)
            p = Vector((0.18 * math.sin(2 * math.pi * u * 1.2) * u, fm["y"] + 0.2 + u * fw,
                        fm["top"] - 0.25 - fh / 2 + span * (0.5 - v)))
            row.append((mesh.vert(p, weights={0: 1 - u, gi: u}), atlas_uv(u, v, 3, 0)))
        grid.append(row)
    for r in range(rows):
        for c in range(cols):
            cells = [grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]]
            mesh.face_uv([q[0] for q in cells], mat["돛_잎카드"], (1, 0, 0), [q[1] for q in cells])
    fz = fm["top"] - 0.25 - fh / 2
    bones.append(("Flag", (0, fm["y"] + 0.2, fz), (0, fm["y"] + 1.7, fz), "Hull"))
    return mesh, cfg, bones, hull_y


def _card(mesh, quad, material, want):
    return mesh.face_uv([mesh.vert(p) for p in quad], material, want, [(0, 0), (1, 0), (1, 1), (0, 1)])


# ──────────────────────────────────────────────────────────── 굽는 셰이더

def _shade(mat, cfg):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    kind = mat.name.rsplit("_", 1)[1]
    ancient = cfg["style"] == "ancient"

    def bands(scale, direction, distortion=0.0):
        wave = _node(nt, "ShaderNodeTexWave", Scale=scale, Distortion=distortion)
        wave.wave_type, wave.bands_direction = "BANDS", direction
        _link(nt, co, wave.inputs["Vector"])
        return wave.outputs["Fac"]

    def game(axis):
        return _math(nt, "MULTIPLY", sep.outputs[axis], UNITS)

    def span(value, lo, hi):
        m = nt.nodes.new("ShaderNodeMapRange")
        _link(nt, value, m.inputs["Value"])
        m.inputs["From Min"].default_value, m.inputs["From Max"].default_value = lo, hi
        return m.outputs["Result"]

    def stretched(scale_xyz, noise_scale, detail=6.0):
        mp = _node(nt, "ShaderNodeMapping")
        mp.inputs["Scale"].default_value = scale_xyz
        _link(nt, co, mp.inputs["Vector"])
        return _noise(nt, mp.outputs["Vector"], noise_scale, detail=detail)

    def inside(value, lo, hi):                     # lo~hi 안이면 1
        return _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", value, lo), _math(nt, "LESS_THAN", value, hi))

    if kind == "선체":
        dark, light = ((0.05, 0.04, 0.03, 1), (0.1, 0.08, 0.055, 1)) if ancient else ((0.03, 0.022, 0.016, 1), (0.065, 0.047, 0.032, 1))
        wood = _mix(nt, stretched((1.0, 0.08, 1.0), 35.0), dark, light)
        strake = _ramp(nt, bands(8.0, "Z"), [(0.0, (1, 1, 1, 1)), (0.07, (1, 1, 1, 1)), (0.14, (0, 0, 0, 1))])
        color = _mix(nt, strake, wood, (0.015, 0.012, 0.01, 1))
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "띠"
        trim = _ramp(nt, attr.outputs["Fac"], [(0.55, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
        z = game(2)
        if ancient:
            # 바랜 금박 뱃전 띠(청동 녹) · 아랫배 이끼와 따개비
            gilt = _mix(nt, _noise(nt, co, 25.0), (0.24, 0.18, 0.07, 1), (0.15, 0.14, 0.08, 1))
            color = _mix(nt, trim, color, gilt)
            moss = _math(nt, "MULTIPLY", span(z, 3.2, 0.8), _noise(nt, co, 8.0, detail=6.0))
            color = _mix(nt, _ramp(nt, moss, [(0.25, (0, 0, 0, 1)), (0.6, (1, 1, 1, 1))]), color, (0.045, 0.065, 0.035, 1))
            vor = _node(nt, "ShaderNodeTexVoronoi", Scale=90.0)
            _link(nt, co, vor.inputs["Vector"])
            barn = _math(nt, "MULTIPLY", _ramp(nt, vor.outputs["Distance"], [(0.05, (1, 1, 1, 1)), (0.09, (0, 0, 0, 1))]), span(z, 2.2, 1.4))
            color = _mix(nt, barn, color, (0.24, 0.23, 0.2, 1))
        else:
            # 붉은 띠 · 포문(대포 자리와 같은 y 간격) · 검은 뱃전
            stripe = inside(z, cfg["cannon_z"] - 0.5, cfg["cannon_z"] + 0.5)          # 포문 줄을 감싸는 띠
            color = _mix(nt, stripe, color, _mix(nt, _noise(nt, co, 20.0), (0.2, 0.02, 0.012, 1), (0.3, 0.035, 0.02, 1)))
            y = game(1)
            dy = _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "DIVIDE", _math(nt, "ADD", y, 7.2), 2.4), 0.0), 0.5), 0.0)
            dz = _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", z, cfg["cannon_z"]), 0.0)
            near = _math(nt, "MAXIMUM", _math(nt, "MULTIPLY", dy, 2.4), dz)
            rows = inside(y, -7.2, 4.8)
            frame = _math(nt, "MULTIPLY", rows, _math(nt, "LESS_THAN", near, 0.5))
            port = _math(nt, "MULTIPLY", rows, _math(nt, "LESS_THAN", near, 0.38))
            color = _mix(nt, frame, color, (0.05, 0.035, 0.025, 1))
            color = _mix(nt, port, color, (0.008, 0.007, 0.006, 1))
            color = _mix(nt, trim, color, (0.02, 0.018, 0.016, 1))
        # 고물 창 — 선미 판 가운데 줄
        stern = _math(nt, "GREATER_THAN", game(1), cfg["stern_y"] - 0.5)
        col_x = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT",
                      _math(nt, "ADD", _math(nt, "DIVIDE", game(0), 1.3), 0.5), 0.0), 0.5), 0.0), 0.3)
        for lo_z, hi_z in cfg["windows"]:                                     # 선미 판·누각 뒷면 창 줄
            win = _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", stern, col_x), inside(z, lo_z, hi_z))
            color = _mix(nt, win, color, (0.02, 0.028, 0.035, 1))
        if cfg.get("stern_port_z"):                                            # 해적선 — 창 대신 뒷면 포문 둘(x ±0.9)
            near = _math(nt, "MAXIMUM",
                         _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "ABSOLUTE", game(0), 0.0), 0.9), 0.0),
                         _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", z, cfg["stern_port_z"]), 0.0))
            color = _mix(nt, _math(nt, "MULTIPLY", stern, _math(nt, "LESS_THAN", near, 0.44)), color, (0.05, 0.035, 0.025, 1))
            color = _mix(nt, _math(nt, "MULTIPLY", stern, _math(nt, "LESS_THAN", near, 0.32)), color, (0.008, 0.007, 0.006, 1))
    elif kind == "갑판":
        base = ((0.12, 0.1, 0.075, 1), (0.2, 0.17, 0.12, 1)) if ancient else ((0.09, 0.07, 0.05, 1), (0.16, 0.12, 0.085, 1))
        board = _mix(nt, stretched((1.0, 0.1, 1.0), 40.0), *base)
        gap = _ramp(nt, bands(10.0, "X"), [(0.0, (1, 1, 1, 1)), (0.06, (1, 1, 1, 1)), (0.12, (0, 0, 0, 1))])
        color = _mix(nt, gap, board, (0.025, 0.02, 0.015, 1))
        if ancient:
            color = _mix(nt, _ramp(nt, _noise(nt, co, 6.0, detail=6.0), [(0.55, (0, 0, 0, 1)), (0.75, (0.8, 0.8, 0.8, 1))]), color,
                         (0.07, 0.085, 0.055, 1))                                        # 갑판 이끼 얼룩
    elif kind == "목재":
        color = _mix(nt, stretched((1.0, 1.0, 0.1), 40.0), (0.05, 0.038, 0.026, 1), (0.11, 0.082, 0.056, 1))
    elif kind == "쇠":
        base = _mix(nt, _noise(nt, co, 30.0), (0.018, 0.018, 0.02, 1), (0.045, 0.045, 0.048, 1))
        rust = _ramp(nt, _noise(nt, co, 40.0, detail=8.0), [(0.6, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
        color = _mix(nt, rust, base, (0.12, 0.05, 0.02, 1))
    elif kind == "선수상":
        # 닳은 금박 목조각 — 바탕은 바랜 금, 녹청은 오목한 얼룩으로만(1차는 녹청 바탕이라 녹색 판으로 읽혔다)
        base = _mix(nt, _noise(nt, co, 18.0, detail=6.0), (0.2, 0.155, 0.075, 1), (0.32, 0.25, 0.12, 1))
        green = _ramp(nt, _noise(nt, co, 30.0, detail=6.0), [(0.58, (0, 0, 0, 1)), (0.72, (0.8, 0.8, 0.8, 1))])
        color = _mix(nt, green, base, (0.1, 0.16, 0.12, 1))
    elif kind == "발광":
        # 고물등 — 따뜻한 등불 유리 + 가로 창살
        glow = _mix(nt, _noise(nt, co, 40.0), (0.9, 0.55, 0.18, 1), (1.0, 0.75, 0.35, 1))
        bars = _ramp(nt, bands(30.0, "Z"), [(0.0, (1, 1, 1, 1)), (0.08, (1, 1, 1, 1)), (0.14, (0, 0, 0, 1))])
        color = _mix(nt, bars, glow, (0.08, 0.05, 0.03, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    _link(nt, color, bsdf.inputs["Base Color"])


def tex_size(name):
    return 1024 if name.endswith(("_선체", "_갑판", "_목재")) else 512


def _generated(name):
    return name.endswith("_잎카드")


def unwrap(obj):
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(obj.data)
    for index, slot in enumerate(obj.material_slots):
        if _generated(slot.material.name):
            continue
        for f in bm.faces:
            f.select = f.material_index == index
        bmesh.update_edit_mesh(obj.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake(obj, folder):
    """굽는 재질만 DIFFUSE 색으로 굽는다(그린 텍스처 재질에는 버림 이미지를 활성으로 둔다)."""
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    saved = (scene.render.engine, scene.cycles.samples)
    view = bpy.context.view_layer
    selected = [o for o in _live_objects() if o.select_get()]
    active = view.objects.active
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 12
    dummy = bpy.data.images.new("_버림", 8, 8)
    temp, images = [], {}
    try:
        _select_only(obj)
        for slot in obj.material_slots:
            m = slot.material
            nt = m.node_tree
            node = nt.nodes.new("ShaderNodeTexImage")
            if _generated(m.name):
                node.image = dummy
                temp.append((nt, node))
            else:
                old = bpy.data.images.get(m.name)
                if old is not None:
                    bpy.data.images.remove(old)
                node.image = images[m.name] = bpy.data.images.new(m.name, tex_size(m.name), tex_size(m.name))
            for n in nt.nodes:
                n.select = False
            node.select = True
            nt.nodes.active = node
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=False, margin=8)
        for nt, node in temp:
            nt.nodes.remove(node)
        for name, img in images.items():
            img.filepath_raw = os.path.join(folder, name + ".png")
            img.file_format = "PNG"
            img.save()
            nt = bpy.data.materials[name].node_tree
            for n in list(nt.nodes):
                if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
                    nt.nodes.remove(n)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            _link(nt, tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    finally:
        bpy.data.images.remove(dummy)
        scene.render.engine, scene.cycles.samples = saved
        for o in _live_objects():
            o.select_set(o in selected)
        view.objects.active = active


# ──────────────────────────────────────────────────────────── 뼈대 · 동작

def rig(obj, bones, collection):
    arm_data = bpy.data.armatures.new(obj.name + "_뼈대")
    arm = bpy.data.objects.new(obj.name + "_뼈대", arm_data)
    collection.objects.link(arm)
    _select_only(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    made = {}
    for name, head, tail, parent in bones:
        b = arm_data.edit_bones.new(name)
        b.head, b.tail = Vector(head) / UNITS, Vector(tail) / UNITS
        b.inherit_scale = "NONE"
        if parent:
            b.parent = made[parent]
        made[name] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return arm


def _fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    return [fc for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]


def animate(arm, cfg, step=4):
    """`Idle_Bob` — 8초 한 주기. 좌우 흔들림 1회·앞뒤 2회·들썩 2회·돛 부풂 2회(돛마다 위상 다름)·깃발 4회.
    전부 한 주기(0~240)의 정수배 사인이라 첫 = 끝. 선형 키를 4프레임마다 — 반복 이음매에서 속도가 끊기지 않는다."""
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 0, FRAMES
    arm.animation_data_create()
    action = bpy.data.actions.new("Idle_Bob")
    arm.animation_data.action = action
    pose = arm.pose.bones
    for pb in pose:
        pb.rotation_mode = "XYZ"
    roll, pitch = (math.radians(a) for a in cfg["wind"])
    sails = [pb for pb in pose if pb.name.startswith("Sail_")]
    for frame in range(0, FRAMES + 1, step):
        ph = math.tau * frame / FRAMES
        pose["Root"].location = (0.0, 0.1 / UNITS * (1 - math.cos(2 * ph)) / 2, 0.0)       # 뿌리 뼈 Y = 위(+Z)
        pose["Root"].keyframe_insert("location", frame=frame)
        pose["Hull"].rotation_euler = (pitch * math.sin(2 * ph + 0.7), 0.0, roll * math.sin(ph))
        pose["Hull"].keyframe_insert("rotation_euler", frame=frame)
        for k, pb in enumerate(sails):
            pb.location = (0.0, 0.35 / UNITS * (1 - math.cos(2 * ph + 0.5 * k)) / 2, 0.0)   # 돛 뼈 Y = 앞(−Y)
            pb.keyframe_insert("location", frame=frame)
        pose["Flag"].rotation_euler = (0.0, 0.0, math.radians(14) * math.sin(4 * ph + 0.3))
        pose["Flag"].keyframe_insert("rotation_euler", frame=frame)
    for fc in _fcurves(action):
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    return action


def loop_matches(arm):
    scene = bpy.context.scene

    def pose_at(frame):
        scene.frame_set(frame)
        return [tuple(round(x, 6) for row in pb.matrix for x in row) for pb in arm.pose.bones]

    same = pose_at(0) == pose_at(FRAMES)
    scene.frame_set(0)
    return same


# ──────────────────────────────────────────────────────────── 조립 · 내보내기

def assemble(key, collection, folder):
    name = SHIP_NAMES[key]
    mesh, cfg, bones, hull_y = build(key)
    bm = mesh.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs, ys, zs = ([v.co[k] for v in bm.verts] for k in range(3))
    xs, ys, zs = list(xs), list(ys), list(zs)
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    length = hull_y[1] - hull_y[0]
    assert tri_count <= TRI_LIMIT, f"{name} 삼각형 {tri_count} > {TRI_LIMIT}"
    assert abs(min(zs)) < 1e-3, f"{name} 최저점 {min(zs):.3f} ≠ 0 (선체 바닥)"
    assert 23.0 <= length <= 25.0, f"{name} 선체 길이 {length:.2f} (약 24)"
    assert 21.0 <= max(zs) <= 23.0, f"{name} 돛대 끝 {max(zs):.2f} (약 22)"
    assert abs(max(xs) + min(xs)) < 1e-3, f"{name} 좌우가 가운데가 아니다"
    info = dict(tris=tri_count, length=length, height=max(zs), width=max(xs) - min(xs), y=(min(ys), max(ys)))
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    for g in mesh.groups:
        obj.vertex_groups.new(name=g)
    textures = {f"{name}_돛_잎카드": lambda: sail_atlas(cfg["style"]), f"{name}_밧줄_잎카드": rope_texture}
    for mat_name in mesh.mats:
        if mat_name in textures:
            m = gen_punk._generated_material(mat_name, gen_punk._save_png(mat_name, textures[mat_name](), folder))
        else:
            m = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
            m.use_nodes = True
            _shade(m, cfg)
        data.materials.append(m)
    return obj, info, bones, cfg


def build_in_window(key, collection, folder):
    obj, info, bones, cfg = assemble(key, collection, folder)
    unwrap(obj)
    bake(obj, folder)
    arm = rig(obj, bones, collection)
    animate(arm, cfg)
    info["loop"] = loop_matches(arm)
    assert info["loop"], f"{obj.name} Idle_Bob 첫 프레임 ≠ 끝 프레임"
    info["bones"] = [b.name for b in arm.data.bones]
    return arm, obj, info


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(SHIP_NAMES)
    for key in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_유닛")
        bpy.context.scene.collection.children.link(col)
        name = SHIP_NAMES[key]
        # 유닛 스킨 규칙: Assets/Art/Units/<이름>/<이름>.fbx + 그 폴더의 Textures/(2026-09-13 저장소 통합)
        unit_dir = os.path.join(OUT, name)
        os.makedirs(os.path.join(unit_dir, "Textures"), exist_ok=True)
        arm, obj, info = build_in_window(key, col, os.path.join(unit_dir, "Textures"))
        for o in _live_objects():
            o.select_set(o in (arm, obj))
        bpy.context.view_layer.objects.active = arm
        path = os.path.join(unit_dir, name + ".fbx")
        # 🔴 use_all_actions=False면 테이크 이름이 장면 이름(「…|Scene」)이 되어 유니티 클립에 `Idle`이 안 든다(1차 check_anim).
        # 헤드리스 파일엔 액션이 Idle_Bob 하나뿐이라 True로 내보내면 테이크 = 「<뼈대>|Idle_Bob」.
        assert [a.name for a in bpy.data.actions] == ["Idle_Bob"], f"액션이 Idle_Bob 하나가 아니다: {[a.name for a in bpy.data.actions]}"
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, mesh_smooth_type="FACE", armature_nodetype="NULL",
                                 bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                                 bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
        print(f"만듦  {name}  삼각형 {info['tris']}  선체 길이 {info['length']:.2f}  높이 {info['height']:.2f}  폭 {info['width']:.2f}  "
              f"y {info['y'][0]:.2f}~{info['y'][1]:.2f}  뼈 {info['bones']}  반복 {info['loop']}  "
              f"재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
