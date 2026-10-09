# -*- coding: utf-8 -*-
"""우리 구랜디 유닛 ↔ 신작(S2) 원랜디 같은 캐릭터 짝 찾기(2026-10-09, blender2; PM 지시). /usr/bin/python3 Tools/w3x/same_char_pairs_s2.py
→ ~/Desktop/구랜디스킨모음/같은캐릭터_신작비교/짝목록.csv + ~/GRD_motion_trial/s2_skin/same_char_pairs.json
우리 쪽: Assets/Data/Units/Roster/*.asset의 skinAlias·unitName + Assets/Art/Units/<로스터>/SOURCE.txt 앞 3줄 + Docs/research/SKIN_GLB_LICENSES.csv 제목.
S2 쪽: 원랜디_신작_스킨/<NN_등급>/_items.json(캐릭터 이름 label·모델 토큰·옛 ID 이름). 교체목록.csv에 이미 있는 로스터는 뺀다. 등급은 달라도 된다.
확신: 높음 = S2 한글 이름이 별명 항목과 같거나(공백·대소문자 무시) 3글자 이상 이름·4글자 이상 영문 토큰이 우리 쪽 글에 낱말로 있음. 낮음 = 그 밖(2글자 이름·옛 ID 이름만 맞음 등)."""
import csv, glob, json, os, re
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))); H = os.path.expanduser("~")
R = H + "/Desktop/구랜디스킨모음"; OUT = R + "/같은캐릭터_신작비교"; os.makedirs(OUT, exist_ok=True)
dec = lambda s: re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), (s or "").strip().strip('"'))
norm = lambda s: re.sub(r"[\s\-_.·'’\"()\[\]]+", "", (s or "").casefold())
swap = {r["우리 로스터"] for r in csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig"))}
lic = {}
for r in csv.DictReader(open(ROOT + "/Docs/research/SKIN_GLB_LICENSES.csv", encoding="utf-8-sig")):
    m = re.search(r"/([^/]+?)(?:\.glb|\.zip)?(?:/|$)", r["파일"]); k = re.sub(r"\.(glb|zip)$", "", r["파일"].split("/")[-1])
    lic.setdefault(k.split("/")[0], []).append(r.get("title", ""))
ours = []
for p in sorted(glob.glob(ROOT + "/Assets/Data/Units/Roster/*.asset")):
    r = os.path.basename(p)[:-6]
    if "위습" in r: continue
    t = open(p, encoding="utf8").read(); nm = re.search(r"unitName: (.*)", t); al = re.search(r"skinAlias: (.*)", t)
    fbx = f"{ROOT}/Assets/Art/Units/{r}/{r}.fbx"
    if not os.path.exists(fbx): continue
    sp = f"{ROOT}/Assets/Art/Units/{r}/SOURCE.txt"; src = " ".join(open(sp, encoding="utf8").read().split("\n")[:3]) if os.path.exists(sp) else ""
    alias = [a.strip() for a in dec(al.group(1) if al else "").split(",") if a.strip()]
    ours.append(dict(roster=r, name=dec(nm.group(1)) if nm else r, alias=alias, src=src, lic=" ".join(lic.get(r, [])), fbx=fbx, swap=r in swap, mdl_orig=bool(re.search(r"원작 모델 .*\.fbx|15_원랜디스킨", src))))
s2 = []
for d in sorted(glob.glob(R + "/원랜디_신작_스킨/[0-9][0-9]_*")):
    try: its = json.load(open(d + "/_items.json"))
    except Exception: continue
    ck = {x["png"]: x for x in json.load(open(d + "/_cells/skin_check.json")) if "png" in x} if os.path.exists(d + "/_cells/skin_check.json") else {}
    for i, it in enumerate(its):
        if f"{i:02d}.png" not in ck: continue
        m = re.search(r"!\d{4}_([A-Za-z0-9\-]+)", it["model"]); tok = m.group(1).lower() if m else ""
        s2.append(dict(grade=os.path.basename(d), idx=i, ids=it["ids"], label=it["label"], base=re.sub(r"\s*\(.*\)$", "", it["label"]), tok=tok, old=" ".join(it["oldnames"]), model=it["model"], vs=it["vs"], cell=f"{d}/_cells/{i:02d}.png", fbx=it["fbx"]))
SERIES = {"나루토", "드래곤", "킹", "dp", "leo", "해군", "나미", "루피"}   # 작품·흔한 이름: 별명에 정확히 있지 않으면 낮음
GENERIC = {"해군"}
SYN = {"테조로": ["테소로"], "징베": ["진베"], "버지스": ["버제스"], "죠즈": ["조즈"], "모몬가": ["아인즈"], "압살롬": ["압살롬"]}
NONOP = {"고죠 사토루", "나루토", "이치고", "죠타로", "메구밍", "뱌쿠야", "야가미 라이토", "네즈코", "타츠마키", "브로냐", "아냐", "샌즈", "미나토", "콘파쿠", "x-드레이크"}
NONOP_RE = re.compile(r"헌터|hxh|블리치|bleach|주술|죠죠|jojo|드래곤볼|dragon ?ball|원펀맨|one ?punch|나루토|naruto|바키|baki|체인소|동방|오버로드|나의 히어로|단간|유희왕|진격|일곱 개의|칠대죄", re.I)
OP_RE = re.compile(r"원피스|one[- _]?piece|바운티러시|bounty|pl_[a-z]+_", re.I)
def words(t): return re.findall(r"[가-힣]+|[a-z0-9]+", t.casefold().replace("_", " "))
def has(ws, label):
    if len(label) <= 2: return label in ws
    return any(w == label or (w.startswith(label) and len(w) - len(label) <= 1) for w in ws)
pairs = []
for o in ours:
    if o["swap"]: continue
    ents = {norm(a) for a in o["alias"]}; aw = words(" ".join(o["alias"])); head = words(o["name"] + " " + o["src"][:70]); full = words(o["src"] + " " + o["lic"])
    for s in s2:
        b = norm(s["base"]); lab = s["base"].casefold(); tk = s["tok"]; why = conf = None
        if lab in GENERIC: continue
        var = re.search(r"\((.+)\)$", s["label"]); var = var.group(1).casefold() if var else ""
        syn = [lab] + SYN.get(lab, [])
        if any(norm(x) in ents for x in syn[1:]): why, conf = f"별명 철자 다른 「{s['base']}」", "높음"
        elif len(var) >= 3 and (has(aw, var) or var in aw) and has(aw + head + full, lab.split()[0]): why, conf = f"별명에 변형 「{var}」(+{s['base']})", "높음"
        elif b in ents: why, conf = f"별명 「{s['base']}」 일치", "높음"
        elif re.search("[가-힣]", lab) and any(has(aw, x) for x in syn): why, conf = f"별명 낱말 「{s['base']}」", "낮음" if lab in SERIES else "높음"
        elif re.search("[가-힣]", lab) and has(head, lab): why, conf = f"출처 앞머리에 「{s['base']}」", "낮음" if lab in SERIES else "높음"
        elif re.search("[가-힣]", lab) and has(full, lab): why, conf = f"출처 뒤쪽에 「{s['base']}」", "낮음"
        elif len(tk) >= 4 and (tk in aw or tk in head): why, conf = f"영문 「{tk}」 일치", "높음"
        elif len(tk) >= 4 and tk in full: why, conf = f"출처 뒤쪽에 영문 「{tk}」", "낮음"
        elif len(tk) >= 4 and any(w.startswith(tk[:5]) for w in aw + head if len(w) >= 5): why, conf = f"영문 비슷 「{tk}」", "낮음"
        if why and conf == "높음" and b not in ents:
            nonop_s2 = lab in NONOP; our_nonop = bool(NONOP_RE.search(o["name"] + " " + o["src"] + " " + " ".join(o["alias"]))); our_op = bool(OP_RE.search(o["src"] + " " + o["lic"] + " " + " ".join(o["alias"])))
            if (not nonop_s2 and our_nonop and not our_op) or (nonop_s2 and our_op and not our_nonop): conf, why = "낮음", why + " · 작품이 다른 듯(우리 쪽 출처와)"
        if why and conf == "높음" and o["alias"] and not why.startswith("별명"): conf, why = "낮음", why + " · 별명엔 없음"
        if why: pairs.append(dict(ours=o, s2=s, why=why, conf=conf))
for p in pairs: p["same_model"] = bool(p["ours"]["mdl_orig"] and p["s2"]["vs"].startswith("동일"))
json.dump(pairs, open(H + "/GRD_motion_trial/s2_skin/same_char_pairs.json", "w"), ensure_ascii=False, indent=1)
with open(OUT + "/짝목록.csv", "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f); w.writerow(["우리 로스터", "지금 스킨", "S2 ID", "S2 이름", "S2 모델", "일치 근거", "동일모델 여부", "확신", "S2 구버전 대비", "S2 폴더"])
    for p in pairs: w.writerow([p["ours"]["roster"], p["ours"]["src"][:80], ",".join(p["s2"]["ids"][:4]), p["s2"]["label"], p["s2"]["model"], p["why"], "동일(바꿀 이유 없음)" if p["same_model"] else "", p["conf"], p["s2"]["vs"], p["s2"]["grade"]])
print(len(pairs), len({p["ours"]["roster"] for p in pairs}), sum(p["conf"] == "낮음" for p in pairs), sum(p["same_model"] for p in pairs), "우리", len(ours), "S2", len(s2))
