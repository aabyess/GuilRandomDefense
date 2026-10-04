"""상점 FBX 여러 채를 한 장 비교 시트로(2026-10-04, 상점 7채 새로 짓기). 뒷면 컬링 켠 Workbench 텍스처 렌더.
  blender -b --factory-startup --python Tools/blender/render_shops_sheet.py -- <출력.png> <fbx…>
줄 둘: 위 = 게임 시점(정면 −Y에서 고도 50°) · 아래 = 45°(앞 오른쪽 위, 고도 30°). 칸마다 같은 배율(가장 큰 건물에 맞춤)로 그려
건물끼리 크기가 비교된다. 파일 이름(상점_ 뗀 것)을 stdout에 순서대로 찍는다.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

CELL = 480
VIEWS = [("game", 0.0, 50.0), ("angle", 45.0, 30.0)]


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def setup(scene):
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.studio_light = "Default" if False else sh.studio_light
    sh.color_type = "TEXTURE"
    sh.show_backface_culling = True
    scene.render.resolution_x = scene.render.resolution_y = CELL
    scene.render.film_transparent = False
    w = bpy.data.worlds.new("w")
    scene.world = w
    w.color = (0.38, 0.52, 0.30)           # 잔디 톤 배경
    scene.view_settings.view_transform = "Standard"
    scene.display_settings.display_device = "sRGB"


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out, files = args[0], args[1:]
    tmp = os.path.join(os.path.dirname(out) or ".", "_sheet_tmp")
    os.makedirs(tmp, exist_ok=True)
    cells = {}
    scale = 0
    for f in files:
        clear()
        bpy.ops.import_scene.fbx(filepath=f)
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        h = max(p.z for p in pts) - min(p.z for p in pts)
        scale = max(scale, h)
    for f in files:
        clear()
        scene = bpy.context.scene
        setup(scene)
        bpy.ops.import_scene.fbx(filepath=f)
        meshes = [o for o in scene.objects if o.type == "MESH"]
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        cx = sum(p.x for p in pts) / len(pts)
        cy = sum(p.y for p in pts) / len(pts)
        zmax = max(p.z for p in pts)
        name = os.path.basename(f).replace("상점_", "").replace(".fbx", "")
        # 땅판(잔디 색)
        bpy.ops.mesh.primitive_plane_add(size=scale * 4, location=(0, 0, -0.001))
        ground = bpy.context.active_object
        gm = bpy.data.materials.new("g")
        gm.diffuse_color = (0.30, 0.5, 0.22, 1)
        ground.data.materials.append(gm)
        cam_d = bpy.data.cameras.new("c")
        cam_d.type = "ORTHO"
        cam_d.ortho_scale = scale * 1.15
        cam = bpy.data.objects.new("c", cam_d)
        scene.collection.objects.link(cam)
        scene.camera = cam
        for tag, az, el in VIEWS:
            a, e = math.radians(az), math.radians(el)
            # 정면 −Y 쪽에서 az만큼 오른쪽(+X)으로 돌려 본다
            d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
            target = Vector((cx, cy, zmax * 0.45))
            cam.location = target + d * scale * 6
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            p = os.path.join(tmp, f"{name}_{tag}.png")
            scene.render.filepath = p
            bpy.ops.render.render(write_still=True)
            cells[(name, tag)] = p
        print("셀", name)
    names = [os.path.basename(f).replace("상점_", "").replace(".fbx", "") for f in files]
    W = CELL * len(names)
    H = CELL * len(VIEWS)
    sheet = np.zeros((H, W, 4), dtype=np.float32)
    for ci, n in enumerate(names):
        for ri, (tag, _, _) in enumerate(VIEWS):
            img = bpy.data.images.load(cells[(n, tag)])
            arr = np.array(img.pixels[:], dtype=np.float32).reshape(CELL, CELL, 4)
            y0 = H - (ri + 1) * CELL
            sheet[y0:y0 + CELL, ci * CELL:(ci + 1) * CELL] = arr
    res = bpy.data.images.new("sheet", W, H)
    res.pixels = sheet.ravel().tolist()
    res.filepath_raw = out
    res.file_format = "PNG"
    res.save()
    print("시트", out, W, H)


main()
