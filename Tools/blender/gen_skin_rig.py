"""스킨 리깅 — 뼈 없는 정적 glb를 mixamorig 사람형 FBX로 짓는다(사장님 「폴더+유닛 이름」 절차, 2026-09-14 첫 건 안흔함_김민준).
  blender -b --factory-startup --python Tools/blender/gen_skin_rig.py -- 안흔함_김민준 [--out DIR] [--render DIR]
결과: <유닛>.fbx + Textures/(glb 내장 이미지 원본 바이트) · 키 1.8m · 발 z 0 · 정면 −Y · Hips가 루트 · 모든 뼈 +Y가 자식 쪽 · T자 쉬는 자세.
가중치: 메시를 합친 뒤 UV 이음새로 갈라진 정점을 붙인 사본에서 자동 가중치(bone heat)를 풀고 위치로 되옮긴다 — 이음새 찢어짐 방지.
작은 딱딱한 조각(얼굴·손·소품)은 재질 이름으로 한 뼈에 100% 묶는다(rigid).
관절 표는 키 비율: x = 옆(+ = 캐릭터 왼쪽 = +X, 오른쪽은 거울) · y = 앞뒤(− = 앞) · z = 높이. 정사영 앞·옆 렌더(키 0.05 눈금)에서 읽었다."""
import json
import math
import os
import shutil
import struct
import sys
import tempfile
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PREFIX = "mixamorig:"

SKINS = {
    "안흔함_김민준": dict(
        source="~/Desktop/구랜디스킨모음/02_안흔함/안흔함_김민준/source/Gon Freecss.glb",
        path="Assets/Art/Units/안흔함_김민준/안흔함_김민준.fbx",
        mesh_name="Gon",
        height=1.8,
        center_band=(0.28, 0.36),                                        # 앞뒤·옆 가운데를 재는 높이(다리) — 원점이 두 다리 사이
        joints=dict(Hips=(0, 0, 0.47), Spine=(0, 0, 0.53), Spine1=(0, 0, 0.60), Spine2=(0, 0, 0.67), Neck=(0, 0, 0.745),
                    Head=(0, 0, 0.785), HeadTop=(0, 0, 0.95),
                    Shoulder=(0.025, 0.02, 0.715), Arm=(0.085, 0.02, 0.705), ForeArm=(0.215, 0.02, 0.705), Hand=(0.35, 0.02, 0.705),
                    HandTip=(0.45, 0.02, 0.705),
                    UpLeg=(0.052, 0, 0.455), Leg=(0.052, -0.005, 0.265), Foot=(0.052, 0, 0.06), ToeBase=(0.052, -0.078, 0.02),
                    ToeTip=(0.052, -0.137, 0.02)),
        # 재질 → 뼈(100%): 35 눈썹·눈, 36 얼굴, 37 가슴 405 배지, 38·41 왼손(+X), 39·42 오른손(−X), 43 찌·바늘, 44 낚싯대(오른손)
        rigid={"35": "Head", "36": "Head", "37": "Spine2", "38": "LeftHand", "41": "LeftHand", "39": "RightHand", "42": "RightHand",
               "43": "RightHand", "44": "RightHand"},
        alpha_keep={"43", "44"},                                         # 낚싯줄·바늘만 진짜 컷아웃(α<0.5 41%, 중간값 0) — 나머지 이미지 알파는 전부 1
    ),
    # 알라(코알라) — 옛 키메라 앤트 자리. 두 발로 선 T자 체형이라 사람형. 원본은 +X를 봐서 Z −90°.
    # 팔이 앞으로 약 30° 뻗고 7° 처져 있어 level_arms로 ±X에 맞춰 굽는다(쉬는 자세 = T자).
    "안흔함_강주혁": dict(
        source="~/Desktop/구랜디스킨모음/02_안흔함/안흔함_강주혁.glb",
        path="Assets/Art/Units/안흔함_강주혁/안흔함_강주혁.fbx",
        mesh_name="Koala",
        height=1.8,
        rotate_z=-90.0,
        center_band=(0.08, 0.20),
        uv_layers=1,                                                     # 재질은 UVMap(첫 번째)만 쓴다 — Sketchfab이 붙인 나머지 4벌은 뺀다
        joints=dict(Hips=(0, 0.03, 0.29), Spine=(0, 0.03, 0.38), Spine1=(0, 0.03, 0.47), Spine2=(0, 0.02, 0.57), Neck=(0, -0.03, 0.69),
                    Head=(0, -0.06, 0.76), HeadTop=(0, -0.09, 0.97),
                    Shoulder=(0.05, -0.02, 0.68), Arm=(0.16, -0.07, 0.705), ForeArm=(0.31, -0.12, 0.705), Hand=(0.42, -0.22, 0.695),
                    HandTip=(0.49, -0.24, 0.685),
                    UpLeg=(0.075, 0.01, 0.26), Leg=(0.085, -0.01, 0.155), Foot=(0.09, -0.01, 0.065), ToeBase=(0.10, -0.10, 0.015),
                    ToeTip=(0.11, -0.16, 0.01)),
        rigid={},
        alpha_keep=set(),
        # 재질 → (BSDF 입력, glb 재질의 텍스처 칸, 파일 이름). 금속·거칠기·발광 맵은 안 씀. 옛 0.jpg·1.png·2.jpg(키메라 앤트)와 이름이 안 겹치게.
        textures={"material_0": [("Base Color", "baseColorTexture", "material_0_baseColor.jpeg"), ("Normal", "normalTexture", "material_0_normal.jpeg")]},
        level_arms=True,
    ),
    # 서아인(사이코패스) — 비만형 도살자, 피 묻은 앞치마. 원본은 +X를 봐서(강주혁과 같은 이유) Z −90°.
    # 이미 T자라 level_arms 불필요 — 렌더로 확인. 목깃 옆에 작은 칼(고정 소품)이 있는데 재질이
    # 하나뿐이라 rigid로 못 떼어낸다 — 몸통에 붙어 있어 자동 가중치로도 그대로 몸통을 따라간다(두
    # 자세 렌더에서 확인, 팔·다리만 움직이는 판정 자세라 문제 없음).
    "특별함_서아인": dict(
        source="~/Desktop/구랜디스킨모음/03_특별함/특별함_서아인.glb",
        path="Assets/Art/Units/특별함_서아인/특별함_서아인.fbx",
        mesh_name="Psychopath",
        height=1.8,
        rotate_z=-90.0,
        center_band=(0.02, 0.10),
        joints=dict(Hips=(0, 0, 0.50), Spine=(0, 0, 0.56), Spine1=(0, 0, 0.63), Spine2=(0, 0, 0.72), Neck=(0, 0, 0.855),
                    Head=(0, 0, 0.90), HeadTop=(0, 0, 1.0),
                    Shoulder=(0.06, 0, 0.78), Arm=(0.16, 0, 0.775), ForeArm=(0.32, 0, 0.775), Hand=(0.46, 0, 0.775),
                    HandTip=(0.53, 0, 0.775),
                    UpLeg=(0.10, 0, 0.50), Leg=(0.10, 0, 0.27), Foot=(0.10, -0.01, 0.045), ToeBase=(0.10, -0.06, 0.02),
                    ToeTip=(0.10, -0.08, 0.02)),
        rigid={},
        alpha_keep=set(),
    ),
    # 이지원(음지소녀) — 원본이 zip 속 FBX(source/Lilith.fbx) + 곁텍스처 하나(textures/Plane.001.png,
    # Eye 재질용)라 glb 파이프라인을 그대로 못 쓴다. source_type="fbx_zip"로 갈래를 타서 build()가
    # zip을 풀고 FBX를 임포트한 뒤, 진짜 텍스처가 있는 재질(Eye) 하나만 그 파일을 잇고 나머지
    # 19개 재질(대부분 FBX에 이미지 없이 색만 있음, 일부는 "Lilithbody.001" 같은 깨진 경로를
    # 참조하지만 실제 파일이 zip에 없음)은 그 재질의 Base Color 값을 그대로 16×16 단색 PNG로
    # 구워 잇는다(회색 기본재질 방지).
    # 🔴 PM 사전조사와 다른 점 셋(직접 확인): ① Cube는 이 파일에 없다(13종이 아니라 실제 12개
    #   메시). ② Detail1/Detail1.001은 중복이 아니라 좌우 대칭 쌍(bbox가 x=0 기준 거울상, 부츠
    #   목 높이의 장식 — 렌더로 확인). 재질(Material.009·010)을 공유해서 좌우로 못 가르니 둘 다
    #   Hips에 rigid로 묶었다 — ⚠️ 실제로는 종아리 높이 장식이라 Leg가 해부학적으로는 맞지만,
    #   한쪽 다리 뼈에 둘 다 묶으면 반대쪽 장식이 그 다리를 따라가 걸을 때 어긋난다. Hips는
    #   적어도 좌우 대칭은 깨지지 않아 덜 어색한 절충안이다(사장님/PM 판단 필요하면 보고).
    #   ③ 전부 포함한 실제 삼각형 합이 11,488로 PM 추정(~11,500)과 정확히 일치 — 뺄 게 없다는 뜻.
    # TeethUp·TeethDown은 재질 슬롯이 아예 없어서(FBX에 재질 미지정) build()가 임포트 직후
    # 자동으로 "<메시이름>Mat" 재질을 만들어 붙인다(치아색 기본값), rigid에서 Head로 묶는다.
    # 방향: 원본이 이미 −Y를 보고 서 있다(Eye·TeethUp·TeethDown 전부 head 메시의 −y쪽 끝에
    # 몰려 있어 확인, 얼굴이 원래 정면 그대로) — rotate_z 불필요.
    # 몸(Detail3, 팔·다리·코트·장갑·부츠를 다 담은 메시)이 재질별로 자동가중치 될 단일 몸통이라
    # body_mesh_name으로 지정(자동 선택은 정점 수로 고르는데 Hair가 Detail3보다 정점이 많아
    # 잘못 고른다 — 실제로 확인함).
    # 🔴 2026-09-15 요크로 교체되어 항목 삭제 — 사장님 지시로 특별함_이지원 스킨을 바운티러시
    # 요크로 바꾼다(기존 릴리스 폐기, blender가 fix_unit_fbx.py로 새로 지음). 위 설명 주석은
    # 다른 유닛(특별함_주영호 141행 등)이 같은 함정을 참고하므로 남겨 둔다 — 이 dict만 뺐다.
    # 다시 이 이름으로 build()를 돌리면 요크 릴리스를 덮어쓰니 되살리지 말 것.
    # 마마보이(오크 전사) — 66메시·69,576삼각형짜리 Sketchfab 조각 세트(몸통 하나 + 갑옷·소품
    # 60여 개가 전부 따로). 정면·회전 확인(fromNegY 렌더): 원본이 이미 정면 −Y·팔 좌우
    # 확산(X)·키 Z위라 rotate_z 불필요 — 04부터 셋 다 rotate_z가 필요했던 것과 다르다.
    # 팔이 어깨에서 약 30~40° 처져 있어(A자에 가까움) level_arms로 편다.
    "특별함_주영호": dict(
        source="~/Desktop/구랜디스킨모음/03_특별함/특별함_주영호.glb",
        path="Assets/Art/Units/특별함_주영호/특별함_주영호.fbx",
        mesh_name="Orc",
        height=1.8,
        # 정점 수로 자동 고르면 Shoulder_Fur(10,964정점)가 Body_Low(정확한 몸통)보다 많아 잘못
        # 뽑힌다 — 이지원(Hair vs Detail3)과 같은 함정이라 body_mesh_name으로 못박는다.
        body_mesh_name="Body_Low_Body_Texture_0",
        # Body_Low는 몸통·머리·팔뿐(정점 z 0.40~1.0 사이) — 다리는 전부 별도 조각(Trouser·
        # Leg_Wrap·Boot)이라 발목 높이로는 center_band를 못 잡는다. Body_Low 자체의 맨 아래
        # (골반 끝단, 0.40~0.45)에서 좌우 중심을 잡는다.
        center_band=(0.40, 0.45),
        joints=dict(Hips=(0, 0, 0.47), Spine=(0, 0, 0.53), Spine1=(0, 0, 0.60), Spine2=(0, 0, 0.66), Neck=(0, 0, 0.78),
                    Head=(0, 0, 0.83), HeadTop=(0, 0, 1.0),
                    Shoulder=(0.09, 0, 0.66), Arm=(0.20, -0.01, 0.62), ForeArm=(0.30, -0.02, 0.53), Hand=(0.38, -0.02, 0.44),
                    HandTip=(0.44, -0.02, 0.40),
                    UpLeg=(0.09, 0, 0.47), Leg=(0.09, 0, 0.27), Foot=(0.09, 0, 0.06), ToeBase=(0.09, -0.06, 0.02),
                    ToeTip=(0.09, -0.10, 0.01)),
        level_arms=True,
        rigid={},                                                        # 재질 기준 rigid는 안 씀(대부분 Armour_Texture 하나를
                                                                          # 60여 조각이 같이 써서 재질로는 못 가른다) — 아래 참고
        alpha_keep=set(),
        # 노멀맵 있는 둘만 명시 — Eye_Texture(베이스만)·Default_Material(이미지 자체가 없음)은
        # 목록에서 빼서 build()가 Eye는 그대로, Default_Material은 baseColorFactor로 단색
        # PNG를 합성하게 한다(PM: image1·4·6·7 안 씀 — 그것들이 바로 이 둘의 금속/거칠기 맵).
        textures={"Body_Texture": [("Base Color", "baseColorTexture", "Body_Texture_diffuse.png"),
                                    ("Normal", "normalTexture", "Body_Texture_normal.png")],
                  "Eye_Texture": [("Base Color", "baseColorTexture", "Eye_Texture_diffuse.png")],
                  "Armour_Texture": [("Base Color", "baseColorTexture", "Armour_Texture_diffuse.png"),
                                      ("Normal", "normalTexture", "Armour_Texture_normal.png")]},
        # ── 이 유닛 전용: 66조각을 이름으로 갈라 몸통(자동가중치)과 갑옷/소품(rigid)으로 나눈다.
        # (부분 문자열, 뼈 틀 — "{side}"는 그 조각의 최종 x부호로 채운다. None이면 자동가중치로 둔다.)
        # 순서대로 첫 매치 — Body_Low·Jaw·Leg_Wrap·Trouser(몸에 밀착해 자동가중치가 자연스럽다)는
        # 전부 매치 없음(자동가중치). Eye는 Head. 허리 장신구(Belt·Tooth류, 좌우 안 가려도 되는
        # 것)는 Hips. Wrist는 팔뚝, Boot는 발. 어깨쪽(Shoulder·Horn·Ring)은 대부분 좌우 한쪽에만
        # 있는 비대칭 디자인이라(원작 확인 — Shoulder_Small/Large/Fur가 1개씩뿐, 좌우 쌍이 아님)
        # x부호로 그때그때 판정하되, 중앙(|x|<0.06m)이면 어깨 망토로 보고 Spine2로.
        # 🔴 Boot는 rigid에서 뺐다 — 전부 Foot에 100% 묶으면 ToeBase가 가중치 0으로 남아
        # assert가 막는다(발이 통째로 부츠 속이라 맨발 메시가 아예 없다). 부츠 자체를
        # 자동가중치로 두면 발목~발끝을 잇는 연속 메시라 Foot·ToeBase 둘 다 히트가 닿는다.
        rigid_by_name=[
            ("Eye", "Head", None), ("Jaw", "Head", None),
            ("Belt", "Hips", None), ("Tooth", "Hips", None), ("Loop", "Hips", None),
            ("Wrist", "{side}ForeArm", None),
            ("Shoulder", "{side}Arm", 0.06), ("Horn", "{side}Arm", 0.06), ("Ring", "{side}Arm", 0.06),
        ],
        # ── 66조각 중 큰 털/모피 덩어리 셋(Shoulder_Fur 19,296·Belt_Fur_1 7,150·Belt_Fur_2 7,494
        # = 33,940 = 전체의 49%)이 삼각형을 절반 가까이 먹는다 — 실제 굴곡이 아니라 털 가닥을
        # 기하로 흉내낸 것이라 크게 줄여도 실루엣이 거의 안 바뀐다. Body_Low·Jaw·Eye(얼굴 판정
        # 부위, PM 지시 "얼굴·손은 덜 줄이게")는 안 줄이고, 나머지(허리띠·장화·손목·뿔 등 소품)는
        # 중간 정도만 줄인다. (부분 문자열, 목표 비율 — bmesh decimate_collapse, delimit={UV·
        # NORMAL·MATERIAL}로 UV 이음매 보존) 순서대로 첫 매치, 미매치는 1.0(안 줄임).
        decimate_rules=[("Fur", 0.22), ("Body_Low", 1.0), ("Jaw", 1.0), ("Eye", 1.0), ("Hand", 1.0)],
        decimate_default=0.72,
    ),
    # 최준우(원피스 피규어 베르고) — Sketchfab 단일 메시(Object_4, PM 사전조사의 "Object_0"은
    # 틀렸다 — 실제 이름은 Object_4, 삼각형 77,202는 일치). Sketchfab_model(X−90)·
    # GLTF_SceneRootNode(X+90) 상쇄 확인(직접 실측 — 임포트 후 오브젝트 loc/rot/scale 전부
    # 항등, g0_0 이동(−0.0006,−0.3693,0.1253)만 남는다). 정면 −Y는 렌더로 직접 확인(카메라
    # −Y쪽에서 얼굴이 보임) — rotate_z 불필요. 원본 키(z) 0.2799(PM 0.280과 일치).
    # 🔴 팔 위험 확인(코알라 교훈) — x단면 스캔(30분할)으로 봤을 때 팔이 있는 높이 구간 전부
    # x폭이 허리~가슴 몸통 폭(약 0.08~0.10)을 못 넘는다 — 긴 코트가 팔까지 통째로 감싸서
    # 단면만으로는 팔이 안 갈린다(코알라와 같은 위험 신호). 다만 코알라는 "좌우 비대칭"이라
    # 관절표 자체가 표현 불가능했던 반면, 이건 팔이 좌우 대칭으로 몸에 붙어 있어 bone_table의
    # x부호 미러가 그대로 통하는 케이스다 — 자동가중치가 실패하는지는 실제로 돌려서 판정한다.
    # 자세: 팔이 옆으로 살짝 벌어져 내려간 것(T자 아님) → level_arms로 사후 수평.
    # 🔴 2026-09-15 blender gen_scan_rig.py로 재작업되어 항목 삭제 — 되살리지 말 것. 유니티에서
    # 공용 Idle을 입히자 팔이 들리고 코트가 풍선처럼 부푸는 문제(위 팔 수평 assert가 여기 있는
    # 이유)가 나와, blender가 원본부터 gen_scan_rig.py로 다시 지었다(같은 경로에 덮어씀). 이
    # 이름으로 build()를 다시 돌리면 그 결과물을 이 낡은 버전으로 덮어쓴다(팔 수평 assert가
    # 내보내기 전에 막아 주긴 하지만 헷갈리지 않게 dict 자체를 뺐다).
    # 🔴 2026-09-15 아이언맨으로 교체되어 항목 삭제 — 되살리지 말 것. 사장님 지시로
    # 특별함_강주혁 스킨을 아이언맨으로 바꾼다(blender가 fix_unit_fbx.py로 같은 경로에 새로
    # 짓는 중). 아래 설명 주석(좌우 비대칭 관절표 한계·위축 Z·정면 +X 등)은 교훈이라 남기지만
    # dict 항목은 뺐다 — 이 이름으로 build()를 다시 돌리면 아이언맨을 코알라로 덮어쓴다.
    # 강주혁(코알라, 카이메라 앤트) — 사이트페브 tripo AI 생성. 기존 안흔함_강주혁(다른 소스,
    # medium_poly 코알라)과 이름만 접두만 다르고 완전히 별개 유닛 — 그 폴더는 안 건드린다.
    # 4조각(Object_4~7, 각 6.4만 정점 근처 — 65536 정점 한도로 쪼개진 한 몸이다. 직접 확인:
    # 정점 수 다르고 bbox가 서로 겹치는 부분 몸통 조각이라 LOD 중복이 아니라 분할 조각. join+
    # 이음새 용접으로 합친다). 원본은 이미 Z-up(PM 사전조사의 "Y가 위"는 틀렸다 — 실측
    # bbox z 스팬 1.0 > y 스팬 0.59 > x 스팬 0.4, Z가 세로축)에 정면은 +X(렌더로 확인, 넥타이
    # 맨 정장 차림 카이메라 앤트 코알라가 +X쪽을 본다) → rotate_z=-90으로 −Y 정면.
    # 팔은 T자가 아니라 몸통 옆에 늘어뜨린 자세(한 손엔 호리병 모양 소품을 들고, 한 손은
    # 주머니에) — level_arms로 사후에 수평으로 편다.
}

# (뼈, 머리 관절, 꼬리 관절, 부모) — 왼쪽/오른쪽은 L·R 두 벌
SPINE = [("Hips", "Hips", "Spine", None), ("Spine", "Spine", "Spine1", "Hips"), ("Spine1", "Spine1", "Spine2", "Spine"),
         ("Spine2", "Spine2", "Neck", "Spine1"), ("Neck", "Neck", "Head", "Spine2"), ("Head", "Head", "HeadTop", "Neck")]
LIMB = [("Shoulder", "Shoulder", "Arm", "Spine2"), ("Arm", "Arm", "ForeArm", "Shoulder"), ("ForeArm", "ForeArm", "Hand", "Arm"),
        ("Hand", "Hand", "HandTip", "ForeArm"),
        ("UpLeg", "UpLeg", "Leg", "Hips"), ("Leg", "Leg", "Foot", "UpLeg"), ("Foot", "Foot", "ToeBase", "Leg"), ("ToeBase", "ToeBase", "ToeTip", "Foot")]


def glb(path):
    b = open(path, "rb").read()
    n = struct.unpack_from("<I", b, 12)[0]
    j = json.loads(b[20:20 + n])
    off = 20 + n                                                        # JSON 청크 길이는 4바이트 채움을 이미 포함
    blen = struct.unpack_from("<I", b, off)[0]
    return j, b[off + 8:off + 8 + blen]


def image_bytes(j, binchunk, index):
    bv = j["bufferViews"][j["images"][index]["bufferView"]]
    s = bv.get("byteOffset", 0)
    return binchunk[s:s + bv["byteLength"]]


def write_solid_png(path, rgba, size=16):
    """이미지 없는 재질용 합성 텍스처 — Base Color 값 그대로 작은 단색 PNG로 굽는다(회색 기본재질 방지)."""
    img = bpy.data.images.new("_solid", size, size, alpha=True)
    img.pixels = list(rgba) * (size * size)
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def bone_table(joints):
    out = []
    for name, head, tail, parent in SPINE:
        out.append((name, joints[head], joints[tail], parent))
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        for name, head, tail, parent in LIMB:
            h, t = joints[head], joints[tail]
            par = parent if parent in ("Spine2", "Hips") else side + parent
            out.append((side + name, (h[0] * sx, h[1], h[2]), (t[0] * sx, t[1], t[2]), par))
    return out


def build(name, cfg, out_dir=None, render_dir=None):
    src = os.path.expanduser(cfg["source"])
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src}
    bpy.ops.wm.read_factory_settings(use_empty=True)

    if cfg.get("source_type") == "fbx_zip":
        # ── zip 속 FBX(뼈·텍스처 임베드 없음) — glb 파이프라인과 갈라서 여기서 끝낸다.
        extract_dir = tempfile.mkdtemp(prefix="skinzip_")
        with zipfile.ZipFile(src) as z:
            z.extractall(extract_dir)
        bpy.ops.import_scene.fbx(filepath=os.path.join(extract_dir, cfg["fbx_member"]))
        scene = bpy.context.scene
        # 재질 슬롯이 아예 없는 메시(예: 이·치아) — 즉석에서 "<메시이름>Mat" 재질을 만들어 붙인다.
        # rigid 표에서 이 이름으로 뼈에 묶는다(특별함_이지원의 TeethUpMat·TeethDownMat 참고).
        for o in list(scene.objects):
            if o.type == "MESH" and len(o.material_slots) == 0:
                mat = bpy.data.materials.new(f"{o.name}Mat")
                mat.use_nodes = True
                mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.85, 0.8, 0.78, 1.0)
                o.data.materials.append(mat)
        os.makedirs(tex_dir, exist_ok=True)
        real_name = cfg.get("real_texture_material")
        real_src = os.path.join(extract_dir, cfg["tex_member"]) if cfg.get("tex_member") else None
        mat_image = {}
        for mt in list(bpy.data.materials):
            if mt.name == real_name and real_src:
                fname = f"{mt.name}_diffuse.png"
                shutil.copy(real_src, os.path.join(tex_dir, fname))
            else:
                bsdf = next((n for n in mt.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if mt.node_tree else None
                rgba = tuple(bsdf.inputs["Base Color"].default_value) if bsdf else (0.6, 0.6, 0.6, 1.0)
                fname = f"{mt.name}_solid.png"
                write_solid_png(os.path.join(tex_dir, fname), rgba)
            mat_image[mt.name] = [("Base Color", fname)]
        report["재질→텍스처"] = mat_image
        cfg = dict(cfg, textures=True)                                  # 아래 재질 루프가 rebuild_material 경로를 타게
    else:
        bpy.ops.import_scene.gltf(filepath=src)
        scene = bpy.context.scene

        # ── 텍스처: 재질이 쓰는 내장 이미지를 원본 바이트 그대로 Textures/에(파일 이름 = 그 이미지를 쓰는 첫 재질 이름)
        j, binchunk = glb(src)
        os.makedirs(tex_dir, exist_ok=True)
        mat_image, files = {}, {}
        for mt in (j["materials"] if not cfg.get("textures") else []):
            ext = mt.get("extensions", {}).get("KHR_materials_pbrSpecularGlossiness", {})
            tex = ext.get("diffuseTexture") or mt.get("pbrMetallicRoughness", {}).get("baseColorTexture")
            src_index = j["textures"][tex["index"]]["source"]
            if src_index not in files:
                fname = f"{mt['name']}_diffuse.png"
                open(os.path.join(tex_dir, fname), "wb").write(image_bytes(j, binchunk, src_index))
                files[src_index] = fname
            mat_image[mt["name"]] = files[src_index]
        for mt in (j["materials"] if cfg.get("textures") else []):
            entries = []
            if mt["name"] not in cfg["textures"]:
                # 이 재질은 이미지가 아예 없다(예: Default_Material — 벨트 장식 몇 조각, 색만 있음).
                # glTF baseColorFactor를 그대로 단색 PNG로(강주혁 항목이 대상 재질을 전부 나열해
                # 두면 이 분기는 안 타지만, 이미지 없는 재질까지 갖는 소스는 여기로 떨어진다).
                rgba = tuple(mt.get("pbrMetallicRoughness", {}).get("baseColorFactor", (0.6, 0.6, 0.6, 1.0)))
                fname = f"{mt['name']}_solid.png"
                write_solid_png(os.path.join(tex_dir, fname), rgba)
                mat_image[mt["name"]] = [("Base Color", fname)]
                continue
            for socket, slot, fname in cfg["textures"][mt["name"]]:
                tex = mt.get(slot) or mt.get("pbrMetallicRoughness", {}).get(slot)
                open(os.path.join(tex_dir, fname), "wb").write(image_bytes(j, binchunk, j["textures"][tex["index"]]["source"]))
                entries.append((socket, fname))
            mat_image[mt["name"]] = entries
        report["재질→텍스처"] = mat_image

    meshes = [o for o in scene.objects if o.type == "MESH"]
    Rz = Matrix.Rotation(math.radians(cfg.get("rotate_z", 0.0)), 4, "Z")
    world = {o.name: np.array([Rz @ o.matrix_world @ v.co for v in o.data.vertices]) for o in meshes}
    P = np.concatenate(list(world.values()))
    lo, hi = P.min(0), P.max(0)
    H = float(hi[2] - lo[2])
    # 정점 수로 자동 고르면(대부분은 이게 맞다) 몸통이 아니라 다른 조각이 뽑힐 수 있다 — 이지원은
    # Hair(2536)가 Detail3(2066, 실제 몸통)보다 정점이 많아 body_mesh_name으로 못박는다.
    body = None
    if cfg.get("body_mesh_name"):
        body = next((o for o in meshes if o.name == cfg["body_mesh_name"]), None)
    body = body or max(meshes, key=lambda o: len(o.data.vertices))
    B = world[body.name]
    band = B[(B[:, 2] >= lo[2] + H * cfg["center_band"][0]) & (B[:, 2] <= lo[2] + H * cfg["center_band"][1])]
    cx, cy = float(band[:, 0].min() + band[:, 0].max()) / 2, float(band[:, 1].min() + band[:, 1].max()) / 2
    s = float(cfg["height"] / H)
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo[2])) @ Rz
    report["원본 키"] = round(float(H), 4)
    report["배율"] = round(s, 4)

    # ── 조각별 감량(선택) — UV·재질·법선 경계를 넘지 않게(delimit) bmesh decimate_collapse.
    # 원본이 그대로면(비율 1.0) 건너뛴다. 이름 매치 순서대로 첫 비율, 없으면 decimate_default.
    name_rigid_points = []                                              # (최종 좌표, 뼈이름) — 이름 기준 rigid, 조각별로 미리 기록
    decimate_rules = cfg.get("decimate_rules", [])
    decimate_default = cfg.get("decimate_default", 1.0)
    rigid_by_name = cfg.get("rigid_by_name", [])
    tri_before = tri_after = 0

    def resolve_bone(template, obj_final_pts, side_threshold):
        if "{side}" not in template:
            return template
        if side_threshold is not None and abs(float(np.mean(obj_final_pts[:, 0]))) < side_threshold:
            return "Spine2"
        return template.format(side="Left" if float(np.mean(obj_final_pts[:, 0])) >= 0 else "Right")

    # ── 메시를 세계로 굽고 하나로
    for o in meshes:
        M = G @ o.matrix_world
        o.parent = None
        o.data.transform(M)
        if M.determinant() < 0:
            o.data.flip_normals()
        o.matrix_basis = Matrix.Identity(4)
        tri_before += len(o.data.polygons)                              # (사각형 섞여도 나중 삼각분할 후 report로 다시 잰다 — 대략치)
        ratio = decimate_default
        for sub, r in decimate_rules:
            if sub in o.name:
                ratio = r
                break
        if ratio < 0.999:
            # 🔴 이 Blender(5.2.1)엔 bmesh.ops.decimate_collapse가 없다(UV 경계를 델리밋으로
            # 보존하는 그 연산 대신 표준 Decimate 모디파이어 COLLAPSE로 — UV 경계를 정확히
            # 안 지키지만(약간의 이음매 왜곡 가능) 안정적으로 동작한다. 털/모피처럼 원래
            # 텍스처가 반복 패턴이라 이음매가 조금 어긋나도 눈에 잘 안 띄는 조각 위주로만
            # 크게 줄여서(Fur 0.22) 이 위험을 줄인다).
            mod = o.modifiers.new("decimate", "DECIMATE")
            mod.ratio = ratio
            dg = bpy.context.evaluated_depsgraph_get()
            ev = o.evaluated_get(dg)
            new_mesh = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
            o.modifiers.remove(mod)
            old_mesh = o.data
            o.data = new_mesh
            bpy.data.meshes.remove(old_mesh)
        tri_after += len(o.data.polygons)
        for sub, template, side_threshold in rigid_by_name:
            if sub in o.name:
                pts = np.array([v.co for v in o.data.vertices])
                bone = resolve_bone(template, pts, side_threshold)
                for p in pts:
                    name_rigid_points.append((Vector(p), PREFIX + bone))
                break
    if decimate_rules or decimate_default < 0.999:
        report["감량(조각별, 대략)"] = {"전": tri_before, "후": tri_after}
    for o in [o for o in scene.objects if o.type != "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)
    for o in scene.objects:
        o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = body.data.name = cfg["mesh_name"]
    while len(body.data.uv_layers) > cfg.get("uv_layers", 99):
        body.data.uv_layers.remove(body.data.uv_layers[-1])
    mats = sorted(sl.material.name for sl in body.material_slots)
    report["재질"] = mats

    # ── 재질: 알파는 진짜 컷아웃만, 이미지는 Textures 파일로
    for sl in body.material_slots:
        m = sl.material
        if cfg.get("textures"):
            rebuild_material(m, mat_image[m.name], tex_dir)
            continue
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        img_node = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
        for n in [n for n in nt.nodes if n.type == "MATH"]:
            nt.nodes.remove(n)
        img_node.image = bpy.data.images.load(os.path.join(tex_dir, mat_image[m.name]), check_existing=True)
        if not bsdf.inputs["Base Color"].links:
            nt.links.new(img_node.outputs["Color"], bsdf.inputs["Base Color"])
        for l in list(bsdf.inputs["Alpha"].links):
            nt.links.remove(l)
        if m.name in cfg["alpha_keep"]:
            nt.links.new(img_node.outputs["Alpha"], bsdf.inputs["Alpha"])
            m.blend_method = "CLIP" if hasattr(m, "blend_method") else None
        else:
            bsdf.inputs["Alpha"].default_value = 1.0
            if hasattr(m, "blend_method"):
                m.blend_method = "OPAQUE"
            if hasattr(m, "surface_render_method"):
                m.surface_render_method = "DITHERED"
    for im in [im for im in bpy.data.images if im.packed_file and im.users == 0]:
        bpy.data.images.remove(im)

    # ── 뼈대
    Hf = cfg["height"]
    table = bone_table(cfg["joints"])
    data = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", data)
    scene.collection.objects.link(arm)
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for bname, h, t, parent in table:
        eb = data.edit_bones.new(PREFIX + bname)
        eb.head = Vector(h) * Hf
        eb.tail = Vector(t) * Hf
        d = (eb.tail - eb.head).normalized()
        eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    for bname, h, t, parent in table:
        if parent:
            data.edit_bones[PREFIX + bname].parent = data.edit_bones[PREFIX + parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    report["뼈"] = len(data.bones)

    # ── 가중치: 이음새를 붙인 사본에서 자동 가중치 → 위치로 되옮김, 딱딱한 조각은 한 뼈 100%
    rigid_slots = {i: PREFIX + cfg["rigid"][sl.material.name] for i, sl in enumerate(body.material_slots) if sl.material.name in cfg["rigid"]}
    rigid_vert = {}
    for p in body.data.polygons:
        if p.material_index in rigid_slots:
            for vi in p.vertices:
                rigid_vert[vi] = rigid_slots[p.material_index]
    # 이름 기준 rigid(재질 하나를 60여 조각이 같이 쓰는 마마보이류) — 조각별로 미리 적어 둔
    # (최종 좌표, 뼈) 목록을 합친 뒤의 실제 정점에 위치로 되찾아 붙인다(조각 순서가 join 뒤
    # 그대로 남는다는 가정 없이, 자동가중치 되옮김과 같은 방식으로 안전하게).
    if name_rigid_points:
        body_kd = KDTree(len(body.data.vertices))
        for v in body.data.vertices:
            body_kd.insert(v.co, v.index)
        body_kd.balance()
        matched, missed = 0, 0
        for pos, bone in name_rigid_points:
            _, vi, dist = body_kd.find(pos)
            if dist < 1e-4:
                rigid_vert[vi] = bone
                matched += 1
            else:
                missed += 1
        report["이름기준 rigid 정점"] = {"매치": matched, "놓침(감량으로 위치 이동)": missed}
    tmp = body.copy()
    tmp.data = body.data.copy()
    scene.collection.objects.link(tmp)
    bm = bmesh.new()
    bm.from_mesh(tmp.data)
    rigid_idx = set(rigid_slots)
    # 🔴 이름 기준 rigid(마마보이류)도 재질 기준과 똑같이 자동가중치 사본에서 빼야 한다 —
    # 안 빼면 눈·이빨·허리 장신구·손목·어깨 갑옷 60여 조각이 전부 본히트 계산에 같이 들어가
    # (서로 안 이어진 섬이 잔뜩 섞여) 히트 확산 자체가 실패한다(실측: 이걸 빼기 전엔
    # weighted 정점이 통째로 0개 — "Bone Heat Weighting: failed to find solution" 경고 뒤
    # 전부 실패).
    rigid_vert_idx = set(rigid_vert)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in rigid_idx or any(v.index in rigid_vert_idx for v in f.verts)],
                     context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    welded = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    report["이음새 붙임(정점)"] = welded - len(bm.verts)
    bm.to_mesh(tmp.data)
    bm.free()
    for o in scene.objects:
        o.select_set(o in (tmp, arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    kd = KDTree(len(tmp.data.vertices))
    for v in tmp.data.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    gname = {g.index: g.name for g in tmp.vertex_groups}
    # bone heat가 못 푼 정점(털 가닥·발톱 같은 떨어진 조각) → 가장 가까운 「가중치 있는」 정점의 가중치를 그대로(가까운 뼈로 주면 귀털이 어깨에 붙었다)
    weighted = [v.index for v in tmp.data.vertices if any(g.weight > 1e-4 for g in v.groups)]
    kd_w = KDTree(len(weighted))
    for i in weighted:
        kd_w.insert(tmp.data.vertices[i].co, i)
    kd_w.balance()
    for b in data.bones:
        body.vertex_groups.new(name=b.name)
    unweighted, far, far_fill = 0, 0.0, 0.0
    for v in body.data.vertices:
        if v.index in rigid_vert:
            body.vertex_groups[rigid_vert[v.index]].add([v.index], 1.0, "REPLACE")
            continue
        co, ti, dist = kd.find(v.co)
        far = max(far, dist)
        ws = [(gname[g.group], g.weight) for g in tmp.data.vertices[ti].groups if g.weight > 1e-4]
        if not ws:
            unweighted += 1
            _, wi, wd = kd_w.find(v.co)
            far_fill = max(far_fill, wd)
            ws = [(gname[g.group], g.weight) for g in tmp.data.vertices[wi].groups if g.weight > 1e-4]
        total = sum(w for _, w in ws)
        for gn, w in ws:
            body.vertex_groups[gn].add([v.index], w / total, "REPLACE")
    bpy.data.objects.remove(tmp, do_unlink=True)
    report["자동가중치 실패→가까운 가중치 정점"] = unweighted
    report["채움 최대 거리(m)"] = round(far_fill, 4)
    report["되옮김 최대 거리"] = round(far, 6)
    counts = {b.name[len(PREFIX):]: 0 for b in data.bones}
    for v in body.data.vertices:
        for g in v.groups:
            if g.weight > 0.01:
                counts[body.vertex_groups[g.group].name[len(PREFIX):]] += 1
    report["뼈별 정점(w>0.01)"] = counts
    dead = [k for k, c in counts.items() if c == 0]
    assert not dead, f"{name}: 가중치 없는 뼈 {dead}"
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    body.parent = arm
    if cfg.get("level_arms"):
        report["팔 수평 굽기(°)"] = level_arms(arm, body)

    bpy.context.view_layer.update()
    V = np.array([v.co for v in body.data.vertices])
    size = V.max(0) - V.min(0)
    report["크기(m)"] = [round(float(c), 3) for c in size]
    report["최저 z"] = round(float(V[:, 2].min()), 4)
    # 🔴 2026-09-15 PM 지시(베르고 교훈) — 팔 45°·무릎 90° 판정과 뼈 0mm 검사로는 "쉬는 자세가
    # 진짜 T자인가"가 안 잡힌다. 베르고는 팔이 옆으로 살짝 내려온 자세라 이 검사들은 전부
    # 통과했는데, 유니티에서 공용 Idle을 입히자(리타게팅) 팔이 들리고 코트가 풍선처럼 부풀며
    # 자락이 훌라후프처럼 벌어졌다 — Idle이 "T자에서 이만큼 굽힌다"는 상대 회전이라, 쉬는
    # 자세 자체가 T자가 아니면 그 상대 회전이 엉뚱한 절대 자세로 어긋난다(가로 1.05m인데
    # 키 1.8m — 팔을 편 폭이 키의 0.9배도 안 됨). 가로(V.max(0)-V.min(0)의 x 성분)가 키의
    # 0.9배 이상이어야 "팔이 수평"이라고 본다 — 안 되면 level_arms를 켜거나 joints의 Arm/
    # ForeArm/Hand z값을 Shoulder와 같은 높이로 맞출 것.
    assert size[0] >= Hf * 0.9, (
        f"{name}: 쉬는 자세 가로 {size[0]:.3f}m가 키 {Hf}m의 0.9배 미만 — 팔이 수평 T자가 아니다"
        f"(유니티 Idle 리타게팅에서 팔이 들리고 옷이 풍선처럼 부푼다, 베르고 교훈). "
        f"level_arms=True를 켜거나 joints의 Arm/ForeArm/Hand z를 Shoulder와 맞출 것.")

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    if render_dir:
        report["판정"] = judge(name, arm, body, render_dir)
    return report


def rebuild_material(m, entries, tex_dir):
    """재질 노드를 베이스·노멀만 남기고 다시 — 금속 0·거칠기 0.8·발광 끔, 불투명."""
    nt = m.node_tree
    for n in [n for n in nt.nodes if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL")]:
        nt.nodes.remove(n)
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    for l in list(bsdf.inputs["Alpha"].links):
        nt.links.remove(l)
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    bsdf.inputs["Emission Strength"].default_value = 0.0
    bsdf.inputs["Emission Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    for i, (socket, fname) in enumerate(entries):
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(os.path.join(tex_dir, fname), check_existing=True)
        node.location = (-700, 300 - 320 * i)
        if socket == "Normal":
            node.image.colorspace_settings.name = "Non-Color"
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nm.location = (-350, 300 - 320 * i)
            nt.links.new(node.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        else:
            nt.links.new(node.outputs["Color"], bsdf.inputs[socket])
    if hasattr(m, "blend_method"):
        m.blend_method = "OPAQUE"


def level_arms(arm, body):
    """팔(위팔·아래팔·손)을 ±X로 돌려 메시를 굽고 그 자세를 쉬는 자세로 — T자, 팔 수평. 굽기 전후 메시 차이 검사."""
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
    want = np.array([v.co for v in baked.vertices])
    old = body.data
    body.data = baked
    baked.name = old.name
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
    assert drift < 1e-4, f"팔 굽기 뒤 메시가 움직였다 {drift}"
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        b = arm.data.bones[PREFIX + side + "Hand"]
        assert (b.tail_local - b.head_local).normalized().dot(Vector((sx, 0, 0))) > 0.999
    return turned


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return (a + ab * t - p).length


def _turn(arm, bone, axis, deg):
    pb = arm.pose.bones[PREFIX + bone]
    head = pb.matrix.to_translation()
    pb.matrix = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()


POSES = {
    "T": [],
    "arms45_knees90": [("LeftArm", "Y", 45), ("RightArm", "Y", -45), ("LeftUpLeg", "X", -45), ("RightUpLeg", "X", -45),
                       ("LeftLeg", "X", 90), ("RightLeg", "X", 90)],
}


def judge(name, arm, body, out):
    """판정 렌더(정사영 앞·옆·무릎/어깨 확대) + 늘어짐 = 자세 모서리 길이 / 쉬는 모서리 길이."""
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
    rest = np.array([v.co for v in body.data.vertices])
    edges = np.array([e.vertices for e in body.data.edges])
    L0 = np.linalg.norm(rest[edges[:, 0]] - rest[edges[:, 1]], axis=1)
    result = {}
    for pose, turns in POSES.items():
        for pb in arm.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        for bone, axis, deg in turns:
            _turn(arm, bone, axis, deg)
        dg = bpy.context.evaluated_depsgraph_get()
        ev = body.evaluated_get(dg)
        me = ev.to_mesh()
        cur = np.array([v.co for v in me.vertices])
        ev.to_mesh_clear()
        ok = L0 > 1e-5
        ratio = np.where(ok, np.linalg.norm(cur[edges[:, 0]] - cur[edges[:, 1]], axis=1) / np.maximum(L0, 1e-9), 1.0)
        top = np.argsort(-ratio)[:5]
        result[pose] = dict(최대늘어짐=round(float(ratio.max()), 2), 상위1퍼센트=round(float(np.percentile(ratio, 99)), 2),
                            최소=round(float(ratio[ok].min()), 2),
                            최대자리=[(round(float(ratio[i]), 2), [round(float(c), 2) for c in rest[edges[i, 0]]]) for i in top],
                            최저z=round(float(cur[:, 2].min()), 3))
        views = [("front", (0, -5, 0.9), (math.radians(90), 0, 0), 2.0), ("side", (5, 0, 0.9), (math.radians(90), 0, math.radians(90)), 2.0)]
        if turns:
            views += [("knee", (3, -3, 0.45), (math.radians(90), 0, math.radians(45)), 0.9), ("shoulder", (0, -5, 1.2), (math.radians(90), 0, 0), 1.0)]
        for tag, loc, rot, scale in views:
            cam.location, cam.rotation_euler, cam.data.ortho_scale = loc, rot, scale
            scene.render.filepath = os.path.join(out, f"{name}_{pose}_{tag}.png")
            bpy.ops.render.render(write_still=True)
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    return result


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
        print("리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
