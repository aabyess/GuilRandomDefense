"""이미 있는 리그(3ds Biped·정리된 자체 이름 등)를 mixamorig로 이름만 바꿔 쓰는 계열 —
구현담당2 전담(gen_biped_skin.py는 구현담당1 담당 파일, 한 스크립트=한 세션 규칙이라 겹치는
SKINS를 넣지 않고 이 파일로 옮겨 온다, 2026-09-18 첫 건 초월_최상호_AD/이치고).
원리·cfg 스키마는 gen_biped_skin.py와 동일(그 파일 읽기로 배워 옮겨 적음, 그 파일 자체는
손대지 않음) — rename/fold/fold_subtree/bone_position_override/allow_dead_bones/materials/
level_arms 전부 같은 뜻.

  blender -b --factory-startup --python Tools/blender/gen_valvebiped_skin.py -- 초월_최상호_AD [--out DIR] [--render DIR]

🔴 PM 실측(이치고, 유니티 반려) — gen_biped_skin.py 계열 출력이 원본 재질의 Alpha 링크/값을
그대로 내보내 FBX에 TransparencyFactor/Opacity가 실려(인형이 유니티 Idle에서 반투명하게
보임, "킹 교훈"), wire_texture_direct가 Mix·VertexColor만 지우고 Alpha 소켓은 안 건드려서
생긴 문제였다. force_opaque()를 추가해 내보내기 직전 전 재질의 Principled Alpha 링크를
끊고 값을 1.0으로 고정한다(gen_objrip_skin.py의 "재질 Alpha 링크 제거" 처리와 같은 원리).
"""
import json
import math
import os
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
    # 초월_최상호_AD(완전 호로화 이치고, Bleach VRChat 립) — PM 사양 원문 보존(2026-09-18,
    # gen_biped_skin.py에서 이 파일로 옮김·재내보내기는 유니티 반투명 반려 뒤 2026-09-18):
    # ※ 최상호는 초월 AD·AP(바지사장) 둘, 이건 AD.
    # 원본: outer/source/HIchigo.zip(zip 안 zip) 안 HollofiedIchigo.fbx + Textures/tex01·
    # tex02.png(+tex_katana.png, PM 브리핑에 없던 여분 — 칼 메시가 없어 안 씀, 직접 확인).
    # 메시 1(Body, 12,605정점·4,195면) · 재질 6(Mouth·Material1·A new.001·A new·Material0·
    # Fist) 전부 이미 Base Color에 텍스처 연결됨(already_linked, 직접 확인 6/6) →
    # texture_direct로 그대로. UV 1층("UVMap", 범위 0.003~0.998 정상). 키 3.4042(PM 실측
    # 3.40과 일치, matrix_world 곱해 확인 — 라이토 교훈대로 항등행렬이라 원래도 안전했음).
    # 뼈 52개 전부 직접 확인·전부 매핑:
    # Hips→Spine→Chest→Neck→Head(척추 2마디뿐, 신문철과 같은 자리표시 처리 — Chest를
    # Spine2로 보내고 Spine1은 자리표시).
    # 머리카락 Hair→Hair2~4·R_Hair→R_Hair2~3·L_Hair→L_Hair2~3(전부 Hair의 서브트리) +
    # Mouth·LeftEye·RightEye → 전부 Head로 접음(PM 지시: 자락이 어깨·등으로 늘어지는지
    # T자 렌더로 확인, 문제 없음).
    # 좌우 shoulder/arm/elbow/wrist → Shoulder/Arm/ForeArm/Hand. 손가락은 Thumb0~2·
    # IndexFinger1~3·RingFinger1~3뿐(Middle·Pinky 없음, 3갈래) → 각 첫마디를
    # fold_subtree 뿌리로 Hand에 접음(타시기 교훈과 같은 방식).
    # 좌우 leg/knee/ankle/toe → UpLeg/Leg/Foot/ToeBase.
    "초월_최상호_AD": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_최상호_AD.zip",
        glb_member="source/HIchigo.zip",
        inner_gltf="HollofiedIchigo.fbx",
        source_format="fbx",
        path="Assets/Art/Units/초월_최상호_AD/초월_최상호_AD.fbx",
        mesh_name="Ichigo",
        height=1.8,
        rename={
            "Hips": "Hips", "Spine": "Spine", "Chest": "Spine2", "Neck": "Neck", "Head": "Head",
            "Left shoulder": "LeftShoulder", "Left arm": "LeftArm", "Left elbow": "LeftForeArm", "Left wrist": "LeftHand",
            "Right shoulder": "RightShoulder", "Right arm": "RightArm", "Right elbow": "RightForeArm", "Right wrist": "RightHand",
            "Left leg": "LeftUpLeg", "Left knee": "LeftLeg", "Left ankle": "LeftFoot", "Left toe": "LeftToeBase",
            "Right leg": "RightUpLeg", "Right knee": "RightLeg", "Right ankle": "RightFoot", "Right toe": "RightToeBase",
        },
        fold_subtree={
            "Hair": "Head", "Mouth": "Head", "LeftEye": "Head", "RightEye": "Head",
            "Thumb0_L": "LeftHand", "IndexFinger1_L": "LeftHand", "RingFinger1_L": "LeftHand",
            "Thumb0_R": "RightHand", "IndexFinger1_R": "RightHand", "RingFinger1_R": "RightHand",
        },
        bone_position_override={"Spine1": ("Spine", "tail")},
        allow_dead_bones={"Spine1"},
        materials={n: ("texture_direct", None) for n in (
            "Mouth", "Material1", "A new.001", "A new", "Material0", "Fist",
        )},
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 초월_신문철_AP(육도 나루토, NUNS 계열 팬 리그 FBX) — PM 사양 원문 보존(2026-09-18).
    # 원본: outer/source/naruto-shippuden.zip(zip 안 zip) 안 source/untitled.fbx +
    # (안쪽 zip 자체 textures/도 있지만 이름이 겹쳐 바깥 textures/ 것을 씀) outer/textures/
    # Untitled195(몸 추정)·196(재킷 추정)·198(얼굴 추정)·Eye.png·Weapon.png(무기 안 씀).
    # 아마추어 뼈 342 전부 직접 확인. 뼈 이름이 "body pelvis" 식 공백 포함 자체 규칙.
    # 척추 2마디뿐(body upper·body spine1) — body spine1→Spine2, Spine1은 자리표시.
    # 🔴 "head head extra" 밑에 머리카락(hair left/right/top 01~09)·얼굴(head face 01~03)·
    # 입(mouth upper/lower·teeth·tongue·chin)·눈꺼풀(eyelid)·눈썹(eyebrows)·포니테일·
    # 이마보호대 끈(cloth headband)·눈(eye) 전부가 서브트리로 매달려 있어(직접 확인) 그
    # 뿌리 하나만 Head로 접으면 전부 한 번에 처리됨(PM 목록보다 간단).
    # "unused head neck"·"head neck left/right/back"은 그 서브트리 밖(body spine1 직계
    # 자식)이라 따로 Neck으로 접음.
    # 재킷 자락 흔들뼈(cloth jacket front/side/back left/right 01~02, body spine1 직계) +
    # 장식 구슬(body ball root 01~08)도 Spine2로 접음.
    # 팔: shoulder→Shoulder·arm1→Arm·arm2→ForeArm·hand→Hand, 손가락 5×3(thumb/index/middle/
    # ring/pinky 0~2) 첫마디를 Hand로 접음, arm extra 01~03(arm1 자식)→Arm, arm extra
    # 04~06+sleeve(arm2 자식)→ForeArm.
    # 🔴 무기(wep01.001·wep02.001, 구도옥/지팡이 추정)는 몸과 같은 메시 오브젝트에 재질
    # 슬롯만 다르게 붙어 있어 drop_meshes로 못 뺌 — cfg["drop_materials"](신규 기능)로 그
    # 재질이 걸린 면만 지운다. 뼈(weapon 01/02 root)는 손으로 접어 어차피 남는 정점이
    # 없게 됨.
    # 🔴 재질 25개 전부 텍스처 노드 자체가 없음(직접 확인, bpy.data.images 빈 배열) — 전부
    # texture_file로 새로 연결해야 함(already_linked 아님). 이름 규칙(jacket1=재킷·
    # kami=머리카락·kao=얼굴·teeth/tongue=입안·body/hand=피부·eyel/eyer=눈·sphere00X=
    # 정체 불명 장식)만으로는 어느 텍스처가 맞는지 확정 안 돼 렌더로 대조(PM 지시).
    "초월_신문철_AP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_신문철_AP.zip",
        glb_member="source/naruto-shippuden.zip",
        inner_gltf="source/untitled.fbx",
        source_format="fbx",
        tex_body_member="textures/Untitled195_20250622211812.png",
        tex_jacket_member="textures/Untitled196_20250622212139.png",
        tex_face_member="textures/Untitled198_20250622231942.png",
        tex_eye_member="textures/Eye.png",
        path="Assets/Art/Units/초월_신문철_AP/초월_신문철_AP.fbx",
        mesh_name="Naruto",
        height=1.8,
        drop_materials={"7_5nrt00t0 wep01.001_0.1_16_16", "7_5nrt00t0 wep02.001_0.1_16_16"},
        rename={
            "body pelvis": "Hips", "body upper": "Spine", "body spine1": "Spine2",
            "head neck": "Neck", "head head": "Head",
            "leg left leg1": "LeftUpLeg", "leg left leg2": "LeftLeg", "leg left foot": "LeftFoot", "leg left toes": "LeftToeBase",
            "leg right leg1": "RightUpLeg", "leg right leg2": "RightLeg", "leg right foot": "RightFoot", "leg right toes": "RightToeBase",
            "arm left shoulder": "LeftShoulder", "arm left arm1": "LeftArm", "arm left arm2": "LeftForeArm", "arm left hand": "LeftHand",
            "arm right shoulder": "RightShoulder", "arm right arm1": "RightArm", "arm right arm2": "RightForeArm", "arm right hand": "RightHand",
        },
        fold={
            "root ground": "Hips", "root body": "Hips",
            "leg left toes_end": "LeftToeBase", "leg right toes_end": "RightToeBase",
        },
        fold_subtree={
            "weapon 01 root": "RightHand", "weapon 02 root": "LeftHand",
            "head head extra": "Head",
            "unused head neck": "Neck", "head neck left": "Neck", "head neck right": "Neck", "head neck back": "Neck",
            "leg left foot extra 01": "LeftUpLeg", "leg left foot extra 02": "LeftUpLeg",
            "leg left foot extra 03": "LeftLeg", "leg left foot extra 04": "LeftLeg", "leg left foot extra 05": "LeftLeg",
            "leg right foot extra 01": "RightUpLeg", "leg right foot extra 02": "RightUpLeg",
            "leg right foot extra 03": "RightLeg", "leg right foot extra 04": "RightLeg", "leg right foot extra 05": "RightLeg",
            "arm left extra 01": "LeftArm", "arm left extra 02": "LeftArm", "arm left extra 03": "LeftArm",
            "arm left extra 04": "LeftForeArm", "arm left extra 05": "LeftForeArm", "arm left extra 06": "LeftForeArm",
            "arm left finger thumb 0": "LeftHand", "arm left finger index 0": "LeftHand", "arm left finger middle 0": "LeftHand",
            "arm left finger ring 0": "LeftHand", "arm left finger pinky 0": "LeftHand",
            "arm right extra 01": "RightArm", "arm right extra 02": "RightArm", "arm right extra 03": "RightArm",
            "arm right extra 04": "RightForeArm", "arm right extra 05": "RightForeArm", "arm right extra 06": "RightForeArm",
            "arm right finger thumb 0": "RightHand", "arm right finger index 0": "RightHand", "arm right finger middle 0": "RightHand",
            "arm right finger ring 0": "RightHand", "arm right finger pinky 0": "RightHand",
            "cloth jacket front right 01": "Spine2", "cloth jacket side right 01": "Spine2", "cloth jacket back right 01": "Spine2",
            "cloth jacket front left 01": "Spine2", "cloth jacket side left 01": "Spine2", "cloth jacket back left 01": "Spine2",
            "body ball root": "Spine2",
        },
        bone_position_override={"Spine1": ("body upper", "tail")},
        allow_dead_bones={"Spine1"},
        materials={
            "7_5nrt00t0 jacket1_0.1_16_16": ("texture_file", "tex_jacket_member"),
            "7_5nrt00t0 kami1_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrt00t0 kami11_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrt00t0 kami12_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrt00t0 kami13_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrt00t0 kao1_0.1_16_16": ("texture_file", "tex_jacket_member"),
            "7_5nrt00t0 kao2_0.1_16_16": ("texture_file", "tex_jacket_member"),
            "7_5nrt00t0 lower teeth_0.1_16_16": ("texture_file", "tex_jacket_member"),
            "7_5nrt00t0 tongue_0.1_16_16": ("texture_file", "tex_jacket_member"),
            "7_5nrt00t0 upper teeth_0.1_16_16": ("texture_file", "tex_jacket_member"),
            "7_5nrtbody1_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrtbody2_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrtbody6_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_5nrthand_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_eyel_0.1_16_16": ("texture_file", "tex_eye_member"),
            "7_eyer_0.1_16_16": ("texture_file", "tex_eye_member"),
            "7_sphere001_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere002_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere003_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere004_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere005_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere006_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere007_0.1_16_16": ("texture_file", "tex_body_member"),
            "7_sphere008_0.1_16_16": ("texture_file", "tex_body_member"),
        },
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
    # 초월_김경현_AP(크로커다일, One Piece Sketchfab glb) — PM 사양 원문 보존(2026-09-18),
    # 재작업(2026-09-22, blender 세션, PM 재반려 뒤 실측으로 근본 원인 재특정).
    # 원본: ~/Desktop/구랜디스킨모음/08_초월/초월_김경현_AP.glb(1.5MB). 스킨 1·뼈 65·재질 1
    # (Cha_2200_00, 이미 Base Color에 텍스처 연결됨 Image_0).
    # 🔴 스킨 좌표가 0.02~0.04 수준(직접 확인, 몸통 메시 Object_7 z 0.001~0.028)으로 아주
    # 작지만 각 메시 오브젝트 자체의 matrix_world가 그만큼 확대해 두고 있어(build()가 항상
    # matrix_world로 굽는 구조라 그대로 둬도 정상 처리됨) 특별 처리 불필요.
    # 뼈 이름이 이미 Mixamo형(CH_Hips_02·CH_LeftUpLeg_03 등, "CH_이름_번호" 규칙) — 접두어
    # CH_·번호 꼬리만 떼는 rename. 척추 Hips-Spine-Spine1까지만 있고(Spine2 없음) 손가락은
    # Index/Middle/Ring/Thumb 4갈래뿐(Pinky 없음).
    # 🔴 09-22 재작업으로 새로 밝혀진 진짜 원인(09-18의 "_0" 뼈 이론은 틀렸음, 그건 이미
    # 09-18에 Spine2로 옳게 접혀 있었다) — 메시가 7개가 아니라 6개(Icosphere 제외)인데 그중
    # "케이프"가 cha_2200_cape0(Object_9, 1,233정점, 정상 높이)와 cha_2200_cape(Object_17,
    # 1,358정점) 두 개였다. cha_2200_cape는 바인드 자세부터 이미 몸통 최고점(z 0.0279)보다
    # 위(z 최고 0.0357)로 솟아 있고, B_cape/BL_cape/BR_cape(root_Hips_049 자식, 케이프
    # 물리 시뮬레이션 전용 뼈대)에만 물려 있는데 그 뼈들을 Hips 강체로 접었더니 "바인드부터
    # 솟아 있는 여분 메시가 그대로 고정된 거대한 검은 판"으로 남았다 — 이게 유니티가 본
    # "코트가 머리 위로 키의 절반만큼 솟음"의 진짜 원인. cha_2200_cape(Object_17) 전체를
    # drop_meshes. 얼굴도 3개(face_2200_00/01/02, Object_11/13/15)가 완전히 동일한
    # 바운딩박스로 겹쳐 있어(표정 교체용) 00 하나만 남기고 01/02 드롭.
    # 🔴 LeftHand(CH_LeftHand_015) 레스트 위치가 발 근처(head z 0.001, 몸통 z 0.023~0.047과
    # 무관)로 깨져 있다 — 실제 갈고리(CH_LeftForeArm_014에 물린 597정점, bbox x
    # 0.0071~0.0159·z 0.0218~0.025)의 바깥쪽 끝을 bone_position_override+offset으로 대신
    # 쓴다. tail도 원본이 깨져 있어(발 근처) bone_tail_offset으로 head에서 +0.003m(로컬
    # 단위) 연장한 임의 방향으로 대체(갈고리는 강체라 정확한 방향은 안 중요, 0이 아니기만
    # 하면 됨).
    # 🔴 척추 Spine1 퇴화(길이 0에 가까움, 유니티 실측 로컬회전 271.45,180,180로 뒤집힘) —
    # CH_Spine_010의 tail이 CH_Spine1_011의 head와 좌표까지 똑같아(둘 다 z 0.0224) 그
    # 사이에 자리표시를 두면 방향이 안 정해졌다. CH_Spine_010 자신의 구간(머리→꼬리)을
    # bone_position_lerp로 절반씩 나눠 Spine·Spine1 둘 다 실제 길이를 갖게 한다. Spine2
    # (코트가 물린 뼈, CH_Spine1_011 그대로)는 손대지 않음 — 코트 거동엔 영향 없음.
    "초월_김경현_AP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_김경현_AP.glb",
        path="Assets/Art/Units/초월_김경현_AP/초월_김경현_AP.fbx",
        mesh_name="Crocodile",
        height=1.8,
        drop_meshes={"Icosphere", "Object_17", "Object_13", "Object_15"},
        rename={
            "CH_Hips_02": "Hips", "CH_Spine_010": "Spine", "CH_Spine1_011": "Spine2",
            "CH_Neck_028": "Neck", "CH_Head_029": "Head",
            "CH_LeftShoulder_012": "LeftShoulder", "CH_LeftArm_013": "LeftArm",
            "CH_LeftForeArm_014": "LeftForeArm", "CH_LeftHand_015": "LeftHand",
            "CH_RightShoulder_032": "RightShoulder", "CH_RightArm_033": "RightArm",
            "CH_RightForeArm_034": "RightForeArm", "CH_RightHand_035": "RightHand",
            "CH_LeftUpLeg_03": "LeftUpLeg", "CH_LeftLeg_04": "LeftLeg", "CH_LeftFoot_05": "LeftFoot", "CH_LeftToeBase_06": "LeftToeBase",
            "CH_RightUpLeg_07": "RightUpLeg", "CH_RightLeg_00": "RightLeg", "CH_RightFoot_08": "RightFoot", "CH_RightToeBase_09": "RightToeBase",
        },
        # "_0" 접미사 뼈(Object_9 cha_2200_cape0 전용 두 번째 뼈대, 09-18에 옳게 정착) →
        # 코트는 원작 신체(허리 아래로 늘어지는 긴 옷)라 팔 몫 없이 전부 Spine2로.
        fold={
            "_rootJoint": "Hips", "CH_Reference_01": "Hips", "CH_Hips_02_0": "Hips",
            "Cha_2200_hip_048": "Hips", "root_Hips_049": "Hips",
            "CH_Spine_010_0": "Spine", "CH_Spine1_011_0": "Spine2",
            "CH_LeftShoulder_012_0": "Spine2", "CH_LeftArm_013_0": "Spine2",
            "CH_RightShoulder_032_0": "Spine2", "CH_RightArm_033_0": "Spine2",
            "CH_Neck_028_0": "Neck", "eye_030": "Head", "Attach_Acc_031": "Head",
        },
        fold_subtree={
            "CH_LeftHandIndex1_016": "LeftHand", "CH_LeftHandMiddle1_019": "LeftHand",
            "CH_LeftHandRing1_022": "LeftHand", "CH_LeftHandThumb1_025": "LeftHand",
            "CH_RightHandIndex1_036": "RightHand", "CH_RightHandMiddle1_039": "RightHand",
            "CH_RightHandRing1_042": "RightHand", "CH_RightHandThumb1_045": "RightHand",
            # cha_2200_cape(Object_17)를 통째로 뺐으니 이 뼈들은 이제 무가중치 — Hips로
            # 접어 안전하게 정리(더 이상 코트 여분과 무관).
            "B_cape_00_050": "Hips", "BL_cape_00_052": "Hips", "BR_cape_00_054": "Hips",
        },
        bone_position_lerp={"Spine1": ("CH_Spine_010", 0.5)},
        bone_position_override={"LeftHand": ("CH_LeftForeArm_014", "head")},
        bone_position_offset={"LeftHand": (0.0089, -0.0002, 0.0)},
        bone_tail_offset={"LeftHand": (0.003, 0.0, 0.0)},
        allow_dead_bones={"Spine1"},
        # 왼손 갈고리 — CH_LeftHand_015·손가락 뼈에 가중치가 전혀 없고 CH_LeftForeArm_014에
        # 갈고리 메시가 통째로 몰려 있다(직접 확인). "오비토·샹크스 방식" — donor(ForeArm)
        # 소유는 그대로 두고 LeftHand에 씨앗 가중치(0.02, ADD)만 최근접 5정점에 부여해
        # 필수 뼈 판정만 만족(REPLACE로 통째 이관하지 않음 — 그러면 갈고리가 두 조각으로
        # 쪼개져 보이는 09-18 반려 재발). 씨앗 기준점은 원본 LeftHand 좌표(발 근처, 깨짐)가
        # 아니라 위 LeftHand 보정 위치를 그대로 씀(dead_bone_donor_anchor).
        dead_bone_donor={"LeftHand": "LeftForeArm"},
        dead_bone_donor_anchor={"LeftHand": ("CH_LeftForeArm_014", "head", (0.0089, -0.0002, 0.0))},
        materials={"Cha_2200_00": ("texture_direct", None)},
        # LeftHand 위치가 고쳐졌으니 체인에 다시 포함(09-18엔 위치가 깨져서 뺐었음).
        level_arms_bones=("Shoulder", "Arm", "ForeArm", "Hand"),
        level_arms=True,
        decimate_ratio=1.0,
        uv_layers=1,
    ),
}

# 22뼈 계층 — gen_biped_skin.py·gen_rigify_skin.py·gen_skin_rig.py와 같은 이름 규칙(PREFIX만 공유).
SPINE = [("Hips", None), ("Spine", "Hips"), ("Spine1", "Spine"), ("Spine2", "Spine1"), ("Neck", "Spine2"), ("Head", "Neck")]
LIMB_NAMES = ["Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase"]


def find_glb_and_extras(cfg, workdir):
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
            inner_dir = os.path.join(workdir, "inner")
            os.makedirs(inner_dir, exist_ok=True)
            subprocess.run(["bsdtar", "-xf", glb_path, "-C", inner_dir], check=True)
            glb_path = os.path.join(inner_dir, cfg["inner_gltf"])
        extras = {k: os.path.join(workdir, v) for k, v in cfg.items() if k.endswith("_member") and k != "glb_member"}
        return glb_path, extras
    return src, {}


def safe_filename(name):
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in name)


def wire_image_material(mat, image):
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


def force_opaque(mat):
    """🔴 PM 실측(이치고, 유니티 Idle 반투명 반려) — Principled Alpha 소켓에 원본 재질의
    링크·값이 그대로 남아 있으면 blend_method=OPAQUE로 바꿔도 FBX 내보내기가
    TransparencyFactor/Opacity 속성을 채워 유니티에서 인형이 반투명해진다. Alpha 링크를
    끊고 값을 1.0으로 고정해야 완전히 사라진다(gen_objrip_skin.py의 Alpha 링크 제거 처리와
    같은 원리, 이 파일 계열에도 이식)."""
    if not mat.use_nodes:
        return
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return
    alpha_in = bsdf.inputs.get("Alpha")
    if alpha_in is None:
        return
    for link in list(alpha_in.links):
        nt.links.remove(link)
    alpha_in.default_value = 1.0
    if hasattr(mat, "blend_method"):
        mat.blend_method = "OPAQUE"


def level_arms(arm, body, chain=("Shoulder", "Arm", "ForeArm", "Hand")):
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
    workdir = workdir or os.path.join("/tmp/gen_valvebiped_skin_work", safe_filename(name))
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

    # 🔴 신문철(육도 나루토, 2026-09-18) — 무기(구도옥/지팡이) 메시가 몸과 같은 오브젝트에
    # 재질 슬롯만 다르게 붙어 있어(drop_meshes로는 못 뺀다, 오브젝트 통째가 아니라 그 재질이
    # 걸린 면만 지워야 함) cfg["drop_materials"](재질 이름 집합)로 그 재질이 걸린 면만
    # bmesh로 지운다(뼈 이름 매핑은 그대로 둬도 됨 — 면이 없어지면 그 뼈의 정점 자체가 없어져
    # 가중치 문제가 안 생긴다).
    if cfg.get("drop_materials"):
        import bmesh as _bmesh
        for o in keep:
            drop_idx = {s.material.name for s in o.material_slots if s.material} & cfg["drop_materials"]
            if not drop_idx:
                continue
            bm = _bmesh.new()
            bm.from_mesh(o.data)
            drop_slot_idx = {i for i, s in enumerate(o.material_slots) if s.material and s.material.name in cfg["drop_materials"]}
            bm.faces.ensure_lookup_table()
            to_delete = [f for f in bm.faces if f.material_index in drop_slot_idx]
            _bmesh.ops.delete(bm, geom=to_delete, context="FACES")
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()
        report["뺀 재질(면 삭제)"] = sorted(cfg["drop_materials"])

    for o in keep:
        uvs = o.data.uv_layers
        while len(uvs) > cfg.get("uv_layers", 1):
            uvs.remove(uvs[-1])
        if len(uvs) == 1:
            uvs[0].name = "UVMap"

    mat_plan = cfg["materials"]
    wired_files = {}
    for o in keep:
        for slot in o.material_slots:
            m = slot.material
            if m is None:
                continue
            kind, arg = mat_plan.get(m.name, (None, None))
            if kind == "texture_direct":
                wire_texture_direct(m)
            elif kind == "texture_file":
                img = wired_files.get(arg)
                if img is None:
                    img = bpy.data.images.load(extra_paths[arg], check_existing=True)
                    wired_files[arg] = img
                wire_image_material(m, img)

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
    allow_dead = cfg.get("allow_dead_bones", set())
    dead = [k for k, c in counts.items() if c == 0 and k not in allow_dead]

    # 🔴 크로커다일(왼손 갈고리, 2026-09-18) — 원본 리그가 왼손(CH_LeftHand_015)·손가락
    # 뼈에 가중치를 전혀 안 두고 팔뚝(CH_LeftForeArm_014)에 갈고리 메시를 통째로 몰아
    # 뒀다(원작 신체 — 갈고리는 손가락이 안 움직이니 그렇게 만든 듯, 직접 확인). 필수 15뼈에
    # LeftHand가 있어 무가중치로 둘 수 없다.
    # 🔴 정정(PM 실측, 유니티 반려) — 처음엔 donor(ForeArm)가 가진 정점 중 가장 가까운 30%를
    # 통째로 LeftHand로 옮겼는데(REPLACE 1.0 + donor에서 제거), 유니티 Idle에서 갈고리가
    # 팔 회전을 안 따라가고 옆으로 수평인 채 남았다(추정: LeftHand·ForeArm이 실제 애니메이션
    # 곡선을 다르게 받아 갈고리가 두 조각으로 쪼개진 것처럼 움직임). "오비토·샹크스 방식"대로
    # 통째로 옮기지 않고 donor 소유를 그대로 둔 채 가장 가까운 몇 정점에 LeftHand를 씨앗
    # 가중치(0.02)만 더해 필수 뼈 판정만 만족시킨다(donor에서 안 뺌 — ADD, REPLACE 아님) —
    # 갈고리 전체가 여전히 ForeArm 강체로 움직여 팔을 그대로 따라간다.
    donor_map = cfg.get("dead_bone_donor", {})
    # 🔴 크로커다일 재작업(2026-09-22) — 기본은 reverse_rename으로 대상 뼈 "자기 자신"의
    # 원본 head_local을 씨앗 기준점으로 쓰는데, 그 원본 위치 자체가 깨진 경우(LeftHand,
    # 발 근처 z 0.001) 씨앗이 엉뚱한 곳(발 근처 정점)에 붙는다. dead_bone_donor_anchor로
    # (기준 뼈, head|tail, 델타) 커스텀 기준점을 지정할 수 있게 한다 — 없으면 이전과 동일.
    anchor_map = cfg.get("dead_bone_donor_anchor", {})
    if dead and donor_map:
        reverse_rename = {v: k for k, v in rename.items()}
        Mw_arm_early = arm_obj.matrix_world
        still_dead = []
        rescued = {}
        for tb in dead:
            donor = donor_map.get(tb)
            src_name = reverse_rename.get(tb)
            if not donor or not src_name or donor not in body.vertex_groups:
                still_dead.append(tb)
                continue
            if tb in anchor_map:
                a_src, a_end, a_off = anchor_map[tb]
                pos = Mw_arm_early @ Vector(getattr(arm_obj.data.bones[a_src], f"{a_end}_local")) + Vector(a_off)
            else:
                pos = Mw_arm_early @ Vector(arm_obj.data.bones[src_name].head_local)
            donor_vg = body.vertex_groups[donor]
            target_vg = body.vertex_groups[tb]
            pool = sorted(
                ((Vector(v.co) - pos).length, v.index) for v in body.data.vertices
                if any(g.group == donor_vg.index and g.weight > 0.01 for g in v.groups)
            )
            n_take = min(5, len(pool))
            for _, vi in pool[:n_take]:
                target_vg.add([vi], 0.02, "ADD")
            rescued[tb] = n_take
        dead = still_dead
        if rescued:
            report["원본이 애초에 무가중치라 donor 뼈 정점에 씨앗(0.02, donor 유지)"] = rescued
            counts = {t: 0 for t in targets}
            for v in body.data.vertices:
                for g in v.groups:
                    if g.weight > 0.01:
                        counts[body.vertex_groups[g.group].name] += 1
            dead = [k for k, c in counts.items() if c == 0 and k not in allow_dead]
    report["뼈별 정점(w>0.01)"] = counts
    assert not dead, f"{name}: 가중치 없는 뼈(접은 뒤) {dead}"

    Mw_arm = arm_obj.matrix_world
    old_head_tail = {b.name: (Mw_arm @ Vector(b.head_local), Mw_arm @ Vector(b.tail_local)) for b in arm_obj.data.bones}
    new_arm_data = bpy.data.armatures.new("Armature")
    new_arm_obj = bpy.data.objects.new("Armature", new_arm_data)
    scene.collection.objects.link(new_arm_obj)
    bpy.context.view_layer.objects.active = new_arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    src_bone_of = {v: k for k, v in rename.items()
                   if (v in dict(SPINE) or v in (side + n for side in ("Left", "Right") for n in LIMB_NAMES))
                   and "Finger" not in k}
    pos_override = cfg.get("bone_position_override", {})
    # 🔴 크로커다일 재작업(2026-09-22, PM 재반려 뒤 실측) — 기존 pos_override는 "다른 실존
    # 뼈의 head/tail 그대로"만 가리킬 수 있어서 두 가지를 표현 못 했다:
    # 1) 두 실존 뼈의 head가 좌표까지 똑같아(길이 0) 그 사이에 자리표시를 두면 방향이 안
    #    정해지는 경우(Spine1) — bone_position_lerp로 그 뼈 "자기 자신"의 head→tail 구간을
    #    비율(fraction)로 나눠 쓴다(실존 길이를 반으로 쪼개 양쪽 다 0이 아니게).
    # 2) 목표 위치가 어떤 실존 뼈에도 없고 메시 실측으로만 나오는 경우(LeftHand — 원본
    #    레스트가 발 근처로 깨져 있어 갈고리 메시 자체의 바깥쪽 끝을 실측해 썼다) —
    #    bone_position_offset으로 pos_override/기본 위치에 고정 델타(같은 로컬 단위)를
    #    더한다. 둘 다 cfg에 없으면 이전과 완전히 같은 결과(다른 캐릭터엔 영향 없음).
    lerp_override = cfg.get("bone_position_lerp", {})
    pos_offset = cfg.get("bone_position_offset", {})

    def head_of(tname):
        if tname in lerp_override:
            src, frac = lerp_override[tname]
            h, t = old_head_tail[src]
            base = h.lerp(t, frac)
        elif tname in pos_override:
            src, end = pos_override[tname]
            base = old_head_tail[src][0 if end == "head" else 1]
        else:
            base = old_head_tail[src_bone_of[tname]][0]
        if tname in pos_offset:
            base = base + Vector(pos_offset[tname])
        return base

    # 🔴 말단 뼈(체인에 다음 뼈가 없는 것 — 척추면 Head, 팔다리면 Hand/ToeBase) tail도 소스
    # 뼈 자신의 tail이 깨져 있으면(크로커다일 LeftHand 재작업) 못 쓴다. tail_offset이 있으면
    # head_of(tname) + 그 델타로 대체(다른 캐릭터는 cfg에 없으니 이전과 동일).
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

    # 🔴 PM 실측(이치고, 유니티 Idle 반투명 반려) — 내보내기 직전 전 재질의 Alpha를 강제한다.
    for m in bpy.data.materials:
        force_opaque(m)
    report["재질 Alpha 강제(OPAQUE)"] = sorted(m.name for m in bpy.data.materials if m.use_nodes)

    # 🔴 크로커다일 — TEX_IMAGE 노드가 그래프에 여럿 있어도 실제 Base Color에 닿는 건 하나뿐일
    # 수 있다(직접 확인: 재질 노드에 TEX_IMAGE 2개+MATH 1개가 있고, Base Color 소켓의
    # from_node가 TEX_IMAGE가 아니라 그 MATH 노드였다 — 처음에 "from_node가 TEX_IMAGE가
    # 아니면 못 지운다"고 잘못 짐작해 둘 다 안 지워지는 버그를 냈었다, 재현 후 되돌림). 지금은
    # 그래프를 건드리지 않고, Base Color에서부터 링크를 거슬러 올라가 실제로 닿는 이미지만
    # "쓰는 이미지" 집합에 넣어 저장 대상을 거른다(PM 지시대로 base color 한 장만).
    def _upstream_images(socket, seen=None):
        seen = seen if seen is not None else set()
        for link in socket.links:
            n = link.from_node
            if id(n) in seen:
                continue
            seen.add(id(n))
            if n.type == "TEX_IMAGE" and n.image:
                yield n.image
            for inp in n.inputs:
                yield from _upstream_images(inp, seen)

    used_images = set()
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            continue
        used_images.update(img.name for img in _upstream_images(bsdf.inputs["Base Color"]))

    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    # 🔴 크로커다일(glb 소스, 2026-09-18) — glb 임베디드 이미지는 filepath 자체가 비어
    # 있어(패킹된 데이터만 있음) 위 "파일 경로에서 복사" 분기가 통째로 건너뛰어져 Textures/가
    # 빈 채로 나갔다(직접 확인: 내보낸 FBX를 다시 열어 보니 이미지 size=(0,0)). filepath가
    # 없으면 이미 메모리에 있는 픽셀 데이터를 img.save()로 직접 저장한다(gen_objrip_skin.py의
    # glb 처리와 같은 원리).
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        for node in m.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and node.image.name in used_images:
                img = node.image
                src_path = bpy.path.abspath(img.filepath) if img.filepath else None
                if src_path and os.path.exists(src_path):
                    dst_path = os.path.join(tex_dir, os.path.basename(src_path))
                    if not os.path.exists(dst_path):
                        with open(src_path, "rb") as f, open(dst_path, "wb") as g:
                            g.write(f.read())
                    img.filepath = dst_path
                    img.reload()
                else:
                    # 🔴 glb 패킹 이미지는 has_data=False인 채로 남아 있어(직접 확인, 원본
                    # 바이트는 packed_files에 있지만 픽셀 버퍼는 지연 로드) img.save()가
                    # 그냥 빈 파일을 썼다(0바이트, 크로커다일에서 재현). img.pixels[0]을 한 번
                    # 읽으면 그 시점에 강제로 디코딩돼 has_data가 True로 바뀐다 — 그다음
                    # save()가 정상 작동.
                    if not img.has_data:
                        _ = img.pixels[0]
                    if img.has_data:
                        safe = "".join(c for c in img.name if c.isalnum() or c in "._-") or m.name
                        if not safe.lower().endswith((".png", ".jpg", ".jpeg")):
                            safe += ".png"
                        dst_path = os.path.join(tex_dir, safe)
                        img.filepath_raw = dst_path
                        img.file_format = "PNG"
                        img.save()
                        img.filepath = dst_path
                        img.name = os.path.basename(dst_path)

    uv = body.data.uv_layers.active
    uvs = np.array([d.uv for d in uv.data])
    uv_range = (float(uvs[:, 0].max() - uvs[:, 0].min()), float(uvs[:, 1].max() - uvs[:, 1].min()))
    uv_area = uv_range[0] * uv_range[1]
    report["UV0 범위"] = uv_range
    report["UV0 면적"] = round(uv_area, 4)
    assert uv_area > 0.05, f"{name}: UV0가 퇴화됐다(면적 {uv_area:.4f})"

    os.makedirs(os.path.dirname(dst), exist_ok=True)
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
