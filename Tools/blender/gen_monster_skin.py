"""바다 괴수 스킨(뼈 있는 립) → Assets/Art/Monsters/<이름>.fbx — 원점 = 수면, 절차적 `Idle_Breath`.

  blender -b --factory-startup --python Tools/blender/gen_monster_skin.py -- [이름 ...] [--out DIR] [--render DIR]

첫 항목: 거대 해왕류를 모모우로 교체(2026-09-25, 사장님 「해왕류 스킨 이걸로 교체」).
옛 해왕류는 Tools/blender/gen_seaking.py가 **직접 지은** 바다뱀이다 — 그 설정은 그대로 남겨 둔다
(되돌리려면 그쪽을 돌리면 같은 파일 이름으로 나간다). 규약은 그 머리말과 같다:
  🔴 원점 = 수면(z=0). 물에 잠기는 부분은 음수 높이 — 게임 바다 판이 가린다(ArtBinder.WaterlineModels).
  🔴 텍스처는 Assets/Art/Monsters/Textures/<재질이름>.png — ArtBinder가 재질 이름으로 찾는다.
  🔴 클립 이름에 `Idle`이 들어가야 BlenderClipPostprocessor가 반복시킨다. 8초·30fps·240프레임, 첫 = 끝.
  크기는 게임이 ArtBinder.EnemyModels 표의 높이로 맞춘다 — 여기 height_m은 옛 파일과 같은 자릿수로 두려는 것뿐이다
  (옛 해왕류 전체 높이 191.1 = 16.76m × 11.4).

왜 gen_animal_skin이 아닌가: 그쪽은 **원본에 애니메이션이 있는 glb**를 살리는 도구다. 모모우는 FBX(XPS 립)이고
클립이 0개라 뼈로 숨쉬기를 새로 짓는다. 사람형이 아니라 gen_objrip_skin(휴머노이드 리깅)도 아니다.
"""
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
UNITS_PER_METER = 11.4                     # gen_seaking과 같은 내보내기 배율(사람 키 20 = 1.75m)
FRAMES, FPS = 240, 30

SKINS = {
    # 원피스 파이팅 패스(OPFP) 모모우 XPS/FBX 립 → 거대 해왕류. zip 안에 source/*.rar(FBX+XPS+PNG) + textures/35003_X.png.
    #   원본 부품: 메시 1(35003.001, 정점 2,926 · 삼각형 4,646) · 재질 1(35003) · 그림 1(35003_X.png 1024², 컬러) · 뼈 18 · 클립 0.
    #   xps.xps도 같은 18뼈·메시 1(5_momoo_1_0_0)·같은 그림이라 FBX만 쓴다.
    #   zip의 textures/35003_X.png와 rar 안 것은 **바이트는 다르고 픽셀은 같다**(RGB ↔ RGBA, 알파 전부 255) — zip 쪽을 쓴다.
    #   🔴 원본 재질은 **같은 컬러 그림을 노멀맵에도 꽂고 있다**(임포터가 만든 NORMAL_MAP 노드) — 그대로면 표면이 얼룩진다. 재질을 새로 짓는다.
    #   자세: 정면 −Y(머리 y −0.06), 몸을 세우고 꼬리를 뒤(+Y)로 말아 올렸다. 원본 크기 길이(y) 0.190 · 폭(x) 0.151 · 높이(z) 0.130.
    #   수면: 아래에서 40%(수면 위 60% — 옛 해왕류 116.3/191.1 = 61%와 같은 비율). 머리·가슴·등 볏·꼬리지느러미가 물 위,
    #   가슴지느러미와 꼬리 중간이 물속(라이브 창에 물판을 대 보고 정함).
    #   방향: 옛 해왕류는 머리가 +X인 **옆모습** 구도였다. 모모우는 몸이 짧고 앞을 보는 자세라 정면 −Y(다른 적·유닛과 같은 규약)로 둔다.
    "거대해왕류": dict(
        source="~/Desktop/구랜디스킨모음/90_적유닛/해왕류/거대해왕류_momoo.zip",
        inner_fbx="35003.fbx",
        texture="textures/35003_X.png",
        path="Assets/Art/Monsters/거대해왕류.fbx",
        material="해왕류_모모우",
        armature_name="거대해왕류_뼈대",
        mesh_name="거대해왕류",
        height_m=16.76,
        waterline=0.40,
        # 숨쉬기 — (뼈, 축(뼈대 공간), 진폭°, 주기 배수, 위상(주기 비율)). 주기 배수는 정수라야 첫 = 끝이 된다.
        motion=[
            ("Bip001 Spine", "X", 1.5, 1, 0.00),
            ("Bip001 Spine1", "X", 1.5, 1, 0.05),
            ("Bip001 Neck", "X", 1.5, 1, 0.10),
            ("Bip001 Head", "X", 2.5, 1, 0.15),
            ("Bip001 Head", "Z", 2.0, 1, 0.40),
            ("Bone009", "X", 4.0, 2, 0.20),            # 머리 위 뿔·볏
            ("Bone001", "Y", 8.0, 2, 0.00),            # 왼 가슴지느러미 — 퍼덕
            ("Bone002", "Y", 6.0, 2, 0.08),
            ("Bone001(mirrored)", "Y", -8.0, 2, 0.00),  # 오른쪽은 거울(부호 반대)
            ("Bone002(mirrored)", "Y", -6.0, 2, 0.08),
            ("Bone004", "Z", 2.0, 1, 0.00),            # 꼬리 — 뒤로 갈수록 늦고 크게
            ("Bone005", "Z", 3.0, 1, 0.08),
            ("Bone006", "Z", 4.0, 1, 0.16),
            ("Bone007", "Z", 5.0, 1, 0.24),
            ("Bone006", "X", 2.0, 1, 0.30),
            ("Bone007", "X", 3.0, 1, 0.38),
        ],
        bob_m=0.10,                               # 몸 전체 오르내림(미터, 내보내기 전) — 옛 해왕류 0.12
    ),
}


def extract(cfg):
    """zip → source/*.rar → 임시 폴더. (fbx 경로, 텍스처 경로, 임시 폴더)를 준다."""
    tmp = tempfile.mkdtemp(prefix="monster_")
    with zipfile.ZipFile(os.path.expanduser(cfg["source"])) as z:
        z.extractall(tmp)
    rars = [os.path.join(r, f) for r, _, fs in os.walk(os.path.join(tmp, "source")) for f in fs if f.lower().endswith(".rar")]
    assert len(rars) == 1, f"source/ 안 rar이 하나가 아니다: {rars}"
    out = os.path.join(tmp, "x")
    subprocess.run(["unar", "-q", "-o", out, rars[0]], check=True)
    fbx = [os.path.join(r, f) for r, _, fs in os.walk(out) for f in fs if f == cfg["inner_fbx"]]
    assert len(fbx) == 1, f"{cfg['inner_fbx']}를 못 찾음"
    tex = os.path.join(tmp, cfg["texture"])
    assert os.path.isfile(tex), f"텍스처 없음: {tex}"
    return fbx[0], tex, tmp


def build(name, cfg, out_dir=None, render_dir=None):
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name}
    fbx, tex_src, tmp = extract(cfg)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    bpy.ops.import_scene.fbx(filepath=fbx)
    arm = next(o for o in scene.objects if o.type == "ARMATURE")
    meshes = [o for o in scene.objects if o.type == "MESH"]
    assert len(meshes) == 1, f"메시가 하나가 아니다 {[o.name for o in meshes]}"
    body = meshes[0]
    report["원본"] = {"메시": len(meshes), "정점": len(body.data.vertices),
                     "삼각형": sum(len(p.vertices) - 2 for p in body.data.polygons),
                     "재질": [m.name for m in body.data.materials], "뼈": len(arm.data.bones),
                     "클립": len(bpy.data.actions)}
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    # 🔴 메시가 뼈대의 자식이다 — 그대로 두 행렬을 옮기면 메시가 **두 번** 옮겨진다. 먼저 풀고 끝에 다시 잇는다.
    mw = body.matrix_world.copy()
    body.parent = None
    body.matrix_world = mw

    # 겉싸개(뼈대 ×0.01·X 90°, 메시 X −90°)를 정점·뼈에 굽는다 — 유니티에 회전·배율이 남으면 WaterlineModels 축이 틀어진다.
    for o in scene.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # 크기·수면: 키를 height_m에 맞추고, 아래에서 waterline 비율 되는 높이를 z=0으로. x·y는 발밑(바닥 5%) 말고 **전체 경계 중심**.
    bpy.context.view_layer.update()
    P = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    lo, hi = P.min(0), P.max(0)
    s = cfg["height_m"] / float(hi[2] - lo[2])
    cx, cy = float((lo[0] + hi[0]) / 2), float((lo[1] + hi[1]) / 2)
    z0 = float(lo[2] + (hi[2] - lo[2]) * cfg["waterline"])
    report["원본 크기(길이y·폭x·높이z)"] = [round(float(hi[1] - lo[1]), 4), round(float(hi[0] - lo[0]), 4), round(float(hi[2] - lo[2]), 4)]
    report["배율"] = round(s, 4)
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -z0))
    arm.matrix_world = G @ arm.matrix_world
    body.matrix_world = G @ body.matrix_world
    for o in scene.objects:
        o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body.parent = arm                             # 둘 다 항등 행렬이라 부모 역행렬이 필요 없다
    mods = [m for m in body.modifiers if m.type == "ARMATURE"]
    assert len(mods) == 1 and mods[0].object == arm, "Armature 모디파이어가 하나가 아니다"
    arm.name = arm.data.name = cfg["armature_name"]
    body.name = body.data.name = cfg["mesh_name"]

    # 노멀 — 바깥으로 맞춘다(유니티 URP는 뒷면을 버린다). 바뀐 면 수를 적는다.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    before = [f.normal.copy() for f in bm.faces]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    report["노멀 뒤집힌 면(고침)"] = sum(1 for f, n in zip(bm.faces, before) if f.normal.dot(n) < 0)
    bm.to_mesh(body.data)
    bm.free()

    # 재질 — 하나로 새로 짓는다(원본 NORMAL_MAP 노드 제거). 텍스처는 재질 이름으로.
    os.makedirs(tex_dir, exist_ok=True)
    tex_dst = os.path.join(tex_dir, cfg["material"] + ".png")
    shutil.copy2(tex_src, tex_dst)
    mat = bpy.data.materials.new(cfg["material"])
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(tex_dst)
    nt.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    body.data.materials.clear()
    body.data.materials.append(mat)
    report["재질"] = cfg["material"]

    # Idle_Breath — 사인만, 주기 배수는 정수 → 첫 프레임 = 끝 프레임.
    arm.animation_data_create()
    act = bpy.data.actions.new("Idle_Breath")
    arm.animation_data.action = act
    axes = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}
    by_bone = {}
    for bone, ax, amp, mult, phase in cfg["motion"]:
        assert bone in arm.data.bones, f"motion의 뼈 {bone}가 없다"
        by_bone.setdefault(bone, []).append((axes[ax], math.radians(amp), mult, phase))
    root = next(b for b in arm.data.bones if b.parent is None)
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    bob = cfg["bob_m"]
    for f in range(FRAMES + 1):
        t = f / FRAMES
        for bone, terms in by_bone.items():
            pb = arm.pose.bones[bone]
            R = pb.bone.matrix_local.to_quaternion()          # 뼈 쉬는 방향(뼈대 공간)
            q = Quaternion()
            for axis, amp, mult, phase in terms:
                q = Quaternion(axis, amp * math.sin(2 * math.pi * (mult * t - phase))) @ q
            pb.rotation_quaternion = R.inverted() @ q @ R      # 뼈대 공간 축 → 뼈 로컬
            pb.keyframe_insert("rotation_quaternion", frame=f + 1)
        rp = arm.pose.bones[root.name]
        Rr = root.matrix_local.to_3x3()
        rp.location = Rr.inverted() @ Vector((0, 0, bob * math.sin(2 * math.pi * t)))
        rp.keyframe_insert("location", frame=f + 1)
    scene.frame_start, scene.frame_end = 1, FRAMES + 1
    report["클립"] = {"이름": act.name, "프레임": [1, FRAMES + 1], "fps": FPS}

    bpy.context.view_layer.update()
    scene.frame_set(1)
    V = np.array([v.co for v in body.data.vertices])
    report["크기(m, 길이y·폭x·높이z)"] = [round(float(np.ptp(V[:, 1])), 3), round(float(np.ptp(V[:, 0])), 3), round(float(np.ptp(V[:, 2])), 3)]
    report["수면 위 높이(m)·비율"] = [round(float(V[:, 2].max()), 3), round(float(V[:, 2].max() / np.ptp(V[:, 2])), 3)]

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    for o in scene.objects:
        o.select_set(o in (arm, body))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=True, global_scale=UNITS_PER_METER, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE", add_leaf_bones=False,
        path_mode="STRIP", embed_textures=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    report["출력"] = dst
    shutil.rmtree(tmp, ignore_errors=True)
    if render_dir:
        report["렌더"] = judge(name, arm, body, render_dir)
    return report


def judge(name, arm, body, out):
    """판정 렌더 — 정면·옆면·위, 뒷면 컬링 켬, 수면 판 겹쳐서."""
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_backface_culling = True
    scene.render.resolution_x, scene.render.resolution_y = 900, 700
    scene.world = bpy.data.worlds.new("판정")
    scene.world.color = (0.3, 0.3, 0.3)
    cam = bpy.data.objects.new("판정_카메라", bpy.data.cameras.new("판정_카메라"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    V = np.array([v.co for v in body.data.vertices])
    cam.data.ortho_scale = float(np.ptp(V, axis=0).max()) * 1.4
    zc = float((V[:, 2].max() + V[:, 2].min()) / 2)
    views = [("front", (0, -60, zc), (math.radians(90), 0, 0)),
             ("side", (60, 0, zc), (math.radians(90), 0, math.radians(90))),
             ("top", (0, 0, 60), (0, 0, 0))]
    paths = []
    for tag, loc, rot in views:
        cam.location, cam.rotation_euler = loc, rot
        p = os.path.join(out, f"{name}_{tag}.png")
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    return paths


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = render_dir = None
    names = []
    it = iter(args)
    for a in it:
        if a == "--out":
            out_dir = next(it)
        elif a == "--render":
            render_dir = next(it)
        else:
            names.append(a)
    for n in names or list(SKINS):
        print("괴수스킨  " + json.dumps(build(n, SKINS[n], out_dir, render_dir), ensure_ascii=False))


if __name__ == "__main__":
    main()
