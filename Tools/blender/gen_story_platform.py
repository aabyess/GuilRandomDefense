"""스토리존 단상 — 닳은 포석 광장(PM 배정 2026-09-13, 구조물 5번). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX·텍스처는 Assets/Art/Structures/로.

화면 없이(정본):  blender --background --factory-startup --python gen_story_platform.py
사장님 창(보여 주기만): ns["build_in_window"](컬렉션, 창 텍스처 폴더)

쓰임: 스토리존 한가운데 — 스토리 적 건물(바닥 정사각 45×45 안)이 올라서는 무대 + 플레이어 유닛이 둘러싸고 공격하는 광장.
적 건물을 가리면 안 되니 낮게.
규격(PM 2차 정정 — 1차 「가운데 반지름 23 비움」은 틀렸다: 45×45 정사각의 모서리까지는 반지름 31.8이라, 02 큰소망유치원
44.4×44.0이 둘레돌·계단을 넘고 45° 계선주가 건물 속에 박혔다):
- 광장 윗면 원 반지름 33(지름 66), 윗면 1.0, 둘레돌 윗면 ≤ 1.2, 원점 바닥 가운데
- 비움은 정사각형: |x| ≤ 23, |y| ≤ 23 안에 광장 윗면보다 높은 것 없음(assert)
- 사방 계단 세 단, 반지름 33 → 35.5, 폭 10
- 계선주 8(계단 사이)·가스등 4(계단 옆, 높이 7, 등갓 유리 `스토리단상_등_발광`, 불 자리 없음)은 반지름 33.5~35.5 고리에만(assert)
- 전체 반지름 36 안, 삼각형 3,500 이하, 뒷면 검사 둘 0건, 헤드리스 정본
포석(PM 2차): 1차 보로노이는 크기·색이 고른 한 톤이라 게임 시점에서 「마른 진흙 갈라짐」, 가는 선 나침반은 스티커·맨홀로 보였다.
- 가운데를 중심으로 동심원 줄로 깐 사각 판석(줄 폭 약 1.7~2.4, 이음 엇갈림), 판마다 따뜻한·차가운 회색 + 명도 ±15%
- 사방 계단에서 가운데로 닳아 매끈하고 조금 어두운 동선 넷(이음새 한 곳은 −X 동선 밑에 숨김), 둘레 가까운 줄눈에만 이끼
- 나침반은 두 가지 돌을 박은 문양: 8방 별(짙은 현무암·모래색 번갈아) + 바깥 짙은 돌 고리, 지름 약 16
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형, 굽기 DIFFUSE(Metallic 0).
"""
import math
import os
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
NAME = "스토리단상"

R_PLAZA = 33.0          # 광장 윗면 반지름
TOP = 1.0               # 광장 윗면
KEEP = 23.0             # 비움 정사각 반폭
R_STEP_OUT = 35.5       # 계단 바깥
R_PROPS = (33.5, 35.5)  # 계선주·가스등 고리
R_LIMIT = 36.0
# 둘레돌 안쪽 반지름 — 비움 정사각(|x|,|y| ≤ 23)의 모서리가 반지름 32.53까지 닿으므로 그보다 바깥에서 시작한다
# (32.3으로 두었더니 45° 방향 둘레돌이 정사각 모서리를 0.2 침범해 assert가 잡았다)
CURB = (32.6, 33.0, 1.12)
STEP_W = 10.0
STEPS = 3
SEG = 72
P = NAME + "_"
PAVE, CURB_MAT, WALL, IRON, ROPE, GLASS = P + "포석", P + "둘레돌", P + "옹벽", P + "쇠", P + "밧줄", P + "등_발광"


def _half_angle(width, radius):
    return math.asin((width / 2) / radius)


def build():
    b = Builder()
    top = [b.bm.verts.new((math.cos(2 * math.pi * i / SEG) * R_PLAZA, math.sin(2 * math.pi * i / SEG) * R_PLAZA, TOP)) for i in range(SEG)]
    low = [b.bm.verts.new((v.co.x, v.co.y, 0.0)) for v in top]
    f = b.bm.faces.new(top)
    f.material_index = b.m(PAVE)
    for i in range(SEG):
        j = (i + 1) % SEG
        side = b.bm.faces.new((low[i], low[j], top[j], top[i]))
        side.material_index = b.m(WALL)
    # 둘레돌 — 부채꼴 한 줄, 사방 계단 자리는 비움
    half_gap = _half_angle(STEP_W, CURB[1]) + math.radians(0.8)
    stones = 56
    for k in range(stones):
        a0, a1 = 2 * math.pi * k / stones + 0.003, 2 * math.pi * (k + 1) / stones - 0.003
        mid = (a0 + a1) / 2
        near_step = min(abs(math.remainder(mid - q * math.pi / 2, 2 * math.pi)) for q in range(4))
        if near_step < half_gap:
            continue
        ring = [(CURB[0], a0), (CURB[1], a0), (CURB[1], a1), (CURB[0], a1)]
        b.extrude([(math.cos(a) * r, math.sin(a) * r, TOP) for r, a in ring], (0, 0, CURB[2] - TOP), CURB_MAT, caps=(False, True))
    # 사방 계단 세 단 — 반지름 33 → 35.5
    run = (R_STEP_OUT - R_PLAZA) / STEPS
    for q in range(4):
        rot = Matrix.Rotation(q * math.pi / 2, 4, "Z")
        for s in range(STEPS):
            r0, r1 = R_PLAZA - 0.6 + s * run, R_PLAZA + (s + 1) * run
            z1 = TOP * (STEPS - s) / (STEPS + 1)
            corners = [(r0, -STEP_W / 2, 0.0), (r1, -STEP_W / 2, 0.0), (r1, STEP_W / 2, 0.0), (r0, STEP_W / 2, 0.0)]
            b.extrude(corners, (0, 0, z1), CURB_MAT, matrix=rot, caps=(False, True))
    # 계선주 8 — 계단 사이 반지름 34.5
    for k in range(8):
        a = math.pi / 8 + k * math.pi / 4
        c = Vector((math.cos(a) * 34.5, math.sin(a) * 34.5, 0.0))
        b.cylinder(c, (0, 0, 1), 0.34, 1.0, IRON, sides=8, caps=(False, True))
        b.cylinder(c + Vector((0, 0, 1.0)), (0, 0, 1), 0.46, 0.18, IRON, sides=8)
        b.cylinder(c + Vector((0, 0, 1.18)), (0, 0, 1), 0.3, 0.14, IRON, sides=8)
        _torus(b, c + Vector((0, 0, 0.62)), 0.42, 0.09, ROPE)
    # 가스등 4 — 계단 오른쪽 옆 반지름 34.5
    for q in range(4):
        a = q * math.pi / 2 - (_half_angle(STEP_W, 34.5) + math.radians(2.6))
        c = Vector((math.cos(a) * 34.5, math.sin(a) * 34.5, 0.0))
        b.box_c(c.x, c.y, 0.9, 0.9, 0.0, 0.6, CURB_MAT, skip=("bottom",))
        b.cylinder(c + Vector((0, 0, 0.6)), (0, 0, 1), 0.17, 5.1, IRON, sides=8, caps=(False, False))
        b.cylinder(c + Vector((0, 0, 1.3)), (0, 0, 1), 0.26, 0.25, IRON, sides=8)
        b.cylinder(c + Vector((0, 0, 5.55)), (0, 0, 1), 0.3, 0.15, IRON, sides=8)
        b.box_c(c.x, c.y, 0.66, 0.66, 5.7, 0.8, GLASS)
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.box_c(c.x + sx * 0.35, c.y + sy * 0.35, 0.08, 0.08, 5.7, 0.8, IRON)
        b.box_c(c.x, c.y, 0.86, 0.86, 6.5, 0.1, IRON)
        b.spike([(c.x - 0.43, c.y - 0.43, 6.6), (c.x + 0.43, c.y - 0.43, 6.6), (c.x + 0.43, c.y + 0.43, 6.6), (c.x - 0.43, c.y + 0.43, 6.6)],
                (c.x, c.y, 7.0), IRON)
    return b


def _torus(b, center, major, minor, mat, u=8, v=4):
    """밧줄 감김 — 가로로 누운 도넛(바깥을 보는 감김)."""
    rings = []
    for i in range(u):
        a = 2 * math.pi * i / u
        axis = Vector((math.cos(a), math.sin(a), 0.0))
        rings.append([b.bm.verts.new(center + axis * (major + minor * math.cos(2 * math.pi * j / v))
                                     + Vector((0, 0, minor * math.sin(2 * math.pi * j / v)))) for j in range(v)])
    for i in range(u):
        for j in range(v):
            a, c = rings[i][j], rings[(i + 1) % u][j]
            d, e = rings[(i + 1) % u][(j + 1) % v], rings[i][(j + 1) % v]
            f = b.bm.faces.new((a, e, d, c))
            f.material_index = b.m(mat)
            f.normal_update()
            mid = sum((x.co for x in (a, c, d, e)), Vector()) / 4
            tube = center + Vector((mid.x - center.x, mid.y - center.y, 0)).normalized() * major
            if f.normal.dot(mid - tube) < 0:
                f.normal_flip()


# ──────────────────────────────────────────────────────────── 셰이더

def _brick(nt, vec, c1, c2, bias=0.0, mortar=0.07, smooth=0.1):
    br = _node(nt, "ShaderNodeTexBrick", Scale=1.0, Bias=bias,
               **{"Mortar Size": mortar, "Mortar Smooth": smooth, "Brick Width": 2.6, "Row Height": 2.0})
    br.offset, br.squash = 0.5, 1.0
    br.inputs["Color1"].default_value, br.inputs["Color2"].default_value = c1, c2
    br.inputs["Mortar"].default_value = (0.045, 0.043, 0.038, 1)
    _link(nt, vec, br.inputs["Vector"])
    return br


def _shade_pavement(nt, co, sep):
    """동심원 줄 판석 + 닳은 동선 넷 + 둘레 줄눈 이끼 + 박은 돌 나침반(8방 별 + 짙은 고리)."""
    # 극좌표(게임 단위) — 이음새 θ=±π는 −X 계단 동선 밑
    xu = _math(nt, "MULTIPLY", sep.outputs[0], UNITS)
    yu = _math(nt, "MULTIPLY", sep.outputs[1], UNITS)
    ru = _math(nt, "SQRT", _math(nt, "ADD", _math(nt, "MULTIPLY", xu, xu), _math(nt, "MULTIPLY", yu, yu)), 0.0)
    th = _math(nt, "ARCTAN2", yu, xu)
    # 줄 폭을 1.7~2.4로 흔든다 — 반지름을 사인으로 비틀어 줄 높이 2.0짜리 벽돌에 넣는다
    rw = _math(nt, "ADD", ru, _math(nt, "MULTIPLY", _math(nt, "SINE", _math(nt, "MULTIPLY", ru, 0.9), 0.0), 0.2))
    # 둘레 방향 호 길이는 그 줄 가운데 반지름으로 잰다 — 점마다 제 반지름을 쓰면 이음 줄이 θ·r = 일정한 곡선이 되어
    # 소용돌이처럼 휘었다(2차 렌더). 줄 번호 = floor(rw / 2)
    row_r = _math(nt, "MULTIPLY", _math(nt, "ADD", _math(nt, "FLOOR", _math(nt, "DIVIDE", rw, 2.0), 0.0), 0.5), 2.0)
    vec = nt.nodes.new("ShaderNodeCombineXYZ")
    _link(nt, _math(nt, "MULTIPLY", th, row_r), vec.inputs[0])
    _link(nt, rw, vec.inputs[1])
    tone = _brick(nt, vec.outputs[0], (0.25, 0.225, 0.19, 1), (0.19, 0.2, 0.215, 1))          # 따뜻한·차가운 회색
    value = _brick(nt, vec.outputs[0], (0.85, 0.85, 0.85, 1), (1.15, 1.15, 1.15, 1))          # 판마다 명도 ±15%
    stone = _mix(nt, 1.0, tone.outputs["Color"], value.outputs["Color"], blend="MULTIPLY")
    stone = _mix(nt, _math(nt, "MULTIPLY", _noise(nt, co, 45.0, detail=8.0), 0.35), stone, (0.13, 0.125, 0.115, 1))
    mortar = tone.outputs["Fac"]
    # 여유 항목(PM 3차, 약하게) — 판석이 새것처럼 깨끗했다: 같은 판 배치에 줄눈만 넓힌 벽돌 무늬로 「가장자리 가까움」을 얻어
    # 거기에만 작은 이 빠짐(잘게 끊긴 노이즈)과 줄눈에서 번진 흙때(부드러운 띠 × 얼룩)를 넣는다
    edge_near = _brick(nt, vec.outputs[0], (0, 0, 0, 1), (0, 0, 0, 1), mortar=0.26, smooth=0.35).outputs["Fac"]
    chips = _math(nt, "MULTIPLY", edge_near, _ramp(nt, _noise(nt, co, 70.0, detail=3.0),
                                                   [(0.0, (0, 0, 0, 1)), (0.6, (0, 0, 0, 1)), (0.68, (1, 1, 1, 1))]))
    grime_band = _brick(nt, vec.outputs[0], (0, 0, 0, 1), (0, 0, 0, 1), mortar=0.2, smooth=1.0).outputs["Fac"]
    # 닳은 동선 넷 — 축(x·y) 따라 폭 5, 나침반 바깥
    along = _math(nt, "MINIMUM", _math(nt, "ABSOLUTE", xu, 0.0), _math(nt, "ABSOLUTE", yu, 0.0))
    path = nt.nodes.new("ShaderNodeMapRange")
    _link(nt, along, path.inputs["Value"])
    path.inputs["From Min"].default_value, path.inputs["From Max"].default_value = 1.5, 3.2
    path.inputs["To Min"].default_value, path.inputs["To Max"].default_value = 1.0, 0.0
    path_out = _math(nt, "MULTIPLY", path.outputs["Result"], _math(nt, "GREATER_THAN", ru, 9.0))
    worn = _mix(nt, 0.6, stone, (0.17, 0.16, 0.145, 1))                                        # 매끈하고 조금 어둡게
    stone = _mix(nt, path_out, stone, worn)
    grime = _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", grime_band, _noise(nt, co, 14.0, detail=5.0)),
                  _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", path_out, 0.7)))                 # 동선은 쓸려 흙때가 적다
    stone = _mix(nt, _math(nt, "MULTIPLY", grime, 0.45), stone, (0.085, 0.07, 0.05, 1))
    stone = _mix(nt, _math(nt, "MULTIPLY", chips, 0.7), stone, (0.07, 0.062, 0.05, 1))
    mortar = _math(nt, "MULTIPLY", mortar, _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", path_out, 0.6)))
    joint_col = _mix(nt, _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", ru, 27.0),
                               _ramp(nt, _noise(nt, co, 9.0, detail=4.0), [(0.0, (0, 0, 0, 1)), (0.45, (0, 0, 0, 1)), (0.6, (1, 1, 1, 1))])),
                     (0.06, 0.05, 0.036, 1), (0.04, 0.07, 0.02, 1))                           # 흙빛 줄눈, 둘레 줄눈만 이끼
    color = _mix(nt, mortar, stone, joint_col)
    # 나침반 — 8방 별: 꼭짓점 사이 반지름 2.4 ~ 꼭짓점 7.0, 꼭짓점마다 현무암·모래색 번갈아 / 바깥 짙은 고리 7.4~8.2
    # 꼭짓점마다 곧은 변 — 2차는 반지름을 각에 비례시켜 변이 휘어 꽃잎처럼 보였다. 꼭짓점 축 기준 국소 좌표(u, |v|)에서
    # 꼭짓점(R_O, 0)과 안쪽 꼭지(R_I, 22.5°)를 잇는 곧은 선 안쪽인지로 판정한다.
    R_I, R_O, half = 2.2, 7.2, math.pi / 8
    k = (R_I * math.sin(half)) / (R_O - R_I * math.cos(half))
    sector = _math(nt, "MULTIPLY", _math(nt, "ADD", th, math.pi), 8.0 / (2 * math.pi))
    frac = _math(nt, "FRACT", _math(nt, "ADD", sector, 0.5), 0.0)
    phi = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", frac, 0.5), 2 * math.pi / 8)
    u = _math(nt, "MULTIPLY", ru, _math(nt, "COSINE", phi, 0.0))
    v = _math(nt, "MULTIPLY", ru, _math(nt, "ABSOLUTE", _math(nt, "SINE", phi, 0.0), 0.0))
    edge = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", R_O, u), k)
    star = _math(nt, "MAXIMUM", _math(nt, "LESS_THAN", v, edge), _math(nt, "LESS_THAN", ru, R_I * math.cos(half)))
    star = _math(nt, "MULTIPLY", star, _math(nt, "LESS_THAN", u, R_O))
    alt = _math(nt, "FLOORED_MODULO", _math(nt, "FLOOR", _math(nt, "ADD", sector, 0.5), 0.0), 2.0)
    basalt = _mix(nt, _noise(nt, co, 30.0), (0.03, 0.03, 0.032, 1), (0.06, 0.06, 0.065, 1))
    sand = _mix(nt, _noise(nt, co, 30.0), (0.42, 0.34, 0.22, 1), (0.52, 0.43, 0.29, 1))
    star_col = _mix(nt, alt, basalt, sand)
    ring = _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", ru, 7.4), _math(nt, "LESS_THAN", ru, 8.2))
    color = _mix(nt, ring, color, basalt)
    color = _mix(nt, star, color, star_col)
    hub = _math(nt, "LESS_THAN", ru, 0.9)
    color = _mix(nt, hub, color, sand)
    return color


def _shade(mat):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    kind = mat.name.split("_", 1)[1]
    emit = None
    if kind == "포석":
        color = _shade_pavement(nt, co, sep)
    elif kind in ("둘레돌", "옹벽"):
        along = _math(nt, "MULTIPLY", _math(nt, "ARCTAN2", sep.outputs[1], sep.outputs[0]), 3.0)
        wall = nt.nodes.new("ShaderNodeCombineXYZ")
        _link(nt, along, wall.inputs[0])
        _link(nt, sep.outputs[2], wall.inputs[1])
        br = _node(nt, "ShaderNodeTexBrick", Scale=12.0, **{"Mortar Size": 0.02, "Brick Width": 0.6, "Row Height": 0.35})
        br.inputs["Color1"].default_value, br.inputs["Color2"].default_value = (0.13, 0.125, 0.115, 1), (0.21, 0.2, 0.18, 1)
        br.inputs["Mortar"].default_value = (0.05, 0.05, 0.045, 1)
        _link(nt, wall.outputs[0], br.inputs["Vector"])
        base = br.outputs["Color"] if kind == "옹벽" else _mix(nt, _noise(nt, co, 20.0), (0.16, 0.155, 0.14, 1), (0.26, 0.25, 0.225, 1))
        color = _mix(nt, _math(nt, "MULTIPLY", _noise(nt, co, 45.0, detail=8.0), 0.45), base, (0.09, 0.085, 0.078, 1))
    elif kind == "쇠":
        base = _mix(nt, _noise(nt, co, 30.0), (0.018, 0.018, 0.02, 1), (0.055, 0.055, 0.06, 1))
        rust = _ramp(nt, _noise(nt, co, 20.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.62, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
        color = _mix(nt, rust, base, (0.12, 0.045, 0.015, 1))
    elif kind == "밧줄":
        wave = _node(nt, "ShaderNodeTexWave", Scale=80.0, Distortion=1.0)
        wave.wave_type, wave.bands_direction = "BANDS", "DIAGONAL"
        _link(nt, co, wave.inputs["Vector"])
        color = _mix(nt, wave.outputs["Fac"], (0.1, 0.075, 0.045, 1), (0.28, 0.22, 0.14, 1))
    elif kind == "등_발광":
        color = emit = _mix(nt, _noise(nt, co, 25.0), (0.85, 0.55, 0.2, 1), (1.0, 0.8, 0.45, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.85
    _link(nt, color, bsdf.inputs["Base Color"])
    if emit is not None:
        _link(nt, emit, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 2.0


def tex_size(name):
    return 4096 if name.endswith("_포석") else (1024 if name.endswith(("_옹벽", "_둘레돌")) else 512)


# ──────────────────────────────────────────────────────────── 조립·내보내기

def assemble(collection):
    gen_portals.MASKS.clear()
    b = build()
    bm = b.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    radius = max(math.hypot(v.co.x, v.co.y) for v in bm.verts)
    assert radius <= R_LIMIT + 1e-3, f"반지름 36을 넘었다: {radius:.2f}"
    assert abs(min(v.co.z for v in bm.verts)) < 1e-3, "최저점 ≠ 0"
    idx = {name: b.mats.index(name) for name in b.mats}
    props = {v for f in bm.faces if f.material_index in (idx[IRON], idx[ROPE], idx[GLASS]) for v in f.verts}
    lamp_base = {v for f in bm.faces if f.material_index == idx[CURB_MAT] for v in f.verts
                 if math.hypot(v.co.x, v.co.y) > R_PROPS[0] and v.co.z > 0.55}
    platform = [v for v in bm.verts if v not in props and v not in lamp_base]
    platform_top = max(v.co.z for v in platform)
    assert platform_top <= 1.2 + 1e-3, f"단상 높이 {platform_top:.2f} > 1.2"
    inner = [v for v in bm.verts if abs(v.co.x) <= KEEP + 1e-3 and abs(v.co.y) <= KEEP + 1e-3]
    assert max((v.co.z for v in inner), default=0.0) <= TOP + 1e-3, "비움 정사각(|x|,|y| ≤ 23) 안에 광장 윗면보다 높은 것이 있다"
    for v in props | lamp_base:
        rr = math.hypot(v.co.x, v.co.y)
        assert R_PROPS[0] - 1e-3 <= rr <= R_PROPS[1] + 1e-3, f"계선주·가스등이 고리(33.5~35.5) 밖: 반지름 {rr:.2f}"
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    assert tri_count <= 3500, f"삼각형 {tri_count} > 3,500"
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    mesh = bpy.data.meshes.new(NAME)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(NAME, mesh)
    collection.objects.link(obj)
    for name in b.mats:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        _shade(mat)
        mesh.materials.append(mat)
    return obj, dict(radius=radius, tris=tri_count, platform_top=platform_top)


def bake(obj, folder):
    gen_portals.tex_size = tex_size
    gen_portals.bake(obj, folder)


def build_in_window(collection, folder):
    obj, info = assemble(collection)
    unwrap(obj)
    bake(obj, folder)
    return obj


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col = bpy.data.collections.new("판_구조물")
    bpy.context.scene.collection.children.link(col)
    obj, info = assemble(col)
    unwrap(obj)
    bake(obj, os.path.join(OUT, "Textures"))
    for o in _live_objects():
        o.select_set(o is obj)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(OUT, NAME + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, global_scale=UNITS,
                             path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
    print(f"만듦  {NAME}  삼각형 {info['tris']}  반지름 {info['radius']:.2f}  단상 윗면 최고 {info['platform_top']:.2f}  "
          f"재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
