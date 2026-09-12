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
"""

import bpy
import bmesh
import glob
import math
import os
import sys
from mathutils import Vector
from mathutils.bvhtree import BVHTree

GRID = 24                  # 뒷면 검사 격자(한 변)
BACKFACE_WARN_RATIO = 0.01  # 적중 면 넓이 ÷ 바닥 넓이가 이 이상이면 경고
CAMERA_ELEVATION = 50.0     # 게임 카메라가 내려다보는 각(도)
# 방위 — 카메라가 있는 쪽 : 광선의 수평 방향. 앞(−Y)에 선 카메라는 +Y 쪽을 본다.
AZIMUTHS = {"앞(−Y)에서": (0.0, 1.0), "뒤(+Y)에서": (0.0, -1.0), "왼(−X)에서": (1.0, 0.0), "오른(+X)에서": (-1.0, 0.0)}

PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Nature")
TRIANGLE_LIMIT = 150


def targets():
    """-- 뒤의 경로들. `--limit N`이 섞여 있으면 삼각형 상한을 N으로 바꾼다
    (벽 조각은 PM 배정 기준이 200 — 자연물 기준 150으로 재면 멀쩡한 조각이 걸린다)."""
    global TRIANGLE_LIMIT
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
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
                     camera_backfaces(obj, "기본 자세")])
    # 애니메이션 모델은 `Open` 끝 자세에서도 한 번 더(열린 뚜껑 안쪽 같은 뒷면은 그 자세에서만 보인다)
    pose = open_end_pose()
    if pose:
        by_name = {row[0]: row for row in rows}
        for obj in bpy.context.scene.objects:
            if obj.type == "MESH" and obj.name in by_name and obj.find_armature() is not None:
                by_name[obj.name][9] += camera_backfaces(obj, pose)
    return rows


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
    measured = []
    for path in targets():
        rel = os.path.join(os.path.basename(os.path.dirname(path)), os.path.basename(path))
        measured += [(rel, *row) for row in measure(path)]

    problems = 0
    print("=" * 100)
    print(f"{'파일':24} {'가로':>7} {'세로':>7} {'앞뒤':>7} {'최저점':>7} {'원점거리':>8} {'삼각형':>6}  재질")
    notes = []
    for rel, name, w, h, d, low, origin, tris, mats, backfaces, cams in measured:
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
    print("=" * 100)
    for line in notes:
        print(line)
    print(f"문제 {problems}건")


main()
