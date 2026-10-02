"""Story04_구일초등학교 ← 이치라쿠 라멘(Sketchfab, 라이선스는 glb extras 확인). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story04_ramen.py -- [--out DIR] [--tris N] [--probe]
원본: ~/Desktop/구랜디스킨모음/05_히든/히든_호치킨.glb (이치라쿠 라멘 포장마차, 유닛 히든_호치킨과 같은 모델 — 유닛 쪽은 안 건드린다). 메시 37·재질 4·삼각형 10,137.
  · 받침 슬래브 Ground_low(원본 z −177~8)만 뺀다 — 나머지는 전부 쓴다(노렌·카운터·의자·등롱·사슬·간판·냉장고·에어컨).
  · 🔴 크기: 건물 본체(Building_Bottom_low) 가장 긴 변을 FOOT로 맞춘다. 지붕·등롱·사슬이 앞으로 튀어나와 전체 폭은 더 크다(실측은 보고).
    전체 경계로 45에 맞추면 높이가 33이라 규격(40~90)에 못 미친다 — 바닥(본체) 기준이 규격 문장 「바닥 45×45」에 맞다.
  · 원점 = 본체 바닥 가운데. 정면 −Y는 원본 그대로(ラーメン 一楽 간판·의자 쪽, 호치킨 SOURCE.txt).
  재질마다 이미지 1장(BaseColor)만 — 금속·노멀 버림.
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

SRC = os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_호치킨.glb")
NAME = "Story04_구일초등학교"
# 메시 → 영역 상자(None = 통째). 나머지 메시는 쓰지 않는다.
DROP = {"Ground_low.000_Ground_0"}
BODY = "Building_Bottom_low.000_Building_Bottom_0"   # 크기·원점 기준(본체)
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


def in_box(fs, box):
    if box is None:
        return True
    vs = {v for f in fs for v in f.verts}
    c = sum((v.co for v in vs), Vector()) / len(vs)
    return box["x"][0] <= c.x <= box["x"][1] and box["y"][0] <= c.y <= box["y"][1] and box["z"][0] <= c.z <= box["z"][1]


def main():
    global BODY_BBOX
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story04_라멘")
    tri_limit = 10000
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
        if o.name in DROP:
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.transform(o.matrix_world)
        if o.name == BODY:
            bv = np.array([v.co[:] for v in bm.verts])
            BODY_BBOX = (bv.min(0).tolist(), bv.max(0).tolist())
        keep = [f for fs in components(bm) if True for f in fs]
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
    import collections
    print("합친 뒤 재질별 면", [(m.name if m else None, c) for m, c in ((body.material_slots[i].material, n) for i, n in sorted(collections.Counter(p.material_index for p in body.data.polygons).items()))])
    V = np.array([v.co for v in body.data.vertices])
    lo, hi = V.min(0), V.max(0)
    bb = np.array(BODY_BBOX)                                        # 본체(Building_Bottom) 경계 — 합치기 전에 잰 값
    s = FOOT / max(bb[1][0] - bb[0][0], bb[1][1] - bb[0][1])
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-(bb[0][0] + bb[1][0]) / 2, -(bb[0][1] + bb[1][1]) / 2, -lo[2]))
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
    import collections as _c
    print("감량 뒤 재질별 면", dict(_c.Counter(p.material_index for p in body.data.polygons)), len(body.material_slots))
    print("감량 뒤 삼각형", tris, "크기", (V.max(0) - V.min(0)).round(2).tolist(), "최저 z", round(float(V[:, 2].min()), 3))
    textures(body, out)
    export(body, out)
    render(body, os.path.join(out, "검증"))


def _apply(body, mod):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    body.modifiers.remove(mod)
    old = body.data
    body.data = me
    me.name = old.name


def decimate(body, ratio):
    """1) 평면 병합(UV·재질 경계 지킴 — 무음영 텍스처가 틀어지지 않게) → 2) 아직 넘으면 접기. UV 경계를 모르는 접기만 쓰면 색이 어긋난다(1차)."""
    bpy.context.view_layer.objects.active = body
    target = int(sum(len(p.vertices) - 2 for p in body.data.polygons) * ratio)
    for ang in (2.0, 5.0, 10.0):
        mod = body.modifiers.new("p", "DECIMATE")
        mod.decimate_type = "DISSOLVE"
        mod.angle_limit = math.radians(ang)
        mod.delimit = {"UV", "MATERIAL", "SHARP"}
        mod.use_dissolve_boundaries = False
        _apply(body, mod)
        n = sum(len(p.vertices) - 2 for p in body.data.polygons)
        print("평면 병합", ang, "° →", n)
        if n <= target:
            break
    n = sum(len(p.vertices) - 2 for p in body.data.polygons)
    if n > target:
        mod = body.modifiers.new("d", "DECIMATE")
        mod.ratio = target / n
        mod.use_collapse_triangulate = True
        _apply(body, mod)
    return body


def base_image(mat):
    for l in mat.node_tree.links:
        if l.to_socket.name == "Base Color" and l.from_node.type == "TEX_IMAGE":
            return l.from_node.image
    # 에니에스로비 재질은 무음영(KHR unlit) — Emission 색으로 이미지가 꽂힌다. 첫 TEX_IMAGE를 쓴다(Principled+이미지로 새로 짜면 유니티 Lit로 보인다).
    return next((n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None)


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
            name = f"건물_라멘_{len(seen) + 1:02d}"
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
    new_idx = [remap[p.material_index] for p in body.data.polygons]
    body.data.materials.clear()                         # 🔴 슬롯을 비우면 면의 재질 번호가 0으로 눌린다 — 저장해 둔 값을 다시 먹인다
    for m in keep_mats:
        body.data.materials.append(m)
    body.data.polygons.foreach_set("material_index", new_idx)
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
