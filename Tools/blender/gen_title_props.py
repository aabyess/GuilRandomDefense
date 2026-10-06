"""첫 화면 클릭 소품 그림(blender 세션 2026-10-06, PM 지시 「선술집 느낌 + 만지면 반응하는 소품」).
각 소품을 따로 투명 PNG + 그림자 PNG(반투명, 같은 크기·같은 자리)로. 배경(노을 역광 + 횃불 주황 + 달빛)과 같은 조명 문법으로 Cycles(GPU) 렌더.
blender -b --factory-startup --python Tools/blender/gen_title_props.py -- ~/GRD_title_props   (환경변수 PROPS="barrels,table" 로 일부만)
스케일: 캔버스 1024px = 3.41m → 300 px/m (모든 소품 같은 척도). 렌더 뒤 PIL로 (소품+그림자) 합집합 상자로 잘라 저장.
파츠: prop_barrels · prop_table · prop_mug_floor · prop_cat · prop_signpost + prop_signboard(흔들 판, 피벗 README) (+ _shadow). 입자 foam_particle.png(64)·drop.png(32)는 PIL."""
import os, sys
if len(sys.argv) > 1 and sys.argv[1] == 'post':          # ---- 후처리(시스템 파이썬): 합집합 상자로 자르고 그림자 만들기 + 입자 PNG
    import json, numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    OUT = os.path.expanduser(sys.argv[2] if len(sys.argv) > 2 else '~/GRD_title_props')
    for job in json.load(open(OUT + '/_jobs.json')):
        ims = [Image.open(p).convert('RGBA') for p in job['layers']]; arr = np.asarray(Image.open(job['shadow']).convert('RGBA'), np.float32)
        sh = np.zeros_like(arr); sh[..., 0] = 14; sh[..., 1] = 9; sh[..., 2] = 11; sh[..., 3] = np.clip(arr[..., 3] * .85, 0, 255); sh_im = Image.fromarray(sh.astype(np.uint8))
        bx = None
        for im in ims + [sh_im]:
            bb = Image.fromarray((np.asarray(im)[..., 3] > (22 if im is sh_im else 6)).astype(np.uint8) * 255).getbbox()
            if bb: bx = bb if bx is None else (min(bx[0], bb[0]), min(bx[1], bb[1]), max(bx[2], bb[2]), max(bx[3], bb[3]))
        bx = (max(0, bx[0] - 6), max(0, bx[1] - 6), min(1024, bx[2] + 6), min(1024, bx[3] + 6))
        for im, nm in zip(ims, job['out']): im.crop(bx).save(f"{OUT}/{nm}.png")
        sh_im.crop(bx).save(f"{OUT}/{job['out'][0]}_shadow.png"); print(job['name'], bx, '크기', bx[2] - bx[0], bx[3] - bx[1])
    # 입자: 거품 방울 64px(부드러운 크림색 원 + 하이라이트) · 맥주 방울 32px(호박색 물방울)
    S = 256; im = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.ellipse((16, 16, S - 16, S - 16), fill=(238, 226, 196, 255)); d.ellipse((30, 30, S - 60, S - 60), fill=(250, 244, 226, 255)); d.ellipse((60, 52, 120, 100), fill=(255, 255, 250, 255))
    im = im.filter(ImageFilter.GaussianBlur(3)); im.resize((64, 64), Image.LANCZOS).save(OUT + '/foam_particle.png')
    S = 256; im = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.polygon([(S / 2, 8), (S / 2 + 70, 140), (S / 2 - 70, 140)], fill=(214, 120, 22, 255)); d.ellipse((S / 2 - 82, 100, S / 2 + 82, 232), fill=(214, 120, 22, 255))
    d.ellipse((S / 2 - 60, 120, S / 2 - 20, 170), fill=(255, 205, 110, 255)); d.ellipse((S / 2 - 52, 128, S / 2 - 38, 142), fill=(255, 250, 220, 255))
    im = im.filter(ImageFilter.GaussianBlur(2)); im.resize((32, 32), Image.LANCZOS).save(OUT + '/drop.png')
    print('post done'); sys.exit(0)
import bpy, bmesh, math
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if argv else '~/GRD_title_props'); os.makedirs(OUT + '/_raw', exist_ok=True)
PX = 1024; PPM = 300.0; SCALE_M = PX / PPM
rs = np.random.RandomState(9); TAU = math.tau
ONLY = os.environ.get('PROPS', 'barrels,table,mug_floor,cat,sign').split(',')
bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene

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

def wood_mat(seed, base=(.20, .10, .045), staves=0, rough=.75):
    m, t, b = newmat(f'원목{seed}')
    tc = nd(t, 'ShaderNodeTexCoord'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (9, 9, 1.3); mp.inputs['Location'].default_value = (seed * 3.1, seed * 1.7, 0); lk(t, tc.outputs['Object'], mp.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 6; nz.inputs['Detail'].default_value = 9; lk(t, mp.outputs[0], nz.inputs[0])
    wv = nd(t, 'ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.bands_direction = 'Z'; wv.inputs['Scale'].default_value = 28; wv.inputs['Distortion'].default_value = 5; lk(t, mp.outputs[0], wv.inputs[0])
    grain = MA(t, 'ADD', MA(t, 'MULTIPLY', nz.outputs['Fac'], .6), MA(t, 'MULTIPLY', wv.outputs['Color'], .4))
    col = ramp(t, grain, [(.2, (base[0] * .22, base[1] * .22, base[2] * .22, 1)), (.85, (*base, 1))])
    if staves:
        sp = nd(t, 'ShaderNodeSeparateXYZ'); lk(t, tc.outputs['Object'], sp.inputs[0])
        ang = MA(t, 'DIVIDE', MA(t, 'ARCTAN2', sp.outputs[1], sp.outputs[0]), TAU)
        k = MA(t, 'MULTIPLY', ang, staves); fl = MA(t, 'FLOOR', k); fr = MA(t, 'FRACT', k)
        wn = nd(t, 'ShaderNodeTexWhiteNoise', noise_dimensions='1D'); lk(t, fl, wn.inputs['W'])
        tone = MA(t, 'ADD', .75, MA(t, 'MULTIPLY', wn.outputs['Value'], .5))
        line = MA(t, 'SUBTRACT', 1, MA(t, 'MULTIPLY', MA(t, 'MAXIMUM', MA(t, 'SUBTRACT', .06, fr), MA(t, 'SUBTRACT', fr, .94)), 14), clamp=True)
        mx = nd(t, 'ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY'); mx.inputs[0].default_value = 1.0; lk(t, col, mx.inputs[6]); lk(t, MA(t, 'MULTIPLY', tone, line), mx.inputs[7]) if False else None
        # 곱하기: 색 × (톤·선) — 색 노드는 RGB라 Mix(Multiply) B에 회색값을 스칼라→RGB로
        g = nd(t, 'ShaderNodeCombineColor'); v_ = MA(t, 'MULTIPLY', tone, line)
        lk(t, v_, g.inputs[0]); lk(t, v_, g.inputs[1]); lk(t, v_, g.inputs[2]); lk(t, g.outputs[0], mx.inputs[7]); col = mx.outputs[2]
    lk(t, col, b.inputs['Base Color']); b.inputs['Roughness'].default_value = rough
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .6; bp.inputs['Distance'].default_value = .004; lk(t, grain, bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m
def iron_mat(rust=.7):
    m, t, b = newmat('쇠')
    tc = nd(t, 'ShaderNodeTexCoord'); nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 10; nz.inputs['Detail'].default_value = 8; lk(t, tc.outputs['Object'], nz.inputs[0])
    rf = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', nz.outputs['Fac'], .4, clamp=True), 2.0 * rust, clamp=True)
    mx = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, rf, mx.inputs[0]); mx.inputs[6].default_value = (.04, .04, .045, 1); mx.inputs[7].default_value = (.22, .085, .03, 1); lk(t, mx.outputs[2], b.inputs['Base Color'])
    lk(t, MA(t, 'SUBTRACT', 1, MA(t, 'MULTIPLY', rf, .7), clamp=True), b.inputs['Metallic']); lk(t, MA(t, 'ADD', .4, MA(t, 'MULTIPLY', rf, .4)), b.inputs['Roughness'])
    return m
def simple(name, col, rough=.6, emit=None, st=0, metallic=0, alpha=1, spec=None):
    m, t, b = newmat(name); b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metallic
    if emit: b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = st
    return m
def glow(name, col, st):
    m, t, b = newmat(name)
    for n in list(t.nodes):
        if n.type != 'OUTPUT_MATERIAL': t.nodes.remove(n)
    o = next(n for n in t.nodes if n.type == 'OUTPUT_MATERIAL'); e = nd(t, 'ShaderNodeEmission'); e.inputs[0].default_value = (*col, 1); e.inputs[1].default_value = st; lk(t, e.outputs[0], o.inputs[0]); return m
def foam_mat():
    m, t, b = newmat('거품'); b.inputs['Base Color'].default_value = (.95, .88, .70, 1); b.inputs['Roughness'].default_value = .55
    try: b.inputs['Subsurface Weight'].default_value = .5
    except Exception: pass
    tc = nd(t, 'ShaderNodeTexCoord'); vr = nd(t, 'ShaderNodeTexVoronoi'); vr.inputs['Scale'].default_value = 30; lk(t, tc.outputs['Object'], vr.inputs[0])
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .8; bp.inputs['Distance'].default_value = .01; lk(t, vr.outputs['Distance'], bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal']); return m
BEER = simple('맥주', (.55, .22, .02), .15, emit=(1, .45, .05), st=.5)
FOAM = foam_mat(); IRON = iron_mat(.6); IRON2 = iron_mat(.3)

ALL = []                                                     # 소품 오브젝트 전부(그룹 렌더용)
def reg(o, group): o['grp'] = group; ALL.append(o); return o
def mesh_ob(name, me, loc, mat, group, rot=(0, 0, 0), smooth=True, scale=(1, 1, 1)):
    if smooth: me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.location = loc; ob.rotation_euler = rot; ob.scale = scale
    if mat: me.materials.append(mat)
    return reg(ob, group)
def box(name, size, loc, mat, group, rot=(0, 0, 0), bevel=.01):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.scale(bm, vec=size, verts=bm.verts); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = mesh_ob(name, me, loc, mat, group, rot, smooth=False)
    if bevel: md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 3
    return ob
def cyl(name, r, h, loc, mat, group, rot=(0, 0, 0), seg=24, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return mesh_ob(name, me, loc, mat, group, rot)
def sph(name, r, loc, mat, group, scale=(1, 1, 1), sub=3, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return mesh_ob(name, me, loc, mat, group, rot, scale=scale)
def torus(name, R, r, loc, mat, group, rot=(0, 0, 0), seg=40):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=10, location=loc, rotation=rot); o = bpy.context.object; o.name = name
    bpy.ops.object.shade_smooth(); o.data.materials.append(mat); return reg(o, group)
def barrel_shell(name, R, Hh, bulge, loc, mat, group, rings=12, seg=40, rot=(0, 0, 0)):
    bm = bmesh.new(); vs = []
    for j in range(rings + 1):
        t = j / rings; r = R * (1 + bulge * (1 - (2 * t - 1) ** 2)); z = (t - .5) * Hh
        vs.append([bm.verts.new((r * math.cos(TAU * i / seg), r * math.sin(TAU * i / seg), z)) for i in range(seg)])
    for j in range(rings):
        for i in range(seg): bm.faces.new((vs[j][i], vs[j][(i + 1) % seg], vs[j + 1][(i + 1) % seg], vs[j + 1][i]))
    bm.faces.new(vs[0][::-1]); bm.faces.new(vs[-1])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return mesh_ob(name, me, loc, mat, group, rot)

def make_barrel(loc, R=.34, Hh=.74, seed=1, group='barrels', rot=(0, 0, 0), tap=False):
    wm = wood_mat(seed, (.30, .17, .08), staves=20)
    b = barrel_shell('barrel', R, Hh, .13, loc, wm, group, rot=rot)
    parts = [b]
    for t in (-.36, -.2, .2, .36):
        r = R * (1 + .13 * (1 - (2 * (t + .5)) ** 2)) + .004
        parts.append(torus('hoop', r, .016, (loc[0], loc[1], loc[2] + t * Hh), IRON, group, seg=48))
    return parts
def beer_drop(loc, group, s=1.0):
    d = sph('drop', .014 * s, loc, simple('방울', (.8, .4, .05), .02, emit=(1, .5, .08), st=1.2), group, scale=(1, 1, 1.5), sub=2); return d

def make_mug(loc, group, seed=3, rot=(0, 0, 0), foam=True, amount=1.0):
    r = .075; h = .17
    wm = wood_mat(seed, (.34, .2, .09), staves=14)
    parts = [cyl('mug', r, h, (loc[0], loc[1], loc[2] + h / 2), wm, group, seg=28, r2=r * 1.07)]
    for z in (.03, .14): parts.append(torus('mhoop', r * (1.0 + .07 * z / h) + .003, .006, (loc[0], loc[1], loc[2] + z), IRON, group, seg=28))
    parts.append(torus('handle', .045, .011, (loc[0] + r * 1.07 + .03, loc[1], loc[2] + h * .52), IRON2, group, rot=(math.radians(90), 0, 0), seg=28))
    if foam:
        parts.append(cyl('beer', r * .94, .01, (loc[0], loc[1], loc[2] + h - .01), BEER, group, seg=28))
        fo = sph('foam', r * .99, (loc[0], loc[1], loc[2] + h + .002), FOAM, group, scale=(1, 1, .5 * amount), sub=3); parts.append(fo)
        for k in range(5):
            a = rs.rand() * TAU; parts.append(sph('fb', r * rs.uniform(.22, .35), (loc[0] + math.cos(a) * r * .62, loc[1] + math.sin(a) * r * .62, loc[2] + h + .012), FOAM, group, scale=(1, 1, .8), sub=2))
    return parts

# ---------------------------------------------------------------- 소품 조립
def build_barrels():
    g = 'barrels'
    make_barrel((-.37, 0, .37), seed=1, group=g, tap=True); make_barrel((.37, .02, .37), seed=2, group=g, rot=(0, 0, .5))
    make_barrel((0, .1, 1.12), seed=3, group=g, rot=(0, 0, 1.0))
    # 맨 위 통 정면에 마개(수도꼭지) + 방울, 앞 왼쪽 통에도 쇠 마개
    tap = cyl('tap', .018, .12, (0, -.17, 1.05), IRON2, g, rot=(math.radians(90), 0, 0)); tap.rotation_euler = (math.radians(90), 0, 0)
    cyl('tapk', .03, .02, (0, -.24, 1.05), IRON2, g, rot=(math.radians(90), 0, 0), seg=10)
    beer_drop((0, -.235, .93), g, 1.3); beer_drop((0, -.235, .72), g, .8)
    # 바닥에 흐른 맥주 웅덩이
    pd = cyl('pud', .1, .004, (0, -.34, .004), simple('웅덩이', (.5, .2, .02), .05, emit=(.9, .4, .05), st=.4), g, seg=24); pd.scale = (1.5, .8, 1)
    # 앞의 작은 통(눕힌)
    make_barrel((.82, -.12, .21), R=.2, Hh=.42, seed=4, group=g, rot=(math.radians(90), 0, .4)) if False else None
def build_table():
    g = 'table'; wm = wood_mat(11, (.27, .15, .07)); legm = wood_mat(12, (.2, .11, .05))
    cyl('top', .62, .07, (0, 0, .72), wm, g, seg=40); cyl('rim', .625, .02, (0, 0, .70), IRON2, g, seg=40, r2=.625)
    for a in (0, TAU / 3, 2 * TAU / 3):
        box('leg', (.07, .07, .72), (math.cos(a + .6) * .36, math.sin(a + .6) * .36, .36), legm, g, rot=(0, 0, a), bevel=.008)
    cyl('ring', .26, .02, (0, 0, .22), legm, g, seg=24)
    mugs = [(-.22, -.06, 31), (.12, .2, 32), (.28, -.16, 33)]
    for (x, y, s) in mugs: make_mug((x, y, .755), g, s, amount=1.0 + .6 * (s % 2))
    cyl('plate', .17, .018, (-.1, .28, .76), simple('접시', (.55, .5, .42), .35), g, seg=28, r2=.15)
    bread = sph('bread', .09, (-.15, .27, .83), simple('빵', (.55, .32, .12), .85), g, scale=(1.25, .8, .62), sub=3, rot=(0, 0, .5))
    meat = sph('meat', .065, (-.04, .31, .82), simple('고기', (.42, .15, .06), .5), g, scale=(1.1, .9, .85), sub=3)
    cyl('bone', .012, .12, (.03, .335, .84), simple('뼈', (.85, .8, .7), .5), g, rot=(math.radians(90), 0, .4))
def build_mug_floor():
    g = 'mug_floor'; parts = make_mug((0, 0, .075), g, 41, rot=(0, 0, 0), foam=False)
    # 옆으로 눕힌다: 전부 부모 없이 y축으로 90° — 피벗 원점에서 회전
    for o in parts:
        o.location = Vector((o.location.x, o.location.y, o.location.z - .075)); 
    em = bpy.data.objects.new('mugroot', None); sc.collection.objects.link(em); em.rotation_euler = (0, math.radians(90), math.radians(-25)); em.location = (0, 0, .085)
    for o in parts: o.parent = em
    pd = cyl('spill', .16, .004, (-.17, -.1, .003), simple('엎질러진맥주', (.5, .2, .02), .05, emit=(.9, .4, .05), st=.35), g, seg=30); pd.scale = (1.7, .9, 1); pd.rotation_euler = (0, 0, .4)
    fo = sph('spillfoam', .08, (-.12, -.06, .012), FOAM, g, scale=(1.6, .9, .25), sub=3); fo.rotation_euler = (0, 0, .4)
    sph('foamb1', .035, (-.28, -.18, .012), FOAM, g, scale=(1, 1, .5), sub=2); sph('foamb2', .028, (-.06, -.05, .014), FOAM, g, scale=(1, 1, .5), sub=2)
def build_cat():
    g = 'cat'; m, t, b = newmat('고양이털')
    tc = nd(t, 'ShaderNodeTexCoord'); nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 7; nz.inputs['Detail'].default_value = 4; lk(t, tc.outputs['Object'], nz.inputs[0])
    wv = nd(t, 'ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.bands_direction = 'Y'; wv.inputs['Scale'].default_value = 22; wv.inputs['Distortion'].default_value = 2.5; lk(t, tc.outputs['Object'], wv.inputs[0])
    wv.inputs['Distortion'].default_value = 9; wv.inputs['Detail'].default_value = 4
    lk(t, ramp(t, MA(t, 'ADD', MA(t, 'MULTIPLY', wv.outputs['Fac'], .12), MA(t, 'MULTIPLY', nz.outputs['Fac'], .88)), [(.25, (.30, .13, .045, 1)), (.75, (.55, .29, .10, 1))]), b.inputs['Base Color']); b.inputs['Roughness'].default_value = .95
    try: b.inputs['Sheen Weight'].default_value = .6
    except Exception: pass
    bn = nd(t, 'ShaderNodeTexNoise'); bn.inputs['Scale'].default_value = 140; bump = nd(t, 'ShaderNodeBump'); bump.inputs['Strength'].default_value = .5; bump.inputs['Distance'].default_value = .004; lk(t, tc.outputs['Object'], bn.inputs[0]); lk(t, bn.outputs['Fac'], bump.inputs['Height']); lk(t, bump.outputs[0], b.inputs['Normal'])
    sph('body', .2, (0, 0, .13), m, g, scale=(1.35, 1.0, .72), sub=5); sph('haunch', .14, (-.12, -.07, .12), m, g, scale=(1.1, 1.0, .85), sub=4)
    sph('head', .105, (.22, -.09, .1), m, g, scale=(1.0, .95, .85), sub=4)
    for sx in (-1, 1):
        e = cyl('ear', .04, .07, (.235, -.09 + sx * .065, .185), m, g, seg=4, r2=.004); e.rotation_euler = (sx * .28, 0, .0)
    nose = sph('nose', .014, (.31, -.09, .09), simple('코', (.7, .35, .35), .4), g, sub=2)
    # 꼬리: 몸을 감싸는 곡선 관(베벨 커브)
    cu = bpy.data.curves.new('tail', 'CURVE'); cu.dimensions = '3D'; sp = cu.splines.new('POLY'); pts = [(-.18 + math.cos(math.radians(a_)) * .26, .06 - math.sin(math.radians(a_)) * .27, .05) for a_ in range(30, 170, 14)]
    sp.points.add(len(pts) - 1)
    for i_, (x_, y_, z_) in enumerate(pts): sp.points[i_].co = (x_, y_, z_, 1)
    cu.bevel_depth = .034; cu.bevel_resolution = 4; cu.use_fill_caps = True; to = bpy.data.objects.new('tail', cu); sc.collection.objects.link(to); cu.materials.append(m); reg(to, g)
    # 감은 눈(가는 선)
    for sx in (-1, 1): box('eye', (.03, .004, .004), (.285, -.09 + sx * .045, .125), simple('눈', (.03, .02, .01), .8), g, bevel=0)
def build_sign():
    gp, gb = 'signpost', 'signboard'; wm = wood_mat(51, (.24, .13, .06)); wd = wood_mat(52, (.30, .17, .07))
    box('post', (.14, .14, 2.3), (0, 0, 1.15), wm, gp, bevel=.012); box('base', (.24, .24, .1), (0, 0, .05), wm, gp, bevel=.012)
    box('arm', (.82, .09, .1), (.41, 0, 2.15), wm, gp, bevel=.01); box('brace', (.5, .07, .07), (.25, 0, 1.9), wm, gp, rot=(0, math.radians(-40), 0), bevel=.008)
    # 등불: 아래 팔에 매달림(쇠 틀 + 발광 유리 + 속 점광)
    box('arm2', (.5, .08, .08), (-.25, 0, 1.78), wm, gp, bevel=.008); lx = -.46
    box('lring', (.012, .012, .15), (lx, 0, 1.68), IRON, gp, bevel=0)
    box('lbase', (.17, .17, .025), (lx, 0, 1.52), IRON, gp, bevel=0); box('ltop', (.2, .2, .03), (lx, 0, 1.76) if False else (lx, 0, 1.66), IRON, gp, bevel=0)
    box('lglass', (.13, .13, .13), (lx, 0, 1.585), glow('등유리', (1, .62, .22), 18), gp, bevel=0)
    for dx in (-.075, .075):
        for dy in (-.075, .075): box('lbar', (.012, .012, .15), (lx + dx, dy, 1.585), IRON, gp, bevel=0)
    ld = bpy.data.lights.new('lanternL', 'POINT'); ld.color = (1, .6, .25); ld.energy = 90; ld.shadow_soft_size = .08; lo = bpy.data.objects.new('lanternL', ld); sc.collection.objects.link(lo); lo.location = (lx, -.1, 1.585)
    # 흔들 판 + 사슬 (피벗 = 팔 끝 아래 고리 위치 (.72, 0, 2.1))
    bx, bz = .72, 1.56
    for dx in (-.34, .34):
        for k in range(4): torus('chain', .018, .0055, (bx + dx, 0, 2.08 - k * .05), IRON, gb, rot=(0, math.radians(90) if k % 2 else 0, 0), seg=16).rotation_euler = (math.radians(90) if k % 2 else 0, 0, 0)
    box('board', (.96, .06, .42), (bx, 0, bz), wd, gb, bevel=.012)
    box('frame', (1.0, .075, .03), (bx, 0, bz + .22), wm, gb, bevel=.006); box('frame', (1.0, .075, .03), (bx, 0, bz - .22), wm, gb, bevel=.006)
    for sx in (-.5, .5): box('frame', (.03, .075, .45), (bx + sx, 0, bz), wm, gb, bevel=.006)
    bpy.ops.object.text_add(location=(bx, -.04, bz - .035), rotation=(math.radians(90), 0, 0)); tx = bpy.context.object; tx.data.body = 'Tavern'; tx.data.size = .2; tx.data.extrude = .012; tx.data.align_x = 'CENTER'
    tx.data.materials.append(simple('간판글씨', (.9, .65, .25), .35, metallic=.5)); reg(tx, gb); tx.name = 'text'
    for sx in (-.4, .4): torus('ringtop', .03, .008, (bx + sx * .85, 0, 2.1), IRON, gb, rot=(math.radians(90), 0, 0), seg=20)

BUILDERS = {'barrels': build_barrels, 'table': build_table, 'mug_floor': build_mug_floor, 'cat': build_cat, 'sign': build_sign}
LAYERS = {'barrels': ['barrels'], 'table': ['table'], 'mug_floor': ['mug_floor'], 'cat': ['cat'], 'sign': ['signpost', 'signboard']}
CENTER = {'barrels': (0, 0, .75), 'table': (0, 0, .4), 'mug_floor': (-.05, -.03, .08), 'cat': (0, 0, .12), 'sign': (.2, 0, 1.25)}

# ---------------------------------------------------------------- 렌더 설정(배경 조명 문법)
def setup():
    sc.render.engine = 'CYCLES'; cy = sc.cycles
    try:
        pr = bpy.context.preferences.addons['cycles'].preferences; pr.compute_device_type = 'METAL'; pr.get_devices()
        for d in pr.devices: d.use = True
        cy.device = 'GPU'
    except Exception: cy.device = 'CPU'
    cy.samples = int(os.environ.get('PROP_SAMPLES', '160'))
    try: cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
    except Exception: pass
    cy.max_bounces = 6; cy.sample_clamp_indirect = 6
    sc.render.resolution_x = sc.render.resolution_y = PX; sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'; sc.render.image_settings.color_depth = '8'
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = float(os.environ.get('PROP_EXPOSURE', '-0.2'))
    try: sc.view_settings.look = 'AgX - High Contrast'
    except Exception: pass
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.07, .075, .11, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .55
    # 해 질 녘 역광(배경 gen_lobby_art: 방향 az 36°, 고도 5°, 주황) — 카메라는 남서쪽에서 보므로 소품은 뒤에서 비친다
    dl = bpy.data.lights.new('dusk', 'SUN'); dl.energy = 3.2; dl.color = (1, .5, .2); dl.angle = math.radians(2.0)
    do = bpy.data.objects.new('dusk', dl); sc.collection.objects.link(do); el = math.radians(9); az = math.radians(36)
    do.rotation_euler = (-Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))).to_track_quat('-Z', 'Y').to_euler()
    mn = bpy.data.lights.new('moon', 'SUN'); mn.energy = .9; mn.color = (.5, .6, 1); mo = bpy.data.objects.new('moon', mn); sc.collection.objects.link(mo); mo.rotation_euler = (math.radians(58), 0, math.radians(215))
    # 횃불 주황: 카메라 왼쪽 앞에서 약하게(앞쪽 면이 완전히 검게 죽지 않게)
    tl = bpy.data.lights.new('torch', 'POINT'); tl.color = (1, .55, .2); tl.energy = 420; tl.shadow_soft_size = .3; to = bpy.data.objects.new('torch', tl); sc.collection.objects.link(to); to.location = (-1.6, -2.2, 1.3)
    tl2 = bpy.data.lights.new('torch2', 'POINT'); tl2.color = (1, .5, .18); tl2.energy = 260; tl2.shadow_soft_size = .3; to2 = bpy.data.objects.new('torch2', tl2); sc.collection.objects.link(to2); to2.location = (1.8, -1.6, .9)
    # 카메라: 남서쪽(−.6,−.8) 고도 24° 아래로 비스듬히, 정사영(소품끼리 같은 척도)
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = SCALE_M; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
    return co
cam = setup()
catcher = bpy.data.objects.new('ground', bpy.data.meshes.new('ground')); sc.collection.objects.link(catcher)
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12); bm.to_mesh(catcher.data); bm.free(); catcher.is_shadow_catcher = True
gm, gt, gb_ = newmat('땅'); gb_.inputs['Base Color'].default_value = (.12, .095, .07, 1); gb_.inputs['Roughness'].default_value = .95; catcher.data.materials.append(gm)

def aim(center):
    d = Vector((-.6, -.8, 0)).normalized(); el = math.radians(24)
    off = Vector((d.x * math.cos(el), d.y * math.cos(el), math.sin(el))) * 12
    cam.location = Vector(center) + off
    cam.rotation_euler = (Vector(center) - cam.location).to_track_quat('-Z', 'Y').to_euler()

def render_to(path, hide_pred, catcher_on):
    # 그림자 패스: 해 한 줄기만(고도 24°, 캔버스 안에 들어오게) — 횃불·달·등불 그림자는 지운다
    sun = bpy.data.objects['dusk']; saved = {}
    for nm in ('moon', 'torch', 'torch2', 'lanternL'):
        o_ = bpy.data.objects.get(nm)
        if o_: saved[nm] = o_.data.energy
    rot0 = sun.rotation_euler.copy()
    if catcher_on:
        for nm in saved: bpy.data.objects[nm].data.energy = 0
        el = math.radians(24); az = math.radians(36)
        sun.rotation_euler = (-Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))).to_track_quat('-Z', 'Y').to_euler()
    for o in ALL: o.visible_camera = not hide_pred(o)
    catcher.is_shadow_catcher = catcher_on; catcher.visible_camera = catcher_on; catcher.visible_shadow = False
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    for nm, e in saved.items(): bpy.data.objects[nm].data.energy = e
    sun.rotation_euler = rot0

LOG = []
for name in ONLY:
    for o in list(ALL):
        bpy.data.objects.remove(o, do_unlink=True)
    ALL.clear()
    BUILDERS[name](); bpy.context.view_layer.update()
    aim(CENTER[name]); layers = LAYERS[name]; paths = []
    for ly in layers:
        p = f'{OUT}/_raw/{name}_{ly}.png'; render_to(p, lambda o, ly=ly: o['grp'] != ly, False); paths.append(p)
    sp = f'{OUT}/_raw/{name}_shadow.png'; render_to(sp, lambda o: True, True)
    names = {'barrels': ['prop_barrels'], 'table': ['prop_table'], 'mug_floor': ['prop_mug_floor'], 'cat': ['prop_cat'], 'sign': ['prop_signpost', 'prop_signboard']}[name]
    LOG.append({'name': name, 'layers': paths, 'shadow': sp, 'out': names}); print('PROP', name, flush=True)
import json
old = json.load(open(OUT + '/_jobs.json')) if os.path.exists(OUT + '/_jobs.json') else []
old = [j for j in old if j['name'] not in [l['name'] for l in LOG]] + LOG
json.dump(old, open(OUT + '/_jobs.json', 'w'))
