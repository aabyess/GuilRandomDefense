"""저폴리 자연물(돌·나무)을 Blender로 만들어 FBX로 내보낸다.

화면 없이 돈다:
    blender --background --factory-startup --python Tools/blender/gen_nature.py

왜 Blender인가 — 유니티 에디터 스크립트로도 메시를 만들 수 있지만, 정점을 밀고
면을 각지게 만드는 일은 이쪽이 훨씬 짧게 끝난다. 결과물(FBX)만 저장소에 들어가므로
평소 작업에는 Blender가 필요 없다 — 모양을 바꾸고 싶을 때만 이 스크립트를 다시 돌린다.

⚠️ 크기 기준: 우리 맵은 **사람 키 20**이 기준이다(ArtBinder.FitToHeight).
사람 키를 1.75m로 보면 1m ≈ 11.4 게임 단위다. 아래 치수는 전부 **미터**로 적고
내보낼 때 이 배수를 곱한다 — 실제 크기 감각으로 숫자를 읽을 수 있게 하려는 것이다.

⚠️ 결정적(deterministic)이다. 같은 씨앗이면 같은 모양이 나오므로, 다시 돌려도
파일이 달라지지 않는다 — 깃에 쓸데없는 변경이 안 쌓인다.
"""

import bpy
import bmesh
import math
import os
import random
from mathutils import Vector

UNITS_PER_METER = 11.4          # 사람 키 20 ÷ 1.75m
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Nature")

# 색은 저폴리 화풍에 맞춰 채도를 낮게 잡았다. 텍스처 없이 색만으로 간다.
COLORS = {
    "바위":   (0.42, 0.44, 0.46, 1.0),
    "나무껍질": (0.30, 0.22, 0.16, 1.0),
    "잎":     (0.24, 0.46, 0.26, 1.0),
    "잎_가을":  (0.62, 0.42, 0.16, 1.0),
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


# ──────────────────────────────────────────────────────────── 실행

def main():
    clear_scene()

    made = []

    # 돌 — 발치 자갈부터 사람만 한 바위까지.
    for i, radius in enumerate([0.35, 0.6, 0.9, 1.4, 2.1], start=1):
        clear_scene()
        rock = finish(make_rock(seed=100 + i, radius_m=radius), f"바위_{i:02d}")
        export(rock, os.path.join(OUT_ROOT, "Rocks"), rock.name)
        made.append((rock.name, f"반지름 약 {radius}m"))

    # 나무 — 침엽수 둘, 활엽수 둘(하나는 가을색).
    trees = [
        ("침엽수_01", lambda: make_conifer(seed=201, height_m=7.0), "높이 7m"),
        ("침엽수_02", lambda: make_conifer(seed=202, height_m=10.5), "높이 10.5m"),
        ("활엽수_01", lambda: make_broadleaf(seed=203, height_m=6.0), "높이 6m"),
        ("활엽수_가을", lambda: make_broadleaf(seed=204, height_m=8.0, autumn=True), "높이 8m, 단풍색"),
    ]
    for name, build, note in trees:
        clear_scene()
        tree = finish(build(), name)
        export(tree, os.path.join(OUT_ROOT, "Trees"), name)
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
            "만들어진 것:\n" + "".join(f"  {n:14} {d}\n" for n, d in made))

    print("=" * 60)
    for name, note in made:
        print(f"만듦  {name:14} {note}")
    print(f"총 {len(made)}개 → {OUT_ROOT}")


main()
