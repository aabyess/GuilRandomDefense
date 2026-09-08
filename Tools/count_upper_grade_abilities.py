#!/usr/bin/env python3
"""상위 다섯 등급의 원작 로스터·능력 수를 **재현 가능하게** 센다.

Docs/reference/UPPER_GRADE_ROSTER_FINAL_2026-09-08.md 의 표를 이 스크립트가 낸다.
사람이 다시 세면 뒤집힌다 — 숫자를 인용할 때는 이걸 돌린 출력만 쓴다.

핵심 규칙 — **획득 경로가 등급이다.** 이름에 등급 문자열이 붙은 유닛 수가 아니다.
  초월  = Trig_Eternal_* 가 CreateNUnitsAtLoc 로 지급하는 유닛 (대문자 H 영웅)
  불멸  = Trig_IM_*        영원 = Trig_Forever_*
  전설  = 능력 데이터 atat(조합 결과) 중 이름에 「전설적인」
  제한  = atat 또는 Trig_R_Johab(다른세계 도박) 중 이름에 「제한됨」

능력 세기: uabi 의 살아있는 피해 능력 + 공격 트리거(udg_HashAttack) 재귀 추적의 RRD 함수
+ 그 트리거가 소환하는 더미의 살아있는 능력(3단). 능력ID 단위로 중복 제거.

    python3 Tools/count_upper_grade_abilities.py
"""
import re, sys, collections
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "Tools/w3x"))
import w3u, w3a  # noqa: E402

SRC = ROOT / "Tools/w3x/원본"
if not (SRC / "war3map_new.j").exists():
    sys.exit(f"원본이 없습니다: {SRC} (gitignore 대상)")

txt = (SRC / "war3map_new.j").read_text(encoding="utf-8", errors="replace").replace("\r", "\n")
fns = dict(re.findall(r"function (\w+) takes .*?returns \w+\n(.*?)\nendfunction", txt, re.S))
units = w3u.parse(str(SRC / "war3map_new.w3u"))
raw = {u["id"]: u["mods"].get("unam", "") for u in units}
uab = {u["id"]: u["mods"].get("uabi", "") for u in units}
strip = lambda s: re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", s).strip()
names = {k: strip(v) for k, v in raw.items()}
recs = w3a.parse(str(SRC / "war3map_new.w3a"))
ab = {a["id"]: a["mods"] for a in recs}
base = {a["id"]: a["base"] for a in recs}

UID = r"(?<![A-Za-z0-9])([hH][0-9A-Z]{3})(?![A-Za-z0-9])"
show = {u: g for u, nm in raw.items() for g in ("전설적인", "제한됨") if g in nm and u[0] == "h"}

acq = collections.defaultdict(set)
for a in recs:
    for m in a["mods"]:
        if m["field"] == "atat":
            for t in re.findall(UID, str(m["value"])):
                if t in names:
                    acq[t].add("조합")
for n, b in fns.items():
    if not n.startswith("Trig_"):
        continue
    f = next((k for k in ("Eternal", "Forever", "IM", "R_Johab") if n[5:].startswith(k)), None)
    if not f:
        continue
    for t in re.findall(r"CreateNUnitsAtLoc\w*\(\d+,'([hH]\w{3})'", b):
        if t in names:
            acq[t].add(f)

roster = {
    "초월함": [u for u, f in acq.items() if "Eternal" in f],
    "불멸의": [u for u, f in acq.items() if "IM" in f],
    "영원한": [u for u, f in acq.items() if "Forever" in f],
    "전설적인": [u for u, f in acq.items() if "조합" in f and show.get(u) == "전설적인"],
    "제한됨": [u for u, f in acq.items() if ("조합" in f or "R_Johab" in f) and show.get(u) == "제한됨"],
}

# 「피해 아님」으로 이미 닫힌 필드그룹(FIELD_MEANINGS_RESOLVED.md)
NOTDMG = {"Aroa", "ANbr", "AUfa", "Asph", "Ablo", "ANht", "AUls", "Ainf", "AOae", "Apg2", "AHad"}
DMG = {"Hbh1", "Hbh2", "Hbh3", "Hcl1", "Hca1", "Ncl1", "Ucs1", "Uds1", "Adm1", "Idam", "Ocl1", "Uch1"}


def live(uid):
    return {i for i in uab.get(uid, "").split(",")
            if i and i in ab and base.get(i) not in NOTDMG
            and any(m["field"] in DMG and str(m["value"]) not in ("0", "0.0", "") for m in ab[i])}


reg = dict(re.findall(r"SaveTriggerHandle\(udg_HashAttack,'([hH]\w{3})',0,gg_trg_(\w+)\)", txt))


def walk(name, seen=None, depth=0):
    if seen is None:
        seen = set()
    fn = "Trig_" + name + "_Actions"
    if fn in seen or depth > 8 or fn not in fns:
        return []
    seen.add(fn)
    b = fns[fn]
    out = [(fn, b)]
    for m in re.findall(r"ConditionalTriggerExecute\(gg_trg_(\w+)\)", b):
        out += walk(m, seen, depth + 1)
    for m in re.findall(r"\b(Trig_\w+)\(\)", b):
        if m in fns and m not in seen:
            seen.add(m)
            out.append((m, fns[m]))
    return out


def total(u, depth=0, seen=None):
    """유닛 u 의 (능력ID 집합, 피해 스킬함수 집합). 소환물 것은 주인에게 귀속."""
    if seen is None:
        seen = set()
    if u in seen or depth > 3:
        return set(), set()
    seen.add(u)
    ids = live(u)
    skills = set()
    nodes = walk(reg[u]) if u in reg else []
    for fn, b in nodes:
        if re.search(r"\bRRD\(|UnitDamageTarget", b):
            skills.add(fn)
    body = "\n".join(b for _, b in nodes)
    for s in (set(re.findall(r"CreateNUnitsAtLoc\w*\(\d+,'([heHE]\w{3})'", body))
              | set(re.findall(r"CreateUnit\([^,]*,'([heHE]\w{3})'", body))):
        a, b2 = total(s, depth + 1, seen)
        ids |= a
        skills |= b2
    return ids, skills


print(f"  {'등급':<7}{'유닛':>5}{'공격트리거':>8}{'능력ID':>7}{'스킬함수':>7}{'유닛당':>7}")
for g, ids in roster.items():
    A, S = set(), set()
    for u in ids:
        a, s = total(u)
        A |= a
        S |= s
    rg = sum(1 for u in ids if u in reg)
    print(f"  {g:<7}{len(ids):>5}{rg:>8}{len(A):>7}{len(S):>7}{len(A) / len(ids):>7.2f}")
print("\n  ⚠️ 우리 쪽(Tools/measure_skill_density.py)은 effect 단위라 이 표와 곧바로 나누면 안 된다.")
