"""VFX Graph용 절차 플립북 3종 — 불꽃·연기·청백 마법불꽃(자작·CC0 취급, blender 2026-10-19). 8×8·셀 512px·RGBA 스트레이트 알파.
  /usr/bin/python3 Tools/blender/gen_vfx_flipbook_flame.py [fire|smoke|magic ...] [--out 폴더] [--cell 512] [--grid 8]
3차원 주기 노이즈(FFT 1/f 필터)를 위로 흘려 밀도를 만들고 색 사다리로 칠한다. 세로로 일어나는 모양(바닥 가운데→위). 순서: 왼쪽 위→오른쪽, 위→아래."""
import sys, os, math
import numpy as np
from PIL import Image
a = sys.argv[1:]
def opt(k, d): return type(d)(a[a.index(k) + 1]) if k in a else d
CELL, G = opt("--cell", 512), opt("--grid", 8); OUT = os.path.expanduser(opt("--out", "~/grd/GRD_vfx_graph/재료/플립북")); N = G * G
kinds = [x for x in a if x in ("fire", "smoke", "magic")] or ["fire", "smoke", "magic"]; os.makedirs(OUT, exist_ok=True)
W = 256   # 노이즈 해상도(셀 크기로 키움)


def noise3(seed, beta, shape):
    rs = np.random.RandomState(seed); f = np.fft.fftn(rs.randn(*shape))
    k = [np.fft.fftfreq(n) for n in shape]; kk = np.sqrt(sum(g ** 2 for g in np.meshgrid(*k, indexing="ij"))); kk[0, 0, 0] = 1
    n = np.real(np.fft.ifftn(f / kk ** beta)); n = (n - n.mean()) / (n.std() + 1e-9); return n


def ramp(v, stops):
    out = np.zeros(v.shape + (3,), np.float32); xs = [s[0] for s in stops]
    for ch in range(3): out[..., ch] = np.interp(v, xs, [s[1][ch] for s in stops])
    return out


def build(kind):
    n1 = noise3(3, 1.9, (W, W, N)); n2 = noise3(9, 1.5, (W, W, N))
    y, x = np.mgrid[0:W, 0:W].astype(np.float32); X = (x - W / 2 + 0.5) / (W / 2); Y = 1 - (y + 0.5) / W        # Y=0 바닥, 1 위
    frames = []
    for i in range(N):
        t = i / (N - 1); nz = n1[..., i] * 0.65 + n2[..., (i + N // 3) % N] * 0.35
        # 위로 흐르는 느낌: 프레임마다 노이즈 평면을 아래로 밀어 비교(시간 이동)
        nz = np.roll(nz, int(t * W * 1.5), axis=0)
        if kind == "smoke":
            grow = 0.25 + 0.55 * t; wid = (0.16 + 0.5 * Y) * (0.6 + t); xs = X + nz * 0.12 * (0.3 + Y)
            body = np.exp(-(xs / np.clip(wid, 1e-3, None)) ** 2) * np.clip(1 - np.abs(Y - grow) / (0.4 + 0.2 * t), 0, 1)
            d = np.clip(body * (0.8 + 0.2 * nz), 0, 1)
            al = np.clip(d * np.clip(1 - t ** 3, 0, 1) * np.clip(t * 10, 0, 1) * 1.6, 0, 1)
            c = np.dstack([0.28 + 0.4 * d, 0.28 + 0.4 * d, 0.3 + 0.4 * d]) * 0 + ramp(d, [(0, (0.22, 0.22, 0.24)), (0.5, (0.45, 0.45, 0.47)), (1, (0.8, 0.8, 0.82))])
        else:
            life = np.clip(math.sin(math.pi * min(1, t * 0.9 + 0.05)) ** 0.7, 0, 1)
            h = 0.95 * (0.6 + 0.4 * life)                                              # 불꽃 높이
            up = np.clip(1 - Y / h, 0, 1); wid = np.clip(0.30 * up ** 0.6 * (0.8 + 0.2 * life) + 0.02, 1e-3, None)
            xs = X + nz * 0.22 * (0.15 + Y) + 0.07 * Y * np.sin(t * 25 + 3 * Y)                                           # 위로 갈수록 일렁임
            shape = np.exp(-(xs / wid) ** 2) * up ** 0.5
            turb = np.clip(0.5 + 0.38 * nz, 0, 1); d = np.clip(shape * (0.35 + 0.9 * turb) - 0.05 * Y, 0, 1)
            heat = np.clip(d * 1.25 - 0.1, 0, 1)
            if kind == "fire":
                c = ramp(heat, [(0, (0.5, 0.05, 0.0)), (0.35, (1.0, 0.35, 0.02)), (0.7, (1.0, 0.8, 0.2)), (1, (1.0, 0.97, 0.8))])
            else:   # magic: 청백
                c = ramp(heat, [(0, (0.05, 0.2, 0.7)), (0.35, (0.2, 0.55, 1.0)), (0.7, (0.65, 0.9, 1.0)), (1, (1.0, 1.0, 1.0))])
            al = np.clip(d * 1.5, 0, 1) * np.clip(t * 14, 0, 1) * np.clip((1 - t) * 8, 0, 1)
        edge = np.clip((1 - np.abs(X)) * 12, 0, 1) * np.clip((1 - Y) * 12, 0, 1)
        al = al * edge
        fr = np.nan_to_num(np.dstack([c, al]).astype(np.float32))
        frames.append(np.asarray(Image.fromarray((np.clip(fr, 0, 1) * 255 + 0.5).astype(np.uint8)).resize((CELL, CELL), Image.BICUBIC), np.uint8))
    return frames


for kind in kinds:
    fs = build(kind); sheet = np.zeros((CELL * G, CELL * G, 4), np.uint8)
    for i, fr in enumerate(fs): r, c = divmod(i, G); sheet[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] = fr
    p = f"{OUT}/flipbook_{ {'fire':'fire','smoke':'smoke','magic':'magic_flame_blue'}[kind] }_{G}x{G}{'' if CELL == 512 else '_' + str(CELL * G)}.png"; Image.fromarray(sheet).save(p); print("OK", p)
