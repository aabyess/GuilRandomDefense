"""첫 화면 「움직이는 배경」 이음새 없는 반복 영상(blender 세션 2026-10-06, PM 지시 · 사장님 「너무 정적」).
기반: gen_lobby_art.py 장면 그대로(카메라·구도·횃불·상점) + 아래 움직임. 🔴 제목 글자는 굽지 않는다(UI가 위에 얹음).
  ① 포탈: 상점_다른세계강화소 포탈_발광 재질의 그림 대신 절차 소용돌이(돌아가는 3갈래 팔 + 밖으로 흐르는 고리, 분홍·보라) — T=frame/N 드라이버
  ② 마을 사람 10명(절차 로우폴리, 걷기 순환): 앞마당·대지 위를 오가며 천천히 걷는다(왕복 8초, 끝에서 돌아섬)
  ③ 선술집(대지 오른쪽 뒤): 열린 문·창의 따뜻한 불빛, 앞 탁자에서 잔 드는 사람 4, 굴뚝 연기, 매단 등
  ④ 횃불 깜빡임(점광+불꽃 크기) · 바다 잔물결(교차 페이드로 이음새 없이)
  ⑤ (미구현) 깃발·구름 — 구름은 하늘이 물리 하늘이라 따로. 깃발은 성 모델 안에 있어 분리 필요.
모든 움직임은 N프레임 주기(드라이버 = 정수 배수 사인 · 걷기·연기는 키프레임) → 프레임 N+1 == 프레임 1. N=192(8초 · 24fps).
사용:
  blender -b --factory-startup --python Tools/blender/gen_title_loop.py -- <출력> <가로> <세로> <샘플>      (환경변수 TITLE_FRAMES="1" 또는 "1-192" 또는 "5,60,120")
  /usr/bin/python3 Tools/blender/gen_title_loop.py post <출력>   → raw/ 를 gen_lobby_post의 후처리(꽃·색조·비네트·입자)로 frames/title_0001.png… + title_loop_preview.mp4
산출 ~/GRD_title_loop/."""
import os, sys
if len(sys.argv) > 1 and sys.argv[1] == 'post':          # ---- 후처리(시스템 파이썬)
    import glob, subprocess
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    D = os.path.expanduser(sys.argv[2] if len(sys.argv) > 2 else '~/GRD_title_loop')
    sys.argv = [sys.argv[0], D]
    import gen_lobby_post as P
    os.makedirs(D + '/frames', exist_ok=True)
    raws = sorted(glob.glob(D + '/raw/f_*.png'))
    for r in raws:
        P.post(r).save(D + '/frames/title_' + os.path.basename(r)[2:])
    if len(raws) > 1:
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '24', '-i', D + '/frames/title_%04d.png', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '17', D + '/title_loop_preview.mp4'])
    print('post done', len(raws)); sys.exit(0)

import math
os.environ['LOBBY_NO_RENDER'] = '1'
SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'gen_lobby_art.py')
exec(compile(open(SRC, encoding='utf-8').read(), SRC, 'exec'), globals())     # → scene·terrain_h·helpers·OUT·RW·RH·SAMPLES
import bpy, numpy as np
from mathutils import Vector
N = 192
bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
scene.frame_start, scene.frame_end = 1, N
rn = np.random.RandomState(24)
TAU = 2 * math.pi

def Z(x, y): return float(terrain_h(np.array([x]), np.array([y]))[0])
def drv(idata, path, idx, expr):
    fc = idata.driver_add(path, idx) if idx is not None and idx >= 0 else idata.driver_add(path)
    fc.driver.type = 'SCRIPTED'; fc.driver.expression = expr; return fc
def sinsum(base, amp1, k1, p1, amp2, k2, p2):
    return f'{base}*(1+{amp1}*sin(2*pi*frame/{N}*{k1}+{p1})+{amp2}*sin(2*pi*frame/{N}*{k2}+{p2}))'
def pmat(name, col, rough=.8, emit=None, strength=0, alpha=1.0):
    m = mat_new(name); t = m.node_tree
    o = nd(t, 'ShaderNodeOutputMaterial'); b = nd(t, 'ShaderNodeBsdfPrincipled')
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough
    if alpha < 1: b.inputs['Alpha'].default_value = alpha
    if emit is not None:
        b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = strength
    lk(t, b.outputs[0], o.inputs[0]); return m
MATS = {}
def M(name, *a, **k):
    if name not in MATS: MATS[name] = pmat(name, *a, **k)
    return MATS[name]
def mk_box(name, sx, sy, sz, loc, mat, parent=None, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob); ob.scale = (sx, sy, sz); ob.location = loc; ob.rotation_euler = rot
    me.materials.append(mat)
    if parent: ob.parent = parent
    return ob
def mk_cyl(name, r, h, loc, mat, parent=None, seg=12, r2=None, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=(r if r2 is None else r2), depth=h)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    ob.location = loc; ob.rotation_euler = rot; me.materials.append(mat)
    if parent: ob.parent = parent
    return ob
def mk_sph(name, r, loc, mat, parent=None, sub=2, sc=(1, 1, 1)):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob); ob.location = loc; ob.scale = sc; me.materials.append(mat)
    if parent: ob.parent = parent
    return ob
def mk_empty(name, loc, parent=None):
    e = bpy.data.objects.new(name, None); scene.collection.objects.link(e); e.location = loc
    if parent: e.parent = parent
    return e

# =============================================================== ① 포탈 소용돌이
def animate_portal():
    root = next((o for o in bpy.data.objects if o.name == 'shop_다른세계강화소'), None)
    if root is None: print('portal root missing'); return
    m = next((mm for mm in bpy.data.materials if mm.name.startswith('상점_다른세계강화소_포탈_발광')), None)
    if m is None: print('portal mat missing'); return
    t = m.node_tree
    bs = next(n for n in t.nodes if n.type == 'BSDF_PRINCIPLED')
    def link_clear(sock):
        for l in list(t.links):
            if l.to_socket == sock: t.links.remove(l)
    def MA(op, a, b=None, c=None):
        n = nd(t, 'ShaderNodeMath', operation=op)
        for i, v in enumerate((a, b, c)):
            if v is None: continue
            if isinstance(v, (int, float)): n.inputs[i].default_value = v
            else: lk(t, v, n.inputs[i])
        return n.outputs[0]
    uv = nd(t, 'ShaderNodeTexCoord').outputs['UV']; sp = nd(t, 'ShaderNodeSeparateXYZ'); lk(t, uv, sp.inputs[0])
    u, v = sp.outputs[0], sp.outputs[1]
    T = nd(t, 'ShaderNodeValue', label='T'); drv(T.outputs[0], 'default_value', None, f'frame/{N}')
    right = MA('GREATER_THAN', u, .5)
    cx = MA('ADD', .245, MA('MULTIPLY', right, .51))
    dx = MA('SUBTRACT', u, cx); dy = MA('SUBTRACT', v, .245)
    r = MA('SQRT', MA('ADD', MA('MULTIPLY', dx, dx), MA('MULTIPLY', dy, dy)))
    th = MA('ARCTAN2', dy, dx)
    tt = MA('MULTIPLY', T.outputs[0], TAU)
    a = MA('SINE', MA('SUBTRACT', MA('ADD', MA('MULTIPLY', r, 70), MA('MULTIPLY', th, 3)), tt))        # 돌아가는 3갈래 소용돌이 + 바깥으로 흐름
    b = MA('SINE', MA('ADD', MA('SUBTRACT', MA('MULTIPLY', r, 34), MA('MULTIPLY', th, 2)), MA('MULTIPLY', tt, 2)))
    ring = MA('ADD', .5, MA('MULTIPLY', MA('ADD', MA('MULTIPLY', a, .55), MA('MULTIPLY', b, .45)), .5))
    strip = MA('ADD', .5, MA('MULTIPLY', MA('SINE', MA('SUBTRACT', MA('ADD', MA('MULTIPLY', u, 50), MA('MULTIPLY', v, 38)), tt)), .5))
    isdisc = MA('LESS_THAN', v, .5)
    val = MA('ADD', MA('MULTIPLY', ring, isdisc), MA('MULTIPLY', strip, MA('SUBTRACT', 1, isdisc)))
    rp = nd(t, 'ShaderNodeValToRGB'); cr = rp.color_ramp; cr.interpolation = 'EASE'
    cr.elements[0].position = 0.0; cr.elements[0].color = (.22, .03, .5, 1); cr.elements[1].position = 1.0; cr.elements[1].color = (1, .92, 1, 1)
    e = cr.elements.new(.55); e.color = (.8, .28, .95, 1)
    lk(t, val, rp.inputs[0])
    for sk in (bs.inputs['Base Color'], bs.inputs['Emission Color']):
        link_clear(sk); lk(t, rp.outputs[0], sk)

# =============================================================== ② 횃불 · ④ 바다
def animate_torches():
    k = 0
    for o in scene.objects:
        if o.name.startswith('torchL') and o.type == 'LIGHT':
            ph = rn.rand() * TAU
            drv(o.data, 'energy', None, sinsum(o.data.energy, .16, 5, ph, .09, 13, ph * 1.7)); k += 1
        elif o.name.startswith('flame') and o.type == 'MESH':
            ph = rn.rand() * TAU
            for i in range(3): drv(o, 'scale', i, f'1+{.13 if i == 2 else .06}*sin(2*pi*frame/{N}*{6}+{ph})+0.05*sin(2*pi*frame/{N}*{15}+{ph*2})')
    print('torches', k)

def animate_sea():
    sea = bpy.data.objects.get('Sea'); m = sea.data.materials[0]; t = m.node_tree
    mps = [n for n in t.nodes if n.type == 'MAPPING']; nzs = [n for n in t.nodes if n.type == 'TEX_NOISE']
    adds = next(n for n in t.nodes if n.type == 'MATH'); bump = next(n for n in t.nodes if n.type == 'BUMP')
    s = nd(t, 'ShaderNodeValue'); drv(s.outputs[0], 'default_value', None, f'frame/{N}')
    D = [(.35, .2, 1.1), (.6, .45, 1.6)]
    outs = []
    for i, (mp, nz) in enumerate(zip(mps, nzs)):
        # 원본 mp: 위치 = D*s · 복제 mp2: D*(s−1) · 교차 페이드(1−s, s) → s=0과 s=1이 같은 그림
        for ax in range(3): drv(mp.inputs['Location'], 'default_value', ax, f'{D[i][ax]}*frame/{N}')
        mp2 = nd(t, 'ShaderNodeMapping'); mp2.inputs['Scale'].default_value = mp.inputs['Scale'].default_value
        src = next(l.from_socket for l in t.links if l.to_socket == mp.inputs[0]); lk(t, src, mp2.inputs[0])
        for ax in range(3): drv(mp2.inputs['Location'], 'default_value', ax, f'{D[i][ax]}*(frame/{N}-1)')
        nz2 = nd(t, 'ShaderNodeTexNoise'); nz2.inputs['Detail'].default_value = nz.inputs['Detail'].default_value; nz2.inputs['Scale'].default_value = nz.inputs['Scale'].default_value
        lk(t, mp2.outputs[0], nz2.inputs[0])
        mx = nd(t, 'ShaderNodeMix', data_type='FLOAT'); lk(t, s.outputs[0], mx.inputs[0]); lk(t, nz.outputs['Fac'], mx.inputs[2]); lk(t, nz2.outputs['Fac'], mx.inputs[3])
        outs.append(mx.outputs[0])
    for l in list(t.links):
        if l.to_node == adds: t.links.remove(l)
    lk(t, outs[0], adds.inputs[0]); lk(t, outs[1], adds.inputs[1])

# =============================================================== ③ 사람
SKIN = [(.62, .42, .30), (.48, .30, .20), (.75, .55, .42), (.35, .22, .15)]
CLOTH = [(.45, .12, .10), (.12, .22, .42), (.25, .38, .15), (.50, .36, .12), (.35, .12, .38), (.55, .52, .45), (.14, .30, .30), (.60, .25, .10)]
def make_person(i, H=2.8, hat=None):
    rng_ = np.random.RandomState(100 + i)
    sk = SKIN[rng_.randint(len(SKIN))]; cl = CLOTH[rng_.randint(len(CLOTH))]; pl = CLOTH[rng_.randint(len(CLOTH))]
    s = H / 1.85
    root = mk_empty(f'person{i}', (0, 0, 0))
    skin = M(f'skin{sk}', sk, .7); tun = M(f'tunic{cl}', tuple(c * .9 for c in cl), .85); pant = M(f'pants{pl}', tuple(c * .45 for c in pl), .9)
    mk_cyl('torso', .21 * s, .66 * s, (0, 0, 1.25 * s), tun, root, 10, r2=.17 * s)
    hd = mk_sph('head', .15 * s, (0, 0, 1.70 * s), skin, root, 2)
    hair = M('hair', (.06, .04, .03) if i % 3 else (.45, .30, .12), .9)
    mk_sph('hair', .16 * s, (0, -.015 * s, 1.74 * s), hair, root, 2, (1, 1, .85))
    if hat or i % 4 == 1:
        mk_cyl('hat', .22 * s, .06 * s, (0, 0, 1.82 * s), M('hat', (.22, .14, .08), .9), root, 12)
        mk_cyl('hat2', .12 * s, .14 * s, (0, 0, 1.91 * s), M('hat', (.22, .14, .08), .9), root, 12)
    legs = []; arms = []
    for sx in (-1, 1):
        hip = mk_empty('hip', (sx * .09 * s, 0, .92 * s), root); mk_cyl('leg', .07 * s, .88 * s, (0, 0, -.44 * s), pant, hip, 8); legs.append(hip)
        sh = mk_empty('sh', (sx * .25 * s, 0, 1.52 * s), root); mk_cyl('arm', .055 * s, .62 * s, (0, 0, -.30 * s), tun, sh, 8)
        mk_sph('hand', .065 * s, (0, 0, -.62 * s), skin, sh, 1); arms.append(sh)
    return root, legs, arms

def ease(x): return .5 - .5 * math.cos(math.pi * x)
def walker(i, A, B, phase=0.0, H=2.8):
    """A↔B 왕복(주기 N프레임): 앞 절반 A→B, 뒤 절반 B→A. 끝에서 서서히 멈추며 돌아선다. phase = 주기 속 시작 위치(0..1)."""
    root, legs, arms = make_person(i, H)
    A = Vector(A); B = Vector(B); d = B - A; yaw0 = math.atan2(d.y, d.x) - math.pi / 2     # 모델 정면 = −Y
    for f in range(1, N + 2):
        u = ((f - 1) / N + phase) % 1.0
        # 걷는 시간 .42 · 멈춰 돌아서는 시간 .08 씩 번갈아
        if u < .42: s = ease(u / .42); face = 0; walking = 1
        elif u < .5: s = 1; face = ease((u - .42) / .08); walking = 0
        elif u < .92: s = 1 - ease((u - .5) / .42); face = 1; walking = 1
        else: s = 0; face = 1 - ease((u - .92) / .08); walking = 0
        p = A + d * s; z = Z(p.x, p.y)
        spd = (math.sin(math.pi * ((u / .42) if u < .5 else ((u - .5) / .42))) if walking else 0.0)
        spd = max(0.0, min(1.0, spd * 1.6))
        root.location = (p.x, p.y, z + .05 * H * abs(math.sin(TAU * (f - 1) / 12)) * spd)
        root.rotation_euler = (0, 0, yaw0 + math.pi * face)
        # 걷기 순환: 24프레임에 두 걸음(1초에 한 걸음 쌍) → 192프레임 = 8번 → 정수 주기
        ph = TAU * (f - 1) / 24
        for k, hip in enumerate(legs): hip.rotation_euler = (math.sin(ph + k * math.pi) * .62 * spd, 0, 0)
        for k, sh in enumerate(arms): sh.rotation_euler = (-math.sin(ph + k * math.pi) * .5 * spd, 0, 0)
        for o in [root] + legs + arms:
            o.keyframe_insert('location', frame=f) if o is root else None
            o.keyframe_insert('rotation_euler', frame=f)
    return root

def drinker(i, loc, yaw, phase):
    root, legs, arms = make_person(i, 2.6)
    root.location = loc; root.rotation_euler = (0, 0, yaw)
    for hip in legs: hip.rotation_euler = (-1.45, 0, 0)       # 앉은 다리
    root.location = (loc[0], loc[1], loc[2] - .18 * 2.6)
    ar = arms[1]; mug = mk_cyl('mug', .07 * 2.6 / 1.85 * 1.0, .12 * 2.6 / 1.85, (0, 0, -.62 * 2.6 / 1.85), M('mug', (.5, .35, .15), .5), ar, 8)
    for f in range(1, N + 2):
        c = .5 - .5 * math.cos(TAU * ((f - 1) / N * 2 + phase))        # 한 판에 두 번 잔 들기
        ar.rotation_euler = (-2.3 * c ** 1.5, 0, -.15)
        ar.keyframe_insert('rotation_euler', frame=f)
        arms[0].rotation_euler = (-.9, 0, .2); 
    arms[0].keyframe_insert('rotation_euler', frame=1)
    return root

# =============================================================== ③ 선술집
def build_tavern(loc, yaw):
    g = mk_empty('tavern', loc); g.rotation_euler = (0, 0, yaw)
    stone = M('tv_stone', (.22, .19, .16), .95); plast = M('tv_plaster', (.55, .42, .28), .9); beam = M('tv_beam', (.07, .04, .025), .9)
    roof = M('tv_roof', (.30, .09, .05), .85); wood = M('tv_wood', (.18, .1, .05), .9)
    warm = M('tv_glow', (1, .6, .25), .5, emit=(1, .56, .2), strength=9.0)
    W, D = 13.0, 9.0
    mk_box('base', W, D, 3.0, (0, 0, 1.5), stone, g)
    mk_box('wall', W - .6, D - .6, 4.2, (0, 0, 5.1), plast, g)
    for x in np.linspace(-W / 2 + .4, W / 2 - .4, 6): mk_box('bm', .35, D - .4, 4.3, (x, 0, 5.1), beam, g)          # 세로 보
    for z in (3.1, 7.2): mk_box('bh', W - .2, D - .3, .35, (0, 0, z), beam, g)
    # 지붕(삼각기둥)
    rh = 5.2; rw = W / 2 + 1.0
    bm = bmesh.new(); vs = [bm.verts.new(v) for v in [(-rw, -D / 2 - 1.0, 0), (rw, -D / 2 - 1.0, 0), (0, -D / 2 - 1.0, rh), (-rw, D / 2 + 1.0, 0), (rw, D / 2 + 1.0, 0), (0, D / 2 + 1.0, rh)]]
    for f in ((0, 1, 2), (3, 5, 4), (0, 2, 5, 3), (1, 4, 5, 2), (0, 3, 4, 1)): bm.faces.new([vs[i] for i in f])
    me = bpy.data.meshes.new('roof'); bm.to_mesh(me); bm.free(); me.materials.append(roof)
    ro = bpy.data.objects.new('roof', me); scene.collection.objects.link(ro); ro.parent = g; ro.location = (0, 0, 7.4)
    # 굴뚝(왼쪽 뒤)
    ch = mk_box('chimney', 1.5, 1.5, 7.0, (-4.2, 1.5, 8.6), stone, g); mk_box('chcap', 1.9, 1.9, .35, (-4.2, 1.5, 12.2), stone, g)
    # 정면(−Y): 문 · 창
    fy = -D / 2 + .25
    mk_box('doorframe', 3.0, .5, 3.9, (0, fy - .05, 1.95 + 3.0), beam, g, ) if False else None
    mk_box('door', 2.3, .3, 3.2, (0, fy - .1, 1.6 + 2.2), warm, g)                                  # 열린 문 안쪽 불빛
    mk_box('dfL', .3, .5, 3.5, (-1.35, fy - .1, 3.95), beam, g); mk_box('dfR', .3, .5, 3.5, (1.35, fy - .1, 3.95), beam, g); mk_box('dft', 3.0, .5, .3, (0, fy - .1, 5.7), beam, g)
    mk_box('step', 3.6, 1.2, .4, (0, fy - .85, 2.2 + 0), stone, g) if False else None
    for sx in (-4.2, 4.2):
        mk_box('win', 1.9, .25, 1.7, (sx, fy - .05, 5.4), warm, g)
        for dx in (-1.1, 1.1): mk_box('shut', .5, .3, 1.9, (sx + dx, fy - .12, 5.4), wood, g)
        mk_box('sill', 2.4, .5, .25, (sx, fy - .2, 4.4), beam, g)
    for sx in (-3.2, 3.2): mk_box('win2', 1.5, .25, 1.4, (sx, fy - .05, 9.0 - 1.2), warm, g) if False else None
    # 간판(매단 판 + 잔)
    mk_box('armb', 2.2, .15, .15, (6.9, -D / 2 - .6, 6.8), beam, g)
    sg = mk_box('sign', 1.6, .15, 1.2, (7.3, -D / 2 - .6, 5.6), wood, g)
    mk_cyl('signmug', .35, .6, (7.3, -D / 2 - .72, 5.6), M('tv_signmug', (.8, .55, .15), .4, emit=(.8, .5, .1), strength=1.5), g, 10, rot=(math.radians(90), 0, 0))
    # 처마 밑 등 + 점광(문·창에서 새는 빛이 앞마당·탁자를 비춘다)
    lamp = mk_sph('lamp', .28, (-3.0, -D / 2 - 1.0, 5.2), warm, g, 1)
    for i, (lx, ly, lz, pw) in enumerate(((0, -D / 2 - .8, 3.6, 3500), (-3.0, -D / 2 - 1.2, 5.0, 1500), (4.2, -D / 2 - 1.0, 5.2, 1500))):
        ld = bpy.data.lights.new(f'tavL{i}', 'POINT'); ld.color = (1, .62, .28); ld.energy = pw; ld.shadow_soft_size = .6
        lo = bpy.data.objects.new(f'tavL{i}', ld); scene.collection.objects.link(lo); lo.parent = g; lo.location = (lx, ly, lz)
        drv(ld, 'energy', None, sinsum(pw, .06, 4, i, .04, 11, i * 2))
    # 앞 탁자 둘 + 벤치 + 사람 4
    tb = M('tv_table', (.20, .11, .06), .9); drinkers = []
    for ti, tx in enumerate((-3.4, 3.4)):
        ty = -D / 2 - 3.2
        mk_cyl('table', 1.1, .15, (tx, ty, 2.7), tb, g, 14); mk_cyl('tleg', .22, 2.6, (tx, ty, 1.4), tb, g, 8)
        for k, (bx, by, yw) in enumerate(((tx - 1.8, ty, -math.pi / 2), (tx + 1.8, ty, math.pi / 2))):
            mk_box('bench', .6, 2.0, .2, (bx, by, 1.8), tb, g)
            drinkers.append((g, bx, by, yw, ti * .37 + k * .5))
    return g, drinkers

TAV_SEATS = []
def place_tavern():
    loc = (28.0, 1.0, Z(28.0, 1.0)); yaw = math.radians(-32)
    g, dr = build_tavern(loc, yaw)
    bpy.context.view_layer.update()
    for i, (g_, bx, by, yw, ph) in enumerate(dr):
        pw = g.matrix_world @ Vector((bx, by, 2.1))
        # 사람은 월드에 직접 놓는다(부모 없이): 문(−Y) 쪽을 보게
        TAV_SEATS.append((Vector((pw.x, pw.y, 0)), yaw + yw + math.pi, ph, loc[2]))   # 스킨 손님은 아래 Sitter가 앉힌다
    # 굴뚝 연기
    chp = g.matrix_world @ Vector((-4.2, 1.5, 12.6))
    sm = M('smoke', (.55, .48, .42), 1.0, alpha=.30, emit=(.75, .42, .25), strength=.9)
    for k in range(22):
        puff = mk_sph('smoke', 1.0, chp, sm, None, 2)
        ph0 = k / 22
        for f in range(1, N + 2):
            tt = ((f - 1) / N + ph0) % 1.0
            sc_ = (.7 + 4.2 * tt) * math.sin(math.pi * min(1.0, tt * 1.1)) ** .8
            puff.scale = (sc_, sc_, sc_ * .9)
            puff.location = (chp.x + 5.0 * tt + 1.0 * math.sin(TAU * tt * 1.5 + k), chp.y + 2.0 * tt, chp.z + 16.0 * tt)
            puff.keyframe_insert('location', frame=f); puff.keyframe_insert('scale', frame=f)
    return g

# =============================================================== 구성
animate_portal(); animate_torches(); animate_sea()
place_tavern()
# 걷는 사람 — 앞마당(카메라 쪽, 횃불 사이) 6 · 대지 위 4. (A, B, 주기 속 시작 위치)
# --- 마을 사람 = 우리 유닛 스킨(Assets/Art/Units/<이름>/<이름>.fbx, 읽기만). 사장님이 바꾸려면 아래 CROWD 이름만 고쳐 다시 렌더. ---
# (유닛 폴더 이름, 동작, A점, B점, 시작 위상) — 동작 'walk' = A↔B 천천히 왕복(위아래 튀지 않음) · 'idle' = 서서 대기(A점, B점은 바라볼 곳)
CROWD = [
    # --- 앞마당(낮은 땅, 포탈·성 앞) ---
    ('초월_박민수_AD',  'walk', (-18, -22), (-12, -25), .00),
    ('초월_두유찬_AD',  'walk', (-29, -10), (-25, -16), .30),
    ('초월_최상호_AD',  'walk', (-9, -14),  (-2, -18), .55),
    ('초월_최상호_AP',  'idle', (-5, -33),  (6, -26), .15),
    ('초월_구주호_AD',  'walk', (-26, -28), (-31, -23), .70),
    ('불멸_이이삭',     'walk', (-22, -20), (-17, -18), .45),
    ('불멸_정윤식',     'idle', (-17, -31), (-12, -34), .90),
    ('초월_조성진_AD',  'walk', (-38, -17), (-35, -23), .25),
    ('초월_이태훈_AP',  'walk', (-11, -37), (-6, -34), .80),
    ('불멸_김용태',     'walk', (3, -29),   (7, -24), .50),
    ('초월_신문철_AP',  'idle', (-23, -37), (-18, -33), .05),
    ('영원_조세민',     'walk', (-34, -33), (-30, -35), .65),
    # --- 섬 윗면(대지) ---
    ('초월_이재윤_AD',  'idle', (-30, -1),  (-30, 6), .40),
    ('초월_엄태웅_AD',  'walk', (-12, 8),   (-5, 9), .10),
    ('초월_배성령_AD',  'walk', (-3, 1),    (3, 6), .62),
    ('초월_김건_AP',    'walk', (21, -8),   (26, -4), .20),
    ('초월_양재모_AD',  'idle', (10, -6),   (13, -9), .85),
    ('초월_임장혁_AD',  'walk', (-27, 6),   (-24, 3), .35),
    ('초월_김만경_AD',  'walk', (-17, -3),  (-13, -6), .75),
    ('초월_박기찬_AD',  'walk', (11, 5),    (15, 2), .55),
    ('초월_황준석_ADAP', 'idle', (-8, -7),   (-4, -3), .95),
    ('초월_김민준_AP',  'walk', (15, -6),   (18, -8), .15),
]
# 선술집 앞 탁자 손님 4명(앉은 자세 + 잔 들기): 자리 순서대로
TAVERN_GUESTS = ['불멸_정준영', '영원_김정래', '초월_노태현_AP', '초월_김경현_AP']
UNITS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Art', 'Units')
def mxn(n): return 'mixamorig:' + n
CHILD = {'Hips': ['Spine'], 'Spine': ['Spine1'], 'Spine1': ['Spine2', 'Neck'], 'Spine2': ['Neck'], 'Neck': ['Head'], 'LeftArm': ['LeftForeArm'], 'LeftForeArm': ['LeftHand'], 'RightArm': ['RightForeArm'], 'RightForeArm': ['RightHand'],
         'LeftUpLeg': ['LeftLeg'], 'LeftLeg': ['LeftFoot'], 'RightUpLeg': ['RightLeg'], 'RightLeg': ['RightFoot']}
def aim(arm, short, dir_world):
    """뼈→자식 관절 벡터가 월드 방향을 보게 포즈(뼈 축이 제각각이라 관절 벡터로 잰다)."""
    pb = arm.pose.bones.get(mxn(short))
    if pb is None: return
    ch = next((arm.pose.bones[mxn(c)] for c in CHILD.get(short, []) if mxn(c) in arm.pose.bones), None)
    if ch is None: return
    inv = arm.matrix_world.to_3x3().inverted(); d = (inv @ Vector(dir_world)).normalized()
    cur = (ch.matrix.translation - pb.matrix.translation).normalized(); rot = cur.rotation_difference(d)
    m = (rot.to_matrix() @ pb.matrix.to_3x3()).to_4x4(); m.translation = pb.matrix.translation; pb.matrix = m; bpy.context.view_layer.update()
def pitch_bone(arm, short, axis_world, theta):
    pb = arm.pose.bones.get(mxn(short))
    if pb is None: return
    inv = arm.matrix_world.to_3x3().inverted(); ax = (inv @ Vector(axis_world)).normalized()
    m = (Matrix.Rotation(theta, 3, ax) @ pb.matrix.to_3x3()).to_4x4(); m.translation = pb.matrix.translation; pb.matrix = m; bpy.context.view_layer.update()
def reset_pose(arm):
    for pb in arm.pose.bones: pb.location = (0, 0, 0); pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0)
    bpy.context.view_layer.update()
def load_skin(unit):
    f = os.path.join(UNITS_DIR, unit, unit + '.fbx'); before = set(bpy.data.objects); acts = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=f)
    new = [o for o in bpy.data.objects if o not in before]; arm = next(o for o in new if o.type == 'ARMATURE')
    for a_ in [a_ for a_ in bpy.data.actions if a_ not in acts]: bpy.data.actions.remove(a_)
    if arm.animation_data: arm.animation_data_clear()
    for m_ in (o for o in new if o.type == 'MESH'):
        for ms in m_.material_slots:
            if ms.material and ms.material.use_nodes:
                for n_ in ms.material.node_tree.nodes:
                    if n_.type == 'BSDF_PRINCIPLED':
                        n_.inputs['Roughness'].default_value = max(.62, n_.inputs['Roughness'].default_value)
                        try: n_.inputs['Specular IOR Level'].default_value = .2
                        except Exception: pass
    return arm
WALK_CYCLE = 48        # 프레임 — 192/48 = 4번(정수라 이음새 없음). 천천히: 한 걸음쌍 2초
class Stroller:
    def __init__(s, idx, unit, mode, A, B, phase, H=2.8):
        s.unit, s.mode, s.phase = unit, mode, phase; s.A = Vector((A[0], A[1], 0)); s.B = Vector((B[0], B[1], 0))
        s.arm = load_skin(unit); s.root = bpy.data.objects.new('stroll%d' % idx, None); scene.collection.objects.link(s.root); s.arm.parent = s.root
        k = H / 1.8 * (1.0 + .05 * (((idx * 37) % 5) - 2) / 2); s.root.scale = (k, k, k); s.idx = idx
        d = s.B - s.A; s.yaw0 = math.atan2(d.x, -d.y)                       # 모델 정면 = 로컬 −Y
    def place(s, f):
        u = ((f - 1) / N + s.phase) % 1.0
        if s.mode == 'walk':
            if u < .42: t = ease(u / .42); face = 0; walking = 1; ph_ = u / .42
            elif u < .5: t = 1; face = ease((u - .42) / .08); walking = 0; ph_ = 1
            elif u < .92: t = 1 - ease((u - .5) / .42); face = 1; walking = 1; ph_ = (u - .5) / .42
            else: t = 0; face = 1 - ease((u - .92) / .08); walking = 0; ph_ = 0
            p = s.A + (s.B - s.A) * t; spd = max(0.0, min(1.0, math.sin(math.pi * ph_) * 1.5)) if walking else 0.0
        else:
            p = s.A; face = 0; spd = 0.0
        s.root.location = (p.x, p.y, Z(p.x, p.y)); s.yaw = s.yaw0 + math.pi * face; s.root.rotation_euler = (0, 0, s.yaw)
        bpy.context.view_layer.update(); return spd
    def D(s, v): return Matrix.Rotation(s.yaw, 3, 'Z') @ Vector(v)
    def pose(s, f, spd):
        a = s.arm; reset_pose(a); phi = TAU * (f - 1) / WALK_CYCLE; psi = TAU * (f - 1) / N        # psi = 8초에 한 번(숨쉬기·고개)
        amp = .36 * spd; kmax = .5 * spd
        for side, sg in (('Left', 1), ('Right', -1)):
            th = amp * math.sin(phi + (0 if sg > 0 else math.pi)); k = kmax * max(0.0, math.cos(phi + (0 if sg > 0 else math.pi)))
            aim(a, side + 'UpLeg', s.D((sg * .04, -math.sin(th), -math.cos(th)))); aim(a, side + 'Leg', s.D((sg * .04, -math.sin(th - k), -math.cos(th - k))))
            al = -.28 * spd * math.sin(phi + (0 if sg > 0 else math.pi)) + .03 * math.sin(psi * 2 + sg)            # 팔은 반대 다리와 같이
            aim(a, side + 'Arm', s.D((sg * .16, -math.sin(al), -math.cos(al)))); aim(a, side + 'ForeArm', s.D((sg * .14, -math.sin(al + .28), -math.cos(al + .28))))
        for b_ in ('Spine', 'Spine1', 'Spine2'): aim(a, b_, s.D((0, -.02 * spd, 1)))
        if not os.environ.get('TITLE_NOHEAD'): pitch_bone(a, 'Head', (0, 0, 1), .35 * math.sin(psi * 1 + s.idx))                                           # 천천히 둘러보는 고개
        pitch_bone(a, 'Head', s.D((1, 0, 0)), .05 * math.sin(psi * 2))
    def key(s, f, pose_too=True):
        s.root.keyframe_insert('location', frame=f); s.root.keyframe_insert('rotation_euler', frame=f)
        if pose_too:
            for pb in s.arm.pose.bones: pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f)
class Sitter(Stroller):
    """선술집 앞 의자에 앉은 스킨 손님 — 무릎 굽혀 앉고 잔을 천천히 두 번 든다."""
    def __init__(s, idx, unit, seat):
        pos, yaw, ph, base_z = seat
        super().__init__(idx, unit, 'idle', (pos.x, pos.y), (pos.x, pos.y + 1), ph)
        s.pos = pos; s.fixed_yaw = yaw; s.base_z = base_z; s.ph = ph
        k = s.root.scale[0]; s.hip_target = base_z + 2.0
        s.mug = mk_cyl('hmug', .075 * k, .16 * k, (0, 0, 0), M('hmug', (.5, .35, .15), .5), None, 14)
        s.root.location = (pos.x, pos.y, base_z + 2.0 - .98 * k + .0)
    def place(s, f):
        s.yaw = s.fixed_yaw; s.root.rotation_euler = (0, 0, s.yaw); bpy.context.view_layer.update(); return 0.0
    def pose(s, f, spd):
        a = s.arm; reset_pose(a); psi = TAU * (f - 1) / N; k = s.root.scale[0]
        hips = a.pose.bones[mxn('Hips')]; cur = (a.matrix_world @ hips.head).z; drop = cur - s.hip_target
        inv = a.matrix_world.to_3x3().inverted(); m = hips.matrix.to_3x3().to_4x4(); m.translation = hips.matrix.translation + inv @ Vector((0, 0, -max(0, drop))); hips.matrix = m; bpy.context.view_layer.update()
        for side, sg in (('Left', 1), ('Right', -1)):
            aim(a, side + 'UpLeg', s.D((sg * .08, -1, -.03))); aim(a, side + 'Leg', s.D((sg * .04, -.12, -1)))
        for b_ in ('Spine', 'Spine1', 'Spine2'): aim(a, b_, s.D((0, -.04, 1)))
        c = .5 - .5 * math.cos(psi * 2 + s.ph * TAU)                                   # 한 판에 두 번 잔 들기
        aim(a, 'RightArm', s.D((-.3, -.45 - .4 * c, -.8 + .55 * c))); aim(a, 'RightForeArm', s.D((-.1, -.5 - .6 * c, .35 + .85 * c)))
        aim(a, 'LeftArm', s.D((.3, -.4, -.9))); aim(a, 'LeftForeArm', s.D((.15, -.9, -.45)))
        pitch_bone(a, 'Head', s.D((1, 0, 0)), .06 + .05 * math.sin(psi * 2 + s.ph))
        hand = a.matrix_world @ a.pose.bones[mxn('RightHand')].head; s.mug.location = hand + Vector((0, 0, .08 * k))
    def key(s, f, pose_too=True):
        super().key(f, pose_too)
        if pose_too: s.mug.keyframe_insert('location', frame=f)
STROLL = [Stroller(i, *c) for i, c in enumerate(CROWD)] + [Sitter(100 + i, TAVERN_GUESTS[i % len(TAVERN_GUESTS)], seat) for i, seat in enumerate(TAV_SEATS)]
_sel = os.environ.get('TITLE_FRAMES', '1')
_fr = range(int(_sel.split('-')[0]), int(_sel.split('-')[1]) + 1) if '-' in _sel else [int(x) for x in _sel.split(',')]
if len(_fr) == 1:
    for st in STROLL: st.pose(_fr[0], st.place(_fr[0]))                              # 정지컷: 그 프레임 포즈만
else:
    bpy.context.preferences.edit.keyframe_new_interpolation_type = 'LINEAR'
    for f in range(1, N + 2, 2):                                                      # 루트 이동은 2프레임마다
        for st in STROLL: st.place(f); st.key(f, False)
    bpy.context.preferences.edit.keyframe_new_interpolation_type = 'BEZIER'
    for f in range(1, N + 2, 4):                                                      # 포즈는 4프레임마다(베지어 보간)
        for st in STROLL: st.pose(f, st.place(f)); st.key(f, True)
    for st in STROLL: st.root.animation_data.action.fcurves if False else None

if os.environ.get('TITLE_CLOSE'):                                   # 확인용 근접 카메라(선술집 앞): TITLE_CLOSE=x,y,z
    tx, ty, tz = [float(v) for v in os.environ['TITLE_CLOSE'].split(',')]; c_ = scene.camera
    c_.location = Vector((tx - 14, ty - 30, tz + 8)); c_.data.lens = 60; c_.data.shift_x = 0; c_.data.shift_y = 0
    c_.rotation_euler = (Vector((tx, ty, tz)) - c_.location).to_track_quat('-Z', 'Y').to_euler()
# 렌더
def setup_loop_render():
    scene.render.fps = 24
    scene.render.resolution_x = RW; scene.render.resolution_y = RH
setup_loop_render()
sel = os.environ.get('TITLE_FRAMES', '1')
if '-' in sel: a, b = sel.split('-'); frames = range(int(a), int(b) + 1)
else: frames = [int(x) for x in sel.split(',')]
os.makedirs(OUT + '/raw', exist_ok=True)
if not os.environ.get('TITLE_NO_RENDER'):
    for f in frames:
        scene.frame_set(f); scene.render.filepath = OUT + '/raw/f_%04d.png' % f
        if os.path.exists(scene.render.filepath) and os.environ.get('TITLE_SKIP_DONE'): continue
        bpy.ops.render.render(write_still=True); print('frame', f, flush=True)
