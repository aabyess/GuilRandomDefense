"""첫 화면(로비) 배경 렌더 — 해 질 녘 섬·협곡 대지·상점·횃불·안개(2026-10-06, blender 세션).
   워크3 본 메뉴 느낌: 어둡고 묵직한 판타지, 3D 장면이 뒤에 깔리고 오른쪽 1/3은 메뉴 자리라 비워 둔다.
   우리 에셋: 상점 7채(~/GRD_motion_trial/상점_7채/*.fbx) · 협곡 텍스처(~/GRD_canyon/canyon_strata.png·canyon_top.png — gen_canyon_tex.py).
   그 밖은 전부 절차(지형=높이장 · 바다 · 하늘 · 바위 · 횃불). 외부 에셋 없음.

   blender -b --factory-startup --python Tools/blender/gen_lobby_art.py -- <출력폴더> <가로> <세로> <샘플수>
   기본: ~/GRD_lobby_art  2560 1440 96   → <출력폴더>/bg_raw_<가로>.png (후처리는 gen_lobby_post.py)
   Blender 창(MCP)에서 장면만 보려면 환경변수 LOBBY_NO_RENDER=1 (render 건너뜀) 후 exec로 불러도 된다.
"""
import bpy, bmesh, math, os, sys, glob
import numpy as np
from mathutils import Vector, Matrix, Euler

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if len(argv) > 0 else '~/GRD_lobby_art')
RW = int(argv[1]) if len(argv) > 1 else 2560
RH = int(argv[2]) if len(argv) > 2 else 1440
SAMPLES = int(argv[3]) if len(argv) > 3 else 96
os.makedirs(OUT, exist_ok=True)
HOME = os.path.expanduser('~')
rng = np.random.RandomState(1006)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ------------------------------------------------------------------ 노드 도우미
def nd(tree, typ, **kw):
    n = tree.nodes.new(typ)
    for k, v in kw.items():
        if k == 'loc': n.location = v
        else:
            try: setattr(n, k, v)
            except Exception: pass
    return n


def mat_new(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes.clear()
    return m


def lk(t, a, b):
    t.links.new(a, b)


def load_img(path):
    return bpy.data.images.load(path, check_existing=True)


# ------------------------------------------------------------------ 지형(높이장)
def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


class Waves:
    """사인 합 소음(주기적이지 않아도 되는 지형용). 시드 고정."""
    def __init__(self, seed, n=24, kmin=.02, kmax=.9, decay=1.0):
        r = np.random.RandomState(seed)
        ang = r.rand(n) * 2 * np.pi
        k = kmin * (kmax / kmin) ** r.rand(n)
        self.kx = k * np.cos(ang); self.ky = k * np.sin(ang)
        self.ph = r.rand(n) * 2 * np.pi
        self.amp = (kmin / k) ** decay
        self.amp /= self.amp.sum()

    def __call__(self, x, y):
        out = np.zeros_like(x, dtype=np.float32)
        for kx, ky, ph, a in zip(self.kx, self.ky, self.ph, self.amp):
            out += a * np.sin(kx * x + ky * y + ph)
        return out


N1 = Waves(1); N2 = Waves(2, kmin=.05, kmax=1.4, decay=.8); N3 = Waves(3, kmin=.15, kmax=2.5, decay=.6)
MESA_H = 9.0
ARM_W = 10.5; ARM_L = 33.0


def box_sdf(x, y, hx, hy):
    qx = np.abs(x) - hx; qy = np.abs(y) - hy
    return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0)


def terrain_h(x, y):
    r = np.sqrt(x * x + y * y)
    # 섬: 해안까지 완만한 구릉 → 바다 밑으로
    shore = 64 + 5 * N1(x * 1.3, y * 1.3)
    land = 2.3 + 1.1 * N1(x, y) + .5 * N2(x, y)
    h = np.where(r < shore, land, land) * smooth(shore + 9, shore - 5, r) - 2.6 * (1 - smooth(shore + 22, shore - 5, r))
    # 십자 협곡 대지: 계단식 절벽
    sdf = np.minimum(box_sdf(x, y, ARM_W, ARM_L), box_sdf(x, y, ARM_L, ARM_W))
    sdf = sdf + 2.6 * N2(x * .9, y * .9) + .6 * N3(x, y)
    p = 1 - smooth(-1.0, 4.5, sdf)                       # 안쪽 1, 바깥 0 (절벽 폭 ≈ 5.5m)
    t = p * 4
    step = (np.floor(t) + smooth(.30, .70, t - np.floor(t))) / 4     # 4단 계단
    p2 = np.where(p > .999, 1.0, step)
    h = h + MESA_H * p2 + (.35 * N2(x, y) + .15 * N3(x, y)) * p2
    return h.astype(np.float32)


def build_terrain():
    ext = 130.0; n = 360
    xs = np.linspace(-ext, ext, n); X, Y = np.meshgrid(xs, xs)
    Z = terrain_h(X, Y)
    # 절벽 면 XY 흔들기: 바위처럼 거칠게
    steep = np.clip(np.hypot(*np.gradient(Z, xs[1] - xs[0])) / 3.0, 0, 1)
    X2 = X + steep * 1.0 * N3(X + 3, Y); Y2 = Y + steep * 1.0 * N3(X, Y + 7)
    verts = np.stack([X2.ravel(), Y2.ravel(), Z.ravel()], -1)
    idx = np.arange(n * n).reshape(n, n)
    faces = np.stack([idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()], -1)
    me = bpy.data.meshes.new('Terrain'); me.from_pydata(verts.tolist(), [], faces.tolist()); me.update()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new('Terrain', me); scene.collection.objects.link(ob)
    return ob


def terrain_material():
    m = mat_new('지형'); t = m.node_tree
    out = nd(t, 'ShaderNodeOutputMaterial', loc=(1800, 0))
    bs = nd(t, 'ShaderNodeBsdfPrincipled', loc=(1500, 0)); bs.inputs['Roughness'].default_value = .92
    lk(t, bs.outputs[0], out.inputs[0])
    geo = nd(t, 'ShaderNodeNewGeometry', loc=(-1200, 0))
    sep_p = nd(t, 'ShaderNodeSeparateXYZ', loc=(-1000, 100)); lk(t, geo.outputs['Position'], sep_p.inputs[0])
    sep_n = nd(t, 'ShaderNodeSeparateXYZ', loc=(-1000, -100)); lk(t, geo.outputs['Normal'], sep_n.inputs[0])
    # 위·옆 마스크
    cliff = nd(t, 'ShaderNodeMapRange', loc=(-700, -200)); cliff.inputs[1].default_value = .9; cliff.inputs[2].default_value = .62
    cliff.inputs[3].default_value = 0; cliff.inputs[4].default_value = 1
    lk(t, sep_n.outputs['Z'], cliff.inputs[0])
    top = nd(t, 'ShaderNodeMapRange', loc=(-700, 100)); top.inputs[1].default_value = 5.2; top.inputs[2].default_value = 7.0
    lk(t, sep_p.outputs['Z'], top.inputs[0])
    sand = nd(t, 'ShaderNodeMapRange', loc=(-700, 300)); sand.inputs[1].default_value = .5; sand.inputs[2].default_value = -.1
    lk(t, sep_p.outputs['Z'], sand.inputs[0])
    # 질감: 협곡 지층(벽) · 붉은 흙(윗면) · 풀(바닥)
    pos = geo.outputs['Position']
    def imgnode(path, scale, proj, loc):
        im = nd(t, 'ShaderNodeTexImage', loc=loc); im.image = load_img(path); im.projection = proj
        if proj == 'BOX': im.projection_blend = .35
        im.interpolation = 'Linear'
        mp = nd(t, 'ShaderNodeMapping', loc=(loc[0] - 250, loc[1])); mp.inputs['Scale'].default_value = (scale,) * 3
        lk(t, pos, mp.inputs[0]); lk(t, mp.outputs[0], im.inputs[0])
        return im
    strata = imgnode(HOME + '/GRD_canyon/canyon_strata.png', .085, 'BOX', (-300, -400))
    topimg = imgnode(HOME + '/GRD_canyon/canyon_top.png', .06, 'FLAT', (-300, 150))
    # 풀: 어두운 이끼 녹색 + 마른 흙 섞기
    nz = nd(t, 'ShaderNodeTexNoise', loc=(-700, 500)); nz.inputs['Scale'].default_value = .09; nz.inputs['Detail'].default_value = 6
    lk(t, pos, nz.inputs[0])
    gr = nd(t, 'ShaderNodeValToRGB', loc=(-300, 500))
    gr.color_ramp.elements[0].color = (.01, .06, .02, 1); gr.color_ramp.elements[1].color = (.04, .15, .04, 1)
    e = gr.color_ramp.elements.new(.5); e.color = (.06, .05, .025, 1)
    lk(t, nz.outputs['Fac'], gr.inputs[0])
    nz2 = nd(t, 'ShaderNodeTexNoise', loc=(-700, 650)); nz2.inputs['Scale'].default_value = 1.4; nz2.inputs['Detail'].default_value = 8
    lk(t, pos, nz2.inputs[0])
    mulg = nd(t, 'ShaderNodeMixRGB', loc=(0, 500), blend_type='MULTIPLY'); mulg.inputs['Fac'].default_value = .55
    lk(t, gr.outputs[0], mulg.inputs[1]); lk(t, nz2.outputs['Color'], mulg.inputs[2])
    sandc = nd(t, 'ShaderNodeRGB', loc=(0, 330)); sandc.outputs[0].default_value = (.34, .27, .17, 1)
    g1 = nd(t, 'ShaderNodeMix', loc=(300, 400), data_type='RGBA'); lk(t, sand.outputs[0], g1.inputs[0])
    lk(t, mulg.outputs[0], g1.inputs[6]); lk(t, sandc.outputs[0], g1.inputs[7])
    # 윗면(붉은 흙) 합성
    tops = nd(t, 'ShaderNodeMix', loc=(600, 300), data_type='RGBA'); lk(t, top.outputs[0], tops.inputs[0])
    lk(t, g1.outputs[2], tops.inputs[6]); lk(t, topimg.outputs[0], tops.inputs[7])
    # 절벽
    cl = nd(t, 'ShaderNodeMix', loc=(900, 100), data_type='RGBA'); lk(t, cliff.outputs[0], cl.inputs[0])
    lk(t, tops.outputs[2], cl.inputs[6]); lk(t, strata.outputs[0], cl.inputs[7])
    # 어둡게(해 질 녘) + 대비
    hsv = nd(t, 'ShaderNodeHueSaturation', loc=(1200, 100)); hsv.inputs['Value'].default_value = .5; hsv.inputs['Saturation'].default_value = 1.1
    lk(t, cl.outputs[2], hsv.inputs['Color']); lk(t, hsv.outputs[0], bs.inputs['Base Color'])
    bump = nd(t, 'ShaderNodeBump', loc=(1200, -300)); bump.inputs['Strength'].default_value = .5; bump.inputs['Distance'].default_value = .3
    bn = nd(t, 'ShaderNodeTexNoise', loc=(900, -350)); bn.inputs['Scale'].default_value = 2.5; bn.inputs['Detail'].default_value = 10
    lk(t, pos, bn.inputs[0]); lk(t, bn.outputs['Fac'], bump.inputs['Height']); lk(t, bump.outputs[0], bs.inputs['Normal'])
    return m


def build_sea():
    me = bpy.data.meshes.new('Sea')
    s = 900
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    ob = bpy.data.objects.new('Sea', me); scene.collection.objects.link(ob)
    m = mat_new('바다'); t = m.node_tree
    out = nd(t, 'ShaderNodeOutputMaterial', loc=(900, 0)); bs = nd(t, 'ShaderNodeBsdfPrincipled', loc=(600, 0))
    bs.inputs['Base Color'].default_value = (.002, .012, .02, 1); bs.inputs['Roughness'].default_value = .05
    bs.inputs['IOR'].default_value = 1.33
    try: bs.inputs['Specular IOR Level'].default_value = .7
    except Exception: pass
    lk(t, bs.outputs[0], out.inputs[0])
    tc = nd(t, 'ShaderNodeTexCoord', loc=(-600, 0))
    mp1 = nd(t, 'ShaderNodeMapping', loc=(-400, 100)); mp1.inputs['Scale'].default_value = (.045, .09, 1)
    mp2 = nd(t, 'ShaderNodeMapping', loc=(-400, -150)); mp2.inputs['Scale'].default_value = (.5, .8, 1)
    lk(t, tc.outputs['Object'], mp1.inputs[0]); lk(t, tc.outputs['Object'], mp2.inputs[0])
    n1 = nd(t, 'ShaderNodeTexNoise', loc=(-150, 100)); n1.inputs['Detail'].default_value = 5; lk(t, mp1.outputs[0], n1.inputs[0])
    n2 = nd(t, 'ShaderNodeTexNoise', loc=(-150, -150)); n2.inputs['Detail'].default_value = 3; lk(t, mp2.outputs[0], n2.inputs[0])
    add = nd(t, 'ShaderNodeMath', loc=(100, 0), operation='ADD'); lk(t, n1.outputs['Fac'], add.inputs[0]); lk(t, n2.outputs['Fac'], add.inputs[1])
    bp = nd(t, 'ShaderNodeBump', loc=(350, -100)); bp.inputs['Strength'].default_value = .55; bp.inputs['Distance'].default_value = .6
    lk(t, add.outputs[0], bp.inputs['Height']); lk(t, bp.outputs[0], bs.inputs['Normal'])
    ob.data.materials.append(m)
    return ob


# ------------------------------------------------------------------ 하늘
def build_world():
    w = bpy.data.worlds.new('황혼'); scene.world = w; w.use_nodes = True
    t = w.node_tree; t.nodes.clear()
    out = nd(t, 'ShaderNodeOutputWorld', loc=(1200, 0))
    bg = nd(t, 'ShaderNodeBackground', loc=(900, 0)); lk(t, bg.outputs[0], out.inputs[0])
    # 하늘: 손으로 그린 황혼 그라데이션(물리 하늘은 너무 어둡게 나온다) — 수평선 주황 → 보라 → 짙은 남색, 해 진 방향에 따뜻한 번짐
    tcs = nd(t, 'ShaderNodeTexCoord', loc=(-900, 100))
    sepz = nd(t, 'ShaderNodeSeparateXYZ', loc=(-700, 100)); lk(t, tcs.outputs['Generated'], sepz.inputs[0])
    ctr = nd(t, 'ShaderNodeVectorMath', loc=(-900, -100), operation='MULTIPLY_ADD'); lk(t, tcs.outputs['Generated'], ctr.inputs[0])   # 월드의 Generated는 0..1 → -1..1 방향으로
    ctr.inputs[1].default_value = (2, 2, 2); ctr.inputs[2].default_value = (-1, -1, -1)
    norm = nd(t, 'ShaderNodeVectorMath', loc=(-700, -100), operation='NORMALIZE'); lk(t, ctr.outputs[0], norm.inputs[0])
    sepn = nd(t, 'ShaderNodeSeparateXYZ', loc=(-500, -100)); lk(t, norm.outputs[0], sepn.inputs[0])
    ramp = nd(t, 'ShaderNodeValToRGB', loc=(-300, 100))
    cr = ramp.color_ramp; cr.interpolation = 'EASE'
    cr.elements[0].position = 0.0; cr.elements[0].color = (.012, .016, .03, 1)
    for pos, col in ((.47, (.03, .035, .06, 1)), (.51, (.95, .36, .12, 1)), (.545, (.40, .13, .20, 1)), (.62, (.07, .08, .21, 1)), (.85, (.018, .03, .10, 1))):
        e = cr.elements.new(pos); e.color = col
    cr.elements[-1].position = 1.0; cr.elements[-1].color = (.01, .02, .075, 1)
    zmap = nd(t, 'ShaderNodeMapRange', loc=(-500, 100)); zmap.inputs[1].default_value = -1; zmap.inputs[2].default_value = 1
    lk(t, sepn.outputs['Z'], zmap.inputs[0]); lk(t, zmap.outputs[0], ramp.inputs[0])
    # 해 방향 번짐
    sdir = nd(t, 'ShaderNodeCombineXYZ', loc=(-700, -350)); sdir.inputs[0].default_value = math.cos(math.radians(SUN_AZ)); sdir.inputs[1].default_value = math.sin(math.radians(SUN_AZ)); sdir.inputs[2].default_value = 0
    dot = nd(t, 'ShaderNodeVectorMath', loc=(-500, -350), operation='DOT_PRODUCT'); lk(t, norm.outputs[0], dot.inputs[0]); lk(t, sdir.outputs[0], dot.inputs[1])
    pw = nd(t, 'ShaderNodeMath', loc=(-300, -350), operation='POWER', use_clamp=True); pw.inputs[1].default_value = 5.0
    cl0 = nd(t, 'ShaderNodeMath', loc=(-420, -350), operation='MAXIMUM'); cl0.inputs[1].default_value = 0
    lk(t, dot.outputs['Value'], cl0.inputs[0]); lk(t, cl0.outputs[0], pw.inputs[0])
    hz = nd(t, 'ShaderNodeMapRange', loc=(-300, -520), clamp=True); hz.inputs[1].default_value = .45; hz.inputs[2].default_value = .56
    lk(t, zmap.outputs[0], hz.inputs[0])
    hz2 = nd(t, 'ShaderNodeMapRange', loc=(-300, -650), clamp=True); hz2.inputs[1].default_value = .56; hz2.inputs[2].default_value = .76; hz2.inputs[3].default_value = 1; hz2.inputs[4].default_value = 0
    lk(t, zmap.outputs[0], hz2.inputs[0])
    gm = nd(t, 'ShaderNodeMath', loc=(-100, -450), operation='MULTIPLY'); lk(t, pw.outputs[0], gm.inputs[0]); lk(t, hz.outputs[0], gm.inputs[1])
    gm2 = nd(t, 'ShaderNodeMath', loc=(80, -450), operation='MULTIPLY'); lk(t, gm.outputs[0], gm2.inputs[0]); lk(t, hz2.outputs[0], gm2.inputs[1])
    gcol = nd(t, 'ShaderNodeMix', loc=(300, -100), data_type='RGBA', blend_type='ADD'); gcol.inputs[6].default_value = (0, 0, 0, 1); gcol.inputs[7].default_value = (1.6, .55, .18, 1)
    lk(t, gm2.outputs[0], gcol.inputs[0]); lk(t, ramp.outputs[0], gcol.inputs[6])
    # 물리 하늘(블렌더 5: MULTIPLE_SCATTERING) — 해는 지평선 바로 위, 해 방향은 SUN_AZ
    sky = nd(t, 'ShaderNodeTexSky', loc=(300, 100)); sky.sky_type = 'MULTIPLE_SCATTERING'
    sky.sun_elevation = math.radians(float(os.environ.get('LOBBY_SUN_EL', '0.4'))); sky.sun_rotation = math.radians(float(os.environ.get('LOBBY_SUN_ROT', '56')))
    sky.sun_disc = bool(os.environ.get('LOBBY_SUN_DISC'))
    try: sky.dust_density = 3.0; sky.air_density = 1.4
    except Exception: pass
    # 별: 윗하늘에서만, 보로노이 점
    tc = nd(t, 'ShaderNodeTexCoord', loc=(-400, -300))
    vor = nd(t, 'ShaderNodeTexVoronoi', loc=(-100, -300)); vor.feature = 'F1'; vor.inputs['Scale'].default_value = 120
    lk(t, norm.outputs[0], vor.inputs[0])
    thr = nd(t, 'ShaderNodeMapRange', loc=(150, -300)); thr.inputs[1].default_value = .035; thr.inputs[2].default_value = 0
    lk(t, vor.outputs['Distance'], thr.inputs[0])
    sep = nd(t, 'ShaderNodeSeparateXYZ', loc=(-100, -550)); lk(t, norm.outputs[0], sep.inputs[0])
    up = nd(t, 'ShaderNodeMapRange', loc=(150, -550)); up.inputs[1].default_value = .12; up.inputs[2].default_value = .5
    lk(t, sep.outputs['Z'], up.inputs[0])
    sm = nd(t, 'ShaderNodeMath', loc=(400, -400), operation='MULTIPLY'); lk(t, thr.outputs[0], sm.inputs[0]); lk(t, up.outputs[0], sm.inputs[1])
    star = nd(t, 'ShaderNodeEmission', loc=(600, -400)); star.inputs['Strength'].default_value = 3.0
    lk(t, sm.outputs[0], star.inputs['Color'])
    ad = nd(t, 'ShaderNodeMixRGB', loc=(700, 0), blend_type='ADD'); ad.inputs['Fac'].default_value = 1
    lk(t, sky.outputs[0], ad.inputs[1]); lk(t, sm.outputs[0], ad.inputs[2])
    lk(t, ad.outputs[0], bg.inputs['Color']); bg.inputs['Strength'].default_value = float(os.environ.get('LOBBY_SKY', '0.14'))
    # (월드 볼륨 안개는 쓰지 않는다 — 무한히 뻗어 하늘을 통째로 가린다. 안개는 ground_fog()의 상자)


def ground_fog():
    if os.environ.get('LOBBY_NOFOG'): return
    me = bpy.data.meshes.new('Fog'); ob = bpy.data.objects.new('Fog', me); scene.collection.objects.link(ob)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bm.to_mesh(me); bm.free()
    ob.scale = (2400, 2400, 22); ob.location = (0, 0, 8.0)
    m = mat_new('안개층'); t = m.node_tree
    out = nd(t, 'ShaderNodeOutputMaterial', loc=(700, 0))
    v = nd(t, 'ShaderNodeVolumePrincipled', loc=(450, 0)); v.inputs['Color'].default_value = (.34, .42, .62, 1)
    v.inputs['Anisotropy'].default_value = .4
    tc = nd(t, 'ShaderNodeTexCoord', loc=(-500, 0)); mp = nd(t, 'ShaderNodeMapping', loc=(-300, 0)); mp.inputs['Scale'].default_value = (.018, .018, .06)
    lk(t, tc.outputs['Object'], mp.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise', loc=(-100, 0)); nz.inputs['Detail'].default_value = 4; lk(t, mp.outputs[0], nz.inputs[0])
    rm = nd(t, 'ShaderNodeMapRange', loc=(150, 0)); rm.inputs[1].default_value = .38; rm.inputs[2].default_value = .72
    rm.inputs[3].default_value = 0; rm.inputs[4].default_value = .013
    hs = nd(t, 'ShaderNodeSeparateXYZ', loc=(-300, -250)); lk(t, tc.outputs['Object'], hs.inputs[0])
    hf = nd(t, 'ShaderNodeMapRange', loc=(-100, -250)); hf.inputs[1].default_value = .35; hf.inputs[2].default_value = -.5   # 위로 갈수록 옅게(Object z -0.5..0.5)
    hf.inputs[3].default_value = 0; hf.inputs[4].default_value = 1
    lk(t, hs.outputs['Z'], hf.inputs[0])
    dm = nd(t, 'ShaderNodeMath', loc=(300, -100), operation='MULTIPLY'); lk(t, rm.outputs[0], dm.inputs[0]); lk(t, hf.outputs[0], dm.inputs[1])
    lk(t, nz.outputs['Fac'], rm.inputs[0]); lk(t, dm.outputs[0], v.inputs['Density'])
    lk(t, v.outputs[0], out.inputs['Volume'])
    ob.data.materials.append(m)
    ob.display_type = 'WIRE'


# ------------------------------------------------------------------ 소품: 상점·횃불·바위
def import_shop(name, loc, yaw, scale=1.0):
    f = HOME + '/GRD_motion_trial/상점_7채/상점_%s.fbx' % name
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=f)
    new = [o for o in bpy.data.objects if o not in before]
    root = bpy.data.objects.new('shop_' + name, None); scene.collection.objects.link(root)
    for o in new:
        if o.parent is None:
            o.parent = root
    root.location = loc; root.rotation_euler = (0, 0, yaw); root.scale = (scale,) * 3
    for o in new:
        if o.type == 'MESH':
            for ms in o.material_slots:
                m = ms.material
                if m and m.use_nodes:
                    for n in m.node_tree.nodes:
                        if n.type == 'BSDF_PRINCIPLED':
                            n.inputs['Roughness'].default_value = max(.6, n.inputs['Roughness'].default_value)
    return root


def torch(loc, power=4200, color=(1, .52, .18), height=2.8, flame=1.0):
    x, y, z = loc
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=.11, radius2=.07, depth=height)
    me = bpy.data.meshes.new('torch'); bm.to_mesh(me); bm.free()
    pole = bpy.data.objects.new('torch', me); scene.collection.objects.link(pole)
    pole.location = (x, y, z + height / 2)
    pm = mat_new('횃불기둥'); b = nd(pm.node_tree, 'ShaderNodeBsdfPrincipled'); b.inputs['Base Color'].default_value = (.045, .03, .02, 1); b.inputs['Roughness'].default_value = .8
    o = nd(pm.node_tree, 'ShaderNodeOutputMaterial'); lk(pm.node_tree, b.outputs[0], o.inputs[0]); me.materials.append(pm)
    # 불꽃(발광 + 점광)
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=.2 * flame)
    for v in bm.verts: v.co.z *= 1.7
    fm = bpy.data.meshes.new('flame'); bm.to_mesh(fm); bm.free()
    fl = bpy.data.objects.new('flame', fm); scene.collection.objects.link(fl)
    fl.location = (x, y, z + height + .22 * flame)
    mm = mat_new('불꽃'); e = nd(mm.node_tree, 'ShaderNodeEmission'); e.inputs['Color'].default_value = (1, .55, .15, 1); e.inputs['Strength'].default_value = 40
    oo = nd(mm.node_tree, 'ShaderNodeOutputMaterial'); lk(mm.node_tree, e.outputs[0], oo.inputs[0]); fm.materials.append(mm)
    ld = bpy.data.lights.new('torchL', 'POINT'); ld.color = color; ld.energy = power; ld.shadow_soft_size = .35
    lo = bpy.data.objects.new('torchL', ld); scene.collection.objects.link(lo); lo.location = (x, y, z + height + .4)
    return pole


def boulders(n, seed):
    r = np.random.RandomState(seed)
    mat = mat_new('바위'); t = mat.node_tree
    out = nd(t, 'ShaderNodeOutputMaterial', loc=(500, 0)); bs = nd(t, 'ShaderNodeBsdfPrincipled', loc=(250, 0))
    bs.inputs['Base Color'].default_value = (.035, .033, .032, 1); bs.inputs['Roughness'].default_value = .95
    lk(t, bs.outputs[0], out.inputs[0])
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1)
    for v in bm.verts:
        d = (v.co.x * 3.1) + (v.co.y * 2.3) + v.co.z * 1.7
        v.co *= 1 + .22 * math.sin(d * 3.0) * math.cos(d * 1.3) + .12 * math.sin(v.co.z * 7 + v.co.x * 5)
    me = bpy.data.meshes.new('boulder'); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    for i in range(n):
        ang = r.rand() * 2 * np.pi; rad = r.uniform(34, 62)
        x, y = rad * np.cos(ang), rad * np.sin(ang)
        if abs(x) < 16 and abs(y) < 40 or abs(y) < 16 and abs(x) < 40: continue
        z = float(terrain_h(np.array([x]), np.array([y]))[0])
        ob = bpy.data.objects.new('boulder', me); scene.collection.objects.link(ob)
        s = r.uniform(.8, 3.2); ob.location = (x, y, z - .15 * s); ob.scale = (s * r.uniform(.8, 1.3), s * r.uniform(.8, 1.3), s * r.uniform(.5, .9))
        ob.rotation_euler = (0, 0, r.rand() * 6.28)


def build_scene():
    build_world()
    terr = build_terrain(); terr.data.materials.append(terrain_material())
    build_sea(); ground_fog(); boulders(46, 5)
    # 상점: 대지 위 셋 + 앞마당 넷(카메라는 남서쪽에서 북동을 본다)
    def Z(x, y): return float(terrain_h(np.array([x]), np.array([y]))[0])
    shops = [('도박소', (-6, 4)), ('유닛강화소', (-22, 3)), ('영원강화소', (4, 14)),                # 대지(십자 중심 부근)
             ('다른세계강화소', (-34, -24)), ('공격타입강화소', (-14, -34)), ('도움소', (-42, 10)), ('항해일지', (12, 24))]
    placed = []
    for name, (x, y) in shops:
        z = Z(x, y)
        yaw = math.atan2(-(y + 70), -(x + 70)) + math.pi * 1.5 if False else math.radians(35)
        import_shop(name, (x, y, z), yaw, 1.0)
        placed.append((x, y, z))
    # 횃불: 상점 앞 + 대지 가장자리
    for (x, y, z) in placed:
        for dx, dy in ((-4.2, -5.8), (4.6, -5.4)):
            torch((x + dx, y + dy, Z(x + dx, y + dy)), power=3800)
    for (x, y) in ((-11, -16), (-5, -24), (-30, -12), (-22, -32), (-30, 14), (-18, -42)):
        torch((x, y, Z(x, y)), power=4400, height=3.2)
    # 달빛(차가운 보조광): 낮은 해를 대신해 윗쪽에서 약하게
    sun = bpy.data.lights.new('moon', 'SUN'); sun.energy = .5; sun.color = (.45, .55, .95); sun.angle = math.radians(2)
    so = bpy.data.objects.new('moon', sun); scene.collection.objects.link(so); so.rotation_euler = (math.radians(58), 0, math.radians(215))
    # 해 진 뒤 남은 따뜻한 역광: 섬 뒤(북동)의 낮은 해 — 대지 윤곽에 붉은 테를 두른다
    dl = bpy.data.lights.new('dusk', 'SUN'); dl.energy = 2.4; dl.color = (1, .46, .16); dl.angle = math.radians(1.5)
    do = bpy.data.objects.new('dusk', dl); scene.collection.objects.link(do)
    el = math.radians(5.0); az = math.radians(SUN_AZ)
    dv = -Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
    do.rotation_euler = dv.to_track_quat('-Z', 'Y').to_euler()
    # 카메라: 남서쪽 낮은 시점에서 섬을 왼쪽·가운데에 놓고 오른쪽은 바다와 하늘로 비운다
    cam = bpy.data.cameras.new('cam'); cam.lens = 30; cam.sensor_width = 36
    co = bpy.data.objects.new('cam', cam); scene.collection.objects.link(co); scene.camera = co
    co.location = (CAM[0], CAM[1], CAM[2])
    d = Vector(TGT) - co.location
    co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam.shift_x = SHIFT_X; cam.shift_y = SHIFT_Y


SUN_AZ = 36
CAM = (-60, -80, 17.0); TGT = (-4, 4, 8.5); SHIFT_X = 0.20; SHIFT_Y = -0.01


def setup_render():
    scene.render.engine = 'CYCLES'
    cy = scene.cycles; cy.device = 'CPU'; cy.samples = SAMPLES
    try: cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
    except Exception: pass
    cy.volume_step_rate = 2.0; cy.volume_max_steps = 256; cy.volume_bounces = 1
    cy.max_bounces = 6; cy.transparent_max_bounces = 6; cy.sample_clamp_indirect = 6; cy.sample_clamp_direct = 14
    scene.render.resolution_x = RW; scene.render.resolution_y = RH; scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'; scene.render.image_settings.color_depth = '16'
    scene.view_settings.view_transform = 'AgX'
    try: scene.view_settings.look = 'AgX - High Contrast'
    except Exception: pass
    scene.view_settings.exposure = float(os.environ.get('LOBBY_EXPOSURE', '0.3'))


build_scene(); setup_render()
if not os.environ.get('LOBBY_NO_RENDER'):
    scene.render.filepath = os.path.join(OUT, 'bg_raw_%d.png' % RW)
    bpy.ops.render.render(write_still=True)
    print('rendered', scene.render.filepath)
