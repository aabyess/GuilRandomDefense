"""레인 상점 줄 건물 세트 — 공통 뼈대·셰이더·굽기·내보내기(blender 세션, 2026-09-13).

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/shops_common.py로 옮긴다.
가게 스크립트는 각자 따로(gen_shops_blender.py = 도박소·강화소·해적단퀘스트·항해일지, 구현담당1 = 영원강화소·
공격타입강화소·도움소) — 같은 파일을 둘이 고치지 않게 공통부만 여기 둔다.

가게 스크립트 쓰는 법:
    import sys; sys.path.insert(0, "<이 파일이 있는 폴더>")
    from shops_common import *                        # Builder, frame, window, gable_sign, register_shader, run …
    def make_도움소():
        b = Builder(); P = "상점_도움소_"
        frame(b, P + "회벽", P + "목재", P + "석재", P + "지붕")     # 세트 공통 뼈대(고치지 말 것)
        ...                                                          # 입구·간판 기호·소품
        return b
    SHOPS = {"상점_도움소": make_도움소}
    if __name__ == "__main__": run(SHOPS, out_dir)
  화면 없이:  blender --background --factory-startup --python <가게 스크립트> [-- 상점_도움소]
  사장님 창:   build_in_window("상점_도움소", make_도움소, 컬렉션)   (창에서는 내보내지 않는다 — 이름 밀림)

세트 규격(PM): 바닥 9×9 안, 전체 높이 12~16, 원점 바닥 가운데, 정면 −Y, 입구·간판 정면, 간판은 그림 기호(글자 금지),
불·연기는 빈 오브젝트 `불_자리_NN`/`연기_자리_NN`만, 뒷면 검사 둘 0건, 채당 삼각형 2,500 이하.
🔴 공통 뼈대: 돌 기단 0~0.8 · 벽 0.8~8.8(8×6.2) · 모서리 기둥·띠보 · 정면 박공(용마루 앞뒤 방향, 옆 처마 9.0, 용마루 14.2,
   덮개 윗면 14.85) · 기와 여섯 켜 · 앞 박공 널 · 박공 삼각벽 가운데 팔각 간판(SIGN_Z 10.75).
   1차 옆 박공은 게임 카메라에서 앞 지붕 비탈이 정면을 덮어 간판이 사라졌다.
🔴 지붕 색으로 가게 구분(PM): 모양·높이·켜는 같고 기와 색만 ROOF_TONES대로. 게임 시점 화면의 60%가 지붕이다.
재질 이름: `상점_<가게>_<종류>` — 종류는 SHADERS에 등록된 것(없으면 register_shader로 더한다). 가게마다 재질을 따로
   두는 이유는 텍스처를 그 가게 메시에 맞춰 굽기 때문(공유하면 서로 덮어쓴다). 발광은 종류 이름 끝 `_발광`, 알파 컷 `_잎카드`.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형(sRGB^2.2), 굽기는 DIFFUSE 색만(Metallic 0).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

UNITS = 11.4

# 공통 뼈대 치수(게임 단위)
WX, WY = 4.0, 3.1
PLINTH = 0.8
EAVE_WALL = 8.8
EAVE = 9.0
RIDGE = 14.2
ROOF_T = 0.22
STEP = 0.1
COURSES = 6
ROOF_FRONT, ROOF_BACK = -3.7, 3.5
SLOPE = (RIDGE - EAVE) / WX
LIMIT = 4.5
SIGN_Z = 10.75

# 가게별 지붕 톤(PM 2026-09-13) — (어두운 장, 밝은 장, 이끼 비율, 널지붕인가). 채도는 절제.
ROOF_TONES = {
    "도박소": ((0.2, 0.075, 0.04), (0.32, 0.13, 0.065), 1.0, False),         # 테라코타
    "강화소": ((0.03, 0.037, 0.045), (0.065, 0.075, 0.088), 0.4, False),     # 짙은 슬레이트 회청
    # PM 2차(일곱 채 한 줄): 이웃해도 색상·명도 중 하나 이상이 확실히 갈리게 두 쌍을 벌림
    #   영원강화소 ↔ 도움소: 둘 다 청록~초록이었다 → 푸른 기 강한 밝은 청록 동판 / 노란 기 올리브~쑥색 유약
    #   공격타입강화소 ↔ 강화소: 검은 쇠빛이 회청 슬레이트와 겹쳤다 → 녹슨 적갈 흑 쇠기와
    "영원강화소": ((0.03, 0.13, 0.17), (0.07, 0.26, 0.32), 0.5, False),       # 푸른 기 강한 밝은 청록 동판(채도 한 칸 절제)
    "공격타입강화소": ((0.035, 0.017, 0.012), (0.095, 0.045, 0.03), 0.2, False),  # 녹슨 적갈 흑 쇠기와
    "도움소": ((0.065, 0.075, 0.022), (0.15, 0.17, 0.055), 0.3, False),       # 노란 기 올리브~쑥색 유약
    # 짙은 갈색 널지붕 — 1차 (0.06~0.12)는 바랜 널벽과 명도가 같아 게임 시점에서 갈색 한 덩어리였다(PM 검수)
    # 3차: 공격타입 적갈 흑 쇠기와와 명도·색상이 겹쳐(밝은 장 휘도 0.047 vs 0.052) 햇볕에 바랜 회갈 널로 올림 —
    #      바랜 널벽(휘도 약 0.19)보다는 여전히 절반쯤 어둡고, 강화소 회청 슬레이트와는 따뜻한 쪽으로 갈린다
    "해적단퀘스트": ((0.055, 0.047, 0.038), (0.12, 0.105, 0.085), 0.6, True),
    "항해일지": ((0.018, 0.028, 0.075), (0.04, 0.058, 0.13), 0.5, False),     # 남색 유약 기와
    # 업적판(PM 2026-09-13): 짙은 가지색·검붉은 자주 유약. 🔴 1차 (0.06,0.018,0.065)~(0.17,0.06,0.18)는 게임 시점에서 선명한
    # 보라로 튀어 절제된 일곱 지붕과 어긋났다 — 휘도는 그대로(어두운 장 0.03·밝은 장 0.09) 채도를 약 절반으로, 붉은 기를 더해
    # 항해일지 남색(푸름)·공격타입 적갈 흑(더 어둡고 누렇다)과 갈린다
    "업적판": ((0.05, 0.024, 0.036), (0.14, 0.072, 0.11), 0.25, False),
}


# ──────────────────────────────────────────────────────────── 짓는 도구

class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []
        self.markers = []

    def m(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _face(self, verts, mat):
        f = self.bm.faces.new(verts)
        f.material_index = self.m(mat)
        return f

    def box(self, x0, x1, y0, y1, z0, z1, mat, skip=()):
        """축 정렬 상자. skip: "bottom"/"top"/"-y"/"+y"/"-x"/"+x" — 벽에 붙는 면만 뺀다(보이는 면은 빼지 말 것)."""
        x0, x1 = sorted((x0, x1))
        y0, y1 = sorted((y0, y1))
        z0, z1 = sorted((z0, z1))
        c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        v = [self.bm.verts.new(p) for p in c]
        index = {"bottom": (0, 3, 2, 1), "top": (4, 5, 6, 7), "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6),
                 "-x": (3, 0, 4, 7), "+x": (1, 2, 6, 5)}
        for name, ids in index.items():
            if name not in skip:
                self._face([v[i] for i in ids], mat)

    def box_c(self, cx, cy, w, d, z0, h, mat, skip=()):
        self.box(cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, z0, z0 + h, mat, skip)

    def extrude(self, points, vec, mat, caps=(True, True), matrix=None):
        """평면 다각형(3D 점)을 vec만큼 밀어낸 기둥 — 감김은 알아서 바깥을 보게 맞춘다."""
        pts = [Vector(p) for p in points]
        vec = Vector(vec)
        if matrix is not None:
            pts = [matrix @ p for p in pts]
            vec = matrix.to_3x3() @ vec
        normal = Vector()
        for i, p in enumerate(pts):
            q = pts[(i + 1) % len(pts)]
            normal += Vector(((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y)))
        if normal.dot(vec) > 0:
            pts.reverse()
        base = [self.bm.verts.new(p) for p in pts]
        top = [self.bm.verts.new(p + vec) for p in pts]
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            self._face((base[j], base[i], top[i], top[j]), mat)
        if caps[0]:
            self._face(base, mat)
        if caps[1]:
            self._face(list(reversed(top)), mat)

    def cylinder(self, center, axis, radius, length, mat, sides=8, caps=(True, True), phase=0.0):
        axis = Vector(axis).normalized()
        u = axis.orthogonal().normalized()
        w = axis.cross(u)
        ring = [Vector(center) + (u * math.cos(phase + 2 * math.pi * i / sides) + w * math.sin(phase + 2 * math.pi * i / sides)) * radius
                for i in range(sides)]
        self.extrude(ring, axis * length, mat, caps)

    def spike(self, base, apex, mat):
        """밑면 다각형에서 한 점으로 모이는 뾰족 기둥 — 옆면만(밑면은 몸통에 묻힌다)."""
        pts = [Vector(p) for p in base]
        apex = Vector(apex)
        center = sum(pts, Vector()) / len(pts)
        ring = [self.bm.verts.new(p) for p in pts]
        tip = self.bm.verts.new(apex)
        for i in range(len(ring)):
            a, c = ring[i], ring[(i + 1) % len(ring)]
            normal = (c.co - a.co).cross(apex - a.co)
            mid = (a.co + c.co + apex) / 3
            if normal.dot(mid - (center + apex) / 2) < 0:
                a, c = c, a
            self._face((a, c, tip), mat)

    def marker(self, name, at):
        """파티클 자리 빈 오브젝트 — 이름은 `불_자리_NN` / `연기_자리_NN`."""
        self.markers.append((name, Vector(at)))


def roof_z(x):
    """지붕 비탈 아랫면 높이(벽 선 x=±WX에서 EAVE, 가운데 RIDGE)."""
    return RIDGE - SLOPE * abs(x)


def frame(b, wall, timber, stone, roof):
    """세트 공통 뼈대 — 기단·벽·모서리 기둥·띠보·박공 다락·기와 여섯 켜·용마루 덮개·앞 박공 널. 가게 스크립트에서 고치지 말 것."""
    b.box(-WX - 0.25, WX + 0.25, -WY - 0.25, WY + 0.25, 0.0, PLINTH, stone, skip=("bottom",))
    b.box(-WX, WX, -WY, WY, PLINTH, EAVE_WALL, wall, skip=("bottom", "top"))
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box_c(sx * (WX - 0.1), sy * (WY - 0.1), 0.55, 0.55, PLINTH, EAVE_WALL - PLINTH, timber, skip=("bottom",))
    b.box(-WX - 0.15, WX + 0.15, -WY - 0.15, -WY + 0.3, EAVE_WALL - 0.35, EAVE_WALL + 0.2, timber)
    b.box(-WX - 0.15, WX + 0.15, WY - 0.3, WY + 0.15, EAVE_WALL - 0.35, EAVE_WALL + 0.2, timber)
    b.box(-WX - 0.15, -WX + 0.3, -WY, WY, EAVE_WALL - 0.35, EAVE_WALL + 0.2, timber)
    b.box(WX - 0.3, WX + 0.15, -WY, WY, EAVE_WALL - 0.35, EAVE_WALL + 0.2, timber)
    b.extrude([(-WX, -WY, EAVE_WALL + 0.2), (WX, -WY, EAVE_WALL + 0.2), (0.0, -WY, RIDGE - 0.1)], (0, 2 * WY, 0), wall)
    # 옆 처마는 바닥 반폭 LIMIT에서 거꾸로 잡는다 — 켜 아랫단 턱이 비탈 법선 방향으로 튀어나오기 때문(±4.78 사고)
    for sx in (-1, 1):
        d = Vector((-sx * 1.0, SLOPE)).normalized()
        n = Vector((-d.y, d.x)) if sx < 0 else Vector((d.y, -d.x))
        ex = LIMIT - 0.01 - abs(n.x) * (ROOF_T + STEP)
        eave = Vector((sx * ex, roof_z(ex)))
        ridge = Vector((0.0, RIDGE))
        length = (ridge - eave).length
        for i in range(COURSES):
            p0 = eave + d * (length * i / COURSES)
            p1 = eave + d * (length * (i + 1) / COURSES + 0.1)
            section = [p0 - n * 0.02, p1, p1 + n * ROOF_T, p0 + n * (ROOF_T + STEP)]
            b.extrude([(p.x, ROOF_FRONT, p.y) for p in section], (0, ROOF_BACK - ROOF_FRONT, 0), roof)
        board = [eave - n * 0.35, ridge - n * 0.35, ridge, eave]
        b.extrude([(p.x, ROOF_FRONT - 0.08, p.y) for p in board], (0, 0.25, 0), timber)
    b.box(-0.38, 0.38, ROOF_FRONT - 0.05, ROOF_BACK + 0.05, RIDGE + 0.2, RIDGE + 0.65, roof)
    b.box_c(0.0, ROOF_FRONT - 0.1, 0.3, 0.3, RIDGE - 0.6, 1.35, timber)


def roof_surface(x_frac, y):
    """지붕 윗면 위의 한 점(x_frac: −1 왼 처마 ~ 0 용마루 ~ 1 오른 처마, y: 앞뒤) — 지붕 위 소품(덧댄 천 등) 자리 잡기용."""
    ex = LIMIT - 0.3
    x = x_frac * ex
    return Vector((x, y, roof_z(abs(x)) + ROOF_T + STEP * 0.5))


def window(b, cx, z0, w, h, timber, glass, shutter=None, y=None):
    """정면(−Y) 창 — 돌출 창틀 + 짙은 유리판 + (선택) 벽에 붙여 연 덧문 두 짝 + 창턱."""
    y = -WY if y is None else y
    b.box(cx - w / 2, cx + w / 2, y - 0.08, y, z0, z0 + h, glass, skip=("+y",))
    t = 0.22
    b.box(cx - w / 2 - t, cx - w / 2, y - 0.2, y, z0, z0 + h, timber, skip=("+y",))
    b.box(cx + w / 2, cx + w / 2 + t, y - 0.2, y, z0, z0 + h, timber, skip=("+y",))
    b.box(cx - w / 2 - t, cx + w / 2 + t, y - 0.2, y, z0 + h, z0 + h + t, timber, skip=("+y",))
    b.box(cx - w / 2 - 0.35, cx + w / 2 + 0.35, y - 0.45, y, z0 - 0.25, z0, timber, skip=("+y",))
    b.box(cx - w / 2 + 0.02, cx + w / 2 - 0.02, y - 0.12, y - 0.08, z0 + h / 2 - 0.06, z0 + h / 2 + 0.06, timber, skip=("+y",))
    b.box(cx - 0.05, cx + 0.05, y - 0.12, y - 0.08, z0, z0 + h, timber, skip=("+y",))
    if shutter:
        for s in (-1, 1):
            x0 = cx + s * (w / 2 + t + 0.05)
            b.box(x0, x0 + s * w / 2, y - 0.14, y, z0, z0 + h, shutter, skip=("+y",))


def gable_sign(b, board, face=None):
    """박공 삼각벽 가운데 팔각 간판 — 나무 테 판(+ 선택: 앞에 색 판). 그림 기호를 붙일 앞면 y를 돌려준다.
    🔴 게임 시점에서 짙은 판 위 짙은 기호는 안 읽힌다(강화소 2차) — 판과 기호는 밝기 차이를 크게."""
    y = -WY
    b.cylinder((0.0, y, SIGN_Z), (0, -1, 0), 1.3, 0.25, board, sides=8, caps=(False, True), phase=math.pi / 8)
    if face:
        b.cylinder((0.0, y - 0.25, SIGN_Z), (0, -1, 0), 1.12, 0.06, face, sides=8, caps=(False, True), phase=math.pi / 8)
        return y - 0.31
    return y - 0.25


# ──────────────────────────────────────────────────────────── 셰이더

def _node(nt, kind, **inputs):
    n = nt.nodes.new(kind)
    for key, value in inputs.items():
        n.inputs[key].default_value = value
    return n


def _link(nt, a, b):
    nt.links.new(a, b)


def _ramp(nt, fac, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    els[0].position, els[0].color = stops[0]
    els[1].position, els[1].color = stops[-1]
    for pos, col in stops[1:-1]:
        e = els.new(pos)
        e.color = col
    _link(nt, fac, r.inputs[0])
    return r.outputs["Color"]


def _mix(nt, fac, a, b, blend="MIX"):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    m.blend_type = blend
    if isinstance(fac, (int, float)):
        m.inputs["Factor"].default_value = fac
    else:
        _link(nt, fac, m.inputs["Factor"])
    for sock, val in ((m.inputs[6], a), (m.inputs[7], b)):
        if isinstance(val, tuple):
            sock.default_value = val if len(val) == 4 else (*val, 1.0)
        else:
            _link(nt, val, sock)
    return m.outputs[2]


def _math(nt, op, a, b):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    for sock, val in ((m.inputs[0], a), (m.inputs[1], b)):
        if isinstance(val, (int, float)):
            sock.default_value = val
        else:
            _link(nt, val, sock)
    return m.outputs[0]


def _noise(nt, vec, scale, detail=5.0, rough=0.6):
    n = _node(nt, "ShaderNodeTexNoise", Scale=scale, Detail=detail, Roughness=rough)
    _link(nt, vec, n.inputs["Vector"])
    return n.outputs["Fac"]


def _bricks(nt, vec, scale, mortar, c1, c2, row=0.5, width=0.5):
    br = _node(nt, "ShaderNodeTexBrick", Scale=scale, **{"Mortar Size": mortar, "Brick Width": width, "Row Height": row})
    br.inputs["Color1"].default_value, br.inputs["Color2"].default_value = c1, c2
    _link(nt, vec, br.inputs["Vector"])
    return br


class Ctx:
    """셰이더 함수에 넘기는 좌표 묶음 — co(물체 좌표 m), wall((x+y, z) 벽 펼침), low(발치일수록 1), sep(x·y·z 분리)."""

    def __init__(self, nt):
        tc = nt.nodes.new("ShaderNodeTexCoord")
        self.co = tc.outputs["Object"]
        self.uv = tc.outputs["UV"]
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        _link(nt, self.co, sep.inputs[0])
        self.sep = sep
        along = _math(nt, "ADD", sep.outputs[0], sep.outputs[1])
        wall = nt.nodes.new("ShaderNodeCombineXYZ")
        _link(nt, along, wall.inputs[0])
        _link(nt, sep.outputs[2], wall.inputs[1])
        self.wall = wall.outputs[0]
        low = nt.nodes.new("ShaderNodeMapRange")
        _link(nt, sep.outputs[2], low.inputs["Value"])
        low.inputs["From Min"].default_value, low.inputs["From Max"].default_value = 0.25, 0.07
        self.low = low.outputs["Result"]


SHADERS = {}          # 종류 → fn(nt, ctx, 재질이름) -> (색 소켓, 발광 소켓 또는 None)
BIG_KINDS = {"회벽", "돌벽", "지붕", "목재", "석재"}   # 1024 텍스처(나머지 512)


def register_shader(kind, big=False):
    """가게 스크립트에서 새 재질 종류를 더할 때 — @register_shader("결정_발광") def _(nt, c, name): return color, emit"""
    def deco(fn):
        SHADERS[kind] = fn
        if big:
            BIG_KINDS.add(kind)
        return fn
    return deco


@register_shader("회벽", big=True)
def _plaster(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 7.0), (0.34, 0.24, 0.13, 1), (0.46, 0.34, 0.19, 1))
    chip = _ramp(nt, _noise(nt, c.co, 24.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.64, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
    color = _mix(nt, chip, base, (0.16, 0.13, 0.1, 1))
    return _mix(nt, _math(nt, "MULTIPLY", c.low, 0.7), color, (0.1, 0.075, 0.05, 1)), None


@register_shader("흰회벽", big=True)
def _white_plaster(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 7.0), (0.45, 0.43, 0.39, 1), (0.6, 0.58, 0.53, 1))
    chip = _ramp(nt, _noise(nt, c.co, 24.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.64, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
    color = _mix(nt, chip, base, (0.2, 0.18, 0.15, 1))
    return _mix(nt, _math(nt, "MULTIPLY", c.low, 0.7), color, (0.12, 0.1, 0.07, 1)), None


@register_shader("돌벽", big=True)
def _rubble(nt, c, name):
    # 막돌 쌓기 — 줄눈을 노이즈로 흔들고 크기 다른 돌 두 벌을 큰 얼룩으로 섞는다(반듯한 블록 방지)
    wobble = _node(nt, "ShaderNodeTexNoise", Scale=18.0, Detail=2.0)
    _link(nt, c.co, wobble.inputs["Vector"])
    shift = nt.nodes.new("ShaderNodeVectorMath")
    shift.operation = "SCALE"
    _link(nt, wobble.outputs["Color"], shift.inputs[0])
    shift.inputs["Scale"].default_value = 0.012
    warped = nt.nodes.new("ShaderNodeVectorMath")
    warped.operation = "ADD"
    _link(nt, c.wall, warped.inputs[0])
    _link(nt, shift.outputs[0], warped.inputs[1])
    big = _bricks(nt, warped.outputs[0], 6.0, 0.014, (0.12, 0.11, 0.1, 1), (0.21, 0.19, 0.17, 1), row=0.5, width=0.9)
    small = _bricks(nt, warped.outputs[0], 11.0, 0.02, (0.15, 0.14, 0.125, 1), (0.1, 0.095, 0.085, 1), row=0.4, width=0.6)
    for br in (big, small):
        br.inputs["Mortar"].default_value = (0.045, 0.042, 0.038, 1)
        br.inputs["Bias"].default_value = 0.4
    patch = _ramp(nt, _noise(nt, c.co, 3.0, detail=2.0), [(0.0, (0, 0, 0, 1)), (0.45, (0, 0, 0, 1)), (0.55, (1, 1, 1, 1))])
    stones = _mix(nt, patch, big.outputs["Color"], small.outputs["Color"])
    color = _mix(nt, _math(nt, "MULTIPLY", _noise(nt, c.co, 40.0), 0.5), stones, (0.06, 0.055, 0.05, 1))
    soot = _ramp(nt, _noise(nt, c.co, 5.0), [(0.0, (0, 0, 0, 1)), (0.55, (0, 0, 0, 1)), (0.75, (0.7, 0.7, 0.7, 1))])
    return _mix(nt, soot, color, (0.02, 0.018, 0.016, 1)), None


@register_shader("석재", big=True)
def _plinth(nt, c, name):
    br = _bricks(nt, c.wall, 5.0, 0.015, (0.16, 0.155, 0.145, 1), (0.24, 0.23, 0.21, 1), row=0.5, width=1.0)
    br.inputs["Mortar"].default_value = (0.07, 0.066, 0.06, 1)
    color = _mix(nt, _math(nt, "MULTIPLY", _noise(nt, c.co, 30.0), 0.4), br.outputs["Color"], (0.08, 0.075, 0.07, 1))
    return _mix(nt, _math(nt, "MULTIPLY", c.low, 0.5), color, (0.07, 0.06, 0.045, 1)), None


def _grain(nt, c):
    stretch = _node(nt, "ShaderNodeMapping")
    stretch.inputs["Scale"].default_value = (1.0, 1.0, 0.12)
    _link(nt, c.co, stretch.inputs["Vector"])
    return _noise(nt, stretch.outputs["Vector"], 60.0, detail=6.0)


@register_shader("목재", big=True)
def _timber(nt, c, name):
    return _mix(nt, _grain(nt, c), (0.035, 0.022, 0.013, 1), (0.09, 0.058, 0.034, 1)), None


@register_shader("붉은목재", big=True)
def _red_timber(nt, c, name):
    paint = _mix(nt, _grain(nt, c), (0.22, 0.03, 0.02, 1), (0.34, 0.06, 0.03, 1))
    worn = _ramp(nt, _noise(nt, c.co, 18.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.6, (0, 0, 0, 1)), (0.68, (1, 1, 1, 1))])
    return _mix(nt, worn, paint, (0.06, 0.035, 0.02, 1)), None


@register_shader("밝은목재")
def _light_timber(nt, c, name):
    """간판 판 — 바랜 밝은 나무(짙은 쇠 기호가 또렷하게)."""
    return _mix(nt, _grain(nt, c), (0.28, 0.2, 0.12, 1), (0.45, 0.34, 0.21, 1)), None


@register_shader("지붕", big=True)
def _roof(nt, c, name):
    shop = name.split("_")[1]
    dark, light, moss_amt, plank = ROOF_TONES.get(shop, ROOF_TONES["도박소"])
    wave = _node(nt, "ShaderNodeTexWave", Scale=6.0 if plank else 14.0, Distortion=4.0 if plank else 1.5, Detail=2.0)
    wave.wave_type, wave.bands_direction = "BANDS", "Y"
    _link(nt, c.co, wave.inputs["Vector"])
    cell = _node(nt, "ShaderNodeTexVoronoi", Scale=40.0)
    _link(nt, c.co, cell.inputs["Vector"])
    tile = _mix(nt, cell.outputs["Distance"], (*dark, 1), (*light, 1))
    groove = _ramp(nt, wave.outputs["Fac"], [(0.0, (0, 0, 0, 1)), (0.18, (0, 0, 0, 1)), (0.3, (1, 1, 1, 1))])
    color = _mix(nt, groove, tuple(v * 0.35 for v in dark) + (1,), tile)
    moss = _ramp(nt, _noise(nt, c.co, 6.0, detail=6.0),
                 [(0.0, (0, 0, 0, 1)), (0.6, (0, 0, 0, 1)), (0.72, (0.55 * moss_amt, 0.55 * moss_amt, 0.55 * moss_amt, 1))])
    return _mix(nt, moss, color, (0.045, 0.06, 0.03, 1)), None


@register_shader("벽돌")
def _brick(nt, c, name):
    br = _bricks(nt, c.wall, 16.0, 0.02, (0.2, 0.07, 0.04, 1), (0.28, 0.1, 0.055, 1))
    br.inputs["Mortar"].default_value = (0.1, 0.09, 0.08, 1)
    up = nt.nodes.new("ShaderNodeMapRange")
    _link(nt, c.sep.outputs[2], up.inputs["Value"])
    up.inputs["From Min"].default_value, up.inputs["From Max"].default_value = 0.9, 1.4
    soot = _math(nt, "MAXIMUM", up.outputs["Result"], _math(nt, "MULTIPLY", _noise(nt, c.co, 8.0), 0.5))
    return _mix(nt, soot, br.outputs["Color"], (0.018, 0.015, 0.013, 1)), None


@register_shader("쇠")
def _iron(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 30.0), (0.02, 0.02, 0.022, 1), (0.06, 0.06, 0.065, 1))
    rust = _ramp(nt, _noise(nt, c.co, 20.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.64, (0, 0, 0, 1)), (0.74, (1, 1, 1, 1))])
    return _mix(nt, rust, base, (0.12, 0.045, 0.015, 1)), None


@register_shader("천")
def _cloth(nt, c, name):
    wave = _node(nt, "ShaderNodeTexWave", Scale=30.0, Distortion=2.0)
    wave.wave_type, wave.bands_direction = "BANDS", "X"
    _link(nt, c.co, wave.inputs["Vector"])
    return _mix(nt, wave.outputs["Fac"], (0.16, 0.012, 0.01, 1), (0.36, 0.03, 0.02, 1)), None


@register_shader("돛천")
def _sailcloth(nt, c, name):
    stain = _noise(nt, c.co, 9.0, detail=6.0)
    return _mix(nt, stain, (0.32, 0.29, 0.22, 1), (0.5, 0.46, 0.36, 1)), None


@register_shader("등_발광")
def _lantern(nt, c, name):
    wave = _node(nt, "ShaderNodeTexWave", Scale=60.0, Distortion=0.3)
    wave.wave_type, wave.bands_direction = "BANDS", "X"
    _link(nt, c.co, wave.inputs["Vector"])
    rib = _ramp(nt, wave.outputs["Fac"], [(0.0, (0, 0, 0, 1)), (0.12, (0, 0, 0, 1)), (0.25, (1, 1, 1, 1))])
    body = _mix(nt, _noise(nt, c.co, 20.0), (0.55, 0.035, 0.015, 1), (0.8, 0.14, 0.03, 1))
    color = _mix(nt, rib, (0.12, 0.01, 0.005, 1), body)
    return color, color


@register_shader("주사위")
def _dice(nt, c, name):
    return _mix(nt, _noise(nt, c.co, 25.0), (0.55, 0.5, 0.4, 1), (0.72, 0.68, 0.58, 1)), None


@register_shader("검정")
def _black(nt, c, name):
    return _mix(nt, _noise(nt, c.co, 40.0), (0.012, 0.011, 0.01, 1), (0.03, 0.028, 0.026, 1)), None


@register_shader("어둠")
def _dark(nt, c, name):
    return _mix(nt, _noise(nt, c.co, 6.0), (0.018, 0.016, 0.015, 1), (0.04, 0.045, 0.05, 1)), None


@register_shader("화덕_발광")
def _forge(nt, c, name):
    up = nt.nodes.new("ShaderNodeMapRange")
    _link(nt, c.sep.outputs[2], up.inputs["Value"])
    up.inputs["From Min"].default_value, up.inputs["From Max"].default_value = 0.13, 0.36
    heat = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", 1.0, up.outputs["Result"]), _math(nt, "ADD", 0.5, _noise(nt, c.co, 25.0)))
    color = _ramp(nt, heat, [(0.0, (0.03, 0.006, 0.003, 1)), (0.35, (0.4, 0.06, 0.01, 1)),
                             (0.7, (0.9, 0.3, 0.04, 1)), (1.0, (1.0, 0.6, 0.15, 1))])
    return color, color


@register_shader("가죽")
def _leather(nt, c, name):
    return _mix(nt, _noise(nt, c.co, 35.0), (0.05, 0.028, 0.015, 1), (0.12, 0.07, 0.035, 1)), None


@register_shader("황동")
def _brass(nt, c, name):
    base = _mix(nt, _noise(nt, c.co, 12.0), (0.28, 0.18, 0.05, 1), (0.5, 0.36, 0.11, 1))
    grime = _ramp(nt, _noise(nt, c.co, 30.0, detail=6.0), [(0.0, (0, 0, 0, 1)), (0.5, (0, 0, 0, 1)), (0.66, (0.8, 0.8, 0.8, 1))])
    return _mix(nt, grime, base, (0.07, 0.05, 0.025, 1)), None


def shade_material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    kind = name.split("_", 2)[2]
    if kind not in SHADERS:
        raise ValueError(f"셰이더 종류가 없다: {name} — register_shader('{kind}')로 더할 것")
    color, emit = SHADERS[kind](nt, Ctx(nt), name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.85
    _link(nt, color, bsdf.inputs["Base Color"])
    if emit is not None:
        _link(nt, emit, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 2.0
    return mat


def tex_size(name):
    return 1024 if name.split("_", 2)[2] in BIG_KINDS else 512


# ──────────────────────────────────────────────────────────── 조립·굽기·내보내기

def _live_objects():
    bpy.context.view_layer.update()
    return [o for o in bpy.context.view_layer.objects if o is not None]


def _select_only(obj):
    for o in _live_objects():
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def assemble(name, maker, collection):
    b = maker()
    bm = b.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    assert min(xs) >= -LIMIT - 1e-3 and max(xs) <= LIMIT + 1e-3 and min(ys) >= -LIMIT - 1e-3 and max(ys) <= LIMIT + 1e-3, \
        f"{name} 바닥 9×9를 넘었다: x {min(xs):.2f}~{max(xs):.2f}, y {min(ys):.2f}~{max(ys):.2f}"
    assert abs(min(zs)) < 1e-3 and 12.0 <= max(zs) <= 16.0, f"{name} 높이 규격 밖: {min(zs):.2f}~{max(zs):.2f}"
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    assert tri_count <= 2500, f"{name} 삼각형 {tri_count} > 2,500"
    for mat_name in b.mats:
        assert mat_name.startswith(name + "_"), f"재질 이름은 '{name}_<종류>'여야 한다: {mat_name}"
    for marker, _ in b.markers:
        # 불·연기 = 파티클, 빛 = 빛 입자(포탈), 트로피 = 업적판 진열장 칸(유니티가 업적 달성 시 트로피를 세움, PM 2026-09-13)
        assert marker.split("_")[0] in {"불", "연기", "빛", "트로피"} and "_자리_" in marker, f"빈 오브젝트 이름 규칙 밖: {marker}"
    bounds = (min(xs), max(xs), min(ys), max(ys), max(zs))
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for mat_name in b.mats:
        mesh.materials.append(shade_material(mat_name))
    for marker, at in b.markers:
        empty = bpy.data.objects.new(marker, None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = 0.1
        empty.location = at / UNITS
        empty.parent = obj
        collection.objects.link(empty)
    return obj, [m for m, _ in b.markers], bounds


def unwrap(obj):
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(obj.data)
    for index in range(len(obj.material_slots)):
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
    scene.cycles.samples = 8
    try:
        _select_only(obj)
        images = {}
        for slot in obj.material_slots:
            mat = slot.material
            old = bpy.data.images.get(mat.name)
            if old is not None:
                bpy.data.images.remove(old)
            size = tex_size(mat.name)
            img = bpy.data.images.new(mat.name, size, size)
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = img
            for n in mat.node_tree.nodes:
                n.select = False
            node.select = True
            mat.node_tree.nodes.active = node
            images[mat.name] = img
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=False, margin=6)
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
            bsdf = nt.nodes["Principled BSDF"]
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            if "_발광" in name:
                nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
                bsdf.inputs["Emission Strength"].default_value = 1.5
            else:
                bsdf.inputs["Emission Strength"].default_value = 0.0
    finally:
        scene.render.engine, scene.cycles.samples = saved
        for o in _live_objects():
            o.select_set(o in selected)
        view.objects.active = active


def build_in_window(name, maker, collection, folder):
    """사장님 창에 짓기만(셰이더·메시·UV·굽기) — 내보내지 않는다. folder는 정본 Textures가 아닌 창 전용 폴더."""
    obj, markers, bounds = assemble(name, maker, collection)
    unwrap(obj)
    bake(obj, folder)
    return obj


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def run(shops, out_dir):
    """화면 없이 정본 FBX·텍스처 — `-- 이름 ...`으로 일부만. out_dir/<이름>.fbx, out_dir/Textures/<재질>.png"""
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(shops)
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        obj, markers, bounds = assemble(name, shops[name], col)
        unwrap(obj)
        bake(obj, os.path.join(out_dir, "Textures"))
        names = sorted(c.name for c in obj.children)
        assert names == sorted(markers), f"빈 오브젝트 이름이 밀렸다: {names}"
        for o in _live_objects():
            o.select_set(o is obj or o.parent is obj)
        bpy.context.view_layer.objects.active = obj
        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {name}  삼각형 {tris(obj)}  재질 {len(obj.material_slots)}  빈 오브젝트 {names}  "
              f"x {bounds[0]:.2f}~{bounds[1]:.2f} y {bounds[2]:.2f}~{bounds[3]:.2f} z ~{bounds[4]:.2f}  → {path}")
