"""선술집 톤 게임 UI 그림 한 벌(blender 세션 2026-10-06, 사장님 「조합 검색 서랍·하단 UI 바도 선술집으로」). PIL 2D. 글자 없음.
/usr/bin/python3 Tools/blender/gen_tavern_ui.py [~/GRD_tavern_ui]   (gen_settings_panel.py의 나무·쇠 도우미를 가져다 쓴다)
산출·9-slice 테두리는 README.md 표. preview_bottom_bar.png · preview_drawer.png (1920×1080)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.argv = [sys.argv[0]] + sys.argv[1:]
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import gen_settings_panel as G
from gen_settings_panel import wood, add_planks_gaps, disc, iron_plate, rounded_mask, bevel, fnoise

OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_tavern_ui'); os.makedirs(OUT, exist_ok=True)
G.OUT = OUT
U8 = lambda a: (np.clip(a, 0, 1) * 255).astype(np.uint8)

def board(W, H, B, rim=(.26, .14, .07), inner=(.20, .105, .055), planks=0, iron=None, radius=40, M=4, nails=True, leather=False, glow=0.0, shade=1.0):
    """나무 액자 판: 바깥 B px = 테두리(위·아래 가로 결, 양옆 세로 결), 안쪽 = 널판/가죽. iron=모서리 쇠장식 크기(px) 또는 None."""
    if leather:
        n = fnoise(H, W, (H // 6, W // 6), 4); n2 = fnoise(H, W, (H // 2, W // 2), 3)
        body = np.dstack([inner[c] * (.8 + .5 * (n - .5) + .25 * (n2 - .5)) * 2.0 for c in range(3)])
    else:
        body = wood(H, W, True, base=inner, planks=max(planks, 1))
        if planks > 1: body = add_planks_gaps(body, planks, True)
    rh = wood(H, W, False, base=rim); rv = wood(H, W, True, base=rim)
    yy, xx = np.mgrid[0:H, 0:W]
    top = (yy < B) & (yy <= xx) & (yy <= W - 1 - xx); bot = (yy >= H - B) & ((H - 1 - yy) <= xx) & ((H - 1 - yy) <= W - 1 - xx)
    inrim = (xx < B) | (xx >= W - B) | (yy < B) | (yy >= H - B)
    a = np.where(inrim[..., None], np.where((top | bot)[..., None], rh, rv), body)
    din = np.minimum.reduce([xx - B, W - B - 1 - xx, yy - B, H - B - 1 - yy])
    for dd, f in ((0, .28), (1, .45), (2, .7), (-1, 1.22), (-2, 1.12)):
        a = np.where((din == dd)[..., None], np.clip(a * f, 0, 1), a)
    if shade < 1:                                   # 안쪽 가장자리 어둡게(안으로 갈수록 깊은 느낌)
        d2 = np.clip(din / max(8, B), 0, 1); a = np.where((din >= 0)[..., None], a * (shade + (1 - shade) * d2[..., None]), a)
    img = Image.fromarray(U8(a)).convert('RGBA'); d = ImageDraw.Draw(img)
    if nails and B >= 24:
        r = max(3, B // 16)
        for t in np.linspace(B * 1.0, W - B * 1.0, max(2, W // 110)):
            disc(d, t, B * .42, r, (30, 28, 30, 255), (120, 118, 124, 255)); disc(d, t, H - B * .42, r, (30, 28, 30, 255), (120, 118, 124, 255))
        for t in np.linspace(B * 1.0, H - B * 1.0, max(2, H // 110)):
            disc(d, B * .42, t, r, (30, 28, 30, 255), (120, 118, 124, 255)); disc(d, W - B * .42, t, r, (30, 28, 30, 255), (120, 118, 124, 255))
    if iron:
        pl = iron_plate(iron, 4 if iron >= 56 else 1)
        for (ox, oy) in ((M, M), (W - M - iron, M), (M, H - M - iron), (W - M - iron, H - M - iron)): img.alpha_composite(pl, (ox, oy))
    mask = rounded_mask(W - 2 * M, H - 2 * M, radius, .8); full = np.zeros((H, W), np.float32); full[M:H - M, M:W - M] = mask
    rgb = bevel(np.asarray(img, np.float32)[..., :3] / 255, full, max(3, B // 18))
    rgb = rgb * (.88 + .2 * fnoise(H, W, (5, 5), 3)[..., None])
    if glow:                                        # 호버: 안쪽에서 번지는 따뜻한 불빛
        g = np.clip(din / max(10, B * 1.4), 0, 1); g = (1 - g) * (din >= -3)
        rgb = np.clip(rgb + glow * g[..., None] * np.array([1.0, .55, .18]) * 0.55, 0, 1)
    out = np.dstack([rgb, full]); return Image.fromarray(U8(out), 'RGBA') if False else Image.fromarray(U8(out))

def save(img, name): img.save(f'{OUT}/{name}.png'); return img

# ① 하단 바 바탕: 가로 널판 띠 + 위 쇠띠(리벳)
def bottom_bar():
    W, H = 1024, 256; B_top = 56
    a = wood(H, W, False, base=(.19, .10, .05), planks=4); a = add_planks_gaps(a, 4, False)
    img = Image.fromarray(U8(a)).convert('RGBA'); d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 34), fill=(30, 30, 35, 255)); d.rectangle((0, 3, W, 8), fill=(98, 98, 106, 255)); d.rectangle((0, 28, W, 34), fill=(12, 12, 14, 255))
    d.rectangle((0, 34, W, 42), fill=(70, 38, 18, 255))                      # 쇠띠 아래 받침 나무 턱
    for x in np.arange(40, W - 20, 96): disc(d, x, 18, 8, (84, 84, 92, 255), (205, 205, 213, 255))
    for x in np.arange(60, W - 20, 192):
        for y in (H * .33, H * .66): disc(d, x, y, 5, (30, 28, 30, 255), (120, 118, 124, 255))
    arr = np.asarray(img, np.float32)[..., :3] / 255
    arr = arr * (.86 + .2 * fnoise(H, W, (4, 8), 3)[..., None])
    arr[-24:] *= np.linspace(1, .55, 24)[:, None, None]                     # 아래쪽 어둡게
    full = np.ones((H, W), np.float32)
    save(Image.fromarray(U8(np.dstack([arr, full]))), 'bar_bottom_9slice')
    # 가로 반복 타일(좌우 거울 이음 → 이음새 없음)
    half = img.crop((0, 0, 256, 256)); t = Image.new('RGBA', (512, 256)); t.paste(half, (0, 0)); t.paste(half.transpose(Image.FLIP_LEFT_RIGHT), (256, 0))
    save(t, 'bar_bottom_tile512')

# ② 칸 판
def cells():
    save(board(256, 256, 40, rim=(.27, .15, .07), inner=(.08, .045, .028), leather=True, iron=34, radius=26, shade=.55), 'cell_big_9slice')
    save(board(128, 128, 18, rim=(.27, .15, .07), inner=(.08, .045, .028), leather=True, iron=0, radius=12, M=2, nails=False, shade=.6), 'cell_small_9slice')
    # 작은 칸 모서리에 아주 작은 쇠 리벳(뭉개지지 않는 크기)
    im = Image.open(f'{OUT}/cell_small_9slice.png'); d = ImageDraw.Draw(im)
    for (x, y) in ((9, 9), (119, 9), (9, 119), (119, 119)): disc(d, x, y, 3.2, (82, 82, 92, 255), (200, 200, 208, 255))
    im.save(f'{OUT}/cell_small_9slice.png')

# ③ 명령 카드 단추 3장
def buttons():
    for nm, kw in (('normal', dict(rim=(.30, .17, .085), inner=(.19, .10, .05))), ('hover', dict(rim=(.36, .21, .10), inner=(.24, .13, .06), glow=1.0)), ('pressed', dict(rim=(.18, .10, .05), inner=(.10, .055, .03), shade=.5))):
        b = board(192, 192, 24, nails=False, radius=22, M=3, planks=0, iron=0, **kw)
        d = ImageDraw.Draw(b)
        for (x, y) in ((12, 12), (180, 12), (12, 180), (180, 180)): disc(d, x, y, 4, (74, 74, 82, 255), (200, 200, 208, 255))
        if nm == 'pressed':
            arr = np.asarray(b, np.float32); arr[..., :3] *= .82; b = Image.fromarray(arr.astype(np.uint8))
        save(b, f'btn_{nm}_9slice')

# ④ 서랍: 세로 게시판 + 명패 + 꼬리표 + 양피지 줄
def drawer():
    save(board(512, 1024, 112, rim=(.26, .14, .07), inner=(.19, .10, .055), planks=4, iron=108, radius=54, M=8), 'drawer_9slice')
    G.build_nameplate(); os.replace(f'{OUT}/nameplate.png', f'{OUT}/drawer_nameplate.png')
    for nm, bright in (('normal', 0), ('selected', 1)):
        w, h = 160, 56
        t = board(w, h, 12, rim=(.24, .13, .06) if not bright else (.46, .27, .12), inner=(.15, .08, .04) if not bright else (.34, .20, .09), nails=False, radius=14, M=2, glow=bright * .8)
        d = ImageDraw.Draw(t); disc(d, 14, h / 2, 5, (60, 60, 66, 255), (200, 200, 208, 255))                 # 끈 구멍 쇠
        d.line((14, h / 2, 2, h / 2 - 12), fill=(150, 112, 60, 255), width=2)
        save(t, f'chip_{nm}_9slice')
    # 양피지 결과 줄
    w, h = 512, 96; n = fnoise(h, w, (h // 5, w // 5), 4); n2 = fnoise(h, w, (3, 12), 3)
    base = np.dstack([.80 * (.86 + .3 * (n - .5)), .70 * (.86 + .3 * (n - .5)), .50 * (.86 + .3 * (n - .5))])
    m = rounded_mask(w - 8, h - 8, 12, 2.2); full = np.zeros((h, w), np.float32); full[4:h - 4, 4:w - 4] = m
    edge = np.clip(1 - np.asarray(Image.fromarray(U8(full)).filter(ImageFilter.GaussianBlur(7)), np.float32) / 255, 0, 1) * full
    base = base * (1 - .55 * edge[..., None] * np.array([.8, 1.0, 1.4])) * (.94 + .1 * n2[..., None])
    rgb = bevel(np.clip(base, 0, 1), full, 4, .2, .35)
    save(Image.fromarray(U8(np.dstack([rgb, full]))), 'row_parchment_9slice')

# ⑤ 상단 자원 칸
def resource():
    save(board(192, 72, 18, rim=(.28, .16, .08), inner=(.12, .065, .035), nails=False, radius=16, M=2, shade=.6), 'resource_plate_9slice')
    im = Image.open(f'{OUT}/resource_plate_9slice.png'); d = ImageDraw.Draw(im)
    for (x, y) in ((9, 9), (183, 9), (9, 63), (183, 63)): disc(d, x, y, 3.4, (82, 82, 92, 255), (200, 200, 208, 255))
    im.save(f'{OUT}/resource_plate_9slice.png')

def slice9(src, w, h, bl, bt=None, br=None, bb=None):
    bt = bl if bt is None else bt; br = bl if br is None else br; bb = bt if bb is None else bb
    sw, sh = src.size; out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    xs = [(0, bl, 0, bl), (bl, sw - br, bl, w - br), (sw - br, sw, w - br, w)]; ys = [(0, bt, 0, bt), (bt, sh - bb, bt, h - bb), (sh - bb, sh, h - bb, h)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if dx1 > dx0 and dy1 > dy0: out.alpha_composite(src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS), (dx0, dy0))
    return out
L = lambda n: Image.open(f'{OUT}/{n}.png').convert('RGBA')

def previews():
    bgp = os.path.expanduser('~/GRD_lobby_art/bg_1920x1080.png')
    base = Image.open(bgp).convert('RGBA') if os.path.exists(bgp) else Image.new('RGBA', (1920, 1080), (30, 20, 16, 255))
    # --- 하단 바
    im = base.copy(); bar = slice9(L('bar_bottom_9slice'), 1920, 250, 0, 44, 0, 8); im.alpha_composite(bar, (0, 830))
    cb = L('cell_big_9slice'); cs = L('cell_small_9slice'); bt = [L('btn_normal_9slice'), L('btn_hover_9slice'), L('btn_pressed_9slice')]
    im.alpha_composite(slice9(cb, 300, 190, 40), (24, 872))                 # 미니맵
    im.alpha_composite(slice9(cb, 190, 190, 40), (340, 872))                # 초상
    im.alpha_composite(slice9(cb, 560, 190, 40), (546, 872))                # 정보
    for r in range(3):
        for c in range(4):
            x, y = 1130 + c * 100, 868 + r * 68; bi = bt[(r * 4 + c) % 3 if (r, c) in ((0, 1), (1, 2)) else 0]
            im.alpha_composite(slice9(bi, 94, 62, 24), (x, y))
    im.alpha_composite(slice9(cs, 130, 100, 18), (1560, 872)); im.alpha_composite(slice9(cs, 130, 100, 18), (1700, 872))
    rp = L('resource_plate_9slice')
    for i in range(3): im.alpha_composite(slice9(rp, 170, 56, 18), (1400 + i * 180, 10))
    im.convert('RGB').save(f'{OUT}/preview_bottom_bar.png')
    # --- 서랍
    im = Image.alpha_composite(base, Image.new('RGBA', (1920, 1080), (0, 0, 0, 110)))
    dw, dh = 620, 960; x0, y0 = 1250, 60
    sh = Image.new('RGBA', (1920, 1080), (0, 0, 0, 0)); sh.paste((0, 0, 0, 190), (x0 + 12, y0 + 20, x0 + dw + 12, y0 + dh + 20)); im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(18)))
    im.alpha_composite(slice9(L('drawer_9slice'), dw, dh, 112), (x0, y0))
    im.alpha_composite(L('drawer_nameplate').resize((320, 100), Image.LANCZOS), (x0 + (dw - 320) // 2, y0 - 36))
    for i in range(6): im.alpha_composite(slice9(L('chip_selected_9slice' if i == 1 else 'chip_normal_9slice'), 74, 48, 12), (x0 + 130 + i * 62 - (0), y0 + 112)) if False else None
    for i in range(5): im.alpha_composite(slice9(L('chip_selected_9slice' if i == 1 else 'chip_normal_9slice'), 84, 44, 12), (x0 + 120 + i * 86, y0 + 118))
    for i in range(7): im.alpha_composite(slice9(L('row_parchment_9slice'), 400, 84, 24), (x0 + 110, y0 + 190 + i * 92))
    im.convert('RGB').save(f'{OUT}/preview_drawer.png')

bottom_bar(); cells(); buttons(); drawer(); resource(); previews()
open(f'{OUT}/README.md', 'w').write('''# 선술집 톤 UI 그림 (blender 10-06) — 정본 Tools/blender/gen_tavern_ui.py · 글자 없음 · 전부 투명 배경 PNG
| 파일 | 크기 | 9-slice 테두리(좌,위,우,아래) px | 쓰임 |
|---|---|---|---|
| bar_bottom_9slice | 1024×256 | 0, 44, 0, 8 (가로로만 늘림) | 하단 바 바탕: 가로 널판 띠 + 위 쇠띠·리벳 |
| bar_bottom_tile512 | 512×256 | — (가로 반복, 좌우 거울 이음) | 위 대신 쓰는 타일 |
| cell_big_9slice | 256² | 40 ×4 | 미니맵·초상·정보 칸(안쪽 어두운 가죽, 모서리 쇠장식) |
| cell_small_9slice | 128² | 18 ×4 | 작은 칸(안쪽 가죽, 가는 나무 액자 + 모서리 작은 리벳) |
| btn_normal / btn_hover / btn_pressed _9slice | 192² | 24 ×4 | 명령 카드 단추(나무 패, 호버=안쪽 따뜻한 불빛, 눌림=어둡게). 100×80 안팎까지 줄여도 모서리 리벳이 선명 |
| drawer_9slice | 512×1024 | 112 ×4 | 조합 검색 서랍 판(설정 판과 같은 결, 널판 4) |
| drawer_nameplate | 512×160 | — | 서랍 위 명패(글자 자리) |
| chip_normal / chip_selected _9slice | 160×56 | 12 ×4 | 등급 칩 나무 꼬리표(선택=밝은 나무+불빛) |
| row_parchment_9slice | 512×96 | 24 ×4 | 결과 줄 양피지 쪽지(가장자리 그을림) |
| resource_plate_9slice | 192×72 | 18 ×4 | 상단 자원 칸 작은 나무 명패 |
preview_bottom_bar.png · preview_drawer.png = 로비 배경 위 1920×1080 합성(9-slice 실제 계산).
''')
print('DONE', OUT)
