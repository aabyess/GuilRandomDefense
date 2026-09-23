"""만든 FBX를 다시 읽어 게임 단위 크기·삼각형 수·재질을 찍는다.

화면 없이 돈다(경로를 안 주면 Assets/Art/Nature 전체):
    blender --background --factory-startup --python Tools/blender/check_nature_fbx.py -- [폴더나 파일 ...]

왜 다시 읽는가 — 스크립트 안의 치수는 "만들려던 크기"일 뿐이다. 내보내기 배수·단위 변환이
한 번 어긋나면 파일 속 크기는 100배로도 틀어진다. 실제 파일을 읽어 봐야 유니티가 받는 크기다.

⚠️ 축: Blender는 Z가 위, 유니티는 Y가 위다. 임포터가 이미 바꿔 주므로 여기서는
가로 = X, 앞뒤 = Y, 세로(높이) = Z로 읽는다.

⚠️ 원점 검사: 최저점이 0이어도 오브젝트 위치가 0이 아니면 "원점은 발밑"이 아니다 —
맵에 놓을 때 위치를 덮어쓰면 그 차이만큼 뜨거나 묻힌다. 그래서 둘 다 찍는다.

⚠️ 위에서 본 뒷면 검사(2026-09-12 추가, PM 상설 지시): 유니티는 뒷면을 안 그린다(`_잎카드` 재질만 양면). 게임
카메라는 위에서 내려다보므로, 위에서 수직으로 쏜 광선이 처음 맞는 면이 「아래를 보는 면」이면 그 자리는 게임에서
속이 뚫려 보이거나 부재가 사라져 보인다(스토리 04·05·06 옥상 면 누락, 08·12 차양 윗면 누락을 이 검사로 찾았다).
바닥을 24×24 격자로 훑어 적중 수와 위치를 찍고, 적중 면 넓이가 바닥 넓이의 1% 이상이면 경고(문제로 셈),
미만이면 작은 부재 밑면일 수 있어 참고로만 표시한다. `_잎카드` 면은 양면이라 셈하지 않는다.

⚠️ 게임 카메라 각도 뒷면 검사(2026-09-12 추가, PM 상설 지시): 옆·비스듬히 보이는 뒷면(보물상자의 두께 없는
한 겹 뚜껑 안쪽 등)은 위에서 본 검사가 못 잡는다. 내려다보는 각 50°·방위 넷(앞·뒤·좌·우)에서 쏜 광선이 처음
맞는 면이 광선을 등지면 기록한다 — 기준은 위와 같다(1% 이상 경고, `_잎카드` 제외). 뼈대가 붙은 모델은 이름이
…Open으로 끝나는 액션의 마지막 프레임 자세에서도 한 번 더 돈다.

⚠️ UV 짓눌림·안 구운 텍셀 검사(2026-09-12 추가, PM 상설 지시): 암벽조각_02 검은 쐐기 — 넓은 면의 UV가 가장자리
한 점으로 짓눌려 안 구워진 검은 여백 텍셀을 집었다. 뒷면 검사로는 안 잡힌다.
  · UV 짓눌림: 면마다 (UV 넓이 ÷ 3D 넓이)를 재서 그 메시 중앙값의 1/50 미만인 면이 3D 넓이로 1% 이상이면 경고.
  · 안 구운 텍셀: 재질 이름으로 찾은 텍스처(`Textures/<재질>.png`, 유니티와 같은 규칙)에서 면의 UV 무게중심 밝기가
    0.01 미만이면 경고. UV가 [0,1] 밖인 면도 경고하되, 루프 UV의 90% 이상이 [0,1] 안인 "펼친(구운) 재질"에만
    적용한다 — 건물 벽처럼 타일로 되풀이하는 재질은 원래 밖으로 나간다. `_잎카드`와 원래 검은 재질(이름에
    검정·그을·쇠·Black·Dark·Soot·Iron)은 뺀다.
  · 두 검사 모두 게임 카메라(내려다보는 50°, 어느 방위든)가 앞면으로 볼 수 없는 면 — 땅을 향한 밑면 — 은 뺀다
    (`_잎카드`는 양면이라 뺀다 대상 아님). 1차로 다 셌더니 바위 20종 전부가 땅에 닿는 밑면 짓눌림으로 걸려
    정작 보이는 결함(암벽조각_02 쐐기)이 묻혔다. 넓이 비율도 볼 수 있는 면 넓이 기준.
  · 폴더 표의 「텍스처 못 찾음」은 이름 규칙으로 PNG를 못 찾아 텍셀 검사를 건너뛴 재질이 있는 파일 수다.

⚠️ HEAD 대비 외형 비교(2026-09-12 추가, PM 상설 지시 — `--head`): 커밋된 FBX를 고쳐 다시 내보낼 때 `git show HEAD:<경로>`의
옛 FBX와 새 FBX에 위(수직)·카메라각 50° 네 방위에서 같은 격자 광선을 쏴, 처음 맞는 재질 이름이 바뀐 광선 비율을 방위별로
센다. 5% 이상이면 「외형 변화」 — 바뀐 재질 쌍을 찍는다. 치수(가로·세로·앞뒤·최저점·원점·삼각형)도 옛→새로 찍는다.
Story10 코니스 띠가 꽉 찬 상자가 되어 옥상을 흰 뚜껑으로 덮은 퇴행(뒷면 검사 0건)이 이 검사를 만든 계기다.
의도한 수정이면 커밋 메시지에 그 사실을 적고 넘긴다(「문제」 수에는 안 넣는다).
"""

import bpy
import bmesh
import glob
import math
import os
import subprocess
import sys
import tempfile
from collections import Counter
from mathutils import Vector
from mathutils.bvhtree import BVHTree

import numpy as np

GRID = 24                  # 뒷면 검사 격자(한 변)
BACKFACE_WARN_RATIO = 0.01  # 적중 면 넓이 ÷ 바닥 넓이가 이 이상이면 경고
CAMERA_ELEVATION = 50.0     # 게임 카메라가 내려다보는 각(도)
UV_SQUASH_FACTOR = 50.0     # (UV 넓이 ÷ 3D 넓이)가 중앙값의 이 분의 1 미만이면 짓눌린 면
UV_SQUASH_WARN_RATIO = 0.01  # 짓눌린 면 넓이 ÷ 전체 넓이가 이 이상이면 경고
BLACK_TEXEL = 0.01          # UV 무게중심 밝기가 이 미만이면 안 구운 텍셀
ATLAS_INSIDE_RATIO = 0.9    # 루프 UV가 이 비율 이상 [0,1] 안이면 펼친(구운) 재질로 보고 [0,1] 밖을 경고
DARK_MATERIAL_HINTS = ("검정", "그을", "쇠", "Black", "Dark", "Soot", "Iron")
HEAD_CHANGE_WARN = 0.05     # HEAD 대비 처음 맞는 재질이 바뀐 광선 비율이 이 이상이면 외형 변화
HEAD_COMPARE = False        # --head
# 방위 — 카메라가 있는 쪽 : 광선의 수평 방향. 앞(−Y)에 선 카메라는 +Y 쪽을 본다.
AZIMUTHS = {"앞(−Y)에서": (0.0, 1.0), "뒤(+Y)에서": (0.0, -1.0), "왼(−X)에서": (1.0, 0.0), "오른(+X)에서": (-1.0, 0.0)}

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Nature")
TRIANGLE_LIMIT = 150


def targets():
    """-- 뒤의 경로들. `--limit N`이 섞여 있으면 삼각형 상한을 N으로 바꾼다
    (벽 조각은 PM 배정 기준이 200 — 자연물 기준 150으로 재면 멀쩡한 조각이 걸린다)."""
    global TRIANGLE_LIMIT, HEAD_COMPARE
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--head" in args:
        HEAD_COMPARE = True
        args = [a for a in args if a != "--head"]
    if "--limit" in args:
        at = args.index("--limit")
        TRIANGLE_LIMIT = int(args[at + 1])
        args = args[:at] + args[at + 2:]
    paths = []
    for arg in args or [DEFAULT_ROOT]:
        if os.path.isdir(arg):
            paths += glob.glob(os.path.join(arg, "**", "*.fbx"), recursive=True)
        else:
            paths.append(arg)
    return sorted(paths)


def measure(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)

    rows = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        lo = [min(p[i] for p in points) for i in range(3)]
        hi = [max(p[i] for p in points) for i in range(3)]
        # 유니티는 사각형·다각형을 삼각형으로 쪼갠다 — n각형 하나 = 삼각형 n-2개.
        tris = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
        mats = sorted({slot.material.name for slot in obj.material_slots if slot.material})
        rows.append([obj.name, hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1],
                     lo[2], obj.matrix_world.translation.length, tris, mats, top_down_backfaces(obj, lo, hi),
                     camera_backfaces(obj, "기본 자세"), *uv_checks(obj, path)])
    # 애니메이션 모델은 `Open` 끝 자세에서도 한 번 더(열린 뚜껑 안쪽 같은 뒷면은 그 자세에서만 보인다)
    pose = open_end_pose()
    if pose:
        by_name = {row[0]: row for row in rows}
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH" and obj.name in by_name and obj.find_armature() is not None:
                by_name[obj.name][9] += camera_backfaces(obj, pose)
    return rows


def texture_path(material, fbx_path):
    """유니티와 같은 규칙으로 재질 이름에서 텍스처를 찾는다 — FBX 옆이나 한 칸 위의 Textures/<재질>.png."""
    base = material.name.split(".")[0]
    here = os.path.dirname(os.path.abspath(fbx_path))
    for folder in (os.path.join(here, "Textures"), os.path.join(here, "..", "Textures")):
        candidate = os.path.join(folder, base + ".png")
        if os.path.exists(candidate):
            return os.path.normpath(candidate)
    return None


def _luminance(path):
    image = bpy.data.images.load(path, check_existing=False)
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    return pixels.reshape(height, width, 4)[:, :, :3].mean(axis=2)


def uv_checks(obj, fbx_path):
    """(UV 짓눌림, 안 구운 텍셀, 텍스처 못 찾은 재질) — 짓눌림 = (면 수, 넓이 비율, 가장 넓은 면 중심, 경고인가),
    텍셀 = [(재질, 종류, 면 수, 넓이 비율, 가장 넓은 면 중심)] (종류: "순검정" / "[0,1] 밖")."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    bm.normal_update()
    uv = bm.loops.layers.uv.active
    if uv is None or not bm.faces:
        bm.free()
        return None, [], []
    slots = [slot.material for slot in obj.material_slots]
    el = math.radians(CAMERA_ELEVATION)
    faces = []
    for face in bm.faces:
        index = face.material_index
        card = index < len(slots) and slots[index] is not None and slots[index].name.split(".")[0].endswith("_잎카드")
        n = face.normal
        # 어느 방위에서 내려다봐도 등을 보이는 면(땅을 향한 밑면)은 게임에서 안 보인다
        if not card and n.z * math.sin(el) + math.hypot(n.x, n.y) * math.cos(el) <= 0.0:
            continue
        uvs = [loop[uv].uv.copy() for loop in face.loops]
        uv_area = abs(sum(uvs[i].x * uvs[i - 1].y - uvs[i - 1].x * uvs[i].y for i in range(len(uvs)))) / 2.0
        faces.append((face, face.calc_area(), uv_area, uvs))
    total = sum(area for _, area, _, _ in faces) or 1.0
    ratios = sorted(uv_area / area for _, area, uv_area, _ in faces if area > 1e-9)
    squash = None
    if ratios:
        limit = ratios[len(ratios) // 2] / UV_SQUASH_FACTOR
        squashed = [(face, area) for face, area, uv_area, _ in faces if area > 1e-9 and uv_area / area < limit]
        if squashed:
            share = sum(area for _, area in squashed) / total
            worst = max(squashed, key=lambda item: item[1])[0].calc_center_median()
            squash = (len(squashed), share, tuple(round(c, 1) for c in worst), share >= UV_SQUASH_WARN_RATIO)

    texels, missing = [], []
    by_material = {}
    for item in faces:
        index = item[0].material_index
        material = slots[index] if index < len(slots) else None
        if material is not None:
            by_material.setdefault(material.name, (material, []))[1].append(item)
    for name, (material, items) in sorted(by_material.items()):
        base = name.split(".")[0]
        if base.endswith("_잎카드") or any(hint in base for hint in DARK_MATERIAL_HINTS):
            continue
        path = texture_path(material, fbx_path)
        if path is None:
            missing.append(base)
            continue
        lum = _luminance(path)
        height, width = lum.shape
        loops = [q for _, _, _, uvs in items for q in uvs]
        atlas = sum(1 for q in loops if -1e-3 <= q.x <= 1 + 1e-3 and -1e-3 <= q.y <= 1 + 1e-3) >= ATLAS_INSIDE_RATIO * len(loops)
        black, outside = [], []
        for face, area, _, uvs in items:
            center = sum(uvs, Vector((0.0, 0.0))) / len(uvs)
            x = min(width - 1, int((center.x % 1.0) * width))
            y = min(height - 1, int((center.y % 1.0) * height))
            if lum[y, x] < BLACK_TEXEL:
                black.append((face, area))
            if atlas and any(not (-1e-3 <= q.x <= 1 + 1e-3 and -1e-3 <= q.y <= 1 + 1e-3) for q in uvs):
                outside.append((face, area))
        for kind, found in (("순검정", black), ("[0,1] 밖", outside)):
            if found:
                worst = max(found, key=lambda item: item[1])[0].calc_center_median()
                texels.append((base, kind, len(found), sum(area for _, area in found) / total,
                               tuple(round(c, 1) for c in worst)))
    bm.free()
    return squash, texels, missing


def _snapshot():
    """지금 장면의 메시들 — [(BVH, bmesh, 면별 재질 이름)], 치수(가로·세로·앞뒤·최저점·원점·삼각형), lo, hi."""
    shots, points, tris, origin = [], [], 0, 0.0
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        bm = _evaluated_bmesh(obj)
        names = [slot.material.name.split(".")[0] if slot.material else "" for slot in obj.material_slots]
        shots.append((BVHTree.FromBMesh(bm), bm, names))
        points += [v.co.copy() for v in bm.verts]
        tris += sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
        origin = max(origin, obj.matrix_world.translation.length)
    if not points:
        return shots, None, None, None
    lo = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    dims = (hi.x - lo.x, hi.z - lo.z, hi.y - lo.y, lo.z, origin, tris)
    return shots, dims, lo, hi


def _first_material(shots, origin, direction, distance):
    best, name = None, None
    for tree, bm, names in shots:
        loc, _, index, dist = tree.ray_cast(origin, direction, distance)
        if index is not None and (best is None or dist < best):
            material_index = bm.faces[index].material_index
            best, name = dist, names[material_index] if material_index < len(names) else ""
    return name


def compare_head(path):
    """HEAD의 같은 경로 FBX와 비교 — None(비교 안 함 이유 문자열) 또는 dict."""
    rel = os.path.relpath(os.path.abspath(path), PROJECT)
    result = subprocess.run(["git", "-C", PROJECT, "show", f"HEAD:{rel}"], capture_output=True)
    if result.returncode != 0:
        return "HEAD에 없음(새 파일)"
    with open(path, "rb") as handle:
        if handle.read() == result.stdout:
            return "HEAD와 바이트 같음"
    folder = tempfile.mkdtemp(prefix="head_fbx_")
    old_path = os.path.join(folder, os.path.basename(path))
    with open(old_path, "wb") as handle:
        handle.write(result.stdout)
    snaps = []
    for source in (old_path, path):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=source)
        snaps.append(_snapshot())
    os.remove(old_path)
    os.rmdir(folder)
    (old, old_dims, old_lo, old_hi), (new, new_dims, new_lo, new_hi) = snaps
    if old_dims is None or new_dims is None:
        return "메시 없음"
    lo = Vector((min(old_lo.x, new_lo.x), min(old_lo.y, new_lo.y), min(old_lo.z, new_lo.z)))
    hi = Vector((max(old_hi.x, new_hi.x), max(old_hi.y, new_hi.y), max(old_hi.z, new_hi.z)))
    height = max(hi.z - lo.z, 0.1)
    el = math.radians(CAMERA_ELEVATION)
    reach = height / math.tan(el) + 1.0
    # camera_backfaces와 같은 정정 — 가로·앞뒤가 키보다 큰 물체도 광선 원점이 바깥에서 시작하게
    travel = max(height / math.sin(el), (max(hi.x - lo.x, hi.y - lo.y) + reach) / math.cos(el)) + 2.0
    views = {}
    down = Vector((0.0, 0.0, -1.0))
    views["위에서"] = [(Vector((lo.x + (hi.x - lo.x) * (i + 0.5) / GRID, lo.y + (hi.y - lo.y) * (j + 0.5) / GRID,
                               hi.z + 1.0)), down, height + 2.0) for i in range(GRID) for j in range(GRID)]
    for label, (ax, ay) in AZIMUTHS.items():
        d = Vector((ax * math.cos(el), ay * math.cos(el), -math.sin(el)))
        x0, x1 = lo.x - (reach if ax < 0 else 0.0), hi.x + (reach if ax > 0 else 0.0)
        y0, y1 = lo.y - (reach if ay < 0 else 0.0), hi.y + (reach if ay > 0 else 0.0)
        views[label] = [(Vector((x0 + (x1 - x0) * (i + 0.5) / GRID, y0 + (y1 - y0) * (j + 0.5) / GRID, lo.z)) - d * travel,
                         d, travel * 2.0) for i in range(GRID) for j in range(GRID)]
    changes = {}
    for label, rays in views.items():
        pairs, hits = Counter(), 0
        for origin, d, distance in rays:
            a, b = _first_material(old, origin, d, distance), _first_material(new, origin, d, distance)
            if a is None and b is None:
                continue
            hits += 1
            if a != b:
                pairs[(a or "(없음)", b or "(없음)")] += 1
        changes[label] = (sum(pairs.values()) / max(hits, 1), hits, pairs)
    for shots in (old, new):
        for _, bm, _ in shots:
            bm.free()
    return {"old": old_dims, "new": new_dims, "changes": changes}


def open_end_pose():
    """가져온 액션 중 이름이 …Open으로 끝나는 것을 뼈대에 걸고 마지막 프레임으로 — 돌려주는 값은 자세 이름(없으면 None)."""
    act = next((a for a in bpy.data.actions if a.name.endswith("Open")), None)
    if act is None:
        return None
    for arm in (o for o in bpy.context.scene.objects if o.type == "ARMATURE"):
        if arm.animation_data is None:
            arm.animation_data_create()
        arm.animation_data.action = act
    bpy.context.scene.frame_set(int(act.frame_range[1]))
    return "Open 끝 자세"


def _evaluated_bmesh(obj):
    """모디파이어(뼈대 자세)까지 반영한 월드 좌표 메시."""
    bm = bmesh.new()
    bm.from_object(obj, bpy.context.evaluated_depsgraph_get())
    bm.transform(obj.matrix_world)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    return bm


def camera_backfaces(obj, pose):
    """게임 카메라 각도(내려다보는 각 CAMERA_ELEVATION, 방위 넷)에서 쏜 광선이 처음 맞는 면이 광선을 등지는 곳 —
    [(방위, x, y, z, 넓이, 경고인가, 자세)]. 옆·비스듬히 보이는 뒷면(한 겹 뚜껑 안쪽 등)은 위에서 본 검사가 못 잡는다.
    땅 위 과녁 격자는 바닥 사각형을 광선이 나아가는 쪽으로 (키 ÷ tan 각)만큼 늘려 건물 옆면까지 덮는다."""
    bm = _evaluated_bmesh(obj)
    tree = BVHTree.FromBMesh(bm)
    names = [slot.material.name if slot.material else "" for slot in obj.material_slots]
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    lo, hi = Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))
    floor = max((hi.x - lo.x) * (hi.y - lo.y), 1e-6)
    height = max(hi.z - lo.z, 0.1)
    el = math.radians(CAMERA_ELEVATION)
    reach = height / math.tan(el) + 1.0
    travel = height / math.sin(el) + 2.0
    found = {}
    for label, (ax, ay) in AZIMUTHS.items():
        d = Vector((ax * math.cos(el), ay * math.cos(el), -math.sin(el)))
        x0, x1 = lo.x - (reach if ax < 0 else 0.0), hi.x + (reach if ax > 0 else 0.0)
        y0, y1 = lo.y - (reach if ay < 0 else 0.0), hi.y + (reach if ay > 0 else 0.0)
        for i in range(GRID):
            for j in range(GRID):
                target = Vector((x0 + (x1 - x0) * (i + 0.5) / GRID, y0 + (y1 - y0) * (j + 0.5) / GRID, lo.z))
                loc, _, index, _ = tree.ray_cast(target - d * travel, d, travel * 2.0)
                if loc is None or index in found:
                    continue
                face = bm.faces[index]
                name = names[face.material_index] if face.material_index < len(names) else ""
                # 같은 자리에 카메라를 보는 면이 겹쳐 있으면(양면 쌍둥이) 보이는 면이므로 뒷면이 아니다
                covered = any(bm.faces[k].normal.dot(d) < -0.1 for _, _, k, _ in tree.find_nearest_range(loc, 1e-3))
                if face.normal.dot(d) > 0.1 and not covered and not name.split(".")[0].endswith("_잎카드"):
                    area = face.calc_area()
                    found[index] = (label, round(loc.x, 1), round(loc.y, 1), round(loc.z, 1), area,
                                    area / floor >= BACKFACE_WARN_RATIO, pose)
    bm.free()
    return list(found.values())


def top_down_backfaces(obj, lo, hi):
    """위에서 수직으로 쏜 광선이 처음 맞는 면이 아래를 보는 면인 곳 — [(x, y, 넓이, 경고인가)]. 월드 좌표(게임 단위)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    names = [slot.material.name if slot.material else "" for slot in obj.material_slots]
    floor = max((hi[0] - lo[0]) * (hi[1] - lo[1]), 1e-6)
    found = {}
    for i in range(GRID):
        for j in range(GRID):
            x = lo[0] + (hi[0] - lo[0]) * (i + 0.5) / GRID
            y = lo[1] + (hi[1] - lo[1]) * (j + 0.5) / GRID
            loc, _, index, _ = tree.ray_cast(Vector((x, y, hi[2] + 1.0)), Vector((0.0, 0.0, -1.0)))
            if loc is None:
                continue
            face = bm.faces[index]
            name = names[face.material_index] if face.material_index < len(names) else ""
            # 같은 자리에 위를 보는 면이 겹쳐 있으면(뒤집은 쌍둥이로 만든 양면) 위에서 보이므로 뒷면이 아니다
            covered = any(bm.faces[k].normal.z > 0.1 for _, _, k, _ in tree.find_nearest_range(loc, 1e-3))
            if face.normal.z < -0.1 and not covered and not name.split(".")[0].endswith("_잎카드"):
                area = face.calc_area()
                found.setdefault(index, (round(x, 1), round(y, 1), area, area / floor >= BACKFACE_WARN_RATIO))
    bm.free()
    return list(found.values())


def main():
    # 임포터가 파일마다 한 줄씩 찍어서, 다 읽고 난 뒤에 표를 한꺼번에 찍는다.
    measured, heads = [], []
    for path in targets():
        rel = os.path.join(os.path.basename(os.path.dirname(path)), os.path.basename(path))
        measured += [(rel, *row) for row in measure(path)]
        if HEAD_COMPARE:
            heads.append((rel, compare_head(path)))

    problems = 0
    print("=" * 100)
    print(f"{'파일':24} {'가로':>7} {'세로':>7} {'앞뒤':>7} {'최저점':>7} {'원점거리':>8} {'삼각형':>6}  재질")
    notes = []
    folders = {}   # 폴더 → [파일 수, UV 짓눌림 경고, 안 구운 텍셀 경고, 뒷면 경고, 텍스처 못 찾음]
    for rel, name, w, h, d, low, origin, tris, mats, backfaces, cams, squash, texels, missing in measured:
        tally = folders.setdefault(os.path.dirname(rel), [0, 0, 0, 0, 0])
        tally[0] += 1
        if missing:
            tally[4] += 1
        flags = []
        if abs(low) > 0.01:
            flags.append("최저점≠0")
        if origin > 0.01:
            flags.append("원점≠발밑")
        if tris > TRIANGLE_LIMIT:
            flags.append(f"삼각형>{TRIANGLE_LIMIT}")
        warn = [b for b in backfaces if b[3]]
        if warn:
            flags.append(f"위에서 뒷면 {len(warn)}곳")
        cam_warn = [c for c in cams if c[5]]
        if cam_warn:
            flags.append(f"카메라각 뒷면 {len(cam_warn)}곳")
        if warn or cam_warn:
            tally[3] += 1
        if squash and squash[3]:
            flags.append(f"UV 짓눌림 {squash[1] * 100:.1f}%")
            tally[1] += 1
        if texels:
            flags.append(f"안 구운 텍셀 {sum(t[2] for t in texels)}면")
            tally[2] += 1
        problems += len(flags)
        note = ("  ⚠️ " + ",".join(flags)) if flags else ""
        print(f"{rel:24} {w:7.2f} {h:7.2f} {d:7.2f} {low:7.2f} {origin:8.2f} {tris:6d}  {'/'.join(mats)}{note}")
        if backfaces:
            listed = ", ".join(f"({x}, {y}) 넓이 {a:.1f}{'' if big else ' 참고'}" for x, y, a, big in backfaces[:6])
            notes.append(f"  {rel}: 위에서 본 뒷면 {len(backfaces)}곳(경고 {len(warn)}) — {listed}")
        if cams:
            ordered = sorted(cams, key=lambda c: -c[4])
            listed = ", ".join(f"[{lab}·{pose}] ({x}, {y}, {z}) 넓이 {a:.1f}{'' if big else ' 참고'}"
                               for lab, x, y, z, a, big, pose in ordered[:5])
            notes.append(f"  {rel}: 카메라각(50°) 뒷면 {len(cams)}곳(경고 {len(cam_warn)}) — {listed}")
        if squash:
            count, share, where, big = squash
            notes.append(f"  {rel}: UV 짓눌림 {count}면, 넓이 {share * 100:.2f}%{'' if big else ' 참고'} — 가장 넓은 면 {where}")
        for material, kind, count, share, where in texels:
            notes.append(f"  {rel}: 안 구운 텍셀 [{material}] {kind} {count}면, 넓이 {share * 100:.2f}% — 가장 넓은 면 {where}")
    print("=" * 100)
    for line in notes:
        print(line)
    print("-" * 100)
    print(f"{'폴더':24} {'파일':>5} {'UV 짓눌림':>9} {'안 구운 텍셀':>11} {'뒷면':>5} {'텍스처 못 찾음':>12}   (해당 파일 수)")
    for folder, (files, squashed, unbaked, backs, nofile) in sorted(folders.items()):
        print(f"{folder:24} {files:5d} {squashed:9d} {unbaked:11d} {backs:5d} {nofile:12d}")
    print(f"문제 {problems}건")
    if HEAD_COMPARE:
        changed = 0
        print("=" * 100)
        print("HEAD 대비(치수 가로×세로×앞뒤 · 최저점 · 원점 · 삼각형, 방위별 처음 맞는 재질이 바뀐 광선 비율)")
        for rel, result in heads:
            if not isinstance(result, dict):
                print(f"  {rel}: {result}")
                continue
            old, new = result["old"], result["new"]
            same = all(abs(a - b) < 0.01 for a, b in zip(old[:5], new[:5]))
            size = (f"{old[0]:.2f}×{old[1]:.2f}×{old[2]:.2f} → {new[0]:.2f}×{new[1]:.2f}×{new[2]:.2f}"
                    f" · 최저점 {old[3]:.2f}→{new[3]:.2f} · 원점 {old[4]:.2f}→{new[4]:.2f} · 삼각형 {old[5]}→{new[5]}")
            rates = " ".join(f"{label} {rate * 100:.1f}%" for label, (rate, _, _) in result["changes"].items())
            big = [(label, pairs) for label, (rate, _, pairs) in result["changes"].items() if rate >= HEAD_CHANGE_WARN]
            mark = []
            if not same:
                mark.append("치수 다름")
            if big:
                mark.append("외형 변화")
                changed += 1
            print(f"  {rel}: {size} {'(치수 같음)' if same else ''}{'  ⚠️ ' + ','.join(mark) if mark else ''}")
            print(f"      {rates}")
            for label, pairs in big:
                listed = ", ".join(f"{a} → {b} {n}발" for (a, b), n in pairs.most_common(4))
                print(f"      [{label}] {listed}")
        print(f"외형 변화 {changed}건(의도한 수정이면 커밋 메시지에 적고 넘긴다)")


main()
