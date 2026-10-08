"""원랜디 창고 15_원랜디스킨 조립 (2026-10-08).

  /usr/bin/python3 Tools/w3x/skin_warehouse_build.py prep   [작업폴더=~/GRD_motion_trial/original_skin]   # 모델 목록·이름 파일 만들기
  /usr/bin/python3 Tools/w3x/skin_warehouse_build.py build  [작업폴더] [창고=~/Desktop/구랜디스킨모음/15_원랜디스킨]  # fbx·검사·미리보기를 창고로

흐름(Tools/w3x/skin_warehouse_run.sh가 순서대로 돌린다): prep → mdx_extract → export_mdx_anim_fbx → verify_mdx_fbx → render_mdx(정적 미리보기) → build.
대상: 원작 w3u 유닛 중 id가 h/H/o/n(조합 캐릭터·라운드 적·보스)이고 모델이 **맵 안 커스텀**인 것(`vfx_warehouse_index.py`의 models.json kind=char).
폴더 하나 = 모델 하나(`<대표 uid>_<원작 이름>`). 같은 모델을 쓰는 유닛이 여럿이면 index.csv에 유닛마다 한 줄, 같은 폴더를 가리킨다.
index.csv: 원작 ID · 원작 이름 · 우리 이름(MASTER_UID_ROSTER_MAP) · 모델 파일 · 애니 클립 목록 · 변환 결과 · 부품 수 검사(objrip 함정: 조각 버림·면 삭제·텍스처 누락) · 메시/뼈/정점/삼각형 · 미리보기
Assets엔 안 넣는다. 🔴 원작(블리자드·중국 모델러) 저작물 — 반입은 사장님 판단 대기.
"""
import csv
import glob
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MODE = sys.argv[1]
W = os.path.expanduser(sys.argv[2] if len(sys.argv) > 2 else "~/GRD_motion_trial/original_skin")
DST = os.path.expanduser(sys.argv[3] if len(sys.argv) > 3 else "~/Desktop/구랜디스킨모음/15_원랜디스킨")
os.makedirs(W, exist_ok=True)

import w3u                                                           # noqa: E402
import wts                                                           # noqa: E402

S = wts.parse(os.path.join(HERE, "원본", "war3map_new.wts"))
strip = lambda v: re.sub(r"\|[cC][0-9a-fA-F]{8}|\|[rR]", "", str(v)).strip()                       # noqa: E731
vfx_models = json.load(open(os.path.expanduser("~/GRD_motion_trial/original_vfx/models.json")))
chars = {m["file"].lower(): m for m in vfx_models if m["kind"] == "char"}
INV = json.load(open(os.path.expanduser("~/GRD_motion_trial/inventory/inventory.json")))
byname = {r["name"].lower(): r for r in INV["files"]}
roster = {}
for r in csv.reader(open(os.path.join(ROOT, "Docs/reference/MASTER_UID_ROSTER_MAP.csv"), encoding="utf-8-sig")):
    if len(r) > 1:
        roster.setdefault(r[0].upper(), []).append(r[1])

# 모델 → 유닛들
units_of = {}
for u in w3u.parse(os.path.join(HERE, "원본", "풀린것", "war3map.w3u")):
    mdl = u["mods"].get("umdl")
    if not mdl or u["id"][0] not in "hHonN":
        continue
    r = byname.get(mdl.replace("\\\\", "\\").lower())
    if r and r["inmap"] and r["file"].lower() in chars:
        units_of.setdefault(r["file"].lower(), []).append((u["id"], strip(wts.resolve(str(u["mods"].get("unam", "")), S))))


def safe(s):
    return re.sub(r"[^\w가-힣.\-]+", "_", s).strip("_")[:44]


def folder_name(k):
    uid, nm = sorted(units_of[k])[0]
    return f"{uid}_{safe(nm)}"


def tag_of(f):
    return os.path.splitext(f)[0].replace("\\", "__").replace("/", "__")


if MODE == "prep":
    names = [chars[k]["file"] for k in sorted(units_of)]
    open(os.path.join(W, "char_names.txt"), "w").write("\n".join(names) + "\n")
    print("char models", len(names), "units", sum(len(v) for v in units_of.values()))
    sys.exit(0)

verify = {r["tag"]: r for r in json.load(open(os.path.join(W, "verify.json")))}
os.makedirs(DST, exist_ok=True)
rows = []
for k in sorted(units_of):
    m = chars[k]
    tag = tag_of(m["file"])
    src = os.path.join(W, "fbx", tag)
    folder = os.path.join(DST, folder_name(k))
    status, side = "실패", {}
    if os.path.exists(os.path.join(src, tag + ".fbx")):
        if os.path.exists(folder):
            shutil.rmtree(folder)
        shutil.copytree(src, folder)
        side = json.load(open(os.path.join(folder, tag + ".json")))
        status = "변환됨"
        pv = glob.glob(os.path.join(W, "preview", os.path.splitext(m["file"])[0] + "_앞비스듬히.png")) or glob.glob(os.path.join(W, "preview", tag + "*.png"))
        if pv:
            shutil.copy(sorted(pv)[0], os.path.join(folder, "preview.png"))
    v = verify.get(tag, {})
    for uid, nm in sorted(units_of[k]):
        rows.append([uid, nm, " / ".join(roster.get(uid.upper(), [])), m["file"], "|".join(s["name"] for s in side.get("sequences", [])), status,
                     "통과" if v.get("ok") else ("; ".join(v.get("problems", [])) or "미검사"), v.get("degenerate_tris", ""), len(side.get("meshes", [])),
                     len(side.get("nodes", [])), m["geosets"], m["verts"], m["tris"], len(side.get("pre2", [])), side.get("ribbons", ""),
                     os.path.relpath(folder, DST), "preview.png" if os.path.exists(os.path.join(folder, "preview.png")) else ""])
HEAD = ["원작 ID", "원작 이름", "우리 이름(MASTER_UID_ROSTER_MAP)", "모델 파일", "애니 클립(시퀀스)", "변환", "부품 수 검사", "퇴화 삼각형(원본)", "메시 수", "뼈 수", "원본 지오셋", "원본 정점",
        "원본 삼각형", "파티클 줄기(JSON만)", "리본(미변환)", "폴더", "미리보기"]
with open(os.path.join(DST, "index.csv"), "w", newline="", encoding="utf-8-sig") as f:
    wr = csv.writer(f)
    wr.writerow(HEAD)
    wr.writerows(sorted(rows))
print("units", len(rows), "models", len(units_of), "converted", sum(1 for r in rows if r[5] == "변환됨"), "verify ok", sum(1 for r in rows if r[6] == "통과"))

# 모아보기 시트: 앞비스듬히 미리보기 격자(4열 × 5행)
from PIL import Image, ImageDraw, ImageFont                          # noqa: E402
try:
    fnt = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 13)
except Exception:                                                    # noqa: BLE001
    fnt = None
items = []
for k in sorted(units_of):
    pvp = os.path.join(DST, folder_name(k), "preview.png")
    if os.path.exists(pvp):
        items.append((folder_name(k), Image.open(pvp).convert("RGB")))
for old in glob.glob(os.path.join(DST, "모아보기_*.png")):
    os.remove(old)
cols, per = 4, 20
for si in range(0, len(items), per):
    chunk = items[si:si + per]
    tw = 300
    th = int(tw * chunk[0][1].height / chunk[0][1].width)
    rws = (len(chunk) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * tw, rws * (th + 18)), (14, 14, 14))
    dr = ImageDraw.Draw(sheet)
    for i, (nm, im) in enumerate(chunk):
        x, y = (i % cols) * tw, (i // cols) * (th + 18)
        dr.text((x + 4, y + 1), nm, fill=(255, 255, 255), font=fnt)
        sheet.paste(im.resize((tw, th)), (x, y + 18))
    sheet.save(os.path.join(DST, f"모아보기_{si // per + 1:02d}.png"))
open(os.path.join(DST, "README.md"), "w", encoding="utf8").write(f"""# 15_원랜디스킨 — 원작(ORD11.089) 맵 안 커스텀 유닛 모델 {len(units_of)}개(유닛 {len(rows)}명)

**게임에 안 넣었다.** 나중에 가져다 쓸 창고. 원작(블리자드·중국 모델러) 저작물이라 반입은 사장님 판단 대기.
생성: `Tools/w3x/skin_warehouse_run.sh` → `skin_warehouse_build.py` · `Tools/blender/export_mdx_anim_fbx.py` · `verify_mdx_fbx.py` · `render_mdx.py`.
- 폴더 = 모델 하나(`<대표 uid>_<원작 이름>`): `<모델>.fbx`(뼈+스킨 가중치 균등, 시퀀스마다 액션 30fps) · `Textures/*.png` · `<모델>.json`(층 알파·UV 이동·파티클·리본) · `preview.png`(정적 앞비스듬히).
- `index.csv`: 유닛마다 한 줄(같은 모델을 쓰는 유닛은 같은 폴더) — 원작 ID·이름·우리 이름(MASTER_UID_ROSTER_MAP)·애니 클립·부품 수 검사.
- 부품 수 검사(objrip 함정 대조): FBX를 다시 읽어 메시 수·삼각형 수·정점 수를 원본과 맞췄다. 팀색(TeamColor/TeamGlow) 층만 있는 지오셋은 일부러 뺐다(바닥 광채 판 등). 넓이 0·겹친 면은 Blender가 합친다(퇴화 삼각형 칸).
- 한계: 헤르미트/베지어 보간은 선형 · 뼈 가중치 균등(원작 행렬 그룹) · 팀색 텍스처(ReplaceableTextures\\TeamColor)는 변환 안 함 · 파티클/리본은 JSON만.
""")
print("sheets", (len(items) + per - 1) // per)
