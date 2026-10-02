"""Story08_사이버넷 ← Dark Young Overlord(zip: source/Dark_Young.fbx + textures Dant_Color·Core_Color). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story08_darkyoung.py -- [--out DIR] [--frame N]
원본: 아마추어 1 + 메시 11(몸통 Cube 12,980 · 다리 Cylinder.004 11,252 · 촉수 9개 588씩) · 재질 2(Core·Dant, FBX가 이미지를 안 물고 있다 → 재질 이름으로 `<이름>_Color.png`를 잇는다)
  · 클립 1개 `ArmatureAction` 프레임 1~100(24fps, 4.17초, 첫·끝 프레임이 같은 순환). 노멀맵 재질 노드가 있으나 색만 쓴다.
1차 산출은 **정적**: 클립의 한 프레임을 평가된(스키닝된) 메시로 굳히고 뼈를 버린다. 프레임 1(= 100, 순환 기점)을 골랐다 —
  촉수가 좌우 대칭으로 위로 벌어지고 입이 정면(−Y)을 봐 가장 위협적이고 정돈된 자세(프레임 20은 키는 더 크나 한쪽으로 쏠려 비대칭).
  최저점(발)을 z=0으로. 크기는 가장 긴 변 44.
규격: 원점 바닥 가운데 · 정면 −Y · 높이 40~90 · 삼각형 1만 이하 · 재질·텍스처 `건물_다크영_NN`.
"""
import math
import os
import sys
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story08_사이버넷_다크영오버로드.zip")
NAME = "Story08_사이버넷"
FOOT = 44.0
TRI_LIMIT = 10000
TEX = {"Core": "Core_Color.png", "Dant": "Dant_Color.png"}     # → 건물_다크영_01·02


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story08_다크영")
    frame = 1
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--frame":
            frame = int(next(it))
    work = "/tmp/gen_story08_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(work, "source", "Dark_Young.fbx"))
    sc = bpy.context.scene
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    act = bpy.data.actions[0]
    print("클립", act.name, "프레임", [round(x, 1) for x in act.frame_range], "fps", sc.render.fps, "길이(초)", round((act.frame_range[1] - act.frame_range[0]) / sc.render.fps, 2))
    arm.animation_data_create()
    arm.animation_data.action = act
    sc.frame_set(frame)
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    mat_names = []
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        m2 = me.copy()
        e.to_mesh_clear()
        m2.transform(e.matrix_world)
        base = {}
        for i, m in enumerate(o.data.materials):
            if m.name not in mat_names:
                mat_names.append(m.name)
            base[i] = mat_names.index(m.name)
        for p in m2.polygons:
            p.material_index = base.get(p.material_index, 0)
        bm.from_mesh(m2)
        bpy.data.meshes.remove(m2)
    me = bpy.data.meshes.new(NAME)
    bm.to_mesh(me)
    bm.free()
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    for i, key in enumerate(mat_names):
        name = f"건물_다크영_{i + 1:02d}"
        img = bpy.data.images.load(os.path.join(work, "textures", TEX[key]))
        w, h = img.size
        arr = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(arr)
        arr = arr.reshape(h, w, 4)
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
        print("텍스처", name, "←", TEX[key], im.size[:])
    obj = bpy.data.objects.new(NAME, me)
    sc.collection.objects.link(obj)
    # 정면: 입이 −Y(원본 그대로 확인) — 회전 없음. 최저점 0, 가장 긴 변 FOOT, 원점 바닥 가운데
    V = np.array([v.co[:] for v in me.vertices])
    lo, hi = V.min(0), V.max(0)
    s = FOOT / max(hi[0] - lo[0], hi[1] - lo[1])
    me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    V = np.array([v.co[:] for v in me.vertices])
    print("굳힌 뒤 크기(게임 단위)", (V.max(0) - V.min(0)).round(2).tolist(), "삼각형", tris, "배율", round(s, 2))
    if tris > TRI_LIMIT:
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
    V = np.array([v.co[:] for v in me.vertices])
    me.transform(Matrix.Translation((0, 0, -V[:, 2].min())))
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    V = np.array([v.co[:] for v in me.vertices])
    dims = V.max(0) - V.min(0)
    print("최종 크기(게임 단위)", dims.round(2).tolist(), "삼각형", tris, "최저 z", round(float(V[:, 2].min()), 3))
    assert 40 <= dims[2] <= 90, f"높이 {dims[2]:.1f}가 규격 40~90 밖"
    me.transform(Matrix.Scale(1 / 11.4, 4))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
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
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = sc.render.resolution_y = 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 75
    for name, loc, rot in (("앞", (0, -150, 30), (90, 0, 0)), ("옆", (150, 0, 30), (90, 0, 90)), ("위", (0, 0, 150), (0, 0, 0)),
                           ("비스듬", (100, -100, 90), (60, 0, 45))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
