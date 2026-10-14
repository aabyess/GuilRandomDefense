"""파칭코 이펙트 그림 재료(10-14, blender): 투명 PNG를 ~/GRD_pachinko_art/fx/에. 전부 코드로 직접 그림(무료·외부 에셋 없음).
  /usr/bin/python3 Tools/pachinko/fx_art.py [kagune brush streak speed clock butterfly spark]   (인자 없으면 전부)
시드 고정 — 다시 돌려도 같은 그림."""
import math, os, sys, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
OUT = os.path.expanduser("~/GRD_pachinko_art/fx"); os.makedirs(OUT, exist_ok=True)
SS = 2
def bez(p, t):
    a = [(1 - t) ** 3, 3 * (1 - t) ** 2 * t, 3 * (1 - t) * t * t, t ** 3]
    return (sum(a[i] * p[i][0] for i in range(4)), sum(a[i] * p[i][1] for i in range(4)))
def save(im, name): im.save(f"{OUT}/{name}.png"); print(name, im.size)
def lerp(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(len(a)))

# ① 카구네 촉수 -------------------------------------------------------------
def tentacle(layer, P, w0, seed, N=520, U=40, **_):
    """젖은 광택·겹친 비늘 판·날카로운 끝·안쪽 붉은 맥동 줄(10-14 2차)."""
    d = ImageDraw.Draw(layer)
    pts = [bez(P, i / N) for i in range(N + 1)]
    NSEG = 26                                              # 마디(판) 수
    for i in range(N):
        s = i / N; x0, y0 = pts[i]; x1, y1 = pts[i + 1]
        dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy) or 1; nx, ny = -dy / L, dx / L
        def W(ss): return w0 * (1 - ss) ** 1.3 * (1 + 0.05 * math.sin(ss * 31 + seed)) * (0.72 + 0.28 * min(1, ss * 9) ** 0.5 if ss < .12 else 1)
        wa, wb = W(s), W(min(1, s + 1 / N))
        for j in range(U):
            u0 = -1 + 2 * j / U; u1 = -1 + 2 * (j + 1) / U; um = (u0 + u1) / 2
            # 겹친 판: 마디 시작에서 밝고 끝으로 어두워지며, 가운데가 앞으로 나온 V자 경계
            q = (s * NSEG + 1.1 * um * um) % 1.0
            plate = 0.52 + 0.75 * (1 - q) ** 2.2            # 판 앞 모서리 밝게 → 겹침 그림자
            edge_line = 0.25 if q > 0.93 else 1.0           # 판 사이 가는 어두운 틈
            body = 0.40 + 0.75 * (1 - abs(um) ** 1.8)       # 단면 볼록
            tipfade = 1 - 0.80 * s
            r = 150 * plate * body * edge_line * tipfade; g = 8 * plate * body * tipfade; bl = 18 * plate * body * tipfade
            # 가장자리 어두운 보랏빛 윤곽
            rim = max(0, abs(um) - 0.82) / 0.18; r *= (1 - 0.65 * rim); g *= (1 - rim); bl *= (1 - 0.3 * rim)
            # 안쪽 붉은 맥동 줄(가운데, 마디마다 박동)
            pulse = 0.35 + 0.65 * (0.5 + 0.5 * math.sin(s * NSEG * 2 * math.pi - 0.8)) ** 2
            core = math.exp(-(um / 0.055) ** 2) * (0.25 + 0.75 * pulse) * 0.8 * (1 - 0.55 * s) * (1 if s < .93 else (1 - s) / .07)
            r = r + 255 * core * 0.95; g = g + 70 * core; bl = bl + 55 * core
            # 젖은 광택: 한쪽(-0.42)에 가늘고 날카로운 한 줄 + 판마다 끊김, 반대편엔 옅은 반사
            gl = math.exp(-((um + 0.42) / 0.045) ** 2) * (0.55 + 0.45 * math.sin(s * 11 + seed)) * (1 - s) ** 0.6 * (0.5 if q > .9 else 1.0)
            gl2 = math.exp(-((um - 0.55) / 0.04) ** 2) * 0.25 * (1 - s) ** 0.6
            h = max(0.0, gl + gl2)
            r = r + (255 - r) * min(1, h * 0.95); g = g + (210 - g) * min(1, h * 0.95); bl = bl + (205 - bl) * min(1, h * 0.95)
            c = (max(0, min(255, int(r))), max(0, min(255, int(g))), max(0, min(255, int(bl))), 255)
            qd = [(x0 + nx * wa * u0, y0 + ny * wa * u0), (x1 + nx * wb * u0, y1 + ny * wb * u0),
                  (x1 + nx * wb * u1, y1 + ny * wb * u1), (x0 + nx * wa * u1, y0 + ny * wa * u1)]
            d.polygon(qd, fill=c)
def kagune():
    W, H = 1024 * SS, 2048 * SS
    specs = [  # (제어점, 두께, 추가 가지)
        ([(-60, 1900), (260, 1500), (900, 1250), (620, 250)], 230, []),
        ([(1100, 2100), (700, 1500), (60, 1100), (330, -40)], 250, [([(1100, 1500), (800, 1180), (560, 1000), (700, 420)], 110)]),
        ([(-80, 700), (400, 820), (260, 1500), (820, 1950)], 200, [([(-80, 1300), (240, 1250), (460, 1700), (300, 2100)], 120)]),
        ([(540, 2150), (420, 1650), (800, 1100), (180, 160)], 280, [([(540, 2150), (860, 1800), (980, 1300), (1100, 900)], 130), ([(540, 2150), (150, 1850), (-20, 1500), (-80, 1200)], 110)]),
    ]
    for k, (P, w, br) in enumerate(specs):
        lay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for b, bw in br: tentacle(lay, [(x * SS, y * SS) for x, y in b], bw * SS, 100 + k)
        tentacle(lay, [(x * SS, y * SS) for x, y in P], w * SS, k)
        # 가장자리 살짝 번짐으로 매끈하게
        im = lay.resize((1024, 2048), Image.LANCZOS)
        save(im, f"kagune_{k+1}")

# ② 붓 획 -------------------------------------------------------------------
def brush(seed, col, edge=None, tilt=1):
    rnd = random.Random(seed); W, H = 2048 * SS, 512 * SS
    lay = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    y0, y1 = (400, 90) if tilt > 0 else (90, 400)
    P = [(90 * SS, y0 * SS), (700 * SS, (y0 + (y1 - y0) * .1 - 40) * SS), (1300 * SS, (y1 + 40) * SS), (1960 * SS, y1 * SS)]
    N = 900; maxw = 120 * SS
    for strand in range(70):                      # 마른 붓 털 가닥
        off = rnd.uniform(-1, 1); wthin = rnd.uniform(3, 11) * SS
        a0 = rnd.uniform(0.0, 0.12); a1 = rnd.uniform(0.82, 1.0)
        alpha = rnd.randint(150, 255)
        for i in range(int(a0 * N), int(a1 * N)):
            t = i / N; x, y = bez(P, t); xn, yn = bez(P, min(1, t + .002))
            dx, dy = xn - x, yn - y; L = math.hypot(dx, dy) or 1; nx, ny = -dy / L, dx / L
            prof = math.sin(math.pi * min(1, t * 1.05)) ** 0.6 * (1 - 0.35 * t)      # 시작 굵고 끝 가늘게
            r = wthin * (0.5 + prof) * (0.8 + 0.3 * math.sin(t * 50 + strand))
            o = off * maxw * prof * (1 + 0.04 * math.sin(t * 90 + strand))
            fade = 1.0 if t < a1 - .08 else (a1 - t) / .08
            d.ellipse((x + nx * o - r, y + ny * o - r, x + nx * o + r, y + ny * o + r), fill=col + (int(alpha * fade),))
    if edge:                                       # 한쪽 가장자리 붉은 번짐
        e = Image.new("RGBA", (W, H), (0, 0, 0, 0)); ed = ImageDraw.Draw(e)
        for i in range(0, N, 3):
            t = i / N; x, y = bez(P, t); prof = math.sin(math.pi * t) ** 0.6
            xn, yn = bez(P, min(1, t + .002)); dx, dy = xn - x, yn - y; L = math.hypot(dx, dy) or 1
            o = maxw * prof * 0.95; r = rnd.uniform(4, 10) * SS * (0.4 + prof)
            ed.ellipse((x - dy / L * o - r, y + dx / L * o - r, x - dy / L * o + r, y + dx / L * o + r), fill=edge + (190,))
        lay = Image.alpha_composite(e, lay)
        d = ImageDraw.Draw(lay)
    for _ in range(140):                           # 흩뿌림
        t = rnd.uniform(0.05, 1.0); x, y = bez(P, t); off = rnd.choice([-1, 1]) * rnd.uniform(0.9, 3.4) * maxw * 0.5
        px = x + rnd.uniform(-60, 60) * SS * (1 + t); py = y + off
        r = rnd.uniform(1.5, 9) * SS * (1.4 if rnd.random() < .1 else 1)
        if rnd.random() < .45:   # 길쭉한 방울
            l = r * rnd.uniform(2, 5); d.ellipse((px - l, py - r * .6, px + l, py + r * .6), fill=col + (rnd.randint(150, 240),))
        else: d.ellipse((px - r, py - r, px + r, py + r), fill=col + (rnd.randint(150, 240),))
    lay = lay.filter(ImageFilter.GaussianBlur(0.8 * SS * .5))
    return lay.resize((2048, 512), Image.LANCZOS)
def brushes():
    save(brush(11, (10, 8, 12), None, 1), "brush_ink")
    save(brush(23, (170, 6, 22), (255, 70, 60), -1), "brush_blood")
    save(brush(37, (14, 10, 16), (200, 18, 38), 1), "brush_ink_red")

# ③ 아나모픽 빛줄기 ---------------------------------------------------------
def streak(name, core, glow, W=2048, H=96, seed=0):
    x = np.linspace(-1, 1, W)[None, :]; y = np.linspace(-1, 1, H)[:, None]
    env = np.clip(1 - np.abs(x) ** 1.6, 0, 1)                   # 양끝 가늘게
    prof_core = np.exp(-(y / 0.045) ** 2) * env ** 0.7
    prof_glow = np.exp(-(y / 0.26) ** 2) * env ** 2.2 * 0.55
    prof_flare = np.exp(-(x / 0.015) ** 2) * np.exp(-(y / 0.9) ** 2) * 0.5
    a = np.clip(prof_core + prof_glow + prof_flare, 0, 1)
    wt = np.clip(prof_core * 1.4, 0, 1)[..., None]
    rgb = np.array(glow)[None, None, :] * (1 - wt) + np.array(core)[None, None, :] * wt
    im = np.dstack([rgb, a * 255]).astype(np.uint8)
    save(Image.fromarray(im, "RGBA"), name)
def streaks():
    streak("streak_white", (255, 255, 255), (200, 220, 255))
    streak("streak_teal", (220, 255, 250), (20, 220, 200))

# ④ 집중선 -----------------------------------------------------------------
def speed():
    W, H = 1920 * SS, 1080 * SS; im = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im); rnd = random.Random(7)
    cx, cy = W / 2, H / 2; R = math.hypot(cx, cy) * 1.05; r_in_base = 0.38 * H
    for _ in range(420):
        a = rnd.uniform(0, 2 * math.pi); r_in = r_in_base * rnd.uniform(0.95, 1.9) * (1 + 0.25 * abs(math.cos(a)))
        wd = rnd.choice([0.0015, 0.0022, 0.0035, 0.006]) * rnd.uniform(0.6, 1.4)
        cs, sn = math.cos(a), math.sin(a)
        p0 = (cx + cs * r_in, cy + sn * r_in)                                    # 안쪽 뾰족 끝
        p1 = (cx + cs * R - sn * wd * R, cy + sn * R + cs * wd * R); p2 = (cx + cs * R + sn * wd * R, cy + sn * R - cs * wd * R)
        d.polygon([p0, p1, p2], fill=(255, 255, 255, rnd.randint(170, 255)))
    # 바깥쪽이 진하고 안쪽으로 옅어지는 감쇠
    arr = np.array(im.resize((1920, 1080), Image.LANCZOS)).astype(np.float32)
    yy, xx = np.mgrid[0:1080, 0:1920]; rr = np.hypot(xx - 960, yy - 540) / np.hypot(960, 540)
    arr[..., 3] *= np.clip((rr - 0.28) / 0.5, 0, 1) ** 0.8
    save(Image.fromarray(arr.astype(np.uint8), "RGBA"), "speedlines")

# ⑤ 시계·톱니 문양 / 나비 ---------------------------------------------------
def clock():
    S = 1024 * SS; im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im); c = S / 2
    silver = (215, 220, 235, 255); purple = (150, 90, 230, 255); dim = (170, 175, 200, 200)
    def circ(r, col, w): d.ellipse((c - r, c - r, c + r, c + r), outline=col, width=int(w * SS))
    circ(480 * SS, silver, 5); circ(468 * SS, purple, 2); circ(380 * SS, silver, 3); circ(290 * SS, purple, 3); circ(120 * SS, silver, 4); circ(24 * SS, silver, 6)
    for i in range(60):                            # 분 눈금
        a = math.radians(i * 6 - 90); big = i % 5 == 0
        r0, r1 = (400 if big else 430) * SS, 460 * SS
        d.line((c + math.cos(a) * r0, c + math.sin(a) * r0, c + math.cos(a) * r1, c + math.sin(a) * r1), fill=silver if big else dim, width=int((6 if big else 2) * SS))
    for i in range(12):                            # 시 마디 다이아
        a = math.radians(i * 30 - 90); x, y = c + math.cos(a) * 340 * SS, c + math.sin(a) * 340 * SS; r = 14 * SS
        d.polygon([(x, y - r * 1.5), (x + r, y), (x, y + r * 1.5), (x - r, y)], outline=purple, width=int(3 * SS))
    n = 16                                          # 톱니바퀴 링(안쪽)
    for i in range(n):
        a0 = 2 * math.pi * i / n; a1 = a0 + 2 * math.pi / n * .5
        pts = [(c + math.cos(a) * r, c + math.sin(a) * r) for a, r in ((a0, 290), (a0 + .03, 322), (a1 - .03, 322), (a1, 290))]
        d.line([(x, y) for x, y in [(p[0], p[1]) for p in pts]], fill=purple, width=int(3 * SS))
    for a, r0, r1, w, col in ((math.radians(-60), 0, 270, 8, silver), (math.radians(30), 0, 190, 12, purple)):   # 시침·분침
        d.line((c, c, c + math.cos(a) * r1 * SS, c + math.sin(a) * r1 * SS), fill=col, width=int(w * SS))
    for k in range(3):                              # 호 장식
        r = (200 + k * 40) * SS; a0 = 40 + k * 100
        d.arc((c - r, c - r, c + r, c + r), a0, a0 + 70, fill=silver, width=int(2 * SS))
    save(im.resize((1024, 1024), Image.LANCZOS), "clock_emblem")
def _spline(pts, n=14):
    out = []; m = len(pts)
    for i in range(m):
        p0, p1, p2, p3 = pts[(i - 1) % m], pts[i], pts[(i + 1) % m], pts[(i + 2) % m]
        for k in range(n):
            t = k / n; t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[c]) + (-p0[c] + p2[c]) * t + (2 * p0[c] - 5 * p1[c] + 4 * p2[c] - p3[c]) * t2 + (-p0[c] + 3 * p1[c] - 3 * p2[c] + p3[c]) * t3) for c in (0, 1)))
    return out
FORE = [(4, -6), (16, -42), (50, -90), (98, -118), (126, -104), (118, -68), (102, -36), (70, -12), (28, -2)]
HIND = [(6, 2), (40, 0), (82, 8), (106, 28), (112, 56), (104, 96), (94, 112), (84, 78), (68, 62), (54, 74), (38, 60), (22, 46), (10, 24)]
def butterfly(f, S=256):
    k = [1.0, 0.64, 0.24, 0.64][f]; Q = 4; Z = S * Q; c = Z / 2; sc = S / 256 * Q
    def wing_mask(poly, sign):
        pts = [(c + sign * x * k * sc, c + (y + 6) * sc * (0.94 + 0.06 * k)) for x, y in _spline(poly)]
        m = Image.new("L", (Z, Z), 0); ImageDraw.Draw(m).polygon(pts, fill=255); return m, pts
    res = Image.new("RGBA", (Z, Z), (0, 0, 0, 0))
    for sign in (-1, 1):
        for poly, vein_n, tone in ((HIND, 6, 0.85), (FORE, 7, 1.0)):
            m, pts = wing_mask(poly, sign)
            depth = np.asarray(m.filter(ImageFilter.GaussianBlur(18 * Q / 4 * 1.6)), dtype=np.float32) / 255.0   # 안쪽일수록 큼
            inside = np.asarray(m, dtype=np.float32) / 255.0
            edge = np.clip(1.0 - (depth - 0.5) / 0.38, 0, 1) * inside      # 가장자리 1 → 안쪽 0
            rgb = np.zeros((Z, Z, 3), np.float32)
            rgb[..., 0] = 170 * (1 - edge) + 78 * edge; rgb[..., 1] = 130 * (1 - edge) + 22 * edge; rgb[..., 2] = 255 * (1 - edge) + 175 * edge
            a = (0.10 + 0.78 * edge ** 1.2) * inside * 255 * tone
            lay = Image.fromarray(np.dstack([rgb, a]).astype(np.uint8), "RGBA")
            d = ImageDraw.Draw(lay)
            ox, oy = c + sign * 6 * k * sc, c + 4 * sc                      # 날개맥: 날개 뿌리에서 가장자리로 방사
            step = max(1, len(pts) // (vein_n * 2))
            for q in range(2, len(pts) - 2, step):
                tx, ty = pts[q]
                d.line([(ox, oy), (ox + (tx - ox) * 0.55 + 0, oy + (ty - oy) * 0.55), (tx * 0.985 + ox * .015, ty * 0.985 + oy * .015)], fill=(225, 205, 255, 150), width=max(1, int(1.1 * Q / 2)))
            mk = Image.new("L", (Z, Z), 0); mk.paste(m)
            lay.putalpha(Image.fromarray((np.asarray(lay)[..., 3].astype(np.float32) * np.asarray(mk, np.float32) / 255).astype(np.uint8)))
            d = ImageDraw.Draw(lay)
            d.line(pts + [pts[0]], fill=(238, 238, 252, 235), width=max(1, int(1.6 * Q / 2)))   # 은빛 가장자리
            res = Image.alpha_composite(res, lay)
    glow = res.filter(ImageFilter.GaussianBlur(11 * Q / 4)).copy()
    ga = np.asarray(glow).copy(); ga[..., 0], ga[..., 1], ga[..., 2] = 150, 80, 255; ga[..., 3] = (ga[..., 3] * 0.65).astype(np.uint8)
    out = Image.alpha_composite(Image.fromarray(ga, "RGBA"), res)
    d = ImageDraw.Draw(out)
    d.ellipse((c - 5 * sc, c - 38 * sc, c + 5 * sc, c + 60 * sc), fill=(30, 14, 56, 255))
    for sx in (-1, 1): d.line([(c, c - 36 * sc), (c + sx * 14 * sc, c - 70 * sc), (c + sx * 26 * sc, c - 84 * sc)], fill=(60, 36, 100, 255), width=int(1.5 * Q))
    return out.resize((S, S), Image.LANCZOS)
def butterflies():
    fr = [butterfly(i) for i in range(4)]
    for i, im in enumerate(fr): save(im, f"butterfly_{i}")
    st = Image.new("RGBA", (1024, 256)); [st.paste(im, (i * 256, 0)) for i, im in enumerate(fr)]; save(st, "butterfly_strip")

# ⑥ 불똥/파편 --------------------------------------------------------------
def spark(kind, S, col, seed):
    rnd = random.Random(seed); im = Image.new("RGBA", (S * 4, S * 4), (0, 0, 0, 0)); d = ImageDraw.Draw(im); c = S * 2; R = S * 2 * .9
    w = col + (255,); hot = (255, 255, 255, 255)
    if kind == "star4":
        d.polygon([(c, c - R), (c + R * .11, c - R * .11), (c + R, c), (c + R * .11, c + R * .11), (c, c + R), (c - R * .11, c + R * .11), (c - R, c), (c - R * .11, c - R * .11)], fill=w)
        r = R * .22; d.ellipse((c - r, c - r, c + r, c + r), fill=hot)
    elif kind == "shard":
        d.polygon([(c - R * .15, c - R), (c + R * .35, c - R * .1), (c + R * .1, c + R), (c - R * .4, c + R * .15)], fill=w)
        d.polygon([(c - R * .15, c - R), (c + R * .1, c - R * .2), (c - R * .1, c + R * .1)], fill=hot)
    elif kind == "streak":
        d.polygon([(c - R, c + R * .6), (c + R * .1, c - R * .08), (c + R * .25, c + R * .02), (c - R, c + R * .6)], fill=w)
        d.polygon([(c - R * .4, c + R * .26), (c + R * .22, c - R * .06), (c + R * .06, c + R * .08)], fill=hot)
    elif kind == "tri":
        a = rnd.uniform(0, 6.28); pts = [(c + math.cos(a + i * 2.1) * R * (0.95 if i == 0 else 0.45), c + math.sin(a + i * 2.1) * R * (0.95 if i == 0 else 0.45)) for i in range(3)]
        d.polygon(pts, fill=w)
    elif kind == "needle":
        d.polygon([(c - R, c), (c, c - R * .09), (c + R, c), (c, c + R * .09)], fill=w)
        d.polygon([(c - R * .3, c), (c, c - R * .05), (c + R * .3, c), (c, c + R * .05)], fill=hot)
    elif kind == "cross":
        for a in (0.4, 1.97):
            ca, sa = math.cos(a), math.sin(a)
            d.polygon([(c - ca * R, c - sa * R), (c - sa * R * .1, c + ca * R * .1), (c + ca * R, c + sa * R), (c + sa * R * .1, c - ca * R * .1)], fill=w)
        r = R * .16; d.ellipse((c - r, c - r, c + r, c + r), fill=hot)
    im = im.filter(ImageFilter.GaussianBlur(S * .04))
    return im.resize((S, S), Image.LANCZOS)
def sparks():
    spec = [("star4", 128, (255, 190, 80)), ("shard", 96, (255, 60, 50)), ("streak", 128, (255, 150, 60)), ("tri", 64, (255, 230, 150)),
            ("needle", 128, (120, 240, 230)), ("cross", 96, (200, 150, 255)), ("shard", 64, (255, 255, 255)), ("star4", 64, (255, 70, 90))]
    ims = []
    for i, (k, S, col) in enumerate(spec): im = spark(k, S, col, i); ims.append(im); save(im, f"spark_{i+1}")
    return ims

def sheet():
    names = sorted(f[:-4] for f in os.listdir(OUT) if f.endswith(".png") and not f.startswith("contact"))
    W = 1800; bg = Image.new("RGB", (W, 2400), (40, 42, 52)); d = ImageDraw.Draw(bg); x = y = 10; rowh = 0
    fnt = ImageFont.truetype("/System/Library/Fonts/Menlo.ttc", 14)
    for n in names:
        im = Image.open(f"{OUT}/{n}.png"); s = min(1, 520 / im.height, 880 / im.width) if im.width > 300 or im.height > 300 else 1
        im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))))
        if x + im.width > W - 10: x = 10; y += rowh + 26; rowh = 0
        d.text((x, y), f"{n} {Image.open(f'{OUT}/{n}.png').size}", font=fnt, fill=(255, 235, 170)); bg.paste(im, (x, y + 20), im)
        x += im.width + 14; rowh = max(rowh, im.height + 0)
    bg.crop((0, 0, W, y + rowh + 40)).save(f"{OUT}/contact_sheet.png")
if __name__ == "__main__":
    want = sys.argv[1:] or ["kagune", "brush", "streak", "speed", "clock", "butterfly", "spark"]
    for k, fn in (("kagune", kagune), ("brush", brushes), ("streak", streaks), ("speed", speed), ("clock", clock), ("butterfly", butterflies), ("spark", sparks)):
        if k in want: fn()
    sheet()
