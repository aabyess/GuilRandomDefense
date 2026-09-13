"""정의문 — 강철 두 짝 문(PM 승인 2026-09-13). 기존 Walls/정의문.fbx(판 한 장 + 납작한 띠) 교체용.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 파일명 `Walls/정의문.fbx` 유지로 교체, 스크립트는 저장소로 옮김.

화면 없이(정본):  blender --background --factory-startup --python gen_justice_gate.py
사장님 창(보여 주기만): ns = {"__name__": "gen_justice_gate", "__file__": 경로}; exec(...); ns["build_in_window"](컬렉션)

규격(PM): 20.6×7×1.4(가로×높이×앞뒤), 원점 발밑 가운데, 앞 −Y. 삼각형 2,500 이하, 뒷면 검사 둘 0건.
- 돌기둥은 넣지 않는다 — 맵 생성기가 문 양옆에 `펑크해저드_문기둥`을 따로 세우고, 부서질 때
  `DestructibleGate.Break`는 문 오브젝트만 움직인다. 폭 20.6을 문짝·윗틀로 꽉 채운다.
- 오브젝트: 부모 `정의문`(윗 문틀) + 자식 `정의문_왼짝`·`정의문_오른짝`. 짝의 원점은 각자 바깥 경첩 쪽 발밑
  (x = ∓10.3) — 부서짐 판에서 짝이 경첩 기준으로 돌거나 빠지기 쉽게. 월드 치수는 그대로.
- 재질은 기존 벽 조각과 겹치지 않는 새 이름(정의문_강철·정의문_쇠띠·정의문_장식) — `금속_어두움` 같은
  공유 텍스처를 덮어쓰지 않게.
- 한 메시로 짓고 재질마다 UV를 펴서 구운 뒤 세 오브젝트로 나눈다(두 짝이 같은 이미지를 겹치지 않게 나눠 쓴다).
게임 단위로 짓고 1/11.4로 줄여 m로 둔다. 셰이더 색은 선형값, 굽기는 DIFFUSE 색만(Metallic 0).
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

UNITS = 11.4
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Walls")            # 복구 뒤: Assets/Art/Walls (파일명 정의문.fbx)
TEX = os.path.join(OUT, "Textures")
TEX_SIZE = 1024

MATS = ["정의문_강철", "정의문_쇠띠", "정의문_장식"]
STEEL, IRON, BRASS = range(3)
PARTS = [("정의문", 0.0), ("정의문_왼짝", -10.3), ("정의문_오른짝", 10.3)]   # (이름, 원점 x)
LINTEL, LEFT, RIGHT = range(3)

HALF_W = 10.3
LEAF_TOP = 6.25
LINTEL_Z = 6.3
TOP = 7.0
GAP = 0.06                                         # 두 짝 사이 반틈
# 3차(PM 검수): 「正義」를 높이 약 2.4로 키우려고 아래 쇠띠를 내리고 가운데 쇠띠·빗장을 올렸다.
# 방패는 1.3배로 키워 윗 쇠띠 앞에 걸친다.
BANDS = ((0.25, 0.8), (3.2, 3.8), (5.6, 6.15))
# 굵은 고딕 — 명조(2차)는 게임 시점에서 義의 가는 획이 뭉개졌다. 윤곽을 offset으로 불려 획 두께 약 0.24
FONTS = ["/System/Library/Fonts/AppleSDGothicNeo.ttc", "/System/Library/Fonts/ヒラギノ明朝 ProN.ttc"]


class Gate:
    def __init__(self):
        self.bm = bmesh.new()
        self.part = self.bm.faces.layers.int.new("part")

    def face(self, pts, mat, part):
        f = self.bm.faces.new([self.bm.verts.new(p) for p in pts])
        f.material_index = mat
        f[self.part] = part
        return f

    def box(self, x0, x1, y0, y1, z0, z1, mat, part, skip=()):
        c = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        verts = [self.bm.verts.new(p) for p in c]
        index = {"bottom": (0, 3, 2, 1), "top": (4, 5, 6, 7), "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6),
                 "-x": (3, 0, 4, 7), "+x": (1, 2, 6, 5)}
        for name, ids in index.items():
            if name not in skip:
                f = self.bm.faces.new([verts[i] for i in ids])
                f.material_index = mat
                f[self.part] = part

    def rivet(self, x, y, z, side, part, s=0.12, h=0.09):
        """징 — 밑변 사각 피라미드(밑면 없음). side −1 = 앞(−Y) 면 위, +1 = 뒤(+Y) 면 위."""
        base = [Vector((x - s, y, z - s)), Vector((x + s, y, z - s)), Vector((x + s, y, z + s)), Vector((x - s, y, z + s))]
        if side > 0:
            base.reverse()
        apex = self.bm.verts.new((x, y + side * h, z))
        ring = [self.bm.verts.new(p) for p in base]
        for i in range(4):
            f = self.bm.faces.new((ring[i], ring[(i + 1) % 4], apex))
            f.material_index = IRON
            f[self.part] = part

    def knuckle(self, cx, cy, z0, z1, r, part, sides=8):
        """경첩 축 — 세운 8각 기둥(뚜껑 둘)."""
        lo = [self.bm.verts.new((cx + math.cos(2 * math.pi * i / sides) * r, cy + math.sin(2 * math.pi * i / sides) * r, z0))
              for i in range(sides)]
        hi = [self.bm.verts.new((v.co.x, v.co.y, z1)) for v in lo]
        faces = [self.bm.faces.new((lo[i], lo[(i + 1) % sides], hi[(i + 1) % sides], hi[i])) for i in range(sides)]
        faces.append(self.bm.faces.new(hi))
        faces.append(self.bm.faces.new(list(reversed(lo))))
        for f in faces:
            f.material_index = IRON
            f[self.part] = part

    def plate(self, outline, y_front, y_back, mat, part):
        """앞(−Y)에서 봐서 반시계인 (x, z) 윤곽을 y_front~y_back 두께로 — 앞면 + 옆면(뒷면은 벽에 묻힌다)."""
        front = [self.bm.verts.new((x, y_front, z)) for x, z in outline]
        back = [self.bm.verts.new((x, y_back, z)) for x, z in outline]
        faces = [self.bm.faces.new(front)]
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((front[i], back[i], back[j], front[j])))
        for f in faces:
            f.material_index = mat
            f[self.part] = part

    def text(self, body, x, z, size, y_face, depth, part, offset=0.0):
        """돋을새김 글자 — 앞(−Y)을 보게 세운 글자 메시. 뒷면은 문짝에 묻힌다. offset > 0이면 획을 굵힌다."""
        font = None
        for path in FONTS:
            if os.path.exists(path):
                font = bpy.data.fonts.load(path, check_existing=True)
                break
        cu = bpy.data.curves.new("_글자", "FONT")
        cu.body, cu.font, cu.size = body, font, size
        cu.align_x, cu.align_y = "CENTER", "CENTER"
        cu.resolution_u = 2
        cu.offset = offset
        cu.extrude = depth / 2
        ob = bpy.data.objects.new("_글자", cu)
        bpy.context.scene.collection.objects.link(ob)
        ob.rotation_euler = (math.radians(90), 0, 0)
        ob.location = (x, y_face - depth / 2, z)
        bpy.context.view_layer.update()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        matrix = ob.matrix_world.copy()
        bpy.data.objects.remove(ob)
        bpy.data.curves.remove(cu)
        tmp = bmesh.new()
        tmp.from_mesh(me)
        bpy.data.meshes.remove(me)
        tmp.transform(matrix)
        mapping = {v: self.bm.verts.new(v.co) for v in tmp.verts}
        for f in tmp.faces:
            # 뒤(+Y)를 보는 면은 문짝에 묻혀 안 보인다 — 삼각형을 아낀다
            if f.normal.y > 0.9:
                continue
            nf = self.bm.faces.new([mapping[v] for v in f.verts])
            nf.material_index = BRASS
            nf[self.part] = part
        tmp.free()


def build_leaf(g, side):
    """side −1 = 왼짝(x −10.3 ~ −0.06), +1 = 오른짝."""
    part = LEFT if side < 0 else RIGHT
    inner, outer = side * GAP, side * HALF_W
    x0, x1 = min(inner, outer), max(inner, outer)
    g.box(x0, x1, -0.3, 0.3, 0.0, LEAF_TOP, STEEL, part, skip=("bottom",))
    # 테두리 선대(바깥 넓게·안쪽 좁게), 앞뒤
    for y0, y1, skip in ((-0.45, -0.3, ("+y", "bottom")), (0.3, 0.45, ("-y", "bottom"))):
        ox0, ox1 = (x0, x0 + 0.55) if side < 0 else (x1 - 0.55, x1)
        ix0, ix1 = (x1 - 0.35, x1) if side < 0 else (x0, x0 + 0.35)
        g.box(ox0, ox1, y0, y1, 0.0, LEAF_TOP, STEEL, part, skip=skip)
        g.box(ix0, ix1, y0, y1, 0.0, LEAF_TOP, STEEL, part, skip=skip)
    # 가로 쇠띠 3단 — 앞뒤로 0.2 돌출, 징
    for z0, z1 in BANDS:
        g.box(x0, x1, -0.5, -0.3, z0, z1, IRON, part, skip=("+y",))
        g.box(x0, x1, 0.3, 0.5, z0, z1, IRON, part, skip=("-y",))
        zc = (z0 + z1) / 2
        n = 12
        for k in range(n):
            x = x0 + 0.45 + (x1 - x0 - 0.9) * k / (n - 1)
            g.rivet(x, -0.5, zc, -1, part)
            if k % 2 == 0:
                g.rivet(x, 0.5, zc, 1, part)
        # 경첩 축 — 바깥 끝, 쇠띠 높이마다
        # 🔴 1차는 축을 두께 가운데(y 0)에 둬서 선대(±0.45)에 묻혀 안 보였다 — 앞면 선대 위로 내놓는다
        # 🔴 3차에 쇠띠를 옮기자 축이 땅 밑(−0.10)·짝 위(6.5)로 삐져나왔다 — 짝 높이 안으로 자른다
        g.knuckle(side * 10.05, -0.45, max(z0 - 0.35, 0.0), min(z1 + 0.35, LEAF_TOP), 0.25, part)
    # 바깥 선대 징(쇠띠 사이)
    for z in (1.7, 4.4):
        g.rivet(side * 9.95, -0.45, z, -1, part)
        g.rivet(side * 9.95, -0.45, z + 0.5, -1, part)
    # 빗장 — 가운데 쇠띠 위를 가로지르는 두꺼운 막대(앞뒤) + 고리쇠
    bx0, bx1 = (-2.4, -GAP) if side < 0 else (GAP, 2.4)
    g.box(bx0, bx1, -0.7, -0.5, 3.3, 3.7, IRON, part, skip=("+y",))
    g.box(bx0, bx1, 0.5, 0.7, 3.3, 3.7, IRON, part, skip=("-y",))
    sx = side * 2.1
    g.box(sx - 0.2, sx + 0.2, -0.66, -0.5, 3.15, 3.85, IRON, part, skip=("+y",))
    g.box(sx - 0.2, sx + 0.2, 0.5, 0.66, 3.15, 3.85, IRON, part, skip=("-y",))
    # 맞닿는 곳 덮개판 — 오른짝 앞면·왼짝 뒷면에 붙은 세로 판이 두 짝 틈을 덮는다(1차는 틈으로 바닥이 비쳤다)
    # 🔴 안쪽 면(문짝에 붙는 면)도 남긴다 — 틈(GAP) 자리엔 뒤에 문짝이 없어 반대편 카메라가 틈으로 그 면을 본다
    #    (2차 검사: 뒤(+Y)·앞(−Y)에서 덮개판 안쪽 뒷면 넓이 3.5 경고 2건)
    if side > 0:
        g.box(-0.28, 0.28, -0.52, -0.3, 0.0, LEAF_TOP, STEEL, part, skip=("bottom",))
    else:
        g.box(-0.28, 0.28, 0.3, 0.52, 0.0, LEAF_TOP, STEEL, part, skip=("bottom",))
    # 방패 반쪽 — 가운데 쇠띠와 윗 쇠띠 사이, 두 짝에 반씩. 뾰족한 방패꼴 + 짙은 쇠 테(뒤에 조금 큰 판)
    # (1차는 넓고 둥근 밑선이라 반달처럼 보였다)
    # 모양 좌표는 오른짝(+x) 기준, 앞에서 봐서 시계 방향으로 적는다 — 왼짝은 거울상이라 그대로 반시계, 오른짝은 뒤집는다.
    cz, k = 4.3, 1.3
    half = [(0.0, 5.22), (1.45, 5.22), (1.45, 4.25), (1.3, 3.92), (0.95, 3.66), (0.45, 3.5), (0.0, 3.42)]
    center_z = 4.95

    def shape(points, grow=1.0):
        pts = [(side * max(abs(x) * grow, GAP), center_z + (z - cz) * grow) for x, z in points]
        return pts if side < 0 else list(reversed(pts))

    def poly(points, y_front, y_back, mat):
        pts = [(side * max(abs(x), GAP), z) for x, z in points]
        g.plate(pts if side < 0 else list(reversed(pts)), y_front, y_back, mat, part)

    g.plate(shape(half, k * 1.1), -0.54, -0.3, IRON, part)           # 짙은 쇠 테
    g.plate(shape(half, k), -0.62, -0.3, BRASS, part)                 # 금빛 방패(2차의 1.3배)
    # 저울 — 짙은 쇠 돋을새김. 3차: 저울대 끝에서 끈이 V자로 내려가 반원 접시가 확실히 아래에 매달리게
    # (2차는 접시가 저울대에 붙은 짧은 가로선이라 「工/王」처럼 읽혔다)
    y0, y1 = -0.7, -0.62

    def relief(ax0, ax1, z0, z1):
        """가운데를 가로지르는 부재는 짝마다 반씩(틈 GAP은 비운다)."""
        lo, hi = sorted((side * max(abs(ax0), GAP), side * max(abs(ax1), GAP)))
        if hi - lo > 1e-4:
            g.box(lo, hi, y0, y1, z0, z1, IRON, part, skip=("+y",))

    relief(0.0, 0.3, 4.2, 4.3)                # 받침 아랫단
    relief(0.0, 0.14, 4.3, 5.42)              # 기둥
    relief(0.0, 1.36, 5.4, 5.56)              # 저울대
    poly([(0.0, 5.94)] + [(0.2 * math.cos(math.radians(a)), 5.74 + 0.2 * math.sin(math.radians(a)))
                          for a in (60, 30, 0, -30, -60)] + [(0.0, 5.54)], y0, y1, IRON)          # 둥근 꼭지
    pc, pr, chord = 1.28, 0.4, 4.78
    for xb in (pc - pr, pc + pr):                                                                  # V자 끈 두 줄
        w = 0.035
        poly([(pc - w, 5.4), (pc + w, 5.4), (xb + w, chord), (xb - w, chord)], y0, y1, IRON)
    poly([(pc - pr, chord), (pc + pr, chord)] + [(pc + pr * math.cos(math.radians(a)), chord + pr * math.sin(math.radians(a)))
                                               for a in (-30, -60, -90, -120, -150)], y0, y1, IRON)  # 반원 접시
    # 正 / 義 — 아래 쇠띠와 가운데 쇠띠 사이 칸을 채우는 높이 약 2.3, 굵은 고딕
    g.text("正" if side < 0 else "義", side * 1.65, 2.0, 2.55, -0.3, 0.12, part, offset=0.03)


def build_mesh():
    g = Gate()
    # 윗 문틀(부모) — 앞뒤 0.6, 앞면 징 한 줄
    g.box(-HALF_W, HALF_W, -0.6, 0.6, LINTEL_Z, TOP, STEEL, LINTEL, skip=("bottom",))
    for k in range(21):
        g.rivet(-HALF_W + 0.5 + (2 * HALF_W - 1.0) * k / 20, -0.6, 6.65, -1, LINTEL)
    build_leaf(g, -1)
    build_leaf(g, 1)
    bm = g.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    return bm


# ──────────────────────────────────────────────────────────── 셰이더

def _node(nt, kind, x, y, **inputs):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for key, value in inputs.items():
        n.inputs[key].default_value = value
    return n


def _ramp(nt, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    els[0].position, els[0].color = stops[0]
    els[1].position, els[1].color = stops[-1]
    for pos, col in stops[1:-1]:
        e = els.new(pos)
        e.color = col
    return r


def _mix(nt, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    nt.links.new(fac, m.inputs["Factor"])
    for sock, val in ((m.inputs[6], a), (m.inputs[7], b)):
        if isinstance(val, tuple):
            sock.default_value = val
        else:
            nt.links.new(val, sock)
    return m.outputs[2]


def _noise(nt, vector, scale, detail=6.0, rough=0.6):
    n = _node(nt, "ShaderNodeTexNoise", 0, 0, Scale=scale, Detail=detail, Roughness=rough)
    nt.links.new(vector, n.inputs["Vector"])
    return n


def _streaks(nt, co, scale):
    """세로로 흘러내린 줄 — 물체 좌표를 z로 늘인 노이즈를, 굵은 노이즈로 한 번 더 끊어 짧은 줄로.
    (1차는 z 배율 0.07이라 문 높이 전체를 한 줄로 긋는 굵은 줄무늬가 됐다)"""
    mp = _node(nt, "ShaderNodeMapping", 0, 0)
    mp.inputs["Scale"].default_value = (1.0, 1.0, 0.22)
    nt.links.new(co, mp.inputs["Vector"])
    lines = _noise(nt, mp.outputs["Vector"], scale, detail=4.0)
    breaker = _noise(nt, co, scale * 0.35, detail=2.0)
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = "MULTIPLY"
    nt.links.new(lines.outputs["Fac"], m.inputs[0])
    nt.links.new(breaker.outputs["Fac"], m.inputs[1])
    m2 = nt.nodes.new("ShaderNodeMath")
    m2.operation = "MULTIPLY"
    nt.links.new(m.outputs[0], m2.inputs[0])
    m2.inputs[1].default_value = 1.9
    return m2.outputs[0]


def shade():
    for name in MATS:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
                nt.nodes.remove(n)
        co = _node(nt, "ShaderNodeTexCoord", 0, 0).outputs["Object"]
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(co, sep.inputs[0])
        low = _node(nt, "ShaderNodeMapRange", 0, 0)                  # 발치(z 0~0.12m)일수록 1
        nt.links.new(sep.outputs[2], low.inputs["Value"])
        low.inputs["From Min"].default_value, low.inputs["From Max"].default_value = 0.07, 0.0

        if name == "정의문_강철":
            mottle = _noise(nt, co, 9.0)
            base = _mix(nt, mottle.outputs["Fac"], (0.03, 0.032, 0.035, 1), (0.085, 0.087, 0.09, 1))
            # 비바람 자국 — 옅은 세로 줄
            weather = _ramp(nt, [(0.0, (0, 0, 0, 1)), (0.58, (0, 0, 0, 1)), (0.75, (0.35, 0.35, 0.35, 1))])
            nt.links.new(_streaks(nt, co, 14.0), weather.inputs[0])
            washed = _mix(nt, weather.outputs["Color"], base, (0.1, 0.1, 0.1, 1))
            # 녹물 줄 — 가늘고 드문드문, 발치로 갈수록 짙게
            rust_n = _ramp(nt, [(0.0, (0, 0, 0, 1)), (0.68, (0, 0, 0, 1)), (0.8, (0.85, 0.85, 0.85, 1))])
            nt.links.new(_streaks(nt, co, 11.0), rust_n.inputs[0])
            rust_amt = nt.nodes.new("ShaderNodeMath")
            rust_amt.operation = "MAXIMUM"
            nt.links.new(rust_n.outputs["Color"], rust_amt.inputs[0])
            half_low = nt.nodes.new("ShaderNodeMath")
            half_low.operation = "MULTIPLY"
            nt.links.new(low.outputs["Result"], half_low.inputs[0])
            half_low.inputs[1].default_value = 0.45
            nt.links.new(half_low.outputs[0], rust_amt.inputs[1])
            color = _mix(nt, rust_amt.outputs[0], washed, (0.11, 0.042, 0.014, 1))
        elif name == "정의문_쇠띠":
            mottle = _noise(nt, co, 16.0)
            base = _mix(nt, mottle.outputs["Fac"], (0.015, 0.015, 0.016, 1), (0.05, 0.05, 0.052, 1))
            spots = _ramp(nt, [(0.0, (0, 0, 0, 1)), (0.58, (0, 0, 0, 1)), (0.7, (1, 1, 1, 1))])
            nt.links.new(_noise(nt, co, 22.0, detail=8.0).outputs["Fac"], spots.inputs[0])
            rusty = _mix(nt, spots.outputs["Color"], base, (0.13, 0.05, 0.016, 1))
            worn = _ramp(nt, [(0.0, (0, 0, 0, 1)), (0.62, (0, 0, 0, 1)), (0.72, (0.5, 0.5, 0.5, 1))])
            nt.links.new(_streaks(nt, co, 10.0), worn.inputs[0])
            color = _mix(nt, worn.outputs["Color"], rusty, (0.16, 0.16, 0.165, 1))
        else:   # 정의문_장식 — 오래된 황동: 광택 색 + 짙은 때 + 푸른 녹 점
            mottle = _noise(nt, co, 12.0)
            base = _mix(nt, mottle.outputs["Fac"], (0.28, 0.18, 0.05, 1), (0.52, 0.37, 0.11, 1))
            grime = _ramp(nt, [(0.0, (0, 0, 0, 1)), (0.5, (0, 0, 0, 1)), (0.66, (0.8, 0.8, 0.8, 1))])
            nt.links.new(_noise(nt, co, 30.0, detail=6.0).outputs["Fac"], grime.inputs[0])
            dirty = _mix(nt, grime.outputs["Color"], base, (0.07, 0.05, 0.025, 1))
            verd = _ramp(nt, [(0.0, (0, 0, 0, 1)), (0.66, (0, 0, 0, 1)), (0.74, (0.9, 0.9, 0.9, 1))])
            nt.links.new(_noise(nt, co, 18.0, detail=3.0).outputs["Fac"], verd.inputs[0])
            color = _mix(nt, verd.outputs["Color"], dirty, (0.05, 0.13, 0.09, 1))
        bsdf = nt.nodes["Principled BSDF"]
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.6
        nt.links.new(color, bsdf.inputs["Base Color"])


# ──────────────────────────────────────────────────────────── UV·굽기·나누기·내보내기

def _live_objects():
    """뷰 레이어 물체 — 글자용 임시 물체를 지운 직후엔 목록에 None이 남아 있어(1차 실행 오류) 걸러 낸다."""
    bpy.context.view_layer.update()
    return [o for o in bpy.context.view_layer.objects if o is not None]


def _select_only(obj):
    view = bpy.context.view_layer
    for o in _live_objects():
        o.select_set(False)
    obj.select_set(True)
    view.objects.active = obj


def unwrap(obj):
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(obj.data)
    for index in range(len(MATS)):
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
            img = bpy.data.images.new(mat.name, TEX_SIZE, TEX_SIZE)
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = img
            for n in mat.node_tree.nodes:
                n.select = False
            node.select = True
            mat.node_tree.nodes.active = node
            images[mat.name] = img
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=False, margin=8)
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
            nt.links.new(tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    finally:
        scene.render.engine, scene.cycles.samples = saved
        for o in _live_objects():
            o.select_set(o in selected)
        view.objects.active = active


def split(full, collection):
    """한 메시를 부품(part)별 세 오브젝트로 — 짝은 원점을 바깥 경첩 쪽 발밑으로 옮긴다."""
    made = []
    for index, (name, origin_x) in enumerate(PARTS):
        bm = bmesh.new()
        bm.from_mesh(full.data)
        layer = bm.faces.layers.int.get("part")
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[layer] != index], context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        offset = Vector((origin_x / UNITS, 0.0, 0.0))
        bmesh.ops.translate(bm, vec=-offset, verts=bm.verts)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        if "part" in mesh.attributes:
            mesh.attributes.remove(mesh.attributes["part"])
        for mat in full.data.materials:
            mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = offset
        collection.objects.link(obj)
        made.append(obj)
    for child in made[1:]:
        child.parent = made[0]
    mesh = full.data
    bpy.data.objects.remove(full, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    return made[0]


def build_in_window(collection, folder=os.path.join(HERE, "walls_gate_window_tex")):
    shade()
    bm = build_mesh()
    mesh = bpy.data.meshes.new("_정의문_통")
    bm.to_mesh(mesh)
    bm.free()
    full = bpy.data.objects.new("_정의문_통", mesh)
    collection.objects.link(full)
    for name in MATS:
        mesh.materials.append(bpy.data.materials[name])
    unwrap(full)
    bake(full, folder)
    return split(full, collection)


def tris(obj):
    return sum(len(p.vertices) - 2 for o in [obj, *obj.children] for p in o.data.polygons)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col = bpy.data.collections.new("판_구조물")
    bpy.context.scene.collection.children.link(col)
    parent = build_in_window(col, TEX)
    names = [parent.name, *sorted(c.name for c in parent.children)]
    assert names == ["정의문", "정의문_오른짝", "정의문_왼짝"], f"이름이 밀렸다: {names}"
    os.makedirs(OUT, exist_ok=True)
    for o in _live_objects():
        o.select_set(o in (parent, *parent.children))
    bpy.context.view_layer.objects.active = parent
    path = os.path.join(OUT, "정의문.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, global_scale=UNITS,
                             path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
    per = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in (parent, *parent.children)}
    print(f"만듦  정의문  삼각형 {tris(parent)} {per}  → {path}")


if __name__ == "__main__":
    main()
