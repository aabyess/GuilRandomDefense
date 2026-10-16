"""대체 폭발 조정 28건(10-14, blender, PM 지시): auto/의 대체 SuperBigExplosion(수명>2초 또는 배율>0.5)을 0.3배·1.2초로.
  /usr/bin/python3 Tools/w3x/scene_tone_28.py [--apply]   기본은 ~/grd/GRD_scenes/tone_28/{before,after}에만 쓰고 전/후 한 장 시트 compare.png 생성.
  --apply: EXCEPT 목록을 뺀 나머지를 auto/에 덮음(원본은 tone_28/before에 있음)."""
import json, os, glob, subprocess, sys
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~"); A = H + "/grd/GRD_scenes/auto"; T = H + "/grd/GRD_scenes/tone_28"
SKIP = {"영원_김정래__tots_R"}
EXCEPT = set(json.load(open(T + "/except.json"))) if os.path.exists(T + "/except.json") else set()
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
hit = lambda e: e.get("op") == "spawn" and e.get("substitute") and e["model"] == "SuperBigExplosion" and (e.get("lifeSec", 0) > 2 or e.get("baseScale", 1) > 0.5)
def peak(d):
    sp = [e for e in d["timeline"] if hit(e)]; best = (0, 0.3)
    for t in sorted({e["t"] + 0.3 for e in sp} | {e["t"] + 1.0 for e in sp}):
        n = sum(1 for e in sp if e["t"] <= t < e["t"] + e.get("lifeSec", 1))
        if n > best[0]: best = (n, t)
    return round(min(best[1], d["durationSec"]), 3)
def one(sid):
    d = json.load(open(f"{A}/{sid}.json")) if not os.path.exists(f"{T}/before/{sid}.json") else json.load(open(f"{T}/before/{sid}.json"))
    json.dump(d, open(f"{T}/before/{sid}.json", "w"), ensure_ascii=False, indent=1)
    a = json.loads(json.dumps(d)); n = 0
    for e in a["timeline"]:
        if hit(e): e["baseScale"] = 0.3; e["lifeSec"] = 1.2; e["baseScaleNote"] = "10-14 대체 폭발 0.3배·1.2초"; n += 1
    a["toneNote"] = f"10-14 겉모습 조정 {n}건(scene_tone_28.py)"
    json.dump(a, open(f"{T}/after/{sid}.json", "w"), ensure_ascii=False, indent=1)
    t = peak(d)
    for k, dd in (("before", "rb"), ("after", "ra")):
        subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_scene.py", "--", f"{T}/{k}/{sid}.json", f"{T}/{dd}", str(t), "800", "245"], capture_output=True)
    return sid, n, t
if __name__ == "__main__":
    ids = []
    for p in sorted(glob.glob(A + "/*.json")):
        sid = os.path.basename(p)[:-5]
        if sid in SKIP: continue
        src = f"{T}/before/{sid}.json" if os.path.exists(f"{T}/before/{sid}.json") else p
        if any(hit(e) for e in json.load(open(src))["timeline"]): ids.append(sid)
    if "--apply" in sys.argv:
        for sid in ids:
            if sid in EXCEPT: continue
            os.replace if False else None
            open(f"{A}/{sid}.json", "w").write(open(f"{T}/after/{sid}.json").read())
        print("apply", len([i for i in ids if i not in EXCEPT]), "except", sorted(EXCEPT)); sys.exit()
    with ThreadPoolExecutor(4) as ex: res = list(ex.map(one, ids))
    W = 300; cols = 2; rows = (len(res) + cols - 1) // cols; cw = W * 2 + 20
    sh = Image.new("RGB", (cw * cols, rows * (W + 26)), (24, 26, 32)); dr = ImageDraw.Draw(sh)
    for i, (sid, n, t) in enumerate(res):
        x, y = (i % cols) * cw, (i // cols) * (W + 26)
        dr.text((x + 4, y + 3), f"{i+1:02d} {sid} · {n}건 · t={t}s   [전 | 후]", font=F(13), fill=(255, 235, 170))
        for j, dd in enumerate(("rb", "ra")):
            f = glob.glob(f"{T}/{dd}/{sid}_*.png")
            if f: sh.paste(Image.open(sorted(f)[0]).convert("RGB").resize((W, W)), (x + j * (W + 10), y + 24))
    sh.save(T + "/compare.png"); print(len(res), "done")
