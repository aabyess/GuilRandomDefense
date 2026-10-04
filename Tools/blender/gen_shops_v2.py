"""레인 상점 7채 — 모양을 확 다르게 다시 짓기(사장님 2026-10-04: 「너무 똑같이 생겨서 못생김」).

옛 판(gen_shops_blender/impl1)은 같은 오두막 뼈대(frame)에 지붕 색만 달랐다. 이 판은 뼈대를 버리고 건물마다 **실루엣**이 다르다:
  도박소=줄무늬 큰 천막+주사위 · 유닛강화소=굴뚝 높은 대장간+모루 · 다른세계강화소=커다란 보랏빛 고리 관문 ·
  영원강화소=기둥 신전 · 공격타입강화소=성가퀴 무기고 성채 · 도움소=줄무늬 등대 · 항해일지=돛 단 배
규격은 옛 판과 같다(shops_common.assemble이 assert): 바닥 ±4.5(9×9)·높이 12~16·삼각형 2,500·재질 `상점_<가게>_<종류>`·원점 바닥 가운데·정면 −Y.
좌표는 게임 단위(1/11.4로 줄여 m). 셰이더·굽기·내보내기는 shops_common 그대로.

화면 없이(정본):  blender --background --factory-startup --python gen_shops_v2.py [-- 상점_도박소 ...]
결과: ~/GRD_motion_trial/상점_7채/<이름>.fbx, Textures/<재질>.png   (환경변수 SHOPS_OUT으로 바꿈)
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import shops_common  # noqa: E402
from shops_common import (Builder, _bricks, _link, _math, _mix, _node, _noise, _ramp,  # noqa: E402
                          register_shader, run)

OUT = os.environ.get("SHOPS_OUT") or os.path.expanduser("~/GRD_motion_trial/상점_7채")

# 지붕 색(옛 ROOF_TONES 위에 덮어씀 — 이번 판에서 쓰는 가게만)
shops_common.ROOF_TONES.update({
    "유닛강화소": ((0.03, 0.037, 0.045), (0.065, 0.075, 0.088), 0.4, False),
    "영원강화소": ((0.35, 0.22, 0.05), (0.62, 0.43, 0.1), 0.0, False),
    "공격타입강화소": ((0.14, 0.03, 0.02), (0.3, 0.07, 0.04), 0.1, False),
    "도움소": ((0.02, 0.05, 0.16), (0.045, 0.1, 0.3), 0.0, False),
    "항해일지": ((0.018, 0.028, 0.075), (0.04, 0.058, 0.13), 0.5, False),
})


# ──────────────────────────────────────────────────────────── 새 재질

@register_shader("대리석", big=True)
def _marble(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 6.0), (0.55, 0.53, 0.48, 1), (0.75, 0.73, 0.68, 1))
    vein = _ramp(nt, _noise(nt, c.co, 9.0, detail=8.0, rough=0.7), [(0.0, (0, 0, 0, 1)), (0.47, (0, 0, 0, 1)), (0.5, (1, 1, 1, 1)), (0.53, (0, 0, 0, 1))])
    return _mix(nt, vein, base, (0.3, 0.3, 0.3, 1)), None


@register_shader("금빛")
def _gold(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 14.0), (0.62, 0.38, 0.05, 1), (0.9, 0.62, 0.14, 1))
    return base, None


@register_shader("룬석", big=True)
def _runestone(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 8.0, detail=6.0), (0.05, 0.04, 0.09, 1), (0.15, 0.1, 0.24, 1))
    crack = _ramp(nt, _noise(nt, c.co, 22.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.6, (0, 0, 0, 1)), (0.68, (1, 1, 1, 1))])
    return _mix(nt, crack, base, (0.02, 0.015, 0.035, 1)), None


@register_shader("포탈_발광")
def _portal(nt, c, name):
    # 문 한가운데(물체 좌표 m)를 중심으로 도는 소용돌이 — 보라~자홍~흰
    shift = nt.nodes.new("ShaderNodeMapping")
    shift.inputs["Location"].default_value = (0.0, 0.0, -PORTAL_Z / 11.4)
    _link(nt, c.co, shift.inputs["Vector"])
    wave = _node(nt, "ShaderNodeTexWave", Scale=7.0, Distortion=5.0, Detail=2.0)
    wave.wave_type, wave.rings_direction = "RINGS", "Y"
    _link(nt, shift.outputs["Vector"], wave.inputs["Vector"])
    color = _ramp(nt, wave.outputs["Fac"], [(0.0, (0.25, 0.02, 0.5, 1)), (0.45, (0.55, 0.12, 0.95, 1)),
                                           (0.75, (0.95, 0.4, 1.0, 1)), (1.0, (1.0, 0.85, 1.0, 1))])
    return color, color


@register_shader("선체", big=True)
def _hull(nt, c, name):
    wave = _node(nt, "ShaderNodeTexWave", Scale=12.0, Distortion=0.4, Detail=1.0)
    wave.wave_type, wave.bands_direction = "BANDS", "Z"
    _link(nt, c.co, wave.inputs["Vector"])
    gap = _ramp(nt, wave.outputs["Fac"], [(0.0, (0, 0, 0, 1)), (0.1, (0, 0, 0, 1)), (0.2, (1, 1, 1, 1))])
    board = _mix(nt, _noise(nt, c.co, 28.0, detail=6.0), (0.07, 0.04, 0.02, 1), (0.16, 0.09, 0.045, 1))
    return _mix(nt, gap, (0.015, 0.01, 0.006, 1), board), None


@register_shader("갑판")
def _deck(nt, c, name):
    wave = _node(nt, "ShaderNodeTexWave", Scale=14.0, Distortion=0.3)
    wave.wave_type, wave.bands_direction = "BANDS", "Y"
    _link(nt, c.co, wave.inputs["Vector"])
    gap = _ramp(nt, wave.outputs["Fac"], [(0.0, (0, 0, 0, 1)), (0.08, (0, 0, 0, 1)), (0.16, (1, 1, 1, 1))])
    board = _mix(nt, _noise(nt, c.co, 40.0, detail=6.0), (0.25, 0.17, 0.09, 1), (0.4, 0.29, 0.16, 1))
    return _mix(nt, gap, (0.04, 0.03, 0.02, 1), board), None


@register_shader("남색벽", big=True)
def _navy(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 9.0), (0.015, 0.045, 0.16, 1), (0.03, 0.08, 0.27, 1))
    return base, None


@register_shader("등대빛_발광")
def _beacon(nt, c, name):
    color = _mix(nt, _noise(nt, c.co, 6.0), (0.9, 0.6, 0.12, 1), (1.0, 0.92, 0.55, 1))
    return color, color


@register_shader("불꽃_발광")
def _flame(nt, c, name):
    color = _mix(nt, _noise(nt, c.co, 12.0), (0.9, 0.25, 0.03, 1), (1.0, 0.75, 0.2, 1))
    return color, color


@register_shader("과녁")
def _target(nt, c, name):
    wave = _node(nt, "ShaderNodeTexWave", Scale=11.0, Distortion=0.0)
    wave.wave_type, wave.rings_direction = "RINGS", "Y"
    _link(nt, c.co, wave.inputs["Vector"])
    rings = _ramp(nt, wave.outputs["Fac"], [(0.0, (0.6, 0.55, 0.45, 1)), (0.5, (0.6, 0.55, 0.45, 1)), (0.52, (0.5, 0.04, 0.03, 1)), (1.0, (0.5, 0.04, 0.03, 1))])
    return rings, None


import gen_shops_blender as _old  # noqa: E402,F401  (해도·지구의·헌돛천 등 옛 재질 등록)
import gen_shops_impl1 as _impl1  # noqa: E402,F401  (칼날·말린풀 등)

PORTAL_Z = 6.0


# ──────────────────────────────────────────────────────────── 짓는 도구

def oriented(b, pts, mat, inside):
    """다각형 한 면 — 감김을 inside(안쪽 한 점) 반대로 바깥이 보이게 맞춘다."""
    pts = [Vector(p) for p in pts]
    n = Vector()
    for i, p in enumerate(pts):
        q = pts[(i + 1) % len(pts)]
        n += Vector(((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y)))
    c = sum(pts, Vector()) / len(pts)
    if n.dot(c - Vector(inside)) < 0:
        pts.reverse()
    return b._face([b.bm.verts.new(p) for p in pts], mat)


def lathe(b, cx, cy, profile, mats, sides=12, phase=0.0, cap_top=False, cap_bottom=False):
    """회전체. profile = [(반지름, z) …] 아래→위, 반지름 0이면 꼭짓점. mats = 재질 이름(들) — 옆면 i마다 돌려 쓴다(줄무늬)."""
    if isinstance(mats, str):
        mats = [mats]
    rings = []
    for r, z in profile:
        if r == 0:
            rings.append([b.bm.verts.new((cx, cy, z))] * sides)
        else:
            rings.append([b.bm.verts.new((cx + r * math.cos(phase + math.tau * i / sides), cy + r * math.sin(phase + math.tau * i / sides), z))
                          for i in range(sides)])
    for k in range(len(profile) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            a, c = rings[k][i], rings[k][j]
            d, e = rings[k + 1][i], rings[k + 1][j]
            m = mats[i % len(mats)]
            if profile[k][0] == 0 and profile[k + 1][0] == 0:
                continue
            if profile[k + 1][0] == 0:
                b._face((a, c, d), m)
            elif profile[k][0] == 0:
                b._face((a, e, d), m)
            else:
                b._face((a, c, e, d), m)
    m0 = mats[0]
    if cap_top and profile[-1][0] > 0:
        b._face(list(rings[-1]), m0)
    if cap_bottom and profile[0][0] > 0:
        b._face(list(reversed(rings[0])), m0)


def bar(b, p0, p1, w, d, mat):
    """두 점을 잇는 각재(단면 w×d). 단면 w 방향은 Y축과 직각."""
    p0, p1 = Vector(p0), Vector(p1)
    t = p1 - p0
    side = Vector((0, 1, 0)).cross(t)
    if side.length < 1e-4:
        side = Vector((1, 0, 0)).cross(t)
    side.normalize()
    up = t.cross(side).normalized()
    sq = [p0 - side * w / 2 - up * d / 2, p0 + side * w / 2 - up * d / 2, p0 + side * w / 2 + up * d / 2, p0 - side * w / 2 + up * d / 2]
    b.extrude(sq, t, mat)


def frame_matrix(origin, normal, xdir):
    """로컬 z=normal, x=xdir(직교화), y=z×x 인 4×4."""
    ez = Vector(normal).normalized()
    ex = (Vector(xdir) - ez * Vector(xdir).dot(ez)).normalized()
    ey = ez.cross(ex)
    m = Matrix((ex, ey, ez)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def loft(b, stations, mat, closed=True):
    """단면 고리(점 목록, 같은 개수)들을 이어 면을 만든다 — 면 방향은 각 단면 무게중심 바깥으로."""
    rings = [[b.bm.verts.new(p) for p in st] for st in stations]
    n = len(stations[0])
    cens = [sum((Vector(p) for p in st), Vector()) / n for st in stations]
    for k in range(len(stations) - 1):
        for i in range(n if closed else n - 1):
            j = (i + 1) % n
            quad = [rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]]
            c = sum((v.co for v in quad), Vector()) / 4
            normal = (quad[1].co - quad[0].co).cross(quad[3].co - quad[0].co)
            inside = (cens[k] + cens[k + 1]) / 2
            if normal.dot(c - inside) < 0:
                quad.reverse()
            b._face(quad, mat)
    return rings


def octagon_plinth(b, r, z0, z1, mat, sides=16, phase=0.0):
    lathe(b, 0, 0, [(r, z0), (r, z1)], mat, sides=sides, phase=phase, cap_top=True)


def dice(b, center, size, yaw, tilt, mat, pips):
    """주사위 하나 — yaw(Z축)·tilt(X축) 돌려 앉힌다. 정면 −Y 면에 5점, 윗면에 2점."""
    rot = Matrix.Translation(center) @ Matrix.Rotation(math.radians(yaw), 4, "Z") @ Matrix.Rotation(math.radians(tilt), 4, "X")
    s = size
    corner = [(-s / 2, -s / 2, -s / 2), (s / 2, -s / 2, -s / 2), (s / 2, s / 2, -s / 2), (-s / 2, s / 2, -s / 2)]
    b.extrude(corner, (0, 0, s), mat, matrix=rot)
    p = s * 0.17
    for px, pz in [(-0.27, -0.27), (0.27, -0.27), (0, 0), (-0.27, 0.27), (0.27, 0.27)]:
        px, pz = px * s, pz * s
        b.extrude([(px - p / 2, -s / 2 - 0.001, pz - p / 2), (px + p / 2, -s / 2 - 0.001, pz - p / 2),
                   (px + p / 2, -s / 2 - 0.001, pz + p / 2), (px - p / 2, -s / 2 - 0.001, pz + p / 2)],
                  (0, -0.06, 0), pips, caps=(False, True), matrix=rot)
    for px, py in [(-0.22, -0.22), (0.22, 0.22)]:
        px, py = px * s, py * s
        b.extrude([(px - p / 2, py - p / 2, s / 2 + 0.001), (px + p / 2, py - p / 2, s / 2 + 0.001),
                   (px + p / 2, py + p / 2, s / 2 + 0.001), (px - p / 2, py + p / 2, s / 2 + 0.001)],
                  (0, 0, 0.06), pips, caps=(False, True), matrix=rot)


def shard(b, cx, cy, cz, height, radius, mat, sides=6, lean=(0.0, 0.0), spin=0.0):
    base = [(cx + radius * math.cos(spin + math.tau * i / sides), cy + radius * math.sin(spin + math.tau * i / sides), cz) for i in range(sides)]
    b.spike(base, (cx + lean[0], cy + lean[1], cz + height), mat)


def diamond(b, cx, cy, cz, half_h, radius, mat, sides=6, squash=1.0):
    """마름모 결정(위아래 뾰족) — 아래 뿔은 뒤집은 spike."""
    ring = [(cx + radius * math.cos(math.tau * i / sides), cy + radius * squash * math.sin(math.tau * i / sides), cz) for i in range(sides)]
    b.spike(ring, (cx, cy, cz + half_h), mat)
    ring2 = list(reversed(ring))
    b.spike(ring2, (cx, cy, cz - half_h), mat)


# ──────────────────────────────────────────────────────────── 1. 도박소 — 줄무늬 큰 천막 + 주사위

def make_gambling():
    b = Builder()
    P = "상점_도박소_"
    red, cream, wood, stone = P + "천", P + "돛천", P + "붉은목재", P + "석재"
    lamp, die, pips, dark, brass, black = P + "등_발광", P + "주사위", P + "검정", P + "어둠", P + "황동", P + "검정"
    ph = math.radians(11.25)                       # 정면(−Y) 한 면이 가운데 오도록
    octagon_plinth(b, 4.4, 0.0, 0.55, stone, sides=16, phase=ph)
    lathe(b, 0, 0, [(4.0, 0.55), (4.0, 5.2)], [red, cream], sides=16, phase=ph)
    lathe(b, 0, 0, [(4.38, 5.2), (4.38, 5.5), (0.35, 12.6)], [red, cream], sides=16, phase=ph, cap_bottom=True)
    # 처마 끝 물결 — 천막 가장자리 아랫단
    lathe(b, 0, 0, [(4.38, 5.2), (4.0, 5.2)], [red, cream], sides=16, phase=ph)
    # 꼭대기: 쇠 꼭지 + 비스듬히 앉은 큰 주사위
    b.cylinder((0, 0, 12.4), (0, 0, 1), 0.3, 0.4, brass, sides=8)
    dice(b, Vector((0, 0, 13.55)), 2.0, 32, 18, die, pips)
    # 입구: 붉은 기둥 둘 + 상인방 + 어두운 안쪽 + 반쯤 걷은 커튼
    for sx in (-1, 1):
        b.box(sx * 1.55 - 0.28, sx * 1.55 + 0.28, -4.35, -3.75, 0.55, 5.6, wood, skip=("bottom",))
    b.box(-1.9, 1.9, -4.35, -3.75, 5.0, 5.6, wood)
    b.box(-1.3, 1.3, -3.98, -3.78, 0.55, 5.0, dark, skip=("+y", "bottom"))
    b.extrude([(-1.3, -4.1, 5.0), (-0.1, -4.1, 5.0), (-0.45, -4.2, 1.6), (-1.3, -4.15, 1.2)], (0, 0.1, 0), red)
    b.extrude([(0.5, -4.1, 5.0), (1.3, -4.1, 5.0), (1.3, -4.1, 0.9), (0.95, -4.2, 2.6)], (0, 0.1, 0), red)
    # 입구 위 룰렛 간판 — 천막 비탈에 기대 세운 원판(12조각 붉은·검정)
    n = Vector((0, -0.86, 0.51))
    center = Vector((0, -3.45, 7.0))
    m = frame_matrix(center, n, (1, 0, 0))
    b.extrude([(1.7 * math.cos(math.tau * i / 12), 1.7 * math.sin(math.tau * i / 12), 0) for i in range(12)], (0, 0, 0.12), brass, matrix=m)
    for i in range(12):
        a0, a1 = math.tau * i / 12, math.tau * (i + 1) / 12
        tri = [(0, 0, 0.12), (1.45 * math.cos(a0), 1.45 * math.sin(a0), 0.12), (1.45 * math.cos(a1), 1.45 * math.sin(a1), 0.12)]
        b.extrude(tri, (0, 0, 0.1), red if i % 2 == 0 else black, matrix=m)
    b.extrude([(0.3 * math.cos(math.tau * i / 8), 0.3 * math.sin(math.tau * i / 8), 0.22) for i in range(8)], (0, 0, 0.12), brass, matrix=m)
    # 등 둘(문 양옆 처마에) + 땅에 놓인 주사위 둘
    for cx in (-3.3, 3.3):
        b.cylinder((cx, -3.95, 5.1), (0, 0, -1), 0.5, 1.0, lamp, sides=10)
        b.box_c(cx, -3.95, 0.12, 0.12, 5.1, 0.6, black)
    dice(b, Vector((-3.3, -3.65, 0.55)), 1.0, 20, 0, die, pips)
    dice(b, Vector((3.3, -3.65, 0.55)), 1.0, -30, 0, die, pips)
    return b


# ──────────────────────────────────────────────────────────── 2. 유닛강화소 — 굴뚝 높은 대장간 + 모루

def make_smithy():
    b = Builder()
    P = "상점_유닛강화소_"
    stone, rubble, brick, timber, roof = P + "석재", P + "돌벽", P + "벽돌", P + "목재", P + "지붕"
    iron, forge, leather, black = P + "쇠", P + "화덕_발광", P + "가죽", P + "검정"
    b.box(-4.45, 4.45, -4.45, 4.45, 0.0, 0.5, stone, skip=("bottom",))
    # 본채: 막돌 상자 (뒤쪽으로 깊게) — 앞면 z는 낮고 뒤가 높은 외지붕
    b.box(-4.2, 4.2, -2.6, 3.9, 0.5, 6.4, rubble, skip=("bottom", "top"))
    for sx in (-1, 1):
        b.box_c(sx * 4.1, -2.6, 0.45, 0.45, 0.5, 6.0, timber, skip=("bottom",))
    pts = [(-4.5, -3.3, 6.2), (4.5, -3.3, 6.2), (4.5, -3.3, 6.65), (-4.5, -3.3, 6.65)]
    # 외지붕: 앞(−y) 낮고 뒤(+y) 높음 — 단면(y,z)을 x로 민다
    slab = [(-3.4, 6.2), (4.2, 8.6), (4.2, 9.05), (-3.4, 6.65)]
    b.extrude([(-4.5, y, z) for y, z in slab], (9.0, 0, 0), roof)
    # 뒤 벽 삼각 채움은 지붕 아래라 안 보임 — 앞면 처마 보
    b.box(-4.5, 4.5, -3.4, -3.0, 6.0, 6.3, timber)
    # 굴뚝 — 앞 왼쪽 모서리에서 15.4까지 솟는다(벽돌, 위로 갈수록 좁게, 연기 자리)
    b.box(-3.9, -0.7, -2.7, 0.4, 0.5, 6.6, brick, skip=("bottom",))
    b.box(-3.5, -1.1, -2.4, 0.1, 6.6, 11.0, brick, skip=("bottom",))
    b.box(-3.25, -1.35, -2.15, -0.15, 11.0, 14.4, brick, skip=("bottom",))
    b.box(-3.4, -1.2, -2.3, 0.0, 14.4, 15.0, brick)
    b.box(-3.1, -1.5, -2.0, -0.3, 15.0, 15.4, black, skip=("bottom",))
    b.marker("연기_자리_01", (-2.3, -1.15, 15.4))
    # 화덕 — 굴뚝 밑 앞으로 내민 아궁이: 돌 틀 + 이글거리는 숯
    b.box(-3.7, -0.9, -3.9, -2.6, 0.5, 2.9, rubble, skip=("bottom", "top"))
    b.box(-3.7, -0.9, -3.9, -2.6, 2.9, 3.3, stone)
    b.box(-3.2, -1.4, -3.95, -3.55, 0.9, 2.5, black, skip=("+y", "bottom"))
    b.box(-3.2, -1.4, -3.99, -3.8, 0.9, 1.6, forge, skip=("bottom",))
    b.marker("불_자리_01", (-2.3, -3.75, 1.7))
    # 풀무 — 화덕 오른쪽, 가죽 주머니 + 나무 손잡이
    b.extrude([(0.1, -3.6, 0.5), (0.1, -3.6, 1.2), (0.5, -3.6, 2.2), (1.7, -3.6, 1.2), (1.7, -3.6, 0.5)], (0, 1.0, 0), leather)
    bar(b, (1.5, -3.2, 1.2), (3.0, -3.2, 2.3), 0.2, 0.2, timber)
    # 모루 — 큰 것 하나(앞 오른쪽), 그루터기 위
    b.cylinder((2.5, -3.4, 0.0), (0, 0, 1), 0.9, 1.1, timber, sides=10, phase=0.3)
    anvil = [(-0.8, 0), (0.8, 0), (0.8, 0.4), (0.35, 0.6), (0.35, 1.15), (1.4, 1.15), (1.4, 1.55), (-0.9, 1.55), (-2.1, 1.35), (-0.9, 1.15),
             (-0.35, 1.15), (-0.35, 0.6), (-0.8, 0.4)]
    k = 1.15
    b.extrude([(2.5 + x * k, -3.4 - 0.65 * k + 0.0, 1.1 + z * k) for x, z in anvil], (0, 1.3 * k, 0), iron)
    # 망치 — 모루 위에 비스듬히 얹은 큰 망치
    bar(b, (2.2, -4.35, 3.5), (3.8, -4.35, 2.9), 0.28, 0.28, timber)
    b.box_c(3.9, -4.35, 0.9, 0.75, 2.5, 0.9, iron) if False else None
    # 지붕 위 우뚝 선 큰 망치 간판(오른쪽 모서리) — 자루 + 쇠 망치머리
    b.box_c(3.2, -0.3, 0.4, 0.4, 8.0, 5.3, timber)
    b.box_c(3.0, -0.3, 2.0, 1.1, 12.8, 1.3, iron)
    b.box_c(3.2, -0.3, 3.2, 1.2, 13.0, 0.9, iron) if False else None
    # 바닥 장식 쇠창살 문(오른쪽 앞 벽) — 줄 창
    for i in range(4):
        b.box_c(0.8 + i * 0.7, -2.65, 0.14, 0.12, 2.4, 2.4, iron)
    b.box(0.6, 3.4, -2.7, -2.55, 2.3, 2.5, iron)
    b.box(0.6, 3.4, -2.7, -2.55, 4.7, 4.9, iron)
    return b


# ──────────────────────────────────────────────────────────── 3. 다른세계강화소 — 보랏빛 고리 관문

def make_portal():
    b = Builder()
    P = "상점_다른세계강화소_"
    stone, rune, glow, dark = P + "석재", P + "룬석", P + "포탈_발광", P + "어둠"
    octagon_plinth(b, 4.4, 0.0, 0.45, stone, sides=8, phase=math.radians(22.5))
    octagon_plinth(b, 3.9, 0.45, 0.9, rune, sides=8, phase=math.radians(22.5))
    cz, R_out, R_in = PORTAL_Z, 4.2, 3.35
    # 받침 둘
    for sx in (-1, 1):
        b.box_c(sx * 1.9, 0.0, 1.9, 1.6, 0.9, 1.1, rune)
    # 고리: 24조각(바깥 R_out·안 R_in, 앞뒤 1.1) — 안쪽 R_in 원판이 포탈
    N = 24
    for i in range(N):
        a0, a1 = math.tau * i / N, math.tau * (i + 1) / N
        pts = [(R_out * math.cos(a0), 0, cz + R_out * math.sin(a0)), (R_out * math.cos(a1), 0, cz + R_out * math.sin(a1)),
               (R_in * math.cos(a1), 0, cz + R_in * math.sin(a1)), (R_in * math.cos(a0), 0, cz + R_in * math.sin(a0))]
        mat = rune
        b.extrude([(x, -0.55, z) for x, _, z in pts], (0, 1.1, 0), mat)
    # 고리 위 룬 발광 블록 8개
    for i in range(8):
        a = math.tau * (i + 0.5) / 8
        r = (R_out + R_in) / 2
        b.box_c(r * math.cos(a), -0.6, 0.5, 0.2, cz + r * math.sin(a) - 0.25, 0.5, glow)
    # 포탈 면 — 소용돌이
    b.extrude([(R_in * 1.01 * math.cos(math.tau * i / 24), -0.1, cz + R_in * 1.01 * math.sin(math.tau * i / 24)) for i in range(24)], (0, 0.2, 0), glow)
    b.marker("빛_자리_01", (0.0, -0.9, cz))
    # 고리 위 떠 있는 큰 결정 + 도는 작은 조각
    diamond(b, 0.0, 0.0, 12.4, 2.6, 1.1, glow, sides=6)
    diamond(b, -2.4, 0.0, 12.0, 1.0, 0.5, glow, sides=5)
    diamond(b, 2.5, -0.3, 11.0, 0.9, 0.45, glow, sides=5)
    # 문 앞 돌계단 둘
    b.box(-1.6, 1.6, -4.4, -3.4, 0.0, 0.5, stone, skip=("bottom",))
    b.box(-1.3, 1.3, -3.4, -2.9, 0.0, 0.9, stone, skip=("bottom",))
    return b


# ──────────────────────────────────────────────────────────── 4. 영원강화소 — 기둥 신전

def make_eternal():
    b = Builder()
    P = "상점_영원강화소_"
    marble, gold, roof, dark, flame = P + "대리석", P + "금빛", P + "지붕", P + "어둠", P + "불꽃_발광"
    b.box(-4.45, 4.45, -4.45, 4.45, 0.0, 0.4, marble, skip=("bottom",))
    b.box(-4.1, 4.1, -4.0, 4.1, 0.4, 0.8, marble, skip=("bottom",))
    b.box(-3.75, 3.75, -3.6, 3.7, 0.8, 1.2, marble, skip=("bottom",))
    # 안채(셀라) — 뒤쪽 벽체 + 어두운 문
    b.box(-2.8, 2.8, -0.2, 3.3, 1.2, 9.0, marble, skip=("bottom", "top"))
    b.box(-0.9, 0.9, -0.24, -0.2, 1.2, 4.6, dark, skip=("+y", "bottom"))
    # 기둥 12(앞 6, 옆 3씩)
    cols = [(x, -3.2) for x in (-3.3, -1.98, -0.66, 0.66, 1.98, 3.3)] + [(sx * 3.3, y) for sx in (-1, 1) for y in (-1.3, 0.6, 2.5)]
    for cx, cy in cols:
        b.box_c(cx, cy, 1.15, 1.15, 1.2, 0.25, marble)
        b.cylinder((cx, cy, 1.45), (0, 0, 1), 0.46, 7.3, marble, sides=10)
        b.box_c(cx, cy, 1.3, 1.3, 8.75, 0.3, gold)
    # 들보 + 금띠
    b.box(-3.9, 3.9, -3.85, 3.7, 9.05, 9.6, marble)
    b.box(-3.95, 3.95, -3.9, -3.8, 9.1, 9.5, gold, skip=("+y",)) if False else None
    for i in range(7):
        b.box_c(-3.3 + i * 1.1, -3.9, 0.55, 0.12, 9.15, 0.35, gold)
    # 박공 지붕(앞이 삼각 박공) — 용마루가 앞뒤
    L0, L1 = -4.1, 3.9
    for sx in (-1, 1):
        b.extrude([(sx * 4.2, L0, 9.55), (0, L0, 12.9), (0, L0, 13.35), (sx * 4.2, L0, 9.55 + 0.45)], (0, L1 - L0, 0), roof)
    # 박공 삼각 벽 + 금 태양 문양
    b.extrude([(-3.95, -3.88, 9.6), (3.95, -3.88, 9.6), (0.0, -3.88, 12.6)], (0, 0.2, 0), marble)
    star = []
    for k in range(24):
        a = math.pi / 2 - math.tau * k / 24
        r = 1.0 if k % 2 == 0 else 0.62
        star.append((1.05 * r * math.cos(a), -3.9, 10.5 + 1.05 * r * math.sin(a)))
    b.extrude(star, (0, -0.15, 0), gold)
    # 용마루 금 구슬·양 끝 장식
    b.cylinder((0.0, -3.9, 13.35), (0, 0, 1), 0.3, 0.5, gold, sides=8)
    shops_octa = [(0.0, -3.9, 13.85)]
    diamond(b, 0.0, -3.9, 14.1, 0.5, 0.4, gold, sides=6)
    # 앞 계단 양옆 화로(불꽃 자리)
    for sx in (-1, 1):
        b.cylinder((sx * 3.9, -3.9, 0.0), (0, 0, 1), 0.38, 1.4, marble, sides=8)
        b.cylinder((sx * 3.9, -3.9, 1.4), (0, 0, 1), 0.55, 0.35, gold, sides=8)
        b.cylinder((sx * 3.9, -3.9, 1.75), (0, 0, 1), 0.5, 0.45, flame, sides=8)
        b.marker(f"불_자리_0{1 if sx < 0 else 2}", (sx * 3.9, -3.9, 2.2))
    return b


# ──────────────────────────────────────────────────────────── 5. 공격타입강화소 — 성가퀴 무기고

def make_armory():
    b = Builder()
    P = "상점_공격타입강화소_"
    wall, brick, stone, roof = P + "돌벽", P + "무기고벽돌", P + "석재", P + "지붕"
    iron, blade, timber, cloth = P + "쇠", P + "칼날", P + "목재", P + "천"
    target, leather, brass, dark = P + "과녁", P + "가죽", P + "황동", P + "어둠"
    b.box(-4.45, 4.45, -4.45, 4.45, 0.0, 0.4, stone, skip=("bottom",))
    # 본체 + 성가퀴
    b.box(-3.7, 3.7, -3.0, 3.7, 0.4, 8.2, brick, skip=("bottom",))
    b.box(-3.9, 3.9, -3.2, 3.9, 8.2, 8.55, wall, skip=("bottom",))
    for i in range(6):
        x = -3.4 + i * 1.36
        b.box(x, x + 0.8, -3.2, -2.8, 8.55, 9.6, wall)
        b.box(x, x + 0.8, 3.5, 3.9, 8.55, 9.6, wall)
    for j in range(4):
        y = -2.4 + j * 1.55
        b.box(-3.9, -3.5, y, y + 0.8, 8.55, 9.6, wall)
        b.box(3.5, 3.9, y, y + 0.8, 8.55, 9.6, wall)
    # 귀퉁이 둥근 탑 + 뾰족 쇠기와 모자
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * 3.3, sy * 3.1
            lathe(b, cx, cy, [(1.0, 0.4), (1.0, 10.2)], wall, sides=10, cap_bottom=False)
            lathe(b, cx, cy, [(1.15, 10.2), (1.15, 10.55), (0.0, 12.9)], roof, sides=10, cap_bottom=True)
    # 가운데 높은 깃대 + 붉은 깃발
    b.cylinder((0.0, 0.5, 8.55), (0, 0, 1), 0.14, 7.0, iron, sides=6)
    b.extrude([(0.14, 0.5, 15.3), (2.8, 0.5, 14.6), (0.14, 0.5, 13.6)], (0, 0.12, 0), cloth)
    # 정문 — 짙은 아치 문, 쇠 장식
    b.box(-1.3, 1.3, -3.18, -3.0, 0.4, 4.4, dark, skip=("+y", "bottom"))
    b.box(-1.6, -1.3, -3.4, -3.0, 0.4, 4.7, wall, skip=("bottom",))
    b.box(1.3, 1.6, -3.4, -3.0, 0.4, 4.7, wall, skip=("bottom",))
    b.box(-1.6, 1.6, -3.4, -3.0, 4.4, 4.8, wall)
    for i in range(5):
        b.box_c(-1.0 + i * 0.5, -3.22, 0.1, 0.06, 0.8, 3.4, iron)
    # 정문 위 교차한 큰 검 둘(X자)
    for ang in (35, -35):
        a = math.radians(ang)
        cx, cz = 0.0, 6.5
        d = Vector((math.sin(a), 0, math.cos(a)))
        b.spike([(cx - d.x * 0.3 + 0.0, -3.28, cz - d.z * 0.3), ], (0, 0, 0), blade) if False else None
        tip = Vector((cx, -3.3, cz)) + d * 2.6
        butt = Vector((cx, -3.3, cz)) - d * 2.0
        bar(b, butt, tip, 0.34, 0.14, blade)
        guard = Vector((cx, -3.3, cz)) - d * 0.8
        nrm = Vector((d.z, 0, -d.x))
        bar(b, guard - nrm * 0.55, guard + nrm * 0.55, 0.16, 0.2, brass)
        bar(b, butt, butt + d * 0.8, 0.14, 0.16, leather)
    # 앞 오른쪽: 과녁(원판) 스탠드 / 앞 왼쪽: 창 걸이
    m = frame_matrix(Vector((2.6, -3.9, 3.3)), (0, -0.96, 0.28), (1, 0, 0))
    b.extrude([(1.3 * math.cos(math.tau * i / 16), 1.3 * math.sin(math.tau * i / 16), 0) for i in range(16)], (0, 0, 0.16), target, matrix=m)
    bar(b, (1.6, -3.7, 0.4), (2.1, -3.85, 2.3), 0.22, 0.22, timber)
    bar(b, (3.6, -3.7, 0.4), (3.1, -3.85, 2.3), 0.22, 0.22, timber)
    b.box(-4.2, -2.0, -4.35, -3.95, 1.4, 1.65, timber)
    b.box(-4.2, -2.0, -4.35, -3.95, 3.2, 3.45, timber)
    for i in range(5):
        x = -4.0 + i * 0.45
        b.cylinder((x, -4.15, 0.45), (0, 0, 1), 0.07, 5.0, timber, sides=5)
        b.spike([(x - 0.15, -4.15 - 0.03, 5.4), (x + 0.15, -4.15 - 0.03, 5.4), (x + 0.15, -4.15 + 0.03, 5.4), (x - 0.15, -4.15 + 0.03, 5.4)],
                (x, -4.15, 6.6), blade)
    # 방패 둘
    for cx in (-2.35, 2.35):
        m = frame_matrix(Vector((cx, -3.2, 3.2)), (0, -1, 0), (1, 0, 0))
        b.extrude([(0.65 * math.cos(math.tau * i / 12), 0.65 * math.sin(math.tau * i / 12), 0) for i in range(12)], (0, 0, 0.14), brass, matrix=m)
        b.extrude([(0.45 * math.cos(math.tau * i / 12), 0.45 * math.sin(math.tau * i / 12), 0.14) for i in range(12)], (0, 0, 0.06), cloth, matrix=m)
    return b


# ──────────────────────────────────────────────────────────── 6. 도움소 — 줄무늬 등대

def make_lighthouse():
    b = Builder()
    P = "상점_도움소_"
    white, navy, stone, roof, wood = P + "흰회벽", P + "남색벽", P + "석재", P + "지붕", P + "목재"
    iron, beacon, red, cream = P + "쇠", P + "등대빛_발광", P + "천", P + "돛천"
    brass, dark, timber = P + "황동", P + "어둠", P + "목재"
    octagon_plinth(b, 4.45, 0.0, 0.5, stone, sides=16)
    tx, ty = 1.2, 1.4
    bands = [(0.5, 3.4, white), (3.4, 6.2, navy), (6.2, 9.0, white), (9.0, 11.0, navy)]
    rr = lambda z: 2.2 - (z - 0.5) * (0.95 / 10.5)
    for z0, z1, m in bands:
        lathe(b, tx, ty, [(rr(z0), z0), (rr(z1), z1)], m, sides=12)
    # 회랑(발코니) + 난간
    lathe(b, tx, ty, [(1.9, 11.0), (1.9, 11.4)], iron, sides=12, cap_top=True)
    lathe(b, tx, ty, [(1.9, 11.0), (1.15, 11.0)], iron, sides=12)
    for i in range(12):
        a = math.tau * i / 12
        b.cylinder((tx + 1.8 * math.cos(a), ty + 1.8 * math.sin(a), 11.4), (0, 0, 1), 0.07, 0.7, iron, sides=4)
    lathe(b, tx, ty, [(1.9, 12.05), (1.9, 12.15)], iron, sides=12)
    # 등롱: 불빛 + 모서리 쇠 살
    lathe(b, tx, ty, [(1.05, 11.4), (1.05, 13.4)], beacon, sides=8)
    for i in range(8):
        a = math.tau * i / 8
        b.cylinder((tx + 1.08 * math.cos(a), ty + 1.08 * math.sin(a), 11.4), (0, 0, 1), 0.07, 2.0, iron, sides=4)
    lathe(b, tx, ty, [(1.4, 13.4), (1.4, 13.65), (0.0, 15.3)], roof, sides=8, cap_bottom=True)
    b.cylinder((tx, ty, 15.1), (0, 0, 1), 0.1, 0.5, iron, sides=4)
    b.marker("빛_자리_01", (tx, ty - 1.2, 12.4))
    # 관리소 오두막 (앞 왼쪽): 흰 벽 + 남색 박공 지붕 + 문
    hx0, hx1, hy0, hy1, hz = -4.2, -0.9, -4.1, -0.4, 4.0
    b.box(hx0, hx1, hy0, hy1, 0.5, hz, white, skip=("bottom", "top"))
    for sx in (hx0, hx1):
        b.box_c(sx + (0.1 if sx == hx0 else -0.1), hy0 + 0.1, 0.32, 0.32, 0.5, hz - 0.5, timber, skip=("bottom",))
    hm = (hx0 + hx1) / 2
    for sx in (-1, 1):
        edge = hm + sx * (hx1 - hx0) / 2 * 1.12
        b.extrude([(edge, hy0 - 0.3, hz - 0.1), (hm, hy0 - 0.3, hz + 1.8), (hm, hy0 - 0.3, hz + 2.25), (edge, hy0 - 0.3, hz + 0.35)], (0, hy1 - hy0 + 0.5, 0), roof)
    b.box(hx0 - 0.1, hx1 + 0.1, hy0 - 0.05, hy0 + 0.05, hz - 0.05, hz + 0.1, timber)
    b.box(-3.4, -2.4, hy0 - 0.12, hy0, 0.5, 3.1, timber, skip=("+y", "bottom"))
    b.box(-3.9, -3.5, hy0 - 0.1, hy0, 1.9, 2.9, dark, skip=("+y",))
    # 구명환(관리소 앞벽): 붉은·흰 번갈아
    m = frame_matrix(Vector((-1.5, hy0 - 0.12, 2.5)), (0, -1, 0), (1, 0, 0))
    for i in range(8):
        a0, a1 = math.tau * i / 8, math.tau * (i + 1) / 8
        pts = [(0.62 * math.cos(a0), 0.62 * math.sin(a0), 0), (0.62 * math.cos(a1), 0.62 * math.sin(a1), 0),
               (0.34 * math.cos(a1), 0.34 * math.sin(a1), 0), (0.34 * math.cos(a0), 0.34 * math.sin(a0), 0)]
        b.extrude(pts, (0, 0, 0.14), red if i % 2 == 0 else cream, matrix=m)
    # 해군 깃대(앞 오른쪽): 깃대 + 흰 깃발에 남색 띠
    b.cylinder((3.7, -3.2, 0.5), (0, 0, 1), 0.12, 9.5, iron, sides=6)
    b.extrude([(3.82, -3.2, 9.8), (3.82, -3.2, 7.4), (0.8, -3.2, 7.4), (0.8, -3.2, 9.8)], (0, 0.1, 0), cream)
    b.extrude([(3.82, -3.21, 9.2), (3.82, -3.21, 8.6), (0.8, -3.21, 8.6), (0.8, -3.21, 9.2)], (0, -0.06, 0), navy)
    # 닻 (오른쪽 앞 땅)
    ax, ay = 2.2, -4.0
    b.box_c(ax, ay, 0.34, 0.3, 0.5, 3.4, iron)
    b.box_c(ax, ay, 1.9, 0.3, 3.0, 0.3, iron)
    bar(b, (ax - 0.15, ay, 0.9), (ax - 1.15, ay, 1.7), 0.3, 0.3, iron)
    bar(b, (ax + 0.15, ay, 0.9), (ax + 1.15, ay, 1.7), 0.3, 0.3, iron)
    return b


# ──────────────────────────────────────────────────────────── 7. 항해일지 — 돛 단 배 + 해도 탁자·항해일지

def make_ship():
    b = Builder()
    P = "상점_항해일지_"
    hull, deck, timber, cream, sail = P + "선체", P + "갑판", P + "목재", P + "돛천", P + "헌돛천"
    roof, brass, dark, lamp, black = P + "지붕", P + "황동", P + "어둠", P + "등_발광", P + "검정"
    chart, globe, leather, stone = P + "해도", P + "지구의", P + "가죽", P + "석재"
    # 받침 두 개(배를 땅에 얹음)
    for x in (-2.2, 2.2):
        b.box(x - 0.5, x + 0.5, -1.4, 1.4, 0.0, 1.1, timber, skip=("bottom",))
    # 선체: x는 −4.2(고물)→+4.2(이물). 단면 = 갑판왼·중간왼·용골왼·용골오·중간오·갑판오 (y,z)
    def station(x):
        t = (x + 4.2) / 8.4                          # 0..1
        beam = 2.3 * (1 - 0.18 * max(0, t - 0.55) ** 1 * 0) * (math.sin(math.pi * (0.18 + 0.78 * t) ) if t < 1 else 0.2)
        beam = max(0.5, 2.5 * math.sin(math.pi * (0.1 + 0.82 * t)))
        keel_z = 0.9 + 1.4 * max(0.0, t - 0.7) / 0.3 * 0.7
        deck_z = 3.3 + 1.1 * max(0.0, t - 0.82) / 0.18 + 0.4 * max(0.0, 0.12 - t) / 0.12
        mid_z = (keel_z + deck_z) / 2
        return [(x, -beam, deck_z), (x, -beam * 0.95, mid_z), (x, -beam * 0.35, keel_z),
                (x, beam * 0.35, keel_z), (x, beam * 0.95, mid_z), (x, beam, deck_z)]
    xs = [-4.2 + 8.4 * i / 8 for i in range(9)]
    sts = [station(x) for x in xs]
    rings = loft(b, sts, hull, closed=False)
    # 갑판 덮개(위쪽) + 앞뒤 마감
    top = [Vector(s[0]) for s in sts] + [Vector(s[5]) for s in reversed(sts)]
    oriented(b, top, deck, (0, 0, 0))
    for st, inside in ((sts[0], (1, 0, 3)), (sts[-1], (-1, 0, 3))):
        oriented(b, [Vector(p) for p in st], hull, (st[0][0] + (1 if st is sts[0] else -1), 0, 3))
    # 고물 선실(뒤): 널 상자 + 지붕 + 창
    b.box(-4.0, -1.0, -2.0, 2.0, 3.3, 6.3, hull, skip=("bottom",))
    b.extrude([(-4.3, -2.3, 6.3), (-0.7, -2.3, 6.3), (-0.7, -2.3, 6.6), (-4.3, -2.3, 6.6)], (0, 4.6, 0), roof)
    for wy in (-1.0, 1.0):
        b.box(-1.0, -0.94, wy - 0.45, wy + 0.45, 4.3, 5.4, lamp, skip=("+x",)) if False else None
    for wx in (-3.1, -1.9):
        b.box(wx - 0.4, wx + 0.4, -2.07, -2.0, 4.1, 5.3, lamp, skip=("+y",))
        b.box(wx - 0.5, wx + 0.5, -2.12, -2.0, 4.0, 4.1, timber, skip=("+y",))
    # 돛대 + 활대 + 돛
    mx = 0.4
    b.cylinder((mx, 0.0, 3.3), (0, 0, 1), 0.3, 11.7, timber, sides=8)
    bar(b, (-2.8, 0.0, 12.6), (3.6, 0.0, 12.6), 0.3, 0.3, timber)
    b.extrude([(-2.6, -0.4, 12.4), (3.4, -0.4, 12.4), (3.4, -0.4, 6.6), (-2.6, -0.4, 6.6)], (0, -0.12, 0), cream)
    # 돛 위 나침반(황동 별 + 짙은 둥근 판)
    cx, cz = 0.4, 9.5
    b.cylinder((cx, -0.52, cz), (0, -1, 0), 1.45, 0.08, black, sides=16)
    star = []
    for k in range(16):
        a = math.pi / 2 - math.tau * k / 16
        r = 1.35 if k % 4 == 0 else (0.7 if k % 2 == 0 else 0.3)
        star.append((cx + r * math.cos(a), -0.6, cz + r * math.sin(a)))
    b.extrude(star, (0, -0.07, 0), brass)
    # 망대(통) + 깃발
    b.cylinder((mx, 0.0, 14.1), (0, 0, 1), 0.7, 1.0, timber, sides=8)
    b.cylinder((mx, 0.0, 15.0), (0, 0, 1), 0.08, 0.7, timber, sides=4)
    b.extrude([(mx + 0.08, 0.0, 15.7), (mx + 1.6, 0.0, 15.35), (mx + 0.08, 0.0, 15.0)], (0, 0.08, 0), roof)
    # 이물 뱃머리 비스프리트
    bar(b, (3.8, 0.0, 4.2), (4.25, 0.0, 5.0), 0.3, 0.3, timber)
    # 널다리(앞 왼쪽 갑판 → 땅)
    b.extrude([(-1.0, -2.3, 3.3), (-1.0, -4.4, 0.0), (-1.0, -4.4, 0.25), (-1.0, -2.3, 3.55)], (1.1, 0, 0), deck)
    # 해도 탁자 + 펼친 항해일지 + 지구의(앞 오른쪽 땅)
    b.box(1.2, 3.6, -4.4, -3.2, 1.4, 1.6, timber)
    for tx, ty in ((1.35, -4.25), (3.45, -4.25), (1.35, -3.35), (3.45, -3.35)):
        b.box_c(tx, ty, 0.22, 0.22, 0.0, 1.4, timber, skip=("bottom",))
    b.box(1.3, 3.5, -4.3, -3.3, 1.6, 1.68, chart)
    # 펼친 책: 가죽 표지 + 두 쪽
    b.box(1.5, 2.55, -4.15, -3.45, 1.68, 1.78, leather)
    b.box(2.5, 3.3, -4.15, -3.45, 1.68, 1.78, leather)
    b.extrude([(1.55, -4.1, 1.78), (2.5, -4.1, 1.78 + 0.18), (2.5, -3.5, 1.78 + 0.18), (1.55, -3.5, 1.78)], (0, 0, 0.0), cream) if False else None
    b.box(1.55, 2.5, -4.1, -3.5, 1.78, 1.9, cream)
    b.box(2.5, 3.25, -4.1, -3.5, 1.78, 1.9, cream)
    _old._sphere(b, Vector((3.0, -3.8, 2.7)), 0.55, globe)
    return b


SHOPS = {
    "상점_도박소": make_gambling,
    "상점_유닛강화소": make_smithy,
    "상점_다른세계강화소": make_portal,
    "상점_영원강화소": make_eternal,
    "상점_공격타입강화소": make_armory,
    "상점_도움소": make_lighthouse,
    "상점_항해일지": make_ship,
}

if __name__ == "__main__":
    run(SHOPS, OUT)
