"""하위 등급(특별함·희귀함·전설·히든·변화됨·랜덤·제한) 스킬 → 원작 이펙트 모델 후보(2026-10-09, blender).

  /usr/bin/python3 Tools/w3x/lower_grade_vfx.py cand     # 1단계: SKILL_VFX_MAPPING.csv 행마다 원작 트리거·능력의 모델을 모아 lower_candidates.json 작성 + 맵 안 모델 복사
  /usr/bin/python3 Tools/w3x/lower_grade_vfx.py assign   # 2단계(렌더 확인 뒤): 칸(적중시·범위_지면·시전자)별 한 모델 배정 → Docs/research/ORIGINAL_VFX_ASSIGN_<등급>.csv
등급 = 그 스킬을 가진 로스터 파일 이름의 앞 토막. 원작_아트가 없거나 맵 밖·변환 불가인 칸은 「유지」(지금 무료팩 프리팹 그대로).
칸 위치는 기존 매핑 행에서 프리팹이 있던 칸을 따른다.
"""
import csv, glob, json, os, re, shutil, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
H = os.path.expanduser("~"); T = H + "/GRD_orig_vfx_trial"
MODE = sys.argv[1]
src = open(HERE + "/cast_vfx_survey.py", encoding="utf8").read()
sys.argv = ["x", "/tmp/_lg"]                                         # 조사 도구의 앞 절반(JASS·능력·모델 분석 함수)만 재사용
exec(compile(src[:src.index("# ───────── 본 조사")], "cast_vfx_survey", "exec"))
tag = lambda m: os.path.splitext(re.split(r"[\\/]", m)[-1])[0]
rows = [r for r in csv.DictReader(l for l in open(ROOT + "/Docs/research/SKILL_VFX_MAPPING.csv", encoding="utf-8-sig") if not l.startswith("#"))]

# 등급: 로스터 파일 이름 앞 토막 ← 로스터가 가진 스킬 guid ← .meta
g2p = {}
for mp in glob.glob(ROOT + "/Assets/Data/**/*.asset.meta", recursive=True):
    m = re.search(r"guid: (\w+)", open(mp, encoding="utf8").read()); g2p[m.group(1)] = mp[:-5]
grade = {}
for rp in glob.glob(ROOT + "/Assets/Data/Units/Roster/*.asset"):
    gname = os.path.basename(rp).split("_")[0]
    blk = re.search(r"skills:\n((?:  - .*\n)+)", open(rp, encoding="utf8").read())
    for g in re.findall(r"guid: (\w+)", blk.group(1)) if blk else []:
        p = g2p.get(g)
        if p: grade.setdefault(os.path.relpath(p, ROOT), set()).add(gname)

def models_of(r):
    out = {}
    trigs = [t for t in re.split(r"[,; ]+", r["원작_트리거"]) if t]
    for t in trigs:
        for tr, d in closure(t).items():
            if d > 1: continue
            body = family(tr)
            for m, att, kind in effects_in(body): out.setdefault(tag(m), (m, f"트리거 {tr}(깊이{d}) {kind}"))
            for uc in units_in(body):
                for m, why in unit_art(uc): out.setdefault(tag(m), (m, f"트리거 {tr} 더미 {why}"))
    if r["원작_능력_ID"]:
        for code in re.findall(r"[A-Z][0-9A-Z]{3}", r["원작_능력_ID"]):
            for m, why in abil_art(code): out.setdefault(tag(m), (m, why))
    return out

if MODE == "cand":
    cand = {}; need = {}
    for r in rows:
        ms = models_of(r) if (r["원작_트리거"] or r["원작_능력_ID"]) else {}
        cand[r["asset_path"]] = dict(grades=sorted(grade.get(r["asset_path"], [])), models={t: why for t, (m, why) in ms.items()}, inmap={t: bool(analyze(m).get("inmap")) for t, (m, why) in ms.items()})
        for t, (m, why) in ms.items(): need[t] = m
    json.dump(cand, open(T + "/lower_candidates.json", "w"), ensure_ascii=False, indent=1)
    wh = {}
    for g in ("16_원랜디스킬", "15_원랜디스킨"):
        for p in glob.glob(f"{H}/Desktop/구랜디스킨모음/{g}/*/*"):
            if os.path.isdir(p): wh.setdefault(os.path.basename(p).lower(), p)
    add = have = 0; miss = []
    for t, m in sorted(need.items()):
        if os.path.isdir(f"{T}/{t}"): have += 1; continue
        if not analyze(m).get("inmap"): continue
        s = wh.get(t.lower())
        if not s: miss.append(t); continue
        shutil.copytree(s, f"{T}/{t}"); add += 1
        pv = f"{T}/{t}/preview.png"
        if os.path.exists(pv): os.makedirs(T + "/_photos", exist_ok=True); shutil.move(pv, f"{T}/_photos/{t}.png")
    print("행", len(rows), "원작 모델 있는 행", sum(1 for v in cand.values() if v["models"]), "서로 다른 모델", len(need), "이미", have, "추가", add, "창고에 없음", miss)
    import collections
    print(collections.Counter(g for v in cand.values() for g in (v["grades"] or ["(로스터 없음)"])))

if MODE == "assign":
    cov = json.load(open("/tmp/ra/cov.json")) if os.path.exists("/tmp/ra/cov.json") else {}
    cand = json.load(open(T + "/lower_candidates.json"))
    idx = {r["폴더 이름"]: r for r in csv.DictReader(open(H + "/Desktop/구랜디스킨모음/16_원랜디스킬/index.csv", encoding="utf-8-sig"))}
    GROUP = {"고리·충격파": "고리", "땅폭발·가시": "땅폭발", "투사체": "투사체", "섬광·번개": "번개", "초승달·베기": "베기", "오라·버프": "오라", "불·얼음·바람": "원소", "미분류": "미분류"}
    SLOT = {"적중시": {"투사체": 3, "번개": 3, "베기": 2, "폭발": 2, "원소": 2, "미분류": 1, "땅폭발": 1, "고리": 1, "오라": 0},
            "범위": {"고리": 3, "폭발": 3, "땅폭발": 3, "원소": 2, "번개": 2, "베기": 1, "미분류": 1, "오라": 1, "투사체": 0},
            "시전자": {"오라": 3, "고리": 2, "원소": 2, "폭발": 1, "땅폭발": 1, "번개": 1, "미분류": 1, "베기": 0, "투사체": 0}}
    def shape(m):
        r = idx.get(m, {}); g = GROUP.get(r.get("모양 묶음", ""), "미분류"); n = m.lower()
        if re.search(r"boom|explo|burst|blast|nova", n) and g in ("미분류", "번개", "원소"): g = "폭발"
        if "투사체" in r.get("역할", "") and g == "미분류": g = "투사체"
        return g
    def ok(m):
        jp = f"{T}/{m}/{m}.json"
        if not os.path.exists(jp): return False
        d = json.load(open(jp))
        if not d["meshes"] or cov.get(m, 0) < 1.0: return False
        return not any(re.search(r"walk|attack|spell", x["name"], re.I) for x in d["sequences"])
    TOKG = ["특별함", "희귀함", "전설적인", "히든", "변화됨", "랜덤", "제한", "특수함"]
    TOP = {"초월", "불멸", "영원"}
    TOPCODE = {}
    for l in list(csv.reader(open(ROOT + "/Docs/research/ORIGINAL_MATCH_NAMES_2026-10-07.tsv", encoding="utf-8-sig"), delimiter="\t"))[1:]:
        for cdv in re.findall(r"[Hh]0[0-9A-Za-z]{2}", l[1]):
            if l[0].startswith(("불멸", "영원")): TOPCODE[cdv.lower()] = l[0].split("_")[0]
    # 쓰이는 에셋만: 로스터·씬·프리팹 등 스킬 에셋이 아닌 곳이 guid로 참조하는 것에서 출발해 스킬끼리 참조를 따라 닿는 것(10-09 PM: 참조 0 = 죽은 에셋, 행 삭제)
    guidre = re.compile(r"guid: (\w{32})"); refs = {}
    for ext in ("*.asset", "*.unity", "*.prefab"):
        for fp in glob.glob(ROOT + f"/Assets/**/{ext}", recursive=True):
            refs[os.path.relpath(fp, ROOT)] = set(guidre.findall(open(fp, encoding="utf8", errors="ignore").read()))
    alive = set(); todo = []
    for f, gs in refs.items():
        if "/UnitSkills/" in f and f.endswith(".asset"): continue
        for g in gs:
            q = g2p.get(g)
            if q and os.path.relpath(q, ROOT) not in alive: alive.add(os.path.relpath(q, ROOT)); todo.append(os.path.relpath(q, ROOT))
    while todo:
        f = todo.pop()
        for g in refs.get(f, ()):
            q = g2p.get(g)
            if q and os.path.relpath(q, ROOT) not in alive: alive.add(os.path.relpath(q, ROOT)); todo.append(os.path.relpath(q, ROOT))
    dead = []
    byg = {}; stats = {}
    for r in rows:
        a = r["asset_path"]; c = cand[a]; n = os.path.basename(a)
        if a not in alive: dead.append(a); continue
        g = next((x for x in TOKG if x in n), None) or next((x for x in c["grades"] if x not in TOP and x != "흔함"), None)
        if not g:                                                    # 상위 등급 유닛의 원작 능력 에셋(원작능력_불멸_·게이트_초월_·원작015_H09B …)은 따로(이미 넘긴 등급 CSV와 안 섞는다)
            t = next((x for x in TOP if x in n), None)
            if not t:
                hc = re.search(r"_([Hh]0[0-9A-Za-z]{2})", n)
                t = (TOPCODE.get(hc.group(1).lower(), "초월") if hc else "초월")
            g = t + "원작스킬"
        g = {"전설적인": "전설"}.get(g, g)
        slots = [k for k, col in (("적중시", "적중시_프리팹"), ("범위", "범위_지면_프리팹"), ("시전자", "시전자_프리팹")) if r[col]]
        pool = [m for m in c["models"] if c["inmap"].get(m) and ok(m)]
        used = set(); cells = {}
        for k in slots:
            best = None
            for m in sorted(pool, key=lambda m: (-SLOT[k].get(shape(m), 0), m)):
                if m not in used and SLOT[k].get(shape(m), 0) >= 1: best = m; break
            if best: used.add(best); cells[k] = "원작:" + best
            else: cells[k] = "유지"
        byg.setdefault(g, []).append([a, cells.get("적중시", ""), cells.get("범위", ""), cells.get("시전자", "")])
    for g, rs in byg.items():
        with open(ROOT + f"/Docs/research/ORIGINAL_VFX_ASSIGN_{g}.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f); w.writerow(["asset_path", "적중시_프리팹", "범위_지면_프리팹", "시전자_프리팹"]); w.writerows(rs)
        cells = [x for r in rs for x in r[1:] if x]
        stats[g] = (len(rs), sum(1 for x in cells if x.startswith("원작:")), sum(1 for x in cells if x == "유지"))
    for g, (n, o, k) in sorted(stats.items()): print(g, "행", n, "원작 칸", o, "유지 칸", k)
    print("죽은 에셋(참조 0 또는 파일 없음)으로 뺀 행", len(dead))
