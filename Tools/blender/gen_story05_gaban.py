"""Story05_구일중학교 ← 스코퍼 가반(OPDS Scopper Gaban, 바깥 zip → .rar → AoT식 .blend). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story05_gaban.py -- [--out DIR]
원본 rar: OPDSScopperGaban.blend(**가반만** — 도끼·몸·머리 3메시, 뼈 70) + role_leilijiaba_skin.fbx(레일리, 쓰지 않는다) + xps.xps(안 씀) + 텍스처 2.
  · 가반 .blend 바인드 자세 = 이미 서 있는 자세(팔이 몸 옆으로 내려와 양손에 도끼를 쥠, 정면 −Y) → 팔 내림 없이 그대로 굳힌다(T자 아님).
  · 도끼(메시 5_-axe, 뼈 Prop1·Prop2로 양손에 매임) 2자루가 한 메시 — 손에 붙은 채 그대로 둔다.
  · 재질 3(도끼=몸 텍스처 공유 · 몸 · 머리) → 이미지 2장(role_jiaba_shenti·role_jiaba_tou, 1024). 같은 그림은 합쳐 `건물_가반_01·02`. 색만.
규격: 원점 바닥 가운데 · 정면 −Y · 바닥 45×45 · 높이 40~90 · 삼각형 1만 이하 · 재질·텍스처 `건물_가반_NN`. 발이 z=0.
"""
import math
import os
import sys
import zipfile

import bmesh
import bpy
import subprocess
import numpy as np
from mathutils import Matrix

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story05_구일중학교_가반.zip")
NAME = "Story05_구일중학교"
FOOT = 44.0
TRI_LIMIT = 10000
PARTS = {"5_body_1.0_0_0": "role_jiaba_shenti.png", "5_-axe_1.0_0_0": "role_jiaba_shenti.png", "5_head_1.0_0_0": "role_jiaba_tou.png"}   # 재질 → 이미지(도끼는 몸과 같은 그림)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story05_가반")
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
    work = "/tmp/gen_story05_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    rar = next(os.path.join(work, "source", f) for f in os.listdir(os.path.join(work, "source")) if f.endswith(".rar"))
    inner = os.path.join(work, "inner")
    os.makedirs(inner, exist_ok=True)
    subprocess.run(["bsdtar", "-xf", rar, "-C", inner], check=True)      # rar — bsdtar(맥 기본)로 풀린다(7z는 RAR5를 0바이트로 푸는 일이 있다)
    gdir = os.path.join(inner, "OPDS - Scopper Gaban")
    bpy.ops.wm.open_mainfile(filepath=os.path.join(gdir, "OPDSScopperGaban.blend"))
    sc = bpy.context.scene
    print("클립", [a.name for a in bpy.data.actions] or "없음(바인드 자세 = 서 있는 자세)")
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    mat_names = []
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        m2 = me.copy()
        e.to_mesh_clear()
        m2.transform(e.matrix_world)
        key = PARTS[o.data.materials[0].name]      # 도끼·몸은 같은 이미지 → 한 재질로 합쳐진다
        if key not in mat_names:
            mat_names.append(key)
        for p in m2.polygons:
            p.material_index = mat_names.index(key)
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
        name = f"건물_가반_{i + 1:02d}"
        img = bpy.data.images.load(os.path.join(gdir, key))
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
        print("텍스처", name, "←", key, im.size[:])
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
    cam.data.ortho_scale = 110
    for name, loc, rot in (("앞", (0, -150, 38), (90, 0, 0)), ("옆", (150, 0, 38), (90, 0, 90)), ("위", (0, 0, 150), (0, 0, 0)),
                           ("비스듬", (100, -100, 90), (60, 0, 45))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
