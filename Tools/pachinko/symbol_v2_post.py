"""숫자 v2 후처리: 별빛 하이라이트 점 + 세로 모션블러 + contact_sheet_v2. /usr/bin/python3 Tools/pachinko/symbol_v2_post.py [폴더=~/GRD_pachinko_art/symbols_v2]
원본 렌더는 <폴더>/_raw/symbol_N.png (blender -b … symbol_v2.py 결과를 복사해 둔 것)."""
import os, random, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else "~/GRD_pachinko_art/symbols_v2"); RAW = D + "/_raw"
def sparkle(im, seed, n=4):
    a = np.asarray(im).astype("float32") / 255; lum = a[..., :3].mean(-1); m = (lum > 0.93) & (a[..., 3] > 0.9)
    ys, xs = np.nonzero(m)
    if len(xs) == 0: return im
    rng = random.Random(seed); idx = list(range(len(xs))); rng.shuffle(idx); pts = []
    for i in idx:
        p = (int(xs[i]), int(ys[i]))
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > 150 ** 2 for q in pts): pts.append(p)
        if len(pts) >= n: break
    S = 2; W, H = im.size; lay = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    for (x, y) in pts:
        r = rng.choice((30, 40, 54)) * S; w = 3 * S; X, Y = x * S, y * S
        d.polygon([(X, Y - r), (X + w, Y - w), (X + r, Y), (X + w, Y + w), (X, Y + r), (X - w, Y + w), (X - r, Y), (X - w, Y - w)], fill=(255, 255, 255, 255))
        d.ellipse([X - 6 * S, Y - 6 * S, X + 6 * S, Y + 6 * S], fill=(255, 255, 255, 255))
    lay = lay.resize((W, H), Image.LANCZOS); glow = lay.filter(ImageFilter.GaussianBlur(8))
    out = im.copy(); out.alpha_composite(glow); out.alpha_composite(lay); return out
def motion_blur(im, px=70, n=41):
    arr = np.asarray(im).astype("float32"); H = arr.shape[0]; acc = np.zeros(arr.shape[:2] + (3,), "float32"); wsum = np.zeros(arr.shape[:2] + (1,), "float32"); aal = np.zeros_like(wsum)
    for k in range(n):
        dy = int(-px + 2 * px * k / (n - 1)); sh = np.roll(arr, dy, axis=0); a = sh[..., 3:4] / 255
        acc += sh[..., :3] * a; wsum += a; aal += a
    rgb = acc / np.maximum(wsum, 1e-3); al = (aal / n * 255 * 1.2).clip(0, 255)
    return Image.fromarray(np.concatenate([rgb, al], -1).clip(0, 255).astype("uint8")).convert("RGBA").filter(ImageFilter.GaussianBlur(2))
ims = {}
for n in range(1, 10):
    raw = Image.open(f"{RAW}/symbol_{n}.png").convert("RGBA"); fin = sparkle(raw, n * 17)
    fin.save(f"{D}/symbol_{n}.png"); motion_blur(fin).save(f"{D}/symbol_{n}_blur.png"); ims[n] = fin
F = ImageFont.truetype("/System/Library/Fonts/AppleSDGothicNeo.ttc", 26, index=0)
def row(seq, h=230, adv=135):
    r = Image.new("RGBA", (adv * len(seq) + h, h), (0, 0, 0, 0))
    for i, n in enumerate(seq):
        if n is None:
            continue
        r.alpha_composite(ims[n].resize((h, h)), (i * adv, 0))
    return r
sheet = Image.new("RGB", (1800, 1500), (8, 8, 18)); d = ImageDraw.Draw(sheet)
for bi, bg in enumerate(((8, 8, 18), (12, 40, 110))):
    y0 = bi * 750; d.rectangle([0, y0, 1800, y0 + 750], fill=bg)
    for i, n in enumerate(range(1, 10)):
        t = ims[n].resize((190, 190)); sheet.paste(t, (20 + i * 195, y0 + 10), t)
    for r_i, (seq, label) in enumerate((([3, None, 3], "3 ▢ 3"), ([7, 7, 7], "7 7 7"), ([6, 1, 3], "6 1 3"))):
        rw = row(seq); sheet.paste(rw, (30 + r_i * 600, y0 + 300), rw)
        d.text((40 + r_i * 600, y0 + 255), label, font=F, fill=(255, 235, 170))
    d.text((20, y0 + 700), "symbol_1~9 (1024 투명) · 겹친 붓 스우시+획 결 · 홀수 불꽃(7 금) / 짝수 크롬(9 보라)", font=F, fill=(200, 210, 225))
sheet.save(f"{D}/contact_sheet_v2.png"); print("done")
