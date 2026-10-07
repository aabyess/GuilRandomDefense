"""창 그림 비교 합성(시스템 파이썬): 지금 화면(ClaudeBridge/shots) 옆에 새 부품으로 같은 배치를 9-slice 조립.
/usr/bin/python3 Tools/blender/compare_wc3_windows.py [~/GRD_wc3_ui]  → compare_drawer.png · compare_helper.png"""
import os, sys
from PIL import Image, ImageDraw
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_wc3_ui')
SHOTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'ClaudeBridge', 'shots')
L = lambda n: Image.open(f'{D}/{n}.png').convert('RGBA')
def s9(name, w, h, m1):
    """m1 = 여백(@1x). 납품은 2배라 원본에서 2*m1을 잘라 목표 2배 크기로 붙인 뒤 반으로 줄인다."""
    src = L(name); sw, sh = src.size; m = 2 * m1; W2, H2 = 2 * w, 2 * h
    out = Image.new('RGBA', (W2, H2))
    xs = [(0, m, 0, m), (m, sw - m, m, W2 - m), (sw - m, sw, W2 - m, W2)]; ys = [(0, m, 0, m), (m, sh - m, m, H2 - m), (sh - m, sh, H2 - m, H2)]
    for a0, a1, b0, b1 in xs:
        for c0, c1, d0, d1 in ys:
            if b1 > b0 and d1 > d0 and a1 > a0 and c1 > c0: out.alpha_composite(src.crop((a0, c0, a1, c1)).resize((b1 - b0, d1 - d0), Image.LANCZOS), (b0, d0))
    return out.resize((w, h), Image.LANCZOS)
def navy(w, h, a=248): return Image.new('RGBA', (w, h), (10, 16, 32, a))

def drawer():
    cur = Image.open(os.path.join(SHOTS, 'g2_n2_drawer.png')).convert('RGBA')
    ours = cur.copy(); x0, y0, x1, y1 = 1310, 143, 1866, 940; W, H = x1 - x0, y1 - y0
    ours.paste(Image.new('RGBA', (W, H), (0, 0, 0, 0)), (x0, y0)); base = cur.crop((x0 - 60, y0, x0, y1)).resize((W, H)); ours.paste(base, (x0, y0))
    ours.alpha_composite(navy(W - 40, H - 40), (x0 + 20, y0 + 20)); ours.alpha_composite(s9('win_frame', W, H, 40), (x0, y0))
    ours.alpha_composite(s9('win_title_strip', 330, 40, 14), (x0 + 22, y0 + 16)); ours.alpha_composite(s9('win_btn', 115, 34, 14), (x0 + 372, y0 + 15)); ours.alpha_composite(s9('win_close_btn', 34, 34, 12), (x0 + 492, y0 + 15))
    ours.alpha_composite(s9('win_search_box_focus', 500, 38, 12), (x0 + 28, y0 + 58))
    for i, nm in enumerate(['selected'] + ['normal'] * 7):
        ours.alpha_composite(s9('win_tab' + ('' if nm == 'normal' else '_selected'), 58, 30, 12), (x0 + 26 + i * 64, y0 + 106))
    for j, k in enumerate(['owned', 'normal', 'normal', 'dim']):
        ours.alpha_composite(s9('win_row' + ('' if k == 'normal' else '_' + k), 490, 146, 14), (x0 + 28, y0 + 178 + j * 153))
    ours.alpha_composite(s9('win_scroll_track', 12, 590, 4), (x0 + 524, y0 + 178)); ours.alpha_composite(s9('win_scroll_handle', 12, 60, 5), (x0 + 524, y0 + 180))
    ours.alpha_composite(s9('drawer_tab', 46, 210, 14), (1872, 280))
    out = Image.new('RGBA', (2 * 640 + 20, 820), (20, 20, 24, 255))
    out.alpha_composite(cur.crop((1290, 130, 1920, 950)).resize((640, 820)), (0, 0)); out.alpha_composite(ours.crop((1290, 130, 1920, 950)).resize((640, 820)), (660, 0))
    d = ImageDraw.Draw(out); d.text((6, 4), 'NOW', fill=(255, 255, 0, 255)); d.text((666, 4), 'NEW', fill=(255, 255, 0, 255))
    out.convert('RGB').save(f'{D}/compare_drawer.png')

def helper():
    cur = Image.open(os.path.join(SHOTS, 'g2_n2_tab0.png')).convert('RGBA'); ours = cur.copy()
    x0, y0, x1, y1 = 82, 62, 1840, 1020; W, H = x1 - x0, y1 - y0
    ours.alpha_composite(navy(W - 40, H - 40, 252), (x0 + 20, y0 + 20)); ours.alpha_composite(s9('win_frame', W, H, 40), (x0, y0))
    ours.alpha_composite(s9('win_title_strip', 220, 40, 14), (x0 + 28, y0 + 26)); ours.alpha_composite(s9('win_search_box', 340, 42, 12), (x0 + 274, y0 + 25))
    for i, (w, k) in enumerate(((190, 'normal'), (94, 'selected'), (94, 'normal'), (94, 'normal'), (170, 'normal'))):
        xx = [630, 838, 938, 1038, 1150][i]; ours.alpha_composite(s9('win_btn' + ('' if k == 'normal' else '_' + k), w, 42, 14), (x0 + xx, y0 + 25))
    ours.alpha_composite(s9('win_btn', 140, 42, 14), (x0 + 1512, y0 + 25)); ours.alpha_composite(s9('win_close_btn', 44, 42, 12), (x0 + 1672, y0 + 25))
    for i, k in enumerate(('selected', 'normal', 'normal')): ours.alpha_composite(s9('win_tab' + ('' if k == 'normal' else '_selected'), 170, 40, 12), (x0 + 38 + i * 176, y0 + 84))
    for c in range(5):
        for r in range(9):
            k = 'owned' if (c, r) in ((0, 0), (1, 0), (0, 7), (1, 8)) else ('dim' if r > 6 else 'normal')
            ours.alpha_composite(s9('win_row' + ('' if k == 'normal' else '_' + k), 322, 76, 14), (x0 + 38 + c * 330, y0 + 176 + r * 81))
            ours.alpha_composite(s9('win_bar_track', 240, 14, 4), (x0 + 112 + c * 330, y0 + 232 + r * 81))
    out = Image.new('RGBA', (1920, 2 * 540 + 20), (20, 20, 24, 255))
    out.alpha_composite(cur.resize((960, 540)), (0, 0)); out.alpha_composite(ours.resize((960, 540)), (960, 0))
    out.alpha_composite(cur.crop((82, 62, 1042, 602)), (0, 560)); out.alpha_composite(ours.crop((82, 62, 1042, 602)), (960, 560))
    d = ImageDraw.Draw(out); d.text((6, 4), 'NOW', fill=(255, 255, 0, 255)); d.text((966, 4), 'NEW (parts only — text/icons are code)', fill=(255, 255, 0, 255))
    out.convert('RGB').save(f'{D}/compare_helper.png')

drawer(); helper(); print('saved')
