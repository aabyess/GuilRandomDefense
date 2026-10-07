"""정보창 「상태:」 버프·디버프 아이콘 7종(PIL, 시스템 파이썬): 금속 테두리 + 기호, 512 슈퍼샘플 → 64(+128 미리보기) + 시트.
/usr/bin/python3 Tools/blender/gen_buff_icons.py [~/GRD_buff_icons]"""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
D = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_buff_icons'); os.makedirs(D, exist_ok=True)
S = 512
GOLD = (236, 196, 92); WHITE = (245, 240, 220); GREEN = (120, 220, 110); RED = (230, 70, 60); BLUE = (90, 170, 255)

def plate(good):
    rim_a, rim_b = ((250, 214, 110), (120, 84, 28)) if good else ((200, 90, 80), (70, 18, 18))
    in_top, in_bot = ((40, 82, 44), (8, 24, 12)) if good else ((96, 26, 26), (22, 6, 8))
    yy, xx = np.mgrid[0:S, 0:S] / (S - 1)
    r = np.clip(np.sqrt((xx - .45) ** 2 + (yy - .38) ** 2) / .8, 0, 1)[..., None]
    inner = np.array(in_top) * (1 - r) + np.array(in_bot) * r
    t = ((xx + yy) / 2)[..., None]; rim = np.array(rim_a) * (1 - t) + np.array(rim_b) * t
    m = Image.new('L', (S, S), 0); ImageDraw.Draw(m).rounded_rectangle((8, 8, S - 9, S - 9), 64, fill=255)
    mi = Image.new('L', (S, S), 0); ImageDraw.Draw(mi).rounded_rectangle((44, 44, S - 45, S - 45), 40, fill=255)
    img = Image.fromarray(rim.astype(np.uint8)).convert('RGBA'); img.putalpha(m)
    inn = Image.fromarray(inner.astype(np.uint8)).convert('RGBA'); inn.putalpha(mi)
    # 안쪽 홈(어두운 선)
    g = Image.new('RGBA', (S, S), (0, 0, 0, 0)); ImageDraw.Draw(g).rounded_rectangle((38, 38, S - 39, S - 39), 44, outline=(0, 0, 0, 200), width=8)
    img.alpha_composite(g); img.alpha_composite(inn); return img

def star(c, R, r, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        a = math.radians(rot + i * 180 / n); k = R if i % 2 == 0 else r
        pts.append((c[0] + k * math.cos(a), c[1] + k * math.sin(a)))
    return pts

def sword(d, x0, y0, x1, y1, w=34, blade=WHITE, hilt=GOLD):
    """(x0,y0)=칼끝 (x1,y1)=손잡이 끝"""
    L = math.hypot(x1 - x0, y1 - y0); ux, uy = (x1 - x0) / L, (y1 - y0) / L; px, py = -uy, ux
    def P(t, s): return (x0 + ux * t * L + px * s, y0 + uy * t * L + py * s)
    d.polygon([P(0, 0), P(.08, w / 2), P(.62, w / 2), P(.62, -w / 2), P(.08, -w / 2)], fill=blade)
    d.line([P(.1, 0), P(.6, 0)], fill=(170, 170, 180), width=5)
    d.polygon([P(.62, -w * 1.3), P(.68, -w * 1.3), P(.68, w * 1.3), P(.62, w * 1.3)], fill=hilt)
    d.line([P(.68, 0), P(.9, 0)], fill=(120, 70, 30), width=int(w * .5)); d.ellipse((P(.95, 0)[0] - 14, P(.95, 0)[1] - 14, P(.95, 0)[0] + 14, P(.95, 0)[1] + 14), fill=hilt)

def arrow(d, cx, cy, w, h, up=True, fill=GREEN):
    s = -1 if up else 1; sh = w * .42
    d.polygon([(cx, cy + s * h / 2), (cx + w / 2, cy - s * h * .05), (cx + sh / 2, cy - s * h * .05), (cx + sh / 2, cy - s * h / 2),
               (cx - sh / 2, cy - s * h / 2), (cx - sh / 2, cy - s * h * .05), (cx - w / 2, cy - s * h * .05)], fill=fill)

def heart(d, cx, cy, sz, fill):
    pts = []
    for i in range(0, 361, 4):
        t = math.radians(i); x = 16 * math.sin(t) ** 3
        y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
        pts.append((cx + x * sz / 34, cy + y * sz / 34))
    d.polygon(pts, fill=fill)

def draw_sym(name, d, big):
    if name == 'buff_atkspeed':
        for i, (y, l) in enumerate([(190, 130), (256, 170), (322, 130)]):
            d.rounded_rectangle((70, y - 9, 70 + l, y + 9), 9, fill=GOLD)
        sword(d, 400, 108, 210, 400, w=40)
    elif name == 'buff_atk':
        sword(d, 256, 100, 256, 380, w=44)
        arrow(d, 150, 330, 100, 130, True, GREEN); arrow(d, 362, 330, 100, 130, True, GREEN)
    elif name == 'debuff_move':
        boot = [(170, 110), (290, 110), (290, 250), (400, 300), (410, 370), (150, 370), (150, 300), (170, 300)]
        d.polygon(boot, fill=(205, 170, 120)); d.polygon([(150, 370), (410, 370), (410, 395), (150, 395)], fill=(90, 60, 40))
        d.rectangle((170, 110, 290, 150), fill=(235, 215, 170))
        arrow(d, 410, 170, 110, 150, False, RED)
    elif name == 'buff_mana':
        pts = [(256, 80), (350, 215)] + [(256 + 128 * math.cos(math.radians(a)), 305 + 128 * math.sin(math.radians(a))) for a in range(-25, 206, 10)] + [(162, 215)]
        d.polygon(pts, fill=BLUE); d.ellipse((190, 250, 235, 330), fill=(190, 225, 255)); d.ellipse((205, 240, 225, 262), fill=WHITE)
    elif name == 'buff_life':
        heart(d, 256, 270, 420, RED); heart(d, 256, 270, 330, (255, 110, 100))
        d.rectangle((226, 190, 286, 350), fill=GREEN); d.rectangle((176, 240, 336, 300), fill=GREEN)
    elif name == 'buff_team':
        d.rectangle((120, 90, 140, 440), fill=(200, 200, 205))
        d.polygon([(140, 100), (330, 100), (290, 160), (330, 220), (140, 220)], fill=GOLD)
        arrow(d, 390, 340, 100, 140, True, GREEN)
        for x in (200, 270):
            d.ellipse((x - 26, 280, x + 26, 332), fill=WHITE); d.pieslice((x - 48, 340, x + 48, 440), 180, 360, fill=WHITE)
    elif name == 'debuff_stun':
        d.ellipse((176, 220, 336, 380), fill=(245, 210, 170)); d.ellipse((215, 280, 240, 305), fill=(40, 20, 20)); d.ellipse((275, 280, 300, 305), fill=(40, 20, 20))
        d.pieslice((176, 205, 336, 330), 180, 360, fill=(200, 60, 50))
        for c, R in [((256, 140), 56), ((130, 200), 42), ((385, 195), 42)]:
            d.polygon(star(c, R, R * .45), fill=(255, 235, 110))
        d.arc((90, 130, 420, 270), 200, 340, fill=(255, 235, 110), width=6)

NAMES = ['buff_atkspeed', 'buff_atk', 'debuff_move', 'buff_mana', 'buff_life', 'buff_team', 'debuff_stun']
sheet = Image.new('RGB', (len(NAMES) * 140 + 10, 220), (28, 28, 34))
for i, n in enumerate(NAMES):
    img = plate(n.startswith('buff'))
    sym = Image.new('RGBA', (S, S), (0, 0, 0, 0)); draw_sym(n, ImageDraw.Draw(sym), S)
    sh = Image.new('RGBA', (S, S), (0, 0, 0, 0)); sh.putalpha(sym.split()[3].point(lambda v: int(v * .65)))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(8)), (6, 10)); img.alpha_composite(sym)
    img.resize((64, 64), Image.LANCZOS).save(f'{D}/{n}.png'); img.resize((128, 128), Image.LANCZOS).save(f'{D}/{n}_128.png')
    bgc = Image.new('RGBA', (128, 128), (28, 28, 34, 255)); bgc.alpha_composite(img.resize((128, 128), Image.LANCZOS))
    sheet.paste(bgc.convert('RGB'), (10 + i * 140, 10)); sheet.paste(Image.open(f'{D}/{n}.png').convert('RGBA').convert('RGB'), (42 + i * 140, 150))
sheet.save(f'{D}/buff_icons_sheet.png'); print('ok')
