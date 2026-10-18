"""성 테마 첫 화면 그림 → Assets/Resources/UI/LobbyCastle/ (옛 선술집 Lobby와 같은 픽셀 크기·같은 9-slice 경계).
  btn_normal/hover/pressed 512x128(경계 40) · menu_frame 512x1024(경계 64/160) · title_plate 1400x300 · bg_1920x1080 · bg_2560x1440
사용: /usr/bin/python3 Tools/ui/gen_castle_lobby.py [출력폴더]   (기본 Assets/Resources/UI/LobbyCastle)"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
sys.path.insert(0, os.path.dirname(__file__))
from castle_art import *

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "UI", "LobbyCastle")
os.makedirs(OUT, exist_ok=True)


def button(state):
    # 40px 경계 안에서 쇠 테두리(두께 ~16) + 어두운 쇠판. hover = 테두리 밝게 + 차가운 안쪽 빛, pressed = 어둡게 눌림.
    tint = {"normal": 1.0, "hover": 1.25, "pressed": 0.75}[state]
    glow = {"normal": 0.0, "hover": 0.55, "pressed": 0.0}[state]
    img = iron_panel(512, 128, 16, 22, seed=11, interior="dark", rim_tone=(0.30, 0.32, 0.36), tint=tint, glow=glow)
    if state == "pressed":
        a = np.asarray(img, np.float32)
        a[..., :3] *= 0.9
        # 위쪽 안쪽 그림자(눌린 느낌)
        ys = np.linspace(1, 0, 128)[:, None] ** 3
        a[..., :3] *= (1 - 0.35 * ys)[..., None]
        img = Image.fromarray(a.astype(np.uint8))
    return img


def menu_frame():
    w, h = 512, 1024
    img = iron_panel(w, h, 22, 34, seed=21, interior="stone", rim_tone=(0.28, 0.30, 0.34), rivets=False)
    a = np.asarray(img, np.float32)
    rgb = a[..., :3].copy()
    # 위·아래 띠(경계 160 안): 흉벽(성가퀴) 무늬 + 가로 쇠띠
    for y0, flip in [(0, False), (h - 160, True)]:
        band = Image.new("L", (w, 160), 0)
        d = ImageDraw.Draw(band)
        for i in range(8):
            x = 48 + i * 52
            d.rectangle([x, 54 if not flip else 66, x + 28, 112 if not flip else 124], fill=255)
        m = np.asarray(band, np.float32) / 255
        if flip: m = m[::-1]
        m = m if not flip else m[::-1][::-1]
        sh = np.asarray(band.filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
        sl = slice(y0, y0 + 160)
        light = np.asarray(band, np.float32) / 255
        rgb[sl] = rgb[sl] * (1 - 0.35 * np.roll(sh, 3, 0)[..., None]) 
        rgb[sl] = rgb[sl] * (1 - light[..., None]) + (np.array([88, 94, 104], np.float32)[None, None, :] * (0.7 + 0.5 * np.linspace(1, 0, 160)[:, None, None] * (1 if not flip else 0) + 0.3)) * light[..., None]
        yy = 130 if not flip else 28
        rgb[y0 + yy:y0 + yy + 4, 14:w - 14] = np.array([120, 126, 138])
        rgb[y0 + yy + 4:y0 + yy + 7, 14:w - 14] = np.array([30, 32, 36])
    # 안쪽 큰 칸(글자 올라가는 곳)을 더 어둡고 단조롭게
    inner = np.zeros((h, w), np.float32)
    inner[170:h - 170, 40:w - 40] = 1
    inner = np.asarray(to_img(inner * 255, "L").filter(ImageFilter.GaussianBlur(6)), np.float32)[..., None] / 255
    rgb = rgb * (1 - 0.45 * inner)
    for cy in range(230, h - 200, 82):
        rivet(rgb, 20, cy, 5)
        rivet(rgb, w - 20, cy, 5)
    for cx, cy in [(30, 30), (w - 30, 30), (30, h - 30), (w - 30, h - 30)]:
        rivet(rgb, cx, cy, 8)
    return composite(rgb, a[..., 3] / 255)


def title_plate():
    w, h = 1400, 300
    # 옛 판: 양 끝이 뾰족한 육각 띠(높이 약 150, 중앙 y 150). 같은 실루엣의 쇠띠 + 어두운 쇠판 + 양끝 쇠 방패 돋움.
    cy = 150
    pts = [(22, cy), (210, 72), (1190, 72), (1378, cy), (1190, 228), (210, 228)]
    outer = mask_poly(w, h, pts)
    inset = [(70, cy), (222, 92), (1178, 92), (1330, cy), (1178, 208), (222, 208)]
    inner = mask_poly(w, h, inset)
    rim = iron_texture(w, h, 31, (0.30, 0.32, 0.36))
    rim *= 1 + bevel_light(outer, 6, strength=1.3)[..., None] * 0.9
    fill = iron_texture(w, h, 33, (0.09, 0.10, 0.12)) * 0.95
    # 가운데는 어두운 돌 조금(글자 뒤 대비)
    fill *= 1 + bevel_light(inner, 5, dx=1, dy=1)[..., None] * 0.6
    rgb = rim * np.clip(outer - inner, 0, 1)[..., None] + fill * inner[..., None]
    rgb = np.where(outer[..., None] > 0.01, rgb / np.maximum(outer[..., None], 1e-3), 0)
    # 위·아래 가는 은줄
    for yy in (96, 202):
        rgb[yy:yy + 3, 232:1168] = np.where(inner[yy:yy + 3, 232:1168, None] > 0.5, np.array([112, 118, 130]), rgb[yy:yy + 3, 232:1168])
    # 중앙 위쪽 이음 + 양끝 쇠 돋움(옛 홍보석 자리): 쇠 방패 + 낡은 붉은 보석
    for cx in (150, 1250):
        base = np.zeros((h, w), np.float32)
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        base = np.clip((46 - np.hypot(xx - cx, yy - cy)) / 1.5, 0, 1)
        ring = np.clip((38 - np.hypot(xx - cx, yy - cy)) / 1.5, 0, 1)
        rgb = rgb * (1 - 0.5 * np.clip((52 - np.hypot(xx - cx - 3, yy - cy - 6)) / 14, 0, 1)[..., None])
        steel = np.array([128, 135, 148], np.float32)
        dome = np.clip(1 - np.hypot(xx - cx, yy - cy) / 46, 0, 1)
        rgb = rgb * (1 - base[..., None]) + steel[None, None, :] * (0.5 + 0.7 * dome[..., None]) * base[..., None]
        rgb = rgb * (1 - ring[..., None]) + np.array([54, 57, 64], np.float32)[None, None, :] * ring[..., None]
        gem = np.clip((28 - np.hypot(xx - cx, yy - cy)) / 1.5, 0, 1)
        spec = np.clip(1 - np.hypot(xx - cx + 8, yy - cy + 9) / 12, 0, 1) ** 2
        gcol = np.array([110, 22, 24], np.float32)[None, None, :] * (0.5 + 0.8 * np.clip(1 - np.hypot(xx - cx, yy - cy) / 28, 0, 1)[..., None]) + 120 * spec[..., None]
        rgb = rgb * (1 - gem[..., None]) + gcol * gem[..., None]
    for cx in (330, 1070):
        rivet(rgb, cx, 80, 6); rivet(rgb, cx, 220, 6)
    out = composite(rgb, outer)
    # 바닥 그림자(옛 판처럼 아래로 번짐)
    sh = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sa = Image.fromarray((np.roll(outer, 14, 0) * 150).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10))
    sh.putalpha(sa)
    sh.alpha_composite(out)
    return sh


def background(w, h, seed=41):
    """성 대전 한 장(정적): 돌벽 + 높은 아치 창 두 개(달빛) + 횃불 둘 + 바닥 판석 + 안개 + 비네트.
    메뉴 틀은 오른쪽, 제목 판은 왼쪽 위 — 둘 뒤는 비교적 조용하게 둔다."""
    s = w / 1920.0
    wall = stone_wall(w, h, seed, bh=int(86 * s), bw=int(170 * s), tone=(0.33, 0.36, 0.40), contrast=0.6)
    wall *= 0.8
    floor_y = int(h * 0.80)
    # 바닥: 원근 판석(가까울수록 큼)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = np.clip((yy - floor_y) / (h - floor_y + 1), 0, 1)
    depth = 1.0 / (0.25 + 0.75 * t + 1e-3)
    fl = fbm(h, w, seed + 9, 5, 8)
    fx = (xx - w / 2) / (0.35 + 1.9 * t + 0.05) / (60 * s)
    fy = depth * 1.4
    cell_x = np.floor(fx).astype(np.int32)
    cell_y = np.floor(fy * 2.0).astype(np.int32)
    key = ((cell_x * 73856093) ^ (cell_y * 19349663) ^ seed) % 1000
    tone = 0.62 + 0.38 * (key / 1000.0)
    gx = np.abs(fx - np.round(fx)) ; gy = np.abs(fy * 2.0 - np.round(fy * 2.0))
    seam = np.clip(np.minimum(gx * 6, gy * 6) , 0, 1) ** 0.5
    floor = np.array([0.24, 0.26, 0.30], np.float32)[None, None, :] * 255 * (tone * seam * (0.7 + 0.5 * fl))[..., None]
    floor *= (0.55 + 0.45 * t)[..., None] ** 0.5
    isfloor = (yy >= floor_y)[..., None]
    img = np.where(isfloor, floor, wall)
    # 바닥 경계에 어두운 걸레받이
    base = np.clip(1 - np.abs(yy - floor_y) / (14 * s), 0, 1)[..., None]
    img = img * (1 - 0.6 * base)
    # 아치 창 두 개
    for cx in (int(w * 0.20), int(w * 0.50)):
        ww, wh = int(150 * s), int(430 * s)
        top = int(h * 0.12)
        win = Image.new("L", (w, h), 0)
        d = ImageDraw.Draw(win)
        d.rectangle([cx - ww // 2, top + ww // 2, cx + ww // 2, top + wh], fill=255)
        d.pieslice([cx - ww // 2, top, cx + ww // 2, top + ww], 180, 360, fill=255)
        wm = np.asarray(win, np.float32) / 255
        frame = np.asarray(win.filter(ImageFilter.MaxFilter(int(14 * s) // 2 * 2 + 1)), np.float32) / 255
        sky = np.array([40, 56, 86], np.float32)[None, None, :] * (0.7 + 0.8 * (1 - (yy - top) / wh).clip(0, 1))[..., None]
        sky = sky + 70 * fbm(h, w, seed + cx, 4, 6)[..., None] * np.array([0.7, 0.85, 1.0], np.float32)
        img = img * (1 - 0.7 * frame[..., None]) + 0 * frame[..., None]
        img = img * (1 - wm[..., None]) + sky * wm[..., None]
        # 창살
        d2 = Image.new("L", (w, h), 0)
        dd = ImageDraw.Draw(d2)
        dd.rectangle([cx - 3, top, cx + 3, top + wh], fill=255)
        for k in range(1, 4): dd.rectangle([cx - ww // 2, top + wh * k // 4 - 3, cx + ww // 2, top + wh * k // 4 + 3], fill=255)
        bars = np.asarray(d2, np.float32) / 255 * wm
        img = img * (1 - bars[..., None]) + np.array([22, 24, 28], np.float32) * bars[..., None]
        # 바닥에 떨어지는 달빛 웅덩이
        img += radial_glow(h, w, cx + 40 * s, floor_y + (h - floor_y) * 0.5, 330 * s, (46, 62, 90), 2.2) * np.clip((yy - floor_y) / 40, 0, 1)[..., None]
        # 창 둘레 빛 번짐
        img += radial_glow(h, w, cx, top + wh * 0.4, 320 * s, (28, 38, 60), 1.6)
    # 횃불 두 개(따뜻한 주황 — 이 장면의 유일한 따뜻한 색)
    for tx, ty in ((int(w * 0.35), int(h * 0.47)), (int(w * 0.65), int(h * 0.47))):
        bracket = Image.new("L", (w, h), 0)
        bd = ImageDraw.Draw(bracket)
        bd.rectangle([tx - int(7 * s), ty, tx + int(7 * s), ty + int(120 * s)], fill=255)
        bd.polygon([(tx - int(26 * s), ty - int(8 * s)), (tx + int(26 * s), ty - int(8 * s)), (tx + int(14 * s), ty + int(24 * s)), (tx - int(14 * s), ty + int(24 * s))], fill=255)
        bm = np.asarray(bracket, np.float32) / 255
        img = img * (1 - bm[..., None]) + np.array([30, 28, 28], np.float32) * bm[..., None]
        img += radial_glow(h, w, tx, ty - 30 * s, 520 * s, (150, 88, 36), 2.4)
        fire = Image.new("L", (w, h), 0)
        fd = ImageDraw.Draw(fire)
        fd.ellipse([tx - int(16 * s), ty - int(62 * s), tx + int(16 * s), ty - int(6 * s)], fill=255)
        fm = np.asarray(fire.filter(ImageFilter.GaussianBlur(5 * s)), np.float32)[..., None] / 255
        img = img * (1 - fm) + np.array([255, 190, 90], np.float32) * fm
    # 낡은 깃발 두 장(색 바랜 붉은 천)
    for bx in (int(w * 0.07), int(w * 0.93)):
        bw_, bh_ = int(110 * s), int(380 * s)
        flag = Image.new("L", (w, h), 0)
        fd = ImageDraw.Draw(flag)
        top = int(h * 0.04)
        fd.polygon([(bx - bw_ // 2, top), (bx + bw_ // 2, top), (bx + bw_ // 2, top + bh_), (bx, top + bh_ - int(60 * s)), (bx - bw_ // 2, top + bh_)], fill=255)
        fm = np.asarray(flag, np.float32) / 255
        cloth = np.array([86, 30, 34], np.float32)[None, None, :] * (0.6 + 0.7 * fbm(h, w, seed + bx, 4, 12))[..., None]
        fold = (0.8 + 0.2 * np.sin((xx - bx) / (9 * s)))[..., None]
        img = img * (1 - fm[..., None]) + cloth * fold * fm[..., None]
        rod = np.zeros((h, w), np.float32); rod[top - int(8 * s):top + int(4 * s), bx - bw_ // 2 - int(12 * s):bx + bw_ // 2 + int(12 * s)] = 1
        img = img * (1 - rod[..., None]) + np.array([40, 42, 48], np.float32) * rod[..., None]
        sh = np.asarray(flag.filter(ImageFilter.GaussianBlur(8 * s)), np.float32)[..., None] / 255
        img = img * (1 - 0.3 * np.roll(sh, int(10 * s), 1))
    # 낮은 안개(바닥 위)
    fog = fbm(h, w, seed + 77, 4, 4)
    fogband = np.clip(1 - np.abs(yy - (floor_y + 20 * s)) / (200 * s), 0, 1) ** 1.5
    img += (fog * fogband)[..., None] * np.array([62, 70, 84], np.float32) * 0.9
    img = vignette(img, 0.7, 2.0)
    # 글자 올라갈 왼쪽 위·오른쪽 메뉴 자리를 살짝 눌러 대비 확보
    img *= (1 - 0.18 * np.clip(1 - (xx / w) / 0.55, 0, 1) * np.clip(1 - (yy / h) / 0.35, 0, 1))[..., None]
    return to_img(img, "RGB")


if __name__ == "__main__":
    out = {}
    for st in ("normal", "hover", "pressed"):
        out[f"btn_{st}.png"] = button(st)
    out["menu_frame.png"] = menu_frame()
    out["title_plate.png"] = title_plate()
    bg = background(1920, 1080)
    out["bg_1920x1080.png"] = bg
    out["bg_2560x1440.png"] = background(2560, 1440)
    for n, im in out.items():
        im.save(os.path.join(OUT, n))
    sheet([(n, im) for n, im in out.items()], os.path.expanduser("~/grd/GRD_title_castle/sheet_2.png"), cols=3, cell=640)
    print("ok", OUT)
