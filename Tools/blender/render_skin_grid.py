"""원작 유닛 스킨 격자 사진(2026-10-09, blender).
  blender -b --factory-startup --python Tools/blender/render_skin_grid.py -- <items.json> <출력폴더> [격자 .blend 경로]
items: [{model,ids,names,ours,fbx,status}]. 각 FBX를 Stand 시퀀스 첫 프레임 자세로 세워 눈높이 정규화 후 개별 PNG(출력폴더/<번호>_<모델>.png)를 렌더하고,
검사 결과(JSON: 부품 메시 수·뼈·텍스처 누락·Image_N·뒤집힌 면 비율)를 skin_check.json에 쓴다. .blend 경로를 주면 전원을 격자로 세운 장면도 저장(사장님 창에서 열기용)."""
import bpy, json, math, os, sys
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "w3x")))
import mdx_anim
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
ITEMS = json.load(open(args[0])); OUT = args[1]; BLEND = args[2] if len(args) > 2 else None
os.makedirs(OUT, exist_ok=True)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    try: sc.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError: sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 360, 440
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w"); w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.57, 0.62, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.0; sc.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sc.collection.objects.link(sun)
    return sc

def stand_pose(arm):
    act = None
    for a in bpy.data.actions:
        if "stand" in a.name.lower(): act = a; break
    if act is None and bpy.data.actions: act = bpy.data.actions[0]
    if arm and act:
        arm.animation_data_create(); arm.animation_data.action = act; bpy.context.scene.frame_set(int(act.frame_range[0]))
    return act.name if act else None

def hide_by_geoset_alpha(new, fbx):
    """원작 Stand 첫 시각에 지오셋·층 알파가 0인 메시(숨겨진 무기·변신체)를 렌더에서 뺀다. 키가 첫 키보다 앞이면 정적 알파(1.0)."""
    side = os.path.join(os.path.dirname(fbx), os.path.basename(fbx)[:-4] + ".json")
    if not os.path.exists(side): return 0
    sd = json.load(open(side)); mdx = None
    for w in ("original_skin", "original_vfx"):
        p = os.path.expanduser(f"~/GRD_motion_trial/{w}/work/" + sd["model"])
        if os.path.exists(p): mdx = p; break
    if not mdx: return 0
    info = mdx_anim.describe(open(mdx, "rb").read())
    st = next((q for q in info["sequences"] if "stand" in q["name"].lower()), info["sequences"][0] if info["sequences"] else None)
    t = st["start"] if st else 0; n = 0
    byname = {m["mesh"]: m for m in sd["meshes"]}
    def alpha_at(keys, default):
        if not keys or t < keys[0][0]: return default
        v = keys[0][1][0]
        for k in keys:
            if k[0] <= t: v = k[1][0]
        return v
    for o in new:
        if o.type != "MESH": continue
        m = next((byname[k] for k in byname if o.name.startswith(k)), None)
        if not m: continue
        ga = alpha_at(m.get("geosetAlphaKeys"), m.get("geosetAlphaStatic") if m.get("geosetAlphaStatic") is not None else 1.0)
        la = alpha_at(m.get("layerAlphaKeys"), 1.0)
        if ga * la < 0.05: o.hide_render = True; o.hide_viewport = True; n += 1
    return n

def bbox(objs):
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in objs:
        if o.type == "MESH" and not o.hide_render:
            ev = o.evaluated_get(dg); pts += [o.matrix_world @ Vector(c) for c in ev.bound_box]
    if not pts: return None
    return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))

def check(objs, fbx):
    meshes = [o for o in objs if o.type == "MESH"]; arm = [o for o in objs if o.type == "ARMATURE"]
    tex_missing, image_n, flipped, tris = 0, 0, 0, 0
    for o in meshes:
        for s in o.material_slots:
            m = s.material
            if m and m.use_nodes:
                for n in m.node_tree.nodes:
                    if n.type == "TEX_IMAGE":
                        if n.image is None: tex_missing += 1
                        else:
                            if n.image.name.startswith("Image_") or n.image.name.startswith("Image."): image_n += 1
                            p = bpy.path.abspath(n.image.filepath)
                            if p and not os.path.exists(p): tex_missing += 1
        me = o.data; tris += len(me.polygons)
        # 법선이 메시 중심에서 바깥을 보는 비율(뒤집힘 추정)
        c = sum((v.co for v in me.vertices), Vector()) / max(len(me.vertices), 1); bad = 0
        for p in me.polygons:
            if (p.center - c).dot(p.normal) < 0: bad += 1
        flipped += bad
    return dict(meshes=len(meshes), bones=sum(len(a.data.bones) for a in arm), texMissing=tex_missing, imageN=image_n, tris=tris, inwardFacePct=round(100 * flipped / max(tris, 1), 1))

report = []; grid = []
for i, it in enumerate(ITEMS):
    if not it.get("fbx"): continue
    sc = reset(); before = set(bpy.data.objects)
    try: bpy.ops.import_scene.fbx(filepath=it["fbx"])
    except Exception as e: report.append(dict(model=it["model"], error=str(e)[:100])); continue
    new = [o for o in bpy.data.objects if o not in before and o.name not in ("sun",)]
    arm = next((o for o in new if o.type == "ARMATURE"), None); an = stand_pose(arm); hidden = hide_by_geoset_alpha(new, it["fbx"]); bpy.context.view_layer.update()
    bb = bbox(new)
    if not bb: report.append(dict(model=it["model"], error="메시 없음")); continue
    lo, hi = bb; h = max(hi.z - lo.z, 1e-6); c = (lo + hi) / 2
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = "ORTHO"
    d = Vector((0.0, -1.0, 0.35)).normalized(); cam.location = c + d * h * 6; cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler(); cam.data.ortho_scale = max(h, (hi.x - lo.x), (hi.y - lo.y)) * 1.25; cam.data.clip_end = h * 50
    sc.render.filepath = f"{OUT}/{i:02d}.png"; bpy.ops.render.render(write_still=True)
    r = check(new, it["fbx"]); r.update(model=it["model"], stand=an, hiddenByAlpha=hidden, heightM=round(h, 3), png=f"{i:02d}.png"); report.append(r)
json.dump(report, open(f"{OUT}/skin_check.json", "w"), ensure_ascii=False, indent=1)
print("rendered", len(report))
