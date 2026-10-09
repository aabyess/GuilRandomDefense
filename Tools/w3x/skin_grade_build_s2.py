"""최신 원랜디(S2 2.323) 유닛 스킨을 등급별 폴더로(2026-10-09, blender2; PM 지시). skin_grade_build.py(구버전)의 S2판.
  /usr/bin/python3 Tools/w3x/skin_grade_build_s2.py [등급번호 …]   → ~/Desktop/구랜디스킨모음/원랜디_신작_스킨/<NN_등급>/<ID_이름>/ + 등급마다 격자 png(글자·NEW)·.blend·index.csv
입력: ~/GRD_motion_trial/s2_skin/units.json(s2_units.py) · fbx/(export_mdx_anim_fbx.py 결과, 같은 폴더에 json). 이름은 구버전 같은 ID 이름 대체(「추정」)."""
import csv, glob, json, os, re, shutil, subprocess, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE))
H = os.path.expanduser("~"); S = H + "/GRD_motion_trial/s2_skin"; OUTR = H + "/Desktop/구랜디스킨모음/원랜디_신작_스킨"
GR = [("01", "흔함"), ("02", "안흔함"), ("03", "특별함"), ("04", "희귀함"), ("05", "전설"), ("06", "히든"), ("07", "변화됨"), ("08", "랜덤_다른세계"), ("09", "제한됨"), ("10", "특수함"), ("11", "초월"), ("12", "불멸"), ("13", "영원"), ("14", "신규_등급미상"), ("15", "더미_상점_퀘스트")]
mp = {r["유닛ID"]: r["로스터"] for r in csv.DictReader(open(ROOT + "/Docs/reference/MASTER_UID_ROSTER_MAP.csv", encoding="utf-8-sig"))}
units = json.load(open(S + "/units.json"))
# 등급 14(미분류) 재분류: ①비유닛(도박·강화소·도전과제·더미 등 이름) → 15 ②모델 번호대(!NNNN_) → 같은 번호대 구버전 등급 ③그래도 모르면 14 유지. 나머지 등급은 구버전 같은 ID 이름 그대로(모델 번호대 불일치는 index 「모델 번호대 등급」 칸에 참고로만).
DUMMY = re.compile(r"더미|도박|퇴치|제압|강화소|연구소|도전과제|도움소|묘비|필요|구입|구매|페널티|항해일지|노획|물품 지원|저지|고급 유닛 생성|깃털|확장팩|은행|가능 회수|잠그기|패왕의길|^\?\?\?$|^\(신규\) .*(Skill|skill)")
SERIES = [(1, 9, "01"), (10, 99, "02"), (100, 199, "03"), (200, 299, "04"), (400, 499, "06"), (500, 599, "05"), (600, 699, "09"), (700, 799, "11"), (900, 999, "12"), (1000, 1099, "10"), (1100, 1399, "08")]
def series_grade(m):
    r = re.search(r"!(\d{4})_", m or "")
    if r:
        n = int(r.group(1))
        for lo, hi, g in SERIES:
            if lo <= n <= hi: return g
for u in units:
    u["series"] = series_grade(u["s2Model"]); u["basis"] = "구버전 같은 ID 이름" + ("(신규 ID는 모델 캐릭터 이름 토큰 추정)" if u["gradeEstimated"] else "")
    if u["grade"] == "14":
        if DUMMY.search(u["name"]) or re.search(r"(?i)dummy|runicobject|questionmark|potofgold|bundleoflumber|magicvault|^(tome|blacksmith|powergenerator|arcanesanctum)", u["s2Model"].split("\\")[-1]): u["grade"] = "15"; u["basis"] = "비유닛(이름·모델이 도박·상점·퀘스트·더미)"
        elif u["series"] and u["series"] != "14": u["grade"] = u["series"]; u["basis"] = "모델 번호대(!" + re.search(r"!(\d{4})_", u["s2Model"]).group(1) + ") → 같은 번호대 구버전 등급"
        elif re.search(r"!14\d\d_", u["s2Model"]): u["grade"] = "15"; u["basis"] = "모델 번호대 14xx(스킬 소환수 더미 S-Snake 등)"
def letter(i):
    s = ""; i += 1
    while i: i, r = divmod(i - 1, 26); s = chr(65 + r) + s
    return s
def safe(s): return re.sub(r'[\\/:*?"<>|\[\]\s]+', "_", s)[:40].strip("_")
def tag(f): return re.sub(r"\.md[lx]$", "", f, flags=re.I).replace("\\", "__").replace("/", "__")
def stock(m): return bool(re.match(r"^(units|abilities|doodads|buildings|objects|textures)\\", m.replace("/", "\\"), re.I))
def run(g, name):
    us = sorted([u for u in units if u["grade"] == g], key=lambda u: u["id"])
    if not us: return
    D = f"{OUTR}/{g}_{name}"; os.makedirs(D, exist_ok=True)
    by = {}
    for u in us: by.setdefault((u["s2File"] or u["s2Model"]).lower(), []).append(u)
    items = []
    for key, ks in by.items():
        u0 = ks[0]; fbx = None; status = None
        if u0["s2File"]:
            fp = f"{S}/fbx/{tag(u0['s2File'])}/{tag(u0['s2File'])}.fbx"
            if os.path.exists(fp): fbx = fp
            else: status = "변환 실패(FBX 없음)"
        elif u0["s2Model"] in ("없음.mdl", ""): status = "S2에도 모델 없음(유닛 데이터에 모델 지정 없음)"
        elif stock(u0["s2Model"]): status = "워크3 기본 모델(맵에 없음)"
        else: status = "S2 맵에 모델 파일 없음(Model\\ 외부 폴더 모델 추정)"
        if fbx and re.match(r"(?i)^(war3mapImported\\)?(!?effect|blskill|mission|npc_)", u0["s2Model"]): fbx = None; status = "이펙트·장식 모델이 유닛 모델로 지정됨(캐릭터 몸 아님)"
        st = u0["status"]; new = st.startswith(("NEW", "신규", "S2에만"))
        vs = {"N": "새 모델", "신": "신규 ID", "S": "구버전엔 맵에 없던 모델", "동": "동일(이름만 바뀜)"}.get(st[0], st)
        items.append(dict(model=u0["s2Model"], ids=[k["id"] for k in ks], names=[f"{k['id']} {k['name']}{'(추정)' if k['nameEstimated'] else ''}" for k in ks],
                          ours=sorted({mp[k["id"]] for k in ks if k["id"] in mp}), fbx=fbx, status=status or "변환됨", new=new, vs=vs, old=u0["oldModel"], basis=u0["basis"], series=u0["series"]))
    json.dump(items, open(f"{D}/_items.json", "w"), ensure_ascii=False, indent=1)
    cells = f"{D}/_cells"
    subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/render_skin_grid_s2.py", "--", f"{D}/_items.json", cells], capture_output=True)
    ck = json.load(open(cells + "/skin_check.json")) if os.path.exists(cells + "/skin_check.json") else []
    chk = {r["png"]: r for r in ck if "png" in r}; errs = {r["model"]: r["error"] for r in ck if "error" in r}
    ok = fail = nnew = 0; reasons = {}
    with open(f"{D}/index.csv", "w", encoding="utf-8-sig", newline="") as fh:
        w = csv.writer(fh); w.writerow(["글자", "ID", "이름(구버전 같은 ID, 추정)", "S2 모델", "구버전 모델", "구버전 대비", "NEW", "등급 근거", "모델 번호대 등급(참고)", "우리 대응 로스터", "폴더", "결과", "메시", "뼈", "숨김 지오셋(Stand 알파0)", "텍스처 누락", "안쪽 면 %(참고)", "동작 목록", "사진"])
        for i, it in enumerate(items):
            c = chk.get(f"{i:02d}.png"); seqs = ""
            if it["fbx"]:
                jp = glob.glob(os.path.dirname(it["fbx"]) + "/*.json")
                if jp: seqs = ", ".join(s["name"] for s in json.load(open(jp[0]))["sequences"])
            why = "" if c else ("메시 없음(입자 전용 모델 — 보이는 몸이 없다)" if errs.get(it["model"]) == "메시 없음" else (errs.get(it["model"]) or it["status"]))
            for k, nmfull in zip(it["ids"], it["names"]):
                nm = nmfull.split(" ", 1)[1]; dname = f"{D}/{k}_{safe(nm)}"
                if c:
                    os.makedirs(dname, exist_ok=True); src = os.path.dirname(it["fbx"])
                    for f in os.listdir(src):
                        s_, d_ = f"{src}/{f}", f"{dname}/{f}"
                        if os.path.isdir(s_): shutil.copytree(s_, d_, dirs_exist_ok=True)
                        else: shutil.copy(s_, d_)
                    shutil.copy(f"{cells}/{i:02d}.png", f"{dname}/preview.png"); ok += 1; nnew += it["new"]
                else: fail += 1; reasons[why] = reasons.get(why, 0) + 1
                w.writerow([letter(i), k, nm, it["model"], it["old"] or "", it["vs"], "NEW" if it["new"] else "", it["basis"], it["series"] or "", mp.get(k, ""), os.path.basename(dname) if c else "", "성공" if c else f"실패: {why}",
                            c and c["meshes"] or "", c and c["bones"] or "", c and c.get("hiddenByAlpha", "") or "", c and c["texMissing"] or "", c and c["inwardFacePct"] or "", seqs, f"{os.path.basename(dname)}/preview.png" if c else ""])
    title = f"원랜디 최신(S2 2.323) {name} 스킨 — 모델 {len(items)}종, 유닛 {ok + fail}기 중 성공 {ok} · NEW(성공 중) {nnew}"
    subprocess.run(["/usr/bin/python3", HERE + "/skin_grid_sheet_s2.py", f"{D}/_items.json", cells, f"{D}/{g}_{name}_격자.png", title], capture_output=True)
    subprocess.run(["blender", "-b", "--factory-startup", "--python", ROOT + "/Tools/blender/build_skin_grid_blend_s2.py", "--", f"{D}/_items.json", f"{D}/{g}_{name}_격자.blend"], capture_output=True)
    line = f"{g}_{name}: {ok + fail}기 중 성공 {ok} · 실패 {fail}" + (f" ({', '.join(f'{k} {v}' for k, v in reasons.items())})" if reasons else "") + f" · 구버전 대비 NEW(성공 중) {nnew} · 시트 {D}/{g}_{name}_격자.png"
    print(line, flush=True); open(OUTR + "/_progress.txt", "a").write(line + "\n")
if __name__ == "__main__":
    os.makedirs(OUTR, exist_ok=True)
    want = sys.argv[1:] or [g for g, _ in GR]
    for g, name in GR:
        if g in want: run(g, name)
