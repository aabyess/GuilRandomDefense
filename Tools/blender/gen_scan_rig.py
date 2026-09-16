"""스캔 리깅 — 뼈 없는 좌우 비대칭 걷는 자세 glb를 mixamorig 사람형 FBX로(2026-09-14 첫 건
특별함_최동준). PM 2차 시도로 blender 세션에 인계(2026-09-15, 구현담당2 손 뗌) — 가중치 방식만
바꾸면 된다: T자 자체(관절 배치·팔다리 굽기·발 접지)는 PM 확인대로 좋음, 문제는 팔 45°에서
어깨가 조각나고 셔츠가 두 겹으로 갈라지는 것(옷·몸이 한 메시라 헐렁한 옷단이 관절 경계에 걸침).
  · JOINTS_CM(관절 좌표표, 실측값): 아래, import 직후.
  · straighten_limbs()(걷는 자세→T자 굽히기): 이 함수.
  · bone_weights()(지금 손봐야 할 가중치 함수 — geodesic 최근접+이웃평균, 이게 찢어짐의 원인):
    이 함수, docstring에 시도 이력(bone heat 실패→직선거리 실패→geodesic로 봉합했지만 부족)
    적어 뒀다.
  · build()의 "가중치" 섹션에서 bone_weights() 호출.
  산출물(FBX·Textures·렌더)은 /tmp/scan_rig에 그대로 있음 — Assets/엔 아직 안 넣었다(커밋은 PM).
  blender -b --factory-startup --python Tools/blender/gen_scan_rig.py -- [--out DIR] [--render DIR]

왜 gen_skin_rig.py를 그대로 못 쓰나 — 그 파일의 joints 표는 한 벌의 (x,y,z)만 받아 Left/Right를
x부호만 뒤집어 대칭으로 만들고(bone_table 참고), level_arms()도 팔 세 뼈만 한 축으로 돌려 수평으로
펴는 좁은 용도다. 이 원본(~/Desktop/구랜디스킨모음/03_특별함/특별함_최동준.glb)은 파일명
그대로 "걷는 도중" 스캔이라 좌우가 전혀 다른 자세(한쪽 발 앞·반대쪽 뒤, 팔도 한쪽은 팔꿈치 굽혀
가슴 쪽으로·반대쪽은 뒤로 스윙)라 이 구조로는 표현이 안 된다(구현담당1이 확인하고 멈춘 지점 —
~/Desktop/구랜디스킨모음/03_특별함/특별함_최동준_mixamo업로드_설명.txt 참고). 그래서 이 파일은 (1) 관절을 왼쪽/오른쪽
따로 갖는 JOINTS_CM 표, (2) 팔다리 네 사슬 전부(어깨~손·엉덩이~발끝)를 각각 T자로 펴는 일반화된
straighten_limbs()를 새로 짠다. 내보내기 설정·판정 렌더는 gen_skin_rig.py와 같은 방식이라 그
파일에서 그대로 가져와 쓴다(읽기 전용 import — 그 파일 자체는 고치지 않는다).

■ 가중치는 gen_skin_rig.py와 다른 방식을 쓴다 — 왜
처음엔 그 파일과 똑같이 `bpy.ops.object.parent_set(type="ARMATURE_AUTO")`(뼈 열 자동가중치)를
그대로 썼는데, 이 메시에서 대부분의 뼈(척추 6개·팔 8개·LeftUpLeg·LeftFoot)가 가중치 정점 0개로
나오고 RightLeg 한 뼈에 정점 51,349개 중 49,999개(전체의 97%)가 몰리는 실패를 실측으로 확인했다
(관절 좌표 자체는 멀쩡함 — 각 관절과 가장 가까운 메시 정점까지 거리가 2~10cm로 정상 범위였다).
경고 로그도 없이 조용히 실패해 원인을 좁히기 어려웠고(열 확산 솔버가 특정 뼈 근처에서 왜 막히는지
후속 조사 없이는 알 수 없음), 이 스캔 하나에 그 원인을 더 파는 대신 직접 검증 가능한 방식으로
바꿨다. 처음엔 정점마다 가장 가까운 뼈마디(선분) 3개를 3D 직선거리로 섞었는데, 이번엔 T자 렌더에서
셔츠 밑단이 왼손 쪽으로 길게 끌려가는 걸 확인했다 — 걷는 자세에서 손이 가슴 쪽으로 굽어 허리
옷단과 「직선거리로는」 가깝지만 몸 표면을 따라가면(지오데식) 훨씬 멀어서 생긴 오배정이다. 그래서
최종적으로 지오데식(표면을 따라가는) 최근접 배정으로 바꿨다: 뼈마디를 따라 표본점을 찍어 가장
가까운 정점을 씨앗 삼고, 여러 씨앗에서 동시에 다익스트라를 돌려 정점마다 표면 거리로 가장 가까운
뼈를 구한 뒤, 그 딱딱한 경계를 정점그룹에서 이웃과 몇 번 평균 내 부드럽게 섞는다(bone_weights()
참고). 이음새 붙이기(가중치 되옮김용 웰드 사본)도 이 방식에선 필요 없다 — 메시 자체의 변(edge)을
그래프로 쓰므로 위치가 아니라 실제 표면 연결을 따라간다.

■ 관절 좌표를 어떻게 쟀나 (JOINTS_CM 값의 출처)
원본 메시(51,349정점, cm 단위, Z 위, 이미 −Y를 보고 서 있음 — raw_from_negY.png로 확인, rotate_z
불필요)를 스크래치 스크립트(analyze.py/analyze2.py, 이 파일엔 없음)로 높이별로 얇게 썰어, 각 층의
XY 점을 그리드 유니온파인드로 뭉치 지었다. 뭉치가 하나면(허리·가슴처럼 몸통이 안 갈라지는 높이)
그 중심을 척추 관절로, 뭉치가 여럿이면(팔이 몸통에서 떨어지는 높이·다리가 갈라지는 높이) 가장 큰
중앙 뭉치(몸통)를 빼고 남는 뭉치들을 팔/다리로 읽었다. 왼쪽/오른쪽 이름은 이 스캔에서 실제로
어느 쪽이 어떻게 움직였는지를 그대로 따른 것뿐이다(자세가 좌우대칭 보행 규범을 따를 필요는 없다
— 이 스캔은 왼팔이 앞으로 굽어 있고 왼다리도 앞으로 나가 있는, 생체역학적으로는 「같은 쪽」이
같이 나간 자세였다. 실측으로 그렇게 나왔으니 그대로 쓴다). 숫자는 원본 스캔의 월드 좌표(cm)
그대로이며, 아래 build()가 메시와 똑같은 변환(G = 스케일 × 중심맞춤)을 관절에도 적용해 메시와
어긋나지 않게 한다.
"""
import bmesh
import hashlib
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gen_skin_rig import glb, image_bytes, rebuild_material  # noqa: E402 — 읽기 전용 재사용
from gen_skin_rig import judge, POSES  # noqa: E402 — 판정 렌더도 그대로 재사용(뼈 이름이 같다)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
PREFIX = "mixamorig:"

# 유닛 표(2026-09-15 blender — 두 번째 유닛 특별함_강주혁을 받으며 표로 바꿈). 관절 좌표는 원본 단위·rotate_z 적용 뒤 좌표계.
#   center_band: 발목 높이(원본 키 비율) — 두 발 사이로 원점 · decimate: 균등 감량 비율 · textures: (소켓, glb 이미지 번호, 파일 이름)

# ── 관절(원본 스캔 cm, 월드 좌표) — 위 docstring 참고. z=바닥 기준 실측 높이, x=+왼쪽/−오른쪽, y=−앞/+뒤.
# 🔴 2차(blender, 2026-09-15) 윗몸 다시 잼 — 1차 표는 위팔 관절이 x ±10·z 123(가슴 속, 실제보다 8cm 안·11cm 아래), 목이 z 131이라
#   Neck 뼈가 윗가슴 정점 5,567개를 가져가고 겨드랑이 옷이 들렸다. 높이 3cm 단면(joint_slices2/3): 어깨 꼭대기 z 139·삼각근 바깥 x ±23~24,
#   왼위팔 중심선 (18, 0, 118~135)·오른위팔 (−18.5, 6~10, 117~135), 왼아래팔은 z 109~117에서 앞으로(팔꿈치 (19.8, −10.8, 109)),
#   오른팔은 뒤로 스윙(z 106~112에서 (−23, 19.6)), 목 좁아짐 z 142~148·머리 z 150부터. 다리(무릎 z 49·발목 z 8)는 1차 값이 단면과 맞아 그대로.
JOINTS_CM = dict(
    Hips=(-3, 6, 84), Spine=(-1, 4, 95), Spine1=(-2, 5, 110), Spine2=(0, 3, 128),
    Neck=(-1, 3, 140), Head=(-1, -3, 150), HeadTop=(0, -3, 168),
    LeftShoulder=(4, 1, 137), LeftArm=(17.5, -0.5, 135), LeftForeArm=(19.5, -8.5, 109.5),
    LeftHand=(8.5, -31.5, 117), LeftHandTip=(5, -37, 119),
    RightShoulder=(-4, 2, 137), RightArm=(-18.5, 6, 135), RightForeArm=(-23, 19.5, 107),
    RightHand=(-24.6, 14.2, 90), RightHandTip=(-25.6, 12.1, 81.3),
    LeftUpLeg=(9, 0, 84), LeftLeg=(4.4, -1.5, 50.4), LeftFoot=(0.6, -0.8, 8),
    LeftToeBase=(1, -10.8, 3), LeftToeTip=(1, -20.8, 2),
    RightUpLeg=(-9, 0, 84), RightLeg=(-6.7, 17.1, 50.4), RightFoot=(0.3, 34.6, 8),
    RightToeBase=(0, 24.6, 3), RightToeTip=(0, 14.6, 2),
)

UNITS = {
    "특별함_최동준": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/03_특별함/특별함_최동준.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=0.4, rotate_z=0.0, joints=JOINTS_CM,
        textures=[("Base Color", 0, "default_baseColor.jpg"), ("Normal", 1, "default_normal.png")]),
    # 원피스 피규어 베르고(뼈 없는 glb, 28cm, 긴 누비 코트·팔을 코트 옆에 내린 선 자세·다리 벌림) — 2026-09-15 PM 인계: 구현담당1이 gen_skin_rig.py로 1차 리깅했지만
    #   유니티에서 쉬는 가로 1.05 m(팔 뼈가 허리 높이 z 1.12에서 수평, Shoulder 뼈가 수직 아래)·Idle에서 코트 풍선·자락 훌라후프 → 원본부터 다시(좌우 따로 관절표).
    #   관절은 원본 좌표 1% 단면(스크래치 vergo/arm_slices.py): 왼팔(+X)은 z 0.117~0.19에서 몸과 떨어져 x 0.041·y −0.366, 오른팔은 z 0.14~0.19 코트에 붙고 더 뒤(y −0.350),
    #   겨드랑이 z 0.192 · 코트 밑단 z 0.044(다리는 그 밑에서 둘로) · 발목 x +0.040/−0.023(벌린 자세). 뼈 없는 원본의 오브젝트 이동(g0_0)은 G가 같이 굽는다.
    "특별함_최준우": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/03_특별함/특별함_최준우.glb"), mesh_name="Vergo",
        height=1.8, center_band=(0.30, 0.40), decimate=0.52, rotate_z=0.0,
        joints=dict(
            Hips=(0.002, -0.360, 0.1417), Spine=(0.002, -0.360, 0.1613), Spine1=(0.002, -0.361, 0.181), Spine2=(0.002, -0.362, 0.2005),
            Neck=(0.002, -0.366, 0.2257), Head=(0.002, -0.370, 0.2411), HeadTop=(0.002, -0.370, 0.2733),
            LeftShoulder=(0.008, -0.362, 0.208), LeftArm=(0.029, -0.362, 0.206), LeftForeArm=(0.0405, -0.364, 0.1711),
            LeftHand=(0.0433, -0.3676, 0.1375), LeftHandTip=(0.043, -0.371, 0.1166),
            # 🔴 오른팔은 2회차까지 소매 앞면(y가 소매 반지름만큼 앞)을 지나 bone heat가 아래 소매를 Shoulder·Spine에 줬다 → 소매 바깥 끝 x−반지름·앞뒤 끝 가운데로 다시 잼(vergo/sleeve.py)
            RightShoulder=(-0.004, -0.360, 0.208), RightArm=(-0.024, -0.357, 0.206), RightForeArm=(-0.0305, -0.3435, 0.1711),
            RightHand=(-0.0343, -0.3495, 0.1375), RightHandTip=(-0.029, -0.360, 0.1166),
            LeftUpLeg=(0.016, -0.360, 0.1417), LeftLeg=(0.029, -0.357, 0.0718), LeftFoot=(0.040, -0.352, 0.013),
            LeftToeBase=(0.050, -0.372, 0.003), LeftToeTip=(0.058, -0.388, 0.001),
            RightUpLeg=(-0.012, -0.360, 0.1417), RightLeg=(-0.018, -0.357, 0.0718), RightFoot=(-0.023, -0.352, 0.013),
            RightToeBase=(-0.035, -0.372, 0.003), RightToeTip=(-0.045, -0.388, 0.001)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        # 🔸 6회차: 캡슐(0.009·0.0095·0.007, 여유 0.003)이 부푼 누비 소매 겉까지 잘라 오른팔 가중치 정점 4,746→2,369 — 코트는 가름·법선 마스크가 막으니 소매 전체가 들어가게 넓힘
        arm_capsule=dict(radius_src=(0.013, 0.013, 0.010), margin_src=0.004),
        # 🔴 1회차: 오른팔(코트에 붙음)이 T자에서 코트 옆판을 커튼처럼 끌고 나옴. 2회차 표면 거리 마스크는 소매·코트가 표면째 붙어 효과 0 →
        #   법선이 팔 축을 향한 정점(소매 옆 코트 옆판)은 팔 가중치 빼기
        arm_facing=dict(lo=-0.1, hi=0.2, smooth=2),
        # 🔴 5회차: 팔 가중치 채우기(assign)는 경계 찢김만 키움(Idle 17배) → 6회차: bone heat 전에 오른 소매·코트 붙은 띠(원본 z 0.136~0.192) 면을 가름
        arm_split=dict(sides=("Right",), radius_src=0.012, facing=0.45, z_src=(0.136, 0.192)),
        # 코트 자락(엉덩이 z 0.1417 ~ 밑단 0.044): 팔 가중치는 그대로 두고 나머지를 Hips→좌우 UpLeg로(아래로 갈수록 넓적다리 몫) — 종아리·어깨에 안 실리게
        coat_hem=dict(hip_src=0.1417, hem_src=0.044, band_src=0.012, center_x_src=0.002, split_src=0.008, share=0.8),
        closeups=[("armpit", 1.25, 0.8), ("crotch", 0.55, 0.9)],
        textures=[("Base Color", 0, "material_diffuse.png")]),
}

# 🗑 폐기(2026-09-15 사장님 지시 — 특별함_강주혁 모델을 아이언맨으로 교체, fix_unit_fbx.py로): 코알라는 AI 메시가 닿은 곳마다 붙어 있어
#   T자·Idle에서 찢겨 6회 만에 멈췄다(Mixamo 업로드본 ~/Desktop/구랜디스킨모음/99_폐기_교체된원본/특별함_강주혁_이전_코알라_mixamo업로드.fbx, 안 기다림). 설정은 기록용으로만 남긴다 — 빌드 대상 아님.
RETIRED = {
    # HxH 키메라 앤트 코알라(tripo AI 생성 glb, 뼈 없음) — 구현담당1이 gen_skin_rig.py(좌우 대칭 관절표)로 3회 시도하다 왼팔 가중치가 죽어 멈춤(PM 인계 09-15).
    #   65536 정점 한도로 4조각 → 합쳐 이음새 붙이면 섬 1(경계변 19). 원본 Z 위·정면 +X → rotate_z −90. UV 네 벌이 바이트 동일(노멀 texCoord 2도 UV0과 같음).
    #   관절은 높이 0.01 단면(스크래치 koala/slices.py): 다리 x ±0.075·y 0.04 곧게(z −0.20~−0.42), 윗도리 밑단에서 합침 z −0.23, 목 좁아짐 z 0.11~0.13,
    #   오른팔(−X)은 앞으로 소품 쥔 손(손목 (−0.21, −0.02, −0.16)), 왼팔(+X)은 윗도리 옆에 붙어 주머니로(팔꿈치 (0.18, 0.08, −0.06)). 큰 신발은 앞·바깥 35°로 벌어짐.
    #   다리는 이미 곧아 팔만 T자로 편다(신발 방향은 그대로). 손에 든 소품(아래 둥근 공 + 위 호리병, 손이 가운데 목을 쥠)은 베이스 텍스처 황갈색으로 골라 RightHand 100%.
    "특별함_강주혁": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/99_폐기_교체된원본/특별함_강주혁_이전_코알라.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=0.086, rotate_z=-90.0,
        joints=dict(
            Hips=(0, 0.03, -0.19), Spine=(0, 0.03, -0.12), Spine1=(0, 0.035, -0.04), Spine2=(0, 0.04, 0.03),
            Neck=(0, 0.04, 0.10), Head=(0, 0.03, 0.14), HeadTop=(0, 0.03, 0.46),
            LeftShoulder=(0.04, 0.05, 0.075), LeftArm=(0.145, 0.06, 0.065), LeftForeArm=(0.19, 0.06, -0.07),
            LeftHand=(0.165, 0.05, -0.16), LeftHandTip=(0.13, 0.05, -0.20),
            RightShoulder=(-0.04, 0.05, 0.075), RightArm=(-0.14, 0.07, 0.07), RightForeArm=(-0.185, 0.03, -0.06),
            RightHand=(-0.21, -0.02, -0.16), RightHandTip=(-0.215, -0.03, -0.21),
            LeftUpLeg=(0.075, 0.04, -0.20), LeftLeg=(0.077, 0.045, -0.31), LeftFoot=(0.077, 0.045, -0.42),
            LeftToeBase=(0.17, -0.04, -0.46), LeftToeTip=(0.25, -0.10, -0.47),
            RightUpLeg=(-0.075, 0.04, -0.20), RightLeg=(-0.07, 0.04, -0.31), RightFoot=(-0.07, 0.04, -0.42),
            RightToeBase=(-0.17, -0.04, -0.46), RightToeTip=(-0.23, -0.07, -0.47)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        rigid_color=dict(bone="RightHand", texture="tripo_material_6a2fe4d0-e7a3-4194-a14c-a24004d42c3c_baseColor.jpg",
                         hue=(15, 55), sat_min=0.35, val_min=0.18, box_src=(-0.40, -0.02, -0.30, 0.08, -0.36, 0.05), close=3, min_cluster=100,
                         fill_src=0.015, fill_hue=(0, 70), fill_sat=0.12),
        # 🔴 몸에 붙은 팔(1회차 T자 렌더): bone heat가 윗도리 옆판·허리를 팔 뼈에 실어 T자로 펴자 날개처럼 끌려 나왔다 → 팔 중심선 캡슐 밖 정점은 팔 가중치를 뺀다
        arm_capsule=dict(radius_src=(0.045, 0.04, 0.04), margin_src=0.02),
        closeups=[("armpit", 1.0, 0.9), ("crotch", 0.5, 0.6)],
        # 머리+큰 귀털이 키의 40%·정점 밀도도 높아 Head 몫이 56%로 나온다(실패 아님 — 22뼈 전부 가중치·빈 정점 0, 복셀 대리 메시로 옮겨도 56%로 같음)
        max_bone_share=0.65,
        textures=[("Base Color", 0, "tripo_material_6a2fe4d0-e7a3-4194-a14c-a24004d42c3c_baseColor.jpg"),
                  ("Normal", 2, "tripo_material_6a2fe4d0-e7a3-4194-a14c-a24004d42c3c_normal.jpg")]),
}

# (뼈, 머리 관절, 꼬리 관절, 부모) — gen_skin_rig.py의 SPINE/LIMB와 같은 모양이지만, 여기서는
# 팔다리 표가 애초에 Left*/Right*로 따로 있어(대칭 거울식 계산이 없다) 그대로 딕셔너리 키로 찾는다.
SPINE = [("Hips", "Hips", "Spine", None), ("Spine", "Spine", "Spine1", "Hips"), ("Spine1", "Spine1", "Spine2", "Spine"),
         ("Spine2", "Spine2", "Neck", "Spine1"), ("Neck", "Neck", "Head", "Spine2"), ("Head", "Head", "HeadTop", "Neck")]
LIMB_TEMPLATE = [("Shoulder", "Shoulder", "Arm", "Spine2"), ("Arm", "Arm", "ForeArm", "Shoulder"),
                 ("ForeArm", "ForeArm", "Hand", "Arm"), ("Hand", "Hand", "HandTip", "ForeArm"),
                 ("UpLeg", "UpLeg", "Leg", "Hips"), ("Leg", "Leg", "Foot", "UpLeg"),
                 ("Foot", "Foot", "ToeBase", "Leg"), ("ToeBase", "ToeBase", "ToeTip", "Foot")]

# 걷는 자세 → T자로 펴기(순서대로, 부모 먼저) — 팔은 좌우로 수평(±X), 다리는 곧게 수직(−Z),
# 발은 자연스러운 선 자세 각(살짝 앞으로 −Y·아래 −Z), 발끝은 정면(−Y). straighten_limbs()가
# 각 뼈를 이 방향으로 (level_arms()와 같은) 쿼터니언 회전 한 번씩 돌린다.
_FOOT_DIR = Vector((0.0, -1.0, -0.3)).normalized()
STRAIGHTEN = [
    ("LeftShoulder", Vector((1, 0, 0))), ("LeftArm", Vector((1, 0, 0))),
    ("LeftForeArm", Vector((1, 0, 0))), ("LeftHand", Vector((1, 0, 0))),
    ("RightShoulder", Vector((-1, 0, 0))), ("RightArm", Vector((-1, 0, 0))),
    ("RightForeArm", Vector((-1, 0, 0))), ("RightHand", Vector((-1, 0, 0))),
    ("LeftUpLeg", Vector((0, 0, -1))), ("LeftLeg", Vector((0, 0, -1))),
    ("LeftFoot", _FOOT_DIR), ("LeftToeBase", Vector((0, -1, 0))),
    ("RightUpLeg", Vector((0, 0, -1))), ("RightLeg", Vector((0, 0, -1))),
    ("RightFoot", _FOOT_DIR), ("RightToeBase", Vector((0, -1, 0))),
]


def bone_table(joints):
    out = []
    for name, head, tail, parent in SPINE:
        out.append((name, joints[head], joints[tail], parent))
    for side in ("Left", "Right"):
        for name, head, tail, parent in LIMB_TEMPLATE:
            par = parent if parent in ("Spine2", "Hips") else side + parent
            out.append((side + name, joints[side + head], joints[side + tail], par))
    return out


def straighten_limbs(arm, body, only=None):
    """걷는 자세 그대로 잡은 뼈대를 팔다리 네 사슬(어깨~손·엉덩이~발끝) 전부 T자로 편 뒤 그 자세를
    쉬는 자세로 굽는다 — gen_skin_rig.py의 level_arms()와 같은 기법(뼈 하나씩 현재 방향→목표 방향
    쿼터니언 회전)을 팔다리 전부·발까지 일반화했다. 굽기 전후 메시가 움직이지 않는지, 최종 뼈
    방향이 목표와 맞는지 검증한다."""
    scene = bpy.context.scene
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    turned = {}
    plan = [(b, t) for b, t in STRAIGHTEN if only is None or b in only]
    for bone, target in plan:
        pb = arm.pose.bones[PREFIX + bone]
        head, tail = pb.matrix.to_translation(), pb.tail.copy()
        d = (tail - head).normalized()
        turned[bone] = round(math.degrees(d.angle(target)), 1)
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
    assert drift < 1e-4, f"이완 뒤 메시가 움직였다 {drift}"
    for bone, target in plan:
        b = arm.data.bones[PREFIX + bone]
        d = (b.tail_local - b.head_local).normalized()
        assert d.dot(target) > 0.995, f"{bone} 정렬 실패 {d} vs {target}"
    return turned


def bone_weights(mesh, table, data, k=3, smooth_iters=3):
    """정점마다 뼈를 지오데식(메시 표면을 따라가는) 최근접으로 배정한 뒤 이웃과 섞어 부드럽게 한다.

    🔴 처음엔 직선거리(3D 유클리드) 최근접 뼈마디 k개를 섞었는데, T자 렌더에서 셔츠 밑단이 왼손
    쪽으로 길게 끌려가는 것을 실측으로 확인했다 — 원인은 걷는 자세에서 손이 가슴 쪽으로 굽어
    허리 옷단과 「3D 공간에서는」 가깝지만 몸 표면을 따라가면(지오데식) 훨씬 멀기 때문에, 직선
    거리만 보면 옷단이 손 뼈에 가중치를 뺏긴다. bone heat(뼈 열, 표면을 따라 확산하니 원래 이
    문제가 없어야 한다)를 먼저 썼는데 이 메시에서 대부분의 뼈가 정점 0개로 조용히 실패해서
    (RightLeg 한 뼈가 정점의 97%를 가져감 — 관절 좌표 자체는 메시에서 2~10cm 이내로 정상이었다)
    직접 지오데식을 짰다: 뼈마디를 따라 표본점을 찍어 가장 가까운 정점을 「씨앗」으로 삼고,
    여러 씨앗에서 동시에 다익스트라(변 길이 누적)를 돌려 정점마다 표면을 따라 가장 가까운 뼈를
    구한다(owner, 1등 배정) — 3D 거리와 달리 접힌 팔이 몸통을 「뚫고」 옷단을 훔치지 않는다.
    그 다음 딱딱한 경계를 정점그룹 자체에서 몇 번 이웃과 평균 내(라플라시안) 부드럽게 섞는다.
    반환: idx(N,k) 뼈 인덱스 · w(N,k) 정규화된 가중치(합 1) · names(뼈 개수) 이름(PREFIX 포함)."""
    import heapq
    names = [PREFIX + bname for bname, *_ in table]
    V = np.array([v.co for v in mesh.vertices])
    n = len(V)
    adj = [[] for _ in range(n)]
    for e in mesh.edges:
        a, b = e.vertices
        d = float(np.linalg.norm(V[a] - V[b]))
        adj[a].append((b, d))
        adj[b].append((a, d))

    from mathutils.kdtree import KDTree
    kd = KDTree(n)
    for i, co in enumerate(V):
        kd.insert(co, i)
    kd.balance()
    heap = []
    for bi, name in enumerate(names):
        b = data.bones[name]
        head, tail = np.array(b.head_local), np.array(b.tail_local)
        for tparam in np.linspace(0.0, 1.0, 5):
            _, vi, _ = kd.find(Vector(head + (tail - head) * tparam))
            heap.append((0.0, vi, bi))
    heapq.heapify(heap)
    visited = np.zeros(n, dtype=bool)
    owner = np.full(n, -1, dtype=np.int64)
    while heap:
        d, v, bi = heapq.heappop(heap)
        if visited[v]:
            continue
        visited[v] = True
        owner[v] = bi
        for nb, w_edge in adj[v]:
            if not visited[nb]:
                heapq.heappush(heap, (d + w_edge, nb, bi))
    assert (owner >= 0).all(), "지오데식 배정에서 못 닿은 정점이 있다(메시가 여러 조각으로 끊겼을 가능성)"

    nb_bones = len(names)
    W = np.zeros((n, nb_bones))
    W[np.arange(n), owner] = 1.0
    for _ in range(smooth_iters):
        Wn = W.copy()
        for v in range(n):
            nbs = adj[v]
            if not nbs:
                continue
            acc = W[v].copy()
            for nb, _ in nbs:
                acc += W[nb]
            Wn[v] = acc / (len(nbs) + 1)
        W = Wn / Wn.sum(axis=1, keepdims=True)

    top = np.argsort(-W, axis=1)[:, :k]
    wk = np.take_along_axis(W, top, axis=1)
    wk = wk / np.maximum(wk.sum(axis=1, keepdims=True), 1e-9)
    return top, wk, names


def mask_arm_weights(body, G, joints, spec):
    """몸에 붙은 팔(코알라 왼팔 주머니·오른팔 소품): 팔 사슬(Arm→ForeArm→Hand→HandTip) 중심선에서 반지름(구간별) 밖 정점은 그쪽 팔 뼈 가중치를
    margin 거리에 걸쳐 0으로 줄이고 남은 뼈로 다시 정규화 — T자로 팔을 들 때 윗도리 옆판이 날개처럼 끌려가지 않게. 남은 게 없으면 가장 가까운 정점 가중치 복사."""
    me = body.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    scale = G.to_scale().x
    groups = {g.name: g.index for g in body.vertex_groups}
    Wm = np.zeros((n, len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight
    out = {}
    for side in ("Left", "Right"):
        pts = [np.array(G @ Vector(joints[side + k])) for k in ("Arm", "ForeArm", "Hand", "HandTip")]
        factor = np.zeros(n)
        for (a, b), r in zip(zip(pts[:-1], pts[1:]), spec["radius_src"]):
            ab = b - a
            t = np.clip(((co - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
            d = np.linalg.norm(co - (a + np.outer(t, ab)), axis=1)
            factor = np.maximum(factor, np.clip(1.0 - (d - r * scale) / (spec["margin_src"] * scale), 0.0, 1.0))
        cols = [groups[PREFIX + side + k] for k in ("Arm", "ForeArm", "Hand") if PREFIX + side + k in groups]
        before = (Wm[:, cols].sum(1) > 0.01).sum()
        Wm[:, cols] *= factor[:, None]
        out[side] = dict(팔가중치정점_전=int(before), 후=int((Wm[:, cols].sum(1) > 0.01).sum()))
    total = Wm.sum(1)
    empty = np.where(total <= 1e-6)[0]
    if len(empty):
        from mathutils.kdtree import KDTree
        full = np.where(total > 1e-6)[0]
        kd = KDTree(len(full))
        for i in full:
            kd.insert(Vector(co[i]), int(i))
        kd.balance()
        for i in empty:
            Wm[i] = Wm[kd.find(Vector(co[i]))[1]]
        total = Wm.sum(1)
    Wm /= np.maximum(total, 1e-9)[:, None]
    for g in body.vertex_groups:
        col = Wm[:, g.index]
        zero = [int(i) for i in np.where(col <= 1e-4)[0]]
        if zero:
            g.remove(zero)
        for i in np.where(col > 1e-4)[0]:
            g.add([int(i)], float(col[i]), "REPLACE")
    out["다시채운정점"] = int(len(empty))
    return out


def rigid_by_color(body, G, spec, tex_dir):
    """손에 든 소품(코알라: 아래 둥근 공 + 위 호리병)을 한 뼈 100%로. 소품이 손에 붙어 이음새를 붙이면 몸과 한 섬이라 섬으로는 못 가른다 —
    베이스 텍스처 색(색상·채도·명도)을 정점 UV에서 뽑고 원본 좌표 상자(G로 옮김) 안에서 고른 뒤, 변 그래프로 닫기(팽창→침식, 줄무늬 구멍 메움)·
    작은 조각 버림(min_cluster). 소품 정점의 다른 가중치는 지운다."""
    me = body.data
    img = bpy.data.images.load(os.path.join(tex_dir, spec["texture"]), check_existing=True)
    w, h = img.size
    px = np.empty(w * h * img.channels, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, img.channels)
    nl, n = len(me.loops), len(me.vertices)
    uv = np.empty(nl * 2, dtype=np.float32)
    me.uv_layers[0].data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    lv = np.empty(nl, dtype=np.int64)
    me.loops.foreach_get("vertex_index", lv)
    xi = np.clip((np.mod(uv[:, 0], 1.0) * w).astype(int), 0, w - 1)
    yi = np.clip((np.mod(uv[:, 1], 1.0) * h).astype(int), 0, h - 1)
    col = np.zeros((n, 3))
    cnt = np.zeros(n)
    np.add.at(col, lv, px[yi, xi, :3])
    np.add.at(cnt, lv, 1)
    col /= np.maximum(cnt, 1)[:, None]
    mx, mn = col.max(1), col.min(1)
    sat = np.where(mx > 1e-6, (mx - mn) / np.maximum(mx, 1e-6), 0.0)
    hue = np.degrees(np.arctan2(np.sqrt(3) * (col[:, 1] - col[:, 2]), 2 * col[:, 0] - col[:, 1] - col[:, 2])) % 360
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    x0, x1, y0, y1, z0, z1 = spec["box_src"]
    lo, hi = np.array(G @ Vector((x0, y0, z0))), np.array(G @ Vector((x1, y1, z1)))
    box = np.all((co >= np.minimum(lo, hi)) & (co <= np.maximum(lo, hi)), axis=1)
    sel = box & (hue > spec["hue"][0]) & (hue < spec["hue"][1]) & (sat > spec["sat_min"]) & (mx > spec["val_min"])
    E = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", E)
    E = E.reshape(-1, 2)
    for _ in range(spec.get("close", 3)):
        t = sel.copy()
        t[E[:, 0][sel[E[:, 1]]]] = True
        t[E[:, 1][sel[E[:, 0]]]] = True
        sel = t & box
    for _ in range(spec.get("close", 3)):
        t = sel.copy()
        t[E[:, 0][~sel[E[:, 1]]]] = False
        t[E[:, 1][~sel[E[:, 0]]]] = False
        sel = t
    adj = [[] for _ in range(n)]
    for a, b in E[sel[E[:, 0]] & sel[E[:, 1]]]:
        adj[a].append(b)
        adj[b].append(a)
    label = -np.ones(n, dtype=np.int64)
    sizes = []
    for v in np.where(sel)[0]:
        if label[v] >= 0:
            continue
        k = len(sizes)
        stack, size = [v], 0
        label[v] = k
        while stack:
            x = stack.pop()
            size += 1
            for y in adj[x]:
                if label[y] < 0:
                    label[y] = k
                    stack.append(y)
        sizes.append(size)
    keep = [k for k, sz in enumerate(sizes) if sz >= spec.get("min_cluster", 100)]
    final = np.isin(label, keep)
    filled = 0
    if spec.get("fill_src"):
        # 줄무늬(검은 갈색)·그늘진 안쪽 면이 색 문턱에서 빠져 몸 뼈에 남으면 T자에서 줄처럼 늘어났다 — 고른 소품 가까이의 넓은 황갈 계열은 소품으로
        from mathutils.kdtree import KDTree
        kd = KDTree(int(final.sum()))
        for i in np.where(final)[0]:
            kd.insert(Vector(co[i]), int(i))
        kd.balance()
        reach = spec["fill_src"] * G.to_scale().x
        broad = box & ~final & (hue >= spec.get("fill_hue", (0, 70))[0]) & (hue <= spec.get("fill_hue", (0, 70))[1]) & (sat > spec.get("fill_sat", 0.12))
        for i in np.where(broad)[0]:
            if kd.find(Vector(co[i]))[2] <= reach:
                final[i] = True
                filled += 1
    ids = [int(i) for i in np.where(final)[0]]
    # 🔴 소품이 몸(배)에 닿은 자리에서 이음새를 붙이면 한 면으로 이어진다 — 손이 움직이면 소품(손 100%)↔배(Spine·Hips) 모서리가 100배로 찢겼다
    #   (코알라 팔 안 편 시험: 팔45° 100×·Idle 113×, T자에선 그게 굽혀져 줄무늬 띠). 소품 면을 몸에서 떼어낸다 — 떼인 몸 쪽 고리 정점은 가장 가까운 비소품 정점 가중치로.
    import bmesh
    from mathutils.kdtree import KDTree
    others = np.where(~final)[0]
    kd = KDTree(len(others))
    for i in others:
        kd.insert(Vector(co[i]), int(i))
    kd.balance()
    body_w = {int(i): {ge.group: ge.weight for ge in me.vertices[int(i)].groups} for i in others}
    for g in body.vertex_groups:
        g.remove(ids)
    grp = body.vertex_groups.get(PREFIX + spec["bone"]) or body.vertex_groups.new(name=PREFIX + spec["bone"])
    grp.add(ids, 1.0, "REPLACE")
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    sel_set = set(ids)
    prop_faces = [f for f in bm.faces if all(v.index in sel_set for v in f.verts)]
    n_before = len(bm.verts)
    bmesh.ops.split(bm, geom=prop_faces, use_only_faces=True)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.verts.ensure_lookup_table()
    dl = bm.verts.layers.deform.verify()
    ring = 0
    prop_face_verts = {v for f in bm.faces if f.select is False for v in f.verts}  # 자리 채움(아래에서 다시 계산)
    prop_vert_ids = set()
    for f in bm.faces:
        pass
    for v in bm.verts:
        w = v[dl]
        bone_idx = grp.index
        if len(w) == 1 and bone_idx in w and w[bone_idx] >= 0.999:
            # 손 100% 정점 — 몸 면에만 붙어 있으면(떼인 고리) 몸 가중치로 바꾼다. 소품 면은 전부 손 100% 정점만 쓰니 이웃 면 중 하나라도 비손 정점이면 몸 쪽.
            body_side = any(any(not (len(u[dl]) == 1 and bone_idx in u[dl] and u[dl][bone_idx] >= 0.999) for u in f.verts) for f in v.link_faces)
            if body_side:
                j = kd.find(v.co)[1]
                w.clear()
                for gi, ww in body_w[j].items():
                    w[gi] = ww
                ring += 1
    bm.to_mesh(me)
    bm.free()
    me.update()
    return dict(뼈=spec["bone"], 정점=len(ids), 조각=sorted((sizes[k] for k in keep), reverse=True), 버린조각=len(sizes) - len(keep), 가까이채움=filled,
                떼어낸면=len(prop_faces), 정점증가=len(me.vertices) - n_before, 몸쪽고리=ring)


def mask_arm_facing(body, G, joints, spec):
    """코트에 붙은 팔(베르고 오른팔 — 원본 z 0.14~0.19에서 소매와 코트가 표면째 붙어 표면 거리로도 안 갈림, 2회차 실측 전·후 정점 수 같음):
    팔 가중치 정점마다 팔 사슬(Arm→ForeArm→Hand→HandTip) 가장 가까운 점에서 정점으로 향하는 방향과 정점 법선의 내적을 본다 —
    소매 겉은 축 바깥을 보고(+), 소매 옆 코트 옆판은 팔 축을 향한다(−). 내적이 lo 이하면 팔 가중치 0, hi 이상이면 그대로, 사이는 부드럽게.
    그 몫을 이웃과 몇 번 평균 내 찢김 없이 잇고, 빈 정점은 가장 가까운 정점 가중치 복사. 🔴 1회차: T자에서 코트 옆판이 커튼처럼 끌려 나옴."""
    from mathutils.kdtree import KDTree
    me = body.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    nor = np.empty(n * 3)
    me.vertices.foreach_get("normal", nor)
    nor = nor.reshape(-1, 3)
    ev = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ev)
    ev = ev.reshape(-1, 2)
    groups = {g.name: g.index for g in body.vertex_groups}
    Wm = np.zeros((n, len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight
    lo, hi = spec.get("lo", -0.1), spec.get("hi", 0.2)
    out = {}
    for side in spec.get("sides", ("Left", "Right")):
        pts = [np.array(G @ Vector(joints[side + k])) for k in ("Arm", "ForeArm", "Hand", "HandTip")]
        best = np.full(n, np.inf)
        radial = np.zeros((n, 3))
        for a, b in zip(pts[:-1], pts[1:]):
            ab = b - a
            t = np.clip(((co - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
            r = co - (a + np.outer(t, ab))
            d = np.linalg.norm(r, axis=1)
            m = d < best
            best[m], radial[m] = d[m], r[m]
        rhat = radial / np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), 1e-9)
        dot = (rhat * nor).sum(1)
        factor = np.clip((dot - lo) / (hi - lo), 0.0, 1.0)
        for _ in range(spec.get("smooth", 2)):                           # 이웃 평균(변 양끝) — 경계 한 줄 찢김 방지
            acc = factor.copy()
            cnt = np.ones(n)
            np.add.at(acc, ev[:, 0], factor[ev[:, 1]])
            np.add.at(acc, ev[:, 1], factor[ev[:, 0]])
            np.add.at(cnt, ev[:, 0], 1)
            np.add.at(cnt, ev[:, 1], 1)
            factor = acc / cnt
        cols = [groups[PREFIX + side + k] for k in ("Arm", "ForeArm", "Hand") if PREFIX + side + k in groups]
        had = Wm[:, cols].sum(1) > 0.01
        Wm[:, cols] *= factor[:, None]
        out[side] = dict(팔가중치정점_전=int(had.sum()), 후=int((Wm[:, cols].sum(1) > 0.01).sum()), 축향한정점=int((had & (dot < lo)).sum()))
        # 🔴 베르고 오른팔 3·4회차: bone heat가 코트에 붙은 아래 소매·소맷부리를 애초에 Spine·Hips·UpLeg에 줘서(팔 가중치 0) T자에서 소매 조각이 제자리에 남아
        #   엉덩이 가시·겨드랑이 커튼이 됐다 → assign_radius_src 안에서 팔 축 바깥을 보는(내적 > hi) 정점은 사슬 투영 구간 뼈로 팔 가중치를 채운다
        #   (관절 근처는 이웃 뼈와 반씩, 위팔 위 25%는 어깨 쪽으로 서서히 줄여 겨드랑이는 bone heat 그대로).
        if side in spec.get("assign_sides", ()):
            scale = G.to_scale().x
            ar, am, bl = spec["assign_radius_src"] * scale, spec["assign_margin_src"] * scale, spec.get("blend_src", 0.006) * scale
            segs = list(zip(pts[:-1], pts[1:]))
            seg_k = np.zeros(n, dtype=np.int64)
            seg_t = np.zeros(n)
            best2 = np.full(n, np.inf)
            for k, (a, b) in enumerate(segs):
                ab = b - a
                t = np.clip(((co - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
                d = np.linalg.norm(co - (a + np.outer(t, ab)), axis=1)
                m = d < best2
                best2[m], seg_k[m], seg_t[m] = d[m], k, t[m]
            L = [float(np.linalg.norm(b - a)) for a, b in segs]
            strength = np.clip(1.0 - (best2 - ar) / am, 0.0, 1.0) * np.clip((dot - hi) / 0.2, 0.0, 1.0)
            strength *= np.where(seg_k == 0, np.clip(seg_t / 0.25, 0.0, 1.0), 1.0)
            bone_cols = [groups[PREFIX + side + k] for k in ("Arm", "ForeArm", "Hand")]
            share = np.zeros((n, 3))
            share[np.arange(n), np.minimum(seg_k, 2)] = 1.0
            Lk = np.array(L)[seg_k]
            prev = 0.5 * np.clip(1.0 - seg_t * Lk / bl, 0.0, 1.0) * (seg_k > 0)
            nxt = 0.5 * np.clip(1.0 - (1.0 - seg_t) * Lk / bl, 0.0, 1.0) * (seg_k < 2)
            rows = np.arange(n)
            share[rows, np.minimum(seg_k, 2)] -= prev + nxt
            share[rows[seg_k > 0], seg_k[seg_k > 0] - 1] += prev[seg_k > 0]
            share[rows[seg_k < 2], np.minimum(seg_k[seg_k < 2] + 1, 2)] += nxt[seg_k < 2]
            cur_arm = Wm[:, bone_cols].sum(1)
            gain = strength > cur_arm + 1e-3
            keep = 1.0 - strength
            rest = Wm.copy()
            rest[:, bone_cols] = 0.0
            rs = rest.sum(1, keepdims=True)
            rest = np.where(rs > 1e-9, rest / np.maximum(rs, 1e-9), 0.0)
            newW = rest * keep[:, None]
            newW[:, bone_cols] = share * strength[:, None]
            Wm[gain] = newW[gain]
            out[side]["팔로채운정점"] = int(gain.sum())
    total = Wm.sum(1)
    empty = np.where(total <= 1e-6)[0]
    if len(empty):
        full = np.where(total > 1e-6)[0]
        kd = KDTree(len(full))
        for i in full:
            kd.insert(Vector(co[i]), int(i))
        kd.balance()
        for i in empty:
            Wm[i] = Wm[kd.find(Vector(co[i]))[1]]
        total = Wm.sum(1)
    Wm /= np.maximum(total, 1e-9)[:, None]
    for g in body.vertex_groups:
        col = Wm[:, g.index]
        zero = [int(i) for i in np.where(col <= 1e-4)[0]]
        if zero:
            g.remove(zero)
        for i in np.where(col > 1e-4)[0]:
            g.add([int(i)], float(col[i]), "REPLACE")
    out["다시채운정점"] = int(len(empty))
    return out


def split_arm_contact(body, G, joints, spec):
    """코트와 한 표면으로 붙은 소매(베르고 오른팔, 원본 z 0.14~0.19): 가중치로는 붙은 띠의 삼각형이 팔·코트 둘 다에 묶여 T자에서 커튼·엉덩이 가시가 남았다(3~5회차).
    bone heat 전에 면을 가른다 — 팔 사슬에서 radius 안이고 법선이 팔 축 바깥을 보는(내적 > facing) 면을 팔 면으로, 팔 면과 아닌 면 사이 변 중 z 띠 안의 것만 split_edges.
    (띠 밖 소매·겨드랑이·소맷부리 아래는 그대로 이어진다.)"""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.normal_update()
    scale = G.to_scale().x
    R, thr = spec["radius_src"] * scale, spec.get("facing", 0.45)
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    out = {}
    for side in spec["sides"]:
        pts = [G @ Vector(joints[side + k]) for k in ("Arm", "ForeArm", "Hand", "HandTip")]
        arm = set()
        for f in bm.faces:
            c = f.calc_center_median()
            best = None
            for a, b in zip(pts[:-1], pts[1:]):
                ab = b - a
                t = max(0.0, min(1.0, (c - a).dot(ab) / max(ab.length_squared, 1e-12)))
                r = c - (a + ab * t)
                if best is None or r.length < best.length:
                    best = r
            if 1e-9 < best.length < R and f.normal.dot(best.normalized()) > thr:
                arm.add(f)
        cut = [e for e in bm.edges if len(e.link_faces) == 2 and ((e.link_faces[0] in arm) != (e.link_faces[1] in arm))
               and zlo < (e.verts[0].co.z + e.verts[1].co.z) / 2 < zhi]
        bmesh.ops.split_edges(bm, edges=cut)
        out[side] = dict(팔면=len(arm), 자른변=len(cut))
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    return out


def coat_hem(body, G, spec):
    """긴 코트 자락(베르고): 엉덩이~밑단 사이 정점은 팔·손 가중치를 그대로 두고, 나머지 몫을 Hips와 좌우 UpLeg에 높이·좌우로 나눈다.
    아래로 갈수록(밑단 share) 넓적다리 몫, 가운데는 좌우를 부드럽게 반씩. 엉덩이 위·밑단 아래 band 구간에서 bone heat 가중치와 섞어 이음매가 안 튀게.
    🔴 bone heat 그대로면 자락이 종아리(Leg)·어깨에 실려 Idle에서 훌라후프처럼 벌어졌다(구현담당1 1차 유니티 실측)."""
    me = body.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    groups = {g.name: g.index for g in body.vertex_groups}
    Wm = np.zeros((n, len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight
    hip, hem = (G @ Vector((0, 0, spec["hip_src"]))).z, (G @ Vector((0, 0, spec["hem_src"]))).z
    scale = G.to_scale().x
    band, split = spec["band_src"] * scale, spec["split_src"] * scale
    cx = (G @ Vector((spec["center_x_src"], 0, 0))).x
    z, x = co[:, 2], co[:, 0]
    smooth = lambda t: t * t * (3 - 2 * t)
    t = np.clip((hip - z) / (hip - hem), 0.0, 1.0)                      # 엉덩이 0 → 밑단 1
    blend = smooth(np.clip((hip + band - z) / band, 0, 1)) * smooth(np.clip((z - (hem - band)) / band, 0, 1))   # 규칙이 쓰이는 몫(구간 밖 0)
    arm_cols = [groups[PREFIX + s + k] for s in ("Left", "Right") for k in ("Arm", "ForeArm", "Hand") if PREFIX + s + k in groups]
    arm_w = Wm[:, arm_cols].sum(1).clip(0, 1)
    rule = np.zeros_like(Wm)
    leg = spec["share"] * smooth(t)
    left = smooth(np.clip(0.5 + (x - cx) / (2 * split), 0, 1))
    rule[:, groups[PREFIX + "Hips"]] = 1 - leg
    rule[:, groups[PREFIX + "LeftUpLeg"]] = leg * left
    rule[:, groups[PREFIX + "RightUpLeg"]] = leg * (1 - left)
    rest = Wm.copy()
    rest[:, arm_cols] = 0
    rs = rest.sum(1, keepdims=True)
    heat_rest = np.where(rs > 1e-9, rest / np.maximum(rs, 1e-9), rule)   # 팔 몫 뺀 나머지의 bone heat 비율
    new_rest = heat_rest * (1 - blend)[:, None] + rule * blend[:, None]
    new = new_rest * (1 - arm_w)[:, None]
    new[:, arm_cols] = Wm[:, arm_cols]
    new /= np.maximum(new.sum(1, keepdims=True), 1e-9)
    touched = np.where(blend > 1e-6)[0]
    for g in body.vertex_groups:
        col = new[:, g.index]
        zero = [int(i) for i in touched if col[i] <= 1e-4]
        if zero:
            g.remove(zero)
        for i in touched:
            if col[i] > 1e-4:
                g.add([int(i)], float(col[i]), "REPLACE")
    before_leg = sum(int((Wm[touched, groups[PREFIX + s + "Leg"]] > 0.05).sum()) for s in ("Left", "Right"))
    after_leg = sum(int((new[touched, groups[PREFIX + s + "Leg"]] > 0.05).sum()) for s in ("Left", "Right"))
    return dict(정점=int(len(touched)), 종아리가중치정점=[before_leg, after_leg], 팔몫남긴정점=int((arm_w[touched] > 0.01).sum()))


def fill_unweighted(body):
    """bone heat가 못 준 정점(합 0)은 가장 가까운 가중치 있는 정점의 가중치를 복사(static-skin-rigging 교훈 — 가까운 「뼈」로 주면 늘어난다)."""
    from mathutils.kdtree import KDTree
    me = body.data
    has = [v.index for v in me.vertices if sum(g.weight for g in v.groups) > 1e-6]
    miss = [v.index for v in me.vertices if sum(g.weight for g in v.groups) <= 1e-6]
    if not miss:
        return dict(빈정점=0)
    kd = KDTree(len(has))
    for i in has:
        kd.insert(me.vertices[i].co, i)
    kd.balance()
    far = 0.0
    for i in miss:
        _, j, d = kd.find(me.vertices[i].co)
        far = max(far, d)
        for g in me.vertices[j].groups:
            body.vertex_groups[g.group].add([i], g.weight, "REPLACE")
    return dict(빈정점=len(miss), 최대거리=round(far, 4))


def build(name, out_dir=None, render_dir=None):
    cfg = UNITS[name]
    out_path = f"Assets/Art/Units/{name}/{name}.fbx"
    source = cfg["source"]
    dst = os.path.join(out_dir, os.path.basename(out_path)) if out_dir else os.path.join(ROOT, out_path)
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": source, "sha256": hashlib.sha256(open(source, "rb").read()).hexdigest()}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=source)
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH" and not o.name.startswith("Icosphere")]
    if len(meshes) > 1:
        # 65536 정점 한도로 쪼개진 한 몸(코알라 4조각) — 세계 변환을 각자 데이터에 구운 뒤 하나로 합친다(이음새는 아래에서 붙인다)
        for o in meshes:
            o.data.transform(o.matrix_world)
            o.parent = None
            o.matrix_basis = Matrix.Identity(4)
        for o in scene.objects:
            o.select_set(o in meshes)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        report["합친 조각"] = len(meshes)
    body = bpy.context.view_layer.objects.active if len(meshes) > 1 else meshes[0]
    body.name = body.data.name = cfg.get("mesh_name", "Body")      # 베르고: 1차 FBX 메시 이름 Vergo 유지(유니티 .meta 이름표)
    report["원본 정점·삼각형"] = [len(body.data.vertices), sum(len(p.vertices) - 2 for p in body.data.polygons)]
    if cfg.get("rotate_z"):                                             # 원본 정면이 −Y가 아니면(코알라 +X) 세계 변환과 함께 돌린다
        body.data.transform(Matrix.Rotation(math.radians(cfg["rotate_z"]), 4, "Z") @ body.matrix_world)
        body.parent = None
        body.matrix_basis = Matrix.Identity(4)

    # ── 텍스처: PM 조사대로 이미지 0=베이스(jpeg)·1=노멀(png), 원본 바이트 그대로 Textures/에.
    j, binchunk = glb(source)
    os.makedirs(tex_dir, exist_ok=True)
    for socket, index, fname in cfg["textures"]:
        open(os.path.join(tex_dir, fname), "wb").write(image_bytes(j, binchunk, index))
    rebuild_material(body.data.materials[0], [(socket, fname) for socket, index, fname in cfg["textures"]], tex_dir)

    # ── 중심맞춤·스케일(gen_skin_rig.py의 build()와 같은 식) — 회전은 불필요(이미 −Y를 본다,
    # raw_from_negY.png로 확인). 관절도 메시와 같은 G를 써서 어긋나지 않게 한다.
    world = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    lo, hi = world.min(0), world.max(0)
    H = float(hi[2] - lo[2])
    band = world[(world[:, 2] >= lo[2] + H * cfg["center_band"][0]) & (world[:, 2] <= lo[2] + H * cfg["center_band"][1])]
    cx, cy = float((band[:, 0].min() + band[:, 0].max()) / 2), float((band[:, 1].min() + band[:, 1].max()) / 2)
    s = cfg["height"] / H
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo[2]))
    report["원본 키(cm)"], report["배율"] = round(H, 3), round(s, 6)
    body.data.transform(G @ body.matrix_world)
    # 🔴 glTF가 메시를 Sketchfab 뿌리 빈 오브젝트(Y위→Z위 회전 행렬) 밑에 둔다 — 세계 변환을 데이터에 구운 뒤 부모를 안 끊으면
    # 메시가 세계에서 또 회전돼 뼈대와 어긋나고 bone heat가 조용히 실패했다(1차 증상 「한 뼈 97%」의 진짜 원인, 2차 실측: 부모 끊으니 죽은 뼈 0).
    body.parent = None
    body.matrix_basis = Matrix.Identity(4)
    for o in [o for o in scene.objects if o != body]:
        bpy.data.objects.remove(o, do_unlink=True)

    # ── 🔴 이음새 붙이기(blender 2차, 2026-09-15): glTF 가져오기가 UV 이음새마다 정점을 쪼개 메시가 섬 6개·경계 변 2,692개로
    # 보였다 — 1차의 bone heat 실패(한 뼈 97%)와 지오데식 배정의 어깨 조각남·목깃 구멍이 전부 이 끊긴 이음새 탓(표면이 이어지지 않으니
    # 열·거리가 이음새를 못 건넌다). 거리 0으로 붙이면 섬 1·경계 0·비다양체 0인 닫힌 몸. UV는 면 모서리(loop)마다 따로 저장되니 붙여도 그대로다.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    before_v = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    report["이음새 붙임"] = dict(정점=[before_v, len(body.data.vertices)], 경계변=boundary, 비다양체변=nonman)

    # ── 감량: 균등 비율(단일 메시·단일 재질이라 이름/재질로 얼굴·손을 따로 못 가린다 — 감량본
    # 설명(~/Desktop/구랜디스킨모음/03_특별함/특별함_최동준_mixamo업로드_설명.txt)이 이미 같은 이유로 균등 감량했고,
    # 이 파일도 시간상 그 판단을 따른다. 정점그룹으로 얼굴·손 보호는 나중에 필요하면 추가 가능).
    mod = body.modifiers.new("decimate", "DECIMATE")
    mod.ratio = cfg["decimate"]
    dg = bpy.context.evaluated_depsgraph_get()
    new_mesh = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    body.modifiers.remove(mod)
    old_mesh = body.data
    body.data = new_mesh
    bpy.data.meshes.remove(old_mesh)
    report["감량 후 삼각형"] = sum(len(p.vertices) - 2 for p in body.data.polygons)
    if cfg.get("arm_split"):
        report["소매·코트 가름"] = split_arm_contact(body, G, cfg["joints"], cfg["arm_split"])

    # ── 뼈대: 22개(Hips 루트 + Spine·Spine1·Spine2·Neck·Head + 좌우 Shoulder·Arm·ForeArm·Hand
    # + 좌우 UpLeg·Leg·Foot·ToeBase) — 위 JOINTS_CM(걷는 자세 그대로, 좌우 따로)에 G를 적용해 짓는다.
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
        eb.head = G @ Vector(h)
        eb.tail = G @ Vector(t)
        d = (eb.tail - eb.head).normalized()
        eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    for bname, h, t, parent in table:
        if parent:
            data.edit_bones[PREFIX + bname].parent = data.edit_bones[PREFIX + parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    report["뼈"] = len(data.bones)

    # ── 가중치: 이음새를 붙인 닫힌 몸에 bone heat(2차) — 1차 지오데식 배정(bone_weights)은 끊긴 이음새에서 조각났다.
    for o in scene.objects:
        o.select_set(o in (body, arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    report["가중치 보정"] = fill_unweighted(body)
    counts = {b.name[len(PREFIX):]: 0 for b in data.bones}
    gname = {g.index: g.name for g in body.vertex_groups}
    for v in body.data.vertices:
        for ge in v.groups:
            if ge.weight > 0.01 and gname[ge.group].startswith(PREFIX):
                counts[gname[ge.group][len(PREFIX):]] += 1
    report["뼈별 정점(w>0.01)"] = counts
    dead = [k for k, c in counts.items() if c == 0]
    assert not dead, f"가중치 없는 뼈 {dead}"
    top_bone = max(counts, key=counts.get)
    top_share = counts[top_bone] / len(body.data.vertices)
    report["최대 몫 뼈"] = [top_bone, round(top_share, 3)]
    assert top_share < cfg.get("max_bone_share", 0.5), f"한 뼈({top_bone})에 정점 {top_share:.0%} — bone heat 실패(1차 증상)? 분포 {counts}"

    if cfg.get("arm_capsule"):
        report["팔 캡슐"] = mask_arm_weights(body, G, cfg["joints"], cfg["arm_capsule"])
    if cfg.get("arm_facing"):
        report["팔 법선 마스크"] = mask_arm_facing(body, G, cfg["joints"], cfg["arm_facing"])
    if cfg.get("rigid_color"):
        report["손 소품 한 뼈"] = rigid_by_color(body, G, cfg["rigid_color"], tex_dir)
    if cfg.get("coat_hem"):
        report["코트 자락"] = coat_hem(body, G, cfg["coat_hem"])
    report["걷는 자세→T자(°)"] = straighten_limbs(arm, body, cfg.get("straighten"))

    # 🔴 다리를 곧게 펴면(사슬 길이는 그대로, 끝점만 바뀐다) 원래 걷는 자세에서 발목까지의 수직
    # 거리보다 편 다리의 길이가 더 길어져 발이 바닥(z=0) 밑으로 내려간다(실측: -0.07m) — 메시·
    # 뼈대 로컬 좌표를 통째로 올려 발이 다시 z=0에 닿게 한다(오브젝트 위치가 아니라 데이터 자체를
    # 옮겨야 judge() 등 로컬 좌표를 직접 읽는 코드도 맞게 나온다).
    bpy.context.view_layer.update()
    minz = min(v.co.z for v in body.data.vertices)
    body.data.transform(Matrix.Translation((0, 0, -minz)))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.head.z -= minz
        eb.tail.z -= minz
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    # 다리를 펴면 키가 늘어난다(1.8 → 1.873 실측) — 발 z 0 기준으로 메시·뼈대를 같이 줄여 키 1.8로
    top = max(v.co.z for v in body.data.vertices)
    k = cfg["height"] / top
    body.data.transform(Matrix.Scale(k, 4))
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.head, eb.tail = eb.head * k, eb.tail * k
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    report["T자 뒤 키 맞춤 배율"] = round(k, 4)
    V = np.array([v.co for v in body.data.vertices])
    report["크기(m)"] = [round(float(c), 3) for c in (V.max(0) - V.min(0))]
    report["최저 z"] = round(float(V[:, 2].min()), 4)

    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
    report["출력"] = dst
    if render_dir:
        report["판정"] = judge(name, arm, body, render_dir)
        report["옆구리·가랑이 확대"] = armpit_crotch_renders(name, arm, body, render_dir, cfg.get("closeups"))
    return report


def armpit_crotch_renders(unit, arm, body, out, closeups=None):
    """PM 요청 — T자에서 옆구리(겨드랑이)·가랑이 확대. judge()가 이미 만든 씬(카메라·조명)을 그대로 쓴다."""
    scene = bpy.context.scene
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    cam = scene.camera
    views = [(tag, (0, -3, z), (math.radians(90), 0, 0), sc) for tag, z, sc in (closeups or [("armpit", 1.35, 0.7), ("crotch", 0.78, 0.6)])]
    for tag, loc, rot, sc in views:
        cam.location, cam.rotation_euler, cam.data.ortho_scale = loc, rot, sc
        scene.render.filepath = os.path.join(out, f"{unit}_{tag}.png")
        bpy.ops.render.render(write_still=True)
    return [f"{unit}_{tag}.png" for tag, *_ in views]


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
    import json
    for name in names or list(UNITS):
        r = build(name, out_dir, render_dir)
        print("리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
