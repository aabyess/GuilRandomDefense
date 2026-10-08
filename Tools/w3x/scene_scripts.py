"""원작 대표 스킬 연출 「대본」 json 생성(2026-10-09, blender) — 샹크스 폭발 · 시키 함대 소환 · 에넬 엘토르 · 드래곤 폭풍.

  /usr/bin/python3 Tools/w3x/scene_scripts.py   → ~/GRD_scenes/scripts/<id>.json (+ README_SCHEMA.md)
각 대본은 war3map.j 트리거(스테이지 머신: TV.SleepForStage(지연, 다음 스테이지))를 사람이 읽어 풀어 쓴 것이다. 근거 줄·함수는 json의 source에.
형식은 README_SCHEMA.md. 단위 = 워크3 거리(1=0.01m, 우리 지도는 구현담당2 nativeSizes로 환산)·초. 난수는 seed로 고정해 값을 풀어 둠(원작은 매번 다름 — random 필드로 표시).
"""
import json, math, os, random, re, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mdx_anim, w3a, w3u                                            # noqa: E402
from mpqread import Archive                                          # noqa: E402
H = os.path.expanduser("~"); OUT = H + "/GRD_scenes/scripts"; os.makedirs(OUT, exist_ok=True)
T = H + "/GRD_orig_vfx_trial"
U = {u["id"]: u["mods"] for u in w3u.parse(HERE + "/원본/풀린것/war3map.w3u")}
A = {a["id"]: {m["field"]: m["value"] for m in a["mods"]} for a in w3a.parse(HERE + "/원본/풀린것/war3map.w3a")}
ARC = Archive(H + "/GRD_motion_trial/_work/ord.mpq")
clean = lambda s: re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", s or "")
tag = lambda m: os.path.splitext(re.split(r"[\\/]", m)[-1])[0]

def dummy(code, **over):
    """w3u 더미 유닛 사실: 모델·크기·수명(체력÷재생)·사망 지속·비행 높이."""
    u = U[code]; hp = u.get("uhpm"); rg = u.get("uhpr")
    life = (hp / -rg) if isinstance(rg, (int, float)) and rg < 0 and hp else None
    d = dict(dummyUnit=code, unitName=clean(u.get("unam")), model=tag(u["umdl"]), modelPath=u["umdl"], baseScale=u.get("usca", 1.0), flyHeight=u.get("umvh", 0.0), lifeSec=life, deathSec=u.get("udtm", 0.0))
    d.update(over); return d

def model_block(name):
    """모델 폴더·시퀀스·입자(PRE2 전부)."""
    t = tag(name); folder = t if os.path.isdir(f"{T}/{t}") else None
    b = dict(folder=folder, inMap=bool(ARC.read(re.sub(r"\.mdl$", ".mdx", name.replace("\\\\", "\\"), flags=re.I)) or ARC.read(re.sub(r"\.mdl$", ".mdx", os.path.basename(name.replace("\\", "/")), flags=re.I))))
    if not folder: b["note"] = "워크3 기본 모델(맵에 없음) — 변환 불가, 우리 쪽 자체 메시/프리팹으로 대체"; return b
    jd = json.load(open(f"{T}/{t}/{t}.json")); src = next((p for p in (H + f"/GRD_motion_trial/{w}/work/" + jd["model"] for w in ("original_vfx", "transcend_vfx", "grade_extra", "original_skin")) if os.path.exists(p)), None)
    info = mdx_anim.describe(open(src, "rb").read()); b["sequencesMs"] = [dict(name=s["name"], start=s["start"], end=s["end"], loop=s["looping"]) for s in info["sequences"]]
    b["meshCount"] = len(jd["meshes"]); b["jsonPath"] = f"~/GRD_orig_vfx_trial/{t}/{t}.json"
    pre = []
    for p in info["pre2"]:
        n = p["node"]; piv = info["pivots"][n["id"]] if n["id"] < len(info["pivots"]) else [0, 0, 0]
        tex = jd and None
        d = dict(node=n["name"], nodeFlags=n.get("flags"), parentId=n.get("parent"), pivot=list(piv), speed=p["speed"], variation=p["variation"], latitude=p["latitude"], gravity=p["gravity"], lifespanSec=p["lifespan"],
                 emissionRate=p["rate"], width=p["width"], length=p["length"], filterMode={0: "blend", 1: "additive", 2: "modulate", 3: "modulate2x", 4: "alphakey"}.get(p["filter"], p["filter"]), rows=p["rows"], cols=p["cols"],
                 headOrTail={0: "head", 1: "tail", 2: "both"}.get(p["head"], p["head"]), tailLength=p["tail_len"], midTime=p["mid"], colorsRGB01=p["colors"], alpha255=p["alpha"], scale3=p["scale"], squirt=p["squirt"], textureIndex=p["tex"],
                 tracks={k: dict(interp=v.get("interp"), keysMsAbs=[[x[0], list(x[1])] for x in v["keys"]]) for k, v in p["tracks"].items()})
        pre.append(d)
    b["pre2"] = pre; b["pre2Note"] = "keysMsAbs = MDX 전체 시간 ms(시퀀스 start 기준으로 빼서 쓸 것). KP2E=방출률 KP2V=가시(0/1) KP2S=속도 KP2G=중력 KP2L=위도 KP2W/N=폭/길이 …. 모델 공간/월드 공간 구분 비트는 nodeFlags에 있다(원작은 거의 월드 공간 = 부모를 따라가지 않음)"
    return b

SPAWN = lambda t, id, d, at, **k: {**dict(t=round(t, 3), op="spawn", id=id), **d, "at": at, **k}
def write(sc):
    models = {}
    for e in sc["timeline"]:
        if e["op"] == "spawn": models[e["model"]] = None
    for m in models: models[m] = model_block(next(e["modelPath"] for e in sc["timeline"] if e["op"] == "spawn" and e["model"] == m))
    sc["models"] = models
    json.dump(sc, open(f"{OUT}/{sc['id']}.json", "w"), ensure_ascii=False, indent=1)
    print(sc["id"], len(sc["timeline"]), "이벤트", sorted(m for m, b in models.items() if not b.get("folder")), "← 변환불가")

# ═══ 1. 에넬 엘토르 (제한_전법규 · Enel_Mana · j 92407 Trig_Enel_Mana_Actions) ═══
def enel():
    tl = []; T0 = 0.45
    tl.append(SPAWN(0, "C", dummy("e07O"), dict(anchor="target"), anim="birth", timescale=2.0, owner="caster", note="shadow ball 구름 덩어리(birth ×2배속)"))
    tl.append(SPAWN(0, "D", dummy("e07K"), dict(anchor="target"), anim="birth", owner="caster", note="lb_hg2 번개 기둥"))
    tl.append(dict(t=0, op="ramp", id="D", prop="flyHeight", to=600, ratePerSec=150, note="SetUnitFlyHeightBJ(D,600,150)"))
    for i in range(36):
        t = T0 + 0.02 * i
        tl.append(dict(t=round(t, 3), op="set", id="C", scalePercent=110 + 3 * i)); tl.append(dict(t=round(t, 3), op="set", id="D", scalePercent=210 + 11 * i))
        if i % 10 == 0: tl.append(SPAWN(t, f"P{i}", dummy("e07P"), dict(anchor="target"), scalePercent=100 + 11 * i, flyHeight=300 + 3 * i, owner="neutral", note="supershinythingy 광구(10틱마다)"))
    t2 = T0 + 0.02 * 36 + 0.03                                       # 1.20
    tl.append(dict(t=round(t2, 3), op="set", id="C", timescale=1.1)); tl.append(dict(t=round(t2, 3), op="ramp", id="C", prop="flyHeight", to=0, ratePerSec=1000)); tl.append(dict(t=round(t2, 3), op="ramp", id="D", prop="flyHeight", to=400, ratePerSec=1000))
    t3 = t2 + 0.15                                                   # 1.35 낙하 → 폭발
    tl.append(dict(t=round(t3, 3), op="kill", id="C")); tl.append(dict(t=round(t3, 3), op="kill", id="D"))
    tl.append(dict(t=round(t3, 3), op="camera_shake", durationSec=1.35, magnitude=90.0, note="vibration(player,90,1.35)"))
    tl.append(SPAWN(t3, "Q", dummy("e07Q"), dict(anchor="target"), facing=270, owner="neutral", note="lightning strike 낙뢰"))
    tl.append(SPAWN(t3, "N", dummy("e07N"), dict(anchor="target"), owner="caster", note="lightning ball + 스톰프 A0PN"))
    tl.append(dict(t=round(t3, 3), op="damage", shape="circle", radius=800, around="target", amount=4500000, stun=0.45, note="A0PN 2범위 에넬제한 뇌영1 (stomp), 적만"))
    t4 = t3 + 0.07
    for i in range(5):
        t = t4 + 0.10 * i
        if i < 3: tl.append(SPAWN(t, f"R{i}", dummy("e07P"), dict(anchor="target"), scalePercent=350, flyHeight=400, owner="caster", note="광구 3회"))
        for k in range(1, 6):
            tl.append(SPAWN(t, f"M{i}_{k}", dummy("e07M"), dict(anchor="target", polar=dict(radius=130 * i, angleDeg=90 + 70 * k)), facing="random", owner="caster", note="misaka light 파편 고리(반지름 130×i)"))
    tl.sort(key=lambda e: e["t"])
    return dict(schemaVersion=1, id="enel_eltor", title="제한됨 에넬 「엘토르」(뇌영)", ourUnit="제한_전법규", origUnit="h05E 에넬", ourSkill="사장님_제한_전법규_마나스킬(마나 145 마나스킬)",
                source=dict(trigger="Enel_Mana", jFunction="Trig_Enel_Mana_Actions", jLine=92407, mechanism="TV.SleepForStage — 스테이지 0→1(0.45s)→2(0.02s×36 + 0.03)→3(0.15 뒤 낙하)→4(0.07 뒤 0.10 간격 ×5)→5 종료"),
                trigger=dict(cause="마나 145(게이지 가득) — 평타 적중한 대상 위치", anchors=dict(target="평타 대상(기본 공격을 맞은 적) 위치")), durationSec=round(t4 + 0.5 + 1.0, 2), timeline=tl)

# ═══ 2. 샹크스 패기 폭발 (초월_황준석_ADAP · Shanks_skill_5 · j 95509) ═══
def shanks():
    tl = [dict(t=0, op="note", text="트리거 시작: 2.5초 대기(시전 준비) — 이 동안 별도 연출 없음(영웅 시전 동작)"),
          SPAWN(2.5, "GD", dummy("e0GD"), dict(anchor="caster"), owner="neutral", note="lb_hg2 번개 기둥 (수명 2초)"),
          SPAWN(2.5, "GC", dummy("e0GC"), dict(anchor="caster"), timescale=0.7, owner="neutral", note="orgia mode red 붉은 패기(입자 전용 모델)"),
          SPAWN(2.5, "T", dummy("e01T"), dict(anchor="caster"), vertexAlpha=0.85, owner="neutral", note="Lightningbolt 거대 번개(×15) — 워크3 기본 모델, 맵에 없음"),
          SPAWN(2.5, "U", dummy("e01U"), dict(anchor="caster"), scalePercent=400, owner="caster", note="WarStompCaster_nocrack 충격 고리, 스톰프 A0S1(스턴)"),
          dict(t=2.5, op="damage", shape="circle", radius=None, around="caster", stun=True, note="A0S1 2범위 샹크스초월 특성스턴 / A08D 패기 스턴 — 범위는 w3a에서 읽을 것"),
          dict(t=16.75, op="note", text="2.5 + 14.25초 뒤 트리거 재진입(쿨다운 성격)")]
    return dict(schemaVersion=1, id="shanks_haki", title="초월 샹크스 「패기 폭발」(하늘패기)", ourUnit="초월_황준석_ADAP", origUnit="h04U 샹크스 = H08Z 마린포드 정상해전 종결자", ourSkill="더미채널_초월_황준석_ADAP_79행_10000 등(구현담당1 d28c48c9b 패기 계열)",
                source=dict(trigger="Shanks_skill_5", jFunction="Trig_Shanks_skill_5_Actions", jLine=95509, mechanism="스테이지 0(2.50s 대기)→1(더미 4개 생성, 14.25s 뒤 재진입)"),
                trigger=dict(cause="영웅 스킬(udg_Hero[0] = 샹크스) — 시전자 위치", anchors=dict(caster="샹크스 위치")), durationSec=5.0, timeline=tl)

# ═══ 3. 시키 함대 소환 (불멸_고도현 · Shiki_Attack 안 A0T4 분기 · j 107519 Trig_Shiki_Attack_Actions) ═══
def shiki():
    rnd = random.Random(33); tl = []
    for k, (lo, hi) in enumerate(((30, 170), (210, 350))):
        ang = rnd.uniform(lo, hi)
        tl.append(SPAWN(0, f"S{k}", dummy("h07R"), dict(anchor="caster", polar=dict(radius=275, angleDeg=round(ang, 1))), facing="caster", owner="caster", lifeSec=2.25, flyHeight=200,
                        random=dict(angleDeg=[lo, hi]), note="시키의 함대 전함 — UnitApplyTimedLife 2.25s, 대상을 공격(원거리 미사일 NewDirtEXNofire, 사거리 1125·주기 0.75s·선딜 0.22)"))
    for k in range(2):
        for shot in range(3):
            t0 = 0.22 + 0.75 * shot + 0.05 * k
            tl.append(SPAWN(t0, f"B{k}_{shot}", dict(dummyUnit="(투사체)", unitName="전함 미사일", model="NewDirtEXNofire", modelPath="NewDirtEXNofire.mdl", baseScale=1.0, flyHeight=0.0, lifeSec=None, deathSec=0.0), dict(anchor="ship", ship=f"S{k}"),
                            moveTo=dict(anchor="target", speedPerSec=2200, arc=0), note="전함 미사일(ua1z 2200 속도) — 대상에 도달하면 사라지며 NewDirtEXNofire 폭발 입자"))
    tl.append(dict(t=0, op="note", text="분기: 평타 때 GetRandomInt(1,33)==3 이고 시키가 A0T4(!함대) 레벨1 보유일 때만. 전함 2척이 시전자 뒤쪽 호(275 거리)에 나타나 2.25초 동안 대상을 포격. HumanBattleship 모델은 워크3 기본(맵에 없음)이라 우리 쪽 배 메시로 대체"))
    tl.append(dict(t=0, op="note", text="같은 트리거 변형: 평타 1/16 → h0BH/BI/BJ 부유물(!effect2, 돌 투척 CatapultMissile_shiki, 비행 높이 945~1000, 수명 3s) 3개가 대상에게 돌을 던짐. Shiki_SKill_item(아이템 보유) = h07P 배떨구기(HumanBattleship ×2.15, 비행 1225에서 낙하, 1s) + e027 배 폭발(@fire6)"))
    tl.sort(key=lambda e: e["t"])
    return dict(schemaVersion=1, id="shiki_fleet", title="불멸 시키 「함대 소환」(A0T4 !함대)", ourUnit="불멸_고도현", origUnit="h04B 금사자 시키", ourSkill="(우리 쪽 해당 능동 스킬 없음 — PM이 정할 것: 불멸_고도현 액티브/게이트 중 가장 큰 것)",
                source=dict(trigger="Shiki_Attack (A0T4 분기) / Shiki_Lion · Shiki_champa2 · Shiki_SKill_item", jFunction="Trig_Shiki_Attack_Actions", jLine=107519, mechanism="이벤트성(스테이지 머신 아님): 평타 때 즉시 CreateNUnitsAtLoc 2기 + TimedLife 2.25s"),
                trigger=dict(cause="평타 1/33 (A0T4 레벨 1)", anchors=dict(caster="시키 위치", target="평타 대상")), durationSec=3.0, timeline=tl)

# ═══ 4. 드래곤 폭풍 (불멸_정준영 · Dragon_Skill_Mana · j 108056) ═══
def dragon():
    rnd = random.Random(11); tl = []
    tl.append(SPAWN(0, "C", dummy("e06D"), dict(anchor="target"), vertexAlpha=0.4, timescale=1.4, owner="caster", lifeSec=4.0, note="ubercloud2 먹구름(×6.5, 비행 180, 투명도 60%)"))
    tl.append(SPAWN(0, "D", dummy("e06E"), dict(anchor="target"), timescale=1.2, owner="caster", lifeSec=4.0, note="Tranquility(워크3 기본, 맵에 없음 — 고요 나무 이펙트 대체)"))
    t = 0.14
    for n in range(1, 12):
        rad = rnd.uniform(90, 360); ang = rnd.uniform(0, 360)
        tl.append(dict(t=round(t, 3), op="camera_shake", durationSec=3.0, magnitude=0.09, note="vibration(…,3.00,0.09)"))
        tl.append(SPAWN(t, f"L{n}", dummy("e06C"), dict(anchor="target", polar=dict(radius=round(rad, 1), angleDeg=round(ang, 1))), owner="neutral", random=dict(radius=[90, 360], angleDeg=[0, 360]), note="roarthunder 낙뢰 ×6"))
        tl.append(SPAWN(t, f"G{n}", dummy("e06F"), dict(anchor="target", polar=dict(radius=round(rad, 1), angleDeg=round(ang, 1))), owner="neutral", note="NewDirtEXNofire00 지면 폭발"))
        tl.append(dict(t=round(t, 3), op="damage", shape="circle", radius=425, around="spawn:L%d" % n, note="낙뢰 지점마다 425 범위 피해(Func025A)"))
        t += 0.25 if n == 11 else rnd.uniform(0.10, 0.40)
    tl.append(dict(t=round(t, 3), op="kill", id="C")); tl.append(dict(t=round(t, 3), op="kill", id="D"))
    tl.sort(key=lambda e: e["t"])
    return dict(schemaVersion=1, id="dragon_storm", title="불멸 드래곤 「폭풍」(마나스킬 번개구름)", ourUnit="불멸_정준영", origUnit="h04D 몽키.D.드래곤", ourSkill="(불멸_정준영 마나스킬/게이트 — PM이 정할 것)",
                source=dict(trigger="Dragon_Skill_Mana", jFunction="Trig_Dragon_Skill_Mana_Actions", jLine=108056, mechanism="스테이지 0(구름 소환, 0.14s 뒤)→1(11회 반복, 간격 rand 0.10~0.40)→2(0.25s 뒤 구름 제거)"),
                trigger=dict(cause="마나 스킬 — 평타 대상 위치", anchors=dict(target="평타 대상 위치")), durationSec=round(t + 0.5, 2), timeline=tl,
                alternate="Dragon_Skill_1_T(태풍): TornadoElementalSmall·T-dustwave·XuanFeng 토네이도 연출 — 원하면 추가")

for s in (enel(), shanks(), shiki(), dragon()): write(s)
