"""인형 받침 세 종 — 유닛 인형이 줄지어 서는 전시 자리 발밑(PM 배정 2026-09-13). blender 세션.

⚠️ 저장소 권한(EPERM) 복구 전 임시 정본(스크래치). 복구 뒤 Tools/blender/, FBX·텍스처는 Assets/Art/Structures/로.

화면 없이(정본):  blender --background --factory-startup --python gen_pedestals.py [-- 받침_불멸 ...]
사장님 창(보여 주기만): ns["build_in_window"](이름, 컬렉션, 창 텍스처 폴더)

공통 규격(PM): 지름(또는 한 변) 6, 높이 0.6~0.9, 윗면 평평(인형이 서는 면 — 반지름 2.2 안은 소품 없음),
원점 바닥 가운데, 정면 −Y, 삼각형 600 이하(받침_조합은 수십 칸이라 200 이하), 뒷면 검사 둘 0건.
실제 칸 크기는 저장소 복구 뒤 맵 생성기에서 읽어 유니티에서 맞춘다 — 여기선 공통 규격만.
- 받침_불멸: 캠프파이어 둘레 원형 전시. 짙은 현무암 원판, **+Y(뒤)가 불 쪽**이라 그쪽 가장자리가 그을렸다 —
  유니티에서 +Y가 캠프파이어를 보게 돌려 세운다. 앞쪽 양옆 둘레에 짧은 쇠 사슬 고리 둘.
- 받침_초월: 초월 25종 두 줄. 흰 대리석 팔각(평평한 변이 ±X·±Y), 옆면 금 띠 한 줄, 윗면 가장자리 몰딩.
- 받침_조합: 조합식 표 칸. 낡은 널 3장 + 쇠 모서리쇠 넷, 가장 단순하게.
좌표는 게임 단위로 짓고 1/11.4로 줄여 m. 셰이더 색은 선형, 굽기 DIFFUSE(Metallic 0 — 금색도 굽는 동안 0).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import gen_portals  # noqa: E402  (굽기 함수 재사용)
from shops_common import Builder, _link, _live_objects, _math, _mix, _node, _noise, _ramp, unwrap  # noqa: E402

UNITS = 11.4
OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Structures")

HALF = 3.0                    # 지름(한 변) 6의 절반
CLEAR_R = 2.2                 # 인형이 서는 자리 — 윗면보다 높은 것 금지
HEIGHT = (0.6, 0.9)
TRI_LIMIT = {"받침_불멸": 600, "받침_초월": 600, "받침_조합": 200}


def _lathe(b, profile, sides, mats, phase=0.0):
    """(반지름, 높이) 단면을 z축으로 돌린 몸통. mats[k] = k번째 띠 재질, mats[-1] = 윗면. 바닥은 막지 않는다(땅에 닿음)."""
    rings = []
    for r, z in profile:
        rings.append([b.bm.verts.new((r * math.cos(phase + 2 * math.pi * i / sides), r * math.sin(phase + 2 * math.pi * i / sides), z))
                      for i in range(sides)])
    for k in range(len(rings) - 1):
        lo, hi = rings[k], rings[k + 1]
        for i in range(sides):
            j = (i + 1) % sides
            b._face((lo[i], lo[j], hi[j], hi[i]), mats[k])
    b._face(rings[-1], mats[-1])
    return profile[-1][1]


def _link_ring(b, center, e1, e2, length, width, wire, mat, seg=6, minor=3):
    """사슬 고리 하나 — e1 방향으로 긴 타원 도넛(e1·e2 평면). 법선은 ∂u×∂v로 바깥."""
    n = e1.cross(e2).normalized()
    a, c = length / 2 - wire, width / 2 - wire
    grid = []
    for i in range(seg):
        u = 2 * math.pi * i / seg
        mid = center + e1 * (a * math.cos(u)) + e2 * (c * math.sin(u))
        rad = (e1 * math.cos(u) + e2 * math.sin(u)).normalized()
        grid.append([b.bm.verts.new(mid + (rad * math.cos(2 * math.pi * j / minor) + n * math.sin(2 * math.pi * j / minor)) * wire)
                     for j in range(minor)])
    for i in range(seg):
        for j in range(minor):
            i2, j2 = (i + 1) % seg, (j + 1) % minor
            b._face((grid[i][j], grid[i2][j], grid[i2][j2], grid[i][j2]), mat)


# ──────────────────────────────────────────────────────────── 모양 셋

def make_immortal():
    b = Builder()
    stone, iron = "받침_불멸_현무암", "받침_불멸_쇠"
    body_r = 2.68
    # 발 → 안으로 들어간 허리 → 윗 테두리. 윗면 0.78 평평(테두리 위 고리못·사슬까지 높이 0.9 안)
    # 🔴 1차(허리 0.32, 고리 0.36)는 사슬이 검은 판 위 낙서였고, 2차(허리에 건 사슬)는 게임 시점에서 윗 테두리에 가려
    # 그냥 검은 원판이었다(PM) — 사슬을 윗면 바깥 가장자리 고리못에 걸어 테두리를 넘어 옆으로 늘어뜨린다
    top = _lathe(b, [(3.0, 0.0), (3.0, 0.14), (body_r, 0.2), (body_r, 0.6), (2.9, 0.66), (2.9, 0.78)], 20, [stone] * 6)
    # 사슬 둘 — 앞쪽 양옆(−Y에서 ±50°). 고리못(윗면 r 2.74, 인형 자리 2.2 밖) → 테두리 모서리를 넘는 누운 고리 →
    # 테두리 옆면에 늘어진 선 고리 둘(벽과 나란해 바깥 끝 반지름 3.0 안) → 모서리 → 고리못
    for center_deg in (-40.0, -140.0):
        c, spread = math.radians(center_deg), 0.145
        path = [(2.74, 0.81, 0.0), (2.94, 0.79, 0.2), (2.95, 0.5, 0.5), (2.94, 0.79, 0.8), (2.74, 0.81, 1.0)]

        def at(r, z, t):
            phi = c - spread + 2 * spread * t
            return Vector((r * math.cos(phi), r * math.sin(phi), z))

        for r, z, t in (path[0], path[-1]):
            b.cylinder(at(r, top - 0.06, t), (0, 0, 1), 0.06, 0.1, iron, sides=6, caps=(False, True))
        for k in range(4):
            p, q = at(*path[k]), at(*path[k + 1])
            e1 = (q - p).normalized()
            mid = (p + q) / 2
            radial = Vector((mid.x, mid.y, 0.0)).normalized()
            tangent = Vector((-radial.y, radial.x, 0.0))
            if k in (0, 3):     # 윗면에 누운 고리
                e2 = (tangent - e1 * e1.dot(tangent)).normalized()
            else:               # 옆면에 나란히 늘어진 고리
                e2 = e1.cross(radial).normalized()
            _link_ring(b, mid, e1, e2, (q - p).length + 0.14, 0.22, 0.045, iron)
    return b, top, "round"


def make_transcendent():
    b = Builder()
    marble, gold = "받침_초월_대리석", "받침_초월_금띠"
    profile = [(3.0, 0.0), (3.0, 0.14), (2.78, 0.2), (2.78, 0.33), (2.83, 0.35), (2.83, 0.47), (2.78, 0.49),
               (2.78, 0.62), (2.95, 0.7), (2.95, 0.78), (2.86, 0.82)]
    mats = [marble, marble, marble, gold, gold, gold, marble, marble, marble, marble, marble]
    top = _lathe(b, profile, 8, mats, phase=math.pi / 8)
    return b, top, "round"


def make_recipe():
    b = Builder()
    wood, iron = "받침_조합_널", "받침_조합_쇠"
    w_half, gap, h = 2.96, 0.04, 0.6
    plank = (2 * w_half - 2 * gap) / 3
    for i in range(3):
        y0 = -w_half + i * (plank + gap)
        b.box(-w_half, w_half, y0, y0 + plank, 0.0, h, wood, skip=("bottom",))
    arm, t = 0.7, 0.03
    for sx in (-1, 1):
        for sy in (-1, 1):
            ex, ey = sx * w_half, sy * w_half
            b.box(ex - sx * arm, ex, ey, ey + sy * t, 0.12, h, iron, skip=("top", "bottom", "+y" if sy < 0 else "-y"))
            b.box(ex, ex + sx * t, ey - sy * arm, ey, 0.12, h, iron, skip=("top", "bottom", "+x" if sx < 0 else "-x"))
            b.box(ex - sx * arm, ex + sx * t, ey - sy * arm, ey + sy * t, h, h + 0.02, iron, skip=("bottom",))
    return b, h, "square"


MAKERS = {"받침_불멸": make_immortal, "받침_초월": make_transcendent, "받침_조합": make_recipe}


# ──────────────────────────────────────────────────────────── 셰이더

def _shade(mat):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}:
            nt.nodes.remove(n)
    co = nt.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    _link(nt, co, sep.inputs[0])
    kind = mat.name.rsplit("_", 1)[1]

    def map_range(value, lo, hi):
        m = nt.nodes.new("ShaderNodeMapRange")
        _link(nt, value, m.inputs["Value"])
        m.inputs["From Min"].default_value, m.inputs["From Max"].default_value = lo / UNITS, hi / UNITS
        return m.outputs["Result"]

    if kind == "현무암":
        base = _mix(nt, _noise(nt, co, 30.0, detail=8.0), (0.026, 0.026, 0.028, 1), (0.058, 0.056, 0.056, 1))
        vor = _node(nt, "ShaderNodeTexVoronoi", Scale=90.0)
        _link(nt, co, vor.inputs["Vector"])
        pit = _ramp(nt, vor.outputs["Distance"], [(0.0, (1, 1, 1, 1)), (0.12, (0, 0, 0, 1))])
        color = _mix(nt, pit, base, (0.01, 0.01, 0.011, 1))
        # 불 쪽(+Y) 가장자리 그을음 — 뒤로 갈수록 × 가장자리일수록, 노이즈로 경계를 흔든다. 갈색 열변색 → 검댕 순서
        flat = nt.nodes.new("ShaderNodeCombineXYZ")
        _link(nt, sep.outputs[0], flat.inputs[0])
        _link(nt, sep.outputs[1], flat.inputs[1])
        radius = nt.nodes.new("ShaderNodeVectorMath")
        radius.operation = "LENGTH"
        _link(nt, flat.outputs[0], radius.inputs[0])
        side = _math(nt, "MULTIPLY", map_range(sep.outputs[1], 0.3, 2.6), map_range(radius.outputs["Value"], 1.5, 2.8))
        jitter = _math(nt, "ADD", side, _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", _noise(nt, co, 20.0, detail=6.0), 0.5), 0.5))
        brown = _ramp(nt, jitter, [(0.2, (0, 0, 0, 1)), (0.45, (1, 1, 1, 1))])
        soot = _ramp(nt, jitter, [(0.5, (0, 0, 0, 1)), (0.8, (1, 1, 1, 1))])
        color = _mix(nt, brown, color, (0.05, 0.03, 0.018, 1))
        color = _mix(nt, soot, color, (0.006, 0.005, 0.0045, 1))
    elif kind == "쇠":
        # 현무암(0.03~0.06) 위에서 사슬이 묻히지 않게 한 톤 밝은 쇠 + 녹
        base = _mix(nt, _noise(nt, co, 30.0), (0.055, 0.055, 0.058, 1), (0.115, 0.11, 0.105, 1))
        rust = _ramp(nt, _noise(nt, co, 60.0, detail=8.0), [(0.0, (0, 0, 0, 1)), (0.55, (0, 0, 0, 1)), (0.68, (1, 1, 1, 1))])
        color = _mix(nt, rust, base, (0.17, 0.07, 0.028, 1))
    elif kind == "대리석":
        # 🔴 1차(Scale 1.6, 짙은 굵은 결 하나)는 금 간 자국처럼 읽혔다 — 가늘고 옅은 결을 여러 가닥, 굵은 결은 더 옅게
        base = _mix(nt, _noise(nt, co, 8.0, detail=4.0), (0.44, 0.43, 0.41, 1), (0.55, 0.54, 0.52, 1))
        color = base
        for scale, distortion, width, vein_color in ((2.2, 10.0, 0.05, (0.36, 0.36, 0.37, 1)), (5.5, 16.0, 0.03, (0.3, 0.3, 0.31, 1))):
            w = _node(nt, "ShaderNodeTexWave", Scale=scale, Distortion=distortion, Detail=6.0)
            w.wave_type, w.bands_direction = "BANDS", "DIAGONAL"
            _link(nt, co, w.inputs["Vector"])
            vein = _ramp(nt, w.outputs["Fac"], [(0.0, (0.6, 0.6, 0.6, 1)), (width, (0, 0, 0, 1))])
            color = _mix(nt, vein, color, vein_color)
    elif kind == "금띠":
        base = _mix(nt, _noise(nt, co, 40.0, detail=4.0), (0.4, 0.26, 0.07, 1), (0.6, 0.41, 0.13, 1))
        worn = _ramp(nt, _noise(nt, co, 90.0, detail=6.0), [(0.0, (0, 0, 0, 1)), (0.62, (0, 0, 0, 1)), (0.72, (1, 1, 1, 1))])
        color = _mix(nt, worn, base, (0.2, 0.13, 0.04, 1))
    elif kind == "널":
        mp = _node(nt, "ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (0.1, 1.0, 1.0)
        _link(nt, co, mp.inputs["Vector"])
        board = _mix(nt, _noise(nt, mp.outputs["Vector"], 45.0, detail=6.0), (0.09, 0.066, 0.042, 1), (0.19, 0.145, 0.095, 1))
        # 널마다 바랜 정도가 다르다 — 널 번호(y 칸)로 톤을 흔든다
        index = _math(nt, "FLOOR", _math(nt, "DIVIDE", _math(nt, "ADD", sep.outputs[1], 2.96 / UNITS), 1.9867 / UNITS), 0.0)
        tone = _math(nt, "FRACT", _math(nt, "MULTIPLY", index, 0.37), 0.0)
        faded = _mix(nt, _noise(nt, co, 5.0), (0.13, 0.12, 0.105, 1), (0.17, 0.155, 0.13, 1))
        color = _mix(nt, _math(nt, "MULTIPLY", tone, 0.9), board, faded)
    else:
        raise ValueError(mat.name)
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    _link(nt, color, bsdf.inputs["Base Color"])


def tex_size(name):
    return 1024 if name.endswith(("_현무암", "_대리석")) else 512


# ──────────────────────────────────────────────────────────── 조립·내보내기

def assemble(name, collection):
    gen_portals.MASKS.clear()
    b, top, shape = MAKERS[name]()
    bm = b.bm
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.normal_update()
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    reach = max(max(abs(x) for x in xs), max(abs(y) for y in ys))
    assert reach <= HALF + 1e-3, f"{name} 한 변 6을 넘었다: {reach:.3f}"
    if shape == "round":
        r = max(math.hypot(v.co.x, v.co.y) for v in bm.verts)
        assert r <= HALF + 1e-3, f"{name} 지름 6을 넘었다: 반지름 {r:.3f}"
    assert abs(min(zs)) < 1e-3 and HEIGHT[0] - 1e-3 <= max(zs) <= HEIGHT[1] + 1e-3, f"{name} 높이 {min(zs):.2f}~{max(zs):.2f}"
    blocked = [v.co for v in bm.verts if math.hypot(v.co.x, v.co.y) < CLEAR_R and v.co.z > top + 1e-4]
    assert not blocked, f"{name} 인형 자리(반지름 {CLEAR_R}) 안에 윗면보다 높은 점 {len(blocked)}개"
    tri_count = sum(len(f.verts) - 2 for f in bm.faces)
    assert tri_count <= TRI_LIMIT[name], f"{name} 삼각형 {tri_count} > {TRI_LIMIT[name]}"
    info = (reach, max(zs), top, tri_count)
    bmesh.ops.scale(bm, vec=Vector((1, 1, 1)) / UNITS, verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for mat_name in b.mats:
        mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        _shade(mat)
        mesh.materials.append(mat)
    return obj, info


def bake(obj, folder):
    gen_portals.tex_size = tex_size
    gen_portals.bake(obj, folder)


def build_in_window(name, collection, folder):
    obj, info = assemble(name, collection)
    unwrap(obj)
    bake(obj, folder)
    return obj


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(MAKERS)
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_구조물")
        bpy.context.scene.collection.children.link(col)
        obj, info = assemble(name, col)
        unwrap(obj)
        bake(obj, os.path.join(OUT, "Textures"))
        for o in _live_objects():
            o.select_set(o is obj)
        bpy.context.view_layer.objects.active = obj
        path = os.path.join(OUT, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, global_scale=UNITS,
                                 path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
        print(f"만듦  {name}  삼각형 {info[3]}  반폭 {info[0]:.2f}  높이 {info[1]:.2f}  윗면 {info[2]:.2f}  "
              f"재질 {[s.material.name for s in obj.material_slots]}  → {path}")


if __name__ == "__main__":
    main()
