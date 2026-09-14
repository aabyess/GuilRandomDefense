"""스킨 리깅 — 뼈 없는 정적 glb를 mixamorig 사람형 FBX로 짓는다(사장님 「폴더+유닛 이름」 절차, 2026-09-14 첫 건 안흔함_김민준).
  blender -b --factory-startup --python Tools/blender/gen_skin_rig.py -- 안흔함_김민준 [--out DIR] [--render DIR]
결과: <유닛>.fbx + Textures/(glb 내장 이미지 원본 바이트) · 키 1.8m · 발 z 0 · 정면 −Y · Hips가 루트 · 모든 뼈 +Y가 자식 쪽 · T자 쉬는 자세.
가중치: 메시를 합친 뒤 UV 이음새로 갈라진 정점을 붙인 사본에서 자동 가중치(bone heat)를 풀고 위치로 되옮긴다 — 이음새 찢어짐 방지.
작은 딱딱한 조각(얼굴·손·소품)은 재질 이름으로 한 뼈에 100% 묶는다(rigid).
관절 표는 키 비율: x = 옆(+ = 캐릭터 왼쪽 = +X, 오른쪽은 거울) · y = 앞뒤(− = 앞) · z = 높이. 정사영 앞·옆 렌더(키 0.05 눈금)에서 읽었다."""
import json
import math
import os
import struct
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PREFIX = "mixamorig:"

SKINS = {
    "안흔함_김민준": dict(
        source="~/Downloads/gon-freecss/source/Gon Freecss.glb",
        path="Assets/Art/Units/안흔함_김민준/안흔함_김민준.fbx",
        mesh_name="Gon",
        height=1.8,
        center_band=(0.28, 0.36),                                        # 앞뒤·옆 가운데를 재는 높이(다리) — 원점이 두 다리 사이
        joints=dict(Hips=(0, 0, 0.47), Spine=(0, 0, 0.53), Spine1=(0, 0, 0.60), Spine2=(0, 0, 0.67), Neck=(0, 0, 0.745),
                    Head=(0, 0, 0.785), HeadTop=(0, 0, 0.95),
                    Shoulder=(0.025, 0.02, 0.715), Arm=(0.085, 0.02, 0.705), ForeArm=(0.215, 0.02, 0.705), Hand=(0.35, 0.02, 0.705),
                    HandTip=(0.45, 0.02, 0.705),
                    UpLeg=(0.052, 0, 0.455), Leg=(0.052, -0.005, 0.265), Foot=(0.052, 0, 0.06), ToeBase=(0.052, -0.078, 0.02),
                    ToeTip=(0.052, -0.137, 0.02)),
        # 재질 → 뼈(100%): 35 눈썹·눈, 36 얼굴, 37 가슴 405 배지, 38·41 왼손(+X), 39·42 오른손(−X), 43 찌·바늘, 44 낚싯대(오른손)
        rigid={"35": "Head", "36": "Head", "37": "Spine2", "38": "LeftHand", "41": "LeftHand", "39": "RightHand", "42": "RightHand",
               "43": "RightHand", "44": "RightHand"},
        alpha_keep={"43", "44"},                                         # 낚싯줄·바늘만 진짜 컷아웃(α<0.5 41%, 중간값 0) — 나머지 이미지 알파는 전부 1
    ),
    # 알라(코알라) — 옛 키메라 앤트 자리. 두 발로 선 T자 체형이라 사람형. 원본은 +X를 봐서 Z −90°.
    # 팔이 앞으로 약 30° 뻗고 7° 처져 있어 level_arms로 ±X에 맞춰 굽는다(쉬는 자세 = T자).
    "안흔함_강주혁": dict(
        source="~/Downloads/medium_poly_koala_3d_model_free.glb",
        path="Assets/Art/Units/안흔함_강주혁/안흔함_강주혁.fbx",
        mesh_name="Koala",
        height=1.8,
        rotate_z=-90.0,
        center_band=(0.08, 0.20),
        uv_layers=1,                                                     # 재질은 UVMap(첫 번째)만 쓴다 — Sketchfab이 붙인 나머지 4벌은 뺀다
        joints=dict(Hips=(0, 0.03, 0.29), Spine=(0, 0.03, 0.38), Spine1=(0, 0.03, 0.47), Spine2=(0, 0.02, 0.57), Neck=(0, -0.03, 0.69),
                    Head=(0, -0.06, 0.76), HeadTop=(0, -0.09, 0.97),
                    Shoulder=(0.05, -0.02, 0.68), Arm=(0.16, -0.07, 0.705), ForeArm=(0.31, -0.12, 0.705), Hand=(0.42, -0.22, 0.695),
                    HandTip=(0.49, -0.24, 0.685),
                    UpLeg=(0.075, 0.01, 0.26), Leg=(0.085, -0.01, 0.155), Foot=(0.09, -0.01, 0.065), ToeBase=(0.10, -0.10, 0.015),
                    ToeTip=(0.11, -0.16, 0.01)),
        rigid={},
        alpha_keep=set(),
        # 재질 → (BSDF 입력, glb 재질의 텍스처 칸, 파일 이름). 금속·거칠기·발광 맵은 안 씀. 옛 0.jpg·1.png·2.jpg(키메라 앤트)와 이름이 안 겹치게.
        textures={"material_0": [("Base Color", "baseColorTexture", "material_0_baseColor.jpeg"), ("Normal", "normalTexture", "material_0_normal.jpeg")]},
        level_arms=True,
    ),
    # 서아인(사이코패스) — 비만형 도살자, 피 묻은 앞치마. 원본은 +X를 봐서(강주혁과 같은 이유) Z −90°.
    # 이미 T자라 level_arms 불필요 — 렌더로 확인. 목깃 옆에 작은 칼(고정 소품)이 있는데 재질이
    # 하나뿐이라 rigid로 못 떼어낸다 — 몸통에 붙어 있어 자동 가중치로도 그대로 몸통을 따라간다(두
    # 자세 렌더에서 확인, 팔·다리만 움직이는 판정 자세라 문제 없음).
    "특별함_서아인": dict(
        source="~/Downloads/psychopath_hunt.glb",
        path="Assets/Art/Units/특별함_서아인/특별함_서아인.fbx",
        mesh_name="Psychopath",
        height=1.8,
        rotate_z=-90.0,
        center_band=(0.02, 0.10),
        joints=dict(Hips=(0, 0, 0.50), Spine=(0, 0, 0.56), Spine1=(0, 0, 0.63), Spine2=(0, 0, 0.72), Neck=(0, 0, 0.855),
                    Head=(0, 0, 0.90), HeadTop=(0, 0, 1.0),
                    Shoulder=(0.06, 0, 0.78), Arm=(0.16, 0, 0.775), ForeArm=(0.32, 0, 0.775), Hand=(0.46, 0, 0.775),
                    HandTip=(0.53, 0, 0.775),
                    UpLeg=(0.10, 0, 0.50), Leg=(0.10, 0, 0.27), Foot=(0.10, -0.01, 0.045), ToeBase=(0.10, -0.06, 0.02),
                    ToeTip=(0.10, -0.08, 0.02)),
        rigid={},
        alpha_keep=set(),
    ),
}

# (뼈, 머리 관절, 꼬리 관절, 부모) — 왼쪽/오른쪽은 L·R 두 벌
SPINE = [("Hips", "Hips", "Spine", None), ("Spine", "Spine", "Spine1", "Hips"), ("Spine1", "Spine1", "Spine2", "Spine"),
         ("Spine2", "Spine2", "Neck", "Spine1"), ("Neck", "Neck", "Head", "Spine2"), ("Head", "Head", "HeadTop", "Neck")]
LIMB = [("Shoulder", "Shoulder", "Arm", "Spine2"), ("Arm", "Arm", "ForeArm", "Shoulder"), ("ForeArm", "ForeArm", "Hand", "Arm"),
        ("Hand", "Hand", "HandTip", "ForeArm"),
        ("UpLeg", "UpLeg", "Leg", "Hips"), ("Leg", "Leg", "Foot", "UpLeg"), ("Foot", "Foot", "ToeBase", "Leg"), ("ToeBase", "ToeBase", "ToeTip", "Foot")]


def glb(path):
    b = open(path, "rb").read()
    n = struct.unpack_from("<I", b, 12)[0]
    j = json.loads(b[20:20 + n])
    off = 20 + n                                                        # JSON 청크 길이는 4바이트 채움을 이미 포함
    blen = struct.unpack_from("<I", b, off)[0]
    return j, b[off + 8:off + 8 + blen]


def image_bytes(j, binchunk, index):
    bv = j["bufferViews"][j["images"][index]["bufferView"]]
    s = bv.get("byteOffset", 0)
    return binchunk[s:s + bv["byteLength"]]


def bone_table(joints):
    out = []
    for name, head, tail, parent in SPINE:
        out.append((name, joints[head], joints[tail], parent))
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        for name, head, tail, parent in LIMB:
            h, t = joints[head], joints[tail]
            par = parent if parent in ("Spine2", "Hips") else side + parent
            out.append((side + name, (h[0] * sx, h[1], h[2]), (t[0] * sx, t[1], t[2]), par))
    return out


def build(name, cfg, out_dir=None, render_dir=None):
    src = os.path.expanduser(cfg["source"])
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    scene = bpy.context.scene

    # ── 텍스처: 재질이 쓰는 내장 이미지를 원본 바이트 그대로 Textures/에(파일 이름 = 그 이미지를 쓰는 첫 재질 이름)
    j, binchunk = glb(src)
    os.makedirs(tex_dir, exist_ok=True)
    mat_image, files = {}, {}
    for mt in (j["materials"] if not cfg.get("textures") else []):
        ext = mt.get("extensions", {}).get("KHR_materials_pbrSpecularGlossiness", {})
        tex = ext.get("diffuseTexture") or mt.get("pbrMetallicRoughness", {}).get("baseColorTexture")
        src_index = j["textures"][tex["index"]]["source"]
        if src_index not in files:
            fname = f"{mt['name']}_diffuse.png"
            open(os.path.join(tex_dir, fname), "wb").write(image_bytes(j, binchunk, src_index))
            files[src_index] = fname
        mat_image[mt["name"]] = files[src_index]
    for mt in (j["materials"] if cfg.get("textures") else []):
        entries = []
        for socket, slot, fname in cfg["textures"][mt["name"]]:
            tex = mt.get(slot) or mt.get("pbrMetallicRoughness", {}).get(slot)
            open(os.path.join(tex_dir, fname), "wb").write(image_bytes(j, binchunk, j["textures"][tex["index"]]["source"]))
            entries.append((socket, fname))
        mat_image[mt["name"]] = entries
    report["재질→텍스처"] = mat_image

    meshes = [o for o in scene.objects if o.type == "MESH"]
    Rz = Matrix.Rotation(math.radians(cfg.get("rotate_z", 0.0)), 4, "Z")
    world = {o.name: np.array([Rz @ o.matrix_world @ v.co for v in o.data.vertices]) for o in meshes}
    P = np.concatenate(list(world.values()))
    lo, hi = P.min(0), P.max(0)
    H = float(hi[2] - lo[2])
    body = max(meshes, key=lambda o: len(o.data.vertices))
    B = world[body.name]
    band = B[(B[:, 2] >= lo[2] + H * cfg["center_band"][0]) & (B[:, 2] <= lo[2] + H * cfg["center_band"][1])]
    cx, cy = float(band[:, 0].min() + band[:, 0].max()) / 2, float(band[:, 1].min() + band[:, 1].max()) / 2
    s = float(cfg["height"] / H)
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo[2])) @ Rz
    report["원본 키"] = round(float(H), 4)
    report["배율"] = round(s, 4)

    # ── 메시를 세계로 굽고 하나로
    for o in meshes:
        M = G @ o.matrix_world
        o.parent = None
        o.data.transform(M)
        if M.determinant() < 0:
            o.data.flip_normals()
        o.matrix_basis = Matrix.Identity(4)
    for o in [o for o in scene.objects if o.type != "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in scene.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = body.data.name = cfg["mesh_name"]
    while len(body.data.uv_layers) > cfg.get("uv_layers", 99):
        body.data.uv_layers.remove(body.data.uv_layers[-1])
    mats = sorted(sl.material.name for sl in body.material_slots)
    report["재질"] = mats

    # ── 재질: 알파는 진짜 컷아웃만, 이미지는 Textures 파일로
    for sl in body.material_slots:
        m = sl.material
        if cfg.get("textures"):
            rebuild_material(m, mat_image[m.name], tex_dir)
            continue
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        img_node = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
        for n in [n for n in nt.nodes if n.type == "MATH"]:
            nt.nodes.remove(n)
        img_node.image = bpy.data.images.load(os.path.join(tex_dir, mat_image[m.name]), check_existing=True)
        if not bsdf.inputs["Base Color"].links:
            nt.links.new(img_node.outputs["Color"], bsdf.inputs["Base Color"])
        for l in list(bsdf.inputs["Alpha"].links):
            nt.links.remove(l)
        if m.name in cfg["alpha_keep"]:
            nt.links.new(img_node.outputs["Alpha"], bsdf.inputs["Alpha"])
            m.blend_method = "CLIP" if hasattr(m, "blend_method") else None
        else:
            bsdf.inputs["Alpha"].default_value = 1.0
            if hasattr(m, "blend_method"):
                m.blend_method = "OPAQUE"
            if hasattr(m, "surface_render_method"):
                m.surface_render_method = "DITHERED"
    for im in [im for im in bpy.data.images if im.packed_file and im.users == 0]:
        bpy.data.images.remove(im)

    # ── 뼈대
    Hf = cfg["height"]
    table = bone_table(cfg["joints"])
    data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", data)
    scene.collection.objects.link(arm)
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for bname, h, t, parent in table:
        eb = data.edit_bones.new(PREFIX + bname)
        eb.head = Vector(h) * Hf
        eb.tail = Vector(t) * Hf
        d = (eb.tail - eb.head).normalized()
        eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    for bname, h, t, parent in table:
        if parent:
            data.edit_bones[PREFIX + bname].parent = data.edit_bones[PREFIX + parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    report["뼈"] = len(data.bones)

    # ── 가중치: 이음새를 붙인 사본에서 자동 가중치 → 위치로 되옮김, 딱딱한 조각은 한 뼈 100%
    rigid_slots = {i: PREFIX + cfg["rigid"][sl.material.name] for i, sl in enumerate(body.material_slots) if sl.material.name in cfg["rigid"]}
    rigid_vert = {}
    for p in body.data.polygons:
        if p.material_index in rigid_slots:
            for vi in p.vertices:
                rigid_vert[vi] = rigid_slots[p.material_index]
    tmp = body.copy()
    tmp.data = body.data.copy()
    scene.collection.objects.link(tmp)
    bm = bmesh.new()
    bm.from_mesh(tmp.data)
    rigid_idx = set(rigid_slots)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in rigid_idx], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    welded = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    report["이음새 붙임(정점)"] = welded - len(bm.verts)
    bm.to_mesh(tmp.data)
    bm.free()
    for o in scene.objects:
        o.select_set(o in (tmp, arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    kd = KDTree(len(tmp.data.vertices))
    for v in tmp.data.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    gname = {g.index: g.name for g in tmp.vertex_groups}
    # bone heat가 못 푼 정점(털 가닥·발톱 같은 떨어진 조각) → 가장 가까운 「가중치 있는」 정점의 가중치를 그대로(가까운 뼈로 주면 귀털이 어깨에 붙었다)
    weighted = [v.index for v in tmp.data.vertices if any(g.weight > 1e-4 for g in v.groups)]
    kd_w = KDTree(len(weighted))
    for i in weighted:
        kd_w.insert(tmp.data.vertices[i].co, i)
    kd_w.balance()
    for b in data.bones:
        body.vertex_groups.new(name=b.name)
    unweighted, far, far_fill = 0, 0.0, 0.0
    for v in body.data.vertices:
        if v.index in rigid_vert:
            body.vertex_groups[rigid_vert[v.index]].add([v.index], 1.0, "REPLACE")
            continue
        co, ti, dist = kd.find(v.co)
        far = max(far, dist)
        ws = [(gname[g.group], g.weight) for g in tmp.data.vertices[ti].groups if g.weight > 1e-4]
        if not ws:
            unweighted += 1
            _, wi, wd = kd_w.find(v.co)
            far_fill = max(far_fill, wd)
            ws = [(gname[g.group], g.weight) for g in tmp.data.vertices[wi].groups if g.weight > 1e-4]
        total = sum(w for _, w in ws)
        for gn, w in ws:
            body.vertex_groups[gn].add([v.index], w / total, "REPLACE")
    bpy.data.objects.remove(tmp, do_unlink=True)
    report["자동가중치 실패→가까운 가중치 정점"] = unweighted
    report["채움 최대 거리(m)"] = round(far_fill, 4)
    report["되옮김 최대 거리"] = round(far, 6)
    counts = {b.name[len(PREFIX):]: 0 for b in data.bones}
    for v in body.data.vertices:
        for g in v.groups:
            if g.weight > 0.01:
                counts[body.vertex_groups[g.group].name[len(PREFIX):]] += 1
    report["뼈별 정점(w>0.01)"] = counts
    dead = [k for k, c in counts.items() if c == 0]
    assert not dead, f"{name}: 가중치 없는 뼈 {dead}"
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    body.parent = arm
    if cfg.get("level_arms"):
        report["팔 수평 굽기(°)"] = level_arms(arm, body)

    bpy.context.view_layer.update()
    V = np.array([v.co for v in body.data.vertices])
    report["크기(m)"] = [round(float(c), 3) for c in (V.max(0) - V.min(0))]
    report["최저 z"] = round(float(V[:, 2].min()), 4)

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    if render_dir:
        report["판정"] = judge(name, arm, body, render_dir)
    return report


def rebuild_material(m, entries, tex_dir):
    """재질 노드를 베이스·노멀만 남기고 다시 — 금속 0·거칠기 0.8·발광 끔, 불투명."""
    nt = m.node_tree
    for n in [n for n in nt.nodes if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL")]:
        nt.nodes.remove(n)
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    for l in list(bsdf.inputs["Alpha"].links):
        nt.links.remove(l)
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    bsdf.inputs["Emission Strength"].default_value = 0.0
    bsdf.inputs["Emission Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    for i, (socket, fname) in enumerate(entries):
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(os.path.join(tex_dir, fname), check_existing=True)
        node.location = (-700, 300 - 320 * i)
        if socket == "Normal":
            node.image.colorspace_settings.name = "Non-Color"
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nm.location = (-350, 300 - 320 * i)
            nt.links.new(node.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        else:
            nt.links.new(node.outputs["Color"], bsdf.inputs[socket])
    if hasattr(m, "blend_method"):
        m.blend_method = "OPAQUE"


def level_arms(arm, body):
    """팔(위팔·아래팔·손)을 ±X로 돌려 메시를 굽고 그 자세를 쉬는 자세로 — T자, 팔 수평. 굽기 전후 메시 차이 검사."""
    scene = bpy.context.scene
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    turned = {}
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        for bone in ("Arm", "ForeArm", "Hand"):
            pb = arm.pose.bones[PREFIX + side + bone]
            head, tail = pb.matrix.to_translation(), pb.tail.copy()
            d = (tail - head).normalized()
            turned[side + bone] = round(math.degrees(d.angle(Vector((sx, 0.0, 0.0)))), 1)
            q = d.rotation_difference(Vector((sx, 0.0, 0.0)))
            pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
            bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    want = np.array([v.co for v in baked.vertices])
    old = body.data
    body.data = baked
    baked.name = old.name
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    got = np.array([v.co for v in me.vertices])
    ev.to_mesh_clear()
    drift = float(np.abs(got - want).max())
    assert drift < 1e-4, f"팔 굽기 뒤 메시가 움직였다 {drift}"
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        b = arm.data.bones[PREFIX + side + "Hand"]
        assert (b.tail_local - b.head_local).normalized().dot(Vector((sx, 0, 0))) > 0.999
    return turned


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return (a + ab * t - p).length


def _turn(arm, bone, axis, deg):
    pb = arm.pose.bones[PREFIX + bone]
    head = pb.matrix.to_translation()
    pb.matrix = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()


POSES = {
    "T": [],
    "arms45_knees90": [("LeftArm", "Y", 45), ("RightArm", "Y", -45), ("LeftUpLeg", "X", -45), ("RightUpLeg", "X", -45),
                       ("LeftLeg", "X", 90), ("RightLeg", "X", 90)],
}


def judge(name, arm, body, out):
    """판정 렌더(정사영 앞·옆·무릎/어깨 확대) + 늘어짐 = 자세 모서리 길이 / 쉬는 모서리 길이."""
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 800, 1000
    scene.world = bpy.data.worlds.new("판정")
    scene.world.color = (0.3, 0.3, 0.3)
    cam = bpy.data.objects.new("판정_카메라", bpy.data.cameras.new("판정_카메라"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    rest = np.array([v.co for v in body.data.vertices])
    edges = np.array([e.vertices for e in body.data.edges])
    L0 = np.linalg.norm(rest[edges[:, 0]] - rest[edges[:, 1]], axis=1)
    result = {}
    for pose, turns in POSES.items():
        for pb in arm.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        for bone, axis, deg in turns:
            _turn(arm, bone, axis, deg)
        dg = bpy.context.evaluated_depsgraph_get()
        ev = body.evaluated_get(dg)
        me = ev.to_mesh()
        cur = np.array([v.co for v in me.vertices])
        ev.to_mesh_clear()
        ok = L0 > 1e-5
        ratio = np.where(ok, np.linalg.norm(cur[edges[:, 0]] - cur[edges[:, 1]], axis=1) / np.maximum(L0, 1e-9), 1.0)
        top = np.argsort(-ratio)[:5]
        result[pose] = dict(최대늘어짐=round(float(ratio.max()), 2), 상위1퍼센트=round(float(np.percentile(ratio, 99)), 2),
                            최소=round(float(ratio[ok].min()), 2),
                            최대자리=[(round(float(ratio[i]), 2), [round(float(c), 2) for c in rest[edges[i, 0]]]) for i in top],
                            최저z=round(float(cur[:, 2].min()), 3))
        views = [("front", (0, -5, 0.9), (math.radians(90), 0, 0), 2.0), ("side", (5, 0, 0.9), (math.radians(90), 0, math.radians(90)), 2.0)]
        if turns:
            views += [("knee", (3, -3, 0.45), (math.radians(90), 0, math.radians(45)), 0.9), ("shoulder", (0, -5, 1.2), (math.radians(90), 0, 0), 1.0)]
        for tag, loc, rot, scale in views:
            cam.location, cam.rotation_euler, cam.data.ortho_scale = loc, rot, scale
            scene.render.filepath = os.path.join(out, f"{name}_{pose}_{tag}.png")
            bpy.ops.render.render(write_still=True)
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    return result


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
        r = build(n, SKINS[n], out_dir, render_dir)
        print("리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
