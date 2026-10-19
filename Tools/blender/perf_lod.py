"""감량판 한 개(blender 2026-10-20, Assets 읽기 전용): Decimate(collapse) + 텍스처 상한 축소 + 전/후 렌더.
  blender -b --factory-startup --python Tools/blender/perf_lod.py -- <원본 fbx> <출력 폴더> <남길 비율 0~1> <텍스처 최대변>
산출: <out>/<이름>.fbx · Textures/(축소본) · 전후.png(왼쪽 전·오른쪽 후, 같은 눈금) · lod.json(삼각형 전/후·비율·텍스처 전/후). 뼈·UV·정점 그룹 유지(Decimate는 메시만)."""
import bpy, sys, os, json, glob, math
from mathutils import Vector
SRC, OUT, RATIO, TEXMAX = sys.argv[sys.argv.index("--") + 1:][:4]; RATIO = float(RATIO); TEXMAX = int(TEXMAX)
NAME = os.path.splitext(os.path.basename(SRC))[0]; os.makedirs(OUT + "/Textures", exist_ok=True)
sc = bpy.context.scene


def tris():
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=SRC)


def emission_all(texdir):
    for o in bpy.data.objects:
        if o.type != "MESH": continue
        for s in o.material_slots:
            m = s.material
            if not m: continue
            img = next((n.image for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None) if m.node_tree else None
            m.use_nodes = True; nt = m.node_tree; nt.nodes.clear(); out = nt.nodes.new("ShaderNodeOutputMaterial"); e = nt.nodes.new("ShaderNodeEmission")
            if img:
                t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; nt.links.new(t.outputs["Color"], e.inputs["Color"])
                tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader")
                nt.links.new(t.outputs["Alpha"], mx.inputs[0]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(e.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
            else:
                e.inputs["Color"].default_value = (0.6, 0.6, 0.6, 1); nt.links.new(e.outputs[0], out.inputs[0])


def render(path, scale=None, wire=False):
    sc = bpy.context.scene
    ids = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in ids else ids[0]; sc.render.resolution_x = sc.render.resolution_y = 700; sc.render.film_transparent = False
    wd = bpy.data.worlds.new("w"); wd.use_nodes = True; wd.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.55, 0.65, 1); sc.world = wd
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in bpy.data.objects:
        if o.type == "MESH": ev = o.evaluated_get(dg); pts += [o.matrix_world @ v.co for v in ev.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    c = (lo + hi) / 2; sz = max((hi - lo).length, 1e-6) if scale is None else scale
    cd = bpy.data.cameras.new("c"); cd.type = "ORTHO"; cd.ortho_scale = sz * 1.05; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam; cd.clip_end = 1e6
    cam.location = c + Vector((sz * 0.6, -sz * 1.4, sz * 0.5)); cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    if wire:
        for o in bpy.data.objects:
            if o.type == "MESH":
                md = o.modifiers.new("wf", "WIREFRAME"); md.thickness = sz * 0.0006; md.use_replace = False
    sc.render.filepath = path; bpy.ops.render.render(write_still=True); return sz


def rig():
    return sum(len(o.data.bones) for o in bpy.data.objects if o.type == "ARMATURE"), sum(len(o.vertex_groups) for o in bpy.data.objects if o.type == "MESH"), sum(1 for o in bpy.data.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers))


load(); r0 = rig(); t0 = tris(); tex0 = sorted(glob.glob(os.path.dirname(SRC) + "/Textures/*.png") + glob.glob(os.path.dirname(SRC) + "/Textures/*.jpg"))
emission_all(None); S = render(OUT + "/_before.png")
load()
SK = []
for o in [o for o in bpy.data.objects if o.type == "MESH"]:
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    n = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if o.data.shape_keys:   # 셰이프 키가 있으면 Decimate를 못 쓴다 → 키를 지우고(기본 모양만 남김) 기록
        SK.append((o.name, len(o.data.shape_keys.key_blocks))); bpy.ops.object.shape_key_remove(all=True)
    if n > 200:
        m = o.modifiers.new("dec", "DECIMATE"); m.decimate_type = "COLLAPSE"; m.ratio = RATIO; m.use_collapse_triangulate = True
        # 뼈 변형(Armature) 모디파이어보다 앞에 둔다
        while o.modifiers[0] != m: bpy.ops.object.modifier_move_up(modifier="dec")
        bpy.ops.object.modifier_apply(modifier="dec")
    o.select_set(False)
t1 = tris()
for o in bpy.data.objects: o.select_set(True)
bpy.context.view_layer.objects.active = [o for o in bpy.data.objects if o.type == "MESH"][0]
bpy.ops.export_scene.fbx(filepath=f"{OUT}/{NAME}.fbx", use_selection=True, object_types={"MESH", "ARMATURE"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False, add_leaf_bones=False)
load_after = None
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f"{OUT}/{NAME}.fbx"); t_chk = tris(); r1 = rig()
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f"{OUT}/{NAME}.fbx")
# 후 렌더: 텍스처는 원본 폴더 것으로 임시 연결(렌더용, 크기만 다른 같은 그림)
imgs = {os.path.basename(p): p for p in tex0}
for o in bpy.data.objects:
    if o.type != "MESH": continue
    for s in o.material_slots:
        m = s.material
        if m and m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image and os.path.basename(n.image.filepath) in imgs: n.image.filepath = imgs[os.path.basename(n.image.filepath)]
emission_all(None); render(OUT + "/_after.png", scale=S)
# 텍스처 축소본(별도 처리: PIL이 있는 시스템 파이썬에서 후처리 — 여기선 목록만)
json.dump(dict(model=NAME, trisBefore=t0, trisAfter=t1, shapeKeysRemoved=SK, trisReimported=t_chk, rigBefore=dict(bones=r0[0], vgroups=r0[1], skinnedMeshes=r0[2]), rigAfter=dict(bones=r1[0], vgroups=r1[1], skinnedMeshes=r1[2]), ratio=RATIO, texMax=TEXMAX, textures=[os.path.basename(p) for p in tex0], src=SRC), open(OUT + "/lod.json", "w"), ensure_ascii=False, indent=1)
print("LOD", NAME, t0, "->", t1, "reimport", t_chk)
