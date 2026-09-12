"""지금까지 만든 FBX 전부를 사장님 Blender 창에 종류별 판으로 전시한다(창 전용 — 내보내기 없음).

창에서(MCP):
    ns = {"__name__": "show_all", "__file__": ".../Tools/blender/show_all.py"}
    exec(compile(open(ns["__file__"], encoding="utf-8").read(), ns["__file__"], "exec"), ns)
    boards = ns["show_all"]()          # {"판_나무": board, ...}

🔴 커밋된 FBX를 그대로 들여올 뿐 새로 짓지 않는다 — 판 위 모양이 곧 저장소 파일이다.
크기도 실제 그대로(미터)다 — 크기 비교가 이 전시의 목적이라 늘리거나 줄이지 않는다
(FBX는 게임 단위로 읽히므로 1/11.4만 곱해 사장님 장면의 미터에 맞춘다).
배치 규칙은 showcase.py. 새 에셋을 만들면 자기 종류의 BOARDS 항목에 더하고, 새 종류면 판을 새로 더한다.
"""

import importlib
import os
import sys

import bpy

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(PROJECT, "Assets", "Art")
OLD_COLLECTIONS = ("벽_작업", "전시판")     # 예전 한 장짜리 전시 — 판을 나누며 비운다

# 판마다 (컬렉션 이름, 제목, [(줄 이름, [(Assets/Art 아래 경로, 이름표, 이어 붙이는가), ...]), ...]).
# 판 안의 줄은 앞(뷰 쪽)부터 — 키 큰 것이 뒤. rows가 비면 빈 판(자리만, 해왕류).
BOARDS = [
    ("판_풀", "GRASS", [
        ("풀·덤불", [("Nature/Grass/풀_01", "Grass_01", False),
                   ("Nature/Grass/풀_02", "Grass_02", False),
                   ("Nature/Grass/풀_03", "Grass_03", False),
                   ("Nature/Grass/덤불_01", "Bush_01", False),
                   ("Nature/Grass/덤불_02", "Bush_02", False),
                   ("Nature/Grass/억새_01", "Reed_01", False)])]),
    ("판_벽", "WALLS", [
        ("기둥", [("Walls/돌기둥", "StonePillar", False), ("Walls/나무기둥", "WoodPost_Lantern", False), ("Walls/이끼돌기둥", "MossyPillar", False)]),
        ("울타리", [("Walls/나무울타리", "WoodFence", True), ("Walls/목책", "Palisade", True), ("Walls/목장울타리", "Rail_Fence", True),
                  ("Walls/대나무울타리", "Bamboo_Fence", True), ("Walls/밧줄난간", "Rope_Rail", True)]),
        ("얇은 벽", [("Walls/돌담_얇음", "StoneWall_Thin", True), ("Walls/석축_이끼", "Mossy_Wall", True),
                   ("Walls/벽돌담", "Brick_Wall", True), ("Walls/해안방파제", "Breakwater", True)]),
        ("두꺼운 벽", [("Walls/돌담_두꺼움", "StoneWall_Thick", True), ("Walls/성벽_마름돌", "Ashlar_Wall", True),
                    ("Walls/폐허벽", "Ruin_Wall", True), ("Walls/흉벽성벽", "Battlement", True)])]),
    ("판_문", "GATE", [
        ("문", [("Walls/정의문", "JusticeGate", False)])]),
    ("판_해왕류", "SEA KING", []),
    ("판_바위", "ROCKS", [
        ("작은 돌", [("Nature/Rocks/둥근강돌_01", "RiverStone_01", False), ("Nature/Rocks/둥근강돌_02", "RiverStone_02", False),
                  ("Nature/Rocks/바위_01", "Rock_01", False), ("Nature/Rocks/판석_01", "Slab_01", False),
                  ("Nature/Rocks/판석_02", "Slab_02", False), ("Nature/Rocks/바위_02", "Rock_02", False),
                  ("Nature/Rocks/자갈무리_01", "Gravel_01", False), ("Nature/Rocks/바위_03", "Rock_03", False)]),
        ("중간 바위", [("Nature/Rocks/이끼바위_01", "MossRock_01", False), ("Nature/Rocks/해안바위_01", "CoastRock_01", False),
                   ("Nature/Rocks/바위_04", "Rock_04", False), ("Nature/Rocks/이끼바위_02", "MossRock_02", False),
                   ("Nature/Rocks/뾰족바위_01", "Spire_01", False), ("Nature/Rocks/해안바위_02", "CoastRock_02", False)]),
        ("큰 바위", [("Nature/Rocks/바위무리_01", "Rock_Cluster_01", False), ("Nature/Rocks/바위_05", "Rock_05", False),
                  ("Nature/Rocks/바위무리_02", "Rock_Cluster_02", False), ("Nature/Rocks/뾰족바위_02", "Spire_02", False),
                  ("Nature/Rocks/암벽조각_01", "Cliff_01", False), ("Nature/Rocks/암벽조각_02", "Cliff_02", False)])]),
    ("판_나무", "TREES", [
        ("작은 나무", [("Nature/Trees/그루터기_01", "Stump_01", False), ("Nature/Trees/쓰러진통나무_01", "FallenLog_01", False),
                   ("Nature/Trees/어린나무_01", "Sapling_01", False), ("Nature/Trees/야자수_03", "Palm_03", False),
                   ("Nature/Trees/죽은나무_01", "DeadTree_01", False), ("Nature/Trees/활엽수_01", "Broadleaf_01", False),
                   ("Nature/Trees/벚나무_01", "Cherry_01", False)]),
        ("중간 나무", [("Nature/Trees/활엽수_가을", "Broadleaf_Autumn", False), ("Nature/Trees/버드나무_01", "Willow_01", False),
                   ("Nature/Trees/단풍나무_01", "Maple_01", False), ("Nature/Trees/자작나무_01", "Birch_01", False),
                   ("Nature/Trees/소나무_01", "Pine_01", False), ("Nature/Trees/야자수_01", "Palm_01", False),
                   ("Nature/Trees/야자수_02", "Palm_02", False)]),
        ("큰 나무", [("Nature/Trees/침엽수_01", "Conifer_01", False), ("Nature/Trees/은행나무_01", "Ginkgo_01", False),
                  ("Nature/Trees/가문비_01", "Spruce_01", False), ("Nature/Trees/참나무_01", "Oak_01", False),
                  ("Nature/Trees/침엽수_02", "Conifer_02", False), ("Nature/Trees/전나무_01", "Fir_01", False)])]),
]
# 바둑판: 앞줄에 작은 판들, 뒷줄에 큰 판들(뒷줄 판을 나중에 뒤로 늘려도 안 부딪힌다).
GRID = [["판_풀", "판_벽", "판_문", "판_해왕류"], ["판_바위", "판_나무"]]
EMPTY_BOARD = {"판_해왕류": (28.0, 24.0)}        # 자리만 비워 두는 판의 가로×앞뒤(m)


def show_all(extra=None):
    """extra = {"판_해왕류": 함수(컬렉션) → [(줄 이름, [무리, ...]), ...]} — 창에서 바로 짓는 것을 그 판에 올릴 때.
    extra가 있는 판은 FBX 줄 대신 그 결과만 쓴다(내보내기 전 창 확인용). 판 배치·크기·뷰는 똑같이 잡힌다."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import showcase
    importlib.reload(showcase)                  # 창에서 여러 번 돌리므로 고친 내용이 바로 반영되게

    for name in OLD_COLLECTIONS:
        showcase.remove_collection(name)

    boards = {}
    for col_name, title, spec in BOARDS:
        col = showcase.collection(col_name)
        rows = []
        for kind, items in ([] if extra and col_name in extra else spec):
            groups = []
            for rel, label, tiling in items:
                obj = showcase.import_fbx(col, os.path.join(ART, rel + ".fbx"), 1.0 / UNITS_PER_METER)
                obj.name = label
                parts = [(obj, 0.0, 0.0)]
                if tiling:
                    # 이어 붙이는 조각은 세 개를 딱 붙이고, 뒤에 1.5배로 늘린 세 개를 또 붙인다(이음매 확인).
                    single = showcase.group(label, parts)
                    length = single["x_max"] - single["x_min"]
                    depth = single["y_max"] - single["y_min"]
                    for k in (1, 2):
                        twin = obj.copy()
                        col.objects.link(twin)
                        parts.append((twin, length * k, 0.0))
                    for k in range(3):
                        stretched = obj.copy()
                        col.objects.link(stretched)
                        stretched.scale.x = obj.scale.x * 1.5
                        parts.append((stretched, length * 0.25 + length * 1.5 * k, depth + 0.1))
                groups.append(showcase.group(label, parts))
            rows.append((kind, groups))
        if extra and col_name in extra:
            rows = extra[col_name](col)
        width, depth = EMPTY_BOARD.get(col_name, (0.0, 0.0))
        boards[col_name] = showcase.lay_out(col, rows, title=title, min_width=width, min_depth=depth)

    showcase.arrange([[boards[n] for n in row] for row in GRID])
    showcase.frame_all(list(boards.values()))
    return boards
