"""유닛 FBX 동작 검사(스크래치) — 내보낸 파일을 다시 읽어 뼈·클립 이름·길이, 첫 = 끝, Hull 최대 기울기, 쉼 자세 치수를 찍는다.
    blender -b --factory-startup --python check_anim.py -- 파일.fbx ..."""
import math
import sys

import bpy


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for path in args:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        scene = bpy.context.scene
        print("=" * 80)
        print(path)
        print("  액션", [(a.name, tuple(round(f) for f in a.frame_range)) for a in bpy.data.actions], " fps", scene.render.fps)
        for arm in [o for o in scene.objects if o.type == "ARMATURE"]:
            act = arm.animation_data.action if arm.animation_data else None
            print(f"  뼈대 {arm.name}  원점 {tuple(round(c, 3) for c in arm.matrix_world.translation)}  배율 {tuple(round(c, 4) for c in arm.scale)}")
            print(f"  뼈 {[b.name for b in arm.data.bones]}")
            if act is None:
                print("  ⚠️ 동작 없음")
                continue
            f0, f1 = (int(round(f)) for f in act.frame_range)

            def pose(frame):
                scene.frame_set(frame)
                return [pb.matrix.copy() for pb in arm.pose.bones]

            first, last = pose(f0), pose(f1)
            gap = max(abs(x - y) for a, b in zip(first, last) for ra, rb in zip(a, b) for x, y in zip(ra, rb))
            tilt = 0.0
            if "Hull" in arm.pose.bones:
                rest = arm.data.bones["Hull"].matrix_local
                for frame in range(f0, f1 + 1, 5):
                    scene.frame_set(frame)
                    e = (rest.inverted() @ arm.pose.bones["Hull"].matrix).to_euler()
                    tilt = max(tilt, abs(math.degrees(e.x)), abs(math.degrees(e.z)))
            print(f"  동작 {act.name}  프레임 {f0}~{f1}  첫=끝 최대 차 {gap:.2e}  Hull 최대 기울기 {tilt:.2f}°")
        scene.frame_set(0)
        for obj in [o for o in scene.objects if o.type == "MESH"]:
            pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
            lo = [min(p[k] for p in pts) for k in range(3)]
            hi = [max(p[k] for p in pts) for k in range(3)]
            print(f"  메시 {obj.name}  부모 {obj.parent.name if obj.parent else None}  정점그룹 {len(obj.vertex_groups)}  "
                  f"x {lo[0]:.2f}~{hi[0]:.2f}  y {lo[1]:.2f}~{hi[1]:.2f}  z {lo[2]:.2f}~{hi[2]:.2f}  "
                  f"삼각형 {sum(len(p.vertices) - 2 for p in obj.data.polygons)}  재질 {[s.material.name for s in obj.material_slots if s.material]}")


main()
