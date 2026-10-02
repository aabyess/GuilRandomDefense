"""Story02_큰소망유치원 ← 아롱파크(Sketchfab aryan_yadav, CC-BY-4.0) 정적 건물. blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story02_aronpark.py -- [--out DIR] [--tris N] [--probe]
원본: ~/Desktop/구랜디스킨모음/92_스토리건물/Story02_큰소망유치원_아롱파크.glb (메시 11·이미지 30·153k삼각형·뼈 없음)
  탑(5층 팽이 지붕) + 큰 콘크리트 받침판(슬래브)·연못 둘·울타리 전체 둘레 + 작은 문·정자. 건물만 남긴다:
  · 슬래브(defaultMaterial.009)·연못(.010)은 통째로 뺀다 — 받침(섬·물 평면).
  · 나머지 메시는 연결 덩어리(타일·난간 기둥 수천 개)로 쪼개 **탑 영역 안에 중심이 있는 덩어리만** 남긴다(울타리·문·정자·연못 가 소품 탈락).
  · 재질 10개 = 각각 이미지 3장(BaseColor·Metal/Rough·Normal). BaseColor 한 장만 쓴다(다른 건물과 같게 색 텍스처만) — 같은 그림은 해시로 합친다.
규격: 원점 바닥 가운데 · 정면 −Y · 바닥 45×45 안 · 높이 40~90 · 삼각형 10000 이하 · 재질 `건물_` 접두 = 텍스처 이름.
"""
import hashlib
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

SRC = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리건물/Story02_큰소망유치원_아롱파크.glb")
NAME = "Story02_큰소망유치원"
DROP_MESHES = {"defaultMaterial.009", "defaultMaterial.010", "defaultMaterial.005"}   # 슬래브·연못·곁채(아치 지붕 5연+정자, --arcade면 곁채 유지)
BOX = dict(x=(-0.85, -0.30), y=(-0.33, 0.33), z=(-0.20, 9.0))     # 탑 영역(원본 단위). 동쪽 곁채(아치 지붕 5연, x -0.38~-0.26)는 뺀다 — --arcade면 -0.245까지
FOOT = 44.0                                                          # 바닥 목표(규격 45 이하, 여유 1)


def components(bm):
    seen, out = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        st, fs = [f], []
        seen.add(f.index)
        while st:
            c = st.pop()
            fs.append(c)
            for e in c.edges:
                for n in e.link_faces:
                    if n.index not in seen:
                        seen.add(n.index)
                        st.append(n)
        out.append(fs)
    return out


def in_box(fs):
    vs = {v for f in fs for v in f.verts}
    c = sum((v.co for v in vs), Vector()) / len(vs)
    return BOX["x"][0] <= c.x <= BOX["x"][1] and BOX["y"][0] <= c.y <= BOX["y"][1] and BOX["z"][0] <= c.z <= BOX["z"][1]


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story02_아롱파크")
    tri_limit = 10000
    if "--arcade" in args:
        BOX["x"] = (BOX["x"][0], -0.245)
        DROP_MESHES.discard("defaultMaterial.005")
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--tris":
            tri_limit = int(next(it))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC)
    rep = {}
    pieces = []
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        if o.name in DROP_MESHES:
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(o.matrix_world)
        keep = [f for fs in components(bm) if in_box(fs) for f in fs]
        drop = [f for f in bm.faces if f not in set(keep)]
        bmesh.ops.delete(bm, geom=drop, context="FACES")
        rep[o.name] = (len(o.data.polygons), len(bm.faces))
        if bm.faces:
            me = bpy.data.meshes.new(o.name)
            bm.to_mesh(me)
            for m in o.data.materials:
                me.materials.append(m)
            nob = bpy.data.objects.new(o.name, me)
            bpy.context.scene.collection.objects.link(nob)
            pieces.append(nob)
        bm.free()
    for o in [o for o in bpy.data.objects if o not in pieces]:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in pieces:
        o.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = body.data.name = NAME
    body.data.transform(Matrix.Rotation(math.radians(-90), 4, "Z"))   # 현관(아치 문)이 원본 +X 면 → 정면 −Y로
    V = np.array([v.co for v in body.data.vertices])
    lo, hi = V.min(0), V.max(0)
    s = FOOT / max(hi[0] - lo[0], hi[1] - lo[1])
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))
    body.data.transform(G)
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    V = np.array([v.co for v in body.data.vertices])
    print("조각별(전→후 면)", json.dumps(rep), "\n배율", round(s, 2), "크기(게임 단위)", (V.max(0) - V.min(0)).round(2).tolist(), "삼각형", tris)
    if "--probe" in args:
        return
    ratio = min(1.0, tri_limit / tris)
    if ratio < 1.0:
        body = decimate(body, ratio)
        tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    V = np.array([v.co for v in body.data.vertices])
    print("감량 뒤 삼각형", tris, "크기", (V.max(0) - V.min(0)).round(2).tolist(), "최저 z", round(float(V[:, 2].min()), 3))
    textures(body, out)
    export(body, out)
    render(body, os.path.join(out, "검증"))


def decimate(body, ratio):
    bpy.context.view_layer.objects.active = body
    mod = body.modifiers.new("d", "DECIMATE")
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    body.modifiers.remove(mod)
    old = body.data
    body.data = me
    me.name = old.name
    return body


def base_image(mat):
    for l in mat.node_tree.links:
        if l.to_socket.name == "Base Color" and l.from_node.type == "TEX_IMAGE":
            return l.from_node.image
    return None


def textures(body, out):
    """재질마다 BaseColor 이미지 한 장 → 1024 이하 png로 저장, 같은 그림은 합친다. 재질 이름 = 텍스처 이름(건물_ 접두)."""
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    seen, report = {}, []
    for i, slot in enumerate(body.material_slots):
        m = slot.material
        img = base_image(m)
        assert img is not None, f"{m.name}: BaseColor 이미지 없음(색만 재질)"
        px = np.array(img.pixels[:], dtype=np.float32)
        h = hashlib.md5(px.tobytes()).hexdigest()
        if h not in seen:
            name = f"건물_아롱_{len(seen) + 1:02d}"
            im2 = bpy.data.images.new(name, img.size[0], img.size[1], alpha=False)
            im2.pixels = px.tolist() if False else px
            if max(img.size) > 1024:
                im2.scale(1024, 1024)
            im2.filepath_raw = os.path.join(tdir, name + ".png")
            im2.file_format = "PNG"
            im2.save()
            seen[h] = (name, im2)
        name, im2 = seen[h]
        report.append((m.name, name))
        # 재질 새로 짜기: Principled + 이미지(금속·거칠기·노말·발광 없음 — 「색만」·Emissive 함정 방지)
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        outn = nt.nodes.new("ShaderNodeOutputMaterial")
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = im2
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        nt.links.new(bsdf.outputs["BSDF"], outn.inputs["Surface"])
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.8
        m.blend_method = "OPAQUE" if hasattr(m, "blend_method") else None
        m.name = name + "_tmp%d" % i
    # 같은 그림 재질은 한 재질로 합친다(슬롯 인덱스 재배정)
    by_name = {}
    for i, slot in enumerate(body.material_slots):
        by_name.setdefault(report[i][1], []).append(i)
    first = {n: idx[0] for n, idx in by_name.items()}
    remap = {i: first[report[i][1]] for i in range(len(report))}
    for p in body.data.polygons:
        p.material_index = remap[p.material_index]
    for n, idx in by_name.items():
        body.material_slots[idx[0]].material.name = n
    print("재질→텍스처", report, "고유 텍스처", len(seen))
    for im in list(bpy.data.images):
        if im.name.startswith("Image_"):
            bpy.data.images.remove(im)


def export(body, out):
    # 안 쓰는 슬롯을 지운다
    used = sorted({p.material_index for p in body.data.polygons})
    keep_mats = [body.material_slots[i].material for i in used]
    remap = {old: new for new, old in enumerate(used)}
    for p in body.data.polygons:
        p.material_index = remap[p.material_index]
    body.data.materials.clear()
    for m in keep_mats:
        body.data.materials.append(m)
    # 규격: 게임 단위(1 bu = 1 단위)를 미터(1/11.4)로 줄였다가 global_scale 11.4로 내보낸다(buildings_common.export와 같게)
    body.data.transform(Matrix.Scale(1 / 11.4, 4))
    body.data.update()
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    dst = os.path.join(out, NAME + ".fbx")
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, global_scale=11.4, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, object_types={"MESH"}, path_mode="RELATIVE")
    print("출력", dst, "재질", [m.name for m in keep_mats])
    body.data.transform(Matrix.Scale(11.4, 4))


def render(body, odir):
    os.makedirs(odir, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x, sc.render.resolution_y = 900, 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 90
    for name, loc, rot in (("앞", (0, -100, 32), (90, 0, 0)), ("옆", (100, 0, 32), (90, 0, 90)), ("위", (0, 0, 100), (0, 0, 0))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
