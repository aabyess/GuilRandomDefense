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
ph = rng.rand(24) * 6.28
rays = np.zeros_like(r)
for k, p in enumerate(ph):
    n = 5 + k % 7
    rays += (np.abs(np.cos((th - p) * n / 2)) ** 24) * (0.5 + 0.5 * rng.rand())
rays = np.clip(rays / 4, 0, 1)
fall = np.clip(1 - r, 0, 1)
core = np.exp(-r * 5)
v = np.clip(core * 1.2 + rays * fall ** 1.2 * 1.1, 0, 1)
# Zap1_Red 근사: 흰 중심 + 붉은 광선
rgb = np.stack([np.clip(v * 1.0 + core * .6, 0, 1), np.clip(v * .25 + core * .7, 0, 1), np.clip(v * .35 + core * .7, 0, 1)], -1)
rgb[r > 1] = 0
def save(name, rgb):
    a = np.concatenate([rgb, np.ones((S, S, 1), np.float32)], -1)
    im = bpy.data.images.new(name, S, S, alpha=True)
    im.pixels = a[::-1].ravel()          # 이미지 y는 아래부터
    im.filepath_raw = os.path.join(OUT, name); im.file_format = "PNG"; im.save()
save("Textures_Zap1_Red.blp.png", rgb)
g = np.exp(-(r * 1.7) ** 2)
purple = np.stack([g * .75, g * .3, g * 1.0], -1); purple[r > 1] = 0
save("Textures_Purple_Glow.blp.png", purple)
