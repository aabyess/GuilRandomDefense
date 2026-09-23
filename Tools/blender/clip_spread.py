"""내보낸 유닛 FBX가 **공용 Idle을 먹고도 안 터지는지** 재는 검수표(PM 승인 표준, 2026-09-23).

왜 필요한가 — 쉬는 자세(T자) 숫자만 보면 놓친다. 사이렌헤드(안흔함_박준희)는 뼈대와 메시가 서로 다른
좌표계라 쉬는 자세에선 멀쩡했는데 클립을 먹이자 유니티에서 ×44로 터졌다. 그래서 **클립을 실제로
입혀 본 뒤** 프레임마다 (뼈 최대 퍼짐 ÷ 쉬는 자세 메시 높이)를 잰다.

🔴 리타겟은 retarget_idle.py(세계 변화량 방식)를 쓴다 — 쿼터니언을 그대로 복사하면 리그마다 뼈 방향이
   달라 팔이 만세를 하고, 그 만세가 「퍼짐」으로 잡혀 멀쩡한 유닛이 실패로 나온다.

쓰는 법:
  blender -b --factory-startup --python Tools/blender/clip_spread.py -- <유닛.fbx> [대조군.fbx ...]
  대조군을 안 주면 랜덤_한마_바키·랜덤_손오공(둘 다 PM 승인본)을 자동으로 같이 잰다.
기준: 전 구간 최댓값 3.0 이하면 통과(사람형 승인본은 1.0~1.2 언저리).
"""

import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import retarget_idle as R                                                    # noqa: E402

PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
CONTROLS = ["랜덤_한마_바키", "랜덤_손오공"]                                  # PM 승인본 — 같은 자로 잰 대조군
FRACS = (0.0, 0.25, 0.5, 0.75, 1.0)
LIMIT = 3.0


def measure(fbx, idles):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    bpy.context.view_layer.update()
    scn = bpy.context.scene
    arm = next((o for o in scn.objects if o.type == "ARMATURE"), None)
    ms = [o for o in scn.objects if o.type == "MESH" and len(o.data.vertices)]
    V0 = np.concatenate([np.array([(o.matrix_world @ v.co)[:] for v in o.data.vertices]) for o in ms])
    H = float(V0[:, 2].max() - V0[:, 2].min())
    name = os.path.splitext(os.path.basename(fbx))[0]
    if not R.can_retarget(arm, idles):
        print(f"  {name}: 사람 필수 뼈가 없어 리타겟 안 함(Generic·네발·소품) — 쉬는 자세 높이 {H:.3f}")
        return None
    print(f"  {name}: 쉬는 자세 높이 {H:.4f} · 뼈 {len(arm.pose.bones)}")
    print("  %6s | %-26s | %-26s | %7s | %7s" % ("프레임", "뼈 퍼짐(x,y,z)", "메시 크기(x,y,z)", "뼈/높이", "메시/높이"))
    worst = 0.0
    for idle in idles:
        # 매 프레임 쉬는 자세에서 다시 시작한다(변화량은 쉬는 자세 기준이라 겹쳐 먹이면 안 된다)
        for pb in arm.pose.bones:
            pb.matrix_basis.identity()
        bpy.context.view_layer.update()
        R.apply_idle(arm, idle)
        B = np.array([(arm.matrix_world @ pb.head)[:] for pb in arm.pose.bones])
        bs = B.max(0) - B.min(0)
        dg = bpy.context.evaluated_depsgraph_get()
        P = np.concatenate([np.array([(o.matrix_world @ v.co)[:] for v in o.evaluated_get(dg).to_mesh().vertices]) for o in ms])
        vs = P.max(0) - P.min(0)
        r, rv = float(bs.max() / H), float(vs.max() / H)
        worst = max(worst, r, rv)
        print("  %6d | %-26s | %-26s | %7.2f | %7.2f"
              % (idle["frame"], [round(float(c), 3) for c in bs], [round(float(c), 3) for c in vs], r, rv))
    print("  → 최댓값 %.2f  (기준 %.1f 이하)  %s" % (worst, LIMIT, "통과" if worst <= LIMIT else "🔴 실패"))
    return worst


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    assert args, "잴 FBX를 하나 이상 주세요"
    targets = list(args)
    if len(args) == 1:
        targets += [os.path.join(PROJECT, "Assets/Art/Units", n, n + ".fbx") for n in CONTROLS]
    idles = R.load_idle(frames=FRACS)
    assert idles, f"공용 Idle이 없다: {R.IDLE}"
    print("공용 Idle %s · 프레임 %s" % (os.path.basename(R.IDLE), [i["frame"] for i in idles]))
    for t in targets:
        measure(t, idles)


main()
