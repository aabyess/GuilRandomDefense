"""S2 격자 시트: 칸마다 글자(A…), 구버전과 다른 새 스킨은 NEW. /usr/bin/python3 Tools/w3x/skin_grid_sheet_s2.py <items.json> <cells폴더> <출력.png> <제목>"""
import json, os, sys
from PIL import Image, ImageDraw, ImageFont
items, cells, out, title = json.load(open(sys.argv[1])), sys.argv[2], sys.argv[3], sys.argv[4]
chk = {r["png"]: r for r in json.load(open(cells + "/skin_check.json")) if "png" in r} if os.path.exists(cells + "/skin_check.json") else {}
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
def letter(i):
    s = ""; i += 1
    while i: i, r = divmod(i - 1, 26); s = chr(65 + r) + s
    return s
W, Hh, LAB, COLS = 270, 330, 82, 6
n = len(items); rows = (n + COLS - 1) // COLS
sh = Image.new("RGB", (W * COLS, 50 + (Hh + LAB) * rows), (28, 30, 36)); d = ImageDraw.Draw(sh)
d.text((10, 10), title, font=F(22), fill=(255, 235, 170))
for i, it in enumerate(items):
    x, y = (i % COLS) * W, 50 + (i // COLS) * (Hh + LAB); png = f"{i:02d}.png"
    if os.path.exists(f"{cells}/{png}"):
        sh.paste(Image.open(f"{cells}/{png}").convert("RGB").resize((W - 6, Hh - 6)), (x + 3, y + 3))
    else:
        d.rectangle([x + 3, y + 3, x + W - 4, y + Hh - 4], outline=(200, 90, 90)); d.text((x + 20, y + Hh // 2 - 10), "모델 없음/실패", font=F(18), fill=(230, 120, 120)); d.text((x + 20, y + Hh // 2 + 14), it["status"][:20], font=F(12), fill=(200, 150, 150))
    d.rectangle([x + 3, y + 3, x + 3 + 14 * (len(letter(i)) + 1) + 6, y + 28], fill=(0, 0, 0)); d.text((x + 8, y + 4), letter(i), font=F(22), fill=(255, 255, 0))
    if it.get("new"):
        d.rectangle([x + W - 62, y + 3, x + W - 4, y + 28], fill=(210, 40, 40)); d.text((x + W - 56, y + 5), "NEW", font=F(20), fill=(255, 255, 255))
    nm = " / ".join(s.split(" ", 1)[1] if " " in s else s for s in it["names"][:2])
    d.text((x + 6, y + Hh), f"{letter(i)}. {nm}"[:22], font=F(14), fill=(255, 255, 255))
    d.text((x + 6, y + Hh + 18), "(" + ",".join(it["ids"][:3]) + ")  " + it["model"].split("\\")[-1][:20], font=F(11), fill=(170, 190, 215))
    ours = ", ".join(it["ours"]) or "대응 우리 유닛 없음"
    d.text((x + 6, y + Hh + 34), "우리: " + ours[:22], font=F(12), fill=(160, 230, 160) if it["ours"] else (170, 170, 170))
    d.text((x + 6, y + Hh + 50), "구버전 대비: " + it.get("vs", "")[:20], font=F(11), fill=(255, 150, 150) if it.get("new") else (150, 160, 170))
    r = chk.get(png)
    if r: d.text((x + 6, y + Hh + 64), f"메시{r['meshes']} 뼈{r['bones']} 안쪽면{r['inwardFacePct']}%" + (" 텍스처누락" if r["texMissing"] else "") + (f" 숨김{r['hiddenByAlpha']}" if r.get("hiddenByAlpha") else ""), font=F(11), fill=(230, 200, 120) if (r["texMissing"] or r["inwardFacePct"] > 40) else (150, 160, 170))
sh.save(out); print(out, sh.size)
