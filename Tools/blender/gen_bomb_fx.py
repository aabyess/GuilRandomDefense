"""초월 엄태웅 「폭탄제조」 폭발 이펙트 시안 (blender 세션 2026-10-06). 반경 = 1(게임 반경 500 대응), 지면 Z=0.
헤드리스(정본): blender -b --factory-startup --python Tools/blender/gen_bomb_fx.py -- ~/GRD_bomb_fx
산출: frames/bomb_NN.png(12장) · bomb_sheet.png(접사진) · 파츠 설명은 README.md. 유니티 연결은 PM/구현담당.
구성: ①지면 섬광 ②불덩이(주황→붉은 암색, 부풀다 식음) ③충격파 고리 ④파편 ⑤연기 기둥 ⑥땅 균열 자국.
"""
import math, os, random, sys
import bpy
from mathutils import Vector

out = os.path.expanduser(sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "~/GRD_bomb_fx")
os.makedirs(out + "/frames", exist_ok=True)
random.seed(7)
N = 12
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.frame_start, sc.frame_end = 1, N
for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: sc.render.engine = e; break
    except TypeError: pass
sc.render.resolution_x, sc.render.resolution_y = 640, 480
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.025, 0.04, 1)
for vt in ("AgX", "Filmic", "Standard"):
    try: sc.view_settings.view_transform = vt; break
    except TypeError: pass
sc.view_settings.exposure = -1.0

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

# 불덩이
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=1, location=(0, 0, 0.3)); fb = bpy.context.object
bpy.ops.object.shade_smooth()
fm = mat("fire", (1, 0.45, 0.08, 1), 3, 0.95); fb.data.materials.append(fm)
for f, s, z in ((1, .1, .1), (3, .55, .4), (6, .85, .6), (9, .75, .8), (12, .5, 1.0)): 
    key(fb, "scale", f, (s, s, s * .85)); key(fb, "location", f, (0, 0, z))
# 불덩이 색: 주황 → 암적
nt = fm.node_tree; em = next(n for n in nt.nodes if n.type == "EMISSION")
for f, c, st in ((1, (1, .8, .4, 1), 6), (4, (1, .4, .08, 1), 3.5), (8, (.6, .1, .02, 1), 1.5), (12, (.2, .03, .01, 1), .5)):
    em.inputs[0].default_value = c; em.inputs[1].default_value = st
    em.inputs[0].keyframe_insert("default_value", frame=f); em.inputs[1].keyframe_insert("default_value", frame=f)

# 충격파 고리
bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=0.035, major_segments=64, minor_segments=8, location=(0, 0, .04)); ring = bpy.context.object
ring.data.materials.append(mat("ring", (1, .55, .2, 1), 2.5, 0.7))
for f, s in ((1, .1), (5, .8), (9, 1.5), (12, 1.9)): key(ring, "scale", f, (s, s, 1))
# 지면 섬광 원판
bpy.ops.mesh.primitive_circle_add(vertices=48, radius=1, fill_type="NGON", location=(0, 0, 0.02)); fl = bpy.context.object
fl.data.materials.append(mat("flash", (1, .6, .2, 1), 1.2, 0.35))
for f, s in ((1, .2), (3, 1.0), (5, 1.3), (7, 1.4)): key(fl, "scale", f, (s, s, 1))
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
for f, e in ((1, 0), (3, 1500), (8, 300), (12, 0)): L.data.energy = e; L.data.keyframe_insert("energy", frame=f)
bpy.ops.object.camera_add(location=(0, -4.6, 2.2), rotation=(math.radians(72), 0, 0)); cam = bpy.context.object; sc.camera = cam
cam.data.lens = 32

# 컴포지터 글로우
try:
    sc.use_nodes = True
    nt = sc.node_tree; rl = nt.nodes["Render Layers"]; gl = nt.nodes.new("CompositorNodeGlare"); gl.glare_type = "FOG_GLOW"; gl.threshold = 0.8
    cp = nt.nodes["Composite"]; nt.links.new(rl.outputs[0], gl.inputs[0]); nt.links.new(gl.outputs[0], cp.inputs[0])
except Exception as e: print("glare skip", e)

for f in range(1, N + 1):
    sc.frame_set(f); sc.render.filepath = f"{out}/frames/bomb_{f:02d}.png"; bpy.ops.render.render(write_still=True)
print("DONE", out)
