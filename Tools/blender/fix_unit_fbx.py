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
# 2026-09-16 사장님이 원본을 바탕화면 모음집으로 옮기고 유닛 이름으로 바꿨다(등급 폴더 안, README.md 있음).
# DL은 이미 지워진 옛 항목(나루토·재규어 등, rev=로 되살림)만 남아 있는 자리다.
SKINS = os.path.expanduser("~/Desktop/구랜디스킨모음")
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
_MH = "~/Desktop/구랜디스킨모음/03_특별함/특별함_박기찬/textures/mpr_bound_character_mplc014mihawk_"
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
# 3ds Max Biped 사람형 뼈 → mixamorig(2026-09-16 토지·하나타로): prefix "Bip001"/"Bip01". 손가락 Finger0=엄지·1=검지·2=중지·3=약지·4=새끼, 마디 ""·"1"·"2".
#   이름을 바꾸면 idle_stretch 판정(mixamorig 사슬)이 실제로 움직이고, 유니티 자동 매핑의 모호함도 없다. tpose_arms에는 biped_tpose_names(prefix)를 같이 준다.
_BIPED_FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")


def biped_rename(prefix, fingers=5, spine2=None, joints=3):
    t = {f"{prefix} Pelvis": "mixamorig:Hips", f"{prefix} Spine": "mixamorig:Spine", f"{prefix} Spine1": "mixamorig:Spine1",
         f"{prefix} Neck": "mixamorig:Neck", f"{prefix} Head": "mixamorig:Head"}
    if spine2:
        t[f"{prefix} {spine2}"] = "mixamorig:Spine2"
    for s, side in (("L", "Left"), ("R", "Right")):
        t.update({f"{prefix} {s} Clavicle": f"mixamorig:{side}Shoulder", f"{prefix} {s} UpperArm": f"mixamorig:{side}Arm",
                  f"{prefix} {s} Forearm": f"mixamorig:{side}ForeArm", f"{prefix} {s} Hand": f"mixamorig:{side}Hand",
                  f"{prefix} {s} Thigh": f"mixamorig:{side}UpLeg", f"{prefix} {s} Calf": f"mixamorig:{side}Leg",
                  f"{prefix} {s} Foot": f"mixamorig:{side}Foot", f"{prefix} {s} Toe0": f"mixamorig:{side}ToeBase"})
        for i, finger in enumerate(_BIPED_FINGERS[:fingers]):
            for j, k in (("", 1), ("1", 2), ("2", 3))[:joints]:
                t[f"{prefix} {s} Finger{i}{j}"] = f"mixamorig:{side}Hand{finger}{k}"
    return t


def biped_tpose_names(fingers=5, joints=3):
    return {s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                     "Hand": f"mixamorig:{side}Hand"},
                    **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(_BIPED_FINGERS[:fingers])
                       for j, k in (("", 1), ("1", 2), ("2", 3))[:joints]})
            for s, side in (("L", "Left"), ("R", "Right"))}


# 피즈(LoL 리그, 2026-09-16): glTF 번호 꼬리는 rename_strip으로 떼고 짝짓는다. Root(Spine·Hip의 부모)=Hips · Chest=Spine1 · knee=Leg · foot=Foot · toe=ToeBase.
FIZZ_RENAME = {"Root": "mixamorig:Hips", "Spine": "mixamorig:Spine", "Chest": "mixamorig:Spine1", "Head": "mixamorig:Head", "L_Shoulder": "mixamorig:LeftShoulder", "L_UpArm": "mixamorig:LeftArm", "L_ForeArm": "mixamorig:LeftForeArm", "L_hand": "mixamorig:LeftHand", "L_thigh": "mixamorig:LeftUpLeg", "L_knee": "mixamorig:LeftLeg", "L_foot": "mixamorig:LeftFoot", "L_toe": "mixamorig:LeftToeBase", "L_Hand_Finger_Index_a": "mixamorig:LeftHandIndex1", "L_Hand_Finger_Index_b": "mixamorig:LeftHandIndex2", "L_Hand_Finger_Middle_a": "mixamorig:LeftHandMiddle1", "L_Hand_Finger_Middle_b": "mixamorig:LeftHandMiddle2", "L_Hand_Finger_Thumb_a": "mixamorig:LeftHandThumb1", "L_Hand_Finger_Thumb_b": "mixamorig:LeftHandThumb2", "R_Shoulder": "mixamorig:RightShoulder", "R_UpArm": "mixamorig:RightArm", "R_ForeArm": "mixamorig:RightForeArm", "R_hand": "mixamorig:RightHand", "R_thigh": "mixamorig:RightUpLeg", "R_knee": "mixamorig:RightLeg", "R_foot": "mixamorig:RightFoot", "R_toe": "mixamorig:RightToeBase", "R_Hand_Finger_Index_a": "mixamorig:RightHandIndex1", "R_Hand_Finger_Index_b": "mixamorig:RightHandIndex2", "R_Hand_Finger_Middle_a": "mixamorig:RightHandMiddle1", "R_Hand_Finger_Middle_b": "mixamorig:RightHandMiddle2", "R_Hand_Finger_Thumb_a": "mixamorig:RightHandThumb1", "R_Hand_Finger_Thumb_b": "mixamorig:RightHandThumb2"}
# 사스케(나루토 게임 추출 FBX, 2026-09-16 희귀함_이은엽): 영어 띄어쓰기 이름. head neck lower=Neck · upper=Head · shoulder 1=Clavicle · 2=UpperArm · finger 1~5 = 엄지~새끼.
SASUKE_RENAME = {"pelvis": "mixamorig:Hips", "spine lower": "mixamorig:Spine", "spine upper": "mixamorig:Spine1", "head neck lower": "mixamorig:Neck", "head neck upper": "mixamorig:Head", "arm left shoulder 1": "mixamorig:LeftShoulder", "arm left shoulder 2": "mixamorig:LeftArm", "arm left elbow": "mixamorig:LeftForeArm", "arm left wrist": "mixamorig:LeftHand", "leg left thigh": "mixamorig:LeftUpLeg", "leg left knee": "mixamorig:LeftLeg", "leg left ankle": "mixamorig:LeftFoot", "leg left toes": "mixamorig:LeftToeBase", "arm left finger 1a": "mixamorig:LeftHandThumb1", "arm left finger 1b": "mixamorig:LeftHandThumb2", "arm left finger 1c": "mixamorig:LeftHandThumb3", "arm left finger 2a": "mixamorig:LeftHandIndex1", "arm left finger 2b": "mixamorig:LeftHandIndex2", "arm left finger 2c": "mixamorig:LeftHandIndex3", "arm left finger 3a": "mixamorig:LeftHandMiddle1", "arm left finger 3b": "mixamorig:LeftHandMiddle2", "arm left finger 3c": "mixamorig:LeftHandMiddle3", "arm left finger 4a": "mixamorig:LeftHandRing1", "arm left finger 4b": "mixamorig:LeftHandRing2", "arm left finger 4c": "mixamorig:LeftHandRing3", "arm left finger 5a": "mixamorig:LeftHandPinky1", "arm left finger 5b": "mixamorig:LeftHandPinky2", "arm left finger 5c": "mixamorig:LeftHandPinky3", "arm right shoulder 1": "mixamorig:RightShoulder", "arm right shoulder 2": "mixamorig:RightArm", "arm right elbow": "mixamorig:RightForeArm", "arm right wrist": "mixamorig:RightHand", "leg right thigh": "mixamorig:RightUpLeg", "leg right knee": "mixamorig:RightLeg", "leg right ankle": "mixamorig:RightFoot", "leg right toes": "mixamorig:RightToeBase", "arm right finger 1a": "mixamorig:RightHandThumb1", "arm right finger 1b": "mixamorig:RightHandThumb2", "arm right finger 1c": "mixamorig:RightHandThumb3", "arm right finger 2a": "mixamorig:RightHandIndex1", "arm right finger 2b": "mixamorig:RightHandIndex2", "arm right finger 2c": "mixamorig:RightHandIndex3", "arm right finger 3a": "mixamorig:RightHandMiddle1", "arm right finger 3b": "mixamorig:RightHandMiddle2", "arm right finger 3c": "mixamorig:RightHandMiddle3", "arm right finger 4a": "mixamorig:RightHandRing1", "arm right finger 4b": "mixamorig:RightHandRing2", "arm right finger 4c": "mixamorig:RightHandRing3", "arm right finger 5a": "mixamorig:RightHandPinky1", "arm right finger 5b": "mixamorig:RightHandPinky2", "arm right finger 5c": "mixamorig:RightHandPinky3"}
# 오카베(Rigify DEF 뼈, 2026-09-16 희귀함_김정래): spine.004=목(005·006은 합침) · head · upper_arm/forearm/thigh/shin의 .001 분할 뼈는 합침 · palm은 Hand로.
OKABE_RENAME = {"DEF-spine": "mixamorig:Hips", "DEF-spine.001": "mixamorig:Spine", "DEF-spine.002": "mixamorig:Spine1", "DEF-spine.003": "mixamorig:Spine2", "DEF-spine.004": "mixamorig:Neck", "DEF-head": "mixamorig:Head", "DEF-shoulder.L": "mixamorig:LeftShoulder", "DEF-upper_arm.L": "mixamorig:LeftArm", "DEF-forearm.L": "mixamorig:LeftForeArm", "DEF-hand.L": "mixamorig:LeftHand", "DEF-thigh.L": "mixamorig:LeftUpLeg", "DEF-shin.L": "mixamorig:LeftLeg", "DEF-foot.L": "mixamorig:LeftFoot", "DEF-toe.L": "mixamorig:LeftToeBase", "DEF-thumb.01.L": "mixamorig:LeftHandThumb1", "DEF-thumb.02.L": "mixamorig:LeftHandThumb2", "DEF-thumb.03.L": "mixamorig:LeftHandThumb3", "DEF-f_index.01.L": "mixamorig:LeftHandIndex1", "DEF-f_index.02.L": "mixamorig:LeftHandIndex2", "DEF-f_index.03.L": "mixamorig:LeftHandIndex3", "DEF-f_middle.01.L": "mixamorig:LeftHandMiddle1", "DEF-f_middle.02.L": "mixamorig:LeftHandMiddle2", "DEF-f_middle.03.L": "mixamorig:LeftHandMiddle3", "DEF-f_ring.01.L": "mixamorig:LeftHandRing1", "DEF-f_ring.02.L": "mixamorig:LeftHandRing2", "DEF-f_ring.03.L": "mixamorig:LeftHandRing3", "DEF-f_pinky.01.L": "mixamorig:LeftHandPinky1", "DEF-f_pinky.02.L": "mixamorig:LeftHandPinky2", "DEF-f_pinky.03.L": "mixamorig:LeftHandPinky3", "DEF-shoulder.R": "mixamorig:RightShoulder", "DEF-upper_arm.R": "mixamorig:RightArm", "DEF-forearm.R": "mixamorig:RightForeArm", "DEF-hand.R": "mixamorig:RightHand", "DEF-thigh.R": "mixamorig:RightUpLeg", "DEF-shin.R": "mixamorig:RightLeg", "DEF-foot.R": "mixamorig:RightFoot", "DEF-toe.R": "mixamorig:RightToeBase", "DEF-thumb.01.R": "mixamorig:RightHandThumb1", "DEF-thumb.02.R": "mixamorig:RightHandThumb2", "DEF-thumb.03.R": "mixamorig:RightHandThumb3", "DEF-f_index.01.R": "mixamorig:RightHandIndex1", "DEF-f_index.02.R": "mixamorig:RightHandIndex2", "DEF-f_index.03.R": "mixamorig:RightHandIndex3", "DEF-f_middle.01.R": "mixamorig:RightHandMiddle1", "DEF-f_middle.02.R": "mixamorig:RightHandMiddle2", "DEF-f_middle.03.R": "mixamorig:RightHandMiddle3", "DEF-f_ring.01.R": "mixamorig:RightHandRing1", "DEF-f_ring.02.R": "mixamorig:RightHandRing2", "DEF-f_ring.03.R": "mixamorig:RightHandRing3", "DEF-f_pinky.01.R": "mixamorig:RightHandPinky1", "DEF-f_pinky.02.R": "mixamorig:RightHandPinky2", "DEF-f_pinky.03.R": "mixamorig:RightHandPinky3"}
# 에렌(진격의 거인 4기 SFM 계열 glb, 2026-09-16 희귀함_김기연): 번호 꼬리는 rename_strip. Chest=Spine1 · elbow=ForeArm · wrist=Hand · Little=Pinky · 왼다리는 허벅지뿐(절단).
EREN_RENAME = {"Hips": "mixamorig:Hips", "Spine": "mixamorig:Spine", "Chest": "mixamorig:Spine1", "Neck": "mixamorig:Neck", "Head": "mixamorig:Head", "Right shoulder": "mixamorig:RightShoulder", "Right arm": "mixamorig:RightArm", "Right elbow": "mixamorig:RightForeArm", "Right wrist": "mixamorig:RightHand", "Right leg": "mixamorig:RightUpLeg", "Thumb0_R": "mixamorig:RightHandThumb1", "Thumb1_R": "mixamorig:RightHandThumb2", "Thumb2_R": "mixamorig:RightHandThumb3", "IndexFinger1_R": "mixamorig:RightHandIndex1", "IndexFinger2_R": "mixamorig:RightHandIndex2", "IndexFinger3_R": "mixamorig:RightHandIndex3", "MiddleFinger1_R": "mixamorig:RightHandMiddle1", "MiddleFinger2_R": "mixamorig:RightHandMiddle2", "MiddleFinger3_R": "mixamorig:RightHandMiddle3", "RingFinger1_R": "mixamorig:RightHandRing1", "RingFinger2_R": "mixamorig:RightHandRing2", "RingFinger3_R": "mixamorig:RightHandRing3", "LittleFinger1_R": "mixamorig:RightHandPinky1", "LittleFinger2_R": "mixamorig:RightHandPinky2", "LittleFinger3_R": "mixamorig:RightHandPinky3", "Left shoulder": "mixamorig:LeftShoulder", "Left arm": "mixamorig:LeftArm", "Left elbow": "mixamorig:LeftForeArm", "Left wrist": "mixamorig:LeftHand", "Left leg": "mixamorig:LeftUpLeg", "Thumb0_L": "mixamorig:LeftHandThumb1", "Thumb1_L": "mixamorig:LeftHandThumb2", "Thumb2_L": "mixamorig:LeftHandThumb3", "IndexFinger1_L": "mixamorig:LeftHandIndex1", "IndexFinger2_L": "mixamorig:LeftHandIndex2", "IndexFinger3_L": "mixamorig:LeftHandIndex3", "MiddleFinger1_L": "mixamorig:LeftHandMiddle1", "MiddleFinger2_L": "mixamorig:LeftHandMiddle2", "MiddleFinger3_L": "mixamorig:LeftHandMiddle3", "RingFinger1_L": "mixamorig:LeftHandRing1", "RingFinger2_L": "mixamorig:LeftHandRing2", "RingFinger3_L": "mixamorig:LeftHandRing3", "LittleFinger1_L": "mixamorig:LeftHandPinky1", "LittleFinger2_L": "mixamorig:LeftHandPinky2", "LittleFinger3_L": "mixamorig:LeftHandPinky3", "Right knee": "mixamorig:RightLeg", "Right ankle": "mixamorig:RightFoot"}
# 죠타로(죠죠 3부 SFM ValveBiped 리그 glb, 2026-09-16 희귀함_배병규): 번호 꼬리는 rename_strip. Spine1=Spine · Spine2=Spine1(가슴) · Finger0~4 = 엄지~새끼.
VALVE_RENAME = {"ValveBiped.Bip01_Pelvis": "mixamorig:Hips", "ValveBiped.Bip01_Spine1": "mixamorig:Spine", "ValveBiped.Bip01_Spine2": "mixamorig:Spine1", "ValveBiped.Bip01_Neck": "mixamorig:Neck", "ValveBiped.Bip01_Head": "mixamorig:Head", "ValveBiped.Bip01_L_Clavicle": "mixamorig:LeftShoulder", "ValveBiped.Bip01_L_UpperArm": "mixamorig:LeftArm", "ValveBiped.Bip01_L_Forearm": "mixamorig:LeftForeArm", "ValveBiped.Bip01_L_Hand": "mixamorig:LeftHand", "ValveBiped.Bip01_L_Thigh": "mixamorig:LeftUpLeg", "ValveBiped.Bip01_L_Calf": "mixamorig:LeftLeg", "ValveBiped.Bip01_L_Foot": "mixamorig:LeftFoot", "ValveBiped.Bip01_L_Toe": "mixamorig:LeftToeBase", "ValveBiped.Bip01_L_Finger0": "mixamorig:LeftHandThumb1", "ValveBiped.Bip01_L_Finger01": "mixamorig:LeftHandThumb2", "ValveBiped.Bip01_L_Finger02": "mixamorig:LeftHandThumb3", "ValveBiped.Bip01_L_Finger1": "mixamorig:LeftHandIndex1", "ValveBiped.Bip01_L_Finger11": "mixamorig:LeftHandIndex2", "ValveBiped.Bip01_L_Finger12": "mixamorig:LeftHandIndex3", "ValveBiped.Bip01_L_Finger2": "mixamorig:LeftHandMiddle1", "ValveBiped.Bip01_L_Finger21": "mixamorig:LeftHandMiddle2", "ValveBiped.Bip01_L_Finger22": "mixamorig:LeftHandMiddle3", "ValveBiped.Bip01_L_Finger3": "mixamorig:LeftHandRing1", "ValveBiped.Bip01_L_Finger31": "mixamorig:LeftHandRing2", "ValveBiped.Bip01_L_Finger32": "mixamorig:LeftHandRing3", "ValveBiped.Bip01_L_Finger4": "mixamorig:LeftHandPinky1", "ValveBiped.Bip01_L_Finger41": "mixamorig:LeftHandPinky2", "ValveBiped.Bip01_L_Finger42": "mixamorig:LeftHandPinky3", "ValveBiped.Bip01_R_Clavicle": "mixamorig:RightShoulder", "ValveBiped.Bip01_R_UpperArm": "mixamorig:RightArm", "ValveBiped.Bip01_R_Forearm": "mixamorig:RightForeArm", "ValveBiped.Bip01_R_Hand": "mixamorig:RightHand", "ValveBiped.Bip01_R_Thigh": "mixamorig:RightUpLeg", "ValveBiped.Bip01_R_Calf": "mixamorig:RightLeg", "ValveBiped.Bip01_R_Foot": "mixamorig:RightFoot", "ValveBiped.Bip01_R_Toe": "mixamorig:RightToeBase", "ValveBiped.Bip01_R_Finger0": "mixamorig:RightHandThumb1", "ValveBiped.Bip01_R_Finger01": "mixamorig:RightHandThumb2", "ValveBiped.Bip01_R_Finger02": "mixamorig:RightHandThumb3", "ValveBiped.Bip01_R_Finger1": "mixamorig:RightHandIndex1", "ValveBiped.Bip01_R_Finger11": "mixamorig:RightHandIndex2", "ValveBiped.Bip01_R_Finger12": "mixamorig:RightHandIndex3", "ValveBiped.Bip01_R_Finger2": "mixamorig:RightHandMiddle1", "ValveBiped.Bip01_R_Finger21": "mixamorig:RightHandMiddle2", "ValveBiped.Bip01_R_Finger22": "mixamorig:RightHandMiddle3", "ValveBiped.Bip01_R_Finger3": "mixamorig:RightHandRing1", "ValveBiped.Bip01_R_Finger31": "mixamorig:RightHandRing2", "ValveBiped.Bip01_R_Finger32": "mixamorig:RightHandRing3", "ValveBiped.Bip01_R_Finger4": "mixamorig:RightHandPinky1", "ValveBiped.Bip01_R_Finger41": "mixamorig:RightHandPinky2", "ValveBiped.Bip01_R_Finger42": "mixamorig:RightHandPinky3"}
# 히비키(Source Filmmaker ValveBiped glb, 2026-09-16 희귀함_박수찬): Spine1·Spine4·Toe0·손가락 끝(가중치 0, 원점 쓰레기 자리)은 뺀다 → Spine2=Spine1 · Neck1 · Head1.
HIBIKI_RENAME = {"ValveBiped.Bip01_Pelvis": "mixamorig:Hips", "ValveBiped.Bip01_Spine": "mixamorig:Spine", "ValveBiped.Bip01_Spine2": "mixamorig:Spine1", "ValveBiped.Bip01_Neck1": "mixamorig:Neck", "ValveBiped.Bip01_Head1": "mixamorig:Head", "ValveBiped.Bip01_L_Clavicle": "mixamorig:LeftShoulder", "ValveBiped.Bip01_L_UpperArm": "mixamorig:LeftArm", "ValveBiped.Bip01_L_Forearm": "mixamorig:LeftForeArm", "ValveBiped.Bip01_L_Hand": "mixamorig:LeftHand", "ValveBiped.Bip01_L_Thigh": "mixamorig:LeftUpLeg", "ValveBiped.Bip01_L_Calf": "mixamorig:LeftLeg", "ValveBiped.Bip01_L_Foot": "mixamorig:LeftFoot", "ValveBiped.Bip01_L_Finger0": "mixamorig:LeftHandThumb1", "ValveBiped.Bip01_L_Finger01": "mixamorig:LeftHandThumb2", "ValveBiped.Bip01_L_Finger1": "mixamorig:LeftHandIndex1", "ValveBiped.Bip01_L_Finger11": "mixamorig:LeftHandIndex2", "ValveBiped.Bip01_L_Finger2": "mixamorig:LeftHandMiddle1", "ValveBiped.Bip01_L_Finger21": "mixamorig:LeftHandMiddle2", "ValveBiped.Bip01_L_Finger3": "mixamorig:LeftHandRing1", "ValveBiped.Bip01_L_Finger31": "mixamorig:LeftHandRing2", "ValveBiped.Bip01_L_Finger4": "mixamorig:LeftHandPinky1", "ValveBiped.Bip01_L_Finger41": "mixamorig:LeftHandPinky2", "ValveBiped.Bip01_R_Clavicle": "mixamorig:RightShoulder", "ValveBiped.Bip01_R_UpperArm": "mixamorig:RightArm", "ValveBiped.Bip01_R_Forearm": "mixamorig:RightForeArm", "ValveBiped.Bip01_R_Hand": "mixamorig:RightHand", "ValveBiped.Bip01_R_Thigh": "mixamorig:RightUpLeg", "ValveBiped.Bip01_R_Calf": "mixamorig:RightLeg", "ValveBiped.Bip01_R_Foot": "mixamorig:RightFoot", "ValveBiped.Bip01_R_Finger0": "mixamorig:RightHandThumb1", "ValveBiped.Bip01_R_Finger01": "mixamorig:RightHandThumb2", "ValveBiped.Bip01_R_Finger1": "mixamorig:RightHandIndex1", "ValveBiped.Bip01_R_Finger11": "mixamorig:RightHandIndex2", "ValveBiped.Bip01_R_Finger2": "mixamorig:RightHandMiddle1", "ValveBiped.Bip01_R_Finger21": "mixamorig:RightHandMiddle2", "ValveBiped.Bip01_R_Finger3": "mixamorig:RightHandRing1", "ValveBiped.Bip01_R_Finger31": "mixamorig:RightHandRing2", "ValveBiped.Bip01_R_Finger4": "mixamorig:RightHandPinky1", "ValveBiped.Bip01_R_Finger41": "mixamorig:RightHandPinky2"}
# 심해왕(원펀맨 게임 추출 glb, 2026-09-16 희귀함_이태훈): 가로우와 같은 게임 립인데 이름 앞에 bone0000_ 번호가 붙는다. CLANK=무릎 · TOE1=발목 · TOE2=발끝(세계 위치로 확인). +X = 왼쪽.
SEAKING_RENAME = {
    'bone0002_WAIST_04': 'mixamorig:Hips',
    'bone0003_SPINE1_05': 'mixamorig:Spine',
    'bone0004_SPINE2_06': 'mixamorig:Spine1',
    'bone0005_SPINE3_07': 'mixamorig:Spine2',
    'bone0006_NECK_08': 'mixamorig:Neck',
    'bone0007_HEAD_09': 'mixamorig:Head',
    'bone0100_CLAVICLE_L_0100': 'mixamorig:LeftShoulder',
    'bone0101_SHOULDER_L_0101': 'mixamorig:LeftArm',
    'bone0103_ELBOW_L_0103': 'mixamorig:LeftForeArm',
    'bone0104_WRIST_L_0104': 'mixamorig:LeftHand',
    'bone0158_THIGH_L_0159': 'mixamorig:LeftUpLeg',
    'bone0159_CLANK_L_0160': 'mixamorig:LeftLeg',
    'bone0160_TOE1_L_0161': 'mixamorig:LeftFoot',
    'bone0161_TOE2_L_0142': 'mixamorig:LeftToeBase',
    'bone0118_F_THUMB1_L_0118': 'mixamorig:LeftHandThumb1',
    'bone0119_F_THUMB2_L_0119': 'mixamorig:LeftHandThumb2',
    'bone0120_F_THUMB3_L_0120': 'mixamorig:LeftHandThumb3',
    'bone0106_F_FORE1_L_0106': 'mixamorig:LeftHandIndex1',
    'bone0107_F_FORE2_L_0107': 'mixamorig:LeftHandIndex2',
    'bone0108_F_FORE3_L_0108': 'mixamorig:LeftHandIndex3',
    'bone0109_F_MIDDLE1_L_0109': 'mixamorig:LeftHandMiddle1',
    'bone0110_F_MIDDLE2_L_0110': 'mixamorig:LeftHandMiddle2',
    'bone0111_F_MIDDLE3_L_0111': 'mixamorig:LeftHandMiddle3',
    'bone0112_F_MEDICINAL1_L_0112': 'mixamorig:LeftHandRing1',
    'bone0113_F_MEDICINAL2_L_0113': 'mixamorig:LeftHandRing2',
    'bone0114_F_MEDICINAL3_L_0114': 'mixamorig:LeftHandRing3',
    'bone0115_F_LITTLE1_L_0115': 'mixamorig:LeftHandPinky1',
    'bone0116_F_LITTLE2_L_0116': 'mixamorig:LeftHandPinky2',
    'bone0117_F_LITTLE3_L_0117': 'mixamorig:LeftHandPinky3',
    'bone0076_CLAVICLE_R_076': 'mixamorig:RightShoulder',
    'bone0077_SHOULDER_R_077': 'mixamorig:RightArm',
    'bone0079_ELBOW_R_079': 'mixamorig:RightForeArm',
    'bone0080_WRIST_R_080': 'mixamorig:RightHand',
    'bone0152_THIGH_R_0153': 'mixamorig:RightUpLeg',
    'bone0153_CLANK_R_0154': 'mixamorig:RightLeg',
    'bone0154_TOE1_R_0155': 'mixamorig:RightFoot',
    'bone0155_TOE2_R_0156': 'mixamorig:RightToeBase',
    'bone0094_F_THUMB1_R_094': 'mixamorig:RightHandThumb1',
    'bone0095_F_THUMB2_R_095': 'mixamorig:RightHandThumb2',
    'bone0096_F_THUMB3_R_096': 'mixamorig:RightHandThumb3',
    'bone0082_F_FORE1_R_082': 'mixamorig:RightHandIndex1',
    'bone0083_F_FORE2_R_083': 'mixamorig:RightHandIndex2',
    'bone0084_F_FORE3_R_084': 'mixamorig:RightHandIndex3',
    'bone0085_F_MIDDLE1_R_085': 'mixamorig:RightHandMiddle1',
    'bone0086_F_MIDDLE2_R_086': 'mixamorig:RightHandMiddle2',
    'bone0087_F_MIDDLE3_R_087': 'mixamorig:RightHandMiddle3',
    'bone0088_F_MEDICINAL1_R_088': 'mixamorig:RightHandRing1',
    'bone0089_F_MEDICINAL2_R_089': 'mixamorig:RightHandRing2',
    'bone0090_F_MEDICINAL3_R_090': 'mixamorig:RightHandRing3',
    'bone0091_F_LITTLE1_R_091': 'mixamorig:RightHandPinky1',
    'bone0092_F_LITTLE2_R_092': 'mixamorig:RightHandPinky2',
    'bone0093_F_LITTLE3_R_093': 'mixamorig:RightHandPinky3',
}
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
_RIN_TEX = "~/Desktop/구랜디스킨모음/03_특별함/특별함_박진웅/textures/Male_{}_Cos_FB2_D.png"
# 개구리 코스튬(프리파이어 INGAME_ANIMATION_SUPEREMOTE_MALE_FROG, 2026-09-22): 린과 같은 bone_
# 계열이지만 Spine1만 이름이 다르다 — bone_Spine1은 없고(rename_bones는 없는 키를 못 참아
# 통째로 빼야 한다) "Bip01 Spine1"이 유일한 실가중치 Spine1(518정점, 직접 확인)이라 그
# 한 뼈만 얹는다.
FROG_RENAME = {k: v for k, v in RIN_RENAME.items() if k != "bone_Spine1"} | {"Bip01 Spine1": "mixamorig:Spine1"}
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
# 블리치 이치고(Sketchfab glb, 조인트 296) → mixamorig. R이 −X · L이 +X(위치로 확인: shoulder R_161 머리 x −0.188).
#   손가락 A~E는 위치로 확인 — A는 손에 바로 붙고 제일 앞(y −0.013) = 엄지, 나머지는 y가 커지는 순서로 검지(B −0.018)·중지(C 0.004)·약지(D 0.027)·새끼(E 0.047).
#   thorax_210(uback과 neck 사이)·ido_294(hips 위 뿌리)·AS/sway 보조 뼈는 이름 그대로 둔다(유니티는 매핑 안 된 중간 뼈를 그냥 지나간다).
ICHIGO_RENAME = {"hips_293": "mixamorig:Hips", "lback_221": "mixamorig:Spine", "mback_220": "mixamorig:Spine1",
                 "uback_215": "mixamorig:Spine2", "neck_108": "mixamorig:Neck", "head_107": "mixamorig:Head"}
for _side, _sfx, _n in (("L", "Left", (198, 197, 191, 180, 279, 268, 252, 251)), ("R", "Right", (162, 161, 155, 144, 250, 239, 223, 222))):
    _c, _s, _e, _h, _g, _k, _a, _t = _n
    ICHIGO_RENAME.update({f"clavicle {_side}_{_c}": f"mixamorig:{_sfx}Shoulder", f"shoulder {_side}_{_s}": f"mixamorig:{_sfx}Arm",
                          f"elbow {_side}_{_e}": f"mixamorig:{_sfx}ForeArm", f"hand {_side}_{_h}": f"mixamorig:{_sfx}Hand",
                          f"groin {_side}_{_g}": f"mixamorig:{_sfx}UpLeg", f"knee {_side}_{_k}": f"mixamorig:{_sfx}Leg",
                          f"ankle {_side}_{_a}": f"mixamorig:{_sfx}Foot", f"toe {_side}_{_t}": f"mixamorig:{_sfx}ToeBase"})
for _side, _sfx, _ids in (("L", "Left", ((165, 164, 163), (168, 167, 166), (172, 171, 170), (175, 174, 173), (178, 177, 176))),
                          ("R", "Right", ((111, 110, 109), (114, 113, 112), (118, 117, 116), (121, 120, 119), (124, 123, 122)))):
    for _letter, _finger, _three in zip("ABCDE", ("Thumb", "Index", "Middle", "Ring", "Pinky"), _ids):
        for _j, (_num, _id) in enumerate(zip((1, 2, 3), _three)):
            ICHIGO_RENAME[f"finger {_side} {_letter}0{_j}_{_id}"] = f"mixamorig:{_sfx}Hand{_finger}{_num}"

# 바키(Sketchfab glb, 2026-09-16 희귀함 6호) 뼈 이름 → mixamorig. 체코/러시아식이라 해독이 필요하다:
#   taz=골반 · telo1/2/3=척추 · sheya=목 · golova=머리 · plech/plec=쇄골 · ruka/ruk=위팔 · predple/predlech=아래팔 · kist=손 · nog/nog2/nog3=넓적다리/종아리/발
#   🔴 **L=왼쪽, P=오른쪽**(체코 pravý) — 실측으로도 P가 +x다. 발끝 뼈는 없다(nog3 다음은 가중치 0인 _end).
#   손가락: uk=검지 · bez=중지 · miz=새끼 · bol/bil=엄지 · fack=약지(마디 1~3).
BAKI_RENAME = {"taz_04": "mixamorig:Hips", "telo1_05": "mixamorig:Spine", "telo2_06": "mixamorig:Spine1", "telo3_07": "mixamorig:Spine2",
               "sheya_046": "mixamorig:Neck", "golova_047": "mixamorig:Head"}
for _pre, _side, _ids in (("L", "Left", ("plech_08", "ruka_09", "predple_010", "kist_011", "nog_054", "nog2_055", "nog3_056")),
                          ("P", "Right", ("plech_027", "ruk_028", "predlech_029", "kist_030", "nog_051", "nog2_052", "nog 3_053"))):
    _cl, _up, _fo, _ha, _th, _ca, _ft = _ids
    BAKI_RENAME.update({f"{_pre} {_cl}": f"mixamorig:{_side}Shoulder", f"{_pre} {_up}": f"mixamorig:{_side}Arm",
                        f"{_pre} {_fo}": f"mixamorig:{_side}ForeArm", f"{_pre} {_ha}": f"mixamorig:{_side}Hand",
                        f"{_pre} {_th}": f"mixamorig:{_side}UpLeg", f"{_pre} {_ca}": f"mixamorig:{_side}Leg",
                        f"{_pre} {_ft}": f"mixamorig:{_side}Foot"})
for _pre, _side, _f in (("L", "Left", (("bol_021", "bol2_022", "bol 3_023"), ("uk1_012", "uk2_013", "uk3_014"), ("bez_015", "bez2_016", "bez3_017"),
                                       ("fack_024", "fack 2_025", "fack 3_026"), ("miz_018", "miz2_019", "miz 3_020"))),
                        ("P", "Right", (("bil_031", "bol2_032", "bol3_033"), ("uk_034", "uk2_035", "uk3_036"), ("bez_037", "bez2_038", "bez3_039"),
                                        ("fack_043", "fack2_044", "fack 3_045"), ("miz_040", "miz 2_041", "miz 3_042")))):
    for _finger, _three in zip(("Thumb", "Index", "Middle", "Ring", "Pinky"), _f):
        for _n, _bone in zip((1, 2, 3), _three):
            BAKI_RENAME[f"{_pre} {_bone}"] = f"mixamorig:{_side}Hand{_finger}{_n}"

# 블리치 ROS 그림죠(pl038_) → mixamorig. 이치고(pl0NN_)와 같은 이름 규칙 — hips/lback/mback/uback/thorax/neck/head + clavicle/shoulder/elbow/hand + groin/knee/ankle/toe.
#   thorax는 이치고와 마찬가지로 이름 그대로 둔다(유니티는 매핑 안 된 중간 뼈를 그냥 지나간다). 손가락 뼈는 이 리그에 없다.
GRIMM_RENAME = {"pl038_cos00_00_hips_717": "mixamorig:Hips", "pl038_cos00_00_lback_655": "mixamorig:Spine",
                "pl038_cos00_00_mback_654": "mixamorig:Spine1", "pl038_cos00_00_uback_653": "mixamorig:Spine2",
                "pl038_cos00_00_neck_485": "mixamorig:Neck", "pl038_face00_00_head_484": "mixamorig:Head"}
for _p, _side, _ids in (("R", "Right", (539, 538, 526, 512, 685, 673, 657, 656)),
                        ("L", "Left", (585, 584, 572, 558, 715, 703, 687, 686))):
    _cl, _sh, _el, _ha, _gr, _kn, _an, _to = _ids
    GRIMM_RENAME.update({f"pl038_cos00_00_clavicle_{_p}_{_cl}": f"mixamorig:{_side}Shoulder",
                         f"pl038_cos00_00_shoulder_{_p}_{_sh}": f"mixamorig:{_side}Arm",
                         f"pl038_cos00_00_elbow_{_p}_{_el}": f"mixamorig:{_side}ForeArm",
                         f"pl038_cos00_00_hand_{_p}_{_ha}": f"mixamorig:{_side}Hand",
                         f"pl038_cos00_00_groin_{_p}_{_gr}": f"mixamorig:{_side}UpLeg",
                         f"pl038_cos00_00_knee_{_p}_{_kn}": f"mixamorig:{_side}Leg",
                         f"pl038_cos00_00_ankle_{_p}_{_an}": f"mixamorig:{_side}Foot",
                         f"pl038_cos00_00_toe_{_p}_{_to}": f"mixamorig:{_side}ToeBase"})

# 블리치 ROS 아이젠(pl020) — 그림죠(pl038)와 같은 리그, 번호만 다르다.
AIZEN_RENAME = {"pl020_cos02_00_hips_587": "mixamorig:Hips", "pl020_cos02_00_lback_529": "mixamorig:Spine",
                "pl020_cos02_00_mback_528": "mixamorig:Spine1", "pl020_cos02_00_uback_485": "mixamorig:Spine2",
                "pl020_cos02_00_neck_332": "mixamorig:Neck", "pl020_face01_00_head_331": "mixamorig:Head"}
for _p, _side, _ids in (("R", "Right", (389, 388, 376, 358, 556, 547, 531, 530)),
                        ("L", "Left", (439, 438, 426, 408, 583, 574, 558, 557))):
    _cl, _sh, _el, _ha, _gr, _kn, _an, _to = _ids
    AIZEN_RENAME.update({f"pl020_cos02_00_clavicle_{_p}_{_cl}": f"mixamorig:{_side}Shoulder",
                         f"pl020_cos02_00_shoulder_{_p}_{_sh}": f"mixamorig:{_side}Arm",
                         f"pl020_cos02_00_elbow_{_p}_{_el}": f"mixamorig:{_side}ForeArm",
                         f"pl020_cos02_00_hand_{_p}_{_ha}": f"mixamorig:{_side}Hand",
                         f"pl020_cos02_00_groin_{_p}_{_gr}": f"mixamorig:{_side}UpLeg",
                         f"pl020_cos02_00_knee_{_p}_{_kn}": f"mixamorig:{_side}Leg",
                         f"pl020_cos02_00_ankle_{_p}_{_an}": f"mixamorig:{_side}Foot",
                         f"pl020_cos02_00_toe_{_p}_{_to}": f"mixamorig:{_side}ToeBase"})

# 킹콩(2005, Sketchfab glb, 2026-09-17 전설적인_임채현): 사스케와 같은 영어 띄어쓰기 규칙 + glTF 번호 꼬리(rename_strip). 척추가 넷이라 spine middle 2는 매핑 안 함.
KONG_RENAME = {"pelvis": "mixamorig:Hips", "spine lower": "mixamorig:Spine", "spine middle 1": "mixamorig:Spine1", "spine upper": "mixamorig:Spine2",
               "head neck lower": "mixamorig:Neck", "head neck upper": "mixamorig:Head"}
for _s, _side in (("left", "Left"), ("right", "Right")):
    KONG_RENAME.update({f"arm {_s} shoulder 1": f"mixamorig:{_side}Shoulder", f"arm {_s} shoulder 2": f"mixamorig:{_side}Arm",
                        f"arm {_s} elbow": f"mixamorig:{_side}ForeArm", f"arm {_s} wrist": f"mixamorig:{_side}Hand",
                        f"leg {_s} thigh": f"mixamorig:{_side}UpLeg", f"leg {_s} knee": f"mixamorig:{_side}Leg",
                        f"leg {_s} ankle": f"mixamorig:{_side}Foot", f"leg {_s} toes": f"mixamorig:{_side}ToeBase"})
    for _f in ("thumb", "index", "middle", "ring", "pinky"):
        for _k in (1, 2, 3):
            KONG_RENAME[f"arm {_s} finger {_f} {_k}"] = f"mixamorig:{_side}Hand{_f.capitalize()}{_k}"

# 원펀맨 킹(아무도 모르는 영웅 립 → XPS/Blender/FBX 팬 변환, 2026-09-17 전설적인_정윤식): 심해왕과 같은 게임 뼈 이름인데 bone0000_ 접두어·번호 꼬리가 없고
#   어깨 이름이 한 칸 다르다 — SHOULDER=쇄골 · ARM=위팔(심해왕은 CLAVICLE·SHOULDER). CLANK=무릎 · TOE1=발목 · TOE2=발끝. 손가락 FORE=검지 · MEDICINAL=약지 · LITTLE=새끼.
KING_RENAME = {"WAIST": "mixamorig:Hips", "SPINE1": "mixamorig:Spine", "SPINE2": "mixamorig:Spine1", "SPINE3": "mixamorig:Spine2",
               "NECK": "mixamorig:Neck", "HEAD": "mixamorig:Head"}
for _p, _side in (("L", "Left"), ("R", "Right")):
    KING_RENAME.update({f"SHOULDER_{_p}": f"mixamorig:{_side}Shoulder", f"ARM_{_p}": f"mixamorig:{_side}Arm", f"ELBOW_{_p}": f"mixamorig:{_side}ForeArm",
                        f"WRIST_{_p}": f"mixamorig:{_side}Hand", f"THIGH_{_p}": f"mixamorig:{_side}UpLeg", f"CLANK_{_p}": f"mixamorig:{_side}Leg",
                        f"TOE1_{_p}": f"mixamorig:{_side}Foot", f"TOE2_{_p}": f"mixamorig:{_side}ToeBase"})
    for _src, _dst in (("THUMB", "Thumb"), ("FORE", "Index"), ("MIDDLE", "Middle"), ("MEDICINAL", "Ring"), ("LITTLE", "Pinky")):
        for _k in (1, 2, 3):
            KING_RENAME[f"F_{_src}{_k}_{_p}"] = f"mixamorig:{_side}Hand{_dst}{_k}"

# 히나타(Free Fire 립 mixamorig 78뼈, 2026-09-17 전설적인_이현주): 🔴 얼굴은 −Y인데 mixamorig:Left* 뼈가 −X(캐릭터의 오른쪽)에 있다 — 좌우 이름이 뒤바뀐 거울 리그
#   (아마추어 배율 −0.01 · 메시 −7.875). 유니티가 Left 팔 애니를 실제 오른팔에 입혀 손목이 바깥으로 꺾였다 → Left↔Right 이름을 맞바꾼다(임시 이름 거쳐 세 단계).
_HINATA_PARTS = ["Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase"] + [f"Hand{f}{k}" for f in ("Thumb", "Index", "Middle", "Ring", "Pinky") for k in (1, 2, 3)]
HINATA_SWAP = {}
HINATA_SWAP.update({f"mixamorig:Left{p}": f"__swapL_{p}" for p in _HINATA_PARTS})
HINATA_SWAP.update({f"mixamorig:Right{p}": f"mixamorig:Left{p}" for p in _HINATA_PARTS})
HINATA_SWAP.update({f"__swapL_{p}": f"mixamorig:Right{p}" for p in _HINATA_PARTS})

UNITS = {
    # 바운티러시 시류 → 희귀함_노수신(2026-09-16 희귀함 9호). 마르코·후즈후와 같은 (merge) pl_ 리그(뼈 81 · 배율 0.01 · 재질 1 · 빈 오브젝트 15).
    #   실측: **이미 T자**(Upper ±0.0046 → Fore ±0.0107 → Palm ±0.0157이 전부 z 0.0266) · 기본 자세 = 쉬는 자세(어긋난 뼈 0) → tpose_arms 불필요.
    #   겹친 변형: 표정 4벌 → face_normal · 손 5벌 → l/r_hand_open · 담배 2벌 → 입에 문 cigarette_mouth · 무기 5벌 → **weapon_01 한 자루**(오른손 r_weapon_joint에 434정점으로 제대로 실림).
    #     버린 무기 메시의 뼈(l_weapon_joint 계열·cigarette_joint)도 같이 뺀다 — 남기면 가중치 0 뼈가 경계를 부풀린다(마르코 날개·이치고 칼과 같은 부류).
    #   🔴 모리아 교훈: `l_collar`·`l/r_coat_shoulder` 같은 코트 보조 뼈가 유니티 자동 매핑에서 Jaw·Eye로 겹쳐 잡힐 수 있다 → 매핑 뼈를 PL_RENAME으로 mixamorig 이름으로 박고,
    #     Head_Face 자손(head_jaw · cigarette_mouth_joint)은 merge_bones로 Head에 합쳐 후보를 없앤다.
    #   🔴 텍스처 알파는 음영 마스크(최소 0.133 · 평균 0.835 · 87.7%가 0.98 미만, 이진 아님) → archive_rgb로 알파 뗀 RGB PNG.
    #   남길 메시 9개 약 8.4천 삼각형이라 감량 없음.
    # 원피스 바운티러시 실버즈 레일리(pl_rayleigh_orig01) → 희귀함_이승우(2026-09-16 희귀함, ※ 동명 셋 중 희귀함). 시류와 같은 pl_ 리그 — 처리도 그대로.
    #   zip 안 rar 안 FBX · 뼈 49 · 메시 15 · 재질 1 · 기본 자세 = 결합 자세.
    #   겹친 변형: 얼굴 face_normal 남김(attack·damage 뺌) · 손 l/r_hand_open 남김(close·skittle 뺌) · skittle(술병) · weapon_01(왼손 칼, 앞으로 뻗음 — 시류 기준) 뺌, 뼈도 같이.
    #   glass(안경)는 얼굴 부속이라 남김. 🔴 _diff 알파는 음영 마스크(최소 0.09 · 평균 0.808 · 100%가 0.98 미만) → archive_rgb로 뗌. 툰 셰이더용 matcap·ramp·dither·abnormal은 안 씀.
    "희귀함_이승우": dict(path="Assets/Art/Units/희귀함_이승우/희귀함_이승우.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_이승우.zip"), "source/pl_rayleigh_orig01.rar", "pl_rayleigh_orig01/pl_rayleigh_orig01.fbx"),
                      archive_rgb={"pl_rayleigh_orig01/pl_rayleigh_orig01_diff.png": "pl_rayleigh_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_skittle", "r_hand_skittle", "skittle", "weapon_01"],
                      # 1회차 판정: 가중치 0 뼈 4(뿌리 pl_rayleigh_orig01 · world_joint · HELPER_key · HELPER_name — 뼈 넘침 0.132) → 같이 뺀다(Hips가 뿌리)
                      drop_bones=["weapon_01_joint", "skittle_joint", "HELPER_key", "HELPER_name", "world_joint", "pl_rayleigh_orig01"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=dict(under="mixamorig:Head", into="mixamorig:Head"),
                      materials=dict(textures={"pl_rayleigh_orig01": [("DiffuseColor", "pl_rayleigh_orig01_diff.png")]})),
    "희귀함_노수신": dict(path="Assets/Art/Units/희귀함_노수신/희귀함_노수신.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_노수신.zip"), "source/shiryu.rar", "shiryu/pl_shiryu_hach01 (merge).fbx"),
                      archive_rgb={"shiryu/pl_shiryu_hach01_diff.png": "pl_shiryu_hach01_diff.png"},
                      # 🔴 무기는 **다섯 벌 전부 뺀다**: weapon_01은 오른손에 제대로 실려 있지만 길이 1.8 m짜리 칼을 수평으로 앞으로 뻗고 있어
                      #   T자 전시에서 몸을 가로지르고 깊이를 2.14 m로 부풀린다(렌더 확인). 이치고의 등에 멘 칼이나 후즈후 꼬리와 달리 실루엣으로 안 읽힌다.
                      #   되살리려면 weapon_01 + r_weapon_joint만 살리면 된다(오른손에 434정점으로 붙어 있다).
                      drop_meshes=["face_attack", "face_damage", "face_sp01",
                                   "l_hand_close", "r_hand_close", "r_hand_open_02",
                                   "weapon_01", "weapon_01_b", "weapon_02", "weapon_03", "weapon_03_b", "cigarette"],
                      drop_bones=["l_weapon_02_joint", "l_weapon_03_joint", "l_weapon_joint", "r_weapon_joint", "cigarette_joint"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=dict(under="mixamorig:Head", into="mixamorig:Head"),
                      materials=dict(textures={"pl_shiryu_hach01": [("DiffuseColor", "pl_shiryu_hach01_diff.png")]})),
    # 블리치 ROS 그림죠 → 희귀함_박기찬(2026-09-16 희귀함 8호). 이치고(오타쿠의길)와 같은 pl0NN_ 리그 — 처리도 그대로.
    #   실측: 뼈 720(가중치 651) · 메시 21 · 재질 20 · 이미지 17 · A자 45°(어깨 ±0.188/1.473 → 팔꿈치 ±0.374/1.287 → 손 ±0.562/1.099) · 뼈 기준 키 1.83.
    #   🔴 Head 자손이 **472개**(가중치 있는 것 460) — 눈썹·눈꺼풀·동공·턱까지 전부 Head 밑이라 아디오식 Jaw/Eye 오매핑 조건이 딱 맞는다 → merge_bones(under=Head).
    #   🔴 칼 메시 `Object_11`(7,888정점, y −1.0까지 뻗음)은 **손 뼈가 아니라 자기 wep00 뼈 사슬**에 실려 있다 → 메시와 그 뼈 8개를 같이 뺀다(남길 메시엔 가중치 0 — 실측).
    #     허리에 찬 칼집(Object_35)·손잡이(Object_9)는 `waist_weapon_root`에 제대로 실려 있어 남긴다(시류·이치고와 같은 판단).
    #   🔸 머리카락 메시만 UV 층이 2개(UVMap/UVMap.001) — 이 경로는 메시를 합치지 않으니 히소카식 사고는 안 나지만 내보낸 뒤 UV0 범위를 확인한다.
    #   조명용 Icosphere 뺌 · 남길 156,368삼각형 → 1,000삼각형 이상만 ×0.25로 약 4만.
    "희귀함_박기찬": dict(path="Assets/Art/Units/희귀함_박기찬/희귀함_박기찬.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_박기찬.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere", "Object_11"],
                      drop_bones=['pl038_wep00_00_00_body_510', 'pl038_wep00_00_00_blade00_508', 'pl038_wep00_00_00_blade01_507', 'pl038_wep00_00_00_blade02_506', 'pl038_wep00_00_00_blade03_505', 'pl038_wep00_00_00_blade04_504', 'pl038_wep00_00_00_blade05_503', 'pl038_wep00_00_00_bottom_509'],
                      rename_bones=GRIMM_RENAME,
                      merge_bones=dict(under="mixamorig:Head", into="mixamorig:Head"),
                      decimate=dict(min_tris=1000, ratio=0.25),
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm",
                                      "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "face_baseColor.png", 1: "weapon_baseColor.png", 3: "cloth_baseColor.png", 6: "eyes_baseColor.png",
                                  7: "feet_baseColor.png", 9: "hair_baseColor.png", 10: "hakama_baseColor.png", 12: "skin_baseColor.png",
                                  14: "skull_baseColor.png", 16: "eyeshadow_baseColor.png"},
                      materials=dict(textures={
                          "5_face_1_0_0": [("DiffuseColor", "face_baseColor.png")],
                          "5_faceshadow_1.0_0_0.002": [("DiffuseColor", "face_baseColor.png")],
                          "5_facelines_1.0_0_0.004": [("DiffuseColor", "face_baseColor.png")],
                          "5_eyebrows_1.0_0_0.004": [("DiffuseColor", "face_baseColor.png")],
                          "5_mouth_1.0_0_0.004": [("DiffuseColor", "face_baseColor.png")],
                          "5_teeth_1.0_0_0.004": [("DiffuseColor", "face_baseColor.png")],
                          "5_hiltwaist_1.0_0_0": [("DiffuseColor", "weapon_baseColor.png")],
                          "5_scabbard_1.0_0_0": [("DiffuseColor", "weapon_baseColor.png")],
                          "5_cloth_1.0_0_0.002": [("DiffuseColor", "cloth_baseColor.png")],
                          "5_eyes_1.0_0_0.004": [("DiffuseColor", "eyes_baseColor.png")],
                          "5_eyewhite_1.0_0_0.004": [("DiffuseColor", "eyes_baseColor.png")],
                          "5_eyelashes_1.0_0_0.004": [("DiffuseColor", "eyes_baseColor.png")],
                          "5_tongue_1.0_0_0.004": [("DiffuseColor", "eyes_baseColor.png")],
                          "5_feet_1.0_0_0.001": [("DiffuseColor", "feet_baseColor.png")],
                          "5_hair_1.0_0_0.004": [("DiffuseColor", "hair_baseColor.png")],
                          "5_hakama_1.0_0_0.001": [("DiffuseColor", "hakama_baseColor.png")],
                          "5_skin_1_0_0": [("DiffuseColor", "skin_baseColor.png")],
                          "5_skull_1.0_0_0.004": [("DiffuseColor", "skull_baseColor.png")],
                          "7_eyeshadow_1.0_0_0.004": [("DiffuseColor", "eyeshadow_baseColor.png")]})),
    # 원피스 바운티러시 제저스 버제스 2년 전(pl_burgess_orig01) → 초월_구주호_AD(2026-09-18 초월). 샹크스형 pl_ 리그(아마추어 「Armature」·뿌리 pl_burgess_orig01 → world_joint).
    #   zip 안 source/「Jesus Burgess - Blackbeard Pirates.7z」(🔴 공백) 안 FBX · 뼈 42 · 메시 11 · 재질 1 · ×0.01 · 이미 T자 · 무기 없음 · _diff 알파 1.0 · 거구.
    #   겹친 변형: face_normal · l/r_hand_open 남김(face_attack·damage · l/r_hand_close · l_hand_sp 뺌) · hat·hair·body 유지.
    #   뺄 뼈(가중치 0): 뿌리·world_joint·model_root·eff_muzzle_a~c·pre/post_flag·HELPER_key/name. 머리카락 c/l/r_hair(Head_Face 밑) → Head.
    "초월_구주호_AD": dict(path="Assets/Art/Units/초월_구주호_AD/초월_구주호_AD.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "08_초월/초월_구주호_AD.zip"), "source/Jesus Burgess - Blackbeard Pirates.7z",
                               "Jesus Burgess Blackbeard Pirates/pl_burgess_orig01.fbx"),
                      archive_rgb={"Jesus Burgess Blackbeard Pirates/pl_burgess_orig01_diff.png": "pl_burgess_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_sp"],
                      drop_bones=["pl_burgess_orig01", "world_joint", "model_root", "eff_muzzle_a", "eff_muzzle_b", "eff_muzzle_c",
                                  "pre_flag", "post_flag", "HELPER_key", "HELPER_name"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_burgess_orig01": [("DiffuseColor", "pl_burgess_orig01_diff.png")]})),
    # 원피스 바운티러시 킬러 오니가시마(팬 재내보내기 「Killer Onigashima by Annettlw」, pl_killer_atta01) → 초월_배성령_AD(2026-09-18 초월). 샹크스(전설적인_이일중)와 같은 재내보내기판.
    #   zip 안 rar(🔴 공백) 안 FBX + zip textures/pl_killer_atta01_diff.png · 아마추어 pl_killer_atta01 · 뿌리 world_joint · 뼈 34 · 메시 10 · 재질 1 · ×0.01 · 이미 T자 · _diff 알파 1.0.
    #   얼굴은 face_normal 하나(가면). 겹친 변형: l/r_hand_open 남김(close 뺌). 🔴 l/r_weapon_01(양손 펀치 블레이드) + l/r_weapon_joint·weapon_length_root_joint 뺌.
    #   sheath(허리 뒤 칼집, sheath_joint) 유지 → Hips · hair_01~03(긴 금발, Head_Face 밑) → Head · waist_01/02 → Hips · L/RHand_Fore_sup → ForeArm.
    "초월_배성령_AD": dict(path="Assets/Art/Units/초월_배성령_AD/초월_배성령_AD.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "08_초월/초월_배성령_AD.zip"), "source/Killer Onigashima.rar", "Killer Onigashima/Killer Onigashima by Annettlw.fbx"),
                      archive_rgb={"Killer Onigashima/pl_killer_atta01_diff.png": "pl_killer_atta01_diff.png"},
                      drop_meshes=["l_hand_close", "r_hand_close", "l_weapon_01", "r_weapon_01"],
                      drop_bones=["world_joint", "l_weapon_joint", "r_weapon_joint", "l_weapon_length_root_joint", "r_weapon_length_root_joint"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^(sheath_joint|waist_0[12]_joint)$", into="mixamorig:Hips"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_killer_atta01": [("DiffuseColor", "pl_killer_atta01_diff.png")]})),
    # 원피스 마샬 D. 티치/검은수염(중국 모바일 립 heihuzi=黑胡子 glb, Biped Bip001) → 초월_김건_AP(2026-09-18 초월). 쿠마 과거·토지 계열.
    #   zip 안 source/「Marshall D_ Teach.glb」 + textures/(heihuzi_shenti_1 · heihuzi_pifeng_0 — glb 내장과 같은 이름) · 뼈 94(손가락 5×3 · Bone01~41) ·
    #   애니 12 · 메시 2 + 조명 Icosphere · 재질 2(OPAQUE, 알파 1.0) · 삼각형 36,645 · 결합 자세 키 약 2.3(모자·모피 깃 포함).
    #   🔴 애니 12 → 가져온 자세는 전투 자세(렌더 teach/look.png 왼쪽) · 결합 자세는 선 A자 → use_rest_pose.
    #   보조 뼈(부모 확인): Head 밑 Bone01~03(모자 깃털)·16/21~23(수염)·17~20(얼굴)·41(모자) → Head · 쇄골 밑 Bone28/27(어깨) → 그쪽 Shoulder ·
    #     Spine2 밑 망토 사슬 Bone04/07/10/13 · 05/09/11/14 · 06/08/12/15 → Spine2 · Spine 밑 배·권총 Bone24~26 → Spine ·
    #     코트 자락 사슬 Bone29~40(Spine 밑, 좌 x>0 · 우 x<0) → 그쪽 넓적다리 50%·Hips 50%(아이젠 천년혈전 교훈).
    "초월_김건_AP": dict(path="Assets/Art/Units/초월_김건_AP/초월_김건_AP.fbx", kind="human", size=("height", 1.8), use_rest_pose=True,
                      archive=(os.path.join(SKINS, "08_초월/초월_김건_AP.zip"), "source/Marshall D_ Teach.glb"),
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                      rename_bones=biped_rename("Bip001", spine2="Spine2"),
                      drop_bones=["Bip001"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bone28$", into="mixamorig:LeftShoulder"),
                                   dict(pattern=r"^Bone27$", into="mixamorig:RightShoulder"),
                                   dict(pattern=r"^Bone(0[4-9]|1[0-5])$", into="mixamorig:Spine2"),
                                   dict(pattern=r"^Bone2[4-6]$", into="mixamorig:Spine"),
                                   dict(pattern=r"^Bone(29|31|33|35|37|39)$", into="mixamorig:LeftUpLeg", share=0.5, rest="mixamorig:Hips"),
                                   dict(pattern=r"^Bone(30|32|34|36|38|40)$", into="mixamorig:RightUpLeg", share=0.5, rest="mixamorig:Hips")],
                      tpose_arms=biped_tpose_names(), seed_zero_bones=0.001,
                      glb_images={0: "heihuzi_pifeng.png", 1: "heihuzi_shenti.png"},
                      materials=dict(textures={"Material.002": [("DiffuseColor", "heihuzi_pifeng.png")],
                                               "Material.001": [("DiffuseColor", "heihuzi_shenti.png")]})),
    # 원피스 돈키호테 도플라밍고(Sketchfab-16.7 glb「Donquixote Doflamingo」, CC-BY-4.0 LorisC93) → 초월_김만경_AD(2026-09-18 초월). 반다이 뼈 이름 + _번호 꼬리(유기·소닉·킹 계열).
    #   뼈 33 · 메시 24(Object_5~28) + 조명 Icosphere · 재질 24(OPAQUE) · 이미지 11(알파 없음) · 삼각형 16,958 · 애니 0 · 스킨 좌표 0.03 수준.
    #   🔴 어깨는 **유기식**: CLAVICLE=쇄골 · SHOULDER=위팔 · ELBOW=아래팔 · WRIST=손 · TOE1=발(발끝 TOE2·손가락 뼈 없음) → 있는 이름만 표로.
    #   분홍 깃털 코트(fur_base4·fur_base3·fur_mant4·fur_eri3·polySurface5 = Object_24~28) 뼈 CLOTH_A_00~02(+가중치 0 CLOTH_A_03~08) → Spine2.
    #   뿌리 _rootJoint 밑 NULL_01·RESERVE_02(가중치 0) 뺌 · FACE_016 → Head.
    "초월_김만경_AD": dict(path="Assets/Art/Units/초월_김만경_AD/초월_김만경_AD.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "08_초월/초월_김만경_AD.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere"],
                      rename_bones=dict({"WAIST": "mixamorig:Hips", "SPINE1": "mixamorig:Spine", "SPINE2": "mixamorig:Spine1", "SPINE3": "mixamorig:Spine2",
                                         "NECK": "mixamorig:Neck", "HEAD": "mixamorig:Head"},
                                        **{f"{b}_{p}": f"mixamorig:{side}{j}" for p, side in (("L", "Left"), ("R", "Right"))
                                           for b, j in (("CLAVICLE", "Shoulder"), ("SHOULDER", "Arm"), ("ELBOW", "ForeArm"), ("WRIST", "Hand"),
                                                        ("THIGH", "UpLeg"), ("CLANK", "Leg"), ("TOE1", "Foot"))}),
                      rename_strip=r"_\d+$",
                      drop_bones=["NULL_01", "RESERVE_02"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^CLOTH_A_0\d_\d+$", into="mixamorig:Spine2")],
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                      "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                      seed_zero_bones=0.001,
                      glb_images={0: "dofla_cloth.png", 3: "dofla_skin.png", 6: "dofla_accessory.png", 8: "dofla_face.png", 9: "dofla_hair.png", 10: "dofla_fur.png"},
                      materials=dict(textures={m: [("DiffuseColor", f)] for m, f in {
                          "shoesShapeC_SHOES": "dofla_cloth.png", "shoesShapeC_SHOES_R": "dofla_cloth.png", "shirtsShapeC_CLOTH": "dofla_cloth.png",
                          "buttonShapeC_CLOTH": "dofla_cloth.png", "handShapeS_SKIN": "dofla_skin.png", "footShapeS_SKIN": "dofla_skin.png",
                          "footShapeS_SKIN_R": "dofla_skin.png", "earringsShapeA_GLASS": "dofla_accessory.png", "toothdownShapeT_TOOTH": "dofla_face.png",
                          "toothupShapeT_TOOTH": "dofla_face.png", "mouthShapeS_FACE": "dofla_face.png", "B_face1ShapeS_FACE": "dofla_skin.png",
                          "beltShapeC_CLOTH": "dofla_cloth.png", "ropeShapeC_CLOTH": "dofla_cloth.png", "pantsShapeC_PANTS": "dofla_cloth.png",
                          "hair2ShapeH_HAIR": "dofla_hair.png", "glasses3ShapeA_GLASS": "dofla_accessory.png", "glasses3ShapeA_FRAME": "dofla_accessory.png",
                          "body1ShapeS_SKIN": "dofla_skin.png", "fur_base4ShapeC_FUR": "dofla_fur.png", "fur_baseShape3C_FUR": "dofla_fur.png",
                          "fur_mant4ShapeC_FUR": "dofla_fur.png", "fur_eri3ShapeC_FUR": "dofla_fur.png", "polySurfaceShape5C_FUR": "dofla_fur.png"}.items()})),
    # 원피스 바솔로뮤 쿠마 과거(OPDS 립 「Kuma (Flashback)」 role_mushixiong_skin.fbx, 제작 O-DV89-O) → 초월_조성진_AD(2026-09-18 초월). 토지(희귀함_이재윤)와 같은 중국 모바일 role_*_skin 계열.
    #   zip 안 source/*.rar 안 FBX(+ 같은 그림 .blend·xps — FBX 사용) · 뼈 99(🔴 Bip002 · 손가락 5×3) · 메시 2 · 재질 2 · ×0.01 · 무가중치 0.
    #   모습(렌더 kuma/look.png): 검은 옷·붉은 허리띠·오른쪽으로 날리는 흰 망토(pifeng) · **왼손에 성경(Bone023, 원작 소품)** 유지.
    #   보조 뼈: Head 밑 Bone024/044~050(머리카락) → Head · 망토 사슬 Bone051~061·(mirrored)·쇄골 밑 Bone054~057·(mirrored) → Spine2(팔 몫 0) ·
    #     Spine 밑 앞·뒤·옆 자락 Bone062~078·(mirrored) → Spine(여기선 다리도 Spine 자식) · 성경 Bone023 → LeftHand. 가중치 0 끝 뼈는 같이 합쳐 사라진다.
    #   텍스처 알파 거의 1(최소 146, 0.000%) → archive_rgb로 뗌.
    #   🔴 1회차 use_rest_pose: 성경(Bone023)·망토 뼈의 결합 자세가 원점·머리 위라 성경이 손에서 떨어져 발밑에, 망토가 머리 위로 솟음(키 1.964) → 가져온 기본 자세(서서 성경 든 모습) 사용.
    "초월_조성진_AD": dict(path="Assets/Art/Units/초월_조성진_AD/초월_조성진_AD.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "08_초월/초월_조성진_AD.zip"), "source/opds___kuma__flashback__by_o_dv89_o_djj3jcd.rar",
                               "OPDS - Kuma (Flashback)/role_mushixiong_skin.fbx"),
                      archive_rgb={"OPDS - Kuma (Flashback)/role_mushixiong.png": "role_mushixiong.png",
                                   "OPDS - Kuma (Flashback)/role_mushixiong_pifeng.png": "role_mushixiong_pifeng.png"},
                      # 🔴 2026-09-18 사장님 「망토가 이상함」: 원본은 바람에 날려 머리 위(키 1.99)까지 솟고 옆으로 퍼진 흰 망토 → fold_mesh로 어깨 둘레(반지름 0.0075) 밖을 아래로 접어 등 뒤로 늘어뜨림.
                      fold_mesh=[dict(mesh="mushixiong_pifeng_shizhuang", pivot=(0.0, 0.0015, 0.0205), r_keep=0.004, drop=0.9, back=0.15, keep_out=0.18)],
                      no_nulls=True, orient_snap=True,
                      rename_bones=biped_rename("Bip002", spine2="Spine2"),
                      drop_bones=["Bip002"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bone023$", into="mixamorig:LeftHand"),
                                   dict(pattern=r"^Bone0(5[1-9]|6[01])(\(mirrored\))?$", into="mixamorig:Spine2"),
                                   dict(pattern=r"^Bone0(6[2-9]|7[0-8])(\(mirrored\))?$", into="mixamorig:Spine")],
                      tpose_arms=biped_tpose_names(), seed_zero_bones=0.001,
                      materials=dict(textures={"daxiong_yifu": [("DiffuseColor", "role_mushixiong.png")],
                                               "daxiong_pifeng": [("DiffuseColor", "role_mushixiong_pifeng.png")]})),
    # 블리치 바라간 루이센방 해방형(모바일 게임 립 bailegangwanjie_0_battleout → Noesis glb, Biped Bip001) → 초월_임장혁_AD(2026-09-18 초월). 긴·코마무라와 같은 형식.
    #   뼈 78 · 메시 7 + 조명 Icosphere · 재질 7(OPAQUE) · 애니 19 · 삼각형 8,914 · 결합 자세 키 약 2.0(왕관 해골 · 넝마 로브).
    #   🔴 wing_0(5,963)은 날개·망토가 아니라 **옆에 세운 큰 양날 도끼(그란 카이다)와 사슬**(렌더 barr/look.png) — 뿌리·Bip001 Prop1·Bone061~078에 실림 → 무기라 메시·뼈째 뺌.
    #   왼손에서 늘어진 사슬(Bone049/050·052/053)과 소매(Bone055/056·058/059)는 몸 메시 → ForeArm. 로브 자락 사슬 Bone028~031(Spine2 밑) → Spine2 ·
    #   Bone033~047(Spine 밑 앞·옆·뒤 자락) → Spine. 넓적다리·Pelvis·Neck은 가중치 0(로브가 덮음) → 씨앗. 애니 19 → use_rest_pose.
    "초월_임장혁_AD": dict(path="Assets/Art/Units/초월_임장혁_AD/초월_임장혁_AD.fbx", kind="human", size=("height", 1.8), use_rest_pose=True,
                      archive=(os.path.join(SKINS, "08_초월/초월_임장혁_AD.zip"), "source/bailegangwanjie_0_battleout.glb"),
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere", "bailegangwanjie_wing_0_noesis_meshnode_0003"],
                      rename_bones=biped_rename("Bip001", fingers=2, joints=2, spine2="Spine2"),
                      drop_bones=["bailegangwanjie_0_battle", "Bip001", "Bip001 Prop1"],
                      drop_bones_re=r"^Bone0(6[1-9]|7[0-8])$",
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bone0(49|50|52|53|58|59)$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^Bone05[56]$", into="mixamorig:RightForeArm"),
                                   dict(pattern=r"^Bone0(2[89]|3[01])$", into="mixamorig:Spine2"),
                                   dict(pattern=r"^Bone0(3[3-9]|4[0-7])$", into="mixamorig:Spine")],
                      # 1회차 Idle 상위1% 1.54(최대 14.3) — 털 옷깃 짧은 변에 Spine2·Shoulder 몫이 0.25/0.75로 뚝 끊김 → 이웃 평균 3회
                      smooth_weights={"bailegangwanjie_body_0_noesis_meshnode_0000": 3},
                      tpose_arms=biped_tpose_names(fingers=2, joints=2), seed_zero_bones=0.001,
                      glb_images={0: "bailegang_body_baseColor.png", 1: "bailegang_face_baseColor.png", 2: "bailegang_hair_baseColor.png",
                                  3: "bailegang_leye_baseColor.png", 4: "bailegang_mouth_baseColor.png", 5: "bailegang_reye_baseColor.png"},
                      materials=dict(textures={f"bailegangwanjie_{k}_0": [("DiffuseColor", f"bailegang_{k}_baseColor.png")]
                                               for k in ("body", "face", "hair", "leye", "mouth", "reye")})),
    # 원펀맨 음속의 소닉(A Hero Nobody Knows 립 「Speed-o'-Sound Sonic」, Sketchfab-16.75 glb, CC-BY-4.0 Cyrone) → 초월_김민준_AP(2026-09-18 초월). 킹(전설적인_정윤식)과 같은 게임 뼈 이름에 _번호 꼬리.
    #   뼈 164 · 메시 13 + 조명 Icosphere · 재질 13(OPAQUE) · 이미지 11 · 삼각형 32,456 · 애니 0 · 스킨 좌표 0.02 수준(키 맞춤으로 1.8).
    #   메시(glTF 순서 = Object_5~17): Body · Mantle(목도리) · Default.Eyes/Face/Mouth · SwordBack(등 칼, WEP2) · -Neta.Eyes/Face/Mouth(「-」 숨김 변형) ·
    #     -SwordHand(손에 든 칼, EQUIPMENT_00_R) · Armor · Hair · Sheath(칼집, mantle1). → 「-」 넷(Object_11~14)과 Icosphere 뺌.
    #   뼈: KING_RENAME + rename_strip(_번호) — SHOULDER=쇄골 · ARM=위팔(킹과 같음). ROOT_GROUND_02·EQUIPMENT_00_R_01(칼 뺀 뒤 가중치 0) 뺌.
    #   합치기: Head 자손(FACE·눈썹·입·이·HAIR) → Head · NECKSCALE → Neck · 목도리 mantle1~34·등 칼 WEP2 → Spine2 · ROLL → 본체 · CLANK_?C → Leg.
    "초월_김민준_AP": dict(path="Assets/Art/Units/초월_김민준_AP/초월_김민준_AP.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "08_초월/초월_김민준_AP.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere", "Object_11", "Object_12", "Object_13", "Object_14"],
                      rename_bones=KING_RENAME, rename_strip=r"_\d+$",
                      drop_bones=["ROOT_GROUND_02", "EQUIPMENT_00_R_01"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^NECKSCALE_\d+$", into="mixamorig:Neck"),
                                   dict(pattern=r"^(mantle\d+|WEP2)_\d+$", into="mixamorig:Spine2")]
                                  + [d for p, side in (("L", "Left"), ("R", "Right")) for d in (
                                      dict(pattern=rf"^SHOULDERROLL_{p}_\d+$", into=f"mixamorig:{side}Arm"),
                                      dict(pattern=rf"^ELBOWROLL_{p}_\d+$", into=f"mixamorig:{side}ForeArm"),
                                      dict(pattern=rf"^CLAVICLEROLL_{p}_\d+$", into=f"mixamorig:{side}Shoulder"),
                                      dict(pattern=rf"^(CLANKROLL_{p}|CLANK_{p}C)_\d+$", into=f"mixamorig:{side}Leg"))],
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(_BIPED_FINGERS)
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      seed_zero_bones=0.001,
                      glb_images={0: "sonic_body.png", 1: "sonic_mantle.png", 2: "sonic_eyes.png", 3: "sonic_face.png", 4: "sonic_mouth.png",
                                  5: "sonic_sword.png", 9: "sonic_armor.png", 10: "sonic_hair.png"},
                      materials=dict(textures={"4_Body_0.1_1_1": [("DiffuseColor", "sonic_body.png")], "4_Mantle_0.1_1_1": [("DiffuseColor", "sonic_mantle.png")],
                                               "5_Default.Eyes_0.1_1_1": [("DiffuseColor", "sonic_eyes.png")], "5_Default.Face_0.1_1_1": [("DiffuseColor", "sonic_face.png")],
                                               "5_Default.Mouth_0.1_1_1": [("DiffuseColor", "sonic_mouth.png")], "5_SwordBack_0.1_1_1": [("DiffuseColor", "sonic_sword.png")],
                                               "5_Sheath_0.1_1_1": [("DiffuseColor", "sonic_sword.png")], "5_Armor_0.1_1_1": [("DiffuseColor", "sonic_armor.png")],
                                               "5_Hair_0.1_1_1": [("DiffuseColor", "sonic_hair.png")]})),
    # 블리치 코마무라 사진(모바일 게임 립 bocunwanjie_0_battleout → Noesis glb, 3ds Max Biped Bip001) → 초월_강주혁_AP(2026-09-18 초월). 긴(전설적인_이승우)과 같은 형식 — 설정 재사용.
    #   뼈 45 · 메시 7 + 조명 Icosphere · 재질 6(OPAQUE) · 애니 22 · 삼각형 4,465 · 결합 자세 키 약 2.88(늑대 얼굴 거구).
    #   🔴 애니 22 → use_rest_pose. 몸 둘(같은 재질): bocun_body_0(2,182) + bocun_body_0_c(575, 소매·갑옷 자락 겉옷 층 — 긴과 같은 구조) 둘 다 유지.
    #   bocun_weapon_0(칼) + Bip001 Prop1·rweapon·bocun_weapon_0 뼈 뺌 · 뿌리 Bip001 뺌.
    #   얼굴 head·bocun_leye/mouth/reye_0 → Head · Bone005/007(쇄골 밑 어깨 갑옷) → 그쪽 Shoulder · Bone001~004·008(Pelvis 밑 갑옷 자락) → Hips. 손가락 Finger0/1 두 마디.
    "초월_강주혁_AP": dict(path="Assets/Art/Units/초월_강주혁_AP/초월_강주혁_AP.fbx", kind="human", size=("height", 1.8), use_rest_pose=True,
                      archive=(os.path.join(SKINS, "08_초월/초월_강주혁_AP.zip"), "source/bocunwanjie_0_battleout.glb"),
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere", "bocun_weapon_0_noesis_meshnode_0003"],
                      rename_bones=biped_rename("Bip001", fingers=2, joints=2, spine2="Spine2"),
                      drop_bones=["Bip001", "Bip001 Prop1", "rweapon", "bocun_weapon_0"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bone007$", into="mixamorig:LeftShoulder"),
                                   dict(pattern=r"^Bone005$", into="mixamorig:RightShoulder"),
                                   dict(pattern=r"^Bone00[1-48]$", into="mixamorig:Hips")],
                      # 1회차 Idle 상위1% 1.52 — 찢김이 아니라 저폴리(3.5천 정점) 목깃·어깨 굵은 변에 반반 가중치 경계가 퍼져 2~2.8배 → 이웃 평균 2회
                      smooth_weights={"bocun_body_0_noesis_meshnode_0004": 2, "bocun_body_0_c_noesis_meshnode_0005": 2},
                      tpose_arms=biped_tpose_names(fingers=2, joints=2), seed_zero_bones=0.001,
                      glb_images={0: "bocun_body_baseColor.png", 1: "bocun_face_baseColor.png", 2: "bocun_leye_baseColor.png",
                                  3: "bocun_mouth_baseColor.png", 4: "bocun_reye_baseColor.png"},
                      materials=dict(textures={f"bocun_{k}_0": [("DiffuseColor", f"bocun_{k}_baseColor.png")]
                                               for k in ("body", "face", "leye", "mouth", "reye")})),
    # 블리치 ROS 아이젠 소스케 천년혈전 봉인 모습(pl020_cos04 — 오른눈을 덮은 검은 띠·흰 목도리·긴 검은 코트) → 초월_최상호_AP(2026-09-18 초월 1호). 전설적인_최상호(pl020_cos02)와 같은 캐릭터 번호의 다른 의상.
    #   zip 안 source/Sketchfab_2026_09_14_03_41_39.glb + textures/ 16장(glb 내장과 같은 이름) · 삼각형 106,029 · 애니 0 · 쉬는 자세 A자.
    #   🔴 **아마추어 9개**(몸 cos04_00 213뼈 · 얼굴 face01_00 126 · 머리카락 hair01_00 186 · 얼굴 부속 objects_a~e · 무기 wep00) — 옛 판(한 리그)과 다른 내보내기.
    #     가져온 자세로 얼굴·머리카락 뿌리가 목 자리(1.521)에 제대로 앉는다(PM이 본 「y 3.02 떠 있음」은 glTF 노드 원시 좌표) → join_armatures로 몸 아마추어 head 밑에 합침.
    #   얼굴·눈썹·속눈썹·쌍꺼풀이 왼쪽(+X)에만 있는 건 원작(오른눈 띠) 그대로.
    #   뺀 것: 칼 '0.006'+무기 아마추어 · 표정 조각(tears·tere·siwa·shadow·kuma·땀 '0.003') · 외곽선 face_outline_BFC_00 ·
    #     텍스처가 **완전 투명 8×8**인 조각(eye_RL_00/01 · 이 조각 '0.004' · 눈동자 덮개 face_eye_L_pupil_FIX) — 불투명이면 흰 덩어리 · 빈 부속 아마추어 a~e.
    #   머리카락 hair01_00_mat_0의 이미지(1024×256 반 검정·반 빨강)는 색이 아니라 마스크 → 단색 짙은 갈색(얼굴 아틀라스 머리색).
    "초월_최상호_AP": dict(path="Assets/Art/Units/초월_최상호_AP/초월_최상호_AP.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "08_초월/초월_최상호_AP.zip"), "source/Sketchfab_2026_09_14_03_41_39.glb"),
                      no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere"] + [f"Icosphere.00{i}" for i in range(1, 9)]
                                  + ["0.006", "0.003", "0.004", "tears_00", "tears_01", "tere", "siwa_00_ob", "siwa_01_ob", "siwa_free_ob",
                                     "shadow_00_ob", "shadow_01_ob", "kuma_ob", "face_outline_BFC_00", "eye_RL_00", "eye_RL_01", "face_eye_L_pupil_FIX"],
                      drop_objects=["pl020_wep00_00_00"] + [f"pl020_face01_00_objects_{c}" for c in "abcde"],
                      join_armatures=dict(main="pl020_cos04_00", attach={"pl020_face01_00": ("face:", "head"), "pl020_hair01_00": ("hair:", "head")}),
                      rename_bones=dict({"hips": "mixamorig:Hips", "lback": "mixamorig:Spine", "mback": "mixamorig:Spine1", "uback": "mixamorig:Spine2",
                                         "neck": "mixamorig:Neck", "head": "mixamorig:Head"},
                                        **{f"{b}_{p}": f"mixamorig:{side}{j}" for p, side in (("L", "Left"), ("R", "Right"))
                                           for b, j in (("clavicle", "Shoulder"), ("shoulder", "Arm"), ("elbow", "ForeArm"), ("hand", "Hand"),
                                                        ("groin", "UpLeg"), ("knee", "Leg"), ("ankle", "Foot"), ("toe", "ToeBase"))}),
                      drop_bones=["weapon_L", "weapon_R", "weapon_01", "weapon_02", "eye_L", "eye_R", "jaw"],
                      merge_bones=[dict(pattern=r"^face:neck$", into="mixamorig:Neck"),
                                   dict(under="mixamorig:Head", into="mixamorig:Head")]
                                  + [d for p, side in (("L", "Left"), ("R", "Right")) for d in (
                                      dict(pattern=rf"^shoulder_{p}_AS0[01]$", into=f"mixamorig:{side}Arm"),
                                      dict(pattern=rf"^elbow_{p}_AS0[0-3]$", into=f"mixamorig:{side}ForeArm"),
                                      dict(pattern=rf"^hand_{p}_AS0[01]$", into=f"mixamorig:{side}Hand"),
                                      dict(pattern=rf"^(groin_{p}_AS00|hip_{p}00)$", into=f"mixamorig:{side}UpLeg"),
                                      dict(pattern=rf"^(knee_{p}_AS0[01]|calf_{p}_AS00)$", into=f"mixamorig:{side}Leg"))]
                                  # 🔴 1회차: 코트 자락 전부 Hips면 Idle·무릎90°에서 넓적다리가 코트를 뚫는다 → 원본처럼 좌우 자락은 그쪽 넓적다리(groin 자식), 가운데 앞뒤만 Hips
                                  #   2회차: 좌우 자락 전부 넓적다리면 뒤 가운데 자락이 찢김(Idle 5.87 · 무릎90° 18.8) → 반반(share 0.5, 나머지 Hips)
                                  + [dict(pattern=r"^skirt_L_", into="mixamorig:LeftUpLeg", share=0.5, rest="mixamorig:Hips"),
                                     dict(pattern=r"^skirt_R_", into="mixamorig:RightUpLeg", share=0.5, rest="mixamorig:Hips"),
                                     dict(pattern=r"^skirt_C[BF]_", into="mixamorig:Hips"),
                                     dict(pattern=r"^mback_sway_", into="mixamorig:Spine1"),
                                     dict(pattern=r"^(uback_sway_|eri_)", into="mixamorig:Spine2")],
                      # 🔴 1회차: 몸 옷까지 ×0.25로 깎으니 코트에 밝은 네모 얼룩(반 흰·반 검정 아틀라스 cos04_26의 UV 이음새가 무너짐) → 머리카락·이만 깎는다
                      decimate=dict(min_tris=1000, ratio=1.0, by_mesh={"0.005": 0.25, "tooth_up": 0.25, "tooth_under": 0.25}),
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm",
                                      "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      seed_zero_bones=0.001,
                      glb_images={0: "pl020_cos04_00_0.png", 1: "pl020_cos04_00_7.png", 2: "pl020_cos04_00_14.png", 3: "pl020_cos04_00_18.png",
                                  4: "pl020_cos04_00_26.png", 5: "pl020_cos04_00_30.png", 6: "pl020_face01_00_0.png", 7: "pl020_face01_00_6.png",
                                  15: "pl020_hair01_00_3.png"},
                      solid_textures={"pl020_hair01_00_mat_0": (0.08, 0.052, 0.045)},
                      materials=dict(textures={
                          **{f"pl020_cos04_00_mat_{i}": [("DiffuseColor", f"pl020_cos04_00_{n}.png")] for i, n in enumerate((0, 7, 14, 18, 26, 30))},
                          "pl020_face01_00_mat_1": [("DiffuseColor", "pl020_face01_00_0.png")],
                          "pl020_face01_00_mat_4": [("DiffuseColor", "pl020_face01_00_0.png")],
                          "pl020_face01_00_mat_0": [("DiffuseColor", "pl020_face01_00_6.png")],
                          "pl020_hair01_00_mat_1": [("DiffuseColor", "pl020_hair01_00_3.png")]})),
    # 블리치 ROS 아이젠 소스케(Sketchfab glb) → 전설적인_최상호(2026-09-17 전설적인 1호). 그림죠(희귀함_박기찬)와 같은 pl0xx 리그 — 처리 그대로.
    #   애니 0 · glTF 결합 추정 켜나 끄나 자세 차 0 · A자 → tpose_arms. 158k 삼각형 → 감량.
    #   숨김 변형 `5_-Sword`(Object_11, 오른손 weapon_R 밑 wep00_00_00 사슬)는 뼈째 버리고, 허리에 찬 칼집·손잡이(wep00_00_01 · waist_weapon)는 남겨 칼 찬 인상 유지.
    #   머리카락 기본색은 16² 단색 이미지라 UV 둘 중 어느 쪽이든 색이 같다. 이미지 알파는 속눈썹(9)만 0.49~1이고 99.7% 이진 → 그대로.
    "전설적인_최상호": dict(path="Assets/Art/Units/전설적인_최상호/전설적인_최상호.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "06_전설적인/전설적인_최상호.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere", "Object_11"],
                      drop_bones=['pl020_wep00_00_00_body_356', 'pl020_wep00_00_00_blade00_354', 'pl020_wep00_00_00_blade01_353', 'pl020_wep00_00_00_blade02_352', 'pl020_wep00_00_00_blade03_351', 'pl020_wep00_00_00_blade04_350', 'pl020_wep00_00_00_bottom_355', 'pl020_cos02_00_weapon_R_357', 'pl020_cos02_00_weapon_L_407', 'root ground_588', 'GLTF_created_0_rootJoint'],
                      rename_bones=AIZEN_RENAME,
                      # 🔸 이 리그는 위팔·아래팔 살이 매핑 뼈가 아니라 자식 보조 뼈(shoulder/elbow AS·sway)에 실려 매핑 뼈 가중치가 0이다(그림죠도 같았음).
                      #   보조 뼈는 유니티에서 안 움직이는 딸린 자식이라 매핑 뼈로 합쳐도 변형이 같다 → 합쳐서 가중치 0 매핑 뼈·가중치 0 sway 부모를 없앤다.
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")]
                                  + [dict(pattern=rf"^pl020_cos02_00_{j}_{s}_(AS|sway)", into=f"mixamorig:{side}{b}")
                                     for s, side in (("L", "Left"), ("R", "Right"))
                                     for j, b in (("shoulder", "Arm"), ("elbow", "ForeArm"), ("groin", "UpLeg"), ("knee", "Leg"))]
                                  + [dict(pattern=r"^pl020_cos02_00_thorax_sway", into="pl020_cos02_00_thorax_468"),
                                     # 가중치 0 중간 뼈(옷깃 eri_*00 · 칼집 holder): 지워도 자식이 윗 뼈로 붙을 뿐 변형 그대로
                                     dict(pattern=r"^pl020_cos02_00_(eri_[LR]_[A-F]00|holder_00)_", into="pl020_cos02_00_thorax_468")],
                      decimate=dict(min_tris=1000, ratio=0.25),
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm",
                                      "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "weapon_baseColor.png", 3: "cloak_baseColor.png", 5: "cloth_baseColor.png", 7: "eyebrows_baseColor.png",
                                  9: "eyelashes_baseColor.png", 10: "face_baseColor.png", 11: "feet_baseColor.png", 13: "hair_baseColor.png",
                                  15: "hakama_baseColor.png", 17: "skin_baseColor.png"},
                      materials=dict(textures={
                          "5_Hilt_1.0_0_0": [("DiffuseColor", "weapon_baseColor.png")],
                          "5_scabbard_1.0_0_0": [("DiffuseColor", "weapon_baseColor.png")],
                          "5_wep_1.0_0_0": [("DiffuseColor", "weapon_baseColor.png")],
                          "5_cloak_1.0_0_0": [("DiffuseColor", "cloak_baseColor.png")],
                          "5_cloth_1.0_0_0": [("DiffuseColor", "cloth_baseColor.png")],
                          "5_eyebrows_1.0_0_0.001": [("DiffuseColor", "eyebrows_baseColor.png")],
                          "5_eyelashes_1.0_0_0.001": [("DiffuseColor", "eyelashes_baseColor.png")],
                          "7_eyeshadow_1.0_0_0.001": [("DiffuseColor", "eyelashes_baseColor.png")],
                          "5_eyelines_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_eyes_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_eyewhite_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_face_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_mouth_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_noselines_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_teeth_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_tongue_1.0_0_0.001": [("DiffuseColor", "face_baseColor.png")],
                          "5_feet_1.0_0_0": [("DiffuseColor", "feet_baseColor.png")],
                          "5_hair_1.0_0_0": [("DiffuseColor", "hair_baseColor.png")],
                          "5_hakama_1.0_0_0": [("DiffuseColor", "hakama_baseColor.png")],
                          "5_skin_1.0_0_0": [("DiffuseColor", "skin_baseColor.png")]})),
    # 주술회전 히구루마 히로미 + 식신 저지맨(JJBTS 추출 FBX 7400, 3ds Max) → 전설적인_노태현(2026-09-17 전설적인 2호). zip 안 source/JJBTSJudgeman.fbx + textures/415202X.png(1024² RGB).
    #   아마추어 둘: `Suit`(사람, 3ds Max Biped **Bip001** 90뼈 — 이름이 익명인 Bone0xx는 저지맨 쪽 `Juj` 41뼈와 얼굴 보조뼈뿐) · `Juj`(저지맨).
    #   🔴 사람 메시 넷(Body·Hair·Skin·Gavel)은 Bip001 정점 그룹이 다 있는데 가져오기에서 **아마추어 모디파이어·부모가 없다** → bind_unskinned="Suit".
    #   🔴 저지맨(원시 폭 2.54 m, 뒤에 뜬 식신)은 사람 경계를 부풀리니 메시·아마추어째 버림. 의사봉 `5_-Gavel`(`-` = 숨김 변형, 오른손 밑 Bone044 100%)도 버림.
    #   얼굴 보조뼈 Bone029~043 → Head. 넥타이 Dummy005(Spine2 밑, 가중치 273)는 그대로. 가중치 0 뿌리(415052·Bip001)·_end 뼈 뺌.
    "전설적인_노태현": dict(path="Assets/Art/Units/전설적인_노태현/전설적인_노태현.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_노태현.zip"), "source/JJBTSJudgeman.fbx"),
                      archive_rgb={"textures/415202X.png": "415202X.png"},
                      no_nulls=True, orient_snap=True,
                      drop_meshes=["5_Judgeman_1_0_0", "5_-Gavel_1_0_0"], drop_objects=["Juj", "Camera"],
                      bind_unskinned="Suit",
                      rename_bones=biped_rename("Bip001", fingers=5, joints=3, spine2="Spine2"),
                      drop_bones=["415052", "Bip001", "Bone044"], drop_bones_re=r"_end$",
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")],
                      tpose_arms=biped_tpose_names(fingers=5, joints=3),
                      materials=dict(textures={k: [("DiffuseColor", "415202X.png")] for k in ("Body", "hair", "head")})),
    # 블리치 자라키 켄파치(모바일 게임 립 FBX 7300, 3ds Max Biped Bip01) → 전설적인_양재모(2026-09-17 전설적인 3호). 하나타로(희귀함_현성현)와 같은 추출본 — 처리 그대로.
    #   뼈 66 · 메시 2 · 재질 1 · 텍스처 256² · FBX ×0.01. 손가락 셋 × 두 마디.
    #   🔴 둘째 메시 wp1_0(260정점, 오른손 밑 weapon_01 사슬)은 쥔 칼이 아니라 **몸 옆에 세워 둔 칼**(원본 앞 렌더 kenpachi/src_front.png) → 칼·뼈 뺀다.
    #   보조 뼈(아이젠 방식 — 유니티에선 안 움직이는 딸린 자식이라 합쳐도 변형 같음): Bone_leg → Leg · Bone_arm → ForeArm · 옷자락 Bone_cloth → Hips · Head 밑(눈·머리·입·안대) → Head.
    #   FBX가 부르는 _rgb1/_rgb2(색 교체 마스크)는 zip에 없다 — 기본색 png만.
    "전설적인_양재모": dict(path="Assets/Art/Units/전설적인_양재모/전설적인_양재모.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_양재모.zip"), "source/cha_kenpachi_general.fbx"),
                      archive_rgb={"textures/cha_kenpachi_general.png": "cha_kenpachi_general.png"},
                      no_nulls=True, orient_snap=True, use_rest_pose=True, drop_meshes=["wp1_0"],
                      rename_bones=biped_rename("Bip01", fingers=3, joints=2),
                      drop_bones=["cha_kenpachi_general", "Bip01", "cha_kenpachi_general.001", "wp1", "weapon_01", "Bone_weapon_01", "Bone_weapon_02"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bone_leg_L_", into="mixamorig:LeftLeg"), dict(pattern=r"^Bone_leg_R_", into="mixamorig:RightLeg"),
                                   dict(pattern=r"^Bone_arm_L_", into="mixamorig:LeftForeArm"), dict(pattern=r"^Bone_arm_R_", into="mixamorig:RightForeArm"),
                                   dict(pattern=r"^Bone_cloth_", into="mixamorig:Hips")],
                      tpose_arms=biped_tpose_names(fingers=3, joints=2),
                      materials=dict(textures={"cha_kenpachi_general": [("DiffuseColor", "cha_kenpachi_general.png")]})),
    # 킹콩(2005, Sketchfab-17.15 glb) → 전설적인_임채현(2026-09-17 전설적인). 뼈 169(glTF 번호 꼬리) · 메시 6 + 조명 Icosphere · 애니 0 · 삼각형 148,842.
    #   🔴 쉬는(결합) 자세는 **누워 있다**(Y위 결합) — 기본 자세가 선 A자라 use_rest_pose 쓰지 않는다(kong/default_front vs rest_front).
    #   다리는 pelvis 밑(사스케와 달리 정상)인데 🔴 **spine lower가 pelvis 형제**(root hips 밑) → Spine을 Hips 밑으로 다시 붙인다.
    #   보조 뼈(아이젠 방식 합치기): skin belly lower → Hips · skin belly upper → Spine · spine middle 2(매핑 안 한 넷째 척추)는 그대로 ·
    #     skin torso pec → Spine2 · skin torso back/pit → Shoulder · skin arm upper a/b → Arm · skin arm lower → ForeArm · 발가락 toe a~e → ToeBase · Head 밑 얼굴 전부 → Head.
    #   🔴 털 카드 4개(재질 25_Fur*, BLEND · baseColorFactor 알파 0.303)는 **텍스처 알파가 없다**(이미지 6장 전부 알파 1.0 — 1·3·5는 노멀) → 컷아웃 마스크 없음, 반투명은 재질 상수뿐.
    #   UV: 5층 중 glTF texCoord 0(첫 층)이 진짜 — 털·눈은 u 1~3(반복 샘플러로 감김), 몸은 0~1. 나머지 층은 내보내기에서 첫 층만 남긴다.
    "전설적인_임채현": dict(path="Assets/Art/Units/전설적인_임채현/전설적인_임채현.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "06_전설적인/전설적인_임채현.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere"], first_uv_only=True,
                      decimate=dict(min_tris=1000, ratio=0.5, by_mesh={"Object_185": 0.3}),
                      rename_strip=r"_\d+$", rename_bones=KONG_RENAME,
                      reparent_bones={"mixamorig:Spine": "mixamorig:Hips"},
                      drop_bones=["_rootJoint", "root ground_02", "root hips_03"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^skin belly lower", into="mixamorig:Hips"),
                                   dict(pattern=r"^skin belly upper", into="mixamorig:Spine"),
                                   dict(pattern=r"^skin torso pec", into="mixamorig:Spine2")]
                                  + [d for s, side in (("left", "Left"), ("right", "Right")) for d in (
                                      dict(pattern=rf"^skin torso (back|pit) {s}", into=f"mixamorig:{side}Shoulder"),
                                      dict(pattern=rf"^skin arm {s} upper", into=f"mixamorig:{side}Arm"),
                                      dict(pattern=rf"^skin arm {s} lower", into=f"mixamorig:{side}ForeArm"),
                                      dict(pattern=rf"^leg {s} toe [a-e] ", into=f"mixamorig:{side}ToeBase"))],
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "kong_eyes_baseColor.jpg", 2: "kong_fur_baseColor.png", 4: "kong_body_baseColor.jpg"},
                      materials=dict(textures={"38_Eyes_1_0_0": [("DiffuseColor", "kong_eyes_baseColor.jpg")],
                                               "24_Body_0.5_0_0": [("DiffuseColor", "kong_body_baseColor.jpg")],
                                               **{f"25_Fur{k}_0.5_0_0": [("DiffuseColor", "kong_fur_baseColor.png")] for k in ("Legs", "Head", "Body", "Arms")}})),
    # 원펀맨 킹(팬 변환 King.fbx, UnitScale 100) → 전설적인_정윤식(2026-09-17 전설적인). zip 안 source/*.7z 안 King.fbx + 텍스처 원본(zip textures/는 재인코딩본이라 7z 쪽).
    #   아마추어 NULL(X 90°) · 뼈 136 · 메시 9 · 재질 7 · 삼각형 23k(감량 없음) · **이미 T자**(손목 z 1.61 = 어깨) · 기본 자세 = 결합 자세 · 정면 −Y.
    #   보조 뼈 합치기(유니티에선 딸린 자식): SHOULDERROLL(위팔 살 411정점) → Arm · ELBOWROLL → ForeArm · CLAVICLEROLL → Shoulder · EFFECT(4정점) → Hand ·
    #     CLANKROLL·CLANK_*C(종아리 살) → Leg · Head 밑(FACE2·눈·입·이·볼·HAIR_03) → Head. 가중치 0 뿌리 root_ground·CLOTH_A_00 뺌.
    #   텍스처 7장 전부 RGBA인데 알파 1.0 → archive_rgb. 마스크(_m_)·eye_shadow는 안 씀. Hair.Hair 메시는 HEAD 100%.
    "전설적인_정윤식": dict(path="Assets/Art/Units/전설적인_정윤식/전설적인_정윤식.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_정윤식.zip"),
                               "source/opm_a_hero_nobody_knows___king__xps__blend_fbx__by_eagle_31_ddt5.7z", "King.fbx"),
                      archive_rgb={f: f for f in ("body_c_0020.png", "leg_c_0020.png", "face_c_0020.png", "hair_c_0020.png", "hand_c_0020.png",
                                                  "tooth1_c.png", "eye_c_0020.001.png")},
                      no_nulls=True, orient_snap=True,
                      rename_bones=KING_RENAME,
                      drop_bones=["root_ground", "CLOTH_A_00"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")]
                                  + [d for p, side in (("L", "Left"), ("R", "Right")) for d in (
                                      dict(pattern=rf"^SHOULDERROLL_{p}", into=f"mixamorig:{side}Arm"),
                                      dict(pattern=rf"^ELBOWROLL_{p}$", into=f"mixamorig:{side}ForeArm"),
                                      dict(pattern=rf"^CLAVICLEROLL_{p}$", into=f"mixamorig:{side}Shoulder"),
                                      dict(pattern=rf"^EFFECT_{p}$", into=f"mixamorig:{side}Hand"),
                                      dict(pattern=rf"^(CLANKROLL_{p}|CLANK_{p}C)$", into=f"mixamorig:{side}Leg"))],
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      materials=dict(textures={"Body": [("DiffuseColor", "body_c_0020.png")], "Pants": [("DiffuseColor", "leg_c_0020.png")],
                                               "Face": [("DiffuseColor", "face_c_0020.png")], "Hair": [("DiffuseColor", "hair_c_0020.png")],
                                               "Hands": [("DiffuseColor", "hand_c_0020.png")], "Mouth": [("DiffuseColor", "tooth1_c.png")],
                                               "Eyes": [("DiffuseColor", "eye_c_0020.001.png")]})),
    # 원피스 바운티러시 제파(pl_zephyr_orig01 (merge)) → 전설적인_이시원(2026-09-17 전설적인). 레일리·시류와 같은 pl_ 리그 — zip 안 rar 안 FBX · 뼈 67 · 메시 18 · 재질 3 · ×0.01 · 기본 = 결합 자세.
    #   🔴 오른손 메시·RHand_Palm 뼈가 없다 — **배틀스매셔가 오른손 자체(의수)**: RArm_Fore 밑 weapon_root(손목 자리) 사슬에 실려 있고, 통이 앞뒤(y −0.016~+0.019)로 뻗는다.
    #     → 무기 규칙 예외로 남기고 weapon_root를 RightHand로 매핑(휴머노이드 필수 뼈), weapon_* 자식은 RightHand로 합침.
    #   🔴 다이나건 통(container·stone)은 스매셔 앞끝에 끼워져 있는데 뼈 dynagan_joint가 **world_joint 직속** → Idle에서 팔만 움직이면 공중에 남는다 → RightHand 밑으로 다시 붙이고 합침.
    #     dynagan_glass(투명 재질 _trans)는 불투명으로 나가면 통을 덮으니 뺀다. cartridge(뿌리 직속, 발밑 원점에 뜬 탄창)도 뺀다.
    #   겹친 변형: face_normal 남김(attack·damage·sp 뺌) · l_hand_open 남김(close 뺌) · battlesmasher_close 남김(open 뺌 — 집게 벌림이 가로를 키움).
    #   보조 뼈 합치기(PM 09-17 방침): coat_root 사슬·bodyparts → Spine1 · RArm_Clavicle 밑 코트·깃 → RightShoulder · L/RHand_Fore_sup → ForeArm · Inhaler_joint → Head.
    #   🔴 _diff 알파는 음영 마스크(최소 0.05 · 평균 0.755 · 100%가 0.98 미만), _dyanagan_diff도(0.61~1) → archive_rgb 둘 다.
    "전설적인_이시원": dict(path="Assets/Art/Units/전설적인_이시원/전설적인_이시원.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_이시원.zip"), "source/zephyr.rar", "zephyr/pl_zephyr_orig01 (merge).fbx"),
                      archive_rgb={"zephyr/pl_zephyr_orig01_diff.png": "pl_zephyr_orig01_diff.png",
                                   "zephyr/pl_zephyr_orig01_dyanagan_diff.png": "pl_zephyr_orig01_dyanagan_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "face_sp", "l_hand_close", "battlesmasher_open", "dynagan_glass", "cartridge"],
                      drop_bones=["cartridge_joint", "world_joint"],
                      rename_bones=dict({k: v for k, v in PL_RENAME.items() if k != "RHand_Palm"}, weapon_root="mixamorig:RightHand"),
                      reparent_bones={"dynagan_joint": "mixamorig:RightHand"},
                      no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^(weapon_|dynagan_)", into="mixamorig:RightHand"),
                                   dict(pattern=r"^(coat_root|b_c_coat_|c_collar|f_l_coat_|b_l_coat_|l_coat_arm_|l_collar|bodyparts_)", into="mixamorig:Spine1"),
                                   dict(pattern=r"^(f_r_coat_|r_coat_arm_|r_collar)", into="mixamorig:RightShoulder"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      materials=dict(textures={"pl_zephyr_orig01": [("DiffuseColor", "pl_zephyr_orig01_diff.png")],
                                               "pl_zephyr_orig01_dyanagan": [("DiffuseColor", "pl_zephyr_orig01_dyanagan_diff.png")]})),
    # 원피스 바운티러시 키자루(pl_kizaru_orig01) → 전설적인_김민준(2026-09-17 전설적인, ※ 동명 김민준 셋 중 전설적인). 레일리와 같은 pl_ 리그.
    #   zip 안 rar 안 FBX · 뼈 40 · 메시 24 · 재질 2(orig01 · _trans=glass_lens) · 전 메시 무가중치 0 · ×0.01 · 기본 = 결합 자세 · 이미 T자.
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · l/r_hand_open 남김(close·yasakani·yubisashi 뺌) · watch 남김(open_watch 뺌).
    #   🔴 빛 변신 부품 l/r_light_leg·l/r_light_shoes(leg·shoes와 같은 자리 겹침) 뺌. glass + glass_lens 유지(내보내기에서 불투명으로 못박음).
    #   보조 뼈 합치기: coat_root·c_coat·l/r_coat_kata·l/r_arm01~02(코트 소매 — 매핑 팔로 안 잡히게)·l/r_coat01~03 → Spine1 · susoA/B(바짓단) → Hips.
    #   🔴 _diff 알파는 음영 마스크(최소 0.09 · 평균 0.68 · 100%가 0.98 미만) → archive_rgb.
    "전설적인_김민준": dict(path="Assets/Art/Units/전설적인_김민준/전설적인_김민준.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_김민준.zip"), "source/pl_kizaru_orig01.rar", "pl_kizaru_orig01/pl_kizaru_orig01.fbx"),
                      archive_rgb={"pl_kizaru_orig01/pl_kizaru_orig01_diff.png": "pl_kizaru_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_yasakani", "r_hand_yasakani", "r_hand_yubisashi",
                                   "l_light_leg", "r_light_leg", "l_light_shoes", "r_light_shoes", "open_watch"],
                      drop_bones=["world_joint"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(pattern=r"^(coat_root|c_coat0|[lr]_coat_kata|[lr]_arm0|[lr]_coat0)", into="mixamorig:Spine1"),   # Head 자손 없음(Head 병합 불필요)
                                   dict(pattern=r"^suso[AB]$", into="mixamorig:Hips")],
                      materials=dict(textures={"pl_kizaru_orig01": [("DiffuseColor", "pl_kizaru_orig01_diff.png")],
                                               "pl_kizaru_orig01_trans": [("DiffuseColor", "pl_kizaru_orig01_diff.png")]})),
    # 원피스 바운티러시 우르즈(pl_urouge_orig01) → 전설적인_백기현(2026-09-17 전설적인). 레일리와 같은 pl_ 리그 — zip 안 rar 안 FBX · 뼈 63 · 메시 13 · 재질 1 · ×0.01 · 이미 T자.
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · l/r_hand_open 남김(close·l_hand_pray 뺌).
    #   🔴 weapon01(오른손 weapon_joint01)·weapon02(뿌리 직속 weapon_joint02) — 둘 다 앞뒤(y −0.019~+0.021)로 크게 뻗는 무기 → 뼈째 뺌.
    #   보조 뼈 합치기(PM 09-17 방침): 코트(coat_root·c_coat·F_coat01·l/r_coat_clavicle·l/r_coat01~02 — 어깨로 자동 매핑 안 되게) · 등 날개 Lwing/Rwing(스카이피아인 외형, 유지) ·
    #     묵주 rosary_01~05 · ribbon → Spine1 · 치마 BL/BR/FL/FR/SL/SR skirt(🔴 temp_FRskirt_02 이름 불규칙) → Hips · beard_joint(Head 자손) → Head · L/RHand_Fore_sup → ForeArm.
    #   🔴 _diff 알파는 음영 마스크(0.02 미만은 0.03%뿐 — 안 쓰는 검은 텍셀, 나머지 0.2~1 분포) → archive_rgb.
    "전설적인_백기현": dict(path="Assets/Art/Units/전설적인_백기현/전설적인_백기현.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_백기현.zip"), "source/pl_urouge_orig01.rar", "pl_urouge_orig01/pl_urouge_orig01.fbx"),
                      archive_rgb={"pl_urouge_orig01/pl_urouge_orig01_diff.png": "pl_urouge_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_pray", "weapon01", "weapon02"],
                      drop_bones=["weapon_joint01", "weapon_joint02", "world_joint"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^(coat_root|c_coat|F_coat|[lr]_coat|[LR]wing_|rosary_|ribbon_)", into="mixamorig:Spine1"),
                                   dict(pattern=r"^(temp_)?[BFS][LR]skirt_", into="mixamorig:Hips"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      materials=dict(textures={"pl_urouge_orig01": [("DiffuseColor", "pl_urouge_orig01_diff.png")]})),
    # 원피스 바운티러시 샹크스 사황(팬 재내보내기 「Shanks Yonko by Annettlw」, pl_shanks_hand01) → 전설적인_이일중(2026-09-17 전설적인).
    #   zip 안 rar(🔴 이름에 공백) 안 FBX · 아마추어 이름 「Armature」 · 뿌리 pl_shanks_hand01 → world_joint · 뼈 51 · 메시 11 · 재질 1 · ×0.01 · 이미 T자.
    #   🔸 **왼팔이 없는 게 원작**(l_hand 메시 없음) — LArm_Fore·LHand_Fore_sup·LHand_Palm 가중치 0. 손을 만들지 않고 seed_zero_bones로 매핑만 살린다(오비토 교훈).
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · r_hand_open 남김(close 뺌). weapon_01(오른손 RHand_Weapon 칼) 뺌.
    #   weapon_02(칼집, sword_sheath_joint)·weapon_03(칼집에 꽂힌 자루, sword_handle_sheath_joint)은 허리에 찬 것이라 유지(아이젠 기준) → 뼈는 Hips로 합침.
    #   보조 뼈 합치기: 망토 Cort_Top·C/F/L/R_Cort01~03·F_Cort01_sup · C/L/R_Collar(어깨 오매핑 후보) → Spine1 · LHair/RHair → Head · RArm_Upper_sup → RightArm ·
    #     L/RHand_Fore_sup → ForeArm. 가중치 0 HELPER·HELPER_key·HELPER_name·world_joint·pl_shanks_hand01 뺌. _diff 알파는 전부 1.0.
    "전설적인_이일중": dict(path="Assets/Art/Units/전설적인_이일중/전설적인_이일중.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_이일중.zip"), "source/Shanks Yonko.rar", "Shanks Yonko/Shanks Yonko by Annettlw.fbx"),
                      archive_rgb={"Shanks Yonko/pl_shanks_hand01_diff.png": "pl_shanks_hand01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "r_hand_close", "weapon_01"],
                      drop_bones=["RHand_Weapon", "HELPER", "HELPER_key", "HELPER_name", "world_joint", "pl_shanks_hand01"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^(Cort_Top|[CFLR]_Cort0|[CLR]_Collar)", into="mixamorig:Spine1"),
                                   dict(pattern=r"^sword_", into="mixamorig:Hips"),
                                   dict(pattern=r"^RArm_Upper_sup$", into="mixamorig:RightArm"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_shanks_hand01": [("DiffuseColor", "pl_shanks_hand01_diff.png")]})),
    # 원피스 바운티러시 후지토라(잇쇼, pl_fujitora_orig01) → 전설적인_임건웅(2026-09-17 전설적인). 샹크스형 pl_ 리그(아마추어 「Armature」·뿌리 pl_fujitora_orig01 → world_joint).
    #   zip 안 rar 안 FBX · 뼈 68 · 메시 13 · 재질 1 · ×0.01 · 이미 T자.
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · l/r_hand_open 남김(close 뺌).
    #   🔴 무기 메시 이름 오타 `weopen_01~04` — 넷 다 손 뼈(weapon_01_joint 오른손 · weapon_02_joint 왼손)에 실려 손에서 앞뒤(y)로 수평으로 뻗는다
    #     (01 뒤로 0.023 · 04 뒤로 0.01 · 02 뒤로 0.022 · 03 앞으로 0.003, 지팡이칼·칼집) → 시류 기준으로 넷 다 뺌 + 뼈.
    #   보조 뼈 합치기: 코트 coat_root·c_coat·l/r_coat·l/r_coat01~03·l/r_coat_arm01~02 → Spine1 · 치마 BL/BR/FL/FR/SL/SR skirt1~3 → Hips ·
    #     소매 L/R_sode01~02(Fore 밑 — 팔 자동 매핑 후보) · L/RHand_Fore_sup → ForeArm. 가중치 0 HELPER_name·world_joint·뿌리 뺌.
    #   🔴 _diff 알파는 음영 마스크(최소 0.43 · 평균 0.84 · 98%가 0.98 미만) → archive_rgb.
    "전설적인_임건웅": dict(path="Assets/Art/Units/전설적인_임건웅/전설적인_임건웅.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_임건웅.zip"), "source/pl_fujitora_orig01.rar", "pl_fujitora_orig01/pl_fujitora_orig01.fbx"),
                      archive_rgb={"pl_fujitora_orig01/pl_fujitora_orig01_diff.png": "pl_fujitora_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "weopen_01", "weopen_02", "weopen_03", "weopen_04"],
                      drop_bones=["weapon_01_joint", "weapon_02_joint", "HELPER_name", "world_joint", "pl_fujitora_orig01"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(pattern=r"^(coat_root|c_coat|[lr]_coat)", into="mixamorig:Spine1"),
                                   dict(pattern=r"^[BFS][LR]skirt[123]$", into="mixamorig:Hips"),
                                   dict(pattern=r"^(L_sode0|LHand_Fore_sup)", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^(R_sode0|RHand_Fore_sup)", into="mixamorig:RightForeArm")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_fujitora_orig01": [("DiffuseColor", "pl_fujitora_orig01_diff.png")]})),
    # 원피스 바운티러시 빅맘(샬롯 링링, pl_bigmom_orig01) → 전설적인_신지우(2026-09-17 전설적인, ※ 불멸_신지우와 동명). 레일리형 pl_ 리그.
    #   zip 안 rar 안 FBX · 뼈 41 · 메시 13 · 재질 1 · ×0.01 · 이미 T자 · 무기 메시 없음. 🔸 거구(원시 몸 폭 ±0.047 · 키 0.099).
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · l/r_hand_open 남김(close·sp_open 뺌).
    #   보조 뼈 합치기: 망토 b/l_cloak_01~03 · 🔴 오른 망토 이름 오타 r_croak_01~03 → Spine1 · 치마 bl/br/fl/fr_skirt → Hips · Head 자손(b/l/r_hair·Head_chin) → Head ·
    #     L/RHand_Fore_sup → ForeArm. 🔴 _diff 알파는 음영 마스크(최소 0.08 · 평균 0.85 · 100%가 0.98 미만) → archive_rgb.
    "전설적인_신지우": dict(path="Assets/Art/Units/전설적인_신지우/전설적인_신지우.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_신지우.zip"), "source/pl_bigmom_orig01.rar", "pl_bigmom_orig01/pl_bigmom_orig01.fbx"),
                      archive_rgb={"pl_bigmom_orig01/pl_bigmom_orig01_diff.png": "pl_bigmom_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_sp_open", "r_hand_sp_open"],
                      drop_bones=["world_joint"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^([bl]_cloak_0|r_croak_0)", into="mixamorig:Spine1"),
                                   dict(pattern=r"^(bl|br|fl|fr)_skirt$", into="mixamorig:Hips"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_bigmom_orig01": [("DiffuseColor", "pl_bigmom_orig01_diff.png")]})),
    # 블리치 메노스 그란데(모바일 게임 립 cha_menosgrande_R_6, 3ds Max Biped) → 전설적인_임장혁(2026-09-17 전설적인, ※ 동명 임장혁 넷 중 전설적인). 🔸 사람형 아님 → Generic.
    #   zip 안 source/*.fbx + textures/*.png(256² RGBA, 알파 전부 1.0 · FBX가 부르는 _rgb1은 zip에 없음). 메시 1 · 정점 2,395 · 재질 1 · UV 「Attribute」 · 무가중치 0 · ×0.01.
    #   형태: 팔·다리 없는 원추형 검은 망토 + 긴 코 가면. 뼈 11: 뿌리 cha_menosgrande_R_6 → Bip01 → Skirt·Skirt1(자락) / spine·spine1·spine2·Head·Point001·Mouse(코끝) · .001.
    #   🔴 원본 클립 없음 → synth_idle로 Idle 루프를 지음(3초·72프레임: 척추 좌우 흔들림 누적 · 머리 끄덕임 · 자락 반대로 살짝).
    #   가중치 0 뿌리 cha_menosgrande_R_6 · .001 · Point001(Mouse는 Head 밑으로) 뺌. Bip01은 뿌리 하나로 남기고 seed_zero_bones.
    #   방향: kind beast — 머리 쪽 = Mouse(코끝, −y) ↔ Head.
    "전설적인_임장혁": dict(path="Assets/Art/Units/전설적인_임장혁/전설적인_임장혁.fbx", kind="beast", generic=True, size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_임장혁.zip"), "source/cha_menosgrande_R_6.fbx"),
                      archive_rgb={"textures/cha_menosgrande_R_6.png": "cha_menosgrande_R_6.png"},
                      no_nulls=True, orient_snap=True, head="Mouse", tail="Head",
                      drop_bones=["cha_menosgrande_R_6", "cha_menosgrande_R_6.001", "Point001"],
                      seed_zero_bones=0.001,
                      synth_idle=dict(take="Idle", frames=72, step=3, bones={
                          "spine": [((0, 1, 0), 1.5, 0.0), ((1, 0, 0), 0.8, 1.2)],
                          "spine1": [((0, 1, 0), 1.5, -0.4)],
                          "spine2": [((0, 1, 0), 1.5, -0.8), ((1, 0, 0), 0.8, 0.4)],
                          "Head": [((1, 0, 0), 2.5, -1.2), ((0, 0, 1), 2.0, 0.6)],
                          "Skirt": [((0, 1, 0), -0.8, -0.6)],
                          "Skirt1": [((0, 1, 0), -1.0, -1.2)]}),   # 1회차 자락 −1.5·−2.0°는 옷단이 바닥 밑 2.4 cm로 내려감 → 절반
                      materials=dict(textures={"cha_menosgrande_R_6": [("DiffuseColor", "cha_menosgrande_R_6.png")]})),
    # 원피스 바운티러시 바솔로뮤 쿠마(pl_bkuma_orig01) → 전설적인_박은석(2026-09-17 전설적인, ※ 동명 박은석 중 전설적인). 샹크스형 pl_ 리그(「Armature」·뿌리 pl_bkuma_orig01 → world_joint).
    #   zip 안 rar 안 FBX · 뼈 29 · 메시 14 · 재질 1 · ×0.01 · 이미 T자 · 🔸 거구(원시 몸 폭 ±0.045 · 키 0.07).
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · 손 다섯 벌 중 **발바닥 장갑 open_gloves**(원작 상징) 남김 — open · close · close_gloves · close_gloves_02 뺌.
    #   book(왼손 LHand_Palm 100%, 손바닥 위 성경책)은 원작 상징 소품이라 유지.
    #   보조 뼈: hair1~3(Head_Neck 밑, Head 자손 아님) → Head. 가중치 0 HELPER·HELPER_key·HELPER_name·world_joint·뿌리 뺌.
    #   🔴 _diff 알파는 음영 마스크(최소 0.07 · 평균 0.56 · 100%가 0.98 미만) → archive_rgb.
    "전설적인_박은석": dict(path="Assets/Art/Units/전설적인_박은석/전설적인_박은석.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_박은석.zip"), "source/pl_bkuma_orig01.rar", "pl_bkuma_orig01/pl_bkuma_orig01.fbx"),
                      archive_rgb={"pl_bkuma_orig01/pl_bkuma_orig01_diff.png": "pl_bkuma_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_open", "r_hand_open", "l_hand_close", "r_hand_close",
                                   "l_hand_close_gloves", "r_hand_close_gloves", "l_hand_close_gloves_02"],
                      drop_bones=["HELPER", "HELPER_key", "HELPER_name", "world_joint", "pl_bkuma_orig01"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(pattern=r"^hair[123]$", into="mixamorig:Head")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_bkuma_orig01": [("DiffuseColor", "pl_bkuma_orig01_diff.png")]})),
    # 원피스 바운티러시 카이도(pl_kaido_orig01) → 전설적인_김용태(2026-09-17 전설적인, ※ 동명 김용태 중 전설적인). 샹크스형 pl_ 리그(「Armature」·뿌리 → world_joint · HELPER_key/name).
    #   zip 안 rar 안 FBX · 뼈 63 · 메시 14 · 재질 1 · ×0.01 · 이미 T자 · 🔸 거구(원시 몸 폭 ±0.055 · 머리카락 z 0.1).
    #   겹친 변형: face_normal 남김(attack·damage 뺌) · l/r_hand_open 남김(close·par 뺌).
    #   🔴 kanabo(오른손 kanabo_joint, 원시 y −0.073까지 앞으로 크게 뻗는 쇠몽둥이) 뺌. hyotan(술 호리병)은 허리가 아니라 **오른손 밑 hyotan_01~03**에 매달려
    #     손 바깥(x −0.085, 손끝 −0.07)으로 가로만 늘린다 → 뺌 + 뼈.
    #   보조 뼈 합치기: 코트 coat_root·c/l/r_coat01~04·l/r_coat_kata → Spine1 · Head 자손(b/l/r_hair·l/r_Beard_01~04) → Head ·
    #     l/r_leg_sup_01~02(Pelvis 직속 바지 보조)·l/r_shide(시메나와 종이 장식) → Hips · L/RHand_Fore_sup → ForeArm.
    #   🔴 _diff 알파는 음영 마스크(평균 0.71 · 99.6%가 0.98 미만, 0 근처는 안 쓰는 텍셀) → archive_rgb.
    "전설적인_김용태": dict(path="Assets/Art/Units/전설적인_김용태/전설적인_김용태.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_김용태.zip"), "source/pl_kaido_orig01.rar", "pl_kaido_orig01/pl_kaido_orig01.fbx"),
                      archive_rgb={"pl_kaido_orig01/pl_kaido_orig01_diff.png": "pl_kaido_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_par", "r_hand_par", "kanabo", "hyotan"],
                      drop_bones=["kanabo_joint", "hyotan_03", "hyotan_02", "hyotan_01", "HELPER_key", "HELPER_name", "world_joint", "pl_kaido_orig01"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^(coat_root|[clr]_coat0|[lr]_coat_kata)", into="mixamorig:Spine1"),
                                   dict(pattern=r"^([lr]_leg_sup_0|[lr]_shide)", into="mixamorig:Hips"),
                                   dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                      seed_zero_bones=0.001,
                      materials=dict(textures={"pl_kaido_orig01": [("DiffuseColor", "pl_kaido_orig01_diff.png")]})),
    # 블리치 이치마루 긴(모바일 게임 립 yinwanjie_0_battleout → Noesis glb, 3ds Max Biped Bip001) → 전설적인_이승우(2026-09-17 전설적인, ※ 동명 이승우 중 전설적인).
    #   채드(희귀함_구주호)와 같은 형식 — 뼈 42 · 메시 8 + 조명 Icosphere · 재질 7(전부 OPAQUE, 알파 1.0) · 애니 22 · 삼각형 4,167.
    #   🔴 애니 22개라 가져온 기본 자세가 클립 프레임(웅크린 전투 자세) → use_rest_pose(결합 자세 = 선 A자). 시험으로 idle 0프레임을 쉬는 자세로 구운 판(B)은
    #     정면 +31° 틀어지고 깊이 0.854로 퍼져 버림(gin/jA vs jB).
    #   🔴 몸 메시 둘(같은 재질 yin_body_0): yin_body_0(1,678정점, 코소데·하카마) + yin_body_0_c(645정점, 대장 하오리·검은 소매) — 겹친 사본이 아니라 겉옷 층이라 둘 다 유지.
    #   yin_weapon_0(칼 신소, 발밑에 누워 있음) + Bip001 Prop1·rweapon·yin_weapon_0 뼈 뺌. 가중치 0 뿌리 Bip001 뺌.
    #   얼굴 head·yin_leye/mouth/reye_0 → Head · Bone001~004(Pelvis 밑 하카마 자락) → Hips. 손가락 Finger0/1 두 마디.
    "전설적인_이승우": dict(path="Assets/Art/Units/전설적인_이승우/전설적인_이승우.fbx", kind="human", size=("height", 1.8), use_rest_pose=True,
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_이승우.zip"), "source/yinwanjie_0_battleout.glb"),
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere", "yin_weapon_0_noesis_meshnode_0003"],
                      rename_bones=biped_rename("Bip001", fingers=2, joints=2, spine2="Spine2"),
                      drop_bones=["Bip001", "Bip001 Prop1", "rweapon", "yin_weapon_0"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bone00[1-4]$", into="mixamorig:Hips")],
                      tpose_arms=biped_tpose_names(fingers=2, joints=2), seed_zero_bones=0.001,
                      glb_images={0: "yin_body_baseColor.png", 1: "yin_face_baseColor.png", 2: "yin_hair_baseColor.png",
                                  3: "yin_leye_baseColor.png", 4: "yin_mouth_baseColor.png", 5: "yin_reye_baseColor.png"},
                      materials=dict(textures={f"yin_{k}_0": [("DiffuseColor", f"yin_{k}_baseColor.png")]
                                               for k in ("body", "face", "hair", "leye", "mouth", "reye")})),
    # 블리치 사도 야스토라 — 거인의 오른팔(모바일 게임 립 cha_chad_arm, 3ds Max Biped Bip01) → 전설적인_구주호(2026-09-17 전설적인). 켄파치(전설적인_양재모)와 같은 cha_ 형식.
    #   뼈 63 · 메시 1(4,104정점) · 재질 1 · 텍스처 256²(알파 1.0, _rgb1/_rgb2 마스크는 zip에 없음) · ×0.01 · 손가락 5×3 · 다리는 켄파치처럼 Spine 밑.
    #   🔸 오른팔 거대 갑주가 원작 — 비대칭 그대로. 팔 내린 쉬는 자세 → use_rest_pose + tpose_arms.
    #   보조 뼈 합치기: Head 밑 eye·hair_B·hair_L/R01~02·Bone003 → Head · L/R_twist(Forearm 밑) → ForeArm. 가중치 0 뿌리 cha_chad_arm·Bip01·.001 뺌.
    "전설적인_구주호": dict(path="Assets/Art/Units/전설적인_구주호/전설적인_구주호.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_구주호.zip"), "source/cha_chad_arm.fbx"),
                      archive_rgb={"textures/cha_chad_arm.png": "cha_chad_arm.png"},
                      no_nulls=True, orient_snap=True, use_rest_pose=True,
                      rename_bones=biped_rename("Bip01", fingers=5, joints=3),
                      drop_bones=["cha_chad_arm", "Bip01", "cha_chad_arm.001"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^L_twist$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^R_twist$", into="mixamorig:RightForeArm")],
                      tpose_arms=biped_tpose_names(fingers=5, joints=3), seed_zero_bones=0.001,
                      materials=dict(textures={"cha_chad_arm": [("DiffuseColor", "cha_chad_arm.png")]})),
    # 블리치 시바 간쥬(모바일 게임 립 cha_ganzu, 3ds Max Biped Bip01) → 전설적인_박성호(2026-09-17 전설적인). 채드 거인의 오른팔(전설적인_구주호)과 같은 cha_ 형식.
    #   뼈 46 · 메시 2 · 재질 1 · 텍스처 256²(알파 1.0) · ×0.01 · 손가락 3×2 · 팔 내린 쉬는 자세.
    #   🔴 cha_ganzu_B_123_bomb_0(64정점, 오른손 Bone_weapon_02에 쥔 폭탄 소품) 뺌 + 뼈. 등에 멘 칼(Point_knife·Bone_weapon_01)·북(Bone_drum)은 몸 메시에 박혀 있어
    #     앞뒤로 안 튀어나옴(원시 깊이 0.006 · 키 0.022) → 유지하고 Pelvis 밑 보조 뼈 Bone_cloth_01/02·Point_knife·Bone_weapon_01·Bone_drum → Hips로 합침.
    #   Head 밑 Bone_eyes·Bone_head_01/02 → Head. 가중치 0 뿌리 cha_ganzu·Bip01·.001·cha_ganzu_B_123_bomb 뺌.
    "전설적인_박성호": dict(path="Assets/Art/Units/전설적인_박성호/전설적인_박성호.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_박성호.zip"), "source/cha_ganzu.fbx"),
                      archive_rgb={"textures/cha_ganzu.png": "cha_ganzu.png"},
                      no_nulls=True, orient_snap=True, use_rest_pose=True, drop_meshes=["cha_ganzu_B_123_bomb_0"],
                      rename_bones=biped_rename("Bip01", fingers=3, joints=2),
                      drop_bones=["cha_ganzu", "Bip01", "cha_ganzu.001", "cha_ganzu_B_123_bomb", "Bone_weapon_02"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^(Bone_cloth_0|Point_knife|Bone_weapon_01|Bone_drum)", into="mixamorig:Hips")],
                      tpose_arms=biped_tpose_names(fingers=3, joints=2), seed_zero_bones=0.001,
                      materials=dict(textures={"cha_ganzu": [("DiffuseColor", "cha_ganzu.png")]})),
    # 나루토 휴가 히나타(Free Fire 립 「hinata skin.fbx」, mixamorig 78뼈 완비) → 전설적인_이현주(2026-09-17 전설적인). 🔴 재빌드: 구현담당1 산출물이 유니티 Idle에서 양손이 손목에서
    #   바깥으로 꺾이고 손바닥이 밖. 원인: **좌우 이름이 뒤바뀐 거울 리그**(얼굴 −Y인데 LeftHand 뼈가 −X) → HINATA_SWAP으로 Left↔Right 이름 교환.
    #   메시 5(.rip) · 재질 4(HASHED, 노드 이름 .tga → zip png) · 텍스처 RGB(알파 없음) · 삼각형 약 1.46만 · 원본 뼈·가중치 그대로 · A자 → tpose_arms.
    #   가중치 0 끝 뼈(HeadTop_End·Toe_End·손가락 4번·*_end) 뺌. 텍스처 파일 이름은 구현담당1 이름 그대로(.meta 유지).
    "전설적인_이현주": dict(path="Assets/Art/Units/전설적인_이현주/전설적인_이현주.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "06_전설적인/전설적인_이현주.zip"), "source/hinata skin.fbx"),
                      archive_rgb={"textures/bottom_hhhh.png": "bottom_hhhh.png", "textures/top_hhh.png": "top_hhh.png", "textures/head_hhhhh.png": "head_hhhhh.png"},
                      no_nulls=True, orient_snap=True,
                      rename_bones=HINATA_SWAP,
                      drop_bones_re=r"(_end|_End|HandThumb4|HandIndex4|HandMiddle4|HandRing4|HandPinky4)$",
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      seed_zero_bones=0.001,
                      materials=dict(textures={"Material.009": [("DiffuseColor", "bottom_hhhh.png")], "Material.012": [("DiffuseColor", "bottom_hhhh.png")],
                                               "Material.010": [("DiffuseColor", "head_hhhhh.png")], "Material.011": [("DiffuseColor", "top_hhh.png")]})),
    # 원피스 바운티러시 빈스모크 이치지(pl_ichiji_orig01 (merge)) → 제한_최영민(2026-09-17 제한됨 1호). 시류형 pl_ 리그 — zip 안 rar 안 FBX · 뼈 67 · 메시 16 · 재질 1 · ×0.01 · 이미 T자 · 무기 없음.
    #   겹친 변형: face_normal 남김(attack·damage·sp01 뺌) · l/r_hand_open 남김(close·sp01·sp02 뺌) · glasses 유지.
    #   보조 뼈 합치기: 코트 coat_root·b_c/b_l/b_r_coat·f_coat·l/r_coat·l/r_coat_shoulder·b/f/l/r_collar(어깨 오매핑 후보)·목도리 scarf_* → Spine1 ·
    #     머리 b/f_hair_01(Head 자손) → Head · 자락 b/f_l/r_suso·l/r_suso·belt_01 → Hips · L/RHand_Fore_sup → ForeArm.
    #   🔴 _diff 알파는 음영 마스크(최소 0.03 · 평균 0.815 · 100%가 0.98 미만) → archive_rgb.
    "제한_최영민": dict(path="Assets/Art/Units/제한_최영민/제한_최영민.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.join(SKINS, "07_제한됨/제한_최영민.zip"), "source/ichiji.rar", "ichiji/pl_ichiji_orig01 (merge).fbx"),
                    archive_rgb={"ichiji/pl_ichiji_orig01_diff.png": "pl_ichiji_orig01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "face_sp01", "l_hand_close", "r_hand_close", "l_hand_sp01", "r_hand_sp01", "l_hand_sp02", "r_hand_sp02"],
                    drop_bones=["world_joint"],
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                 dict(pattern=r"^(coat_root|[bf]_c?_?[lr]?_?coat_0|f_coat_0|b_[clr]_coat_0|[lr]_coat_0|[lr]_coat_shoulder|[bflr]_collar|scarf_)", into="mixamorig:Spine1"),
                                 dict(pattern=r"^([bf]_[lr]_suso|[lr]_suso|belt_01)$", into="mixamorig:Hips"),
                                 dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                 dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                    seed_zero_bones=0.001,
                    materials=dict(textures={"pl_ichiji_orig01": [("DiffuseColor", "pl_ichiji_orig01_diff.png")]})),
    # 원피스 바운티러시 길드 테소로(pl_tesoro_orig01) → 제한_이유범(2026-09-18 제한됨 — 사장님이 도르도니→레그→테소로로 최종 교체). zip 안 source/*.7z 안 원본 FBX(9.3 MB · out.fbx 팬 재내보내기는 안 씀).
    #   pl_ 리그 뼈 43 · 메시 24 · 재질 2(기본·_trans) · 무가중치 0 · 쉬는 자세 = 오른손 내민 서 있는 자세(T자 아님) → tpose_arms.
    #   겹친 변형: face_normal · l/r_hand_open 남김(attack·damage · close · sp_01~04 뺌).
    #   안경: glass(머리에 올린 안경 — 이름에 _set 없는 기본) 남김 · glass_set(눈에 쓴 안경)·glass_lenz_set 뺌(렌더 tesoro/g_cmp.png). 렌즈 glass_lenz는 _trans 재질이지만 불투명 diff로.
    #   l_skill(왼손에서 뻗는 황금 스킬 이펙트)+l_skill_01~06_joint 뺌 · weapon_01은 오른손을 덮은 황금 손(튀어나오지 않음) → 남김.
    #   합치기: Head 자손(c_hair·glass_joint·귀걸이) → Head · 셔츠 자락 b_shirt·f_l/f_r/l/r_shirt → Hips · L/RHand_Fore_sup → ForeArm.
    #   🔴 _diff 알파는 음영 마스크(최소 0.231 · 평균 0.729 · 100%가 0.98 미만) → archive_rgb.
    "제한_이유범": dict(path="Assets/Art/Units/제한_이유범/제한_이유범.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.join(SKINS, "07_제한됨/제한_이유범.zip"), "source/gild_tesoro___bounty_rush_by_josoukitsune_dfawye9.7z",
                             "pl_tesoro_orig01/pl_tesoro_orig01.fbx"),
                    archive_rgb={"pl_tesoro_orig01/pl_tesoro_orig01_diff.png": "pl_tesoro_orig01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "glass_set", "glass_lenz_set", "l_skill",
                                 "l_hand_close", "r_hand_close", "l_hand_sp_01", "l_hand_sp_02", "l_hand_sp_03", "l_hand_sp_04",
                                 "r_hand_sp_02", "r_hand_sp_03", "r_hand_sp_04"],   # r_hand_sp_01은 원본에 없다
                    drop_bones=["world_joint", "HELPER_key", "HELPER_name", "glass_joint_set"] + [f"l_skill_0{i}_joint" for i in range(1, 7)],
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                 dict(pattern=r"^(b_shirt|f_[lr]_shirt|[lr]_shirt)$", into="mixamorig:Hips"),
                                 dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                 dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                    tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                    "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                    seed_zero_bones=0.001,
                    materials=dict(textures={"pl_tesoro_orig01": [("DiffuseColor", "pl_tesoro_orig01_diff.png")],
                                             "pl_tesoro_orig01_trans": [("DiffuseColor", "pl_tesoro_orig01_diff.png")]})),
    # 원피스 바운티러시 센고쿠 불상형(pl_sengoku_orig02) → 불멸_고도현(2026-09-22 불멸, blender 세션
    # — 이제 fix_unit_fbx.py도 blender 세션 전담, 구현담당2 꺼짐). zip 안 source/*.rar 안 FBX.
    #   pl_ 리그 뼈 55 · 메시 11 · 재질 2(기본·_trans) · 무가중치 0(테소로와 같은 리그, 손상 뼈
    #   없음) · 기본 자세 = 팔을 들어 주먹 쥔 자세(T자 아님) → tpose_arms.
    #   겹친 변형: face_normal · l/r_hand_open만 남김(attack·damage·close 뺌).
    #   코트: coat_root 서브트리(c_coat×3·l/r_coat×3·l/r_coat_arm×2·l/r_collar·l/r_epaulette_joint
    #   ×2) 전부 Chest(mixamorig:Spine1)로 — 이 리그는 coat_arm이 LArm/RArm 계열이 아니라
    #   coat_root 밑에서만 갈라져 팔 뼈에 안 물려 있음(직접 확인) → 팔 따라 뒤집힐 위험 자체가
    #   없어 별도 주의 없이 통째로 강체 처리.
    #   치마자락 bl/br/fl/fr/sl/srskirt_01 → Hips.
    #   🔴 황금 광택 — 원본 재질이 이미 Metallic 1.0으로 만들어져 있었다(직접 확인). diff.png
    #   만으로 금색이 잘 나옴 — matcap_gold·ramp·dither(원작 게임 셰이더 전용)는 안 씀.
    #   🔴 _diff 알파는 그림자 마스크로 추정(테소로와 같은 패턴) → archive_rgb로 알파 없이.
    #   거구 체형 — 키 1.8 정규화(얌마 선례, PM 확정: 유니티 쪽에서 최종 크기 조정) · T자 가로:
    #   세로 비율 1.359(참고용, 얌마 1.135보다 더 벌크).
    "불멸_고도현": dict(path="Assets/Art/Units/불멸_고도현/불멸_고도현.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.join(SKINS, "09_불멸/불멸_고도현.zip"), "source/pl_sengoku_orig02.rar",
                             "pl_sengoku_orig02/pl_sengoku_orig02.fbx"),
                    archive_rgb={"pl_sengoku_orig02/pl_sengoku_orig02_diff.png": "pl_sengoku_orig02_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close"],
                    drop_bones=["world_joint"],
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(pattern=r"^(coat_root|c_coat\d*|c_collar|l_coat\d*|l_collar|l_coat_arm\d*|l_epaulette_0[12]_joint"
                                        r"|r_coat\d*|r_collar|r_coat_arm\d*|r_epaurette_0[12]_joint)$", into="mixamorig:Spine1"),
                                 dict(pattern=r"^(bl|br|fl|fr|sl|sr)skirt_01$", into="mixamorig:Hips"),
                                 dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                 dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                    tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                    "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                    seed_zero_bones=0.001,
                    materials=dict(textures={"pl_sengoku_orig02": [("DiffuseColor", "pl_sengoku_orig02_diff.png")],
                                             "pl_sengoku_orig02_trans": [("DiffuseColor", "pl_sengoku_orig02_diff.png")]})),
    # 원피스 바운티러시 흰수염 에드워드 뉴게이트 전성기(pl_whitebeard_youn01) → 불멸_이이삭
    # (2026-09-22 불멸, blender 세션). 원본이 zip 안 glb 하나(바로 source=, archive 불필요).
    #   pl_ 리그 뼈 61(센고쿠와 같은 계열이나 Sketchfab glb라 전부 "_NN" 번호 꼬리 — rename_strip
    #   으로 PL_RENAME 그대로 재사용) · 메시 14 · 재질 1(_trans 없음) · 무가중치 0.
    #   겹친 변형: face_normal만(attack·damage 뺌). 손은 PM 지시대로 open만(hold·close 뺌 —
    #   무기를 손에서 없애니 쥔 모양 자체가 안 맞음).
    #   🔴 무기(weapon, 언월도) — PM 지시대로 뺌. 메시(weapon)뿐 아니라 그 부착 뼈
    #   weapon_01_047(RHand_Palm 자식)도 drop_bones로 같이 뺌(메시가 없으면 무가중치로
    #   남는데, 굳이 남겨 둘 이유가 없어 정리).
    #   🔴 코트 — PM 경고대로 어깨에 걸친 망토형(소매 안 낌). coat_root 서브트리(c_coat×3·
    #   c_collar·l/r_coat×3·l/r_coat_arm×3·l/r_collar) 전부 Chest(mixamorig:Spine1)로 강체 —
    #   이 리그도 coat_arm이 LArm/RArm 계열이 아니라 coat_root 밑에서만 갈라져(직접 확인)
    #   팔 뼈에 안 물려 있어 팔 따라 뒤집힐 위험 자체가 없음(센고쿠와 같은 구조).
    #   머리카락(c_hair×2·l_hair×2·r_hair×2)·모자(hat_parts) → Head. 허리끈 waist_01/02 → Hips.
    #   뿌리 쪽 보조 뼈(post_flag·pre_flag·world_joint·HELPER_key·HELPER_name, 전부 무가중치
    #   확인) → drop_bones.
    #   거구 체형 — 얌마·센고쿠 선례대로 키 1.8 정규화(유니티 쪽에서 최종 크기 조정).
    "불멸_이이삭": dict(path="Assets/Art/Units/불멸_이이삭/불멸_이이삭.fbx", kind="human", size=("height", 1.8),
                    source=os.path.join(SKINS, "09_불멸/불멸_이이삭.glb"),
                    # 🔴 glTF "바인드 자세 추정"(기본값 켜짐)이 이 리그에서 완전히 잘못 재구성돼
                    # 뼈·메시 전부가 z≈29.8(정상 범위 0~1.8과 무관)로 튀었다(직접 확인 — 조명용
                    # Icosphere만 정상 z −1~1, 캐릭터 전부는 z 29.76~29.815). 덴지·좀비·루피·
                    # 나나치와 같은 함정 — guess_bind 꺼서 장면 기본 자세를 그대로 쓴다.
                    gltf_guess_bind=False,
                    glb_images={0: "pl_whitebeard_youn01_diff.png"},   # glb 내장 이미지(Image_0, 1024², 외부 텍스처 파일 없음)
                    # glb 임포트라 오브젝트 이름이 Object_N(글b 노드 순서) — 직접 확인한 대응:
                    # Object_7=face_attack · Object_8=face_damage · Object_13=l_hand_close ·
                    # Object_15=l_hand_hold · Object_19=r_hand_close · Object_21=r_hand_hold ·
                    # Object_24=weapon.
                    drop_meshes=["Object_7", "Object_8", "Object_13", "Object_15", "Object_19", "Object_21", "Object_24", "Icosphere"],
                    # 🔴 PM 재반려(2026-09-22, 유니티 rig) — _rootJoint 자체를 안 빼서 최상위가
                    # Armature > _rootJoint > mixamorig:Hips로 남아 유니티가 _rootJoint를
                    # Hips로 오인했다(뼈 31개, 다른 유닛 25개 수준과 다름). _rootJoint도 같이
                    # 뺀다 — 부모 없는 진짜 루트라 빼면 mixamorig:Hips가 Armature 바로 밑
                    # 최상위가 된다(센고쿠·테소로와 같은 형태).
                    drop_bones=["_rootJoint", "pl_whitebeard_youn01_01", "post_flag_02", "pre_flag_00",
                                "world_joint_03", "HELPER_key_058", "HELPER_name_059", "weapon_01_047"],
                    rename_bones=PL_RENAME, rename_strip=r"_[0-9]+$", no_nulls=True, orient_snap=True,
                    merge_bones=[dict(pattern=r"^(coat_root|c_coat|c_collar|l_coat|l_coat_arm|l_collar"
                                        r"|r_coat|r_coat_arm|r_collar)(_[0-9]+)*$", into="mixamorig:Spine1"),
                                 dict(pattern=r"^waist(_[0-9]+)*$", into="mixamorig:Hips"),
                                 dict(pattern=r"^(c_hair|l_hair|r_hair|hat_parts)(_[0-9]+)*$", into="mixamorig:Head"),
                                 dict(pattern=r"^LHand_Fore_sup(_[0-9]+)*$", into="mixamorig:LeftForeArm"),
                                 dict(pattern=r"^RHand_Fore_sup(_[0-9]+)*$", into="mixamorig:RightForeArm")],
                    tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                    "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                    seed_zero_bones=0.001,
                    materials=dict(textures={"pl_whitebeard_youn01": [("DiffuseColor", "pl_whitebeard_youn01_diff.png")]})),
    # 원피스 바운티러시 사카즈키(아카이누) 해군 원수(pl_akainu_gens01) → 초월_양재모_AD
    # (2026-09-22 초월, 사장님 지시로 가프↔아카이누 스킨 맞바꿈 — 기존 "초월_양재모_AD" 가프
    # 항목은 "불멸_박은석"으로 옮겨졌고 PM이 커밋함, 이 키를 재사용). zip 안 source/*.rar 안 FBX.
    #   pl_ 리그 뼈 63(센고쿠·흰수염과 같은 계열, 번호 꼬리 없음) · 메시 17 · 재질 1(전체) ·
    #   애니 combo·damage·dodge·down 등 다수(안 씀, 결합 자세 기준) · 무가중치 0.
    #   겹친 변형: face_normal만(attack·damage 뺌). 손은 open만(close·close_magma 뺌).
    #   🔴 용암 개 소환수(이누가미 모델 메이고) 스킬 이펙트 — l/r_sp01_dog·l/r_sp01_dog_eye
    #   메시(용암 텍스처 늑대머리 모양, 렌더로 직접 확인) + 전용 뼈 사슬(Arm_Upper_Magma→
    #   Fore_Magma→sp_joint01/02→Palm_Magma→sp_dog01_Lear/Rear·sp_dog_jaw, 좌우 각 6개=12개)
    #   전부 뺌. l_hand_close_magma·r_hand_close_magma(마그마 코팅 주먹)도 같이 뺌 — 몸
    #   텍스처(diff)에 그려진 마그마 무늬는 그대로 유지(빼는 건 별도 메시로 튀어나온 이펙트뿐).
    #   l_arms·r_arms(101정점씩, 렌더로 확인 — 소맷단/토시 장식, 원작 신체) → 유지.
    #   🔴 코트 — 해군 원수 코트도 흰수염·센고쿠와 같은 패턴, coat_root 서브트리(c_coat×3·
    #   c_coat_eri·l/r_coat×3·l/r_coat_arm×2·l/r_coat_eri)가 LArm/RArm 계열이 아니라 coat_root
    #   밑에서만 갈라져(직접 확인) 팔 뼈에 안 물려 있음 → Chest(mixamorig:Spine1) 강체로 안전.
    #   옷자락 suso_l/suso_r(Body_Pelvis 자식) → Hips.
    "초월_양재모_AD": dict(path="Assets/Art/Units/초월_양재모_AD/초월_양재모_AD.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.join(SKINS, "08_초월/초월_양재모_AD.zip"), "source/pl_akainu_gens01.rar",
                             "pl_akainu_gens01/pl_akainu_gens01.fbx"),
                    archive_rgb={"pl_akainu_gens01/pl_akainu_gens01_diff.png": "pl_akainu_gens01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close",
                                 "l_hand_close_magma", "r_hand_close_magma",
                                 "l_sp01_dog", "r_sp01_dog", "l_sp01_dog_eye", "r_sp01_dog_eye"],
                    drop_bones=["world_joint"],
                    drop_bones_re=r"^[LR]Arm_(Upper_Magma|Upper_Fore_Magma|sp_joint0[12]|Upper_Palm_Magma)$"
                                  r"|^[LR]_sp_dog01_[LR]ear$|^[LR]sp_dog_jaw$",
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(pattern=r"^(coat_root|c_coat0[123]|c_coat_eri|l_coat|l_coat0[123]|l_coat_arm|l_coat_arm0[12]"
                                        r"|l_coat_eri|r_coat|r_coat0[123]|r_coat_arm|r_coat_arm0[12]|r_coat_eri)$", into="mixamorig:Spine1"),
                                 dict(pattern=r"^suso_[lr]$", into="mixamorig:Hips"),
                                 dict(pattern=r"^LHand_Fore_sup$", into="mixamorig:LeftForeArm"),
                                 dict(pattern=r"^RHand_Fore_sup$", into="mixamorig:RightForeArm")],
                    tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                    "Hand": f"mixamorig:{side}Hand"} for s, side in (("L", "Left"), ("R", "Right"))},
                    seed_zero_bones=0.001,
                    materials=dict(textures={"pl_akainu_gens01": [("DiffuseColor", "pl_akainu_gens01_diff.png")]})),
    # 나루토 젊은 오비토(NUNS4 XPS 립 「Obito (Young Cloak Hidden)」, 원본 .blend) → 제한_김민규(2026-09-17 제한됨). zip → source/*.rar → OBXBod1.blend.
    #   뼈 192(`2obx00t0 l thigh` 식 Biped 소문자) · 메시 6(body·hood·Hcells(하시라마 세포)·wood(나무 가시)·face·eye — 얼굴·눈은 한쪽 절반뿐, 나머지 반은 세포 몸) · 애니 0 · 쉬는 자세 A자.
    #   🔴 upperarm·forearm·calf 본체 뼈는 가중치 0 — 전부 비틀림 보조 뼈(arm bone01~06·sleeve·foot bone01~05)에 실려 있다 → 본체로 합친다.
    #   🔴 spine이 pelvis 자식이 아니라 루트 `2obx00t0`(가중치 0) 자식 → 루트 둘 빼고 Spine을 Hips에 다시 붙인다.
    #   망토 앞·옆·뒤 뼈(pelvis 밑)·body·cutting → Hips · hood_off(spine1 밑 뒤로 넘긴 두건) → Spine1 · Head 자손(얼굴·눈썹·입술·hood_on) → Head.
    #   텍스처는 rar 속 dds 대신 바깥 zip textures/의 PNG(2obxbody RGB · 2obxeye 팔레트) → archive_rgb_outer.
    "제한_김민규": dict(path="Assets/Art/Units/제한_김민규/제한_김민규.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.join(SKINS, "07_제한됨/제한_김민규.zip"), "source/nuns4___obito__young_cloak_hidden__xps_blend_by_o_dv89_o_dfw5a21.rar",
                             "NUNS4 - Obito (Young Cloak Hidden)/OBXBod1.blend"),
                    archive_rgb={"textures/2obxbody.png": "2obxbody.png", "textures/2obxeye.png": "2obxeye.png"}, archive_rgb_outer=True,
                    no_nulls=True, orient_snap=True, first_uv_only=True,
                    rename_bones={k.lower(): v for k, v in biped_rename("2obx00t0").items()},
                    drop_bones=["2obx00t0", "2obx00t0 trall"], reparent_bones={"mixamorig:Spine": "mixamorig:Hips"},
                    merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")]
                                + [dict(pattern=rf"^2obx00t0 {s} foot bone0[12]$", into=f"mixamorig:{side}UpLeg") for s, side in (("l", "Left"), ("r", "Right"))]
                                + [dict(pattern=rf"^(2obx00t0 {s} foot bone0[345]|2obx_asianime{s}\d*)$", into=f"mixamorig:{side}Leg") for s, side in (("l", "Left"), ("r", "Right"))]
                                + [dict(pattern=rf"^2obx00t0 {s} arm bone0[123]$", into=f"mixamorig:{side}Arm") for s, side in (("l", "Left"), ("r", "Right"))]
                                + [dict(pattern=rf"^(2obx00t0 {s} arm bone0[456]|2obx00t0 {side.lower()} sleeve|2obx_udeanm{s}\d*)$", into=f"mixamorig:{side}ForeArm")
                                   for s, side in (("l", "Left"), ("r", "Right"))]
                                + [dict(pattern=r"^2obx00t0 ([lr] (back|front|side)cape bone\d|body|cutting)$", into="mixamorig:Hips"),
                                   dict(pattern=r"^2obx00t0 hood_off( bone| bonenub)?$", into="mixamorig:Spine1")],
                    tpose_arms=biped_tpose_names(), seed_zero_bones=0.001,
                    materials=dict(textures={f"5_{k}_1_0_0": [("DiffuseColor", "2obxeye.png" if k == "eye" else "2obxbody.png")]
                                             for k in ("body", "hood", "Hcells", "wood", "face", "eye")})),
    # 유희왕 무토 유기(UE 게임 립 「Yugi Moto.fbx」 FBX 7500 · UnitScale 2.54, chr0400) → 제한_임준성(2026-09-17 제한됨). zip 안 source/*.fbx + textures/T_Chr0400_*_C.png(알파 전부 255).
    #   뼈 127(킹·심해왕과 같은 게임 뼈 이름 계열) — 🔴 어깨는 심해왕식: CLAVICLE=쇄골 · SHOULDER=위팔(킹은 SHOULDER·ARM). CLANK=무릎 · TOE1=발목 · TOE2=발끝.
    #   메시 4: form0(몸·옷·머리카락·결투 원반, 얼굴 없음) · facial1(얼굴 — form0과 겹치지 않음, 최근접 중앙 23 mm)
    #     · 🔴 damage_AB = 어깨에 걸친 재킷 망토(배틀시티 복장, 이름과 달리 원작 모습 — PM 2차 지시로 포함) · damage_BC = 몸 속에 묻힌 찢김 조각(허벅지·종아리·가슴 패치) → 뺌.
    #   망토 가중치: CLOTH_A → Spine2 · 위팔(SHOULDER_L/R 189/169) → move_weights로 같은 쪽 쇄골(Shoulder) — 팔 따라 날개처럼 뜨지 않게.
    #   form0 안 재질 칸 lens(눈 덮개, zip에 텍스처 없음)·weapon_glow(원반 발광 110면)·eyeshadow(눈 위 반투명 그늘 — 불투명이면 눈이 검은 선글라스가 된다, 얼굴 근접 렌더 확인) → drop_material_faces로 면만 지움.
    #   HAIR_00~25·FACE·EYE → Head · CLOTH_A(등 뒤로 걸친 재킷)·E(깃)·G(앞자락) → Spine2 · EQUIPMENT_00_L(왼 팔뚝 결투 원반 988) → LeftForeArm · ROLL·EFFECT는 본체로.
    #   NULL·RESERVE·UTILITY_00~09·THROW(가중치 0) 뺌.
    "제한_임준성": dict(path="Assets/Art/Units/제한_임준성/제한_임준성.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.join(SKINS, "07_제한됨/제한_임준성.zip"), "source/Yugi Moto.fbx"),
                    archive_rgb={f"textures/T_Chr0400_{k}_C.png": f"T_Chr0400_{k}_C.png" for k in ("skin", "cloth_01", "cloth_02", "weapon", "hair", "eye")},
                    no_nulls=True, orient_snap=True,
                    drop_meshes=["chr0400_damage_BC"], drop_material_faces=["MI_chr0400_lens", "MI_chr0400_weapon_glow", "MI_chr0400_eyeshadow"],
                    rename_bones=dict({k: v for k, v in KING_RENAME.items() if not re.match(r"^(SHOULDER|ARM)_[LR]$", k)},
                                      **{f"{b}_{p}": f"mixamorig:{side}{j}" for p, side in (("L", "Left"), ("R", "Right"))
                                         for b, j in (("CLAVICLE", "Shoulder"), ("SHOULDER", "Arm"))}),
                    drop_bones=["NULL", "RESERVE", "THROW"] + [f"UTILITY_{i:02d}" for i in range(10)],
                    merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                 dict(pattern=r"^CLOTH_[AEG]_\d\d(_[LR])?$", into="mixamorig:Spine2")]
                                + [d for p, side in (("L", "Left"), ("R", "Right")) for d in (
                                    dict(pattern=rf"^CLAVICLEROLL_{p}$", into=f"mixamorig:{side}Shoulder"),
                                    dict(pattern=rf"^ELBOWROLL_{p}$", into=f"mixamorig:{side}ForeArm"),
                                    dict(pattern=rf"^EFFECT_{p}$", into=f"mixamorig:{side}Hand"),
                                    dict(pattern=rf"^CLANKROLL_{p}$", into=f"mixamorig:{side}Leg"))]
                                + [dict(pattern=r"^EQUIPMENT_00_L$", into="mixamorig:LeftForeArm"), dict(pattern=r"^EQUIPMENT_00_R$", into="mixamorig:RightHand")],
                    tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                         "Hand": f"mixamorig:{side}Hand"},
                                        **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(_BIPED_FINGERS)
                                           for j, k in (("", 1), ("1", 2), ("2", 3))})
                                for s, side in (("L", "Left"), ("R", "Right"))},
                    double_sided_meshes=["chr0400_damage_AB"],
                    move_weights=[dict(meshes=["chr0400_damage_AB"], bones=[f"mixamorig:{side}Arm"], into=f"mixamorig:{side}Shoulder") for side in ("Left", "Right")],
                    seed_zero_bones=0.001,
                    materials=dict(textures={**{f"MI_chr0400_{k}": [("DiffuseColor", f"T_Chr0400_{k}_C.png")]
                                                for k in ("skin", "cloth_01", "cloth_02", "weapon", "hair", "eye")},
                                             "MI_chr0400_cloth_01.001": [("DiffuseColor", "T_Chr0400_cloth_01_C.png")],
                                             "MI_chr0400_hair.001": [("DiffuseColor", "T_Chr0400_hair_C.png")],
                                             "MI_chr0400_skin.001": [("DiffuseColor", "T_Chr0400_skin_C.png")],
                                             "MI_chr0400_oral": [("DiffuseColor", "T_Chr0400_skin_C.png")]})),
    # L4D2 부머 → 희귀함_이용민(2026-09-16 희귀함 7호). 오늘 받은 것 중 제일 깨끗한 원본:
    #   뼈 33개가 **이미 mixamorig 표준 이름**이라 rename 불필요 · Head 직계 자식은 HeadTop_End 하나뿐(아디오식 Jaw/Eye 위험 없음) ·
    #   메시 1개(2,659정점 · 5,257삼각형)라 감량·join 사고 없음 · 기본 자세 = 쉬는 자세(어긋난 뼈 0).
    #   손가락은 검지(HandIndex1~4) 한 줄뿐 — 유니티 Humanoid에서 선택 뼈라 그대로 둔다.
    #   축·정면 실측: 위쪽 +z(엉덩이→머리 (0, 0.155, 0.988)) · **정면 −Y**(양발 발끝이 −y로 0.03씩 · 배가 y −0.211까지 나옴) → 돌릴 필요 없음(orient_snap이 확인만).
    #   🔴 FBX에 **재질·텍스처가 0개**라 아틀라스 boomer_color.png(2048², 얼굴·셔츠·바지가 한 장)를 손으로 물린다 — 메시에 재질이 없으니 mesh_material로 새로 만들어 붙임.
    #   쉬는 자세는 팔이 수평 아래 약 58°(어깨 ±0.14/0.563 → 손 ±0.25/0.389) → tpose_arms로 T자.
    "희귀함_이용민": dict(path="Assets/Art/Units/희귀함_이용민/희귀함_이용민.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_이용민.zip"),      # zip 하나뿐이라 (압축, 파일) 두 칸 — rar 자리에 None을 넣으면 안 된다
                               "source/PC _ Computer - Left 4 Dead 2 - Special Infected - Boomer.fbx"),
                      # 텍스처는 archive_rgb로 유닛 Textures/에 먼저 놓는다 — archive_textures는 임시 폴더에만 풀려서 재질을 짤 때 「파일이 없다」로 걸린다.
                      #   이 아틀라스는 알파가 전부 1.0(완전 불투명)이라 RGB로 저장해도 달라지는 게 없다.
                      archive_rgb={"textures/boomer_color.png": "boomer_color.png"},
                      no_nulls=True, orient_snap=True,
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      materials=dict(mesh_material={"boomer_reference": "boomer_color"},
                                     textures={"boomer_color": [("DiffuseColor", "boomer_color.png")]})),
    # 바키 → 희귀함_엄태웅(2026-09-16 희귀함 6호). Sketchfab glb인데 **인물 하나가 아니라 장면**이다 — 실측:
    #   · 같은 인물 3벌(아마추어 Object_4·126·202, 각 69뼈·21,485정점·33,476삼각형)이 x로 8 m씩 떨어져 서 있다 → **한 벌(Object_4)만 남긴다**.
    #   · 나머지 스킨 4개는 옷 변형(검은 수트·긴 코트)과 소품이고 본체와 **겹치지 않는 별개 자리**에 있다(렌더로 확인) → 전부 뺀다.
    #   🔴 원본 축: 아마추어에 X −90°가 걸려 **위쪽이 −y · 정면이 +z**다(발목→머리 (0.105,−0.994,0.009) · 주성분 −y 111 대 39/16 ·
    #     눈 뼈 z 0.768 > 머리 뼈 0.663 · 발끝 z 0.880 > 몸 평균 0.727). orientation+orient_snap이 이걸 세워 준다.
    #   🔴 쉬는 자세는 팔이 수평 아래 **76.5°**(PM 조사의 55°보다 가파르다 — 어깨(±0.28,0.0)→손(±0.46,0.75)) → tpose_arms로 T자.
    #   🔴 Head 자손에 눈 뼈 L/P glaz와 cp1251로 깨진 이름 하나가 있다 → merge_bones(under=Head)로 합쳐 아디오식 Jaw/Eye 오매핑을 막는다.
    # 심해왕(원펀맨 게임 추출 glb, Sketchfab-16.14) — 2026-09-16 희귀함. 스킨 1 · 뼈 188 · 메시 48 · 재질 48 · 이미지 9 · 이미 T자(어깨·팔꿈치·손목 z 같음) · 발끝 −Y.
    #   🔴 얼굴이 세 벌: face_old*(재질 기본색 알파 0·BLEND = 숨긴 판, 대부분 뿌리 NULL 100퍼센트) · face1/face2(표정 판, Head/Neck만) · modelface*(얼굴 뼈에 제대로 실린 기본) → modelface만 남긴다.
    #     modelface 이빨 up0/up2/up3/down0/down2/down3도 알파 0 숨김 판이라 뺀다.
    #   배율: dsk.fbx 노드 ×0.01(세계 키 0.036) — NULL 관절 배율 없음(가로우의 ×10000 풀기 불필요, 역결합 행렬 단위 배율).
    #   가중치 0 부속 뼈(RESERVE·NULL·EFFECT·CLANK_*C·UTILITY·OPTION — OPTION_76은 몸에서 2 m 밖)는 뺀다. 팔꿈치·어깨 ROLL 뼈는 가중치가 커서 남김(매핑 뼈 자식).
    #   얼굴 뼈 67개(눈·입·이빨·볼·HAIR_03 왕관)는 merge_bones under=Head. 망토 텍스처 알파는 이진 컷아웃(중간값 0.7퍼센트) → 그대로.
    #   🔴 1회차: 조명용 Icosphere(±1 m)를 안 빼 「뼈 넘침」에 걸림 — 키 맞추기 전에 뺀다.
    "희귀함_이태훈": dict(path="Assets/Art/Units/희귀함_이태훈/희귀함_이태훈.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_이태훈.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=['Icosphere', 'Object_16', 'Object_17', 'Object_18', 'Object_19', 'Object_20', 'Object_21', 'Object_22', 'Object_23', 'Object_24', 'Object_25', 'Object_26', 'Object_27', 'Object_28', 'Object_29', 'Object_30', 'Object_31', 'Object_32', 'Object_33', 'Object_34', 'Object_35', 'Object_53', 'Object_55', 'Object_57', 'Object_59', 'Object_61', 'Object_63'],
                      rename_bones=SEAKING_RENAME,
                      drop_bones=['_rootJoint', 'bone0000_NULL_02', 'bone0001_RESERVE_03', 'bone0081_EFFECT_R_081', 'bone0105_EFFECT_L_0105', 'bone0157_CLANK_RC_0158', 'bone0163_CLANK_LC_0164', 'bone0164_EQUIPMENT_02_0165', 'bone0165_THROW_0166', 'bone0166_UTILITY_01_0167', 'bone0167_UTILITY_02_0168', 'bone0168_UTILITY_03_0169', 'bone0169_UTILITY_04_0170', 'bone0170_UTILITY_05_0171', 'bone0171_UTILITY_06_0172', 'bone0172_UTILITY_07_0173', 'bone0173_UTILITY_08_0174', 'bone0174_UTILITY_09_0175', 'bone0175_OPTION_51_0176', 'bone0176_OPTION_52_0177', 'bone0177_OPTION_53_0178', 'bone0178_OPTION_54_0179', 'bone0179_OPTION_55_0180', 'bone0180_OPTION_56_0181', 'bone0181_OPTION_71_0162', 'bone0182_OPTION_72_0182', 'bone0183_OPTION_73_0183', 'bone0184_OPTION_74_0184', 'bone0185_OPTION_75_0185', 'bone0186_OPTION_76_0186', 'bone0127_CLOTH_A_14_0128', 'bone0128_CLOTH_A_17_0129', 'bone0129_CLOTH_A_18_0130'],
                      # 🔴 1회차 Idle 46배: 털 망토 뒤쪽이 천 시뮬 뼈 CLOTH_A_17 100%(Idle이 안 움직임)이고 옆 정점은 RightShoulder 45% → 팔 휘두르면 털이 찢김 → 천 뼈 사슬째 Spine2로
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(under="bone0124_CLOTH_A_00_0125", with_root=True, into="mixamorig:Spine2")],
                      smooth_weights={"Object_13": 12},
                      glb_images={0: 'fin_PARTS_baseColor.png', 1: 'body_BODY_baseColor.png', 2: 'fur_FUR_baseColor.png', 3: 'mantle_MANT_baseColor.png', 4: 'eye_EYE_baseColor.png', 5: 'face_FACE_baseColor.png', 7: 'crown_CROWN_baseColor.png', 8: 'ear_EAR_baseColor.png'},
                      materials=dict(textures={m: [("DiffuseColor", f)] for m, f in {'fin_shoulderShapeC_PARTS': 'fin_PARTS_baseColor.png', 'fin_backShapeC_PARTS': 'fin_PARTS_baseColor.png', 'bodyShapeC_1_BODY_b': 'body_BODY_baseColor.png', 'bodyShapeC_1_BODY_r': 'body_BODY_baseColor.png', 'bodyShapeC_PARTS': 'fin_PARTS_baseColor.png', 'bodyShapeC_NAIL': 'body_BODY_baseColor.png', 'bodyShapeC_1_BODY_w': 'body_BODY_baseColor.png', 'furShapeC_FUR_STEG': 'fur_FUR_baseColor.png', 'mantleShapeC_MANT': 'mantle_MANT_baseColor.png', 'accessoryShapeC_1_FUR_g': 'fur_FUR_baseColor.png', 'modelfaceEyeeye_Reye_RShapeE_EYER': 'eye_EYE_baseColor.png', 'modelfaceEyeeye_Leye_LShapeE_EYEL': 'eye_EYE_baseColor.png', 'modelfaceEyeeye_R1eye_R1ShapeE_EYER': 'eye_EYE_baseColor.png', 'modelfaceEyeeye_L1eye_L1ShapeE_EYEL': 'eye_EYE_baseColor.png', 'modelfacefacefaceShapeS_1_FACE': 'face_FACE_baseColor.png', 'modelfacehigehigeShapeS_1_FACE': 'face_FACE_baseColor.png', 'modelfaceteethteeth_upteeth_upShapeC_1_TEETH': 'ear_EAR_baseColor.png', 'modelfaceteethteeth_downteeth_downShapeC_1_TEETH': 'ear_EAR_baseColor.png', 'modelfacecrowncrowncrownShapeC_CROWN_g': 'crown_CROWN_baseColor.png', 'modelfacecrowncrown_redcrown_redShapeC_CROWN_r': 'crown_CROWN_baseColor.png', 'modelfacefin_ear1fin_ear1ShapeS_EAR': 'ear_EAR_baseColor.png', 'modelfacetonguetongueShapeT_TOOTH001': 'face_FACE_baseColor.png'}.items()})),
    # 주술회전 후시구로 토지(중국 모바일 게임 립, DeviantArt kurokozeref) — 2026-09-16 희귀함. zip 안 zip 안 FBX(바이너리 7300, 3ds Max Biped).
    #   뼈 129 · 메시 7 · 재질 7(텍스처 노드 0 → 손으로 물림) · FBX ×0.1 노드(세계 키 0.29).
    #   🔴 팔 본체 Bip001 L/R UpperArm·Forearm이 **가중치 0**: 위팔은 Circle00x 밑 bone_niuqu_*·niuqu_shouzhou, 아래팔은 UpperArm 밑 ForeTwist·ForeTwist1이 실었다(우솝 구조) →
    #     되붙이기(우솝) 대신 **가중치를 본체로 합친다**(매핑 뼈에 가중치가 생기고 보조 뼈가 사라진다).
    #   🔴 뺄 것: zhouling(몸에 감긴 저주령 벌레, 6,239정점, Bone033~048 사슬로 몸 밖 0.5까지) · wuqi2(칼, 몸에서 떨어져 떠 있음) · wuqi1(단검, 역시 떠 있음, 스킨 없음).
    #   Head 밑 얼굴 뼈(eye·meimao·yanpi·xiaba·yachi·zuichun·zuijiao·hair) → Head로. 텍스처 알파는 음영 마스크(중간값 15%) → archive_rgb로 뗌.
    "희귀함_이재윤": dict(path="Assets/Art/Units/희귀함_이재윤/희귀함_이재윤.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_이재윤.zip"), "source/jujutsu_kaisen___toji_fushiguro_by_kurokozeref_dgzfc93.zip",
                               "role_fuheishener_skin#3092/role_fuheishener_skin.fbx"),
                      archive_rgb={"role_fuheishener_skin#3092/tex_role_fuheishener.png": "tex_role_fuheishener.png"},
                      no_nulls=True, orient_snap=True,
                      drop_meshes=["role_fuheishener_zhouling", "role_fuheishener_wuqi1", "role_fuheishener_wuqi2"],
                      # 🔴 2회차: Bip001 이름 그대로면 공용 Idle 판정이 한 뼈도 못 돌려 늘어짐 1.0(가짜 통과) → mixamorig로. 뿌리 뼈 role_fuheishener_skin(가중치 0)도 뺀다.
                      rename_bones=biped_rename("Bip001", spine2="Spine2"),
                      drop_bones=["role_fuheishener_skin", "Bip001", "dadao", "xiaodao"] + [f"Bone0{i}" for i in range(33, 49)],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Bip001 L ForeTwist", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^Bip001 R ForeTwist", into="mixamorig:RightForeArm"),
                                   dict(pattern=r"^(Circle002|bone_niuqu_01|bone_niuqu_03|niuqu_shouzhou_L)$", into="mixamorig:LeftArm"),
                                   dict(pattern=r"^(Circle001|bone_niuqu_00|bone_niuqu_02|niuqu_shouzhou_R)$", into="mixamorig:RightArm"),
                                   dict(pattern=r"^niuqu_spine_0[12]$", into="mixamorig:Spine")],
                      tpose_arms=biped_tpose_names(), use_rest_pose=True,     # 🔴 1회차: 기본 자세가 전투 자세(정면 +73°·몸 기울기 0.11)라 그대로 구우면 다리가 벌어진 채 → 결합 자세(A자·−Y)로
                      materials=dict(textures={f"role_fuheishener_{k}_mat_01": [("DiffuseColor", "tex_role_fuheishener.png")] for k in ("face", "skin", "body", "hair")})),
    # 블리치 야마다 하나타로(모바일 게임 립 FBX, 3ds Max Biped Bip01) — 2026-09-16 희귀함. 뼈 39 · 메시 2 · 재질 1 · 텍스처 256² 불투명 · FBX ×0.01(세계 키 0.02).
    #   기본 자세 = 결합 자세(차 3e-6). Twist 보조 뼈 없음, 팔다리 본체에 가중치 있음(Neck만 0 — 유니티 선택 뼈). 손가락 셋 × 두 마디.
    #   🔴 둘째 메시 cha_yamada_Y_wp1_0(220정점, 뿌리 직계 Weapon1 뼈 100%)은 등에 멘 칼이 아니라 **몸 옆 x 0.84에 세워 둔 참백도**(1회차 T자 앞·옆 렌더) → 칼·뼈 뺀다.
    #   얼굴은 eye Bone 하나 → Head.
    #   가중치 0 뿌리·빈 뼈(cha_yamada · Bip01 · cha_yamada.001 · cha_yamada_Y_wp1) 뺌. FBX가 부르는 cha_yamada_rgb1/2.png는 zip에 없다(색 교체 마스크) — cha_yamada.png만.
    "희귀함_현성현": dict(path="Assets/Art/Units/희귀함_현성현/희귀함_현성현.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_현성현.zip"), "source/cha_yamada.fbx"),
                      archive_rgb={"textures/cha_yamada.png": "cha_yamada.png"},
                      no_nulls=True, orient_snap=True, use_rest_pose=True, drop_meshes=["cha_yamada_Y_wp1_0"],
                      rename_bones=biped_rename("Bip01", fingers=3, joints=2),
                      drop_bones=["cha_yamada", "Bip01", "cha_yamada.001", "cha_yamada_Y_wp1", "Weapon1"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")],
                      tpose_arms=biped_tpose_names(fingers=3, joints=2),
                      materials=dict(textures={"cha_yamada": [("DiffuseColor", "cha_yamada.png")]})),
    # 블리치 사도 야스토라(채드, 모바일 게임 립 → Noesis → glb, 3ds Max Biped Bip001) — 2026-09-16 희귀함. zip 안 glb. 뼈 35 · 메시 6 · 삼각형 3,726 · 애니 24.
    #   🔴 glb 애니 24개라 가져온 자세가 클립 프레임(결합 자세와 차 1.08) → use_rest_pose(결합 자세 = 팔 내린 A자 67°) → T자.
    #   🔴 조명용 Icosphere 뺌(다섯 번째). 몸·얼굴·머리 재질이 텍스처 × 정점색 MIX인데 정점색이 전부 1.0(흰색) → 곱해도 그대로라 텍스처를 기본색에 직접 물린다.
    #   얼굴 뼈 head(가중치 0) 밑 chadu_leye/reye/mouth_0 → Head로. 손가락 둘 × 두 마디. 뿌리 Bip001(가중치 0) 뺌.
    "희귀함_구주호": dict(path="Assets/Art/Units/희귀함_구주호/희귀함_구주호.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_구주호.zip"), "source/chadu_0_battleout.glb"),
                      no_nulls=True, orient_snap=True, use_rest_pose=True, drop_meshes=["Icosphere"],
                      rename_bones=biped_rename("Bip001", fingers=2, joints=2, spine2="Spine2"),
                      drop_bones=["Bip001"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")],
                      tpose_arms=biped_tpose_names(fingers=2, joints=2),
                      glb_images={0: "chadu_body_baseColor.png", 1: "chadu_face_baseColor.png", 2: "chadu_hair_baseColor.png",
                                  3: "chadu_leye_baseColor.png", 4: "chadu_mouth_baseColor.png", 5: "chadu_reye_baseColor.png"},
                      materials=dict(textures={f"chadu_{k}_0": [("DiffuseColor", f"chadu_{k}_baseColor.png")]
                                               for k in ("body", "face", "hair", "leye", "mouth", "reye")})),
    # 진격의 거인 갑옷거인(중국 모바일 게임 립 role_kaizhijuren — 토지와 같은 게임, Sketchfab glb) — 2026-09-16 희귀함. 뼈 69 · 메시 3 · 삼각형 20,300 · 애니 0.
    #   🔴 glTF 결합 추정(guess_original_bind_pose)을 켜면 가중치 0인 UpperArm·Forearm의 역결합 행렬이 쓰레기라 팔이 키의 두 배 리본으로 늘어난다(쉬는 경계 x ±0.159) →
    #     gltf_guess_bind=False면 쉼 = 자세 = 주먹 쥔 전투 선 자세(깨끗) → 그 자세에서 T자로.
    #   🔴 조명용 Icosphere 뺌. 재질 MIX(텍스처 × 정점색 0.5 회색) — 이 게임 셰이더의 0.5 = 중립(토지와 같은 텍스처 단독) → 텍스처만 기본색에.
    #   Twist: 위팔 twist_bone_*_001~003 → Arm, ForeTwist·ForeTwist1·shouzhou → ForeArm(토지와 같은 구조, 본체 가중치 0). Head 밑 Bone001·Point001·head_bar → Head.
    "희귀함_박은석": dict(path="Assets/Art/Units/희귀함_박은석/희귀함_박은석.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_박은석.glb"), gltf_guess_bind=False,
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                      rename_bones=biped_rename("Bip001", spine2="Spine2"), rename_strip=r"_[0-9]+$",
                      drop_bones=["_rootJoint", "Bip001_00"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^twist_bone_L_00[123]_", into="mixamorig:LeftArm"),
                                   dict(pattern=r"^twist_bone_R_00[123]_", into="mixamorig:RightArm"),
                                   dict(pattern=r"^(Bip001 L ForeTwist1?|shouzhou_L)_[0-9]+$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^(Bip001 R ForeTwist1?|shouzhou_R)_[0-9]+$", into="mixamorig:RightForeArm")],
                      tpose_arms=biped_tpose_names(),
                      glb_images={0: "role_kaizhijuren_body_baseColor.png", 1: "role_kaizhijuren_face_baseColor.png", 2: "role_kaizhijuren_hair_baseColor.png"},
                      materials=dict(textures={f"role_kaizhijuren_{k}_mat_01": [("DiffuseColor", f"role_kaizhijuren_{k}_baseColor.png")]
                                               for k in ("body", "face", "hair")})),
    # 리그 오브 레전드 피즈(Sketchfab-15.25 glb, LoL 리그) — 2026-09-16 희귀함. 스킨 1 · 뼈 84 · 메시 1(Object_6, 4,929정점 · 7,891삼각형) · 이미지 512² 1장 · 애니 24.
    #   🔴 애니 24개라 가져온 자세가 클립 프레임(차 61) → use_rest_pose(결합 자세 A자). 🔴 조명용 Icosphere 뺌.
    #   🔴 LoL 추출본은 좌우 뒤집힘: L_ 뼈가 −x인데 눈·발끝은 −Y → mirror_x(가렌과 같음).
    #   🔴 삼지창이 몸과 같은 메시 안에 있고 뿌리 직계 Weapon_70 뼈 100%(몸 옆 x 82에 세워 둠) → drop_verts_of_bones로 그 정점을 지운다.
    #   뼈: Root_1(Spine·Hip 둘의 부모) → Hips · Spine_2 · Chest_3 → Spine1 · Head_4(Neck 없음, 유니티 선택 뼈) · Hip_9는 Hips와 UpLeg 사이 중간 뼈로 둠.
    #   Head 밑 머리카락 사슬·눈·눈꺼풀·입·귀걸이 → Head로. 가중치 0 BUFFBONE·PARENTING_HAND_LOC·뿌리 뺌. 손 지느러미 L/R_Fin은 손 자식이라 둠.
    "희귀함_조현규": dict(path="Assets/Art/Units/희귀함_조현규/희귀함_조현규.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_조현규.glb"), no_nulls=True, orient_snap=True, use_rest_pose=True, mirror_x=True,
                      drop_meshes=["Icosphere"], drop_verts_of_bones=["Weapon_70"],
                      rename_bones=FIZZ_RENAME, rename_strip=r"_[0-9]+$",
                      drop_bones=["GLTF_created_0_rootJoint", "Weapon_70", "BUFFBONE_CSTM_WEAPON_1_71", "BUFFBONE_GLB_WEAPON_1_74", "BUFFBONE_GLB_CHANNEL_LOC_72",
                                  "BUFFBONE_GLB_GROUND_LOC_73", "C_BUFFBONE_GLB_CENTER_LOC_75", "C_BUFFBONE_GLB_LAYOUT_LOC_78", "C_BUFFBONE_GLB_OVERHEAD_LOC_79",
                                  "C_BUFFBONE_GLB_CHEST_LOC_76", "L_PARENTING_HAND_LOC_29", "R_PARENTING_HAND_LOC_61", "L_BUFFBONE_GLB_HAND_LOC_81",
                                  "R_BUFFBONE_GLB_HAND_LOC_83", "L_BUFFBONE_GLB_FOOT_LOC_80", "R_BUFFBONE_GLB_FOOT_LOC_82"],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")],
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm",
                                      "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "fizz_base_baseColor.png"},
                      materials=dict(textures={"MAT_fizz_base2": [("DiffuseColor", "fizz_base_baseColor.png")]})),
    # 나루토 사스케(게임 추출 FBX 7400, 2022) — 2026-09-16 희귀함. zip 안 source/Sasuke.fbx + textures 5장. 뼈 189 · 메시 12 · 재질 12(텍스처 노드 0) · 기본 자세 = 결합 자세.
    #   🔴 메시 이름 +/- 접두어 = 게임 표시/숨김 변형: +Left Arm 남김 · -Left Arm Susanoo · -Sword · -Hair Damaged 뺌(뼈 sword control도).
    #   🔴 다리 뼈 leg left/right thigh가 pelvis가 아니라 뿌리 root ground 밑이다 → 유니티 휴머노이드는 UpLeg가 Hips 밑이어야 하니 Hips로 다시 붙인다.
    #   가중치 0 _end 뼈 전부 뺌. Head 밑 얼굴 뼈(턱·입술·혀·눈썹·눈꺼풀·눈알·볼·머리카락·모자) → Head로. 셔츠 깃·소매·아랫자락 뼈는 가중치가 있어 둔다.
    "희귀함_이은엽": dict(path="Assets/Art/Units/희귀함_이은엽/희귀함_이은엽.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_이은엽.zip"), "source/Sasuke.fbx"),
                      archive_rgb={"textures/main.png": "main.png", "textures/left_eye.png": "left_eye.png", "textures/right_eye.png": "right_eye.png"},
                      no_nulls=True, orient_snap=True,
                      drop_meshes=["5_-Left Arm Susanoo.Left Arm Susanoo_1_0_0", "5_-Sword.Sword_1_0_0", "7_-Hair Damaged.Hair Damaged_1_0_0"],
                      rename_bones=SASUKE_RENAME,
                      drop_bones=['root ground', 'sword control', 'head lip lower right_end', 'head lip lower left_end', 'head lip lower middle_end', 'head tongue b_end', 'hair side left 5b_end', 'hair side left 8_end', 'hair side left 9_end', 'hair side left 6_end', 'hair side left 7b_end', 'hair side right 5b_end', 'hair side right 8_end', 'hair side right 9_end', 'hair side right 6_end', 'hair side right 7b_end', 'hair back_end', 'hair front_end', 'head cap b_end', 'hair side left 4b_end', 'hair side left 3_end', 'hair side right 1c_end', 'hair side right 2_end', 'hair side right 4b_end', 'hair side right 3_end', 'hair side left 1c_end', 'hair side left 2_end', 'head eyebrow right 1_end', 'head eyebrow right 2_end', 'head eyebrow left 2_end', 'head eyebrow left 1_end', 'head eyebrow left 3_end', 'head eyebrow right 3_end', 'head cheek left_end', 'head cheek right_end', 'head lip upper right_end', 'head lip upper left_end', 'head lip upper middle_end', 'head eyeball right_end', 'head eyeball left_end', 'shirt collar left_end', 'shirt collar back_end', 'shirt collar right_end', 'shirt collar front_end', 'shirt sleeve left c_end', 'arm left finger 3c_end', 'arm left finger 4c_end', 'arm left finger 5c_end', 'arm left finger 2c_end', 'arm left finger 1c_end', 'arm right finger 3c_end', 'arm right finger 5c_end', 'arm right finger 1c_end', 'arm right finger 4c_end', 'arm right finger 2c_end', 'sword control_end', 'shirt lower front c_end', 'shirt lower back c_end', 'leg left toes_end', 'leg right toes_end'],
                      reparent_bones={"mixamorig:LeftUpLeg": "mixamorig:Hips", "mixamorig:RightUpLeg": "mixamorig:Hips"},
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")],
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      materials=dict(textures={"5_Face_1_0_0": [("DiffuseColor", "main.png")], "5_Hair_1_0_0": [("DiffuseColor", "main.png")],
                                               "5_Lower Body_1_0_0": [("DiffuseColor", "main.png")], "5_Upper Body_1_0_0": [("DiffuseColor", "main.png")],
                                               "5_+Left Arm.Left Arm_1_0_0": [("DiffuseColor", "main.png")], "5_Teeth_1_0_0": [("DiffuseColor", "main.png")],
                                               "5_Tongue_1_0_0": [("DiffuseColor", "main.png")],
                                               "5_Left Eye_1_0_0": [("DiffuseColor", "left_eye.png")], "5_Right Eye_1_0_0": [("DiffuseColor", "right_eye.png")]})),
    # 슈타인즈 게이트 오카베 린타로 → 희귀함_김정래(2026-09-16 희귀함, ※ 동명 넷 중 희귀함). zip 안 zip 안 원본 블렌더 파일 okabe_wiggle.blend1을 직접 연다.
    #   Rigify 생성 리그(뼈 489, 변형 114 = DEF 70 + 코트·벨트·이빨·혀 44) · 메시 11 · 이미 T자 · +Y 정면(orient_snap이 돌림) · 키 약 36(단위 m 아님).
    #   🔴 외곽선은 따로 된 메시가 아니라 **Solidify 수정자 + Outline 재질 칸** → blend_prep이 수정자·빈 재질 칸을 뺀다.
    #   🔴 DEF 뼈가 ORG/MCH 밑이라 변형 뼈끼리 계층이 없다(DEF-spine·허벅지·어깨·위팔이 뿌리) → parents 표로 다시 잇고 나머지 뼈 삭제.
    #   🔴 머리 메시 가중치 32%가 **변형 아닌 뼈 `face`**에 실려 있다(블렌더는 무시하지만 FBX는 클러스터로 씀) → 그 그룹 빼고 가까운 정점으로 채움.
    #   분할 뼈 .001(위팔·아래팔·허벅지·정강이)·palm·목 spine.005/006은 본체로 합치고 Head 밑(이빨·혀) → Head. 코트·벨트 뼈는 Hips 밑 그대로.
    #   얼굴(head_alteredTop)·COAT의 모양 키(표정·팔꿈치 보정)는 그대로 둔다(드라이버만 뺌).
    "희귀함_김정래": dict(path="Assets/Art/Units/희귀함_김정래/희귀함_김정래.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_김정래.zip"), "source/okabe_wiggle.zip", "okabe_wiggle.blend1"),
                      archive_rgb={"okabe UV- base.png": "okabe_UV_base.png", "Circle.png": "Circle.png"},
                      # 🔴 1회차 T자 렌더: 머리카락이 회색 — hair·Skin 재질이 툰 노드뿐이라 FBX가 뷰포트 회색을 씀 → 원본 Mix의 밝은 쪽 색(hair B · Skin A, 선형)을 단색으로
                      blend_prep=dict(drop_unused_materials=["Outline"], fallback_bone="DEF-head",
                                      solid_colors={"hair": (0.037, 0.042, 0.05), "Skin": (0.913, 0.694, 0.402)}, parents={'DEF-thigh.L': 'DEF-spine', 'DEF-thigh.R': 'DEF-spine', 'DEF-shoulder.L': 'DEF-spine.003', 'DEF-shoulder.R': 'DEF-spine.003', 'DEF-upper_arm.L': 'DEF-shoulder.L', 'DEF-upper_arm.R': 'DEF-shoulder.R', 'coats1.L': 'DEF-spine', 'coatf1.L': 'DEF-spine', 'coatb1.L': 'DEF-spine', 'coats1.R': 'DEF-spine', 'coatf1.R': 'DEF-spine', 'coatb1.R': 'DEF-spine', 'coatRear1': 'DEF-spine', 'belt1': 'DEF-spine', 'teethBottom': 'DEF-head', 'teethTop': 'DEF-head', 'tongue1': 'DEF-head'}),
                      no_nulls=True, orient_snap=True,
                      rename_bones=OKABE_RENAME,
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^DEF-spine\.00[56]$", into="mixamorig:Neck"),
                                   dict(pattern=r"^DEF-upper_arm\.L\.001$", into="mixamorig:LeftArm"),
                                   dict(pattern=r"^DEF-upper_arm\.R\.001$", into="mixamorig:RightArm"),
                                   dict(pattern=r"^DEF-forearm\.L\.001$", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^DEF-forearm\.R\.001$", into="mixamorig:RightForeArm"),
                                   dict(pattern=r"^DEF-thigh\.L\.001$", into="mixamorig:LeftUpLeg"),
                                   dict(pattern=r"^DEF-thigh\.R\.001$", into="mixamorig:RightUpLeg"),
                                   dict(pattern=r"^DEF-shin\.L\.001$", into="mixamorig:LeftLeg"),
                                   dict(pattern=r"^DEF-shin\.R\.001$", into="mixamorig:RightLeg"),
                                   dict(pattern=r"^DEF-palm\.0[1-4]\.L$", into="mixamorig:LeftHand"),
                                   dict(pattern=r"^DEF-palm\.0[1-4]\.R$", into="mixamorig:RightHand")],
                      materials=dict(textures={"body_new": [("DiffuseColor", "okabe_UV_base.png")], "specular": [("DiffuseColor", "okabe_UV_base.png")],
                                               "BODY_CELL": [("DiffuseColor", "okabe_UV_base.png")], "eyes": [("DiffuseColor", "Circle.png")]})),
    # 진격의 거인 에렌 예거(4기 리베리오 — 왼다리 절단·목발, Sketchfab-14.87 glb, SFM 계열 리그) — 2026-09-16 희귀함. 뼈 211(`_End` 끝점 뼈 절반) · 메시 11 · 이미지 11 · 애니 0.
    #   🔴 glTF 결합 추정을 켜면 쉬는 메시가 옆으로 눕는다(Y 위) → gltf_guess_bind=False(쉼 = 자세, Z 위).
    #   🔴 **왼다리가 없다**(허벅지 뼈 Left leg만 · 신발도 오른쪽 하나) — 유니티 휴머노이드는 LeftLowerLeg·LeftFoot이 필수 →
    #     오른 무릎·발목 뼈를 X 거울로 세우고(add_bones) 왼 허벅지 그룹의 가장 낮은 정점(바짓단 끝) 3개에 0.001 씨앗 가중치.
    #   🔴 조명용 Icosphere · 목발 Object_226(바닥까지 닿는 막대) 뺌, Crutches 뼈도. Twist: ZArmTwist → Arm · ZHandTwist → ForeArm.
    #   Head 밑 얼굴 뼈(눈·눈썹·눈꺼풀·콧구멍·이빨·혀·턱·머리카락) → Head로. 가중치 0 `_End` 뼈는 정규식으로 뺌.
    "희귀함_김기연": dict(path="Assets/Art/Units/희귀함_김기연/희귀함_김기연.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_김기연.glb"), gltf_guess_bind=False,
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere", "Object_226"],
                      rename_bones=EREN_RENAME, rename_strip=r"_[0-9]+$",
                      drop_bones=["_rootJoint", "Crutches_Root_0123", "Part_Hand_0124", "Crutches_Length_1_0125", "Crutches_Length_2_0126",
                                  "Crutches_Length_3_0127", "Crutches_Length_4_0128"],
                      drop_bones_re=r"(?i)_end(_|$)",
                      add_bones=[dict(name="mixamorig:LeftLeg", mirror_of="mixamorig:RightLeg", parent="mixamorig:LeftUpLeg", seed_group="mixamorig:LeftUpLeg"),
                                 dict(name="mixamorig:LeftFoot", mirror_of="mixamorig:RightFoot", parent="mixamorig:LeftLeg", seed_group="mixamorig:LeftUpLeg")],
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^ZArmTwist_L", into="mixamorig:LeftArm"), dict(pattern=r"^ZArmTwist_R", into="mixamorig:RightArm"),
                                   dict(pattern=r"^ZHandTwist_L", into="mixamorig:LeftForeArm"), dict(pattern=r"^ZHandTwist_R", into="mixamorig:RightForeArm")],
                      tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm",
                                      "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: 'Buttons_B_baseColor.png', 1: 'Shoes_baseColor.png', 2: 'Buttons_C_baseColor.png', 3: 'Head_baseColor.png', 4: 'Face_baseColor.png', 5: 'Gloves_baseColor.png', 6: 'Armband_baseColor.png', 7: 'Bandage_baseColor.png', 8: 'Nail_Finger_baseColor.png', 10: 'Buttons_A_baseColor.png'},
                      materials=dict(textures={m: [("DiffuseColor", f"{m}_baseColor.png")] for m in ('Buttons_B', 'Shoes', 'Buttons_C', 'Head', 'Face', 'Gloves', 'Armband', 'Bandage', 'Nail_Finger', 'Buttons_A')})),
    # 히로아카 다비 → 희귀함_배성령(2026-09-16 희귀함, ※ 동명 셋 중 희귀함 — 특별함_배성령은 사이타마). Mixamo 리그 glb, 뼈 이름이 이미 mixamorig + 번호 꼬리.
    #   뼈 79 · 메시 5(+조명용 Icosphere) · 삼각형 7,482 · 이미지 5 · 애니 1 · 단위 cm(원점이 엉덩이, 발 z −252 · 머리 꼭대기 205).
    #   🔴 애니 1개라 가져온 자세가 클립 프레임(차 449) → use_rest_pose(결합 자세 = T자, 팔 z 96~99 수평).
    #   🔴 가중치 0 끝점 뼈(HeadTop_End · 손가락 4번 · Toe_End · _end)가 전부 쓰레기 자리(−0.47, −8.45, −251.7)에 모여 뼈 넘침·키 측정을 망친다 → 정규식으로 뺀다.
    "희귀함_배성령": dict(path="Assets/Art/Units/희귀함_배성령/희귀함_배성령.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_배성령.glb"), no_nulls=True, orient_snap=True, use_rest_pose=True,
                      drop_meshes=["Icosphere"],
                      rename_regex=(r"(mixamorig:[A-Za-z0-9]+?)_[0-9]+", r"\1"),
                      drop_bones=["_rootJoint"],
                      drop_bones_re=r"(HeadTop_End|Thumb4|Index4|Middle4|Ring4|Pinky4|Toe_End)((_end)?_[0-9]+)?$",   # 손가락 4번은 rename_regex가 꼬리를 먼저 뗀다
                      glb_images={0: "dabi_lower_baseColor.png", 1: "dabi_face_baseColor.png", 2: "dabi_hair_baseColor.png",
                                  3: "dabi_upper_baseColor.png", 4: "dabi_eyes_baseColor.png"},
                      materials=dict(textures={f"21_{k}_1_0_0": [("DiffuseColor", f"dabi_{k}_baseColor.png")] for k in ("lower", "face", "hair", "upper", "eyes")})),
    # 죠죠 3부 쿠죠 죠타로 → 희귀함_배병규(2026-09-16 희귀함). Sketchfab-15.44 glb, SFM ValveBiped 리그(에렌과 같은 얼굴 뼈 규칙). 뼈 145 · 메시 7 · 이미지 3 · 애니 0.
    #   🔴 glTF 결합 추정을 켜면 쉬는 메시가 z 4.98~11.22로 떠서 다리가 뭉개진다(PM의 「단위 2.4배 불일치」) → gltf_guess_bind=False(쉼 = 자세).
    #   이미 T자. 어깨 보조 뼈 Arm_*_Shoulder_Adj_A(어깨, 가중치 36) → Arm · Adj_B(팔꿈치, 19)·Sleeve(70) → ForeArm으로 합침.
    #   Head 밑 얼굴 뼈(눈꺼풀·눈썹·볼·코·입·혀·이빨·눈·모자 챙·머리카락) → Head. 옷깃·키체인·재킷 자락·주머니 뼈는 가중치가 있어 둔다.
    #   조명용 Icosphere 뺌. 이빨·혀 재질 3개는 텍스처 없음(입 안 흰색 단색 그대로).
    "희귀함_배병규": dict(path="Assets/Art/Units/희귀함_배병규/희귀함_배병규.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_배병규.glb"), gltf_guess_bind=False,
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                      # 🔴 1회차 T자: 옷깃 금색 키체인이 옆으로 수평 막대처럼 뻗음(뼈가 수평, 게임은 물리) → 사슬 뿌리를 돌려 늘어뜨린다.
                      #   2회차 Y축 90°는 곧게 아래(z 1.616→1.373)지만 y +0.046이라 코트 속에 묻힘 → +X를 아래·앞 (0, −0.55, −0.84)로 보내는 축 (0, 0.84, −0.55)·90°
                      pose_bones_world={"Keychain_A_67": ((0.0, 0.84, -0.55), 90.0)},
                      rename_bones=VALVE_RENAME, rename_strip=r"_[0-9]+$",
                      drop_bones=["GLTF_created_0_rootJoint"], drop_bones_re=r"(?i)_end(_[0-9]+)?$",
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head"),
                                   dict(pattern=r"^Arm_Left_Shoulder_Adj_A_", into="mixamorig:LeftArm"),
                                   dict(pattern=r"^Arm_Right_Shoulder_Adj_A_", into="mixamorig:RightArm"),
                                   dict(pattern=r"^(Arm_Left_Shoulder_Adj_B|Arm_Left_Sleeve)_", into="mixamorig:LeftForeArm"),
                                   dict(pattern=r"^(Arm_Right_Shoulder_Adj_B|Arm_Right_Sleeve)_", into="mixamorig:RightForeArm")],
                      glb_images={0: "JotaroKujoPart3_EyeR_baseColor.jpg", 1: "JotaroKujoPart3_EyeL_baseColor.png", 2: "JotaroKujoPart3_Body_baseColor.png"},
                      materials=dict(textures={"JotaroKujoPart3_EyeR": [("DiffuseColor", "JotaroKujoPart3_EyeR_baseColor.jpg")],
                                               "JotaroKujoPart3_EyeL": [("DiffuseColor", "JotaroKujoPart3_EyeL_baseColor.png")],
                                               "JotaroKujoPart3_Face": [("DiffuseColor", "JotaroKujoPart3_Body_baseColor.png")],
                                               "JotaroKujoPart3_Body": [("DiffuseColor", "JotaroKujoPart3_Body_baseColor.png")]})),
    # 코토부키 히비키 → 희귀함_박수찬(2026-09-16 희귀함). Sketchfab-16.67 glb, ValveBiped 리그. 스킨 1 · 뼈 108 · 메시 16 · 삼각형 58,318 · 이미 T자.
    #   🔴 재질 14개가 전부 KHR_materials_pbrSpecularGlossiness의 diffuseTexture에 텍스처(표준 baseColor 비어 있음) → 확장의 diffuse 이미지를 재질 이름 기준 파일로 뽑아
    #     기본색에 직접 물린다. Glasses(검정 0.092)·glass(투명 알파 0.097)는 텍스처 없이 색 그대로.
    #   🔴 가중치 0 매핑 뼈 Spine1·Spine4·Toe0·손가락 끝이 결합 추정에서 원점(0,0,0) 쓰레기 자리 → gltf_guess_bind=False + 뺀다(척추는 Spine→Spine2→Neck1로 이어짐).
    #   Head1 직계 자식 익명 뼈 54개(boneN.NNN — 머리카락·헤드폰·안경 흔들림) → Head로. 조명용 Icosphere 뺌. 머리카락 메시만 UV 2벌(합치지 않으니 UV0 확인만).
    "희귀함_박수찬": dict(path="Assets/Art/Units/희귀함_박수찬/희귀함_박수찬.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_박수찬.glb"), gltf_guess_bind=False,
                      no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                      rename_bones=HIBIKI_RENAME, rename_strip=r"_[0-9]+$",
                      drop_bones=["_rootJoint"],
                      drop_bones_re=r"^ValveBiped\.Bip01_(Spine1|Spine4|[LR]_Toe0|[LR]_Finger[0-4]2)_[0-9]+$",
                      merge_bones=[dict(under="mixamorig:Head", into="mixamorig:Head")],
                      glb_images={0: 'hibiki_Shirt_diffuse.png', 1: 'hibiki_Body_diffuse.png', 2: 'hibiki_ArmCover_diffuse.png', 3: 'hibiki_Scarf_diffuse.png', 4: 'hibiki_Pants_diffuse.png', 5: 'hibiki_Jacket_diffuse.png', 6: 'hibiki_Headphones_diffuse.png', 7: 'hibiki_Eye_R_diffuse.png', 8: 'hibiki_Hair_diffuse.png', 10: 'hibiki_Bracelet_diffuse.png', 11: 'hibiki_Boots_diffuse.png'},
                      materials=dict(textures={m: [("DiffuseColor", {'Shirt': 'hibiki_Shirt_diffuse.png', 'Body': 'hibiki_Body_diffuse.png', 'ArmCover': 'hibiki_ArmCover_diffuse.png', 'Scarf': 'hibiki_Scarf_diffuse.png', 'Pants': 'hibiki_Pants_diffuse.png', 'Jacket': 'hibiki_Jacket_diffuse.png', 'Headphones': 'hibiki_Headphones_diffuse.png', 'Eye_R': 'hibiki_Eye_R_diffuse.png', 'Eye_L': 'hibiki_Eye_R_diffuse.png', 'Hair': 'hibiki_Hair_diffuse.png', 'Bracelet': 'hibiki_Bracelet_diffuse.png', 'Boots': 'hibiki_Boots_diffuse.png'}[m])] for m in ('Shirt', 'Body', 'ArmCover', 'Scarf', 'Pants', 'Jacket', 'Headphones', 'Eye_R', 'Eye_L', 'Hair', 'Bracelet', 'Boots')})),
    "희귀함_엄태웅": dict(path="Assets/Art/Units/희귀함_엄태웅/희귀함_엄태웅.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_엄태웅.glb"), no_nulls=True, orient_snap=True,
                      # 🔴 조명용 Icosphere 7개를 안 빼면 그것까지 합친 높이를 1.8 m로 맞춰서 사람이 1/3 크기(가로 0.49 m)로 줄어든다 — 첫 빌드에서 실제로 그랬다
                      drop_meshes=["Object_197", "Object_198", "Object_199", "Object_200",      # 복제 2벌
                                   "Object_273", "Object_274", "Object_275", "Object_276",
                                   "Object_97", "Object_124", "Object_281", "Object_293",       # 옷 변형·소품 스킨 4개
                                   "Icosphere", "Icosphere.001", "Icosphere.002", "Icosphere.003",
                                   "Icosphere.004", "Icosphere.005", "Icosphere.006"],
                      rename_bones=BAKI_RENAME,
                      # 🔴 glTF 뿌리 _rootJoint는 몸에서 7 m 떨어진 원점 근처(y +0.146)에 있어 Icosphere를 빼고 나면 「뼈 넘침」에 걸린다.
                      #   가중치 0이고 자식은 Hips뿐이라 빼면 Hips가 뿌리가 된다(김경현·문필환과 같은 처리).
                      drop_bones=["_rootJoint"],
                      merge_bones=dict(under="mixamorig:Head", into="mixamorig:Head"),
                      decimate=dict(min_tris=3000, ratio=0.6),                                  # 33,476 → 약 2만
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={2: "material_baseColor.png", 3: "golova_baseColor.png", 4: "material_3_baseColor.png", 0: "material_004_baseColor.png"},
                      materials=dict(textures={"material": [("DiffuseColor", "material_baseColor.png")],
                                               "golova": [("DiffuseColor", "golova_baseColor.png")],
                                               "material_3": [("DiffuseColor", "material_3_baseColor.png")],
                                               ".004": [("DiffuseColor", "material_004_baseColor.png")]})),
    # 바운티러시 후즈후 → 희귀함_양재모(2026-09-16 희귀함 4호). (merge) 형식 pl_ 리그(뼈 66 · 배율 0.01 · 재질 1 · 빈 오브젝트 13).
    #   이미 T자(Upper ±0.013 → Fore ±0.0228 → Hand ±0.0347이 전부 z 0.0349) · 기본 자세 = 쉬는 자세(어긋난 뼈 0, 손 밀림 없음) · PL_RENAME 그대로(발끝 Toe 있음, Toe_02는 이름 유지).
    #   🔴 Head_Face 자손 16개(c_hair_a·f_l/f_r_hair·l/r_hair_a·l_ear·r_ear·Head_fang·Head_jaw)가 **전부 가중치 있음** — 좌우 짝인 귀가 Eye로, Head_jaw가 Jaw로 잡힐 수 있다.
    #     Head_jaw만 이름을 주는 방법도 있지만(가중치 있으니 유니티가 셈), 이치고처럼 merge_bones under로 Head 자손을 통째로 합쳐 후보 자체를 없앤다(휴머노이드 클립은 얼굴·귀·머리카락 뼈를 안 움직인다).
    #   겹친 변형: 얼굴 6벌 → face_normal · 손 5벌 → l/r_hand_open · 손목 2벌 → l/r_wrist(둘 다 편 손과 똑같이 맞물려서 기본판을 남김, _b는 sp용).
    #   남긴 8메시 7,393삼각형이라 감량 없음. 꼬리(tail_01~10, Body_Pelvis 밑)는 그대로 — 뒤로 길어 깊이가 2.5 m쯤 된다(원본 설계).
    #   🔸 텍스처: zip 판과 rar 판 픽셀 완전 동일(최대차 0.0) · 알파는 음영 마스크(최소 0.0 · 평균 0.748 · 98.9%가 0.98 미만) → archive_rgb로 알파 뺌.
    "희귀함_양재모": dict(path="Assets/Art/Units/희귀함_양재모/희귀함_양재모.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_양재모.zip"), "source/whoswho.rar", "whoswho/pl_whoswho_jinj01 (merge).fbx"),
                      archive_rgb={"whoswho/pl_whoswho_jinj01_diff.png": "pl_whoswho_jinj01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02_a", "face_sp02_b",
                                   "l_hand_close", "r_hand_close", "l_hand_sp01", "r_hand_sp01",
                                   "l_hand_sp01_b", "r_hand_sp01_b", "l_hand_sp02", "r_hand_sp02", "l_wrist_b", "r_wrist_b"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      merge_bones=dict(under="mixamorig:Head", into="mixamorig:Head"),
                      materials=dict(textures={"pl_whoswho_jinj01": [("DiffuseColor", "pl_whoswho_jinj01_diff.png")]})),
    # 바운티러시 마르코 → 희귀함_노태현(2026-09-16 희귀함 3호). 류마·아디오와 같은 pl_ 리그(뼈 52 · 배율 0.01 · 재질 2).
    #   이미 T자(Clavicle·Upper·Fore·Hand가 전부 z 0.0169) · 기본 자세 = 쉬는 자세(빡빡이식 손 밀림 없음, 어긋난 뼈 0) · Head_Face 자손 0(얼굴 뼈 겹침 위험 자체가 없다).
    #   🔴 불꽃(스킬) 판을 뺀다: l/r_arm_skill(재질 pl_marco_orig01_skill) · l/r_leg_skill — 기본 대기 모습이 아니고 x ±0.0245까지(몸은 ±0.0029) 퍼져 크기도 망친다.
    #     그러면 불사조 날개 뼈 16개(L/RArm_Wing_01~07·_sup)가 가중치 0으로 남아 「뼈 넘침」 가드에 걸리므로 같이 뺀다(남길 메시엔 가중치 0 — 실측 확인).
    #   겹친 변형: 얼굴 3벌 → face_normal · 손 3벌 → l/r_hand_open. 남긴 8메시 합 3,932삼각형이라 감량 없음.
    #   🔸 텍스처: zip 판(1,550,538 B)과 rar 판(1,550,864 B)은 바이트만 다르고 **픽셀은 완전히 같다**(최대차 0.000000). FBX가 부르는 rar 판 기준.
    #     알파는 음영 마스크(최소 0.247 · 평균 0.834 · 100%가 0.98 미만) → archive_rgb로 알파 뺀 RGB PNG.
    "희귀함_노태현": dict(path="Assets/Art/Units/희귀함_노태현/희귀함_노태현.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "04_희귀함/희귀함_노태현.zip"), "source/pl_marco_orig01.rar", "pl_marco_orig01/pl_marco_orig01.fbx"),
                      archive_rgb={"pl_marco_orig01/pl_marco_orig01_diff.png": "pl_marco_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_arm_skill", "r_arm_skill", "l_leg_skill", "r_leg_skill",
                                   "l_hand_close", "r_hand_close", "r_hand_sp"],
                      drop_bones=[f"{s}Arm_Wing_{k}" for s in ("L", "R") for k in ("01", "02", "03", "04", "05", "06", "07", "sup")],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      materials=dict(textures={"pl_marco_orig01": [("DiffuseColor", "pl_marco_orig01_diff.png")]})),
    # 블리치 이치고 → 희귀함_최상호_오타쿠의길(2026-09-16 희귀함 2호, Sketchfab glb 조인트 296 중 256개에 가중치).
    #   쉬는 자세 A자 45°(어깨 ±0.188/1.446 → 팔꿈치 ±0.374/1.26 → 손 ±0.555/1.079) → tpose_arms로 T자. R이 −X · L이 +X.
    #   🔴 숨김 판 세 개(재질 `5_-` 접두사)를 뺀다: 5_-armRhidden(Object_13) · 5_-WepHand.1(Object_15, y −1.4까지 뻗은 칼) · 5_-WepHand.2(Object_17, y +1.58) — 두면 3m 칼날이 몸을 가로지른다.
    #   🔴 얼굴 뼈 105개(pupil·eyelid·eyebrow·tooth·tongue·jaw·nose…)가 Head 자손 — 아디오와 같은 Jaw/Eye 오매핑 위험. 진짜 눈 뼈 eye R.001_19·eye L.001_51은 **가중치 0**이라
    #     이름만 붙여선 유니티가 뼈로 안 센다(아디오에서 확인) → merge_bones under로 Head 자손을 통째로 Head에 합친다(93개가 가중치 있음, 휴머노이드 클립은 얼굴 뼈를 안 움직이니 겉모습 그대로).
    #   등에 멘 칼(5_WepBack.1/.2)은 남기되 키 잴 때 뺀다(칼 포함 1.826 vs 몸 1.82). 남긴 158,297 삼각형 → 1,000삼각형 이상 메시만 ×0.25로 약 4만.
    "희귀함_최상호_오타쿠의길": dict(path="Assets/Art/Units/희귀함_최상호_오타쿠의길/희귀함_최상호_오타쿠의길.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "04_희귀함/희귀함_최상호_오타쿠의길.glb"), no_nulls=True, orient_snap=True,
                      drop_meshes=["Icosphere", "Object_13", "Object_15", "Object_17"],
                      size_ignore_meshes=["Object_9", "Object_11"],
                      # 숨김 칼 메시를 빼도 그 뼈는 남아 「뼈 넘침」 가드에 걸린다(붕대 뼈 머리가 y +1.57, 칼날 뼈가 y −1.38) — 남는 메시에 가중치 0이라 뺀다
                      drop_bones=[f"bandage0{i} A_{j}" for i, j in zip(range(1, 9), (133, 132, 131, 130, 129, 128, 127, 126))]
                                 + [f"blade0{i} A_{j}" for i, j in zip(range(1, 6), (139, 138, 137, 136, 135))],
                      rename_bones=ICHIGO_RENAME,
                      merge_bones=dict(under="mixamorig:Head", into="mixamorig:Head"),
                      decimate=dict(min_tris=1000, ratio=0.25),
                      tpose_arms={s: dict({"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm",
                                           "Hand": f"mixamorig:{side}Hand"},
                                          **{f"Finger{i}{j}": f"mixamorig:{side}Hand{finger}{k}" for i, finger in enumerate(("Thumb", "Index", "Middle", "Ring", "Pinky"))
                                             for j, k in (("", 1), ("1", 2), ("2", 3))})
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "5_skin_baseColor.png", 1: "5_WepBack_1_baseColor.png", 2: "5_WepBack_2_baseColor.png", 3: "5_torso_baseColor.png",
                                  4: "5_hakama_baseColor.png", 5: "5_face_baseColor.png", 6: "5_feet_baseColor.png", 7: "5_hair_baseColor.png",
                                  8: "5_noseline_baseColor.png", 9: "7_eyeshadow_baseColor.png"},
                      materials=dict(textures={
                          "5_skin_1.0_0_0": [("DiffuseColor", "5_skin_baseColor.png")],
                          "5_armRdefault_1.0_0_0": [("DiffuseColor", "5_skin_baseColor.png")],
                          "5_WepBack.1_1.0_0_0": [("DiffuseColor", "5_WepBack_1_baseColor.png")],
                          "5_WepBack.2_1.0_0_0": [("DiffuseColor", "5_WepBack_2_baseColor.png")],
                          "5_torso_1.0_0_0": [("DiffuseColor", "5_torso_baseColor.png")],
                          "5_sleeves_1.0_0_0": [("DiffuseColor", "5_torso_baseColor.png")],
                          "5_acc_1.0_0_0": [("DiffuseColor", "5_torso_baseColor.png")],
                          "5_hakama_1.0_0_0": [("DiffuseColor", "5_hakama_baseColor.png")],
                          "5_belt_1.0_0_0": [("DiffuseColor", "5_hakama_baseColor.png")],
                          "5_face_1.0_0_0.001": [("DiffuseColor", "5_face_baseColor.png")],
                          "5_eyes_1.0_0_0.001": [("DiffuseColor", "5_face_baseColor.png")],
                          "5_eyebrow_1.0_0_0.001": [("DiffuseColor", "5_face_baseColor.png")],
                          "5_eyelashes_1.0_0_0.001": [("DiffuseColor", "5_face_baseColor.png")],
                          "5_mouth_1.0_0_0.001": [("DiffuseColor", "5_face_baseColor.png")],
                          "5_teeth_1.0_0_0.001": [("DiffuseColor", "5_face_baseColor.png")],
                          "5_feet_1.0_0_0": [("DiffuseColor", "5_feet_baseColor.png")],
                          "5_hair_1.0_0_0": [("DiffuseColor", "5_hair_baseColor.png")],
                          "5_noseline_1_0_0": [("DiffuseColor", "5_noseline_baseColor.png")],
                          "7_eyeshadow_1.0_0_0.001": [("DiffuseColor", "7_eyeshadow_baseColor.png")]})),
    "안흔함_강재규": dict(rev="e8236711", path="Assets/Art/Units/안흔함_강재규/안흔함_강재규.fbx", kind="beast", size=("length", 2.0), anim=True, head="Head_M"),
    "안흔함_이호준": dict(rev="6b2afdbc", path="Assets/Art/Units/안흔함_이호준/안흔함_이호준.fbx", kind="human", size=("height", 1.2), anim=True,
                      hips="Bone_61", head="Bone.004_3", source=os.path.join(SKINS, "02_안흔함/안흔함_이호준.glb"), recipe={}, clip_ground=True),
    "안흔함_김경현": dict(rev="4c92dba1", path="Assets/Art/Units/안흔함_김경현/안흔함_김경현.fbx", kind="human", size=("height", 1.8)),
    "안흔함_김수빈": dict(rev="c6cc54d4", path="Assets/Art/Units/안흔함_김수빈/안흔함_김수빈.fbx", kind="human", size=("height", 1.8), hips="hips_112",
                      source=os.path.join(SKINS, "02_안흔함/안흔함_김수빈.glb"),
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
                      source=os.path.join(SKINS, "03_특별함/특별함_최상호.glb"),
                      # mesh_0(Pupil 582정점·모양 키 3) = Object_7, mesh_0.001(shock 60정점·모양 키 3) = Object_8
                      recipe=dict(rename=LUFFY_RENAME, mesh_alias={"mesh_0": "Object_7", "mesh_0.001": "Object_8"})),
    # 🔴 원인 3겹(2026-09-14 PM 유니티 확인): ①Biped 무게중심 Bip001이 Hips 위에 끼어 엉덩이 높이가 바닥으로 저장 ②팔·다리 메시를 BN_ 보조 뼈가
    #    Pelvis/Clavicle에 나란히 붙어 몰았다 ③살린 Null 뼈 틀 규약이 Biped와 섞여 아바타 skeleton 90° + A자 쉬는 자세 → 넷을 다 켠다(outT3와 같은 설정)
    "흔함_문필환": dict(rev="01d46427", path="Assets/Art/Units/흔함_문필환/흔함_문필환.fbx", kind="human", size=("height", 1.8),
                    drop_bones=["Bip001"], reparent_bones=BIPED_LIMB_REPARENT, tpose_arms=True, null_frames_from_node=True),
    "안흔함_박민수": dict(rev="dd84a0cb", path="Assets/Art/Units/안흔함_박민수/안흔함_박민수.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "02_안흔함/안흔함_박민수.glb"), gltf_guess_bind=False,
                      recipe=dict(rename=DENJI_RENAME)),
    "안흔함_상붕카": dict(path="Assets/Art/Characters/안흔함_상붕카.glb", kind="prop", size=("length", 1.8)),
    # 사이타마(Ready Player Me·Mixamo 리그 glb, 2026-09-14): 번호 꼬리를 떼고 mixamorig 이름으로. 쉬는 자세 A자(위팔 수평 아래 59°, 아래팔 앞 33°) → T자
    #   (손가락 네 줄이 다 있어 손바닥 굴리기까지). 조명용 Icosphere 뺌. Wolf3D_Body 베이스는 1×1 단색 jpg — 원본 바이트 그대로(유니티가 읽음).
    "특별함_배성령": dict(path="Assets/Art/Units/특별함_배성령/특별함_배성령.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "03_특별함/특별함_배성령.glb"), no_nulls=True, drop_meshes=["Icosphere"],
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
                      source=os.path.expanduser("~/Desktop/구랜디스킨모음/03_특별함/특별함_박진웅/source/rin itoshi.fbx"),
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
                      archive=(os.path.join(SKINS, "03_특별함/특별함_조성진.zip"), "source/pl_jozu.7z", "pl_jozu/pl_jozu_orig01.fbx"),
                      archive_rgb={"pl_jozu/pl_jozu_orig01_diff.tga": "pl_jozu_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "l_hand_open", "r_hand_open"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True,
                      materials=dict(textures={"pl_jozu_orig01": [("DiffuseColor", "pl_jozu_orig01_diff.png")]})),
    # 사보 스탬피드(바운티러시 glb): 메시 이름이 전부 Object_N으로 지워져 겹친 변형을 렌더로 판정(idle_a엔 배율 채널이 없어 애니로 못 가림).
    #   (glTF 메시 데이터 이름으로 확인: Object_N = mesh N−6 — 7 face_attack · 8 face_damage · 9 face_normal · 10 goggle · 13 hat_hair · 16/25 glove_open · 20/29 leg · 21 pipe · 22 pipe_weapon · 30/31 sp_leg)
    #   남김: Object_6 body · 9 face_normal · 10 goggle · 11 hair · 12 hat · 13 hat_hair · 16/25 l/r_glove_open · 20/29 l/r_leg · 21 pipe(등에 멤)
    #   뺌: 7 face_attack · 8 face_damage · 14/23 glove_close · 15/24 glove_dragon · 17/26 hand_close · 18/27 hand_dragon · 19/28 hand_open · 22 pipe_weapon · 30/31 sp_leg · Icosphere
    "특별함_박예원": dict(path="Assets/Art/Units/특별함_박예원/특별함_박예원.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "03_특별함/특별함_박예원.glb"), no_nulls=True,
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
                      source=os.path.join(SKINS, "03_특별함/특별함_고우선.glb"), no_nulls=True, drop_meshes=["Object_15", "Icosphere"],
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
                      source=os.path.join(SKINS, "03_특별함/특별함_이정범.glb"), no_nulls=True, drop_meshes=["Object_58", "Icosphere"],
                      rename_regex=(r"(.+?)_[0-9]+", r"\1"),
                      drop_bones=["yixin_weapon_0_nocloth", "rweapon", "Bip001 Prop1", "Bip001"], tpose_arms=True, orient_snap=True,
                      glb_images={0: "yixin_leye_0_baseColor.png", 1: "yixin_mouth_0_baseColor.png", 2: "yixin_reye_0_baseColor.png",
                                  4: "yixin_body_0_baseColor.png", 5: "yixin_face_0_baseColor.png", 6: "yixin_hair_0_baseColor.png"},
                      materials=dict(textures={f"yixin_{k}_0": [("DiffuseColor", f"yixin_{k}_0_baseColor.png")] for k in ("leye", "mouth", "reye", "body", "face", "hair")})),
    # 가로우(원펀맨 게임 추출 glb): 이미 T자. 뿌리 NULL_0133 > RESERVE_0143(가중치 0 중간 뼈)가 WAIST(Hips) 위에 끼어 있다 → 뺀다(흔함_문필환 Bip001 교훈).
    #   조명용 Icosphere 뺌. 텍스처 10장(재질 14가 나눠 씀) → 재질 이름 기준 파일, 공유 이미지는 파일 하나.
    "특별함_유재헌": dict(path="Assets/Art/Units/특별함_유재헌/특별함_유재헌.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "03_특별함/특별함_유재헌.glb"), drop_meshes=["Icosphere"], no_nulls=True,
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
                      archive=(os.path.join(SKINS, "03_특별함/특별함_김용태/source/Jinbei Onigashima.rar"),
                               "Jinbei Onigashima/Jinbei Onigashima by Annettlw.fbx"),
                      archive_textures=["Jinbei Onigashima/pl_jinbe_atta01_diff_hq.png"],
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "l_hand_open", "r_hand_open", "l_hand_sp_01", "r_hand_sp_01", "cup"],
                      rename_bones=PL_RENAME, reparent_bones={"l_arm01": "mixamorig:LeftArm", "r_arm01": "mixamorig:RightArm"},
                      null_frames_from_node=True, orient_snap=True),
    # 쿠르타 입은 인도 남자(Avatar SDK·Mixamo 리그 glb, 2026-09-14): 뼈 이름에 Sketchfab 번호 꼬리 → mixamorig 표준 이름(_rootJoint 밑 Hips 그대로).
    #   쉬는 자세 이미 T자(위팔 수평 아래 2.6°). 조명용 Icosphere 뺌. 텍스처: glb 내장 이미지를 재질 이름 기준 파일로, 노멀은 _normal,
    #   머리카락(haircut)만 알파 컷아웃(원본 alphaMode MASK — 진짜 컷아웃). 눈썹(AvatarEyelashes)은 원본 검정 단색. 삼각형 65,095는 줄이지 않음.
    "특별함_이병준": dict(path="Assets/Art/Units/특별함_이병준/특별함_이병준.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "03_특별함/특별함_이병준.glb"), drop_meshes=["Icosphere"],
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
                      archive=(os.path.join(SKINS, "03_특별함/특별함_이현빈.zip"), "source/nolan.rar", "nolan/pl_noland_sora01 (merge).fbx"),
                      archive_textures=["nolan/pl_noland_sora01_diff.png"], rgb_textures=["pl_noland_sora01_diff.png"],
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "l_hand_close", "r_hand_open", "l_handle_sheath"],
                      rename_bones=PL_RENAME, null_frames_from_node=True, orient_snap=True),
    # Mr.5(바운티러시 pl_ (merge) 리그, 2026-09-14): 이미 T자. 표정 5 · 손 13 변형(주먹·코 파기·코딱지 대포·권총 쥔 손·권총 집어넣는 손). FBX 안에 이펙트 메시는 없다
    #   (rar의 Mesh/·Texture2D/ 스킬 이펙트 OBJ·PNG는 안 옮김). 기본 = 오른손에 권총(r_hand_weapon_revolver + r_weapon_revolver_01) · 왼손 open. 알파 = 명암 마스크 → RGB.
    "특별함_조세민": dict(path="Assets/Art/Units/특별함_조세민/특별함_조세민.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "03_특별함/특별함_조세민.zip"), "source/mr 5.rar", "mr 5/pl_mr5five_orig01 (merge).fbx"),
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
                      archive=(os.path.join(SKINS, "03_특별함/특별함_황정기/source/Usopp Onigashimaa.rar"),
                               "Usopp Onigashima/Usopp Onigashima by Annettlw.fbx"),
                      archive_textures=["Usopp Onigashima/34065_D.png"],
                      drop_bones=["Bip001"], reparent_bones=USOPP_REPARENT, tpose_arms=True),
    # 모리아(바운티러시 pl_ 리그, 2026-09-14): 팔은 이미 T자(팔·손 뼈 같은 높이). 표정·손 모양 변형 메시가 한자리에 겹쳐 있어 기본만 남긴다 —
    #   남김 body·coat·face_normal(웃는 얼굴)·l/r_hand_open, 뺌 = 아래 9개(공격 얼굴·주먹·가위 쥔 손·작은 가위 날). 끝·이펙트 Null은 원래 틀 그대로 뼈로.
    "특별함_임채준": dict(path="Assets/Art/Units/특별함_임채준/특별함_임채준.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "03_특별함/특별함_임채준.zip"), "source/pl_geckomoria_topw01.rar",
                               "pl_geckomoria_topw01/pl_geckomoria_topw01.fbx"),
                      archive_textures=["pl_geckomoria_topw01/pl_geckomoria_topw01_diff.png"],
                      drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close", "l_hand_scissors_open", "l_hand_scissors_close",
                                   "L_scissor", "R_scissor", "L_scissors"],
                      rename_bones=PL_RENAME, null_frames_from_node=True),
    # 미호크: 이미 T자·미터 단위. 「-」 메시 3개(손에 쥔 검·손 칼날 = 공격 변형)를 빼고 「+」(등의 검·목걸이 칼)는 남긴다.
    #   텍스처: 재질 14개가 occ/alb/nmh/spec 4장씩 부르는데 폴더엔 이름 잘린 알베도 4장뿐 → 재질을 7개로 모아 그 4장을 물리고,
    #   없는 머리·수염(hair_kidsalb)·모자 깃털(fur_blend_kidsalb)은 단색. 옷(Body·코트·칼집·목걸이)은 cloth 알베도(원본은 fur_blend를 불렀지만 UV가 cloth 그림).
    "특별함_박기찬": dict(path="Assets/Art/Units/특별함_박기찬/특별함_박기찬.fbx", kind="human", size=("height", 1.8),
                      source=os.path.expanduser("~/Desktop/구랜디스킨모음/03_특별함/특별함_박기찬/source/Mihawk.fbx"), hips="mixamorig:Hips", head="mixamorig:Head",
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
                      source=os.path.join(SKINS, "03_특별함/특별함_박민수.glb"), no_nulls=True, drop_meshes=["Icosphere"], mirror_x=True,
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
                      archive=(os.path.join(SKINS, "03_특별함/특별함_강주혁.zip"), "source/IronMan.fbx"),
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
    # 아디오(바운티러시 pl_ 리그 FBX, zip 속 rar 속 pl_adio_orig01.fbx) — 2026-09-15 새 스킨. 이미 T자·기본 자세 = 쉬는 자세(손 결합 밀림 없음). 뼈 64 · 발끝 뼈 있음 → PL_RENAME.
    #   겹친 변형 13개 뺌: 얼굴 face_attack·face_sp_01·face_sp_02·face_damage(→ face_normal) · 손 close·weapon_01·weapon_02(→ l/r_hand_open) · 쌍권총 weapon_01·weapon_02(기본 대기엔 없음).
    #   빈 오브젝트 13(총구 효과·플래그·머리끝·치마끝 표식)은 뼈로 안 살림.
    #   텍스처: FBX는 .png를 부름(rar 안 같은 이름 .jpeg 판은 안 씀). rar 안 png와 zip textures/ 판이 바이트는 다르지만 픽셀 동일, 알파 전부 1.0(마스크 아님) — RGB로 써도 잃는 것 없음.
    "특별함_송형성": dict(path="Assets/Art/Units/특별함_송형성/특별함_송형성.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "03_특별함/특별함_송형성.zip"), "source/pl_adio_orig01.rar", "pl_adio_orig01/pl_adio_orig01.fbx"),
                      archive_rgb={"pl_adio_orig01/pl_adio_orig01_diff.png": "pl_adio_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_sp_01", "face_sp_02", "face_damage", "l_hand_close", "r_hand_close", "l_hand_weapon_01", "r_hand_weapon_01",
                                   "l_hand_weapon_02", "r_hand_weapon_02", "weapon_01", "weapon_02"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      # 🔴 유니티 아바타 실패(앞머리 f_l_hair_t가 Jaw·LeftEye 겹침) — 2차(HairRoot + 가중치 0 이름 눈·턱 뼈)도 실패 →
                      #   Head 자손 머리카락 15뼈(앞 6 · 뒤 3사슬×3) 가중치를 Head로 합치고 지움. 휴머노이드 Idle은 이 뼈들을 원래 안 움직인다.
                      merge_bones=dict(pattern=r"^(f_[lr]_hair_[bct]|b_[clr]_hair_0[123])$", into="mixamorig:Head"),
                      materials=dict(textures={"pl_adio_orig01": [("DiffuseColor", "pl_adio_orig01_diff.png")]})),
    # 찰로스(바운티러시 pl_ 리그 FBX, zip 속 rar 속 「charlos/pl_charlos_orig01 (merge).fbx」) — 2026-09-15 새 스킨. 이미 T자·기본 자세 = 쉬는 자세(손 결합 밀림 없음).
    #   뼈 60 · 발끝 뼈 있음 → PL_RENAME 그대로. 겹친 변형 16개 뺌: 얼굴 face_sp01·sp02·attack·damage(→ face_normal) · 손 close·sp02·sp03·sp04·r_hand_weapon01(→ l/r_hand_open) ·
    #   총 r_weapon_01 · 콧물 변형 hanamizu_sp. 콧물 hanamizu(찰로스 트레이드마크)·등 탱크 backpack은 남김. 빈 오브젝트 16(총구·비눗방울 효과·플래그)은 뼈로 안 살림.
    #   텍스처: rar 안 _diff.png와 zip textures/ 판이 바이트는 다르지만 픽셀 동일 — 알파 = 명암 마스크(알파<0.98 98.6%·평균 0.856) → RGB PNG.
    "특별함_김태영": dict(path="Assets/Art/Units/특별함_김태영/특별함_김태영.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "03_특별함/특별함_김태영.zip"), "source/charlos.rar", "charlos/pl_charlos_orig01 (merge).fbx"),
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
                      source=os.path.join(SKINS, "03_특별함/특별함_왕승환.glb"), glb_fix_identity_ibm=True, no_nulls=True, drop_meshes=["Icosphere"],
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
                      archive=(os.path.join(SKINS, "03_특별함/특별함_정승준.zip"), "source/pl_ryuma_orig01.rar", "pl_ryuma_orig01/pl_ryuma_orig01.fbx"),
                      archive_rgb={"pl_ryuma_orig01/pl_ryuma_orig01_diff.png": "pl_ryuma_orig01_diff.png"},
                      drop_meshes=["l_hand_close", "r_hand_close", "l_blade", "l_sheath", "r_blade"],
                      rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                      materials=dict(textures={"pl_ryuma_orig01": [("DiffuseColor", "pl_ryuma_orig01_diff.png")]})),
    # 요크(바운티러시 pl_ 리그 FBX, zip 속 rar 속 「pl_york_orig01 (merge).fbx」) — 2026-09-15 사장님 지시로 릴리스 스킨 교체. 이미 T자.
    #   겹친 변형: 얼굴 6벌 → face_normal · 손 4벌 → l/r_hand_open · 오른손 총(r_weapon_gun_01)·총 쥔 손(r_hand_weapon_gun01) 뺌(기본 대기) ·
    #   콧물 풍선(snot_bubble, 자는 연출) 뺌. 고글 렌즈(_trans_goggles 재질)는 남기되 불투명(알파는 명암 마스크라 RGB로).
    #   텍스처: rar 안 _diff.png와 zip textures/ 판이 바이트는 다르지만 픽셀 동일 — 알파 = 명암 마스크(알파<0.98 99.2%) → RGB PNG. 재질 3개가 같은 텍스처.
    "특별함_이지원": dict(path="Assets/Art/Units/특별함_이지원/특별함_이지원.fbx", kind="human", size=("height", 1.8),
                      archive=(os.path.join(SKINS, "03_특별함/특별함_이지원.zip"), "source/york.rar", "york/pl_york_orig01 (merge).fbx"),
                      archive_rgb={"york/pl_york_orig01_diff.png": "pl_york_orig01_diff.png"},
                      drop_meshes=["face_attack", "face_damage", "face_sp01", "face_sp02", "face_sp03", "l_hand_close", "r_hand_close",
                                   "l_hand_open_02", "r_hand_open_02", "r_hand_weapon_gun01", "r_weapon_gun_01", "snot_bubble"],
                      rename_bones=YORK_RENAME, no_nulls=True, orient_snap=True,
                      # pl_york_orig01_trans 재질은 뺀 콧물 풍선만 써서 표에서 뺀다(없는 재질을 걸면 relink가 멈춘다)
                      materials=dict(textures={m: [("DiffuseColor", "pl_york_orig01_diff.png")] for m in ("pl_york_orig01", "pl_york_orig01_trans_goggles")})),
    "특별함_노건완": dict(path="Assets/Art/Units/특별함_노건완/특별함_노건완.fbx", kind="beast", size=("length", 2.0), anim=True, anim_drop_ok=True,
                      source=os.path.join(SKINS, "03_특별함/특별함_노건완.glb"), no_nulls=True, drop_meshes=["Icosphere"], head="Bone.003_02", tail="Bone.008_014", orient_snap=True,
                      pose_from_clip=("Scene", 0, "all"), take_names={"Scene": "Idle"}, clip_scene_basis=True,
                      drop_bones=["_rootJoint"], reparent_bones={"Bone.001_010": "Bone_00"},
                      glb_images={0: "carp_baseColor.png", 2: "carp_normal.png"},
                      materials=dict(textures={"carp": [("DiffuseColor", "carp_baseColor.png"), ("NormalMap", "carp_normal.png")]})),
    "특별함_조도연": dict(path="Assets/Art/Units/특별함_조도연/특별함_조도연.fbx", kind="human", size=("height", 1.8),
                      source=os.path.join(SKINS, "03_특별함/특별함_조도연.glb"), glb_fix_identity_ibm=True, no_nulls=True, drop_meshes=["Icosphere"],
                      squash_chain=dict(bones=[f"thongue_C0_{i}_Jnt_0{26 + i}" for i in range(5)], factor=0.285),
                      pose_from_clip=("Take 001", 0, [f"stach_{s}0_{i}_Jnt_0{base + i}" for s, base in (("R", 31), ("L", 35)) for i in range(4)]),
                      rename_bones=TAHM_RENAME, orient_snap=True, reparent_bones={"chain_C0_0_Jnt_089": "mixamorig:Hips"},
                      tpose_arms={s: {"UpperArm": f"mixamorig:{side}Arm", "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                  for s, side in (("L", "Left"), ("R", "Right"))},
                      glb_images={0: "Body_baseColor.png", 2: "Body_normal.png", 3: "Assets_baseColor.png", 6: "Assets_normal.png"},
                      materials=dict(textures={"Body": [("DiffuseColor", "Body_baseColor.png"), ("NormalMap", "Body_normal.png")],
                                               "Assets": [("DiffuseColor", "Assets_baseColor.png"), ("NormalMap", "Assets_normal.png")]})),
    # 파란 동물 후드 잠옷(키구루미) 캐릭터 glb → 영원_이지원(2026-09-22 영원, blender 세션).
    # 뼈 66(_rootJoint 포함) · 메시 4(+Cube·Icosphere 조명용 더미) · 이미지 4(다 1024²) ·
    # 애니 1(Take 001, 0~23.2프레임 — 안 씀). 이미 mixamorig: 이름에 Sketchfab 번호 꼬리만
    # 붙음(희귀함_배성령/다비와 완전히 같은 계열) → rename_regex로 꼬리 통째로 떼고
    # 손가락 4번·HeadTop_End·Toe_End(전부 (0,0,0) 근처 쓰레기 자리) 드롭.
    # 다리는 레스트에서 이미 거의 수직(UpLeg→Leg→Foot 델타가 거의 -Z 하나로만, x/y 성분
    # 미미 — 영원_김영원처럼 쪼그려 앉은 레스트가 아니라 유니티 Hips 자동 판정 걱정 없음).
    # 팔은 레스트가 수평 T가 아니라 아래로 처져 있어(수평 기준 약 58° 아래, 애니메이션 프레임
    # 이 아니라 진짜 bind pose — 액션은 있지만 edit bone 레스트 값과 무관) tpose_arms로 폄.
    # 재질 eyeSG2(Object_6, 9336정점) · lambert3SG1(Object_7, 6720정점) 둘 다 BaseColor가
    # 이미 Image_0 하나를 같이 씀(직접 확인) — 그 한 장만 내보내 relink.
    "영원_이지원": dict(path="Assets/Art/Units/영원_이지원/영원_이지원.fbx", kind="human", size=("height", 1.8),
                    source=os.path.expanduser("~/Desktop/구랜디스킨모음/10_영원/영원_이지원.glb"),
                    no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                    rename_regex=(r"(mixamorig:[A-Za-z0-9]+?)_[0-9]+", r"\1"),
                    drop_bones=["_rootJoint"],
                    drop_bones_re=r"(HeadTop_End|Thumb4|Index4|Middle4|Ring4|Pinky4|Toe_End)((_end)?_[0-9]+)?$",
                    tpose_arms={s: {"Clavicle": f"mixamorig:{side}Shoulder", "UpperArm": f"mixamorig:{side}Arm",
                                    "Forearm": f"mixamorig:{side}ForeArm", "Hand": f"mixamorig:{side}Hand"}
                                for s, side in (("L", "Left"), ("R", "Right"))},
                    glb_images={0: "jiwon_diffuse.png"},
                    materials=dict(textures={"eyeSG2": [("DiffuseColor", "jiwon_diffuse.png")],
                                             "lambert3SG1": [("DiffuseColor", "jiwon_diffuse.png")]})),
    # 프리파이어 개구리 코스튬(INGAME_ANIMATION_SUPEREMOTE_MALE_FROG) → 영원_김영원(2026-09-22
    # 영원, blender 세션). zip 안 source/*.fbx(뼈 45·메시 3) + textures/Male_Cos_NB_Frog_D.png
    # — FBX 자체 텍스처 참조는 깨져 있음(크기 0×0, 없는 경로) → archive_textures로 같은 zip의
    # textures/에서 직접 꺼내 기본 이름 그대로 relink(재질 표 불필요, 원본 재질이 이미 이
    # 파일명을 그대로 부름).
    # 뼈대: bone_Hips(실가중치 125) 밑으로 bone_Spine → Bip01 Spine1(FROG_RENAME 주석 참고)
    # → bone_Neck → bone_Head, 팔·다리는 린과 같은 bone_ 계열. 손가락 두 마디(bone_·Bip01 각
    # 한 벌씩 실가중치 있음, 렌더링에 안 크게 영향 줘서 FROG_RENAME이 안 다루는 나머지는
    # merge_bones로 Hand에 몰아 접음(관절 없이 손 하나로).
    # 🔴 다리 옆 장식 뼈 8개(Bone002·003·008·009·011·012·014·015, 전부 bone_Spine 자식이라
    # 다리 체인이 아니라 허리에 매달린 장식) — x부호로 좌우 확인(양수=왼쪽, bone_LeftLegUpper
    # x=+0.46로 검증): Bone017·018·014·015(양수)=왼쪽, Bone008·009·011·012(음수)=오른쪽인데
    # 그중 몸 중앙(x≈0)인 Bone002·003만 별도로 척추(Spine)에 붙임. 나머지 8개는 다리를 따라
    # 움직이도록 해당 쪽 Leg에 붙임.
    # 눈·입(Bone_Lefteye·Lefteye001·Mouth, 전부 bone_Head 자식) → Head로 접음.
    # 더미: Cube(단위 정육면체, 정점 그룹 0 — 면이 없어 이 파이프라인은 아예 안 읽음, 린과
    # 같은 증상이라 drop_meshes에 안 넣는다)·Male_Cos_NB_Frog01(118정점, 원점 근처 티끌,
    # Bone001 하나만 묾) — 렌더로 확인, 둘 다 몸통과 무관한 원본 잡동사니라 드롭.
    # bone_Root·Bip01(둘 다 실가중치 0, bone_Hips 위 래퍼) → 드롭해서 Hips를 뿌리로.
    "영원_김영원": dict(path="Assets/Art/Units/영원_김영원/영원_김영원.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.expanduser("~/Desktop/구랜디스킨모음/10_영원/영원_김영원.zip"),
                             "source/INGAME_ANIMATION_SUPEREMOTE_MALE_FROG_Rig.fbx"),
                    archive_textures=["textures/Male_Cos_NB_Frog_D.png"],
                    drop_meshes=["Male_Cos_NB_Frog01"],
                    drop_bones=["bone_Root", "Bip01", "Bone001"],
                    rename_bones=FROG_RENAME,
                    merge_bones=[dict(under="Bone002", into="mixamorig:Spine", with_root=True),
                                 dict(under="Bone017", into="mixamorig:LeftLeg", with_root=True),
                                 dict(under="Bone014", into="mixamorig:LeftLeg", with_root=True),
                                 dict(under="Bone008", into="mixamorig:RightLeg", with_root=True),
                                 dict(under="Bone011", into="mixamorig:RightLeg", with_root=True),
                                 dict(under="Bone_Lefteye", into="mixamorig:Head", with_root=True),
                                 dict(under="Bone_Lefteye001", into="mixamorig:Head", with_root=True),
                                 dict(under="Bone_Mouth", into="mixamorig:Head", with_root=True),
                                 dict(under="bone_Left_Finger01", into="mixamorig:LeftHand", with_root=True),
                                 dict(under="bone_Left_Finger11", into="mixamorig:LeftHand", with_root=True),
                                 dict(under="bone_Right_Finger01", into="mixamorig:RightHand", with_root=True),
                                 dict(under="bone_Right_Finger11", into="mixamorig:RightHand", with_root=True)],
                    # 🔴 PM 재반려 두 번(2026-09-22, 유니티 재검수) — 오브젝트 이름 수정 뒤에도
                    # 유니티가 여전히 Hips를 최상위 오브젝트로 잡고 Chest도 못 찾음(사람 뼈
                    # 20개뿐). level_chain으로 다리·척추를 곧게 세워도(재수입 확인상 무릎 굽힘
                    # 0·완전 수직까지 만들었음) 안 됨 — 두꺼비는 애초에 사람 체형이 아니라서
                    # Humanoid 자체를 포기함(PM 지시). 빅맘·라분과 같은 Generic+합성 Idle로
                    # 전환 — tpose_arms·level_chains 다 빼고 원작의 웅크린 레스트를 그대로 씀
                    # (Idle이 아니라 레스트 자체가 웅크린 두꺼비 자세, 원작 그대로).
                    generic=True,
                    synth_idle=dict(take="Idle", frames=72, step=3, bones={
                        "mixamorig:Spine": [((1, 0, 0), 2.0, 0.0)],
                        "mixamorig:Spine1": [((1, 0, 0), 1.5, -0.4)],
                        "mixamorig:Head": [((1, 0, 0), 1.0, -0.6)]}),
                    seed_zero_bones=0.001, orient_snap=True),
    # 블리치 히츠가야 토시로 만해(효린마루, 얼음 날개) glb → 영원_문필환(2026-09-22 영원,
    # blender 세션). 뼈 110(3ds Max Biped, "Bip001 X_NN" — 갑옷거인과 같은 계열) · 메시
    # 2(Object_9=415121 8,464정점 몸통 전체, Object_11=415121_body 492정점 — 헤드·척추만
    # 물려 있어 속옷/이너 레이어로 추정, 유지) · 이미지 1(512², 토스처 셰이더: Emission+
    # Transparent Mix, BSDF_PRINCIPLED 없음 — DiffuseColor로만 relink해도 원본과 같은 결과,
    # 직접 렌더 확인) · 애니 32(attack·die·run·skill·idle·stay_show 등, 이번엔 안 씀).
    # rename_bones=biped_rename("Bip001", fingers=5, joints=3)(Spine2 없음, 갑옷거인 선례와
    # 같은 골격) + rename_strip으로 glTF 번호 꼬리 제거.
    # 🔴 검(카타나) — Bip001 Prop1(뿌리 자식, "소품" 이름) 밑 Bone037이 실제 칼(Object_9
    # 안에 201정점, 진타 야구방망이와 같은 증상) → drop_verts_of_bones로 정점 지우고 두
    # 뼈 다 드롭.
    # 장식 뼈(BoneNNN 계열, biped_rename 표에 없어 이름만 정리되고 그대로 남음, 전부 실
    # 가중치 있음 확인): Bone038~040(머리 위 z 2.1~2.5, 뾰족한 흰 머리카락 끝) · Bone041~050
    # (척추에서 아래로 길게 늘어지는 사슬, 코트 자락 추정) · Bone052~058·059~065(좌우 대칭,
    # x ±1.7까지 뻗음 — 얼음 날개 한 쌍) · Bone066~087(4쌍, 골반 자식, 짧게 늘어짐 — 하카마
    # 자락/허리끈). 전부 사람 골격이 아니라 Unity Humanoid가 안 다루니 이름만 정리하고 그대로
    # 둠(다른 유닛의 장식 뼈와 같은 처리).
    "영원_문필환": dict(path="Assets/Art/Units/영원_문필환/영원_문필환.fbx", kind="human", size=("height", 1.8),
                    source=os.path.expanduser("~/Desktop/구랜디스킨모음/10_영원/영원_문필환.glb"),
                    no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                    drop_verts_of_bones=["Bone037_0108"],
                    drop_bones=["_rootJoint", "Bip001_02", "Bip001 Prop1_0107", "Bone037_0108"],
                    rename_bones=biped_rename("Bip001", fingers=5, joints=3), rename_strip=r"_[0-9]+$",
                    tpose_arms=biped_tpose_names(fingers=5, joints=3),
                    glb_images={0: "moonpil_diffuse.png"},
                    materials=dict(textures={"415121": [("DiffuseColor", "moonpil_diffuse.png")],
                                             "415121_body": [("DiffuseColor", "moonpil_diffuse.png")]})),
    # 원피스 바운티러시 시키(shiki, pl_shiki_orig01) → 영원_윤현모(2026-09-22 영원, blender
    # 세션). ⚠️ PM 지시: 다리가 칼날 모양인 게 원작 — 무기처럼 빼지 말고 다리로 그대로 둘 것
    # (선례와 달리 이번엔 손에 든 무기 자체가 아예 없음, 렌더로 확인 — 다리 자체가 칼).
    # zip 안 7z(shiki___bounty_rush_by_josoukitsune_dfawyed.7z, bsdtar로 품) 안 pl_
    # 계열 FBX. 뼈대는 표준 PL_RENAME 이름을 쓰지만 LFoot_Toe·RFoot_Toe가 없음(요크와 같은
    # 증상) → YORK_RENAME(ToeBase 뺀 PL_RENAME).
    # 🔴 Body_Waist가 다른 pl_ 유닛과 다르게 world_joint 밑에서 Body_Pelvis와 형제(부모-자식
    # 아님)로 떨어져 있고, 정작 배·가슴·다리·치마·리본은 전부 Body_Waist 자식 — 이대로 두면
    # Hips(Pelvis)와 나머지 몸통이 서로 끊긴 두 갈래가 된다 → reparent_bones로 Body_Waist를
    # Hips 밑에 붙임.
    # 코트(coat_root 서브트리, 21개) → Spine1 강체(아카이누 선례). 치마(b/f_l/r_skirt) →
    # Hips 강체. 리본(waist 허리끈, ribbon 서브트리) → Hips 강체. 하카마(l/r_hakama, 넓적다리·
    # 종아리 자식)는 이미 다리 뼈에 물려 있어 손 안 댐. 소매(l/r_sode)도 마찬가지로 손 안 댐.
    # 머리카락 5갈래(b/f_l/f_r_hair)·수염(beard_joint)·담배(cigar_joint) → Head.
    # world_joint·HELPER_key·HELPER_name(가중치 0, 원본 최상위 pl_shiki_orig01 뼈까지) 드롭
    # 해서 Hips를 뿌리로.
    "영원_윤현모": dict(path="Assets/Art/Units/영원_윤현모/영원_윤현모.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.expanduser("~/Desktop/구랜디스킨모음/10_영원/영원_윤현모.zip"),
                             "source/shiki___bounty_rush_by_josoukitsune_dfawyed.7z",
                             "pl_shiki_orig01/pl_shiki_orig01.fbx"),
                    archive_rgb={"textures/pl_shiki_orig01_diff.png": "pl_shiki_orig01_diff.png"}, archive_rgb_outer=True,
                    drop_meshes=["face_attack", "face_damage", "face_sp", "l_hand_close", "l_hand_sp",
                                 "r_hand_close", "r_hand_sp"],
                    drop_bones=["pl_shiki_orig01", "world_joint", "HELPER_key", "HELPER_name"],
                    rename_bones=YORK_RENAME,
                    reparent_bones={"Body_Waist": "mixamorig:Hips"},
                    merge_bones=[dict(under="coat_root", into="mixamorig:Spine1", with_root=True),
                                 dict(under="b_l_skirt", into="mixamorig:Hips", with_root=True),
                                 dict(under="b_r_skirt", into="mixamorig:Hips", with_root=True),
                                 dict(under="f_l_skirt", into="mixamorig:Hips", with_root=True),
                                 dict(under="f_r_skirt", into="mixamorig:Hips", with_root=True),
                                 dict(under="ribbon", into="mixamorig:Hips", with_root=True),
                                 dict(under="b_c_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="b_l_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="b_r_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="f_l_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="f_r_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="beard_joint", into="mixamorig:Head", with_root=True),
                                 dict(under="cigar_joint", into="mixamorig:Head", with_root=True)],
                    no_nulls=True, orient_snap=True, seed_zero_bones=0.001,
                    materials=dict(textures={"pl_shiki_orig01": [("DiffuseColor", "pl_shiki_orig01_diff.png")]})),
    # 나비/나방 날개 달린 여성 캐릭터(긴 갈래머리, 검은 코트) glb → 영원_최상호(2026-09-22
    # 영원, blender 세션). 문필환과 완전히 같은 계열(3ds Max Biped "Bip001 X_NN") — 뼈 120
    # · 메시 2(Object_9=525241 12,224정점 몸통, Object_11=525241_body 818정점 — 이너
    # 레이어, 유지) · 이미지 1(512², Emission+Transparent Mix 토스처 셰이더, 문필환과 같음)
    # · 애니 14(idle 포함, 안 씀). 손에 든 무기 없음(렌더로 확인, 양손 다 빈손).
    # 🔴 Bip001 Head_014_0 — Bip001 Head_014와 세계 위치가 완전히 같은 중복 뼈인데 실가중치가
    # 있어(배치 1차 시도에서 assert로 발견) 드롭은 못 하고 Head로 합침.
    # 장식 뼈(BoneNNN, biped_rename 표에 없어 번호 꼬리만 정리되고 그대로 남음, 전부 사람
    # 골격 아님, 문필환처럼 이름만 정리하고 둠): Bone001~009(머리에서 아래로 길게 늘어지는
    # 갈래머리, 무릎 아래까지) · Bone058~065(머리 뒤쪽 짧은 머리채) · Bone046~057(등에서
    # 뻗는 나비 날개 3쌍) · Bone010~045(骨반 자식 6갈래, 코트 자락 앞뒤·옆).
    "영원_최상호": dict(path="Assets/Art/Units/영원_최상호/영원_최상호.fbx", kind="human", size=("height", 1.8),
                    source=os.path.expanduser("~/Desktop/구랜디스킨모음/10_영원/영원_최상호.glb"),
                    no_nulls=True, orient_snap=True, drop_meshes=["Icosphere"],
                    drop_bones=["_rootJoint", "Bip001_02"],
                    rename_bones=biped_rename("Bip001", fingers=5, joints=3, spine2="Spine2"),
                    rename_strip=r"_[0-9]+$",
                    merge_bones=[dict(under="Bip001 Head_014_0", into="mixamorig:Head", with_root=True)],
                    tpose_arms=biped_tpose_names(fingers=5, joints=3),
                    glb_images={0: "sangho_diffuse.png"},
                    materials=dict(textures={"525241": [("DiffuseColor", "sangho_diffuse.png")],
                                             "525241_body": [("DiffuseColor", "sangho_diffuse.png")]})),
    # 원피스 바운티러시 아틀라스(베가펑크 위성 '폭력', pl_atlas_orig01) → 히든_전주연
    # (2026-09-22 히든, blender 세션). zip 안 rar(bsdtar로 품 — extract_archive는 이미
    # bsdtar만 씀, 7z 안 거침·폴백 불필요 확인) 안 pl_ 표준 계열(Body_Pelvis, PL_RENAME
    # 그대로, 번호 꼬리 없음 — 다른 pl_ 유닛과 같은 뼈대, 우루루의 Body_Waist 같은 함정 없음).
    # 손에 든 무기 없음(메시 목록에 weapon류 없음, rockat=등에 멘 로켓 추진체라 신체 일부로
    # 유지 — rocket_joint 서브트리도 안 건드림).
    "히든_전주연": dict(path="Assets/Art/Units/히든_전주연/히든_전주연.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_전주연.zip"),
                             "source/atlas.rar", "atlas/pl_atlas_orig01 (merge).fbx"),
                    archive_rgb={"atlas/pl_atlas_orig01_diff.png": "pl_atlas_orig01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "face_sp01", "l_hand_close", "l_hand_sp01",
                                 "r_hand_close", "r_hand_sp01"],
                    # 🔴 1차 배치는 통과했지만 재수입해서 확인하니 Hips 부모가 world_joint로
                    # 남아 있었음(다른 pl_ 유닛처럼 드롭을 깜빡함) — 오늘 교훈 그대로 적용해
                    # 드롭, Hips를 진짜 뿌리로.
                    drop_bones=["world_joint"],
                    rename_bones=YORK_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(pattern=r"^(b_collar|f_collar|l_collar|r_collar)$", into="mixamorig:Spine1"),
                                 dict(pattern=r"^(b_l_skirt_01|b_r_skirt_01|f_l_skirt_01|f_r_skirt_01|s_l_skirt_01|s_r_skirt_01)$",
                                      into="mixamorig:Hips"),
                                 dict(under="b_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="c_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="r_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="l_antenna_01", into="mixamorig:Head", with_root=True),
                                 dict(under="l_ear_01", into="mixamorig:Head", with_root=True),
                                 dict(under="r_ear_01", into="mixamorig:Head", with_root=True),
                                 dict(under="l_hail_01", into="mixamorig:Head", with_root=True),
                                 # 🔴 PM 진단(유니티 실측) — rocket_joint 밑에 l/r_rocket_joint
                                 # 두 자식이 갈라져 있는 모양이 유니티 자동 매핑엔 "머리+눈 둘"
                                 # 구조로 보여서 Head가 진짜 mixamorig:Head가 아니라
                                 # rocket_joint로 잘못 잡혔음(Idle 고개 끄덕임이 로켓팩에
                                 # 걸리고 진짜 머리는 기본 자세로 남아 숙여진 것처럼 보인
                                 # 원인). 등 로켓팩을 몸통(Spine1) 강체로 합쳐서 그 구조
                                 # 자체를 없앤다.
                                 dict(under="rocket_joint", into="mixamorig:Spine1", with_root=True)],
                    materials=dict(textures={"pl_atlas_orig01": [("DiffuseColor", "pl_atlas_orig01_diff.png")]})),
    # 원피스 야마토(pl_aceyamato_yamato_doub01, 바운티러시) → 히든_여은서(2026-09-22 히든,
    # blender 세션). zip 안 rar(bsdtar로 품) 안 "Yamato by Annettlw.fbx"(공백 있음).
    # "doub01"은 에이스+야마토 더블 캐릭터 파일 이름일 뿐 — 렌더로 직접 확인, 메시 목록에
    # 에이스 관련 요소 없음(body·horn·earring·bottle_sake 등 전부 야마토 단독분), 따로 뺄 것
    # 없음.
    # 쇠몽둥이(카나보, weapon 메시) — 렌더로 확인, 오른손으로 실제 휘두르는 자세라 무기로
    # 드롭(오른손 뼈 밑 r_weapon_joint는 무게 0으로 남아도 무해, 안 건드림).
    # 술병(bottle_sake, L_bottle 뼈로 왼손목에 작은 사슬로 매달림) — 렌더로 확인, 쥔 무기가
    # 아니라 손목에 매달린 장신구라 유지. 어깨에 걸친 구슬 사슬 장식(l/r_chain_01~03, 팔
    # 자식)도 유지 — 이미 팔 뼈에 실려 있어 안 건드림.
    "히든_여은서": dict(path="Assets/Art/Units/히든_여은서/히든_여은서.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_여은서.zip"),
                             "source/Yamato.rar", "Yamato/Yamato by Annettlw.fbx"),
                    archive_rgb={"Yamato/pl_aceyamato_yamato_doub01_diff.png": "pl_aceyamato_yamato_doub01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "face_sp", "face_sp02", "face_sp03",
                                 "l_hand_close", "l_hand_sp01", "r_hand_close", "r_hand_sp01", "weapon"],
                    # 🔴 world_joint 하나만 드롭했더니(1차 배치) 재수입 확인에서 Hips 부모가
                    # 그 위의 진짜 최상위 뼈(아마추어 이름과 같은 "pl_aceyamato_yamato_doub01",
                    # 시키 때와 같은 증상)로 남아 있었음 — 같이 드롭.
                    drop_bones=["pl_aceyamato_yamato_doub01", "world_joint", "HELPER_key", "HELPER_name"],
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(under="b_c_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="f_l_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="f_r_hair_01", into="mixamorig:Head", with_root=True),
                                 dict(under="l_earring", into="mixamorig:Head", with_root=True),
                                 dict(under="r_earring", into="mixamorig:Head", with_root=True),
                                 dict(under="l_breast_01", into="mixamorig:Spine1", with_root=True),
                                 dict(under="r_breast_01", into="mixamorig:Spine1", with_root=True),
                                 dict(under="l_skirt_b_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="l_skirt_f_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="l_skirt_s_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="r_skirt_b_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="r_skirt_f_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="r_skirt_s_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="l_tuna_under_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="l_tuna_upper_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="r_tuna_under_01", into="mixamorig:Hips", with_root=True),
                                 dict(under="r_tuna_upper_01", into="mixamorig:Hips", with_root=True)],
                    materials=dict(textures={"pl_aceyamato_yamato_doub01":
                                             [("DiffuseColor", "pl_aceyamato_yamato_doub01_diff.png")]})),
    # 원피스 바운티러시 가짜 루피(데마로 블랙, pl_demaroblack_orig01) → 히든_한나웅
    # (2026-09-22 히든, blender 세션). zip 안 rar(bsdtar로 품) 안 "fake luffy" 폴더 안
    # "pl_demaroblack_orig01 (merge).fbx"(공백·괄호 있는 파일명).
    # 🔴 아틀라스 때 발견한 "(merge)" 관련 90°회전+0.01배율 구조 — 이번엔 별도 빈 오브젝트가
    # 아니라 아마추어 오브젝트 자신에게 바로 있음(이름도 "...(merge)" 그대로). matrix_world
    # 굽기는 어느 쪽이든 결과가 같아서(직접 확인) 이 패턴 자체는 "(merge)" 파일들의 공통
    # 내보내기 흔적으로 보이고 정면·좌우 반전의 직접 원인은 아닌 듯 — 계속 지켜볼 것.
    # 무기(가짜 권총류, 왼손 전용) — l_hand_weapon·l_weapon_01·l_weapon_02(935+704정점,
    # 꽤 큼) 렌더 없이도 이름·정점 규모로 명백, 손 변형 중 close/sp01도 같이 드롭.
    "히든_한나웅": dict(path="Assets/Art/Units/히든_한나웅/히든_한나웅.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_한나웅.zip"),
                             "source/fake luffy.rar", "fake luffy/pl_demaroblack_orig01 (merge).fbx"),
                    archive_rgb={"fake luffy/pl_demaroblack_orig01_diff.png": "pl_demaroblack_orig01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "face_sp01", "l_hand_close", "l_hand_sp01",
                                 "r_hand_close", "r_hand_sp01", "l_hand_weapon", "l_weapon_01", "l_weapon_02"],
                    drop_bones=["pl_demaroblack_orig01", "world_joint"],
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(pattern=r"^[bfs]_[lr]_coat_01$", into="mixamorig:Spine"),
                                 dict(under="c_collar_01", into="mixamorig:Spine1", with_root=True),
                                 dict(under="c_hat_01", into="mixamorig:Head", with_root=True)],
                    materials=dict(textures={"pl_demaroblack_orig01": [("DiffuseColor", "pl_demaroblack_orig01_diff.png")]})),
    # 원펀맨 게임 립 제노스(데몬 사이보그) → 히든_최경범(2026-09-22 히든, blender 세션).
    # glb 하나(뼈 296·스킨 1·애니 0·메시 18·재질 7·이미지 3). 뼈 이름은 이미 mixamorig
    # 계열(Hips·Spine·LeftShoulder 등)에 번호 꼬리만 붙음("Hips_00" — "mixamorig:" 접두는
    # 없음) → rename_regex로 접두 붙이며 꼬리 제거.
    # 🔴 메시 18개 중 상당수가 LOD 중복(PM 사전조사와 일치) — glTF 재질 이름에 LOD가 실제로
    # 안 남는 것(Hair01·Metal·Body)도 있어 재질만으론 못 가르고, 정점 수 내림차순(LOD00이
    # 가장 조밀하다는 표준 관례) + 가져오기 로그 순서로 각 쌍을 대조해 판정:
    #   Face01: Object_8(4,019·LOD00, 재질에 "_LOD00" 명시돼 있어 확실) 유지 · Object_10
    #     (2,307·LOD01)·Object_12(1,462·LOD02) 드롭.
    #   Hair01: Object_18(15,125) 유지 · Object_20(6,741)·Object_22(5,347) 드롭(재질 이름에
    #     LOD 구분 없어 정점 수로만 판정 — 세 배 가까이 차이나 확신 있음).
    #   GENOS04_Metal: Object_32(18,756) 유지 · Object_34(7,052)·Object_36(12,671) 드롭.
    #   GENOS04_Body: Object_38(6,338) 유지 · Object_40(2,586)·Object_42(4,416) 드롭.
    #   Teeth·Tongue·눈 등 작은 얼굴 부속(Object_14·16·30, 재질 "MAT_..._Face"로 LOD00과
    #   다름 — LOD 계열 아니라 별개 부속) + Plug01~03(목·머리 케이블 꽂이, PM 지시대로
    #   신체 유지) 전부 남김. Plug04는 gltf 가져오기 로그엔 있는데 실제 오브젝트가 안
    #   생김(면 없음 추정, 이번 세션에 여러 번 본 Cube 증상과 같음) — drop_meshes에 안 넣음.
    # 팔에 박힌 무기(소이포 캐논 등)는 GENOS04_Metal 메시 자체(제노스의 기계 팔)라 원래도
    # 따로 뺄 대상이 없음(별도 이펙트 메시 없음, 확인).
    # 🔸 재질별 이미지는 glTF에서 직접 읽음(덴지 교훈): MAT_..._Face_LOD00·MAT_..._Face
    #   둘 다 Image_0 · MAT_..._Hair01 → Image_1 · MAT_..._Body·MAT_..._Meta01 둘 다 Image_2
    #   (금속·피부가 같은 아틀라스 공유). 원본 재질에 Principled BSDF 자체가 없어(이모션/
    #   언릿 계열 셰이더로 보임) Metallic 스칼라를 블렌더에서 못 읽음 — 유니티에서 직접
    #   Metallic 값 확인 필요(PM 요청 사항, 제가 여기서는 확인 불가).
    "히든_최경범": dict(path="Assets/Art/Units/히든_최경범/히든_최경범.fbx", kind="human", size=("height", 1.8),
                    source=os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_최경범.glb"),
                    no_nulls=True, orient_snap=True,
                    drop_meshes=["Object_10", "Object_12", "Object_20", "Object_22",
                                 "Object_34", "Object_36", "Object_40", "Object_42", "Icosphere"],
                    drop_bones=["_rootJoint"],
                    rename_regex=(r"([A-Za-z][A-Za-z0-9_]*?)_[0-9]+", r"mixamorig:\1"),
                    # Hips_00 자체는 가중치 0(자식 Hipsd_01이 실제 엉덩이 가중치를 쥠, 린의
                    # bone_Hips_Dummy와 같은 패턴) → 안전판.
                    seed_zero_bones=0.001,
                    glb_images={0: "genos_face.png", 1: "genos_hair.png", 2: "genos_body_metal.png"},
                    materials=dict(textures={
                        "MAT_HERO_GENOS01_Face_LOD00": [("DiffuseColor", "genos_face.png")],
                        "MAT_HERO_GENOS01_Face": [("DiffuseColor", "genos_face.png")],
                        "MAT_HERO_GENOS01_Hair01": [("DiffuseColor", "genos_hair.png")],
                        "MAT_HERO_GENOS04_Body": [("DiffuseColor", "genos_body_metal.png")],
                        "MAT_HERO_GENOS04_Meta01": [("DiffuseColor", "genos_body_metal.png")]})),
    # 원피스 바운티러시 알비다(pl_alvida_orig01) → 히든_석성례(2026-09-22 히든, blender
    # 세션). zip 안 rar(bsdtar로 품) 안 pl_alvida_orig01.fbx. 표준 pl_ 계열, world_joint가
    # 바로 뿌리(그 위에 별도 아마추어 이름 뼈 없음 — 시키·야마토와 다름, 직접 확인), Toe
    # 뼈 있음.
    # 철퇴(weapon_01·weapon_02, 오른손 RHand_Weapon01 뼈) — 렌더 없이도 이름·정점 규모로
    # 명백해 드롭.
    "히든_석성례": dict(path="Assets/Art/Units/히든_석성례/히든_석성례.fbx", kind="human", size=("height", 1.8),
                    archive=(os.path.expanduser("~/Desktop/구랜디스킨모음/05_히든/히든_석성례.zip"),
                             "source/pl_alvida_orig01.rar", "pl_alvida_orig01/pl_alvida_orig01.fbx"),
                    archive_rgb={"pl_alvida_orig01/pl_alvida_orig01_diff.png": "pl_alvida_orig01_diff.png"},
                    drop_meshes=["face_attack", "face_damage", "l_hand_close", "r_hand_close",
                                 "weapon_01", "weapon_02"],
                    drop_bones=["world_joint"],
                    # 🔴 1차 배치: "기본 자세≠쉬는 자세" 2.1991(다른 pl_ 유닛은 0.0) — 렌더로
                    # 확인하니 몸 전체가 대각선으로 기울어진 채 굳어 있었음(가져온 기본 자세가
                    # 진짜 쉬는 자세가 아니라 애니메이션 중간 프레임으로 보임) → use_rest_pose로
                    # 진짜 결합 자세를 씀.
                    use_rest_pose=True,
                    rename_bones=PL_RENAME, no_nulls=True, orient_snap=True,
                    merge_bones=[dict(under="Bellyband", into="mixamorig:Hips", with_root=True),
                                 dict(under="Body_Bust", into="mixamorig:Spine1", with_root=True),
                                 dict(under="CHair01", into="mixamorig:Head", with_root=True),
                                 dict(under="LSide_Hair", into="mixamorig:Head", with_root=True),
                                 dict(under="RSide_Hair", into="mixamorig:Head", with_root=True),
                                 dict(under="LScarf01", into="mixamorig:Spine1", with_root=True),
                                 dict(under="RScarf01", into="mixamorig:Spine1", with_root=True)],
                    materials=dict(textures={"pl_alvida_orig01": [("DiffuseColor", "pl_alvida_orig01_diff.png")]})),
}
HIPS = re.compile(r"(?i)(^|[:_ .])(hips?|pelvis)($|[_ .0-9])")
HEAD = re.compile(r"(?i)(^|[:_ .])head($|[_ .0-9])")
UPPER = re.compile(r"(?i)(up_?leg|upper_?leg|thigh|upper_?arm|[:_ ]arm$|shoulder|clavicle|bone\.029|bone\.006)")
LEFT = re.compile(r"(?i)(left|(^|[:_ ])l([:_ ]|$)|\.l($|_)|_l($|_)|^l(arm|leg))")
RIGHT = re.compile(r"(?i)(right|(^|[:_ ])r([:_ ]|$)|\.r($|_)|_r($|_)|^r(arm|leg))")
SKIP = ("end", "top", "tweak", "mch", "org", "pole", "widget", "adj", "vis_")


def load(path, anim, guess_bind=True):
    if path.lower().endswith((".blend", ".blend1")):                   # 🔸 오카베(2026-09-16): 원본 블렌더 파일을 그대로 연다(FBX보다 Rigify·수정자 정보가 온전)
        bpy.ops.wm.open_mainfile(filepath=path, load_ui=False)
        bpy.context.view_layer.update()
        return
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


def _find_texture(tex_dir, fallback_dir, fn):
    """🔴 흰수염(2026-09-22) — --out 시험 때 tex_dir(=tex_out_dir)이 방금 이번 실행이 쓴
    파일만 있는 빈 폴더일 수 있다(archive_rgb 등을 안 쓰는 유닛이 이미 커밋된 실제 Textures/
    파일을 참조하는 경우). tex_dir에 없으면 fallback_dir(실제 커밋 경로)에서 읽기만 한다
    (기존 유닛 --out 회귀 시험이 깨지지 않게 — 쓰기는 절대 fallback_dir로 안 감)."""
    file = os.path.join(tex_dir, fn)
    if os.path.isfile(file):
        return file
    if fallback_dir:
        alt = os.path.join(fallback_dir, fn)
        if os.path.isfile(alt):
            return alt
    return file


def relink_textures(table, tex_dir, fallback_dir=None):
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
            file = _find_texture(tex_dir, fallback_dir, fn)
            assert os.path.isfile(file), f"{mat_name}: 텍스처 파일이 없다 {file}"
            getattr(new, FBX_TEX_SLOT[prop]).image = bpy.data.images.load(file, check_existing=True)
            done.append(f"{mat_name}.{prop}={fn}")
    return done


def build_materials(spec, tex_dir, fallback_dir=None):
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
    done["텍스처"] = relink_textures(spec["textures"], tex_dir, fallback_dir)
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


def level_chain(arm, chain, target, report=None, report_key=None):
    """뼈 사슬(부모→자식 이름 순서)을 target 방향으로 곧게 편다(영원_김영원, PM 2026-09-22):
    쪼그려 앉은 레스트(무릎 굽음·척추가 골반에서 비껴남)는 유니티 휴머노이드 자동 매핑이
    Hips 판정에 실패해 최상위 오브젝트를 Hips로 잘못 잡는 원인이 된다 — tpose_arms와 같은
    회전 방식(사슬의 부모 뼈를 통째로 돌려 자식이 target 방향을 보게)을 다리·척추 등
    임의의 사슬에 재사용."""
    pose = arm.pose.bones

    def P(n):
        return arm.matrix_world @ pose[n].head

    def turn(n, R3):
        pivot = P(n)
        pose[n].matrix = Matrix.Translation(pivot) @ R3.to_4x4() @ Matrix.Translation(-pivot) @ pose[n].matrix
        bpy.context.view_layer.update()

    bpy.context.view_layer.update()
    moved = []
    for a, b in zip(chain, chain[1:]):
        cur = P(b) - P(a)
        if cur.length > 1e-6:
            turn(a, cur.normalized().rotation_difference(Vector(target).normalized()).to_matrix())
            moved.append(a)
    if report is not None:
        report[report_key or "곧게 편 사슬"] = moved
    return moved


def refit_size(arm, meshes, cfg, report):
    """level_chain으로 다리를 펴면 키가 늘어나(웅크린 자세보다 커짐) 진작 재 놓은 크기·바닥
    맞춤(G 변환, cfg["size"] 기준)이 틀어진다 — 자세를 다 바꾼 뒤 여기서 다시 잰다. 회전만
    쓰는 turn()과 달리 배율까지 필요해서 루트 뼈 세계 행렬에 배율+이동을 왼쪽곱(자식은
    부모를 따라간다, turn()과 같은 원리). 🔴 pose.matrix만 바꾸면 FBX 내보내기가 읽는
    edit bone(레스트)엔 안 남는다(tpose_arms에서 배운 교훈) — tpose_arms의 굽기 순서
    그대로 재사용: 변형된 메시를 새 데이터로 굽고, 자세를 edit bone(레스트)에 박은 뒤
    pose를 항등으로 되돌린다."""
    pose = arm.pose.bones
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in meshes:
        eo = o.evaluated_get(depsgraph)
        me = eo.to_mesh()
        pts.extend(eo.matrix_world @ v.co for v in me.vertices)
        eo.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    axis, target = cfg["size"]
    current = (hi - lo).z if axis == "height" else max((hi - lo).x, (hi - lo).y)
    s = target / max(current, 1e-12)
    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    M = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo.z))
    # 뿌리 뼈가 여럿일 수 있다(영원_김영원: mixamorig:Hips 말고 Null→뼈로 살아난 Point001도
    # 부모 없는 뿌리) — 엉덩이만 정확히 골라야 한다.
    hips = pick(arm, cfg.get("hips"), HIPS)
    root = pose[hips.name] if hips is not None else next(b for b in pose if b.parent is None)
    root.matrix = M @ root.matrix
    bpy.context.view_layer.update()

    dg = bpy.context.evaluated_depsgraph_get()
    posed_verts = {}
    for m in meshes:
        assert not m.data.shape_keys, f"{m.name}: 모양 키가 있어 재맞춤으로 굽지 못한다"
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
        h, t, Mb = rest[eb.name]
        eb.head, eb.tail = h, t if (t - h).length > 1e-9 else h + Mb.col[1].xyz * 1e-3
        eb.align_roll(Mb.col[2].xyz)
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in pose:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    dg = bpy.context.evaluated_depsgraph_get()
    drift = 0.0
    for m in meshes:
        ev = m.evaluated_get(dg)
        me = ev.to_mesh()
        drift = max(drift, max((ev.matrix_world @ v.co - m.matrix_world @ c).length for v, c in zip(me.vertices, posed_verts[m.name])))
        ev.to_mesh_clear()
    assert drift < 1e-4, f"재맞춤 굽기 뒤 메시가 {drift:.5f} 움직였다"
    report["다리 편 뒤 재맞춤"] = {"배율": round(s, 4), "이동": [round(-cx, 4), round(-cy, 4), round(-lo.z, 4)], "굽기_오차": round(drift, 6)}


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


def blend_prep(cfg, report):
    """🔸 오카베(Rigify 「생성된」 리그 .blend, 2026-09-16): 변형 뼈만 남기고 사람형 계층으로 다시 잇는다.
    ① 메시·아마추어 아닌 오브젝트 삭제 ② 외곽선 Solidify·지오메트리 노드·대상 없는 Armature 수정자 삭제, 면이 안 쓰는 재질 칸(Outline) 삭제
    ③ 포즈 뼈 제약·드라이버 삭제(ORG/MCH를 지우면 깨진다) ④ 변형 뼈가 아닌 정점 그룹 삭제(FBX는 use_deform=False 그룹도 클러스터로 쓴다 — 머리 `face` 32%) → 빈 정점은 같은 메시 가까운 정점 복사
    ⑤ 변형 뼈를 parents 표(없으면 가장 가까운 변형 조상)로 다시 붙이고 나머지 뼈 삭제."""
    from mathutils.kdtree import KDTree
    bp = cfg["blend_prep"]
    scene = bpy.context.scene
    for o in [o for o in scene.objects if o.type not in ("MESH", "ARMATURE")]:
        bpy.data.objects.remove(o, do_unlink=True)
    arm = main_armature()
    deform = {b.name for b in arm.data.bones if b.use_deform}
    stats = dict(수정자=0, 재질칸=0, 그룹=0, 채운정점=0)
    for m in [o for o in scene.objects if o.type == "MESH"]:
        for mod in list(m.modifiers):
            if mod.type in ("SOLIDIFY", "NODES") or (mod.type == "ARMATURE" and mod.object is None):
                m.modifiers.remove(mod)
                stats["수정자"] += 1
        if m.data.shape_keys and m.data.shape_keys.animation_data:
            m.data.shape_keys.animation_data_clear()
        used = {p.material_index for p in m.data.polygons}
        for i in reversed(range(len(m.data.materials))):
            mat = m.data.materials[i]
            if i not in used and (mat is None or mat.name in set(bp.get("drop_unused_materials", ()))):
                m.data.materials.pop(index=i)
                for p in m.data.polygons:
                    if p.material_index > i:
                        p.material_index -= 1
                stats["재질칸"] += 1
        for g in [g for g in m.vertex_groups if g.name not in deform]:
            m.vertex_groups.remove(g)
            stats["그룹"] += 1
        tot = [sum(ge.weight for ge in v.groups) for v in m.data.vertices]
        full = [i for i, t in enumerate(tot) if t > 1e-6]
        empty = [i for i, t in enumerate(tot) if t <= 1e-6]
        if empty and full:
            kd = KDTree(len(full))
            for i in full:
                kd.insert(m.data.vertices[i].co, i)
            kd.balance()
            for i in empty:
                j = kd.find(m.data.vertices[i].co)[1]
                for ge in m.data.vertices[j].groups:
                    m.vertex_groups[ge.group].add([i], ge.weight, "REPLACE")
            stats["채운정점"] += len(empty)
        elif empty:
            g = m.vertex_groups.get(bp["fallback_bone"]) or m.vertex_groups.new(name=bp["fallback_bone"])
            g.add(empty, 1.0, "REPLACE")
            stats["채운정점"] += len(empty)
    for mat_name, rgb in bp.get("solid_colors", {}).items():          # 툰 노드(Shader to RGB→Ramp→Mix)는 FBX가 못 읽어 뷰포트 회색(0.8)이 된다 → 밝은 쪽 색을 Principled 기본색으로
        mat = bpy.data.materials[mat_name]
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        nt.links.new(bsdf.outputs[0], out.inputs[0])
        mat.diffuse_color = (*rgb, 1.0)
        stats.setdefault("단색", []).append(mat_name)
    if arm.animation_data:
        arm.animation_data_clear()
    for pb in arm.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
        pb.matrix_basis = Matrix.Identity(4)
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    parents = bp.get("parents", {})
    for name in deform:
        b = eb[name]
        if name in parents:
            b.parent = eb[parents[name]]
        else:
            p = b.parent
            while p is not None and p.name not in deform:
                p = p.parent
            b.parent = p
        b.use_connect = False
    for b in [b for b in eb if b.name not in deform]:
        eb.remove(b)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    stats["남은 뼈"] = len(arm.data.bones)
    report["블렌드 정리"] = stats


HUMANOID_REQUIRED = ["Hips", "Spine", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand",
                     "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot"]


def humanoid_weight_check(name, meshes):
    """🔴 오비토(2026-09-17 PM 유니티 isHuman False): 가중치 0 매핑 뼈는 FBX 스킨 뼈 목록에서 빠져 유니티 휴머노이드 매핑 후보가 안 된다 →
    필수 15뼈(mixamorig: 이름)가 전부 어떤 메시에든 가중치 > 0이어야 통과."""
    got = set()
    for m in meshes:
        idx = {g.index: g.name for g in m.vertex_groups}
        for v in m.data.vertices:
            for ge in v.groups:
                if ge.weight > 0:
                    got.add(idx[ge.group])
    missing = [b for b in HUMANOID_REQUIRED if "mixamorig:" + b not in got]
    assert not missing, f"{name}: 휴머노이드 필수 뼈에 가중치가 없다(유니티 매핑 실패) {missing}"
    return "필수 15뼈 가중치 있음"


def fix(name, cfg, out_dir=None, save_blend=False):
    if cfg.get("hold"):
        return {"이름": name, "보류": cfg["hold"]}
    dst_path = os.path.join(ROOT, cfg["path"])
    # 🔴 흰수염(2026-09-22, PM 지시) — 아래 네 군데 텍스처 저장소(archive_rgb·glb_images·
    # texture_file 복사·단색 PNG 생성)가 전부 dst_path 기준으로만 썼다. --out으로 시험
    # 내보내기를 해도 이 텍스처들은 커밋된 실제 Assets Textures/에 그대로 덮어써졌다(센고쿠
    # 시험 때 실측 — git status에 잡혀 되돌림). FBX 출력과 같은 규칙으로 --out이 있으면
    # 거기 Textures/에 쓰게 한다.
    tex_out_dir = os.path.join(out_dir, "Textures") if out_dir else os.path.join(os.path.dirname(dst_path), "Textures")
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
            tex_repo = tex_out_dir
            os.makedirs(tex_repo, exist_ok=True)
            members = list(cfg["archive_rgb"])
            rgb_arc = outer[0] if cfg.get("archive_rgb_outer") else arc   # 🔸 젊은 오비토(2026-09-17): 텍스처 PNG가 rar 안이 아니라 바깥 zip의 textures/에 있다
            for got_path, member_name in zip(extract_archive(rgb_arc, members), members):
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
    if cfg.get("blend_prep"):
        blend_prep(cfg, report)
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
    if cfg.get("drop_material_faces"):                                  # 🔸 유기(2026-09-17): UE 립 한 메시 안의 눈 렌즈 덮개·결투 원반 발광 면 — 그 재질 칸 면만 지우고 빈 재질 칸도 뺀다
        import bmesh
        gone_mats = set(cfg["drop_material_faces"])
        dropped = {}
        for m in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            idx = {i for i, mat in enumerate(m.data.materials) if mat is not None and mat.name in gone_mats}
            if not idx:
                continue
            bm = bmesh.new()
            bm.from_mesh(m.data)
            faces = [f for f in bm.faces if f.material_index in idx]
            counts = {i: sum(1 for f in faces if f.material_index == i) for i in idx}
            bmesh.ops.delete(bm, geom=faces, context="FACES")
            loose = [v for v in bm.verts if not v.link_faces]
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
            bm.to_mesh(m.data)
            bm.free()
            for i in sorted(idx, reverse=True):
                dropped[m.data.materials[i].name] = counts[i]
                m.data.materials.pop(index=i)
                for poly in m.data.polygons:
                    if poly.material_index > i:
                        poly.material_index -= 1
        assert set(dropped) == gone_mats, f"{name}: 뺄 재질이 없다 {gone_mats - set(dropped)}"
        report["뺀 재질 면"] = dropped
    if cfg.get("double_sided_meshes"):
        # 🔴 유기(2026-09-17 PM 유니티 인형): 한 겹 천 재킷 망토 — 블렌더 렌더는 양면을 그리지만 유니티(URP Lit)는 뒷면을 잘라
        #   정면에서 망토 안쪽이 안 보이고 앞 모서리 접힌 띠만 남아 「소매 끝 실」처럼 보였다(가중치 늘어짐 아님 — Idle 변 늘어짐 2배 넘는 곳은 목깃뿐, 실측).
        #   면을 복제해 뒤집고 법선 반대로 아주 조금(키의 0.03%) 밀어 양면으로 만든다(가중치 그룹은 bmesh 복제가 같이 옮긴다).
        import bmesh
        ds = {}
        for mname in cfg["double_sided_meshes"]:
            m = bpy.data.objects.get(mname)
            assert m is not None and m.type == "MESH", f"{name}: 양면으로 만들 메시가 없다 {mname}"
            if m.data.has_custom_normals:
                with bpy.context.temp_override(object=m, active_object=m):
                    bpy.ops.mesh.customdata_custom_splitnormals_clear()
            bm = bmesh.new()
            bm.from_mesh(m.data)
            bm.normal_update()
            zs = [v.co.z for v in bm.verts]
            off = (max(zs) - min(zs)) * 0.0003
            orig = {v: v.normal.copy() for v in bm.verts}
            faces = list(bm.faces)
            dup = bmesh.ops.duplicate(bm, geom=faces)
            new_faces = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
            for vo, vn in dup["vert_map"].items():                     # vert_map은 양방향(원본→복제·복제→원본)이라 원본 쪽 키만
                if isinstance(vo, bmesh.types.BMVert) and vo in orig and vn not in orig:
                    vn.co = vn.co - orig[vo] * off
            bmesh.ops.reverse_faces(bm, faces=new_faces)
            bm.to_mesh(m.data)
            bm.free()
            m.data.update()
            ds[mname] = len(new_faces)
        report["양면 메시(복제 면)"] = ds
    if cfg.get("drape_mesh"):
        # 🔸 쿠마 과거·크로커다일(2026-09-18 사장님 「망토가 이상함」): 원본이 바람에 날리는 모양으로 굳어 있어 게임 인형으로 보면 천이 얼굴을 가리거나 머리 위로 솟는다 →
        #   붙는 자리(pivot)는 그대로 두고 거리에 따라 점점 크게 돌려 등 뒤로 늘어뜨린다. 옷 무게가 아니라 기하 변형이라 가중치·재질은 그대로.
        draped = {}
        for dm in cfg["drape_mesh"]:
            m = bpy.data.objects.get(dm["mesh"])
            assert m is not None and m.type == "MESH", f"{name}: 늘어뜨릴 메시가 없다 {dm['mesh']}"
            Mw = m.matrix_world
            Mwi = Mw.inverted()
            pivot = Vector(dm["pivot"])
            axis = Vector(dm["axis"]).normalized()
            r0, r1 = dm.get("ramp", (0.0, 1.0))
            moved = 0.0
            for v in m.data.vertices:
                w = Mw @ v.co
                d = (w - pivot).length
                f = min(max((d - r0) / max(r1 - r0, 1e-9), 0.0), 1.0)
                if f <= 0:
                    continue
                if dm.get("ease", True):
                    f = f * f * (3 - 2 * f)
                R = Matrix.Rotation(math.radians(dm["deg"]) * f, 4, axis)
                nw = pivot + (R @ (w - pivot))
                moved = max(moved, (nw - w).length)
                v.co = Mwi @ nw
            m.data.update()
            draped[dm["mesh"]] = dict(각도=dm["deg"], 최대이동=round(moved, 4))
        report["늘어뜨린 망토"] = draped
    if cfg.get("drop_objects"):                                         # 🔸 히구루마: 식신 저지맨이 자기 아마추어(Juj)를 따로 가진다 — 메시 뺀 뒤 그 아마추어·카메라도 뺀다
        for gone in cfg["drop_objects"]:
            o = bpy.data.objects.get(gone)
            assert o is not None, f"{name}: 뺄 오브젝트가 없다 {gone}"
            bpy.data.objects.remove(o, do_unlink=True)
        report["뺀 오브젝트"] = list(cfg["drop_objects"])
    if cfg.get("join_armatures"):
        # 🔸 아이젠 천년혈전(초월_최상호_AP, 2026-09-18): 블리치 ROS glb가 몸(pl020_cos04_00)·얼굴·머리카락·얼굴 부속 a~e를 **아마추어 9개**로 따로 내보냈다
        #   (얼굴·머리카락 뿌리는 목 자리 1.521, 결합 자세 = 가져온 자세). 뼈 이름이 겹치므로(head·neck) 붙일 아마추어 뼈에 접두어를 달고(정점 그룹 같이)
        #   몸 아마추어에 합친 뒤 뿌리 뼈를 attach 뼈 밑에 붙인다. 이후 merge_bones로 Head·Neck에 합친다.
        ja = cfg["join_armatures"]
        main_o = bpy.data.objects[ja["main"]]
        joined = {}
        for aname, (prefix, parent_bone) in ja["attach"].items():
            ex = bpy.data.objects[aname]
            assert ex.type == "ARMATURE", f"{name}: 합칠 아마추어가 아니다 {aname}"
            bound = [m for m in bpy.context.scene.objects if m.type == "MESH" and (m.parent == ex or any(md.type == "ARMATURE" and md.object == ex for md in m.modifiers))]
            old_names = [b.name for b in ex.data.bones]
            roots = [b.name for b in ex.data.bones if b.parent is None]
            for m in bound:                                             # 정점 그룹 이름을 먼저(뼈 이름 바꾸기의 자동 갱신은 부모 관계에만 걸려 믿지 않는다)
                for g in m.vertex_groups:
                    if g.name in old_names and not g.name.startswith(prefix):
                        g.name = prefix + g.name
            for b in list(ex.data.bones):
                if not b.name.startswith(prefix):
                    b.name = prefix + b.name
            for m in bound:
                for g in m.vertex_groups:                                # 자동 갱신이 두 번 붙였으면 되돌린다
                    if g.name.startswith(prefix + prefix):
                        g.name = g.name[len(prefix):]
                for md in m.modifiers:
                    if md.type == "ARMATURE" and md.object == ex:
                        md.object = main_o
                if m.parent == ex:
                    Mw = m.matrix_world.copy()
                    m.parent = main_o
                    m.parent_type = "OBJECT"
                    m.matrix_world = Mw
            for o in bpy.context.scene.objects:
                o.select_set(o in (main_o, ex))
            bpy.context.view_layer.objects.active = main_o
            bpy.ops.object.join()
            bpy.ops.object.mode_set(mode="EDIT")
            eb = main_o.data.edit_bones
            for r in roots:
                eb[prefix + r].parent = eb[parent_bone]
                eb[prefix + r].use_connect = False
            bpy.ops.object.mode_set(mode="OBJECT")
            joined[aname] = dict(뼈=len(old_names), 메시=len(bound), 붙인곳=parent_bone)
        bpy.context.view_layer.update()
        report["합친 아마추어"] = joined
    if cfg.get("bind_unskinned"):
        # 🔴 히구루마(2026-09-17): 사람 메시 넷이 정점 그룹(Bip001 이름)은 있는데 가져오기에서 아마추어 모디파이어·부모가 안 붙었다 → 세계 자리 그대로 붙인다
        arm_b = bpy.data.objects[cfg["bind_unskinned"]]
        bound = []
        for m in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            if skinned_to(m) is None and m.vertex_groups:
                Wm = m.matrix_world.copy()
                m.parent = arm_b
                m.matrix_world = Wm
                mod = m.modifiers.new("Armature", "ARMATURE")
                mod.object = arm_b
                bound.append(m.name)
        bpy.context.view_layer.update()
        report["아마추어에 붙인 메시"] = bound
    if cfg.get("drop_verts_of_bones"):
        # 🔸 피즈(2026-09-16): 삼지창이 몸과 **같은 메시**(Object_6)에 들어 있고 뿌리 직계 Weapon_70 뼈 100% — 메시로는 못 빼니 그 뼈 몫이 반 넘는 정점을 지운다
        import bmesh as _bm
        gone_bones = set(cfg["drop_verts_of_bones"])
        removed = 0
        for m in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            ids = {g.index for g in m.vertex_groups if g.name in gone_bones}
            if not ids:
                continue
            kill = [v.index for v in m.data.vertices
                    if sum(ge.weight for ge in v.groups if ge.group in ids) > 0.5 * max(sum(ge.weight for ge in v.groups), 1e-9)]
            bm = _bm.new()
            bm.from_mesh(m.data)
            bm.verts.ensure_lookup_table()
            _bm.ops.delete(bm, geom=[bm.verts[i] for i in kill], context="VERTS")
            bm.to_mesh(m.data)
            bm.free()
            m.data.update()
            for gname in gone_bones:
                g = m.vertex_groups.get(gname)
                if g is not None:
                    m.vertex_groups.remove(g)
            removed += len(kill)
        report["뼈로 지운 정점"] = f"{sorted(gone_bones)} {removed}"
    if cfg.get("first_uv_only"):                                         # 🔸 킹콩: glb UV 5층 — glTF texCoord 0(첫 층)만 쓰니 나머지 층은 뺀다(유니티 UV1~4 쓰레기 방지)
        dropped = 0
        for m in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
            while len(m.data.uv_layers) > 1:
                m.data.uv_layers.remove(m.data.uv_layers[len(m.data.uv_layers) - 1])
                dropped += 1
        report["뺀 UV 층"] = dropped
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
                mod.ratio = dec.get("by_mesh", {}).get(m.name, dec["ratio"])   # 🔸 킹콩: 털 카드는 몸보다 약하게(카드 모양 보호)
                mod.use_collapse_triangulate = True
                with bpy.context.temp_override(object=m, active_object=m, selected_objects=[m]):
                    bpy.ops.object.modifier_move_to_index(modifier="fix_decimate", index=0)
                    bpy.ops.object.modifier_apply(modifier="fix_decimate")
                cut += 1
            tri_after += sum(len(p.vertices) - 2 for p in m.data.polygons)
        report["감량"] = f"삼각형 {tri_before} → {tri_after} (메시 {cut}개 ×{dec['ratio']}, {dec.get('min_tris', 1000)}삼각형 이상만)" + (f" · 메시별 {dec['by_mesh']}" if dec.get('by_mesh') else "")
    if cfg.get("rename_bones"):                                         # 사람형 매핑 뼈 이름을 표준으로(가중치 그룹 이름도 같이)
        arm0 = main_armature()
        strip = re.compile(cfg["rename_strip"]) if cfg.get("rename_strip") else None   # 🔸 갑옷거인: glTF 번호 꼬리(Bip001 Pelvis_01) — 표 이름과 꼬리 뗀 이름으로 짝짓기
        for old, new in cfg["rename_bones"].items():
            b = arm0.data.bones.get(old)
            if b is None and strip is not None:
                hit = [x for x in arm0.data.bones if strip.sub("", x.name) == old]
                assert len(hit) <= 1, f"{name}: 꼬리 뗀 이름이 겹친다 {old} {[x.name for x in hit]}"
                b = hit[0] if hit else None
                old = b.name if b else old
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
        report["텍스처"] = relink_textures(ref["textures"], tex_out_dir, os.path.join(os.path.dirname(dst_path), "Textures"))
    if cfg.get("glb_images"):                                           # glb 내장 이미지를 원본 바이트 그대로 재질 이름 기준 파일로(재질을 짜기 전에)
        import json as _json
        import struct as _struct
        tex_repo = tex_out_dir
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
        tex_repo = tex_out_dir
        os.makedirs(tex_repo, exist_ok=True)
        for src_tex, dst_name in cfg["copy_textures"].items():
            shutil.copy2(os.path.expanduser(src_tex), os.path.join(tex_repo, dst_name))
            arc_textures.append(os.path.join(tex_repo, dst_name))
    mat_spec = cfg.get("materials")
    if cfg.get("solid_textures"):
        # 3ds Max 맵이 파일 경로 없이 빠진 FBX(아이언맨 Map #1·#3) + 색만 있는 재질: 재질마다 기본색을 단색 PNG로 구워 Textures/<안전한 재질 이름>.png에 건다.
        #   None = 원본 기본색 그대로, (r,g,b) = 선형 색으로 바로잡음(3ds Max 내보내기가 black·darksilver를 0.8 회색, yellow를 흰색으로 떨궜다).
        from bpy_extras.node_shader_utils import PrincipledBSDFWrapper
        tex_repo = tex_out_dir
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
        report["재질 새로"] = build_materials(mat_spec, tex_out_dir, os.path.join(os.path.dirname(dst_path), "Textures"))
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
    if arm is not None and cfg.get("pose_bones_world"):
        # 🔸 죠타로(2026-09-16): 옷깃 키체인 뼈가 쉬는 자세에서 수평(게임은 물리로 늘어뜨림) → 유니티에선 옆으로 막대처럼 뻗는다.
        #   세계 축 기준으로 뼈 머리를 중심으로 돌려 두고(자식 따라감) 아래 「기본 자세 그대로 붙잡기」가 굽는다.
        Mw3 = arm.matrix_world.to_3x3().normalized()
        for bname, (axis, deg) in cfg["pose_bones_world"].items():
            pb = arm.pose.bones[bname]
            ax = (Mw3.inverted() @ Vector(axis)).normalized()
            pivot = pb.head.copy()
            pb.matrix = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(deg), 4, ax) @ Matrix.Translation(-pivot) @ pb.matrix
            bpy.context.view_layer.update()
        report["돌린 뼈"] = list(cfg["pose_bones_world"])
    if arm is not None and cfg.get("use_rest_pose"):
        # 🔸 토지(2026-09-16): FBX 기본 자세가 전투 자세(몸이 +X로 73° 돌고 한 주먹을 든 채)인데 결합 자세는 −Y를 본 깨끗한 A자 → 기본 자세를 버리고 결합 자세로
        for pb in arm.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        report["기본 자세 버림"] = "결합 자세 사용"
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
    if cfg.get("fold_mesh"):
        # 🔸 쿠마 과거·크로커다일(2026-09-18 사장님 「망토가 이상함」): 원본이 바람에 날리는 모양으로 굳어 있어 인형으로 보면 천이 머리 위로 솟고 옆으로 크게 퍼진다.
        #   붙는 자리(pivot) 둘레 r_keep 안쪽은 그대로 두고, 그보다 바깥으로 뻗은 몫을 **아래로 접어 내린다**(천이 중력에 늘어진 모양).
        #   r_keep 밖 몫 중 keep_out만 옆으로 남기고 drop만큼 내리고 back만큼 뒤로(+Y). 회전이 아니라 접기라서 몸을 감싸지 않는다(회전 시험 3종은 전부 몸을 휘감았다).
        folded = {}
        for fm in cfg["fold_mesh"]:
            m = bpy.data.objects.get(fm["mesh"])
            assert m is not None and m.type == "MESH", f"{name}: 접을 메시가 없다 {fm['mesh']}"
            Mw, Mwi = m.matrix_world, m.matrix_world.inverted()
            px, py, pz = fm["pivot"]
            r_keep, drop, back, keep_out = fm["r_keep"], fm.get("drop", 0.95), fm.get("back", 0.18), fm.get("keep_out", 0.3)
            moved = 0.0
            for v in m.data.vertices:
                w = Mw @ v.co
                dx, dy = w.x - px, w.y - py
                r = math.hypot(dx, dy)
                if r <= r_keep or r < 1e-9:
                    continue
                k = (r_keep + (r - r_keep) * keep_out) / r
                over = r - r_keep
                nw = Vector((px + dx * k, py + dy * k + over * back, w.z - over * drop))
                moved = max(moved, (nw - w).length)
                v.co = Mwi @ nw
            m.data.update()
            folded[fm["mesh"]] = dict(반지름=r_keep, 내림=drop, 최대이동=round(moved, 4))
        report["접어 내린 천"] = folded
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
        drop_list = list(cfg.get("drop_bones", ()))
        if cfg.get("drop_bones_re"):                                    # 🔸 에렌: glTF `_End` 끝점 뼈 수십 개 — 정규식으로(가중치 있으면 아래 assert가 막는다)
            rx = re.compile(cfg["drop_bones_re"])
            drop_list += [eb.name for eb in data.edit_bones if rx.search(eb.name) and eb.name not in drop_list]
        for gone in drop_list:
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
        for ab in cfg.get("add_bones", ()):
            # 🔸 에렌(4기, 왼다리 절단, 2026-09-16): 유니티 휴머노이드는 LeftLowerLeg·LeftFoot이 **필수**라 없으면 아바타가 안 만들어진다 →
            #   반대쪽 뼈를 X 거울로 복사해 세우고, 가장 가까운 정점 몇 개에 아주 작은 가중치를 줘 스킨 뼈로 내보낸다(겉모습 영향 없음).
            src = data.edit_bones[ab["mirror_of"]]
            eb = data.edit_bones.new(ab["name"])
            eb.head = Vector((-src.head.x, src.head.y, src.head.z))
            eb.tail = Vector((-src.tail.x, src.tail.y, src.tail.z))
            eb.roll = -src.roll
            eb.parent = data.edit_bones[ab["parent"]]
            eb.use_connect = False
            report.setdefault("거울로 만든 뼈", []).append(ab["name"])
        for child_name, parent_name in cfg.get("reparent_bones", {}).items():
            eb, par = data.edit_bones.get(child_name), data.edit_bones.get(parent_name)
            assert eb is not None and par is not None, f"{name}: 다시 붙일 뼈가 없다 {child_name} → {parent_name}"
            eb.use_connect = False
            eb.parent = par
        if cfg.get("reparent_bones"):
            report["다시 붙인 뼈"] = len(cfg["reparent_bones"])
        # 🔴 특별함_송형성(아디오, 2026-09-15 PM 유니티): 「Found duplicate transform 'f_l_hair_t' for human bone 'Jaw' and 'LeftEye'」 — 눈·턱 뼈 없는 pl_ 리그에서
        #   유니티 자동 매핑이 Head 자손 머리카락 끝 뼈를 선택 얼굴 뼈로 잡는다(요크도 LeftEye=l_hair_01·Jaw=f_l_hair_01 — 겹치지만 않아 통과). 2차에서 앞머리를
        #   가중치 0 HairRoot 밑으로 + 가중치 0 mixamorig:LeftEye·RightEye·Jaw를 넣었지만 유니티가 여전히 f_l_hair_t를 둘 다에 잡음 — 가중치 0 뼈는 스킨 뼈 목록에
        #   없어 후보로 안 센 것으로 본다(사이타마·이병준 이름 붙은 눈은 눈 메시가 실려 있다). 휴머노이드 클립은 매핑 안 된 뼈를 안 움직이니
        #   merge_bones: 이름이 맞는 뼈의 가중치를 into 뼈에 더하고 뼈를 지운다(겉모습·게임 동작 그대로, Head 자손에서 후보 자체를 없앰).
        # 🔸 심해왕(2026-09-16): 목록도 받는다 — 얼굴 뼈 → Head에 더해 천 시뮬 뼈(CLOTH_A_*) → Spine2. with_root면 under 뼈 자신도 합친다.
        for mb in ([cfg["merge_bones"]] if isinstance(cfg.get("merge_bones"), dict) else cfg.get("merge_bones") or []):
            if mb.get("under"):                                         # 이름 규칙이 제각각인 얼굴 뼈 100개(이치고) — 그 뼈의 자손을 통째로
                root_eb = data.edit_bones[mb["under"]]
                gone = [eb.name for eb in data.edit_bones if root_eb in eb.parent_recursive or (mb.get("with_root") and eb == root_eb)]
            else:
                gone = [eb.name for eb in data.edit_bones if re.search(mb["pattern"], eb.name)]
            assert gone and mb["into"] in data.edit_bones, f"{name}: merge_bones 대상 없음"
            moved = 0
            share = mb.get("share", 1.0)                                # 🔸 아이젠 천년혈전: 코트 자락 뼈 몫을 넓적다리 share · 나머지 rest(Hips)로 나눠 합친다(전부 다리면 찢기고 전부 Hips면 다리가 뚫고 나옴)
            for m in meshes:
                src = {m.vertex_groups[n].index for n in gone if n in m.vertex_groups}
                if not src:
                    continue
                tg = m.vertex_groups.get(mb["into"]) or m.vertex_groups.new(name=mb["into"])
                rg = (m.vertex_groups.get(mb["rest"]) or m.vertex_groups.new(name=mb["rest"])) if share < 1.0 else None
                for v in m.data.vertices:
                    add = sum(ge.weight for ge in v.groups if ge.group in src)
                    if add > 0:
                        cur = sum(ge.weight for ge in v.groups if ge.group == tg.index)
                        tg.add([v.index], cur + add * share, "REPLACE")
                        if rg is not None:
                            cur_r = sum(ge.weight for ge in v.groups if ge.group == rg.index)
                            rg.add([v.index], cur_r + add * (1.0 - share), "REPLACE")
                        moved += 1
                for gi in sorted(src, reverse=True):
                    m.vertex_groups.remove(m.vertex_groups[gi])
            for n in gone:
                eb = data.edit_bones[n]
                for c in list(eb.children):
                    c.use_connect = False
                    c.parent = eb.parent
                data.edit_bones.remove(eb)
            report.setdefault("합친 뼈", []).append(f"{len(gone)}개 → {mb['into']} · 옮긴 정점 {moved}")
        # 🔸 move_weights [{meshes, bones, into}]: 그 메시에서만 bones 가중치를 into 뼈로 옮긴다 — 유기(2026-09-17) 어깨에 걸친 재킷 망토가
        #   위팔(SHOULDER_L/R)에 실려 있어 T자·Idle에서 팔 따라 날개처럼 뜬다 → 망토의 위팔 몫을 같은 쪽 쇄골(Shoulder)로.
        for mw in cfg.get("move_weights", ()):
            moved = 0
            for m in meshes:
                if m.name not in mw["meshes"]:
                    continue
                src = {m.vertex_groups[n].index for n in mw["bones"] if n in m.vertex_groups}
                if not src:
                    continue
                tg = m.vertex_groups.get(mw["into"]) or m.vertex_groups.new(name=mw["into"])
                for v in m.data.vertices:
                    add_w = sum(ge.weight for ge in v.groups if ge.group in src)
                    if add_w > 0:
                        cur = sum(ge.weight for ge in v.groups if ge.group == tg.index)
                        tg.add([v.index], cur + add_w, "REPLACE")
                        for gi in src:
                            m.vertex_groups[gi].remove([v.index])
                        moved += 1
            assert moved, f"{name}: move_weights 옮긴 정점 없음 {mw}"
            report.setdefault("옮긴 가중치", []).append(f"{','.join(mw['meshes'])}: {','.join(mw['bones'])} → {mw['into']} · 정점 {moved}")
        # 🔸 smooth_weights {메시: 반복}: 이웃(변) 평균으로 가중치를 고른다 — 심해왕 털 망토(원본 천 시뮬 뼈 100% 정점 옆이 RightShoulder 45%라
        #   Idle에서 2 mm 변이 95 mm로 벌어짐). 반쯤 섞고(0.5) 합 1로 되돌린다.
        for sw_name, sw_iters in cfg.get("smooth_weights", {}).items():
            import numpy as np
            sw_obj = bpy.data.objects[sw_name]
            sw_n, sw_g = len(sw_obj.data.vertices), len(sw_obj.vertex_groups)
            sw_w = np.zeros((sw_n, sw_g))
            for sw_v in sw_obj.data.vertices:
                for sw_ge in sw_v.groups:
                    sw_w[sw_v.index, sw_ge.group] = sw_ge.weight
            sw_e = np.array([e.vertices[:] for e in sw_obj.data.edges])
            for _ in range(sw_iters):
                sw_acc = np.zeros_like(sw_w)
                sw_cnt = np.zeros(sw_n)
                np.add.at(sw_acc, sw_e[:, 0], sw_w[sw_e[:, 1]])
                np.add.at(sw_acc, sw_e[:, 1], sw_w[sw_e[:, 0]])
                np.add.at(sw_cnt, sw_e[:, 0], 1)
                np.add.at(sw_cnt, sw_e[:, 1], 1)
                sw_has = sw_cnt > 0
                sw_w[sw_has] = 0.5 * sw_w[sw_has] + 0.5 * sw_acc[sw_has] / sw_cnt[sw_has, None]
                sw_w /= np.maximum(sw_w.sum(1, keepdims=True), 1e-9)
            for sw_gi, sw_grp in enumerate(sw_obj.vertex_groups):
                sw_col = sw_w[:, sw_gi]
                sw_grp.remove([int(i) for i in np.where(sw_col <= 1e-4)[0]])
                for i in np.where(sw_col > 1e-4)[0]:
                    sw_grp.add([int(i)], float(sw_col[i]), "REPLACE")
            report.setdefault("가중치 고름", []).append(f"{sw_name} ×{sw_iters}")
        # 유니티식 선택 얼굴 뼈 겹침 점검(휴리스틱 — 경고만, PM 2026-09-15): Head 자손 중 이름이 Eye·Jaw가 아닌 끝 뼈(자식 없음)가 앞쪽(−Y)에서 좌우 짝이면 위험.
        #   요크·고우선·김태영 등 걸려도 통과한 유니티가 있어 판정이 아니라 표시. 가중치 0 뼈는 유니티가 뼈로 안 세니 이름 붙인 빈 눈 뼈로는 못 막는다.
        if "mixamorig:Head" in data.edit_bones:
            head_eb = data.edit_bones["mixamorig:Head"]
            unit = max(head_eb.length, 1e-4)
            desc = [eb for eb in data.edit_bones if eb != head_eb and head_eb in eb.parent_recursive]
            leaves = [(eb.name, (eb.head - head_eb.head) / unit) for eb in desc
                      if not eb.children and not re.search(r"(?i)eye|jaw", eb.name) and (eb.head - head_eb.head).y < -0.1 * unit]
            risky = sorted(n for n, d in leaves if abs(d.x) > 0.1 and any(abs(d2.x + d.x) < 0.15 and abs(d2.y - d.y) < 0.15 and abs(d2.z - d.z) < 0.15 for _, d2 in leaves))
            report["얼굴 뼈 겹침 점검"] = f"Head 자손 {len(desc)}" + (f" · ⚠️ 끝·앞쪽·좌우 짝 {risky}" if risky else " · 위험 없음")
        bpy.ops.object.mode_set(mode="OBJECT")
        report["뼈"] = len(data.bones)
        for ab in cfg.get("add_bones", ()):                             # 거울로 만든 뼈에 씨앗 가중치: 부모 그룹 정점 중 가장 낮은(원본 z) 3개에 0.001
            cand = []
            for m in meshes:
                g = m.vertex_groups.get(ab["seed_group"])
                if g is None:
                    continue
                Mw = mesh_world[m.name]
                for v in m.data.vertices:
                    if any(ge.group == g.index and ge.weight > 0.3 for ge in v.groups):
                        cand.append(((Mw @ v.co).z, m, v.index))
            assert cand, f"{name}: add_bones 씨앗 그룹 정점이 없다 {ab['seed_group']}"
            for _, m, vi in sorted(cand, key=lambda c: c[0])[:3]:
                (m.vertex_groups.get(ab["name"]) or m.vertex_groups.new(name=ab["name"])).add([vi], 0.001, "REPLACE")

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
    if new_arm is not None and cfg.get("level_chains"):
        for i, lc in enumerate(cfg["level_chains"]):
            level_chain(new_arm, lc["chain"], lc["target"], report, f"곧게 편 사슬 {i}")
    if new_arm is not None and cfg.get("tpose_arms"):
        tpose_arms(new_arm, meshes, report, cfg["tpose_arms"] if isinstance(cfg["tpose_arms"], dict) else None)
    if new_arm is not None and cfg.get("level_chains"):
        refit_size(new_arm, meshes, cfg, report)
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

    if new_arm is not None and cfg.get("synth_idle") and not clips:
        # 🔸 메노스 그란데(2026-09-17): 원본에 클립이 없는 Generic 유닛 — 뼈마다 「세계 축 · 진폭(°) · 위상」 사인 흔들림으로 Idle 루프를 짓는다.
        #   각도 = 진폭 × (sin(2πt/N + 위상) − sin(위상)) → 0프레임·N프레임 모두 0이라 쉬는 자세에서 시작·끝(루프 이음새 없음, FBX 기본 자세 = 결합 자세).
        #   뼈 기본 회전 = 쉬는 행렬⁻¹ × 세계 축 회전 × 쉬는 행렬(부모 흔들림은 자식에 누적).
        si = cfg["synth_idle"]
        n = int(si.get("frames", 72))
        step = int(si.get("step", 3))
        new_arm.animation_data_create()
        act = bpy.data.actions.new(si.get("take", "Idle"))
        act.use_fake_user = True
        new_arm.animation_data.action = act
        for pb in new_arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
        keyed = []
        for f in list(range(0, n, step)) + [n]:
            for bname, waves in si["bones"].items():
                pb = new_arm.pose.bones[bname]
                R3 = pb.bone.matrix_local.to_3x3()
                rot = Matrix.Identity(3)
                for axis, deg, phase in waves:
                    ang = math.radians(deg) * (math.sin(2 * math.pi * f / n + phase) - math.sin(phase))
                    rot = Matrix.Rotation(ang, 3, Vector(axis).normalized()) @ rot
                pb.rotation_quaternion = (R3.inverted() @ rot @ R3).to_quaternion()
                pb.keyframe_insert("rotation_quaternion", frame=1 + f)
            keyed.append(1 + f)
        clips = [(act.name, 1, [None] * (n + 1))]
        scene.frame_start, scene.frame_end = 1, 1 + n
        scene.frame_set(1)
        report["지은 Idle"] = f"{act.name} {n}프레임(키 {len(keyed)}) · 뼈 {list(si['bones'])}"

    # ── 검사·내보내기
    if new_arm is not None and clips and cfg.get("synth_idle"):
        scene.frame_set(1)
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
    # 🔴 킹(2026-09-17 PM 유니티 반려): 원본 재질 알파(메시 이름 _0.1_ = 알파 0.1)가 relink_textures의 old.alpha로 그대로 넘어가
    #   FBX TransparencyFactor로 나가서 유니티 인형 재질 9개가 전부 Transparent(큐 3000)로 떴다 → 내보내기 직전 모든 재질을 불투명으로 못박고 검사한다.
    #   일부러 반투명을 남길 유닛만 keep_alpha=True.
    if not cfg.get("keep_alpha"):
        from bpy_extras.node_shader_utils import PrincipledBSDFWrapper
        fixed = []
        for mat in {s.material for o in scene.objects if o.type == "MESH" for s in o.material_slots if s.material}:
            if mat.use_nodes:
                for nd in [nd for nd in mat.node_tree.nodes if nd.type == "BSDF_PRINCIPLED"]:
                    sock = nd.inputs["Alpha"]
                    if sock.is_linked or sock.default_value < 1.0:
                        fixed.append(mat.name)
                    for lk in list(sock.links):
                        mat.node_tree.links.remove(lk)
                    sock.default_value = 1.0
            if hasattr(mat, "blend_method"):
                mat.blend_method = "OPAQUE"
            if hasattr(mat, "surface_render_method"):
                mat.surface_render_method = "DITHERED"
        bad = [mat.name for mat in {s.material for o in scene.objects if o.type == "MESH" for s in o.material_slots if s.material} if mat.use_nodes
               and any(nd.inputs["Alpha"].is_linked or nd.inputs["Alpha"].default_value < 1.0 for nd in mat.node_tree.nodes if nd.type == "BSDF_PRINCIPLED")]
        assert not bad, f"{name}: 재질 알파가 1이 아니거나 Alpha 연결이 남았다 {bad}"
        report["불투명으로 고친 재질"] = sorted(set(fixed))
    if cfg.get("seed_zero_bones") and main_armature() is not None:
        arm = main_armature()                                           # 앞의 arm은 뼈대 다시 짓기에서 지워졌을 수 있다
        # 🔸 샹크스(2026-09-17): 왼팔이 없는 게 원작이라 LArm_Fore·LHand_Palm 가중치 0 → 스킨 뼈 목록에서 빠져 유니티 휴머노이드 매핑 실패(오비토 교훈).
        #   가중치 0 뼈마다 뼈 머리(세계)에 가장 가까운 정점 3개(모든 메시)에 아주 작은 몫을 준다 — 겉모습 그대로.
        import numpy as _np
        w_seed = float(cfg["seed_zero_bones"])
        ms = [o for o in scene.objects if o.type == "MESH"]
        used = set()
        for m in ms:
            idx = {g.index: g.name for g in m.vertex_groups}
            for v in m.data.vertices:
                for ge in v.groups:
                    if ge.weight > 0:
                        used.add(idx[ge.group])
        pts = [(m, i, (m.matrix_world @ v.co)[:]) for m in ms for i, v in enumerate(m.data.vertices)]
        P = _np.array([p[2] for p in pts])
        seeded = {}
        for b in arm.data.bones:
            if b.name in used:
                continue
            head = _np.array((arm.matrix_world @ b.head_local)[:])
            near = _np.argsort(_np.linalg.norm(P - head, axis=1))[:3]
            for k in near:
                m, i, _ = pts[int(k)]
                g = m.vertex_groups.get(b.name) or m.vertex_groups.new(name=b.name)
                g.add([i], w_seed, "ADD")
            seeded[b.name] = sorted({pts[int(k)][0].name for k in near})
        report["가중치 0 뼈 씨앗"] = seeded
    if cfg.get("kind") == "human" and not cfg.get("generic"):
        report["휴머노이드 가중치"] = humanoid_weight_check(name, [o for o in scene.objects if o.type == "MESH"])
    # 🔴 영원_김영원(2026-09-22, PM 유니티 반려) — 아마추어 오브젝트 이름이 원본 그대로
    # (예: "INGAME_ANIMATION_SUPEREMOTE_MALE_FROG_Rig")로 남으면 블렌더 FBX 내보내기가
    # 그 오브젝트 노드와 뿌리 뼈(mixamorig:Hips) 노드를 겹쳐 써서, 유니티가 Hips를 뼈가
    # 아니라 그 오브젝트 이름으로 잡는다(뼈대 자체는 정상 — Hips는 부모 없는 진짜 뿌리
    # 뼈였다, 재수입해서 직접 확인). 다른 유닛은 원본 아마추어 오브젝트 이름이 우연히
    # "Armature"였을 뿐이라 안 드러났던 문제 — 내보내기 직전에 무조건 통일한다.
    arm_obj = main_armature()
    if arm_obj is not None:
        arm_obj.name = "Armature"
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
