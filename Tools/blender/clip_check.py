"""자체 클립(Generic 유닛)이 **바닥을 뚫지 않는지** — 클립마다 전 프레임의 최저 z와 최대 변을 잰다.

🔴 **클립을 새로 짓거나(`synth_clips`) 자른 뒤에는(`split_clips`) 이 검사와 `clip_table.py`를 _둘 다_ 돌릴 것.**
   한 지표만 보면 놓친다 — 2026-09-23에 배 둘(고대의배·해적선, 241프레임)을 「길다」만 보고 「한 클립에 여러
   동작이 이어 붙었다」고 오진했다. `clip_table.py`의 **이음새**를 같이 재니 0.000이라 순수한 흔들림 루프였고,
   진짜 그런 유닛은 안흔함_강재규 하나뿐이었다. 반대로 이 검사만 보면 바닥은 0인데 첫↔끝 자세가 어긋난
   클립(Idle이 한 바퀴마다 튀는 것)을 못 잡는다. **바닥은 여기서, 이음새는 저기서** 본다.

쓰는 법:
  blender -b --factory-startup --python Tools/blender/clip_check.py -- <유닛.fbx> [<유닛.fbx> ...]

읽는 법:
  · **최저 z** — 0이어야 한다. 음수면 그 프레임에 발(또는 꼬리·자락)이 바닥을 뚫는다.
    고치는 법: 지은 클립이면 `ground=True`(synth_clips), 원본 클립이면 `clip_ground=True`.
  · **최대 변** — 쉬는 자세 크기에서 크게 벗어나면 뼈대가 터진 것이다(사람 클립을 남의 뼈대에 잘못 입힌 증상).
"""

import os
import sys

import bpy
import numpy as np


def check(fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    scn = bpy.context.scene
    arm = next((o for o in scn.objects if o.type == "ARMATURE"), None)
    ms = [o for o in scn.objects if o.type == "MESH" and len(o.data.vertices)]
    name = os.path.splitext(os.path.basename(fbx))[0]
    if arm is None or not bpy.data.actions:
        print("%-16s 자체 클립 없음" % name)
        return
    arm.animation_data_create()
    out = []
    for act in sorted(bpy.data.actions, key=lambda a: -a.frame_range[1]):
        arm.animation_data.action = act
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        lo, hi = 9e9, 0.0
        for f in range(f0, f1 + 1, 2):
            scn.frame_set(f)
            dg = bpy.context.evaluated_depsgraph_get()
            P = np.concatenate([np.array([(o.matrix_world @ v.co)[:] for v in o.evaluated_get(dg).to_mesh().vertices])
                                for o in ms])
            lo = min(lo, float(P[:, 2].min()))
            hi = max(hi, float((P.max(0) - P.min(0)).max()))
        out.append("%-7s 최저z %+.4f 최대변 %.3f" % (act.name.split("|")[-1], lo, hi))
    print("%-16s " % name + " | ".join(out))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    assert args, "잴 FBX를 하나 이상 주세요"
    for a in args:
        check(a)


main()
