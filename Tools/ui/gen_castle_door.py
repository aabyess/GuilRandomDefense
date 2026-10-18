"""성 테마 문 짝 → Assets/Resources/UI/DoorCastle/ : door_left/door_right 1280x1440(옛 선술집 Door와 같은 크기·같은 배치: 경첩 바깥, 손잡이 고리 가운데 쪽) + door_gap_light(차가운 빛줄).
사용: /usr/bin/python3 Tools/ui/gen_castle_door.py [출력폴더]"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
from castle_art import *

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "DoorCastle")
os.makedirs(OUT, exist_ok=True)
W, H = 1280, 1440


def door_left():
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # 세로 쇠판 6장(+ 가운데 쪽 좁은 한 장) — 판 사이 틈은 어둡게(옛 문은 주황 틈이라 투명하지 않고 어두운 틈 + 빛줄은 gap 그림이 맡는다)
    edges = [0, 200, 420, 640, 855, 1070, 1215, 1280]
    base = np.zeros((H, W, 3), np.float32)
    seam = np.zeros((H, W), np.float32)
    for i in range(len(edges) - 1):
        x0, x1 = edges[i] + 6, edges[i + 1] - 6
        u = np.clip((xx - x0) / max(1, x1 - x0), 0, 1)
        plate = iron_texture(W, H, 50 + i, (0.22, 0.23, 0.26))
        # 세로 결 + 가장자리 하이라이트/그림자
        streak = fbm(H, W, 90 + i, 3, 2)[..., None] * 0.0 + 1
        edge = np.minimum(xx - x0, x1 - xx)
        bev = np.clip(edge / 12, 0, 1)
        light = 1.0 + 0.35 * (1 - bev) * np.where(xx - x0 < x1 - xx, 1, -1) + 0.12 * (1 - np.abs(u - 0.4))
        inside = (xx >= x0) & (xx < x1)
        wear = 0.8 + 0.5 * fbm(H, W, 120 + i, 5, 4)
        base = np.where(inside[..., None], plate * (light * wear)[..., None], base)
    base = np.where(base.sum(2, keepdims=True) == 0, np.array([8, 9, 11], np.float32), base)
    # 녹 얼룩(푸른 회색 위에 아주 옅은 갈색)
    rust = np.clip(fbm(H, W, 200, 5, 5) - 0.55, 0, 1) * 2.0
    base = base * (1 - 0.35 * rust[..., None]) + np.array([60, 38, 24], np.float32)[None, None, :] * 0.35 * rust[..., None]
    # 가로 쇠띠 셋 (옛 문과 같은 위치: y=200~310, 630~735, 1130~1235), 경첩 쪽(왼쪽)에서 시작해 가운데 쪽 직전에서 뾰족하게 끝남
    img = base.copy()
    straps = [(200, 310), (630, 735), (1130, 1235)]
    for (y0, y1) in straps:
        pts = [(40, y0), (1100, y0), (1140, (y0 + y1) // 2), (1100, y1), (40, y1)]
        m = mask_poly(W, H, pts)
        st = iron_texture(W, H, 300 + y0, (0.14, 0.15, 0.17)) * (0.8 + 0.5 * fbm(H, W, 310 + y0, 5, 6))[..., None]
        st = st * (1 + bevel_light(m, 6, strength=1.4)[..., None] * 0.9)
        sh = np.asarray(to_img(np.roll(np.roll(m, 12, 0), 5, 1) * 255, "L").filter(ImageFilter.GaussianBlur(9)), np.float32)[..., None] / 255
        img = img * (1 - 0.55 * sh)
        img = img * (1 - m[..., None]) + st * m[..., None]
        for cx in range(90, 1100, 141):
            rivet(img, cx, (y0 + y1) // 2, 17, tone=(96, 102, 112))
    # 경첩(왼쪽 바깥 가장자리의 굵은 쇠 원통 3개)
    for (y0, y1) in straps:
        pts = [(0, y0 - 45), (52, y0 - 45), (52, y1 + 45), (0, y1 + 45)]
        m = mask_poly(W, H, pts)
        cyl = 0.5 + 0.7 * np.clip(1 - np.abs((yy - (y0 + y1) / 2) / ((y1 - y0) / 2 + 45)), 0, 1)
        hinge = np.array([58, 62, 70], np.float32)[None, None, :] * cyl[..., None]
        img = img * (1 - m[..., None]) + hinge * m[..., None]
        rivet(img, 26, y0 - 20, 7); rivet(img, 26, y1 + 20, 7)
    # 판마다 작은 리벳(옛 문 위치 참고: 띠 사이)
    for i, cx in enumerate([107, 320, 533, 746, 960, 1150]):
        for cy in (170, 340, 600, 765, 1100, 1265):
            rivet(img, cx, cy, 8, tone=(86, 92, 102))
    # 손잡이: 쇠 받침판 + 큰 고리(가운데 쪽, 옛 문과 같은 자리 ≈ (1125, 800))
    plate = mask_poly(W, H, [(1062, 684), (1195, 684), (1195, 868), (1062, 868)])
    pimg = iron_texture(W, H, 400, (0.20, 0.21, 0.24)) * (1 + bevel_light(plate, 5)[..., None] * 0.9)
    shp = np.asarray(to_img(np.roll(np.roll(plate, 10, 0), 6, 1) * 255, "L").filter(ImageFilter.GaussianBlur(8)), np.float32)[..., None] / 255
    img = img * (1 - 0.5 * shp)
    img = img * (1 - plate[..., None]) + pimg * plate[..., None]
    for cx, cy in [(1082, 720), (1172, 720), (1082, 830), (1172, 830)]:
        rivet(img, cx, cy, 11, tone=(100, 106, 116))
    ccx, ccy, R, r = 1125, 810, 100, 74
    d = np.hypot(xx - ccx, yy - ccy)
    ring = np.clip((R - d) / 1.5, 0, 1) * np.clip((d - r) / 1.5, 0, 1)
    ringsh = np.asarray(to_img(np.roll(np.roll(ring, 14, 0), 8, 1) * 255, "L").filter(ImageFilter.GaussianBlur(9)), np.float32)[..., None] / 255
    img = img * (1 - 0.6 * ringsh)
    tube = np.clip(1 - np.abs(d - (R + r) / 2) / ((R - r) / 2), 0, 1)
    ang = np.arctan2(yy - ccy, xx - ccx)
    ringc = np.array([84, 90, 100], np.float32)[None, None, :] * (0.4 + 0.8 * tube[..., None]) * (0.85 + 0.3 * np.cos(ang + 2.3))[..., None]
    img = img * (1 - ring[..., None]) + ringc * ring[..., None]
    img = vignette(img, 0.25, 2.0)
    return composite(img, np.ones((H, W), np.float32))


def gap_light():
    # 가운데(x=640) 세로 빛줄: 차가운 푸른 흰색, 알파로 번진다(색은 DoorTransition이 MenuTheme.DoorGapLight로 곱해 칠한다)
    xx = np.arange(W, dtype=np.float32)[None, :].repeat(H, 0)
    core = np.exp(-((xx - 640) / 3.0) ** 2)
    mid = np.exp(-((xx - 640) / 26.0) ** 2) * 0.55
    wide = np.exp(-((xx - 640) / 95.0) ** 2) * 0.22
    yy = np.arange(H, dtype=np.float32)[:, None].repeat(W, 1)
    fade = 0.75 + 0.25 * np.sin(yy / H * np.pi)
    a = np.clip((core + mid + wide) * fade, 0, 1)
    rgb = np.zeros((H, W, 3), np.float32)
    rgb[..., 0] = 215 + 40 * core; rgb[..., 1] = 230 + 25 * core; rgb[..., 2] = 255
    return composite(rgb, a)


if __name__ == "__main__":
    L = door_left()
    R = L.transpose(Image.FLIP_LEFT_RIGHT)
    gap = gap_light()
    L.save(os.path.join(OUT, "door_left.png")); R.save(os.path.join(OUT, "door_right.png")); gap.save(os.path.join(OUT, "door_gap_light.png"))
    sheet([("door_left", L), ("door_right", R), ("door_gap_light", gap)], os.path.expanduser("~/grd/GRD_title_castle/sheet_3.png"), cols=3, cell=560)
    print("ok", OUT)
