"""명령 카드 아이콘 6종(blender 세션 2026-10-07, PM 지시): 이동(빨간 화살표)·홀딩(방패)·정지·공격(교차 칼)·반복(순환 화살표)·판매(금화).
워크3 BTN 문법(어두운 그라데이션 바탕 + 위에서 빛 받는 입체 소품 + 가장자리 어둡게)으로 새로 모델링·렌더 — 블리자드 그림 추출 없음.
blender -b --factory-startup --python Tools/blender/gen_wc3_cmd_icons.py -- [~/GRD_wc3_ui/icons]   → <이름>_256 렌더 → PIL로 128·64 + 비교 시트(icons_sheet.png)
"""
import os, sys, math
HERE = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(HERE, 'gen_ui_wc3_common.py'), encoding='utf-8').read())
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if argv else '~/GRD_wc3_ui/icons'); os.makedirs(OUT + '/_raw', exist_ok=True)
NAMES = os.environ.get('ICONS', 'move,hold,stop,attack,patrol,sell').split(',')

def scene():
    sc = reset_scene(); render_setup(sc, 96, transparent=True)
    s = bpy.data.lights.new('key', 'SUN'); s.energy = 4.5; s.angle = math.radians(6); so = link(bpy.data.objects.new('key', s), sc); so.rotation_euler = (math.radians(40), math.radians(-25), math.radians(-20))
    f = bpy.data.lights.new('rim', 'SUN'); f.energy = 1.5; f.color = (.6, .75, 1); fo = link(bpy.data.objects.new('rim', f), sc); fo.rotation_euler = (math.radians(120), math.radians(35), math.radians(40))
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.35, .38, .45, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .25
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = 1.0; co = link(bpy.data.objects.new('cam', cam), sc); sc.camera = co
    co.location = (0, -10, 0); co.rotation_euler = (math.radians(90), 0, 0); sc.render.resolution_x = sc.render.resolution_y = 256
    sc.view_settings.view_transform = 'Standard'
    return sc
def extrude2d(sc, name, pts, depth, mat, loc=(0, 0, 0), rot=(0, 0, 0), bevel=.012):
    bm = bmesh.new(); vs = [bm.verts.new((x, 0, z)) for x, z in pts]; f = bm.faces.new(vs)
    ex = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, vec=(0, depth, 0), verts=[v for v in ex['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); ob = link(bpy.data.objects.new(name, me), sc); ob.location = (loc[0], loc[1] - depth / 2, loc[2]); ob.rotation_euler = rot; me.materials.append(mat)
    if bevel: b = ob.modifiers.new('b', 'BEVEL'); b.width = bevel; b.segments = 3; b.limit_method = 'ANGLE'
    return ob
def arrow_pts(L=.62, shaft=.13, head=.34, hl=.26):
    x0 = -L / 2; xh = L / 2 - hl
    return [(x0, -shaft / 2), (xh, -shaft / 2), (xh, -head / 2), (L / 2, 0), (xh, head / 2), (xh, shaft / 2), (x0, shaft / 2)]
def red_mat(): return metal_mat('빨강', col=(.75, .06, .04), rough=.35, wear=.3, dark=(.2, .01, .01))
GOLDM = None

def icon_move(sc):
    a = extrude2d(sc, 'arrow', arrow_pts(), .1, red_mat(), rot=(0, math.radians(-40), 0)); a.location = (-.02, 0, -.02)
def icon_hold(sc):
    steel = metal_mat('강철', col=(.55, .58, .64), rough=.3, wear=.4, dark=(.08, .08, .1)); g = metal_mat('금', col=(.8, .58, .18), rough=.3, wear=.4, dark=(.15, .1, .02))
    pts = [(-.3, .33), (.3, .33), (.3, .02)] + [(.3 * math.cos(math.radians(a)), -.38 * math.sin(math.radians(a)) + 0.02 * 0) for a in range(0, 181, 10)][1:-1] + [(-.3, .02)]
    pts = [(-.3, .33), (.3, .33), (.3, .0)] + [(.3 * math.cos(math.radians(a)), -.38 * math.sin(math.radians(a))) for a in range(10, 171, 10)] + [(-.3, .0)]
    extrude2d(sc, 'shield', pts, .08, steel, bevel=.03)
    inner = [(x * .78, z * .78 + .01) for x, z in pts]; extrude2d(sc, 'boss', inner, .03, metal_mat('남색판', col=(.08, .14, .4), rough=.4, wear=.3, dark=(.02, .03, .1)), loc=(0, -.045, 0), bevel=.01)
    mkbox(sc, 'v', (.06, .03, .5), (0, -.07, -.02), g, bevel=.012); mkbox(sc, 'h', (.4, .03, .06), (0, -.07, .12), g, bevel=.012)
def icon_stop(sc):
    oct_ = [(.33 * math.cos(math.radians(22.5 + 45 * k)), .33 * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    extrude2d(sc, 'sign', oct_, .08, red_mat(), bevel=.02)
    hand = metal_mat('흰', col=(.9, .88, .82), rough=.4, wear=.2, dark=(.4, .38, .35))
    mkbox(sc, 'palm', (.2, .04, .18), (0, -.06, -.05), hand, bevel=.03)
    for i, (x, h) in enumerate(((-.075, .17), (-.025, .2), (.025, .2), (.075, .17))): mkbox(sc, 'f', (.04, .04, h), (x, -.06, .04 + h / 2), hand, bevel=.018)
    mkbox(sc, 'thumb', (.04, .04, .12), (-.12, -.06, -.02), hand, rot=(0, math.radians(-35), 0), bevel=.018)
def sword(sc, ang, steel, g):
    root = link(bpy.data.objects.new('sw', None), sc); root.rotation_euler = (0, math.radians(ang), 0)
    blade = extrude2d(sc, 'blade', [(-.025, -.12), (.025, -.12), (.025, .33), (0, .4), (-.025, .33)], .02, steel, bevel=.006); blade.parent = root
    gu = mkbox(sc, 'guard', (.2, .04, .04), (0, 0, -.14), g, bevel=.012); gu.parent = root
    gr = mkcyl(sc, 'grip', .022, .14, (0, 0, -.23), metal_mat('가죽', col=(.25, .12, .05), rough=.7, wear=.2, dark=(.05, .02, .01)), seg=10); gr.parent = root
    po = mksph(sc, 'pommel', .035, (0, 0, -.31), g, sub=2); po.parent = root
def icon_attack(sc):
    steel = metal_mat('강철', col=(.7, .72, .78), rough=.2, wear=.3, dark=(.12, .12, .15)); g = metal_mat('금', col=(.8, .58, .18), rough=.3, wear=.4, dark=(.15, .1, .02))
    sword(sc, 40, steel, g); sword(sc, -40, steel, g)
def icon_patrol(sc):
    blue = metal_mat('청', col=(.15, .45, .9), rough=.3, wear=.3, dark=(.02, .06, .15))
    for k in range(2):
        a0 = math.radians(20 + 180 * k); pts_o = []; pts_i = []; R, r = .3, .19
        for i in range(17):
            a = a0 + math.radians(130) * i / 16; pts_o.append((R * math.cos(a), R * math.sin(a))); pts_i.append((r * math.cos(a), r * math.sin(a)))
        ae = a0 + math.radians(130); mid = (R + r) / 2
        tip = ((mid) * math.cos(ae) + .14 * -math.sin(ae), (mid) * math.sin(ae) + .14 * math.cos(ae))
        hw = ((R + .07) * math.cos(ae), (R + .07) * math.sin(ae)); hi = ((r - .07) * math.cos(ae), (r - .07) * math.sin(ae))
        pts = pts_o + [hw, tip, hi] + pts_i[::-1]
        extrude2d(sc, 'arc', pts, .08, blue, bevel=.015)
def icon_sell(sc):
    coin = metal_mat('동전', col=(.85, .6, .15), rough=.25, wear=.4, dark=(.2, .12, .02))
    for i, (x, z, r_) in enumerate(((-.16, -.14, .13), (.14, -.15, .13), (0, -.08, .14), (-.08, .08, .13), (.11, .07, .12), (.0, .2, .12))):
        mkcyl(sc, 'coin', r_, .035, (x, -.04 * i, z), coin, rot=(math.radians(90), 0, 0), seg=40)
        mktorus(sc, 'rim', r_ * .8, .01, (x, -.04 * i - .02, z), coin, rot=(math.radians(90), 0, 0), seg=40)
ICONS = {'move': icon_move, 'hold': icon_hold, 'stop': icon_stop, 'attack': icon_attack, 'patrol': icon_patrol, 'sell': icon_sell}
for n in NAMES:
    sc = scene(); ICONS[n](sc); render(sc, f'{OUT}/_raw/{n}.png')
