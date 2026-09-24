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
import re

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


def bone_map(arm, idle=None):
    """이 뼈대의 뼈 이름 → mixamorig 이름 표. 못 지으면 None.

    ① 이름이 이미 mixamorig면 그대로 쓴다(빠른 길).
    ② 아니면 structural_map으로 **계층을 보고** 짜 맞춘다(아래 주석 참고). 검사에 떨어지면 None.
    """
    if arm is None:
        return None
    suffix = {b.name.split(":")[-1]: b.name for b in arm.data.bones}
    if all(h in suffix for h in HUMAN_MIN):
        return {suffix[k]: k for k in suffix}
    return structural_map(arm)


def can_retarget(arm, idle):
    """이 뼈대가 공용 Idle을 받을 수 있나."""
    if arm is None or idle is None:
        return False
    return bone_map(arm, idle) is not None


def apply_idle(arm, idle, mapping=None):
    """읽어 둔 세계 변화량을 이 뼈대에 입힌다(부모부터 차례로). 옮겨 입힌 뼈 수를 준다.

    mapping을 주면 그 표(뼈 이름 → mixamorig 이름)로 찾는다 — 이름이 mixamorig가 아닌 리그용.
    안 주면 한 번 지어서 쓴다(프레임마다 부르면 낭비다 — 바깥에서 한 번 지어 넘길 것).
    """
    W = arm.matrix_world
    Wi = W.inverted()
    if mapping is None:
        mapping = bone_map(arm, idle) or {}
    order = sorted(arm.pose.bones, key=lambda pb: len(pb.parent_recursive))
    moved = 0
    for pb in order:
        canon = mapping.get(pb.name)
        Rd = idle["delta"].get("mixamorig:" + canon) if canon else None
        if Rd is None:
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


# ─────────────────────────────────────────────────────────────────────────────
# 뼈 이름이 mixamorig가 아닌 리그도 받아 주기(2026-09-24, PM 요청 — 전수 검사에서 10종이 통째로 빠졌다)
#
# 🔴 이름표를 잔뜩 만드는 길은 **안 간다.** 같은 낱말이 리그마다 다른 뼈를 가리키기 때문이다 —
#    MMD는 "Left leg"가 **넓적다리**인데 Mixamo는 "LeftLeg"가 **정강이**다. 이름만 보면 반드시 틀린다.
# ✅ 그래서 **이름은 네 곳에만 쓰고(엉덩이·머리·손·발) 나머지는 계층으로 채운다.**
#    이 넷은 어느 리그에서나 낱말이 겹치지 않는다. 그 사이의 사슬은 「엉덩이에서 발까지 가는 길의 끝 세 마디」처럼
#    구조가 정해 준다. 트위스트·보조 뼈가 끼어 있어도 **끝에서부터** 세면 안 밀린다.
# ✅ 그리고 **짜 맞춘 결과를 검사한다**(엉덩이 > 무릎 > 발목, 어깨 > 엉덩이, 왼손·오른손이 반대편).
#    검사에 떨어지면 숫자를 내지 않고 「매핑 못 함」으로 돌려준다 — 잘못 짜 맞춘 숫자는 없느니만 못하다.
STRIP_PREFIX = ("mixamorig:", "org-", "def-", "mch-", "vis-", "tweak_", "bip001", "b_")


def _norm(name):
    """뼈 이름을 견주기 좋게 — 소문자 · 접두어 제거 · 번호 꼬리 제거 · 글자와 숫자만.

    꼬리 숫자는 두 가지로 돌려준다: 구분자가 있는 것(`_112` · `.001`)과 **붙은 것**(`Foot01`).
    붙은 숫자까지 늘 떼면 `Spine1`과 `Spine`이 같아져 버리므로, **둘 다** 내고 부르는 쪽에서 견준다.
    """
    s = name.lower()
    for p in STRIP_PREFIX:
        if s.startswith(p):
            s = s[len(p):]
    s = re.sub(r"[_.]\d+$", "", s)          # _112 · .001 같은 꼬리
    s = re.sub(r"[^a-z0-9]", "", s)
    return s, re.sub(r"\d+$", "", s)        # (그대로, 붙은 숫자까지 뗀 것)


HIPS_T = {"hips", "hip", "pelvis", "cog", "bodypelvis", "root hips", "roothips"}
HEAD_T = {"head", "headface", "face"}
HAND_T = {"lhand", "rhand", "handl", "handr", "lefthand", "righthand",
          "lwrist", "rwrist", "wristl", "wristr", "leftwrist", "rightwrist",
          "lhandpalm", "rhandpalm", "handpalml", "handpalmr"}
FOOT_T = {"lfoot", "rfoot", "footl", "footr", "leftfoot", "rightfoot",
          "lankle", "rankle", "anklel", "ankler", "leftankle", "rightankle",
          "lfootheel", "rfootheel", "footheell", "footheelr"}


def _chain(bone, upto):
    """upto(제외)에서 bone(포함)까지 부모를 거슬러 올라간 길. 닿지 않으면 None."""
    out = []
    b = bone
    while b is not None and b != upto:
        out.append(b)
        b = b.parent
    return list(reversed(out)) if b == upto else None


def structural_map(arm):
    """이 뼈대의 뼈 이름 → mixamorig 이름 표를 짓는다. 못 지으면 None.

    ⚠️ 이름은 엉덩이·머리·손·발에만 쓴다. 나머지는 「그 사이를 잇는 길」의 끝에서부터 센다.
    """
    bones = list(arm.data.bones)
    W = {b.name: (arm.matrix_world @ b.head_local) for b in bones}
    # 🔴 **살이 붙은 뼈를 먼저 고른다.** Rigify는 같은 이름이 DEF-·ORG-·MCH-·컨트롤로 네댓 벌 있고,
    #    컨트롤 쪽을 잡으면 엉덩이까지 가는 길이 아예 없어 매핑이 통째로 실패한다(안흔함_김수빈).
    skinned = {g.name for o in bpy.data.objects if o.type == "MESH"
               for m2 in o.modifiers if m2.type == "ARMATURE" and m2.object == arm
               for g in o.vertex_groups}

    def picks(tokens, side=None):
        """뜻이 맞는 뼈 후보를 **여럿** 준다(살 붙은 것 먼저, 그 안에서 이름 짧은 것 먼저)."""
        c = [b for b in bones if any(x in tokens for x in _norm(b.name))]
        if side is not None:
            c = [b for b in c if (W[b.name].x > 0) == (side == "Left")]
        return sorted(c, key=lambda b: (b.name not in skinned, len(b.name)))

    def pick(tokens, side=None):
        c = picks(tokens, side)
        return c[0] if c else None

    # 🔴 엉덩이는 **이름으로 고르지 않는다.** 네 팔다리 끝(양손·양발)의 **가장 깊은 공통 조상**이 엉덩이다.
    #    이름으로 골랐더니 두 번 틀렸다: 특별함_최상호는 살이 붙은 `pelvis_38`을 집었는데 팔이 거기 안 매달려 있었고,
    #    안흔함_김수빈(Rigify)은 `hips_112`가 컨트롤 쪽이라 변형 뼈(DEF-)에서 아예 길이 안 닿았다.
    #    공통 조상은 이름 규칙과 무관하게 언제나 맞다. 그 위에 「엉덩이」라는 이름이 더 깊이 있으면 그걸 쓴다.
    ends = []
    for side in ("Left", "Right"):
        for T in (HAND_T, FOOT_T):
            c = picks(T, side)
            if not c:
                return None
            ends.append(c)

    def anc(b):
        out, x = [], b
        while x is not None:
            out.append(x)
            x = x.parent
        return out

    hips = None
    for combo in ([g[0] for g in ends],):                            # 후보 첫 줄(살 붙은 것 우선)로 잡는다
        sets = [set(anc(b)) for b in combo]
        common = set.intersection(*sets)
        if common:
            hips = max(common, key=lambda b: len(anc(b)))
    if hips is None:
        return None
    # 「엉덩이」라는 이름이 공통 조상보다 **더 깊은 곳에** 있고 **네 끝 모두의 조상**이면 그걸 쓴다.
    #   ⚠️ 네 끝 모두를 따지는 게 중요하다 — 특별함_최상호는 `pelvis_38`이 다리 쪽에만 있고 팔은 안 매달려 있었다.
    allanc = set.intersection(*[set(anc(g[0])) for g in ends])
    named = [b for b in allanc if any(x in HIPS_T for x in _norm(b.name))]
    if named:
        hips = max(named, key=lambda b: len(anc(b)))

    head = next((b for b in picks(HEAD_T) if _chain(b, hips)), None)
    if head is None:
        return None
    m = {hips.name: "Hips", head.name: "Head"}

    spine = _chain(head, hips)
    if not spine or len(spine) < 2:
        return None
    neck = spine[-2]
    m[neck.name] = "Neck"
    mid = spine[:-2]                                                 # 엉덩이와 목 사이 = 척추
    for i, nm in zip((0, len(mid) // 2, len(mid) - 1), ("Spine", "Spine1", "Spine2")):
        if 0 <= i < len(mid):
            m.setdefault(mid[i].name, nm)

    for side in ("Left", "Right"):
        # 후보를 차례로 시험해 **엉덩이까지 길이 닿는 것**을 쓴다(Rigify 컨트롤 뼈는 안 닿는다)
        arm_c = next((c for b in picks(HAND_T, side) for c in [_chain(b, hips)] if c and len(c) >= 3), None)
        leg_c = next((c for b in picks(FOOT_T, side) for c in [_chain(b, hips)] if c and len(c) >= 3), None)
        if arm_c is None or leg_c is None:
            return None
        foot = leg_c[-1]
        for b, nm in zip(arm_c[-3:], (side + "Arm", side + "ForeArm", side + "Hand")):
            m[b.name] = nm
        if len(arm_c) >= 4:
            m.setdefault(arm_c[-4].name, side + "Shoulder")
        for b, nm in zip(leg_c[-3:], (side + "UpLeg", side + "Leg", side + "Foot")):
            m[b.name] = nm
        toes = [c for c in foot.children if "toe" in _norm(c.name)[0]]
        if toes:
            m.setdefault(min(toes, key=lambda b: len(b.name)).name, side + "ToeBase")

    inv = {v: k for k, v in m.items()}
    if any(b not in inv for b in HUMAN_MIN):
        return None
    z = lambda nm: W[inv[nm]].z                                      # noqa: E731
    H = max(W[b.name].z for b in bones) - min(W[b.name].z for b in bones)
    if H <= 1e-6:
        return None
    # 🔴 **「말이 되는 자리에 있는가」까지 봐야 한다.** 여기까지만 하고 넘겼더니 안흔함_김수빈(Rigify)이
    #   넓적다리 자리에 **정강이**를, 위팔 자리에 **아래팔**을 넣은 채 검사를 통과했다(한 마디씩 밀린 표).
    #   높이 순서만으로는 한 마디 밀린 것을 못 잡는다 — **사슬의 길이**로 잡는다:
    #   넓적다리~발은 키의 30%가 넘고(멀쩡한 유닛은 0.50~0.56), 위팔~손은 20%가 넘는다(0.30~0.33).
    #   한 마디 밀리면 확 짧아진다 — 김수빈은 다리 0.162 · 팔 0.092로 걸린다. 문턱은 팔 짧은 유닛(이정범 0.232)이
    #   억울하게 안 걸리도록 넉넉히 잡았다.
    ok = (z("Hips") > z("LeftLeg") > z("LeftFoot") and z("Hips") > z("RightLeg") > z("RightFoot")
          and (z("LeftArm") + z("RightArm")) / 2 > z("Hips")
          and W[inv["LeftHand"]].x > 0 > W[inv["RightHand"]].x
          and z("Hips") - z("LeftUpLeg") < 0.15 * H and z("Hips") - z("RightUpLeg") < 0.15 * H
          and (z("LeftUpLeg") - z("LeftFoot")) > 0.30 * H and (z("RightUpLeg") - z("RightFoot")) > 0.30 * H
          and (W[inv["LeftArm"]] - W[inv["LeftHand"]]).length > 0.20 * H
          and (W[inv["RightArm"]] - W[inv["RightHand"]]).length > 0.20 * H)
    return m if ok else None
