"""펑크해저드 얼음·용암 소품 — 정의문 양쪽을 얼음 반/용암 반으로 꾸미는 흩뿌림 소품 여섯(PM 배정 2026-09-13). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX·텍스처는 Assets/Art/Structures/로.

화면 없이(정본):  blender --background --factory-startup --python gen_punk.py [-- 펑크_얼음가시_01 ...]
사장님 창(보여 주기만): ns["build_in_window"](이름, 컬렉션, 창 텍스처 폴더)

규격(PM): 원점 바닥 가운데, 최저점 0, 뒷면 검사 둘 0건, 삼각형 채당 1,500 이하, 헤드리스 정본. 맵 생성기가 자연물처럼
흩뿌리니 **정면이 따로 없다** — 사방 어디서 봐도 되게 방사형으로 짓는다.
- 펑크_얼음가시_01~03: 땅에서 비스듬히 솟은 육각 얼음 결정 다발(01 작은 무더기 · 02 중간 · 03 큰 기둥 셋).
  반투명 대신 푸른 흰 불투명 + 모서리 밝게(굽기에서 Bevel 노드 법선 차이로 모서리를 찾는다), 바닥에 서리 원판(알파 컷).
- 펑크_용암바위_01~03: 식어 굳은 검은 용암 딱지(01 낮은 판 · 02 둔덕 · 03 뾰족 덩어리). 딱지를 보로노이 조각으로
  갈라 틈 바닥만 `펑크_용암틈_발광` — 발광 면적은 표면의 15% 이하(assert). 03에만 빈 오브젝트 `연기_자리_01`.
재질:
- `<이름>_얼음`·`<이름>_암석` — 절차 셰이더를 메시마다 굽는다(UV가 메시마다 달라 이름도 따로).
- `펑크_서리_잎카드`·`펑크_용암틈_발광` — 여섯이 **같이 쓰는** 그린 텍스처. UV를 직접 편다(서리 = 원판 반지름으로
  정규화한 평면, 틈 = 틈 방향 u·가운데 v=1) — 어느 메시에서든 같은 PNG가 맞는다. 굽지 않는다.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형, 굽기 DIFFUSE(Metallic 0).
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

from shops_common import _link, _live_objects, _math, _mix, _node, _noise, _ramp, _select_only  # noqa: E402

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")
FROST, GLOW = "펑크_서리_잎카드", "펑크_용암틈_발광"
TRI_LIMIT = 1500
GLOW_LIMIT = 0.15

# 결정: (길이, 반지름, 기울기°) 기둥 + 둘레 잔결정
ICE = {
    "펑크_얼음가시_01": dict(radius=2.0, height=3.0, seed=3, pillars=[(3.05, 0.42, 7.0)], small=8, small_len=(0.8, 1.8), small_r=(0.14, 0.28)),
    "펑크_얼음가시_02": dict(radius=4.0, height=7.5, seed=7, pillars=[(7.6, 0.85, 6.0), (5.4, 0.7, 20.0)], small=14,
                        small_len=(1.3, 3.6), small_r=(0.22, 0.48)),
    "펑크_얼음가시_03": dict(radius=6.0, height=14.0, seed=11, pillars=[(14.05, 1.45, 4.0), (10.6, 1.2, 13.0), (8.4, 1.05, 18.0)],
                        small=22, small_len=(1.8, 5.0), small_r=(0.3, 0.7)),
}
# 용암 딱지: 조각 수·틈 폭·틈 깊이·조각 가운데 솟음(√넓이 배)·바깥 벽 기울기(높이당 안으로 든 거리)·모양
# 🔴 1차(01 조각 6·틈 0.12·곧은 바깥 벽)는 부채꼴로 자른 치즈케이크 같았고 틈이 실금이었다(발광 3.6%)
ROCK = {
    "펑크_용암바위_01": dict(radius=2.5, height=1.5, seed=5, cells=9, gap=0.22, depth=0.3, bump=0.12, inset=0.6, shape="slab"),
    "펑크_용암바위_02": dict(radius=4.0, height=4.0, seed=9, cells=13, gap=0.26, depth=0.55, bump=0.22, inset=0.4, shape="mound"),
    "펑크_용암바위_03": dict(radius=6.0, height=7.0, seed=13, cells=16, gap=0.32, depth=0.9, bump=0.55, inset=0.3, shape="spire",
                        peaks=[(0.8, -0.6, 1.0, 4.4), (-2.4, 1.5, 0.62, 3.2), (2.2, 2.4, 0.45, 2.6)]),
}
NAMES = list(ICE) + list(ROCK)


class Mesh:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.heat = self.bm.verts.layers.float.new("열")
        self.mats = []
        self.markers = []

    def m(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def vert(self, co, heat=0.0):
        v = self.bm.verts.new(co)
        v[self.heat] = heat
        return v

    def face(self, verts, mat, want, uvs=None):
        """want 쪽을 보게 감김을 맞춘다(뉴얼 법선)."""
        n = Vector()
        cos = [v.co for v in verts]
        for i, p in enumerate(cos):
            q = cos[(i + 1) % len(cos)]
            n += Vector(((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y)))
        if n.dot(Vector(want)) < 0:
            verts = list(reversed(verts))
            uvs = list(reversed(uvs)) if uvs else None
        f = self.bm.faces.new(verts)
        f.material_index = self.m(mat)
        if uvs:
            for loop, uv in zip(f.loops, uvs):
                loop[self.uv].uv = uv
        return f


# ──────────────────────────────────────────────────────────── 얼음

def crystal(mesh, mat, pos, azimuth, tilt, length, radius, rnd):
    """육각 결정 — 땅속 밑고리(z 0으로 눌러 땅에 박힘) · 어깨 고리 · 끝점."""
    axis = Vector((math.sin(tilt) * math.cos(azimuth), math.sin(tilt) * math.sin(azimuth), math.cos(tilt)))
    u = Vector((0, 0, 1)).cross(axis)
    u = u.normalized() if u.length > 1e-6 else Vector((1, 0, 0))
    w = axis.cross(u)
    spin = rnd.uniform(0, math.pi)
    radii = [radius * rnd.uniform(0.82, 1.12) for _ in range(6)]
    base = Vector((pos[0], pos[1], -(radius * math.sin(tilt) + 0.08)))

    def ring(center, scale):
        return [center + (u * math.cos(spin + math.pi * i / 3) + w * math.sin(spin + math.pi * i / 3)) * radii[i] * scale for i in range(6)]

    shoulder = ring(base + axis * (length * rnd.uniform(0.64, 0.74)), 1.0)
    assert min(p.z for p in shoulder) > 0.05, f"결정 어깨가 땅속: 길이 {length:.2f} 기울기 {math.degrees(tilt):.0f}"
    bottom = ring(base, 0.85)
    for p in bottom:
        p.z = max(p.z, 0.0)
    tip = base + axis * length + u * (radius * rnd.uniform(-0.12, 0.12))
    vb = [mesh.vert(p) for p in bottom]
    vs = [mesh.vert(p) for p in shoulder]
    vt = mesh.vert(tip)

    def out(vs_):
        c = sum((v.co for v in vs_), Vector()) / len(vs_)
        return c - (base + axis * axis.dot(c - base))

    for i in range(6):
        j = (i + 1) % 6
        mesh.face([vb[i], vb[j], vs[j], vs[i]], mat, out([vb[i], vb[j], vs[j], vs[i]]))
        mesh.face([vs[i], vs[j], vt], mat, out([vs[i], vs[j], vt]))
    return tip


def frost_disc(mesh, radius, rnd, segments=32):
    turn = rnd.uniform(0, 2 * math.pi)
    c, s = math.cos(turn), math.sin(turn)

    def uv(p):
        x, y = (p.x * c - p.y * s) / radius, (p.x * s + p.y * c) / radius
        return (x * 0.5 + 0.5, y * 0.5 + 0.5)

    center = mesh.vert((0, 0, 0.03))
    ring = [mesh.vert((radius * math.cos(2 * math.pi * i / segments), radius * math.sin(2 * math.pi * i / segments), 0.03))
            for i in range(segments)]
    for i in range(segments):
        vs = [center, ring[i], ring[(i + 1) % segments]]
        mesh.face(vs, FROST, (0, 0, 1), [uv(v.co) for v in vs])


def make_ice(name):
    cfg = ICE[name]
    rnd = random.Random(cfg["seed"])
    mesh = Mesh()
    mat = name + "_얼음"
    R = cfg["radius"]
    pillars = cfg["pillars"]
    for k, (length, radius, tilt) in enumerate(pillars):
        ang = 2 * math.pi * k / len(pillars) + rnd.uniform(-0.3, 0.3)
        dist = 0.0 if len(pillars) == 1 else 0.2 * R
        crystal(mesh, mat, (dist * math.cos(ang), dist * math.sin(ang)), ang + rnd.uniform(-0.4, 0.4), math.radians(tilt),
                length, radius, rnd)
    for k in range(cfg["small"]):
        ang = 2 * math.pi * k / cfg["small"] + rnd.uniform(-0.25, 0.25)
        dist = rnd.uniform(0.3, 0.62) * R
        tilt = math.radians(rnd.uniform(25.0, 55.0))
        az = ang + rnd.uniform(-0.35, 0.35)
        length, radius = rnd.uniform(*cfg["small_len"]), rnd.uniform(*cfg["small_r"])
        pos = Vector((dist * math.cos(ang), dist * math.sin(ang)))
        lean = Vector((math.cos(az), math.sin(az)))

        def min_len():          # 어깨(길이 64%)가 땅 위에 오려면
            return (2 * radius * math.sin(tilt) + 0.2) / (0.64 * math.cos(tilt)) * 1.1

        length = max(length, min_len())
        # 🔴 1차는 원판 안에 들이려고 길이만 줄여 어깨가 땅속으로 들어갔다 — 짧게는 최소 길이까지, 그다음 눕힘을 세우고, 그다음 굵기
        while (pos + lean * (length * math.sin(tilt) + radius)).length > 0.9 * R:
            if length * 0.9 >= min_len():
                length *= 0.9
            elif tilt > math.radians(12.0):
                tilt -= math.radians(5.0)
            else:
                radius *= 0.85
                length = max(length * 0.9, min_len())
        crystal(mesh, mat, pos, az, tilt, length, radius, rnd)
    frost_disc(mesh, R, rnd)
    return mesh, cfg


# ──────────────────────────────────────────────────────────── 용암

def _hull(points):
    pts = sorted(points, key=lambda p: (p.x, p.y))

    def cross(o, a, b):
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x)

    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def _clip(poly, tags, n, d, tag):
    """n·p ≤ d 쪽만 남긴다. tags[i] = 변 (i → i+1)의 출처("B" 바깥 둘레 / 이웃 씨앗 번호)."""
    out_v, out_t = [], []
    m = len(poly)
    for i in range(m):
        p, q = poly[i], poly[(i + 1) % m]
        tp, tq = n.dot(p) - d, n.dot(q) - d
        if tp <= 0:
            out_v.append(p)
            out_t.append(tags[i])
        if (tp <= 0) != (tq <= 0):
            out_v.append(p.lerp(q, tp / (tp - tq)))
            out_t.append(tags[i] if tp > 0 else tag)
    keep_v, keep_t = [], []
    for i, p in enumerate(out_v):                                  # 겹친 점 정리
        if keep_v and (p - keep_v[-1]).length < 1e-5:
            keep_t[-1] = out_t[i]
            continue
        keep_v.append(p)
        keep_t.append(out_t[i])
    if len(keep_v) > 1 and (keep_v[0] - keep_v[-1]).length < 1e-5:
        keep_v.pop()
        keep_t.pop()
    return keep_v, keep_t


def _offset(poly, tags, half):
    """이웃 변(틈)만 half만큼 안으로 민 조각 테두리. 변이 뒤집히면 None(바깥 둘레와 예각으로 만나 모서리가 둘레를 따라
    멀리 밀리는 건 이웃과 어긋나지 않으니 허용 — 1차는 그것까지 막아 조각마다 따로 줄이게 만들었다)."""
    m = len(poly)
    lines = []
    for k in range(m):
        d = poly[(k + 1) % m] - poly[k]
        if d.length < 1e-6:
            return None
        d = d.normalized()
        lines.append((poly[k] + Vector((-d.y, d.x)) * (0.0 if tags[k] == "B" else half), d))
    out = []
    for k in range(m):
        (a1, d1), (a2, d2) = lines[k - 1], lines[k]
        cr = d1.x * d2.y - d1.y * d2.x
        if abs(cr) < 1e-6:
            out.append(a2)
            continue
        diff = a2 - a1
        out.append(a1 + d1 * ((diff.x * d2.y - diff.y * d2.x) / cr))
    for k in range(m):
        orig = poly[(k + 1) % m] - poly[k]
        if (out[(k + 1) % m] - out[k]).dot(orig) <= 0.05 * orig.length_squared:
            return None
    return out


def _merge_short_edges(cells, gap, hull):
    """짧은 변(줄이면 뒤집히는 변)을 **모든 조각에서 같이** 한 점으로 모은다.
    🔴 1차는 조각마다 따로 줄여 이웃 조각과 꼭짓점이 어긋났고, 틈 바닥에 난 구멍으로 아래쪽 조각 벽 안쪽이 보여
    카메라각 뒷면이 났다(02 2건·03 1건). 이웃 변은 틈 폭 2배, 바깥 둘레 조각 변은 3배(틈 선이 둘레와 예각으로 만나면
    짧지 않아도 뒤집힌다 — 02 조각 7)보다 짧으면 모은다. 둘레 모서리 점은 그 자리에, 둘레 위 점은 둘레 위에 둔다."""
    def same(a, b):
        return (a - b).length < 1e-4

    def on_hull(p):
        return any(same(poly[k], p) and (tags[k] == "B" or tags[k - 1] == "B") for poly, tags in cells for k in range(len(poly)))

    def corner(p):
        return any(same(h, p) for h in hull)

    skipped = []
    merged = 0
    while True:
        best = None
        for poly, tags in cells:
            m = len(poly)
            for k in range(m):
                a, b = poly[k], poly[(k + 1) % m]
                length = (b - a).length
                if length >= gap * (3.0 if tags[k] == "B" else 2.0) or any(same(a, s) and same(b, t) or same(a, t) and same(b, s)
                                                                           for s, t in skipped):
                    continue
                if best is None or length < best[0]:
                    best = (length, a.copy(), b.copy(), tags[k] == "B")
        if best is None:
            return merged
        _, a, b, along_hull = best
        ca, cb = corner(a), corner(b)
        ha, hb = on_hull(a), on_hull(b)
        if ca and cb or (ha and hb and not along_hull and not (ca or cb)):
            skipped.append((a, b))
            continue
        if ca or cb:
            target = a if ca else b
        elif along_hull or not (ha or hb):
            target = (a + b) / 2                    # 같은 둘레 변 위의 두 점이면 가운데도 둘레 위
        else:
            target = a if ha else b
        for cell in cells:
            poly, tags = cell
            new_p, new_t = [], []
            for k, p in enumerate(poly):
                q = target if same(p, a) or same(p, b) else p
                if new_p and same(q, new_p[-1]):
                    new_t[-1] = tags[k]
                    continue
                new_p.append(q)
                new_t.append(tags[k])
            if len(new_p) > 1 and same(new_p[0], new_p[-1]):
                new_p.pop()
                new_t.pop()
            cell[0], cell[1] = new_p, new_t
        merged += 1


def _area_centroid(poly):
    a = cx = cy = 0.0
    for i, p in enumerate(poly):
        q = poly[(i + 1) % len(poly)]
        c = p.x * q.y - q.x * p.y
        a += c
        cx += (p.x + q.x) * c
        cy += (p.y + q.y) * c
    a /= 2
    return a, Vector((cx / (6 * a), cy / (6 * a)))


def _height(cfg, x, y):
    r = math.hypot(x, y) / cfg["radius"]
    fall = max(0.0, 1 - r * r)
    wob = 0.05 * math.sin(1.9 * x + 0.7) * math.sin(1.6 * y + 2.1) + 0.03 * math.sin(3.7 * x - 1.3 * y)
    if cfg["shape"] == "slab":
        v = 0.45 + 0.55 * fall ** 0.35
    elif cfg["shape"] == "mound":
        v = 0.25 + 0.75 * fall ** 1.1
    else:
        # 🔴 1차(지수 1.5)는 뾰족 덩어리가 아니라 봉우리 난 둔덕으로 읽혔다 — 봉우리를 좁고 가파르게
        v = 0.14 + 0.14 * fall + max(amp * max(0.0, 1 - math.hypot(x - px, y - py) / pr) ** 2.4 for px, py, amp, pr in cfg["peaks"])
    return cfg["height"] * (v + wob)


def make_rock(name):
    cfg = ROCK[name]
    rnd = random.Random(cfg["seed"])
    mesh = Mesh()
    rock = name + "_암석"
    R, half, depth = cfg["radius"], cfg["gap"] / 2, cfg["depth"]
    hull = _hull([Vector((R * rnd.uniform(0.9, 1.0) * math.cos(a), R * rnd.uniform(0.9, 1.0) * math.sin(a)))
                  for a in (2 * math.pi * (i + rnd.uniform(-0.2, 0.2)) / 16 for i in range(16))])
    seeds, spacing = [], R * math.sqrt(math.pi / cfg["cells"]) * 0.8
    while len(seeds) < cfg["cells"]:
        for _ in range(4000):
            a, r = rnd.uniform(0, 2 * math.pi), R * 0.8 * math.sqrt(rnd.random())
            p = Vector((r * math.cos(a), r * math.sin(a)))
            if all((p - s).length >= spacing for s in seeds):
                seeds.append(p)
                if len(seeds) == cfg["cells"]:
                    break
        spacing *= 0.93

    def top_z(p):
        return _height(cfg, p.x, p.y)

    def floor_z(p, on_border):
        return 0.0 if on_border else max(top_z(p) - depth, 0.0)

    peak_cell = min(range(len(seeds)), key=lambda i: (seeds[i] - Vector(cfg["peaks"][0][:2])).length) if "peaks" in cfg else None
    cells = []
    for i, seed in enumerate(seeds):
        poly, tags = list(hull), ["B"] * len(hull)
        for j, other in enumerate(seeds):
            if j != i:
                poly, tags = _clip(poly, tags, other - seed, (other.length_squared - seed.length_squared) / 2, j)
        cells.append([poly, tags])
    _merge_short_edges(cells, cfg["gap"], hull)
    for i, (poly, tags) in enumerate(cells):
        inner = _offset(poly, tags, half)
        if inner is None:
            edges = [(tags[k], round((poly[(k + 1) % len(poly)] - poly[k]).length, 3)) for k in range(len(poly))]
            raise AssertionError(f"{name} 조각 {i} 테두리를 못 줄였다(짧은 변 합치기 뒤에도 뒤집힘): 변 {edges}")
        m = len(poly)
        border = [tags[k - 1] == "B" or tags[k] == "B" for k in range(m)]
        area, c2 = _area_centroid(inner)
        cz = top_z(c2) + cfg["bump"] * math.sqrt(area)
        center = mesh.vert((c2.x, c2.y, cz))
        mids, tops, bottoms = [], [], []
        for k, p in enumerate(inner):
            # 바깥 둘레 점은 윗면을 안으로 들여 바깥 벽이 비스듬히 선다(좌표만의 함수라 이웃 조각과 같은 자리)
            # 🔴 들임을 높이로만 정하면 가장자리 작은 조각은 윗면이 가운데를 넘어 접혀 카메라각 뒷면이 났다(01 3건·02 2건·03 1건)
            # — 조각 가운데까지 거리의 30%로 묶는다
            t = p - p.normalized() * min(cfg["inset"] * top_z(p), 0.3 * (p - c2).length) if border[k] else p
            q = c2.lerp(t, 0.55)
            mids.append(mesh.vert((q.x, q.y, cz + (top_z(p) - cz) * 0.55 + rnd.uniform(-0.04, 0.04) * cfg["height"]), 0.03))
            tops.append(mesh.vert((t.x, t.y, top_z(p)), 0.05 if border[k] else 0.2))
            bottoms.append(mesh.vert((p.x, p.y, floor_z(p, border[k])), 0.0 if border[k] else 1.0))
        for k in range(m):
            n = (k + 1) % m
            mesh.face([center, mids[k], mids[n]], rock, (0, 0, 1))
            mesh.face([mids[k], tops[k], tops[n], mids[n]], rock, (0, 0, 1))
            d = inner[n] - inner[k]
            mesh.face([bottoms[k], bottoms[n], tops[n], tops[k]], rock, (d.y, -d.x, 0))
            if tags[k] == "B":
                continue
            # 틈 바닥 반쪽 — 조각 테두리(v 0) → 틈 가운데(v 1). u는 틈 방향 전역 좌표라 이웃 반쪽과 이어진다
            e = (poly[n] - poly[k]).normalized()
            if e.x < -1e-9 or (abs(e.x) <= 1e-9 and e.y < 0):
                e = -e
            pts = [(poly[k], border[k], 1.0), (poly[n], border[n], 1.0), (inner[n], border[n], 0.0), (inner[k], border[k], 0.0)]
            mesh.face([mesh.vert((p.x, p.y, floor_z(p, b))) for p, b, _ in pts], GLOW, (0, 0, 1),
                      [(p.dot(e) / 4.0, v) for p, _, v in pts])
        if i == peak_cell:
            k = max((k for k in range(m) if tags[k] != "B"), key=lambda k: (poly[(k + 1) % m] - poly[k]).length)
            mid = (poly[k] + poly[(k + 1) % m]) / 2
            mesh.markers.append(("연기_자리_01", Vector((mid.x, mid.y, floor_z(mid, False) + 0.1))))
    # 높이를 규격에 맞춘다(틈 깊이·연기 자리도 같이)
    top = max(v.co.z for v in mesh.bm.verts)
    k = cfg["height"] / top
    for v in mesh.bm.verts:
        v.co.z *= k
    mesh.markers = [(nm, Vector((p.x, p.y, p.z * k))) for nm, p in mesh.markers]
    return mesh, cfg


# ──────────────────────────────────────────────────────────── 그린 텍스처(여섯이 같이 씀)

def _save_png(name, rgba, folder):
    os.makedirs(folder, exist_ok=True)
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(folder, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def glow_texture(size=256):
    """세로 v: 0 조각 테두리(검붉음) → 1 틈 가운데(노랑 주황). 가로 u: 이음매 없이 되풀이되는 흔들림 + 굳은 딱지 점."""
    u = (np.arange(size) / size)[None, :]
    v = ((np.arange(size) + 0.5) / size)[:, None]
    wave = sum(a * np.sin(2 * np.pi * k * u + ph) for k, a, ph in ((3, 0.5, 0.3), (7, 0.3, 1.7), (13, 0.2, 4.1), (29, 0.12, 2.2)))
    wave = (wave - wave.min()) / (wave.max() - wave.min())
    t = np.clip(v * (0.7 + 0.5 * wave), 0, 1)
    dark, mid, hot = np.array((0.2, 0.015, 0.0)), np.array((0.85, 0.2, 0.02)), np.array((1.0, 0.62, 0.18))
    col = dark + (mid - dark) * _smooth(0.0, 0.55, t)[..., None]
    col = col + (hot - col) * _smooth(0.62, 0.95, t)[..., None]
    crust = (np.sin(2 * np.pi * 47 * u + 1.3) * np.sin(2 * np.pi * 9 * v * 3 + 0.4) > 0.8) & (v < 0.45)
    col[np.broadcast_to(crust, col.shape[:2])] *= 0.35
    return np.dstack([col, np.ones((size, size))])


def frost_texture(size=512):
    """원판 서리 — 흔들리는 가장자리 + 둘레의 떨어진 서리 점(알파 0/1), 결은 방사형 깃털."""
    y, x = np.mgrid[0:size, 0:size]
    X, Y = (x + 0.5) / size * 2 - 1, (y + 0.5) / size * 2 - 1
    r, th = np.hypot(X, Y), np.arctan2(Y, X)
    edge = 0.8 + 0.07 * np.sin(3 * th + 0.4) + 0.05 * np.sin(5 * th + 2.1) + 0.035 * np.sin(11 * th + 1.3) + 0.02 * np.sin(23 * th + 0.2)
    edge = edge + 0.03 * np.sin(61 * th) * (np.sin(7 * th + 1.0) > 0)
    cell = np.floor(x / 7) * 91.7 + np.floor(y / 7) * 37.3
    speck = np.modf(np.abs(np.sin(cell) * 43758.5453))[0] > 0.9
    alpha = (r < edge) | (speck & (r < edge + 0.13) & (r < 0.99))
    # 🔴 1차(한 겹 sin θ·48)는 햇살 무늬처럼 고른 바큇살이었다 — 결 두 겹을 반지름에 따라 휘게 하고 옅게, 뭉친 얼룩을 더한다
    feather = (0.5 + 0.5 * np.sin(th * 23 + 3 * np.sin(th * 5 + r * 6) + r * 9)) * (0.5 + 0.5 * np.sin(th * 37 - 2 * np.sin(th * 3 + r * 4)))
    patch = 0.5 + 0.25 * np.sin(4 * th + 3 * r + 0.7) + 0.25 * np.sin(9 * th - 5 * r + 2.3)
    base = np.array((0.74, 0.8, 0.86))
    streak = np.array((0.6, 0.7, 0.8))
    col = base + (streak - base) * (feather * 0.45 + patch * 0.35)[..., None]
    col = col * (1.0 - 0.12 * np.clip(r / edge, 0, 1))[..., None]
    return np.dstack([col, alpha.astype(np.float32)])


def _generated_material(name, img):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    _link(nt, tex.outputs["Color"], bsdf.inputs["Base Color"])
    if name.endswith("_잎카드"):
        cut = _math(nt, "GREATER_THAN", tex.outputs["Alpha"], 0.5)
        _link(nt, cut, bsdf.inputs["Alpha"])
    if "_발광" in name:
        _link(nt, tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.5
    return mat


# ──────────────────────────────────────────────────────────── 굽는 셰이더

def _shade(mat, cfg):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    if mat.name.endswith("_얼음"):
        up = nt.nodes.new("ShaderNodeMapRange")
        _link(nt, sep.outputs[2], up.inputs["Value"])
        up.inputs["From Min"].default_value, up.inputs["From Max"].default_value = 0.0, cfg["height"] / UNITS
        body = _mix(nt, up.outputs["Result"], (0.13, 0.28, 0.42, 1), (0.42, 0.57, 0.68, 1))
        mp = _node(nt, "ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1.0, 1.0, 0.12)
        _link(nt, co, mp.inputs["Vector"])
        streak = _ramp(nt, _noise(nt, mp.outputs["Vector"], 20.0, detail=4.0), [(0.45, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
        body = _mix(nt, streak, body, (0.6, 0.72, 0.8, 1))
        # 모서리 — Bevel 법선이 참 법선과 갈라지는 곳
        bevel = _node(nt, "ShaderNodeBevel", Radius=0.16 / UNITS)
        bevel.samples = 8
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        dot = nt.nodes.new("ShaderNodeVectorMath")
        dot.operation = "DOT_PRODUCT"
        _link(nt, bevel.outputs["Normal"], dot.inputs[0])
        _link(nt, geo.outputs["True Normal"], dot.inputs[1])
        edge = _ramp(nt, _math(nt, "SUBTRACT", 1.0, dot.outputs["Value"]), [(0.0, (0, 0, 0, 1)), (0.05, (1, 1, 1, 1))])
        color = _mix(nt, edge, body, (0.8, 0.88, 0.93, 1))
        ao = _node(nt, "ShaderNodeAmbientOcclusion", Distance=0.6 / UNITS)
        dim = _ramp(nt, ao.outputs["AO"], [(0.0, (1, 1, 1, 1)), (0.8, (0, 0, 0, 1))])
        color = _mix(nt, dim, color, (0.05, 0.13, 0.22, 1))
    elif mat.name.endswith("_암석"):
        # 🔴 1차(0.016~0.048 + 짙은 줄무늬 + 재 0.075)는 직사광 렌더에서 검은 줄 든 회색 대리석으로 읽혔다 —
        # 바탕을 한 톤 더 검게, 새끼줄 결은 밝은 쪽으로 옅게(대비를 줄여 결로만), 재는 드문드문 조금만
        base = _mix(nt, _noise(nt, co, 25.0, detail=8.0), (0.009, 0.008, 0.008, 1), (0.026, 0.023, 0.021, 1))
        rope = _node(nt, "ShaderNodeTexWave", Scale=9.0, Distortion=10.0, Detail=6.0)
        rope.wave_type = "BANDS"
        _link(nt, co, rope.inputs["Vector"])
        color = _mix(nt, _ramp(nt, rope.outputs["Fac"], [(0.55, (0, 0, 0, 1)), (1.0, (0.45, 0.45, 0.45, 1))]), base, (0.04, 0.036, 0.033, 1))
        vor = _node(nt, "ShaderNodeTexVoronoi", Scale=80.0)
        _link(nt, co, vor.inputs["Vector"])
        color = _mix(nt, _ramp(nt, vor.outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.1, (0, 0, 0, 1))]), color, (0.003, 0.003, 0.003, 1))
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        nsep = nt.nodes.new("ShaderNodeSeparateXYZ")
        _link(nt, geo.outputs["True Normal"], nsep.inputs[0])
        ash = _ramp(nt, _math(nt, "MULTIPLY", nsep.outputs[2], _noise(nt, co, 9.0, detail=5.0)),
                    [(0.5, (0, 0, 0, 1)), (0.7, (0.6, 0.6, 0.6, 1))])
        color = _mix(nt, ash, color, (0.05, 0.047, 0.044, 1))
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "열"
        heat = _ramp(nt, attr.outputs["Fac"], [(0.1, (0, 0, 0, 1)), (1.0, (1, 1, 1, 1))])
        color = _mix(nt, heat, color, (0.24, 0.045, 0.01, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    bsdf.inputs["Emission Strength"].default_value = 0.0
    _link(nt, color, bsdf.inputs["Base Color"])


def tex_size(name):
    return 512 if name.startswith(("펑크_얼음가시_01", "펑크_용암바위_01")) else 1024


# ──────────────────────────────────────────────────────────── 조립·UV·굽기·내보내기

def assemble(name, collection, folder):
    mesh, cfg = make_ice(name) if name in ICE else make_rock(name)
    bm = mesh.bm
    bm.normal_update()
    R, H = cfg["radius"], cfg["height"]
    reach = max(math.hypot(v.co.x, v.co.y) for v in bm.verts)
    zs = [v.co.z for v in bm.verts]
    assert reach <= R + 1e-3, f"{name} 바닥 지름 {2 * R}을 넘었다: 반지름 {reach:.2f}"
    assert abs(min(zs)) < 1e-3 and 0.75 * H <= max(zs) <= H + 1e-3, f"{name} 높이 {min(zs):.2f}~{max(zs):.2f} (규격 {H})"
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    assert tri_count <= TRI_LIMIT, f"{name} 삼각형 {tri_count} > {TRI_LIMIT}"
    glow = 0.0
    if name in ROCK:
        gi = mesh.m(GLOW)
        total = sum(f.calc_area() for f in bm.faces)
        glow = sum(f.calc_area() for f in bm.faces if f.material_index == gi) / total
        assert glow <= GLOW_LIMIT, f"{name} 발광 면적 {glow:.1%} > 15%"
        want = ["연기_자리_01"] if "peaks" in cfg else []
        assert [m[0] for m in mesh.markers] == want, f"{name} 빈 오브젝트 {mesh.markers}"
    info = dict(tris=tri_count, reach=reach, height=max(zs), glow=glow)
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    generated = {FROST: frost_texture, GLOW: glow_texture}
    for mat_name in mesh.mats:
        if mat_name in generated:
            mat = _generated_material(mat_name, _save_png(mat_name, generated[mat_name](), folder))
        else:
            mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
            mat.use_nodes = True
            _shade(mat, cfg)
        data.materials.append(mat)
    for marker, at in mesh.markers:
        empty = bpy.data.objects.new(marker, None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = 0.1
        empty.location = at / UNITS
        empty.parent = obj
        collection.objects.link(empty)
    return obj, info


def unwrap(obj):
    """굽는 재질 면만 재질마다 스마트 투영 — 그린 텍스처 면(서리·틈)은 직접 편 UV를 둔다."""
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(obj.data)
    for index, slot in enumerate(obj.material_slots):
        if slot.material.name in (FROST, GLOW):
            continue
        for f in bm.faces:
            f.select = f.material_index == index
        bmesh.update_edit_mesh(obj.data)
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake(obj, folder):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    saved = (scene.render.engine, scene.cycles.samples)
    view = bpy.context.view_layer
    selected = [o for o in _live_objects() if o.select_get()]
    active = view.objects.active
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    dummy = bpy.data.images.new("_버림", 8, 8)
    temp, images = [], {}
    try:
        _select_only(obj)
        for slot in obj.material_slots:
            mat = slot.material
            nt = mat.node_tree
            node = nt.nodes.new("ShaderNodeTexImage")
            if mat.name in (FROST, GLOW):
                node.image = dummy
                temp.append((nt, node))
            else:
                old = bpy.data.images.get(mat.name)
                if old is not None:
                    bpy.data.images.remove(old)
                node.image = images[mat.name] = bpy.data.images.new(mat.name, tex_size(mat.name), tex_size(mat.name))
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


def build_in_window(name, collection, folder):
    obj, info = assemble(name, collection, folder)
    unwrap(obj)
    bake(obj, folder)
    return obj, info


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else NAMES
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        obj, info = build_in_window(name, col, os.path.join(OUT, "Textures"))
        for o in _live_objects():
            o.select_set(o is obj or o.parent is obj)
        bpy.context.view_layer.objects.active = obj
        path = os.path.join(OUT, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {name}  삼각형 {info['tris']}  반지름 {info['reach']:.2f}  높이 {info['height']:.2f}  발광 {info['glow']:.1%}  "
              f"재질 {[s.material.name for s in obj.material_slots]}  빈 {[c.name for c in obj.children]}  → {path}")


if __name__ == "__main__":
    main()
