"""사장님 짝 정하기용 번호/글자 시트(2026-10-09): 구랜디 흔함(우리 로스터) 번호 시트 + 원랜디 흔함 글자 시트.
  /usr/bin/python3 Tools/w3x/match_sheets.py → ~/Desktop/구랜디스킨모음/매칭_흔함/{구랜디_흔함.png, 원랜디_흔함.png, 구랜디_흔함_목록.csv, 원랜디_흔함_목록.csv}"""
import csv, glob, json, os, re, subprocess, sys
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~"); OUT = H + "/Desktop/구랜디스킨모음/매칭_흔함"; os.makedirs(OUT, exist_ok=True)
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
def dec(s):
    s = (s or "").strip().strip('"')
    return re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
# ① 우리 흔함
rows = []
for p in sorted(glob.glob(ROOT + "/Assets/Data/Units/Roster/흔함_*.asset")):
    t = open(p, encoding="utf8").read(); r = os.path.basename(p)[:-6]
    nm = re.search(r"unitName: (.*)", t); al = re.search(r"skinAlias: (.*)", t)
    fbx = f"{ROOT}/Assets/Art/Units/{r}/{r}.fbx"
    rows.append(dict(roster=r, name=dec(nm.group(1)) if nm else r, alias=dec(al.group(1)) if al else "", fbx=fbx if os.path.exists(fbx) else None))
items = [dict(model=r["roster"], ids=[], names=[f"- {r['name']}"], ours=[], fbx=r["fbx"], status="FBX 없음") for r in rows]
json.dump(items, open("/tmp/our_common_items.json", "w"), ensure_ascii=False)
cells = OUT + "/_cells_구랜디"
subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_skin_grid.py", "--", "/tmp/our_common_items.json", cells], capture_output=True)
def sheet(cellsdir, labels, subs, out, title, cols, tag_fn, W=340, Hh=410):
    n = len(labels); rws = (n + cols - 1) // cols
    sh = Image.new("RGB", (W * cols, 70 + (Hh + 64) * rws), (28, 30, 36)); d = ImageDraw.Draw(sh)
    d.text((12, 14), title, font=F(30), fill=(255, 235, 170))
    for i in range(n):
        x, y = (i % cols) * W, 70 + (i // cols) * (Hh + 64); png = f"{cellsdir}/{i:02d}.png"
        if os.path.exists(png): sh.paste(Image.open(png).convert("RGB").resize((W - 6, Hh - 6)), (x + 3, y + 3))
        else: d.rectangle([x + 3, y + 3, x + W - 4, y + Hh - 4], outline=(200, 90, 90)); d.text((x + 70, y + Hh // 2), "모델 없음", font=F(24), fill=(230, 120, 120))
        d.ellipse([x + 8, y + 8, x + 78, y + 78], fill=(220, 60, 60)); tg = tag_fn(i); f = F(44 if len(tg) == 1 else 34); w = d.textlength(tg, font=f)
        d.text((x + 43 - w / 2, y + 22 if len(tg) == 1 else y + 26), tg, font=f, fill=(255, 255, 255))
        d.text((x + 8, y + Hh + 4), labels[i][:16], font=F(24), fill=(255, 255, 255)); d.text((x + 8, y + Hh + 36), subs[i][:26], font=F(16), fill=(170, 190, 215))
    sh.save(out); print(out, sh.size)
sheet(cells, [r["name"] for r in rows], [(r["alias"] or "(별명 없음)") + "  · " + r["roster"] for r in rows], OUT + "/구랜디_흔함.png", f"우리 구랜디 흔함 유닛 — {len(rows)}기 (번호 1~{len(rows)})", 5, lambda i: str(i + 1))
with open(OUT + "/구랜디_흔함_목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["번호", "로스터", "이름", "별명(skinAlias)", "FBX"]); [w.writerow([i + 1, r["roster"], r["name"], r["alias"], r["fbx"] or ""]) for i, r in enumerate(rows)]
# ② 원랜디 흔함(01_흔함 시트 31기 그대로 + 글자)
OD = H + "/Desktop/구랜디스킨모음/원랜디_구버전_스킨/01_흔함"
oi = json.load(open(OD + "/_items.json"))
sys.path.insert(0, HERE); import w3u
UN = {u["id"]: u["mods"] for u in w3u.parse(ROOT + "/Tools/w3x/원본/풀린것/war3map.w3u")}
cl = lambda x: re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", x or "").strip()
bym = {}
for k, v in UN.items():
    n = cl(v.get("unam", "")); m = (v.get("umdl") or "").lower()
    if m and n and n != "흔함영웅" and not n.startswith("흔함영웅"): bym.setdefault(m, []).append(n.split(" - ")[0].strip())
for it in oi:                                                        # 「흔함영웅」은 이름이 아니라 등급 표시 — 같은 모델을 쓰는 다른 유닛의 캐릭터 이름을 쓴다
    it["label"] = next((n for n in (cl(x.split(" ", 1)[1]) if " " in x else x for x in it["names"]) if n and not n.startswith("흔함영웅")), None) or ((bym.get(it["model"]) or ["(이름 불명)"])[0] + " (같은 모델 다른 유닛 이름)")
LET = [chr(65 + i) for i in range(26)] + ["AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH"]
DESC = {"칠무해", "초신성", "해군", "해군원수", "해적단", "갱", "하늘섬의", "가짜신", "도력", "전", "어인해적단", "선장", "준장", "CP9", "투명인간", "가드", "흰수염"}
def short(l):
    star = "(같은 모델" in l; base = l.split(" (같은")[0].split(" - ")[0]; tk = base.split()
    out = [tk[0]] if tk else [base]
    if len(tk) > 1 and tk[1] not in DESC and len(tk[0]) + len(tk[1]) < 11 and not tk[1].isdigit(): out.append(tk[1])
    return " ".join(out) + (" *" if star else "")
sheet(OD + "/_cells", [short(it["label"]) for it in oi], ["(" + ",".join(it["ids"][:3]) + ") " + it["model"][:18] + ("" if it["fbx"] else " · 모델 없음") for it in oi], OUT + "/원랜디_흔함.png", f"원랜디 흔함 유닛 — {len(oi)}칸 (글자 A~)   * = 흔함영웅이라 이름이 없어, 같은 모델을 쓰는 다른 유닛 이름으로 표기", 5, lambda i: LET[i])
with open(OUT + "/원랜디_흔함_목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["글자", "원작 ID", "원작 이름", "모델", "우리 대응", "렌더"]); [w.writerow([LET[i], ",".join(it["ids"]), " / ".join(it["names"]), it["model"], ", ".join(it["ours"]), "있음" if it["fbx"] else "모델 없음"]) for i, it in enumerate(oi)]
