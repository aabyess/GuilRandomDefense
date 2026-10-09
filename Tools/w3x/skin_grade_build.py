"""구버전(ORD11.089) 원랜디 유닛 스킨을 등급별 폴더로(2026-10-09, blender; PM 지시).
  /usr/bin/python3 Tools/w3x/skin_grade_build.py [등급번호 …]   → ~/Desktop/구랜디스킨모음/원랜디_구버전_스킨/<NN_등급>/<원작ID_이름>/(FBX·Textures·json·preview.png) + 등급마다 격자 시트 png·.blend·index.csv
등급 = w3u 이름 끝 토막(「… - 희귀함」) → 키워드; 이름이 모호한 변형(「흔함영웅」 등)은 같은 등급, 대문자 H0xx 초월 변형은 MASTER_UID_ROSTER_MAP의 로스터 접두. 더미·위습·도박·보스(o0xx·e0xx·n0xx)는 제외.
변환본은 15_원랜디스킨(FBX 보존)에서 가져온다 — 워크3 기본 모델(맵에 없음)·메시 없음은 실패 사유로 표기."""
import csv, glob, json, os, re, shutil, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import w3u
H = os.path.expanduser("~"); WH = H + "/Desktop/구랜디스킨모음/15_원랜디스킨"; OUTR = H + "/Desktop/구랜디스킨모음/원랜디_구버전_스킨"
GR = [("01", "흔함"), ("02", "안흔함"), ("03", "특별함"), ("04", "희귀함"), ("05", "전설"), ("06", "히든"), ("07", "변화됨"), ("08", "랜덤_다른세계"), ("09", "제한됨"), ("10", "특수함"), ("11", "초월"), ("12", "불멸"), ("13", "영원")]
clean = lambda s: re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", s or "").strip()
U = {u["id"]: u["mods"] for u in w3u.parse(ROOT + "/Tools/w3x/원본/풀린것/war3map.w3u")}
idx = {r["원작 ID"]: r for r in csv.DictReader(open(WH + "/index.csv", encoding="utf-8-sig"))}
mp = {r["유닛ID"]: r["로스터"] for r in csv.DictReader(open(ROOT + "/Docs/reference/MASTER_UID_ROSTER_MAP.csv", encoding="utf-8-sig"))}
SKIP = re.compile(r"더미|위습|도박|생성|보스|잡몹|실험|묘비|테스트|dummy", re.I)
def grade_of(k, n):
    if k[0] in "eonEON" or "더미" in n or SKIP.search(n) and not re.search(r"흔함|특별|희귀|전설|히든|초월|불멸|영원|제한|변화|특수", n): return None
    t = n.rsplit(" - ", 1)[-1] if " - " in n else n
    if "히든조합" in n or "[히든" in n: return "06"
    for key, g in (("랜덤전용", "08"), ("안흔함", "02"), ("흔함", "01"), ("특별함", "03"), ("희귀함", "04"), ("전설", "05"), ("히든", "06"), ("변화된", "07"), ("변화됨", "07"), ("제한됨", "09"), ("특수함", "10"), ("초월", "11"), ("불멸", "12"), ("영원", "13")):
        if key in t: return g
    r = mp.get(k, "")
    for pre, g in (("흔함_", "01"), ("안흔함_", "02"), ("특별함_", "03"), ("희귀함_", "04"), ("전설", "05"), ("히든_", "06"), ("변화됨_", "07"), ("랜덤_", "08"), ("다른세계_", "08"), ("제한_", "09"), ("특수함_", "10"), ("초월_", "11"), ("불멸_", "12"), ("영원_", "13")):
        if r.startswith(pre): return g
    return None
UNITS = {g: [] for g, _ in GR}
for k, v in U.items():
    n = clean(v.get("unam", ""))
    if not n or not v.get("umdl") and not n: continue
    g = grade_of(k, n)
    if g: UNITS[g].append(k)
def safe(s): return re.sub(r'[\\/:*?"<>|\[\]]', "_", s)[:40].strip()
def run(g, name):
    ids = sorted(UNITS[g]); D = f"{OUTR}/{g}_{name}"; os.makedirs(D, exist_ok=True)
    by = {}   # 모델 → 한 칸
    for k in ids:
        mdl = (U[k].get("umdl") or "").lower(); by.setdefault(mdl, []).append(k)
    items = []; rows = []
    for mdl, ks in by.items():
        r = next((idx[k] for k in ks if k in idx), None); fbx = None
        if r:
            fd = f"{WH}/{r['폴더']}"; f = [x for x in os.listdir(fd) if x.endswith(".fbx")]; fbx = f"{fd}/{f[0]}" if f else None
        stock = bool(re.search(r"^(units|abilities|doodads|buildings|objects)\\", mdl.replace("/", "\\"), re.I))
        items.append(dict(model=mdl or "(없음)", ids=ks, names=[f"{k} {clean(U[k].get('unam', ''))}" for k in ks], ours=sorted({mp[k] for k in ks if k in mp}), folder=r["폴더"] if r else None, fbx=fbx,
                          status=("워크3 기본 모델(맵에 없음)" if stock else ("모델 없음" if not mdl else ("원본 맵에 모델 파일 없음(Model\\ 외부 폴더 모델 추정)" if not r else r["변환"])))))
    json.dump(items, open(f"{D}/_items.json", "w"), ensure_ascii=False, indent=1)
    cells = f"{D}/_cells"
    subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_skin_grid.py", "--", f"{D}/_items.json", cells], capture_output=True)
    chk = {r["png"]: r for r in json.load(open(cells + "/skin_check.json")) if "png" in r} if os.path.exists(cells + "/skin_check.json") else {}
    errs = {r["model"]: r["error"] for r in json.load(open(cells + "/skin_check.json")) if "error" in r} if os.path.exists(cells + "/skin_check.json") else {}
    ok = fail = 0; reasons = {}
    with open(f"{D}/index.csv", "w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh); w.writerow(["원작 ID", "원작 이름", "모델", "우리 대응 로스터", "폴더", "결과", "메시", "뼈", "숨김 지오셋(Stand 알파0)", "텍스처 누락", "안쪽 면 %(참고)", "동작 목록(시퀀스)", "사진"])
        for i, it in enumerate(items):
            c = chk.get(f"{i:02d}.png"); seqs = ""
            if it["folder"]:
                jp = [x for x in glob.glob(f"{WH}/{it['folder']}/*.json")]
                if jp: seqs = ", ".join(s["name"] for s in json.load(open(jp[0]))["sequences"])
            for k in it["ids"]:
                nm = clean(U[k].get("unam", "")); dname = f"{D}/{k}_{safe(nm)}"
                why = ("메시 없음(입자 전용 모델 — 보이는 몸이 없다)" if errs.get(it["model"]) == "메시 없음" else (errs.get(it["model"]) or it["status"]))
                res = "성공" if c else f"실패: {why}"
                if c:
                    os.makedirs(dname, exist_ok=True)
                    src = f"{WH}/{it['folder']}"
                    for f in os.listdir(src):
                        s_, d_ = f"{src}/{f}", f"{dname}/{f}"
                        if os.path.isdir(s_): shutil.copytree(s_, d_, dirs_exist_ok=True)
                        elif f != "preview.png": shutil.copy(s_, d_)
                    shutil.copy(f"{cells}/{i:02d}.png", f"{dname}/preview.png"); ok += 1
                else: fail += 1; reasons[why] = reasons.get(why, 0) + 1
                w.writerow([k, nm, it["model"], mp.get(k, ""), it["folder"] or "", res, c and c["meshes"] or "", c and c["bones"] or "", c and c.get("hiddenByAlpha", "") or "", c and c["texMissing"] or "", c and c["inwardFacePct"] or "", seqs, f"{k}_{safe(nm)}/preview.png" if c else ""])
    title = f"원랜디 구버전 {name} 스킨 — {ok + fail}기 중 성공 {ok}"
    items_ok = items
    subprocess.run(["/usr/bin/python3", HERE + "/skin_grid_sheet.py", f"{D}/_items.json", cells, f"{D}/{g}_{name}_격자.png", title], capture_output=True)
    subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/build_skin_grid_blend.py", "--", f"{D}/_items.json", f"{D}/{g}_{name}_격자.blend"], capture_output=True)
    line = f"{g}_{name}: {ok + fail}기 중 성공 {ok} · 실패 {fail}" + (f" ({', '.join(f'{k} {v}' for k, v in reasons.items())})" if reasons else "") + f" · 시트 {D}/{g}_{name}_격자.png"
    print(line, flush=True); open(OUTR + "/_progress.txt", "a").write(line + "\n")
if __name__ == "__main__":
    want = sys.argv[1:] or [g for g, _ in GR]
    for g, name in GR:
        if g in want: run(g, name)
