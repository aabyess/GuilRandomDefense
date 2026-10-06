"""선술집 문 v2 — 진짜 3D 문이 열리는 프레임 묶음(blender 세션 2026-10-06, 사장님 「더 진짜같이」).
문틀(돌 기둥·나무 인방·돌벽) 안의 쌍여닫이 문: PBR 원목(결·옹이·닳은 모서리·손잡이 때) · 녹슨 쇠띠·리벳·경첩·고리.
앞(카메라 쪽) = 어둑한 밤 거리(차가운 달빛 + 벽 등), 뒤(안쪽) = 따뜻한 주황 선술집 빛(면광 + 안개 → 열릴수록 틈으로 쏟아지는 빛줄기 + 바닥 빛 웅덩이 + 먼지 입자).
문 뒤는 투명(안쪽 바닥·벽은 카메라에 안 보이게) · 빛줄기(볼륨)만 반투명으로 남는다.
blender -b --factory-startup --python Tools/blender/gen_tavern_door_v2.py -- <출력> <가로> <세로> <샘플>   (기본 ~/GRD_tavern_door_v2 1920 1080 96)
환경변수 DOOR_FRAMES="0,12,23" 또는 "0-23". 프레임 i: 열림 각도 = 100° × i/23 (등간격, 안쪽(+Y)으로).  산출 frames/door_00~23.png (RGBA).
좌표: 문 열린 구멍 x −1..1, z 0..2.4, 카메라는 −Y에서 +Y를 본다. 짝: 경첩 x=∓0.95, 폭 0.95(가운데에서 만남)."""
import bpy, bmesh, math, os, sys, random
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if len(argv) > 0 else '~/GRD_tavern_door_v2')
RW = int(argv[1]) if len(argv) > 1 else 1920
RH = int(argv[2]) if len(argv) > 2 else 1080
SAMPLES = int(argv[3]) if len(argv) > 3 else 96
os.makedirs(OUT + '/frames', exist_ok=True)
random.seed(5); rs = np.random.RandomState(5)
bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
H = 2.4; LW = .95; TAU = math.tau

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

def wood_mat(seed, worn=True):
    m, t, b = newmat(f'원목{seed}')
    tc = nd(t, 'ShaderNodeTexCoord'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (11, 1.4, 1.1); mp.inputs['Location'].default_value = (seed * 3.7, seed * 1.3, 0)
    lk(t, tc.outputs['Object'], mp.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 7; nz.inputs['Detail'].default_value = 9; nz.inputs['Roughness'].default_value = .62; lk(t, mp.outputs[0], nz.inputs[0])
    wv = nd(t, 'ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.bands_direction = 'X'; wv.inputs['Scale'].default_value = 4.5; wv.inputs['Distortion'].default_value = 7; wv.inputs['Detail'].default_value = 3; lk(t, mp.outputs[0], wv.inputs[0])
    knot = nd(t, 'ShaderNodeTexVoronoi'); knot.inputs['Scale'].default_value = 2.2; lk(t, tc.outputs['Object'], knot.inputs[0])   # 옹이
    kn = MA(t, 'LESS_THAN', knot.outputs['Distance'], .07 + .03 * (seed % 3))
    grain = MA(t, 'ADD', MA(t, 'MULTIPLY', nz.outputs['Fac'], .55), MA(t, 'MULTIPLY', wv.outputs['Color'], .45))
    base = ramp(t, grain, [(.2, (.045, .02, .009, 1)), (.85, (.20 + .02 * (seed % 3), .10, .045, 1))])
    dk = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, kn, dk.inputs[0]); lk(t, base, dk.inputs[6]); dk.inputs[7].default_value = (.012, .006, .003, 1)
    col = dk.outputs[2]
    if worn:                                                  # 닳은 모서리: 베벨 노멀 ↔ 기하 노멀 차이
        bv = nd(t, 'ShaderNodeBevel'); bv.inputs['Radius'].default_value = .012; geo = nd(t, 'ShaderNodeNewGeometry')
        dt = nd(t, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); lk(t, bv.outputs[0], dt.inputs[0]); lk(t, geo.outputs['Normal'], dt.inputs[1])
        edge = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, dt.outputs['Value'], clamp=True), 14, clamp=True)
        wr = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, edge, wr.inputs[0]); lk(t, col, wr.inputs[6]); wr.inputs[7].default_value = (.36, .22, .12, 1); col = wr.outputs[2]
        lk(t, bv.outputs[0], b.inputs['Normal']) if False else None
    lk(t, col, b.inputs['Base Color'])
    b.inputs['Roughness'].default_value = .72
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .7; bp.inputs['Distance'].default_value = .006; lk(t, grain, bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m

def iron_mat(rust=1.0):
    m, t, b = newmat('녹슨쇠')
    tc = nd(t, 'ShaderNodeTexCoord'); nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 9; nz.inputs['Detail'].default_value = 8; lk(t, tc.outputs['Object'], nz.inputs[0])
    nz2 = nd(t, 'ShaderNodeTexNoise'); nz2.inputs['Scale'].default_value = 60; lk(t, tc.outputs['Object'], nz2.inputs[0])
    rf = MA(t, 'MULTIPLY', ramp_f := MA(t, 'SUBTRACT', nz.outputs['Fac'], .38, clamp=True), 2.2 * rust, clamp=True)
    mix = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, rf, mix.inputs[0]); mix.inputs[6].default_value = (.035, .035, .04, 1); mix.inputs[7].default_value = (.22, .085, .03, 1)
    lk(t, mix.outputs[2], b.inputs['Base Color'])
    lk(t, MA(t, 'SUBTRACT', 1, MA(t, 'MULTIPLY', rf, .75), clamp=True), b.inputs['Metallic'])
    lk(t, MA(t, 'ADD', .38, MA(t, 'MULTIPLY', rf, .45)), b.inputs['Roughness'])
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

IRON = iron_mat(1.0); IRON2 = iron_mat(.5)
STONE = stone_mat('벽돌', (.20, .19, .20), 2.6); PILL = stone_mat('기둥돌', (.26, .245, .24), 1.8, sq=True)
BEAM = wood_mat(31)

# ----------------------------------------------------------------------------- 문틀·벽
WT = .9                                                      # 벽 두께(y −.45..+.45)
mkbox('wall_L', (6, WT, 7), (-4.0, 0, 3.5), STONE, .02)
mkbox('wall_R', (6, WT, 7), (4.0, 0, 3.5), STONE, .02)
mkbox('wall_T', (2.0, WT, 4.5), (0, 0, H + .45 + 2.25), STONE, .02)
for sx in (-1, 1):                                           # 두꺼운 돌 기둥(문 옆, 앞으로 튀어나옴)
    for k, (z0, hz) in enumerate(((0, .8), (.8, .7), (1.5, .9))):
        mkbox('pillar', (.42, .22, hz), (sx * 1.2, -.56, z0 + hz / 2), PILL, .025)
lint = mkbox('lintel', (3.0, .5, .42), (0, -.5, H + .21), BEAM, .02)                     # 위 인방(두꺼운 나무)
mkbox('lintel2', (3.3, .3, .2), (0, -.62, H + .52), PILL, .02)
mkbox('step', (2.6, .9, .14), (0, -.75, .07), PILL, .02)                                   # 문턱
mkbox('doorway_back', (2.0, .25, .12), (0, .05, .06), PILL, .02) if False else None

# ----------------------------------------------------------------------------- 문짝
def build_leaf(side):
    hx = side * 0.95; inner = -side
    piv = bpy.data.objects.new(f'piv{side}', None); sc.collection.objects.link(piv); piv.location = (hx, 0, 0)
    ks = []
    n = 6; pw = LW / n
    for i in range(n):
        cx = inner * (pw * (i + .5))
        ph = H - .006 * (i % 2); dz = .004 * ((i * 7) % 3)
        pl = mkbox(f'pl{i}', (pw - .012, .075 + .006 * ((i * 5) % 3), ph), (hx + cx, 0, ph / 2 + dz), wood_mat(i + 7 * (side + 2)), .008)
        ks.append(pl)
    for zc in (.28, H / 2 + .05, H - .28):
        sw = LW * .86
        st = mkbox('strap', (sw, .022, .09), (hx + inner * sw / 2, -.05, zc), IRON, .004); ks.append(st)
        tip = mkcyl('tip', .045, .022, (hx + inner * sw, -.05, zc), IRON, (math.radians(90), 0, 0), 3); tip.scale = (1, 1, 1); ks.append(tip)
        for k in range(8):
            ks.append(mksph('rv', .0165, (hx + inner * (.07 + k * .11), -.064, zc), IRON2, None, 1))
        ks.append(mkcyl('hinge', .032, .16, (hx + inner * .003, -.055, zc), IRON, (0, 0, 0), 14))
    ks.append(mkbox('edge', (.045, .06, H), (hx + inner * (LW - .0225), -.04, H / 2), wood_mat(77), .008))
    hpx = hx + inner * (LW - .12)
    ks.append(mkbox('hp', (.11, .02, .15), (hpx, -.064, H * .46), IRON, .012))
    for dz in (.045, -.045):
        for dx in (-.035, .035): ks.append(mksph('hr', .012, (hpx + dx, -.076, H * .46 + dz), IRON2, None, 1))
    bpy.ops.mesh.primitive_torus_add(major_radius=.07, minor_radius=.0125, major_segments=48, minor_segments=12, location=(hpx, -.098, H * .46 - .05), rotation=(math.radians(90), 0, 0)); rg = bpy.context.object
    rg.data.materials.append(IRON); bpy.ops.object.shade_smooth(); ks.append(rg)
    ks.append(mkcyl('mount', .018, .045, (hpx, -.078, H * .46 + .01), IRON, (math.radians(90), 0, 0), 12))
    # 손잡이 주변 때 얼룩(원판 데칼: 방사형 알파)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(hpx, -.0395, H * .46 - .02), rotation=(math.radians(90), 0, 0)); dc = bpy.context.object; dc.scale = (.42, .5, 1); dc.name = 'grime'
    gm, gt, gb = newmat('때')
    for nn in list(gt.nodes):
        if nn.type != 'OUTPUT_MATERIAL': gt.nodes.remove(nn)
    go = next(nn for nn in gt.nodes if nn.type == 'OUTPUT_MATERIAL'); tcx = nd(gt, 'ShaderNodeTexCoord'); gr = nd(gt, 'ShaderNodeTexGradient', gradient_type='SPHERICAL')
    cen = nd(gt, 'ShaderNodeVectorMath', operation='SUBTRACT'); cen.inputs[1].default_value = (.5, .5, 0); lk(gt, tcx.outputs['UV'], cen.inputs[0]); mu = nd(gt, 'ShaderNodeVectorMath', operation='SCALE'); mu.inputs['Scale'].default_value = 2.0; lk(gt, cen.outputs[0], mu.inputs[0]); lk(gt, mu.outputs[0], gr.inputs[0])
    nz = nd(gt, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 14; lk(gt, tcx.outputs['Object'], nz.inputs[0])
    al = MA(gt, 'MULTIPLY', MA(gt, 'SUBTRACT', 1, gr.outputs['Fac'], clamp=True), MA(gt, 'ADD', .45, nz.outputs['Fac']), clamp=True)
    al2 = MA(gt, 'MULTIPLY', al, .62)
    tr = nd(gt, 'ShaderNodeBsdfTransparent'); dk = nd(gt, 'ShaderNodeBsdfDiffuse'); dk.inputs[0].default_value = (.01, .006, .003, 1); mxs = nd(gt, 'ShaderNodeMixShader'); lk(gt, al2, mxs.inputs[0]); lk(gt, tr.outputs[0], mxs.inputs[1]); lk(gt, dk.outputs[0], mxs.inputs[2]); lk(gt, mxs.outputs[0], go.inputs[0])
    dc.data.materials.append(gm); ks.append(dc)
    for o in ks:
        o.parent = piv; o.matrix_parent_inverse = piv.matrix_world.inverted()
    return piv
PL = build_leaf(-1); PR = build_leaf(1)

# ----------------------------------------------------------------------------- 바닥(거리·방 안)·등·먼지
def plane(name, sx, sy, loc, mat, vis=True):
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=.5); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.scale = (sx, sy, 1); ob.location = loc; me.materials.append(mat)
    if not vis: ob.visible_camera = False
    return ob
cob, ct, cb = newmat('자갈길')
tcc = nd(ct, 'ShaderNodeTexCoord'); vor = nd(ct, 'ShaderNodeTexVoronoi'); vor.inputs['Scale'].default_value = 5.5; lk(ct, tcc.outputs['Object'], vor.inputs[0]); vn = nd(ct, 'ShaderNodeTexNoise'); vn.inputs['Scale'].default_value = 18; lk(ct, tcc.outputs['Object'], vn.inputs[0])
lk(ct, ramp(ct, MA(ct, 'ADD', vor.outputs['Distance'], MA(ct, 'MULTIPLY', vn.outputs['Fac'], .25), clamp=True), [(.0, (.12, .115, .115, 1)), (.45, (.05, .048, .05, 1)), (.8, (.012, .012, .014, 1))]), cb.inputs['Base Color'])
cb.inputs['Roughness'].default_value = .32
cbp = nd(ct, 'ShaderNodeBump'); cbp.inputs['Strength'].default_value = 1.0; cbp.inputs['Distance'].default_value = .05; lk(ct, vor.outputs['Distance'], cbp.inputs['Height']); lk(ct, cbp.outputs[0], cb.inputs['Normal'])
plane('street', 14, 12, (0, -6.4, 0), cob)
wf, wt, wb = newmat('실내바닥'); wb.inputs['Base Color'].default_value = (.12, .06, .03, 1); wb.inputs['Roughness'].default_value = .5
plane('room_floor', 10, 8, (0, 4.2, 0), wf, vis=False)
for (x, y, z, sx, sy, rx, ry) in ((-3.2, 4, 3, 8, 4, 0, 90), (3.2, 4, 3, 8, 4, 0, 90)):                 # 방 옆벽 — 카메라엔 안 보임
    w_ = plane('rw', 8, 6, (x, y, z), wf, vis=False); w_.rotation_euler = (0, math.radians(90), 0)
bk = plane('room_back', 8, 6, (0, 7.6, 3), wf, vis=False); bk.rotation_euler = (math.radians(90), 0, 0)
cl = plane('room_ceil', 10, 8, (0, 4.2, 3.2), wf, vis=False)
# 벽 등(왼쪽 벽, 문 옆): 쇠 틀 + 발광 + 약한 점광
lamp = mkbox('lampbody', (.22, .22, .36), (-2.05, -.62, 1.75), IRON, .01)
mkbox('lampglass', (.15, .15, .27), (-2.05, -.62, 1.75), emit_mat('등', (1, .62, .25), 14), 0)
mkbox('lamparm', (.5, .06, .06), (-1.85, -.52, 2.0), IRON, .005)
ld = bpy.data.lights.new('lampL', 'POINT'); ld.color = (1, .62, .28); ld.energy = 55; ld.shadow_soft_size = .1
lo = bpy.data.objects.new('lampL', ld); sc.collection.objects.link(lo); lo.location = (-2.05, -.9, 1.75); lo.visible_volume_scatter = False

# 빛
def area(name, loc, rot, e, col, size, shape='RECTANGLE', size_y=None):
    ld = bpy.data.lights.new(name, 'AREA'); ld.shape = shape; ld.size = size
    if size_y: ld.size_y = size_y
    ld.energy = e; ld.color = col; ob = bpy.data.objects.new(name, ld); sc.collection.objects.link(ob); ob.location = loc; ob.rotation_euler = rot; return ob
area('inner_key', (0, 2.6, 1.25), (math.radians(-90), 0, 0), 2600, (1, .55, .2), 2.2, size_y=1.9)
area('inner_up', (0, 1.6, 2.7), (math.radians(180 - 28), 0, 0), 900, (1, .5, .16), 2.2, size_y=1.2)
mn = bpy.data.lights.new('moon', 'SUN'); mn.energy = 2.2; mn.color = (.5, .62, 1); mn.angle = math.radians(3); mo = bpy.data.objects.new('moon', mn); sc.collection.objects.link(mo); mo.visible_volume_scatter = False; mo.rotation_euler = (math.radians(60), 0, math.radians(-28))
w = bpy.data.worlds.new('밤'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.012, .018, .04, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 1.0

# 빛줄기용 안개(방 + 문 앞 거리 일부)
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); me = bpy.data.meshes.new('fog'); bm.to_mesh(me); bm.free()
fog = bpy.data.objects.new('fog', me); sc.collection.objects.link(fog); fog.scale = (6.0, 6.2, 2.9); fog.location = (0, 3.3, 1.45)
fm, ft, fb = newmat('안개')
for n_ in list(ft.nodes):
    if n_.type != 'OUTPUT_MATERIAL': ft.nodes.remove(n_)
fo = next(n_ for n_ in ft.nodes if n_.type == 'OUTPUT_MATERIAL'); vp = nd(ft, 'ShaderNodeVolumePrincipled'); vp.inputs['Color'].default_value = (1, .8, .6, 1); vp.inputs['Density'].default_value = .045; vp.inputs['Anisotropy'].default_value = .45
lk(ft, vp.outputs[0], fo.inputs['Volume']); me.materials.append(fm); fog.display_type = 'WIRE'

# 먼지 입자(빛이 지나는 곳)
dm = emit_mat('먼지', (1, .78, .5), 7)
for i in range(170):
    p = (rs.uniform(-1.0, 1.0), rs.uniform(-2.2, 3.0), rs.uniform(.2, 2.3)); o = mksph('dust', rs.uniform(.004, .009), p, dm, None, 1)
    o['p0'] = p; o['ph'] = rs.rand() * TAU

# 카메라
cam = bpy.data.cameras.new('cam'); cam.lens = 26; cam.sensor_width = 36; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
co.location = (0, -4.9, 1.6); co.rotation_euler = (math.radians(85), 0, 0)

# 렌더 설정
sc.render.engine = 'CYCLES'; cy = sc.cycles
try:
    pr = bpy.context.preferences.addons['cycles'].preferences; pr.compute_device_type = 'METAL'; pr.get_devices()
    for d_ in pr.devices: d_.use = True
    cy.device = 'GPU'; print('GPU', [d_.name for d_ in pr.devices])
except Exception as e: print('gpu fail', e); cy.device = 'CPU'
cy.samples = SAMPLES
try: cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
except Exception: pass
cy.max_bounces = 7; cy.volume_bounces = 2; cy.volume_step_rate = 1.5; cy.sample_clamp_indirect = 6
sc.render.resolution_x = RW; sc.render.resolution_y = RH; sc.render.film_transparent = True
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'; sc.render.image_settings.color_depth = '8'
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = float(os.environ.get('DOOR_EXPOSURE', '0.0'))
try: sc.view_settings.look = 'AgX - Punchy'
except Exception: pass

sel = os.environ.get('DOOR_FRAMES', '0-23')
if '-' in sel: a_, b_ = sel.split('-'); frames = range(int(a_), int(b_) + 1)
else: frames = [int(x) for x in sel.split(',')]
for i in frames:
    ang = math.radians(100.0 * i / 23.0)
    PL.rotation_euler = (0, 0, ang)           # 왼짝: 경첩 x=−0.95 → 안쪽(+Y)으로 열리려면 +Z 회전(−X→+Y 쪽으로 …)
    PR.rotation_euler = (0, 0, -ang)
    for o in bpy.data.objects:
        if o.name.startswith('dust'):
            p = o['p0']; t_ = i / 23.0
            o.location = (p[0] + .05 * math.sin(TAU * t_ + o['ph']), p[1] + .07 * math.cos(TAU * t_ + o['ph'] * 1.3), p[2] + .08 * t_ - .04)
    sc.render.filepath = f'{OUT}/frames/door_{i:02d}.png'; bpy.ops.render.render(write_still=True); print('frame', i, flush=True)
