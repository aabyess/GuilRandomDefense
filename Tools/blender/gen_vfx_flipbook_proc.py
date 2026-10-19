"""VFX Graph용 절차 플립북(자작·CC0 취급, blender 2026-10-19). 8×8·셀 512px·알파(스트레이트) PNG. 색은 흰~하늘색, 원하는 색은 VFX Graph에서 틴트.
  /usr/bin/python3 Tools/blender/gen_vfx_flipbook_proc.py [ring|arc ...] [--out 폴더] [--cell 512] [--grid 8]
순서: 왼쪽 위→오른쪽, 위→아래(VFX Graph Flipbook Player 기본). 충격 링=가운데서 퍼지며 옅어짐 · 전기 아크=가로로 가른 번개가 프레임마다 모양 바뀌며 깜박임."""
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
a = sys.argv[1:]
def opt(k, d): return type(d)(a[a.index(k) + 1]) if k in a else d
CELL, G = opt("--cell", 512), opt("--grid", 8); OUT = os.path.expanduser(opt("--out", "~/grd/GRD_vfx_graph/재료/플립북")); N = G * G
kinds = [x for x in a if x in ("ring", "arc")] or ["ring", "arc"]; os.makedirs(OUT, exist_ok=True)


def vnoise(shape_u, seed, n):
    """주기 1차원 값 노이즈(각도용): n 격자점, 코사인 보간."""
    r = np.random.RandomState(seed).rand(n); u = shape_u * n; i = np.floor(u).astype(int); f = u - i; f = (1 - np.cos(f * math.pi)) / 2
    return r[i % n] * (1 - f) + r[(i + 1) % n] * f


def ring_frame(t, seed):
    S = CELL; y, x = np.mgrid[0:S, 0:S].astype(np.float32); x = (x - S / 2 + 0.5) / (S / 2); y = (y - S / 2 + 0.5) / (S / 2)
    r = np.sqrt(x * x + y * y); th = (np.arctan2(y, x) / (2 * math.pi)) % 1.0
    R = 0.08 + 0.88 * (1 - (1 - t) ** 2.2)                       # 반지름: 빠르게 퍼지다 느려짐
    w = 0.16 * (1 - t) ** 0.8 + 0.015                              # 두께: 점점 얇아짐
    wob = (vnoise(th, seed, 24) - 0.5) * 0.06 * (0.3 + t) + (vnoise(th, seed + 1, 7) - 0.5) * 0.05
    d = (r - (R + wob)) / w
    core = np.exp(-(d ** 2) * 2.2)                                 # 고리 몸통
    glow = np.exp(-np.abs(d) * 0.9) * 0.35
    fade = np.clip(1 - t ** 1.6, 0, 1) * np.clip(t * 12, 0, 1)
    flash = np.exp(-(r / (0.35 * (1 - t) + 0.05)) ** 2) * (1 - t) ** 3 * 0.8        # 시작 번쩍임
    edge = np.clip((1 - r) * 12, 0, 1)                             # 셀 가장자리에서 0
    al = np.clip((core + glow + flash) * fade * edge, 0, 1)
    c = np.stack([0.75 + 0.25 * core, 0.88 + 0.12 * core, np.ones_like(core)], -1)
    return np.dstack([c, al])


def bolt(rs, p0, p1, depth, amp, pts):
    """중점 변위로 갈라진 번개 점열."""
    P = [np.array(p0, float), np.array(p1, float)]
    for dd in range(depth):
        Q = [P[0]]
        for i in range(len(P) - 1):
            m = (P[i] + P[i + 1]) / 2; d = P[i + 1] - P[i]; nrm = np.array([-d[1], d[0]]) / (np.linalg.norm(d) + 1e-6)
            Q += [m + nrm * rs.randn() * amp / (2 ** dd) * np.linalg.norm(d) * 0.5, P[i + 1]]
        P = Q
    return P


def arc_frame(i, seed):
    S = CELL; K = 2; big = S * K; rs = np.random.RandomState(seed + i * 7)
    life = math.sin(math.pi * min(1, (i + 0.5) / N)) ** 0.5; flick = 0.65 + 0.35 * rs.rand()
    core_im = Image.new("L", (big, big), 0); glow_im = Image.new("L", (big, big), 0); dc, dg = ImageDraw.Draw(core_im), ImageDraw.Draw(glow_im)
    ymid = big / 2 + rs.uniform(-0.05, 0.05) * big; main = bolt(rs, (0.04 * big, ymid), (0.96 * big, ymid + rs.uniform(-0.08, 0.08) * big), 6, 0.28, 0)
    for k in range(len(main) - 1):
        seg = [tuple(main[k]), tuple(main[k + 1])]; dc.line(seg, fill=255, width=int(4 * K)); dg.line(seg, fill=255, width=int(12 * K))
    for _ in range(rs.randint(2, 5)):                              # 가지
        j = rs.randint(len(main) // 5, len(main) - len(main) // 5); st = main[j]; ang = rs.uniform(-1.1, 1.1) + (0 if rs.rand() < 0.5 else math.pi)
        ln = rs.uniform(0.1, 0.3) * big; en = st + ln * np.array([math.cos(ang) * 0.7, math.sin(ang)])
        br = bolt(rs, st, en, 4, 0.6, 0)
        for k in range(len(br) - 1):
            seg = [tuple(br[k]), tuple(br[k + 1])]; dc.line(seg, fill=190, width=int(1.6 * K)); dg.line(seg, fill=140, width=int(5 * K))
    core = np.array(core_im.filter(ImageFilter.GaussianBlur(1.2 * K)).resize((S, S), Image.LANCZOS), np.float32) / 255
    glow = np.array(glow_im.filter(ImageFilter.GaussianBlur(7 * K)).resize((S, S), Image.LANCZOS), np.float32) / 255
    wide = np.array(glow_im.filter(ImageFilter.GaussianBlur(20 * K)).resize((S, S), Image.LANCZOS), np.float32) / 255
    I = np.clip(core * 1.3 + glow * 1.2 + wide * 1.1, 0, 1) * (0.35 + 0.65 * life) * flick
    xs = np.linspace(-1, 1, S)[None, :]; ys = np.linspace(-1, 1, S)[:, None]; edge = np.clip((1 - np.abs(xs)) * 14, 0, 1) * np.clip((1 - np.abs(ys)) * 14, 0, 1)
    I = I * edge; hot = np.clip(core * life * flick * 1.2, 0, 1)
    c = np.stack([0.45 + 0.55 * hot, 0.65 + 0.35 * hot, np.ones_like(hot)], -1)
    return np.dstack([c, I])


for kind in kinds:
    sheet = np.zeros((CELL * G, CELL * G, 4), np.float32)
    for i in range(N):
        fr = ring_frame(i / (N - 1), 11) if kind == "ring" else arc_frame(i, 5)
        r, c = divmod(i, G); sheet[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] = fr
    img = Image.fromarray((np.clip(sheet, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA"); p = f"{OUT}/flipbook_{ {'ring':'impact_ring','arc':'electric_arc'}[kind] }_{G}x{G}{'' if CELL == 512 else '_' + str(CELL * G)}.png"
    img.save(p); print("OK", p)
