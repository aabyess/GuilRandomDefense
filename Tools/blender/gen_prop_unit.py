"""뼈 없는 소품 유닛(자전거 「안흔함_상붕카」와 같은 부류) — Sketchfab glb를 그대로 하나의 FBX로.
구현담당2 전담, 2026-09-15 첫 건 특별함_김정래(노트북).
  blender -b --factory-startup --python Tools/blender/gen_prop_unit.py -- [이름 ...] [--out DIR] [--render DIR]

원본이 이미 겉싸개 노드에 배율·축 회전을 층층이 먹인 채로 온다(Sketchfab_model 축 회전 ·
.fbx 노드 축 회전 · 부품 노드 ×100 등 — PM 조사). 이걸 굳이 풀어 해석하지 않고, gen_skin_rig.py·
fix_unit_fbx.py와 같은 방식으로 각 메시 오브젝트의 matrix_world를 메시 데이터에 그대로 구워
(mesh.transform) 오브젝트 행렬을 항등으로 만든 뒤, 그 결과(이미 올바른 비율의 실측 크기)를
기준으로 우리 규약(정면 −Y·바닥 z 0·가장 긴 변 0.6m)에 맞춰 다시 중심 잡고 스케일한다 — 겉싸개
행렬이 몇 겹이든, 몇 배씩 곱혀 있든 신경 쓸 필요가 없다.

■ 특별함_김정래(노트북) 확인한 것
겉싸개를 다 구운 뒤 원본 좌표(임포트 직후, 아직 우리 배율 적용 전) 그대로 앞(−Y)에서 보면 이미
화면을 정면으로 보고, 위에서 보면 키보드가 −Y(가까운) 쪽에 있다 — rotate_z 불필요(스크래치 렌더
raw_negY.png·raw_top.png로 확인, 이 파일엔 없음). 화면은 경첩이 열린 채로(Screen 노드 행렬 자체가
비스듬히 기운 값) 들어온다 — PM 지시대로 그 모양 그대로 둔다(따로 접거나 세우지 않음).
재질 텍스처: ComputerFrame(이미지0=베이스·이미지1=발광) · ComputerScreen(이미지2=베이스·
이미지3=금속성·거칠기 맵·이미지4=발광). 이미지3(금속성·거칠기)은 우리 재질이 베이스+발광만
쓰므로 파일로 안 뺀다(SOURCE.txt에 기록).
"""
import hashlib
import json
import math
import os
import struct
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))

UNITS = {
    "특별함_김정래": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/03_특별함/특별함_김정래.glb"),
        path="Assets/Art/Units/특별함_김정래/특별함_김정래.fbx",
        length=0.6, rotate_z=0.0,
        # 메시 이름별 재질 → (베이스 이미지 번호, 발광 이미지 번호 또는 None)
        materials={"ComputerFrame": (0, 1), "ComputerScreen": (2, 4)},
        unused_images={3: "ComputerScreen 재질의 metallicRoughness 맵(금속성·거칠기) — 베이스+발광만 쓰므로 안 뺌"},
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


def set_material(mat, base_path, emissive_path):
    """베이스+발광만 쓰는 단순 재질로 다시 짠다(gen_skin_rig.py의 rebuild_material과 달리 발광
    세기를 실제로 올린다 — 그쪽은 Normal 소켓용이라 Emission Strength를 0으로 고정해 둔다)."""
    nt = mat.node_tree
    for n in [n for n in nt.nodes if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL")]:
        nt.nodes.remove(n)
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    for l in list(bsdf.inputs["Alpha"].links):
        nt.links.remove(l)
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    base_node = nt.nodes.new("ShaderNodeTexImage")
    base_node.image = bpy.data.images.load(base_path, check_existing=True)
    base_node.location = (-700, 300)
    nt.links.new(base_node.outputs["Color"], bsdf.inputs["Base Color"])
    if emissive_path:
        em_node = nt.nodes.new("ShaderNodeTexImage")
        em_node.image = bpy.data.images.load(emissive_path, check_existing=True)
        em_node.location = (-700, -50)
        nt.links.new(em_node.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0
    else:
        bsdf.inputs["Emission Strength"].default_value = 0.0
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"


def build(name, cfg, out_dir=None, render_dir=None):
    src = cfg["source"]
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src, "sha256": hashlib.sha256(open(src, "rb").read()).hexdigest()}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    report["원본 메시"] = [(o.name, len(o.data.vertices)) for o in meshes]

    # ── 텍스처: 내장 이미지 원본 바이트 그대로(재인코딩 금지), 재질 이름 기준 파일명.
    j, binchunk = glb(src)
    os.makedirs(tex_dir, exist_ok=True)
    tex_paths = {}
    for mat_name, (base_idx, em_idx) in cfg["materials"].items():
        base_path = os.path.join(tex_dir, f"{mat_name}_baseColor.png")
        open(base_path, "wb").write(image_bytes(j, binchunk, base_idx))
        em_path = None
        if em_idx is not None:
            em_path = os.path.join(tex_dir, f"{mat_name}_emissive.png")
            open(em_path, "wb").write(image_bytes(j, binchunk, em_idx))
        tex_paths[mat_name] = (base_path, em_path)
    report["안 쓴 이미지"] = cfg.get("unused_images", {})

    # ── 겉싸개 행렬을 전부 메시 데이터로 굽는다(오브젝트 행렬 항등) — Sketchfab_model 축 회전·
    # .fbx 노드 축 회전·부품 ×100이 몇 겹이든 matrix_world 하나로 이미 다 곱해져 있다.
    Rz = math.radians(cfg.get("rotate_z", 0.0))
    for o in meshes:
        M = o.matrix_world.copy()
        o.parent = None                    # 🔴 이걸 안 하면 부모(Frame·Screen 겉싸개 노드)의 ×100·회전이
        o.data.transform(M)                # 남아 있어 데이터에 구운 뒤에도 내보낼 때 한 번 더 곱혀(실측:
        if M.to_3x3().determinant() < 0:   # 재수입 크기가 60m로 100배 폭주, 원점거리 0.976 — Frame 노드
            o.data.flip_normals()          # 자체 이동값과 정확히 일치) 이중 변환이 됐다.
        o.matrix_basis = Matrix.Identity(4)

    frame = max(meshes, key=lambda o: len(o.data.vertices))  # 프레임(바닥판) = 정점 더 많은 쪽(실측: 8,164 vs 158)
    fpts = np.array([v.co for v in frame.data.vertices])
    flo, fhi = fpts.min(0), fpts.max(0)
    allpts = np.concatenate([np.array([v.co for v in o.data.vertices]) for o in meshes])
    lo, hi = allpts.min(0), allpts.max(0)
    longest = float((hi - lo).max())
    s = cfg["length"] / longest
    cx, cy = float((flo[0] + fhi[0]) / 2), float((flo[1] + fhi[1]) / 2)  # 원점 = 프레임(바닥 접지면) 가운데
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -float(lo[2]))) @ Matrix.Rotation(Rz, 4, "Z")
    report["원본 크기(임포트 단위)"] = [round(float(c), 3) for c in (hi - lo)]
    report["배율"] = round(s, 6)
    for o in meshes:
        o.data.transform(G)

    for o in scene.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = obj.data.name = name.split("_")[-1] if "_" in name else name

    for sl in obj.material_slots:
        base_path, em_path = tex_paths[sl.material.name]
        set_material(sl.material, base_path, em_path)

    V = np.array([v.co for v in obj.data.vertices])
    report["크기(m)"] = [round(float(c), 4) for c in (V.max(0) - V.min(0))]
    report["최저 z"] = round(float(V[:, 2].min()), 5)
    report["삼각형"] = sum(len(p.vertices) - 2 for p in obj.data.polygons)

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    # 🔴 PM 실측(유니티) — 뼈 없는 단일 메시라 축 변환(블렌더 Z위→Y위)이 메시 노드 자체의
    # Lcl Rotation(−90,0,0)으로 남는다. 뼈 있는 유닛은 이 회전이 아마추어(자식) 노드에 걸려서
    # 안 드러났는데, 노트북은 노드가 메시 하나뿐이라 ArtBinder가 모델 루트 회전을 항등으로
    # 대입하면서 그 −90°가 지워져 옆으로 눕는다. bake_space_transform=True로 축 변환을 정점에
    # 구워 노드 회전을 0으로 만든다(나머지 인자는 그대로).
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    if render_dir:
        report["렌더"] = judge(name, obj, render_dir)
        report["재수입 검증"] = reimport_check(dst)
    return report


def judge(name, obj, out):
    """판정 렌더 — 앞(−Y)·옆(−X)·위, 재질 색(텍스처) 입혀서."""
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 800, 800
    scene.world = bpy.data.worlds.new("판정")
    scene.world.color = (0.5, 0.5, 0.5)
    cam = bpy.data.objects.new("판정_카메라", bpy.data.cameras.new("판정_카메라"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    # 🔴 손으로 짠 오일러(옆·위 방향)는 부호를 잘못 짜면 빈 렌더가 나오기 쉽다(사람형 스캔에서
    # 실측으로 겪음) — TRACK_TO 제약으로 물체 중심을 직접 겨눠 좌표계 실수를 없앤다.
    target = bpy.data.objects.new("판정_과녁", None)
    scene.collection.objects.link(target)
    con = cam.constraints.new("TRACK_TO")
    con.target = target
    con.track_axis = "TRACK_NEGATIVE_Z"
    con.up_axis = "UP_Y"
    V = np.array([v.co for v in obj.data.vertices])
    lo, hi = V.min(0), V.max(0)
    center = (lo + hi) / 2
    size = float((hi - lo).max())
    cam.data.ortho_scale = size * 1.6
    cam.data.clip_end = size * 20
    target.location = Vector(center)
    dist = size * 8
    views = [
        ("front", (center[0], center[1] - dist, center[2])),
        ("side", (center[0] - dist, center[1], center[2])),
        ("top", (center[0], center[1], center[2] + dist)),
    ]
    paths = []
    for tag, loc in views:
        cam.location = loc
        p = os.path.join(out, f"{name}_{tag}.png")
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    return paths


def reimport_check(path):
    """내보낸 FBX를 다시 읽어 크기·정면·바닥을 실측(PM 요청)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    pts = np.concatenate([np.array([o.matrix_world @ v.co for v in o.data.vertices]) for o in meshes])
    lo, hi = pts.min(0), pts.max(0)
    origin_dist = max(o.matrix_world.translation.length for o in meshes)
    return {"메시": len(meshes), "가로": round(float(hi[0] - lo[0]), 4), "세로(높이)": round(float(hi[2] - lo[2]), 4),
            "앞뒤": round(float(hi[1] - lo[1]), 4), "최저 z": round(float(lo[2]), 5), "오브젝트 원점거리": round(float(origin_dist), 5),
            "Model Lcl Rotation": model_lcl_rotation(path)}


def model_lcl_rotation(path):
    """FBX를 원시로 파싱해 Model 노드의 Lcl Rotation을 읽는다(PM 지시 — 유니티가 노드 하나짜리
    FBX의 모델 루트 회전을 항등으로 대입해 버려서, 블렌더 Z위→Y위 축 변환이 정점이 아니라 이
    회전값으로만 남아 있으면 뼈 없는 단일 메시가 유니티에서 눕는다. bake_space_transform=True로
    이 값이 (0,0,0)이 되게 굽는다 — export_scene.fbx 호출 참고)."""
    import io_scene_fbx.parse_fbx as pf
    root, _version = pf.parse(path)

    def find(elem, elem_id):
        return [e for e in elem.elems if e.id == elem_id]

    objects = find(root, b"Objects")[0]
    rotations = []
    for model in find(objects, b"Model"):
        rot = (0.0, 0.0, 0.0)
        for p70 in find(model, b"Properties70"):
            for prop in find(p70, b"P"):
                if prop.props[0] == b"Lcl Rotation":
                    rot = tuple(prop.props[4:])
        rotations.append(rot)
    return rotations


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
    for n in names or list(UNITS):
        r = build(n, UNITS[n], out_dir, render_dir)
        print("소품유닛  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
