"""창고 헛간 — 플레이어별 창고 섬(44×44) 한쪽 가장자리의 항구 창고(PM 배정 2026-09-13, 구조물 6번). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX·텍스처는 Assets/Art/Structures/로.

화면 없이(정본):  blender --background --factory-startup --python gen_warehouse.py [-- 창고_회색함석 ...]
사장님 창(보여 주기만): ns["build_in_window"](이름, 컬렉션, 창 텍스처 폴더)

규격(PM): 바닥 16×10 안(x ±8, y ±5), 높이 12 안, 원점 바닥 가운데, 정면 −Y. 박공 지붕 + 큰 미닫이 나무문(반쯤 열림)
+ 문 앞 나무 상자·자루·그물 더미. 네 명 창고가 나란히 붙으니 지붕 색만 넷(상점과 겹치지 않게):
연회색 함석 · 녹슨 붉은 함석 · 바랜 초록 널 · 모래색 기와. 뒷면 검사 둘 0건, 헤드리스 정본.
- 옆 박공(용마루가 가로 X) — 긴 지붕 비탈이 게임 카메라에 넓게 보여 지붕 색이 창고를 가른다. 문은 처마(6.8) 아래라 가려지지 않는다.
- 변형 넷은 메시가 같고 지붕 재질만 다르다(벽·문·소품 텍스처는 같은 내용으로 다시 쓴다).
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

import gen_portals  # noqa: E402  (굽기 함수 재사용)
from shops_common import Builder, _link, _live_objects, _math, _mix, _node, _noise, _ramp, unwrap  # noqa: E402

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")

LIMIT_X, LIMIT_Y, LIMIT_Z = 8.0, 5.0, 12.0
WX = 7.4                      # 벽 반폭(가로)
Y_FRONT, Y_BACK = -3.2, 4.2   # 벽 앞·뒤(정면 소품 자리를 앞에 남긴다)
PLINTH = 0.6
EAVE = 6.8
RIDGE = 11.0
ROOF_T, STEP, COURSES = 0.2, 0.1, 5
Y_RIDGE = (Y_FRONT + Y_BACK) / 2
HALF_D = (Y_BACK - Y_FRONT) / 2
SLOPE = (RIDGE - EAVE) / HALF_D

VARIANTS = {
    "창고_회색함석": "함석회색",
    "창고_붉은함석": "함석붉은",
    "창고_초록널": "널초록",
    "창고_모래기와": "기와모래",
}
P = "창고_"
WALL, TIMBER, STONE, DARK, IRON, CRATE, SACK, NET, FLOAT = (P + k for k in ("널벽", "목재", "석재", "어둠", "쇠", "상자", "자루", "그물", "부표"))
INNER, INNER_FLOOR, INNER_CRATE = P + "안쪽", P + "안쪽바닥", P + "안쪽상자"
ROOM_DEPTH = 1.5              # 열린 문간 안쪽 방 깊이(PM 2차: 1차는 평평한 짙은 판이라 칠해 놓은 사각형으로 보였다)
OPEN_X0, OPEN_X1, OPEN_Z1 = -3.6, 2.4, PLINTH + 5.0


def _quad(b, pts, mat):
    """점 네 개(바깥에서 봐서 반시계)로 면 하나."""
    f = b.bm.faces.new([b.bm.verts.new(p) for p in pts])
    f.material_index = b.m(mat)
    return f


def _room(b):
    """문간 안쪽 얕은 방 — 안을 보는 면(바닥·옆벽·뒷벽·천장) + 쌓인 상자 실루엣 둘."""
    x0, x1, z0, z1 = OPEN_X0, OPEN_X1, PLINTH, OPEN_Z1
    y0, y1 = Y_FRONT, Y_FRONT + ROOM_DEPTH
    fz = z0 + 0.02                                                                       # 기단 윗면과 겹치지 않게
    _quad(b, [(x0, y0, fz), (x1, y0, fz), (x1, y1, fz), (x0, y1, fz)], INNER_FLOOR)      # 바닥(위를 봄)
    _quad(b, [(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], INNER)            # 뒷벽(−y를 봄)
    _quad(b, [(x0, y1, z0), (x0, y1, z1), (x0, y0, z1), (x0, y0, z0)], INNER)            # 왼벽(+x를 봄)
    _quad(b, [(x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0)], INNER)            # 오른벽(−x를 봄)
    _quad(b, [(x0, y0, z1), (x0, y1, z1), (x1, y1, z1), (x1, y0, z1)], INNER)            # 천장(아래를 봄)
    for cx0, cx1, cz0, cz1 in ((-3.3, -1.9, z0, z0 + 1.3), (-3.0, -2.0, z0 + 1.3, z0 + 2.2), (-1.6, -0.5, z0, z0 + 1.0)):
        b.box(cx0, cx1, y1 - 0.9, y1 - 0.05, cz0, cz1, INNER_CRATE, skip=("bottom", "+y"))


def build(variant):
    rnd = random.Random(11)
    b = Builder()
    roof = P + "지붕_" + VARIANTS[variant]
    # 기단·벽
    b.box(-WX - 0.2, WX + 0.2, Y_FRONT - 0.2, Y_BACK + 0.2, 0.0, PLINTH, STONE, skip=("bottom",))
    b.box(-WX, WX, Y_FRONT, Y_BACK, PLINTH, EAVE, WALL, skip=("bottom", "top", "-y"))
    # 정면 벽은 문간 자리를 뚫은 세 장(왼·오른·위) + 안쪽 방
    y = Y_FRONT
    _quad(b, [(-WX, y, PLINTH), (OPEN_X0, y, PLINTH), (OPEN_X0, y, EAVE), (-WX, y, EAVE)], WALL)
    _quad(b, [(OPEN_X1, y, PLINTH), (WX, y, PLINTH), (WX, y, EAVE), (OPEN_X1, y, EAVE)], WALL)
    _quad(b, [(OPEN_X0, y, OPEN_Z1), (OPEN_X1, y, OPEN_Z1), (OPEN_X1, y, EAVE), (OPEN_X0, y, EAVE)], WALL)
    _room(b)
    for sx in (-1, 1):
        for y in (Y_FRONT, Y_BACK):
            b.box_c(sx * (WX - 0.1), y + (0.1 if y == Y_FRONT else -0.1), 0.5, 0.5, PLINTH, EAVE - PLINTH, TIMBER, skip=("bottom",))
    for y0, y1 in ((Y_FRONT - 0.15, Y_FRONT + 0.25), (Y_BACK - 0.25, Y_BACK + 0.15)):
        b.box(-WX - 0.15, WX + 0.15, y0, y1, EAVE - 0.35, EAVE + 0.15, TIMBER)
    b.box(-WX - 0.15, -WX + 0.25, Y_FRONT, Y_BACK, EAVE - 0.35, EAVE + 0.15, TIMBER)
    b.box(WX - 0.25, WX + 0.15, Y_FRONT, Y_BACK, EAVE - 0.35, EAVE + 0.15, TIMBER)
    # 박공 다락 — 양옆(x) 삼각벽
    b.extrude([(-WX, Y_FRONT, EAVE + 0.15), (-WX, Y_BACK, EAVE + 0.15), (-WX, Y_RIDGE, RIDGE - 0.1)], (2 * WX, 0, 0), WALL)
    # 지붕 켜 — 앞·뒤 비탈, 처마는 한계에서 거꾸로(켜 턱이 법선 방향으로 튀어나오므로)
    x0, x1 = -LIMIT_X + 0.05, LIMIT_X - 0.05
    for sy in (-1, 1):
        d = Vector((-sy * 1.0, SLOPE)).normalized()                    # (y, z) 처마 → 용마루
        n = Vector((-d.y, d.x)) if sy < 0 else Vector((d.y, -d.x))
        over = 0.35 if sy < 0 else 0.25
        ey = (Y_FRONT - over) if sy < 0 else (Y_BACK + over)
        limit = LIMIT_Y - 0.02 - abs(n.x) * (ROOF_T + STEP)
        ey = max(ey, -limit) if sy < 0 else min(ey, limit)
        eave = Vector((ey, EAVE - SLOPE * abs(ey - (Y_FRONT if sy < 0 else Y_BACK))))
        ridge = Vector((Y_RIDGE, RIDGE))
        length = (ridge - eave).length
        for i in range(COURSES):
            p0 = eave + d * (length * i / COURSES)
            p1 = eave + d * (length * (i + 1) / COURSES + 0.1)
            section = [p0 - n * 0.02, p1, p1 + n * ROOF_T, p0 + n * (ROOF_T + STEP)]
            b.extrude([(x0, p.x, p.y) for p in section], (x1 - x0, 0, 0), roof)
    b.box(x0 - 0.02, x1 + 0.02, Y_RIDGE - 0.35, Y_RIDGE + 0.35, RIDGE + 0.15, RIDGE + 0.5, roof)
    y = Y_FRONT
    # 큰 미닫이 문 — 문간(어둠) + 반쯤 밀린 널문 + 쇠 레일·바퀴
    ox0, ox1, oz1 = OPEN_X0, OPEN_X1, OPEN_Z1
    b.box(ox0 - 0.35, ox0, y - 0.3, y, PLINTH, oz1 + 0.3, TIMBER, skip=("+y",))
    b.box(ox1, ox1 + 0.35, y - 0.3, y, PLINTH, oz1 + 0.3, TIMBER, skip=("+y",))
    b.box(ox0 - 0.9, ox1 + 3.8, y - 0.26, y, oz1 + 0.3, oz1 + 0.55, IRON, skip=("+y",))          # 레일
    dx0, dx1 = -0.4, 5.4                                                                        # 오른쪽으로 반쯤 밀린 문짝
    b.box(dx0, dx1, y - 0.5, y - 0.3, PLINTH + 0.1, oz1 + 0.25, TIMBER)
    for zz in (PLINTH + 1.2, oz1 - 1.0):                                                        # 가로 띠
        b.box(dx0 + 0.1, dx1 - 0.1, y - 0.58, y - 0.5, zz, zz + 0.25, TIMBER, skip=("+y",))
    b.extrude([(dx0 + 0.2, y - 0.5, PLINTH + 1.45), (dx1 - 0.2, y - 0.5, oz1 - 1.0), (dx1 - 0.2, y - 0.5, oz1 - 0.75),
               (dx0 + 0.2, y - 0.5, PLINTH + 1.7)], (0, -0.08, 0), TIMBER)                     # 대각 버팀
    for wx in (dx0 + 0.6, dx1 - 0.6):
        b.cylinder((wx, y - 0.4, oz1 + 0.25), (0, -1, 0), 0.16, 0.12, IRON, sides=8)
    # 높은 창 둘
    for cx in (-5.8, 6.1):
        b.box(cx - 0.6, cx + 0.6, y - 0.05, y, 4.6, 5.6, DARK, skip=("+y",))
        b.box(cx - 0.75, cx + 0.75, y - 0.2, y, 4.45, 4.6, TIMBER, skip=("+y",))
        b.box(cx - 0.75, cx + 0.75, y - 0.2, y, 5.6, 5.75, TIMBER, skip=("+y",))
    # 문 앞 소품 — 나무 상자 더미, 자루, 그물 더미 + 부표
    # 🔴 1차는 상자·자루·그물이 앞으로 나가 y −5.28(바닥 한계 −5)이었다 — 벽 쪽으로 당겨 둔다
    fy = y - 0.2
    for cx, cy, z0, s, turn in ((-6.3, fy - 0.6, 0.0, 1.3, 0.0), (-4.9, fy - 0.7, 0.0, 1.1, 9.0), (-5.7, fy - 0.65, 1.3, 1.0, -14.0)):
        rot = Matrix.Translation((cx, cy, z0 + s / 2)) @ Matrix.Rotation(math.radians(turn), 4, "Z")
        h = s / 2
        b.extrude([(-h, -h, -h), (h, -h, -h), (h, h, -h), (-h, h, -h)], (0, 0, s), CRATE, matrix=rot, caps=(z0 > 0, True))
        for zz in (-h + 0.08, h - 0.22):                                                        # 상자 띠
            b.extrude([(-h - 0.02, -h - 0.02, zz), (h + 0.02, -h - 0.02, zz), (h + 0.02, h + 0.02, zz), (-h - 0.02, h + 0.02, zz)],
                      (0, 0, 0.14), TIMBER, matrix=rot, caps=(False, False))
    for k, (cx, cy) in enumerate(((-3.2, fy - 0.5), (-2.4, fy - 0.8), (-2.9, fy - 1.0))):
        made = bmesh.ops.create_icosphere(b.bm, subdivisions=1, radius=0.5)["verts"]
        for v in made:
            v.co = Vector((v.co.x * 0.9, v.co.y * 0.75, max(v.co.z, -0.3) * 1.1 + 0.33)) + Vector((cx, cy, 0.0))
            v.co.z = max(v.co.z, 0.0)
        for f in {f for v in made for f in v.link_faces}:
            f.material_index = b.m(SACK)
            f.smooth = True
    net = bmesh.ops.create_icosphere(b.bm, subdivisions=2, radius=1.0)["verts"]
    for v in net:
        p = v.co.copy()
        bump = 1.0 + 0.18 * math.sin(p.x * 5.0 + p.y * 3.0) * math.cos(p.z * 4.0)
        v.co = Vector((p.x * 1.6 * bump + 5.4, p.y * 0.6 * bump + fy - 0.8, max(p.z, -0.2) * 0.65 + 0.13))
        v.co.z = max(v.co.z, 0.0)
    for f in {f for v in net for f in v.link_faces}:
        f.material_index = b.m(NET)
        f.smooth = True
    for cx, cy, cz in ((4.5, fy - 0.7, 0.62), (6.2, fy - 1.0, 0.5), (5.6, fy - 0.3, 0.72)):
        made = bmesh.ops.create_icosphere(b.bm, subdivisions=1, radius=0.22, matrix=Matrix.Translation((cx, cy, cz)))["verts"]
        for f in {f for v in made for f in v.link_faces}:
            f.material_index = b.m(FLOAT)
            f.smooth = True
    return b


# ──────────────────────────────────────────────────────────── 셰이더

def _shade(mat):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    kind = mat.name.split("_", 1)[1]

    def bands(scale, direction="X", distortion=0.3):
        w = _node(nt, "ShaderNodeTexWave", Scale=scale, Distortion=distortion)
        w.wave_type, w.bands_direction = "BANDS", direction
        _link(nt, co, w.inputs["Vector"])
        return w.outputs["Fac"]

    if kind == "널벽":
        along = nt.nodes.new("ShaderNodeCombineXYZ")
        _link(nt, _math(nt, "ADD", sep.outputs[0], sep.outputs[1]), along.inputs[0])
        _link(nt, sep.outputs[2], along.inputs[1])
        w = _node(nt, "ShaderNodeTexWave", Scale=11.0, Distortion=0.6)
        w.wave_type, w.bands_direction = "BANDS", "X"
        _link(nt, along.outputs[0], w.inputs["Vector"])
        gap = _ramp(nt, w.outputs["Fac"], [(0.0, (0, 0, 0, 1)), (0.07, (0, 0, 0, 1)), (0.15, (1, 1, 1, 1))])
        mp = _node(nt, "ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1.0, 1.0, 0.12)
        _link(nt, co, mp.inputs["Vector"])
        board = _mix(nt, _noise(nt, mp.outputs["Vector"], 50.0, detail=6.0), (0.1, 0.075, 0.05, 1), (0.2, 0.155, 0.105, 1))
        color = _mix(nt, gap, (0.02, 0.016, 0.012, 1), board)
    elif kind == "목재":
        mp = _node(nt, "ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1.0, 1.0, 0.15)
        _link(nt, co, mp.inputs["Vector"])
        color = _mix(nt, _noise(nt, mp.outputs["Vector"], 55.0, detail=6.0), (0.04, 0.026, 0.016, 1), (0.1, 0.066, 0.04, 1))
    elif kind == "석재":
        br = _node(nt, "ShaderNodeTexBrick", Scale=5.0, **{"Mortar Size": 0.015, "Brick Width": 1.0, "Row Height": 0.5})
        br.inputs["Color1"].default_value, br.inputs["Color2"].default_value = (0.15, 0.145, 0.135, 1), (0.23, 0.22, 0.2, 1)
        br.inputs["Mortar"].default_value = (0.06, 0.058, 0.052, 1)
        wall = nt.nodes.new("ShaderNodeCombineXYZ")
        _link(nt, _math(nt, "ADD", sep.outputs[0], sep.outputs[1]), wall.inputs[0])
        _link(nt, sep.outputs[2], wall.inputs[1])
        _link(nt, wall.outputs[0], br.inputs["Vector"])
        color = br.outputs["Color"]
    elif kind == "어둠":
        color = _mix(nt, _noise(nt, co, 6.0), (0.015, 0.013, 0.012, 1), (0.035, 0.03, 0.026, 1))
    elif kind in ("안쪽", "안쪽바닥", "안쪽상자"):
        # 문간 안 — 짙은 널, 위로 갈수록 그림자(문틀 위가 가장 어둡다)
        up = nt.nodes.new("ShaderNodeMapRange")
        _link(nt, sep.outputs[2], up.inputs["Value"])
        up.inputs["From Min"].default_value, up.inputs["From Max"].default_value = PLINTH / UNITS, OPEN_Z1 / UNITS
        if kind == "안쪽상자":
            gap = _ramp(nt, bands(28.0, "Z"), [(0.0, (0, 0, 0, 1)), (0.08, (0, 0, 0, 1)), (0.16, (1, 1, 1, 1))])
            wood = _mix(nt, gap, (0.02, 0.014, 0.008, 1), _mix(nt, _noise(nt, co, 40.0), (0.08, 0.06, 0.035, 1), (0.13, 0.1, 0.06, 1)))
        elif kind == "안쪽바닥":
            gap = _ramp(nt, bands(9.0, "X"), [(0.0, (0, 0, 0, 1)), (0.07, (0, 0, 0, 1)), (0.14, (1, 1, 1, 1))])
            wood = _mix(nt, gap, (0.012, 0.009, 0.006, 1), _mix(nt, _noise(nt, co, 35.0), (0.06, 0.045, 0.028, 1), (0.1, 0.075, 0.045, 1)))
        else:
            gap = _ramp(nt, bands(11.0, "X"), [(0.0, (0, 0, 0, 1)), (0.07, (0, 0, 0, 1)), (0.14, (1, 1, 1, 1))])
            wood = _mix(nt, gap, (0.01, 0.008, 0.006, 1), _mix(nt, _noise(nt, co, 35.0), (0.045, 0.034, 0.022, 1), (0.075, 0.056, 0.036, 1)))
        # 🔴 1차(선형)는 벽 위쪽까지 널이 밝게 읽혔다 — 제곱근으로 일찍 어두워지게(허리 높이에서 이미 70%)
        color = _mix(nt, _math(nt, "POWER", up.outputs["Result"], 0.5), wood, (0.004, 0.0035, 0.003, 1))
    elif kind == "쇠":
        base = _mix(nt, _noise(nt, co, 30.0), (0.02, 0.02, 0.022, 1), (0.06, 0.06, 0.065, 1))
        rust = _ramp(nt, _noise(nt, co, 20.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.6, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
        color = _mix(nt, rust, base, (0.12, 0.045, 0.015, 1))
    elif kind == "상자":
        gap = _ramp(nt, bands(28.0, "Z"), [(0.0, (0, 0, 0, 1)), (0.08, (0, 0, 0, 1)), (0.16, (1, 1, 1, 1))])
        board = _mix(nt, _noise(nt, co, 40.0), (0.22, 0.16, 0.09, 1), (0.36, 0.27, 0.16, 1))
        color = _mix(nt, gap, (0.06, 0.04, 0.025, 1), board)
    elif kind == "자루":
        weave = _math(nt, "MULTIPLY", bands(140.0, "X"), bands(140.0, "Z"))
        color = _mix(nt, _math(nt, "ADD", _math(nt, "MULTIPLY", weave, 0.5), _math(nt, "MULTIPLY", _noise(nt, co, 12.0), 0.5)),
                     (0.22, 0.17, 0.1, 1), (0.4, 0.32, 0.2, 1))
    elif kind == "그물":
        mesh_x = _ramp(nt, bands(55.0, "DIAGONAL", 2.0), [(0.0, (1, 1, 1, 1)), (0.12, (0, 0, 0, 1))])
        base = _mix(nt, _noise(nt, co, 10.0), (0.05, 0.045, 0.035, 1), (0.1, 0.09, 0.07, 1))
        color = _mix(nt, mesh_x, base, (0.26, 0.24, 0.18, 1))
    elif kind == "부표":
        stripe = _ramp(nt, bands(20.0, "Z"), [(0.0, (0, 0, 0, 1)), (0.45, (0, 0, 0, 1)), (0.55, (1, 1, 1, 1))])
        color = _mix(nt, stripe, (0.6, 0.12, 0.04, 1), (0.6, 0.56, 0.48, 1))
    elif kind.startswith("지붕_"):
        tone = kind.split("_", 1)[1]
        grime = _ramp(nt, _noise(nt, co, 6.0, detail=6.0), [(0.0, (0, 0, 0, 1)), (0.58, (0, 0, 0, 1)), (0.72, (0.7, 0.7, 0.7, 1))])
        if tone.startswith("함석"):
            # 골함석(PM 2차) — 명도 중간, 골은 용마루→처마 방향 줄로 간격 약 0.4(가로 x로 되풀이; 1차 Scale 60은 간격이 너무
            # 좁아 평판·슬레이트로 보였다), 녹은 흩뿌린 점이 아니라 켜 겹침 턱·못 자리에서 처마 쪽으로 흘러내린 가는 줄 +
            # 처마 끝단에 모인 녹.
            # Wave BANDS 한 주기 = 2π/20 ÷ Scale (m) → 게임 단위 0.4면 Scale ≈ 9
            corr = _ramp(nt, bands(9.0, "X", 0.0), [(0.0, (0, 0, 0, 1)), (0.35, (0.35, 0.35, 0.35, 1)), (0.5, (1, 1, 1, 1)),
                                                     (0.65, (0.35, 0.35, 0.35, 1)), (1.0, (0, 0, 0, 1))])
            dark, light, rust = (((0.1, 0.105, 0.11, 1), (0.2, 0.205, 0.21, 1), (0.13, 0.055, 0.022, 1)) if tone == "함석회색"
                                 else ((0.09, 0.035, 0.018, 1), (0.16, 0.065, 0.032, 1), (0.05, 0.022, 0.012, 1)))
            color = _mix(nt, corr, dark, light)
            # 흘러내린 녹물 줄 — 비탈 방향(y·z)으로 길게 늘인 노이즈를 가늘게 자른다
            mp = _node(nt, "ShaderNodeMapping")
            mp.inputs["Scale"].default_value = (1.0, 0.07, 0.07)
            _link(nt, co, mp.inputs["Vector"])
            streak = _ramp(nt, _noise(nt, mp.outputs["Vector"], 30.0, detail=3.0),
                           [(0.0, (0, 0, 0, 1)), (0.6, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
            # 켜 안에서 윗 켜 겹침 턱 바로 아래가 가장 짙고 제 처마 쪽으로 옅어진다(켜 높이 약 0.9, 처마 z 약 6.45)
            t = _math(nt, "FRACT", _math(nt, "DIVIDE", _math(nt, "SUBTRACT", sep.outputs[2], 6.45 / UNITS), 0.905 / UNITS), 0.0)
            overlap = _ramp(nt, t, [(0.0, (0.3, 0.3, 0.3, 1)), (0.55, (0.5, 0.5, 0.5, 1)), (1.0, (1, 1, 1, 1))])
            eave_near = nt.nodes.new("ShaderNodeMapRange")
            _link(nt, sep.outputs[2], eave_near.inputs["Value"])
            # 🔴 1차(7.1→6.45, 둥근 노이즈)는 처마가 분무기로 뿌린 띠처럼 매끈했다 — 좁히고 비탈 방향 줄로 끊는다
            eave_near.inputs["From Min"].default_value, eave_near.inputs["From Max"].default_value = 6.85 / UNITS, 6.4 / UNITS
            drip = _ramp(nt, _noise(nt, mp.outputs["Vector"], 45.0, detail=3.0), [(0.0, (0.15, 0.15, 0.15, 1)), (0.45, (0.3, 0.3, 0.3, 1)),
                                                                                (0.6, (1, 1, 1, 1))])
            lip = _math(nt, "MULTIPLY", eave_near.outputs["Result"], drip)
            amount = _math(nt, "MAXIMUM", _math(nt, "MULTIPLY", streak, overlap), lip)
            color = _mix(nt, amount, color, rust)
        elif tone == "널초록":
            gap = _ramp(nt, bands(9.0, "X", 1.0), [(0.0, (0, 0, 0, 1)), (0.08, (0, 0, 0, 1)), (0.16, (1, 1, 1, 1))])
            board = _mix(nt, _noise(nt, co, 25.0, detail=6.0), (0.07, 0.12, 0.07, 1), (0.15, 0.21, 0.13, 1))
            peel = _ramp(nt, _noise(nt, co, 18.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.62, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
            color = _mix(nt, gap, (0.02, 0.025, 0.015, 1), _mix(nt, peel, board, (0.14, 0.11, 0.08, 1)))
        else:   # 기와모래
            # 🔴 1차(선형 0.34~0.48, 세로 골만)는 게임 시점에서 거의 흰 판 + 함석 골처럼 읽혔다 — 명도를 내리고
            # 기와 한 장 폭(약 0.7)의 세로 골 + 기와 줄(켜 하나에 두 줄)마다 아랫단 그림자로 기와다움을 준다
            cell = _node(nt, "ShaderNodeTexVoronoi", Scale=40.0)
            _link(nt, co, cell.inputs["Vector"])
            tile = _mix(nt, cell.outputs["Distance"], (0.19, 0.145, 0.085, 1), (0.29, 0.23, 0.14, 1))
            groove = _ramp(nt, bands(5.0, "X", 0.0), [(0.0, (0, 0, 0, 1)), (0.15, (0.35, 0.35, 0.35, 1)), (0.3, (1, 1, 1, 1))])
            row_t = _math(nt, "FRACT", _math(nt, "DIVIDE", _math(nt, "SUBTRACT", sep.outputs[2], 6.45 / UNITS), 0.4525 / UNITS), 0.0)
            row = _ramp(nt, row_t, [(0.0, (0, 0, 0, 1)), (0.12, (1, 1, 1, 1))])
            color = _mix(nt, _math(nt, "MULTIPLY", groove, row), (0.07, 0.055, 0.035, 1), tile)
        if not tone.startswith("함석"):
            color = _mix(nt, grime, color, (0.06, 0.055, 0.045, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.85
    _link(nt, color, bsdf.inputs["Base Color"])


def tex_size(name):
    return 1024 if any(name.endswith(k) for k in ("_널벽", "_목재")) or "_지붕_" in name else 512


# ──────────────────────────────────────────────────────────── 조립·내보내기

def assemble(variant, collection):
    gen_portals.MASKS.clear()
    b = build(variant)
    bm = b.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    assert min(xs) >= -LIMIT_X - 1e-3 and max(xs) <= LIMIT_X + 1e-3 and min(ys) >= -LIMIT_Y - 1e-3 and max(ys) <= LIMIT_Y + 1e-3, \
        f"{variant} 바닥 16×10을 넘었다: x {min(xs):.2f}~{max(xs):.2f}, y {min(ys):.2f}~{max(ys):.2f}"
    assert abs(min(zs)) < 1e-3 and max(zs) <= LIMIT_Z + 1e-3, f"{variant} 높이 {min(zs):.2f}~{max(zs):.2f}"
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    bounds = (min(xs), max(xs), min(ys), max(ys), max(zs), tri_count)
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    mesh = bpy.data.meshes.new(variant)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(variant, mesh)
    collection.objects.link(obj)
    for name in b.mats:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        _shade(mat)
        mesh.materials.append(mat)
    return obj, bounds


def bake(obj, folder):
    gen_portals.tex_size = tex_size
    gen_portals.bake(obj, folder)


def build_in_window(variant, collection, folder):
    obj, bounds = assemble(variant, collection)
    unwrap(obj)
    bake(obj, folder)
    return obj


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(VARIANTS)
    for variant in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        obj, bounds = assemble(variant, col)
        unwrap(obj)
        bake(obj, os.path.join(OUT, "Textures"))
        for o in _live_objects():
            o.select_set(o is obj)
        bpy.context.view_layer.objects.active = obj
        path = os.path.join(OUT, variant + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {variant}  삼각형 {bounds[5]}  x {bounds[0]:.2f}~{bounds[1]:.2f} y {bounds[2]:.2f}~{bounds[3]:.2f} "
              f"z ~{bounds[4]:.2f}  재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
