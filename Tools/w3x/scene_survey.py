"""대표 스킬 연출 후보 표(2026-10-09, blender): 원작 캐릭터마다 「눈에 띄는 큰 스킬」 트리거를 j에서 점수로 찾는다.

  /usr/bin/python3 Tools/w3x/scene_survey.py   → ~/GRD_scenes/scene_table.csv · scene_table.json
입력: Docs/reference/MASTER_UID_ROSTER_MAP.csv(유닛 ID → 우리 로스터) · war3map.j(공격 트리거 사슬) · w3u(더미 유닛 umdl) · w3a(능력 이름)
점수: 더미 유닛 종류×3 + 이펙트 호출 + 대기·타이머×2 + 번개×2 + 반복문×2 + 입자 모델 수 (깊이 ≥1 트리거만; 공격 트리거 자신은 제외)
"""
import csv, json, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
src = open(HERE + "/cast_vfx_survey.py", encoding="utf8").read()
sys.argv = ["x", "/tmp/_ss"]
exec(compile(src[:src.index("# ───────── 로스터·우리 스킬")], "cast_vfx_survey", "exec"))
OUT = os.path.expanduser("~/GRD_scenes"); os.makedirs(OUT, exist_ok=True)
JL = J.split("\n")
def jline(fn):
    for i, ln in enumerate(JL, 1):
        if f"function {fn} takes" in ln: return i
    return ""
UNAME = {k: v.get("unam", "") for k, v in UNITS.items()}
rows = list(csv.DictReader(open(ROOT + "/Docs/reference/MASTER_UID_ROSTER_MAP.csv", encoding="utf-8-sig")))
res = []
def nn(v): return re.sub(r"\s+", " ", re.sub(r"\|c[0-9a-fA-F]{8}|\|r", " ", v or "")).strip()
ANAME = {k: nn(UNAME.get(k, "")) for k in ATTACK}
for r in rows:
    uid, roster = r["유닛ID"], r["로스터"]
    code = uid if uid in ATTACK else next((k for k in ATTACK if k.lower() == uid.lower()), None)
    if not code:                                                    # 합쳐진 유닛(h04U 샹크스 초월 = H08Z 마린포드 정상해전 종결자)은 이름으로 공격 코드를 찾는다
        un = nn(UNAME.get(uid, ""))
        code = next((k for k, v in ANAME.items() if v and len(v) >= 6 and v in un), None)
    if not code: continue
    atk = ATTACK[code]; clo = closure(atk); best = []
    pre = atk.split("_")[0]                                         # 이벤트로 도는 스킬 트리거(Shanks_ET_pegi1 등)는 공격 트리거가 안 부르므로 같은 접두 계열을 깊이 1로 더한다
    for fname in FN:
        mm = re.match(rf"Trig_({re.escape(pre)}_\w+?)(?:_Func\d.*|_Conditions|_Actions)$", fname)
        if mm and mm.group(1) not in clo: clo[mm.group(1)] = 1
    for trig, depth in clo.items():
        if depth < 1: continue
        body = family(trig)
        effs = effects_in(body); units = [u for u in units_in(body) if u.lower() != code.lower()]
        dummies = {}
        for uc in units:
            for m, why in unit_art(uc):
                if "몸" in why or "투사체" in why: dummies[uc] = (UNAME.get(uc, ""), m)
        n_sleep = len(re.findall(r"TriggerSleepAction|PolledWait|TimerStart|CreateTimer|TriggerRegisterTimer|RegisterTimerPutsTriggerToSleep", body))
        n_light = len(re.findall(r"AddLightning", body)); n_loop = len(re.findall(r"\bloop\b", body))
        pre = 0
        for m in {m for m, a, k in effs} | {mm for (_, mm) in dummies.values()}:
            a = analyze(m); pre += 1 if a.get("pre2") else 0
        abil = sorted({a for a in re.findall(r"'(A[0-9A-Z]{3})'", body) if a in ABIL})
        score = 3 * len(dummies) + len(effs) + 2 * n_sleep + 2 * n_light + 2 * n_loop + pre
        best.append((score, trig, depth, effs, dummies, n_sleep, n_light, n_loop, pre, abil))
    best.sort(key=lambda x: -x[0])
    for score, trig, depth, effs, dummies, ns, nl, nloop, pre, abil in best[:2]:
        fn = next((n for n in FN if n.startswith(f"Trig_{trig}_") and n.endswith("Actions")), "")
        res.append(dict(roster=roster, uid=code, name=UNAME.get(code, ""), attack=atk, trigger=trig, depth=depth, jline=jline(fn) if fn else "", score=score,
                        dummies={k: v[0] + " / " + os.path.basename(v[1].replace("\\", "/")) for k, v in dummies.items()}, effects=sorted({os.path.basename(m.replace("\\", "/")) for m, a, k in effs}),
                        sleeps=ns, lightnings=nl, loops=nloop, particle_models=pre, abilities=[f"{a} {ABIL[a].get('anam', '')}" for a in abil][:4]))
json.dump(res, open(OUT + "/scene_table.json", "w"), ensure_ascii=False, indent=1)
with open(OUT + "/scene_table.csv", "w", newline="", encoding="utf-8-sig") as f:
    w = csv.writer(f); w.writerow(["우리 로스터", "원작 유닛 ID", "원작 이름", "공격 트리거", "대표 후보 트리거", "깊이", "j 줄", "점수", "더미 유닛(코드: 이름/모델)", "이펙트 모델", "대기·타이머", "번개", "반복", "입자 모델 수", "능력(코드 이름)"])
    for x in res: w.writerow([x["roster"], x["uid"], x["name"], x["attack"], x["trigger"], x["depth"], x["jline"], x["score"], "; ".join(f"{k}: {v}" for k, v in x["dummies"].items()), "; ".join(x["effects"][:12]), x["sleeps"], x["lightnings"], x["loops"], x["particle_models"], "; ".join(x["abilities"])])
print(len(res), "후보 행", len({x["roster"] for x in res}), "로스터")
