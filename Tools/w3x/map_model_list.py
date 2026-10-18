"""S2 맵 장식 모델 목록 csv + 격자 시트(2026-10-19, blender). 입력: ~/grd/GRD_s2_map/s2_doodad_models.json(doo×w3d·w3b 해석) · ~/grd/GRD_s2_models/map/_render/_report.json·사진.
  /usr/bin/python3 Tools/w3x/map_model_list.py → ~/grd/GRD_s2_models/map/목록.csv · sheet_map_models.png
열: 모델(MPQ 경로) · 폴더 · 용도(원작 이름·id) · 개수 · 크기(원작 단위 X·Y·Z, 장식 배율 곱 전) · 배율 · 원작 좌표 범위 · 텍스처 어림(맵에 없는 워크3 기본 텍스처)"""
import csv, json, os, re
from PIL import Image, ImageDraw, ImageFont
H = os.path.expanduser("~"); M = H + "/grd/GRD_s2_models/map"
rows = json.load(open(H + "/grd/GRD_s2_map/s2_doodad_models.json")); rep = json.load(open(M + "/_render/_report.json"))
norm = lambda s: s.lower().replace("/", "\\").removesuffix(".mdx").removesuffix(".mdl")
use = {}
for r in rows: use.setdefault(norm(r["file"]), []).append(r)
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
out = []; cells = []
for model, ext, cnt, texs in rep:
    us = use.get(norm(model), [])
    tag = re.sub(r"[\\/]", "__", model[:-4]); approx = sorted({t[1] for t in texs if not os.path.exists(f"{M}/{tag}/Textures/{t[1]}.png") and not os.path.exists(f"{M}/{tag}/Textures/{t[1]}")})
    out.append([model, tag, " / ".join(f"{u['name'].strip()}({u['id']})" for u in us) or "(맵 장식 아님)", sum(u["n"] for u in us),
                "×".join(str(round(e)) for e in ext), " / ".join(f"{u['scale'][0]}~{u['scale'][1]}" for u in us),
                " / ".join(f"x{u['x'][0]}~{u['x'][1]} y{u['y'][0]}~{u['y'][1]}" for u in us), "; ".join(t[1] for t in texs if t[1])[:120]])
    cells.append((model, us, ext))
csv.writer(open(M + "/목록.csv", "w", newline="", encoding="utf-8-sig")).writerows([["모델", "폴더", "용도(원작 이름·id)", "개수", "크기(원작 단위)", "장식 배율", "원작 좌표 범위", "텍스처(참고)"]] + out)
W, Hh, COLS = 420, 380, 5
sh = Image.new("RGB", (W * COLS, 50 + Hh * ((len(cells) + COLS - 1) // COLS)), (28, 30, 36)); d = ImageDraw.Draw(sh)
d.text((10, 10), f"S2 맵 장식 모델 {len(cells)}종 — 앞비스듬히(워크3 +X가 앞)", font=F(22), fill=(255, 235, 170))
for i, (model, us, ext) in enumerate(cells):
    x, y = (i % COLS) * W, 50 + (i // COLS) * Hh
    p = f"{M}/_render/{model[:-4].replace(chr(92), '/')}_앞비스듬히.png"
    if os.path.exists(p): sh.paste(Image.open(p).convert("RGB").resize((W - 8, 280)), (x + 4, y + 4))
    d.text((x + 6, y + 288), model.split("\\")[-1][:40], font=F(15), fill=(255, 255, 255))
    d.text((x + 6, y + 308), (us[0]["name"].strip() + f" ×{sum(u['n'] for u in us)}" if us else "(장식 아님)")[:36], font=F(14), fill=(160, 230, 160))
    d.text((x + 6, y + 328), "크기 " + "×".join(str(round(e)) for e in ext), font=F(12), fill=(170, 190, 215))
sh.save(M + "/sheet_map_models.png"); print("목록", len(out), "· 시트", sh.size)

# ---- 베껴 만든 워크3 기본 모델(gen_s2_stock_decor.py) 시트 ----
import glob, json as _j
gens = sorted(glob.glob(M + "/gen_*/preview.png"))
gens = [g for g in gens if os.path.basename(os.path.dirname(g)) not in ("gen_Egghead", "gen_ArcaneSanctum_Egghead")]
if gens:
    W2, H2, C2 = 420, 380, 5
    sh2 = Image.new("RGB", (W2 * C2, 50 + H2 * ((len(gens) + C2 - 1) // C2)), (28, 30, 36)); d2 = ImageDraw.Draw(sh2)
    d2.text((10, 10), f"워크3 기본 모델 베껴 만든 {len(gens)}종(어림판 — gen_s2_stock_decor.py)", font=F(22), fill=(255, 235, 170))
    for i, g in enumerate(gens):
        x, y = (i % C2) * W2, 50 + (i // C2) * H2; nm = os.path.basename(os.path.dirname(g))[4:]
        sh2.paste(Image.open(g).convert("RGB").resize((W2 - 8, H2 - 60)), (x + 4, y + 4))
        j = _j.load(open(f"{os.path.dirname(g)}/{nm}.json")); d2.text((x + 6, y + H2 - 52), nm, font=F(16), fill=(255, 255, 255))
        d2.text((x + 6, y + H2 - 30), (j.get("note", "") + " " + "×".join(map(str, j["sizeWc3"])))[:44], font=F(12), fill=(160, 230, 160))
    sh2.save(M + "/sheet_stock.png"); print("스톡 시트", len(gens))
