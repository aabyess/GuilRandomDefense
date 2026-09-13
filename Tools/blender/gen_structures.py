"""원랜디 구조물 — 1번: 불멸 전시 섬 가운데 캠프파이어(화로). PM 배정 2026-09-13.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구되면 Tools/blender/gen_structures.py로 옮기고
   OUT을 Assets/Art/Structures로 바꿔 커밋한다.

화면 없이(정본 FBX·텍스처):  blender --background --factory-startup --python gen_structures.py
사장님 창(보여 주기만):      ns = {"__name__": "gen_structures", "__file__": 경로}; exec(open(경로).read(), ns)
                              obj = ns["build_in_window"](컬렉션)
🔴 창에서는 내보내지 않는다 — 사장님 장면에 화난 건물의 `연기_자리_01`이 이미 있어 새 빈 오브젝트가
   `연기_자리_01.009`로 이름이 밀린 채 FBX에 들어갔다(유니티는 이름으로 파티클 자리를 찾는다).
   창에서 구운 PNG도 정본 Textures가 아닌 별도 폴더에 쓴다.

규격(PM): 둘레석 지름 약 10, 전체 높이 약 4(장작 끝), 원점 바닥 가운데, 삼각형 3,000 이하.
불꽃·연기는 메시로 안 만든다 — 빈 오브젝트 `불_자리_01`(장작 가운데 위)·`연기_자리_01`(그 위 약 6)에
유니티 파티클을 붙인다(화난 건물 `연기_자리_NN`과 같은 방식).
이름 규칙(PM 2026-09-13): 스스로 빛나는 재질은 끝 `_발광`(유니티가 색 텍스처를 Emission에도 넣는다),
알파 컷은 끝 `_잎카드`. 둘 다면 `_발광_잎카드`.
좌표는 게임 단위로 짓고 마지막에 1/11.4로 줄여 m로 둔다. 내보내기 global_scale 11.4.
절차적 셰이더 색은 선형값(sRGB^2.2). 굽기는 DIFFUSE 색만, Metallic 0. 알파는 EMIT로 따로 구워 합친다.
PM 검수 반영(3차):
- 불씨 — 보로노이 선이 벌집 「용암 그물」로 보였다. 선을 없애고, 크기가 제각각인 덩어리 몇 곳만 속에서
  달아오르고 가장자리로 갈수록 주황→검붉음으로 사그라들게. 밝은 곳은 면적 20% 안쪽(굽고 나서 잰다).
- 그을린 흙 — 다각형 모서리로 딱 끊겨 검은 판을 깐 것 같았다. 둘레석 밑 불투명 원판(반지름 약 4.7) +
  바깥 알파 컷 고리(`구조물_그을린흙_잎카드`, 지름 약 12)로 흙이 섬 윗면으로 번지듯 사라지게.
장작 껍질·숯은 원통 UV(둘레 u·길이 v)로 무늬를 그려 결이 장작 길이 방향으로 선다(1차는 뱀 비늘).
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

UNITS = 11.4
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")          # 복구 뒤: Assets/Art/Structures
TEX = os.path.join(OUT, "Textures")
TEX_SIZE = 1024

MATS = ["구조물_그을린흙", "구조물_둘레석", "구조물_장작", "구조물_숯", "구조물_불씨_발광", "구조물_그을린흙_잎카드"]
EARTH, STONE, BARK, CHAR, EMBER, EARTH_CARD = range(6)
LOG_COUNT = 6
MASKS = {}        # 재질 이름 → 알파로 구울 셰이더 소켓(shade가 채운다)


# ──────────────────────────────────────────────────────────── 메시

def _faces_of(verts):
    return list({f for v in verts for f in v.link_faces})


def add_disc(bm, seed=7):
    """그을린 흙 — ① 둘레석 밑까지 오는 불투명 원판(반지름 약 4.7, 윗면 + 비스듬한 옆면)
    ② 바깥 알파 컷 고리(안 4.3 ~ 바깥 약 6.2 들쭉날쭉, 바닥에 붙은 한 장). 고리 안쪽은 원판 밑에 숨는다."""
    rnd = random.Random(seed)
    n, top = 40, 0.12
    angle = [2 * math.pi * i / n for i in range(n)]
    radii = [4.7 + 0.2 * noise.noise(Vector((math.cos(a) * 1.3, math.sin(a) * 1.3, 3.1))) for a in angle]
    center = bm.verts.new((0.0, 0.0, top))
    ring = [bm.verts.new((math.cos(a) * r, math.sin(a) * r, top)) for a, r in zip(angle, radii)]
    skirt = [bm.verts.new((math.cos(a) * (r + 0.3), math.sin(a) * (r + 0.3), 0.0)) for a, r in zip(angle, radii)]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((center, ring[i], ring[j])).material_index = EARTH
        bm.faces.new((skirt[i], skirt[j], ring[j], ring[i])).material_index = EARTH
    m = 48
    inner, outer = [], []
    for i in range(m):
        a = 2 * math.pi * i / m
        r_out = 6.1 + 0.55 * noise.noise(Vector((math.cos(a) * 2.2, math.sin(a) * 2.2, 8.4))) + rnd.uniform(-0.2, 0.2)
        inner.append(bm.verts.new((math.cos(a) * 4.3, math.sin(a) * 4.3, 0.04)))
        outer.append(bm.verts.new((math.cos(a) * r_out, math.sin(a) * r_out, 0.04)))
    for i in range(m):
        j = (i + 1) % m
        bm.faces.new((inner[i], outer[i], outer[j], inner[j])).material_index = EARTH_CARD


def add_stones(bm, count=11, seed=11):
    """둘레석 고리 — 크기가 들쭉날쭉한 돌, 링 반지름 4.2(바깥 끝 지름 약 10). 매끈 음영."""
    rnd = random.Random(seed)
    for k in range(count):
        a = 2 * math.pi * k / count + rnd.uniform(-0.12, 0.12)
        sx, sy, sz = rnd.uniform(1.7, 2.3), rnd.uniform(1.1, 1.6), rnd.uniform(0.9, 1.6)
        radius = 4.2 + rnd.uniform(-0.25, 0.25)
        made = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.5)["verts"]
        offset = Vector((rnd.uniform(0, 50), rnd.uniform(0, 50), k * 3.7))
        for v in made:
            p = v.co.copy()
            p += p.normalized() * 0.16 * noise.noise(p * 2.2 + offset)
            # x = 반지름 방향, y = 둘레 방향. 🔴 축을 맞바꾸면(x↔y) 거울상이라 면이 전부 안을 본다(1차 실수)
            p = Vector((p.x * sy, p.y * sx, p.z * sz))
            v.co = p
        low = min(v.co.z for v in made)
        floor = low + 0.22 * sz                                  # 밑을 눌러 평평하게 앉힌다
        for v in made:
            v.co.z = max(v.co.z, floor)
        low = min(v.co.z for v in made)
        rot = Matrix.Rotation(a, 3, "Z")
        tilt = Matrix.Rotation(rnd.uniform(-0.15, 0.15), 3, "X")
        for v in made:
            v.co = rot @ (tilt @ (v.co - Vector((0, 0, low)))) + Vector((math.cos(a) * radius, math.sin(a) * radius, 0.05))
            v.co.z = max(v.co.z, 0.03)                           # 기울인 뒤 땅 밑으로 내려간 정점(최저점 0 규칙)
        for f in _faces_of(made):
            f.material_index = STONE
            f.smooth = True


def add_log(bm, uv, part, k, base, tip, radius, seed, sides=7):
    """장작 하나 — 밑(base)에서 끝(tip)으로 7각 기둥 두 마디. 끝 38%와 두 마구리는 숯.
    UV: 장작 k번은 u [k/6, (k+1)/6) 띠에 둘레를, v에 길이(0~1)를 편다. 마구리는 띠 가운데 작은 원."""
    rnd = random.Random(seed)
    axis = (tip - base).normalized()
    u = axis.orthogonal().normalized()
    w = axis.cross(u)
    u = w.cross(axis)                                             # u × w = axis(바깥을 보는 감김)
    ts = (0.0, 0.62, 1.0)
    rings = []
    for t in ts:
        c = base.lerp(tip, t)
        r = radius * (1.0 - 0.18 * t) * rnd.uniform(0.93, 1.05)
        rings.append([bm.verts.new(c + (u * math.cos(2 * math.pi * i / sides) + w * math.sin(2 * math.pi * i / sides)) * r)
                      for i in range(sides)])
    u0, du = k / LOG_COUNT + 0.004, 1.0 / LOG_COUNT - 0.008
    for seg, mat in ((0, BARK), (1, CHAR)):
        lo, hi = rings[seg], rings[seg + 1]
        for i in range(sides):
            j = (i + 1) % sides
            f = bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
            f.material_index, f.smooth = mat, True
            f[part] = 1
            coords = ((i, ts[seg]), (i + 1, ts[seg]), (i + 1, ts[seg + 1]), (i, ts[seg + 1]))
            for loop, (col, v) in zip(f.loops, coords):
                loop[uv].uv = (u0 + du * col / sides, v)
    cx = u0 + du / 2
    for ring, order, v0 in ((rings[2], 1, 0.9), (rings[0], -1, 0.1)):
        verts = ring if order == 1 else list(reversed(ring))
        f = bm.faces.new(verts)
        f.material_index = CHAR
        f[part] = 1
        for loop in f.loops:
            i = ring.index(loop.vert)
            a = 2 * math.pi * i / sides
            loop[uv].uv = (cx + math.cos(a) * du * 0.4, v0 + math.sin(a) * 0.05)
        for e in f.edges:
            e.smooth = False                                      # 마구리 모서리는 각지게(옆면 매끈 음영이 번지지 않게)


def add_logs(bm, uv, part, seed=23):
    """원추형으로 세운 장작 — 밑동은 반지름 2.3 둘레에, 끝은 가운데 위(높이 약 3.9)에서 엇갈린다."""
    rnd = random.Random(seed)
    for k in range(LOG_COUNT):
        a = 2 * math.pi * k / LOG_COUNT + rnd.uniform(-0.2, 0.2)
        rb = 2.3 + rnd.uniform(-0.2, 0.25)
        base = Vector((math.cos(a) * rb, math.sin(a) * rb, 0.35))
        b = a + math.pi + rnd.uniform(-0.5, 0.5)                   # 끝은 가운데를 살짝 넘어간다
        rt = rnd.uniform(0.2, 0.5)
        tip = Vector((math.cos(b) * rt, math.sin(b) * rt, 3.75 + rnd.uniform(-0.15, 0.15)))
        add_log(bm, uv, part, k, base, tip, rnd.uniform(0.26, 0.34), seed * 100 + k)


def add_ember_bed(bm, seed=31):
    """숯·잿더미 — 가운데 낮은 둔덕(반지름 2.1, 높이 0.55), 덩어리로 달아오른 불씨 텍스처."""
    n, rings = 14, (1.0, 0.72, 0.42)
    heights = (0.08, 0.36, 0.52)
    layers = []
    for r, h in zip(rings, heights):
        layer = []
        for i in range(n):
            a = 2 * math.pi * i / n
            wob = 1.0 + 0.12 * noise.noise(Vector((math.cos(a) * 2.0, math.sin(a) * 2.0, r * 5 + seed)))
            layer.append(bm.verts.new((math.cos(a) * 2.1 * r * wob, math.sin(a) * 2.1 * r * wob, h + 0.05 * wob)))
        layers.append(layer)
    top = bm.verts.new((0.0, 0.0, 0.6))
    faces = []
    for lo, hi in zip(layers, layers[1:]):
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new((lo[i], lo[j], hi[j], hi[i])))
    last = layers[-1]
    for i in range(n):
        faces.append(bm.faces.new((top, last[i], last[(i + 1) % n])))
    for f in faces:
        f.material_index, f.smooth = EMBER, True


def add_charcoal(bm, count=9, seed=41):
    """숯덩이 — 잿더미 위에 반쯤 묻힌 찌그러진 상자."""
    rnd = random.Random(seed)
    for k in range(count):
        a, r = rnd.uniform(0, 2 * math.pi), rnd.uniform(0.5, 1.8)
        size = rnd.uniform(0.3, 0.6)
        m = (Matrix.Translation((math.cos(a) * r, math.sin(a) * r, 0.55 - 0.18 * r))
             @ Matrix.Rotation(rnd.uniform(0, math.pi), 4, "Z") @ Matrix.Rotation(rnd.uniform(-0.5, 0.5), 4, "X")
             @ Matrix.Diagonal((size * rnd.uniform(1.0, 1.8), size, size * 0.6, 1.0)))
        made = bmesh.ops.create_cube(bm, size=1.0, matrix=m)["verts"]
        for v in made:
            v.co += Vector((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), rnd.uniform(-0.04, 0.04)))
            v.co.z = max(v.co.z, 0.14)
        for f in _faces_of(made):
            f.material_index = CHAR


MARKERS = [("불_자리_01", Vector((0.0, 0.0, 1.2))), ("연기_자리_01", Vector((0.0, 0.0, 7.2)))]


def build_campfire(collection):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.verify()
    part = bm.faces.layers.int.new("part")                        # 1 = 장작(원통 UV를 직접 편 면)
    add_disc(bm)
    add_stones(bm)
    add_ember_bed(bm)
    add_charcoal(bm)
    add_logs(bm, uv, part)
    bm.normal_update()
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    mesh = bpy.data.meshes.new("캠프파이어")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("캠프파이어", mesh)
    collection.objects.link(obj)
    for name in MATS:
        obj.data.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
    for name, at in MARKERS:
        empty = bpy.data.objects.new(name, None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = 0.1
        empty.location = at / UNITS
        empty.parent = obj
        collection.objects.link(empty)
    return obj


# ──────────────────────────────────────────────────────────── UV

def unwrap(obj):
    """장작(원통 UV 직접 편 면)을 뺀 나머지를 재질마다 따로 스마트 투영."""
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    obj.select_set(True)
    view.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bm = bmesh.from_edit_mesh(obj.data)
    part = bm.faces.layers.int.get("part")
    for index in range(len(MATS)):
        picked = False
        for f in bm.faces:
            f.select = f.material_index == index and f[part] == 0
            picked = picked or f.select
        bmesh.update_edit_mesh(obj.data)
        if picked:
            bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.attributes.remove(obj.data.attributes["part"])


# ──────────────────────────────────────────────────────────── 셰이더

def _node(nt, kind, x, y, **inputs):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for key, value in inputs.items():
        n.inputs[key].default_value = value
    return n


def _ramp(nt, x, y, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    r.location = (x, y)
    els = r.color_ramp.elements
    els[0].position, els[0].color = stops[0]
    els[1].position, els[1].color = stops[-1]
    for pos, col in stops[1:-1]:
        e = els.new(pos)
        e.color = col
    return r


def _mix(nt, x, y, fac, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    m.location = (x, y)
    if isinstance(fac, float):
        m.inputs["Factor"].default_value = fac
    else:
        nt.links.new(fac, m.inputs["Factor"])
    for sock, val in ((m.inputs[6], a), (m.inputs[7], b)):
        if isinstance(val, tuple):
            sock.default_value = val
        else:
            nt.links.new(val, sock)
    return m.outputs[2]


def _math(nt, op, a, b):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    for sock, val in ((m.inputs[0], a), (m.inputs[1], b)):
        if isinstance(val, float):
            sock.default_value = val
        else:
            nt.links.new(val, sock)
    return m.outputs[0]


def _radial(nt):
    """물체 좌표 (x, y) 반지름(m)과 바깥 방향 단위벡터."""
    tc = _node(nt, "ShaderNodeTexCoord", -1400, 0)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(sep.outputs[0], comb.inputs[0])
    nt.links.new(sep.outputs[1], comb.inputs[1])
    length = nt.nodes.new("ShaderNodeVectorMath")
    length.operation = "LENGTH"
    nt.links.new(comb.outputs[0], length.inputs[0])
    unit = nt.nodes.new("ShaderNodeVectorMath")
    unit.operation = "NORMALIZE"
    nt.links.new(comb.outputs[0], unit.inputs[0])
    return tc, sep, length.outputs["Value"], unit.outputs["Vector"]


def _noise(nt, vector, scale, detail=6.0, rough=0.6, x=-900, y=0):
    n = _node(nt, "ShaderNodeTexNoise", x, y, Scale=scale, Detail=detail, Roughness=rough)
    nt.links.new(vector, n.inputs["Vector"])
    return n


def _uv_scaled(nt, tc, sx, sy):
    """UV에 축별 배율 — 장작 띠(u 1/6 = 둘레)에 맞춰 결을 늘인다."""
    mp = _node(nt, "ShaderNodeMapping", -1150, 300)
    mp.inputs["Scale"].default_value = (sx, sy, 1.0)
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
    return mp.outputs["Vector"]


def _principled(mat, color, roughness=0.9, emission=None):
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = roughness
    nt.links.new(color, bsdf.inputs["Base Color"])
    if emission is not None:
        nt.links.new(emission, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 3.0


def _earth_color(nt, tc, radius):
    """그을린 흙 색 — 가운데 숯검정 → 바깥 탄 흙(원판·고리 공통, 물체 좌표라 이음매가 맞는다)."""
    obj_co = tc.outputs["Object"]
    edge = _node(nt, "ShaderNodeMapRange", -900, 300)
    nt.links.new(radius, edge.inputs["Value"])
    edge.inputs["From Min"].default_value, edge.inputs["From Max"].default_value = 0.1, 0.55
    burnt = _mix(nt, -600, 200, edge.outputs["Result"], (0.012, 0.01, 0.008, 1), (0.075, 0.052, 0.034, 1))
    mottle = _noise(nt, obj_co, 18.0)
    dirt = _mix(nt, -400, 200, _math(nt, "MULTIPLY", mottle.outputs["Fac"], 0.6), burnt, (0.035, 0.026, 0.018, 1))
    ash_n = _noise(nt, obj_co, 7.0, detail=8.0, x=-900, y=-300)
    ash = _ramp(nt, -650, -300, [(0.0, (0, 0, 0, 1)), (0.62, (0, 0, 0, 1)), (0.74, (0.4, 0.4, 0.4, 1))])
    nt.links.new(ash_n.outputs["Fac"], ash.inputs[0])
    grit_n = _noise(nt, obj_co, 120.0, detail=2.0, x=-900, y=-600)
    grit = _ramp(nt, -650, -600, [(0.0, (0, 0, 0, 1)), (0.68, (0, 0, 0, 1)), (0.78, (0.35, 0.35, 0.35, 1))])
    nt.links.new(grit_n.outputs["Fac"], grit.inputs[0])
    ashy = _mix(nt, -200, 100, ash.outputs["Color"], dirt, (0.07, 0.066, 0.06, 1))
    return _mix(nt, -50, 100, grit.outputs["Color"], ashy, (0.09, 0.085, 0.08, 1))


def shade():
    MASKS.clear()
    for name in MATS:
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        for n in list(nt.nodes):
            if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
                nt.nodes.remove(n)
        tc, sep, radius, outward = _radial(nt)
        obj_co = tc.outputs["Object"]

        if name == "구조물_그을린흙":
            _principled(mat, _earth_color(nt, tc, radius), 1.0)

        elif name == "구조물_그을린흙_잎카드":
            _principled(mat, _earth_color(nt, tc, radius), 1.0)
            # 알파 — 반지름 0.40m(4.6단위)부터 0.53m(6.0단위)까지 사라짐, 노이즈로 들쭉날쭉
            wobble = _noise(nt, obj_co, 9.0, detail=4.0, x=-900, y=-900)
            shifted = _math(nt, "ADD", radius, _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", wobble.outputs["Fac"], 0.5), 0.09))
            fade = _node(nt, "ShaderNodeMapRange", -600, -900)
            nt.links.new(shifted, fade.inputs["Value"])
            fade.inputs["From Min"].default_value, fade.inputs["From Max"].default_value = 0.44, 0.5
            fade.inputs["To Min"].default_value, fade.inputs["To Max"].default_value = 1.0, 0.0
            MASKS[name] = fade.outputs["Result"]

        elif name == "구조물_둘레석":
            grain = _noise(nt, obj_co, 26.0)
            broad = _noise(nt, obj_co, 5.0, detail=3.0, x=-900, y=200)
            base = _mix(nt, -600, 300, grain.outputs["Fac"], (0.045, 0.043, 0.04, 1), (0.15, 0.142, 0.13, 1))
            base = _mix(nt, -500, 350, _math(nt, "MULTIPLY", broad.outputs["Fac"], 0.5), base, (0.09, 0.075, 0.06, 1))
            geo = _node(nt, "ShaderNodeNewGeometry", -1200, -400)
            inward = nt.nodes.new("ShaderNodeVectorMath")
            inward.operation = "DOT_PRODUCT"
            nt.links.new(geo.outputs["Normal"], inward.inputs[0])
            nt.links.new(outward, inward.inputs[1])
            soot_fac = _node(nt, "ShaderNodeMapRange", -900, -300)
            nt.links.new(inward.outputs["Value"], soot_fac.inputs["Value"])
            soot_fac.inputs["From Min"].default_value, soot_fac.inputs["From Max"].default_value = 0.25, -0.45
            blot = _noise(nt, obj_co, 7.0, x=-900, y=-600)
            blot_r = _ramp(nt, -650, -600, [(0.0, (0.35, 0.35, 0.35, 1)), (0.6, (1, 1, 1, 1))])
            nt.links.new(blot.outputs["Fac"], blot_r.inputs[0])
            soot = _math(nt, "MULTIPLY", soot_fac.outputs["Result"], blot_r.outputs["Color"])
            sooty = _mix(nt, -400, 0, soot, base, (0.008, 0.007, 0.007, 1))
            moss_n = _noise(nt, obj_co, 9.0, detail=5.0, x=-900, y=600)
            moss_cut = _ramp(nt, -650, 600, [(0.0, (0, 0, 0, 1)), (0.5, (0, 0, 0, 1)), (0.6, (1, 1, 1, 1))])
            nt.links.new(moss_n.outputs["Fac"], moss_cut.inputs[0])
            up = nt.nodes.new("ShaderNodeSeparateXYZ")
            nt.links.new(geo.outputs["Normal"], up.inputs[0])
            away = _math(nt, "SUBTRACT", 1.0, soot_fac.outputs["Result"])
            moss_mask = _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", moss_cut.outputs["Color"], up.outputs[2]), away)
            moss_col = _mix(nt, -400, 500, grain.outputs["Fac"], (0.02, 0.035, 0.008, 1), (0.055, 0.08, 0.02, 1))
            color = _mix(nt, -200, 200, moss_mask, sooty, moss_col)
            _principled(mat, color, 0.85)

        elif name == "구조물_장작":
            vec = _uv_scaled(nt, tc, 1.0, 0.18)
            wave = _node(nt, "ShaderNodeTexWave", -900, 300, Scale=40.0, Distortion=9.0, Detail=3.0)
            wave.wave_type, wave.bands_direction = "BANDS", "X"
            nt.links.new(vec, wave.inputs["Vector"])
            fissure = _ramp(nt, -650, 300, [(0.0, (0, 0, 0, 1)), (0.22, (0, 0, 0, 1)), (0.42, (1, 1, 1, 1))])
            nt.links.new(wave.outputs["Fac"], fissure.inputs[0])
            tone = _noise(nt, tc.outputs["UV"], 30.0, x=-900, y=0)
            bark = _mix(nt, -450, 100, tone.outputs["Fac"], (0.03, 0.019, 0.011, 1), (0.085, 0.056, 0.032, 1))
            ridged = _mix(nt, -300, 200, fissure.outputs["Color"], (0.008, 0.006, 0.005, 1), bark)
            uvs = nt.nodes.new("ShaderNodeSeparateXYZ")
            nt.links.new(tc.outputs["UV"], uvs.inputs[0])
            scorch = _node(nt, "ShaderNodeMapRange", -600, -300)
            nt.links.new(uvs.outputs[1], scorch.inputs["Value"])
            scorch.inputs["From Min"].default_value, scorch.inputs["From Max"].default_value = 0.4, 0.62
            color = _mix(nt, -150, 100, scorch.outputs["Result"], ridged, (0.012, 0.01, 0.009, 1))
            _principled(mat, color, 0.95)

        elif name == "구조물_숯":
            # 숯 — 더 검게, 균열은 결 따라 세로로 길게(PM: 1차 거북등이 「회색 자갈 무늬 모자」로 보였다).
            # UV 띠(u 1/6 = 둘레)라 u를 크게 늘일수록 칸이 길이 방향으로 길쭉해진다.
            vec = _uv_scaled(nt, tc, 16.0, 1.2)
            cells = _node(nt, "ShaderNodeTexVoronoi", -900, 300, Scale=18.0)
            cells.feature = "DISTANCE_TO_EDGE"
            nt.links.new(vec, cells.inputs["Vector"])
            crack = _ramp(nt, -650, 300, [(0.0, (0, 0, 0, 1)), (0.03, (0, 0, 0, 1)), (0.08, (1, 1, 1, 1))])
            nt.links.new(cells.outputs["Distance"], crack.inputs[0])
            sheen = _noise(nt, vec, 7.0, x=-900, y=0)
            body = _mix(nt, -450, 0, sheen.outputs["Fac"], (0.009, 0.0085, 0.008, 1), (0.032, 0.03, 0.028, 1))
            color = _mix(nt, -250, 200, crack.outputs["Color"], (0.003, 0.0028, 0.0026, 1), body)
            _principled(mat, color, 0.8)

        elif name == "구조물_불씨_발광":
            # 잿더미 — 잿빛 재·검은 숯 바탕. 크기가 제각각인 덩어리 몇 곳만 속에서 달아오르고,
            # 가장자리로 갈수록 주황 → 검붉음으로 사그라든다. 선·세포 무늬는 쓰지 않는다(3차 PM 검수).
            # 재는 짙은 잿빛까지만(4차 렌더에서 흰 대리석처럼 떴다)
            ash_n = _noise(nt, obj_co, 32.0, detail=7.0, x=-900, y=-300)
            base = _ramp(nt, -650, -300, [(0.0, (0.01, 0.009, 0.008, 1)), (0.5, (0.024, 0.022, 0.02, 1)),
                                          (0.66, (0.06, 0.057, 0.054, 1)), (1.0, (0.1, 0.095, 0.09, 1))])
            nt.links.new(ash_n.outputs["Fac"], base.inputs[0])
            # 덩어리 — 크기가 다른 노이즈 셋(자리를 서로 비켜 놓음)의 최댓값. 4차는 큰 덩어리 하나만 떴다
            lumps = None
            for i, (scale, drop) in enumerate(((4.5, 0.0), (9.0, 0.03), (17.0, 0.06))):
                mp = _node(nt, "ShaderNodeMapping", -1300, 600 + 200 * i)
                mp.inputs["Location"].default_value = (3.7 * i, -2.1 * i, 1.3 * i)
                nt.links.new(obj_co, mp.inputs["Vector"])
                field = _math(nt, "SUBTRACT", _noise(nt, mp.outputs["Vector"], scale, detail=2.0, rough=0.5,
                                                     x=-1100, y=600 + 200 * i).outputs["Fac"], drop)
                lumps = field if lumps is None else _math(nt, "MAXIMUM", lumps, field)
            # 넓은 범위로 사그라들게(4차는 0.6~0.76이라 가장자리가 칼로 자른 듯했다), 가운데가 더 뜨겁게
            heat = _node(nt, "ShaderNodeMapRange", -800, 600)
            nt.links.new(lumps, heat.inputs["Value"])
            heat.inputs["From Min"].default_value, heat.inputs["From Max"].default_value = 0.56, 0.8
            core = _math(nt, "POWER", heat.outputs["Result"], 1.5)
            speck = _noise(nt, obj_co, 70.0, detail=3.0, x=-1100, y=1200)          # 속불 알갱이(선이 아닌 얼룩)
            grainy = _math(nt, "MULTIPLY", core, _math(nt, "ADD", 0.6, _math(nt, "MULTIPLY", speck.outputs["Fac"], 0.8)))
            glow = _ramp(nt, -600, 600, [(0.0, (0.0, 0.0, 0.0, 1)), (0.08, (0.05, 0.006, 0.002, 1)),
                                         (0.35, (0.35, 0.05, 0.008, 1)), (0.65, (0.8, 0.25, 0.03, 1)),
                                         (0.9, (1.0, 0.5, 0.1, 1))])
            nt.links.new(grainy, glow.inputs[0])
            cover = _ramp(nt, -600, 400, [(0.0, (0, 0, 0, 1)), (0.35, (1, 1, 1, 1))])
            nt.links.new(grainy, cover.inputs[0])
            color = _mix(nt, -300, 100, cover.outputs["Color"], base.outputs["Color"], glow.outputs["Color"])
            _principled(mat, color, 1.0, emission=glow.outputs["Color"])


# ──────────────────────────────────────────────────────────── 굽기·내보내기

def _pixels(img):
    import numpy as np
    arr = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    return arr


def bake(obj, folder=TEX):
    """재질마다 1024 PNG로 DIFFUSE 색만 굽고(알파 재질은 EMIT로 알파를 따로 구워 합친다),
    재질을 이미지 한 장짜리로 바꾼다. 장면 설정은 되돌린다."""
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    saved = (scene.render.engine, scene.cycles.samples, scene.render.bake.use_clear, scene.render.bake.margin)
    view = bpy.context.view_layer
    selected = [o for o in view.objects if o.select_get()]
    active = view.objects.active
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    try:
        for o in view.objects:
            o.select_set(False)
        obj.select_set(True)
        view.objects.active = obj
        images, nodes = {}, {}
        for slot in obj.material_slots:
            mat = slot.material
            for stale in (mat.name, mat.name + "_알파"):
                old = bpy.data.images.get(stale)
                if old is not None:
                    bpy.data.images.remove(old)
            img = bpy.data.images.new(mat.name, TEX_SIZE, TEX_SIZE, alpha=mat.name in MASKS)
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = img
            for n in mat.node_tree.nodes:
                n.select = False
            node.select = True
            mat.node_tree.nodes.active = node
            images[mat.name], nodes[mat.name] = img, node
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=False, margin=8)

        # 알파 — 알파 재질만 마스크를 발광으로 굽고, 나머지 재질은 버리는 이미지에 받는다
        if MASKS:
            dummy = bpy.data.images.new("_버림", 64, 64)
            alphas = {}
            for name, node in nodes.items():
                nt = bpy.data.materials[name].node_tree
                if name in MASKS:
                    alphas[name] = bpy.data.images.new(name + "_알파", TEX_SIZE, TEX_SIZE)
                    node.image = alphas[name]
                    bsdf = nt.nodes["Principled BSDF"]
                    emit = nt.nodes.new("ShaderNodeEmission")
                    nt.links.new(MASKS[name], emit.inputs["Color"])
                    out = nt.nodes["Material Output"]
                    keep = out.inputs["Surface"].links[0].from_socket
                    nt.links.new(emit.outputs[0], out.inputs["Surface"])
                    alphas[name + "_복구"] = (emit, keep)
                else:
                    node.image = dummy
            bpy.ops.object.bake(type="EMIT", use_clear=False, margin=8)
            for name in MASKS:
                nt = bpy.data.materials[name].node_tree
                emit, keep = alphas[name + "_복구"]
                nt.links.new(keep, nt.nodes["Material Output"].inputs["Surface"])
                nt.nodes.remove(emit)
                color = _pixels(images[name])
                alpha = _pixels(alphas[name])
                color[3::4] = alpha[0::4]
                images[name].pixels.foreach_set(color)
                bpy.data.images.remove(alphas[name])
            for name, node in nodes.items():
                node.image = images[name]
            bpy.data.images.remove(dummy)

        for name, img in images.items():
            img.filepath_raw = os.path.join(folder, name + ".png")
            img.file_format = "PNG"
            img.save()
            mat = bpy.data.materials[name]
            nt = mat.node_tree
            for n in list(nt.nodes):
                if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
                    nt.nodes.remove(n)
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = img
            bsdf = nt.nodes["Principled BSDF"]
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            if name.endswith("_잎카드"):
                # 창 표시도 유니티 알파 컷(0.5)처럼 딱 끊기게 — 그냥 이으면 반투명 회색 테가 떠 보인다(4차 렌더)
                cut = nt.nodes.new("ShaderNodeMath")
                cut.operation = "GREATER_THAN"
                cut.inputs[1].default_value = 0.5
                nt.links.new(tex.outputs["Alpha"], cut.inputs[0])
                nt.links.new(cut.outputs[0], bsdf.inputs["Alpha"])
            if "_발광" in name:
                nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
                bsdf.inputs["Emission Strength"].default_value = 1.5
            else:
                bsdf.inputs["Emission Strength"].default_value = 0.0
    finally:
        scene.render.engine, scene.cycles.samples, scene.render.bake.use_clear, scene.render.bake.margin = saved
        for o in view.objects:
            o.select_set(o in selected)
        view.objects.active = active


def ember_glow_share(obj):
    """구운 불씨 텍스처에서 달아오른(주황빛) 면적 비율과 밝은(주황~노랑) 면적 비율 — 면 넓이 가중 표본."""
    img = bpy.data.images["구조물_불씨_발광"]
    px = _pixels(img).reshape(img.size[1], img.size[0], 4)
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    index = MATS.index("구조물_불씨_발광")
    total = hot = bright = 0.0
    for poly in mesh.polygons:
        if poly.material_index != index:
            continue
        uvs = [uv[i].uv for i in poly.loop_indices]
        c = sum(uvs, Vector((0, 0))) / len(uvs)
        samples = [c] + [c.lerp(q, 0.6) for q in uvs]
        for s in samples:
            x = min(img.size[0] - 1, int(s.x % 1.0 * img.size[0]))
            y = min(img.size[1] - 1, int(s.y % 1.0 * img.size[1]))
            r, g, b = px[y, x, :3]
            w = poly.area / len(samples)
            total += w
            if r > 0.25 and r > 2.2 * b:
                hot += w
            if r > 0.7 and g > 0.3:
                bright += w
    return hot / total, bright / total


def export(obj, filename):
    os.makedirs(OUT, exist_ok=True)
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    obj.select_set(True)
    for child in obj.children:
        child.select_set(True)
    view.objects.active = obj
    path = os.path.join(OUT, filename)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
                             global_scale=UNITS, path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False,
                             mesh_smooth_type="FACE")
    return path


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def build_in_window(collection, folder=os.path.join(HERE, "structures_window_tex")):
    """사장님 창에 짓기만 — 셰이더·메시·UV·굽기까지, 내보내기는 안 한다. 🔴 창에서 구운 PNG는 정본 Textures가 아니라
    따로 둔 폴더에 쓴다 — 창 굽기가 정본 PNG를 덮으면, 헤드리스 FBX의 UV와 짝이 어긋날 수 있다."""
    shade()
    obj = build_campfire(collection)
    unwrap(obj)
    bake(obj, folder)
    return obj


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    col = bpy.data.collections.new("판_구조물")
    bpy.context.scene.collection.children.link(col)
    obj = build_in_window(col, TEX)
    names = sorted(child.name for child in obj.children)
    assert names == sorted(n for n, _ in MARKERS), f"빈 오브젝트 이름이 밀렸다: {names}"
    hot, bright = ember_glow_share(obj)
    path = export(obj, "캠프파이어.fbx")
    print(f"만듦  캠프파이어  삼각형 {tris(obj)}  빈 오브젝트 {names}  불씨 달아오름 {hot * 100:.1f}% · 밝음 {bright * 100:.1f}%  → {path}")


if __name__ == "__main__":
    main()
