"""정의문 ← 에니에스로비(Sketchfab Cyrone™, CC-BY-4.0) 문짝. blender 세션, 2026-10-02 PM 지시. 기존 Walls/정의문.fbx 교체용(구조 계약은 gen_justice_gate.py와 같다).
    blender -b --factory-startup --python Tools/blender/gen_justice_gate_enies.py -- [--out DIR] [--tris N]
원본: ~/Desktop/구랜디스킨모음/93_구조물/정의문_에니에스로비.glb 의 메시 5_doors_1_0_0_0 (한 메시, 두 짝이 ∓30°로 벌어져 있다 — 정점 6,747+6,750).
  · 두 짝을 **닫은 자세**로 편다(각 짝을 가운데 기준으로 ±30° 돌려 X축에 맞추고 가운데에서 맞닿게). 열린 채로 쓰면 코드의 부서짐 연출과 폭이 안 맞는다.
  · 구조: 빈 부모 `정의문` + 자식 `정의문_왼짝`·`정의문_오른짝`, 짝의 원점 = 바깥 경첩 쪽 발밑(x = ∓HALF_W), 앞(장식 면) −Y.
  · 재질·텍스처 1장(원본 Image_11, 1024) → `정의문_에니에스`. 하늘·바다·땅·집·배는 쓰지 않는다.
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

SRC = os.path.expanduser("~/Desktop/구랜디스킨모음/93_구조물/정의문_에니에스로비.glb")
UNITS = 11.4
HALF_W = 10.3                      # 게임 단위 — 기존 정의문과 같은 가로(20.6)
GAP = 0.06
MAT = "정의문_에니에스"


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.expanduser("~/GRD_motion_trial/정의문_에니에스로비")
    tri_limit = 10000
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--tris":
            tri_limit = int(next(it))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC)
    src = bpy.data.objects["5_doors_1_0_0_0"]
    mat = src.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE")
    alpha_linked = any(l.to_socket.name == "Alpha" for l in mat.node_tree.links)
    px = np.array(img.pixels[:]).reshape(-1, 4)
    print("알파 연결", alpha_linked, "이미지 알파 최소", float(px[:, 3].min()), "크기", img.size[:])
    # 세계 좌표로 굽기
    src.data.transform(src.matrix_world)
    src.parent = None
    src.matrix_world = Matrix.Identity(4)
    V = np.array([v.co[:] for v in src.data.vertices])
    xmid = (V[:, 0].min() + V[:, 0].max()) / 2
    lo_z = V[:, 2].min()
    leaves = {}
    for name, side, rot in (("정의문_왼짝", -1, math.radians(30)), ("정의문_오른짝", 1, math.radians(-30))):
        bm = bmesh.new()
        bm.from_mesh(src.data)
        kill = [f for f in bm.faces if (sum((v.co.x for v in f.verts)) / len(f.verts) < xmid) != (side < 0)]
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
        P = np.array([v.co[:] for v in bm.verts])
        c = np.array([(P[:, 0].min() + P[:, 0].max()) / 2, (P[:, 1].min() + P[:, 1].max()) / 2, 0.0])
        bmesh.ops.translate(bm, vec=Vector(-c), verts=bm.verts)
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(rot, 3, "Z"), verts=bm.verts)
        P = np.array([v.co[:] for v in bm.verts])
        length = P[:, 0].max() - P[:, 0].min()
        thick = P[:, 1].max() - P[:, 1].min()
        print(side, "닫은 뒤 길이", round(length, 1), "두께", round(thick, 1), "z", round(P[:, 2].min(), 1), round(P[:, 2].max(), 1))
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        leaves[name] = (me, side, length, thick)
    length = leaves["정의문_왼짝"][2]
    s = (HALF_W - GAP) / length                # 게임 단위 / 원본 단위 — 가로 20.6 − 틈
    tall = (V[:, 2].max() - lo_z) * s
    thick = max(l[3] for l in leaves.values()) * s
    print(f"배율 {s:.5f} 가로 {2 * (HALF_W):.2f} 높이 {tall:.2f} 두께 {thick:.2f} (게임 단위) → 가로:높이:두께 = 1 : {tall / (2 * HALF_W):.3f} : {thick / (2 * HALF_W):.3f}")
    col = bpy.context.scene.collection
    parent = bpy.data.objects.new("정의문", None)
    col.objects.link(parent)
    objs = []
    for name, (me, side, length_, thick_) in leaves.items():
        # 원본 단위 → 게임 단위(s) → 미터(1/11.4). z 최저 0. 가운데 정렬 뒤 경첩(바깥 끝)이 원점이 되게 옮긴다.
        me.transform(Matrix.Scale(s, 4))
        P = np.array([v.co[:] for v in me.vertices])
        me.transform(Matrix.Translation((side * (GAP + length * s / 2), 0, -(lo_z * s))))   # 닫은 자리(가운데 쪽 끝이 ±GAP)
        hinge = Vector((side * HALF_W, 0, 0))
        me.transform(Matrix.Translation(-hinge))
        me.transform(Matrix.Scale(1 / UNITS, 4))
        me.materials.append(mat)
        o = bpy.data.objects.new(name, me)
        col.objects.link(o)
        o.parent = parent
        o.location = hinge / UNITS
        objs.append(o)
    bpy.data.objects.remove(src, do_unlink=True)
    # 감량(한도 초과 시)
    total = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    ratio = min(1.0, tri_limit / total)
    if ratio < 1.0:
        for o in objs:
            m = o.modifiers.new("d", "DECIMATE")
            m.ratio = ratio
            m.use_collapse_triangulate = True
            dg = bpy.context.evaluated_depsgraph_get()
            nm = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
            o.modifiers.remove(m)
            o.data = nm
        total = sum(len(p.vertices) - 2 for o in objs for p in o.data.polygons)
    print("삼각형", total)
    # 재질·텍스처
    tdir = os.path.join(out, "Textures")
    os.makedirs(tdir, exist_ok=True)
    im = bpy.data.images.new(MAT, img.size[0], img.size[1], alpha=False)
    im.pixels = np.array(img.pixels[:], dtype=np.float32)
    if max(img.size) > 1024:
        im.scale(1024, 1024)
    im.filepath_raw = os.path.join(tdir, MAT + ".png")
    im.file_format = "PNG"
    im.save()
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = im
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], o_.inputs["Surface"])
    b.inputs["Metallic"].default_value = 0.0
    b.inputs["Roughness"].default_value = 0.7
    mat.name = MAT
    mat.blend_method = "OPAQUE"
    # 내보내기
    for ob in bpy.data.objects:
        ob.select_set(ob in (parent, *objs))
    bpy.context.view_layer.objects.active = parent
    dst = os.path.join(out, "정의문.fbx")
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={"MESH", "EMPTY"}, global_scale=UNITS,
                             path_mode="RELATIVE", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
    print("출력", dst)


if __name__ == "__main__":
    main()
