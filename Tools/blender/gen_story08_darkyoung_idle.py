"""Story08 Dark Young — 시범①: 원본 대기 클립(프레임 1~100, 24fps)을 살린 FBX. PM 지시 2026-10-03.
    blender -b --factory-startup --python Tools/blender/gen_story08_darkyoung_idle.py -- [--out DIR] [--bright F]
정적판(gen_story08_darkyoung.py)과 같은 키·위치: 프레임 1 평가 메시의 bbox로 배율 s(가장 긴 XY=44)·XY 중앙·바닥 z=0을 구해
armature 위치와 FBX global_scale에 먹인다(뼈·클립 이동값도 같이 배율을 받는다). 클립 이름 `Idle`, 첫·끝 프레임이 같은 순환.
"""
import math
import os
import sys
import zipfile

import bpy
import numpy as np

SRC_ZIP = os.path.expanduser("~/Desktop/구랜디스킨모음/92_스토리스킨/Story08_사이버넷_다크영오버로드.zip")
NAME = "Story08_사이버넷"
FOOT = 44.0
TEX = {"Core": "Core_Color.png", "Dant": "Dant_Color.png"}


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/스토리8_DarkYoung/대기동작판")
    if "--out" in args:
        out = args[args.index("--out") + 1]
    bright = float(args[args.index("--bright") + 1]) if "--bright" in args else None   # 정적판과 같은 의미: 거의 검정인 텍스처의 바닥 sRGB 명도
    work = "/tmp/gen_story08_idle_work"
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(SRC_ZIP) as z:
        z.extractall(work)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(work, "source", "Dark_Young.fbx"))
    sc = bpy.context.scene
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    act = bpy.data.actions[0]
    act.name = "Idle"
    arm.animation_data_create()
    arm.animation_data.action = act
    sc.frame_start, sc.frame_end = 1, 100
    sc.frame_set(1)
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in meshes:
        e = o.evaluated_get(dg)
        me = e.to_mesh()
        pts.append(np.array([(e.matrix_world @ v.co)[:] for v in me.vertices]))
        e.to_mesh_clear()
    V = np.concatenate(pts)
    lo, hi = V.min(0), V.max(0)
    s = FOOT / max(hi[0] - lo[0], hi[1] - lo[1])
    off = (-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2])
    arm.location = off
    # 원본 클립이 armature 객체의 위치·회전·스케일도 키로 갖고 있어(전부 항등) 내보낼 때 armature.location을 덮어쓴다 → 위치 키 값에 이동량을 더한다
    for l in act.layers:
        for st in l.strips:
            for cb in st.channelbags:
                for fc in cb.fcurves:
                    if fc.data_path == "location":
                        for k in fc.keyframe_points:
                            k.co[1] += off[fc.array_index]
                            k.handle_left[1] += off[fc.array_index]
                            k.handle_right[1] += off[fc.array_index]
                        fc.update()
    print("배율", round(s, 3), "armature 이동(원본 단위)", [round(x, 3) for x in arm.location], "뼈", len(arm.data.bones))
    # 재질 ← 텍스처(정적판과 같은 이름 건물_다크영_NN, 1024로 줄임)
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    mat_names = []
    for o in meshes:
        for m in o.data.materials:
            if m.name not in mat_names:
                mat_names.append(m.name)
    newmats = {}
    for i, key in enumerate(mat_names):
        name = f"건물_다크영_{i + 1:02d}"
        img = bpy.data.images.load(os.path.join(work, "textures", TEX[key]))
        w, h = img.size
        arr = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(arr)
        arr = arr.reshape(h, w, 4)
        arr[..., 3] = 1.0
        if bright is not None:
            rgb = arr[..., :3]
            srgb = np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(np.maximum(rgb, 1e-8), 1 / 2.4) - 0.055)
            lum = srgb @ np.array([0.2126, 0.7152, 0.0722])
            med = float(np.median(lum))
            if med < 0.1:
                top = max(float(np.percentile(lum, 99.5)), 0.02)
                srgb = bright + (1 - bright) * np.power(np.clip(srgb / top, 0, 1), 0.5)
            arr[..., :3] = np.where(srgb <= 0.04045, srgb / 12.92, np.power((srgb + 0.055) / 1.055, 2.4))
            print("밝기", name, "중앙", round(med, 3), "→ 적용" if med < 0.1 else "→ 그대로")
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
        newmats[key] = mat
    for o in meshes:
        for i, m in enumerate(o.data.materials):
            o.data.materials[i] = newmats[m.name]
    os.makedirs(out, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, NAME + ".fbx"), use_selection=True, global_scale=s, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE", add_leaf_bones=False,
                             object_types={"ARMATURE", "MESH"}, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0, bake_anim_step=1.0,
                             path_mode="RELATIVE")
    print("내보냄", out, "클립", act.name, "길이(초)", round(99 / sc.render.fps, 2))


if __name__ == "__main__":
    main()
