#!/usr/bin/env python3
"""워크3풍 UI 배치 시안(정적 목업) — 사장님 10-07 「UI를 이렇게 바꿀 수 있어? 최대한 따라 해 봐」(참고: 원작 원랜디 화면).
⚠️ 원작 UI 그림(BLP)·스크린샷 픽셀은 쓰지 않는다 — 모양·배치·색만 보고 **전부 새로 그린다**(돌 질감은 노이즈로 생성). 월드 배경은 우리 게임 촬영 사진.
실행: /usr/bin/python3 Tools/ui/gen_wc3_style_mock.py  → Docs/ui_mockups/wc3_style/mock_ours.png (+ compare.png: 참고 사진과 위아래로)
"""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
OUT = os.path.join(ROOT, 'Docs', 'ui_mockups', 'wc3_style')
os.makedirs(OUT, exist_ok=True)
BASE = os.path.join(ROOT, 'ClaudeBridge', 'shots', 'g2_c1_stone.png')       # 우리 게임 촬영(1920×1080)
REF = '/tmp/claude-501/ref_original_ui.png'
FONT_B = os.path.join(ROOT, 'Assets', 'Fonts', 'Pretendard-Bold.ttf')
FONT_R = os.path.join(ROOT, 'Assets', 'Fonts', 'Pretendard-Regular.ttf')
def F(size, bold=True): return ImageFont.truetype(FONT_B if bold else FONT_R, size)
rng = np.random.default_rng(7)
W, H = 1920, 1080

GOLD = (222, 184, 90); GOLD_DK = (120, 90, 30); WHITE = (240, 240, 240)
STONE = (108, 110, 114)

# ── 돌 질감: 여러 크기의 노이즈를 겹친다(회색 거친 바위)
def fbm(w, h, octaves=5, base=8, seed=1):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        cw, ch = max(2, w // (base * 2 ** (octaves - 1 - o)) + 2), max(2, h // (base * 2 ** (octaves - 1 - o)) + 2)
        n = r.random((ch, cw)).astype(np.float32)
        n = np.array(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32) / 255
        out += n * amp; tot += amp; amp *= 0.55
    return out / tot

def stone_tex(w, h, base=(104, 106, 110), seed=3, contrast=70):
    n = fbm(w, h, 6, 6, seed)
    n2 = fbm(w, h, 4, 3, seed + 9)
    v = (n - 0.5) * contrast + (n2 - 0.5) * 30
    arr = np.zeros((h, w, 3), np.float32)
    for c in range(3): arr[..., c] = base[c] + v
    # 어두운 균열: 노이즈 임계 근처 얇은 선
    crack = np.abs(fbm(w, h, 3, 5, seed + 21) - 0.5) < 0.012
    arr[crack] *= 0.55
    arr += rng.normal(0, 4, (h, w))[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))

def bevel(im, box, light=(170, 172, 176), dark=(40, 41, 46), w=3, inner=None):
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    for i in range(w):
        d.line([(x0 + i, y0 + i), (x1 - i, y0 + i)], fill=light)
        d.line([(x0 + i, y0 + i), (x0 + i, y1 - i)], fill=light)
        d.line([(x0 + i, y1 - i), (x1 - i, y1 - i)], fill=dark)
        d.line([(x1 - i, y0 + i), (x1 - i, y1 - i)], fill=dark)
    if inner: d.rectangle([x0 + w, y0 + w, x1 - w, y1 - w], fill=inner)

def text(d, xy, s, size, fill=WHITE, anchor='la', bold=True, shadow=True):
    f = F(size, bold)
    if shadow: d.text((xy[0] + 1, xy[1] + 1), s, font=f, fill=(0, 0, 0), anchor=anchor)
    d.text(xy, s, font=f, fill=fill, anchor=anchor)

# ── 월드 배경: 우리 촬영 사진의 게임 영역만(HUD는 새로 덮는다)
base = Image.open(BASE).convert('RGB')
img = base.copy()
d = ImageDraw.Draw(img)

# ======================================================== 하단 콘솔
CY = 792   # 콘솔 윗선
# 돌 판
console = stone_tex(W, H - CY, seed=11)
img.paste(console, (0, CY))
# 톱니 윗선: 돌 이빨(가로 24px·높이 12px 간격) — 위로 솟은 총안
tooth_w, tooth_h = 26, 13
x = 0
while x < W:
    d.rectangle([x, CY - tooth_h, x + tooth_w - 1, CY], fill=None)
    tex = stone_tex(tooth_w, tooth_h + 2, seed=100 + (x // tooth_w))
    img.paste(tex, (x, CY - tooth_h))
    d.line([(x, CY - tooth_h), (x + tooth_w - 1, CY - tooth_h)], fill=(160, 162, 166))
    d.line([(x, CY - tooth_h), (x, CY)], fill=(38, 39, 43)); d.line([(x + tooth_w - 1, CY - tooth_h), (x + tooth_w - 1, CY)], fill=(38, 39, 43))
    x += tooth_w + 12
d.line([(0, CY), (W, CY)], fill=(30, 31, 35), width=3)

def panel(box, inner=(12, 12, 18), frame=7):
    x0, y0, x1, y1 = box
    # 돌 틀(밝은 위/왼, 어두운 아래/오른) + 안쪽 짙은 판
    bevel(img, box, (190, 192, 196), (30, 31, 36), frame)
    d.rectangle([x0 + frame, y0 + frame, x1 - frame, y1 - frame], fill=inner)
    # 안쪽 가는 금선
    d.rectangle([x0 + frame, y0 + frame, x1 - frame, y1 - frame], outline=(92, 72, 34))

# 미니맵 + 단추 5개
mm = (14, 806, 372, 1070)
panel(mm, (8, 20, 40))
src_mm = base.crop((26, 812, 246, 1060)).resize((mm[2] - mm[0] - 16, mm[3] - mm[1] - 16))
img.paste(src_mm, (mm[0] + 8, mm[1] + 8))
for i in range(5):
    cx, cy = 400, 836 + i * 50
    d.ellipse([cx - 22, cy - 22, cx + 22, cy + 22], fill=(40, 41, 46), outline=(20, 20, 24), width=3)
    d.ellipse([cx - 19, cy - 19, cx + 19, cy + 19], fill=(58, 60, 66), outline=GOLD_DK, width=2)
    d.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=(24, 44, 78))
    d.arc([cx - 9, cy - 9, cx + 9, cy + 9], 200, 340, fill=(120, 180, 240), width=3)

# 초상화 아치
px0, py0, px1, py1 = 440, 806, 640, 1070
panel((px0, py0, px1, py1), (6, 6, 10), 8)
arch = Image.new('RGBA', (px1 - px0 - 22, 196), (0, 0, 0, 0))
pw, ph = arch.size
mask = Image.new('L', (pw, ph), 0); md = ImageDraw.Draw(mask)
md.rounded_rectangle([0, 0, pw - 1, ph + 30], radius=pw // 2, fill=255)
portrait = base.crop((272, 814, 500, 1016)).resize((pw, ph))
arch.paste(portrait, (0, 0), mask)
img.paste(arch, (px0 + 11, py0 + 12), arch)
d.arc([px0 + 10, py0 + 11, px1 - 11, py0 + 11 + pw], 180, 360, fill=GOLD_DK, width=3)
# 체력·마나 숫자 띠
d.rectangle([px0 + 11, 1010, px1 - 11, 1038], fill=(10, 60, 14), outline=(0, 0, 0)); text(d, ((px0 + px1) // 2, 1024), '6122 / 6122', 20, anchor='mm')
d.rectangle([px0 + 11, 1040, px1 - 11, 1062], fill=(12, 24, 70), outline=(0, 0, 0)); text(d, ((px0 + px1) // 2, 1051), '15 / 1000', 18, anchor='mm')

# 정보창
ix0, iy0, ix1, iy1 = 660, 806, 1216, 1070
panel((ix0, iy0, ix1, iy1), (14, 14, 20), 8)
d.rectangle([ix0 + 18, iy0 + 14, ix1 - 18, iy0 + 44], fill=(24, 22, 30), outline=GOLD_DK)
text(d, ((ix0 + ix1) // 2, iy0 + 29), '신문철 말썽쟁이', 24, GOLD, 'mm')
d.rectangle([ix0 + 18, iy0 + 50, ix1 - 18, iy0 + 72], fill=(30, 28, 38), outline=(92, 72, 34))
text(d, ((ix0 + ix1) // 2, iy0 + 61), '레벨 1  초월함', 17, (200, 200, 210), 'mm')
# 방어/공격 칸(왼쪽) · 트로피(가운데) · 능력치(오른쪽)
d.rectangle([ix0 + 22, iy0 + 86, ix0 + 70, iy0 + 134], fill=(8, 8, 12), outline=(92, 72, 34))
d.polygon([(ix0 + 34, iy0 + 98), (ix0 + 58, iy0 + 98), (ix0 + 58, iy0 + 114), (ix0 + 46, iy0 + 128), (ix0 + 34, iy0 + 114)], fill=(150, 160, 190))
text(d, (ix0 + 80, iy0 + 92), '아머: 무적', 19, (255, 140, 60), 'la')
text(d, (ix0 + 80, iy0 + 118), '공격: 30002', 19, (255, 190, 90), 'la')
text(d, (ix0 + 22, iy0 + 150), '상태:', 17, (255, 140, 60), 'la')
d.rectangle([ix0 + 250, iy0 + 98, ix0 + 298, iy0 + 146], fill=(8, 8, 12), outline=(92, 72, 34))
d.ellipse([ix0 + 262, iy0 + 106, ix0 + 286, iy0 + 126], fill=GOLD); d.rectangle([ix0 + 270, iy0 + 126, ix0 + 278, iy0 + 138], fill=GOLD)
for k, (lab, val) in enumerate([('힘', '12'), ('민첩성', '9'), ('지능', '7')]):
    text(d, (ix0 + 340, iy0 + 92 + k * 30), f'{lab}:', 18, (255, 140, 60), 'la'); text(d, (ix0 + 440, iy0 + 92 + k * 30), val, 18, WHITE, 'la')
# 스킬 아이콘 줄(우리 정보창에 이미 있음)
for k in range(5):
    sx = ix0 + 22 + k * 56
    d.rectangle([sx, iy1 - 74, sx + 46, iy1 - 28], fill=(30, 30, 44), outline=GOLD_DK)
    d.ellipse([sx + 8, iy1 - 66, sx + 38, iy1 - 36], fill=[(210, 170, 70), (90, 150, 220), (200, 80, 80), (80, 190, 110), (170, 110, 210)][k])

# 인벤토리 2×3 + 제목 띠
vx0, vy0, vx1, vy1 = 1226, 806, 1432, 1070
panel((vx0, vy0, vx1, vy1), (12, 14, 24), 7)
d.rectangle([vx0 + 12, vy0 + 12, vx1 - 12, vy0 + 38], fill=(26, 24, 34), outline=GOLD_DK)
text(d, ((vx0 + vx1) // 2, vy0 + 25), '인벤토리', 19, GOLD, 'mm')
for r_ in range(3):
    for c_ in range(2):
        bx, by = vx0 + 14 + c_ * 92, vy0 + 50 + r_ * 70
        d.rectangle([bx, by, bx + 84, by + 62], fill=(24, 30, 56), outline=(60, 70, 110), width=2)
        d.rectangle([bx + 3, by + 3, bx + 81, by + 59], outline=(14, 16, 32))

# 명령 카드 4×3
cx0, cy0, cx1, cy1 = 1446, 806, 1906, 1070
panel((cx0, cy0, cx1, cy1), (8, 8, 10), 7)
labels = [('홀드', 'H'), ('공격', 'A'), ('모으기', 'V'), ('판매', ''), ('', ''), ('', ''), ('', ''), ('', ''), ('엄마간식', 'Q'), ('특성강화', ''), ('', ''), ('', '')]
for r_ in range(3):
    for c_ in range(4):
        bx, by = cx0 + 14 + c_ * 108, cy0 + 14 + r_ * 80
        d.rectangle([bx, by, bx + 100, by + 72], fill=(4, 4, 6), outline=(70, 64, 48), width=2)
        d.rectangle([bx + 2, by + 2, bx + 98, by + 70], outline=(24, 22, 16))
        lab, key = labels[r_ * 4 + c_]
        if lab:
            text(d, (bx + 50, by + 30), lab, 19, WHITE, 'mm')
            if key: text(d, (bx + 92, by + 64), key, 14, (255, 220, 120), 'rd')

# 돌 기둥(패널 사이 이음) — 얇은 어두운 홈
for xx in (378, 436, 646, 1220, 1438):
    d.line([(xx, CY + 4), (xx, H)], fill=(36, 37, 41), width=2)

# ======================================================== 상단 바
d.rectangle([0, 0, W, 38], fill=(10, 10, 16)); d.line([(0, 38), (W, 38)], fill=(90, 92, 98), width=2)
for k, lab in enumerate(['퀘스트', '메뉴 (F10)', '동맹 (F11)', '대화 (F12)', '항법 선택']):
    bx = 4 + k * 168
    bevel(img, (bx, 3, bx + 160, 34), (130, 132, 138), (26, 27, 32), 2, (22, 24, 36))
    text(d, (bx + 80, 19), lab, 17, (210, 218, 235), 'mm')
# 가운데 시계 구슬
ox, oy, r_ = 960, 28, 50
for rr in range(r_, 0, -1):
    t = rr / r_
    col = (int(30 + 60 * (1 - t)), int(60 + 90 * (1 - t)), int(120 + 110 * (1 - t)))
    d.ellipse([ox - rr, oy - rr, ox + rr, oy + rr], fill=col)
d.ellipse([ox - r_, oy - r_, ox + r_, oy + r_], outline=(170, 172, 178), width=5)
d.ellipse([ox - r_ + 5, oy - r_ + 5, ox + r_ - 5, oy + r_ - 5], outline=(60, 62, 68), width=2)
d.ellipse([ox - 22, oy - 26, ox + 6, oy - 6], fill=(190, 220, 255))
# 자원(금·목·특성)
for k, (lab, val, col) in enumerate([('금', '40', (230, 190, 40)), ('목', '1', (70, 170, 70)), ('특', '1', (120, 200, 90))]):
    bx = 1120 + k * 208
    bevel(img, (bx, 3, bx + 196, 34), (130, 132, 138), (26, 27, 32), 2, (8, 8, 12))
    d.ellipse([bx + 8, 8, bx + 28, 28], fill=col, outline=(30, 30, 30))
    text(d, (bx + 188, 19), val, 20, WHITE, 'rm')
text(d, (1890, 19), '구랜디', 17, (120, 190, 230), 'rm')

# ======================================================== 우상단 타이머 창 · 점수판
for (y0, lab, val, redlab) in [(46, '라운드 준비 박진웅', '0:17', True), (92, '다음 스토리 (01. 하이츠)까지', '0:06', False)]:
    bevel(img, (1420, y0, 1900, y0 + 38), (130, 132, 138), (26, 27, 32), 2, (10, 10, 14))
    text(d, (1436, y0 + 19), lab, 19, (255, 80, 80) if redlab else GOLD, 'lm'); text(d, (1884, y0 + 19), val, 21, WHITE, 'rm')
bevel(img, (1420, 138, 1900, 216), (130, 132, 138), (26, 27, 32), 2, (10, 10, 14))
text(d, (1436, 154), '유닛 카운트 = ', 19, (255, 176, 255), 'lm'); text(d, (1566, 154), '70', 19, (80, 255, 80), 'lm'); text(d, (1600, 154), '<- 패배', 19, (255, 176, 255), 'lm')
bevel(img, (1862, 142, 1892, 166), (150, 152, 158), (26, 27, 32), 2, (24, 28, 44)); d.line([(1870, 154), (1884, 154)], fill=WHITE, width=3)
text(d, (1436, 182), '난이도 : 보통모드', 17, (0, 255, 255), 'lm'); text(d, (1700, 182), '남은 라운드 유닛 수', 17, (0, 255, 0), 'lm')
d.rectangle([1436, 196, 1448, 208], fill=(255, 2, 2)); text(d, (1456, 202), '길동이', 17, (255, 2, 2), 'lm'); text(d, (1740, 202), '0', 17, WHITE, 'lm')

# ======================================================== 왼쪽 세로 영웅 줄(체력·마나 막대 달린 작은 초상)
for k in range(6):
    y0 = 50 + k * 86
    bevel(img, (4, y0, 70, y0 + 66), (150, 152, 158), (26, 27, 32), 3, (20, 20, 28))
    g = [(210, 170, 70), (90, 150, 220), (170, 90, 200), (200, 80, 80), (80, 190, 150), (220, 130, 60)][k]
    d.rectangle([8, y0 + 4, 66, y0 + 62], fill=tuple(int(c * .55) for c in g))
    text(d, (37, y0 + 33), '초', 30, g, 'mm')
    d.rectangle([4, y0 + 69, 70, y0 + 74], fill=(0, 0, 0)); d.rectangle([5, y0 + 70, 5 + int(64 * (1 - k * .12)), y0 + 73], fill=(40, 200, 60))
    d.rectangle([4, y0 + 76, 70, y0 + 80], fill=(0, 0, 0)); d.rectangle([5, y0 + 77, 5 + int(64 * (.9 - k * .1)), y0 + 79], fill=(50, 90, 230))

# 안전 라벨
text(d, (W - 12, H - 6), '※ 배치 시안(정적 목업) — 칸 동작은 그대로, 겉모습·배치만', 15, (230, 230, 120), 'rd')
img.save(os.path.join(OUT, 'mock_ours.png'))

# 비교(위: 원작 참고 사진 확대 · 아래: 우리 시안)
if os.path.exists(REF):
    ref = Image.open(REF).convert('RGB').resize((W, int(W * 478 / 850)))
    cmp_img = Image.new('RGB', (W, ref.height + H + 8), (20, 20, 20))
    cmp_img.paste(ref, (0, 0)); cmp_img.paste(img, (0, ref.height + 8))
    cmp_img.save(os.path.join(OUT, 'compare.png'))
print('ok', os.listdir(OUT))
