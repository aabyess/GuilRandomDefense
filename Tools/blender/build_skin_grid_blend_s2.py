"""흔함 등 스킨 전원을 격자로 세운 .blend(사장님 Blender 창에서 열기용, 2026-10-09). blender -b --factory-startup --python 이 파일 -- items.json 출력.blend
각 유닛은 Stand 첫 프레임 자세·키 정규화 없이 원작 상대 크기(워크3 1=0.01m 그대로), 발밑 글자 = 원작 이름 + 우리 대응."""
import bpy, json, math, os, sys
OUTB = sys.argv[-1]
sys.path.insert(0, os.path.dirname(__file__))
import importlib.util
spec = importlib.util.spec_from_file_location("rsg", os.path.join(os.path.dirname(__file__), "render_skin_grid_s2.py"))
src = open(os.path.join(os.path.dirname(__file__), "render_skin_grid_s2.py"), encoding="utf8").read().split("report = []; grid = []")[0]
sys.argv = ["x", "--", sys.argv[sys.argv.index("--") + 1], "/tmp/_g"]
exec(compile(src, "render_skin_grid", "exec"))
items = [i for i in ITEMS if i.get("fbx")]

OUTBLEND = sys.argv  # noqa
sc = reset(); COLS = 6; GAP = 2.4
font = None
try: font = bpy.data.fonts.load("/System/Library/Fonts/AppleSDGothicNeo.ttc")
except Exception as e: print("font", e)
for n, it in enumerate(items):
    before = set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=it["fbx"])
    new = [o for o in bpy.data.objects if o not in before]
    arm = next((o for o in new if o.type == "ARMATURE"), None); stand_pose(arm); hide_by_geoset_alpha(new, it["fbx"]); bpy.context.view_layer.update()
    root = bpy.data.objects.new(f"unit_{n:02d}", None); sc.collection.objects.link(root)
    for o in new:
        if o.parent is None: o.parent = root
    r, c = divmod(n, COLS); root.location = (c * GAP, -r * GAP, 0)
    nm = " / ".join(s.split(" ", 1)[1] if " " in s else s for s in it["names"][:2]) + "\n(" + ",".join(it["ids"][:3]) + ")\n우리: " + (", ".join(it["ours"]) or "대응 없음")
    cu = bpy.data.curves.new(f"t{n}", "FONT"); cu.body = nm; cu.size = 0.16; cu.align_x = "CENTER"
    if font: cu.font = font
    t = bpy.data.objects.new(f"label_{n:02d}", cu); sc.collection.objects.link(t); t.location = (c * GAP, -r * GAP - 0.35, 0.01); t.rotation_euler = (0, 0, 0)
    mat = bpy.data.materials.new(f"lm{n}"); mat.diffuse_color = (1, 1, 0.6, 1); t.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=OUTB)
print("saved", OUTB, len(items))
