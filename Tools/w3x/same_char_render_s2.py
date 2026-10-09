# -*- coding: utf-8 -*-
"""같은 캐릭터 비교 사진(2026-10-09, blender2). same_char_pairs_s2.py 다음에: /usr/bin/python3 Tools/w3x/same_char_render_s2.py
왼쪽 우리 지금 스킨(Stand 첫 프레임, 같은 카메라·키 정규화) | 오른쪽 S2 스킨(원랜디_신작_스킨/<등급>/_cells 재사용) → 같은캐릭터_신작비교/<로스터>__<S2ID>_<이름>.png + 목록_시트_n.png(확신 낮음은 별도 시트)."""
import csv, json, os, re, subprocess, sys
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE)); H = os.path.expanduser("~")
OUT = H + "/Desktop/구랜디스킨모음/같은캐릭터_신작비교"; TMP = H + "/GRD_motion_trial/s2_skin/same_char"; os.makedirs(TMP, exist_ok=True)
pairs = json.load(open(H + "/GRD_motion_trial/s2_skin/same_char_pairs.json"))
rost = sorted({p["ours"]["roster"] for p in pairs}); idx = {r: i for i, r in enumerate(rost)}
items = [dict(model=r, ids=[], names=[], ours=[], fbx=next(p["ours"]["fbx"] for p in pairs if p["ours"]["roster"] == r), status="") for r in rost]
json.dump(items, open(TMP + "/items.json", "w"), ensure_ascii=False)
if not os.path.exists(TMP + "/cells/skin_check.json") or "--rerender" in sys.argv:
    subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_skin_grid.py", "--", TMP + "/items.json", TMP + "/cells"], capture_output=True)
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
safe = lambda s: re.sub(r'[\\/:*?"<>|\s]+', "_", s)[:30]
W, Hh = 360, 440; made = []
for p in pairs:
    o, s = p["ours"], p["s2"]; a = f"{TMP}/cells/{idx[o['roster']]:02d}.png"
    if not os.path.exists(a) or not os.path.exists(s["cell"]): continue
    im = Image.new("RGB", (W * 2 + 6, Hh + 84), (28, 30, 36)); d = ImageDraw.Draw(im)
    im.paste(Image.open(a).convert("RGB").resize((W, Hh)), (0, 84)); im.paste(Image.open(s["cell"]).convert("RGB").resize((W, Hh)), (W + 6, 84))
    d.text((8, 4), "우리 " + o["roster"], font=F(21), fill=(255, 255, 255)); d.text((8, 34), "지금 스킨: " + re.sub(r"\s+", " ", o["src"])[:34], font=F(13), fill=(170, 190, 215)); d.text((8, 56), ("별명: " + ", ".join(o["alias"][:3]))[:46], font=F(13), fill=(160, 230, 160))
    d.text((W + 14, 4), f"S2 {s['ids'][0]} {s['label']}"[:18], font=F(21), fill=(255, 235, 170)); d.text((W + 14, 34), s["model"].split("\\")[-1][:36] + "  ·  " + s["vs"][:14], font=F(13), fill=(170, 190, 215)); d.text((W + 14, 56), f"일치: {p['why']}"[:46] + f"  확신 {p['conf']}", font=F(13), fill=(255, 150, 150) if p["conf"] == "낮음" else (160, 230, 160))
    fn = f"{o['roster']}__{s['ids'][0]}_{safe(s['label'])}.png"; im.save(f"{OUT}/{fn}"); made.append((p, fn, im))
    p["file"] = fn
def sheet(sel, stem, title, cols=3, per=18):
    for k in range((len(sel) + per - 1) // per):
        ch = sel[k * per:(k + 1) * per]; tw = 560; th = int(tw * ch[0][2].height / ch[0][2].width); rws = (len(ch) + cols - 1) // cols
        sh = Image.new("RGB", (cols * tw, 50 + rws * (th + 22)), (20, 22, 26)); dr = ImageDraw.Draw(sh); dr.text((10, 10), f"{title} ({k + 1}/{(len(sel) + per - 1) // per})", font=F(24), fill=(255, 235, 170))
        for j, (p, fn, im) in enumerate(ch):
            x, y = (j % cols) * tw, 50 + (j // cols) * (th + 22); sh.paste(im.resize((tw - 4, th)), (x + 2, y + 20)); dr.text((x + 4, y + 1), f"{k * per + j + 1}. {p['ours']['roster']} ↔ {p['s2']['label']}", font=F(15), fill=(255, 255, 255))
        sh.save(f"{OUT}/{stem}_{k + 1}.png")
hi = [m for m in made if m[0]["conf"] == "높음"]; lo = [m for m in made if m[0]["conf"] == "낮음"]
for f in os.listdir(OUT):
    if re.match(r"(목록_시트|확신낮음_시트)_\d+\.png", f): os.remove(f"{OUT}/{f}")
sheet(hi, "목록_시트", f"같은 캐릭터 우리 ↔ S2 — 확신 높음 {len(hi)}쌍"); sheet(lo, "확신낮음_시트", f"확신 낮음(다른 캐릭터일 수도) {len(lo)}쌍")
with open(OUT + "/짝목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["우리 로스터", "지금 스킨", "S2 ID", "S2 이름", "S2 모델", "일치 근거", "동일모델 여부", "확신", "S2 구버전 대비", "S2 폴더", "비교 사진"])
    for p in pairs: w.writerow([p["ours"]["roster"], re.sub(r"\s+", " ", p["ours"]["src"])[:90], ",".join(p["s2"]["ids"][:4]), p["s2"]["label"], p["s2"]["model"], p["why"], "동일(바꿀 이유 없음)" if p["same_model"] else "", p["conf"], p["s2"]["vs"], p["s2"]["grade"], p.get("file", "")])
print("pairs", len(pairs), "made", len(made), "높음", len(hi), "낮음", len(lo), "rosters", len(rost), "동일모델", sum(p["same_model"] for p in pairs))
