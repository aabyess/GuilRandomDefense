"""원랜디 창고 17_원랜디아이콘 조립 (2026-10-08).

  /usr/bin/python3 Tools/w3x/icon_warehouse_build.py [창고폴더=~/Desktop/구랜디스킨모음/17_원랜디아이콘]

원작 맵 안에 들어 있는 아이콘 BLP(능력·유닛·아이템·업그레이드·버프의 아이콘 필드가 가리키는 것 중 맵 안 커스텀)를 PNG로 꺼낸다.
  <창고>/<종류>/<파일이름>.png (종류 = 명령(BTN)·패시브(PASBTN)·비활성(DISBTN)·유닛·기타)
  index.csv: 아이콘 경로 · 종류 · 이 아이콘을 쓰는 원작 능력(코드·이름·필드)·유닛(uid·이름·우리 로스터)·아이템·업그레이드·버프 · 우리 스킬 대응(Tools/skill_icons/skill_icon_map.csv가 이 아이콘을 쓰는 우리 SkillData 에셋)
  모아보기_N.png: 격자 시트(64px)
Assets엔 안 넣는다. 🔴 원작 저작물 — 반입은 사장님 판단 대기.
"""
import collections
import csv
import io
import os
import re
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import w3a                                                           # noqa: E402
import w3q                                                           # noqa: E402
import w3u                                                           # noqa: E402
import wts                                                           # noqa: E402
from mpqread import Archive                                          # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DST = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/Desktop/구랜디스킨모음/17_원랜디아이콘")
SRC = os.path.join(HERE, "원본", "풀린것")
S = wts.parse(os.path.join(HERE, "원본", "war3map_new.wts"))
ARC = Archive(os.path.expanduser("~/GRD_motion_trial/_work/ord.mpq"))
strip = lambda v: re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", "", str(v)).strip()                       # noqa: E731
res = lambda v: strip(wts.resolve(str(v), S))                                                       # noqa: E731

uses = collections.defaultdict(lambda: collections.defaultdict(list))                              # 경로(소문자) → 종류 → [설명]
names = {}


def note(path, kind, desc):
    if not isinstance(path, str):
        return
    for p in re.split(r"[,;]", path):
        p = p.replace("\\\\", "\\").strip()
        if not p.lower().endswith((".blp", ".tga")) and not re.search(r"\\btn|\\pasbtn|\\disbtn", p.lower()):
            continue
        if not p.lower().endswith((".blp", ".tga")):
            p += ".blp"
        names.setdefault(p.lower(), p)
        uses[p.lower()][kind].append(desc)


# 능력(아이콘·연구·해제)
abil_name = {}
for a in w3a.parse(os.path.join(SRC, "war3map.w3a")):
    f = {}
    for m in a["mods"]:
        f.setdefault(m["field"], m["value"])
    abil_name[a["id"]] = res(f.get("anam", ""))
    for fld in ("aart", "arar", "auar"):
        if f.get(fld):
            note(f[fld], "능력", f"{a['id']} {abil_name[a['id']][:30]} ({fld})")
# 유닛
UNITS = {}
for u in w3u.parse(os.path.join(SRC, "war3map.w3u")):
    UNITS[u["id"]] = res(u["mods"].get("unam", ""))
    if u["mods"].get("uico"):
        note(u["mods"]["uico"], "유닛", f"{u['id']} {UNITS[u['id']][:30]}")
# 아이템
for t in w3u.parse(os.path.join(SRC, "war3map.w3t")):
    if t["mods"].get("iico"):
        note(t["mods"]["iico"], "아이템", f"{t['id']} {res(t['mods'].get('unam', ''))[:30]}")
# 업그레이드(w3q)
for q in w3q.parse(os.path.join(SRC, "war3map.w3q")):
    f = {}
    for m in q["mods"]:
        f.setdefault(m[0], m[2])
    if f.get("gar1"):
        note(f["gar1"], "업그레이드", f"{q['id'] if q['id'].isalnum() else q['base']} {res(f.get('gnam', ''))[:30]}")
# 버프(w3h) — fart 필드
try:
    for b in w3u.parse(os.path.join(SRC, "war3map.w3h")):
        if b["mods"].get("fart"):
            note(b["mods"]["fart"], "버프", f"{b['id']} {res(b['mods'].get('fnam', ''))[:30]}")
except Exception:                                                    # noqa: BLE001
    pass

# 우리 로스터 대응(유닛 uid → 로스터)
roster = {}
for r in csv.reader(open(os.path.join(ROOT, "Docs/reference/MASTER_UID_ROSTER_MAP.csv"), encoding="utf-8-sig")):
    if len(r) > 1:
        roster.setdefault(r[0].upper(), []).append(r[1])
# 우리 스킬 대응(skill_icon_map.csv)
ours = collections.defaultdict(set)
for r in list(csv.reader(open(os.path.join(ROOT, "Tools/skill_icons/skill_icon_map.csv"), encoding="utf-8-sig")))[1:]:
    if len(r) > 5 and r[5]:
        ours[r[5].replace("\\\\", "\\").lower()].add(os.path.basename(r[0]).replace("SkillData_", "")[:-6])

os.makedirs(DST, exist_ok=True)


def kind_of(p):
    b = os.path.basename(p.replace("\\", "/")).lower()
    if b.startswith("pasbtn"):
        return "패시브"
    if b.startswith("disbtn") or b.startswith("dis"):
        return "비활성"
    if b.startswith("btn") or "commandbuttons" in p.lower():
        return "명령"
    return "기타"


rows, thumbs, used = [], collections.defaultdict(list), set()
for k, p in sorted(names.items()):
    data = None
    for c in (p, os.path.basename(p.replace("\\", "/"))):
        try:
            data = ARC.read(c)
        except Exception:                                            # noqa: BLE001
            data = None
        if data:
            break
    if not data:
        continue                                                     # 맵 밖(워크3 기본)
    try:
        im = Image.open(io.BytesIO(data)).convert("RGBA")
    except Exception as e:                                           # noqa: BLE001
        rows.append([p, kind_of(p), "", "", "", "", "", "", "", "", f"BLP 읽기 실패 {e}"])
        continue
    kind = kind_of(p)
    d = os.path.join(DST, kind)
    os.makedirs(d, exist_ok=True)
    stem = re.sub(r"[^\w.\-가-힣]+", "_", os.path.basename(p.replace("\\", "/"))[:-4])
    out = os.path.join(d, stem + ".png")
    n = 2
    while out in used:                                               # 같은 이름이 다른 경로에서 이미 나왔으면 번호
        out = os.path.join(d, f"{stem}_{n}.png")
        n += 1
    used.add(out)
    im.save(out)
    u = uses[k]
    unit_ids = [x.split()[0] for x in u.get("유닛", [])]
    rows.append([p, kind, im.size[0], "; ".join(u.get("능력", [])[:8]), "; ".join(u.get("유닛", [])[:6]), "; ".join(f"{i}→{'/'.join(roster[i.upper()])}" for i in unit_ids if i.upper() in roster),
                 "; ".join(u.get("아이템", [])[:4]), "; ".join(u.get("업그레이드", [])[:4]), "; ".join(u.get("버프", [])[:4]),
                 "; ".join(sorted(ours.get(k, []))[:6]), os.path.relpath(out, DST)])
    thumbs[kind].append((os.path.basename(out)[:-4], im.resize((64, 64))))
HEAD = ["아이콘 경로(원작)", "종류", "가로(px)", "쓰는 원작 능력", "쓰는 원작 유닛", "그 유닛의 우리 로스터", "쓰는 아이템", "쓰는 업그레이드", "쓰는 버프", "우리 스킬 대응(skill_icon_map.csv)", "PNG"]
with open(os.path.join(DST, "index.csv"), "w", newline="", encoding="utf-8-sig") as f:
    wr = csv.writer(f)
    wr.writerow(HEAD)
    wr.writerows([r[:len(HEAD)] + [""] * (len(HEAD) - len(r)) for r in rows])
n = 0
for kind, items in sorted(thumbs.items()):
    for s in range(0, len(items), 120):
        chunk = items[s:s + 120]
        cols = 12
        rws = (len(chunk) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * 76, rws * 88 + 4), (20, 20, 20))
        dr = ImageDraw.Draw(sheet)
        for i, (nm, im) in enumerate(chunk):
            x, y = (i % cols) * 76 + 6, (i // cols) * 88 + 4
            bg = Image.new("RGB", (64, 64), (60, 60, 60))
            bg.paste(im, (0, 0), im)
            sheet.paste(bg, (x, y))
            dr.text((x - 4, y + 66), nm[:12], fill=(200, 200, 200))
        n += 1
        sheet.save(os.path.join(DST, f"모아보기_{n:02d}_{kind}.png"))
open(os.path.join(DST, "README.md"), "w", encoding="utf8").write(f"""# 17_원랜디아이콘 — 원작(ORD11.089) 맵 안 커스텀 아이콘 {len(rows)}개

게임에 안 넣었다(창고). 원작 저작물이라 반입은 사장님 판단 대기. 생성: `Tools/w3x/icon_warehouse_build.py`.
- 폴더 = 종류(명령 BTN·패시브 PASBTN·비활성 DISBTN·기타). PNG는 원작 BLP를 그대로 변환(크기 보존).
- `index.csv`: 이 아이콘을 쓰는 원작 능력(w3a aart/arar/auar)·유닛(w3u uico, 우리 로스터 대응은 Docs/reference/MASTER_UID_ROSTER_MAP.csv)·아이템(w3t iico)·업그레이드(w3q gar1)·버프(w3h fart)와,
  `Tools/skill_icons/skill_icon_map.csv`에서 우리 SkillData가 이 아이콘 경로를 쓰는 곳.
- 이름은 맵 데이터가 가리키는 것만 센다 — 가리키는 곳이 없는 아이콘은 못 찾는다(원작 맵 파일 중 이름을 못 찾은 455개 안에 있을 수 있다).
""")
print("icons", len(rows), "sheets", n, "kinds", {k: len(v) for k, v in thumbs.items()})
