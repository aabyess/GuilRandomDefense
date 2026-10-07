"""워크3풍 게임 UI 그림 렌더(blender 세션 2026-10-07, 규격표 Docs/design/WC3_UI_ART_SPEC_2026-10-07.md · 구현담당2 의뢰).
블리자드 UI 그림 추출·복제 없음 — 돌·금속·구슬을 전부 새로 모델링해 Cycles로 찍는다. 공통 재질·도우미 = gen_ui_wc3_common.py.
척도: 목표 1px = 0.01m, 납품 = 목표×2 → 렌더 200px/m. 광원은 위(약간 왼쪽·앞) 한 방향.
  blender -b --factory-startup --python Tools/blender/gen_wc3_ui.py -- [출력 ~/GRD_wc3_ui] [샘플 128]   (환경변수 WC3_PARTS="bar,capL,capR,pillar,panel")
  /usr/bin/python3 Tools/blender/gen_wc3_ui.py compare [출력]   → compare_console.png(참고 사진과 같은 구도 나란히)
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
if len(sys.argv) > 1 and sys.argv[1] == 'compare':                       # ---------------- 비교 합성(시스템 파이썬)
    from PIL import Image, ImageDraw
    D = os.path.expanduser(sys.argv[2] if len(sys.argv) > 2 else '~/GRD_wc3_ui')
    L = lambda n: Image.open(f'{D}/{n}.png').convert('RGBA')
    def s9(src, w, h, m):                                                    # 9-slice(m = 납품 여백 px) → 목표 크기(납품/2)
        sw, sh = src.size; out = Image.new('RGBA', (w * 2, h * 2), (0, 0, 0, 0)); W2, H2 = w * 2, h * 2
        xs = [(0, m, 0, m), (m, sw - m, m, W2 - m), (sw - m, sw, W2 - m, W2)]
        for sx0, sx1, dx0, dx1 in xs:
            for sy0, sy1, dy0, dy1 in xs if sw == sh else [(0, m, 0, m), (m, sh - m, m, H2 - m), (sh - m, sh, H2 - m, H2)]:
                if dx1 > dx0 and dy1 > dy0: out.alpha_composite(src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS), (dx0, dy0))
        return out.resize((w, h), Image.LANCZOS)
    ours = Image.new('RGBA', (1920, 1080), (88, 92, 70, 255))               # 월드 자리(참고 사진의 풀밭 톤)
    bar = L('console_bar_tile').resize((256, 285), Image.LANCZOS)
    for x in range(0, 1920, 256): ours.alpha_composite(bar, (x, 795))
    ours.alpha_composite(L('console_cap_left').resize((70, 285), Image.LANCZOS), (0, 795)); ours.alpha_composite(L('console_cap_right').resize((70, 285), Image.LANCZOS), (1850, 795))
    pf = L('panel_frame_stone')
    d = ImageDraw.Draw(ours)
    for (x0, y0, x1, y1) in ((16, 809, 368, 1066), (750, 809, 1215, 1080), (1468, 815, 1915, 1075)):
        d.rectangle((x0 + 20, y0 + 20, x1 - 20, y1 - 20), fill=(0, 0, 0, 255)); ours.alpha_composite(s9(pf, x1 - x0, y1 - y0, 56), (x0, y0))
    d.rectangle((440 + 14, 858, 705 - 14, 1080), fill=(0, 0, 0, 255))      # 초상 자리(아치는 다음 덩어리)
    pl = L('stone_pillar')
    for (x0, x1) in ((368, 440), (705, 750), (1215, 1238), (1418, 1468)):
        w = x1 - x0; src = pl; m = 16
        ours.alpha_composite(s9(src, w, 285, m) if False else Image.new('RGBA', (1, 1)), (0, 0))
        img = Image.new('RGBA', (w * 2, 570)); img.alpha_composite(src.crop((0, 0, m, 570)), (0, 0)); img.alpha_composite(src.crop((m, 0, 160 - m, 570)).resize((max(1, w * 2 - 2 * m), 570), Image.LANCZOS), (m, 0)); img.alpha_composite(src.crop((160 - m, 0, 160, 570)), (w * 2 - m, 0))
        ours.alpha_composite(img.resize((w, 285), Image.LANCZOS), (x0, 795))
    ref = Image.open('/tmp/claude-501/ref_original_ui.png').convert('RGBA').resize((1920, 1080), Image.LANCZOS)
    out = Image.new('RGBA', (1920, 2 * 330 + 20), (20, 20, 24, 255))
    out.alpha_composite(ref.crop((0, 750, 1920, 1080)), (0, 0)); out.alpha_composite(ours.crop((0, 750, 1920, 1080)), (0, 350))
    dd = ImageDraw.Draw(out); dd.text((8, 4), 'REF (original, scaled)', fill=(255, 255, 0, 255)); dd.text((8, 354), 'OURS (new render)', fill=(255, 255, 0, 255))
    out.convert('RGB').save(f'{D}/compare_console.png'); ours.convert('RGB').save(f'{D}/preview_console_full.png'); print('compare saved'); sys.exit(0)

import math, random
exec(open(os.path.join(HERE, 'gen_ui_wc3_common.py'), encoding='utf-8').read())
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if argv else '~/GRD_wc3_ui'); os.makedirs(OUT, exist_ok=True)
SAMPLES = int(argv[1]) if len(argv) > 1 else 128
PPM = 200.0                                    # 납품 px / m (목표 1px = 1cm, 납품 2배)
PARTS = os.environ.get('WC3_PARTS', 'bar,capL,capR,pillar,panel').split(',')

STONE_TONES = [(.075, .072, .07), (.064, .062, .064), (.086, .08, .075), (.058, .057, .06), (.079, .075, .072), (.069, .065, .063)]
RUST_LOW = .9
def stone_set(sc, prefix, n=6, moss=.55, rust_every=4, light=1.0, rust_low=None):
    return [stone_mat(f'{prefix}{i}', base=STONE_TONES[i % 6], seed=i + hash(prefix) % 97, moss=moss, rust=(.75 if i % rust_every == 0 else 0.0), light=light, rust_low=(RUST_LOW if rust_low is None else rust_low)) for i in range(n)]
def mortar_mat(): return simple('줄눈', (.012, .012, .014), .95)

def courses(x0, x1, z0, z1, seed, row_h=(.24, .31), blen=(.5, .85), offset=True):
    """돌 줄 배치(running bond). 가로 구간 [x0,x1]을 정확히 채운다."""
    r = random.Random(seed); out = []; z = z0; row = 0
    while z < z1 - 1e-6:
        h = min(r.uniform(*row_h), z1 - z)
        if z1 - (z + h) < .08: h = z1 - z
        x = x0; first = True
        while x < x1 - 1e-6:
            L = r.uniform(*blen) * (.55 if (first and offset and row % 2) else 1.0)
            if x1 - (x + L) < .14: L = x1 - x
            out.append((x, min(x + L, x1), z, z + h)); x += L; first = False
        z += h; row += 1
    return out

def place_blocks(sc, blocks, mats, depth=(.14, .22), y_front=0.0, gap=.026, chip=.04, tag='b', seed=0, xshift=0.0):
    r = random.Random(seed)
    for i, (x0, x1, z0, z1) in enumerate(blocks):
        d = r.uniform(*depth); sx = x1 - x0 - gap; sz = z1 - z0 - gap
        rough_block(sc, f'{tag}{i}', (sx, d, sz), (xshift + (x0 + x1) / 2, y_front + d / 2 - r.uniform(0, .02), (z0 + z1) / 2), mats[r.randrange(len(mats))], seed * 1000 + i, chip=chip)

def merlons(sc, x0, x1, z0, h, mats, w=.42, gapw=.22, seed=9, y_front=-.04, xshift=0.0, first_offset=None):
    """총안 톱니: [x0,x1] 안에 주기 (w+gapw)로 merlon. 각 merlon = 몸 돌 + 위 덮개."""
    per = w + gapw; x = x0 + (gapw / 2 if first_offset is None else first_offset); j = 0; r = random.Random(seed)
    while x + w <= x1 + 1e-6:
        cx = xshift + x + w / 2
        rough_block(sc, f'mer{seed}_{j}', (w - .012, .22, h - .055), (cx, y_front + .11, z0 + (h - .055) / 2), mats[j % len(mats)], seed * 100 + j, chip=.03)
        rough_block(sc, f'mcap{seed}_{j}', (w + .02, .25, .06), (cx, y_front + .1, z0 + h - .03), mats[(j + 2) % len(mats)], seed * 100 + 50 + j, chip=.018)
        x += per; j += 1

# ------------------------------------------------------------------ 1. console_bar_tile (가로 타일 2.56 × 2.85, 위 0.24 총안)
def part_bar():
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    W, H, CZ = 2.56, 2.85, .24; body_top = H - CZ
    mats = stone_set(sc, '벽돌'); capm = stone_set(sc, '띠돌', 3, moss=.3, light=1.1)
    body = courses(-W / 2, W / 2, 0, body_top - .11, seed=11)
    ledge = courses(-W / 2, W / 2, body_top - .11, body_top, seed=12, row_h=(.11, .11), blen=(.5, .8))
    for k in (-1, 0, 1):                                      # 주기 복제 → 이음새 없음
        mkbox(sc, 'mortar', (W, .1, body_top), (k * W, .2, body_top / 2), mortar_mat())
        place_blocks(sc, body, mats, tag=f'b{k}_', seed=11, xshift=k * W)
        place_blocks(sc, ledge, capm, depth=(.22, .24), y_front=-.05, tag=f'l{k}_', seed=12, xshift=k * W, chip=.02)
        merlons(sc, -W / 2, W / 2, body_top, CZ, capm, seed=13, xshift=k * W)
    ortho_cam(sc, 0, H / 2, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/console_bar_tile.png')

# ------------------------------------------------------------------ 2. console_cap_left/right (0.70 × 2.85, 바깥 모서리 탑)
def part_cap(side):
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    W, H, CZ = .70, 2.85, .24; body_top = H - CZ
    mats = stone_set(sc, '모서리돌'); capm = stone_set(sc, '모서리띠', 3, moss=.3, light=1.1)
    sg = -1 if side == 'L' else 1                              # 바깥쪽 방향
    x0, x1 = -W / 2, W / 2
    mkbox(sc, 'mortar', (W - .04, .1, body_top), (-sg * .02, .2, body_top / 2), mortar_mat())
    # 모서리 귀돌(quoin): 바깥 기둥처럼 크게 엇갈려 쌓은 돌(앞으로 더 튀어나옴)
    r = random.Random(21 if side == 'L' else 22); z = 0; i = 0
    while z < body_top - .11:
        h = min(r.uniform(.22, .3), body_top - .11 - z); L = .5 if i % 2 == 0 else .36
        cx = sg * (W / 2 - L / 2 - .01)
        rough_block(sc, f'q{i}', (L - .016, .26, h - .016), (cx, -.08 + .13, z + h / 2), mats[i % 6], 300 + i, chip=.035)
        # 안쪽 나머지는 보통 벽돌
        rest_w = W - L - .02
        if rest_w > .05: rough_block(sc, f'qi{i}', (rest_w - .016, .17, h - .016), (-sg * (W / 2 - rest_w / 2), .085, z + h / 2), mats[(i + 3) % 6], 400 + i, chip=.028)
        z += h; i += 1
    rough_block(sc, 'capledge', (W + .02, .3, .11), (sg * .01, -.06 + .15, body_top - .055), capm[0], 450, chip=.02)
    merlons(sc, x0, x1, body_top, CZ, capm, w=.42, gapw=.22, seed=31 if side == 'L' else 32, y_front=-.08, first_offset=(0.0 if side == 'L' else W - .42))
    ortho_cam(sc, 0, H / 2, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/console_cap_{"left" if side == "L" else "right"}.png')

# ------------------------------------------------------------------ 3. stone_pillar (0.80 × 2.85, 앞으로 튀어나온 돌 기둥 + 총안 캡)
def part_pillar():
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    W, H, CZ = .80, 2.85, .24; body_top = H - CZ
    mats = stone_set(sc, '기둥돌', light=1.05); capm = stone_set(sc, '기둥띠', 3, moss=.3, light=1.15)
    r = random.Random(41); z = 0; i = 0
    while z < body_top - .12:
        h = min(r.uniform(.2, .27), body_top - .12 - z); L = .78 if i % 2 == 0 else .7
        rough_block(sc, f'p{i}', (L - .016, .3, h - .016), (0, -.12 + .15, z + h / 2), mats[i % 6], 500 + i, chip=.034); z += h; i += 1
    mkbox(sc, 'mortar', (W - .06, .1, body_top), (0, .25, body_top / 2), mortar_mat())
    rough_block(sc, 'pcap', (W, .34, .12), (0, -.15 + .17, body_top - .06), capm[0], 590, chip=.02)
    rough_block(sc, 'pmer', (.5, .3, CZ - .06), (0, -.12 + .15, body_top + (CZ - .06) / 2), capm[1], 591, chip=.03)
    rough_block(sc, 'pmc', (.56, .33, .06), (0, -.13 + .16, H - .03), capm[2], 592, chip=.018)
    ortho_cam(sc, 0, H / 2, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/stone_pillar.png')

# ------------------------------------------------------------------ 4. panel_frame_stone (1.2 × 1.2, 테두리 0.28, 가운데 투명, 안쪽 어두운 홈)
def part_panel():
    """미니맵·정보창·명령 카드 바깥 돌 테. 9-slice로 늘리므로 변은 '깎은 돌 몰딩'(늘려도 티 안 남), 모서리만 깨진 돌덩이."""
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    S, B = 1.2, .28
    mats = stone_set(sc, '틀돌', moss=.3, rust_low=0.0)
    mort = mortar_mat()
    for (sx, sz, x, z) in ((S, B, 0, S / 2 - B / 2), (S, B, 0, -S / 2 + B / 2), (B, S, -S / 2 + B / 2, 0), (B, S, S / 2 - B / 2, 0)): mkbox(sc, 'back', (sx, .05, sz), (x, .25, z), mort)
    # 변: 바깥쪽이 높고 안쪽으로 비스듬히 깎인 몰딩(안쪽 가장자리 쪽이 어둡게 내려감)
    for (sx, sz, x, z, rx) in ((S, B, 0, S / 2 - B / 2, 0), (S, B, 0, -S / 2 + B / 2, 0), (B, S, -S / 2 + B / 2, 0, 1), (B, S, S / 2 - B / 2, 0, 1)):
        ob = mkbox(sc, 'rim', (sx - .02, .16, sz - .02), (x, .08, z), mats[0 if rx == 0 else 1], bevel=.06); ob.modifiers['b'].segments = 4
    for (x, z) in ((-1, 1), (1, 1), (-1, -1), (1, -1)):                    # 모서리 돌덩이(조금 더 튀어나옴)
        rough_block(sc, f'corner{x}{z}', (B + .02, .22, B + .02), (x * (S / 2 - B / 2), .06, z * (S / 2 - B / 2)), mats[2 + (x + z) % 3], 900 + x * 3 + z, chip=.04)
    groove = simple('홈', (.003, .003, .004), .9); g = .03
    for (sx, sz, x, z) in ((S - 2 * B + 2 * g, g, 0, S / 2 - B + g / 2 - .004), (S - 2 * B + 2 * g, g, 0, -S / 2 + B - g / 2 + .004), (g, S - 2 * B, -S / 2 + B - g / 2 + .004, 0), (g, S - 2 * B, S / 2 - B + g / 2 - .004, 0)):
        mkbox(sc, 'groove', (sx, .17, sz), (x, .085, z), groove, bevel=.004)
    ortho_cam(sc, 0, 0, S, int(S * PPM), int(S * PPM)); render(sc, f'{OUT}/panel_frame_stone.png')

for p in PARTS:
    {'bar': part_bar, 'capL': lambda: part_cap('L'), 'capR': lambda: part_cap('R'), 'pillar': part_pillar, 'panel': part_panel}[p]()
