"""사실적인 바위 20종을 Blender로 만들어 텍스처와 함께 FBX로 내보낸다.

화면 없이 돈다:
    blender --background --factory-startup --python Tools/blender/gen_rocks.py
    blender --background --factory-startup --python Tools/blender/gen_rocks.py -- 이끼바위_ 뾰족바위_

뒤에 이름 앞부분을 주면 그것만 만든다(안 주면 전부). 검증은 Tools/blender/check_nature_fbx.py
--limit 1500으로 한다(바위는 나무·풀의 150개 기준이 아니라 PM이 정한 1500개 기준이다).

⚠️ gen_nature.py(나무·풀·기존 저폴리 바위)와는 완전히 별개 스크립트다 — 나무 작업(구현담당1)
과 파일이 안 겹치게 PM이 나눴다. gen_nature.py는 손대지 않는다 — 겹치는 유틸(finish·export
등)은 여기 따로 복사해 뒀다(가져다 쓰지 않는다, 서로 독립적으로 고칠 수 있게).

⚠️ 크기 기준·결정성·씨앗 대역 규칙은 gen_nature.py와 같다:
  - 1m ≈ 11.4 게임 단위(사람 키 20 = 1.75m).
  - 같은 씨앗이면 같은 모양(단, FBX 바이트 자체는 내보낼 때마다 달라진다).
  - 씨앗은 바위 100번대를 쓴다. 기존 9종(101~105·151·152·161·162)은 그대로 두고,
    새 11종은 170번대부터 쓴다(2026-09-12, PM 지시).

⚠️ 기존 9종(바위_01~05·판석_01·02·바위무리_01·02)은 **크기를 지금과 같게** 유지해야 한다
(PM 지시) — 그래서 이 아홉만은 원래 알고리즘(2026-09-04/09-12 gen_nature.py 원본)의 난수
소비 순서를 그대로 지킨다: 거칠기 푸시 → 가로세로 눌림(sx,sy,sz) → 바닥 클램프 → z회전.
모서리를 깎는 새 단계는 **별도의(메인 시드와 안 섞이는) 난수열**로 맨 뒤에 더한다 — 크기를
정하는 난수 순서에 새 단계가 끼어들면 같은 시드라도 다른 크기가 나온다. 판석 2종은 그마저도
안 건다(윗면이 완전히 평평해야 하는데, 챔퍼가 그 테두리를 건드릴 위험이 있어 텍스처만
새로 입힌다 — 기존 지오메트리 100% 그대로).

⚠️ 텍스처: 절차적 돌 셰이더(보로노이+노이즈)를 Cycles로 재질마다 **한 번만** 굽는다
(재질 4종을 쓰는 대표 오브젝트 하나씩 — 20개를 다 구우면 재질 공유 의미가 없다).
결과 PNG는 Assets/Art/Nature/Textures/<재질이름>.png(512², 재질 이름과 같은 파일명) —
FBX에는 안 박는다(path_mode="RELATIVE" — AUTO는 절대경로 소스를 내보낼 때 경로가 깨진다,
gen_nature.py가 먼저 겪고 고친 문제와 동일). 노이즈·보로노이는 UV가 아니라 오브젝트
좌표(TexCoord.Object)로 평가한다 — 그래야 서로 다른 UV를 가진 다른 바위들이 같은 이미지를
공유해도 자연스럽게 보인다. UV는 굽기 대상 텍셀 배치용으로만 쓴다(box_uv 참고, 구면 좌표 —
세로(V)는 오브젝트 실제 높이를 0~1로 그대로 쓴다. 이끼 재질이 "v>0.62=이끼"로 구워지면,
그 재질을 쓰는 다른 바위도 자기 윗면(자기 몸의 위쪽 부분)에서 자동으로 이끼가 보인다).

⚠️ 삼각형 상한 1,500(PM 지시, 벽 조각과 같은 기준 — 자연물 150 기준이 아니다).
"""

import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Vector

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Nature")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")
UP = Vector((0.0, 0.0, 1.0))
TEX_SIZE = 512

# 별도(메인 시드와 안 섞이는) 난수열을 만들 때 더하는 오프셋 — 기존 9종의 크기 결정 난수
# 순서를 안 건드리려고 모서리 깎기 선택은 이 오프셋을 더한 시드로 딴 스트림을 쓴다.
CHAMFER_SEED_OFFSET = 100000


# ──────────────────────────────────────────────────────────── 준비(gen_nature.py와 같은 관례, 독립 사본)

def finish(obj, name):
    """바닥에 딱 붙는 원점으로 맞추고 각진 음영으로 바꾼다."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co.z -= lowest

    bpy.ops.object.shade_flat()
    obj.name = name
    obj.data.name = name
    return obj


def fit_height(obj, height_m):
    """최고점이 정확히 height_m이 되도록 통째로 늘리거나 줄인다. finish() 뒤에 부른다."""
    scale = height_m / max(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co *= scale


def export(obj, folder, name):
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.export_scene.fbx(
        filepath=os.path.join(folder, f"{name}.fbx"),
        use_selection=True,
        global_scale=UNITS_PER_METER,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        # ⚠️ path_mode="RELATIVE" — AUTO로 두면 이미지의 절대경로 소스를 내보내면서
        # "Rocks/Users/.../Textures/Stone_A.png" 같은 뒤섞인 경로를 박아 재임포트 시
        # image.has_data=False(분홍 재질)가 된다(gen_nature.py가 먼저 겪은 버그, 직접
        # 재현해서 확인함). RELATIVE는 텍스처를 FBX에 박지 않으면서도 경로가 실제로
        # 유효하다 — 유니티 쪽 코드는 어차피 재질 이름으로 Textures/<이름>.png를
        # 다시 찾아 붙이므로 무관하지만, Blender에서 직접 열어 봐도 정상적으로 보인다.
        path_mode="RELATIVE",
    )


def frame(axis):
    axis = axis.normalized()
    ref = UP if abs(axis.z) < 0.95 else Vector((1.0, 0.0, 0.0))
    u = ref.cross(axis).normalized()
    return u, axis.cross(u)


def add_ring(bm, center, axis, radii, spin=0.0):
    u, w = frame(axis)
    verts = []
    for i, radius in enumerate(radii):
        angle = spin + math.tau * i / len(radii)
        verts.append(bm.verts.new(center + (u * math.cos(angle) + w * math.sin(angle)) * radius))
    return verts


def bridge(bm, lower, upper, material_index):
    count = len(lower)
    for i in range(count):
        j = (i + 1) % count
        bm.faces.new((lower[i], lower[j], upper[j], upper[i])).material_index = material_index


def cap(bm, ring, center, material_index, facing_axis):
    middle = bm.verts.new(center)
    count = len(ring)
    for i in range(count):
        j = (i + 1) % count
        loop = (middle, ring[i], ring[j]) if facing_axis else (middle, ring[j], ring[i])
        bm.faces.new(loop).material_index = material_index


def point(bm, ring, apex, material_index):
    tip = bm.verts.new(apex)
    count = len(ring)
    for i in range(count):
        bm.faces.new((ring[i], ring[(i + 1) % count], tip)).material_index = material_index


# ──────────────────────────────────────────────────────────── UV — 구면 좌표(직접 계산)

def box_uv(bm, density=1.0):
    """구면 좌표로 UV를 만든다. 세로(V)는 오브젝트의 실제 최저~최고 높이를 0~1로 그대로
    쓴다(위로 갈수록 1) — 랩(모듈로)이 필요 없는 축이라 찢어질 일이 없다. 가로(U)는
    방위각(atan2)×density라 경도가 한 바퀴(또는 density>1이면 여러 바퀴) 돌 때마다
    랩이 생기는데, 면마다 첫 정점을 기준으로 나머지 정점을 "가장 가까운 바퀴"로
    맞춘다(정점별로 독립적으로 0~1에 욱여넣으면 wrap 경계에 걸친 면 하나가 이미지
    전체 폭으로 찢어져 늘어난다 — 2026-09-12 실제로 겪은 사고, 첫 굽기 결과 이미지에
    거대한 대각선 줄무늬가 나왔다. 내장 bpy.ops.uv.sphere_project()도 시도해봤지만
    이 버전에서 v가 실제 높이와 상관관계가 없어 버렸다 — 그래서 직접 계산한다).

    이 v를 stone_material의 이끼 마스크가 그대로 읽는다("UV.y > 0.65") — 셋으로 미리
    눌러 담는 밴드가 아니라 원래 값 그대로라서, 어떤 모양의 바위든 "자기 몸의 위쪽
    35%"가 항상 그 조건을 만족한다(모양마다 달라지는 밴드 경계 불일치가 없다)."""
    uv = bm.loops.layers.uv.new("UVMap")
    zs = [v.co.z for v in bm.verts]
    z_lo, z_hi = min(zs), max(zs)
    z_span = max(z_hi - z_lo, 1e-6)

    def raw(co):
        v = (co.z - z_lo) / z_span
        u = (math.atan2(co.y, co.x) / math.tau + 0.5) * density
        return u, v

    for face in bm.faces:
        pts = [raw(loop.vert.co) for loop in face.loops]
        u0 = pts[0][0]
        # (u - u0 + 0.5) % 1.0 - 0.5 → u0에서 u까지 "가장 가까운 방향"의 부호 있는 거리.
        fixed = [(u0 + ((u - u0 + 0.5) % 1.0 - 0.5), v) for u, v in pts]
        for loop, (u, v) in zip(face.loops, fixed):
            loop[uv].uv = (u, v)


# ──────────────────────────────────────────────────────────── 재질(절차적 → 굽기)

# (어두운 색, 밝은 색, 이끼 여부) — 램프 양 끝 색. 이끼는 Stone_A와 같은 돌결에 이끼만 얹는다.
STONE_PALETTE = {
    "Stone_A": ((0.30, 0.30, 0.31), (0.58, 0.58, 0.57), False),
    "Stone_B": ((0.28, 0.22, 0.15), (0.62, 0.52, 0.38), False),
    "Stone_C": ((0.10, 0.10, 0.11), (0.34, 0.34, 0.35), False),
    "Stone_Moss": ((0.30, 0.30, 0.31), (0.58, 0.58, 0.57), True),
}


def stone_material(name):
    """이름이 같으면 재사용한다(굽기 뒤엔 이미지 재질이라 그걸 그대로 돌려준다).
    보로노이(깨진 결)+노이즈(잔 알갱이)를 섞어 돌색으로 램프한다 — 노이즈·보로노이는 UV가
    아니라 오브젝트 좌표를 읽어서, 서로 다른 UV를 가진 여러 바위가 같은 이미지를 공유해도
    자연스럽게 이어져 보인다."""
    if name in bpy.data.materials:
        return bpy.data.materials[name]

    dark, light, moss = STONE_PALETTE[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.88

    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (2.2, 2.2, 2.2)
    nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])

    voronoi = nt.nodes.new("ShaderNodeTexVoronoi")
    voronoi.inputs["Scale"].default_value = 5.0
    nt.links.new(mapping.outputs["Vector"], voronoi.inputs["Vector"])

    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 16.0
    noise.inputs["Detail"].default_value = 6.0
    noise.inputs["Roughness"].default_value = 0.65
    nt.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])

    mix = nt.nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 0.55
    nt.links.new(voronoi.outputs["Distance"], mix.inputs["Color1"])
    nt.links.new(noise.outputs["Factor"], mix.inputs["Color2"])

    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*dark, 1.0)
    ramp.color_ramp.elements[0].position = 0.2
    ramp.color_ramp.elements[1].color = (*light, 1.0)
    ramp.color_ramp.elements[1].position = 0.85
    nt.links.new(mix.outputs["Color"], ramp.inputs["Factor"])

    base_color_socket = ramp.outputs["Color"]

    if moss:
        # 🔴 2026-09-12 정정(2차) — PM 렌더 검수(dbc2118b 이후): 여전히 큰 초록 "덩어리".
        # 원인 — 1차 수정도 마스크의 주 원료가 높이(v) 하나뿐이라, v>~0.745인 면은
        # 노이즈 진폭(0.18)이 못 뒤집을 만큼 통째로 위쪽 절반이 이끼가 됐다(PNG를 직접
        # 봐야 알 수 있는 문제 — v 그라데이션 자체가 얼룩이 아니라 띠였다). 이번엔 마스크의
        # 주 원료를 노이즈로 바꾸고 높이는 가중치만 얹는다 — 그래야 한 높이 안에서도
        # 돌·이끼가 섞인다.
        uv_sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(coord.outputs["UV"], uv_sep.inputs["Vector"])

        patch_noise = nt.nodes.new("ShaderNodeTexNoise")
        patch_noise.inputs["Scale"].default_value = 8.0
        patch_noise.inputs["Detail"].default_value = 4.0
        patch_noise.inputs["Roughness"].default_value = 0.6
        nt.links.new(mapping.outputs["Vector"], patch_noise.inputs["Vector"])

        height_weight = nt.nodes.new("ShaderNodeMath")
        height_weight.operation = "MULTIPLY"
        nt.links.new(uv_sep.outputs["Y"], height_weight.inputs[0])
        height_weight.inputs[1].default_value = 0.15   # 가중치만 — 위쪽에 살짝 더 나도록

        combined = nt.nodes.new("ShaderNodeMath")
        combined.operation = "ADD"
        nt.links.new(patch_noise.outputs["Factor"], combined.inputs[0])
        nt.links.new(height_weight.outputs[0], combined.inputs[1])

        # combined 분포를 실측(probe_noise_dist.py)해 문턱을 잡았다 — 0.55~0.75처럼 폭을
        # 넓게 두면 대부분 픽셀이 "약하게만" 섞여 회색에 묻혀 안 보인다(1차 재시도 때
        # 실측: 강한 초록<0.08> 3%뿐). 0.60(≈면적 상위 37%)~0.66(폭 0.06)로 좁혀 — 문턱을
        # 넘은 면적 대부분이 빠르게 진한 이끼색까지 차오르고, 가장자리만 얇게 섞인다.
        mask = nt.nodes.new("ShaderNodeMapRange")
        mask.clamp = True
        nt.links.new(combined.outputs[0], mask.inputs["Value"])
        mask.inputs["From Min"].default_value = 0.60
        mask.inputs["From Max"].default_value = 0.66
        mask.inputs["To Min"].default_value = 0.0
        mask.inputs["To Max"].default_value = 1.0
        mask_out = mask.outputs["Result"]

        # 이끼 위에도 돌 결(voronoi×noise의 mix 출력)을 30%만 곱해 평평한 색 면을 없앤다.
        grain = nt.nodes.new("ShaderNodeMath")
        grain.operation = "MULTIPLY_ADD"
        grain.use_clamp = True
        nt.links.new(mix.outputs["Color"], grain.inputs[0])
        grain.inputs[1].default_value = 0.3
        grain.inputs[2].default_value = 0.7

        moss_shaded = nt.nodes.new("ShaderNodeMixRGB")
        moss_shaded.blend_type = "MULTIPLY"
        moss_shaded.inputs["Factor"].default_value = 1.0
        moss_shaded.inputs["Color1"].default_value = (0.10, 0.15, 0.05, 1.0)  # 짙은 회녹
        nt.links.new(grain.outputs[0], moss_shaded.inputs["Color2"])

        moss_mix = nt.nodes.new("ShaderNodeMixRGB")
        nt.links.new(mask_out, moss_mix.inputs["Factor"])
        nt.links.new(ramp.outputs["Color"], moss_mix.inputs["Color1"])
        nt.links.new(moss_shaded.outputs["Color"], moss_mix.inputs["Color2"])
        base_color_socket = moss_mix.outputs["Color"]

        rough_mix = nt.nodes.new("ShaderNodeMixRGB")
        rough_mix.inputs["Color1"].default_value = (0.88, 0.88, 0.88, 1.0)
        rough_mix.inputs["Color2"].default_value = (0.97, 0.97, 0.97, 1.0)
        nt.links.new(mask_out, rough_mix.inputs["Factor"])
        nt.links.new(rough_mix.outputs["Color"], bsdf.inputs["Roughness"])

    nt.links.new(base_color_socket, bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (*light, 1.0)
    return mat


def _bake(obj, mat, image):
    """오브젝트 하나·재질 하나를 이미지로 굽는다."""
    scene = bpy.context.scene
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.nodes.active = tex
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    prev = (scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    scene.cycles.use_denoising = False
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 10
    # 바위는 재질 하나에 이미지 하나뿐이다(잎처럼 색+마스크를 따로 구워 합치지 않는다) —
    # 그래서 매번 지우고 구워도 안전하다(tree_c.py의 use_clear=False 경고는 한 재질에
    # 이미지를 두 번 이상 구울 때 얘기다, 여기는 해당 없음).
    scene.render.bake.use_clear = True
    bpy.ops.object.bake(type="DIFFUSE")
    scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising = prev
    nt.nodes.remove(tex)


def _to_image_material(mat, image):
    """절차적 노드 트리를 지우고 구운 이미지 하나를 참조하는 단순한 재질로 바꾼다."""
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.88
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.nodes.active = tex


def _bake_host():
    """굽기 전용 표준 지오메트리(반지름 1m, 분할 4) — 실제 바위 20종은 0.18m 자갈부터
    2.6m 절벽 조각까지 15배 차이가 나는데, 그중 아무거나로 구우면 노이즈·보로노이 눈금이
    바위마다 들쭉날쭉해진다(맨 처음 이렇게 했다가 0.35m짜리로 구운 Stone_A가 거대한 민짜
    삼각형 몇 개로만 보였다 — 그 작은 크기에선 보로노이 세포 하나가 바위 전체보다 컸다).
    내보내지 않는 굽기 전용 오브젝트라 실제 바위와 위상이 달라도 상관없다."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    box_uv(bm, density=1.0)
    mesh = bpy.data.meshes.new("_bake_host")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("_bake_host", mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def bake_all(material_names, out_dir):
    """재질마다 표준 호스트 하나로만 굽는다 — 20개를 다 구우면 재질 공유가 무의미하고,
    실제 바위로 구우면 위 _bake_host 주석의 눈금 문제가 생긴다."""
    os.makedirs(out_dir, exist_ok=True)
    results = {}
    for name in material_names:
        mat = bpy.data.materials[name]
        host = _bake_host()
        host.data.materials.append(mat)
        img = bpy.data.images.new(name + "_baked", TEX_SIZE, TEX_SIZE)
        _bake(host, mat, img)
        path = os.path.join(out_dir, f"{name}.png")
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        img.reload()
        _to_image_material(mat, img)
        bpy.data.objects.remove(host, do_unlink=True)
        results[name] = path
    return results


def material_for(name):
    return stone_material(name)


# ──────────────────────────────────────────────────────────── 짓기 유틸

def build_object(bm, material_name):
    box_uv(bm)
    mesh = bpy.data.meshes.new("임시")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material_for(material_name))

    obj = bpy.data.objects.new("임시", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    return obj


def chamfer(bm, rng, prob, offset_ratio=0.2):
    """모서리 일부만 챔퍼(segments=1, 각진 절단면) — 전부 깎으면 다시 둥글어져서 "깨진" 느낌이
    사라진다. offset은 골라낸 모서리들의 평균 길이에 비례해서 정한다(고정값을 쓰면 자갈처럼
    작은 바위에서 오프셋이 모서리보다 길어져 겹침이 심해진다)."""
    pick = [e for e in bm.edges if rng.random() < prob]
    if not pick:
        return
    avg_len = sum(e.calc_length() for e in pick) / len(pick)
    if avg_len <= 1e-6:
        return
    bmesh.ops.bevel(bm, geom=pick, offset=avg_len * offset_ratio, segments=1,
                     affect="EDGES", clamp_overlap=True)


# ──────────────────────────────────────────────────────────── 기존 9종 — 옛 수식 그대로(크기 보존) + 챔퍼만 추가

def make_rock(seed, radius_m, subdivisions=2, roughness=0.30, chamfer_prob=0.18):
    """구를 찌그러뜨려 바위를 만든다 — gen_nature.py 원본과 같은 난수 순서(거칠기→sx,sy,sz→
    z회전)라 크기가 그대로다. 모서리 깎기만 별도 난수열로 맨 뒤에 더한다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    ret = bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius_m)
    verts = ret["verts"]

    for v in verts:
        v.co += v.co.normalized() * (rng.uniform(-roughness, roughness) * radius_m)

    sx, sy, sz = rng.uniform(0.9, 1.35), rng.uniform(0.9, 1.35), rng.uniform(0.55, 0.85)
    for v in verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))

    floor = -radius_m * sz * 0.45
    for v in verts:
        if v.co.z < floor:
            v.co.z = floor

    spin = rng.uniform(0, math.tau)          # 원본의 obj.rotation_euler.z와 같은 자리(같은 난수 소비 순서)
    cos_a, sin_a = math.cos(spin), math.sin(spin)
    for v in verts:
        x, y = v.co.x, v.co.y
        v.co.x, v.co.y = x * cos_a - y * sin_a, x * sin_a + y * cos_a

    # offset_ratio를 낮게 잡는다(기본 0.2가 아니라 0.10) — 크기 보존이 중요한 기존 9종
    # 중 바위_04가 처음엔 세로가 +21%까지 늘었다(챔퍼가 저분할(분할 2) 아이코스피어의
    # 뾰족한 꼭짓점 근처를 깎으면서, 깎아 넣는 게 아니라 오히려 바깥으로 살짝 밀려난
    # 경우가 있었다).
    chamfer(bm, random.Random(seed + CHAMFER_SEED_OFFSET), chamfer_prob, offset_ratio=0.10)
    return bm


def make_slab(seed, width_m, thickness_m):
    """판석 — gen_nature.py 원본 그대로(윗면 완전 평평). 챔퍼를 안 건다 — 테두리 모서리를
    깎으면 "완전히 평평한 윗면"이라는 판석의 정체성이 흔들릴 위험이 있어서다(원본 주석
    "여기에 난수를 섞으면 평평한 윗면이 깨진다"와 같은 이유). 텍스처만 새로 입는다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    sides = 9
    spin = rng.uniform(0, math.tau)
    stretch = rng.uniform(0.70, 0.85)
    outline = [width_m / 2 * rng.uniform(0.85, 1.05) for _ in range(sides)]

    def ring_at(inset, z):
        verts = []
        for i, radius in enumerate(outline):
            angle = spin + math.tau * i / sides
            verts.append(bm.verts.new(Vector((
                math.cos(angle) * radius * inset,
                math.sin(angle) * radius * inset * stretch,
                z))))
        return verts

    bottom = ring_at(0.90, 0.0)
    belt = ring_at(1.00, thickness_m * 0.62)
    top = ring_at(0.86, thickness_m)
    bridge(bm, bottom, belt, 0)
    bridge(bm, belt, top, 0)
    cap(bm, top, Vector((0.0, 0.0, thickness_m)), 0, facing_axis=True)
    cap(bm, bottom, Vector((0.0, 0.0, 0.0)), 0, facing_axis=False)
    return bm


def add_rock_lump(bm, rng, x, y, radius, subdivisions, roughness=0.30):
    """gen_nature.py의 add_rock_lump와 같은 수식 — 바위무리(클러스터)의 크기를 보존한다."""
    verts = bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)["verts"]
    for v in verts:
        v.co += v.co.normalized() * (rng.uniform(-roughness, roughness) * radius)

    sx, sy, sz = rng.uniform(0.9, 1.35), rng.uniform(0.9, 1.35), rng.uniform(0.55, 0.85)
    floor = -radius * sz * 0.45
    spin = rng.uniform(0, math.tau)
    cos_a, sin_a = math.cos(spin), math.sin(spin)
    for v in verts:
        px, py, pz = v.co.x * sx, v.co.y * sy, max(v.co.z * sz, floor)
        v.co = Vector((x + px * cos_a - py * sin_a, y + px * sin_a + py * cos_a, pz - floor))
    return verts


def make_rock_cluster(seed, small_count, chamfer_prob=0.15):
    """바위무리 — gen_nature.py 원본과 같은 수식(크기 보존) + 별도 난수열로 챔퍼만 추가."""
    rng = random.Random(seed)
    bm = bmesh.new()
    add_rock_lump(bm, rng, 0.0, 0.0, 1.0, subdivisions=2)

    angle = rng.uniform(0, math.tau)
    for _ in range(small_count):
        angle += rng.uniform(0.9, 1.4)
        reach = rng.uniform(1.45, 1.8)
        add_rock_lump(bm, rng, math.cos(angle) * reach, math.sin(angle) * reach,
                      rng.uniform(0.30, 0.48), subdivisions=1, roughness=0.15)

    # offset_ratio를 낮게 잡는다(make_rock과 같은 이유) — 기본값(0.2)으로는 바위무리_01의
    # 가로·앞뒤가 10% 넘게 줄었다(여러 덩어리가 겹치는 자리라 챔퍼가 뾰족한 끝을 깎아내는
    # 효과가 단일 바위보다 크게 나타난다).
    chamfer(bm, random.Random(seed + CHAMFER_SEED_OFFSET), chamfer_prob, offset_ratio=0.10)
    return bm


# ──────────────────────────────────────────────────────────── 새 11종(2026-09-12) — 자유 설계

def make_boulder(seed, radius_m, subdivisions=2, roughness=0.22, layers=0, facet=0.5,
                  chamfer_prob=0.2, squash=None):
    """사실적인 바위 하나 — 층진 면(결 축으로 정점을 부분적으로 당겨 "선반"을 만든다)·
    거친 표면·모서리 깎기를 한 흐름으로 짓는다. 새로 만드는 11종 전용(기존 9종은 크기
    보존을 위해 make_rock의 옛 수식을 그대로 쓴다)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    ret = bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius_m)
    verts = ret["verts"]

    if layers > 0:
        axis = Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 1.0)).normalized()
        step = (radius_m * 2) / layers
        for v in verts:
            h = v.co.dot(axis)
            shelf = round(h / step) * step
            v.co += axis * (shelf - h) * facet

    for v in verts:
        v.co += v.co.normalized() * (rng.uniform(-roughness, roughness) * radius_m)

    sx, sy, sz = squash or (rng.uniform(0.85, 1.3), rng.uniform(0.85, 1.3), rng.uniform(0.5, 0.85))
    for v in verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))

    if chamfer_prob > 0:
        chamfer(bm, rng, chamfer_prob, offset_ratio=0.22)

    floor = -radius_m * sz * 0.45
    for v in bm.verts:
        if v.co.z < floor:
            v.co.z = floor
    return bm


def make_spire(seed, sides=8, levels=6):
    """뾰족바위 — 아래는 굵고 위로 갈수록 좁아지다 끝이 뾰족한 첨탑형. 허리를 사인 곡선으로
    잘록하게 해 침식 결(층)을 흉내낸다. 비율로 짓는다(전체 높이 = 1, fit_height가 맞춘다)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    spin = rng.uniform(0, math.tau)
    freq = rng.uniform(2.0, 3.5)

    rings = []
    for i in range(levels):
        t = i / (levels - 1)
        waist = 1.0 + 0.12 * math.sin(t * math.pi * freq)
        radius = (1.0 - t) ** 1.3 * 0.42 * waist + 0.03
        radii = [radius * rng.uniform(0.85, 1.15) for _ in range(sides)]
        rings.append(add_ring(bm, Vector((0.0, 0.0, t)), UP, radii, spin))

    for lower, upper in zip(rings, rings[1:]):
        bridge(bm, lower, upper, 0)
    cap(bm, rings[0], Vector((0.0, 0.0, 0.0)), 0, facing_axis=False)
    point(bm, rings[-1], Vector((0.0, 0.0, 1.05)), 0)

    chamfer(bm, random.Random(seed + CHAMFER_SEED_OFFSET), 0.15, offset_ratio=0.18)
    return bm


def make_gravel_patch(seed, count=8, spread=1.0):
    """자갈 무리 — 바위무리와 달리 큰 것 없이 비슷한 크기의 작은 돌이 낮게 흩어져 있다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    for _ in range(count):
        angle = rng.uniform(0, math.tau)
        reach = rng.uniform(0.0, spread)
        x, y = math.cos(angle) * reach, math.sin(angle) * reach
        r = rng.uniform(0.35, 0.55)

        verts = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=r)["verts"]
        for v in verts:
            v.co += v.co.normalized() * (rng.uniform(-0.12, 0.12) * r)
        sx, sy, sz = rng.uniform(0.9, 1.25), rng.uniform(0.9, 1.25), rng.uniform(0.5, 0.75)
        floor = -r * sz * 0.45
        for v in verts:
            px, py, pz = v.co.x * sx, v.co.y * sy, max(v.co.z * sz, floor)
            v.co = Vector((x + px, y + py, pz - floor))
    return bm


# ──────────────────────────────────────────────────────────── 목록
#
# (폴더, 이름, 짓기(bm 반환), 재질, 맞출 높이 m(None이면 안 맞춤), 설명)

CATALOG = [
    # 기존 9종 — 씨앗·크기 그대로, 텍스처 + 부분 챔퍼만 새로.
    ("Rocks", "바위_01", lambda: make_rock(seed=101, radius_m=0.35), "Stone_A", None, "반지름 약 0.35m"),
    ("Rocks", "바위_02", lambda: make_rock(seed=102, radius_m=0.6), "Stone_A", None, "반지름 약 0.6m"),
    ("Rocks", "바위_03", lambda: make_rock(seed=103, radius_m=0.9), "Stone_A", None, "반지름 약 0.9m"),
    ("Rocks", "바위_04", lambda: make_rock(seed=104, radius_m=1.4), "Stone_C", None, "반지름 약 1.4m"),
    ("Rocks", "바위_05", lambda: make_rock(seed=105, radius_m=2.1), "Stone_C", None, "반지름 약 2.1m"),
    ("Rocks", "판석_01", lambda: make_slab(seed=151, width_m=0.7, thickness_m=0.12),
     "Stone_B", 0.12, "폭 약 0.7m, 두께 0.12m, 윗면 평평"),
    ("Rocks", "판석_02", lambda: make_slab(seed=152, width_m=1.1, thickness_m=0.18),
     "Stone_B", 0.18, "폭 약 1.1m, 두께 0.18m, 윗면 평평"),
    ("Rocks", "바위무리_01", lambda: make_rock_cluster(seed=161, small_count=3),
     "Stone_A", 1.0, "높이 1.0m, 큰 것 1 + 작은 것 3"),
    ("Rocks", "바위무리_02", lambda: make_rock_cluster(seed=162, small_count=2),
     "Stone_A", 1.6, "높이 1.6m, 큰 것 1 + 작은 것 2"),

    # 새 11종(2026-09-12, PM 지시) — 씨앗 170번대.
    ("Rocks", "이끼바위_01",
     lambda: make_boulder(seed=170, radius_m=0.5, subdivisions=2, roughness=0.24, layers=3,
                           facet=0.4, chamfer_prob=0.15),
     "Stone_Moss", None, "반지름 약 0.5m, 이끼"),
    ("Rocks", "이끼바위_02",
     lambda: make_boulder(seed=171, radius_m=0.85, subdivisions=3, roughness=0.22, layers=4,
                           facet=0.45, chamfer_prob=0.15),
     "Stone_Moss", None, "반지름 약 0.85m, 이끼"),
    ("Rocks", "뾰족바위_01", lambda: make_spire(seed=172, sides=7, levels=6),
     "Stone_C", 1.3, "높이 1.3m, 첨탑형"),
    ("Rocks", "뾰족바위_02", lambda: make_spire(seed=173, sides=8, levels=7),
     "Stone_C", 2.0, "높이 2.0m, 첨탑형"),
    ("Rocks", "해안바위_01",
     lambda: make_boulder(seed=174, radius_m=0.5, subdivisions=2, roughness=0.12, layers=5,
                           facet=0.55, chamfer_prob=0.45, squash=(1.3, 1.1, 0.42)),
     "Stone_B", None, "반지름 약 0.5m, 파도에 깎임"),
    ("Rocks", "해안바위_02",
     lambda: make_boulder(seed=175, radius_m=0.9, subdivisions=3, roughness=0.10, layers=6,
                           facet=0.6, chamfer_prob=0.45, squash=(1.35, 1.05, 0.4)),
     "Stone_B", None, "반지름 약 0.9m, 파도에 깎임"),
    ("Rocks", "둥근강돌_01",
     lambda: make_boulder(seed=176, radius_m=0.18, subdivisions=2, roughness=0.06, layers=0,
                           chamfer_prob=0.55, squash=(1.15, 1.0, 0.75)),
     "Stone_B", None, "반지름 약 0.18m, 매끈"),
    ("Rocks", "둥근강돌_02",
     lambda: make_boulder(seed=177, radius_m=0.30, subdivisions=2, roughness=0.06, layers=0,
                           chamfer_prob=0.55, squash=(1.1, 1.0, 0.8)),
     "Stone_B", None, "반지름 약 0.30m, 매끈"),
    ("Rocks", "암벽조각_01",
     lambda: make_boulder(seed=178, radius_m=1.8, subdivisions=3, roughness=0.26, layers=6,
                           facet=0.65, chamfer_prob=0.10),
     "Stone_C", None, "반지름 약 1.8m, 층진 절벽 조각"),
    ("Rocks", "암벽조각_02",
     lambda: make_boulder(seed=179, radius_m=2.6, subdivisions=3, roughness=0.28, layers=7,
                           facet=0.7, chamfer_prob=0.10),
     "Stone_C", None, "반지름 약 2.6m, 층진 절벽 조각"),
    ("Rocks", "자갈무리_01", lambda: make_gravel_patch(seed=180, count=8, spread=1.0),
     "Stone_A", 0.35, "높이 0.35m, 잔돌 8개"),
]


# ──────────────────────────────────────────────────────────── 실행

def main():
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    picked = [entry for entry in CATALOG
              if not prefixes or any(entry[1].startswith(p) for p in prefixes)]
    if not picked:
        raise SystemExit(f"이름이 맞는 게 없다: {prefixes}")

    # 씬을 한 번만 비운다 — 굽기는 재질 하나당 대표 오브젝트 하나만 골라 한 번씩 하므로,
    # 선택된 오브젝트 전부가 같은 씬에 동시에 있어야 한다(gen_nature.py처럼 아이템마다
    # 비우고 다시 짓는 방식이 아니다).
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects, bpy.data.images):
        for item in list(block):
            if item.users == 0:
                block.remove(item)

    built = []
    used_materials = set()
    for folder, name, build_bm, mat_name, height_m, note in picked:
        obj = build_object(build_bm(), mat_name)
        finish(obj, name)
        if height_m:
            fit_height(obj, height_m)
        built.append((folder, name, obj, note))
        used_materials.add(mat_name)

    baked = bake_all(used_materials, TEX_DIR)

    for folder, name, obj, note in built:
        export(obj, os.path.join(OUT_ROOT, folder), name)

    os.makedirs(os.path.join(OUT_ROOT, "Rocks"), exist_ok=True)
    with open(os.path.join(OUT_ROOT, "Rocks", "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_rocks.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 화면 없이 실행\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20 = 1.75m).\n"
            "텍스처는 Assets/Art/Nature/Textures/<재질이름>.png — 재질 4종(Stone_A/B/C/Moss)이\n"
            "20개 바위 전체를 나눠 쓴다(재질당 텍스처 한 장, 대표 오브젝트 하나로만 굽는다).\n"
            "모양을 바꾸려면 스크립트의 숫자를 고치고 다시 돌린다 — 씨앗이 고정이라\n"
            "안 고치면 같은 파일이 나온다. gen_nature.py(나무·풀)와는 별개 스크립트다.\n\n"
            "만들어진 것:\n" + "".join(f"  {n:14} {d}\n" for _, n, _, _, _, d in CATALOG))

    print("=" * 60)
    for folder, name, obj, note in built:
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"만듦  {name:14} {note}  (삼각형 {tris})")
    print(f"총 {len(built)}개 → {OUT_ROOT}")
    print("텍스처:")
    for mat_name, path in baked.items():
        print(f"  {mat_name:12} → {path}")


main()
