"""Story09_7탄약창 ← Fat Titan(진격의 거인 뚱뚱한 거인, zip 안 zip 안 AoTWAFatTitan.blend). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story09_fattitan.py -- [--out DIR] [--arm 도]
원본: 맨 바깥 zip(source/Fat Titan.zip + textures 3장) → 안쪽 zip → AoTWAFatTitan.blend(블렌더 파일). 뼈(Bip001 계열 69) + 메시 3(몸 9,494 · 얼굴 4,930 · 머리카락 3,348 = 17,772삼각형).
  · **클립 없음**(액션 0). 바인드 자세 = 정면 −Y를 보는 **T자**(팔 수평).
  · 팔이 수평이면 폭이 4.8(키 4.2)이라 정사각 바닥에 넓게 퍼진다 → 어깨~위팔 뼈를 포즈로 내려(팔 내림 `--arm` 도, 기본 70° — 높이 90 규격 안에서 가장 많이 내린 각(80°면 높이 102)) 자연스러운 서 있는 자세로 굳힌다. `--arm 0`이면 T자 그대로.
  · 재질 3(몸·얼굴·머리카락) = 텍스처 `monster_wugoujuren_03_<부위>_tex.png` (블렌드 안 경로는 `…texX.png`라 깨져 있어 바깥 zip의 textures/로 다시 잇는다). 색만.
규격: 원점 바닥 가운데 · 정면 −Y · 바닥 45×45 안 · 높이 40~90 · 삼각형 1만 이하 · 재질·텍스처 `건물_거인_NN`. 발이 z=0.
"""
import math
import os
import sys
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story09_7탄약창_뚱뚱한거인.zip")
NAME = "Story09_7탄약창"
FOOT = 44.0
TRI_LIMIT = 10000
PARTS = {"5_body_1_0_0": "body", "5_face_1_0_0": "face", "5_hair_1_0_0": "hair"}      # 재질 → 텍스처 부위 (→ 건물_거인_01~03)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story09_거인")
    arm_deg = 70.0
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--arm":
            arm_deg = float(next(it))
    work = "/tmp/gen_story09_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    with zipfile.ZipFile(os.path.join(work, "source", "Fat Titan.zip")) as z:
        z.extractall(os.path.join(work, "inner"))
    bpy.ops.wm.open_mainfile(filepath=os.path.join(work, "inner", "AoTWAFatTitan.blend"))
    sc = bpy.context.scene
    print("클립", [a.name for a in bpy.data.actions] or "없음(바인드 자세 = T자)")
    arm = bpy.data.objects["Armature"]
    if arm_deg:
        # 팔 내림: 위팔 뼈를 세계 Y축(앞뒤)으로 돌린다 — 왼팔(+x)은 +θ, 오른팔(−x)은 −θ. 아래팔·손은 위팔을 따라간다.
        for side, sign in (("L", 1), ("R", -1)):
            pb = arm.pose.bones[f"Bip001 {side} UpperArm"]
            head = pb.head.copy()
            R = Matrix.Translation(head) @ Matrix.Rotation(math.radians(arm_deg) * sign, 4, "Y") @ Matrix.Translation(-head)
            pb.matrix = R @ pb.matrix
        bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    mat_names = []
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        m2 = me.copy()
        e.to_mesh_clear()
        m2.transform(e.matrix_world)
        key = o.data.materials[0].name
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
        name = f"건물_거인_{i + 1:02d}"
        img = bpy.data.images.load(os.path.join(work, "textures", f"monster_wugoujuren_03_{PARTS[key]}_tex.png"))
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
        print("텍스처", name, "←", PARTS[key], im.size[:])
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
