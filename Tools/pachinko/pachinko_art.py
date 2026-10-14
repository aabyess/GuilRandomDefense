"""30R 보스 파칭코 화면용 그림 재료(2026-10-14, blender): 숫자 심볼 1~9(+흐림판) · 글자 로고 6 · 기계 틀 + 릴 창 json.
시안 3(claude.ai/artifact/Y1Lwz6UjzAzchRANw58ACR) 색감 그대로. 폰트: Dela Gothic One(OFL 1.1, Google Fonts) 전부. 그림은 전부 자체 제작(코드로 그림).
  /usr/bin/python3 Tools/pachinko/pachinko_art.py [출력폴더=~/GRD_pachinko_art]   폰트는 <출력>/_fonts/DelaGothicOne-Regular.ttf"""
import json, math, os, sys
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont
OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_pachinko_art")
FONT = OUT + "/_fonts/DelaGothicOne-Regular.ttf"
OUTLINE = (26, 7, 16, 255)
def hexc(h): h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
def gradient(size, stops):
    """세로 그라데이션. stops=[(pos0..1, '#rrggbb')…]"""
    w, h = size; col = Image.new("RGB", (1, h))
    px = col.load()
    for y in range(h):
        t = y / max(1, h - 1)
        for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
            if p0 <= t <= p1:
                u = (t - p0) / max(1e-6, p1 - p0); a, b = hexc(c0), hexc(c1)
                px[0, y] = tuple(int(a[i] + (b[i] - a[i]) * u) for i in range(3)); break
    return col.resize((w, h))
def text_mask(text, font_px, pad, size):
    f = ImageFont.truetype(FONT, font_px); m = Image.new("L", size, 0); d = ImageDraw.Draw(m)
    bb = d.textbbox((0, 0), text, font=f); w, h = bb[2] - bb[0], bb[3] - bb[1]
    d.text(((size[0] - w) // 2 - bb[0], (size[1] - h) // 2 - bb[1]), text, font=f, fill=255)
    return m
def grow(mask, r):
    return mask.filter(ImageFilter.MaxFilter(r * 2 + 1)) if r > 0 else mask
def fat_text(text, size, font_px, stops, outline_px, depth, glow=None, glow_px=0, stroke=OUTLINE, gloss=True):
    """두꺼운 입체 글자: 그림자 → 돌출(depth) → 외곽선 → 그라데이션 면 → 광택 → (선택) 글로우."""
    S = 2; W, H = size[0] * S, size[1] * S
    base = text_mask(text, font_px * S, 0, (W, H))
    base = base.filter(ImageFilter.GaussianBlur(0.8 * S)).point(lambda v: 255 if v > 110 else int(v * 255 / 110))   # 가장자리 매끈
    ol = grow(base, outline_px * S)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if glow and glow_px:
        g = grow(ol, glow_px * S // 2).filter(ImageFilter.GaussianBlur(glow_px * S))
        gl = Image.new("RGBA", (W, H), hexc(glow) + (255,)); gl.putalpha(g.point(lambda v: min(255, int(v * 1.6)))); img.alpha_composite(gl)
    # 돌출: 외곽선 모양을 아래로 depth만큼 쌓아 어두운 면으로
    ext = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for k in range(depth * S, 0, -1):
        layer = Image.new("RGBA", (W, H), stroke[:3] + (255,)); layer.putalpha(ol)
        ext.alpha_composite(layer, (0, k))
    sh = ol.filter(ImageFilter.GaussianBlur(6 * S)); shadow = Image.new("RGBA", (W, H), (0, 0, 0, 150)); shadow.putalpha(sh.point(lambda v: v * 150 // 255))
    img.alpha_composite(shadow, (4 * S, (depth + 6) * S)); img.alpha_composite(ext)
    top = Image.new("RGBA", (W, H), stroke[:3] + (255,)); top.putalpha(ol); img.alpha_composite(top)
    # 면: 글자 영역 높이에 맞춘 그라데이션
    bb = base.getbbox() or (0, 0, W, H)
    grad = Image.new("RGB", (W, H), hexc(stops[0][1])); gh = gradient((W, bb[3] - bb[1]), stops); grad.paste(gh, (0, bb[1]))
    face = grad.convert("RGBA"); face.putalpha(base); img.alpha_composite(face)
    if gloss:   # 위쪽 절반 광택 + 가장자리 안쪽 하이라이트
        gm = Image.new("L", (W, H), 0); gd = ImageDraw.Draw(gm)
        gd.ellipse((bb[0] - (bb[2] - bb[0]) * .1, bb[1] - (bb[3] - bb[1]) * .85, bb[2] + (bb[2] - bb[0]) * .1, bb[1] + (bb[3] - bb[1]) * .55), fill=120)
        gm = ImageChops.multiply(gm, base)
        hi = Image.new("RGBA", (W, H), (255, 255, 255, 255)); hi.putalpha(gm); img.alpha_composite(hi)
        edge = ImageChops.subtract(base, base.filter(ImageFilter.MinFilter(5 * S + 1)))
        e = Image.new("RGBA", (W, H), (255, 255, 255, 255)); e.putalpha(edge.point(lambda v: v * 110 // 255)); img.alpha_composite(e.filter(ImageFilter.GaussianBlur(S)))
    return img.resize(size, Image.LANCZOS)
def motion_blur(img, px):
    """세로 모션블러(회전 중 릴용): ±px 범위 평균 + 위아래로 번짐."""
    acc = None; n = 41
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    premul = img.copy()
    layers = []
    for i in range(n):
        dy = int(-px + 2 * px * i / (n - 1)); layers.append(ImageChops.offset(premul, 0, dy))
    # 알파 가중 평균
    import numpy as np
    arr = np.stack([np.asarray(l).astype("float32") for l in layers])
    a = arr[..., 3:4]; rgb = (arr[..., :3] * a).sum(0) / np.maximum(a.sum(0), 1e-3); al = a.mean(0)
    res = np.concatenate([rgb, al * 1.15], axis=-1).clip(0, 255).astype("uint8")
    return Image.fromarray(res).convert("RGBA").filter(ImageFilter.GaussianBlur(2.5))
os.makedirs(OUT + "/symbols", exist_ok=True); os.makedirs(OUT + "/logos", exist_ok=True)
RED = [(0, "#ff8fb1"), (.55, "#ff1f4b"), (1, "#a3001e")]; BLUE = [(0, "#9fd8ff"), (.55, "#1f7bff"), (1, "#0a2f9a")]; GREEN = [(0, "#c6ffb0"), (.55, "#16c24e"), (1, "#04652a")]
GOLD7 = [(0, "#fff2a0"), (.40, "#ffc400"), (.75, "#ff1f4b"), (1, "#8a0016")]
SYM = {1: RED, 4: RED, 7: GOLD7, 2: BLUE, 5: BLUE, 8: BLUE, 3: GREEN, 6: GREEN, 9: GREEN}
for n, st in SYM.items():
    im = fat_text(str(n), (512, 512), 470, st, 12, 14, glow=("#ffd23f" if n == 7 else None), glow_px=14 if n == 7 else 0)
    im.save(f"{OUT}/symbols/symbol_{n}.png")
    motion_blur(im, 64).save(f"{OUT}/symbols/symbol_{n}_blur.png")
LOGOS = {  # 파일 · 글자 · 폰트px · 그라데이션 · 글로우 · 외곽선
    "reach": ("リーチ!!", 330, [(0, "#ffffff"), (.5, "#ffd0dc"), (1, "#ff3b6b")], "#ff1f6b", "#3a0010"),
    "ooatari": ("大当たり!!", 300, [(0, "#fffbe0"), (.35, "#ffd23f"), (.6, "#ff8a00"), (1, "#ff1f4b")], "#ff8a00", "#4a0e00"),
    "keizoku": ("継続", 480, [(0, "#ffffff"), (.55, "#f1f1f1"), (1, "#ffd23f")], "#ffffff", "#000000"),
    "fukkatsu": ("復活!!", 380, [(0, "#ffffff"), (.5, "#ffe6a0"), (1, "#ff6aa8")], "#ff1f6b", "#3a0010"),
    "gekiatsu": ("激アツ", 420, [(0, "#fffbe0"), (.3, "#ffd23f"), (.62, "#ff4f2d"), (1, "#c3001e")], "#ff1f4b", "#3a0006"),
    "zannen": ("ざんねん", 330, [(0, "#f4eefb"), (.55, "#cfc3dc"), (1, "#7b6a96")], "#6b4dff", "#1a0f2a"),
}
for key, (txt, px, st, glow, stroke) in LOGOS.items():
    W = 1024; font = ImageFont.truetype(FONT, px); bb = ImageDraw.Draw(Image.new("L", (8, 8))).textbbox((0, 0), txt, font=font)
    tw = bb[2] - bb[0]; px2 = int(px * min(1.0, (W - 160) / max(tw, 1)))                           # 1024폭 안에 맞춤(양옆 80 여백)
    font = ImageFont.truetype(FONT, px2); bb = ImageDraw.Draw(Image.new("L", (8, 8))).textbbox((0, 0), txt, font=font); H = int((bb[3] - bb[1]) + 200)
    fat_text(txt, (W, H), px2, st, 12, 14, glow=glow, glow_px=18, stroke=hexc(stroke) + (255,)).save(f"{OUT}/logos/logo_{key}.png")
# ── 기계 틀 1920×1080 ──
SC = 2; FW, FH = 1920 * SC, 1080 * SC
X0, Y0, MW, MH = 330, 190, 1260, 760          # 기계 바깥 사각형(1920×1080 기준)
frame = Image.new("RGBA", (FW, FH), (0, 0, 0, 0)); d = ImageDraw.Draw(frame)
def rr(box, r, fill=None, outline=None, width=1): d.rounded_rectangle([v * SC for v in box], r * SC, fill=fill, outline=outline, width=width * SC)
# 바깥 광채
glow = Image.new("L", (FW, FH), 0); ImageDraw.Draw(glow).rounded_rectangle([(X0 - 6) * SC, (Y0 - 6) * SC, (X0 + MW + 6) * SC, (Y0 + MH + 6) * SC], 30 * SC, fill=255)
gl = Image.new("RGBA", (FW, FH), (255, 176, 0, 255)); gl.putalpha(glow.filter(ImageFilter.GaussianBlur(22 * SC)).point(lambda v: v * 190 // 255)); frame.alpha_composite(gl)
rr((X0 - 10, Y0 - 10, X0 + MW + 10, Y0 + MH + 10), 30, fill=(42, 18, 0, 255))
rr((X0 - 6, Y0 - 6, X0 + MW + 6, Y0 + MH + 6), 27, fill=(255, 210, 63, 255))
rr((X0 - 2, Y0 - 2, X0 + MW + 2, Y0 + MH + 2), 24, fill=(42, 18, 0, 255))
# 금 테두리 면: 그라데이션을 마스크로
band = Image.new("L", (FW, FH), 0); bd = ImageDraw.Draw(band)
bd.rounded_rectangle([X0 * SC, Y0 * SC, (X0 + MW) * SC, (Y0 + MH) * SC], 22 * SC, fill=255)
gold = gradient((FW, MH * SC), [(0, "#fff4b0"), (.18, "#ffb300"), (.5, "#a35a00"), (.62, "#ffe27a"), (.85, "#ff8a00"), (1, "#7a3d06")])
gl2 = Image.new("RGB", (FW, FH), (0, 0, 0)); gl2.paste(gold, (0, Y0 * SC)); gf = gl2.convert("RGBA"); gf.putalpha(band); frame.alpha_composite(gf)
# 창 영역 계산
PAD_X, TOP, BOT = 62, 132, 120
WX0, WY0, WX1, WY1 = X0 + PAD_X, Y0 + TOP, X0 + MW - PAD_X, Y0 + MH - BOT
GAP, INSET = 22, 22
rw = (WX1 - WX0 - 2 * INSET - 2 * GAP) / 3
reels = [dict(index=i, x=round(WX0 + INSET + i * (rw + GAP)), y=WY0 + INSET, w=round(rw), h=WY1 - WY0 - 2 * INSET) for i in range(3)]
# 창 패널: 어두운 보라 틀 + 릴 구멍(투명)
rr((WX0 - 8, WY0 - 8, WX1 + 8, WY1 + 8), 18, fill=(42, 18, 0, 255))
rr((WX0 - 3, WY0 - 3, WX1 + 3, WY1 + 3), 15, fill=(255, 210, 63, 255))
panel = Image.new("L", (FW, FH), 0); ImageDraw.Draw(panel).rounded_rectangle([WX0 * SC, WY0 * SC, WX1 * SC, WY1 * SC], 12 * SC, fill=255)
pg = Image.new("RGB", (FW, FH)); pg.paste(gradient((FW, (WY1 - WY0) * SC), [(0, "#1a0628"), (1, "#0a0212")]), (0, WY0 * SC)); pf = pg.convert("RGBA"); pf.putalpha(panel); frame.alpha_composite(pf)
hole = Image.new("L", (FW, FH), 0); hd = ImageDraw.Draw(hole)
for r in reels:
    hd.rounded_rectangle([r["x"] * SC, r["y"] * SC, (r["x"] + r["w"]) * SC, (r["y"] + r["h"]) * SC], 10 * SC, fill=255)
    rr((r["x"] - 3, r["y"] - 3, r["x"] + r["w"] + 3, r["y"] + r["h"] + 3), 12, fill=(42, 18, 0, 255))
a = frame.getchannel("A"); a = ImageChops.subtract(a, hole); frame.putalpha(a)
# 릴 구멍 가장자리 안쪽 그림자(위아래 어둡게) — 구멍은 비워 두고 테두리만
dd = ImageDraw.Draw(frame)
for r in reels:
    for k in range(10):
        al = int(150 * (1 - k / 10))
        dd.rounded_rectangle([(r["x"] + k) * SC, (r["y"] + k) * SC, (r["x"] + r["w"] - k) * SC, (r["y"] + r["h"] - k) * SC], 10 * SC, outline=(0, 0, 0, al), width=SC)
# 페이라인(가운데 가로선) — 릴 위에 덮는 얇은 붉은 선
cy = reels[0]["y"] + reels[0]["h"] // 2
line = Image.new("RGBA", (FW, FH), (0, 0, 0, 0)); ld = ImageDraw.Draw(line)
ld.rectangle([WX0 * SC, (cy - 1) * SC, WX1 * SC, (cy + 1) * SC], fill=(255, 31, 107, 255))
lg = line.filter(ImageFilter.GaussianBlur(7 * SC)); frame.alpha_composite(lg); frame.alpha_composite(line)
# 전구 자리: 바깥 테두리 한 줄 + 위 마키 줄
bulbs = []
def bulb(x, y, kind):
    bulbs.append(dict(x=round(x), y=round(y), kind=kind))
step = 40
bx0, by0, bx1, by1 = X0 + 20, Y0 + 20, X0 + MW - 20, Y0 + MH - 20
x = bx0
while x <= bx1: bulb(x, by0, "top"); bulb(x, by1, "bottom"); x += step
y = by0 + step
while y < by1: bulb(bx0, y, "left"); bulb(bx1, y, "right"); y += step
for b in bulbs:
    cx, cy2 = b["x"] * SC, b["y"] * SC
    dd.ellipse([cx - 11 * SC, cy2 - 11 * SC, cx + 11 * SC, cy2 + 11 * SC], fill=(42, 18, 0, 255))
    dd.ellipse([cx - 8 * SC, cy2 - 8 * SC, cx + 8 * SC, cy2 + 8 * SC], fill=(255, 245, 190, 255))
    dd.ellipse([cx - 4 * SC, cy2 - 5 * SC, cx + 2 * SC, cy2 + 1 * SC], fill=(255, 255, 255, 255))
# 위쪽 마키(글자 자리) 판 + 보석 둘
MX0, MY0, MX1, MY1 = X0 + 250, Y0 + 40, X0 + MW - 250, Y0 + 100
rr((MX0 - 4, MY0 - 4, MX1 + 4, MY1 + 4), 14, fill=(42, 18, 0, 255)); rr((MX0, MY0, MX1, MY1), 11, fill=(255, 226, 150, 255))
dd.rounded_rectangle([(MX0 + 6) * SC, (MY0 + 6) * SC, (MX1 - 6) * SC, (MY0 + 28) * SC], 8 * SC, fill=(255, 250, 220, 255))
for gx, c in ((X0 + 160, (255, 31, 107)), (X0 + MW - 160, (41, 242, 255))):
    for rad, col in ((26, (42, 18, 0, 255)), (21, c + (255,)), (10, (255, 255, 255, 230))):
        dd.ellipse([(gx - rad) * SC, (Y0 + 70 - rad) * SC, (gx + rad) * SC, (Y0 + 70 + rad) * SC], fill=col)
# 아래쪽: 홀드 구슬 4칸 + 카운터 틈
holds = []
for i in range(4):
    hx = X0 + MW // 2 - 90 + i * 60; hy = Y0 + MH - 58; holds.append(dict(x=hx, y=hy, r=15))
    for rad, col in ((18, (42, 18, 0, 255)), (14, (154, 166, 184, 255)), (6, (255, 255, 255, 230))):
        dd.ellipse([(hx - rad) * SC, (hy - rad) * SC, (hx + rad) * SC, (hy + rad) * SC], fill=col)
frame = frame.resize((1920, 1080), Image.LANCZOS); frame.save(OUT + "/machine_frame_1920x1080.png")
json.dump(dict(canvas=[1920, 1080], machine=dict(x=X0, y=Y0, w=MW, h=MH), reels=reels, paylineY=reels[0]["y"] + reels[0]["h"] // 2,
               marquee=dict(x=MX0, y=MY0, w=MX1 - MX0, h=MY1 - MY0), holds=holds, bulbs=bulbs,
               note="reels = 투명 구멍(릴 이미지를 틀 뒤에 깔면 이 칸으로 보임). 숫자 심볼은 릴 폭에 맞춰 줄이면 됨(512 정사각). bulbs=전구 자리 중심점(점멸은 구현 쪽)"),
          open(OUT + "/machine_layout.json", "w"), ensure_ascii=False, indent=1)
# 시트
sheet = Image.new("RGB", (1920, 1500), (30, 28, 38)); sd = ImageDraw.Draw(sheet)
lab = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 22, index=0)
for i, n in enumerate(range(1, 10)):
    for row, suf in enumerate(("", "_blur")):
        s = Image.open(f"{OUT}/symbols/symbol_{n}{suf}.png").resize((200, 200)); bg = Image.new("RGB", (200, 200), (250, 240, 214) if row == 0 else (250, 240, 214)); bg.paste(s, (0, 0), s)
        sheet.paste(bg, (30 + i * 205, 30 + row * 215))
sd.text((30, 460), "숫자 심볼 1~9 (위: 정지 / 아래: 회전 모션블러) · 512×512 투명 PNG(밝은 바탕 위에 올려 봄)", font=lab, fill=(255, 235, 170))
y = 500
for key in LOGOS:
    s = Image.open(f"{OUT}/logos/logo_{key}.png"); w = 520; s = s.resize((w, int(s.height * w / s.width))); bg = Image.new("RGB", s.size, (40, 14, 60)); bg.paste(s, (0, 0), s)
    col = list(LOGOS).index(key); sheet.paste(bg, (30 + (col % 3) * 620, y + (col // 3) * 300))
fr = Image.open(OUT + "/machine_frame_1920x1080.png"); bgf = Image.new("RGB", fr.size, (20, 80, 110)); bgf.paste(fr, (0, 0), fr)
sheet2 = Image.new("RGB", (1920, 1130 + 540), (30, 28, 38)); sheet2.paste(sheet.crop((0, 0, 1920, 1130)), (0, 0)); sheet2.paste(bgf.resize((960, 540)), (30, 1140))
sheet2.save(OUT + "/contact_sheet.png"); print("done", OUT)
