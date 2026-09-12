"""FBX 여러 개를 한 장의 미리보기 그림으로 렌더링한다.

화면 없이 돈다:
    blender --background --factory-startup --python Tools/blender/render_preview.py -- \
        <출력.png> "Assets/Art/Nature/Rocks/*.fbx" "Assets/Art/Nature/Trees/*.fbx"

왜 필요한가 — 생성 스크립트는 화면 없이 돌기 때문에 **만든 쪽이 결과를 못 본다.**
크기·삼각형 수는 숫자로 확인되지만, 「돌이 별 모양으로 뾰족하다」 같은 건 그림을
봐야 안다(2026-09-12 첫 렌더에서 실제로 바위_01이 그랬다). 이 그림을 이미지로 읽으면
세션이 모양까지 스스로 검수할 수 있다.

⚠️ 칸마다 **가장 긴 변을 같은 크기로 맞춘다** — 모양 검수용이다. 실제 크기 비교는
안 된다(자갈과 10m 나무가 같은 크기로 보인다). 크기는 재임포트 수치로 확인한다.
⚠️ 그리는 순서는 인자로 준 순서 그대로다(가로 먼저, 왼쪽 위부터).
"""

import bpy
import glob
import math
import sys
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) < 2:
    raise SystemExit("사용법: ... -- <출력.png> <FBX 패턴> [패턴 ...]")

out_png, patterns = args[0], args[1:]
files = [f for pattern in patterns for f in sorted(glob.glob(pattern))]
if not files:
    raise SystemExit(f"FBX를 하나도 못 찾았습니다: {patterns}")

CELL_X = 2.0     # 칸 가로 간격
CELL_Y = 2.8     # 칸 앞뒤 간격 — 넓게 잡아야 뒷줄의 키 큰 나무가 앞줄을 안 가린다
TILT = 52        # 카메라가 내려다보는 각(도)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

cols = math.ceil(math.sqrt(len(files)))
rows = math.ceil(len(files) / cols)

for index, path in enumerate(files):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]

    # Workbench는 뷰포트 색(diffuse_color)을 그린다. FBX 임포터는 노드 색만 채우므로 옮겨 준다.
    for o in objs:
        for m in o.data.materials:
            if m and m.use_nodes and "Principled BSDF" in m.node_tree.nodes:
                m.diffuse_color = m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value
        o.rotation_euler.z += math.radians(35)     # 정면보다 비스듬해야 입체가 읽힌다

    bpy.context.view_layer.update()
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in corners), min(p.y for p in corners), min(p.z for p in corners)))
    hi = Vector((max(p.x for p in corners), max(p.y for p in corners), max(p.z for p in corners)))
    size = max(hi - lo)
    if size <= 0:
        continue

    scale = 1.0 / size
    col, row = index % cols, index // cols
    target = Vector(((col - (cols - 1) / 2) * CELL_X, ((rows - 1) / 2 - row) * CELL_Y, 0))
    base = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for o in objs:
        o.location = (o.location - base) * scale + target
        o.scale = o.scale * scale

bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.001))
ground = bpy.context.active_object
ground_mat = bpy.data.materials.new("바닥")
ground_mat.diffuse_color = (0.78, 0.80, 0.76, 1.0)
ground.data.materials.append(ground_mat)

theta = math.radians(TILT)
camera_data = bpy.data.cameras.new("미리보기")
camera_data.type = "ORTHO"
camera_data.ortho_scale = max(cols * CELL_X, rows * CELL_Y * math.cos(math.radians(90 - TILT))) + 1.2
camera = bpy.data.objects.new("미리보기", camera_data)
bpy.context.scene.collection.objects.link(camera)
distance = 30
camera.location = (0, -distance * math.sin(theta), distance * math.cos(theta) + 0.4)
camera.rotation_euler = (theta, 0, 0)

scene = bpy.context.scene
scene.camera = camera
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 1400
scene.render.resolution_y = int(1400 * (rows * CELL_Y * 0.8) / (cols * CELL_X)) if rows > 1 else 700
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)

print(f"미리보기 {len(files)}개 → {out_png}")
for index, path in enumerate(files):
    print(f"  {index // cols + 1}행 {index % cols + 1}열  {path}")
