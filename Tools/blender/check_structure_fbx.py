"""스크래치용 검사(저장소 권한 복구 전) — check_nature_fbx.py의 치수·삼각형·위에서 본 뒷면·카메라각 50° 뒷면을 그대로 옮긴 것.
    blender -b --factory-startup --python check_scratch.py -- 파일.fbx ...
빈 오브젝트(파티클 자리) 이름·게임 좌표도 찍는다."""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

GRID = 24
WARN = 0.01
EL = math.radians(50.0)
AZIMUTHS = {"앞(−Y)에서": (0.0, 1.0), "뒤(+Y)에서": (0.0, -1.0), "왼(−X)에서": (1.0, 0.0), "오른(+X)에서": (-1.0, 0.0)}


POSE = None   # --pose=Open: 뼈대에 그 클립 끝 프레임을 걸고 변형된 메시로 잰다(보물상자 열린 끝 자세 뒷면 검사)


def _mesh(obj):
    if POSE:
        return obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
    return obj.data


def world_bm(obj):
    bm = bmesh.new()
    bm.from_mesh(_mesh(obj))
    bm.transform(obj.matrix_world)
    bm.normal_update()
    bm.faces.ensure_lookup_table()
    return bm


def card(names, face):
    return (names[face.material_index] if face.material_index < len(names) else "").split(".")[0].endswith("_잎카드")


def top_down(obj, lo, hi):
    bm = world_bm(obj)
    tree = BVHTree.FromBMesh(bm)
    names = [s.material.name if s.material else "" for s in obj.material_slots]
    floor = max((hi.x - lo.x) * (hi.y - lo.y), 1e-6)
    found = {}
    for i in range(GRID):
        for j in range(GRID):
            x = lo.x + (hi.x - lo.x) * (i + 0.5) / GRID
            y = lo.y + (hi.y - lo.y) * (j + 0.5) / GRID
            loc, _, index, _ = tree.ray_cast(Vector((x, y, hi.z + 1.0)), Vector((0, 0, -1)))
            if loc is None:
                continue
            face = bm.faces[index]
            covered = any(bm.faces[k].normal.z > 0.1 for _, _, k, _ in tree.find_nearest_range(loc, 1e-3))
            if face.normal.z < -0.1 and not covered and not card(names, face):
                area = face.calc_area()
                found.setdefault(index, (round(x, 1), round(y, 1), area, area / floor >= WARN, round(loc.z, 1)))
    bm.free()
    return list(found.values())


def camera(obj, lo, hi):
    bm = world_bm(obj)
    tree = BVHTree.FromBMesh(bm)
    names = [s.material.name if s.material else "" for s in obj.material_slots]
    floor = max((hi.x - lo.x) * (hi.y - lo.y), 1e-6)
    height = max(hi.z - lo.z, 0.1)
    reach = height / math.tan(EL) + 1.0
    travel = height / math.sin(EL) + 2.0
    found = {}
    for label, (ax, ay) in AZIMUTHS.items():
        d = Vector((ax * math.cos(EL), ay * math.cos(EL), -math.sin(EL)))
        x0, x1 = lo.x - (reach if ax < 0 else 0.0), hi.x + (reach if ax > 0 else 0.0)
        y0, y1 = lo.y - (reach if ay < 0 else 0.0), hi.y + (reach if ay > 0 else 0.0)
        for i in range(GRID):
            for j in range(GRID):
                target = Vector((x0 + (x1 - x0) * (i + 0.5) / GRID, y0 + (y1 - y0) * (j + 0.5) / GRID, lo.z))
                loc, _, index, _ = tree.ray_cast(target - d * travel, d, travel * 2.0)
                if loc is None or index in found:
                    continue
                face = bm.faces[index]
                covered = any(bm.faces[k].normal.dot(d) < -0.1 for _, _, k, _ in tree.find_nearest_range(loc, 1e-3))
                if face.normal.dot(d) > 0.1 and not covered and not card(names, face):
                    area = face.calc_area()
                    found[index] = (label, round(loc.x, 1), round(loc.y, 1), round(loc.z, 1), area, area / floor >= WARN)
    bm.free()
    return list(found.values())


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    # --water: 물에 뜨거나 박히는 소품(잔교·목선) — z<0 면은 게임에서 수면에 가려지니 참고로만 센다(PM 2026-09-13)
    water = "--water" in args
    global POSE
    POSE = next((a.split("=", 1)[1] for a in args if a.startswith("--pose=")), None)
    args = [a for a in args if not a.startswith("--")]
    lines, problems = [], 0
    for path in args:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        if POSE:
            act = next((a for a in bpy.data.actions if a.name.split("|")[-1] == POSE), None)
            if act is None:
                problems += 1
                lines.append(f"  ⚠️ 클립 {POSE} 없음: {[a.name for a in bpy.data.actions]}")
            for arm in [o for o in bpy.context.scene.objects if o.type == "ARMATURE" and act]:
                ad = arm.animation_data or arm.animation_data_create()
                ad.action = act
                if hasattr(ad, "action_slot") and len(act.slots):
                    ad.action_slot = act.slots[0]
                bpy.context.scene.frame_set(int(round(act.frame_range[1])))
                lines.append(f"  자세: {act.name} 끝 프레임 {int(round(act.frame_range[1]))}")
        for obj in bpy.context.scene.objects:
            if obj.type == "EMPTY":
                lines.append(f"  빈 오브젝트 {obj.name}  게임 좌표 {tuple(round(c, 2) for c in obj.matrix_world.translation)}"
                             f"  부모 {obj.parent.name if obj.parent else None}")
            if obj.type != "MESH":
                continue
            pts = [obj.matrix_world @ v.co for v in _mesh(obj).vertices]
            lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
            hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
            tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
            mats = sorted({s.material.name for s in obj.material_slots if s.material})
            tops, cams = top_down(obj, lo, hi), camera(obj, lo, hi)
            tw = [t for t in tops if t[3] and not (water and t[4] < 0)]
            cw = [c for c in cams if c[5] and not (water and c[3] < 0)]
            if water:
                lines.append(f"  물 아래(z<0) 뒷면 참고: 위 {sum(1 for t in tops if t[3] and t[4] < 0)} · 카메라 {sum(1 for c in cams if c[5] and c[3] < 0)}")
            problems += len(tw) + len(cw)
            # 원점 규칙(PM 2026-09-13): FBX 최상위 오브젝트만 원점 = 발밑 가운데여야 한다. 자식 원점은 경첩·회전축 같은
            # 의도한 피벗일 수 있어 경고가 아니라 참고 줄로만 찍는다.
            if obj.parent is None and obj.matrix_world.translation.length > 0.01:
                problems += 1
                lines.append(f"  ⚠️ 최상위 원점≠발밑: {obj.name} {tuple(round(c, 2) for c in obj.matrix_world.translation)}")
            elif obj.parent is not None:
                lines.append(f"  자식 피벗(참고): {obj.name} {tuple(round(c, 2) for c in obj.matrix_world.translation)}")
            lines.insert(0, f"{os.path.basename(path)} [{obj.name}]  가로 {hi.x - lo.x:.2f}  세로 {hi.z - lo.z:.2f}  앞뒤 {hi.y - lo.y:.2f}"
                            f"  최저점 {lo.z:.2f}  원점거리 {obj.matrix_world.translation.length:.2f}  삼각형 {tris}  재질 {'/'.join(mats)}")
            lines.append(f"  위에서 본 뒷면 {len(tops)}곳(경고 {len(tw)}) {tops[:5]}")
            lines.append(f"  카메라각 뒷면 {len(cams)}곳(경고 {len(cw)}) {sorted(cams, key=lambda c: -c[4])[:5]}")
    print("=" * 80)
    for line in lines:
        print(line)
    print(f"뒷면 경고 {problems}건")


main()
