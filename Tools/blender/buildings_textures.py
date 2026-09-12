"""건물 공용 텍스처 — 재질 셰이더 37종을 절차적으로 만들고 UV 0~1 평면에 구워 PNG로 저장한다.

화면 없이 돈다(굽기):
    blender --background --factory-startup --python Tools/blender/buildings_textures.py
    blender --background --factory-startup --python Tools/blender/buildings_textures.py -- 건물_외벽_ 건물_지붕_

buildings_common.py(건물 13종 조립 담당)가 이 파일의 MATERIALS·SHADERS·material()·bake_all()을
그대로 가져다 쓴다 — 이름·모양을 바꾸지 않는다. 노드 도구(_tree·_math·_noise·_voronoi·_ramp·_mix·_sep_uv)는
gen_walls.py 것을 그대로 재사용한다(중복 정의 금지, import).

🔴 재질 이름 = 텍스처 파일 이름: Assets/Art/Buildings/Textures/<이름>.png. 전부 `건물_` 접두(창에서 벽의
`벽돌` 등과 안 섞이게). 알파가 필요한 재질만 이름 끝 `_잎카드`(철조망·철망 — 배경 투명, 나머지는 전부 불투명).
셰이더는 UV만 쓴다(오브젝트 좌표 아님) — 타일 크기는 MATERIALS의 tile 값을 보고 호출부가 UV를 나눠 반복한다.
tile=None이면 FIT(호출부가 면 하나에 UV 0~1 그대로 붙인다 — 창문·시계판·국기처럼 한 재질이 한 개 뿐인 경우).
"""

import math
import os
import sys

import bpy
import numpy as np

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Buildings")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gen_walls import _tree, _math, _noise, _voronoi, _ramp, _mix, _sep_uv  # noqa: E402


# ──────────────────────────────────────────────────────────── 재질 목록
# 이름: (tile — 텍스처 한 변이 덮는 게임 단위(사람 키 20), None이면 FIT / 뷰포트 색 / 굽기 픽셀).
MATERIALS = {
    # 벽체
    "건물_벽돌_붉은":       (8,    (0.55, 0.20, 0.16, 1.0), 512),
    "건물_콘크리트":         (12,   (0.60, 0.59, 0.56, 1.0), 512),
    "건물_외벽_흰":         (12,   (0.76, 0.75, 0.71, 1.0), 512),
    "건물_타일_베이지":      (8,    (0.80, 0.74, 0.60, 1.0), 512),
    "건물_외벽_노랑":        (12,   (0.90, 0.82, 0.56, 1.0), 256),
    "건물_외벽_분홍":        (12,   (0.88, 0.72, 0.70, 1.0), 256),
    "건물_외벽_하늘":        (12,   (0.68, 0.79, 0.86, 1.0), 256),
    "건물_외벽_연두":        (12,   (0.76, 0.83, 0.62, 1.0), 256),
    "건물_나무_판":         (6,    (0.24, 0.17, 0.11, 1.0), 256),
    "건물_셔터":           (6,    (0.60, 0.61, 0.63, 1.0), 256),
    # 창·유리
    "건물_유리창":          (None, (0.30, 0.42, 0.52, 1.0), 256),
    "건물_유리_커튼월":       (8,    (0.20, 0.42, 0.45, 1.0), 512),
    "건물_원룸창":          (None, (0.05, 0.06, 0.09, 1.0), 256),
    # 지붕·옥상
    "건물_지붕_기와":        (6,    (0.22, 0.26, 0.31, 1.0), 512),
    "건물_지붕_슁글_빨강":     (6,    (0.55, 0.18, 0.15, 1.0), 256),
    "건물_지붕_슁글_파랑":     (6,    (0.18, 0.28, 0.48, 1.0), 256),
    "건물_지붕_슁글_초록":     (6,    (0.20, 0.40, 0.22, 1.0), 256),
    "건물_지붕_슁글_주황":     (6,    (0.68, 0.38, 0.14, 1.0), 256),
    "건물_옥상_방수":        (16,   (0.16, 0.42, 0.27, 1.0), 256),
    # 금속
    "건물_금속_골함석":       (6,    (0.32, 0.34, 0.20, 1.0), 512),
    "건물_금속_회색":        (4,    (0.38, 0.39, 0.41, 1.0), 256),
    "건물_금속_스테인리스":     (4,    (0.68, 0.69, 0.70, 1.0), 256),
    "건물_철조망_잎카드":      (6,    (0.45, 0.44, 0.40, 1.0), 512),
    "건물_철망_잎카드":       (4,    (0.55, 0.55, 0.52, 1.0), 512),
    # 칠·간판
    "건물_색_흰":          (16,   (0.94, 0.94, 0.92, 1.0), 128),
    "건물_색_검정":         (16,   (0.06, 0.06, 0.07, 1.0), 128),
    "건물_색_남색":         (16,   (0.10, 0.14, 0.32, 1.0), 128),
    "건물_색_빨강":         (16,   (0.62, 0.10, 0.10, 1.0), 128),
    "건물_색_노랑":         (16,   (0.85, 0.70, 0.10, 1.0), 128),
    "건물_색_초록":         (16,   (0.12, 0.42, 0.18, 1.0), 128),
    "건물_네온_분홍":        (16,   (0.95, 0.20, 0.55, 1.0), 128),
    "건물_네온_하늘":        (16,   (0.25, 0.70, 0.95, 1.0), 128),
    "건물_도장_주홍":        (6,    (0.56, 0.17, 0.09, 1.0), 256),
    # 바닥·특수
    "건물_바닥_보도블록":      (8,    (0.52, 0.50, 0.47, 1.0), 512),
    "건물_바닥_운동장":       (16,   (0.55, 0.46, 0.32, 1.0), 256),
    "건물_시계판":          (None, (0.92, 0.92, 0.90, 1.0), 256),
    "건물_국기_태극기":       (None, (0.95, 0.95, 0.93, 1.0), 256),
}


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    mat.diffuse_color = MATERIALS[name][1]
    return mat


# ──────────────────────────────────────────────────────────── 셰이더 보조 도구
# (gen_walls의 _tree·_math·_noise·_voronoi·_ramp·_mix·_sep_uv 위에 이 파일에서만 쓰는 것들)

def _hash(nt, val):
    """UV 셀 번호 하나를 0~1 사이 의사난수 하나로 — 벽돌·타일·슁글 등 칸마다 색을 살짝 바꿀 때."""
    return _math(nt, "FRACT", _math(nt, "MULTIPLY", _math(nt, "SINE", _math(nt, "MULTIPLY", val, 12.9898)), 43758.5))


def _grid_lines(nt, u, v, cols, rows, thickness):
    """가로세로 격자 눈금선 마스크(1=선) — 콘크리트 거푸집 판, 유리 커튼월 멀리언에 쓴다."""
    fx = _math(nt, "FRACT", _math(nt, "MULTIPLY", u, cols))
    fy = _math(nt, "FRACT", _math(nt, "MULTIPLY", v, rows))
    near_x = _math(nt, "MINIMUM", fx, _math(nt, "SUBTRACT", 1.0, fx))
    near_y = _math(nt, "MINIMUM", fy, _math(nt, "SUBTRACT", 1.0, fy))
    line_x = _math(nt, "LESS_THAN", near_x, thickness)
    line_y = _math(nt, "LESS_THAN", near_y, thickness)
    return _math(nt, "MAXIMUM", line_x, line_y)


def _combine(nt, x, y, z=0.0):
    n = nt.nodes.new("ShaderNodeCombineXYZ")
    for k, val in ((0, x), (1, y), (2, z)):
        if isinstance(val, (int, float)):
            n.inputs[k].default_value = val
        else:
            nt.links.new(val, n.inputs[k])
    return n.outputs[0]


# ──────────────────────────────────────────────────────────── 벽체

def shader_brick_red(mat):
    """한국 빌라·대학 붉은 벽돌 — 줄눈 회백, 벽돌마다 붉은 기 다르게.

    🔴 2026-09-12 정정(blender 세션 견본 렌더 검수) — 처음엔 타일(게임 단위 8)에 6단이라
    한 단이 1.33 — 실제 건물에 붙이니 한 층(15)에 7단뿐이라 장난감처럼 커 보였다.
    한 단 높이 ≈1이 되게 8단으로, 가로도 비례해 4장(벽돌 폭 2, 2:1 비율)으로 늘렸다."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    row = _math(nt, "MULTIPLY", v, 8.0)
    shift = _math(nt, "MULTIPLY", _math(nt, "MODULO", _math(nt, "FLOOR", row), 2.0), 0.5)
    col = _math(nt, "ADD", _math(nt, "MULTIPLY", u, 4.0), shift)
    fy, fx = _math(nt, "FRACT", row), _math(nt, "FRACT", col)
    mortar_y = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fy, 0.5)), 0.44)
    mortar_x = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fx, 0.5)), 0.42)
    brick = _math(nt, "MULTIPLY", mortar_y, mortar_x)
    cell_id = _math(nt, "ADD", _math(nt, "FLOOR", col), _math(nt, "MULTIPLY", _math(nt, "FLOOR", row), 9.0))
    hue = _hash(nt, cell_id)
    grain = _noise(nt, uv, 34.0, 5.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", hue, 0.5), _math(nt, "MULTIPLY", grain, 0.5), clamp=True)
    brick_col = _ramp(nt, tone, ((0.0, (0.45, 0.12, 0.09, 1)), (0.5, (0.62, 0.20, 0.15, 1)), (1.0, (0.76, 0.34, 0.24, 1))))
    color = _mix(nt, brick, (0.72, 0.70, 0.66, 1), brick_col)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85


def shader_concrete(mat):
    """노출 콘크리트 — 거푸집 판 경계선(3×2칸) + 폼타이 구멍(격자 교차점) + 아래로 갈수록 진해지는 빗물 얼룩.

    🔴 2026-09-12 정정(blender 세션 견본 렌더 검수) — 구멍·경계선이 너무 진하고 커서 바닥판·
    기둥에서 타일처럼 읽혔다. 구멍은 반지름을 줄이고 옅게(완전 대체가 아니라 절반 섞기), 경계선
    대비도 절반, 전체 명도를 0.55 근처로 낮췄다."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    lines = _grid_lines(nt, u, v, 3.0, 2.0, 0.010)
    gx = _math(nt, "ROUND", _math(nt, "MULTIPLY", u, 3.0))
    gy = _math(nt, "ROUND", _math(nt, "MULTIPLY", v, 2.0))
    dx = _math(nt, "SUBTRACT", u, _math(nt, "DIVIDE", gx, 3.0))
    dy = _math(nt, "SUBTRACT", v, _math(nt, "DIVIDE", gy, 2.0))
    d2 = _math(nt, "ADD", _math(nt, "MULTIPLY", dx, dx), _math(nt, "MULTIPLY", dy, dy))
    hole = _math(nt, "LESS_THAN", d2, 0.0004)
    grain = _noise(nt, uv, 22.0, 5.0, 0.65)
    tone = _math(nt, "ADD", 0.45, _math(nt, "MULTIPLY", grain, 0.25), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.38, 0.37, 0.35, 1)), (1.0, (0.60, 0.59, 0.56, 1))))
    color = _mix(nt, _math(nt, "MULTIPLY", lines, 0.5), base, (0.30, 0.29, 0.27, 1))
    color = _mix(nt, _math(nt, "MULTIPLY", hole, 0.6), color, (0.24, 0.23, 0.22, 1))
    streak_noise = _noise(nt, uv, 5.0, 3.0, 0.6)
    streak_col = _math(nt, "GREATER_THAN", streak_noise, 0.58)
    streak_amount = _math(nt, "MULTIPLY", streak_col, _math(nt, "POWER", _math(nt, "SUBTRACT", 1.0, v), 1.6))
    color = _mix(nt, streak_amount, color, (0.30, 0.32, 0.30, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9


def shader_flat_paint(mat, color, streak=False, streak_strength=1.0):
    """미장 도장 — 고운 알갱이(grain)만 있는 거의 단색. streak=True면 위에서 아래로 흐른 때 자국
    (streak_strength로 옅게 조절 — 파스텔 유치원 벽은 흰 벽보다 자국을 옅게 둔다)."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    grain = _noise(nt, uv, 45.0, 4.0, 0.55)
    shade = _math(nt, "ADD", 0.92, _math(nt, "MULTIPLY", grain, 0.16))
    shaded = _mix(nt, 1.0, color, shade, blend="MULTIPLY")
    if streak:
        streak_noise = _noise(nt, uv, 5.0, 3.0, 0.6)
        streak_col = _math(nt, "GREATER_THAN", streak_noise, 0.62)
        amount = _math(nt, "MULTIPLY", streak_col, _math(nt, "POWER", _math(nt, "SUBTRACT", 1.0, v), 2.0))
        amount = _math(nt, "MULTIPLY", amount, streak_strength, clamp=True)
        dirty = _mix(nt, 1.0, shaded, (0.55, 0.53, 0.48, 1), blend="MULTIPLY")
        shaded = _mix(nt, amount, shaded, dirty)
    nt.links.new(shaded, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75


def shader_tile_beige(mat):
    """한국 학교 외벽 작은 직사각 타일 — 가로줄눈 뚜렷, 타일마다 명도 살짝."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    row = _math(nt, "MULTIPLY", v, 10.0)
    col = _math(nt, "MULTIPLY", u, 4.0)
    fy, fx = _math(nt, "FRACT", row), _math(nt, "FRACT", col)
    grout_y = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fy, 0.5)), 0.47)
    grout_x = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fx, 0.5)), 0.46)
    tile_area = _math(nt, "MULTIPLY", grout_y, grout_x)
    cell_id = _math(nt, "ADD", _math(nt, "FLOOR", col), _math(nt, "MULTIPLY", _math(nt, "FLOOR", row), 5.0))
    hue = _hash(nt, cell_id)
    grain = _noise(nt, uv, 30.0, 4.0, 0.6)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", hue, 0.3), _math(nt, "MULTIPLY", grain, 0.2), clamp=True)
    tile_col = _ramp(nt, tone, ((0.0, (0.72, 0.66, 0.52, 1)), (1.0, (0.85, 0.79, 0.64, 1))))
    color = _mix(nt, tile_area, (0.55, 0.53, 0.48, 1), tile_col)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.5


def shader_wood_panel(mat):
    """일본식 목조 벽·문 — 세로 결, 판 사이 좁은 홈."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"
    wave.bands_direction = "Y"
    wave.inputs["Scale"].default_value = 16.0
    wave.inputs["Distortion"].default_value = 2.0
    wave.inputs["Detail"].default_value = 2.0
    nt.links.new(uv, wave.inputs["Vector"])
    grain = _noise(nt, uv, 45.0, 6.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", wave.outputs["Fac"], 0.55), _math(nt, "MULTIPLY", grain, 0.45), clamp=True)
    seam = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", u, 6.0)), 0.5)), 0.46)
    color = _ramp(nt, tone, ((0.0, (0.10, 0.07, 0.05, 1)), (0.5, (0.24, 0.17, 0.11, 1)), (1.0, (0.38, 0.28, 0.18, 1))))
    color = _mix(nt, seam, (0.06, 0.04, 0.03, 1), color)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75


def shader_shutter(mat):
    """상가 1층 롤셔터 — 가로 골, 은회색."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    rib = _math(nt, "SINE", _math(nt, "MULTIPLY", v, 60.0))
    band = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", rib, 0.5))
    grain = _noise(nt, uv, 20.0, 4.0, 0.6)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", band, 0.7), _math(nt, "MULTIPLY", grain, 0.3), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.42, 0.43, 0.45, 1)), (0.5, (0.60, 0.61, 0.63, 1)), (1.0, (0.78, 0.79, 0.80, 1))))
    slat = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", v, 10.0)), 0.5)), 0.04)
    color = _mix(nt, slat, color, (0.28, 0.29, 0.30, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.4
    bsdf.inputs["Metallic"].default_value = 0.6


# ──────────────────────────────────────────────────────────── 창·유리(불투명 유리색 — 투명 아님)

def shader_window(mat):
    """알루미늄 창틀 + 2짝 유리(짙은 청회색, 위가 밝은 하늘 반사) + 가운데 세로 틀. FIT."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    frame_w = 0.07
    near_edge = _math(nt, "MINIMUM", _math(nt, "MINIMUM", u, _math(nt, "SUBTRACT", 1.0, u)), _math(nt, "MINIMUM", v, _math(nt, "SUBTRACT", 1.0, v)))
    is_frame = _math(nt, "LESS_THAN", near_edge, frame_w)
    mid_gap = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", u, 0.5)), frame_w * 0.55)
    is_frame = _math(nt, "MAXIMUM", is_frame, mid_gap)
    grain = _noise(nt, uv, 12.0, 3.0, 0.6)
    tone = _math(nt, "ADD", v, _math(nt, "MULTIPLY", grain, 0.08))
    glass = _ramp(nt, tone, ((0.0, (0.10, 0.14, 0.20, 1)), (1.0, (0.55, 0.70, 0.82, 1))))
    color = _mix(nt, is_frame, glass, (0.55, 0.56, 0.58, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.15
    bsdf.inputs["Metallic"].default_value = 0.2


def shader_curtainwall(mat):
    """유리 외벽 — 멀리언 격자 한 타일에 2×2칸, 청록 반사 칸마다 명도 다르게."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    lines = _grid_lines(nt, u, v, 2.0, 2.0, 0.02)
    cell_id = _math(nt, "ADD", _math(nt, "FLOOR", _math(nt, "MULTIPLY", u, 2.0)), _math(nt, "MULTIPLY", _math(nt, "FLOOR", _math(nt, "MULTIPLY", v, 2.0)), 5.0))
    hue = _hash(nt, cell_id)
    grad = _ramp(nt, v, ((0.0, (0.08, 0.20, 0.22, 1)), (1.0, (0.30, 0.55, 0.58, 1))))
    tint = _math(nt, "ADD", 0.85, _math(nt, "MULTIPLY", hue, 0.3))
    glass = _mix(nt, 1.0, grad, tint, blend="MULTIPLY")
    color = _mix(nt, lines, glass, (0.15, 0.16, 0.17, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.2
    bsdf.inputs["Metallic"].default_value = 0.3


def shader_studio_window(mat):
    """밤 원룸창 — 어두운 안쪽 + 푸른 모니터 불빛 번짐 + 아래 이불 덩어리 실루엣 + 창틀. FIT."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    frame_w = 0.05
    near_edge = _math(nt, "MINIMUM", _math(nt, "MINIMUM", u, _math(nt, "SUBTRACT", 1.0, u)), _math(nt, "MINIMUM", v, _math(nt, "SUBTRACT", 1.0, v)))
    is_frame = _math(nt, "LESS_THAN", near_edge, frame_w)
    dark = (0.03, 0.035, 0.05, 1)
    dx = _math(nt, "SUBTRACT", u, 0.35)
    dy = _math(nt, "SUBTRACT", v, 0.62)
    d2 = _math(nt, "ADD", _math(nt, "MULTIPLY", dx, dx), _math(nt, "MULTIPLY", dy, dy))
    glow = _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", d2, 8.0), clamp=True)
    base = _mix(nt, glow, dark, (0.20, 0.42, 0.85, 1))
    bx = _math(nt, "SUBTRACT", u, 0.55)
    by = _math(nt, "SUBTRACT", v, 0.12)
    bd2 = _math(nt, "ADD", _math(nt, "MULTIPLY", bx, bx), _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", by, by), 2.5))
    blanket = _math(nt, "LESS_THAN", bd2, 0.10)
    base = _mix(nt, blanket, base, (0.10, 0.08, 0.09, 1))
    color = _mix(nt, is_frame, base, (0.30, 0.30, 0.32, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.3


# ──────────────────────────────────────────────────────────── 지붕·옥상

def shader_roof_tile(mat):
    """일본 기와 — 짙은 회청, 세로 골 줄."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    groove = _math(nt, "SINE", _math(nt, "MULTIPLY", u, 40.0))
    tone = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", groove, 0.22))
    grain = _noise(nt, uv, 15.0, 4.0, 0.6)
    tone = _math(nt, "ADD", tone, _math(nt, "MULTIPLY", grain, 0.18), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.12, 0.15, 0.19, 1)), (0.5, (0.22, 0.26, 0.31, 1)), (1.0, (0.34, 0.38, 0.42, 1))))
    row_seam = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", v, 6.0)), 0.5)), 0.05)
    color = _mix(nt, row_seam, color, (0.08, 0.10, 0.13, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.55


def shader_shingle(mat, color):
    """아스팔트 슁글 — 비늘처럼 어긋난 줄, 줄마다 겹침 그림자, 조각마다 색 살짝."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    rows = 8.0
    row = _math(nt, "MULTIPLY", v, rows)
    shift = _math(nt, "MULTIPLY", _math(nt, "MODULO", _math(nt, "FLOOR", row), 2.0), 0.5)
    col = _math(nt, "ADD", _math(nt, "MULTIPLY", u, 6.0), shift)
    fy = _math(nt, "FRACT", row)
    butt = _math(nt, "LESS_THAN", fy, 0.10)
    cell_id = _math(nt, "ADD", _math(nt, "FLOOR", col), _math(nt, "MULTIPLY", _math(nt, "FLOOR", row), 11.0))
    hue = _hash(nt, cell_id)
    grain = _noise(nt, uv, 30.0, 5.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", hue, 0.35), _math(nt, "MULTIPLY", grain, 0.35), clamp=True)
    dark = tuple(c * 0.55 for c in color[:3]) + (1,)
    light = tuple(min(c * 1.25, 1.0) for c in color[:3]) + (1,)
    base = _ramp(nt, tone, ((0.0, dark), (1.0, light)))
    seam_col = tuple(c * 0.4 for c in color[:3]) + (1,)
    color_out = _mix(nt, butt, base, seam_col)
    nt.links.new(color_out, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85


def shader_rooftop_waterproof(mat):
    """한국 옥상 초록 우레탄 방수 바닥 — 얼룩·보수 자국·잔금."""
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, 25.0, 5.0, 0.65)
    patch = _voronoi(nt, uv, 3.0, "F1", 1.0)
    patch_hash = _hash(nt, patch.outputs["Distance"])
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", grain, 0.5), _math(nt, "MULTIPLY", patch_hash, 0.2), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.10, 0.30, 0.20, 1)), (0.5, (0.16, 0.42, 0.27, 1)), (1.0, (0.24, 0.52, 0.34, 1))))
    stain = _math(nt, "GREATER_THAN", patch_hash, 0.82)
    color = _mix(nt, stain, base, (0.30, 0.30, 0.26, 1))
    crack = _math(nt, "LESS_THAN", patch.outputs["Distance"], 0.03)
    color = _mix(nt, crack, color, (0.08, 0.18, 0.13, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.6


# ──────────────────────────────────────────────────────────── 금속

def shader_corrugated(mat):
    """반원형 탄약고 골함석 — 국방색 도장 + 세로 골 + 아래로 갈수록 녹물."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    ridge = _math(nt, "SINE", _math(nt, "MULTIPLY", u, 50.0))
    tone = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", ridge, 0.3))
    grain = _noise(nt, uv, 25.0, 4.0, 0.6)
    tone = _math(nt, "ADD", tone, _math(nt, "MULTIPLY", grain, 0.15), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.20, 0.22, 0.12, 1)), (0.5, (0.32, 0.34, 0.20, 1)), (1.0, (0.44, 0.46, 0.28, 1))))
    rust_noise = _noise(nt, uv, 6.0, 3.0, 0.7)
    rust = _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", rust_noise, 0.58), _math(nt, "POWER", _math(nt, "SUBTRACT", 1.0, v), 1.4))
    color = _mix(nt, rust, base, (0.32, 0.16, 0.08, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.7
    bsdf.inputs["Metallic"].default_value = 0.3


def shader_metal_gray(mat):
    """도장 강철 — 난간·기둥·문. 대각선 스크래치가 옅게."""
    nt, bsdf, uv = _tree(mat)
    scratch = nt.nodes.new("ShaderNodeTexWave")
    scratch.wave_type = "BANDS"
    scratch.bands_direction = "DIAGONAL"
    scratch.inputs["Scale"].default_value = 40.0
    scratch.inputs["Distortion"].default_value = 6.0
    nt.links.new(uv, scratch.inputs["Vector"])
    grain = _noise(nt, uv, 12.0, 4.0, 0.6)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", grain, 0.7), _math(nt, "MULTIPLY", scratch.outputs["Fac"], 0.3), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.24, 0.25, 0.27, 1)), (0.5, (0.38, 0.39, 0.41, 1)), (1.0, (0.52, 0.53, 0.55, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.45
    bsdf.inputs["Metallic"].default_value = 0.6


def shader_stainless(mat):
    """물탱크·게양대 헤어라인 — 한쪽으로 길게 뻗은 잔결(너무 촘촘하면 굽기 해상도에서
    뭉개져 민무늬 회색이 된다 — v 방향을 적당히만 늘인다)."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    vec = _combine(nt, _math(nt, "MULTIPLY", u, 2.0), _math(nt, "MULTIPLY", v, 22.0))
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = 3.0
    n.inputs["Detail"].default_value = 2.0
    n.inputs["Roughness"].default_value = 0.5
    nt.links.new(vec, n.inputs["Vector"])
    # 🔴 2026-09-12 정정(blender 세션 견본 렌더 검수) — 유니티 쪽은 금속성 없이 색만 쓰므로
    # 어두운 회색이면 그대로 검은 물탱크로 보인다. 밝은 은회색(0.68~0.78)으로 올렸다.
    tone = _math(nt, "ADD", 0.68, _math(nt, "MULTIPLY", n.outputs["Fac"], 0.20), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.66, 0.67, 0.68, 1)), (1.0, (0.80, 0.81, 0.82, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.3
    bsdf.inputs["Metallic"].default_value = 0.85


def shader_wire_concertina(mat):
    """원형 윤형 철조망 코일 — 겹친 고리, 배경 투명(알파)."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    period = 4.0
    fu = _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", u, period)), 0.5)
    fv = _math(nt, "SUBTRACT", v, 0.5)
    d2 = _math(nt, "ADD", _math(nt, "MULTIPLY", fu, fu), _math(nt, "MULTIPLY", fv, fv))
    ring = _math(nt, "MULTIPLY", _math(nt, "LESS_THAN", d2, 0.42 * 0.42), _math(nt, "GREATER_THAN", d2, 0.30 * 0.30))
    grain = _noise(nt, uv, 30.0, 3.0, 0.6)
    tone = _math(nt, "ADD", 0.55, _math(nt, "MULTIPLY", grain, 0.3), clamp=True)
    metal_col = _ramp(nt, tone, ((0.0, (0.30, 0.29, 0.26, 1)), (1.0, (0.58, 0.56, 0.50, 1))))
    nt.links.new(metal_col, bsdf.inputs["Base Color"])
    nt.links.new(ring, bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.5
    bsdf.inputs["Metallic"].default_value = 0.6
    mat.use_backface_culling = False


def shader_wire_mesh(mat):
    """마름모 철망(학교 담장·탄약창) — 두 대각선 줄무늬를 겹쳐 마름모 선을 낸다. 배경 투명."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    scale = 16.0
    a = _math(nt, "SINE", _math(nt, "MULTIPLY", _math(nt, "ADD", u, v), math.pi * scale))
    b = _math(nt, "SINE", _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", u, v), math.pi * scale))
    line_a = _math(nt, "GREATER_THAN", _math(nt, "ABSOLUTE", a), 0.90)
    line_b = _math(nt, "GREATER_THAN", _math(nt, "ABSOLUTE", b), 0.90)
    line = _math(nt, "MAXIMUM", line_a, line_b)
    bsdf.inputs["Base Color"].default_value = (0.55, 0.55, 0.52, 1.0)
    nt.links.new(line, bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.4
    bsdf.inputs["Metallic"].default_value = 0.5
    mat.use_backface_culling = False


# ──────────────────────────────────────────────────────────── 칠·간판

def shader_solid(mat, color, grain_strength=0.03, grain_scale=30.0):
    """거의 단색 + 아주 옅은 결 — 간판 판·글자, 네온(발광 없음, 유니티는 색만 씀)."""
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, grain_scale, 3.0, 0.5)
    shade = _math(nt, "ADD", 1.0 - grain_strength, _math(nt, "MULTIPLY", grain, grain_strength * 2))
    color_out = _mix(nt, 1.0, color, shade, blend="MULTIPLY")
    nt.links.new(color_out, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.5


def shader_paint_vermilion(mat):
    """도리이 주칠 — 살짝 벗겨져 밑바탕(회갈색 나무)이 비치는 자국."""
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, 35.0, 4.0, 0.6)
    tone = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", grain, 0.5), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.42, 0.11, 0.06, 1)), (1.0, (0.66, 0.22, 0.11, 1))))
    peel = _voronoi(nt, uv, 8.0, "F1", 1.0)
    peel_mask = _math(nt, "LESS_THAN", peel.outputs["Distance"], 0.05)
    color = _mix(nt, peel_mask, base, (0.42, 0.36, 0.28, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.6


# ──────────────────────────────────────────────────────────── 바닥·특수

def shader_sidewalk(mat):
    """회색·붉은 보도블록 — 벽돌처럼 어긋난 줄(엇갈림), 드문드문 붉은 블록."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    rows = 8.0
    row = _math(nt, "MULTIPLY", v, rows)
    shift = _math(nt, "MULTIPLY", _math(nt, "MODULO", _math(nt, "FLOOR", row), 2.0), 0.5)
    col = _math(nt, "ADD", _math(nt, "MULTIPLY", u, 8.0), shift)
    fy, fx = _math(nt, "FRACT", row), _math(nt, "FRACT", col)
    seam_y = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fy, 0.5)), 0.45)
    seam_x = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fx, 0.5)), 0.45)
    block = _math(nt, "MULTIPLY", seam_y, seam_x)
    cell_id = _math(nt, "ADD", _math(nt, "FLOOR", col), _math(nt, "MULTIPLY", _math(nt, "FLOOR", row), 13.0))
    hue = _hash(nt, cell_id)
    is_red = _math(nt, "LESS_THAN", hue, 0.12)
    grain = _noise(nt, uv, 25.0, 4.0, 0.6)
    gray_tone = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", grain, 0.25), clamp=True)
    gray_col = _ramp(nt, gray_tone, ((0.0, (0.45, 0.45, 0.43, 1)), (1.0, (0.65, 0.65, 0.62, 1))))
    block_col = _mix(nt, is_red, gray_col, (0.55, 0.22, 0.18, 1))
    color = _mix(nt, block, (0.30, 0.29, 0.27, 1), block_col)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8


def shader_playground(mat):
    """운동장 마사토 흙 — 따뜻한 갈색, 잔돌 알갱이."""
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, 40.0, 5.0, 0.7)
    speck = _voronoi(nt, uv, 60.0, "F1", 1.0)
    speck_mask = _math(nt, "LESS_THAN", speck.outputs["Distance"], 0.08)
    tone = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", grain, 0.35), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.42, 0.34, 0.22, 1)), (1.0, (0.62, 0.52, 0.36, 1))))
    color = _mix(nt, _math(nt, "MULTIPLY", speck_mask, 0.5), base, (0.30, 0.26, 0.18, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95


def shader_clock(mat):
    """흰 판 + 12눈금 + 시·분침(10시 10분). FIT."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    cx, cy = _math(nt, "SUBTRACT", u, 0.5), _math(nt, "SUBTRACT", v, 0.5)
    r2 = _math(nt, "ADD", _math(nt, "MULTIPLY", cx, cx), _math(nt, "MULTIPLY", cy, cy))
    rim = _math(nt, "MULTIPLY", _math(nt, "LESS_THAN", r2, 0.46 * 0.46), _math(nt, "GREATER_THAN", r2, 0.43 * 0.43))
    ang = _math(nt, "ARCTAN2", cy, cx)
    tick_ang = _math(nt, "MODULO", ang, math.pi / 6.0)
    tick_ang2 = _math(nt, "MINIMUM", tick_ang, _math(nt, "SUBTRACT", math.pi / 6.0, tick_ang))
    ticks = _math(nt, "MULTIPLY", _math(nt, "LESS_THAN", tick_ang2, 0.05), _math(nt, "GREATER_THAN", r2, 0.35 * 0.35))
    ticks = _math(nt, "MULTIPLY", ticks, _math(nt, "LESS_THAN", r2, 0.46 * 0.46))

    def hand(angle_deg, length, width):
        a = math.radians(90 - angle_deg)
        ca, sa = math.cos(a), math.sin(a)
        lx = _math(nt, "ADD", _math(nt, "MULTIPLY", cx, ca), _math(nt, "MULTIPLY", cy, sa))
        ly = _math(nt, "SUBTRACT", _math(nt, "MULTIPLY", cy, ca), _math(nt, "MULTIPLY", cx, sa))
        inlen = _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", lx, -0.03), _math(nt, "LESS_THAN", lx, length))
        inwid = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", ly), width)
        return _math(nt, "MULTIPLY", inlen, inwid)

    hour_hand = hand((10 + 10 / 60) * 30.0, 0.22, 0.022)
    minute_hand = hand(10 * 6.0, 0.34, 0.016)
    mark = _math(nt, "MAXIMUM", rim, _math(nt, "MAXIMUM", ticks, _math(nt, "MAXIMUM", hour_hand, minute_hand)))
    color = _mix(nt, mark, (0.95, 0.95, 0.92, 1), (0.08, 0.08, 0.09, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.4


def shader_flag(mat):
    """태극기(비율 3:2) — 태극 원(정확한 S자 경계) + 네 모서리 괘(건·곤·감·리 근사). FIT."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    # 실제 깃발 비율(3:2)로 보정한 좌표 — 안 그러면 태극 원이 타원으로 보인다.
    X = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", u, 0.5), 3.0)
    Y = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", v, 0.5), 2.0)
    R = 0.75   # 관례: 태극 원 지름 = 깃발 폭의 1/2(폭 3이므로 지름 1.5, 반지름 0.75)
    r2 = _math(nt, "ADD", _math(nt, "MULTIPLY", X, X), _math(nt, "MULTIPLY", Y, Y))
    inside = _math(nt, "LESS_THAN", r2, R * R)
    up_y = _math(nt, "SUBTRACT", Y, R / 2)
    down_y = _math(nt, "ADD", Y, R / 2)
    up_d2 = _math(nt, "ADD", _math(nt, "MULTIPLY", X, X), _math(nt, "MULTIPLY", up_y, up_y))
    down_d2 = _math(nt, "ADD", _math(nt, "MULTIPLY", X, X), _math(nt, "MULTIPLY", down_y, down_y))
    in_up = _math(nt, "LESS_THAN", up_d2, (R / 2) * (R / 2))
    in_down = _math(nt, "LESS_THAN", down_d2, (R / 2) * (R / 2))
    y_pos = _math(nt, "GREATER_THAN", Y, 0.0)
    is_red = _math(nt, "MAXIMUM", _math(nt, "MULTIPLY", y_pos, _math(nt, "SUBTRACT", 1.0, in_down)), in_up)
    taegeuk = _mix(nt, is_red, (0.10, 0.20, 0.55, 1), (0.75, 0.10, 0.12, 1))
    color = _mix(nt, inside, (0.95, 0.95, 0.93, 1), taegeuk)

    def bar(cxp, cyp, w, h, gap, broken):
        hx = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", X, cxp)), w / 2)
        hy = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", Y, cyp)), h / 2)
        base = _math(nt, "MULTIPLY", hx, hy)
        if broken:
            gapmask = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", X, cxp)), gap / 2)
            base = _math(nt, "MULTIPLY", base, _math(nt, "SUBTRACT", 1.0, gapmask))
        return base

    def trigram(cx0, cy0, bars, w=0.5, h=0.075, gap_y=0.15, gapw=0.16):
        offsets = (gap_y, 0.0, -gap_y)
        m = None
        for off, broken in zip(offsets, bars):
            b = bar(cx0, cy0 + off, w, h, gapw, broken)
            m = b if m is None else _math(nt, "MAXIMUM", m, b)
        return m

    # 원(반지름 0.75)과 안 겹치게 대각선 모서리 쪽에(관례적 위치 근사).
    corner_x, corner_y = 1.15, 0.72
    trig_mask = _math(
        nt, "MAXIMUM",
        _math(nt, "MAXIMUM", trigram(-corner_x, corner_y, (True, True, True)),        # 건(좌상)
              trigram(corner_x, corner_y, (True, False, True))),                      # 리(우상)
        _math(nt, "MAXIMUM", trigram(-corner_x, -corner_y, (False, True, False)),     # 감(좌하)
              trigram(corner_x, -corner_y, (False, False, False))))                   # 곤(우하)
    trig_mask = _math(nt, "MULTIPLY", trig_mask, _math(nt, "SUBTRACT", 1.0, inside))
    color = _mix(nt, trig_mask, color, (0.05, 0.05, 0.07, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.7


SHADERS = {
    "건물_벽돌_붉은": shader_brick_red,
    "건물_콘크리트": shader_concrete,
    # 🔴 2026-09-12 정정(PM) — 밝기 0.88→0.78(직사광에서 1.0에 닿아 뭉개짐). 얼룩 비율은 그대로.
    "건물_외벽_흰": lambda m: shader_flat_paint(m, (0.78, 0.77, 0.73, 1), streak=True),
    "건물_타일_베이지": shader_tile_beige,
    # 🔴 2026-09-12 정정(blender 세션 견본 렌더 검수) — 처음 채도가 "파스텔 사탕"으로 보여
    # 사실감이 떨어졌다(사장님이 돌담 막돌 때도 같은 이유로 싫어하심 — check_assignment류
    # 교훈 재사용). 채도를 낮춘 바랜 도장 색으로, 흰 외벽처럼 옅은 흙때(streak_strength 0.5)를 얹었다.
    "건물_외벽_노랑": lambda m: shader_flat_paint(m, (0.90, 0.82, 0.56, 1), streak=True, streak_strength=0.5),
    "건물_외벽_분홍": lambda m: shader_flat_paint(m, (0.88, 0.72, 0.70, 1), streak=True, streak_strength=0.5),
    "건물_외벽_하늘": lambda m: shader_flat_paint(m, (0.68, 0.79, 0.86, 1), streak=True, streak_strength=0.5),
    "건물_외벽_연두": lambda m: shader_flat_paint(m, (0.76, 0.83, 0.62, 1), streak=True, streak_strength=0.5),
    "건물_나무_판": shader_wood_panel,
    "건물_셔터": shader_shutter,
    "건물_유리창": shader_window,
    "건물_유리_커튼월": shader_curtainwall,
    "건물_원룸창": shader_studio_window,
    "건물_지붕_기와": shader_roof_tile,
    "건물_지붕_슁글_빨강": lambda m: shader_shingle(m, (0.55, 0.18, 0.15)),
    "건물_지붕_슁글_파랑": lambda m: shader_shingle(m, (0.18, 0.28, 0.48)),
    "건물_지붕_슁글_초록": lambda m: shader_shingle(m, (0.20, 0.40, 0.22)),
    "건물_지붕_슁글_주황": lambda m: shader_shingle(m, (0.68, 0.38, 0.14)),
    "건물_옥상_방수": shader_rooftop_waterproof,
    "건물_금속_골함석": shader_corrugated,
    "건물_금속_회색": shader_metal_gray,
    "건물_금속_스테인리스": shader_stainless,
    "건물_철조망_잎카드": shader_wire_concertina,
    "건물_철망_잎카드": shader_wire_mesh,
    "건물_색_흰": lambda m: shader_solid(m, (0.94, 0.94, 0.92, 1)),
    "건물_색_검정": lambda m: shader_solid(m, (0.06, 0.06, 0.07, 1)),
    "건물_색_남색": lambda m: shader_solid(m, (0.10, 0.14, 0.32, 1)),
    "건물_색_빨강": lambda m: shader_solid(m, (0.62, 0.10, 0.10, 1)),
    "건물_색_노랑": lambda m: shader_solid(m, (0.85, 0.70, 0.10, 1)),
    "건물_색_초록": lambda m: shader_solid(m, (0.12, 0.42, 0.18, 1)),
    "건물_네온_분홍": lambda m: shader_solid(m, (0.95, 0.20, 0.55, 1), grain_strength=0.015),
    "건물_네온_하늘": lambda m: shader_solid(m, (0.25, 0.70, 0.95, 1), grain_strength=0.015),
    "건물_도장_주홍": shader_paint_vermilion,
    "건물_바닥_보도블록": shader_sidewalk,
    "건물_바닥_운동장": shader_playground,
    "건물_시계판": shader_clock,
    "건물_국기_태극기": shader_flag,
}


# ──────────────────────────────────────────────────────────── 굽기

def _to_image_material(mat, image, alpha):
    nt = mat.node_tree
    bsdf_old = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    rough, metal = bsdf_old.inputs["Roughness"].default_value, bsdf_old.inputs["Metallic"].default_value
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat.surface_render_method = "DITHERED"
        mat.use_backface_culling = False
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.nodes.active = tex


def bake_all(out_dir=None, only=None):
    """재질마다 절차적 셰이더를 UV 0~1 평면 한 장에 구워 PNG로 저장하고, 재질을 이미지 재질로 바꾼다.
    `_잎카드` 재질은 색(DIFFUSE)과 알파(EMIT로 마스크만) 둘을 따로 구워 RGBA로 합친다(gen_nature.py
    bake_material_c와 같은 방식) — 알파를 낀 채로 색을 구우면 투명 부분이 검게 눌어붙는다."""
    out_dir = out_dir or TEX_DIR
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    plane = bpy.context.active_object
    plane.name = "굽기판"
    prev = (scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed)
    scene.render.engine = "CYCLES"
    scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = 8, False, 0
    scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 2
    scene.render.bake.use_clear = False
    written = []

    def do_bake(nt, image, bake_type):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        nt.nodes.active = tex
        bpy.ops.object.select_all(action="DESELECT")
        plane.select_set(True)
        bpy.context.view_layer.objects.active = plane
        bpy.ops.object.bake(type=bake_type)
        nt.nodes.remove(tex)

    for name in MATERIALS:
        if only and name not in only:
            continue
        px = MATERIALS[name][2]
        mat = material(name)
        SHADERS[name](mat)
        plane.data.materials.clear()
        plane.data.materials.append(mat)
        nt = mat.node_tree
        out_path = os.path.join(out_dir, f"{name}.png")

        # 🔴 2026-09-12 발견(blender 세션이 스테인리스 렌더에서 "거의 검정"으로 지적한 것을
        # 추적하다 찾음) — Cycles의 DIFFUSE 굽기는 물리적으로 옳게 금속성을 반영해, Metallic이
        # 높을수록 굽힌 색을 실제로 어둡게 죽인다(실측: 금_장식도 같은 증상 — gen_walls.py의
        # 기존 패턴에도 있던 문제, 이 파일은 안 건드리고 여기서만 고친다). 유니티는 이 텍스처를
        # 색으로만 쓰고 금속성을 따로 안 읽으므로(PM 확인), 굽는 동안만 0으로 낮췄다 되돌린다.
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        saved_metallic = bsdf.inputs["Metallic"].default_value
        bsdf.inputs["Metallic"].default_value = 0.0

        if name.endswith("_잎카드"):
            alpha_link = next(l for l in nt.links if l.to_socket == bsdf.inputs["Alpha"])
            mask_out = alpha_link.from_socket
            color_img = bpy.data.images.new(name + "_color", px, px)
            nt.links.remove(alpha_link)
            do_bake(nt, color_img, "DIFFUSE")
            emit = nt.nodes.new("ShaderNodeEmission")
            out_node = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
            surf_link = next(l for l in nt.links if l.to_socket == out_node.inputs["Surface"])
            nt.links.remove(surf_link)
            nt.links.new(mask_out, emit.inputs["Color"])
            nt.links.new(emit.outputs["Emission"], out_node.inputs["Surface"])
            mask_img = bpy.data.images.new(name + "_mask", px, px)
            do_bake(nt, mask_img, "EMIT")
            color = np.array(color_img.pixels[:], dtype=np.float32).reshape(-1, 4)
            mask = np.array(mask_img.pixels[:], dtype=np.float32).reshape(-1, 4)
            color[:, 3] = (mask[:, 0] > 0.5).astype(np.float32)
            img = bpy.data.images.new(name + "_baked", px, px, alpha=True)
            img.pixels = color.ravel().tolist()
            img.filepath_raw = out_path
            img.file_format = "PNG"
            img.alpha_mode = "STRAIGHT"
            img.save()
            bpy.data.images.remove(color_img)
            bpy.data.images.remove(mask_img)
            img.reload()
            bsdf.inputs["Metallic"].default_value = saved_metallic
            _to_image_material(mat, img, alpha=True)
        else:
            old = bpy.data.images.get(name)
            if old:
                bpy.data.images.remove(old)
            img = bpy.data.images.new(name, px, px)
            do_bake(nt, img, "DIFFUSE")
            img.filepath_raw = out_path
            img.file_format = "PNG"
            img.save()
            img.reload()
            bsdf.inputs["Metallic"].default_value = saved_metallic
            _to_image_material(mat, img, alpha=False)
        written.append(out_path)

    scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = prev
    mesh = plane.data
    bpy.data.objects.remove(plane, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    return written


def main():
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if prefixes:
        only = [n for n in MATERIALS if any(n.startswith(p) for p in prefixes)]
        if not only:
            raise SystemExit(f"이름이 맞는 재질이 없다: {prefixes}")
    written = bake_all(only=only)
    print("=" * 60)
    for path in written:
        print(f"구움  {os.path.basename(path)}")
    print(f"총 {len(written)}장 → {TEX_DIR}")


if __name__ == "__main__":
    main()
