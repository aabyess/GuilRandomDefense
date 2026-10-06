#!/usr/bin/env python3
"""하단 UI 시안 그림(사장님 10-07 「하단 UI 별로, 시안 3개」) — 직접 그린다(원작·외부 파일 0).
  A 돌 하단 바 + 얇은 나무 테두리  ·  B 밝은 원목·리벳 없이 단정  ·  C 워크3 돌·금속 톤(청동 테두리)
산출(각 시안 접두사 a_/b_/c_): <p>bar.png(타일) · <p>edge.png(바 윗선 띠, 가로 타일) · <p>cell.png(콘솔 칸 9-slice, 테두리 12px).
실행: /usr/bin/python3 Tools/ui/gen_bar_variants.py [출력폴더]   (Pillow·numpy — 맥 시스템 파이썬에 있다)
기본 출력: Assets/Resources/UI/SkinBar/ (임포트 설정·9-slice 테두리는 Editor BarSkinApply가 건다).
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'UI', 'SkinBar')
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(20261007)


def to_img(arr, mode='RGB'):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), mode)


def wood(w, h, base, plank_h, seed, grain=0.5, seam=(40, 26, 16), contrast=1.0):
    """가로 판자 — 판자마다 밝기 차이 + 결(가로로 긴 노이즈) + 이음매 선."""
    r = np.random.default_rng(seed)
    arr = np.zeros((h, w, 3), np.float32)
    y = 0
    while y < h:
        ph = min(plank_h, h - y)
        shade = r.uniform(-14, 14) * contrast
        col = np.array(base, np.float32) + shade
        # 결: 가로 방향으로 길게 늘린 노이즈
        n = r.normal(0, 1, (ph, w // 8 + 2))
        n = np.kron(n, np.ones((1, 8)))[:, :w]
        k = np.ones(5) / 5
        n = np.apply_along_axis(lambda v: np.convolve(v, k, mode='same'), 1, n)
        rings = np.sin((np.arange(ph)[:, None] * 0.9 + n * 2.2) * 1.3) * 0.5
        plank = col[None, None, :] + (n[..., None] * 9 + rings[..., None] * 8) * grain * contrast
        arr[y:y + ph] = plank
        arr[y:y + 1] = np.array(seam, np.float32)        # 이음매(위)
        arr[y + ph - 1:y + ph] = arr[y + ph - 1:y + ph] * 0.8
        y += ph
    return arr


def bevel_frame(w, h, border, light, mid, dark, inner_fill, outline=(20, 14, 10), radius=0):
    """9-slice 칸: 바깥 윤곽선 + 밝은 위/왼 · 어두운 아래/오른 모서리 + 안쪽 채움."""
    ss = 4
    im = Image.new('RGBA', (w * ss, h * ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    b = border * ss
    d.rectangle([0, 0, w * ss - 1, h * ss - 1], fill=outline + (255,))
    o = ss * 2
    d.rectangle([o, o, w * ss - 1 - o, h * ss - 1 - o], fill=mid + (255,))
    # 밝은 위/왼
    d.polygon([(o, o), (w * ss - 1 - o, o), (w * ss - 1 - b, b), (b, b), (b, h * ss - 1 - b), (o, h * ss - 1 - o)], fill=light + (255,))
    # 어두운 아래/오른
    d.polygon([(w * ss - 1 - o, h * ss - 1 - o), (o, h * ss - 1 - o), (b, h * ss - 1 - b), (w * ss - 1 - b, h * ss - 1 - b), (w * ss - 1 - b, b), (w * ss - 1 - o, o)], fill=dark + (255,))
    d.rectangle([b, b, w * ss - 1 - b, h * ss - 1 - b], fill=inner_fill + (255,))
    # 안쪽 한 줄 그림자
    d.rectangle([b, b, w * ss - 1 - b, b + ss * 2], fill=tuple(int(c * 0.55) for c in inner_fill) + (255,))
    return im.resize((w, h), Image.LANCZOS)


def stone(w, h, seed, base=(112, 114, 120)):
    r = np.random.default_rng(seed)
    im = Image.new('RGB', (w, h), base)
    d = ImageDraw.Draw(im)
    y = 0; row = 0
    while y < h:
        bh = int(r.integers(26, 38)); x = -int(r.integers(0, 40)) if row % 2 else 0
        while x < w:
            bw = int(r.integers(40, 64)); s = int(r.integers(-14, 14))
            d.rectangle([x, y, x + bw - 2, y + bh - 2], fill=tuple(max(0, min(255, c + s)) for c in base), outline=(46, 48, 54))
            x += bw
        y += bh; row += 1
    a = np.asarray(im, np.float32) + r.normal(0, 7, (h, w))[..., None]
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))


def save(img, name):
    img.save(os.path.join(OUT, name))
    print(name, img.size)


# ── A: 돌 바 + 얇은 나무 테두리 ──
stone_bar = stone(256, 256, 7)
save(stone_bar, 'a_bar.png')
edge = to_img(wood(256, 14, (150, 104, 62), 14, 11, 0.6, (50, 32, 18)))
ed = ImageDraw.Draw(edge); ed.line([(0, 0), (255, 0)], fill=(205, 160, 105)); ed.line([(0, 13), (255, 13)], fill=(40, 26, 15))
save(edge, 'a_edge.png')
save(bevel_frame(96, 96, 10, (176, 126, 78), (128, 88, 52), (82, 54, 32), (28, 28, 32)), 'a_cell.png')

# ── B: 밝은 원목, 리벳 없이 단정 ──
save(to_img(wood(512, 256, (205, 158, 100), 64, 21, 0.55, (150, 108, 64), 0.8)), 'b_bar.png')
bedge = to_img(wood(256, 16, (225, 180, 120), 16, 22, 0.4, (160, 118, 72), 0.6))
bd = ImageDraw.Draw(bedge); bd.line([(0, 0), (255, 0)], fill=(245, 215, 160)); bd.line([(0, 15), (255, 15)], fill=(120, 84, 48))
save(bedge, 'b_edge.png')
save(bevel_frame(96, 96, 12, (238, 196, 138), (190, 140, 88), (140, 98, 58), (58, 40, 28), outline=(96, 66, 38)), 'b_cell.png')

# ── C: 워크3 돌·금속(청동 테두리) ──
c = np.asarray(stone(256, 256, 9, base=(62, 64, 72)), np.float32)
c += np.random.default_rng(5).normal(0, 3, (256, 256))[..., None]
save(to_img(c), 'c_bar.png')
cedge = Image.new('RGB', (256, 14), (120, 92, 44)); cd = ImageDraw.Draw(cedge)
cd.line([(0, 0), (255, 0)], fill=(236, 204, 120)); cd.line([(0, 1), (255, 1)], fill=(190, 150, 70)); cd.line([(0, 12), (255, 12)], fill=(70, 50, 22)); cd.line([(0, 13), (255, 13)], fill=(30, 22, 10))
save(cedge, 'c_edge.png')
save(bevel_frame(96, 96, 10, (232, 200, 116), (168, 130, 60), (92, 66, 28), (14, 15, 20), outline=(24, 18, 8)), 'c_cell.png')
