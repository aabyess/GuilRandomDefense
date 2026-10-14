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
        r = ramp(nt, [(0.0, (0.0, 0.01, 0.05, 1)), (0.18, (0.01, 0.04, 0.2, 1)), (0.3, (0.0, 0.02, 0.1, 1)), (0.38, (3.0, 3.2, 3.5, 1)), (0.46, (0.02, 0.08, 0.4, 1)), (0.58, (0.0, 0.02, 0.12, 1)), (0.68, (2.5, 2.8, 3.2, 1)), (0.78, (0.05, 0.2, 0.7, 1)), (1.0, (0.0, 0.01, 0.06, 1))])
    else:                   # 불꽃: 따뜻한 반사
        r = ramp(nt, [(0.0, (0.05, 0.0, 0.0, 1)), (0.3, (1.0, 0.25, 0.02, 1)), (0.5, (1.0, 0.9, 0.5, 1)), (0.7, (1.0, 0.35, 0.05, 1)), (1.0, (0.1, 0.0, 0.0, 1))])
    nt.links.new(sep.outputs["Z"], r.inputs[0])
    bg = node(nt, "ShaderNodeBackground"); bg.inputs["Strength"].default_value = 1.6; nt.links.new(r.outputs[0], bg.inputs[0])
    out = node(nt, "ShaderNodeOutputWorld"); nt.links.new(bg.outputs[0], out.inputs[0])
def chrome_mat():
    m = bpy.data.materials.new("chrome"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial")
    metal = node(nt, "ShaderNodeBsdfGlossy"); metal.inputs["Roughness"].default_value = 0.04; metal.inputs["Color"].default_value = (0.92, 0.95, 1.0, 1)
    glass = node(nt, "ShaderNodeBsdfGlass"); glass.inputs["Roughness"].default_value = 0.02; glass.inputs["IOR"].default_value = 1.45; glass.inputs["Color"].default_value = (0.7, 0.88, 1.0, 1)
    lw = node(nt, "ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.2
    mix = node(nt, "ShaderNodeMixShader"); nt.links.new(lw.outputs["Facing"], mix.inputs[0])
    nt.links.new(metal.outputs[0], mix.inputs[1]); nt.links.new(glass.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0]); return m
def flame_mat():
    m = bpy.data.materials.new("flame"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial")
    lw = node(nt, "ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.5
    col = ramp(nt, [(0.0, (1.0, 0.75, 0.12, 1)), (0.35, (1.0, 0.28, 0.02, 1)), (1.0, (0.55, 0.0, 0.0, 1))]); nt.links.new(lw.outputs["Facing"], col.inputs[0])
    em = node(nt, "ShaderNodeEmission"); em.inputs["Strength"].default_value = 1.1; nt.links.new(col.outputs[0], em.inputs[0])
    glass = node(nt, "ShaderNodeBsdfGlass"); glass.inputs["Color"].default_value = (0.95, 0.05, 0.02, 1); glass.inputs["Roughness"].default_value = 0.03
    gl = node(nt, "ShaderNodeBsdfGlossy"); gl.inputs["Roughness"].default_value = 0.08; gl.inputs["Color"].default_value = (1, 0.8, 0.6, 1)
    mix1 = node(nt, "ShaderNodeMixShader"); mix1.inputs[0].default_value = 0.65; nt.links.new(glass.outputs[0], mix1.inputs[1]); nt.links.new(em.outputs[0], mix1.inputs[2])
    mix2 = node(nt, "ShaderNodeMixShader"); mix2.inputs[0].default_value = 0.2; nt.links.new(mix1.outputs[0], mix2.inputs[1]); nt.links.new(gl.outputs[0], mix2.inputs[2])
    nt.links.new(mix2.outputs[0], out.inputs[0]); return m
def tongue_mat(color, strength, opacity):
    m = bpy.data.materials.new("tongue"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial"); em = node(nt, "ShaderNodeEmission"); em.inputs["Color"].default_value = color + (1,); em.inputs["Strength"].default_value = strength
    tr = node(nt, "ShaderNodeBsdfTransparent"); mix = node(nt, "ShaderNodeMixShader"); mix.inputs[0].default_value = opacity
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0]); return m
def flame_core_mat():
    m = bpy.data.materials.new("flamecore"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial"); em = node(nt, "ShaderNodeEmission"); em.inputs["Color"].default_value = (1.0, 0.82, 0.18, 1); em.inputs["Strength"].default_value = 3.2
    tr = node(nt, "ShaderNodeBsdfTransparent"); lw = node(nt, "ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.4
    mix = node(nt, "ShaderNodeMixShader"); nt.links.new(lw.outputs["Facing"], mix.inputs[0]); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0]); return m
def flame_tip_mat():
    m = bpy.data.materials.new("flametip"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial"); em = node(nt, "ShaderNodeEmission"); em.inputs["Color"].default_value = (1.0, 0.55, 0.05, 1); em.inputs["Strength"].default_value = 2.2
    tr = node(nt, "ShaderNodeBsdfTransparent"); mix = node(nt, "ShaderNodeMixShader"); mix.inputs[0].default_value = 0.2
    nt.links.new(em.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0]); return m
import bmesh
def catmull2(pts, per=12):
    P = [pts[0]] + list(pts) + [pts[-1]]; out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(per):
            t = k / per; t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in range(len(p1))))
    out.append(tuple(pts[-1])); return out
def extend(path, ang_turn, length, steps=6, w_end=0.0):
    """마지막 점에서 접선 방향으로 휘어 나가며 칼끝으로 가늘어지는 꼬리를 덧붙인다."""
    (x0, y0, r0), (x1, y1, r1) = path[-2], path[-1]; a = math.atan2(y1 - y0, x1 - x0); out = list(path); x, y = x1, y1
    for i in range(1, steps + 1):
        t = i / steps; a += ang_turn / steps * (0.5 + t); st = length / steps * (1.3 - t * 0.6)
        x += math.cos(a) * st; y += math.sin(a) * st; out.append((x, y, max(r1 * (1 - t) ** 1.2, w_end)))
    return out
def ribbon(pts_r, mat, width=0.55, height=0.22, per=12, name="rib", flat_z=0.0):
    """붓획 리본: 중심선(x,y)과 굵기 r(0~1.2)을 따라 가로로 넓고 세로로 얇은 단면(가운데가 볼록). 끝은 r→0 이라 칼끝."""
    pts = catmull2([(x, y, r) for x, y, r in pts_r], per); n = len(pts)
    bm = bmesh.new(); rows = []
    U = (-1.0, -0.5, 0.0, 0.5, 1.0)
    for i, (x, y, r) in enumerate(pts):
        a = pts[max(i - 1, 0)]; b = pts[min(i + 1, n - 1)]; tx, ty = b[0] - a[0], b[1] - a[1]; L = math.hypot(tx, ty) or 1.0; nx, ny = -ty / L, tx / L
        w = width * max(r, 0.0) * 0.5; row = []
        for u in U:
            z = flat_z + height * max(r, 0.0) * (1 - u * u) ** 0.8
            row.append(bm.verts.new((x + nx * w * u, y + ny * w * u, z)))
        rows.append(row)
    for i in range(n - 1):
        for j in range(len(U) - 1):
            try: bm.faces.new((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]))
            except ValueError: pass
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat)
    for p in ob.data.polygons: p.use_smooth = True
    sol = ob.modifiers.new("sol", "SOLIDIFY"); sol.thickness = 0.04; sol.offset = -1
    ss = ob.modifiers.new("ss", "SUBSURF"); ss.levels = 2; ss.render_levels = 2
    return ob

# 숫자 붓획(단위 좌표 x -1..1, y -1.2..1.2, r=굵기). 끝은 r→작게 + extend()가 칼끝 꼬리를 이어 붙인다.
D = {
 6: dict(paths=[[(0.95, 1.2, .08), (0.55, 1.18, .55), (0.0, 0.95, .95), (-0.5, 0.35, 1.25), (-0.68, -0.4, 1.3), (-0.4, -0.98, 1.2), (0.18, -1.14, 1.1), (0.68, -0.8, 1.0), (0.7, -0.25, .9), (0.35, 0.05, .8), (-0.05, 0.0, .65), (-0.25, -0.35, .5)]], tails=[(-1, 3.4, 0.55)]),
 3: dict(paths=[[(-0.75, 0.95, .1), (-0.35, 1.12, .8), (0.2, 1.18, 1.1), (0.66, 0.85, 1.15), (0.45, 0.4, 1.0), (0.0, 0.16, .8), (0.5, -0.02, 1.0), (0.8, -0.5, 1.15), (0.5, -1.0, 1.05), (-0.05, -1.16, .9), (-0.6, -0.95, .55)]], tails=[(-1, -2.4, 0.6)]),
 1: dict(paths=[[(-0.5, 0.5, .1), (-0.1, 0.95, .8), (0.15, 1.2, 1.1), (0.08, 0.3, 1.2), (0.0, -0.4, 1.15), (-0.1, -1.0, .8)]], tails=[(-1, 1.6, 0.4)]),
 2: dict(paths=[[(-0.7, 0.7, .1), (-0.3, 1.1, .85), (0.3, 1.18, 1.1), (0.72, 0.75, 1.1), (0.45, 0.2, 1.0), (-0.2, -0.45, 1.1), (-0.7, -1.0, 1.1), (0.0, -1.1, 1.0), (0.75, -1.05, .6)]], tails=[(-1, 3.0, 0.7)]),
 4: dict(paths=[[(0.35, 1.2, .1), (-0.2, 0.5, .95), (-0.7, -0.2, 1.05), (-0.1, -0.35, .95), (0.55, -0.35, .8), (0.95, -0.38, .4)], [(0.38, 1.0, .5), (0.35, 0.2, 1.1), (0.28, -0.7, 1.0), (0.15, -1.15, .3)]], tails=[(-1, 2.6, 0.5)]),
 5: dict(paths=[[(0.8, 1.12, .1), (0.2, 1.12, .8), (-0.35, 1.1, 1.0), (-0.45, 0.45, 1.05), (-0.38, 0.12, .95), (0.2, 0.3, 1.05), (0.7, 0.0, 1.15), (0.72, -0.55, 1.05), (0.3, -1.05, 1.0), (-0.3, -1.1, .8)]], tails=[(-1, -2.0, 0.6)]),
 7: dict(paths=[[(-0.8, 1.0, .1), (-0.1, 1.12, .9), (0.7, 1.12, 1.1), (0.45, 0.2, 1.1), (0.1, -0.5, .9), (-0.15, -1.0, .5)]], tails=[(-1, 1.4, 0.5)]),
 8: dict(paths=[[(0.3, 0.1, .9), (-0.35, 0.5, 1.05), (-0.3, 1.0, 1.15), (0.2, 1.15, 1.1), (0.55, 0.8, 1.0), (0.2, 0.3, 1.0), (-0.35, -0.2, 1.1), (-0.65, -0.7, 1.15), (-0.25, -1.1, 1.05), (0.35, -1.1, 1.0), (0.7, -0.65, 1.0), (0.4, -0.15, .8), (-0.1, 0.15, .5)]], tails=[(-1, 3.0, 0.55)]),
 9: dict(paths=[[(0.45, -1.15, .1), (0.7, -0.5, .95), (0.62, 0.35, 1.1), (0.3, 0.95, 1.15), (-0.3, 1.1, 1.05), (-0.7, 0.7, 1.05), (-0.5, 0.15, .95), (-0.05, 0.0, .85), (0.4, 0.2, .6)]], tails=[(-1, 2.6, 0.5)]),
}
STYLE = {d: ("flame" if d % 2 else "chrome") for d in range(1, 10)}
def build(digit):
    sc = reset(); style = STYLE[digit]; world_env(sc, style)
    mat = chrome_mat() if style == "chrome" else flame_mat(); core = flame_core_mat(); tip = flame_tip_mat()
    objs = []; rng = random.Random(digit * 11); allpts = []
    for path in D[digit]["paths"]:
        pth = list(path)
        for (_, turn, ln) in D[digit]["tails"]: pth = extend(pth, turn, ln)
        objs.append(ribbon(pth, mat, width=0.78, height=0.3))
        if style == "flame":
            objs.append(ribbon([(x, y, r * 0.42) for x, y, r in pth], core, width=0.78, height=0.36, flat_z=0.05))
        allpts += catmull2([(x, y, r) for x, y, r in pth], 4)
    if style == "flame":   # 위·오른쪽으로 한 방향 휩쓸려 날리는 얇고 뾰족한 불꽃 혀: 빨강(큰·반투명) → 주황 → 노랑(가는 심)
        mats = [tongue_mat((1.0, 0.1, 0.02), 1.6, 0.42), tongue_mat((1.0, 0.42, 0.04), 2.2, 0.5), tongue_mat((1.0, 0.85, 0.25), 3.0, 0.62)]
        widths = [0.6, 0.42, 0.24]
        sel = [p for p in allpts if p[2] > 0.45]
        for k in range(30):
            x, y, r = rng.choice(sel); lay = k % 3
            base = math.radians(58 + rng.uniform(-14, 14)); ln = rng.uniform(0.7, 1.7) * (0.55 + 0.45 * r) * (1.0 - 0.18 * lay); turn = rng.uniform(0.55, 0.95)
            pts = [(x, y, 0.5 * r)]; a = base; px, py = x, y
            for i in range(1, 8):
                t = i / 7; a += turn / 7; px += math.cos(a) * ln / 7; py += math.sin(a) * ln / 7; pts.append((px, py, (0.95 * (1 - t) ** 1.25) * (0.55 + 0.45 * r) + 0.01))
            objs.append(ribbon(pts, mats[lay], width=widths[lay], height=0.04, per=6, flat_z=0.14 + 0.06 * lay + rng.uniform(0, 0.1)))
    root = bpy.data.objects.new("root", None); bpy.context.collection.objects.link(root)
    for o in objs: o.parent = root
    root.rotation_euler = (math.radians(8), math.radians(-6), math.radians(-9))
    cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.ortho_scale = 4.4
    co = bpy.data.objects.new("c", cam); bpy.context.collection.objects.link(co); sc.camera = co; co.location = (0, 0.1, 8)
    for loc, size, energy in (((-3, 3, 5), 3.0, 700), ((3.2, -2.5, 4), 2.0, 350)):
        li = bpy.data.lights.new("a", "AREA"); li.size = size; li.energy = energy; lo = bpy.data.objects.new("a", li); bpy.context.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (math.radians(30), math.radians(20), 0)
    sc.render.filepath = f"{OUT}/symbol_{digit}.png"; bpy.ops.render.render(write_still=True)
for dgt in DIGITS: build(dgt)
