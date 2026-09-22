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
    # 코라손(전설적인_홍인창, One Piece 돈키호테 로시난테) — PM 사양 원문 보존(2026-09-17):
    # 뼈 없음·메시 7(Body 2,623·Face 3,088·Hair 6,969·Iris 98·Met 1,543·Wear 4,816·
    # Mantle 57,213정점·삼각형 54,624=전체 63%)·재질 7개 전부 TEX_IMAGE 직결·좌우 대칭 99.6%·
    # 크기 2.621×1.459×2.885(단위 아님→키 1.8)·팔을 거의 수평으로 벌린 자세(T자에 가까움).
    # 감량은 Mantle 위주(목표 3~4만), 0.3 밑으로 내리면 bone heat 전체가 죽는다(하나미 교훈).
    # 직접 확인: 삼각형 합 86,182 정확히 일치, 전 메시 UV 1층뿐. Body는 팔 부분만 있고
    # (z 56.6~91.6%) 몸통 아래는 Wear가 전체(0~88.9%) 담당 → body_mesh_name="Object_8".
    "전설적인_홍인창": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_홍인창.glb",
        path="Assets/Art/Units/전설적인_홍인창/전설적인_홍인창.fbx",
        mesh_name="Corazon",
        height=1.8,
        body_mesh_name="Object_8",
        center_band=(0.05, 0.15),
        joints=dict(Hips=(0, 0, 0.450), Spine=(0, 0, 0.500), Spine1=(0, 0, 0.550), Spine2=(0, 0, 0.600), Neck=(0, 0, 0.780),
                    Head=(0, 0, 0.840), HeadTop=(0, 0, 0.970),
                    Shoulder=(0.052, 0.02, 0.610), Arm=(0.173, 0.02, 0.610), ForeArm=(0.312, 0.02, 0.610), Hand=(0.381, 0, 0.590),
                    HandTip=(0.416, 0, 0.580),
                    UpLeg=(0.070, 0, 0.420), Leg=(0.070, 0, 0.220), Foot=(0.070, 0, 0.030), ToeBase=(0.070, -0.050, 0.010),
                    ToeTip=(0.070, -0.090, 0.010)),
        # 🔴 실측 발견 — 감량 비율과 무관하게(원본 그대로도 실패) Mantle(깃털 코트)이 껴 있으면
        # Bone Heat Weighting이 전체(가중치 있는 정점 0개)로 죽는다(하나미와 증상은 같지만
        # 원인이 다름 — 감량 문제가 아니라 Mantle을 빼면 바로 통과한다, 직접 실측으로 격리
        # 확인). 깃털이 잘게 쪼개진 조각들이라 메시 그래프가 심하게 끊겨 있어 열 확산 계산
        # 자체가 안 되는 것으로 추정 — 코트라 몸통을 그대로 따라가면 충분하다고 보고 강체로
        # 뺀다(카스미 Cabbard와 같은 원칙).
        rigid={"MI_N127_E001_Mantle_CS01": "Spine2"},
        alpha_keep=set(),
        textures={
            "MI_N127_E001_Body_CS01": [("Base Color", "baseColorTexture", "Body_diffuse.png")],
            "MI_N127_E001_Face_CS01": [("Base Color", "baseColorTexture", "Face_diffuse.png")],
            "MI_N127_E001_Hair_CS01": [("Base Color", "baseColorTexture", "Hair_diffuse.png")],
            "MI_N127_E001_Iris_CS01": [("Base Color", "baseColorTexture", "Iris_diffuse.png")],
            "MI_N127_E001_Mantle_CS01": [("Base Color", "baseColorTexture", "Mantle_diffuse.png")],
            "MI_N127_E001_Met_CS01": [("Base Color", "baseColorTexture", "Met_diffuse.png")],
            "MI_N127_E001_Wear_CS01": [("Base Color", "baseColorTexture", "Wear_diffuse.png")],
        },
        level_arms=True,
        # 🔴 decimate_rules는 오브젝트 "이름"으로 매치하는데, 이 소스는 전부 glTF 기본 이름
        # (Object_N)이라 "Mantle"이라는 문자열이 아예 없어 처음엔 감량이 하나도 안 먹었다
        # (직접 확인: 전 86,182 → 후 86,182, 그대로). Mantle의 실제 오브젝트 이름 Object_6으로.
        decimate_rules=[("Object_6", 0.3)],
        decimate_default=1.0,
    ),
    # 사쿠라(전설적인_진연서, 윤식파여장부) — PM 사양 원문 보존(2026-09-17):
    # 메시 20개 = 같은 몸 세 벌 겹침(Object_4/10/16 각 28,877정점 등 작은 부품까지 3회 반복,
    # 직접 확인 bbox 완전 동일 — 진짜 겹침, 다른 위치 아님) + 한 번만 있는 Object_22/23.
    # 삼각형 160,766 → 한 벌만 남기면 약 5.4만(직접 확인 55,030, 거의 일치).
    # 좌우 대칭 92.9% · A자(팔 높이 50~54%) · 크기 119×33×163 · 텍스처 3장 연결됨.
    # 🔴 Object_22(1,068정점, 머리 위로 뻗은 부속물 추정)·Object_23(96정점)이 몸(0~162 단위)과
    # 완전히 다른 단위로 들어와 있었다(직접 확인: 정점 좌표 자체가 0~1.8 스케일, 부모·자체
    # scale은 항등이라 데이터 자체의 문제) — 100배 해야 몸 단위와 맞고, 그러면 z 115.6~177.0
    # (몸 키 162.3보다 위로 솟음 — 머리 위 장식물, 아마 리본/땋은머리)로 위치가 앞뒤 맥락과
    # 맞는다. mesh_scale_override로 보정.
    "전설적인_진연서": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_진연서.glb",
        path="Assets/Art/Units/전설적인_진연서/전설적인_진연서.fbx",
        mesh_name="Sakura",
        height=1.8,
        body_mesh_name="Object_4",
        drop_meshes={"Object_10", "Object_11", "Object_12", "Object_13", "Object_14", "Object_15",
                     "Object_16", "Object_17", "Object_18", "Object_19", "Object_20", "Object_21"},
        mesh_scale_override={"Object_22": 100.0, "Object_23": 100.0},
        center_band=(0.05, 0.15),
        joints=dict(Hips=(0, 0, 0.30), Spine=(0, 0, 0.38), Spine1=(0, 0, 0.45), Spine2=(0, 0, 0.52), Neck=(0, 0, 0.78),
                    Head=(0, 0, 0.85), HeadTop=(0, 0, 0.98),
                    # 🔴 실측 발견 — 48% 높이엔 팔이 아예 없다가(x 16.6, 몸통뿐) 50%에서 갑자기
                    # 59.7로 튀고 위로 갈수록(52~70%) 서서히 좁아진다 — 팔이 이미 거의 수평으로
                    # 뻗어 있어 그 "높이"에서만 단면이 잡히는 것(A자가 아니라 이미 T자에 가까움).
                    # z를 그 실제 높이(50~51%)에 맞추고, ForeArm~Hand 사이에 실제 정점이 있는
                    # 구간만 좁게 잡아야 bone heat가 Hand 몫을 챙긴다(처음엔 Hand가 메시 맨 끝
                    # 정점이라 ForeArm에 다 뺏겨 가중치 0였다).
                    Shoulder=(0.10, 0, 0.60), Arm=(0.15, 0, 0.56), ForeArm=(0.20, 0, 0.52), Hand=(0.30, 0, 0.44),
                    HandTip=(0.42, 0, 0.38),
                    UpLeg=(0.061, 0, 0.30), Leg=(0.061, 0, 0.15), Foot=(0.061, 0, 0.02), ToeBase=(0.061, -0.049, 0.01),
                    ToeTip=(0.061, -0.092, 0.01)),
        rigid={},
        alpha_keep=set(),
        level_arms=True,
        # 🔴 bone heat가 성공하는 관절 배치 범위 안에서는 가로가 최대 1.3m대까지만 나온다
        # (직접 실측 — 더 벌리면 손 쪽에 정점이 없어 bone heat 자체가 죽는다). 기본 0.9는
        # 못 채워 PM 판단 대상으로 낮춰 둠(0.72 — 실측 1.301m/1.8m).
        tpose_width_ratio=0.7,
        decimate_rules=[],
        decimate_default=1.0,
    ),
    # 베가펑크(제한_이충민, One Piece 초기 모습) — PM 사양 원문 보존(2026-09-17):
    # ZBrush 2021 내보내기 · 정점 75,000 · 면 159,012(직접 확인 158,892, 삼각분할 방식 차이) ·
    # UV 0 · 법선 0 · 재질 0 · MTL·텍스처 없음(색 전혀 없는 회색 조각상).
    # 원시 크기 5.99×2.25×7.02(직접 확인 정확히 일치) · 좌우 대칭 98.7% · 이미 T자(팔 높이
    # 키의 60~70%) · 정면 −Y.
    # 형태(렌더 확인): 사과 달린 모자·부스스한 옆머리·콧수염·긴 혀가 배까지 늘어짐·짧은 흰
    # 가운·가는 다리·큰 부츠(옆에 안테나).
    # 🔴 색 입히기 필요 — 텍스처 없이 부위별 단색(paint_regions, 이번에 새로 추가한 기능):
    # 높이(z)·좌우폭(x)·앞뒤(y)로 첫 매치 우선 잘라 재질 슬롯을 만든다. 원작 색이 애매해
    # (렌더 확인은 했지만 정확한 색상값은 근거 없음) 무난한 근사색으로 넣고 PM 판단 요청.
    "제한_이충민": dict(
        source="~/Desktop/구랜디스킨모음/07_제한됨/제한_이충민.zip",
        source_type="obj_zip",
        obj_member="source/Vegapunk_75k.zip",
        inner_obj="Vegapunk_75k.obj",
        # ZBrush 내보내기가 100개 섬으로 쪼개져 있어(직접 확인) bone heat가 전체 실패한다 —
        # merge-by-distance로 붙인다(직접 실측: 0.05에서 1개+작은 조각 3개로 거의 다 붙음).
        weld_distance=0.05,
        path="Assets/Art/Units/제한_이충민/제한_이충민.fbx",
        mesh_name="Vegapunk",
        height=1.8,
        body_mesh_name="Vegapunk_75k",
        center_band=(0.05, 0.15),
        # 관절표: 원본 키 7.0175(zmin −3.549) 대비 비율. 단면 실측: 65~70% 높이에서 팔이
        # x=±2.99까지 벌어짐(이미 T자) · 42% 부근 허리 · 90~98% 머리 · 98%+ 모자.
        joints=dict(Hips=(0, 0, 0.42), Spine=(0, 0, 0.48), Spine1=(0, 0, 0.55), Spine2=(0, 0, 0.67), Neck=(0, 0, 0.87),
                    Head=(0, 0, 0.91), HeadTop=(0, 0, 0.98),
                    Shoulder=(0.08, 0, 0.67), Arm=(0.20, 0, 0.67), ForeArm=(0.32, 0, 0.67), Hand=(0.415, 0, 0.67),
                    HandTip=(0.44, 0, 0.67),
                    UpLeg=(0.10, 0, 0.40), Leg=(0.10, 0, 0.20), Foot=(0.10, 0, 0.03), ToeBase=(0.10, -0.05, 0.01),
                    ToeTip=(0.10, -0.10, 0.01)),
        rigid={},
        alpha_keep=set(),
        level_arms=True,
        # 이미 T자라(PM 확인) level_arms가 교정할 게 거의 없다 — 폭은 실제 팔 메시 범위
        # (원본 x=±2.99, 키 대비 0.426)로 정해져 관절표를 더 벌려도 안 바뀐다(직접 실측).
        # 0.848 실측 — 0.9 기본 기준 미달이라 유닛별로 낮춤.
        tpose_width_ratio=0.8,
        # 첫 매치 우선 — 좁은 예외(사과·혀·맨팔)를 넓은 부위(로브·머리)보다 앞에 둔다.
        paint_regions=[
            ("apple", (0.55, 0.05, 0.05, 1.0), lambda x, y, z: z > 3.35 and x > -0.05),
            ("tongue", (0.85, 0.45, 0.5, 1.0), lambda x, y, z: -0.8 <= z < 1.2 and y < -0.5),
            ("arm_skin", (0.85, 0.68, 0.58, 1.0), lambda x, y, z: 0.8 <= z < 2.4 and abs(x) > 0.55),
            ("hat", (0.12, 0.12, 0.14, 1.0), lambda x, y, z: z > 3.15),
            ("hair", (0.83, 0.81, 0.76, 1.0), lambda x, y, z: 2.4 <= z < 3.15 and y > 0.05),
            ("face_skin", (0.85, 0.68, 0.58, 1.0), lambda x, y, z: 2.4 <= z < 3.15 and y <= 0.05),
            ("robe", (0.90, 0.90, 0.88, 1.0), lambda x, y, z: -0.6 <= z < 2.4),
            ("leg_skin", (0.85, 0.68, 0.58, 1.0), lambda x, y, z: -3.0 <= z < -0.6),
            ("boots", (0.13, 0.09, 0.07, 1.0), lambda x, y, z: True),
        ],
        # 🔴 weld_distance=0.05 자체가 이미 75,000 → 약 10,155정점(섬을 붙이려면 이보다 작은
        # 거리로는 100개 섬이 안 붙는다, 직접 실측)으로 크게 줄인다 — 그 위에 추가로
        # decimate까지 걸면 과하게 뭉개진다(직접 겪음: 4,596정점까지 떨어짐). 감량은 weld
        # 하나로 끝낸다.
        decimate_rules=[],
        decimate_default=1.0,
    ),
    # 하나미(희귀함_고어진) — PM 사양 원문 보존(2번 유실됐던 지시, 다음 압축 대비 여기 그대로
    # 적어 둔다, 2026-09-16):
    # 목표 키 1.8m(흔함만 1.53, 나머지 전 등급 1.8) · 발 z 0 · 정면 −Y · T자 · mixamorig 이름 ·
    # 무가중치 정점 0. 팔은 있다("몸통형" 아님) — PM 실측(gltf Z업): 몸+머리+다리만 재면 좌우
    # 대칭 99.9%(Body만 100%). 팔 끝 높이 0.89~1.13(키 1.93의 46~58%) · 40~60% 높이대 폭 1.13 →
    # 팔이 몸 옆으로 비스듬히 내려온 A자(팔이 Body 메시 안에 통짜로 붙어 있어 따로 조각이 없을
    # 뿐). 높이대별 폭(전체 대비 %): 0%:0.58·10%:0.54·20~30%:0.56·40%:1.13·50%:1.11·60%:0.97·
    # 70%:0.75·80%:0.56·90%:0.20 → 팔은 60~80% 높이의 어깨에서 시작해 40~50% 높이에서 손끝.
    # 관절표는 이 단면 실측으로 잡고 level_arms로 T자로 편다.
    # 메시: Hanami_Body_0(4,156·6,892, x±0.56, z −0.03~1.73) · Hanami_Head_0(3,506·6,006,
    # z 1.62~1.90) · Hanami_Leg_0(1,842·3,030, z 0.13~1.12 — 겉 하의, Body와 같은 다리 사슬로
    # 자동가중치) · Hanami_Teeth_0(10,434·16,944 — 입 안인데 무거움, 크게 감량) ·
    # Leaf_leaf_0(17,896·34,464) · Leaf_flower_0(4,244·7,832) — 잎·꽃은 오른쪽 어깨에만(비대칭,
    # x +0.12~+0.35, z 1.53~1.74, 직접 확인) → 드롭 말고 오른쪽 어깨 뼈에 100% 강체(하나미
    # 상징), 크게 감량. 재질 6개 전부 Principled TEX_IMAGE 기본색 연결(흰 인형 위험 없음).
    # UV 층이 메시마다 2~3개 → join 전 통일 필요. 조명 구 없음. 전체 삼각형 75,168 → 목표 3~4만.
    # 🔴 좌우 라벨 주의 — 이 스크립트 관례(파일 맨 위 docstring)는 "+X = 캐릭터 왼쪽"인데 PM
    # 실측 보고는 잎·꽃 위치를 "오른쪽"이라 불렀다(뷰어 기준일 수 있음) — rigid_by_name의
    # "{side}"는 실제 빌드 시점 좌표의 x부호로 자동 판정하므로 이 라벨 혼동과 무관하게 항상
    # 맞게 붙는다(직접 하드코딩 안 함).
    "희귀함_고어진": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_고어진.glb",
        path="Assets/Art/Units/희귀함_고어진/희귀함_고어진.fbx",
        mesh_name="Hanami",
        height=1.8,
        uv_layers=1,
        # 정점 수로 자동 고르면 Leaf_leaf_0(17,896)이 Hanami_Body_0(4,156)보다 많아 잘못
        # 뽑힌다 — 이지원·주영호와 같은 함정, body_mesh_name으로 못박는다(직접 겪음: 처음
        # 돌렸을 때 center_band가 몸통이 아니라 잎에 걸려 빈 배열로 죽었다).
        body_mesh_name="Hanami_Body_0",
        center_band=(0.05, 0.15),                                        # 낮은 다리 높이대 — 좌우 중심 잡기
        # 🔴 실측 발견 — 관절표는 "원본 미터"가 아니라 키(원본 H) 대비 "비율"이다(bone_table이
        # 나중에 Vector(h)*Hf로 최종 키를 곱해 절대위치를 만든다 — 김민준 HeadTop=0.95, Shoulder
        # z=0.715처럼 전부 0~1 근방). 처음에 원본 실측 미터(z 1.05~1.87 등)를 그대로 넣었다가
        # 뼈가 전부 키의 몇 배 높이로 날아가 메시 밖에 놓였고, Bone Heat Weighting이 정점을
        # 하나도 못 찾아 kd_w가 빈 트리가 돼 "'>' not supported between NoneType and float"로
        # 죽었다 — 원본 z를 (z−바닥)/원본키(1.9319)로, x·y도 /원본키로 나눠 비율로 바꿔 해결.
        joints=dict(Hips=(0, 0, 0.577), Spine=(0, 0, 0.665), Spine1=(0, 0, 0.732), Spine2=(0, 0, 0.794), Neck=(0, 0, 0.872),
                    Head=(0, 0, 0.908), HeadTop=(0, 0, 0.986),
                    # PM 실측 단면(40~50% 높이=손끝, 60~80% 높이=어깨) 기반 — 팔이 몸 옆으로
                    # 비스듬히 내려온 A자, level_arms로 사후 T자.
                    Shoulder=(0.052, 0.026, 0.820), Arm=(0.129, 0.026, 0.794), ForeArm=(0.259, 0.016, 0.613), Hand=(0.290, 0, 0.484),
                    HandTip=(0.295, 0, 0.432),
                    UpLeg=(0.078, 0, 0.561), Leg=(0.078, 0, 0.302), Foot=(0.078, 0, 0.044), ToeBase=(0.078, -0.052, 0.018),
                    ToeTip=(0.078, -0.093, 0.018)),
        rigid={},
        alpha_keep=set(),
        level_arms=True,
        # 잎·꽃(하나미 상징) — 실루엣 왜곡보다 상징성이 중요하다고 PM이 명시, 드롭 말고 강체.
        # side_threshold=None → 항상 실제 x부호로 판정(중앙 예외 없음, 이미 한쪽에만 있어 필요없다).
        rigid_by_name=[("Leaf", "{side}Shoulder", None)],
        # 입 안(Teeth)과 잎(Leaf, 전체의 69%가 이 둘)이 무거워 감량, 몸통·머리·다리는 원본 유지
        # (실루엣·판정 부위). 🔴 실측 발견 — 0.08·0.15처럼 너무 세게 줄이면 표준 Decimate
        # COLLAPSE가 이 잘게 쪼개진 이빨/꽃잎 지오메트리를 퇴화 삼각형으로 뭉개, Bone Heat
        # Weighting이 전체(가중치 있는 정점 0개)로 실패해 kd_w가 빈 트리라 죽었다("'>' not
        # supported between NoneType and float"). 0.3까지만 줄이면 안정적으로 동작(직접 실측:
        # 감량 후 75,168→33,699, PM 목표 3~4만과 일치).
        decimate_rules=[("Teeth", 0.3), ("Leaf", 0.3), ("Body", 1.0), ("Head", 1.0), ("Leg", 1.0)],
        decimate_default=1.0,
    ),
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

    # 박기찬(료멘 스쿠나, Jujutsu Kaisen) — zip 속 zip(source/untitled.zip) 안 맨 OBJ
    # (Ryomen Sukuna.obj, 정점 14,856·면 28,156, 직접 확인) — usemtl로 재질 15개 이름은
    # 이미 붙어 있는데(MI_CP_020_00_*) mtllib(untitled.mtl)가 없어 임포트 직후엔 색이
    # 비어 있다. 베가펑크(재질 0개, paint_regions로 새로 만듦)와는 다른 경우라
    # material_textures(기존 재질 이름 → 텍스처 파일)로 새 갈래를 추가해 처리.
    # 텍스처 4장은 안쪽 zip이 아니라 바깥 zip 최상위 textures/에 있다(직접 확인):
    # Hair_C·Eyes_C·Face_C·Body_C — Iris용 별도 파일은 없어 Eyes_C를 공유.
    # Outline·Toonline·Decal·DecayDecal 4개는 외곽선/데칼 껍데기 재질이라(셀 교훈)
    # drop_materials로 그 재질을 쓰는 면부터 지운다(별 오브젝트가 아니라 재질로만
    # 갈려 있어 drop_meshes로는 못 뺀다).
    # 렌더로 직접 확인(오소그래픽 무재질 렌더) — 이미 T자, 소매가 넓게 늘어진 통옷(자락이
    # 발목까지) 한 벌에 다리가 안 갈린다(코라손·베가펑크와 같은 부류). 단면 실측:
    # 오른손끝 x=71.60(폭비 0.411) at z frac 0.488(허리 높이 — 넓은 소매가 손목까지 아래로
    # 처져 있다), 어깨선 x=20.8 at frac 0.82, 목~머리는 frac 0.84~0.99(폭 9~11 일정).
    # 최종 폭비 실측 0.822(0.9 기준 미달) — 늘어진 소매 특유 형태라 tpose_width_ratio로
    # 낮춤(베가펑크와 같은 사유).
    "초월_박기찬_AD": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_박기찬_AD.zip",
        source_type="obj_zip",
        obj_member="source/untitled.zip",
        inner_obj="Ryomen Sukuna.obj",
        drop_materials={"MI_CP_020_00_Outline", "MI_CP_020_00_Toonline", "MI_CP_020_00_Decal",
                         "MI_CP_020_00_DecayDecal"},
        material_textures={
            "MI_CP_020_00_Hair": "textures/T_CP_020_00_Hair_C.png",
            "MI_CP_020_00_Face": "textures/T_CP_020_00_Face_C.png",
            "MI_CP_020_00_Eyes": "textures/T_CP_020_00_Eyes_C.png",
            "MI_CP_020_00_Iris": "textures/T_CP_020_00_Eyes_C.png",
            "MI_CP_020_00_ClothesA": "textures/T_CP_020_00_Body_C.png",
            "MI_CP_020_00_ClothesB": "textures/T_CP_020_00_Body_C.png",
            "MI_CP_020_00_ClothesC": "textures/T_CP_020_00_Body_C.png",
            "MI_CP_020_00_Skin": "textures/T_CP_020_00_Body_C.png",
            "MI_CP_020_00_Leg": "textures/T_CP_020_00_Body_C.png",
            "MI_CP_020_00_ShoesA": "textures/T_CP_020_00_Body_C.png",
            "MI_CP_020_00_ShoesB": "textures/T_CP_020_00_Body_C.png",
        },
        path="Assets/Art/Units/초월_박기찬_AD/초월_박기찬_AD.fbx",
        mesh_name="Sukuna",
        height=1.8,
        center_band=(0.05, 0.15),
        # 관절표: 실측 프랙션(원본 키 174.26 대비). 팔은 어깨(0.82)에서 손(0.49)까지 늘어진
        # 소매를 그대로 따라 대각선으로 잡아야 bone heat가 소매를 제대로 갈라 먹는다 —
        # level_arms가 나중에 수평 T자로 편다.
        # 🔴 PM 실측 정정(2026-09-18, 유니티 idleview + 직접 뼈 좌표 재검사) — 처음 관절표는
        # 소매 바깥 천 윤곽(당겨 늘어진 실루엣)을 그대로 따라가 "Shoulder" 뼈 하나가 위팔
        # 전체를 먹고(0.41m) "Arm"(실제 어깨 관절)이 팔꿈치 근처(키 65%)에 잡혀 있었다 —
        # 유니티는 Shoulder를 거의 안 돌려서 위팔이 수평으로 남고 팔꿈치에서만 꺾이는
        # 허수아비 자세가 나왔다(롤 문제가 아니었다 — roll 수정은 결과를 안 바꿈, PM 확인).
        # 진짜 어깨 관절/팔꿈치/손목은 겉천보다 훨씬 안쪽(짧다) — PM이 표준 비율(위팔
        # ≈0.28m·아래팔≈0.25m)로 다시 잡은 좌표를 그대로 반영, 겉천(손끝까지 늘어진 소매
        # 자락)은 HandTip 몫으로 bone heat가 알아서 확산해 가져가게 둔다.
        joints=dict(Hips=(0, 0, 0.45), Spine=(0, 0, 0.52), Spine1=(0, 0, 0.60), Spine2=(0, 0, 0.68),
                    Neck=(0, 0, 0.83), Head=(0, 0, 0.87), HeadTop=(0, 0, 0.99),
                    # 🔴 재빌드 1차 시도 실패(직접 확인) — Hand·HandTip을 손목 실측 위치(0.23H)
                    # 그대로 짧게 두니 소매 끝(실제 천이 x=0.41H까지 나가는 부분)에 bone heat가
                    # 안착할 뼈가 없어져(전 뼈에서 멀어짐) 정점이 엉뚱한 뼈로 흩어지며 메시가
                    # 찢어졌다(렌더 직접 확인 — 팔 한쪽이 허공에 뜬 조각으로 떨어져 나감).
                    # Hand 뼈 자체(손목→HandTip)는 실제 손보다 길게 늘여 늘어진 소매 끝까지
                    # 닿게 둔다 — 이 캐릭터는 원래 손목 밖으로 천이 한참 늘어진 디자인이라
                    # Hand 하나가 손목+늘어진 자락을 함께 맡는 게 정상(다른 뼈로 새로 안 뺀다).
                    Shoulder=(0.03, 0, 0.83), Arm=(0.10, 0, 0.80), ForeArm=(0.20, 0, 0.68),
                    Hand=(0.30, 0, 0.58), HandTip=(0.41, 0, 0.49),
                    UpLeg=(0.07, 0, 0.43), Leg=(0.07, 0, 0.20), Foot=(0.07, 0, 0.02),
                    ToeBase=(0.07, -0.06, 0.01), ToeTip=(0.07, -0.12, 0.01)),
        rigid={},
        alpha_keep=set(),
        level_arms=True,
        # 폭 실측 0.822 — 늘어진 넓은 소매 특유 형태(베가펑크와 같은 사유로 낮춤).
        tpose_width_ratio=0.8,
    ),

    # 이태훈(스모커, One Piece 타임스킵 후) — 뼈 없는 단일 glb(코라손과 같은 소스 계열
    # MI_N118_*, PM 원문은 압축 중 유실돼 직접 재조사, SOURCE.txt 참고). 메시 7개(Fur·
    # Iris·Body·Face·Hair·Mantle·Wear) — Wear(19,286정점)가 몸통 전체를 담은 본체.
    # 삼각형 합 49,718로 이미 목표(3~5만) 안이라 감량 불필요.
    # 렌더로 직접 확인: 팔이 거의 T자, 발끝까지 오는 긴 코트 한 벌에 다리가 안 갈림(코라손과
    # 같은 부류) — Fur(털 옷깃)·Mantle(젖혀진 코트 자락)은 코라손 교훈대로 선제적으로
    # Spine2에 강체 바인딩(깃털/조각난 코트가 Bone Heat를 통째로 죽이는 전례).
    # 단면 실측(원본 키 1.9575 기준): 손끝 x=0.846(폭비 0.432) at frac 0.587, 어깨선
    # x=0.365~0.40 at frac 0.80~0.82 — 코라손처럼 소매가 어깨보다 살짝 처져 있다.
    "초월_이태훈_AP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_이태훈_AP.glb",
        path="Assets/Art/Units/초월_이태훈_AP/초월_이태훈_AP.fbx",
        mesh_name="Smoker",
        height=1.8,
        body_mesh_name="Object_8",
        center_band=(0.05, 0.15),
        # 🔴 스쿠나에서 배운 교훈 그대로 선반영 — 처음엔 겉천(코트 소매) 윤곽을 그대로 따라
        # 4등분해서 "Shoulder"가 위팔 몫을 다 먹고 "Arm"(실제 어깨 관절)이 팔꿈치 근처에
        # 잡히는 실수를 할 뻔했다. Shoulder는 목 옆 짧게, Arm=실제 어깨 관절(겉천 단면에서
        # 잡은 원래 "Shoulder" 자리), ForeArm=팔꿈치(표준 위팔 길이 비율로 보간), Hand=손목,
        # HandTip만 원래 소매 끝(겉천 실측 자리)까지 길게 늘여 bone heat가 늘어진 소매 끝에
        # 안착할 자리를 준다.
        joints=dict(Hips=(0, 0, 0.44), Spine=(0, 0, 0.50), Spine1=(0, 0, 0.56), Spine2=(0, 0, 0.62),
                    Neck=(0, 0, 0.87), Head=(0, 0, 0.91), HeadTop=(0, 0, 0.99),
                    Shoulder=(0.03, 0, 0.85), Arm=(0.19, 0, 0.82), ForeArm=(0.30, 0, 0.71),
                    Hand=(0.40, 0, 0.62), HandTip=(0.46, 0, 0.56),
                    UpLeg=(0.06, 0, 0.42), Leg=(0.06, 0, 0.20), Foot=(0.06, 0, 0.02),
                    ToeBase=(0.06, -0.06, 0.01), ToeTip=(0.06, -0.12, 0.01)),
        # 🔴 실측 발견 — 실패 원인은 Fur가 아니라 소스 자체가 748개 섬(가장 큰 게 4.5%뿐,
        # 재질 경계마다 중복 정점이 나는 소스로 추정)으로 쪼개져 있던 것 — rigid를 아예
        # 비워도 실패가 그대로였다(직접 격리 확인). weld_distance로 고쳤다.
        # 🔴 유니티 반려(PM) — Fur를 Spine2에 강체로 고정하니 목깃이 어깨/소매(Wear, 팔을
        # 따라 움직임)와 뜯어져 팔이 조금만 움직여도 목깃 조각이 공중에 뚝 떨어져 보였다
        # (직접 확인: Arm/Hand 가중치를 받은 정점은 전부 Wear·Body 재질이었지 Fur가 아니었다
        # — Fur 강체 자체는 "제대로" 작동했다, 문제는 강체를 건 정책 자체). Fur는 목~어깨에
        # 걸쳐 있어 한 뼈로 통째로 얼리면 안 맞는다 — weld 수정으로 섬 문제가 없어졌으니
        # 이제 강체를 빼고 다시 bone heat에 맡겨 Neck/Spine2/Shoulder에 자연스럽게 걸치게
        # 한다(마유리·가프처럼 조각이 아니라 "몸에 붙은 옷깃" 부류로 재분류).
        rigid={"MI_N118_E002_Mantle_CS01": "Spine2"},
        # 🔴 실측 발견 — 748개 섬(가장 큰 게 4.5%)이라 기본 이음새 거리(1e-4)로는 274개로만
        # 줄고 본 히트가 전부 실패(weighted 정점 0개, 베가펑크와 같은 증상이나 원인은 재질
        # 경계 중복 정점으로 추정). 0.005에서 97%가 한 섬으로 붙는 것을 직접 실측 확인.
        weld_distance=0.005,
        alpha_keep=set(),
        textures={
            "MI_N118_E001_Fur_CS01": [("Base Color", "baseColorTexture", "Fur_diffuse.png")],
            "MI_N118_E001_Iris_CS01": [("Base Color", "baseColorTexture", "Iris_diffuse.png")],
            "MI_N118_E002_Body_CS01": [("Base Color", "baseColorTexture", "Body_diffuse.png")],
            "MI_N118_E002_Face_CS01": [("Base Color", "baseColorTexture", "Face_diffuse.png")],
            "MI_N118_E002_Hair_CS01": [("Base Color", "baseColorTexture", "Hair_diffuse.png")],
            "MI_N118_E002_Mantle_CS01": [("Base Color", "baseColorTexture", "Mantle_diffuse.png")],
            "MI_N118_E002_Wear_CS01": [("Base Color", "baseColorTexture", "Wear_diffuse.png")],
        },
        level_arms=True,
        # 폭 실측 0.865(0.9 기준 살짝 미달) — 코라손과 같은 처진 소매 형태.
        tpose_width_ratio=0.85,
        decimate_rules=[],
        decimate_default=1.0,
    ),
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
    elif cfg.get("source_type") == "obj_zip":
        # ── zip(속 zip) 안 OBJ, 재질·UV·법선 전부 없는 조각상(베가펑크) — 부위별로 직접
        # 색을 칠한다. cfg["paint_regions"] = [(이름, RGBA, predicate(x,y,z)), ...] 순서대로
        # 첫 매치 우선(겹치면 앞 항목이 이긴다 — 혀·사과처럼 좁은 예외를 넓은 부위보다 먼저 둘 것).
        extract_dir = tempfile.mkdtemp(prefix="skinobj_")
        with zipfile.ZipFile(src) as z:
            z.extractall(extract_dir)
        obj_path = os.path.join(extract_dir, cfg["obj_member"])
        if obj_path.lower().endswith(".zip"):
            inner_dir = os.path.join(extract_dir, "inner")
            with zipfile.ZipFile(obj_path) as z:
                z.extractall(inner_dir)
            obj_path = os.path.join(inner_dir, cfg["inner_obj"])
        bpy.ops.wm.obj_import(filepath=obj_path)
        scene = bpy.context.scene
        body_obj = next(o for o in scene.objects if o.type == "MESH")
        # 🔴 실측 발견(베가펑크) — ZBrush 내보내기가 조각을 100개 섬(가장 큰 게 전체 정점의
        # 6.7%뿐)으로 안 붙인 채 내보냈다. Bone Heat Weighting은 메시가 하나로 이어져 있어야
        # 열 확산을 풀 수 있는데, 섬이 이 정도로 쪼개져 있으면 전체(가중치 있는 정점 0개)로
        # 죽는다(하나미·코라손과 증상은 같지만 원인은 처음 보는 종류 — 직접 실측으로 격리
        # 확인: merge-by-distance 1e-4(원래 이음새 붙이기 거리)로는 섬 100→96개뿐, 0.05로는
        # 1개(97%)+작은 조각 3개로 거의 다 붙는다). cfg["weld_distance"]로 유닛별 조정 가능.
        weld = cfg.get("weld_distance")
        if weld:
            import bmesh as _bmesh
            _bm = _bmesh.new()
            _bm.from_mesh(body_obj.data)
            _bmesh.ops.remove_doubles(_bm, verts=_bm.verts, dist=weld)
            _bm.to_mesh(body_obj.data)
            _bm.free()
            body_obj.data.update()
        # 🔴 스쿠나 — 이 소스는 재질·UV 자체는 있다(usemtl로 면마다 이름 붙어 있음, mtl 파일만
        # 없어 임포트 직후엔 색이 비어 있다). paint_regions(베가펑크, 재질 자체가 0)와는
        # 다른 경우라 cfg["material_textures"](기존 재질 이름 → 텍스처 멤버 키)로 처리하는
        # 갈래를 새로 추가. cfg["drop_materials"]가 있으면 그 재질을 쓰는 면부터 지운다
        # (Outline·Toonline류 외곽선/데칼 껍데기 — 별도 오브젝트가 아니라 재질로만 갈려
        # 있어 drop_meshes로는 못 뺀다).
        if cfg.get("drop_materials"):
            drop_idx = {i for i, m in enumerate(body_obj.data.materials) if m and m.name in cfg["drop_materials"]}
            bm = bmesh.new()
            bm.from_mesh(body_obj.data)
            bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in drop_idx], context="FACES")
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
            bm.to_mesh(body_obj.data)
            bm.free()
            # 면을 지워도 재질 슬롯 자체는 남아 있어 이후 mat_image[m.name] 조회에서
            # KeyError가 난다 — 슬롯째로 제거(뒤에서부터 pop해 인덱스 밀림 방지).
            for i in sorted(drop_idx, reverse=True):
                body_obj.data.materials.pop(index=i)
        if cfg.get("material_textures"):
            # material_textures = {재질이름: extract_dir 기준 상대경로} — 값(경로)이 같으면
            # 같은 파일로 묶여 한 번만 복사(스쿠나의 Body 아틀라스 하나를 여러 재질이 공유).
            os.makedirs(tex_dir, exist_ok=True)
            mat_image = {}
            written = {}
            for mat_name, rel_path in cfg["material_textures"].items():
                m = bpy.data.materials.get(mat_name)
                if m is None:
                    continue
                fname = written.get(rel_path)
                if fname is None:
                    src_path = os.path.join(extract_dir, rel_path)
                    fname = os.path.basename(src_path)
                    dst_path = os.path.join(tex_dir, fname)
                    if not os.path.exists(dst_path):
                        shutil.copy(src_path, dst_path)
                    written[rel_path] = fname
                mat_image[m.name] = [("Base Color", fname)]
            report["재질→텍스처"] = mat_image
            cfg = dict(cfg, textures=True)
        else:
            regions = cfg["paint_regions"]
            mat_idx = {}
            for rname, rgba, _ in regions:
                m = bpy.data.materials.new(rname)
                m.use_nodes = True
                body_obj.data.materials.append(m)
                mat_idx[rname] = len(body_obj.data.materials) - 1
            Mw = body_obj.matrix_world
            vw = [Mw @ v.co for v in body_obj.data.vertices]
            for p in body_obj.data.polygons:
                n = len(p.vertices)
                cx = sum(vw[vi].x for vi in p.vertices) / n
                cy = sum(vw[vi].y for vi in p.vertices) / n
                cz = sum(vw[vi].z for vi in p.vertices) / n
                for rname, rgba, pred in regions:
                    if pred(cx, cy, cz):
                        p.material_index = mat_idx[rname]
                        break
            os.makedirs(tex_dir, exist_ok=True)
            mat_image = {}
            for rname, rgba, _ in regions:
                fname = f"{rname}_solid.png"
                write_solid_png(os.path.join(tex_dir, fname), rgba)
                mat_image[rname] = [("Base Color", fname)]
            report["재질→텍스처"] = mat_image
            cfg = dict(cfg, textures=True)
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

    # cfg["drop_meshes"] — 이름으로 잡동사니·중복 조각을 뺀다(사쿠라: 몸 세 벌이 완전히 겹쳐
    # 있어 한 벌만 남기고 나머지 둘은 뺀다, 직접 확인 bbox 완전 동일).
    meshes = [o for o in scene.objects if o.type == "MESH" and o.name not in cfg.get("drop_meshes", set())]
    report["뺀 메시"] = sorted(cfg.get("drop_meshes", set()))
    # cfg["mesh_scale_override"] — {오브젝트이름: 배율}로 단위가 다른 조각을 몸통 단위에 맞춘다
    # (사쿠라: Object_22·23이 몸(0~162 단위)과 다른 단위(0~1.8, 100배 작음)로 들어와 있었다,
    # 직접 확인 — 부모·자체 scale은 항등인데 정점 좌표 자체의 스케일이 다름).
    scale_override = cfg.get("mesh_scale_override", {})
    Rz = Matrix.Rotation(math.radians(cfg.get("rotate_z", 0.0)), 4, "Z")
    world = {}
    for o in meshes:
        s = scale_override.get(o.name, 1.0)
        world[o.name] = np.array([Rz @ o.matrix_world @ (v.co * s) for v in o.data.vertices])
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
        s = scale_override.get(o.name)
        if s:
            o.data.transform(Matrix.Scale(s, 4))
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
    # 🔴 실측 발견(스모커) — 기본 이음새 거리(1e-4)로는 748개 섬(가장 큰 게 4.5%뿐)이 274개
    # 로만 줄고 절반은 여전히 안 이어져 본 히트가 전부 실패한다(weighted 정점 0개, 베가펑크와
    # 같은 증상). 재질 경계마다 중복 정점이 많이 나는 소스로 보임 — cfg["weld_distance"]로
    # 유닛별로 키울 수 있게 연다(0.005에서 97%가 한 섬으로 붙는 것을 직접 실측 확인).
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=cfg.get("weld_distance", 1e-4))
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
    # 🔴 사쿠라(전설적인_진연서) — 이 캐릭터는 실제 팔(손끝) 메시가 몸통 대비 짧아서(직접 실측:
    # bone heat가 성공하는 관절 배치로는 가로가 아무리 조정해도 1.2~1.3m를 못 넘었다 — 손을
    # 더 벌리면 그 자리에 정점이 없어 bone heat 자체가 죽는다) 이 안전선을 못 채운다. PM 판단
    # 대상으로 cfg["tpose_width_ratio"]로 유닛별 하한을 낮출 수 있게 열어 둔다(기본 0.9 그대로).
    min_ratio = cfg.get("tpose_width_ratio", 0.9)
    assert size[0] >= Hf * min_ratio, (
        f"{name}: 쉬는 자세 가로 {size[0]:.3f}m가 키 {Hf}m의 {min_ratio}배 미만 — 팔이 수평 T자가 아니다"
        f"(유니티 Idle 리타게팅에서 팔이 들리고 옷이 풍선처럼 부푼다, 베르고 교훈). "
        f"level_arms=True를 켜거나 joints의 Arm/ForeArm/Hand z를 Shoulder와 맞출 것.")

    # 🔴 실측 발견(사쿠라) — 중복 메시(Object_10~21)를 드롭해도 그 오브젝트들이 쓰던 재질
    # 데이터블록(material_6~17)이 파일에 남아 FBX로 같이 나간다(직접 확인: use_selection=
    # False라 장면 오브젝트 타입만 보는 게 아니라 bpy.data.materials 전체를 씀. orphans_purge
    # 로는 안 지워짐 — glTF 임포트가 재질에 use_fake_user를 걸어 두는 것으로 추정). 최종
    # body가 실제로 쓰는 재질 이름 목록 밖의 것들을 직접 지운다.
    used_mat_names = {sl.material.name for sl in body.material_slots if sl.material}
    for m in list(bpy.data.materials):
        if m.name not in used_mat_names:
            m.use_fake_user = False
            bpy.data.materials.remove(m)
    bpy.data.orphans_purge(do_local_ids=True, do_recursive=True)
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
    # 🔴 실측 발견(스쿠나, 구현담당2가 gen_biped_skin.py에서 "디오"로 먼저 찾은 것과 같은
    # 버그) — rotation_difference 기반 스윙 교정은 방향(swing)만 고치고 roll(자기 축
    # 비틀림)은 armature_apply가 그 순간의 아무 값으로 굳혀 버린다. bind=rest라 이 시점엔
    # 눈에 안 보이지만(변형이 항등) 실제 애니메이션(유니티 Idle 리타게팅)에서 팔이 엉뚱한
    # 축으로 굽는다 — 코라손·베가펑크는 스윙 각도가 작아(0~수 도) 티가 안 났고, 스쿠나는
    # 52°나 돌아 확 드러났다. armature_apply 직후 EDIT 모드로 다시 들어가 뼈 생성 때와
    # 같은 규칙(817행)으로 align_roll을 다시 먹인다 — bind=rest 상태라 roll을 바꿔도
    # 이 시점 메시엔 변화가 없다(위 drift assert가 이미 통과한 뒤).
    bpy.ops.object.mode_set(mode="EDIT")
    for side in ("Left", "Right"):
        for bone in ("Arm", "ForeArm", "Hand"):
            eb = arm.data.edit_bones[PREFIX + side + bone]
            d = (eb.tail - eb.head).normalized()
            eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
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
