"""고유 동작 전수조사 3단계(2026-10-01): survey_clips.py 결과(out_*.jsonl·com_*.jsonl)를 읽어 표(고유동작_전수조사.md)를 만든다. 시스템 python.

  python3 Tools/blender/survey_report.py   →  ~/GRD_motion_trial/고유동작_전수조사.md · _work/survey/summary.json
판정 규칙(숫자는 survey_clips.py가 잰 값 — 뼈 머리 위치의 첫↔끝 RMS ÷ 모델 키 = seam, 최대 이동 ÷ 키 = motion):
  쓸 만함 = Idle 후보(이음새 ≤0.12 · 움직임 ≥0.004)와 Attack 후보(움직임 ≥0.08)가 둘 다 있다(Move 없음은 허용 — 이호준·황길라와 같은 Generic 경로).
  애매    = 클립은 있으나 위가 모자란다(Idle만·Attack만·한 덩어리라 분리 필요·이음새 큼·이름으로 못 가름).
  불가    = 쓸 동작이 없다(원본에 동작 없음·T자/껍데기뿐·읽기 실패).
  이미    = 이미 고유 동작(UnitModelPostprocessor.GenericRigUnits·적 셋·황길라 승격 대기).
"""
import collections, glob, json, os, re

HOME = os.path.expanduser("~/GRD_motion_trial")
S = HOME + "/_work/survey"
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GRADE_ORDER = ["영원", "불멸", "초월", "제한", "전설적인", "히든", "희귀함", "특별함", "안흔함", "흔함", "랜덤", "다른세계", "특수함", "변화됨", "초월위습"]


def load(pattern):
    out = []
    for f in sorted(glob.glob(pattern)):
        out += [json.loads(l) for l in open(f)]
    return out


SRC, COM = load(S + "/out_*.jsonl"), load(S + "/com_*.jsonl")
cs = open(PROJECT + "/Assets/Editor/UnitModelPostprocessor.cs", encoding="utf-8").read()
blk = cs[cs.index("GenericRigUnits"):]
blk = blk[:blk.index("};")]
OWN = set(re.findall(r'^\s+"([^"]+)",', blk, re.M)) | {"주영호", "왕승환", "김민준안경"}
PENDING = {"특수함_황길라": "승격 대기(PM 등록 중)"}


def take(name):
    toks = [t for t in re.split(r"\|", name) if t and t.lower() not in ("base layer", "armature", "layer0", "mixamo.com", "skeleton", "armature.001")]
    t = toks[-1] if toks else name
    t = re.sub(r"^(Armature|skeleton[ _]?#?\d*)[|_ ]*", "", t, flags=re.I)
    return t


def kind(t):
    n = t.lower()
    if re.search(r"idle|stand|wait|breath|stay|stance|normal$|rest|^loop$|show|pose", n) and not re.search(r"attack|skill|hit|die|dead", n): return "Idle"
    if re.search(r"walk|run|move|jog|dash|sprint|fly|swim|glide", n): return "Move"
    if re.search(r"die|dead|death|dying|lose|ko$", n): return "Die"
    if re.search(r"hit|hurt|damage|dizzy|stun|shock|blown|down|knock|flinch|react", n): return "Hit"
    if re.search(r"attack|atk|punch|kick|slash|strike|shoot|combo|common|swing|chop|stab|fire\b|cast|spell|skill|bankai|kame|ossu|jump", n): return "Attack"
    return "?"


def grade_of(name):
    for g in GRADE_ORDER:
        if name.startswith(g): return g
    return "적" if re.match(r"R\d", name) else "기타"


def best(recs):
    ok = [r for r in recs if r.get("ok")]
    if not ok: return recs[0] if recs else None
    return max(ok, key=lambda r: (sum(1 for a in r.get("actions", []) if a["frames"] >= 3), r.get("bones", 0)))


def evaluate(r):
    acts = [a for a in r.get("actions", []) if a["frames"] >= 3]
    info = dict(n_actions=len(acts))
    if not r.get("ok"): return "불가", "읽기 실패: " + (r.get("err", "")[:60].replace("\n", " ")), info, []
    if r.get("armatures", 0) == 0: return "불가", "뼈대 없음(정적 모델)", info, []
    if not acts:
        return "불가", "원본에 동작 없음" + ("(T자 껍데기 %d개)" % len(r.get("actions", [])) if r.get("actions") else ""), info, []
    rows = []
    seen_t = set()
    for a in acts:
        t = take(a["name"])
        key = (re.sub(r"[_\.]\d+$", "", t), a["frames"])
        if key in seen_t: continue
        seen_t.add(key)
        rows.append(dict(take=t, kind=kind(t), frames=a["frames"], seam=a.get("seam"), motion=a.get("motion"), drift=a.get("root_drift_xy"), minz=a.get("minbone_z")))
    idle = [x for x in rows if x["kind"] == "Idle" and x["seam"] is not None]
    move = [x for x in rows if x["kind"] == "Move" and x["seam"] is not None]
    att = [x for x in rows if x["kind"] == "Attack" and (x["motion"] or 0) >= 0.08]
    unk = [x for x in rows if x["kind"] == "?"]
    goodidle = [x for x in idle if x["seam"] <= 0.12 and (x["motion"] or 0) >= 0.004]
    info.update(idle=len(idle), move=len(move), att=len(att))
    if len(rows) == 1 and rows[0]["frames"] >= 120:
        return "애매", "한 덩어리 클립 1개(%df) — 구간 분리 필요(렌더·뼈 곡선으로 가를 수 있는지 봐야 함)" % rows[0]["frames"], info, rows
    if goodidle and att:
        why = "Idle %s(이음새 %.3f) + Attack %d개" % (goodidle[0]["take"][:18], goodidle[0]["seam"], len(att))
        if move: why += " + Move %s" % move[0]["take"][:14]
        else: why += " (Move 없음 → 이호준식)"
        return "쓸 만함", why, info, rows
    if idle and att:
        return "애매", "Idle 이음새 큼(최소 %.2f) — 끝 프레임 당겨 이음새 지우는 loop 처리 필요 + Attack 있음" % min(x["seam"] for x in idle), info, rows
    if idle and not goodidle and not att and all((x["motion"] or 0) < 0.004 for x in idle):
        return "애매", "Idle이 정지(움직임 0 — 쓸 숨쉬기 없음) · 공격 후보 %d개" % len(att), info, rows
    if goodidle:
        return "애매", "Idle만 있음(Attack 후보 없음 — 스킬·이름 불명 %d개)" % len(unk), info, rows
    if att or (unk and not idle):
        return "애매", "Idle 없음 — 동작 %d개(공격 후보 %d·이름 불명 %d)" % (len(rows), len(att), len(unk)), info, rows
    return "애매", "동작 %d개뿐이나 쓸 Idle·Attack가 안 가려짐" % len(rows), info, rows


com_meshes = collections.defaultdict(set)
for r in COM:
    if r.get("ok"):
        for nm, nv in r.get("meshes", []): com_meshes[r["name"]].add(re.sub(r"\.\d+$", "", nm).lower())
EXTRA_KW = re.compile(r"cape|cloak|wing|sword|blade|katana|weapon|gun|rifle|hat|helmet|staff|axe|shield|aura|effect|fx|glow|trail|particle|flame|fire|smoke|ring|halo|spear|bow|hammer|mask|scarf|coat|armor|tail|horn|crown|backpack|bag|ribbon|feather|wind|beam|slash|energy|ball|orb", re.I)


def extras(unit, r):
    have = com_meshes.get(unit, set())
    out = []
    for nm, nv in r.get("meshes", []):
        base = re.sub(r"\.\d+$", "", nm).lower()
        if base in have or nv < 20: continue
        out.append((nm, nv, bool(EXTRA_KW.search(nm))))
    return out


by = collections.defaultdict(list)
for r in SRC: by[r["name"]].append(r)
units = {}
for name, recs in by.items():
    r = best(recs)
    if name in OWN or name in PENDING:
        verdict, why, info, rows = "이미", PENDING.get(name, "이미 고유 동작(Generic)"), {}, []
    else:
        verdict, why, info, rows = evaluate(r)
    units[name] = dict(name=name, kind=r["kind"], src=r["src"], verdict=verdict, why=why, info=info, rows=rows, bones=r.get("bones"), mesh_count=r.get("mesh_count"), extras=extras(name, r), height=r.get("height"))
nosrc = json.load(open(S + "/jobs.json"))["no_source"]
json.dump(dict(units=units, no_source=nosrc), open(S + "/summary.json", "w"), ensure_ascii=False, indent=1)

L = ["# 고유 동작 전수조사 (2026-10-01, Blender 세션 · 읽기 전용)", "",
     "사장님 지시 「각 스킨에 고유 스킨이나 모션 있으면 살려도 좋아」 — 구랜디스킨모음 원본을 열어 **지금 안 쓰는 자체 동작·꾸밈**을 쟀다. Assets·에디터는 안 건드렸다.",
     "", "## 0. 어떻게 쟀나", "- `Tools/blender/survey_extract.py`(압축 풀기 → 모델 파일 %d개) → `survey_clips.py`(Blender로 열어 클립·뼈·메시 측정) → `survey_report.py`(이 표). 산출 원자료: `~/GRD_motion_trial/_work/survey/`." % len(SRC),
     "- 클립마다 **이음새**(끝↔첫 자세, 뼈 머리 RMS ÷ 모델 키) · **움직임**(최대 이동 ÷ 키) · 뿌리 이동 · 최저 뼈 높이를 쟀다. 이름으로 Idle·Move·Attack·Hit·Die를 가렸고 이름이 안 가려지면 「?」.",
     "- 판정: **쓸 만함** = 이음새 ≤0.12·움직임 있는 Idle + 움직임 ≥0.08인 Attack 후보가 둘 다 있음(Move 없음은 허용 — 이호준·황길라처럼 Generic 자체 클립 경로). **애매** = 클립은 있으나 위가 모자람(Idle만·한 덩어리·이음새 큼·이름 불명). **불가** = 쓸 동작 없음. **이미** = 이미 고유 동작.",
     "- ⚠️ 이 표는 **측정과 이름**으로 가른 1차 선별이다. 「쓸 만함」도 렌더로 발 미끄럼·뼈 뒤틀림을 눈으로 본 뒤에야 확정(2단계에서 동작판을 뽑아 렌더). 한 파일만 읽은 압축은 가장 동작 많은 모델을 골랐다.",
     "- 원본을 못 찾은 유닛 %d종(커밋본이 옛 git 판이라 구랜디스킨모음에 같은 이름 파일이 없다): %s" % (len(nosrc), ", ".join(nosrc)), ""]
cnt = collections.Counter(u["verdict"] for u in units.values())
L += ["## 1. 한눈 요약", "", "| 판정 | 수 |", "|---|---|"] + [f"| {k} | {cnt.get(k, 0)} |" for k in ("이미", "쓸 만함", "애매", "불가")] + [""]
bygrade = collections.defaultdict(list)
for u in units.values(): bygrade[grade_of(u["name"]) if u["kind"] == "유닛" else u["kind"]].append(u)
L += ["| 등급 | 이미 | 쓸 만함 | 애매 | 불가 |", "|---|---|---|---|---|"]
order = GRADE_ORDER + ["적", "보스", "기타"]
for g in order:
    if g in bygrade:
        c = collections.Counter(u["verdict"] for u in bygrade[g])
        L.append(f"| {g} | {c.get('이미', 0)} | {c.get('쓸 만함', 0)} | {c.get('애매', 0)} | {c.get('불가', 0)} |")
L += ["", "## 2. (a) 자체 동작 — 등급 위부터", ""]
for g in order:
    if g not in bygrade: continue
    L += [f"### {g}", "", "| 유닛 | 판정 | 이유 | 클립(이름·프레임) | 이음새 |", "|---|---|---|---|---|"]
    rank = {"쓸 만함": 0, "애매": 1, "이미": 2, "불가": 3}
    for u in sorted(bygrade[g], key=lambda x: (rank[x["verdict"]], x["name"])):
        names = [x["take"] for x in u["rows"]]
        pre = os.path.commonprefix(names) if len(names) > 1 else ""
        pre = pre[:pre.rfind("_") + 1] if "_" in pre else ""
        clips = ", ".join(f"{x['take'][len(pre):][:18]}({x['frames']})" for x in u["rows"][:9]) + (f" … 외 {len(u['rows']) - 9}" if len(u["rows"]) > 9 else "")
        seams = ", ".join(f"{x['kind'][0]}:{x['seam']}" for x in u["rows"][:6] if x["seam"] is not None)
        L.append(f"| {u['name']} | {u['verdict']} | {u['why']} | {clips} | {seams} |")
    L.append("")
L += ["## 3. (b) 원본에 있는데 우리가 뺀(커밋본에 이름이 없는) 메시", "", "커밋된 FBX의 메시 이름과 원본 메시 이름을 견줘 **이름이 없는 것**(정점 20 이상)만 적었다. 이름이 `Object_12` 같은 glb는 이름으로 못 가려 개수만 적는다 — ⚠️ 이름 비교의 한계(병합·개명된 메시는 「뺀 것」으로 잘못 잡힐 수 있음). 꾸밈 낱말(망토·날개·무기·이펙트…)이 든 것은 ★.", "",
      "| 유닛 | 뺀 메시 후보(정점) |", "|---|---|"]
for g in order:
    for u in sorted(bygrade.get(g, []), key=lambda x: x["name"]):
        ex = [e for e in u["extras"]]
        star = [e for e in ex if e[2]]
        if not ex: continue
        txt = ", ".join(f"★{n}({v})" for n, v, _ in star[:6]) + (" · " if star and len(ex) > len(star) else "") + (f"그 밖 {len(ex) - len(star)}개" if len(ex) > len(star) else "")
        if star or len(ex) >= 3:
            L.append(f"| {u['name']} | {txt} |")
open(HOME + "/고유동작_전수조사.md", "w").write("\n".join(L))
print(dict(cnt), len(units))
