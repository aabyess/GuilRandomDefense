"""새 첫 화면 — 선술집 실내 장면(blender 세션 2026-10-06, 사장님 「첫 화면 디자인 자체를 선술집처럼」).
카메라 고정 1920×1080. 손님·바텐더 = 우리 유닛 스킨(Assets/Art/Units/<이름>/<이름>.fbx, **읽기만**) — 바꾸려면 아래 CAST 의 이름만 고쳐 다시 렌더.
장면: 나무 들보 천장·샹들리에·벽 등불 · 벽난로(불꽃·튀는 불씨) · 바 카운터 + 뒤 선반 술병·오크통 · 탁자 3 + 의자 · 창밖 밤 · 바닥 톱밥·엎지른 맥주.
빈 자리(UI가 얹음): 왼쪽 위 = 제목 간판 · 오른쪽 = 메뉴 칠판 · 앞쪽 아래 = 클릭 소품.
사용:  blender -b --factory-startup --python Tools/blender/gen_tavern_title.py -- <출력> <가로> <세로> <샘플>   환경변수 TT_FRAMES="1" 또는 "1-240" 또는 "5,60"
모든 움직임은 N=240프레임(10초 · 24fps) 주기: 포즈는 8프레임마다 키(Bezier)·불꽃·촛불·샹들리에는 drivers(정수 사이클).
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix, Quaternion

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if len(argv) > 0 else '~/GRD_tavern_title')
RW = int(argv[1]) if len(argv) > 1 else 1920
RH = int(argv[2]) if len(argv) > 2 else 1080
SAMPLES = int(argv[3]) if len(argv) > 3 else 96
os.makedirs(OUT + '/raw', exist_ok=True)
N = 240; TAU = math.tau; rs = np.random.RandomState(1007)
ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
UNITS = os.path.join(ROOT, 'Assets', 'Art', 'Units')

# ===== 사장님이 바꿀 수 있는 목록: (역할, 유닛 폴더 이름, 위치 x, y, 바라보는 방향 yaw°(0=카메라 쪽 −Y, 90=−X쪽), 의자/서 있음) =====
CAST = [
    ('wipe',  '특별함_조세민',     (-3.4, 5.85), 0,    'stand'),   # 바텐더: 서서 잔 닦기
    ('toast', '랜덤_손오공',       (0.2, 3.0), 0,    'sit'),     # 탁자1 뒤 — 건배
    ('toast', '랜덤_카마도_탄지로', (-.75, 2.2), 90,   'sit'),     # 탁자1 왼쪽 — 건배
    ('toast', '랜덤_미도리야_이즈쿠', (1.15, 2.2), -90, 'sit'),     # 탁자1 오른쪽 — 건배
    ('sleep', '다른세계_고죠_사토루', (1.1, 3.8), 90,  'sit'),     # 탁자2 — 엎드려 졸기
    ('laugh', '랜덤_한마_바키',     (2.9, 3.8), -90,  'sit'),     # 탁자2 — 크게 웃기
    ('lute',  '랜덤_리바이_아커만', (1.1, 0.6), -25,  'sit'),     # 류트 연주
    ('toast', '초월_최상호_AD',    (-2.25, 0.35), 14, 'sit'),     # 사장님 지정(구일) — 앞 탁자, 건배
    ('laugh', '초월_최상호_AP',    (-1.35, 0.35), -14, 'sit'),    # 사장님 지정(바지사장) — 앞 탁자, 웃음
]
TABLES = [(.2, 2.2), (2.0, 3.8), (-1.8, -0.4)]

bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'BEZIER'

# ------------------------------------------------------------------ 노드·재질·메시 도우미
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
def simple(name, col, rough=.7, emit=None, st=0, metallic=0):
    m, t, b = newmat(name); b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metallic
    if emit: b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = st
    return m
def glow(name, col, st):
    m, t, b = newmat(name)
    for n in list(t.nodes):
        if n.type != 'OUTPUT_MATERIAL': t.nodes.remove(n)
    o = next(n for n in t.nodes if n.type == 'OUTPUT_MATERIAL'); e = nd(t, 'ShaderNodeEmission'); e.inputs[0].default_value = (*col, 1); e.inputs[1].default_value = st; lk(t, e.outputs[0], o.inputs[0]); return m
def wood_mat(seed, base=(.20, .10, .045), rough=.72, staves=0, scale=(9, 9, 1.3)):
    m, t, b = newmat(f'원목{seed}')
    tc = nd(t, 'ShaderNodeTexCoord'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = scale; mp.inputs['Location'].default_value = (seed * 3.1, seed * 1.7, 0); lk(t, tc.outputs['Object'], mp.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 6; nz.inputs['Detail'].default_value = 9; lk(t, mp.outputs[0], nz.inputs[0])
    wv = nd(t, 'ShaderNodeTexWave'); wv.wave_type = 'BANDS'; wv.bands_direction = 'Z'; wv.inputs['Scale'].default_value = 24; wv.inputs['Distortion'].default_value = 5; lk(t, mp.outputs[0], wv.inputs[0])
    grain = MA(t, 'ADD', MA(t, 'MULTIPLY', nz.outputs['Fac'], .6), MA(t, 'MULTIPLY', wv.outputs['Color'], .4))
    col = ramp(t, grain, [(.2, (base[0] * .2, base[1] * .2, base[2] * .2, 1)), (.85, (*base, 1))])
    if staves:
        sp = nd(t, 'ShaderNodeSeparateXYZ'); lk(t, tc.outputs['Object'], sp.inputs[0])
        ang = MA(t, 'DIVIDE', MA(t, 'ARCTAN2', sp.outputs[1], sp.outputs[0]), TAU); k = MA(t, 'MULTIPLY', ang, staves); fl = MA(t, 'FLOOR', k); fr = MA(t, 'FRACT', k)
        wn = nd(t, 'ShaderNodeTexWhiteNoise', noise_dimensions='1D'); lk(t, fl, wn.inputs['W'])
        tone = MA(t, 'ADD', .72, MA(t, 'MULTIPLY', wn.outputs['Value'], .5))
        line = MA(t, 'SUBTRACT', 1, MA(t, 'MULTIPLY', MA(t, 'MAXIMUM', MA(t, 'SUBTRACT', .06, fr), MA(t, 'SUBTRACT', fr, .94)), 14), clamp=True)
        v_ = MA(t, 'MULTIPLY', tone, line); g = nd(t, 'ShaderNodeCombineColor'); [lk(t, v_, g.inputs[i]) for i in range(3)]
        mx = nd(t, 'ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY'); mx.inputs[0].default_value = 1.0; lk(t, col, mx.inputs[6]); lk(t, g.outputs[0], mx.inputs[7]); col = mx.outputs[2]
    lk(t, col, b.inputs['Base Color']); b.inputs['Roughness'].default_value = rough
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .6; bp.inputs['Distance'].default_value = .004; lk(t, grain, bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m
def iron_mat():
    m, t, b = newmat('쇠'); b.inputs['Base Color'].default_value = (.05, .05, .055, 1); b.inputs['Metallic'].default_value = .85; b.inputs['Roughness'].default_value = .45; return m
def brick_floor():
    m, t, b = newmat('마루')
    tc = nd(t, 'ShaderNodeTexCoord'); sp = nd(t, 'ShaderNodeSeparateXYZ'); cm = nd(t, 'ShaderNodeCombineXYZ'); lk(t, tc.outputs['Object'], sp.inputs[0]); lk(t, sp.outputs[0], cm.inputs[0]); lk(t, sp.outputs[1], cm.inputs[1])
    br = nd(t, 'ShaderNodeTexBrick'); br.inputs['Scale'].default_value = 1.0; br.inputs['Brick Width'].default_value = 1.8; br.inputs['Row Height'].default_value = .24; br.inputs['Mortar Size'].default_value = .012
    br.inputs['Color1'].default_value = (.12, .062, .03, 1); br.inputs['Color2'].default_value = (.075, .038, .018, 1); br.inputs['Mortar'].default_value = (.01, .006, .003, 1); br.inputs['Bias'].default_value = .2; lk(t, cm.outputs[0], br.inputs[0])
    nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 5; nz.inputs['Detail'].default_value = 8; lk(t, tc.outputs['Object'], nz.inputs[0])
    fine = nd(t, 'ShaderNodeTexNoise'); fine.inputs['Scale'].default_value = 120; lk(t, tc.outputs['Object'], fine.inputs[0])
    # 톱밥: 고운 소음의 밝은 점 · 맥주 자국: 큰 소음의 어두운 얼룩
    saw = MA(t, 'MULTIPLY', MA(t, 'GREATER_THAN', fine.outputs['Fac'], .76), .45)
    stain = MA(t, 'MULTIPLY', MA(t, 'GREATER_THAN', nz.outputs['Fac'], .63), .35)
    mx = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, saw, mx.inputs[0]); lk(t, br.outputs['Color'], mx.inputs[6]); mx.inputs[7].default_value = (.62, .46, .26, 1)
    mx2 = nd(t, 'ShaderNodeMix', data_type='RGBA'); lk(t, stain, mx2.inputs[0]); lk(t, mx.outputs[2], mx2.inputs[6]); mx2.inputs[7].default_value = (.02, .012, .006, 1)
    lk(t, mx2.outputs[2], b.inputs['Base Color']); lk(t, MA(t, 'SUBTRACT', .75, MA(t, 'MULTIPLY', stain, .45)), b.inputs['Roughness'])
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .6; bp.inputs['Distance'].default_value = .01; lk(t, MA(t, 'ADD', br.outputs['Fac'], MA(t, 'MULTIPLY', fine.outputs['Fac'], .3)), bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal']); return m
def plaster_mat(name='회벽', col=(.45, .31, .19)):
    m, t, b = newmat(name); tc = nd(t, 'ShaderNodeTexCoord'); nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3; nz.inputs['Detail'].default_value = 10; lk(t, tc.outputs['Object'], nz.inputs[0])
    lk(t, ramp(t, nz.outputs['Fac'], [(.3, (col[0] * .55, col[1] * .55, col[2] * .55, 1)), (.75, (*col, 1))]), b.inputs['Base Color']); b.inputs['Roughness'].default_value = .95
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .5; bp.inputs['Distance'].default_value = .02; lk(t, nz.outputs['Fac'], bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal']); return m
def stone_mat(name='벽난로돌', col=(.19, .17, .16)):
    m, t, b = newmat(name); tc = nd(t, 'ShaderNodeTexCoord'); sp = nd(t, 'ShaderNodeSeparateXYZ'); cm = nd(t, 'ShaderNodeCombineXYZ'); lk(t, tc.outputs['Object'], sp.inputs[0]); lk(t, sp.outputs[0], cm.inputs[0]); lk(t, sp.outputs[2], cm.inputs[1])
    br = nd(t, 'ShaderNodeTexBrick'); br.inputs['Scale'].default_value = 3.0; br.inputs['Mortar Size'].default_value = .04; br.inputs['Color1'].default_value = (*col, 1); br.inputs['Color2'].default_value = (col[0] * .55, col[1] * .55, col[2] * .6, 1); br.inputs['Mortar'].default_value = (.04, .035, .033, 1); lk(t, cm.outputs[0], br.inputs[0])
    lk(t, br.outputs['Color'], b.inputs['Base Color']); b.inputs['Roughness'].default_value = .9
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = 1.0; bp.inputs['Distance'].default_value = .03; lk(t, br.outputs['Fac'], bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal']); return m

def mesh_ob(name, me, loc, mat, rot=(0, 0, 0), scale=(1, 1, 1), smooth=True):
    if smooth: me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.location = loc; ob.rotation_euler = rot; ob.scale = scale
    if mat: me.materials.append(mat)
    return ob
def box(name, size, loc, mat, rot=(0, 0, 0), bevel=.012):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.scale(bm, vec=size, verts=bm.verts); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = mesh_ob(name, me, loc, mat, rot, smooth=False)
    if bevel: md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 2
    return ob
def cyl(name, r, h, loc, mat, rot=(0, 0, 0), seg=24, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return mesh_ob(name, me, loc, mat, rot)
def sph(name, r, loc, mat, scale=(1, 1, 1), sub=3, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return mesh_ob(name, me, loc, mat, rot, scale)
def torus(name, R, r, loc, mat, rot=(0, 0, 0), seg=40):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=10, location=loc, rotation=rot); o = bpy.context.object; o.name = name
    bpy.ops.object.shade_smooth(); o.data.materials.append(mat); return o
def barrel(name, R, Hh, loc, mat, bulge=.13, rot=(0, 0, 0), rings=10, seg=32):
    bm = bmesh.new(); vs = []
    for j in range(rings + 1):
        t = j / rings; r = R * (1 + bulge * (1 - (2 * t - 1) ** 2)); z = (t - .5) * Hh
        vs.append([bm.verts.new((r * math.cos(TAU * i / seg), r * math.sin(TAU * i / seg), z)) for i in range(seg)])
    for j in range(rings):
        for i in range(seg): bm.faces.new((vs[j][i], vs[j][(i + 1) % seg], vs[j + 1][(i + 1) % seg], vs[j + 1][i]))
    bm.faces.new(vs[0][::-1]); bm.faces.new(vs[-1]); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return mesh_ob(name, me, loc, mat, rot)
def drv(idata, path, idx, expr):
    fc = idata.driver_add(path, idx) if idx is not None and idx >= 0 else idata.driver_add(path)
    fc.driver.type = 'SCRIPTED'; fc.driver.expression = expr; return fc
def sine(base, amp, k, ph=0): return f'{base}+{amp}*sin(2*pi*frame/{N}*{k}+{ph})'
def flick(base, amp1, k1, p1, amp2, k2, p2): return f'{base}*(1+{amp1}*sin(2*pi*frame/{N}*{k1}+{p1})+{amp2}*sin(2*pi*frame/{N}*{k2}+{p2}))'

IRON = iron_mat(); IRON2 = iron_mat()
WOOD_D = wood_mat(1, (.16, .085, .04)); WOOD_M = wood_mat(2, (.24, .13, .06)); WOOD_L = wood_mat(3, (.32, .19, .09)); BEAM = wood_mat(4, (.10, .05, .025), scale=(3, 3, 3))
FOAM = simple('거품', (.95, .88, .70), .55); BEERM = simple('맥주', (.55, .22, .02), .15, emit=(1, .45, .05), st=.4)
FLAME = glow('불꽃', (1, .4, .07), 6); FLAME2 = glow('불꽃속', (1, .7, .2), 9); EMBER = glow('불씨', (1, .3, .06), 7); CAND = glow('촛불', (1, .65, .2), 30)

# ------------------------------------------------------------------ 방
RX0, RX1, RY0, RY1, H = -7.0, 7.0, -8.6, 6.5, 3.6
PL = plaster_mat(); PLD = plaster_mat('아래판자', (.26, .15, .075))
def build_room():
    fl = box('floor', (RX1 - RX0, RY1 - RY0, .1), ((RX0 + RX1) / 2, (RY0 + RY1) / 2, -.05), brick_floor(), bevel=0)
    box('ceil', (RX1 - RX0, RY1 - RY0, .1), ((RX0 + RX1) / 2, (RY0 + RY1) / 2, H + .05), wood_mat(5, (.10, .055, .03)), bevel=0)
    box('wall_back', (RX1 - RX0, .2, H), (0, RY1 + .1, H / 2), PL, bevel=0); box('wall_front', (RX1 - RX0, .2, H), (0, RY0 - .1, H / 2), PL, bevel=0)
    box('wall_L', (.2, RY1 - RY0, H), (RX0 - .1, (RY0 + RY1) / 2, H / 2), PL, bevel=0); box('wall_R', (.2, RY1 - RY0, H), (RX1 + .1, (RY0 + RY1) / 2, H / 2), PL, bevel=0)
    for (nm, lx, ly, sx, sy) in (('pb', 0, RY1 - .05, RX1 - RX0, .12), ('pf', 0, RY0 + .05, RX1 - RX0, .12), ('pl', RX0 + .05, (RY0 + RY1) / 2, .12, RY1 - RY0), ('pr', RX1 - .05, (RY0 + RY1) / 2, .12, RY1 - RY0)):   # 아래 판자
        box(nm, (sx, sy, 1.1), (lx, ly, .55), PLD, bevel=.01)
    # 들보(천장 가로) + 기둥·벽 보
    for y in np.arange(RY0 + 1.0, RY1, 1.9): box('beam', (RX1 - RX0, .32, .36), (0, y, H - .18), BEAM, bevel=.02)
    for x in (-5.5, -2.0, 1.5, 5.0): box('rafter', (.22, RY1 - RY0, .22), (x, (RY0 + RY1) / 2, H - .4), BEAM, bevel=.015)
    for x in (-6.9, -2.4, 1.0, 6.9) if False else ():
        pass
    for x in (-6.8, -3.2, 0.2, 3.6, 6.8): box('post', (.3, .3, H), (x, RY1 - .15, H / 2), BEAM, bevel=.02)                       # 뒤 벽 기둥
    for z in (2.4,): box('hbeam', (RX1 - RX0, .2, .22), (0, RY1 - .12, z + .8), BEAM, bevel=.015)
    for y in (-6.0, -2.0, 2.0, 5.8): box('postL', (.3, .3, H), (RX0 + .15, y, H / 2), BEAM, bevel=.02); box('postR', (.3, .3, H), (RX1 - .15, y, H / 2), BEAM, bevel=.02)
    # 창(왼쪽 벽): 밤하늘 + 창틀
    sky = glow('밤하늘', (.05, .09, .22), 1.8); box('winsky', (.05, 2.4, 1.5), (RX0 - .15, 1.5, 1.95), sky, bevel=0)
    for dy in (-1.2, 0, 1.2): box('winbar', (.1, .08, 1.5), (RX0 + .02, 1.5 + dy, 1.95), WOOD_D, bevel=.006)
    box('wint', (.12, 2.5, .1), (RX0 + .02, 1.5, 2.72), WOOD_D); box('winb', (.12, 2.5, .1), (RX0 + .02, 1.5, 1.2), WOOD_D); box('wsill', (.3, 2.6, .07), (RX0 + .1, 1.5, 1.16), WOOD_M)
    # 달빛 면광(창 밖)
    ld = bpy.data.lights.new('moon', 'AREA'); ld.shape = 'RECTANGLE'; ld.size = 2.4; ld.size_y = 1.5; ld.energy = 700; ld.color = (.45, .58, 1); lo = bpy.data.objects.new('moon', ld); sc.collection.objects.link(lo); lo.location = (RX0 - .4, 1.5, 2.0); lo.rotation_euler = (0, math.radians(90), 0)
build_room()

# ------------------------------------------------------------------ 바 카운터·선반·오크통
def build_bar():
    top = wood_mat(6, (.30, .17, .08)); front = wood_mat(7, (.20, .105, .05))
    box('bar_body', (5.6, .7, 1.05), (-3.5, 5.1, .525), front, bevel=.02); box('bar_top', (5.9, .95, .09), (-3.5, 5.12, 1.09), top, bevel=.02)
    for x in np.arange(-6.0, -0.8, .9): box('bar_pan', (.8, .03, .85), (x, 4.74, .52), wood_mat(int(abs(x) * 7) + 10, (.14, .07, .035)), bevel=.01)   # 앞 널판 장식
    cyl('rail', .028, 5.4, (-3.5, 4.64, .2), simple('황동', (.7, .5, .16), .3, metallic=.9), rot=(0, math.radians(90), 0), seg=12)
    # 뒤 선반 3단 + 병·잔
    for z in (1.5, 2.0, 2.5): box('shelf', (5.6, .32, .05), (-3.5, 6.33, z), WOOD_M, bevel=.01)
    cols = [(.05, .18, .07), (.25, .12, .03), (.12, .04, .02), (.05, .12, .22), (.18, .04, .06), (.3, .26, .1)]
    for zi, z in enumerate((1.5, 2.0, 2.5)):
        for x in np.arange(-6.1, -0.9, .21):
            if rs.rand() < .18: continue
            c = cols[rs.randint(len(cols))]; h = rs.uniform(.26, .38); bm = simple('병', c, .12, emit=tuple(v * .5 for v in c), st=.35)
            cyl('bottle', .045, h, (x + rs.uniform(-.03, .03), 6.3, z + .025 + h / 2), bm, seg=12); cyl('neck', .017, .12, (x, 6.3, z + .025 + h + .05), bm, seg=8)
    # 뒤 오른쪽 오크통 벽(가로로 눕힌 통 6)
    for i, (x, z) in enumerate(((-1.2, .38), (-.2, .38), (.8, .38), (-.7, 1.05), (.3, 1.05), (-.2, 1.72))):
        b = barrel('keg', .42, .85, (x, 6.0, z), wood_mat(20 + i, (.28, .16, .075), staves=22), rot=(math.radians(90), 0, 0))
        torus('khoop', .5, .02, (x, 6.0 - .26, z), IRON, rot=(math.radians(90), 0, 0)); torus('khoop2', .5, .02, (x, 6.0 + .26, z), IRON, rot=(math.radians(90), 0, 0))
        cyl('tap', .02, .14, (x, 5.52, z - .05), IRON2, rot=(math.radians(90), 0, 0), seg=8)
build_bar()

# ------------------------------------------------------------------ 벽난로
FIRE_LIGHTS = []
def build_fireplace():
    st = stone_mat(); cx = 2.4; y0 = RY1 - .25
    box('hpL', (.7, .9, 2.4), (cx - 1.15, y0 - .2, 1.2), st, bevel=.02); box('hpR', (.7, .9, 2.4), (cx + 1.15, y0 - .2, 1.2), st, bevel=.02)
    box('hpT', (3.0, .9, .7), (cx, y0 - .2, 2.45), st, bevel=.02); box('hback', (1.7, .2, 2.2), (cx, RY1 - .1, 1.1), simple('그을음', (.015, .012, .01), .95), bevel=0)
    box('mantle', (3.4, 1.05, .14), (cx, y0 - .25, 2.85), WOOD_D, bevel=.02); box('chimney', (2.4, .8, H - 2.85), (cx, y0 - .1, 2.85 + (H - 2.85) / 2), st, bevel=.02)
    box('hearth', (3.2, 1.3, .12), (cx, y0 - .55, .06), st, bevel=.02)
    for i, (dx, r) in enumerate(((-.3, .13), (.25, .12), (0, .11))):
        cyl('log', r, 1.0, (cx + dx * 1.3, RY1 - .55, .2 + i * .02), simple('장작', (.06, .03, .015), .9), rot=(0, math.radians(90), math.radians(8 * (i - 1))), seg=12)
    emb = sph('embers', .5, (cx, RY1 - .55, .1), EMBER, scale=(1.1, .5, .12), sub=3)
    drv(EMBER.node_tree.nodes['Emission'].inputs[1], 'default_value', None, flick(7, .25, 7, 0, .15, 17, 1))
    # 불꽃 7가닥
    for i in range(7):
        a = (i - 3) * .17; h = .85 - abs(i - 3) * .12
        fl = sph('flame', .12, (cx + a * 1.3, RY1 - .55 + .02 * (i % 2), .3 + h * .3), FLAME if i % 2 == 0 else FLAME2, scale=(.55, .45, h * 2.6), sub=2)
        ph = i * 1.3
        drv(fl, 'scale', 2, sine(h * 2.6, h * .6, 6 + i % 3, ph)); drv(fl, 'scale', 0, sine(.55, .12, 9, ph + 1)); drv(fl, 'location', 0, sine(cx + a * 1.3, .05, 5 + i % 2, ph)); drv(fl, 'rotation_euler', 1, sine(0, .18, 4 + i % 3, ph))
    for (e, (dx, dy, dz, pw)) in enumerate(((0, -.55, .55, 1500), (0, -1.5, 1.5, 900))):
        ld = bpy.data.lights.new(f'fire{e}', 'POINT'); ld.color = (1, .5, .17); ld.energy = pw; ld.shadow_soft_size = .35; lo = bpy.data.objects.new(f'fire{e}', ld); sc.collection.objects.link(lo); lo.location = (cx + dx, RY1 + dy, dz)
        drv(ld, 'energy', None, flick(pw, .16, 7, e, .1, 19, e * 2)); FIRE_LIGHTS.append(lo)
    # 튀는 불씨(키프레임 루프)
    bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
    for k in range(26):
        sp = sph('spark', rs.uniform(.012, .022), (cx, RY1 - .6, .4), EMBER, sub=1); ph0 = k / 26; ax = rs.uniform(-.55, .55); ay = rs.uniform(-.25, .1); sp0 = rs.uniform(.6, 1.1)
        for f in range(1, N + 2, 3):
            t = ((f - 1) / N * 1.0 + ph0 * sp0) % 1.0
            sp.location = (cx + ax * t * 1.6, RY1 - .6 + ay * t * 1.0 - .2 * t * t, .45 + 1.7 * t - .6 * t * t); s_ = .018 * (1 - t) ** .5 * 40 / 40; sp.scale = (1 - t * .8,) * 3
            sp.keyframe_insert('location', frame=f); sp.keyframe_insert('scale', frame=f)
    bpy.context.preferences.edit.keyframe_new_interpolation_type = 'BEZIER'
build_fireplace()

# ------------------------------------------------------------------ 샹들리에·벽 등불
def build_lights():
    # 샹들리에: 철 고리 + 초 8 + 사슬 3 — 천천히 흔들(driver)
    root = bpy.data.objects.new('chand', None); sc.collection.objects.link(root); root.location = (-0.5, 1.5, 3.25)
    parts = [torus('ring', .6, .03, (0, 0, -.6), IRON, seg=48)]
    for a in (0, TAU / 3, 2 * TAU / 3): parts.append(cyl('chain', .008, .62, (math.cos(a + .5) * .3, math.sin(a + .5) * .3, -.3), IRON, rot=(math.sin(a + .5) * .55, -math.cos(a + .5) * .55, 0), seg=6))
    for i in range(8):
        a = TAU * i / 8; x, y = math.cos(a) * .6, math.sin(a) * .6
        parts.append(cyl('candle', .028, .16, (x, y, -.5), simple('초', (.9, .85, .7), .6), seg=10)); fl = sph('cflame', .025, (x, y, -.4), CAND, scale=(.7, .7, 1.7), sub=1); parts.append(fl)
        drv(fl, 'scale', 2, sine(1.7, .35, 9 + i % 4, i * .9))
    for p in parts: p.parent = root
    drv(root, 'rotation_euler', 0, sine(0, .035, 1, 0)); drv(root, 'rotation_euler', 1, sine(0, .03, 1, 1.7))
    ld = bpy.data.lights.new('chandL', 'POINT'); ld.color = (1, .62, .25); ld.energy = 900; ld.shadow_soft_size = .3; lo = bpy.data.objects.new('chandL', ld); sc.collection.objects.link(lo); lo.parent = root; lo.location = (0, 0, -.45); drv(ld, 'energy', None, flick(900, .07, 11, 0, .05, 23, 1))
        # 앞 탁자(주인공) 위를 비추는 따뜻한 보조등
    ld = bpy.data.lights.new('hero', 'POINT'); ld.color = (1, .6, .25); ld.energy = 520; ld.shadow_soft_size = .4; lo = bpy.data.objects.new('hero', ld); sc.collection.objects.link(lo); lo.location = (-2.4, -1.8, 2.7); drv(ld, 'energy', None, flick(520, .06, 7, 3, .04, 17, 1))
    # 벽 등불 둘(왼쪽·바 뒤)
    for i, (x, y, z) in enumerate(((-6.85, -1.6, 2.1), (-1.2, 6.3, 2.9), (6.85, 1.2, 2.2))):
        box('lant', (.16, .16, .26), (x, y, z), IRON, bevel=.01); box('lglow', (.1, .1, .18), (x, y, z), CAND, bevel=0)
        ld = bpy.data.lights.new(f'wall{i}', 'POINT'); ld.color = (1, .6, .22); ld.energy = 260; ld.shadow_soft_size = .15; lo = bpy.data.objects.new(f'wall{i}', ld); sc.collection.objects.link(lo); lo.location = (x * .95, y * .97 if abs(y) > 6 else y, z); drv(ld, 'energy', None, flick(260, .09, 9 + i, i, .06, 21, 2))
build_lights()

# ------------------------------------------------------------------ 탁자·의자·잔
def make_mug(loc, seed, foam=True):
    r = .06; h = .13; ob = cyl('mug', r, h, (loc[0], loc[1], loc[2] + h / 2), wood_mat(seed, (.34, .2, .09), staves=14), seg=24, r2=r * 1.08)
    torus('mh', r * 1.04, .005, (loc[0], loc[1], loc[2] + .03), IRON, seg=24); torus('mh2', r * 1.07, .005, (loc[0], loc[1], loc[2] + .1), IRON, seg=24)
    torus('hd', .035, .009, (loc[0] + r * 1.1 + .02, loc[1], loc[2] + h * .52), IRON2, rot=(math.radians(90), 0, 0), seg=20)
    if foam: sph('foam', r * .98, (loc[0], loc[1], loc[2] + h + .002), FOAM, scale=(1, 1, .5), sub=3)
    return ob
def build_table(cx, cy, r=.62, seed=30, mugs=3):
    wm = wood_mat(seed, (.26, .14, .065))
    cyl('ttop', r, .07, (cx, cy, .76), wm, seg=40); cyl('trim', r + .005, .02, (cx, cy, .73), IRON2, seg=40)
    for a in (0, TAU / 3, 2 * TAU / 3): box('tleg', (.07, .07, .74), (cx + math.cos(a + .6) * .34, cy + math.sin(a + .6) * .34, .37), WOOD_D, rot=(0, 0, a), bevel=.008)
    for i in range(mugs):
        a = TAU * i / mugs + .5; make_mug((cx + math.cos(a) * .3, cy + math.sin(a) * .3, .795), seed + i)
def build_stool(x, y, seed=40): cyl('stool', .2, .05, (x, y, .46), WOOD_M, seg=20); [box('sleg', (.045, .045, .45), (x + dx, y + dy, .22), WOOD_D, bevel=.005) for dx, dy in ((.11, .11), (-.11, .11), (.11, -.11), (-.11, -.11))]
for (cx, cy) in TABLES: build_table(cx, cy, seed=int(30 + abs(cx) * 3))
for (c) in CAST:
    if c[4] == 'sit': build_stool(c[2][0], c[2][1])

# ------------------------------------------------------------------ 손님(우리 유닛 스킨)
def load_unit(unit):
    f = os.path.join(UNITS, unit, unit + '.fbx'); before = set(bpy.data.objects); acts = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=f)
    new = [o for o in bpy.data.objects if o not in before]; arm = next(o for o in new if o.type == 'ARMATURE'); meshes = [o for o in new if o.type == 'MESH']
    for a in [a for a in bpy.data.actions if a not in acts]: bpy.data.actions.remove(a)
    if arm.animation_data: arm.animation_data_clear()
    for m in meshes:                                               # 반짝임 줄이고 거친 느낌(실내 촛불빛)
        for ms in m.material_slots:
            if ms.material and ms.material.use_nodes:
                for n in ms.material.node_tree.nodes:
                    if n.type == 'BSDF_PRINCIPLED':
                        n.inputs['Roughness'].default_value = max(.62, n.inputs['Roughness'].default_value)
                        try: n.inputs['Specular IOR Level'].default_value = .2
                        except Exception: pass
    return arm, meshes
def bone_world(arm, name, tail=False):
    pb = arm.pose.bones[name]; return arm.matrix_world @ (pb.tail if tail else pb.head)
def mx(n): return 'mixamorig:' + n
CHILD = {'Hips': ['Spine'], 'Spine': ['Spine1'], 'Spine1': ['Spine2', 'Neck'], 'Spine2': ['Neck'], 'Neck': ['Head'], 'LeftShoulder': ['LeftArm'], 'RightShoulder': ['RightArm'],
         'LeftArm': ['LeftForeArm'], 'LeftForeArm': ['LeftHand'], 'RightArm': ['RightForeArm'], 'RightForeArm': ['RightHand'],
         'LeftUpLeg': ['LeftLeg'], 'LeftLeg': ['LeftFoot'], 'RightUpLeg': ['RightLeg'], 'RightLeg': ['RightFoot']}
def aim(arm, bname, dir_world):
    """뼈 → 자식 관절 벡터가 월드 방향 dir_world를 보게 포즈 회전(뼈 자체 축 방향이 제각각인 리그라 관절 벡터로 잰다)."""
    short = bname.split(':')[-1]; pb = arm.pose.bones.get(bname)
    if pb is None: return
    ch = None
    for c in CHILD.get(short, []):
        if mx(c) in arm.pose.bones: ch = arm.pose.bones[mx(c)]; break
    if ch is None: return
    inv = arm.matrix_world.to_3x3().inverted(); d = (inv @ Vector(dir_world)).normalized()
    cur = (ch.matrix.translation - pb.matrix.translation).normalized(); rot = cur.rotation_difference(d)
    m3 = rot.to_matrix() @ pb.matrix.to_3x3(); m = m3.to_4x4(); m.translation = pb.matrix.translation; pb.matrix = m; bpy.context.view_layer.update()
def pitch(arm, bname, axis_world, theta):
    pb = arm.pose.bones.get(bname)
    if pb is None: return
    inv = arm.matrix_world.to_3x3().inverted(); ax = (inv @ Vector(axis_world)).normalized()
    m3 = Matrix.Rotation(theta, 3, ax) @ pb.matrix.to_3x3(); m = m3.to_4x4(); m.translation = pb.matrix.translation; pb.matrix = m; bpy.context.view_layer.update()
def reset_pose(arm):
    for pb in arm.pose.bones: pb.location = (0, 0, 0); pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
KEYB = ['Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head', 'LeftShoulder', 'RightShoulder', 'LeftArm', 'LeftForeArm', 'LeftHand', 'RightArm', 'RightForeArm', 'RightHand', 'LeftUpLeg', 'LeftLeg', 'RightUpLeg', 'RightLeg']

class Guest:
    def __init__(s, role, unit, pos, yaw, pose):
        s.role, s.unit, s.pose = role, unit, pose; s.arm, s.meshes = load_unit(unit)
        s.yaw = math.radians(yaw); s.root = bpy.data.objects.new('guest_' + unit, None); sc.collection.objects.link(s.root)
        s.arm.parent = s.root; s.root.location = (pos[0], pos[1], 0); s.root.rotation_euler = (0, 0, s.yaw)
        s.sit_drop = 0.0; s.props = []
        # 잔(손에 쥠) / 류트
        if role in ('toast', 'wipe', 'laugh'):
            s.mug = cyl('handmug', .052, .12, (0, 0, 0), wood_mat(60 + len(CAST_OBJS), (.34, .2, .09), staves=14), seg=20, r2=.058); sph('hfoam', .052, (0, 0, .062), FOAM, scale=(1, 1, .5), sub=2).parent = s.mug
            s.props.append(s.mug)
        if role == 'lute':
            body = sph('lute', .2, (0, 0, 0), wood_mat(70, (.42, .24, .1)), scale=(.95, .55, 1.25), sub=3); box('lneck', (.06, .04, .62), (0, 0, .5), WOOD_D).parent = body; torus('lhole', .06, .008, (0, -.1, 0), simple('흑', (.01, .01, .01), .9), rot=(math.radians(90), 0, 0)).parent = body
            s.lute = body; s.props.append(body)
        if role == 'wipe':
            s.cloth = box('cloth', (.22, .02, .16), (0, 0, 0), simple('행주', (.8, .78, .7), .9), bevel=0); s.props.append(s.cloth)
    def D(s, v):                      # 캐릭터 로컬 방향 → 월드 방향 (정면 = 로컬 −Y)
        return Matrix.Rotation(s.yaw, 3, 'Z') @ Vector(v)
    def apply(s, t):
        """주기 위치 t∈[0,1)의 포즈를 만든다."""
        a = s.arm; reset_pose(a); ph = TAU * t; sit = s.pose == 'sit'
        hips = a.pose.bones[mx('Hips')]
        if sit:
            hips.location = (0, 0, 0)
            lowering = .43
            # Hips 뼈 이동: 뼈 로컬 축이 다를 수 있어 월드 Z로 직접 내린다
            inv = a.matrix_world.to_3x3().inverted(); hips.matrix = Matrix.Translation(hips.matrix.translation + inv @ Vector((0, 0, -lowering))) @ hips.matrix.to_3x3().to_4x4(); bpy.context.view_layer.update()
            for side in ('Left', 'Right'):
                aim(a, mx(side + 'UpLeg'), s.D((0.12 if side == 'Left' else -.12, -1.0, -.04))); aim(a, mx(side + 'Leg'), s.D((0, -.12, -1)))
        # 척추 기울기·몸 흔들기
        lean = (0, -.05, 1); sway = 0
        if s.role == 'laugh': sway = math.sin(ph * 6) * .09; lean = (sway, -.12 + .05 * math.sin(ph * 12), 1)
        if s.role == 'toast': lean = (.02 * math.sin(ph * 3), -.08, 1)
        if s.role == 'sleep': lean = (0, -.95, .55 + .02 * math.sin(ph * 2))
        if s.role == 'lute': lean = (-.05 + .02 * math.sin(ph * 6), -.1, 1)
        for b in ('Spine', 'Spine1', 'Spine2'): aim(a, mx(b), s.D(lean))
        AX = s.D((1, 0, 0))                                          # 로컬 +X(캐릭터 왼쪽) 축 — 양의 각 = 앞으로 숙임
        if s.role == 'sleep': pitch(a, mx('Neck'), AX, .5); pitch(a, mx('Head'), AX, .35)
        elif s.role == 'laugh': pitch(a, mx('Neck'), AX, -.25 - .05 * math.sin(ph * 12)); pitch(a, mx('Head'), AX, -.3)   # 고개 젖히고 웃음
        elif s.role == 'toast': pitch(a, mx('Head'), AX, .08 * math.sin(ph * 2))
        elif s.role == 'wipe': pitch(a, mx('Head'), AX, .25)
        elif s.role == 'lute': pitch(a, mx('Head'), AX, .2)
        L, R = 'Left', 'Right'
        def arm_pose(side, upper, fore):
            aim(a, mx(side + 'Arm'), s.D(upper)); aim(a, mx(side + 'ForeArm'), s.D(fore))
        sx = 1 if True else -1                                    # 로컬 +X = 캐릭터 왼쪽? (정면 −Y를 볼 때 +X는 캐릭터의 왼쪽)
        if s.role == 'toast':
            c = .5 - .5 * math.cos(ph * 2)                       # 두 번 건배
            arm_pose(R, (-.35, -.55 - .35 * c, -.7 + .5 * c), (-.1, -.5 - .6 * c, .5 + .5 * c))      # 오른팔: 잔 높이 들어 앞으로
            arm_pose(L, (.3, -.2, -1), (.2, -.5, -.6))
        elif s.role == 'laugh':
            arm_pose(R, (-.4, -.4, -.9), (-.25, -.7, -.5)); arm_pose(L, (.4 + .1 * math.sin(ph * 6), -.4, -.75 - .1 * math.sin(ph * 6)), (.4, -.9, -.3))   # 배 잡거나 탁자 치기
        elif s.role == 'sleep':
            arm_pose(R, (-.5, -.5, -.7), (.1, -1, -.15)); arm_pose(L, (.5, -.5, -.7), (-.1, -1, -.15))   # 탁자 위에 팔 포개고
        elif s.role == 'lute':
            arm_pose(L, (.45, -.55, -.35), (.55, -.9, .45)); arm_pose(R, (-.4, -.35, -.9), (.15, -.9, -.2 + .15 * math.sin(ph * 12)))   # 왼손 목 · 오른손 줄 튕김
        elif s.role == 'wipe':
            arm_pose(R, (-.25, -.5, -.82), (.1, -1, .15)); arm_pose(L, (.3, -.45, -.82), (-.1, -1, .2 + .1 * math.sin(ph * 4)))   # 두 손으로 잔 닦기
        # 팔이 몸 안쪽으로 파고들지 않게 손 위치 보정은 생략 — 소품 위치는 손 뼈에서
        root_m = s.root.matrix_world
        if hasattr(s, 'mug'):
            hand = bone_world(s.arm, mx('RightHand') if s.role != 'wipe' else mx('RightHand')); s.mug.location = hand + Vector((0, 0, .02)) if s.role != 'toast' else hand + Vector((0, 0, .03))
            if s.role == 'wipe': s.mug.rotation_euler = (math.radians(75), 0, s.yaw)
        if hasattr(s, 'cloth'):
            hh = bone_world(s.arm, mx('LeftHand')); s.cloth.location = hh + Vector((0, -.04, .05)).copy(); s.cloth.rotation_euler = (0, 0, s.yaw + .6 * math.sin(ph * 4))
        if hasattr(s, 'lute'):
            ch = bone_world(s.arm, mx('Spine1')); s.lute.location = ch + s.D((.05, -.22, -.12)); s.lute.rotation_euler = (math.radians(-25), math.radians(8), s.yaw + math.radians(70))
    def key(s, frame):
        for pb in s.arm.pose.bones: pb.keyframe_insert('rotation_quaternion', frame=frame); pb.keyframe_insert('location', frame=frame)
        for p in s.props: p.keyframe_insert('location', frame=frame); p.keyframe_insert('rotation_euler', frame=frame)
CAST_OBJS = []
for role, unit, pos, yaw, pose in CAST: CAST_OBJS.append(Guest(role, unit, pos, yaw, pose))

# ------------------------------------------------------------------ 카메라·렌더 설정
cam = bpy.data.cameras.new('cam'); cam.lens = 24; cam.sensor_width = 36; co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
co.location = (0.0, -5.2, 1.55); tgt = Vector((.4, 3.2, 1.3)); co.rotation_euler = (tgt - co.location).to_track_quat('-Z', 'Y').to_euler()
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.06, .04, .03, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .25
sc.render.engine = 'CYCLES'; cy = sc.cycles
try:
    pr = bpy.context.preferences.addons['cycles'].preferences; pr.compute_device_type = 'METAL'; pr.get_devices()
    for d_ in pr.devices: d_.use = True
    cy.device = 'GPU'
except Exception: cy.device = 'CPU'
cy.samples = SAMPLES
try: cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
except Exception: pass
cy.max_bounces = 6; cy.sample_clamp_indirect = 5
sc.render.resolution_x = RW; sc.render.resolution_y = RH; sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_depth = '8'; sc.render.fps = 24
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = float(os.environ.get('TT_EXPOSURE', '-0.25'))
try: sc.view_settings.look = 'AgX - Punchy'
except Exception: pass
sc.frame_start, sc.frame_end = 1, N

sel = os.environ.get('TT_FRAMES', '1')
if '-' in sel: a_, b_ = sel.split('-'); frames = range(int(a_), int(b_) + 1)
else: frames = [int(x) for x in sel.split(',')]
if len(frames) > 1 or os.environ.get('TT_ANIM'):
    # 손님 포즈 키: 8프레임마다 + 마지막(N+1)은 첫 프레임과 같게
    for f in range(1, N + 2, 8):
        t = ((f - 1) % N) / N
        for g in CAST_OBJS: g.apply(t); g.key(f)
else:
    for g in CAST_OBJS: g.apply(((frames[0] - 1) % N) / N)
for f in frames:
    sc.frame_set(f); sc.render.filepath = f'{OUT}/raw/f_{f:04d}.png'
    if os.path.exists(sc.render.filepath) and os.environ.get('TT_SKIP_DONE'): continue
    bpy.ops.render.render(write_still=True); print('frame', f, flush=True)
