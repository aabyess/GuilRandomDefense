"""구랜디 현재 스킨 격자 사진(2026-10-18, blender) — S2 신규 스킨 격자(render_skin_grid_s2.py)와 같은 배경·칸 크기.
  blender -b --factory-startup --python Tools/blender/render_ours_grid.py -- <items.json> <출력폴더>
items: [{grade,name,fbx}] (Assets/Prefabs/Generated/Unit_<등급>_<이름>.prefab이 쓰는 모델 FBX, Tools/blender/ours_grid_items.py가 뽑는다).
Idle/Stand 액션이 있으면 그 첫 프레임, 없으면 FBX 휴식 자세(우리 쪽 대부분은 공용 idle.fbx로 게임에서 움직인다). 정면 = Unity +Z = 카메라가 보는 쪽(-Y에서 바라봄).
출력폴더/<번호>.png + skin_check.json."""
import bpy, json, math, os, sys
from mathutils import Vector
args = sys.argv[sys.argv.index("--") + 1:]
ITEMS = json.load(open(args[0])); OUT = args[1]
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
    for key in ("stand", "idle"):
        for a in bpy.data.actions:
            if key in a.name.lower(): act = a; break
        if act: break
    if arm and act:
        arm.animation_data_create(); arm.animation_data.action = act; bpy.context.scene.frame_set(int(act.frame_range[0]))
    return act.name if act else None

def texture_fallback(new, fbx):
    """FBX가 텍스처 경로를 못 잇는 우리 쪽 유닛(Assets/Art/Units/<이름>/Textures): 재질 이름으로 Base_Color 이미지를 찾아 붙인다(렌더 전용)."""
    tdir = os.path.join(os.path.dirname(fbx), "Textures")
    if not os.path.isdir(tdir): return 0
    files = os.listdir(tdir); n = 0
    for o in new:
        if o.type != "MESH": continue
        for sl in o.material_slots:
            m = sl.material
            if not m or not m.use_nodes: continue
            if any(nd.type == "TEX_IMAGE" and nd.image and os.path.exists(bpy.path.abspath(nd.image.filepath)) for nd in m.node_tree.nodes): continue
            base = m.name.split(".")[0].lower()
            cand = [f for f in files if not f.endswith(".meta") and f.lower().rsplit(".", 1)[0] in (base, base + "_base_color", base + "_basecolor", base + "_albedo", base + "_diffuse", base + "_d")]
            cand = cand or [f for f in files if not f.endswith(".meta") and base in f.lower() and ("base_color" in f.lower() or "albedo" in f.lower() or "diffuse" in f.lower())]
            if not cand: continue
            bs = next((nd for nd in m.node_tree.nodes if nd.type == "BSDF_PRINCIPLED"), None)
            if not bs: continue
            tn = m.node_tree.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images.load(os.path.join(tdir, cand[0]))
            m.node_tree.links.new(tn.outputs["Color"], bs.inputs["Base Color"]); n += 1
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
    except Exception as e: report.append(dict(model=it["name"], error=str(e)[:100])); continue
    new = [o for o in bpy.data.objects if o not in before and o.name not in ("sun",)]
    arm = next((o for o in new if o.type == "ARMATURE"), None); an = stand_pose(arm); hidden = 0; texture_fallback(new, it["fbx"]); bpy.context.view_layer.update()
    bb = bbox(new)
    if not bb: report.append(dict(model=it["name"], error="메시 없음")); continue
    lo, hi = bb; h = max(hi.z - lo.z, 1e-6); c = (lo + hi) / 2
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = "ORTHO"
    d = Vector((0.0, -1.0, 0.35)).normalized(); cam.location = c + d * h * 6; cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler(); cam.data.ortho_scale = max(h, (hi.x - lo.x), (hi.y - lo.y)) * 1.25; cam.data.clip_end = h * 50
    sc.render.filepath = f"{OUT}/{i:02d}.png"; bpy.ops.render.render(write_still=True)
    r = check(new, it["fbx"]); r.update(model=it["name"], grade=it["grade"], stand=an, hiddenByAlpha=hidden, heightM=round(h, 3), png=f"{i:02d}.png"); report.append(r)
json.dump(report, open(f"{OUT}/skin_check.json", "w"), ensure_ascii=False, indent=1)
print("rendered", len(report))
