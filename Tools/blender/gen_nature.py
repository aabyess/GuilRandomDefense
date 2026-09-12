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
import numpy as np
import os
import random
import sys
from mathutils import Euler, Vector

UNITS_PER_METER = 11.4          # 사람 키 20 ÷ 1.75m
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Nature")
TEXTURE_DIR = os.path.join(OUT_ROOT, "Textures")   # C 스타일 나무가 구운 PNG가 여기로 간다
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
    # 🔴 위치까지 굽는다(2026-09-12 정정). 예전엔 location=False라, 조각을 location으로 올려
    # 짓고 join한 나무는 오브젝트 위치가 기둥 절반 높이에 남았다 — 최저점은 0인데 원점이
    # 13~25 단위 떠 있어서, 맵에 놓으며 위치를 덮어쓰면 그만큼 땅에 묻혔다.
    # 월드 좌표는 그대로 두고 원점만 옮기는 것이라 모양·크기는 안 바뀐다.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

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
    #
    # 🔴 2026-09-12 정정 — path_mode="COPY"였다. 그동안 이 카탈로그 어떤 것도 실제
    # 이미지 텍스처가 없어(전부 material()의 단색 재질) 눈에 띄지 않았는데, C 스타일
    # 나무가 처음으로 진짜 텍스처를 쓰면서 그대로 뒀으면 파일마다 옆에 <이름>.fbm/
    # 폴더가 생겨 PNG를 복사했을 것이다 — PM 약속(재질 이름과 같은 PNG를
    # Assets/Art/Nature/Textures/에 한 벌만, FBX에 안 박는다)과 어긋난다. 기본값(AUTO)으로
    # 두면 이미 그 폴더에 저장해 둔 PNG를 복사 없이 그대로 가리킨다.
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
    )


# ──────────────────────────────────────────────────────────── 한 메시로 짓기 (2026-09-12 추가)
#
# 새로 만드는 것은 조각마다 오브젝트를 만들어 join하지 않고, 처음부터 bmesh 하나에 짓는다 —
# 원점·재질 번호·삼각형 수를 한 곳에서 통제할 수 있어서다. (처음엔 join한 기존 나무의 원점이
# 기둥 절반 높이에 떠 있던 것도 이유였는데, 그건 finish()가 위치까지 굽도록 고쳐 풀렸다.)
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
    그래서 bridge()·cap()이 만드는 면이 전부 바깥을 본다.

    u를 수평 기준(UP × axis)에서 뽑으므로, 방위가 같은 두 축(꺾인 가지의 앞뒤 마디)은
    u가 같다 — 두 고리의 점 번호가 서로 맞물려 면이 꼬이지 않는다."""
    axis = axis.normalized()
    ref = UP if abs(axis.z) < 0.95 else Vector((1.0, 0.0, 0.0))
    u = ref.cross(axis).normalized()
    return u, axis.cross(u)


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


# ──────────────────────────────────────────────────────────── 나무 (C 스타일)
#
# 2026-09-12 — 사장님이 "C안"(가지가 갈라지는 구조 + 잎 카드 + 절차적 셰이더를 이미지로
# 구운 질감)을 확정해, 프리미티브(원뿔·구)로 짓던 침엽수·활엽수·야자수·죽은나무·그루터기를
# 이 방식으로 바꾼다. 원본 스크래치는 blender 세션의 tree_c.py(Tube 클래스로 가지를
# 튜브+UV로 짓고 Cycles로 절차적 셰이더를 PNG로 굽는다) — 이 섹션은 그걸 이 파일의
# 관례(회전 없는 bmesh 직접 조립, finish/fit_height/export 재사용)에 맞춰 옮긴 것이다.
#
# 재질은 종(種)마다 하나씩만 만들어 공유한다(개체 수만큼 안 늘린다 — 침엽수_01/02가 같은
# 껍질을 쓴다). clear_scene()이 매 CATALOG 항목마다 사용자 없는(users==0) 메시·재질·
# 오브젝트를 지우는데, 구운 재질은 참조하는 메시가 아직 없는 사이(다음 나무를 짓기 전)에도
# 안 지워져야 하므로 use_fake_user=True로 표시한다.

class BranchTube:
    """가지가 갈라지는 축을 따라 고리를 놓고 옆면을 잇는다. UV는 u=둘레·v=길이 — 이
    값 그대로 절차적 셰이더를 굽는다(바깥 텍스처 좌표가 필요 없다).

    bend_to를 주면 grow()가 끝나 갈수록 그 방향으로 서서히 휜다(야자잎이 늘어지는 것처럼) —
    안 주면 wobble만큼만 무작위로 흔든다. cap_tip=False면 끝을 뾰족하게 안 닫고 마지막
    고리(last_ring)만 돌려준다 — 그루터기 단면·야자 줄기 꼭대기처럼 평평하게 덮을 자리."""

    def __init__(self, bm, uv, material_index):
        self.bm, self.uv, self.m = bm, uv, material_index

    def grow(self, rng, start, direction, length, radius, sides, segments, taper, wobble, uv_scale,
              bend_to=None, bend_amount=0.0, cap_tip=True):
        pos, d = Vector(start), Vector(direction).normalized()
        rings, path = [], [(pos.copy(), d.copy())]
        step = length / segments
        for s in range(segments + 1):
            t = s / segments
            r = radius * (1.0 - taper * t)
            u, w = frame(d)
            ring = [self.bm.verts.new(pos + (u * math.cos(a) + w * math.sin(a)) * r)
                    for a in (math.tau * i / sides for i in range(sides))]
            rings.append((ring, t * length * uv_scale))
            if s < segments:
                noise = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.3, 1.0))) * wobble
                # bend_to가 있으면 남은 구간에 걸쳐 그 방향으로 서서히 섞는다 — 급하게
                # 꺾이지 않고 포물선처럼 늘어진다(야자잎, 처음엔 위로 뻗다가 끝에서 처진다).
                d = (d + (bend_to - d) * bend_amount + noise).normalized() if bend_to is not None \
                    else (d + noise).normalized()
                pos = pos + d * step
                path.append((pos.copy(), d.copy()))
        for (lower, v0), (upper, v1) in zip(rings, rings[1:]):
            for i in range(sides):
                j = (i + 1) % sides
                f = self.bm.faces.new((lower[i], lower[j], upper[j], upper[i]))
                f.material_index = self.m
                us = (i / sides, (i + 1) / sides, (i + 1) / sides, i / sides)
                vs = (v0, v0, v1, v1)
                for loop, uu, vv in zip(f.loops, us, vs):
                    loop[self.uv].uv = (uu, vv)
        last_ring = rings[-1][0]
        if cap_tip:
            tip = self.bm.verts.new(pos + d * (radius * 0.5))
            for i in range(sides):
                f = self.bm.faces.new((last_ring[i], last_ring[(i + 1) % sides], tip))
                f.material_index = self.m
                for loop, uu in zip(f.loops, (i / sides, (i + 1) / sides, (i + 0.5) / sides)):
                    loop[self.uv].uv = (uu, rings[-1][1] + 0.05)
        return path, last_ring


def path_point(path, t):
    """가지 경로(위치·방향 목록)에서 비율 t의 위치·방향을 보간한다."""
    k = t * (len(path) - 1)
    i = min(int(k), len(path) - 2)
    f = k - i
    return path[i][0].lerp(path[i + 1][0], f), path[i][1].lerp(path[i + 1][1], f).normalized()


def add_leaf_card(bm, uv, center, normal, up_hint, width, height, material_index, rng):
    """잎(또는 바늘잎 다발, 야자 잎사귀) 카드 한 장 — 평면 사각형, UV 0~1. 뒷면 컬링을
    끈 재질과 짝이라 한 장만 쓴다(add_ribbon처럼 앞뒤 두 장을 안 만든다)."""
    n = normal.normalized()
    right = up_hint.cross(n)
    if right.length < 1e-4:
        right = Vector((1.0, 0.0, 0.0)).cross(n)
    right.normalize()
    up = n.cross(right).normalized()
    base = center
    corners = (base - right * width / 2, base + right * width / 2,
               base + right * width / 2 + up * height, base - right * width / 2 + up * height)
    verts = [bm.verts.new(c) for c in corners]
    f = bm.faces.new(verts)
    f.material_index = material_index
    for loop, coord in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop[uv].uv = coord


def build_object_c(bm, materials):
    """bmesh를 오브젝트로 만든다. build_object()와 달리 UV를 다시 덮어쓰지 않는다 —
    BranchTube·add_leaf_card가 지을 때 이미 굽기에 맞는 UV를 넣어 놨다."""
    mesh = bpy.data.meshes.new("임시")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("임시", mesh)
    bpy.context.collection.objects.link(obj)
    for mat in materials:
        mesh.materials.append(mat)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    return obj


# ─────── 절차적 재질 (굽기 전 노드 셰이더)

def procedural_bark_c(name, dark, light, band_scale=14.0, distortion=6.0, vertical_scale=6.0):
    """나무껍질 — 세로 결이 흐르는 절차적 셰이더. band_scale·distortion·vertical_scale로
    종마다 결의 조밀함·방향을 다르게 준다(야자는 조밀한 가로 띠, 침엽·활엽은 세로 결).
    이미 구운 재질(캐시, _c_baked)이면 다시 안 만든다 — 여러 나무가 공유한다."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if mat.get("_c_baked"):
        return mat
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.9
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (vertical_scale, 1.0, 1.0)
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"
    wave.bands_direction = "Y"
    wave.inputs["Scale"].default_value = band_scale
    wave.inputs["Distortion"].default_value = distortion
    wave.inputs["Detail"].default_value = 3.0
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 18.0
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.7
    mix = nt.nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs["Fac"].default_value = 0.7
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*dark, 1)
    ramp.color_ramp.elements[0].position = 0.25
    ramp.color_ramp.elements[1].color = (*light, 1)
    ramp.color_ramp.elements[1].position = 0.8
    nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], wave.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    nt.links.new(wave.outputs["Fac"], mix.inputs["Color1"])
    nt.links.new(noise.outputs["Fac"], mix.inputs["Color2"])
    nt.links.new(mix.outputs["Color"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (*light, 1)
    return mat


def procedural_rings_c(name, dark, light):
    """그루터기 잘린 단면 — 나이테. ShaderNodeTexWave를 RINGS로 바로 쓴다(UV 중심 0.5,0.5
    기준 동심원). 단면 UV(build_stump_c 참고)가 중심을 0.5,0.5에 맞춰 놨다."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if mat.get("_c_baked"):
        return mat
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.85
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Location"].default_value = (-0.5, -0.5, 0.0)   # UV(0.5,0.5)을 원점으로
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "RINGS"
    wave.rings_direction = "Z"
    wave.inputs["Scale"].default_value = 9.0
    wave.inputs["Distortion"].default_value = 1.4
    wave.inputs["Detail"].default_value = 2.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*dark, 1)
    ramp.color_ramp.elements[1].color = (*light, 1)
    nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], wave.inputs["Vector"])
    nt.links.new(wave.outputs["Fac"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (*light, 1)
    return mat


def procedural_leaf_c(name, color_dark, color_light, needle):
    """잎(또는 바늘잎 다발) 카드용 — 잎 모양 마스크(알파)와 잎맥·색 얼룩.
    needle=True면 바늘잎 다발(세로 줄무늬), False면 끝이 뾰족한 넓은 잎(야자 잎사귀도
    이 모양을 좁고 길게 써서 재사용한다). PM 약속대로 재질 이름 끝을 `_잎카드`로 붙여서
    부른다(호출부, CATALOG 아님)."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if mat.get("_c_baked"):
        return mat
    mat.use_nodes = True
    mat.use_backface_culling = False
    mat.surface_render_method = "DITHERED"
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.6
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["UV"], sep.inputs["Vector"])

    def m(op, a, b=None, clamp=False):
        n = nt.nodes.new("ShaderNodeMath")
        n.operation = op
        n.use_clamp = clamp
        if isinstance(a, (int, float)):
            n.inputs[0].default_value = a
        else:
            nt.links.new(a, n.inputs[0])
        if b is not None:
            if isinstance(b, (int, float)):
                n.inputs[1].default_value = b
            else:
                nt.links.new(b, n.inputs[1])
        return n.outputs[0]

    u, v = sep.outputs["X"], sep.outputs["Y"]
    x = m("SUBTRACT", u, 0.5)
    if needle:
        stripes = m("FRACT", m("MULTIPLY", u, 7.0))
        gap = m("LESS_THAN", m("ABSOLUTE", m("SUBTRACT", stripes, 0.5)), 0.32)
        half = m("MULTIPLY", m("SUBTRACT", 1.0, m("POWER", v, 1.5)), 0.5)
        inside = m("LESS_THAN", m("ABSOLUTE", x), half)
        mask = m("MULTIPLY", inside, gap)
    else:
        half = m("MULTIPLY", m("POWER", m("SINE", m("MULTIPLY", v, math.pi)), 0.7), 0.48)
        mask = m("LESS_THAN", m("ABSOLUTE", x), half)
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 9.0
    noise.inputs["Detail"].default_value = 4.0
    nt.links.new(coord.outputs["UV"], noise.inputs["Vector"])
    vein = m("LESS_THAN", m("ABSOLUTE", x), 0.02)
    tone = m("ADD", m("MULTIPLY", noise.outputs["Fac"], 0.7), m("MULTIPLY", vein, 0.35), clamp=True)
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (*color_dark, 1)
    ramp.color_ramp.elements[1].color = (*color_light, 1)
    nt.links.new(tone, ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(mask, bsdf.inputs["Alpha"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (*color_light, 1)
    return mat


# ─────── 굽기 — 절차적 셰이더를 PNG로 굽고 재질을 이미지 재질로 바꾼다

def bake_material_c(obj, mat, alpha, size=512):
    """mat이 이미 구운 재질(_c_baked)이면 아무것도 안 한다 — 여러 나무가 같은 재질
    (같은 종의 껍질 등)을 공유하므로, 맨 처음 그 재질을 쓰는 오브젝트에서만 굽는다.

    🔴 use_clear=False가 핵심이다(tree_c.py에서 겪은 사고) — 굽기는 오브젝트의 모든
    재질 슬롯을 돌며 활성 이미지 노드를 전부 지운다. 켜 두면 이 오브젝트의 다른 재질
    슬롯에 이미 구워 넣은 이미지가 이번 굽기에 0으로 지워져 나무가 새까매진다.

    끝나면 use_fake_user=True를 켠다 — clear_scene()이 다음 나무를 지으려 이 재질을
    참조하는 메시가 없는 순간에도 지우지 않게 한다."""
    if mat.get("_c_baked"):
        return

    scene = bpy.context.scene
    os.makedirs(TEXTURE_DIR, exist_ok=True)

    def do_bake(image, bake_type):
        nt = mat.node_tree
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        nt.nodes.active = tex
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        prev = (scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising)
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 4
        scene.cycles.use_denoising = False
        scene.render.bake.use_pass_direct = False
        scene.render.bake.use_pass_indirect = False
        scene.render.bake.use_pass_color = True
        scene.render.bake.margin = 4
        scene.render.bake.use_clear = False
        bpy.ops.object.bake(type=bake_type)
        scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising = prev
        nt.nodes.remove(tex)

    if not alpha:
        img = bpy.data.images.new(mat.name + "_baked", size, size)
        do_bake(img, "DIFFUSE")
        img.filepath_raw = os.path.join(TEXTURE_DIR, f"{mat.name}.png")
        img.file_format = "PNG"
        img.save()
        img.reload()
        _to_image_material_c(mat, img, alpha=False)
    else:
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        alpha_link = next(l for l in nt.links if l.to_socket == bsdf.inputs["Alpha"])
        mask_out = alpha_link.from_socket
        color_img = bpy.data.images.new(mat.name + "_color", size, size)
        # 색 굽기 — 알파는 잠시 끊는다(투명 부분이 검게 구워지지 않게).
        nt.links.remove(alpha_link)
        do_bake(color_img, "DIFFUSE")
        # 마스크 굽기 — 발광으로 마스크만 내보낸다.
        emit = nt.nodes.new("ShaderNodeEmission")
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        nt.links.new(mask_out, emit.inputs["Color"])
        surf_link = next(l for l in nt.links if l.to_socket == out.inputs["Surface"])
        nt.links.remove(surf_link)
        nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
        mask_img = bpy.data.images.new(mat.name + "_mask", size, size)
        do_bake(mask_img, "EMIT")
        # 합치기: 색 RGB + 마스크 R → RGBA.
        color = np.array(color_img.pixels[:], dtype=np.float32).reshape(-1, 4)
        mask = np.array(mask_img.pixels[:], dtype=np.float32).reshape(-1, 4)
        color[:, 3] = (mask[:, 0] > 0.5).astype(np.float32)
        final = bpy.data.images.new(mat.name + "_baked", size, size, alpha=True)
        final.pixels = color.ravel().tolist()
        final.filepath_raw = os.path.join(TEXTURE_DIR, f"{mat.name}.png")
        final.file_format = "PNG"
        final.alpha_mode = "STRAIGHT"
        final.save()
        bpy.data.images.remove(color_img)
        bpy.data.images.remove(mask_img)
        final.reload()
        _to_image_material_c(mat, final, alpha=True)

    mat["_c_baked"] = True
    mat.use_fake_user = True


def _to_image_material_c(mat, image, alpha):
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.8
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Linear"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat.surface_render_method = "DITHERED"
        mat.use_backface_culling = False
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    nt.nodes.active = tex


# ─────── 종별 조립(기하) — bmesh만 짓는다(재질·굽기는 make_*_c가 한다)

def _conifer_c_bm(seed):
    """침엽수 — 기둥에서 층층이 가지가 갈라지고, 가지마다 바늘잎 카드 다발이 달린다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    tube = BranchTube(bm, uv, 0)
    trunk, _ = tube.grow(rng, Vector((0, 0, 0)), UP, 1.0, 0.05, 8, 10, 0.85, 0.02, 2.0)
    tiers = 7
    spin = rng.uniform(0, math.tau)
    for i in range(tiers):
        t = i / (tiers - 1)
        z = 0.2 + 0.7 * t
        count = 7 - i // 2
        span = 0.36 * (1 - t) + 0.06
        spin += 0.4
        for k in range(count):
            a = spin + math.tau * k / count + rng.uniform(-0.15, 0.15)
            p, _ = path_point(trunk, z)
            heading = Vector((math.cos(a), math.sin(a), 0.0))
            direction = (heading + UP * rng.uniform(-0.15, 0.05)).normalized()
            branch, _ = tube.grow(rng, p, direction, span, 0.012 * (1.2 - t * 0.5), 4, 2, 0.7, 0.08, 6.0)
            for c in range(9):
                lp, ld = path_point(branch, rng.uniform(0.1, 1.0))
                side = ld.cross(UP).normalized()
                normal = (UP + side * rng.uniform(-0.7, 0.7) + ld * rng.uniform(-0.3, 0.3)).normalized()
                add_leaf_card(bm, uv, lp - ld * 0.05, normal, ld, rng.uniform(0.10, 0.14), rng.uniform(0.16, 0.22), 1, rng)
    for c in range(6):
        lp, ld = path_point(trunk, rng.uniform(0.88, 1.0))
        a = rng.uniform(0, math.tau)
        normal = Vector((math.cos(a), math.sin(a), 0.6)).normalized()
        add_leaf_card(bm, uv, lp, normal, UP, 0.07, 0.13, 1, rng)
    return bm


def _broadleaf_c_bm(seed):
    """활엽수 — 기둥에서 굵은 가지 다섯, 그 가지마다 잔가지와 잎 카드가 촘촘히 달린다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    tube = BranchTube(bm, uv, 0)
    trunk, _ = tube.grow(rng, Vector((0, 0, 0)), UP, 0.62, 0.075, 8, 8, 0.55, 0.06, 2.0)
    azimuth = rng.uniform(0, math.tau)
    for n in range(5):
        azimuth += math.tau * 0.382 + rng.uniform(-0.3, 0.3)
        p, d = path_point(trunk, rng.uniform(0.5, 0.95))
        heading = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        rise = math.radians(rng.uniform(30, 60))
        direction = heading * math.cos(rise) + UP * math.sin(rise)
        branch, _ = tube.grow(rng, p, direction, rng.uniform(0.3, 0.42), 0.028, 6, 5, 0.6, 0.12, 4.0)
        for k in range(4):
            q, qd = path_point(branch, rng.uniform(0.4, 0.95))
            side = qd.cross(UP).normalized() * rng.choice((-1, 1))
            sub_dir = (qd * 0.6 + side * 0.6 + UP * rng.uniform(0.1, 0.6)).normalized()
            twig, _ = tube.grow(rng, q, sub_dir, rng.uniform(0.16, 0.24), 0.012, 5, 3, 0.7, 0.15, 6.0)
            for c in range(12):
                lp, ld = path_point(twig, rng.uniform(0.15, 1.0))
                normal = (ld.cross(UP) * rng.uniform(-1, 1) + UP * rng.uniform(0.2, 1.0)
                          + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0))).normalized()
                add_leaf_card(bm, uv, lp, normal, ld, rng.uniform(0.13, 0.18), rng.uniform(0.15, 0.21), 1, rng)
        for c in range(8):
            lp, ld = path_point(branch, rng.uniform(0.6, 1.0))
            normal = (UP * rng.uniform(0.3, 1.0) + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0))).normalized()
            add_leaf_card(bm, uv, lp, normal, ld, rng.uniform(0.14, 0.19), rng.uniform(0.16, 0.22), 1, rng)
    return bm


def _dead_tree_c_bm(seed):
    """죽은 나무 — 잎 카드 없이 기둥·가지·잔가지만 갈라진다. 끝은 튜브 기본 동작대로
    뾰족하게 닫혀 부러진 듯 보인다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    tube = BranchTube(bm, uv, 0)
    trunk, _ = tube.grow(rng, Vector((0, 0, 0)), UP, 0.85, 0.055, 7, 9, 0.55, 0.05, 2.0)
    azimuth = rng.uniform(0, math.tau)
    for n in range(6):
        azimuth += math.tau * 0.382 + rng.uniform(-0.35, 0.35)
        p, d = path_point(trunk, rng.uniform(0.35, 0.92))
        heading = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        rise = math.radians(rng.uniform(25, 55))
        direction = heading * math.cos(rise) + UP * math.sin(rise)
        branch, _ = tube.grow(rng, p, direction, rng.uniform(0.28, 0.42), 0.022, 6, 4, 0.65, 0.10, 3.0)
        if n % 2 == 0:                                          # 잔가지 — 한 가지 건너 하나
            q, qd = path_point(branch, rng.uniform(0.5, 0.9))
            turn = qd.cross(UP).normalized() * rng.choice((-1, 1))
            twig_dir = (qd * 0.5 + turn * 0.6 + UP * rng.uniform(0.2, 0.6)).normalized()
            tube.grow(rng, q, twig_dir, rng.uniform(0.14, 0.22), 0.010, 5, 3, 0.7, 0.15, 4.0)
    return bm


def _stump_c_bm(seed):
    """그루터기 — 짧은 기둥에 잘린 단면(나이테)과 뿌리 넷. 단면 UV는 중심(0.5,0.5) 기준
    반지름 비례라 procedural_rings_c의 동심원과 맞아떨어진다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    tube = BranchTube(bm, uv, 0)
    _, top_ring = tube.grow(rng, Vector((0, 0, 0)), UP, 1.0, 0.62, 9, 4, 0.12, 0.02, 1.0, cap_tip=False)

    mid = bm.verts.new(Vector((0.0, 0.0, 1.0)))
    n = len(top_ring)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((mid, top_ring[i], top_ring[j]))
        f.material_index = 1
        for loop, vtx in zip(f.loops, (mid, top_ring[i], top_ring[j])):
            loop[uv].uv = (0.5 + vtx.co.x * 0.75, 0.5 + vtx.co.y * 0.75)

    # 뿌리 — 기존 make_stump와 같은 삼각 쐐기. 작고 눈에 덜 띄어 UV는 대충 한 점으로 둔다.
    spin = rng.uniform(0, math.tau)
    for k in range(4):
        angle = spin + math.tau * (k + rng.uniform(0.2, 0.8)) / 4
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        left = bm.verts.new(out * 0.62 - side * 0.14)
        right = bm.verts.new(out * 0.62 + side * 0.14)
        high = bm.verts.new(out * 0.50 + UP * rng.uniform(0.14, 0.22))
        tip = bm.verts.new(out * rng.uniform(0.85, 1.0))
        f1 = bm.faces.new((left, tip, high)); f1.material_index = 0
        f2 = bm.faces.new((tip, right, high)); f2.material_index = 0
        for f in (f1, f2):
            for loop in f.loops:
                loop[uv].uv = (0.1, 0.1)
    return bm


def _palm_c_bm(seed, rng):
    """야자수 — 살짝 기운 줄기 꼭대기에서 잎줄기(라키스) 일곱이 뻗어 나가다 처지고,
    그 잎줄기마다 좌우로 잎사귀 카드가 달린다."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    tube = BranchTube(bm, uv, 0)
    lean_angle = rng.uniform(0, math.tau)
    lean_dir = Vector((math.cos(lean_angle), math.sin(lean_angle), 0.0))
    trunk_dir = (UP * 0.94 + lean_dir * 0.34).normalized()
    _, top_ring = tube.grow(rng, Vector((0, 0, 0)), trunk_dir, 0.85, 0.045, 8, 10, 0.30, 0.02, 1.4,
                             cap_tip=False)

    crown = sum((v.co for v in top_ring), Vector()) / len(top_ring)
    crown_center = bm.verts.new(crown)
    n = len(top_ring)
    for i in range(n):
        j = (i + 1) % n
        f = bm.faces.new((crown_center, top_ring[i], top_ring[j]))
        f.material_index = 0
        for loop in f.loops:
            loop[uv].uv = (0.5, 0.95)

    start = rng.uniform(0, math.tau)
    for i in range(7):
        angle = start + math.tau * i / 7 + rng.uniform(-0.2, 0.2)
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        initial = (out * 0.55 + UP * 0.45).normalized()
        droop_dir = (out * 0.75 - UP * 0.65).normalized()
        span = rng.uniform(0.42, 0.55)
        rachis, _ = tube.grow(rng, crown, initial, span, 0.012, 4, 8, 0.6, 0.04, 4.0,
                               bend_to=droop_dir, bend_amount=0.22)
        for c in range(9):
            lp, ld = path_point(rachis, rng.uniform(0.15, 1.0))
            side = ld.cross(UP).normalized()
            for sgn in (-1, 1):
                normal = (UP * 0.3 + side * sgn * 0.9).normalized()
                center = lp + side * sgn * 0.02
                add_leaf_card(bm, uv, center, normal, ld, rng.uniform(0.05, 0.07), rng.uniform(0.22, 0.30), 1, rng)
    return bm


# ─────── 종별 조립(재질+굽기) — CATALOG가 부르는 것은 이 함수들이다

def make_conifer_c(seed):
    bark = procedural_bark_c("C_침엽_껍질", (0.13, 0.09, 0.06), (0.34, 0.24, 0.16))
    needle = procedural_leaf_c("침엽_잎카드", (0.05, 0.20, 0.09), (0.20, 0.42, 0.20), needle=True)
    obj = build_object_c(_conifer_c_bm(seed), [bark, needle])
    bake_material_c(obj, bark, alpha=False)
    bake_material_c(obj, needle, alpha=True)
    return obj


def make_broadleaf_c(seed, autumn=False):
    bark = procedural_bark_c("C_활엽_껍질", (0.14, 0.10, 0.07), (0.36, 0.27, 0.18))
    if autumn:
        leaf = procedural_leaf_c("가을_잎카드", (0.35, 0.14, 0.04), (0.72, 0.42, 0.10), needle=False)
    else:
        leaf = procedural_leaf_c("활엽_잎카드", (0.09, 0.24, 0.09), (0.32, 0.54, 0.22), needle=False)
    obj = build_object_c(_broadleaf_c_bm(seed), [bark, leaf])
    bake_material_c(obj, bark, alpha=False)
    bake_material_c(obj, leaf, alpha=True)
    return obj


def make_dead_tree_c(seed):
    bark = procedural_bark_c("C_고사_껍질", (0.16, 0.14, 0.12), (0.40, 0.36, 0.31),
                              band_scale=10.0, distortion=8.0)
    obj = build_object_c(_dead_tree_c_bm(seed), [bark])
    bake_material_c(obj, bark, alpha=False)
    return obj


def make_stump_c(seed):
    bark = procedural_bark_c("C_그루터기_껍질", (0.14, 0.10, 0.07), (0.36, 0.27, 0.18))
    rings = procedural_rings_c("C_나이테", (0.42, 0.30, 0.16), (0.68, 0.52, 0.32))
    obj = build_object_c(_stump_c_bm(seed), [bark, rings])
    bake_material_c(obj, bark, alpha=False)
    bake_material_c(obj, rings, alpha=False)
    return obj


def make_palm_c(seed):
    rng = random.Random(seed)
    bark = procedural_bark_c("C_야자_껍질", (0.16, 0.11, 0.06), (0.42, 0.33, 0.20),
                              band_scale=26.0, distortion=2.0, vertical_scale=1.0)
    frond = procedural_leaf_c("야자_잎카드", (0.08, 0.24, 0.10), (0.30, 0.52, 0.22), needle=False)
    obj = build_object_c(_palm_c_bm(seed, rng), [bark, frond])
    bake_material_c(obj, bark, alpha=False)
    bake_material_c(obj, frond, alpha=True)
    return obj


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

    # 나무 — 침엽수 둘, 활엽수 둘(하나는 가을색). 2026-09-12 C 스타일로 교체(사장님 확정) —
    # 높이는 기존 FBX 실측값(check_nature_fbx.py)에 fit_height로 정확히 맞춘다(맵 배치가
    # 안 바뀌게). 씨앗은 그대로(201~204) — 모양만 C 스타일로 바뀐다.
    ("Trees", "침엽수_01", lambda: make_conifer_c(seed=201), "높이 7.06m, C 스타일", 7.06),
    ("Trees", "침엽수_02", lambda: make_conifer_c(seed=202), "높이 10.59m, C 스타일", 10.59),
    ("Trees", "활엽수_01", lambda: make_broadleaf_c(seed=203), "높이 5.24m, C 스타일", 5.24),
    ("Trees", "활엽수_가을", lambda: make_broadleaf_c(seed=204, autumn=True), "높이 7.04m, C 스타일 단풍색", 7.04),

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

    # 2026-09-12 추가 ─ 나무(250번대). 같은 날 밤 C 스타일로 다시 교체(사장님 확정) — 씨앗
    # 그대로, 높이도 기존 실측값 그대로(변화 없음, fit_height로 다시 못박아 둔다).
    ("Trees", "그루터기_01", lambda: make_stump_c(seed=251), "높이 0.45m, C 스타일 나이테", 0.45),
    ("Trees", "죽은나무_01", lambda: make_dead_tree_c(seed=252), "높이 5m, C 스타일 잎 없음", 5.0),
    ("Trees", "야자수_01", lambda: make_palm_c(seed=253), "높이 6.5m, C 스타일 잎이 늘어짐, 바닷가용", 6.5),
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
