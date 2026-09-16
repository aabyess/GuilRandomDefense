"""동물형 스킨 — 뼈·스킨·애니메이션이 이미 있는 glb를 유니티 Generic 리그 + `Idle` 클립 FBX로 옮긴다.
  (사람형은 gen_skin_rig.py의 mixamorig 22뼈 경로를 쓴다 — 이 스크립트는 네 발 달린/물고기형 등
  원본에 이미 스켈레톤+애니메이션이 있어서 그걸 그대로 살리기만 하면 되는 경우 전용.)

  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Tools/blender/gen_animal_skin.py -- 특별함_노건완 [--out DIR] [--render DIR]

결과: <유닛>.fbx(원본 뼈대·스킨 가중치·애니메이션 그대로) + Textures/(재질 이름 기준 실제 PNG, 노멀은 파일명에 `_normal`).
좌표계: 정면 −Y · 등 +Z(위) · 배 아래. 크기는 몸길이(가장 긴 축) 기준(ArtBinder.FourLeggedModels 관례 — 키가 아니라 몸길이로 맞춘다).
🔴 클립 이름에 `Idle`이 들어가야 유니티가 기본 반복 상태로 쓴다(고대의배 1차 사고: 「<뼈대>|Scene」엔 안 들어가 반복이 안 붙었다) —
   bake_anim_use_all_actions=True로 내보내기 전에 액션 이름 자체를 `Idle`로 바꿔 둔다(액션이 정확히 1개인지 assert).
"""
import json
import math
import os
import struct
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

ANIMALS = {
    "특별함_노건완": dict(
        source="~/Desktop/구랜디스킨모음/03_특별함/특별함_노건완.glb",
        path="Assets/Art/Units/특별함_노건완/특별함_노건완.fbx",
        body_mesh="Object_9",
        armature_obj="Object_6",
        exclude_meshes={"Icosphere"},           # 재질 없는 떠돌이 정점 구(placeholder) — 실측 확인, PM 사전조사엔 없었다
        length=2.0,                             # 몸길이(가장 긴 축) — ArtBinder.FourLeggedModels 크기 기준
        # 실측(깊이 평가된 메시, 월드공간): x −0.285~0.334(긴 축·머리−꼬리) · y −0.090~0.089(폭) · z −0.083~0.167(등−배).
        # x 낮은 끝(정점밀도 1148, 높은 끝은 133)이 머리(입·아가미 디테일로 촘촘) → 머리는 현재 −X, 꼬리는 +X.
        # z 양쪽 끝 중 +쪽이 더 멀리 뻗음(0.167 vs −0.083) → 등지느러미가 있는 +Z가 등, −Z가 배.
        # 목표: 머리 −Y · 등 +Z(그대로) · 배 아래. Rz(+90°): world.x=−old.y · world.y=old.x · world.z=old.z
        #  → 머리(old x=−0.285) → 새 y=−0.285(정면 −Y 방향, 맞다). 등(old z=+0.167)은 그대로 +Z 유지. 폭(y)이 새 x로.
        #  Z회전이라 행렬식 +1(순수 회전) — 반사가 아니라 노멀 뒤집기가 필요 없다.
        rotate_z=90.0,
        action_rename="Idle",
    ),
}


def glb(path):
    b = open(path, "rb").read()
    n = struct.unpack_from("<I", b, 12)[0]
    j = json.loads(b[20:20 + n])
    off = 20 + n
    blen = struct.unpack_from("<I", b, off)[0]
    return j, b[off + 8:off + 8 + blen]


def image_bytes(j, binchunk, index):
    bv = j["bufferViews"][j["images"][index]["bufferView"]]
    s = bv.get("byteOffset", 0)
    return binchunk[s:s + bv["byteLength"]]


def build(name, cfg, out_dir=None, render_dir=None):
    src = os.path.expanduser(cfg["source"])
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    scene = bpy.context.scene

    # ── 떠돌이 메시(재질 없는 placeholder 등) 제거 — 실측으로 확인된 것만, PM 사전조사에 없어도 실제로 있으면 뺀다
    for o in list(scene.objects):
        if o.type == "MESH" and o.name in cfg.get("exclude_meshes", set()):
            bpy.data.objects.remove(o, do_unlink=True)
    report["뺀 메시"] = sorted(cfg.get("exclude_meshes", set()))

    body = bpy.data.objects[cfg["body_mesh"]]
    orig_arm = bpy.data.objects[cfg["armature_obj"]]
    report["뼈"] = len(orig_arm.data.bones)
    report["원본 뼈 이름"] = "그대로 유지(번호 꼬리 포함) — Generic 리그는 이름 무관, 스킨 가중치가 뼈 인덱스로 이미 바인딩돼 있어 이름을 바꿔도 득이 없다"

    # 🔴 2026-09-15 PM 2차 버그(유니티 실측) — 이전 판(transform_apply 두 단계 굽기)은 Blender
    # 재수입으로는 멀쩡해 보였지만, 유니티가 실제로 읽는 FBX Cluster TransformLink(스킨 바인드
    # 포즈)가 손상됐다(원시 FBX 파싱으로 실측 확인: 29/30 뼈가 서로 거의 같은 엉뚱한 위치로
    # 뭉쳐 있었다, 최대 오차 32m). transform_apply가 오브젝트 트랜스폼을 edit bone에 구워
    # 넣는 과정과 그 뒤에도 남아있던 기존 애니메이션 fcurve(옛 레스트 기준 pose_bone.matrix_basis
    # 값)가 서로 안 맞아떨어진 것으로 보인다 — 더 땜질하지 않고, Blender의 스킨 바인딩·익스포트
    # 경로를 아예 안 거치는 방식으로 다시 짠다: 새 아마추어를 "원본 임포트 직후"(어떤 변형도
    # 손대기 전)의 진짜 레스트 월드 행렬로 직접 만들고, 애니메이션도 프레임마다 원본 뼈의 월드
    # 자세를 그대로 읽어 새 아마추어 위에 다시 굽는다. mesh 쪽은 이미 크기·원점이 검증됐던
    # 부분이라(정점 직접 굽기 방식) 그대로 둔다.
    orig_bone_names = [b.name for b in orig_arm.data.bones]
    orig_parent = {b.name: (b.parent.name if b.parent else None) for b in orig_arm.data.bones}
    root_name = next(n for n in orig_bone_names if orig_parent[n] is None)

    # ── 애니메이션: 액션이 정확히 하나인지 확인 (원본은 그대로 두고 나중에 새로 굽는다)
    actions = list(bpy.data.actions)
    assert len(actions) == 1, f"{name}: 액션이 1개가 아니다 {[a.name for a in actions]}"
    src_action_name = actions[0].name
    f0, f1 = actions[0].frame_range
    report["원본 클립"] = {"이름": src_action_name, "프레임범위": [round(float(f0), 3), round(float(f1), 3)]}
    frames = list(range(int(round(f0)), int(round(f1)) + 1))

    # 🔴 2026-09-15 실측으로 찾은 진짜 근본 원인 — 이 파일의 스킨 바인드 포즈(inverseBindMatrices,
    # Blender가 bone.matrix_local로 그대로 들여온 것) 자체가 못 쓸 자리다: pose_position을
    # "REST"로 두고 모디파이어를 태워 보면 물고기가 아니라 x≈6·z≈−13 언저리에 정점이 뭉친
    # 형태 없는 덩어리가 나온다(스팬 0.25). 반대로 애니메이션이 실제로 돌고 있는 "POSE" 상태
    # (프레임 0)로 태우면 우리가 알던 정상 물고기 모양(스팬 0.618)이 그대로 나온다. 즉 이
    # 파일은 "레스트=바인드"가 실제로 안 쓰는 죽은 자리이고, 실제 형태는 오직 애니메이션
    # 프레임을 거쳐야만 나온다 — 원시 FBX에서 봤던 "29개 뼈가 (0,−13,−28) 근처로 뭉친" 것도
    # 바로 이 죽은 바인드 자리였다(Blender 내보내기 버그가 아니라 원본 파일 자체의 특성).
    # → 파일의 바인드를 아예 안 쓰고, "프레임 0의 포즈"를 우리가 정할 새 레스트 기준으로
    # 대신 쓴다(수학적으로 바인드 자리를 어디로 잡든 스킨 변형 공식은 똑같이 성립한다 —
    # 새 바인드=프레임0 포즈로 두면 프레임0에서 변형이 항등이 되고, 그 항등 상태에서 구운
    # 메시가 바로 "진짜 물고기 모양"이 된다).
    scene.frame_set(frames[0])
    orig_rest_world = {bname: (orig_arm.matrix_world @ orig_arm.pose.bones[bname].matrix).copy()
                       for bname in orig_bone_names}

    # 원본 루트 뼈의 월드 위치로 루프 검사(고대의배 loop_matches와 같은 발상, 로컬이 아니라
    # 월드로 봐야 숨은 부모 변환이 있어도 정확하다)
    loc0 = orig_rest_world[root_name].translation.copy()
    scene.frame_set(frames[-1])
    loc1 = (orig_arm.matrix_world @ orig_arm.pose.bones[root_name].matrix).translation.copy()
    drift = (loc1 - loc0).length
    report["원본 루트 드리프트(m)"] = round(float(drift), 6)
    scene.frame_set(frames[0])

    # ── 재질·텍스처: glb 내장 이미지를 파일 이름 = 재질 이름 기준으로 뽑는다(노멀은 `_normal` — 유니티 자동 인식)
    j, binchunk = glb(src)
    os.makedirs(tex_dir, exist_ok=True)
    mat = body.material_slots[0].material if body.material_slots else None
    assert mat is not None, f"{name}: 재질 없음"
    mt = next(m for m in j["materials"] if m["name"] == mat.name)
    base_src = j["textures"][mt["pbrMetallicRoughness"]["baseColorTexture"]["index"]]["source"]
    normal_src = j["textures"][mt["normalTexture"]["index"]]["source"]
    base_path = os.path.join(tex_dir, f"{mat.name}_baseColor.png")
    normal_path = os.path.join(tex_dir, f"{mat.name}_normal.png")
    open(base_path, "wb").write(image_bytes(j, binchunk, base_src))
    open(normal_path, "wb").write(image_bytes(j, binchunk, normal_src))
    report["재질"] = mat.name
    report["텍스처"] = {"baseColor": os.path.basename(base_path), "normal": os.path.basename(normal_path)}

    # 재질 노드를 베이스+노멀만 남기고 다시(금속 0·발광 끔·불투명) — gen_skin_rig.rebuild_material과 같은 결
    nt = mat.node_tree
    for n in [n for n in nt.nodes if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL")]:
        nt.nodes.remove(n)
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    if "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = 0.0
    base_node = nt.nodes.new("ShaderNodeTexImage")
    base_node.image = bpy.data.images.load(base_path, check_existing=True)
    base_node.location = (-700, 300)
    nt.links.new(base_node.outputs["Color"], bsdf.inputs["Base Color"])
    normal_img = nt.nodes.new("ShaderNodeTexImage")
    normal_img.image = bpy.data.images.load(normal_path, check_existing=True)
    normal_img.image.colorspace_settings.name = "Non-Color"
    normal_img.location = (-700, -40)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.location = (-350, -40)
    nt.links.new(normal_img.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"

    # 🔴 2026-09-15 실측으로 발견한 세 번째 함정 — body(Object_9)의 "로컬" 정점 데이터는 그
    # 자체로는 물고기 모양이 아니다. **더 중요한 발견**: pose_position="REST"(파일 자체의
    # 바인드 포즈)로 태워 봐도 물고기가 안 나온다(x≈6·z≈−13 언저리에 뭉친 형태 없는 덩어리,
    # 스팬 0.25) — 이 파일은 바인드 자리 자체가 죽은 쓰레기 값이고, 실제 물고기 모양은
    # 오직 'Scene' 애니메이션이 도는 POSE 상태(프레임 0)에서만 나온다(스팬 0.618, 우리가
    # 알던 정상 크기). 그래서 REST를 강제하지 않고 프레임 0(scene.frame_set(frames[0])이
    # 방금 이미 되어 있다)의 POSE 상태 그대로 모디파이어를 한 번 태워 "물고기 모양"을 새
    # 정적 메시로 구워낸다. 이 프레임을 새 아마추어의 레스트 기준으로도 동시에 쓰므로
    # (orig_rest_world가 이미 이 프레임에서 잡혔다) 수학적으로 앞뒤가 맞는다 — 프레임 0에서
    # 변형이 항등이 되게 새 바인드를 잡았으니, 프레임 0에서 구운 모양이 곧 새 레스트 모양이다.
    dg = bpy.context.evaluated_depsgraph_get()
    baked_mesh = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old_mesh = body.data
    body.data = baked_mesh
    old_mesh.name = old_mesh.name + "_raw_template_unused"
    bpy.data.meshes.remove(old_mesh)
    for mod in list(body.modifiers):
        if mod.type == "ARMATURE":
            body.modifiers.remove(mod)          # 옛 orig_arm 대상 모디파이어 제거 — 이제부턴 순수 정점 변환만
    report["레스트 포즈로 구운 실제 몸 정점 수"] = len(body.data.vertices)

    # ── 방향·크기: 이제 body.data가 "진짜 물고기 모양"이므로 raw 정점을 그대로 재도 된다.
    V0 = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    span_before = float(V0[:, 0].max() - V0[:, 0].min())          # 머리−꼬리 축(x) 길이, 회전 전
    report["회전 전 몸길이(m)"] = round(span_before, 4)

    Rz = Matrix.Rotation(math.radians(cfg["rotate_z"]), 4, "Z")
    s = cfg["length"] / span_before
    S = Matrix.Scale(s, 4)

    def apply_only(obj):
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        if obj.parent is not None:
            bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # 메시만 굽는다 — 뼈대(orig_arm)는 이제 손대지 않는다(측정용으로만 쓰고 나중에 통째로 지운다).
    apply_only(body)
    report["숨은 임포트 변환 제거 뒤 body 항등인가"] = body.matrix_world == Matrix.Identity(4)

    body.matrix_world = S @ Rz @ body.matrix_world
    apply_only(body)

    V1 = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    cx, cy = float(V1[:, 0].min() + V1[:, 0].max()) / 2, float(V1[:, 1].min() + V1[:, 1].max()) / 2
    belly_z = float(V1[:, 2].min())
    Tc = Matrix.Translation((-cx, -cy, -belly_z))

    body.matrix_world = Tc @ body.matrix_world
    apply_only(body)

    # 🔴 2026-09-15 PM 지시 — 뼈대는 더 이상 오브젝트 행렬·transform_apply를 안 거친다. 새
    # 아마추어를 처음부터 "G_total(=Tc@S@Rz) @ 원본 임포트 직후 레스트 월드 행렬"로 직접 짓는다.
    # `orig_rest_world`는 임포트 직후(어떤 손질도 하기 전) 값이고, body도 첫 apply_only 직후엔
    # 정확히 같은 기준(오브젝트 행렬을 항등으로 구웠을 뿐 좌표계 자체는 그대로)이라 두 갈래가
    # 서로 어긋나지 않는다 — G_total 하나로 메시와 뼈 양쪽에 같은 변환을 준 것과 동치다.
    G_total = Tc @ S @ Rz
    report["G_total"] = [[round(v, 6) for v in row] for row in G_total]

    arm_data = bpy.data.armatures.new("Armature")
    new_arm = bpy.data.objects.new(cfg["armature_obj"] + "_new", arm_data)
    scene.collection.objects.link(new_arm)
    bpy.context.view_layer.objects.active = new_arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb_map = {}
    for bname in orig_bone_names:
        eb_map[bname] = arm_data.edit_bones.new(bname)
    for bname in orig_bone_names:
        eb = eb_map[bname]
        M = G_total @ orig_rest_world[bname]
        eb.matrix = M
        eb.length = 0.02                      # 스킨 정확도엔 영향 없음(matrix_local은 head+방향만 씀) — 시각용 임의값
    for bname in orig_bone_names:
        if orig_parent[bname]:
            eb_map[bname].parent = eb_map[orig_parent[bname]]
    bpy.ops.object.mode_set(mode="OBJECT")

    # 새 레스트 월드 위치가 _rootJoint 기준으로 orig_rest_world와 G_total만큼만 다른지 자체 점검
    new_root_rest = new_arm.matrix_world @ new_arm.data.bones[root_name].matrix_local
    expect_root_rest = G_total @ orig_rest_world[root_name]
    report["새 레스트 자체검증(_rootJoint, m)"] = round((new_root_rest.translation - expect_root_rest.translation).length, 8)

    # 메시를 새 아마추어에 다시 묶는다 — 정점그룹(뼈 이름 기준)은 그대로, 모디파이어 대상만 교체
    for mod in list(body.modifiers):
        if mod.type == "ARMATURE":
            mod.object = new_arm
            break
    else:
        mod = body.modifiers.new("Armature", "ARMATURE")
        mod.object = new_arm
    body.parent = new_arm

    # ── Idle 굽기: 매 프레임 "원본" 아마추어(orig_arm, 손 안 댐)의 월드 포즈 행렬을 그대로 읽어
    # G_total을 곱하고 새 아마추어의 포즈로 그대로 심는다 — 원본 파일의 계층·오프셋이 뭐였든
    # 상관없이 항상 올바른 새 레스트 기준으로 재구성된다. 부모→자식 순서로 굽어야 자식의
    # matrix_basis 계산이 그 프레임의 "이미 갱신된" 부모 포즈를 본다.
    topo, seen = [], set()

    def visit(n):
        if n in seen:
            return
        seen.add(n)
        topo.append(n)
        for c in orig_bone_names:
            if orig_parent[c] == n:
                visit(c)

    visit(root_name)
    for n in orig_bone_names:
        if n not in seen:
            topo.append(n)

    new_arm.animation_data_create()
    action = bpy.data.actions.new(cfg["action_rename"])
    new_arm.animation_data.action = action
    bpy.context.view_layer.objects.active = new_arm
    bpy.ops.object.mode_set(mode="POSE")
    for pb in new_arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    for f in frames:
        scene.frame_set(f)
        for bname in topo:
            world = G_total @ (orig_arm.matrix_world @ orig_arm.pose.bones[bname].matrix)
            new_arm.pose.bones[bname].matrix = world
            bpy.context.view_layer.update()
        for bname in topo:
            pb = new_arm.pose.bones[bname]
            pb.keyframe_insert(data_path="location", frame=f)
            pb.keyframe_insert(data_path="rotation_quaternion", frame=f)
            pb.keyframe_insert(data_path="scale", frame=f)
    bpy.ops.object.mode_set(mode="OBJECT")

    root_pb = new_arm.pose.bones[root_name]
    scene.frame_set(frames[0])
    nloc0 = (new_arm.matrix_world @ root_pb.matrix).translation.copy()
    scene.frame_set(frames[-1])
    nloc1 = (new_arm.matrix_world @ root_pb.matrix).translation.copy()
    report["새 루트 드리프트(m)"] = round(float((nloc1 - nloc0).length), 6)
    scene.frame_set(frames[0])

    # 원본 아마추어·액션은 이제 필요 없다 — 완전히 지운다(export 시 액션이 Idle 하나만
    # 남게 하려면 오브젝트를 지우는 것만으론 부족하다, 데이터블록 자체를 지워야 한다).
    bpy.data.objects.remove(orig_arm, do_unlink=True)
    if src_action_name in bpy.data.actions and bpy.data.actions[src_action_name].users == 0:
        bpy.data.actions.remove(bpy.data.actions[src_action_name])

    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    V2 = np.array([body.matrix_world @ v.co for v in me.vertices])
    ev.to_mesh_clear()
    report["최종 크기(m)"] = [round(float(V2[:, i].max() - V2[:, i].min()), 4) for i in range(3)]
    report["최종 최저z"] = round(float(V2[:, 2].min()), 4)
    report["최종 원점거리(xy)"] = round(float((V2[:, 0].max() + V2[:, 0].min()) / 2), 4), round(float((V2[:, 1].max() + V2[:, 1].min()) / 2), 4)

    body.name = body.data.name = "carp"

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    for o in scene.objects:
        o.select_set(o in (new_arm, body))
    bpy.context.view_layer.objects.active = new_arm
    assert [a.name for a in bpy.data.actions] == [cfg["action_rename"]], f"액션이 {cfg['action_rename']} 하나가 아니다: {[a.name for a in bpy.data.actions]}"
    assert new_arm.matrix_world == Matrix.Identity(4) and body.matrix_world == Matrix.Identity(4), "오브젝트 변환이 항등이 아니다"
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Y", axis_up="Z", add_leaf_bones=False,
        armature_nodetype="NULL", mesh_smooth_type="FACE", path_mode="RELATIVE",
        # 🔴 2026-09-15 PM 3차 버그 — primary/secondary_bone_axis를 안 주면(기본값) FBX
        # 내보내기가 뼈마다 임의 roll 기준으로 Cluster를 계산해, 스킨 변형 결과가 원시 정점과
        # 축마다 다른 비율로 어긋났다(사람형 gen_skin_rig.py는 이미 Y/X를 명시해서 이 문제가
        # 없었다). 여기도 같은 값으로 맞춘다.
        primary_bone_axis="Y", secondary_bone_axis="X",
        use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    report["출력"] = dst

    if render_dir:
        report["판정 렌더"] = judge(name, new_arm, body, render_dir, frames[0], frames[-1])
    return report


def judge(name, arm, body, out, f0, f1):
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 800, 800
    scene.world = bpy.data.worlds.new("판정")
    scene.world.color = (0.3, 0.3, 0.3)
    cam = bpy.data.objects.new("판정_카메라", bpy.data.cameras.new("판정_카메라"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.4
    views = [("side", (3, 0, 0.5), (math.radians(90), 0, math.radians(90))),
             ("top", (0, 0, 3), (0, 0, 0)),
             ("front", (0, -3, 0.5), (math.radians(90), 0, 0))]
    paths = {}
    scene.frame_set(int(round(f0)))
    for tag, loc, rot in views:
        cam.location, cam.rotation_euler = loc, rot
        scene.render.filepath = os.path.join(out, f"{name}_{tag}.png")
        bpy.ops.render.render(write_still=True)
        paths[tag] = scene.render.filepath
    mid_a, mid_b = int(round(f0 + (f1 - f0) * 0.25)), int(round(f0 + (f1 - f0) * 0.75))
    for tag, frame in (("idle_a", mid_a), ("idle_b", mid_b)):
        scene.frame_set(frame)
        cam.location, cam.rotation_euler = (3, 0, 0.5), (math.radians(90), 0, math.radians(90))
        scene.render.filepath = os.path.join(out, f"{name}_{tag}.png")
        bpy.ops.render.render(write_still=True)
        paths[tag] = scene.render.filepath
    scene.frame_set(0)
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
    for n in names or list(ANIMALS):
        r = build(n, ANIMALS[n], out_dir, render_dir)
        print("동물리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
