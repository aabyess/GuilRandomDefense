"""scene render 스냅샷 → 한 장 시트(시간 라벨·제목·원작 정보). /usr/bin/python3 Tools/w3x/scene_sheet.py"""
import json, os
from PIL import Image, ImageDraw, ImageFont
import subprocess
H = os.path.expanduser("~"); R = H + "/GRD_scenes/render"; ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
def times_for(sid):
    ev = json.load(open(f"{H}/GRD_scenes/scripts/{sid}.json"))["timeline"]
    if sid == "dragon_storm":
        L = sorted(e["t"] for e in ev if e.get("id", "").startswith("L") and e["op"] == "spawn"); return [0.1, L[1] + 0.08, L[4] + 0.08, L[7] + 0.08, L[10] + 0.1, L[10] + 0.6]
    return None
GEO = {"shanks_haki": (950, 480), "enel_eltor": (700, 245), "dragon_storm": (700, 120), "shiki_fleet": (750, 80)}
F = lambda s: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", s, index=0)
CFG = {"shanks_haki": [2.55, 2.75, 3.0, 3.4, 3.9], "enel_eltor": [0.3, 0.9, 1.3, 1.45, 1.75, 2.3], "dragon_storm": [0.1, 0.6, 1.1, 1.7, 2.4, 3.1], "shiki_fleet": [0.1, 0.45, 0.9, 1.4, 2.0, 2.5]}
for sid, ts in CFG.items():
    ts = times_for(sid) or ts
    if os.environ.get("RENDER", "1") == "1":
        subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_scene.py", "--", f"{H}/GRD_scenes/scripts/{sid}.json", R, ",".join(f"{x:.3f}" for x in ts), str(GEO[sid][0]), str(GEO[sid][1])], capture_output=True)
    sc = json.load(open(f"{H}/GRD_scenes/scripts/{sid}.json")); n = len(ts); W = 420
    sh = Image.new("RGB", (W * min(n, 3), 70 + (W + 26) * ((n + 2) // 3)), (24, 26, 32)); d = ImageDraw.Draw(sh)
    d.text((10, 8), f"{sc['title']} — 원작 재현(Blender, 입자는 추정)", font=F(20), fill=(255, 235, 170))
    d.text((10, 38), f"{sc['ourUnit']} ← {sc['origUnit']} · {sc['source']['trigger']} j {sc['source']['jLine']}", font=F(14), fill=(190, 200, 215))
    miss = [m for m, b in sc["models"].items() if not b.get("folder")]
    for i, t in enumerate(ts):
        im = Image.open(f"{R}/{sid}_{i}.png").convert("RGB").resize((W - 6, W - 6)); x, y = (i % 3) * W, 70 + (i // 3) * (W + 26)
        sh.paste(im, (x + 3, y + 22)); d.text((x + 8, y + 2), f"t = {t:.2f}s", font=F(15), fill=(255, 255, 255))
    if miss: d.text((10, sh.height - 20), "※ 자리표시자(워크3 기본 모델, 맵에 없음): " + ", ".join(miss), font=F(13), fill=(255, 170, 90))
    sh.save(f"{R}/{sid}_sheet.png")
print("ok")
