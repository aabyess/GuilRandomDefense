"""리기파이(Rigify) 이름 뼈대가 이미 있는 정적 glb를 mixamorig 사람형 FBX로 짓는다.
(gen_skin_rig.py와 다른 계열 — 그쪽은 뼈 0개에서 자동가중치로 새로 짓고, 여기는 이미 있는
스킨을 mixamorig 이름으로 고쳐 쓰는 것. 2026-09-16 첫 건 희귀함_황준석/나나미.)

  blender -b --factory-startup --python Tools/blender/gen_rigify_skin.py -- 희귀함_황준석 [--out DIR] [--render DIR]

결과: <유닛>.fbx + Textures/ · 키 1.8m(머리카락 포함 전체 실루엣 기준) · 발 z 0 · 정면 −Y ·
mixamorig 22뼈(다른 두 스크립트와 같은 이름 규칙, PREFIX 동일) · T자 쉬는 자세.

리기파이 표준 체인(spine→spine.006, thigh/shin/foot/toe.L·R, shoulder/upper_arm/forearm/hand.L·R)을
22뼈로 접는다 — 손가락(palm·f_*·thumb)은 그 손으로, 목/머리 여분(spine.006)은 머리로, 가슴 흔들
뼈(breast)는 Spine2로, 골반 보조뼈(pelvis)는 Hips로, 뒤꿈치 보조(heel.02)는 발로 몰아 넣고
지운다 — 접은 뒤 남는 뼈가 22개뿐이어야 한다(가중치 0 뼈가 남으면 유니티 경계가 부푼다).

정점색(COLOR_0)만 있고 이미지가 없는 재질은 UV로 구워 PNG로 만든다 — 안 구우면 FBX에서
정점색 셰이더 노드가 통째로 사라져 유니티가 새하얗게/회색으로 그린다.
🔴 같은 재질(예: Cycles Toon Shader)을 몸·머리카락·눈썹처럼 서로 다른 정점색을 가진 조각
여럿이 같이 쓰면, 그 조각들이 각자 독립적으로 0~1 UV를 다 쓰고 있어서 한 장에 같이 구우면
서로 겹쳐 색이 섞인다 — 조각별로 재질을 단일 사용자로 복제해 따로따로 굽는다(재질 슬롯 수가
원본 재질 개수보다 늘어나는 이유)."""
import json
import math
import os
import re
import struct
import sys
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PREFIX = "mixamorig:"

# ── 유닛별 설정 ────────────────────────────────────────────────────────────

SKINS = {
    "희귀함_황준석": dict(
        # 사장님 스킨 모음집(2026-09-15 규칙) — 폴더 안 zip. 압축 풀 곳은 스크립트가 임시로 만든다.
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_황준석.zip",
        glb_member="source/Nanami_rest.glb",
        tie_texture_member="textures/TieLeopardPattern.png",
        path="Assets/Art/Units/희귀함_황준석/희귀함_황준석.fbx",
        mesh_name="Nanami",
        height=1.8,                                          # 머리카락 포함 전체 실루엣 기준(원본 top z 2.0136)
        # 제외할 잡동사니 메시(부모 없음·재질 없음, 캐릭터와 무관한 임포트 찌꺼기 — 직접 확인).
        drop_meshes={"Cube", "Icosphere"},
        # 스킨이 아예 없는 조각 → 뼈 하나에 100% 고정(rigid). 오른손(무기 bbox x<0, hand.R과
        # 같은 쪽)·머리(홍채, Auto_Eye라는 빈 오브젝트의 자식이라 원래 스킨 개념이 없다).
        rigid={"Weapon": "RightHand", "Auto_Eye_IRIS": "Head"},
        # 리기파이 표준 체인 → mixamorig. 좌우는 원본이 이미 .L=+X(왼쪽)·.R=−X(오른쪽)라
        # 그대로 옮긴다(실측: hand.L x=+0.425·hand.R x=−0.425 — mixamorig Left=+X 규칙과 일치).
        rename={
            "spine": "Hips", "spine.001": "Spine", "spine.002": "Spine1", "spine.003": "Spine2",
            "spine.004": "Neck", "spine.005": "Head",
            "shoulder.L": "LeftShoulder", "upper_arm.L": "LeftArm", "forearm.L": "LeftForeArm", "hand.L": "LeftHand",
            "shoulder.R": "RightShoulder", "upper_arm.R": "RightArm", "forearm.R": "RightForeArm", "hand.R": "RightHand",
            "thigh.L": "LeftUpLeg", "shin.L": "LeftLeg", "foot.L": "LeftFoot", "toe.L": "LeftToeBase",
            "thigh.R": "RightUpLeg", "shin.R": "RightLeg", "foot.R": "RightFoot", "toe.R": "RightToeBase",
        },
        # 22뼈 밖의 리기파이 보조/여분 뼈 → 접어 넣을 자리(가중치를 옮기고 뼈 자체는 지운다).
        # 직접 확인(Nanami 메시 가중치 조회): 전부 실제로 정점을 물고 있다(0이 아님) — 안 접으면
        # 그 정점들이 허공에 남는다.
        fold={
            "spine.006": "Head",                              # 목~머리 체인이 22뼈(Neck·Head)보다 한 마디 길다
            "breast.L": "Spine2", "breast.R": "Spine2",        # 가슴 흔들뼈
            "pelvis.L": "Hips", "pelvis.R": "Hips",            # 골반 보조뼈
            "heel.02.L": "LeftFoot", "heel.02.R": "RightFoot",  # 뒤꿈치 IK 보조뼈
            "neutral_bone": "Hips",                            # ⚠️ 원점(0,0,0) 기준뼈인데 정점 548개(최대가중치 1.0)가
                                                                # 물려 있다 — 어느 부위인지 특정 못 해 가장 안 움직이는
                                                                # Hips로 보냈다(추정, 사장님/PM 확인 필요하면 보고).
        },
        # 손가락 체인(palm·손가락 마디·엄지) → 그 손(Hand)으로. 이름 접두로 판정.
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        # 재질별 굽기 방식. "vertex_color"면 그 재질을 쓰는 조각마다 재질을 복제해 따로 굽는다
        # (몸·머리카락·눈썹처럼 서로 다른 정점색을 가진 조각이 같은 재질명을 공유해서 — 한 장에
        # 같이 구우면 UV가 겹쳐 색이 섞인다). "texture_file"이면 zip의 그 파일을 그대로 잇는다.
        # "solid"면 원본에 색 정보 자체가 없어(정점색도 이미지도 없음) 무난한 기본색으로 채운다.
        materials=dict(
            **{"Cycles Toon Shader": ("vertex_color", None)},
            **{"Tie": ("texture_file", "tie_texture_member")},
            **{"Metal": ("vertex_color", None)},
            **{"Glass": ("vertex_color", None)},
            # 눈 재질 셋 — glb에 정점색도 이미지도 없다(procedural 눈 애드온 셰이더가 glTF로 안 나옴,
            # 직접 확인: color_attributes 빈 리스트). 무난한 기본색으로 채운다(추정, 보고 대상).
            **{"Auto Eye - IRIS": ("solid", (0.30, 0.18, 0.08, 1.0))},        # 짙은 갈색 홍채
            **{"Auto Eye - PUPIL": ("solid", (0.02, 0.02, 0.02, 1.0))},      # 검정 동공
            **{"Auto Eye - CORNEA/SCLERA": ("solid", (0.92, 0.90, 0.86, 1.0))},  # 흰자
        ),
        rotate_z=0.0,                                          # 정면 이미 −Y(눈썹 y가 몸통 y범위 중 음의 극단 쪽에 확인)
        level_arms=True,                                       # 팔이 옆으로 늘어진 자세 → 사후 수평(T자)
        decimate_ratio=0.10,                                   # 38.3만 → 약 3.8만(주영호·최준우와 같은 4만대 목표)
        bake_size=2048,                                         # 1024는 작은 UV 섬끼리 여백(margin)이 번져 색이 섞였다(실측)
    ),
    "희귀함_배현진": dict(
        # 소스가 zip 속 glb가 아니라 .blend 통짜 파일 — source_type="blend"로 open_mainfile 경로를 탄다.
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_배현진.zip",
        source_type="blend",
        blend_member="source/Sketchfab_2023_10_30_10_06_59.blend",
        armature_name="Kasumi Miwa Rig ",                       # 뼈 하나짜리 무기 rig(Blade/Cabbard/Sword Rig)가
                                                                  # 따로 3개 더 있어 next()로 아무거나 집으면 안 된다.
        path="Assets/Art/Units/희귀함_배현진/희귀함_배현진.fbx",
        mesh_name="Kasumi",
        height=1.8,
        # Blade는 실측 결과 Cabbard(칼집)와 월드 bbox가 거의 겹친다(칼집 안에 들어있는 칼날 —
        # z 0.60~0.89 vs 칼집 0.60~0.89, y −0.21~0.63 vs 칼집 −0.03~0.64) → 칼집이 이미 그
        # 실루엣을 가리므로 칼날은 실루엣에 안 보인다, 버린다. Blade Rig·Sword Rig(칼날 전용
        # 뼈 1개짜리)는 자동으로 안 쓰인다(join 대상이 아니면 export 선택에도 안 들어간다).
        # Cube는 캐릭터와 무관한 찌꺼기(직접 확인: 정점그룹 0개, 몸통과 안 겹치는 위치 z −1~1
        # — 리기파이 뼈 커스텀 모양(위젯)용 기본 정육면체로 보인다). 첫 빌드에서 그대로 같이
        # 나가는 걸 발견해 추가(뼈 목록 조사 때는 hidden collection이라 안 보였던 것으로 추정).
        # 🔴 Cabbard(칼집)도 뺀다(2026-09-23, 사장님이 게임에서 「칼이 몸을 관통한다」고 지적 → PM 지시로 재판정).
        #   1차엔 「허리에 찬 장비」로 보고 Spine 강체로 남겼는데, 재수입 + Idle 리타겟 렌더로 직접 보니 칼집이
        #   골반~허벅지를 **대각선으로 뚫고** 반대쪽으로 튀어나온다 — 원본에서 칼집이 스킨이 아예 없는(Armature
        #   모디파이어 없는) 조각이라 어느 뼈 하나에 통째로 묶으면 몸을 따라갈 수가 없다. 게다가 칼날(Blade)은
        #   1차에서 이미 뺐으므로 남은 건 빈 칼집뿐이다. 손에 든 무기를 빼는 선례와 같은 처리.
        drop_meshes={"Blade", "Cube", "Cabbard"},
        # 스킨이 아예 없는 조각(Armature 모디파이어 자체가 없다, 직접 확인) — head·hair·hair
        # highlight는 얼굴/머리 덩어리라 Head에, Cabbard(칼집)는 허리~등 높이(월드 z 0.6~0.89,
        # spine 뼈 z 1.12~1.27보다 낮다)라 허리 쪽 Spine에 강체 고정.
        rigid={"head": "Head", "hair": "Head", "hair highlight": "Head"},   # Cabbard는 위 drop_meshes로 뺐다
        # 나나미와 사실상 같은 리기파이 표준 체인(뼈 68개, 이름 동일) — 직접 확인.
        rename={
            "spine": "Hips", "spine.001": "Spine", "spine.002": "Spine1", "spine.003": "Spine2",
            "spine.004": "Neck", "spine.005": "Head",
            "shoulder.L": "LeftShoulder", "upper_arm.L": "LeftArm", "forearm.L": "LeftForeArm", "hand.L": "LeftHand",
            "shoulder.R": "RightShoulder", "upper_arm.R": "RightArm", "forearm.R": "RightForeArm", "hand.R": "RightHand",
            "thigh.L": "LeftUpLeg", "shin.L": "LeftLeg", "foot.L": "LeftFoot", "toe.L": "LeftToeBase",
            "thigh.R": "RightUpLeg", "shin.R": "RightLeg", "foot.R": "RightFoot", "toe.R": "RightToeBase",
        },
        fold={
            "spine.006": "Head", "breast.L": "Spine2", "breast.R": "Spine2",
            "pelvis.L": "Hips", "pelvis.R": "Hips", "heel.02.L": "LeftFoot", "heel.02.R": "RightFoot",
            "face": "Head",                                     # 나나미의 neutral_bone 자리에 이 유닛은 face가 있다
                                                                  # (직접 확인, 나머지 67개는 동일) — 얼굴 보조뼈라 Head로.
        },
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        # 전부 Emission(정점색 아님) — TEX_IMAGE가 곧바로 Emission에 물려 있는 단순 구조(직접
        # 확인: head skin/hair/tie primary 노드 그래프 셋 다 동일 패턴). "emission_texture"는
        # 굽기 없이 그 이미지를 그대로 Base Color로 재배선한다(이미지가 없는 재질을 만나면
        # 자동으로 무난한 단색으로 대체 — PM이 우려한 "top suit secondary 텍스처 없음"도 이걸로
        # 커버). OH_Outline_Material은 목록에서 뺀다 — join 이전 단계에서 슬롯 자체를 지운다
        # (그 재질은 OH_OUTLINE 모디파이어가 즉석에서 만드는 셸 면 전용이라 원본 메시엔 실제로
        # 그 재질을 쓰는 면이 0개, 직접 확인: OH_OUTLINE은 Geometry Nodes가 아니라 SOLIDIFY
        # 모디파이어였다 — 파이프라인이 애초에 .data 원본 메시만 쓰고 모디파이어를 평가하지
        # 않으므로 이 셸은 join 이전에도 절대 섞여 들어오지 않는다).
        materials={n: ("emission_texture", None) for n in (
            "bottom suit primary", "bottom suit secondary", "socks",
            "hand skin", "hand skin shadow",
            "head skin", "head skin shadow", "inside of eye", "inside of mouth",
            "eyelashes", "eyelashes secondary", "eye primary", "eye secondary",
            "hair", "hair highlight", "shirt",
            "shoes 1", "shoes 2", "shoes shine",
            "tie primary", "tie secondary",
            "top suit primary", "top suit secondary", "sleeves", "buttons",
            "handle tip 2", "purple ribbon", "scabbard color",
        )},
        max_texture_size=512,                                    # 원본 4096×4096 — 이 저폴리 스타일엔 과하다(PM 지시)
        rotate_z=0.0,                                            # 나나미와 같은 소스 계열(Sketchfab 리기파이) 추정,
                                                                  # --render로 실제 확인할 것
        level_arms=True,
        decimate_ratio=1.0,                                      # 이미 저폴리(합쳐서 7,422 삼각형) — 감량 불필요
    ),
    # 전설적인_임채민(죠나단 죠스타, JoJo 1부) — PM 사양 원문 보존(2026-09-17):
    # ※ 임채민 동명 셋 — 희귀함_임채민(이시마루)은 이미 있음. 이건 전설적인_임채민.
    # 원본: source/Jojo PT1.glb(SMD 게임 추출) + textures/body1_0.png(2048×1024)·eye_1.png(256²).
    # 아마추어 p1jnt01_ARM · 뼈 74(직접 확인 일치): bip_pelvis→Hips·bip_spine_0/1→Spine/Spine2
    # (척추 2마디뿐이라 mixamorig 6마디에 하나 모자람 — Spine1은 bip_spine_0 꼬리의 0-길이
    # 자리표시로 처리, allow_dead_bones+bone_position_override로 가중치 없이 통과)·
    # bip_neck→Neck·bip_head→Head·bip_collar_L/R→Shoulder·bip_upperArm→Arm·bip_lowerArm→
    # ForeArm·bip_hand_L(소문자 h)·bip_Hand_R(대문자 H, 직접 확인 — 대소문자 다름 주의)→Hand·
    # bip_hip→UpLeg·bip_knee→Leg·bip_foot→Foot·bip_toe→ToeBase. 손가락은 나나미·카스미와 같은
    # Rigify 규칙(palm.0N.side·f_index/f_middle/f_ring/f_pinky.0N.side·thumb.0N.side) →
    # finger_prefixes가 그대로 처리.
    # Head 직계 자식 12(직접 확인 일치): Eyebrow.L/R·Cheek.L/R·Mouth.L/R·MouthTop·Nose·
    # LowerJaw(자손 MouthBottom·TeethLower·Tongue 포함)·Eye.L/R·TeethUpper → fold_subtree로
    # 한 번에 Head.
    # 메시 8(_jnt01t0_ 접두, body 4,886·face 1,173·hair 2,153·eye_l/r 97×2·lower_teeth 97·
    # tongue 77·upper_teeth 110) + Icosphere(조명 구, 드롭) · 삼각형 약 1.2만(감량 불필요).
    # 재질 body1·eye 전부 이미 Principled+TEX_IMAGE 직결(정점색 없음, 직접 확인) — emission_
    # texture 굽기 방식이 이미지 있으면 그대로 재배선하는 분기를 그대로 재사용(중복이지만 안전).
    # 좌우 대칭 99.7%·이미 T자에 가까움(bip_upperArm_L z 1.495→bip_hand_L z 1.474, 거의 수평,
    # PM 실측과 일치) → level_arms 불필요.
    "전설적인_임채민": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_임채민.zip",
        glb_member="source/Jojo PT1.glb",
        path="Assets/Art/Units/전설적인_임채민/전설적인_임채민.fbx",
        mesh_name="Jonathan",
        height=1.8,
        drop_meshes={"Icosphere"},
        rename={
            "bip_pelvis": "Hips", "bip_spine_0": "Spine", "bip_spine_1": "Spine2",
            "bip_neck": "Neck", "bip_head": "Head",
            "bip_collar_L": "LeftShoulder", "bip_upperArm_L": "LeftArm", "bip_lowerArm_L": "LeftForeArm", "bip_hand_L": "LeftHand",
            "bip_collar_R": "RightShoulder", "bip_upperArm_R": "RightArm", "bip_lowerArm_R": "RightForeArm", "bip_Hand_R": "RightHand",
            "bip_hip_L": "LeftUpLeg", "bip_knee_L": "LeftLeg", "bip_foot_L": "LeftFoot", "bip_toe_L": "LeftToeBase",
            "bip_hip_R": "RightUpLeg", "bip_knee_R": "RightLeg", "bip_foot_R": "RightFoot", "bip_toe_R": "RightToeBase",
        },
        fold={},
        fold_subtree={"Eyebrow.L": "Head", "Eyebrow.R": "Head", "Cheek.L": "Head", "Cheek.R": "Head",
                      "Mouth.L": "Head", "Mouth.R": "Head", "MouthTop": "Head", "Nose": "Head",
                      "LowerJaw": "Head", "Eye.L": "Head", "Eye.R": "Head", "TeethUpper": "Head"},
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        bone_position_override={"Spine1": ("bip_spine_0", "tail")},
        allow_dead_bones={"Spine1"},
        materials={"body1": ("emission_texture", None), "eye": ("emission_texture", None)},
        rotate_z=0.0,
        level_arms=False,
        decimate_ratio=1.0,
        bake_size=1024,
    ),
    # 제한_전법규(산타나, JoJo 1부 기둥의 남자) — PM 사양 원문 보존(2026-09-17):
    # 원본: source/santana.zip 안 santana.blend + Santana.png.
    # 메시 1(Mesh_0000.rip, 4,339정점·6,064면·무가중치 0·Armature 수정자) · z 0~2.05 ·
    # x ±1.05(팔 벌린 자세) · 재질 Mesh_0000.rip → //Santana.png.
    # 아마추어 metarig 뼈 50: pelvis·spine.001~003·neck·head·shoulder/upper_arm/forearm/
    # hand.L/R·손가락 index/thumb/middle/ring/pinky_0~2_L/R·thigh/shin/foot.L/R (Rigify
    # 규칙, 죠나단과 같은 계열이나 toe 없음 — foot이 끝).
    # 🔴 아마추어 오브젝트 자체 배율 1.0396(직접 확인) — old_head_tail에 matrix_world를
    # 곱하도록 gen_rigify_skin.py에 일반 수정(히나타 때 gen_biped_skin.py에 넣은 것과 같음).
    "제한_전법규": dict(
        source="~/Desktop/구랜디스킨모음/07_제한됨/제한_전법규.zip",
        source_type="blend",
        blend_member="source/santana.zip",
        inner_blend="santana.blend",
        path="Assets/Art/Units/제한_전법규/제한_전법규.fbx",
        mesh_name="Santana",
        height=1.8,
        drop_meshes=set(),
        rigid={},
        rename={
            "pelvis": "Hips", "spine.001": "Spine", "spine.002": "Spine1", "spine.003": "Spine2",
            "neck": "Neck", "head": "Head",
            "shoulder.L": "LeftShoulder", "upper_arm.L": "LeftArm", "forearm.L": "LeftForeArm", "hand.L": "LeftHand",
            "shoulder.R": "RightShoulder", "upper_arm.R": "RightArm", "forearm.R": "RightForeArm", "hand.R": "RightHand",
            "thigh.L": "LeftUpLeg", "shin.L": "LeftLeg", "foot.L": "LeftFoot",
            "thigh.R": "RightUpLeg", "shin.R": "RightLeg", "foot.R": "RightFoot",
        },
        fold={},
        fold_subtree={
            "index_0_L": "LeftHand", "thumb_0_L": "LeftHand", "middle_0_L": "LeftHand", "ring_0_L": "LeftHand", "pinky_0_L": "LeftHand",
            "index_0_R": "RightHand", "thumb_0_R": "RightHand", "middle_0_R": "RightHand", "ring_0_R": "RightHand", "pinky_0_R": "RightHand",
        },
        # 발가락 뼈가 아예 없다(foot이 끝) — 죠나단 Spine1과 같은 방식으로 foot 꼬리에
        # 0-길이 자리표시.
        bone_position_override={"LeftToeBase": ("foot.L", "tail"), "RightToeBase": ("foot.R", "tail")},
        allow_dead_bones={"LeftToeBase", "RightToeBase"},
        finger_prefixes=(),
        materials={"Mesh_0000.rip": ("emission_texture", None)},
        rotate_z=0.0,
        level_arms=True,
        decimate_ratio=1.0,
    ),
    # 초월_이재윤_AD(옷코츠 유타, Sketchfab) — PM 사양 원문 보존(2026-09-18): 카스미와 같은
    # 계열(같은 작가·시기 — spine.001~006·pelvis.L/R·heel.02.L/R Rigify 표준 체인, 직접
    # 확인 이름까지 동일) 재사용.
    # 아마추어 2개: "Yuta Okkotsu Rig"(157뼈, 본체) + "Sword Rig"(1뼈, 칼 전용) → Sword
    # Rig·Sword 메시 전부 뺌(카스미의 Blade Rig와 같은 원칙).
    # 메시 6(hair·hands·head·pants·shirt·shoes) 전부 SUBSURF+SOLIDIFY+ARMATURE 수정자 —
    # 파이프라인은 .data 원본만 써서 두 수정자는 자동 무시(모디파이어 평가 안 함), OH_Outline_
    # Material은 SOLIDIFY 셸 전용이라 기본 처리로 자동 제거.
    # 재질 20개(Sword 제외) 전부 이미 Emission→TEX_IMAGE(packed) 직결 — emission_texture로
    # 그대로.
    "초월_이재윤_AD": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_이재윤_AD.zip",
        source_type="blend",
        blend_member="source/Sketchfab_2023_11_04_19_44_13.blend",
        armature_name="Yuta Okkotsu Rig",
        path="Assets/Art/Units/초월_이재윤_AD/초월_이재윤_AD.fbx",
        mesh_name="Yuta",
        height=1.8,
        drop_meshes={"Sword"},
        rigid={},
        rename={
            "spine": "Hips", "spine.001": "Spine", "spine.002": "Spine1", "spine.003": "Spine2",
            "spine.004": "Neck", "spine.005": "Head",
            "shoulder.L": "LeftShoulder", "upper_arm.L": "LeftArm", "forearm.L": "LeftForeArm", "hand.L": "LeftHand",
            "shoulder.R": "RightShoulder", "upper_arm.R": "RightArm", "forearm.R": "RightForeArm", "hand.R": "RightHand",
            "thigh.L": "LeftUpLeg", "shin.L": "LeftLeg", "foot.L": "LeftFoot", "toe.L": "LeftToeBase",
            "thigh.R": "RightUpLeg", "shin.R": "RightLeg", "foot.R": "RightFoot", "toe.R": "RightToeBase",
        },
        fold={
            "spine.006": "Head", "pelvis.L": "Hips", "pelvis.R": "Hips",
            "heel.02.L": "LeftFoot", "heel.02.R": "RightFoot",
        },
        fold_subtree={"face": "Head"},
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        materials={n: ("emission_texture", None) for n in (
            "hair", "hair highlights", "hand skin", "ring",
            "head skin", "head skin shadow", "eye socket", "eyebrows", "eye color", "eye color2",
            "belt", "pants", "pants highlight", "pants shadow",
            "shirt", "shirt highlight", "button",
            "Shoes", "Shoes 2", "Shoes 3",
        )},
        rotate_z=0.0,
        level_arms=True,
        # SUBSURF(레벨2) 적용하면 원본 15,178삼각형이 약 16배로 불어난다(약 24만) — 목표
        # 3~5만으로 다시 감량. 여긴 bone heat가 아니라 원본 리그 가중치를 그대로 이어받는
        # 방식이라(자동가중치 아님) 0.3 밑으로 내려도 하나미·코라손의 "bone heat 죽음" 위험이
        # 없다.
        apply_subsurf=True,
        decimate_ratio=0.17,
        bake_size=2048,
    ),
    # 초월_두유찬_AD(게토 스구루/켄자쿠, Jujutsu Kaisen Sketchfab) — 유타와 같은 제작자
    # .blend(SOURCE.txt 참고). 아마추어 "Kenjaku Rig" 159뼈(별도 무기 리그 없음), 메시 5
    # (feet·hair·hands·head·robe) 전부 SUBSURF+SOLIDIFY+ARMATURE, 비-얼굴 뼈 25개가
    # 카스미·유타와 정확히 같은 이름 규칙(spine~spine.006 등) — rename/fold/fold_subtree
    # 유타 그대로 재사용.
    # 🔴 PM 사전조사의 "손이 머리 위(포즈)" 걱정 — 무재질 렌더로 REST 자세 직접 확인,
    # 이미 깨끗한 좌우 대칭 T자였다(다른 프레임에서 잰 수치로 추정, rest 자체는 정상이라
    # 별도 교정 불필요).
    "초월_두유찬_AD": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_두유찬_AD.zip",
        source_type="blend",
        blend_member="source/Sketchfab_2023_10_30_06_15_48.blend",
        armature_name="Kenjaku Rig",
        path="Assets/Art/Units/초월_두유찬_AD/초월_두유찬_AD.fbx",
        mesh_name="Getou",
        height=1.8,
        drop_meshes=set(),
        rigid={},
        rename={
            "spine": "Hips", "spine.001": "Spine", "spine.002": "Spine1", "spine.003": "Spine2",
            "spine.004": "Neck", "spine.005": "Head",
            "shoulder.L": "LeftShoulder", "upper_arm.L": "LeftArm", "forearm.L": "LeftForeArm", "hand.L": "LeftHand",
            "shoulder.R": "RightShoulder", "upper_arm.R": "RightArm", "forearm.R": "RightForeArm", "hand.R": "RightHand",
            "thigh.L": "LeftUpLeg", "shin.L": "LeftLeg", "foot.L": "LeftFoot", "toe.L": "LeftToeBase",
            "thigh.R": "RightUpLeg", "shin.R": "RightLeg", "foot.R": "RightFoot", "toe.R": "RightToeBase",
        },
        fold={
            "spine.006": "Head", "pelvis.L": "Hips", "pelvis.R": "Hips",
            "heel.02.L": "LeftFoot", "heel.02.R": "RightFoot",
            # 🔴 PM 사전조사엔 없던 뼈 2개(직접 확인) — upper_arm.R/L의 자식으로 소매 장식
            # (커프스 추정, "Bone"만 robe 메시에 실제 가중치 있음, "Bone.001"은 무가중치).
            "Bone": "RightArm", "Bone.001": "LeftArm",
        },
        fold_subtree={"face": "Head"},
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        # 🔴 실측 발견 — "spine.001"(→Spine)이 전 메시에서 가중치 0(직접 확인, robe도 spine·
        # spine.002·003만 쓰고 spine.001은 건너뜀) — 죠나단·가프의 Spine1 없음 패턴과 같은
        # 종류, 이번엔 Spine 자리. 자리표시로 spine.002 머리에 0-길이로 박아 둔다.
        bone_position_override={"Spine": ("spine.002", "head")},
        allow_dead_bones={"Spine"},
        # 🔴 실측 발견 — "Kenjaku Head Texture "(head skin 재질이 씀)가 소스 자체에서 누락
        # (packed=False·size=(0,0), 외부 OneDrive 경로만 남음) — 가장 톤이 비슷한 손 텍스처로
        # 대체(완벽히 같은 피부는 아니지만 텍스처 없음보다는 훨씬 낫다, PM 판단 필요하면 보고).
        image_substitute={"Kenjaku Head Texture ": "Kenjaku Hands Texture"},
        # PM 지시대로 ear rings·teeth·tougue 등 전부 유지, Outline만 기본값으로 자동 제거.
        materials={n: ("emission_texture", None) for n in (
            "head skin", "darker head skin", "Inside of mouth", "Inside of eye", "ear rings",
            "teeth", "tougue", "eyes", "eyelashes",
            "top clothes", "top clothes shadow", "white", "bronze", "darker bronze ", "green", "darker green", "shirt",
            "hand skin",
            "hair", "hair highlight",
            "bottom clothes", "bottom clothes shadow", "socks", "darker socks", "sole", "straps",
        )},
        rotate_z=0.0,
        level_arms=True,
        # 유타와 같은 이유 — SUBSURF 적용 시 삼각형이 크게 불어나 3~5만으로 재감량. 원본
        # 리그 가중치를 그대로 이어받는 방식(자동가중치 아님)이라 0.3 밑으로 내려도 안전.
        apply_subsurf=True,
        # 🔴 실측 발견 — SUBSURF 적용 후 458,496삼각형(유타보다 훨씬 큼, 메시 5개가 전부
        # 적용받아서로 추정) — 0.2로는 91,698(목표 3~5만 초과), 0.087 근처로 낮춰 4만대 목표.
        decimate_ratio=0.087,
        bake_size=2048,
    ),
    "희귀함_정내연": dict(
        # 사장님 모음집 zip 속에 스케치팹 원본 zip이 한 번 더 들어 있다(직접 확인) —
        # find_glb_and_extras가 .zip으로 끝나는 member를 자동으로 한 번 더 푼다.
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_정내연.zip",
        glb_member="source/nobara_kugisaki.zip",
        inner_gltf="scene.gltf",
        path="Assets/Art/Units/희귀함_정내연/희귀함_정내연.fbx",
        mesh_name="Nobara",
        height=1.8,                                               # PM 지시 — 흔함만 1.53m, 그 외 전 등급 1.8m 공통
        # Icosphere(조명 구, 42정점, 부모·아마추어 없음)가 원본 bbox를 [2.86,2.00,2.58]로
        # 부풀린 원흉(직접 확인) — 몸과 무관, 드롭 이후 자동 재중심·재스케일(10단계)이 처리한다.
        # 나머지 드롭 대상은 전부 직접 확인: Outline류(중복 셸, 원본 메시와 bbox가 거의 겹침)와
        # 못(Nail_147 밑 Object_61 — 무가중치·PM 지시로 버림) + 그 Outline(Object_62).
        drop_meshes={
            "Icosphere",
            "Object_10", "Object_13", "Object_23", "Object_28", "Object_36", "Object_41", "Object_47",  # Outline 셸(7곳)
            "Object_58", "Object_62",                             # OH_Outline_Material 셸(망치·못)
            "Object_61",                                          # 못(Nail_147) — PM 지시로 버림
            # 🔴 실측 발견 — PM은 "손에 쥘 크기면" 오른손 강체를 지시했지만, 크기가 아니라
            # 위치가 문제였다: Hammer_144 밑 Object_53~57 월드 중심이 hand.R 월드 위치에서
            # 1.15~1.28m 떨어져 있다(반대쪽 옆구리 바깥, 손 근처가 전혀 아님 — 직접 실측).
            # 강체로 오른손에 물리면 팔 각도가 바뀔 때마다 그 거리만큼 큰 반지름으로 휘둘려
            # 첫 시도에서 전체 폭이 2.939m로 튀었다(키 1.8m인데 팔 벌린 폭이 그보다 넓음).
            # PM 확인 전까지 실루엣 보존 원칙(강체가 실루엣을 왜곡·비정상적으로 매달리면 버림)
            # 대로 버려 둔다 — 재배치해서 살릴지는 PM 판단 대상으로 보고.
            "Object_53", "Object_54", "Object_55", "Object_56", "Object_57",
        },
        # 표준 체인 — 이 소스는 glTF 임포트가 이름 끝에 고유번호를 붙였다(직접 확인, 재수입해도
        # 같은 파일이면 번호도 같다). 나나미·카스미와 뼈 배치 자체는 리기파이 표준이라 동일.
        rename={
            "spine_164": "Hips", "spine.001_151": "Spine", "spine.002_150": "Spine1", "spine.003_143": "Spine2",
            "spine.004_94": "Neck", "spine.005_93": "Head",
            "shoulder.L_117": "LeftShoulder", "upper_arm.L_116": "LeftArm", "forearm.L_115": "LeftForeArm", "hand.L_114": "LeftHand",
            "shoulder.R_140": "RightShoulder", "upper_arm.R_139": "RightArm", "forearm.R_138": "RightForeArm", "hand.R_137": "RightHand",
            "thigh.L_158": "LeftUpLeg", "shin.L_157": "LeftLeg", "foot.L_156": "LeftFoot", "toe.L_154": "LeftToeBase",
            "thigh.R_163": "RightUpLeg", "shin.R_162": "RightLeg", "foot.R_161": "RightFoot", "toe.R_159": "RightToeBase",
        },
        fold={
            "spine.006_92": "Head", "breast.L_141": "Spine2", "breast.R_142": "Spine2",
            "pelvis.L_152": "Hips", "pelvis.R_153": "Hips", "heel.02.L_155": "LeftFoot", "heel.02.R_160": "RightFoot",
            "GLTF_created_0_rootJoint": "Hips",                    # spine 위에 있는 진짜 뼈대 뿌리(나나미·카스미엔 없던 한 마디)
        },
        # 얼굴 대형 서브트리(코·입술·턱·귀·눈썹·눈꺼풀·이마·볼·눈·이·혀, 직접 확인 90개 가까이) —
        # 뿌리 face_91 하나만 주면 build()가 자손 전부를 Head로 접는다(fold_subtree, 새 기능).
        fold_subtree={"face_91": "Head"},
        # 손가락(palm.01.L_98처럼 이름 끝에 번호가 붙어도 build()가 접미 숫자를 떼고 판정한다).
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        # 전부 Emission 전용(정점색 아님, 직접 확인: TEX_IMAGE 있는 건 Hair 하나뿐 — 나머지는
        # Emission 상수색을 그대로 단색 재질로). _shadow·_highlight는 표면거리 실측 결과(최소
        # 거리 0.0, 대부분 수mm 이내지만 완전히 겹치진 않음 — PM 판정기준 "본체에 구멍이 있고
        # 그 자리를 채우는 면") 전부 KEEP(버리면 그 자리에 구멍) — Emission 단색 재질로.
        materials={n: ("emission_texture", None) for n in (
            "Belt_Seconadary", "Belt_primary", "Eye_Color_Primary", "Eye_Color_Secondary",
            "Eyebrows_Primary", "Eyebrows_Secondary", "Hair", "Hair_highlights", "Hair_shadow", "Lips",
            "Shoe_Sole", "Shoes", "Shoes_highlight", "Skirt_Buttons",
            "Tights", "Tights_highlight", "Tights_shadow", "Top_Buttons", "eye_socket",
            "hand_skin", "head_skin", "head_skin_shadow", "shirt",
            "skirt_primary", "skirt_secondary", "skirt_shadow", "top_primary", "top_secondary",
        )},
        rotate_z=0.0,                                             # 나나미·카스미와 같은 계열(Sketchfab 리기파이) 추정,
                                                                    # --render로 실제 확인할 것
        level_arms=True,                                          # PM 실측: upper_arm.L z 1.23 vs hand.L z 1.036 = A자 약 28°
        decimate_ratio=0.08,                                      # 46.4만(Outline 절반 버리면 약 23만) → 3~4만대 목표
        bake_size=2048,
    ),
    "희귀함_강재규": dict(
        # ⚠️ 강재규 동명 5명 중 이건 희귀함_강재규(마히토, Sketchfab Jujutsu Kaisen) 하나뿐.
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_강재규.zip",
        source_type="blend",
        blend_member="source/Sketchfab_2023_10_30_13_18_35.blend",
        armature_name="Mahito Rig",
        path="Assets/Art/Units/희귀함_강재규/희귀함_강재규.fbx",
        mesh_name="Mahito",
        height=1.8,
        # 메시 5개뿐(body·hair·pants·shirt·shoes), 전부 스킨 있음(직접 확인) — 드롭·rigid 없음.
        # Outline은 재질 슬롯(OH_OUTLINE 솔리디파이가 즉석에서 만드는 셸, 실사용 면 0, 직접
        # 확인) — outline_material_names 기본값("Outline" 포함)이 자동으로 슬롯을 지운다.
        rename={
            "spine": "Hips", "spine.001": "Spine", "spine.002": "Spine1", "spine.003": "Spine2",
            "spine.004": "Neck", "spine.005": "Head",
            "shoulder.L": "LeftShoulder", "upper_arm.L": "LeftArm", "forearm.L": "LeftForeArm", "hand.L": "LeftHand",
            "shoulder.R": "RightShoulder", "upper_arm.R": "RightArm", "forearm.R": "RightForeArm", "hand.R": "RightHand",
            "thigh.L": "LeftUpLeg", "shin.L": "LeftLeg", "foot.L": "LeftFoot", "toe.L": "LeftToeBase",
            "thigh.R": "RightUpLeg", "shin.R": "RightLeg", "foot.R": "RightFoot", "toe.R": "RightToeBase",
        },
        fold={
            "spine.006": "Head", "pelvis.L": "Hips", "pelvis.R": "Hips",
            "heel.02.L": "LeftFoot", "heel.02.R": "RightFoot",
            # 머리카락 흔들뼈 3갈래(Bone·Bone.009·Bone.012 밑, 전부 spine.006 자식·hair 메시만
            # 물림, 직접 확인: z 1.49~1.88 — 머리~목덜미 높이) — Head로 접는다.
            "Bone": "Head", "Bone.002": "Head", "Bone.003": "Head",
            "Bone.009": "Head", "Bone.010": "Head", "Bone.011": "Head",
            "Bone.012": "Head", "Bone.013": "Head", "Bone.014": "Head",
        },
        fold_subtree={"face": "Head"},                            # 나나미·노바라와 같은 대형 얼굴 서브트리
        finger_prefixes=("palm.", "f_index.", "f_middle.", "f_ring.", "f_pinky.", "thumb."),
        # 전부 Emission→TEX_IMAGE 직결(정점색도, 텍스처 없는 재질도 없음 — Outline 빼고 27개
        # 재질 전부 직접 확인). emission_texture로 그대로 배선.
        materials={n: ("emission_texture", None) for n in (
            "skin", "darker skin", "white", "eyebrows", "eyelashes",
            "right eye inside", "left eye inside", "black eye color",
            "tats 1", "tats 1 shadow", "tats 2", "tats 2 shadow",
            "hair", "hair shadow", "hair band", "hair highlights",
            "pants primary", "pants highlight", "clothes shadow", "belt", "belt2", "belt3", "pants black",
            "shirt primary", "shirt highlight", "shirt alt",
            "shoes", "shoes 2", "shoes shadow",
        )},
        rotate_z=0.0,                                             # 같은 계열 추정, --render로 실제 확인할 것
        level_arms=True,                                          # 직접 확인: hand.L z 1.219 vs shoulder.L z 1.605 = A자(팔 아래로)
        decimate_ratio=0.35,                                      # 이미 저폴리(합쳐서 13,217정점) — 약한 감량만
        bake_size=2048,
    ),
}

# 22뼈 계층(gen_skin_rig.py와 완전히 같은 이름 규칙 — PREFIX만 공유, 그 파일은 안 건드린다)
SPINE = [("Hips", None), ("Spine", "Hips"), ("Spine1", "Spine"), ("Spine2", "Spine1"), ("Neck", "Spine2"), ("Head", "Neck")]
LIMB_NAMES = ["Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase"]


def find_glb_and_extras(cfg, workdir):
    """소스가 zip이면 풀어서 glb(또는 source_type="blend"면 .blend) 경로와 곁텍스처 경로를 돌려준다.
    🔴 노바라(희귀함_정내연) — 사장님 모음집 zip 안의 "source/" 항목 자체가 또 zip(스케치팹
    원본 다운로드 그대로, scene.gltf+scene.bin+textures/)이다. glb_member가 .zip으로 끝나면
    한 번 더 풀어 그 안의 gltf 파일(기본 "scene.gltf", inner_gltf로 바꿀 수 있다)을 가리킨다.
    텍스처는 그 zip 안에 이미 상대경로로 같이 들어 있어(scene.gltf가 "textures/..."를 상대로
    찾는다) 별도 extras 처리가 필요 없다."""
    src = os.path.expanduser(cfg["source"])
    if src.lower().endswith(".zip"):
        with zipfile.ZipFile(src) as z:
            z.extractall(workdir)
        member_key = "blend_member" if cfg.get("source_type") == "blend" else "glb_member"
        glb_path = os.path.join(workdir, cfg[member_key])
        if glb_path.lower().endswith(".zip"):
            inner_dir = os.path.join(workdir, "inner")
            with zipfile.ZipFile(glb_path) as z:
                z.extractall(inner_dir)
            # 🔴 산타나 — source_type="blend"인데 blend_member 자체가 zip(중첩)이면 안의 실제
            # 파일은 .blend다, gltf가 아니다 — inner_blend로 이름을 받는다(기본 inner_gltf는
            # blend가 아닐 때만).
            inner_name = cfg.get("inner_blend") if cfg.get("source_type") == "blend" else cfg.get("inner_gltf", "scene.gltf")
            glb_path = os.path.join(inner_dir, inner_name or "scene.gltf")
        extras = {}
        for key, member in cfg.items():
            if key.endswith("_member") and key != member_key:
                extras[key] = os.path.join(workdir, cfg[key])
        return glb_path, extras
    return src, {}


def build(name, cfg, out_dir=None, render_dir=None, workdir=None):
    report = {"이름": name}
    # 🔴 실측 발견(마히토) — workdir이 유닛 이름과 무관한 고정 경로 하나("/tmp/gen_rigify_
    # skin_work")를 공유하면, 그 밑 Textures 임시 폴더도 같이 재사용돼 이전에 빌드한 다른
    # 유닛의 이미지 파일이 안 지워진 채 남는다. 내보내기 12단계가 그 임시 폴더 안의 파일을
    # "전부" 최종 Textures로 복사하므로, 안 쓰는 다른 유닛 텍스처가 그대로 섞여 들어간다
    # (직접 확인: 나나미 폴더에 카스미 재질 파일 29개가 섞여 있었다 — FBX 자체는 자기 재질만
    # 참조해 렌더링엔 문제없지만 저장소에 안 쓰는 바이너리가 그대로 쌓인다). 유닛 이름별로
    # 임시 폴더를 나눈다.
    workdir = workdir or os.path.join("/tmp/gen_rigify_skin_work", safe_filename(name))
    os.makedirs(workdir, exist_ok=True)
    glb_path, extra_paths = find_glb_and_extras(cfg, workdir)
    report["원본"] = glb_path
    src_colors = gltf_material_colors(glb_path)          # 언릿 glTF의 emissiveFactor(위 함수 설명 참고)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    if cfg.get("source_type") == "blend":
        bpy.ops.wm.open_mainfile(filepath=glb_path)
        # 🔴 산타나 — "//상대경로" 이미지가 열 때는 has_data=True인데 이후 파이프라인 도중
        # image.save()가 "does not have any image data"로 죽었다(원인 불명, reload()도 안
        # 먹힘). 열자마자 픽셀을 메모리에 팩(pack)해 파일 참조 없이도 안전하게 만든다.
        for img in bpy.data.images:
            if img.source == "FILE" and not img.packed_file:
                try:
                    img.pack()
                except RuntimeError:
                    pass
        # 🔴 게토 — "Kenjaku Head Texture "(끝에 공백까지 원본 그대로)는 산타나와 달리
        # 애초에 packed=False·size=(0,0)·has_data=False로 소스 자체에 텍스처가 없다(외부
        # OneDrive 경로만 남아 있고 파일 자체가 .blend 안에 없음 — 진짜 누락, 위 pack()도
        # 파일이 없어 RuntimeError로 그냥 넘어간다). cfg["image_substitute"]로 그 재질
        # 노드가 쓰는 이미지를 다른(실존하는) 이미지로 갈아 끼운다.
        for broken, sub in cfg.get("image_substitute", {}).items():
            sub_img = bpy.data.images.get(sub)
            if sub_img is None:
                continue
            for mat in bpy.data.materials:
                if not mat.node_tree:
                    continue
                for node in mat.node_tree.nodes:
                    if node.type == "TEX_IMAGE" and node.image and node.image.name == broken:
                        node.image = sub_img
    else:
        bpy.ops.import_scene.gltf(filepath=glb_path)
    scene = bpy.context.scene

    # 무기 소품 전용 1뼈짜리 rig가 따로 있는 소스(카스미: Blade Rig·Cabbard Rig·Sword Rig)는
    # next()로 아무 ARMATURE나 집으면 안 된다 — armature_name이 있으면 그걸로 직접 찾는다.
    arm_obj = bpy.data.objects[cfg["armature_name"]] if cfg.get("armature_name") else next(
        o for o in scene.objects if o.type == "ARMATURE")
    all_meshes = [o for o in scene.objects if o.type == "MESH"]
    keep = [o for o in all_meshes if o.name not in cfg.get("drop_meshes", set())]
    report["뺀 메시"] = sorted(cfg.get("drop_meshes", set()))
    report["쓴 메시"] = sorted(o.name for o in keep)
    # 🔴 실측 발견(카스미) — 뺀 메시를 그냥 select_set(False)만 해서 최종 export에서 빼는
    # 방식으로는 부족하다. FBX 내보내기가 use_selection=True라도, 리기파이 뼈의 custom_shape
    # 같은 걸로 어딘가 참조된 오브젝트(예: 위젯용 기본 정육면체 "Cube")는 선택 안 해도 같이
    # 딸려 나온다(직접 실측: Cube를 keep에서 뺀 뒤에도 export된 FBX를 재확인하니 그대로 들어
    # 있었다). 아예 씬에서 지워버려야 확실히 안 나간다.
    for o in list(all_meshes):
        if o not in keep:
            bpy.data.objects.remove(o, do_unlink=True)

    # OH_Outline_Material 슬롯 제거 — 그 재질을 실제로 쓰는 면은 원본 메시에 0개다(OH_OUTLINE은
    # SOLIDIFY 모디파이어가 즉석에서 만드는 셸 전용 재질, 직접 확인). 슬롯만 지워 안 쓰는
    # 복잡한 셰이더 그래프가 최종 FBX에 딸려 나가는 걸 막는다.
    # 🔴 마히토 — 같은 함정인데 재질 이름이 다르다("Outline", OH_OUTLINE 솔리디파이 모디파이어가
    # 즉석에서 만드는 셸 — 원본 .data엔 이 슬롯을 쓰는 면이 0개, 직접 확인). 소스마다 이름이
    # 다를 수 있어 cfg["outline_material_names"]로 목록을 받고, 기본값에 지금까지 나온 이름을
    # 다 넣어 둔다.
    outline_names = cfg.get("outline_material_names", {"OH_Outline_Material", "Outline"})
    outline_leftover = 0
    for o in keep:
        for i in reversed(range(len(o.data.materials))):
            mat = o.data.materials[i]
            if mat and mat.name in outline_names:
                used = sum(1 for p in o.data.polygons if p.material_index == i)
                if used:
                    outline_leftover += used
                else:
                    o.data.materials.pop(index=i)
    report["Outline류 실사용 면(있으면 안 됨)"] = outline_leftover

    # 🔴 유타 — PM 지시: SUBSURF는 모양을 살리려고 적용하되(원본 저해상도 그대로면 각져
    # 보임) SOLIDIFY(OH_Outline 셸 전용, 이미 위에서 빈 슬롯만 지웠지 모디파이어 자체는 아직
    # 붙어 있다)는 적용하지 말고 지운다. cfg["apply_subsurf"]=True인 유닛만.
    if cfg.get("apply_subsurf"):
        for o in keep:
            for m in list(o.modifiers):
                if m.type == "SOLIDIFY":
                    o.modifiers.remove(m)
            ss = next((m for m in o.modifiers if m.type == "SUBSURF"), None)
            if ss:
                bpy.context.view_layer.objects.active = o
                bpy.ops.object.modifier_apply(modifier=ss.name)

    # ── 1) 스킨 없는 조각(rigid) — 뼈 하나에 100% 묶을 그룹을 지금 만들어 둔다(join 전에 해야
    #    이름으로 합쳐진다). 부모가 armature가 아니라 빈 오브젝트인 것도 있어 world 행렬로 굽는다.
    for mesh_name, bone_rigify in cfg.get("rigid", {}).items():
        o = bpy.data.objects[mesh_name]
        vg = o.vertex_groups.new(name=bone_rigify)             # 임시로 리기파이 쪽 이름 대신 최종 mixamorig 이름 직행
        vg.add(range(len(o.data.vertices)), 1.0, "REPLACE")
        if not any(m.type == "ARMATURE" for m in o.modifiers):
            mod = o.modifiers.new("Armature", "ARMATURE")
            mod.object = arm_obj

    # ── 2) UV 층을 첫 장만 남기고 "UVMap"으로 통일(히소카 사고 재발 방지 — join 전에 반드시).
    for o in keep:
        uvs = o.data.uv_layers
        while len(uvs) > 1:
            uvs.remove(uvs[-1])
        if len(uvs) == 1:
            uvs[0].name = "UVMap"

    # 🔴 실측 발견(히소카와 다른 새 함정) — join 이전, 원본 그대로인 개별 메시조차 UV0가
    # 이미 대량 퇴화돼 있다(Nanami 몸통 32.5만 loop 중 88.1%가 (0,0), Eyebrows는 100%, Watch
    # 11.1% — 정점색으로만 색을 넣고 텍스처를 안 쓴 원본이라 UV를 안 챙긴 것으로 보인다).
    # 이 상태로 구우면 거의 모든 면이 같은 UV점에 겹쳐 굽혀 결과가 무늬 없이 새하얗게/단색으로
    # 나온다(직접 실측: 구운 이미지 표준편차 0.0, 전부 정확히 같은 값). 정점색을 구울 조각만
    # Smart UV Project로 새로 펴서 이 문제를 근본적으로 없앤다. Tie는 zip이 준 실제 텍스처
    # (TieLeopardPattern.png)에 맞춘 원본 UV가 멀쩡하므로(퇴화 0%) 절대 다시 펴면 안 된다.
    mat_plan_names = cfg["materials"]
    for o in keep:
        needs_reunwrap = any(
            slot.material and mat_plan_names.get(slot.material.name.split(".")[0], (None,))[0] == "vertex_color"
            for slot in o.material_slots
        )
        if needs_reunwrap:
            bpy.context.view_layer.objects.active = o
            for oo in scene.objects:
                oo.select_set(oo is o)
            bpy.ops.object.mode_set(mode="EDIT")
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.smart_project(angle_limit=66.0, island_margin=0.06)
            bpy.ops.object.mode_set(mode="OBJECT")

    # ── 3) 재질별 굽기 준비 — 같은 재질을 쓰는 조각이 여럿이면(정점색 방식만) 조각마다 단일
    #    사용자 복제를 만든다. texture_file·solid는 원래 하나로 공유해도 안전(전 조각이 같은
    #    그림/같은 색이라야 맞으므로 그대로 공유).
    mat_plan = cfg["materials"]
    shared_vc_mats = {mn for mn, (kind, _) in mat_plan.items() if kind == "vertex_color"}
    for o in keep:
        for slot in o.material_slots:
            m = slot.material
            if m is None:
                continue
            base = m.name.split(".")[0]
            if base in shared_vc_mats:
                m2 = m.copy()
                m2.name = f"{base}__{o.name}"
                slot.material = m2

    # ── 4) 정점색 → 이미지 굽기(조각별). 재질 이름 → 새 이미지 경로.
    # 🔴 실측 발견 — bpy.ops.object.bake()는 오브젝트 하나를 대상으로 "그 오브젝트의 모든 재질
    # 슬롯"을 한 번에 굽는다. 재질별로 따로따로 bake()를 호출하면(먼저 처리한 재질만 그래프가
    # 준비돼 있고 아직 안 건드린 나머지 재질은 원본 그래프인 채로 같이 구워져) 나머지 재질이
    # "No active and selected image texture node found"로 새까맣게 구워진다(직접 실측: Belt·
    # Goggles·Watch처럼 재질이 2개 이상인 조각에서만 재현) — 오브젝트당 모든 재질의 굽기용
    # 그래프+이미지를 먼저 다 준비한 뒤 bake()를 오브젝트당 딱 한 번만 부른다.
    tex_dir_tmp = os.path.join(workdir, "Textures")
    os.makedirs(tex_dir_tmp, exist_ok=True)
    # 🔴 실측 발견(죠나단) — macOS는 대소문자 구분이 없어 zip을 푼 "textures/"와 이 굽기용
    # "Textures/"가 실제로 같은 물리 폴더일 수 있다(source_type이 zip을 workdir에 통째로
    # 풀 때 이미 벌어진다). 그러면 원본 zip이 곁다리로 준 미사용 텍스처(예: body1_0.png)까지
    # 이 폴더에 섞여 있다가, 마지막 단계가 "폴더 안 전부"를 최종 산출물로 복사해 안 쓰는
    # 원본 파일까지 같이 나갔다(직접 확인: 나중에 PM 유니티 확인으로 발견). os.listdir로 다
    # 쓸어담지 않고, 이 함수가 실제로 만든 파일 이름만 기억해 그것만 최종 복사한다.
    written_textures = set()
    baked_images = {}          # 재질명 -> image
    verify_report = {}
    for o in keep:
        prepared = []          # (재질, 이미지) — 이 오브젝트에서 이번에 준비한 것들
        for slot in o.material_slots:
            m = slot.material
            if m is None:
                continue
            base = m.name.split("__")[0]
            kind, arg = mat_plan.get(base, (None, None))
            if kind == "vertex_color" and m.name not in baked_images:
                img = prepare_vertex_color_bake(o, m, cfg["bake_size"], tex_dir_tmp)
                baked_images[m.name] = img
                prepared.append((m, img))
        if prepared:
            bake_object_once(o)
            for m, img in prepared:
                verify_report[m.name] = verify_vertex_color_bake(o, m, img, cfg["bake_size"])
                path = os.path.join(tex_dir_tmp, safe_filename(m.name) + ".png")
                img.filepath_raw = path
                img.file_format = "PNG"
                img.save()
                written_textures.add(os.path.basename(path))
                wire_image_material(m, img)

    # ── 5) solid/texture_file 재질도 이미지로(공유 1장이면 충분).
    for mat_name, (kind, arg) in mat_plan.items():
        m = bpy.data.materials.get(mat_name)
        if m is None or kind == "vertex_color":
            continue
        if kind == "solid":
            sp = os.path.join(tex_dir_tmp, safe_filename(mat_name) + ".png")
            img = solid_png(sp, arg, 64)
            written_textures.add(os.path.basename(sp))
            wire_image_material(m, img)
        elif kind == "emission_texture":
            # 이미 Emission -> TEX_IMAGE로 색이 물려 있는 재질(카스미) — 굽기 없이 그 이미지를
            # 그대로 Base Color로 재배선. 이미지가 없는 재질(정점색도 텍스처도 없는 경우)은
            # Emission 기본색으로 무난한 단색을 만든다.
            tex_node = next((n for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image), None)
            if tex_node:
                img = tex_node.image
                # 🔴 산타나 — open_mainfile 직후엔 has_data=True였는데 이후 파이프라인 단계를
                # 거치면 image.save()가 "does not have any image data"로 죽는 경우가 있었다
                # (원인 불명, 직접 겪음). 저장 전에 강제로 다시 읽어 안전하게 만든다.
                if not img.has_data:
                    img.reload()
                max_size = cfg.get("max_texture_size")
                if max_size and max(img.size) > max_size:
                    img.scale(max_size, max_size)
                wire_image_material(m, img)
                path = os.path.join(tex_dir_tmp, safe_filename(mat_name) + ".png")
                img.filepath_raw = path
                img.file_format = "PNG"
                img.save()
                written_textures.add(os.path.basename(path))
            else:
                emit = next((n for n in m.node_tree.nodes if n.type == "EMISSION"), None)
                color = tuple(emit.inputs["Color"].default_value) if emit else (0.6, 0.6, 0.6, 1.0)
                if max(color[:3]) <= 1e-4:                         # 🔴 위 gltf_material_colors 참고 — 임포터가 버린 emissiveFactor를 원본에서 되찾는다
                    color = src_colors.get(mat_name, color)
                assert max(color[:3]) > 1e-4, \
                    f"{name}: 재질 {mat_name}의 단색이 새까맣다 — 원본에서 색을 못 찾았다(게임에서 검게 보인다)"
                sp = os.path.join(tex_dir_tmp, safe_filename(mat_name) + ".png")
                img = solid_png(sp, color, 64)
                written_textures.add(os.path.basename(sp))
                wire_image_material(m, img)
                report.setdefault("원본 색으로 채운 단색 재질", {})[mat_name] = [round(c, 3) for c in color[:3]]
        elif kind == "texture_file":
            src_path = extra_paths[arg]
            dst_path = os.path.join(tex_dir_tmp, os.path.basename(src_path))
            # 🔴 macOS 기본 파일시스템은 대소문자 구분이 없다 — zip을 푼 "textures"와 굽기용
            # tex_dir_tmp("Textures")가 실제로 같은 폴더일 수 있어, src와 dst가 같은 파일이면
            # wb로 여는 순간 read 전에 0바이트로 잘린다(실제로 겪음). 같으면 복사를 건너뛴다.
            same = os.path.exists(dst_path) and os.path.samefile(src_path, dst_path)
            if not same:
                with open(src_path, "rb") as f:
                    data = f.read()
                with open(dst_path, "wb") as g:
                    g.write(data)
            written_textures.add(os.path.basename(dst_path))
            img = bpy.data.images.load(dst_path, check_existing=True)
            wire_image_material(m, img)

    report["정점색 굽기 검증(면 중심 샘플)"] = verify_report

    # ── 6) 모든 조각을 세계 좌표로 굽고 하나로 합친다(부모가 armature든 빈 오브젝트든 상관없이
    #    world 행렬을 정점에 직접 구우므로 계층이 뒤섞여도 안전 — gen_skin_rig.py와 같은 방식).
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

    # ── 7) 뼈 접기·개명 — 편집모드에서 이름을 바꾸고 여분 뼈를 지우기 전에, 정점 그룹 쪽은
    #    "가중치 합치기"가 필요하다(같은 이름 그룹이 여러 개면 add가 최댓값이 아니라 마지막
    #    값으로 덮으므로, 여기서는 새 이름 그룹을 만들고 옛 그룹들의 가중치를 더해 넣는다).
    rename = dict(cfg["rename"])
    fold = dict(cfg["fold"])
    finger_prefixes = cfg.get("finger_prefixes", ())

    # 🔴 노바라 — fold_subtree={"face_91": "Head"}처럼 뿌리 뼈 이름만 주면 그 자손 전부(코·입술·
    # 턱·귀·눈썹·눈꺼풀·이마·볼·눈·이·혀 등 대형 얼굴 서브트리, 이 유닛은 90개 가까이)를 한 번에
    # Head로 접는다 — 하나하나 손으로 나열하면 개수도 많고 glTF 임포트가 이름 끝에 붙이는 고유
    # 번호(spine_164처럼 "_숫자")가 재수입 때 달라질 수 있어 오타/불일치 위험이 크다.
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
        # 🔴 노바라 — glTF 임포트가 이름 충돌 방지로 끝에 "_숫자"를 붙여(예: "palm.01.L_98")
        # 손가락 접두/접미 판정이 그대로는 안 걸린다(Nanami·Kasumi는 이 충돌이 없어 원래도
        # 문제없었다). 접미 숫자만 떼고 판정 — Rigify 이름 자체엔 "_숫자" 꼬리가 없으니 안전하다.
        stripped = re.sub(r"_\d+$", "", old_name)
        for side, suf in (("L", ".L"), ("R", ".R")):
            for pre in finger_prefixes:
                if stripped.startswith(pre) and stripped.endswith(suf):
                    return ("Left" if side == "L" else "Right") + "Hand"
        return None

    old_names = [b.name for b in arm_obj.data.bones]
    mapping = {}
    unmapped = []
    for n in old_names:
        if n == "neutral_bone":
            mapping[n] = target_mixamorig(n)
            continue
        t = target_mixamorig(n)
        if t is None:
            unmapped.append(n)
        else:
            mapping[n] = t
    report["매핑 안 된 뼈"] = unmapped
    assert not unmapped, f"{name}: 매핑 못 한 뼈 {unmapped}"

    # 최종 이름별로 옛 그룹들을 합친다.
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
    # 🔴 실측 발견(카스미 Cabbard→Spine) — rigid 루프(위 1번)는 리기파이 옛 이름이 아니라
    # 최종 이름(예: "Spine")으로 그룹을 직행 생성한다. 그런데 위 루프는 mapping의 옛 이름으로만
    # 그룹을 찾으므로 그런 그룹은 절대 못 찾는다. "Head"처럼 fold/rename으로 같은 최종 이름에
    # 도달하는 다른 옛 그룹이 우연히 있으면 가려져 안 보이고(뼈별 정점 수가 0이 아니게 나옴),
    # 그런 옛 그룹이 하나도 없는 이름(Spine)은 통째로 가중치가 증발해 정점이 무가중치가 된다.
    # 최종 이름과 똑같은 그룹은 자기 자신으로 통과시켜 이 구멍을 막는다.
    for t in targets:
        vg = body.vertex_groups.get(t)
        if vg and vg.index not in old_index_to_target:
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

    # 🔴 실측 발견(카스미 "top" 메시 단추/옷깃 62곳) — Cabbard처럼 우리가 rigid로 의도한 소품이
    # 아니라, 소스 파일 자체의 자동 가중치가 애초에 비어 있던 순정점(뼈 그룹이 하나도 없고
    # OH_Outline_VertexGroup만 있음)이 나온다. gen_skin_rig.py의 본 히트 실패 대체 기법과 같은
    # 원칙으로, 가장 가까운 "이미 가중치가 있는" 정점의 분포를 그대로 복사해 안전망을 둔다.
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

    # 옛 그룹(리기파이 이름 전부 + rigid로 이미 최종 이름을 써버린 것도 정리) 지우고 새 이름만 남긴다.
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
    # 🔴 임채민(죠나단) — 척추가 2마디(bip_spine_0·bip_spine_1)뿐이라 mixamorig 6마디
    # (Hips·Spine·Spine1·Spine2·Neck·Head)에 못 채운다. Spine1은 자리표시(bip_spine_1 끝점의
    # 0-길이 스텁, 어깨가 갈라지는 진짜 지점 바로 앞)로 두고 실제 가중치는 없다 — 정상.
    # cfg["allow_dead_bones"]로 이 유닛의 알려진 예외만 봐준다(다른 유닛엔 영향 없음).
    allow_dead = cfg.get("allow_dead_bones", set())
    dead = [k for k, c in counts.items() if c == 0 and k not in allow_dead]
    report["뼈별 정점(w>0.01)"] = counts
    assert not dead, f"{name}: 가중치 없는 뼈(접은 뒤) {dead}"

    # ── 8) 뼈대 자체를 22개로 다시 짓는다(편집모드에서 옛 뼈를 지우고 이름 바꾸는 것보다,
    #    깨끗하게 새 아마추어를 짓고 위치만 옮기는 편이 계층 사고를 줄인다).
    # 🔴 산타나 — 소스 아마추어 오브젝트 자체에 배율이 걸려 있을 수 있다(직접 확인: scale
    # 1.0396). head_local/tail_local은 그 변환 적용 전 로컬 좌표라, 이미 matrix_world를
    # 적용해 세계 좌표로 구운 메시와 안 맞을 수 있어 곱해서 맞춘다(히나타 때 gen_biped_skin.
    # py에 넣은 것과 같은 일반 수정).
    Mw_arm = arm_obj.matrix_world
    old_head_tail = {b.name: (Mw_arm @ Vector(b.head_local), Mw_arm @ Vector(b.tail_local)) for b in arm_obj.data.bones}
    new_arm_data = bpy.data.armatures.new("Armature")
    new_arm_obj = bpy.data.objects.new("Armature", new_arm_data)
    scene.collection.objects.link(new_arm_obj)
    bpy.context.view_layer.objects.active = new_arm_obj
    bpy.ops.object.mode_set(mode="EDIT")
    # 관절 위치 표: 리기파이 원본 뼈의 head/tail을 그대로 쓰되, 접힌 뼈(spine.006 등)의 원래
    # 이름은 더 없으니 표준 체인 이름(spine.005 등)에서 그대로 가져온다.
    src_bone_of = {v: k for k, v in cfg["rename"].items()}
    # 🔴 임채민(죠나단) — 척추 마디가 mixamorig보다 적은 소스용: cfg["bone_position_override"]에
    # {target: (source_bone, "head"|"tail")}를 주면 그 점 하나로 head=tail 0-길이 자리표시
    # 뼈를 짓는다(위 allow_dead_bones와 짝 — 그 target은 가중치가 없는 게 정상이다).
    pos_override = cfg.get("bone_position_override", {})
    eb_by_name = {}
    for tname, _ in SPINE:
        if tname in pos_override:
            src, end = pos_override[tname]
            h = t = old_head_tail[src][0 if end == "head" else 1]
        else:
            src = src_bone_of[tname]
            h, t = old_head_tail[src]
        eb = new_arm_data.edit_bones.new(PREFIX + tname)
        eb.head, eb.tail = h, t
        eb_by_name[tname] = eb
    for tname in eb_by_name:
        parent = dict(SPINE)[tname]
        if parent:
            eb_by_name[tname].parent = eb_by_name[parent]
    for side, mirror_key in (("Left", ".L"), ("Right", ".R")):
        for limb in LIMB_NAMES:
            tname = side + limb
            # 🔴 산타나 — 발가락 뼈가 아예 없는 소스(thigh→shin→foot뿐)용: SPINE과 같은
            # bone_position_override를 LIMB 쪽에도 적용(전엔 SPINE 루프에만 있었다 — 일반화).
            if tname in pos_override:
                src, end = pos_override[tname]
                h = t = old_head_tail[src][0 if end == "head" else 1]
            else:
                src = src_bone_of[tname]
                h, t = old_head_tail[src]
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

    # 새 아마추어로 다시 부모/모디파이어 연결(정점 그룹 이름은 이미 mixamorig 접두 없이 맞춰
    # 놨으니, 그룹 이름 앞에 PREFIX를 붙여 새 뼈 이름과 맞춘다).
    for vg in body.vertex_groups:
        vg.name = PREFIX + vg.name
    for m in list(body.modifiers):
        body.modifiers.remove(m)
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = new_arm_obj
    body.parent = new_arm_obj
    # 옛 아마추어·그 메시(이제 안 씀)는 지운다.
    bpy.data.objects.remove(arm_obj, do_unlink=True)
    arm_obj = new_arm_obj

    # ── 9) 팔 수평 굽기(level_arms) — gen_skin_rig.py와 같은 기법: 포즈모드에서 회전 적용 뒤
    #    그 자세를 새 쉬는 자세로 굽는다.
    if cfg.get("level_arms"):
        report["팔 수평 굽기(°)"] = level_arms(arm_obj, body)

    # ── 10) 방향·크기. 정면은 이미 −Y(원본 확인) — rotate_z만 옵션으로 남겨 둔다.
    Rz = Matrix.Rotation(math.radians(cfg.get("rotate_z", 0.0)), 4, "Z")
    if cfg.get("rotate_z", 0.0) != 0.0:
        for o in (body, arm_obj):
            o.data.transform(Rz) if o.type == "MESH" else None
        # 뼈대 회전은 편집모드에서(간단히 재구성 시점에 반영하는 게 더 안전하지만, 이 유닛은
        # rotate_z=0이라 실행되지 않는다 — 다음 유닛에서 필요해지면 여기부터 확장할 것).

    bpy.context.view_layer.update()
    V = np.array([v.co for v in body.data.vertices])
    lo, hi = V.min(0), V.max(0)
    H = float(hi[2] - lo[2])
    s = cfg["height"] / H
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))
    for o in (body, arm_obj):
        o.data.transform(G) if o.type == "MESH" else None
    # 뼈대(에딧본)도 같은 G로 — 새로 지은 뼈라 rest만 옮기면 된다.
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

    # ── 11) 감량 — 🔴 이 Blender(5.2.1)엔 bmesh.ops.decimate_collapse가 없다(gen_skin_rig.py도
    # 같은 이유로 표준 Decimate 모디파이어 COLLAPSE를 쓴다 — UV 경계를 정확히 보존하진 않지만
    # 안정적으로 동작). 감량 전 정점 그룹·UV·정점색 전부 preserve_all_data_layers로 지킨다.
    tri_before = sum(len(p.vertices) - 2 for p in body.data.polygons)
    mod = body.modifiers.new("decimate", "DECIMATE")
    mod.ratio = cfg["decimate_ratio"]
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    new_mesh = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    body.modifiers.remove(mod)
    old_mesh = body.data
    body.data = new_mesh
    new_mesh.name = old_mesh.name
    tri_after = sum(len(p.vertices) - 2 for p in body.data.polygons)
    report["감량"] = {"전": tri_before, "후": tri_after}

    # ── 12) 내보내기.
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    os.makedirs(tex_dir, exist_ok=True)
    for fn in written_textures:
        with open(os.path.join(tex_dir_tmp, fn), "rb") as f, open(os.path.join(tex_dir, fn), "wb") as g:
            g.write(f.read())
    # 재질 이미지 노드가 임시 경로를 보고 있던 걸 최종 경로로 다시 잇는다.
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        for node in m.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image and node.image.filepath:
                fn = os.path.basename(node.image.filepath)
                final_path = os.path.join(tex_dir, fn)
                if os.path.exists(final_path):
                    node.image.filepath = final_path
                    node.image.reload()

    # 🔴 면 정리 + 삼각형화(2026-09-23 blender, 사장님이 희귀함_배현진 팔이 게임에서 종잇장처럼 눌려 보인다고 지적 → 원인):
    #   유니티 임포트 경고 192건 "A polygon of Mesh 'Kasumi' ... is self-intersecting and has been discarded" —
    #   유니티는 스스로 교차하는 다각형을 **버린다**. 즉 게임 안에서 그 면들이 실제로 없어져 팔·어깨가 뚫려 보였다.
    #   원본(리기파이 .blend)은 사각형이 6,576개인데, 그중 심하게 휜 것(면 크기 대비 평면 이탈 0.25 초과)이 19개
    #   있었다(최대 0.694) — 유니티가 제 방식으로 삼각형화하면서 자기교차로 판정한 것들이다.
    #   → ① 겹친 정점 합치기 ② 면적 0 면 없애기 ③ **전부 삼각형으로** 나눠 내보낸다. 어차피 유니티는 삼각형으로
    #      바꿔 쓰므로 겉모습은 안 변하고, 나누는 주체가 유니티가 아니라 우리가 되어 버려지는 면이 없어진다.
    tri_before = len(body.data.polygons)
    bm = bmesh.new()
    bm.from_mesh(body.data)
    v0 = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    gone = [f for f in bm.faces if f.calc_area() < 1e-10]
    if gone:
        bmesh.ops.delete(bm, geom=gone, context="FACES")
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    report["면 정리"] = {"정점": [v0, len(body.data.vertices)], "면적0 면 지움": len(gone),
                      "면": [tri_before, len(body.data.polygons)], "전부 삼각형": all(len(p.vertices) == 3 for p in body.data.polygons)}

    # UV0 퇴화 검사(히소카 사고) — 면적이 사실상 0이면 실패로 본다.
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
    # 🔴 PM 지적(2026-09-17) — path_mode="COPY"는 embed_textures=False라도 유니티가 FBX 안에서
    # 텍스처 데이터를 찾아내 <유닛>.fbm/ 폴더로 뽑아낸다(Textures/와 이중으로 용량을 먹는다).
    # gen_skin_rig.py가 이미 쓰던 path_mode="STRIP"(경로 참조를 아예 안 남김)로 맞춘다.
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    return report


def level_arms(arm, body):
    scene = bpy.context.scene
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    turned = {}
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        for bone in ("Arm", "ForeArm", "Hand"):
            pb = arm.pose.bones[PREFIX + side + bone]
            head, tail = pb.matrix.to_translation(), pb.tail.copy()
            d = (tail - head).normalized()
            turned[side + bone] = round(math.degrees(d.angle(Vector((sx, 0.0, 0.0)))), 1)
            q = d.rotation_difference(Vector((sx, 0.0, 0.0)))
            pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
            bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = body.data
    body.data = baked
    baked.name = old.name
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return turned


def prepare_vertex_color_bake(obj, mat, size, out_dir):
    """정점색(Color 애트리뷰트) → Emission 그래프 + 굽기용 빈 이미지를 만들어 재질에 물린다
    (아직 굽지 않는다 — bake_object_once가 오브젝트당 한 번만 실제로 굽는다)."""
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
            nt.nodes.remove(n)
    next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = obj.data.color_attributes[0].name if obj.data.color_attributes else "Color"
    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(vc.outputs["Color"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    img = bpy.data.images.new(safe_filename(mat.name), size, size)
    # 🔴 실측 확인(처음엔 버그로 오인 — 정정) — 기본 sRGB 색공간으로 구우면 저장된 픽셀 값이
    # 정점색의 감마 인코딩(baked ≈ srgb_encode(정점색), 1.055·x^(1/2.4)−0.055)이 된다. 이건
    # 버그가 아니라 의도대로다: glTF COLOR_0는 선형 공간이 규격이고(스펙 확인), Blender의
    # BYTE_COLOR 애트리뷰트는 .color로 읽으면 항상 선형으로 자동 디코드해 준다 — 반대로 유니티가
    # Base Color 텍스처를 sRGB로 임포트해 매 샘플마다 선형으로 디코드하므로, 파일에는 감마
    # 인코딩된 값을 넣어야 유니티에서 다시 원래 선형(=정점색)으로 돌아온다. sRGB 색공간을
    # 그대로 두는 게 맞다 — verify_vertex_color_bake가 이 관계를 반영해서 비교한다.
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.nodes.active = tex
    tex.select = True
    return img


def bake_object_once(obj):
    """obj의 모든(이미 준비된) 재질 슬롯을 한 번에 굽는다 — 재질별로 따로 부르면 아직 준비 안 된
    형제 재질이 새까맣게 구워지는 실측 버그(Belt·Goggles·Watch)가 생겨 오브젝트당 한 번만 부른다."""
    scene = bpy.context.scene
    saved = (scene.render.engine, scene.cycles.samples if hasattr(scene, "cycles") else None)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    for o in scene.objects:
        o.select_set(o is obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="EMIT", margin=2)
    scene.render.engine = saved[0]
    if saved[1] is not None:
        scene.cycles.samples = saved[1]


def srgb_encode(c):
    """선형(0~1) → sRGB 감마 인코딩. img.pixels가(색공간이 sRGB인 이미지에서) 이미 인코딩된
    값을 돌려주므로, 정점색(선형)과 비교하려면 이쪽을 감마로 올려야 한다."""
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055


def verify_vertex_color_bake(obj, mat, img, size):
    """면 몇 개를 골라 (면 중심 UV로 이미지 샘플) vs (그 면 정점색 평균을 sRGB로 올린 값)을
    비교해 숫자로 남긴다(회당 사고 재발 방지 — PM 지시).
    🔴 실측 발견 1 — obj.data.polygons를 무조건 등간격으로 훑으면, 오브젝트에 재질이 여러 개일 때
    (Belt·Goggles·Watch·Weapon처럼) 이 재질 슬롯을 안 쓰는 면(다른 재질 얼굴)을 표본으로 뽑아
    "최대차"가 크게 잘못 찍힌다(실측: Metal__Belt를 검사하면서 Cycles Toon Shader__Belt 얼굴을
    같이 훑어 0.2~0.5대 가짜 오차가 나왔다). material_index로 이 재질의 슬롯에 속한 면만 걸러야
    한다.
    🔴 실측 발견 2(처음엔 버그로 오인) — 위 필터링 뒤에도 오차가 여전히 0.2~0.5대로 남아
    "굽기 자체가 틀렸다"고 봤는데, 숫자를 맞춰 보니 baked ≈ 1.055·vcol^(1/2.4)−0.055(sRGB
    인코딩)로 1% 안에서 정확히 들어맞았다 — 버그가 아니라 의도한 동작이었다(prepare_vertex_
    color_bake 주석 참고: glTF COLOR_0는 선형, 유니티 sRGB 텍스처 임포트는 그 반대 방향으로
    디코드하므로 파일엔 감마 인코딩된 값이 들어가야 유니티에서 원래 색이 나온다). 정점색을
    그대로 비교하면 항상 어긋나 보이므로, 비교 직전에 sRGB로 올려서 맞춘다."""
    slot_index = next(i for i, sl in enumerate(obj.material_slots) if sl.material == mat)
    layer_name = next(n.layer_name for n in mat.node_tree.nodes if n.type == "VERTEX_COLOR")
    color_layer = obj.data.color_attributes[layer_name] if layer_name in obj.data.color_attributes else obj.data.color_attributes[0]
    pixels = np.array(img.pixels[:]).reshape(size, size, 4)
    uv_layer = obj.data.uv_layers.active.data
    samples = []
    own_faces = [p for p in obj.data.polygons if p.material_index == slot_index]
    step = max(1, len(own_faces) // 6)
    for i in range(0, len(own_faces), step):
        p = own_faces[i]
        us = [uv_layer[li].uv[0] for li in p.loop_indices]
        vs = [uv_layer[li].uv[1] for li in p.loop_indices]
        cu, cv = sum(us) / len(us), sum(vs) / len(vs)
        px, py = int(cu * size) % size, int(cv * size) % size
        baked_col = pixels[py, px][:3]
        vcols = [tuple(color_layer.data[li].color)[:3] for li in p.loop_indices]
        vcol_avg = tuple(sum(c[k] for c in vcols) / len(vcols) for k in range(3))
        vcol_avg_srgb = tuple(srgb_encode(c) for c in vcol_avg)
        diff = max(abs(baked_col[k] - vcol_avg_srgb[k]) for k in range(3))
        samples.append({"면": p.index, "구운색": [round(float(c), 3) for c in baked_col],
                         "정점색평균(선형)": [round(c, 3) for c in vcol_avg],
                         "정점색평균(sRGB, 비교기준)": [round(c, 3) for c in vcol_avg_srgb],
                         "최대차": round(float(diff), 3)})
    return samples


def wire_image_material(mat, image):
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL"):
            nt.nodes.remove(n)
    # 카스미의 원본 재질은 Principled가 아예 없는 Emission-only 그래프라(직접 확인) 없으면
    # 새로 만든다(나나미 등 glTF 임포트로 만들어진 재질은 원래 있어 이 분기를 안 탄다).
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None)
    if out is None:
        out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])


def gltf_material_colors(path):
    """🔴 노바라(희귀함_정내연, 2026-09-23 사장님 지적 「얼굴·목·다리가 새까맣다») — 언릿(KHR_materials_unlit) glTF는
    **진짜 색이 emissiveFactor에 있고 baseColorFactor는 [0,0,0]**인 경우가 있다(이 원본은 재질 36개 중 35개가 그렇다).
    블렌더 glTF 임포터는 언릿 재질을 Emission+Transparent로 만들면서 **baseColor만** Emission Color에 넣고 emissiveFactor는
    버린다 → 우리가 그 검은 Emission을 읽어 64×64 새까만 PNG를 27장 구웠고, 게임에서 피부·옷·눈이 전부 검게 나왔다
    (머리카락만 유일하게 텍스처가 있어 제대로 나왔다 — 그래서 「머리만 정상」으로 보였다).
    원본 파일에서 재질 이름 → (r,g,b,1)을 직접 읽는다. emissiveFactor가 있으면 그걸, 없거나 검으면 baseColorFactor를 쓴다.
    (glTF 색은 선형이고 solid_png도 선형 픽셀을 받으므로 그대로 넘기면 된다.)"""
    import json as _json
    import struct as _struct
    if not path or not os.path.exists(path):
        return {}
    if path.lower().endswith(".glb"):
        raw = open(path, "rb").read()
        jlen = _struct.unpack_from("<I", raw, 12)[0]
        j = _json.loads(raw[20:20 + jlen])
    elif path.lower().endswith(".gltf"):
        j = _json.load(open(path, encoding="utf-8"))
    else:
        return {}
    out = {}
    for m in j.get("materials", []):
        nm = m.get("name")
        if not nm:
            continue
        emis = m.get("emissiveFactor")
        base = m.get("pbrMetallicRoughness", {}).get("baseColorFactor")
        pick = emis if (emis and max(emis[:3]) > 1e-4) else (base if (base and max(base[:3]) > 1e-4) else None)
        if pick:
            out[nm] = tuple(pick[:3]) + (1.0,)
    return out


def solid_png(path, rgba, size=16):
    img = bpy.data.images.new(os.path.basename(path), size, size)
    img.pixels = list(rgba) * (size * size)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def safe_filename(name):
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in name)


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
