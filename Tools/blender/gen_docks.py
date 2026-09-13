"""부두 소품 세트 — 섬 테두리에 자연물처럼 흩뿌리는 잔교·계선주·목선·부표더미(PM 배정 2026-09-13). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX·텍스처는 Assets/Art/Structures/로.

화면 없이(정본):  blender --background --factory-startup --python gen_docks.py [-- 부두_잔교_짧은 ...]
사장님 창(보여 주기만): ns["build_in_window"](이름, 컬렉션, 창 텍스처 폴더)

🔴 높이 기준이 소품마다 다르다(PM): 섬 윗면 z=1, 수면은 그보다 낮다(정확한 높이는 복구 뒤 맵 생성기에서).
- 잔교: 원점 = 섬 쪽 끝 갑판 윗면 가운데. 갑판 윗면 z 0, +Y(뒤)가 섬 쪽, −Y로 바다에 뻗는다. 말뚝은 z −4까지.
- 목선: 원점 = 수면 가운데. 흘수선 z 0, 선체 바닥 z −0.8. 뱃머리 −Y.
- 계선주·부표더미: 원점 바닥 가운데(평소 규칙).
그래서 assert도 「최저점 0」 대신 위 기준으로 막는다. 뒷면 검사는 check_scratch.py --water(z<0은 참고).
삼각형: 잔교 짧은 1,000 · 긴 1,500 · 목선 1,200 · 계선주 200 · 부표더미 500 이하.
재질은 `<파일명>_종류` — 메시마다 굽는다(gen_punk.bake 재사용, 그린 공유 텍스처 없음).
색 띠(목선 뱃전 줄·통 테·부표 줄무늬)는 정점 속성 「띠」로 굽는다 — 물체 좌표로는 기울어진 부표·통의 축을 모른다.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형, 굽기 DIFFUSE(Metallic 0).
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import gen_punk  # noqa: E402  (UV·굽기 재사용)
from shops_common import _link, _live_objects, _math, _mix, _node, _noise, _ramp  # noqa: E402

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")

SPECS = {
    "부두_잔교_짧은": dict(kind="pier", length=14.0, tris=1000, seed=3),
    "부두_잔교_긴": dict(kind="pier", length=24.0, tris=1500, seed=5, extras=True),
    "부두_계선주": dict(kind="bollard", tris=200),
    # length는 뱃머리~널판 선미 비율 전(선체는 그 97%) — 규격 9~11은 assert가 선체 실제 길이로 잰다
    "부두_목선_01": dict(kind="boat", length=9.6, width=2.6, tris=1200, seed=4, cargo=False,
                     paint=((0.06, 0.13, 0.2, 1), (0.11, 0.21, 0.29, 1))),
    "부두_목선_02": dict(kind="boat", length=11.0, width=3.0, tris=1200, seed=8, cargo=True,
                     paint=((0.2, 0.05, 0.032, 1), (0.3, 0.085, 0.05, 1))),
    "부두_부표더미": dict(kind="buoys", tris=500),
}
PIER_HALF, PILE_X, PILE_R = 2.5, 2.2, 0.26
BOLLARD = [(0.6, 0.0), (0.6, 0.12), (0.38, 0.16), (0.32, 0.7), (0.36, 1.2), (0.5, 1.3), (0.48, 1.5)]


class DockMesh:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.band = self.bm.verts.layers.float.new("띠")
        self.mats = []

    def m(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def vert(self, co, band=0.0):
        v = self.bm.verts.new(co)
        v[self.band] = band
        return v

    def face(self, verts, mat, want):
        """want 쪽을 보게 감김을 맞춘다(뉴얼 법선)."""
        n = Vector()
        cos = [v.co for v in verts]
        for i, p in enumerate(cos):
            q = cos[(i + 1) % len(cos)]
            n += Vector(((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y)))
        if n.dot(Vector(want)) < 0:
            verts = list(reversed(verts))
        f = self.bm.faces.new(verts)
        f.material_index = self.m(mat)
        return f


def _center(verts):
    return sum((v.co for v in verts), Vector()) / len(verts)


# ──────────────────────────────────────────────────────────── 짓는 도구

BOX_FACES = {"bottom": ((0, 3, 2, 1), (0, 0, -1)), "top": ((4, 5, 6, 7), (0, 0, 1)), "-y": ((0, 1, 5, 4), (0, -1, 0)),
             "+y": ((2, 3, 7, 6), (0, 1, 0)), "-x": ((3, 0, 4, 7), (-1, 0, 0)), "+x": ((1, 2, 6, 5), (1, 0, 0))}


def box(mesh, x0, x1, y0, y1, z0, z1, mat, skip=()):
    x0, x1 = sorted((x0, x1))
    y0, y1 = sorted((y0, y1))
    z0, z1 = sorted((z0, z1))
    c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    v = [mesh.vert(p) for p in c]
    for name, (ids, want) in BOX_FACES.items():
        if name not in skip:
            mesh.face([v[i] for i in ids], mat, want)


def prism(mesh, p0, p1, width, height, mat, up=(0, 0, 1)):
    """두 점 사이 직사각 기둥(버팀대·오어)."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = (p1 - p0).normalized()
    side = axis.cross(Vector(up))
    side = side.normalized() if side.length > 1e-6 else axis.orthogonal().normalized()
    upv = side.cross(axis).normalized()
    offs = [side * (sx * width / 2) + upv * (sz * height / 2) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    a = [mesh.vert(p0 + o) for o in offs]
    b = [mesh.vert(p1 + o) for o in offs]
    mid = (p0 + p1) / 2
    for i in range(4):
        j = (i + 1) % 4
        vs = [a[i], a[j], b[j], b[i]]
        c = _center(vs)
        mesh.face(vs, mat, c - (mid + axis * axis.dot(c - mid)))
    mesh.face(a, mat, -axis)
    mesh.face(b, mat, axis)


def cyl(mesh, p0, p1, radius, sides, mat, caps=(False, True)):
    p0, p1 = Vector(p0), Vector(p1)
    axis = (p1 - p0).normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u)
    ring = [u * math.cos(2 * math.pi * i / sides) + w * math.sin(2 * math.pi * i / sides) for i in range(sides)]
    a = [mesh.vert(p0 + d * radius) for d in ring]
    b = [mesh.vert(p1 + d * radius) for d in ring]
    for i in range(sides):
        j = (i + 1) % sides
        mesh.face([a[i], a[j], b[j], b[i]], mat, ring[i] + ring[j])
    if caps[0]:
        mesh.face(a, mat, -axis)
    if caps[1]:
        mesh.face(b, mat, axis)


def lathe(mesh, profile, sides, mat, matrix, bands=None, cap_bottom=False, cap_top=True, mats=None):
    """(반지름, 높이) 단면을 국소 z축으로 돌린 몸통. 감김은 단면 방향에서 바로 나온다: 법선 ∝ (방사·dz, −dr)."""
    rot = matrix.to_3x3()
    angles = [2 * math.pi * i / sides for i in range(sides)]
    rings = [[mesh.vert(matrix @ Vector((r * math.cos(a), r * math.sin(a), z)), bands[k] if bands else 0.0) for a in angles]
             for k, (r, z) in enumerate(profile)]
    for k in range(len(rings) - 1):
        dr, dz = profile[k + 1][0] - profile[k][0], profile[k + 1][1] - profile[k][1]
        for i in range(sides):
            j = (i + 1) % sides
            am = (angles[i] + angles[j]) / 2 if j else angles[i] + math.pi / sides
            want = rot @ Vector((math.cos(am) * dz, math.sin(am) * dz, -dr))
            mesh.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], mats[k] if mats else mat, want)
    if cap_bottom:
        mesh.face(rings[0], mats[0] if mats else mat, rot @ Vector((0, 0, -1)))
    if cap_top:
        mesh.face(rings[-1], mats[-1] if mats else mat, rot @ Vector((0, 0, 1)))


def torus(mesh, center, axis, major, minor, seg, minor_seg, mat):
    center, axis = Vector(center), Vector(axis).normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u)
    grid, radials = [], []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        radial = u * math.cos(a) + w * math.sin(a)
        radials.append(radial)
        grid.append([mesh.vert(center + radial * major + (radial * math.cos(2 * math.pi * j / minor_seg)
                                                          + axis * math.sin(2 * math.pi * j / minor_seg)) * minor)
                     for j in range(minor_seg)])
    for i in range(seg):
        i2 = (i + 1) % seg
        ring_mid = center + (radials[i] + radials[i2]).normalized() * major
        for j in range(minor_seg):
            j2 = (j + 1) % minor_seg
            vs = [grid[i][j], grid[i2][j], grid[i2][j2], grid[i][j2]]
            mesh.face(vs, mat, _center(vs) - ring_mid)


def lump(mesh, center, scale, floor_z, mat, subdivisions=2):
    """찌그러뜨린 이코구 — 그물 더미."""
    made = bmesh.ops.create_icosphere(mesh.bm, subdivisions=subdivisions, radius=1.0)["verts"]
    for v in made:
        p = v.co.copy()
        bump = 1.0 + 0.16 * math.sin(p.x * 5.0 + p.y * 3.0) * math.cos(p.z * 4.0)
        v.co = Vector((p.x * scale[0] * bump + center[0], p.y * scale[1] * bump + center[1],
                       max(p.z, -0.3) * scale[2] * bump + center[2]))
        v.co.z = max(v.co.z, floor_z)
        v[mesh.band] = 0.0
    for f in {f for v in made for f in v.link_faces}:
        f.material_index = mesh.m(mat)


# ──────────────────────────────────────────────────────────── 모양

def bollard(mesh, base, scale, iron, rope=None, sides=10):
    lathe(mesh, [(r * scale, z * scale) for r, z in BOLLARD], sides, iron, Matrix.Translation(base))
    if rope:
        # 허리(z 0.56의 반지름 약 0.336)에 감긴 밧줄 한 바퀴, 살짝 기울여 감긴 느낌
        torus(mesh, Vector(base) + Vector((0, 0, 0.56 * scale)), (0.14, 0.0, 1.0), 0.41 * scale, 0.075 * scale, 10, 3, rope)


def make_bollard(name, cfg):
    mesh = DockMesh()
    bollard(mesh, (0, 0, 0), 1.0, name + "_쇠", rope=name + "_밧줄")
    return mesh


def make_pier(name, cfg):
    mesh = DockMesh()
    rnd = random.Random(cfg["seed"])
    deck, wood, iron = name + "_널", name + "_말뚝", name + "_쇠"
    L = cfg["length"]
    n = round(L / 0.62)
    pitch = L / n
    cfg["pitch"], cfg["plank"] = pitch, pitch - 0.06
    # 갑판 널 — 폭 방향으로 깔고 틈 0.06, 닳아 조금씩 가라앉은 널(섬 쪽 첫 널 윗면만 정확히 z 0 = 원점)
    for i in range(n):
        y1 = -i * pitch
        top = 0.0 if i == 0 else -rnd.uniform(0.0, 0.03)
        xl, xr = -PILE_X - rnd.uniform(-0.05, 0.08), PILE_X + rnd.uniform(-0.05, 0.08)
        box(mesh, xl, xr, y1 - cfg["plank"], y1, top - 0.16, top, deck, skip=("bottom",))
    y_end = -(n - 1) * pitch - cfg["plank"]
    for sx in (-1.95, 0.0, 1.95):                                               # 세로 멍에
        box(mesh, sx - 0.15, sx + 0.15, y_end, 0.0, -0.5, -0.16, wood)
    rows = max(2, round(L / 3.6) + 1)
    ys = [-0.5 - (L - 1.0) * k / (rows - 1) for k in range(rows)]
    for y in ys:
        box(mesh, -2.45, 2.45, y - 0.18, y + 0.18, -0.84, -0.5, wood)           # 가로 보
        for sx in (-PILE_X, PILE_X):
            cyl(mesh, (sx, y, -4.0), (sx, y, 0.25), PILE_R, 8, wood)             # 말뚝 — 갑판 위로 조금 솟는다
    for k in range(rows - 1):                                                    # 옆 버팀대(지그재그)
        za, zb = (-0.95, -2.9) if k % 2 == 0 else (-2.9, -0.95)
        for sx in (-PILE_X, PILE_X):
            prism(mesh, (sx, ys[k] - 0.3, za), (sx, ys[k + 1] + 0.3, zb), 0.16, 0.2, wood, up=(1, 0, 0))
    if cfg.get("extras"):
        for sx in (-1.5, 1.5):                                                   # 끝 계선주 둘
            bollard(mesh, (sx, -L + 1.3, 0.0), 0.72, iron)
        yl0, yl1 = -L, -L + 0.1                                                  # 끝면 사다리 — 물속으로
        for rx in (0.45, 1.35):
            box(mesh, rx - 0.05, rx + 0.05, yl0, yl1, -3.0, 0.9, iron)
        for k in range(7):
            zr = -0.35 - 0.4 * k
            box(mesh, 0.5, 1.3, yl0, yl1, zr - 0.04, zr + 0.04, iron, skip=("-x", "+x"))
    return mesh


def make_boat(name, cfg):
    mesh = DockMesh()
    hull, board = name + "_선체", name + "_안판"
    L, W = cfg["length"], cfg["width"]
    NS, NU, IB = 12, 8, 2
    half = NS // 2
    ss = [-1 + i / half for i in range(half)] + [0.94 * i / half for i in range(half + 1)]   # 뱃머리 −1 … 고물(널판 선미) 0.94

    def w(s):
        return max(W / 2 * max(0.0, 1 - abs(s) ** 2.3) ** 0.55, 0.04)

    def d(s):
        return -0.8 * (1 - (0.7 if s < 0 else 0.5) * abs(s) ** 3)

    def h(s):
        return 0.55 + (0.5 if s < 0 else 0.3) * s * s

    shift = -(ss[0] + ss[-1]) * L / 4

    def y_of(s):
        return s * L / 2 + shift

    def shape(s, inner):
        return (max(w(s) - 0.09, 0.02), d(s) + 0.16, h(s) - 0.03) if inner else (w(s), d(s), h(s))

    def point(s, j, inner):
        ww, dd, hh = shape(s, inner)
        th = -math.pi / 2 + math.pi * j / NU
        t = min(1.0, 1 - math.cos(th))
        return Vector((ww * math.sin(th), y_of(s), dd + (hh - dd) * t ** 1.2))

    def inner_z(s, x):
        ww, dd, hh = shape(s, True)
        th = math.asin(min(1.0, abs(x) / ww))
        return dd + (hh - dd) * (1 - math.cos(th)) ** 1.2

    def inner_half_width(s, z):
        ww, dd, hh = shape(s, True)
        t = min(max((z - dd) / (hh - dd), 0.0), 1.0) ** (1 / 1.2)
        return ww * math.sin(math.acos(1 - t))

    O = [[mesh.vert(point(s, j, False), 1.0 if j in (0, NU) else 0.0) for j in range(NU + 1)] for s in ss]
    I = {i: [mesh.vert(point(ss[i], j, True)) for j in range(NU + 1)] for i in range(IB, NS + 1)}
    for i in range(NS):
        for j in range(NU):
            th = -math.pi / 2 + math.pi * (j + 0.5) / NU
            mesh.face([O[i][j], O[i + 1][j], O[i + 1][j + 1], O[i][j + 1]], hull, (math.sin(th), 0, -math.cos(th)))
    for i in range(IB, NS):
        for j in range(NU):
            th = -math.pi / 2 + math.pi * (j + 0.5) / NU
            mesh.face([I[i][j], I[i][j + 1], I[i + 1][j + 1], I[i + 1][j]], board, (-math.sin(th), 0, math.cos(th)))
        for j in (0, NU):                                                        # 뱃전 윗면
            mesh.face([O[i][j], O[i + 1][j], I[i + 1][j], I[i][j]], hull, (0, 0, 1))
    for i in range(IB):                                                          # 이물 갑판
        mesh.face([O[i][0], O[i][NU], O[i + 1][NU], O[i + 1][0]], board, (0, 0, 1))
    mesh.face([O[IB][0]] + I[IB] + [O[IB][NU]], board, (0, 1, 0))                # 이물 칸막이
    mesh.face(O[NS], hull, (0, 1, 0))                                            # 널판 선미 바깥
    mesh.face(I[NS], board, (0, -1, 0))                                          # 널판 선미 안쪽
    # 가로 앉을 판 둘 + 바닥 널
    for s in (-0.3, 0.4):
        z_top = h(s) - 0.3
        hw = inner_half_width(s, z_top - 0.08) * 0.98
        box(mesh, -hw, hw, y_of(s) - 0.2, y_of(s) + 0.2, z_top - 0.08, z_top, board)
    zf = max(inner_z(s, 0.42) for s in [-0.5 + 1.15 * k / 20 for k in range(21)]) + 0.03
    box(mesh, -0.42, 0.42, y_of(-0.5), y_of(0.65), zf - 0.04, zf, board, skip=("bottom",))
    cfg["floor"] = zf
    if not cfg["cargo"]:
        # 🔴 1차는 오어가 안판 재질이라 겹판 이음 줄(z 띠)에 통째로 걸려 검은 막대로 읽혔다 — 오어 재질을 따로
        for sx in (-1, 1):                                                       # 앉을 판 위에 누운 오어 둘
            z = h(0.0) - 0.3 + 0.05
            p0, p1 = Vector((sx * 0.28, y_of(-0.55), z)), Vector((sx * 0.5, y_of(0.35), z))
            prism(mesh, p0, p1, 0.1, 0.08, name + "_오어")
            tip = p1 + (p1 - p0).normalized() * 1.1
            prism(mesh, p1, tip, 0.3, 0.04, name + "_오어")
    else:
        lump(mesh, (-0.1, y_of(-0.15), zf + 0.28), (0.95, 1.4, 0.4), zf, name + "_그물")
        barrel = [(0.3, 0.0), (0.34, 0.1), (0.35, 0.18), (0.38, 0.45), (0.35, 0.72), (0.34, 0.8), (0.3, 0.9)]
        hoops = [0.0, 1.0, 1.0, 0.0, 1.0, 1.0, 0.0]
        for sx in (-0.42, 0.42):
            lathe(mesh, barrel, 10, name + "_통", Matrix.Translation((sx, y_of(0.52), zf)), bands=hoops)
        torus(mesh, (0.35, y_of(-0.55), zf + 0.08), (0, 0, 1), 0.33, 0.07, 12, 3, name + "_밧줄")
    return mesh


def make_buoys(name, cfg):
    mesh = DockMesh()
    buoy, rope = name + "_부표", name + "_밧줄"
    shape = [(0.1, -0.45), (0.26, -0.38), (0.34, -0.2), (0.34, 0.2), (0.26, 0.38), (0.1, 0.45)]
    lying = Matrix.Rotation(math.radians(90), 4, "Y")
    for (x, y, z), yaw, tilt, scale, flip in (((-0.5, 0.2, 0.34), 20, lying, 1.0, 0.0), ((0.38, -0.42, 0.34), -35, lying, 1.0, 1.0),
                                               ((0.05, 0.35, 0.68), 70, Matrix.Rotation(math.radians(55), 4, "Y"), 1.0, 0.0),
                                               ((0.8, 0.6, 0.27), 100, lying, 0.8, 1.0)):
        matrix = Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, "Z") @ tilt @ Matrix.Scale(scale, 4)
        lathe(mesh, shape, 8, buoy, matrix, bands=[k / 5 + flip for k in range(6)], cap_bottom=True, cap_top=True)
    torus(mesh, (-0.45, -0.7, 0.06), (0, 0, 1), 0.5, 0.06, 12, 3, rope)               # 바닥에 사린 밧줄
    torus(mesh, (-0.4, -0.66, 0.18), (0.12, 0.0, 1.0), 0.38, 0.06, 8, 3, rope)
    low = min(v.co.z for v in mesh.bm.verts)
    for v in mesh.bm.verts:
        v.co.z -= low
    return mesh


MAKERS = {"pier": make_pier, "bollard": make_bollard, "boat": make_boat, "buoys": make_buoys}


# ──────────────────────────────────────────────────────────── 셰이더

def _shade(mat, cfg):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    kind = mat.name.rsplit("_", 1)[1]

    def bands(scale, direction="X", distortion=0.3):
        wave = _node(nt, "ShaderNodeTexWave", Scale=scale, Distortion=distortion)
        wave.wave_type, wave.bands_direction = "BANDS", direction
        _link(nt, co, wave.inputs["Vector"])
        return wave.outputs["Fac"]

    def game(axis):                      # 물체 좌표(m) → 게임 단위
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

    def band_attr():
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "띠"
        return attr.outputs["Fac"]

    def rust(color, scale=40.0, lo=0.6):
        spots = _ramp(nt, _noise(nt, co, scale, detail=8.0), [(lo, (0, 0, 0, 1)), (lo + 0.1, (1, 1, 1, 1))])
        return _mix(nt, spots, color, (0.12, 0.05, 0.02, 1))

    if kind == "널":
        # 🔴 1차(0.12~0.23 · 닳은 길 0.3 · 못 반지름 0.05/녹 0.1)는 직사광에서 허연 판자에 징이 줄지어 박힌 것처럼 읽혔다
        # — 판 명도를 내리고 못은 작게(머리 0.03, 녹 번짐 0.055)
        board = _mix(nt, stretched((0.1, 1.0, 1.0), 45.0), (0.085, 0.07, 0.052, 1), (0.17, 0.14, 0.1, 1))
        row = _math(nt, "MULTIPLY", game(1), -1.0 / cfg["pitch"])
        tone = _math(nt, "FRACT", _math(nt, "MULTIPLY", _math(nt, "FLOOR", row, 0.0), 0.37), 0.0)
        board = _mix(nt, _math(nt, "MULTIPLY", tone, 0.7), board, (0.12, 0.11, 0.095, 1))          # 널마다 바랜 정도
        ax = _math(nt, "ABSOLUTE", game(0), 0.0)
        worn = _math(nt, "MULTIPLY", span(ax, 1.4, 0.0), _noise(nt, co, 12.0))                    # 가운데 밟혀 닳은 길
        board = _mix(nt, _ramp(nt, worn, [(0.25, (0, 0, 0, 1)), (0.6, (0.7, 0.7, 0.7, 1))]), board, (0.2, 0.17, 0.125, 1))
        board = _mix(nt, span(ax, 1.8, 2.3), board, (0.05, 0.043, 0.032, 1))                        # 물 튄 끝 짙게
        # 못 — 멍에(x 0·±1.95) 위, 널 가운데
        dy = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", _math(nt, "FRACT", row, 0.0), cfg["plank"] / cfg["pitch"] / 2), cfg["pitch"])
        dx = _math(nt, "MINIMUM", ax, _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", ax, 1.95), 0.0))
        dist = _math(nt, "SQRT", _math(nt, "ADD", _math(nt, "MULTIPLY", dx, dx), _math(nt, "MULTIPLY", dy, dy)), 0.0)
        board = _mix(nt, _ramp(nt, dist, [(0.03, (0.7, 0.7, 0.7, 1)), (0.055, (0, 0, 0, 1))]), board, (0.08, 0.04, 0.02, 1))
        color = _mix(nt, _ramp(nt, dist, [(0.018, (1, 1, 1, 1)), (0.03, (0, 0, 0, 1))]), board, (0.02, 0.018, 0.016, 1))
    elif kind == "말뚝":
        wood = _mix(nt, stretched((1.0, 1.0, 0.1), 40.0), (0.055, 0.042, 0.03, 1), (0.11, 0.085, 0.058, 1))
        wet = _math(nt, "MULTIPLY", span(game(2), 0.0, -1.6), _noise(nt, co, 10.0))
        wood = _mix(nt, _ramp(nt, wet, [(0.2, (0, 0, 0, 1)), (0.55, (1, 1, 1, 1))]), wood, (0.028, 0.042, 0.028, 1))
        vor = _node(nt, "ShaderNodeTexVoronoi", Scale=120.0)
        _link(nt, co, vor.inputs["Vector"])
        belt = _math(nt, "MULTIPLY", span(game(2), -0.3, -0.8), span(game(2), -2.6, -1.8))          # 따개비 띠
        barn = _math(nt, "MULTIPLY", _ramp(nt, vor.outputs["Distance"], [(0.05, (1, 1, 1, 1)), (0.09, (0, 0, 0, 1))]), belt)
        color = _mix(nt, barn, wood, (0.22, 0.21, 0.18, 1))
    elif kind == "쇠":
        color = rust(_mix(nt, _noise(nt, co, 30.0), (0.018, 0.018, 0.02, 1), (0.045, 0.045, 0.048, 1)))
    elif kind == "밧줄":
        twist = bands(60.0, "DIAGONAL", 1.0)
        color = _mix(nt, _ramp(nt, twist, [(0.3, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))]), (0.17, 0.12, 0.065, 1), (0.34, 0.26, 0.15, 1))
    elif kind == "선체":
        paint = _mix(nt, _noise(nt, co, 20.0), cfg["paint"][0], cfg["paint"][1])
        chip = _ramp(nt, _noise(nt, co, 35.0, detail=8.0), [(0.6, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
        paint = _mix(nt, chip, paint, (0.17, 0.13, 0.085, 1))                                    # 벗겨진 칠
        strake = _ramp(nt, bands(11.0, "Z", 0.0), [(0.0, (1, 1, 1, 1)), (0.08, (1, 1, 1, 1)), (0.16, (0, 0, 0, 1))])
        color = _mix(nt, strake, paint, (0.03, 0.028, 0.025, 1))                                 # 겹판 이음
        color = _mix(nt, _ramp(nt, band_attr(), [(0.62, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))]), color, (0.46, 0.43, 0.37, 1))
        algae = _ramp(nt, span(game(2), 0.25, 0.0), [(0.0, (0, 0, 0, 1)), (1.0, (1, 1, 1, 1))])
        color = _mix(nt, _math(nt, "MULTIPLY", algae, _noise(nt, co, 14.0)), color, (0.05, 0.075, 0.035, 1))
        color = _mix(nt, _ramp(nt, span(game(2), 0.02, -0.08), [(0.0, (0, 0, 0, 1)), (1.0, (1, 1, 1, 1))]), color,
                     (0.03, 0.026, 0.022, 1))                                                    # 흘수선 아래 타르
    elif kind == "안판":
        wood = _mix(nt, stretched((1.0, 0.12, 1.0), 40.0), (0.14, 0.105, 0.068, 1), (0.25, 0.19, 0.12, 1))
        gap = _ramp(nt, bands(11.0, "Z", 0.0), [(0.0, (1, 1, 1, 1)), (0.06, (1, 1, 1, 1)), (0.13, (0, 0, 0, 1))])
        wood = _mix(nt, gap, wood, (0.035, 0.028, 0.02, 1))
        color = _mix(nt, span(game(2), -0.2, -0.7), wood, (0.05, 0.04, 0.03, 1))                   # 바닥에 고인 물때
    elif kind == "오어":
        color = _mix(nt, stretched((1.0, 0.1, 1.0), 50.0), (0.24, 0.18, 0.11, 1), (0.36, 0.28, 0.17, 1))
    elif kind == "그물":
        strands = _ramp(nt, bands(55.0, "DIAGONAL", 2.0), [(0.0, (1, 1, 1, 1)), (0.12, (0, 0, 0, 1))])
        base = _mix(nt, _noise(nt, co, 10.0), (0.05, 0.06, 0.045, 1), (0.1, 0.1, 0.07, 1))
        color = _mix(nt, strands, base, (0.28, 0.26, 0.19, 1))
    elif kind == "통":
        stave = _ramp(nt, bands(20.0, "X", 0.0), [(0.0, (1, 1, 1, 1)), (0.06, (1, 1, 1, 1)), (0.12, (0, 0, 0, 1))])
        wood = _mix(nt, _noise(nt, co, 30.0), (0.2, 0.13, 0.07, 1), (0.3, 0.2, 0.11, 1))
        wood = _mix(nt, stave, wood, (0.05, 0.035, 0.02, 1))
        hoop = _ramp(nt, band_attr(), [(0.9, (0, 0, 0, 1)), (0.97, (1, 1, 1, 1))])
        color = _mix(nt, hoop, wood, rust(_mix(nt, _noise(nt, co, 50.0), (0.03, 0.028, 0.026, 1), (0.06, 0.055, 0.05, 1))))
    elif kind == "부표":
        parity = _math(nt, "MODULO", _math(nt, "FLOOR", _math(nt, "MULTIPLY", band_attr(), 5.0), 0.0), 2.0)
        color = _mix(nt, parity, (0.46, 0.045, 0.028, 1), (0.6, 0.58, 0.52, 1))
        dirt = _ramp(nt, _noise(nt, co, 25.0, detail=8.0), [(0.58, (0, 0, 0, 1)), (0.72, (0.8, 0.8, 0.8, 1))])
        color = _mix(nt, dirt, color, (0.16, 0.14, 0.1, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    _link(nt, color, bsdf.inputs["Base Color"])


def tex_size(name):
    if name.endswith(("_널", "_선체")) or name == "부두_잔교_긴_말뚝":
        return 1024
    return 512


# ──────────────────────────────────────────────────────────── 조립·내보내기

def assemble(name, collection):
    cfg = SPECS[name]
    mesh = MAKERS[cfg["kind"]](name, cfg)
    bm = mesh.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs, ys, zs = ([v.co[k] for v in bm.verts] for k in range(3))
    xs, ys, zs = list(xs), list(ys), list(zs)
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    assert tri_count <= cfg["tris"], f"{name} 삼각형 {tri_count} > {cfg['tris']}"
    kind = cfg["kind"]
    if kind == "pier":
        L = cfg["length"]
        assert max(abs(x) for x in xs) <= PIER_HALF + 1e-3, f"{name} 폭 5를 넘었다: {max(abs(x) for x in xs):.2f}"
        assert min(ys) >= -L - 1e-3 and max(ys) <= 1e-3, f"{name} 길이 0~−{L} 밖: y {min(ys):.2f}~{max(ys):.2f}"
        assert abs(min(zs) + 4.0) < 1e-3 and max(zs) <= 1.2, f"{name} 높이 {min(zs):.2f}~{max(zs):.2f} (말뚝 −4, 갑판 0)"
        deck = [v.co.z for f in bm.faces if f.material_index == mesh.m(name + "_널") for v in f.verts]
        assert abs(max(deck)) < 1e-4, f"{name} 갑판 윗면 {max(deck):.3f} ≠ 0 (원점)"
    elif kind == "boat":
        length, width = max(ys) - min(ys), max(xs) - min(xs)
        assert 9.0 - 1e-3 <= length <= 11.0 + 1e-3 and width <= cfg["width"] + 1e-3, f"{name} 길이 {length:.2f} 폭 {width:.2f}"
        assert abs(min(zs) + 0.8) < 1e-3 and max(zs) <= 1.5, f"{name} 높이 {min(zs):.2f}~{max(zs):.2f} (흘수선 0, 바닥 −0.8)"
        assert abs(max(ys) + min(ys)) < 1e-3 and abs(max(xs) + min(xs)) < 1e-3, f"{name} 원점이 가운데가 아니다"
    else:
        reach = max(math.hypot(x, y) for x, y in zip(xs, ys))
        limit, height = (0.6, (1.2, 1.5)) if kind == "bollard" else (1.5, (0.8, 1.5))
        assert reach <= limit + 1e-3, f"{name} 지름 {2 * limit}을 넘었다: 반지름 {reach:.2f}"
        assert abs(min(zs)) < 1e-3 and height[0] <= max(zs) <= height[1] + 1e-3, f"{name} 높이 {min(zs):.2f}~{max(zs):.2f}"
    info = dict(tris=tri_count, x=(min(xs), max(xs)), y=(min(ys), max(ys)), z=(min(zs), max(zs)))
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    for mat_name in mesh.mats:
        mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        _shade(mat, cfg)
        data.materials.append(mat)
    return obj, info


def build_in_window(name, collection, folder):
    obj, info = assemble(name, collection)
    gen_punk.unwrap(obj)
    gen_punk.tex_size = tex_size
    gen_punk.bake(obj, folder)
    return obj, info


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(SPECS)
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        obj, info = build_in_window(name, col, os.path.join(OUT, "Textures"))
        for o in _live_objects():
            o.select_set(o is obj)
        bpy.context.view_layer.objects.active = obj
        path = os.path.join(OUT, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        fmt = lambda pair: f"{pair[0]:.2f}~{pair[1]:.2f}"  # noqa: E731
        print(f"만듦  {name}  삼각형 {info['tris']}  x {fmt(info['x'])}  y {fmt(info['y'])}  z {fmt(info['z'])}  "
              f"재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
