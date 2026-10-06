"""첫 화면 설정 창 판 — 선술집 나무 간판/게시판(blender 세션 2026-10-06, 사장님 「너무 각지다」). PIL·numpy 2D 그림(렌더 루프를 안 끊게 가볍게).
/usr/bin/python3 Tools/blender/gen_settings_panel.py [~/GRD_settings_panel]
산출: panel_9slice.png(1024², 투명 배경 · 9-slice 테두리 128px) · nameplate.png(512×160) · slider_groove.png(512×48) · slider_knob_rivet.png / slider_knob_mug.png(64²) · preview.png(1280×720). 글자는 안 굽는다(UI가 얹음).
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_settings_panel'); os.makedirs(OUT, exist_ok=True)
rng = np.random.RandomState(1006)
SLICE = 128

def fnoise(h, w, cells, oct=4):
    """여러 겹 값 소음(0..1). cells=(세로셀, 가로셀)로 결 방향을 늘린다."""
    acc = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(oct):
        ch, cw = max(2, int(cells[0] * 2 ** o)), max(2, int(cells[1] * 2 ** o))
        a = rng.rand(ch, cw).astype(np.float32)
        im = Image.fromarray((a * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        acc += np.asarray(im, np.float32) / 255 * amp; tot += amp; amp *= .5
    return acc / tot

def wood(h, w, vertical=True, base=(.20, .105, .055), planks=1, var=.25):
    """짙은 원목: 긴 결 + 옹이 + 널판별 색 차이. vertical=결이 위아래."""
    if vertical: g = fnoise(h, w, (3, 90)); g2 = fnoise(h, w, (6, 200), 3)
    else: g = fnoise(h, w, (90, 3)); g2 = fnoise(h, w, (200, 6), 3)
    rings = .5 + .5 * np.sin((np.arange(w if vertical else h)[None, :] if vertical else np.arange(h)[:, None]) * .45 + g * 14)
    lum = .55 + .6 * (g - .5) + .22 * (rings - .5) + .18 * (g2 - .5)
    col = np.zeros((h, w, 3), np.float32)
    if planks > 1:
        pw = (w if vertical else h) / planks
        idx = ((np.arange(w) if vertical else np.arange(h)) / pw).astype(int)
        pv = (rng.rand(planks) - .5) * var
        tone = pv[np.clip(idx, 0, planks - 1)]
        lum = lum * (1 + (tone[None, :] if vertical else tone[:, None]))
    for c in range(3): col[..., c] = base[c] * lum * 1.9
    return np.clip(col, 0, 1)

def add_planks_gaps(a, planks, vertical=True):
    h, w = a.shape[:2]; n = w if vertical else h; pw = n / planks
    for k in range(1, planks):
        p = int(round(k * pw))
        for d, f in ((-3, .62), (-2, .38), (-1, .22), (0, .12), (1, .22), (2, .55), (3, .85)):
            q = p + d
            if 0 <= q < n:
                if vertical: a[:, q] *= f
                else: a[q, :] *= f
        q = p + 4
        if q < n:                                                   # 밝은 모서리 한 줄
            if vertical: a[:, q] = np.clip(a[:, q] * 1.18, 0, 1)
            else: a[q, :] = np.clip(a[q, :] * 1.18, 0, 1)
    return a

def disc(d, cx, cy, r, base, hi, ss=1):
    """둥근 리벳/못머리(위쪽 왼쪽 하이라이트 · 아래쪽 어두움)."""
    d.ellipse((cx - r - 1, cy - r - 1, cx + r + 1, cy + r + 1), fill=(10, 8, 7, 200))
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=base)
    d.ellipse((cx - r * .78, cy - r * .78, cx + r * .62, cy + r * .62), fill=tuple(int(c * 1.25) for c in base[:3]) + (255,))
    d.ellipse((cx - r * .55, cy - r * .62, cx - r * .05, cy - r * .12), fill=hi)

def iron_plate(sz, rivets=4):
    S = sz * 2; im = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle((8, 8, S - 8, S - 8), radius=S // 7, fill=(8, 8, 9, 255))
    d.rounded_rectangle((14, 14, S - 14, S - 14), radius=S // 8, fill=(44, 44, 49, 255))
    d.rounded_rectangle((22, 22, S - 24, S - 24), radius=S // 9, fill=(58, 58, 64, 255))
    d.rounded_rectangle((22, 22, S - 24, 40), radius=10, fill=(92, 92, 98, 255))           # 윗 모서리 빛
    arr = np.asarray(im, np.float32)
    nz = (fnoise(S, S, (30, 30), 4) - .5) * 26
    arr[..., :3] = np.clip(arr[..., :3] + nz[..., None], 0, 255)
    im = Image.fromarray(arr.astype(np.uint8)); d = ImageDraw.Draw(im)
    for (fx, fy) in ((.26, .26), (.74, .26), (.26, .74), (.74, .74))[:rivets]:
        disc(d, S * fx, S * fy, S * .075, (82, 82, 90, 255), (190, 190, 198, 255))
    return im.resize((sz, sz), Image.LANCZOS)

def rounded_mask(w, h, r, wear=1.0, seed=1):
    ss = 2; m = Image.new('L', (w * ss, h * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, w * ss - 1, h * ss - 1), radius=r * ss, fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    a = np.asarray(m, np.float32) / 255
    # 가장자리 닳음: 가장자리 거리 안쪽 몇 px를 소음으로 깎는다
    dist = np.asarray(m.filter(ImageFilter.GaussianBlur(5)), np.float32) / 255
    nz = fnoise(h, w, (h // 8, w // 8), 3)
    a = a * np.clip((dist - (1 - nz) * .45 * wear) * 6 + .5, 0, 1)
    return np.clip(a, 0, 1)

def bevel(rgb, a, depth=6, light=.35, dark=.45):
    """알파 모양을 따라 위·왼쪽은 밝게, 아래·오른쪽은 어둡게(깎인 판 느낌) + 안쪽 그림자."""
    m = Image.fromarray((a * 255).astype(np.uint8))
    b1 = np.asarray(m.filter(ImageFilter.GaussianBlur(depth)), np.float32) / 255
    sh = np.roll(np.roll(b1, depth // 2, 0), depth // 2, 1); hl = np.roll(np.roll(b1, -depth // 2, 0), -depth // 2, 1)
    edge = np.clip(1 - b1, 0, 1) * a * 1.4
    rgb = rgb * (1 - dark * np.clip(1 - sh, 0, 1)[..., None] * a[..., None] * 1.0)
    rgb = rgb * (1 + light * np.clip(1 - hl, 0, 1)[..., None] * a[..., None] * .5)
    rgb = rgb * (1 - .35 * np.clip(edge, 0, 1)[..., None])
    return np.clip(rgb, 0, 1)

# ----------------------------------------------------------- ① 9-slice 판
def build_panel():
    S = 1024; M = 8; B = SLICE
    body = wood(S, S, True, planks=7); body = add_planks_gaps(body, 7, True)
    # 테두리 널(바깥 B px): 위·아래는 가로 결, 왼·오른쪽은 세로 결 — 더 밝고 닳은 테 + 안쪽 모서리 홈
    rim_h = wood(S, S, False, base=(.26, .14, .07)); rim_v = wood(S, S, True, base=(.26, .14, .07))
    yy, xx = np.mgrid[0:S, 0:S]
    top = (yy < B) & (yy <= xx) & (yy <= S - 1 - xx); bot = (yy >= S - B) & ((S - 1 - yy) <= xx) & ((S - 1 - yy) <= S - 1 - xx)
    horiz = top | bot
    inrim = (xx < B) | (xx >= S - B) | (yy < B) | (yy >= S - B)
    rim = np.where(horiz[..., None], rim_h, rim_v)
    a = np.where(inrim[..., None], rim, body)
    # 테 안쪽 단 + 홈
    dist_in = np.minimum.reduce([xx - B, S - B - 1 - xx, yy - B, S - B - 1 - yy])
    for dd, f in ((0, .30), (1, .45), (2, .65), (-1, 1.25), (-2, 1.15)):
        mask = (dist_in == dd)
        a = np.where(mask[..., None], a * f if f < 1 else np.clip(a * f, 0, 1), a)
    # 못 자국(중앙 판 위아래 · 좌우 가장자리 안쪽)
    img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).convert('RGBA'); d = ImageDraw.Draw(img)
    pw = (S - 2 * B) / 7
    for k in range(7):
        px = B + (k + .5) * pw
        for py in (B + 26, S - B - 26): disc(d, px - 1, py, 7, (36, 30, 28, 255), (110, 100, 92, 255))
    for py in np.linspace(B + 40, S - B - 40, 7):                    # 테두리 널을 박은 큰 못
        disc(d, 62, py, 8, (30, 28, 30, 255), (120, 118, 124, 255)); disc(d, S - 62, py, 8, (30, 28, 30, 255), (120, 118, 124, 255))
    for px in np.linspace(B + 50, S - B - 50, 7):
        disc(d, px, 62, 8, (30, 28, 30, 255), (120, 118, 124, 255)); disc(d, px, S - 62, 8, (30, 28, 30, 255), (120, 118, 124, 255))
    # 쇠 모서리 장식(리벳 4) — 9-slice 모서리 B×B 안에 들어가게 B−4
    plate = iron_plate(B - 4)
    for (ox, oy) in ((M, M), (S - M - (B - 4), M), (M, S - M - (B - 4)), (S - M - (B - 4), S - M - (B - 4))):
        img.alpha_composite(plate, (ox, oy))
    # 모양: 둥근 모서리 + 닳은 가장자리, 가장자리는 바깥 M px 투명
    mask = rounded_mask(S - 2 * M, S - 2 * M, 54, 1.0); full = np.zeros((S, S), np.float32); full[M:S - M, M:S - M] = mask
    rgb = np.asarray(img, np.float32)[..., :3] / 255
    rgb = bevel(rgb, full, 7)
    # 판 때(먼지) 얼룩
    dirt = fnoise(S, S, (6, 6), 4); rgb = rgb * (.86 + .22 * dirt[..., None])
    out = np.dstack([np.clip(rgb, 0, 1), full]); Image.fromarray((out * 255).astype(np.uint8), 'RGBA').save(OUT + '/panel_9slice.png')

# ----------------------------------------------------------- ② 명패
def build_nameplate():
    w, h = 512, 160
    a = wood(h, w, False, base=(.30, .17, .085)) ; img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).convert('RGBA')
    # 위아래 쇠띠 + 양 끝 리벳
    d = ImageDraw.Draw(img)
    for (y0, y1) in ((10, 28), (h - 28, h - 10)):
        d.rectangle((0, y0, w, y1), fill=(40, 40, 46, 255)); d.line((0, y0 + 2, w, y0 + 2), fill=(96, 96, 104, 255), width=2); d.line((0, y1, w, y1), fill=(14, 14, 16, 255), width=2)
    for cx in (34, w - 34):
        for cy in (19, h - 19): disc(d, cx, cy, 6, (84, 84, 92, 255), (200, 200, 208, 255))
    mask = rounded_mask(w - 12, h - 12, 26, .8); full = np.zeros((h, w), np.float32); full[6:h - 6, 6:w - 6] = mask
    rgb = bevel(np.asarray(img, np.float32)[..., :3] / 255, full, 5)
    # 위 가운데에 걸 고리 두 개(사슬 자리) — 위쪽 가장자리에 쇠 고리
    out = Image.fromarray((np.dstack([rgb, full]) * 255).astype(np.uint8), 'RGBA'); d = ImageDraw.Draw(out)
    for cx in (96, w - 96):
        d.ellipse((cx - 11, 0, cx + 11, 22), outline=(60, 60, 66, 255), width=5); d.ellipse((cx - 11, 0, cx + 11, 22), outline=(120, 120, 128, 255), width=1)
    out.save(OUT + '/nameplate.png')

# ----------------------------------------------------------- ③ 슬라이더
def build_slider():
    w, h = 512, 48; ss = 3
    g = Image.new('RGBA', (w * ss, h * ss), (0, 0, 0, 0)); d = ImageDraw.Draw(g)
    d.rounded_rectangle((0, 8 * ss, w * ss - 1, h * ss - 8 * ss), radius=14 * ss, fill=(10, 6, 4, 255))                 # 파낸 홈
    d.rounded_rectangle((3 * ss, 11 * ss, w * ss - 4 * ss, h * ss - 11 * ss), radius=11 * ss, fill=(30, 17, 9, 255))
    d.rounded_rectangle((3 * ss, 11 * ss, w * ss - 4 * ss, 20 * ss), radius=9 * ss, fill=(14, 8, 5, 255))              # 위쪽 안쪽 그림자
    d.line((8 * ss, (h - 10) * ss, (w - 8) * ss, (h - 10) * ss), fill=(88, 54, 28, 255), width=2 * ss)                  # 아랫 모서리 빛
    g.resize((w, h), Image.LANCZOS).save(OUT + '/slider_groove.png')
    # 채워진 쪽(밝은 주황 나무 속살) — UI가 가로로 채울 때 쓰는 같은 크기 판
    f = Image.new('RGBA', (w * ss, h * ss), (0, 0, 0, 0)); d = ImageDraw.Draw(f)
    d.rounded_rectangle((3 * ss, 11 * ss, w * ss - 4 * ss, h * ss - 11 * ss), radius=11 * ss, fill=(168, 96, 34, 255))
    d.rounded_rectangle((3 * ss, 11 * ss, w * ss - 4 * ss, 20 * ss), radius=9 * ss, fill=(214, 138, 56, 255))
    f.resize((w, h), Image.LANCZOS).save(OUT + '/slider_fill.png')
    # 손잡이: 쇠 리벳
    S = 64 * 4; k = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(k)
    disc(d, S / 2, S / 2, S * .40, (74, 74, 82, 255), (210, 210, 218, 255))
    d.ellipse((S * .42, S * .42, S * .58, S * .58), fill=(30, 30, 34, 255))
    k.resize((64, 64), Image.LANCZOS).save(OUT + '/slider_knob_rivet.png')
    # 손잡이: 맥주잔
    m = Image.new('RGBA', (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(m)
    d.arc((S * .62, S * .30, S * .98, S * .72), -80, 80, fill=(120, 70, 28, 255), width=int(S * .08))                  # 손잡이
    d.polygon([(S * .16, S * .26), (S * .70, S * .26), (S * .64, S * .90), (S * .22, S * .90)], fill=(150, 88, 34, 255))
    d.polygon([(S * .16, S * .26), (S * .30, S * .26), (S * .34, S * .90), (S * .22, S * .90)], fill=(196, 122, 50, 255))
    d.rectangle((S * .17, S * .46, S * .69, S * .52), fill=(60, 36, 16, 255)); d.rectangle((S * .20, S * .72, S * .66, S * .78), fill=(60, 36, 16, 255))   # 쇠테
    for cx, cy in ((.26, .22), (.40, .17), (.54, .21), (.64, .24)): d.ellipse((S * (cx - .13), S * (cy - .09), S * (cx + .13), S * (cy + .09)), fill=(246, 240, 224, 255))      # 거품
    m.resize((64, 64), Image.LANCZOS).save(OUT + '/slider_knob_mug.png')

# ----------------------------------------------------------- ④ 미리보기 (9-slice를 실제로 늘려서)
def slice9(src, w, h, b=SLICE):
    s = src.size[0]; out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    xs = [(0, b, 0, b), (b, s - b, b, w - b), (s - b, s, w - b, w)]
    for (sx0, sx1, dx0, dx1) in xs:
        for (sy0, sy1, dy0, dy1) in xs:
            piece = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS); out.alpha_composite(piece, (dx0, dy0))
    return out

def build_preview():
    bgp = os.path.expanduser('~/GRD_lobby_art/bg_1920x1080.png')
    bg = Image.open(bgp).convert('RGBA').resize((1280, 720), Image.LANCZOS) if os.path.exists(bgp) else Image.new('RGBA', (1280, 720), (40, 24, 20, 255))
    bg = Image.alpha_composite(bg, Image.new('RGBA', (1280, 720), (0, 0, 0, 120)))
    panel = Image.open(OUT + '/panel_9slice.png'); pw_, ph_ = 560, 480
    P = slice9(panel, pw_, ph_); x0, y0 = (1280 - pw_) // 2, (720 - ph_) // 2 + 14
    sh = Image.new('RGBA', (1280, 720), (0, 0, 0, 0)); sh.paste((0, 0, 0, 180), (x0 + 10, y0 + 18, x0 + pw_ + 10, y0 + ph_ + 18)); bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(16)))
    bg.alpha_composite(P, (x0, y0))
    npl = Image.open(OUT + '/nameplate.png').resize((300, 94), Image.LANCZOS); bg.alpha_composite(npl, ((1280 - 300) // 2, y0 - 48))
    d = ImageDraw.Draw(bg)
    gr = Image.open(OUT + '/slider_groove.png'); fl = Image.open(OUT + '/slider_fill.png'); kn = Image.open(OUT + '/slider_knob_mug.png'); kr = Image.open(OUT + '/slider_knob_rivet.png')
    for i, (frac, knob) in enumerate(((.7, kn), (.4, kr), (.9, kn))):
        sy = y0 + 150 + i * 92; sx = (1280 - 360) // 2
        g = gr.resize((360, 34), Image.LANCZOS); bg.alpha_composite(g, (sx, sy))
        f = fl.resize((360, 34), Image.LANCZOS).crop((0, 0, int(360 * frac), 34)); bg.alpha_composite(f, (sx, sy))
        bg.alpha_composite(knob, (sx + int(360 * frac) - 32, sy - 15))
    bg.convert('RGB').save(OUT + '/preview.png')

build_panel(); build_nameplate(); build_slider(); build_preview(); print('DONE', OUT)
