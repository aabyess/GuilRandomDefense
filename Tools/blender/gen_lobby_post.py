"""로비 배경 후처리 + 합성 시안(2026-10-06, blender 세션). gen_lobby_art.py의 렌더(bg_raw_2560.png)를 받아
   꽃·비네트·색조·오른쪽 가라앉힘·필름 입자를 입히고 bg_2560x1440.png · bg_1920x1080.png를 만든 뒤
   gen_lobby_ui.py의 단추·틀·제목 판을 얹은 1600×900 시안(mockup_1600x900.png)을 만든다. 시스템 파이썬:
   /usr/bin/python3 Tools/blender/gen_lobby_post.py [작업폴더 ~/GRD_lobby_art]
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_lobby_art')
FONT_MAIN = D + '/fonts/nanummyeongjo/NanumMyeongjo-ExtraBold.ttf'
FONT_TITLE = D + '/fonts/songmyung/SongMyung-Regular.ttf'
rng = np.random.RandomState(7)


def post(raw):
    a = np.asarray(Image.open(raw).convert('RGB'), np.float32) / 255
    h, w = a.shape[:2]
    # 꽃: 밝은 곳(횃불·노을)을 번지게
    hi = np.clip(a - .66, 0, 1) * 1.6
    himg = Image.fromarray((np.clip(hi, 0, 1) * 255).astype(np.uint8))
    bloom = sum(np.asarray(himg.filter(ImageFilter.GaussianBlur(r)), np.float32) / 255 * k for r, k in ((w * .004, .5), (w * .012, .45), (w * .03, .35)))
    a = a + bloom * np.array([1.0, .82, .6], np.float32) * .32
    # 대비 S곡선 + 분할 색조(그늘은 청록, 빛은 주황)
    lum = a.mean(-1, keepdims=True)
    a = np.clip(a, 0, 1)
    a = a ** 1.28
    a = a * (1 - .06) + .06 * (a * a * (3 - 2 * a))
    shadow = np.clip(1 - lum * 2.2, 0, 1); light = np.clip(lum * 1.6 - .3, 0, 1)
    a = a + shadow * np.array([-.012, .008, .03], np.float32) + light * np.array([.04, .015, -.03], np.float32)
    # 비네트(아래·양옆 깊게) + 오른쪽 1/3 가라앉힘(메뉴 자리)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32); xn = x / w; yn = y / h
    r = np.sqrt(((xn - .45) / .75) ** 2 + ((yn - .42) / .8) ** 2)
    vig = 1 - np.clip(r - .38, 0, 1) ** 1.5 * 1.15
    right = 1 - .38 * np.clip((xn - .62) / .38, 0, 1) ** 1.2
    bottom = 1 - .35 * np.clip((yn - .72) / .28, 0, 1) ** 1.4
    a = a * (vig * right * bottom)[..., None]
    # 필름 입자
    a = a + (rng.randn(h, w, 1).astype(np.float32) * .012)
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


def gold_text(im, text, font, cx, cy, size, outline=3):
    f = ImageFont.truetype(font, size)
    layer = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
    bb = d.textbbox((0, 0), text, font=f); tw, th = bb[2] - bb[0], bb[3] - bb[1]
    pos = (cx - tw / 2 - bb[0], cy - th / 2 - bb[1])
    # 그림자 + 검은 외곽 + 금 그라데이션
    sh = Image.new('RGBA', im.size, (0, 0, 0, 0)); ImageDraw.Draw(sh).text((pos[0] + 3, pos[1] + 5), text, font=f, fill=(0, 0, 0, 200))
    sh = sh.filter(ImageFilter.GaussianBlur(3)); im.alpha_composite(sh)
    d.text(pos, text, font=f, fill=(20, 10, 4, 255), stroke_width=outline + 1, stroke_fill=(20, 10, 4, 255))
    mask = Image.new('L', im.size, 0); ImageDraw.Draw(mask).text(pos, text, font=f, fill=255)
    gy = np.linspace(0, 1, im.size[1], dtype=np.float32)[:, None]
    top = pos[1]; span = th
    t = np.clip((gy - top / im.size[1]) / (span / im.size[1] + 1e-6), 0, 1)
    col = np.where(t[..., None] < .5, (np.array([255, 236, 160]) * (1 - t[..., None] * 2) + np.array([240, 176, 60]) * t[..., None] * 2), (np.array([240, 176, 60]) * (1 - (t[..., None] - .5) * 2) + np.array([150, 88, 24]) * (t[..., None] - .5) * 2))
    col = np.broadcast_to(col, (im.size[1], im.size[0], 3)).astype(np.uint8)
    fill = Image.fromarray(np.concatenate([col, np.full(col.shape[:2] + (1,), 255, np.uint8)], -1))
    layer.paste(fill, (0, 0), mask)
    im.alpha_composite(layer)


def mockup(bg, ui, out):
    W, H = 1600, 900
    im = bg.resize((W, H), Image.LANCZOS).convert('RGBA')
    # 메뉴 틀(오른쪽): 가운데보다 오른쪽 1/3
    fw, fh = 400, 760
    frame = Image.open(ui + '/menu_frame.png').resize((fw, fh), Image.LANCZOS)
    fx, fy = 1130, 90
    sh = Image.new('RGBA', (W, H), (0, 0, 0, 0)); sh.paste((0, 0, 0, 170), (fx + 8, fy + 14, fx + fw + 8, fy + fh + 14)); im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(14)))
    im.alpha_composite(frame, (fx, fy))
    labels = ['혼자 하기', '같이 하기', '세이브 코드 불러오기']
    states = ['normal', 'hover', 'normal']
    bw, bh = 296, 74
    for i, (lb, st) in enumerate(zip(labels, states)):
        b = Image.open(ui + '/btn_%s.png' % st).resize((bw, bh), Image.LANCZOS)
        bx = fx + (fw - bw) // 2; by = fy + 190 + i * 112
        im.alpha_composite(b, (bx, by))
        gold_text(im, lb, FONT_MAIN, bx + bw // 2, by + bh // 2 - 1, 29 if i < 2 else 23, 2)
    # 제목 판 + 글자
    tw_ = 820; th_ = int(300 * tw_ / 1400)
    tp = Image.open(ui + '/title_plate.png').resize((tw_, th_), Image.LANCZOS)
    tx, ty = 120, 22
    im.alpha_composite(tp, (tx, ty))
    gold_text(im, '구랜디', FONT_TITLE, tx + tw_ // 2, ty + th_ // 2 - 2, 86, 3)
    # 버전 표기
    d = ImageDraw.Draw(im); d.text((W - 62, H - 24), '0.3.7v', fill=(200, 190, 170, 200), font=ImageFont.truetype(FONT_MAIN, 14))
    im.convert('RGB').save(out)


def specimen(out):
    im = Image.new('RGB', (1200, 420), (24, 22, 28)); d = ImageDraw.Draw(im)
    rows = [(D + '/fonts/nanummyeongjo/NanumMyeongjo-ExtraBold.ttf', 'Nanum Myeongjo ExtraBold — 구랜디 혼자 하기 같이 하기'),
            (D + '/fonts/songmyung/SongMyung-Regular.ttf', 'Song Myung — 구랜디 혼자 하기 같이 하기'),
            (D + '/fonts/gowunbatang/GowunBatang-Bold.ttf', 'Gowun Batang Bold — 구랜디 혼자 하기 같이 하기')]
    for i, (fp, tx) in enumerate(rows):
        d.text((30, 20 + i * 135), tx, font=ImageFont.truetype(fp, 54), fill=(240, 196, 110))
        d.text((30, 90 + i * 135), '세이브 코드 불러오기 · 라운드 25 · 0123456789', font=ImageFont.truetype(fp, 30), fill=(200, 190, 170))
    im.save(out)


if __name__ == '__main__':
    final = post(D + '/bg_raw_2560.png')
    final.save(D + '/bg_2560x1440.png')
    final.resize((1920, 1080), Image.LANCZOS).save(D + '/bg_1920x1080.png')
    mockup(final, D + '/ui', D + '/mockup_1600x900.png')
    specimen(D + '/fonts/specimen.png')
    print('done')
