"""적 출발점 바닥 포탈(blender 세션 2026-10-07, PM 지시 · 규격 구현담당1): 원작 웨이게이트풍 바닥 마법진.
지름 1 단위 모델(구현담당1이 ×56). 오브젝트 둘: 「돌판」(원판 메시, 원점 = 아랫면 가운데) · 「문양」(얇은 평면, 원점 가운데, 돌판 윗면 위에 따로 — Unity에서 회전).
둘 다 XZ 평면에 눕고 법선 +Y(Unity 기준; Blender에선 +Z, FBX axis_up=Y로 내보냄).
텍스처: portal_stone.png(돌판, 1024 타일) · portal_glyph.png(문양, 1024 RGBA: 검정 바탕 = 알파 0, 푸른/흰 빛) — Additive 셰이더 가정.
1) /usr/bin/python3 Tools/blender/gen_enemy_portal.py tex [~/GRD_enemy_portal]   → 텍스처 두 장
2) blender -b --factory-startup --python Tools/blender/gen_enemy_portal.py -- [~/GRD_enemy_portal]   → enemy_portal.fbx · .glb · preview.png
"""
import os, sys, math
if len(sys.argv) > 1 and sys.argv[1] == 'tex':                                   # ---------------- 텍스처(PIL)
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    D = os.path.expanduser(sys.argv[2] if len(sys.argv) > 2 else '~/GRD_enemy_portal'); os.makedirs(D, exist_ok=True)
    rng = np.random.RandomState(56); N = 1024
    def fnoise(cells, oct_=5):                                                    # 이음새 없는(주기) 값 소음
        acc = np.zeros((N, N)); amp = 1; tot = 0
        for o in range(oct_):
            c = cells * 2 ** o; g = rng.rand(c, c); g = np.pad(g, ((0, 1), (0, 1)), mode='wrap')
            im = Image.fromarray((g * 255).astype(np.uint8)).resize((N + N // c, N + N // c), Image.BICUBIC).crop((0, 0, N, N))
            acc += np.asarray(im, np.float32) / 255 * amp; tot += amp; amp *= .55
        return acc / tot
    n1 = fnoise(4); n2 = fnoise(32, 3)
    base = np.array([96, 98, 104]) * (.65 + .55 * n1[..., None]) * (.85 + .3 * n2[..., None])
    # 큰 판석 줄눈(타일 주기): 4×4 판석, 줄눈 어둡게
    yy, xx = np.mgrid[0:N, 0:N]; jx = (xx % 256 < 5) | (yy % 256 < 5); off = ((yy // 256) % 2) * 128; jx2 = ((xx + off) % 256 < 5)
    joint = (yy % 256 < 5) | jx2
    base[joint] *= .28
    Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).save(D + '/portal_stone.png')
    # 문양
    S = 2048; im = Image.new('RGB', (S, S), (0, 0, 0)); d = ImageDraw.Draw(im); c = S / 2
    def ring(r, w, col): d.ellipse((c - r, c - r, c + r, c + r), outline=col, width=w)
    BL = (90, 170, 255); WH = (220, 240, 255); DB = (40, 90, 200)
    ring(980, 10, BL); ring(940, 4, WH); ring(760, 6, BL); ring(720, 3, DB); ring(420, 5, WH); ring(390, 3, BL); ring(160, 4, WH)
    for k in range(48):                                                            # 바깥 띠 룬 눈금
        a = 2 * math.pi * k / 48; r0, r1 = 760, 940; L = 60 if k % 2 == 0 else 30
        x0, y0 = c + (r0 + 20) * math.cos(a), c + (r0 + 20) * math.sin(a); x1, y1 = c + (r0 + 20 + L * 2) * math.cos(a), c + (r0 + 20 + L * 2) * math.sin(a)
        d.line((x0, y0, x1, y1), fill=WH if k % 4 == 0 else BL, width=6 if k % 4 == 0 else 3)
        if k % 4 == 2:
            mx_, my_ = c + 850 * math.cos(a), c + 850 * math.sin(a); s_ = 26
            d.polygon([(mx_, my_ - s_), (mx_ + s_, my_), (mx_, my_ + s_), (mx_ - s_, my_)], outline=WH, width=4)
    def star(n, r_out, r_in, rot, col, w):
        pts = []
        for k in range(2 * n):
            r_ = r_out if k % 2 == 0 else r_in; a = rot + math.pi * k / n; pts.append((c + r_ * math.cos(a), c + r_ * math.sin(a)))
        d.line(pts + [pts[0]], fill=col, width=w)
    star(8, 720, 420, 0, BL, 6); star(8, 720, 420, math.pi / 8, DB, 3)
    for k in range(6):                                                              # 안쪽 육각 + 꼭짓점 원
        a = 2 * math.pi * k / 6 - math.pi / 2; b = 2 * math.pi * (k + 1) / 6 - math.pi / 2
        d.line((c + 390 * math.cos(a), c + 390 * math.sin(a), c + 390 * math.cos(b), c + 390 * math.sin(b)), fill=WH, width=5)
        px, py = c + 390 * math.cos(a), c + 390 * math.sin(a); d.ellipse((px - 22, py - 22, px + 22, py + 22), outline=WH, width=4)
        d.line((c, c, c + 160 * math.cos(a), c + 160 * math.sin(a)), fill=BL, width=4)
    core = im.filter(ImageFilter.GaussianBlur(3))
    glow = im.filter(ImageFilter.GaussianBlur(24))
    a = np.asarray(core, np.float32) * 1.0 + np.asarray(glow, np.float32) * 1.6
    rr = np.sqrt(((np.mgrid[0:S, 0:S][1] - c) / c) ** 2 + ((np.mgrid[0:S, 0:S][0] - c) / c) ** 2)
    a += (np.clip(1 - rr / .2, 0, 1) ** 2)[..., None] * np.array([60, 120, 220])                 # 가운데 은은한 빛
    a = np.clip(a, 0, 255); alpha = np.clip(a.max(-1), 0, 255)
    out = np.dstack([a, alpha]).astype(np.uint8)
    Image.fromarray(out).resize((1024, 1024), Image.LANCZOS).save(D + '/portal_glyph.png'); print('tex saved'); sys.exit(0)

import bpy, bmesh
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if argv else '~/GRD_enemy_portal')
bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
def img_mat(name, path, emit=False):
    m = bpy.data.materials.new(name); m.use_nodes = True; t = m.node_tree; b = next(n for n in t.nodes if n.type == 'BSDF_PRINCIPLED')
    tx = t.nodes.new('ShaderNodeTexImage'); tx.image = bpy.data.images.load(path); t.links.new(tx.outputs['Color'], b.inputs['Base Color'])
    if emit:
        t.links.new(tx.outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 3.0; t.links.new(tx.outputs['Alpha'], b.inputs['Alpha'])
        try: m.blend_method = 'BLEND'
        except Exception: pass
    b.inputs['Roughness'].default_value = .85
    return m
# 돌판: 지름 1, 두께 0.03. 가장자리 바깥 띠(조금 높게 0.04) + 안쪽 바닥(0.025) — 하나의 메시
bm = bmesh.new(); seg = 96
def disc(r, z0, z1):
    g = bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=z1 - z0); bmesh.ops.translate(bm, vec=(0, 0, (z0 + z1) / 2), verts=g['verts'])
disc(.5, 0, .04); disc(.47, 0.0, .042)
me = bpy.data.meshes.new('돌판'); bm.to_mesh(me); bm.free()
stone = bpy.data.objects.new('돌판', me); sc.collection.objects.link(stone)
# 바깥 띠는 따로 높이: 안쪽 원판을 둘러싼 링(돌 블록 16개)
bm = bmesh.new()
for k in range(16):
    a0 = 2 * math.pi * k / 16 + .01; a1 = 2 * math.pi * (k + 1) / 16 - .01; vs = []
    for (r_, z_) in ((.5, 0), (.5, .055), (.44, .055), (.44, 0)):
        pass
    ring = []
    for i in range(7):
        a = a0 + (a1 - a0) * i / 6
        ring.append([bm.verts.new((r_ * math.cos(a), r_ * math.sin(a), z_)) for r_, z_ in ((.5, .0), (.5, .055), (.435, .055), (.435, .0))])
    for i in range(6):
        for j in range(4): bm.faces.new((ring[i][j], ring[i][(j + 1) % 4], ring[i + 1][(j + 1) % 4], ring[i + 1][j]))
    bm.faces.new([ring[0][j] for j in range(4)][::-1]); bm.faces.new([ring[6][j] for j in range(4)])
me2 = bpy.data.meshes.new('테'); bm.to_mesh(me2); bm.free(); rim = bpy.data.objects.new('테', me2); sc.collection.objects.link(rim)
bv = rim.modifiers.new('b', 'BEVEL'); bv.width = .006; bv.segments = 2
bpy.context.view_layer.objects.active = rim; rim.select_set(True); bpy.ops.object.modifier_apply(modifier='b')
stone.select_set(True); bpy.context.view_layer.objects.active = stone; bpy.ops.object.join(); stone = bpy.context.object; stone.name = '돌판'; stone.data.name = '돌판'
# UV: 위에서 평면 투영(돌 텍스처 2번 반복)
me = stone.data; uv = me.uv_layers.new(name='UVMap')
for poly in me.polygons:
    for li in poly.loop_indices:
        co = me.vertices[me.loops[li].vertex_index].co; uv.data[li].uv = (co.x * 2 + .5, co.y * 2 + .5)
for p in me.polygons: p.use_smooth = False
stone.data.materials.append(img_mat('portal_stone', OUT + '/portal_stone.png'))
# 문양: 지름 0.94 평면, 원점 가운데, 돌판 안쪽 바닥(0.042) 위 0.003에 놓되 원점은 자기 가운데
bpy.ops.mesh.primitive_plane_add(size=.94, location=(0, 0, .045)); glyph = bpy.context.object; glyph.name = '문양'; glyph.data.name = '문양'
glyph.data.materials.append(img_mat('portal_glyph', OUT + '/portal_glyph.png', emit=True))
# 내보내기: FBX(Y-up, 법선 +Y) · glb
for o in sc.objects: o.select_set(o.name in ('돌판', '문양'))
bpy.ops.export_scene.fbx(filepath=OUT + '/enemy_portal.fbx', use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, path_mode='COPY', embed_textures=False)
bpy.ops.export_scene.gltf(filepath=OUT + '/enemy_portal.glb', use_selection=True, export_yup=True)
# 미리보기(위에서 비스듬히, 어두운 흙바닥)
bpy.ops.mesh.primitive_plane_add(size=4, location=(0, 0, -.001)); fl = bpy.context.object
fm = bpy.data.materials.new('흙'); fm.use_nodes = True; next(n for n in fm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED').inputs['Base Color'].default_value = (.12, .08, .05, 1); fl.data.materials.append(fm)
s = bpy.data.lights.new('sun', 'SUN'); s.energy = 2.0; so = bpy.data.objects.new('sun', s); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.cameras.new('c'); cam.lens = 50; co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co); sc.camera = co; co.location = (0, -1.4, 1.2); co.rotation_euler = (math.radians(50), 0, 0)
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (.02, .025, .04, 1)
sc.render.resolution_x, sc.render.resolution_y = 1280, 900; sc.render.filepath = OUT + '/preview.png'; bpy.ops.render.render(write_still=True); print('done', OUT)
