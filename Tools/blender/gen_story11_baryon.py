"""Story11_日本 ← 나루토 바리온 모드(Sketchfab MontanariArt "Naruto (Baryon)", CC-BY-4.0, zip 안 zip의 scene.gltf). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story11_baryon.py -- [--out DIR] [--probe]
원본: **뼈·클립·텍스처·UV 전부 없음** — 점프 자세로 굳은 피규어(뼈 0, 애니 0), 메시 24 · 삼각형 1,403,021(!), 색은 **정점색(COLOR_0)뿐**(재질 17개는 색 없는 회색).
  → ① 메시마다 접기 감량(전체 약 0.7% 남김) ② 흩어진 작은 덩어리 제거 ③ 스마트 UV ④ 정점색을 1024 텍스처로 굽기(Cycles emit) → `건물_바리온_01`.
  자세를 서 있는 자세로 바꿀 수 없다(뼈 없음) — 점프 자세 그대로. 규격: 원점 바닥 가운데 · 정면 −Y(얼굴·가슴이 −Y, 렌더 확인) · 가장 긴 변 44 · 삼각형 1만 이하.
"""
import math
import os
import sys
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story11_日本_나루토바리온.zip")
NAME = "Story11_日本"
FOOT = 44.8              # 44면 높이 39.4로 하한(40) 미달 → 바닥 45 한도 안에서 최대로
TRI_LIMIT = 10000
VOXEL = 0.05              # 복셀 리메시 크기(원본 단위, 몸 높이 약 4.7) — 얇은 촉수가 사라지지 않는 한 크게
MIN_ISLAND = 0.02          # 가장 큰 덩어리 면 수 대비 이 비율 미만의 떨어진 덩어리는 뺀다


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story11_바리온")
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
    work = "/tmp/gen_story11_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    inner_zip = next(os.path.join(work, "source", f) for f in os.listdir(os.path.join(work, "source")) if f.endswith(".zip"))
    with zipfile.ZipFile(inner_zip) as z:
        z.extractall(os.path.join(work, "inner"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(work, "inner", "scene.gltf"))
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    print("원본 삼각형", sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons), "메시", len(meshes))
    # 고해상 원본: 세계 좌표로 합치고 정점색을 발광으로 보이게(굽기 원천)
    for o in meshes:
        o.data.transform(o.matrix_world)
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
        o.data.materials.clear()
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    hi = bpy.context.view_layer.objects.active
    hi.name = "hi"
    cname = hi.data.color_attributes[0].name
    hmat = bpy.data.materials.new("hi_emit")
    hmat.use_nodes = True
    nt = hmat.node_tree
    nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = cname
    nt.links.new(vc.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs["Emission"], o_.inputs["Surface"])
    hi.data.materials.append(hmat)
    # 저해상: 복제 → 복셀 리메시(열린 껍질을 닫힌 메시로 — 접기 감량이 열린 껍질을 조각냈다, 1·2차) → 작은 덩어리 제거 → 접기 감량
    bpy.ops.object.select_all(action="DESELECT")
    lo = hi.copy()
    lo.data = hi.data.copy()
    lo.name = NAME
    bpy.context.scene.collection.objects.link(lo)
    lo.data.materials.clear()
    for a_ in list(lo.data.color_attributes):
        lo.data.color_attributes.remove(a_)
    bpy.context.view_layer.objects.active = lo
    lo.select_set(True)
    lo.data.remesh_voxel_size = VOXEL
    lo.data.remesh_voxel_adaptivity = 0.0
    bpy.ops.object.voxel_remesh()
    print("복셀 리메시 삼각형", sum(len(p.vertices) - 2 for p in lo.data.polygons), "복셀", VOXEL)
    bm = bmesh.new()
    bm.from_mesh(lo.data)
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
        comps.append(fs)
    comps.sort(key=len, reverse=True)
    big = len(comps[0])
    drop = [f for fs in comps if len(fs) < MIN_ISLAND * big for f in fs]
    print("덩어리", len(comps), "큰 것 면", [len(c) for c in comps[:6]], "뺀 면", len(drop))
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(lo.data)
    bm.free()
    tris = sum(len(p.vertices) - 2 for p in lo.data.polygons)
    if tris > TRI_LIMIT:
        m = lo.modifiers.new("d", "DECIMATE")
        m.ratio = TRI_LIMIT / tris * 0.99
        m.use_collapse_triangulate = True
        dg = bpy.context.evaluated_depsgraph_get()
        nm = bpy.data.meshes.new_from_object(lo.evaluated_get(dg), depsgraph=dg)
        lo.modifiers.remove(m)
        lo.data = nm
        nm.name = NAME
    # 크기·원점(고해상·저해상에 같은 변환 — 굽기가 같은 좌표계에서 해야 한다) — 원본 단위에서 굽고 마지막에 스케일
    # UV 펴기
    bpy.ops.object.select_all(action="DESELECT")
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    name = "건물_바리온_01"
    img = bpy.data.images.new(name, 1024, 1024, alpha=False)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = img
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], o_.inputs["Surface"])
    nt.nodes.active = t
    lo.data.materials.append(mat)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 1
    sc.cycles.device = "CPU"
    sc.render.bake.margin = 10
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = 0.06
    sc.render.bake.max_ray_distance = 0.4
    bpy.ops.object.select_all(action="DESELECT")
    hi.select_set(True)
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.bake(type="EMIT")
    img.filepath_raw = os.path.join(tdir, name + ".png")
    img.file_format = "PNG"
    img.save()
    bpy.data.objects.remove(hi, do_unlink=True)
    me = lo.data
    # 크기·원점
    V = np.array([v.co[:] for v in me.vertices])
    lo_, hi_ = V.min(0), V.max(0)
    s = FOOT / max(hi_[0] - lo_[0], hi_[1] - lo_[1])
    me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(lo_[0] + hi_[0]) / 2, -(lo_[1] + hi_[1]) / 2, -lo_[2])))
    V = np.array([v.co[:] for v in me.vertices])
    dims = V.max(0) - V.min(0)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print("최종 크기(게임 단위)", dims.round(2).tolist(), "삼각형", tris, "배율", round(s, 2), "최저 z", round(float(V[:, 2].min()), 3))
    body = lo
    me.transform(Matrix.Scale(1 / 11.4, 4))
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    os.makedirs(out, exist_ok=True)
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
    sc.display.shading.light = "FLAT"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = sc.render.resolution_y = 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 80
    for name, loc, rot in (("앞", (0, -150, 20), (90, 0, 0)), ("옆", (150, 0, 20), (90, 0, 90)), ("위", (0, 0, 150), (0, 0, 0)),
                           ("비스듬", (100, -100, 70), (60, 0, 45))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
