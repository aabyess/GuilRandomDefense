"""상위 등급 뽑기 컷인 합성(2026-10-08, 사장님 시안). PIL+numpy — 시스템 파이썬으로: /usr/bin/python3 Tools/blender/gen_cutin_comp.py

  입력: ~/GRD_cutin/render/{A_<유닛>_face.png, B_<유닛>_bust.png}  (gen_cutin_char.py가 만든 투명 PNG)
  산출: ~/GRD_cutin/{A,B}_<유닛>_still.png(정점 프레임) · {A,B}_<유닛>.mp4(1920×1080 30fps 1.5초, 게임 화면 위) · layers/(층별 PNG — 등급색만 바꿔 다시 뽑는다)
  A안 = 단간론파 「논파」(집중선·먹물·붓글씨 별명·칼자국·기울인 얼굴) · B안 = 캐릭터 소개 카드(동심원 대리석·망점·먹 띠에 이름·흰 줄에 칭호·평행선·상반신)
  폰트는 전부 OFL: East Sea Dokdo · Nanum Brush Script · Nanum Gothic ExtraBold · Black Han Sans (~/GRD_cutin/fonts, 구글 폰트 raw).
  등급색: Assets UnitData.GradeColor — 초월 청록(0.20,0.80,0.76) · 불멸 상아(0.95,0.93,0.80) · 영원 남색(0.22,0.28,0.72).
"""
import math
import os
import random
import subprocess
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HOME = os.path.expanduser("~/GRD_cutin")
FONTS = os.path.join(HOME, "fonts")
W, H = 1920, 1080
GAME = os.path.expanduser("~/GitHub/GuilRandomDefense/Docs/ui_mockups/compare/31_fubao_focus_on.png")
GRADE = {  # main = 바탕색 · dark = 문양/그림자 · accent = 강조 글자 · name = 등급 글자
    "초월": dict(main=(51, 204, 194), dark=(18, 120, 120), accent=(255, 64, 150), label="초월함"),
    "불멸": dict(main=(242, 237, 204), dark=(180, 150, 80), accent=(230, 80, 40), label="불멸의"),
    "영원": dict(main=(56, 71, 184), dark=(24, 30, 100), accent=(255, 214, 40), label="영원함"),
}
rng = random.Random(11)
nrng = np.random.RandomState(11)


def font(name, size, index=0):
    return ImageFont.truetype(os.path.join(FONTS, name), size, index=index)


def noise(w, h, scale, octaves=4, seed=0):
    r = np.random.RandomState(seed)
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        gw, gh = max(2, int(w / scale * (2 ** o))), max(2, int(h / scale * (2 ** o)))
        g = r.rand(gh, gw).astype(np.float32)
        out += np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32) / 255 * amp
        tot += amp
        amp *= 0.5
    return out / tot


def ragged(mask, amount=6, seed=0, blur=4.0):
    """L 마스크 가장자리를 먹물처럼 너덜하게."""
    m = np.asarray(mask.filter(ImageFilter.GaussianBlur(blur)), np.float32) / 255
    n = noise(mask.width, mask.height, 14, 3, seed)
    out = ((m + (n - 0.5) * min(amount * 0.07, 0.9)) > 0.5).astype(np.uint8) * 255
    return Image.fromarray(out)


def tint(color, k):
    return tuple(max(0, min(255, int(c * k))) for c in color)


# ═════════ A안: 논파 컷인 층 ═════════
def layers_A(face_png, nick, name, grade):
    g = GRADE[grade]
    L = {}
    # 1) 바탕 + 집중선(초점 = 얼굴 쪽)
    fx, fy = 1180, 470
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt((xx - fx) ** 2 + (yy - fy) ** 2) / 1300
    base = np.zeros((H, W, 3), np.float32)
    for i in range(3):
        base[..., i] = g["main"][i] * (1.15 - 0.55 * np.clip(d, 0, 1))
    bg = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert("RGBA")
    ln = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ln)
    for k in range(150):
        ang = rng.uniform(0, math.tau)
        wdt = rng.uniform(0.004, 0.03)
        r0 = rng.uniform(150, 520)
        r1 = rng.uniform(1300, 2200)
        pts = [(fx + math.cos(ang) * r0, fy + math.sin(ang) * r0 * 0.8),
               (fx + math.cos(ang + wdt) * r1, fy + math.sin(ang + wdt) * r1 * 0.8),
               (fx + math.cos(ang - wdt) * r1, fy + math.sin(ang - wdt) * r1 * 0.8)]
        col = (255, 255, 255, rng.randint(70, 200)) if rng.random() < 0.7 else tint(g["dark"], 0.9) + (rng.randint(110, 200),)
        dr.polygon(pts, fill=col)
    bg.alpha_composite(ln)
    L["bg_lines"] = bg
    # 2) 먹물: 큰 붓질 두 줄 + 튀김
    ink = Image.new("L", (W, H), 0)
    di = ImageDraw.Draw(ink)
    di.polygon([(-60, 560), (760, 430), (1100, 520), (700, 700), (-60, 820)], fill=255)          # 왼쪽 큰 먹 붓질(글자 밑판)
    di.polygon([(1500, 1000), (2000, 860), (2000, 1100), (1400, 1100)], fill=255)
    di.polygon([(-40, -40), (700, -40), (420, 120), (-40, 220)], fill=255)
    for _ in range(95):                                                           # 튀김 방울
        cx, cy = rng.choice([(rng.uniform(380, 1250), rng.uniform(300, 760)), (rng.uniform(-20, 300), rng.uniform(40, 300)), (rng.uniform(1300, 1900), rng.uniform(820, 1060))])
        r = rng.choice([4, 6, 9, 14, 22]) * rng.uniform(0.6, 1.3)
        di.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    ink = ragged(ink, 9, 3, 5)
    L["ink"] = Image.merge("RGBA", (Image.new("L", (W, H), 8), Image.new("L", (W, H), 6), Image.new("L", (W, H), 10), ink))
    # 3) 붓글씨 별명(크게) + 작은 글씨
    t = Image.new("L", (W, H), 0)
    size = 330
    while True:
        f = font("EastSeaDokdo-Regular.ttf", size)
        box = ImageDraw.Draw(t).textbbox((0, 0), nick, font=f)
        if box[2] - box[0] <= 1150 or size < 90:
            break
        size -= 10
    tt = Image.new("L", (W, H), 0)
    ImageDraw.Draw(tt).text((90 - box[0], 540 - box[1] - (box[3] - box[1]) // 2), nick, font=f, fill=255, stroke_width=3, stroke_fill=255)
    tt = tt.rotate(7, resample=Image.BICUBIC, center=(500, 540))
    tt = ragged(tt, 5, 5, 1.2)
    glow = tt.filter(ImageFilter.GaussianBlur(12)).point(lambda v: min(255, v * 2))
    L["text_glow"] = Image.merge("RGBA", (Image.new("L", (W, H), 255), Image.new("L", (W, H), 255), Image.new("L", (W, H), 255), glow))
    L["text"] = Image.merge("RGBA", (Image.new("L", (W, H), 0), Image.new("L", (W, H), 0), Image.new("L", (W, H), 0), tt))
    sm = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ds = ImageDraw.Draw(sm)
    ds.polygon([(80, 790), (760, 760), (790, 850), (60, 880)], fill=(8, 6, 10, 235))
    ds.text((110, 800), f"{g['label']}  {name}", font=font("NanumGothic-ExtraBold.ttf", 54), fill=(255, 255, 255, 255))
    L["label"] = sm
    # 4) 얼굴(기울임 8°) — 오른쪽
    face = Image.open(face_png).convert("RGBA")
    fh = 1020
    face = face.resize((int(face.width * fh / face.height), fh), Image.LANCZOS).rotate(-8, resample=Image.BICUBIC, expand=True)
    fl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fl.alpha_composite(face, (1300 - face.width // 2, 520 - face.height // 2))
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    a = fl.split()[3].filter(ImageFilter.GaussianBlur(10))
    sh = Image.merge("RGBA", (Image.new("L", (W, H), 0), Image.new("L", (W, H), 0), Image.new("L", (W, H), 0), a.point(lambda v: int(v * 0.45))))
    L["face_shadow"] = ImageChops.offset(sh, 14, 14)
    L["face"] = fl
    # 5) 하얀 칼자국 선
    sl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dsl = ImageDraw.Draw(sl)
    for (x0, y0, x1, y1, w0) in [(-100, 980, 2000, 120, 7), (-100, 100, 1500, 1100, 4), (500, 1150, 2000, 300, 3)]:
        n = 60
        for i in range(n):
            t0, t1 = i / n, (i + 1) / n
            w = w0 * (1 - abs(2 * (t0 + t1) / 2 - 1)) + 0.8
            dsl.line([(x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0), (x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1)], fill=(255, 255, 255, 235), width=max(1, int(w)))
    L["slash"] = sl
    return L


# ═════════ B안: 소개 카드 층 ═════════
def marble(w, h, c1, c2, c3, seed):
    n1 = noise(w, h, 80, 5, seed)
    n2 = noise(w, h, 40, 5, seed + 1)
    warp = np.clip(n1 + (n2 - 0.5) * 0.8, 0, 1)
    veins = np.abs(np.sin(warp * 14.0)) ** 3
    img = np.zeros((h, w, 3), np.float32)
    for i in range(3):
        img[..., i] = c1[i] * (1 - warp) + c2[i] * warp
        img[..., i] = img[..., i] * (1 - veins * 0.7) + c3[i] * veins * 0.7
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))


def layers_B(bust_png, name, nick, grade):
    g = GRADE[grade]
    L = {}
    # 1) 바탕 + 오른쪽 동심원 + 대리석
    bg = Image.new("RGB", (W, H), g["main"])
    px = np.asarray(bg, np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    vg = 1 - 0.22 * np.clip(np.sqrt((xx - 1300) ** 2 + (yy - 380) ** 2) / 1400, 0, 1)
    bg = Image.fromarray(np.clip(px * vg[..., None], 0, 255).astype(np.uint8)).convert("RGBA")
    cx, cy = 1360, 400
    R = 560
    mk = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mk).ellipse((cx - R, cy - R, cx + R, cy + R), fill=255)
    mb = marble(W, H, (255, 255, 255), tint(g["main"], 0.9), g["accent"], 4)
    disc = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    disc.paste(mb.convert("RGBA"), (0, 0), mk.point(lambda v: int(v * 0.85)))
    dd = ImageDraw.Draw(disc)
    dark = g["dark"] + (255,)
    for rr, wd in [(R, 10), (R - 60, 5), (R - 150, 4), (R - 175, 12)]:
        dd.ellipse((cx - rr, cy - rr, cx + rr, cy + rr), outline=dark, width=wd)
    for k, (ox, oy, rr) in enumerate([(-190, -40, 150), (140, -150, 130), (60, 170, 160)]):                 # 안쪽 큰 원 셋(톱니바퀴 느낌)
        dd.ellipse((cx + ox - rr, cy + oy - rr, cx + ox + rr, cy + oy + rr), outline=dark, width=14)
        dd.ellipse((cx + ox - rr + 40, cy + oy - rr + 40, cx + ox + rr - 40, cy + oy + rr - 40), fill=tint(g["dark"], 1.0) + (200,))
        for a in range(3):
            ang = a * 2.094 + k
            dd.line([(cx + ox, cy + oy), (cx + ox + math.cos(ang) * rr, cy + oy + math.sin(ang) * rr)], fill=(255, 255, 255, 200), width=8)
    bg.alpha_composite(disc)
    # 망점(왼쪽 위·오른쪽 아래 모서리로 갈수록 작아지는)
    ht = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dh = ImageDraw.Draw(ht)
    step = 22
    for y in range(0, H, step):
        for x in range(0, W, step):
            ox = x + (step // 2 if (y // step) % 2 else 0)
            v = max(0.0, 1 - math.hypot(ox - 120, y - 80) / 700)
            v2 = max(0.0, 1 - math.hypot(ox - 1900, y - 1050) / 600)
            r = (v + v2) * step * 0.45
            if r > 1:
                dh.ellipse((ox - r, y - r, ox + r, y + r), fill=tint(g["dark"], 0.8) + (150,))
    bg.alpha_composite(ht)
    L["bg"] = bg
    # 2) 먹 띠 + 흰 줄 + 평행선 (수평으로 그린 뒤 기울인다)
    BW, BH = 2600, 700
    band = Image.new("RGBA", (BW, BH), (0, 0, 0, 0))
    db = ImageDraw.Draw(band)
    y0 = 280
    # 평행선(청록·노랑·분홍) — 띠 위아래로 얇게
    for off, col, wd in [(-70, (0, 235, 235, 255), 7), (-52, (255, 235, 40, 255), 4), (-36, (255, 70, 170, 255), 9), (-16, (255, 255, 255, 255), 3), (190, (255, 70, 170, 255), 8), (210, (0, 235, 235, 255), 5), (236, (255, 235, 40, 255), 4), (262, (20, 15, 25, 255), 10)]:
        db.line([(0, y0 + off), (BW, y0 + off)], fill=col, width=wd)
    # 먹 띠: 왼쪽이 너덜한 붓질
    bm = Image.new("L", (BW, BH), 0)
    dbm = ImageDraw.Draw(bm)
    dbm.polygon([(1040, y0 - 8), (BW, y0 - 8), (BW, y0 + 118), (1090, y0 + 126)], fill=255)
    for _ in range(40):
        cx2, cy2 = rng.uniform(1000, 1200), rng.uniform(y0 - 60, y0 + 200)
        r = rng.choice([5, 8, 12, 20]) * rng.uniform(0.7, 1.2)
        dbm.ellipse((cx2 - r, cy2 - r, cx2 + r, cy2 + r), fill=255)
    for _ in range(18):
        cx2, cy2 = rng.uniform(1300, 2000), rng.choice([y0 - 40, y0 + 150])
        r = rng.choice([4, 6, 9]) * rng.uniform(0.7, 1.2)
        dbm.ellipse((cx2 - r, cy2 - r, cx2 + r, cy2 + r), fill=255)
    bm = ragged(bm, 7, 9, 2.5)
    band.paste(Image.new("RGBA", (BW, BH), (8, 6, 12, 255)), (0, 0), bm)
    # 흰 줄(칭호)
    db.polygon([(1250, y0 + 138), (BW, y0 + 138), (BW, y0 + 232), (1210, y0 + 232)], fill=(255, 255, 255, 255))
    # 이름: 둘째 글자만 강조색(흰 글씨 + 분홍)
    fn = font("NanumGothic-ExtraBold.ttf", 108)
    x = 1300
    for i, ch in enumerate(name):
        col = g["accent"] + (255,) if i == 1 else (255, 255, 255, 255)
        db.text((x, y0 - 10), ch, font=fn, fill=col)
        x += db.textlength(ch, font=fn) + 6
    ft = font("NanumGothic-ExtraBold.ttf", 54)
    title = f"{g['label']} {nick}"
    while db.textlength(title, font=ft) > 900 and ft.size > 26:
        ft = font("NanumGothic-ExtraBold.ttf", ft.size - 4)
    tx = 1330
    db.text((tx + 3, y0 + 150 + 3), title, font=ft, fill=tint(g["accent"], 0.45) + (255,))
    db.text((tx, y0 + 150), title, font=ft, fill=g["accent"] + (255,))
    band = band.rotate(-9, resample=Image.BICUBIC, center=(BW // 2, y0 + 100), expand=False)
    bl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bl.alpha_composite(band, (-340, 90))
    L["band"] = bl
    # 3) 상반신 — 왼쪽
    bust = Image.open(bust_png).convert("RGBA")
    bh = 1180
    bust = bust.resize((int(bust.width * bh / bust.height), bh), Image.LANCZOS)
    bu = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    bu.alpha_composite(bust, (-120, H - bh + 70))
    a = bu.split()[3].filter(ImageFilter.GaussianBlur(10))
    sh = Image.merge("RGBA", (Image.new("L", (W, H), 0), Image.new("L", (W, H), 0), Image.new("L", (W, H), 0), a.point(lambda v: int(v * 0.4))))
    L["bust_shadow"] = ImageChops.offset(sh, 16, 12)
    L["bust"] = bu
    return L


# ═════════ 움직임 ═════════
def ease_out(t):
    return 1 - (1 - min(max(t, 0), 1)) ** 3


def ease_in(t):
    return min(max(t, 0), 1) ** 3


def place(layer, dx=0.0, dy=0.0, s=1.0, a=1.0, rot=0.0):
    im = layer
    if s != 1.0:
        im = im.resize((int(W * s), int(H * s)), Image.BILINEAR)
        canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        canvas.alpha_composite(im, (int((W - im.width) / 2 + dx), int((H - im.height) / 2 + dy))) if (int((W - im.width) / 2 + dx) >= 0 and int((H - im.height) / 2 + dy) >= 0) else canvas.paste(im, (int((W - im.width) / 2 + dx), int((H - im.height) / 2 + dy)))
        im = canvas
    elif dx or dy:
        im = ImageChops.offset(im, int(dx), int(dy))
        # offset은 감싸므로 밀려난 쪽을 지운다
        m = Image.new("L", (W, H), 255)
        dm = ImageDraw.Draw(m)
        if dx > 0:
            dm.rectangle((0, 0, dx, H), fill=0)
        elif dx < 0:
            dm.rectangle((W + dx, 0, W, H), fill=0)
        if dy > 0:
            dm.rectangle((0, 0, W, dy), fill=0)
        elif dy < 0:
            dm.rectangle((0, H + dy, W, H), fill=0)
        im = Image.merge("RGBA", im.split()[:3] + (ImageChops.multiply(im.split()[3], m),))
    if a < 1.0:
        im = Image.merge("RGBA", im.split()[:3] + (im.split()[3].point(lambda v: int(v * a)),))
    return im


T_IN, T_OUT, TOTAL = 0.22, 1.2, 1.5


def prog(t, delay=0.0, dur=T_IN):
    """등장 진행(0→1)과 퇴장 진행(0→1)."""
    return ease_out((t - delay) / dur), ease_in((t - T_OUT - delay * 0.3) / (TOTAL - T_OUT))


def frame_A(L, game, t):
    im = game.copy().convert("RGBA")
    dark = Image.new("RGBA", (W, H), (0, 0, 0, int(150 * min(1, t / 0.12) * (1 - ease_in((t - 1.3) / 0.2)))))
    im.alpha_composite(dark)
    pin, pout = prog(t, 0.0)
    drift = 1.0 + 0.04 * (t / TOTAL)
    # 바탕: 확대하며 들어오고 퇴장 땐 사선으로 밀려남
    s = 1.35 - 0.35 * pin
    im.alpha_composite(place(L["bg_lines"], dx=-700 * pout, dy=-220 * pout, s=s * drift, a=min(1, pin * 1.6) * (1 - pout)))
    pi2, po2 = prog(t, 0.04)
    im.alpha_composite(place(L["ink"], dx=-900 * (1 - pi2) - 900 * po2, dy=60 * (1 - pi2), a=1 - po2))
    pi3, po3 = prog(t, 0.08)
    pi4, po4 = prog(t, 0.0)
    im.alpha_composite(place(L["face_shadow"], dx=1300 * (1 - pi4) + 1300 * po4, dy=-120 * (1 - pi4), a=1 - po4))
    im.alpha_composite(place(L["face"], dx=1300 * (1 - pi4) + 1300 * po4, dy=-120 * (1 - pi4), s=1.0 + 0.03 * (t / TOTAL), a=1 - po4))
    im.alpha_composite(place(L["text_glow"], dx=-1400 * (1 - pi3) - 1400 * po3, dy=40 * (1 - pi3), a=(1 - po3) * 0.9))
    im.alpha_composite(place(L["text"], dx=-1400 * (1 - pi3) - 1400 * po3, dy=40 * (1 - pi3), a=1 - po3))
    pi5, po5 = prog(t, 0.1)
    im.alpha_composite(place(L["label"], dx=-1100 * (1 - pi5) - 1100 * po5, a=1 - po5))
    pi6, po6 = prog(t, 0.0, 0.14)
    im.alpha_composite(place(L["slash"], dx=900 * (1 - pi6) - 900 * po6, dy=-500 * (1 - pi6), a=1 - po6))
    if t < 0.06:                                                      # 첫 프레임 번쩍
        fl = Image.new("RGBA", (W, H), (255, 255, 255, int(190 * (1 - t / 0.06))))
        im.alpha_composite(fl)
    return im.convert("RGB")


def frame_B(L, game, t):
    im = game.copy().convert("RGBA")
    dark = Image.new("RGBA", (W, H), (0, 0, 0, int(130 * min(1, t / 0.1) * (1 - ease_in((t - 1.3) / 0.2)))))
    im.alpha_composite(dark)
    pin, pout = prog(t, 0.0, 0.16)
    im.alpha_composite(place(L["bg"], dy=-60 * pout, a=min(1, pin * 1.5) * (1 - pout), s=1.0 + 0.03 * (1 - pin)))
    pb, ob = prog(t, 0.05, 0.2)
    im.alpha_composite(place(L["band"], dx=2300 * (1 - pb) - 2300 * ob, a=1))
    pc, oc = prog(t, 0.16, 0.24)
    im.alpha_composite(place(L["bust_shadow"], dx=-900 * (1 - pc) - 900 * oc, a=1 - oc))
    im.alpha_composite(place(L["bust"], dx=-900 * (1 - pc) - 900 * oc, s=1.0 + 0.025 * (t / TOTAL), a=1 - oc))
    if t < 0.05:
        im.alpha_composite(Image.new("RGBA", (W, H), (255, 255, 255, int(160 * (1 - t / 0.05)))))
    return im.convert("RGB")


def make(style, unit, grade, name, nick):
    out = os.path.join(HOME, f"{style}_{unit}")
    game = Image.open(GAME).convert("RGB").resize((W, H))
    if style == "A":
        L = layers_A(os.path.join(HOME, "render", f"A_{unit}_face.png"), nick, name, grade)
        fr = frame_A
    else:
        L = layers_B(os.path.join(HOME, "render", f"B_{unit}_bust.png"), name, nick, grade)
        fr = frame_B
    os.makedirs(os.path.join(HOME, "layers"), exist_ok=True)
    for k, v in L.items():
        v.save(os.path.join(HOME, "layers", f"{style}_{unit}_{k}.png"))
    fr(L, game, 0.65).save(out + "_still.png")
    tmp = os.path.join(HOME, "_frames")
    os.makedirs(tmp, exist_ok=True)
    n = int(TOTAL * 30)
    for i in range(n):
        fr(L, game, i / 30.0).save(os.path.join(tmp, f"f{i:03d}.png"))
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "30", "-i", os.path.join(tmp, "f%03d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", out + ".mp4"], check=True)
    for i in range(n):
        os.remove(os.path.join(tmp, f"f{i:03d}.png"))
    print("done", out)


if __name__ == "__main__":
    # 유닛, 등급, 이름, 별명
    JOBS = [("초월_최상호_AD", "초월", "최상호", "구일에서가장자유로운남자"), ("초월_노태현_AP", "초월", "노태현", "여동생살해자")]
    only = sys.argv[1:]
    for unit, grade, name, nick in JOBS:
        for style in ("A", "B"):
            if only and f"{style}_{unit}" not in only:
                continue
            make(style, unit, grade, name, nick)
