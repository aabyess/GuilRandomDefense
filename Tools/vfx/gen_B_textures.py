# 안 B 텍스처 세트 — PIL/numpy 절차 생성. RGB는 흰색(코드에서 틴트), 모양은 알파. /usr/bin/python3
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
OUT = os.path.expanduser('~/grd/GRD_vfx_graph/재료_B')
os.makedirs(OUT, exist_ok=True)

def save(name, alpha, rgb=(255, 255, 255)):
    a = (np.clip(alpha, 0, 1) * 255 + .5).astype('uint8')
    im = np.zeros(a.shape + (4,), 'uint8'); im[..., 0], im[..., 1], im[..., 2] = rgb; im[..., 3] = a
    Image.fromarray(im, 'RGBA').save(f'{OUT}/{name}.png'); print(name, a.shape[::-1])

def grid(w, h):
    yy, xx = np.mgrid[0:h, 0:w]
    return (xx + .5) / w * 2 - 1, (yy + .5) / h * 2 - 1   # -1..1

def ray(x, y, ang, L, w0, p=1.5):
    """한 방향 광선: 끝이 뾰족(폭이 (1-u/L)^p로 줄고 세기도 줄어든다)."""
    c, s = math.cos(ang), math.sin(ang)
    u = x * c + y * s; v = -x * s + y * c
    t = np.clip(1 - np.abs(u) / L, 0, 1)
    wd = w0 * t ** p + 1e-4
    return np.exp(-(v / wd) ** 2) * t ** 0.7 * (np.abs(u) < L)

def sparkle():
    x, y = grid(1024, 1024); r = np.hypot(x, y)
    a = np.maximum(ray(x, y, 0, .98, .022), ray(x, y, math.pi / 2, .98, .022))
    a = np.maximum(a, .55 * np.maximum(ray(x, y, math.pi / 4, .42, .016), ray(x, y, -math.pi / 4, .42, .016)))
    a = np.maximum(a, np.exp(-(r / .075) ** 2)) + .38 * np.exp(-(r / .2) ** 2) + .12 * np.exp(-(r / .45) ** 2)
    save('sparkle_4pt', a)

def burst8():
    x, y = grid(1024, 1024); r = np.hypot(x, y); a = np.zeros_like(r)
    for i in range(8):
        a = np.maximum(a, ray(x, y, math.pi / 4 * i - math.pi / 2, .98 if i % 2 == 0 else .62, .055 if i % 2 == 0 else .045, 1.3))
    a = np.maximum(a, np.exp(-(r / .17) ** 2) * 1.0) + .45 * np.exp(-(r / .33) ** 2) + .12 * np.exp(-(r / .6) ** 2)
    save('burst_8pt', a)

def shock_ring():
    x, y = grid(1024, 1024); r = np.hypot(x, y); R = .62
    line = np.exp(-((r - R) / .014) ** 2)
    d = r - R; soft = np.where(d > 0, np.exp(-(d / .12) ** 2), np.exp(-(d / .05) ** 2)) * .38
    save('ring_shock', (line + soft) * (r < .98) * np.clip((.98 - r) / .08, 0, 1))

def dot():
    x, y = grid(512, 512); r = np.hypot(x, y)
    save('dot_soft', .75 * np.exp(-(r / .16) ** 2) + .45 * np.exp(-(r / .4) ** 2))

def magic_ring():
    x, y = grid(1024, 1024); r = np.hypot(x, y); th = np.arctan2(y, x)
    outer = np.exp(-((r - .9) / .012) ** 2); inner = .8 * np.exp(-((r - .78) / .007) ** 2)
    glow = .32 * np.exp(-((r - .9) / .05) ** 2) + .14 * np.exp(-((r - .78) / .05) ** 2)
    k = np.cos(th * 24) > .55   # 눈금 24개
    tick = ((r > .82) & (r < .875) & k) * .75
    k2 = np.cos(th * 8 + .39) > .93
    dia = ((r > .66) & (r < .745) & k2) * .6
    fill = .07 * np.clip((.78 - r) / .78, 0, 1)
    a = (outer + inner + tick + dia + glow + fill) * np.clip((.99 - r) / .05, 0, 1)
    save('ring_magic', a)

def lightning(name, seed):
    W, H, S = 256, 1024, 2; rnd = random.Random(seed)
    m = Image.new('L', (W * S, H * S), 0); d = ImageDraw.Draw(m)
    def bolt(x0, y0, y1, amp, wd):
        pts = [(x0, y0)]; y = y0; x = x0
        while y < y1:
            y += rnd.uniform(34, 74); x += rnd.uniform(-amp, amp); x = min(max(x, 40), W - 40); pts.append((x, min(y, y1)))
        d.line([(a * S, b * S) for a, b in pts], fill=255, width=int(wd * S), joint='curve')
        return pts
    main = bolt(W / 2, 0, H, 46, 9)
    for _ in range(3):   # 가지
        i = rnd.randint(2, len(main) - 4); bx, by = main[i]
        pts = [(bx, by)]
        for k in range(rnd.randint(2, 4)):
            bx += rnd.uniform(-60, 60) + (25 if rnd.random() < .5 else -25); by += rnd.uniform(30, 60); pts.append((min(max(bx, 20), W - 20), by))
        d.line([(a * S, b * S) for a, b in pts], fill=255, width=int(4 * S), joint='curve')
    core = np.asarray(m.resize((W, H), Image.LANCZOS), float) / 255
    glow = np.asarray(m.filter(ImageFilter.GaussianBlur(14 * S)).resize((W, H), Image.LANCZOS), float) / 255
    glow2 = np.asarray(m.filter(ImageFilter.GaussianBlur(4 * S)).resize((W, H), Image.LANCZOS), float) / 255
    a = np.clip(core + .9 * glow2 + 1.8 * glow, 0, 1)
    yy = np.linspace(0, 1, H)[:, None]; a *= np.clip(yy * 18, 0, 1) * np.clip((1 - yy) * 18, 0, 1) ** .3   # 위 끝 부드럽게
    save(name, a)

def trail():
    W, H = 1024, 128; yy, xx = np.mgrid[0:H, 0:W]; u = (xx + .5) / W; v = (yy + .5) / H * 2 - 1
    wd = .08 + .55 * u ** 1.1
    a = np.exp(-(v / wd) ** 2) * u ** 1.6 * np.clip((1 - u) * 20 + .0, 0, 1) ** 0 
    a = a + .0; a = np.where(u > .97, a * (1 - (u - .97) / .03), a)
    save('trail_h', a)

def plate():
    x, y = grid(512, 512); r = np.hypot(x, y)
    a = 1 - np.clip((r - .25) / .7, 0, 1); a = a * a * (3 - 2 * a)
    save('plate_dark', a * .85, rgb=(0, 0, 0))

sparkle(); burst8(); shock_ring(); dot(); magic_ring(); trail(); plate()
for n, s in (('lightning_a', 11), ('lightning_b', 27), ('lightning_c', 53)): lightning(n, s)
