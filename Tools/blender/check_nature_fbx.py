"""만든 FBX를 다시 읽어 게임 단위 크기·삼각형 수·재질을 찍는다.

화면 없이 돈다(경로를 안 주면 Assets/Art/Nature 전체):
    blender --background --factory-startup --python Tools/blender/check_nature_fbx.py -- [폴더나 파일 ...]

왜 다시 읽는가 — 스크립트 안의 치수는 "만들려던 크기"일 뿐이다. 내보내기 배수·단위 변환이
한 번 어긋나면 파일 속 크기는 100배로도 틀어진다. 실제 파일을 읽어 봐야 유니티가 받는 크기다.

⚠️ 축: Blender는 Z가 위, 유니티는 Y가 위다. 임포터가 이미 바꿔 주므로 여기서는
가로 = X, 앞뒤 = Y, 세로(높이) = Z로 읽는다.

⚠️ 원점 검사: 최저점이 0이어도 오브젝트 위치가 0이 아니면 "원점은 발밑"이 아니다 —
맵에 놓을 때 위치를 덮어쓰면 그 차이만큼 뜨거나 묻힌다. 그래서 둘 다 찍는다.
"""

import bpy
import glob
import os
import sys

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
        rows.append((obj.name, hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1],
                     lo[2], obj.matrix_world.translation.length, tris, mats))
    return rows


def main():
    # 임포터가 파일마다 한 줄씩 찍어서, 다 읽고 난 뒤에 표를 한꺼번에 찍는다.
    measured = []
    for path in targets():
        rel = os.path.join(os.path.basename(os.path.dirname(path)), os.path.basename(path))
        measured += [(rel, *row) for row in measure(path)]

    problems = 0
    print("=" * 100)
    print(f"{'파일':24} {'가로':>7} {'세로':>7} {'앞뒤':>7} {'최저점':>7} {'원점거리':>8} {'삼각형':>6}  재질")
    for rel, name, w, h, d, low, origin, tris, mats in measured:
        flags = []
        if abs(low) > 0.01:
            flags.append("최저점≠0")
        if origin > 0.01:
            flags.append("원점≠발밑")
        if tris > TRIANGLE_LIMIT:
            flags.append(f"삼각형>{TRIANGLE_LIMIT}")
        problems += len(flags)
        note = ("  ⚠️ " + ",".join(flags)) if flags else ""
        print(f"{rel:24} {w:7.2f} {h:7.2f} {d:7.2f} {low:7.2f} {origin:8.2f} {tris:6d}  {'/'.join(mats)}{note}")
    print("=" * 100)
    print(f"문제 {problems}건")


main()
