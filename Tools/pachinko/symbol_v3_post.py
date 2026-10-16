"""v3 후처리: 세로 모션블러(_blur, 정지판 기준 + 불꽃은 f0) + contact_sheet_v3. /usr/bin/python3 Tools/pachinko/symbol_v3_post.py [폴더]"""
import os, sys, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/grd/GRD_pachinko_art/symbols_v3")
def motion_blur(im, px=90, n=41):
    arr = np.asarray(im.convert("RGBA")).astype("float32"); acc = np.zeros(arr.shape[:2] + (3,), "float32"); wsum = np.zeros(arr.shape[:2] + (1,), "float32")
    for k in range(n):
        sh = np.roll(arr, int(-px + 2 * px * k / (n - 1)), axis=0); a = sh[..., 3:4] / 255; acc += sh[..., :3] * a; wsum += a
    rgb = acc / np.maximum(wsum, 1e-3); al = (wsum / n * 255 * 1.25).clip(0, 255)
    return Image.fromarray(np.concatenate([rgb, al], -1).clip(0, 255).astype("uint8")).convert("RGBA").filter(ImageFilter.GaussianBlur(2))
import glob
def fit_margins(n, top_min=205, side_min=154):
    """여백 보정(PM 10-14: 좌우 15%·위 20%): 위가 모자라면 그 숫자의 모든 프레임을 같은 양만큼 아래로 내린다."""
    fs = [f for f in glob.glob(f"{D}/symbol_{n}.png") + glob.glob(f"{D}/symbol_{n}_[fs]*.png")]
    boxes = [Image.open(f).convert("RGBA").getchannel("A").getbbox() for f in fs]
    top = min(b[1] for b in boxes); left = min(b[0] for b in boxes); right = min(1024 - b[2] for b in boxes)
    dy = max(0, top_min - top); dx = 0
    if left < side_min: dx = side_min - left
    elif right < side_min: dx = -(side_min - right)
    if dy or dx:
        for f in fs:
            im = Image.open(f).convert("RGBA"); o = Image.new("RGBA", im.size, (0, 0, 0, 0)); o.paste(im, (dx, dy)); o.save(f)
    return dy, dx
if os.environ.get("FIT", "1") == "1":
    for n in range(1, 10): print("margin", n, fit_margins(n))
ims = {}
for n in range(1, 10):
    im = Image.open(f"{D}/symbol_{n}.png").convert("RGBA"); ims[n] = im; motion_blur(im).save(f"{D}/symbol_{n}_blur.png")
F = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 26, index=0)
def row(seq, h=300, adv=170):
    r = Image.new("RGBA", (adv * len(seq) + h, h), (0, 0, 0, 0))
    for i, n in enumerate(seq):
        if n: r.alpha_composite(ims[n].resize((h, h)), (i * adv, 0))
    return r
sheet = Image.new("RGB", (1800, 1500), (8, 8, 18)); d = ImageDraw.Draw(sheet)
for bi, bg in enumerate(((8, 8, 18), (12, 40, 110))):
    y0 = bi * 750; d.rectangle([0, y0, 1800, y0 + 750], fill=bg)
    for i, n in enumerate(range(1, 10)):
        t = ims[n].resize((196, 196)); sheet.paste(t, (12 + i * 198, y0 + 6), t)
    for ri, (seq, lab) in enumerate((([3, None, 3], "3 ▢ 3"), ([7, 7, 7], "7 7 7"), ([6, 1, 3], "6 1 3"))):
        rw = row(seq); sheet.paste(rw, (20 + ri * 600, y0 + 290), rw); d.text((30 + ri * 600, y0 + 250), lab, font=F, fill=(255, 235, 170))
    d.text((20, y0 + 705), "symbols_v3 — 웹 시안 SVG 그대로 렌더(1024 투명) · 홀수 불꽃 f0~f5 · 7 금 · 짝수 크리스털 + 별빛 s0/s1 · _blur", font=F, fill=(200, 210, 225))
sheet.save(f"{D}/contact_sheet_v3.png"); print("done")
