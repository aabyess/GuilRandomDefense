"""그랜드캐니언풍 흙 섬 텍스처 두 장(2026-10-06, 레인 사이 「십자」 대지). 전부 절차 생성(외부 에셋 없음).
   canyon_strata.png — 절벽 지층. 가로(u)·세로(v) 모두 이음새 없이 반복, v 1장에 띠 7개.
   canyon_top.png    — 윗면 마른 적갈색 흙. 사방 이음새 없음, 풀 없음.
   미리보기: preview_strata_4x4.png / preview_top_4x4.png (4×4 타일)
   blender -b --factory-startup --python Tools/blender/gen_canyon_tex.py -- [출력폴더, 기본 ~/GRD_canyon]
   (Blender 창에서는 exec(open(경로).read()) 로 돌려도 된다 — 그땐 기본 폴더.)
   이음새 없는 비결: 소음은 전부 FFT 필터 백색소음(자동 주기), 알갱이는 모듈로 감싸 그림.
"""
import bpy, numpy as np, os, sys
OUT = os.path.expanduser(sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "~/GRD_canyon")
os.makedirs(OUT, exist_ok=True)
S = 1024


def pnoise(seed, fu, fv, S=S):
    """주기 소음 0..1. fu/fv = 가로/세로 대표 주파수(클수록 잔 무늬). 비등방 가능."""
    rng = np.random.RandomState(seed)
    w = rng.randn(S, S).astype(np.float32)
    F = np.fft.fft2(w)
    ku = np.fft.fftfreq(S) * S
    kv = np.fft.fftfreq(S) * S
    KU, KV = np.meshgrid(ku, kv)           # KU 가로(열), KV 세로(행)
    f = np.exp(-((KU / fu) ** 2 + (KV / fv) ** 2))
    n = np.real(np.fft.ifft2(F * f)).astype(np.float32)
    n -= n.mean(); n /= (n.std() * 4 + 1e-9)
    return np.clip(n + 0.5, 0, 1)


def fbm(seed, base, octaves=5, aniso=(1, 1)):
    t = 0; a = 1; tot = 0
    for o in range(octaves):
        t = t + a * pnoise(seed + o * 11, base * 2 ** o * aniso[0], base * 2 ** o * aniso[1])
        tot += a; a *= .55
    return t / tot


def disc_layer(seed, n, rmin, rmax, grid=None):
    """모듈로로 감싼 작은 알갱이(잔돌) 높이장 0..1 + 그림자 방향 보정은 호출측."""
    rng = np.random.RandomState(seed)
    h = np.zeros((S, S), np.float32)
    yy, xx = np.mgrid[-rmax - 1:rmax + 2, -rmax - 1:rmax + 2].astype(np.float32)
    for _ in range(n):
        cx, cy = rng.randint(0, S, 2)
        r = rng.uniform(rmin, rmax)
        e = rng.uniform(.5, 1.0)               # 납작한 정도
        d = np.sqrt((xx / 1.0) ** 2 + (yy / e) ** 2) / r
        bump = np.clip(1 - d * d, 0, 1) ** .6 * rng.uniform(.6, 1.0)
        ys = (np.arange(yy.shape[0]) + cy - int(rmax) - 1) % S
        xs = (np.arange(xx.shape[1]) + cx - int(rmax) - 1) % S
        sl = np.ix_(ys, xs)
        h[sl] = np.maximum(h[sl], bump)
    return h


def lerp(a, b, t):
    return a + (b - a) * t[..., None]


def save(name, rgb):
    a = np.concatenate([np.clip(rgb, 0, 1), np.ones(rgb.shape[:2] + (1,), np.float32)], -1)
    h, w = a.shape[:2]
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.pixels = a[::-1].ravel()
    im.filepath_raw = os.path.join(OUT, name); im.file_format = "PNG"; im.save()
    bpy.data.images.remove(im)


def srgb(c):
    return np.array([c[0] / 255, c[1] / 255, c[2] / 255], np.float32)


# ---------------------------------------------------------------- 지층
def strata():
    rng = np.random.RandomState(2026)
    # 띠 7개, 굵기 제각각(합 1). 색은 적갈·주황·크림·짙은 갈색.
    thick = np.array([.20, .08, .17, .05, .22, .10, .18]); thick /= thick.sum()
    cols = [srgb(c) for c in [(150, 62, 38),    # 적갈
                              (205, 112, 58),   # 주황
                              (232, 205, 160),  # 연한 크림
                              (74, 44, 34),     # 짙은 갈색
                              (176, 78, 44),    # 붉은 주황
                              (221, 170, 112),  # 황토
                              (120, 52, 36)]]   # 진한 적갈
    edges = np.concatenate([[0], np.cumsum(thick)])
    v = (np.arange(S)[:, None] + .5) / S * np.ones((1, S), np.float32)
    # 경계가 물결치게: 가로로 주기적인 저주파 소음으로 v를 휘게 한다(완만, 띠 수 유지)
    warp = (fbm(31, 3, 4, (1.6, .6)) - .5) * .035 + (fbm(41, 9, 3, (1.4, .7)) - .5) * .008
    vv = (v + warp) % 1.0
    idx = np.clip(np.searchsorted(edges, vv, side="right") - 1, 0, len(cols) - 1)
    base = np.zeros((S, S, 3), np.float32)
    for i, c in enumerate(cols):
        base[idx == i] = c
    # 띠 안 그라디언트(위가 밝고 아래가 어두움) + 띠마다 약간의 색 흔들림
    pos = (vv - edges[idx]) / thick[idx]
    base *= (1.08 - .22 * pos)[..., None]
    # 띠 경계의 어두운 틈(퇴적 경계선)
    d_edge = np.minimum(vv - edges[idx], edges[idx + 1] - vv)
    base *= (1 - .28 * np.exp(-(d_edge / .006) ** 2) * (.4 + .6 * fbm(91, 50, 3, (.4, 1.0))))[..., None]
    # 풍화: 가로로 긴 결 + 세로로 흘러내린 얼룩 + 잔 알갱이
    grain = fbm(51, 70, 4, (1.0, 1.0))
    streak_h = fbm(61, 60, 4, (.25, 1.0))          # 가로로 긴 결
    drip = fbm(71, 6, 3, (1.0, .25))               # 세로로 긴 얼룩(위아래로 길다)
    blot = fbm(81, 4, 4)                           # 큰 풍화 얼룩
    shade = 1 + (grain - .5) * .35 + (streak_h - .5) * .55 - (drip - .5) * .35 + (blot - .5) * .30
    # 얼룩은 어둡게(흑갈 쪽으로), 밝은 곳은 크림 쪽으로
    rgb = base * shade[..., None]
    stain = np.clip((drip - .55) * 3, 0, 1) * .35
    rgb = lerp(rgb, rgb * srgb((110, 85, 75)) * 1.6, stain)
    # 절벽 균열(어두운 세로 선, 띠를 가로지름). 모듈로 감쌈
    crack = np.zeros((S, S), np.float32)
    for _ in range(6):
        x0 = rng.randint(0, S); y0 = rng.randint(0, S); L = rng.randint(60, 220)
        x = float(x0)
        for k in range(L):
            x += rng.randn() * .9
            crack[(y0 + k) % S, int(x) % S] = 1
    from_blur = crack.copy()                         # 한 칸 번짐(모듈로)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        from_blur += .5 * np.roll(np.roll(crack, dx, 1), dy, 0)
    rgb *= (1 - .18 * np.clip(from_blur * 1.8, 0, 1))[..., None]
    return np.clip(rgb, 0, 1)


# ---------------------------------------------------------------- 윗면 붉은 흙
def top():
    rng = np.random.RandomState(7)
    dark = srgb((128, 56, 36)); mid = srgb((170, 82, 50)); light = srgb((199, 116, 72)); dust = srgb((214, 150, 105))
    big = fbm(101, 3, 4)
    mid_n = fbm(111, 12, 4)
    fine = fbm(121, 120, 3)
    t = np.clip((big - .5) * 1.0 + .5, 0, 1)
    rgb = lerp(lerp(dark, mid, np.clip(t * 2, 0, 1)), light, np.clip(t * 2 - 1, 0, 1))
    # 마른 먼지 얼룩(연한 쪽)
    rgb = lerp(rgb, dust, np.clip((mid_n - .58) * 3, 0, 1) * .45)
    rgb *= (1 + (fine - .5) * .45)[..., None]
    # 마른 땅 갈라짐: 셀 경계가 아닌 소음 등고선의 가는 선(주기 자동)
    cr = fbm(131, 7, 3)
    line = np.exp(-((cr - .5) / .012) ** 2) * np.clip((fbm(133, 5, 3) - .45) * 4, 0, 1)
    rgb *= (1 - .22 * line)[..., None]
    # 잔돌: 밝기 높이장에서 위쪽 밝게·아래쪽 그림자
    pebbles = disc_layer(5, 350, 2, 5)
    pebbles2 = disc_layer(6, 70, 5, 10)
    h = np.maximum(pebbles, pebbles2)
    shade = h - np.roll(np.roll(h, 2, 0), 2, 1)       # 빛은 위왼쪽
    tint = fbm(141, 20, 2)[..., None]
    pebcol = lerp(rgb, srgb((120, 78, 62)) * (.8 + .6 * tint) , np.clip(h * 1.5, 0, 1) * .7)
    rgb = np.where((h > .08)[..., None], pebcol, rgb)
    rgb *= (1 + shade * .9)[..., None]
    return np.clip(rgb, 0, 1)


def tile4(rgb):
    return np.tile(rgb, (4, 4, 1))


def preview(name, rgb):
    # 4×4 = 4096² 는 무거워 512로 줄여 담는다(이음새 확인용이라 충분)
    t = tile4(rgb)[::4, ::4]
    save(name, t)


s = strata(); save("canyon_strata.png", s); preview("preview_strata_4x4.png", s)
t = top();    save("canyon_top.png", t);    preview("preview_top_4x4.png", t)
print("saved", OUT)
