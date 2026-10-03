"""맵에 없는 워크3 기본 텍스처 Zap1_Red·Purple_Glow의 **근사**를 그린다(2026-10-03, 분홍 오라 조사).
   옛 근사(mdx_extract.placeholder)는 이름의 「zap」만 보고 **지그재그 번개**를 그렸는데, 원작 사진(Docs/reference/ui/원랜디_인게임_01.png)의 초월 발밑 오라
   (= HandsAura2 지오셋 1: 뾰족한 판 12장이 방사형으로 펼쳐진 모양)는 판마다 **흰 중심에서 퍼지는 광선**이고 보라 빛(Purple_Glow)과 가산돼 분홍·흰색으로 보인다.
   → Zap1_Red = 흰 중심 + 붉은 광선 방사형, Purple_Glow = 보라 원형 광채. 근거는 사진뿐이다(정품 BLP가 맵에 없다) — 정품을 구하면 교체.
   blender -b --factory-startup --python Tools/blender/gen_aura_approx_tex.py -- <출력폴더>
"""
import bpy, numpy as np, os, sys
OUT = sys.argv[sys.argv.index("--") + 1]
S = 256
y, x = np.mgrid[0:S, 0:S].astype(np.float32)
x = (x + .5) / S * 2 - 1; y = (y + .5) / S * 2 - 1
r = np.sqrt(x * x + y * y); th = np.arctan2(y, x)
rng = np.random.RandomState(7)
rays = np.zeros_like(r)
for k in range(24):
    p = rng.rand() * 6.28
    n = 5 + k % 7
    rays += (np.abs(np.cos((th - p) * n / 2)) ** 40) * (0.6 + 0.4 * rng.rand())
rays = np.clip(rays / 2.2, 0, 1)
# 2차(2026-10-03, 「원랜디처럼 더 밝게」): 흰 코어가 판의 절반 이상 · 광선은 연분홍~마젠타(붉은 기 거의 없음) · 가장자리만 보라
core = np.clip(1.15 - r * 0.85, 0, 1)                            # 판 전체의 분홍 바탕(가장자리로 갈수록 보라)
ray = rays * np.clip(1 - r, 0, 1) ** 0.6
v = np.clip(core * 0.6 + ray * 0.5 + np.exp(-r * 6) * 0.2, 0, 1)
# 밝기 → 색 사다리: 보라(가장자리) → 마젠타 → 연분홍 → 흰(중심). 붉은 기 없이 파랑 높게.
stops = [(0.0, (0.0, 0.0, 0.0)), (0.2, (0.4, 0.1, 0.75)), (0.45, (0.8, 0.25, 0.9)), (0.65, (0.98, 0.42, 0.95)), (0.9, (1.0, 0.85, 1.0)), (1.0, (1.0, 1.0, 1.0))]
rgb = np.zeros(v.shape + (3,), np.float32)
for (a0, c0), (a1, c1) in zip(stops[:-1], stops[1:]):
    m = (v >= a0) & (v <= a1)
    w = ((v - a0) / (a1 - a0))[..., None]
    rgb = np.where(m[..., None], np.array(c0, np.float32) * (1 - w) + np.array(c1, np.float32) * w, rgb)
rgb[r > 1] = 0
def save(name, rgb):
    a = np.concatenate([rgb, np.ones((S, S, 1), np.float32)], -1)
    im = bpy.data.images.new(name, S, S, alpha=True)
    im.pixels = a[::-1].ravel()          # 이미지 y는 아래부터
    im.filepath_raw = os.path.join(OUT, name); im.file_format = "PNG"; im.save()
save("Textures_Zap1_Red.blp.png", rgb)
g = np.exp(-(r * 1.5) ** 2)
purple = np.stack([g * .22, g * .06, g * .38], -1); purple[r > 1] = 0
save("Textures_Purple_Glow.blp.png", purple)
