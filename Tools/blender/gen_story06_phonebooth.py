"""Story06_구일고등학교 ← 공중전화 부스(zip: source/phonebooth.fbx + Unity 표준 셰이더 텍스처). blender 세션, 2026-10-02 PM 지시.
    blender -b --factory-startup --python Tools/blender/gen_story06_phonebooth.py -- [--out DIR] [--rot 도]
원본: 메시 7(Booth 틀·유리 / Book·callText·Can·Cube 소품 / phone 3,433삼각형) · 재질 4(Booth·other·Phone·PhoneNum) · 삼각형 4,235 — 감량 불필요.
  FBX는 재질에 이미지를 안 물고 있다(Unity 출력물) → 재질 이름으로 `phonebooth_<재질>_AlbedoTransparency_sRGB.png`를 직접 잇는다(PhoneNum만 파일명이 `_sRG.png`).
  Metallic·Normal·Emission은 버린다(색 텍스처만).
🔴 알파: 다른 셋은 알파가 전부 1(불투명)인데 **Booth만 알파 최소 0.25, 16.5%가 반투명 = 유리창**. 불투명으로 굳히면 안쪽 전화기가 가려지므로 **알파 컷**:
  알파 < 0.5 → 완전 투명(0), 나머지 → 1. 투명이 된 칸의 RGB는 옅은 하늘색으로 채운다 — 알파 컷을 못 쓰는(불투명) 셰이더로 보여도 하늘색 유리로 읽히게.
  나머지 텍스처는 알파 채널 없이 저장.
규격: 원점 바닥 가운데 · 정면 −Y · 바닥(가장 긴 변) 44 · 높이 90 이하 · 재질·텍스처 `건물_전화부스_NN`.
"""
import math
import os
import sys
import zipfile

import bpy
import numpy as np
from mathutils import Matrix

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story06_구일고등학교_공중전화부스.zip")
NAME = "Story06_구일고등학교"
FOOT = 44.0
GLASS_RGB = np.array([0.72, 0.88, 0.96])
ORDER = ["Booth", "other", "Phone", "PhoneNum"]          # → 건물_전화부스_01~04
ALBEDO = {"Booth": "phonebooth_Booth_AlbedoTransparency_sRGB.png", "other": "phonebooth_other_AlbedoTransparency_sRGB.png",
          "Phone": "phonebooth_Phone_AlbedoTransparency_sRGB.png", "PhoneNum": "phonebooth_PhoneNum_AlbedoTransparency_sRG.png"}


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/Story06_전화부스")
    rot = -90.0                                   # 전화기·문 쪽이 원본 +X → 정면 −Y로
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--rot":
            rot = float(next(it))
    work = "/tmp/gen_story06_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(work, "source", "phonebooth.fbx"))
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in meshes:
        o.data.transform(o.matrix_world)
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
    for o in bpy.data.objects:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = body.data.name = NAME
    me = body.data
    if rot:
        me.transform(Matrix.Rotation(math.radians(rot), 4, "Z"))
    V = np.array([v.co[:] for v in me.vertices])
    lo, hi = V.min(0), V.max(0)
    s = FOOT / max(hi[0] - lo[0], hi[1] - lo[1])
    me.transform(Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])))
    V = np.array([v.co[:] for v in me.vertices])
    dims = V.max(0) - V.min(0)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print("크기(게임 단위)", dims.round(2).tolist(), "삼각형", tris, "배율", round(s, 2), "최저 z", round(float(V[:, 2].min()), 3))
    assert dims[2] <= 90.0, "높이 규격(90) 초과"
    # 텍스처 + 재질
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    mats = {}
    for i, key in enumerate(ORDER):
        name = f"건물_전화부스_{i + 1:02d}"
        img = bpy.data.images.load(os.path.join(work, "textures", ALBEDO[key]))
        w, h = img.size
        arr = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(arr)
        arr = arr.reshape(h, w, 4)
        info = ""
        if key == "Booth":
            glass = arr[..., 3] < 0.5
            arr[glass, :3] = GLASS_RGB
            arr[..., 3] = np.where(glass, 0.0, 1.0)
            info = f"알파 컷: 투명 {glass.mean() * 100:.1f}% (반투명 {float(((arr[..., 3] > 0) & (arr[..., 3] < 1)).mean()):.3f})"
        else:
            arr[..., 3] = 1.0
        im = bpy.data.images.new(name, w, h, alpha=(key == "Booth"))
        im.pixels.foreach_set(arr.ravel())
        if max(w, h) > 1024:
            im.scale(1024, 1024)
        im.filepath_raw = os.path.join(tdir, name + ".png")
        im.file_format = "PNG"
        im.save()
        print("텍스처", name, "←", ALBEDO[key], im.size[:], info)
        # 유리 재질은 `_잎카드` 꼬리 — NatureMaterialPostprocessor가 양면 + 알파 컷으로 짓는다(PM 2026-10-02). 텍스처 파일명은 그대로.
        mat = bpy.data.materials.new(name + "_잎카드" if key == "Booth" else name)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        o_ = nt.nodes.new("ShaderNodeOutputMaterial")
        b = nt.nodes.new("ShaderNodeBsdfPrincipled")
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = im
        nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
        if key == "Booth":
            nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
            mat.blend_method = "CLIP" if hasattr(mat, "blend_method") else None
        nt.links.new(b.outputs["BSDF"], o_.inputs["Surface"])
        b.inputs["Metallic"].default_value = 0.0
        b.inputs["Roughness"].default_value = 0.6
        mats[key] = mat
    # 슬롯을 새 재질로 바꾼다(원본 재질 이름 → 새 재질)
    for i, slot in enumerate(body.material_slots):
        slot.material = mats[slot.material.name.split(".")[0]]
    # 같은 재질이 슬롯 둘이면 합친다
    seen = {}
    remap = {}
    for i, slot in enumerate(body.material_slots):
        remap[i] = seen.setdefault(slot.material.name, i)
    idx = [remap[p.material_index] for p in me.polygons]
    keep = sorted({remap[i] for i in remap})
    final = {old: new for new, old in enumerate(keep)}
    mat_list = [body.material_slots[i].material for i in keep]
    me.materials.clear()
    for m in mat_list:
        me.materials.append(m)
    me.polygons.foreach_set("material_index", [final[i] for i in idx])
    import collections
    print("재질별 면", {mat_list[k].name: v for k, v in collections.Counter(final[i] for i in idx).items()})
    # 내보내기(건물 규약: 미터로 줄였다가 global_scale 11.4)
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
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.film_transparent = False
    sc.render.resolution_x = sc.render.resolution_y = 900
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 110
    for name, loc, rot in (("앞", (0, -150, 45), (90, 0, 0)), ("옆", (150, 0, 45), (90, 0, 90)), ("위", (0, 0, 150), (0, 0, 0)),
                           ("비스듬", (100, -100, 100), (60, 0, 45))):
        cam.location = loc
        cam.rotation_euler = [math.radians(x) for x in rot]
        sc.render.filepath = os.path.join(odir, name + ".png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    main()
