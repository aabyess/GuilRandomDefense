"""컷인 C안 — 유닛마다 포즈 후보 중 하나를 고르고 41기 자동 검사를 한다(2026-10-08, PM 기준). /usr/bin/python3 Tools/blender/cutin_pick.py [유닛 …]

검사 두 줄(합성 C안의 실제 배치로 계산 — gen_cutin_comp.layers_C/frame_C와 같은 숫자):
  ① 눈이 보인다: 눈 위치(gen_cutin_char가 후보마다 적은 eye 좌표)가 화면 안이고, 흰 띠·아래 줄무늬에 안 가린다(띠 레이어 알파로 ±28px 검사)
  ② 캐릭터가 틀의 50% 이상: 캐릭터 실루엣의 화면 안 높이 ≥ 화면 높이 50%
고르는 순서: Tools/blender/cutin_picks.json(사람이 고른 것) → 공용 클립 유닛 3기 중 1기는 프리셋(가리키기·팔짱·주먹·허리 돌려 가며, 비슷해 보이는 것 방지)
  → 공격 프레임 c1·c2·c3 중 둘 다 통과한 첫 것 → 프리셋 중 통과한 첫 것 → 그래도 없으면 c1(실패로 적는다).
산출: render/C_<유닛>_thigh.png(고른 것 복사) · C_<유닛>_cands.json의 chosen/checks · ~/GRD_cutin/C_all/_check.txt(통과·실패 목록).
"""
import glob
import json
import os
import shutil
import sys

from PIL import Image

HOME = os.path.expanduser(os.environ.get("CUTIN_HOME", "~/GRD_cutin"))
R = os.path.join(HOME, "render")
W, H = 1920, 1080
HH = 1320                                  # layers_C의 캐릭터 높이
X0, Y0 = W // 2 - HH // 2 - 560, H - HH + 150   # 미끄러진 뒤(정점) 캐릭터 그림 왼쪽 위
PICKS = os.environ.get("CUTIN_PICKS") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "cutin_picks.json")
BAND = None
TOP_MARGIN = 18                            # 머리 꼭대기 ~ 화면 위 끝 최소 여백(px)


def band_mask():
    """흰 띠 + 아래 줄무늬 가림 마스크(아무 유닛의 C 층에서 — 모양은 유닛마다 같다)."""
    global BAND
    if BAND is None:
        b = sorted(glob.glob(os.path.join(HOME, "layers", "C_*_band.png")))
        st = sorted(glob.glob(os.path.join(HOME, "layers", "C_*_stripes.png")))
        m = Image.open(b[0]).split()[3]
        if st:
            from PIL import ImageChops
            m = ImageChops.lighter(m, Image.open(st[0]).split()[3])
        BAND = m
    return BAND


def check(cand, zoom=1.0):
    eu, ev = cand.get("eye", [0.5, 0.9])
    if zoom > 1.01:                                       # gen_cutin_comp.layers_C의 확대 자르기와 같은 식(위 끝 = 머리 꼭대기 바로 위)
        a0 = Image.open(os.path.join(R, cand["file"])).split()[3]
        atop = (a0.point(lambda v: 255 if v > 40 else 0).getbbox() or (0, 0, 1, 1))[1] / a0.height
        side = 1 / zoom
        x0 = min(max(eu - side / 2, 0), 1 - side)
        y0 = min(max(atop - side * 0.04, 0), 1 - side)
        eu, ev = (eu - x0) / side, 1 - ((1 - ev) - y0) / side
    # 머리 꼭대기: 캐릭터 그림(확대 자르기 뒤)의 맨 위 불투명 줄이 화면 위 끝 아래 TOP_MARGIN 이상이어야 한다.
    # 모자라면 그만큼 캐릭터를 내린다(dy, 최대 220px) — 박민수·노태현·최상호(바지사장)·김용태가 머리가 잘렸다(10-08 PM).
    im = Image.open(os.path.join(R, cand["file"])).split()[3]
    if zoom > 1.01:
        S0 = im.width
        side = S0 / zoom
        cx0 = min(max(cand.get("eye", [0.5, 0.9])[0] * S0 - side / 2, 0), S0 - side)
        atop = (im.point(lambda v: 255 if v > 40 else 0).getbbox() or (0, 0, 1, 1))[1]
        cy0 = min(max(atop - side * 0.04, 0), S0 - side)          # 확대 자르기 위 끝 = 머리 꼭대기 바로 위(gen_cutin_comp와 같은 식)
        im = im.crop((int(cx0), int(cy0), int(cx0 + side), int(cy0 + side))).resize((S0, S0))
    bb = im.point(lambda v: 255 if v > 40 else 0).getbbox() or (0, 0, 1, 1)
    top_scr = Y0 + bb[1] * HH / im.height
    dy = int(min(max(TOP_MARGIN - top_scr, 0), 220))
    head_clipped_in_render = bb[1] <= 1                        # 그림 맨 윗줄까지 몸이 찼다 = 렌더 틀 자체에서 잘림(내려도 안 보인다)
    ex, ey = X0 + eu * HH, Y0 + (1 - ev) * HH + dy
    m = band_mask()
    eye_in = 20 <= ex < W - 20 and 60 <= ey < H - 20
    covered = False
    if eye_in:
        # 눈은 흰 띠 「위쪽」이어야 한다(원본처럼 얼굴이 띠 위로). 띠 아래로 내려간 눈(윤현모 913px 같은 것)은 실패 — 10-08 1차 검사가 놓쳤다
        col_top = next((y for y in range(H) if m.getpixel((int(min(max(ex, 0), W - 1)), y)) > 60), H)
        if ey > col_top - 30:
            covered = True
        for ox in (-28, 0, 28):                       # (ox·oy — dy와 이름이 겹치면 내림 값이 덮인다)
            for oy in (-28, 0, 28):
                x, y = int(ex + ox), int(ey + oy)
                if 0 <= x < W and 0 <= y < H and m.getpixel((x, y)) > 60:
                    covered = True
    top = max(0, Y0 + dy + bb[1] * HH / im.height)
    bot = min(H, Y0 + dy + bb[3] * HH / im.height)
    cover = (bot - top) / H
    head_ok = (top_scr + dy) >= TOP_MARGIN - 1 and not head_clipped_in_render
    return dict(eye_ok=bool(eye_in and not covered), eye=[round(ex), round(ey)], height=round(cover, 2), height_ok=cover >= 0.5,
                head_ok=bool(head_ok), dy=dy)


def main(only):
    manual = json.load(open(PICKS)) if os.path.exists(PICKS) else {}
    infos = sorted(glob.glob(os.path.join(R, "C_*_cands.json")))
    shared = [f for f in infos if json.load(open(f)).get("source", "").startswith("공용")]
    presets_cycle = ["p_point", "p_crossed", "p_fist", "p_hips"]
    lines, n_ok = [], 0
    for f in infos:
        inf = json.load(open(f))
        u = inf["unit"]
        if only and u not in only:
            continue
        mp0 = manual.get(u)
        zoom0 = mp0.get("zoom", 1.0) if isinstance(mp0, dict) else 1.0
        for c in inf["candidates"]:
            c["checks"] = check(c, zoom0)
        ok = lambda c: c["checks"]["eye_ok"] and c["checks"]["height_ok"] and c["checks"]["head_ok"]
        byid = {c["id"]: c for c in inf["candidates"]}
        why = ""
        chosen = None
        mp = manual.get(u)
        mid = mp.get("pick") if isinstance(mp, dict) else mp
        if mid in byid:
            chosen, why = byid[mid], "사람이 고름(cutin_picks.json)"
        inf["zoom"] = mp.get("zoom", 1.0) if isinstance(mp, dict) else 1.0
        if chosen is None and f in shared and shared.index(f) % 3 == 1:
            k = (shared.index(f) // 3) % len(presets_cycle)
            for pid in presets_cycle[k:] + presets_cycle[:k]:
                if pid in byid and ok(byid[pid]):
                    chosen, why = byid[pid], "공용 클립 섞기(프리셋)"
                    break
        if chosen is None:
            chosen = next((c for c in inf["candidates"] if c["id"].startswith("c") and ok(c)), None)
            why = "공격 프레임 통과"
        if chosen is None:
            chosen = next((c for c in inf["candidates"] if ok(c)), None)
            why = "프리셋 통과"
        if chosen is None:
            chosen, why = inf["candidates"][0], "통과 후보 없음 — c1 그대로"
        inf["chosen"], inf["chosen_why"] = chosen["id"], why
        inf["dy"] = chosen["checks"]["dy"]
        shutil.copyfile(os.path.join(R, chosen["file"]), os.path.join(R, f"C_{u}_thigh.png"))
        json.dump(inf, open(f, "w"), ensure_ascii=False, indent=1)
        ck = chosen["checks"]
        passed = ck["eye_ok"] and ck["height_ok"] and ck["head_ok"]
        n_ok += passed
        lines.append(f"{'통과' if passed else '실패'}\t{u}\t{chosen['id']}\t눈 {'보임' if ck['eye_ok'] else '안 보임'} {ck['eye']}\t높이 {int(ck['height'] * 100)}%\t머리 {'안 잘림' if ck['head_ok'] else '잘림'}{f'(내림 {ck[chr(100)+chr(121)]}px)' if ck['dy'] else ''}\t{why}")
    os.makedirs(os.path.join(HOME, "C_all"), exist_ok=True)
    rep = os.path.join(HOME, "C_all", "_check.txt")
    with open(rep, "w") as fp:
        fp.write(f"컷인 C안 자동 검사 — 통과 {n_ok}/{len(lines)} (① 눈이 화면 안 + 흰 띠 위쪽 + 띠·줄무늬에 안 가림 ② 캐릭터 높이 ≥ 화면 50% ③ 머리 꼭대기가 화면 위 끝에서 18px 이상 아래 — 모자라면 캐릭터를 내림)\n" + "\n".join(lines) + "\n")
    print(open(rep).read())


if __name__ == "__main__":
    main(set(sys.argv[1:]))
