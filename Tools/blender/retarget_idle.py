"""공용 Idle 클립을 **뼈 이름 규칙이 제각각인 우리 유닛들**에 옮겨 입히는 정본 리타게터.

PM 요청(2026-09-23): 「`clip_spread.py` 옆에 그 리타게터도 정본으로 남겨 달라」.
show_units.py(전시판)와 clip_spread.py(검수표)가 둘 다 여기를 쓴다.

🔴 왜 따로 만들었나 — 「만세 사고」
  1차에서는 프레임마다 `pose_bone.rotation_quaternion`을 원본에서 대상으로 그대로 복사했다.
  그랬더니 **모든 유닛이 팔을 만세**로 들었다. 원인: `rotation_quaternion`은 세계 회전이 아니라
  **그 뼈의 쉬는 방향(roll) 기준 로컬 회전**이다. 원본 리그(Mixamo)와 대상 리그의 뼈 방향이 다르면
  같은 숫자가 전혀 다른 자세가 된다. 우리 유닛은 파이프라인이 넷이라 뼈 방향이 제각각이다.

🔴 고침 — **세계 회전의 변화량만 옮긴다**
      Rd = (자세 때 세계 회전) × (쉬는 때 세계 회전)⁻¹        ← 원본에서 뽑는다
      대상 세계 회전 = Rd × (대상의 쉬는 때 세계 회전)          ← 대상에 먹인다
  Rd는 「이 뼈가 쉬는 자세에서 얼마나 돌았나」라서 리그가 달라도 뜻이 같다.
  머리 자리는 **부모부터 차례로**(parent-first) 잡아야 한다 — 자식 뼈의 머리 세계 좌표가
  부모의 자세에 딸려 오기 때문이다. 그래서 order를 조상 수로 정렬한다.

🔴 뿌리(Hips)만 위치도 옮긴다(hips_off) — 클립의 무게중심 오르내림이 여기 들어 있다.
"""

import os

import bpy
from mathutils import Matrix, Vector

IDLE = os.path.expanduser("~/Desktop/구랜디스킨모음/99_공용_애니메이션/idle.fbx")

# 유니티 휴머노이드 필수 15뼈(UpperChest·Neck·어깨·발끝은 선택) — 이게 다 있어야 리타겟을 시도한다.
HUMAN_MIN = ["Hips", "Spine", "Head", "LeftArm", "LeftForeArm", "LeftHand",
             "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot",
             "RightUpLeg", "RightLeg", "RightFoot"]


def _import_idle():
    """공용 idle.fbx를 들여와 (아마추어, 새로 생긴 오브젝트들)을 준다. 파일이 없으면 (None, [])."""
    if not os.path.exists(IDLE):
        return None, []
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=IDLE)
    new_objs = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new_objs if o.type == "ARMATURE"), new_objs


def _snapshot(arm):
    """지금 프레임의 세계 변화량 Rd를 뼈마다 뽑아 둔다."""
    W = arm.matrix_world
    delta, hips_off = {}, None
    for pb in arm.pose.bones:
        rest_w = (W @ pb.bone.matrix_local).to_3x3()
        pose_w = (W @ pb.matrix).to_3x3()
        delta[pb.name] = pose_w @ rest_w.inverted()
        if pb.name.endswith("Hips"):
            hips_off = (W @ pb.matrix).translation - (W @ pb.bone.matrix_local).translation
    return {"delta": delta, "hips_off": hips_off, "뼈": len(delta)}


def calmest_frame(arm):
    """클립에서 **가장 얌전한 프레임**(뼈 회전량 합이 최소)을 고른다 — 손을 든 순간을 잡으면 비교가 안 된다."""
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    scn = bpy.context.scene
    best, best_fr = None, f0
    for fr in range(f0, f1 + 1, 5):
        scn.frame_set(fr)
        tot = sum(abs(pb.matrix_basis.to_quaternion().angle) for pb in arm.pose.bones)
        if best is None or tot < best:
            best, best_fr = tot, fr
    return best_fr, round(float(best), 3)


def load_idle(frames=None):
    """idle.fbx에서 자세를 읽어 둔다.

    frames 없음 → 가장 얌전한 프레임 하나를 dict로 준다(전시판용).
    frames = [비율…] 또는 [프레임 번호…] → 그 프레임들의 dict를 **차례대로 리스트**로 준다(검수표용).
    파일이 없으면 None.
    """
    arm, new_objs = _import_idle()
    if arm is None:
        return None
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    scn = bpy.context.scene
    out = None
    if frames is None:
        fr, tot = calmest_frame(arm)
        scn.frame_set(fr)
        out = dict(_snapshot(arm), frame=fr, 회전량=tot, 구간=(f0, f1))
    else:
        out = []
        for f in frames:
            fr = int(f0 + (f1 - f0) * f) if isinstance(f, float) and 0.0 <= f <= 1.0 else int(f)
            scn.frame_set(fr)
            out.append(dict(_snapshot(arm), frame=fr, 구간=(f0, f1)))
    for o in new_objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return out


def can_retarget(arm, idle):
    """이 뼈대가 공용 Idle을 받을 수 있나 — 사람 필수 15뼈가 다 있고 이름이 클립과 맞나."""
    if arm is None or idle is None:
        return False
    names = {b.name.split(":")[-1] for b in arm.data.bones}
    d = idle[0]["delta"] if isinstance(idle, list) else idle["delta"]
    return all(h in names for h in HUMAN_MIN) and any(b.name in d for b in arm.pose.bones)


def apply_idle(arm, idle):
    """읽어 둔 세계 변화량을 이 뼈대에 입힌다(부모부터 차례로). 옮겨 입힌 뼈 수를 준다."""
    W = arm.matrix_world
    Wi = W.inverted()
    order = sorted(arm.pose.bones, key=lambda pb: len(pb.parent_recursive))
    moved = 0
    for pb in order:
        Rd = idle["delta"].get(pb.name)
        if Rd is None:
            continue
        rest_w = W @ pb.bone.matrix_local
        rot_w = (Rd @ rest_w.to_3x3()).to_4x4()
        if pb.parent is not None:
            p_rest_w = W @ pb.parent.bone.matrix_local
            head_w = (W @ pb.parent.matrix) @ p_rest_w.inverted() @ rest_w.translation
        else:
            head_w = rest_w.translation + (idle["hips_off"] or Vector((0, 0, 0)))
        pb.matrix = Wi @ (Matrix.Translation(head_w) @ rot_w)
        bpy.context.view_layer.update()
        moved += 1
    return moved
