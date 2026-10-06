"""선술집 문 v3 — v1 구도(door_left/right 각 1920×2160 정면, 투명 배경, 문짝이 프레임을 꽉 채움) 그대로 **문짝 디테일만** 높임(blender 세션 2026-10-06, 사장님·PM 「나무 문 자체의 디테일」).
PBR 원목(결·옹이·널판 사이 틈·닳은 모서리·손때 얼룩) · 녹슨 쇠띠·리벳·경첩·고리(금속 반사·녹줄기) · 못 자국·미세한 흠집 · 널판 틈과 안쪽 가장자리로만 새는 따뜻한 빛. 외벽·문틀·거리 없음.
blender -b --factory-startup --python Tools/blender/gen_tavern_door_v3.py -- ~/GRD_tavern_door_v3 [가로 1920] [세로 2160] [샘플 128]
산출 door_left.png · door_right.png (+ _45 · _75 안쪽으로 젖힌 각도) · door_gap_light.png(v1과 같은 파일, 있으면 복사)."""
import bpy, bmesh, math, os, sys, random
import numpy as np
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if len(argv) > 0 else '~/GRD_tavern_door_v3'); RX = int(argv[1]) if len(argv) > 1 else 1920; RY = int(argv[2]) if len(argv) > 2 else 2160; SAMPLES = int(argv[3]) if len(argv) > 3 else 128
os.makedirs(OUT, exist_ok=True); random.seed(5); rs = np.random.RandomState(5)
bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
W, H = 1.0, 1.125; TAU = math.tau
def nd(t, typ, **kw):
    n = t.nodes.new(typ)
    for k, v in kw.items():
        try: setattr(n, k, v)
        except Exception: pass
    return n
def lk(t, a, b): t.links.new(a, b)
def newmat(name):
    m = bpy.data.materials.new(name); m.use_nodes = True; t = m.node_tree
    for n in list(t.nodes): t.nodes.remove(n)
    o = nd(t, 'ShaderNodeOutputMaterial'); b = nd(t, 'ShaderNodeBsdfPrincipled'); lk(t, b.outputs[0], o.inputs[0]); return m, t, b
def MA(t, op, a, b=None, c=None, clamp=False):
    n = nd(t, 'ShaderNodeMath', operation=op, use_clamp=clamp)
    for i, v in enumerate((a, b, c)):
        if v is None: continue
        if isinstance(v, (int, float)): n.inputs[i].default_value = v
        else: lk(t, v, n.inputs[i])
    return n.outputs[0]
def ramp(t, fac, stops):
    r = nd(t, 'ShaderNodeValToRGB'); cr = r.color_ramp
    while len(cr.elements) < len(stops): cr.elements.new(.5)
    for e, (p, c) in zip(cr.elements, stops): e.position = p; e.color = c
    lk(t, fac, r.inputs[0]); return r.outputs[0]

GRIME = {'p': None}
def wood_mat(seed, worn=True, grime=None):
    m, t, b = newmat(f'원목{seed}')
    tc = nd(t, 'ShaderNodeTexCoord'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (11, 1.4, 1.1); mp.inputs['Location'].default_value = (seed * 3.7, seed * 1.3, 0)
    lk(t, tc.outputs['Object'], mp.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 7; nz.inputs['Detail'].default_value = 9; nz.inputs['Roughness'].default_value = .62; lk(t, mp.outputs[0], nz.inputs[0])
    wv = nd(t, 'ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.bands_direction = 'X'; wv.inputs['Scale'].default_value = 4.5; wv.inputs['Distortion'].default_value = 7; wv.inputs['Detail'].default_value = 3; lk(t, mp.outputs[0], wv.inputs[0])
    knot = nd(t, 'ShaderNodeTexVoronoi'); knot.inputs['Scale'].default_value = 2.2; lk(t, tc.outputs['Object'], knot.inputs[0])   # 옹이
    kn = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, MA(t, 'DIVIDE', knot.outputs['Distance'], .16), clamp=True), .55)
    grain = MA(t, 'ADD', MA(t, 'MULTIPLY', nz.outputs['Fac'], .55), MA(t, 'MULTIPLY', wv.outputs['Color'], .45))
    base = ramp(t, grain, [(.2, (.03, .014, .006, 1)), (.85, (.145 + .015 * (seed % 3), .072, .03, 1))])
    dk = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, kn, dk.inputs[0]); lk(t, base, dk.inputs[6]); dk.inputs[7].default_value = (.012, .006, .003, 1)
    col = dk.outputs[2]
    st_ = nd(t, 'ShaderNodeTexNoise'); st_.inputs['Scale'].default_value = 2.2; st_.inputs['Detail'].default_value = 5; lk(t, tc.outputs['Object'], st_.inputs[0])       # 큰 얼룩(손·비·그을음)
    sm_ = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, MA(t, 'MULTIPLY', MA(t, 'GREATER_THAN', st_.outputs['Fac'], .5), .45), sm_.inputs[0]); lk(t, col, sm_.inputs[6]); sm_.inputs[7].default_value = (.01, .006, .003, 1); col = sm_.outputs[2]
    mps = nd(t, 'ShaderNodeMapping'); mps.inputs['Scale'].default_value = (60, 2.5, .6); lk(t, tc.outputs['Object'], mps.inputs[0])                                       # 세로 긁힘
    sc_ = nd(t, 'ShaderNodeTexNoise'); sc_.inputs['Scale'].default_value = 6; sc_.inputs['Detail'].default_value = 2; lk(t, mps.outputs[0], sc_.inputs[0])
    scr = MA(t, 'MULTIPLY', MA(t, 'GREATER_THAN', sc_.outputs['Fac'], .66), .5)
    sw_ = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, scr, sw_.inputs[0]); lk(t, col, sw_.inputs[6]); sw_.inputs[7].default_value = (.28, .17, .09, 1); col = sw_.outputs[2]
    if worn:                                                  # 닳은 모서리: 베벨 노멀 ↔ 기하 노멀 차이
        bv = nd(t, 'ShaderNodeBevel'); bv.inputs['Radius'].default_value = .012; geo = nd(t, 'ShaderNodeNewGeometry')
        dt = nd(t, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); lk(t, bv.outputs[0], dt.inputs[0]); lk(t, geo.outputs['Normal'], dt.inputs[1])
        edge = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, dt.outputs['Value'], clamp=True), 14, clamp=True)
        wr = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, edge, wr.inputs[0]); lk(t, col, wr.inputs[6]); wr.inputs[7].default_value = (.36, .22, .12, 1); col = wr.outputs[2]
        lk(t, bv.outputs[0], b.inputs['Normal']) if False else None
    GRIME['p'] = grime
    if GRIME['p'] is not None:                              # 손때: 손잡이 둘레 어두운 얼룩(널판 로컬 좌표 기준 — 오브젝트 좌표라 문이 돌아가도 따라간다)
        oc = nd(t, 'ShaderNodeTexCoord'); sp2 = nd(t, 'ShaderNodeSeparateXYZ'); lk(t, oc.outputs['Object'], sp2.inputs[0])
        dx = MA(t, 'SUBTRACT', sp2.outputs[0], GRIME['p'][0]); dz = MA(t, 'SUBTRACT', sp2.outputs[2], GRIME['p'][1])
        dist = MA(t, 'SQRT', MA(t, 'ADD', MA(t, 'MULTIPLY', dx, dx), MA(t, 'MULTIPLY', dz, dz)))
        gn = nd(t, 'ShaderNodeTexNoise'); gn.inputs['Scale'].default_value = 9; gn.inputs['Detail'].default_value = 6; lk(t, oc.outputs['Object'], gn.inputs[0])
        gr = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, MA(t, 'DIVIDE', dist, .38), clamp=True), MA(t, 'ADD', .5, gn.outputs['Fac']), clamp=True)
        gm_ = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, MA(t, 'MULTIPLY', gr, .8), gm_.inputs[0]); lk(t, col, gm_.inputs[6]); gm_.inputs[7].default_value = (.01, .006, .003, 1); col = gm_.outputs[2]
    lk(t, col, b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = .78
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = 1.0; bp.inputs['Distance'].default_value = .008; lk(t, grain, bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m

def iron_mat(rust=1.0):
    m, t, b = newmat('녹슨쇠')
    tc = nd(t, 'ShaderNodeTexCoord'); nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 9; nz.inputs['Detail'].default_value = 8; lk(t, tc.outputs['Object'], nz.inputs[0])
    nz2 = nd(t, 'ShaderNodeTexNoise'); nz2.inputs['Scale'].default_value = 60; lk(t, tc.outputs['Object'], nz2.inputs[0])
    rf = MA(t, 'MULTIPLY', ramp_f := MA(t, 'SUBTRACT', nz.outputs['Fac'], .38, clamp=True), 1.6 * rust, clamp=True)
    mix = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, rf, mix.inputs[0]); mix.inputs[6].default_value = (.035, .035, .04, 1); mix.inputs[7].default_value = (.16, .06, .022, 1)
    lk(t, mix.outputs[2], b.inputs['Base Color'])
    lk(t, MA(t, 'SUBTRACT', 1, MA(t, 'MULTIPLY', rf, .9), clamp=True), b.inputs['Metallic'])
    lk(t, MA(t, 'ADD', .42, MA(t, 'MULTIPLY', rf, .45)), b.inputs['Roughness'])
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .5; bp.inputs['Distance'].default_value = .004; lk(t, MA(t, 'ADD', nz.outputs['Fac'], nz2.outputs['Fac']), bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m

def stone_mat(name, col, scale=3.0, sq=False):
    m, t, b = newmat(name)
    tc = nd(t, 'ShaderNodeTexCoord'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (scale, scale, scale); lk(t, tc.outputs['Object'], mp.inputs[0])
    br = nd(t, 'ShaderNodeTexBrick'); br.inputs['Scale'].default_value = 1.0; br.inputs['Mortar Size'].default_value = .035; br.inputs['Bias'].default_value = 0
    br.inputs['Brick Width'].default_value = .55 if not sq else .5; br.inputs['Row Height'].default_value = .28 if not sq else .5
    br.inputs['Color1'].default_value = (*col, 1); br.inputs['Color2'].default_value = (col[0] * .6, col[1] * .6, col[2] * .65, 1); br.inputs['Mortar'].default_value = (col[0] * .3, col[1] * .3, col[2] * .32, 1)
    sp_ = nd(t, 'ShaderNodeSeparateXYZ'); cb_ = nd(t, 'ShaderNodeCombineXYZ'); lk(t, mp.outputs[0], sp_.inputs[0]); lk(t, sp_.outputs[0], cb_.inputs[0]); lk(t, sp_.outputs[2], cb_.inputs[1]); lk(t, sp_.outputs[1], cb_.inputs[2]); lk(t, cb_.outputs[0], br.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 25; nz.inputs['Detail'].default_value = 8; lk(t, mp.outputs[0], nz.inputs[0])
    mx = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, MA(t, 'MULTIPLY', nz.outputs['Fac'], .45), mx.inputs[0]); lk(t, br.outputs['Color'], mx.inputs[6]); mx.inputs[7].default_value = (.01, .012, .014, 1)
    lk(t, mx.outputs[2], b.inputs['Base Color']); b.inputs['Roughness'].default_value = .9
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .9; bp.inputs['Distance'].default_value = .03; lk(t, MA(t, 'ADD', MA(t, 'MULTIPLY', br.outputs['Fac'], .8), MA(t, 'MULTIPLY', nz.outputs['Fac'], .4)), bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m

def emit_mat(name, col, st):
    m, t, b = newmat(name)
    for n in list(t.nodes):
        if n.type != 'OUTPUT_MATERIAL': t.nodes.remove(n)
    o = next(n for n in t.nodes if n.type == 'OUTPUT_MATERIAL'); e = nd(t, 'ShaderNodeEmission'); e.inputs[0].default_value = (*col, 1); e.inputs[1].default_value = st; lk(t, e.outputs[0], o.inputs[0]); return m

def mkbox(name, size, loc, mat, bevel=.01, parent=None):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.scale = size; ob.location = loc; me.materials.append(mat)
    bpy.context.view_layer.objects.active = ob
    if bevel:
        bpy.context.view_layer.update(); ob.select_set(True); bpy.ops.object.transform_apply(scale=True); ob.select_set(False)
        md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 3; md.limit_method = 'ANGLE'
    if parent: ob.parent = parent; ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob
def mkcyl(name, r, h, loc, mat, rot=(0, 0, 0), seg=20, parent=None, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.location = loc; ob.rotation_euler = rot; me.materials.append(mat)
    if parent: ob.parent = parent; ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob
def mksph(name, r, loc, mat, parent=None, sub=2):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.location = loc; me.materials.append(mat)
    if parent: ob.parent = parent; ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob


IRON = iron_mat(1.0); IRON2 = iron_mat(.45)
def nails_row(ks, hx, inner, zc, n=7):
    pass
def build_leaf(side):
    hx = side * 1.0; inner = -side
    piv = bpy.data.objects.new(f'piv{side}', None); sc.collection.objects.link(piv); piv.location = (hx, 0, 0); ks = []
    hand_x = hx + inner * (W - .12); hand_z = H * .46
    nP = 6; pw = W / nP
    for i in range(nP):
        cx = inner * (pw * (i + .5)); ph = H - .004 * (i % 2); dz = .003 * ((i * 7) % 3)
        ks.append(mkbox(f'pl{i}', (pw - .016, .07 + .006 * ((i * 5) % 3), ph), (hx + cx, 0, ph / 2 + dz), wood_mat(i + 7 * (side + 2), grime=(hand_x - (hx + cx), hand_z - (ph / 2 + dz))), .01))
    # 널판 틈 뒤 불빛판(가장자리만 새는 빛: 틈과 안쪽 이음선)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=.5); me = bpy.data.meshes.new('bg'); bm.to_mesh(me); bm.free()
    g = bpy.data.objects.new(f'backglow{side}', me); sc.collection.objects.link(g); g.rotation_euler = (math.radians(90), 0, 0); g.scale = (W, H, 1); g.location = (hx + inner * W / 2, .09, H / 2)
    gm, gt, gb = newmat('bg')
    for nn in list(gt.nodes):
        if nn.type != 'OUTPUT_MATERIAL': gt.nodes.remove(nn)
    o = next(nn for nn in gt.nodes if nn.type == 'OUTPUT_MATERIAL'); em = nd(gt, 'ShaderNodeEmission'); em.inputs[1].default_value = 1.3
    tc = nd(gt, 'ShaderNodeTexCoord'); sp = nd(gt, 'ShaderNodeSeparateXYZ'); lk(gt, tc.outputs['UV'], sp.inputs[0])
    xin = sp.outputs[0] if side == -1 else sp.outputs[0]
    lk(gt, ramp(gt, xin, [(0, (.4, .09, .01, 1)), (1, (.8, .3, .06, 1))]), em.inputs[0]); lk(gt, em.outputs[0], o.inputs[0]); me.materials.append(gm); ks.append(g)
    for zc in (.2, H / 2 + .03, H - .2):
        sw = W * .86
        ks.append(mkbox('strap', (sw, .024, .085), (hx + inner * sw / 2, -.05, zc), IRON, .004))
        ks.append(mkcyl('tip', .0425, .024, (hx + inner * sw, -.05, zc), IRON, (math.radians(90), 0, 0), 3))
        for k in range(8): ks.append(mksph('rv', .0165, (hx + inner * (.07 + k * .11), -.066, zc), IRON2, None, 2))
        ks.append(mkcyl('hinge', .032, .15, (hx + inner * .003, -.058, zc), IRON, (0, 0, 0), 16))
        ks.append(mkcyl('knuckle', .037, .025, (hx + inner * .003, -.058, zc + .065), IRON, (0, 0, 0), 16))
        # 못 자국(널판 위 못머리·박힌 구멍) — 띠 위아래 한 줄
        for k in range(6):
            nx = hx + inner * (pw * (k + .5)); ks.append(mksph('nail', .009, (nx, -.043, zc + (.065 if k % 2 else -.065)), IRON2, None, 1))
    ks.append(mkbox('edge', (.05, .06, H), (hx + inner * (W - .025), -.04, H / 2), wood_mat(77), .01))
    hpx = hx + inner * (W - .12)
    ks.append(mkbox('hp', (.11, .022, .15), (hpx, -.065, H * .46), IRON, .014))
    for dz in (.045, -.045):
        for dx in (-.035, .035): ks.append(mksph('hr', .012, (hpx + dx, -.078, H * .46 + dz), IRON2, None, 2))
    bpy.ops.mesh.primitive_torus_add(major_radius=.07, minor_radius=.0125, major_segments=64, minor_segments=14, location=(hpx, -.1, H * .46 - .05), rotation=(math.radians(90), 0, 0)); rg = bpy.context.object
    rg.data.materials.append(IRON); bpy.ops.object.shade_smooth(); ks.append(rg); ks.append(mkcyl('mount', .018, .045, (hpx, -.08, H * .46 + .01), IRON, (math.radians(90), 0, 0), 14))
    for o_ in ks: o_.parent = piv; o_.matrix_parent_inverse = piv.matrix_world.inverted()
    return piv
L = build_leaf(-1); R = build_leaf(1)
def area(name, loc, rot, e, col, size, size_y=None):
    ld = bpy.data.lights.new(name, 'AREA'); ld.shape = 'RECTANGLE'; ld.size = size; ld.size_y = size_y or size; ld.energy = e; ld.color = col; ob = bpy.data.objects.new(name, ld); sc.collection.objects.link(ob); ob.location = loc; ob.rotation_euler = rot; return ob
# 정면 은은한 조명(왼쪽 위 따뜻한 키 + 오른쪽 아래 차가운 보조 + 아래 반사)
area('key', (-2.4, -3.8, 2.4), (math.radians(68), 0, math.radians(-32)), 360, (1, .74, .5), 3.2)
area('fill', (2.8, -3.2, .5), (math.radians(86), 0, math.radians(38)), 100, (.55, .66, 1), 3.2)
area('bounce', (0, -2.4, -.4), (math.radians(110), 0, 0), 55, (1, .6, .28), 4)
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.08, .07, .08, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .6
cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = H; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co; co.rotation_euler = (math.radians(90), 0, 0)
sc.render.engine = 'CYCLES'; cy = sc.cycles
try:
    pr = bpy.context.preferences.addons['cycles'].preferences; pr.compute_device_type = 'METAL'; pr.get_devices()
    for d_ in pr.devices: d_.use = True
    cy.device = 'GPU'
except Exception: cy.device = 'CPU'
cy.samples = SAMPLES
try: cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
except Exception: pass
cy.max_bounces = 6; cy.sample_clamp_indirect = 6
sc.render.resolution_x, sc.render.resolution_y = RX, RY; sc.render.film_transparent = True
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'; sc.render.image_settings.color_depth = '8'
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = float(os.environ.get('DOOR_EXPOSURE', '0.1'))
try: sc.view_settings.look = 'AgX - Punchy'
except Exception: pass
def render(leaf, cx, path):
    for o in (L, R):
        for c in o.children_recursive: c.hide_render = (o is not leaf)
    co.location = (cx, -6, H / 2); sc.render.filepath = path; bpy.ops.render.render(write_still=True); print('render', path, flush=True)
for ang in [int(a) for a in os.environ.get('DOOR_ANGLES', '0,45,75').split(',')]:
    suffix = '' if ang == 0 else f'_{ang}'
    for leaf, nm, cx in ((L, 'left', -.5), (R, 'right', .5)):
        leaf.rotation_euler = (0, 0, math.radians(-ang if leaf.location.x < 0 else ang))
        render(leaf, cx, f'{OUT}/door_{nm}{suffix}.png')
    L.rotation_euler = R.rotation_euler = (0, 0, 0)
