"""원랜디 창고 16_원랜디스킬 조립 (2026-10-08).

  /usr/bin/python3 Tools/w3x/vfx_warehouse_build.py [작업폴더=~/GRD_motion_trial/original_vfx] [창고폴더=~/Desktop/구랜디스킨모음/16_원랜디스킬]

입력: <작업폴더>/models.json(vfx_warehouse_index.py) · fbx/<모델>/(export_mdx_anim_fbx.py) · verify.json(verify_mdx_fbx.py) · preview/(render_cast_vfx.py orig)
산출(창고):
  <모양 묶음>/<모델이름>/  FBX · Textures/ · <모델>.json(파티클·알파곡선) · preview.png(4컷 한 장)
  index.csv  모델 경로·원작 파일·모양 묶음·쓰는 원작 능력/트리거(used_by)·주인 유닛·변환 결과·검사 결과·메시/뼈/시퀀스/파티클 수
  모아보기_N.png  모양 묶음별 미리보기 시트
  README.md   구조·한계 설명
Assets엔 넣지 않는다. 🔴 원작(블리자드·중국 모델러) 저작물 — 반입은 사장님 판단 대기.
"""
import csv
import glob
import json
import os
import re
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

W = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_motion_trial/original_vfx")
DST = os.path.expanduser(sys.argv[2] if len(sys.argv) > 2 else "~/Desktop/구랜디스킨모음/16_원랜디스킬")
models = json.load(open(os.path.join(W, "models.json")))
verify = {r["tag"]: r for r in json.load(open(os.path.join(W, "verify.json")))}
os.makedirs(DST, exist_ok=True)


def tag_of(f):
    return os.path.splitext(f)[0].replace("\\", "__").replace("/", "__")


def safe(s):
    return re.sub(r'[<>:"|?*]', "_", s)


def preview_sheet(tag, orig_name):
    stem = os.path.splitext(orig_name)[0]
    ims = []
    for i in range(4):
        for cand in (f"{stem}_orig_s{i}.png", f"{tag}_orig_s{i}.png"):
            p = os.path.join(W, "preview", cand)
            if os.path.exists(p):
                ims.append(Image.open(p).convert("RGB"))
                break
    if not ims:
        return None
    w, h = ims[0].size
    s = min(1.0, 300 / w)
    tw, th = int(w * s), int(h * s)
    sheet = Image.new("RGB", (tw * len(ims), th), (24, 24, 24))
    for i, im in enumerate(ims):
        sheet.paste(im.resize((tw, th)), (i * tw, 0))
    return sheet


rows, by_shape, sheets = [], {}, {}
for m in models:
    if m["kind"] != "fx":
        continue
    tag = tag_of(m["file"])
    src = os.path.join(W, "fbx", tag)
    shape = m["shapes"][0] if m["shapes"] else "미분류"
    folder = os.path.join(DST, safe(shape), safe(tag))
    status, fbx_rel = "실패", ""
    side = {}
    if os.path.exists(os.path.join(src, tag + ".fbx")):
        if os.path.exists(folder):
            shutil.rmtree(folder)
        shutil.copytree(src, folder)
        side = json.load(open(os.path.join(folder, tag + ".json")))
        fbx_rel = os.path.relpath(os.path.join(folder, tag + ".fbx"), DST)
        status = "변환됨" if side["meshes"] else "입자 전용(메시 없음)"
        sh = preview_sheet(tag, m["file"])
        if sh is not None:
            sh.save(os.path.join(folder, "preview.png"))
            sheets.setdefault(shape, []).append((tag, sh))
    v = verify.get(tag, {})
    rows.append([m["file"], tag, shape, "·".join(m["shapes"]), "; ".join(m["used_by"]), m["n_used_by"], "; ".join(m["owners"]), m["n_owners"], status,
                 "통과" if v.get("ok") else ("; ".join(v.get("problems", [])) or "미검사"), v.get("degenerate_tris", ""),
                 len(side.get("meshes", [])), len(side.get("nodes", [])), "|".join(s["name"] for s in side.get("sequences", [])), len(side.get("pre2", [])),
                 side.get("ribbons", ""), m["geosets"], m["verts"], m["tris"], "|".join(m["roles"]), fbx_rel,
                 os.path.relpath(os.path.join(folder, "preview.png"), DST) if os.path.exists(os.path.join(folder, "preview.png")) else ""])
HEAD = ["원작 모델 파일", "폴더 이름", "모양 묶음", "모양(후보)", "쓰는 원작 능력/더미/트리거", "참조 수", "주인 유닛(H코드 이름)", "주인 수", "변환", "부품 수 검사", "퇴화 삼각형(원본)",
        "메시 수", "뼈 수", "애니 클립(시퀀스)", "파티클 줄기(PRE2, JSON만)", "리본(미변환)", "원본 지오셋", "원본 정점", "원본 삼각형", "역할", "FBX", "미리보기"]
with open(os.path.join(DST, "index.csv"), "w", newline="", encoding="utf-8-sig") as f:
    wr = csv.writer(f)
    wr.writerow(HEAD)
    wr.writerows(sorted(rows, key=lambda r: (r[2], r[0].lower())))

# 모아보기 시트: 모양 묶음별로 16개씩(2열 8행 → 읽기 좋게 1열 8행 × 2)
n = 0
for shape, items in sorted(sheets.items()):
    items.sort()
    for k in range(0, len(items), 10):
        chunk = items[k:k + 10]
        w = max(i[1].width for i in chunk)
        sheet = Image.new("RGB", (w, sum(i[1].height + 16 for i in chunk)), (10, 10, 10))
        d = ImageDraw.Draw(sheet)
        try:
            fnt = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 13)   # 한글 라벨
        except Exception:                                            # noqa: BLE001
            fnt = None
        y = 0
        for tag, im in chunk:
            d.text((4, y + 1), f"{shape} · {tag}", fill=(255, 255, 255), font=fnt)
            sheet.paste(im, (0, y + 16))
            y += im.height + 16
        n += 1
        sheet.save(os.path.join(DST, f"모아보기_{n:02d}_{safe(shape)}.png"))
open(os.path.join(DST, "README.md"), "w", encoding="utf8").write(f"""# 16_원랜디스킬 — 원작(ORD11.089) 맵 안 이펙트·투사체·장식 모델 창고

**게임에 안 들어갔다.** 나중에 가져다 쓸 창고다. 원작(블리자드·중국 모델러) 저작물이라 반입은 사장님 판단 대기.
생성: `Tools/w3x/original_asset_inventory.py` → `vfx_warehouse_index.py` → `mdx_extract.py` → `Tools/blender/export_mdx_anim_fbx.py` → `verify_mdx_fbx.py` → `render_cast_vfx.py` → `vfx_warehouse_build.py`.

- 폴더 = `<모양 묶음>/<모델 이름>/` : `<모델>.fbx`(뼈 애니 30fps 구움, 시퀀스마다 액션) · `Textures/*.png` · `<모델>.json`(FBX에 못 실린 것: 시퀀스·층 알파 곡선·UV 이동·PRE2 파티클·리본 수) · `preview.png`(원작 추정 렌더 4컷 — 파티클은 값 추정).
- `index.csv`: 모델별로 원작 파일·쓰는 능력/더미 유닛/트리거·주인 유닛·변환 결과·부품 수 검사.
- 모양 묶음은 **이름·텍스처 이름 키워드 투표**라 틀릴 수 있다. 「주인 유닛」은 w3u 더미 유닛 → j 함수 → 공격 트리거 사슬로 모은 후보라 넓게 잡힌다(참조 수 참고).
- 한계: 헤르미트/베지어 보간은 선형, 뼈 가중치는 행렬 그룹 균등, 지오셋·층 알파/UV 이동/파티클/리본은 JSON으로만(FBX엔 없음). 맵 밖 워크3 기본 텍스처는 `mdx_extract`가 비슷한 그림으로 대신 깔았다(원작과 다르다).
- 부품 수 검사: FBX를 다시 읽어 (지오셋×층) 메시 수·삼각형 수·정점 수·애니 유무를 원본과 맞췄다. 넓이 0 삼각형과 정점 집합이 같은 겹친 면(양면 복사)은 Blender가 합치므로 기대값에서 뺐다(「퇴화 삼각형」 칸에 원본 개수).
""")
print("rows", len(rows), "sheets", n, "converted", sum(1 for r in rows if r[8] != "실패"), "verify ok", sum(1 for r in rows if r[9] == "통과"))
