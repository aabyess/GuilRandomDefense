"""유닛 모델 정리 재내보내기(PM 2026-09-13, 사장님 「유닛들 눕혀져 있거나 그런 거 확인하고 고쳐」). blender 세션.
    blender -b --factory-startup --python fix_unit_fbx.py -- [이름 ...] [--out 폴더] [--blend]
이름을 안 주면 UNITS 표 전부. --out을 주면 그 폴더에 쓰고(시험), 없으면 같은 경로·같은 파일명으로 덮어쓴다(.meta GUID 유지, .meta는 안 건드림).
--blend: 내보내기 직전 장면을 <출력>_진단.blend로 남긴다.

유니티에서 본 문제(PM 실측): 1) 키 약 0.02 + Idle 리타게팅 실패 — glTF→FBX 변환본의 겉싸개 노드에 배율 0.01·0.0001·100·±90°가 겹겹이
  2) Idle 입히면 누움(흔함_문필환)  3) 메시 납작(안흔함_박민수)  4) 너무 작음(상붕카)

방식 — 연산자로 굽지 않고 **다시 짓는다**
  🔴 1차(블렌더 「변환 적용」·「자세를 쉬는 자세로」): 배율 걸린 아마추어에서 스킨이 틀어져 조각이 수백 m 흩어졌다(박준희 y −850).
  · 파일을 애니메이션 없이 읽은 **기본 자세**(유니티가 보여 주는 모습)를 원본으로 삼는다.
    뼈: 세계 자세 행렬의 머리·꼬리·회전. 메시: 기본 자세가 쉬는 자세와 같으면 원래 데이터(모양 키 포함), 다르면 자세대로 변형한 모양.
    🔴 2차: 쉬는 자세로 뼈를 지었더니 재규어(뿌리 뼈 기본 자세에 배율 10)의 클립이 10배로 늘어났다 — 기본 자세로 짓는다.
  · 새 아마추어: 같은 뼈 이름·부모, 행렬 G(바닥·가운데 이동 × 배율 × 방향)를 곱해 짓는다. 메시는 G × 세계 행렬로 굽고 새 아마추어에만 스킨.
    뼈에 매달린 굳은 조각은 그 뼈에 가중치 1. 겉싸개 빈 오브젝트·다른 아마추어는 옮기지 않는다(0개). 재질 이름 그대로.
  · 방향: 사람형은 엉덩이→머리 = +Z, 좌우 허벅지·어깨로 오른쪽을 잡아 앞(위 × 오른쪽) = −Y. 재규어는 머리 쪽 = −Y, 자전거는 그대로.
  · 크기: 사람형 키 1.8m, 볼보이 키 1.2m, 재규어 몸길이 2.0m, 자전거 길이 1.8m. 발바닥 z=0, 사람형은 엉덩이, 나머지는 경계 가운데가 원점.
  · 🛡 뼈와 메시가 같은 자리에 없으면(블렌더 FBX 가져오기가 assimp 변환본의 스킨 결합을 못 살린 경우) 쓰지 않고 멈춘다.
  · 클립: Generic 둘(강재규·이호준)은 `_자체.controller`가 파일 안 클립을 쓴다 → 원본을 애니메이션째 읽어 프레임마다 세계 뼈 행렬을 뽑아
    새 뼈대의 자세로 다시 굽는다. 사람형은 공용 Idle을 리타게팅하므로(importAnimation 0) 싣지 않는다.
  · 🔴 3차(흔함_문필환 Humanoid 실패): FBX의 Null 노드(가중치 없는 Biped 팔다리·손가락 45개, _end·HELPER 등)는 블렌더가 뼈 밑에 매단
    빈 오브젝트로 읽는다. 그걸 겉싸개로 알고 지웠더니 유니티가 Thigh·UpperArm을 못 찾았다 — 부모 사슬이 아마추어에 닿는 빈 오브젝트는
    같은 이름·같은 세계 자세의 뼈로 살린다(가중치 없음). glb에서 다시 짓는 넷은 기준 FBX에 있던 이름만 살린다.
  · 🔴 테이크 이름은 원본 그대로 — 유니티 클립 ID가 이름에서 나오고 `_자체.controller`가 그 ID를 가리킨다. 블렌더 FBX 가져오기가 붙이는
    「아마추어|」 한 칸만 떼고, 내보내기의 「오브젝트|액션」 이름 짓기를 액션 이름만 쓰게 바꿔 끼운다.
  · 🔴 텍스처(김수빈 흰색): glb 이미지는 경로 없는 내장 이미지라 내보내기가 참조를 「Image_0」(확장자 없음)로 쓰거나 빼먹었다(최상호·박민수 0개).
    glb에서 다시 짓는 넷은 기준 FBX의 재질별 텍스처 표(DiffuseColor·NormalMap → 파일명)를 원시로 읽어, 재질을 그 표대로 다시 짜고
    같은 폴더 Textures/의 실제 파일을 물린다(재질 이름·기본색 그대로).
  · 🔴 원본은 git 커밋에서 꺼낸다(rev = 유닛을 처음 들인 커밋). 같은 경로를 덮어쓰므로, 다시 돌리면 이미 정리된 파일(보조 노드·텍스처가 빠진)을
    원본으로 삼게 된다 — 임시 폴더에 꺼내고 Textures/는 유닛 폴더를 링크한다.
  · clip_ground(이호준, PM 결정 2026-09-13): 원본 좀비 클립은 몸을 낮춰 발이 쉬는 자세 바닥보다 0.11~0.14m 묻힌다(원본 glb 실측 그대로).
    유니티 편집 중엔 동작을 못 틀어 높이를 맞출 수 없으니 원천에서 — 프레임마다 변형된 메시 최저점을 z=0에 닿게 뿌리 뼈 위치 키를 옮긴다.
  · 재질을 새로 짜는 유닛(2026-09-14 사장님 「회색」 제보, PM 요청) — cfg materials:
    mesh_material {메시: 재질} = 그 메시 전부 한 재질 / face_runs = 면 순서대로 (재질, 개수) 연속 구간 / textures = relink_textures 표.
    황정기: Mixamo 교체본(4fe82fb5)에 재질이 0개 — 옛 원본(4c92dba1)의 재질 「173texture.jpg」(Diffuse 173texture.jpg · Normal 173_Norm.jpg)를 다시 짠다.
    신문철: Mixamo가 재질을 버렸다 — 원본 Naruto.obj(Wii Clash of Ninja Rev 3 추출, 면 3,537·정점 1,941)의 usemtl 묶음을
    리깅본 면에 정점 번호 집합으로 대조(3,537/3,537 일치, 겹침 0)해 NARUTO_FACE_RUNS로 적어 뒀다(휴지통 원본이 없어도 재현).
    원본 MTL의 nrta_tex2는 텍스처가 nrt_tex02와 같아 합쳤다(Kd 색은 내보내기 잔재). hige·nrt_tex10/20·오라·툰 맵은 이 모델이 안 쓴다.
  · 내보내기: FBX 단위 적용(1m = 유니티 1), 앞 −Z·위 Y(블렌더 −Y 정면 → 유니티 +Z), 끝 뼈 안 붙임. 상붕카는 원래 .glb(glTFast)라 GLB로.

원본 glb에서 다시 짓는 넷(PM 결정 2026-09-13 — 블렌더가 이 FBX들의 스킨 결합을 못 살려서)
  지금 FBX를 **기준**으로 읽어 이름을 맞춘다: 메시 이름·재질 이름·뼈 이름·뼈 부모가 기준과 똑같아야 통과(assert).
  SOURCE.txt의 이전 손질을 재현한다 — 박민수: 포치타 아마추어·메시와 외곽선 껍데기 제거, 척추 이름 6개. 최상호: 뼈 이름 22개,
  발·손 노드를 무릎·팔꿈치 밑으로(기준 FBX의 부모를 그대로 따른다). 김수빈: 재질 Baked_All.001 → Baked_All. 공통: 조명용 Icosphere 제거.
  안흔함_박준희는 원본(siren_head.glb)이 없어 파일을 건드리지 않는다 — PM이 유니티 쪽에서 맞춘다.
"""
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
DL = os.path.expanduser("~/Downloads")
LUFFY_RENAME = {
    "root hips_429": "Hips", "spine lower_428": "Spine", "spine middle_427": "Chest", "spine upper_426": "UpperChest",
    "head neck lower_217": "Neck", "head neck upper_216": "Head",
    "leg *side* thigh.L_19": "LeftUpperLeg", "leg *side* knee.L_11": "LeftLowerLeg", "leg *side* ankle.L.001_434": "LeftFoot",
    "leg *side* toes.L_433": "LeftToes", "leg *side* thigh.R_37": "RightUpperLeg", "leg *side* knee.R_29": "RightLowerLeg",
    "leg *side* ankle.R.001_445": "RightFoot", "leg *side* toes.R_444": "RightToes",
    "arm *side* shoulder 1.L_245": "LeftShoulder", "arm *side* shoulder 2.L_224": "LeftUpperArm", "arm *side* elbow.L_220": "LeftLowerArm",
    "arm *side* wrist.L_243": "LeftHand", "arm *side* shoulder 1.R_273": "RightShoulder", "arm *side* shoulder 2.R_252": "RightUpperArm",
    "arm *side* elbow.R_248": "RightLowerArm", "arm *side* wrist.R_271": "RightHand",
}
DENJI_RENAME = {"spine_09": "Hips", "spine.001_010": "Spine", "spine.002_011": "Chest", "spine.003_012": "UpperChest",
                "spine.004_013": "Neck", "spine.005_014": "Head"}
def _biped_arm_reparent():
    """흔함_문필환: 팔·손 메시를 움직이는 BN_ 보조 뼈가 전부 Clavicle 직계 자식으로 나란히 붙어 있다(3ds Max 제약으로 Biped를 따라가던 뼈).
    유니티 사람형은 Bip001 UpperArm·Forearm·Hand·Finger(가중치 0)를 돌리므로, 위치가 같은 Biped 마디 밑으로 옮겨야 메시가 따라간다(2026-09-14 실측)."""
    table = {}
    for side in "LR":
        for k, seg in ((1, "UpperArm"), (2, "UpperArm"), (3, "UpperArm"), (4, "Forearm"), (5, "Forearm"), (6, "Forearm"), (7, "Hand")):
            table[f"BN_Arm_{side}0{k}"] = f"Bip001 {side} {seg}"
        for k in (1, 2):
            table[f"BN_BiXiu_{side}0{k}"] = f"Bip001 {side} Forearm"
        for i, sfx in enumerate(("0", "01", "02", "1", "11", "12", "2", "21", "22", "3", "31", "32", "4", "41", "42"), start=1):
            table[f"BN_Finger_{side}{i}"] = f"Bip001 {side} Finger{sfx}"
    return table


BIPED_ARM_REPARENT = _biped_arm_reparent()


def _biped_leg_reparent():
    """다리도 같은 구조(2026-09-14 실측): 다리·발 정점을 움직이는 BN_Cal·BN_TuiXiu·BN_Toe가 전부 Bip001 Pelvis 직계 자식으로 나란히 붙어 있다.
    BN_Cal 01~03 = 넓적다리 구간, 04 = 무릎(Calf 머리)~06 = 종아리 구간, 07 = 발목(Foot 머리), TuiXiu = 무릎, BN_Toe 1 = 발끝(Toe0 머리)."""
    table = {}
    for side in "LR":
        for k, seg in ((1, "Thigh"), (2, "Thigh"), (3, "Thigh"), (4, "Calf"), (5, "Calf"), (6, "Calf"), (7, "Foot")):
            table[f"BN_Cal_{side}0{k}"] = f"Bip001 {side} {seg}"
        for k in (1, 2):
            table[f"BN_TuiXiu_{side}0{k}"] = f"Bip001 {side} Calf"
        table[f"BN_Toe_{side}1"] = f"Bip001 {side} Toe0"
    return table


BIPED_LIMB_REPARENT = dict(BIPED_ARM_REPARENT, **_biped_leg_reparent())
# 특별함_황정기(우솝 오니가시마, Fighting Path Biped, 2026-09-14): Twist 뼈가 팔다리 뼈의 형제로 붙어 있다(ThighTwist ← Spine, CalfTwist ← Thigh,
#   UpArmTwist ← Clavicle, ForeTwist ← UpperArm). 유니티가 Thigh·Calf·UpperArm·Forearm(가중치 0)을 돌리면 메시가 안 따라가니 그 뼈 밑으로 옮긴다.
#   Bone001(따로 서 있는 새총 지팡이, 1,246정점)은 Bip001(무게중심, 뺀다) 밑이라 뿌리로 떨어진다 → 몸을 따라가게 Pelvis 밑으로.
USOPP_REPARENT = {f"Bip001 {s}{twist}": f"Bip001 {s} {limb}" for s in "LR"
                  for twist, limb in (("ThighTwist", "Thigh"), ("CalfTwist", "Calf"), ("UpArmTwist", "UpperArm"))}
USOPP_REPARENT.update({f"Bip001 {s} ForeTwist": f"Bip001 {s} Forearm" for s in "LR"})
USOPP_REPARENT["Bone001"] = "Bip001 Pelvis"
# 바운티러시 pl_ 리그 사람형 뼈 → mixamorig 이름(2026-09-14 PM 유니티): 모리아에서 자동 매핑이 망토 뼈 l_cloak_Clavicle을 Jaw·LeftEye로 겹쳐 잡아
#   「Found duplicate transform」로 아바타가 실패했다 — 매핑 뼈 이름을 표준으로 박아 모호함을 없앤다. 보조 뼈(망토·후드·치마·무기)는 이름 그대로.
PL_RENAME = {"Body_Pelvis": "mixamorig:Hips", "Body_Belly": "mixamorig:Spine", "Body_Chest": "mixamorig:Spine1", "Head_Neck": "mixamorig:Neck",
             "Head_Face": "mixamorig:Head"}
for _s, _side in (("L", "Left"), ("R", "Right")):
    PL_RENAME.update({f"{_s}Arm_Clavicle": f"mixamorig:{_side}Shoulder", f"{_s}Arm_Upper": f"mixamorig:{_side}Arm", f"{_s}Arm_Fore": f"mixamorig:{_side}ForeArm",
                      f"{_s}Hand_Palm": f"mixamorig:{_side}Hand", f"{_s}Leg_Thigh": f"mixamorig:{_side}UpLeg", f"{_s}Leg_Calf": f"mixamorig:{_side}Leg",
                      f"{_s}Foot_Heel": f"mixamorig:{_side}Foot", f"{_s}Foot_Toe": f"mixamorig:{_side}ToeBase"})
# 요크(바운티러시 pl_ 리그, 2026-09-15): PL_RENAME과 같되 발끝 LFoot_Toe·RFoot_Toe가 뼈가 아니라 빈 오브젝트(Heel 자식) → ToeBase 없이.
#   다리는 L/RLeg_Root(가중치 0) → L/RLeg_Thigh로 한 칸 더 — UpLeg는 Thigh(Root는 Hips와 UpLeg 사이 중간 뼈).
YORK_RENAME = {k: v for k, v in PL_RENAME.items() if not k.endswith("Foot_Toe")}
# 기사(strong_knight.glb, 2026-09-15): 번호 꼬리 조인트 → mixamorig. anke(발목, 원본 오타)→Foot · foot(발볼)→ToeBase · toes는 끝.
#   손가락 thumb/point/middle/ring/pink 1~3 → HandThumb/Index/Middle/Ring/Pinky 1~3(tpose_arms 손바닥 굴리기). Joint_3_*·tip은 끝 조인트(가중치 0).
KNIGHT_RENAME = {"hips_01": "mixamorig:Hips", "spine_012": "mixamorig:Spine", "chest_013": "mixamorig:Spine1", "neck_061": "mixamorig:Neck", "head_062": "mixamorig:Head"}
for _s, _side, _n, _fingers in (("L", "Left", (14, 15, 16, 17, 2, 3, 4, 5), {"thumb": 18, "pink": 22, "ring": 26, "middle": 30, "point": 34}),
                                ("R", "Right", (38, 39, 40, 41, 7, 8, 9, 10), {"thumb": 42, "pink": 45, "ring": 49, "middle": 53, "point": 57})):
    KNIGHT_RENAME.update({f"{_s}_shoulder_0{_n[0]}": f"mixamorig:{_side}Shoulder", f"{_s}_arm_0{_n[1]}": f"mixamorig:{_side}Arm",
                          f"{_s}_elbow_0{_n[2]}": f"mixamorig:{_side}ForeArm", f"{_s}_wrist_0{_n[3]}": f"mixamorig:{_side}Hand",
                          f"{_s}_leg_0{_n[4]}": f"mixamorig:{_side}UpLeg", f"{_s}_knee_0{_n[5]}": f"mixamorig:{_side}Leg",
                          f"{_s}_anke_0{_n[6]}": f"mixamorig:{_side}Foot", f"{_s}_foot_0{_n[7]}": f"mixamorig:{_side}ToeBase"})
    for _f, _start in _fingers.items():
        _fn = {"thumb": "Thumb", "point": "Index", "middle": "Middle", "ring": "Ring", "pink": "Pinky"}[_f]
        for _k in (1, 2, 3):
            KNIGHT_RENAME[f"{_s}_{_f}{_k}_0{_start + _k - 1}"] = f"mixamorig:{_side}Hand{_fn}{_k}"
# 미호크(특별함_박기찬, 2026-09-14): 뼈 이름이 공백식(body lower·arm left arm1…) — 사람형 매핑 뼈만 mixamorig로. 손가락·얼굴·모자·검 뼈는 그대로.
MIHAWK_RENAME = {"body lower": "mixamorig:Hips", "body upper": "mixamorig:Spine", "body spine1": "mixamorig:Spine1", "body spine2": "mixamorig:Spine2",
                 "head neck": "mixamorig:Neck", "head head": "mixamorig:Head"}
for _s, _side in (("left", "Left"), ("right", "Right")):
    MIHAWK_RENAME.update({f"arm {_s} shoulder": f"mixamorig:{_side}Shoulder", f"arm {_s} arm1": f"mixamorig:{_side}Arm",
                          f"arm {_s} arm2": f"mixamorig:{_side}ForeArm", f"arm {_s} hand": f"mixamorig:{_side}Hand",
                          f"leg {_s} thigh": f"mixamorig:{_side}UpLeg", f"leg {_s} calf": f"mixamorig:{_side}Leg",
                          f"leg {_s} foot": f"mixamorig:{_side}Foot", f"leg {_s} toes": f"mixamorig:{_side}ToeBase"})
_MH = "~/Downloads/mihawk/textures/mpr_bound_character_mplc014mihawk_"
# 가로우(원펀맨 게임 추출 glb, 2026-09-14): 사람형 매핑 뼈를 mixamorig로. CLANK = 종아리, TOE1 = 발, TOE2 = 발끝. ROLL(트위스트)은 이미 팔다리 뼈의 자식.
GAROU_RENAME = {"WAIST_079": "mixamorig:Hips", "SPINE1_074": "mixamorig:Spine", "SPINE2_072": "mixamorig:Spine1", "SPINE3_068": "mixamorig:Spine2",
                "NECK_010": "mixamorig:Neck", "HEAD_02": "mixamorig:Head"}
for _side, _ids in (("Left", dict(CLAVICLE="CLAVICLE_L_069", SHOULDER="SHOULDER_L_071", ELBOW="ELBOW_L_084", WRIST="WRIST_L_086", THIGH="THIGH_L_082",
                                  CLANK="CLANK_L_0139", TOE1="TOE1_L_0127", TOE2="TOE2_L_0140",
                                  fingers={"Thumb": ("F_THUMB1_L_087", "F_THUMB2_L_0106", "F_THUMB3_L_0117"), "Index": ("F_FORE1_L_00", "F_FORE2_L_01", "F_FORE3_L_0108"),
                                           "Middle": ("F_MIDDLE1_L_0107", "F_MIDDLE2_L_0115", "F_MIDDLE3_L_0116"),
                                           "Ring": ("F_MEDICINAL1_L_0109", "F_MEDICINAL2_L_0110", "F_MEDICINAL3_L_0114"),
                                           "Pinky": ("F_LITTLE1_L_0111", "F_LITTLE2_L_0112", "F_LITTLE3_L_0113")})),
                    ("Right", dict(CLAVICLE="CLAVICLE_R_073", SHOULDER="SHOULDER_R_076", ELBOW="ELBOW_R_088", WRIST="WRIST_R_090", THIGH="THIGH_R_083",
                                   CLANK="CLANK_R_0138", TOE1="TOE1_R_0132", TOE2="TOE2_R_0141",
                                   fingers={"Thumb": ("F_THUMB1_R_091", "F_THUMB2_R_0103", "F_THUMB3_R_0105"), "Index": ("F_FORE1_R_096", "F_FORE2_R_097", "F_FORE3_R_099"),
                                            "Middle": ("F_MIDDLE1_R_098", "F_MIDDLE2_R_0102", "F_MIDDLE3_R_0104"),
                                            "Ring": ("F_MEDICINAL1_R_093", "F_MEDICINAL2_R_0100", "F_MEDICINAL3_R_0101"),
                                            "Pinky": ("F_LITTLE1_R_092", "F_LITTLE2_R_094", "F_LITTLE3_R_095")}))):
    GAROU_RENAME.update({_ids["CLAVICLE"]: f"mixamorig:{_side}Shoulder", _ids["SHOULDER"]: f"mixamorig:{_side}Arm", _ids["ELBOW"]: f"mixamorig:{_side}ForeArm",
                         _ids["WRIST"]: f"mixamorig:{_side}Hand", _ids["THIGH"]: f"mixamorig:{_side}UpLeg", _ids["CLANK"]: f"mixamorig:{_side}Leg",
                         _ids["TOE1"]: f"mixamorig:{_side}Foot", _ids["TOE2"]: f"mixamorig:{_side}ToeBase"})
    for _finger, _chain in _ids["fingers"].items():
        GAROU_RENAME.update({_bone: f"mixamorig:{_side}Hand{_finger}{_i}" for _i, _bone in enumerate(_chain, start=1)})
# 레이쥬(게임 이벤트 모델 glb, 뼈 이름 전부 익명 bone_N, 2026-09-14): 계층·세계 위치로 사람형 뼈를 찾았다. +X = 왼쪽(발끝 −Y). 손가락은 사슬이 모호해 이름 그대로.
REIJU_RENAME = {"bone_2_04": "mixamorig:Hips", "bone_3_05": "mixamorig:Spine", "bone_4_06": "mixamorig:Spine1", "bone_5_07": "mixamorig:Spine2",
                "bone_6_08": "mixamorig:Neck", "bone_7_09": "mixamorig:Head",
                "bone_16_018": "mixamorig:LeftShoulder", "bone_18_020": "mixamorig:LeftArm", "bone_20_022": "mixamorig:LeftForeArm", "bone_22_024": "mixamorig:LeftHand",
                "bone_17_019": "mixamorig:RightShoulder", "bone_19_021": "mixamorig:RightArm", "bone_21_023": "mixamorig:RightForeArm", "bone_23_025": "mixamorig:RightHand",
                "bone_8_010": "mixamorig:LeftUpLeg", "bone_10_012": "mixamorig:LeftLeg", "bone_12_014": "mixamorig:LeftFoot", "bone_14_016": "mixamorig:LeftToeBase",
                "bone_9_011": "mixamorig:RightUpLeg", "bone_11_013": "mixamorig:RightLeg", "bone_13_015": "mixamorig:RightFoot", "bone_15_017": "mixamorig:RightToeBase"}
# 사보(바운티러시 pl_ 리그 glb, 뼈 이름에 Sketchfab 번호 꼬리): PL_RENAME과 같은 매핑을 꼬리 붙은 이름에. 나머지 꼬리는 rename_regex로 뗀다.
SABO_RENAME = {"Body_Pelvis_07": "mixamorig:Hips", "Body_Belly_08": "mixamorig:Spine", "Body_Chest_09": "mixamorig:Spine1", "Head_Neck_010": "mixamorig:Neck",
               "Head_Face_011": "mixamorig:Head",
               "LArm_Clavicle_012": "mixamorig:LeftShoulder", "LArm_Upper_013": "mixamorig:LeftArm", "LArm_Fore_014": "mixamorig:LeftForeArm", "LHand_Palm_016": "mixamorig:LeftHand",
               "RArm_Clavicle_020": "mixamorig:RightShoulder", "RArm_Upper_021": "mixamorig:RightArm", "RArm_Fore_022": "mixamorig:RightForeArm", "RHand_Palm_024": "mixamorig:RightHand",
               "LLeg_Thigh_033": "mixamorig:LeftUpLeg", "LLeg_Calf_034": "mixamorig:LeftLeg", "LFoot_Heel_035": "mixamorig:LeftFoot", "LFoot_Toe_036": "mixamorig:LeftToeBase",
               "RLeg_Thigh_037": "mixamorig:RightUpLeg", "RLeg_Calf_038": "mixamorig:RightLeg", "RFoot_Heel_039": "mixamorig:RightFoot", "RFoot_Toe_040": "mixamorig:RightToeBase"}
# 이토시 린(프리파이어 코스튬 FBX, 2026-09-14): bone_ 이름 → mixamorig. bone_Hips는 가중치 0이지만 다리·척추의 부모(엉덩이 정점은 자식 bone_Hips_Dummy가 몬다).
RIN_RENAME = {"bone_Hips": "mixamorig:Hips", "bone_Spine": "mixamorig:Spine", "bone_Spine1": "mixamorig:Spine1", "bone_Neck": "mixamorig:Neck",
              "bone_Head": "mixamorig:Head"}
for _side in ("Left", "Right"):
    RIN_RENAME.update({f"bone_{_side}Clav": f"mixamorig:{_side}Shoulder", f"bone_{_side}Arm": f"mixamorig:{_side}Arm",
                       f"bone_{_side}ForeArm": f"mixamorig:{_side}ForeArm", f"bone_{_side}Hand": f"mixamorig:{_side}Hand",
                       f"bone_{_side}LegUpper": f"mixamorig:{_side}UpLeg", f"bone_{_side}Leg": f"mixamorig:{_side}Leg",
                       f"bone_{_side}Ankle": f"mixamorig:{_side}Foot", f"bone_{_side}Toe": f"mixamorig:{_side}ToeBase"})
_RIN_TEX = "~/Downloads/rin-itoshi-free-fire-skin/textures/Male_{}_Cos_FB2_D.png"
# 가렌(LoL 추출 glb, 2026-09-14): 번호 꼬리 이름 → mixamorig. Root_1(가중치 359)이 Spine1과 Pelvis_41의 부모라 Root_1 = Hips, Pelvis_41은 중간 뼈로 둔다.
#   무릎은 KneeUpper(종아리)·KneeLower(같은 자리 중간 뼈) 둘 — KneeUpper = Leg. 손가락(두 마디)은 매핑 안 함(대검 쥔 모양 유지).
GAREN_RENAME = {"Root_1": "mixamorig:Hips", "Spine1_2": "mixamorig:Spine", "Spine2_3": "mixamorig:Spine1", "Spine3_4": "mixamorig:Spine2",
                "Neck_5": "mixamorig:Neck", "Head_6": "mixamorig:Head"}
for _s, _side, _arm, _leg in (("R", "Right", 7, 42), ("L", "Left", 23, 47)):
    GAREN_RENAME.update({f"{_s}_Clavicle_{_arm}": f"mixamorig:{_side}Shoulder", f"{_s}_Shoulder_{_arm + 1}": f"mixamorig:{_side}Arm",
                         f"{_s}_Elbow_{_arm + 2}": f"mixamorig:{_side}ForeArm", f"{_s}_Hand_{_arm + 3}": f"mixamorig:{_side}Hand",
                         f"{_s}_Hip_{_leg}": f"mixamorig:{_side}UpLeg", f"{_s}_KneeUpper_{_leg + 1}": f"mixamorig:{_side}Leg",
                         f"{_s}_Foot_{_leg + 3}": f"mixamorig:{_side}Foot", f"{_s}_Toe_{_leg + 4}": f"mixamorig:{_side}ToeBase"})
# 탐 켄치(LoL 팬아트 glb, mGear 리그, 2026-09-14): <부위>_<C0/L0/R0>_<이름>_Jnt_<n> → mixamorig. spine 넷 중 03은 중간 뼈(04가 목·어깨의 부모라 Spine2).
#   왼쪽 본 뼈는 가중치 0(leaf_ 자식이 짐)이지만 매핑은 본 뼈 — leaf_는 자식이라 따라간다. 손가락(엄지+둘)·눈·턱은 매핑 안 함.
TAHM_RENAME = {"spine_C0_pelvis_Jnt_01": "mixamorig:Hips", "spine_C0_spine_01_Jnt_02": "mixamorig:Spine", "spine_C0_spine_02_Jnt_03": "mixamorig:Spine1",
               "spine_C0_spine_04_Jnt_05": "mixamorig:Spine2", "neck_C0_0_Jnt_06": "mixamorig:Neck", "neck_C0_head_Jnt_09": "mixamorig:Head"}
for _s, _side, _n in (("R", "Right", (39, 40, 42, 44, 76, 78, 80, 82)), ("L", "Left", (58, 59, 61, 63, 83, 85, 87, 69))):
    TAHM_RENAME.update({f"shoulder_{_s}0_shoulder_Jnt_0{_n[0]}": f"mixamorig:{_side}Shoulder", f"arm_{_s}0_upperarm_Jnt_0{_n[1]}": f"mixamorig:{_side}Arm",
                        f"arm_{_s}0_lowerarm_Jnt_0{_n[2]}": f"mixamorig:{_side}ForeArm", f"arm_{_s}0_hand_Jnt_0{_n[3]}": f"mixamorig:{_side}Hand",
                        f"leg_{_s}0_thigh_Jnt_0{_n[4]}": f"mixamorig:{_side}UpLeg", f"leg_{_s}0_calf_Jnt_0{_n[5]}": f"mixamorig:{_side}Leg",
                        f"leg_{_s}0_foot_Jnt_0{_n[6]}": f"mixamorig:{_side}Foot", f"foot_{_s}0_ball_Jnt_0{_n[7]}": f"mixamorig:{_side}ToeBase"})
# 아이언맨(3ds Max 2020 Biped FBX, 2026-09-15): Bip01 이름 → mixamorig. 척추 넷 중 Spine3가 목·쇄골의 부모라 Spine2(UpperChest), Spine2는 중간 뼈.
#   Twist 뼈·무게중심 뼈 없음(Pelvis가 뿌리). 손가락 다섯 × 세 마디 → HandThumb/Index/Middle/Ring/Pinky 1~3(tpose_arms 손바닥 굴리기에 쓴다).
IRONMAN_RENAME = {"Bip01 Pelvis": "mixamorig:Hips", "Bip01 Spine": "mixamorig:Spine", "Bip01 Spine1": "mixamorig:Spine1", "Bip01 Spine3": "mixamorig:Spine2",
                  "Bip01 Neck": "mixamorig:Neck", "Bip01 Head": "mixamorig:Head"}
for _s, _side in (("L", "Left"), ("R", "Right")):
    IRONMAN_RENAME.update({f"Bip01 {_s} Clavicle": f"mixamorig:{_side}Shoulder", f"Bip01 {_s} UpperArm": f"mixamorig:{_side}Arm",
                           f"Bip01 {_s} Forearm": f"mixamorig:{_side}ForeArm", f"Bip01 {_s} Hand": f"mixamorig:{_side}Hand",
                           f"Bip01 {_s} Thigh": f"mixamorig:{_side}UpLeg", f"Bip01 {_s} Calf": f"mixamorig:{_side}Leg",
                           f"Bip01 {_s} Foot": f"mixamorig:{_side}Foot", f"Bip01 {_s} Toe0": f"mixamorig:{_side}ToeBase"})
    for _i, _finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky")):
        for _j, _k in (("", 1), ("1", 2), ("2", 3)):
            IRONMAN_RENAME[f"Bip01 {_s} Finger{_i}{_j}"] = f"mixamorig:{_side}Hand{_finger}{_k}"
NARUTO_FACE_RUNS = [["nrt_tex02", 450], ["nrt_eye", 62], ["nrt_tex01", 1106], ["nrt_tex02", 1919]]
UNITS = {
    "안흔함_강재규": dict(rev="e8236711", path="Assets/Art/Units/안흔함_강재규/안흔함_강재규.fbx", kind="beast", size=("length", 2.0), anim=True, head="Head_M"),
    "안흔함_이호준": dict(rev="6b2afdbc", path="Assets/Art/Units/안흔함_이호준/안흔함_이호준.fbx", kind="human", size=("height", 1.2), anim=True,
                      hips="Bone_61", head="Bone.004_3", source=os.path.join(DL, "zombi.glb"), recipe={}, clip_ground=True),
    "안흔함_김경현": dict(rev="4c92dba1", path="Assets/Art/Units/안흔함_김경현/안흔함_김경현.fbx", kind="human", size=("height", 1.8)),
    "안흔함_김수빈": dict(rev="c6cc54d4", path="Assets/Art/Units/안흔함_김수빈/안흔함_김수빈.fbx", kind="human", size=("height", 1.8), hips="hips_112",
                      source=os.path.join(DL, "nanachi.glb"),
                      # Object_4 = 몸에서 멀리 떨어진 Rigify FK 위젯 조각(128정점) — 경계 상자를 3.3m로 부풀려 PM 승인으로 뺀다(2026-09-13)
                      recipe=dict(material_alias={"Baked_All.001": "Baked_All"}, drop_meshes=["Object_4"])),
    "안흔함_문필환": dict(rev="81a18fbb", path="Assets/Art/Units/안흔함_문필환/안흔함_문필환.fbx", kind="human", size=("height", 1.8)),
    "안흔함_박준희": dict(path="Assets/Art/Units/안흔함_박준희/안흔함_박준희.fbx", kind="human", size=("height", 1.8),
                      hold="원본 siren_head.glb 없음 — 블렌더 FBX 가져오기가 스킨 결합을 못 살려 파일을 건드리지 않음(PM이 유니티 쪽에서 맞춤)"),
    "안흔함_신문철": dict(rev="483a35ec", path="Assets/Art/Units/안흔함_신문철/안흔함_신문철.fbx", kind="human", size=("height", 1.8),
                      materials=dict(face_runs={"Naruto.001_Naruto": NARUTO_FACE_RUNS},
                                     textures={"nrt_tex01": [("DiffuseColor", "nrt_tex01.png")], "nrt_tex02": [("DiffuseColor", "nrt_tex02.png")],
                                               "nrt_eye": [("DiffuseColor", "nrt_eye.png")]})),
    "안흔함_황정기": dict(rev="4fe82fb5", path="Assets/Art/Units/안흔함_황정기/안흔함_황정기.fbx", kind="human", size=("height", 1.8),
                      materials=dict(mesh_material={"173_2": "173texture.jpg"},
                                     textures={"173texture.jpg": [("DiffuseColor", "173texture.jpg"), ("NormalMap", "173_Norm.jpg")]})),
    # 🔸 기존 pl_ 여섯(2026-09-14 PM): 표정·손 변형 메시가 겹친 채 게임에 들어가 있었다 → 기본만 남기고, 매핑 뼈 이름을 PL_RENAME(모리아에서 유니티 통과)으로.
    #    로(양재모) 무기 3: weapon_01 = 오른손에 뽑은 칼날(앞으로), weapon_02 = 왼손 칼집(술 달림, 뒤로) + weapon_03 = 코등이·손잡이 — 02+03이 칼집에 든 기본 모습.
    "특별함_양재모": dict(rev="2d515a55", path="Assets/Art/Units/특별함_양재모/특별함_양재모.fbx", kind="human", size=("height", 1.8),
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_open_death_01", "l_hand_open_death_02",
                                   "l_hand_open_death_03", "weapon_01"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    "특별함_최상호": dict(rev="b037f72d", path="Assets/Art/Units/특별함_최상호/특별함_최상호.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "luffy.glb"),
                      # mesh_0(Pupil 582정점·모양 키 3) = Object_7, mesh_0.001(shock 60정점·모양 키 3) = Object_8
                      recipe=dict(rename=LUFFY_RENAME, mesh_alias={"mesh_0": "Object_7", "mesh_0.001": "Object_8"})),
    # 🔴 원인 3겹(2026-09-14 PM 유니티 확인): ①Biped 무게중심 Bip001이 Hips 위에 끼어 엉덩이 높이가 바닥으로 저장 ②팔·다리 메시를 BN_ 보조 뼈가
    #    Pelvis/Clavicle에 나란히 붙어 몰았다 ③살린 Null 뼈 틀 규약이 Biped와 섞여 아바타 skeleton 90° + A자 쉬는 자세 → 넷을 다 켠다(outT3와 같은 설정)
    "흔함_문필환": dict(rev="01d46427", path="Assets/Art/Units/흔함_문필환/흔함_문필환.fbx", kind="human", size=("height", 1.8),
                    drop_bones=["Bip001"], reparent_bones=BIPED_LIMB_REPARENT, tpose_arms=True, null_frames_from_node=True),
    "안흔함_박민수": dict(rev="dd84a0cb", path="Assets/Art/Units/안흔함_박민수/안흔함_박민수.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "denji_and_pochita.glb"), gltf_guess_bind=False,
                      recipe=dict(rename=DENJI_RENAME)),
    "안흔함_상붕카": dict(path="Assets/Art/Characters/안흔함_상붕카.glb", kind="prop", size=("length", 1.8)),
    # 사이타마(Ready Player Me·Mixamo 리그 glb, 2026-09-14): 번호 꼬리를 떼고 mixamorig 이름으로. 쉬는 자세 A자(위팔 수평 아래 59°, 아래팔 앞 33°) → T자
    #   (손가락 네 줄이 다 있어 손바닥 굴리기까지). 조명용 Icosphere 뺌. Wolf3D_Body 베이스는 1×1 단색 jpg — 원본 바이트 그대로(유니티가 읽음).
    "특별함_배성령": dict(path="Assets/Art/Units/특별함_배성령/특별함_배성령.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "saitama_everyday_opm_-_opm.glb"), no_nulls=True, drop_meshes=["Icosphere"],
                      rename_regex=(r"([A-Za-z][A-Za-z0-9_]*?)_[0-9]+", r"mixamorig:\1"), orient_snap=True,
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "Wolf3D_Eye_baseColor.jpg", 1: "Wolf3D_Body_baseColor.jpg", 2: "Wolf3D_Body_normal.jpg", 3: "Wolf3D_Skin_baseColor.jpg",
                                  4: "Wolf3D_Outfit_Bottom_baseColor.jpg", 6: "Wolf3D_Outfit_Bottom_normal.jpg", 7: "Wolf3D_Outfit_Footwear_baseColor.jpg",
                                  9: "Wolf3D_Outfit_Footwear_normal.jpg", 10: "Wolf3D_Outfit_Top_baseColor.jpg", 12: "Wolf3D_Outfit_Top_normal.jpg",
                                  13: "Wolf3D_Teeth_baseColor.jpg"},
                      materials=dict(textures={
                          "Wolf3D_Eye": [("DiffuseColor", "Wolf3D_Eye_baseColor.jpg")],
                          "Wolf3D_Body": [("DiffuseColor", "Wolf3D_Body_baseColor.jpg"), ("NormalMap", "Wolf3D_Body_normal.jpg")],
                          "Wolf3D_Skin": [("DiffuseColor", "Wolf3D_Skin_baseColor.jpg")],
                          "Wolf3D_Outfit_Bottom": [("DiffuseColor", "Wolf3D_Outfit_Bottom_baseColor.jpg"), ("NormalMap", "Wolf3D_Outfit_Bottom_normal.jpg")],
                          "Wolf3D_Outfit_Footwear": [("DiffuseColor", "Wolf3D_Outfit_Footwear_baseColor.jpg"), ("NormalMap", "Wolf3D_Outfit_Footwear_normal.jpg")],
                          "Wolf3D_Outfit_Top": [("DiffuseColor", "Wolf3D_Outfit_Top_baseColor.jpg"), ("NormalMap", "Wolf3D_Outfit_Top_normal.jpg")],
                          "Wolf3D_Teeth": [("DiffuseColor", "Wolf3D_Teeth_baseColor.jpg")]})),
    # 이토시 린: 쉬는 자세 A자 50.5° → T자(tpose_arms에 mixamorig 이름표 — 손가락은 순서가 불확실해 손바닥 굴리기 건너뜀). 기본 자세≠쉬는 자세라 굽힌다.
    #   재질 5개가 전부 비어 있다(이미지 없음) → 메시 이름으로 텍스처를 짝지어 재질을 새로. 렌더로 확인: Accessory 메시 = 머리카락(Hair 텍스처),
    #   Accessory_VFX 메시 = 얼굴·귀(Accessory 텍스처 — 이펙트가 아니었다). Cube 메시는 블렌더가 읽지 않음(면 없음).
    "특별함_박진웅": dict(path="Assets/Art/Units/특별함_박진웅/특별함_박진웅.fbx", kind="human", size=("height", 1.8),
                      source=os.path.expanduser("~/Downloads/rin-itoshi-free-fire-skin/source/rin itoshi.fbx"),
                      rename_bones=RIN_RENAME, orient_snap=True,
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                      "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                      copy_textures={_RIN_TEX.format(k): f"Male_{k}_Cos_FB2_D.png" for k in ("Top", "Bottom", "Shoe", "Accessory", "Hair")},
                      materials=dict(mesh_material={"Male_Lobby_Top_Cos_FB2": "Male_Top_Cos_FB2", "Male_Lobby_Bottom_Cos_FB2": "Male_Bottom_Cos_FB2",
                                                    "Male_Lobby_Shoe_Cos_FB2": "Male_Shoe_Cos_FB2", "Male_Lobby_Accessory_Cos_FB2": "Male_Hair_Cos_FB2",
                                                    "Male_Lobby_Accessory_Cos_FB2_VFX": "Male_Accessory_Cos_FB2"},
                                     textures={f"Male_{k}_Cos_FB2": [("DiffuseColor", f"Male_{k}_Cos_FB2_D.png")] for k in ("Top", "Bottom", "Shoe", "Accessory", "Hair")})),
    # 조즈(바운티러시 pl_ 리그, zip 안 7z): 이미 T자. 표정 3·손 2벌 겹침 → face_normal + 주먹(close, 권투형 거구 기본 모습). Cube 메시는 원본에 없음.
    #   텍스처: 7z 안 _diff.tga와 zip의 _diff.png가 픽셀 동일(1024) — 알파 = 명암 마스크(중간값 99%)라 tga에서 알파 뺀 RGB PNG로 새로 쓰고 재질을 거기에 잇는다.
    "특별함_조성진": dict(path="Assets/Art/Units/특별함_조성진/특별함_조성진.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-jozu.zip"), "source/pl_jozu.7z", "pl_jozu/pl_jozu_orig01.fbx"),
                      archive_rgb={"pl_jozu/pl_jozu_orig01_diff.tga": "pl_jozu_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_open", "r_hand_open"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True,
                      materials=dict(textures={"pl_jozu_orig01": [("DiffuseColor", "pl_jozu_orig01_diff.png")]})),
    # 사보 스탬피드(바운티러시 glb): 메시 이름이 전부 Object_N으로 지워져 겹친 변형을 렌더로 판정(idle_a엔 배율 채널이 없어 애니로 못 가림).
    #   (glTF 메시 데이터 이름으로 확인: Object_N = mesh N−6 — 7 face_attack · 8 face_damage · 9 face_normal · 10 goggle · 13 hat_hair · 16/25 glove_open · 20/29 leg · 21 pipe · 22 pipe_weapon · 30/31 sp_leg)
    #   남김: Object_6 body · 9 face_normal · 10 goggle · 11 hair · 12 hat · 13 hat_hair · 16/25 l/r_glove_open · 20/29 l/r_leg · 21 pipe(등에 멤)
    #   뺌: 7 face_attack · 8 face_damage · 14/23 glove_close · 15/24 glove_dragon · 17/26 hand_close · 18/27 hand_dragon · 19/28 hand_open · 22 pipe_weapon · 30/31 sp_leg · Icosphere
    "특별함_박예원": dict(path="Assets/Art/Units/특별함_박예원/특별함_박예원.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "one_oiece_bounty_rush_sabo_stampede.glb"), no_nulls=True,
                      drop_meshes=["Object_7", "Object_8", "Object_14", "Object_15", "Object_17", "Object_18", "Object_19", "Object_22", "Object_23", "Object_24",
                                   "Object_26", "Object_27", "Object_28", "Object_30", "Object_31", "Icosphere"],
                      rename_bones=SABO_RENAME, rename_regex=(r"([A-Za-z][A-Za-z0-9_]*?)_[0-9]+", r"\1"), orient_snap=True,
                      # 🔴 유니티 Idle에서 바지가 코트 앞·옆 트임을 뚫음(PM 09-14) — idle.fbx를 옮겨 입혀 잰 관통 98% 프레임·최대 9cm →
                      #   자락 가중치 85%까지 같은 쪽 넓적다리로(엉덩이→끝 선형) + 코트에 가려진 넓적다리·허리 반지름 ×0.45 → 27% 프레임·정점 1개 최대 2.2cm, 렌더로 안 보임
                      hem_follow=dict(prefix="coat", share=0.85, ramp="linear", thigh_shrink=0.45, shrink_top=0.12, shrink_min_leg=0.15),
                      glb_images={0: "pl_sabo_stam01_diff.png"},
                      materials=dict(textures={"pl_sabo_stam01": [("DiffuseColor", "pl_sabo_stam01_diff.png")]})),
    # 레이쥬: 이미 T자. 뿌리 bone_0_02 > bone_1_03(+ 형제 bone_26_028) — 가중치 0 중간 뼈 → 빼서 Hips(bone_2_04)가 _rootJoint 바로 밑.
    #   🔴 트위스트 뼈가 형제로 붙음(우솝 교훈): 팔꿈치 자리 bone_202/203이 위팔 밑, 무릎 자리 bone_200/201이 넓적다리 밑 → 아래팔·종아리 밑으로.
    #   mat_4(Object_15)는 이미지 없는 알파 0 BLEND 판(Object_13과 같은 자리 겹침, 안 보이는 재질) · 조명용 Icosphere 뺌.
    "특별함_고우선": dict(path="Assets/Art/Units/특별함_고우선/특별함_고우선.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "reiju.glb"), no_nulls=True, drop_meshes=["Object_15", "Icosphere"],
                      rename_bones=REIJU_RENAME, drop_bones=["bone_26_028", "bone_1_03", "bone_0_02"],
                      reparent_bones={"bone_202_073": "mixamorig:LeftForeArm", "bone_203_074": "mixamorig:RightForeArm",
                                      "bone_200_071": "mixamorig:LeftLeg", "bone_201_072": "mixamorig:RightLeg"},
                      orient_snap=True,
                      glb_images={0: "model_0_mat_0_baseColor.png", 2: "model_0_mat_0_normal.png", 4: "model_0_mat_1_baseColor.png", 6: "model_0_mat_1_normal.png",
                                  8: "model_0_mat_2_normal.png", 9: "model_0_mat_3_baseColor.png", 10: "model_0_mat_3_normal.png", 12: "model_0_mat_7_normal.png",
                                  13: "model_0_mat_8_baseColor.png", 14: "model_0_mat_9_baseColor.png", 15: "model_0_mat_9_normal.png",
                                  16: "model_0_mat_12_baseColor.png", 18: "model_0_mat_12_normal.png"},
                      materials=dict(textures={
                          "model_0_mat_0": [("DiffuseColor", "model_0_mat_0_baseColor.png"), ("NormalMap", "model_0_mat_0_normal.png")],
                          "model_0_mat_1": [("DiffuseColor", "model_0_mat_1_baseColor.png"), ("NormalMap", "model_0_mat_1_normal.png")],
                          "model_0_mat_2": [("DiffuseColor", "model_0_mat_1_baseColor.png"), ("NormalMap", "model_0_mat_2_normal.png")],
                          "model_0_mat_11": [("DiffuseColor", "model_0_mat_1_baseColor.png"), ("NormalMap", "model_0_mat_2_normal.png")],
                          "model_0_mat_3": [("DiffuseColor", "model_0_mat_3_baseColor.png"), ("NormalMap", "model_0_mat_3_normal.png")],
                          "model_0_mat_5": [("DiffuseColor", "model_0_mat_3_baseColor.png"), ("NormalMap", "model_0_mat_3_normal.png")],
                          "model_0_mat_6": [("DiffuseColor", "model_0_mat_3_baseColor.png")],
                          "model_0_mat_7": [("DiffuseColor", "model_0_mat_3_baseColor.png"), ("NormalMap", "model_0_mat_7_normal.png")],
                          "model_0_mat_8": [("DiffuseColor", "model_0_mat_8_baseColor.png")],
                          "model_0_mat_9": [("DiffuseColor", "model_0_mat_9_baseColor.png"), ("NormalMap", "model_0_mat_9_normal.png")],
                          "model_0_mat_10": [("DiffuseColor", "model_0_mat_9_baseColor.png"), ("NormalMap", "model_0_mat_9_normal.png")],
                          "model_0_mat_12": [("DiffuseColor", "model_0_mat_12_baseColor.png"), ("NormalMap", "model_0_mat_12_normal.png")],
                          "model_0_mat_13": [("DiffuseColor", "model_0_mat_12_baseColor.png"), ("NormalMap", "model_0_mat_12_normal.png")]})),
    # 쿠로사키 잇신(모바일 게임 추출 glb, 3ds Max Biped, 2026-09-14): 이름 번호 꼬리를 떼 Biped 이름 그대로(Bip001 Pelvis…). 쉬는 자세 A자 35.8° → T자.
    #   Bip001(무게중심, 가중치 0) 뺌. 🔴 칼(Object_58)은 Bip001 > Prop1 > rweapon에 매달려 쉬는 자세에서 발밑 바닥에 앞으로 누워 있다(애니가 손으로 옮기던 것)
    #   → 유니티 Idle에선 바닥에 남으니 칼 메시와 그 뼈 사슬을 뺀다. 손가락은 Finger0·Finger1 두 줄뿐이라 T자 굽기의 손바닥 굴리기는 건너뜀.
    "특별함_이정범": dict(path="Assets/Art/Units/특별함_이정범/특별함_이정범.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "isshin_kurosaki.glb"), no_nulls=True, drop_meshes=["Object_58", "Icosphere"],
                      rename_regex=(r"(.+?)_[0-9]+", r"\1"),
                      drop_bones=["yixin_weapon_0_nocloth", "rweapon", "Bip001 Prop1", "Bip001"], tpose_arms=True, orient_snap=True,
                      glb_images={0: "yixin_leye_0_baseColor.png", 1: "yixin_mouth_0_baseColor.png", 2: "yixin_reye_0_baseColor.png",
                                  4: "yixin_body_0_baseColor.png", 5: "yixin_face_0_baseColor.png", 6: "yixin_hair_0_baseColor.png"},
                      materials=dict(textures={f"yixin_{k}_0": [("DiffuseColor", f"yixin_{k}_0_baseColor.png")] for k in ("leye", "mouth", "reye", "body", "face", "hair")})),
    # 가로우(원펀맨 게임 추출 glb): 이미 T자. 뿌리 NULL_0133 > RESERVE_0143(가중치 0 중간 뼈)가 WAIST(Hips) 위에 끼어 있다 → 뺀다(흔함_문필환 Bip001 교훈).
    #   조명용 Icosphere 뺌. 텍스처 10장(재질 14가 나눠 씀) → 재질 이름 기준 파일, 공유 이미지는 파일 하나.
    "특별함_유재헌": dict(path="Assets/Art/Units/특별함_유재헌/특별함_유재헌.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "garouopm.glb"), drop_meshes=["Icosphere"], no_nulls=True,
                      # 🔴 NULL_0133 배율 ×10000을 풀어 다시 싸고(glb_unscale_joint) 결합 자세 추정을 꺼야(gltf_guess_bind=False) 뼈대·스킨이 안 뭉개진다
                      glb_unscale_joint="NULL_0133", gltf_guess_bind=False,
                      rename_bones=GAROU_RENAME, drop_bones=["RESERVE_0143", "NULL_0133"], orient_snap=True,
                      glb_images={0: "hairShapeH_HAIR_baseColor.png", 1: "faceShapeS_FACE_baseColor.png", 2: "eyeShapeE_EYE_baseColor.png",
                                  3: "C_inmouthShapeS_SKIN_baseColor.png", 4: "bodyShapeC_TOPS_baseColor.png", 5: "bodyShapeC_TOPS_normal.png",
                                  6: "handShapeS_HAND_baseColor.png", 7: "pantsShapeC_BOTTOMS_baseColor.png", 8: "pantsShapeC_BOTTOMS_normal.png",
                                  9: "footShapeC_SHOES_baseColor.png"},
                      materials=dict(textures={
                          "hairShapeH_HAIR": [("DiffuseColor", "hairShapeH_HAIR_baseColor.png")],
                          "faceShapeS_FACE": [("DiffuseColor", "faceShapeS_FACE_baseColor.png")],
                          "eyebrowShapeH_EYEBROW": [("DiffuseColor", "faceShapeS_FACE_baseColor.png")],
                          "eye_LShapeE_EYE_L": [("DiffuseColor", "eyeShapeE_EYE_baseColor.png")],
                          "eye_RShapeE_EYE_R": [("DiffuseColor", "eyeShapeE_EYE_baseColor.png")],
                          "C_inmouthShapeS_SKIN": [("DiffuseColor", "C_inmouthShapeS_SKIN_baseColor.png")],
                          "toothdownShapeT_TOOTH": [("DiffuseColor", "C_inmouthShapeS_SKIN_baseColor.png")],
                          "bodyShapeC_TOPS": [("DiffuseColor", "bodyShapeC_TOPS_baseColor.png"), ("NormalMap", "bodyShapeC_TOPS_normal.png")],
                          "neckShapeS_NECK": [("DiffuseColor", "bodyShapeC_TOPS_baseColor.png")],
                          "handShapeS_HAND": [("DiffuseColor", "handShapeS_HAND_baseColor.png")],
                          "obiShapeC_OBI": [("DiffuseColor", "pantsShapeC_BOTTOMS_baseColor.png")],
                          "pantsShapeC_BOTTOMS": [("DiffuseColor", "pantsShapeC_BOTTOMS_baseColor.png"), ("NormalMap", "pantsShapeC_BOTTOMS_normal.png")],
                          "footShapeC_SHOES": [("DiffuseColor", "footShapeC_SHOES_baseColor.png")],
                          "footShapeS_FOOT": [("DiffuseColor", "footShapeC_SHOES_baseColor.png")]})),
    # 진베 오니가시마(바운티러시 pl_ 리그를 Annettlw가 합친 판, 2026-09-14): 이미 T자. 표정 5·손 3벌 겹침 + 찻잔(cup). 손은 주먹(close) — 어인 가라테 기본 모습.
    #   🔴 코트 소매 뼈 l_arm01·r_arm01이 팔이 아니라 coat_root(가슴) 밑이라 유니티가 팔을 내려도 소매가 T자에 남는다 → 위팔(LeftArm·RightArm) 밑으로.
    "특별함_김용태": dict(path="Assets/Art/Units/특별함_김용태/특별함_김용태.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-jinbei-onigashima/source/Jinbei Onigashima.rar"),
                               "Jinbei Onigashima/Jinbei Onigashima by Annettlw.fbx"),
                      archive_textures=["Jinbei Onigashima/pl_jinbe_atta01_diff_hq.png"],
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "l_hand_open", "r_hand_open", "l_hand_sp_01", "r_hand_sp_01", "cup"],
                      rename_bones=PL_RENAME, reparent_bones={"l_arm01": "mixamorig:LeftArm", "r_arm01": "mixamorig:RightArm"},
                      null_frames_from_node=True, orient_snap=True),
    # 쿠르타 입은 인도 남자(Avatar SDK·Mixamo 리그 glb, 2026-09-14): 뼈 이름에 Sketchfab 번호 꼬리 → mixamorig 표준 이름(_rootJoint 밑 Hips 그대로).
    #   쉬는 자세 이미 T자(위팔 수평 아래 2.6°). 조명용 Icosphere 뺌. 텍스처: glb 내장 이미지를 재질 이름 기준 파일로, 노멀은 _normal,
    #   머리카락(haircut)만 알파 컷아웃(원본 alphaMode MASK — 진짜 컷아웃). 눈썹(AvatarEyelashes)은 원본 검정 단색. 삼각형 65,095는 줄이지 않음.
    "특별함_이병준": dict(path="Assets/Art/Units/특별함_이병준/특별함_이병준.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "indian_man_in_kurta.glb"), drop_meshes=["Icosphere"],
                      rename_regex=(r"([A-Za-z][A-Za-z0-9_]*?)_[0-9]+", r"mixamorig:\1"),
                      # glTF 메시 노드 빈 오브젝트 11개(재질과 같은 이름)가 Null로 살아나 가중치 없는 뿌리 뼈가 된다 — 뺀다
                      drop_bones=["AvatarBody", "AvatarEyelashes", "AvatarHead", "AvatarLeftCornea", "AvatarLeftEyeball", "AvatarRightCornea",
                                  "AvatarRightEyeball", "AvatarTeethLower", "AvatarTeethUpper", "haircut", "outfit"],
                      glb_images={0: "AvatarBody_baseColor.jpg", 1: "AvatarBody_normal.png", 2: "AvatarHead_baseColor.jpg", 3: "AvatarHead_normal.png",
                                  4: "AvatarLeftCornea_baseColor.jpg", 5: "AvatarEyeball_baseColor.jpg", 6: "AvatarEyeball_normal.png",
                                  7: "AvatarRightCornea_baseColor.jpg", 8: "AvatarTeeth_baseColor.jpg", 9: "AvatarTeeth_normal.png",
                                  10: "haircut_baseColor.png", 11: "haircut_normal.png", 12: "outfit_baseColor.jpg", 13: "outfit_normal.png"},
                      materials=dict(textures={
                          "AvatarBody": [("DiffuseColor", "AvatarBody_baseColor.jpg"), ("NormalMap", "AvatarBody_normal.png")],
                          "AvatarHead": [("DiffuseColor", "AvatarHead_baseColor.jpg"), ("NormalMap", "AvatarHead_normal.png")],
                          "AvatarLeftCornea": [("DiffuseColor", "AvatarLeftCornea_baseColor.jpg")],
                          "AvatarRightCornea": [("DiffuseColor", "AvatarRightCornea_baseColor.jpg")],
                          "AvatarLeftEyeball": [("DiffuseColor", "AvatarEyeball_baseColor.jpg"), ("NormalMap", "AvatarEyeball_normal.png")],
                          "AvatarRightEyeball": [("DiffuseColor", "AvatarEyeball_baseColor.jpg"), ("NormalMap", "AvatarEyeball_normal.png")],
                          "AvatarTeethLower": [("DiffuseColor", "AvatarTeeth_baseColor.jpg"), ("NormalMap", "AvatarTeeth_normal.png")],
                          "AvatarTeethUpper": [("DiffuseColor", "AvatarTeeth_baseColor.jpg"), ("NormalMap", "AvatarTeeth_normal.png")],
                          "haircut": [("DiffuseColor", "haircut_baseColor.png"), ("TransparencyFactor", "haircut_baseColor.png"), ("NormalMap", "haircut_normal.png")],
                          "outfit": [("DiffuseColor", "outfit_baseColor.jpg"), ("NormalMap", "outfit_normal.png")],
                          "AvatarEyelashes": [("BaseColor", (0.0, 0.0, 0.0))]})),
    # 놀란드(바운티러시 pl_ (merge) 리그, 2026-09-14): 이미 T자. 표정 5·손 4 변형 + 칼 두 상태(오른손에 뽑은 칼 r_weapon_01 / 칼집에 든 손잡이 l_handle_sheath).
    #   l_handle_sheath는 쉬는 자세에서 오른발 옆 바닥에 떨어져 있다(원본 결함) → 뽑은 칼 + 왼허리 빈 칼집(l_sheath) + 칼 쥔 오른손 주먹을 기본으로.
    #   텍스처 알파 = 명암 마스크(중간값 99.8%) → 알파 뺀 RGB PNG로(rgb_textures). 원본 FBX는 DiffuseColor만 부름.
    "특별함_이현빈": dict(path="Assets/Art/Units/특별함_이현빈/특별함_이현빈.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-nolan.zip"), "source/nolan.rar", "nolan/pl_noland_sora01 (merge).fbx"),
                      archive_textures=["nolan/pl_noland_sora01_diff.png"], rgb_textures=["pl_noland_sora01_diff.png"],
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "l_hand_close", "r_hand_open", "l_handle_sheath"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # Mr.5(바운티러시 pl_ (merge) 리그, 2026-09-14): 이미 T자. 표정 5 · 손 13 변형(주먹·코 파기·코딱지 대포·권총 쥔 손·권총 집어넣는 손). FBX 안에 이펙트 메시는 없다
    #   (rar의 Mesh/·Texture2D/ 스킬 이펙트 OBJ·PNG는 안 옮김). 기본 = 오른손에 권총(r_hand_weapon_revolver + r_weapon_revolver_01) · 왼손 open. 알파 = 명암 마스크 → RGB.
    "특별함_조세민": dict(path="Assets/Art/Units/특별함_조세민/특별함_조세민.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-mr-5.zip"), "source/mr 5.rar", "mr 5/pl_mr5five_orig01 (merge).fbx"),
                      archive_textures=["mr 5/pl_mr5five_orig01_diff.png"], rgb_textures=["pl_mr5five_orig01_diff.png"],
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "l_hand_close", "r_hand_close", "r_hand_open",
                                   "l_hand_nose_01", "l_hand_nose_02", "r_hand_nose_01", "r_hand_nose_02", "l_hand_nose_fancycannon_01",
                                   "l_hand_nose_fancycannon_02", "r_hand_nose_fancycannon_01", "r_hand_nose_fancycannon_02", "l_hand_put_revolver"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # 루치: 손 open만, 비둘기 날개는 접은 쪽(close)
    "흔함_노태현": dict(rev="bab90f7c", path="Assets/Art/Units/흔함_노태현/흔함_노태현.fbx", kind="human", size=("height", 1.8),
                    drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_shigun", "r_hand_shigun",
                                 "pigeon_l_wing_open", "pigeon_r_wing_open"],
                    rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # 시저: 손 open만, 이펙트 메시 eff 뺌. r_sword_01·coat·leg 유지
    "흔함_강주혁": dict(rev="fbcca7bb", path="Assets/Art/Units/흔함_강주혁/흔함_강주혁.fbx", kind="human", size=("height", 1.8),
                    drop_meshes=["eff", "face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_gastanets", "r_hand_gastanets",
                                 "l_hand_pose", "r_hand_pose"],
                    rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # 마젤란: 손 open만
    "흔함_박민석": dict(rev="57d13ab9", path="Assets/Art/Units/흔함_박민석/흔함_박민석.fbx", kind="human", size=("height", 1.8),
                    drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close"],
                    rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    "특별함_노태현": dict(rev="2d515a55", path="Assets/Art/Units/특별함_노태현/특별함_노태현.fbx", kind="human", size=("height", 1.8),
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_shigan", "r_hand_shigan"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # 🔴 특별함_박민석: r_hand_open이 쉬는 자세에서 몸 오른쪽 2배 키 거리에 떠 있다(원본 결함) → 손은 양쪽 close(주먹)를 기본으로. chain·headphone·boot 유지
    #   09-15 사장님 신고(오른팔 이상): r_hand_open 메시 노드가 오른쪽으로 4.47 밀리고 그 클러스터 TransformLink도 같이 밀려, 블렌더가 RHand 뼈 쉬는 자리를 틀리게 잡았다 →
    #   정점이 맞는 r_hand_close가 기본 자세 굽기에서 왼손 쪽(+x)으로 넘어가 유니티 Idle에서 오른 주먹이 2.2m 옆에 떴다. body1도 오른 소매 끝이 RHand 뼈에 실려 같은 사고 →
    #   둘 다 원시 정점 그대로(bake_rest_meshes). 원본에서 기본 자세가 쉬는 자세와 다른 뼈는 이 밀린 오른손 사슬뿐이라 원시 정점 = 옳은 기본 모습.
    "특별함_박민석": dict(rev="2d515a55", path="Assets/Art/Units/특별함_박민석/특별함_박민석.fbx", kind="human", size=("height", 1.8),
                      drop_meshes=["face_attack", "face_damage", "l_hand_open", "r_hand_open"], bake_rest_meshes=["r_hand_close", "body1"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # 새 스킨(git 원본 없음) — 다운로드 rar에서 FBX·텍스처를 꺼내 짓는다. 쉬는 자세 팔 A자 44.7° → T자로 굽는다. 재질 34065 하나(Dots Stroke·Material은 면 0, 안 읽힘).
    "특별함_황정기": dict(path="Assets/Art/Units/특별함_황정기/특별함_황정기.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-fighting-path-usopp-onigashima/source/Usopp Onigashimaa.rar"),
                               "Usopp Onigashima/Usopp Onigashima by Annettlw.fbx"),
                      archive_textures=["Usopp Onigashima/34065_D.png"],
                      drop_bones=["Bip001"], reparent_bones=USOPP_REPARENT, tpose_arms=True),
    # 모리아(바운티러시 pl_ 리그, 2026-09-14): 팔은 이미 T자(팔·손 뼈 같은 높이). 표정·손 모양 변형 메시가 한자리에 겹쳐 있어 기본만 남긴다 —
    #   남김 body·coat·face_normal(웃는 얼굴)·l/r_hand_open, 뺌 = 아래 9개(공격 얼굴·주먹·가위 쥔 손·작은 가위 날). 끝·이펙트 Null은 원래 틀 그대로 뼈로.
    "특별함_임채준": dict(path="Assets/Art/Units/특별함_임채준/특별함_임채준.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-gecko-moria-marineford.zip"), "source/pl_geckomoria_topw01.rar",
                               "pl_geckomoria_topw01/pl_geckomoria_topw01.fbx"),
                      archive_textures=["pl_geckomoria_topw01/pl_geckomoria_topw01_diff.png"],
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_scissors_open", "l_hand_scissors_close",
                                   "L_scissor", "R_scissor", "L_scissors"],
                      rename_bones=PL_RENAME, null_frames_from_node=True),
    # 미호크: 이미 T자·미터 단위. 「-」 메시 3개(손에 쥔 검·손 칼날 = 공격 변형)를 빼고 「+」(등의 검·목걸이 칼)는 남긴다.
    #   텍스처: 재질 14개가 occ/alb/nmh/spec 4장씩 부르는데 폴더엔 이름 잘린 알베도 4장뿐 → 재질을 7개로 모아 그 4장을 물리고,
    #   없는 머리·수염(hair_kidsalb)·모자 깃털(fur_blend_kidsalb)은 단색. 옷(Body·코트·칼집·목걸이)은 cloth 알베도(원본은 fur_blend를 불렀지만 UV가 cloth 그림).
    "특별함_박기찬": dict(path="Assets/Art/Units/특별함_박기찬/특별함_박기찬.fbx", kind="human", size=("height", 1.8),
                      source=os.path.expanduser("~/Downloads/mihawk/source/Mihawk.fbx"), hips="mixamorig:Hips", head="mixamorig:Head",
                      drop_meshes=["24_-SwordHand_0.1_1.0_1.0", "24_-BladeHandL_0.1_1.0_1.0", "24_-BladeHandR_0.1_1.0_1.0"],
                      # 손에 쥔 검 메시를 빼면 그 뼈(검 끝이 앞으로 2.1m)는 가중치 없이 남아 경계를 망친다 — 같이 뺀다(자식부터)
                      drop_bones=["sword tip", "sword bottom", "arm right weapon"],
                      # 등의 검 끝(원본 z 2.4)이 모자(2.07)보다 높아 키에 넣으면 몸이 1.49m로 작아진다 — 키는 검 빼고 모자 끝까지로
                      size_ignore_meshes=["24_+SwordBack_0.1_1.0_1.0"],
                      rename_bones=MIHAWK_RENAME,
                      copy_textures={_MH + "face_kid.png": "Mihawk_Face.png", _MH + "skin_kid.png": "Mihawk_Skin.png",
                                     _MH + "cloth_ki.png": "Mihawk_Cloth.png", _MH + "weapon_k.png": "Mihawk_Weapon.png"},
                      materials=dict(mesh_material={"24_Hair_0.1_1.0_1.0": "Mihawk_Hair", "24_FacialHair_0.1_1.0_1.0": "Mihawk_Beard",
                                                    "24_Face_0.1_1.0_1.0": "Mihawk_Face", "24_Skin_0.1_1.0_1.0": "Mihawk_Skin",
                                                    "24_Body_0.1_1.0_1.0": "Mihawk_Cloth", "24_Body_0.1_1.0_1.001": "Mihawk_Cloth",
                                                    "24_BodySwordSheath_0.1_1.0_1.0": "Mihawk_Cloth", "24_+BladeNeck_0.1_1.0_1.0": "Mihawk_Cloth",
                                                    "24_Fur_0.1_1.0_1.0": "Mihawk_Plume", "24_+SwordBack_0.1_1.0_1.0": "Mihawk_Weapon"},
                                     textures={"Mihawk_Face": [("DiffuseColor", "Mihawk_Face.png")], "Mihawk_Skin": [("DiffuseColor", "Mihawk_Skin.png")],
                                               "Mihawk_Cloth": [("DiffuseColor", "Mihawk_Cloth.png")], "Mihawk_Weapon": [("DiffuseColor", "Mihawk_Weapon.png")],
                                               "Mihawk_Hair": [("BaseColor", (0.02, 0.02, 0.02))], "Mihawk_Beard": [("BaseColor", (0.02, 0.02, 0.02))],
                                               "Mihawk_Plume": [("BaseColor", (0.75, 0.72, 0.66))]})),
    # 가렌(LoL 추출 glb, 뼈 69·메시 1·삼각형 6,336): 🔴 좌우 뒤집힌 추출본 — 정면 −Y인데 R_ 뼈가 +X(몸 왼쪽)에 있다(LoL 왼손 좌표계).
    #   mirror_x로 통째 X 거울 → 뼈 이름대로의 몸 쪽(대검이 오른손). 쉬는 자세 A자(위팔 수평 아래 58°) → T자.
    #   바인드 자세에선 대검(Weapon_21, R_Hand 자식)이 손을 떠나 몸 옆에 서 있고 스카프 뼈 사슬이 머리 높이에서 뒤로 수평으로 뻗었다 —
    #   유니티 사람형 Idle은 매핑 안 된 뼈를 안 움직이니 idle1 첫 프레임의 무기·오른손 손가락·스카프 자세로 굳힌다.
    #   Buffbone_*_Loc 12개 = 가중치 0 표식 → 뺌. 재질 1(Garen_Base_Mat, 512 RGB PNG — 알파 없음).
    "특별함_박민수": dict(path="Assets/Art/Units/특별함_박민수/특별함_박민수.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "garen_league_of_legends_character.glb"), no_nulls=True, drop_meshes=["Icosphere"], mirror_x=True,
                      pose_from_clip=("garen_2013_idle1.anm", 0, ["Weapon_21", "Scarf1_38", "Scarf2_39", "Scarf3_40"]
                                      + [f"R_{f}{k}_{11 + 2 * i + k - 1}" for i, f in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky")) for k in (1, 2)]),
                      rename_bones=GAREN_RENAME, orient_snap=True,
                      drop_bones=["C_Buffbone_Glb_Head_Loc_62", "Buffbone_Glb_Weapon_1_59", "R_Buffbone_Glb_Hand_Loc_68", "L_Buffbone_Glb_Hand_Loc_66",
                                  "C_Buffbone_Glb_Chest_Loc_61", "R_Buffbone_Glb_Foot_Loc_67", "L_Buffbone_Glb_Foot_Loc_65", "Buffbone_Glb_Channel_Loc_57",
                                  "Buffbone_Glb_Ground_Loc_58", "C_Buffbone_Glb_Center_Loc_60", "C_Buffbone_Glb_Layout_Loc_63", "C_Buffbone_Glb_Overhead_Loc_64"],
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                      "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "Garen_Base_Mat.png"},
                      materials=dict(textures={"Garen_Base_Mat": [("DiffuseColor", "Garen_Base_Mat.png")]})),
    # 탐 켄치(LoL 팬아트 glb, mGear 뼈 94·메시 11·삼각형 16,194): 🔴 가중치 없는 조인트 19개(왼팔·왼다리 본 뼈·neck_C0_0/1·chain_C0_3 등)의 IBM이 단위행렬 →
    #   결합 자세로 읽으면 원점에 뭉개졌다 → glb_fix_identity_ibm(자식 leaf_ 결합 자리에서 되짚음). 얼굴·혀 비균일 배율은 장면 노드 자세에만 있고 IBM엔 없다(det 1).
    #   혀(Object_280, thongue_C0_0~4)가 쉬는 자세에서 입 밖 앞으로 1.06m(몸 키 1.03m보다 김) — PM 결정(a) 입 안으로: 사슬 방향으로 0.285배(끝이 윗니 앞 −0.485 뒤 −0.44).
    #   🔸 메기 수염(stach_R0/L0_0~3)이 결합 자세에선 입 양옆으로 곧게 뻗은 막대(좌우 0.4) — Take 001(정지 자세 클립) 0프레임의 수염 8뼈 자세로 굳혀 늘어뜨린다.
    #   꼬리 chain_C0_0~2(가중치 524/418/318)가 root 밑(pelvis 형제) → Hips 밑으로. 쇄골이 위·뒤로 45°라 쇄골은 안 펴고 위팔부터 T자(수평 아래 21°).
    #   모자 포함 키 1.8. 재질 Body(이미지 0 + 노멀 2)·Assets(3 + 노멀 6) — 1(ORM)·4(ORM)·5(발광)·7(스펙큘러)은 FBX가 안 실음.
    # 잉어(Sketchfab carp_fish.glb, 뼈 30·메시 1·삼각형 7,406·헤엄 클립 1개 「Scene」 1.97초): 🔴 inverseBindMatrices가 틀려 결합 자세로 읽으면
    #   물고기가 세로로 선다(0.25×0.18×0.63 — 긴 축이 z), 클립 0프레임은 정상(0.62×0.18×0.25, 머리 −X·등 +Z). 아마추어 오브젝트 세계 배율 0.458(스트레이).
    #   → 클립 0프레임 전체를 새 쉬는 자세로 굽고(pose_from_clip "all" — 매 프레임 변형 = 자세(f)·자세(0)⁻¹이라 틀린 IBM이 상쇄된다) 클립을 새 뼈대에 다시 굽는다.
    #   _rootJoint는 세계 원점(몸에서 14 떨어짐, 가중치 0) → 빼고 꼬리 사슬 Bone.001_010을 같은 자리 Bone_00 밑으로(뿌리 하나). 방향은 머리 Bone.003_02 ↔ 꼬리 Bone.008_014(0프레임 꼬리가 휘어 81° — orient_snap으로 90° 단위).
    #   Generic(사람형 아님) · 몸길이 2.0 · 배 최저 z 0 · 머리 −Y · 테이크 Idle(루트 이동 없음 — clip_ground 안 씀). 구현담당1 두 차례 경위는 SOURCE.txt.
    # 아이언맨 Mk.III(3ds Max 2020 Biped FBX, ironman.zip 속 source/IronMan.fbx) — 2026-09-15 사장님 지시로 코알라(폐기) 대신 특별함_강주혁.
    #   뼈 106(Bip01, 손가락 5×3·Nub·아머 판 보조 뼈 Bone02~21) · 메시 125 · 삼각형 211,872 · 오브젝트 배율 0.0254·Z −90°(파이프라인이 세계로 굽는다).
    #   Biped 보조 메시 9개(SpineRemoval*·Calf*Removal1·FootRemovCtr1·*SeqCtlr1 — 몸에서 멀리 떨어진 1,008정점 조종 도형, 재질·가중치 없음)와
    #   손 빔 표식 2개(9정점) 뺌 → 보조 뺀 몸 2.61×1.26×6.06(가로가 넓어 보인 건 조종 도형 탓, T자 아님). 부모·스킨 없는 허리 판 polySurface3864(82정점)는
    #   뿌리 뼈(Hips) 100%로. 결합 자세는 팔 늘어뜨림(수평 아래 73°)·다리 넓게·아머 판 닫힘, FBX 기본 자세는 T자·다리 곧게인데 아머 판 보조 뼈가 판을 연 채
    #   → Bip01 뼈만 기본 자세, 보조 뼈는 결합 자세(default_pose_only). 큰 메시(600삼각형 이상 71개)만 ×0.25 감량(단색이라 UV 걱정 없음) → 약 6.2만.
    "특별함_강주혁": dict(path="Assets/Art/Units/특별함_강주혁/특별함_강주혁.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "ironman.zip"), "source/IronMan.fbx"),
                      drop_meshes=["SpineRemoval1", "SpineRemoval02", "SpineRemoval03", "SpineRemoval04", "CalfRRemoval1", "CalfLRemoval1",
                                   "FootRemovCtr1", "RSeqCtlr1", "LSeqCtlr1", "righthandBEAM", "LeftHandBEAM"],
                      decimate=dict(min_tris=600, ratio=0.25), default_pose_only=r"^Bip01 ",
                      rename_bones=IRONMAN_RENAME, orient_snap=True,
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      solid_textures={"Iron_man_leg:red": None, "Iron_man_leg:gold": None, "HD_Ironman:silver": None, "lambert1": None, "14 - Default": None,
                                      "HD_Ironman:darksilver": (0.3, 0.3, 0.32), "HD_Ironman:black": (0.02, 0.02, 0.02), "HD_Ironman:yellow": (1.0, 0.9, 0.55)}),
    # 찰로스(바운티러시 pl_ 리그 FBX, zip 속 rar 속 「charlos/pl_charlos_orig01 (merge).fbx」) — 2026-09-15 새 스킨. 이미 T자·기본 자세 = 쉬는 자세(손 결합 밀림 없음).
    #   뼈 60 · 발끝 뼈 있음 → PL_RENAME 그대로. 겹친 변형 16개 뺌: 얼굴 face_sp01·sp02·attack·damage(→ face_normal) · 손 close·sp02·sp03·sp04·r_hand_weapon01(→ l/r_hand_open) ·
    #   총 r_weapon_01 · 콧물 변형 hanamizu_sp. 콧물 hanamizu(찰로스 트레이드마크)·등 탱크 backpack은 남김. 빈 오브젝트 16(총구·비눗방울 효과·플래그)은 뼈로 안 살림.
    #   텍스처: rar 안 _diff.png와 zip textures/ 판이 바이트는 다르지만 픽셀 동일 — 알파 = 명암 마스크(알파<0.98 98.6%·평균 0.856) → RGB PNG.
    "특별함_김태영": dict(path="Assets/Art/Units/특별함_김태영/특별함_김태영.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-charlos.zip"), "source/charlos.rar", "charlos/pl_charlos_orig01 (merge).fbx"),
                      archive_rgb={"charlos/pl_charlos_orig01_diff.png": "pl_charlos_orig01_diff.png"},
                      drop_meshes=["face_sp01", "face_sp02", "face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_sp02", "r_hand_sp02",
                                   "l_hand_sp03", "r_hand_sp03", "l_hand_sp04", "r_hand_sp04", "r_hand_weapon01", "r_weapon_01", "hanamizu_sp"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      materials=dict(textures={"pl_charlos_orig01": [("DiffuseColor", "pl_charlos_orig01_diff.png")]})),
    # 기사(strong_knight.glb, Sketchfab-12.66, 스킨 조인트 65·메시 5·삼각형 12,944·클립 idle1 안 씀) — 2026-09-15 새 스킨.
    #   🔴 끝 조인트 14개(_rootJoint·toes·손가락 끝 Joint_3_*·tip) IBM이 단위행렬 → glb_fix_identity_ibm(탐 켄치와 같은 수리). 좌우·정면은 이름대로(L이 +X, 발끝 −Y).
    #   팔 A자(위팔 수평 아래 37°·아래팔 45° 아래·앞으로 59°) → T자(손가락 매핑으로 손바닥 굴리기). 조종용 빈 오브젝트(hipcontrol·*_Goal·*_Pole 등)는 뼈로 안 살림.
    #   무기 3개는 스킨 없음: Maul(등에 멘 망치) ← chest_013 · daggercase·dagger(왼 허리 단검집) ← spine_012 → 그 뼈 100%(파이프라인 rigid).
    #   망치 머리가 정수리보다 8cm 위라 키는 망치 빼고 잰다(size_ignore_meshes). 재질 3(cloth·knight·weapons)이 KHR_materials_pbrSpecularGlossiness —
    #   diffuse(이미지 0·3·6)·노멀(2·5·8)을 원본 바이트 그대로 <재질>_diffuse.png·_normal.png로. specularGlossiness(1·4·7)는 안 씀.
    "특별함_왕승환": dict(path="Assets/Art/Units/특별함_왕승환/특별함_왕승환.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "strong_knight.glb"), glb_fix_identity_ibm=True, no_nulls=True, drop_meshes=["Icosphere"],
                      rename_bones=KNIGHT_RENAME, orient_snap=True, size_ignore_meshes=["Maul_weapons_0"],
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "cloth_diffuse.png", 2: "cloth_normal.png", 3: "knight_diffuse.png", 5: "knight_normal.png", 6: "weapons_diffuse.png", 8: "weapons_normal.png"},
                      materials=dict(textures={m: [("DiffuseColor", f"{m}_diffuse.png"), ("NormalMap", f"{m}_normal.png")] for m in ("cloth", "knight", "weapons")})),
    # 류마(바운티러시 pl_ 리그 FBX, zip 속 rar 속 pl_ryuma_orig01.fbx) — 2026-09-15 새 스킨. 이미 T자·기본 자세 = 쉬는 자세. 뼈 62(번호 꼬리 없음 → PL_RENAME 그대로).
    #   겹친 변형: 손 2벌 → l/r_hand_open · 칼 3벌(허리에 찬 waist_blade+waist_sheath / 왼손 l_blade+l_sheath / 오른손 r_blade) → 허리 한 벌만(렌더로 판정:
    #   손에 든 칼은 T자 손끝에 칼코등이만 떠 보이고, 허리 칼은 대기 모습에 맞음). 빈 오브젝트 7(무기 자리·플래그 표식)은 뼈로 안 살림.
    #   텍스처: rar 안 _diff.png와 zip textures/ 판이 바이트는 다르지만 픽셀 동일 — 알파 = 명암 마스크(알파<0.98 99.99%·평균 0.746) → RGB PNG.
    "특별함_정승준": dict(path="Assets/Art/Units/특별함_정승준/특별함_정승준.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-ryuma.zip"), "source/pl_ryuma_orig01.rar", "pl_ryuma_orig01/pl_ryuma_orig01.fbx"),
                      archive_rgb={"pl_ryuma_orig01/pl_ryuma_orig01_diff.png": "pl_ryuma_orig01_diff.png"},
                      drop_meshes=["l_hand_close", "r_hand_close", "l_blade", "l_sheath", "r_blade"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      materials=dict(textures={"pl_ryuma_orig01": [("DiffuseColor", "pl_ryuma_orig01_diff.png")]})),
    # 요크(바운티러시 pl_ 리그 FBX, zip 속 rar 속 「pl_york_orig01 (merge).fbx」) — 2026-09-15 사장님 지시로 릴리스 스킨 교체. 이미 T자.
    #   겹친 변형: 얼굴 6벌 → face_normal · 손 4벌 → l/r_hand_open · 오른손 총(r_weapon_gun_01)·총 쥔 손(r_hand_weapon_gun01) 뺌(기본 대기) ·
    #   콧물 풍선(snot_bubble, 자는 연출) 뺌. 고글 렌즈(_trans_goggles 재질)는 남기되 불투명(알파는 명암 마스크라 RGB로).
    #   텍스처: rar 안 _diff.png와 zip textures/ 판이 바이트는 다르지만 픽셀 동일 — 알파 = 명암 마스크(알파<0.98 99.2%) → RGB PNG. 재질 3개가 같은 텍스처.
    "특별함_이지원": dict(path="Assets/Art/Units/특별함_이지원/특별함_이지원.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(DL, "one-piece-bounty-rush-york.zip"), "source/york.rar", "york/pl_york_orig01 (merge).fbx"),
                      archive_rgb={"york/pl_york_orig01_diff.png": "pl_york_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "face_sp03", "l_hand_close", "r_hand_close",
                                   "l_hand_open_02", "r_hand_open_02", "r_hand_weapon_gun01", "r_weapon_gun_01", "snot_bubble"],
                      rename_bones=YORK_RENAME, no_nulls=True, orient_snap=True,
                      # pl_york_orig01_trans 재질은 뺀 콧물 풍선만 써서 표에서 뺀다(없는 재질을 걸면 relink가 멈춘다)
                      materials=dict(textures={m: [("DiffuseColor", "pl_york_orig01_diff.png")] for m in ("pl_york_orig01", "pl_york_orig01_trans_goggles")})),
    "특별함_노건완": dict(path="Assets/Art/Units/특별함_노건완/특별함_노건완.fbx", kind="beast", size=("length", 2.0), anim=True, anim_drop_ok=True,
                      source=os.path.join(DL, "carp_fish.glb"), no_nulls=True, drop_meshes=["Icosphere"], head="Bone.003_02", tail="Bone.008_014", orient_snap=True,
                      pose_from_clip=("Scene", 0, "all"), take_names={"Scene": "Idle"}, clip_scene_basis=True,
                      drop_bones=["_rootJoint"], reparent_bones={"Bone.001_010": "Bone_00"},
                      glb_images={0: "carp_baseColor.png", 2: "carp_normal.png"},
                      materials=dict(textures={"carp": [("DiffuseColor", "carp_baseColor.png"), ("NormalMap", "carp_normal.png")]})),
    "특별함_조도연": dict(path="Assets/Art/Units/특별함_조도연/특별함_조도연.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(DL, "league_of_legend_fan_arttahm_kench.glb"), glb_fix_identity_ibm=True, no_nulls=True, drop_meshes=["Icosphere"],
                      squash_chain=dict(bones=[f"thongue_C0_{i}_Jnt_0{26 + i}" for i in range(5)], factor=0.285),
                      pose_from_clip=("Take 001", 0, [f"stach_{s}0_{i}_Jnt_0{base + i}" for s, base in (("R", 31), ("L", 35)) for i in range(4)]),
                      rename_bones=TAHM_RENAME, orient_snap=True, reparent_bones={"chain_C0_0_Jnt_089": "mixamorig:Hips"},
                      tpose_arms={s: {"UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "Body_baseColor.png", 2: "Body_normal.png", 3: "Assets_baseColor.png", 6: "Assets_normal.png"},
                      materials=dict(textures={"Body": [("DiffuseColor", "Body_baseColor.png"), ("NormalMap", "Body_normal.png")],
                                               "Assets": [("DiffuseColor", "Assets_baseColor.png"), ("NormalMap", "Assets_normal.png")]})),
}
HIPS = re.compile(r"(?i)(^|[:_ .])(hips?|pelvis)($|[_ .0-9])")
HEAD = re.compile(r"(?i)(^|[:_ .])head($|[_ .0-9])")
UPPER = re.compile(r"(?i)(up_?leg|upper_?leg|thigh|upper_?arm|[:_ ]arm$|shoulder|clavicle|bone\.029|bone\.006)")
LEFT = re.compile(r"(?i)(left|(^|[:_ ])l([:_ ]|$)|\.l($|_)|_l($|_)|^l(arm|leg))")
RIGHT = re.compile(r"(?i)(right|(^|[:_ ])r([:_ ]|$)|\.r($|_)|_r($|_)|^r(arm|leg))")
SKIP = ("end", "top", "tweak", "mch", "org", "pole", "widget", "adj", "vis_")


def load(path, anim, guess_bind=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.lower().endswith((".glb", ".gltf")):
        # guess_bind=False: 쉬는 자세 = 장면 기본 노드 자세. 🔴 denji는 결합 자세(추정)로 읽으면 메시가 가는 선으로 뭉개졌다(실측) —
        # 장면 자세로 읽어야 T자(6.9×1.5×7.4)가 나온다. zombi·luffy·nanachi는 기본값(추정 켬)이 지금 FBX 모양과 같다.
        bpy.ops.import_scene.gltf(filepath=path, guess_original_bind_pose=guess_bind)
        if not anim:                                           # glTF는 클립을 늘 싣는다 — 기본 자세로 읽으려면 걷고 자세를 되돌린다
            for o in bpy.context.scene.objects:
                if o.type == "ARMATURE":
                    if o.animation_data:
                        o.animation_data.action = None
                    for pb in o.pose.bones:
                        pb.matrix_basis = Matrix.Identity(4)
            for act in list(bpy.data.actions):
                bpy.data.actions.remove(act)
    else:
        bpy.ops.import_scene.fbx(filepath=path, use_anim=anim)
    bpy.context.view_layer.update()


def skinned_to(mesh):
    return next((m.object for m in mesh.modifiers if m.type == "ARMATURE" and m.object), None)


def main_armature():
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if not arms:
        return None
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    return max(arms, key=lambda a: (sum(1 for m in meshes if skinned_to(m) == a), len(a.data.bones)))


FBX_TEX_SLOT = {"DiffuseColor": "base_color_texture", "NormalMap": "normalmap_texture", "TransparencyFactor": "alpha_texture"}


def fbx_texture_table(path):
    """원시 FBX에서 재질 이름 → [(속성, 파일명)] — Material ← Texture(OP 연결의 속성) ← Video의 RelativeFilename."""
    from io_scene_fbx import parse_fbx
    root, _ = parse_fbx.parse(path)
    objects = next(e for e in root.elems if e.id == b"Objects")
    conns = next(e for e in root.elems if e.id == b"Connections")
    node = {e.props[0]: e for e in objects.elems}

    def filename(e):
        rf = next((s for s in e.elems if s.id in (b"RelativeFilename", b"FileName")), None)
        return rf.props[0].decode("utf-8", "replace").replace("\\", "/").rsplit("/", 1)[-1] if rf else None

    video = {c.props[2]: filename(node[c.props[1]]) for c in conns.elems
             if c.props[1] in node and c.props[2] in node and node[c.props[1]].id == b"Video" and node[c.props[2]].id == b"Texture"}
    table = {}
    for c in conns.elems:
        ch, pa = c.props[1], c.props[2]
        if c.props[0] == b"OP" and ch in node and pa in node and node[ch].id == b"Texture" and node[pa].id == b"Material":
            entry = (c.props[3].decode(), video.get(ch) or filename(node[ch]))
            refs = table.setdefault(node[pa].props[1].split(b"\x00\x01")[0].decode("utf-8", "replace"), [])
            if entry not in refs:
                refs.append(entry)
    return table


def relink_textures(table, tex_dir):
    """재질마다 노드를 비우고 원칙형 BSDF 하나로 다시 짜 기준 FBX 표대로 텍스처를 건다(표에 없는 재질은 텍스처 없이)."""
    from bpy_extras.node_shader_utils import PrincipledBSDFWrapper
    used = sorted({s.material.name for o in bpy.context.scene.objects if o.type == "MESH" for s in o.material_slots if s.material})
    missing = set(table) - set(used)
    assert not missing, f"기준 FBX에 텍스처가 걸린 재질이 원본에 없다: {missing}"
    done = []
    for mat_name in used:
        mat = bpy.data.materials[mat_name]
        old = PrincipledBSDFWrapper(mat, is_readonly=True)
        color, alpha = tuple(old.base_color)[:3], old.alpha
        mat.use_nodes = True
        mat.node_tree.nodes.clear()
        new = PrincipledBSDFWrapper(mat, is_readonly=False)
        new.base_color, new.alpha = color, alpha
        for prop, fn in table.get(mat_name, ()):
            if prop == "BaseColor":                                     # 텍스처가 없는 재질은 단색(fn = RGB) — 미호크 머리·수염·깃털
                new.base_color = tuple(fn)
                done.append(f"{mat_name}.BaseColor={tuple(fn)}")
                continue
            assert prop in FBX_TEX_SLOT, f"{mat_name}: 옮길 줄 모르는 텍스처 속성 {prop}"
            file = os.path.join(tex_dir, fn)
            assert os.path.isfile(file), f"{mat_name}: 텍스처 파일이 없다 {file}"
            getattr(new, FBX_TEX_SLOT[prop]).image = bpy.data.images.load(file, check_existing=True)
            done.append(f"{mat_name}.{prop}={fn}")
    return done


def build_materials(spec, tex_dir):
    """재질을 새로 짠다 — mesh_material(메시 통째로 한 재질)·face_runs(면 순서 연속 구간) 뒤 textures 표대로 텍스처를 문다."""
    done = {}
    for mesh_name, mat_name in spec.get("mesh_material", {}).items():
        obj = bpy.data.objects[mesh_name]
        mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for p in obj.data.polygons:
            p.material_index = 0
        done[mesh_name] = {mat_name: len(obj.data.polygons)}
    for mesh_name, runs in spec.get("face_runs", {}).items():
        obj = bpy.data.objects[mesh_name]
        polys = obj.data.polygons
        assert sum(n for _, n in runs) == len(polys), f"{mesh_name}: 면 수 {len(polys)} ≠ 구간 합 {sum(n for _, n in runs)}"
        order = list(dict.fromkeys(m for m, _ in runs))
        obj.data.materials.clear()
        for m in order:
            obj.data.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
        i = 0
        for m, n in runs:
            k = order.index(m)
            for j in range(i, i + n):
                polys[j].material_index = k
            i += n
        done[mesh_name] = {m: sum(n for mm, n in runs if mm == m) for m in order}
    done["텍스처"] = relink_textures(spec["textures"], tex_dir)
    return done


def reference(path):
    """지금 FBX에서 이름만 읽는다(블렌더가 이 파일의 결합을 못 살려도 이름·부모·재질은 믿을 수 있다)."""
    load(path, anim=False)
    arm = main_armature()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    return dict(bones={b.name: (b.parent.name if b.parent else None) for b in arm.data.bones},
                meshes={m.name for m in meshes}, mats={s.material.name for m in meshes for s in m.material_slots if s.material},
                empties={o.name for o in skeleton_empties(arm)}, textures=fbx_texture_table(path))


def apply_recipe(recipe, ref, report=None):
    scene = bpy.context.scene
    glb_mats = sorted({s.material.name for o in scene.objects if o.type == "MESH" for s in o.material_slots if s.material})
    ref = dict(ref, meshes=ref["meshes"] - set(recipe.get("drop_meshes", ())))     # 기준 FBX에 있어도 뺄 조각(PM 승인)
    for old, new in recipe.get("mesh_alias", {}).items():             # glb 이름이 기준 FBX와 다른 메시(정점 수·재질·모양 키로 대조함)
        o = bpy.data.objects.get(old)
        assert o is not None and o.type == "MESH", f"이름 바꿀 메시가 원본에 없다: {old}"
        clash = bpy.data.objects.get(new)
        if clash is not None and clash != o:
            clash.name = new + "__원본노드"                          # 🔴 glb엔 같은 이름의 빈 노드가 있어 그냥 바꾸면 .001이 붙어 지워졌다
        o.name = new
        assert o.name == new, f"메시 이름을 {new}로 못 바꿨다({o.name})"
    for o in [o for o in scene.objects if o.type == "MESH" and o.name not in ref["meshes"]]:
        bpy.data.objects.remove(o, do_unlink=True)                       # 포치타·외곽선 껍데기·Icosphere 등 기준에 없는 메시
    meshes = [o for o in scene.objects if o.type == "MESH"]
    missing = ref["meshes"] - {m.name for m in meshes}
    assert not missing, f"기준 FBX에 있는데 원본에 없는 메시: {missing}"
    for src, dst in recipe.get("material_alias", {}).items():
        for m in meshes:
            for slot in m.material_slots:
                if slot.material and slot.material.name == src:
                    slot.material = bpy.data.materials.get(dst) or slot.material
    arm = main_armature()
    for o in [o for o in scene.objects if o.type == "ARMATURE" and o != arm]:
        bpy.data.objects.remove(o, do_unlink=True)                       # 포치타 뼈대
    for old, new in recipe.get("rename", {}).items():
        assert old in arm.data.bones, f"이름 바꿀 뼈가 원본에 없다: {old}"
        arm.data.bones[old].name = new                                    # 스킨 메시의 정점 그룹 이름도 같이 바뀐다
        for m in meshes:
            g = m.vertex_groups.get(old)
            if g is not None:
                g.name = new
    moved = []
    bpy.context.view_layer.objects.active = arm
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, parent in ref["bones"].items():
        eb = arm.data.edit_bones.get(name)
        assert eb is not None, f"기준 FBX의 뼈가 원본에 없다: {name}"
        now = eb.parent.name if eb.parent else None
        if now != parent:
            eb.use_connect = False
            eb.parent = arm.data.edit_bones[parent] if parent else None   # 에딧 본은 세계 위치를 그대로 두고 부모만 바꾼다
            moved.append(f"{name}: {now} → {parent}")
    bpy.ops.object.mode_set(mode="OBJECT")
    assert set(arm.data.bones.keys()) == set(ref["bones"]), f"뼈 이름 차: {set(arm.data.bones.keys()) ^ set(ref['bones'])}"
    assert all((b.parent.name if b.parent else None) == ref["bones"][b.name] for b in arm.data.bones)
    mats = {s.material.name for m in meshes for s in m.material_slots if s.material}
    assert mats <= ref["mats"] and (mats == ref["mats"] or recipe.get("drop_meshes")), f"재질 이름 차: 원본 {sorted(mats)} / 기준 {sorted(ref['mats'])}"
    if report is not None:
        report["재질 대조"] = f"glb {glb_mats} → FBX {sorted(mats)}"
        report["부모 옮김"] = moved
    bpy.context.view_layer.update()


def bone_above(obj, arm, empties=()):
    o = obj
    while o is not None:
        if o.parent is not None and o.parent.name in empties:            # 뼈로 살린 빈 오브젝트 밑에 매달린 조각
            return o.parent.name
        if o.parent == arm and o.parent_type == "BONE" and o.parent_bone:
            return o.parent_bone
        o = o.parent
    return None


def skeleton_empties(arm, keep=None):
    """부모 사슬이 (빈 오브젝트만 거쳐) 아마추어에 닿는 빈 오브젝트 = FBX 뼈대 속 Null 노드. 부모 먼저 순서.
    keep을 주면 그 이름만(glb에서 다시 짓는 넷 — 기준 FBX에 있던 것만)."""
    def depth(o):
        d, p = 0, o.parent
        while p is not None and p != arm:
            if p.type != "EMPTY":
                return None
            d, p = d + 1, p.parent
        return d if p == arm else None
    found = [(depth(o), o.name, o) for o in bpy.context.scene.objects if o.type == "EMPTY"]
    return [o for d, _, o in sorted((t for t in found if t[0] is not None), key=lambda t: (t[0], t[1]))
            if keep is None or o.name in keep]


def empty_parent(o, arm, names):
    """살린 뼈의 부모 이름 — 가장 가까운, 살린 빈 오브젝트 또는 매달린 뼈. 아마추어에 바로 붙었으면 None."""
    p, child = o.parent, o
    while p is not None and p != arm:
        if p.name in names:
            return p.name
        p, child = p.parent, p
    return child.parent_bone if child.parent_type == "BONE" and child.parent_bone else None


def pick(arm, name, pattern):
    if name:
        return arm.data.bones.get(name)
    return next((b for b in arm.data.bones if pattern.search(b.name) and not any(x in b.name.lower() for x in SKIP)), None)


def pose_head(arm, bone):
    return arm.matrix_world @ arm.pose.bones[bone.name].head


def orientation(cfg, arm, report):
    """방향 행렬 R(3×3) — 기본 자세의 세계 → 위 +Z · 정면 −Y."""
    R = Matrix.Identity(3)
    if arm is None or cfg["kind"] == "prop":
        return R
    if cfg["kind"] == "beast":
        head = pick(arm, cfg.get("head"), re.compile(r"(?i)^head"))
        # tail: 뿌리 뼈가 몸에서 멀리 떨어진 리그(잉어 _rootJoint가 세계 원점) — 머리↔꼬리 뼈로 방향을 잰다
        root = arm.data.bones[cfg["tail"]] if cfg.get("tail") else next(b for b in arm.data.bones if b.parent is None)
        fwd = pose_head(arm, head) - pose_head(arm, root)
        fwd.z = 0.0
        yaw = math.atan2(fwd.x, -fwd.y)
        report["방향"] = f"머리 쪽 {math.degrees(-yaw):+.0f}°"
        return Matrix.Rotation(-yaw, 3, "Z")
    hips, head = pick(arm, cfg.get("hips"), HIPS), pick(arm, cfg.get("head"), HEAD)
    report["엉덩이/머리"] = f"{hips.name if hips else None}/{head.name if head else None}"
    if hips and head:
        up = (pose_head(arm, head) - pose_head(arm, hips)).normalized()
        R = up.rotation_difference(Vector((0, 0, 1))).to_matrix()
        report["위"] = tuple(round(c, 2) for c in up)
    lefts = [pose_head(arm, b) for b in arm.data.bones if UPPER.search(b.name) and LEFT.search(b.name) and not RIGHT.search(b.name)]
    rights = [pose_head(arm, b) for b in arm.data.bones if UPPER.search(b.name) and RIGHT.search(b.name) and not LEFT.search(b.name)]
    if lefts and rights:
        right = R @ (sum(rights, Vector()) / len(rights) - sum(lefts, Vector()) / len(lefts))
        right.z = 0.0
        fwd = Vector((0, 0, 1)).cross(right.normalized())
        yaw = math.atan2(fwd.x, -fwd.y)
        R = Matrix.Rotation(-yaw, 3, "Z") @ R
        report["정면"] = f"{math.degrees(-yaw):+.0f}° (좌 {len(lefts)}·우 {len(rights)})"
    else:
        report["정면"] = "좌우 뼈 못 찾음 — 회전 안 함"
    return R


def sample_clips(src, arm_name, recipe, ref, guess_bind=True, scene_basis=False):
    """애니메이션째 읽어 액션마다 프레임별 세계 뼈 행렬.
    scene_basis: 매 프레임 되돌릴 자세 = 가져온 직후 자세(glTF 노드 기본 TRS를 결합 자세 기준으로 옮긴 것). 🔴 잉어(2026-09-15): IBM이 틀린 glb는
    곡선 없는 채널(Bone_00 이동 등)이 단위가 아니라 노드 기본값이어야 한다 — 단위로 되돌리면 모든 프레임이 결합 자리(6,0,−13)에 떨어져 새 쉬는 자세와 47m 어긋났다."""
    load(src, anim=True, guess_bind=guess_bind)
    if recipe is not None:
        apply_recipe(recipe, ref)
    arm = bpy.data.objects[arm_name]
    hold = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones} if scene_basis else None
    if scene_basis and arm.animation_data:
        for t in arm.animation_data.nla_tracks:                         # NLA 트랙 끄고 활성 액션 하나만(clip_pose와 같은 조건)
            t.mute = True
    empties = skeleton_empties(arm, ref["empties"] if ref is not None else None)
    from_gltf = src.lower().endswith((".glb", ".gltf"))
    clips = []
    for act in list(bpy.data.actions):
        ad = arm.animation_data or arm.animation_data_create()
        ad.action = act
        if hasattr(ad, "action_slot") and len(act.slots):
            ad.action_slot = act.slots[0]
        # 🔴 glTF로 읽은 액션은 frame_range가 1~1로 나와 한 프레임만 구웠다(이호준 시험) — 실제 키 범위로
        curves = list(act.fcurves) if hasattr(act, "fcurves") else []
        if not curves:
            curves = [fc for layer in act.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]
        keys = [kp.co.x for fc in curves for kp in fc.keyframe_points]
        f0, f1 = (int(math.floor(min(keys))), int(math.ceil(max(keys)))) if keys else (int(round(f)) for f in act.frame_range)
        frames = []
        for f in range(f0, f1 + 1):
            # 🔴 액션이 키를 안 가진 뼈는 앞서 남은 자세를 그대로 쥔다(이호준 키 1개짜리 첫 클립이 둘째 클립 1프레임 자세로 구워졌다).
            # 유니티에서 곡선 없는 뼈는 쉬는 자세 — 매 프레임 쉬는 자세로 되돌린 뒤 평가한다.
            for pb in arm.pose.bones:
                pb.matrix_basis = hold[pb.name] if hold else Matrix.Identity(4)
            bpy.context.scene.frame_set(f)
            world = {pb.name: arm.matrix_world @ pb.matrix for pb in arm.pose.bones}
            world.update({o.name: o.matrix_world.copy() for o in empties})
            frames.append(world)
        # 블렌더 FBX 가져오기는 액션 이름 앞에 「아마추어|」를 붙인다(강재규: 원본 테이크 「skeleton #1|skeleton #1|skeleton #1|All…」 →
        # 액션 「skeleton #1|」 + 그것). 🔴 예전엔 아마추어 이름과 같은 칸을 전부 떼 「skeleton #1|All…」로 바뀌었다. glTF 액션 이름은 원본 그대로.
        take = act.name[len(arm_name) + 1:] if not from_gltf and act.name.startswith(arm_name + "|") else act.name
        clips.append((take, f0, frames))
    return clips


def tpose_arms(arm, meshes, report, names=None):
    """쉬는 자세를 진짜 T자로(흔함_문필환, PM 2026-09-14): 좌우 Clavicle·UpperArm·Forearm을 몸 옆(L +X · R −X)으로 곧게, 아래팔을 굴려
    손바닥 아래(검지→새끼 방향이 앞 −Y의 반대 = 검지가 앞), 손·손가락 곧게. 그 자세로 메시를 굽고 쉬는 자세로 적용한다.
    🔴 유니티가 아바타를 만들 때 T자를 강제로 맞추는데, 쉬는 자세가 A자(수평 아래 43°)면 skeleton 행이 실제와 어긋나 어깨 근육값이 −1.99로
    범위를 넘고 Idle을 입히면 팔이 들렸다."""
    pose = arm.pose.bones

    def P(n):
        return arm.matrix_world @ pose[n].head

    def turn(n, R3):
        pivot = P(n)
        pose[n].matrix = Matrix.Translation(pivot) @ R3.to_4x4() @ Matrix.Translation(-pivot) @ pose[n].matrix
        bpy.context.view_layer.update()

    def align(n, a, b, target):
        cur = P(b) - P(a)
        if cur.length > 1e-6:
            turn(n, cur.normalized().rotation_difference(target.normalized()).to_matrix())

    def roll_to(n, axis, vec, target):
        axis = axis.normalized()
        a = (vec - axis * vec.dot(axis)).normalized()
        b = (target - axis * target.dot(axis)).normalized()
        turn(n, Matrix.Rotation(math.atan2(axis.dot(a.cross(b)), a.dot(b)), 3, axis))

    bpy.context.view_layer.update()
    before = {}
    have = lambda n: n in pose
    for side in "LR":
        d = Vector((1.0 if side == "L" else -1.0, 0.0, 0.0))
        # names = {"L": {"Clavicle": 뼈, "UpperArm": 뼈, "Forearm": 뼈, "Hand": 뼈, ...손가락 선택}, "R": {...}} — 없으면 Biped 이름(Bip001 L …)
        b = (lambda k, _m=names[side]: _m.get(k, f"__없음_{k}")) if names else (lambda k, _s=side: f"Bip001 {_s} {k}")
        if have(b("Clavicle")):                                         # 쇄골 없이 넘기면 안 굽힘(탐 켄치: 쇄골이 위·뒤로 45° — 펴면 어깨 몸통이 크게 틀어진다)
            align(b("Clavicle"), b("Clavicle"), b("UpperArm"), d)
        align(b("UpperArm"), b("UpperArm"), b("Forearm"), d)
        align(b("Forearm"), b("Forearm"), b("Hand"), d)
        skipped = []
        # 손가락이 다 있을 때만 손바닥 굴리기·손가락 펴기(모바일 추출 Biped는 Finger0·Finger1 두 줄뿐 — 특별함_이정범)
        if all(have(b(k)) for k in ("Finger1", "Finger4", "Finger2")):
            for _ in range(2):
                roll_to(b("Forearm"), d, P(b("Finger1")) - P(b("Finger4")), Vector((0.0, -1.0, 0.0)))   # 검지가 앞 = 손바닥 아래
                align(b("Hand"), b("Hand"), b("Finger2"), d)
        else:
            skipped.append("손바닥 굴리기")
            if have(b("Finger1")):
                align(b("Hand"), b("Hand"), b("Finger1"), d)
        for f in "1234":
            if have(b(f"Finger{f}1")):
                align(b(f"Finger{f}"), b(f"Finger{f}"), b(f"Finger{f}1"), d)
            if have(b(f"Finger{f}2")):
                align(b(f"Finger{f}1"), b(f"Finger{f}1"), b(f"Finger{f}2"), d)
        if have(b("Finger02")):
            thumb = P(b("Finger01")) - P(b("Finger0"))
            align(b("Finger01"), b("Finger01"), b("Finger02"), thumb)
        before[side] = dict(arm=tuple(round(c, 3) for c in (P(b("Hand")) - P(b("UpperArm"))).normalized()),
                            spread=tuple(round(c, 3) for c in (P(b("Finger1")) - P(b("Finger4"))).normalized()) if have(b("Finger4")) else None,
                            건너뜀=skipped)
    # 굽기: 변형된 메시를 데이터로, 자세를 쉬는 자세로
    dg = bpy.context.evaluated_depsgraph_get()
    posed_verts = {}
    for m in meshes:
        if skinned_to(m) != arm:
            continue
        assert not m.data.shape_keys, f"{m.name}: 모양 키가 있어 T자로 굽지 못한다"
        baked = bpy.data.meshes.new_from_object(m.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        m.data = baked
        posed_verts[m.name] = [v.co.copy() for v in baked.vertices]
    rest = {pb.name: (pb.head.copy(), pb.tail.copy(), pb.matrix.copy()) for pb in pose}
    for o in bpy.context.view_layer.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.use_connect = False
    for eb in arm.data.edit_bones:
        h, t, M = rest[eb.name]
        eb.head, eb.tail = h, t if (t - h).length > 1e-9 else h + M.col[1].xyz * 1e-3
        eb.align_roll(M.col[2].xyz)
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in pose:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    drift = 0.0
    for m in meshes:
        if m.name not in posed_verts:
            continue
        ev = m.evaluated_get(dg)
        me = ev.to_mesh()
        drift = max(drift, max((ev.matrix_world @ v.co - m.matrix_world @ c).length for v, c in zip(me.vertices, posed_verts[m.name])))
        ev.to_mesh_clear()
    assert drift < 1e-4, f"T자 굽기 뒤 메시가 {drift:.5f} 움직였다"
    report["T자"] = dict(before, 굽기_오차=round(drift, 6))


def clip_pose(src, guess_bind, clip, frame, bones):
    """glTF 클립 한 프레임의 뼈 자세(쉬는 자세 기준 basis)를 읽는다 — 바인드 자세에 없는 무기 쥔 자리·늘어진 천(특별함_박민수 가렌)."""
    load(src, anim=True, guess_bind=guess_bind)
    arm = main_armature()
    ad = arm.animation_data
    for t in ad.nla_tracks:                                             # glTF 가져오기는 클립마다 NLA 트랙을 깐다 — 다 끄고 한 클립만
        t.mute = True
    act = bpy.data.actions[clip]
    ad.action = act
    if hasattr(ad, "action_slot") and act.slots:
        ad.action_slot = act.slots[0]
    bpy.context.scene.frame_set(int(frame))
    if bones == "all":                                                  # 바인드(IBM)가 틀린 glb(잉어): 한 프레임 전체를 새 쉬는 자세로
        bones = [pb.name for pb in arm.pose.bones]
    missing = [n for n in bones if n not in arm.pose.bones]
    assert not missing, f"클립 자세로 굳힐 뼈가 없다 {missing}"
    return {n: arm.pose.bones[n].matrix_basis.copy() for n in bones}


def _normalized(M):
    loc, q, _ = M.decompose()
    return Matrix.Translation(loc) @ q.to_matrix().to_4x4()


def safe_texture_name(material_name):
    """재질 이름 → 파일 이름(아이언맨 HD_Ironman:black → HD_Ironman_black) — 영문·숫자·_·- 밖의 글자(:·공백 등)는 _로."""
    return re.sub(r"[^0-9A-Za-z_\-]+", "_", material_name)


def write_solid_png(dst, rgb_linear, size=4):
    """단색 텍스처(sRGB 8비트 RGB PNG) — 기본색만 있는 재질을 유니티가 재질 이름으로 텍스처를 찾아도 회색이 안 되게(황정기 함정)."""
    import struct as _struct
    import zlib
    enc = lambda c: 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    px = bytes(int(round(max(0.0, min(1.0, enc(max(0.0, c)))) * 255)) for c in rgb_linear[:3])
    raw = b"".join(b"\x00" + px * size for _ in range(size))
    chunk = lambda t, d: _struct.pack(">I", len(d)) + t + d + _struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    with open(dst, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", _struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def write_rgb_png(src, dst):
    """알파를 뺀 8비트 RGB PNG로 다시 쓴다 — 바운티러시 _diff의 알파는 투명이 아니라 명암 마스크(중간값 99%)라 유니티가 투명으로 읽지 않게.
    블렌더 저장(색 관리)을 거치지 않고 바이트 픽셀을 그대로 zlib로 싸서 색이 1도 안 바뀐다."""
    import struct
    import zlib
    import numpy as np
    img = bpy.data.images.load(src, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    rgb = np.clip(np.round(px.reshape(h, w, 4)[::-1, :, :3] * 255.0), 0, 255).astype(np.uint8)
    bpy.data.images.remove(img)
    raw = b"".join(b"\x00" + row.tobytes() for row in rgb)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(dst, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))
    return dst


def unscale_glb_joint(src, joint_name):
    """glb 조인트 하나의 큰 균일 배율(가로우 NULL_0133 ×10000 + inverseBindMatrices ×0.0001)을 풀어 임시 glb로 다시 싼다.
    블렌더 뼈는 배율을 못 담아 그 밑 뼈대·스킨이 한 줄로 뭉개졌다(2026-09-14). 자손 조인트 이동 ×배율, 그 조인트·자손의 IBM 앞에 ×배율 —
    G_new·IBM_new = G_old·S⁻¹·S·IBM_old라 스킨 정점 세계 위치는 그대로. 결합 자세 추정(gltf_guess_bind)도 꺼야 바르게 선다."""
    import json as _json
    import struct as _struct
    import numpy as np
    b = open(src, "rb").read()
    n = _struct.unpack_from("<I", b, 12)[0]
    j = _json.loads(b[20:20 + n])
    blen = _struct.unpack_from("<I", b, 20 + n)[0]
    binc = bytearray(b[20 + n + 8:20 + n + 8 + blen])
    nodes = j["nodes"]
    ji = next(i for i, nd in enumerate(nodes) if nd.get("name") == joint_name)
    sc = nodes[ji]["scale"]
    assert max(sc) - min(sc) < 1e-6, f"{joint_name}: 균일 배율이 아니다 {sc}"
    s = float(sc[0])
    nodes[ji]["scale"] = [1.0, 1.0, 1.0]
    desc, stack = set(), list(nodes[ji].get("children", []))
    while stack:
        c = stack.pop()
        desc.add(c)
        nodes[c]["translation"] = [v * s for v in nodes[c].get("translation", [0.0, 0.0, 0.0])]
        stack += nodes[c].get("children", [])
    S = np.diag([s, s, s, 1.0])
    for sk in j["skins"]:
        acc = j["accessors"][sk["inverseBindMatrices"]]
        bv = j["bufferViews"][acc["bufferView"]]
        start = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
        m = np.frombuffer(bytes(binc[start:start + 64 * acc["count"]]), dtype="<f4").reshape(-1, 4, 4).astype(np.float64)
        for k, joint in enumerate(sk["joints"]):
            if joint == ji or joint in desc:
                m[k] = (S @ m[k].T).T                                     # glTF 열 우선 → 행 우선으로 곱하고 되돌림
        binc[start:start + 64 * acc["count"]] = m.astype("<f4").tobytes()
    js = _json.dumps(j, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    binc += b"\x00" * (-len(binc) % 4)
    out = os.path.join(tempfile.mkdtemp(prefix="fix_unit_glb_"), os.path.basename(src))
    with open(out, "wb") as f:
        f.write(_struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binc)) + _struct.pack("<II", len(js), 0x4E4F534A) + js
                + _struct.pack("<II", len(binc), 0x004E4942) + bytes(binc))
    return out, dict(배율=s, 자손=len(desc))


def repair_glb_identity_ibm(src):
    """가중치 없는 조인트에 inverseBindMatrices가 단위행렬로 박힌 glb(mGear 리그 탐 켄치 — 왼팔·왼다리 본 뼈, neck_C0_0 등 19개)를 고쳐 임시 glb로 다시 싼다.
    결합 자세 추정이 IBM으로 뼈를 세우니 그 뼈들이 원점에 뭉개졌다(2026-09-14). 결합 세계 행렬 B = IBM⁻¹:
      자식 중 IBM이 멀쩡한 게 있으면 B = B_자식 × 자식 로컬⁻¹(leaf_ 자식은 로컬이 단위라 본 뼈 = leaf 자리) — 아래에서 위로 되풀이,
      없으면 B = B_부모 × 제 로컬(끝 뼈) — 위에서 아래로. 회전은 직교화(뼈는 배율을 못 담는다). 가중치 있는 조인트는 IBM이 멀쩡하니 스킨 정점은 그대로."""
    import json as _json
    import struct as _struct
    import numpy as np
    b = open(src, "rb").read()
    n = _struct.unpack_from("<I", b, 12)[0]
    j = _json.loads(b[20:20 + n])
    blen = _struct.unpack_from("<I", b, 20 + n)[0]
    binc = bytearray(b[20 + n + 8:20 + n + 8 + blen])
    nodes = j["nodes"]
    parent = {c: i for i, nd in enumerate(nodes) for c in nd.get("children", [])}

    def local(i):
        nd = nodes[i]
        if "matrix" in nd:
            return np.array(nd["matrix"], dtype=np.float64).reshape(4, 4).T
        x, y, z, w = nd.get("rotation", [0.0, 0.0, 0.0, 1.0])
        R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                      [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                      [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
        M = np.eye(4)
        M[:3, :3] = R * np.array(nd.get("scale", [1.0, 1.0, 1.0]))
        M[:3, 3] = nd.get("translation", [0.0, 0.0, 0.0])
        return M

    def ortho(M):
        U, _, Vt = np.linalg.svd(M[:3, :3])
        out = M.copy()
        out[:3, :3] = U @ Vt
        return out

    fixed = []
    for sk in j["skins"]:
        acc = j["accessors"][sk["inverseBindMatrices"]]
        bv = j["bufferViews"][acc["bufferView"]]
        start = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
        m = np.frombuffer(bytes(binc[start:start + 64 * acc["count"]]), dtype="<f4").reshape(-1, 4, 4).astype(np.float64)
        slot = {joint: k for k, joint in enumerate(sk["joints"])}
        bind = {joint: np.linalg.inv(m[k].T) for joint, k in slot.items() if np.abs(m[k] - np.eye(4)).max() > 1e-6}
        todo = [joint for joint in sk["joints"] if joint not in bind]
        changed = True
        while changed:
            changed = False
            for joint in list(todo):
                kid = next((c for c in nodes[joint].get("children", []) if c in bind), None)
                if kid is not None:
                    bind[joint] = ortho(bind[kid] @ np.linalg.inv(local(kid)))
                elif parent.get(joint) in bind:
                    bind[joint] = ortho(bind[parent[joint]] @ local(joint))
                else:
                    continue
                todo.remove(joint)
                m[slot[joint]] = np.linalg.inv(bind[joint]).T
                fixed.append(nodes[joint].get("name", str(joint)))
                changed = True
        binc[start:start + 64 * acc["count"]] = m.astype("<f4").tobytes()
    js = _json.dumps(j, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    binc += b"\x00" * (-len(binc) % 4)
    out = os.path.join(tempfile.mkdtemp(prefix="fix_unit_glb_"), os.path.basename(src))
    with open(out, "wb") as f:
        f.write(_struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binc)) + _struct.pack("<II", len(js), 0x4E4F534A) + js
                + _struct.pack("<II", len(binc), 0x004E4942) + bytes(binc))
    return out, dict(고친_조인트=len(fixed), 이름=fixed)


def extract_archive(archive, members):
    """압축 원본(rar·zip)에서 필요한 파일만 임시 폴더로 — bsdtar(libarchive)가 rar도 읽는다. 받은 순서대로 경로를 돌려준다."""
    tmp = tempfile.mkdtemp(prefix="fix_unit_arc_")
    subprocess.run(["bsdtar", "-xf", os.path.expanduser(archive), "-C", tmp] + list(members), check=True)
    out = [os.path.join(tmp, m) for m in members]
    for f in out:
        assert os.path.isfile(f), f"압축에서 못 꺼냈다: {f}"
    return out


def original(cfg):
    """덮어쓰기 전 원본 파일 — git 커밋 rev에서 임시 폴더로 꺼낸다(Textures/는 유닛 폴더를 링크해 텍스처 경로가 풀리게)."""
    data = subprocess.run(["git", "-C", ROOT, "show", f"{cfg['rev']}:{cfg['path']}"], capture_output=True, check=True).stdout
    tmp = tempfile.mkdtemp(prefix="fix_unit_")
    out = os.path.join(tmp, os.path.basename(cfg["path"]))
    with open(out, "wb") as f:
        f.write(data)
    tex = os.path.join(ROOT, os.path.dirname(cfg["path"]), "Textures")
    if os.path.isdir(tex):
        os.symlink(tex, os.path.join(tmp, "Textures"))
    return out


def fix(name, cfg, out_dir=None, save_blend=False):
    if cfg.get("hold"):
        return {"이름": name, "보류": cfg["hold"]}
    dst_path = os.path.join(ROOT, cfg["path"])
    orig = original(cfg) if cfg.get("rev") else dst_path
    src = cfg.get("source", orig)
    arc_textures = []
    unscale_info = None
    if cfg.get("glb_unscale_joint"):
        src, unscale_info = unscale_glb_joint(src, cfg["glb_unscale_joint"])
    ibm_info = None
    if cfg.get("glb_fix_identity_ibm"):                                 # 가중치 없는 조인트의 단위행렬 IBM → 뼈가 원점에 뭉개짐(탐 켄치)
        src, ibm_info = repair_glb_identity_ibm(src)
    if cfg.get("archive"):                                              # 새로 들이는 스킨(git 원본 없음): 다운로드 압축에서 FBX·텍스처를 꺼내 읽는다
        *outer, member = cfg["archive"]                                 # (압축, 파일) 또는 (zip, zip 안 rar, 파일) — 겹친 압축은 안쪽부터 꺼낸다
        arc = outer[0]
        for inner in outer[1:]:
            arc = extract_archive(arc, [inner])[0]
        got = extract_archive(arc, [member] + list(cfg.get("archive_textures", ())))
        src, arc_textures = got[0], got[1:]
        if cfg.get("archive_rgb"):                                      # 압축 속 텍스처를 알파 뺀 RGB PNG로 유닛 Textures/에 새 이름으로(재질을 짜기 전에 — 재질 표가 이 파일을 부른다)
            tex_repo = os.path.join(os.path.dirname(dst_path), "Textures")
            os.makedirs(tex_repo, exist_ok=True)
            members = list(cfg["archive_rgb"])
            for got_path, member_name in zip(extract_archive(arc, members), members):
                out_path = os.path.join(tex_repo, cfg["archive_rgb"][member_name])
                write_rgb_png(got_path, out_path)
                arc_textures.append(out_path)
    recipe = cfg.get("recipe") if "source" in cfg else None
    report = {"이름": name, "원본": f"{cfg.get('rev', '작업 파일')} {os.path.basename(src)}"}
    if unscale_info:
        report["glb 조인트 배율 풂"] = dict(unscale_info, 조인트=cfg["glb_unscale_joint"])
    if ibm_info:
        report["glb 단위 IBM 고침"] = ibm_info["고친_조인트"]
    ref = reference(orig) if recipe is not None else None
    guess = cfg.get("gltf_guess_bind", True)
    held_pose = clip_pose(src, guess, *cfg["pose_from_clip"]) if cfg.get("pose_from_clip") else None
    load(src, anim=False, guess_bind=guess)
    if recipe is not None:
        apply_recipe(recipe, ref, report)
    if cfg.get("default_pose_only"):
        # 🔴 아이언맨(2026-09-15): FBX 기본 자세가 T자(팔 수평·다리 곧게)인데 아머 판 보조 뼈(Bone02~21·Rseq/Lseq)는 등 날개·종아리 판을 연 채였다
        #   (등 뒤로 막대처럼 뻗고 헬멧 옆 판이 섬). 결합 자세는 판이 다 닫힌 깨끗한 모습 — 이름이 맞는 뼈만 기본 자세를 두고 나머지는 결합 자세로 되돌린다.
        arm0 = main_armature()
        keep_re = re.compile(cfg["default_pose_only"])
        reset = [pb.name for pb in arm0.pose.bones if not keep_re.search(pb.name)]
        for bname in reset:
            arm0.pose.bones[bname].matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        report["결합 자세로 되돌린 뼈"] = len(reset)
    if held_pose:                                                       # 기본 자세로 두면 아래 「기본 자세 그대로 붙잡기」가 메시·뼈를 그 자세로 굽는다
        arm0 = main_armature()
        for bname, basis in held_pose.items():
            arm0.pose.bones[bname].matrix_basis = basis
        bpy.context.view_layer.update()
        report["클립 자세로 굳힌 뼈"] = f"{cfg['pose_from_clip'][0]} {cfg['pose_from_clip'][1]}프레임 {len(held_pose)}개"
    if cfg.get("mirror_x"):                                             # 좌우 뒤집힌 추출본(LoL): 뿌리 오브젝트마다 세계 X 거울 — 아래 G·뼈·메시가 거울 좌표로 짜인다
        Mx = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))
        for o in [o for o in bpy.context.scene.objects if o.parent is None]:
            o.matrix_world = Mx @ o.matrix_world
        bpy.context.view_layer.update()
        arm0 = main_armature()
        assert arm0 is None or arm0.matrix_world.determinant() < 0, f"{name}: X 거울이 안 먹었다"
        report["X 거울"] = "좌우 뒤집힌 원본 되돌림"
    if cfg.get("drop_meshes"):                                          # 바운티러시 pl_ 리그: 표정·손 모양 변형 메시가 한자리에 겹쳐 있다 — 기본만 남긴다
        assert not cfg.get("anim") or cfg.get("anim_drop_ok"), f"{name}: drop_meshes는 클립 다시 굽기와 같이 못 쓴다(다시 불러온 뒤 또 빼려면 anim_drop_ok)"
        for gone in cfg["drop_meshes"]:
            o = bpy.data.objects.get(gone)
            assert o is not None and o.type == "MESH", f"{name}: 뺄 메시가 없다 {gone}"
            bpy.data.objects.remove(o, do_unlink=True)
        report["뺀 메시"] = list(cfg["drop_meshes"])
    if cfg.get("decimate"):
        # 부품 많은 딱딱한 표면(아이언맨 21만 삼각형): 큰 메시만 균등 감량 — 아마추어보다 먼저 적용해 정점 그룹이 보간돼 따라가게. 단색 텍스처라 UV 이음새 걱정 없음.
        dec = cfg["decimate"]
        tri_before = tri_after = cut = 0
        for m in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            tris = sum(len(p.vertices) - 2 for p in m.data.polygons)
            tri_before += tris
            if tris >= dec.get("min_tris", 1000):
                assert not m.data.shape_keys, f"{name}: {m.name} 모양 키가 있어 감량 못 함"
                if m.data.users > 1:
                    m.data = m.data.copy()
                mod = m.modifiers.new("fix_decimate", "DECIMATE")
                mod.ratio = dec["ratio"]
                mod.use_collapse_triangulate = True
                with bpy.context.temp_override(object=m, active_object=m, selected_objects=[m]):
                    bpy.ops.object.modifier_move_to_index(modifier="fix_decimate", index=0)
                    bpy.ops.object.modifier_apply(modifier="fix_decimate")
                cut += 1
            tri_after += sum(len(p.vertices) - 2 for p in m.data.polygons)
        report["감량"] = f"삼각형 {tri_before} → {tri_after} (메시 {cut}개 ×{dec['ratio']}, {dec.get('min_tris', 1000)}삼각형 이상만)"
    if cfg.get("rename_bones"):                                         # 사람형 매핑 뼈 이름을 표준으로(가중치 그룹 이름도 같이)
        arm0 = main_armature()
        for old, new in cfg["rename_bones"].items():
            b = arm0.data.bones.get(old)
            assert b is not None, f"{name}: 이름 바꿀 뼈가 없다 {old}"
            assert new not in arm0.data.bones, f"{name}: 새 이름이 이미 있다 {new}"
            b.name = new
            for m in bpy.context.scene.objects:
                g = m.vertex_groups.get(old) if m.type == "MESH" else None
                if g is not None:
                    g.name = new
        report["이름 바꾼 뼈"] = len(cfg["rename_bones"])
    scene = bpy.context.scene
    arm = main_armature()
    arm_name = arm.name if arm else None
    clips = []
    if arm is not None and cfg.get("anim"):
        clips = sample_clips(src, arm_name, recipe, ref, guess, scene_basis=cfg.get("clip_scene_basis", False))
        load(src, anim=False, guess_bind=guess)
        if recipe is not None:
            apply_recipe(recipe, ref)
        arm = bpy.data.objects[arm_name]
        if held_pose:                                                   # 🔴 클립 읽느라 다시 불러오면 굳힌 자세가 날아간다(잉어) — 다시 입힌다
            for bname, basis in held_pose.items():
                arm.pose.bones[bname].matrix_basis = basis
            bpy.context.view_layer.update()
        if cfg.get("drop_meshes"):
            for gone in cfg["drop_meshes"]:
                o = bpy.data.objects.get(gone)
                if o is not None:
                    bpy.data.objects.remove(o, do_unlink=True)
        if cfg.get("take_names"):                                       # 원본 테이크 이름 → 유니티 클립 이름(잉어 Scene → Idle)
            clips = [(cfg["take_names"].get(t, t), f0, fr) for t, f0, fr in clips]
            report["테이크 이름"] = [c[0] for c in clips]
    scene = bpy.context.scene
    if recipe is not None:
        report["텍스처"] = relink_textures(ref["textures"], os.path.join(os.path.dirname(dst_path), "Textures"))
    if cfg.get("glb_images"):                                           # glb 내장 이미지를 원본 바이트 그대로 재질 이름 기준 파일로(재질을 짜기 전에)
        import json as _json
        import struct as _struct
        tex_repo = os.path.join(os.path.dirname(dst_path), "Textures")
        os.makedirs(tex_repo, exist_ok=True)
        raw = open(src, "rb").read()
        jlen = _struct.unpack_from("<I", raw, 12)[0]
        gj = _json.loads(raw[20:20 + jlen])
        blen = _struct.unpack_from("<I", raw, 20 + jlen)[0]
        gbin = raw[20 + jlen + 8:20 + jlen + 8 + blen]
        for index, fname in cfg["glb_images"].items():
            bv = gj["bufferViews"][gj["images"][index]["bufferView"]]
            with open(os.path.join(tex_repo, fname), "wb") as f:
                f.write(gbin[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]])
            arc_textures.append(os.path.join(tex_repo, fname))
    if cfg.get("rename_regex"):                                         # 번호 꼬리 이름(Hips_01·LeftHandIndex1_022) → mixamorig 표준 이름, 가중치 그룹 같이
        pattern, repl = cfg["rename_regex"]
        arm0 = main_armature()
        table = {b.name: re.fullmatch(pattern, b.name).expand(repl) for b in arm0.data.bones if re.fullmatch(pattern, b.name)}   # 🔴 re.sub은 이름 속 _0_ 마다 또 바꿨다(yixin_weapon_0_nocloth_45) — 통째 일치로만
        assert len(set(table.values())) == len(table), f"{name}: 이름 바꾸면 겹친다"
        for old, new in table.items():
            arm0.data.bones[old].name = new
            for m in bpy.context.scene.objects:
                g = m.vertex_groups.get(old) if m.type == "MESH" else None
                if g is not None:
                    g.name = new
        report["이름 바꾼 뼈"] = len(table)
    if cfg.get("copy_textures"):                                        # 흩어진 원본 텍스처를 재질 이름 기준 파일명으로 유닛 Textures/에(재질을 짜기 전에)
        tex_repo = os.path.join(os.path.dirname(dst_path), "Textures")
        os.makedirs(tex_repo, exist_ok=True)
        for src_tex, dst_name in cfg["copy_textures"].items():
            shutil.copy2(os.path.expanduser(src_tex), os.path.join(tex_repo, dst_name))
            arc_textures.append(os.path.join(tex_repo, dst_name))
    mat_spec = cfg.get("materials")
    if cfg.get("solid_textures"):
        # 3ds Max 맵이 파일 경로 없이 빠진 FBX(아이언맨 Map #1·#3) + 색만 있는 재질: 재질마다 기본색을 단색 PNG로 구워 Textures/<안전한 재질 이름>.png에 건다.
        #   None = 원본 기본색 그대로, (r,g,b) = 선형 색으로 바로잡음(3ds Max 내보내기가 black·darksilver를 0.8 회색, yellow를 흰색으로 떨궜다).
        from bpy_extras.node_shader_utils import PrincipledBSDFWrapper
        tex_repo = os.path.join(os.path.dirname(dst_path), "Textures")
        os.makedirs(tex_repo, exist_ok=True)
        table = dict((mat_spec or {}).get("textures", {}))
        solid_done = {}
        for mat_name, rgb in cfg["solid_textures"].items():
            mat = bpy.data.materials.get(mat_name)
            assert mat is not None, f"{name}: 단색 텍스처 재질이 없다 {mat_name}"
            color = tuple(rgb) if rgb is not None else tuple(PrincipledBSDFWrapper(mat, is_readonly=True).base_color)[:3]
            fname = safe_texture_name(mat_name) + ".png"
            write_solid_png(os.path.join(tex_repo, fname), color)
            arc_textures.append(os.path.join(tex_repo, fname))
            table[mat_name] = [("BaseColor", color), ("DiffuseColor", fname)]
            solid_done[mat_name] = (fname, tuple(round(c, 3) for c in color))
        mat_spec = dict(mat_spec or {}, textures=table)
        report["단색 텍스처"] = solid_done
    if mat_spec:
        report["재질 새로"] = build_materials(mat_spec, os.path.join(os.path.dirname(dst_path), "Textures"))
    meshes = [o for o in scene.objects if o.type == "MESH"]
    mats_before = sorted({s.material.name for o in meshes for s in o.material_slots if s.material})
    if arm is not None and cfg.get("squash_chain"):
        # 쉬는 자세에서 입 밖으로 뻗은 혀(탐 켄치): 첫 뼈 머리를 중심으로 사슬 방향 성분만 factor배 — 정점(사슬 가중치 비율만큼)과 사슬 뼈 쉬는 자리를 같이.
        #   자세는 단위라 쉬는 자리를 옮겨도 스킨 변형은 그대로 0이다. 중심 뒤쪽(입 안 뿌리) 정점은 안 건드린다.
        sq = cfg["squash_chain"]
        chain, f = sq["bones"], sq["factor"]
        W0 = arm.matrix_world
        pivot = W0 @ arm.data.bones[chain[0]].head_local
        axis = ((W0 @ arm.data.bones[chain[-1]].head_local) - pivot).normalized()

        def squash(p, w=1.0):
            t = (p - pivot).dot(axis)
            return p - axis * (t * (1.0 - f) * w) if t > 0 else p

        moved = 0
        for m in meshes:
            ids = {g.index for g in m.vertex_groups if g.name in chain}
            if not ids:
                continue
            assert not m.data.shape_keys, f"{name}: {m.name} 모양 키가 있어 사슬을 줄이지 못한다"
            Mw = m.matrix_world
            Mi = Mw.inverted()
            for v in m.data.vertices:
                tot = sum(g.weight for g in v.groups)
                w = sum(g.weight for g in v.groups if g.group in ids)
                if w > 0 and tot > 0:
                    v.co = Mi @ squash(Mw @ v.co, w / tot)
                    moved += 1
        for o in scene.objects:
            o.select_set(o == arm)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode="EDIT")
        Wi = W0.inverted()
        for n in chain:
            eb = arm.data.edit_bones[n]
            eb.head, eb.tail = Wi @ squash(W0 @ eb.head), Wi @ squash(W0 @ eb.tail)
        bpy.ops.object.mode_set(mode="OBJECT")
        bpy.context.view_layer.update()
        report["사슬 줄임"] = f"{chain[0]}~{chain[-1]} ×{f} · 정점 {moved}"

    # ── 기본 자세 그대로 붙잡기
    if arm is not None:
        arm.data.pose_position = "POSE"
        bpy.context.view_layer.update()
        posed = max(max(abs(a - b) for ra, rb in zip(pb.matrix_basis, Matrix.Identity(4)) for a, b in zip(ra, rb)) for pb in arm.pose.bones)
        report["기본 자세≠쉬는 자세"] = round(posed, 4)
        if posed > 1e-4:
            dg = bpy.context.evaluated_depsgraph_get()
            keep_rest = set(cfg.get("bake_rest_meshes", ()))
            for m in meshes:
                if skinned_to(m) != arm or m.name in keep_rest:
                    # bake_rest_meshes: 🔴 특별함_박민석(2026-09-15 사장님 신고 「오른팔 이상」) — 원본 r_hand_open의 오른손 클러스터 결합이 오른쪽으로 4.47 밀려 있어
                    #   블렌더가 그걸로 RHand 뼈 쉬는 자리를 잡는다. 정점이 맞는 r_hand_close도 그 틀린 쉬는 자리 기준 기본 자세로 변형되면 왼손 쪽(+x)으로 넘어갔다 → 원시 정점 그대로.
                    continue
                assert not m.data.shape_keys, f"{m.name}: 모양 키가 있는데 기본 자세가 쉬는 자세와 달라 굳힐 수 없다"
                baked = bpy.data.meshes.new_from_object(m.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
                m.data = baked
        # 🛡 뼈와 메시가 같은 자리에 있는지
        mp = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
        bp = [arm.matrix_world @ pb.head for pb in arm.pose.bones if pb.name not in set(cfg.get("drop_bones", ()))]   # 뺄 뼈(빈 무기 뼈 등)는 대조에서 제외
        mlo = Vector((min(p.x for p in mp), min(p.y for p in mp), min(p.z for p in mp)))
        mhi = Vector((max(p.x for p in mp), max(p.y for p in mp), max(p.z for p in mp)))
        blo = Vector((min(p.x for p in bp), min(p.y for p in bp), min(p.z for p in bp)))
        bhi = Vector((max(p.x for p in bp), max(p.y for p in bp), max(p.z for p in bp)))
        ext = max(mhi - mlo)
        spill = max(max(mlo - blo), max(bhi - mhi), 0.0) / max(ext, 1e-12)
        report["뼈 넘침"] = round(spill, 3)
        if spill > 0.5 or max(bhi - blo) < 0.2 * ext:
            raise RuntimeError(f"{name}: 뼈({tuple(round(c, 3) for c in blo)}~{tuple(round(c, 3) for c in bhi)})와 메시"
                               f"({tuple(round(c, 3) for c in mlo)}~{tuple(round(c, 3) for c in mhi)})가 어긋난다 — 쓰지 않음")
    mesh_world = {m.name: m.matrix_world.copy() for m in meshes}

    # ── G = 이동 × 배율 × 방향
    R = orientation(cfg, arm, report)
    if cfg.get("orient_snap"):                                          # 엉덩이→머리가 몸 구부정해서 기운 경우(시저 22°) — 기울기는 버리고 90° 단위 축 교정만(유니티 AutoUpright와 같게)
        M3 = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
        used = set()
        for i in range(3):
            j = max((j for j in range(3) if j not in used), key=lambda j: abs(R[i][j]))
            used.add(j)
            M3[i][j] = 1.0 if R[i][j] > 0 else -1.0
        R = M3 if abs(M3.determinant() - 1.0) < 1e-6 else Matrix.Identity(3)
        report["방향 스냅"] = [[int(v) for v in row] for row in R]
    ignore = set(cfg.get("size_ignore_meshes", ()))                      # 키를 잴 때 뺄 메시(몸보다 높이 솟은 등의 검 등) — 바닥·가운데·배율은 몸으로
    assert ignore <= {o.name for o in meshes}, f"{name}: size_ignore_meshes에 없는 메시 {ignore - {o.name for o in meshes}}"
    pts = [R @ (mesh_world[o.name] @ v.co) for o in meshes if o.name not in ignore for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    axis, target = cfg["size"]
    current = (hi - lo).z if axis == "height" else max((hi - lo).x, (hi - lo).y)
    s = target / max(current, 1e-12)
    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    if cfg["kind"] == "human" and arm is not None:
        hips = pick(arm, cfg.get("hips"), HIPS)
        if hips is not None:
            h = R @ pose_head(arm, hips)
            cx, cy = h.x, h.y
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo.z)) @ R.to_4x4()

    # ── 새 아마추어(기본 자세 = 새 쉬는 자세)
    new_arm, rigid, root_bone, revived = None, {}, None, set()
    if arm is not None:
        W = arm.matrix_world
        G3 = G.to_3x3()
        bones = [(pb.name, G @ (W @ pb.head), G @ (W @ pb.tail), (G3 @ (W @ pb.matrix).to_3x3()).normalized(),
                  pb.parent.name if pb.parent else None, pb.bone.use_connect) for pb in arm.pose.bones]
        # 뼈대 속 Null(빈 오브젝트) → 뼈. 머리 = 세계 위치, 꼬리 = 자식 쪽(없으면 부모 방향으로 짧게)
        extra = [] if cfg.get("no_nulls") else skeleton_empties(arm, ref["empties"] if ref is not None else None)   # glb는 Null 뼈 개념이 없다 — 메시 노드 빈 오브젝트를 뼈로 살리지 않음
        extra_names = {o.name for o in extra}
        clash = extra_names & {b[0] for b in bones}
        assert not clash, f"{name}: 빈 오브젝트와 뼈 이름이 겹친다: {sorted(clash)[:5]}"
        bones += [(o.name, G @ o.matrix_world.translation, None, (G3 @ o.matrix_world.to_3x3()).normalized(),
                   empty_parent(o, arm, extra_names), False) for o in extra]
        heads = {b[0]: b[1] for b in bones}
        kids = {}
        for b in bones:
            if b[4]:
                kids.setdefault(b[4], []).append(b[0])
        tails = {b[0]: b[2] for b in bones if b[2] is not None}
        for bname, head, tail, rot, parent, connect in bones:
            if tail is not None:
                continue
            child = next((heads[c] for c in kids.get(bname, ()) if (heads[c] - head).length > 1e-6), None)
            if cfg.get("null_frames_from_node"):
                # 🔴 흔함_문필환(2026-09-14): 꼬리를 자식 쪽으로 지으면 살린 뼈만 +Y가 자식 쪽(블렌더 규약)이 되고, 원래 LimbNode(Pelvis·Spine·Clavicle)는
                #    +X가 자식 쪽(3ds Max 규약)이라 틀이 90° 섞였다 — 유니티 아바타 skeleton이 그 자리마다 90° 틀어졌다. 원래 Null 회전 그대로(뼈 Y = 노드 Y) 짓는다.
                tails[bname] = head + rot.col[1] * max((child - head).length if child is not None else 0.05, 1e-3)
            elif child is not None:
                tails[bname] = child
            elif parent in tails and (tails[parent] - heads[parent]).length > 1e-9:
                d = tails[parent] - heads[parent]
                tails[bname] = head + d.normalized() * max(0.3 * d.length, 0.01)
            else:
                tails[bname] = head + rot.col[1] * 0.05
        bones = [(n, h, tails[n], r, p, c) for n, h, t, r, p, c in bones]
        revived = extra_names
        report["Null→뼈"] = len(extra)
        if ref is not None and ref["empties"] - extra_names:
            report["기준에만 있는 Null"] = sorted(ref["empties"] - extra_names)
        rigid = {m.name: (bone_above(m, arm, extra_names) if skinned_to(m) is None else None) for m in meshes}
        root_bone = next(b.name for b in arm.data.bones if b.parent is None)
        data = bpy.data.armatures.new(arm.data.name)
        bpy.data.objects.remove(arm, do_unlink=True)
        new_arm = bpy.data.objects.new(arm_name, data)
        scene.collection.objects.link(new_arm)
        for o in scene.objects:
            o.select_set(o == new_arm)
        bpy.context.view_layer.objects.active = new_arm
        bpy.ops.object.mode_set(mode="EDIT")
        for bname, head, tail, rot, parent, connect in bones:
            eb = data.edit_bones.new(bname)
            eb.head = head
            eb.tail = tail if (tail - head).length > 1e-9 else head + rot.col[1] * 1e-3
            eb.align_roll(rot.col[2])
        for bname, head, tail, rot, parent, connect in bones:
            if parent:
                eb = data.edit_bones[bname]
                eb.parent = data.edit_bones[parent]
                eb.use_connect = connect and (data.edit_bones[parent].tail - eb.head).length < 1e-6
        # drop_bones: 가중치 없는 중간 뼈를 빼고 자식을 그 부모에 잇는다(세계 위치 그대로) — 흔함_문필환 시험(2026-09-14):
        #   유니티 아바타가 Bip001(무게중심, 매핑 안 됨) 아래 Bip001 Pelvis(Hips)를 바닥 높이로 저장해 하반신이 묻혔다 — 김경현처럼 Hips를 루트 밑에
        for gone in cfg.get("drop_bones", ()):
            eb = data.edit_bones.get(gone)
            assert eb is not None, f"{name}: 뺄 뼈가 없다 {gone}"
            for m in meshes:
                g = m.vertex_groups.get(gone)
                assert g is None or not any(ge.group == g.index and ge.weight > 1e-4 for v in m.data.vertices for ge in v.groups), \
                    f"{name}: {gone}에 가중치가 있어 뺄 수 없다({m.name})"
            for child in list(eb.children):
                child.use_connect = False
                child.parent = eb.parent
            data.edit_bones.remove(eb)
            report.setdefault("뺀 뼈", []).append(gone)
        for child_name, parent_name in cfg.get("reparent_bones", {}).items():
            eb, par = data.edit_bones.get(child_name), data.edit_bones.get(parent_name)
            assert eb is not None and par is not None, f"{name}: 다시 붙일 뼈가 없다 {child_name} → {parent_name}"
            eb.use_connect = False
            eb.parent = par
        if cfg.get("reparent_bones"):
            report["다시 붙인 뼈"] = len(cfg["reparent_bones"])
        bpy.ops.object.mode_set(mode="OBJECT")
        report["뼈"] = len(data.bones)

    # ── 메시: 세계로 굽고 새 아마추어에만 스킨
    for m in meshes:
        M = G @ mesh_world[m.name]
        m.parent = None
        if m.data.users > 1:
            m.data = m.data.copy()
        held_normals = None
        if M.determinant() < 0 and m.data.has_custom_normals:
            # 🔴 거울(mirror_x): transform + flip_normals만으론 사용자 법선이 틀어졌다(가렌 실측 정점 평균 오차 중앙 0.058·99% 0.96) —
            #    (면, 정점)으로 짝지어 역전치로 옮긴 법선을 다시 넣는다(오차 최대 0.002)
            N3 = M.to_3x3().inverted().transposed()
            poly_of = [0] * len(m.data.loops)
            for poly in m.data.polygons:
                poly_of[poly.loop_start:poly.loop_start + poly.loop_total] = [poly.index] * poly.loop_total
            held_normals = {(poly_of[i], lp.vertex_index): (N3 @ m.data.corner_normals[i].vector).normalized() for i, lp in enumerate(m.data.loops)}
        m.data.transform(M)
        if M.determinant() < 0:
            m.data.flip_normals()
            if held_normals is not None:
                poly_of = [0] * len(m.data.loops)
                for poly in m.data.polygons:
                    poly_of[poly.loop_start:poly.loop_start + poly.loop_total] = [poly.index] * poly.loop_total
                m.data.normals_split_custom_set([held_normals[(poly_of[i], lp.vertex_index)] for i, lp in enumerate(m.data.loops)])
        m.matrix_parent_inverse = Matrix.Identity(4)
        m.matrix_basis = Matrix.Identity(4)
        if new_arm is not None:
            for mod in [x for x in m.modifiers if x.type == "ARMATURE"]:
                m.modifiers.remove(mod)
            bone = rigid.get(m.name)
            if bone is not None or not m.vertex_groups:
                bone = bone or root_bone
                group = m.vertex_groups.get(bone) or m.vertex_groups.new(name=bone)
                group.add(list(range(len(m.data.vertices))), 1.0, "REPLACE")
            mod = m.modifiers.new("Armature", "ARMATURE")
            mod.object = new_arm
            m.parent = new_arm
            m.matrix_parent_inverse = Matrix.Identity(4)
            m.matrix_basis = Matrix.Identity(4)
    if new_arm is not None and cfg.get("tpose_arms"):
        tpose_arms(new_arm, meshes, report, cfg["tpose_arms"] if isinstance(cfg["tpose_arms"], dict) else None)
    if new_arm is not None and cfg.get("hem_follow"):
        # 🔴 특별함_박예원(사보) 유니티 Idle(2026-09-15): 긴 코트 자락이 척추 뼈(coat_*)에만 실려 넓적다리가 벌어지면 바지가 코트 앞·옆 트임을 뚫었다
        #   (idle.fbx를 옮겨 입혀 잰 관통: 98% 프레임, 최대 9cm). UpLeg 높이 아래 코트 정점의 코트 가중치 일부를 같은 쪽 UpLeg로 넘긴다 —
        #   엉덩이 높이 0 → 자락 끝 share, 가운데 ±center에서 좌우를 매끄럽게 나눠 앞섶이 안 찢기게. 코트 아닌 가중치는 그대로.
        hf = cfg["hem_follow"]
        share, center, ramp, shrink = hf.get("share", 0.6), hf.get("center", 0.05), hf.get("ramp", "smooth"), hf.get("thigh_shrink", 1.0)
        left, right = hf.get("left", "mixamorig:LeftUpLeg"), hf.get("right", "mixamorig:RightUpLeg")
        top = (new_arm.matrix_world @ new_arm.data.bones[left].head_local).z
        moved = 0
        for m in meshes:
            coat_ids = {g.index for g in m.vertex_groups if g.name.startswith(hf["prefix"])}
            if not coat_ids:
                continue
            Mw = m.matrix_world
            zs = [(Mw @ v.co).z for v in m.data.vertices if any(g.group in coat_ids and g.weight > 1e-4 for g in v.groups)]
            if not zs:                                                  # 그룹 이름만 있고 실린 정점이 없는 메시
                continue
            hem = min(zs)
            gl = m.vertex_groups.get(left) or m.vertex_groups.new(name=left)
            gr = m.vertex_groups.get(right) or m.vertex_groups.new(name=right)
            for v in m.data.vertices:
                cw = [(g.group, g.weight) for g in v.groups if g.group in coat_ids and g.weight > 1e-4]
                if not cw:
                    continue
                p = Mw @ v.co
                t = min(max((top - p.z) / max(top - hem, 1e-6), 0.0), 1.0)
                if ramp == "smooth":
                    t = t * t * (3.0 - 2.0 * t)
                k = share * t
                if k <= 1e-4:
                    continue
                amount = sum(w for _, w in cw) * k
                for gi, w in cw:
                    m.vertex_groups[gi].add([v.index], w * (1.0 - k), "REPLACE")
                wl = min(max(0.5 + p.x / (2.0 * center), 0.0), 1.0)      # 정면 −Y 규약: 몸 왼쪽 = +X
                for grp, part in ((gl, wl), (gr, 1.0 - wl)):
                    if part > 1e-4:
                        old = next((g.weight for g in v.groups if g.group == grp.index), 0.0)
                        grp.add([v.index], old + amount * part, "REPLACE")
                moved += 1
        shrunk = 0
        if shrink < 1.0:
            # 코트에 가려진 넓적다리(엉덩이 z ~ 자락 끝 위 fade)를 UpLeg→Leg 축 쪽으로 반지름 × thigh_shrink — 자락 끝 아래 보이는 바지는 그대로
            fade = hf.get("fade", 0.06)
            s_top = top + hf.get("shrink_top", 0.0)                     # 엉덩이 높이 위(허리까지) 바지도 넓적다리 따라 앞으로 나와 코트를 뚫었다(사보 프레임 49, z 0.88)
            min_leg = hf.get("shrink_min_leg", 0.5)                     # 넓적다리 가중치가 이보다 작으면 안 줄임, 사이는 몫에 비례
            W1 = new_arm.matrix_world
            axes = {}
            for side, up in ((1.0, left), (-1.0, right)):
                b = new_arm.data.bones[up]
                axes[side] = (W1 @ b.head_local, W1 @ b.children[0].head_local if b.children else W1 @ b.tail_local)
            for m in meshes:
                legs = {g.index for g in m.vertex_groups if g.name in (left, right) or g.name == left.replace("UpLeg", "Leg") or g.name == right.replace("UpLeg", "Leg")}
                if not legs or not any(g.name.startswith(hf["prefix"]) for g in m.vertex_groups):
                    continue
                Mw = m.matrix_world
                Mi = Mw.inverted()
                for v in m.data.vertices:
                    tot = sum(g.weight for g in v.groups)
                    lw = sum(g.weight for g in v.groups if g.group in legs)
                    if tot <= 0 or lw / tot < min_leg or any(m.vertex_groups[g.group].name.startswith(hf["prefix"]) and g.weight > 1e-4 for g in v.groups):
                        continue
                    p = Mw @ v.co
                    if p.z >= s_top or p.z <= hem:
                        continue
                    k = min((p.z - hem) / fade, 1.0) * min(lw / tot / 0.5, 1.0)
                    a0, a1 = axes[1.0 if p.x >= 0 else -1.0]
                    d = a1 - a0
                    u = min(max((p - a0).dot(d) / max(d.length_squared, 1e-9), 0.0), 1.0)
                    q = a0 + d * u
                    v.co = Mi @ (q + (p - q) * (1.0 - (1.0 - shrink) * k))
                    shrunk += 1
        report["코트 자락 → 넓적다리"] = f"{hf['prefix']}* 정점 {moved} · 끝 몫 {share} · {ramp} · 엉덩이 z {top:.3f}" + (f" · 가린 넓적다리 ×{shrink} 정점 {shrunk}" if shrunk else "")
    removed = [o.name for o in scene.objects if o.type != "MESH" and o != new_arm and o.name not in revived]
    for o in [o for o in scene.objects if o.type != "MESH" and o != new_arm]:
        bpy.data.objects.remove(o, do_unlink=True)
    report["지운 노드"] = len(removed)
    for act in list(bpy.data.actions):
        bpy.data.actions.remove(act)

    # ── 클립 다시 굽기
    if new_arm is not None and clips:
        rest = {b.name: b.matrix_local.copy() for b in new_arm.data.bones}
        parent = {b.name: (b.parent.name if b.parent else None) for b in new_arm.data.bones}
        order = []
        seen = set()

        def visit(b):
            if b.name in seen:
                return
            if b.parent:
                visit(b.parent)
            seen.add(b.name)
            order.append(b.name)

        for b in new_arm.data.bones:
            visit(b)
        new_arm.animation_data_create()
        for pb in new_arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
        for take, f0, frames in clips:
            act = bpy.data.actions.new(take)
            assert act.name == take, f"{name}: 액션 이름이 잘렸다/바뀌었다: {take!r} → {act.name!r}"
            act.use_fake_user = True                                    # --blend 진단 파일에 클립이 다 남게(사용자 0이면 저장 때 빠진다)
            new_arm.animation_data.action = act
            for fi, world in enumerate(frames):
                pose = {}
                for bname in order:
                    P = _normalized(G @ world[bname])
                    pose[bname] = P
                    p = parent[bname]
                    local = rest[p].inverted() @ rest[bname] if p else rest[bname]
                    basis = local.inverted() @ ((pose[p].inverted() @ P) if p else P)
                    bl, bq, _ = basis.decompose()
                    pb = new_arm.pose.bones[bname]
                    pb.location, pb.rotation_quaternion = bl, bq
                    pb.keyframe_insert("location", frame=f0 + fi)
                    pb.keyframe_insert("rotation_quaternion", frame=f0 + fi)
            if cfg.get("clip_ground"):
                # 뿌리만 세계 z로 옮기면 자식은 부모 기준 키라 통째로 따라온다. 뿌리 기본 행렬의 이동 = 쉬는 자세 회전⁻¹ × 세계 이동
                roots = [b.name for b in new_arm.data.bones if b.parent is None]
                lifts = []
                for fi in range(len(frames)):
                    scene.frame_set(f0 + fi)
                    dg = bpy.context.evaluated_depsgraph_get()
                    low = float("inf")
                    for m in meshes:
                        ev = m.evaluated_get(dg)
                        me = ev.to_mesh()
                        low = min(low, min((ev.matrix_world @ v.co).z for v in me.vertices))
                        ev.to_mesh_clear()
                    for r in roots:
                        pb = new_arm.pose.bones[r]
                        pb.location = pb.location + rest[r].to_quaternion().inverted() @ Vector((0.0, 0.0, -low))
                        pb.keyframe_insert("location", frame=f0 + fi)
                    lifts.append(-low)
                report.setdefault("클립 접지(m)", {})[take[-12:]] = (round(min(lifts), 3), round(max(lifts), 3))
        report["클립"] = [c[0] for c in clips]

    # ── 검사·내보내기
    if new_arm is not None and clips and cfg.get("clip_scene_basis"):
        # FBX 모델 Lcl(기본 자세)은 내보낼 때 현재 프레임 자세로 적힌다 — 첫 프레임(= 새 쉬는 자세)으로 두어 Lcl 전역 = Cluster TransformLink
        scene.frame_set(clips[0][1])
    bpy.context.view_layer.update()
    pts = [m.matrix_world @ v.co for m in meshes for v in m.data.vertices]
    lo2 = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi2 = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    report["크기(m)"] = tuple(round(c, 3) for c in (hi2 - lo2))
    report["최저 z"] = round(lo2.z, 4)
    mats_after = sorted({sl.material.name for o in meshes for sl in o.material_slots if sl.material})
    assert mats_after == mats_before, f"{name} 재질 이름이 바뀌었다: {set(mats_before) ^ set(mats_after)}"
    dst = os.path.join(out_dir, os.path.basename(dst_path)) if out_dir else dst_path
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if arc_textures:
        tex_out = os.path.join(os.path.dirname(dst), "Textures")
        os.makedirs(tex_out, exist_ok=True)
        for t in arc_textures:
            out_t = os.path.join(tex_out, os.path.basename(t))
            if os.path.basename(t) in set(cfg.get("rgb_textures", ())):         # 알파(명암 마스크) 뺀 RGB로
                write_rgb_png(t, out_t)
                report.setdefault("알파 뺀 텍스처", []).append(os.path.basename(t))
            elif os.path.abspath(t) != os.path.abspath(out_t):
                shutil.copy2(t, out_t)
        report["텍스처 복사"] = [os.path.basename(t) for t in arc_textures]
    if save_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.splitext(dst)[0] + "_진단.blend", copy=True)
    if dst.lower().endswith(".glb"):
        bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", export_yup=True, use_selection=False, export_animations=False)
    else:
        import io_scene_fbx.export_fbx_bin as fbx_bin
        name_of = fbx_bin.get_blenderID_name

        def take_name(bid):                                             # (오브젝트, 액션) → 액션 이름만(원본 테이크 이름 유지)
            if isinstance(bid, tuple) and len(bid) == 2 and isinstance(bid[1], bpy.types.Action):
                return bid[1].name
            return name_of(bid)

        fbx_bin.get_blenderID_name = take_name
        try:
            bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                                     apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                                     primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                                     mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False,
                                     bake_anim=bool(clips), bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                                     bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
        finally:
            fbx_bin.get_blenderID_name = name_of
    report["메시"] = len(meshes)
    report["출력"] = dst
    return report


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir, names, save_blend = None, [], False
    it = iter(args)
    for a in it:
        if a == "--out":
            out_dir = next(it)
        elif a == "--blend":
            save_blend = True
        else:
            names.append(a)
    for name in names or list(UNITS):
        r = fix(name, UNITS[name], out_dir, save_blend)
        print("정리  " + "  ".join(f"{k} {v}" for k, v in r.items() if k != "출력"))


if __name__ == "__main__":
    main()
