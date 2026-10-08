"""배정 시트(유닛별 한 줄): 스킬 이름 밑에 배정 모델 실렌더. 입력 ~/GRD_orig_vfx_trial/assignment.csv + /tmp/ra/r_<모델>_<0-3>.png(render_fbx_real.py)."""
import csv, os, textwrap
import numpy as np
from PIL import Image, ImageDraw, ImageFont
H = os.path.expanduser("~"); T = H + "/GRD_orig_vfx_trial"; OUT = T + "/assignment_sheets"; os.makedirs(OUT, exist_ok=True)
F = lambda s: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", s, index=0)
f14, f12, f18 = F(14), F(12), F(18)
rows = list(csv.DictReader(open(T + "/assignment.csv", encoding="utf-8-sig")))
def best(m):
    c = []
    for i in range(6):
        p = f"/tmp/ra/r_{m}_{i}.png"
        if os.path.exists(p):
            a = np.array(Image.open(p).convert("RGB")).astype(int); c.append(((np.abs(a - a[0, 0]).sum(2) > 30).mean(), p))
    return Image.open(max(c)[1]).convert("RGB") if c else None
CONF = {"강한짝": (60, 120, 200), "없음": (110, 110, 120), "상": (60, 160, 90), "중": (200, 160, 40), "하": (190, 70, 70)}
W, IMG, HEAD, FOOT = 270, 270, 78, 62
for u in sorted({r["초월 유닛"] for r in rows}):
    rs = [r for r in rows if r["초월 유닛"] == u]
    sh = Image.new("RGB", (W * len(rs), 40 + HEAD + IMG + FOOT), (30, 32, 38)); d = ImageDraw.Draw(sh)
    d.text((10, 8), f"{u}  — 원작 캐릭터 후보 안에서 스킬 1 = 모델 1 (PM 초안)", font=f18, fill=(240, 240, 240))
    for i, r in enumerate(rs):
        x = i * W; d.rectangle([x + 2, 40, x + W - 3, 40 + HEAD + IMG + FOOT - 3], outline=(70, 74, 84))
        nm = r["스킬 이름"]
        for k, line in enumerate(textwrap.wrap(nm, 17)[:3]): d.text((x + 8, 44 + k * 18), line, font=f14, fill=(255, 235, 170))
        im = best(r["배정 모델"]) if r["배정 모델"] else None
        if not r["배정 모델"]: d.text((x + 20, 40 + HEAD + 110), "원작 연출 없음\n(상시 패시브·수치형)" if r["확신"] == "없음" else "(후보 없음)", font=f14, fill=(150, 150, 160))
        if im: sh.paste(im.resize((IMG - 10, IMG - 10)), (x + 5, 40 + HEAD))
        d.text((x + 8, 40 + HEAD + IMG), (r["배정 모델"] or "(없음)")[:30] + ("  ⚠중복" if r["중복"] else ""), font=f12, fill=(180, 220, 255))
        d.text((x + 8, 40 + HEAD + IMG + 18), f"스킬 {r['스킬 모양']}", font=f12, fill=(200, 200, 200))
        d.rectangle([x + 8, 40 + HEAD + IMG + 38, x + 82, 40 + HEAD + IMG + 56], fill=CONF[r["확신"]]); d.text((x + 14, 40 + HEAD + IMG + 39), f"확신 {r['확신']}", font=f12, fill=(255, 255, 255))
    sh.save(f"{OUT}/{u}.png")
print(len(os.listdir(OUT)), "sheets")
