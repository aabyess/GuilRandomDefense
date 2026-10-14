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
    uvl = bm.loops.layers.uv.new("UVMap"); vuv = {}
    for i, row in enumerate(rows):
        for j, v in enumerate(row): vuv[v] = (j / (len(U) - 1), i / (n - 1))
    for f in bm.faces:
        for lp in f.loops: lp[uvl].uv = vuv[lp.vert]
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat)
    for p in ob.data.polygons: p.use_smooth = True
    sol = ob.modifiers.new("sol", "SOLIDIFY"); sol.thickness = 0.04; sol.offset = -1
    ss = ob.modifiers.new("ss", "SUBSURF"); ss.levels = 2; ss.render_levels = 2
    return ob

# ── 3차: 겹쳐진 큰 붓 스우시 3~5장 + 획 방향 줄무늬 결(UV 노이즈) ──
def streak_mat(kind, seed, shift=0.0):
    """kind: flame(빨강→주황→노랑 결, 불사조 깃털) / chrome(은·하늘·연보라·흰 반투명 유리 결). U=폭 방향, V=획 방향."""
    m = bpy.data.materials.new(f"{kind}{seed}"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial"); bsdf = node(nt, "ShaderNodeBsdfPrincipled")
    uv = node(nt, "ShaderNodeUVMap"); mp = node(nt, "ShaderNodeMapping"); mp.inputs["Scale"].default_value = (30.0, 1.2, 1.0); mp.inputs["Location"].default_value = (seed * 3.1, seed * 1.7, 0)
    nz = node(nt, "ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 1.0; nz.inputs["Detail"].default_value = 3.0; nz.inputs["Roughness"].default_value = 0.55
    nt.links.new(uv.outputs[0], mp.inputs[0]); nt.links.new(mp.outputs[0], nz.inputs["Vector"])
    sep = node(nt, "ShaderNodeSeparateXYZ"); uv2 = node(nt, "ShaderNodeUVMap"); nt.links.new(uv2.outputs[0], sep.inputs[0])
    # 가로 위치: 가운데(0.5)=1, 가장자리=0 (삼각파) → 노이즈와 섞어 결 위에 중심 밝음
    ab = node(nt, "ShaderNodeMath"); ab.operation = "SUBTRACT"; nt.links.new(sep.outputs["X"], ab.inputs[0]); ab.inputs[1].default_value = 0.5
    ab2 = node(nt, "ShaderNodeMath"); ab2.operation = "ABSOLUTE"; nt.links.new(ab.outputs[0], ab2.inputs[0])
    inv = node(nt, "ShaderNodeMath"); inv.operation = "MULTIPLY_ADD"; nt.links.new(ab2.outputs[0], inv.inputs[0]); inv.inputs[1].default_value = -2.0; inv.inputs[2].default_value = 1.0
    mixf = node(nt, "ShaderNodeMix"); mixf.data_type = "FLOAT"; mixf.inputs[0].default_value = 0.5
    nt.links.new(nz.outputs["Fac"], mixf.inputs[2]); nt.links.new(inv.outputs[0], mixf.inputs[3])
    sh = node(nt, "ShaderNodeMath"); sh.operation = "ADD"; sh.inputs[1].default_value = shift; nt.links.new(mixf.outputs[0], sh.inputs[0])
    if kind == "flame":   # 바깥 테두리만 진홍, 획 절반 이상은 주황→노랑 결
        st = [(0.0, (0.55, 0.0, 0.04, 1)), (0.18, (0.9, 0.04, 0.03, 1)), (0.34, (1.0, 0.28, 0.02, 1)), (0.55, (1.0, 0.52, 0.04, 1)), (0.75, (1.0, 0.78, 0.12, 1)), (0.9, (1.0, 0.94, 0.4, 1)), (1.0, (1.0, 1.0, 0.85, 1))]
    elif kind == "purple":
        st = [(0.0, (0.1, 0.02, 0.28, 1)), (0.3, (0.45, 0.2, 0.85, 1)), (0.5, (0.95, 0.9, 1.0, 1)), (0.72, (0.8, 0.45, 0.98, 1)), (1.0, (0.55, 0.85, 1.0, 1))]
    elif kind == "gold":
        st = [(0.0, (0.6, 0.02, 0.0, 1)), (0.25, (1.0, 0.1, 0.02, 1)), (0.5, (1.0, 0.55, 0.05, 1)), (0.72, (1.0, 0.82, 0.12, 1)), (1.0, (1.0, 0.99, 0.75, 1))]
    else:   # 청록·하늘 진하게 + 은 하이라이트 얇은 선
        st = [(0.0, (0.0, 0.04, 0.3, 1)), (0.22, (0.0, 0.35, 0.85, 1)), (0.4, (0.0, 0.78, 0.82, 1)), (0.55, (0.3, 0.8, 1.0, 1)), (0.7, (0.55, 0.45, 0.95, 1)), (0.88, (0.9, 0.97, 1.0, 1)), (1.0, (1.0, 1.0, 1.0, 1))]
    cr = ramp(nt, st); nt.links.new(sh.outputs[0], cr.inputs[0])
    lwr = node(nt, "ShaderNodeLayerWeight"); lwr.inputs["Blend"].default_value = 0.5
    rim = node(nt, "ShaderNodeMath"); rim.operation = "MULTIPLY_ADD"; nt.links.new(lwr.outputs["Facing"], rim.inputs[0]); rim.inputs[1].default_value = 3.0; rim.inputs[2].default_value = -2.0
    rimc = node(nt, "ShaderNodeClamp"); nt.links.new(rim.outputs[0], rimc.inputs[0])
    mixc = node(nt, "ShaderNodeMix"); mixc.data_type = "RGBA"; rimk = node(nt, "ShaderNodeMath"); rimk.operation = "MULTIPLY"; rimk.inputs[1].default_value = 0.25 if kind in ("flame", "gold") else 0.9; nt.links.new(rimc.outputs[0], rimk.inputs[0]); nt.links.new(rimk.outputs[0], mixc.inputs[0]); nt.links.new(cr.outputs[0], mixc.inputs[6]); mixc.inputs[7].default_value = (1, 1, 1, 1)
    nt.links.new(mixc.outputs[2], bsdf.inputs["Base Color"])
    if kind in ("flame", "gold"):
        bsdf.inputs["Roughness"].default_value = 0.22; bsdf.inputs["Metallic"].default_value = 0.0
        nt.links.new(mixc.outputs[2], bsdf.inputs["Emission Color"]); bsdf.inputs["Emission Strength"].default_value = 0.9 if kind == "flame" else 0.5
        try: bsdf.inputs["Coat Weight"].default_value = 0.6
        except Exception: pass
    else:
        bsdf.inputs["Metallic"].default_value = 0.85; bsdf.inputs["Roughness"].default_value = 0.1
        try: bsdf.inputs["Coat Weight"].default_value = 0.4
        except Exception: pass
    # 반투명(유리 느낌): 가장자리·결 사이로 비침
    tr = node(nt, "ShaderNodeBsdfTransparent"); mix = node(nt, "ShaderNodeMixShader")
    op = node(nt, "ShaderNodeMath"); op.operation = "MULTIPLY_ADD"; nt.links.new(sh.outputs[0], op.inputs[0]); op.inputs[1].default_value = 0.3; op.inputs[2].default_value = 0.52 if kind in ("chrome", "purple") else 0.62
    clamp = node(nt, "ShaderNodeClamp"); clamp.inputs[2].default_value = 0.9; nt.links.new(op.outputs[0], clamp.inputs[0])
    nt.links.new(clamp.outputs[0], mix.inputs[0]); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(bsdf.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
    return m
def swoosh(ctrl, mat, width=0.6, lift=0.0, asym=0.35, per=14, height=0.12):
    """ctrl=[(x,y)…] 중심선. 굵기 = 초승달꼴: 시작쪽이 굵고 양 끝은 칼끝."""
    pts = catmull2([(x, y, 0.0) for x, y in ctrl], per); n = len(pts); out = []
    for i, (x, y, _) in enumerate(pts):
        t = i / (n - 1); prof = (math.sin(math.pi * t ** (1 - asym)) ** 0.9) ; out.append((x, y, prof * 1.0))
    return ribbon(out, mat, width=width * 0.98, height=height, per=1, flat_z=lift)
SW = {  # (중심선, 폭, 높이띄움, 스트리크 shift, seed) — 겹쳐서 숫자 꼴. 아래·끝 꼬리는 크게 말려 날린다
 1: [([(-0.7, 0.45), (-0.25, 0.95), (0.25, 1.32), (0.7, 1.2)], 0.58, 0.00, 0.0, 1),
     ([(0.28, 1.2), (0.16, 0.4), (0.04, -0.4), (-0.2, -1.05), (-0.65, -1.45)], 0.62, 0.04, 0.0, 2),
     ([(0.42, 0.95), (0.34, 0.2), (0.22, -0.55)], 0.3, 0.10, 0.2, 3),
     ([(-0.25, -1.0), (0.35, -1.25), (0.95, -1.05), (1.2, -0.55)], 0.4, 0.08, 0.1, 4),
     ([(0.5, 1.2), (0.95, 1.38), (1.2, 1.15)], 0.28, 0.14, 0.28, 5)],
 2: [([(-0.8, 0.6), (-0.4, 1.15), (0.3, 1.28), (0.82, 0.88), (0.6, 0.3)], 0.6, 0.00, 0.0, 1),
     ([(0.65, 0.4), (0.05, -0.35), (-0.55, -0.9), (-0.95, -1.2)], 0.62, 0.04, 0.0, 2),
     ([(-0.85, -1.05), (0.0, -1.18), (0.85, -1.05), (1.25, -0.6), (1.15, -0.2)], 0.5, 0.08, 0.1, 3),
     ([(0.2, 1.08), (0.78, 0.95), (0.9, 0.5)], 0.3, 0.14, 0.25, 4)],
 3: [([(-1.0, 0.68), (-0.5, 1.22), (0.3, 1.36), (0.92, 1.1), (1.05, 0.7), (0.5, 0.2), (0.1, 0.12)], 0.7, 0.00, 0.0, 1),
     ([(0.05, 0.22), (0.74, 0.0), (0.95, -0.5), (0.62, -1.04), (-0.15, -1.24), (-0.95, -1.16), (-1.5, -0.9), (-1.75, -0.45)], 0.66, 0.05, 0.05, 2),
     ([(0.7, 1.0), (1.05, 1.38), (1.5, 1.55), (1.75, 1.2)], 0.5, 0.12, 0.15, 3),
     ([(0.15, -0.25), (0.62, -0.42), (0.6, -0.86), (0.1, -1.08)], 0.3, 0.14, 0.25, 4),
     ([(0.3, -1.12), (-0.2, -1.4), (-0.95, -1.48), (-1.55, -1.25)], 0.16, 0.12, 0.35, 6)],
 4: [([(0.4, 1.35), (-0.3, 0.6), (-0.85, -0.12), (-0.5, -0.3)], 0.55, 0.00, 0.0, 1),
     ([(-0.85, -0.28), (0.0, -0.42), (0.75, -0.42), (1.2, -0.1), (1.1, 0.25)], 0.5, 0.05, 0.1, 2),
     ([(0.45, 1.15), (0.4, 0.3), (0.3, -0.6), (0.1, -1.3), (-0.3, -1.5)], 0.6, 0.02, 0.0, 3),
     ([(0.1, 0.7), (-0.25, 0.15)], 0.26, 0.12, 0.25, 4)],
 5: [([(0.95, 1.2), (0.25, 1.17), (-0.45, 1.1)], 0.46, 0.00, 0.0, 1),
     ([(-0.4, 1.12), (-0.52, 0.5), (-0.38, 0.1)], 0.5, 0.04, 0.0, 2),
     ([(-0.4, 0.28), (0.3, 0.42), (0.85, 0.0), (0.8, -0.62), (0.2, -1.12), (-0.55, -1.12), (-1.0, -0.72), (-1.1, -0.3)], 0.62, 0.06, 0.0, 3),
     ([(0.1, 0.15), (0.62, 0.05), (0.68, -0.42)], 0.3, 0.14, 0.25, 4)],
 6: [([(1.5, 1.7), (0.8, 1.2), (0.2, 0.8), (-0.4, 0.35), (-0.7, -0.25), (-0.52, -0.95), (0.1, -1.22), (0.72, -0.92)], 0.78, 0.00, 0.0, 1),
     ([(0.9, 1.0), (1.45, 1.1), (1.9, 0.8)], 0.14, 0.2, 0.2, 5),
     ([(-0.5, -0.75), (-0.05, -1.14), (0.64, -0.96), (0.82, -0.3), (0.42, 0.12), (-0.12, 0.02), (-0.32, -0.42), (-0.05, -0.62), (0.18, -0.4)], 0.66, 0.06, 0.0, 2),
     ([(0.2, 1.02), (0.62, 1.32), (1.05, 1.32), (1.2, 1.08)], 0.26, 0.14, 0.2, 3),
     ([(-0.64, 0.1), (-0.8, -0.62), (-0.42, -1.18), (0.25, -1.38), (0.85, -1.2)], 0.3, 0.12, 0.25, 4)],
 7: [([(-0.95, 1.12), (-0.2, 1.22), (0.65, 1.24), (1.05, 1.08)], 0.5, 0.00, 0.0, 1),
     ([(0.9, 1.15), (0.45, 0.2), (0.08, -0.62), (-0.28, -1.2), (-0.7, -1.5)], 0.62, 0.04, 0.0, 2),
     ([(0.58, 1.3), (0.98, 1.45), (1.25, 1.2)], 0.32, 0.12, 0.2, 3),
     ([(0.6, 0.9), (0.25, 0.1), (-0.05, -0.6)], 0.32, 0.10, 0.28, 4),
     ([(-0.4, 1.02), (0.3, 1.1), (0.85, 0.95)], 0.26, 0.16, 0.32, 5)],
 8: [([(0.3, 0.15), (-0.42, 0.5), (-0.42, 1.08), (0.15, 1.28), (0.68, 0.98), (0.32, 0.36), (-0.35, -0.25)], 0.52, 0.00, 0.0, 1),
     ([(-0.38, -0.2), (-0.85, -0.65), (-0.48, -1.18), (0.25, -1.24), (0.85, -0.78), (0.38, -0.14), (-0.2, 0.14)], 0.56, 0.05, 0.0, 2),
     ([(0.5, 1.18), (0.95, 1.08), (1.05, 0.62)], 0.26, 0.12, 0.25, 3),
     ([(0.42, -1.12), (0.95, -1.25), (1.3, -0.92), (1.2, -0.5)], 0.34, 0.1, 0.1, 4)],
 9: [([(0.6, -0.95), (0.85, -0.2), (0.65, 0.72), (0.0, 1.22), (-0.68, 0.92), (-0.62, 0.2), (0.0, -0.06), (0.52, 0.26)], 0.56, 0.00, 0.0, 1),
     ([(0.72, 0.55), (0.66, -0.4), (0.4, -1.02), (-0.15, -1.32), (-0.8, -1.25), (-1.15, -0.9)], 0.58, 0.05, 0.0, 2),
     ([(-0.3, 1.12), (0.3, 1.25), (0.78, 0.82)], 0.28, 0.12, 0.25, 3),
     ([(0.1, -1.2), (0.72, -1.42), (1.15, -1.18)], 0.3, 0.1, 0.1, 4)],
}
STYLE = {d: ("flame" if d % 2 else "chrome") for d in range(1, 10)}; STYLE[7] = "gold"; STYLE[9] = "purple"
def build(digit):
    sc = reset(); style = STYLE[digit]; world_env(sc, "chrome" if style in ("chrome", "purple") else "flame")
    objs = []
    for ctrl, w, lift, shift, seed in SW[digit]:
        objs.append(swoosh(ctrl, streak_mat(style, seed + digit * 10, shift - (0.2 if style == 'flame' else 0.0)), width=w, lift=lift))
    root = bpy.data.objects.new("root", None); bpy.context.collection.objects.link(root)
    for o in objs: o.parent = root
    root.rotation_euler = (math.radians(6), math.radians(-5), math.radians(-8))
    cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.ortho_scale = 4.8
    co = bpy.data.objects.new("c", cam); bpy.context.collection.objects.link(co); sc.camera = co; co.location = (0.05, 0.05, 8)
    for loc, size, energy in (((-3, 3, 5), 3.0, 500), ((3.2, -2.5, 4), 2.0, 250)):
        li = bpy.data.lights.new("a", "AREA"); li.size = size; li.energy = energy; lo = bpy.data.objects.new("a", li); bpy.context.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (math.radians(30), math.radians(20), 0)
    sc.render.filepath = f"{OUT}/symbol_{digit}.png"; bpy.ops.render.render(write_still=True)
for dgt in DIGITS: build(dgt)
