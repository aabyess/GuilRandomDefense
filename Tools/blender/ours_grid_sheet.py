"""구랜디 현재 스킨 격자 시트(S2 skin_grid_sheet_s2.py와 같은 칸 270×330·6열·폭 1620·배경). 등급마다 <등급>_격자.png + index.csv.
  /usr/bin/python3 Tools/blender/ours_grid_sheet.py <items.json> <cells폴더(render_ours_grid 출력)> <출력폴더>"""
import csv, json, os, sys
from PIL import Image, ImageDraw, ImageFont
items, cells, out = json.load(open(sys.argv[1])), sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
chk = {r["png"]: r for r in json.load(open(cells + "/skin_check.json")) if "png" in r}
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
ORDER = ["흔함", "안흔함", "특별함", "희귀함", "히든", "전설적인", "변화됨", "제한", "초월", "불멸", "영원", "랜덤", "다른세계", "특수함", "초월위습"]
def letter(i):
    s = ""; i += 1
    while i: i, r = divmod(i - 1, 26); s = chr(65 + r) + s
    return s
W, Hh, LAB, COLS = 270, 330, 96, 6
rows_idx = []
for gi, grade in enumerate(ORDER):
    sel = [(k, it) for k, it in enumerate(items) if it["grade"] == grade]
    if not sel: continue
    n = len(sel); rows = (n + COLS - 1) // COLS
    sh = Image.new("RGB", (W * COLS, 50 + (Hh + LAB) * rows), (28, 30, 36)); d = ImageDraw.Draw(sh)
    d.text((10, 10), f"구랜디 현재 스킨(게임에 들어간 모델) — {grade} {n}기", font=F(22), fill=(255, 235, 170))
    for i, (k, it) in enumerate(sel):
        x, y = (i % COLS) * W, 50 + (i // COLS) * (Hh + LAB); png = f"{k:02d}.png"
        if os.path.exists(f"{cells}/{png}"):
            sh.paste(Image.open(f"{cells}/{png}").convert("RGB").resize((W - 6, Hh - 6)), (x + 3, y + 3))
        else:
            d.rectangle([x + 3, y + 3, x + W - 4, y + Hh - 4], outline=(200, 90, 90)); d.text((x + 20, y + Hh // 2 - 10), "모델 없음", font=F(18), fill=(230, 120, 120))
        L = letter(i)
        d.rectangle([x + 3, y + 3, x + 3 + 14 * (len(L) + 1) + 6, y + 28], fill=(0, 0, 0)); d.text((x + 8, y + 4), L, font=F(22), fill=(255, 255, 0))
        d.text((x + 6, y + Hh), f"{L}. {it['name']}"[:20], font=F(16), fill=(255, 255, 255))
        fb = os.path.basename(it["fbx"]) if it["fbx"] else "(FBX 없음)"
        d.text((x + 6, y + Hh + 18), fb[:30], font=F(11), fill=(170, 190, 215))
        r = chk.get(png)
        if r: d.text((x + 6, y + Hh + 34), f"메시{r['meshes']} 뼈{r['bones']} 안쪽면{r['inwardFacePct']}%" + (" 텍스처누락" if r["texMissing"] else "") + (f" 자세:{r['stand']}"[:28] if r.get("stand") else " 자세:휴식"), font=F(11), fill=(230, 200, 120) if (r["texMissing"] or r["inwardFacePct"] > 40) else (150, 160, 170))
        rows_idx.append([grade, L, it["name"], it["fbx"] or "", r["stand"] if r and r.get("stand") else "", f"{grade}_격자.png"])
    sh.save(f"{out}/{grade}_격자.png"); print(grade, n, sh.size)
with open(out + "/index.csv", "w", newline="", encoding="utf-8-sig") as f:
    w = csv.writer(f); w.writerow(["등급", "칸 글자", "이름", "모델 파일", "자세(액션, 비면 휴식)", "격자 파일"]); w.writerows(rows_idx)
