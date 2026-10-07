"""명령 카드 아이콘 후처리(시스템 파이썬): 렌더(투명) → 워크3 BTN식 바탕(어두운 남흑 방사 그라데이션) + 소품 그림자 + 가장자리 베벨 → 128·64 + 시트.
/usr/bin/python3 Tools/blender/gen_wc3_cmd_icons_post.py [~/GRD_wc3_ui/icons]"""
import os, sys
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_wc3_ui/icons')
NAMES = ['move', 'hold', 'stop', 'attack', 'patrol', 'sell']; S = 256
yy, xx = np.mgrid[0:S, 0:S] / (S - 1)
r = np.sqrt((xx - .45) ** 2 + (yy - .4) ** 2)
bg = np.zeros((S, S, 3)); top = np.array([46, 58, 84]); bot = np.array([8, 10, 18])
t = np.clip(r / .75, 0, 1)[..., None]; bg = top * (1 - t) + bot * t
def bevel(a):
    e = 10; out = a.copy()
    for i in range(e):
        k = (e - i) / e
        out[i, :] = out[i, :] * (1 + .5 * k); out[:, i] = out[:, i] * (1 + .35 * k)
        out[S - 1 - i, :] = out[S - 1 - i, :] * (1 - .55 * k); out[:, S - 1 - i] = out[:, S - 1 - i] * (1 - .45 * k)
    return np.clip(out, 0, 255)
sheet = Image.new('RGB', (len(NAMES) * 140 + 10, 300), (24, 24, 28))
for i, n in enumerate(NAMES):
    fg = Image.open(f'{D}/_raw/{n}.png').convert('RGBA')
    base = Image.fromarray(bevel(bg).astype(np.uint8)).convert('RGBA')
    sh = Image.new('RGBA', (S, S), (0, 0, 0, 0)); sh.putalpha(fg.split()[3].point(lambda v: int(v * .7)))
    base.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)), (6, 8)); base.alpha_composite(fg)
    d = ImageDraw.Draw(base); d.rectangle((0, 0, S - 1, S - 1), outline=(0, 0, 0, 255), width=2)
    base = base.convert('RGB')
    base.resize((128, 128), Image.LANCZOS).save(f'{D}/cmd_{n}_128.png'); base.resize((64, 64), Image.LANCZOS).save(f'{D}/cmd_{n}_64.png')
    sheet.paste(base.resize((128, 128), Image.LANCZOS), (10 + i * 140, 10)); sheet.paste(base.resize((64, 64), Image.LANCZOS), (42 + i * 140, 160))
sheet.save(f'{D}/icons_sheet.png'); print('icons saved')
