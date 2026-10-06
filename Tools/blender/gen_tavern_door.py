"""선술집 쌍여닫이 문 그림(blender 세션 2026-10-06, PM 지시 「첫 화면 → 게임/대기실 문 열림 연출」).
산출 ~/GRD_tavern_door/ : door_left.png · door_right.png(각 1920×2160 정면, 문짝 하나가 화면 절반 960×1080 의 2배) ·
  door_gap_light.png(문틈 빛 한 장, 1920×2160 가운데 세로 줄, 투명 배경) · door_left_45/75.png · door_right_45/75.png(안쪽으로 젖힌 각도).
화면 없이(정본): blender -b --factory-startup --python Tools/blender/gen_tavern_door.py -- ~/GRD_tavern_door
좌표: 문 전체 x −1..1(왼짝 −1..0 · 오른짝 0..1), 높이 z 0..H, 앞(보는 쪽)이 −Y. 각 짝은 폭 1 × 높이 H=1.125(= 960:1080).
구성: 세로 널판 6장(틈으로 뒤의 따뜻한 불빛이 비침) · 쇠띠 3줄 + 못 · 경첩 통 · 안쪽 가장자리 손잡이 고리 · 문틀 바깥 테두리."""
import math, os, random, sys
import bpy, numpy as np

out = os.path.expanduser(sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "~/GRD_tavern_door"); os.makedirs(out, exist_ok=True)
W, H = 1.0, 1.125
RX, RY = 1920, 2160
random.seed(11)
bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try: sc.render.engine = e; break
    except TypeError: pass
sc.render.resolution_x, sc.render.resolution_y = RX, RY
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"; sc.view_settings.exposure = -0.2
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.06, .05, .05, 1)

def nodes(m): m.use_nodes = True; nt = m.node_tree; return nt, next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")

def wood_mat(name, seed):
    m = bpy.data.materials.new(name); nt, b = nodes(m)
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs[3].default_value = (14, 1.5, 1.2)               # 결을 위아래(Z)로 길게
    mp.inputs[1].default_value = (seed * 3.7, seed * 1.3, 0)
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 6; nz.inputs["Detail"].default_value = 8
    wv = nt.nodes.new("ShaderNodeTexWave"); wv.wave_type = "BANDS"; wv.bands_direction = "X"; wv.inputs["Scale"].default_value = 5
    wv.inputs["Distortion"].default_value = 6; wv.inputs["Detail"].default_value = 3
    nt.links.new(tc.outputs["Object"], mp.inputs[0]); nt.links.new(mp.outputs[0], nz.inputs["Vector"]); nt.links.new(mp.outputs[0], wv.inputs["Vector"])
    mix = nt.nodes.new("ShaderNodeMath"); mix.operation = "MULTIPLY_ADD"; mix.inputs[1].default_value = .5
    nt.links.new(nz.outputs[0], mix.inputs[0]); nt.links.new(wv.outputs[0], mix.inputs[2])
    cr = nt.nodes.new("ShaderNodeValToRGB"); e = cr.color_ramp
    e.elements[0].color = (0.035, 0.016, 0.007, 1); e.elements[1].color = (0.10 + .015 * (seed % 3), 0.05, 0.02, 1)
    e.elements[0].position = .25; e.elements[1].position = .85
    nt.links.new(mix.outputs[0], cr.inputs[0]); nt.links.new(cr.outputs[0], b.inputs[0])
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = .6; bp.inputs["Distance"].default_value = .02
    nt.links.new(mix.outputs[0], bp.inputs["Height"]); nt.links.new(bp.outputs[0], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = .75
    return m

def iron_mat():
    m = bpy.data.materials.new("iron"); nt, b = nodes(m)
    b.inputs[0].default_value = (.045, .045, .05, 1); b.inputs["Metallic"].default_value = .85; b.inputs["Roughness"].default_value = .42
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 40; bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = .25; bp.inputs["Distance"].default_value = .004
    nt.links.new(nz.outputs[0], bp.inputs["Height"]); nt.links.new(bp.outputs[0], b.inputs["Normal"])
    return m

def glow_mat(name, col, st):
    m = bpy.data.materials.new(name); nt, b = nodes(m)
    for n in list(nt.nodes):
        if n.type != "OUTPUT_MATERIAL": nt.nodes.remove(n)
    o = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"); em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = col; em.inputs[1].default_value = st
    nt.links.new(em.outputs[0], o.inputs[0]); return m

IRON = iron_mat()
def box(name, size, loc, mat, bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.name = name; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        bv = o.modifiers.new("b", "BEVEL"); bv.width = bevel; bv.segments = 2
    o.data.materials.append(mat); return o
def cyl(name, r, d, loc, rot, mat, verts=24):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d, vertices=verts, location=loc, rotation=rot); o = bpy.context.object; o.name = name
    bpy.ops.object.shade_smooth(); o.data.materials.append(mat); return o

def build_leaf(side):
    """side −1=왼짝(경첩 x=−1, 손잡이 x≈−0.06) · +1=오른짝(경첩 x=+1). 반환: 짝 전체 부모 Empty(경첩 축)."""
    hx = side * 1.0                       # 경첩 가장자리
    inner = 1.0 if side == -1 else -1.0   # 경첩에서 중앙 쪽 방향(+x/−x)
    root = bpy.data.objects.new(f"leaf{side}", None); sc.collection.objects.link(root); root.location = (hx, 0, 0)
    kids = []
    n = 6; pw = W / n
    for i in range(n):
        cx = inner * (pw * (i + .5))
        pl = box(f"plank{side}_{i}", (pw - .014, .07, H - .004 * (i % 2)), (hx + cx, 0, H / 2 - .002 * (i % 2)), wood_mat(f"wood{side}{i}", i + 5 * (side + 2)), .006)
        kids.append(pl)
    # 뒤 불빛판(널판 틈으로 보인다) — 가운데 이음선 쪽이 더 밝다
    bk = bpy.data.objects.new("x", None)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(hx + inner * W / 2, .09, H / 2)); g = bpy.context.object
    g.rotation_euler = (math.radians(90), 0, 0); g.scale = (W, H, 1); g.name = f"backglow{side}"
    gm = bpy.data.materials.new("bg"); nt, b = nodes(gm)
    for nd in list(nt.nodes):
        if nd.type != "OUTPUT_MATERIAL": nt.nodes.remove(nd)
    o = next(nd for nd in nt.nodes if nd.type == "OUTPUT_MATERIAL"); em = nt.nodes.new("ShaderNodeEmission"); em.inputs[1].default_value = 2.2
    tc = nt.nodes.new("ShaderNodeTexCoord"); sp = nt.nodes.new("ShaderNodeSeparateXYZ"); cr = nt.nodes.new("ShaderNodeValToRGB")
    nt.links.new(tc.outputs["UV"], sp.inputs[0])
    cr.color_ramp.elements[0].color = (.4, .08, .01, 1); cr.color_ramp.elements[1].color = (.95, .45, .1, 1)
    # UV.x: 평면의 +x가 월드 +x. 왼짝은 오른쪽(1)이 중앙, 오른짝은 왼쪽(0)이 중앙.
    if side == -1: nt.links.new(sp.outputs[0], cr.inputs[0])
    else:
        inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1; nt.links.new(sp.outputs[0], inv.inputs[1]); nt.links.new(inv.outputs[0], cr.inputs[0])
    nt.links.new(cr.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], o.inputs[0]); g.data.materials.append(gm); kids.append(g)
    # 쇠띠 3줄 + 못
    for zc in (.17, H / 2 + .02, H - .17):
        s = box(f"strap{side}", (W * .86, .02, .075), (hx + inner * W * .43, -.045, zc), IRON, .003); kids.append(s)
        tip = cyl("tip", .0375, .02, (hx + inner * W * .86, -.045, zc), (math.radians(90), 0, 0), IRON, 6); tip.scale = (1.2, 1, 1); kids.append(tip)
        for k in range(7):
            rv = cyl("rv", .011, .012, (hx + inner * (.08 + k * .12), -.058, zc), (math.radians(90), 0, 0), IRON, 10); kids.append(rv)
        hn = cyl("hinge", .028, .12, (hx + inner * .004, -.055, zc), (0, 0, 0), IRON, 14); kids.append(hn)  # 경첩 통
        kn = cyl("knuckle", .034, .02, (hx + inner * .004, -.055, zc + .06), (0, 0, 0), IRON, 14); kids.append(kn)
    # 안쪽 가장자리 세로 보강대 + 손잡이 고리
    ed = box(f"edge{side}", (.035, .045, H), (hx + inner * (W - .0175), -.04, H / 2), wood_mat(f"edgewood{side}", 9), .006); kids.append(ed)
    px_ = hx + inner * (W - .11)
    pl = box("hp", (.10, .02, .14), (px_, -.052, H * .48), IRON, .012); kids.append(pl)
    bpy.ops.mesh.primitive_torus_add(major_radius=.062, minor_radius=.011, major_segments=40, minor_segments=10, location=(px_, -.085, H * .48 - .045), rotation=(math.radians(90), 0, 0)); rg = bpy.context.object
    bpy.ops.object.shade_smooth(); rg.data.materials.append(IRON); kids.append(rg)
    kn = cyl("mount", .016, .035, (px_, -.065, H * .48 + .01), (math.radians(90), 0, 0), IRON, 12); kids.append(kn)
    for k in kids: k.parent = root; k.matrix_parent_inverse = root.matrix_world.inverted()
    return root

L = build_leaf(-1); R = build_leaf(1)
# 조명: 노을 쪽(왼쪽 위 앞) 따뜻한 키 + 아래서 오는 약한 주황 반사 + 오른쪽 차가운 보조
def area(name, loc, rot, e, col, size):
    bpy.ops.object.light_add(type="AREA", location=loc, rotation=rot); l = bpy.context.object; l.name = name
    l.data.energy = e; l.data.color = col; l.data.size = size; return l
area("key", (-2.5, -3.5, 2.6), (math.radians(65), 0, math.radians(-35)), 420, (1, .72, .45), 3)
area("fill", (2.8, -3.0, .6), (math.radians(85), 0, math.radians(40)), 120, (.55, .65, 1), 3)
area("bounce", (0, -2.2, -.3), (math.radians(110), 0, 0), 60, (1, .6, .25), 4)
bpy.ops.object.camera_add(); cam = bpy.context.object; cam.data.type = "ORTHO"; cam.data.ortho_scale = H; sc.camera = cam
cam.rotation_euler = (math.radians(90), 0, 0)

def render(leaf, cx, path, other=None):
    for o in (L, R):
        for c in o.children_recursive: c.hide_render = (o is not leaf)
    cam.location = (cx, -6, H / 2); sc.render.filepath = path; bpy.ops.render.render(write_still=True)

# 정면 + 안쪽으로 젖힌 45·75°(경첩 축 회전, +Y쪽으로 열림)
for ang in (0, 45, 75):
    suffix = "" if ang == 0 else f"_{ang}"
    for leaf, nm, cx in ((L, "left", -.5), (R, "right", .5)):
        s = leaf.location.x
        leaf.rotation_euler = (0, 0, math.radians(-ang if s < 0 else ang))   # 왼짝: 시계 방향이 안쪽(뒤)으로 열림
        render(leaf, cx, f"{out}/door_{nm}{suffix}.png")
    L.rotation_euler = R.rotation_euler = (0, 0, 0)

# 문틈 빛: 가운데 세로 줄(문을 열기 시작할 때 이음선에서 새는 빛)
x = (np.arange(RX) - (RX - 1) / 2) / (RX / 2)            # −1..1 (한 장이 문 전체 폭을 덮는 1920×2160)
y = np.linspace(0, 1, RY)[:, None]
core = np.exp(-(x / 0.012) ** 2); halo = np.exp(-(x / 0.12) ** 2) * .55
fade = (0.55 + .45 * np.sin(np.pi * np.clip(y, 0, 1))) * np.clip(1.15 - 0.25 * np.abs(y - .5) * 2, 0, 1)
a = np.clip((core + halo)[None, :] * fade, 0, 1)
col = np.stack([np.ones_like(a), .62 + .3 * core[None, :] * np.ones_like(a), .18 + .5 * core[None, :] * np.ones_like(a), a], -1).clip(0, 1)
img = bpy.data.images.new("gap", RX, RY, alpha=True); img.pixels = np.flipud(col).astype(np.float32).ravel()
img.filepath_raw = f"{out}/door_gap_light.png"; img.file_format = "PNG"; img.save()
print("DONE", out)
