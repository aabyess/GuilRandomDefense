"""등급별 짝 정하기 시트(2026-10-09): python3 match_sheets2.py <등급이름: 초월> → ~/Desktop/구랜디스킨모음/매칭_<등급>/{구랜디_<등급>[_n].png, 원랜디_<등급>[_n].png, 목록 csv}
우리 로스터 <등급>_*.asset 전원(번호) + 원랜디_구버전_스킨/<NN_등급> 전 칸(글자, 실패 칸은 「모델 없음」). 한 장 최대 20칸."""
import csv, glob, json, os, re, subprocess, sys
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE)); sys.path.insert(0, HERE)
import w3u
GRADE = sys.argv[1]; NN = {"흔함": "01", "안흔함": "02", "특별함": "03", "희귀함": "04", "전설적인": "05", "히든": "06", "변화됨": "07", "랜덤": "08", "제한": "09", "특수함": "10", "초월": "11", "불멸": "12", "영원": "13"}[GRADE]
FOLD = {"01": "01_흔함", "02": "02_안흔함", "03": "03_특별함", "04": "04_희귀함", "05": "05_전설", "06": "06_히든", "07": "07_변화됨", "08": "08_랜덤_다른세계", "09": "09_제한됨", "10": "10_특수함", "11": "11_초월", "12": "12_불멸", "13": "13_영원"}[NN]
H = os.path.expanduser("~"); OUT = H + "/Desktop/구랜디스킨모음/매칭_" + {"전설적인": "전설"}.get(GRADE, GRADE); os.makedirs(OUT, exist_ok=True)
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
cl = lambda x: re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", "", x or "").strip()
def dec(s): return re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), (s or "").strip().strip('"'))
UN = {u["id"]: u["mods"] for u in w3u.parse(ROOT + "/Tools/w3x/원본/풀린것/war3map.w3u")}
corr = {}
for f in ("ORIGINAL_MATCH_NAMES_2026-10-07.tsv", "ORIGINAL_MATCH_NAMES_2026-10-09.tsv"):
    for l in open(f"{ROOT}/Docs/research/{f}", encoding="utf-8-sig").read().split("\n")[1:]:
        c = l.split("\t")
        if len(c) >= 3: corr.setdefault(c[0], []).append(f"{c[1]} {c[2]}")
for r in csv.DictReader(open(ROOT + "/Docs/reference/MASTER_UID_ROSTER_MAP.csv", encoding="utf-8-sig")):
    if r["유닛ID"] in UN: corr.setdefault(r["로스터"], []).append(r["유닛ID"] + " " + cl(UN[r["유닛ID"]].get("unam", "")).split(" - ")[0][:24])
rows = []
for p in sorted(glob.glob(f"{ROOT}/Assets/Data/Units/Roster/{GRADE}_*.asset")):
    t = open(p, encoding="utf8").read(); r = os.path.basename(p)[:-6]
    if "위습" in r: continue
    nm = re.search(r"unitName: (.*)", t); al = re.search(r"skinAlias: (.*)", t); fbx = f"{ROOT}/Assets/Art/Units/{r}/{r}.fbx"
    rows.append(dict(roster=r, name=dec(nm.group(1)) if nm else r, alias=dec(al.group(1)) if al else "", fbx=fbx if os.path.exists(fbx) else None, corr=" · ".join(dict.fromkeys(corr.get(r, [])))[:60]))
items = [dict(model=r["roster"], ids=[], names=[f"- {r['name']}"], ours=[], fbx=r["fbx"], status="FBX 없음") for r in rows]
json.dump(items, open("/tmp/our_items.json", "w"), ensure_ascii=False)
cells = OUT + "/_cells_구랜디"
subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_skin_grid.py", "--", "/tmp/our_items.json", cells], capture_output=True)
def sheets(cdir, labels, subs, subs2, stem, title, tags, per=30, cols=5, W=340, Hh=410, subs3=None):
    subs3 = subs3 or [""] * len(labels)
    n = len(labels); parts = [(a, min(a + per, n)) for a in range(0, n, per)]
    for k, (a, b) in enumerate(parts, 1):
        rws = (b - a + cols - 1) // cols; sh = Image.new("RGB", (W * cols, 70 + (Hh + 104) * rws), (28, 30, 36)); d = ImageDraw.Draw(sh)
        d.text((12, 14), title + (f"  ({k}/{len(parts)})" if len(parts) > 1 else ""), font=F(30), fill=(255, 235, 170))
        for j, i in enumerate(range(a, b)):
            x, y = (j % cols) * W, 70 + (j // cols) * (Hh + 104); png = f"{cdir}/{i:02d}.png"
            if os.path.exists(png): sh.paste(Image.open(png).convert("RGB").resize((W - 6, Hh - 6)), (x + 3, y + 3))
            else: d.rectangle([x + 3, y + 3, x + W - 4, y + Hh - 4], outline=(200, 90, 90)); d.text((x + 80, y + Hh // 2), "모델 없음", font=F(24), fill=(230, 120, 120))
            d.ellipse([x + 8, y + 8, x + 78, y + 78], fill=(220, 60, 60)); tg = tags[i]; f = F(44 if len(tg) == 1 else 32); w = d.textlength(tg, font=f)
            d.text((x + 43 - w / 2, y + 22 if len(tg) == 1 else y + 27), tg, font=f, fill=(255, 255, 255))
            d.text((x + 8, y + Hh + 2), labels[i][:15], font=F(24), fill=(255, 255, 255)); d.text((x + 8, y + Hh + 32), subs[i][:30], font=F(15), fill=(170, 190, 215)); d.text((x + 8, y + Hh + 54), subs2[i][:44], font=F(13), fill=(160, 230, 160)); d.text((x + 8, y + Hh + 76), subs3[i][:36], font=F(13), fill=(150, 200, 150))
        out = f"{OUT}/{stem}{'_' + str(k) if len(parts) > 1 else ''}.png"; sh.save(out); print(out, sh.size)
sheets(cells, [r["name"] for r in rows], [r["roster"] for r in rows], ["별명: " + (r["alias"] or "(없음)") for r in rows], subs3=["원작: " + (r["corr"] or "(대응표 없음)") for r in rows], stem= f"구랜디_{GRADE}", title=f"우리 구랜디 {GRADE} 유닛 — {len(rows)}기 (번호 1~{len(rows)})", tags=[str(i + 1) for i in range(len(rows))])
with open(f"{OUT}/구랜디_{GRADE}_목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["번호", "로스터", "이름", "별명(skinAlias)", "원작 대응", "FBX"]); [w.writerow([i + 1, r["roster"], r["name"], r["alias"], r["corr"], r["fbx"] or ""]) for i, r in enumerate(rows)]
OD = f"{H}/Desktop/구랜디스킨모음/원랜디_구버전_스킨/{FOLD}"; oi = json.load(open(OD + "/_items.json"))
LET = [chr(65 + i) for i in range(26)] + [a + b for a in "AB" for b in "ABCDEFGHIJKLMNOPQRSTUVWXYZ"]
def short(it):
    ns = [cl(x.split(" ", 1)[1]) if " " in x else x for x in it["names"]]; full = next((n for n in ns if n), "")
    base = full.split(" - ")[0]; tk = base.split(); return " ".join(tk[:2]) if tk else base, base
labels = []; subs = []; subs2 = []
for it in oi:
    lab, base = short(it); labels.append(lab); subs.append("(" + ",".join(it["ids"][:3]) + ") " + it["model"][:22]); subs2.append(base[:20] + ("" if it["fbx"] else " · 모델 없음"))
ours_of = lambda it: ", ".join(it["ours"]) or "대응 우리 유닛 없음"
subs3 = ["우리: " + ours_of(it) for it in oi]
sheets(OD + "/_cells", labels, subs, subs2, f"원랜디_{GRADE}", f"원랜디 {GRADE} 유닛 — {len(oi)}칸 (글자 A~)", LET[:len(oi)], per=24, cols=6, subs3=subs3)
with open(f"{OUT}/원랜디_{GRADE}_목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["글자", "원작 ID", "원작 이름", "모델", "우리 대응", "렌더"]); [w.writerow([LET[i], ",".join(it["ids"]), " / ".join(cl(x) for x in it["names"]), it["model"], ", ".join(it["ours"]), "있음" if it["fbx"] else it["status"]]) for i, it in enumerate(oi)]
