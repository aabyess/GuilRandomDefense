"""워크3풍 UI 평면 부품(시스템 파이썬, PIL): 채팅 줄 띠 · 점수판 행 구분선 · 메뉴 구분선. (blender 세션 2026-10-07)
/usr/bin/python3 Tools/blender/gen_wc3_ui_flat.py [~/GRD_wc3_ui]"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_wc3_ui'); os.makedirs(D, exist_ok=True)
# chat_line_band 320×80: 검정, 알파 최대 0.6, 양끝 32px·위아래 10px로 번짐
w, h = 320, 80; x = np.linspace(0, 1, w); y = np.linspace(0, 1, h)
fx = np.clip(np.minimum(x, 1 - x) / (32 / w), 0, 1); fy = np.clip(np.minimum(y, 1 - y) / (7 / h), 0, 1)
img = np.zeros((h, w, 4), np.uint8); img[..., 3] = ((fx[None, :] ** 1.2) * (fy[:, None] ** .9) * .6 * 255).astype(np.uint8)
Image.fromarray(img).save(D + '/chat_line_band.png')
# score_row_divider 936×4
im = Image.new('RGBA', (936, 4), (0, 0, 0, 0)); d = ImageDraw.Draw(im); d.line((8, 0, 927, 0), fill=(150, 120, 60, 140)); d.rectangle((8, 1, 927, 3), fill=(4, 4, 8, 220)); im.save(D + '/score_row_divider.png')
# menu_divider 800×12: 금~청동 선(양끝 사라짐) + 가운데 마름모(파란 보석)
S = 4; im = Image.new('RGBA', (800 * S, 12 * S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for i in range(800 * S):
    t = abs(i / (800 * S) - .5) * 2; al = int(255 * min(1, (1 - t) * 4)) if t < .98 else 0
    d.line((i, 5 * S, i, 7 * S), fill=(196, 150, 62, al)); d.line((i, 5 * S, i, 5 * S), fill=(243, 210, 122, al))
cx, cy, r = 400 * S, 6 * S, 6 * S
d.polygon([(cx, cy - r), (cx + r * 1.6, cy), (cx, cy + r), (cx - r * 1.6, cy)], fill=(201, 162, 74, 255), outline=(110, 82, 30, 255))
d.polygon([(cx, cy - r * .45), (cx + r * .7, cy), (cx, cy + r * .45), (cx - r * .7, cy)], fill=(40, 90, 200, 255))
im.resize((800, 12), Image.LANCZOS).save(D + '/menu_divider.png'); print('flat parts saved')
