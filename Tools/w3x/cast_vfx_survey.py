"""초월 유닛의 「스킬 쓸 때 터지는 이펙트」 목록 조사(2026-10-08, 사장님 지시).

  /usr/bin/python3 Tools/w3x/cast_vfx_survey.py [출력폴더]      (기본 ~/GRD_cast_vfx_trial/)

흐름: 초월 로스터(ATTACK_REINSTALL_TABLE의 H코드) → war3map.j의 Trig_AttackHashTable(H코드→공격 트리거) → 그 트리거가 부르는
스킬 트리거 닫힌 사슬(ConditionalTriggerExecute/TriggerExecute) → 각 트리거 계열(`Trig_<이름>_*` 함수)에서
  ① AddSpecialEffect* 모델 ② CreateNUnits/CreateUnit 로 만드는 더미 유닛(w3u umdl·ua1m·uabi → w3a 아트) ③ UnitAddAbility 능력 아트
를 모은다. 거기에 H코드 유닛 자신의 uabi 능력 아트(.mdl/.mdx 값만 — 조합 능력의 아트 필드는 레시피 데이터라 뺀다).
각 모델은 맵(Archive.read)에서 열어 PRE2/PREM/RIBB/텍스처 애니/플립북/길이를 센다.
산출: <출력폴더>/survey.json · survey.md(표 원본). 문서(Docs/research/TRANSCEND_CAST_VFX_2026-10-08.md)는 이걸 다듬은 것.
"""
import collections
import glob
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import mdx_anim                                                      # noqa: E402
import mdx_geo                                                       # noqa: E402
import w3a                                                           # noqa: E402
import w3u                                                           # noqa: E402
from mpqread import Archive                                          # noqa: E402

OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_cast_vfx_trial")
GRADE = sys.argv[2] if len(sys.argv) > 2 else "초월"                  # 초월(기본)·불멸·영원 — 10-09 확장: 불멸·영원은 ORIGINAL_MATCH_NAMES 표의 원작 유닛 H코드를 쓴다
os.makedirs(OUT, exist_ok=True)
MPQ = os.path.expanduser("~/GRD_motion_trial/_work/ord.mpq")
J = open(os.path.join(HERE, "원본/풀린것/war3map.j"), encoding="utf8", errors="replace").read()
UNITS = {u["id"]: u["mods"] for u in w3u.parse(os.path.join(HERE, "원본/풀린것/war3map.w3u"))}
ABIL = {a["id"]: {m["field"]: m["value"] for m in a["mods"]} for a in w3a.parse(os.path.join(HERE, "원본/풀린것/war3map.w3a"))}
ARC = Archive(MPQ)

# ───────── JASS 함수 ─────────
FN = {}
for m in re.finditer(r"function (\w+) takes [^\n]*?returns \w+(.*?)endfunction", J, re.S):
    FN[m.group(1)] = m.group(2)


def family(trig):
    """트리거 이름 → 그 계열 함수 본문 이어붙임(`Trig_<이름>_` 접두. Robin_ ≠ Robine_)."""
    pre = f"Trig_{trig}_"
    return "\n".join(b for n, b in FN.items() if n.startswith(pre))


EXEC = re.compile(r"(?:ConditionalTriggerExecute|TriggerExecute|TriggerExecuteBJ|TriggerExecuteWait)\(gg_trg_(\w+)\)")


def closure(root):
    """공격 트리거에서 부르는 스킬 트리거 사슬 → {트리거: 깊이}."""
    seen, todo = {root: 0}, [root]
    while todo:
        t = todo.pop()
        for y in EXEC.findall(family(t)):
            if y not in seen:
                seen[y] = seen[t] + 1
                todo.append(y)
    return seen


def gates_of(trig_names):
    """사슬 안 각 트리거를 부르는 줄 앞의 `if` 조건에서 발동 문턱을 읽는다 → {트리거: [("life",40)|("mana",125)|("rand",10)|("buff",...)]}."""
    out = collections.defaultdict(list)
    for t in trig_names:
        lines = family(t).split("\n")
        for i, ln in enumerate(lines):
            m = re.search(r"(?:ConditionalTriggerExecute|TriggerExecute|TriggerExecuteBJ)\(gg_trg_(\w+)\)", ln)
            if not m:
                continue
            conds = []
            for back in lines[max(0, i - 8):i][::-1]:
                if "ConditionalTriggerExecute" in back or "TriggerExecute" in back:
                    break
                if re.match(r"\s*(?:else)?if ", back) or back.lstrip().startswith("if "):
                    for r in re.findall(r"GetRandomInt\(1,(\d+)\)==\d+", back):
                        conds.append(("rand", int(r)))
                    for r in re.findall(r"UNIT_STATE_LIFE,[^)]*\)==(\d+)", back):
                        conds.append(("life", int(r)))
                    for r in re.findall(r"UNIT_STATE_MANA,[^)]*\)==(\d+)", back):
                        conds.append(("mana", int(r)))
            out[m.group(1)] += conds
    return out


# H코드 → 공격 트리거
ATTACK = {}
for code, trg in re.findall(r"SaveTriggerHandle\(udg_HashAttack,'(\w{4})',0,gg_trg_(\w+)\)", FN["Trig_AttackHashTable_Actions"]):
    ATTACK[code] = trg

# ───────── 이펙트 수집 ─────────
ART_FIELDS = [("acat", "시전자"), ("atat", "대상"), ("asat", "특수"), ("aeat", "효과"), ("amat", "투사체"), ("alig", "번개")]
MODEL_RE = re.compile(r"\.(mdl|mdx)$", re.I)
STOCK = re.compile(r"^(abilities|units|objects|buildings|doodads|environment|ui|spells)\\", re.I)


JUNK = re.compile(r"(^|\\)\.mdl$|dummy|spawnmodels|^\.mdx$", re.I)          # 빈 이름·더미(안 보이는 몸)·워크3 사망 연출 — 이펙트가 아니다


def abil_art(code):
    out = []
    a = ABIL.get(code, {})
    for f, role in ART_FIELDS:
        v = a.get(f)
        if isinstance(v, str) and v and (MODEL_RE.search(v) or f == "alig"):
            for s in re.split(r"[,;]", v):
                s = s.strip()
                if s and not JUNK.search(s):
                    out.append((s, f"능력 {code} {a.get('anam', '')} · {role}({f})"))
    return out


def effects_in(text):
    """(모델, 붙는곳, 호출 꼴) 목록."""
    out = []
    for m in re.finditer(r"AddSpecialEffect(\w*)\(([^\n]*?)\)\s*(?:set|call|endif|else|\n|$)", text):
        kind, args = m.group(1) or "Loc", m.group(2)
        strs = re.findall(r'"([^"]*)"', args)
        model = next((s for s in strs if MODEL_RE.search(s)), None)
        attach = next((s for s in strs if s and not MODEL_RE.search(s)), "")
        if model and not JUNK.search(model):
            out.append((model, attach, "AddSpecialEffect" + kind))
    for m in re.finditer(r'AddLightning\w*\("(\w+)"', text):
        out.append((m.group(1), "", "AddLightning"))
    return out


UNIT_RE = re.compile(r"(?:CreateNUnitsAtLoc|CreateUnitAtLoc|CreateUnit|CreateNUnitsAtLocFacingLocBJ|CreateNUnitsAtLoc)\w*\(\s*(?:\d+\s*,\s*)?(?:Player\([^)]*\)|\w+)?\s*,?\s*'(\w{4})'")


def units_in(text):
    codes = set(re.findall(r"Create\w*Unit\w*\([^'\n]*?'(\w{4})'", text))
    return sorted(c for c in codes if c in UNITS)


def unit_art(code):
    u = UNITS[code]
    out = []
    mdl = u.get("umdl")
    if mdl and MODEL_RE.search(mdl) and not JUNK.search(mdl):
        out.append((mdl, f"더미 {code} {u.get('unam', '')} · 몸(umdl) ×{u.get('usca', 1.0):.2f}"))
    a1 = u.get("ua1m")
    if a1 and MODEL_RE.search(a1) and not JUNK.search(a1):
        out.append((a1, f"더미 {code} {u.get('unam', '')} · 투사체(ua1m)"))
    for ab in str(u.get("uabi", "")).split(","):
        ab = ab.strip()
        if ab in ABIL:
            out += [(m, s) for m, s in abil_art(ab)]
    return out


# ───────── MDX 분석 ─────────
MDX_CACHE = {}


def read_model(name):
    mx = name.replace("\\\\", "\\")
    cand = [mx, re.sub(r"\.mdl$", ".mdx", mx, flags=re.I)]
    cand += [c.split("\\")[-1] for c in cand]
    for c in cand:
        try:
            d = ARC.read(c)
        except Exception:
            d = None
        if d:
            return d
    return None


SHAPES = [
    ("고리·충격파", r"ring|wave|shock|nova|stomp|circle|halo|spiral|vortex"),
    ("초승달·베기", r"slash|moon|crescent|blade|knife|sword|katana|cut|getsuga"),
    ("땅폭발·가시", r"explo|bomb|boom|crack|dust|rock|stone|spike|ground|quake|meteor|crater"),
    ("섬광·번개", r"flash|glow|flare|light|beam|zap|bolt|lightning|thunder|star|spark|shine|impact"),
    ("투사체", r"missile|arrow|ball|shot|bullet|gun|orb|fireball|cannon|rocket"),
    ("오라·버프", r"aura|buff|shield|armor|heal|blessing|target|invis"),
    ("불·얼음·바람", r"fire|flame|ice|frost|freez|wind|storm|water|wood|tornado"),
]


def shape_of(name, textures):
    low = name.lower()
    votes = collections.Counter()
    for sh, rx in SHAPES:
        if re.search(rx, low):
            votes[sh] += 3
        for t in textures:
            if re.search(rx, t.lower()):
                votes[sh] += 1
    return [s for s, _ in votes.most_common(2)] or ["미분류"]


def analyze(name):
    if name in MDX_CACHE:
        return MDX_CACHE[name]
    r = dict(model=name)
    if not MODEL_RE.search(name):
        r.update(kind="번개", inmap=None, shape=["섬광·번개"])
        MDX_CACHE[name] = r
        return r
    data = read_model(name)
    if data is None:
        r.update(inmap=False, stock=bool(STOCK.match(name)), shape=shape_of(name, []))
        MDX_CACHE[name] = r
        return r
    try:
        geo = mdx_geo.parse(data)
        info = mdx_anim.describe(data)
        texs = [t["path"] or f"repl{t['replaceable']}" for t in geo["textures"]]
        seqs = info["sequences"]
        dur = max((s["end"] - s["start"] for s in seqs), default=0) / 1000.0
        layer_tex_anim = sum(1 for la in info["layer_anims"] if la["tracks"])
        r.update(inmap=True, textures=texs, geosets=len(geo["geosets"]), pre2=len(info["pre2"]), prem=len(info["prem"]), ribbons=len(info["ribbons"]),
                 txan=len(info["txan"]), layer_anims=layer_tex_anim, geoa=len(info["geoa"]), seq=[s["name"] for s in seqs][:6], dur=round(dur, 2),
                 flipbook=sum(1 for p in info["pre2"] if p["rows"] * p["cols"] > 1),
                 particle_tex=[texs[p["tex"]] for p in info["pre2"] if p["tex"] < len(texs)][:6],
                 shape=shape_of(name, texs))
    except Exception as e:                                           # 하나가 깨져도 계속
        r.update(inmap=True, error=str(e)[:80], shape=shape_of(name, []))
    MDX_CACHE[name] = r
    return r


# ───────── 로스터·우리 스킬 ─────────
def unq(v):
    v = v.strip()
    if v.startswith('"'):
        v = v[1:-1]
        v = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), v)
        return re.sub(r"\\x([0-9A-Fa-f]{2})", lambda m: chr(int(m.group(1), 16)), v)
    return v


g2p = {}
for mf in glob.glob(os.path.join(ROOT, "Assets/Data/UnitSkills/*.asset.meta")):
    g2p[re.search(r"guid: (\w+)", open(mf, encoding="utf8").read()).group(1)] = mf[:-5]
CSV = {}
import csv                                                           # noqa: E402
for row in csv.reader(l for l in open(os.path.join(ROOT, "Docs/research/SKILL_VFX_MAPPING.csv"), encoding="utf8") if not l.startswith("#")):
    if row and row[0].startswith("Assets/"):
        CSV[os.path.basename(row[0])] = row


def skill_gates(name):
    g = []
    n = re.search(r"(?:게이지|게이트)\s*(\d+)", name)
    if re.search(r"체력스킬|LIFE|체력게이지|체력 게이지", name):
        g.append(("life", int(n.group(1)) if n else None))
    if re.search(r"마나스킬|MANA|마나게이지|마나 게이지", name):
        g.append(("mana", int(n.group(1)) if n else None))
    for r in re.findall(r"1/(\d+)", name):
        g.append(("rand", int(r)))
    for r in re.findall(r"(\d+)%", name):
        if re.search(r"평타|발동", name) and 0 < int(r) < 100:
            g.append(("rand", round(100 / int(r))))
    return g


def our_skills(roster_path):
    t = open(roster_path, encoding="utf8").read()
    blk = re.search(r"skills:\n((?:  - .*\n)+)", t)
    res = []
    for g in re.findall(r"guid: (\w+)", blk.group(1)) if blk else []:
        p = g2p.get(g)
        if not p:
            continue
        st = open(p, encoding="utf8").read()
        sn = re.search(r"skillName: ((?:\"(?:[^\"\\]|\\.)*\"|[^\n]*)(?:\n    [^\n]*)*)", st)
        name = unq(re.sub(r"\n\s+", "", sn.group(1))) if sn else ""
        stem = os.path.basename(p)[:-6]
        row = CSV.get(os.path.basename(p))
        res.append(dict(gates=skill_gates(name), asset=stem.replace("SkillData_", ""), skillName=name, csv_trig=(row[2] if row else ""),
                        cur_hit=(row[5] if row else ""), cur_area=(row[6] if row else ""), cur_caster=(row[7] if row else ""),
                        text=(stem + " " + st).lower()))
    return res


def pair_skill(trig, skills):
    t = trig.lower()
    hits = []
    for s in skills:
        if re.search(r"(?<![a-z0-9])" + re.escape(t) + r"(?![a-z0-9])", s["csv_trig"].lower().replace(",", " ")) or (f"trig_{t}" in s["text"]) or \
                re.search(r"(?<![a-z0-9])" + re.escape(t) + r"(?![a-z0-9])", s["asset"].lower()):
            hits.append(s)
    return hits


def pair_by_gate(gates, skills, taken):
    """이름으로 못 짝지은 트리거 → 같은 발동 문턱(종류+수치)을 가진 우리 스킬. 반환 [(스킬, 근거)]."""
    out = []
    for kind, num in gates:
        for sk in skills:
            if sk["asset"] in taken:
                continue
            for k2, n2 in sk["gates"]:
                if k2 == kind and n2 == num:
                    out.append((sk, f"문턱 일치({kind} {num})"))
    return out


# ───────── 본 조사 ─────────
roster_rows = [l.rstrip("\n").split("\t") for l in open(os.path.join(ROOT, "Docs/research/ATTACK_REINSTALL_TABLE_2026-10-07.tsv"), encoding="utf8")][1:]
if GRADE != "초월":
    _m = [l.rstrip("\n").split("\t") for l in open(os.path.join(ROOT, "Docs/research/ORIGINAL_MATCH_NAMES_2026-10-07.tsv"), encoding="utf8")][1:]
    roster_rows = [[GRADE, r[0], r[1] + " (" + r[2] + ")"] for r in _m if r[0].startswith(GRADE + "_")]
result = []
for grade, roster, corr, *_ in roster_rows:
    if grade != GRADE:
        continue
    rp = os.path.join(ROOT, f"Assets/Data/Units/Roster/{roster}.asset")
    gname = unq(re.search(r"unitName: (.*)", open(rp, encoding="utf8").read()).group(1))
    codes = [c for c in re.findall(r"\b[Hh][0-9A-Za-z]{3}\b", corr.split("(")[0])] if "대응 없음" not in corr else []
    codes = [c.upper() if c.upper() in ATTACK else c for c in codes]
    skills = our_skills(rp)
    unit = dict(roster=roster, gameName=gname, codes=codes, corr=corr, ourSkills=[{k: v for k, v in s.items() if k != "text"} for s in skills], families=[])
    for code in codes:
        atk = ATTACK.get(code)
        fam = dict(code=code, attack=atk, triggers=[])
        # H코드 유닛 자신의 능력(uabi)
        own = []
        for ab in str(UNITS.get(code, {}).get("uabi", "")).split(","):
            own += abil_art(ab.strip())
        fam["own"] = [dict(model=m, src=s, **{k: v for k, v in analyze(m).items() if k != "model"}) for m, s in own]
        if atk:
            clo = closure(atk)
            gmap = gates_of(clo)
            taken = set()
            for trig, depth in sorted(clo.items(), key=lambda x: x[1]):
                body = family(trig)
                effs = [dict(model=m, src=f"{kind}({att or '위치'})", **{k: v for k, v in analyze(m).items() if k != "model"}) for m, att, kind in effects_in(body)]
                for uc in units_in(body):
                    if uc.lower() == code.lower():
                        continue
                    for m, s in unit_art(uc):
                        effs.append(dict(model=m, src=s, **{k: v for k, v in analyze(m).items() if k != "model"}))
                for ab in re.findall(r"UnitAddAbility\([^,]*,'(\w{4})'\)", body):
                    for m, s in abil_art(ab):
                        effs.append(dict(model=m, src=s, **{k: v for k, v in analyze(m).items() if k != "model"}))
                # 중복 제거(같은 모델·출처)
                seen, uniq = set(), []
                for e in effs:
                    k = (e["model"], e["src"])
                    if k not in seen:
                        seen.add(k)
                        uniq.append(e)
                pairs = [(p, "이름") for p in pair_skill(trig, skills)]
                if not pairs:
                    pairs = pair_by_gate(gmap.get(trig, []), skills, taken)[:2]
                for p, _ in pairs:
                    taken.add(p["asset"])
                fam["triggers"].append(dict(trigger=trig, depth=depth, gate=gmap.get(trig, []), ours=[p["asset"] + " ｜ " + p["skillName"] + f" [{why}]" for p, why in pairs],
                                            cur=[dict(hit=p["cur_hit"], area=p["cur_area"], caster=p["cur_caster"]) for p, _ in pairs], effects=uniq,
                                            nodmg_dummy=len(units_in(body))))
        unit["families"].append(fam)
    result.append(unit)

json.dump(result, open(os.path.join(OUT, "survey.json"), "w"), ensure_ascii=False, indent=1)
# 요약
tot = collections.Counter()
for u in result:
    for f in u["families"]:
        for t in f["triggers"]:
            for e in t["effects"]:
                tot["inmap" if e.get("inmap") else ("stock" if e.get("stock") else "missing")] += 1
print("유닛", len(result), "이펙트 줄", dict(tot), "모델 종류", len(MDX_CACHE))


# ───────── 문서 쓰기 ─────────
def mark(e):
    if e.get("inmap"):
        return "✅"
    if e.get("inmap") is None:
        return "—(번개)"
    return "워크3 기본(맵 밖)" if e.get("stock") else "❌ 맵에 없음"


def parts(e):
    if not e.get("inmap"):
        return ""
    bits = []
    if e.get("pre2"):
        bits.append(f"PRE2×{e['pre2']}" + (f"(플립북{e['flipbook']})" if e.get("flipbook") else ""))
    if e.get("prem"):
        bits.append(f"PREM×{e['prem']}")
    if e.get("ribbons"):
        bits.append(f"리본×{e['ribbons']}")
    return " ".join(bits) or "없음"


def tanim(e):
    if not e.get("inmap"):
        return ""
    bits = []
    if e.get("txan"):
        bits.append(f"UV이동×{e['txan']}")
    if e.get("layer_anims"):
        bits.append(f"층알파/번호×{e['layer_anims']}")
    if e.get("geoa"):
        bits.append(f"지오셋알파×{e['geoa']}")
    return " ".join(bits) or "없음"


lines = []
w = lines.append
w("# 초월 유닛 스킬 시전 이펙트 — 원작 MDX 목록 (2026-10-08)\n")
w("blender 세션 조사(PM 지시·사장님 요청). **코드·Assets 변경 없음.** 생성: `Tools/w3x/cast_vfx_survey.py`(재실행 가능) → `~/GRD_cast_vfx_trial/survey.json`.\n")
w("## 읽는 법·방법·한계\n")
w("- **유닛 ↔ 원작**: `ATTACK_REINSTALL_TABLE_2026-10-07.tsv`의 H코드(초월 25종 중 21종에 대응). 대응 없음 4종(김건·박민석·엄태웅·이재윤)은 원작 이펙트가 없어 맨 아래 따로 적는다.")
w("- **원작 스킬 = 트리거 사슬**: `war3map.j`의 `Trig_AttackHashTable`이 H코드 → 공격 트리거(`gg_trg_X_Attack`)를 저장한다. 그 트리거가 `ConditionalTriggerExecute`/`TriggerExecute`로 부르는 스킬 트리거를 끝까지 따라가 (호출자 닫힌 사슬) 각 계열(`Trig_<이름>_*` 함수)에서 이펙트를 뽑았다. **사슬 밖에서 도는 트리거(타이머 등)는 못 잡는다**[한계].")
w("- **출처 3종**: ① `AddSpecialEffect*` 모델 ② `CreateNUnits…('xxxx')`로 만드는 더미 유닛의 `umdl`(몸=이펙트)·`ua1m`(투사체)·`uabi`(더미 능력의 w3a 아트 필드) ③ `UnitAddAbility` 능력 아트. 그리고 H코드 유닛 자신의 `uabi` 능력 아트(「own」). w3a 아트는 `.mdl/.mdx` 값만(조합 능력 레시피 데이터 제외).")
w("- **맵 안**: `Archive.read` 성공이면 ✅. 실패한 것은 `Abilities\\`·`Units\\` 등 워크3 기본 경로면 「기본(맵 밖)」, 그 외는 「❌ 맵에 없음」(보호 맵이라 이름이 바뀌었거나 외부 파일일 수 있다 — 원인 미확인).")
w("- **모양**: 모델 이름+텍스처 이름 키워드 투표(고리·초승달·땅폭발·섬광·투사체·오라·원소). **이름 추정이라 틀릴 수 있다** — 시범 3종은 실제로 그려 확인했다(§시범 문서 참고).")
w("- **입자/텍스처 애니**: MDX의 PRE2(파티클 방출기, 플립북=rows×cols>1)·PREM·RIBB(리본) 개수, TXAN(UV 이동)·재질 층 애니(KMTA/KMTF/KTAT) 유무. **Unity에서 그대로 안 나오는 부분**이다 — 다시 짜야 한다.")
w("- **우리 스킬 짝**: ①이름(에셋 파일명·`원작_트리거` 칸·설명의 `Trig_X`)이 같으면 「이름」 ②못 찾으면 발동 문턱(체력게이지/마나게이지/1÷확률)이 같은 우리 스킬을 「문턱 일치」. 초월 로스터 스킬은 대부분 **사장님 신규 사양**(이름·수치가 원작과 다름)이라 **짝이 비는 게 정상**이다. 비면 「(짝 없음)」 — 사장님/PM이 어떤 원작 이펙트를 어떤 스킬에 붙일지 정해야 한다.\n")

# 요약 표
allm = collections.Counter()
for u in result:
    for f in u["families"]:
        for t in f["triggers"]:
            for e in t["effects"]:
                allm[e["model"]] += 1
w("## 요약\n")
inmap = sum(1 for m in MDX_CACHE.values() if m.get("inmap"))
stock = sum(1 for m in MDX_CACHE.values() if m.get("inmap") is False and m.get("stock"))
miss = sum(1 for m in MDX_CACHE.values() if m.get("inmap") is False and not m.get("stock"))
w(f"- 원작 초월 21종의 시전 이펙트에 쓰인 서로 다른 모델 **{len(MDX_CACHE)}종**: 맵 안 ✅ **{inmap}** · 워크3 기본 {stock} · 맵에 없음 {miss}.")
shape_c = collections.Counter(m["shape"][0] for m in MDX_CACHE.values() if m.get("inmap"))
w("- 맵 안 모델의 모양(1순위): " + " · ".join(f"{k} {v}" for k, v in shape_c.most_common()) + ".")
w(f"- 입자(PRE2/PREM) 있는 모델 {sum(1 for m in MDX_CACHE.values() if m.get('pre2') or m.get('prem'))} · 리본 있는 모델 {sum(1 for m in MDX_CACHE.values() if m.get('ribbons'))} · UV/층 애니 있는 모델 {sum(1 for m in MDX_CACHE.values() if m.get('txan') or m.get('layer_anims'))}.")
w("- 여러 유닛이 같이 쓰는 모델(5곳 이상): " + ", ".join(f"`{m}`×{c}" for m, c in allm.most_common(12) if c >= 5) + "\n")

for u in result:
    w(f"## {u['roster']} — 「{u['gameName']}」\n")
    if not u["codes"]:
        w(f"원작 대응: {u['corr']} → **원작 이펙트 없음**. 우리 스킬 {len(u['ourSkills'])}개: " + " / ".join(s["skillName"][:30] for s in u["ourSkills"]) + "\n")
        continue
    w(f"원작 대응: {u['corr']} (H코드 {', '.join(u['codes'])}). 우리 스킬 {len(u['ourSkills'])}개 — " + " / ".join(s["skillName"][:26] for s in u["ourSkills"]) + "\n")
    pairedassets = set()
    for f in u["families"]:
        for t in f["triggers"]:
            for o in t["ours"]:
                pairedassets.add(o.split(" ｜ ")[0])
    for f in u["families"]:
        w(f"### {f['code']} · 공격 트리거 `{f['attack']}`\n")
        if f["own"]:
            w("**유닛 자신의 능력 아트(own)**: " + ", ".join(f"`{e['model']}`({mark(e)})" for e in f["own"]) + "\n")
        for t in f["triggers"]:
            gate = ", ".join(f"{k} {n}" for k, n in t["gate"]) or "—"
            pair = "; ".join(t["ours"]) or "(짝 없음)"
            w(f"**`{t['trigger']}`** (사슬 깊이 {t['depth']} · 발동 조건 참고: {gate}) → 우리 스킬: {pair}")
            if not t["effects"]:
                w("\n- 이 트리거 계열엔 `AddSpecialEffect`·더미 유닛·능력 아트가 없다(피해만 주거나 다른 트리거에 맡김).\n")
                continue
            w("\n| 이펙트 파일 | 출처 | 맵 안 | 모양 | 입자 | 텍스처·층 애니 | 길이(초) |\n|---|---|---|---|---|---|---|")
            for e in t["effects"]:
                w(f"| `{e['model']}` | {e['src']} | {mark(e)} | {'·'.join(e.get('shape', []))} | {parts(e)} | {tanim(e)} | {e.get('dur', '')} |")
            w("")
        unp = [s for s in u["ourSkills"] if s["asset"] not in pairedassets]
    w(f"_우리 스킬 중 원작 이펙트와 짝지어지지 않은 것: {len([s for s in u['ourSkills'] if s['asset'] not in pairedassets])}개 (사장님 신규 사양이거나 패시브·오라 — 시전 이펙트가 필요하면 위 목록에서 골라 붙인다)._\n")

open(os.path.join(OUT, "survey.md"), "w", encoding="utf8").write("\n".join(lines))
print("md", len(lines), "줄")
