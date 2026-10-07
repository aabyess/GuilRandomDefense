"""워크3 콘솔풍 UI 그림 공통(blender 세션 2026-10-07, 사장님 「원작 원랜디 UI와 최대한 똑같게」). 블리자드 그림 추출 없이 전부 새 모델링·렌더.
다른 gen_ui_wc3_*.py가 `exec(open(이 파일).read())` 또는 import로 쓴다. 정사영 카메라 · 투명 배경 · Cycles(GPU Metal).
재질: stone(거친 회색 돌) · mortar(어두운 줄눈) · moss(이끼 섞기) · brass(낡은 금빛 금속) · iron · gem(색) · orb(파란 유리 구슬, 속빛) · lacquer(남색 단추판)."""
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Matrix

TAU = math.tau

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene

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
def ramp(t, fac, stops, interp='LINEAR'):
    r = nd(t, 'ShaderNodeValToRGB'); cr = r.color_ramp; cr.interpolation = interp
    while len(cr.elements) < len(stops): cr.elements.new(.5)
    for e, (p, c) in zip(cr.elements, stops): e.position = p; e.color = c
    lk(t, fac, r.inputs[0]); return r.outputs[0]
def mixc(t, fac, a, b):
    m = nd(t, 'ShaderNodeMix', data_type='RGBA')
    if isinstance(fac, (int, float)): m.inputs[0].default_value = fac
    else: lk(t, fac, m.inputs[0])
    for i, v in ((6, a), (7, b)):
        if isinstance(v, tuple): m.inputs[i].default_value = v
        else: lk(t, v, m.inputs[i])
    return m.outputs[2]

def stone_mat(name='돌', base=(.13, .125, .125), seed=0, moss=.5, rust=.0, light=1.0, rust_low=0.0):
    """워크3 콘솔 돌(규격표 #6C6E72~#3A3B3F 회갈색): 큰 얼룩 + 고운 알갱이 + 드문 금 + 모서리 닳아 밝게
    + AO로 오목한 곳 어둡게·이끼(초록) + (선택) 붉은 녹/핏빛 얼룩. base는 선형값."""
    m, t, b = newmat(name)
    tc = nd(t, 'ShaderNodeTexCoord'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Location'].default_value = (seed * 7.1, seed * 3.3, seed * 1.9); lk(t, tc.outputs['Object'], mp.inputs[0])
    n1 = nd(t, 'ShaderNodeTexNoise'); n1.inputs['Scale'].default_value = 4.5; n1.inputs['Detail'].default_value = 10; n1.inputs['Roughness'].default_value = .62; lk(t, mp.outputs[0], n1.inputs[0])
    n2 = nd(t, 'ShaderNodeTexNoise'); n2.inputs['Scale'].default_value = 55; n2.inputs['Detail'].default_value = 6; lk(t, mp.outputs[0], n2.inputs[0])
    vr = nd(t, 'ShaderNodeTexVoronoi'); vr.feature = 'DISTANCE_TO_EDGE'; vr.inputs['Scale'].default_value = 2.2; vr.inputs['Randomness'].default_value = 1.0; lk(t, mp.outputs[0], vr.inputs[0])
    wob = nd(t, 'ShaderNodeTexNoise'); wob.inputs['Scale'].default_value = 1.3; lk(t, mp.outputs[0], wob.inputs[0])
    crack = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, MA(t, 'DIVIDE', vr.outputs['Distance'], .012), clamp=True), MA(t, 'GREATER_THAN', wob.outputs['Fac'], .55))   # 드문 금(일부 영역만)
    tone = MA(t, 'ADD', MA(t, 'MULTIPLY', n1.outputs['Fac'], .65), MA(t, 'MULTIPLY', n2.outputs['Fac'], .35))
    warm = tuple(c * f for c, f in zip(base, (1.06, 1.0, .93)))
    col = ramp(t, tone, [(.28, tuple(c * .42 for c in base) + (1,)), (.52, base + (1,)), (.74, tuple(min(1, c * 1.55 * light) for c in warm) + (1,))])
    col = mixc(t, MA(t, 'MULTIPLY', crack, .45), col, (.012, .011, .011, 1))
    bv = nd(t, 'ShaderNodeBevel'); bv.inputs['Radius'].default_value = .012; geo = nd(t, 'ShaderNodeNewGeometry')
    dt = nd(t, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); lk(t, bv.outputs[0], dt.inputs[0]); lk(t, geo.outputs['Normal'], dt.inputs[1])
    edge = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, dt.outputs['Value'], clamp=True), 9, clamp=True)
    col = mixc(t, MA(t, 'MULTIPLY', edge, .7), col, tuple(min(1, c * 2.4) for c in warm) + (1,))
    ao = nd(t, 'ShaderNodeAmbientOcclusion'); ao.inputs['Distance'].default_value = .06; ao.samples = 8
    occ = MA(t, 'SUBTRACT', 1, ao.outputs['AO'], clamp=True)                                                        # 1 = 오목
    gp = nd(t, 'ShaderNodeTexNoise'); gp.inputs['Scale'].default_value = 7; gp.inputs['Detail'].default_value = 8; lk(t, geo.outputs['Position'], gp.inputs[0])
    if moss > 0:
        mm = MA(t, 'MULTIPLY', MA(t, 'MULTIPLY', MA(t, 'GREATER_THAN', gp.outputs['Fac'], .52), MA(t, 'MULTIPLY', occ, 3.0, clamp=True)), moss, clamp=True)
        col = mixc(t, mm, col, (.035, .055, .018, 1))
    if rust > 0:
        rn = nd(t, 'ShaderNodeTexNoise'); rn.inputs['Scale'].default_value = 2.5; rn.inputs['Detail'].default_value = 9; lk(t, mp.outputs[0], rn.inputs[0])
        rm = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', MA(t, 'MULTIPLY', rn.outputs['Fac'], 2.2), 1.15, clamp=True), rust, clamp=True)
        col = mixc(t, rm, col, (.13, .025, .012, 1))
    if rust_low > 0:                                                                                                # 아래쪽 붉은 녹·핏빛 얼룩(참고 사진 콘솔 아래 가장자리)
        spz = nd(t, 'ShaderNodeSeparateXYZ'); lk(t, geo.outputs['Position'], spz.inputs[0])
        low = MA(t, 'SUBTRACT', 1, MA(t, 'DIVIDE', spz.outputs[2], .55), clamp=True)
        rn2 = nd(t, 'ShaderNodeTexNoise'); rn2.inputs['Scale'].default_value = 3.5; rn2.inputs['Detail'].default_value = 8; lk(t, geo.outputs['Position'], rn2.inputs[0])
        rl = MA(t, 'MULTIPLY', MA(t, 'MULTIPLY', low, MA(t, 'ADD', .35, rn2.outputs['Fac'])), rust_low, clamp=True)
        col = mixc(t, rl, col, (.11, .02, .01, 1))
    col = mixc(t, MA(t, 'MULTIPLY', occ, 1.2, clamp=True), col, (.006, .006, .007, 1))                              # 오목한 곳 어둡게
    lk(t, col, b.inputs['Base Color']); b.inputs['Roughness'].default_value = .9
    try: b.inputs['Specular IOR Level'].default_value = .2
    except Exception: pass
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = 1.0; bp.inputs['Distance'].default_value = .035
    lk(t, MA(t, 'SUBTRACT', tone, MA(t, 'MULTIPLY', crack, .5)), bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m

def simple(name, col, rough=.6, metallic=0.0, emit=None, st=0.0):
    m, t, b = newmat(name); b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metallic
    if emit: b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = st
    return m

def metal_mat(name='금속', col=(.78, .56, .22), rough=.35, wear=.6, dark=(.12, .08, .03)):
    """낡은 금빛 금속(brass): 패인 곳은 어둡게(때), 모서리는 밝게 닳음."""
    m, t, b = newmat(name)
    tc = nd(t, 'ShaderNodeTexCoord'); n1 = nd(t, 'ShaderNodeTexNoise'); n1.inputs['Scale'].default_value = 25; n1.inputs['Detail'].default_value = 8; lk(t, tc.outputs['Object'], n1.inputs[0])
    bv = nd(t, 'ShaderNodeBevel'); bv.inputs['Radius'].default_value = .01; geo = nd(t, 'ShaderNodeNewGeometry')
    dt = nd(t, 'ShaderNodeVectorMath', operation='DOT_PRODUCT'); lk(t, bv.outputs[0], dt.inputs[0]); lk(t, geo.outputs['Normal'], dt.inputs[1])
    edge = MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', 1, dt.outputs['Value'], clamp=True), 10, clamp=True)
    col_ = mixc(t, MA(t, 'MULTIPLY', MA(t, 'GREATER_THAN', n1.outputs['Fac'], .55), wear * .6), col + (1,), dark + (1,))
    col_ = mixc(t, MA(t, 'MULTIPLY', edge, .5), col_, tuple(min(1, c * 1.35) for c in col) + (1,))
    lk(t, col_, b.inputs['Base Color']); b.inputs['Metallic'].default_value = 1.0
    lk(t, MA(t, 'ADD', rough, MA(t, 'MULTIPLY', n1.outputs['Fac'], .25)), b.inputs['Roughness'])
    bp = nd(t, 'ShaderNodeBump'); bp.inputs['Strength'].default_value = .25; bp.inputs['Distance'].default_value = .003; lk(t, n1.outputs['Fac'], bp.inputs['Height']); lk(t, bp.outputs[0], b.inputs['Normal'])
    return m

def gem_mat(name, col, glow=.6):
    m, t, b = newmat(name); b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = .05
    try: b.inputs['Transmission Weight'].default_value = .6; b.inputs['Coat Weight'].default_value = 1.0
    except Exception: pass
    b.inputs['Emission Color'].default_value = (*col, 1); b.inputs['Emission Strength'].default_value = glow; return m

def orb_mat(name='구슬', deep=(.02, .08, .35), mid=(.15, .45, 1.0), core=(.75, .9, 1.0), glow=2.5):
    """파란 유리 구슬: 가장자리 짙은 남색 → 가운데 밝은 하늘색(Layer Weight) + 은은한 발광 + 소용돌이 얼룩."""
    m, t, b = newmat(name)
    lw = nd(t, 'ShaderNodeLayerWeight'); lw.inputs[0].default_value = .35
    tc = nd(t, 'ShaderNodeTexCoord'); nz = nd(t, 'ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3; nz.inputs['Distortion'].default_value = 2.5; lk(t, tc.outputs['Object'], nz.inputs[0])
    f = MA(t, 'ADD', MA(t, 'SUBTRACT', 1, lw.outputs['Facing']), MA(t, 'MULTIPLY', MA(t, 'SUBTRACT', nz.outputs['Fac'], .5), .35))
    col = ramp(t, f, [(.0, deep + (1,)), (.55, mid + (1,)), (.95, core + (1,))])
    lk(t, col, b.inputs['Base Color']); lk(t, col, b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = glow
    b.inputs['Roughness'].default_value = .04
    try: b.inputs['Coat Weight'].default_value = 1.0
    except Exception: pass
    return m

# ---------------------------------------------------------------- 메시 도우미
def link(ob, sc): sc.collection.objects.link(ob); return ob
def mkbox(sc, name, size, loc, mat, bevel=.0, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.scale(bm, vec=size, verts=bm.verts); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = link(bpy.data.objects.new(name, me), sc); ob.location = loc; ob.rotation_euler = rot; me.materials.append(mat)
    if bevel: md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 3
    return ob
def rough_block(sc, name, size, loc, mat, seed, chip=.035, sub=3):
    """깨진 돌덩이: 상자 → 세분 → 노이즈 변위(모서리 깨짐) → 평평한 앞면은 살짝만."""
    ob = mkbox(sc, name, size, loc, mat, bevel=min(size[0], size[2]) * .2)
    ob.modifiers['b'].segments = 3
    ss = ob.modifiers.new('s', 'SUBSURF'); ss.levels = sub; ss.render_levels = sub; ss.subdivision_type = 'SIMPLE'
    tex = bpy.data.textures.new(f'chip{seed}', 'CLOUDS'); tex.noise_scale = .09; tex.noise_depth = 3
    dp = ob.modifiers.new('d', 'DISPLACE'); dp.texture = tex; dp.strength = chip; dp.mid_level = .5; dp.texture_coords = 'GLOBAL'
    return ob
def mkcyl(sc, name, r, h, loc, mat, rot=(0, 0, 0), seg=48, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = link(bpy.data.objects.new(name, me), sc); ob.location = loc; ob.rotation_euler = rot; me.materials.append(mat); return ob
def mksph(sc, name, r, loc, mat, sub=4, scale=(1, 1, 1)):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    ob = link(bpy.data.objects.new(name, me), sc); ob.location = loc; ob.scale = scale; me.materials.append(mat); return ob
def mktorus(sc, name, R, r, loc, mat, rot=(0, 0, 0), seg=64, mseg=16):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=mseg, location=loc, rotation=rot)
    o = bpy.context.object; o.name = name; bpy.ops.object.shade_smooth(); o.data.materials.append(mat); return o

# ---------------------------------------------------------------- 카메라·빛·렌더
def ortho_cam(sc, cx, cz, width_m, rx, ry):
    """정면(−Y에서 +Y를 봄) 정사영. width_m = 가로 화면 폭(미터)."""
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = width_m if rx >= ry else width_m * ry / rx
    co = link(bpy.data.objects.new('cam', cam), sc); sc.camera = co; co.location = (cx, -10, cz); co.rotation_euler = (math.radians(90), 0, 0)
    sc.render.resolution_x, sc.render.resolution_y = rx, ry; return co
def ui_lights(sc, key=4.2, fill=.35):
    """워크3 UI 문법: 왼쪽 위 앞에서 오는 해(또렷한 그림자) + 오른쪽 아래 차가운 보조 + 약한 하늘."""
    s = bpy.data.lights.new('key', 'SUN'); s.energy = key; s.angle = math.radians(8); s.color = (1, .97, .9)
    so = link(bpy.data.objects.new('key', s), sc); so.rotation_euler = (math.radians(24), math.radians(-12), math.radians(-10))
    f = bpy.data.lights.new('fill', 'SUN'); f.energy = fill; f.color = (.75, .82, 1); fo = link(bpy.data.objects.new('fill', f), sc); fo.rotation_euler = (math.radians(100), math.radians(30), math.radians(35))
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.5, .52, .58, 1); w.node_tree.nodes['Background'].inputs[1].default_value = .12
def render_setup(sc, samples=96, transparent=True, exposure=0.0):
    sc.render.engine = 'CYCLES'; cy = sc.cycles
    try:
        pr = bpy.context.preferences.addons['cycles'].preferences; pr.compute_device_type = 'METAL'; pr.get_devices()
        for d in pr.devices: d.use = True
        cy.device = 'GPU'
    except Exception: cy.device = 'CPU'
    cy.samples = samples
    try: cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
    except Exception: pass
    cy.max_bounces = 6
    sc.render.film_transparent = transparent; sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'; sc.render.image_settings.color_depth = '8'
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.exposure = exposure
def render(sc, path): sc.render.filepath = path; bpy.ops.render.render(write_still=True); print('render', path, flush=True)
