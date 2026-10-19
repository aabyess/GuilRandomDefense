"""VFX Graph용 이음새 없는 노이즈 3장 + 화염 줄기 가로 그라디언트 1장(자작·CC0 취급, blender 2026-10-19).
  /usr/bin/python3 Tools/blender/gen_vfx_tiles.py [--out 폴더] [--size 1024]
산출: noise_fbm.png(구름결) · noise_cell.png(세포/Worley) · noise_swirl.png(소용돌이 왜곡) — 회색조 1024, 타일링 · gradient_flame_h.png(1024×64 RGBA, 왼쪽 뜨거움→오른쪽 투명)"""
import sys, os
import numpy as np
from PIL import Image
a = sys.argv[1:]
def opt(k, d): return type(d)(a[a.index(k) + 1]) if k in a else d
S = opt("--size", 1024); OUT = os.path.expanduser(opt("--out", "~/grd/GRD_vfx_graph/재료/타일")); os.makedirs(OUT, exist_ok=True)


def fft_noise(seed, beta, n=S):
    f = np.fft.fft2(np.random.RandomState(seed).randn(n, n)); k = np.fft.fftfreq(n); kk = np.sqrt(k[:, None] ** 2 + k[None, :] ** 2); kk[0, 0] = 1
    return np.real(np.fft.ifft2(f / kk ** beta))


def norm(v): v = v - v.min(); return v / (v.max() + 1e-9)


def save(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "L").save(f"{OUT}/{name}"); print("OK", name)


def worley(seed, cells=12, n=S):
    rs = np.random.RandomState(seed); pts = (rs.rand(cells, cells, 2) + np.indices((cells, cells)).transpose(1, 2, 0)) / cells
    y, x = np.mgrid[0:n, 0:n] / n; d = np.full((n, n), 9.0)
    # 단순: 격자 점을 3×3 복제해 최소 거리
    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            P = pts.reshape(-1, 2) + np.array([ox, oy])
            for (px, py) in P:
                d = np.minimum(d, (x - px) ** 2 + (y - py) ** 2)
    return np.sqrt(d)


save(norm(fft_noise(1, 1.6)), "noise_fbm.png")
w = worley(2); save(1 - norm(w), "noise_cell.png")
# 소용돌이: 두 노이즈로 좌표를 비틀어 샘플(주기 유지)
b = fft_noise(3, 1.8); c = fft_noise(4, 1.8); base = norm(fft_noise(5, 1.5)); n = S
yy, xx = np.mgrid[0:n, 0:n]; wy = (yy + (norm(b) - 0.5) * n * 0.5).astype(int) % n; wx = (xx + (norm(c) - 0.5) * n * 0.5).astype(int) % n
sw = base[wy, wx]; sw = np.clip((sw - sw.mean()) * 2.2 + 0.5, 0, 1); save(sw, "noise_swirl.png")
# 가로 그라디언트
x = np.linspace(0, 1, S)[None, :].repeat(64, 0); xs = [0, 0.12, 0.45, 0.8, 1.0]
cols = [(1.0, 0.97, 0.8), (1.0, 0.8, 0.2), (1.0, 0.35, 0.03), (0.5, 0.05, 0.0), (0.2, 0.0, 0.0)]; al = [1.0, 1.0, 0.85, 0.35, 0.0]
g = np.dstack([np.interp(x, xs, [c[i] for c in cols]) for i in range(3)] + [np.interp(x, xs, al)])
Image.fromarray((g * 255 + 0.5).astype(np.uint8)).save(f"{OUT}/gradient_flame_h.png"); print("OK gradient_flame_h.png")
