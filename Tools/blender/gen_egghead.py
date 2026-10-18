"""에그헤드 건물 모델(14번째 스토리 「건물형 적」) — 원작 워크3 기본 ArcaneSanctum(마법 학교) 모습을 베껴 Blender로 만든다 (2026-10-18, PM 지시).

  blender -b --factory-startup --python Tools/blender/gen_egghead.py -- [--out 루트]
  산출: ~/grd/GRD_s2_models/gen_Egghead/ Egghead.fbx(정적) · Egghead@Idle/Hit/Death.fbx(오브젝트 애니, 테이크 이름=클립 이름) ·
        Textures/*.png · Egghead.json · preview.png · sheet.png
원작 ArcaneSanctum은 맵에 없다(워크3 기본). 그 실루엣을 따른다: 넓은 청회색 돌 몸체 + 가운데 높은 원뿔 지붕 첨탑 +
네 모서리 작은 첨탑 + 정면 아치 문 + 꼭대기 빛나는 구슬. 텍스처는 numpy로 그린 근사 그림.
크기: 모델은 높이 7.6m·바닥 4.5m 정사각(=우리 월드 키 ≈76·바닥 ≈45의 1/10). ArtBinder가 키 기준으로 맞추므로 json heightRef만 본다.
클립: Idle(구슬 둥실·첨탑 미세 흔들림, 루프 2초) · Hit(0.4초 흔들) · Death(1.6초 무너짐 — 첨탑이 기울며 낙하, 몸체가 가라앉음).
"""
import json, math, os, sys
import bpy
import numpy as np

H = os.path.expanduser("~")
OUT = H + "/grd/GRD_s2_models/gen_Egghead"
if "--out" in sys.argv:
    OUT = sys.argv[sys.argv.index("--out") + 1]
FPS = 30
HEIGHT, BASE = 7.6, 4.5


def save_png(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).astype(np.float32).ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def noise(n, seed, sc=8):
    r = np.random.RandomState(seed)
    a = r.rand(n // sc + 2, n // sc + 2)
    from numpy import linspace
    xi = np.linspace(0, a.shape[0] - 1.001, n)
    i = xi.astype(int); f = xi - i
    row = a[i][:, i] * 0
    A = a[i][:, i]; B = a[i + 1][:, i]; C = a[i][:, i + 1]; D = a[i + 1][:, i + 1]
    fx = f[:, None]; fy = f[None, :]
    return A * (1 - fx) * (1 - fy) + B * fx * (1 - fy) + C * (1 - fx) * fy + D * fx * fy


def tex_stone(n=256):
    """청회색 벽돌 — 줄눈."""
    r = np.random.RandomState(1)
    base = np.array([0.46, 0.5, 0.62])
    y, x = np.mgrid[0:n, 0:n]
    bh, bw = n // 8, n // 4
    row = y // bh
    xx = (x + (row % 2) * bw // 2) % bw
    mortar = ((y % bh) < 3) | (xx < 3)
    shade = 0.8 + 0.2 * noise(n, 2) + 0.1 * noise(n, 3, 3)
    bid = (row * 7 + (x + (row % 2) * bw // 2) // bw) % 5
    tone = 0.9 + 0.04 * bid
    img = np.zeros((n, n, 4), np.float32); img[..., 3] = 1
    img[..., :3] = base[None, None] * (shade * tone)[..., None]
    img[mortar, :3] = (0.22, 0.24, 0.3)
    return img


def tex_roof(n=256):
    """보랏빛 푸른 기와 — 가로 줄."""
    y, x = np.mgrid[0:n, 0:n]
    th = n // 10
    row = y // th
    xx = (x + (row % 2) * 16) % 32
    line = ((y % th) < 3) | (xx < 2)
    g = 0.75 + 0.25 * noise(n, 5, 4)
    img = np.zeros((n, n, 4), np.float32); img[..., 3] = 1
    img[..., 0] = 0.30 * g; img[..., 1] = 0.28 * g; img[..., 2] = 0.72 * g
    img[line, :3] *= 0.45
    return img


def tex_trim(n=64):
    img = np.zeros((n, n, 4), np.float32); img[..., 3] = 1
    g = 0.8 + 0.2 * noise(n, 7, 4)
    img[..., 0] = 0.9 * g; img[..., 1] = 0.72 * g; img[..., 2] = 0.3 * g   # 금장
    return img


def tex_glow(n=128):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    d = np.hypot(x - n / 2, y - n / 2) / (n / 2)
    v = np.clip(1.2 - d, 0, 1) ** 1.5
    img = np.zeros((n, n, 4), np.float32); img[..., 3] = 1
    img[..., 0] = 0.1 + 0.3 * v; img[..., 1] = 0.35 + 0.35 * v; img[..., 2] = 0.8 + 0.2 * v
    return img


def tex_door(n=128):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    img = np.zeros((n, n, 4), np.float32); img[..., 3] = 1
    plank = ((x // 16) % 2) * 0.05
    img[..., 0] = 0.32 + plank; img[..., 1] = 0.2 + plank; img[..., 2] = 0.1
    img[(x % 16) < 1.5, :3] = 0.1
    return img


def tex_window(n=64):
    img = np.zeros((n, n, 4), np.float32); img[..., 3] = 1
    img[..., 0] = 0.95; img[..., 1] = 0.85; img[..., 2] = 0.45
    y, x = np.mgrid[0:n, 0:n]
    img[(x % 32 < 2) | (y % 32 < 2), :3] = 0.25
    return img


def mat(name, png, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bs = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(png, check_existing=True)
    nt.links.new(tn.outputs["Color"], bs.inputs["Base Color"])
    bs.inputs["Roughness"].default_value = 0.8
    if emit:
        for k in ("Emission Color", "Emission"):
            if k in bs.inputs:
                nt.links.new(tn.outputs["Color"], bs.inputs[k]); break
        if "Emission Strength" in bs.inputs:
            bs.inputs["Emission Strength"].default_value = emit
    return m


def uv_box(o, scale=1.0):
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=1.0 / scale)
    bpy.ops.object.mode_set(mode="OBJECT")


def prim(kind, name, loc, size, m, parent=None, verts=None, rot=(0, 0, 0), uvs=1.0):
    if kind == "box":
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    elif kind == "cone":
        bpy.ops.mesh.primitive_cone_add(vertices=verts or 8, radius1=0.5, radius2=0, depth=1, location=loc)
    elif kind == "cyl":
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts or 12, radius=0.5, depth=1, location=loc)
    elif kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.5, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    o.rotation_euler = rot
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(m)
    uv_box(o, uvs)
    bpy.ops.object.shade_flat()
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")   # 부품 원점 = 자기 중심(애니 위치 키의 기준)
    if parent:
        o.parent = parent
        o.matrix_parent_inverse = parent.matrix_world.inverted()
    return o


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    os.makedirs(OUT + "/Textures", exist_ok=True)
    T = OUT + "/Textures/"
    for n, f in (("Egghead_stone", tex_stone), ("Egghead_roof", tex_roof), ("Egghead_trim", tex_trim),
                 ("Egghead_glow", tex_glow), ("Egghead_door", tex_door), ("Egghead_window", tex_window)):
        save_png(f(), T + n + ".png")
    stone = mat("Egghead_stone", T + "Egghead_stone.png")
    roof = mat("Egghead_roof", T + "Egghead_roof.png")
    trim = mat("Egghead_trim", T + "Egghead_trim.png")
    glow = mat("Egghead_glow", T + "Egghead_glow.png", emit=1.6)
    door = mat("Egghead_door", T + "Egghead_door.png")
    win = mat("Egghead_window", T + "Egghead_window.png", emit=1.5)

    root = bpy.data.objects.new("Egghead", None)
    sc.collection.objects.link(root)
    parts = {}
    B = BASE
    # 몸체(1층: 넓은 받침, 2층: 중간, 3층: 첨탑 몸통)
    parts["base"] = prim("box", "base", (0, 0, 0.5), (B, B, 1.0), stone, root, uvs=2.2)
    parts["hall"] = prim("box", "hall", (0, 0, 1.0 + 0.9), (B * 0.82, B * 0.82, 1.8), stone, root, uvs=2.2)
    parts["trim1"] = prim("box", "trim1", (0, 0, 2.85), (B * 0.9, B * 0.9, 0.18), trim, root)
    parts["tower"] = prim("cyl", "tower", (0, 0, 2.9 + 1.3), (B * 0.5, B * 0.5, 2.6), stone, root, verts=10, uvs=1.6)
    parts["trim2"] = prim("cyl", "trim2", (0, 0, 5.5 + 0.05), (B * 0.58, B * 0.58, 0.16), trim, root, verts=10)
    parts["spire"] = prim("cone", "spire", (0, 0, 5.6 + 0.8), (B * 0.58, B * 0.58, 1.6), roof, root, verts=10, uvs=1.6)
    parts["orbstem"] = prim("cyl", "orbstem", (0, 0, 7.3 - 0.05), (0.08, 0.08, 0.5), trim, root, verts=6)
    parts["orb"] = prim("sphere", "orb", (0, 0, HEIGHT - 0.18), (0.5, 0.5, 0.5), glow, root)
    # 지붕(2층 위 낮은 기와)
    parts["eave"] = prim("box", "eave", (0, 0, 3.0), (B * 0.95, B * 0.95, 0.1), roof, root)
    # 모서리 첨탑 4개
    for i, (sx, sy) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        cx, cy = sx * B * 0.45, sy * B * 0.45
        parts["pil%d" % i] = prim("cyl", "pil%d" % i, (cx, cy, 1.0 + 1.0), (0.55, 0.55, 2.0), stone, root, verts=8, uvs=1.2)
        parts["cap%d" % i] = prim("cone", "cap%d" % i, (cx, cy, 3.0 + 0.45), (0.7, 0.7, 0.9), roof, root, verts=8, uvs=1.2)
    # 정면 문(−Y) + 창
    parts["door"] = prim("box", "door", (0, -B * 0.41 - 0.02, 1.0 + 0.55), (0.9, 0.12, 1.1), door, root)
    parts["arch"] = prim("cyl", "arch", (0, -B * 0.41 - 0.02, 1.0 + 1.1), (0.9, 0.9, 0.12), door, root, verts=12, rot=(math.radians(90), 0, 0))
    for i, x in enumerate((-1.3, 1.3)):
        parts["win%d" % i] = prim("box", "win%d" % i, (x, -B * 0.41 - 0.02, 1.0 + 1.0), (0.5, 0.1, 0.7), win, root)
    for i, a in enumerate((0, 90, 180, 270)):
        r = B * 0.25 + 0.02
        parts["twin%d" % i] = prim("box", "twin%d" % i, (r * math.sin(math.radians(a)), -r * math.cos(math.radians(a)), 4.1),
                                   (0.35, 0.1, 0.6), win, root, rot=(0, 0, math.radians(a)))
    return sc, root, parts


# ---- 애니 -----------------------------------------------------------------------
def key(o, f, loc=None, rot=None, scale=None):
    if loc is not None:
        o.location = loc; o.keyframe_insert("location", frame=f)
    if rot is not None:
        o.rotation_euler = rot; o.keyframe_insert("rotation_euler", frame=f)
    if scale is not None:
        o.scale = scale; o.keyframe_insert("scale", frame=f)


def clear_anim(parts, root):
    for o in list(parts.values()) + [root]:
        o.animation_data_clear()


def anim_idle(parts, root, base):
    n = 60
    for f in (1, 31, 61):
        t = (f - 1) / 60 * 2 * math.pi
        o = parts["orb"]; key(o, f, loc=(base["orb"][0], base["orb"][1], base["orb"][2] + 0.18 * math.sin(t)))
        s = 1 + 0.08 * math.sin(t); key(o, f, scale=(s, s, s))
        for i in range(4):
            c = parts["cap%d" % i]; key(c, f, rot=(0.02 * math.sin(t + i), 0.02 * math.cos(t + i), 0))
    return 1, 61


def anim_hit(parts, root, base):
    for f, k in ((1, 0), (3, 1), (5, -1), (8, .6), (11, -.4), (13, 0)):
        key(root, f, loc=(0.12 * k, 0, 0), rot=(0, 0.015 * k, 0))
    return 1, 13


def anim_death(parts, root, base):
    f1, f2 = 1, 48
    for i in range(4):
        c, p = parts["cap%d" % i], parts["pil%d" % i]
        sx = 1 if i < 2 else -1
        for o in (c, p):
            b = base[o.name]
            key(o, f1, loc=b, rot=(0, 0, 0))
            key(o, 20 + 2 * i, loc=b, rot=(0.0, 0.0, 0.0))
            key(o, f2, loc=(b[0] + sx * 1.2, b[1] + (1.4 if i % 2 else -1.4), 0.25), rot=(1.2 * sx, 0.9, 0.3 * i))
    # 끝 자리: 받침(높이 0.55) 위에 폐허로 눕는다. (nm, 끝 위치, 기울기)
    ends = {"spire": ((0.9, 0.6, 1.0), 0.5), "orb": ((-1.6, -1.8, 0.25), 0.0), "orbstem": ((1.8, -0.9, 0.7), 0.6),
            "trim2": ((0.2, 0.3, 0.8), 0.1), "tower": ((0.1, 0.2, 1.15), 0.12)}
    for nm, (e, tilt) in ends.items():
        o = parts[nm]; b = base[nm]
        key(o, f1, loc=b, rot=(0, 0, 0))
        key(o, 10, loc=b, rot=(0, 0, 0))
        key(o, f2, loc=e, rot=(tilt, tilt * 0.6, 0))
        if nm == "tower":
            key(o, 10, scale=(1, 1, 1)); key(o, f2, scale=(1, 1, 0.45))
    for nm in ("hall", "trim1", "eave", "twin0", "twin1", "twin2", "twin3", "door", "arch", "win0", "win1"):
        o = parts[nm]; b = base[nm]
        key(o, f1, loc=b, rot=(0, 0, 0), scale=(1, 1, 1))
        key(o, 16, loc=b, rot=(0, 0, 0), scale=(1, 1, 1))
        key(o, f2, loc=(b[0] * 0.9, b[1] * 0.9, 0.7 if nm == "hall" else 0.6), rot=(0.05, 0, 0.04),
            scale=(1, 1, 0.3) if nm == "hall" else (0.3, 0.3, 0.3))
    key(parts["base"], f1, loc=base["base"], scale=(1, 1, 1)); key(parts["base"], f2, loc=(0, 0, 0.275), scale=(1.05, 1.05, 0.55))
    key(root, f1, loc=(0, 0, 0)); key(root, 20, loc=(0.05, 0, 0)); key(root, 26, loc=(-0.05, 0, 0)); key(root, f2, loc=(0, 0, 0))
    return f1, f2


def export(objs, path, scene_name):
    bpy.context.scene.name = scene_name
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)


def render(sc, path, frame, size=(512, 640), az=35):
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng; break
        except TypeError:
            pass
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.view_settings.view_transform = "Standard"
    if not sc.world:
        w = bpy.data.worlds.new("w"); w.use_nodes = True
        w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.62, 0.78, 1)
        sc.world = w
    if "cam" not in bpy.data.objects:
        cd = bpy.data.cameras.new("cam"); cd.lens = 40
        cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
        ld = bpy.data.lights.new("sun", "SUN"); ld.energy = 4
        sun = bpy.data.objects.new("sun", ld); sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sc.collection.objects.link(sun)
        ld2 = bpy.data.lights.new("fill", "SUN"); ld2.energy = 1.2
        fl = bpy.data.objects.new("fill", ld2); fl.rotation_euler = (math.radians(70), 0, math.radians(-120)); sc.collection.objects.link(fl)
    cam = bpy.data.objects["cam"]
    a = math.radians(az)
    d = 15
    cam.location = (d * math.sin(a) * 0.9, -d * math.cos(a) * 0.9, 6.5)
    cam.rotation_euler = (math.radians(76), 0, a)
    sc.frame_set(frame)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    sc, root, parts = build()
    base = {o.name: tuple(o.location) for o in parts.values()}
    allobj = [root] + list(parts.values())
    export(allobj, OUT + "/Egghead.fbx", "Egghead")
    clips = {}
    for nm, fn in (("Idle", anim_idle), ("Hit", anim_hit), ("Death", anim_death)):
        clear_anim(parts, root)
        for o in allobj:
            if o.name in base: o.location = base[o.name]
            o.rotation_euler = (0, 0, 0); o.scale = o.scale
        root.location = (0, 0, 0); root.rotation_euler = (0, 0, 0)
        for o in parts.values():
            o.scale = (1, 1, 1)
        a, b = fn(parts, root, base)
        sc.frame_start, sc.frame_end = a, b
        export(allobj, OUT + "/Egghead@%s.fbx" % nm, nm)
        clips[nm] = dict(file="Egghead@%s.fbx" % nm, frames=[a, b], seconds=round((b - a) / FPS, 2), loop=(nm == "Idle"))
    # 미리보기: 정지 / 피격 중간 / 죽음 3장
    clear_anim(parts, root)
    for o in parts.values():
        o.scale = (1, 1, 1); o.location = base[o.name]; o.rotation_euler = (0, 0, 0)
    prev = []
    for i, (nm, fr, az) in enumerate((("Idle", 1, 35), ("Idle", 1, 145), ("Death", 24, 35), ("Death", 48, 35))):
        if nm == "Death":
            clear_anim(parts, root); anim_death(parts, root, base)
        p = OUT + "/_p%d.png" % i
        render(sc, p, fr, az=az); prev.append(p)
    # 합치기
    ims = [bpy.data.images.load(p) for p in prev]
    W, Hh = ims[0].size
    arr = np.zeros((Hh, W * len(ims), 4), np.float32)
    for i, im in enumerate(ims):
        arr[:, i * W:(i + 1) * W] = np.array(im.pixels[:], np.float32).reshape(Hh, W, 4)
    save_png(arr[::-1], OUT + "/preview.png")
    for p in prev: os.remove(p)
    json.dump(dict(model="Egghead(제작)", source="워크3 기본 ArcaneSanctum(맵에 없음) 모습을 Blender로 베껴 만듦", scaleNote="모델 1m = 우리 월드 10",
                   heightRef=HEIGHT, footprint=[BASE, BASE], heightWorld=76, footprintWorld=[45, 45],
                   front="-Y(정면 문)", clips=clips, textures=sorted(os.listdir(OUT + "/Textures")),
                   note="걷지 않는 건물형 적. 클립은 오브젝트 애니(뼈 없음) — Egghead@<클립>.fbx의 테이크 이름이 클립 이름.", approx=True),
              open(OUT + "/Egghead.json", "w"), ensure_ascii=False, indent=1)
    print("OK", OUT)


if "--dbg" not in sys.argv:
    main()
