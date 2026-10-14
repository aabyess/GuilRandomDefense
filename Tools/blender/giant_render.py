"""거구 후보 키 비교 렌더(blender -b --python giant_render.py -- <out_dir>). Assets 안 씀.
기준: 키 = 메시 경계 높이(ArtBinder.UnitHeight 48 × 등급배수 — 흔함_강재규 0.68, 초월 ×1.2·김민준 ×1.07, 이이삭·박은석 ×1.2, 임채준(특별함) ×1.0)."""
import bpy, sys, os, json, math, mathutils
OUT = sys.argv[sys.argv.index("--") + 1]; os.makedirs(OUT, exist_ok=True)
U = "/Users/sang/GitHub/GuilRandomDefense/Assets/Art/Units"
BASE = 48.0
REFS = [("흔함_강재규", BASE * 0.68), ("초월_김민준_AP", BASE * 1.2 * 1.07)]
CANDS = [("불멸_이이삭", "흰수염", BASE * 1.2), ("전설적인_박은석", "쿠마", BASE * 1.2), ("특별함_임채준", "모리아", BASE * 1.0)]
ORIG = [("war3bhz1", "h03B_에드워드_뉴게이트_사황_흰수염_해적단의_아버지_-_전설적인", 1.4, "흰수염 전설h03B"),
        ("Xiong10", "h030_바솔로뮤_쿠마_폭군_-_전설적인", 1.6, "쿠마 h030"),
        ("MoriaMorp8", "h02N_겟코_모리아_쉐도우_아스가르드_-_전설적인", 1.6, "모리아 h02N"),
        ("ygmny2", "H0BB_흔함영웅", 0.96, "모리아 특별 h00B(ygmny2)"),
        ("Nami2", "H081_흔함영웅", 1.0, "나미 흔함(기준)")]
def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
def load(fbx):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    new = [o for o in bpy.data.objects if o not in before]
    return new
def texture_up(name, objs):
    d = f"{U}/{name}/Textures"; pngs = [f for f in os.listdir(d) if f.lower().endswith((".png",".jpg")) and not any(k in f.lower() for k in ("height","metallic","mixed_ao","normal","roughness"))] if os.path.isdir(d) else []
    st = {}
    try: st = json.load(open(f"{U}/{name}/slot_textures.json"))
    except Exception: pass
    for o in objs:
        if o.type != "MESH": continue
        for sl in o.material_slots:
            m = sl.material
            if not m: continue
            key = m.name.split(".0")[0]; pick = None
            tex = st.get(key, {}).get("tex")
            if tex:
                stem = os.path.splitext(tex)[0].lower()
                for f in pngs:
                    if f.lower().startswith(stem): pick = f; break
            if not pick:
                for f in pngs:
                    if os.path.splitext(os.path.splitext(f)[0])[0].lower() in key.lower() or key.lower() in f.lower(): pick = f; break
            if not pick and len(pngs) == 1: pick = pngs[0]
            if not pick: continue
            m.use_nodes = True; nt = m.node_tree
            for n in list(nt.nodes):
                if n.type == "TEX_IMAGE": nt.nodes.remove(n)
            bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(f"{d}/{pick}")
            if bsdf: nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
            m.diffuse_color = (1, 1, 1, 1)
def bbox(objs):
    pts = []
    for o in objs:
        if o.type == "MESH":
            pts += [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
    xs, ys, zs = zip(*[(p.x, p.y, p.z) for p in pts])
    return min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)
def root_of(objs):
    return [o for o in objs if o.parent is None]
# --- A: 원작 높이 실측 ---
res = {}
for mdl, folder, usca, label in ORIG:
    clear(); p = os.path.expanduser(f"~/Desktop/구랜디스킨모음/15_원랜디스킨/{folder}/{mdl}.fbx")
    objs = load(p); b = bbox(objs); h = b[5] - b[4]
    res[label] = dict(model=mdl, rawHeightM=round(h, 3), usca=usca, heightWithScale=round(h * usca, 3))
json.dump(res, open(OUT + "/원작높이.json", "w"), ensure_ascii=False, indent=1)
# --- B: 현재 모델 높이 + 렌더 ---
cur = {}
def build(items, tag):
    clear(); bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
    x = 0.0; placed = []
    for name, label, tgt in items:
        objs = load(f"{U}/{name}/{name}.fbx"); texture_up(name, objs); b = bbox(objs); h0 = b[5] - b[4]
        s = tgt / h0
        emp = bpy.data.objects.new("grp", None); bpy.context.scene.collection.objects.link(emp)
        for r in root_of(objs): r.parent = emp
        emp.scale = (s, s, s); bpy.context.view_layer.update()
        b = bbox(objs); w = b[1] - b[0]
        cx = (b[0] + b[1]) / 2; cy = (b[2] + b[3]) / 2
        emp.location = mathutils.Vector((x - cx, -cy, -b[4]))
        bpy.context.view_layer.update()
        placed.append((label, name, tgt, x)); x += max(w, 20) * 0.5 + 40; 
        cur[name] = dict(rawHeight=round(h0, 3), targetHeight=round(tgt, 2))
    return placed
def shoot(path, items, fovfit):
    placed = build(items, "")
    sc = bpy.context.scene; sc.render.resolution_x = 1800; sc.render.resolution_y = 700
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.film_transparent = False; sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.72, 0.74, 0.78)
    sc.render.image_settings.file_format = "PNG"
    # 바닥
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, -0.01)); g = bpy.context.object; g.scale = (2000, 400, 1)
    xs = [p[3] for p in placed]; cx = (min(xs) + max(xs)) / 2; span = max(xs) - min(xs) + 120
    cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.clip_end = 5000; cam.clip_start = 1; cam.ortho_scale = max(span, 130 * 1.6 * 1.0) * 1.0
    co = bpy.data.objects.new("c", cam); bpy.context.collection.objects.link(co); sc.camera = co
    co.location = (cx, -600, 55); co.rotation_euler = (math.radians(90), 0, 0)
    bpy.context.view_layer.update()
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    return placed
meta = {}
for name, who, base_h in CANDS:
    items = [(r, r, h) for r, h in REFS]
    items += [(f"{name}", f"{who} 지금", base_h), (name, f"{who} ×1.3", base_h * 1.3), (name, f"{who} ×1.6", base_h * 1.6)]
    placed = shoot(f"{OUT}/_raw_{who}.png", items, 1)
    meta[who] = [dict(label=l, unit=n, height=round(t, 1), x=round(x, 1)) for l, n, t, x in placed]
json.dump(dict(cur=cur, meta=meta), open(OUT + "/meta.json", "w"), ensure_ascii=False, indent=1)
