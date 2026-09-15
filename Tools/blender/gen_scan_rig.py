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
펴는 좁은 용도다. 이 원본(~/Downloads/free_download_athletic_african_man_walking_223.glb)은 파일명
그대로 "걷는 도중" 스캔이라 좌우가 전혀 다른 자세(한쪽 발 앞·반대쪽 뒤, 팔도 한쪽은 팔꿈치 굽혀
가슴 쪽으로·반대쪽은 뒤로 스윙)라 이 구조로는 표현이 안 된다(구현담당1이 확인하고 멈춘 지점 —
~/Downloads/특별함_최동준_mixamo업로드_설명.txt 참고). 그래서 이 파일은 (1) 관절을 왼쪽/오른쪽
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

UNIT = "특별함_최동준"
SOURCE = os.path.expanduser("~/Downloads/free_download_athletic_african_man_walking_223.glb")
OUT_PATH = f"Assets/Art/Units/{UNIT}/{UNIT}.fbx"
HEIGHT = 1.8                       # 최종 키(m)
CENTER_BAND = (0.02, 0.08)         # 발목 높이(원본 키 비율) — 두 발 사이로 원점(gen_skin_rig.py와 같은 방식)
DECIMATE_RATIO = 0.4               # 99,994 → 약 4만 삼각형(균등 감량 — 이유는 아래 build() 주석)

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


def straighten_limbs(arm, body):
    """걷는 자세 그대로 잡은 뼈대를 팔다리 네 사슬(어깨~손·엉덩이~발끝) 전부 T자로 편 뒤 그 자세를
    쉬는 자세로 굽는다 — gen_skin_rig.py의 level_arms()와 같은 기법(뼈 하나씩 현재 방향→목표 방향
    쿼터니언 회전)을 팔다리 전부·발까지 일반화했다. 굽기 전후 메시가 움직이지 않는지, 최종 뼈
    방향이 목표와 맞는지 검증한다."""
    scene = bpy.context.scene
    for o in scene.objects:
        o.select_set(o == arm)
    bpy.context.view_layer.objects.active = arm
    turned = {}
    for bone, target in STRAIGHTEN:
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
    for bone, target in STRAIGHTEN:
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


def build(out_dir=None, render_dir=None):
    dst = os.path.join(out_dir, os.path.basename(OUT_PATH)) if out_dir else os.path.join(ROOT, OUT_PATH)
    tex_dir = os.path.join(os.path.dirname(dst), "Textures")
    report = {"이름": UNIT, "원본": SOURCE, "sha256": hashlib.sha256(open(SOURCE, "rb").read()).hexdigest()}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SOURCE)
    scene = bpy.context.scene
    body = next(o for o in scene.objects if o.type == "MESH")
    body.name = body.data.name = "Body"
    report["원본 정점·삼각형"] = [len(body.data.vertices), sum(len(p.vertices) - 2 for p in body.data.polygons)]

    # ── 텍스처: PM 조사대로 이미지 0=베이스(jpeg)·1=노멀(png), 원본 바이트 그대로 Textures/에.
    j, binchunk = glb(SOURCE)
    os.makedirs(tex_dir, exist_ok=True)
    base_path, normal_path = os.path.join(tex_dir, "default_baseColor.jpg"), os.path.join(tex_dir, "default_normal.png")
    open(base_path, "wb").write(image_bytes(j, binchunk, 0))
    open(normal_path, "wb").write(image_bytes(j, binchunk, 1))
    rebuild_material(body.data.materials[0], [("Base Color", "default_baseColor.jpg"), ("Normal", "default_normal.png")], tex_dir)

    # ── 중심맞춤·스케일(gen_skin_rig.py의 build()와 같은 식) — 회전은 불필요(이미 −Y를 본다,
    # raw_from_negY.png로 확인). 관절도 메시와 같은 G를 써서 어긋나지 않게 한다.
    world = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    lo, hi = world.min(0), world.max(0)
    H = float(hi[2] - lo[2])
    band = world[(world[:, 2] >= lo[2] + H * CENTER_BAND[0]) & (world[:, 2] <= lo[2] + H * CENTER_BAND[1])]
    cx, cy = float((band[:, 0].min() + band[:, 0].max()) / 2), float((band[:, 1].min() + band[:, 1].max()) / 2)
    s = HEIGHT / H
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
    # 설명(~/Downloads/특별함_최동준_mixamo업로드_설명.txt)이 이미 같은 이유로 균등 감량했고,
    # 이 파일도 시간상 그 판단을 따른다. 정점그룹으로 얼굴·손 보호는 나중에 필요하면 추가 가능).
    mod = body.modifiers.new("decimate", "DECIMATE")
    mod.ratio = DECIMATE_RATIO
    dg = bpy.context.evaluated_depsgraph_get()
    new_mesh = bpy.data.meshes.new_from_object(body.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    body.modifiers.remove(mod)
    old_mesh = body.data
    body.data = new_mesh
    bpy.data.meshes.remove(old_mesh)
    report["감량 후 삼각형"] = sum(len(p.vertices) - 2 for p in body.data.polygons)

    # ── 뼈대: 22개(Hips 루트 + Spine·Spine1·Spine2·Neck·Head + 좌우 Shoulder·Arm·ForeArm·Hand
    # + 좌우 UpLeg·Leg·Foot·ToeBase) — 위 JOINTS_CM(걷는 자세 그대로, 좌우 따로)에 G를 적용해 짓는다.
    table = bone_table(JOINTS_CM)
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
    top_share = max(counts.values()) / len(body.data.vertices)
    assert top_share < 0.5, f"한 뼈에 정점 {top_share:.0%} — bone heat 실패(1차 증상)"

    report["걷는 자세→T자(°)"] = straighten_limbs(arm, body)

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
    k = HEIGHT / top
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
        report["판정"] = judge(UNIT, arm, body, render_dir)
        report["옆구리·가랑이 확대"] = armpit_crotch_renders(arm, body, render_dir)
    return report


def armpit_crotch_renders(arm, body, out):
    """PM 요청 — T자에서 옆구리(겨드랑이)·가랑이 확대. judge()가 이미 만든 씬(카메라·조명)을 그대로 쓴다."""
    scene = bpy.context.scene
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    cam = scene.camera
    views = [("armpit", (0, -3, 1.35), (math.radians(90), 0, 0), 0.7), ("crotch", (0, -3, 0.78), (math.radians(90), 0, 0), 0.6)]
    for tag, loc, rot, sc in views:
        cam.location, cam.rotation_euler, cam.data.ortho_scale = loc, rot, sc
        scene.render.filepath = os.path.join(out, f"{UNIT}_{tag}.png")
        bpy.ops.render.render(write_still=True)
    return [f"{UNIT}_{tag}.png" for tag, *_ in views]


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = render_dir = None
    it = iter(args)
    for a in it:
        if a == "--out":
            out_dir = next(it)
        elif a == "--render":
            render_dir = next(it)
    import json
    r = build(out_dir, render_dir)
    print("리깅  " + json.dumps(r, ensure_ascii=False))


if __name__ == "__main__":
    main()
