#!/usr/bin/env python3
"""UI 근사 그림 생성기 — 사장님 인게임 사진(Docs/reference/ui/원랜디_인게임_01.png) 모양을 직접 그린다.

정품 워크3 UI 그림은 이 PC에 없어서(Docs/UI_ORIGINAL_STYLE.md ④) **같은 배치·색·두께를 근사**한 그림을 먼저 만든다.
생성물은 전부 이 스크립트가 그린 것(원작 파일 0) — 정품이 생기면 같은 이름으로 바꿔 끼우는 슬롯(UiSkin)이다.

실행: python3 Tools/ui/gen_ui_skin.py [출력폴더]   (Pillow·numpy 필요: python3 -m venv v && v/bin/pip install pillow numpy)
기본 출력: Docs/ui_mockups/skin_preview/ (미리보기). 게임에 넣을 때는 출력 폴더를 Assets/Resources/UI/Skin/ 로 준다.
"""
import os, sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..', 'Docs', 'ui_mockups', 'skin_preview')
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(20261003)
random.seed(20261003)

# 사진에서 읽은 색(근사)
GOLD = (201, 162, 74); GOLD_HI = (243, 210, 122); GOLD_DK = (110, 82, 30)
NAVY = (14, 22, 52); NAVY_HI = (28, 44, 96)
STONE_A = (112, 114, 120); STONE_B = (88, 90, 96); STONE_DK = (46, 48, 54)
BLACK = (0, 0, 0)


def noise(w, h, amp, seed=0):
    r = np.random.default_rng(seed)
    n = r.normal(0, amp, (h, w))
    return n


def stone_tile(w=128, h=128, seed=1):
    """회색 돌벽 — 큰 돌 + 어두운 이음매 + 잡티."""
    img = np.zeros((h, w, 3), dtype=np.float32)
    base = np.array(STONE_A, dtype=np.float32)
    img[:] = base
    r = np.random.default_rng(seed)
    # 돌 블록 이음매
    im = Image.new('RGB', (w, h), STONE_A)
    d = ImageDraw.Draw(im)
    y = 0
    row = 0
    while y < h:
        bh = int(r.integers(26, 38))
        x = -int(r.integers(0, 40)) if row % 2 else 0
        while x < w:
            bw = int(r.integers(40, 64))
            shade = int(r.integers(-14, 14))
            d.rectangle([x, y, x + bw - 2, y + bh - 2], fill=tuple(max(0, min(255, c + shade)) for c in STONE_A))
            d.rectangle([x, y, x + bw - 2, y + bh - 2], outline=STONE_DK)
            x += bw
        y += bh
        row += 1
    arr = np.asarray(im, dtype=np.float32)
    arr += noise(w, h, 7, seed)[..., None]
    arr = np.clip(arr, 0, 255).astype(np.uint8)
    out = Image.fromarray(arr, 'RGB').filter(ImageFilter.GaussianBlur(0.6))
    return out


def round_mask(img, radius, ss_=4):
    """이미지 알파를 둥근 사각형으로 깎는다(4배 확대 마스크 → 축소로 부드러운 가장자리). 사장님 10-03 「UI가 너무 각져 있다」."""
    w, h = img.size
    m = Image.new('L', (w * ss_, h * ss_), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, w * ss_ - 1, h * ss_ - 1], radius=radius * ss_, fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    out = img.convert('RGBA')
    a = ImageChops.multiply(out.getchannel('A'), m)
    out.putalpha(a)
    return out


def rounded_panel(w, h, radius, fill, lines, ss_=4):
    """둥근 칸: fill은 RGBA 이미지(w×h) 또는 색, lines는 [(inset, 두께, 색), …] 바깥부터 — 금테를 둥글게 그린다."""
    base = fill if isinstance(fill, Image.Image) else Image.new('RGBA', (w, h), fill)
    base = round_mask(base.convert('RGBA'), radius)
    big = base.resize((w * ss_, h * ss_), Image.LANCZOS)
    d = ImageDraw.Draw(big)
    for inset, width, color in lines:
        for k in range(width):
            o = (inset + k) * ss_
            d.rounded_rectangle([o, o, w * ss_ - 1 - o, h * ss_ - 1 - o], radius=max(1, (radius - inset - k)) * ss_, outline=color, width=ss_)
    return big.resize((w, h), Image.LANCZOS)


def save(img, name):
    img.save(os.path.join(OUT, name))
    print('  ', name, img.size)


def nine_slice_frame(size, border, inner=None, stone=True, gold_line=2):
    """돌 테두리(바깥) + 금 안선 + 속 채움. 속이 None이면 투명."""
    w = h = size
    base = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    if stone:
        st = stone_tile(w, h, seed=size).convert('RGBA')
        mask = Image.new('L', (w, h), 0)
        md = ImageDraw.Draw(mask)
        md.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, fill=255)
        md.rounded_rectangle([border, border, w - 1 - border, h - 1 - border], radius=3, fill=0)
        base.paste(st, (0, 0), mask)
        d = ImageDraw.Draw(base)
        d.rounded_rectangle([0, 0, w - 1, h - 1], radius=6, outline=STONE_DK)
    d = ImageDraw.Draw(base)
    if inner is not None:
        d.rectangle([border, border, w - 1 - border, h - 1 - border], fill=inner + (255,))
    for i in range(gold_line):
        d.rectangle([border + i, border + i, w - 1 - border - i, h - 1 - border - i], outline=GOLD if i else GOLD_HI)
    return base


def navy_fill(w, h, seed=3):
    arr = np.zeros((h, w, 3), dtype=np.float32)
    ys = np.linspace(0, 1, h)[:, None]
    for c in range(3):
        arr[..., c] = NAVY[c] + (NAVY_HI[c] - NAVY[c]) * (1 - ys) * 0.5
    arr += noise(w, h, 4, seed)[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), 'RGB')


def gen_stone_tile():
    save(stone_tile(256, 256, 11), 'stone_tile.png')


def gen_console_frame():
    # 콘솔 칸 틀: 어두운 속 + 돌 테두리(칸 감싸는 용) 9-slice 96px, border 20
    img = nine_slice_frame(96, 20, inner=(6, 6, 8), stone=True, gold_line=2)
    save(round_mask(img, 14), 'console_cell_frame_9s.png')


def gen_dialog_panel():
    # 모드 선택 대화상자·멀티보드: 돌 테 + 금 안선 + 남색 속
    w = h = 128
    img = nine_slice_frame(128, 16, inner=None, stone=True, gold_line=3)
    nav = navy_fill(w - 32, h - 32).convert('RGBA')
    img.paste(nav, (16, 16))
    d = ImageDraw.Draw(img)
    for i in range(3):
        d.rectangle([16 + i, 16 + i, w - 17 - i, h - 17 - i], outline=GOLD if i else GOLD_HI)
    save(round_mask(img, 14), 'dialog_panel_9s.png')


def gen_multiboard_frame():
    w = h = 64
    save(rounded_panel(w, h, 8, (10, 14, 28, 235), [(0, 1, GOLD_DK + (255,)), (2, 1, GOLD + (255,)), (3, 1, GOLD_HI + (255,))]), 'multiboard_frame_9s.png')
    # 타이머 창: 짙은 붉은 속 + 금 테
    save(rounded_panel(w, h, 8, (28, 10, 14, 240), [(0, 1, GOLD_DK + (255,)), (2, 1, (190, 150, 50, 255))]), 'timer_frame_9s.png')


def button(name, w=128, h=40, hover=False):
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    top = (34, 54, 120) if hover else (20, 34, 84)
    bot = (12, 20, 50)
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3))
        d.line([(0, y), (w, y)], fill=c + (255,))
    save(rounded_panel(w, h, 8, img, [(0, 1, GOLD_DK + (255,)), (1, 1, GOLD + (255,))]), name)


def gen_buttons():
    button('button_navy_9s.png')
    button('button_navy_hover_9s.png', hover=True)
    # 상단 바 버튼(메뉴/동맹/대화): 더 납작한 남색판 + 금테
    save(rounded_panel(96, 28, 8, (16, 26, 62, 255), [(0, 1, GOLD_DK + (255,)), (1, 1, GOLD + (255,))]), 'topbar_button_9s.png')
    # 상단 바 자원 칸
    save(rounded_panel(96, 28, 8, (6, 10, 22, 255), [(0, 1, (74, 61, 26, 255)), (1, 1, (120, 96, 40, 255))]), 'topbar_resource_9s.png')


def ss(size, fn, ss_=4):
    """슈퍼샘플로 그린 뒤 줄이기."""
    big = Image.new('RGBA', (size * ss_, size * ss_), (0, 0, 0, 0))
    fn(ImageDraw.Draw(big), size * ss_)
    return big.resize((size, size), Image.LANCZOS)



def gen_icons():
    def coin(d, s):
        d.ellipse([s * .08, s * .08, s * .92, s * .92], fill=(206, 164, 46), outline=(110, 80, 20), width=int(s * .05))
        d.ellipse([s * .22, s * .22, s * .78, s * .78], outline=(250, 226, 130), width=int(s * .05))
        d.rectangle([s * .45, s * .3, s * .55, s * .7], fill=(250, 226, 130))
    save(ss(64, coin), 'icon_gold.png')

    def tree(d, s):
        d.rectangle([s * .44, s * .62, s * .56, s * .92], fill=(104, 66, 30))
        for k, (cy, w) in enumerate([(.55, .42), (.38, .34), (.22, .24)]):
            d.polygon([(s * .5, s * (cy - .3)), (s * (.5 - w), s * (cy + .12)), (s * (.5 + w), s * (cy + .12))], fill=(36, 134, 62), outline=(16, 78, 34))
    save(ss(64, tree), 'icon_lumber.png')

    def meat(d, s):
        d.ellipse([s * .1, s * .15, s * .72, s * .72], fill=(196, 104, 52), outline=(100, 50, 20), width=int(s * .04))
        d.line([(s * .55, s * .55), (s * .88, s * .88)], fill=(240, 230, 210), width=int(s * .09))
        d.ellipse([s * .8, s * .8, s * .96, s * .96], fill=(240, 230, 210))
    save(ss(64, meat), 'icon_food.png')

    def sword(d, s):
        d.polygon([(s * .2, s * .8), (s * .75, s * .25), (s * .82, s * .32), (s * .3, s * .88)], fill=(190, 196, 208), outline=(90, 94, 104))
        d.rectangle([s * .15, s * .72, s * .32, s * .82], fill=(160, 120, 40))
    save(ss(64, sword), 'icon_attack.png')

    def shield(d, s):
        d.polygon([(s * .2, s * .15), (s * .8, s * .15), (s * .8, s * .55), (s * .5, s * .9), (s * .2, s * .55)], fill=(150, 160, 180), outline=(70, 76, 92), width=int(s * .04))
        d.polygon([(s * .5, s * .22), (s * .72, s * .22), (s * .72, s * .53), (s * .5, s * .8)], fill=(110, 120, 142))
    save(ss(64, shield), 'icon_armor.png')

    def hero_frame(d, s):
        d.rectangle([s * .04, s * .04, s * .96, s * .96], fill=(16, 14, 20), outline=GOLD, width=int(s * .05))
    save(ss(64, hero_frame), 'icon_hero_frame.png')


def gen_round_button():
    def f(d, s):
        d.ellipse([s * .04, s * .04, s * .96, s * .96], fill=(20, 18, 14), outline=GOLD, width=int(s * .08))
        d.ellipse([s * .2, s * .2, s * .8, s * .8], outline=GOLD_DK, width=int(s * .05))
    save(ss(64, f), 'minimap_button.png')

    def clock(d, s):
        d.ellipse([s * .02, s * .02, s * .98, s * .98], fill=(26, 36, 90), outline=GOLD, width=int(s * .06))
        d.ellipse([s * .16, s * .16, s * .84, s * .84], fill=(70, 120, 210), outline=GOLD_HI, width=int(s * .04))
        for k in range(12):
            a = k * math.pi / 6
            x, y = s * (.5 + .4 * math.cos(a)), s * (.5 + .4 * math.sin(a))
            d.ellipse([x - s * .02, y - s * .02, x + s * .02, y + s * .02], fill=GOLD_HI)
        d.ellipse([s * .36, s * .3, s * .66, s * .6], fill=(230, 236, 250))
        d.ellipse([s * .42, s * .3, s * .72, s * .6], fill=(70, 120, 210))
    save(ss(128, clock), 'clock_orb.png')


def gen_bars():
    for name, col, dk in (('bar_hp', (52, 190, 72), (20, 90, 34)), ('bar_mp', (46, 96, 230), (16, 36, 110)), ('bar_xp', (170, 100, 250), (70, 36, 120))):
        w, h = 128, 16
        im = Image.new('RGBA', (w, h), (0, 0, 0, 255))
        d = ImageDraw.Draw(im)
        for y in range(1, h - 1):
            t = (y - 1) / (h - 3)
            c = tuple(int(col[i] + (dk[i] - col[i]) * t) for i in range(3))
            d.line([(1, y), (w - 2, y)], fill=c + (255,))
        d.rectangle([0, 0, w - 1, h - 1], outline=(30, 30, 30))
        save(im, name + '.png')


def gen_emblem():
    """인벤토리 자리 엠블럼 — 돌판 위에 닻+원 무늬(직접 도안, 원작 문장 아님)."""
    w, h = 192, 224
    img = stone_tile(w, h, 9).convert('RGBA')
    d = ImageDraw.Draw(img)
    d.rectangle([4, 4, w - 5, h - 5], outline=STONE_DK, width=4)
    # 닻 무늬
    cx, cy = w // 2, h // 2 + 6
    col = (40, 56, 120)
    hi = (90, 120, 200)
    d.ellipse([cx - 56, cy - 56, cx + 56, cy + 56], outline=col, width=6)
    d.line([(cx, cy - 70), (cx, cy + 60)], fill=col, width=8)
    d.ellipse([cx - 12, cy - 86, cx + 12, cy - 62], outline=col, width=5)
    d.line([(cx - 28, cy - 40), (cx + 28, cy - 40)], fill=col, width=7)
    d.arc([cx - 50, cy - 10, cx + 50, cy + 70], 20, 160, fill=col, width=8)
    d.polygon([(cx - 52, cy + 40), (cx - 40, cy + 26), (cx - 34, cy + 46)], fill=col)
    d.polygon([(cx + 52, cy + 40), (cx + 40, cy + 26), (cx + 34, cy + 46)], fill=col)
    # 윗 장식(사자 머리 대신 단순 투구 도형)
    d.polygon([(cx - 36, 40), (cx, 12), (cx + 36, 40), (cx + 24, 56), (cx - 24, 56)], fill=(78, 80, 88), outline=STONE_DK)
    save(img, 'inventory_emblem.png')


def gen_console_bg():
    """콘솔 전체 배경(가로 1920×높이 291 = 27%) — 돌 타일을 깔고 윗선에 성가퀴."""
    w, h = 1920, 291
    tile = stone_tile(256, 256, 21)
    img = Image.new('RGB', (w, h))
    for y in range(0, h, 256):
        for x in range(0, w, 256):
            img.paste(tile, (x, y))
    img = img.convert('RGBA')
    d = ImageDraw.Draw(img)
    # 윗선 그림자·성가퀴
    d.rectangle([0, 0, w, 6], fill=STONE_DK + (255,))
    x = 0
    while x < w:
        bw = int(rng.integers(60, 120))
        d.rectangle([x, 0, x + bw - 4, 12], fill=STONE_A + (255,), outline=STONE_DK)
        x += bw + int(rng.integers(8, 24))
    save(img, 'console_bg_1920x291.png')


def contact_sheet():
    names = sorted(f for f in os.listdir(OUT) if f.endswith('.png') and f != 'contact_sheet.png' and not f.startswith('console_bg'))
    cell = 150
    cols = 6
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * cell, rows * (cell + 18)), (32, 36, 46))
    d = ImageDraw.Draw(sheet)
    for i, n in enumerate(names):
        im = Image.open(os.path.join(OUT, n)).convert('RGBA')
        im.thumbnail((cell - 10, cell - 10))
        x = (i % cols) * cell + 5
        y = (i // cols) * (cell + 18) + 5
        sheet.paste(im, (x, y), im)
        d.text((x, y + cell - 6), n[:22], fill=(200, 200, 210))
    sheet.save(os.path.join(OUT, 'contact_sheet.png'))


if __name__ == '__main__':
    print('출력:', os.path.abspath(OUT))
    gen_stone_tile(); gen_console_frame(); gen_dialog_panel(); gen_multiboard_frame()
    gen_buttons(); gen_icons(); gen_round_button(); gen_bars(); gen_emblem(); gen_console_bg()
    contact_sheet()
