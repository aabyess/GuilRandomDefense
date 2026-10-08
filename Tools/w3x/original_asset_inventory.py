"""원작 맵(ORD11.089)에 들어 있는 커스텀 파일 목록 조사 (2026-10-08, PM 지시 「원랜디 창고 ①」).

  /usr/bin/python3 Tools/w3x/original_asset_inventory.py [출력폴더=~/GRD_motion_trial/inventory]

원작은 보호 맵이라 `(listfile)`이 없어 **이름을 알아야** 꺼낸다. 그래서 이름을 여러 곳에서 모은다:
  ① 맵 데이터 파일(w3u w3a w3t w3b w3d w3q w3h w3i wts doo, war3mapSkin/Misc/Extra.txt)의 문자열 전부에서 파일 경로꼴
  ② war3map.j 문자열 리터럴(AddSpecialEffect·사운드·음악 …)
  ③ 찾은 MDX의 텍스처(TEXS)·모델 입자(PREM)·리본 텍스처 — 재귀로 따라간다
이름을 읽어 보고(MPQ 해시 조회) 되면 「맵 안 커스텀」, 안 되면 「맵 밖(워크3 기본)」 또는 「맵에 없음」.
MPQ 블록 표의 존재하는 파일 수와 비교해 **이름을 못 찾은 파일**(상한이 아닌 하한 근거) 수도 센다.
역할(유닛 모델·투사체·스킬/이펙트·아이콘·텍스처·소리·음악·로딩 화면 …)은 어느 필드/파일에서 이름을 얻었는지로 가른다.
산출: <출력폴더>/inventory.json(이름·확장자·역할 모음·맵안여부·크기·참조처) · 터미널에 집계.
"""
import collections
import json
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import mdx_geo                                                       # noqa: E402
import w3a                                                           # noqa: E402
import w3u                                                           # noqa: E402
import wts                                                           # noqa: E402
from mpqread import Archive                                          # noqa: E402

OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_motion_trial/inventory")
os.makedirs(OUT, exist_ok=True)
SRC = os.path.join(HERE, "원본", "풀린것")
ARC = Archive(os.path.expanduser("~/GRD_motion_trial/_work/ord.mpq"))
EXT = r"mdx|mdl|blp|tga|wav|mp3|mid|midi|flac|ogg|pcx|dds|slk|txt|fdf|toc|w3a|w3u|w3e|jpg|bmp|ini|mpq|j|ai|lua"
PATH_RE = re.compile(rb"[ -~]{0,200}?\.(?:%s)\b" % EXT.encode(), re.I)
STR_RE = re.compile(r'"((?:[^"\\]|\\.)*)"')


def norm(p):
    p = p.replace("\\\\", "\\").replace("/", "\\").strip().strip('"').strip()
    return p


def read_any(name):
    n = norm(name)
    cands = [n]
    if n.lower().endswith(".mdl"):
        cands.append(n[:-4] + ".mdx")
    if n.lower().endswith(".mdx"):
        cands.append(n[:-4] + ".mdl")
    if not re.search(r"\.\w{2,4}$", n):
        cands += [n + ".blp", n + ".mdx", n + ".tga"]
    cands += [c.split("\\")[-1] for c in list(cands)]
    for c in cands:
        try:
            d = ARC.read(c)
        except Exception:                                            # noqa: BLE001
            d = None
        if d:
            return c, d
    return None, None


NAMES = {}                                                           # 정규화 이름(소문자) → dict(name, refs:set, roles:set)


def add(name, role, ref=""):
    n = norm(name)
    if not n or len(n) > 200 or not re.search(r"\.\w{2,4}$", n):
        return
    e = NAMES.setdefault(n.lower(), dict(name=n, roles=set(), refs=set()))
    e["roles"].add(role)
    if ref:
        e["refs"].add(ref)


def scan_bytes(data, role, ref):
    for m in PATH_RE.finditer(data):
        s = m.group(0).decode("latin1")
        s = s.lstrip(" \t\x00\r\n")
        # 문자열은 NUL로 끊긴다 — 마지막 NUL 뒤만
        s = s.split("\x00")[-1]
        for part in re.split(r"[,;|]", s):
            if re.search(r"\.\w{2,4}$", part.strip()):
                add(part, role, ref)


ROLE_OF_FILE = {"war3map.w3u": "유닛", "war3map.w3a": "능력", "war3map.w3t": "아이템", "war3map.w3b": "장식물", "war3map.w3d": "파괴물",
                "war3map.w3q": "업그레이드", "war3map.w3h": "버프", "war3map.w3i": "맵정보", "war3map.wts": "문자열", "war3map.doo": "배치",
                "war3mapSkin.txt": "스킨txt", "war3mapMisc.txt": "미스크txt", "war3mapExtra.txt": "엑스트라txt"}
for fn, role in ROLE_OF_FILE.items():
    p = os.path.join(SRC, fn)
    if os.path.exists(p):
        scan_bytes(open(p, "rb").read(), "데이터:" + role, fn)

# 유닛 모델·투사체·아이콘은 w3u 필드로 가른다
UNIT_FIELDS = {"umdl": "유닛모델", "ua1m": "투사체", "ua2m": "투사체", "uico": "유닛아이콘", "ushu": "그림자", "uubs": "건물소품", "ussi": "애니소리", "ubpr": "건설모델"}
for u in w3u.parse(os.path.join(SRC, "war3map.w3u")):
    for k, v in u["mods"].items():
        if isinstance(v, str) and k in UNIT_FIELDS:
            for part in re.split(r"[,;]", v):
                add(part, UNIT_FIELDS[k], f"w3u {u['id']}.{k}")
ART = {"acat": "시전자이펙트", "atat": "대상이펙트", "asat": "특수이펙트", "aeat": "효과이펙트", "amat": "투사체", "alig": "번개", "aart": "능력아이콘",
       "arar": "연구아이콘", "auar": "해제아이콘", "aaea": "광역이펙트", "arhk": "단축키아이콘"}
for a in w3a.parse(os.path.join(SRC, "war3map.w3a")):
    for m in a["mods"]:
        if isinstance(m["value"], str) and m["field"] in ART:
            for part in re.split(r"[,;]", m["value"]):
                add(part, ART[m["field"]], f"w3a {a['id']}.{m['field']}")

# war3map.j 문자열 리터럴
J = open(os.path.join(SRC, "war3map.j"), encoding="utf8", errors="replace").read()
for m in STR_RE.finditer(J):
    s = m.group(1)
    if re.search(r"\.(?:%s)$" % EXT, s, re.I):
        ext = s.rsplit(".", 1)[-1].lower()
        role = {"wav": "소리", "mp3": "음악/소리", "mid": "음악", "flac": "음악", "ogg": "음악"}.get(ext, "트리거참조")
        add(s, role, "war3map.j")
# AddSpecialEffect* 모델은 따로 역할
for m in re.finditer(r'AddSpecialEffect\w*\(([^\n]*?)\)', J):
    for s in STR_RE.findall(m.group(1)):
        if re.search(r"\.(?:mdx|mdl)$", s, re.I):
            add(s, "트리거이펙트", "war3map.j AddSpecialEffect")

# 안 알려진 기본 파일 — 이미 알려진 이름 모음에서 시작(맵 데이터가 가리키는 것 외에도 로딩 화면 등)
for nm in ["war3mapPreview.tga", "war3mapMap.blp", "war3map.w3i", "war3mapMisc.txt", "war3mapSkin.txt", "war3map.wts", "war3map.j", "war3map.w3u",
           "war3map.w3a", "war3map.w3t", "war3map.w3b", "war3map.w3d", "war3map.w3q", "war3map.w3h", "war3map.w3e", "war3map.shd", "war3map.mmp",
           "war3map.doo", "war3mapUnits.doo", "war3map.w3r", "war3map.w3c", "war3map.w3s", "war3map.wpm", "war3map.imp", "war3map.wct", "war3map.wtg",
           "war3mapExtra.txt", "(listfile)", "(attributes)", "war3mapPath.tga", "war3mapMap.tga"]:
    add(nm, "맵기본파일", "knownmap")

# MDX 재귀: 텍스처·PREM 모델·리본/입자 텍스처
FOUND = {}                                                           # key → dict(found_name, size, ext)
queue = list(NAMES.keys())
seen = set()
while queue:
    k = queue.pop()
    if k in seen:
        continue
    seen.add(k)
    e = NAMES[k]
    c, d = read_any(e["name"])
    if d is None:
        continue
    FOUND[k] = dict(file=c, size=len(d))
    if c.lower().endswith((".mdx", ".mdl")) and d[:4] == b"MDLX":
        try:
            mdl = mdx_geo.parse(d)
            for t in mdl["textures"]:
                if t["path"] and t["replaceable"] == 0:
                    n0 = len(NAMES)
                    add(t["path"], "텍스처", "mdx:" + e["name"])
                    if len(NAMES) > n0:
                        queue.append(norm(t["path"]).lower())
        except Exception:                                            # noqa: BLE001
            pass
        for m in re.finditer(rb"[\x20-\x7e]{3,200}?\.(?:mdx|mdl)", d, re.I):                  # PREM 등이 가리키는 모델 경로
            s = m.group(0).decode("latin1")
            n0 = len(NAMES)
            add(s, "하위모델", "mdx:" + e["name"])
            if len(NAMES) > n0:
                queue.append(norm(s).lower())

# MPQ 블록 표 요약
a = ARC.a
blocks = [b for b in a.block_table if (b.flags & 0x80000000) and b.archived_size > 0]
total_exist = len(blocks)
total_size = sum(b.size for b in blocks)
# 찾은 이름이 가리키는 블록
used = set()
for k in FOUND:
    he = a.get_hash_table_entry(FOUND[k]["file"])
    if he is not None:
        used.add(he.block_table_index)
unnamed = [b for i, b in enumerate(a.block_table) if (b.flags & 0x80000000) and b.archived_size > 0 and i not in used]
print("MPQ 존재 파일(블록) 수", total_exist, "압축 해제 총 크기 MB", round(total_size / 1e6, 1))
print("이름을 찾아 읽은 파일", len(used), " 이름을 못 찾은 파일", len(unnamed), "크기 MB", round(sum(b.size for b in unnamed) / 1e6, 1))

rows = []
for k, e in sorted(NAMES.items()):
    f = FOUND.get(k)
    ext = e["name"].rsplit(".", 1)[-1].lower()
    stock = bool(re.match(r"(abilities|units|objects|buildings|doodads|environment|ui|spells|replaceabletextures|textures|sound|war3|scripts|splats|terrainart|cliffs|buildings|plugins|shaders|fonts|fx|model|models|war3mapimported)\\", e["name"].lower())) \
        or e["name"].lower().startswith(("btn", "pasbtn", "disbtn", "atc"))
    rows.append(dict(name=e["name"], ext=ext, roles=sorted(e["roles"]), refs=sorted(e["refs"])[:6], nrefs=len(e["refs"]), inmap=bool(f),
                     size=(f or {}).get("size", 0), file=(f or {}).get("file", ""), stockpath=stock))
json.dump(dict(blocks=total_exist, total_size=total_size, named=len(used), unnamed=len(unnamed), unnamed_size=sum(b.size for b in unnamed), files=rows),
          open(os.path.join(OUT, "inventory.json"), "w"), ensure_ascii=False, indent=1)
c = collections.Counter()
for r in rows:
    c[(r["ext"], "맵안" if r["inmap"] else "맵밖/없음")] += 1
for k, v in sorted(c.items()):
    print(k, v)
