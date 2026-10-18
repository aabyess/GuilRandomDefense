"""S2 신규 기능 소품 중 원작에서 그대로 못 가져온 둘을 Blender로 다시 만든다 (2026-10-18, PM 지시).

  blender -b --factory-startup --python Tools/blender/gen_s2_props.py -- [elevator] [kinetic] [sheet]
  산출: ~/grd/GRD_s2_models/gen_ElevatorPuzzle/ · gen_kineticfield/  (각 <이름>.fbx · Textures/*.png · <이름>.json · preview*.png)

1) ElevatorPuzzle(B007 엘리베이터): 원작 MDX 지오셋(328×328 평판, 타일 24장 UV 아틀라스)을 그대로 쓰고,
   맵 안에 없는 워크3 대체 텍스처(ReplaceableId 11, 지형 텍스처 — w3b btxf 비어 있음)를 대신할 아틀라스를 그린다.
   원작 층 필터가 additive라 검정=투명인 빛나는 타일 무늬. 원작 클립 9개(Birth/Stand/Death × 1~3단)는 뼈가 1개뿐이라
   모양 변화가 없어 정적 메시로 낸다(클립 이름은 json에 보관).
2) kineticfield_fx_stand(진화체 실루엣 밑 필드): 원작은 메시 없이 파티클 3개뿐(json pre2).
   그 값(텍스처 Star8b·ping4, 색, 크기, 수명, 알파 곡선)대로 바닥 필드를 만든다: 얇은 원판(별빛) + 퍼지는 별 3겹 + 회전 링 2겹,
   1초(30프레임) 루프. 오브젝트 이동·크기·회전 애니는 FBX 오브젝트별 액션(<이름>|Scene)으로, 알파 곡선은 json(layers[].alphaKeys)으로.
   색은 우리 반입기(CinematicImporter colorsRGB01)와 같은 RGB 해석 — 파랑 계열.
단위: 워크3 1 = 0.01m. 방향 규칙은 export_mdx_anim_fbx.py와 같음(axis_forward=-Z, axis_up=Y). 재질 이름 끝 _add = 가산.
텍스처(Star8b·ping4·대체 지형)는 워크3 기본 파일이라 맵에 없어 이 스크립트가 그린 근사 그림이다.
"""
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_geo                                                       # noqa: E402

H = os.path.expanduser("~")
ROOT = H + "/grd/GRD_s2_models"
WORK = ROOT + "/_work"
SC = 0.01
FPS = 30


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    return sc


# ---- 텍스처(numpy → PNG) -----------------------------------------------------
def save_png(arr, path):
    """arr HxWx4 float 0..1 (위가 0행)."""
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).astype(np.float32).ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def grid(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    return (x + 0.5) / n * 2 - 1, (y + 0.5) / n * 2 - 1


def tex_star8(n=256):
    """Star8b 근사: 가운데 빛 + 긴 광선 4 + 짧은 대각 광선 4 (흰색, 가산용 검정 배경)."""
    x, y = grid(n)
    r = np.hypot(x, y) + 1e-6
    a = np.arctan2(y, x)
    core = np.exp(-(r / 0.16) ** 2) + 0.35 * np.exp(-(r / 0.45) ** 2)
    long_ = np.abs(np.cos(2 * a)) ** 60 * np.clip(1 - r, 0, 1) ** 1.5
    diag = np.abs(np.sin(2 * a)) ** 90 * np.clip(1 - r / 0.62, 0, 1) ** 1.8 * 0.6
    v = np.clip(core + long_ + diag, 0, 1)
    return np.dstack([v, v, v, np.ones_like(v)])


def tex_ping_ring(n=256):
    """ping4(미니맵 핑) 근사: 가는 밝은 고리 + 안쪽으로 번지는 꼬리."""
    x, y = grid(n)
    r = np.hypot(x, y)
    ring = np.exp(-((r - 0.82) / 0.05) ** 2)
    inner = np.clip((r - 0.35) / 0.47, 0, 1) ** 3 * (r < 0.82) * 0.45
    v = np.clip(ring + inner, 0, 1) * (r < 1)
    return np.dstack([v, v, v, np.ones_like(v)])


def tex_floor_disc(n=256):
    """원판: 부드러운 원 + 가는 동심원 두 줄 + 8방 눈금."""
    x, y = grid(n)
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    base = np.clip(1 - r, 0, 1) ** 1.6 * 0.35
    lines = np.exp(-((r - 0.55) / 0.012) ** 2) * 0.8 + np.exp(-((r - 0.9) / 0.015) ** 2)
    ticks = (np.abs(np.sin(4 * a)) < 0.06) * ((r > 0.58) & (r < 0.88)) * 0.6
    v = np.clip(base + lines + ticks, 0, 1)
    return np.dstack([v, v, v, np.ones_like(v)])


def tex_elevator_atlas(n=512):
    """대체 지형 텍스처 근사(가산): 4×4 칸마다 돌 타일 가장자리 빛 + 칸 안 룬 무늬. 검정 = 투명."""
    out = np.zeros((n, n, 4), np.float32)
    out[..., 3] = 1
    c = n // 4
    gold = np.array([1.0, 0.78, 0.35])
    teal = np.array([0.35, 0.95, 1.0])
    yy, xx = np.mgrid[0:c, 0:c].astype(np.float32)
    u = (xx + 0.5) / c * 2 - 1
    v = (yy + 0.5) / c * 2 - 1
    edge = np.maximum(np.abs(u), np.abs(v))
    rim = np.exp(-((edge - 0.9) / 0.035) ** 2)
    r = np.hypot(u, v)
    rng = np.random.default_rng(3)
    for i in range(4):
        for j in range(4):
            k = rng.integers(0, 3)
            if k == 0:
                rune = np.exp(-((r - 0.45) / 0.03) ** 2) + (np.abs(u) < 0.025) * (r < 0.45) + (np.abs(v) < 0.025) * (r < 0.45)
            elif k == 1:
                rune = np.exp(-((np.abs(u) + np.abs(v) - 0.55) / 0.03) ** 2) + np.exp(-(r / 0.08) ** 2)
            else:
                rune = np.exp(-((r - 0.3) / 0.025) ** 2) + np.exp(-((r - 0.6) / 0.025) ** 2) * (np.abs(np.sin(3 * np.arctan2(v, u))) > 0.3)
            col = rim[..., None] * gold * 0.9 + np.clip(rune, 0, 1)[..., None] * teal * 0.8
            out[i * c:(i + 1) * c, j * c:(j + 1) * c, :3] = np.clip(col, 0, 1)
    return out


# ---- 재질 ---------------------------------------------------------------------
def add_mat(name, png, color=(1, 1, 1)):
    """가산(_add): 미리보기 렌더는 Emission+Transparent 더하기. FBX에는 이미지 텍스처 이름만 실린다."""
    m = bpy.data.materials.new(name + "_add")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(png, check_existing=True)
    oi = nt.nodes.new("ShaderNodeObjectInfo")
    mul = nt.nodes.new("ShaderNodeVectorMath")
    mul.operation = "MULTIPLY"
    nt.links.new(tn.outputs["Color"], mul.inputs[0])
    mul.inputs[1].default_value = color
    mul2 = nt.nodes.new("ShaderNodeVectorMath")
    mul2.operation = "MULTIPLY"
    nt.links.new(mul.outputs["Vector"], mul2.inputs[0])
    nt.links.new(oi.outputs["Color"], mul2.inputs[1])            # 오브젝트 color.rgb = 색 곡선(미리보기용)
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(mul2.outputs["Vector"], em.inputs["Color"])
    nt.links.new(oi.outputs["Alpha"], em.inputs["Strength"])     # 오브젝트 color.a = 알파 곡선(미리보기용)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    ad = nt.nodes.new("ShaderNodeAddShader")
    nt.links.new(tr.outputs[0], ad.inputs[0])
    nt.links.new(em.outputs[0], ad.inputs[1])
    nt.links.new(ad.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        pass
    m.use_backface_culling = False
    return m


def plane(name, size, mat, z=0.0):
    me = bpy.data.meshes.new(name)
    s = size / 2
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    uv = me.uv_layers.new(name="UVMap")
    for li, (u, v) in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
        uv.data[li].uv = (u, v)
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.location.z = z
    o.color = (1, 1, 1, 1)
    return o


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)


def lerp3(keys, t):
    """keys [(t,(…))] 선형."""
    for (t0, a), (t1, b) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            k = (t - t0) / max(t1 - t0, 1e-6)
            return tuple(x + (y - x) * k for x, y in zip(a, b))
    return keys[-1][1]


# ==================== 1) ElevatorPuzzle ====================
def build_elevator():
    sc = reset()
    od = ROOT + "/gen_ElevatorPuzzle"
    os.makedirs(od + "/Textures", exist_ok=True)
    png = od + "/Textures/ElevatorPuzzle_atlas.png"
    save_png(tex_elevator_atlas(), png)
    g = mdx_geo.parse(open(WORK + "/D\\ElevatorPuzzle.mdx", "rb").read())
    gs = g["geosets"][0]
    me = bpy.data.meshes.new("ElevatorPuzzle")
    verts = [(x * SC, y * SC, z * SC) for x, y, z in gs["verts"]]
    tris = [tuple(t) for t in gs["tris"] if len(set(t)) == 3]
    me.from_pydata(verts, [], tris)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            u, v = gs["uvs"][vi]
            uvl.data[li].uv = (u, 1 - v)
    me.update()
    me.materials.append(add_mat("ElevatorPuzzle_g0_L0", png))
    o = bpy.data.objects.new("ElevatorPuzzle", me)
    sc.collection.objects.link(o)
    o.rotation_euler = (0, 0, math.radians(-90))
    o.color = (1, 1, 1, 1)
    export([o], od + "/ElevatorPuzzle.fbx")
    seqs = ["Stand", "Death", "StandSecond", "DeathSecond", "StandThird", "BirthSecond", "Birth", "DeathThird", "BirthThird"]
    json.dump(dict(model="ElevatorPuzzle(재제작)", source="S2 D\\ElevatorPuzzle.mdl (B007)", scaleNote="워크3 1 = 0.01m",
                   sizeWc3=[328, 328, 0], meshes=[dict(mesh="ElevatorPuzzle", filter="additive", suffix="add", textureFile=os.path.basename(png),
                                                       note="원작 텍스처 = ReplaceableId 11(워크3 지형, 맵 밖) → 근사 아틀라스")],
                   originalSequences=seqs, sequencesNote="뼈 1개·지오셋 1개라 모양 변화 없음 — 정적 메시", approx=True),
              open(od + "/ElevatorPuzzle.json", "w"), ensure_ascii=False, indent=1)
    preview([o], od + "/preview.png", frames=[1], dist=9.0)
    return od


# ==================== 2) kineticfield ====================
def build_kinetic():
    sc = reset()
    N = 30
    sc.frame_start, sc.frame_end = 1, N
    od = ROOT + "/gen_kineticfield"
    os.makedirs(od + "/Textures", exist_ok=True)
    star_png, ring_png, disc_png = od + "/Textures/kf_star8.png", od + "/Textures/kf_ping_ring.png", od + "/Textures/kf_disc.png"
    save_png(tex_star8(), star_png)
    save_png(tex_ping_ring(), ring_png)
    save_png(tex_floor_disc(), disc_png)
    side = json.load(open(ROOT + "/kineticfield_fx_stand/kineticfield_fx_stand.json"))
    p_star, p_r1, p_r2 = side["pre2"]
    layers, objs = [], []

    def colkeys(p):
        return [(0.0, tuple(p["colors"][0])), (p["mid"], tuple(p["colors"][1])), (1.0, tuple(p["colors"][2]))]

    def alphakeys(p):
        return [(0.0, (p["alpha"][0] / 255.0,)), (p["mid"], (p["alpha"][1] / 255.0,)), (1.0, (p["alpha"][2] / 255.0,))]

    def scalekeys(p):
        return [(0.0, (p["scale"][0],)), (p["mid"], (p["scale"][1],)), (1.0, (p["scale"][2],))]

    # 바닥 원판(새로 넣음 — PM: 얇은 원판): 별 색 가운데 값, 은은하게, 천천히 회전
    disc = plane("kf_disc", 2.6, add_mat("kf_disc", disc_png, (1, 1, 1)), z=0.01)
    for f in (1, N + 1):
        disc.rotation_euler = (0, 0, math.radians(0 if f == 1 else 36))
        disc.keyframe_insert("rotation_euler", frame=f)
    disc.color = (*p_star["colors"][1], 0.55)
    objs.append(disc)
    layers.append(dict(mesh="kf_disc", texture=os.path.basename(disc_png), blend="add", color=p_star["colors"][1], alphaKeys=[[0, 0.55], [1, 0.55]], note="새로 넣은 바닥 원판(원작엔 없음)"))

    # 별 파티클(BlizParticle01): 크기 width 125 × scale 1→1.3→2, 수명 1s, 위로 100/s, 알파 90→90→11. 루프 안에 3겹 엇갈림.
    life = p_star["lifespan"]
    for k in range(3):
        mat = add_mat("kf_star%d" % k, star_png, (1, 1, 1))
        o = plane("kf_star%d" % k, p_star["width"] * SC, mat)
        for f in range(1, N + 2):
            t = (((f - 1) / N) + k / 3.0) % 1.0          # 0..1 수명 비율
            s = lerp3(scalekeys(p_star), t)[0]
            o.scale = (s, s, s)
            o.location.z = p_star["speed"] * SC * t * life * 0.35     # 위로 떠오름(바닥 필드라 1/3만)
            o.rotation_euler = (0, 0, math.radians(22.5 * k + 30 * t))
            col = lerp3(colkeys(p_star), t)
            a = lerp3(alphakeys(p_star), t)[0]
            if t > 0.97 or t < 0.03:
                a = 0.0                                        # 되감는 순간 깜박임 숨김
            o.color = (*col, a * 2.2)
            for pth in ("scale", "location", "rotation_euler", "color"):
                o.keyframe_insert(pth, frame=f)
        objs.append(o)
        layers.append(dict(mesh=o.name, texture=os.path.basename(star_png), blend="add", phase=k / 3.0, lifespan=life,
                           colorKeys=[[t, list(c)] for t, c in colkeys(p_star)], alphaKeys=[[t, a[0]] for t, a in alphakeys(p_star)],
                           scaleKeys=[[t, s[0]] for t, s in scalekeys(p_star)], baseSize=p_star["width"] * SC))
    # 링(BlizParticle08shockwaves ×2): 크기 163 고정, 수명 0.9s, 알파 20→0. 하나는 안→밖(+50), 하나는 밖→안(-50), 서로 반대로 회전
    for k, p in enumerate((p_r1, p_r2)):
        for j in range(2):
            mat = add_mat("kf_ring%d_%d" % (k, j), ring_png, (1, 1, 1))
            o = plane("kf_ring%d_%d" % (k, j), p["scale"][0] * SC, mat, z=0.02)
            for f in range(1, N + 2):
                t = (((f - 1) / N) + j / 2.0 + k * 0.25) % 1.0
                grow = 0.7 + 0.5 * t if p["speed"] > 0 else 1.2 - 0.5 * t
                o.scale = (grow, grow, grow)
                o.rotation_euler = (0, 0, math.radians((120 if k == 0 else -120) * t))
                col = lerp3(colkeys(p), t)
                a = lerp3(alphakeys(p), t)[0] * 6.0          # 원작 20/255는 파티클 30개/초가 겹쳐 보이는 밝기 → 2겹이라 키움
                if t > 0.97 or t < 0.03:
                    a = 0.0
                o.color = (*col, a)
                for pth in ("scale", "rotation_euler", "color"):
                    o.keyframe_insert(pth, frame=f)
            objs.append(o)
            layers.append(dict(mesh=o.name, texture=os.path.basename(ring_png), blend="add", phase=j / 2.0 + k * 0.25, lifespan=p["lifespan"],
                               colorKeys=[[t, list(c)] for t, c in colkeys(p)], alphaKeys=[[t, a[0] * 6.0] for t, a in alphakeys(p)],
                               scaleNote="크기 고정 %.2fm, %s" % (p["scale"][0] * SC, "안→밖 0.7→1.2배" if p["speed"] > 0 else "밖→안 1.2→0.7배")))
    for o in objs:
        ad = o.animation_data
        if ad and ad.action:
            try:
                for layer in ad.action.layers:
                    for strip in layer.strips:
                        for cb in strip.channelbags:
                            for fc in cb.fcurves:
                                for kp in fc.keyframe_points:
                                    kp.interpolation = "LINEAR"
            except Exception:
                pass
    root = bpy.data.objects.new("kineticfield", None)
    sc.collection.objects.link(root)
    for o in objs:
        o.parent = root
    root.rotation_euler = (0, 0, math.radians(-90))
    export([root] + objs, od + "/kineticfield.fbx")
    json.dump(dict(model="kineticfield(재제작)", source="S2 kineticfield_fx_stand.mdx (파티클 3개, 메시 없음)", scaleNote="워크3 1 = 0.01m",
                   loop=dict(frames=N, fps=FPS, seconds=N / FPS, action="<오브젝트>|Scene (오브젝트마다 하나, 같은 30프레임 루프)"), layers=layers,
                   colorNote="원작 PRE2 색을 RGB로 읽음(CinematicImporter colorsRGB01과 같은 해석)",
                   alphaNote="FBX에 못 싣는 층 알파 — alphaKeys(수명 비율 0..1 → 알파 0..1)를 쓸 것. 미리보기는 오브젝트 color.a로 그림",
                   approx=["kf_star8.png=Star8b 근사", "kf_ping_ring.png=ping4 근사", "kf_disc.png=새로 그림"]),
              open(od + "/kineticfield.json", "w"), ensure_ascii=False, indent=1)
    preview(objs, od + "/preview.png", frames=[1, 8, 15, 22], dist=6.0)
    return od


# ---- 미리보기 ----------------------------------------------------------------------
def preview(objs, path, frames, dist=6.0):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            pass
    sc.render.resolution_x, sc.render.resolution_y = 640, 480
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.03, 0.03, 0.05, 1)
    sc.world = w
    cd = bpy.data.cameras.new("cam")
    cd.lens = 35
    cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam)
    cam.location = (0, -dist * 0.8, dist * 0.6)
    cam.rotation_euler = (math.radians(53), 0, 0)
    sc.camera = cam
    shots = []
    for f in frames:
        sc.frame_set(f)
        p = path.replace(".png", "_f%02d.png" % f)
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        shots.append(p)
    # 한 장으로 합치기(Blender 이미지로)
    ims = [bpy.data.images.load(p) for p in shots]
    W, Hh = ims[0].size
    arr = np.zeros((Hh, W * len(ims), 4), np.float32)
    for i, im in enumerate(ims):
        a = np.array(im.pixels[:], np.float32).reshape(Hh, W, 4)
        arr[:, i * W:(i + 1) * W] = a
    save_png(arr[::-1], path)
    for p in shots:
        os.remove(p)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    todo = args or ["elevator", "kinetic"]
    if "elevator" in todo:
        print("OK", build_elevator())
    if "kinetic" in todo:
        print("OK", build_kinetic())
