"""3ds Max Biped 뼈대(이름 "Bip001 ...")가 이미 있는 정적 glb를 mixamorig 사람형 FBX로 짓는다.
(gen_rigify_skin.py와 같은 계열 원칙 — 이미 있는 스킨을 mixamorig 이름으로 고쳐 쓴다. 다만
소스 뼈 이름 규칙이 Biped라 rename 표가 다르고, 트위스트 뼈(팔다리 비틀림 보조뼈)가 본체
뼈와 가중치를 나눠 갖는 게 큰 차이 — 본체로 합쳐야 한다. 2026-09-17 첫 건 전설적인_이유선.)

  blender -b --factory-startup --python Tools/blender/gen_biped_skin.py -- 전설적인_이유선 [--out DIR] [--render DIR]

결과: <유닛>.fbx + Textures/ · 키 1.8m · 발 z 0 · 정면 −Y · mixamorig 22뼈 · T자 쉬는 자세.

blender 세션의 Tools/blender/fix_unit_fbx.py에 있는 biped_rename()·biped_tpose_names()를
참고해 이름 대응표를 옮겨 왔다(그 파일 자체는 안 건드림, 이 파일에 내 방식대로 재구현).
"""
import json
import math
import os
import re
import subprocess
import sys
import zipfile

import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PREFIX = "mixamorig:"
_FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")

def biped_rename_table(prefix, fingers=5, joints=3):
    """3ds Biped 이름("{prefix} Pelvis" 등) → mixamorig. fix_unit_fbx.py의 biped_rename()과
    이름 대응은 같지만(그 파일은 안 건드린다는 원칙 — 재구현), 🔴 손가락은 다르게 접는다:
    이 파일은 22뼈로 뼈대를 완전히 새로 짓기 때문에(손가락 뼈 자체가 없다) 손가락을 개별
    LeftHandIndex1 같은 대상으로 매핑하면 그 이름의 실제 뼈가 없어 FBX 내보내기의
    use_armature_deform_only=True가 그 정점그룹을 통째로 버린다(실측 발견: 타시기 손가락
    정점 1,504개가 무가중치로 나갔다 — A자 자세 그대로 얼어붙어 있었다, 손 뼈만 T자로 돌고
    손가락은 안 따라와서 위치로 들통났다). fix_unit_fbx.py의 biped_rename()은 원본 뼈를 전부
    그대로 남기고 이름만 바꾸는 다른 용도라 이 문제가 없다 — 여기서는 손가락을 전부
    "{side}Hand"로 접어 22뼈 안에서 실제로 존재하는 뼈로 보낸다."""
    t = {f"{prefix} Pelvis": "Hips", f"{prefix} Spine": "Spine", f"{prefix} Spine1": "Spine1",
         f"{prefix} Spine2": "Spine2", f"{prefix} Neck": "Neck", f"{prefix} Head": "Head"}
    for s, side in (("L", "Left"), ("R", "Right")):
        t.update({f"{prefix} {s} Clavicle": f"{side}Shoulder", f"{prefix} {s} UpperArm": f"{side}Arm",
                  f"{prefix} {s} Forearm": f"{side}ForeArm", f"{prefix} {s} Hand": f"{side}Hand",
                  f"{prefix} {s} Thigh": f"{side}UpLeg", f"{prefix} {s} Calf": f"{side}Leg",
                  f"{prefix} {s} Foot": f"{side}Foot", f"{prefix} {s} Toe0": f"{side}ToeBase"})
        for i, finger in enumerate(_FINGERS[:fingers]):
            for j, k in (("", 1), ("1", 2), ("2", 3))[:joints]:
                t[f"{prefix} {s} Finger{i}{j}"] = f"{side}Hand"
    return t


SKINS = {
    # 전설적인_이유선(타시기, One Piece) — PM 사양 원문 보존(2026-09-17, 두 번 유실 전례가
    # 있어 시작 전에 적어 둔다):
    # 원본: source/Tashigi.zip 안 Tashigi.gltf+.bin + 12014_{Body,Cloak,Hair,Face,Weapon}_BC.png.
    # 아마추어 1·뼈 156·3ds Biped(Bip001 Pelvis·Spine/Spine1/Spine2·Neck·Head·L/R Clavicle/
    # UpperArm/Forearm/Hand·손가락 5×3(Finger0~4, "","1","2")·L/R Thigh/Calf/Foot/Toe0).
    # 🔴 트위스트 뼈(LThighTwist·LCalfTwist·LCalfTwist1·L ForeTwist(+R) 등) — 팔다리 본체와
    # 가중치를 나눠 가짐(직접 확인: LCalfTwist·L ForeTwist 자체는 0, "1" 붙은 보조가 대신 들고
    # 있음) → 본체 뼈로 합쳐야 유실 없음. Head 직계 자식 5: Bone Hair 01/03/05·Bone YanJing·
    # Facebone(그 밑에 Facebone01~51 대형 서브트리, 직접 확인 38개 자식) → Head로 병합. 뿌리
    # "12014 C"→Bip001, 둘 다 가중치 0(직접 확인) → Hips로 접어 없앤다.
    # 메시 6(5_body 6,753·5_face 3,130·5_hair 2,927·5_+cloak 5,994·5_+Snail 1,377·7_glass 96)
    # + Icosphere(조명 구, 드롭) · 삼각형 31,360(감량 불필요) · 좌우 대칭. "+" 접두 = 게임에서
    # 켜진 기본 변형 → 남김(사스케 "-"와 반대). 칼 텍스처(Weapon_BC)는 있지만 칼 메시가 없어
    # 안 씀. 전 메시 UV 2층 — UV0(실제 이름 "UVMap", 직접 확인 범위 0.99×0.99 정상)가 진짜,
    # UV1("UVMap.001")은 범위가 1.9×2.0으로 비정상(라이트맵 종류로 추정) → 버림.
    # 🔴 재질 기본색이 전부 TEX_IMAGE×VertexColor MIX 구조인데, 전 메시 정점색이 (1,1,1,1)
    # 흰색 중립(직접 확인, 6개 메시 전부 샘플 20곳 전부 동일) → 굽기 없이 텍스처를 Base Color에
    # 직결(정점색·Mix 노드 우회).
    # 자세: L UpperArm z 1.389→L Hand z 1.065, x 0.115→0.396(직접 확인 일치) = A자 약 49° →
    # level_arms로 T자. 규격 동일: 키 1.8·발 z 0·정면 −Y·T자·mixamorig 이름·무가중치 정점 0.
    # 전설적인_신문철(나루토 선인모드, 프리파이어 콜라보) — PM 사양 원문 보존(2026-09-17):
    # ※ 신문철 동명 셋(안흔함 모델 있음). 이건 전설적인_신문철.
    # 원본: source/Male_Lobby_Cos_NB_Sage.zip 안 "naruto mode sabio.fbx" + 텍스처 5장(_D만 씀,
    # _SD·_I는 안 씀). 완전한 캐릭터(얼굴·손·발이 Accessory·Top 메시 안에 들어 있음). 붉은
    # 망토·등 두루마리·이마 보호대·주황 바지·샌들. A자 자세.
    # 🔴 뼈 이름이 실제로는 "Bip01 X"(3ds Biped)가 아니라 "bone_X"(프리파이어 자체 규칙,
    # 직접 확인) — biped_rename_table 대신 cfg["rename"] 직접 사용.
    # 뼈 126: bone_Hips→Hips·bone_Spine→Spine·bone_Spine1→Spine2(척추 2마디뿐, 죠나단과 같은
    # 자리표시 처리)·bone_Neck→Neck·bone_Head→Head·bone_LeftClav→Shoulder·bone_LeftArm→Arm·
    # bone_LeftForeArm→ForeArm·bone_LeftHand→Hand·bone_LeftLegUpper→UpLeg·bone_LeftLeg→Leg·
    # bone_LeftAnkle→Foot·bone_LeftToe→ToeBase(R 동일).
    # 손가락 Finger01~42(5개×2마디) → {side}Hand로 접기(타시기 교훈, Rigify 이름이 아니라서
    # finger_prefixes 대신 각 첫마디를 fold_subtree 뿌리로).
    # 망토 밑단 흔들뼈 Hemline_..._Bone001~040(Dummy001 밑 8갈래 링, 직접 확인 좌표로 앞/뒤
    # 판정) → 앞자락(001·006·036)은 UpLeg 좌우, 뒤·옆(011·016·021·026·031)은 Hips.
    # 머리카락 Hair_..._Bone001~010(Dummy001 밑 2갈래) → Head.
    # 무기·배낭 더미(Left/Right_Spine_Backpack·Spine_Weapon, Left/Right_Weapon) — 메시 없음,
    # 가중치 0 추정 → Spine2/Hand로 접어 안전하게 처리.
    "전설적인_신문철": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_신문철.zip",
        glb_member="source/Male_Lobby_Cos_NB_Sage.zip",
        inner_gltf="Male_Lobby_Cos_NB_Sage/naruto mode sabio.fbx",
        source_format="fbx",
        path="Assets/Art/Units/전설적인_신문철/전설적인_신문철.fbx",
        mesh_name="Naruto",
        height=1.8,
        rename={
            "bone_Hips": "Hips", "bone_Spine": "Spine", "bone_Spine1": "Spine2",
            "bone_Neck": "Neck", "bone_Head": "Head",
            "bone_LeftClav": "LeftShoulder", "bone_LeftArm": "LeftArm", "bone_LeftForeArm": "LeftForeArm", "bone_LeftHand": "LeftHand",
            "bone_RightClav": "RightShoulder", "bone_RightArm": "RightArm", "bone_RightForeArm": "RightForeArm", "bone_RightHand": "RightHand",
            "bone_LeftLegUpper": "LeftUpLeg", "bone_LeftLeg": "LeftLeg", "bone_LeftAnkle": "LeftFoot", "bone_LeftToe": "LeftToeBase",
            "bone_RightLegUpper": "RightUpLeg", "bone_RightLeg": "RightLeg", "bone_RightAnkle": "RightFoot", "bone_RightToe": "RightToeBase",
        },
        fold_subtree={
            # 앞자락(y<0, 직접 확인 좌표) → 그쪽 UpLeg. 뒤·옆 → Hips.
            "Hemline_NB_Sage_Cos_Bone001": "LeftUpLeg", "Hemline_NB_Sage_Cos_Bone006": "LeftUpLeg",
            "Hemline_NB_Sage_Cos_Bone031": "RightUpLeg", "Hemline_NB_Sage_Cos_Bone036": "RightUpLeg",
            "Hemline_NB_Sage_Cos_Bone011": "Hips", "Hemline_NB_Sage_Cos_Bone016": "Hips",
            "Hemline_NB_Sage_Cos_Bone021": "Hips", "Hemline_NB_Sage_Cos_Bone026": "Hips",
            "bone_Hips_Dummy": "Hips",                            # Dummy001 자신 + 못 걸린 나머지
            "Hair_NB_Sage_Cos_Dummy001": "Head",
            "bone_Left_Finger01": "LeftHand", "bone_Left_Finger11": "LeftHand", "bone_Left_Finger21": "LeftHand",
            "bone_Left_Finger31": "LeftHand", "bone_Left_Finger41": "LeftHand",
            "bone_Right_Finger01": "RightHand", "bone_Right_Finger11": "RightHand", "bone_Right_Finger21": "RightHand",
            "bone_Right_Finger31": "RightHand", "bone_Right_Finger41": "RightHand",
            "bone_Left_Weapon": "LeftHand", "bone_Right_Weapon": "RightHand",
            "bone_Left_Spine_Backpack": "Spine2", "bone_Left_Spine_Weapon": "Spine2", "bone_Right_Spine_Weapon": "Spine2",
        },
        fold={"bone_LeftToe_end": "LeftToeBase", "bone_RightToe_end": "RightToeBase"},
        bone_position_override={"Spine1": ("bone_Spine", "tail")},
        allow_dead_bones={"Spine1"},
        materials={n: ("texture_direct", None) for n in (
            "Male_Shoe_Cos_NB_Sage", "Male_Accessory_Cos_NB_Sage", "Male_Top_Cos_NB_Sage",
            "Male_Bottom_Cos_NB_Sage", "Male_Hair_Cos_NB_Sage", "Male_Top_Cos_NB_Yellow",
        )},
        # 🔴 PM 지시(유니티 반려 후, fix_unit_fbx.py tpose_arms 방식 참고해 재구현) — 이전
        # pb.tail 기반 계산이 world_matrix 없이 섞여 이 리그에서 어긋났다(178.5°짜리 헛돈
        # 회전). level_arms를 world 벡터(head→다음 관절 head, arm.matrix_world 곱) 기반으로
        # 다시 짜서 해결.
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 전설적인_이현주(히나타, 그분의애마) — PM 사양 원문 보존(2026-09-17):
    # Free Fire 립이지만 뼈는 이미 mixamorig 78개 완비(비-mixamo 뼈 0, 직접 확인 일치) —
    # 접두만 떼고 손가락·발끝 여분(Thumb/Index/Middle/Ring/Pinky 1~4·HeadTop_End·Toe_End,
    # 전부 "_end" 잎 포함)을 22뼈로 접으면 된다.
    # 🔴 아마추어 오브젝트 자체가 배율 0.01·X −90°(cm 단위 스캔 소스, 직접 확인) — 위 build()
    # 의 old_head_tail이 arm_obj.matrix_world를 곱하도록 고쳐 대응(이 유닛 때문에 발견한
    # 일반 버그, 다른 유닛엔 영향 없음: 그쪽은 이미 항등행렬이었다).
    # 메시 5개(Mesh_0026·7477·7485·7488·7494), 전부 무가중치 정점 0(PM 확인). 삼각형 약
    # 1.46만 — 감량 불필요. 재질 4개(Material.009~012) 텍스처가 소스 안에서 이미 깨진 경로
    # (공백 이름 "bottom hhhh.tga")를 가리켜서 zip의 실제 파일(밑줄 이름 .png)로 재연결.
    # 손 높이(3.94)가 엉덩이(3.88)와 같고 손 X ±1.44 → A자에 가까움 — level_arms로 편다.
    # 🔴 rebuild="금지"(2026-09-24) — **커밋본을 만든 건 fix_unit_fbx.py다.** 같은 원본(hinata skin.fbx)을
    #    쓰지만 나오는 물건이 다르다: 커밋본 메시 5·뼈 52 ↔ 이 설정 메시 1·뼈 22(정점·면은 9,770/14,360으로 같다).
    #    **정점이 같다고 같은 파일이 아니다** — 메시와 뼈를 같이 볼 것.
    "전설적인_이현주": dict(
        rebuild="금지",
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_이현주.zip",
        glb_member="source/hinata skin.fbx",
        source_format="fbx",
        top_texture_member="textures/top_hhh.png",
        bottom_texture_member="textures/bottom_hhhh.png",
        head_texture_member="textures/head_hhhhh.png",
        path="Assets/Art/Units/전설적인_이현주/전설적인_이현주.fbx",
        mesh_name="Hinata",
        height=1.8,
        rename={f"mixamorig:{n}": n for n in (
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
            "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        )},
        fold_subtree={
            "mixamorig:HeadTop_End": "Head",
            "mixamorig:LeftToe_End": "LeftToeBase", "mixamorig:RightToe_End": "RightToeBase",
            "mixamorig:LeftHandThumb1": "LeftHand", "mixamorig:LeftHandIndex1": "LeftHand",
            "mixamorig:LeftHandMiddle1": "LeftHand", "mixamorig:LeftHandRing1": "LeftHand", "mixamorig:LeftHandPinky1": "LeftHand",
            "mixamorig:RightHandThumb1": "RightHand", "mixamorig:RightHandIndex1": "RightHand",
            "mixamorig:RightHandMiddle1": "RightHand", "mixamorig:RightHandRing1": "RightHand", "mixamorig:RightHandPinky1": "RightHand",
        },
        materials={
            "Material.009": ("texture_file", "bottom_texture_member"),
            "Material.010": ("texture_file", "head_texture_member"),
            "Material.011": ("texture_file", "top_texture_member"),
            "Material.012": ("texture_file", "bottom_texture_member"),
        },
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 전설적인_구주호(채드 거인의 오른팔)·전설적인_박성호(간쥬)는 blender 세션으로 옮김
    # (2026-09-17, PM 지시 — blender 큐가 비어서). 여기 항목 없음, 되살리지 말 것.
    # 제한_박성호(퍼펙트 셀, Dragon Ball Sparking Zero) — PM 사양 원문 보존(2026-09-17):
    # ※ 박성호 동명(전설적인_박성호=간쥬)와 다름, 제한_ 접두 필수.
    # 원본: source/Cell Perfect.zip 안 Cell Perfect.gltf+.bin + 텍스처 9장(zip textures/).
    # 스킨 1(Armature) 뼈 239: root·pelvis·spine 01~03·neck 01·head·head end + 얼굴 fb*
    # 수백(facialbase 밑 서브트리) → 전부 Head. 팔다리는 UE 규칙(공백 포함, "upperarm l"류).
    # 메시 14: OUTLINE 메시 4개(7_0001·0005·0007·0009, 외곽선용 뒤집힌 껍데기) 전부 뺌.
    # 본체 7_0000(9,327 상체)·7_0002(4,200, 날개 포함)·7_0004(2,257 머리)·7_0006(1,362 머리
    # 볏)·7_0008(3,248 하체)·7_0014(1,206 얼굴 부분)·눈 4(EyeLB/LW/RB/RW 62씩) 유지.
    # 재질 14개 전부 BLEND → OPAQUE로. 텍스처: *_di(색) 사용, *_li(조명 마스크) 무시.
    # 키 약 2.29(머리 볏 포함) · y-up(임포트 시 자동 Z-up 변환됨) · 날개가 등 뒤로 뻗음
    # (원작 신체라 유지).
    "제한_박성호": dict(
        source="~/Desktop/구랜디스킨모음/07_제한됨/제한_박성호.zip",
        glb_member="source/Cell Perfect.zip",
        inner_gltf="Cell Perfect.gltf",
        path="Assets/Art/Units/제한_박성호/제한_박성호.fbx",
        mesh_name="Cell",
        height=1.8,
        drop_meshes={"Icosphere", "7_0001-OUTLINE_0.1_16_16", "7_0005-OUTLINE_0.1_16_16",
                     "7_0007-OUTLINE_0.1_16_16", "7_0009-OUTLINE_0.1_16_16"},
        rename={
            "pelvis": "Hips", "spine 01": "Spine", "spine 02": "Spine1", "spine 03": "Spine2",
            "neck 01": "Neck", "head": "Head",
            "clavicle l": "LeftShoulder", "upperarm l": "LeftArm", "lowerarm l": "LeftForeArm", "hand l": "LeftHand",
            "clavicle r": "RightShoulder", "upperarm r": "RightArm", "lowerarm r": "RightForeArm", "hand r": "RightHand",
            "thigh l": "LeftUpLeg", "calf l": "LeftLeg", "foot l": "LeftFoot", "ball l": "LeftToeBase",
            "thigh r": "RightUpLeg", "calf r": "RightLeg", "foot r": "RightFoot", "ball r": "RightToeBase",
        },
        fold={
            "root": "Hips", "AttachPoint": "Hips", "AttachPoint l": "LeftHand", "AttachPoint r": "RightHand",
            "head end": "Head", "head eyeball right": "Head", "head eyeball left": "Head",
            "elbow l": "LeftForeArm", "elbow r": "RightForeArm",
            "lowerarm twist 01 l": "LeftForeArm", "lowerarm twist 02 l": "LeftForeArm", "lowerarm twist 03 l": "LeftForeArm",
            "upperarm twist 01 l": "LeftArm", "upperarm twist 02 l": "LeftArm", "upperarm twist 03 l": "LeftArm",
            "lowerarm twist 01 r": "RightForeArm", "lowerarm twist 02 r": "RightForeArm", "lowerarm twist 03 r": "RightForeArm",
            "upperarm twist 01 r": "RightArm", "upperarm twist 02 r": "RightArm", "upperarm twist 03 r": "RightArm",
            "sp3tail 01": "Spine2", "sp3tail 01 end": "Spine2",
            "knee l": "LeftLeg", "knee r": "RightLeg",
            "calf twist 01 l": "LeftLeg", "calf twist 02 l": "LeftLeg", "calf twist 03 l": "LeftLeg",
            "thigh twist 01 l": "LeftUpLeg", "thigh twist 02 l": "LeftUpLeg", "thigh twist 03 l": "LeftUpLeg",
            "calf twist 01 r": "RightLeg", "calf twist 02 r": "RightLeg", "calf twist 03 r": "RightLeg",
            "thigh twist 01 r": "RightUpLeg", "thigh twist 02 r": "RightUpLeg", "thigh twist 03 r": "RightUpLeg",
            "ball end l": "LeftToeBase", "ball end r": "RightToeBase",
        },
        fold_subtree={
            "facialbase": "Head",
            "middle 01 l": "LeftHand", "thumb 01 l": "LeftHand", "index 01 l": "LeftHand", "ring 01 l": "LeftHand", "pinky 01 l": "LeftHand",
            "middle 01 r": "RightHand", "thumb 01 r": "RightHand", "index 01 r": "RightHand", "ring 01 r": "RightHand", "pinky 01 r": "RightHand",
            # 날개는 원작 신체라 유지(PM 지시) — 등뼈(Spine2)로 접어 가중치를 살린다.
            "wing 01 l": "Spine2", "wing 01 r": "Spine2",
        },
        materials={n: ("texture_direct", None) for n in (
            "7_0000-Cpl034p3c01s1d1_0.1_16_16", "7_0002-Cpl034p3c01s1d1_0.1_16_16",
            "7_0004-Cpl034p3c01s1d1_0.1_16_16", "7_0006-Cpl034p3c01s1d1_0.1_16_16",
            "7_0008-Cpl034p3c01s1d1_0.1_16_16", "7_0014-Cpl034p3c01s1d1_0.1_16_16",
            "7_EyeLB_0.1_16_16", "7_EyeLW_0.1_16_16", "7_EyeRB_0.1_16_16", "7_EyeRW_0.1_16_16",
        )},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 초월_노태현_AP(쿠로츠치 마유리, Bleach Mobile) — PM 사양 원문 보존(2026-09-18):
    # 원본: source/cha_mayuri.fbx + textures/cha_mayuri.png·cha_mayuri_wp.png.
    # 메시 2 · 무가중치 0 · UV 'Attribute' 1층. cha_mayuri_0(4,755, 몸) 유지 ·
    # cha_mayuri_wp_0(1,745, 무기 아시소기 지조) → 뺌 + 뼈 weapon_01·weapon_02·weapon_case·
    # cha_mayuri_wp.
    # 뼈 56(직접 확인 일치): 뿌리 cha_mayuri→Bip01·손가락 Finger0~2×2(→Hand)·Bone_eyes·
    # Bone_mouth·Point_eye_L/R(→Head)·Bone_arm_L/R_01~02(팔 보조, 부모 Forearm이라 ForeArm으로)·
    # Bone_cloth_L/R_01~04(옷자락, 부모 Pelvis라 Hips로)·끝 cha_mayuri.001 제거.
    # Spine·Spine1만(Spine2 없음, 타시기와 같은 패턴 — 여긴 Clavicle이 Neck에 붙음).
    "초월_노태현_AP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_노태현_AP.zip",
        glb_member="source/cha_mayuri.fbx",
        source_format="fbx",
        tex_member="textures/cha_mayuri.png",
        path="Assets/Art/Units/초월_노태현_AP/초월_노태현_AP.fbx",
        mesh_name="Mayuri",
        height=1.8,
        biped_prefix="Bip01",
        drop_meshes={"cha_mayuri_wp_0"},
        # 🔴 Spine2가 없는 소스에서 biped_rename_table의 기본 "{prefix} Spine2" 항목(실존하지
        # 않는 이름)을 그대로 두면, Spine1을 Spine2로 덮어써도 뒤집기(reverse)에서 삽입 순서
        # 때문에 없는 이름이 다시 이긴다(직접 겪음 — KeyError 'Bip01 Spine2'). 그 항목을
        # 빼고 Spine1만 Spine2로 보낸다.
        rename={k: v for k, v in dict(biped_rename_table("Bip01"), **{"Bip01 Spine1": "Spine2"}).items()
                if k != "Bip01 Spine2"},
        fold={
            "cha_mayuri": "Hips", "Bip01": "Hips", "cha_mayuri.001": "Hips", "cha_mayuri_wp": "Hips",
            "weapon_01": "Hips", "weapon_02": "RightHand", "weapon_case": "Hips",
            "Bone_eyes": "Head", "Bone_mouth": "Head", "Point_eye_L": "Head", "Point_eye_R": "Head",
        },
        fold_subtree={
            "Bone_arm_L_01": "LeftForeArm", "Bone_arm_R_01": "RightForeArm",
            "Bone_cloth_L_01": "Hips", "Bone_cloth_L_03": "Hips",
            "Bone_cloth_R_01": "Hips", "Bone_cloth_R_03": "Hips",
        },
        bone_position_override={"Spine1": ("Bip01 Spine", "tail")},
        allow_dead_bones={"Spine1"},
        # 🔴 실측 발견 — FBX가 참조하는 이미지 경로가 소스 폴더(source/) 바로 밑을 가리키는데
        # 실제 파일은 zip의 textures/ 안에 있다(크기 (0,0), 못 찾음) — texture_direct(기존
        # 참조 재사용)로는 안 되고, texture_file로 실제 위치에서 새로 불러와야 한다.
        materials={"cha_mayuri": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 초월_양재모_AD(몽키 D. 가프, OPFP XPS 12002) — PM 사양 원문 보존(2026-09-18):
    # 원본: source/*.rar(bsdtar) 안 "OPFP - Monkey D. Garp/12002.fbx" + zip textures/
    # 12002_D.png·12002B_D.png. 아마추어 "12002" 뼈 141(직접 확인 일치) · 메시 3(0_Face
    # 3,630→12002_Face·Body 4,685→12002_Body·Body_a 2,443→12002_B 어깨 걸친 해군 코트 추정)
    # 전부 유지, 무가중치 0.
    # Bip001 계열(타시기와 같은 패턴, Clavicle이 Neck에 붙어 Spine2 없음) — biped_rename_table
    # + Spine1→Spine2 재배정. Facebone(68개 서브트리, Head 자식)→Head.
    # 코트 자락 뼈 3갈래(Bone006·Bone015 — L Clavicle 자식 2뿌리, Bone001·Bone018 — R Clavicle
    # 자식 2뿌리, Bone000 — Spine1 자식 1뿌리) → 어깨·척추로 접어 날개처럼 안 뜨게(PM·유기 교훈).
    # 🔴 rebuild="금지"(2026-09-24) — **다른 사람이 들어간다.** 2026-09-22 사장님 지시로 가프↔아카이누를
    #    맞바꿨고(fix_unit_fbx.py 1289행), 커밋본은 아카이누(pl_akainu_gens01)다. 이 설정은 **맞바꾸기 전 가프**를
    #    가리킨다. 게다가 오늘 돌려 보니 rar가 안 풀려 CalledProcessError로 죽는다 — 그런데 **옛 관문은 OK로 찍었다**
    #    (예외 이름 목록에 CalledProcessError가 없었다). 관문은 이제 산출 파일 존재로 판정한다.
    "초월_양재모_AD": dict(
        rebuild="금지",
        source="~/Desktop/구랜디스킨모음/08_초월/초월_양재모_AD.zip",
        glb_member="source/opfp___monkey_d__garp_xps_fbx_by_o_dv89_o_dfocdty.rar",
        inner_gltf="OPFP - Monkey D. Garp/12002.fbx",
        source_format="fbx",
        body_tex_member="textures/12002_D.png",
        coat_tex_member="textures/12002B_D.png",
        path="Assets/Art/Units/초월_양재모_AD/초월_양재모_AD.fbx",
        mesh_name="Garp",
        height=1.8,
        biped_prefix="Bip001",
        rename={k: v for k, v in dict(biped_rename_table("Bip001"), **{"Bip001 Spine1": "Spine2"}).items()
                if k != "Bip001 Spine2"},
        fold={"Bip001": "Hips"},
        fold_subtree={
            "Facebone": "Head",
            "Bone006": "LeftShoulder", "Bone015": "LeftShoulder",
            "Bone001": "RightShoulder", "Bone018": "RightShoulder",
            "Bone000": "Spine2",
        },
        bone_position_override={"Spine1": ("Bip001 Spine", "tail")},
        allow_dead_bones={"Spine1"},
        materials={
            "12002_Body": ("texture_file", "body_tex_member"),
            "12002_Face": ("texture_file", "body_tex_member"),
            "12002_B": ("texture_file", "coat_tex_member"),
        },
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 초월_강재규_AP(토니토니 쵸파 몬스터 포인트, OPFP XPS 11306_U) — PM 사양 원문 보존
    # (2026-09-18): 가프·타시기와 같은 설정 재사용.
    # 원본: source/*.rar(bsdtar) 안 「OPFP - Chopper (Monster Point)/11306_U.fbx」 +
    # zip textures/11306_D.png.
    # 아마추어 11306_U 뼈 43(직접 확인 일치): Bip001 + 손가락 Finger0~4×2(→Hand, 2마디뿐—
    # 가프의 3마디보다 짧음) · Bone001(Head 자식, 뿔 장식 추정) · Clavicle이 Neck에 붙어
    # Spine2 없음(같은 패턴).
    # 🔸 거대 괴물 체형(원작 몬스터 포인트) — 가로가 크게 나올 수 있음, 체형 유지하고 수치만
    # 보고.
    "초월_강재규_AP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_강재규_AP.zip",
        glb_member="source/opfp___chopper__monster_point__xps_by_o_dv89_o_dfbloco.rar",
        inner_gltf="OPFP - Chopper (Monster Point)/11306_U.fbx",
        source_format="fbx",
        tex_member="textures/11306_D.png",
        path="Assets/Art/Units/초월_강재규_AP/초월_강재규_AP.fbx",
        mesh_name="Chopper",
        height=1.8,
        biped_prefix="Bip001",
        rename={k: v for k, v in dict(biped_rename_table("Bip001"), **{"Bip001 Spine1": "Spine2"}).items()
                if k != "Bip001 Spine2"},
        fold={"Bip001": "Hips", "Bone001": "Head"},
        bone_position_override={"Spine1": ("Bip001 Spine", "tail")},
        # 🔴 몬스터 포인트 체형은 목이 거의 없다(직접 확인: Neck 가중치 0) — 실제 원본 자체
        # 특징이라 예외로 둔다.
        allow_dead_bones={"Spine1", "Neck"},
        materials={"11306_U_F": ("texture_file", "tex_member"), "11306_U": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 초월_황준석_ADAP(쿠자/아오키지, OPDS 하치노스판) — PM 사양 원문 보존(2026-09-18,
    # SOURCE.txt 참고). 원본: source/*.rar(bsdtar) 안 「OPDS - Kuzan (Hachinosu)/
    # role_shifanduiqingzhi_skin.fbx」+ zip textures/tex_role_kuzan_shifan.png.
    # 아마추어 뼈 128·메시 1(11,682정점)·재질 1 — Spine2 있음(마유리·가프·쵸파와 다른
    # 패턴, biped_rename_table 그대로).
    # 🔴 PM 경고 그대로 실측 확인 — "Bip001 L/R UpperArm·Forearm"은 정점그룹 자체가 없고
    # LUpArmTwist·L/R ForeTwist(+1)이 실제 가중치를 짐 → fold로 본체에 합침(타시기와 같은
    # 트위스트 패턴, 이름은 3ds Biped 표준이라 PM이 말한 "niuqu"와는 다른 이름이었음).
    # 얼굴 보조 뼈(bone06~10, Head 자식)→Head. 옷깃 장식(bone01·Dummy001→LeftShoulder,
    # bone03·Dummy002→RightShoulder). 케이프/코트 시뮬레이션 뼈 80여 개(Spine2·Spine
    # 자식 트리, 실측 — PM 사전조사보다 훨씬 많음)→각각 Spine2·Spine로 fold_subtree.
    "초월_황준석_ADAP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_황준석_ADAP.zip",
        glb_member="source/opds___kuzan__hachinosu__by_o_dv89_o_djj3jxr.rar",
        inner_gltf="OPDS - Kuzan (Hachinosu)/role_shifanduiqingzhi_skin.fbx",
        source_format="fbx",
        tex_member="textures/tex_role_kuzan_shifan.png",
        path="Assets/Art/Units/초월_황준석_ADAP/초월_황준석_ADAP.fbx",
        mesh_name="Kuzan",
        height=1.8,
        biped_prefix="Bip001",
        rename=biped_rename_table("Bip001"),
        fold={
            "Bip001": "Hips",
            "Bip001 LUpArmTwist": "LeftArm", "Bip001 L ForeTwist": "LeftForeArm", "Bip001 L ForeTwist1": "LeftForeArm",
            "Bip001 RUpArmTwist": "RightArm", "Bip001 R ForeTwist": "RightForeArm", "Bip001 R ForeTwist1": "RightForeArm",
        },
        fold_subtree={
            "bone06": "Head", "bone07": "Head", "bone08": "Head", "bone09": "Head", "bone10": "Head",
            "bone01": "LeftShoulder", "Dummy001": "LeftShoulder",
            "bone03": "RightShoulder", "Dummy002": "RightShoulder",
            "bone11": "Spine2", "bone17": "Spine2", "bone23": "Spine2", "bone40": "Spine2", "bone45": "Spine2",
            "bone51": "Spine2", "Dummy003": "Spine2", "Dummy004": "Spine2",
            "bone57": "Spine", "bone61": "Spine", "bone65": "Spine", "bone69": "Spine", "bone73": "Spine",
            "bone77": "Spine",
        },
        materials={"tex_role_kuzan_shifan_mat_01": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    "전설적인_이유선": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_이유선.zip",
        glb_member="source/Tashigi.zip",
        inner_gltf="Tashigi.gltf",
        path="Assets/Art/Units/전설적인_이유선/전설적인_이유선.fbx",
        mesh_name="Tashigi",
        height=1.8,
        biped_prefix="Bip001",
        drop_meshes={"Icosphere"},
        # 트위스트 → 본체(가중치 합침). "1" 붙은 보조가 실제 가중치를 들고 있어도 이름만
        # 다를 뿐 최종적으로 같은 본체 뼈로 합쳐지면 되므로 구분 없이 전부 매핑.
        fold={
            "Bip001 LThighTwist": "LeftUpLeg", "Bip001 RThighTwist": "RightUpLeg",
            "Bip001 LCalfTwist": "LeftLeg", "Bip001 LCalfTwist1": "LeftLeg",
            "Bip001 RCalfTwist": "RightLeg", "Bip001 RCalfTwist1": "RightLeg",
            "Bip001 LUpArmTwist": "LeftArm", "Bip001 RUpArmTwist": "RightArm",
            "Bip001 L ForeTwist": "LeftForeArm", "Bip001 L ForeTwist1": "LeftForeArm",
            "Bip001 R ForeTwist": "RightForeArm", "Bip001 R ForeTwist1": "RightForeArm",
            "12014 C": "Hips", "Bip001": "Hips",
        },
        # 🔴 실측 발견 — PM 목록에 없던 뼈 34개가 더 있었다(직접 확인, 전부 fold_subtree로 뿌리만
        # 지정해 처리): "Bone Pifeng NN"(26개, 코트 자락 흔들뼈 — R/L Clavicle과 Spine2에서
        # 갈라지는 4갈래, 코트(+cloak) 메시만 물림), "Bone Chest 01~04"(가슴 흔들뼈, Spine2
        # 자식, body 메시 물림), "Bone002/005/006/007"(오른손 자식, 전전충(+Snail) 메시 전용 —
        # Bone007이 그 뿌리).
        fold_subtree={"Bone Hair 01": "Head", "Bone Hair 03": "Head", "Bone Hair 05": "Head",
                      "Bone YanJing": "Head", "Facebone": "Head",
                      "Bone Pifeng 02": "RightShoulder", "Bone Pifeng 13": "LeftShoulder",
                      "Bone Pifeng 11": "Spine2", "Bone Pifeng 22": "Spine2",
                      "Bone Chest 01": "Spine2", "Bone Chest 03": "Spine2", "Bone007": "RightHand"},
        materials={n: ("texture_direct", None) for n in (
            "5_+cloak_1_0_0", "5_+Snail_1_0_0", "5_body_1_0_0", "5_face_1_0_0", "5_hair_1_0_0", "7_glass_1_0_0",
        )},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 불멸_정윤식(골 D. 로저, 중국 모바일 role_ 립 glb) — blender 세션(2026-09-22, PM 1차
    # 지시·재검수로 권총 문제 정정).
    # 원본: ~/Desktop/구랜디스킨모음/09_불멸/불멸_정윤식.glb(8.9MB). 스킨 1·뼈 114
    # (Bip001 계열 + 순번 접미사 "_NN", Sketchfab 재추출 흔적 — 진짜 3ds Biped 이름표와
    # 다르니 biped_prefix 자동표 대신 rename 직접 작성)·메시 4(role_luojie_qiang 684정점·
    # role_luojieshizhuang_body 10,723·role_luojieshizhuang_pifeng 5,893, Icosphere 조명구
    # 제외)·재질 3·이미지 3·애니 30.
    # 🔴 PM 우려("luojie 기본 몸/의상 몸 겹친 두 벌")는 실측 결과 사실 아님 — role_luojie_qiang
    # 은 몸 복제본이 아니라 qiang_Bone34/41(Pelvis 자식)에 물린 소품이었다. 1차 판정(허리
    # 검집)은 오판 — PM이 유니티에서 실측한 결과 권총(수발총, qiang=枪=총)이었고, 레스트
    # 좌표가 가슴~어깨 높이인데 Hips 강체로 접어 Idle 중 궤적이 크게 벌어져 어깨 옆 허공에
    # 떴다. 선례대로(손에 드는/걸치는 무기 소품 전부 제거) role_luojie_qiang 메시를
    # drop_meshes로 통째로 뺀다.
    # 🔴 PM 경고대로 "Bip001 L/R Forearm"은 정점그룹은 있으나 가중치 0(죽은 뼈)이고 L/R
    # ForeTwist(+1)이 팔뚝 몫을 실제로 짐 → fold_subtree로 ForeArm에 합침. 🔴 여기서 더
    # 나아가 실측 발견 — 이 Forearm 죽은 뼈의 레스트 좌표 자체가 발목 높이(z 0.0016, Toe0과
    # 같은 좌표)로 무너져 있다. bone_position_override로 ForeTwist의 head(팔꿈치로 타당한
    # 좌표)를 대신 쓴다 — 이걸 안 하면 팔이 화면 밖까지 늘어지는 사고가 난다(실측·재현).
    # 로저 특유의 팔자수염 2가닥 — Bone046→047→048·Bone050→051→052(Head 자식) + 단일
    # Bone054(턱 중앙 추정) → Head. 옷깃 흔들뼈 Bone007~011·024~027(L Clavicle 자식)·
    # Bone013~017·019~022(R Clavicle 자식) → 각 Shoulder. 가슴 흔들뼈 Bone001~005·
    # 037/038·040/041·043/044(Spine2 자식) → Spine2. 왼쪽 허벅지 전용 비대칭 흔들뼈
    # Bone029→030→031·Bone033(코트 자락 추정, 왼쪽에만 있음) → LeftUpLeg.
    # wuqi_bone01(무기, R Hand 자식, 339정점 실가중치 — 손에 쥔 소품이라 손과 함께 움직이면
    # 문제없어 남김) → RightHand. 스킬 이펙트 뼈(eff_luojie_skill_2_dao 서브트리, 전부
    # 무가중치) → RightHand로 안전 정리. "_0" 접미사 중복 관절(Clavicle_0·UpperArm_0·
    # Spine2_0, 전부 실가중치 있음) → 각각 본체와 같은 대상(같은 관절의 이중 노드일 뿐이라
    # 위험 없음, 정준영류 "코트 전용 두 번째 뼈대"와 다른 패턴). 왼발끝만 무가중치(오른발끝은
    # 유효) — 원본 자체의 비대칭, allow_dead_bones.
    "불멸_정윤식": dict(
        source="~/Desktop/구랜디스킨모음/09_불멸/불멸_정윤식.glb",
        path="Assets/Art/Units/불멸_정윤식/불멸_정윤식.fbx",
        mesh_name="Roger",
        height=1.8,
        drop_meshes={"Icosphere", "Object_9"},
        rename={
            "Bip001 Pelvis_03": "Hips", "Bip001 Spine_04": "Spine", "Bip001 Spine1_017": "Spine1",
            "Bip001 Spine2_018": "Spine2", "Bip001 Neck_019": "Neck", "Bip001 Head_020": "Head",
            "Bip001 L Clavicle_029": "LeftShoulder", "Bip001 L UpperArm_00": "LeftArm",
            "Bip001 L Forearm_01": "LeftForeArm", "Bip001 L Hand_030": "LeftHand",
            "Bip001 L Thigh_05": "LeftUpLeg", "Bip001 L Calf_06": "LeftLeg",
            "Bip001 L Foot_07": "LeftFoot", "Bip001 L Toe0_08": "LeftToeBase",
            "Bip001 R Clavicle_057": "RightShoulder", "Bip001 R UpperArm_058": "RightArm",
            "Bip001 R Forearm_059": "RightForeArm", "Bip001 R Hand_060": "RightHand",
            "Bip001 R Thigh_013": "RightUpLeg", "Bip001 R Calf_014": "RightLeg",
            "Bip001 R Foot_015": "RightFoot", "Bip001 R Toe0_016": "RightToeBase",
        },
        fold={
            "_rootJoint": "Hips", "Bip001_02": "Hips", "eff_guadian_buff_body_0107": "Hips",
            "Bone033_012": "LeftUpLeg",
            "hit_pos_0104": "Spine2", "Bip001 Spine2_018_0": "Spine2",
            "head_bar_028": "Head", "Bone054_027": "Head",
            "wuqi_bone01_076": "RightHand",
            "Bip001 L UpperArm_00_0": "LeftArm", "Bip001 L Clavicle_029_0": "LeftShoulder",
            "Bip001 R UpperArm_058_0": "RightArm", "Bip001 R Clavicle_057_0": "RightShoulder",
            "qiang_Bone34_0105": "Hips", "qiang_Bone41_0106": "Hips",
        },
        fold_subtree={
            "Bone046_021": "Head", "Bone050_024": "Head",
            "eff_luojie_skill_2_dao_077": "RightHand",
            "Bone029_09": "LeftUpLeg",
            "Bip001 L ForeTwist_046": "LeftForeArm", "Bip001 R ForeTwist_082": "RightForeArm",
            "Bone001_093": "Spine2", "Bone037_098": "Spine2", "Bone040_0100": "Spine2", "Bone043_0102": "Spine2",
            "Bone007_048": "LeftShoulder", "Bone013_084": "RightShoulder",
            "Bip001 L Finger0_031": "LeftHand", "Bip001 L Finger1_034": "LeftHand",
            "Bip001 L Finger2_037": "LeftHand", "Bip001 L Finger3_040": "LeftHand", "Bip001 L Finger4_043": "LeftHand",
            "Bip001 R Finger0_061": "RightHand", "Bip001 R Finger1_064": "RightHand",
            "Bip001 R Finger2_067": "RightHand", "Bip001 R Finger3_070": "RightHand", "Bip001 R Finger4_073": "RightHand",
        },
        bone_position_override={
            "LeftForeArm": ("Bip001 L ForeTwist_046", "head"),
            "RightForeArm": ("Bip001 R ForeTwist_082", "head"),
        },
        allow_dead_bones={"LeftToeBase"},
        materials={n: ("texture_direct", None) for n in (
            "tex_role_luojieshizhuang_body_mat", "tex_role_luojieshizhuang_pifeng_mat",
        )},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 불멸_김용태(얌마 리야르고, Bleach Mobile cha_ 립) — blender 세션(2026-09-22).
    # 원본: ~/Desktop/구랜디스킨모음/09_불멸/불멸_김용태.zip 안 source/cha_yammy_arrancar.fbx
    # (286KB, 저폴리) + textures/cha_yammy_arrancar.png(256², RGBA). 뼈 42(3ds Biped "Bip01
    # X" 표준, 노태현_AP 마유리와 같은 "cha_" 계열)·메시 1(4,941정점)·재질 1.
    # 척추 Hips-Spine-Spine1까지만(Spine2 없음, 마유리와 같은 패턴) — Spine1→Spine2, Spine1은
    # 자리표시(Spine tail·Spine1 head 사이 간격 있어 퇴화 없음, 직접 확인).
    # 손가락 3갈래×1마디(Finger0/1/2 + 각 "01"접미, Pinky·중간마디 없음) —
    # biped_rename_table(fingers=3, joints=2)로 커버.
    # 팔다리 전부 실측 확인 — 손상된 뼈 없음(로저·크로커다일과 달리 Forearm·Hand 정상).
    # Bone_eyes·Bone_mouth(Head 자식)→Head. Bone_hair_01→02→03(Head 자식 체인)→Head.
    # 🔴 Bone_sword(Spine 직계 자식, 210정점 실가중치) — 렌더로 직접 확인, 허리 옆구리에 찬
    # 장검(칼집). 원작 신체(허리 강체 부착)라 유지, Spine으로 fold.
    # 🔴 텍스처: FBX가 참조하는 경로(source/cha_yammy_arrancar.png)가 깨져 있어(실제 위치는
    # zip textures/) texture_file로 재연결. RGBA 4채널이나 알파 전 픽셀 1.0 고정 확인(PM
    # 우려대로 반투명 위험 있었음) — texture_file/texture_direct 공통 로직이 Alpha 링크 없이
    # Base Color만 연결하고 blend_method OPAQUE 강제해 해결.
    # ⚠️ 스케일 — PM 지시대로 키 1.8m을 강제하지 않고 원본 비율(T자 가로:세로 1.135, 일반
    # 인체보다 다소 벌크)을 먼저 보고했음. PM 검수 결과 1.8 정규화 그대로 괜찮다고 확정
    # (유니티 쪽에서 크기 조정) — height=1.8 유지.
    "불멸_김용태": dict(
        source="~/Desktop/구랜디스킨모음/09_불멸/불멸_김용태.zip",
        glb_member="source/cha_yammy_arrancar.fbx",
        source_format="fbx",
        tex_member="textures/cha_yammy_arrancar.png",
        path="Assets/Art/Units/불멸_김용태/불멸_김용태.fbx",
        mesh_name="Yammy",
        height=1.8,
        biped_prefix="Bip01",
        rename={k: v for k, v in dict(biped_rename_table("Bip01", fingers=3, joints=2),
                                        **{"Bip01 Spine1": "Spine2"}).items() if k != "Bip01 Spine2"},
        fold={
            "cha_yammy_arrancar": "Hips", "Bip01": "Hips", "cha_yammy_arrancar.001": "Hips",
            "Bone_eyes": "Head", "Bone_mouth": "Head", "Bone_sword": "Spine",
        },
        fold_subtree={"Bone_hair_01": "Head"},
        bone_position_override={"Spine1": ("Bip01 Spine", "tail")},
        allow_dead_bones={"Spine1"},
        materials={"cha_yammy_arrancar": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 히단자와 진타(블리치, cha_ 립) → 초월_김경현_AP(2026-09-22 초월, 사장님 지시로 크로커다일
    # 자리를 히든_황정기로 옮기고 이 키를 진타로 재배정). 얌마(불멸_김용태)와 완전히 같은
    # "cha_" Bleach mobile Biped 형식이라 그 항목을 그대로 본떠서 만듦.
    # 원본: ~/Desktop/구랜디스킨모음/08_초월/초월_김경현_AP.zip 안 source/cha_jinta.fbx
    # (250KB, 저폴리) + textures/cha_jinta.png(256², RGBA). 뼈 40(3ds Biped "Bip01 X" 표준)·
    # 메시 1(3,736정점)·재질 1. 척추 Pelvis-Spine-Spine1까지만(Spine2 없음, 얌마와 같은 패턴) —
    # 이번엔 Spine1 자체가 실가중치(368)를 쥐고 있었지만 얌마 템플릿 그대로 Spine2로 보내고
    # Spine1은 자리표시(허용 — 퇴화 아님, Spine tail z 0.0079 vs Spine1 head z 0.0093로 간격
    # 있음, 직접 확인).
    # 손가락 3갈래×2마디(Finger0/1/2 + 각 "01"접미, 얌마와 동일 패턴) — biped_rename_table
    # (fingers=3, joints=2).
    # 팔다리 전부 실측 확인 — 손상된 뼈 없음. Bone_eyes(Head 자식)→Head. Bone_hair_01→02
    # (Head 자식 체인)→Head.
    # 🔴 Bone_weapon(135정점 실가중치) — 진타가 드는 야구방망이. PM 지시대로 뺌(선례: 손에
    # 드는 무기는 제거).
    # 🔴 텍스처: FBX가 참조하는 경로가 깨져 있어(이미지 채널 0, size (0,0)) texture_file로
    # 재연결. RGBA 4채널 — 얌마 때와 같은 패턴이라 texture_file/texture_direct 공통 로직이
    # Alpha 링크 없이 Base Color만 연결하고 OPAQUE 강제.
    "초월_김경현_AP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_김경현_AP.zip",
        glb_member="source/cha_jinta.fbx",
        source_format="fbx",
        tex_member="textures/cha_jinta.png",
        path="Assets/Art/Units/초월_김경현_AP/초월_김경현_AP.fbx",
        mesh_name="Jinta",
        height=1.8,
        biped_prefix="Bip01",
        drop_meshes=set(),
        rename={k: v for k, v in dict(biped_rename_table("Bip01", fingers=3, joints=2),
                                        **{"Bip01 Spine1": "Spine2"}).items() if k != "Bip01 Spine2"},
        fold={
            "cha_jinta": "Hips", "Bip01": "Hips", "cha_jinta.001": "Hips",
            "Bone_eyes": "Head", "Bone_weapon": "RightHand",
        },
        fold_subtree={"Bone_hair_01": "Head"},
        drop_bone_verts={"Bone_weapon"},
        bone_position_override={"Spine1": ("Bip01 Spine", "tail")},
        allow_dead_bones={"Spine1"},
        materials={"cha_jinta": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 불멸_이승우(가로우 우주적 공포 모드, 원펀맨) — blender 세션(2026-09-22).
    # 원본: ~/Desktop/구랜디스킨모음/09_불멸/불멸_이승우.glb(2.9MB). 스킨 1·뼈 66(표준
    # mixamorig: 이름 + "_NN" 번호 꼬리, 손가락 4갈래×4마디 전부)·메시 2(G7_Cosmic_0
    # 14,169정점·G7_White_0 13,137정점)·재질 2(Cosmic·White)·이미지 1장.
    # 🔴 White 재질은 텍스처가 아예 없이 고정 회색(0.76)뿐 — wire_flat_color로 처리.
    # Cosmic·White 둘 다 실루엣이 거의 같은 이중 레이어(면 비율 52:48)로, 코즈믹 별무늬
    # 겉면 + 무지 안쪽면으로 추정(원작 이중 레이어 효과, 프로시저럴 노드·발광 없음 —
    # 굽기 불필요, 그대로 유니티에 넘어감).
    # 좌우 뒤집힘(히나타 함정) 없음 — armature matrix_world 배율 전부 양수, LeftShoulder
    # x=+·RightShoulder x=− 정상 확인. 표준 이름이라 rename은 접두어·번호만 떼면 됨,
    # 무가중치 뼈 0개(이번 불멸 6건 중 가장 깨끗한 리그).
    # 🔴 mixamorig:Head_06의 tail이 z 2.83(키 1.8보다 큼, 가중치 정점은 z 1.39~1.71로
    # 정상)으로 원본 자체가 깨져 있었다 — bone_tail_offset으로 head+(0,0,0.25) 대체
    # (가중치엔 영향 없음, T자 렌더에서 머리 위 거대한 뼈 표시로 발견).
    # 원본 팔 각도 A자 약 63°(수평 기준) — level_arms로 T자 교정.
    "불멸_이승우": dict(
        source="~/Desktop/구랜디스킨모음/09_불멸/불멸_이승우.glb",
        path="Assets/Art/Units/불멸_이승우/불멸_이승우.fbx",
        mesh_name="Garou",
        height=1.8,
        rename={
            "mixamorig:Hips_01": "Hips", "mixamorig:Spine_02": "Spine", "mixamorig:Spine1_03": "Spine1",
            "mixamorig:Spine2_04": "Spine2", "mixamorig:Neck_05": "Neck", "mixamorig:Head_06": "Head",
            "mixamorig:LeftShoulder_08": "LeftShoulder", "mixamorig:LeftArm_09": "LeftArm",
            "mixamorig:LeftForeArm_010": "LeftForeArm", "mixamorig:LeftHand_011": "LeftHand",
            "mixamorig:RightShoulder_032": "RightShoulder", "mixamorig:RightArm_033": "RightArm",
            "mixamorig:RightForeArm_034": "RightForeArm", "mixamorig:RightHand_035": "RightHand",
            "mixamorig:LeftUpLeg_056": "LeftUpLeg", "mixamorig:LeftLeg_057": "LeftLeg",
            "mixamorig:LeftFoot_058": "LeftFoot", "mixamorig:LeftToeBase_059": "LeftToeBase",
            "mixamorig:RightUpLeg_061": "RightUpLeg", "mixamorig:RightLeg_00": "RightLeg",
            "mixamorig:RightFoot_062": "RightFoot", "mixamorig:RightToeBase_063": "RightToeBase",
        },
        fold={
            "_rootJoint": "Hips",
            "mixamorig:HeadTop_End_07": "Head",
            "mixamorig:LeftToe_End_060": "LeftToeBase", "mixamorig:RightToe_End_064": "RightToeBase",
        },
        fold_subtree={
            "mixamorig:LeftHandThumb1_012": "LeftHand", "mixamorig:LeftHandIndex1_016": "LeftHand",
            "mixamorig:LeftHandMiddle1_020": "LeftHand", "mixamorig:LeftHandRing1_024": "LeftHand",
            "mixamorig:LeftHandPinky1_028": "LeftHand",
            "mixamorig:RightHandThumb1_036": "RightHand", "mixamorig:RightHandIndex1_040": "RightHand",
            "mixamorig:RightHandMiddle1_044": "RightHand", "mixamorig:RightHandRing1_048": "RightHand",
            "mixamorig:RightHandPinky1_052": "RightHand",
        },
        bone_tail_offset={"Head": (0.0, 0.0, 0.25)},
        materials={
            "Cosmic": ("texture_direct", None),
            "White": ("flat_color", (0.76, 0.76, 0.76, 1.0)),
        },
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 블리치 우라하라 상점 츠무기야 우루루(진타와 콤비) → 히든_전유라(2026-09-22 히든,
    # blender 세션). 뼈 47·3ds Biped "Bip001 X"(Tashigi와 같은 규칙, biped_prefix 그대로
    # 씀) · 메시 7(body·face·hair·leye·reye·mouth·weapon) · 애니 26(안 씀).
    # 🔴 weapon(바주카 계열) — 렌더로 확인, 몸에서 분리된 별도 메시라 통째로 드롭.
    # 뿌리 "Bip001"(가중치 0, Pelvis 위 래퍼) → Hips로 접음(타시기와 같은 패턴).
    # 얼굴: Bip001 Head 밑에 "head"(소문자, 실가중치 있는 진짜 얼굴 서브루트) → 그 밑에
    # yu_leye_0·yu_mouth_0·yu_reye_0(눈·입 뼈, 메시 이름과 같음) — "head" 서브트리 통째로
    # Head에 합침(fold_subtree).
    # Toe0 뼈 없음(발까지만, PM 사전조사와 일치) — biped_rename_table 기본표에 Toe0 매핑이
    # 있지만 원본에 없는 이름이라 그냥 매치가 안 될 뿐(에러 아님), ToeBase는 무가중치라
    # allow_dead_bones로 허용.
    # 재질↔이미지는 파일명 끝 번호로 추측하지 않고 glTF material.node_tree에서 직접 읽어
    # 확인(덴지 교훈): yu_body_0→tex04·yu_face_0→tex05·yu_hair_0→tex06·yu_leye_0→tex00·
    # yu_mouth_0→tex01·yu_reye_0→tex02(weapon=tex03, 드롭이라 안 씀). 파일명 끝 숫자(_0~_6)는
    # 이 tex 번호와 무관한 값이라 파일명은 texNN 부분 문자열로만 매칭.
    "히든_전유라": dict(
        source="~/Desktop/구랜디스킨모음/05_히든/히든_전유라.zip",
        glb_member="source/yu_0_battleout.glb",
        path="Assets/Art/Units/히든_전유라/히든_전유라.fbx",
        mesh_name="Ururu",
        height=1.8,
        biped_prefix="Bip001",
        # 🔴 1차 시도에서 "yu_weapon_0"이 실제 오브젝트 이름과 안 맞아(noesis 접미사 붙음)
        # 조용히 안 빠지고 "쓴 메시"에 들어갔다 — 전체 이름으로 정정. 조명용 Icosphere도
        # 같은 증상으로 딸려 들어갔었어서 같이 뺌(Cube는 면이 없어 이 파이프라인이 아예
        # 안 읽음, 다른 유닛과 같은 증상이라 안 넣어도 됨).
        drop_meshes={"yu_weapon_0_noesis_meshnode_0003", "Icosphere"},
        fold={"Bip001": "Hips"},
        # 바주카(메시 드롭과 별개로 뼈도 따로 있음, 1차 배치 assert로 발견): Bip001 Prop1
        # 밑 서브트리(rweapon·메시와 이름이 같은 yu_weapon_0 뼈) — 메시를 이미 드롭했으니
        # 어디로 접든 상관없어 Hips로.
        # Bip001 Spine 자식으로 따로 달린 3마디짜리 사슬 4개(Bone001→002→003·005→006→007·
        # 009→010→011·013→014→015) — Prop1과 무관, body 메시에 실가중치 있음(각 13~24정점,
        # 직접 확인 — 가슴 주변 장식용 늘어진 리본/끈으로 추정) → 몸통(Spine2)으로 접음.
        fold_subtree={"head": "Head", "Bip001 Prop1": "Hips",
                      "Bone001": "Spine2", "Bone005": "Spine2", "Bone009": "Spine2", "Bone013": "Spine2"},
        # 척추가 Pelvis-Spine-Spine1까지만 있고 Spine2가 없음(PM 사전조사와 일치).
        # 🔴 PM 재검수(유니티 Idle에서 양팔이 얼굴 위로 말려 올라감) — Spine1의 tail을 그대로
        # 쓰면 원본이 이미 Neck 바로 앞에서 끝나 Spine2가 사실상 길이 0으로 무너짐(크로커다일·
        # 드래곤 때와 같은 함정) → Spine1↔Neck 사이를 반으로 보간(lerp)해 Spine1·Spine2 둘 다
        # 실제 길이를 갖게 함.
        # Toe0도 없음(발까지만) — 마찬가지로 Foot의 tail을 자리표시로(다리 끝이라 자식이 없어
        # 길이 0이어도 리타겟에 영향 없음, Spine2와 다른 경우).
        bone_position_override={"Spine2": ("Bip001 Spine1", "Bip001 Neck", 0.5),
                                "LeftToeBase": ("Bip001 L Foot", "tail"), "RightToeBase": ("Bip001 R Foot", "tail")},
        bone_tail_offset={"LeftToeBase": (0.0, -0.1, -0.02), "RightToeBase": (0.0, -0.1, -0.02)},
        allow_dead_bones={"Spine2", "LeftToeBase", "RightToeBase"},
        tex00_member="textures/yu_0_battleout0_tex00_3.png",
        tex01_member="textures/yu_0_battleout0_tex01_4.png",
        tex02_member="textures/yu_0_battleout0_tex02_5.png",
        tex04_member="textures/yu_0_battleout0_tex04_0.png",
        tex05_member="textures/yu_0_battleout0_tex05_1.png",
        tex06_member="textures/yu_0_battleout0_tex06_2.png",
        materials={
            "yu_leye_0": ("texture_file", "tex00_member"),
            "yu_mouth_0": ("texture_file", "tex01_member"),
            "yu_reye_0": ("texture_file", "tex02_member"),
            "yu_body_0": ("texture_file", "tex04_member"),
            "yu_face_0": ("texture_file", "tex05_member"),
            "yu_hair_0": ("texture_file", "tex06_member"),
        },
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 블리치 참월(이치고의 참백도 정령, 선글라스·검은 코트 아저씨) → 히든_서승혁
    # (2026-09-22 히든, blender 세션). 얌마·진타와 완전히 같은 블리치 cha_ Bip01 3마디
    # 손가락 형식(biped_prefix="Bip01") — 진타 항목 그대로 본뜸.
    # 뼈 67 · 메시 3(cha_zangetsu_0 몸통, wp1_0=칼, Cube 면 없어 안 읽힘) · 재질 1 · 애니 0.
    # 칼(wp1_0, "weapon"+weapon_line01~03 뼈 사슬로 오른손에 물림) — 드롭.
    "히든_서승혁": dict(
        source="~/Desktop/구랜디스킨모음/05_히든/히든_서승혁.zip",
        glb_member="source/cha_zangetsu.fbx",
        path="Assets/Art/Units/히든_서승혁/히든_서승혁.fbx",
        mesh_name="Zangetsu",
        height=1.8,
        biped_prefix="Bip01",
        drop_meshes={"wp1_0"},
        fold={"cha_zangetsu": "Hips", "cha_zangetsu.001": "Hips", "Bip01": "Hips", "wp1": "Hips"},
        fold_subtree={"F_hair01": "Head", "L_hair01": "Head", "L_hair03": "Head", "L_hair05": "Head",
                      "R_hair01": "Head", "L_neck01": "Spine1", "R_neck01": "Spine1",
                      "skirt_BL01": "Hips", "skirt_BR01": "Hips", "skirt_FL01": "Hips", "skirt_FR01": "Hips",
                      "weapon": "Hips"},
        # 척추가 Pelvis-Spine-Spine1까지만 있고 Spine2가 없음(우루루와 같은 증상) —
        # Spine1↔Neck 사이를 반으로 보간(lerp)해 Spine1·Spine2 둘 다 실제 길이를 갖게 함.
        bone_position_override={"Spine2": ("Bip01 Spine1", "Bip01 Neck", 0.5)},
        tex_member="textures/cha_zangetsu.png",
        materials={"cha_zangetsu": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 블리치 토센 카나메(아란칼 편 "총괄관", 흰 제복) → 히든_정욱진(2026-09-22 히든,
    # blender 세션). 참월·진타와 같은 cha_ Bip01 3마디 손가락 형식.
    # 뼈 54 · 메시 4(B_123=몸통 5,147정점, B_234_wp1=240정점, B_234_wp3=102정점, Cube 면
    # 없어 안 읽힘) · 재질 1 · 애니 0.
    # 🔴 스즈무시(칼) — "Weapon"·"Weapon_case" 뼈는 손이 아니라 Bip01(허리 높이)에 물려
    # 있어(직접 확인) 처음엔 허리에 찬 칼집으로 봤지만, 1차 T자 렌더로 실제로 보니 칼이
    # 뼈 부모와 무관하게 오른손 위치에서 뻗어 나와 실제로 쥔 자세임(뼈 부모는 팔 흔들림에
    # 안 끌리게 하려는 트릭으로 보임) → PM 지시대로("손에 들었으면 뺄 것") wp1_0(칼날)·
    # wp3_0(칼자루 고리 장식) 둘 다 드롭.
    "히든_정욱진": dict(
        source="~/Desktop/구랜디스킨모음/05_히든/히든_정욱진.zip",
        glb_member="source/cha_tosen_general.fbx",
        path="Assets/Art/Units/히든_정욱진/히든_정욱진.fbx",
        mesh_name="Tosen",
        height=1.8,
        biped_prefix="Bip01",
        drop_meshes={"cha_tosen_B_234_wp1_0", "cha_tosen_B_234_wp3_0"},
        fold={"cha_tosen_general": "Hips", "Bip01": "Hips", "cha_tosen_B_123": "Hips",
              "cha_tosen_B_234_wp1": "Hips", "cha_tosen_B_234_wp3": "Hips"},
        fold_subtree={"Hair_B01": "Head", "Hair_F01": "Head", "Hood01": "Head",
                      "Skirt_BL01": "Hips", "Skirt_BR01": "Hips", "Skirt_FL01": "Hips", "Skirt_FR01": "Hips",
                      "Weapon": "Hips", "Weapon_case": "Hips"},
        bone_position_override={"Spine2": ("Bip01 Spine1", "Bip01 Neck", 0.5)},
        tex_member="textures/cha_tosen_general.png",
        materials={"cha_tosen_general": ("texture_file", "tex_member")},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
}

# 22뼈 계층 — gen_rigify_skin.py·gen_skin_rig.py와 같은 이름 규칙(PREFIX만 공유).
SPINE = [("Hips", None), ("Spine", "Hips"), ("Spine1", "Spine"), ("Spine2", "Spine1"), ("Neck", "Spine2"), ("Head", "Neck")]
LIMB_NAMES = ["Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase"]




def find_glb_and_extras(cfg, workdir):
    """소스 zip 안에 zip이 한 번 더 있을 수 있다(전설적인 계열, Sketchfab/게임추출 원본 그대로
    한 겹 더 압축) — gen_rigify_skin.py와 같은 처리."""
    src = os.path.expanduser(cfg["source"])
    if src.lower().endswith(".zip"):
        with zipfile.ZipFile(src) as z:
            z.extractall(workdir)
        glb_path = os.path.join(workdir, cfg["glb_member"])
        if glb_path.lower().endswith(".zip"):
            inner_dir = os.path.join(workdir, "inner")
            with zipfile.ZipFile(glb_path) as z:
                z.extractall(inner_dir)
            glb_path = os.path.join(inner_dir, cfg.get("inner_gltf", "scene.gltf"))
        elif glb_path.lower().endswith(".rar"):
            # 🔴 가프 — 사장님 zip 안 원본이 zip이 아니라 rar(OPFP XPS 다운로드 그대로)일 수
            # 있다. 파이썬 표준 zipfile로는 못 열어 bsdtar(맥 기본 제공)로 푼다.
            inner_dir = os.path.join(workdir, "inner")
            os.makedirs(inner_dir, exist_ok=True)
            subprocess.run(["bsdtar", "-xf", glb_path, "-C", inner_dir], check=True)
            glb_path = os.path.join(inner_dir, cfg["inner_gltf"])
        # 히나타 — 곁텍스처(밑줄 이름 png)가 소스 FBX 안 깨진 경로(공백 이름 tga) 대신
        # 실제로 필요한 경우, "_member"로 끝나는 cfg 키를 전부 workdir 기준 경로로 돌려준다
        # (gen_rigify_skin.py의 texture_file kind와 같은 장치).
        extras = {k: os.path.join(workdir, v) for k, v in cfg.items() if k.endswith("_member") and k != "glb_member"}
        return glb_path, extras
    return src, {}


def safe_filename(name):
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in name)


def wire_image_material(mat, image):
    """재질 그래프를 밀고 Principled+TEX_IMAGE로 새로 잇는다(히나타 — 소스 이미지 경로 자체가
    깨져 있어(공백 이름) wire_texture_direct처럼 기존 TEX_IMAGE 노드를 재활용할 수 없다)."""
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
            nt.nodes.remove(n)
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None) or nt.nodes.new("ShaderNodeBsdfPrincipled")
    out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None) or nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"


def wire_texture_direct(mat):
    """정점색이 전부 중립(1,1,1,1)이라 굽기 없이 기존 TEX_IMAGE를 Base Color에 직결(Mix·
    VertexColor 노드는 그래프에서 제거)."""
    nt = mat.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image), None)
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
    if tex is None or bsdf is None or out is None:
        return
    for n in list(nt.nodes):
        if n.type in ("MIX", "MIX_RGB", "VERTEX_COLOR", "ATTRIBUTE"):
            nt.nodes.remove(n)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"


def wire_flat_color(mat, color):
    """텍스처가 아예 없고 단색인 재질(가로우 White — TEX_IMAGE 자체가 없이 고정 회색)을
    Principled Base Color 상수로 재배선. 노드를 다 밀고 새로 잇는다(wire_image_material과
    같은 전략, 이미지 대신 상수)."""
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf.inputs["Base Color"].default_value = color
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"


def level_arms(arm, body, chain=("Shoulder", "Arm", "ForeArm", "Hand")):
    """🔴 실측 발견(신문철/나루토) — 이전 버전은 pb.tail(뼈 자신의 pose-tail)로 방향을
    쟀는데, world_matrix 곱 없이 pb.matrix.to_translation()과 섞어 쓴 게 이 리그(축 관례가
    다른 프리파이어)에서 어긋났다(178.5°짜리 헛돈 회전 → 크기 Y가 6m대로 튐). PM이
    fix_unit_fbx.py의 tpose_arms(읽기만, 안 건드림)에서 쓰는 방식대로 고쳤다 — 매 뼈의
    방향을 "그 뼈 head → 다음 뼈 head"의 arm.matrix_world 곱한 실제 월드 벡터로 재고, 매
    회전 뒤 view_layer.update()로 다음 계산에 반영한다(부모→자식 순서 고정)."""
    scene = bpy.context.scene
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    pose = arm.pose.bones
    Mw = arm.matrix_world

    def P(bname):
        return Mw @ pose[bname].head

    def turn(bname, R3):
        pivot = P(bname)
        pose[bname].matrix = Matrix.Translation(pivot) @ R3.to_4x4() @ Matrix.Translation(-pivot) @ pose[bname].matrix
        bpy.context.view_layer.update()

    def align(bname, head_name, next_name, target):
        cur = P(next_name) - P(head_name)
        if cur.length > 1e-6:
            turn(bname, cur.normalized().rotation_difference(target.normalized()).to_matrix())

    turned = {}
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        d = Vector((sx, 0.0, 0.0))
        full = [PREFIX + side + c for c in chain]
        for i in range(len(full) - 1):
            bname = full[i]
            before = (P(full[i + 1]) - P(bname)).normalized()
            align(bname, bname, full[i + 1], d)
            turned[side + chain[i]] = round(math.degrees(before.angle(d)), 1)
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = body.data
    body.data = baked
    baked.name = old.name
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    # 🔴 PM 지시(2026-09-17, 디오/구현담당2가 먼저 찾은 버그, gen_mmd_skin.py·gen_objrip_skin.py
    # 참고해 이식) — rotation_difference는 방향(swing)만 맞추고 그 축 둘레 비틀림(roll)은
    # 뼈마다 제각각 남는다. T자 렌더는 바인드=레스트라 안 보이지만, 유니티가 팔을 실제로
    # 움직이면 이 제각각의 roll이 굽힘 축을 엉뚱한 방향으로 돌려 관절이 뒤틀린다(나루토 실측:
    # 팔이 머리 뒤로 꺾이고 다리가 벌어짐). armature_apply 직후(바인드=레스트라 지금 roll을
    # 바꿔도 메시는 안 움직인다) 전 매핑 뼈의 roll을 표준 규칙으로 다시 맞춘다.
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        d = (eb.tail - eb.head)
        if d.length > 1e-6:
            d = d.normalized()
            eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    return turned


def build(name, cfg, out_dir=None, render_dir=None, workdir=None):
    report = {"이름": name}
    workdir = workdir or os.path.join("/tmp/gen_biped_skin_work", safe_filename(name))
    os.makedirs(workdir, exist_ok=True)
    glb_path, extra_paths = find_glb_and_extras(cfg, workdir)
    report["원본"] = glb_path

    bpy.ops.wm.read_factory_settings(use_empty=True)
    if cfg.get("source_format") == "fbx" or glb_path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=glb_path)
    else:
        bpy.ops.import_scene.gltf(filepath=glb_path)
    scene = bpy.context.scene

    arm_obj = next(o for o in scene.objects if o.type == "ARMATURE")
    all_meshes = [o for o in scene.objects if o.type == "MESH"]
    keep = [o for o in all_meshes if o.name not in cfg.get("drop_meshes", set())]
    report["뺀 메시"] = sorted(cfg.get("drop_meshes", set()))
    report["쓴 메시"] = sorted(o.name for o in keep)
    for o in list(all_meshes):
        if o not in keep:
            bpy.data.objects.remove(o, do_unlink=True)

    # 🔴 진타(2026-09-22) — 무기(야구방망이)가 별도 메시가 아니라 한 메시 안에 뼈(Bone_weapon)
    # 가중치로만 구분돼 있어 drop_meshes로 못 뺀다(그 메시엔 몸도 같이 있다). cfg["drop_bone_verts"]
    # (뼈 이름 집합)면 그 뼈에 주로 물린(가중치 최댓값 기준) 면을 bmesh로 지운다 — 선례
    # fix_unit_fbx.py의 drop_material_faces와 같은 원리, 뼈 기준이라는 점만 다르다.
    if cfg.get("drop_bone_verts"):
        import bmesh as _bmesh
        target_bones = cfg["drop_bone_verts"]
        for o in keep:
            gidx = {vg.index for vg in o.vertex_groups if vg.name in target_bones}
            if not gidx:
                continue
            drop_vidx = set()
            for v in o.data.vertices:
                best = max(v.groups, key=lambda g: g.weight, default=None)
                if best is not None and best.group in gidx and best.weight > 0.5:
                    drop_vidx.add(v.index)
            if not drop_vidx:
                continue
            bm = _bmesh.new()
            bm.from_mesh(o.data)
            bm.verts.ensure_lookup_table()
            to_del = [bm.verts[i] for i in drop_vidx]
            _bmesh.ops.delete(bm, geom=to_del, context="VERTS")
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()
        report["뼈 기준 정점 삭제"] = {b: 0 for b in target_bones}  # 개수는 실측 로그로 대체(아래)

    # UV 층을 첫 장만 남기고 통일(히소카 사고 재발 방지 — 이 소스는 UV1이 범위 1.9×2.0으로
    # 퇴화가 아니라 오히려 "너무 큰" 비정상 라이트맵이라 반드시 첫 장만 남겨야 한다).
    for o in keep:
        uvs = o.data.uv_layers
        while len(uvs) > cfg.get("uv_layers", 1):
            uvs.remove(uvs[-1])
        if len(uvs) == 1:
            uvs[0].name = "UVMap"

    # 재질 처리. texture_direct(정점색 중립, 굽기 불필요) 또는 texture_file(소스 안 이미지
    # 경로가 깨져 있어 zip의 실제 파일로 재연결 — 히나타: 공백 이름 tga → 밑줄 이름 png).
    mat_plan = cfg["materials"]
    wired_files = {}          # arg(멤버 키) -> 이미 로드한 image (여러 재질이 같은 텍스처 공유)
    for o in keep:
        for slot in o.material_slots:
            m = slot.material
            if m is None:
                continue
            kind, arg = mat_plan.get(m.name, (None, None))
            if kind == "texture_direct":
                wire_texture_direct(m)
            elif kind == "flat_color":
                wire_flat_color(m, arg)
            elif kind == "texture_file":
                img = wired_files.get(arg)
                if img is None:
                    img = bpy.data.images.load(extra_paths[arg], check_existing=True)
                    wired_files[arg] = img
                wire_image_material(m, img)

    # 모든 조각을 세계 좌표로 굽고 하나로 합친다.
    for o in keep:
        M = Matrix(o.matrix_world)
        o.parent = None
        o.matrix_basis = Matrix.Identity(4)
        o.matrix_world = M
        o.data.transform(M)
        o.matrix_world = Matrix.Identity(4)
        if M.determinant() < 0:
            o.data.flip_normals()
    for o in scene.objects:
        o.select_set(o in keep)
    bpy.context.view_layer.objects.active = keep[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = body.data.name = cfg["mesh_name"]
    report["재질(최종)"] = sorted(s.material.name for s in body.material_slots if s.material)

    # 뼈 접기·개명. 소스가 진짜 3ds Biped 이름("{prefix} X")이면 biped_rename_table로 자동
    # 생성하고, 신문철(나루토)처럼 정리된 자체 이름(bone_Hips 등)이면 cfg["rename"]을 직접 쓴다.
    rename = dict(cfg["rename"]) if cfg.get("rename") else biped_rename_table(cfg["biped_prefix"])
    fold = dict(cfg.get("fold", {}))
    for root, target in cfg.get("fold_subtree", {}).items():
        children_of = {}
        for b in arm_obj.data.bones:
            if b.parent:
                children_of.setdefault(b.parent.name, []).append(b.name)
        stack, subtree = [root], []
        while stack:
            n = stack.pop()
            subtree.append(n)
            stack.extend(children_of.get(n, []))
        for n in subtree:
            fold.setdefault(n, target)

    def target_mixamorig(old_name):
        if old_name in rename:
            return rename[old_name]
        if old_name in fold:
            return fold[old_name]
        return None

    old_names = [b.name for b in arm_obj.data.bones]
    mapping = {}
    unmapped = []
    for n in old_names:
        t = target_mixamorig(n)
        if t is None:
            unmapped.append(n)
        else:
            mapping[n] = t
    report["매핑 안 된 뼈"] = unmapped
    assert not unmapped, f"{name}: 매핑 못 한 뼈 {unmapped}"

    targets = sorted(set(mapping.values()))
    new_group_index = {}
    for t in targets:
        vg = body.vertex_groups.new(name=f"__new__{t}")
        new_group_index[t] = vg.index
    old_index_to_target = {}
    for old_name, t in mapping.items():
        vg = body.vertex_groups.get(old_name)
        if vg:
            old_index_to_target[vg.index] = t
    for v in body.data.vertices:
        acc = {}
        for g in v.groups:
            t = old_index_to_target.get(g.group)
            if t:
                acc[t] = acc.get(t, 0.0) + g.weight
        for t, w in acc.items():
            if w > 0:
                body.vertex_groups[new_group_index[t]].add([v.index], min(w, 1.0), "REPLACE")

    new_indices = set(new_group_index.values())

    def _total_new_weight(v):
        return sum(g.weight for g in v.groups if g.group in new_indices)

    zero_verts = [v.index for v in body.data.vertices if _total_new_weight(v) < 0.01]
    if zero_verts:
        import mathutils
        zero_set = set(zero_verts)
        kd = mathutils.kdtree.KDTree(len(body.data.vertices))
        for v in body.data.vertices:
            if v.index not in zero_set:
                kd.insert(v.co, v.index)
        kd.balance()
        for vi in zero_verts:
            _, src_idx, _ = kd.find(body.data.vertices[vi].co)
            for g in body.data.vertices[src_idx].groups:
                if g.group in new_indices:
                    body.vertex_groups[g.group].add([vi], g.weight, "REPLACE")
        report["소스 자체 무가중치라 최근접 정점으로 채움"] = len(zero_verts)

    for vg in list(body.vertex_groups):
        if not vg.name.startswith("__new__"):
            body.vertex_groups.remove(vg)
    for vg in body.vertex_groups:
        vg.name = vg.name[len("__new__"):]

    counts = {t: 0 for t in targets}
    for v in body.data.vertices:
        for g in v.groups:
            if g.weight > 0.01:
                counts[body.vertex_groups[g.group].name] += 1
    # cfg["allow_dead_bones"] — 척추 마디가 mixamorig보다 적은 소스용 자리표시 뼈(가중치 0이
    # 정상)만 예외로 봐준다(gen_rigify_skin.py와 같은 장치).
    allow_dead = cfg.get("allow_dead_bones", set())
    dead = [k for k, c in counts.items() if c == 0 and k not in allow_dead]
    report["뼈별 정점(w>0.01)"] = counts
    assert not dead, f"{name}: 가중치 없는 뼈(접은 뒤) {dead}"

    # 뼈대를 22개로 다시 짓는다.
    # 🔴 히나타 — 소스 아마추어 오브젝트 자체에 배율·회전이 걸려 있을 수 있다(cm 단위 배율
    # 0.01·X −90°, 직접 확인). b.head_local/tail_local은 아마추어 로컬(이 변환 적용 전) 좌표라
    # 그대로 쓰면 이미 matrix_world를 적용해 세계 좌표로 구운 메시와 어긋난다 — 곱해서 맞춘다.
    Mw_arm = arm_obj.matrix_world
    old_head_tail = {b.name: (Mw_arm @ Vector(b.head_local), Mw_arm @ Vector(b.tail_local)) for b in arm_obj.data.bones}
    new_arm_data = bpy.data.armatures.new("Armature")
    new_arm_obj = bpy.data.objects.new("Armature", new_arm_data)
    scene.collection.objects.link(new_arm_obj)
    bpy.context.view_layer.objects.active = new_arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    # 🔴 손가락도 이제 "{side}Hand"로 접혀서 rename을 그냥 뒤집으면(마지막 항목이 이긴다)
    # 손가락 옛 이름이 진짜 Hand 옛 이름을 덮어써 손목이 아니라 손가락 위치로 뼈가 지어진다
    # (직접 겪음). anatomical 위치 대상만 골라 별도로 뒤집는다 — Finger로 시작하는 옛 이름은
    # 스킵(같은 값 "…Hand"를 가리켜도 위치 출처로 쓰면 안 된다).
    src_bone_of = {v: k for k, v in rename.items()
                   if (v in dict(SPINE) or v in (side + n for side in ("Left", "Right") for n in LIMB_NAMES))
                   and "Finger" not in k}
    # cfg["bone_position_override"] — {target: (source_bone, "head"|"tail")}로 그 점 하나에
    # 0-길이 자리표시 뼈를 짓는다(위 allow_dead_bones와 짝, gen_rigify_skin.py와 같은 장치).
    # 🔴 우루루(2026-09-22, PM 유니티 재검수) — Spine2가 없는 골격에 이 방식으로 자리표시를
    # 하면(Spine1의 tail = 원본이 이미 Neck 바로 앞에서 끝나는 자리라 사실상 같은 점) Spine2가
    # 길이 0으로 무너져 유니티 리타겟에서 자식(어깨·팔)이 방향을 못 잡고 헛돈다(크로커다일·
    # 드래곤 때와 같은 함정). {target: (source_a, source_b, 비율)} 3항이면 두 원본 뼈의 head
    # 사이를 보간(lerp) — Spine1↔Neck 사이를 반으로 갈라 Spine1·Spine2 둘 다 실제 길이를
    # 가지게 한다.
    pos_override = cfg.get("bone_position_override", {})

    def head_of(tname):
        if tname in pos_override:
            ov = pos_override[tname]
            if len(ov) == 3:
                src_a, src_b, t = ov
                return old_head_tail[src_a][0].lerp(old_head_tail[src_b][0], t)
            src, end = ov
            return old_head_tail[src][0 if end == "head" else 1]
        return old_head_tail[src_bone_of[tname]][0]

    # 🔴 실측 발견(신문철/나루토) — 옛 뼈 자신의 tail을 그대로 새 뼈의 tail로 쓰면 안 된다.
    # 이 소스(프리파이어)는 tail이 다음 관절이 아니라 다른 참고축을 가리켜서(직접 확인:
    # bone_LeftArm의 tail이 bone_LeftForeArm의 head와 전혀 다른 방향), level_arms의 방향
    # 계산이 178.5°짜리 헛돈 회전을 만들었다(팔이 반대로 꺾임, 크기 Y가 키보다 커짐). 다음
    # 관절의 head를 tail로 체인처럼 잇는다 — 끝 뼈(Head·Hand·ToeBase)만 옛 tail을 쓴다.
    # 🔴 가로우(2026-09-22) — 그 "옛 tail"조차 깨진 경우(mixamorig:Head_06의 tail이 키
    # 전체(1.8)보다 큰 z 2.83, 가중치 정점은 z 1.39~1.71로 멀쩡한데 tail만 허공에 뜸) —
    # cfg["bone_tail_offset"]={target: (dx,dy,dz)}면 head_of(tname)+델타로 대체(가중치엔
    # 영향 없음, 표시·roll 계산용 안전판).
    tail_offset = cfg.get("bone_tail_offset", {})

    def terminal_tail(tname):
        if tname in tail_offset:
            return head_of(tname) + Vector(tail_offset[tname])
        return old_head_tail[src_bone_of[tname]][1]

    spine_chain = [t for t, _ in SPINE]
    eb_by_name = {}
    for i, (tname, parent) in enumerate(SPINE):
        h = head_of(tname)
        t = head_of(spine_chain[i + 1]) if i + 1 < len(spine_chain) else terminal_tail(tname)
        eb = new_arm_data.edit_bones.new(PREFIX + tname)
        eb.head, eb.tail = h, t
        eb_by_name[tname] = eb
    for tname in eb_by_name:
        parent = dict(SPINE)[tname]
        if parent:
            eb_by_name[tname].parent = eb_by_name[parent]
    for side, mirror_key in (("Left", ".L"), ("Right", ".R")):
        for i, limb in enumerate(LIMB_NAMES):
            tname = side + limb
            h = head_of(tname)
            next_limb = LIMB_NAMES[i + 1] if i + 1 < len(LIMB_NAMES) and LIMB_NAMES[i + 1] not in ("UpLeg",) else None
            t = head_of(side + next_limb) if next_limb else terminal_tail(tname)
            eb = new_arm_data.edit_bones.new(PREFIX + tname)
            eb.head, eb.tail = h, t
            parent_t = {"Shoulder": "Spine2", "Arm": side + "Shoulder", "ForeArm": side + "Arm", "Hand": side + "ForeArm",
                        "UpLeg": "Hips", "Leg": side + "UpLeg", "Foot": side + "Leg", "ToeBase": side + "Foot"}[limb]
            eb.parent = eb_by_name[parent_t]
            eb_by_name[tname] = eb
    for eb in new_arm_data.edit_bones:
        d = (eb.tail - eb.head)
        if d.length > 1e-6:
            d = d.normalized()
            eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    report["뼈"] = len(new_arm_data.bones)
    assert len(new_arm_data.bones) == 22, f"{name}: 22뼈가 아니라 {len(new_arm_data.bones)}개"

    for vg in body.vertex_groups:
        vg.name = PREFIX + vg.name
    for m in list(body.modifiers):
        body.modifiers.remove(m)
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = new_arm_obj
    body.parent = new_arm_obj
    bpy.data.objects.remove(arm_obj, do_unlink=True)
    arm_obj = new_arm_obj

    if cfg.get("level_arms"):
        report["팔 수평 굽기(°)"] = level_arms(arm_obj, body, tuple(cfg.get("level_arms_bones", ("Shoulder", "Arm", "ForeArm", "Hand"))))

    bpy.context.view_layer.update()
    V = np.array([v.co for v in body.data.vertices])
    lo, hi = V.min(0), V.max(0)
    H = float(hi[2] - lo[2])
    s = cfg["height"] / H
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))
    for o in (body, arm_obj):
        o.data.transform(G) if o.type == "MESH" else None
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm_obj.data.edit_bones:
        eb.head = G @ eb.head
        eb.tail = G @ eb.tail
    bpy.ops.object.mode_set(mode="OBJECT")

    bpy.context.view_layer.update()
    V2 = np.array([v.co for v in body.data.vertices])
    report["크기(m)"] = [round(float(c), 3) for c in (V2.max(0) - V2.min(0))]
    report["최저 z"] = round(float(V2[:, 2].min()), 4)

    tri_before = sum(len(p.vertices) - 2 for p in body.data.polygons)
    ratio = cfg.get("decimate_ratio", 1.0)
    if ratio < 0.999:
        mod = body.modifiers.new("decimate", "DECIMATE")
        mod.ratio = ratio
        dg = bpy.context.evaluated_depsgraph_get()
        ev = body.evaluated_get(dg)
        new_mesh = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
        body.modifiers.remove(mod)
        old_mesh = body.data
        body.data = new_mesh
        new_mesh.name = old_mesh.name
    tri_after = sum(len(p.vertices) - 2 for p in body.data.polygons)
    report["감량"] = {"전": tri_before, "후": tri_after}

    # 내보내기 — 텍스처는 소스 폴더에서 최종 Textures로 복사.
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        for node in m.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and node.image.filepath:
                src_path = bpy.path.abspath(node.image.filepath)
                if os.path.exists(src_path):
                    dst_path = os.path.join(tex_dir, os.path.basename(src_path))
                    if not os.path.exists(dst_path):
                        with open(src_path, "rb") as f, open(dst_path, "wb") as g:
                            g.write(f.read())
                    node.image.filepath = dst_path
                    node.image.reload()

    uv = body.data.uv_layers.active
    uvs = np.array([d.uv for d in uv.data])
    uv_range = (float(uvs[:, 0].max() - uvs[:, 0].min()), float(uvs[:, 1].max() - uvs[:, 1].min()))
    uv_area = uv_range[0] * uv_range[1]
    report["UV0 범위"] = uv_range
    report["UV0 면적"] = round(uv_area, 4)
    assert uv_area > 0.05, f"{name}: UV0가 퇴화됐다(면적 {uv_area:.4f})"

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    # 🔴 참월(2026-09-22, PM 유니티 검수) — 아마추어 오브젝트 이름이 "Armature.001"로 남는
    # 경우가 있었다(무해했지만 fix_unit_fbx.py와 같은 보편 수정을 넣어 둠 — 다른 이름과
    # 충돌해도 "Armature"로 통일).
    arm_obj.name = "Armature"
    for o in scene.objects:
        o.select_set(o in (body, arm_obj))
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    return report


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = render_dir = None
    names = []
    it = iter(args)
    for a in it:
        if a == "--out":
            out_dir = next(it)
        elif a == "--render":
            render_dir = next(it)
        else:
            names.append(a)
    for n in names or list(SKINS):
        r = build(n, SKINS[n], out_dir, render_dir)
        print("리깅  " + json.dumps(r, ensure_ascii=False, default=str))


if __name__ == "__main__":
    main()
