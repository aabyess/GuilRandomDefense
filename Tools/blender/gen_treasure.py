"""보물 소품 넷 — 보물찾기 발견 연출(PM 배정 2026-09-13, 구현담당2 무응답으로 blender가 인수). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 옛 gen_props.py는 저장소 안이라 못 읽어 새로 짓는다.
화면 없이(정본):  blender --background --factory-startup --python gen_treasure.py [-- chest open coins mark]
사장님 창(보여 주기만): ns["build_in_window"]("chest", 컬렉션, 창 텍스처 폴더) → (뼈대 또는 None, 메시, 정보)

규격(PM, 지난 검수에서 정한 그대로):
- 공통: 원점 바닥 가운데, 최저점 0, 정면 −Y, 뒷면 검사 둘 0건, 삼각형 채당 2,500 이하, 헤드리스 정본. 이름은 NAMES 한 곳.
- 보물상자: 몸통 8×5.5×6(몸통 4 + 둥근 뚜껑 2). **속 빈 몸통**(벽 0.35·안 바닥 0.4), 뚜껑은 두께 0.25 껍질 + 어두운 안감,
  쇠띠는 **겉을 감싸는 굽은 띠**(원판 금지). 뼈 Root + Lid(뒤 윗모서리 경첩). 클립 `Idle`(2초 반복, 뚜껑이 살짝 들썩)과
  `Open`(1.5초, 끝에서 느려짐 = 1−(1−t)³, 끝 자세 유지). Open 최대 각은 뚜껑이 바닥(0) 위에 머무는 각 −100°(assert).
  🔴 뒷면 검사는 Open 끝 자세에서도(check_scratch.py --pose=Open). 클립 이름은 FBX를 다시 읽어 「…|Idle」·「…|Open」 확인.
- 보물상자_열림: 같은 모델을 Open 끝 각으로 굳힌 정지 판 + 몸통 안 봉긋한 금화 + 테두리 넘어 흘러내린 몇 닢 + 보석 셋.
- 금화더미: 지름 7·높이 2.5, 진한 금색(굽기 Metallic 0), 동전 결 봉우리 + 흩어진 몇 닢. 🔴 재질 이름 assert(빨간 원판 뒤바뀜 재발 금지).
- 보물표시: 짙은 붉은 갈색 흙더미(가운데 1.2) 위에 흰 돌 X, 지름 약 6.
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

import gen_ships  # noqa: E402  (정점 가중치 메시·UV·굽기·뼈대)
from gen_docks import BOX_FACES, box, cyl, lathe, torus  # noqa: E402
from gen_ships import ShipMesh  # noqa: E402
from shops_common import _link, _live_objects, _math, _mix, _node, _noise, _ramp  # noqa: E402

NAMES = {"chest": "보물상자", "open": "보물상자_열림", "coins": "금화더미", "mark": "보물표시"}   # 파일명은 여기만

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Props")
TRI_LIMIT = 2500
FPS, IDLE_FRAMES, OPEN_FRAMES = 30, 60, 45
W, D, H, RISE, WALL, LID_T, BAND = 8.0, 5.5, 4.0, 2.0, 0.35, 0.25, 0.07
HX, HY = W / 2, D / 2
HINGE = Vector((0.0, HY, H))
OPEN_DEG = -100.0                 # +X축 기준 음수 = 앞 가장자리가 위·뒤로 넘어간다
N_ARC = 14


class ChestMesh(ShipMesh):
    """기본 가중치를 바꿔 가며 짓는다(몸통 Root / 뚜껑 Lid) — 뚜껑 정점은 따로 모아 열림 판에서 돌린다."""

    def __init__(self, groups):
        super().__init__(groups)
        self.default = {0: 1.0}
        self.lid = []

    def vert(self, co, band=0.0, weights=None):
        w = weights or self.default
        v = super().vert(co, band, w)
        if w.get(1):
            self.lid.append(v)
        return v


def quad(mesh, pts, mat, want):
    return mesh.face([mesh.vert(p) for p in pts], mat, want)


def inner_box(mesh, x0, x1, y0, y1, z0, z1, mat):
    """안을 보는 상자(윗면 없음) — 속 빈 몸통의 안벽과 안 바닥."""
    c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    v = [mesh.vert(p) for p in c]
    for name, (ids, want) in BOX_FACES.items():
        if name != "top":
            mesh.face([v[i] for i in ids], mat, tuple(-a for a in want))


def coin(mesh, center, axis, mat, radius=0.34, thick=0.09, on_ground=False):
    axis = Vector(axis).normalized()
    center = Vector(center)
    if on_ground:                 # 기울어진 동전의 가장 낮은 점이 바닥에 닿게
        center.z = radius * math.sqrt(max(0.0, 1 - axis.z ** 2)) + thick / 2 * abs(axis.z)
    cyl(mesh, center - axis * thick / 2, center + axis * thick / 2, radius, 12, mat, caps=(True, True))


def gem(mesh, center, band, mat, tilt=(0.2, 0.1, 1.0), scale=1.0):
    rot = Vector(tilt).normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
    lathe(mesh, [(0.04, -0.32), (0.42, 0.0), (0.3, 0.2)], 8, mat, Matrix.Translation(center) @ rot @ Matrix.Scale(scale, 4),
          bands=[band] * 3, cap_bottom=True, cap_top=True)


# ──────────────────────────────────────────────────────────── 보물상자

def build_chest(name, opened, filled):
    mesh = ChestMesh(["Root", "Lid"])
    m = {k: f"{name}_{k}" for k in ("나무", "쇠", "안감", "금화", "보석")}
    wood, iron, lining = m["나무"], m["쇠"], m["안감"]
    ix, iy = HX - WALL, HY - WALL
    # ── 몸통: 겉벽 · 안벽/안 바닥(안감) · 윗단 테두리
    box(mesh, -HX, HX, -HY, HY, 0.0, H, wood, skip=("bottom", "top"))
    inner_box(mesh, -ix, ix, -iy, iy, 0.4, H, lining)
    for pts in (((-HX, -HY), (HX, -HY), (ix, -iy), (-ix, -iy)), ((HX, HY), (-HX, HY), (-ix, iy), (ix, iy)),
                ((-HX, HY), (-HX, -HY), (-ix, -iy), (-ix, iy)), ((HX, -HY), (HX, HY), (ix, iy), (ix, -iy))):
        quad(mesh, [(x, y, H) for x, y in pts], wood, (0, 0, 1))
    # ── 몸통 쇠띠: 앞뒤 세로 띠 둘씩 + 아랫단을 한 바퀴 감는 띠(세로 띠보다 두꺼워 겹쳐도 앞에 선다) · 자물쇠판 · 옆 손잡이
    for bx in (-2.6, 2.6):
        box(mesh, bx - 0.28, bx + 0.28, -HY - BAND, -HY, 0.25, H - 0.02, iron, skip=("+y",))
        box(mesh, bx - 0.28, bx + 0.28, HY, HY + BAND, 0.25, H - 0.02, iron, skip=("-y",))
    low = 0.1
    box(mesh, -HX - low, HX + low, -HY - low, -HY, 0.35, 0.85, iron, skip=("+y",))
    box(mesh, -HX - low, HX + low, HY, HY + low, 0.35, 0.85, iron, skip=("-y",))
    box(mesh, -HX - low, -HX, -HY, HY, 0.35, 0.85, iron, skip=("+x",))
    box(mesh, HX, HX + low, -HY, HY, 0.35, 0.85, iron, skip=("-x",))
    box(mesh, -0.7, 0.7, -HY - 0.1, -HY, 2.4, 3.5, iron, skip=("+y",))
    box(mesh, -0.1, 0.1, -HY - 0.13, -HY - 0.1, 2.7, 3.15, lining, skip=("+y",))            # 열쇠 구멍
    for sx in (-1, 1):
        x0, x1 = (HX, HX + 0.08) if sx > 0 else (-HX - 0.08, -HX)
        box(mesh, x0, x1, -0.3, 0.3, 3.3, 3.7, iron, skip=("-x",) if sx > 0 else ("+x",))
        torus(mesh, (sx * (HX + 0.1), 0.0, 2.85), (1, 0, 0), 0.5, 0.07, 12, 3, iron)
    # ── 뚜껑(Lid 가중치): 둥근 겉판 · 안감 안판 · 양 끝 판 · 아랫단 테두리 · 겉을 감싸는 굽은 쇠띠 · 걸쇠
    mesh.default = {1: 1.0}

    def arc(sy, rise):
        return [(sy * math.cos(math.pi * (1 - k / N_ARC)), H + rise * math.sin(math.pi * (1 - k / N_ARC))) for k in range(N_ARC + 1)]

    outer, inner = arc(HY, RISE), arc(HY - LID_T, RISE - LID_T)
    band = arc(HY + BAND, RISE + BAND)
    lx = HX - LID_T
    for k in range(N_ARC):
        phi = math.pi * (1 - (k + 0.5) / N_ARC)
        out = (0, math.cos(phi), math.sin(phi))
        (ya, za), (yb, zb) = outer[k], outer[k + 1]
        quad(mesh, [(-HX, ya, za), (HX, ya, za), (HX, yb, zb), (-HX, yb, zb)], wood, out)
        (ya, za), (yb, zb) = inner[k], inner[k + 1]
        quad(mesh, [(-lx, ya, za), (lx, ya, za), (lx, yb, zb), (-lx, yb, zb)], lining, tuple(-a for a in out))
    for sx in (-1, 1):
        mesh.face([mesh.vert((sx * HX, y, z)) for y, z in outer], wood, (sx, 0, 0))
        mesh.face([mesh.vert((sx * lx, y, z)) for y, z in inner], lining, (-sx, 0, 0))
    oy, jy = HY, HY - LID_T
    for pts in (((-HX, -oy), (HX, -oy), (lx, -jy), (-lx, -jy)), ((HX, oy), (-HX, oy), (-lx, jy), (lx, jy)),
                ((-HX, oy), (-HX, -oy), (-lx, -jy), (-lx, jy)), ((HX, -oy), (HX, oy), (lx, jy), (lx, -jy))):
        quad(mesh, [(x, y, H) for x, y in pts], wood, (0, 0, -1))
    for bx in (-2.6, 2.6):
        x0, x1 = bx - 0.28, bx + 0.28
        for k in range(N_ARC):
            phi = math.pi * (1 - (k + 0.5) / N_ARC)
            (ya, za), (yb, zb) = band[k], band[k + 1]
            (oa, pa), (ob, pb) = outer[k], outer[k + 1]
            quad(mesh, [(x0, ya, za), (x1, ya, za), (x1, yb, zb), (x0, yb, zb)], iron, (0, math.cos(phi), math.sin(phi)))
            quad(mesh, [(x0, oa, pa), (x0, ya, za), (x0, yb, zb), (x0, ob, pb)], iron, (-1, 0, 0))
            quad(mesh, [(x1, oa, pa), (x1, ya, za), (x1, yb, zb), (x1, ob, pb)], iron, (1, 0, 0))
        for k in (0, N_ARC):
            (ya, za), (oa, pa) = band[k], outer[k]
            quad(mesh, [(x0, oa, pa), (x1, oa, pa), (x1, ya, za), (x0, ya, za)], iron, (0, 0, -1))
    box(mesh, -0.4, 0.4, -HY - 0.12, -HY, H - 0.9, H + 0.35, iron)                         # 걸쇠 — 뚜껑과 함께 들린다
    mesh.default = {0: 1.0}
    info = {}
    if opened:
        rot = Matrix.Translation(HINGE) @ Matrix.Rotation(math.radians(OPEN_DEG), 4, "X") @ Matrix.Translation(-HINGE)
        for v in mesh.lid:
            v.co = rot @ v.co
        info["lid_low"] = min(v.co.z for v in mesh.lid)
        assert info["lid_low"] >= 0.0, f"{name} 열린 뚜껑이 바닥 아래로: {info['lid_low']:.2f}"
    if filled:
        gold, gems = m["금화"], m["보석"]

        def mound(u, v):
            return H - 0.1 + 1.0 * math.sin(math.pi * u) ** 0.7 * math.sin(math.pi * v) ** 0.7 \
                + 0.06 * math.sin(9 * u + 1.3) * math.sin(7 * v + 0.4)

        nx, ny = 12, 8
        grid = [[mesh.vert((-ix + 2 * ix * i / nx, -iy + 2 * iy * j / ny, mound(i / nx, j / ny))) for i in range(nx + 1)]
                for j in range(ny + 1)]
        for j in range(ny):
            for i in range(nx):
                mesh.face([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]], gold, (0, 0, 1))
        coin(mesh, (1.4, -HY - 0.18, H - 0.28), (0.0, -0.78, 0.62), gold)                   # 앞 테두리에 걸쳐 흘러내림
        coin(mesh, (-1.6, -HY + 0.17, H + 0.05), (0.15, 0.0, 1.0), gold)                     # 테두리 위
        coin(mesh, (-0.9, -HY - 0.1, H - 0.55), (0.1, -0.9, 0.4), gold)
        for x, y, axis in ((-1.9, -HY - 1.0, (0.1, 0.05, 1.0)), (0.3, -HY - 1.5, (0.5, -0.2, 1.0)),
                           (2.0, -HY - 0.8, (-0.1, 0.1, 1.0)), (-0.5, -HY - 0.65, (0.0, -1.0, 0.35))):
            coin(mesh, (x, y, 0.0), axis, gold, on_ground=True)
        for (x, y), b, tilt in (((-1.4, 0.3), 0.0, (0.3, 0.1, 1.0)), ((1.0, -0.6), 0.5, (-0.2, 0.3, 1.0)), ((0.3, 1.0), 1.0, (0.1, -0.3, 1.0))):
            u, v = (x + ix) / (2 * ix), (y + iy) / (2 * iy)
            gem(mesh, Vector((x, y, mound(u, v) + 0.15)), b, gems, tilt)
    bones = [("Root", (0, 0, 0), (0, 0, 1.5), None), ("Lid", tuple(HINGE), tuple(HINGE + Vector((0, 0, 1.5))), "Root")]
    return mesh, bones, info


# ──────────────────────────────────────────────────────────── 금화더미 · 보물표시

def _radial_mound(mesh, radius, rings, segments, height_at, mat):
    center = mesh.vert((0.0, 0.0, height_at(0.0, 0.0)))
    grid = []
    for k in range(1, rings + 1):
        r = radius * k / rings
        grid.append([mesh.vert((r * math.cos(2 * math.pi * s / segments), r * math.sin(2 * math.pi * s / segments),
                                height_at(r * math.cos(2 * math.pi * s / segments), r * math.sin(2 * math.pi * s / segments))))
                     for s in range(segments)])
    for s in range(segments):
        t = (s + 1) % segments
        mesh.face([center, grid[0][s], grid[0][t]], mat, (0, 0, 1))
        for k in range(rings - 1):
            mesh.face([grid[k][s], grid[k + 1][s], grid[k + 1][t], grid[k][t]], mat, (0, 0, 1))
    return [center] + [v for ring in grid for v in ring]


def build_coins(name):
    mesh = ShipMesh(["Root"])
    gold = f"{name}_금화"
    R, TOP = 2.9, 2.5
    rng = random.Random(9)
    lumps = [(rng.uniform(-1.4, 1.4), rng.uniform(-1.4, 1.4), rng.uniform(0.6, 1.2), rng.uniform(0.08, 0.2)) for _ in range(7)]

    def raw(x, y):
        r = min(1.0, math.hypot(x, y) / R)
        base = max(0.0, 1 - r * r) ** 0.9
        bump = sum(a * max(0.0, 1 - math.hypot(x - px, y - py) / rad) ** 2 for px, py, rad, a in lumps)
        return (TOP * base + bump) * min(1.0, (1 - r) * 4)

    verts = _radial_mound(mesh, R, 9, 28, raw, gold)
    k = TOP / max(v.co.z for v in verts)
    for v in verts:
        v.co.z *= k

    def height(x, y):
        return raw(x, y) * k

    for i in range(11):                                                      # 둘레에 흩어진 닢
        ang = 2 * math.pi * i / 11 + rng.uniform(-0.2, 0.2)
        r = rng.uniform(2.95, 3.1)
        tilt = (rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 1.0) if i % 3 else (math.cos(ang), math.sin(ang), 0.5)
        coin(mesh, (r * math.cos(ang), r * math.sin(ang), 0.0), tilt, gold, on_ground=True)
    for ang in (0.6, 2.4, 4.3):                                               # 비탈에 누운 닢
        x, y = 1.9 * math.cos(ang), 1.9 * math.sin(ang)
        e = 0.05
        gx, gy = (height(x + e, y) - height(x - e, y)) / (2 * e), (height(x, y + e) - height(x, y - e)) / (2 * e)
        coin(mesh, (x, y, height(x, y) + 0.06), (-gx, -gy, 1.0), gold)
    return mesh


def build_mark(name):
    mesh = ShipMesh(["Root"])
    soil, stone = f"{name}_흙", f"{name}_돌"
    R, TOP = 3.0, 1.2

    def height(x, y):
        r = min(1.0, math.hypot(x, y) / R)
        return TOP * (1 - r * r) ** 1.5 + 0.05 * math.sin(3.1 * x + 0.7) * math.sin(2.7 * y + 1.9) * r * (1 - r)

    _radial_mound(mesh, R, 8, 24, height, soil)
    # 흰 돌 X — 흙더미 표면을 따라 눕힌 두 줄(두께 있음, 밑은 흙에 묻힘). 두 번째 줄을 조금 높여 가운데 겹침이 싸우지 않게
    for ang, lift in ((math.radians(45), 0.12), (math.radians(-45), 0.15)):
        d, n = Vector((math.cos(ang), math.sin(ang))), Vector((-math.sin(ang), math.cos(ang)))
        segs, half_len, half_w = 12, 2.5, 0.42
        rows = []
        for i in range(segs + 1):
            c = d * (-half_len + 2 * half_len * i / segs)
            row = []
            for side in (1, -1):
                p = c + n * (side * half_w)
                row.append((mesh.vert((p.x, p.y, height(p.x, p.y) + lift)), mesh.vert((p.x, p.y, height(p.x, p.y) - 0.05))))
            rows.append(row)
        for i in range(segs):
            (lt0, lb0), (rt0, rb0) = rows[i]
            (lt1, lb1), (rt1, rb1) = rows[i + 1]
            mesh.face([lt0, rt0, rt1, lt1], stone, (0, 0, 1))
            mesh.face([lb0, lt0, lt1, lb1], stone, (n.x, n.y, 0))
            mesh.face([rt0, rb0, rb1, rt1], stone, (-n.x, -n.y, 0))
        for i, sign in ((0, -1), (segs, 1)):
            (lt, lb), (rt, rb) = rows[i]
            mesh.face([lb, rb, rt, lt], stone, (sign * d.x, sign * d.y, 0))
    rng = random.Random(4)
    for ang in (45, -45, 135, -135):                                           # X 끝 쪽에 박힌 흰 돌 몇 개
        for t in (1.2, 2.1):
            a = math.radians(ang)
            p = Vector((t * math.cos(a), t * math.sin(a))) + Vector((-math.sin(a), math.cos(a))) * rng.uniform(-0.5, 0.5)
            made = bmesh.ops.create_icosphere(mesh.bm, subdivisions=1, radius=rng.uniform(0.2, 0.3),
                                              matrix=Matrix.Translation((p.x, p.y, height(p.x, p.y) + 0.18)))["verts"]
            for v in made:
                v.co.z = height(p.x, p.y) + 0.1 + (v.co.z - height(p.x, p.y) - 0.18) * 0.55
                v[mesh.deform][0] = 1.0
                v[mesh.band] = 0.0
            for f in {f for v in made for f in v.link_faces}:
                f.material_index = mesh.m(stone)
    return mesh


# ──────────────────────────────────────────────────────────── 셰이더

def _shade(mat):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    kind = mat.name.rsplit("_", 1)[1]

    def bands(scale, direction):
        wave = _node(nt, "ShaderNodeTexWave", Scale=scale, Distortion=0.0)
        wave.wave_type, wave.bands_direction = "BANDS", direction
        _link(nt, co, wave.inputs["Vector"])
        return wave.outputs["Fac"]

    def voronoi(scale, feature="F1"):
        vor = _node(nt, "ShaderNodeTexVoronoi", Scale=scale)
        vor.feature = feature
        _link(nt, co, vor.inputs["Vector"])
        return vor

    if kind == "나무":
        mp = _node(nt, "ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (0.12, 1.0, 1.0)
        _link(nt, co, mp.inputs["Vector"])
        wood = _mix(nt, _noise(nt, mp.outputs["Vector"], 40.0, detail=6.0), (0.14, 0.075, 0.035, 1), (0.26, 0.15, 0.07, 1))
        planks = _ramp(nt, bands(4.5, "Z"), [(0.0, (1, 1, 1, 1)), (0.05, (1, 1, 1, 1)), (0.1, (0, 0, 0, 1))])
        color = _mix(nt, planks, wood, (0.04, 0.02, 0.01, 1))
    elif kind == "쇠":
        base = _mix(nt, _noise(nt, co, 30.0), (0.025, 0.025, 0.027, 1), (0.06, 0.056, 0.052, 1))
        rust = _ramp(nt, _noise(nt, co, 40.0, detail=8.0), [(0.6, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
        color = _mix(nt, rust, base, (0.14, 0.06, 0.02, 1))
        rivet = _ramp(nt, voronoi(45.0).outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.08, (0, 0, 0, 1))])
        color = _mix(nt, rivet, color, (0.2, 0.18, 0.15, 1))
    elif kind == "안감":
        color = _mix(nt, _noise(nt, co, 20.0, detail=4.0), (0.06, 0.009, 0.01, 1), (0.12, 0.02, 0.018, 1))
    elif kind == "금화":
        # 겹친 동전 — 보로노이 칸 하나가 한 닢: 가운데 문양 원 · 동전 면 · 밝은 테 · 닢 사이 짙은 틈. 칸마다 밝기 다름
        vor = voronoi(16.0)
        dist = vor.outputs["Distance"]
        gold = _mix(nt, vor.outputs["Color"], (0.38, 0.22, 0.035, 1), (0.62, 0.4, 0.08, 1))
        # 🔴 1차(동전 면 0.3·틈 0.36부터)는 보로노이 F1 거리가 칸 가장자리에서 0.8까지 가서 틈이 면적 대부분을 먹어
        # 금화더미가 갈색 흙에 금 점 박힌 것처럼 읽혔다 — 면을 0.5까지, 테 0.5~0.62, 짙은 틈은 칸 가장자리(0.66~)만
        emboss = _ramp(nt, dist, [(0.16, (0.5, 0.5, 0.5, 1)), (0.24, (0, 0, 0, 1))])
        gold = _mix(nt, emboss, gold, (0.78, 0.56, 0.16, 1))
        face = _ramp(nt, dist, [(0.62, (1, 1, 1, 1)), (0.72, (0, 0, 0, 1))])
        color = _mix(nt, face, (0.16, 0.09, 0.015, 1), gold)
        rim = _ramp(nt, dist, [(0.46, (0, 0, 0, 1)), (0.5, (1, 1, 1, 1)), (0.58, (1, 1, 1, 1)), (0.62, (0, 0, 0, 1))])
        color = _mix(nt, rim, color, (0.72, 0.5, 0.14, 1))
    elif kind == "보석":
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "띠"
        hue = _ramp(nt, attr.outputs["Fac"], [(0.0, (0.4, 0.01, 0.03, 1)), (0.5, (0.02, 0.3, 0.08, 1)), (1.0, (0.03, 0.08, 0.42, 1))])
        sparkle = _ramp(nt, _noise(nt, co, 60.0), [(0.62, (0, 0, 0, 1)), (0.75, (0.6, 0.6, 0.6, 1))])
        color = _mix(nt, sparkle, hue, (0.9, 0.9, 0.95, 1))
    elif kind == "흙":
        base = _mix(nt, _noise(nt, co, 12.0, detail=6.0), (0.085, 0.032, 0.016, 1), (0.16, 0.068, 0.034, 1))
        clods = _ramp(nt, voronoi(40.0, "DISTANCE_TO_EDGE").outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.05, (0, 0, 0, 1))])
        color = _mix(nt, clods, base, (0.04, 0.015, 0.008, 1))
    elif kind == "돌":
        base = _mix(nt, _noise(nt, co, 20.0), (0.5, 0.48, 0.44, 1), (0.66, 0.64, 0.59, 1))
        cracks = _ramp(nt, voronoi(30.0, "DISTANCE_TO_EDGE").outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.04, (0, 0, 0, 1))])
        color = _mix(nt, cracks, base, (0.22, 0.21, 0.19, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.7
    _link(nt, color, bsdf.inputs["Base Color"])


def tex_size(name):
    return 1024 if name.endswith(("_나무", "_금화", "_흙")) else 512


# ──────────────────────────────────────────────────────────── 동작

def _set_action(arm, action):
    ad = arm.animation_data or arm.animation_data_create()
    ad.action = action
    if hasattr(ad, "action_slot") and len(action.slots):
        ad.action_slot = action.slots[0]


def animate_chest(arm):
    scene = bpy.context.scene
    scene.render.fps = FPS
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    lid = arm.pose.bones["Lid"]
    idle = bpy.data.actions.new("Idle")
    _set_action(arm, idle)
    for f in range(0, IDLE_FRAMES + 1, 2):                    # 2초 — 뚜껑이 2.5° 들썩였다 닫힘(첫 = 끝 = 0)
        lid.rotation_euler = (math.radians(-2.5) * math.sin(math.pi * f / IDLE_FRAMES) ** 2, 0.0, 0.0)
        lid.keyframe_insert("rotation_euler", frame=f)
    opening = bpy.data.actions.new("Open")
    _set_action(arm, opening)
    for f in range(0, OPEN_FRAMES + 1):                       # 1.5초 — 1−(1−t)³로 끝에서 느려지고 끝 자세 유지
        t = f / OPEN_FRAMES
        lid.rotation_euler = (math.radians(OPEN_DEG) * (1 - (1 - t) ** 3), 0.0, 0.0)
        lid.keyframe_insert("rotation_euler", frame=f)
    for action in (idle, opening):
        action["gen_treasure"] = True          # 🔴 사장님 창엔 옛 보물상자의 Idle·Open이 이미 있다 — 이름으로 찾지 말고 이 표시·반환값으로
        for fc in gen_ships._fcurves(action):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
    return idle, opening


def check_chest_motion(arm, obj, idle, opening):
    scene = bpy.context.scene

    def pose_at(frame):
        scene.frame_set(frame)
        return [tuple(round(x, 6) for row in pb.matrix for x in row) for pb in arm.pose.bones]

    _set_action(arm, idle)
    loop = pose_at(0) == pose_at(IDLE_FRAMES)
    _set_action(arm, opening)
    scene.frame_set(OPEN_FRAMES)
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
    zs = [v.co.z * UNITS for v in evaluated.vertices]
    _set_action(arm, idle)
    scene.frame_set(0)
    assert loop, "Idle 첫 프레임 ≠ 끝 프레임"
    assert min(zs) >= -1e-3, f"Open 끝 자세 최저점 {min(zs):.2f} < 0"
    assert max(zs) > H + 4.5, f"Open 끝에서 뚜껑이 안 열렸다(최고점 {max(zs):.2f}) — 회전 부호 확인"
    return dict(idle_loop=loop, open_low=min(zs), open_top=max(zs))


# ──────────────────────────────────────────────────────────── 조립 · 내보내기

def assemble(kind, collection):
    name = NAMES[kind]
    bones, extra = None, {}
    if kind in ("chest", "open"):
        mesh, bones, extra = build_chest(name, opened=kind == "open", filled=kind == "open")
    elif kind == "coins":
        mesh = build_coins(name)
    else:
        mesh = build_mark(name)
    expect = {"chest": ("나무", "쇠", "안감"), "open": ("나무", "쇠", "안감", "금화", "보석"),
              "coins": ("금화",), "mark": ("흙", "돌")}[kind]
    assert sorted(mesh.mats) == sorted(f"{name}_{k}" for k in expect), f"{name} 재질 이름 {mesh.mats} ≠ {expect}"
    bm = mesh.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs, ys, zs = ([v.co[i] for v in bm.verts] for i in range(3))
    xs, ys, zs = list(xs), list(ys), list(zs)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    reach = max(math.hypot(x, y) for x, y in zip(xs, ys))
    assert tris <= TRI_LIMIT, f"{name} 삼각형 {tris} > {TRI_LIMIT}"
    assert abs(min(zs)) < 1e-3, f"{name} 최저점 {min(zs):.3f} ≠ 0"
    if kind == "chest":
        assert max(abs(x) for x in xs) <= HX + 0.2 and max(abs(y) for y in ys) <= HY + 0.2 and max(zs) <= H + RISE + 0.1, \
            f"{name} 크기 x {min(xs):.2f}~{max(xs):.2f} y {min(ys):.2f}~{max(ys):.2f} z ~{max(zs):.2f}"
    elif kind == "coins":
        gold_area = sum(f.calc_area() for f in bm.faces if f.material_index == mesh.m(f"{name}_금화")) / sum(f.calc_area() for f in bm.faces)
        assert reach <= 3.5 + 1e-3 and abs(max(zs) - 2.5) < 0.05 and gold_area > 0.999, \
            f"{name} 반지름 {reach:.2f} 높이 {max(zs):.2f} 금 면적 {gold_area:.3f}"
    elif kind == "mark":
        assert reach <= 3.0 + 1e-3 and max(zs) <= 1.6, f"{name} 반지름 {reach:.2f} 높이 {max(zs):.2f}"
        centre = [v.co.z for v in bm.verts if math.hypot(v.co.x, v.co.y) < 1e-6]
        assert centre and abs(min(centre) - 1.2) < 1e-3, f"{name} 가운데 흙더미 {centre}"
    info = dict(tris=tris, x=(min(xs), max(xs)), y=(min(ys), max(ys)), z=(min(zs), max(zs)), reach=reach, **extra)
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    if kind == "chest":
        for g in mesh.groups:
            obj.vertex_groups.new(name=g)
    for mat_name in mesh.mats:
        mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        _shade(mat)
        data.materials.append(mat)
    return obj, info, bones


def build_in_window(kind, collection, folder):
    obj, info, bones = assemble(kind, collection)
    gen_ships.unwrap(obj)
    gen_ships.tex_size = tex_size
    gen_ships.bake(obj, folder)
    arm = None
    if kind == "chest":
        arm = gen_ships.rig(obj, bones, collection)
        idle, opening = animate_chest(arm)
        info.update(check_chest_motion(arm, obj, idle, opening))
        info["_actions"] = (idle, opening)
    return arm, obj, info


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(NAMES)
    for kind in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_보물")
        bpy.context.scene.collection.children.link(col)
        name = NAMES[kind]
        arm, obj, info = build_in_window(kind, col, os.path.join(OUT, "Textures"))
        for o in _live_objects():
            o.select_set(o in (arm, obj))
        bpy.context.view_layer.objects.active = arm or obj
        path = os.path.join(OUT, name + ".fbx")
        if arm:
            actions = sorted(a.name for a in bpy.data.actions)
            assert actions == ["Idle", "Open"], f"액션이 Idle·Open 둘이 아니다: {actions}"
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, global_scale=UNITS,
                                     path_mode="RELATIVE", add_leaf_bones=False, mesh_smooth_type="FACE", armature_nodetype="NULL",
                                     bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                                     bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
        else:
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, global_scale=UNITS,
                                     path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        fmt = lambda pair: f"{pair[0]:.2f}~{pair[1]:.2f}"  # noqa: E731
        extra = {k: (round(v, 2) if isinstance(v, float) else v) for k, v in info.items()
                 if k not in ("x", "y", "z", "tris", "reach") and not k.startswith("_")}
        print(f"만듦  {name}  삼각형 {info['tris']}  x {fmt(info['x'])}  y {fmt(info['y'])}  z {fmt(info['z'])}  반지름 {info['reach']:.2f}  "
              f"{extra}  재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
