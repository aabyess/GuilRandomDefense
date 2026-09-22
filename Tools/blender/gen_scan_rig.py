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
    # 헌터x헌터 레오리오(zip 안 Microsoft GLTF Exporter glb) — 2026-09-16 희귀함 5호. 뼈·스킨·애니 0 · 메시 14 · 재질 14 · 총 3,228삼각형(감량 불필요).
    #   **이미 정확한 T자**(팔이 수평 z 0.129 · 팔 span 0.441 ≈ 키 0.450) → straighten=[]로 아무 뼈도 안 편다(기본 STRAIGHTEN을 그대로 두면 다리·발까지 억지로 돌린다).
    #   🔴 왼손 가방을 뺀다: mesh_id16·mesh_id17(납작한 검은 판)과 mesh_id15(손잡이). 판이 x −0.256까지 뻗어 가로를 키우고, 렌더로 보면 두께 없는 판자라 대기 모습에 안 맞는다.
    #     (원하면 손 뼈에 붙여 되살릴 수 있다 — 그때는 세 메시를 살리고 rigid로 LeftHand 100%.)
    #   🔴 텍스처는 **glb 인덱스로** 뽑는다: zip의 textures/gltf_embedded_N.png는 재인코딩본이라 glb 이미지와 바이트가 다르고 번호도 한 칸 밀려 있다(JSON 이미지 18개 vs 파일 6개).
    #     재질 14개가 이미지 4장을 나눠 쓴다 — 4번(정장·피부 계열 6재질) · 1번(3재질) · 10번(머리카락, **알파 45%가 투명한 컷아웃**) · 7번(1재질).
    #   모델이 원점에서 x +0.035 · y −0.27만큼 치우쳐 있어 관절도 그 자리(원본 좌표)로 적는다. 발은 x 0.008·0.0625, 발끝은 −Y.
    "희귀함_두유찬": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_두유찬.zip"),
        member="source/Leorio.glb",
        height=1.8, center_band=(0.02, 0.08), decimate=1.0, rotate_z=0.0,
        drop_meshes=["mesh_id15", "mesh_id16", "mesh_id17"],
        joints=dict(
            Hips=(0.035, -0.270, 0.014), Spine=(0.035, -0.270, 0.048), Spine1=(0.035, -0.270, 0.082), Spine2=(0.035, -0.270, 0.118),
            Neck=(0.035, -0.265, 0.142), Head=(0.035, -0.262, 0.160), HeadTop=(0.035, -0.262, 0.222),
            LeftShoulder=(0.052, -0.258, 0.128), LeftArm=(0.080, -0.255, 0.129), LeftForeArm=(0.159, -0.252, 0.129),
            LeftHand=(0.217, -0.251, 0.128), LeftHandTip=(0.252, -0.251, 0.127),
            RightShoulder=(0.018, -0.258, 0.128), RightArm=(-0.010, -0.255, 0.129), RightForeArm=(-0.089, -0.252, 0.129),
            RightHand=(-0.147, -0.251, 0.128), RightHandTip=(-0.182, -0.251, 0.127),
            LeftUpLeg=(0.055, -0.270, 0.014), LeftLeg=(0.060, -0.268, -0.099), LeftFoot=(0.0625, -0.262, -0.198),
            LeftToeBase=(0.0625, -0.290, -0.216), LeftToeTip=(0.0625, -0.312, -0.222),
            RightUpLeg=(0.015, -0.270, 0.014), RightLeg=(0.010, -0.268, -0.099), RightFoot=(0.008, -0.262, -0.198),
            RightToeBase=(0.008, -0.290, -0.216), RightToeTip=(0.008, -0.312, -0.222)),
        straighten=[],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={4: "47_baseColor.png", 1: "43_baseColor.png", 10: "44_baseColor.png", 7: "38_baseColor.png"},
        materials={m: [("Base Color", "47_baseColor.png")] for m in ("47", "37", "39", "40", "41", "42")}
                  | {m: [("Base Color", "43_baseColor.png")] for m in ("43", "45", "46")}
                  | {"44": [("Base Color", "44_baseColor.png")], "38": [("Base Color", "38_baseColor.png")]}),
    # 주술회전 이타도리 유지(Sketchfab-16.95 glb) — 2026-09-16 희귀함. 뼈·스킨·애니 0 · 재질 1 · 1024² RGB 아틀라스 1장 · 삼각형 325,014.
    #   메시 4조각(정점 65,532 ×3 + 2,555)은 16비트 인덱스 한계로 잘린 한 덩어리 — 합쳐 이음새 붙이면 162,489정점 · **섬 1개**. Z 위·이미 −Y 정면.
    #   비대칭 선 자세(X거울 1% 이내 53%): 오른팔(−X)은 손을 엉덩이 앞으로, 왼팔은 옆에 내림 · 왼발이 바깥으로 돌아감.
    #   관절은 원본 좌표 실측(스크래치 yuji/landmarks.py·blobs.py, 키 1.898 · 바닥 z −0.9505): 🔴 조거 바지 밑위가 깊어 가랑이 갈라짐이 키 0.325 —
    #   거기에 UpLeg를 두면 허벅지가 짧아지니 고관절은 키 0.46(z −0.077). 팔꿈치는 소매 바깥 끝(R −0.356·L +0.294)에서 반지름만큼 안쪽.
    #   🔴 2회차: 손·아래팔은 **텍스처 피부색 정점**으로 다시 잼(yuji/skinarm.py) — 오른손이 초안보다 앞·아래(손목 y −0.17·z 0.08). 소맷부리 z R 0.27 · L 0.19.
    "희귀함_김경현": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_김경현.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=0.13, rotate_z=0.0, bake_space=True,
        joints=dict(
            Hips=(-0.02, -0.01, -0.077), Spine=(-0.02, -0.01, 0.06), Spine1=(-0.015, 0.0, 0.226), Spine2=(-0.01, 0.02, 0.378),
            Neck=(-0.005, 0.04, 0.568), Head=(0.0, 0.03, 0.68), HeadTop=(0.0, 0.01, 0.947),
            LeftShoulder=(0.03, 0.04, 0.56), LeftArm=(0.16, 0.06, 0.53), LeftForeArm=(0.24, 0.155, 0.28),
            LeftHand=(0.228, 0.09, -0.06), LeftHandTip=(0.232, 0.08, -0.20),
            RightShoulder=(-0.05, 0.04, 0.56), RightArm=(-0.19, 0.05, 0.53), RightForeArm=(-0.285, 0.04, 0.32),
            RightHand=(-0.18, -0.17, 0.08), RightHandTip=(-0.185, -0.20, -0.03),
            LeftUpLeg=(0.07, -0.02, -0.077), LeftLeg=(0.11, 0.05, -0.42), LeftFoot=(0.125, 0.10, -0.79),
            LeftToeBase=(0.21, 0.03, -0.92), LeftToeTip=(0.29, -0.04, -0.94),
            RightUpLeg=(-0.11, -0.02, -0.077), RightLeg=(-0.18, -0.01, -0.42), RightFoot=(-0.21, 0.04, -0.79),
            RightToeBase=(-0.25, -0.09, -0.92), RightToeTip=(-0.27, -0.19, -0.94)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        # 🔴 1회차(캡슐 없음): 손이 바지·후드 밑단에 닿아 T자에서 후드 옆판·바지 엉덩이가 날개로 끌려 나옴(팔45° 7.94) → 캡슐 + 법선 마스크
        arm_capsule=dict(radius_src=(0.075, 0.07, 0.07), margin_src=0.02),
        arm_facing=dict(lo=-0.1, hi=0.2, smooth=2),
        # 🔴 3회차: 관절을 피부색으로 바로잡고 캡슐을 넓혀도 손→엉덩이 띠가 남음 — 섬 1개라 손·소매 밑면이 바지·후드와 **면으로** 이어져 있다 → 겨드랑이 아래 띠 가르기
        arm_split=dict(sides=("Left", "Right"), radius_src=0.075, facing=0.45, z_src=(-0.25, 0.42), after_heat=True),
        # 🔴 4회차 늘어난 변 조사: 손↔바지(RightHand 0.9/RightUpLeg 0.1)와 소매 밑면↔후드 옆(팔/Spine1) 두 무리 → 소맷부리 아래는 피부색으로 팔/옷 가름
        skin_arm=dict(texture="Yuji_baseColor.png", cuff_src={"Left": 0.19, "Right": 0.27}, x_min_src=0.10, z_min_src=-0.40),
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        textures=[("Base Color", 0, "Yuji_baseColor.png")]),
    # 주술회전 죠고(팬 제작 FBX, 뼈 없음) — 2026-09-16 희귀함. zip 안 source/Jogoat12.fbx + textures 3장. 메시 17 · 재질 23(대부분 단색) · 삼각형 약 33만 · 키 7.73(단위 m 아님).
    #   비대칭 쪼그려 선 자세(다리 넓게 · 오른팔이 더 벌어져 불씨를 쥠). 이미 −Y 정면(머리·얼굴이 y −1.63~−0.29로 앞에 튀어나온 체형).
    #   🔸 팔이 **따로 된 메시**(Realistic_White_Male_Low_Poly, 파란 피부)라 몸과 면으로 안 붙었다 — 유지·베르고식 날개 조건 아님(1회차에서 확인).
    #   뺀 것: Cube.004(오른손에 쥔 붉은 불씨 — T자로 펴면 손끝 밖으로 튀어나간다) · Material_Preview_Dummy(정점 0).
    #   관절은 원본 좌표 실측(스크래치 jogo/joints.py): 팔 메시 단면 중심 · 부츠(Cube.017 왼·Cube.019 오른) 발목 z 0.7 · 로브 다리 갈라짐 z 2.2 · 머리 중심 y −1.05.
    "희귀함_서민성": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_서민성.zip"),
        member="source/Jogoat12.fbx", textures_dir="textures",
        height=1.8, center_band=(0.0, 0.05), decimate=1.0, rotate_z=0.0, add_missing_uv=True, keep_solid_materials=True,
        drop_meshes=["Cube.004", "Material_Preview_Dummy"],
        # 33만 → 약 3.7만: 머리 Cube 10.9만·로브 Cube.016 16.3만이 대부분
        decimate_by_mesh={"Cube": 0.08, "Cube.016": 0.05, "Realistic_White_Male_Low_Poly": 0.5, "Cube.017": 0.4, "Cube.019": 0.4,
                          "Cube.026": 0.3, "Cube.006": 0.5, "Cube.001": 0.5, "Cube.002": 0.4, "Cube.003": 0.4},
        joints=dict(
            Hips=(0.0, -0.2, 2.7), Spine=(0.0, -0.1, 3.4), Spine1=(0.0, 0.0, 4.2), Spine2=(0.0, -0.05, 5.0),
            Neck=(0.0, -0.35, 6.0), Head=(0.0, -0.85, 6.4), HeadTop=(0.0, -1.05, 7.85),
            LeftShoulder=(0.3, 0.0, 5.7), LeftArm=(1.2, 0.0, 5.55), LeftForeArm=(1.39, 0.15, 4.38),
            LeftHand=(1.58, 0.30, 3.2), LeftHandTip=(1.57, 0.30, 2.34),
            RightShoulder=(-0.3, 0.0, 5.7), RightArm=(-1.25, -0.05, 5.55), RightForeArm=(-1.95, -0.06, 4.33),
            RightHand=(-2.61, -0.04, 3.2), RightHandTip=(-2.80, 0.0, 2.45),
            LeftUpLeg=(0.75, -0.2, 2.7), LeftLeg=(1.40, -0.45, 1.75), LeftFoot=(1.31, 0.24, 0.7),
            LeftToeBase=(1.40, -0.15, 0.25), LeftToeTip=(1.40, -0.38, 0.15),
            RightUpLeg=(-0.75, -0.2, 2.7), RightLeg=(-1.40, -0.45, 1.75), RightFoot=(-1.30, 0.25, 0.7),
            RightToeBase=(-1.40, -0.15, 0.25), RightToeTip=(-1.40, -0.38, 0.15)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        # 🔴 1회차 T자: 왼손이 닿아 있던 로브 옆구리가 LeftHand 가중치를 받아 검은 자락이 튀어나옴.
        #   2회차 팔 캡슐은 두꺼운 주먹·소매를 같이 잘라 소매가 찢기고 손→엉덩이 실이 생김(Idle 2.55→7.19) → 로브 재질에서 손 뼈 가중치만 뺀다
        strip_hand_on_materials=["Material.003", "Material.020"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.6, 0.9)],
        materials={"skin": [("Base Color", "Skin.png")], "Material": [("Base Color", "Eye.png")], "Material.005": [("Base Color", "cLOATKI.png")]}),
    # 주술회전 젠인 나오야(Sketchfab-16.95 glb, 뼈 없음) — 2026-09-16 희귀함. ※ 동명 다섯 중 희귀함. 메시 4조각(65,532 ×3 + 21,408 — 16비트 한계로 잘린 한 덩어리) · 삼각형 354,643.
    #   유지와 같은 형태: 기모노(아주 넓은 소매가 늘어짐) + 발목까지 오는 하카마 · 오른손을 허리 하카마에 얹음 · 왼 주먹은 앞으로 내림 · 오른발 앞으로 좁게 내디딤.
    #   관절은 원본 좌표 실측(스크래치 naoya/blobs.py·skinarm.py + 격자 렌더, 키 1.899 · 바닥 z −0.951): 피부색 손(오른 z 0.06~0.30 · 왼 z −0.07~0.05),
    #   하카마 밑 다리 두 덩어리 z −0.73 아래(앞발 y −0.11 · 뒷발 y +0.18), 어깨 z 0.52.
    #   손↔하카마 접촉은 유지 처리 그대로(피부색 가르기) · 하카마 자락은 베르고 coat_hem(Hips→UpLeg). 소매가 팔 몫이라 캡슐은 안 씀(죠고 교훈).
    "희귀함_강주혁": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_강주혁.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=0.12, rotate_z=0.0, bake_space=True,
        joints=dict(
            Hips=(0.05, 0.0, -0.02), Spine=(0.05, -0.02, 0.12), Spine1=(0.04, -0.03, 0.28), Spine2=(0.03, -0.03, 0.42),
            Neck=(0.02, -0.05, 0.57), Head=(0.02, -0.07, 0.67), HeadTop=(0.02, -0.08, 0.947),
            LeftShoulder=(0.08, -0.02, 0.53), LeftArm=(0.22, 0.0, 0.52), LeftForeArm=(0.27, -0.03, 0.27),
            LeftHand=(0.28, -0.15, 0.06), LeftHandTip=(0.28, -0.21, -0.05),
            RightShoulder=(-0.04, -0.02, 0.53), RightArm=(-0.19, 0.0, 0.52), RightForeArm=(-0.31, 0.02, 0.29),
            RightHand=(-0.22, -0.06, 0.19), RightHandTip=(-0.16, -0.09, 0.08),
            LeftUpLeg=(0.14, 0.0, -0.02), LeftLeg=(0.11, 0.10, -0.42), LeftFoot=(0.07, 0.17, -0.80),
            LeftToeBase=(0.07, 0.07, -0.92), LeftToeTip=(0.07, 0.0, -0.94),
            RightUpLeg=(-0.04, 0.0, -0.02), RightLeg=(0.01, -0.07, -0.42), RightFoot=(0.025, -0.10, -0.80),
            RightToeBase=(0.01, -0.20, -0.92), RightToeTip=(0.0, -0.30, -0.94)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        skin_arm=dict(texture="Naoya_baseColor.png", cuff_src={"Left": 0.09, "Right": 0.30}, x_min_src=0.12, z_min_src=-0.2),
        # 🔴 1회차: T자에서 손·아래팔이 하카마 허리·기모노 옆구리를 끌고(늘어난 변이 ForeArm/Spine·Hips 섞임, 원본 z 0~0.33) Idle에서 하카마 주름이 가운데서 찢김(11.67).
        #   하카마 회색과 셔츠 소맷부리 흰색은 밝기가 겹쳐(중앙 0.61~0.82 vs ~0.79) 색으로 못 가른다 → 유지식 접촉 띠 가르기 + 법선 마스크, 자락 좌우 섞임은 넓게.
        arm_split=dict(sides=("Left", "Right"), radius_src=0.07, facing=0.45, z_src=(-0.25, 0.40), after_heat=True),
        arm_facing=dict(lo=-0.1, hi=0.2, smooth=2),
        # 🔸 넓은 소매 기모노 교훈(PM 요청으로 남김): ① bone heat는 **면 연결이 아니라 근접으로** 번진다 — 붙은 면 가르기·법선 마스크로는 아래팔 옆 몸판·하카마에 샌 팔 몫이 안 없어진다.
        #   ② 하카마 회색 ↔ 셔츠 흰색은 **밝기로 못 가르고**(0.61~0.82 vs ~0.79 겹침), 기모노 남색 ↔ 무채색은 **채도로 갈린다**(split_by_saturation).
        # ⏸ 2026-09-16 보류(4회차까지): T자에서 손·아래팔이 하카마 허리·기모노 몸판을 끌어 옆판이 벌어지고 주름이 찢긴다 — 아직 못 잡음. 2회차 설정으로 되돌려 둠.
        #   2회차 Idle 14.23(상위1% 1.80) · 팔45° 10.37(1.56) · T 가로 1.532(기준 1.62 미달).
        #   3회차 몸통 타원 기둥 팔 몫 0(strip_arm_in_torso): 상위1% 그대로, 최대 63은 0.08 mm 퇴화 변(무의미).
        #   4회차 하카마 넓은 타원 + 자락 규칙을 띠 위~밑단 아래로: 팔45° 상위1% 6.18 · Idle 2.78로 **나빠짐**(하카마가 무릎을 못 따라감).
        #   남은 가설: 넓은 소매 늘어짐이 하카마 옆과 가까워 bone heat가 서로 섞음 — 소매(남색) 늘어짐 아래 끝을 하카마(회색)와 색으로 가르거나, 팔을 덜 펴는 T자.
        # ✅ 2026-09-17 재빌드(사장님 「찌그러져 있다」 — 유니티 Idle 소매 뭉침·하카마 허리가 손 쪽으로 집힘·옆판 비틀림):
        #   늘어난 변 조사(스크래치 naoya2/diag.py — 어느 뼈 몫 차이가 늘어난 변을 만드나): 하카마 밑단 z 0.2~0.3의 UpLeg/Leg 차이 + 허리 z 1.1의 RightHand 몫이 두 무리.
        #   ① strip_bones_nonskin: 허리 구역 피부색 아닌 정점에서 손 몫 빼기(허리 무리 395 → 80 변)
        #   ② coat_hem share 0.6 → 0.3(하카마가 다리보다 엉덩이를 더 따라감 — 밑단 무리 줄임. 0.15·띠 0.12는 오히려 나빠짐)
        #   ③ arm_drop_deg 35(PM 허용 — 팔 내린 쪽이 유니티 Idle에 가까움)
        #   ④ 🔴 delete_stretched_faces ratio 2.5 + 구멍 메우기: 손·아래팔이 하카마·기모노에 면으로 붙은 스캔이라 T자로 펴면 **쉬는 자세 자체가 손→허리 실 다발**
        #     (공용 Idle 비교는 쉬는 자세 기준이라 이 찢김을 못 셈) → 펴기 전후 변 길이 2.5배 넘게 늘어난 면(원래 손이 덮은 접촉면) 1,611개 지움.
        #   결과 Idle 상위1% 1.80 → 1.512 · 팔45° 1.56 → 1.61(하카마 옆판 닫힘) · 원 판 Idle의 하카마 허리 찢김·집힘 사라짐.
        #   시험(naoya2/): A 팔 안 펴기 4.68(나빠짐) · B 구역 가중치 고르기 1.92 · C 손 빼기+share 0.3 1.547 · E 띠 넓힘 1.72 · I 작은 섬 지우기(주먹까지 지워져 버림).
        coat_hem=dict(hip_src=-0.02, hem_src=-0.73, band_src=0.05, center_x_src=0.05, split_src=0.15, share=0.3),
        strip_bones_nonskin=dict(texture="Naoya_baseColor.png", bones=["RightHand", "LeftHand"], z_src=(-0.35, 0.45)),
        arm_drop_deg=35,
        delete_stretched_faces=dict(ratio=2.5, fill_holes=True, fill_max_sides=3000),
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        textures=[("Base Color", 0, "Naoya_baseColor.png")]),
    # 체인소맨 덴지(사람 모습, Sketchfab-16.6 glb 뼈 없음) — 2026-09-16 희귀함. ※ 동명 여섯 중 희귀함. 메시 5 · 삼각형 28,293 · 이미지 6 · 키 74.6(cm 계열).
    #   재질 이름이 섞여 여러 모델을 합친 것처럼 보이지만 렌더·메시별 렌더로 확인하니 **겹친 몸 없음**: Object_2 몸(셔츠·넥타이·바지) · 3 얼굴 · 4 머리카락 ·
    #   5 운동화(01___Default) · 6 팔·손(21_body). 이미 좌우 대칭 T자 · −Y 정면. 팔이 따로 된 메시(소매 끝 x ±20)라 날개 조건 아님.
    #   관절은 원본 좌표(스크래치 denji2/joints.py + 렌더 비율): 팔 z 57 수평 · 어깨 ±7 · 팔꿈치 ±19 · 손목 ±31 · 손끝 ±37 · 다리 x ±3.8 · 고관절 z 38 · 무릎 21 · 발목 4.5.
    "희귀함_박민수": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_박민수.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=1.0, rotate_z=0.0,
        # 🔴 1회차: bone heat 전체 실패(가중치 0/14,254). 복셀 대리 메시(닫힌 한 덩어리)에서도 실패 → 메시별로 떼어 시험하니 **얼굴 Object_3**이 원인
        #   (얼굴 빼면 8,424/8,424 · 얼굴 혼자 3,442/5,863) → 얼굴은 heat에서 빼고 Head 100%.
        rigid_meshes={"Object_3": "Head"},
        # 얼굴 5,863 + 머리카락 4,539정점이 저폴리 몸(1,465)보다 훨씬 촘촘해 Head 몫이 73%로 나온다(실패 아님 — 22뼈 전부 가중치 있음, 코알라와 같은 경우)
        max_bone_share=0.8,
        joints=dict(
            Hips=(0.0, 1.8, 38.0), Spine=(0.0, 1.5, 43.0), Spine1=(0.0, 1.3, 48.0), Spine2=(0.0, 1.3, 53.0),
            Neck=(0.0, 1.5, 59.0), Head=(0.0, 1.3, 62.0), HeadTop=(0.0, 1.2, 72.5),
            LeftShoulder=(1.5, 1.5, 57.5), LeftArm=(7.0, 1.8, 57.5), LeftForeArm=(19.0, 2.3, 57.2),
            LeftHand=(31.0, 2.2, 56.7), LeftHandTip=(37.0, 2.3, 56.5),
            RightShoulder=(-1.5, 1.5, 57.5), RightArm=(-7.0, 1.8, 57.5), RightForeArm=(-19.0, 2.3, 57.2),
            RightHand=(-31.0, 2.2, 56.7), RightHandTip=(-37.0, 2.3, 56.5),
            LeftUpLeg=(3.5, 1.8, 38.0), LeftLeg=(3.8, 1.5, 21.0), LeftFoot=(4.1, 1.8, 4.5),
            LeftToeBase=(4.1, -2.5, 0.5), LeftToeTip=(4.1, -5.0, -1.0),
            RightUpLeg=(-3.5, 1.8, 38.0), RightLeg=(-3.8, 1.5, 21.0), RightFoot=(-4.1, 1.8, 4.5),
            RightToeBase=(-4.1, -2.5, 0.5), RightToeTip=(-4.1, -5.0, -1.0)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={0: "denji_body_baseColor.png", 1: "denji_body_normal.png", 2: "denji_face_baseColor.png", 3: "denji_hair_baseColor.png",
                    4: "denji_shoes_baseColor.png", 5: "denji_arms_baseColor.png"},
        materials={"BODY": [("Base Color", "denji_body_baseColor.png")], "FACE": [("Base Color", "denji_face_baseColor.png")],
                   "HAIR": [("Base Color", "denji_hair_baseColor.png")], "01___Default": [("Base Color", "denji_shoes_baseColor.png")],
                   "21_body_1_0_0": [("Base Color", "denji_arms_baseColor.png")]}),
    # 근육 거구 빅 가이(Sketchfab glb, 뼈 없음) — 2026-09-16 희귀함. 메시 16 · 재질 1(body, 이미지 5: 0 기본색·1 금속거칠기·2 발광·3 노멀·4 반사) · 키 9.31(단위 m 아님).
    #   몸만 재면 좌우 대칭 100% · 팔을 무릎까지 늘어뜨리고 상체를 앞으로 숙인 고릴라형 · −Y 정면.
    #   🔴 뺄 것: 사슬 chain_1~13(+x 손목에서 바닥까지) · gun_L(바닥에 놓인 쇠공 — 이름과 달리 사슬 끝 추). 팔찌 bracer는 남긴다(손목에 heat로 붙음).
    #   관절은 원본 좌표 실측(스크래치 bigguy/joints.py 단면 + grid.py 앞·옆 렌더 픽셀, 0.01024/px): 팔찌 중심 = 손목(±3.25, −0.28, −3.98) ·
    #   어깨 ±1.9 · 팔꿈치(±3.07, −0.32, −2.39) · 손끝 z −5.8 · 고관절 z −3.82 · 무릎 −5.36 · 발목 −6.79 · 앞으로 숙인 머리 중심 y −2.27.
    "희귀함_이상혁": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_이상혁.glb"),
        height=1.8, center_band=(0.0, 0.05), decimate=1.0, rotate_z=0.0,
        drop_meshes=["pasted__chain_1_body_0", "pasted__chain_2_body_0", "pasted__chain_3_body_0", "pasted__chain_4_body_0", "pasted__chain_5_body_0", "pasted__chain_6_body_0", "pasted__chain_7_body_0", "pasted__chain_8_body_0", "pasted__chain_9_body_0", "pasted__chain_10_body_0", "pasted__chain_11_body_0", "pasted__chain_12_body_0", "pasted__chain_13_body_0", "pasted__gun_L_body_0"],
        joints=dict(
            Hips=(0.0, -1.2, -3.82), Spine=(0.0, -1.0, -2.8), Spine1=(0.0, -0.7, -1.8), Spine2=(0.0, -0.6, -0.8),
            Neck=(0.0, -1.5, -0.05), Head=(0.0, -2.1, 0.35), HeadTop=(0.0, -2.3, 1.76),
            LeftShoulder=(0.6, -0.8, -0.2), LeftArm=(1.9, -0.3, -0.15), LeftForeArm=(3.07, -0.32, -2.39),
            LeftHand=(3.25, -0.28, -3.98), LeftHandTip=(2.8, 0.3, -5.8),
            RightShoulder=(-0.6, -0.8, -0.2), RightArm=(-1.9, -0.3, -0.15), RightForeArm=(-3.07, -0.32, -2.39),
            RightHand=(-3.25, -0.28, -3.98), RightHandTip=(-2.8, 0.3, -5.8),
            LeftUpLeg=(0.9, -1.2, -3.82), LeftLeg=(1.1, -1.35, -5.36), LeftFoot=(1.33, -0.83, -6.79),
            LeftToeBase=(1.33, -1.3, -7.3), LeftToeTip=(1.33, -1.75, -7.45),
            RightUpLeg=(-0.9, -1.2, -3.82), RightLeg=(-1.1, -1.35, -5.36), RightFoot=(-1.33, -0.83, -6.79),
            RightToeBase=(-1.33, -1.3, -7.3), RightToeTip=(-1.33, -1.75, -7.45)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.7, 0.9)],
        textures=[("Base Color", 0, "bigguy_baseColor.png")]),
    # 보디빌더(Sketchfab-16.74 glb, 뼈 없음) — 2026-09-16 희귀함(구현담당2에서 막혀 넘어옴). 메시 3(65,532 ×2 + 47,686 — 16비트로 잘린 한 덩어리) · 삼각형 302,223 · 키 2.0 · 좌우 대칭 100%.
    #   팔을 몸에 붙인 차렷 자세: 손이 허벅지 옆(z −0.25~−0.05)·위팔이 옆구리(z 0.30~0.60)에 닿고, 🔴 스캔이 그 자리를 **실제 면으로 이어 붙였다** → T자에서 손→허벅지 리본.
    #   둘 다 피부라 유지식 색 가르기 불가 → delete_bridge_faces(heat 뒤 팔 몫이 갈리는 다리 놓은 면 삭제). 합친 뒤 이음새 붙이기(remove_doubles)는 build가 한다.
    #   관절은 원본 좌표(스크래치 bodybuilder/blobs.py 단면 + 격자 렌더): 손목(±0.364, −0.03, 0.008) · 팔꿈치(±0.405, 0.25) · 어깨 관절(±0.28, 0.55) · 고관절(±0.10, −0.02) ·
    #   무릎(±0.17, −0.44) · 발목(±0.19, +0.09, −0.89) · 목 0.73 · 머리 0.78 · 꼭대기 0.998.
    "희귀함_선효진": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_선효진.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=0.12, rotate_z=0.0,
        joints=dict(
            Hips=(0.0, 0.0, -0.02), Spine=(0.0, 0.0, 0.10), Spine1=(0.0, 0.0, 0.28), Spine2=(0.0, 0.0, 0.46),
            Neck=(0.0, -0.03, 0.70), Head=(0.0, -0.05, 0.78), HeadTop=(0.0, -0.05, 0.998),
            LeftShoulder=(0.10, 0.0, 0.62), LeftArm=(0.28, 0.0, 0.55), LeftForeArm=(0.405, -0.02, 0.25),
            LeftHand=(0.364, -0.03, 0.008), LeftHandTip=(0.30, -0.08, -0.19),
            RightShoulder=(-0.10, 0.0, 0.62), RightArm=(-0.28, 0.0, 0.55), RightForeArm=(-0.405, -0.02, 0.25),
            RightHand=(-0.364, -0.03, 0.008), RightHandTip=(-0.30, -0.08, -0.19),
            LeftUpLeg=(0.10, 0.0, -0.02), LeftLeg=(0.17, -0.01, -0.44), LeftFoot=(0.19, 0.09, -0.89),
            LeftToeBase=(0.21, -0.08, -0.97), LeftToeTip=(0.24, -0.18, -0.99),
            RightUpLeg=(-0.10, 0.0, -0.02), RightLeg=(-0.17, -0.01, -0.44), RightFoot=(-0.19, 0.09, -0.89),
            RightToeBase=(-0.21, -0.08, -0.97), RightToeTip=(-0.24, -0.18, -0.99)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        # 🔴 1회차: 지운 면 5개뿐 · T자 리본 그대로 — bone heat가 허벅지 **표면 전체**에 Hand·ForeArm 몫을 점진적으로 퍼뜨려 한 면이 0.4→0.6을 넘는 경우가 드물었다.
        #   → 2회차: 다리·엉덩이 타원 기둥(손 안쪽 가장자리 x 0.29 직전까지) 안의 팔 몫을 먼저 0으로 만들어 경계를 날카롭게 한 뒤 다리 놓은 면을 지운다.
        strip_arm_in_torso=[dict(center_src=(0.0, -0.01), rx_src=0.285, ry_src=0.20, z_src=(-0.95, 0.10), blend_src=0.015)],
        # 🔴 2회차: 허벅지는 제자리로 왔지만 손→허벅지 바깥면 삼각 막이 남음(굵은 허벅지 바깥면이 x 0.285 밖이라 팔 몫 유지) → 반지름 정규화 거리로 가름
        #   3회차: 리본은 사라졌지만 **손이 허벅지 쪽으로 분류돼 엉덩이에 남음**(손 반지름 0.05가 작아 굵은 허벅지가 이김) → 손·아래팔 반지름 키우고 팔 쪽으로 기울임
        arm_leg_radius=dict(z_src=(-0.40, 0.12), arm_r=(0.08, 0.08, 0.09), leg_r=0.14, leg_bias=0.85, arm_side_strip=("Hips", "Spine")),
        # 4회차: 손은 팔을 따라왔지만 왼손→허벅지 가는 실 하나(팔45° 11.3) — 경계 0.4~0.6 사이 정점 면이 남음 → 0.5 기준 양쪽이면 지우고 띠는 손 접촉 높이로 좁힘(팔꿈치 전이 면 보호)
        #   6회차: 가시 사라짐(Idle 상위1% 1.30) · 지운 자리가 허벅지 바깥 구멍으로 보임 → 띠 안 경계 고리 메우기
        delete_bridge_faces=dict(z_src=(-0.35, 0.08), x_min_src=0.18, arm_hi=0.5, arm_lo=0.5, after_masks=True, fill_holes=True),
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        textures=[("Base Color", 0, "bodybuilder_baseColor.png")]),
    # 원피스 흰수염 에드워드 뉴게이트(OBJ, Blender 4.3 내보냄, 뼈 없음) — 2026-09-17 전설적인. zip 안 zip 안 OBJ · 텍스처는 바깥 zip textures/ 해시 이름 PNG 2장(1024² RGB).
    #   그룹 3: 몸(재질 edward001_cloak_d.001) · 얼굴(같은 재질) · 망토(Material.001). MTL은 zip에 없다 → 텍스처는 UV로 두 장을 다 입혀 렌더해 짝지음(whitebeard/ok_front·back):
    #     e027c45c… = 몸·얼굴·대검(피부·바지·수염) · f6e8c483… = 망토(겉 흰색·해골 마크, 안감 붉은 비늘).
    #   🔴 대검이 몸 그룹에 느슨한 조각 16개로 들어 있다(자루 x −54~−118, 머리 위 z 381까지) → drop_loose_parts(조각 중심 x < −50, 부츠 중심은 ±36).
    #   관절은 원본 좌표(단위 cm쯤, 키 263 — whitebeard/slices.py 단면 + grid_front·side 10 격자): 오른팔(−X)은 대검 쥔 채 팔꿈치 굽혀 주먹이 앞(y −24)·z 146,
    #   왼팔은 옆에 내려 주먹 z 128. 망토는 어깨에 걸쳐 뒤로 늘어지고 빈 소매가 등 뒤(y +50~73)에 매달려 있다. 정면 −Y(망토가 +Y).
    #   center_band는 발바닥 0~3%만(망토 자락이 z 10부터라 넣으면 중심이 뒤로 35 밀린다).
    "전설적인_정준영": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/06_전설적인/전설적인_정준영.zip"),
        member=["source/One Piece - WhiteBeard.zip", "One Piece - WhiteBeard.obj"], textures_dir="textures",
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        drop_loose_parts=[dict(mesh="edward001_body_d_edward001_body_d_0", x_max=-50.0)],
        # 🔴 1회차: bone heat 전체 실패(가중치 0). 메시별 시험(whitebeard/heat_test.py): 몸 2,083/2,115 · 얼굴 1,058/1,154 · **망토 0/2,060** → 망토가 전체를 망친다.
        #   망토는 어깨에 걸친 케이프(빈 소매가 등 뒤에 매달림)라 팔을 안 따라가는 게 맞다 → heat에서 빼고 Spine2 100%.
        rigid_meshes={"edward001_cloak_d_edward001_cloak_d_0": "Spine2"},
        # 🔴 1회차(망토 고정 후): 왼허리 띠 자락(떨어진 조각 67정점, x 9~35 · z 114~171)이 LeftHand 몫이라 T자에서 팔 따라 떠올랐다 → Hips 100%
        rigid_loose_parts=[dict(box_src=((5.0, -30.0, 110.0), (40.0, -3.0, 175.0)), bone="Hips")],
        joints=dict(
            Hips=(0.0, 0.0, 128.0), Spine=(0.0, 0.0, 148.0), Spine1=(0.0, -2.0, 170.0), Spine2=(0.0, -2.0, 192.0),
            Neck=(0.0, -2.0, 225.0), Head=(0.0, -4.0, 238.0), HeadTop=(0.0, -4.0, 263.0),
            LeftShoulder=(10.0, 5.0, 212.0), LeftArm=(42.0, 10.0, 207.0), LeftForeArm=(51.0, 14.0, 172.0),
            LeftHand=(53.0, 4.0, 140.0), LeftHandTip=(52.0, 2.0, 122.0),
            RightShoulder=(-10.0, 5.0, 212.0), RightArm=(-42.0, 8.0, 207.0), RightForeArm=(-60.0, 4.0, 172.0),
            RightHand=(-72.0, -15.0, 158.0), RightHandTip=(-84.0, -26.0, 138.0),
            LeftUpLeg=(15.0, 0.0, 125.0), LeftLeg=(24.0, 4.0, 85.0), LeftFoot=(35.0, 12.0, 22.0),
            LeftToeBase=(38.0, -5.0, 5.0), LeftToeTip=(40.0, -14.0, 2.0),
            RightUpLeg=(-15.0, 0.0, 125.0), RightLeg=(-24.0, 4.0, 85.0), RightFoot=(-35.0, 12.0, 22.0),
            RightToeBase=(-38.0, -5.0, 5.0), RightToeTip=(-40.0, -14.0, 2.0)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        materials={"edward001_cloak_d.001": [("Base Color", "e027c45cd4924407bfea05b73ba02555_RGB_edwar.png")],
                   "Material.001": [("Base Color", "f6e8c483efd8400e9c86868f7dc8bf2c_RGB_edwar.png")]}),
    # 나루토 우치하 오비토(토비 — 아카츠키 로브·소용돌이 가면, Sketchfab glb, 뼈 없음) — 2026-09-17 전설적인. 메시 15(부위별 빈 오브젝트 부모) · 삼각형 157,211 · 키 4.68(단위 m 아님) · 좌우 대칭.
    #   🔴 **팔·손이 모델에 없다** — 로브(Red_suit·Black_suit 한 덩어리 저폴리 껍데기)가 소매까지 한 실루엣으로 닫혀 있고 소맷부리가 허벅지 높이(z −0.41, x ±0.9)에 늘어져 있다.
    #   무거운 둘: 가면 Object_10(63,808삼각형) · 머리카락 Object_27(69,644) → 조각별 감량 + 얼굴 부속 전부 rigid Head(하나미 교훈: 세게 깎으면 heat 전체 실패).
    #   관절은 원본 좌표(obito/grid_front·side 0.1 격자): 발바닥 −2.51 · 로브 자락 −1.9 · 어깨 모서리 (±0.56, 1.2) · 목깃 1.62~1.8 · 꼭대기 2.17. 다리는 x ±0.52로 벌려 섰다.
    #   텍스처 2장(Image_0 가면 · Image_1 아카츠키 구름), 나머지 재질은 단색 → keep_solid_materials.
    "전설적인_김민규": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/06_전설적인/전설적인_김민규.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=1.0, rotate_z=0.0,
        decimate_by_mesh={"Object_10": 0.15, "Object_27": 0.15},
        rigid_meshes={"Object_10": "Head", "Object_27": "Head", "Object_25": "Head", "Object_4": "Head", "Object_6": "Head", "Object_7": "Head", "Object_8": "Head"},
        max_bone_share=0.8,
        joints=dict(
            Hips=(0.0, 0.0, -0.15), Spine=(0.0, 0.0, 0.25), Spine1=(0.0, 0.0, 0.65), Spine2=(0.0, 0.0, 1.0),
            Neck=(0.0, -0.02, 1.40), Head=(0.0, -0.05, 1.62), HeadTop=(0.0, -0.05, 2.17),
            LeftShoulder=(0.12, 0.0, 1.30), LeftArm=(0.50, 0.0, 1.15), LeftForeArm=(0.72, 0.0, 0.40),
            LeftHand=(0.86, -0.05, -0.35), LeftHandTip=(0.90, -0.05, -0.60),
            RightShoulder=(-0.12, 0.0, 1.30), RightArm=(-0.50, 0.0, 1.15), RightForeArm=(-0.72, 0.0, 0.40),
            RightHand=(-0.86, -0.05, -0.35), RightHandTip=(-0.90, -0.05, -0.60),
            LeftUpLeg=(0.28, 0.0, -0.20), LeftLeg=(0.42, 0.0, -1.20), LeftFoot=(0.52, 0.08, -2.20),
            LeftToeBase=(0.53, -0.25, -2.45), LeftToeTip=(0.55, -0.45, -2.49),
            RightUpLeg=(-0.28, 0.0, -0.20), RightLeg=(-0.42, 0.0, -1.20), RightFoot=(-0.52, 0.08, -2.20),
            RightToeBase=(-0.53, -0.25, -2.45), RightToeTip=(-0.55, -0.45, -2.49)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        # 🔴 1회차: T자로 펴니 로브 앞판이 팔 따라 붉은 날개로 펼쳐짐(obito/r1). 2회차: 몸통 타원 안 팔 몫 0 → 소매·옆판이 한 면이라 더 큰 막(r2, 팔45° 최대 19.5).
        #   → 3회차: **팔이 없는 모델이니 로브 전체에서 팔(Arm·ForeArm·Hand) 몫을 뺀다**(상자 전체 타원) — 로브는 어깨·척추·다리만 따라가고 T자에서 닫힌 채(r3).
        #   팔 뼈 6개는 가중치 0으로 남는다(휴머노이드 계층용 — 그림죠 가중치 0 팔 뼈도 유니티 통과).
        strip_arm_in_torso=[dict(center_src=(0.0, 0.0), rx_src=5.0, ry_src=5.0, z_src=(-3.0, 3.0), blend_src=0.06)],
        # 🔴 3회차 유니티 반려(isHuman False — 가중치 0 팔 뼈가 스킨 뼈 목록에서 빠져 매핑 16) → 4회차: 가중치 0 뼈마다 가까운 정점 3개에 0.001
        seed_zero_bones=0.001,
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={0: "obito_mask_baseColor.png", 1: "obito_suit_baseColor.png"},
        materials={"Material": [("Base Color", "obito_mask_baseColor.png")], "Suit": [("Base Color", "obito_suit_baseColor.png")],
                   "Black_suit": [("Base Color", "obito_suit_baseColor.png")]},
        keep_solid_materials=True),
    # 원피스 포트거스 D. 에이스(Sketchfab Substance 저장 FBX, 뼈 없음) — 2026-09-17 전설적인. 🔴 재빌드: 구현담당2 gen_objrip_skin.py 커밋본(42f9b8c8)이 유니티 Idle에서
    #   손목이 바깥으로 꺾이고 손바닥이 밖·손가락이 벌어짐(윤곽 스캔으로 손목 자리를 못 잡음 — 원본 키 0.1058·배율 17배·손 정점 40개 안팎).
    #   zip 안 source/ace_pasar_substance_low_02.fbx + textures/ Substance 맵(BaseColor 5장만 씀) · 메시 23(전부 몸에 붙은 의상·소품) · 재질 5 · 좌우 대칭 · 정면 −Y · A자.
    #   관절은 원본 좌표 실측(스크래치 ace/slices.py — ace_l 몸 메시를 x 1 mm 띠로 잘라 팔 중심·굵기): 겨드랑이 x 0.011부터 팔 ·
    #     어깨 (±0.0085, 0.1385) · 팔꿈치 = 팔꿈치 보호대 codonera 중심 x 0.0175 옆 가장 가는 곳 (±0.0185, 0.1295) ·
    #     🔴 손목 = 팔찌 pulseras 중심(0.0263, 0.1191) 바깥 가장 가는 단면(x 0.028 굵기 4.2) (±0.0275, 0.1175) · 손끝 (±0.0318, 0.110).
    #     다리: 고관절 (±0.0055, 0.104) · 무릎 (±0.0085, 0.083) · 발목 (±0.0097, 0.066) · 발바닥 0.0571.
    #   텍스처: 커밋본 대응 그대로(cuerpo=_7 · detalles=_13 · ropa=_1 · bolas=무번호 · cinturones=_19), 파일 이름도 커밋본 그대로($ 뗌).
    "전설적인_엄태웅": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/06_전설적인/전설적인_엄태웅.zip"),
        member="source/ace_pasar_substance_low_02.fbx", textures_dir="textures",
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        # 🔴 1회차: 23조각 합친 몸에 bone heat → 9,136정점 실패, 가까운 가중치 정점 복사가 A자 손 옆 반바지 단 고리(좌우 1,296정점)·홀스터 조각에 손 몫 0.9 → T자에서 공중에 뜸.
        #   2회차: 복셀 대리 메시 heat 전체 실패. 3회차: 허벅지·홀스터 타원 안 팔 몫 0 → 제자리지만 채운 경계에서 반바지가 찢겨 Idle 상위1% 1.91.
        #   → 4회차: heat 대리 = 몸 ace_l + 신발 두 조각만(4,910정점 전부 가중치) → 나머지 옷·소품은 가장 가까운 몸 표면 가중치 보간. Idle 상위1% 1.34.
        heat_proxy=dict(keep_meshes=["ace_l", "zapatos_l", "suelazapatos_l"]),
        # 🔴 4회차 Idle: 왼허리 홀스터(pistola·detallespistola)가 A자 왼아래팔 바로 옆이라 팔 몫을 받아 Idle에서 팔꿈치 높이로 딸려 올라감 → Hips 100%
        rigid_meshes={"pelo_l": "Head", "sombrero_l": "Head", "pistola_l": "Hips", "detallespistola_e": "Hips"},
        joints=dict(
            Hips=(0.0, 0.001, 0.106), Spine=(0.0, 0.001, 0.113), Spine1=(0.0, 0.001, 0.122), Spine2=(0.0, 0.002, 0.131),
            Neck=(0.0, 0.001, 0.1440), Head=(0.0, 0.0, 0.1490), HeadTop=(0.0, 0.0, 0.1629),
            LeftShoulder=(0.002, 0.003, 0.1415), LeftArm=(0.0085, 0.005, 0.1385), LeftForeArm=(0.0185, 0.0045, 0.1295),
            LeftHand=(0.0275, 0.0008, 0.1175), LeftHandTip=(0.0318, 0.0, 0.1100),
            RightShoulder=(-0.002, 0.003, 0.1415), RightArm=(-0.0085, 0.005, 0.1385), RightForeArm=(-0.0185, 0.0045, 0.1295),
            RightHand=(-0.0275, 0.0008, 0.1175), RightHandTip=(-0.0318, 0.0, 0.1100),
            LeftUpLeg=(0.0055, 0.001, 0.104), LeftLeg=(0.0085, 0.0035, 0.083), LeftFoot=(0.0097, 0.0047, 0.066),
            LeftToeBase=(0.0100, -0.004, 0.0590), LeftToeTip=(0.0100, -0.009, 0.0580),
            RightUpLeg=(-0.0055, 0.001, 0.104), RightLeg=(-0.0085, 0.0035, 0.083), RightFoot=(-0.0097, 0.0047, 0.066),
            RightToeBase=(-0.0100, -0.004, 0.0590), RightToeTip=(-0.0100, -0.009, 0.0580)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        seed_zero_bones=0.001,
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        texture_sources={"_BaseColor_7.png": "$_BaseColor_7.png", "_BaseColor_13.png": "$_BaseColor_13.png", "_BaseColor_1.png": "$_BaseColor_1.png",
                         "_BaseColor.png": "$_BaseColor.png", "_BaseColor_19.png": "$_BaseColor_19.png"},
        materials={"cuerpo": [("Base Color", "_BaseColor_7.png")], "detalles": [("Base Color", "_BaseColor_13.png")], "ropa": [("Base Color", "_BaseColor_1.png")],
                   "bolas": [("Base Color", "_BaseColor.png")], "cinturones": [("Base Color", "_BaseColor_19.png")]}),
    # 나루토 하루노 사쿠라(Sketchfab-15.47 glb, 뼈 없음) — 2026-09-17 전설적인. 구현담당1에서 두 번 실패해 이관(① 텍스처 없는 벌을 남김 → 흰 인형 ② 뼈가 한 점에 몰림·배율 0.03).
    #   🔴 메시 20 = **같은 몸 세 벌 완전 겹침**(Object_4~9 · 10~15 · 16~21, 자리·정점 수 동일, 세 벌 다 KHR spec-gloss diffuseTexture로 Image_0/1 연결) → 첫 벌만 남김.
    #   🔴 Object_22/23(Image_2, 단위 m — 나머지는 cm)은 ×100으로 맞춰 보니 **다른 캐릭터의 긴 검은 포니테일**(뒤로 1 m 뻗음, 사쿠라는 짧은 분홍 머리) → 뺌.
    #   관절은 원본 좌표(단위 cm, 키 162.7 — sakura/slices.py x 3 cm 띠 + look_front 렌더): 어깨 (±15.5, 125) · 팔꿈치 (±32.5, 107.5) · 손목 = 장갑 시작 (±49, 89) · 손끝 (±59, 81.5) ·
    #     고관절 (±8.5, 82) · 무릎 (±14, 45) · 발목 (±20.7, 9) — 다리를 벌려 섬. 팔 A자(수평 아래 약 47°).
    "전설적인_진연서": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/06_전설적인/전설적인_진연서.glb"),
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        drop_meshes=[f"Object_{i}" for i in range(10, 24)],
        joints=dict(
            Hips=(0.0, 1.0, 84.0), Spine=(0.0, 1.0, 95.0), Spine1=(0.0, 0.0, 106.0), Spine2=(0.0, 0.0, 117.0),
            Neck=(0.0, 0.0, 133.0), Head=(0.0, 0.0, 139.0), HeadTop=(0.0, 0.0, 162.3),
            LeftShoulder=(3.0, 0.0, 131.0), LeftArm=(15.5, 1.0, 125.0), LeftForeArm=(32.5, 1.0, 107.5),
            LeftHand=(49.0, 0.5, 89.0), LeftHandTip=(59.0, 1.8, 81.5),
            RightShoulder=(-3.0, 0.0, 131.0), RightArm=(-15.5, 1.0, 125.0), RightForeArm=(-32.5, 1.0, 107.5),
            RightHand=(-49.0, 0.5, 89.0), RightHandTip=(-59.0, 1.8, 81.5),
            LeftUpLeg=(8.5, 1.0, 82.0), LeftLeg=(14.0, 0.0, 45.0), LeftFoot=(20.7, 2.0, 9.0),
            LeftToeBase=(21.0, -8.0, 3.0), LeftToeTip=(21.0, -15.0, 1.0),
            RightUpLeg=(-8.5, 1.0, 82.0), RightLeg=(-14.0, 0.0, 45.0), RightFoot=(-20.7, 2.0, 9.0),
            RightToeBase=(-21.0, -8.0, 3.0), RightToeTip=(-21.0, -15.0, 1.0)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        seed_zero_bones=0.001,
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={0: "material_0_diffuse.png", 1: "material_1_diffuse.png"},
        materials={m: [("Base Color", "material_0_diffuse.png")] for m in ("material_0", "material_3", "material_4", "material_5")}
                  | {m: [("Base Color", "material_1_diffuse.png")] for m in ("material_1", "material_2")}),
    # 블리치 거인학살자(사무라이 소드 — 긴 코트, Sketchfab glb, 뼈 없음) → 희귀함_박도진. 🔴 2026-09-17 재빌드(사장님 「망토가 이상함」): 구현담당2 gen_objrip_skin.py 커밋본은
    #   bone heat 21뼈 전부 실패 → 지오데식 대체라 코트에 오른팔 몫이 커서 유니티 Idle에서 코트가 머리 뒤·오른쪽 위로 솟았다.
    #   메시 10: Object_2 머리카락 · 3/4 **정점 전부 한 점에 뭉친 퇴화 조각**(뺌) · 5 눈썹 · 6 얼굴 · 7 **긴 코트(소매 달림, jacket_katan)** · 8 속옷·바지·소맷부리(jacket_katana) ·
    #   9 katana_hair 파편(뺌) · 10 손 · 11 신발. 원본 키 71.52 · 이미 T자 · 대칭 · 정면 −Y. 망토가 아니라 소매 달린 코트라 Spine2 강체는 안 됨(소매가 팔을 따라가야 함).
    #   → heat 대리 = 속옷·손·신발·얼굴만(에이스 방식), 코트·머리카락은 가까운 몸 표면 보간 · 코트 허리 아래 자락만 coat_hem(속 바지는 무릎 따라감).
    #   관절은 원본 좌표 실측(스크래치 dojin/slices.py): 팔 z 57.5 수평 · 어깨 ±6.5 · 팔꿈치 ±17 · 손목 ±28.5(소맷부리 끝·손 시작) · 손끝 ±37 · 엉덩이 44 · 무릎 24 · 발목 5 · 다리 x ±3.9.
    "희귀함_박도진": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_박도진.glb"),
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        drop_meshes=["Object_3", "Object_4", "Object_9"],
        # 🔴 1회차: 대리 메시에서도 heat 전체 실패 → 메시별 시험(dojin/heat_test.py): **얼굴 Object_6 혼자 0/5,838**(비다양체 1,448), 나머지는 전부 성공 → 얼굴은 Head 강체(덴지와 같음)
        heat_proxy=dict(keep_meshes=["Object_8", "Object_10", "Object_11"]),
        rigid_meshes={"Object_2": "Head", "Object_5": "Head", "Object_6": "Head"},
        joints=dict(
            Hips=(0.0, 1.5, 44.0), Spine=(0.0, 1.5, 48.5), Spine1=(0.0, 1.5, 53.0), Spine2=(0.0, 1.5, 56.5),
            Neck=(0.0, 1.0, 60.5), Head=(0.0, 0.6, 62.5), HeadTop=(0.0, 0.6, 70.76),
            LeftShoulder=(1.5, 2.0, 58.5), LeftArm=(6.5, 2.4, 57.9), LeftForeArm=(17.0, 2.4, 57.7),
            LeftHand=(28.5, 2.0, 57.4), LeftHandTip=(37.0, 1.5, 57.6),
            RightShoulder=(-1.5, 2.0, 58.5), RightArm=(-6.5, 2.4, 57.9), RightForeArm=(-17.0, 2.4, 57.7),
            RightHand=(-28.5, 2.0, 57.4), RightHandTip=(-37.0, 1.5, 57.6),
            LeftUpLeg=(3.2, 1.5, 43.0), LeftLeg=(3.6, 2.0, 24.0), LeftFoot=(3.9, 3.0, 5.0),
            LeftToeBase=(3.9, -2.0, 1.5), LeftToeTip=(3.9, -6.0, 0.5),
            RightUpLeg=(-3.2, 1.5, 43.0), RightLeg=(-3.6, 2.0, 24.0), RightFoot=(-3.9, 3.0, 5.0),
            RightToeBase=(-3.9, -2.0, 1.5), RightToeTip=(-3.9, -6.0, 0.5)),
        straighten=[],
        coat_hem=dict(hip_src=44.0, hem_src=12.0, band_src=2.0, center_x_src=0.0, split_src=4.0, share=0.3, only_meshes=["Object_7"]),
        seed_zero_bones=0.001,
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={i: f"Image_{i}.png" for i in range(8)},
        materials={"haed_4": [("Base Color", "Image_0.png")], "head": [("Base Color", "Image_3.png")], "head_1": [("Base Color", "Image_3.png")],
                   "jacket_katan": [("Base Color", "Image_4.png")], "jacket_katana": [("Base Color", "Image_5.png")],
                   "katana_hands": [("Base Color", "Image_6.png")], "katana_shoes": [("Base Color", "Image_7.png")]}),
    # 주술회전 후시구로 토지(포트나이트 콜라보 립 Toji.fbx, 뼈 없음) → 초월_박민수_AD(2026-09-18 초월 재리깅 — 구현담당2 산출물 2회 반려 후 덮어쓰기).
    #   zip 안 source/Toji.fbx + textures/ 3장 · 메시 3(Body 19,458 · Hair 8,790 · Head 6,842) · 🔴 재질 이름이 메시와 뒤바뀜(Hair 메시에 「Head」 재질, Head 메시에 「Hair」 재질).
    #     🔴 텍스처 짝은 **FBX 안에 박힌 것(Toji.fbm)** 기준: Body→Body_CL · Hair(=얼굴 메시)→Head_CL · **Head(=머리카락 메시)→FaceAcc_CS**(604바이트 짙은 색).
    #     재질 이름만 보고 Head→Head_CL로 묶으면 머리카락이 살색이 된다(1회차 실측 — 원본 렌더와 대조).
    #   키 1.697 · A자(손이 허리 높이) · Body는 닫히지 않은 껍데기(경계 변 4,876).
    #   반려 이유: 오버사이즈 스웨터 소매가 팔을 안 따라감(지오데식 표면거리로는 소매가 가슴에 더 가깝다) → bone heat(뼈에서 보이는 안쪽 부피로 확산)로 소매 안 팔 뼈를 잡게 한다.
    #   관절(원본 좌표 단면 실측 toji2/verts.npz): 어깨 x 0.19 z 1.29 → 팔꿈치 0.40 z 1.16 → 손 0.55 z 0.94 → 손끝 0.597 z 0.88(A자 그대로 재고 straighten으로 T자) ·
    #     가랑이 z 0.68(0.70부터 한 덩어리) · 무릎 0.4 · 발목 0.06 · 다리 x ±0.15 · 목 1.33 · 머리 1.40 · 꼭대기 1.697.
    "초월_박민수_AD": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/08_초월/초월_박민수_AD.zip"),
        member=["source/Toji.fbx"], textures_dir="textures", mesh_name="Toji",
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        split_legs=dict(z_max_src=0.66),
        joints=dict(
            Hips=(0.0, 0.0, 0.72), Spine=(0.0, 0.0, 0.85), Spine1=(0.0, 0.0, 1.0), Spine2=(0.0, 0.0, 1.15),
            Neck=(0.0, 0.02, 1.33), Head=(0.0, 0.0, 1.4), HeadTop=(0.0, 0.0, 1.697),
            LeftShoulder=(0.06, 0.02, 1.31), LeftArm=(0.19, 0.03, 1.29), LeftForeArm=(0.4, 0.03, 1.16),
            LeftHand=(0.55, 0.0, 0.94), LeftHandTip=(0.597, -0.02, 0.88),
            RightShoulder=(-0.06, 0.02, 1.31), RightArm=(-0.19, 0.03, 1.29), RightForeArm=(-0.4, 0.03, 1.16),
            RightHand=(-0.55, 0.0, 0.94), RightHandTip=(-0.597, -0.02, 0.88),
            LeftUpLeg=(0.11, 0.0, 0.7), LeftLeg=(0.14, 0.0, 0.4), LeftFoot=(0.15, 0.0, 0.06),
            LeftToeBase=(0.15, -0.12, 0.02), LeftToeTip=(0.15, -0.19, 0.01),
            RightUpLeg=(-0.11, 0.0, 0.7), RightLeg=(-0.14, 0.0, 0.4), RightFoot=(-0.15, 0.0, 0.06),
            RightToeBase=(-0.15, -0.12, 0.02), RightToeTip=(-0.15, -0.19, 0.01)),
        rigid_meshes={"Hair": "Head", "Head": "Head"},
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        materials={"Body": [("Base Color", "T_ApplePound_Body_CL.png")], "Hair": [("Base Color", "T_ApplePound_Head_CL.png")],
                   "Head": [("Base Color", "T_ApplePound_FaceAcc_CS.png")]}),
    # 나루토 우치하 마다라(Sketchfab-15.10 glb 「Madara Uchiha」, CC-BY-4.0 Frankie-2nd_account) → 초월_유재헌_ADAP(2026-09-18 초월). 뼈 없는 정적 메시 → 오쿠야스 방식으로 새로 리깅.
    #   메시 12 · 재질 3 · 이미지 2(256², 알파 없음) · 삼각형 34,481 · 키 1.582 · A자(팔 약 30° 아래) · 정면 −Y.
    #   🔴 Sphere_for_copying_normals_to_madara_face(1,986정점, 재질 Root·텍스처 없음) = 얼굴 법선 복사용 도우미 구체 → 뺌.
    #   Lower_Metal_Plates Left/Middle/Right는 사본이 아니라 좌우·가운데 갑옷 판(x −0.278~−0.114 · ±0.09 · 0.114~0.278) → 셋 다 유지.
    #   관절(원본 좌표 단면 실측 madara/verts.npz): 어깨 z 1.21(몸통 폭 ±0.196) · 팔이 아래로 30° — x 0.17 z 1.19 → 팔꿈치 x 0.36 z 1.02 → 손 x 0.50 z 0.89 → 손끝 0.60 z 0.83 ·
    #     가랑이 z 0.68(0.70부터 한 덩어리) · 무릎 0.36 · 발목 0.06 · 다리 x ±0.1 · 목 1.27 · 머리 1.33 · 꼭대기 1.582.
    #   허리까지 오는 뒷머리는 Spine2 강체(Head 강체면 머리 흔들 때 허리까지 따라 흔들림) · 앞·위 머리카락·눈썹·눈은 Head 강체 ·
    #   허리 갑옷 판 3개와 로브 자락은 coat_hem(엉덩이 0.85 → 밑단 0.36, 넓적다리 몫 0.5) · 가랑이 밑은 split_legs.
    "초월_유재헌_ADAP": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/08_초월/초월_유재헌_ADAP.glb"), mesh_name="Madara",
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        drop_meshes=["Sphere_for_copying_normals_to_madara_face_0"],
        rigid_meshes={"L2_Back_hair_Plane.001_0": "Spine2", "L2_Top_hair_Plane.001_0": "Head", "L2_Front_Hair_Plane.000_0": "Head",
                      "L2_Madara_Eye_brows_0": "Head", "L2_Madara_torso_1": "Head"},
        # 🔴 복셀 대리(0.012·0.025): 몸 torso_0이 닫히지 않은 껍데기라 로브·갑옷 판만 큰 덩어리가 되고 몸·팔·발 뼈 가중치 0(키드와 같은 증상) → 대리 없이 직접 heat
        split_legs=dict(z_max_src=0.66),
        coat_hem=dict(hip_src=0.85, hem_src=0.36, band_src=0.06, split_src=0.08, center_x_src=0.0, share=0.5,
                      only_meshes=["L2_Madara_Lower_robe_0", "L2_Madara_Lower_Metal_Plates_Left_0",
                                   "L2_Madara_Lower_Metal_Plates_Middle_0", "L2_Madara_Lower_Metal_Plates_Right_0"]),
        joints=dict(
            Hips=(0.0, 0.0, 0.7), Spine=(0.0, 0.0, 0.82), Spine1=(0.0, 0.0, 0.95), Spine2=(0.0, 0.0, 1.1),
            Neck=(0.0, 0.0, 1.27), Head=(0.0, -0.02, 1.33), HeadTop=(0.0, -0.02, 1.582),
            LeftShoulder=(0.06, 0.02, 1.21), LeftArm=(0.17, 0.03, 1.19), LeftForeArm=(0.36, 0.04, 1.02),
            LeftHand=(0.5, 0.04, 0.89), LeftHandTip=(0.6, 0.03, 0.83),
            RightShoulder=(-0.06, 0.02, 1.21), RightArm=(-0.17, 0.03, 1.19), RightForeArm=(-0.36, 0.04, 1.02),
            RightHand=(-0.5, 0.04, 0.89), RightHandTip=(-0.6, 0.03, 0.83),
            LeftUpLeg=(0.09, 0.0, 0.66), LeftLeg=(0.1, 0.0, 0.36), LeftFoot=(0.1, -0.01, 0.06),
            LeftToeBase=(0.1, -0.09, 0.02), LeftToeTip=(0.1, -0.15, 0.01),
            RightUpLeg=(-0.09, 0.0, 0.66), RightLeg=(-0.1, 0.0, 0.36), RightFoot=(-0.1, -0.01, 0.06),
            RightToeBase=(-0.1, -0.09, 0.02), RightToeTip=(-0.1, -0.15, 0.01)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={0: "madara_body.png", 1: "madara_eyes.png"},
        materials={"Blender_Render_Madara_Material": [("Base Color", "madara_body.png")],
                   "Blender_Render_Madara_Eyes": [("Base Color", "madara_eyes.png")]}),
    # 죠죠 니지무라 오쿠야스(JoJo Diamond Records Reversal 모바일 OBJ 립) → 초월_엄태웅_AD(2026-09-18 초월, 구현담당2 gen_objrip_skin 산출물 반려 후 재리깅).
    #   zip 안 source/「Mobile - JoJo's Bizarre Adventure_ Diamond Records Reversal - Pa.zip」 안 Okuyasu.obj·.mtl·.png + zip textures/Okuyasu.png ·
    #   정점 2,205 · 면 2,542 · 재질 1(Mesh_0004.rip.002, MTL Kd 0.64 틴트 무시) · 키 1.794 · Y위 · 이미 T자 · 저폴리(감량 금지).
    #   반려 이유(PM 유니티 idleview): ① 위팔이 수평인 채 팔꿈치만 꺾임(어깨 관절이 바깥) ② 두 다리가 한 덩어리(가랑이 가중치 좌우 섞임).
    #   관절(원본 좌표 단면 실측 oku/…): 팔 수평 z 1.48 · 몸통 폭 z 1.35에서 ±0.21 → 어깨(Arm) x 0.19 · 팔꿈치 고리 x 0.43~0.46 → 0.44 · 손목 고리 0.67~0.70 → 0.69 · 손끝 0.878 ·
    #     가랑이 z 0.88(0.75~0.8은 가운데 한 조각뿐) · 무릎 고리 z 0.52 · 발목 0.1 · 다리 x ±0.1 · 머리 1.62~1.794.
    #   split_legs(새 옵션): 원본 z < 0.85 정점은 반대쪽 다리 뼈 몫 0.
    "초월_엄태웅_AD": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/08_초월/초월_엄태웅_AD.zip"),
        member=["source/Mobile - JoJo's Bizarre Adventure_ Diamond Records Reversal - Pa.zip", "Okuyasu.obj"], textures_dir="textures",
        mesh_name="Okuyasu", height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        split_legs=dict(z_max_src=0.85),
        # 🔴 1회차 직접 heat: 허리띠·깃 장식·귀걸이·$ 배지가 떨어진 섬(비다양체 변 456)이라 엉뚱한 뼈(허리띠 → 손)를 받아 Idle에서 날아감 → 복셀 대리 heat 후 가까운 면 보간
        heat_proxy=dict(voxel_m=0.015, keep_largest=True),
        joints=dict(
            Hips=(0.0, 0.0, 0.95), Spine=(0.0, 0.0, 1.08), Spine1=(0.0, 0.0, 1.2), Spine2=(0.0, 0.0, 1.35),
            Neck=(0.0, 0.0, 1.55), Head=(0.0, 0.0, 1.62), HeadTop=(0.0, 0.0, 1.794),
            LeftShoulder=(0.05, 0.03, 1.5), LeftArm=(0.19, 0.04, 1.49), LeftForeArm=(0.44, 0.04, 1.48),
            LeftHand=(0.69, 0.04, 1.48), LeftHandTip=(0.878, 0.04, 1.49),
            RightShoulder=(-0.05, 0.03, 1.5), RightArm=(-0.19, 0.04, 1.49), RightForeArm=(-0.44, 0.04, 1.48),
            RightHand=(-0.69, 0.04, 1.48), RightHandTip=(-0.878, 0.04, 1.49),
            LeftUpLeg=(0.1, 0.0, 0.9), LeftLeg=(0.1, -0.01, 0.52), LeftFoot=(0.1, 0.02, 0.1),
            LeftToeBase=(0.1, -0.1, 0.02), LeftToeTip=(0.1, -0.17, 0.01),
            RightUpLeg=(-0.1, 0.0, 0.9), RightLeg=(-0.1, -0.01, 0.52), RightFoot=(-0.1, 0.02, 0.1),
            RightToeBase=(-0.1, -0.1, 0.02), RightToeTip=(-0.1, -0.17, 0.01)),
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        materials={"Mesh_0004.rip.002": [("Base Color", "Okuyasu.png")]}),
    # 원피스 유스타스 키드(Sketchfab-16.67 glb 「Eustass Kid」, CC-BY-4.0 Cyrone) → 초월_임채민_AP(2026-09-18 초월). 🔴 뼈 781개가 이름 없는 bone_N이라 사람 뼈 매핑 불가 →
    #   strip_skin(새 옵션)으로 스킨·아마추어를 떼어 결합 자세(T자) 정적 메시로 만들고 새로 리깅. 메시 15 + 조명 Icosphere · 재질 7(base 이미지 0/2/4/6/8/10/12, 나머지는 노멀) · 삼각형 44,282.
    #   겹친 사본: Object_788 ↔ 804(얼굴, 같은 자리·표정 차 미세) → 788만. 790 ↔ 806은 사본이 아니라 좌우 코트 소매(등 뒤로 늘어짐) → 둘 다 유지.
    #   메시: 792 모피 코트 · 796 어깨 모피 깃 · 790/806 빈 소매 · 798 빨간 머리 · 788 얼굴 · 800/808/816 눈 · 812 고글 · 794 가슴 피부 · 802 왼쪽 거대 금속 팔(원작) · 814 오른팔 · 810 몸·바지.
    #   관절(결합 자세 원본 좌표, kid/verts.npz 단면): 다리 x ±0.15(z 0.35 밑에서 갈라짐) · 허리띠 z 1.1 · 팔 수평 z 1.69 ·
    #     오른팔(−X) 팔꿈치 −0.55 · 손목 −0.85 · 손끝 −1.06 · 금속 왼팔(+X) 좁아지는 곳 0.8(팔꿈치)·1.1(손목) · 손끝 1.4 · 머리 꼭대기 2.12.
    #   코트·깃·소매 → Spine2 강체(팔에 안 끌리게) · 머리카락·얼굴·눈·고글 → Head 강체 · 몸·가슴 피부·두 팔은 복셀 대리 heat.
    "초월_임채민_AP": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/08_초월/초월_임채민_AP.glb"), strip_skin=True, mesh_name="Kid",
        height=1.8, center_band=(0.0, 0.03), decimate=1.0, rotate_z=0.0,
        drop_meshes=["Object_804"],
        rigid_meshes={"Object_792": "Spine2", "Object_796": "Spine2", "Object_790": "Spine2", "Object_806": "Spine2",
                      "Object_798": "Head", "Object_788": "Head", "Object_800": "Head", "Object_808": "Head", "Object_816": "Head", "Object_812": "Head"},
        # 🔴 1·2회차 복셀 대리(0.012·0.025): 옷 810이 닫히지 않은 껍데기라 복셀로 몸통·바지가 안 생기고 금속 팔만 큰 덩어리 → 몸·다리 뼈 가중치 0. 대리 없이 bone heat.
        joints=dict(
            Hips=(0.0, 0.03, 0.98), Spine=(0.0, 0.03, 1.12), Spine1=(0.0, 0.03, 1.3), Spine2=(0.0, 0.03, 1.48),
            Neck=(0.0, 0.03, 1.74), Head=(0.0, 0.0, 1.83), HeadTop=(0.0, 0.0, 2.12),
            LeftShoulder=(0.06, 0.06, 1.69), LeftArm=(0.25, 0.08, 1.69), LeftForeArm=(0.8, 0.08, 1.69),
            LeftHand=(1.1, 0.08, 1.68), LeftHandTip=(1.4, 0.06, 1.7),
            RightShoulder=(-0.06, 0.06, 1.69), RightArm=(-0.25, 0.08, 1.69), RightForeArm=(-0.55, 0.08, 1.69),
            RightHand=(-0.85, 0.08, 1.68), RightHandTip=(-1.06, 0.08, 1.68),
            LeftUpLeg=(0.12, 0.03, 0.92), LeftLeg=(0.14, 0.04, 0.5), LeftFoot=(0.15, 0.06, 0.1),
            LeftToeBase=(0.15, -0.12, 0.03), LeftToeTip=(0.15, -0.24, 0.01),
            RightUpLeg=(-0.12, 0.03, 0.92), RightLeg=(-0.14, 0.04, 0.5), RightFoot=(-0.15, 0.06, 0.1),
            RightToeBase=(-0.15, -0.12, 0.03), RightToeTip=(-0.15, -0.24, 0.01)),
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        glb_images={0: "kid_face.png", 2: "kid_coat.png", 4: "kid_skin.png", 6: "kid_hair.png", 8: "kid_arm.png", 10: "kid_cloth.png", 12: "kid_goggles.png"},
        materials={"face": [("Base Color", "kid_face.png")], "coat": [("Base Color", "kid_coat.png")], "skin": [("Base Color", "kid_skin.png")],
                   "hair": [("Base Color", "kid_hair.png")], "material": [("Base Color", "kid_arm.png")], "cloth": [("Base Color", "kid_cloth.png")],
                   "model_0_mat_10": [("Base Color", "kid_goggles.png")]}),
    # 원피스 베가펑크(초기 모습 — 사과 모자·긴 혀·흰 가운·큰 부츠, ZBrush 2021 OBJ, 뼈 없음) → 제한_이충민(2026-09-17 제한됨, 구현담당1 gen_skin_rig 산출물 반려 후 새로).
    #   zip 안 source/Vegapunk_75k.zip 안 OBJ 하나 · 정점 75,000 · 삼각형 159,012 · UV·법선·재질 0 · Y위(가져오기가 Z위로) · 이미 T자 · 정면 −Y.
    #   🔴 섬 100개(각각 닫힌 조각 — 경계 변 730뿐, 이음새가 아니라 ZBrush 부품) → 붙이지 않고 모양 그대로, bone heat는 복셀 대리(heat_proxy)에서 풀어 옮긴다.
    #   🔴 색: **폴리페인트 정점 색**이 원작 색으로 칠해져 있다(16색 — 흰 가운·머리·다리·부츠 · 파란 셔츠 · 회색 · 피부 · 분홍 혀 · 빨간 사과 · 초록 잎 · 갈색 꼭지)
    #     → vertex_color_palette로 단색 재질 8개(가까운 색으로 정점 가르고 면은 다수결).
    #   관절(원본 좌표, vega/islands·slices): 발바닥 −3.55 · 부츠 윗단 −2.35 · 다리 둘(x ±0.38~0.7)이 −0.5에서 합침 · 가운 자락 −0.55 · 팔 수평 z 1.3(소매 끝 2.4 · 손 2.32~2.99)
    #     · 몸통 폭 z 1.0에서 ±0.6 · 얼굴(피부 섬) 1.12~2.70 앞(y −0.8)으로 · 사과 2.82~3.18 · 잎 끝 3.47.
    "제한_이충민": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/07_제한됨/제한_이충민.zip"),
        member=["source/Vegapunk_75k.zip", "Vegapunk_75k.obj"], mesh_name="Vegapunk",
        height=1.8, center_band=(0.0, 0.03), decimate=0.25, rotate_z=0.0,
        vertex_color_palette={"white": (1.0, 1.0, 1.0), "grey": (0.537, 0.537, 0.537), "shirt_blue": (0.239, 0.578, 0.992),
                              "skin": (0.858, 0.529, 0.458), "tongue_pink": (0.876, 0.204, 0.41), "apple_red": (0.848, 0.01, 0.01),
                              "leaf_green": (0.197, 0.582, 0.025), "stem_brown": (0.586, 0.145, 0.025)},
        heat_proxy=dict(voxel_m=0.012, keep_largest=True), decimate_after_weights=True,
        move_zone_weights=[dict(box_src=((-0.9, -2.0, 1.45), (0.9, 2.0, 3.5)), x_in=0.35, x_out=0.9,
                               bones=["LeftShoulder", "RightShoulder"], into="Neck")],
        max_bone_share=0.95,   # 머리카락 섬(I02~I15)이 정점 절반 이상이라 Head 몫이 원래 크다(감량 전 75% · 감량 후 89% — w>0.01 정점 수, 목·머리가 한 통이라 Neck도 88%)
        joints=dict(
            Hips=(0.0, 0.0, -0.35), Spine=(0.0, 0.0, 0.1), Spine1=(0.0, 0.0, 0.55), Spine2=(0.0, 0.0, 1.0),
            Neck=(0.0, -0.05, 1.55), Head=(0.0, -0.15, 1.85), HeadTop=(0.0, -0.2, 3.47),
            LeftShoulder=(0.15, -0.05, 1.35), LeftArm=(0.75, -0.05, 1.3), LeftForeArm=(1.5, -0.05, 1.3),
            LeftHand=(2.3, -0.05, 1.3), LeftHandTip=(2.99, -0.05, 1.3),
            RightShoulder=(-0.15, -0.05, 1.35), RightArm=(-0.75, -0.05, 1.3), RightForeArm=(-1.5, -0.05, 1.3),
            RightHand=(-2.3, -0.05, 1.3), RightHandTip=(-2.99, -0.05, 1.3),
            LeftUpLeg=(0.55, -0.05, -0.45), LeftLeg=(0.55, -0.12, -1.4), LeftFoot=(0.55, 0.0, -2.9),
            LeftToeBase=(0.55, -0.35, -3.35), LeftToeTip=(0.55, -0.72, -3.45),
            RightUpLeg=(-0.55, -0.05, -0.45), RightLeg=(-0.55, -0.12, -1.4), RightFoot=(-0.55, 0.0, -2.9),
            RightToeBase=(-0.55, -0.35, -3.35), RightToeTip=(-0.55, -0.72, -3.45)),
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)]),
    # 주술회전 오리모토 리카(저주령, Sketchfab 정적 glb, 뼈 없음) → 제한_김강민(2026-09-17 제한됨). 🔸 사람형 아님(다리 대신 뒤로 누운 꼬리) → Generic + 자체 Idle.
    #   메시 4: Body_low(몸·팔·꼬리, Body1) · Hair_low(머리 뒤로 늘어진 촉수 다발) · Teeth_low·Mouth_low(크게 벌린 입) · 재질 3 · 이미지 9 중 기본색 3장(0 입·3 머리·6 몸)만.
    #   좌표(원본, 격자 0.5 렌더 rika/p_front·side): 머리 꼭대기 z 2.27 · 목 1.12 · 어깨 (±0.75, 1.15) · 팔꿈치 (±1.92, 0.28) · 손목 (±3.0, −0.49) · 손끝 (±3.38, −0.88) ·
    #     허리 0 · 꼬리가 z −3.0까지 내려갔다가 뒤(+Y)로 3.25까지 눕는다. 정면 −Y(입).
    #   뼈: Hips → Spine·Spine1·Spine2 → Neck → Head · 좌우 Shoulder·Arm·ForeArm·Hand · Tail1~6(꼬리 중심선). 머리카락·입·이는 Head 강체.
    "제한_김강민": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/07_제한됨/제한_김강민.glb"),
        height=1.8, center_band=(0.55, 0.7), decimate=1.0, rotate_z=0.0,
        rigid_meshes={"Hair_low_Hair_0": "Head", "Teeth_low_Mouth_0": "Head", "Mouth_low_Mouth_0": "Head"},
        max_bone_share=0.85,                                            # 머리카락 6,158·이 4,093정점이 Head 강체라 Head 몫 78%(heat 실패 아님 — 20뼈 전부 가중치)
        generic_bones=[
            ("Hips", (0.0, 0.15, -0.25), (0.0, 0.15, 0.25), None),
            ("Spine", (0.0, 0.15, 0.25), (0.0, 0.12, 0.7), "Hips"),
            ("Spine1", (0.0, 0.12, 0.7), (0.0, 0.1, 1.0), "Spine"),
            ("Spine2", (0.0, 0.1, 1.0), (0.0, 0.1, 1.2), "Spine1"),
            ("Neck", (0.0, 0.1, 1.2), (0.0, 0.05, 1.45), "Spine2"),
            ("Head", (0.0, 0.05, 1.45), (0.0, -0.1, 2.27), "Neck"),
            ("LeftShoulder", (0.15, 0.1, 1.2), (0.75, 0.15, 1.15), "Spine2"),
            ("LeftArm", (0.75, 0.15, 1.15), (1.92, 0.15, 0.28), "LeftShoulder"),
            ("LeftForeArm", (1.92, 0.15, 0.28), (3.0, 0.1, -0.49), "LeftArm"),
            ("LeftHand", (3.0, 0.1, -0.49), (3.38, 0.1, -0.88), "LeftForeArm"),
            ("RightShoulder", (-0.15, 0.1, 1.2), (-0.75, 0.15, 1.15), "Spine2"),
            ("RightArm", (-0.75, 0.15, 1.15), (-1.92, 0.15, 0.28), "RightShoulder"),
            ("RightForeArm", (-1.92, 0.15, 0.28), (-3.0, 0.1, -0.49), "RightArm"),
            ("RightHand", (-3.0, 0.1, -0.49), (-3.38, 0.1, -0.88), "RightForeArm"),
            ("Tail1", (0.0, 0.15, -0.25), (0.0, -0.1, -1.1), "Hips"),
            ("Tail2", (0.0, -0.1, -1.1), (0.0, -0.05, -1.9), "Tail1"),
            ("Tail3", (0.0, -0.05, -1.9), (0.0, 0.4, -2.7), "Tail2"),
            ("Tail4", (0.0, 0.4, -2.7), (0.0, 1.5, -2.95), "Tail3"),
            ("Tail5", (0.0, 1.5, -2.95), (0.0, 2.4, -3.0), "Tail4"),
            ("Tail6", (0.0, 2.4, -3.0), (0.0, 3.25, -3.05), "Tail5")],
        straighten=[],
        seed_zero_bones=0.001,
        synth_idle=dict(take="Idle", frames=72, step=3, bones={
            "Spine1": [((1, 0, 0), 1.5, 0.0)], "Spine2": [((1, 0, 0), 1.5, -0.4)],
            "Head": [((1, 0, 0), 3.0, -0.9), ((0, 0, 1), 2.5, 0.5)],
            "LeftArm": [((0, 1, 0), 4.0, 0.3)], "RightArm": [((0, 1, 0), -4.0, 0.3)],
            "LeftForeArm": [((0, 1, 0), 3.0, -0.3)], "RightForeArm": [((0, 1, 0), -3.0, -0.3)],
            "Tail1": [((0, 1, 0), 2.0, 0.0)], "Tail2": [((0, 1, 0), 3.0, -0.7)], "Tail3": [((0, 0, 1), 4.0, -1.4)],
            "Tail4": [((0, 0, 1), 6.0, -2.1)], "Tail5": [((0, 0, 1), 8.0, -2.8)], "Tail6": [((0, 0, 1), 10.0, -3.5)]}),
        glb_images={0: "rika_mouth_baseColor.png", 3: "rika_hair_baseColor.png", 6: "rika_body_baseColor.png"},
        materials={"Mouth": [("Base Color", "rika_mouth_baseColor.png")], "Hair": [("Base Color", "rika_hair_baseColor.png")],
                   "Body1": [("Base Color", "rika_body_baseColor.png")]}),
    "특별함_최동준": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/03_특별함/특별함_최동준.glb"),
        height=1.8, center_band=(0.02, 0.08), decimate=0.4, rotate_z=0.0, joints=JOINTS_CM,
        textures=[("Base Color", 0, "default_baseColor.jpg"), ("Normal", 1, "default_normal.png")]),
    # 히소카(헌터x헌터, Sketchfab zip 안 FBX) — 2026-09-16 희귀함 1호. 뼈·가중치·애니 0인 정적 메시, 좌우 대칭 A자(팔 수평 아래 28°)라 베르고보다 쉽다.
    #   🔴 툰 외곽선 껍데기 3개(재질 Outline)를 빼야 한다 — 두면 캐릭터를 검게 덮는다. 눈은 별도 메시(Eye_L/R)라 bone heat가 머리 뼈로 가져간다.
    #   관절은 원본 좌표 실측(스크래치 hisoka/measure.py·measure_arm.py): 팔은 거의 수평이라 z단면 말고 **x 띠**로 잘라 중심 z·y와 굵기를 봤다 —
    #   어깨 x 0.24·z 1.50 → 팔꿈치 0.43·1.42 → 손목(제일 가늘다) 0.605·1.33 → 손끝 0.79·1.15. 다리는 엉덩이 x 0.105 → 무릎 0.21 → 발목 0.232로 살짝 벌어짐(굽 부츠).
    "희귀함_최상호_윤식파의두뇌": dict(
        source=os.path.expanduser("~/Desktop/구랜디스킨모음/04_희귀함/희귀함_최상호_윤식파의두뇌.zip"),
        member="source/Hisoka_Sketchfab.fbx", textures_dir="textures",
        height=1.8, center_band=(0.02, 0.08), decimate=1.0, rotate_z=0.0,
        drop_meshes=["HairOutline", "ClothingOutline", "CharacterOutline.001"],
        # 남긴 193,438 삼각형 → 4만 안팎(머리카락이 9만이라 거기서 많이, 눈은 구라서 크게 줄여도 된다)
        decimate_by_mesh={"Hair": 0.13, "Character": 0.45, "Clothing": 0.28, "Eye_L": 0.08, "Eye_R": 0.08},
        joints=dict(
            Hips=(0.0, 0.0, 0.95), Spine=(0.0, 0.0, 1.08), Spine1=(0.0, 0.0, 1.21), Spine2=(0.0, 0.0, 1.36),
            Neck=(0.0, 0.0, 1.60), Head=(0.0, 0.0, 1.72), HeadTop=(0.0, 0.0, 1.95),
            LeftShoulder=(0.07, 0.02, 1.49), LeftArm=(0.24, 0.02, 1.50), LeftForeArm=(0.43, 0.02, 1.42),
            LeftHand=(0.605, -0.04, 1.33), LeftHandTip=(0.79, -0.09, 1.15),
            RightShoulder=(-0.07, 0.02, 1.49), RightArm=(-0.24, 0.02, 1.50), RightForeArm=(-0.43, 0.02, 1.42),
            RightHand=(-0.605, -0.04, 1.33), RightHandTip=(-0.79, -0.09, 1.15),
            LeftUpLeg=(0.105, 0.0, 0.95), LeftLeg=(0.21, 0.01, 0.52), LeftFoot=(0.232, 0.02, 0.135),
            LeftToeBase=(0.235, -0.06, 0.03), LeftToeTip=(0.235, -0.17, 0.02),
            RightUpLeg=(-0.105, 0.0, 0.95), RightLeg=(-0.21, 0.01, 0.52), RightFoot=(-0.232, 0.02, 0.135),
            RightToeBase=(-0.235, -0.06, 0.03), RightToeTip=(-0.235, -0.17, 0.02)),
        straighten=["LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand"],
        closeups=[("armpit", 1.35, 0.8), ("crotch", 0.85, 0.9)],
        materials={"Skin": [("Base Color", "HisokaUV_001_Skin_Light_Albedo.png")],
                   "Clothing": [("Base Color", "HisokaUV_001_Clothing_Light_Albedo.png")],
                   "Hair": [("Base Color", "HisokaUV_001_Hair_Light_Albedo.png")],
                   "Eye": [("Base Color", "HisokaUV_001_EyeLight_Albedo.png")]}),
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


def straighten_limbs(arm, body, only=None, arm_drop_deg=0.0):
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
    if arm_drop_deg:                                                    # 🔸 나오야(PM 허용 2026-09-16): 팔을 수평에서 아래로 기울인 T자 — 넓은 소매·하카마를 덜 끈다(최대 30°)
        c, sn = math.cos(math.radians(arm_drop_deg)), math.sin(math.radians(arm_drop_deg))
        plan = [(b, Vector((t.x * c, t.y, -sn)) if (abs(t.x) > 0.9 and not b.endswith("Shoulder")) else t) for b, t in plan]
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


def skin_arm(body, G, spec, tex_dir):
    """맨살 아래팔·손이 바지·후드에 닿은 선 자세(유지): 소맷부리 아래에서는 **텍스처 피부색**이 팔인지 옷인지 가른다.
    🔴 늘어난 변 조사(yuji/strip.py): 손 정점이 RightHand 0.9 / RightUpLeg 0.1로 섞여 T자에서 손→엉덩이 띠 →
    가중치만 가르니(5회차) 띠가 오히려 길어졌다(늘어난 변 중앙 0.755 m) — 손과 바지가 **면으로 붙어** 있다(섬 1개).
    그래서 ① 소맷부리 아래·팔 쪽 구역에서 피부 면↔옷 면 사이 변을 가르고 ② 피부 정점은 그 쪽 팔 사슬만, 옷 정점은 팔 사슬 빼기."""
    from mathutils.kdtree import KDTree
    img = bpy.data.images.load(os.path.join(tex_dir, spec["texture"]), check_existing=True)
    w, h = img.size
    px = np.empty(w * h * img.channels, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, img.channels)

    def is_skin(c):
        r, g, b = c[..., 0], c[..., 1], c[..., 2]
        # 🔴 6회차: 그늘진 손가락(0.41,0.35,0.31)이 옷으로 분류돼 엉덩이에 살색 조각·실이 남음 → 밝기 문턱 낮춤(검은 바지는 b > r라 여전히 옷)
        return (r > 0.25) & (r - b > 0.05) & (r - b < 0.35) & (r > g) & (g > b)

    zmin = (G @ Vector((0, 0, spec["z_min_src"]))).z
    zones = []
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        cuff = (G @ Vector((0, 0, spec["cuff_src"][side]))).z
        xmin = (G @ Vector((sign * spec["x_min_src"], 0, 0))).x * sign
        zones.append((side, sign, cuff, xmin))

    def zone_of(p):
        for side, sign, cuff, xmin in zones:
            if zmin < p[2] < cuff and p[0] * sign > xmin:
                return side
        return None

    bm = bmesh.new()
    bm.from_mesh(body.data)
    uv_layer = bm.loops.layers.uv[0]
    face_skin = {}
    for f in bm.faces:
        cs = []
        for l in f.loops:
            u, v = l[uv_layer].uv
            cs.append(px[min(int(v % 1.0 * h), h - 1), min(int(u % 1.0 * w), w - 1), :3])
        face_skin[f] = bool(is_skin(np.mean(cs, axis=0)))
    cut = [e for e in bm.edges if len(e.link_faces) == 2 and face_skin[e.link_faces[0]] != face_skin[e.link_faces[1]]
           and zone_of((e.verts[0].co + e.verts[1].co) / 2)]
    bmesh.ops.split_edges(bm, edges=cut)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    out = {"자른변": len(cut)}

    me = body.data
    n = len(me.vertices)
    vskin_votes = np.zeros(n)
    vcnt = np.zeros(n)
    uv = np.empty(len(me.loops) * 2)
    me.uv_layers[0].data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    for poly in me.polygons:
        idx = list(poly.loop_indices)
        c = np.mean([px[min(int(uv[i, 1] % 1.0 * h), h - 1), min(int(uv[i, 0] % 1.0 * w), w - 1), :3] for i in idx], axis=0)
        sk = float(is_skin(c))
        for vi in poly.vertices:
            vskin_votes[vi] += sk
            vcnt[vi] += 1
    skin = vskin_votes / np.maximum(vcnt, 1) > 0.5
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    groups = {gr.name: gr.index for gr in body.vertex_groups}
    Wm = np.zeros((n, len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight
    for side, sign, cuff, xmin in zones:
        region = (co[:, 2] < cuff) & (co[:, 2] > zmin) & (co[:, 0] * sign > xmin)
        arm_cols = [groups[PREFIX + side + k] for k in ("Arm", "ForeArm", "Hand")]
        other = [i for i in range(Wm.shape[1]) if i not in arm_cols]
        sk, cl = region & skin, region & ~skin
        Wm[np.ix_(np.where(sk)[0], other)] = 0.0
        Wm[np.ix_(np.where(cl)[0], arm_cols)] = 0.0
        out[side] = dict(피부=int(sk.sum()), 옷=int(cl.sum()))
    total = Wm.sum(1)
    empty = np.where(total <= 1e-6)[0]
    if len(empty):                                                     # 비면 같은 부류(피부/옷) 중 가장 가까운 정점 가중치
        for kind in (True, False):
            src = np.where((total > 1e-6) & (skin == kind))[0]
            dst = empty[skin[empty] == kind]
            if not len(dst) or not len(src):
                continue
            kd = KDTree(len(src))
            for i in src:
                kd.insert(Vector(co[i]), int(i))
            kd.balance()
            for i in dst:
                Wm[i] = Wm[kd.find(Vector(co[i]))[1]]
        total = Wm.sum(1)
    Wm /= np.maximum(total, 1e-9)[:, None]
    for gr in body.vertex_groups:
        c = Wm[:, gr.index]
        zero = [int(i) for i in np.where(c <= 1e-4)[0]]
        if zero:
            gr.remove(zero)
        for i in np.where(c > 1e-4)[0]:
            gr.add([int(i)], float(c[i]), "REPLACE")
    out["다시채운정점"] = int(len(empty))
    return out


def strip_arm_in_torso(body, G, spec, tex_dir):
    """몸통·치마 안쪽 타원 기둥에서는 팔 가중치를 뺀다(나오야, 2026-09-16): 팔을 몸 옆에 내린 옷 입은 스캔은 bone heat가 **근접**으로 퍼져
    아래팔·주먹 옆 기모노 몸판·하카마 앞 주름까지 팔 몫을 준다(붙은 면이 아니라 가르기·법선 마스크가 못 막음). 타원 안(정규화 거리 < 1) 팔 몫 0,
    blend 폭 동안 서서히 되살림. 피부색(손) 정점은 제외. 빈 정점은 가장 가까운 가중치 정점 복사."""
    from mathutils.kdtree import KDTree
    me = body.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    scale = G.to_scale().x
    c = G @ Vector((spec["center_src"][0], spec["center_src"][1], 0.0))
    rx, ry, blend = spec["rx_src"] * scale, spec["ry_src"] * scale, spec["blend_src"] * scale
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    d = np.sqrt(((co[:, 0] - c.x) / rx) ** 2 + ((co[:, 1] - c.y) / ry) ** 2)
    keep = np.clip((d - 1.0) * min(rx, ry) / blend, 0.0, 1.0)            # 0 = 안쪽(팔 몫 없음) → 1 = 밖
    zb = blend
    keep = np.maximum(keep, np.clip((co[:, 2] - zhi) / zb, 0.0, 1.0))     # 위쪽 경계도 부드럽게
    keep[co[:, 2] < zlo] = 1.0
    if spec.get("skin_texture"):
        img = bpy.data.images.load(os.path.join(tex_dir, spec["skin_texture"]), check_existing=True)
        w, h = img.size
        px = np.empty(w * h * img.channels, dtype=np.float32)
        img.pixels.foreach_get(px)
        px = px.reshape(h, w, img.channels)[:, :, :3]
        uv = np.empty(len(me.loops) * 2)
        me.uv_layers[0].data.foreach_get("uv", uv)
        uv = uv.reshape(-1, 2)
        lv = np.empty(len(me.loops), dtype=np.int64)
        me.loops.foreach_get("vertex_index", lv)
        col = np.zeros((n, 3))
        cnt = np.zeros(n)
        np.add.at(col, lv, px[np.clip((uv[:, 1] % 1 * h).astype(int), 0, h - 1), np.clip((uv[:, 0] % 1 * w).astype(int), 0, w - 1)])
        np.add.at(cnt, lv, 1)
        col /= np.maximum(cnt, 1)[:, None]
        r, g, b = col[:, 0], col[:, 1], col[:, 2]
        skin = (r > 0.25) & (r - b > 0.05) & (r - b < 0.35) & (r > g) & (g > b)
        keep[skin] = 1.0
    groups = {gr.name: gr.index for gr in body.vertex_groups}
    Wm = np.zeros((n, len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight
    arm_cols = [groups[PREFIX + s + k] for s in ("Left", "Right") for k in ("Arm", "ForeArm", "Hand") if PREFIX + s + k in groups]
    before = int((Wm[:, arm_cols].sum(1) > 0.01).sum())
    Wm[:, arm_cols] *= keep[:, None]
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
    for gr in body.vertex_groups:
        cc = Wm[:, gr.index]
        zero = [int(i) for i in np.where(cc <= 1e-4)[0]]
        if zero:
            gr.remove(zero)
        for i in np.where(cc > 1e-4)[0]:
            gr.add([int(i)], float(cc[i]), "REPLACE")
    return dict(팔가중치정점_전=before, 후=int((Wm[:, arm_cols].sum(1) > 0.01).sum()), 다시채운정점=int(len(empty)))


def split_by_saturation(body, G, spec, tex_dir):
    """🔸 나오야(2026-09-16 PM 가설 1): 넓은 소매 기모노(남색, 채도 ≥ hi)와 하카마·셔츠(무채, 채도 ≤ lo) — 밝기로는 하카마 회색과 셔츠 흰색이 겹쳐 못 가르지만
    **채도로는 갈린다**. bone heat 뒤 z 구간 안에서 채도 높은 면↔낮은 면 사이 변을 가른다(가중치는 bmesh가 사본에 복사)."""
    img = bpy.data.images.load(os.path.join(tex_dir, spec["texture"]), check_existing=True)
    w, h = img.size
    px = np.empty(w * h * img.channels, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, img.channels)[:, :, :3]
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    bm = bmesh.new()
    bm.from_mesh(body.data)
    uvl = bm.loops.layers.uv[0]
    kind = {}
    for f in bm.faces:
        cs = np.mean([px[min(int(l[uvl].uv[1] % 1.0 * h), h - 1), min(int(l[uvl].uv[0] % 1.0 * w), w - 1)] for l in f.loops], axis=0)
        sat = (cs.max() - cs.min()) / max(cs.max(), 1e-6)
        kind[f] = 1 if sat >= spec["hi"] else (0 if sat <= spec["lo"] else None)
    cut = [e for e in bm.edges if len(e.link_faces) == 2 and kind[e.link_faces[0]] is not None and kind[e.link_faces[1]] is not None
           and kind[e.link_faces[0]] != kind[e.link_faces[1]] and zlo < (e.verts[0].co.z + e.verts[1].co.z) / 2 < zhi]
    bmesh.ops.split_edges(bm, edges=cut)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    return dict(자른변=len(cut))


def delete_bridge_faces(body, G, spec):
    """🔸 보디빌더(2026-09-16, 구현담당2에서 막혀 넘어옴): 차렷 자세 스캔이 손↔허벅지·팔↔옆구리 닿은 자리를 **실제 삼각형으로 이어 붙였다**(포토그래메트리 결함).
    둘 다 피부라 유지식 색 가르기는 못 쓴다 → bone heat 뒤, 접촉 띠(z·옆쪽 x) 안에서 꼭짓점 팔 몫이 한쪽은 arm_hi 넘고 다른 쪽은 arm_lo 밑인
    **다리 놓은 면**을 지우고 떨어진 정점도 지운다. 원래 붙어 있던 자리라 Idle(팔 내림)에선 구멍이 안 보인다."""
    me = body.data
    groups = {g.name: g.index for g in body.vertex_groups}
    arm_idx = {groups[PREFIX + s + k] for s in ("Left", "Right") for k in ("Arm", "ForeArm", "Hand") if PREFIX + s + k in groups}
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    xmin = abs((G @ Vector((spec["x_min_src"], 0, 0))).x - (G @ Vector((0, 0, 0))).x)
    xc = (G @ Vector((0, 0, 0))).x
    armw = [sum(ge.weight for ge in v.groups if ge.group in arm_idx) for v in me.vertices]
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    kill = []
    for f in bm.faces:
        c = f.calc_center_median()
        if not (zlo < c.z < zhi and abs(c.x - xc) > xmin):
            continue
        ws = [armw[v.index] for v in f.verts]
        if max(ws) > spec.get("arm_hi", 0.6) and min(ws) < spec.get("arm_lo", 0.4):
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    filled = 0
    if spec.get("fill_holes"):
        # 🔸 6회차: 지운 자리가 허벅지 바깥면 구멍으로 남아 Idle(팔을 가슴 앞으로)에서 안쪽 면이 보인다 → 띠 안 경계 고리를 메우고,
        #   새 면 UV는 같은 정점의 기존 UV를 복사(주변 피부색). 가중치는 정점 것이라 그대로.
        uvl = bm.loops.layers.uv[0]
        old_faces = set(bm.faces)
        edges = [e for e in bm.edges if e.is_boundary and zlo < (e.verts[0].co.z + e.verts[1].co.z) / 2 < zhi]
        res = bmesh.ops.holes_fill(bm, edges=edges, sides=spec.get("fill_max_sides", 400))
        new_faces = [f for f in res["faces"]]
        uv_of = {}
        for f in old_faces:
            for l in f.loops:
                uv_of.setdefault(l.vert.index if l.vert.is_valid else None, l[uvl].uv.copy())
        bm.verts.index_update()
        uv_of = {}
        for f in bm.faces:
            if f in new_faces:
                continue
            for l in f.loops:
                uv_of.setdefault(l.vert.index, l[uvl].uv.copy())
        for f in new_faces:
            for l in f.loops:
                if l.vert.index in uv_of:
                    l[uvl].uv = uv_of[l.vert.index]
        bmesh.ops.triangulate(bm, faces=new_faces)
        filled = len(new_faces)
    bm.to_mesh(me)
    bm.free()
    me.update()
    return dict(지운면=len(kill), 떨어진정점=len(loose), 메운구멍=filled)


def arm_leg_radius(body, G, joints, spec):
    """🔸 보디빌더 3회차: 차렷 자세로 손·아래팔이 굵은 허벅지에 닿은 스캔 — 접촉 띠(z) 안 정점마다 팔 축(위팔·아래팔·손 선분)까지 거리/팔 반지름과
    허벅지 축(UpLeg→Leg)까지 거리/허벅지 반지름을 비교해, 허벅지가 더 가까우면 그 쪽 팔 몫 0, 팔이 더 가까우면 그 쪽 다리 몫 0.
    (타원 기둥은 허벅지 바깥면이 손 안쪽 가장자리보다 바깥이라 못 갈랐다 — 반지름으로 정규화해야 굵은 허벅지가 이긴다.)"""
    from mathutils.kdtree import KDTree
    me = body.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    scale = G.to_scale().x
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    groups = {g.name: g.index for g in body.vertex_groups}
    Wm = np.zeros((n, len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight

    def seg_dist(a, b):
        ab = b - a
        t = np.clip(((co - a) @ ab) / max(ab @ ab, 1e-12), 0.0, 1.0)
        return np.linalg.norm(co - (a + np.outer(t, ab)), axis=1)

    inz = (co[:, 2] > zlo) & (co[:, 2] < zhi)
    out = {}
    for side in ("Left", "Right"):
        J = {k: np.array(G @ Vector(joints[side + k])) for k in ("Arm", "ForeArm", "Hand", "HandTip", "UpLeg", "Leg")}
        ra, rf, rh = [r * scale for r in spec["arm_r"]]
        d_arm = np.minimum.reduce([seg_dist(J["Arm"], J["ForeArm"]) / ra, seg_dist(J["ForeArm"], J["Hand"]) / rf, seg_dist(J["Hand"], J["HandTip"]) / rh])
        d_leg = seg_dist(J["UpLeg"], J["Leg"]) / (spec["leg_r"] * scale)
        sidem = inz & ((co[:, 0] > 0) if side == "Left" else (co[:, 0] < 0))
        arm_cols = [groups[PREFIX + side + k] for k in ("Arm", "ForeArm", "Hand") if PREFIX + side + k in groups]
        leg_cols = [groups[PREFIX + side + k] for k in ("UpLeg", "Leg") if PREFIX + side + k in groups]
        # 팔 쪽 정점은 몸통 뼈 몫도 뺀다(5회차: 손 정점이 Hips 0.51·RightHand 0.38이라 반쯤 골반에 붙어 손→허벅지 가시 하나가 남았다)
        leg_cols += [groups[PREFIX + k] for k in spec.get("arm_side_strip", ()) if PREFIX + k in groups]
        bias = spec.get("leg_bias", 1.0)                                # < 1이면 팔 쪽으로 기운다(허벅지가 확실히 가까울 때만 다리)
        to_leg = sidem & (d_leg < d_arm * bias)
        to_arm = sidem & ~to_leg
        Wm[np.ix_(np.where(to_leg)[0], arm_cols)] = 0.0
        Wm[np.ix_(np.where(to_arm)[0], leg_cols)] = 0.0
        out[side] = dict(허벅지쪽=int(to_leg.sum()), 팔쪽=int(to_arm.sum()))
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
        c = Wm[:, g.index]
        zero = [int(i) for i in np.where(c <= 1e-4)[0]]
        if zero:
            g.remove(zero)
        for i in np.where(c > 1e-4)[0]:
            g.add([int(i)], float(c[i]), "REPLACE")
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


def vertex_skin_mask(body, tex_dir, texture):
    """정점마다 텍스처 피부색 여부(면 모서리 UV 평균) — skin_arm과 같은 판정."""
    img = bpy.data.images.load(os.path.join(tex_dir, texture), check_existing=True)
    w, h = img.size
    px = np.empty(w * h * img.channels, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, img.channels)
    me = body.data
    uv = np.empty(len(me.loops) * 2)
    me.uv_layers[0].data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    vi = np.empty(len(me.loops), dtype=np.int64)
    me.loops.foreach_get("vertex_index", vi)
    c = px[np.minimum((uv[:, 1] % 1.0 * h).astype(int), h - 1), np.minimum((uv[:, 0] % 1.0 * w).astype(int), w - 1), :3]
    acc = np.zeros((len(me.vertices), 3)); cnt = np.zeros(len(me.vertices))
    np.add.at(acc, vi, c); np.add.at(cnt, vi, 1)
    col = acc / np.maximum(cnt, 1)[:, None]
    r, g, b = col[:, 0], col[:, 1], col[:, 2]
    return (r > 0.25) & (r - b > 0.05) & (r - b < 0.35) & (r > g) & (g > b)


def weight_matrix(body):
    me = body.data
    Wm = np.zeros((len(me.vertices), len(body.vertex_groups)))
    for v in me.vertices:
        for ge in v.groups:
            Wm[v.index, ge.group] = ge.weight
    return Wm


def write_weights(body, Wm, rows):
    for gi, g in enumerate(body.vertex_groups):
        col = Wm[rows, gi]
        g.remove([int(r) for r in rows])
        nz = col > 1e-4
        for r, w in zip(np.asarray(rows)[nz], col[nz]):
            g.add([int(r)], float(w), "REPLACE")


def strip_bones_nonskin(body, G, spec, tex_dir):
    """🔸 나오야 재빌드(2026-09-17): 오른손을 하카마 허리에 얹은 스캔 — 늘어난 변 조사(naoya2/diag.py)에서 허리 z 1.1의 RightHand 몫 차이가 한 무리.
    구역(원본 z) 안의 **피부색이 아닌 정점**에서 손 뼈 몫을 빼고 남은 몫으로 다시 1로 맞춘다(남은 게 없으면 Spine)."""
    skin = vertex_skin_mask(body, tex_dir, spec["texture"])
    co = np.array([v.co[:] for v in body.data.vertices])
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    rows = np.nonzero((~skin) & (co[:, 2] > zlo) & (co[:, 2] < zhi))[0]
    Wm = weight_matrix(body)
    groups = {g.name: g.index for g in body.vertex_groups}
    cols = [groups[PREFIX + b] for b in spec["bones"] if PREFIX + b in groups]
    before = int((Wm[rows][:, cols].sum(1) > 0.01).sum())
    Wm[np.ix_(rows, cols)] = 0.0
    s = Wm[rows].sum(1)
    empty = rows[s < 1e-6]
    Wm[empty, groups[PREFIX + spec.get("fallback", "Spine")]] = 1.0
    Wm[rows] /= np.maximum(Wm[rows].sum(1), 1e-9)[:, None]
    write_weights(body, Wm, rows)
    return dict(피부아닌정점=len(rows), 손몫있던정점=before, 빈정점채움=len(empty), 피부정점=int(skin.sum()))


def smooth_zone(body, G, spec):
    """🔸 나오야 재빌드: 하카마 밑단 z 0.2~0.3에서 UpLeg·Leg 몫 차이가 짧은 변에 몰려 주름이 찢김 → 구역(원본 z) 안 정점 가중치를 이웃 평균으로 여러 번 고른다(합 1 유지)."""
    me = body.data
    co = np.array([v.co[:] for v in me.vertices])
    zlo, zhi = (G @ Vector((0, 0, spec["z_src"][0]))).z, (G @ Vector((0, 0, spec["z_src"][1]))).z
    inside = (co[:, 2] > zlo) & (co[:, 2] < zhi)
    ev = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ev)
    ev = ev.reshape(-1, 2)
    Wm = weight_matrix(body)
    n = len(me.vertices)
    deg = np.zeros(n); np.add.at(deg, ev[:, 0], 1); np.add.at(deg, ev[:, 1], 1)
    keep_cols = [g.index for g in body.vertex_groups if any(k in g.name for k in spec.get("frozen", []))]
    for _ in range(spec["iters"]):
        acc = np.zeros_like(Wm)
        np.add.at(acc, ev[:, 0], Wm[ev[:, 1]]); np.add.at(acc, ev[:, 1], Wm[ev[:, 0]])
        avg = acc / np.maximum(deg, 1)[:, None]
        new = Wm.copy()
        new[inside] = (1 - spec.get("mix", 0.5)) * Wm[inside] + spec.get("mix", 0.5) * avg[inside]
        if keep_cols:
            new[:, keep_cols] = Wm[:, keep_cols]
        new[inside] /= np.maximum(new[inside].sum(1), 1e-9)[:, None]
        Wm = new
    rows = np.nonzero(inside)[0]
    write_weights(body, Wm, rows)
    return dict(정점=len(rows), 반복=spec["iters"])


def move_zone_weights(body, G, spec):
    """🔸 베가펑크(2026-09-17): 목·머리통 밑(원본 z 1.45~) 가운데 정점에 복셀 대리 옮기기가 LeftShoulder·RightShoulder를 25%씩 반대로 줘
    가운데 선(x 0)에서 좌우가 바뀌며 짧은 변이 Idle에서 6~17배로 찢김 → 상자(원본 좌표) 안 정점의 bones 몫을 into로 옮긴다.
    |x| ≤ x_in(원본)이면 전부, x_out 이상이면 안 옮기고 그 사이는 선형."""
    me = body.data
    co = np.array([v.co[:] for v in me.vertices])
    lo = G @ Vector(spec["box_src"][0])
    hi = G @ Vector(spec["box_src"][1])
    lo, hi = np.minimum(lo, hi), np.maximum(lo, hi)
    x_in = abs((G @ Vector((spec["x_in"], 0, 0))).x - (G @ Vector((0, 0, 0))).x)
    x_out = abs((G @ Vector((spec["x_out"], 0, 0))).x - (G @ Vector((0, 0, 0))).x)
    inside = np.all((co >= lo) & (co <= hi), axis=1)
    f = np.clip((x_out - np.abs(co[:, 0])) / max(x_out - x_in, 1e-9), 0.0, 1.0) * inside
    Wm = weight_matrix(body)
    cols = [body.vertex_groups[PREFIX + b].index for b in spec["bones"] if PREFIX + b in body.vertex_groups]
    into = body.vertex_groups.get(PREFIX + spec["into"]) or body.vertex_groups.new(name=PREFIX + spec["into"])
    if into.index >= Wm.shape[1]:
        Wm = np.c_[Wm, np.zeros((len(Wm), into.index + 1 - Wm.shape[1]))]
    moved = Wm[:, cols] * f[:, None]
    Wm[:, cols] -= moved
    Wm[:, into.index] += moved.sum(1)
    rows = np.nonzero(f > 0)[0]
    write_weights(body, Wm, rows)
    return dict(정점=len(rows), 옮긴합=round(float(moved.sum()), 1))


def split_legs(body, G, spec):
    """🔸 오쿠야스(초월_엄태웅_AD 재리깅, 2026-09-18 PM 유니티 반려): 저폴리 두 다리가 가랑이에서 가중치가 좌우 섞여 한 덩어리로 움직였다 →
    원본 z < z_max_src 정점에서 반대쪽 다리 뼈(UpLeg·Leg·Foot·ToeBase) 몫을 지우고 합 1로. x가 center 기준 +면 왼쪽(Left)."""
    me = body.data
    co = np.array([v.co[:] for v in me.vertices])
    zmax = (G @ Vector((0, 0, spec["z_max_src"]))).z
    xc = (G @ Vector((spec.get("center", 0.0), 0, 0))).x
    Wm = weight_matrix(body)
    legs = ("UpLeg", "Leg", "Foot", "ToeBase")
    cols = {side: [body.vertex_groups[PREFIX + side + b].index for b in legs if PREFIX + side + b in body.vertex_groups] for side in ("Left", "Right")}
    rows = np.nonzero(co[:, 2] < zmax)[0]
    cleared = 0
    for r in rows:
        other = cols["Right"] if co[r, 0] > xc else cols["Left"]
        own_up = body.vertex_groups[PREFIX + ("Left" if co[r, 0] > xc else "Right") + "UpLeg"].index
        if Wm[r, other].sum() > 0:
            cleared += 1
            Wm[r, other] = 0.0
            tot = Wm[r].sum()
            if tot <= 1e-9:
                Wm[r, own_up] = 1.0
            else:
                Wm[r] /= tot
    write_weights(body, Wm, rows)
    return dict(구역정점=len(rows), 반대쪽_지운정점=cleared)


def delete_stretched_faces(body, co_before, spec):
    """🔸 나오야 재빌드(2026-09-17): 손이 하카마·기모노에 면으로 붙은 스캔은 T자로 펴면 붙은 면이 손→허리 **실 다발**로 늘어난다(쉬는 자세 자체가 찢김).
    펴기 전후 정점 자리로 면마다 가장 많이 늘어난 변 비율을 재 ratio 넘는 면을 지우고(원래 손이 덮어 안 보이던 접촉면), 경계 고리를 메워 UV는 같은 정점 것 복사."""
    me = body.data
    co_after = np.array([v.co[:] for v in me.vertices])
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    kill = []
    for f in bm.faces:
        idx = [v.index for v in f.verts]
        r = 0.0
        for a, b in zip(idx, idx[1:] + idx[:1]):
            l0 = np.linalg.norm(co_before[a] - co_before[b])
            if l0 > 1e-9:
                r = max(r, np.linalg.norm(co_after[a] - co_after[b]) / l0)
        if r > spec["ratio"]:
            kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    filled = 0
    if spec.get("fill_holes"):
        uvl = bm.loops.layers.uv[0]
        edges = [e for e in bm.edges if e.is_boundary]
        res = bmesh.ops.holes_fill(bm, edges=edges, sides=spec.get("fill_max_sides", 400))
        new_faces = list(res["faces"])
        bm.verts.index_update()
        uv_of = {}
        for f in bm.faces:
            if f in new_faces:
                continue
            for l in f.loops:
                uv_of.setdefault(l.vert.index, l[uvl].uv.copy())
        for f in new_faces:
            for l in f.loops:
                if l.vert.index in uv_of:
                    l[uvl].uv = uv_of[l.vert.index]
        bmesh.ops.triangulate(bm, faces=new_faces)
        filled = len(new_faces)
    islands = []
    seen = set()
    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            comp.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        islands.append(comp)
    islands.sort(key=len, reverse=True)
    dropped = []
    if spec.get("min_island"):                                          # 지운 뒤 몸에서 떨어져 팔에 딸려 가거나 허리에 남는 옷 조각(작은 섬) 삭제
        for comp in islands[1:]:
            if len(comp) < spec["min_island"]:
                dropped.append(len(comp))
                bmesh.ops.delete(bm, geom=comp, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    me.update()
    return dict(지운면=len(kill), 떨어진정점=len(loose), 메운구멍=filled, 섬크기_상위=[len(c) for c in islands[:8]], 지운섬=len(dropped), 지운섬정점=sum(dropped))


def synth_idle_scan(arm, spec):
    """🔸 리카(Generic): 원본 클립이 없는 유닛의 Idle 루프 — fix_unit_fbx.py synth_idle과 같은 식.
    뼈마다 [(세계 축, 진폭°, 위상)] · 각도 = 진폭 × (sin(2πt/N + 위상) − sin(위상)) → 첫·끝 프레임 = 쉬는 자세. 뼈 이름은 PREFIX 뺀 이름."""
    scene = bpy.context.scene
    n, step = int(spec.get("frames", 72)), int(spec.get("step", 3))
    arm.animation_data_create()
    act = bpy.data.actions.new(spec.get("take", "Idle"))
    arm.animation_data.action = act
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    frames = list(range(0, n, step)) + [n]
    for f in frames:
        for bname, waves in spec["bones"].items():
            pb = arm.pose.bones[PREFIX + bname]
            R3 = (arm.matrix_world.to_3x3() @ pb.bone.matrix_local.to_3x3())
            rot = Matrix.Identity(3)
            for axis, deg, phase in waves:
                ang = math.radians(deg) * (math.sin(2 * math.pi * f / n + phase) - math.sin(phase))
                rot = Matrix.Rotation(ang, 3, Vector(axis).normalized()) @ rot
            pb.rotation_quaternion = (R3.inverted() @ rot @ R3).to_quaternion()
            pb.keyframe_insert("rotation_quaternion", frame=1 + f)
    scene.frame_start, scene.frame_end = 1, 1 + n
    scene.frame_set(1)
    return f"{act.name} {n}프레임(키 {len(frames)}) · 뼈 {list(spec['bones'])}"


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
    if spec.get("only_meshes"):                                         # 🔸 박도진: 코트 조각에만(속 바지는 무릎을 따라가야 한다) — 합치기 전 표시한 __coat__ 그룹
        cg = body.vertex_groups.get("__coat__")
        mask = np.zeros(n)
        if cg is not None:
            for v in me.vertices:
                for ge in v.groups:
                    if ge.group == cg.index and ge.weight > 0.5:
                        mask[v.index] = 1.0
        blend = blend * mask
    for gname, gi in groups.items():                                    # 표시용 그룹(__coat__·__heatsrc__)은 가중치 계산에서 뺀다
        if gname.startswith("__"):
            Wm[:, gi] = 0.0
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


def build(name, out_dir=None, render_dir=None):
    cfg = dict(UNITS[name])
    if os.environ.get("SCANRIG_OVERRIDE"):                              # 시험 빌드: 같은 항목으로 설정만 바꿔 병렬로(저장소 경로에는 --out 없이 쓰지 말 것)
        import json as _json
        cfg.update(_json.loads(os.environ["SCANRIG_OVERRIDE"]))
    out_path = f"Assets/Art/Units/{name}/{name}.fbx"
    source = cfg["source"]
    dst = os.path.join(out_dir, os.path.basename(out_path)) if out_dir else os.path.join(ROOT, out_path)
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": source, "sha256": hashlib.sha256(open(source, "rb").read()).hexdigest()}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    src_file, tex_src_dir = source, os.path.dirname(source)
    if cfg.get("member"):                                               # 압축 원본(히소카 zip: source/*.fbx + textures/*.png) — 임시 폴더에 풀어 쓴다(사장님 원본은 그대로)
        import tempfile
        import zipfile
        tmp_dir = tempfile.mkdtemp(prefix="scanrig_")
        with zipfile.ZipFile(source) as z:
            z.extractall(tmp_dir)
        members = [cfg["member"]] if isinstance(cfg["member"], str) else list(cfg["member"])
        for inner in members[:-1]:                                      # 🔸 흰수염: zip 안 zip(source/*.zip 안에 OBJ) — 안쪽 압축을 같은 임시 폴더에 푼다
            with zipfile.ZipFile(os.path.join(tmp_dir, inner)) as z:
                z.extractall(tmp_dir)
        src_file = os.path.join(tmp_dir, members[-1])
        tex_src_dir = os.path.join(tmp_dir, cfg.get("textures_dir", "textures"))
        report["압축 안"] = cfg["member"]
    if src_file.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=src_file, use_anim=False)
    elif src_file.lower().endswith(".obj"):                             # 흰수염: Y위 OBJ — 가져오기가 Z위(x, −z, y)로 돌린다. MTL이 없어도 usemtl 이름으로 재질이 생긴다
        bpy.ops.wm.obj_import(filepath=src_file)
    else:
        bpy.ops.import_scene.gltf(filepath=src_file)
    scene = bpy.context.scene
    if cfg.get("strip_skin"):
        # 🔸 키드(초월_임채민_AP, 2026-09-18): Sketchfab glb가 이름 없는 뼈 781개(bone_N)로 스킨돼 있어 사람 뼈 매핑이 안 된다 →
        #   애니·자세를 지우고(메시 데이터 = 결합 자세 T자) 아마추어 수정자·정점 그룹·아마추어를 떼어 정적 메시로 만든 뒤 새로 리깅한다.
        for a in [o for o in scene.objects if o.type == "ARMATURE"]:
            if a.animation_data:
                a.animation_data.action = None
            for pb in a.pose.bones:
                pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        stripped = 0
        for o in [o for o in scene.objects if o.type == "MESH"]:
            Mw = o.matrix_world.copy()
            for md in [md for md in o.modifiers if md.type == "ARMATURE"]:
                o.modifiers.remove(md)
            o.vertex_groups.clear()
            o.parent = None
            o.matrix_world = Mw
            stripped += 1
        for a in [o for o in scene.objects if o.type == "ARMATURE"]:
            bpy.data.objects.remove(a, do_unlink=True)
        report["스킨 뗌"] = stripped
    meshes = [o for o in scene.objects if o.type == "MESH" and not o.name.startswith("Icosphere")]
    if cfg.get("drop_meshes"):                                          # 🔴 툰 외곽선 껍데기(재질 Outline) — 그대로 두면 캐릭터를 검게 덮는다(덴지·히소카)
        drop = set(cfg["drop_meshes"])
        assert drop <= {o.name for o in meshes}, f"{name}: drop_meshes에 없는 메시 {drop - {o.name for o in meshes}}"
        gone = [o for o in meshes if o.name in drop]
        meshes = [o for o in meshes if o.name not in drop]              # 먼저 목록을 갈라야 한다 — 지운 뒤 o.name을 읽으면 StructRNA removed
        for o in gone:
            bpy.data.objects.remove(o, do_unlink=True)
        report["뺀 메시"] = sorted(drop)
    for dp in cfg.get("drop_loose_parts", []):
        # 🔸 흰수염: 세워 든 대검(무라쿠모기리)이 몸과 **같은 메시**에 느슨한 조각 16개로 들어 있다 — 이음새 붙인 뒤 조각 중심이 x_max보다 왼쪽(−X)인 조각을 지운다
        o = bpy.data.objects[dp["mesh"]]
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dp.get("weld", 0.01))
        bm.verts.ensure_lookup_table()
        Mw = o.matrix_world
        seen, kill, nparts = set(), [], 0
        for v in bm.verts:
            if v.index in seen:
                continue
            stack, comp = [v], []
            seen.add(v.index)
            while stack:
                x = stack.pop()
                comp.append(x)
                for e in x.link_edges:
                    y = e.other_vert(x)
                    if y.index not in seen:
                        seen.add(y.index)
                        stack.append(y)
            c = sum(((Mw @ q.co) for q in comp), Vector()) / len(comp)
            if c.x < dp["x_max"]:
                kill += comp
                nparts += 1
        bmesh.ops.delete(bm, geom=kill, context="VERTS")
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()
        report.setdefault("뺀 조각", []).append(f"{dp['mesh']}: 조각 {nparts} · 정점 {len(kill)}")
    if cfg.get("decimate_by_mesh"):                                     # 조각마다 다른 비율(히소카는 머리카락 9만·눈 1.6만 × 2) — 합치기 전에 각자 이음새 붙이고 감량
        by = cfg["decimate_by_mesh"]
        assert set(by) <= {o.name for o in meshes}, f"{name}: decimate_by_mesh에 없는 메시 {set(by) - {o.name for o in meshes}}"
        stats = {}
        for o in meshes:
            before = sum(len(p.vertices) - 2 for p in o.data.polygons)
            bm = bmesh.new()
            bm.from_mesh(o.data)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()
            r = by.get(o.name, 1.0)
            if r < 1.0:
                mod = o.modifiers.new("decimate", "DECIMATE")
                mod.ratio = r
                dg = bpy.context.evaluated_depsgraph_get()
                nm = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
                o.modifiers.remove(mod)
                om = o.data
                o.data = nm
                bpy.data.meshes.remove(om)
            stats[o.name] = [before, sum(len(p.vertices) - 2 for p in o.data.polygons)]
        report["조각별 감량"] = stats
    for mname in (cfg.get("coat_hem") or {}).get("only_meshes", []):
        o = bpy.data.objects[mname]
        o.vertex_groups.new(name="__coat__").add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    for mname in (cfg.get("heat_proxy") or {}).get("keep_meshes", []):     # 에이스: heat 대리로 쓸 몸 메시 표시(합친 뒤에도 남는다)
        o = bpy.data.objects[mname]
        o.vertex_groups.new(name="__heatsrc__").add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    for mname, bone in cfg.get("rigid_meshes", {}).items():               # 🔸 덴지: bone heat를 망치는 메시(얼굴) — 표시 그룹을 달아 두고 heat 동안 떼었다가 그 뼈 100%로 붙인다
        o = bpy.data.objects[mname]
        o.vertex_groups.new(name="__rigid__" + bone).add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    if len(meshes) > 1:
        # 🔴 합치기 전에 UV 이름을 하나로(히소카 2026-09-16 PM 유니티): 조각마다 UV 이름이 다르면(Character·Hair·Clothing은 UVMap, Eye_L/R은 DiffuseUV)
        #   join이 이름별로 층을 따로 만들어 **첫 UV가 빈 층**이 된다 → 유니티가 _BaseMap을 UV0로 뽑아 텍스처 평균색(회백색)만 보인다. 여분 UV 층도 뺀다.
        uv_names = {}
        for o in meshes:
            uvs = o.data.uv_layers
            if not len(uvs) and cfg.get("add_missing_uv"):              # 🔸 죠고: 단색 로브(Cube.016)는 UV가 아예 없다 — 빈 층(0,0)을 만들어 join이 층을 어긋내지 않게(단색이라 무해)
                uvs.new(name="UVMap")
            assert len(uvs), f"{name}: {o.name}에 UV가 없다"
            uv_names[o.name] = [u.name for u in uvs]
            while len(uvs) > 1:                                          # 첫(활성) 층만 남긴다
                uvs.remove(uvs[len(uvs) - 1])
            uvs[0].name = "UVMap"
        report["UV 이름 통일"] = uv_names
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
    os.makedirs(tex_dir, exist_ok=True)
    wrote = set()
    if cfg.get("glb_images"):                                           # glb 내장 이미지를 인덱스로 뽑는다(레오리오: zip의 textures/ 파일 이름과 glb 이미지 인덱스가 안 맞는다 — 이미지 18개 vs 파일 6개)
        j, binchunk = glb(src_file)
        for index, fname in cfg["glb_images"].items():
            open(os.path.join(tex_dir, fname), "wb").write(image_bytes(j, binchunk, index))
            wrote.add(fname)
        report["glb 이미지"] = {k: v for k, v in cfg["glb_images"].items()}
    if cfg.get("vertex_color_palette"):
        # 🔸 베가펑크(2026-09-17): ZBrush OBJ에 UV·재질·텍스처는 없고 **폴리페인트 정점 색**(점 영역 FLOAT_COLOR)만 있다 — 원작 색이 이미 칠해져 있다.
        #   유니티 URP Lit은 정점 색을 안 쓰니 팔레트 색마다 단색 재질을 만들고, 정점을 가장 가까운 팔레트 색으로 가른 뒤 면은 정점 다수결.
        #   색 값은 속성 값 그대로 선형 Base Color(블렌더 판정 렌더에서 본 색과 같게). UV는 판정 assert(첫 UV 퇴화 금지)용 평면 투영 — 텍스처는 안 쓴다.
        pal = cfg["vertex_color_palette"]
        names = list(pal)
        P = np.array([pal[n] for n in names], dtype=np.float64)
        me = body.data
        ca = me.color_attributes.get(cfg.get("vertex_color_attr", "Color"))
        assert ca is not None and ca.domain == "POINT", f"{name}: 정점 색 속성이 없다 {[(a.name, a.domain) for a in me.color_attributes]}"
        col = np.zeros(len(me.vertices) * 4, dtype=np.float32)
        ca.data.foreach_get("color", col)
        col = col.reshape(-1, 4)[:, :3].astype(np.float64)
        dist = np.linalg.norm(col[:, None, :] - P[None, :, :], axis=2)
        vlab = dist.argmin(1)
        report["팔레트 최대 색 차"] = round(float(dist.min(1).max()), 3)
        me.materials.clear()
        for n in names:
            m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
            m.use_nodes = True
            bsdf = next(nd for nd in m.node_tree.nodes if nd.type == "BSDF_PRINCIPLED")
            bsdf.inputs["Base Color"].default_value = (*pal[n], 1.0)
            bsdf.inputs["Roughness"].default_value = 0.8
            m.diffuse_color = (*pal[n], 1.0)
            m.blend_method = "OPAQUE"
            me.materials.append(m)
        per_face = np.zeros(len(me.polygons), dtype=np.int64)
        for p in me.polygons:
            per_face[p.index] = np.bincount(vlab[list(p.vertices)], minlength=len(names)).argmax()
        me.polygons.foreach_set("material_index", per_face)
        me.color_attributes.remove(ca)
        co = np.zeros(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        lo_c, hi_c = co.min(0), co.max(0)
        li = np.zeros(len(me.loops), dtype=np.int64)
        me.loops.foreach_get("vertex_index", li)
        uvl = me.uv_layers.new(name="UVMap")
        uv = np.c_[(co[li, 0] - lo_c[0]) / (hi_c[0] - lo_c[0]), (co[li, 2] - lo_c[2]) / (hi_c[2] - lo_c[2])]
        uvl.data.foreach_set("uv", uv.ravel())
        me.shade_smooth()
        report["팔레트 면"] = {n: int((per_face == i).sum()) for i, n in enumerate(names)}
    if cfg.get("materials"):                                            # 재질 이름 → [(소켓, 파일)] — FBX가 텍스처 경로를 잃은 경우(히소카) 압축 안 textures/에서 원본 바이트 그대로
        import shutil
        for mat_name, entries in cfg["materials"].items():
            m = body.data.materials.get(mat_name)
            assert m is not None, f"{name}: 재질이 없다 {mat_name} (있는 것 {[mm.name for mm in body.data.materials]})"
            for socket, fname in entries:
                if fname in wrote:                                      # glb에서 이미 뽑은 파일
                    continue
                src_tex = os.path.join(tex_src_dir, cfg.get("texture_sources", {}).get(fname, fname))   # 🔸 에이스: 원본 「$_BaseColor_7.png」 → 커밋본 이름 「_BaseColor_7.png」(유니티 .meta 유지)
                assert os.path.exists(src_tex), f"{name}: 텍스처가 없다 {src_tex}"
                shutil.copyfile(src_tex, os.path.join(tex_dir, fname))
            if m.node_tree is None or not any(nd.type == "BSDF_PRINCIPLED" for nd in m.node_tree.nodes):
                # 🔸 마다라(2026-09-18): glTF가 Principled 없이(Unlit 계열) 들여온 재질 — rebuild_material이 Principled를 찾다 멈춘다 → 노드를 새로 깔아 준다
                m.use_nodes = True
                nt = m.node_tree
                nt.nodes.clear()
                out_node = nt.nodes.new("ShaderNodeOutputMaterial")
                bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
                bsdf.inputs["Roughness"].default_value = 0.8
                nt.links.new(bsdf.outputs[0], out_node.inputs[0])
                report.setdefault("Principled 새로 깐 재질", []).append(m.name)
            rebuild_material(m, entries, tex_dir)
        report["재질→텍스처"] = {k: [f for _, f in v] for k, v in cfg["materials"].items()}
        extra = [mm.name for mm in body.data.materials if mm.name not in cfg["materials"]]
        if cfg.get("keep_solid_materials"):                             # 🔸 죠고: 로브·부츠·칼라·입 등 단색 Principled 재질 19개는 텍스처가 원래 없다 — 기본색 그대로 둔다
            extra = [n for n in extra if any(nd.type == "TEX_IMAGE" for nd in body.data.materials[n].node_tree.nodes)]
            report["단색 재질"] = len([mm for mm in body.data.materials if mm.name not in cfg["materials"]])
        assert not extra, f"{name}: 텍스처를 안 이은 재질 {extra}"
    elif not cfg.get("vertex_color_palette"):
        j, binchunk = glb(source)
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
    def decimate_body():
        mod = body.modifiers.new("decimate", "DECIMATE")
        mod.ratio = cfg["decimate"]
        dg = bpy.context.evaluated_depsgraph_get()
        new_mesh = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        body.modifiers.remove(mod)
        old_mesh = body.data
        body.data = new_mesh
        bpy.data.meshes.remove(old_mesh)
        report["감량 후 삼각형"] = sum(len(p.vertices) - 2 for p in body.data.polygons)

    if not cfg.get("decimate_after_weights"):
        decimate_body()
    if cfg.get("arm_split") and not cfg["arm_split"].get("after_heat"):
        report["소매·코트 가름"] = split_arm_contact(body, G, cfg["joints"], cfg["arm_split"])

    # ── 뼈대: 22개(Hips 루트 + Spine·Spine1·Spine2·Neck·Head + 좌우 Shoulder·Arm·ForeArm·Hand
    # + 좌우 UpLeg·Leg·Foot·ToeBase) — 위 JOINTS_CM(걷는 자세 그대로, 좌우 따로)에 G를 적용해 짓는다.
    # 🔸 리카(2026-09-17): 사람형이 아닌 유닛(다리 대신 꼬리)은 generic_bones로 뼈 목록을 직접 준다 — (이름, 머리, 꼬리, 부모) 원본 좌표.
    table = [tuple(b) for b in cfg["generic_bones"]] if cfg.get("generic_bones") else bone_table(cfg["joints"])
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
    rigid_parts = []
    for bone in dict.fromkeys(cfg.get("rigid_meshes", {}).values()):  # 같은 뼈를 여러 메시가 쓰면(오비토 얼굴 부속 7개 → Head) 한 번만 떼어 낸다
        # 🔴 덴지(2026-09-16): 얼굴 메시(Object_3)가 끼면 몸 전체 bone heat가 실패(가중치 정점 0/14,254 — 얼굴만 떼면 8,424/8,424, 얼굴 혼자는 3,442/5,863).
        #   얼굴을 heat 동안 따로 떼어 두었다가 Head 100%로 다시 붙인다.
        g = body.vertex_groups["__rigid__" + bone]
        for o in scene.objects:
            o.select_set(o == body)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="DESELECT")
        body.vertex_groups.active_index = g.index
        bpy.ops.object.vertex_group_select()
        bpy.ops.mesh.separate(type="SELECTED")
        bpy.ops.object.mode_set(mode="OBJECT")
        part = [o for o in scene.objects if o.type == "MESH" and o != body and o not in [q for q, _ in rigid_parts]][0]
        rigid_parts.append((part, bone))
        body.vertex_groups.remove(body.vertex_groups["__rigid__" + bone])
    if cfg.get("heat_proxy"):
        # 🔸 덴지(2026-09-16): 머리카락 카드·얼굴 속 부품의 비다양체 변 345개로 bone heat가 **전체 실패**(가중치 정점 0 / 14,254) →
        #   몸 사본을 복셀 리메시로 닫힌 한 덩어리로 만들어 거기서 heat를 풀고, 데이터 전송(가장 가까운 면 보간)으로 원본에 옮긴다.
        proxy = body.copy()
        proxy.data = body.data.copy()
        proxy.name = "heat_proxy"
        scene.collection.objects.link(proxy)
        if cfg["heat_proxy"].get("keep_meshes"):
            # 🔸 에이스(2026-09-17): 옷·소품 23조각이 겹쳐 heat가 9,136정점에서 실패 → 대리 = 몸 메시(ace_l)만 남긴 사본. heat는 매끈한 몸 표면에서 풀고
            #   반바지·홀스터·팔찌는 가장 가까운 몸 표면 가중치를 보간해 받는다(가까운 정점 복사는 A자 손 옆 반바지 고리에 손 몫을 줬다).
            keep_idx = proxy.vertex_groups["__heatsrc__"].index
            pbm = bmesh.new()
            pbm.from_mesh(proxy.data)
            dl = pbm.verts.layers.deform.verify()
            bmesh.ops.delete(pbm, geom=[v for v in pbm.verts if keep_idx not in v[dl]], context="VERTS")
            pbm.to_mesh(proxy.data)
            pbm.free()
            report["heat 대리 메시"] = dict(정점=len(proxy.data.vertices), 남긴메시=cfg["heat_proxy"]["keep_meshes"])
        else:
            rm = proxy.modifiers.new("remesh", "REMESH")
            rm.mode = "VOXEL"
            rm.voxel_size = cfg["heat_proxy"]["voxel_m"]
            for o in scene.objects:
                o.select_set(o == proxy)
            bpy.context.view_layer.objects.active = proxy
            bpy.ops.object.modifier_apply(modifier="remesh")
            report["heat 대리 메시"] = dict(정점=len(proxy.data.vertices), 복셀=cfg["heat_proxy"]["voxel_m"])
            if cfg["heat_proxy"].get("keep_largest"):
                # 🔸 베가펑크(2026-09-17): 겹친 ZBrush 부품 100개를 복셀로 합치면 큰 덩어리 하나 + 안쪽 작은 껍데기 56개(12~42정점)가 남아
                #   뼈 없는 작은 섬 때문에 heat가 전체 실패(가중치 0) → 가장 큰 덩어리만 남긴다(옮기기는 가장 가까운 면이라 작은 부품도 받는다).
                pbm = bmesh.new()
                pbm.from_mesh(proxy.data)
                pbm.verts.ensure_lookup_table()
                seen, comps = set(), []
                for v in pbm.verts:
                    if v.index in seen:
                        continue
                    stack, comp = [v], []
                    seen.add(v.index)
                    while stack:
                        x = stack.pop()
                        comp.append(x)
                        for e in x.link_edges:
                            y = e.other_vert(x)
                            if y.index not in seen:
                                seen.add(y.index)
                                stack.append(y)
                    comps.append(comp)
                comps.sort(key=len, reverse=True)
                bmesh.ops.delete(pbm, geom=[v for c in comps[1:] for v in c], context="VERTS")
                pbm.to_mesh(proxy.data)
                pbm.free()
                report["heat 대리 메시"].update(뺀섬=len(comps) - 1, 남은정점=len(proxy.data.vertices))
        for g in [g for g in proxy.vertex_groups if g.name.startswith("__")]:
            proxy.vertex_groups.remove(g)
        for o in scene.objects:
            o.select_set(o in (proxy, arm))
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
        weighted = sum(1 for v in proxy.data.vertices if sum(ge.weight for ge in v.groups) > 1e-6)
        pbm = bmesh.new()
        pbm.from_mesh(proxy.data)
        report["heat 대리 메시"].update(가중치정점=weighted, 비다양체변=sum(1 for e in pbm.edges if not e.is_manifold), 섬=len(proxy.data.vertices) and None)
        pbm.free()
        assert weighted > 0, f"{name}: 대리 메시에서도 bone heat 실패 {report['heat 대리 메시']}"
        for o in scene.objects:
            o.select_set(o in (body, arm))
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.parent_set(type="ARMATURE_NAME")
        for vg in proxy.vertex_groups:
            if vg.name not in body.vertex_groups:
                body.vertex_groups.new(name=vg.name)
        dt = body.modifiers.new("weights", "DATA_TRANSFER")
        dt.object = proxy
        dt.use_vert_data = True
        dt.data_types_verts = {"VGROUP_WEIGHTS"}
        dt.vert_mapping = "POLYINTERP_NEAREST"
        dt.layers_vgroup_select_src = "ALL"
        dt.layers_vgroup_select_dst = "NAME"
        for o in scene.objects:
            o.select_set(o == body)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.modifier_move_to_index(modifier="weights", index=0)
        bpy.ops.object.modifier_apply(modifier="weights")
        bpy.data.objects.remove(proxy, do_unlink=True)
    else:
        for o in scene.objects:
            o.select_set(o in (body, arm))
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for part, bone in rigid_parts:
        for vg in list(part.vertex_groups):
            part.vertex_groups.remove(vg)
        part_n = len(part.data.vertices)
        part.vertex_groups.new(name=PREFIX + bone).add(list(range(part_n)), 1.0, "REPLACE")
        for o in scene.objects:
            o.select_set(o in (body, part))
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()
        report.setdefault("한 뼈로 붙인 메시", []).append(f"{bone} 정점 {part_n}")
    report["가중치 보정"] = fill_unweighted(body)
    if cfg.get("decimate_after_weights"):
        # 🔸 베가펑크(2026-09-17): 겹친 ZBrush 부품을 먼저 0.25로 깎으면 복셀 대리에서도 heat 전체 실패(가중치 0), 안 깎으면 성공(실측) →
        #   원본 해상도에서 가중치를 주고 나서 깎는다(Collapse 감량은 정점 그룹을 보간해 옮긴다).
        decimate_body()
        report["감량 뒤 가중치 보정"] = fill_unweighted(body)
    if cfg.get("delete_bridge_faces") and not cfg["delete_bridge_faces"].get("after_masks"):
        report["다리 놓은 면 지움"] = delete_bridge_faces(body, G, cfg["delete_bridge_faces"])
    if cfg.get("split_by_saturation"):
        report["채도 경계 가름"] = split_by_saturation(body, G, cfg["split_by_saturation"], tex_dir)
    if cfg.get("arm_split", {}).get("after_heat"):
        # 🔴 유지(2026-09-16): 가른 뒤 bone heat는 **전체 실패**(가중치 있는 정점 0 / 22,321 — 가른 자리 정점이 같은 좌표에 두 벌) →
        #   붙은 몸에 먼저 heat를 주고 가른다. bmesh가 가른 정점에 가중치를 복사하니, 뒤의 캡슐·법선 마스크가 몸 쪽 사본의 팔 몫만 뺀다.
        report["소매·코트 가름"] = split_arm_contact(body, G, cfg["joints"], cfg["arm_split"])
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
    if cfg.get("strip_hand_on_materials"):
        # 🔸 죠고(2026-09-16): 팔이 따로 된 메시라 재질로 몸과 갈린다 — 로브 재질 면의 정점에서 손 뼈 가중치만 뺀다(소매는 위팔·아래팔 몫 유지).
        #   캡슐은 두꺼운 주먹·소매를 같이 잘라 소매가 찢기고 손→엉덩이 실이 생겼다(2회차).
        mats = {i for i, mm in enumerate(body.data.materials) if mm.name in set(cfg["strip_hand_on_materials"])}
        assert mats, f"{name}: strip_hand_on_materials 재질이 없다"
        hand_groups = {body.vertex_groups[PREFIX + s + "Hand"].index for s in ("Left", "Right") if PREFIX + s + "Hand" in body.vertex_groups}
        on = {vi for p in body.data.polygons if p.material_index in mats for vi in p.vertices}
        cleared = 0
        for vi in on:
            v = body.data.vertices[vi]
            hw = [(g.group, g.weight) for g in v.groups if g.group in hand_groups and g.weight > 0]
            if not hw:
                continue
            rest = sum(g.weight for g in v.groups if g.group not in hand_groups)
            for gi, _ in hw:
                body.vertex_groups[gi].remove([vi])
            cleared += 1
            if rest <= 1e-6:                                             # 손 뼈뿐이던 정점 — 아래 fill_unweighted가 가까운 가중치로 채운다
                continue
            for g in v.groups:
                body.vertex_groups[g.group].add([vi], g.weight / rest, "REPLACE")
        report["손 가중치 뺀 로브 정점"] = cleared
        report["손 뺀 뒤 채움"] = fill_unweighted(body)
    if cfg.get("arm_facing"):
        report["팔 법선 마스크"] = mask_arm_facing(body, G, cfg["joints"], cfg["arm_facing"])
    if cfg.get("skin_arm"):
        report["피부색 팔"] = skin_arm(body, G, cfg["skin_arm"], tex_dir)
    if cfg.get("rigid_color"):
        report["손 소품 한 뼈"] = rigid_by_color(body, G, cfg["rigid_color"], tex_dir)
    if cfg.get("strip_arm_in_torso"):
        zones = cfg["strip_arm_in_torso"]
        report["몸통 안 팔 가중치 뺌"] = [strip_arm_in_torso(body, G, z, tex_dir) for z in (zones if isinstance(zones, list) else [zones])]
    if cfg.get("arm_leg_radius"):
        report["팔·허벅지 반지름 가름"] = arm_leg_radius(body, G, cfg["joints"], cfg["arm_leg_radius"])
    if cfg.get("delete_bridge_faces", {}).get("after_masks"):          # 마스크로 팔/몸 가중치 경계를 먼저 날카롭게 만든 뒤 지운다(보디빌더 2회차)
        report["다리 놓은 면 지움"] = delete_bridge_faces(body, G, cfg["delete_bridge_faces"])
    if cfg.get("coat_hem"):
        report["코트 자락"] = coat_hem(body, G, cfg["coat_hem"])
    for g in [g for g in body.vertex_groups if g.name.startswith("__")]:     # 표시용 그룹 정리(내보내기 전)
        body.vertex_groups.remove(g)
    if cfg.get("strip_bones_nonskin"):
        report["피부 아닌 곳 손 몫 뺌"] = strip_bones_nonskin(body, G, cfg["strip_bones_nonskin"], tex_dir)
    if cfg.get("split_legs"):
        report["다리 좌우 가름"] = split_legs(body, G, cfg["split_legs"])
    for mz in cfg.get("move_zone_weights", []):
        report.setdefault("구역 가중치 옮김", []).append(move_zone_weights(body, G, mz))
    for zs in cfg.get("smooth_zones", []):
        report.setdefault("구역 가중치 고르기", []).append(smooth_zone(body, G, zs))
    if cfg.get("seed_zero_bones"):
        # 🔴 오비토(2026-09-17 PM 유니티 반려 isHuman False, 매핑 16): 로브에서 팔 몫을 다 빼 팔 뼈 6개가 가중치 0 → FBX 스킨 뼈 목록(bindpose)에서 빠져
        #   유니티 자동 매핑이 팔을 못 잡았다. → 가중치 0 뼈마다 뼈 머리에 가장 가까운 정점 3개에 아주 작은 몫(기본 0.001)을 준다(에렌 add_bones와 같은 수).
        #   T자로 펴도 그 정점이 0.1% 따라갈 뿐이라 겉모습은 그대로.
        w_seed = float(cfg["seed_zero_bones"])
        co = np.array([v.co[:] for v in body.data.vertices])
        seeded = {}
        for b in arm.data.bones:
            g = body.vertex_groups.get(b.name)
            has = g is not None and any(ge.group == g.index and ge.weight > 0 for v in body.data.vertices for ge in v.groups)
            if has:
                continue
            g = g or body.vertex_groups.new(name=b.name)
            near = np.argsort(np.linalg.norm(co - np.array(b.head_local[:]), axis=1))[:3]
            g.add([int(i) for i in near], w_seed, "ADD")
            seeded[b.name[len(PREFIX):]] = [int(i) for i in near]
        report["가중치 0 뼈 씨앗"] = seeded
    for rp in cfg.get("rigid_loose_parts", []):
        # 🔸 흰수염: 왼쪽 허리에 늘어진 띠 자락(떨어진 조각)이 왼주먹 옆이라 heat가 LeftHand에 줘서 T자에서 팔 따라 떠올랐다 →
        #   중심이 원본 좌표 상자 안인 떨어진 조각은 가중치를 지우고 한 뼈 100%.
        bm = bmesh.new()
        bm.from_mesh(body.data)
        bm.verts.ensure_lookup_table()
        lo_w, hi_w = G @ Vector(rp["box_src"][0]), G @ Vector(rp["box_src"][1])
        lo_w, hi_w = [min(a, b) for a, b in zip(lo_w, hi_w)], [max(a, b) for a, b in zip(lo_w, hi_w)]
        seen, hit, nparts = set(), [], 0
        for v in bm.verts:
            if v.index in seen:
                continue
            stack, comp = [v], []
            seen.add(v.index)
            while stack:
                x = stack.pop()
                comp.append(x.index)
                for e in x.link_edges:
                    y = e.other_vert(x)
                    if y.index not in seen:
                        seen.add(y.index)
                        stack.append(y)
            c = sum((bm.verts[i].co for i in comp), Vector()) / len(comp)
            if all(lo_w[k] <= c[k] <= hi_w[k] for k in range(3)) and len(comp) < rp.get("max_verts", 2000):
                hit += comp
                nparts += 1
        bm.free()
        for vg in body.vertex_groups:
            vg.remove(hit)
        body.vertex_groups[PREFIX + rp["bone"]].add(hit, 1.0, "REPLACE")
        report.setdefault("떨어진 조각 한 뼈", []).append(f"{rp['bone']}: 조각 {nparts} · 정점 {len(hit)}")
    co_before_straighten = np.array([v.co[:] for v in body.data.vertices])
    report["걷는 자세→T자(°)"] = straighten_limbs(arm, body, cfg.get("straighten"), cfg.get("arm_drop_deg", 0.0))
    if cfg.get("delete_stretched_faces"):
        report["펴서 늘어난 면 지움"] = delete_stretched_faces(body, co_before_straighten, cfg["delete_stretched_faces"])

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

    uvs = body.data.uv_layers
    uv = np.array([d.uv[:] for d in uvs[0].data])
    span = [float(uv[:, 0].max() - uv[:, 0].min()), float(uv[:, 1].max() - uv[:, 1].min())]
    report["UV0"] = dict(층=[u.name for u in uvs], 범위=[round(v, 4) for v in span],
                         u=[round(float(uv[:, 0].min()), 4), round(float(uv[:, 0].max()), 4)],
                         v=[round(float(uv[:, 1].min()), 4), round(float(uv[:, 1].max()), 4)])
    assert len(uvs) == 1 and min(span) > 0.05, f"{name}: 첫 UV가 퇴화했다(유니티가 텍스처 평균색만 읽는다) {report['UV0']}"
    if not cfg.get("generic_bones"):
        report["휴머노이드 가중치"] = humanoid_weight_check(name, [body])
    clip = False
    if cfg.get("synth_idle"):
        report["지은 Idle"] = synth_idle_scan(arm, cfg["synth_idle"])
        clip = True
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    import io_scene_fbx.export_fbx_bin as fbx_bin                        # 테이크 이름 = 액션 이름(「Armature|Idle」 아닌 「Idle」 — fix_unit_fbx.py와 같은 처리)
    name_of = fbx_bin.get_blenderID_name

    def take_name(bid):
        if isinstance(bid, tuple) and len(bid) == 2 and isinstance(bid[1], bpy.types.Action):
            return bid[1].name
        return name_of(bid)

    fbx_bin.get_blenderID_name = take_name
    try:
        bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                                 primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                                 mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=clip,
                                 bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                                 bake_space_transform=cfg.get("bake_space", False))   # 유지: PM 요청 — 축 변환을 정점에 구워 뿌리 Lcl Rotation (0,0,0)
    finally:
        fbx_bin.get_blenderID_name = name_of
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
