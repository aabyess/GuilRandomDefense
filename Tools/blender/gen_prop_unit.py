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
    # 나루토 이치라쿠 라멘(一楽) 포장마차 → 히든_호치킨(2026-09-22 히든, blender 세션). Sketchfab glb 한 덩이(뼈·애니 0).
    #   메시 37 · 재질 4 · 이미지 12(재질마다 베이스·metallicRoughness·노멀 3장씩) · 삼각형 10,137 — **감량 불필요**(가볍다).
    #   원본 축 Z 위 · 가게 정면(ラーメン 一楽 간판·의자)이 −Y를 본다 → rotate_z 불필요(raw_negY.png로 확인).
    #   🔴 Ground_low(돌 받침 슬래브, z −177~8 · 가로세로 1,262)는 지시대로 뺀다 — 맵 바닥 위에 또 깔리면 겹친다. 빼면 바닥이 건물 밑면(z −23.8)이 된다.
    #   🔴 베이스 텍스처가 **jpeg**다(메탈릭·노멀은 png) — 확장자를 mimeType대로 .jpg로 뺀다.
    "히든_호치킨": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_호치킨.glb"),
        path="Assets/Art/Units/히든_호치킨/히든_호치킨.fbx",
        length=0.6, rotate_z=0.0, center="all",
        drop_meshes=["Ground_low.000_Ground_0"],
        materials={"Detail_Objects": (0, None), "Building_Bottom": (6, None), "Building_Top": (9, None)},
        unused_images={1: "Detail_Objects metallicRoughness", 2: "Detail_Objects 노멀",
                       3: "Ground 베이스(메시째 뺐다)", 4: "Ground metallicRoughness", 5: "Ground 노멀",
                       7: "Building_Bottom metallicRoughness", 8: "Building_Bottom 노멀",
                       10: "Building_Top metallicRoughness", 11: "Building_Top 노멀"},
    ),
    # 원피스 흑수염 해적단 뗏목 → 히든_이삭토스트(2026-09-22 히든, blender 세션). zip 안 **rar** 안 FBX(뼈·애니 0 · 메시 1(8,349정점) · 재질 1).
    #   원본 크기(가져오기 단위) 0.366 × 0.334 × 0.400 — 통나무 넷을 Y축으로 나란히 묶은 뗏목 + 돛대 하나(흑수염 3해골 졸리로저 검은 돛 3단 + 뒤쪽 황토색 돛).
    #   🔴 뱃머리는 이미 −Y다(rotate_z 불필요) — 졸리로저 돛의 그림 면과 통나무 네 개의 자른 단면이 둘 다 −Y를 본다(raw_negY.png · q_front_left.png로 직접 확인).
    #   🔴 사람은 안 타 있다(PM이 확인하라고 한 것 — 3/4·정면·후면·위 렌더 넷 다 확인, 배 위에 인물 메시 없음).
    #   바닥은 통나무 밑면(원본 z −0.022)이 z 0에 놓인다(별도 받침 없음).
    "히든_이삭토스트": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_이삭토스트.zip"),
        member=["source/Blackbeard Pirates Raft.rar", "Blackbeard Pirates Raft/Blackbeard Pirates Raft by Annettlw.fbx"],
        path="Assets/Art/Units/히든_이삭토스트/히든_이삭토스트.fbx",
        length=0.6, rotate_z=0.0, center="all",
        materials={"heihuzichuan_obj201": ("heihuzichuan_obj201.png", None)},
    ),
    # 원피스 방주 맥심(에넬의 방주, 스카이피아) → 히든_감탄떡볶이(2026-09-22 히든, blender 세션). 역시 zip 안 **rar** 안 FBX(뼈·애니 0 · 메시 2 · 재질 2).
    #   원본 크기 1.090 × 1.302 × 0.913 — 금빛 신(神) 얼굴 뱃머리 · 옆구리 프로펠러 넷 · 노 여러 자루.
    #   🔴 뱃머리(신 얼굴)가 이미 −Y다(rotate_z 불필요, raw_negY.png로 직접 확인).
    #   🔴 에넬은 안 타 있다(PM이 확인하라고 한 것 — 정면·3/4 렌더로 확인, 갑판에 인물 메시 없음. 앞 갑판의 하늘색 구 두 개는 다이얼 장식).
    #   🔴 텍스처 두 장은 재질 이름과 **글자 그대로 같다**(kongdao_obj_409a/b) — 이름 짐작이 아니라 원본 FBX의 재질 이름 자체가 파일 이름이다(직접 확인).
    "히든_감탄떡볶이": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_감탄떡볶이.zip"),
        member=["source/Ark Maxim.rar", "Ark Maxim/Ark Maxim by Annettlw.fbx"],
        path="Assets/Art/Units/히든_감탄떡볶이/히든_감탄떡볶이.fbx",
        length=0.6, rotate_z=0.0, center="all",
        materials={"kongdao_obj_409a": ("kongdao_obj_409a.png", None),
                   "kongdao_obj_409b": ("kongdao_obj_409b.png", None)},
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


def extract(src, cfg, report):
    """🔸 2026-09-23 blender: 압축 원본 지원. cfg["member"]가 없으면 src를 그대로 쓴다(노트북·이치라쿠처럼 glb 한 덩이).
    member는 압축 안 경로를 바깥→안 순서로 적는다 — 마지막이 읽을 모델, 앞의 것들은 그 안의 또 다른 압축이다
    (흑수염 뗏목·방주 맥심: zip 안 rar). ⚠️ .rar는 파이썬 표준 라이브러리로 못 푼다 — 시스템 bsdtar를 쓴다
    (macOS 기본 탑재, rar 읽기 됨. 실측으로 확인). 사장님 원본은 건드리지 않고 임시 폴더에 푼다."""
    if not cfg.get("member"):
        return src, os.path.dirname(src)
    import subprocess
    import tempfile
    import zipfile
    tmp_dir = tempfile.mkdtemp(prefix="propunit_")
    with zipfile.ZipFile(src) as z:
        z.extractall(tmp_dir)
    members = [cfg["member"]] if isinstance(cfg["member"], str) else list(cfg["member"])
    for inner in members[:-1]:
        path = os.path.join(tmp_dir, inner)
        if path.lower().endswith(".rar"):
            subprocess.run(["bsdtar", "-xf", path, "-C", tmp_dir], check=True, capture_output=True)
        else:
            with zipfile.ZipFile(path) as z:
                z.extractall(tmp_dir)
    report["압축 안"] = cfg["member"]
    return os.path.join(tmp_dir, members[-1]), os.path.join(tmp_dir, cfg.get("textures_dir", "textures"))


def build(name, cfg, out_dir=None, render_dir=None):
    src = cfg["source"]
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src, "sha256": hashlib.sha256(open(src, "rb").read()).hexdigest()}

    bpy.ops.wm.read_factory_settings(use_empty=True)
    src_file, tex_src_dir = extract(src, cfg, report)          # 🔸 2026-09-23 blender: 압축(zip 안 rar) + FBX 입력 지원 — 아래 extract() 참고
    if src_file.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=src_file)
    else:
        bpy.ops.import_scene.fbx(filepath=src_file, use_anim=False)
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    report["원본 메시"] = [(o.name, len(o.data.vertices)) for o in meshes]
    if cfg.get("drop_meshes"):                                  # 🔸 이치라쿠: 받침 바닥판(Ground_low)은 맵 위에 또 깔리므로 뺀다
        drop = set(cfg["drop_meshes"])
        assert drop <= {o.name for o in meshes}, f"{name}: drop_meshes에 없는 메시 {drop - {o.name for o in meshes}}"
        gone = [o for o in meshes if o.name in drop]
        meshes = [o for o in meshes if o.name not in drop]
        for o in gone:
            bpy.data.objects.remove(o, do_unlink=True)
        report["뺀 메시"] = sorted(drop)

    # ── 텍스처: 원본 바이트 그대로(재인코딩 금지), 재질 이름 기준 파일명.
    #   값이 정수면 glb 내장 이미지 번호, 문자열이면 압축 안 textures/의 파일 이름(FBX 원본은 텍스처가 바깥에 있다).
    os.makedirs(tex_dir, exist_ok=True)
    j, binchunk = glb(src_file) if src_file.lower().endswith(".glb") else (None, None)
    tex_paths = {}
    for mat_name, (base_src, em_src) in cfg["materials"].items():
        out_names = cfg.get("texture_names", {}).get(mat_name)
        paths = []
        for kind, ref in (("baseColor", base_src), ("emissive", em_src)):
            if ref is None:
                paths.append(None)
                continue
            if isinstance(ref, int):
                # 🔴 확장자는 glb가 말하는 mimeType을 따른다 — 이치라쿠는 베이스가 **jpeg**인데 .png로 적으면 유니티가 확장자로 형식을 고르다 깨진다
                ext = {"image/jpeg": ".jpg"}.get(j["images"][ref].get("mimeType"), ".png")
                p = os.path.join(tex_dir, f"{mat_name}_{kind}{ext}")
                open(p, "wb").write(image_bytes(j, binchunk, ref))
            else:                                               # 바깥 텍스처 파일 — 이름 그대로 복사(유니티 .meta 유지)
                p = os.path.join(tex_dir, (out_names or {}).get(kind, os.path.basename(ref)))
                open(p, "wb").write(open(os.path.join(tex_src_dir, ref), "rb").read())
            paths.append(p)
        tex_paths[mat_name] = tuple(paths)
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

    allpts = np.concatenate([np.array([v.co for v in o.data.vertices]) for o in meshes])
    lo, hi = allpts.min(0), allpts.max(0)
    if cfg.get("center") == "all":                           # 🔸 건물·뗏목처럼 「프레임 한 조각」이 없는 소품 — 전체 bbox 가운데를 원점으로
        flo, fhi = lo, hi
    else:
        frame = max(meshes, key=lambda o: len(o.data.vertices))  # 프레임(바닥판) = 정점 더 많은 쪽(실측: 8,164 vs 158)
        fpts = np.array([v.co for v in frame.data.vertices])
        flo, fhi = fpts.min(0), fpts.max(0)
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
