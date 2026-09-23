"""유닛별 자체 클립 표 — 이름·개수·길이 + **이음새**(끝 자세 ↔ 첫 자세) 진단.

🔴 **클립을 새로 짓거나 자른 뒤에는 `clip_check.py`와 _둘 다_ 돌릴 것.**
   2026-09-23에 「클립이 241프레임으로 길다」만 보고 배 둘(고대의배·해적선)을 「한 클립에 여러 동작이
   이어 붙었다」고 오진했다. 여기 **이음새**를 같이 재니 0.000이라 순수한 흔들림 루프였다.
   진짜 그런 유닛은 안흔함_강재규 하나뿐이었고, 그건 이음새가 아니라 **내용**(서기·웅크리기·앞발 치기)으로 갈렸다.
   → 「길다」는 결함의 근거가 못 된다. 길이 · 이음새 · 눈(렌더) 셋을 같이 본다.

읽는 법:
  · **이음새** = 마지막 프레임과 첫 프레임의 뼈 머리 차이(뼈당 RMS, m). 뼈 수가 달라도 견줄 수 있게 나눠 뒀다.
      - 계속 도는 클립(Idle · Move)은 **0에 가까워야** 한다. 크면 한 바퀴마다 자세가 튄다.
        고치는 법: 자른 클립이면 `split_clips`의 `loop=k`(끝 k프레임을 첫 자세로 당긴다),
        지은 클립이면 위상을 맞춰 첫·끝이 쉬는 자세가 되게 한다(진폭 × (sin(2πt/N+φ) − sin(φ)) 꼴이면 자동).
      - 한 번짜리(Attack)는 **커도 정상**이다 — 때린 자세로 끝나는 게 맞고, 유니티가 트리거 뒤 Idle로 섞어 돌아간다.
  · **첫자세에서 최대** = 동작의 크기. 0에 가까우면 사실상 정지 클립이다.
  · 🔴 표시 = 클립이 하나뿐인데 길고(150프레임 이상) 크게 움직이면서 **이음새까지 큰** 경우 —
    「여러 동작을 이어 붙였을 수 있다」는 뜻이지 확정이 아니다. **렌더로 눈으로 확인할 것.**

쓰는 법:
  blender -b --factory-startup --python Tools/blender/clip_table.py -- <이름>=<유닛.fbx> [...]
  (이름 없이 경로만 줘도 된다 — 파일 이름을 쓴다)
"""

import os
import sys

import bpy
import numpy as np


def row(label, fbx):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    scn = bpy.context.scene
    arm = next((o for o in scn.objects if o.type == "ARMATURE"), None)
    acts = sorted(bpy.data.actions, key=lambda a: -a.frame_range[1])
    if arm is None or not acts:
        print("%-16s 클립 없음" % label)
        return
    arm.animation_data_create()
    nb = len(arm.pose.bones)
    out = []
    for act in acts:
        arm.animation_data.action = act
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        n = f1 - f0 + 1
        P = []
        for f in range(f0, f1 + 1, max(1, n // 24)):
            scn.frame_set(f)
            P.append(np.array([(arm.matrix_world @ pb.head)[:] for pb in arm.pose.bones]).ravel())
        P = np.array(P)
        span = float(np.linalg.norm(P - P[0], axis=1).max() / np.sqrt(nb))
        seam = float(np.linalg.norm(P[-1] - P[0]) / np.sqrt(nb))
        out.append((act.name.split("|")[-1], n, span, seam))
    flag = ""
    if len(out) == 1 and out[0][1] >= 150 and out[0][2] > 0.05 and out[0][3] > 0.02:
        flag = "  🔴 한 클립에 여러 동작이 이어 붙었을 수 있음 — 렌더로 확인할 것"
    elif len(out) > 1 and any(o[1] <= 2 for o in out):
        flag = "  ⚠️ 키 1~2개짜리 껍데기 클립 있음"
    print("%-16s 클립 %d개 · 총 %d프레임%s" % (label, len(out), sum(o[1] for o in out), flag))
    for nm, n, span, seam in out:
        print("      %-10s %4d프레임 · 첫자세에서 최대 %.3f m · **이음새 %.4f m**" % (nm, n, span, seam))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    assert args, "잴 FBX를 하나 이상 주세요(<이름>=<경로> 또는 <경로>)"
    for a in args:
        label, path = a.split("=", 1) if "=" in a else (os.path.splitext(os.path.basename(a))[0], a)
        row(label, path)


main()
