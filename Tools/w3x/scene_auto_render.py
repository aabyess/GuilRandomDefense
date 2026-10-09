"""자동 대본 일괄 재현 렌더 + 시트(2026-10-09, blender). /usr/bin/python3 Tools/w3x/scene_auto_render.py [id접두 …]
스냅샷 시각: 스폰 시각들 +0.12초를 고르게(최대 6장). 결과 ~/GRD_scenes/auto_render/<id>_sheet.png (+ <id>_n.png)."""
import json, os, subprocess, sys
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~"); S = os.environ.get("SCENE_OUT", H + "/GRD_scenes/auto"); R = os.environ.get("SCENE_RENDER", H + "/GRD_scenes/auto_render"); os.makedirs(R, exist_ok=True)
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
def times_of(sc):
    ts = sorted({e["t"] for e in sc["timeline"] if e["op"] == "spawn"}); D = sc["durationSec"]
    if not ts: return [0.2]
    pick = ts if len(ts) <= 5 else [ts[int(i * (len(ts) - 1) / 4)] for i in range(5)]
    out = [min(p + 0.12, D) for p in pick]
    out.append(min(max(out) + 0.5, D)); return sorted(set(round(x, 3) for x in out))[:6]
def one(fn):
    sid = fn[:-5]; sc = json.load(open(f"{S}/{fn}")); ts = times_of(sc)
    if not os.path.exists(f"{R}/{sid}_{len(ts) - 1}.png"):
        subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_scene.py", "--", f"{S}/{fn}", R, ",".join(map(str, ts)), "800", "245"], capture_output=True)
    n = len(ts); W = 360; cols = 3
    sh = Image.new("RGB", (W * min(n, cols), 78 + (W + 22) * ((n + cols - 1) // cols)), (24, 26, 32)); d = ImageDraw.Draw(sh)
    d.text((8, 6), f"{sc['title']} — {sc['ourUnit']} · {'대표' if sc.get('primary') else '2차'} · 품질: {sc.get('quality')}", font=F(17), fill=(255, 235, 170))
    d.text((8, 30), f"j {sc['source']['jLine']} {sc['source']['trigger']} · ourSkill: {sc.get('ourSkill') or '(없음)'}", font=F(13), fill=(190, 200, 215))
    if sc.get("substitutions"): d.text((8, 50), "대체: " + ", ".join(sc["substitutions"][:5]), font=F(12), fill=(255, 170, 90))
    for i, t in enumerate(ts):
        f = f"{R}/{sid}_{i}.png"
        if not os.path.exists(f): continue
        im = Image.open(f).convert("RGB").resize((W - 4, W - 4)); x, y = (i % cols) * W, 78 + (i // cols) * (W + 22)
        sh.paste(im, (x + 2, y + 18)); d.text((x + 6, y + 1), f"t = {t:.2f}s", font=F(13), fill=(255, 255, 255))
    sh.save(f"{R}/{sid}_sheet.png"); return sid
if __name__ == "__main__":
    pre = sys.argv[1:]
    fs = sorted(f for f in os.listdir(S) if f.endswith(".json") and (not pre or any(f.startswith(p) for p in pre)))
    with ThreadPoolExecutor(4) as ex: done = list(ex.map(one, fs))
    print(len(done), "시트")
