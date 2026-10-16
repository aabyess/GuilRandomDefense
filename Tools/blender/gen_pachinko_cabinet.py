"""파칭코 기계(정면) 3D 모델 — 영상형 파칭코의 「기계 안에 들어간 느낌」용.

좌표: 1920x1080 캔버스 픽셀(원점 왼쪽 위)을 그대로 쓴다. 1 BU = 100px.
  px(x, y) -> 월드 X=(x-960)/100, Z=(540-y)/100.  깊이 Y: 0=테두리 앞면, +는 안쪽, -는 카메라 쪽.
카메라는 -Y에서 +Y를 본다. 액정 창·간판 창은 뚫려 있어 body 렌더에서 알파 0.

사장님 Blender 창(MCP execute_blender_code)에서:  exec(open(<이 파일>).read()); build(); render_front('<png>')
헤드리스:  blender -b --factory-startup --python gen_pachinko_cabinet.py -- <out.png>
"""
import bpy, bmesh, math, random, sys, os
from mathutils import Vector, Matrix

# ---- 레이아웃(픽셀) : machine_layout 과 json 으로 같이 내보낸다 -----------------
CANVAS = (1920, 1080)
LCD = (512, 170, 896, 504)          # x, y, w, h  (16:9)
MARQUEE = (500, 64, 920, 70)        # 간판 창(흐르는 글자 띠)
CAB = (300, 40, 1620, 1060)         # 몸체 바깥
FIELD = (360, 150, 1560, 880)       # 판면(못 밭) 창
TRAY = (420, 892, 1500, 1044)
FRAME_PAD = 40                      # 액정 둘레 장식 프레임 두께


def P(x, y):
    return ((x - 960) / 100.0, (540 - y) / 100.0)


# ---- 재료 ---------------------------------------------------------------------
def _mat(name, base=(0.8, 0.8, 0.8, 1), metal=0.0, rough=0.4, emit=None, emit_str=0.0, coat=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    b = nt.nodes.new('ShaderNodeBsdfPrincipled')
    b.inputs['Base Color'].default_value = base
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    for k in ('Coat Weight', 'Clearcoat'):
        if k in b.inputs and coat:
            b.inputs[k].default_value = coat
            break
    if emit:
        b.inputs['Emission Color'].default_value = emit
        b.inputs['Emission Strength'].default_value = emit_str
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    return m


def materials():
    return {
        'chrome': _mat('pc_chrome', (0.95, 0.96, 1, 1), 1.0, 0.08),
        'gold': _mat('pc_gold', (1.0, 0.72, 0.22, 1), 1.0, 0.18),
        'darkgold': _mat('pc_darkgold', (0.55, 0.36, 0.1, 1), 1.0, 0.3),
        'lacquer': _mat('pc_lacquer', (0.05, 0.02, 0.12, 1), 0.0, 0.18, coat=1.0),
        'red': _mat('pc_red', (0.45, 0.02, 0.04, 1), 0.0, 0.2, coat=1.0),
        'board': _mat('pc_board', (0.03, 0.05, 0.16, 1), 0.0, 0.35),
        'black': _mat('pc_black', (0.01, 0.01, 0.015, 1), 0.0, 0.3),
        'steel': _mat('pc_steel', (0.7, 0.72, 0.75, 1), 1.0, 0.3),
        'brass': _mat('pc_brass', (0.9, 0.7, 0.3, 1), 1.0, 0.25),
        'rubber': _mat('pc_rubber', (0.02, 0.02, 0.03, 1), 0.0, 0.7),
        'ledoff': _mat('pc_ledoff', (0.08, 0.08, 0.1, 1), 0.0, 0.2),
    }


def led_mat(name, color, strength=2.2):
    return _mat(name, (color[0] * 0.3, color[1] * 0.3, color[2] * 0.3, 1), 0.0, 0.3,
                emit=(color[0], color[1], color[2], 1), emit_str=strength)


def streak_mat():
    m = bpy.data.materials.get('pc_streak') or bpy.data.materials.new('pc_streak')
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
    ramp.color_ramp.elements[1].position = 1.0
    ramp.color_ramp.elements[1].color = (0, 0, 0, 1)
    e = ramp.color_ramp.elements.new(0.5)
    e.color = (0.10, 0.10, 0.10, 1)
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Strength'].default_value = 1.0
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    nt.links.new(sep.outputs['X'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], mix.inputs['Fac'])
    nt.links.new(tr.outputs['BSDF'], mix.inputs[1])
    nt.links.new(em.outputs['Emission'], mix.inputs[2])
    nt.links.new(mix.outputs['Shader'], out.inputs['Surface'])
    try:
        m.surface_render_method = 'BLENDED'
    except Exception:
        pass
    return m


# ---- 메시 도우미 ----------------------------------------------------------------
_COLL = {}


def coll(name):
    if name in _COLL and _COLL[name].name in bpy.data.collections:
        return _COLL[name]
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    _COLL[name] = c
    return c


def _obj(name, bm, mat, cname, bevel=0.0, smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    coll(cname).objects.link(o)
    if mat is not None:
        o.data.materials.append(mat)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    if bevel:
        md = o.modifiers.new('bev', 'BEVEL')
        md.width = bevel
        md.segments = 2
        md.limit_method = 'ANGLE'
    return o


def _cube(bm, x0, y0, x1, y1, d0, d1):
    cx, cz = P((x0 + x1) / 2, (y0 + y1) / 2)
    sx, sz = (x1 - x0) / 100.0, (y1 - y0) / 100.0
    m = Matrix.Translation((cx, (d0 + d1) / 2, cz)) @ Matrix.Diagonal((sx, d1 - d0, sz, 1))
    bmesh.ops.create_cube(bm, size=1.0, matrix=m)


def box(name, x0, y0, x1, y1, d0, d1, mat, cname='body', bevel=0.02):
    bm = bmesh.new()
    _cube(bm, x0, y0, x1, y1, d0, d1)
    return _obj(name, bm, mat, cname, bevel)


def ring(name, outer, inner, d0, d1, mat, cname='body', bevel=0.02):
    """사각 고리: 바깥(x0,y0,x1,y1)에서 안쪽을 뺀 것(상하좌우 네 상자)."""
    ox0, oy0, ox1, oy1 = outer
    ix0, iy0, ix1, iy1 = inner
    bm = bmesh.new()
    _cube(bm, ox0, oy0, ox1, iy0, d0, d1)
    _cube(bm, ox0, iy1, ox1, oy1, d0, d1)
    _cube(bm, ox0, iy0, ix0, iy1, d0, d1)
    _cube(bm, ix1, iy0, ox1, iy1, d0, d1)
    return _obj(name, bm, mat, cname, bevel)


def cyl(name, cx, cy, r_px, d0, d1, mat, cname='body', seg=24, bevel=0.0):
    bm = bmesh.new()
    x, z = P(cx, cy)
    m = Matrix.Translation((x, (d0 + d1) / 2, z)) @ Matrix.Rotation(math.radians(90), 4, 'X')
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                          radius1=r_px / 100.0, radius2=r_px / 100.0, depth=(d1 - d0), matrix=m)
    return _obj(name, bm, mat, cname, bevel, smooth=True)


def tube(name, pts_px, radius, depth, mat, cname='body'):
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = radius
    cu.bevel_resolution = 3
    sp = cu.splines.new('POLY')
    sp.points.add(len(pts_px) - 1)
    for i, (px, py) in enumerate(pts_px):
        x, z = P(px, py)
        sp.points[i].co = (x, depth, z, 1)
    o = bpy.data.objects.new(name, cu)
    coll(cname).objects.link(o)
    o.data.materials.append(mat)
    return o


def arc_pts(cx, cy, r, a0, a1, n=24):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             cy - r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


# ---- 모델 -----------------------------------------------------------------------
def build(seed=7):
    # 새로 시작: 기본 큐브·조명 정리
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    _COLL.clear()
    for n in ('body', 'lights', 'glass', 'decor_fire', 'decor_slime', 'stage'):
        coll(n)
    M = materials()
    rnd = random.Random(seed)

    cx0, cy0, cx1, cy1 = CAB
    fx0, fy0, fx1, fy1 = FIELD
    lx, ly, lw, lh = LCD
    pad = FRAME_PAD
    fr = (lx - pad, ly - pad, lx + lw + pad, ly + lh + pad)   # 액정 장식 프레임 바깥

    # 몸체 뒤 껍데기(판면 창 둘레 + 안쪽 어둠). 액정·간판 자리는 뚫려 있어야 하므로 판 자체를 고리로.
    # 1) 앞 테두리(몸체 정면 패널): 판면 창과 트레이 윗부분 사이를 제외한 바깥 고리
    mx, my, mw, mh = MARQUEE
    ring('bezel_main', (cx0, fy0, cx1, cy1), (fx0, fy0, fx1, fy1), 0.0, 0.45, M['lacquer'], bevel=0.05)
    ring('bezel_top', (cx0, cy0, cx1, fy0), (mx, my, mx + mw, my + mh), 0.0, 0.45, M['lacquer'], bevel=0.05)
    ring('marquee_frame', (mx - 22, my - 14, mx + mw + 22, my + mh + 14), (mx, my, mx + mw, my + mh),
         -0.12, 0.5, M['gold'], bevel=0.03)
    # 간판 뒤 어둠(창은 알파 0이어야 하므로 없음) — 창 안쪽 벽만
    ring('marquee_wall', (mx, my, mx + mw, my + mh), (mx + 6, my + 6, mx + mw - 6, my + mh - 6),
         0.5, 1.2, M['black'], bevel=0.0)

    # 바깥 크롬 외곽선과 크라운
    ring('outer_chrome', (cx0 - 12, cy0 - 12, cx1 + 12, cy1 + 12), (cx0, cy0, cx1, cy1), 0.0, 0.5, M['chrome'], bevel=0.05)
    box('crown', cx0 + 40, cy0 - 30, cx1 - 40, cy0 + 4, -0.2, 0.5, M['gold'], bevel=0.05)
    for i in range(7):   # 크라운 보석
        x = cx0 + 160 + i * (cx1 - cx0 - 320) / 6
        cyl('crown_gem%d' % i, x, cy0 - 13, 11, -0.35, -0.1, M['red'], seg=16)

    # 판면: 못 밭 바탕(액정 장식 프레임 자리는 뚫음 — 고리)
    ring('pin_board', (fx0, fy0, fx1, fy1), (fr[0] + 6, fr[1] + 6, fr[2] - 6, fr[3] - 6), 0.6, 0.9, M['board'], bevel=0.0)
    # 판면 장식 무늬(은은한 격자 선)
    for i in range(1, 12):
        x = fx0 + i * (fx1 - fx0) / 12
        top = fr[3] + 24 if fr[0] - 10 < x < fr[2] + 10 else fy0 + 4
        box('boardline_v%d' % i, x - 1, top, x + 1, fy1 - 4, 0.58, 0.6, M['darkgold'], bevel=0)

    # 액정 장식 프레임(테마 장식을 붙일 자리) — 금 이중 테두리 + 안쪽 크롬 립
    ring('lcd_frame_outer', fr, (lx - 12, ly - 12, lx + lw + 12, ly + lh + 12), -0.3, 0.6, M['gold'], bevel=0.06)
    ring('lcd_frame_inner', (lx - 12, ly - 12, lx + lw + 12, ly + lh + 12), (lx, ly, lx + lw, ly + lh),
         -0.25, 0.7, M['chrome'], bevel=0.03)
    ring('lcd_frame_groove', (fr[0] + 10, fr[1] + 10, fr[2] - 10, fr[3] - 10),
         (fr[0] + 16, fr[1] + 16, fr[2] - 16, fr[3] - 16), -0.32, -0.28, M['black'], bevel=0)
    for (gx, gy) in ((fr[0], fr[1]), (fr[2], fr[1]), (fr[0], fr[3]), (fr[2], fr[3])):   # 모서리 보석
        cyl('lcd_corner_%d_%d' % (gx, gy), gx, gy, 24, -0.55, -0.25, M['gold'], seg=24, bevel=0.02)
        cyl('lcd_corner_gem_%d_%d' % (gx, gy), gx, gy, 12, -0.62, -0.5, M['red'], seg=16)

    # 못(핀) 밭 — 액정 프레임·시작구·가장자리를 피해 엇갈린 격자
    bm = bmesh.new()
    pitch = 32
    n = 0
    y = fy0 + 40
    row = 0
    while y < fy1 - 40:
        x = fx0 + 46 + (pitch / 2 if row % 2 else 0)
        while x < fx1 - 40:
            jx = x + rnd.uniform(-5, 5)
            jy = y + rnd.uniform(-4, 4)
            inframe = (fr[0] - 24 < jx < fr[2] + 24) and (fr[1] - 24 < jy < fr[3] + 24)
            nearstart = abs(jx - 960) < 90 and 745 < jy < 830
            if not inframe and not nearstart:
                px_, pz = P(jx, jy)
                m = Matrix.Translation((px_, 0.45, pz)) @ Matrix.Rotation(math.radians(90), 4, 'X')
                bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=True, segments=8, radius1=0.048, radius2=0.048,
                                      depth=0.3, matrix=m)
                n += 1
            x += pitch
        y += pitch * 0.9
        row += 1
    _obj('pins', bm, M['brass'], 'body', 0, smooth=True)
    # 못 머리(작은 구) — 반짝임
    bm = bmesh.new()
    rnd2 = random.Random(seed)
    # (생략: 머리를 따로 만들면 정점 수가 늘어 가벼운 원기둥만 쓴다)
    bm.free()

    # 안내 레일(공이 올라가는 호) : 왼쪽에서 위로 돌아 오른쪽으로 — 이중 레일
    for k, off in enumerate((0, 16)):
        r = 220 - off
        lx0 = fx0 + 18 + off
        rx0 = fx1 - 18 - off
        pl = [(lx0, fy1 - 60), (lx0, fy0 + 40 + r)] + arc_pts(lx0 + r, fy0 + 40 + r, r, 180, 120, 10)
        pr = [(rx0, fy1 - 110), (rx0, fy0 + 40 + r)] + arc_pts(rx0 - r, fy0 + 40 + r, r, 0, 60, 10)
        tube('railL%d' % k, pl, 0.045, 0.3, M['chrome'] if k == 0 else M['steel'])
        tube('railR%d' % k, pr, 0.045, 0.3, M['chrome'] if k == 0 else M['steel'])

    # 시작구(튤립) : 액정 아래 가운데, 금 고리 + 어두운 입
    cyl('start_ring', 960, 790, 46, 0.2, 0.55, M['gold'], seg=32, bevel=0.02)
    cyl('start_hole', 960, 790, 30, 0.1, 0.6, M['black'], seg=32)
    cyl('start_lamp', 960, 745, 10, 0.1, 0.4, M['red'], seg=16)
    # 아웃구
    box('out_hole', 900, 852, 1020, 880, 0.2, 0.7, M['black'], bevel=0.02)
    # 풍차 두 개(좌우)
    for (wx, wy) in ((430, 780), (1490, 780)):
        cyl('windmill_hub_%d' % wx, wx, wy, 16, 0.1, 0.6, M['gold'], seg=16)
        for a in range(4):
            bm = bmesh.new()
            ang = a * 90 + 20
            _cube(bm, wx - 4, wy - 38, wx + 4, wy, 0.2, 0.5)
            o = _obj('windmill_vane_%d_%d' % (wx, a), bm, M['chrome'], 'body', 0.01)
            o.rotation_euler = (0, math.radians(ang), 0)
            # 회전축을 날개 중심으로: 위치를 (wx,wy)에 맞춤
            x, z = P(wx, wy)
            o.location = (0, 0, 0)
            o.data.transform(Matrix.Translation((-x, 0, -z)))
            o.location = (x, 0, z)

    # 트리거: LED 하우징(어두운 홈) — 켜지는 LED는 lights 컬렉션
    for side, (a, b) in (('L', (318, 346)), ('R', (1574, 1602))):
        box('led_housing_' + side, a - 4, 150, b + 4, 880, -0.08, 0.3, M['black'], bevel=0.02)
    box('led_housing_top', 480, 44, 1440, 60, -0.08, 0.3, M['black'], bevel=0.01)
    box('led_housing_lcd_b', fr[0] + 30, fr[3] + 6, fr[2] - 30, fr[3] + 18, -0.08, 0.3, M['black'], bevel=0.01)

    # 트레이(구슬 받침)
    tx0, ty0, tx1, ty1 = TRAY
    ring('tray_rim', (tx0, ty0, tx1, ty1), (tx0 + 26, ty0 + 24, tx1 - 26, ty1 - 20), -1.0, -0.3, M['chrome'], bevel=0.12)
    box('tray_floor', tx0 + 26, ty0 + 24, tx1 - 26, ty1 - 20, 0.1, 0.4, M['black'], bevel=0)
    box('tray_front', tx0 - 20, ty1 - 20, tx1 + 20, ty1 + 14, -1.0, 0.5, M['red'], bevel=0.08)
    box('tray_gold', tx0 - 20, ty1 - 24, tx1 + 20, ty1 - 18, -1.04, -0.9, M['gold'], bevel=0.02)
    # 구슬 더미
    bm = bmesh.new()
    for i in range(60):
        bx = rnd.uniform(tx0 + 60, tx1 - 60)
        # 가운데로 갈수록 더미가 높게
        hh = 40 * (1 - abs(bx - 960) / 560.0) + 14
        by = rnd.uniform(ty1 - 40 - hh, ty1 - 40)
        x, z = P(bx, by)
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.13,
                                  matrix=Matrix.Translation((x, rnd.uniform(-0.15, 0.05), z)))
    _obj('tray_beads', bm, M['chrome'], 'body', 0, smooth=True)

    # 발사 핸들(오른쪽 아래)
    hx, hy = 1560, 975
    cyl('handle_base', hx, hy, 66, -0.2, 0.4, M['chrome'], seg=40, bevel=0.03)
    cyl('handle_base_in', hx, hy, 50, -0.35, -0.15, M['gold'], seg=40)
    cyl('handle_stem', hx, hy, 34, -0.8, -0.3, M['steel'], seg=32)
    cyl('handle_grip', hx, hy, 46, -1.5, -0.8, M['rubber'], seg=32, bevel=0.1)
    for a in range(12):   # 손잡이 홈
        ang = math.radians(a * 30)
        cyl('handle_rib%d' % a, hx + 38 * math.cos(ang), hy + 38 * math.sin(ang), 5, -1.45, -0.85, M['black'], seg=8)
    cyl('handle_cap', hx, hy, 24, -1.65, -1.45, M['chrome'], seg=24, bevel=0.03)

    # 좌우 아래 스피커 그릴(작은 구멍들)
    for side, gx0 in (('L', 330), ('R', 1500)):
        bm = bmesh.new()
        for i in range(4):
            for j in range(3):
                x, z = P(gx0 + 15 + i * 22, 898 + j * 22)
                bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.07, radius2=0.07, depth=0.1,
                                      matrix=Matrix.Translation((x, -0.02, z)) @ Matrix.Rotation(math.radians(90), 4, 'X'))
        _obj('grill_' + side, bm, M['black'], 'body', 0, smooth=True)

    # ---- LED (lights 컬렉션): 알록달록 구간 ---------------------------------------
    COLS = [(1, 0.1, 0.2), (1, 0.55, 0.05), (1, 0.9, 0.1), (0.1, 1, 0.3), (0.1, 0.7, 1), (0.6, 0.2, 1)]
    ledmats = [led_mat('pc_led%d' % i, c) for i, c in enumerate(COLS)]
    k = 0
    for side, (a, b) in (('L', (318, 346)), ('R', (1574, 1602))):
        for i in range(18):
            y0 = 158 + i * 40
            box('led_%s_%d' % (side, i), a, y0, b, y0 + 32, -0.22, -0.1, ledmats[(i + k) % 6], 'lights', 0.01)
        k += 3
    for i in range(24):
        x0 = 484 + i * 40
        box('led_top_%d' % i, x0, 46, x0 + 32, 58, -0.22, -0.1, ledmats[i % 6], 'lights', 0.01)
    # 액정 프레임 아래 LED 줄
    for i in range(20):
        x0 = fr[0] + 34 + i * ((fr[2] - fr[0] - 68) / 20.0)
        box('led_lcd_%d' % i, x0, fr[3] + 8, x0 + 28, fr[3] + 16, -0.22, -0.1, ledmats[(i + 2) % 6], 'lights', 0.01)
    # 액정 프레임 모서리 보석 불
    for (gx, gy) in ((fr[0], fr[1]), (fr[2], fr[1]), (fr[0], fr[3]), (fr[2], fr[3])):
        cyl('led_gem_%d_%d' % (gx, gy), gx, gy, 12, -0.66, -0.55, ledmats[0], 'lights', seg=16)

    # ---- 앞 유리 반사 (glass 컬렉션) ---------------------------------------------
    sm = streak_mat()
    for i, (sx, w, sh) in enumerate(((600, 90, 0), (740, 36, 0), (1180, 120, 0), (1340, 40, 0))):
        me = bpy.data.meshes.new('streak%d' % i)
        bm = bmesh.new()
        slant = 160
        pts = [(sx, fy0), (sx + w, fy0), (sx + w - slant, fy1), (sx - slant, fy1)]
        vs = []
        for px_, py_ in pts:
            x, z = P(px_, py_)
            vs.append(bm.verts.new((x, -0.4, z)))
        f = bm.faces.new(vs)
        uv = bm.loops.layers.uv.new()
        for l, (u, v) in zip(f.loops, ((0, 1), (1, 1), (1, 0), (0, 0))):
            l[uv].uv = (u, v)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new('streak%d' % i, me)
        coll('glass').objects.link(o)
        o.data.materials.append(sm)
        o.visible_shadow = False

    setup_stage()
    return n


# ---- 무대(조명·월드·카메라) ------------------------------------------------------------
def setup_stage():
    sc = bpy.context.scene
    # 월드: 어두운 방 + 기계 뒤쪽(-Y)에 큰 소프트박스 면 → 크롬·금에 비침
    w = bpy.data.worlds.get('pc_world') or bpy.data.worlds.new('pc_world')
    w.use_nodes = True
    w.node_tree.nodes.clear()
    bg = w.node_tree.nodes.new('ShaderNodeBackground')
    bg.inputs['Color'].default_value = (0.04, 0.04, 0.07, 1)
    bg.inputs['Strength'].default_value = 1.0
    o = w.node_tree.nodes.new('ShaderNodeOutputWorld')
    w.node_tree.links.new(bg.outputs['Background'], o.inputs['Surface'])
    sc.world = w

    em = _mat('pc_softbox', (1, 1, 1, 1), 0, 1, emit=(1, 0.97, 0.92, 1), emit_str=6.0)
    for i, (x, z, sw, sh, st) in enumerate(((-6, 7, 8, 3, 6), (6, 7, 8, 3, 6), (0, 0, 22, 1.4, 3), (-9, -3, 3, 10, 4), (9, -3, 3, 10, 4))):
        me = bpy.data.meshes.new('sb%d' % i)
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5,
                              matrix=Matrix.Diagonal((sw, sh, 1, 1)))
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new('softbox%d' % i, me)
        ob.data.materials.append(em)
        ob.location = (x, -14, z)
        ob.rotation_euler = (math.radians(-90), 0, 0)
        ob.visible_camera = False
        coll('stage').objects.link(ob)
    for name, loc, energy in (('key', (0, -12, 8), 2500), ('fill', (-8, -10, -4), 900)):
        ld = bpy.data.lights.new(name, 'AREA')
        ld.energy = energy
        ld.size = 10
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = (math.radians(70) if loc[2] > 0 else math.radians(100), 0, 0)
        coll('stage').objects.link(lo)

    # 카메라: 원근(망원)으로 y=0 평면이 1920px = 19.2 BU 에 정확히 맞게
    cd = bpy.data.cameras.new('cam')
    cd.sensor_fit = 'HORIZONTAL'
    cd.sensor_width = 36.0
    D = 60.0
    cd.lens = 36.0 * D / 19.2
    cd.clip_end = 400
    co = bpy.data.objects.new('cam', cd)
    co.location = (0, -D, 0)
    co.rotation_euler = (math.radians(90), 0, 0)
    coll('stage').objects.link(co)
    sc.camera = co
    r = sc.render
    r.resolution_x, r.resolution_y, r.resolution_percentage = 1920, 1080, 100
    r.film_transparent = True
    r.image_settings.file_format = 'PNG'
    r.image_settings.color_mode = 'RGBA'
    for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
        try:
            r.engine = eng
            break
        except TypeError:
            continue
    try:
        sc.eevee.taa_render_samples = 64
    except Exception:
        pass
    sc.view_settings.view_transform = 'Standard'


def hide_for(layers):
    """layers: 보일 컬렉션 이름 집합. 무대(stage)는 늘 보임."""
    for c in bpy.data.collections:
        if c.name == 'stage':
            continue
        c.hide_render = c.name not in layers
        c.hide_viewport = c.hide_render


def render_front(path, layers=('body', 'lights', 'glass')):
    hide_for(set(layers))
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def layout_json():
    return {
        'canvas': list(CANVAS),
        'lcd': {'x': LCD[0], 'y': LCD[1], 'w': LCD[2], 'h': LCD[3]},
        'marquee': {'x': MARQUEE[0], 'y': MARQUEE[1], 'w': MARQUEE[2], 'h': MARQUEE[3]},
        'lcd_frame': {'x': LCD[0] - FRAME_PAD, 'y': LCD[1] - FRAME_PAD, 'w': LCD[2] + 2 * FRAME_PAD, 'h': LCD[3] + 2 * FRAME_PAD},
        'cabinet': {'x': CAB[0], 'y': CAB[1], 'w': CAB[2] - CAB[0], 'h': CAB[3] - CAB[1]},
    }


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    out = argv[0] if argv else os.path.expanduser('~/grd/GRD_pachinko_cabinet/front_preview.png')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    build()
    render_front(out)
