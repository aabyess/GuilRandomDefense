"""교체 24기 전후 비교 캡처 일괄(2026-10-09, blender): /usr/bin/python3 Tools/w3x/swap_before_after_run.py [로스터 …]
왼쪽 교체 전(git 8c522dbce^의 옛 FBX) | 오른쪽 교체 후(model_fixed Stand). 출력 ~/Desktop/구랜디스킨모음/교체24_전후비교/ (카드 <로스터>.png + 목록_N.png).
옛 FBX는 ~/GRD_swap_before/에 git archive로 풀어 둔다(Assets 안 건드림)."""
import csv, os, subprocess, sys, glob
from PIL import Image, ImageDraw, ImageFont
H = os.path.expanduser("~"); ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = H + "/Desktop/구랜디스킨모음/교체24_전후비교"; TMP = H + "/GRD_swap_before"; os.makedirs(OUT, exist_ok=True); os.makedirs(TMP + "/render", exist_ok=True)
F = lambda n: ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", n, index=0)
OLD_REF = {"초월_김경현_AP": "41ad6a5d0"}   # 시범 반입(cfeac9142)으로 이미 상디가 된 기 → 그 전 판(진타)
rows = list(csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig")))
only = set(sys.argv[1:]); cards = []
for r in rows:
    ro = r["우리 로스터"]
    if only and ro not in only: continue
    name = ro.split("_")[1] + "_" + r["원작 이름"].split()[0]; pk = f"{H}/GRD_skin_swap/{name}"
    if not os.path.isdir(pk):
        c = glob.glob(f"{H}/GRD_skin_swap/{ro.split('_')[1]}_*"); pk = c[0] if c else pk
    old = f"{TMP}/Assets/Art/Units/{ro}"
    if not os.path.isdir(old): subprocess.run(f"cd {ROOT} && git archive {OLD_REF.get(ro, '8c522dbce^')} 'Assets/Art/Units/{ro}' | tar -x -C {TMP}", shell=True)
    pre = f"{TMP}/render/{ro}"
    if not os.path.exists(pre + "_after.png"):
        p = subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/swap_before_after.py", "--", old, pk + "/model_fixed", pk + "/clip_map.json", pre], capture_output=True, text=True)
        if not os.path.exists(pre + "_after.png"): print("실패", ro, p.stdout[-300:], p.stderr[-300:]); continue
    W, Hh = 512, 768
    card = Image.new("RGB", (W * 2, Hh + 70), (40, 42, 50)); d = ImageDraw.Draw(card)
    for i, side in enumerate(("before", "after")):
        im = Image.open(f"{pre}_{side}.png").convert("RGBA"); card.paste(im, (W * i, 70), im)
    d.text((12, 6), ro, font=F(26), fill=(255, 235, 170)); d.text((12, 40), f"지금 스킨: {r['지금 스킨']}  →  원작: {r['원작 이름']} ({r['원작 모델']})", font=F(18), fill=(200, 210, 225))
    d.text((12, 76), "교체 전(기본 자세)", font=F(16), fill=(255, 170, 110)); d.text((W + 12, 76), "교체 후(Stand)", font=F(16), fill=(130, 220, 140))
    d.line([(W, 70), (W, Hh + 70)], fill=(90, 95, 110), width=2)
    card.save(f"{OUT}/{ro}.png"); cards.append((ro, card))
    print("카드", ro)
if not only:
    per = 6
    for k in range(0, len(cards), per):
        grp = cards[k:k + per]; w, h = 512, 419
        sh = Image.new("RGB", (w * 3, h * 2), (24, 26, 32))
        for i, (ro, c) in enumerate(grp): sh.paste(c.resize((w, h)), (w * (i % 3), h * (i // 3)))
        sh.save(f"{OUT}/목록_{k // per + 1}.png")
    print("목록", (len(cards) + per - 1) // per, "장")
