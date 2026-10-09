"""원작 스킨 교체 시범 묶음(2026-10-09, blender): 우리 유닛 하나를 원작 유닛의 겉모습(스킨·동작·스킬 연출)으로 바꾸기 위한 Assets 밖 산출.
  /usr/bin/python3 Tools/w3x/skin_swap_pack.py <김경현_상디|양재모_아카이누|박민석_브룩> → ~/GRD_skin_swap/<이름>/
    model/        원작 FBX(뼈·클립 30fps 구움)+Textures(알파0 수정)+json(+ swap.json: 시퀀스·지오셋 숨김·타격 시점)
    clip_map.json 원작 시퀀스 → 우리 Idle/Move/Attack(+변형)/Spell/Death + 타격 시점(w3u udp1)
    scripts/      연출 대본(원작 스킬 트리거 해석) + 재현 사진(scripts_render/)
    pairing.csv   우리 스킬 ↔ 원작 연출 짝(모양·게이트 근거·확신·겹침 표시)
스킬 효과·수치·이름은 우리 사양 그대로 — 여기선 겉모습·연출만."""
import csv, glob, json, os, re, shutil, subprocess, sys, yaml
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE)); sys.path.insert(0, HERE)
import mdx_anim, w3u
H = os.path.expanduser("~"); WH = H + "/Desktop/구랜디스킨모음/원랜디_구버전_스킨/11_초월"
U = {u["id"]: u["mods"] for u in w3u.parse(HERE + "/원본/풀린것/war3map.w3u")}
J = open(HERE + "/원본/풀린것/war3map.j", encoding="utf8", errors="replace").read().split("\n")
def jline(tr):
    for i, l in enumerate(J, 1):
        if f"function Trig_{tr}_Actions takes" in l: return i
    return ""
CFG = {
 "김경현_상디": dict(roster="초월_김경현_AP", uid="H09G", sheet="Q", title="상디 H09G(정열의 요리사)", attack="SandiAttack_Upgrade",
    pairs=[("게이트_초월_김경현_AP_4266b6e3", "Sandi_skill_Mana2", "마나 게이지 범위(85, 반경 550) ↔ 원작 상디 마나 115 킥3(범위 불·이온 대포)", "상"),
           ("게이트_초월_김경현_AP_b7ae9b41", "Sandi_skill_1", "확률 1/15 범위+단일 ↔ 원작 상디 1/6 킥1(흰 충격파·성 속성 5연출)", "상"),
           ("사장님_초월_김경현_AP_숙련된칼솜씨", "Sandi_skill_Mana2", "액티브 단일 큰 타격(쿨 60) ↔ 원작 마나 킥3의 큰 한 방(재사용)", "중"),
           ("원작능력_초월_김경현_AP_A0X4", "@thunderclap", "20% 단일 스턴 ↔ 원작 상디 평타 1/18 천둥 내려치기(e045·e044 + 스턴)", "중"),
           ("원작트리거_초월_김경현_AP_Snake_Attack_35", "@attack", "평타 범위 35% ↔ 원작 상디 평타 투사체 blackbladiable(날아가는 검은 칼날)", "중")]),
 "양재모_아카이누": dict(roster="초월_양재모_AD", uid="H095", sheet="I", title="아카이누 H095/h051(신 해군원수)", attack="Akainu_Attack",
    pairs=[("게이트_초월_양재모_AD_Law_Skill_2", "Akainu_02", "확률 1/8 범위+단일 ↔ 원작 아카이누 대분화(7.5%, 범위 600+단일)", "상"),
           ("버프게이트_초월_양재모_AD_B03Z", "Akainu_01", "마나 135 게이지 ↔ 원작 아카이누 마나 135 유성(Akainu_01)", "중"),
           ("사장님_초월_양재모_AD_다한증", "Akainu_02_hidden", "체력 게이지 50 ↔ 원작 아카이누 체력 50 분기가 쓰는 대분화 변형", "중"),
           ("사장님_초월_양재모_AD_상호파의최강자", "Akainu_03", "평타 1/8 단일 ↔ 원작 아카이누 1/10 슬램(용암 균열)", "상"),
           ("회수_초월_양재모_AD_f54a123f", "Akainu_01_Shoot", "1/20 범위 ↔ 원작 유성 발사(Akainu_01_Shoot)", "중"),
           ("@평타", "@attack", "평타 ↔ 원작 아카이누 평타 투사체 MagmaHand_2year(용암 주먹)", "중")]),
 "박민석_브룩": dict(roster="초월_박민석_ADAP", uid="H09I", sheet="R", title="브룩 소울 킹 H09I/h04T", attack="BrookAttack",
    pairs=[("사장님_초월_박민석_ADAP_외동의고함", "Brook_Skill_Mana", "마나 125 범위 깡딜+스턴 ↔ 원작 브룩 마나 115 stomp 범위(525)", "상"),
           ("사장님_초월_박민석_ADAP_흑인", "Brook_Skill_1", "범퍼 15% 범위 300 ↔ 원작 브룩 1/7 연주", "중"),
           ("사장님_초월_박민석_ADAP_외동LvDevil", "Brook_Skill_3", "평타 1/10 단일 깡딜+방깍 ↔ 원작 브룩 Skill_3", "중"),
           ("사장님_초월_박민석_ADAP_어지러움", "@attack", "스턴 1/6 단일 ↔ 원작 평타 투사체(스턴 연출 없음 — 투사체만)", "하"),
           ("사장님_초월_박민석_ADAP_공복상태", "-", "체력 85 즉사 ↔ 원작 대응 연출 없음(유지)", "없음"),
           ("사장님_초월_박민석_ADAP_불가항력", "-", "패시브 보잡 — 연출 없음", "없음")]),
}
import math
ATT = {}
for code, trg in re.findall(r"SaveTriggerHandle\(udg_HashAttack,'(\w{4})',0,gg_trg_(\w+)\)", "\n".join(J)):
    ATT[code.lower()] = trg
import jass_sim
from jass_sim import parse as jparse
def trig_gates(attack):
    """공격 트리거에서 하위 트리거 호출마다 그 앞의 if 조건(마나·체력 게이지·확률)을 모은다 → {트리거: (종류, 수)}."""
    fn = jass_sim.FN.get(f"Trig_{attack}_Actions", ""); blk, strs = jparse(fn); out = {}
    def walk(b, conds):
        for st in b:
            if st[0] == "if":
                for c, bb in st[1]: walk(bb, conds + [re.sub(r'"@\d+@"', "S", c)])
                if st[2]: walk(st[2], conds)
            elif st[0] == "loop": walk(st[1], conds)
            elif st[0] == "call":
                m = re.match(r"(?:Conditional)?TriggerExecute\(gg_trg_(\w+)\)", st[1].strip())
                if m and m.group(1) not in out:
                    cs = " ".join(conds); kind = None
                    mm = re.search(r"UNIT_STATE_MANA[^=]*==\s*([\d.]+)", cs)
                    if mm: kind = ("mana", int(float(mm.group(1))))
                    ml = re.search(r"UNIT_STATE_LIFE[^=]*==\s*([\d.]+)", cs)
                    if not kind and ml: kind = ("life", int(float(ml.group(1))))
                    if not kind:
                        rr = re.search(r"GetRandomInt\(1,\s*(\d+)\)\s*(==|<)\s*(\d+)", cs)
                        kind = ("rand", (int(rr.group(1)) / (int(rr.group(3)) if rr.group(2) == "<" else 1))) if rr else ("other", 0)
                    out[m.group(1)] = kind
    walk(blk, []); return out
def our_skills_of(roster):
    g2p = {}
    for mp in glob.glob(ROOT + "/Assets/Data/UnitSkills/*.asset.meta"):
        m = re.search(r"guid: (\w+)", open(mp, encoding="utf8").read()); g2p[m.group(1)] = mp[:-5]
    t = open(f"{ROOT}/Assets/Data/Units/Roster/{roster}.asset", encoding="utf8").read(); blk = re.search(r"skills:\n((?:  - .*\n)+)", t); res = []
    for g in re.findall(r"guid: (\w+)", blk.group(1)) if blk else []:
        pth = g2p.get(g)
        if not pth: continue
        d = yaml.safe_load(open(pth, encoding="utf8").read().split("--- !u!114 &11400000\n", 1)[1])["MonoBehaviour"]; L = d["levels"][0]
        if d["triggerType"] == 2 or not L["effects"]: continue
        tt = d["triggerType"]; kind = ("rand", round(1 / max(L["triggerChance"], 1e-3), 1)) if tt == 0 else ((("mana" if L.get("gaugeKind", 0) == 0 else "life"), L.get("hitCountThreshold", 0)) if tt == 3 else (("active", 0) if tt == 5 else ("cool", L["cooldown"])))
        res.append(dict(asset=os.path.basename(pth)[10:-6], name=d["skillName"], kind=kind, area=any(e["target"] in (2, 4) for e in L["effects"])))
    return res
def auto_pairs(roster, uids):
    atks = [ATT[u.lower()] for u in uids if u.lower() in ATT]; gates = {}
    if not atks:                                                    # 합쳐진 유닛(h04V 쿠잔 초월 = H097 전 해군대장 아오키지): 이름 일부가 같은 공격 코드를 찾는다
        nn = lambda v: re.sub(r"\s+", " ", re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", " ", v or "")).strip()
        for u in uids:
            un = nn(U.get(u, {}).get("unam", ""))
            for code, trg in ATT.items():
                cn = nn(U.get(code, {}).get("unam", "")) or nn(U.get(code.upper(), {}).get("unam", ""))
                if cn and len(cn) >= 6 and cn in un and trg not in atks: atks.append(trg)
    for a in atks: gates.update(trig_gates(a))
    sks = our_skills_of(roster)
    if not gates: return [(k["asset"], "-", "원작 공격 트리거에서 하위 연출을 못 찾음(유지)", "없음") for k in sks] + [("@평타", "@attack", "평타 ↔ 원작 평타 투사체(ua1m)", "중")], atks
    sims = {t: jass_sim.simulate(t) for t in gates}; size = {t: sum(1 for e in sims[t]["events"] if e["op"] == "spawn") for t in gates}
    cand = []
    for sk in sks:
        for t, g in gates.items():
            sc = 0; kk = sk["kind"][0]
            if kk == g[0]:
                sc += 3
                if (kk in ("mana", "life") and abs(sk["kind"][1] - g[1]) <= max(30, 0.35 * g[1])) or (kk == "rand" and 0.4 <= sk["kind"][1] / max(g[1], 1) <= 2.5): sc += 2
            elif kk in ("active", "cool") and g[0] in ("mana", "rand", "other"): sc += 3
            if size[t] == 0: sc -= 6
            if sk["area"] and any(e["op"] == "damage" for e in sims[t]["events"]): sc += 1
            sc += min(size[t], 20) / 20
            cand.append((sc, sk, t, g))
    cand.sort(key=lambda x: -x[0]); used_t, used_s, pairs = set(), set(), []
    for sc, sk, t, g in cand:
        if sc < 3 or sk["asset"] in used_s or t in used_t: continue
        used_s.add(sk["asset"]); used_t.add(t); pairs.append((sk["asset"], t, f"우리 {sk['kind'][0]} {sk['kind'][1]} ↔ 원작 {g[0]} {g[1]:g} ({'범위' if sk['area'] else '단일'}, 연출 {size[t]}개)", "상" if sc >= 5 else "중"))
    for sk in sks:
        if sk["asset"] not in used_s: pairs.append((sk["asset"], "-", "같은 발동 방식의 원작 연출 없음(유지)", "없음"))
    pairs.append(("@평타", "@attack", "평타 ↔ 원작 평타 투사체(ua1m)", "중")); return pairs, atks
def run_row(row):
    roster = row["우리 로스터"]; ids = [x for x in row["원작 ID"].split("/") if x]; title = row["원작 이름"]
    nm = roster.split("_")[1] + "_" + title.split()[0]
    if nm in CFG: C = dict(CFG[nm]); C.update(uids=ids, roster=roster); return nm, C
    pairs, atks = auto_pairs(roster, ids)
    return nm, dict(roster=roster, uid=ids[0], uids=ids, sheet=row["시트 글자"], title=f"{title} {row['원작 ID']}", attack=atks[0] if atks else "", pairs=pairs)

def pack(name, C):
    OUT = f"{H}/GRD_skin_swap/{name}"; os.makedirs(OUT, exist_ok=True)
    fold = next(d for uu in C["uids"] for d in sorted(os.listdir(WH)) if d.lower().startswith(uu.lower() + "_") and os.path.isdir(f"{WH}/{d}"))
    src = f"{WH}/{fold}"; mdir = OUT + "/model"; shutil.rmtree(mdir, ignore_errors=True); shutil.copytree(src, mdir)
    # 모델 json → 시퀀스·지오셋 숨김
    jp = glob.glob(mdir + "/*.json")[0]; sd = json.load(open(jp)); mdx = next(p for p in (H + f"/GRD_motion_trial/{w}/work/{sd['model']}" for w in ("original_skin", "original_vfx")) if os.path.exists(p))
    info = mdx_anim.describe(open(mdx, "rb").read()); seqs = info["sequences"]
    def alpha_at(keys, t, default):
        if not keys or t < keys[0][0]: return default
        v = keys[0][1][0]
        for k in keys:
            if k[0] <= t: v = k[1][0]
        return v
    hid = {}
    for sq in seqs:
        t = sq["start"]; hid[sq["name"]] = [m["mesh"] for m in sd["meshes"] if alpha_at(m.get("geosetAlphaKeys"), t, m.get("geosetAlphaStatic") if m.get("geosetAlphaStatic") is not None else 1.0) * alpha_at(m.get("layerAlphaKeys"), t, 1.0) < 0.05]
    u = U[next(x for x in C["uids"] if x in U)]
    def kind(n):
        l = n.lower()
        if any(w in l for w in ("gold", "portrait", "cinema", "altern", "victory", "channel", "morph", "birth", "decay", "dissipate")): return "Other"
        for k, v in (("stand", "Idle"), ("walk", "Move"), ("attack", "Attack"), ("spell", "Spell"), ("death", "Death")):
            if k in l: return v
        return "Other"
    def rank(n):
        NUM = dict(one="1", two="2", three="3", four="4", five="5", six="6", seven="7", eight="8", nine="9"); l = [NUM.get(x, x) for x in re.sub(r"[^a-z0-9]", " ", n.lower()).split()]; nums = [int(x) for x in l if x.isdigit()]; words = [x for x in l if not x.isdigit() and x not in ("stand", "walk", "attack", "spell", "death")]
        return (1 if words else 0, nums[0] if nums else 1, n)
    clips = []; by = {}
    for i, sq in enumerate(seqs): by.setdefault(kind(sq["name"]), []).append(i)
    ours_of = {}
    for k, idxs in by.items():
        idxs.sort(key=lambda i: rank(seqs[i]["name"]))
        for n_, i in enumerate(idxs):
            if k == "Other": ours_of[i] = None; continue
            base = {"Idle": "Idle", "Move": "Move", "Attack": "Attack", "Spell": "Spell", "Death": "Death"}[k]
            ours_of[i] = base if n_ == 0 else f"{base} {n_ + 1}" + ("(변형 — 필요 없으면 생략)" if k in ("Idle", "Move") else "")
    for i, sq in enumerate(seqs):
        dur = (sq["end"] - sq["start"]) / 1000
        clips.append(dict(index=i, originalName=sq["name"], fbxAction=f"s{i}_" + re.sub(r"[^A-Za-z0-9_-]", "", sq["name"])[:24], startMs=sq["start"], endMs=sq["end"], durationSec=round(dur, 3), loop=sq["looping"], kind=kind(sq["name"]), ourClip=ours_of[i], hiddenMeshes=hid[sq["name"]]))
    atk = [c for c in clips if c["kind"] == "Attack"]
    hit = dict(attackHitSec=u.get("udp1"), backswingSec=u.get("ubs1"), attackCooldownSec=u.get("ua1c"), castPointSec=u.get("ucpt"), range=u.get("ua1r"), missileModel=u.get("ua1m"), missileSpeed=u.get("ua1z"), unitScale=u.get("usca"),
               note="타격 시점 = w3u udp1(공격 시작에서 투사체가 나가거나 피해가 들어가는 시각, 원작 애니 속도 1.0 기준). 우리 공격 속도에 맞춰 Attack 클립 재생 속도를 늘리거나 줄이면 이 시각도 같은 배율로 줄인다. 투사체 유닛이면 이 시각에 발사")
    json.dump(dict(unit=name, originalId=C["uid"], originalName=re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", "", u.get("unam", "")), model=sd["model"], fbxFolder=src, fps=30, clips=clips, hit=hit,
                   ourStates=dict(Idle="Stand(첫 시퀀스)", Move="Walk", Attack="Attack 계열(여러 개면 무작위/순환)", Spell="Spell 계열(스킬 시전 연출)", Death="Death"),
                   notes=["지오셋 숨김: clips[].hiddenMeshes = 그 시퀀스 시작 시각에 알파 0인 메시(변신체·숨은 소품) — 그 시퀀스 재생 중엔 숨긴다. FBX 자체엔 지오셋 알파가 안 실린다", "알파 0 텍스처는 PNG를 불투명으로 고쳐 두었다(BLP 알파 비트 0 결함)"]),
              open(OUT + "/clip_map.json", "w"), ensure_ascii=False, indent=1)
    sd["swap"] = dict(clipMap="../clip_map.json"); json.dump(sd, open(jp, "w"), ensure_ascii=False, indent=1)
    # 연출 대본
    rows = []; files = {}
    for skill, trig, why, conf in C["pairs"]:
        if trig.startswith("@") or trig == "-": continue
        if trig not in [r["trigger"] for r in rows]:
            rows.append(dict(roster=C["roster"], uid=C["uid"], name=C["title"], attack=C["attack"], trigger=trig, depth=1, jline=jline(trig), score=0, dummies={}, effects=[], sleeps=0, lightnings=0, loops=0, particle_models=0, abilities=[],
                             ourSkillOverride=skill))
    os.makedirs(OUT + "/scripts", exist_ok=True)
    env = dict(os.environ, SCENE_TABLE=OUT + "/_rows.json", SCENE_OUT=OUT + "/scripts", SCENE_ALL="1", SCENE_INDEX=OUT + "/scripts_index.csv")
    json.dump(rows, open(OUT + "/_rows.json", "w"), ensure_ascii=False)
    subprocess.run(["/usr/bin/python3", HERE + "/scene_auto.py", "compile"], env=env, capture_output=True)
    # 평타 투사체 대본(손으로): 원작 평타 미사일이 시전자에서 대상으로 날아가 맞는다
    sys.argv = ["x"]; import scene_scripts as SS; SS.OUT = OUT + "/scripts"; sys.argv = ["swap"]
    if u.get("ua1m"):
        mname = os.path.splitext(u["ua1m"])[0]; hitt = float(u.get("udp1") or 0.3)
        mb = SS.model_block(u["ua1m"]); dist = 600; spd = float(u.get("ua1z") or 1200)
        sc = dict(schemaVersion=1, id=f"{name}__평타_투사체", title=f"{C['title']} 평타 투사체 {mname}", ourUnit=C["roster"], origUnit=C["uid"], ourSkill=None, primary=True, source=dict(trigger="(평타 투사체 ua1m)", jFunction="w3u " + C["uid"], jLine="", score=0), quality="양호", durationSec=round(hitt + dist / spd + 1.0, 2), substitutions=[],
                  trigger=dict(cause="평타 — 공격 시작 후 udp1 초에 발사", anchors=dict(caster="(0,0)", target="(600,0)")),
                  timeline=[SS.SPAWN(hitt, "m1", dict(dummyUnit="(투사체)", unitName="평타 미사일", model=mname, modelPath=u["ua1m"], baseScale=1.0, flyHeight=60.0, lifeSec=None, deathSec=0.3), dict(anchor="caster", offset=[0, 0], world=[0, 0]), owner="caster", moveTo=dict(anchor="target", speedPerSec=spd, arc=0), note="원작 평타 투사체(ua1m), 속도 ua1z")], models={mname: mb})
        sc["timeline"][0]["lifeSec"] = round(dist / spd + 0.1, 2); json.dump(sc, open(f"{OUT}/scripts/{name}__평타_투사체.json", "w"), ensure_ascii=False, indent=1)
    if name == "김경현_상디":
        d1, d2 = SS.dummy("e045"), SS.dummy("e044"); mbs = {}
        for d in (d1, d2): mbs[d["model"]] = SS.model_block(d["modelPath"])
        sc = dict(schemaVersion=1, id=f"{name}__천둥_내려치기", title="상디 평타 1/18 천둥 내려치기 (e045 + e044 thunderclap)", ourUnit=C["roster"], origUnit=C["uid"], ourSkill="SkillData_원작능력_초월_김경현_AP_A0X4", primary=False, source=dict(trigger="SandiAttack_Upgrade(1/18 분기)", jFunction="Trig_SandiAttack_Upgrade_Actions", jLine=jline("SandiAttack_Upgrade"), score=0), quality="양호", durationSec=2.0, substitutions=[],
                  trigger=dict(cause="평타 1/18", anchors=dict(target="대상")), timeline=[SS.SPAWN(0, "a", d1, dict(anchor="target", offset=[0, 0], world=[600, 0]), owner="neutral"), SS.SPAWN(0, "b", d2, dict(anchor="target", offset=[0, 0], world=[600, 0]), owner="caster", note="thunderclap 명령 — 범위·스턴은 그 더미 능력"), dict(t=0, op="damage", shape="circle", radius=None, around="target", amount=450000, note="원작 450,000 마법 + 대상 최대체력 10% 계산(PV<200)")], models=mbs)
        json.dump(sc, open(f"{OUT}/scripts/{name}__천둥_내려치기.json", "w"), ensure_ascii=False, indent=1)
    subprocess.run(["/usr/bin/python3", HERE + "/scene_auto_render.py"], env=dict(os.environ, SCENE_OUT=OUT + "/scripts", SCENE_RENDER=OUT + "/scripts_render"), capture_output=True)
    # 짝 표
    fx = {"Akainu_01": "김만경 평타 문(초월평타문.csv)에도 사용", "Akainu_02": "김만경 평타 문(초월평타문.csv)에도 사용", "Akainu_03": "김만경 평타 문(초월평타문.csv)에도 사용"}
    def sinfo(skill):
        pth = f"{ROOT}/Assets/Data/UnitSkills/SkillData_{skill}.asset"
        if not os.path.exists(pth): return ("(에셋 없음)", "", "")
        d = yaml.safe_load(open(pth, encoding="utf8").read().split("--- !u!114 &11400000\n", 1)[1])["MonoBehaviour"]; L = d["levels"][0]
        g = {0: "확률 %.3g" % L["triggerChance"], 1: "쿨 자동", 2: "오라(상시)", 3: "횟수/게이지", 4: "범위 진입", 5: "액티브 버튼"}.get(d["triggerType"], "")
        return d["skillName"][:50], g, f"사거리/반경 {L['range']}"
    with open(OUT + "/pairing.csv", "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f); w.writerow(["우리 스킬 에셋", "우리 스킬 이름", "우리 발동", "우리 범위", "원작 연출(트리거)", "대본 파일", "짝 근거(모양·게이트)", "확신", "겹침 표시"])
        for skill, trig, why, conf in C["pairs"]:
            n_, g_, r_ = sinfo(skill) if not skill.startswith("@") else ("(평타 자체)", "", "")
            scr = ("scripts/" + [x for x in os.listdir(OUT + "/scripts") if x.endswith(".json") and (f"__{trig}" in x or (trig == "@attack" and "평타" in x) or (trig == "@thunderclap" and "천둥" in x))][0]) if trig not in ("-",) and any(x.endswith(".json") and (f"__{trig}" in x or (trig == "@attack" and "평타" in x) or (trig == "@thunderclap" and "천둥" in x)) for x in os.listdir(OUT + "/scripts")) else ""
            w.writerow(["SkillData_" + skill if not skill.startswith("@") else skill, n_, g_, r_, trig, scr, why, conf, fx.get(trig, "")])
    open(OUT + "/README.md", "w").write(f"# {name} — {C['title']} 스킨 교체 시범 (범위 가: 겉모습·동작·연출만)\n- model/: 원작 FBX(30fps 구움)+Textures(알파0 수정)+json · clip_map.json: 시퀀스→우리 클립·타격 시점·숨김 메시 · scripts/: 연출 대본 + scripts_render/ 재현 사진 · pairing.csv: 우리 스킬↔원작 연출 짝\n- 원작 시트 글자: {C['sheet']} · 우리 유닛: {C['roster']}\n")
    print(name, "클립", len(clips), "대본", len([x for x in os.listdir(OUT + "/scripts") if x.endswith(".json")]), "타격", hit["attackHitSec"])


if __name__ == "__main__":
    rows = list(csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig")))
    want = sys.argv[1:]
    for r in rows:
        nm, C = run_row(r)
        if want and nm not in want: continue
        C.setdefault("uids", [C["uid"]]); print("==", nm, [p[1] for p in C["pairs"]]); pack(nm, C)
