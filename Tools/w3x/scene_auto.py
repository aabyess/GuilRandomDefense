"""대표 연출 대본 일괄 생성(2026-10-09, blender): scene_table.json의 모든 (로스터, 트리거)를 jass_sim으로 풀어 대본 json으로.

  /usr/bin/python3 Tools/w3x/scene_auto.py [prep|compile]
  prep    : 필요한 맵 안 모델을 창고에서 평평한 ~/GRD_orig_vfx_trial/로 복사(없으면 MPQ에서 변환 목록을 만든다)
  compile : 대본 ~/GRD_scenes/auto/<id>.json + 색인 ~/GRD_scenes/auto_index.csv
워크3 기본 모델(맵에 없음) 대체 규칙: 번개류→roarthunder · 배→우리 해적선 프리팹 · 폭발/충격→SuperBigExplosion · 불→E_FireEX · 얼음→AZ-Ice-Zhendi3 · 치유/신성→Effect Blessing of Elun · 바람→tornado33 · 그 외→SuperBigExplosion
ourSkill: 그 로스터 스킬 중 마나/게이트/확률 액티브(시그니처: triggerType≠Aura) 가운데 피해 효과 합이 가장 큰 것(패시브·버프 금지), 없으면 빈칸.
"""
import csv, glob, json, os, re, shutil, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import jass_sim, w3u, w3a, yaml                                     # noqa: E402
from mpqread import Archive                                          # noqa: E402
H = os.path.expanduser("~"); T = H + "/GRD_orig_vfx_trial"; OUT = os.environ.get("SCENE_OUT", H + "/GRD_scenes/auto"); os.makedirs(OUT, exist_ok=True)
MODE = sys.argv[1] if len(sys.argv) > 1 else "compile"
S2 = bool(os.environ.get("JASS_S2"))               # S2(2.323) 해석: 단위 자료는 슬크(unitui·unitbalance·unitdata), MPQ는 s2.mpq
if S2:
    import slk
    ARC = Archive(H + "/GRD_motion_trial/_work/s2.mpq")
    _ui, _bal, _dat = (slk.parse(ARC.read(f"units\\{n}.slk"))[1] for n in ("unitui", "unitbalance", "unitdata"))
    fl = lambda v: float(v) if v not in (None, "", "-", "_") else None
    U = {k: dict(umdl=r.get("file", ""), usca=fl(r.get("modelScale")) or 1.0, umvh=fl(_dat.get(k, {}).get("moveHeight")) or 0.0, udtm=fl(_dat.get(k, {}).get("death")) or 0.0,
                 uhpm=fl(_bal.get(k, {}).get("HP")), uhpr=fl(_bal.get(k, {}).get("regenHP")), unam=f"S2 {k}") for k, r in _ui.items()}
else:
    U = {u["id"]: u["mods"] for u in w3u.parse(HERE + "/원본/풀린것/war3map.w3u")}
    ARC = Archive(H + "/GRD_motion_trial/_work/ord.mpq")
tag = lambda m: os.path.splitext(re.split(r"[\\/]", m)[-1])[0]
clean = lambda s: re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", s or "")
table = json.load(open(os.environ.get("SCENE_TABLE", H + "/GRD_scenes/scene_table.json")))
SIMS = {}
def sim_of(tr):
    if tr not in SIMS: SIMS[tr] = jass_sim.simulate(tr)
    return SIMS[tr]
def in_map(path):
    p = path.replace("\\\\", "\\"); q = re.sub(r"\.mdl$", ".mdx", p, flags=re.I)
    return bool(ARC.read(q) or ARC.read(os.path.basename(q.replace("\\", "/"))))
def model_of_event(e):
    if e.get("effectModelPath"): return e["effectModelPath"]
    u = U.get(e["code"], {}); return u.get("umdl", "")

# ── prep ──
def all_models():
    need = {}
    for row in table:
        for e in sim_of(row["trigger"])["events"]:
            if e["op"] == "spawn":
                m = model_of_event(e)
                if m: need[tag(m)] = m
    return need
if MODE == "prep":
    wh = {}
    for g in ("16_원랜디스킬", "15_원랜디스킨"):
        for p in glob.glob(f"{H}/Desktop/구랜디스킨모음/{g}/*/*"):
            if os.path.isdir(p): wh.setdefault(os.path.basename(p).lower(), p)
    add = have = 0; miss = []; stock = []
    for t, m in sorted(all_models().items()):
        if os.path.isdir(f"{T}/{t}"): have += 1; continue
        if not in_map(m): stock.append(t); continue
        s = wh.get(t.lower())
        if not s: miss.append(m); continue
        shutil.copytree(s, f"{T}/{t}"); add += 1
        pv = f"{T}/{t}/preview.png"
        if os.path.exists(pv): os.makedirs(T + "/_photos", exist_ok=True); shutil.move(pv, f"{T}/_photos/{t}.png")
    open("/tmp/auto_missing.txt", "w").write("\n".join(miss))
    print("이미", have, "추가", add, "맵 안인데 창고에 없음", len(miss), "워크3 기본(대체 대상)", len(stock)); sys.exit()

# ── compile ──
def substitute(t, path):
    n = t.lower()
    if re.search(r"light|thunder|bolt|zap|spark|elec", n): return "roarthunder", "번개류 → 맵 안 roarthunder"
    if re.search(r"ship|boat|battle|galleon|transport|frigate|destroyer", n): return None, "배 → 우리 해적선 프리팹(Unit_해적선)"
    if re.search(r"ice|frost|snow|blizz|freez", n): return "AZ-Ice-Zhendi3", "얼음 → AZ-Ice-Zhendi3"
    if re.search(r"fire|flame|burn|inferno|lava|magma|conflag", n): return "E_FireEX", "불 → E_FireEX"
    if re.search(r"heal|holy|bless|light|divine|resurrect|revive|tranq", n): return "Effect Blessing of Elun", "치유/신성 → Effect Blessing of Elun"
    if re.search(r"wind|tornado|storm|cyclone|whirl", n): return "tornado33", "바람 → tornado33"
    return "SuperBigExplosion", "그 외 → SuperBigExplosion(범용 폭발)"

def skill_pick(roster):
    rp = f"{ROOT}/Assets/Data/Units/Roster/{roster}.asset"
    if not os.path.exists(rp): return "", ""
    g2p = SKP
    blk = re.search(r"skills:\n((?:  - .*\n)+)", open(rp, encoding="utf8").read()); best = (0, "", "")
    for g in re.findall(r"guid: (\w+)", blk.group(1)) if blk else []:
        p = g2p.get(g)
        if not p: continue
        try: d = yaml.safe_load(open(p, encoding="utf8").read().split("--- !u!114 &11400000\n", 1)[1])["MonoBehaviour"]
        except Exception: continue
        if d["triggerType"] == 2: continue
        sc = 0.0
        for L in d["levels"][:1]:
            for ef in L["effects"]:
                if ef["kind"] == 0 and ef["target"] in (2, 3, 4, 5): sc += (abs(ef.get("multiplier", 0)) + abs(ef.get("bonus", 0))) * max(ef.get("hitCount", 1), 1) * max(ef.get("chance", 1), 0.01)
        if sc > best[0]: best = (sc, os.path.relpath(p, ROOT), d["skillName"])
    return best[1], best[2]
SKP = {}
def review_map():
    """구현담당1 검수본(정본): 행 번호 → md 표의 (우리 유닛, 트리거) → 추천 ourSkill. 판정 「검토」(지원형 — 피해 스킬 없음)는 제외 목록."""
    rv = list(csv.DictReader(open(ROOT + "/Docs/research/REPRESENTATIVE_OURSKILL_REVIEW_2026-10-09.csv", encoding="utf-8-sig")))
    mdrows = {}
    for ln in open(ROOT + "/Docs/research/REPRESENTATIVE_SCENES_2026-10-09.md", encoding="utf8"):
        m = re.match(r"\| (\d+) \| (\S+) \|.*?`(\w+)`\(깊이", ln)
        if m: mdrows[int(m.group(1))] = (m.group(2), m.group(3))
    mp = {}; excl = set()
    for r in rv:
        key = mdrows.get(int(r["행"]))
        if not key: continue
        if r["판정"] == "검토": excl.add(key[0]); continue
        mp[key] = r["추천ourSkill"]
    return mp, excl, set(mdrows.values())
if MODE == "compile":
    REVIEW, EXCL, VALID = review_map()
    for mp in glob.glob(ROOT + "/Assets/Data/UnitSkills/*.asset.meta"):
        m = re.search(r"guid: (\w+)", open(mp, encoding="utf8").read()); SKP[m.group(1)] = mp[:-5]
    index = []; best_by_roster = {}
    for row in table: best_by_roster[row["roster"]] = max(best_by_roster.get(row["roster"], row), row, key=lambda r: r["score"])
    for row in table:
        if not os.environ.get("SCENE_ALL") and (row["roster"] in EXCL or (row["roster"], row["trigger"]) not in VALID): continue                            # 지원형(피해 스킬 없음) — 이번 확대에서 제외(PM)
        sid = f"{row['roster']}__{row['trigger']}".replace(" ", "_")
        s = sim_of(row["trigger"]); ev = [dict(e) for e in s["events"]]
        spawns = {e["id"]: e for e in ev if e["op"] == "spawn"}
        models = {}; subs = []; out = []; skipped = []
        for e in ev:
            if e["op"] == "spawn":
                mp_ = model_of_event(e)
                if not mp_ or not tag(mp_): continue
                t = tag(mp_); sub = None
                if not os.path.isdir(f"{T}/{t}") and in_map(mp_):
                    skipped.append(t); continue                       # 유닛 몸 모델(창고 16에 없는 영웅 모델) — 이펙트가 아니라 생략
                if not os.path.isdir(f"{T}/{t}"):
                    st, why = substitute(t, mp_); sub = dict(original=mp_, reason=why)
                    if st: sub["model"] = st; t = st
                    else: sub.update(ship=True, kind="unityPrefab", path="Assets/Prefabs/Generated/Unit_해적선.prefab", lengthWc3=380)
                    subs.append(f"{tag(mp_)}→{st or '해적선'}")
                u = U.get(e["code"], {}); hp, rg = u.get("uhpm"), u.get("uhpr")
                life = (hp / -rg) if isinstance(rg, (int, float)) and rg < 0 and hp else None
                d = dict(t=e["t"], op="spawn", id=e["id"], dummyUnit=e["code"], unitName=clean(u.get("unam", "")) or "(이펙트)", model=t, modelPath=mp_, baseScale=u.get("usca", 1.0) if e["code"] != "(이펙트)" else 1.0,
                         flyHeight=u.get("umvh", 0.0) if e["code"] != "(이펙트)" else 0.0, lifeSec=life, deathSec=u.get("udtm", 0.0), at=e["at"], owner=e.get("owner"))
                if sub:
                    d["substitute"] = sub
                    if sub.get("model") == "roarthunder": d["baseScale"] = 6.0; d["baseScaleNote"] = "roarthunder 기준 크기(드래곤 6)로 적음 — 원작 값 아님"
                    elif sub.get("model") == "SuperBigExplosion": d["baseScale"] = min(d["baseScale"], 1.2); d["baseScaleNote"] = "범용 폭발 대체 — 화면 덮음 방지로 상한 1.2"
                    elif sub.get("model"): d["baseScale"] = min(d["baseScale"], 3.0); d["baseScaleNote"] = "대체 모델 크기 보정(원작 값 상한 3)"
                out.append(d); models[t] = None
            else: out.append(e)
        # 같은 시각 set 접기
        byid = {e["id"]: e for e in out if e["op"] == "spawn"}; keep = []
        for e in out:
            if e["op"] == "set" and e["id"] in byid and e["t"] == byid[e["id"]]["t"]:
                tgt = byid[e["id"]]
                for k, v in e.items():
                    if k not in ("t", "op", "id"): tgt[k] = v
            elif e["op"] == "set" and e["id"] not in byid: continue
            elif e["op"] in ("kill", "ramp", "teleport") and e.get("id") not in byid: continue
            else: keep.append(e)
        # lifeSec 정리: set(lifeSec)는 spawn에 접힘, 늦게 오면 kill로
        for e in list(keep):
            if e["op"] == "set" and "lifeSec" in e and e["id"] in byid:
                byid[e["id"]]["lifeSec"] = e["lifeSec"]; keep.remove(e)
        cut = [e for e in keep if e["t"] <= 12.0]
        if len(cut) != len(keep): s["truncated"] = True
        keep = cut
        for e in keep:
            if e["op"] == "spawn" and e.get("lifeSec") is None:
                mb = None
                if e["dummyUnit"] == "(이펙트)":
                    jp = f"{T}/{e['model']}/{e['model']}.json"
                    if os.path.exists(jp):
                        sq = json.load(open(jp))["sequences"]
                        e["lifeSec"] = max([x["seconds"] for x in sq] or [1.0]) if sq else 1.0
                    else: e["lifeSec"] = 1.5
                else: e["lifeSec"] = 3.0; e["lifeNote"] = "수명 미상(체력 재생이 음수가 아님) — 기본 3초"
        n_sp = sum(1 for e in keep if e["op"] == "spawn")
        sk = row.get("ourSkillOverride") or REVIEW.get((row["roster"], row["trigger"]))
        sk = ("SkillData_" + sk) if sk else ""
        skn = sk[10:] if sk else ""
        sc = dict(schemaVersion=1, id=sid, title=f"{clean(row['name'])[:30]} — {row['trigger']}", ourUnit=row["roster"], origUnit=f"{row['uid']} {clean(row['name'])[:30]}", ourSkill=sk or None, ourSkillName=skn or None,
                  primary=(best_by_roster[row["roster"]] is row), source=dict(trigger=row["trigger"], jFunction=f"Trig_{row['trigger']}_Actions", jLine=row["jline"], score=row["score"], mechanism="jass_sim 해석(스테이지 머신·조건·루프 실행)"),
                  skippedBodyModels=skipped, trigger=dict(cause="원작 트리거 실행 — 시전자 (0,0), 대상 (600,0), 각도 0° = 시전자→대상", anchors=dict(caster="시전자", target="공격 대상")),
                  durationSec=round(min(max(s["endT"], max([e["t"] for e in keep] or [0])) + 1.0, 13.0), 2), unresolvedConditions=s["unresolved"], truncated=s.get("truncated", False), quality=("자동 해석 실패(이벤트 없음)" if n_sp == 0 else ("검토 필요(조건 미해결 많음/반복 폭주)" if (s["unresolved"] > 6 or s.get("truncated") or n_sp > 250) else "양호")), timeline=sorted(keep, key=lambda e: e["t"]), substitutions=subs)
        # models 블록
        sys.path.insert(0, HERE)
        import scene_scripts as SS_                                  # noqa: E402  (model_block 재사용)
        for m in list(models):
            nm = next((e["modelPath"] for e in sc["timeline"] if e["op"] == "spawn" and e["model"] == m), m + ".mdl")
            models[m] = SS_.model_block(m + ".mdl" if os.path.isdir(f"{T}/{m}") else nm)
        sc["models"] = models
        json.dump(sc, open(f"{OUT}/{sid}.json", "w"), ensure_ascii=False, indent=1)
        index.append([sid, row["roster"], clean(row["name"])[:20], row["trigger"], row["jline"], row["score"], "대표" if sc["primary"] else "2차", n_sp, sc["durationSec"], s["unresolved"], sc["quality"], "; ".join(subs[:4]), sk, skn])
    with open(os.environ.get("SCENE_INDEX", H + "/GRD_scenes/auto_index.csv"), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f); w.writerow(["id", "우리 유닛", "원작", "트리거", "j 줄", "점수", "구분", "더미·이펙트 수", "길이(초)", "미해결 조건", "품질", "대체 모델", "ourSkill 에셋", "ourSkill 이름"]); w.writerows(index)
    print(len(index), "대본")
