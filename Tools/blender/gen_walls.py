"""맵 벽 조각(사실적 화풍) — 돌담·성벽·울타리·기둥·정의문 17종을 만들어 FBX로 내보낸다.

화면 없이 돈다(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_walls.py
    blender --background --factory-startup --python Tools/blender/gen_walls.py -- 돌담_ 정의문
사장님 창에서는 이 파일을 exec해 build_live()만 부른다(show_all.py의 extra 훅) — 창 모양 = FBX 모양.

사장님 확정(2026-09-12): 나무·바위처럼 벽도 **사실적 화풍**. 절차적 셰이더를 이미지로 굽고, 조각은
평면 투영 UV로 그 텍스처를 탄다. 텍스처는 변형끼리 공유(돌 4·나무 2·대나무·밧줄·금·금속 = 10장).

🔴 조각 규격 4가지 고정(PM 유니티 코드가 벽 길이만큼 복제해 붙이고 끝을 맞춰 늘린다 — 같은 규격끼리
바꿔 끼울 수 있어야 한다). 치수는 게임 단위(사람 키 20). 내보낼 때 1/11.4로 미터로 바꾸고 11.4배로 내보낸다.
    두꺼운 벽 8×5.5×7 · 얇은 벽 6×5.5×1.4 · 울타리 6×5.5×1.0 · 기둥 2.2×7×2.2 · 문 20.6×7×1.4
🔴 이어 붙이는 조각은 양 끝면이 x=±길이/2에서 평평하고(끝 점은 안 흔든다), 반복 무늬의 주기가 길이를 딱
나눠야 둘을 붙여도 틈·튀어나온 것이 없다. 삼각형 조각당 1,200 이하(한 벽에 50장 넘게 붙는다).
🔴 재질 이름 약속: 텍스처는 Assets/Art/Walls/Textures/<재질이름>.png(512²). 투명이 필요한 재질만 이름 끝
`_잎카드`(지금은 없음 — 대나무 틈·밧줄은 기하로 뚫었다). FBX는 path_mode="RELATIVE"(../Textures 상대경로), 재질은
이름으로 공유해 `.001`이 안 붙는다.
⚠️ 같은 씨앗이면 같은 모양이지만 FBX 바이트는 매번 달라진다 — 고친 것만 이름으로 골라 내보낸다.
씨앗 대역: 벽 400번대.
"""

import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Walls")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")
LIVE_COLLECTION = "판_벽"

# 규격(길이 X × 높이 × 두께 Y, 게임 단위)
THICK, THIN, FENCE, PILLAR, GATE = (8.0, 5.5, 7.0), (6.0, 5.5, 1.4), (6.0, 5.5, 1.0), (2.2, 7.0, 2.2), (20.6, 7.0, 1.4)

# 재질 — (이름, 텍스처 타일 한 변(게임 단위), 뷰포트 색). 셰이더는 아래 SHADERS.
MATERIALS = {
    "돌_막돌":   (4.0, (0.46, 0.46, 0.44, 1.0)),
    "돌_마름돌":  (4.0, (0.56, 0.55, 0.52, 1.0)),
    "돌_이끼":   (4.0, (0.40, 0.46, 0.36, 1.0)),
    "돌_해안":   (4.0, (0.32, 0.36, 0.38, 1.0)),
    "벽돌":     (2.0, (0.55, 0.32, 0.24, 1.0)),
    "나무_판":   (3.0, (0.52, 0.40, 0.27, 1.0)),
    "나무_통나무": (2.0, (0.40, 0.30, 0.20, 1.0)),
    "대나무":    (2.5, (0.62, 0.62, 0.32, 1.0)),
    "밧줄":     (0.8, (0.60, 0.50, 0.34, 1.0)),
    "금_장식":   (4.0, (0.85, 0.72, 0.30, 1.0)),
    "금속_어두움": (2.0, (0.30, 0.26, 0.20, 1.0)),
}
MAT_INDEX = {name: i for i, name in enumerate(MATERIALS)}
BAKE_SIZE = 512


# ──────────────────────────────────────────────────────────── 재질(절차적 → 굽기)

def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    mat.diffuse_color = MATERIALS[name][1]
    return mat


def _tree(mat):
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    coord = nt.nodes.new("ShaderNodeTexCoord")
    return nt, bsdf, coord.outputs["UV"]


def _math(nt, op, a, b=None, clamp=False):
    n = nt.nodes.new("ShaderNodeMath")
    n.operation = op
    n.use_clamp = clamp
    for k, val in enumerate((a, b)):
        if val is None:
            continue
        if isinstance(val, (int, float)):
            n.inputs[k].default_value = val
        else:
            nt.links.new(val, n.inputs[k])
    return n.outputs[0]


def _noise(nt, uv, scale, detail=4.0, roughness=0.5):
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = roughness
    nt.links.new(uv, n.inputs["Vector"])
    return n.outputs["Fac"]


def _voronoi(nt, uv, scale, feature="F1", randomness=1.0):
    v = nt.nodes.new("ShaderNodeTexVoronoi")
    v.feature = feature
    v.inputs["Scale"].default_value = scale
    v.inputs["Randomness"].default_value = randomness
    nt.links.new(uv, v.inputs["Vector"])
    return v


def _ramp(nt, fac, stops):
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = stops[0]
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = stops[-1]
    for pos, col in stops[1:-1]:
        e = ramp.color_ramp.elements.new(pos)
        e.color = col
    nt.links.new(fac, ramp.inputs["Fac"])
    return ramp.outputs["Color"]


def _mix(nt, fac, a, b, blend="MIX"):
    m = nt.nodes.new("ShaderNodeMixRGB")
    m.blend_type = blend
    for k, val in ((0, fac), (1, a), (2, b)):
        if isinstance(val, (int, float)):
            m.inputs[k].default_value = val
        elif isinstance(val, tuple):
            m.inputs[k].default_value = val
        else:
            nt.links.new(val, m.inputs[k])
    return m.outputs["Color"]


def _sep_uv(nt, uv):
    s = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(uv, s.inputs["Vector"])
    return s.outputs["X"], s.outputs["Y"]


def shader_rubble(mat, mossy=False, wet=False):
    """막돌 — 보로노이 셀이 돌 하나, 틈은 짙게, 돌마다 색이 조금씩 다르고 결이 있다."""
    nt, bsdf, uv = _tree(mat)
    cells = _voronoi(nt, uv, 5.0, "DISTANCE_TO_EDGE", 0.9)
    gap = _math(nt, "MULTIPLY", cells.outputs["Distance"], 9.0, clamp=True)        # 0 틈 → 1 돌 가운데
    tint = _voronoi(nt, uv, 5.0, "F1", 0.9).outputs["Color"]
    grain = _noise(nt, uv, 24.0, 6.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", grain, 0.35), _math(nt, "MULTIPLY", gap, 0.65), clamp=True)
    if wet:
        base = _ramp(nt, tone, ((0.0, (0.05, 0.06, 0.07, 1)), (0.3, (0.16, 0.19, 0.21, 1)), (1.0, (0.36, 0.40, 0.42, 1))))
    else:
        base = _ramp(nt, tone, ((0.0, (0.12, 0.11, 0.10, 1)), (0.3, (0.36, 0.35, 0.33, 1)), (1.0, (0.62, 0.60, 0.56, 1))))
    color = _mix(nt, 0.25, base, tint, "OVERLAY")
    if mossy:
        moss = _math(nt, "GREATER_THAN", _math(nt, "ADD", _noise(nt, uv, 3.0, 3.0), _math(nt, "MULTIPLY", gap, -0.4)), 0.55)
        color = _mix(nt, _math(nt, "MULTIPLY", moss, 0.85), color, (0.22, 0.38, 0.14, 1))
    if wet:
        speck = _math(nt, "LESS_THAN", _voronoi(nt, uv, 40.0, "F1").outputs["Distance"], 0.12)
        color = _mix(nt, _math(nt, "MULTIPLY", speck, 0.7), color, (0.78, 0.76, 0.70, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.55 if wet else 0.9


def shader_ashlar(mat):
    """마름돌 — 매끈한 회색 돌, 옅은 결과 얼룩. 블록 모양은 기하가 낸다."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    bands = _math(nt, "SINE", _math(nt, "ADD", _math(nt, "MULTIPLY", v, 18.0), _math(nt, "MULTIPLY", _noise(nt, uv, 4.0), 6.0)))
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", _noise(nt, uv, 9.0, 5.0), 0.55), _math(nt, "MULTIPLY", bands, 0.08))
    tone = _math(nt, "ADD", tone, _math(nt, "MULTIPLY", _noise(nt, uv, 2.0, 2.0), 0.3), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.30, 0.30, 0.28, 1)), (0.5, (0.52, 0.51, 0.48, 1)), (1.0, (0.70, 0.69, 0.65, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8


def shader_brick(mat):
    """벽돌 — 어긋나게 쌓은 직사각형, 줄눈은 밝은 회색, 벽돌마다 붉은 기 다르게. 타일 한 변에 4단 × 2장."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    row = _math(nt, "MULTIPLY", v, 4.0)
    shift = _math(nt, "MULTIPLY", _math(nt, "MODULO", _math(nt, "FLOOR", row), 2.0), 0.5)
    col = _math(nt, "ADD", _math(nt, "MULTIPLY", u, 2.0), shift)
    fy = _math(nt, "FRACT", row)
    fx = _math(nt, "FRACT", col)
    mortar_y = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fy, 0.5)), 0.44)
    mortar_x = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", fx, 0.5)), 0.47)
    brick = _math(nt, "MULTIPLY", mortar_y, mortar_x)
    cell_id = _math(nt, "ADD", _math(nt, "FLOOR", col), _math(nt, "MULTIPLY", _math(nt, "FLOOR", row), 7.0))
    hue = _math(nt, "FRACT", _math(nt, "MULTIPLY", _math(nt, "SINE", _math(nt, "MULTIPLY", cell_id, 12.9898)), 43758.5))
    grain = _noise(nt, uv, 30.0, 5.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", hue, 0.5), _math(nt, "MULTIPLY", grain, 0.5), clamp=True)
    brick_col = _ramp(nt, tone, ((0.0, (0.42, 0.20, 0.14, 1)), (0.5, (0.58, 0.30, 0.20, 1)), (1.0, (0.70, 0.44, 0.30, 1))))
    color = _mix(nt, brick, (0.68, 0.66, 0.60, 1), brick_col)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85


def shader_wood(mat, bark=False):
    """나무 — 결(파동 무늬)을 노이즈로 흔든다. bark=True면 통나무 껍질(굵고 짙은 세로 골)."""
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"
    wave.bands_direction = "X" if bark else "Y"
    wave.inputs["Scale"].default_value = 10.0 if bark else 14.0
    wave.inputs["Distortion"].default_value = 5.0 if bark else 2.5
    wave.inputs["Detail"].default_value = 3.0
    nt.links.new(uv, wave.inputs["Vector"])
    grain = _noise(nt, uv, 40.0, 6.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", wave.outputs["Fac"], 0.6), _math(nt, "MULTIPLY", grain, 0.4), clamp=True)
    if bark:
        color = _ramp(nt, tone, ((0.0, (0.14, 0.09, 0.06, 1)), (0.5, (0.32, 0.22, 0.14, 1)), (1.0, (0.50, 0.38, 0.26, 1))))
    else:
        seam = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", u, 4.0)), 0.5)), 0.47)
        color = _ramp(nt, tone, ((0.0, (0.28, 0.19, 0.11, 1)), (0.5, (0.50, 0.37, 0.23, 1)), (1.0, (0.68, 0.54, 0.36, 1))))
        color = _mix(nt, seam, (0.16, 0.11, 0.07, 1), color)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8


def shader_bamboo(mat):
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    node = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", v, 3.0)), 0.5)), 0.04)
    streak = _math(nt, "MULTIPLY", _noise(nt, uv, 6.0, 2.0), _math(nt, "ADD", 0.6, _math(nt, "MULTIPLY", _math(nt, "SINE", _math(nt, "MULTIPLY", u, 60.0)), 0.4)))
    color = _ramp(nt, streak, ((0.0, (0.42, 0.40, 0.16, 1)), (0.5, (0.62, 0.60, 0.28, 1)), (1.0, (0.76, 0.72, 0.40, 1))))
    color = _mix(nt, node, color, (0.28, 0.24, 0.10, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.5


def shader_rope(mat):
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    twist = _math(nt, "SINE", _math(nt, "MULTIPLY", _math(nt, "ADD", _math(nt, "MULTIPLY", u, 3.0), _math(nt, "MULTIPLY", v, 8.0)), math.tau))
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", twist, 0.4), _math(nt, "MULTIPLY", _noise(nt, uv, 20.0), 0.5), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.30, 0.22, 0.12, 1)), (0.5, (0.55, 0.44, 0.28, 1)), (1.0, (0.74, 0.62, 0.42, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9


def shader_metal(mat, gold=False):
    nt, bsdf, uv = _tree(mat)
    scratch = nt.nodes.new("ShaderNodeTexWave")
    scratch.wave_type = "BANDS"
    scratch.bands_direction = "DIAGONAL"
    scratch.inputs["Scale"].default_value = 30.0
    scratch.inputs["Distortion"].default_value = 8.0
    nt.links.new(uv, scratch.inputs["Vector"])
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", _noise(nt, uv, 5.0, 4.0), 0.7), _math(nt, "MULTIPLY", scratch.outputs["Fac"], 0.3), clamp=True)
    if gold:
        color = _ramp(nt, tone, ((0.0, (0.55, 0.40, 0.12, 1)), (0.5, (0.85, 0.70, 0.28, 1)), (1.0, (1.0, 0.90, 0.52, 1))))
    else:
        color = _ramp(nt, tone, ((0.0, (0.12, 0.10, 0.08, 1)), (0.5, (0.28, 0.24, 0.18, 1)), (1.0, (0.44, 0.38, 0.28, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.35 if gold else 0.6
    bsdf.inputs["Metallic"].default_value = 0.8 if gold else 0.5


SHADERS = {
    "돌_막돌": lambda m: shader_rubble(m),
    "돌_마름돌": shader_ashlar,
    "돌_이끼": lambda m: shader_rubble(m, mossy=True),
    "돌_해안": lambda m: shader_rubble(m, wet=True),
    "벽돌": shader_brick,
    "나무_판": lambda m: shader_wood(m),
    "나무_통나무": lambda m: shader_wood(m, bark=True),
    "대나무": shader_bamboo,
    "밧줄": shader_rope,
    "금_장식": lambda m: shader_metal(m, gold=True),
    "금속_어두움": lambda m: shader_metal(m),
}


def bake_all(out_dir=None, size=BAKE_SIZE, only=None):
    """재질마다 절차적 셰이더를 UV 0~1 평면에 구워 PNG로 저장하고, 재질을 그 이미지 재질로 바꾼다.
    셰이더가 UV만 쓰므로 평면 한 장에 구운 것이 조각 위에서도 똑같다(조각은 타일 UV로 반복)."""
    out_dir = out_dir or TEX_DIR
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    plane = bpy.context.active_object
    plane.name = "굽기판"
    prev = (scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed)
    scene.render.engine = "CYCLES"
    scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = 4, False, 0
    scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 2
    scene.render.bake.use_clear = False
    written = []
    for name in MATERIALS:
        if only and name not in only:
            continue
        mat = material(name)
        SHADERS[name](mat)
        plane.data.materials.clear()
        plane.data.materials.append(mat)
        old = bpy.data.images.get(name)
        if old:
            bpy.data.images.remove(old)
        img = bpy.data.images.new(name, size, size)
        nt = mat.node_tree
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.nodes.active = tex
        bpy.ops.object.select_all(action="DESELECT")
        plane.select_set(True)
        bpy.context.view_layer.objects.active = plane
        bpy.ops.object.bake(type="DIFFUSE")
        img.filepath_raw = os.path.join(out_dir, f"{name}.png")
        img.file_format = "PNG"
        img.save()
        img.reload()
        # 이미지 재질로 교체(거칠기·금속성은 셰이더 값 유지)
        rough = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED").inputs["Roughness"].default_value
        metal = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED").inputs["Metallic"].default_value
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        nt.nodes.active = tex
        written.append(img.filepath_raw)
    scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = prev
    mesh = plane.data
    bpy.data.objects.remove(plane, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    return written


# ──────────────────────────────────────────────────────────── 메시 도구

def build_object(bm, material_names, name, collection, meters):
    """bmesh → 오브젝트. 면마다 평면(박스) 투영 UV를 준다 — 재질의 타일 크기로 나눠 텍스처가 반복된다.
    원점은 바닥면 한가운데(짓는 좌표 그대로). meters=True면 게임 단위를 미터로 바꾼다(내보내기용)."""
    uv = bm.loops.layers.uv.new("UVMap")
    bm.normal_update()
    for face in bm.faces:
        tile = MATERIALS[material_names[face.material_index]][0]
        n = face.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in face.loops:
            co = loop.vert.co
            if ax == 0:
                loop[uv].uv = (co.y / tile, co.z / tile)
            elif ax == 1:
                loop[uv].uv = (co.x / tile, co.z / tile)
            else:
                loop[uv].uv = (co.x / tile, co.y / tile)
    used = sorted({f.material_index for f in bm.faces})
    remap = {old: new for new, old in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    if meters:
        bmesh.ops.scale(bm, vec=Vector((1.0, 1.0, 1.0)) / UNITS_PER_METER, verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for old in used:                          # 쓰는 재질만 — 안 쓰는 슬롯이 유니티까지 따라가지 않게
        mesh.materials.append(material(material_names[old]))
    return obj


BOX_FACES = {"bottom": (0, 3, 2, 1), "top": (4, 5, 6, 7), "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6), "-x": (3, 0, 4, 7), "+x": (1, 2, 6, 5)}


def add_box(bm, x0, x1, y0, y1, z0, z1, m, skip=()):
    corners = [bm.verts.new(Vector(p)) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                                                (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for face_name, index in BOX_FACES.items():
        if face_name not in skip:
            bm.faces.new([corners[i] for i in index]).material_index = m


def bridge(bm, lower, upper, m):
    n = len(lower)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lower[i], lower[j], upper[j], upper[i])).material_index = m


def cap(bm, ring, center, m, up=True):
    c = bm.verts.new(center)
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((c, ring[i], ring[j]) if up else (c, ring[j], ring[i])).material_index = m


def ring_at(bm, center, radius, sides, spin=0.0, ry=None):
    return [bm.verts.new(Vector((center[0] + math.cos(spin + math.tau * i / sides) * radius,
                                 center[1] + math.sin(spin + math.tau * i / sides) * (ry or radius), center[2]))) for i in range(sides)]


def cylinder(bm, x, y, z0, z1, radius, sides, m, top="cap", top_radius=None, base_cap=False, spin=0.0):
    """세로 원기둥. top: cap(평평) · point(뾰족) · none."""
    lower = ring_at(bm, (x, y, z0), radius, sides, spin)
    upper = ring_at(bm, (x, y, z1), top_radius if top_radius is not None else radius, sides, spin)
    bridge(bm, lower, upper, m)
    if base_cap:
        cap(bm, lower, (x, y, z0), m, up=False)
    if top == "cap":
        cap(bm, upper, (x, y, z1), m)
    elif top == "point":
        cap(bm, upper, (x, y, z1 + radius * 1.6), m)


def tube(bm, points, radius, sides, m):
    """점 목록을 따라가는 관(밧줄)."""
    rings = []
    for i, p in enumerate(points):
        d = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((1, 0, 0))
        u = ref.cross(d).normalized()
        w = d.cross(u)
        rings.append([bm.verts.new(p + (u * math.cos(math.tau * k / sides) + w * math.sin(math.tau * k / sides)) * radius) for k in range(sides)])
    for a, b in zip(rings, rings[1:]):
        bridge(bm, a, b, m)
    cap(bm, rings[0], points[0], m, up=False)
    cap(bm, rings[-1], points[-1], m)


# ──────────────────────────────────────────────────────────── 돌 벽

WALL_ROWS = ((0.00, 1.00), (0.27, 0.95), (0.53, 0.91), (0.78, 0.87), (0.84, 0.97), (1.00, 0.93))


def make_rubble_wall(seed, size, columns, stone, ruin=False, rounded=False):
    """막돌 담 — 단면을 길이 방향으로 뽑고 안쪽 점만 흔든다. 양 끝 열은 안 흔든다(이어 붙임).
    ruin=True면 가운데 열의 높이가 무너져 내려앉는다(끝 열은 그대로라 옆 조각과 맞는다).
    rounded=True면 갓돌 턱 없이 둥글게(파도에 깎인 방파제)."""
    rng = random.Random(seed)
    length, height, thickness = size
    bm = bmesh.new()
    half, step = thickness / 2, length / columns
    rows = ((0.0, 1.0), (0.3, 0.97), (0.6, 0.94), (0.85, 0.86), (1.0, 0.6)) if rounded else WALL_ROWS
    top = len(rows) - 1
    relief = max(thickness * 0.025, height * 0.02)

    def side(sign):
        grid = []
        for i in range(columns + 1):
            edge = i in (0, columns)
            drop = 0.0 if (edge or not ruin) else rng.uniform(0.0, 0.35) * height * math.sin(math.pi * i / columns)
            cap_x, cap_reach = rng.uniform(-0.2, 0.2) * step, rng.uniform(-0.02, 0.02) * thickness
            column = []
            for j, (z_ratio, half_ratio) in enumerate(rows):
                x, z, reach = -length / 2 + step * i, height * z_ratio - drop * z_ratio, half * half_ratio
                if not edge:
                    if j >= 3 and not rounded:
                        x += cap_x
                        reach += cap_reach
                    else:
                        x += rng.uniform(-0.2, 0.2) * step
                        if 0 < j < top:
                            z += rng.uniform(-0.05, 0.05) * height
                        reach += (-rng.uniform(0.0, 1.0) if j == 0 else rng.uniform(-1.0, 1.0)) * relief
                    reach = min(reach, half)
                column.append(bm.verts.new(Vector((x, sign * reach, z))))
            grid.append(column)
        return grid

    front, back = side(-1.0), side(1.0)
    m = MAT_INDEX[stone]
    for i in range(columns):
        for j in range(top):
            bm.faces.new((front[i][j], front[i + 1][j], front[i + 1][j + 1], front[i][j + 1])).material_index = m
            bm.faces.new((back[i + 1][j], back[i][j], back[i][j + 1], back[i + 1][j + 1])).material_index = m
        bm.faces.new((front[i][top], front[i + 1][top], back[i + 1][top], back[i][top])).material_index = m
    for i, facing_left in ((0, True), (columns, False)):
        ring = [front[i][j] for j in range(top + 1)] + [back[i][j] for j in reversed(range(top + 1))]
        middle = bm.verts.new(Vector((front[i][0].co.x, 0.0, height * 0.5)))
        for k in range(len(ring)):
            a, b = ring[k], ring[(k + 1) % len(ring)]
            bm.faces.new((middle, a, b) if facing_left else (middle, b, a)).material_index = m
    return bm, list(MATERIALS)


def make_ashlar_wall(seed, size, stone, crenellated=False, block_len=2.0, row_h=1.1, gap=0.07):
    """마름돌 성벽 — 반듯한 블록을 한 단마다 반 칸씩 어긋나게 쌓는다. 블록 사이 틈이 줄눈이다.
    끝 블록은 x=±길이/2에서 딱 잘라 두 조각을 붙이면 줄눈 하나 폭이 된다."""
    rng = random.Random(seed)
    length, height, thickness = size
    bm = bmesh.new()
    m = MAT_INDEX[stone]
    rows = int(round(height / row_h))
    for r in range(rows):
        z0, z1 = r * row_h + (gap / 2 if r else 0.0), (r + 1) * row_h - gap / 2
        offset = block_len / 2 if r % 2 else 0.0
        x = -length / 2 - offset
        while x < length / 2 - 1e-6:
            x0, x1 = max(x, -length / 2), min(x + block_len, length / 2)
            at_end = x0 <= -length / 2 + 1e-6 or x1 >= length / 2 - 1e-6
            inset = 0.0 if at_end else rng.uniform(0.0, 0.05)   # 끝 블록은 안 들여놓는다 — 양 끝면이 같아야 이어 붙는다
            add_box(bm, x0 + (gap / 2 if x0 > -length / 2 + 1e-6 else 0.0), x1 - (gap / 2 if x1 < length / 2 - 1e-6 else 0.0),
                    -thickness / 2 + inset, thickness / 2 - inset, z0, z1, m, skip=("bottom",) if r == 0 else ())
            x += block_len
    if crenellated:
        period = length / 4.0                              # 총안 4개 — 주기가 길이를 나눠 이어 붙여도 규칙적이다
        for k in range(4):
            cx = -length / 2 + period * (k + 0.5)
            add_box(bm, cx - period * 0.28, cx + period * 0.28, -thickness / 2 + 0.05, thickness / 2 - 0.05, height, height + 1.3, m, skip=("bottom",))
    return bm, list(MATERIALS)


def make_brick_wall(seed, size):
    """벽돌담 — 몸통 + 위에 살짝 넓은 갓돌 단. 무늬는 텍스처가 낸다."""
    length, height, thickness = size
    bm = bmesh.new()
    add_box(bm, -length / 2, length / 2, -thickness / 2, thickness / 2, 0.0, height - 0.3, MAT_INDEX["벽돌"], skip=("bottom", "top"))
    add_box(bm, -length / 2, length / 2, -thickness / 2 - 0.12, thickness / 2 + 0.12, height - 0.3, height, MAT_INDEX["돌_마름돌"])
    return bm, list(MATERIALS)


# ──────────────────────────────────────────────────────────── 울타리

def make_plank_fence(seed, size, planks=6):
    """널판 울타리 — 끝이 뾰족한 널판과 뒤의 가로대 둘. 널판 사이 틈을 양 끝에 반씩 둔다."""
    rng = random.Random(seed)
    length, height, thickness = size
    bm = bmesh.new()
    m, m_rail = MAT_INDEX["나무_판"], MAT_INDEX["나무_통나무"]
    width = length / planks
    gap = width * 0.08
    front, plank_back, back = -thickness / 2, -thickness / 2 + thickness * 0.55, thickness / 2
    tallest = rng.randrange(planks)
    for k in range(planks):
        x0 = -length / 2 + width * k + gap / 2
        x1 = x0 + width - gap
        peak = height if k == tallest else height * rng.uniform(0.9, 0.98)
        shoulder = peak - (x1 - x0) * 0.45
        xm = (x0 + x1) / 2
        a = [bm.verts.new(Vector(p)) for p in ((x0, front, 0.0), (x1, front, 0.0), (x1, front, shoulder), (xm, front, peak), (x0, front, shoulder))]
        b = [bm.verts.new(Vector(p)) for p in ((x0, plank_back, 0.0), (x1, plank_back, 0.0), (x1, plank_back, shoulder), (xm, plank_back, peak), (x0, plank_back, shoulder))]
        for loop in ((a[0], a[1], a[2], a[3], a[4]), (b[1], b[0], b[4], b[3], b[2]), (a[1], b[1], b[2], a[2]), (b[0], a[0], a[4], b[4]), (a[2], b[2], b[3], a[3]), (a[3], b[3], b[4], a[4])):
            bm.faces.new(loop).material_index = m
    for low, high in ((0.22, 0.32), (0.66, 0.76)):
        add_box(bm, -length / 2, length / 2, plank_back - thickness * 0.05, back, height * low, height * high, m_rail)
    return bm, list(MATERIALS)


def make_palisade(seed, size, logs=6):
    """목책 — 뾰족하게 깎은 통나무 말뚝을 나란히 박고 뒤에 가로대 둘. 말뚝 간격 = 길이/개수."""
    rng = random.Random(seed)
    length, height, thickness = size
    bm = bmesh.new()
    m = MAT_INDEX["나무_통나무"]
    pitch = length / logs
    r = min(pitch * 0.46, thickness * 0.48)
    for k in range(logs):
        x = -length / 2 + pitch * (k + 0.5)
        top = height * rng.uniform(0.9, 1.0) - r * 1.6
        cylinder(bm, x, -thickness / 2 + r, 0.0, top, r * rng.uniform(0.92, 1.0), 7, m, top="point", spin=rng.uniform(0, 1))
    for z in (height * 0.3, height * 0.65):
        add_box(bm, -length / 2, length / 2, thickness / 2 - 0.25, thickness / 2, z - 0.18, z + 0.18, m)
    return bm, list(MATERIALS)


def make_rail_fence(seed, size):
    """목장 울타리 — 기둥 둘 + 가로대 셋. 가로대는 끝까지 뻗어 붙이면 한 줄이 된다. 기둥 간격 = 길이/2."""
    length, height, thickness = size
    bm = bmesh.new()
    m_post, m_rail = MAT_INDEX["나무_통나무"], MAT_INDEX["나무_판"]
    post = 0.42
    for x in (-length / 4, length / 4):
        add_box(bm, x - post / 2, x + post / 2, -thickness / 2, thickness / 2, 0.0, height, m_post, skip=("bottom",))
    for z in (height * 0.28, height * 0.56, height * 0.84):
        add_box(bm, -length / 2, length / 2, -thickness / 2 + 0.05, -thickness / 2 + 0.38, z - 0.16, z + 0.16, m_rail)
    return bm, list(MATERIALS)


def make_bamboo_fence(seed, size, poles=11):
    """대나무 울타리 — 가는 대를 촘촘히 세우고 가로 띠 둘로 묶는다. 대 사이 틈은 기하로 뚫려 있다."""
    rng = random.Random(seed)
    length, height, thickness = size
    bm = bmesh.new()
    m, m_tie = MAT_INDEX["대나무"], MAT_INDEX["밧줄"]
    pitch = length / poles
    r = pitch * 0.38
    for k in range(poles):
        x = -length / 2 + pitch * (k + 0.5)
        cylinder(bm, x, 0.0, 0.0, height * rng.uniform(0.94, 1.0), r, 6, m, top="cap", spin=rng.uniform(0, 1))
    for z in (height * 0.28, height * 0.72):
        for y in (-r - 0.08, r + 0.08):
            add_box(bm, -length / 2, length / 2, y - 0.07, y + 0.07, z - 0.12, z + 0.12, m_tie)
    return bm, list(MATERIALS)


def make_rope_rail(seed, size):
    """밧줄 난간 — 말뚝 둘(길이/2 간격)에 밧줄 두 줄이 처져 걸린다. 밧줄은 말뚝 사이에서 늘어지고 끝은
    이웃 조각과 같은 높이·같은 처짐이라 이어진다(주기 = 길이/2)."""
    length, height, thickness = size
    bm = bmesh.new()
    m_post, m_rope = MAT_INDEX["나무_통나무"], MAT_INDEX["밧줄"]
    span = length / 2
    r_post = min(thickness * 0.45, 0.38)
    for x in (-length / 4, length / 4):
        cylinder(bm, x, 0.0, 0.0, height, r_post, 8, m_post, top="cap")
    for h in (height * 0.5, height * 0.86):
        pts = []
        for k in range(25):
            x = -length / 2 + length * k / 24
            phase = ((x + length / 4) % span) / span              # 말뚝에서 0, 다음 말뚝에서 1
            sag = math.sin(math.pi * phase) * 0.45
            pts.append(Vector((x, 0.0, h - sag)))
        tube(bm, pts, 0.11, 5, m_rope)
    return bm, list(MATERIALS)


# ──────────────────────────────────────────────────────────── 기둥

def make_stone_pillar(seed, size, stone):
    rng = random.Random(seed)
    width, height, _ = size
    bm = bmesh.new()
    m = MAT_INDEX[stone]
    rings = []
    for k, (z_ratio, half_ratio) in enumerate(((0.0, 1.0), (0.1, 1.0), (0.13, 0.82), (0.5, 0.8), (0.87, 0.78), (0.9, 1.0), (1.0, 0.95))):
        half = width / 2 * half_ratio
        cut = half * 0.22
        pts = ((half, -(half - cut)), (half, half - cut), (half - cut, half), (-(half - cut), half), (-half, half - cut), (-half, -(half - cut)), (-(half - cut), -half), (half - cut, -half))
        ring = []
        for x, y in pts:
            if k == 3:
                x += rng.uniform(-0.04, 0.04) * half
                y += rng.uniform(-0.04, 0.04) * half
            ring.append(bm.verts.new(Vector((x, y, height * z_ratio))))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        bridge(bm, a, b, m)
    cap(bm, rings[-1], (0.0, 0.0, height), m)
    return bm, list(MATERIALS)


def make_wood_post(seed, size):
    """나무 기둥 — 네모 기둥에 덮개, 위쪽에 랜턴 걸이 팔과 랜턴(어두운 금속)."""
    width, height, _ = size
    bm = bmesh.new()
    m, m_metal = MAT_INDEX["나무_통나무"], MAT_INDEX["금속_어두움"]
    w = width * 0.32
    add_box(bm, -w, w, -w, w, 0.0, height - 0.25, m, skip=("bottom",))
    add_box(bm, -w * 1.5, w * 1.5, -w * 1.5, w * 1.5, height - 0.25, height, m)
    arm_z = height * 0.8
    add_box(bm, w, width * 0.95, -0.12, 0.12, arm_z - 0.14, arm_z + 0.14, m)
    add_box(bm, width * 0.62, width * 0.92, -0.32, 0.32, arm_z - 1.35, arm_z - 0.25, m_metal)
    return bm, list(MATERIALS)


# ──────────────────────────────────────────────────────────── 정의문

def make_gate(size):
    """정의문 — 금빛 두 짝 문. 기둥·윗보·띠·징·문양은 어두운 금속, 문짝은 금 질감."""
    length, height, thickness = size
    bm = bmesh.new()
    gold, dark = MAT_INDEX["금_장식"], MAT_INDEX["금속_어두움"]
    half_length, half_thick = length / 2, thickness / 2
    post, inner, door_top = 0.8, length / 2 - 0.8, height - 0.8
    leaf, strap, stud = thickness * 0.32, thickness * 0.44, thickness * 0.41
    for sign in (-1, 1):
        x0, x1 = sorted((sign * half_length, sign * inner))
        add_box(bm, x0, x1, -half_thick, half_thick, 0.0, height, dark, skip=("bottom",))
    add_box(bm, -inner, inner, -half_thick, half_thick, door_top, height, dark, skip=("-x", "+x"))
    add_box(bm, -inner, -0.06, -leaf, leaf, 0.0, door_top, gold, skip=("bottom", "top", "-x"))
    add_box(bm, 0.06, inner, -leaf, leaf, 0.0, door_top, gold, skip=("bottom", "top", "+x"))
    for side in (-1, 1):
        against = "+y" if side < 0 else "-y"
        y0, y1 = sorted((side * leaf, side * strap))
        for low, high in ((door_top * 0.18, door_top * 0.27), (door_top * 0.69, door_top * 0.79)):
            add_box(bm, -inner, inner, y0, y1, low, high, dark, skip=(against, "-x", "+x"))
        y0, y1 = sorted((side * leaf, side * stud))
        for x in (-inner * 2 / 3, -inner / 3, inner / 3, inner * 2 / 3):
            add_box(bm, x - 0.25, x + 0.25, y0, y1, 0.2, door_top - 0.2, dark, skip=(against,))
        mid = door_top / 2
        rim = [bm.verts.new(Vector(p)) for p in ((0.0, side * leaf, mid + door_top * 0.18), (0.9, side * leaf, mid), (0.0, side * leaf, mid - door_top * 0.18), (-0.9, side * leaf, mid))]
        apex = bm.verts.new(Vector((0.0, side * half_thick, mid)))
        for k in range(4):
            a, b = rim[k], rim[(k + 1) % 4]
            bm.faces.new((a, apex, b) if side < 0 else (a, b, apex)).material_index = dark
    return bm, list(MATERIALS)


# ──────────────────────────────────────────────────────────── 목록
#
# (이름, 만들기, 설명, 이어 붙이는가, 전시 줄, 이름표). 이름은 유니티가 참조하는 파일명 — 기존 5종은 그대로.

CATALOG = [
    ("돌담_두꺼움", lambda: make_rubble_wall(401, THICK, 8, "돌_막돌"), "8×5.5×7 막돌", True, "두꺼운 벽", "StoneWall_Thick"),
    ("성벽_마름돌", lambda: make_ashlar_wall(411, THICK, "돌_마름돌"), "8×5.5×7 마름돌 블록", True, "두꺼운 벽", "Ashlar_Wall"),
    ("폐허벽", lambda: make_rubble_wall(412, THICK, 8, "돌_이끼", ruin=True), "8×5.5×7 무너지고 이끼", True, "두꺼운 벽", "Ruin_Wall"),
    ("흉벽성벽", lambda: make_ashlar_wall(413, THICK, "돌_마름돌", crenellated=True), "8×5.5×7 위가 톱니(총안)", True, "두꺼운 벽", "Battlement"),
    ("돌담_얇음", lambda: make_rubble_wall(402, THIN, 6, "돌_막돌"), "6×5.5×1.4 막돌", True, "얇은 벽", "StoneWall_Thin"),
    ("석축_이끼", lambda: make_rubble_wall(421, THIN, 6, "돌_이끼"), "6×5.5×1.4 이끼 석축", True, "얇은 벽", "Mossy_Wall"),
    ("벽돌담", lambda: make_brick_wall(422, THIN), "6×5.5×1.4 벽돌", True, "얇은 벽", "Brick_Wall"),
    ("해안방파제", lambda: make_rubble_wall(423, THIN, 6, "돌_해안", rounded=True), "6×5.5×1.4 파도에 깎인 돌", True, "얇은 벽", "Breakwater"),
    ("나무울타리", lambda: make_plank_fence(404, FENCE), "6×5.5×1 널판", True, "울타리", "WoodFence"),
    ("목책", lambda: make_palisade(431, FENCE), "6×5.5×1 뾰족 통나무 말뚝", True, "울타리", "Palisade"),
    ("목장울타리", lambda: make_rail_fence(432, FENCE), "6×5.5×1 기둥+가로대", True, "울타리", "Rail_Fence"),
    ("대나무울타리", lambda: make_bamboo_fence(433, FENCE), "6×5.5×1 대나무", True, "울타리", "Bamboo_Fence"),
    ("밧줄난간", lambda: make_rope_rail(434, FENCE), "6×5.5×1 말뚝+밧줄", True, "울타리", "Rope_Rail"),
    ("돌기둥", lambda: make_stone_pillar(403, PILLAR, "돌_마름돌"), "2.2×7×2.2 돌", False, "기둥", "StonePillar"),
    ("나무기둥", lambda: make_wood_post(441, PILLAR), "2.2×7×2.2 나무+랜턴", False, "기둥", "WoodPost_Lantern"),
    ("이끼돌기둥", lambda: make_stone_pillar(442, PILLAR, "돌_이끼"), "2.2×7×2.2 이끼 돌", False, "기둥", "MossyPillar"),
    ("정의문", lambda: make_gate(GATE), "20.6×7×1.4 금빛 문", False, "문", "JusticeGate"),
]
ROW_ORDER = ("두꺼운 벽", "얇은 벽", "울타리", "기둥", "문")


# ──────────────────────────────────────────────────────────── 사장님 창에서 짓기

def build_groups(collection, kinds=None, meters=True):
    """CATALOG를 짓고 showcase 무리로 돌려준다 — {줄 이름: [무리, ...]}. 이어 붙이는 조각은 3개 붙이고
    뒤에 1.5배 늘린 3개. 재질은 굽지 않은 상태면 먼저 굽는다(창에서 한 번, 이후 재사용)."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import showcase
    if any(bpy.data.materials.get(n) is None or not any(x.type == "TEX_IMAGE" for x in bpy.data.materials[n].node_tree.nodes) for n in MATERIALS):
        bake_all()
    unit = 1.0 / UNITS_PER_METER if meters else 1.0
    rows = {}
    for name, maker, _, tiling, kind, label in CATALOG:
        if kinds and kind not in kinds:
            continue
        bm, material_names = maker()
        length = (max(v.co.x for v in bm.verts) - min(v.co.x for v in bm.verts)) * unit
        depth = (max(v.co.y for v in bm.verts) - min(v.co.y for v in bm.verts)) * unit
        original = build_object(bm, material_names, name, collection, meters=meters)
        parts = [(original, 0.0, 0.0)]
        if tiling:
            for k in (1, 2):
                twin = original.copy()
                collection.objects.link(twin)
                parts.append((twin, length * k, 0.0))
            for k in range(3):
                stretched = original.copy()
                collection.objects.link(stretched)
                stretched.scale.x = 1.5
                parts.append((stretched, length * 0.25 + length * 1.5 * k, depth + 0.1))
        rows.setdefault(kind, []).append(showcase.group(label, parts))
    return [(k, rows[k]) for k in ROW_ORDER if k in rows]


def rows_walls(collection):
    return build_groups(collection, kinds=("두꺼운 벽", "얇은 벽", "울타리", "기둥"))


def rows_gate(collection):
    return build_groups(collection, kinds=("문",))


# ──────────────────────────────────────────────────────────── 실행(화면 없이)

def clear_objects():
    """물체·메시만 지운다 — 구운 재질·이미지는 조각 사이에 재사용한다(재질 이름에 .001이 안 붙게)."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def export(obj, folder, name):
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(folder, f"{name}.fbx"), use_selection=True, global_scale=UNITS_PER_METER,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE",
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="RELATIVE")                # FBX 기준 상대경로(../Textures) — Blender에서 바로 열려도 텍스처가 붙는다.
                                             # STRIP은 파일명만 남겨 형제 폴더를 못 찾았다(구현담당1 검증). 유니티는 재질 이름으로 찾는다.


def main():
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    picked = [e for e in CATALOG if not prefixes or any(e[0].startswith(p) for p in prefixes)]
    if not picked:
        raise SystemExit(f"이름이 맞는 게 없다: {prefixes}")
    clear_objects()
    textures = bake_all()
    made = []
    for name, maker, note, *_ in picked:
        clear_objects()
        bm, material_names = maker()
        obj = build_object(bm, material_names, name, bpy.context.scene.collection, meters=True)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        export(obj, OUT_ROOT, name)
        made.append((name, note, tris))
    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_walls.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 사장님 창에서 짓고 확인한 뒤 화면 없이 내보냄\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20 = 1.75m). 치수는 게임 단위.\n"
            "원점: 바닥면 한가운데. 길이 X축. 이어 붙이는 조각은 양 끝면이 평평하고 무늬 주기가 길이를 나눈다.\n"
            "규격: 두꺼운 벽 8×5.5×7 · 얇은 벽 6×5.5×1.4 · 울타리 6×5.5×1 · 기둥 2.2×7×2.2 · 문 20.6×7×1.4\n"
            f"재질(텍스처 Textures/<이름>.png, 512²): {', '.join(MATERIALS)} — 전부 불투명.\n\n"
            "만들어진 것:\n" + "".join(f"  {n:8} {d}\n" for n, _, d, *_ in CATALOG))
    print("=" * 60)
    for name, note, tris in made:
        print(f"만듦  {name:8} 삼각형 {tris:5d}  {note}")
    print(f"총 {len(made)}개, 텍스처 {len(textures)}장 → {OUT_ROOT}")


if __name__ == "__main__":
    main()
