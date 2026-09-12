"""사장님 Blender 창의 작업물 전시 — 큰 판 위에 종류별로 한 줄씩 늘어놓는다.

사장님 지시(2026-09-12): 「돌은 돌끼리 나무는 나무끼리 문은 문끼리 이쁘게 나열」. 창에 짓는 모든
Blender 작업(gen_walls.py·show_all.py, 앞으로의 gen_*.py)이 이걸 쓴다.

🔴 보여주는 배치일 뿐이다 — 물체의 위치(와 전시용 배율)만 바꾸고 메시는 안 건드리므로, FBX를
내보낼 때의 원점·축과는 무관하다. 내보내기는 각 gen_*.py의 main()이 따로 한다.

쓰는 법(창에서):
    group = showcase.group("StoneWall_Thick", [(obj, 0.0, 0.0), (twin, length, 0.0), ...])
    board = showcase.lay_out(collection, [("돌", [group, ...]), ("나무", [...])])
    showcase.frame(board)

규칙:
- 한 줄에 한 종류. 앞줄(뷰 쪽, −y)부터 늘어놓는다 — 키 큰 것(나무)을 마지막 줄에 두면 앞을 안 가린다.
- 줄 안: 칸 너비 = 그 줄에서 가장 넓은 무리 + 간격. 무리는 칸 가운데 — 같은 간격이고, 크기가
  제각각이어도 서로 안 겹친다. 원점은 줄마다 같은 선(y), 전부 같은 방향(길이 = X축).
- 줄 사이: 앞줄의 뒤끝과 다음 줄 라벨 사이가 늘 같은 간격 — 줄마다 깊이가 달라도 고르다.
- 무리 앞에 영어 라벨(한글은 Blender 기본 폰트에서 깨진다). 판은 사방에 여유를 두고 깐다.
- 마지막에 뷰를 판 전체(키 큰 것 꼭대기까지)에 맞춘다 — 사장님이 키를 안 눌러도 다 보이게.
"""

import math

import bpy
import bmesh
from mathutils import Euler, Vector

BOARD_NAME = "전시판"
BOARD_COLOR = (0.80, 0.80, 0.78, 1.0)
LABEL_COLOR = (0.12, 0.12, 0.12, 1.0)


def _material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = color
    mat.diffuse_color = color          # 뷰포트(Solid)가 읽는 값
    return mat


def clear(collection):
    """컬렉션 안의 물체만 지운다(주인 없는 메시·글자도 같이). 컬렉션 밖은 안 건드린다."""
    datas = {obj.data for obj in collection.objects if obj.data is not None}
    for obj in list(collection.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for data in datas:
        if data.users:
            continue
        if isinstance(data, bpy.types.Mesh):
            bpy.data.meshes.remove(data)
        elif isinstance(data, bpy.types.Curve):
            bpy.data.curves.remove(data)


def import_fbx(collection, path, scale):
    """이미 만들어 둔 FBX를 들여와 전시한다. 들어온 물체는 전부 collection으로 옮긴다.

    scale — 창 단위로 맞추는 배율(FBX는 게임 단위로 읽히므로 미터 장면이면 1/11.4).
    메시가 아니라 물체 배율만 바꾼다 — 비율은 실제 그대로다."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [obj for obj in bpy.data.objects if obj not in before]
    for obj in new:
        for owner in list(obj.users_collection):
            owner.objects.unlink(obj)
        collection.objects.link(obj)
    meshes = [obj for obj in new if obj.type == "MESH"]
    for obj in new:
        if obj.type != "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    obj = meshes[0]
    obj.scale = obj.scale * scale
    return obj


def group(label, parts):
    """한 칸에 함께 서는 물체 무리. parts = [(물체, 무리 안 x, 무리 안 y), ...].

    물체의 배율·회전은 미리 정해 두고 부른다 — 여기서 그걸 반영해 무리의 가로·앞뒤 범위를 잰다.
    무리의 원점 선은 y = 0(본 줄), 뒤로 딸린 것(늘린 복제 등)은 +y 쪽에 둔다."""
    xs, ys = [], []
    for obj, dx, dy in parts:
        basis = obj.matrix_basis.copy()
        basis.translation = Vector((0.0, 0.0, 0.0))
        # 막 만든 물체는 bound_box가 아직 계산 전(전부 0)일 수 있어서 메시면 정점을 직접 읽는다.
        corners = [v.co for v in obj.data.vertices] if obj.type == "MESH" else [Vector(c) for c in obj.bound_box]
        for corner in corners:
            point = basis @ corner
            xs.append(point.x + dx)
            ys.append(point.y + dy)
    return {"label": label, "parts": parts,
            "x_min": min(xs), "x_max": max(xs), "y_min": min(ys), "y_max": max(ys)}


def lay_out(collection, rows, start=(7.0, 0.0), gap=0.6, margin=0.8, label_size=0.22):
    """rows = [(종류, [무리, ...]), ...] — 앞 줄(뷰 쪽, −y)부터. 판을 깔고 라벨을 눕혀 두고, 판 물체를 돌려준다.

    start는 첫 줄의 왼쪽 끝과 원점 선(창 단위). 판은 전시물보다 margin만큼 바깥까지 깐다.
    🔴 뒷줄로 갈수록 +y다 — 첫 창 확인에서 나무 줄을 앞에 두니 7m 나무가 문 줄을 가렸다."""
    label_band = label_size * 2.2                                   # 무리 앞 라벨 자리
    label_mat = _material("라벨", LABEL_COLOR)

    left = start[0]
    right = left
    front_edge = back_edge = None
    for r, (_, row) in enumerate(rows):
        row_front = min(g["y_min"] for g in row)                    # 원점 선 기준(음수 쪽)
        row_back = max(g["y_max"] for g in row)
        if r == 0:
            line_y = start[1]
            front_edge = line_y + row_front - label_band
        else:
            line_y = back_edge + gap + label_band - row_front        # 앞줄 뒤끝 + 간격 + 라벨 자리
        label_y = line_y + row_front - label_band * 0.5             # 라벨은 줄마다 한 선에

        slot = max(g["x_max"] - g["x_min"] for g in row) + gap
        for k, g in enumerate(row):
            center_x = left + slot * (k + 0.5)
            origin_x = center_x - (g["x_min"] + g["x_max"]) / 2      # 무리 가운데를 칸 가운데에
            for obj, dx, dy in g["parts"]:
                obj.location = (origin_x + dx, line_y + dy, 0.0)

            curve = bpy.data.curves.new(g["label"], "FONT")
            curve.body = g["label"]
            curve.size = label_size
            curve.align_x = "CENTER"
            curve.align_y = "CENTER"
            curve.materials.append(label_mat)
            text = bpy.data.objects.new(g["label"], curve)          # 기본이 판에 누운 방향(XY 평면)이다
            collection.objects.link(text)
            text.location = (center_x, label_y, 0.002)

        right = max(right, left + slot * len(row))
        back_edge = line_y + row_back

    x0, x1 = left - margin, right + margin
    y0, y1 = front_edge - margin, back_edge + margin

    # 판은 z를 아주 조금 내린다 — 조각 바닥과 같은 높이면 겹쳐 깜빡인다.
    bm = bmesh.new()
    corners = [bm.verts.new(Vector((x, y, -0.003))) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))]
    bm.faces.new(corners)
    mesh = bpy.data.meshes.new(BOARD_NAME)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(_material(BOARD_NAME, BOARD_COLOR))
    board = bpy.data.objects.new(BOARD_NAME, mesh)
    collection.objects.link(board)
    return board


def frame(board, tilt_degrees=42.0):
    """3D 뷰를 판 전체에 맞춘다 — 사장님이 따로 키를 안 눌러도 판이 한눈에 들어오게.
    정면(−y)에서 내려다봐서 줄이 가로로 반듯하게 보이고 라벨이 읽힌다."""
    bpy.context.view_layer.update()
    points = [board.matrix_world @ v.co for v in board.data.vertices]
    lo = Vector((min(p.x for p in points), min(p.y for p in points), 0.0))
    hi = Vector((max(p.x for p in points), max(p.y for p in points), 0.0))

    # 판 위에서 가장 키 큰 것까지 화면에 들어오게 — 판만 재면 뒷줄 나무 꼭대기와 앞줄 라벨이
    # 화면 밖으로 잘렸다(첫 창 확인). 원근 때문에 앞쪽이 커지므로 거리를 넉넉히 둔다.
    tallest = 0.0
    for obj in board.users_collection[0].objects:
        if obj.type == "MESH" and obj is not board:
            tallest = max(tallest, max((obj.matrix_world @ v.co).z for v in obj.data.vertices))
    size = max(hi.x - lo.x, hi.y - lo.y, tallest)
    center = (lo + hi) / 2
    center.z = tallest * 0.3
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            r3d = area.spaces.active.region_3d
            r3d.view_location = center
            # 0.9 — 사장님 창에서 잰 값: 1.8이면 58m 판이 화면 가로의 40%만 찼다(뷰포트 화각이 넓다).
            r3d.view_distance = size * 0.9
            r3d.view_rotation = Euler((math.radians(tilt_degrees), 0.0, 0.0)).to_quaternion()
            area.tag_redraw()
