"""원랜디 창고 16(스킬/이펙트 모델)·15(스킨)용 색인 만들기 (2026-10-08).

  /usr/bin/python3 Tools/w3x/vfx_warehouse_index.py [작업폴더=~/GRD_motion_trial/original_vfx]

입력: original_asset_inventory.py가 낸 ~/GRD_motion_trial/inventory/inventory.json + 원작 w3u·w3a·war3map.j.
산출: <작업폴더>/models.json — 맵 안 모델 전부(캐릭터 182 제외 = 「16」 대상, 캐릭터 = 「15」 대상)의
  파일·모양 묶음·참조처(w3u 더미/투사체 · w3a 능력 · j 트리거)·주인 유닛(H코드+이름)·텍스처 목록.
모양 묶음: 모델 이름+텍스처 이름 키워드 투표(cast_vfx_survey.SHAPES와 같은 표) — 이름 추정이라 틀릴 수 있다.
주인 유닛 찾는 법: ① 능력 → w3u uabi 역색인 ② 더미 유닛(e…)·트리거 이펙트 → 그것을 만드는 j 함수 → `Trig_<이름>_*` 계열 → 공격 트리거 사슬(닫힌 호출자) → H코드.
"""
import collections
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mdx_geo                                                       # noqa: E402
import w3a                                                           # noqa: E402
import w3u                                                           # noqa: E402
import wts                                                           # noqa: E402
from mpqread import Archive                                          # noqa: E402

W = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_motion_trial/original_vfx")
os.makedirs(W, exist_ok=True)
SRC = os.path.join(HERE, "원본", "풀린것")
INV = json.load(open(os.path.expanduser("~/GRD_motion_trial/inventory/inventory.json")))
ARC = Archive(os.path.expanduser("~/GRD_motion_trial/_work/ord.mpq"))
S = wts.parse(os.path.join(HERE, "원본", "war3map_new.wts"))
strip = lambda v: re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", "", v)                                  # noqa: E731
J = open(os.path.join(SRC, "war3map.j"), encoding="utf8", errors="replace").read()
UNITS = {u["id"]: u["mods"] for u in w3u.parse(os.path.join(SRC, "war3map.w3u"))}
ABIL = {a["id"]: {m["field"]: m["value"] for m in a["mods"]} for a in w3a.parse(os.path.join(SRC, "war3map.w3a"))}
uname = lambda uid: strip(wts.resolve(str(UNITS[uid].get("unam", "")), S)).strip() if uid in UNITS else ""   # noqa: E731
aname = lambda c: strip(wts.resolve(str(ABIL.get(c, {}).get("anam", "")), S)).strip()                     # noqa: E731

FN = {m.group(1): m.group(2) for m in re.finditer(r"function (\w+) takes [^\n]*?returns \w+(.*?)endfunction", J, re.S)}
EXEC = re.compile(r"(?:ConditionalTriggerExecute|TriggerExecute|TriggerExecuteBJ|TriggerExecuteWait)\(gg_trg_(\w+)\)")


def family(trig):
    pre = f"Trig_{trig}_"
    return "\n".join(b for n, b in FN.items() if n.startswith(pre))


def closure(root):
    seen, todo = {root}, [root]
    while todo:
        t = todo.pop()
        for y in EXEC.findall(family(t)):
            if y not in seen:
                seen.add(y)
                todo.append(y)
    return seen


ATTACK = dict(re.findall(r"SaveTriggerHandle\(udg_HashAttack,'(\w{4})',0,gg_trg_(\w+)\)", FN["Trig_AttackHashTable_Actions"]))
TRIG2H = collections.defaultdict(set)                                # 트리거 이름 → 주인 H코드들
for code, atk in ATTACK.items():
    for t in closure(atk):
        TRIG2H[t].add(code)
# 함수 이름 → 트리거(`Trig_<트리거>_Actions`/`_FuncNNN` 접두)
TRIGS = sorted({m.group(1) for n in FN for m in [re.match(r"Trig_(.+?)_(?:Actions|Conditions|Func\d.*)$", n)] if m}, key=len, reverse=True)


def trig_of(fn):
    m = re.match(r"Trig_(.+?)_(?:Actions|Conditions|Func\d.*)$", fn)
    return m.group(1) if m else ""


def funcs_with(needle):
    nl = needle.lower()
    return [n for n, b in FN.items() if nl in b.lower()]


# 능력 → 주인(uabi 역색인)
ABIL_OWNERS = collections.defaultdict(list)
for uid, m in UNITS.items():
    for c in str(m.get("uabi", "")).split(","):
        if c.strip():
            ABIL_OWNERS[c.strip()].append(uid)

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
    txt = (name + " " + " ".join(textures)).lower()
    sc = [(len(re.findall(p, txt)), n) for n, p in SHAPES]
    sc = [x for x in sc if x[0] > 0]
    sc.sort(key=lambda x: -x[0])
    return [n for _, n in sc[:2]] or ["미분류"]


# 캐릭터 모델 = w3u umdl 중 id가 h/H/o/n인 것 중 맵 안
byname = {r["name"].lower(): r for r in INV["files"]}


def key_of(n):
    r = byname.get(n.replace("\\\\", "\\").lower())
    return r["file"].lower() if r and r["inmap"] else None


char = set()
for uid, m in UNITS.items():
    if m.get("umdl") and uid[0] in "hHonN":
        k = key_of(m["umdl"])
        if k:
            char.add(k)

files = {}
for r in INV["files"]:
    if r["inmap"] and r["file"].lower().endswith(".mdx"):
        e = files.setdefault(r["file"].lower(), dict(file=r["file"], size=r["size"], names=set(), refs=set(), roles=set()))
        e["names"].add(r["name"])
        e["refs"].update(r["refs"])
        e["roles"].update(r["roles"])

out = []
for k, e in sorted(files.items()):
    is_char = k in char
    data = ARC.read(e["file"])
    mdl = mdx_geo.parse(data) if data and data[:4] == b"MDLX" else None
    tex = [t["path"] for t in mdl["textures"]] if mdl else []
    owners, used_by = set(), []
    # w3u 더미/투사체/몸
    for uid, m in UNITS.items():
        for fld in ("umdl", "ua1m"):
            v = m.get(fld)
            if isinstance(v, str) and key_of(v) == k:
                used_by.append(f"w3u {uid} {uname(uid)[:24]}.{fld}")
                if uid[0] in "hHo":
                    owners.add(uid)
                for fn in funcs_with(f"'{uid}'"):
                    owners |= TRIG2H.get(trig_of(fn), set())
    # w3a 능력 아트
    for c, a in ABIL.items():
        for fld in ("acat", "atat", "asat", "aeat", "amat", "alig"):
            v = a.get(fld)
            if isinstance(v, str) and any(key_of(p.strip()) == k for p in re.split(r"[,;]", v) if p.strip()):
                used_by.append(f"w3a {c} {aname(c)[:24]}.{fld}")
                for o in ABIL_OWNERS.get(c, []):
                    owners.add(o)
                for fn in funcs_with(c):
                    owners |= TRIG2H.get(trig_of(fn), set())
    # j 트리거 이펙트
    stem = os.path.basename(e["file"].replace("\\", "/"))
    stem0 = re.sub(r"\.\w+$", "", stem)
    trigs = sorted({trig_of(fn) for fn in funcs_with(stem0 + ".md")} - {""})
    for t in trigs[:8]:
        used_by.append(f"war3map.j Trig_{t}")
        owners |= TRIG2H.get(t, set())
    out.append(dict(file=e["file"], size=e["size"], kind=("char" if is_char else "fx"), shapes=shape_of(stem0, tex), textures=tex[:12],
                    used_by=used_by[:10], n_used_by=len(used_by), owners=[f"{o} {uname(o)[:26]}" for o in sorted(owners)[:6]], n_owners=len(owners),
                    roles=sorted(e["roles"]), geosets=len(mdl["geosets"]) if mdl else 0,
                    verts=sum(len(g["verts"]) for g in mdl["geosets"]) if mdl else 0, tris=sum(len(g["tris"]) for g in mdl["geosets"]) if mdl else 0,
                    pre2=mdl["counts"]["pre2"] if mdl else 0, ribbons=mdl["counts"]["ribbons"] if mdl else 0))
json.dump(out, open(os.path.join(W, "models.json"), "w"), ensure_ascii=False, indent=1)
print("models", len(out), "char", sum(1 for o in out if o["kind"] == "char"), "fx", sum(1 for o in out if o["kind"] == "fx"),
      "with owners", sum(1 for o in out if o["owners"]), "with used_by", sum(1 for o in out if o["used_by"]))
