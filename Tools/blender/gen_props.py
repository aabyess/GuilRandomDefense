"""보물찾기 소품 4종 — 보물상자(뼈대+Idle/Open 동작)·보물상자_열림·금화더미·보물표시.

화면 없이 돈다(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_props.py [-- 보물상자 금화더미]
사장님 창에서는 이 파일을 exec해 rows(collection)만 부른다(show_all의 extra 훅 — 판_보물).

🔴 치수는 게임 단위(사람 키 20), 짓는 좌표는 미터(÷11.4) — 벽·건물과 같은 방식. 원점 = 바닥 가운데,
정면(자물쇠)은 −Y, 삼각형은 하나당 3,000 이하.
🔴 재질 이름 = 텍스처 이름: Assets/Art/Props/Textures/<이름>.png, 전부 `보물_` 접두. path_mode="RELATIVE".
🔴 굽는 동안 Metallic 0으로 낮췄다 되돌린다(Cycles DIFFUSE 굽기가 금속성만큼 색을 죽이는 버그 —
    건물 텍스처에서 먼저 겪고 고친 것과 동일, buildings_textures.py 참고).
🔴 셰이더 색은 선형값이다 — sRGB 눈대중을 그대로 넣으면 한 톤 허옇게 뜬다(동물 만들 때 겪음). 대략 sRGB^2.2.
🔴 보물상자 뼈대 — Root(바닥) + Lid(뒤쪽 경첩, −y를 보는 본이라 X축 회전이 들어올리는 방향 —
    gen_creatures.py 노루/물범 목 본과 같은 관례). Idle(닫힘, 정지) / Open(0→OPEN_ANGLE_DEG(100°), 30fps 1~46프레임,
    끝으로 갈수록 느려짐) 두 동작을 각각 bpy.data.actions로 만들어 bake_anim_use_all_actions로 내보낸다
    (gen_creatures.py는 동작이 하나뿐이라 이 다중 동작 내보내기는 여기서 처음 — 클립 이름을 FBX
    재확인해야 한다).
"""

import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gen_walls import _tree, _math, _noise, _voronoi, _ramp, _mix, _sep_uv  # noqa: E402
from gen_walls import add_box, bridge, cap, ring_at, cylinder, BOX_FACES  # noqa: E402
from gen_creatures import _fcurves  # noqa: E402 — 새 레이어드 액션(5.x)·구 API 둘 다 받는다

UNITS_PER_METER = 11.4
M = 1.0 / UNITS_PER_METER   # 🔴 치수는 게임 단위로 적었지만 bmesh는 미터로 지어야 한다(내보낼 때 ×11.4) —
                            # 실측(첫 시도 91.2×91.2로 11.4배 나옴)으로 확인, 짓기 끝에 통째로 스케일한다.
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Props")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")
FPS = 30
UP = Vector((0.0, 0.0, 1.0))


# ──────────────────────────────────────────────────────────── 기하 보조 도구(gen_walls에 없는 것)

def shell_half_cylinder(bm, x0, x1, cy, radius, z0, sides, outer_m, inner_m, thickness):
    """두께가 있는 반원통 껍질(뚜껑) — x축으로 누운 반원통. 겉면(outer_m)·속면(inner_m, 안쪽을
    향한다)·양 끝단(두께가 드러나는 테두리, outer_m). 한 겹 면만 지으면 유니티(뒷면 안 그림)에서
    열었을 때 안쪽이 통째로 사라진다(blender 세션 지적) — 겉+속 두 겹과 테두리로 진짜 두께를 준다."""
    def profile(r):
        return [(cy + math.cos(math.pi * k / sides) * r, z0 + math.sin(math.pi * k / sides) * r) for k in range(sides + 1)]
    po, pin = profile(radius), profile(radius - thickness)
    oa = [bm.verts.new((x0, y, z)) for y, z in po]
    ob = [bm.verts.new((x1, y, z)) for y, z in po]
    ia = [bm.verts.new((x0, y, z)) for y, z in pin]
    ib = [bm.verts.new((x1, y, z)) for y, z in pin]
    for k in range(sides):
        bm.faces.new((oa[k], oa[k + 1], ob[k + 1], ob[k])).material_index = outer_m
        bm.faces.new((ib[k], ib[k + 1], ia[k + 1], ia[k])).material_index = inner_m
        bm.faces.new((ia[k], ia[k + 1], oa[k + 1], oa[k])).material_index = outer_m   # x0쪽 테두리
        bm.faces.new((ob[k], ob[k + 1], ib[k + 1], ib[k])).material_index = outer_m   # x1쪽 테두리
    return oa, ob, ia, ib


def cylinder_x(bm, x0, x1, cy, cz, radius, sides, m):
    """가로(x축)로 누운 원기둥 — 경첩 축."""
    def ring(x):
        return [bm.verts.new((x, cy + math.cos(t) * radius, cz + math.sin(t) * radius))
                for t in (math.tau * i / sides for i in range(sides))]
    a, b = ring(x0), ring(x1)
    bridge(bm, a, b, m)
    cap(bm, a, (x0, cy, cz), m, up=False)
    cap(bm, b, (x1, cy, cz), m, up=True)


def add_box_inner(bm, x0, x1, y0, y1, z0, z1, m, skip=()):
    """add_box와 같은 상자지만 안쪽을 향한다(면 순서 반대) — 속 빈 상자의 내벽용."""
    corners = [bm.verts.new(Vector(p)) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                                                  (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for face_name, index in BOX_FACES.items():
        if face_name not in skip:
            bm.faces.new([corners[i] for i in reversed(index)]).material_index = m


def top_rim(bm, x0, x1, y0, y1, ix0, ix1, iy0, iy1, z, m):
    """위를 향한 사각 고리(겉 테두리 x0..x1,y0..y1 → 안쪽 구멍 ix0..ix1,iy0..iy1) — 속 빈 상자
    윗단면(벽 두께가 보이는 자리)."""
    outer = ((x0, y0), (x1, y0), (x1, y1), (x0, y1))
    inner = ((ix0, iy0), (ix1, iy0), (ix1, iy1), (ix0, iy1))
    for i in range(4):
        j = (i + 1) % 4
        a, b2, c, d = outer[i], outer[j], inner[j], inner[i]
        bm.faces.new([bm.verts.new((a[0], a[1], z)), bm.verts.new((b2[0], b2[1], z)),
                      bm.verts.new((c[0], c[1], z)), bm.verts.new((d[0], d[1], z))]).material_index = m


def tilted_coin(bm, center, radius, thickness, normal, sides, m):
    """기운 동전 하나 — normal 방향으로 세운 얇은 원판(위·아래 면 + 옆면)."""
    ref = Vector((1, 0, 0)) if abs(normal.z) < 0.9 else Vector((0, 1, 0))
    u = normal.cross(ref).normalized()
    v = normal.cross(u)
    ring = [Vector((math.cos(t), math.sin(t))) for t in (math.tau * i / sides for i in range(sides))]
    bottom = [bm.verts.new(center + (u * p.x + v * p.y) * radius) for p in ring]
    top = [bm.verts.new(center + normal * thickness + (u * p.x + v * p.y) * radius) for p in ring]
    bridge(bm, bottom, top, m)
    cap(bm, bottom, center, m, up=False)
    cap(bm, top, center + normal * thickness, m, up=True)


def stake(bm, base, direction, length, radius, sides, m):
    """땅에 비스듬히 꽂힌 가는 막대(말뚝) — base에서 direction(정규화 안 해도 됨)으로 length만큼."""
    d = direction.normalized()
    ref = Vector((1, 0, 0)) if abs(d.z) < 0.9 else Vector((0, 1, 0))
    u = d.cross(ref).normalized()
    v = d.cross(u)
    ring = [Vector((math.cos(t), math.sin(t))) for t in (math.tau * i / sides for i in range(sides))]
    bottom = [bm.verts.new(base + (u * p.x + v * p.y) * radius) for p in ring]
    top_c = base + d * length
    top = [bm.verts.new(top_c + (u * p.x + v * p.y) * radius * 0.4) for p in ring]   # 끝이 뾰족하게 가늘어진다
    bridge(bm, bottom, top, m)
    cap(bm, bottom, base, m, up=False)
    cap(bm, top, top_c, m, up=True)


def auto_uv(bm):
    """면마다 평면 투영(가장 넓게 보이는 축으로) + 면 자신의 bbox로 0~1 정규화.

    🔴 처음엔 buildings_common.to_object()처럼 원본 좌표(미터)를 그대로 UV로 썼는데, 여기 소품은
    tile 나눔이 없어 좌표가 작다 보니 대부분의 굽기 캔버스가 안 쓰이고 한쪽 구석에만 색이 몰렸다
    (실측: 보물_금화.png 대부분이 검정). 면마다 제 몫의 0~1을 다 쓰게 정규화한다."""
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")   # 두 번 불러도 안전하게
    bm.normal_update()
    for f in bm.faces:
        if f.normal.length < 1e-6:
            continue
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        coords = []
        for loop in f.loops:
            co = loop.vert.co
            coords.append((co.y, co.z) if ax == 0 else (co.x, co.z) if ax == 1 else (co.x, co.y))
        u0, u1 = min(c[0] for c in coords), max(c[0] for c in coords)
        v0, v1 = min(c[1] for c in coords), max(c[1] for c in coords)
        du, dv = max(u1 - u0, 1e-6), max(v1 - v0, 1e-6)
        for loop, (cu, cv) in zip(f.loops, coords):
            loop[uv].uv = ((cu - u0) / du, (cv - v0) / dv)


# ──────────────────────────────────────────────────────────── 재질(절차적 셰이더, 색은 선형)

MATERIALS = {
    "보물_나무판": 512,
    "보물_금속테": 512,
    "보물_금화": 512,
    "보물_보석": 256,
    "보물_흙": 512,
    "보물_안감": 256,   # 뚜껑 안쪽 어두운 나무 안감
}


def shader_wood(mat):
    nt, bsdf, uv = _tree(mat)
    u, v = _sep_uv(nt, uv)
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.wave_type = "BANDS"
    wave.bands_direction = "Y"
    wave.inputs["Scale"].default_value = 8.0
    wave.inputs["Distortion"].default_value = 2.5
    nt.links.new(uv, wave.inputs["Vector"])
    grain = _noise(nt, uv, 30.0, 5.0, 0.7)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", wave.outputs["Fac"], 0.5), _math(nt, "MULTIPLY", grain, 0.5), clamp=True)
    seam = _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "FRACT", _math(nt, "MULTIPLY", u, 3.0)), 0.5)), 0.46)
    color = _ramp(nt, tone, ((0.0, (0.05, 0.02, 0.01, 1)), (0.5, (0.18, 0.09, 0.04, 1)), (1.0, (0.36, 0.20, 0.09, 1))))
    color = _mix(nt, seam, (0.03, 0.01, 0.005, 1), color)
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75


def shader_metal(mat):
    nt, bsdf, uv = _tree(mat)
    scratch = nt.nodes.new("ShaderNodeTexWave")
    scratch.wave_type = "BANDS"
    scratch.bands_direction = "DIAGONAL"
    scratch.inputs["Scale"].default_value = 25.0
    scratch.inputs["Distortion"].default_value = 5.0
    nt.links.new(uv, scratch.inputs["Vector"])
    grain = _noise(nt, uv, 15.0, 4.0, 0.65)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", grain, 0.7), _math(nt, "MULTIPLY", scratch.outputs["Fac"], 0.3), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.012, 0.011, 0.012, 1)), (0.5, (0.05, 0.045, 0.045, 1)), (1.0, (0.11, 0.10, 0.10, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.4
    bsdf.inputs["Metallic"].default_value = 0.7


def shader_gold(mat):
    """금화 — 정의문 금_장식보다 더 노랗고 밝게(PM 지정 선형값 0.95,0.62,0.10 안팎), 동전 사이
    틈은 어둡게, 테두리는 밝게(voronoi 셀 = 동전 하나)."""
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, 20.0, 4.0, 0.5)
    facet = _voronoi(nt, uv, 5.0, "F1", 1.0)
    d = facet.outputs["Distance"]
    gap = _math(nt, "LESS_THAN", d, 0.06)
    rim = _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", d, 0.06), _math(nt, "LESS_THAN", d, 0.14))
    tone = _math(nt, "ADD", 0.5, _math(nt, "MULTIPLY", grain, 0.3), clamp=True)
    base = _ramp(nt, tone, ((0.0, (0.75, 0.48, 0.05, 1)), (1.0, (0.95, 0.62, 0.10, 1))))
    color = _mix(nt, rim, base, (1.0, 0.85, 0.35, 1))
    color = _mix(nt, gap, color, (0.15, 0.09, 0.01, 1))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.2
    bsdf.inputs["Metallic"].default_value = 0.9


def shader_gem(mat):
    nt, bsdf, uv = _tree(mat)
    facet = _voronoi(nt, uv, 5.0, "F1", 1.0)
    tone = _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", facet.outputs["Distance"], 3.0), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.18, 0.0, 0.01, 1)), (1.0, (0.62, 0.04, 0.07, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.1


def shader_lining(mat):
    """뚜껑 안쪽 어두운 나무 안감 — 무늬는 옅게, 전체적으로 나무판보다 짙다."""
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, 18.0, 4.0, 0.6)
    color = _ramp(nt, grain, ((0.0, (0.03, 0.015, 0.008, 1)), (1.0, (0.09, 0.05, 0.025, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.85


def shader_dirt(mat):
    nt, bsdf, uv = _tree(mat)
    grain = _noise(nt, uv, 12.0, 5.0, 0.7)
    speck = _voronoi(nt, uv, 30.0, "F1", 1.0)
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", grain, 0.6), _math(nt, "MULTIPLY", speck.outputs["Distance"], 1.2), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.05, 0.03, 0.015, 1)), (1.0, (0.17, 0.10, 0.045, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95


SHADERS = {
    "보물_나무판": shader_wood,
    "보물_금속테": shader_metal,
    "보물_금화": shader_gold,
    "보물_보석": shader_gem,
    "보물_흙": shader_dirt,
    "보물_안감": shader_lining,
}


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    return mat


def _bake(obj, mat, image, bake_type):
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
    scene.cycles.samples, scene.cycles.use_denoising = 8, False
    scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 6
    scene.render.bake.use_clear = False
    bpy.ops.object.bake(type=bake_type)
    scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising = prev
    nt.nodes.remove(tex)


def ensure_baked(obj, name, out_dir=None):
    """재질 이름마다 딱 한 번만 굽는다(여러 소품이 같은 재질을 공유 — 나무판·금속테는 상자 둘,
    금화·보석은 열린 상자·금화더미가 같이 쓴다). 굽는 동안 Metallic 0(실측 확인한 버그 회피),
    구운 뒤 원래 값으로 되돌린다.

    🔴 obj에 다른 재질 슬롯이 같이 있으면(상자=나무판+금속테, 금화더미=금화+보석) Cycles가
    "Circular dependency" 경고를 내고 이미지가 새까맣게 깨졌다(실측 확인) — 다른 슬롯의 활성
    이미지 노드와 얽히는 문제라, 이 재질 하나만 남긴 임시 복제본에서 굽고 지운다."""
    mat = material(name)
    if mat.get("_baked"):
        return mat
    out_dir = out_dir or TEX_DIR
    os.makedirs(out_dir, exist_ok=True)
    SHADERS[name](mat)
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    rough, metal = bsdf.inputs["Roughness"].default_value, bsdf.inputs["Metallic"].default_value
    bsdf.inputs["Metallic"].default_value = 0.0
    px = MATERIALS[name]
    img = bpy.data.images.new(name, px, px)

    dup = obj.copy()
    dup.data = obj.data.copy()
    for col in obj.users_collection:
        col.objects.link(dup)
    dup.data.materials.clear()
    dup.data.materials.append(mat)
    for poly in dup.data.polygons:
        poly.material_index = 0
    _bake(dup, mat, img, "DIFFUSE")
    dup_mesh = dup.data
    bpy.data.objects.remove(dup, do_unlink=True)
    bpy.data.meshes.remove(dup_mesh)

    path = os.path.join(out_dir, f"{name}.png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    img.reload()
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
    mat["_baked"] = True
    mat.use_fake_user = True
    return mat


# ──────────────────────────────────────────────────────────── 보물상자

CHEST_W, CHEST_D, CHEST_H = 8.0, 5.5, 6.0
MI = {name: i for i, name in enumerate(MATERIALS)}

# 🔴 열림 각 — 처음엔 손으로 짠 Y-Z 평면 회전 공식으로 "39.46°가 한계"라고 계산해 37°로
# 낮췄는데, 그 손 계산 자체가 틀렸다(뼈가 실제로 도는 축이 그 공식과 안 맞았다 — 앞서 뚜껑
# 방향이 뒤집혔던 것과 같은 종류의 실수). 실제 뼈대를 프레임마다 직접 돌려(evaluated_get로
# 변형된 메시를 읽어) 뚜껑 정점의 최저 높이를 재보니 0°~180° 전 구간에서 한 번도 바닥 밑으로
# 안 내려간다(90°에서도 최저 0.31m, 180°에서도 0.07m — 실측 확인) — 「바닥 뚫림」 자체는
# 걱정할 필요가 없었다. 다만 별도로 진짜 문제 하나를 새로 찾았다: 110°는 수직을 지나쳐 버려
# 뚜껑 앞끝 안쪽 면(오목면) 한 조각이 카메라 반대쪽으로 돌아간다(카메라각 뒷면 검사가 잡음,
# 넓이 4.2 — 면 방향을 통째로 뒤집어 봤더니 다른 13조각이 전부 깨져서 방향 버그가 아니라
# 「수직을 넘겨서 생기는 진짜 뒷면」임을 확인). PM이 원래 요청한 95~100° 범위(수직에 못 미쳐
# 이 문제 자체가 없어진다) 안에서 100°로 정한다.
OPEN_ANGLE_DEG = 100.0
WALL_T = 0.3       # 몸통 벽 두께
LID_T = 0.25       # 뚜껑 두께
STRAP_T = 0.15     # 장식 금속 띠 두께


def build_chest_mesh():
    """몸통(Root)을 먼저 짓고 뚜껑(Lid)을 나중에 지어, 그 경계 정점 번호로 가중치를 나눈다.
    돌려주는 값: (bm, body_end, lid_end, body_h, lid_r, hx, hy) — verts[:body_end]=Root,
    verts[body_end:lid_end]=Lid."""
    hx, hy = CHEST_W / 2, CHEST_D / 2
    body_h = 3.4
    lid_r = CHEST_H - body_h
    wood, metal, lining = MI["보물_나무판"], MI["보물_금속테"], MI["보물_안감"]
    bm = bmesh.new()

    # 몸통(Root) — 속 빈 상자(겉벽 + 안쪽 벽 + 바닥 + 위 테두리) + 가운데 금속 띠 + 네 모서리
    # 보강 + 자물쇠 + 뚜껑 접하는 테두리 + 경첩 축. blender 세션 지적: 겉벽만 지으면 뚜껑을
    # 열었을 때 속이 막힌 나무판만 보인다 — 안쪽 벽·바닥을 따로 짓는다(안쪽 벽은 안을 향한다).
    add_box(bm, -hx, hx, -hy, hy, 0.0, body_h, wood, skip=("bottom", "top"))
    ihx, ihy = hx - WALL_T, hy - WALL_T
    add_box_inner(bm, -ihx, ihx, -ihy, ihy, WALL_T, body_h, wood, skip=("bottom", "top"))
    add_box(bm, -ihx, ihx, -ihy, ihy, WALL_T - 0.05, WALL_T, wood, skip=("bottom",))   # 안쪽 바닥
    top_rim(bm, -hx, hx, -hy, hy, -ihx, ihx, -ihy, ihy, body_h, wood)                  # 위 테두리(벽 두께가 보임)
    mid = body_h * 0.5
    add_box(bm, -hx - 0.05, hx + 0.05, -hy - 0.05, hy + 0.05, mid - 0.25, mid + 0.25, metal, skip=("bottom", "top"))
    post, inset = 0.35, 0.92
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * hx * inset, sy * hy * inset
            add_box(bm, cx - post / 2, cx + post / 2, cy - post / 2, cy + post / 2, 0.0, body_h, metal, skip=("bottom", "top"))
    add_box(bm, -0.55, 0.55, -hy - 0.12, -hy + 0.05, body_h - 1.7, body_h - 0.5, metal)     # 자물쇠(정면 −y)
    cylinder_x(bm, -hx, hx, hy - 0.1, body_h, 0.22, 8, metal)                                # 경첩 축(뒤쪽)
    bm.verts.ensure_lookup_table()
    body_end = len(bm.verts)

    # 뚜껑(Lid) — 두께 있는 반원통 껍질(겉 나무판, 안쪽은 짙은 안감) + 겉면에 꼭 붙는 장식
    # 금속 띠 둘(속이 빈 반원판이 아니라 진짜 두께로 감싸게 — PM 지적).
    shell_half_cylinder(bm, -hx, hx, 0.0, lid_r, body_h, 14, wood, lining, LID_T)
    for sx in (-0.5, 0.5):
        scx = sx * hx
        shell_half_cylinder(bm, scx - 0.18, scx + 0.18, 0.0, lid_r + 0.02, body_h, 14, metal, metal, STRAP_T)
    bm.verts.ensure_lookup_table()
    lid_end = len(bm.verts)

    return bm, body_end, lid_end, body_h, lid_r, hx, hy


def rig_chest(obj, body_end, lid_end, body_h, hy):
    """Generic 뼈대 Root(바닥)+Lid(경첩, −y를 보는 본 — X축 회전이 들어올리는 방향, 노루 목과 같은 관례).
    가중치는 몸통 정점 범위/뚜껑 정점 범위로 100% 직접 나눈다(거리 계산 필요 없음 — 두 부위가
    지어진 순서 그대로 안 섞인다)."""
    arm = bpy.data.objects.new("보물상자_뼈대", bpy.data.armatures.new("보물상자_뼈대"))
    for col in obj.users_collection:
        col.objects.link(arm)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    root = arm.data.edit_bones.new("Root")
    root.head, root.tail = Vector((0, 0, 0)), Vector((0, 0, 0.4))
    lid = arm.data.edit_bones.new("Lid")
    lid.head, lid.tail = Vector((0, hy, body_h)), Vector((0, hy - 0.8, body_h))   # −y를 보는 본
    lid.parent = root
    bpy.ops.object.mode_set(mode="OBJECT")

    g_root = obj.vertex_groups.new(name="Root")
    g_lid = obj.vertex_groups.new(name="Lid")
    g_root.add(list(range(0, body_end)), 1.0, "REPLACE")
    g_lid.add(list(range(body_end, lid_end)), 1.0, "REPLACE")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    return arm


def ease_out(t):
    return 1.0 - (1.0 - t) ** 3


def animate_chest(arm):
    """Idle(닫힘, 정지 2프레임) + Open(0→OPEN_ANGLE_DEG, 1~46프레임, 끝으로 갈수록 느려짐) —
    각각 별도 액션으로 만들어 둘 다 bake_anim_use_all_actions로 내보낸다."""
    scene = bpy.context.scene
    scene.render.fps = FPS
    if arm.animation_data is None:
        arm.animation_data_create()
    lid = arm.pose.bones["Lid"]
    lid.rotation_mode = "XYZ"

    idle = bpy.data.actions.new("Idle")
    arm.animation_data.action = idle
    lid.rotation_euler = (0.0, 0.0, 0.0)
    lid.keyframe_insert("rotation_euler", frame=1)
    lid.keyframe_insert("rotation_euler", frame=2)
    idle.use_fake_user = True

    open_action = bpy.data.actions.new("Open")
    arm.animation_data.action = open_action
    frames = (1, 12, 24, 36, 46)
    for f in frames:
        t = (f - 1) / 45.0
        # 방향은 렌더로 직접 확인했다(양수 X 회전이 뚜껑을 위·뒤로 들어 올린다).
        lid.rotation_euler = (math.radians(OPEN_ANGLE_DEG) * ease_out(t), 0.0, 0.0)
        lid.keyframe_insert("rotation_euler", frame=f)
    for fc in _fcurves(open_action):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"
    open_action.use_fake_user = True
    scene.frame_start, scene.frame_end = 1, 46
    arm.animation_data.action = idle
    return idle, open_action


def build_chest(collection):
    bm, body_end, lid_end, body_h, lid_r, hx, hy = build_chest_mesh()
    bmesh.ops.scale(bm, vec=Vector((M, M, M)), verts=bm.verts)
    body_h, hy = body_h * M, hy * M      # 뼈대도 같은 축척이어야 메시와 어긋나지 않는다
    auto_uv(bm)
    mesh = bpy.data.meshes.new("보물상자")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("보물상자", mesh)
    collection.objects.link(obj)
    # 🔴 정정 — material_index를 전역 MI(나무판=0·금속테=1·…·안감=5)로 매겼는데, 슬롯을
    # 나무판·금속테 둘만 담으면 뚜껑 안감(5)이 범위 밖이라 깨진다(금화더미가 겪은 것과
    # 같은 버그, 여기 뚜껑 안감에도 있었다) — 항상 MATERIALS 전부를 MI 순서로 담는다.
    for name in MATERIALS:
        mesh.materials.append(material(name))
    ensure_baked(obj, "보물_나무판")
    ensure_baked(obj, "보물_금속테")
    ensure_baked(obj, "보물_안감")
    arm = rig_chest(obj, body_end, lid_end, body_h, hy)
    animate_chest(arm)
    return obj, arm


def build_chest_open(collection):
    """열린 상자 — Open 동작의 마지막 프레임(46)을 그대로 굳힌 정지 모델(뼈대는 버린다) +
    금빛 내용물(금화·보석).

    🔴 처음엔 회전을 직접 손으로(Y-Z 평면 회전 공식) 계산했는데, 실제 뼈대가 도는 축/방향과
    안 맞아 거의 안 열린 것처럼 나왔다(실측: 뚜껑 최고 높이가 겨우 몸통 높이 근처, 렌더로도
    닫힌 것처럼 보임). 뼈대 쪽은 렌더로 직접 확인해 맞았으므로, 손 계산 대신 그 자세를
    그대로 굳혀 재사용한다 — 애니메이션 끝과 정지 모델이 항상 일치하는 장점도 있다."""
    tmp_obj, tmp_arm = build_chest(collection)
    tmp_arm.animation_data.action = bpy.data.actions["Open"]
    scene = bpy.context.scene
    scene.frame_set(46)
    bpy.context.view_layer.update()
    deg = bpy.context.evaluated_depsgraph_get()
    eval_obj = tmp_obj.evaluated_get(deg)
    eval_mesh = eval_obj.to_mesh()
    bm = bmesh.new()
    bm.from_mesh(eval_mesh)
    eval_obj.to_mesh_clear()
    bpy.data.objects.remove(tmp_obj, do_unlink=True)
    bpy.data.objects.remove(tmp_arm, do_unlink=True)

    hx, hy = (CHEST_W / 2) * M, (CHEST_D / 2) * M   # 굳힌 메시가 이미 M 스케일(미터)이라 여기도 맞춘다
    gold, gem = MI["보물_금화"], MI["보물_보석"]
    rng_seed = 0
    import random
    rng = random.Random(rng_seed)
    # 🔴 정정 — 몸통 높이(body_h≈3.4)보다 한참 낮게(z 0.3~1.7) 쌓았더니 벽에 가려 밖에서 하나도
    # 안 보였다(렌더로 확인). 테두리 위로 살짝 올라오게 쌓는다.
    for _ in range(10):
        cx = rng.uniform(-hx * 0.7, hx * 0.7)
        cy = rng.uniform(-hy * 0.5, hy * 0.5)
        r = rng.uniform(0.5, 0.9) * M
        z0 = rng.uniform(2.4, 3.3)
        cylinder(bm, cx, cy, z0 * M, (z0 + rng.uniform(0.3, 0.8)) * M, r, 10, gold)
    for _ in range(4):
        cx = rng.uniform(-hx * 0.6, hx * 0.6)
        cy = rng.uniform(-hy * 0.4, hy * 0.4)
        gem_obj = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=rng.uniform(0.3, 0.5) * M,
                                              matrix=Matrix.Translation((cx, cy, rng.uniform(3.0, 3.9) * M)))
        for f in {f for v in gem_obj["verts"] for f in v.link_faces}:
            f.material_index = gem
    auto_uv(bm)   # 새로 더한 동전·보석 면에 UV를 준다(굳혀 온 상자 면은 이미 있던 UV를 그대로 씀)
    mesh = bpy.data.meshes.new("보물상자_열림")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("보물상자_열림", mesh)
    collection.objects.link(obj)
    # 🔴 굳혀 온 상자 메시의 material_index는 전역 MI 순서로 이미 박혀 있다(뚜껑 안감=5 포함) —
    # 슬롯도 항상 MATERIALS 전부를 MI 순서로 담는다(금화더미가 겪은 버그와 같은 종류).
    for name in MATERIALS:
        mesh.materials.append(material(name))
    ensure_baked(obj, "보물_나무판")
    ensure_baked(obj, "보물_안감")
    ensure_baked(obj, "보물_금속테")
    ensure_baked(obj, "보물_금화")
    ensure_baked(obj, "보물_보석")
    return obj


# ──────────────────────────────────────────────────────────── 금화더미·보물표시

def build_gold_pile(collection):
    """동전 여러 개 + 보석 몇 알 — 낮고 둥근 둔덕(지름 7, 가운데 2.5)을 동전이 덮은 모양.
    기울기 제각각인 동전 + 둔덕 밖으로 몇 닢 굴러 나간 것 + 보석 3~4알.

    🔴 정정(blender 세션 렌더 검수) — material_index에 전역 MI(나무판=0·금속테=1·금화=2·
    보석=3···)를 그대로 썼는데, 이 메시엔 재질을 금화·보석 둘만 담아 슬롯이 [0,1]뿐이었다.
    그래서 동전(2)·보석(3) 둘 다 범위 밖 인덱스라 깨져(클램프되어) 보석 재질(빨강)로
    뭉뚱그려 구워졌다 — 빨간 원판 더미로 보인 진짜 원인. 이 메시 안의 실제 슬롯 순서
    (0=금화, 1=보석)를 쓴다."""
    import random
    rng = random.Random(1)
    gold, gem = 0, 1   # 이 메시의 로컬 슬롯 순서(아래 for name in ("보물_금화","보물_보석"))
    bm = bmesh.new()

    mound_r, peak = 3.3, 2.5

    def mound_z(rad):
        return peak * math.cos(min(rad, mound_r) / mound_r * math.pi / 2) if rad < mound_r else 0.0

    for _ in range(34):
        ang = rng.uniform(0, math.tau)
        # 🔴 정정 — rng.uniform(0.0, mound_r)**0.7 * mound_r는 mound_r를 두 번(위 상한과
        # 바깥 곱)으로 써서 반지름이 최대 7.8까지 갔다(둔덕 지름 6.6이 아니라 15.6로 나온 원인).
        rad = rng.uniform(0.0, 1.0) ** 0.7 * mound_r
        cx, cy = math.cos(ang) * rad, math.sin(ang) * rad
        z = mound_z(rad)
        r = rng.uniform(0.45, 0.8)
        tilt_ang = rng.uniform(0, math.tau)
        tilt_amt = rng.uniform(0.1, 0.6)
        normal = Vector((math.sin(tilt_amt) * math.cos(tilt_ang), math.sin(tilt_amt) * math.sin(tilt_ang), math.cos(tilt_amt)))
        tilted_coin(bm, Vector((cx, cy, z)), r, rng.uniform(0.15, 0.25), normal, 10, gold)
    # 둔덕 밖으로 굴러 나간 동전 몇 닢(낮게, 바닥에 거의 눕는다)
    for _ in range(6):
        ang = rng.uniform(0, math.tau)
        rad = rng.uniform(mound_r + 0.2, mound_r + 1.3)
        cx, cy = math.cos(ang) * rad, math.sin(ang) * rad
        normal = Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 1.0)).normalized()
        # z=0.08(딱 0이 아니라 살짝 띄운다) — 기운 동전은 테두리 한쪽이 중심보다 낮아, 중심을
        # 0에 두면 그 낮은 쪽이 바닥 밑으로 내려간다(실측: 최저점 -0.37).
        tilted_coin(bm, Vector((cx, cy, 0.08)), rng.uniform(0.4, 0.7), 0.15, normal, 10, gold)
    for _ in range(4):
        ang = rng.uniform(0, math.tau)
        rad = rng.uniform(0.0, mound_r * 0.8)
        cx, cy = math.cos(ang) * rad, math.sin(ang) * rad
        z = mound_z(rad) + rng.uniform(0.1, 0.4)
        geo = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=rng.uniform(0.35, 0.55),
                                          matrix=Matrix.Translation((cx, cy, z)))
        for f in {f for v in geo["verts"] for f in v.link_faces}:
            f.material_index = gem
    # 🔴 정정 — 0.08만큼 띄워도 큰 반지름(최대 0.7)에 기운 각(최대 0.6rad)이 겹치면 링이
    # 그보다 더 깊이(z=-0.31까지) 내려갔다. 손으로 여유값을 더 얹는 대신 마지막에 한 번
    # 바닥(z=0) 밑 정점을 전부 끌어올려 확실히 막는다.
    for v in bm.verts:
        if v.co.z < 0.0:
            v.co.z = 0.0
    bmesh.ops.scale(bm, vec=Vector((M, M, M)), verts=bm.verts)
    auto_uv(bm)
    mesh = bpy.data.meshes.new("금화더미")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("금화더미", mesh)
    collection.objects.link(obj)
    for name in ("보물_금화", "보물_보석"):
        mesh.materials.append(material(name))
    ensure_baked(obj, "보물_금화")
    ensure_baked(obj, "보물_보석")
    return obj


def build_treasure_marker(collection):
    """땅의 X 표시(엇갈려 꽂힌 나무 말뚝 둘) + 살짝 파인 흙더미 — 탐색 전 표식. 지름 8·높이 1.2.

    🔴 정정(blender 세션 렌더 검수) — 처음엔 얕은 흙 능선으로만 X를 표시해 게임 시점에서
    갈색 원판으로만 보이고 X가 안 보였다. 재질부터 다른(짙은 나무) 말뚝 둘을 엇갈려 꽂아
    실루엣 자체로 드러나게 한다. 둔덕도 0.7→1.2로 키웠다."""
    import random
    rng = random.Random(2)
    dirt, wood = 0, 1   # 이 메시의 로컬 슬롯 순서(아래 for name in ("보물_흙","보물_나무판"))
    bm = bmesh.new()
    sides = 20
    radius = 4.0
    peak = 1.2
    rim = ring_at(bm, (0, 0, peak * 0.7), radius, sides)
    for v in rim:
        v.co.z += rng.uniform(-0.08, 0.08)
    dip = ring_at(bm, (0, 0, peak * 0.25), radius * 0.45, sides)
    center = bm.verts.new((0, 0, 0.0))
    cap(bm, rim, (0, 0, peak), dirt)          # 위로 볼록한 가장자리(둔덕)
    bridge(bm, rim, dip, dirt)                # 가장자리 → 가운데 파인 곳으로 완만하게 내려간다
    cap(bm, dip, center.co, dirt, up=False)
    base = ring_at(bm, (0, 0, 0.0), radius, sides)
    bridge(bm, base, rim, dirt)
    cap(bm, base, (0, 0, 0.0), dirt, up=False)
    # 흩어진 흙덩이 몇 개(둘레)
    for _ in range(6):
        ang = rng.uniform(0, math.tau)
        rad = rng.uniform(radius * 0.6, radius * 0.95)
        cx, cy = math.cos(ang) * rad, math.sin(ang) * rad
        geo = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=rng.uniform(0.2, 0.35),
                                          matrix=Matrix.Translation((cx, cy, rng.uniform(0.1, 0.3))))
        for f in {f for v in geo["verts"] for f in v.link_faces}:
            f.material_index = dirt
    # X 표시 — 엇갈려 꽂힌 말뚝 둘(가운데서 위·양옆으로 벌어진다, 전체 폭 ~5)
    # 🔴 정정 — 처음엔 direction z성분(1.6)이 너무 커서 끝점이 z≈3.0까지 치솟았다
    # (규격 높이 1.2를 3배 가까이 넘음). 기울기를 눕혀 끝 높이를 1.0~1.1로 낮추고,
    # 대신 수평으로 더 벌려 가로 폭 ~5(요청한 「크기 ~5」)는 그대로 유지한다.
    for sign in (-1, 1):
        base_pt = Vector((sign * 0.5, -0.3, 0.15))
        direction = Vector((sign * 2.0, 0.4, 0.9))
        stake(bm, base_pt, direction, 2.2, 0.16, 7, wood)
    # 🔴 바닥 아래로 내려가는 정점 방지(원점=바닥면이 규격) — 흙덩이·말뚝 밑동이 살짝 파고들 수
    # 있어 마지막에 한 번 더 죔쇠처럼 걸어준다.
    for v in bm.verts:
        if v.co.z < 0.0:
            v.co.z = 0.0
    bmesh.ops.scale(bm, vec=Vector((M, M, M)), verts=bm.verts)
    auto_uv(bm)
    mesh = bpy.data.meshes.new("보물표시")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("보물표시", mesh)
    collection.objects.link(obj)
    for name in ("보물_흙", "보물_나무판"):
        mesh.materials.append(material(name))
    ensure_baked(obj, "보물_흙")
    ensure_baked(obj, "보물_나무판")
    return obj


# ──────────────────────────────────────────────────────────── 검사·내보내기

FOOTPRINT_SPEC = {
    "보물상자": (8.0, 5.5, 6.0), "보물상자_열림": (8.0, 5.5, 6.0),
    "금화더미": (7.0, 7.0, 2.5), "보물표시": (8.0, 8.0, 1.2),
}
TRI_LIMIT = 3000


def measure(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    k = UNITS_PER_METER
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return (max(xs) - min(xs)) * k, (max(ys) - min(ys)) * k, (max(zs) - min(zs)) * k, tris, min(zs) * k


def check(obj, name):
    w, d, h, tris, lowest = measure(obj)
    sw, sd, sh = FOOTPRINT_SPEC[name]
    problems = []
    if w > sw + 1.5 or d > sd + 1.5:
        problems.append(f"바닥 {w:.1f}×{d:.1f} (규격 {sw:.1f}×{sd:.1f})")
    if h > sh + 1.0:
        problems.append(f"높이 {h:.1f} (규격 {sh:.1f})")
    if abs(lowest) > 0.1:
        problems.append(f"바닥이 z={lowest:.2f} (원점은 바닥면)")
    if tris > TRI_LIMIT:
        problems.append(f"삼각형 {tris} > {TRI_LIMIT}")
    return problems, (w, d, h, tris)


def export(objects, name, animated):
    os.makedirs(OUT_ROOT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[-1]
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_ROOT, f"{name}.fbx"), use_selection=True, global_scale=UNITS_PER_METER,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE",
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=animated, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="RELATIVE")


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


CATALOG = [
    ("보물상자", "Chest", build_chest, True),
    ("보물상자_열림", "Chest_Open", build_chest_open, False),
    ("금화더미", "Gold_Pile", build_gold_pile, False),
    ("보물표시", "Dig_Spot", build_treasure_marker, False),
]


def rows(collection):
    """show_all의 extra 훅 — 판_보물 한 줄. 상자는 뼈대째(Idle 재생)."""
    import showcase
    out = []
    for name, label, maker, animated in CATALOG:
        result = maker(collection)
        obj, arm = result if animated else (result, None)
        problems, (w, d, h, tris) = check(obj, name)
        print(f"{name:8} {w:5.1f}×{d:5.1f}×{h:5.1f}  삼각형 {tris:5d}  " + ("통과" if not problems else "⚠️ " + " / ".join(problems)))
        parts = [(arm if arm else obj, 0.0, 0.0)]
        out.append(showcase.group(label, parts))
    return out


def write_source():
    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_props.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 화면 없이 실행\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20 = 1.75m). 치수는 게임 단위.\n"
            "원점: 바닥면 한가운데. 정면(자물쇠)은 −Y. 삼각형 하나당 3,000 이하.\n"
            "재질(텍스처 Textures/<이름>.png): 보물_나무판·보물_금속테·보물_금화·보물_보석·보물_흙 — 전부 불투명.\n"
            "보물상자만 뼈대(Root+Lid) + 동작 둘(Idle 정지, Open 0→100° 1~46프레임/30fps) — FBX 클립\n"
            "이름이 「보물상자_뼈대|보물상자_뼈대|Idle」처럼 겹쳐 나온다(Blender 5.2 exporter 자체 동작,\n"
            "gen_creatures.py의 기존 동작도 같은 증상 — 스크립트 문제 아님).\n\n"
            "만들어진 것:\n"
            "  보물상자      나무판+금속테·모서리 보강·자물쇠, 반원통 뚜껑 — 뼈대+Idle/Open\n"
            "  보물상자_열림   Open 마지막 자세를 그대로 굳힌 정지 모델 + 금화·보석 내용물\n"
            "  금화더미      동전+보석 쏟아진 더미\n"
            "  보물표시      X 표시 + 살짝 파인 흙더미\n")


def main():
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    picked = [e for e in CATALOG if not prefixes or any(e[0].startswith(p) for p in prefixes)]
    if not picked:
        raise SystemExit(f"이름이 맞는 게 없다: {prefixes}")
    made = []
    for name, label, maker, animated in picked:
        clear_scene()
        result = maker(bpy.context.scene.collection)
        obj, arm = result if animated else (result, None)
        problems, (w, d, h, tris) = check(obj, name)
        export([obj, arm] if arm else [obj], name, animated)
        made.append((name, w, d, h, tris, problems))
    write_source()
    print("=" * 60)
    for name, w, d, h, tris, problems in made:
        print(f"만듦  {name:8} {w:5.1f}×{d:5.1f}×{h:5.1f}  삼각형 {tris:5d}  " + ("통과" if not problems else "⚠️ " + " / ".join(problems)))
    print(f"총 {len(made)}개 → {OUT_ROOT}")


if __name__ == "__main__":
    main()
