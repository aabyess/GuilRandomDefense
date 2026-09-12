"""지금까지 만든 FBX 전부를 사장님 Blender 창의 판 위에 종류별로 전시한다(창 전용 — 내보내기 없음).

창에서(MCP):
    ns = {"__name__": "show_all", "__file__": ".../Tools/blender/show_all.py"}
    exec(compile(open(ns["__file__"], encoding="utf-8").read(), ns["__file__"], "exec"), ns)
    ns["show_all"]()

🔴 커밋된 FBX를 그대로 들여올 뿐 새로 짓지 않는다 — 판 위 모양이 곧 저장소 파일이다.
크기도 실제 그대로(미터)다 — 크기 비교가 이 전시의 목적이라 늘리거나 줄이지 않는다
(FBX는 게임 단위로 읽히므로 1/11.4만 곱해 사장님 장면의 미터에 맞춘다).
배치 규칙은 showcase.py. 새 에셋을 만들면 아래 ROWS에 한 줄 더한다.
"""

import importlib
import os
import sys

import bpy

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(PROJECT, "Assets", "Art")
COLLECTION = "전시판"
OLD_COLLECTIONS = ("벽_작업",)          # 예전 창 작업 컬렉션 — 이 전시로 옮기며 비운다

# (종류, [(Assets/Art 아래 경로(확장자 빼고), 라벨, 이어 붙이는 조각인가), ...]) — 앞줄(뷰 쪽)부터.
# 🔴 키 큰 나무 줄이 맨 뒤다 — 앞에 두면 뒷줄을 가린다.
ROWS = [
    ("문", [("Walls/정의문", "JusticeGate", False)]),
    ("나무 울타리", [("Walls/나무울타리", "WoodFence", True)]),
    ("돌 벽", [("Walls/돌담_두꺼움", "StoneWall_Thick", True),
              ("Walls/돌담_얇음", "StoneWall_Thin", True),
              ("Walls/돌기둥", "StonePillar", False)]),
    ("풀·덤불", [("Nature/Grass/풀_01", "Grass_01", False),
               ("Nature/Grass/풀_02", "Grass_02", False),
               ("Nature/Grass/풀_03", "Grass_03", False),
               ("Nature/Grass/덤불_01", "Bush_01", False),
               ("Nature/Grass/덤불_02", "Bush_02", False),
               ("Nature/Grass/억새_01", "Reed_01", False)]),
    ("바위", [("Nature/Rocks/바위_01", "Rock_01", False),
             ("Nature/Rocks/바위_02", "Rock_02", False),
             ("Nature/Rocks/바위_03", "Rock_03", False),
             ("Nature/Rocks/바위_04", "Rock_04", False),
             ("Nature/Rocks/바위_05", "Rock_05", False),
             ("Nature/Rocks/판석_01", "Slab_01", False),
             ("Nature/Rocks/판석_02", "Slab_02", False),
             ("Nature/Rocks/바위무리_01", "Rock_Cluster_01", False),
             ("Nature/Rocks/바위무리_02", "Rock_Cluster_02", False)]),
    ("나무", [("Nature/Trees/침엽수_01", "Conifer_01", False),
             ("Nature/Trees/침엽수_02", "Conifer_02", False),
             ("Nature/Trees/활엽수_01", "Broadleaf_01", False),
             ("Nature/Trees/활엽수_가을", "Broadleaf_Autumn", False),
             ("Nature/Trees/야자수_01", "Palm_01", False),
             ("Nature/Trees/죽은나무_01", "DeadTree_01", False),
             ("Nature/Trees/그루터기_01", "Stump_01", False)]),
]


def show_all():
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import showcase
    importlib.reload(showcase)                  # 창에서 여러 번 돌리므로 고친 내용이 바로 반영되게

    for name in OLD_COLLECTIONS:
        old = bpy.data.collections.get(name)
        if old is not None:
            showcase.clear(old)
            bpy.data.collections.remove(old)

    collection = bpy.data.collections.get(COLLECTION)
    if collection is None:
        collection = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(collection)
    showcase.clear(collection)

    rows = []
    for kind, items in ROWS:
        groups = []
        for rel, label, tiling in items:
            obj = showcase.import_fbx(collection, os.path.join(ART, rel + ".fbx"), 1.0 / UNITS_PER_METER)
            obj.name = label
            parts = [(obj, 0.0, 0.0)]
            if tiling:
                # 이어 붙이는 조각은 세 개를 딱 붙이고, 뒤에 1.5배로 늘린 세 개를 또 붙인다(이음매 확인).
                single = showcase.group(label, parts)
                length = single["x_max"] - single["x_min"]
                depth = single["y_max"] - single["y_min"]
                for k in (1, 2):
                    twin = obj.copy()
                    collection.objects.link(twin)
                    parts.append((twin, length * k, 0.0))
                for k in range(3):
                    stretched = obj.copy()
                    collection.objects.link(stretched)
                    stretched.scale.x = obj.scale.x * 1.5
                    parts.append((stretched, length * 0.25 + length * 1.5 * k, depth + 0.1))
            groups.append(showcase.group(label, parts))
        rows.append((kind, groups))

    board = showcase.lay_out(collection, rows)
    showcase.frame(board)
    return board
