"""Story13_쉬었음 ← 나뭇잎 마을(나루토, zip: source/untitled.fbx + textures/Picsart_…png). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story13_konoha.py -- [--out DIR] [--box x0 x1 y0 y1] [--probe]
원본: 메시 1개(NB_GP_Terrain003, 56,224삼각형) = **섬 모양 지형 디오라마**(물·해안·ㄷ자 절벽·마을 광장·건물 모형). 6.4 × 5.3 × 1.2(원본 단위),
  텍스처 한 장 아틀라스 2828².  전체를 45에 맞추면 높이가 8밖에 안 돼 규격(높이 40~90)에 못 든다 → **대표 구역을 잘라 쓴다**:
  호카게 광장(원형 탑 + 부속 둥근 건물 둘) + 뒤로 선 절벽 + 호카게 바위 머리 둘(구역 안에 드는 것).
  · 지형이 열린 판이라 자른 가장자리를 바닥(z 최저)까지 내려 옆면을 짓고 바닥을 막는다(옆면 UV는 가장자리 색 그대로 — 절벽 줄무늬처럼 보임).
  · 텍스처는 **쓰는 UV 구역만 잘라** 1024 이하로 줄인다(통째 2828→1024로 줄이면 구역 해상도가 3분의 1로 떨어진다).
  · 정면 −Y(원본 그대로: 광장이 남쪽, 절벽이 북쪽 뒤). 원점 = 구역 바닥 가운데.
재질·텍스처 이름 `건물_나뭇잎_NN`, 색 텍스처만.
"""
import math
import os
import sys
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리건물/Story13_쉬었음_나뭇잎마을.zip")
NAME = "Story13_쉬었음"
BOX = (2.0, 3.0, 3.35, 4.35)          # x0 x1 y0 y1 (원본 단위)
FOOT = 44.0                            # 게임 단위, 바닥(구역) 가장 긴 변
TRI_LIMIT = 10000


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story13_나뭇잎")
    box = BOX
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--box":
            box = tuple(float(next(it)) for _ in range(4))
    work = "/tmp/gen_story13_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    fbx = os.path.join(work, "source", "untitled.fbx")
    png = next(os.path.join(work, "textures", f) for f in os.listdir(os.path.join(work, "textures")) if f.endswith(".png"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    src = next(o for o in bpy.data.objects if o.type == "MESH")
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bm.transform(src.matrix_world)
    x0, x1, y0, y1 = box
    # 깨끗한 직사각형 자르기: 네 평면으로 면을 자르고(bisect) 바깥을 버린다. 면 중심 판정(1차)은 큰 삼각형 때문에 울퉁불퉁한 윤곽이 났다.
    for co, no in (((x0, 0, 0), (-1, 0, 0)), ((x1, 0, 0), (1, 0, 0)), ((0, y0, 0), (0, -1, 0)), ((0, y1, 0), (0, 1, 0))):   # clear_outer = 법선 쪽을 지운다
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=co, plane_no=no, clear_outer=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # 🔴 원본은 삼각형이 낱개로 떨어진 메시(경계 변 89,128개 — 법선 분리 때문)라 용접 없이는 「경계」가 면마다 생긴다. 먼저 붙인다(UV는 면 모서리별이라 유지).
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    zs = [v.co.z for v in bm.verts]
    zbase = min(zs)
    uv = bm.loops.layers.uv.active
    # 자른 가장자리 → 바닥(zbase)까지 내린 옆면 + 바닥 막기. extrude 연산은 가시를 만들어(1차, 높이 254) 경계 변마다 직접 짓는다.
    bnd = [(e.verts[0], e.verts[1], e.link_faces[0]) for e in bm.edges if e.is_boundary]
    low = {}
    for v0, v1, f in bnd:
        for v in (v0, v1):
            if v not in low:
                low[v] = bm.verts.new((v.co.x, v.co.y, zbase))
    uv_of = {}
    for v0, v1, f in bnd:
        for l in f.loops:
            if l.vert in (v0, v1):
                uv_of[l.vert] = l[uv].uv.copy()
    side_faces, bottom_edges = [], []
    for v0, v1, f in bnd:
        # f의 바닥 방향 권선에 맞춰: f에서 v0→v1이 정방향이면 (v1,v0,low0,low1)이 바깥을 본다
        fwd = any(l.vert == v0 and l.link_loop_next.vert == v1 for l in f.loops)
        quad = (v1, v0, low[v0], low[v1]) if fwd else (v0, v1, low[v1], low[v0])
        nf = bm.faces.new(quad)
        for l in nf.loops:
            vv = l.vert
            src = vv if vv in uv_of else next(k for k, w in low.items() if w == vv)
            l[uv].uv = uv_of[src]
        side_faces.append(nf)
        bottom_edges.append(bm.edges.get((low[v0], low[v1])))
    cap = bmesh.ops.holes_fill(bm, edges=[e for e in bottom_edges if e], sides=0)
    for f in cap.get("faces", []):
        for l in f.loops:
            l[uv].uv = next(iter(uv_of.values()))
        f.normal_flip() if f.normal.z > 0 else None
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    # 감량 전 청소 — 겹친 점·퇴화 면이 접기 감량에서 정점을 허공으로 날렸다(1차: 높이 254)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    me = bpy.data.meshes.new(NAME)
    bm.to_mesh(me)
    bm.free()
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    V = np.array([v.co[:] for v in me.vertices])
    print("구역", box, "삼각형", tris, "크기(원본)", (V.max(0) - V.min(0)).round(3).tolist(), "z", round(V[:, 2].min(), 3), round(V[:, 2].max(), 3))
    if "--probe" in args:
        return
    # 텍스처: 쓰는 UV 구역만 잘라 1024 이하로
    uvl = me.uv_layers.active
    U = np.array([d.uv[:] for d in uvl.data])
    margin = 0.004
    u0, u1 = max(U[:, 0].min() - margin, 0), min(U[:, 0].max() + margin, 1)
    v0, v1 = max(U[:, 1].min() - margin, 0), min(U[:, 1].max() + margin, 1)
    img = bpy.data.images.load(png)
    W, H = img.size
    arr = np.empty(W * H * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    arr = arr.reshape(H, W, 4)
    px0, px1, py0, py1 = int(u0 * W), int(math.ceil(u1 * W)), int(v0 * H), int(math.ceil(v1 * H))
    crop = arr[py0:py1, px0:px1]
    print("UV 구역", (round(u0, 3), round(u1, 3), round(v0, 3), round(v1, 3)), "→ 픽셀", crop.shape[1], "×", crop.shape[0])
    name = "건물_나뭇잎_01"
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    im = bpy.data.images.new(name, crop.shape[1], crop.shape[0], alpha=False)
    im.pixels.foreach_set(crop.ravel())
    side = max(crop.shape[1], crop.shape[0])
    if side > 1024:
        k = 1024 / side
        im.scale(max(1, int(crop.shape[1] * k)), max(1, int(crop.shape[0] * k)))
    im.filepath_raw = os.path.join(tdir, name + ".png")
    im.file_format = "PNG"
    im.save()
    print("텍스처", im.size[:])
    # UV 재매핑(잘라낸 구역 기준 0~1)
    su, sv = (px1 - px0) / W, (py1 - py0) / H
    ou, ov = px0 / W, py0 / H
    for d in uvl.data:
        d.uv = ((d.uv[0] - ou) / su, (d.uv[1] - ov) / sv)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = im
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], o_.inputs["Surface"])
    b.inputs["Metallic"].default_value = 0.0
    b.inputs["Roughness"].default_value = 0.85
    me.materials.clear()
    me.materials.append(mat)
    for p in me.polygons:
        p.material_index = 0
    obj = bpy.data.objects.new(NAME, me)
    bpy.context.scene.collection.objects.link(obj)
    # 크기·원점: 구역 가장 긴 변 = FOOT, 바닥 가운데 원점, 최저 z = 0
    V = np.array([v.co[:] for v in me.vertices])
    lo, hi = V.min(0), V.max(0)
    s = FOOT / max(hi[0] - lo[0], hi[1] - lo[1])
    me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
    # 감량
    if tris > TRI_LIMIT and "--no-decimate" not in args:
        bpy.context.view_layer.objects.active = obj
        m = obj.modifiers.new("d", "DECIMATE")
        m.ratio = TRI_LIMIT / tris * 0.995
        m.use_collapse_triangulate = True
        dg = bpy.context.evaluated_depsgraph_get()
        nm = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        obj.modifiers.remove(m)
        obj.data = nm
        nm.name = NAME
        me = nm
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    V = np.array([v.co[:] for v in me.vertices])
    dims = V.max(0) - V.min(0)
    print("최종 크기(게임 단위)", dims.round(2).tolist(), "삼각형", tris, "최저 z", round(float(V[:, 2].min()), 3), "배율", round(s, 2))
    # 미터로 줄여 내보내기(건물 규약: 1/11.4 → global_scale 11.4)
    me.transform(Matrix.Scale(1 / 11.4, 4))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    os.makedirs(out, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, NAME + ".fbx"), use_selection=True, global_scale=11.4, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, object_types={"MESH"}, path_mode="RELATIVE")
    me.transform(Matrix.Scale(11.4, 4))
    render(obj, os.path.join(out, "검증"))


def render(obj, odir):
    os.makedirs(odir, exist_ok=True)
    sc = bpy.context.scene
    sc.view_settings.view_transform = "Standard"
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = sc.render.resolution_y = 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 90
    for name, loc, rot in (("앞", (0, -100, 28), (90, 0, 0)), ("옆", (100, 0, 28), (90, 0, 90)), ("위", (0, 0, 100), (0, 0, 0)),
                           ("비스듬", (70, -70, 70), (55, 0, 45))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
