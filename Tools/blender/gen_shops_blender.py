"""레인 상점 — blender 세션 몫: 도박소·강화소(·해적단퀘스트·항해일지 예정). 공통부는 shops_common.py.

화면 없이(정본):  blender --background --factory-startup --python gen_shops_blender.py [-- 상점_도박소 ...]
결과: 이 폴더/structures/<이름>.fbx, structures/Textures/<재질>.png
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from shops_common import (EAVE_WALL, PLINTH, ROOF_T, SIGN_Z, SLOPE, STEP, WX, WY, Builder, _link, _math, _mix,  # noqa: E402
                          _node, _noise, _ramp, frame, gable_sign, register_shader, roof_z, run, window)

OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")


def make_gambling():
    """도박소 — 황토 회벽 + 붉게 칠한 목재, 붉은 등 둘, 박공 주사위 간판, 반쯤 열린 붉은 커튼 문. 지붕 테라코타."""
    b = Builder()
    P = "상점_도박소_"
    wall, timber, stone, roof = P + "회벽", P + "붉은목재", P + "석재", P + "지붕"
    cloth, lamp, dice, pips, dark, iron = P + "천", P + "등_발광", P + "주사위", P + "검정", P + "어둠", P + "쇠"
    frame(b, wall, timber, stone, roof)
    y = -WY
    dw, dh = 2.8, 5.2
    b.box(-dw / 2, dw / 2, y - 0.04, y, PLINTH, PLINTH + dh, dark, skip=("+y",))
    b.box(-dw / 2 - 0.35, -dw / 2, y - 0.3, y, PLINTH, PLINTH + dh + 0.35, timber, skip=("+y",))
    b.box(dw / 2, dw / 2 + 0.35, y - 0.3, y, PLINTH, PLINTH + dh + 0.35, timber, skip=("+y",))
    b.box(-dw / 2 - 0.5, dw / 2 + 0.5, y - 0.4, y, PLINTH + dh, PLINTH + dh + 0.45, timber, skip=("+y",))
    b.box(-dw / 2 - 0.4, dw / 2 + 0.4, y - 0.55, y, 0.0, PLINTH, stone, skip=("+y",))
    b.extrude([(-dw / 2 + 0.02, y - 0.12, PLINTH + dh), (-0.05, y - 0.12, PLINTH + dh),
               (-0.25, y - 0.32, PLINTH + 1.4), (-dw / 2 + 0.02, y - 0.2, PLINTH + 1.2)], (0, 0.1, 0), cloth)
    b.extrude([(0.45, y - 0.1, PLINTH + dh), (dw / 2 - 0.02, y - 0.1, PLINTH + dh),
               (dw / 2 - 0.02, y - 0.1, PLINTH + 0.9), (0.95, y - 0.1, PLINTH + 2.6)], (0, -0.3, 0), cloth)
    b.box(dw / 2 - 0.55, dw / 2 - 0.05, y - 0.5, y - 0.05, PLINTH + 2.6, PLINTH + 2.85, timber)
    for cx in (-2.85, 2.85):
        window(b, cx, 3.6, 1.3, 2.4, timber, dark, shutter=timber)
    front = gable_sign(b, timber, cloth)
    s = 0.9
    for cx, cz, turn, faces in ((-0.55, SIGN_Z + 0.05, 12.0, 5), (0.6, SIGN_Z - 0.1, -18.0, 3)):
        rot = Matrix.Translation((cx, front - s / 2, cz)) @ Matrix.Rotation(math.radians(turn), 4, "Y")
        corner = [(-s / 2, -s / 2, -s / 2), (s / 2, -s / 2, -s / 2), (s / 2, s / 2, -s / 2), (-s / 2, s / 2, -s / 2)]
        b.extrude(corner, (0, 0, s), dice, matrix=rot)
        pip = 0.16
        spots = {5: [(-0.24, -0.24), (0.24, -0.24), (0, 0), (-0.24, 0.24), (0.24, 0.24)],
                 3: [(-0.26, -0.26), (0, 0), (0.26, 0.26)]}[faces]
        for px, pz in spots:
            b.extrude([(px - pip / 2, -s / 2 - 0.001, pz - pip / 2), (px + pip / 2, -s / 2 - 0.001, pz - pip / 2),
                       (px + pip / 2, -s / 2 - 0.001, pz + pip / 2), (px - pip / 2, -s / 2 - 0.001, pz + pip / 2)],
                      (0, -0.05, 0), pips, caps=(False, True), matrix=rot)
        for px, py in ((-0.22, -0.22), (0.22, 0.22)):
            b.extrude([(px - pip / 2, py - pip / 2, s / 2 + 0.001), (px + pip / 2, py - pip / 2, s / 2 + 0.001),
                       (px + pip / 2, py + pip / 2, s / 2 + 0.001), (px - pip / 2, py + pip / 2, s / 2 + 0.001)],
                      (0, 0, 0.05), pips, caps=(False, True), matrix=rot)
    for cx in (-2.3, 2.3):
        ly = y - 0.85
        b.box(cx - 0.06, cx + 0.06, ly - 0.1, y, 7.55, 7.7, iron, skip=("+y",))
        b.extrude([(cx - 0.04, y, 7.55), (cx - 0.04, y, 6.95), (cx - 0.04, y - 0.55, 7.55)], (0.08, 0, 0), iron)
        b.box_c(cx, ly, 0.05, 0.05, 7.0, 0.55, iron)
        b.cylinder((cx, ly, 6.85), (0, 0, 1), 0.18, 0.15, timber, sides=10)
        b.cylinder((cx, ly, 6.6), (0, 0, 1), 0.33, 0.25, lamp, sides=10)
        b.cylinder((cx, ly, 5.95), (0, 0, 1), 0.46, 0.65, lamp, sides=10)
        b.cylinder((cx, ly, 5.7), (0, 0, 1), 0.33, 0.25, lamp, sides=10)
        b.cylinder((cx, ly, 5.55), (0, 0, 1), 0.18, 0.15, timber, sides=10)
        b.box_c(cx, ly, 0.06, 0.06, 5.0, 0.55, cloth)
    b.cylinder((-3.55, y - 0.8, 0.0), (0, 0, 1), 0.5, 1.2, timber, sides=10)
    return b


def make_smithy():
    """강화소(대장간) — 막돌벽 + 짙은 목재, 벽돌 굴뚝(연기 자리), 화덕 불빛(불 자리), 모루·풀무, 박공 모루 간판(밝은 판),
    차양. 지붕 짙은 슬레이트 회청."""
    b = Builder()
    P = "상점_강화소_"
    wall, timber, stone, roof = P + "돌벽", P + "목재", P + "석재", P + "지붕"
    brick, iron, forge, leather, board = P + "벽돌", P + "쇠", P + "화덕_발광", P + "가죽", P + "밝은목재"
    frame(b, wall, timber, stone, roof)
    y = -WY
    b.box(2.2, 3.4, 0.6, 1.8, PLINTH, 15.45, brick, skip=("bottom",))
    b.box(2.05, 3.55, 0.45, 1.95, 15.45, 15.8, stone, skip=("bottom",))
    b.marker("연기_자리_01", (2.8, 1.2, 16.1))
    fx0, fx1, fz0, fz1 = 0.7, 3.5, PLINTH + 0.6, PLINTH + 3.6
    b.box(fx0, fx1, y - 0.03, y, fz0, fz1, forge, skip=("+y",))
    b.box(fx0 - 0.45, fx0, y - 0.45, y, PLINTH, fz1 + 0.45, brick, skip=("+y",))
    b.box(fx1, fx1 + 0.45, y - 0.45, y, PLINTH, fz1 + 0.45, brick, skip=("+y",))
    b.box(fx0 - 0.45, fx1 + 0.45, y - 0.45, y, fz1, fz1 + 0.6, brick, skip=("+y",))
    b.box(fx0 - 0.45, fx1 + 0.45, y - 0.9, y, PLINTH, fz0, brick, skip=("+y",))
    b.marker("불_자리_01", ((fx0 + fx1) / 2, y - 0.4, fz0 + 0.6))
    bx = fx0 - 1.3
    b.extrude([(bx, y - 1.15, PLINTH + 1.5), (bx, y - 0.5, PLINTH + 1.2), (bx, y - 0.5, PLINTH + 1.9)], (0.9, 0, 0), leather)
    b.extrude([(bx - 0.05, y - 1.25, PLINTH + 1.45), (bx - 0.05, y - 0.45, PLINTH + 1.9), (bx - 0.05, y - 0.45, PLINTH + 2.0),
               (bx - 0.05, y - 1.25, PLINTH + 1.55)], (1.0, 0, 0), timber)
    dx0, dx1, dh = -3.3, -1.1, 5.0
    b.box(dx0, dx1, y - 0.12, y, PLINTH, PLINTH + dh, timber, skip=("+y",))
    for z in (PLINTH + 0.9, PLINTH + dh - 0.9):
        b.box(dx0 + 0.1, dx1 - 0.3, y - 0.18, y - 0.12, z, z + 0.22, iron, skip=("+y",))
    b.box(dx0 - 0.3, dx0, y - 0.3, y, PLINTH, PLINTH + dh + 0.3, timber, skip=("+y",))
    b.box(dx1, dx1 + 0.3, y - 0.3, y, PLINTH, PLINTH + dh + 0.3, timber, skip=("+y",))
    b.box(dx0 - 0.4, dx1 + 0.4, y - 0.4, y, PLINTH + dh, PLINTH + dh + 0.4, timber, skip=("+y",))
    b.extrude([(-WX - 0.2, y, 6.9), (-WX - 0.2, y - 1.05, 6.25), (-WX - 0.2, y - 1.05, 6.5), (-WX - 0.2, y, 7.15)],
              (2 * WX + 0.4, 0, 0), timber)
    for cx in (-WX + 0.05, WX - 0.05):
        b.box_c(cx, y - 0.95, 0.3, 0.3, PLINTH, 6.3 - PLINTH, timber, skip=("bottom",))
    ax, ay = -0.35, y - 0.85
    b.cylinder((ax, ay, 0.0), (0, 0, 1), 0.5, 1.35, timber, sides=9)
    b.box_c(ax, ay, 0.8, 0.55, 1.35, 0.3, iron)
    b.box_c(ax, ay, 0.45, 0.35, 1.65, 0.3, iron)
    b.box_c(ax, ay, 1.3, 0.5, 1.95, 0.42, iron)
    b.spike([(ax + 0.64, ay - 0.2, 2.02), (ax + 0.64, ay + 0.2, 2.02), (ax + 0.64, ay + 0.2, 2.36), (ax + 0.64, ay - 0.2, 2.36)],
            (ax + 1.45, ay, 2.28), iron)
    # 박공 간판 — 밝은 나무 판 + 굵은 쇠 모루(3차: 짙은 판 위 짙은 기호가 게임 시점에서 안 읽혔다)
    front = gable_sign(b, timber, board)
    anvil = [(-0.75, -0.65), (0.75, -0.65), (0.5, -0.35), (0.3, -0.35), (0.3, 0.0), (1.05, 0.0), (0.75, 0.45), (-0.8, 0.45),
             (-0.8, 0.0), (-0.3, 0.0), (-0.3, -0.35), (-0.5, -0.35)]
    b.extrude([(x * 1.05, front, SIGN_Z + z * 1.05) for x, z in anvil], (0, -0.16, 0), iron)
    for k in range(3):
        b.cylinder((WX + 0.1, -1.8 + k * 0.5, 0.3), (0, 1, 0), 0.22, 0.45, timber, sides=6, phase=0.3 * k)
    return b


# ──────────────────────────────────────────────────────────── 해적단퀘스트·항해일지 전용 재질 종류

@register_shader("헌돛천")
def _old_sail(nt, c, name):
    """지붕에 덧댄 헌 돛천 — 누렇게 바랜 바탕 + 박음질 이음선 + 빗물 얼룩(1차 밝은 돛천은 종이 한 장처럼 떴다)."""
    base = _mix(nt, _noise(nt, c.co, 12.0), (0.2, 0.17, 0.12, 1), (0.32, 0.28, 0.2, 1))
    seam = _node(nt, "ShaderNodeTexWave", Scale=7.0, Distortion=0.4)
    seam.wave_type, seam.bands_direction = "BANDS", "Y"
    _link(nt, c.co, seam.inputs["Vector"])
    line = _ramp(nt, seam.outputs["Fac"], [(0.0, (0.8, 0.8, 0.8, 1)), (0.06, (0, 0, 0, 1))])
    color = _mix(nt, line, base, (0.08, 0.065, 0.045, 1))
    stain = _ramp(nt, _noise(nt, c.co, 5.0, detail=6.0), [(0.0, (0, 0, 0, 1)), (0.55, (0, 0, 0, 1)), (0.7, (0.7, 0.7, 0.7, 1))])
    return _mix(nt, stain, color, (0.08, 0.07, 0.05, 1)), None


@register_shader("뼈")
def _bone(nt, c, name):
    return _mix(nt, _noise(nt, c.co, 22.0), (0.52, 0.48, 0.38, 1), (0.7, 0.66, 0.55, 1)), None


@register_shader("널벽", big=True)
def _plank_wall(nt, c, name):
    """비바람에 바랜 세로 널벽 — 널 사이 짙은 틈 + 결 + 발치 때."""
    wave = _node(nt, "ShaderNodeTexWave", Scale=9.0, Distortion=0.6, Detail=1.0)
    wave.wave_type, wave.bands_direction = "BANDS", "X"
    _link(nt, c.wall, wave.inputs["Vector"])
    gap = _ramp(nt, wave.outputs["Fac"], [(0.0, (0, 0, 0, 1)), (0.08, (0, 0, 0, 1)), (0.16, (1, 1, 1, 1))])
    stretch = _node(nt, "ShaderNodeMapping")
    stretch.inputs["Scale"].default_value = (1.0, 1.0, 0.12)
    _link(nt, c.co, stretch.inputs["Vector"])
    # 회색 쪽으로 바랜 밝은 널 — 짙은 갈색 널지붕과 명도 차를 둔다(1차는 지붕과 같은 톤이라 한 덩어리)
    board = _mix(nt, _noise(nt, stretch.outputs["Vector"], 50.0, detail=6.0), (0.13, 0.12, 0.105, 1), (0.26, 0.245, 0.215, 1))
    color = _mix(nt, gap, (0.02, 0.018, 0.016, 1), board)
    return _mix(nt, _math(nt, "MULTIPLY", c.low, 0.6), color, (0.05, 0.04, 0.03, 1)), None


@register_shader("해도")
def _chart(nt, c, name):
    """창에 펼친 해도 — 누런 양피지 + 옅은 격자선 + 푸르스름한 해안 얼룩."""
    paper = _mix(nt, _noise(nt, c.co, 14.0), (0.42, 0.34, 0.2, 1), (0.58, 0.49, 0.31, 1))
    coast = _ramp(nt, _noise(nt, c.co, 9.0, detail=4.0), [(0.0, (0, 0, 0, 1)), (0.52, (0, 0, 0, 1)), (0.56, (0.7, 0.7, 0.7, 1))])
    color = _mix(nt, coast, paper, (0.16, 0.22, 0.26, 1))
    for direction in ("X", "Z"):
        grid = _node(nt, "ShaderNodeTexWave", Scale=12.0, Distortion=0.0)
        grid.wave_type, grid.bands_direction = "BANDS", direction
        _link(nt, c.co, grid.inputs["Vector"])
        line = _ramp(nt, grid.outputs["Fac"], [(0.0, (0.6, 0.6, 0.6, 1)), (0.05, (0, 0, 0, 1))])
        color = _mix(nt, line, color, (0.12, 0.09, 0.06, 1))
    return color, None


@register_shader("지구의")
def _globe(nt, c, name):
    ocean = _mix(nt, _noise(nt, c.co, 30.0), (0.025, 0.07, 0.16, 1), (0.05, 0.12, 0.24, 1))
    land = _ramp(nt, _noise(nt, c.co, 45.0, detail=4.0), [(0.0, (0, 0, 0, 1)), (0.55, (0, 0, 0, 1)), (0.6, (1, 1, 1, 1))])
    return _mix(nt, land, ocean, (0.36, 0.28, 0.14, 1)), None


def _sphere(b, center, radius, mat, u=12, v=8):
    made = bmesh.ops.create_uvsphere(b.bm, u_segments=u, v_segments=v, radius=radius, matrix=Matrix.Translation(center))["verts"]
    for f in {f for vert in made for f in vert.link_faces}:
        f.material_index = b.m(mat)
        f.smooth = True


def _rotated_bar(b, center, length, width, depth, angle_deg, mat, y_face):
    """정면(−Y) 판 위에 붙는 기울어진 막대(뼈 등) — 앞면 y_face에서 앞으로 depth."""
    rot = Matrix.Translation((center[0], y_face, center[1])) @ Matrix.Rotation(math.radians(angle_deg), 4, "Y")
    b.extrude([(-length / 2, 0, -width / 2), (length / 2, 0, -width / 2), (length / 2, 0, width / 2), (-length / 2, 0, width / 2)],
              (0, -depth, 0), mat, matrix=rot)


def make_pirate_quest():
    """해적단퀘스트(선술집) — 바랜 널벽, 스윙 문, 현상수배 전단, 럼 통, 해골 깃발, 박공 해골 간판. 지붕 바랜 널지붕 + 돛천 덧댐."""
    b = Builder()
    P = "상점_해적단퀘스트_"
    wall, timber, stone, roof = P + "널벽", P + "목재", P + "석재", P + "지붕"
    sail, dark, bone, black, iron, board = P + "돛천", P + "어둠", P + "뼈", P + "검정", P + "쇠", P + "밝은목재"
    frame(b, wall, timber, stone, roof)
    y = -WY
    # 입구 — 가운데 어두운 문간 + 허리높이 스윙 문 두 짝
    dw, dh = 2.6, 5.3
    b.box(-dw / 2, dw / 2, y - 0.04, y, PLINTH, PLINTH + dh, dark, skip=("+y",))
    b.box(-dw / 2 - 0.35, -dw / 2, y - 0.3, y, PLINTH, PLINTH + dh + 0.35, timber, skip=("+y",))
    b.box(dw / 2, dw / 2 + 0.35, y - 0.3, y, PLINTH, PLINTH + dh + 0.35, timber, skip=("+y",))
    b.box(-dw / 2 - 0.5, dw / 2 + 0.5, y - 0.4, y, PLINTH + dh, PLINTH + dh + 0.45, timber, skip=("+y",))
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.06, s * (dw / 2 - 0.05)))
        b.box(x0, x1, y - 0.35, y - 0.22, PLINTH + 1.3, PLINTH + 3.6, timber)
        for z in (PLINTH + 1.9, PLINTH + 2.6):
            b.box(x0 + 0.1, x1 - 0.1, y - 0.42, y - 0.35, z, z + 0.14, black, skip=("+y",))
    # 창 하나(오른쪽) + 현상수배 전단 둘(왼쪽)
    window(b, 2.85, 3.6, 1.3, 2.4, timber, dark, shutter=timber)
    for cx, cz, turn in ((-3.25, 4.2, 4.0), (-2.2, 3.6, -7.0)):
        rot = Matrix.Translation((cx, y, cz)) @ Matrix.Rotation(math.radians(turn), 4, "Y")
        b.extrude([(-0.42, 0, -0.6), (0.42, 0, -0.6), (0.42, 0, 0.6), (-0.42, 0, 0.6)], (0, -0.04, 0), sail, matrix=rot)
        b.extrude([(-0.2, -0.04, 0.02), (0.2, -0.04, 0.02), (0.26, -0.04, -0.3), (-0.26, -0.04, -0.3)], (0, -0.02, 0), black, matrix=rot)
        b.cylinder(rot @ Vector((0, -0.04, 0.2)), (0, -1, 0), 0.15, 0.02, black, sides=8)
    # 럼 통 — 문 오른쪽에 둘(하나는 눕혀 얹음), 쇠테
    for cx, cy, z0, axis, length in ((1.95, y - 0.75, 0.0, (0, 0, 1), 1.3), (3.05, y - 0.75, 0.0, (0, 0, 1), 1.3)):
        b.cylinder((cx, cy, z0), axis, 0.55, length, timber, sides=10)
        for hz in (0.25, 1.0):
            b.cylinder((cx, cy, z0 + hz), axis, 0.575, 0.1, iron, sides=10)
    b.cylinder((1.85, y - 0.75, 1.85), (1, 0, 0), 0.45, 1.3, timber, sides=10)
    # 해골 깃발 — 왼쪽 앞 모서리 앞에 선 장대, 지붕 앞끝보다 앞에서 휘날림
    b.box_c(-4.15, -3.98, 0.14, 0.14, 0.0, 13.6, timber, skip=("bottom",))
    b.box(-4.05, -2.35, -4.02, -3.92, 11.55, 12.9, black)
    b.cylinder((-3.2, -4.02, 12.32), (0, -1, 0), 0.26, 0.04, bone, sides=10)
    _rotated_bar(b, (-3.2, 11.85), 0.95, 0.12, 0.03, 30.0, bone, -4.02)
    _rotated_bar(b, (-3.2, 11.85), 0.95, 0.12, 0.03, -30.0, bone, -4.02)
    # 박공 간판 — 밝은 판 위 해골과 엇갈린 뼈(짙은 눈구멍)
    front = gable_sign(b, timber, board)
    _rotated_bar(b, (0.0, SIGN_Z - 0.2), 2.0, 0.24, 0.1, 35.0, bone, front)
    _rotated_bar(b, (0.0, SIGN_Z - 0.2), 2.0, 0.24, 0.1, -35.0, bone, front)
    b.cylinder((0.0, front - 0.1, SIGN_Z + 0.2), (0, -1, 0), 0.55, 0.14, bone, sides=12)
    b.box(-0.3, 0.3, front - 0.22, front - 0.1, SIGN_Z - 0.45, SIGN_Z - 0.2, bone)
    for ex in (-0.22, 0.22):
        b.box(ex - 0.12, ex + 0.12, front - 0.28, front - 0.24, SIGN_Z + 0.12, SIGN_Z + 0.36, black, skip=("+y",))
    # 지붕 돛천 덧댐 — 왼쪽 비탈 가운데에 한 장
    d = Vector((1.0, SLOPE)).normalized()                         # 왼 비탈 처마→용마루 (x, z)
    n = Vector((-d.y, d.x))
    lift = ROOF_T + STEP + 0.06
    corners = []
    for t, yy in ((0.25, -1.6), (0.25, 1.2), (0.62, 1.4), (0.62, -1.4)):
        x = -(1 - t) * 4.2
        base = Vector((x, roof_z(abs(x))))
        p = base + n * lift
        corners.append((p.x, yy, p.y))
    b.extrude(corners, (n.x * 0.05, 0, n.y * 0.05), P + "헌돛천")
    # 처진 가장자리 — 처마 쪽 끝이 켜 턱을 넘어 아래로 늘어진 자락(PM: 종이 판처럼 안 보이게)
    d3, n3 = Vector((d.x, 0, d.y)), Vector((n.x, 0, n.y))
    c0, c1 = Vector(corners[0]), Vector(corners[1])
    flap = [c0, c1, c1 - d3 * 0.55 - n3 * 0.12 + Vector((0, 0.1, 0)), c0 - d3 * 0.7 - n3 * 0.12 + Vector((0, -0.05, 0))]
    b.extrude([tuple(p) for p in flap], (n.x * 0.04, 0, n.y * 0.04), P + "헌돛천")
    return b


def make_logbook():
    """항해일지(항해사 사무소) — 흰 회벽 반목조, 해도가 펼쳐진 큰 창, 지구의, 박공 망원경, 나침반 간판. 지붕 남색 유약 기와."""
    b = Builder()
    P = "상점_항해일지_"
    wall, timber, stone, roof = P + "흰회벽", P + "목재", P + "석재", P + "지붕"
    chart, globe, brass, dark, black = P + "해도", P + "지구의", P + "황동", P + "어둠", P + "검정"
    frame(b, wall, timber, stone, roof)
    y = -WY
    # 반목조 — 정면 세로 기둥 둘 + 허리 띠
    for cx in (-1.75, 0.35):
        b.box_c(cx, y - 0.08, 0.3, 0.16, PLINTH, EAVE_WALL - PLINTH, timber)
    b.box(-WX, WX, y - 0.16, y, 6.7, 7.0, timber, skip=("+y",))
    # 문 — 왼쪽, 짙은 널문 + 황동 문고리·판
    dx0, dx1, dh = -3.45, -1.95, 5.2
    b.box(dx0, dx1, y - 0.12, y, PLINTH, PLINTH + dh, timber, skip=("+y",))
    b.cylinder((dx1 - 0.3, y - 0.12, PLINTH + 2.6), (0, -1, 0), 0.14, 0.06, brass, sides=10)
    b.box(dx0 + 0.3, dx1 - 0.3, y - 0.15, y - 0.12, PLINTH + 4.2, PLINTH + 4.6, brass, skip=("+y",))
    # 큰 창 — 해도가 펼쳐진 창(유리 대신 해도 판)
    window(b, 2.2, 2.6, 2.6, 3.3, timber, chart, shutter=None)
    # 지구의 — 문 오른쪽 앞 받침대 위
    gx, gy = -0.75, y - 0.8
    b.cylinder((gx, gy, 0.0), (0, 0, 1), 0.38, 0.2, timber, sides=8)
    b.cylinder((gx, gy, 0.2), (0, 0, 1), 0.1, 1.5, timber, sides=6)
    _sphere(b, Vector((gx, gy, 2.3)), 0.55, globe)
    for k in range(10):                                           # 황동 자오선 고리(xz 면 원을 짧은 막대로)
        a0, a1 = 2 * math.pi * k / 10, 2 * math.pi * (k + 1) / 10
        p0 = Vector((gx + 0.63 * math.cos(a0), gy, 2.3 + 0.63 * math.sin(a0)))
        p1 = Vector((gx + 0.63 * math.cos(a1), gy, 2.3 + 0.63 * math.sin(a1)))
        tangent = p1 - p0
        side = Vector((0, 1, 0)).cross(tangent).normalized() * 0.035
        sq = [p0 - side + Vector((0, -0.035, 0)), p0 + side + Vector((0, -0.035, 0)),
              p0 + side + Vector((0, 0.035, 0)), p0 - side + Vector((0, 0.035, 0))]
        b.extrude(sq, tangent, brass)
    # 망원경 — 박공 오른쪽 벽에서 앞·위로 내민 황동 통
    # PM 검수: 1차(굵기 0.17)는 게임 시점에서 거의 안 보였다 — 약 1.6배로 굵히고, 나침반 간판(x ±1.3)과 안 겹치게
    # 오른쪽 위로 비스듬히 내민다. 끝 x 약 3.5 · y 약 −4.3(앞 한계 −4.5 안), 지붕 앞끝보다 앞이라 지붕을 안 뚫는다.
    mount = Vector((2.3, y, 9.6))
    aim = Vector((0.6, -0.45, 0.65)).normalized()
    b.box_c(mount.x, y - 0.14, 0.5, 0.28, mount.z - 0.35, 0.7, timber)
    start = mount + Vector((0, -0.25, 0))
    b.cylinder(start, aim, 0.27, 0.95, brass, sides=10)
    b.cylinder(start + aim * 0.95, aim, 0.19, 0.7, brass, sides=10)
    b.cylinder(start + aim * 0.5, aim, 0.3, 0.14, black, sides=10)                                  # 가운데 띠
    # 박공 간판 — 짙은 판 위 황동 나침반(8방 별)
    front = gable_sign(b, timber, black)
    star = []
    for k in range(16):
        a = math.pi / 2 - 2 * math.pi * k / 16
        r = 1.0 if k % 4 == 0 else (0.5 if k % 2 == 0 else 0.22)
        star.append((0.95 * r * math.cos(a), front, SIGN_Z + 0.95 * r * math.sin(a)))
    b.extrude(star, (0, -0.12, 0), brass)
    b.cylinder((0.0, front - 0.12, SIGN_Z), (0, -1, 0), 0.16, 0.05, black, sides=10)
    return b


SHOPS = {"상점_도박소": make_gambling, "상점_강화소": make_smithy,
         "상점_해적단퀘스트": make_pirate_quest, "상점_항해일지": make_logbook}

if __name__ == "__main__":
    run(SHOPS, OUT)
