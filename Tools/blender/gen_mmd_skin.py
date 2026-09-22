"""MMD/PMX 유래 FBX 리그 → mixamorig 사람형 FBX. 구현담당2 전담, 2026-09-16 첫 건
희귀함_임채민(이시마루 키요타카, Kiyotaka Ishimaru.fbx). MMD 모델이 또 오면 이 파일에 쌓는다.
  blender -b --factory-startup --python Tools/blender/gen_mmd_skin.py -- [이름 ...] [--out DIR] [--render DIR]

■ 이 원본의 특징(PM 바이너리 실측 + 이 스크립트 작성 중 재확인)
- 뼈 302개(FBX 7400 · UnitScaleFactor 1.0 · UpAxis Y) — 실제 메시에 가중치가 있는 정점그룹은
  87개뿐(가져오면 확인됨, 나머지 215개는 IK·더미·그림자·문자열·혀·눈 보조 뼈로 가중치 자체가
  없다 — 지울 때 잃을 게 없다는 뜻).
- 🔴 PM 정정: `Leg_L/Knee_L/Ankle_L`·`ShoulderP_L/ShoulderC_L`는 정점 0개, 실제로 다리를 움직이는
  건 `LegD_L/KneeD_L/AnkleD_L`("D"=Deform 계열). 팔도 `Arm_L`(가중치 31) 하나가 아니라
  `ArmTwist_L/1/2/3`(합쳐서 550+)·`Elbow_L`+`HandTwist_L/1/2/3`가 상당량을 나눠 가진다 —
  우솝(Annettlw Biped) 사고와 같은 함정. WEIGHT_MAP이 이 전부를 목표 뼈 하나로 합친다.
- 이미 거의 T자다(팔이 수평에서 23~29° 처짐, 다리는 0.2~3.5°로 사실상 수직) — PM 짐작(A자)과
  달리 실측이 우선이라 그대로 진행, straighten_arms()로 그 처짐만 편다.
- 재질 66개·텍스처 노드 0개(FBX가 안 물고 있음) — MATERIAL_BUCKETS로 5장(face/fuku/hair/
  hitomi/wp)에 이름 기준 수동 대응해 재질도 5개로 합친다.
- 표정 셰이프키 56개(눈 깜빡임·감정 등) — 전부 삭제(표정 애니메이션 안 씀, 용량만 먹음).
- Head 직계 자식 뼈(Eye_L·Eye_R·Eyes·HeadTip·TongueParent·勲章 등)는 새 뼈대에 안 만든다(PM
  경고 — 아디오·모리아 사고, Head는 HeadTip 위치를 tail로만 쓴다).
- 원본 키 22.09(파일 단위) — MMD 통상 관례(8등신=1.58m)와 다른 임의 배율이라 그대로 실측해
  1.8m로 스케일한다(사람 키 규격 값은 신용하지 않는다).
"""
import hashlib
import json
import math
import os
import shutil
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
PREFIX = "mixamorig:"

# 🔴 전설적인_박병규(디오) — 이시마루·몬도와 업로더가 달라 뼈 이름이 일본어 원문 그대로고(左/右
# 접두어가 아니라 ".L"/".R" 접미어, 예: "ひじ.L"), IK·D계열·트위스트·センター 자체가 이 파일엔
# 없다(실측: 가중치 있는 정점그룹 134개가 그대로 "몸 골격 그 자체" — 87/302였던 이시마루보다
# 훨씬 단순). 원본에 上半身2(Chest)가 없어 上半身 자체를 Spine·Chest 둘 다의 가중치 소스로 쓰고
# (범프 없이 그 구간 전체가 50/50으로 섞이지만 정적 T자 표시용이라 무해) 위치만 上半身의
# head→tail 60% 지점을 가상의 경계로 나눈다(_TORSOMID_, build()에서 계산). SKINS 딕셔너리
# 리터럴 안에서 바로 참조하므로 SKINS보다 앞에 둔다.
SPINE_DIO = [
    ("Hips", "下半身", "_HIPTAIL_", None),
    ("Spine", "上半身", "_TORSOMID_", "Hips"),
    ("Chest", "_TORSOMID_", "首", "Spine"),
    ("Neck", "首", "頭", "Chest"),
    ("Head", "頭", "_SELF_", "Neck"),
]
LIMB_DIO = [
    ("Shoulder", "肩", "腕", "Chest"), ("Arm", "腕", "ひじ", "Shoulder"),
    ("ForeArm", "ひじ", "手首", "Arm"), ("Hand", "手首", "_SELF_", "ForeArm"),
    ("UpLeg", "足", "ひざ", "Hips"), ("Leg", "ひざ", "足首", "UpLeg"),
    ("Foot", "足首", "つま先", "Leg"), ("ToeBase", "つま先", "_SELF_", "Foot"),
]
FINGER_CHAINS_DIO = [
    ("Thumb1", "親指０", "親指１", None), ("Thumb2", "親指１", "親指２", "Thumb1"),
    ("Thumb3", "親指２", "_SELF_", "Thumb2"),
    ("Index1", "人指１", "人指２", None), ("Index2", "人指２", "人指３", "Index1"),
    ("Index3", "人指３", "_SELF_", "Index2"),
    ("Middle1", "中指１", "中指２", None), ("Middle2", "中指２", "中指３", "Middle1"),
    ("Middle3", "中指３", "_SELF_", "Middle2"),
    ("Ring1", "薬指１", "薬指２", None), ("Ring2", "薬指２", "薬指３", "Ring1"),
    ("Ring3", "薬指３", "_SELF_", "Ring2"),
    ("Pinky1", "小指１", "小指２", None), ("Pinky2", "小指２", "小指３", "Pinky1"),
    ("Pinky3", "小指３", "_SELF_", "Pinky2"),
]
# 목표 뼈 → 원본 정점그룹. 관절은 전부 "{s}"(".L"/".R")로 채우고, 팔의 어깨·팔꿈치 보조 뼈
# (Arm_Left_Shoulder_Adj_A/B 등)는 "_Left"/"_Right" 긴 이름이라 "{S}"로 따로 채운다(build()의
# 병합 루프가 둘 다 채워 준다). Head_*·Hair_*(표정·머리카락, SFM식 영어 이름)·Earring_*(귀걸이)는
# 뼈를 안 만들고 weight_prefix_map으로 Head에, Strap_*(어깨끈)는 Chest에 뭉뚱그린다.
WEIGHT_MAP_DIO = {
    "Hips": ["下半身"],
    "Spine": ["上半身"],
    "Chest": ["上半身"],
    "Neck": ["首"],
    "Head": ["頭", "目.L", "目.R"],
    "Shoulder": ["肩.{s}"],
    # 🔴 PM 실측(유니티 Idle) — Arm_*_Shoulder_Adj_A/B는 Shoulder(빗장뼈)가 아니라 위팔 시작
    # 부위를 보정하는 트위스트류 뼈였다(이름과 달리) — Shoulder로 합쳤더니 Idle에서 위팔이
    # 비틀린 수건처럼 가늘어졌다. Arm으로 옮김.
    "Arm": ["腕.{s}", "Arm_{S}_Shoulder_Adj_A", "Arm_{S}_Shoulder_Adj_B"],
    "ForeArm": ["ひじ.{s}", "Arm_{S}_Elbow_Adj_A", "Arm_{S}_Elbow_Adj_B"],
    "Hand": ["手首.{s}"],
    "Thumb1": ["親指０.{s}"], "Thumb2": ["親指１.{s}"], "Thumb3": ["親指２.{s}"],
    "Index1": ["人指１.{s}"], "Index2": ["人指２.{s}"], "Index3": ["人指３.{s}"],
    "Middle1": ["中指１.{s}"], "Middle2": ["中指２.{s}"], "Middle3": ["中指３.{s}"],
    "Ring1": ["薬指１.{s}"], "Ring2": ["薬指２.{s}"], "Ring3": ["薬指３.{s}"],
    "Pinky1": ["小指１.{s}"], "Pinky2": ["小指２.{s}"], "Pinky3": ["小指３.{s}"],
    "UpLeg": ["足.{s}"], "Leg": ["ひざ.{s}"], "Foot": ["足首.{s}"], "ToeBase": ["つま先.{s}"],
}

SKINS = {
    "희귀함_임채민": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_임채민.zip",
        fbx_member="Kiyotaka Ishimaru.fbx",
        mesh_name="Kiyotaka",
        path="Assets/Art/Units/희귀함_임채민/희귀함_임채민.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        # 재질 66개 → 5장(PM 지시 — 부위별 이름 대응, 확실하지 않은 것은 PM의 "나머지는 wp로
        # 맞춰보고 색 비교" 지시대로 wp를 기본값으로 뒀다).
        material_buckets={
            "face": ["Face", "Neck", "Ears", "Hand", "WhiteEye", "Eyebrow", "TeethUpper", "TeethLower", "Tongue",
                     "MouthInside", "鼻線", "EyebrowInterval線1", "EyebrowInterval線2", "Double線", "アイライン",
                     "Lower睫", "m_隈", "m_Ohこ", "m_CheekBlush", "m_Face影", "m_Pale", "m_FaceRed"],
            "fuku": ["Collar", "CollarMiddle", "Coat胴", "BreastポJacket", "CoatポJacket", "Coat裏地", "CoatArm",
                     "CollarPart1", "CollarPart2", "ShoulderPart", "Button", "Button穴1", "Button穴2",
                     "勲章1", "勲章2", "勲章3", "Arm章", "Arm章裏", "Arm章ピン1", "Arm章ピン2",
                     "SleeveButton", "SleeveMiddle", "Belt留", "Belt金具", "Belt", "Trousers", "TrousersPart"],
            "hair": ["Hair"],
            "hitomi": ["Pupil", "EyeLightり"],
        },
        texture_files={"face": "face.png", "fuku": "fuku.png", "hair": "hb.png", "hitomi": "hitomi_i.png", "wp": "wp.png"},
    ),
    # 오오와다 몬도(단간론파, 이시마루와 같은 업로더 MMD) — 골격 이름 규약이 같아 bone_table()·
    # DEFAULT_WEIGHT_MAP을 그대로 쓰고, 이 캐릭터에만 있는 것만 extra/prefix로 얹는다.
    "희귀함_김만경": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_김만경.zip",
        fbx_member="Mondo Owada.fbx",
        mesh_name="Mondo",
        path="Assets/Art/Units/희귀함_김만경/희귀함_김만경.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        # 🔴 리젠트 머리 뼈(リーゼント1/2, Head 직계 자식, 가중치 3,178·3,483 — 정점의 1/4 가까이
        # 차지하는 큰 덩어리)와 옷깃 보조 뼈(BackCollar·CollarParent·Collar_L/R)는 이시마루에
        # 없던 것들 — DEFAULT_WEIGHT_MAP에 없으니 여기서 얹는다(PM 경고: 리젠트를 뼈로 남기면
        # Head가 아니라 Jaw·Eye로 오매핑되는 사고 — 가중치만 Head로 합치고 뼈 자체는 안 만든다).
        weight_map_extra={
            "Head": ["リーゼント1", "リーゼント2"],
            "Chest": ["BackCollar", "BackCollar_L", "BackCollar_R", "CollarParent", "Collar_L", "Collar_R"],
        },
        # 코트 밑단 물리 뼈(CoatLowerClothes_0_1 ~ _7_8, 8×8=64개 낱개 이름) — 전부 나열하는 대신
        # 접두어로 한 번에 Hips로 합친다(뒤에서 접히는 긴 코트라 골반이 가장 자연스러운 정착점).
        weight_prefix_map={"Hips": ["CoatLowerClothes_"]},
        # 재질 45개 → 6장(PM 지시 — 부위별 이름 대응). kami1.png(139B)는 단색 placeholder일
        # 가능성이 커 렌더로 머리색 확인 필요(PM 경고). Tongue/MouthInside는 전용 텍스처(sita.png,
        # 舌=혀)가 따로 있어 face와 분리했다. Trousers·Leg·Shoes·爪(손톱)는 PM이 명시 안 해서
        # uwagi(상의/전신 옷 아틀라스로 추정)에 기본으로 묶고 렌더로 색 확인.
        material_buckets={
            "face": ["Ears", "Neck", "Hand", "Face", "WhiteEye", "Eyebrow", "EyebrowInterval線", "アイライン",
                     "Double線", "鼻線", "UpperTeeth", "LowerTeeth", "m_隈", "m_ピキ", "m_ピキピキ", "m_Face影",
                     "m_Pale", "m_FaceRed", "m_CheekBlush", "Sweat", "Sweat2", "Tears"],
            "tongue": ["Tongue", "MouthInside"],
            "tehuku": ["Tanクトップ"],
            "hair": ["リーゼント", "リーゼントBehhス", "HairBack", "HairLowerStrand"],
            "hitomi": ["Pupil"],
            "fuku": ["CollarOutside", "CollarMiddle", "Coat", "Coat裏地", "Belt", "Buckle", "SleeveButton",
                     "ButtButton", "Button", "TrousersLeft", "TrousersRight", "Leg", "Shoes", "Shoes底", "爪"],
        },
        texture_files={"face": "kaoz_pi.tga.png", "tongue": "sita.png", "tehuku": "s_tehuku.png",
                       "hair": "kami1.png", "hitomi": "hitomi_o.png", "fuku": "uwagi.png"},
    ),
    # 디오(각성) — 이시마루·몬도와 업로더가 달라 뼈 이름이 일본어 원문(".L"/".R" 접미어) +
    # SFM식 영어 얼굴·머리카락·끈 뼈 혼합. IK·D계열·트위스트·センター가 아예 없어(가중치 있는
    # 정점그룹 134개가 그대로 몸 골격 전체) 이시마루·몬도보다 단순하다. 上半身2가 없어
    # torso_mid_bone으로 上半身 60% 지점을 Spine·Chest 가상 경계로 삼는다(둘 다 上半身에서
    # 가중치를 받아 그 구간이 50/50으로 섞이지만 정적 T자라 무해).
    "전설적인_박병규": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_박병규.zip",
        fbx_member="Dio (Awakened).fbx",
        mesh_name="Dio",
        path="Assets/Art/Units/전설적인_박병규/전설적인_박병규.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        bone_sep=".", root_bone="下半身", hip_tail_bones=("足.L", "足.R"),
        torso_mid_bone="上半身", torso_mid_t=0.6,
        spine=SPINE_DIO, limb=LIMB_DIO, finger_chains=FINGER_CHAINS_DIO,
        weight_map=WEIGHT_MAP_DIO,
        # Head_*(표정)·Hair_*(머리카락)·Earring_*(귀걸이)는 SFM식 영어 이름이라 뼈를 따로 안 만들고
        # 전부 Head로, Strap_*(어깨끈)는 Chest로 뭉뚱그린다(좌우 구분 없이 접두어로 한 번에).
        weight_prefix_map={"Head": ["Head_", "Hair_", "Earring_"], "Chest": ["Strap_"]},
        # 재질 7개 중 5개(Body·Face·UpperTeeth·LowerTeeth·Tongue)는 몸 텍스처 한 장을, 눈 2개
        # (EyeL·EyeR)는 눈 전용 텍스처를 쓴다(PM 실측).
        material_buckets={
            "body": ["Body", "Face", "UpperTeeth", "LowerTeeth", "Tongue"],
            "eye": ["EyeL", "EyeR"],
        },
        texture_files={"body": "BUT_IT_WAS_ME_DIO_AWAKENED.png", "eye": "eyel_(Awakened).png"},
    ),
}

# (뼈, 머리 소스뼈, 꼬리 소스뼈, 부모) — 소스뼈 이름으로 head_local을 찾아 뼈 위치를 짓는다.
# "_HIPTAIL_"은 좌우 UpLeg 시작점 평균(특수 처리, 아래 build() 참고).
SPINE = [
    ("Hips", "LowerBody", "_HIPTAIL_", None),
    ("Spine", "UpperBody", "UpperBody2", "Hips"),
    ("Chest", "UpperBody2", "Neck", "Spine"),
    ("Neck", "Neck", "Head", "Chest"),
    ("Head", "Head", "HeadTip", "Neck"),
]
FINGER_CHAINS = [
    ("Thumb1", "Thumb0", "Thumb1", None), ("Thumb2", "Thumb1", "Thumb2", "Thumb1"),
    ("Thumb3", "Thumb2", "ThumbTip", "Thumb2"),
    ("Index1", "IndexFinger1", "IndexFinger2", None), ("Index2", "IndexFinger2", "IndexFinger3", "Index1"),
    ("Index3", "IndexFinger3", "IndexFingerTip", "Index2"),
    ("Middle1", "MiddleFinger1", "MiddleFinger2", None), ("Middle2", "MiddleFinger2", "MiddleFinger3", "Middle1"),
    ("Middle3", "MiddleFinger3", "MiddleFingerTip", "Middle2"),
    ("Ring1", "RingFinger1", "RingFinger2", None), ("Ring2", "RingFinger2", "RingFinger3", "Ring1"),
    ("Ring3", "RingFinger3", "RingFingerTip", "Ring2"),
    ("Pinky1", "LittleFinger1", "LittleFinger2", None), ("Pinky2", "LittleFinger2", "LittleFinger3", "Pinky1"),
    ("Pinky3", "LittleFinger3", "LittleFingerTip", "Pinky2"),
]
LIMB = [
    ("Shoulder", "Shoulder", "Arm", "Chest"), ("Arm", "Arm", "Elbow", "Shoulder"),
    ("ForeArm", "Elbow", "Wrist", "Arm"), ("Hand", "Wrist", "HandTip", "ForeArm"),
    ("UpLeg", "LegD", "KneeD", "Hips"), ("Leg", "KneeD", "AnkleD", "UpLeg"),
    ("Foot", "AnkleD", "LegTipEX", "Leg"), ("ToeBase", "LegTipEX", "_LEGTIPTAIL_", "Foot"),
]
# 손가락은 부모가 전부 Hand(자기 손목), 첫 마디만 예외적으로 표 밖에서 직접 연결한다.
FINGER_PARENT_ROOT = "Hand"

# 목표 뼈 → 합칠 원본 정점그룹(같은 업로더 MMD 표준 뼈 이름 기준, 좌우는 build()가 _L/_R을
# 채운다). 🔴 이 표가 이 스크립트의 핵심 — 트위스트·D계열 가중치를 기능적으로 맞는 목표 뼈
# 하나로 합친다. 캐릭터마다 다른 옷깃·리젠트 머리 같은 추가 뼈는 스킨 설정의 weight_map_extra
# (정확한 이름 추가)·weight_prefix_map(접두어 일치 — CoatLowerClothes_0_1 같은 무더기용)로 얹는다.
DEFAULT_WEIGHT_MAP = {
    "Hips": ["LowerBody"],
    "Spine": ["UpperBody"],
    "Chest": ["UpperBody2", "勲章"],
    "Neck": ["Neck"],
    "Head": ["Head", "Eye_L", "Eye_R", "Tongue1", "Tongue2", "Tongue3"],
    "Shoulder": ["Shoulder_{s}"],
    "Arm": ["Arm_{s}", "ArmTwist_{s}", "ArmTwist1_{s}", "ArmTwist2_{s}", "ArmTwist3_{s}"],
    "ForeArm": ["Elbow_{s}", "HandTwist_{s}", "HandTwist1_{s}", "HandTwist2_{s}", "HandTwist3_{s}", "Sleeve丈_{s}"],
    "Hand": ["Wrist_{s}"],
    "Thumb1": ["Thumb0_{s}"], "Thumb2": ["Thumb1_{s}"], "Thumb3": ["Thumb2_{s}"],
    "Index1": ["IndexFinger1_{s}"], "Index2": ["IndexFinger2_{s}"], "Index3": ["IndexFinger3_{s}"],
    "Middle1": ["MiddleFinger1_{s}"], "Middle2": ["MiddleFinger2_{s}"], "Middle3": ["MiddleFinger3_{s}"],
    "Ring1": ["RingFinger1_{s}"], "Ring2": ["RingFinger2_{s}"], "Ring3": ["RingFinger3_{s}"],
    "Pinky1": ["LittleFinger1_{s}"], "Pinky2": ["LittleFinger2_{s}"], "Pinky3": ["LittleFinger3_{s}"],
    "UpLeg": ["LegD_{s}"], "Leg": ["KneeD_{s}"],
    "Foot": ["AnkleD_{s}", "String1_{s}", "String2_{s}", "String3-1_{s}", "String3-2_{s}", "String4-1_{s}", "String4-2_{s}"],
    "ToeBase": ["LegTipEX_{s}"],
}
# Thumb 계열은 손가락 target 이름이 mixamorig 관례(Thumb1~3, MMD의 Thumb0~2를 한 칸씩 옮김)와
# 달라 위 WEIGHT_MAP 키를 그대로 다시 쓰지만, mixamorig 표준 이름(HandThumb1 등)으로 최종 치환은
# bone_table()에서 처리한다.
FINGER_TARGET_RENAME = {"Thumb1": "Thumb1", "Thumb2": "Thumb2", "Thumb3": "Thumb3",
                        "Index1": "Index1", "Index2": "Index2", "Index3": "Index3",
                        "Middle1": "Middle1", "Middle2": "Middle2", "Middle3": "Middle3",
                        "Ring1": "Ring1", "Ring2": "Ring2", "Ring3": "Ring3",
                        "Pinky1": "Pinky1", "Pinky2": "Pinky2", "Pinky3": "Pinky3"}

def bone_table(spine=None, limb=None, finger_chains=None, finger_rename=None, sep="_"):
    """(뼈이름, 머리소스, 꼬리소스, 부모) — 좌우 팔다리·손가락까지 전부 채운 완성 표.
    🔴 전설적인_박병규(디오) — 이시마루·몬도의 영어 소스뼈 이름·"_" 접미어 규약과 달리 뼈 이름이
    일본어 원문 + ".L"/".R" 접미어라 spine/limb/finger_chains/sep을 스킨별로 갈아 끼울 수 있게
    했다(기본값은 기존 이시마루·몬도용 표 그대로 — 그 둘은 동작 안 바뀜)."""
    spine = spine if spine is not None else SPINE
    limb = limb if limb is not None else LIMB
    finger_chains = finger_chains if finger_chains is not None else FINGER_CHAINS
    finger_rename = finger_rename if finger_rename is not None else FINGER_TARGET_RENAME
    out = list(spine)
    for side, tag in (("Left", "L"), ("Right", "R")):
        for name, head_src, tail_src, parent in limb:
            h = f"{head_src}{sep}{tag}"
            par = parent if parent == "Hips" else (parent if parent == "Chest" else side + parent)
            out.append((side + name, h, (tail_src, tag), par))
        for name, head_src, tail_src, parent in finger_chains:
            par = side + "Hand" if parent is None else side + "Hand" + finger_rename[parent]
            out.append((side + "Hand" + finger_rename[name], (head_src, tag), (tail_src, tag), par))
    return out


def resolve_pos(data, ref, hips_tail_ref=None, sep="_"):
    """소스뼈 이름(문자열) 또는 (이름,side) 튜플, 특수 마커를 실제 head_local 좌표로.
    "_SELF_"(단독 또는 (,"_SELF_",side) 튜플)는 build()가 head 자리에서 직접 처리하므로 여기선
    안 옴 — 그 외 마커만 다룬다."""
    if ref == "_HIPTAIL_":
        return hips_tail_ref
    if ref == "_LEGTIPTAIL_":
        return None  # LegTipEX의 tail_local을 build()에서 직접 채운다
    if isinstance(ref, tuple):
        name, side = ref
        return data.bones[f"{name}{sep}{side}"].head_local.copy()
    return data.bones[ref].head_local.copy()


def bone_name_for(ref, sep="_"):
    """head_ref/tail_ref(문자열 또는 (이름,side) 튜플)를 실제 소스 뼈 이름 문자열로."""
    if isinstance(ref, tuple):
        name, side = ref
        return f"{name}{sep}{side}"
    return ref


def build(name, cfg, out_dir=None, render_dir=None):
    src = os.path.expanduser(cfg["source"])
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src, "sha256": hashlib.sha256(open(src, "rb").read()).hexdigest()}

    import tempfile
    import zipfile
    extract_dir = tempfile.mkdtemp(prefix="mmdskin_")
    with zipfile.ZipFile(src) as z:
        z.extractall(extract_dir)
    fbx_path = None
    tex_paths = {}
    for root_dir, _dirs, files in os.walk(extract_dir):
        for f in files:
            if f == cfg["fbx_member"]:
                fbx_path = os.path.join(root_dir, f)
            elif f.lower().endswith(".png"):
                tex_paths[f] = os.path.join(root_dir, f)
    assert fbx_path, f"{cfg['fbx_member']}을 못 찾음"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    scene = bpy.context.scene
    body = next(o for o in scene.objects if o.type == "MESH")
    src_arm = next(o for o in scene.objects if o.type == "ARMATURE")
    body.name = body.data.name = cfg["mesh_name"]
    report["원본 정점·삼각형"] = [len(body.data.vertices), sum(len(p.vertices) - 2 for p in body.data.polygons)]
    report["원본 뼈(소스)"] = len(src_arm.data.bones)
    report["원본 재질"] = len(body.data.materials)

    # ── 표정 셰이프키(56개) 삭제 — 애니메이션 안 씀, 기본형(Basis)만 남기고 통째로 제거.
    if body.data.shape_keys:
        n_keys = len(body.data.shape_keys.key_blocks)
        for kb in reversed(body.data.shape_keys.key_blocks):
            body.shape_key_remove(kb)
        report["셰이프키 삭제"] = n_keys

    # ── 재질 → 부위별 이름 대응(cfg["material_buckets"]/["texture_files"], 캐릭터마다 재질
    # 이름·텍스처 장수가 완전히 달라 스킨별 설정으로 뺐다). 재질 노드가 원래 없어(텍스처
    # 미연결) 새로 물린다. 얼굴 텍스처는 스킨 톤이 우세하니 원본과 색 비교로 검증한다.
    texture_files = cfg["texture_files"]
    material_buckets = cfg["material_buckets"]

    def bucket_for(mat_name):
        base = mat_name.split(".")[0]
        for bucket, names in material_buckets.items():
            if any(base == n or base.startswith(n) for n in names):
                return bucket
        return cfg.get("material_default_bucket", "wp")

    os.makedirs(tex_dir, exist_ok=True)
    bucket_mat = {}
    for bucket, fname in texture_files.items():
        shutil.copy2(tex_paths[fname], os.path.join(tex_dir, fname))
        mat = bpy.data.materials.new(f"MMD_{bucket}")
        mat.use_nodes = True
        nt = mat.node_tree
        for n in [n for n in nt.nodes if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL")]:
            nt.nodes.remove(n)
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        img_node = nt.nodes.new("ShaderNodeTexImage")
        img_node.image = bpy.data.images.load(os.path.join(tex_dir, fname), check_existing=True)
        nt.links.new(img_node.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Metallic"].default_value = 0.0
        bsdf.inputs["Roughness"].default_value = 0.8
        if hasattr(mat, "blend_method"):
            mat.blend_method = "OPAQUE"
        bucket_mat[bucket] = mat
    old_names = [m.name if m else "" for m in body.data.materials]
    old_bucket = [bucket_for(n) for n in old_names]
    # 🔴 materials.clear()가 먼저면 polygon.material_index가 그 시점에 전부 0으로 눌려서,
    # 뒤에서 읽는 old_bucket[p.material_index]가 전부 같은 값이 된다(실측: 35,374면 전부
    # material_index 1로 몰림) — clear() 전에 원래 인덱스를 파이썬 리스트로 먼저 떠 둔다.
    face_old_index = [p.material_index for p in body.data.polygons]
    new_slot_of = {}
    body.data.materials.clear()
    for bucket in texture_files:
        body.data.materials.append(bucket_mat[bucket])
        new_slot_of[bucket] = len(body.data.materials) - 1
    for p, old_idx in zip(body.data.polygons, face_old_index):
        p.material_index = new_slot_of[old_bucket[old_idx]]
    report["재질 대응"] = {b: sorted({old_names[i] for i, ob in enumerate(old_bucket) if ob == b}) for b in texture_files}

    # ── 뼈대: mixamorig 축약+손가락 세트를 짓는다. 위치는 원본(소스) 뼈의 head_local에서 그대로
    # 가져온다 — LowerBody/UpperBody가 같은 점이라 Hips는 좌우 UpLeg 시작점 평균을 꼬리로 삼아
    # 길이를 만든다(둘 다 대칭이라 x=0).
    # 🔴 전설적인_박병규(디오) — root_bone·hip_tail_bones·sep을 cfg로 갈아 끼울 수 있게 했다
    # (기본값은 이시마루·몬도의 "LowerBody"/"LegD_L,R"/"_" 그대로 — 그 둘은 동작 안 바뀜).
    sep = cfg.get("bone_sep", "_")
    root_bone = cfg.get("root_bone", "LowerBody")
    hip_tail_l, hip_tail_r = cfg.get("hip_tail_bones", ("LegD_L", "LegD_R"))
    sd = src_arm.data
    hips_head = sd.bones[root_bone].head_local.copy()
    hips_tail = Vector(((sd.bones[hip_tail_l].head_local + sd.bones[hip_tail_r].head_local) / 2))
    table = bone_table(cfg.get("spine"), cfg.get("limb"), cfg.get("finger_chains"), cfg.get("finger_rename"), sep)

    # 🔴 디오 전용 — 上半身2(Chest)가 원본에 없어 上半身의 head→tail 60% 지점을 Spine·Chest의
    # 가상 경계로 쓴다(cfg["torso_mid_bone"]/["torso_mid_t"]).
    markers = {}
    torso_mid_bone = cfg.get("torso_mid_bone")
    if torso_mid_bone:
        b = sd.bones[torso_mid_bone]
        markers["_TORSOMID_"] = b.head_local.lerp(b.tail_local, cfg.get("torso_mid_t", 0.6))

    data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", data)
    scene.collection.objects.link(arm)
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for bname, head_ref, tail_ref, parent in table:
        if head_ref == root_bone:
            head = hips_head
        elif head_ref in markers:
            head = markers[head_ref]
        else:
            head = resolve_pos(sd, head_ref, hips_head, sep)
        tail_is_self = tail_ref == "_SELF_" or (isinstance(tail_ref, tuple) and tail_ref[0] == "_SELF_")
        if tail_ref == "_HIPTAIL_":
            tail = hips_tail
        elif tail_ref in markers:
            tail = markers[tail_ref]
        elif tail_is_self:
            tail = sd.bones[bone_name_for(head_ref, sep)].tail_local.copy()
        elif isinstance(tail_ref, tuple) and tail_ref[0] == "_LEGTIPTAIL_":
            tail = sd.bones[f"LegTipEX_{tail_ref[1]}"].tail_local.copy()
        else:
            tail = resolve_pos(sd, tail_ref, hips_head, sep)
        eb = data.edit_bones.new(PREFIX + bname)
        eb.head, eb.tail = head, tail
        if (eb.tail - eb.head).length < 1e-5:
            eb.tail = eb.head + Vector((0, 0, 0.01))
        d = (eb.tail - eb.head).normalized()
        eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    for bname, head_ref, tail_ref, parent in table:
        if parent:
            data.edit_bones[PREFIX + bname].parent = data.edit_bones[PREFIX + parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    report["뼈"] = len(data.bones)

    # 🔴 PM 실측(유니티 줄세우기 — Idle 리타게팅 실패, 골격 357개로 원본 302개가 거의 그대로
    # 남아 있었다) — 새 뼈대(51개)를 다 지은 뒤에도 원본 MMD 아마추어(src_arm)를 씬에서 안
    # 지우면 내보내기가 그것까지 통째로 같이 담는다. IK·더미·그림자·D·트위스트가 사람 뼈
    # 사슬 바깥(루트 쪽)에 매달려 있어 Idle이 골반·다리를 움직여도 안 따라가 뼈구름이 벌어졌다.
    # 위치 참고는 다 끝났으니(sd 참조 전부 이 줄 위에서 끝) 원본 아마추어 오브젝트·데이터를
    # 통째로 지운다 — 남기는 뼈가 아니라 아예 하나도 안 남긴다(자식 관계 등으로 새길 것 없음).
    for mod in [m for m in body.modifiers if m.type == "ARMATURE"]:
        body.modifiers.remove(mod)                        # 원본 임포트가 만든, src_arm을 겨눈 모디파이어
    src_arm_data = src_arm.data
    bpy.data.objects.remove(src_arm, do_unlink=True)
    bpy.data.armatures.remove(src_arm_data)

    # ── 가중치: DEFAULT_WEIGHT_MAP(+스킨별 weight_map_extra/weight_prefix_map)대로 소스
    # 정점그룹을 목표 뼈로 합친다(누적 — 겹치면 더함, 끝에 정규화).
    weight_map = cfg.get("weight_map", DEFAULT_WEIGHT_MAP)
    weight_map_extra = cfg.get("weight_map_extra", {})
    weight_prefix_map = cfg.get("weight_prefix_map", {})
    src_groups = {vg.name: vg.index for vg in body.vertex_groups}
    for b in data.bones:
        body.vertex_groups.new(name=b.name)
    target_groups = {vg.name[len(PREFIX):]: vg for vg in body.vertex_groups if vg.name.startswith(PREFIX)}
    merged, missing_src, used_src = 0, set(), set()
    for bname, head_ref, tail_ref, parent in table:
        side = "L" if bname.startswith("Left") else ("R" if bname.startswith("Right") else None)
        side_long = "Left" if side == "L" else ("Right" if side == "R" else None)
        base = bname[4:] if side == "L" else (bname[5:] if side == "R" else bname)
        if base.startswith("Hand") and base != "Hand":       # LeftHandThumb1 -> HandThumb1 -> Thumb1
            base = base[4:]
        sources = list(weight_map.get(base, [])) + list(weight_map_extra.get(base, []))
        if not sources and base not in weight_map and base not in weight_map_extra:
            continue
        tgt = target_groups[bname]
        for s in sources:
            # 🔴 디오의 어깨·팔꿈치 보조 뼈(Arm_Left_Shoulder_Adj_A 등)는 ".L"/".R"이 아니라
            # "_Left"/"_Right" 긴 이름이라 {S}로 따로 채운다(관절 본체는 {s}=".L"/".R").
            sname = s.format(s=side, S=side_long) if side else s
            if sname not in src_groups:
                missing_src.add(sname)
                continue
            used_src.add(sname)
            sidx = src_groups[sname]
            for v in body.data.vertices:
                for g in v.groups:
                    if g.group == sidx and g.weight > 1e-4:
                        tgt.add([v.index], g.weight, "ADD")
                        merged += 1
    # 접두어 일치 병합(캐릭터 전용 무더기 그룹, 예: 코트 자락 물리 뼈 CoatLowerClothes_0_1…7_8) —
    # 좌우 구분 없는 스킨 전체 규칙이라 bname 순회 밖에서 한 번만.
    for tgt_name, prefixes in weight_prefix_map.items():
        tgt = target_groups[tgt_name]
        for sname, sidx in src_groups.items():
            if sname in used_src or not any(sname.startswith(p) for p in prefixes):
                continue
            used_src.add(sname)
            for v in body.data.vertices:
                for g in v.groups:
                    if g.group == sidx and g.weight > 1e-4:
                        tgt.add([v.index], g.weight, "ADD")
                        merged += 1
    report["병합한 원본 정점그룹 가중치 항목"] = merged
    unused = sorted(set(src_groups) - used_src)
    if unused:
        report["병합 안 된 원본 정점그룹(가중치 있었는데 버려짐 — 확인 필요)"] = unused
    if missing_src:
        report["WEIGHT_MAP에 없는 소스그룹(무시됨)"] = sorted(missing_src)
    # 🔴 원본(MMD) 87개 정점그룹은 지우지 않으면 남아 있다 — add(...,"ADD")는 새 mixamorig 그룹에
    # 더할 뿐 원본 그룹 자체를 안 건드리므로, 그대로 두면 dead_bones()·정규화가 원본 가중치까지
    # 겹쳐 세서 틀린다. 목표 뼈로 다 옮겼으니 원본은 전부 지운다.
    for vg in [vg for vg in body.vertex_groups if not vg.name.startswith(PREFIX)]:
        body.vertex_groups.remove(vg)
    # 정규화(합이 1 되게) — 손가락·주요 관절은 원래도 배타적이라 대개 그대로지만 방어적으로.
    for v in body.data.vertices:
        total = sum(g.weight for g in v.groups)
        if total > 1e-6:
            for g in v.groups:
                g.weight /= total

    def dead_bones():
        counts = {b.name[len(PREFIX):]: 0 for b in data.bones}
        for v in body.data.vertices:
            for g in v.groups:
                if g.weight > 0.01:
                    counts[body.vertex_groups[g.group].name[len(PREFIX):]] += 1
        return [k for k, c in counts.items() if c == 0], counts

    dead, counts = dead_bones()
    report["뼈별 정점(w>0.01)"] = counts
    assert not dead, f"{name}: 가중치 없는 뼈 {dead} — WEIGHT_MAP 확인 필요"

    # 🔴 무가중치 정점 채우기(gen_objrip_skin.py에서 검증한 방식, 유니티 실측으로 값어치 확인됨) —
    # 302개 중 215개 보조 뼈를 통째로 안 만들었으니 그 뼈에만 실려 있던 정점이 있으면 여기서 잡힌다.
    weighted_idx = [v.index for v in body.data.vertices if sum(g.weight for g in v.groups) > 1e-6]
    unweighted_idx = [v.index for v in body.data.vertices if sum(g.weight for g in v.groups) <= 1e-6]
    if unweighted_idx:
        kd_w = KDTree(len(weighted_idx))
        for i, vi in enumerate(weighted_idx):
            kd_w.insert(body.data.vertices[vi].co, i)
        kd_w.balance()
        for vi in unweighted_idx:
            _, ri, _ = kd_w.find(body.data.vertices[vi].co)
            src_vi = weighted_idx[ri]
            for g in body.data.vertices[src_vi].groups:
                if g.weight > 1e-4:
                    body.vertex_groups[g.group].add([vi], g.weight, "REPLACE")
        report["무가중치 정점 채움(가장 가까운 정점 복사)"] = len(unweighted_idx)
    total_unweighted = sum(1 for v in body.data.vertices if sum(g.weight for g in v.groups) <= 1e-6)
    report["무가중치 정점(내보내기 전)"] = total_unweighted
    assert total_unweighted == 0, f"{name}: 무가중치 정점 {total_unweighted}개 남음"

    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    body.parent = arm

    report["팔 처짐→수평(°)"] = straighten_arms(arm, body)

    # ── 중심맞춤·스케일 — 발 접지 뼈(AnkleD) 높이 밴드로 원점, 키 1.8m.
    bpy.context.view_layer.update()
    V = np.array([v.co for v in body.data.vertices])
    lo, hi = V.min(0), V.max(0)
    H = float(hi[2] - lo[2])
    band = V[(V[:, 2] >= lo[2] + H * cfg["center_band"][0]) & (V[:, 2] <= lo[2] + H * cfg["center_band"][1])]
    cx, cy = float((band[:, 0].min() + band[:, 0].max()) / 2), float((band[:, 1].min() + band[:, 1].max()) / 2)
    s = cfg["height"] / H
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo[2]))
    report["원본 키(파일 단위)"], report["배율"] = round(H, 4), round(s, 6)
    body.data.transform(G)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.head, eb.tail = G @ eb.head, G @ eb.tail
    bpy.ops.object.mode_set(mode="OBJECT")

    report["UV0"] = check_uv0(body.data)

    bpy.context.view_layer.update()
    V = np.array([v.co for v in body.data.vertices])
    report["크기(m)"] = [round(float(c), 3) for c in (V.max(0) - V.min(0))]
    report["최저 z"] = round(float(V[:, 2].min()), 4)

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    shutil.rmtree(extract_dir, ignore_errors=True)
    if render_dir:
        report["렌더"] = judge(name, arm, body, render_dir)
    return report


def straighten_arms(arm, body):
    """MMD 원본 팔이 수평에서 23~29° 처져 있다(실측) — 어깨·팔꿈치·손목 뼈를 각각 좌우 수평
    (±X)으로 돌려 그 자세를 쉬는 자세로 굽는다(gen_skin_rig.py의 level_arms()와 같은 기법).
    다리는 이미 0.2~3.5°로 사실상 수직이라 건드리지 않는다."""
    scene = bpy.context.scene
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    turned = {}
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        for bone in ("Shoulder", "Arm", "ForeArm", "Hand"):
            pb = arm.pose.bones[PREFIX + side + bone]
            head, tail = pb.matrix.to_translation(), pb.tail.copy()
            d = (tail - head).normalized()
            target = Vector((sx, 0.0, 0.0))
            turned[side + bone] = round(math.degrees(d.angle(target)), 1)
            q = d.rotation_difference(target)
            pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
            bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    want = np.array([v.co for v in baked.vertices])
    old = body.data
    body.data = baked
    baked.name = old.name
    bpy.data.meshes.remove(old)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    got = np.array([v.co for v in me.vertices])
    ev.to_mesh_clear()
    drift = float(np.abs(got - want).max())
    assert drift < 1e-4, f"팔 펴기 뒤 메시가 움직였다 {drift}"
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        b = arm.data.bones[PREFIX + side + "Hand"]
        d = (b.tail_local - b.head_local).normalized()
        assert d.dot(Vector((sx, 0, 0))) > 0.99, f"{side}Hand 정렬 실패 {d}"
    # 🔴 PM 실측(유니티, 전설적인_박병규/디오) — 조합판 Idle(팔 내림)에서 위팔이 비틀린 수건처럼
    # 가늘어지고 팔꿈치가 옆으로 꺾여 튀어나왔다. 원인: pose bone을 d.rotation_difference(target)
    # 로 돌리면 방향(swing)만 목표에 맞춰질 뿐 그 축을 중심으로 한 회전(roll·비틀림)은 원본 MMD
    # 자세 그대로 뼈마다 제각각 남는다 — 지금 T자 렌더는 멀쩡해 보이는데(메시는 바인드=레스트라
    # roll이 뭐든 변형이 항등이라 안 보인다), 나중에 유니티가 팔을 실제로 움직이면 그 제각각의
    # roll이 팔꿈치 굽힘 축을 엉뚱한 방향으로 돌려 놓는다. armature_apply로 새 레스트를 구운
    # 지금(바인드=레스트라 롤을 바꿔도 메시는 안 움직인다) 편집 모드에서 bone_table() 만들 때와
    # 똑같은 규칙(Vector((0,-1,0)) 기준, 수평 뼈라 전부 이 분기)으로 roll을 다시 맞춘다.
    bpy.ops.object.mode_set(mode="EDIT")
    for side in ("Left", "Right"):
        for bone in ("Shoulder", "Arm", "ForeArm", "Hand"):
            eb = arm.data.edit_bones[PREFIX + side + bone]
            d = (eb.tail - eb.head).normalized()
            eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    return turned


def check_uv0(mesh):
    uv = mesh.uv_layers.active
    if uv is None:
        return {"UV 없음": True}
    us = np.array([d.uv[0] for d in uv.data])
    vs = np.array([d.uv[1] for d in uv.data])
    outside = int(np.sum((us < -1e-4) | (us > 1 + 1e-4) | (vs < -1e-4) | (vs > 1 + 1e-4)))
    return {"범위": [round(float(us.min()), 4), round(float(us.max()), 4), round(float(vs.min()), 4), round(float(vs.max()), 4)],
            "밖 정점(루프)": outside}


def judge(name, arm, body, out):
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 800, 1000
    scene.world = bpy.data.worlds.new("판정")
    scene.world.color = (0.3, 0.3, 0.3)
    cam = bpy.data.objects.new("판정_카메라", bpy.data.cameras.new("판정_카메라"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.2
    views = [("front", (0, -5, 0.9), (math.radians(90), 0, 0)), ("side", (5, 0, 0.9), (math.radians(90), 0, math.radians(90)))]
    paths = []
    for tag, loc, rot in views:
        cam.location, cam.rotation_euler = loc, rot
        p = os.path.join(out, f"{name}_{tag}.png")
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    return paths


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
        print("MMD리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
