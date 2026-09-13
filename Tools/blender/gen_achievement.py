"""협동 건물(업적판) + 트로피 여섯 — 원작 레인 상점 줄의 h07B(Q_buliding_Cooperation)(PM 배정 2026-09-13). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, 건물은 Assets/Art/Structures/, 트로피는 Assets/Art/Props/.
화면 없이(정본):  blender --background --factory-startup --python gen_achievement.py [-- shop 트로피_문퀘스트 ...]
사장님 창(보여 주기만): ns["build_shop"](컬렉션, 창 텍스처 폴더) · ns["build_trophy"](이름, 컬렉션, 창 텍스처 폴더)

원작: 팀 공동 업적을 이룰 때마다 표시가 한 칸씩 바뀌는 업적판, 칸 여섯(트리거 순) —
  1 문 퀘스트(정의문 파괴) · 2 삼대장 처치 · 3 하늘 퀘스트 1 · 4 하늘 퀘스트 2 · 5 하늘 퀘스트 3(거대 해왕류) · 6 트레저헌터(보물 9개).
우리 게임엔 표시 시스템이 아직 없다 — 건물과 칸 자리(빈 오브젝트)를 먼저 만들어 둔다.
- 상점_업적판: 레인 상점 세트 뼈대 그대로(shops_common.frame, 바닥 9×9·높이 12~16, 정면 박공). 지붕 짙은 자주 유약(ROOF_TONES),
  흰 회벽 + 짙은 목재, 박공 간판 = 월계관 두른 방패. 정면 1층 왼쪽에 앞으로 내민 **유리 진열장**(가로 여섯 칸 선반, 칸마다
  황동 받침 + 명패, 빈 오브젝트 `트로피_자리_01~06` = 위 순서, 받침 윗면), 오른쪽에 문. 🔴 유리 면은 없다 — 알파 컷 유리의
  반사 줄은 테이프 붙인 흰 사선으로 읽혀 트로피를 가렸다(PM 3차). 진열장은 가는 황동 테(앞 모서리·윗면 네 변·아랫단)로만 낸다.
- 트로피 여섯: 각 높이 약 1.2(1.1~1.3 assert), 원점 바닥 가운데, 바닥 반폭 0.45 안(칸 폭 0.92), 삼각형 300 이하, 발광 없음.
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

import gen_ships  # noqa: E402  (트로피 UV·굽기)
import shops_common as sc  # noqa: E402
from gen_docks import DockMesh, box, cyl, lathe, prism  # noqa: E402
from shops_common import (PLINTH, SIGN_Z, WX, WY, Builder, _link, _math, _mix, _node, _noise, _ramp,  # noqa: E402
                          frame, gable_sign, register_shader, window)

UNITS = 11.4
SHOP = "상점_업적판"
OUT_SHOP = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")
OUT_PROPS = os.path.join(HERE, "..", "..", "Assets", "Art", "Props")          # 소품 → Assets/Art/Props (보물 넷과 같은 폴더)
TROPHY_ORDER = ["트로피_문퀘스트", "트로피_삼대장", "트로피_하늘1", "트로피_하늘2", "트로피_하늘3", "트로피_트레저헌터"]
CASE_X0, CASE_X1, CASE_Y, COUNTER_Z, GLASS_Z = -3.6, 1.9, -4.35, 1.3, 3.0
STAND_H = 0.12
STAND_Y = (CASE_Y + (-WY)) / 2
TROPHY_HALF, TROPHY_H, TROPHY_TRIS = 0.45, (1.1, 1.3), 300


def slot_x(k):
    return CASE_X0 + (CASE_X1 - CASE_X0) / 6 * (k + 0.5)


# ──────────────────────────────────────────────────────────── 건물

@register_shader("벨벳")
def _velvet(nt, c, name):
    """진열장 뒤판 — 짙은 남색 벨벳(금·은 트로피가 또렷하게)."""
    return _mix(nt, _noise(nt, c.co, 25.0, detail=3.0), (0.012, 0.018, 0.06, 1), (0.03, 0.04, 0.11, 1)), None


@register_shader("자주")
def _purple_enamel(nt, c, name):
    return _mix(nt, _noise(nt, c.co, 18.0), (0.12, 0.02, 0.14, 1), (0.22, 0.05, 0.25, 1)), None


def make_achievement():
    b = Builder()
    P = SHOP + "_"
    wall, timber, stone, roof = P + "흰회벽", P + "목재", P + "석재", P + "지붕"
    velvet, brass, dark, black, purple = P + "벨벳", P + "황동", P + "어둠", P + "검정", P + "자주"
    frame(b, wall, timber, stone, roof)
    y = -WY
    # ── 진열장: 받침장 · 벨벳 뒤판 · 가는 황동 테 · 칸 받침 · 명패 · 트로피 자리
    b.box(CASE_X0, CASE_X1, CASE_Y, y, 0.0, COUNTER_Z, timber, skip=("+y",))
    b.box(CASE_X0 + 0.05, CASE_X1 - 0.05, y - 0.06, y, COUNTER_Z, GLASS_Z, velvet, skip=("+y",))
    # 🔴 1차 통나무 뚜껑은 게임 시점(50° 내려다봄)에서 트로피를 통째로 가렸고, 2차 유리 면은 반사 줄이 테이프처럼 보였다 —
    # 윗면은 열어 두고 「유리 진열장」은 가는 황동 테(앞 모서리 기둥 둘 · 윗면 네 변 · 앞과 옆 아랫단)로만
    t = 0.06
    for px in (CASE_X0, CASE_X1):
        b.box(px - t, px + t, CASE_Y - t, CASE_Y + t, COUNTER_Z, GLASS_Z, brass)
        b.box(px - t, px + t, CASE_Y + t, y, GLASS_Z - t, GLASS_Z + t, brass, skip=("+y",))
        b.box(px - t, px + t, CASE_Y + t, y, COUNTER_Z, COUNTER_Z + 0.08, brass, skip=("+y",))
    b.box(CASE_X0 - t, CASE_X1 + t, CASE_Y - t, CASE_Y + t, GLASS_Z - t, GLASS_Z + t, brass)
    b.box(CASE_X0 - t, CASE_X1 + t, y - 2 * t, y, GLASS_Z - t, GLASS_Z + t, brass, skip=("+y",))
    b.box(CASE_X0 - t, CASE_X1 + t, CASE_Y - t, CASE_Y + t, COUNTER_Z, COUNTER_Z + 0.08, brass)
    for k in range(6):
        cx = slot_x(k)
        b.box_c(cx, STAND_Y, 0.62, 0.55, COUNTER_Z, STAND_H, brass)
        b.box(cx - 0.25, cx + 0.25, CASE_Y - 0.04, CASE_Y, 0.75, 0.95, brass, skip=("+y",))
        b.marker(f"트로피_자리_{k + 1:02d}", (cx, STAND_Y, COUNTER_Z + STAND_H))
    # ── 문(오른쪽) · 디딤돌 · 진열장 위 가로 띠보 · 윗 창
    dx0, dx1, dh = 2.3, 3.45, 5.0
    b.box(dx0, dx1, y - 0.12, y, PLINTH, PLINTH + dh, timber, skip=("+y",))
    b.box(dx0 - 0.25, dx0, y - 0.28, y, PLINTH, PLINTH + dh + 0.25, timber, skip=("+y",))
    b.box(dx1, dx1 + 0.25, y - 0.28, y, PLINTH, PLINTH + dh + 0.25, timber, skip=("+y",))
    b.box(dx0 - 0.35, dx1 + 0.35, y - 0.38, y, PLINTH + dh, PLINTH + dh + 0.35, timber, skip=("+y",))
    b.cylinder((dx0 + 0.3, y - 0.12, PLINTH + 2.5), (0, -1, 0), 0.12, 0.06, brass, sides=10)
    b.box(dx0 - 0.35, dx1 + 0.35, y - 0.7, y, 0.0, PLINTH, stone, skip=("+y",))
    b.box(-WX, dx0 - 0.4, y - 0.16, y, GLASS_Z + 0.4, GLASS_Z + 0.65, timber, skip=("+y",))
    window(b, -0.85, 4.35, 2.6, 2.2, timber, dark)
    # ── 박공 간판: 짙은 판 위 황동 방패(안은 자주 법랑) + 월계관 잎 여섯씩
    front = gable_sign(b, timber, black)
    shield = [(-0.52, 0.55), (0.52, 0.55), (0.52, 0.0), (0.33, -0.42), (0.0, -0.66), (-0.33, -0.42), (-0.52, 0.0)]
    b.extrude([(x, front, SIGN_Z + 0.08 + z) for x, z in shield], (0, -0.1, 0), brass)
    b.extrude([(x * 0.76, front - 0.1, SIGN_Z + 0.08 + z * 0.76) for x, z in shield], (0, -0.03, 0), purple)
    for sx in (-1, 1):
        for k in range(6):
            t = 0.4 + 0.36 * k
            center = Vector((sx * 0.86 * math.sin(t), -0.86 * math.cos(t)))
            tangent = Vector((sx * math.cos(t), math.sin(t))).normalized()
            normal = Vector((-tangent.y, tangent.x))
            leaf = [center + tangent * 0.14, center + normal * 0.055, center - tangent * 0.14, center - normal * 0.055]
            b.extrude([(p.x, front, SIGN_Z - 0.04 + p.y) for p in leaf], (0, -0.08, 0), brass)
    return b


def build_shop(collection, folder):
    obj, markers, bounds = sc.assemble(SHOP, make_achievement, collection)
    assert markers == [f"트로피_자리_{k:02d}" for k in range(1, 7)], f"트로피 자리 이름·순서: {markers}"
    assert not any("_잎카드" in s.material.name for s in obj.material_slots), "업적판에 알파 재질이 남았다(유리는 뺐다)"
    sc.unwrap(obj)
    sc.bake(obj, folder)
    return obj, markers, bounds


# ──────────────────────────────────────────────────────────── 트로피

class BandMesh(DockMesh):
    """기본 띠 값을 바꿔 가며 짓는다(리본 색·모자 띠)."""

    def __init__(self):
        super().__init__()
        self.band_default = 0.0

    def vert(self, co, band=None):
        return super().vert(co, self.band_default if band is None else band)


def _center(verts):
    return sum((v.co for v in verts), Vector()) / len(verts)


def extrude_poly(mesh, pts, vec, mat, caps=(True, True)):
    """평면 다각형을 vec만큼 밀어낸 판 — 옆면 바깥 = 변 방향 × 판 법선(감김을 법선 기준 반시계로 맞춘 뒤)."""
    pts, vec = [Vector(p) for p in pts], Vector(vec)
    nrm = vec.normalized()
    area = Vector()
    for i, p in enumerate(pts):
        area += p.cross(pts[(i + 1) % len(pts)])
    if area.dot(nrm) < 0:
        pts.reverse()
    base = [mesh.vert(p) for p in pts]
    top = [mesh.vert(p + vec) for p in pts]
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        mesh.face([base[i], base[j], top[j], top[i]], mat, (pts[j] - pts[i]).cross(nrm))
    if caps[0]:
        mesh.face(base, mat, -vec)
    if caps[1]:
        mesh.face(top, mat, vec)


def _base(mesh, name):
    box(mesh, -0.3, 0.3, -0.26, 0.26, 0.0, 0.16, name + "_받침", skip=("bottom",))


def make_door_quest(name):
    """문 퀘스트 — 부서진 강철 문짝 조각(윗단이 뜯긴 판 + 경첩) 앞면에 금박 「正」."""
    mesh = BandMesh()
    steel, gold = name + "_강철", name + "_금박"
    _base(mesh, name)
    outline = [(-0.28, 0.16), (0.28, 0.16), (0.3, 0.8), (0.18, 0.95), (0.08, 0.86), (-0.05, 1.18), (-0.16, 0.98), (-0.3, 1.05)]
    extrude_poly(mesh, [(x, -0.05, z) for x, z in outline], (0, 0.1, 0), steel)
    cyl(mesh, (-0.31, 0.0, 0.3), (-0.31, 0.0, 0.72), 0.05, 8, steel, caps=(True, True))
    for x0, x1, z0, z1 in ((-0.19, 0.19, 0.69, 0.75), (-0.03, 0.03, 0.33, 0.72), (0.0, 0.15, 0.51, 0.56),
                           (-0.16, -0.1, 0.33, 0.5), (-0.23, 0.23, 0.3, 0.36)):          # 正 — 가로·가운데 세로·오른 짧은 가로·왼 짧은 세로·밑 가로
        box(mesh, x0, x1, -0.075, -0.05, z0, z1, gold, skip=("+y",))
    return mesh


def make_admirals(name):
    """삼대장 — 받침 기둥 위 해군 모자(흰 모자·남색 띠·챙), 뒤로 붉은·푸른·노란 세 줄 리본이 받침까지 늘어짐."""
    mesh = BandMesh()
    cap, ribbon, base_m = name + "_모자", name + "_리본", name + "_받침"
    _base(mesh, name)
    cyl(mesh, (0, 0, 0.16), (0, 0, 0.8), 0.06, 8, base_m, caps=(False, True))
    lathe(mesh, [(0.3, 0.8), (0.34, 0.88), (0.36, 0.96), (0.34, 1.06), (0.24, 1.14), (0.05, 1.17)], 10, cap, Matrix.Identity(4),
          bands=[1.0, 1.0, 0.0, 0.0, 0.0, 0.0], cap_bottom=True, cap_top=True)
    mesh.band_default = 1.0                                                                 # 챙 — 남색
    arc = [a for a in (math.radians(v) for v in (-160, -125, -90, -55, -20))]
    # 챙 바깥 반지름 0.43 — 1차 0.46은 앞으로 칸 반폭 0.45를 넘었다(assert)
    visor = [(0.43 * math.cos(a), 0.43 * math.sin(a), 0.82) for a in arc] + [(0.33 * math.cos(a), 0.33 * math.sin(a), 0.82) for a in reversed(arc)]
    extrude_poly(mesh, visor, (0, 0, 0.035), cap)
    for k, band in enumerate((0.0, 0.5, 1.0)):                                              # 붉은·푸른·노란
        mesh.band_default = band
        x = -0.1 + 0.1 * k
        pts = [Vector((x, 0.3, 0.84)), Vector((x * 1.3, 0.34, 0.5)), Vector((x * 1.6, 0.3, 0.17))]
        for p, q in zip(pts, pts[1:]):
            prism(mesh, p, q, 0.07, 0.015, ribbon, up=(0, 1, 0))
    mesh.band_default = 0.0
    return mesh


def _puff(mesh, center, r, mat, squash=0.75, stretch=1.0):
    """뭉게 하나 — squash: 위아래 누름, stretch: 옆(x)으로 늘임."""
    profile = [(0.3 * r, -0.9 * r), (0.8 * r, -0.55 * r), (r, 0.0), (0.8 * r, 0.55 * r), (0.3 * r, 0.9 * r)]
    lathe(mesh, profile, 8, mat, Matrix.Translation(center) @ Matrix.Diagonal((stretch, 1.0, squash, 1.0)),
          cap_bottom=True, cap_top=True)


def make_sky1(name):
    """하늘 퀘스트 1 — 은 받침 단 위에 옆으로 길게 누운 구름 띠(아래 뭉게 둘이 겹쳐 한 덩어리) + 한쪽으로 치우친 위 뭉게.
    🔴 1차(줄기 위 뭉게)는 버섯·나무, 2차(둥근 뭉게 셋을 가운데로 쌓음)는 눈사람으로 읽혔다(PM) — 뭉게를 위아래 0.6배로 누르고
    옆으로 늘여 겹치고, 위 뭉게는 오른쪽으로 치우쳐 대칭을 깬다. 높이 1.1 규격은 받침 단을 올려 맞춘다."""
    mesh = BandMesh()
    silver = name + "_은"
    # 받침 단은 다른 트로피와 같은 짙은 나무 — 3차 은 받침 단은 근접에서 흰 각설탕 덩어리처럼 구름보다 먼저 읽혔다
    box(mesh, -0.26, 0.26, -0.22, 0.22, 0.0, 0.62, name + "_받침", skip=("bottom",))
    for c, r, stretch in (((-0.13, 0.0, 0.73), 0.22, 1.3), ((0.13, 0.0, 0.72), 0.22, 1.3), ((0.12, 0.0, 0.97), 0.26, 1.2)):
        _puff(mesh, Vector(c), r, silver, squash=0.6, stretch=stretch)
    return mesh


def make_sky2(name):
    """하늘 퀘스트 2 — 받침 줄기 위 날개 달린 은 나침반(정면 원판 + 금 8방 별 + 좌우 깃 셋)."""
    mesh = BandMesh()
    silver, gold = name + "_은", name + "_금"
    _base(mesh, name)
    cyl(mesh, (0, 0, 0.16), (0, 0, 0.52), 0.05, 8, silver, caps=(False, True))
    at = Vector((0.0, 0.0, 0.8))
    lathe(mesh, [(0.28, -0.04), (0.28, 0.04)], 12, silver, Matrix.Translation(at) @ Matrix.Rotation(math.radians(90), 4, "X"),
          cap_bottom=True, cap_top=True)
    star = []
    for k in range(16):
        a = math.pi / 2 - 2 * math.pi * k / 16
        r = 0.25 if k % 4 == 0 else (0.12 if k % 2 == 0 else 0.07)
        star.append((r * math.cos(a), -0.04, at.z + r * math.sin(a)))
    extrude_poly(mesh, star, (0, -0.03, 0), gold)
    for sx in (-1, 1):
        for start, tip, width in (((0.24, 0.02, 0.9), (0.36, 0.02, 1.2), 0.12), ((0.26, 0.02, 0.82), (0.38, 0.02, 1.02), 0.11),
                                  ((0.26, 0.02, 0.74), (0.35, 0.02, 0.86), 0.1)):
            prism(mesh, (sx * start[0], start[1], start[2]), (sx * tip[0], tip[1], tip[2]), width, 0.03, silver, up=(0, 1, 0))
    return mesh


def make_sky3(name):
    """하늘 퀘스트 3(거대 해왕류) — 금 받침잔에 꽂힌 휜 상아색 이빨 하나."""
    mesh = BandMesh()
    gold, ivory = name + "_금", name + "_상아"
    _base(mesh, name)
    lathe(mesh, [(0.14, 0.16), (0.2, 0.24), (0.22, 0.4), (0.19, 0.45)], 10, gold, Matrix.Identity(4))
    angles = [2 * math.pi * i / 8 for i in range(8)]
    rings, centers = [], []
    for k in range(7):
        t = k / 6
        c = Vector((0.12 * t * t, 0.0, 0.4 + 0.8 * t))
        r = 0.17 * (1 - t) ** 0.8 + 0.012
        centers.append(c)
        rings.append([mesh.vert(c + Vector((r * math.cos(a), r * math.sin(a), 0.0))) for a in angles])
    for k in range(6):
        mid = (centers[k] + centers[k + 1]) / 2
        for i in range(8):
            j = (i + 1) % 8
            vs = [rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]]
            mesh.face(vs, ivory, _center(vs) - mid)
    mesh.face(rings[0], ivory, (0, 0, -1))
    mesh.face(rings[-1], ivory, (0, 0, 1))
    return mesh


def _coin(mesh, center, axis, mat):
    axis = Vector(axis).normalized()
    cyl(mesh, Vector(center) - axis * 0.012, Vector(center) + axis * 0.012, 0.07, 8, mat, caps=(True, True))


def make_treasure_hunter(name):
    """트레저헌터 — 받침 단 위 금화 넘치는 작은 상자(뒤로 열린 뚜껑, 쇠띠, 흘러내린 닢)."""
    mesh = BandMesh()
    wood, iron, gold, base_m = name + "_나무", name + "_쇠", name + "_금화", name + "_받침"
    _base(mesh, name)
    box(mesh, -0.24, 0.24, -0.2, 0.2, 0.16, 0.34, base_m, skip=("bottom",))
    box(mesh, -0.26, 0.26, -0.18, 0.18, 0.34, 0.62, wood, skip=("bottom",))
    for bx in (-0.14, 0.14):
        box(mesh, bx - 0.03, bx + 0.03, -0.2, -0.18, 0.34, 0.62, iron, skip=("+y",))
    nx, ny = 4, 3
    grid = [[mesh.vert((-0.24 + 0.48 * i / nx, -0.16 + 0.32 * j / ny,
                        0.62 + 0.09 * math.sin(math.pi * i / nx) * math.sin(math.pi * j / ny))) for i in range(nx + 1)] for j in range(ny + 1)]
    for j in range(ny):
        for i in range(nx):
            mesh.face([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]], gold, (0, 0, 1))
    prism(mesh, (0.0, 0.19, 0.62), (0.0, 0.27, 1.14), 0.54, 0.05, wood, up=(0, 1, 0))
    for c, axis in (((0.1, -0.2, 0.6), (0, -0.7, 0.7)), ((-0.18, -0.22, 0.35), (0.1, 0, 1)), ((0.18, -0.24, 0.17), (0, 0, 1)),
                    ((-0.05, 0.0, 0.73), (0.2, 0.1, 1))):
        _coin(mesh, c, axis, gold)
    return mesh


TROPHIES = {"트로피_문퀘스트": (make_door_quest, ("받침", "강철", "금박")),
            "트로피_삼대장": (make_admirals, ("받침", "모자", "리본")),
            "트로피_하늘1": (make_sky1, ("받침", "은")),
            "트로피_하늘2": (make_sky2, ("받침", "은", "금")),
            "트로피_하늘3": (make_sky3, ("받침", "금", "상아")),
            "트로피_트레저헌터": (make_treasure_hunter, ("받침", "나무", "쇠", "금화"))}
assert list(TROPHIES) == TROPHY_ORDER


def _shade_trophy(mat):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    kind = mat.name.rsplit("_", 1)[1]

    def band_attr():
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "띠"
        return attr.outputs["Fac"]

    if kind == "받침":
        color = _mix(nt, _noise(nt, co, 60.0, detail=6.0), (0.03, 0.02, 0.015, 1), (0.075, 0.048, 0.03, 1))
    elif kind == "강철":
        base = _mix(nt, _noise(nt, co, 40.0), (0.07, 0.08, 0.09, 1), (0.15, 0.16, 0.18, 1))
        scratch = _ramp(nt, _noise(nt, co, 120.0, detail=2.0), [(0.62, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
        color = _mix(nt, scratch, base, (0.3, 0.31, 0.33, 1))
    elif kind in ("금박", "금"):
        base = _mix(nt, _noise(nt, co, 40.0), (0.48, 0.3, 0.06, 1), (0.74, 0.52, 0.15, 1))
        color = _mix(nt, _ramp(nt, _noise(nt, co, 90.0), [(0.6, (0, 0, 0, 1)), (0.72, (0.7, 0.7, 0.7, 1))]), base, (0.2, 0.12, 0.03, 1))
    elif kind == "모자":
        color = _mix(nt, _ramp(nt, band_attr(), [(0.45, (0, 0, 0, 1)), (0.55, (1, 1, 1, 1))]),
                     _mix(nt, _noise(nt, co, 30.0), (0.56, 0.56, 0.54, 1), (0.66, 0.66, 0.63, 1)), (0.02, 0.035, 0.11, 1))
    elif kind == "리본":
        color = _ramp(nt, band_attr(), [(0.0, (0.5, 0.02, 0.02, 1)), (0.25, (0.5, 0.02, 0.02, 1)), (0.26, (0.02, 0.08, 0.45, 1)),
                                        (0.74, (0.02, 0.08, 0.45, 1)), (0.75, (0.62, 0.46, 0.03, 1)), (1.0, (0.62, 0.46, 0.03, 1))])
    elif kind == "은":
        base = _mix(nt, _noise(nt, co, 30.0), (0.46, 0.48, 0.5, 1), (0.68, 0.7, 0.72, 1))
        color = _mix(nt, _ramp(nt, _noise(nt, co, 12.0, detail=4.0), [(0.55, (0, 0, 0, 1)), (0.7, (0.6, 0.6, 0.6, 1))]), base, (0.2, 0.2, 0.21, 1))
    elif kind == "상아":
        rings = _node(nt, "ShaderNodeTexWave", Scale=40.0, Distortion=2.0)
        rings.wave_type, rings.bands_direction = "BANDS", "Z"
        _link(nt, co, rings.inputs["Vector"])
        color = _mix(nt, rings.outputs["Fac"], (0.52, 0.47, 0.35, 1), (0.68, 0.62, 0.48, 1))
    elif kind == "나무":
        color = _mix(nt, _noise(nt, co, 60.0, detail=6.0), (0.12, 0.07, 0.035, 1), (0.24, 0.15, 0.07, 1))
    elif kind == "쇠":
        color = _mix(nt, _noise(nt, co, 40.0), (0.025, 0.025, 0.027, 1), (0.06, 0.056, 0.052, 1))
    elif kind == "금화":
        vor = _node(nt, "ShaderNodeTexVoronoi", Scale=160.0)
        _link(nt, co, vor.inputs["Vector"])
        gold = _mix(nt, vor.outputs["Color"], (0.45, 0.28, 0.05, 1), (0.7, 0.48, 0.12, 1))
        color = _mix(nt, _ramp(nt, vor.outputs["Distance"], [(0.62, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))]), gold, (0.16, 0.09, 0.015, 1))
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.6
    _link(nt, color, bsdf.inputs["Base Color"])


def assemble_trophy(name, collection):
    maker, kinds = TROPHIES[name]
    mesh = maker(name)
    assert sorted(mesh.mats) == sorted(f"{name}_{k}" for k in kinds), f"{name} 재질 이름 {mesh.mats} ≠ {kinds}"
    bm = mesh.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs, ys, zs = ([v.co[i] for v in bm.verts] for i in range(3))
    xs, ys, zs = list(xs), list(ys), list(zs)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    assert tris <= TROPHY_TRIS, f"{name} 삼각형 {tris} > {TROPHY_TRIS}"
    assert abs(min(zs)) < 1e-3 and TROPHY_H[0] <= max(zs) <= TROPHY_H[1], f"{name} 높이 {min(zs):.2f}~{max(zs):.2f}"
    reach = max(max(abs(x) for x in xs), max(abs(y) for y in ys))
    assert reach <= TROPHY_HALF + 1e-3, f"{name} 바닥 반폭 {reach:.2f} > {TROPHY_HALF}(칸 폭)"
    info = dict(tris=tris, height=max(zs), reach=reach)
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    data = bpy.data.meshes.new(name)
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    for mat_name in mesh.mats:
        m = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        m.use_nodes = True
        _shade_trophy(m)
        data.materials.append(m)
    return obj, info


def build_trophy(name, collection, folder):
    obj, info = assemble_trophy(name, collection)
    gen_ships.unwrap(obj)
    gen_ships.tex_size = lambda _name: 512
    gen_ships.bake(obj, folder)
    return obj, info


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["shop"] + TROPHY_ORDER
    for key in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        if key == "shop":
            obj, markers, bounds = build_shop(col, os.path.join(OUT_SHOP, "Textures"))
            children = sorted(c.name for c in obj.children)
            assert children == sorted(markers), f"빈 오브젝트 이름이 밀렸다: {children}"
            path, types = os.path.join(OUT_SHOP, SHOP + ".fbx"), {"MESH", "EMPTY"}
            detail = f"x {bounds[0]:.2f}~{bounds[1]:.2f} y {bounds[2]:.2f}~{bounds[3]:.2f} z ~{bounds[4]:.2f}  빈 {children}"
        else:
            obj, info = build_trophy(key, col, os.path.join(OUT_PROPS, "Textures"))
            path, types = os.path.join(OUT_PROPS, key + ".fbx"), {"MESH"}
            detail = f"높이 {info['height']:.2f}  반폭 {info['reach']:.2f}"
        for o in sc._live_objects():
            o.select_set(o is obj or o.parent is obj)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types=types, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {obj.name}  삼각형 {sc.tris(obj)}  {detail}  재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
