"""성 테마 설정 창 그림 → Assets/Resources/UI/SettingsCastle/ (옛 Settings와 같은 크기·같은 9-slice 경계).
  panel_9slice 1024x1024(경계 128) · nameplate 512x160 · slider_groove/fill 512x48(경계 좌우 20) · slider_knob_mug 64x64(이름은 그대로, 그림은 쇠고리)
사용: /usr/bin/python3 Tools/ui/gen_castle_settings.py [출력폴더]"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
from castle_art import *

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "SettingsCastle")
os.makedirs(OUT, exist_ok=True)


def panel():
    S = 1024
    img = iron_panel(S, S, 100, 46, seed=61, interior="stone", rim_tone=(0.26, 0.28, 0.32), rivets=False)
    a = np.asarray(img, np.float32)
    rgb = a[..., :3].copy()
    alpha = a[..., 3] / 255
    # 모서리 쇠판 4개(옛 판처럼) + 사방에 리벳 줄
    for cx, cy in [(64, 64), (S - 64, 64), (64, S - 64), (S - 64, S - 64)]:
        m = mask_poly(S, S, [(cx - 56, cy - 56), (cx + 56, cy - 56), (cx + 56, cy + 56), (cx - 56, cy + 56)])
        m = m * alpha
        pl = iron_texture(S, S, 70 + cx % 7, (0.24, 0.25, 0.29)) * (1 + bevel_light(m, 6, strength=1.3)[..., None] * 0.9)
        rgb = rgb * (1 - m[..., None]) + pl * m[..., None]
        for dx, dy in [(-30, -30), (30, -30), (-30, 30), (30, 30)]:
            rivet(rgb, cx + dx, cy + dy, 9, tone=(104, 110, 120))
    for t in range(190, S - 190, 114):
        for cx, cy in [(t, 50), (t, S - 50), (50, t), (S - 50, t)]:
            rivet(rgb, cx, cy, 8, tone=(92, 98, 108))
    return composite(rgb, alpha)


def nameplate():
    w, h = 512, 160
    img = iron_panel(w, h, 18, 20, seed=81, interior="dark", rim_tone=(0.30, 0.32, 0.36), rivets=True)
    a = np.asarray(img, np.float32)
    rgb = a[..., :3].copy()
    # 위쪽 사슬 고리 둘(옛 판의 걸이 자리 x≈96, 416)
    for cx in (96, 416):
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        d = np.hypot(xx - cx, yy - 8)
        ring = np.clip((13 - d) / 1.2, 0, 1) * np.clip((d - 8) / 1.2, 0, 1)
        rgb = rgb * (1 - ring[..., None]) + np.array([104, 110, 122], np.float32) * ring[..., None]
    return composite(rgb, a[..., 3] / 255)


def groove():
    w, h = 512, 48
    pts = [(0, 0), (w, 0), (w, h), (0, h)]
    r = 22
    m = Image.new("L", (w * 2, h * 2), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, w * 2 - 1, h * 2 - 1], radius=r * 2, fill=255)
    m = np.asarray(m.resize((w, h), Image.LANCZOS), np.float32) / 255
    tex = iron_texture(w, h, 91, (0.07, 0.08, 0.10)) * 0.8
    top = np.linspace(1.2, 0.5, h)[:, None, None]
    rgb = tex * top
    edge = np.clip(1 - np.asarray(to_img(m * 255, "L").filter(ImageFilter.GaussianBlur(3)), np.float32) / 255, 0, 1)
    rgb = rgb + 40 * edge[..., None] * 0.6
    return composite(rgb, m)


def fill():
    w, h = 512, 48
    m = Image.new("L", (w * 2, h * 2), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, w * 2 - 1, h * 2 - 1], radius=40, fill=255)
    m = np.asarray(m.resize((w, h), Image.LANCZOS), np.float32) / 255
    g = np.linspace(1.15, 0.55, h)[:, None]
    base = np.array([150, 168, 196], np.float32)[None, None, :] * g[..., None] * np.ones((1, w, 1), np.float32)
    base *= (0.9 + 0.2 * fbm(h, w, 95, 3, 8))[..., None]
    return composite(base, m)


def knob():
    S = 64
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    c = S / 2
    d = np.hypot(xx - c, yy - c)
    outer = np.clip((28 - d) / 1.2, 0, 1)
    inner = np.clip((d - 11) / 1.2, 0, 1)
    ring = outer * inner
    ang = np.arctan2(yy - c, xx - c)
    tube = np.clip(1 - np.abs(d - 19.5) / 9, 0, 1)
    rgb = np.array([150, 158, 172], np.float32)[None, None, :] * (0.35 + 0.8 * tube[..., None]) * (0.8 + 0.35 * np.cos(ang + 2.3))[..., None]
    # 안쪽 구멍을 어둡게 비침 없이 투명으로
    return composite(rgb, ring)


if __name__ == "__main__":
    out = {"panel_9slice.png": panel(), "nameplate.png": nameplate(), "slider_groove.png": groove(), "slider_fill.png": fill(), "slider_knob_mug.png": knob()}
    for n, im in out.items(): im.save(os.path.join(OUT, n))
    sheet([(n, im) for n, im in out.items()], os.path.expanduser("~/grd/GRD_title_castle/sheet_4.png"), cols=3, cell=560)
    print("ok", OUT)
