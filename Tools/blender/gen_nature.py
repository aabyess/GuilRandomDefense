"""저폴리 자연물(돌·나무·풀)을 Blender로 만들어 FBX로 내보낸다.

화면 없이 돈다:
    blender --background --factory-startup --python Tools/blender/gen_nature.py
    blender --background --factory-startup --python Tools/blender/gen_nature.py -- 풀_ 판석_

뒤에 이름 앞부분을 주면 그것만 만든다(안 주면 전부). 검증은 Tools/blender/check_nature_fbx.py로
한다 — 실제 파일을 다시 읽어 크기·삼각형 수·원점을 찍는다.

왜 Blender인가 — 유니티 에디터 스크립트로도 메시를 만들 수 있지만, 정점을 밀고
면을 각지게 만드는 일은 이쪽이 훨씬 짧게 끝난다. 결과물(FBX)만 저장소에 들어가므로
평소 작업에는 Blender가 필요 없다 — 모양을 바꾸고 싶을 때만 이 스크립트를 다시 돌린다.

⚠️ 크기 기준: 우리 맵은 **사람 키 20**이 기준이다(ArtBinder.FitToHeight).
사람 키를 1.75m로 보면 1m ≈ 11.4 게임 단위다. 아래 치수는 전부 **미터**로 적고
내보낼 때 이 배수를 곱한다 — 실제 크기 감각으로 숫자를 읽을 수 있게 하려는 것이다.

⚠️ 결정적(deterministic)이다. 같은 씨앗이면 같은 모양이 나온다.
🔴 다만 **파일 바이트는 매번 달라진다** — FBX 헤더에 내보낸 시각과 내부 ID가 박힌다
(2026-09-12 확인: 기존 9종을 다시 내보내 보니 크기·삼각형은 그대로인데 바이트는 전부 달랐다).
그래서 모양을 안 바꾼 것은 다시 내보내지 말고, 새로 만들거나 고친 것만 이름으로 골라 돌린다 —
안 그러면 깃에 가짜 변경이 쌓인다.

⚠️ 씨앗 대역: 바위 100번대 · 나무 200번대 · 풀 300번대. 이미 쓴 번호는 다시 쓰지 않는다
(아래 CATALOG가 전부다 — 새 것을 넣기 전에 거기서 번호를 확인할 것).
"""

import bpy
import bmesh
import math
import os
import random
import sys
from mathutils import Euler, Vector

UNITS_PER_METER = 11.4          # 사람 키 20 ÷ 1.75m
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Nature")
UP = Vector((0.0, 0.0, 1.0))

# 색은 저폴리 화풍에 맞춰 채도를 낮게 잡았다. 텍스처 없이 색만으로 간다.
COLORS = {
    "바위":   (0.42, 0.44, 0.46, 1.0),
    "나무껍질": (0.30, 0.22, 0.16, 1.0),
    "잎":     (0.24, 0.46, 0.26, 1.0),
    "잎_가을":  (0.62, 0.42, 0.16, 1.0),
    # 2026-09-12 추가 — 전부 기존 "잎"보다 채도가 낮다(맵에 깔리는 양이 많아 튀면 안 된다).
    "풀":     (0.38, 0.52, 0.30, 1.0),
    "덤불":   (0.22, 0.38, 0.24, 1.0),
    "마른풀":  (0.62, 0.55, 0.38, 1.0),   # 억새 줄기·잎
    "억새꽃":  (0.72, 0.66, 0.52, 1.0),   # 더 밝으면 흰 종잇조각처럼 뜬다(첫 렌더)
    "나무속":  (0.64, 0.52, 0.38, 1.0),   # 그루터기 잘린 단면
    "마른껍질": (0.40, 0.36, 0.31, 1.0),   # 죽은 나무 — 나무껍질보다 잿빛
    "야자껍질": (0.46, 0.38, 0.28, 1.0),
    "야자잎":  (0.36, 0.50, 0.28, 1.0),
}


# ──────────────────────────────────────────────────────────── 준비

def clear_scene():
    """기본 씬의 큐브·카메라·조명까지 전부 지운다."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def material(name):
    """이름이 같으면 재사용한다 — FBX 하나에 같은 재질이 여러 벌 생기는 걸 막는다."""
    if name in bpy.data.materials:
        return bpy.data.materials[name]

    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = COLORS[name]
    bsdf.inputs["Roughness"].default_value = 0.85
    mat.diffuse_color = COLORS[name]     # 뷰포트·FBX가 읽는 값
    return mat


def finish(obj, name):
    """바닥에 딱 붙는 원점으로 맞추고 각진 음영으로 바꾼다.

    원점이 발밑에 있어야 맵에 놓을 때 y만 지면 높이로 주면 된다 — 가운데에 있으면
    절반이 땅에 묻힌다."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    lowest = min((obj.matrix_world @ v.co).z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co.z -= lowest

    bpy.ops.object.shade_flat()
    obj.name = name
    obj.data.name = name
    return obj


def export(obj, folder, name):
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # global_scale로 미터 → 게임 단위. 축 기본값(-Z 앞, Y 위)이 유니티 규약과 같다.
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(folder, f"{name}.fbx"),
        use_selection=True,
        global_scale=UNITS_PER_METER,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        mesh_smooth_type="FACE",      # 각진 면을 그대로 살린다
        use_mesh_modifiers=True,
        add_leaf_bones=False,         # 뼈가 없는 모델이라 빈 뼈를 만들지 않는다
        bake_anim=False,
        path_mode="COPY",
    )


# ──────────────────────────────────────────────────────────── 한 메시로 짓기 (2026-09-12 추가)
#
# 🔴 새로 만드는 것은 조각마다 오브젝트를 만들어 join하지 않고, 처음부터 bmesh 하나에 짓는다.
# 기존 나무(make_conifer·make_broadleaf)는 합친 뒤 오브젝트 위치가 기둥 절반 높이에 남는다 —
# 기둥을 location으로 올려 만들었고 finish()는 위치를 굽지 않기 때문이다. 최저점은 0이라
# 겉보기엔 맞지만, check_nature_fbx.py로 다시 읽으면 원점이 발밑에서 13~25 게임 단위 떠 있다.
# 한 메시로 지으면 위치가 처음부터 원점이라 이 문제가 안 생긴다.
#
# 크기는 비율로 짓고 마지막에 fit_height()가 미터로 맞춘다 — "의도한 높이"와 파일 속 높이가
# 어긋날 틈을 없애려는 것이다(판석·그루터기처럼 미터로 지은 것도 같은 길로 한 번 더 맞춘다).

def build_object(bm, material_names):
    """bmesh를 오브젝트 하나로 만든다. material_names 순서가 곧 면의 material_index다."""
    # 텍스처는 없지만 UV 한 벌은 둔다 — 기존 9종(프리미티브가 만든 것)에는 UV가 들어 있어서,
    # 같은 조건으로 임포트되게 맞춘다. 값은 대충 투영이라 텍스처용이 아니다.
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = (co.x + co.z, co.y + co.z)

    mesh = bpy.data.meshes.new("임시")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("임시", mesh)
    bpy.context.collection.objects.link(obj)
    for name in material_names:
        mesh.materials.append(material(name))

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    return obj


def fit_height(obj, height_m):
    """최고점이 정확히 height_m이 되도록 통째로(세 축 같은 배율로) 늘리거나 줄인다.

    finish() 뒤에 부른다 — 그때는 최저점이 0이라 배율을 곱해도 발밑은 0 그대로다."""
    scale = height_m / max(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co *= scale


def frame(axis):
    """axis를 도는 기준 벡터 둘(u, w). u×w = axis라 각도가 늘면 축을 반시계로 돈다 —
    그래서 bridge()·cap()·point()가 만드는 면이 전부 바깥을 본다.

    u를 수평 기준(UP × axis)에서 뽑으므로, 방위가 같은 두 축(꺾인 가지의 앞뒤 마디)은
    u가 같다 — 두 고리의 점 번호가 서로 맞물려 면이 꼬이지 않는다."""
    axis = axis.normalized()
    ref = UP if abs(axis.z) < 0.95 else Vector((1.0, 0.0, 0.0))
    u = ref.cross(axis).normalized()
    return u, axis.cross(u)


def add_ring(bm, center, axis, radii, spin=0.0):
    """center를 지나 axis에 수직인 고리. 점마다 반지름을 따로 받아 테두리를 울퉁불퉁하게 할 수 있다."""
    u, w = frame(axis)
    verts = []
    for i, radius in enumerate(radii):
        angle = spin + math.tau * i / len(radii)
        verts.append(bm.verts.new(center + (u * math.cos(angle) + w * math.sin(angle)) * radius))
    return verts


def bridge(bm, lower, upper, material_index):
    """점 수가 같은 두 고리를 옆면으로 잇는다."""
    count = len(lower)
    for i in range(count):
        j = (i + 1) % count
        bm.faces.new((lower[i], lower[j], upper[j], upper[i])).material_index = material_index


def cap(bm, ring, center, material_index, facing_axis):
    """고리를 가운데 점에서 부채꼴로 막는다.

    n각형 한 장보다 삼각형이 두 개 많지만, 테두리가 울퉁불퉁해 오목해져도 유니티가
    엉뚱하게 쪼개지 않는다. facing_axis=False면 반대쪽(바닥면)을 본다."""
    middle = bm.verts.new(center)
    count = len(ring)
    for i in range(count):
        j = (i + 1) % count
        loop = (middle, ring[i], ring[j]) if facing_axis else (middle, ring[j], ring[i])
        bm.faces.new(loop).material_index = material_index


def point(bm, ring, apex, material_index):
    """고리를 한 점으로 모아 뾰족하게 닫는다(부러진 가지 끝)."""
    tip = bm.verts.new(apex)
    count = len(ring)
    for i in range(count):
        bm.faces.new((ring[i], ring[(i + 1) % count], tip)).material_index = material_index


def add_ribbon(bm, points, widths, side, material_index):
    """가운데 선(points)을 따라 좌우로 widths만큼 벌린 띠 — 풀잎·억새·야자잎.

    폭이 0인 점은 뾰족한 끝이 된다.
    🔴 앞뒤 두 벌로 만든다 — 유니티 기본 재질은 뒷면을 안 그려서, 한 장짜리 잎은
    반대편에서 보면 통째로 사라진다. 대신 삼각형이 두 배다."""
    for back in (False, True):
        rows = []
        for center, width in zip(points, widths):
            if width <= 0:
                rows.append([bm.verts.new(center)])
            else:
                half = side * (width / 2)
                rows.append([bm.verts.new(center - half), bm.verts.new(center + half)])
        for lower, upper in zip(rows, rows[1:]):
            loop = lower + upper[::-1]
            if back:
                loop.reverse()
            bm.faces.new(loop).material_index = material_index


def add_rock_lump(bm, rng, x, y, radius, subdivisions, roughness=0.30):
    """make_rock과 같은 규칙(법선 방향으로 밀고, 가로로 퍼뜨리고, 바닥을 눌러 붙인다)을
    bmesh 안에서 한다 — 여러 개를 한 메시에 모으려고. 눌러 붙인 바닥은 z=0에 온다."""
    verts = bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)["verts"]
    for v in verts:
        v.co += v.co.normalized() * (rng.uniform(-roughness, roughness) * radius)

    sx, sy, sz = rng.uniform(0.9, 1.35), rng.uniform(0.9, 1.35), rng.uniform(0.55, 0.85)
    floor = -radius * sz * 0.45
    spin = rng.uniform(0, math.tau)
    cos, sin = math.cos(spin), math.sin(spin)
    for v in verts:
        px, py, pz = v.co.x * sx, v.co.y * sy, max(v.co.z * sz, floor)
        v.co = Vector((x + px * cos - py * sin, y + px * sin + py * cos, pz - floor))


# ──────────────────────────────────────────────────────────── 돌

def make_rock(seed, radius_m, roughness=0.30):
    """구를 찌그러뜨려 바위를 만든다.

    정점을 법선 방향으로 무작위로 밀고, 바닥 아래로 내려간 정점은 바닥면에 눌러 붙인다 —
    그래야 땅에 놓았을 때 뜨거나 파고들지 않는다."""
    rng = random.Random(seed)

    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius_m)
    obj = bpy.context.active_object

    mesh = bmesh.new()
    mesh.from_mesh(obj.data)

    for v in mesh.verts:
        v.co += v.normal * (rng.uniform(-roughness, roughness) * radius_m)

    # 가로로 퍼지고 낮은 형태가 바위답다. 축마다 다르게 눌러 같은 모양이 반복되지 않게 한다.
    sx, sy, sz = rng.uniform(0.9, 1.35), rng.uniform(0.9, 1.35), rng.uniform(0.55, 0.85)
    for v in mesh.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))

    floor = -radius_m * sz * 0.45
    for v in mesh.verts:
        if v.co.z < floor:
            v.co.z = floor

    mesh.to_mesh(obj.data)
    mesh.free()

    obj.data.materials.append(material("바위"))
    obj.rotation_euler.z = rng.uniform(0, math.tau)
    return obj


def make_slab(seed, width_m, thickness_m):
    """판석 — 윗면이 완전히 평평한 납작한 돌(디딤돌). 테두리만 울퉁불퉁하다.

    옆면은 두 단이다: 아래는 거의 수직, 위 한 단은 안쪽으로 깎아 모서리를 죽인다.
    🔴 윗면 점은 전부 같은 높이다 — 여기에 난수를 섞으면 "평평한 윗면"이 깨진다.
    난수는 테두리 반지름(가로)에만 준다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    sides = 9
    spin = rng.uniform(0, math.tau)
    stretch = rng.uniform(0.70, 0.85)          # 짧은 축 ÷ 긴 축 — 조금 길쭉해야 돌답다
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
    return build_object(bm, ["바위"])


def make_rock_cluster(seed, small_count):
    """바위 무리 — 큰 바위 하나 곁에 작은 돌 몇 개. 전부 한 오브젝트다.

    큰 것만 촘촘하게(분할 2 = 삼각형 80), 작은 것은 거칠게(분할 1 = 20) — 작은 돌은
    모서리가 눈에 덜 띄고, 그래야 오브젝트당 삼각형 150개 안에 들어간다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    add_rock_lump(bm, rng, 0.0, 0.0, 1.0, subdivisions=2)

    # 🔴 작은 돌은 거칠기를 낮춘다 — 분할 1(정점 12개)에 큰 바위와 같은 0.30을 주면 얇은 조각이
    # 갈고리처럼 튄다(첫 렌더에서 바위무리_02 왼쪽이 그랬다). 거리도 큰 바위 가장자리
    # (반지름 × 가로 늘림 1.35 × 거칠기 1.3 ≈ 1.75)까지 밀어야 파묻히지 않고 보인다.
    angle = rng.uniform(0, math.tau)
    for _ in range(small_count):
        angle += rng.uniform(0.9, 1.4)           # 한쪽으로 몰리게 — 빙 두르면 사람이 놓은 것 같다
        reach = rng.uniform(1.45, 1.8)
        add_rock_lump(bm, rng, math.cos(angle) * reach, math.sin(angle) * reach,
                      rng.uniform(0.30, 0.48), subdivisions=1, roughness=0.15)
    return build_object(bm, ["바위"])


# ──────────────────────────────────────────────────────────── 나무

def make_conifer(seed, height_m):
    """침엽수 — 기둥 하나에 원뿔 세 겹."""
    rng = random.Random(seed)
    trunk_h = height_m * 0.34

    bpy.ops.mesh.primitive_cone_add(
        vertices=7, radius1=height_m * 0.045, radius2=height_m * 0.030,
        depth=trunk_h, location=(0, 0, trunk_h / 2))
    trunk = bpy.context.active_object
    trunk.data.materials.append(material("나무껍질"))

    parts = [trunk]
    layers = 3
    for i in range(layers):
        t = i / layers
        bottom = trunk_h * 0.75 + height_m * 0.60 * t
        radius = height_m * (0.30 - 0.09 * t) * rng.uniform(0.92, 1.08)
        depth = height_m * (0.40 - 0.07 * t)

        bpy.ops.mesh.primitive_cone_add(
            vertices=8, radius1=radius, radius2=0.0,
            depth=depth, location=(0, 0, bottom + depth / 2))
        cone = bpy.context.active_object
        cone.data.materials.append(material("잎"))
        parts.append(cone)

    return join(parts, rng)


def make_broadleaf(seed, height_m, autumn=False):
    """활엽수 — 살짝 기운 기둥에 둥근 잎 덩어리 셋."""
    rng = random.Random(seed)
    trunk_h = height_m * 0.55
    leaf = "잎_가을" if autumn else "잎"

    bpy.ops.mesh.primitive_cone_add(
        vertices=7, radius1=height_m * 0.055, radius2=height_m * 0.035,
        depth=trunk_h, location=(0, 0, trunk_h / 2))
    trunk = bpy.context.active_object
    trunk.rotation_euler.x = rng.uniform(-0.05, 0.05)
    trunk.data.materials.append(material("나무껍질"))

    parts = [trunk]
    for i in range(3):
        angle = rng.uniform(0, math.tau)
        spread = height_m * rng.uniform(0.04, 0.13)
        radius = height_m * rng.uniform(0.20, 0.27)
        z = trunk_h * rng.uniform(0.88, 1.12) + radius * 0.35

        bpy.ops.mesh.primitive_ico_sphere_add(
            subdivisions=1, radius=radius,
            location=(math.cos(angle) * spread, math.sin(angle) * spread, z))
        blob = bpy.context.active_object
        blob.scale = (1.0, 1.0, rng.uniform(0.72, 0.92))
        blob.data.materials.append(material(leaf))
        parts.append(blob)

    return join(parts, rng)


def join(parts, rng):
    """여러 조각을 한 오브젝트로 합친다. 재질 슬롯은 그대로 남는다."""
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()

    obj = bpy.context.active_object
    obj.rotation_euler.z = rng.uniform(0, math.tau)
    return obj


def make_stump(seed):
    """그루터기 — 밑동이 벌어진 짧은 기둥에 뿌리 넷이 드러난다.

    🔴 잘린 단면(윗면)은 점이 전부 같은 높이라 완전히 평평하고, 색을 따로 준다(나무속).
    비율로 짓는다: 높이 1, 윗면 반지름 약 0.55."""
    rng = random.Random(seed)
    bm = bmesh.new()
    sides = 8
    spin = rng.uniform(0, math.tau)

    def ring_at(z, radius):
        radii = [radius * rng.uniform(0.92, 1.08) for _ in range(sides)]
        return add_ring(bm, Vector((0.0, 0.0, z)), UP, radii, spin)

    bottom = ring_at(0.0, 0.78)        # 밑동이 벌어진다
    flare = ring_at(0.2, 0.60)
    top = ring_at(1.0, 0.55)
    bridge(bm, bottom, flare, 0)
    bridge(bm, flare, top, 0)
    cap(bm, top, Vector((0.0, 0.0, 1.0)), 1, facing_axis=True)
    cap(bm, bottom, Vector((0.0, 0.0, 0.0)), 0, facing_axis=False)

    # 뿌리 — 기둥에서 땅으로 흘러내리는 삼각 쐐기. 윗면 두 장만 만든다(밑면은 땅,
    # 안쪽 면은 기둥 속이라 안 보인다). 끝을 멀리·높게 빼면 가시처럼 보인다(첫 렌더) —
    # 밑동 테두리(0.78) 바로 바깥까지만, 낮고 넓게.
    for k in range(4):
        angle = spin + math.tau * (k + rng.uniform(0.2, 0.8)) / 4
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        left = bm.verts.new(out * 0.70 - side * 0.16)
        right = bm.verts.new(out * 0.70 + side * 0.16)
        high = bm.verts.new(out * 0.55 + UP * rng.uniform(0.16, 0.24))
        tip = bm.verts.new(out * rng.uniform(0.95, 1.10))
        bm.faces.new((left, tip, high)).material_index = 0
        bm.faces.new((tip, right, high)).material_index = 0

    return build_object(bm, ["나무껍질", "나무속"])


def make_dead_tree(seed):
    """죽은 나무 — 잎 없이 구부정한 기둥과 위로 뻗은 마른 가지. 끝은 부러진 듯 뾰족하다.

    가지는 두 마디다: 중간에서 한 번 위로 꺾여야 마른 가지답다. 비율로 짓는다(기둥 끝 = 1)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    sides = 6
    levels = (0.0, 0.25, 0.55, 0.8)
    radii = (0.060, 0.045, 0.032, 0.020)

    # 기둥 가운데 선을 마디마다 조금씩 틀어 구부정하게 한다.
    centers = []
    drift = Vector((0.0, 0.0, 0.0))
    for z in levels:
        centers.append(Vector((drift.x, drift.y, z)))
        drift += Vector((rng.uniform(-0.035, 0.035), rng.uniform(-0.035, 0.035), 0.0))

    rings = [add_ring(bm, c, UP, [r * rng.uniform(0.9, 1.1) for _ in range(sides)])
             for c, r in zip(centers, radii)]
    for lower, upper in zip(rings, rings[1:]):
        bridge(bm, lower, upper, 0)
    point(bm, rings[-1], Vector((drift.x, drift.y, 1.0)), 0)
    cap(bm, rings[0], centers[0], 0, facing_axis=False)

    def trunk_at(z):
        for k in range(len(levels) - 1):
            if z <= levels[k + 1]:
                t = (z - levels[k]) / (levels[k + 1] - levels[k])
                return centers[k].lerp(centers[k + 1], t), radii[k] + (radii[k + 1] - radii[k]) * t
        return centers[-1], radii[-1]

    # 가지는 길고 굵게, 낮게부터 — 첫 렌더에서 짧고 가는 가지가 꼭대기에만 몰려 "잔가지 달린
    # 장대"로 보였다. 멀리서 실루엣이 읽혀야 죽은 나무다.
    azimuth = rng.uniform(0, math.tau)
    for n, z in enumerate(sorted(rng.uniform(0.30, 0.78) for _ in range(5))):
        azimuth += math.tau * 0.382 + rng.uniform(-0.4, 0.4)   # 황금각 근처 — 가지가 한쪽에 안 몰린다
        heading = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        rise = math.radians(rng.uniform(20, 45))
        bent = rise + math.radians(rng.uniform(8, 20))
        first = heading * math.cos(rise) + UP * math.sin(rise)
        second = heading * math.cos(bent) + UP * math.sin(bent)
        length = rng.uniform(0.32, 0.50) * (1.2 - z * 0.5)     # 위쪽 가지일수록 짧다

        # 가지 밑동 고리는 기둥 속(가운데 선)에서 시작한다 — 이음매가 기둥에 가려진다.
        center, trunk_radius = trunk_at(z)
        thick = trunk_radius * 0.7
        elbow_at = center + first * (length * 0.55)
        base = add_ring(bm, center, first, [thick] * 4)
        elbow = add_ring(bm, elbow_at, second, [thick * 0.5] * 4)
        bridge(bm, base, elbow, 0)
        point(bm, elbow, elbow_at + second * (length * 0.45), 0)

        if n % 2 == 0:                                         # 잔가지 — 한 가지 건너 하나
            turn = azimuth + rng.choice((-0.9, 0.9))
            twig = (Vector((math.cos(turn), math.sin(turn), 0.0)) * math.cos(math.radians(40))
                    + UP * math.sin(math.radians(40)))
            twig_ring = add_ring(bm, elbow_at, twig, [thick * 0.3] * 3)
            point(bm, twig_ring, elbow_at + twig * (length * 0.35), 0)

    return build_object(bm, ["마른껍질"])


def make_palm(seed):
    """야자수 — 한쪽으로 휜 가는 기둥 끝에서 잎이 사방으로 뻗어 아래로 늘어진다. 바닷가용.

    잎 높이는 포물선이다(살짝 올라갔다가 떨어진다) — 끝이 밑동보다 낮게 처져야 야자잎이다.
    비율로 짓는다(기둥 높이 0.85)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    sides = 5
    lean_angle = rng.uniform(0, math.tau)
    lean_dir = Vector((math.cos(lean_angle), math.sin(lean_angle), 0.0))
    lean = rng.uniform(0.08, 0.14)
    steps = (0.0, 0.25, 0.5, 0.75, 1.0)
    radii = (0.030, 0.026, 0.023, 0.021, 0.020)

    # 기울기를 높이의 제곱으로 준다 — 밑동은 곧고 위로 갈수록 휜다.
    centers = [lean_dir * (lean * t * t) + UP * (0.85 * t) for t in steps]
    rings = [add_ring(bm, c, UP, [r] * sides) for c, r in zip(centers, radii)]
    for lower, upper in zip(rings, rings[1:]):
        bridge(bm, lower, upper, 0)
    cap(bm, rings[-1], centers[-1], 0, facing_axis=True)
    cap(bm, rings[0], centers[0], 0, facing_axis=False)

    crown = centers[-1]
    start = rng.uniform(0, math.tau)
    for i in range(6):
        angle = start + math.tau * i / 6 + rng.uniform(-0.25, 0.25)
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        length = rng.uniform(0.42, 0.52)
        droop = rng.uniform(0.75, 0.95)
        points = [crown + out * (length * t) + UP * (length * (0.35 * t - droop * t * t))
                  for t in (0.0, 0.25, 0.55, 0.8, 1.0)]
        add_ribbon(bm, points, (0.02, 0.09, 0.08, 0.05, 0.0), side, 1)

    return build_object(bm, ["야자껍질", "야자잎"])


# ──────────────────────────────────────────────────────────── 풀

def make_grass(seed, blades):
    """풀 포기 — 한 점에서 잎날이 사방으로 뻗는다. 위로 갈수록 좁아지고 바깥으로 휜다.

    휨은 기울기를 높이의 제곱으로 준다 — 밑동은 곧게 서고 끝으로 갈수록 눕는다.
    비율로 짓는다(가장 긴 잎 = 1)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    start = rng.uniform(0, math.tau)
    for i in range(blades):
        angle = start + math.tau * i / blades + rng.uniform(-0.35, 0.35)
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        length = 1.0 if i == 0 else rng.uniform(0.55, 0.92)    # 첫 잎이 가장 길다 → 높이 기준
        lean = length * rng.uniform(0.22, 0.45)
        width = rng.uniform(0.07, 0.10)
        base = out * rng.uniform(0.0, 0.06)
        points = [base + out * (lean * t * t) + UP * (length * t) for t in (0.0, 0.45, 0.8, 1.0)]
        add_ribbon(bm, points, (width, width * 0.7, width * 0.35, 0.0), side, 0)
    return build_object(bm, ["풀"])


def make_bush(seed, lumps):
    """덤불 — 둥근 덩어리 여럿을 겹친다. 땅 아래로 내려간 부분은 눌러 평평하게 한다(바위와 같은 이유).

    비율로 짓는다(가운데 덩어리 반지름 = 1)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    for i in range(lumps):
        if i == 0:
            x = y = 0.0
            radius = 1.0
        else:
            angle = math.tau * i / (lumps - 1) + rng.uniform(-0.4, 0.4)
            reach = rng.uniform(0.55, 0.85)
            x, y = math.cos(angle) * reach, math.sin(angle) * reach
            radius = rng.uniform(0.55, 0.80)
        # 🔴 덩어리마다 아무렇게나 돌린다 — 안 돌리면 20면체 꼭짓점이 전부 위를 향해서, 덤불이
        # 초록 보석(결정) 무더기처럼 보인다(첫 렌더). 흔드는 폭도 키워야 반듯한 각이 무너진다.
        turn = Euler((rng.uniform(0, math.tau), rng.uniform(0, math.tau), rng.uniform(0, math.tau))).to_matrix()
        squash = rng.uniform(0.62, 0.82)
        lift = radius * squash * rng.uniform(0.25, 0.55)
        for v in bmesh.ops.create_icosphere(bm, subdivisions=1, radius=radius)["verts"]:
            co = turn @ (v.co + v.co.normalized() * (rng.uniform(-0.20, 0.20) * radius))
            v.co = Vector((x + co.x, y + co.y, lift + co.z * squash))

    for v in bm.verts:
        v.co.z = max(v.co.z, 0.0)
    return build_object(bm, ["덤불"])


def make_reed(seed, stalks):
    """억새 — 가는 줄기 끝에 이삭이 고개를 숙이고, 밑동에서 긴 잎 셋이 휘어 눕는다. 바닷가용.

    비율로 짓는다(가장 긴 줄기 = 1)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    start = rng.uniform(0, math.tau)
    for i in range(stalks):
        angle = start + math.tau * i / stalks + rng.uniform(-0.4, 0.4)
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        length = 1.0 if i == 0 else rng.uniform(0.72, 0.95)
        base = out * rng.uniform(0.0, 0.05)
        top = base + out * (length * rng.uniform(0.05, 0.12)) + UP * length
        add_ribbon(bm, [base, base.lerp(top, 0.5), top], (0.018, 0.013, 0.0), side, 0)

        # 이삭 — 줄기 끝에서 바깥으로 살짝 올라갔다가 숙인다. 가늘고 길게 — 넓고 짧으면
        # 줄기에 매단 깃발처럼 보인다(첫 렌더).
        plume = rng.uniform(0.26, 0.34)
        points = [top + out * (plume * t) + UP * (plume * (1.1 * t - 1.3 * t * t))
                  for t in (0.0, 0.35, 0.7, 1.0)]
        add_ribbon(bm, points, (0.012, 0.03, 0.022, 0.0), side, 1)

    for i in range(3):
        angle = start + math.tau * (i + 0.5) / 3 + rng.uniform(-0.3, 0.3)
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        length = rng.uniform(0.35, 0.50)
        lean = length * rng.uniform(0.6, 0.8)
        points = [out * (0.02 + lean * t * t) + UP * (length * (1.6 * t - 0.9 * t * t))
                  for t in (0.0, 0.45, 0.8, 1.0)]
        add_ribbon(bm, points, (0.04, 0.03, 0.015, 0.0), side, 0)

    return build_object(bm, ["마른풀", "억새꽃"])


# ──────────────────────────────────────────────────────────── 목록
#
# (폴더, 이름, 만들기, 설명, 맞출 높이 m) — 높이가 None이면 fit_height를 안 부른다(기존 9종은
# 모양 그대로 두려고 None이다). SOURCE.txt는 골라 돌려도 이 목록 전체로 쓴다.

CATALOG = [
    # 돌 — 발치 자갈부터 사람만 한 바위까지.
    *[("Rocks", f"바위_{i:02d}", (lambda s=100 + i, r=radius: make_rock(seed=s, radius_m=r)),
       f"반지름 약 {radius}m", None)
      for i, radius in enumerate([0.35, 0.6, 0.9, 1.4, 2.1], start=1)],

    # 나무 — 침엽수 둘, 활엽수 둘(하나는 가을색).
    ("Trees", "침엽수_01", lambda: make_conifer(seed=201, height_m=7.0), "높이 7m", None),
    ("Trees", "침엽수_02", lambda: make_conifer(seed=202, height_m=10.5), "높이 10.5m", None),
    ("Trees", "활엽수_01", lambda: make_broadleaf(seed=203, height_m=6.0), "높이 6m", None),
    ("Trees", "활엽수_가을", lambda: make_broadleaf(seed=204, height_m=8.0, autumn=True), "높이 8m, 단풍색", None),

    # 2026-09-12 추가 ─ 풀(300번대)
    ("Grass", "풀_01", lambda: make_grass(seed=301, blades=4), "높이 0.4m, 잎날 4장", 0.4),
    ("Grass", "풀_02", lambda: make_grass(seed=302, blades=5), "높이 0.65m, 잎날 5장", 0.65),
    ("Grass", "풀_03", lambda: make_grass(seed=303, blades=7), "높이 0.9m, 잎날 7장", 0.9),
    ("Grass", "덤불_01", lambda: make_bush(seed=311, lumps=5), "높이 0.9m", 0.9),
    ("Grass", "덤불_02", lambda: make_bush(seed=312, lumps=7), "높이 1.4m", 1.4),
    ("Grass", "억새_01", lambda: make_reed(seed=321, stalks=6), "높이 1.8m, 바닷가용", 1.8),

    # 2026-09-12 추가 ─ 돌(150·160번대)
    ("Rocks", "판석_01", lambda: make_slab(seed=151, width_m=0.7, thickness_m=0.12),
     "폭 약 0.7m, 두께 0.12m, 윗면 평평", 0.12),
    ("Rocks", "판석_02", lambda: make_slab(seed=152, width_m=1.1, thickness_m=0.18),
     "폭 약 1.1m, 두께 0.18m, 윗면 평평", 0.18),
    ("Rocks", "바위무리_01", lambda: make_rock_cluster(seed=161, small_count=3),
     "높이 1.0m, 큰 것 1 + 작은 것 3", 1.0),
    ("Rocks", "바위무리_02", lambda: make_rock_cluster(seed=162, small_count=2),
     "높이 1.6m, 큰 것 1 + 작은 것 2", 1.6),

    # 2026-09-12 추가 ─ 나무(250번대)
    ("Trees", "그루터기_01", lambda: make_stump(seed=251), "높이 0.45m, 단면 평평", 0.45),
    ("Trees", "죽은나무_01", lambda: make_dead_tree(seed=252), "높이 5m, 잎 없음", 5.0),
    ("Trees", "야자수_01", lambda: make_palm(seed=253), "높이 6.5m, 잎이 늘어짐, 바닷가용", 6.5),
]


# ──────────────────────────────────────────────────────────── 실행

def main():
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    picked = [entry for entry in CATALOG
              if not prefixes or any(entry[1].startswith(p) for p in prefixes)]
    if not picked:
        raise SystemExit(f"이름이 맞는 게 없다: {prefixes}")

    clear_scene()

    made = []
    for folder, name, build, note, height_m in picked:
        clear_scene()
        obj = finish(build(), name)
        if height_m:
            fit_height(obj, height_m)
        export(obj, os.path.join(OUT_ROOT, folder), name)
        made.append((name, note))

    os.makedirs(OUT_ROOT, exist_ok=True)
    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_nature.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 화면 없이 실행\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20 = 1.75m).\n"
            "모양을 바꾸려면 스크립트의 숫자를 고치고 다시 돌린다 — 씨앗이 고정이라\n"
            "안 고치면 같은 파일이 나온다.\n\n"
            "만들어진 것:\n" + "".join(f"  {n:14} {d}\n" for _, n, _, d, _ in CATALOG))

    print("=" * 60)
    for name, note in made:
        print(f"만듦  {name:14} {note}")
    print(f"총 {len(made)}개 → {OUT_ROOT}")


main()
