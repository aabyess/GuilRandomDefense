"""원랜디 창고 18_원랜디소리 조립 (2026-10-08).

  /usr/bin/python3 Tools/w3x/sound_warehouse_build.py [창고폴더=~/Desktop/구랜디스킨모음/18_원랜디소리]

원작 맵 안 소리 파일(mp3·wav)을 **원본 그대로** 꺼낸다(변환 없음).
index.csv: 파일 · 크기 · 길이(초, mp3 헤더로 추정) · war3map.j 변수(gg_snd_…) · 그 소리를 트리거하는 원작 트리거(함수 이름 접두) · 주인 유닛(H코드 이름, 공격 트리거 사슬로 추정) · 쓰는 능력(w3a 문자열 필드)
이름은 war3map.j의 CreateSound 문자열·w3a/w3u/w3b 문자열·MDX 안 소리 이름(SND)에서 모은다 — 못 찾은 이름은 못 뽑는다.
🔴 원작 저작물(성우·BGM 포함) — 반입은 사장님 판단 대기. Assets엔 안 넣는다.
"""
import collections
import csv
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import w3a                                                           # noqa: E402
import w3u                                                           # noqa: E402
import wts                                                           # noqa: E402
from mpqread import Archive                                          # noqa: E402

DST = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/Desktop/구랜디스킨모음/18_원랜디소리")
SRC = os.path.join(HERE, "원본", "풀린것")
INV = json.load(open(os.path.expanduser("~/GRD_motion_trial/inventory/inventory.json")))
ARC = Archive(os.path.expanduser("~/GRD_motion_trial/_work/ord.mpq"))
S = wts.parse(os.path.join(HERE, "원본", "war3map_new.wts"))
strip = lambda v: re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", "", str(v)).strip()                       # noqa: E731
J = open(os.path.join(SRC, "war3map.j"), encoding="utf8", errors="replace").read()
FN = {m.group(1): m.group(2) for m in re.finditer(r"function (\w+) takes [^\n]*?returns \w+(.*?)endfunction", J, re.S)}
UNITS = {u["id"]: strip(wts.resolve(str(u["mods"].get("unam", "")), S)) for u in w3u.parse(os.path.join(SRC, "war3map.w3u"))}
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
TRIG2H = collections.defaultdict(set)
for code, atk in ATTACK.items():
    for t in closure(atk):
        TRIG2H[t].add(code)


def trig_of(fn):
    m = re.match(r"Trig_(.+?)_(?:Actions|Conditions|Func\d.*)$", fn)
    return m.group(1) if m else ""


# 소리 파일 → 변수
snd_var = collections.defaultdict(set)
for m in re.finditer(r'set (gg_snd_\w+|udg_\w+)=CreateSound\("([^"]+)"', J):
    snd_var[m.group(2).replace("\\\\", "\\").lower()].add(m.group(1))
# 소리 파일 → 능력(w3a 문자열 필드)
abil = collections.defaultdict(list)
for a in w3a.parse(os.path.join(SRC, "war3map.w3a")):
    for mm in a["mods"]:
        if isinstance(mm["value"], str) and re.search(r"\.(mp3|wav)$", mm["value"], re.I):
            abil[os.path.basename(mm["value"].replace("\\", "/")).lower()].append(f"{a['id']}.{mm['field']}")
# 직접 문자열로 재생하는 곳
direct = collections.defaultdict(set)
for n, b in FN.items():
    for s in re.findall(r'"([^"]+\.(?:mp3|wav))"', b, re.I):
        direct[s.replace("\\\\", "\\").lower()].add(n)


def mp3_seconds(data):
    """MPEG 프레임 헤더로 길이 추정(CBR 가정)."""
    i = 0
    if data[:3] == b"ID3":
        i = 10 + ((data[6] << 21) | (data[7] << 14) | (data[8] << 7) | data[9])
    br = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0]
    while i + 4 < len(data):
        if data[i] == 0xFF and (data[i + 1] & 0xE0) == 0xE0:
            b = br[(data[i + 2] >> 4) & 15]
            if b:
                return round((len(data) - i) * 8 / (b * 1000), 2)
        i += 1
    return ""


os.makedirs(DST, exist_ok=True)
rows = []
for r in INV["files"]:
    if not (r["inmap"] and r["ext"] in ("mp3", "wav")):
        continue
    data = ARC.read(r["file"])
    out = os.path.join(DST, os.path.basename(r["file"].replace("\\", "/")))
    open(out, "wb").write(data)
    key = r["file"].replace("\\\\", "\\").lower()
    base = os.path.basename(key.replace("\\", "/"))
    vs = sorted(snd_var.get(key, set()) | snd_var.get(base, set()))
    funcs = set()
    for v in vs:
        funcs |= {n for n, b in FN.items() if v in b}
    funcs |= direct.get(key, set()) | direct.get(base, set())
    trigs = sorted({trig_of(f) for f in funcs} - {""})
    owners = set()
    for t in trigs:
        owners |= TRIG2H.get(t, set())
    rows.append([os.path.basename(out), len(data), mp3_seconds(data) if out.endswith(".mp3") else "", "; ".join(vs[:4]), "; ".join(trigs[:8]), len(trigs),
                 "; ".join(f"{o} {UNITS.get(o, '')[:20]}" for o in sorted(owners)[:5]), len(owners), "; ".join(abil.get(base, [])[:4])])
HEAD = ["파일", "크기(바이트)", "길이(초,추정)", "j 변수", "트리거하는 원작 트리거(최대 8)", "트리거 수", "주인 유닛(추정, 최대 5)", "주인 수", "쓰는 능력(w3a 필드)"]
with open(os.path.join(DST, "index.csv"), "w", newline="", encoding="utf-8-sig") as f:
    wr = csv.writer(f)
    wr.writerow(HEAD)
    wr.writerows(sorted(rows))
open(os.path.join(DST, "README.md"), "w", encoding="utf8").write(f"""# 18_원랜디소리 — 원작(ORD11.089) 맵 안 소리 {len(rows)}개 (원본 그대로)

게임에 안 넣었다(창고). 원작 저작물(성우 녹음 포함)이라 반입은 사장님 판단 대기. 생성: `Tools/w3x/sound_warehouse_build.py`.
- 파일은 MPQ에서 **변환 없이** 꺼낸 mp3/wav. 음악(긴 곡)은 맵 안에 없다(전부 짧은 효과음·대사, 최대 약 63KB).
- `index.csv`: 파일 → war3map.j 변수(`gg_snd_…`) → 그 변수/파일명을 쓰는 원작 트리거 → (공격 트리거 사슬로 추정한) 주인 유닛. 주인은 사슬이 넓게 잡혀 후보일 뿐이다. 트리거 수 0 = 이름을 쓰는 곳을 못 찾음.
- 길이는 MPEG 프레임 비트레이트로 어림(CBR 가정).
""")
print("sounds", len(rows), "with trigger", sum(1 for r in rows if r[5]), "with owner", sum(1 for r in rows if r[7]))
