"""물범 섬 표적 셋(물범 → 노루 → 양, 사실적) + 장식 물범바위 — 메시·구운 질감·뼈대·Idle_Breath → FBX.

화면 없이 돈다(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_creatures.py [-- 물범 노루 양 물범바위]
사장님 창에서는 이 파일을 exec해 rows(collection)만 부른다(show_all의 extra 훅 — 판_동물) — 창 모양 = FBX 모양.

사장님 지시(2026-09-12, PM 배정): 물범 섬(26×26) 4곳 한가운데 서 있는 움직이지 않는 표적. 잡으면 목재를 주고 같은 자리에서
물범 → 노루 → 양 순서로 다음 단계가 나온다(잡몹 임시 모형 대체).
🔴 치수는 미터로 적고 내보낼 때 11.4를 곱한다(사람 키 20 = 1.75m). 물범 몸길이 16~18 · 노루 어깨 10~12 · 양 어깨 9~11,
   바닥 20×20 안, 원점 = 발밑(배 밑) 가운데, 얼굴은 −Y, 삼각형 마리당 6,000 이하.
🔴 물범바위는 물범에 넣지 않는다(PM 결정) — 물범이 죽어 사라질 때 바위도 같이 사라지면 노루가 맨바닥에 선다.
   따로 섬 장식으로 남는다: 가로 14 안팎·높이 4 안팎·윗면이 살짝 평평, 삼각형 1,500 이하, 뼈대 없음.
🔴 재질 이름 = 텍스처 이름: Assets/Art/Creatures/Textures/<이름>.png. 수염처럼 가는 것만 `_잎카드`(양면 + 알파 컷).
   FBX path_mode="RELATIVE". 굽는 동안 금속성 0(DIFFUSE 굽기가 금속성만큼 색을 죽인다 — 여기 재질은 원래 0).
🔴 동작 `Idle_Breath` — 8초·30fps·240프레임 반복, 첫 = 끝(해왕류와 같은 방식, 사장님 「요동친다」 피드백으로 폭은 작게).
   FBX 클립 이름은 「<동물>_뼈대|Idle_Breath」. Generic 뼈대, 가중치는 부위마다 허락된 뼈 사이에서만 직접 준다.

몸은 Skin 모디파이어로 짓는다: 뼈대 모양 꺾은선(점마다 반지름)에 살을 입히고 Subdivision → 메시로 굳힌다
(Subdivision이 살을 15% 안팎 줄이므로 반지름은 그만큼 크게 적는다). 부위(몸·다리·머리·귀·뿔·눈·수염)를 따로 지어
한 메시에 합친다 — 가중치를 부위별로 제한하려고. 눈은 머리 겉면에 반쯤 박는다(튀어나오면 인형 눈알처럼 보였다 — 1차).
질감은 Object 좌표·법선으로 그린 절차적 셰이더(등은 짙고 배는 밝은 역그늘, 점박이·흰 엉덩이 등)를 UV에 굽는다.
⚠️ 셰이더 색은 **선형값**이다 — sRGB 눈대중(0.5)을 그대로 넣으면 한 톤 이상 허옇게 뜬다(1차 렌더). 대략 sRGB^2.2.
씨앗 대역: 동물 800번대.
"""

import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gen_walls import _math, _mix, _noise, _ramp, _voronoi   # 노드 도구(벡터 소켓을 받는다)

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Creatures")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")
FRAMES, FPS = 240, 30
UP = Vector((0.0, 0.0, 1.0))


# ──────────────────────────────────────────────────────────── 셰이더(Object 좌표·법선 → UV에 굽는다, 색은 선형)

def _tree(mat):
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    coord = nt.nodes.new("ShaderNodeTexCoord")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep_n = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Normal"], sep_n.inputs["Vector"])
    sep_o = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["Object"], sep_o.inputs["Vector"])
    return nt, bsdf, coord, sep_n.outputs["Z"], sep_o.outputs


def _scaled(nt, vec, scale):
    m = nt.nodes.new("ShaderNodeMapping")
    m.inputs["Scale"].default_value = scale
    nt.links.new(vec, m.inputs["Vector"])
    return m.outputs["Vector"]


def _band(nt, value, lo, hi):
    """value가 lo에서 0, hi에서 1(부드럽게, 넘치면 잘림)."""
    return _math(nt, "DIVIDE", _math(nt, "SUBTRACT", value, lo), hi - lo, clamp=True)


def _updown(nt, nz, streak, amount=0.3):
    """등(법선 위) 1 … 배(법선 아래) 0, 털 결 노이즈로 경계를 흔든다."""
    up = _math(nt, "ADD", _math(nt, "MULTIPLY", nz, 0.5), 0.5)
    return _math(nt, "ADD", up, _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", streak, 0.5), amount), clamp=True)


def shader_seal(mat):
    """점박이물범 — 등 짙은 회갈, 배 은회색, 작은 짙은 점이 온몸(배는 드물게), 젖은 윤기."""
    nt, bsdf, coord, nz, _ = _tree(mat)
    obj = coord.outputs["Object"]
    streak = _noise(nt, obj, 6.0, 4.0, 0.6)
    shade = _updown(nt, nz, streak, 0.35)
    base = _ramp(nt, shade, ((0.0, (0.46, 0.43, 0.37, 1)), (0.45, (0.20, 0.19, 0.16, 1)),
                             (0.75, (0.085, 0.08, 0.07, 1)), (1.0, (0.05, 0.047, 0.042, 1))))
    wobble = _noise(nt, obj, 14.0, 2.0, 0.5)
    vor = _voronoi(nt, obj, 26.0)
    size = _math(nt, "ADD", 0.16, _math(nt, "MULTIPLY", wobble, 0.22))
    spots = _math(nt, "LESS_THAN", vor.outputs["Distance"], size)
    spots = _math(nt, "MULTIPLY", spots, _math(nt, "ADD", 0.25, _math(nt, "MULTIPLY", shade, 0.75)))
    dark = _mix(nt, 1.0, base, (0.35, 0.35, 0.35, 1), "MULTIPLY")
    nt.links.new(_mix(nt, spots, base, dark), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.4


def shader_flat(color, roughness=0.3):
    def make(mat):
        nt, bsdf, coord, _, _ = _tree(mat)
        tone = _noise(nt, coord.outputs["Object"], 40.0, 2.0, 0.5)
        nt.links.new(_ramp(nt, tone, ((0.0, tuple(c * 0.8 for c in color[:3]) + (1,)), (1.0, color))), bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = roughness
    return make


def shader_whisker(mat):
    """수염 카드 — UV v 방향으로 가는 줄 5가닥(u = 뿌리 0 → 끝 1), 끝으로 갈수록 가늘게. 나머지는 투명."""
    nt, bsdf, coord, _, _ = _tree(mat)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["UV"], sep.inputs["Vector"])
    u, v = sep.outputs["X"], sep.outputs["Y"]
    width = _math(nt, "SUBTRACT", 0.09, _math(nt, "MULTIPLY", u, 0.06))
    line = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", v, 5.0)), 0.5)), width)
    mask = _math(nt, "MULTIPLY", line, _math(nt, "LESS_THAN", u, 0.97))
    nt.links.new(_ramp(nt, u, ((0.0, (0.25, 0.23, 0.19, 1)), (1.0, (0.70, 0.67, 0.60, 1)))), bsdf.inputs["Base Color"])
    nt.links.new(mask, bsdf.inputs["Alpha"])
    mat.surface_render_method = "DITHERED"


def shader_rock(mat):
    """물범바위 — 젖은 회색 해안 바위: 결 노이즈, 가는 금, 윗면에 옅은 지의류 반점, 밑동은 젖어 짙게."""
    nt, bsdf, coord, nz, o = _tree(mat)
    obj = coord.outputs["Object"]
    grain = _noise(nt, obj, 9.0, 6.0, 0.65)
    crack = _voronoi(nt, _scaled(nt, obj, (1.0, 1.3, 2.2)), 4.0, "DISTANCE_TO_EDGE")
    crack_line = _math(nt, "MULTIPLY", _math(nt, "LESS_THAN", crack.outputs["Distance"], 0.012),
                       _math(nt, "GREATER_THAN", _noise(nt, obj, 2.5, 2.0, 0.5), 0.45))
    tone = _math(nt, "SUBTRACT", grain, _math(nt, "MULTIPLY", crack_line, 0.25), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.07, 0.07, 0.065, 1)), (0.5, (0.16, 0.155, 0.145, 1)), (1.0, (0.30, 0.29, 0.27, 1))))
    lichen_spot = _voronoi(nt, obj, 30.0)
    lichen = _math(nt, "MULTIPLY", _math(nt, "LESS_THAN", lichen_spot.outputs["Distance"], 0.20),
                   _math(nt, "MULTIPLY", _band(nt, nz, 0.4, 0.9), _math(nt, "GREATER_THAN", _noise(nt, obj, 3.0, 2.0, 0.5), 0.52)))
    color = _mix(nt, lichen, base, (0.42, 0.40, 0.28, 1))
    wet = _math(nt, "SUBTRACT", 1.0, _band(nt, o["Z"], 0.02, 0.12))
    color = _mix(nt, _math(nt, "MULTIPLY", wet, 0.55), color, _mix(nt, 1.0, color, (0.45, 0.45, 0.45, 1), "MULTIPLY"))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75


def shader_deer(mat):
    """노루 — 등 적갈색, 배 황갈, 엉덩이 흰 반점(뒤), 목 앞 흰 반점, 주둥이 끝 짙게, 아래 다리 회갈·발굽 검게.
    좌표는 노루 메시의 Object 좌표(미터, 원점 발밑) — 부위 경계가 몸 치수에 묶여 있다."""
    nt, bsdf, coord, nz, o = _tree(mat)
    obj = coord.outputs["Object"]
    streak = _noise(nt, _scaled(nt, obj, (18.0, 18.0, 4.0)), 1.0, 5.0, 0.6)
    shade = _updown(nt, nz, streak, 0.25)
    color = _ramp(nt, shade, ((0.0, (0.55, 0.38, 0.20, 1)), (0.5, (0.30, 0.13, 0.045, 1)), (1.0, (0.19, 0.075, 0.025, 1))))
    legs = _math(nt, "SUBTRACT", 1.0, _band(nt, o["Z"], 0.16, 0.28))
    color = _mix(nt, legs, color, (0.16, 0.11, 0.075, 1))
    hoof = _math(nt, "LESS_THAN", o["Z"], 0.035)
    color = _mix(nt, hoof, color, (0.02, 0.018, 0.015, 1))
    rump = _math(nt, "MULTIPLY", _band(nt, o["Y"], 0.30, 0.36), _math(nt, "GREATER_THAN", o["Z"], 0.42))
    color = _mix(nt, rump, color, (0.80, 0.74, 0.60, 1))
    throat_d = nt.nodes.new("ShaderNodeVectorMath")
    throat_d.operation = "DISTANCE"
    nt.links.new(obj, throat_d.inputs[0])
    throat_d.inputs[1].default_value = (0.0, -0.37, 0.70)
    throat = _math(nt, "SUBTRACT", 1.0, _band(nt, throat_d.outputs["Value"], 0.035, 0.058))
    color = _mix(nt, _math(nt, "MULTIPLY", throat, _math(nt, "LESS_THAN", nz, 0.3)), color, (0.75, 0.68, 0.55, 1))
    muzzle = _math(nt, "SUBTRACT", 1.0, _band(nt, o["Y"], -0.67, -0.63))
    color = _mix(nt, muzzle, color, (0.02, 0.017, 0.015, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85


def shader_antler(mat):
    nt, bsdf, coord, _, o = _tree(mat)
    tone = _noise(nt, _scaled(nt, coord.outputs["Object"], (60.0, 60.0, 10.0)), 1.0, 4.0, 0.6)
    color = _ramp(nt, tone, ((0.0, (0.08, 0.055, 0.03, 1)), (1.0, (0.26, 0.19, 0.11, 1))))
    tips = _band(nt, o["Z"], 0.99, 1.05)
    nt.links.new(_mix(nt, tips, color, (0.62, 0.56, 0.45, 1)), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.6


def shader_wool(mat):
    """양털 — 크림색 곱슬 덩어리: 둥근 뭉치(보로노이 가운데 거리 F1)를 노이즈로 흐려 경계가 안 보이게, 아래로 갈수록
    흙때로 누렇게. (2차: DISTANCE_TO_EDGE 선은 금 간 타일처럼 읽혔다 — 선을 그리지 않고 뭉치 명암만 둔다.)"""
    nt, bsdf, coord, nz, _ = _tree(mat)
    obj = coord.outputs["Object"]
    warp = nt.nodes.new("ShaderNodeTexNoise")
    warp.inputs["Scale"].default_value = 12.0
    warp.inputs["Detail"].default_value = 3.0
    nt.links.new(obj, warp.inputs["Vector"])
    warped = nt.nodes.new("ShaderNodeVectorMath")
    warped.operation = "ADD"
    nt.links.new(obj, warped.inputs[0])
    scaled_warp = nt.nodes.new("ShaderNodeVectorMath")
    scaled_warp.operation = "SCALE"
    scaled_warp.inputs["Scale"].default_value = 0.04
    nt.links.new(warp.outputs["Color"], scaled_warp.inputs[0])
    nt.links.new(scaled_warp.outputs["Vector"], warped.inputs[1])
    curl = _voronoi(nt, warped.outputs["Vector"], 34.0, "SMOOTH_F1")
    fiber = _noise(nt, obj, 70.0, 6.0, 0.7)
    clump = _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", curl.outputs["Distance"], 1.5), clamp=True)
    clump = _math(nt, "ADD", _math(nt, "MULTIPLY", clump, 0.7), _math(nt, "MULTIPLY", fiber, 0.3), clamp=True)
    curl_tone = _ramp(nt, clump, ((0.0, (0.70, 0.67, 0.62, 1)), (0.55, (0.92, 0.90, 0.86, 1)), (1.0, (1.0, 1.0, 0.98, 1))))
    dirt = _noise(nt, obj, 4.0, 3.0, 0.5)
    shade = _updown(nt, nz, dirt, 0.4)
    base = _ramp(nt, shade, ((0.0, (0.30, 0.24, 0.15, 1)), (0.45, (0.52, 0.45, 0.33, 1)), (1.0, (0.70, 0.64, 0.50, 1))))
    nt.links.new(_mix(nt, 1.0, base, curl_tone, "MULTIPLY"), bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0


def shader_sheep_skin(mat):
    """양 얼굴·다리 — 짙은 갈흑색 짧은 털, 주둥이 끝은 회색, 발굽은 검게."""
    nt, bsdf, coord, _, o = _tree(mat)
    tone = _noise(nt, coord.outputs["Object"], 30.0, 4.0, 0.6)
    color = _ramp(nt, tone, ((0.0, (0.025, 0.02, 0.017, 1)), (1.0, (0.075, 0.058, 0.045, 1))))
    muzzle = _band(nt, _math(nt, "MULTIPLY", o["Y"], -1.0), 0.68, 0.73)
    color = _mix(nt, _math(nt, "MULTIPLY", muzzle, _math(nt, "GREATER_THAN", o["Z"], 0.5)), color, (0.14, 0.12, 0.11, 1))
    color = _mix(nt, _math(nt, "LESS_THAN", o["Z"], 0.035), color, (0.01, 0.01, 0.01, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8


# ──────────────────────────────────────────────────────────── 목록
#
# 부위: skin(꺾은선 점 (x, y, z, 반지름x, 반지름y) + 선) · eyes(가운데들·반지름) · whiskers(좌우 뿌리). 좌표는 미터,
# 동물마다 scale을 곱한다(몸 치수 맞춤). bones = 그 부위 정점이 가중치를 나눠 받을 수 있는 뼈.

def _mirror(points):
    return [(-x, y, z, rx, ry) for x, y, z, rx, ry in points]


def _chain(points):
    return [(i, i + 1) for i in range(len(points) - 1)]


SEAL_BODY = [
    (0.0, 0.58, 0.12, 0.09, 0.07),      # 0 꼬리 밑(뒷지느러미 뿌리)
    (0.0, 0.38, 0.17, 0.19, 0.16),      # 1 엉덩이
    (0.0, 0.10, 0.21, 0.26, 0.21),      # 2 배
    (0.0, -0.18, 0.23, 0.24, 0.21),     # 3 가슴
    (0.0, -0.40, 0.29, 0.16, 0.15),     # 4 목
    (0.0, -0.54, 0.38, 0.125, 0.115),   # 5 머리
    (0.0, -0.66, 0.37, 0.07, 0.065),    # 6 주둥이
    (0.0, -0.72, 0.36, 0.04, 0.035),    # 7 코끝
    (0.08, 0.76, 0.08, 0.07, 0.02),     # 8 뒷지느러미 L
    (0.12, 0.88, 0.06, 0.09, 0.012),    # 9
    (-0.08, 0.76, 0.08, 0.07, 0.02),    # 10 뒷지느러미 R
    (-0.12, 0.88, 0.06, 0.09, 0.012),   # 11
    (0.19, -0.22, 0.10, 0.05, 0.035),   # 12 앞지느러미 L
    (0.30, -0.30, 0.03, 0.075, 0.015),  # 13
    (-0.19, -0.22, 0.10, 0.05, 0.035),  # 14 앞지느러미 R
    (-0.30, -0.30, 0.03, 0.075, 0.015), # 15
]
SEAL_EDGES = _chain(SEAL_BODY[:8]) + [(0, 8), (8, 9), (0, 10), (10, 11), (3, 12), (12, 13), (3, 14), (14, 15)]

# 노루(3차, PM): 실제 노루 어깨 0.65~0.75m → 어깨 ≈ 0.72m(게임 8.2). 몸통을 앞뒤로 길게, 목은 앞으로 ~38° 기울여
# 머리가 몸통 앞으로 나온다(2차는 목이 막대처럼 수직이고 몸통이 다리에 비해 작았다). 좌표는 최종 미터(scale 1.0).
DEER_BODY = [
    (0.0, 0.46, 0.58, 0.025, 0.03), (0.0, 0.36, 0.60, 0.11, 0.13), (0.0, 0.24, 0.60, 0.12, 0.14),
    (0.0, 0.00, 0.57, 0.13, 0.155), (0.0, -0.22, 0.58, 0.12, 0.16), (0.0, -0.33, 0.64, 0.085, 0.105),
    (0.0, -0.42, 0.74, 0.06, 0.07), (0.0, -0.50, 0.86, 0.055, 0.063), (0.0, -0.58, 0.86, 0.05, 0.055),
    (0.0, -0.67, 0.81, 0.032, 0.036), (0.0, -0.715, 0.795, 0.022, 0.024),
]
DEER_FRONT_LEG = [(0.075, -0.20, 0.60, 0.055, 0.055), (0.075, -0.21, 0.44, 0.042, 0.042), (0.075, -0.19, 0.26, 0.022, 0.022),
                  (0.075, -0.21, 0.08, 0.017, 0.017), (0.075, -0.215, 0.02, 0.019, 0.019)]
DEER_BACK_LEG = [(0.075, 0.26, 0.60, 0.085, 0.085), (0.075, 0.18, 0.42, 0.06, 0.06), (0.075, 0.31, 0.26, 0.026, 0.026),
                 (0.075, 0.27, 0.08, 0.017, 0.017), (0.075, 0.27, 0.02, 0.019, 0.019)]
DEER_EAR = [(0.03, -0.50, 0.915, 0.016, 0.016), (0.08, -0.48, 0.99, 0.035, 0.01), (0.11, -0.47, 1.04, 0.016, 0.006)]
DEER_ANTLER = [(0.023, -0.53, 0.91, 0.011, 0.011), (0.03, -0.52, 0.99, 0.009, 0.009), (0.038, -0.535, 1.07, 0.005, 0.005),
               (0.038, -0.58, 1.03, 0.004, 0.004)]
DEER_ANTLER_EDGES = [(0, 1), (1, 2), (1, 3)]

# 양(3차, PM): 어깨 ≈ 0.80m(게임 9.1). 몸통이 땅에 가깝게 — 털 밑이 z≈0.27이라 드러난 다리는 어깨의 ~35%,
# 다리 굵기는 2차의 1.5배, 털이 무릎 위까지 덮는다(2차는 가는 다리 위에 털 뭉치가 떠 보였다). 좌표는 최종 미터.
SHEEP_WOOL = [(0.0, 0.50, 0.48, 0.06, 0.06), (0.0, 0.36, 0.50, 0.30, 0.27), (0.0, 0.06, 0.50, 0.34, 0.27),
              (0.0, -0.22, 0.53, 0.31, 0.29), (0.0, -0.38, 0.62, 0.16, 0.17)]
SHEEP_HEAD = [(0.0, -0.44, 0.66, 0.075, 0.075), (0.0, -0.54, 0.70, 0.072, 0.08), (0.0, -0.64, 0.66, 0.06, 0.064),
              (0.0, -0.72, 0.61, 0.04, 0.043), (0.0, -0.755, 0.595, 0.026, 0.026)]
SHEEP_EAR = [(0.06, -0.54, 0.74, 0.02, 0.02), (0.13, -0.53, 0.73, 0.045, 0.012), (0.19, -0.52, 0.70, 0.02, 0.008)]
SHEEP_FRONT_LEG = [(0.13, -0.22, 0.40, 0.08, 0.08), (0.13, -0.23, 0.24, 0.06, 0.06), (0.13, -0.22, 0.11, 0.045, 0.045),
                   (0.13, -0.23, 0.03, 0.045, 0.045)]
SHEEP_BACK_LEG = [(0.13, 0.30, 0.40, 0.09, 0.09), (0.13, 0.33, 0.24, 0.06, 0.06), (0.13, 0.31, 0.11, 0.045, 0.045),
                  (0.13, 0.31, 0.03, 0.045, 0.045)]


def _legs(front, back):
    """다리 넷 — (이름, 점들). 오른쪽은 x를 뒤집는다."""
    return [("Leg_FL", front), ("Leg_FR", _mirror(front)), ("Leg_BL", back), ("Leg_BR", _mirror(back))]


def _leg_bone(name, pts):
    top, bottom = pts[0], pts[-1]
    return (name, top[:3], (bottom[0], bottom[1], 0.0), "Root")


CREATURES = {
    "물범": dict(
        label="Seal", scale=0.92,                 # 1.0이면 몸길이 18.9(규격 16~18) — 지느러미 끝까지 17.4로
        materials=[("물범_가죽", 1024, False, (0.30, 0.29, 0.26, 1.0), shader_seal),
                   ("물범_눈코", 64, False, (0.02, 0.02, 0.02, 1.0), shader_flat((0.012, 0.011, 0.01, 1.0), 0.1)),
                   ("물범_수염_잎카드", 256, True, (0.70, 0.67, 0.60, 1.0), shader_whisker)],
        parts=[dict(kind="skin", mat="물범_가죽", points=SEAL_BODY, edges=SEAL_EDGES, subdiv=2,
                    bones=["Hips", "Belly", "Chest", "Neck", "Head", "RearFlipper", "Flipper_L", "Flipper_R"]),
               dict(kind="eyes", mat="물범_눈코", centers=[(0.078, -0.615, 0.405), (-0.078, -0.615, 0.405)], radius=0.02, bones=["Head"]),
               dict(kind="eyes", mat="물범_눈코", centers=[(0.0, -0.742, 0.365)], radius=0.014, bones=["Head"]),
               dict(kind="whiskers", mat="물범_수염_잎카드", roots=[(0.045, -0.70, 0.35)], length=0.14, bones=["Head"])],
        bones=[("Root", (0, 0, 0), (0, -0.2, 0), None),
               ("Hips", (0, 0.62, 0.12), (0, 0.30, 0.18), "Root"),
               ("Belly", (0, 0.30, 0.18), (0, -0.02, 0.21), "Hips"),
               ("Chest", (0, -0.02, 0.21), (0, -0.30, 0.25), "Belly"),
               ("Neck", (0, -0.30, 0.25), (0, -0.48, 0.34), "Chest"),
               ("Head", (0, -0.48, 0.34), (0, -0.76, 0.36), "Neck"),
               ("RearFlipper", (0, 0.62, 0.12), (0, 0.92, 0.06), "Hips"),
               ("Flipper_L", (0.12, -0.20, 0.14), (0.30, -0.30, 0.03), "Chest"),
               ("Flipper_R", (-0.12, -0.20, 0.14), (-0.30, -0.30, 0.03), "Chest")]),
    "노루": dict(
        label="Roe Deer", scale=1.0,               # 좌표가 최종 미터(3차) — 어깨 ≈ 8
        materials=[("노루_털", 1024, False, (0.30, 0.13, 0.05, 1.0), shader_deer),
                   ("노루_뿔", 256, False, (0.20, 0.15, 0.09, 1.0), shader_antler),
                   ("노루_눈코", 64, False, (0.02, 0.02, 0.02, 1.0), shader_flat((0.012, 0.011, 0.01, 1.0), 0.1))],
        parts=[dict(kind="skin", mat="노루_털", points=DEER_BODY, edges=_chain(DEER_BODY), subdiv=2,     # 1이면 몸통이 각져 보였다(2차)
                    bones=["Hips", "Spine", "Chest", "Neck", "Head", "Tail"])]
              + [dict(kind="skin", mat="노루_털", points=pts, edges=_chain(pts), bones=[name]) for name, pts in _legs(DEER_FRONT_LEG, DEER_BACK_LEG)]
              + [dict(kind="skin", mat="노루_털", points=DEER_EAR, edges=_chain(DEER_EAR), bones=["Ear_L"]),
                 dict(kind="skin", mat="노루_털", points=_mirror(DEER_EAR), edges=_chain(DEER_EAR), bones=["Ear_R"]),
                 dict(kind="skin", mat="노루_뿔", points=DEER_ANTLER, edges=DEER_ANTLER_EDGES, bones=["Head"], subdiv=0),
                 dict(kind="skin", mat="노루_뿔", points=_mirror(DEER_ANTLER), edges=DEER_ANTLER_EDGES, bones=["Head"], subdiv=0),
                 dict(kind="eyes", mat="노루_눈코", centers=[(0.040, -0.595, 0.885), (-0.040, -0.595, 0.885)], radius=0.011, bones=["Head"]),
                 dict(kind="eyes", mat="노루_눈코", centers=[(0.0, -0.722, 0.797)], radius=0.011, bones=["Head"])],
        bones=[("Root", (0, 0, 0), (0, -0.2, 0), None),
               ("Hips", (0, 0.40, 0.60), (0, 0.16, 0.59), "Root"),
               ("Spine", (0, 0.16, 0.59), (0, -0.08, 0.57), "Hips"),
               ("Chest", (0, -0.08, 0.57), (0, -0.30, 0.63), "Spine"),
               ("Neck", (0, -0.30, 0.65), (0, -0.50, 0.86), "Chest"),
               ("Head", (0, -0.50, 0.86), (0, -0.73, 0.79), "Neck"),
               ("Ear_L", (0.03, -0.50, 0.915), (0.11, -0.47, 1.04), "Head"),
               ("Ear_R", (-0.03, -0.50, 0.915), (-0.11, -0.47, 1.04), "Head"),
               ("Tail", (0, 0.40, 0.60), (0, 0.48, 0.56), "Hips")]
              + [_leg_bone(name, pts) for name, pts in _legs(DEER_FRONT_LEG, DEER_BACK_LEG)]),
    "양": dict(
        label="Sheep", scale=1.07,                 # 좌표 1.0이면 어깨 8.4(창 실측) — 1.07로 어깨 ≈ 9
        materials=[("양_털", 1024, False, (0.62, 0.56, 0.44, 1.0), shader_wool),
                   ("양_피부", 512, False, (0.05, 0.04, 0.03, 1.0), shader_sheep_skin),
                   ("양_눈", 64, False, (0.02, 0.015, 0.01, 1.0), shader_flat((0.03, 0.02, 0.008, 1.0), 0.1))],
        parts=[dict(kind="skin", mat="양_털", points=SHEEP_WOOL, edges=_chain(SHEEP_WOOL), subdiv=2,
                    bones=["Hips", "Spine", "Chest", "Neck", "Tail"], lumpy=(0.03, 16.0)),
               dict(kind="skin", mat="양_피부", points=SHEEP_HEAD, edges=_chain(SHEEP_HEAD), bones=["Neck", "Head"]),
               dict(kind="skin", mat="양_피부", points=SHEEP_EAR, edges=_chain(SHEEP_EAR), bones=["Ear_L"]),
               dict(kind="skin", mat="양_피부", points=_mirror(SHEEP_EAR), edges=_chain(SHEEP_EAR), bones=["Ear_R"])]
              + [dict(kind="skin", mat="양_피부", points=pts, edges=_chain(pts), bones=[name]) for name, pts in _legs(SHEEP_FRONT_LEG, SHEEP_BACK_LEG)]
              + [dict(kind="eyes", mat="양_눈", centers=[(0.062, -0.61, 0.705), (-0.062, -0.61, 0.705)], radius=0.013, bones=["Head"])],
        bones=[("Root", (0, 0, 0), (0, -0.2, 0), None),
               ("Hips", (0, 0.46, 0.50), (0, 0.16, 0.50), "Root"),
               ("Spine", (0, 0.16, 0.50), (0, -0.12, 0.52), "Hips"),
               ("Chest", (0, -0.12, 0.52), (0, -0.36, 0.58), "Spine"),
               ("Neck", (0, -0.36, 0.60), (0, -0.50, 0.70), "Chest"),
               ("Head", (0, -0.50, 0.70), (0, -0.755, 0.595), "Neck"),
               ("Ear_L", (0.06, -0.54, 0.74), (0.19, -0.52, 0.70), "Head"),
               ("Ear_R", (-0.06, -0.54, 0.74), (-0.19, -0.52, 0.70), "Head"),
               ("Tail", (0, 0.46, 0.50), (0, 0.56, 0.40), "Hips")]
              + [_leg_bone(name, pts) for name, pts in _legs(SHEEP_FRONT_LEG, SHEEP_BACK_LEG)]),
}
ORDER = ("물범", "노루", "양")
ROCK = dict(label="Seal_Rock", materials=[("물범바위_돌", 512, False, (0.16, 0.155, 0.145, 1.0), shader_rock)],
            size=(1.25, 0.95, 0.36), seed=801)


# ──────────────────────────────────────────────────────────── 재질·굽기

def material(name, spec):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    mat.diffuse_color = spec[3]
    mat.use_backface_culling = False
    return mat


def _bake(obj, mat, image, bake_type):
    scene = bpy.context.scene
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.nodes.active = tex
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    prev = (scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed)
    scene.render.engine = "CYCLES"
    scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = 4, False, 0
    scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 6
    scene.render.bake.use_clear = False       # 🔴 다른 슬롯의 구운 이미지를 지우지 않게
    bpy.ops.object.bake(type=bake_type)
    scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = prev
    nt.nodes.remove(tex)


def _image(name, size, alpha=False):
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    return bpy.data.images.new(name, size, size, alpha=alpha)


def bake_textures(obj, specs, out_dir=None):
    """재질마다 절차적 셰이더를 구워 PNG로 저장하고, 전부 구운 뒤 이미지 재질로 바꾼다(텍스처 이름 = 재질 이름)."""
    import numpy as np
    out_dir = out_dir or TEX_DIR
    os.makedirs(out_dir, exist_ok=True)
    finals = []
    for spec in specs:
        name, size, alpha = spec[0], spec[1], spec[2]
        mat = bpy.data.materials[name]
        spec[4](mat)
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        rough = bsdf.inputs["Roughness"].default_value
        bsdf.inputs["Metallic"].default_value = 0.0
        alpha_link = next((l for l in nt.links if l.to_socket == bsdf.inputs["Alpha"]), None)
        mask_out = alpha_link.from_socket if alpha_link else None
        if alpha_link:
            nt.links.remove(alpha_link)       # 색을 구울 땐 알파를 끊는다(투명 부분이 검게 안 구워지게)
        color_img = _image(name + "_color", size)
        _bake(obj, mat, color_img, "DIFFUSE")
        pixels = np.array(color_img.pixels[:], dtype=np.float32).reshape(-1, 4)
        if alpha:
            emit = nt.nodes.new("ShaderNodeEmission")
            out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
            nt.links.new(mask_out, emit.inputs["Color"])
            nt.links.remove(next(l for l in nt.links if l.to_socket == out.inputs["Surface"]))
            nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
            mask_img = _image(name + "_mask", size)
            _bake(obj, mat, mask_img, "EMIT")
            mask = np.array(mask_img.pixels[:], dtype=np.float32).reshape(-1, 4)
            pixels[:, 3] = (mask[:, 0] > 0.5).astype(np.float32)
            bpy.data.images.remove(mask_img)
        final = _image(name, size, alpha=alpha)
        final.pixels = pixels.ravel().tolist()
        final.filepath_raw = os.path.join(out_dir, f"{name}.png")
        final.file_format = "PNG"
        final.alpha_mode = "STRAIGHT"
        final.save()
        bpy.data.images.remove(color_img)
        finals.append((mat, final, alpha, rough))
    for mat, image, alpha, rough in finals:
        image.reload()                        # 디스크 PNG가 정본
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = rough
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if alpha:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            mat.surface_render_method = "DITHERED"
        nt.nodes.active = tex
    return [os.path.join(out_dir, f"{s[0]}.png") for s in specs]


# ──────────────────────────────────────────────────────────── 메시

def skin_mesh(points, edges, scale, subdiv=1):
    """꺾은선(점마다 반지름) → Skin(+Subdivision) → 굳힌 임시 메시. 첫 점이 뿌리."""
    me = bpy.data.meshes.new("_살")
    me.from_pydata([Vector(p[:3]) * scale for p in points], edges, [])
    ob = bpy.data.objects.new("_살", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("Skin", "SKIN")
    mod.branch_smoothing = 0.6
    mod.use_smooth_shade = True
    layer = me.skin_vertices[0].data
    for i, p in enumerate(points):
        layer[i].radius = (p[3] * scale, p[4] * scale)
        layer[i].use_root = i == 0
    if subdiv:
        sub = ob.modifiers.new("Subdivision", "SUBSURF")
        sub.levels = sub.render_levels = subdiv
    result = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    return result


def build(key, collection):
    """부위를 지어 한 메시로 합친다. 돌려주는 값: (오브젝트, [(정점 시작, 끝, 허락된 뼈)], 바닥 올림 dz)."""
    spec = CREATURES[key]
    s = spec["scale"]
    names = [m[0] for m in spec["materials"]]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    ranges = []
    for part in spec["parts"]:
        mi = names.index(part["mat"])
        v0, f0 = len(bm.verts), len(bm.faces)
        if part["kind"] == "skin":
            me = skin_mesh(part["points"], part["edges"], s, part.get("subdiv", 1))
            bm.from_mesh(me)
            bpy.data.meshes.remove(me)
            bm.verts.ensure_lookup_table()
            bm.faces.ensure_lookup_table()
            if part.get("lumpy"):
                amp, freq = part["lumpy"]
                bm.normal_update()
                for v in bm.verts[v0:]:
                    n = noise.noise(v.co * freq) + 0.5 * noise.noise(v.co * freq * 2.3 + Vector((3.1, 1.7, 0.4)))
                    v.co += v.normal * amp * s * (0.6 + n)
        elif part["kind"] == "eyes":
            for c in part["centers"]:
                bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=part["radius"] * s,
                                          matrix=Matrix.Translation(Vector(c) * s))
            bm.verts.ensure_lookup_table()
            bm.faces.ensure_lookup_table()
        elif part["kind"] == "whiskers":
            for root in part["roots"]:
                for side in (1, -1):
                    base = Vector((root[0] * side, root[1], root[2])) * s
                    for tilt in (0.02, -0.03):
                        length = part["length"] * s
                        p0 = base + Vector((0, 0, -0.012 * s))
                        p1 = base + Vector((0, 0, 0.012 * s))
                        tip = base + Vector((side * length, 0.03 * s, tilt * s))
                        p2 = tip + Vector((0, 0, 0.035 * s))
                        p3 = tip + Vector((0, 0, -0.035 * s))
                        f = bm.faces.new([bm.verts.new(p) for p in (p0, p1, p2, p3)])
                        for loop, coord in zip(f.loops, ((0, 0), (0, 1), (1, 1), (1, 0))):
                            loop[uv].uv = coord
            bm.verts.ensure_lookup_table()
            bm.faces.ensure_lookup_table()
        for f in bm.faces[f0:]:
            f.material_index = mi
            f.smooth = part["kind"] != "whiskers"
        ranges.append((v0, len(bm.verts), part["bones"]))
    dz = -min(v.co.z for v in bm.verts)            # 발(배) 밑을 원점 높이로
    for v in bm.verts:
        v.co.z += dz
    mesh = bpy.data.meshes.new(key)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(key, mesh)
    collection.objects.link(obj)
    for m in spec["materials"]:
        mesh.materials.append(material(m[0], m))
    unwrap(obj)
    return obj, ranges, dz


def unwrap(obj):
    """재질마다 따로 스마트 UV — 재질 하나가 텍스처 한 장을 통째로 쓴다. `_잎카드`(수염)는 손으로 준 UV 그대로."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    for i, mat in enumerate(obj.data.materials):
        if mat.name.endswith("_잎카드"):
            continue
        bpy.ops.mesh.select_all(action="DESELECT")
        obj.active_material_index = i
        bpy.ops.object.material_slot_select()
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")


def build_rock(collection):
    """물범바위 — 찌부러진 공(ico 3단)을 노이즈로 울퉁불퉁, 윗면은 살짝 눌러 평평, 밑은 z=0에서 자른다."""
    sx, sy, sz = ROCK["size"]
    rng = random.Random(ROCK["seed"])
    offset = Vector((rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
    for v in bm.verts:
        d = v.co.normalized()
        bump = 1.0 + 0.16 * noise.noise(d * 1.6 + offset) + 0.06 * noise.noise(d * 4.5 + offset)
        p = Vector((d.x * sx / 2, d.y * sy / 2, d.z * sz)) * bump
        flat_top = sz * 0.82
        if p.z > flat_top:
            p.z = flat_top + (p.z - flat_top) * 0.25
        p.z = max(p.z, 0.0)
        v.co = p
    for f in bm.faces:
        f.smooth = True
    mesh = bpy.data.meshes.new("물범바위")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("물범바위", mesh)
    collection.objects.link(obj)
    spec = ROCK["materials"][0]
    mesh.materials.append(material(spec[0], spec))
    unwrap(obj)
    return obj


# ──────────────────────────────────────────────────────────── 뼈대·가중치·동작

def rig(obj, key, ranges, dz):
    spec = CREATURES[key]
    s = spec["scale"]
    arm = bpy.data.objects.new(key + "_뼈대", bpy.data.armatures.new(key + "_뼈대"))
    for col in obj.users_collection:
        col.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in spec["bones"]:
        b = arm.data.edit_bones.new(name)
        lift = Vector((0, 0, 0)) if name == "Root" else Vector((0, 0, dz))
        b.head, b.tail = Vector(head) * s + lift, Vector(tail) * s + lift
        b.inherit_scale = "NONE"              # 🔴 가슴을 부풀려도 목·머리가 같이 커지지 않게
        if parent:
            b.parent = arm.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    weights(obj, arm, ranges)
    return arm


def weights(obj, arm, ranges, blend=0.6):
    """정점마다 자기 부위에 허락된 뼈 중 가장 가까운 둘(뼈를 선분으로 본 거리)에 거리 반비례로 나눈다.
    자동 가중치(bone heat)는 떨어진 조각(눈·다리)이 있으면 조용히 실패했다(해왕류)."""
    segs = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm.data.bones}
    groups = {name: obj.vertex_groups.new(name=name) for name in segs if name != "Root"}

    def dist(p, a, b):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared)) if ab.length_squared > 1e-12 else 0.0
        return (p - (a + ab * t)).length

    verts = obj.data.vertices
    for start, end, allowed in ranges:
        for i in range(start, end):
            p = verts[i].co
            near = sorted((dist(p, *segs[n]), n) for n in allowed)[:2]
            if len(near) == 1:
                groups[near[0][1]].add([i], 1.0, "REPLACE")
                continue
            (d0, n0), (d1, n1) = near
            w0 = max(0.0, min(1.0, 0.5 + (d1 - d0) / (blend * (d0 + d1) + 1e-9) * 0.5))
            groups[n0].add([i], w0, "REPLACE")
            if w0 < 1.0:
                groups[n1].add([i], 1.0 - w0, "REPLACE")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def _fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    return [fc for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]


def pulse(t, shift=0.0):
    """0 → 1 → 0, 한 주기(8초)에 한 번."""
    return (1 - math.cos(math.tau * (t - shift))) / 2


def bump(t, center, half):
    """center 둘레 ±half 안에서만 0 → 1 → 0(짧은 까딱). 밖은 정확히 0이라 첫 = 끝이 지켜진다."""
    x = (t - center) / half
    return (1 - x * x) ** 3 if abs(x) < 1 else 0.0


def swell(amount, shift=0.0):
    return lambda t: (1 + amount * pulse(t, shift), 1.0, 1 + amount * pulse(t, shift))


def turn(axis, degrees, shape):
    def f(t):
        v = [0.0, 0.0, 0.0]
        v[axis] = math.radians(degrees) * shape(t)
        return tuple(v)
    return f


# 동물마다 (뼈, 속성, 시간 t(0~1) → 값). 폭은 작게 — 해왕류에서 사장님이 「요동친다」고 하셔서 줄인 결을 따른다.
ANIMS = {
    # 뼈가 −y(얼굴 쪽)를 보므로 X축 음수 회전이 머리를 드는 방향(시험: +7°는 머리 끝이 7.6cm 내려갔다).
    "물범": [("Belly", "scale", swell(0.035)), ("Chest", "scale", swell(0.025, 0.05)),
             ("Neck", "rotation_euler", turn(0, -7.0, lambda t: bump(t, 0.60, 0.20))),       # 가끔 머리를 살짝 든다
             ("Head", "rotation_euler", turn(0, -4.0, lambda t: bump(t, 0.62, 0.18))),
             ("RearFlipper", "rotation_euler", turn(2, 4.0, lambda t: math.sin(math.tau * t))),
             ("Flipper_L", "rotation_euler", turn(2, 3.0, lambda t: pulse(t, 0.1))),
             ("Flipper_R", "rotation_euler", turn(2, -3.0, lambda t: pulse(t, 0.1)))],
    "노루": [("Spine", "scale", swell(0.015)), ("Chest", "scale", swell(0.015, 0.05)),
             ("Neck", "rotation_euler", turn(0, 1.5, pulse)),
             ("Ear_L", "rotation_euler", turn(0, 18.0, lambda t: bump(t, 0.28, 0.035) + 0.6 * bump(t, 0.36, 0.03))),
             ("Ear_R", "rotation_euler", turn(0, 18.0, lambda t: bump(t, 0.66, 0.035))),
             ("Tail", "rotation_euler", turn(0, 20.0, lambda t: bump(t, 0.47, 0.03) + 0.7 * bump(t, 0.54, 0.03)))],
    "양": [("Spine", "scale", swell(0.025)), ("Chest", "scale", swell(0.02, 0.05)),
           ("Neck", "rotation_euler", turn(2, 3.0, lambda t: math.sin(math.tau * t))),
           ("Head", "rotation_euler", turn(0, 2.0, lambda t: pulse(t, 0.25))),
           ("Ear_L", "rotation_euler", turn(0, 6.0, lambda t: pulse(t, 0.4))),
           ("Ear_R", "rotation_euler", turn(0, 6.0, lambda t: pulse(t, 0.45)))],
}


def animate(arm, key, step=2):
    """Idle_Breath — 채널 함수를 2프레임마다 찍는다(짧은 귀·꼬리 까딱이 뭉개지지 않게), 자동 클램프 베지어."""
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 1, FRAMES
    if arm.animation_data is None:
        arm.animation_data_create()
    action = bpy.data.actions.new("Idle_Breath")
    arm.animation_data.action = action
    bones = arm.pose.bones
    for b in bones:
        b.rotation_mode = "XYZ"
    frames = list(range(1, FRAMES, step)) + [FRAMES]
    for frame in frames:
        t = (frame - 1) / (FRAMES - 1)
        for bone, path, fn in ANIMS[key]:
            setattr(bones[bone], path, fn(t))
            bones[bone].keyframe_insert(path, frame=frame)
    for fc in _fcurves(action):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"
    return action


def check_loop(arm):
    scene = bpy.context.scene

    def pose_at(f):
        scene.frame_set(f)
        return [(b.name, tuple(round(v, 5) for v in b.matrix.to_translation()), tuple(round(v, 5) for v in b.matrix.to_euler())) for b in arm.pose.bones]
    return pose_at(1) == pose_at(FRAMES)


# ──────────────────────────────────────────────────────────── 조립·검사·내보내기

def assemble(key, collection, tex_dir=None):
    """메시 → 굽기 → 뼈대 → 동작. 창과 화면 없는 실행이 같은 길을 탄다."""
    obj, ranges, dz = build(key, collection)
    textures = bake_textures(obj, CREATURES[key]["materials"], tex_dir)
    arm = rig(obj, key, ranges, dz)
    animate(arm, key)
    return obj, arm, textures


def assemble_rock(collection, tex_dir=None):
    obj = build_rock(collection)
    textures = bake_textures(obj, ROCK["materials"], tex_dir)
    return obj, textures


def measure(obj):
    """게임 단위 치수(가로 x, 앞뒤 y, 높이 z)·삼각형·최저점."""
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    k = UNITS_PER_METER
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return (max(xs) - min(xs)) * k, (max(ys) - min(ys)) * k, (max(zs) - min(zs)) * k, tris, min(zs) * k


def export(objects, name, animated):
    os.makedirs(OUT_ROOT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_ROOT, f"{name}.fbx"), use_selection=True, global_scale=UNITS_PER_METER,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE",
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=animated, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="RELATIVE")


def rows(collection):
    """show_all의 extra 훅 — 판_동물 한 줄: 물범·노루·양(뼈대째, 숨쉬기 재생) + 물범바위."""
    import showcase
    groups = []
    for key in ORDER:
        obj, arm, _ = assemble(key, collection)
        g = showcase.group(CREATURES[key]["label"], [(obj, 0.0, 0.0)])
        g["parts"] = [(arm, 0.0, 0.0)]          # 자리는 뼈대를 옮긴다 — 메시는 자식이라 따라온다
        groups.append(g)
    rock, _ = assemble_rock(collection)
    groups.append(showcase.group(ROCK["label"], [(rock, 0.0, 0.0)]))
    return [("동물", groups)]


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.materials, bpy.data.images):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def main():
    picks = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(ORDER) + ["물범바위"]
    made = []
    for key in picks:
        clear_scene()
        col = bpy.context.scene.collection
        if key == "물범바위":
            obj, textures = assemble_rock(col)
            export([obj], key, animated=False)
            made.append((key, measure(obj), "뼈대 없음", len(textures)))
        else:
            obj, arm, textures = assemble(key, col)
            export([obj, arm], key, animated=True)
            made.append((key, measure(obj), f"뼈 {len(arm.data.bones)} · 루프 {check_loop(arm)}", len(textures)))
    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_creatures.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 사장님 창에서 짓고 확인한 뒤 화면 없이 내보냄\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20). 원점 발밑(배 밑) 가운데, 얼굴 −Y.\n"
            "물범 섬 표적(물범 → 노루 → 양 순서로 같은 자리). 물범바위는 따로 남는 섬 장식(뼈대 없음).\n"
            f"동작: Idle_Breath 8초·{FPS}fps·{FRAMES}프레임 반복(첫=끝), 클립 이름 「<동물>_뼈대|Idle_Breath」.\n"
            "재질: 텍스처 Textures/<재질이름>.png, `_잎카드`(물범 수염)만 양면+알파 컷.\n\n"
            "만들어진 것:\n"
            "  물범      점박이물범, 평평한 바닥에 엎드려 누움 · 지느러미발 · 수염 카드 · 가끔 머리를 살짝 든다\n"
            "  노루      서 있는 노루 · 짧은 뿔 · 흰 엉덩이 반점 · 귀·꼬리 까딱\n"
            "  양        곱슬 털 덩어리 몸 + 짙은 얼굴·다리 · 몸통 호흡 + 머리 살짝 흔들\n"
            "  물범바위  윗면이 살짝 평평한 해안 바위(가로 14 안팎·높이 4 안팎)\n")
    print("=" * 60)
    for key, (w, d, h, tris, low), note, tex in made:
        print(f"만듦  {key:6} 가로 {w:5.1f} 앞뒤 {d:5.1f} 높이 {h:5.1f}  삼각형 {tris:5d}  최저 {low:+.2f}  {note}  텍스처 {tex}")


if __name__ == "__main__":
    main()
