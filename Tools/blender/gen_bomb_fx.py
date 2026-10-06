"""초월 엄태웅 「폭탄제조」 폭발 이펙트 시안 (blender 세션 2026-10-06). 반경 = 1(게임 반경 500 대응), 지면 Z=0.
헤드리스(정본): blender -b --factory-startup --python Tools/blender/gen_bomb_fx.py -- ~/GRD_bomb_fx
산출: frames/bomb_NN.png(14장 · 1·14번은 빈 장) · bomb_sheet.png(접사진) · 파츠 설명은 README.md. 유니티 연결은 PM/구현담당.
구성: ①지면 섬광 ②불덩이(주황→붉은 암색, 부풀다 식음) ③충격파 고리 ④파편 ⑤연기 기둥 ⑥땅 균열 자국.
"""
import math, os, random, sys
import bpy
from mathutils import Vector

out = os.path.expanduser(sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "~/GRD_bomb_fx")
os.makedirs(out + "/frames", exist_ok=True)
random.seed(7)
N = 12
TOTAL = 14
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.frame_start, sc.frame_end = 1, TOTAL
for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: sc.render.engine = e; break
    except TypeError: pass
sc.render.resolution_x, sc.render.resolution_y = 640, 480
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.025, 0.04, 1)
for vt in ("Standard",):
    try: sc.view_settings.view_transform = vt; break
    except TypeError: pass
sc.view_settings.exposure = -0.7

def mat(name, col, strength, alpha=1.0, emit=True):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    if emit:
        em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = col; em.inputs[1].default_value = strength
        if alpha < 1:
            tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader")
            mx.inputs[0].default_value = alpha
            nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], o.inputs[0])
        else: nt.links.new(em.outputs[0], o.inputs[0])
    else:
        b = nt.nodes.new("ShaderNodeBsdfDiffuse"); b.inputs[0].default_value = col; nt.links.new(b.outputs[0], o.inputs[0])
    try: m.blend_method = "BLEND"
    except Exception: pass
    return m

def key(ob, path, frame, val, idx=-1):
    setattr(ob, path, val); ob.keyframe_insert(path, frame=frame)

# 지면
bpy.ops.mesh.primitive_plane_add(size=8); g = bpy.context.object
g.data.materials.append(mat("ground", (0.10, 0.09, 0.08, 1), 1, emit=False))
# 땅 균열 자국(검게 그을린 원판)
bpy.ops.mesh.primitive_circle_add(vertices=48, radius=1.05, fill_type="NGON", location=(0, 0, 0.003)); scorch = bpy.context.object
scorch.data.materials.append(mat("scorch", (0.01, 0.01, 0.01, 1), 1, emit=False))
for f, s in ((1, 0.05), (4, 1.0), (N, 1.1)): key(scorch, "scale", f, (s, s, 1))

# 불덩이 — 노이즈 변위한 울퉁불퉁 공 + 덩이 공 6개. 발광 그라데이션: 가운데 노랑 → 가장자리 주황·적(Layer Weight 정면도).
def fire_mat():
    m = bpy.data.materials.new("fire"); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    o = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission")
    lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs[0].default_value = 0.35
    ramp = nt.nodes.new("ShaderNodeValToRGB"); cr = ramp.color_ramp
    cr.elements[0].position = 0.15; cr.elements[0].color = (0.9, 0.12, 0.02, 1)
    cr.elements[1].position = 0.85; cr.elements[1].color = (1.0, 0.75, 0.05, 1)
    e = cr.elements.new(0.5); e.color = (1.0, 0.25, 0.02, 1)
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs["Facing"], inv.inputs[1]); nt.links.new(inv.outputs[0], ramp.inputs[0])
    # Facing=1(가운데) → 1-1=0 이라 반대로: 가운데=노랑이 되게 ramp 입력에 Facing 그대로 쓴다.
    nt.links.remove(nt.links[-1]); nt.links.new(lw.outputs["Facing"], ramp.inputs[0])
    dark = nt.nodes.new("ShaderNodeMix"); dark.data_type = "RGBA"; dark.inputs[7].default_value = (0.12, 0.02, 0.01, 1)
    nt.links.new(ramp.outputs[0], dark.inputs[6]); nt.links.new(dark.outputs[2], em.inputs[0])
    nt.links.new(em.outputs[0], o.inputs[0])
    return m, dark.inputs[0], em.inputs[1]
fm, fade_in, str_in = fire_mat()
tex = bpy.data.textures.new("fn", "CLOUDS"); tex.noise_scale = 0.6; tex.noise_depth = 3
def lump(r, loc, f_hi):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=5, radius=1, location=loc); ob = bpy.context.object
    bpy.ops.object.shade_smooth(); ob.data.materials.append(fm)
    d = ob.modifiers.new("d", "DISPLACE"); d.texture = tex; d.strength = 0.55; d.mid_level = 0.4
    return ob
main = lump(1, (0, 0, .3), 1); lumps = [main]
for i in range(6):
    a_ = i * math.tau / 6 + random.random(); lumps.append(lump(1, (0, 0, .3), 0))
    lumps[-1].data.name = "lump%d" % i
sizes = ((1, .1), (3, .55), (6, .85), (9, .75), (12, .5))
for f, sz in sizes:
    key(main, "scale", f, (sz, sz, sz * .85)); key(main, "location", f, (0, 0, {1: .1, 3: .4, 6: .6, 9: .8, 12: 1.0}[f]))
for i, lp in enumerate(lumps[1:]):
    a_ = i * math.tau / 6 + .4; off = (math.cos(a_) * .45, math.sin(a_) * .45)
    for f, sz in sizes:
        k = sz * random.uniform(.45, .65); t = (f - 1) / 11
        key(lp, "scale", f, (k, k, k)); key(lp, "location", f, (off[0] * (.3 + t), off[1] * (.3 + t), {1: .1, 3: .4, 6: .6, 9: .8, 12: 1.0}[f] * .8 + .1))
for f, fade, st in ((1, 0, 4.5), (4, 0.05, 3.5), (8, .6, 2.2), (12, 1.0, 1.0)):
    fade_in.default_value = fade; str_in.default_value = st
    fade_in.keyframe_insert("default_value", frame=f); str_in.keyframe_insert("default_value", frame=f)

# 충격파 고리
bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=0.035, major_segments=64, minor_segments=8, location=(0, 0, .04)); ring = bpy.context.object
ring.data.materials.append(mat("ring", (1, .55, .2, 1), 2.5, 0.7))
for f, s in ((1, .1), (5, .8), (9, 1.5), (12, 1.9)): key(ring, "scale", f, (s, s, 1))
# 지면 섬광 원판
bpy.ops.mesh.primitive_circle_add(vertices=48, radius=1, fill_type="NGON", location=(0, 0, 0.02)); fl = bpy.context.object
fl.data.materials.append(mat("flash", (1, .6, .2, 1), 1.2, 0.35))
for f, s in ((1, .2), (3, .6), (5, .75), (7, .8)): key(fl, "scale", f, (s, s, 1))
fl.hide_render = False; key(fl, "hide_render", 6, True); key(fl, "hide_render", 5, False)

# 파편
dm = mat("debris", (1, .45, .1, 1), 2.5)
for i in range(40):
    a = random.uniform(0, math.tau); r = random.uniform(.5, 1.6); h = random.uniform(.6, 1.6)
    bpy.ops.mesh.primitive_cube_add(size=random.uniform(.03, .08), location=(0, 0, .1)); d = bpy.context.object
    d.data.materials.append(dm); d.rotation_euler = (random.random() * 6, random.random() * 6, random.random() * 6)
    key(d, "location", 2, (0, 0, .1))
    for f in range(3, N + 1):
        t = (f - 2) / (N - 2)
        key(d, "location", f, (math.cos(a) * r * t, math.sin(a) * r * t, max(0.02, .1 + h * 4 * t * (1 - t))))

# 연기 기둥
sm = mat("smoke", (.12, .11, .10, 1), 1, 0.75, emit=False)
sm2 = mat("smoke2", (.18, .16, .14, 1), 0.6, 0.55)
for i in range(14):
    a = random.uniform(0, math.tau); r = random.uniform(.05, .6)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1, location=(0, 0, .2)); s = bpy.context.object; bpy.ops.object.shade_smooth()
    s.data.materials.append(sm2)
    f0 = random.randint(3, 6); z1 = random.uniform(.8, 2.2); sz = random.uniform(.2, .45)
    key(s, "scale", 1, (0, 0, 0)); key(s, "scale", f0, (0.01, 0.01, 0.01)); key(s, "scale", N, (sz, sz, sz))
    key(s, "location", f0, (math.cos(a) * r * .3, math.sin(a) * r * .3, .15)); key(s, "location", N, (math.cos(a) * r, math.sin(a) * r, z1))

# 점광(주변 바닥 조명) + 카메라
bpy.ops.object.light_add(type="POINT", location=(0, 0, 1.2)); L = bpy.context.object; L.data.energy = 2000
for f, e in ((1, 0), (3, 350), (8, 80), (12, 0)): L.data.energy = e; L.data.keyframe_insert("energy", frame=f)
bpy.ops.object.camera_add(location=(0, -4.6, 2.2), rotation=(math.radians(72), 0, 0)); cam = bpy.context.object; sc.camera = cam
cam.data.lens = 32

# 컴포지터 글로우
try:
    sc.use_nodes = True
    nt = sc.node_tree; rl = nt.nodes["Render Layers"]; gl = nt.nodes.new("CompositorNodeGlare"); gl.glare_type = "FOG_GLOW"; gl.threshold = 0.8
    cp = nt.nodes["Composite"]; nt.links.new(rl.outputs[0], gl.inputs[0]); nt.links.new(gl.outputs[0], cp.inputs[0])
except Exception as e: print("glare skip", e)

for ob in list(bpy.data.objects):  # 첫 장(1)·끝 장(TOTAL)은 바닥만 — 이펙트 전부 숨김
    if ob.type == "MESH" and ob is not g:
        ob.hide_render = False; ob.keyframe_insert("hide_render", frame=2)
        ob.hide_render = True; ob.keyframe_insert("hide_render", frame=1); ob.keyframe_insert("hide_render", frame=TOTAL)
        if ob.animation_data and ob.animation_data.action:
            for fc in ob.animation_data.action.fcurves if hasattr(ob.animation_data.action, "fcurves") else []:
                if fc.data_path == "hide_render":
                    for kp in fc.keyframe_points: kp.interpolation = "CONSTANT"
L.data.energy = 0; L.data.keyframe_insert("energy", frame=1); L.data.keyframe_insert("energy", frame=TOTAL)
for f in range(1, TOTAL + 1):
    sc.frame_set(f); sc.render.filepath = f"{out}/frames/bomb_{f:02d}.png"; bpy.ops.render.render(write_still=True)
print("DONE", out)
