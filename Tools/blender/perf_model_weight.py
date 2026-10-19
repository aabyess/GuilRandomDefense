"""Assets 유닛·적·구조물 FBX 무게 표(blender 2026-10-20, 읽기 전용): 삼각형·재질·뼈·텍스처 크기.
  blender -b --factory-startup --python Tools/blender/perf_model_weight.py -- <Assets 폴더> <출력 csv>
대상: Art/Enemies·Units·Structures·Buildings·Characters·Creatures·Props·Walls의 FBX(@ 클립 파일·Textures 제외). 텍스처 = 같은 폴더 Textures/의 그림 전부(최대 변·장수)."""
import bpy, sys, os, csv, glob
A, OUTCSV = sys.argv[sys.argv.index("--") + 1:][:2]
cats = ["Enemies", "Units", "Structures", "Buildings", "Characters", "Creatures", "Props", "Walls"]
files = []
for ct in cats:
    for p in glob.glob(f"{A}/Art/{ct}/**/*.fbx", recursive=True):
        if "@" in os.path.basename(p): continue
        files.append((ct, p))
rows = []
for n, (ct, p) in enumerate(sorted(files)):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try: bpy.ops.import_scene.fbx(filepath=p)
    except Exception as e: rows.append([ct, os.path.relpath(p, A), "ERR", "", "", "", "", "", str(e)[:60]]); continue
    tris = sum(sum(len(poly.vertices) - 2 for poly in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    mats = len({s.material.name for o in bpy.data.objects if o.type == "MESH" for s in o.material_slots if s.material})
    meshes = sum(1 for o in bpy.data.objects if o.type == "MESH"); bones = sum(len(o.data.bones) for o in bpy.data.objects if o.type == "ARMATURE")
    d = os.path.dirname(p); mx = 0; cnt = 0; px = 0
    for tp in glob.glob(d + "/Textures/*.png") + glob.glob(d + "/Textures/*.jpg") + glob.glob(d + "/Textures/*.tga"):
        try:
            im = bpy.data.images.load(tp); w, h = im.size; bpy.data.images.remove(im); mx = max(mx, w, h); cnt += 1; px += w * h
        except Exception: pass
    rows.append([ct, os.path.relpath(p, A), tris, meshes, mats, bones, cnt, mx, round(px / 1e6, 2)])
    if n % 50 == 0: print("scan", n, len(files), flush=True)
rows.sort(key=lambda r: -(r[2] if isinstance(r[2], int) else -1))
with open(OUTCSV, "w", newline="", encoding="utf-8-sig") as f:
    w = csv.writer(f); w.writerow(["분류", "경로", "삼각형", "메시수", "재질수", "뼈수", "텍스처장수", "텍스처최대변", "텍스처메가픽셀합"]); w.writerows(rows)
print("DONE", len(rows))
