"""성문(쇠창살, portcullis) 문 전환 그림 → Assets/Resources/UI/DoorCastle/ :
  gate_frame.png 1920x1080 — 돌벽 + 돌 아치(가운데가 투명, 폭 760·꼭대기 y=90) + 양옆 횃불 빛. 맨 위 겹.
  gate_grate.png 800x1040  — 쇠창살(아래쪽 뾰족 끝). 프레임 뒤에서 위로 올라가 아치 위 돌벽 뒤로 사라진다.
사용: /usr/bin/python3 Tools/ui/gen_castle_gate.py"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
from castle_art import *

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "DoorCastle")
os.makedirs(OUT, exist_ok=True)
W, H = 1920, 1080
CX, SPRING, R = 960, 470, 380      # 아치 가운데·시작선 y·반지름(폭 760)


def frame():
    wall = stone_wall(W, H, 71, bh=96, bw=190, tone=(0.33, 0.36, 0.40), contrast=0.6) * 0.85
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # 아치 열린 곳(위는 반원, 아래는 직사각형)
    op = Image.new("L", (W * 2, H * 2), 0)
    d = ImageDraw.Draw(op)
    d.rectangle([(CX - R) * 2, SPRING * 2, (CX + R) * 2, H * 2], fill=255)
    d.pieslice([(CX - R) * 2, (SPRING - R) * 2, (CX + R) * 2, (SPRING + R) * 2], 180, 360, fill=255)
    opening = np.asarray(op.resize((W, H), Image.LANCZOS), np.float32) / 255
    # 아치 테두리 쐐기돌(바깥 반지름 R+78까지): 각도로 나눈 밝은 돌 + 어두운 틈
    dist = np.hypot(xx - CX, np.minimum(yy - SPRING, 0))
    ring = (dist > R) & (dist < R + 78) & (yy < SPRING + 1) | ((np.abs(xx - CX) > R) & (np.abs(xx - CX) < R + 78) & (yy >= SPRING))
    ang = np.arctan2(np.minimum(yy - SPRING, 0), xx - CX)
    seg = np.where(yy < SPRING, np.floor(ang / (np.pi / 11)), np.floor((yy - SPRING) / 110) + 40)
    rnd = ((seg * 2654435761) % 1000).astype(np.float32) / 1000
    keystone = 0.9 + 0.4 * rnd
    ringimg = np.array([118, 124, 134], np.float32)[None, None, :] * keystone[..., None] * (0.8 + 0.4 * fbm(H, W, 72, 5, 10))[..., None]
    # 쐐기돌 틈 선
    if True:
        fa = (ang / (np.pi / 11)) % 1.0
        fy = ((yy - SPRING) / 110) % 1.0
        edge = np.where(yy < SPRING, np.minimum(fa, 1 - fa) * 3.5, np.minimum(fy, 1 - fy) * 6.0)
        ringimg *= np.clip(0.25 + edge, 0, 1)[..., None]
    img = np.where(ring[..., None], ringimg, wall)
    # 아치 안쪽으로 파인 깊이감(안쪽 가장자리 어둡게)
    inner = np.asarray(to_img(opening * 255, "L").filter(ImageFilter.GaussianBlur(14)), np.float32) / 255
    img *= (1 - 0.55 * np.clip(inner * (1 - opening) * 3, 0, 1))[..., None]
    img *= (1 - 0.35 * np.clip(np.asarray(to_img((1 - opening) * 255, "L").filter(ImageFilter.GaussianBlur(40)), np.float32) / 255 * opening * 2, 0, 1))[..., None]
    # 횃불 둘 + 불빛(따뜻한 색)
    for tx in (int(W * 0.17), int(W * 0.83)):
        ty = 430
        br = Image.new("L", (W, H), 0); bd = ImageDraw.Draw(br)
        bd.rectangle([tx - 8, ty, tx + 8, ty + 150], fill=255)
        bd.polygon([(tx - 30, ty - 10), (tx + 30, ty - 10), (tx + 16, ty + 28), (tx - 16, ty + 28)], fill=255)
        bm = np.asarray(br, np.float32)[..., None] / 255
        img = img * (1 - bm) + np.array([30, 28, 28], np.float32) * bm
        img += radial_glow(H, W, tx, ty - 34, 520, (140, 84, 36), 2.4)
        fire = Image.new("L", (W, H), 0); ImageDraw.Draw(fire).ellipse([tx - 18, ty - 72, tx + 18, ty - 6], fill=255)
        fm = np.asarray(fire.filter(ImageFilter.GaussianBlur(6)), np.float32)[..., None] / 255
        img = img * (1 - fm) + np.array([255, 190, 90], np.float32) * fm
    img = vignette(img, 0.55, 2.0)
    alpha = 1 - opening
    return composite(img, alpha)


def grate():
    gw, gh = 800, 1040
    img = np.zeros((gh, gw, 3), np.float32)
    mask = np.zeros((gh, gw), np.float32)
    iron = iron_texture(gw, gh, 81, (0.19, 0.20, 0.23)) * (0.8 + 0.5 * fbm(gh, gw, 82, 5, 8))[..., None]
    yy, xx = np.mgrid[0:gh, 0:gw].astype(np.float32)
    vx = [60 + 100 * i for i in range(8)]       # 세로 살대 x(8개, 사이 100)
    hy = [70 + 175 * i for i in range(5)]       # 가로 살대 y(5개)
    bar_w, bar_h, spike_top = 28, 24, 940
    light = np.ones((gh, gw), np.float32)
    for x in vx:
        m = np.clip((bar_w / 2 - np.abs(xx - x)) / 1.2, 0, 1) * (yy < spike_top)
        # 아래 뾰족 끝(삼각)
        tip = np.clip(1 - (yy - spike_top) / 100, 0, 1)
        m = np.maximum(m, np.clip((bar_w / 2 * tip - np.abs(xx - x)) / 1.2, 0, 1) * (yy >= spike_top) * (tip > 0))
        mask = np.maximum(mask, m)
        light = np.where(m > 0.5, 0.55 + 0.9 * np.clip(1 - np.abs(xx - x) / (bar_w / 2) * 0.9, 0, 1) * (0.7 + 0.3 * (xx < x)), light)
    for y in hy:
        m = np.clip((bar_h / 2 - np.abs(yy - y)) / 1.2, 0, 1) * ((xx > vx[0] - bar_w / 2) & (xx < vx[-1] + bar_w / 2))
        mask = np.maximum(mask, m)
        light = np.where(m > 0.5, 0.5 + 0.9 * np.clip(1 - np.abs(yy - y) / (bar_h / 2), 0, 1) * (0.7 + 0.3 * (yy < y)), light)
    img = iron * light[..., None]
    # 교차점 리벳
    for x in vx:
        for y in hy:
            rivet(img, x, y, 11, tone=(90, 96, 106))
    # 위쪽 가로 보강대(맨 위 굵은 띠)
    top = np.zeros((gh, gw), np.float32); top[0:46, 20:gw - 20] = 1
    img = img * (1 - top[..., None]) + iron * 0.9 * (0.6 + 0.6 * np.linspace(1, 0, gh)[:, None, None]) * top[..., None]
    mask = np.maximum(mask, top)
    # 살대 그림자(왼쪽 아래로 은은히)
    sh = np.asarray(to_img(np.roll(np.roll(mask, 8, 0), 6, 1) * 255, "L").filter(ImageFilter.GaussianBlur(5)), np.float32)[..., None] / 255
    img = img * (1 - 0.0 * sh)
    return composite(img, mask)


if __name__ == "__main__":
    f, g = frame(), grate()
    f.save(os.path.join(OUT, "gate_frame.png")); g.save(os.path.join(OUT, "gate_grate.png"))
    # 합성 미리보기: 검정 뒤 + 창살 닫힌 위치 + 프레임
    base = Image.new("RGBA", (W, H), (14, 16, 22, 255))
    base.alpha_composite(g, (CX - 400, 0)); base.alpha_composite(f)
    base.convert("RGB").save(os.path.expanduser("~/grd/GRD_title_castle/gate_closed_preview.png"))
    sheet([("gate_frame", f), ("gate_grate", g)], os.path.expanduser("~/grd/GRD_title_castle/sheet_gate.png"), cols=2, cell=900)
    print("ok")
