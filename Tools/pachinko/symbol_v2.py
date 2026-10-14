"""파칭코 숫자 v2(10-14, blender): 실제 파칭코(e도쿄구울) 숫자 느낌 — 홀수=불꽃 감긴 붓글씨 숫자, 짝수=은빛 크롬/유리 액체 금속. 검은 외곽선 없음.
  blender -b --factory-startup --python Tools/pachinko/symbol_v2.py -- <출력폴더> <숫자들 예 3,6> [samples=64] [size=1024]
폰트: Kaushan Script(OFL 1.1, Google Fonts) — <출력폴더>/../_fonts2/Kaushan.ttf. 꼬리·불꽃은 코드로 만든 곡선."""
import bpy, sys, os, math, random, mathutils
argv = sys.argv[sys.argv.index("--") + 1:]
OUT = os.path.expanduser(argv[0]); DIGITS = [int(x) for x in argv[1].split(",")]
SAMPLES = int(argv[2]) if len(argv) > 2 else 64; SIZE = int(argv[3]) if len(argv) > 3 else 1024
FONT = os.path.expanduser("~/GRD_pachinko_art/_fonts2/Kaushan.ttf"); os.makedirs(OUT, exist_ok=True)
def node(nt, t, **kw):
    n = nt.nodes.new(t)
    for k, v in kw.items(): setattr(n, k, v)
    return n
def ramp(nt, stops):
    r = nt.nodes.new("ShaderNodeValToRGB"); el = r.color_ramp.elements
    while len(el) > len(stops): el.remove(el[-1])
    while len(el) < len(stops): el.new(0.5)
    for e, (p, c) in zip(el, stops): e.position = p; e.color = c
    return r
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene; sc.render.engine = "CYCLES"; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    try: sc.cycles.device = "CPU"
    except Exception: pass
    sc.render.resolution_x = sc.render.resolution_y = SIZE; sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.cycles.film_exposure = 1.0
    sc.view_settings.view_transform = "Standard"
    return sc
def world_env(sc, style):
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; nt = w.node_tree; nt.nodes.clear()
    tc = node(nt, "ShaderNodeTexCoord"); sep = node(nt, "ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    mr = node(nt, "ShaderNodeMapRange"); mr.inputs["From Min"].default_value = 0.0; mr.inputs["From Max"].default_value = 1.0
    nt.links.new(sep.outputs["Z"], mr.inputs["Value"])
    if style == "chrome":   # 푸른 하늘 띠 + 흰 소프트박스 + 어두운 바닥 띠 → 액체 금속 반사
        r = ramp(nt, [(0.0, (0.01, 0.02, 0.06, 1)), (0.25, (0.05, 0.25, 0.8, 1)), (0.38, (0.9, 0.95, 1, 1)), (0.5, (0.1, 0.35, 1.0, 1)), (0.62, (1, 1, 1, 1)), (0.8, (0.25, 0.55, 1, 1)), (1.0, (0.02, 0.05, 0.2, 1))])
    else:                   # 불꽃: 따뜻한 반사
        r = ramp(nt, [(0.0, (0.05, 0.0, 0.0, 1)), (0.3, (1.0, 0.25, 0.02, 1)), (0.5, (1.0, 0.9, 0.5, 1)), (0.7, (1.0, 0.35, 0.05, 1)), (1.0, (0.1, 0.0, 0.0, 1))])
    nt.links.new(sep.outputs["Z"], r.inputs[0])
    bg = node(nt, "ShaderNodeBackground"); bg.inputs["Strength"].default_value = 1.6; nt.links.new(r.outputs[0], bg.inputs[0])
    out = node(nt, "ShaderNodeOutputWorld"); nt.links.new(bg.outputs[0], out.inputs[0])
def chrome_mat():
    m = bpy.data.materials.new("chrome"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial")
    metal = node(nt, "ShaderNodeBsdfGlossy"); metal.inputs["Roughness"].default_value = 0.04; metal.inputs["Color"].default_value = (0.82, 0.9, 1.0, 1)
    glass = node(nt, "ShaderNodeBsdfGlass"); glass.inputs["Roughness"].default_value = 0.02; glass.inputs["IOR"].default_value = 1.45; glass.inputs["Color"].default_value = (0.7, 0.88, 1.0, 1)
    lw = node(nt, "ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.35
    mix = node(nt, "ShaderNodeMixShader"); nt.links.new(lw.outputs["Facing"], mix.inputs[0])   # 가장자리=금속, 안쪽=유리
    nt.links.new(metal.outputs[0], mix.inputs[1]); nt.links.new(glass.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0]); return m
def flame_mat():
    m = bpy.data.materials.new("flame"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial")
    lw = node(nt, "ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.5
    col = ramp(nt, [(0.0, (1.0, 0.75, 0.12, 1)), (0.35, (1.0, 0.28, 0.02, 1)), (1.0, (0.55, 0.0, 0.0, 1))]); nt.links.new(lw.outputs["Facing"], col.inputs[0])
    em = node(nt, "ShaderNodeEmission"); em.inputs["Strength"].default_value = 1.1; nt.links.new(col.outputs[0], em.inputs[0])
    glass = node(nt, "ShaderNodeBsdfGlass"); glass.inputs["Color"].default_value = (1.0, 0.12, 0.05, 1); glass.inputs["Roughness"].default_value = 0.03
    gl = node(nt, "ShaderNodeBsdfGlossy"); gl.inputs["Roughness"].default_value = 0.08; gl.inputs["Color"].default_value = (1, 0.8, 0.6, 1)
    mix1 = node(nt, "ShaderNodeMixShader"); mix1.inputs[0].default_value = 0.65; nt.links.new(glass.outputs[0], mix1.inputs[1]); nt.links.new(em.outputs[0], mix1.inputs[2])
    mix2 = node(nt, "ShaderNodeMixShader"); mix2.inputs[0].default_value = 0.2; nt.links.new(mix1.outputs[0], mix2.inputs[1]); nt.links.new(gl.outputs[0], mix2.inputs[2])
    nt.links.new(mix2.outputs[0], out.inputs[0]); return m
def flame_tip_mat():
    m = bpy.data.materials.new("flametip"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial"); em = node(nt, "ShaderNodeEmission"); em.inputs["Color"].default_value = (1.0, 0.55, 0.05, 1); em.inputs["Strength"].default_value = 2.2
    tr = node(nt, "ShaderNodeBsdfTransparent"); mix = node(nt, "ShaderNodeMixShader"); mix.inputs[0].default_value = 0.2
    nt.links.new(em.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0]); return m
def catmull(pts, per=14):
    P = [pts[0]] + list(pts) + [pts[-1]]; out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(per):
            t = k / per; t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(len(p1))))
    out.append(tuple(pts[-1])); return out
def stroke(pts_r, mat, bevel=0.3, per=14):
    """pts_r=[(x,y,r)…] 굵기 r 가변 붓획 → 둥근 단면 튜브. 점은 Catmull-Rom으로 매끈하게."""
    pts = catmull([(x, y, 0.0, r) for x, y, r in pts_r], per)
    cu = bpy.data.curves.new("s", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = bevel; cu.bevel_resolution = 8; cu.use_fill_caps = True
    sp = cu.splines.new("POLY"); sp.points.add(len(pts) - 1)
    for pt, (x, y, z, r) in zip(sp.points, pts): pt.co = (x, y, z, 1); pt.radius = max(r, 0.02)
    ob = bpy.data.objects.new("s", cu); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat); return ob
def swirl(x, y, a0, r0, turns, rad, steps=12):
    pts = []
    for i in range(steps + 1):
        t = i / steps; a = a0 + turns * 2 * math.pi * t; r = r0 * (1 - 0.85 * t); pts.append((x + math.cos(a) * r, y + math.sin(a) * r, rad * (1 - t) + 0.06))
    return pts
D = {
 6: [[(0.85, 1.12, .55), (0.45, 1.12, .9), (-0.12, 0.8, 1.05), (-0.58, 0.15, 1.1), (-0.62, -0.5, 1.15), (-0.25, -1.0, 1.1), (0.3, -1.1, 1.05), (0.72, -0.72, 1.0), (0.72, -0.2, .95), (0.4, 0.12, .9), (-0.02, 0.06, .85), (-0.27, -0.3, .75), (-0.1, -0.58, .6), (0.15, -0.5, .45), (0.2, -0.3, .25)]],
 3: [[(-0.7, 0.85, .45), (-0.3, 1.1, .85), (0.25, 1.15, 1.05), (0.68, 0.82, 1.0), (0.5, 0.38, .95), (0.05, 0.14, .85), (0.55, -0.05, .95), (0.82, -0.5, 1.05), (0.5, -0.98, 1.0), (-0.05, -1.14, .9), (-0.62, -0.9, .55), (-0.85, -0.55, .2)]],
 1: [[(-0.45, 0.55, .5), (-0.05, 0.95, .9), (0.15, 1.18, 1.0), (0.08, 0.3, 1.1), (0.0, -0.4, 1.1), (-0.12, -1.0, .8), (-0.35, -1.18, .3)], [(-0.55, -1.12, .55), (0.0, -1.12, .9), (0.55, -1.12, .55)]],
 2: [[(-0.65, 0.7, .45), (-0.3, 1.1, .9), (0.3, 1.15, 1.05), (0.7, 0.75, 1.0), (0.45, 0.2, .95), (-0.2, -0.45, 1.0), (-0.7, -1.0, 1.0), (0.0, -1.1, 1.0), (0.75, -1.05, .7), (1.0, -0.8, .35)]],
 4: [[(0.3, 1.18, .5), (-0.2, 0.5, .95), (-0.7, -0.2, 1.0), (-0.1, -0.35, .95), (0.55, -0.35, .9), (0.95, -0.38, .5)], [(0.35, 1.0, .8), (0.35, 0.2, 1.05), (0.28, -0.7, 1.05), (0.15, -1.15, .5)]],
 5: [[(0.75, 1.1, .5), (0.2, 1.12, .9), (-0.35, 1.1, 1.0), (-0.45, 0.45, 1.0), (-0.38, 0.12, .95), (0.2, 0.3, 1.0), (0.7, 0.0, 1.05), (0.72, -0.55, 1.0), (0.3, -1.05, 1.0), (-0.3, -1.1, .85), (-0.75, -0.8, .45)]],
 7: [[(-0.75, 1.0, .55), (-0.1, 1.12, .95), (0.7, 1.1, 1.05), (0.45, 0.2, 1.0), (0.1, -0.5, .95), (-0.15, -1.0, .75), (-0.3, -1.2, .25)]],
 8: [[(0.25, 0.1, .9), (-0.35, 0.5, 1.0), (-0.3, 1.0, 1.05), (0.2, 1.15, 1.0), (0.55, 0.8, .95), (0.2, 0.3, .95), (-0.35, -0.2, 1.0), (-0.65, -0.7, 1.05), (-0.25, -1.1, 1.0), (0.35, -1.1, 1.0), (0.7, -0.65, 1.0), (0.4, -0.15, .95), (-0.1, 0.15, .85), (-0.35, 0.55, .6)]],
 9: [[(0.4, -1.1, .5), (0.7, -0.5, .95), (0.62, 0.35, 1.0), (0.3, 0.95, 1.05), (-0.3, 1.1, 1.0), (-0.7, 0.7, 1.0), (-0.5, 0.15, .95), (-0.05, 0.0, .9), (0.4, 0.2, .75), (0.5, 0.35, .5)]],
}
STYLE = {d: ("flame" if d % 2 else "chrome") for d in range(1, 10)}
TAILS = {6: [(0.85, 1.12, 0.0)], 2: [(1.0, -0.8, 3.4)], 8: [(-0.35, 0.55, 1.2)], 4: [(0.95, -0.38, 0.0)], 9: [(0.5, 0.35, 2.4)]}
def build(digit):
    sc = reset(); style = STYLE[digit]; world_env(sc, style)
    mat = chrome_mat() if style == "chrome" else flame_mat(); tip = flame_tip_mat()
    objs = []; rng = random.Random(digit * 7)
    for path in D[digit]: objs.append(stroke(path, mat, bevel=0.27))
    if style == "chrome":
        for (x, y, a0) in TAILS.get(digit, []): objs.append(stroke(swirl(x, y, a0, 0.5, 1.2, 0.7), mat, bevel=0.22, per=8))
    else:
        path = D[digit][0]
        for idx in range(1, len(path) - 1):
            x, y, r = path[idx]; ang = math.pi / 2 + rng.uniform(-0.9, 0.9); ln = rng.uniform(0.45, 0.95); pts = []
            for i in range(7):
                t = i / 6; pts.append((x + math.cos(ang) * ln * t * 0.9, y + math.sin(ang) * ln * t * 0.9, 0.9 * (1 - t) ** 1.3 + 0.05)); ang += 0.14 * rng.choice((-1, 1)) + 0.18
            objs.append(stroke(pts, mat, bevel=0.2, per=8)); objs.append(stroke([(a, b, c * 0.6) for a, b, c in pts], tip, bevel=0.13, per=8))
    root = bpy.data.objects.new("root", None); bpy.context.collection.objects.link(root)
    for o in objs: o.parent = root
    root.rotation_euler = (math.radians(10), math.radians(-5), math.radians(-7))
    cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.ortho_scale = 4.3
    co = bpy.data.objects.new("c", cam); bpy.context.collection.objects.link(co); sc.camera = co; co.location = (0, 0, 8)
    for loc, size, energy in (((-3, 3, 5), 3.0, 800), ((3.2, -2.5, 4), 2.0, 450)):
        li = bpy.data.lights.new("a", "AREA"); li.size = size; li.energy = energy; lo = bpy.data.objects.new("a", li); bpy.context.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (math.radians(30), math.radians(20), 0)
    sc.render.filepath = f"{OUT}/symbol_{digit}.png"; bpy.ops.render.render(write_still=True)
for dgt in DIGITS: build(dgt)
