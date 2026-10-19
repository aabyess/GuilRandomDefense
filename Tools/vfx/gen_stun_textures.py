#!/usr/bin/env /usr/bin/python3
# 스턴 대체 시안 텍스처(자작·CC0 취급): 룬 고리 + 푸른 불꽃 한 가닥. 흰색 그림 + 알파(색은 파티클 startColor가 입힘, Kenney와 같은 꼴).
# 쓰는 법: python3 Tools/vfx/gen_stun_textures.py → Assets/Art/Effects/Own/{rune_ring,flame_blue}.png
import math, random, os
from PIL import Image, ImageDraw, ImageFilter
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Effects", "Own")
os.makedirs(OUT, exist_ok=True)
SS = 4  # 슈퍼샘플

def rune_ring(size=512):
    S = size * SS; c = S / 2
    layer = Image.new("L", (S, S), 0); d = ImageDraw.Draw(layer)
    def ring(r, w):
        d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=int(w))
    ring(S * 0.470, S * 0.012)   # 바깥 쇠 테
    ring(S * 0.455, S * 0.004)
    ring(S * 0.335, S * 0.008)   # 안쪽 테
    ring(S * 0.322, S * 0.003)
    # 룬 12개(엘더 푸타르크 느낌의 가지 달린 획) — 두 테 사이
    rnd = random.Random(7)
    rin, rout = S * 0.350, S * 0.440
    for i in range(12):
        a = i / 12 * math.tau
        ux, uy = math.cos(a), math.sin(a); tx, ty = -uy, ux
        def P(r, off): return (c + ux * r + tx * off, c + uy * r + ty * off)
        w = S * 0.0085
        d.line([P(rin, 0), P(rout, 0)], fill=255, width=int(w))  # 줄기
        kind = rnd.randrange(5)
        mid = (rin + rout) / 2; q = (rout - rin) * 0.28; off = S * 0.032
        if kind == 0:   # ᚠ
            d.line([P(mid + q * 1.2, 0), P(rout - q * .2, off)], fill=255, width=int(w)); d.line([P(mid - q * .2, 0), P(rout - q * 1.4, off)], fill=255, width=int(w))
        elif kind == 1:  # ᚦ
            d.line([P(mid + q, 0), P(mid, off)], fill=255, width=int(w)); d.line([P(mid, off), P(mid - q, 0)], fill=255, width=int(w))
        elif kind == 2:  # ᚱ
            d.line([P(rout, 0), P(mid, off)], fill=255, width=int(w)); d.line([P(mid, off), P(rin + q * .3, -off * .6)], fill=255, width=int(w))
        elif kind == 3:  # ᛉ
            d.line([P(mid, 0), P(rout, off)], fill=255, width=int(w)); d.line([P(mid, 0), P(rout, -off)], fill=255, width=int(w))
        else:            # ᚨ
            d.line([P(rout, 0), P(mid, off)], fill=255, width=int(w)); d.line([P(mid + q * .8, 0), P(mid - q * .6, off)], fill=255, width=int(w))
    # 테 위 마름모 못 4개
    for k in range(4):
        a = k / 4 * math.tau + math.pi / 4
        px, py = c + math.cos(a) * S * 0.4625, c + math.sin(a) * S * 0.4625
        r = S * 0.018
        d.polygon([(px, py - r), (px + r, py), (px, py + r), (px - r, py)], fill=255)
    core = layer.resize((size, size), Image.LANCZOS)
    glow = core.filter(ImageFilter.GaussianBlur(size * 0.012))
    import numpy as np
    arr = np.maximum(np.array(core, dtype=np.float32), np.array(glow, dtype=np.float32) * 1.6)
    arr = np.clip(arr, 0, 255).astype("uint8")
    rgba = np.zeros((size, size, 4), dtype="uint8"); rgba[..., :3] = 255; rgba[..., 3] = arr
    Image.fromarray(rgba).save(os.path.join(OUT, "rune_ring.png"))

def flame_blue(w=256, h=256):
    import numpy as np
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (x - w / 2) / (w / 2); v = 1 - y / h                    # v: 0 밑 → 1 끝
    # 눈물 모양: 아래가 넓고 위로 가늘어지며 살짝 휘어진다
    width = np.clip(0.78 * (1 - v) ** 0.9 * (0.35 + 0.65 * np.sin(np.pi * np.clip(v * 1.15, 0, 1))), 0.001, 1)
    bend = 0.22 * v * v
    d = np.abs(u - bend) / width
    rnd = np.random.RandomState(3)
    noise = (rnd.rand(h // 8 + 1, w // 8 + 1)).astype(np.float32)
    noise = np.array(Image.fromarray((noise * 255).astype("uint8")).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255
    d = np.clip(d + (noise - 0.5) * 0.35 * v, 0, None)
    a = np.clip(1 - d ** 1.6, 0, 1) * np.clip(1.15 - v * 0.9, 0, 1)
    core = np.clip(1 - d * 1.8, 0, 1)                            # 안쪽 더 밝게(흰 심)
    a = np.clip(a * 0.85 + core * 0.4, 0, 1)
    a *= np.clip(v / 0.14, 0, 1) ** 1.5                           # 밑동은 부드럽게 사라짐(잘린 가장자리 없이)
    rgba = np.zeros((h, w, 4), dtype="uint8"); rgba[..., :3] = 255; rgba[..., 3] = (a * 255).astype("uint8")
    Image.fromarray(rgba).save(os.path.join(OUT, "flame_blue.png"))

rune_ring(); flame_blue()
print("ok", os.listdir(OUT))
