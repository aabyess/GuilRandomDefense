"""맵 벽 조각(돌담·돌기둥·나무울타리·정의문)을 Blender로 만들어 FBX로 내보낸다.

화면 없이 돈다(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_walls.py
    blender --background --factory-startup --python Tools/blender/gen_walls.py -- 돌담_ 정의문

🔴 조각은 사장님 Blender 창에서 먼저 짓고(MCP) 모양을 본 뒤 확정한다. 그래도 **정본은 이
스크립트**다 — 창에서는 이 파일을 그대로 읽어 build_live()만 부르므로, 창에 보이는 모양과 여기서
내보내는 모양이 어긋날 수 없다. main()은 __name__ == "__main__"일 때(화면 없이 돌릴 때)만 돈다 —
장면을 통째로 지우는 clear_scene()이 사장님 창에서 돌면 안 되기 때문이다.

⚠️ 치수는 **게임 단위**로 적는다(PM 배정표 그대로, 사람 키 20). 내보낼 때는 1/11.4를 곱해 미터로
바꾸고 gen_nature.py와 같은 배수(11.4)로 내보낸다 — 유니티에는 적은 숫자 그대로 들어간다.
창에서는 미터로 안 바꾼다(옆에 놓인 나무 FBX와 같은 크기로 보이게).

⚠️ 원점은 바닥면 한가운데(길이·두께의 중심, 높이 0). 길이는 X축, 두께는 Y축.
⚠️ 이어 붙이는 조각(돌담 2종·울타리)은 PM 코드가 복제해 붙이고 끝을 맞춰 늘린다. 그래서
양 끝(x = ±길이/2)의 점은 **흔들지 않는다** — 두 끝면의 모양이 똑같아 붙였을 때 틈도, 튀어나온
돌도 없다. 삼각형은 조각당 200개 이하(레인 사이 벽 하나에 50장 넘게 붙는다).
⚠️ 같은 씨앗이면 같은 모양이지만 FBX 바이트는 매번 달라진다(gen_nature.py 주석 참고) —
고친 것만 이름으로 골라 내보낸다. 씨앗 대역: 벽 400번대.
"""

import bpy
import bmesh
import os
import random
import sys
from mathutils import Vector

UNITS_PER_METER = 11.4          # 사람 키 20 ÷ 1.75m — gen_nature.py와 같은 값
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Walls")
LIVE_COLLECTION = "벽_작업"      # 사장님 창에서 짓는 곳 — 이 컬렉션 밖의 물체는 안 건드린다

COLORS = {
    "바위":        (0.42, 0.44, 0.46, 1.0),   # gen_nature.py의 바위와 같은 값 — 섬의 돌과 한 계열
    "갓돌":        (0.52, 0.53, 0.54, 1.0),   # 담 윗단·기둥 받침과 머리 — 몸통보다 밝아야 층이 읽힌다
    "울타리":      (0.40, 0.30, 0.21, 1.0),   # 나무껍질(0.30·0.22·0.16)보다 한 단계 밝게
    "울타리_가로대": (0.33, 0.25, 0.18, 1.0),
    "정의문_금":    (0.85, 0.72, 0.30, 1.0),   # 부서지는 문 — 일부러 채도를 올려 눈에 띄게(PM 지시)
    "정의문_테":    (0.60, 0.48, 0.20, 1.0),
}


# ──────────────────────────────────────────────────────────── 준비

def clear_scene():
    """기본 씬의 큐브·카메라·조명까지 전부 지운다. 🔴 화면 없이 돌 때(main)만 부른다."""
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


def build_object(bm, material_names, name, collection, meters):
    """bmesh를 오브젝트 하나로 만든다. material_names 순서가 곧 면의 material_index다.

    조각은 처음부터 원점(바닥면 한가운데) 기준으로 짓는다 — 위치를 옮기지 않으므로 원점이
    어긋날 틈이 없다. meters=True면 게임 단위를 미터로 바꾼다(내보내기용)."""
    if meters:
        bmesh.ops.scale(bm, vec=Vector((1.0, 1.0, 1.0)) / UNITS_PER_METER, verts=bm.verts)

    # 텍스처는 없지만 UV 한 벌은 둔다 — 자연물 FBX와 같은 조건으로 임포트되게. 값은 대충 투영이다.
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = (co.x + co.z, co.y + co.z)

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for material_name in material_names:
        mesh.materials.append(material(material_name))
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
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
    )


# ──────────────────────────────────────────────────────────── 면 짓기 도구

# 상자의 여섯 면 — 점 번호는 add_box의 corners 순서. 전부 바깥에서 봐서 반시계(= 바깥을 보는 면).
BOX_FACES = {
    "bottom": (0, 3, 2, 1), "top": (4, 5, 6, 7),
    "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6),
    "-x": (3, 0, 4, 7), "+x": (1, 2, 6, 5),
}


def add_box(bm, x0, x1, y0, y1, z0, z1, material_index, skip=()):
    """축에 맞춘 상자. skip에 준 면은 안 만든다 — 다른 조각에 붙어 안 보이는 면에 삼각형을 안 쓰려고."""
    corners = [bm.verts.new(Vector(p)) for p in (
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for face_name, index in BOX_FACES.items():
        if face_name not in skip:
            bm.faces.new([corners[i] for i in index]).material_index = material_index


def bridge(bm, lower, upper, material_index):
    """점 수가 같은 두 고리(아래서 봐 반시계)를 옆면으로 잇는다 — 면이 바깥을 본다."""
    count = len(lower)
    for i in range(count):
        j = (i + 1) % count
        bm.faces.new((lower[i], lower[j], upper[j], upper[i])).material_index = material_index


def cap_top(bm, ring, center, material_index):
    """고리를 가운데 점에서 부채꼴로 막는다(위를 보는 면)."""
    middle = bm.verts.new(center)
    count = len(ring)
    for i in range(count):
        bm.faces.new((middle, ring[i], ring[(i + 1) % count])).material_index = material_index


# ──────────────────────────────────────────────────────────── 돌담

# 담의 단면 — (높이 비율, 반두께 비율). 아래가 가장 넓고 위로 살짝 좁아지다가,
# 갓돌 아래에서 한 번 넓어져 윗단이 올라앉은 모양이 된다. 🔴 반두께 비율은 1.0을 넘지 않는다 —
# 넘으면 조각이 배정표 두께보다 두꺼워진다.
WALL_ROWS = (
    (0.00, 1.00),
    (0.27, 0.95),
    (0.53, 0.91),
    (0.78, 0.87),
    (0.84, 0.97),
    (1.00, 0.93),
)
WALL_CAP_BAND = 3          # 이 띠(4번째 행과 5번째 행 사이)부터 위는 갓돌 색


def make_stone_wall(seed, length, height, thickness, columns):
    """돌담 — 단면을 길이 방향으로 뽑고, 안쪽 점만 흔들어 쌓은 돌처럼 면을 깬다.

    🔴 양 끝 열(i = 0, columns)은 흔들지 않는다. 두 끝면이 똑같은 단면이라, 복제해 이어 붙이면
    틈도 튀어나온 돌도 없다. 가운데 점을 x로도 흔드는 건 그래서 안전하다(끝면에 안 닿는다)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    half = thickness / 2
    step = length / columns
    top = len(WALL_ROWS) - 1
    # 면을 앞뒤로 흔드는 폭. 두께에만 비례시키면 얇은 담(1.4)은 흔들림이 거의 0이라 매끈한
    # 콘크리트 턱처럼 보였다(첫 창 확인) — 높이 기준 바닥값을 둔다. 두꺼운 담은 두께 쪽이 커서 그대로다.
    relief = max(thickness * 0.025, height * 0.02)

    def side(sign):
        grid = []
        for i in range(columns + 1):
            edge = i in (0, columns)
            # 🔴 갓돌 행(WALL_CAP_BAND 이상)은 x·두께 흔들림을 한 값으로 함께 쓴다 — 행마다 따로
            # 흔들면 턱(높이 차가 작은 두 행)이 비틀리거나 뒤집혀 검은 금처럼 보였다(첫 창 확인).
            cap_x = rng.uniform(-0.2, 0.2) * step
            cap_reach = rng.uniform(-0.02, 0.02) * thickness
            column = []
            for j, (z_ratio, half_ratio) in enumerate(WALL_ROWS):
                x = -length / 2 + step * i
                z = height * z_ratio
                reach = half * half_ratio
                if not edge:
                    if j >= WALL_CAP_BAND:
                        x += cap_x
                        reach += cap_reach
                    else:
                        x += rng.uniform(-0.2, 0.2) * step
                        if j in (1, 2):                  # 갓돌 근처 행은 간격이 좁아 높이를 안 흔든다
                            z += rng.uniform(-0.05, 0.05) * height
                        if j == 0:
                            reach -= rng.uniform(0.0, 1.0) * relief     # 바닥 행은 안으로만 — 두께를 안 넘게
                        else:
                            reach += rng.uniform(-1.0, 1.0) * relief
                    reach = min(reach, half)
                column.append(bm.verts.new(Vector((x, sign * reach, z))))
            grid.append(column)
        return grid

    front, back = side(-1.0), side(1.0)
    for i in range(columns):
        for j in range(top):
            band = 1 if j >= WALL_CAP_BAND else 0
            bm.faces.new((front[i][j], front[i + 1][j], front[i + 1][j + 1], front[i][j + 1])).material_index = band
            bm.faces.new((back[i + 1][j], back[i][j], back[i][j + 1], back[i + 1][j + 1])).material_index = band
        bm.faces.new((front[i][top], front[i + 1][top], back[i + 1][top], back[i][top])).material_index = 1

    # 끝면 — 붙이면 가려지지만 벽이 끝나는 자리에선 보인다. 단면이 볼록하지 않아도 되게 부채꼴로.
    for i, facing_left in ((0, True), (columns, False)):
        ring = [front[i][j] for j in range(top + 1)] + [back[i][j] for j in reversed(range(top + 1))]
        middle = bm.verts.new(Vector((front[i][0].co.x, 0.0, height * 0.5)))
        for k in range(len(ring)):
            a, b = ring[k], ring[(k + 1) % len(ring)]
            bm.faces.new((middle, a, b) if facing_left else (middle, b, a)).material_index = 0

    return bm, ["바위", "갓돌"]


# ──────────────────────────────────────────────────────────── 돌기둥

# (높이 비율, 반폭 비율) — 받침 두 줄, 몸통 세 줄, 머리 두 줄.
PILLAR_RINGS = ((0.00, 1.00), (0.10, 1.00), (0.13, 0.82), (0.50, 0.80), (0.87, 0.78), (0.90, 1.00), (1.00, 0.95))
PILLAR_BAND_MATERIALS = (1, 1, 0, 0, 1, 1)     # 띠마다 재질 — 받침·머리는 갓돌 색


def make_pillar(seed, width, height):
    """돌기둥 — 모서리를 깎은 팔각 기둥에 넓은 받침과 머리. 문기둥·벽 끝에 선다(이어 붙이지 않는다)."""
    rng = random.Random(seed)
    bm = bmesh.new()
    rings = []
    for k, (z_ratio, half_ratio) in enumerate(PILLAR_RINGS):
        half = width / 2 * half_ratio
        cut = half * 0.22                      # 네모 그대로면 너무 반듯하다
        corners = ((half, -(half - cut)), (half, half - cut), (half - cut, half), (-(half - cut), half),
                   (-half, half - cut), (-half, -(half - cut)), (-(half - cut), -half), (half - cut, -half))
        ring = []
        for x, y in corners:
            if k == 3:                         # 몸통 가운데 줄만 살짝 흔들어 돌 느낌을 준다
                x += rng.uniform(-0.05, 0.05) * half
                y += rng.uniform(-0.05, 0.05) * half
            ring.append(bm.verts.new(Vector((x, y, height * z_ratio))))
        rings.append(ring)

    for k in range(len(rings) - 1):
        bridge(bm, rings[k], rings[k + 1], PILLAR_BAND_MATERIALS[k])
    cap_top(bm, rings[-1], Vector((0.0, 0.0, height)), 1)
    return bm, ["바위", "갓돌"]


# ──────────────────────────────────────────────────────────── 나무울타리

def add_plank(bm, x0, x1, y0, y1, peak, shoulder, material_index):
    """끝이 뾰족한 널빤지 하나. 밑면은 없다(땅에 박힌다). 삼각형 14개."""
    xm = (x0 + x1) / 2

    def outline(y):
        return [bm.verts.new(Vector(p)) for p in
                ((x0, y, 0.0), (x1, y, 0.0), (x1, y, shoulder), (xm, y, peak), (x0, y, shoulder))]

    a, b = outline(y0), outline(y1)
    for loop in ((a[0], a[1], a[2], a[3], a[4]),      # 앞(-y)
                 (b[1], b[0], b[4], b[3], b[2]),      # 뒤(+y)
                 (a[1], b[1], b[2], a[2]),            # +x 옆
                 (b[0], a[0], a[4], b[4]),            # -x 옆
                 (a[2], b[2], b[3], a[3]),            # 지붕 오른쪽
                 (a[3], b[3], b[4], a[4])):           # 지붕 왼쪽
        bm.faces.new(loop).material_index = material_index


def make_fence(seed, length, height, thickness, planks):
    """나무울타리 — 끝이 뾰족한 널빤지를 나란히 세우고 뒤에 가로대 둘.

    🔴 이어 붙이기: 널빤지 사이 틈을 양 끝에 반씩 둬서, 두 조각을 붙여도 틈 간격이 같다.
    가로대는 양 끝까지 딱 맞게 뻗어 붙이면 한 줄로 이어진다."""
    rng = random.Random(seed)
    bm = bmesh.new()
    width = length / planks
    gap = width * 0.07
    front = -thickness / 2
    plank_back = -thickness / 2 + thickness * 0.6
    back = thickness / 2
    tallest = rng.randrange(planks)            # 하나는 반드시 배정표 높이에 닿는다

    for k in range(planks):
        x0 = -length / 2 + width * k + gap / 2
        x1 = x0 + width - gap
        peak = height if k == tallest else height * rng.uniform(0.90, 0.98)
        add_plank(bm, x0, x1, front, plank_back, peak, peak - (x1 - x0) * 0.45, 0)

    # 가로대는 널빤지 속으로 조금 파고들게 둔다 — 면이 딱 맞닿으면 두 면이 겹쳐 깜빡인다.
    for low, high in ((0.22, 0.32), (0.66, 0.76)):
        add_box(bm, -length / 2, length / 2, plank_back - thickness * 0.05, back,
                height * low, height * high, 1)
    return bm, ["울타리", "울타리_가로대"]


# ──────────────────────────────────────────────────────────── 정의문

def make_gate(length, height, thickness):
    """정의문 — 금빛 두 짝 문. 양옆 기둥·윗보·띠·징·가운데 문양은 어두운 금(테), 문짝은 밝은 금.

    부서지는 게임 요소라 일부러 눈에 띄는 색이다(PM 지시). 이어 붙이지 않는다. 난수 없음."""
    bm = bmesh.new()
    half_length, half_thick = length / 2, thickness / 2
    post = 0.8
    inner = half_length - post
    door_top = height - 0.8
    leaf = thickness * 0.32                    # 문짝 반두께
    strap = thickness * 0.44                   # 가로 띠가 문짝 밖으로 나오는 끝
    stud = thickness * 0.41                    # 세로 징 — 띠보다 조금 덜 나와서 띠가 위를 지나간다

    for sign in (-1, 1):                       # 양옆 기둥
        x0, x1 = sorted((sign * half_length, sign * inner))
        add_box(bm, x0, x1, -half_thick, half_thick, 0.0, height, 1, skip=("bottom",))
    add_box(bm, -inner, inner, -half_thick, half_thick, door_top, height, 1, skip=("-x", "+x"))

    # 문짝 두 짝 — 가운데 가는 틈으로 두 짝이 읽힌다. 기둥에 붙은 바깥 옆면은 안 만든다.
    add_box(bm, -inner, -0.06, -leaf, leaf, 0.0, door_top, 0, skip=("bottom", "top", "-x"))
    add_box(bm, 0.06, inner, -leaf, leaf, 0.0, door_top, 0, skip=("bottom", "top", "+x"))

    for side in (-1, 1):
        against = "+y" if side < 0 else "-y"   # 문짝에 붙은 면
        y0, y1 = sorted((side * leaf, side * strap))
        for low, high in ((door_top * 0.18, door_top * 0.27), (door_top * 0.69, door_top * 0.79)):
            add_box(bm, -inner, inner, y0, y1, low, high, 1, skip=(against, "-x", "+x"))

        y0, y1 = sorted((side * leaf, side * stud))
        for x in (-inner * 2 / 3, -inner / 3, inner / 3, inner * 2 / 3):
            add_box(bm, x - 0.25, x + 0.25, y0, y1, 0.2, door_top - 0.2, 1, skip=(against,))

        # 가운데 문양 — 문짝에서 튀어나온 마름모 뿔. 밑면은 문짝에 붙어 안 만든다.
        mid = door_top / 2
        rim = [bm.verts.new(Vector(p)) for p in (
            (0.0, side * leaf, mid + door_top * 0.18), (0.9, side * leaf, mid),
            (0.0, side * leaf, mid - door_top * 0.18), (-0.9, side * leaf, mid))]
        apex = bm.verts.new(Vector((0.0, side * half_thick, mid)))
        for k in range(4):
            a, b = rim[k], rim[(k + 1) % 4]
            bm.faces.new((a, apex, b) if side < 0 else (a, b, apex)).material_index = 1

    return bm, ["정의문_금", "정의문_테"]


# ──────────────────────────────────────────────────────────── 목록
#
# (이름, 만들기, 설명, 이어 붙이는가, 전시 줄, 라벨) — 치수는 게임 단위(길이 X × 높이 × 두께).
# 전시 줄·라벨은 사장님 창의 판 위 배치(showcase.py)에만 쓰인다 — FBX와 무관하다.
# 라벨이 영어인 건 Blender 기본 폰트가 한글을 못 그려서다.

CATALOG = [
    ("돌담_두꺼움", lambda: make_stone_wall(seed=401, length=8.0, height=5.5, thickness=7.0, columns=6),
     "8 × 5.5 × 7, 레인 사이 벽, 이어 붙임", True, "돌", "StoneWall_Thick"),
    ("돌담_얇음", lambda: make_stone_wall(seed=402, length=6.0, height=5.5, thickness=1.4, columns=5),
     "6 × 5.5 × 1.4, 펑크해저드 벽, 이어 붙임", True, "돌", "StoneWall_Thin"),
    ("돌기둥", lambda: make_pillar(seed=403, width=2.2, height=7.0),
     "2.2 × 7 × 2.2, 문기둥·벽 끝", False, "돌", "StonePillar"),
    ("나무울타리", lambda: make_fence(seed=404, length=6.0, height=5.5, thickness=1.0, planks=6),
     "6 × 5.5 × 1.0, 유닛 우리 벽·칸막이·뽑기 부스, 이어 붙임", True, "나무", "WoodFence"),
    ("정의문", lambda: make_gate(length=20.6, height=7.0, thickness=1.4),
     "20.6 × 7 × 1.4, 펑크해저드 문(부서짐, 금빛)", False, "문", "JusticeGate"),
]


# ──────────────────────────────────────────────────────────── 사장님 창에서 짓기

ROW_ORDER = ("돌", "나무", "문")     # 판 위 줄 순서(앞부터). 여기 없는 종류(자연물 등)는 그 뒤에 붙는다.


def build_live(meters=True, extra_fbx=()):
    """사장님 Blender 창용 — LIVE_COLLECTION을 비우고 조각을 전부 다시 지어, 큰 판 위에 종류별로
    한 줄씩 전시한다(showcase.py, 사장님 지시). 마지막에 뷰를 판에 맞춘다.

    🔴 이 컬렉션 안의 물체만 지운다. 컬렉션 밖(Cube·Light·Camera 등)은 안 건드린다.
    이어 붙이는 조각은 세 개를 딱 붙이고(틈 확인), 그 뒤에 1.5배로 늘린 세 개를 또 붙인다
    (PM 코드가 끝을 맞춰 늘리므로 늘어난 모양도 봐야 한다). 복제는 메시를 공유한다.
    extra_fbx = ((줄, 라벨, FBX 경로), ...) — 이미 커밋된 FBX를 들여와 같은 판에 함께 둔다(예: 자연물).

    ⚠️ 기본은 미터(meters=True) — 사장님 장면이 미터라서다. 내보내는 메시와 같은 크기라, 창에서
    본 것이 곧 FBX 속 모양이다. 전시 배치는 물체 위치만 바꾸므로 FBX 원점·축과 무관하다."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import importlib
    import showcase
    importlib.reload(showcase)                  # 창에서 여러 번 돌리므로 고친 내용이 바로 반영되게

    unit = 1.0 / UNITS_PER_METER if meters else 1.0
    collection = bpy.data.collections.get(LIVE_COLLECTION)
    if collection is None:
        collection = bpy.data.collections.new(LIVE_COLLECTION)
        bpy.context.scene.collection.children.link(collection)
    showcase.clear(collection)

    rows = {}
    for name, maker, _, tiling, kind, label in CATALOG:
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
            behind = depth + 0.1                # 붙인 줄 바로 뒤 — 조금 띄워 두 줄이 구분되게
            for k in range(3):
                stretched = original.copy()
                collection.objects.link(stretched)
                stretched.scale.x = 1.5
                parts.append((stretched, length * 0.25 + length * 1.5 * k, behind))
        rows.setdefault(kind, []).append(showcase.group(label, parts))

    for kind, label, path in extra_fbx:
        obj = showcase.import_fbx(collection, path, unit)
        rows.setdefault(kind, []).append(showcase.group(label, [(obj, 0.0, 0.0)]))

    order = [k for k in ROW_ORDER if k in rows] + [k for k in rows if k not in ROW_ORDER]
    board = showcase.lay_out(collection, [(k, rows[k]) for k in order])
    showcase.frame(board)
    return board


# ──────────────────────────────────────────────────────────── 실행(화면 없이)

def main():
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    picked = [entry for entry in CATALOG
              if not prefixes or any(entry[0].startswith(p) for p in prefixes)]
    if not picked:
        raise SystemExit(f"이름이 맞는 게 없다: {prefixes}")

    made = []
    for name, maker, note, *_ in picked:
        clear_scene()
        bm, material_names = maker()
        obj = build_object(bm, material_names, name, bpy.context.scene.collection, meters=True)
        export(obj, OUT_ROOT, name)
        made.append((name, note))

    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_walls.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 사장님 창에서 짓고 확인한 뒤 화면 없이 내보냄\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20 = 1.75m). 치수는 게임 단위.\n"
            "원점: 바닥면 한가운데. 길이 X축. 이어 붙이는 조각은 양 끝면이 같은 단면이다.\n\n"
            "만들어진 것:\n" + "".join(f"  {n:10} {d}\n" for n, _, d, *_ in CATALOG))

    print("=" * 60)
    for name, note in made:
        print(f"만듦  {name:10} {note}")
    print(f"총 {len(made)}개 → {OUT_ROOT}")


if __name__ == "__main__":
    main()
