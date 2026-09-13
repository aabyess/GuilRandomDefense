"""포탈 세트 — 비바람에 닳은 돌 아치 + 빛나는 막(PM 배정 2026-09-13, 구조물 4번). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX·텍스처는 Assets/Art/Structures/로.

화면 없이(정본):  blender --background --factory-startup --python gen_portals.py [-- 포탈_작은_스토리 ...]
사장님 창(보여 주기만): ns = {"__name__": "gen_portals", "__file__": 경로}; exec(...); ns["build_in_window"](이름, 컬렉션, 창 텍스처 폴더)

규격(PM):
| 파일명 | 쓰임 | 안쪽 폭×높이 | 바닥 |
| 포탈_작은_스토리 / 포탈_작은_뽑기 | 레인→스토리존 / 뽑기 섬 | 6×9 | 9×4 안 |
| 포탈_큰_복귀 | 스토리존→레인 복귀 | 12×16 | 17×6 안 |
- 원점 바닥 가운데, 정면 −Y(막이 XZ 면), 삼각형 채당 2,000 이하, 뒷면 검사 둘 0건(막은 `_잎카드` 양면이라 제외).
- 막 재질 `포탈_막_<색>_발광_잎카드`(가장자리는 알파로 흩어짐), 막 가운데 빈 오브젝트 `빛_자리_01`(유니티 소용돌이 입자 소켓).
- 같은 크기의 색 변형은 메시가 같고 막 재질만 다르다. 돌 재질은 크기마다 따로(`포탈_작은_돌`, `포탈_큰_돌`) —
  텍스처를 그 메시에 맞춰 굽기 때문(작은 두 변형은 같은 메시라 같은 돌 텍스처를 같은 내용으로 다시 쓴다).
구성: 돌 받침 두 단 · 기둥(쌓은 돌 켜) · 반원 아치(쐐기돌 여럿 + 뒤로 물린 이끼 줄눈 고리) · 크게 튀어나온 머릿돌 +
닻 문양 돋을새김(앞면) · 아치 안 막 한 장.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형, 굽기 DIFFUSE(Metallic 0), 알파는 EMIT로 따로 구워 합친다.
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

from shops_common import Builder, _link, _math, _mix, _node, _noise, _ramp, _select_only, _live_objects, unwrap  # noqa: E402

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")

SIZES = {
    # 안쪽 폭, 안쪽 높이, 아치 고리 두께, 아치 앞뒤 반폭, 받침(아래·위) 반폭 x·y, 쐐기돌 수, 기둥 켜 수, 바닥 반폭 한계 x·y
    "작은": dict(w=6.0, h=9.0, ring=1.3, depth=0.7, step1=(4.5, 2.0), step2=(4.3, 1.5), stones=11, courses=4, limit=(4.5, 2.0)),
    # 큰 포탈은 그냥 두 배로 키우면 돌이 벽돌처럼 커 보인다(PM) — 고리 두께·쐐기돌 수·기둥 켜로 돌 한 개 크기를 작은 포탈과 비슷하게
    "큰": dict(w=12.0, h=16.0, ring=1.6, depth=1.6, step1=(8.5, 3.0), step2=(8.2, 2.3), stones=17, courses=7, limit=(8.5, 3.0)),
}
STEP_H = 0.4
FLOOR = 2 * STEP_H
TUCK = 0.25          # 막을 아치 안쪽 윤곽보다 사방으로 키워 돌 속에 가장자리를 숨기는 폭(PM 2차)
# 막 색(선형) — (중간 짙은 색, 가운데 밝은 색, 돌에 닿는 곳 거의 검정)
MEMBRANE = {
    "호박": ((0.45, 0.16, 0.02), (1.0, 0.62, 0.18), (0.05, 0.012, 0.003)),
    "보라": ((0.18, 0.03, 0.38), (0.72, 0.42, 1.0), (0.03, 0.004, 0.05)),
    "청록": ((0.0, 0.25, 0.26), (0.35, 1.0, 0.9), (0.0, 0.035, 0.04)),
}
VARIANTS = {"포탈_작은_스토리": ("작은", "호박"), "포탈_작은_뽑기": ("작은", "보라"), "포탈_큰_복귀": ("큰", "청록")}
MASKS = {}


# ──────────────────────────────────────────────────────────── 메시

def build_portal(size, color, seed=5):
    s = SIZES[size]
    rnd = random.Random(seed)
    b = Builder()
    stone, moss = f"포탈_{size}_돌", f"포탈_{size}_이끼줄눈"
    membrane = f"포탈_막_{color}_발광_잎카드"
    half, r_in = s["w"] / 2, s["w"] / 2
    r_out = r_in + s["ring"]
    spring = FLOOR + s["h"] - r_in                 # 반원 아치가 시작하는 높이
    d = s["depth"]
    # 받침 두 단
    (x1, y1), (x2, y2) = s["step1"], s["step2"]
    b.box(-x1, x1, -y1, y1, 0.0, STEP_H, stone, skip=("bottom",))
    b.box(-x2, x2, -y2, y2, STEP_H, FLOOR, stone, skip=("bottom",))
    # 기둥 — 켜마다 살짝 다른 크기로 튀어나온 돌(안쪽 면은 막 가장자리에 맞춰 반듯하게)
    course_h = (spring - FLOOR) / s["courses"]
    for sx in (-1, 1):
        for k in range(s["courses"]):
            z0 = FLOOR + k * course_h
            out = s["ring"] + rnd.uniform(-0.05, 0.12)
            dd = d + rnd.uniform(-0.04, 0.1)
            b.box(sx * half, sx * (half + out), -dd, dd, z0, z0 + course_h, stone, skip=("bottom",) if k == 0 else ())
    # 줄눈 고리 — 쐐기돌 뒤로 물린 이끼 줄눈(쐐기돌 사이 틈으로 보인다)
    n = s["stones"]
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        pts = [(math.cos(a) * r, spring + math.sin(a) * r) for r, a in ((r_in + 0.06, a0), (r_out - 0.06, a0), (r_out - 0.06, a1), (r_in + 0.06, a1))]
        b.extrude([(x, -d + 0.1, z) for x, z in pts], (0, 2 * d - 0.2, 0), moss)
    # 쐐기돌 — 사이에 작은 각 틈, 가운데는 머릿돌(더 크고 앞뒤로 더 튀어나옴)
    gap = 0.018
    key = n // 2
    for i in range(n):
        a0, a1 = math.pi * i / n + gap, math.pi * (i + 1) / n - gap
        ro = r_out + (0.45 * s["ring"] / 1.3 if i == key else rnd.uniform(-0.04, 0.08))
        dd = d + (0.18 * s["ring"] / 1.3 if i == key else rnd.uniform(-0.03, 0.06))
        if i == key:
            widen = 0.06
            a0, a1 = a0 - widen, a1 + widen
        pts = [(math.cos(a) * r, spring + math.sin(a) * r) for r, a in ((r_in, a0), (ro, a0), (ro, a1), (r_in, a1))]
        b.extrude([(x, -dd, z) for x, z in pts], (0, 2 * dd, 0), stone)
    # 머릿돌 닻 돋을새김(앞면) — 고리·가로대·자루·양팔·갈고리
    k_mid = r_in + (r_out + 0.45 * s["ring"] / 1.3 - r_in) / 2
    kz = spring + k_mid
    front = -(d + 0.18 * s["ring"] / 1.3)
    u = s["ring"] / 1.3                            # 크기 비율(작은 = 1)
    rel = 0.08 * u
    b.cylinder((0.0, front, kz + 0.52 * u), (0, -1, 0), 0.13 * u, rel, stone, sides=8)          # 고리
    b.box(-0.05 * u, 0.05 * u, front - rel, front, kz - 0.5 * u, kz + 0.42 * u, stone, skip=("+y",))   # 자루
    b.box(-0.32 * u, 0.32 * u, front - rel, front, kz + 0.25 * u, kz + 0.34 * u, stone, skip=("+y",))  # 가로대
    for sx in (-1, 1):
        arm = [(0.0, kz - 0.5 * u), (sx * 0.38 * u, kz - 0.26 * u), (sx * 0.44 * u, kz - 0.14 * u), (sx * 0.34 * u, kz - 0.18 * u),
               (0.0, kz - 0.38 * u)]
        b.extrude([(x, front, z) for x, z in arm], (0, -rel, 0), stone)
        fluke = [(sx * 0.44 * u, kz - 0.14 * u), (sx * 0.52 * u, kz - 0.02 * u), (sx * 0.34 * u, kz - 0.18 * u)]
        b.extrude([(x, front, z) for x, z in fluke], (0, -rel, 0), stone)
    # 막 — 아치 안쪽 윤곽(직사각 + 반원)보다 사방 TUCK만큼 큰 한 장, 앞뒤 가운데(y 0). 가장자리는 기둥·쐐기돌·
    # 받침 속에 묻힌다(돌이 속이 찬 덩어리라 막 가장자리가 돌 안으로 들어가 보이지 않는다). 양면 잎카드라 한 장.
    # PM 2차: 1·2차는 가장자리를 알파 노이즈로 흩었는데, 알파 컷은 톱니로 잘려 찢긴 종이·오려 붙인 판처럼 보였다.
    outline = [(-(half + TUCK), FLOOR - TUCK), (half + TUCK, FLOOR - TUCK)]
    for j in range(0, 17):
        a = math.pi * j / 16
        outline.append((math.cos(a) * (r_in + TUCK), spring + math.sin(a) * (r_in + TUCK)))
    verts = [b.bm.verts.new((x, 0.0, z)) for x, z in outline]
    face = b.bm.faces.new(verts)
    face.material_index = b.m(membrane)
    face.normal_update()
    if face.normal.y > 0:                          # 정면(−Y)을 보게
        face.normal_flip()
    b.marker("빛_자리_01", (0.0, 0.0, FLOOR + s["h"] / 2))
    return b, dict(inner=(s["w"], s["h"]), limit=s["limit"], spring=spring, r_in=r_in)


# ──────────────────────────────────────────────────────────── 셰이더

def shade_stone(mat, size):
    nt = mat.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    co = tc.outputs["Object"]
    base = _mix(nt, _noise(nt, co, 9.0), (0.11, 0.105, 0.095, 1), (0.24, 0.23, 0.2, 1))
    grain = _mix(nt, _math(nt, "MULTIPLY", _noise(nt, co, 45.0, detail=8.0), 0.5), base, (0.07, 0.065, 0.06, 1))
    # 비바람 얼룩(세로로 흘러내림)
    mp = _node(nt, "ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (1.0, 1.0, 0.25)
    _link(nt, co, mp.inputs["Vector"])
    streak = _ramp(nt, _noise(nt, mp.outputs["Vector"], 14.0), [(0.0, (0, 0, 0, 1)), (0.55, (0, 0, 0, 1)), (0.72, (0.6, 0.6, 0.6, 1))])
    color = _mix(nt, streak, grain, (0.05, 0.05, 0.045, 1))
    # 위를 보는 면에 이끼
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    up = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, geo.outputs["Normal"], up.inputs[0])
    patch = _ramp(nt, _noise(nt, co, 7.0, detail=6.0), [(0.0, (0, 0, 0, 1)), (0.5, (0, 0, 0, 1)), (0.62, (1, 1, 1, 1))])
    moss = _math(nt, "MULTIPLY", patch, _math(nt, "MAXIMUM", up.outputs[2], 0.0))
    color = _mix(nt, moss, color, (0.035, 0.06, 0.015, 1))
    return color, None


def shade_moss_joint(mat, size):
    nt = mat.node_tree
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    return _mix(nt, _noise(nt, co, 25.0, detail=6.0), (0.015, 0.03, 0.008, 1), (0.05, 0.085, 0.02, 1)), None


def shade_membrane(mat, color_key, spring, r_in, floor_z):
    """빛나는 막 — 가운데 밝고 가장자리로 짙어지는 소용돌이 빛. 알파는 윤곽 가까이에서 노이즈로 흩어진다."""
    nt = mat.node_tree
    edge_col, core_col, rim_col = MEMBRANE[color_key]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    co = tc.outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    # 막 가운데를 원점으로 한 (x, z) — 아래는 직사각, 위는 반원이라 타원 거리로 근사(m 단위)
    cz = (floor_z + spring + r_in) / 2 / UNITS
    half_h = (spring + r_in - floor_z) / 2 / UNITS
    half_w = r_in / UNITS
    dx = _math(nt, "DIVIDE", sep.outputs[0], half_w)
    dz = _math(nt, "DIVIDE", _math(nt, "SUBTRACT", sep.outputs[2], cz), half_h)
    dist = _math(nt, "SQRT", _math(nt, "ADD", _math(nt, "MULTIPLY", dx, dx), _math(nt, "MULTIPLY", dz, dz)), 0.0)
    # 소용돌이 — 극좌표 각을 거리로 비틀어 노이즈에 넣는다
    angle = _math(nt, "ARCTAN2", dz, dx)
    twist = _math(nt, "ADD", angle, _math(nt, "MULTIPLY", dist, 4.0))
    swirl_vec = nt.nodes.new("ShaderNodeCombineXYZ")
    _link(nt, _math(nt, "MULTIPLY", _math(nt, "COSINE", twist, 0.0), dist), swirl_vec.inputs[0])
    _link(nt, _math(nt, "MULTIPLY", _math(nt, "SINE", twist, 0.0), dist), swirl_vec.inputs[1])
    swirl = _noise(nt, swirl_vec.outputs[0], 3.0, detail=4.0)
    glow = _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", dist, 0.85))
    heat = _math(nt, "ADD", _math(nt, "MULTIPLY", glow, 0.75), _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", swirl, 0.5), 0.6))
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.15, (*[c * 0.25 for c in edge_col], 1)
    els[1].position, els[1].color = 0.95, (*core_col, 1)
    mid = els.new(0.5)
    mid.color = (*edge_col, 1)
    _link(nt, heat, ramp.inputs[0])
    color = ramp.outputs["Color"]
    # 가장자리 — 알파가 아니라 색으로(PM 2차): 돌 윤곽까지의 실제 거리(아래 직사각은 기둥 면, 위는 반원, 바닥은 받침
    # 윗면)가 0.8 안쪽이면 거의 검정 적갈로 어두워진다 — 돌 틈에서 빛이 새어 나오는 느낌. 알파는 전부 1(구멍 없음).
    above = _math(nt, "MAXIMUM", _math(nt, "SUBTRACT", sep.outputs[2], spring / UNITS), 0.0)
    radial = _math(nt, "SQRT", _math(nt, "ADD", _math(nt, "MULTIPLY", sep.outputs[0], sep.outputs[0]),
                                     _math(nt, "MULTIPLY", above, above)), 0.0)
    edge = _math(nt, "MINIMUM", _math(nt, "SUBTRACT", r_in / UNITS, radial),
                 _math(nt, "SUBTRACT", sep.outputs[2], floor_z / UNITS))
    wob = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", _noise(nt, co, 25.0, detail=3.0), 0.5), 0.3 / UNITS)
    rim = nt.nodes.new("ShaderNodeMapRange")
    _link(nt, _math(nt, "ADD", edge, wob), rim.inputs["Value"])
    rim.inputs["From Min"].default_value, rim.inputs["From Max"].default_value = 0.0, 0.8 / UNITS
    rim.inputs["To Min"].default_value, rim.inputs["To Max"].default_value = 1.0, 0.0
    color = _mix(nt, rim.outputs["Result"], color, (*rim_col, 1))
    return color, color


def shade(obj, size, color_key, info):
    for slot in obj.material_slots:
        mat = slot.material
        mat.use_nodes = True
        nt = mat.node_tree
        for node in list(nt.nodes):
            if node.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
                nt.nodes.remove(node)
        if mat.name.startswith("포탈_막_"):
            color, emit = shade_membrane(mat, color_key, info["spring"], info["r_in"], FLOOR)
        elif mat.name.endswith("_이끼줄눈"):
            color, emit = shade_moss_joint(mat, size)
        else:
            color, emit = shade_stone(mat, size)
        bsdf = nt.nodes["Principled BSDF"]
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.9
        _link(nt, color, bsdf.inputs["Base Color"])
        if emit is not None:
            _link(nt, emit, bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = 2.0


# ──────────────────────────────────────────────────────────── 조립·굽기·내보내기

def _pixels(img):
    import numpy as np
    arr = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    return arr


def tex_size(name):
    return 1024 if name.endswith("_돌") or "_막_" in name else 512


def bake(obj, folder):
    """재질마다 DIFFUSE 색을 굽고, 막(`_잎카드`)은 EMIT로 알파를 따로 구워 합친다. 장면 설정은 되돌린다."""
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
        images, nodes = {}, {}
        for slot in obj.material_slots:
            mat = slot.material
            for stale in (mat.name, mat.name + "_알파"):
                old = bpy.data.images.get(stale)
                if old is not None:
                    bpy.data.images.remove(old)
            img = bpy.data.images.new(mat.name, tex_size(mat.name), tex_size(mat.name), alpha=mat.name in MASKS)
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = img
            for n in mat.node_tree.nodes:
                n.select = False
            node.select = True
            mat.node_tree.nodes.active = node
            images[mat.name], nodes[mat.name] = img, node
        bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_clear=False, margin=8)
        if any(name in MASKS for name in images):
            dummy = bpy.data.images.new("_버림", 64, 64)
            restore = {}
            alphas = {}
            for name, node in nodes.items():
                nt = bpy.data.materials[name].node_tree
                if name in MASKS:
                    alphas[name] = bpy.data.images.new(name + "_알파", tex_size(name), tex_size(name))
                    node.image = alphas[name]
                    emit = nt.nodes.new("ShaderNodeEmission")
                    nt.links.new(MASKS[name], emit.inputs["Color"])
                    out = nt.nodes["Material Output"]
                    restore[name] = (emit, out.inputs["Surface"].links[0].from_socket)
                    nt.links.new(emit.outputs[0], out.inputs["Surface"])
                else:
                    node.image = dummy
            bpy.ops.object.bake(type="EMIT", use_clear=False, margin=8)
            for name, (emit, keep) in restore.items():
                nt = bpy.data.materials[name].node_tree
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
        scene.render.engine, scene.cycles.samples = saved
        for o in _live_objects():
            o.select_set(o in selected)
        view.objects.active = active


def assemble(name, collection):
    size, color_key = VARIANTS[name]
    MASKS.clear()
    b, info = build_portal(size, color_key)
    bm = b.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    lx, ly = info["limit"]
    assert min(xs) >= -lx - 1e-3 and max(xs) <= lx + 1e-3 and min(ys) >= -ly - 1e-3 and max(ys) <= ly + 1e-3, \
        f"{name} 바닥 {2 * lx}×{2 * ly}를 넘었다: x {min(xs):.2f}~{max(xs):.2f}, y {min(ys):.2f}~{max(ys):.2f}"
    assert abs(min(zs)) < 1e-3, f"{name} 최저점 {min(zs):.3f}"
    membrane_index = b.mats.index(f"포탈_막_{color_key}_발광_잎카드")
    mverts = {v for f in bm.faces if f.material_index == membrane_index for v in f.verts}
    mw = max(v.co.x for v in mverts) - min(v.co.x for v in mverts)
    mh = max(v.co.z for v in mverts) - min(v.co.z for v in mverts)
    # 막은 안쪽 윤곽보다 사방 TUCK 크다 — 막 크기에서 되짚어 안쪽 폭×높이를 확인한다
    mw, mh = mw - 2 * TUCK, mh - 2 * TUCK
    assert abs(mw - info["inner"][0]) < 0.01 and abs(mh - info["inner"][1]) < 0.01, f"{name} 안쪽 {mw:.2f}×{mh:.2f} ≠ {info['inner']}"
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    assert tri_count <= 2000, f"{name} 삼각형 {tri_count} > 2,000"
    bounds = (min(xs), max(xs), min(ys), max(ys), max(zs), mw, mh)
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for mat_name in b.mats:
        mesh.materials.append(bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name))
    for marker, at in b.markers:
        empty = bpy.data.objects.new(marker, None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = 0.1
        empty.location = at / UNITS
        empty.parent = obj
        collection.objects.link(empty)
    shade(obj, size, color_key, info)
    return obj, [m for m, _ in b.markers], bounds


def build_in_window(name, collection, folder):
    obj, markers, bounds = assemble(name, collection)
    unwrap(obj)
    bake(obj, folder)
    return obj


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(VARIANTS)
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        obj, markers, bounds = assemble(name, col)
        unwrap(obj)
        bake(obj, os.path.join(OUT, "Textures"))
        names = sorted(c.name for c in obj.children)
        assert names == sorted(markers), f"빈 오브젝트 이름이 밀렸다: {names}"
        for o in _live_objects():
            o.select_set(o is obj or o.parent is obj)
        bpy.context.view_layer.objects.active = obj
        path = os.path.join(OUT, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"만듦  {name}  삼각형 {tris}  재질 {[s.material.name for s in obj.material_slots]}  빈 오브젝트 {names}  "
              f"x {bounds[0]:.2f}~{bounds[1]:.2f} y {bounds[2]:.2f}~{bounds[3]:.2f} z ~{bounds[4]:.2f}  안쪽 {bounds[5]:.2f}×{bounds[6]:.2f}  → {path}")


if __name__ == "__main__":
    main()
