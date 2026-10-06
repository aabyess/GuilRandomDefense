"""첫 화면(로비) 워크3풍 UI 재료 — 단추 9-slice 3종 · 메뉴 틀 9-slice · 제목 판(2026-10-06, blender 세션).
   전부 절차 생성(numpy+Pillow, 외부 에셋 없음). 시스템 파이썬으로 돈다:  /usr/bin/python3 Tools/blender/gen_lobby_ui.py [출력폴더]
   기본 출력 ~/GRD_lobby_art/ui/  (Assets엔 안 씀)
     btn_normal.png · btn_hover.png · btn_pressed.png   512×128   9-slice 테두리(보더) 좌우 40 · 상하 40
     menu_frame.png                                      512×1024  9-slice 테두리 좌우 64 · 상 160 · 하 160
     title_plate.png                                     1400×300 투명 — 글자 없음(유니티 폰트로 얹는다)
   모양: 어두운 철판 + 청동 띠 + 얇은 금선 + 모서리 리벳(귀 잘린 사각), 호버는 테두리 발광, 눌림은 음영 반전.
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/GRD_lobby_art/ui')
os.makedirs(OUT, exist_ok=True)
rng = np.random.RandomState(11)


def c(r, g, b): return np.array([r, g, b], np.float32) / 255.0


def lerp(a, b, t): return a + (b - a) * t[..., None]


def ramp(t, stops):
    """t(0..1 배열) → 색. stops=[(위치,색),…]"""
    t = np.clip(t, 0, 1); out = np.zeros(t.shape + (3,), np.float32)
    for (a0, c0), (a1, c1) in zip(stops[:-1], stops[1:]):
        m = (t >= a0) & (t <= a1)
        w = ((t - a0) / (a1 - a0 + 1e-9))[..., None]
        out = np.where(m[..., None], c0 * (1 - w) + c1 * w, out)
    return out


def blur(a, r):
    if a.ndim == 2:
        im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)); return np.asarray(im.filter(ImageFilter.GaussianBlur(r)), np.float32) / 255
    return np.stack([blur(a[..., i], r) for i in range(a.shape[-1])], -1)


def noise(h, w, scale, seed, aniso=(1, 1)):
    r = np.random.RandomState(seed); n = r.rand(max(2, int(h / scale / aniso[1])), max(2, int(w / scale / aniso[0]))).astype(np.float32)
    im = Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(im, np.float32) / 255


def fbm(h, w, base, seed, octs=4, aniso=(1, 1)):
    t = 0; a = 1; tot = 0
    for o in range(octs):
        t = t + a * noise(h, w, base / 2 ** o, seed + o, aniso); tot += a; a *= .5
    return t / tot


def edt(mask):
    """안쪽 거리(마스크 밖 0, 가장자리 바로 안쪽 ≈1) — scipy 없이 두 번 훑는 3-4 근사."""
    h, w = mask.shape; INF = 1e6
    d = np.where(mask, INF, 0.0).astype(np.float32)
    for y in range(h):
        if y > 0:
            d[y] = np.minimum(d[y], d[y - 1] + 1)
            d[y, 1:] = np.minimum(d[y, 1:], d[y - 1, :-1] + 1.4142); d[y, :-1] = np.minimum(d[y, :-1], d[y - 1, 1:] + 1.4142)
        for x in range(1, w): d[y, x] = min(d[y, x], d[y, x - 1] + 1)
        for x in range(w - 2, -1, -1): d[y, x] = min(d[y, x], d[y, x + 1] + 1)
    for y in range(h - 2, -1, -1):
        d[y] = np.minimum(d[y], d[y + 1] + 1)
        d[y, 1:] = np.minimum(d[y, 1:], d[y + 1, :-1] + 1.4142); d[y, :-1] = np.minimum(d[y, :-1], d[y + 1, 1:] + 1.4142)
        for x in range(1, w): d[y, x] = min(d[y, x], d[y, x - 1] + 1)
        for x in range(w - 2, -1, -1): d[y, x] = min(d[y, x], d[y, x + 1] + 1)
    return np.where(mask, np.minimum(d, 4000), 0).astype(np.float32)


def cham_dist(h, w, cut):
    """귀 잘린 사각형 안쪽 거리(가장자리=0, 안쪽 +). cut = 모서리를 자르는 크기(px)"""
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    dx = np.minimum(x + .5, w - x - .5); dy = np.minimum(y + .5, h - y - .5)
    return np.minimum(np.minimum(dx, dy), (dx + dy - cut) / 1.4142)


def shade_from(d, light=(-1, -1)):
    """거리장에서 바깥 법선 · 빛(왼쪽 위) 내적. 위·왼 모서리 +, 아래·오른 −"""
    gy, gx = np.gradient(blur(d, 0.8))
    nx, ny = -gx, -gy
    L = np.array(light, np.float32); L /= np.linalg.norm(L)
    return nx * L[0] + ny * L[1]          # 법선(바깥)·빛 방향 — 빛 쪽을 향한 면이 +


def brushed(h, w, seed, strength=.12):
    return (fbm(h, w, 40, seed, 3, (8, 1)) - .5) * strength + (noise(h, w, 1.2, seed + 9) - .5) * strength * .5


def plate(h, w, base, seed, pressed=False, warm=0.0):
    """어두운 철판: 푸른빛 도는 강철 + 결 + 얼룩 + 안쪽 비네트"""
    steel = ramp(fbm(h, w, 90, seed, 4), [(0, c(24, 26, 31)), (.5, c(40, 43, 50)), (1, c(58, 61, 68))])
    steel = steel + brushed(h, w, seed + 3)[..., None]
    if warm: steel = steel + np.array([.10, .035, 0], np.float32) * warm
    g = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    steel = steel * (1.08 - .3 * g)[..., None] if not pressed else steel * (.78 + .2 * g)[..., None]
    return steel * base


def rivet(img, cx, cy, r, press=False):
    """리벳: 어두운 구멍 테 + 반구 + 하이라이트"""
    h, w = img.shape[:2]
    y0, y1 = int(max(0, cy - r - 3)), int(min(h, cy + r + 4)); x0, x1 = int(max(0, cx - r - 3)), int(min(w, cx + r + 4))
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    d = np.sqrt((xx + .5 - cx) ** 2 + (yy + .5 - cy) ** 2)
    ring = np.clip(1 - (d - r) / 2.0, 0, 1) * np.clip((d - r + 2.2) / 2.2, 0, 1)         # 어두운 테
    body = np.clip((r - d) / 1.2, 0, 1)
    nxl = (xx + .5 - cx) / r; nyl = (yy + .5 - cy) / r
    lit = np.clip(.55 - .55 * (nxl * -.6 + nyl * -.8) * (1 if not press else -1), 0, 1)
    spec = np.exp(-(((xx + .5 - cx + r * .35) ** 2 + (yy + .5 - cy + r * .38) ** 2) / (r * .35) ** 2)) * .7
    col = ramp(lit, [(0, c(48, 30, 16)), (.55, c(168, 118, 56)), (1, c(236, 196, 120))]) + spec[..., None] * np.array([.9, .8, .6], np.float32)
    sub = img[y0:y1, x0:x1]
    sub[:] = sub * (1 - ring[..., None] * .8)
    sub[:] = lerp(sub, np.clip(col, 0, 1), body)


def metal_frame(img, d, bw, seed, mode='normal'):
    """바깥에서 안으로: 검은 외곽선 → 청동 띠(경사) → 어두운 홈 → 금선 → 안쪽 그림자."""
    h, w = d.shape
    sh = shade_from(d)                                   # +: 빛 받는 쪽
    inv = -1 if mode == 'pressed' else 1
    # 구간
    o0 = 1.6                                             # 외곽선
    b0, b1 = o0, o0 + bw                                 # 청동 띠
    g0 = b1 + 0.8                                        # 금선
    g1 = g0 + 2.2
    bronze_t = np.clip((d - b0) / bw, 0, 1)
    metal = fbm(h, w, 6, seed + 40, 3)
    ridge = np.sin(np.pi * bronze_t) * .22                      # 띠 가운데가 볼록한 둥근 단면
    bronze = ramp(.5 + sh * 1.25 * inv + ridge + (metal - .5) * .4 + (.5 - bronze_t) * .1,
                  [(0, c(52, 30, 16)), (.35, c(124, 80, 38)), (.62, c(190, 134, 66)), (1, c(246, 206, 130))])
    if mode == 'hover': bronze = bronze * 1.18 + np.array([.10, .05, 0], np.float32)
    gold = ramp(.5 + sh * .9 * inv, [(0, c(120, 82, 22)), (.5, c(226, 176, 66)), (1, c(255, 232, 150))])
    if mode == 'hover': gold = np.clip(gold * 1.15 + .08, 0, 1)
    out = np.where((d < o0)[..., None], c(8, 6, 6), img)
    inb = ((d >= b0) & (d < b1))[..., None]
    out = np.where(inb, bronze, out)
    # 띠 안쪽 어두운 홈
    groove = ((d >= b1) & (d < g0))[..., None]
    out = np.where(groove, c(14, 10, 8), out)
    gl = ((d >= g0) & (d < g1))[..., None]
    out = np.where(gl, gold, out)
    # 금선 안쪽 그림자(판이 움푹 들어간 느낌)
    sd = np.clip(1 - (d - g1) / 14.0, 0, 1) ** 1.6
    out = out * (1 - sd[..., None] * (.55 if mode != 'pressed' else .75))
    return out


def glow(img, d, color, radius, strength):
    """테두리 바깥으로 번지는 빛(호버) — d<=0 바깥은 알파로 처리하지 않고 안쪽 가장자리에서 빛"""
    inner = np.clip(1 - d / radius, 0, 1) ** 2.2
    return np.clip(img + inner[..., None] * color * strength, 0, 1)


def button(mode, w=512, h=128):
    seed = 5
    pressed = mode == 'pressed'
    d = cham_dist(h, w, 26)
    base = plate(h, w, 1.0 if mode != 'hover' else 1.12, seed, pressed, warm=.5 if mode == 'hover' else 0)
    img = metal_frame(base.copy(), d, 13, seed, mode)
    # 안쪽 가운데 살짝 오목한 광택(위쪽 밝음)
    y = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
    inside = np.clip((d - 20) / 6, 0, 1)
    if pressed: img = img * (1 - inside[..., None] * .22)
    else: img = img + (inside * (1 - y) ** 2 * (.07 if mode != 'hover' else .11))[..., None] * np.array([1, .95, .85], np.float32)
    if mode == 'hover': img = glow(img, d, c(255, 150, 40), 34, .55)
    # 리벳 네 귀
    for (cx, cy) in ((24, 24), (w - 24, 24), (24, h - 24), (w - 24, h - 24)):
        rivet(img, cx, cy, 6.5, pressed)
    # 귀 잘린 밖은 투명
    a = np.clip((d + .5) / 1.0, 0, 1)
    out = np.concatenate([np.clip(img, 0, 1), a[..., None]], -1)
    return Image.fromarray((out * 255).astype(np.uint8))


# ---------------------------------------------------------------- 룬
RUNES = [  # 간단한 선 룬(0..1 좌표 선분들)
    [((.5, 0), (.5, 1)), ((.5, .25), (.85, .45)), ((.5, .5), (.85, .7))],                     # ᚦ 류
    [((.3, 0), (.3, 1)), ((.3, .15), (.75, .4)), ((.75, .4), (.3, .65))],                      # ᚱ 류
    [((.5, 0), (.5, 1)), ((.15, .3), (.5, .12)), ((.85, .3), (.5, .12))],                      # ᛏ 류
    [((.2, 0), (.2, 1)), ((.2, .5), (.8, .1)), ((.2, .5), (.8, .9))],                          # ᚲ 류
    [((.5, 0), (.5, 1)), ((.15, .15), (.85, .45)), ((.85, .15), (.15, .45))],                  # ᚷ 류
    [((.25, 0), (.25, 1)), ((.75, 0), (.75, 1)), ((.25, .3), (.75, .7))],                      # ᚺ 류
    [((.5, 0), (.5, 1)), ((.5, .15), (.15, .5)), ((.5, .15), (.85, .5)), ((.5, .5), (.15, .85))],  # ᛉ 류
]


def draw_rune(dr, x, y, s, k, fill, wdt=3):
    for (ax, ay), (bx, by) in RUNES[k % len(RUNES)]:
        dr.line([(x + ax * s * .6, y + ay * s), (x + bx * s * .6, y + by * s)], fill=fill, width=wdt)


def rune_row(img, x0, x1, y, size, spacing, start=0, color=(214, 160, 70, 255)):
    """금속 위에 새긴 룬: 어두운 홈 + 밝은 아랫변"""
    h, w = img.shape[:2]
    layer = Image.new('L', (w, h), 0); dr = ImageDraw.Draw(layer)
    k = start; x = x0
    while x + size * .6 <= x1:
        draw_rune(dr, x, y, size, k, 255, 3); k += 3; x += spacing
    m = np.asarray(layer, np.float32) / 255
    m = blur(m, 0.7)
    shadow = np.roll(np.roll(m, 1, 0), 1, 1)
    out = img * (1 - m[..., None] * .55)
    glow_ = np.clip(shadow - m, 0, 1)
    out = out + glow_[..., None] * c(255, 215, 140) * .6
    out = lerp(out, np.array(color[:3], np.float32) / 255, m * .45)
    return out


# ---------------------------------------------------------------- 메뉴 틀
def menu_frame(w=512, h=1024):
    seed = 21
    d = cham_dist(h, w, 54)
    # 안쪽: 가죽 + 철 — 위·아래 160은 룬판, 가운데는 가죽
    y = np.arange(h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
    leather = ramp(fbm(h, w, 14, seed, 4) * .8 + noise(h, w, 2.2, seed + 5) * .2,
                   [(0, c(30, 18, 12)), (.5, c(52, 33, 22)), (1, c(78, 52, 34))])
    leather = leather * (.9 + .25 * fbm(h, w, 160, seed + 8, 2))[..., None]
    iron = plate(h, w, 1.0, seed + 2)
    img = leather.copy()
    # 철 테두리 폭: 좌우 40, 위·아래 160
    border = np.minimum(np.minimum(np.arange(w)[None, :], w - 1 - np.arange(w)[None, :]) * np.ones((h, 1)), 1e9)
    band_x = border < 38
    band_top = (y < 150) | (y > h - 150)
    img = np.where((band_x | band_top)[..., None], iron, img)
    img = metal_frame(img, d, 15, seed, 'normal')
    # 위·아래 룬판: 청동 안쪽 띠 두 줄 + 룬 한 줄
    for yb, flip in ((0, 1), (h, -1)):
        for off, th in ((26, 4), (126, 4)):
            yy = yb + flip * off
            band = (np.abs(y - yy) < th) & (d > 14)
            ln = ramp(.5 + .4 * (np.abs(y - yy) / th - .5) * -flip, [(0, c(110, 72, 32)), (.5, c(210, 160, 74)), (1, c(250, 214, 130))])
            img = np.where(band[..., None], ln, img)
    img = rune_row(img, 70, w - 70, 64, 40, 54, 0)
    img = rune_row(img, 70, w - 70, h - 64 - 40, 40, 54, 3)
    # 가죽 안쪽 가장자리 바느질 점선
    inner = cham_dist(h, w, 54)
    stitch = (np.abs(inner - 52) < 1.3) & (((np.arange(h)[:, None] // 7) + (np.arange(w)[None, :] // 7)) % 2 == 0) & (y > 160) & (y < h - 160)
    img = np.where(stitch[..., None], c(200, 160, 100), img)
    inleather = (inner > 56) & (y > 160) & (y < h - 160)
    # 가죽 안쪽 그림자(철틀 쪽 어둡게)
    edge = np.clip(1 - (inner - 56) / 26, 0, 1) ** 2
    img = np.where(inleather[..., None], img * (1 - edge[..., None] * .55), img)
    # 모서리 큰 리벳 + 변 중간 리벳
    for (cx, cy) in ((30, 30), (w - 30, 30), (30, h - 30), (w - 30, h - 30)):
        rivet(img, cx, cy, 10)
    for yy in np.linspace(230, h - 230, 8):
        rivet(img, 20, yy, 5); rivet(img, w - 20, yy, 5)
    a = np.clip((d + .5) / 1.0, 0, 1)
    out = np.concatenate([np.clip(img, 0, 1), a[..., None]], -1)
    return Image.fromarray((out * 255).astype(np.uint8))


# ---------------------------------------------------------------- 제목 판
def title_plate(w=1400, h=300):
    seed = 31
    # 가운데는 길쭉한 명판, 양끝은 날개(뾰족)처럼 빠지는 모양: 알파 = 명판 실루엣
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    cx, cy = w / 2, h / 2
    # 명판 본체: 높이 150(가운데 ±75), 양끝에서 날개 장식 ±
    body_half = 78.0
    body = (np.abs(y - cy) < body_half) & (np.abs(x - cx) < w * .5 - 190)
    # 양끝 날개: 마름모 꼴로 위아래로 솟는 장식
    def wing(side):
        xs = x if side > 0 else w - x
        # 끝 점(190,150)에서 안쪽으로 퍼지는 장식(위·아래 뾰족)
        t = (xs - 20) / 330.0
        hh = 130 * np.sin(np.clip(t, 0, 1) * np.pi / 1.7) * (1 - .15 * np.clip(t, 0, 1))
        return (xs > 20) & (xs < 350) & (np.abs(y - cy) < np.maximum(hh * .62, 0) + 14 * np.clip(t, 0, 1))
    mask = body | wing(1) | wing(-1)
    m = blur(mask.astype(np.float32), 1.2)
    # 거리장(마스크 안쪽 거리)
    d = edt(mask)
    base = plate(h, w, 1.0, seed)
    img = metal_frame(base, d, 16, seed, 'normal')
    # 안쪽 룬 띠(위·아래 선 + 가운데 비움: 글자 자리)
    for yy in (cy - 58, cy + 58):
        band = (np.abs(y - yy) < 2.5) & (d > 16) & (np.abs(x - cx) < w * .5 - 215)
        img = np.where(band[..., None], c(200, 150, 64), img)
    img = rune_row(img, 250, 560, cy - 50, 26, 40, 0)
    img = rune_row(img, w - 560, w - 250, cy - 50, 26, 40, 2)
    img = rune_row(img, 250, 560, cy + 28, 26, 40, 4)
    img = rune_row(img, w - 560, w - 250, cy + 28, 26, 40, 5)
    # 가운데 글자 자리는 약간 어둡게(글자 대비)
    mid = np.exp(-(((x - cx) / (w * .26)) ** 4 + ((y - cy) / 54) ** 4))
    img = img * (1 - mid[..., None] * .35)
    # 양끝 큰 보석(루비빛 횃불 색) + 리벳
    for sx in (150, w - 150):
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32); r = 30
        dd = np.sqrt((xx - sx) ** 2 + (yy - cy) ** 2)
        ring = ((dd < r + 7) & (dd > r)); gem = dd <= r
        img = np.where(ring[..., None], c(210, 160, 70), img)
        t = np.clip(1 - dd / r, 0, 1)
        gcol = ramp(t ** .7 * .8 + .1 + ((xx - sx) * -.35 + (yy - cy) * -.5) / r * .15, [(0, c(70, 8, 8)), (.5, c(190, 30, 20)), (1, c(255, 150, 90))])
        img = np.where(gem[..., None], gcol, img)
        spec = np.exp(-(((xx - sx + 9) ** 2 + (yy - cy + 10) ** 2) / 40.0))
        img = np.where(gem[..., None], np.clip(img + spec[..., None] * .8, 0, 1), img)
    for (rx, ry) in ((300, cy - 66), (w - 300, cy - 66), (300, cy + 66), (w - 300, cy + 66)):
        rivet(img, rx, ry, 6)
    # 외곽 아래 그림자(투명 PNG 안에 부드러운 그림자 알파)
    a_body = np.clip(d / 1.2, 0, 1)
    shadow = np.roll(blur(mask.astype(np.float32), 10), 12, 0) * .6
    alpha = np.maximum(a_body, np.clip(shadow, 0, 1) * (1 - a_body) * 0.0 + shadow * (1 - a_body))
    col = np.where((a_body > 0.01)[..., None], np.clip(img, 0, 1), np.zeros(3, np.float32))
    out = np.concatenate([col, alpha[..., None]], -1)
    return Image.fromarray((out * 255).astype(np.uint8))


if __name__ == '__main__':
    for m in ('normal', 'hover', 'pressed'):
        button(m).save(os.path.join(OUT, 'btn_%s.png' % m))
    menu_frame().save(os.path.join(OUT, 'menu_frame.png'))
    title_plate().save(os.path.join(OUT, 'title_plate.png'))
    print('saved', OUT)
