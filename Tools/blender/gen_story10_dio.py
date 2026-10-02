"""Story10_동양미래대학교 ← DIO 머리(죠죠 모바일 게임 추출, zip 안 zip 안 DioHead.obj/.mtl/.png). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story10_dio.py -- [--out DIR]
원본: OBJ 1개(정점 2,615 · 면 2,676) · 재질 1 · 텍스처 DioHead.png 한 장(바깥 zip의 textures/ 에도 같은 파일). 
  연결 덩어리 10개 = ① 머리+머리카락 2,218면 ② **돌 받침 틀**(폭 0.30, 면 92 — 기둥·보석띠, 머리를 담은 전시 틀) ③ 눈·속눈썹 조각 8개 ④ **위에 뜬 돔 뚜껑**(70면) + 꼭지(12면).
  · 남기는 것: 머리 + 눈 조각(②돌 받침·④뚜껑·꼭지는 뺀다 — 받침은 지면 판 성격, 뚜껑은 허공에 떠 있다).
  · 목 단면(`CUT` 평면 0.050으로 평평하게 자른 뒤 막음): 용접 후 머리 덩어리에 열린 변이 있다 — 가장 낮은 열린 고리(목)를 막고(holes_fill), 목 단면 높이를 z=0(바닥)에 맞춘다. 목 말고 열린 구멍이 더 있으면 같이 막는다(귀 뒤·눈 속).
  · 정면 −Y(얼굴): OBJ를 Z-up으로 들여오면 얼굴이 −Y를 본다(렌더로 확인).
규격: 바닥 가장 긴 변 44 · 높이 40~90 · 삼각형 1만 이하(원본 2,676 — 감량 없음) · 재질·텍스처 `건물_디오_01`. 알파 없음.
"""
import math
import os
import sys
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story10_동양미래대학교_디오머리.zip")
NAME = "Story10_동양미래대학교"
FOOT = 44.0
KEEP_ALL = "--head-only" not in sys.argv     # 사장님 10-02: 「병 안에 넣은 상태로」 — 받침 틀·돔 뚜껑·꼭지 전부 살린 원본 전시 장면이 기본. --head-only면 머리만(이전 판)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story10_디오")
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
    work = "/tmp/gen_story10_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    inner_zip = next(os.path.join(work, "source", f) for f in os.listdir(os.path.join(work, "source")) if f.endswith(".zip"))
    with zipfile.ZipFile(inner_zip) as z:
        z.extractall(os.path.join(work, "inner"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.obj_import(filepath=os.path.join(work, "inner", "DioHead.obj"))
    src = next(o for o in bpy.data.objects if o.type == "MESH")
    bm = bmesh.new()
    bm.from_mesh(src.data)
    bm.transform(src.matrix_world)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    # 연결 덩어리 → 남길 것 고르기(월드 Z가 위). 기준 = 가장 큰 덩어리(머리+머리카락)의 경계 상자.
    # 1차(폭 0.28 기준)는 돔 꼭지(폭 0.026, 머리 위 0.1에 떠 있음)를 못 걸러 유니티에서 머리 위 검은 휜 조각이 경계를 늘렸다 → 머리 상자 밖(여유 0.01)은 전부 뺀다.
    seen, comps = set(), []
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
        vs = {v for q in fs for v in q.verts}
        lo = [min(v.co[i] for v in vs) for i in range(3)]
        hi = [max(v.co[i] for v in vs) for i in range(3)]
        comps.append((fs, lo, hi))
    big = max(comps, key=lambda c: len(c[0]))
    m = 0.01
    keep, drop = [], []
    for fs, lo, hi in comps:
        inside = all(lo[i] >= big[1][i] - m and hi[i] <= big[2][i] + m for i in range(3))
        wide = (hi[0] - lo[0]) > 0.28 or (hi[1] - lo[1]) > 0.28
        ok = KEEP_ALL or fs is big[0] or (inside and not wide)
        (keep if ok else drop).extend(fs)
        print("덩어리", len(fs), "면", [round(x, 3) for x in lo], [round(x, 3) for x in hi], "남김" if ok else "뺌")
    print("남김 면", len(keep), "뺌 면", len(drop))
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # 목 바닥을 평평하게: 원본 목 단면은 V자(z 0.0393~0.0485) → 0.050 평면으로 잘라 아래를 버리고(clear_inner) 그 단면을 막는다
    CUT = 0.050
    geom = [] if KEEP_ALL else list(bm.verts) + list(bm.edges) + list(bm.faces)
    if geom != []:
      bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=(0, 0, CUT), plane_no=(0, 0, 1), clear_inner=True)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # 열린 고리 찾기 → 가장 낮은 것(목)을 막는다
    bnd = [e for e in bm.edges if e.is_boundary]
    print("열린 변", len(bnd))
    zmin_open = min(v.co.z for e in bnd for v in e.verts) if bnd else None
    new_faces = 0
    if bnd:
        # 열린 고리마다 변 수·높이를 보고(목 = 가장 낮고 가장 큰 고리). 변 수 제한 없이 전부 막는다.
        loops, rest = [], set(bnd)
        while rest:
            e0 = rest.pop(); grp = {e0}; st = [e0]
            while st:
                e = st.pop()
                for v in e.verts:
                    for n in v.link_edges:
                        if n in rest:
                            rest.discard(n); grp.add(n); st.append(n)
            zs = [v.co.z for e in grp for v in e.verts]
            loops.append((len(grp), round(min(zs), 4), round(max(zs), 4)))
        print("열린 고리(변수, z최저, z최고)", sorted(loops, key=lambda l: l[1])[:8], "총", len(loops))
        res = bmesh.ops.holes_fill(bm, edges=bnd, sides=0)
        new_faces = len(res["faces"])
    print("막은 면", new_faces, "목 높이(원본)", None if zmin_open is None else round(zmin_open, 4))
    left = [e for e in bm.edges if e.is_boundary]
    print("채운 뒤 남은 열린 변", len(left), "그중 목(z<0.06)", sum(1 for e in left if min(v.co.z for v in e.verts) < 0.06))
    if left:                                          # 남은 것은 비평면 고리 — 삼각 채우기로 마저 막는다
        bmesh.ops.triangle_fill(bm, edges=left, use_beauty=True)
        print("마저 막은 뒤 남은 열린 변", sum(1 for e in bm.edges if e.is_boundary))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    uv = bm.loops.layers.uv.active
    for f in bm.faces:                                   # 새 면 UV(0,0)은 텍스처 좌하단 픽셀 — 피부색이 아닐 수 있어 가까운 원본 모서리 UV로
        if any(l[uv].uv.length < 1e-9 for l in f.loops):
            near = [l[uv].uv.copy() for v in f.verts for l in v.link_loops if l[uv].uv.length > 1e-9]
            if near:
                for l in f.loops:
                    l[uv].uv = near[0]
    me = bpy.data.meshes.new(NAME)
    bm.to_mesh(me)
    bm.free()
    bpy.data.objects.remove(src, do_unlink=True)
    # 텍스처·재질
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    name = "건물_디오_01"
    img = bpy.data.images.load(os.path.join(work, "inner", "DioHead.png"))
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    arr = arr.reshape(h, w, 4)
    print("텍스처 원본", (w, h), "알파 최소", float(arr[..., 3].min()))
    arr[..., 3] = 1.0
    im = bpy.data.images.new(name, w, h, alpha=False)
    im.pixels.foreach_set(arr.ravel())
    if max(w, h) > 1024:
        im.scale(1024, 1024)
    im.filepath_raw = os.path.join(tdir, name + ".png")
    im.file_format = "PNG"
    im.save()
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
    b.inputs["Roughness"].default_value = 0.7
    me.materials.append(mat)
    for p in me.polygons:
        p.material_index = 0
    obj = bpy.data.objects.new(NAME, me)
    bpy.context.scene.collection.objects.link(obj)
    # 크기·원점: 바닥 가장 긴 변 FOOT, 원점 바닥 가운데, 목 단면(최저)이 z=0
    V = np.array([v.co[:] for v in me.vertices])
    lo, hi = V.min(0), V.max(0)
    s = FOOT / max(hi[0] - lo[0], hi[1] - lo[1])
    me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
    V = np.array([v.co[:] for v in me.vertices])
    dims = V.max(0) - V.min(0)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print("최종 크기(게임 단위)", dims.round(2).tolist(), "삼각형", tris, "최저 z", round(float(V[:, 2].min()), 3), "배율", round(s, 2))
    print("원본 장면 전체" if KEEP_ALL else "머리만")
    assert 40 <= dims[2] <= 90, f"높이 {dims[2]:.1f} 규격 밖"
    me.transform(Matrix.Scale(1 / 11.4, 4))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, NAME + ".fbx"), use_selection=True, global_scale=11.4, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE", use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, object_types={"MESH"}, path_mode="RELATIVE")
    me.transform(Matrix.Scale(11.4, 4))
    render(os.path.join(out, "검증"))


def render(odir):
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
    cam.data.ortho_scale = 80
    for name, loc, rot in (("앞", (0, -150, 36), (90, 0, 0)), ("옆", (150, 0, 36), (90, 0, 90)), ("위", (0, 0, 150), (0, 0, 0)),
                           ("아래", (0, 0, -150), (180, 0, 0)), ("비스듬", (100, -100, 80), (60, 0, 45))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
