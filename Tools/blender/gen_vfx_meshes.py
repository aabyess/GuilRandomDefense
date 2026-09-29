"""스킬 이펙트 메시 5종(PM 2026-09-29, 원작 조사 Docs/research/ONE_RANDOM_SKILL_VFX.md §4). blender 세션.

원작 워크3 이펙트의 문법은 「검은 바탕 가산합성 텍스처를 얇은 판(고리·초승달·세로판)에 얹는 것」이다. 여기서 그 판을 만든다.
유니티 연결(SkillVfx 메시 파티클)은 구현담당1 몫. 🔴 원작 텍스처는 반입 금지 — Kenney(CC0) 것을 쓰고, Kenney에 없는 모양은
여기서 numpy로 새로 그린다(흰 그림, 회색값 = 알파, Kenney와 같은 꼴).

화면 없이(정본):  blender --background --factory-startup --python Tools/blender/gen_vfx_meshes.py [-- 이름 ...]
사장님 창(보여 주기만):  import gen_vfx_meshes as g; g.build_in_window()

규격 — 🔴 「메시 치수 1 = 대표 치수 1」. 유니티 파티클 startSize에 원하는 대표 치수(게임 단위)를 그대로 넣으면 된다.
레인 적 키 22.5(ArtBinder)가 기준이다.
| 파일                | 대표 치수          | 원점              | 텍스처(UV)                                            | 양면 |
| VFX_충격파_원판     | UV 사각 한 변 = 1  | 바닥 가운데        | Kenney circle_02 / circle_03 (위에서 본 평면 투영)      | 윗면만 |
| VFX_충격파_벽       | 지름 1            | 바닥 가운데        | vfx_wall_fade (U 둘레 4바퀴 반복, V 바닥→위)            | 양면 |
| VFX_초승달_검기     | 바깥 지름 1        | 휘두르는 중심      | vfx_slash_strip (U 호 방향, V 안→바깥 날)               | 양면 |
| VFX_번개_기둥       | 높이 1            | 바닥 가운데        | Kenney trace_06 (세로판 3장 60° 교차, 빛 기둥)          | 양면 |
| VFX_땅_폭발         | 바깥 지름 1        | 바닥 가운데        | vfx_spike (날 12장, V 뿌리→끝)                          | 양면 |
- 양면 = 뒤집은 쌍둥이 면을 겹쳐 둔다. 유니티 Skill_*.mat은 _Cull 2(뒷면 버림)라 한 겹 세로판은 반쪽이 사라진다. 뒷면 버림 덕에
  한 방향에서는 늘 한 겹만 보여서 가산합성이 두 배로 밝아지지 않는다.
- 🔴 좌표축: FBX를 bake_space_transform=True로 내보내 **메시 데이터 자체가 유니티 Y-up**이다(메시 파티클은 오브젝트 회전을 안 쓰고
  메시를 그대로 그린다). 초승달의 호는 Blender −Y 쪽 = 유니티 +Z(앞)으로 열린다.
- 삼각형은 파일당 600 이하(파티클로 수십 개 겹쳐 뿌리므로).
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Effects", "Meshes")
TEX_OUT = os.path.join(OUT, "Textures")
KENNEY = os.path.join(ROOT, "Assets", "Art", "Effects", "Kenney")
TRI_LIMIT = 600

# ------------------------------------------------------------------ 텍스처(새로 그림)


def _save_gray(name, a):
    """a: 0~1 회색값(h, w). Kenney처럼 RGB = 알파 = 값으로 저장한다(가산·알파 어느 재질에서도 같은 모양)."""
    a = np.clip(a, 0.0, 1.0)
    v = (a * 255).astype(np.uint8)
    img = np.dstack([v, v, v, v])
    os.makedirs(TEX_OUT, exist_ok=True)
    path = os.path.join(TEX_OUT, name + ".png")
    h, w = a.shape
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.pixels = (img[::-1].astype(np.float32) / 255.0).ravel()  # Blender 픽셀은 아래 줄부터
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)
    return path


def _noise1d(n, seed, octaves=4):
    """이어 붙는(주기) 1차원 잡음 0~1."""
    rng = np.random.default_rng(seed)
    x = np.arange(n) / n
    out = np.zeros(n)
    amp = 1.0
    for o in range(octaves):
        k = (3, 5, 9, 17, 33)[o]  # 🔴 2의 거듭제곱(4·8·16…)만 쓰면 결이 1/4 주기로 똑같이 되풀이됐다
        for _ in range(3):
            out += amp * rng.uniform(0.3, 1.0) * np.sin(2 * math.pi * (k * x + rng.uniform()))
        amp *= 0.5
    out -= out.min()
    return out / out.max()


def tex_wall_fade(w=256, h=256):
    """충격파 벽: 바닥이 가장 밝고 위로 사라진다. 세로 결(흙먼지가 솟는 줄)이 가로로 이어 붙는다."""
    v = np.linspace(0, 1, h)[:, None]           # 0 = 바닥(이미지 아래)
    streak = _noise1d(w, 7)[None, :]
    top = 0.55 + 0.45 * streak                   # 줄마다 솟는 높이가 다르다
    fade = np.clip(1 - v / top, 0, 1) ** 1.6
    base = np.clip(1 - v / 0.06, 0, 1) * 0.35     # 바닥 선을 조금 더 밝게
    a = np.clip((fade * (0.55 + 0.45 * streak) + base) * 1.6, 0, 1)  # 1차 렌더에서 흐려서 1.6배
    return _save_gray("vfx_wall_fade", a[::-1])   # 이미지 윗줄 = V 1


def tex_slash_strip(w=512, h=128):
    """초승달 검기: V 1(바깥 날)이 날카롭게 밝고 안쪽으로 번진다. 양 끝(U 0·1)은 가늘게 사라진다. 결이 호를 따라 흐른다."""
    u = np.linspace(0, 1, w)[None, :]
    v = np.linspace(0, 1, h)[:, None]           # 0 = 안, 1 = 날
    edge = np.exp(-((1 - v) / 0.10) ** 2)        # 날 선
    body = np.clip(v, 0, 1) ** 1.4 * 0.8         # 안으로 번짐(1차 렌더는 날 선만 보여 가늘었다)
    grain = 0.75 + 0.25 * _noise1d(h, 3)[:, None]  # 호를 따라 흐르는 결(가로줄)
    ends = np.sin(np.pi * np.clip(u, 0, 1)) ** 0.6
    a = (edge + body * grain) * ends
    return _save_gray("vfx_slash_strip", a[::-1])


def tex_spike(w=128, h=256):
    """땅 폭발 날: 뿌리(V 0)가 넓고 밝고 끝(V 1)으로 뾰족해진다."""
    u = np.linspace(-1, 1, w)[None, :]
    v = np.linspace(0, 1, h)[:, None]
    half = 0.9 * (1 - v) ** 0.8 + 0.02           # 높이에 따른 반폭
    shape = np.clip(1 - np.abs(u) / half, 0, 1) ** 0.7
    bright = (1 - v) ** 0.5
    a = shape * bright * np.clip(v / 0.05, 0.4, 1)
    return _save_gray("vfx_spike", a[::-1])


# ------------------------------------------------------------------ 메시


def _mesh(name, verts, faces, uvs, double=False):
    """faces: 꼭짓점 번호 튜플, uvs: 면마다 꼭짓점별 (u, v). double이면 뒤집은 쌍둥이 면을 붙인다."""
    bm = bmesh.new()
    bv = [bm.verts.new(p) for p in verts]
    uvl = bm.loops.layers.uv.new("UVMap")
    sets = [(f, uv) for f, uv in zip(faces, uvs)]
    if double:
        sets += [(tuple(reversed(f)), tuple(reversed(uv))) for f, uv in zip(faces, uvs)]
    dup = {}
    for f, uv in sets:
        key = frozenset(f)
        vs = [bv[i] for i in f]
        if key in dup:  # 같은 꼭짓점으로 면을 두 번 못 만든다 → 쌍둥이는 꼭짓점을 복제
            vs = [bm.verts.new(bv[i].co) for i in f]
        dup[key] = True
        face = bm.faces.new(vs)
        for loop, (u, v) in zip(face.loops, uv):
            loop[uvl].uv = (u, v)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return me


def mesh_ring_flat(name, seg=64, r0=0.25, r1=0.48, z=0.002):
    """위에서 본 평면 투영 UV — UV 사각 [0,1]² = 가로세로 1. Kenney circle_02(빛 r≈0.26~0.37)·circle_03(0.26~0.47)이 그대로 얹힌다."""
    verts, faces, uvs = [], [], []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        for r in (r0, r1):
            verts.append((r * math.cos(a), r * math.sin(a), z))
    for i in range(seg):
        j = (i + 1) % seg
        f = (2 * i, 2 * i + 1, 2 * j + 1, 2 * j)
        faces.append(f)
        uvs.append(tuple((verts[k][0] + 0.5, verts[k][1] + 0.5) for k in f))
    return _mesh(name, verts, faces, uvs)


def mesh_ring_wall(name, seg=64, rb=0.42, rt=0.5, hgt=0.12, reps=4):
    """바깥으로 벌어진 낮은 벽(워크3 천둥박수·발구르기의 솟는 먼지 고리). U = 둘레(reps번 반복), V = 바닥 0 → 위 1."""
    verts, faces, uvs = [], [], []
    for i in range(seg + 1):  # 이음매에서 U가 끊기지 않게 첫 줄을 한 번 더
        a = 2 * math.pi * i / seg
        verts.append((rb * math.cos(a), rb * math.sin(a), 0.0))
        verts.append((rt * math.cos(a), rt * math.sin(a), hgt))
    for i in range(seg):
        f = (2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1)  # 바깥을 보는 면
        u0, u1 = reps * i / seg, reps * (i + 1) / seg
        faces.append(f)
        uvs.append(((u0, 0), (u1, 0), (u1, 1), (u0, 1)))
    return _mesh(name, verts, faces, uvs, double=True)


def mesh_crescent(name, seg=40, span=170, r_out=0.5, r_in_mid=0.22, cup=0.04):
    """가로로 누운 초승달. 호의 가운데가 −Y(유니티 앞 +Z). 바깥 날 반지름 0.5 고정, 안쪽 반지름이 가운데 0.22(1차 0.30은 게임 각도에서 실처럼 가늘었다) → 양 끝 0.5로 좁아져 뾰족하다.
    바깥 날이 조금 들려(cup) 비스듬히 봐도 두께가 읽힌다. U = 호(0 → 1), V = 안 0 → 날 1."""
    verts, faces, uvs = [], [], []
    rows = 3
    for i in range(seg + 1):
        t = i / seg
        a = math.radians(-90 - span / 2 + span * t)
        taper = math.sin(math.pi * t) ** 0.8
        r_in = r_out - (r_out - r_in_mid) * taper
        for k in range(rows + 1):
            s = k / rows
            r = r_in + (r_out - r_in) * s
            verts.append((r * math.cos(a), r * math.sin(a), cup * s * s * taper))
    for i in range(seg):
        for k in range(rows):
            p = i * (rows + 1) + k
            q = (i + 1) * (rows + 1) + k
            f = (p, q, q + 1, p + 1)
            faces.append(f)
            uvs.append(((i / seg, k / rows), ((i + 1) / seg, k / rows), ((i + 1) / seg, (k + 1) / rows), (i / seg, (k + 1) / rows)))
    return _mesh(name, verts, faces, uvs, double=True)


def mesh_pillar(name, planes=3, width=0.3, hgt=1.0, u0=0.4, u1=0.6, v0=0.17, v1=0.83):
    """세로판 3장 60° 교차 = 빛 기둥. Kenney trace_06(빛이 가로 0.39~0.61·세로 0.17~0.83)을 U 0.4~0.6·V 0.17~0.83으로 잘라 판을 채운다(안 자르면 기둥이 땅에서 뜬다).
    🔴 1차는 spark_05(들쭉날쭉한 번개)를 얹었는데 교차판마다 번개가 어긋나 새장처럼 보였다 — 번개 줄기는 이 기둥 위에
    카메라를 보는 스프라이트(spark_05)로 따로 겹친다(README)."""
    verts, faces, uvs = [], [], []
    for p in range(planes):
        a = math.pi * p / planes
        dx, dy = math.cos(a) * width / 2, math.sin(a) * width / 2
        n = len(verts)
        verts += [(-dx, -dy, 0), (dx, dy, 0), (dx, dy, hgt), (-dx, -dy, hgt)]
        faces.append((n, n + 1, n + 2, n + 3))
        uvs.append(((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
    return _mesh(name, verts, faces, uvs, double=True)


def mesh_burst(name, blades=12, r_base=0.08, r_tip=0.5, hgt=0.55, w_base=0.13, seed=5):
    """땅에서 사방으로 솟는 날(원작 k3의 S1/S2 가시). 날마다 기울기·길이를 조금씩 흔든다. V = 뿌리 0 → 끝 1."""
    rng = np.random.default_rng(seed)
    verts, faces, uvs = [], [], []
    for b in range(blades):
        a = 2 * math.pi * (b + rng.uniform(-0.2, 0.2)) / blades
        L = rng.uniform(0.8, 1.0)
        cx, cy = math.cos(a), math.sin(a)
        tx, ty = -cy, cx  # 날 폭 방향(둘레 방향)
        rt, ht, wb = r_base + (r_tip - r_base) * L, hgt * L * rng.uniform(0.85, 1.0), w_base * rng.uniform(0.85, 1.1)
        n = len(verts)
        verts += [(r_base * cx - tx * wb / 2, r_base * cy - ty * wb / 2, 0),
                  (r_base * cx + tx * wb / 2, r_base * cy + ty * wb / 2, 0),
                  (rt * cx + tx * wb * 0.18, rt * cy + ty * wb * 0.18, ht),
                  (rt * cx - tx * wb * 0.18, rt * cy - ty * wb * 0.18, ht)]
        faces.append((n, n + 1, n + 2, n + 3))
        uvs.append(((0, 0), (1, 0), (1, 1), (0, 1)))
    return _mesh(name, verts, faces, uvs, double=True)


# 이름 → (메시 함수, 텍스처 파일(폴더, 이름), 텍스처 그리는 함수 또는 None)
ASSETS = {
    "VFX_충격파_원판": (mesh_ring_flat, (KENNEY, "circle_03"), None),
    "VFX_충격파_벽": (mesh_ring_wall, (TEX_OUT, "vfx_wall_fade"), tex_wall_fade),
    "VFX_초승달_검기": (mesh_crescent, (TEX_OUT, "vfx_slash_strip"), tex_slash_strip),
    "VFX_번개_기둥": (mesh_pillar, (KENNEY, "trace_06"), None),
    "VFX_땅_폭발": (mesh_burst, (TEX_OUT, "vfx_spike"), tex_spike),
}


def material(tex_folder, tex_name, tint=(1, 1, 1)):
    """미리보기용 가산 재질(발광 × 텍스처, 알파 = 텍스처). 유니티는 이 재질을 안 쓴다 — Resources/Effects/Skill_<텍스처>.mat을 쓴다."""
    name = "VFX_" + tex_name
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(os.path.join(tex_folder, tex_name + ".png"), check_existing=True)
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*tint, 1)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    add = nt.nodes.new("ShaderNodeAddShader")
    mul = nt.nodes.new("ShaderNodeMixRGB")
    mul.blend_type = "MULTIPLY"
    mul.inputs[0].default_value = 1.0
    nt.links.new(img.outputs["Color"], mul.inputs[1])
    mul.inputs[2].default_value = (*tint, 1)
    nt.links.new(mul.outputs[0], em.inputs["Color"])
    em.inputs["Strength"].default_value = 2.0
    nt.links.new(em.outputs[0], add.inputs[0])
    nt.links.new(tr.outputs[0], add.inputs[1])
    nt.links.new(add.outputs[0], out.inputs["Surface"])
    for attr, val in (("blend_method", "BLEND"), ("surface_render_method", "BLENDED")):
        if hasattr(m, attr):
            try:
                setattr(m, attr, val)
            except TypeError:
                pass
    m.use_backface_culling = True
    return m


def build(name, collection, location=(0, 0, 0), scale=1.0, draw_textures=True):
    fn, (folder, tex), draw = ASSETS[name]
    if draw and draw_textures:
        draw()
    me = fn(name)
    me.materials.append(material(folder, tex))
    ob = bpy.data.objects.new(name, me)
    ob.location = location
    ob.scale = (scale, scale, scale)
    collection.objects.link(ob)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    assert tris <= TRI_LIMIT, f"{name} 삼각형 {tris} > {TRI_LIMIT}"
    return ob, tris


def build_in_window(spacing=40.0, scale=22.5):
    """사장님 창에 판_스킬이펙트를 새로 세운다(적 키 22.5 곁에 실제 권장 크기로). 기존 장면은 안 건드린다."""
    col = bpy.data.collections.get("판_스킬이펙트")
    if col is None:
        col = bpy.data.collections.new("판_스킬이펙트")
        bpy.context.scene.collection.children.link(col)
    mine = [o.name for o in col.objects if o.name.split(".")[0] in ASSETS or o.name.startswith("라벨_VFX")]
    for n in mine:
        bpy.data.objects.remove(bpy.data.objects[n])
    rec = {"VFX_충격파_원판": 60, "VFX_충격파_벽": 45, "VFX_초승달_검기": 50, "VFX_번개_기둥": 90, "VFX_땅_폭발": 40}
    for i, name in enumerate(ASSETS):
        ob, _ = build(name, col, (i * spacing * 2, 0, 0), rec[name], draw_textures=True)
        cu = bpy.data.curves.new("라벨_" + name, "FONT")
        cu.body = name.replace("VFX_", "")
        lab = bpy.data.objects.new("라벨_" + name, cu)
        lab.location = (i * spacing * 2 - 15, -40, 0)
        lab.scale = (6, 6, 6)
        col.objects.link(lab)
    return col


def main():
    picked = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(ASSETS)
    os.makedirs(OUT, exist_ok=True)
    for name in picked:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        col = bpy.data.collections.new("판_스킬이펙트")
        bpy.context.scene.collection.children.link(col)
        ob, tris = build(name, col)
        assert {o.name for o in bpy.context.scene.objects} == {name}
        dims = tuple(round(d, 3) for d in ob.dimensions)
        path = os.path.join(OUT, name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=False, object_types={"MESH"}, global_scale=1.0,
                                 bake_space_transform=True, apply_scale_options="FBX_SCALE_ALL", path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False,
                                 mesh_smooth_type="FACE")
        print(f"만듦  {name}  삼각형 {tris}  크기(x,y,z) {dims}  → {os.path.relpath(path, ROOT)}")


if __name__ == "__main__":
    main()
