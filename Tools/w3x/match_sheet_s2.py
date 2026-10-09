# -*- coding: utf-8 -*-
"""신작(S2) 등급별 매칭 시트(2026-10-09, blender2). /usr/bin/python3 Tools/w3x/match_sheet_s2.py <등급: 초월|불멸|영원|히든|흔함|안흔함|특별함|희귀함|전설적인|변화됨|랜덤|제한|특수함>
→ ~/Desktop/구랜디스킨모음/매칭_신작_<등급>/{원랜디_신작_<등급>_n.png(글자 A… · 모델 기준 캐릭터 이름 · NEW · 옛 ID 이름 회색), 구랜디_<등급>.png(우리 쪽 번호 · 교체 예정 표시), 목록 csv}
입력: 원랜디_신작_스킨/<NN_등급>/{_items.json,_cells,index.csv}(skin_grade_build_s2.py가 먼저 돌아야 함) · 우리 쪽 칸은 매칭_<등급>/_cells_구랜디(match_sheets2.py가 만든 것; 없으면 먼저 그걸 돌릴 것) · 교체 표시는 ~/GRD_skin_swap/교체목록.csv."""
import csv, glob, json, os, re, sys
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
GRADE = sys.argv[1]; NN = {"흔함": "01", "안흔함": "02", "특별함": "03", "희귀함": "04", "전설적인": "05", "히든": "06", "변화됨": "07", "랜덤": "08", "제한": "09", "특수함": "10", "초월": "11", "불멸": "12", "영원": "13"}[GRADE]
H = os.path.expanduser("~"); R = H + "/Desktop/구랜디스킨모음"; SD = glob.glob(f"{R}/원랜디_신작_스킨/{NN}_*")[0]
OUT = f"{R}/매칭_신작_{ {'전설적인': '전설'}.get(GRADE, GRADE) }"; os.makedirs(OUT, exist_ok=True)
OURS = f"{R}/매칭_{ {'전설적인': '전설'}.get(GRADE, GRADE) }"
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
def letter(i):
    s = ""; i += 1
    while i: i, r = divmod(i - 1, 26); s = chr(65 + r) + s
    return s
items = json.load(open(SD + "/_items.json")); cells = SD + "/_cells"
chk = {r["png"]: r for r in json.load(open(cells + "/skin_check.json")) if "png" in r} if os.path.exists(cells + "/skin_check.json") else {}
swap = {}
for r in csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig")): swap[r["우리 로스터"]] = r["원작 이름"]
def sheets(cdir, n, draw_cell, stem, title, per=30, cols=6, W=300, Hh=360, LAB=112):
    parts = [(a, min(a + per, n)) for a in range(0, n, per)]
    for k, (a, b) in enumerate(parts, 1):
        rws = (b - a + cols - 1) // cols; sh = Image.new("RGB", (W * cols, 70 + (Hh + LAB) * rws), (28, 30, 36)); d = ImageDraw.Draw(sh)
        d.text((12, 14), title + (f"  ({k}/{len(parts)})" if len(parts) > 1 else ""), font=F(30), fill=(255, 235, 170))
        for j, i in enumerate(range(a, b)):
            x, y = (j % cols) * W, 70 + (j // cols) * (Hh + LAB); png = f"{cdir}/{i:02d}.png"
            if os.path.exists(png): sh.paste(Image.open(png).convert("RGB").resize((W - 6, Hh - 6)), (x + 3, y + 3))
            else: d.rectangle([x + 3, y + 3, x + W - 4, y + Hh - 4], outline=(200, 90, 90)); d.text((x + 70, y + Hh // 2 - 14), "모델 없음/실패", font=F(24), fill=(230, 120, 120))
            draw_cell(d, x, y, i, Hh, W)
        out = f"{OUT}/{stem}{'_' + str(k) if len(parts) > 1 else ''}.png"; sh.save(out); print(out, sh.size)
def badge(d, x, y, tg):
    d.ellipse([x + 8, y + 8, x + 78, y + 78], fill=(220, 60, 60)); f = F(44 if len(tg) == 1 else 32); w = d.textlength(tg, font=f); d.text((x + 43 - w / 2, y + 22 if len(tg) == 1 else y + 27), tg, font=f, fill=(255, 255, 255))
def cell_new(d, x, y, i, Hh, W):
    it = items[i]; badge(d, x, y, letter(i))
    if it.get("new"): d.rectangle([x + W - 74, y + 8, x + W - 6, y + 40], fill=(210, 40, 40)); d.text((x + W - 66, y + 10), "NEW", font=F(26), fill=(255, 255, 255))
    d.text((x + 8, y + Hh + 2), it["label"][:14], font=F(26), fill=(255, 255, 255))
    d.text((x + 8, y + Hh + 34), "(" + ",".join(it["ids"][:3]) + ") " + it["model"].split("\\")[-1][:22], font=F(14), fill=(170, 190, 215))
    old = ", ".join((o.split(" ", 1)[1] if " " in o else o) for o in it["oldnames"][:2])
    d.text((x + 8, y + Hh + 56), "(옛 ID 이름) " + old[:28], font=F(12), fill=(125, 125, 130))
    d.text((x + 8, y + Hh + 74), "우리: " + (", ".join(it["ours"]) or "대응 없음"), font=F(13), fill=(160, 230, 160) if it["ours"] else (150, 150, 150))
    r = chk.get(f"{i:02d}.png"); d.text((x + 8, y + Hh + 92), ("구버전 대비: " + it["vs"]) if r else ("실패: " + it["status"][:30]), font=F(12), fill=(255, 150, 150) if it.get("new") else (150, 160, 170))
sheets(cells, len(items), cell_new, f"원랜디_신작_{GRADE}", f"원랜디 최신(S2) {GRADE} — {len(items)}칸 (글자 A~)")
with open(f"{OUT}/원랜디_신작_{GRADE}_목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["글자", "ID", "캐릭터 이름(모델 기준)", "옛 ID 이름", "S2 모델", "NEW", "구버전 대비", "우리 대응", "렌더"])
    for i, it in enumerate(items): w.writerow([letter(i), ",".join(it["ids"]), it["label"], " / ".join(it["oldnames"]), it["model"], "NEW" if it.get("new") else "", it["vs"], ", ".join(it["ours"]), "있음" if f"{i:02d}.png" in chk else it["status"]])
# 우리 쪽: 교체 예정 표시
rows = list(csv.DictReader(open(f"{OURS}/구랜디_{GRADE}_목록.csv", encoding="utf-8-sig"))) if os.path.exists(f"{OURS}/구랜디_{GRADE}_목록.csv") else []
if rows:
    def cell_our(d, x, y, i, Hh, W):
        r = rows[i]; badge(d, x, y, r["번호"]); sw = swap.get(r["로스터"])
        d.text((x + 8, y + Hh + 2), r["이름"][:14], font=F(26), fill=(255, 255, 255)); d.text((x + 8, y + Hh + 34), r["로스터"], font=F(14), fill=(170, 190, 215))
        d.text((x + 8, y + Hh + 56), "별명: " + (r["별명(skinAlias)"].split(",")[0] or "(없음)"), font=F(13), fill=(160, 230, 160)); d.text((x + 8, y + Hh + 74), "원작: " + (r["원작 대응"] or "(대응표 없음)")[:34], font=F(12), fill=(150, 200, 150))
        if sw: d.rectangle([x + 3, y + Hh - 40, x + W - 4, y + Hh - 6], fill=(230, 150, 20)); d.text((x + 10, y + Hh - 38), ("교체→원작 " + " ".join(sw.split()[:2]))[:18], font=F(22), fill=(20, 20, 20))
    sheets(OURS + "/_cells_구랜디", len(rows), cell_our, f"구랜디_{GRADE}", f"우리 구랜디 {GRADE} — {len(rows)}기 (번호 1~, 주황 띠 = 교체 예정)")
else: print("우리 쪽 목록 없음:", OURS)
