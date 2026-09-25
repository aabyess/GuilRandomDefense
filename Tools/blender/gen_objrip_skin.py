"""OBJ 립(모바일 게임 립) → mixamorig 사람형 FBX. 구현담당2 전담, 2026-09-16 첫 두 건
희귀함_윤현모(조셉 죠스타)·희귀함_장태영(히가시카타 죠스케) — 둘 다 JoJo Diamond Records 립.
  blender -b --factory-startup --python Tools/blender/gen_objrip_skin.py -- [이름 ...] [--out DIR] [--render DIR]

원본이 이중 zip이다: <유닛>.zip 안에 source/Mobile - JoJo's....zip이 또 있고, 그 안에 <이름>.obj·
<이름>.mtl·<이름>.png가 있다(mtl이 map_Kd로 이미 연결). build()가 둘 다 풀어 처리한다.

■ gen_skin_rig.py와 다른 점 — 왜 새 파일인가(PM 지시, 스크립트 하나=한 세션)
- 소스가 glb가 아니라 이중 zip 속 OBJ/MTL/PNG라 그 파일의 glb 파서를 못 쓴다.
- 이 두 립은 PM이 미리 실측 확인한 대로 이미 T자·좌우 대칭(거울 오차 <0.4%)이라 gen_skin_rig.py의
  대칭 joints 표 방식을 그대로 쓸 수 있고(gen_scan_rig.py가 풀어야 했던 비대칭 자세 문제가 없다),
  자세를 펴는 straighten_limbs() 자체가 필요 없다 — 관절 표만 심고 바로 가중치로 간다.
- 뼈 이름은 유니티 Humanoid 자동 매핑용 축약 mixamorig 세트(Hips·Spine·Chest·Neck·Head + 팔다리) —
  gen_skin_rig.py의 SPINE 표(Hips·Spine·Spine1·Spine2·Neck·Head 6개)보다 척추가 하나 적다.
- 가중치: 저폴리(정점 2,300~2,700)·T자·대칭이라 bone heat가 이번엔 원래 의도대로 될 가능성이 커
  (gen_scan_rig.py에서 실패한 건 걷는 자세·훨씬 조밀한 메시였다) 먼저 시도하고, 실패한 뼈가
  하나라도 있으면 gen_scan_rig.py에서 검증한 지오데식(표면 최근접) 방식으로 자동 대체한다.
- PM 경고 3번(Head 직계 자식 뼈 금지 — 아디오 아바타 깨짐 사례): Head는 HeadTop을 tail로만 쓰고
  별도 자식 뼈(앞머리·귀 등)를 만들지 않는다.
- UV0 검사: 내보내기 전에 모든 루프 UV가 [0,1] 안인지 재고 벗어나면 경고(재질 1개·아틀라스
  하나뿐이라 벗어나면 텍스처가 엉뚱하게 씌워질 수 있다).
"""
import hashlib
import json
import math
import os
import shutil
import sys
import tempfile
import zipfile

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
PREFIX = "mixamorig:"

# 🔴 **여기 다섯 종은 「옛 시도」다 — 다시 뽑지 말 것**(2026-09-24, 181개 설정 실행 검사에서 드러남).
#   희귀함_박도진 · 희귀함_선효진 · 전설적인_엄태웅 · 초월_박민수_AD · 초월_엄태웅_AD —
#   **같은 이름이 gen_scan_rig.py에도 있고, 커밋된 FBX를 만든 것은 그쪽이다.**
#   근거(직접 재서 대조): 희귀함_박도진
#       커밋본            정점 20,562 · 삼각형 39,577 · 뼈 22(Spine1·Spine2)
#       gen_scan_rig 산출 정점 20,562 · 삼각형 39,577 · 뼈 22(Spine1·Spine2)  ← **한 개도 안 다름**
#       이 파일 산출      정점 25,857 · 삼각형 49,369 · 뼈 21(Chest)          ← 26% 더 많음
#   그리고 커밋된 objrip 산출 21종 중 **Spine1/Spine2를 가진 것이 정확히 이 다섯**이다(나머지 16종은 Chest).
#   → 이 다섯을 이 파일로 다시 뽑으면 **다른 모델이 조용히 들어간다.** rebuild="금지"로 막아 두고,
#     지운 게 아니라 남겨 둔 이유는 「왜 금지인지」가 여기 있어야 다음 사람이 안 되돌리기 때문이다.
#   ⚠️ 커밋본들은 게임에서 멀쩡하다. **고칠 게 있어서 금지가 아니라, 바꾸면 안 되니까 금지다.**
SKINS = {
    # ══════════════ 적 유닛(Enemy) — 2026-09-24. 산출만 Assets/Art/Enemies로 나간다.
    # 원피스 해적무쌍 계열 립(Sketchfab glb, 재질 MI_N1xx_E001_*) → R11·R12·R15. **뼈 0인 정적 glb**.
    #   세 대장이 **같은 Mantle(해군 코트) 메시**를 쓴다 — rigid_materials로 코트를 Chest에 못박는다(build() 주석).
    #   A자(팔 약 40° 처짐) → straighten_arms. 원본 키 3.38~3.39(단위 없음, 머리카락까지).
    # R11 김정래 = 키자루. 정점 23,672 · 재질 7. 관절 근거(높이 1 정규화, 코트 뺀 몸만 층마다 잼):
    #   z 0.90 위 폭 ±0.034 = 머리 · 0.875 깃 · 팔 중심선 x0.10 z0.835 → x0.22 z0.74 → x0.34 z0.628 → 손끝 x0.40 z0.607
    #   z 0.55부터 아래로 두 다리가 갈린다(0.575엔 틈 없음) → 가랑이 ≈0.56 · 다리 중심 x ±0.05(틈 ±0.028·바깥 ±0.071)
    "김정래": dict(source="~/Desktop/구랜디스킨모음/90_적유닛/R11-R20/R11_김정래.glb",
               mesh_name="Kizaru",
               path="Assets/Art/Enemies/김정래/김정래.fbx", height=1.8,
               center_band=(0.02, 0.06),
               straighten_arms=True,
               rigid_materials={"Mantle": "Chest"}, texture_by_material=True,
               joints=dict(Hips=(0, 0, 0.58), Spine=(0, 0, 0.67), Chest=(0, 0, 0.78),
                           Neck=(0, 0, 0.875), Head=(0, 0, 0.905), HeadTop=(0, 0, 1.0),
                           Shoulder=(0.03, 0.01, 0.84), Arm=(0.11, 0.02, 0.83), ForeArm=(0.22, 0.02, 0.735),
                           Hand=(0.33, 0.02, 0.64), HandTip=(0.40, 0.02, 0.607),
                           UpLeg=(0.05, 0, 0.555), Leg=(0.05, 0, 0.30), Foot=(0.052, 0.01, 0.045),
                           ToeBase=(0.052, -0.05, 0.02), ToeTip=(0.052, -0.075, 0.015))),
    # R12 박예원 = 아오키지. 정점 26,639 · 재질 6. 키자루와 비율이 거의 같다(같은 게임 리그). 관절 근거:
    #   머리 z 0.90 위(폭 ±0.05) · 팔 중심선 x0.09 z0.815 → x0.21 z0.748 → x0.30 z0.63 → 손끝 x0.38 z0.615
    #   z 0.525에서 가운데 틈이 처음 열린다(0.55엔 없음) → 가랑이 ≈0.54 · 다리 중심 x ±0.05
    "박예원": dict(source="~/Desktop/구랜디스킨모음/90_적유닛/R11-R20/R12_박예원.glb",
               mesh_name="Aokiji",
               path="Assets/Art/Enemies/박예원/박예원.fbx", height=1.8,
               center_band=(0.02, 0.06),
               straighten_arms=True,
               rigid_materials={"Mantle": "Chest"}, texture_by_material=True,
               joints=dict(Hips=(0, 0, 0.565), Spine=(0, 0, 0.66), Chest=(0, 0, 0.77),
                           Neck=(0, 0, 0.875), Head=(0, 0, 0.905), HeadTop=(0, 0, 1.0),
                           Shoulder=(0.03, 0.01, 0.835), Arm=(0.10, 0.03, 0.82), ForeArm=(0.21, 0.03, 0.74),
                           Hand=(0.31, 0.03, 0.64), HandTip=(0.38, 0.03, 0.615),
                           UpLeg=(0.05, 0, 0.54), Leg=(0.05, 0, 0.29), Foot=(0.05, 0.01, 0.045),
                           ToeBase=(0.05, -0.05, 0.02), ToeTip=(0.05, -0.075, 0.015))),
    # R15 유재헌 = 아카이누. 정점 21,770 · 재질 6(Met=모자). 팔이 셋 중 가장 길다(손끝 x 0.44). 관절 근거:
    #   머리 z 0.90 위 · 팔 중심선 x0.12 z0.815 → x0.24 z0.742 → x0.33 z0.643 → 손끝 x0.44 z0.587
    #   z 0.525에서 가운데 틈이 열린다(0.55엔 없음) → 가랑이 ≈0.54 · 다리 중심 x ±0.05
    "유재헌": dict(source="~/Desktop/구랜디스킨모음/90_적유닛/R11-R20/R15_유재헌.glb",
               mesh_name="Akainu",
               path="Assets/Art/Enemies/유재헌/유재헌.fbx", height=1.8,
               center_band=(0.02, 0.06),
               straighten_arms=True,
               # 🔴 join_all — 없으면 **모자(Met)·눈동자(Iris)가 「떨어진 조각」으로 버려진다**(1차 빌드: 원본 키 3.384→3.307,
               #   텍스처 6→4장). 본체가 몸통 옷(Wear)이라 얼굴 위 모자까지 거리가 문턱(키의 5%)을 넘는다. 여섯 조각 전부 몸에 붙은 것(화면 확인).
               join_all=True, keep_fused_faces=True,
               rigid_materials={"Mantle": "Chest"}, texture_by_material=True,
               joints=dict(Hips=(0, 0, 0.56), Spine=(0, 0, 0.66), Chest=(0, 0, 0.77),
                           Neck=(0, 0, 0.875), Head=(0, 0, 0.905), HeadTop=(0, 0, 1.0),
                           Shoulder=(0.035, 0.0, 0.83), Arm=(0.12, 0.01, 0.815), ForeArm=(0.235, 0.01, 0.73),
                           Hand=(0.35, 0.01, 0.63), HandTip=(0.44, 0.01, 0.587),
                           UpLeg=(0.05, 0, 0.535), Leg=(0.05, 0, 0.29), Foot=(0.052, 0.01, 0.045),
                           ToeBase=(0.052, -0.05, 0.02), ToeTip=(0.052, -0.075, 0.015))),
    # R20 박은석 = 제저스 버지스 ★보스. 같은 해적무쌍 립이지만 **코트가 없고 체형이 완전히 다르다** — 표를 베끼지 말 것.
    #   정점 24,050 · 재질 6(Met=머리 장식). 다리가 짧고(가랑이 ≈0.34) 몸통·팔이 거대하며 머리가 어깨에 파묻혔다.
    #   관절 근거(높이 1 정규화 · 층 측정 + 정면 화면 픽셀 대조):
    #     팔꿈치 검은 띠 중심 x0.43 z0.605 · 장갑 시작(손목) x0.625 z0.43 · 손끝 x0.74 z0.335 (층: x0.62 z0.40 · x0.74 z0.334)
    #     벨트 z≈0.45 · 청바지 두 다리가 z≈0.34 아래로 갈린다 · 다리 중심 x ±0.055(바깥 ±0.084)
    #     수염 아래끝 z≈0.80 · 얼굴 0.80~0.99 — 목이 거의 없다(Neck 짧게)
    "박은석": dict(source="~/Desktop/구랜디스킨모음/90_적유닛/R11-R20/R20_박은석_보스.glb",
               mesh_name="Burgess",
               path="Assets/Art/Enemies/박은석/박은석.fbx", height=1.8,
               center_band=(0.02, 0.06),
               straighten_arms=True, texture_by_material=True,
               # 🔴 join_all — 없으면 머리 장식(Met)이 버려진다(1차 빌드: 원본 키 3.446→3.362). 유재헌과 같은 이유.
               # 🔴 force_geodesic — bone heat가 「뼈가 다 살아 있다」로 통과했지만 **전신에 번졌다**(1차: Hand 15,829 · Foot 12,908
               #   · Hips 14,179정점이 w>0.01 — 전체 16,915). 거대한 팔이 몸통에 닿을 듯 붙어 열이 샌다. 이어서 팔↔다리 융착 면
               #   219개가 **지워지고** 키가 1.705로 줄었다. 죽은 뼈 검사는 이 번짐을 못 본다 — 뼈별 정점 수를 보고 잡았다.
               join_all=True, force_geodesic=True, keep_fused_faces=True,
               reassign_above=[dict(src=("LeftUpLeg", "RightUpLeg"), dst="Hips", z=0.41)],
               joints=dict(Hips=(0, 0, 0.42), Spine=(0, 0, 0.56), Chest=(0, 0, 0.70),
                           Neck=(0, 0, 0.83), Head=(0, 0, 0.86), HeadTop=(0, 0, 1.0),
                           Shoulder=(0.08, 0, 0.80), Arm=(0.24, 0, 0.79), ForeArm=(0.43, 0, 0.605),
                           Hand=(0.625, 0, 0.43), HandTip=(0.75, 0, 0.33),
                           UpLeg=(0.055, 0, 0.38), Leg=(0.055, 0, 0.20), Foot=(0.055, 0.01, 0.045),
                           ToeBase=(0.055, -0.05, 0.02), ToeTip=(0.055, -0.08, 0.015))),
    # 드래곤볼 부도카이3 크리링 립 → R01 박진웅. **뼈 0인 정적 OBJ**라 새로 리깅한다.
    #   정점 1,396 · 면 2,504 · 재질 19(텍스처 10장) · **이미 T자**라 이 파이프라인이 그대로 맞는다.
    #   🔴 관절 자리는 **손으로 안 지었다** — 높이 1로 정규화해 층마다 폭을 재서 뽑았다:
    #        z 0.65~0.80에서 x폭이 0.94로 튄다 → **팔 높이 0.72**(그 위 0.80~1.00이 머리)
    #        z 0.45~0.50이 가장 좁다(0.157) → **허리/엉덩이 0.50**
    #        z 0.15~0.30이 가장 넓다(0.30) → 두 다리, 각 다리 중심 x ±0.075
    #      크리링은 **키가 작고 머리가 크다**(머리가 위 20%를 차지한다) — 다른 유닛 표를 베끼면 안 맞는다.
    # 드래곤볼 천진반 립 → R02 김갑식. **뼈 0인 정적 OBJ**, 크리링과 같은 립 계열이지만 **T자가 아니다**
    #   (팔이 옆으로 늘어진 선 자세) → `straighten_arms=True`로 편다. 희귀함_유재헌과 같은 길이다.
    #   정점 2,580 · 면 4,943 · 재질 3(Tien.png·face·eyes·bottom).
    #   ⚠️ zip 안에 `Tien.blend`도 있지만 **리그가 없다**(메시·카메라·조명뿐, 정점그룹 0·액션 0. 직접 열어 확인).
    #   🔴 관절은 **처진 자세 그대로** 넣는다(가중치가 실제 메시와 맞아야 한다) — 펴는 건 straighten_arms가 나중에 한다.
    #   관절 자리 근거(높이 1 정규화 · 층마다 XY 뭉치를 세어 뽑음, 몸 중심 x=0.221):
    #     z 0.86~1.00 뭉치 하나(머리) · z 0.74~0.78 하나(가슴, 팔이 아직 몸에 붙음)
    #     z 0.66부터 좌우 대칭 두 뭉치가 갈림(팔) → 0.66에서 ±0.169 · 0.50에서 ±0.194가 가장 멀다(손)
    #     z 0.44 아래로 옆 뭉치가 사라진다 → **손끝 0.44**
    #     다리는 z 0.22에서 ±0.074인데 z 0.02(발)에서 ±0.165 → **아래로 벌어진다**(차렷이 아니라 벌린 자세)
    "김갑식": dict(source="~/Desktop/구랜디스킨모음/90_적유닛/R01-R10/R02_김갑식.zip",
               mesh_name="Tien",
               path="Assets/Art/Enemies/김갑식/김갑식.fbx", height=1.8,
               center_band=(0.02, 0.06), chest_ratio=0.75,
               straighten_arms=True,
               joints=dict(Hips=(0, 0, 0.53), Spine=(0, 0, 0.63), Chest=(0, 0, 0.75),
                           Neck=(0, 0, 0.84), Head=(0, 0, 0.88), HeadTop=(0, 0, 1.0),
                           Shoulder=(0.055, 0, 0.80), Arm=(0.085, 0, 0.78), ForeArm=(0.160, 0, 0.62),
                           Hand=(0.185, 0, 0.48), HandTip=(0.190, 0, 0.44),
                           UpLeg=(0.070, 0, 0.47), Leg=(0.100, 0, 0.26), Foot=(0.150, -0.01, 0.045),
                           ToeBase=(0.150, -0.10, 0.02), ToeTip=(0.150, -0.15, 0.015))),
    "박진웅": dict(source="~/Desktop/구랜디스킨모음/90_적유닛/R01-R10/R01_박진웅.zip",
               mesh_name="Krillin_Budokai_3",
               path="Assets/Art/Enemies/박진웅/박진웅.fbx", height=1.8,
               center_band=(0.02, 0.06), chest_ratio=0.70,
               joints=dict(Hips=(0, 0, 0.50), Spine=(0, 0, 0.58), Chest=(0, 0, 0.70),
                           Neck=(0, 0, 0.79), Head=(0, 0, 0.83), HeadTop=(0, 0, 1.0),
                           Shoulder=(0.06, 0, 0.72), Arm=(0.11, 0, 0.72), ForeArm=(0.26, 0, 0.72),
                           Hand=(0.40, 0, 0.72), HandTip=(0.46, 0, 0.72),
                           UpLeg=(0.075, 0, 0.48), Leg=(0.075, 0, 0.26), Foot=(0.075, -0.01, 0.04),
                           ToeBase=(0.075, -0.06, 0.02), ToeTip=(0.075, -0.09, 0.015))),
    # 조셉 죠스타 — 키 2.010(원본), 팔이 0.81H에서 수평, 폭÷키 0.956. 스카프가 앞으로 두껍게
    # 튀어나와(앞뒤 0.958) 몸 뼈(Spine·Chest)를 잘 따라가는지 렌더로 확인 필요.
    "희귀함_윤현모": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_윤현모.zip",
        mesh_name="Joseph",
        path="Assets/Art/Units/희귀함_윤현모/희귀함_윤현모.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.81,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0.01, 0.62), Chest=(0, 0.01, 0.81),
            Neck=(0, -0.01, 0.895), Head=(0, -0.02, 0.93), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.055, 0, 0.81), Arm=(0.10, 0, 0.81), ForeArm=(0.245, 0, 0.81),
            Hand=(0.414, 0, 0.81), HandTip=(0.478, 0, 0.81),
            UpLeg=(0.09, 0, 0.485), Leg=(0.09, 0, 0.27), Foot=(0.09, -0.01, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 히가시카타 죠스케 — 키 1.846(원본), 팔이 0.825H에서 수평, 폭÷키 0.981. 긴 코트 자락이 허벅지
    # 아래까지 내려와(Hips·UpLeg 근처) 다리 뼈에 자연스럽게 붙는지 렌더로 확인 필요.
    "희귀함_장태영": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_장태영.zip",
        mesh_name="Josuke",
        path="Assets/Art/Units/희귀함_장태영/희귀함_장태영.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.825,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0.01, 0.63), Chest=(0, 0.01, 0.825),
            Neck=(0, -0.01, 0.90), Head=(0, -0.02, 0.935), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.05, 0, 0.825), Arm=(0.09, 0, 0.825), ForeArm=(0.235, 0, 0.825),
            Hand=(0.405, 0, 0.825), HandTip=(0.491, 0, 0.825),
            UpLeg=(0.085, 0, 0.485), Leg=(0.085, 0, 0.27), Foot=(0.085, -0.01, 0.045),
            ToeBase=(0.085, -0.14, 0.02), ToeTip=(0.085, -0.20, 0.015),
        ),
    ),
    # 나란챠(강보명) — 키 1.711(원본), 팔이 0.78H에서 수평, 팔폭÷키 0.92(핸드팁 x비 0.4585로 실측 일치).
    # 깊이 0.357로 얇아(조셉의 스카프 같은 돌출 없음) 몸 뼈 가중치가 가장 단순할 것으로 예상.
    "희귀함_강보명": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_강보명.zip",
        mesh_name="Narancia",
        path="Assets/Art/Units/희귀함_강보명/희귀함_강보명.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.78,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0.01, 0.61), Chest=(0, 0.01, 0.78),
            Neck=(0, -0.01, 0.87), Head=(0, -0.02, 0.91), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.045, 0, 0.78), Arm=(0.08, 0, 0.78), ForeArm=(0.224, 0, 0.78),
            Hand=(0.394, 0, 0.78), HandTip=(0.4585, 0, 0.78),
            UpLeg=(0.08, 0, 0.485), Leg=(0.08, 0, 0.27), Foot=(0.08, -0.01, 0.045),
            ToeBase=(0.08, -0.14, 0.02), ToeTip=(0.08, -0.20, 0.015),
        ),
    ),
    # 스피드왜건(최현우) — 키 1.893(원본), 팔이 0.80~0.85H 대에서 벌어짐(T자), 핸드팁 x비 0.4585.
    # 🔴 OBJ에 "o" 그룹이 둘인데 이번엔 나란챠와 반대로 떨어진 소품(왼발 옆 바닥, 모자로 추정,
    # 본체와 거리 중앙값 0.637 — 몸 키의 1/3 가까이)이라 자동으로 버려진다(build()의 거리 문턱
    # 판정 참고, 렌더로 눈으로 한 번 더 확인).
    "희귀함_최현우": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_최현우.zip",
        mesh_name="Speedwagon",
        path="Assets/Art/Units/희귀함_최현우/희귀함_최현우.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.82,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0.01, 0.63), Chest=(0, 0.01, 0.82),
            Neck=(0, -0.01, 0.90), Head=(0, -0.02, 0.935), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.05, 0, 0.82), Arm=(0.09, 0, 0.82), ForeArm=(0.235, 0, 0.82),
            Hand=(0.405, 0, 0.82), HandTip=(0.4585, 0, 0.82),
            UpLeg=(0.085, 0, 0.485), Leg=(0.085, 0, 0.27), Foot=(0.085, -0.01, 0.045),
            ToeBase=(0.085, -0.14, 0.02), ToeTip=(0.085, -0.20, 0.015),
        ),
    ),
    # 야가미 라이토(데스노트, AI 생성 glb→fbx 립) — PM 실측: 뼈 0·정점 52,989(≈10.6만 삼각형,
    # 3~4만으로 감량)·좌우 대칭 99.9%·이미 −Y 정면. 🔴 T자가 아니라 A자(팔이 수평에서 약 33°
    # 처짐, 팔 끝 높이비 61%) — straighten_arms=True로 편다. 🔴 메시 노드 Lcl Scaling=100 우려
    # (실측 결과 이 프로젝트 파이프라인은 항상 겉싸개를 정점에 굽어 문제없었지만, 노트북 사고와
    # 같은 부류라 check_model_scale=True로 내보낸 뒤 원시 파싱 재확인). 관절 좌표는 실제 A자
    # 자세를 그대로 측정한 값(가중치 계산이 실제 메시 모양과 맞아야 하므로) — 목표(수평) 각도가
    # 아니라 처진 각도 그대로 넣고, straighten_arms()가 다 지은 뒤에 수평으로 편다.
    "희귀함_유재헌": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_유재헌.zip",
        mesh_name="Light",
        path="Assets/Art/Units/희귀함_유재헌/희귀함_유재헌.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        decimate_ratio=0.35,
        straighten_arms=True,
        check_model_scale=True,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0.005, 0.65), Chest=(0.033, 0.0, 0.78),
            Neck=(0, 0, 0.859), Head=(0, -0.01, 0.927), HeadTop=(0, -0.01, 1.0),
            # 🔴 PM 실측(유니티 Idle, 2026-09-17) — 처음 잡은 관절은 어깨·손끝 위치(전체 팔
            # 폭)는 얼추 맞아 T자 렌더는 멀쩡했지만 팔꿈치·손목 관절이 실제 메시보다 어깨 쪽으로
            # 너무 몰려 있었다(위팔·아래팔 길이가 카카시 대비 절반 — 유니티가 팔꿈치를 구부리면
            # 메시 위팔 중간이 꺾였다). 원본을 다시 실측(윤곽선 스캔, x/H 정규화 재확인)해 어깨→
            # 손끝까지 부드럽게 넓어지다 59% 높이에서 뚝 끊기는 것(손끝)을 확인하고 그 사이를
            # 고르게 나눠 다시 잡았다.
            Shoulder=(0.075, 0, 0.84), Arm=(0.131, 0.02, 0.80), ForeArm=(0.287, 0.025, 0.70),
            Hand=(0.394, 0.025, 0.62), HandTip=(0.413, 0.025, 0.59),
            UpLeg=(0.09, 0, 0.485), Leg=(0.09, 0, 0.27), Foot=(0.09, -0.02, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 리조토 네로(장하민) — 키 1.885(원본), 이미 T자(팔 끝 81%H, 팔폭÷키 0.97), 좌우 대칭 100%,
    # "o" 그룹 1개(떨어진 조각 없음, join 판정 자체가 불필요). 깊이 0.54로 조셉보다 얇음(두건 끝
    # 정도만 튀어나올 것으로 예상, 렌더로 확인).
    "희귀함_장하민": dict(
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_장하민.zip",
        mesh_name="Risotto",
        path="Assets/Art/Units/희귀함_장하민/희귀함_장하민.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.81,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0.01, 0.62), Chest=(0, 0.01, 0.81),
            Neck=(0, -0.01, 0.895), Head=(0, -0.02, 0.93), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.055, 0, 0.81), Arm=(0.10, 0, 0.81), ForeArm=(0.245, 0, 0.81),
            Hand=(0.414, 0, 0.81), HandTip=(0.4871, 0, 0.81),
            UpLeg=(0.09, 0, 0.485), Leg=(0.09, 0, 0.27), Foot=(0.09, -0.01, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 보디빌더(선효진) — Sketchfab glb(zip 아님, extract_source()에 .glb 직접 처리 분기 추가),
    # 정점 65532+65532+47686(거의 같은 크기 조각 셋 — join()의 20% 정점비 우회 규칙으로 확인,
    # 거리 중앙값만으로는 5% 문턱을 살짝 넘겨 하나가 버려질 뻔했다), 원본 302,223삼각형 →
    # decimate_ratio로 3-4만대로 감량. "차렷" 자세(팔이 수평 T가 아니라 옆구리에 붙어 늘어짐,
    # 겨드랑이 밑 틈이 좁아 bone heat가 가슴↔팔 사이에서 새어 나갈 위험 — 렌더로 겨드랑이 부위
    # "날개" 아티팩트 없는지 확인 필요) → 관절 좌표는 이 처진 자세 그대로 실측해 심고,
    # straighten_arms=True로 다 지은 뒤 수평 T로 편다(희귀함_유재헌과 같은 처리).
    # 크로치(다리 갈라지는 높이) 직접 스캔 실측: 키의 37% 지점.
    "희귀함_선효진": dict(
        rebuild="금지",   # ← 위 「옛 시도 다섯」 참고. 커밋본은 gen_scan_rig.py가 만든다.
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_선효진.glb",
        mesh_name="Bodybuilder",
        path="Assets/Art/Units/희귀함_선효진/희귀함_선효진.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        decimate_ratio=0.12,
        straighten_arms=True,
        joints=dict(
            Hips=(0, 0, 0.40), Spine=(0, 0, 0.60), Chest=(0, 0, 0.82),
            Neck=(0, 0, 0.87), Head=(0, 0, 0.895), HeadTop=(0, 0, 1.0),
            Shoulder=(0.06, 0, 0.84), Arm=(0.14, 0, 0.815), ForeArm=(0.17, -0.02, 0.62),
            Hand=(0.17, -0.05, 0.48), HandTip=(0.16, -0.08, 0.42),
            UpLeg=(0.13, 0, 0.37), Leg=(0.13, 0, 0.185), Foot=(0.13, -0.01, 0.055),
            ToeBase=(0.13, -0.14, 0.02), ToeTip=(0.13, -0.20, 0.015),
        ),
    ),
    # 사무라이 소드(박도진) — Sketchfab glb, 몸을 헤드·재킷·바지·손·신발 등 10개 오브젝트로 쪼개
    # 놓음(각자 스케일이 달라 matrix_world 적용 필수, join() 후 raw co를 그대로 읽으면 틀린다).
    # 이미 T자·좌우 대칭(PM 실측 94%) — straighten_arms 불필요. 실측: 어깨 폭 정점 최댓값이
    # 키의 0.51(양쪽 합 1.02, 정상 인체 비율), 긴 트렌치코트가 발목까지 내려와 사타구니 틈이
    # 옷으로 가려짐(따로 크로치 높이를 재지 못해 표준 비율 사용).
    "희귀함_박도진": dict(
        rebuild="금지",   # ← 아래 「옛 시도 다섯」 참고. 이 유닛의 커밋본은 gen_scan_rig.py가 만든다.
        source="~/Desktop/구랜디스킨모음/04_희귀함/희귀함_박도진.glb",
        mesh_name="SamuraiSword",
        path="Assets/Art/Units/희귀함_박도진/희귀함_박도진.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.48), Spine=(0, 0, 0.62), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.875), Head=(0, 0, 0.91), HeadTop=(0, 0, 1.0),
            Shoulder=(0.07, 0, 0.82), Arm=(0.14, 0, 0.82), ForeArm=(0.28, 0, 0.82),
            Hand=(0.42, 0, 0.82), HandTip=(0.51, 0, 0.82),
            UpLeg=(0.09, 0, 0.48), Leg=(0.09, 0, 0.25), Foot=(0.09, -0.02, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 전설적인_엄태웅(포트거스 D. 에이스) — Substance 저장 FBX(source/*.fbx, 이중 zip 아님, 안쪽
    # zip 없이 outer/textures/*.png 5장). UnitScale 100 우려는 무관(우리 파이프라인은 실측 키로
    # 다시 스케일). 메시 23개 전부 의상·소품이라 크기비 조인 규칙으로 다 합침. 팔 벌린 A자
    # (40~60% 높이대 폭이 실측 약 키의 0.55~0.60) → straighten_arms로 T자로 편다.
    # 🔴 재질 5개(cuerpo·detalles·ropa·bolas·cinturones) 전부 텍스처 노드 0(단색) — Substance
    # BaseColor 5장을 그림으로 대조해 대응(texture_files): cuerpo=얼굴 있는 살구색 살결($_7),
    # detalles=자잘한 장식 아틀라스(하트·줄무늬·눈 모양, $_13), ropa=주황+파랑 큰 덩어리($_1),
    # bolas=진한 적갈색 구슬($_기본), cinturones=거의 검정에 가까운 남색 단색($_19).
    "전설적인_엄태웅": dict(
        rebuild="금지",   # ← 위 「옛 시도 다섯」 참고. 커밋본은 gen_scan_rig.py가 만든다.
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_엄태웅.zip",
        mesh_name="Ace",
        path="Assets/Art/Units/전설적인_엄태웅/전설적인_엄태웅.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        straighten_arms=True,
        join_all=True,
        texture_files={
            "cuerpo": "$_BaseColor_7.png",
            "detalles": "$_BaseColor_13.png",
            "ropa": "$_BaseColor_1.png",
            "bolas": "$_BaseColor.png",
            "cinturones": "$_BaseColor_19.png",
        },
        joints=dict(
            Hips=(0, 0, 0.46), Spine=(0, 0, 0.62), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.87), Head=(0, 0, 0.905), HeadTop=(0, 0, 1.0),
            # 🔴 PM 실측(유니티 Idle, 2026-09-17) — 위팔·아래팔이 카카시 대비 허벅지 비율로 짧아
            # (63%·62%, 정상 87%·69%) 손목 관절이 실제 손목(팔찌 위치)보다 안쪽에 있는 것으로
            # 추정(손목이 바깥으로 꺾이고 손가락이 벌어지는 증상). 비례 연장을 한 번 시도했으나
            # (ForeArm/Hand를 늘려 재배치) LeftHand/RightHand 가중치 정점이 42/42 → 18/36으로
            # 오히려 줄어(손목 관절이 실제 손 표면에서 더 멀어짐 — 저폴리+극단 배율이라 손 자체가
            # 원래도 정점이 희박해 손 위치 오차에 특히 민감) 되돌렸다. 정확한 손목 위치는 유니티
            # 쪽 월드 좌표 실측이 더 믿을 만해 보여 PM 판단 대기 — 원래 값 유지.
            Shoulder=(0.08, 0, 0.82), Arm=(0.14, 0, 0.78), ForeArm=(0.22, -0.01, 0.66),
            Hand=(0.295, -0.02, 0.54), HandTip=(0.31, -0.03, 0.48),
            UpLeg=(0.09, 0, 0.45), Leg=(0.09, 0, 0.22), Foot=(0.09, -0.02, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 전설적인_박민석(모하메드 압둘) — JoJo 립(조셉·리조토와 같은 이중 zip). 이미 T자·좌우 대칭
    # 99.4%·그룹 1개(join 판정 불필요). PM 경고: 발목까지 오는 긴 로브 자락이 10~30% 높이대에서
    # 키의 1.0 안팎까지 퍼져(다리보다 훨씬 넓다) 좌우 다리 사이를 가로지른다 — 자락 면이 한쪽
    # 다리에만 붙으면 걸을 때 찢어질 수 있어 렌더로 확인 필요.
    "전설적인_박민석": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_박민석.zip",
        mesh_name="Avdol",
        path="Assets/Art/Units/전설적인_박민석/전설적인_박민석.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.485), Spine=(0, 0.01, 0.62), Chest=(0, 0.01, 0.81),
            Neck=(0, -0.01, 0.89), Head=(0, -0.02, 0.93), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.055, 0, 0.81), Arm=(0.10, 0, 0.81), ForeArm=(0.25, 0, 0.81),
            Hand=(0.41, 0, 0.81), HandTip=(0.485, 0, 0.81),
            UpLeg=(0.09, 0, 0.485), Leg=(0.09, 0, 0.27), Foot=(0.09, -0.01, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 전설적인_김정래(브루노 부차라티) — JoJo 립. 이미 T자·좌우 대칭 100%·그룹 1개(join 판정
    # 불필요). 깊이 0.299로 몸에 붙는 정장이라 압둘 같은 로브 문제 없음(0~70% 폭이 0.25~0.41로
    # 일정).
    "전설적인_김정래": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_김정래.zip",
        mesh_name="Bruno",
        path="Assets/Art/Units/전설적인_김정래/전설적인_김정래.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.485), Spine=(0, 0.01, 0.62), Chest=(0, 0.01, 0.82),
            Neck=(0, -0.01, 0.895), Head=(0, -0.02, 0.93), HeadTop=(0, -0.02, 1.0),
            Shoulder=(0.055, 0, 0.82), Arm=(0.10, 0, 0.82), ForeArm=(0.25, 0, 0.82),
            Hand=(0.42, 0, 0.82), HandTip=(0.495, 0, 0.82),
            UpLeg=(0.09, 0, 0.485), Leg=(0.09, 0, 0.27), Foot=(0.09, -0.01, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 전설적인_이재윤(하타케 카카시) — 사무라이 소드와 같은 Sketchfab 다중 재질 glb(재질 13개
    # 전부 이미 TEX_IMAGE 연결됨). 삼각형 58,616 → 감량. 팔 벌린 A자(실측: 50% 높이대 폭 0.3676,
    # 양쪽 합 0.735*H=2.62 — PM 실측 2.63과 일치) → straighten_arms로 T자.
    "전설적인_이재윤": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_이재윤.glb",
        mesh_name="Kakashi",
        path="Assets/Art/Units/전설적인_이재윤/전설적인_이재윤.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        decimate_ratio=0.6,
        straighten_arms=True,
        joints=dict(
            Hips=(0, 0, 0.43), Spine=(0, 0, 0.60), Chest=(0, 0, 0.82),
            Neck=(0, 0, 0.88), Head=(0, 0, 0.92), HeadTop=(0, 0, 1.0),
            Shoulder=(0.09, 0, 0.84), Arm=(0.14, 0, 0.80), ForeArm=(0.26, 0, 0.66),
            Hand=(0.34, 0, 0.54), HandTip=(0.368, 0, 0.49),
            UpLeg=(0.10, 0, 0.43), Leg=(0.10, 0, 0.22), Foot=(0.10, -0.02, 0.045),
            ToeBase=(0.10, -0.14, 0.02), ToeTip=(0.10, -0.20, 0.015),
        ),
    ),
    # 전설적인_양문호(봉쿠레) — JoJo 립과 같은 이중 zip(안쪽 zip 이름은 다르지만 구조 동일).
    # 아주 저폴리(정점 512·면 738) · 이미 T자·좌우 대칭 100% · 원시 단위 cm급(키 20.92) → 1.8m.
    # 🔴 PM 경고대로 MTL에 Ke 0.588(발광)·Kd 0.588(회색 틴트) — 이미 Base Color에 텍스처가
    # 연결돼 있어(already_linked 분기) 텐트·발광 없이 텍스처만 그대로 쓴다.
    # 🔴 알파 채널(map_d, 같은 PNG) 9%가 0인데 실제 UV가 걸리는 면은 738개 중 6개뿐(허리~엉덩이
    # 높이, 케이프 자락의 작은 장식 조각으로 추정) — 그 자리 색을 확인해 보니 케이프의 짙은
    # 남색(RGB 0.11~0.17,0.13~0.21,0.35~0.48)이라 검정/투명색이 아니다. PM 지시대로 알파를
    # 무시하고(항상 하던 대로 Alpha=1 강제) RGB만 쓴다 — 케이프 색과 자연스럽게 이어질 것으로 예상.
    "전설적인_양문호": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_양문호.zip",
        mesh_name="BonClay",
        path="Assets/Art/Units/전설적인_양문호/전설적인_양문호.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.44), Spine=(0, 0, 0.60), Chest=(0, 0, 0.78),
            Neck=(0, 0, 0.85), Head=(0, 0, 0.90), HeadTop=(0, 0, 1.0),
            Shoulder=(0.06, 0, 0.85), Arm=(0.12, 0, 0.83), ForeArm=(0.30, 0, 0.70),
            Hand=(0.42, 0, 0.58), HandTip=(0.48, 0, 0.56),
            UpLeg=(0.10, 0, 0.44), Leg=(0.10, 0, 0.22), Foot=(0.10, -0.02, 0.045),
            ToeBase=(0.10, -0.14, 0.02), ToeTip=(0.10, -0.20, 0.015),
        ),
    ),
    # 전설적인_박민수(체인소맨 악마 모습) — Prisma3D 내보내기 OBJ, 이중 zip인데 텍스처가 inner가
    # 아니라 outer/textures/에 따로 있고 MTL 자체가 없다(extract_source 일반화로 처리). 🔴 정점
    # 89,325 = 면 29,775×3(삼각형마다 정점이 따로 떨어진 "정점 수프") — 이미 있는 복제 정점
    # 병합이 그대로 이어붙인다. 🔴 축 함정: raw v.co만 보면 Y가 키처럼 보이지만(Y폭 1.7726 = PM
    # 실측 키 1.773과 일치) 이건 착각이었다 — wm.obj_import는 축 변환을 메시가 아니라 오브젝트의
    # matrix_world에 얹어 둬서(이 파일은 world_z=local_y 회전) 이 파이프라인이 늘 쓰는
    # body.matrix_world @ v.co 기준으로는 이미 올바른 Z-up이다(관절 좌표는 matrix_world를 곱해
    # 다시 잰 값 — raw co 기준 첫 실측과 우연히 값이 같다, 같은 회전을 두 갈래로 표현했을 뿐).
    # 팔 폭이 키의 1.22배(2.164)로 넓은 건 팔에서 뻗은 전기톱 날 때문(원작 신체 일부, 무기 규칙
    # 예외로 유지) — HandTip을 실측한 날 끝(74~84% 높이대에서 폭이 0.59~0.61로 갑자기 튐)까지
    # 늘려 날 전체가 손 뼈 가중치를 받게 했다.
    "전설적인_박민수": dict(
        source="~/Desktop/구랜디스킨모음/06_전설적인/전설적인_박민수.zip",
        mesh_name="ChainsawMan",
        path="Assets/Art/Units/전설적인_박민수/전설적인_박민수.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.36), Spine=(0, 0, 0.55), Chest=(0, 0, 0.78),
            Neck=(0, 0, 0.87), Head=(0, 0, 0.93), HeadTop=(0, 0, 1.0),
            Shoulder=(0.04, 0, 0.80), Arm=(0.07, 0, 0.79), ForeArm=(0.15, 0, 0.775),
            Hand=(0.35, 0, 0.77), HandTip=(0.60, 0, 0.77),
            UpLeg=(0.09, 0, 0.36), Leg=(0.09, 0, 0.18), Foot=(0.09, -0.02, 0.03),
            ToeBase=(0.09, -0.14, 0.015), ToeTip=(0.09, -0.20, 0.01),
        ),
    ),
    # 제한_강보명(암부 카카시) — 위 게임(Clash of Ninja Revolution 3) 립, 이중 zip. 저폴리
    # (정점 2,627·면 4,642, 눈 조각 2개 26정점씩 별도) · 이미 T자(팔 78~82% 높이대에 넓게 퍼짐,
    # 실측 핸드팁 x비 0.4712) · 좌우 대칭 91%(등 칼집 탓 — 정상). 감량 금지(PM 지시).
    # MTL Kd 0.8 틴트·map_Ks 있지만 이미 Base Color에 텍스처 연결됨(already_linked) — 틴트 없이
    # 텍스처만 그대로.
    "제한_강보명": dict(
        source="~/Desktop/구랜디스킨모음/07_제한됨/제한_강보명.zip",
        mesh_name="AnbuKakashi",
        path="Assets/Art/Units/제한_강보명/제한_강보명.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.45), Spine=(0, 0, 0.60), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.87), Head=(0, 0, 0.92), HeadTop=(0, 0, 1.0),
            Shoulder=(0.055, 0, 0.80), Arm=(0.10, 0, 0.80), ForeArm=(0.25, 0, 0.80),
            Hand=(0.40, 0, 0.80), HandTip=(0.47, 0, 0.80),
            UpLeg=(0.09, 0, 0.45), Leg=(0.09, 0, 0.23), Foot=(0.09, -0.01, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 초월_박민석_ADAP(브룩) — 원피스 해적무쌍 계열 뼈 없는 정적 glb(MI_P009). PM 사양 원문
    # 보존(2026-09-18): 원시 키 2.9922 · 재질 6(Body·Face·Hair·Met·Parts·Wear) 전부 이미
    # TEX_IMAGE 연결됨. 메시 6개 다 main(Wear, z 0.001~2.464로 최대 z폭)과 정점 비율 20%
    # 이상이라 거리 판정 없이 자동 합침(가르는 소품 없음).
    # 🔴 실측(직접 확인) — Object_2(Body)만 따로 스캔해 팔·척추 윤곽을 골랐다: Wear(하체 로브)는
    # z 0~46% 폭이 0.35~0.38로 고정돼 있어 가랑이 위치가 실루엣으로 안 드러남(로브가 다리를
    # 통째로 가림) → 힙은 표준 비율(40%)로 둠. Body는 손끝(48.7% 높이, x폭 2.261 — PM 실측
    # "가로 2.26" 해골 손가락뼈와 일치)에서 어깨(약 65% 높이, 갈비뼈가 살짝 벌어지는 자리)까지
    # 좁아지다 76% 높이 위로는 순수 척추(폭 0.05 이하)만 남음 — 갈비뼈 폭이 어깨보다 넓게
    # 나온 자리(68~75%)는 갈비뼈 자체 폭이라 어깨 관절은 그 시작점(65%)으로 잡음. 어깨(65%,
    # x 0.08)→손끝(48.7%, x 0.378) 사이 수직 낙차 0.487m ÷ 수평 폭 0.892m = 약 28.6° 아래
    # 처짐 — PM 사전조사 "팔 약 30° 아래"와 일치(straighten_arms로 수평 T자로 폄).
    # 머리 부위는 Face(80.5~90.2%)·Hair 아프로(78.9~92.8%)·Met 왕관모자(86.8~100%)가 각각
    # 겹쳐 있어 Head 관절은 그 중간(86%), HeadTop은 실측 최고점(100%, 왕관 끝).
    # 🔴 손가락뼈처럼 가늘어 bone heat 실패 위험 큼(PM 경고) — force_geodesic 강제는 안 하고
    # 기본대로 시도 후 실패 뼈 있으면 자동 지오데식 대체(에이스와 같은 방식).
    "초월_박민석_ADAP": dict(
        source="~/Desktop/구랜디스킨모음/08_초월/초월_박민석_ADAP.glb",
        mesh_name="Brook",
        path="Assets/Art/Units/초월_박민석_ADAP/초월_박민석_ADAP.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        decimate_ratio=0.5,
        straighten_arms=True,
        joints=dict(
            Hips=(0, 0, 0.40), Spine=(0, 0, 0.52), Chest=(0, 0, 0.65),
            Neck=(0, 0, 0.78), Head=(0, 0, 0.86), HeadTop=(0, 0, 1.0),
            Shoulder=(0.08, 0, 0.65), Arm=(0.20, 0, 0.60), ForeArm=(0.28, 0, 0.56),
            Hand=(0.345, 0, 0.52), HandTip=(0.378, 0, 0.487),
            UpLeg=(0.09, 0, 0.40), Leg=(0.09, 0, 0.20), Foot=(0.09, -0.02, 0.03),
            ToeBase=(0.09, -0.14, 0.015), ToeTip=(0.09, -0.20, 0.01),
        ),
    ),
    # 초월_박민수_AD(후시구로 토지, 포트나이트 콜라보 립 T_ApplePound_*) — PM 사양 원문 보존
    # (2026-09-18). ※ 희귀함_이재윤(토지, 중국 모바일 립)과 다른 원본.
    # 원본: source/Toji.fbx + textures/T_ApplePound_{Head,Body,FaceAcc}_CL/CS.png. 뼈 없음.
    # 메시 3(Body 19,458·Hair 8,790·Head 6,842) 전부 무가중치.
    # 🔴 재질·메시 이름 뒤바뀜 확인 — 메시 오브젝트 "Hair"의 재질 이름이 "Head"(텍스처
    # T_ApplePound_Head_CL.png)이고, 메시 오브젝트 "Head"의 재질 이름이 "Hair"(텍스처
    # T_ApplePound_FaceAcc_CS.png). 재질 자체는 각자 다른 텍스처에 이미 제대로 연결돼 있어
    # (already_linked 3/3) 재질 이름으로만 처리하면 원래 연결 그대로 유지되니 문제없음
    # (오브젝트 이름과 재질 이름의 대응 혼동일 뿐, 텍스처 배선 자체는 안 꼬임 — 렌더로 최종
    # 확인).
    # 관절 실측(Body만 스캔) — 다리·허리대는 폭 0.37~0.45(양다리 합) 완만히 좁아져 뚜렷한
    # 가랑이 경계가 안 보임(바지 실루엣) → 골반 표준 근사(48%). 팔은 50~52% 높이에서 폭이
    # 1.18~1.20(PM 실측 "가로 1.20"과 일치)로 급격히 넓어졌다가 80% 높이 위로는 순수 목
    # 폭(0.33)까지 좁아짐 — 어깨(80%)에서 늘어져 손(56%, z≈0.95 raw — PM 실측 "손 허리높이
    # 0.95"와 일치)까지 처지는 A자. Body 메시 천장(85%)이 목 끝, Head·Hair 메시가 그 위(81~
    # 100%)를 덮어 머리 관절은 그 중간(91%)·HeadTop은 실측 최고점(100%).
    # 🔴 정정(2026-09-18, 유니티 반려) — Idle에서 스웨터 소매·어깨 천이 T자 위치에 얼어붙어
    # 날개처럼 남음. 원인: 뼈 중심축이 옷 속 빈 공간이라 그 최근접 정점이 부푼 소매 표면이
    # 아니라 어깨 솔기 너머 Chest 쪽 정점을 집었다(표면 경로가 더 가까움). cloth_radius로
    # Shoulder·Arm·ForeArm 뼈에 부푼 소매 반지름만큼 추가 씨앗을 심어 소매 표면에도 팔 뼈가
    # 직접 닿게 함(geodesic_bone_weights() 자체를 이 캐릭터로 일반화, cloth_radius 없는 다른
    # 캐릭터는 기존과 동일).
    "초월_박민수_AD": dict(
        rebuild="금지",   # ← 위 「옛 시도 다섯」 참고. 커밋본은 gen_scan_rig.py가 만든다.
        source="~/Desktop/구랜디스킨모음/08_초월/초월_박민수_AD.zip",
        mesh_name="Toji",
        path="Assets/Art/Units/초월_박민수_AD/초월_박민수_AD.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        straighten_arms=True,
        cloth_reclaim=True,
        joints=dict(
            Hips=(0, 0, 0.48), Spine=(0, 0, 0.60), Chest=(0, 0, 0.78),
            Neck=(0, 0, 0.85), Head=(0, 0, 0.91), HeadTop=(0, 0, 1.0),
            Shoulder=(0.16, 0, 0.80), Arm=(0.26, 0, 0.70), ForeArm=(0.30, 0, 0.60),
            Hand=(0.35, 0, 0.56), HandTip=(0.35, 0, 0.52),
            UpLeg=(0.09, 0, 0.48), Leg=(0.09, 0, 0.27), Foot=(0.09, -0.02, 0.03),
            ToeBase=(0.09, -0.14, 0.015), ToeTip=(0.09, -0.20, 0.01),
        ),
    ),
    # 초월_엄태웅_AD(니지무라 오쿠야스) — 조셉·죠스케(희귀함)와 같은 JoJo Diamond Records
    # 모바일 OBJ 립(이중 zip). PM 사양 원문 보존(2026-09-18): 정점 2,205·면 2,542·재질 1·
    # 키 1.794(원시 실측 1.7939, 일치)·이미 T자(팔 82~84% 높이대에서 수평, 실측 핸드팁
    # x반폭 0.869)·좌우 대칭 96.8%. MTL Kd 0.64 틴트 무시, 텍스처 직결(already_linked).
    # 저폴리라 감량 금지(decimate_ratio 기본 1.0 유지).
    "초월_엄태웅_AD": dict(
        rebuild="금지",   # ← 위 「옛 시도 다섯」 참고. 커밋본은 gen_scan_rig.py가 만든다.
        source="~/Desktop/구랜디스킨모음/08_초월/초월_엄태웅_AD.zip",
        mesh_name="Okuyasu",
        path="Assets/Art/Units/초월_엄태웅_AD/초월_엄태웅_AD.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        joints=dict(
            Hips=(0, 0, 0.46), Spine=(0, 0, 0.61), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.88), Head=(0, 0, 0.94), HeadTop=(0, 0, 1.0),
            Shoulder=(0.10, 0, 0.83), Arm=(0.30, 0, 0.83), ForeArm=(0.55, 0, 0.83),
            Hand=(0.75, 0, 0.83), HandTip=(0.869, 0, 0.83),
            UpLeg=(0.09, 0, 0.46), Leg=(0.09, 0, 0.24), Foot=(0.09, -0.01, 0.045),
            ToeBase=(0.09, -0.14, 0.02), ToeTip=(0.09, -0.20, 0.015),
        ),
    ),
    # 랜덤_이민형(요시카게 키라, 죠죠 4부) — 2026-09-23 blender. 조셉·죠스케·오쿠야스와 같은 JoJo Diamond Records 모바일 OBJ 립(이중 zip).
    #   source/Kira.zip 안 Kira.obj — 정점 2,428 · 삼각형 3,037 · 오브젝트와 재질 둘 다 "Mesh_5299.rip" · 텍스처 Kira.png 한 장. 저폴리라 감량 금지.
    #   원본 키 1.789 · **이미 T자**(팔 중심선이 키의 80% 높이에서 수평, 손끝 x 반폭 0.467) · 좌우 대칭 · 정면 −Y(raw_negY.png로 확인) → straighten 불필요.
    #   🔴 손에 든 소품 없음(PM이 확인하라고 한 것 — 정면·측면·후면 렌더로 확인. 빈손이고 잘린 손 같은 부속도 없다).
    #   관절은 정규화 단면 실측(높이 1 기준): 가랑이 z≈0.42(z 0.35에서 다리 둘로 갈리고 0.45에서 합쳐짐) · 다리 중심 x 0.055 ·
    #   몸통 반폭 z 0.65에서 0.087 · 0.80에서 0.129 · 머리만 남는 높이 z 0.90(반폭 0.046).
    #   ⚠️ 오쿠야스 항목의 손끝 x 0.869는 메시 반폭(1.749/2=0.87m)보다 크게 잡혀 뼈가 메시 밖으로 나가 있다 — 이 항목은 실측 반폭 0.467을 그대로 쓴다.
    "랜덤_이민형": dict(
        source="~/Desktop/구랜디스킨모음/11_랜덤유닛/랜덤_이민형.zip",
        mesh_name="Kira",
        path="Assets/Art/Units/랜덤_이민형/랜덤_이민형.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.80,
        joints=dict(
            Hips=(0, 0, 0.50), Spine=(0, 0, 0.62), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.875), Head=(0, 0, 0.905), HeadTop=(0, 0, 1.0),
            Shoulder=(0.05, 0, 0.80), Arm=(0.10, 0, 0.80), ForeArm=(0.24, 0, 0.80),
            Hand=(0.39, 0, 0.80), HandTip=(0.46, 0, 0.80),
            UpLeg=(0.055, 0, 0.47), Leg=(0.055, 0, 0.26), Foot=(0.055, -0.01, 0.04),
            ToeBase=(0.055, -0.05, 0.018), ToeTip=(0.055, -0.075, 0.014),
        ),
    ),
    # 랜덤_주호페이크(쿠죠 죠타로 4부, 부상 복장) — 2026-09-23 blender. 이민형(키라)과 완전히 같은 계열·같은 이중 zip.
    #   ⚠️ 안쪽 zip 이름에 공백과 작은따옴표가 있다("Mobile - JoJo's Bizarre Adventure_ Diamond Records Reversal - Pa.zip") — PM은 bsdtar가 실패한다고 했지만
    #   이 파이프라인은 파이썬 zipfile로 풀므로 쉘을 안 거쳐 그대로 된다(직접 확인). unzip 폴백 불필요.
    #   4taroInjured.obj — 정점 2,962 · 삼각형 3,653 · 오브젝트/재질 둘 다 "Mesh_0022.rip" · 텍스처 Holetaro.png 한 장 · 원본 키 1.966.
    #   **이미 T자**(팔 중심선 키의 80% 높이, 손끝 x 반폭 0.491) · 정면 −Y. 피 얼룩·찢어진 옷은 텍스처와 메시에 그대로 있다(PM 지시대로 전부 유지).
    #   모자챙·머리카락은 따로 된 메시가 아니라 한 덩어리라 강체로 떼어 줄 수 없다 — 머리 둘레에 있어 지오데식이 Head로 준다(렌더로 확인).
    #   긴 코트가 아래로 넓게 퍼진다(z 0.15에서 x ±0.25까지) — 장하민(리조토) 코트와 같은 상황이라 같은 방식으로 둔다.
    #   가랑이가 높다(z≈0.52에서 다리가 갈린다 — 롱코트와 긴 다리 비율).
    "랜덤_주호페이크": dict(
        source="~/Desktop/구랜디스킨모음/11_랜덤유닛/랜덤_주호페이크.zip",
        mesh_name="Jotaro",
        path="Assets/Art/Units/랜덤_주호페이크/랜덤_주호페이크.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.80,
        joints=dict(
            Hips=(0, 0, 0.53), Spine=(0, 0, 0.64), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.875), Head=(0, 0, 0.905), HeadTop=(0, 0, 1.0),
            Shoulder=(0.05, 0, 0.80), Arm=(0.105, 0, 0.80), ForeArm=(0.25, 0, 0.80),
            Hand=(0.42, 0, 0.80), HandTip=(0.49, 0, 0.80),
            UpLeg=(0.053, 0, 0.50), Leg=(0.053, 0, 0.27), Foot=(0.053, -0.01, 0.04),
            ToeBase=(0.053, -0.05, 0.018), ToeTip=(0.053, -0.075, 0.014),
        ),
    ),
    # 랜덤_김건모(디아볼로, 죠죠 5부) — 2026-09-23 blender. 주호페이크와 **완전히 같은 구조**(같은 이름의 안쪽 zip).
    #   Diavolo.obj — 정점 3,390 · 삼각형 4,523 · 오브젝트/재질 둘 다 "Mesh_0066.rip" · 텍스처 Diavolo.png 한 장 · 원본 키 1.957.
    #   **이미 T자**(팔 80% 높이, 손끝 x 반폭 0.496) · 정면 −Y. 무기 없음.
    #   ⚠️ PM 사전조사엔 「점무늬 정장에 긴 머리」로 적혀 있었는데, 실제 모델은 **그물 상의에 맨 상반신 + 점무늬 보라 바지**다(원작 디아볼로 본체 차림이 맞다).
    #   긴 분홍 머리가 등 뒤로 z 0.35까지 내려온다(y 0.213) — 따로 된 메시가 아니라 한 덩어리라 강체로 못 떼고 지오데식에 맡긴다.
    #   가랑이 z≈0.43(z 0.40에서 다리가 갈리고 0.45에서 합쳐짐) · 다리 중심 x 0.052.
    "랜덤_김건모": dict(
        source="~/Desktop/구랜디스킨모음/11_랜덤유닛/랜덤_김건모.zip",
        mesh_name="Diavolo",
        path="Assets/Art/Units/랜덤_김건모/랜덤_김건모.fbx",
        height=1.8,
        center_band=(0.02, 0.06),
        chest_ratio=0.80,
        joints=dict(
            Hips=(0, 0, 0.49), Spine=(0, 0, 0.62), Chest=(0, 0, 0.80),
            Neck=(0, 0, 0.86), Head=(0, 0, 0.90), HeadTop=(0, 0, 1.0),
            Shoulder=(0.05, 0, 0.80), Arm=(0.105, 0, 0.80), ForeArm=(0.25, 0, 0.80),
            Hand=(0.42, 0, 0.80), HandTip=(0.49, 0, 0.80),
            UpLeg=(0.052, 0, 0.47), Leg=(0.052, 0, 0.26), Foot=(0.052, -0.01, 0.04),
            ToeBase=(0.052, -0.05, 0.018), ToeTip=(0.052, -0.075, 0.014),
        ),
    ),
}

SPINE = [("Hips", "Hips", "Spine", None), ("Spine", "Spine", "Chest", "Hips"),
         ("Chest", "Chest", "Neck", "Spine"), ("Neck", "Neck", "Head", "Chest"),
         ("Head", "Head", "HeadTop", "Neck")]
LIMB = [("Shoulder", "Shoulder", "Arm", "Chest"), ("Arm", "Arm", "ForeArm", "Shoulder"),
        ("ForeArm", "ForeArm", "Hand", "Arm"), ("Hand", "Hand", "HandTip", "ForeArm"),
        ("UpLeg", "UpLeg", "Leg", "Hips"), ("Leg", "Leg", "Foot", "UpLeg"),
        ("Foot", "Foot", "ToeBase", "Leg"), ("ToeBase", "ToeBase", "ToeTip", "Foot")]


def bone_table(joints):
    out = []
    for name, head, tail, parent in SPINE:
        out.append((name, joints[head], joints[tail], parent))
    for side, sx in (("Left", 1.0), ("Right", -1.0)):
        for name, head, tail, parent in LIMB:
            h, t = joints[head], joints[tail]
            par = parent if parent in ("Chest", "Hips") else side + parent
            out.append((side + name, (h[0] * sx, h[1], h[2]), (t[0] * sx, t[1], t[2]), par))
    return out


def extract_source(src):
    """압축을 풀어 (mesh_path, mtl_path, texture_path, outer_dir, inner_dir, all_images)를
    돌려준다. mesh_path는 .obj 또는 .fbx(희귀함_유재헌처럼 하위 폴더에 fbx+png가 같이 든 립도
    있다 — os.walk로 어디 있든 찾는다). all_images는 찾은 모든 이미지의 {파일명: 경로}(재질이
    여럿이라 텍스처도 여러 장인 소스 — cfg["texture_files"]로 재질별 파일명을 골라 쓴다).
    🔴 PM 실측(희귀함_선효진/보디빌더) — 이 캐릭터부턴 zip이 아니라 맨 .glb 파일 하나(Sketchfab
    다운로드, 텍스처가 파일로 안 풀려 있고 glb 안에 이미지로 박혀 있음)라 압축 해제 자체가 필요
    없다. .glb/.gltf면 그대로 mesh_path로 돌려주고 mtl·png는 None(재질은 build()에서 이미 연결된
    노드 그래프의 이미지를 직접 저장해서 처리— glTF 임포터가 재질을 이미 다 만들어 준다).
    🔴 PM 실측(전설적인_엄태웅) — JoJo 립처럼 이중 zip(outer/source/*.zip)이 아니라 outer zip
    바로 밑에 source/*.fbx + textures/*.png(여러 장, 재질 5개짜리)가 있는 단일 zip 구조도 있다.
    source/*.zip이 없으면 안쪽 압축 해제를 건너뛰고 outer 자체를 그대로 뒤진다(inner=None)."""
    if src.lower().endswith((".glb", ".gltf")):
        return src, None, None, None, None, {}
    outer = tempfile.mkdtemp(prefix="objrip_outer_")
    with zipfile.ZipFile(src) as z:
        z.extractall(outer)
    inner_zip = None
    for root, _dirs, files in os.walk(os.path.join(outer, "source")):
        for f in files:
            if f.lower().endswith(".zip"):
                inner_zip = os.path.join(root, f)
    if inner_zip:
        inner = tempfile.mkdtemp(prefix="objrip_inner_")
        with zipfile.ZipFile(inner_zip) as z:
            z.extractall(inner)
        search_root = inner
    else:
        inner = None
        search_root = outer
    mesh = mtl = png = None
    all_images = {}
    for root, _dirs, files in os.walk(search_root):
        for f in files:
            low = f.lower()
            p = os.path.join(root, f)
            if low.endswith((".obj", ".fbx")):
                mesh = p
            elif low.endswith(".mtl"):
                mtl = p
            elif low.endswith((".png", ".jpg", ".jpeg")):
                png = p
                all_images[f] = p
    # 🔴 PM 실측(전설적인_박민수/체인소맨) — 이중 zip인데 텍스처가 inner(mesh와 같은 zip) 안이
    # 아니라 outer/textures/에 따로 있고 MTL 자체가 없는 조합도 있다(Prisma3D 내보내기, mtllib이
    # 가리키는 .mtl 파일이 zip에 없음). inner에서 이미지를 못 찾았으면 outer 전체를 마저 뒤진다.
    if not png and search_root != outer:
        for root, _dirs, files in os.walk(outer):
            for f in files:
                if f.lower().endswith((".png", ".jpg", ".jpeg")):
                    p = os.path.join(root, f)
                    png = png or p
                    all_images[f] = p
    assert mesh and png, f"메시/텍스처를 못 찾음: {search_root}"
    return mesh, mtl, png, outer, inner, all_images


def check_uv0(mesh):
    """루프 UV가 전부 [0,1] 안인지 — 재질 하나·아틀라스 하나뿐이라 벗어나면 텍스처가 엉뚱하게 씌워진다."""
    uv = mesh.uv_layers.active
    if uv is None:
        return {"UV 없음": True}
    us = np.array([d.uv[0] for d in uv.data])
    vs = np.array([d.uv[1] for d in uv.data])
    outside = int(np.sum((us < -1e-4) | (us > 1 + 1e-4) | (vs < -1e-4) | (vs > 1 + 1e-4)))
    return {"범위": [round(float(us.min()), 4), round(float(us.max()), 4), round(float(vs.min()), 4), round(float(vs.max()), 4)],
            "밖 정점(루프)": outside}


def geodesic_bone_weights(mesh, table, data, k=3, smooth_iters=3, cloth_radius=None):
    """gen_scan_rig.py에서 검증한 지오데식 최근접 배정 — bone heat가 실패한 뼈가 있을 때만 쓴다
    (표면을 따라가는 다익스트라로 뼈를 배정한 뒤 이웃과 섞어 부드럽게, 직선거리와 달리 접힌
    부위가 먼 표면의 가중치를 훔치지 않는다).
    🔴 PM 실측(초월_박민수_AD/토지, 유니티 반려) — 오버사이즈 스웨터처럼 옷이 팔 중심축에서
    멀리 부풀면, 뼈 중심축 위 점의 최근접 정점이 부푼 소매 표면이 아니라 몸통 쪽 정점을
    집어(중심축 자체가 옷 속 빈 공간이라 표면에서 멀다), 소매 대부분이 다익스트라 경쟁에서
    Chest/Spine 씨앗에 넘어갔다(어깨 솔기를 통한 표면 경로가 더 가까워서). cfg["cloth_radius"]
    = {"ForeArm": 0.15, "Arm": 0.15, ...}(뼈 이름 접미사→반지름 m)를 주면 중심축 점 외에도
    그 반지름만큼 bone.x_axis/z_axis 방향으로 띄운 점들을 추가 씨앗으로 심어, 부푼 소매
    표면에도 팔 뼈 씨앗이 직접 닿게 한다(중심축 씨앗은 그대로 유지 — 몸에 붙는 옷은 결과가
    바뀌지 않는다, 뼈 이름이 cloth_radius에 없으면 기존과 완전히 동일)."""
    import heapq
    names = [PREFIX + bname for bname, *_ in table]
    cloth_radius = cloth_radius or {}
    V = np.array([v.co for v in mesh.vertices])
    n = len(V)
    adj = [[] for _ in range(n)]
    for e in mesh.edges:
        a, b = e.vertices
        d = float(np.linalg.norm(V[a] - V[b]))
        adj[a].append((b, d))
        adj[b].append((a, d))
    kd = KDTree(n)
    for i, co in enumerate(V):
        kd.insert(co, i)
    kd.balance()
    heap = []
    for bi, name in enumerate(names):
        b = data.bones[name]
        head, tail = np.array(b.head_local), np.array(b.tail_local)
        bare = name[len(PREFIX):]
        for side in ("Left", "Right"):
            if bare.startswith(side):
                bare = bare[len(side):]
        radius = cloth_radius.get(bare)
        for tparam in np.linspace(0.0, 1.0, 5):
            center = Vector(head + (tail - head) * tparam)
            _, vi, _ = kd.find(center)
            heap.append((0.0, vi, bi))
            if radius:
                for ax in (b.x_axis, b.z_axis):
                    for sign in (1.0, -1.0):
                        _, vi2, _ = kd.find(center + Vector(ax) * (radius * sign))
                        heap.append((0.0, vi2, bi))
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
    # 🔴 립 모델은 소품(장갑·부츠·머리 장식 등)이 몸통과 변으로 안 이어진 별도 섬인 경우가 있다
    # (실측: 조셉에서 다익스트라가 못 닿은 정점 존재) — 그런 섬은 표면을 못 타므로, 이미 배정된
    # 정점 중 3D 위치가 가장 가까운 쪽의 배정을 그대로 따라간다(gen_skin_rig.py의 「가장 가까운
    # 가중치 정점」 대체와 같은 발상).
    reached = np.where(owner >= 0)[0]
    if len(reached) < n:
        kd_r = KDTree(len(reached))
        for i, vi in enumerate(reached):
            kd_r.insert(V[vi], i)
        kd_r.balance()
        for vi in np.where(owner < 0)[0]:
            _, ri, _ = kd_r.find(V[vi])
            owner[vi] = owner[reached[ri]]
    assert (owner >= 0).all(), "지오데식 배정에서 못 닿은 정점이 있다"
    W = np.zeros((n, len(names)))
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


def model_lcl_scaling(path):
    """FBX를 원시로 파싱해 모든 Model 노드의 Lcl Scaling을 읽는다(gen_prop_unit.py의
    model_lcl_rotation()과 같은 방식 — 겉싸개 배율이 조용히 남았는지 확인)."""
    import io_scene_fbx.parse_fbx as pf
    root, _version = pf.parse(path)

    def find(elem, elem_id):
        return [e for e in elem.elems if e.id == elem_id]

    objects = find(root, b"Objects")[0]
    out = []
    for model in find(objects, b"Model"):
        scale = (1.0, 1.0, 1.0)
        for p70 in find(model, b"Properties70"):
            for prop in find(p70, b"P"):
                if prop.props[0] == b"Lcl Scaling":
                    scale = tuple(prop.props[4:])
        out.append(scale)
    return out


def straighten_arms(arm, body):
    """A자로 처진 팔(희귀함_유재헌 등, cfg["straighten_arms"]=True)을 좌우 수평(±X)으로 펴고
    그 자세를 쉬는 자세로 굽는다 — gen_mmd_skin.py의 straighten_arms()와 같은 기법(뼈 하나씩
    현재→목표 방향 쿼터니언 회전 후 포즈를 레스트로 적용). 다리는 이미 서 있는 자세라 안 건드림."""
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
    # 🔴 PM 실측(유니티, 전설적인_박병규/디오, gen_mmd_skin.py에서 먼저 찾음) — 조합판 Idle(팔
    # 내림)에서 위팔이 비틀린 수건처럼 가늘어지고 팔꿈치가 옆으로 꺾여 튀어나왔다. 원인:
    # d.rotation_difference(target)는 방향(swing)만 목표에 맞출 뿐 그 축을 중심으로 한 회전
    # (roll·비틀림)은 원본 자세 그대로 뼈마다 제각각 남는다 — T자 렌더는 멀쩡해 보이는데(바인드=
    # 레스트라 roll이 뭐든 변형이 항등이라 안 보인다) 유니티가 팔을 실제로 움직이면 그 제각각의
    # roll이 팔꿈치 굽힘 축을 엉뚱한 방향으로 돌려 놓는다. armature_apply로 새 레스트를 구운
    # 지금(바인드=레스트라 roll을 바꿔도 메시는 안 움직인다) bone_table()과 같은 규칙으로 roll을
    # 다시 맞춘다(이 스크립트로 straighten_arms=True를 쓴 모든 캐릭터에 재적용 필요 — 유재헌·에이스·
    # 카카시).
    bpy.ops.object.mode_set(mode="EDIT")
    for side in ("Left", "Right"):
        for bone in ("Shoulder", "Arm", "ForeArm", "Hand"):
            eb = arm.data.edit_bones[PREFIX + side + bone]
            d = (eb.tail - eb.head).normalized()
            eb.align_roll(Vector((0, 0, 1)) if abs(d.y) > 0.7 else Vector((0, -1, 0)))
    bpy.ops.object.mode_set(mode="OBJECT")
    return turned


def _copy_unless_same(src, dst):
    """🔴 이미 그 자리에 있는 파일을 자기 자신에 복사하면 `shutil.SameFileError`로 **항목 전체가 죽는다**
    (2026-09-24 R01 박진웅: 재질 19개가 텍스처 10장을 나눠 쓰는데, 앞 재질이 tex_dir에 넣어 둔 것을
    뒤 재질이 원본으로 잡아 src == dst가 됐다. 텍스처만 남고 FBX가 안 나왔다).
    ⚠️ 복사하는 자리가 **세 군데**다 — 한 군데만 막으면 다음 재질에서 또 죽는다. 그래서 여기로 모았다."""
    if os.path.exists(dst) and os.path.samefile(src, dst):
        return
    shutil.copy2(src, dst)


def build(name, cfg, out_dir=None, render_dir=None):
    src = os.path.expanduser(cfg["source"])
    dst = os.path.join(out_dir, os.path.basename(cfg["path"])) if out_dir else os.path.join(ROOT, cfg["path"])
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": name, "원본": src, "sha256": hashlib.sha256(open(src, "rb").read()).hexdigest()}

    mesh_path, mtl_path, png_path, outer_dir, inner_dir, all_images = extract_source(src)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if mesh_path.lower().endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=mesh_path)
    elif mesh_path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=mesh_path)
    else:
        bpy.ops.wm.obj_import(filepath=mesh_path)
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    # 🔴 PM 실측(전설적인_박민수/체인소맨) — 이 OBJ는 raw v.co만 보면 Y가 키처럼 보이지만(Y폭
    # 1.7726), 이건 착각이다 — wm.obj_import는 축 변환을 메시 데이터가 아니라 오브젝트의
    # matrix_world에 얹어 둔다(이 파일은 (0,0,-1)/(0,1,0) 회전 — 즉 world_z=local_y로 이미
    # 올바르게 Z-up 변환됨). 이 파이프라인은 항상 body.matrix_world @ v.co로 재는데, 한때 raw
    # v.co만 보고 "Y가 키"라고 오판해 추가로 축을 또 돌렸다가(중복 회전) 오히려 깨졌다 — 손대지
    # 않는 게 정답이었다. 다른 축 특이 케이스가 또 나오면 반드시 matrix_world를 곱해서 재확인할 것.
    report["원본 오브젝트"] = [(o.name, len(o.data.vertices)) for o in meshes]
    # 🔴 PM 실측(유니티, 희귀함_강보명) — OBJ의 "o" 그룹이 둘 이상이면 wm.obj_import가 블렌더
    # 오브젝트도 둘 이상 만든다. 그중 첫 번째만 처리하고 나머지를 씬에 남겨 두면(고아 오브젝트)
    # 내보내기가 그것까지 같이 담아 유니티에 렌더러가 두 개로 잡히고, 몸에서 떨어진 조각(정점
    # 198개, 손목 밴드의 남은 뒷면 조각으로 확인)이 유닛 옆에 먼지처럼 붙어 다닌다.
    # 🔴 PM 실측(희귀함_최현우/스피드왜건) — 그렇다고 무조건 다 합치면 안 된다. 이 파일은 둘째
    # 조각이 왼발 옆 바닥에 떨어진 별개 소품(모자로 추정)이었는데, 무조건 join을 돌리면 몸에
    # 안 붙어야 할 게 발에 끌려 다니게 된다. 나란챠(손목 밴드, 몸 표면과 거의 겹침)와 스피드왜건
    # (바닥 소품, 몸에서 뚝 떨어짐)을 가르는 실측 기준: 조각의 각 정점에서 본체 메시의 가장
    # 가까운 정점까지 거리 — 나란챠는 중앙값 0.033(거의 붙어 있음), 스피드왜건은 0.637(몸 키의
    # 1/3 가까이 떨어짐). 본체 키의 5%를 문턱으로 삼아, 그 밑이면 겹치는 조각으로 보고 합치고
    # (join), 넘으면 떨어진 소품으로 보고 버린다(렌더로 한 번 더 눈으로 확인할 것 — PM 지시).
    # 🔴 PM 실측(희귀함_선효진/보디빌더) — 거리 중앙값 문턱만으로는 안 된다. 이 파일은 본체와
    # 거의 같은 크기(65532/65532/47686정점)인 조각 셋인데, 그중 하나(Object_5)는 몸 표면에서
    # 헐렁하게 뜬 옷(탱크톱 추정, 중앙값 0.106 = 키의 5.3%)이라 5% 문턱을 살짝 넘어 버려질
    # 뻔했다. 스피드왜건의 진짜 소품은 절대 정점 수(304개)도 본체 대비 비율(6.2%)도 작았던 것과
    # 달리, 여기 조각들은 본체 정점 수의 20% 넘게 차지하는 "큰 덩어리"라 옷/신체 일부일 수밖에
    # 없다(바닥에 뚝 떨어진 소품이 본체만 한 정점 수를 가질 이유가 없다) — 정점 수 비율이 20%
    # 이상이면 거리와 무관하게 항상 합치고, 그 밑(작은 조각)일 때만 거리 중앙값 문턱을 적용한다.
    # 🔴 PM 실측(전설적인_엄태웅) — "본체"를 정점 수로 고르면 안 된다. 이 캐릭터는 머리카락
    # (pelo_l, 6,492정점)이 실제 몸(ace_l, 3,824정점)보다 정점이 많지만 머리 부분만 덮어 키의
    # 10%도 안 되는 높이라, 머리카락을 본체로 잘못 고르면 문턱(키의 5%)이 실제 키가 아니라
    # 머리카락 높이의 5%가 돼(0.0005 — 사실상 0) 팔찌·벨트·바지 등 진짜 몸에 붙은 조각 18개가
    # 전부 "떨어진 소품"으로 잘못 버려졌다. 본체는 정점 수가 아니라 **키(z축 폭)가 가장 큰**
    # 조각으로 고른다 — 옷·장신구 조각이 아무리 정점이 빽빽해도 몸 전체 키만큼 클 수는 없다.
    def zextent(o):
        co = np.array([o.matrix_world @ v.co for v in o.data.vertices])
        return float(co[:, 2].max() - co[:, 2].min())

    SIZE_JOIN_RATIO = 0.20
    if len(meshes) > 1:
        main = max(meshes, key=zextent)
        main_pts = np.array([main.matrix_world @ v.co for v in main.data.vertices])
        main_height = float(main_pts[:, 2].max() - main_pts[:, 2].min())
        threshold = main_height * 0.05
        kd = KDTree(len(main_pts))
        for i, co in enumerate(main_pts):
            kd.insert(co, i)
        kd.balance()
        to_join, dropped = [main], []
        for o in meshes:
            if o is main:
                continue
            # 🔴 PM 실측(전설적인_엄태웅) — 23개 조각이 전부 몸에 붙은 의상·소품이라고 PM이 직접
            # 확인해 준 경우(cfg["join_all"]) 거리 판정 자체를 건너뛴다. 신발(zapatos_l)처럼
            # 발목과 살짝 이가 갈라진(문턱을 2배 넘게) 정상 이음매까지 "떨어진 소품"으로 오판하는
            # 사례가 나왔다 — 진짜 떨어진 소품이 있는 캐릭터(스피드왜건 등)에는 이 옵션을 안 쓴다.
            if cfg.get("join_all"):
                to_join.append(o)
                continue
            size_ratio = len(o.data.vertices) / len(main.data.vertices)
            if size_ratio >= SIZE_JOIN_RATIO:
                to_join.append(o)
                continue
            pts = np.array([o.matrix_world @ v.co for v in o.data.vertices])
            dists = [kd.find(p)[2] for p in pts]
            median = float(sorted(dists)[len(dists) // 2])
            if median <= threshold:
                to_join.append(o)
            else:
                dropped.append((o.name, len(o.data.vertices), round(median, 4)))
        for o in scene.objects:
            o.select_set(o in to_join)
        for o in meshes:
            if o not in to_join:
                bpy.data.meshes.remove(o.data)                # 오브젝트+메시 데이터까지 완전히 버림
        if dropped:
            report["떨어진 조각(버림 — 본체와 거리 중앙값, 문턱 " + str(round(threshold, 4)) + ")"] = dropped
        bpy.context.view_layer.objects.active = main
        if len(to_join) > 1:
            bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active if len(meshes) > 1 else meshes[0]
    body.name = body.data.name = cfg["mesh_name"]
    # 🔴 PM 실측(희귀함_선효진/보디빌더) — glb 소스가 면마다 정점을 따로 둬(하드 엣지·UV 심마다
    # 위치는 같은데 인덱스가 다른 "복제 정점") 이 메시 하나가 조인 뒤에도 위상적으로 2,123개
    # 조각으로 쪼개져 있었다(가장 큰 조각도 전체 178,750정점 중 1,271개뿐). geodesic_bone_weights()
    # 는 뼈 하나당 씨앗을 KDTree로 "가장 가까운 정점"에 얹은 뒤 메시 "간선"만 따라 다익스트라를
    # 도는데, 조각이 2천 개 넘게 쪼개져 있으면 사실상 조각 단위 유클리드 최근접 배정이나 마찬가지가
    # 돼(진짜 표면 최근접이 아니라) 겨드랑이·사타구니처럼 몸이 서로 닿을 듯 가까운 부위에서 팔
    # 뼈가 엉뚱하게 몸통 쪽 작은 조각까지 통째로 삼켜 버렸다(렌더로 확인: T자로 펼 때 사타구니
    # 천 조각이 양쪽 손을 따라 무릎까지 날개처럼 찢어져 늘어남). 위치가 같은 정점을 합치면
    # (bmesh.ops.remove_doubles, 블렌더는 UV를 정점이 아니라 loop 단위로 들고 있어 심을 안
    # 망가뜨리고 합칠 수 있다) 조각이 1개로 붙는다(정점 178,750→151,095) — 지오데식이 진짜
    # 표면을 따라가게 된다. glb 소스 전반에 재발할 수 있는 문제라 모든 소스에 공통으로 적용한다.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    n_before = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    if len(body.data.vertices) != n_before:
        report["복제 정점 병합(위상 조각 방지)"] = [n_before, len(body.data.vertices)]
    report["원본 정점·삼각형"] = [len(body.data.vertices), sum(len(p.vertices) - 2 for p in body.data.polygons)]
    report["UV0"] = check_uv0(body.data)

    os.makedirs(tex_dir, exist_ok=True)
    if len(body.data.materials) > 1:                        # join()이 같은 이름 재질을 슬롯 여러 개로 남길 수 있다
        for o in scene.objects:
            o.select_set(o == body)
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.material_slot_remove_unused()
    # 🔴 PM 실측(희귀함_박도진/사무라이 소드) — 이전까지는 항상 재질이 1개(JoJo OBJ 립·유재헌
    # FBX)였는데, 이 glb는 헤드·재킷·바지·손·신발마다 재질이 따로 10개다. materials[0]만 고쳐서는
    # 렌더에선 (블렌더가 나머지 9개의 원본 임시 이미지를 메모리에 여전히 들고 있어) 멀쩡해 보이지만,
    # glb 소스는 outer_dir/inner_dir이 없어(zip이 아니라 압축 해제 임시 폴더 자체가 없다) FBX로
    # 내보낸 뒤 그 임시 이미지가 사라지면 유니티에서 9개 재질의 텍스처가 깨진다. 재질 슬롯 전부를
    # 돌면서 각각 처리한다.
    already_linked_count = 0
    alpha_fixed = []
    for mat in body.data.materials:
        if mat is None:
            continue
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if mat.use_nodes else None
        already_linked = bsdf and bsdf.inputs["Base Color"].links and \
            bsdf.inputs["Base Color"].links[0].from_node.type == "TEX_IMAGE"
        # 🔴 희귀함_유재헌(FBX 소스) — PM 실측대로 텍스처가 이미 Base Color에 연결돼 있다. OBJ
        # 소스는(mtl만 있고 노드가 없어) 항상 새로 지어야 했지만, 이미 연결된 걸 또 지으면 이미지
        # 경로를 우리 Textures/ 폴더로 다시 잇는 수고가 드니 이미 연결된 경우는 건드리지 않고 스킵.
        if already_linked:
            already_linked_count += 1
            img_node = bsdf.inputs["Base Color"].links[0].from_node
            img = img_node.image
            # 🔴 PM 실측(희귀함_선효진/보디빌더) — glb 소스는 png_path가 없다(텍스처가 파일이
            # 아니라 glb 안에 박힌 이미지 데이터). 원본 파일을 복사(shutil.copy2)하는 대신, 이미
            # 메모리에 로드된 이미지 데이터를 우리 Textures/ 폴더로 직접 저장(img.save())한다.
            # glb는 노멀맵·러프니스맵 등 나머지 PBR 텍스처도 같이 딸려 오지만(재질 노드에 여러 장
            # 연결) 이 프로젝트 관례대로 Base Color(진단용 색 텍스처) 한 장만 저장·연결하고
            # 나머지는 손대지 않는다(다른 모든 캐릭터도 디퓨즈 한 장만 쓴다).
            src_on_disk = bpy.path.abspath(img.filepath) if img.filepath else None
            if png_path and len(body.data.materials) == 1:
                tex_dst = os.path.join(tex_dir, os.path.basename(png_path))
                _copy_unless_same(png_path, tex_dst)
            elif src_on_disk and os.path.isfile(src_on_disk):
                # 🔴 PM 실측(제한_강보명/암부 카카시) — OBJ+MTL 다중 재질 소스는 이미지 노드가
                # 원본 파일 경로만 갖고 있고 픽셀 데이터를 메모리에 안 읽어 둘 때가 있다
                # (has_data=False) — 이때 img.save()는 "이미지 데이터가 없다"는 에러로 죽는다.
                # 원본 파일이 디스크에 실제로 있으면 그냥 그 파일을 그대로 복사한다(더 튼튼함).
                safe = os.path.basename(src_on_disk)
                tex_dst = os.path.join(tex_dir, safe)
                _copy_unless_same(src_on_disk, tex_dst)
            else:
                # 🔴 cfg["texture_by_material"] — Sketchfab glb는 박힌 그림 이름이 `Image_0`~`Image_6`뿐이다(2026-09-25 R11).
                #   유니티 ArtBinder.MatchTexture는 **재질 이름으로** 폴더 안 텍스처를 찾으므로(정확일치 → 부분일치 → 한 장뿐이면 그것)
                #   `MI_N110_E001_Body_CS01` ↔ `Image_0`은 안 맞고, 장수가 여럿이라 폴백도 없어 **전부 회색**으로 나간다.
                #   그래서 파일 이름을 재질 이름으로 짓는다. 옛 항목(희귀함_박도진 등 Image_N 커밋본)은 안 바꾸려고 켜는 항목만.
                src_img_name = mat.name if cfg.get("texture_by_material") else img.name
                safe = "".join(c for c in src_img_name if c.isalnum() or c in "._-") or mat.name
                if not safe.lower().endswith((".png", ".jpg", ".jpeg")):
                    safe += ".png"
                tex_dst = os.path.join(tex_dir, safe)
                img.filepath_raw = tex_dst
                img.file_format = "PNG"
                img.save()
            img.filepath = tex_dst
            img.name = os.path.basename(tex_dst)
            bsdf.inputs["Metallic"].default_value = 0.0
            bsdf.inputs["Roughness"].default_value = 0.8
        else:
            # 🔴 PM 실측(전설적인_엄태웅) — 재질 5개가 전부 텍스처 노드 0(단색)인 소스도 있다.
            # 이때는 재질 이름별로 어느 텍스처 파일을 쓸지 cfg["texture_files"]에 직접 대응시켜
            # 준다({"cuerpo": "$_BaseColor_7.png", ...}) — 없으면(재질 1개짜리 OBJ 립처럼) 기존
            # 대로 단일 png_path를 쓴다.
            src_name = cfg.get("texture_files", {}).get(mat.name)
            src_path = all_images.get(src_name) if src_name else png_path
            # 🔴 glb 원본의 **단색 재질**은 그대로 둔다(2026-09-24, 희귀함_박도진).
            #   .glb는 텍스처가 파일로 안 풀려 있어 png_path가 None이다. 그런 소스에서 텍스처 노드가 없는 재질은
            #   「못 찾은」 게 아니라 **원래 단색**이다(박도진 katana_hair — 나머지 재질 아홉은 전부 이미지가 붙어 있다).
            #   예전엔 여기서 죽어 **항목 전체가 안 돌았다**(181개 실행 검사에서 잡힘). glTF 임포터가 만든 단색을 그대로 쓴다.
            if src_path is None and png_path is None:
                report.setdefault("단색 재질 유지", []).append(mat.name)
                continue
            assert src_path, f"{mat.name}: 텍스처를 못 찾음(texture_files 확인)"
            dst_name = os.path.basename(src_path).lstrip("$") or mat.name + ".png"
            tex_dst = os.path.join(tex_dir, dst_name)
            # 🔴 같은 파일을 자기 자신에 복사하면 shutil이 SameFileError로 죽는다(2026-09-24 R01 박진웅).
            #   재질 19개가 텍스처 10장을 나눠 쓰는데, 앞 재질이 이미 tex_dir에 넣어 둔 것을 뒤 재질이
            #   src로 잡으면 src == dst가 된다. **이미 그 자리에 있는 것**이니 복사만 건너뛰면 된다.
            _copy_unless_same(src_path, tex_dst)
            mat.use_nodes = True
            nt = mat.node_tree
            for n in [n for n in nt.nodes if n.type not in ("BSDF_PRINCIPLED", "OUTPUT_MATERIAL")]:
                nt.nodes.remove(n)
            bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
            img_node = nt.nodes.new("ShaderNodeTexImage")
            img_node.image = bpy.data.images.load(tex_dst, check_existing=True)
            nt.links.new(img_node.outputs["Color"], bsdf.inputs["Base Color"])
            bsdf.inputs["Metallic"].default_value = 0.0
            bsdf.inputs["Roughness"].default_value = 0.8
        # 🔴 PM 실측(blender 세션, 전설적인_엄태웅 이후) — glb/FBX 재질 노드에 Alpha가 텍스처의
        # 알파 채널(또는 다른 노드)에 연결돼 있으면 유니티에서 인형이 반투명하게 들어온다(원본
        # 텍스처의 알파가 의도치 않게 살아있는 경우). Alpha 링크를 끊고 1.0으로 고정한다.
        if "Alpha" in bsdf.inputs and bsdf.inputs["Alpha"].links:
            for link in list(bsdf.inputs["Alpha"].links):
                mat.node_tree.links.remove(link)
            alpha_fixed.append(mat.name)
        if "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = 1.0
        if hasattr(mat, "blend_method"):
            mat.blend_method = "OPAQUE"
    report["재질"] = [m.name for m in body.data.materials if m]
    report["재질 텍스처 이미 연결됨(개수)"] = already_linked_count
    report["재질 Alpha 링크 제거(반투명 방지)"] = alpha_fixed

    # ── 감량(선택, cfg["decimate_ratio"]) — 원본 삼각형이 규격보다 훨씬 많은 AI 생성 메시용.
    decimate_ratio = cfg.get("decimate_ratio", 1.0)
    if decimate_ratio < 0.999:
        tri_before = sum(len(p.vertices) - 2 for p in body.data.polygons)
        mod = body.modifiers.new("decimate", "DECIMATE")
        mod.ratio = decimate_ratio
        dg = bpy.context.evaluated_depsgraph_get()
        new_mesh = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        body.modifiers.remove(mod)
        old_mesh = body.data
        body.data = new_mesh
        bpy.data.meshes.remove(old_mesh)
        report["감량"] = {"전": tri_before, "후": sum(len(p.vertices) - 2 for p in body.data.polygons)}

    # 🔴 노멀 재계산(2026-09-23 blender, 사장님이 희귀함_장하민 머리가 게임에서 검은 덩어리로 보인다고 지적 → 원인):
    #   게임 립 OBJ는 면 방향이 뒤집혀 들어온다(장하민 실측: 면 4,772개 중 바깥을 보는 게 1,784개뿐 = 37%).
    #   블렌더 기본 렌더는 양면을 다 그려서 멀쩡해 보이지만, 유니티 URP는 뒷면을 버리므로 **얼굴이 안쪽 껍데기로 보여 검게** 나온다
    #   (뒷면 컬링을 켜고 렌더해 그대로 재현함). 껍질마다 바깥으로 맞춘다 — gen_scan_rig.py가 이미 상시로 하는 것과 같은 처리.
    bm = bmesh.new()
    bm.from_mesh(body.data)
    before = sum(1 for f in bm.faces if f.normal.dot(f.calc_center_median() - sum((v.co for v in bm.verts), Vector()) / len(bm.verts)) > 0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    after = sum(1 for f in bm.faces if f.normal.dot(f.calc_center_median() - sum((v.co for v in bm.verts), Vector()) / len(bm.verts)) > 0)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    report["노멀 재계산"] = {"면": len(body.data.polygons), "바깥 향한 면 전": before, "후": after}

    world = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    lo, hi = world.min(0), world.max(0)
    H = float(hi[2] - lo[2])
    band = world[(world[:, 2] >= lo[2] + H * cfg["center_band"][0]) & (world[:, 2] <= lo[2] + H * cfg["center_band"][1])]
    cx, cy = float((band[:, 0].min() + band[:, 0].max()) / 2), float((band[:, 1].min() + band[:, 1].max()) / 2)
    s = cfg["height"] / H
    G = Matrix.Scale(s, 4) @ Matrix.Translation((-cx, -cy, -lo[2]))
    report["원본 키"], report["배율"] = round(H, 4), round(s, 5)
    body.data.transform(G @ body.matrix_world)
    body.matrix_basis = Matrix.Identity(4)

    Hf = cfg["height"]
    table = bone_table(cfg["joints"])
    # 🔴 발끝 뼈는 **표를 믿지 않고 메시에서 잰다**(2026-09-24, PM 요청 — 전수 검사 C군).
    #   이 파일의 joints 표들은 ToeBase y를 −0.14로 **복붙**해 왔다. 그런데 신발 앞끝은 모델마다 다르다 —
    #   제한_강보명·희귀함_유재헌·전설적인_이재윤·희귀함_윤현모·초월_박민석_ADAP **다섯 종의 발끝 좌표가
    #   ±0.16, −0.25까지 똑같았다.** 결함 다섯이 아니라 복붙 한 줄이었다.
    #   한 종씩 고치면 여섯 번째가 또 같은 값으로 태어나므로 **여기서** 고친다.
    #   재는 법: 발목(Foot) 높이 아래·그쪽 발 x 둘레의 정점이 신발이다. 그 앞끝(y 최소)과 뒤끝(y 최대)을 재서
    #   ToeBase는 앞에서 35% 되는 자리, ToeTip은 앞끝 바로 안쪽에 둔다. x·z는 표 그대로 둔다(그쪽은 안 틀렸다).
    if cfg.get("fit_toes", True) and "ToeBase" in cfg["joints"]:
        vz = np.array([v.co[:] for v in body.data.vertices])
        fx, fy, fz = (c * Hf for c in cfg["joints"]["Foot"])
        fitted, tbl = {}, []
        for side, sx in (("Left", 1.0), ("Right", -1.0)):
            sel = vz[(vz[:, 2] <= fz * 1.6) & (np.abs(vz[:, 0] - sx * fx) <= max(fx, Hf * 0.06) * 1.8)]
            if len(sel) < 20:
                continue
            y0, y1 = float(sel[:, 1].min()), float(sel[:, 1].max())
            fitted[side + "ToeBase"] = y0 + 0.35 * (y1 - y0)
            fitted[side + "ToeTip"] = y0 + 0.05 * (y1 - y0)
        for bname, h, t, parent in table:
            h = (h[0], fitted[bname] / Hf, h[2]) if bname in fitted else h
            tn = bname.replace("ToeBase", "ToeTip")
            t = (t[0], fitted[tn] / Hf, t[2]) if tn in fitted and bname.endswith("ToeBase") else t
            tbl.append((bname, h, t, parent))
        if fitted:
            table = tbl
            report["발끝 다시 잼"] = {k: round(v, 4) for k, v in fitted.items()}
            report["발끝 표값"] = round(cfg["joints"]["ToeBase"][1] * Hf, 4)
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

    # ── 가중치: 먼저 bone heat(정상적인 뼈 열 자동가중치)를 시도 — 저폴리·T자·대칭이라 될 가능성이
    # 크다(PM 조건 확인). 죽은 뼈(정점 0개)가 하나라도 있으면 지오데식으로 통째로 대체한다.
    for b in data.bones:
        body.vertex_groups.new(name=b.name)
    for o in scene.objects:
        o.select_set(o in (body, arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")

    def dead_bones():
        counts = {b.name[len(PREFIX):]: 0 for b in data.bones}
        for v in body.data.vertices:
            for g in v.groups:
                if g.weight > 0.01:
                    counts[body.vertex_groups[g.group].name[len(PREFIX):]] += 1
        return [k for k, c in counts.items() if c == 0], counts

    def rescue_dead(dead_list):
        """🔴 저폴리 부위는 이웃 관절 사이 굴곡이 거의 없어 한쪽 뼈가 표면을 한 뼘도 못 얻는 경우가
        있다(실측: 조셉 부츠 ToeBase 정점 0개는 Foot이 다 가져감, 죠스케는 옷깃에 가려 Neck이
        0개). 부모나 자식 뼈가 가진 정점 중 이 뼈의 마디(머리-꼬리 선분)에 가장 가까운 쪽을
        나눠 준다 — bone heat 결과가 대체로 괜찮은데 극소수 뼈만 죽은 경우, 통째로 지오데식으로
        갈아엎지 않고 이 정도로 구제하면 나머지 뼈의 좋은 가중치를 그대로 살릴 수 있다."""
        table_by_name = {bname: (h, t, parent) for bname, h, t, parent in table}
        children = {}
        for bname, (h, t, parent) in table_by_name.items():
            if parent:
                children.setdefault(parent, []).append(bname)
        for bone_name in dead_list:
            h, t, parent = table_by_name[bone_name]
            neighbors = ([parent] if parent else []) + children.get(bone_name, [])
            donors = [n for n in neighbors if n]
            head_w, tail_w = Vector(h) * Hf, Vector(t) * Hf
            target_group = body.vertex_groups[PREFIX + bone_name]
            pool = []
            for donor in donors:
                dg = body.vertex_groups[PREFIX + donor]
                for v in body.data.vertices:
                    if any(g.group == dg.index and g.weight > 0.01 for g in v.groups):
                        ab = tail_w - head_w
                        tt = max(0.0, min(1.0, (v.co - head_w).dot(ab) / max(ab.length_squared, 1e-9)))
                        dist = (head_w + ab * tt - v.co).length
                        pool.append((dist, v.index, dg))
            pool.sort(key=lambda p: p[0])
            n_take = max(1, int(len(pool) * 0.3)) if pool else 0
            for _dist, vi, dg in pool[:n_take]:
                dg.remove([vi])
                target_group.add([vi], 1.0, "REPLACE")

    dead, counts = dead_bones()
    report["가중치 방식"] = "bone heat"
    # 🔴 PM 실측(희귀함_선효진/보디빌더) — bone heat는 메시 "표면"이 아니라 부피 전체의 열
    # 확산(voxel)이라, 손이 허벅지 바깥쪽에 닿을 듯 가까운 "차렷" 자세에서는 위상이 멀쩡히
    # 이어져 있어도(위 복제 정점 병합 이후) 뼈 전부가 "살아는" 있지만 허벅지 겉면 일부가 손
    # 쪽으로 살짝 새서 T자로 펼 때 넓적다리에서 손까지 가는 실 같은 자국이 남았다(렌더로 확인).
    # 지오데식(표면 최단경로)은 이런 부피 누출이 원천적으로 없어(간선만 타고 가므로 허벅지→손은
    # 반드시 어깨를 거쳐야 한다) cfg["force_geodesic"]=True면 bone heat를 아예 건너뛰고 늘 지오데식만
    # 쓴다 — 앞으로도 몸이 스스로에 닿는 자세(차렷·팔짱 등)에서 재발할 수 있어 옵션으로 남긴다.
    if cfg.get("force_geodesic"):
        dead = [b.name[len(PREFIX):] for b in data.bones]
    # 🔴 죽은 뼈가 소수(전체의 15% 미만)면 bone heat 결과가 대체로 정확하다는 뜻이니(실측:
    # 희귀함_유재헌 — bone heat가 팔·몸통은 다 맞혔는데 발끝 2개만 놓쳤다) 통째로 지오데식으로
    # 바꾸지 않고 이웃 뼈 구제만 적용한다. bone heat가 아예 실패한 경우(조셉·죠스케처럼 전부
    # 죽음)만 지오데식으로 완전히 대체한다 — 안 그러면 잘 된 부위까지 지오데식의 덜 정교한
    # 경계(느슨한 옷 이음매에서 특히 두드러짐)로 덮어써 버린다.
    if dead and len(dead) < len(data.bones) * 0.15:
        report["bone heat 부분 실패 뼈(이웃 구제)"] = list(dead)
        rescue_dead(dead)
        dead, counts = dead_bones()
    elif dead:
        report["bone heat 실패 뼈"] = dead
        report["가중치 방식"] = "지오데식(bone heat 실패로 대체)"
        for vg in list(body.vertex_groups):
            body.vertex_groups.remove(vg)
        for b in data.bones:
            body.vertex_groups.new(name=b.name)
        idx, w, names = geodesic_bone_weights(body.data, table, data, cloth_radius=cfg.get("cloth_radius"))
        for vi in range(len(body.data.vertices)):
            for k in range(idx.shape[1]):
                body.vertex_groups[names[idx[vi, k]]].add([vi], float(w[vi, k]), "REPLACE")
        dead, counts = dead_bones()
        if dead:
            report["가중치 구제(이웃 뼈에서 30% 이관)"] = list(dead)
            rescue_dead(dead)
            dead, counts = dead_bones()
    report["뼈별 정점(w>0.01)"] = counts
    assert not dead, f"{name}: 가중치 없는 뼈 {dead}"

    # 🔴 PM 실측(초월_박민수_AD/토지, 유니티 반려) — 오버사이즈 스웨터는 소매가 팔 표면을 크게
    # 벗어나 부풀어서, 지오데식 최단경로가 소매 대부분을 어깨 솔기 너머 Chest/Spine 씨앗에
    # 내줬다(팔 뼈 중심축이 옷 속 빈 공간이라 표면 최근접이 아니었다). cloth_radius(추가 씨앗)로
    # 고치려 했지만 반지름을 조금만 키워도 목둘레 천까지 팔로 끌려가 Idle에서 천막처럼
    # 부풀었다(직접 렌더로 확인, 실패). 대신 표면 위상과 무관하게 "3D 직선거리"로 재배정하는
    # 후처리로 바꿨다 — Chest/Spine 소유 정점 중 팔 사슬(Shoulder-Arm-ForeArm) 선분까지의
    # 직선거리가 몸통 사슬(Chest-Spine-Neck) 선분까지의 직선거리보다 뚜렷이(20% 이상) 가까우면
    # 그 팔로 넘긴다 — 소매처럼 팔을 감싸고 물리적으로 가까운 부위만 정확히 잡고, 진짜 목둘레
    # (몸통에 더 가까움)는 안 건드린다. cfg["cloth_reclaim"]=True인 캐릭터만 적용.
    if cfg.get("cloth_reclaim"):
        def seg_dist(p, a, b):
            ab = b - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
            return (a + ab * t - p).length

        torso_segs = [(np.array(data.bones[PREFIX + n1].head_local), np.array(data.bones[PREFIX + n2].head_local))
                      for n1, n2 in (("Chest", "Neck"), ("Spine", "Chest"))]
        reclaim_count = 0
        for side in ("Left", "Right"):
            arm_segs = [(np.array(data.bones[PREFIX + side + n1].head_local), np.array(data.bones[PREFIX + side + n2].head_local))
                        for n1, n2 in (("Shoulder", "Arm"), ("Arm", "ForeArm"), ("ForeArm", "Hand"))]
            chest_vg = body.vertex_groups[PREFIX + "Chest"]
            spine_vg = body.vertex_groups[PREFIX + "Spine"]
            arm_vg = body.vertex_groups[PREFIX + side + "Arm"]
            forearm_vg = body.vertex_groups[PREFIX + side + "ForeArm"]
            for v in body.data.vertices:
                owner_vg = max((g for g in v.groups if g.weight > 0.01), key=lambda g: g.weight, default=None)
                if owner_vg is None or owner_vg.group not in (chest_vg.index, spine_vg.index):
                    continue
                p = Vector(v.co)
                d_torso = min(seg_dist(p, Vector(a), Vector(b)) for a, b in torso_segs)
                arm_d = [seg_dist(p, Vector(a), Vector(b)) for a, b in arm_segs]
                d_arm = min(arm_d)
                if d_arm < d_torso * 0.8:
                    target_vg = forearm_vg if arm_d.index(d_arm) >= 1 else arm_vg
                    for g in list(v.groups):
                        if g.group in (chest_vg.index, spine_vg.index):
                            body.vertex_groups[g.group].remove([v.index])
                    target_vg.add([v.index], 1.0, "REPLACE")
                    reclaim_count += 1
        if reclaim_count:
            report["소매 3D 직선거리 재배정(옷이 부풀어 표면경로가 속은 경우)"] = reclaim_count
            # 🔴 실측 발견 — 정점마다 REPLACE 1.0으로 끊어 어깨-소매 경계에 작게 갈라진 솔기가
            # 남는다(렌더로 확인). 이웃 평균으로 무디게 하려 했으나 mesh.edges 위상 인접(그
            # 경계가 실은 겹친 UV 심이라 안 건너감)·KDTree 3D 반경 인접(오히려 더 큰 구멍·삼각
            # 결손 생성) 둘 다 렌더로 확인해 보니 원래보다 나빠져 전부 되돌렸다 — 지금은 무딤
            # 처리 없이 하드 재배정만 남긴 상태(작은 솔기 자국은 있지만 천막처럼 날리거나 구멍이
            # 뚫리지는 않음). 더 나은 대안(PM 제안 "몸 속 팔 원통 대리"류 복셀 리메시 프록시
            # heat)은 아직 미구현 — 다음 담당이 이어서 고칠 것.
            dead, counts = dead_bones()
            report["뼈별 정점(w>0.01)"] = counts
            assert not dead, f"{name}: 가중치 없는 뼈(소매 재배정 뒤) {dead}"

    # 🔴 어깨에 걸친 해군 코트(2026-09-25 R11 키자루·R12 아오키지·R15 아카이누 — 셋이 **같은 Mantle 메시**,
    #   정점 6,549~6,551·경계 똑같음). 코트는 팔을 안 끼고 어깨에 얹혀 있고, 빈 소매가 **등 뒤로 수평으로** 뻗어 있다
    #   (게임에서 천 뼈가 잡던 자세가 립에 그대로 굳었다). 팔 뼈 근처라 가중치를 자동에 맡기면 소매·어깨가 팔을 따라가
    #   걷기에서 찢어진다. cfg["rigid_materials"] = {"재질 이름 조각": "뼈"} — 그 재질 면의 정점을 **그 뼈 하나에 1.0**으로 못박는다.
    #   straighten_arms보다 **먼저** 해야 한다(팔을 펼 때 코트가 따라 올라가면 안 된다).
    if cfg.get("rigid_materials"):
        pinned = {}
        for part, bone in cfg["rigid_materials"].items():
            mats = {i for i, m in enumerate(body.data.materials) if m and part in m.name}
            assert mats, f"{name}: rigid_materials '{part}'에 맞는 재질이 없다 {[m.name for m in body.data.materials]}"
            vids = sorted({vi for p in body.data.polygons if p.material_index in mats for vi in p.vertices})
            for vg in body.vertex_groups:
                vg.remove(vids)
            body.vertex_groups[PREFIX + bone].add(vids, 1.0, "REPLACE")
            pinned[part + "→" + bone] = len(vids)
        report["재질 통째 고정"] = pinned
        dead, counts = dead_bones()
        report["뼈별 정점(w>0.01)"] = counts
        assert not dead, f"{name}: 가중치 없는 뼈(재질 고정 뒤) {dead}"

    # 🔴 cfg["reassign_above"] = [dict(src=(뼈…), dst=뼈, z=높이비)] — src가 우세한 정점 중 z(키 비율) 이상을 dst 1.0으로 옮긴다
    #   (2026-09-25 R20 버지스). 다리가 짧아(가랑이 0.34) 벨트(0.42~0.48)가 UpLeg 씨앗에 더 가까워 **벨트가 허벅지를 따라 휘었다**
    #   (걷기 8프레임 라이브 창에서 확인). 벨트 아래끝 위로는 골반에 붙인다.
    for rule in cfg.get("reassign_above", []):
        src = {PREFIX + b for b in rule["src"]}
        dst_vg = body.vertex_groups[PREFIX + rule["dst"]]
        moved = []
        for v in body.data.vertices:
            if v.co.z < rule["z"] * Hf or not v.groups:
                continue
            if body.vertex_groups[max(v.groups, key=lambda g: g.weight).group].name in src:
                moved.append(v.index)
        for vg in body.vertex_groups:
            vg.remove(moved)
        dst_vg.add(moved, 1.0, "REPLACE")
        report.setdefault("높이 위 재배정", {})["→".join(rule["src"]) + "→" + rule["dst"] + f"@{rule['z']}"] = len(moved)
    if cfg.get("reassign_above"):
        dead, counts = dead_bones()
        report["뼈별 정점(w>0.01)"] = counts
        assert not dead, f"{name}: 가중치 없는 뼈(높이 재배정 뒤) {dead}"

    # 🔴 PM 실측(희귀함_선효진/보디빌더) — 이 소스는 65,536정점(16비트 인덱스 한계) 단위로 잘려
    # 나온 조각 셋을 합친 거라(Object_4·5·6, 위 복제 정점 병합으로 다시 1개 표면으로 붙음) 진짜
    # 원본 메시 그대로인데, "차렷" 자세에서 손이 허벅지 옆에 거의 닿아 원본 자체에 손에서 허벅지
    # 까지 이어지는 얇고 촘촘한 "막"(포토그래메트리류 스캔에서 자세상 맞닿은 두 부위가 하나의
    # 표면으로 재구성되는 흔한 결함 — 에지 길이 분포로 확인: 유별나게 긴 에지가 없어 정상 밀도의
    # 진짜 표면이다)이 붙어 있다. 지오데식 거리로 검증해 보니(팔 사슬에 걸린 정점 전부의 씨앗
    # 대비 최대 거리 0.50 < 팔 하나 길이의 1.6배인 0.71) 이 막의 정점들 자체가 이미 다리 뼈로
    # 올바르게 가중치돼 있다 — 즉 가중치는 처음부터 문제가 아니었다. 문제는 순수 위상(topology):
    # 다리로 가중치된 정점과 팔로 가중치된 정점 사이를 원본이 실제 삼각형 면으로 이어 놔서, 가중치를
    # 아무리 옮겨도 그 면 자체가 양 끝을 붙들고 있어 T자로 펼 때 리본처럼 늘어난다 — 면을 잘라야
    # 한다. 정점의 "주 뼈"가 팔 사슬 쪽인 면과 다리 사슬 쪽인 면이 만나는 자리(해부학적으로 절대
    # 안 이어져야 할 조합)를 찾아 그 면만 지운다 — 팔이 실제로 벌어지면 그 자리는 원래도 빈
    # 틈(겨드랑이~엉덩이 사이)이어야 맞다(다른 모든 캐릭터도 T자에서 팔과 몸통 사이는 뚫려 있다).
    ARM_CHAIN = ["Shoulder", "Arm", "ForeArm", "Hand"]
    # Shoulder는 Chest에 정상적으로 붙지만(빗장뼈 관절) Arm·ForeArm·Hand는 팔 사슬 밖 어떤 뼈와도
    # 표면이 이어질 수 없다(팔이 벌어지면 그 자리는 항상 빈 틈) — 허벅지(Hips 쪽으로도 새는 걸
    # 봐서 UpLeg뿐 아니라 몸통 전체를 "팔 아님"으로 잡는다.
    ARM_STRICT = ["Arm", "ForeArm", "Hand"]
    ARM_NAMES = {PREFIX + s + b for s in ("Left", "Right") for b in ARM_CHAIN}
    ARM_STRICT_NAMES = {PREFIX + s + b for s in ("Left", "Right") for b in ARM_STRICT}
    dom = {}
    for v in body.data.vertices:
        if not v.groups:
            continue
        g = max(v.groups, key=lambda g: g.weight)
        dom[v.index] = body.vertex_groups[g.group].name
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.ensure_lookup_table()
    to_delete = []
    for f in bm.faces:
        names = {dom.get(v.index) for v in f.verts}
        if (names & ARM_STRICT_NAMES) and (names - ARM_NAMES):
            to_delete.append(f)
    # 🔴 cfg["keep_fused_faces"] — 이 삭제는 **스캔 결함용**인데 무조건 돈다(2026-09-25 R15 아카이누 81면 · R20 버지스 58면).
    #   게임 립은 소매가 몸통에 정상으로 이어져 있어, 지오데식 경계가 겨드랑이에서 Shoulder를 건너뛰면
    #   Arm↔Chest가 한 면을 나눠 갖게 되고 **그 면이 지워져 옆구리에 구멍이 뚫린다**(리타게팅 옆모습 렌더로 발견 —
    #   정면 T자에선 팔에 가려 안 보인다). 원본에 막이 없는 게임 립은 이걸 켜서 삭제를 건너뛴다.
    if cfg.get("keep_fused_faces"):
        report["팔↔다리 융착 면(삭제 안 함 — keep_fused_faces)"] = len(to_delete)
        to_delete = []
    report["팔↔다리 융착 면 제거(원본 스캔 결함)"] = len(to_delete)
    if to_delete:
        bmesh.ops.delete(bm, geom=to_delete, context="FACES_ONLY")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bm.to_mesh(body.data)
        body.data.update()
    bm.free()

    # 🔴 PM 실측(유니티 임포트 경고, 희귀함_강보명) — 뼈는 전부 정점을 가졌어도(위 dead_bones 검사
    # 통과) 개별 정점이 어느 뼈에도 안 걸리는 경우가 남을 수 있다(bone heat가 일부 정점에 미세
    # 가중치도 안 준 경우 — 유니티가 그런 정점을 전부 뼈 #0(Hips)에 강제 배정해, 정지 T자에선 안
    # 보이다가 자세를 잡는 순간 골반 쪽으로 늘어난다). 가장 가까운 「가중치 있는」 정점의 값을
    # 그대로 복사해 채운다([[static-skin-rigging]] 방식과 동일).
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

    # 🔴 bone heat 경로는 parent_set(type="ARMATURE_AUTO")가 이미 Armature 모디파이어를 하나
    # 만들어 놨다 — 여기서 또 하나 더 만들면 같은 변형이 두 겹으로 쌓여, 이어서 팔을 펼 때
    # 그 회전이 두 번 적용돼 목표 각의 2배로 꺾인다(실측: 희귀함_유재헌, 팔이 수평이 아니라
    # 위로 솟음 — 뼈 자체는 정확히 수평인데 메시만 위로 꺾여 있어 모디파이어 중복으로 좁혔다).
    # 지오데식 경로는 parent_set을 안 거치므로 모디파이어가 아예 없다 — 있으면 지우고 하나만.
    for old_mod in [m for m in body.modifiers if m.type == "ARMATURE"]:
        body.modifiers.remove(old_mod)
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    body.parent = arm

    if cfg.get("straighten_arms"):
        report["팔 처짐→수평(°)"] = straighten_arms(arm, body)

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
    if cfg.get("check_model_scale"):
        # 🔴 PM 실측(희귀함_유재헌) — 원본 FBX 메시 노드에 Lcl Scaling=100이 걸려 있었다(노트북
        # 회전과 같은 부류 — 조용히 남으면 유니티에서 100배로 커진다). 우리 파이프라인은 언제나
        # o.parent=None 뒤 o.data.transform(o.matrix_world)·matrix_basis=Identity로 겉싸개를
        # 정점에 굽지만, 실제로 됐는지 원시 FBX를 파싱해 확인한다.
        report["Model Lcl Scaling(내보낸 FBX, 전부 1.0이어야 함)"] = model_lcl_scaling(dst)
    if outer_dir:
        shutil.rmtree(outer_dir, ignore_errors=True)
    if inner_dir:
        shutil.rmtree(inner_dir, ignore_errors=True)
    if render_dir:
        report["렌더"] = judge(name, arm, body, render_dir)
    return report


def judge(name, arm, body, out):
    """판정 렌더 — 정면·옆면 T자, 재질(텍스처) 입혀서."""
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
        print("립리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
