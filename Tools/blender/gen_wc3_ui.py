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
    bar = L('console_bar_tile').resize((256, 285), Image.LANCZOS); tall = L('console_bar_tile_tall').resize((256, 299), Image.LANCZOS)
    for x in range(0, 1920, 256):
        layer = Image.new('RGBA', (1920, 1080)); layer.alpha_composite(bar, (x, 795)); ours.alpha_composite(layer)
    for (xa, xb) in ((0, 440), (1418, 1920)):                                 # 미니맵·명령 카드 구역 = 높은 타일
        layer = Image.new('RGBA', (1920, 1080))
        for x in range(0, 1920, 256): layer.alpha_composite(tall, (x, 781))
        ours.paste(Image.new('RGBA', (xb - xa, 1080 - 760), (88, 92, 70, 255)), (xa, 760)); ours.alpha_composite(layer.crop((xa, 0, xb, 1080)), (xa, 0))
    
    pf = L('panel_frame_stone')
    d = ImageDraw.Draw(ours)
    has_ip = os.path.exists(f'{D}/info_panel_frame.png'); has_cmd = os.path.exists(f'{D}/command_grid_frame.png')
    for (x0, y0, x1, y1) in ((16, 809, 368, 1066), (750, 809, 1215, 1080), (1468, 815, 1915, 1075)):
        d.rectangle((x0 + 12, y0 + 12, x1 - 12, y1 - 12), fill=(0, 0, 0, 255))
        if x0 == 750 and has_ip:
            ip = L('info_panel_frame'); ours.alpha_composite(s9(ip, x1 - x0, y1 - y0, 40) if ip.size[0] != ip.size[1] and False else ip.resize((x1 - x0, y1 - y0), Image.LANCZOS), (x0, y0))
        elif x0 == 1468 and has_cmd:
            cw, ch = 111, 86
            for j in range(3):
                for i in range(4):
                    nm = 'command_cell_hover' if (i, j) == (1, 0) else ('command_cell_pressed' if (i, j) == (2, 1) else 'command_cell')
                    ours.alpha_composite(L(nm).resize((cw, ch), Image.LANCZOS), (x0 + 1 + i * cw, y0 + 1 + j * ch))
            ours.alpha_composite(L('command_grid_frame').resize((x1 - x0, y1 - y0), Image.LANCZOS), (x0, y0))
        else: ours.alpha_composite(s9(pf, x1 - x0, y1 - y0, 32), (x0, y0))
    if os.path.exists(f'{D}/inventory_cell.png'):
        ours.alpha_composite(L('inventory_title').resize((180, 30), Image.LANCZOS), (1238, 836))
        for j in range(3):
            for i in range(2):
                ours.alpha_composite(L('inventory_cell' if (i + j) % 3 else 'inventory_cell_filled').resize((86, 86), Image.LANCZOS), (1238 + i * 94, 874 + j * 62 if False else 874 + j * 61))
    if os.path.exists(f'{D}/portrait_arch_frame.png'):
        mk = L('portrait_arch_mask').resize((265, 222), Image.LANCZOS); blk = Image.new('RGBA', (265, 222), (0, 0, 0, 255)); blk.putalpha(mk.split()[0])
        ours.alpha_composite(blk, (440, 858)); ours.alpha_composite(L('portrait_arch_frame').resize((265, 222), Image.LANCZOS), (440, 858))
    else: d.rectangle((440 + 14, 858, 705 - 14, 1080), fill=(0, 0, 0, 255))
    if os.path.exists(f'{D}/info_title_strip.png'):
        ours.alpha_composite(L('info_title_strip').resize((440, 30), Image.LANCZOS), (762, 814)); ours.alpha_composite(L('info_level_strip').resize((440, 18), Image.LANCZOS), (762, 848))
        for (x, y) in ((752, 863), (976, 906)): ours.alpha_composite(L('icon_slot_gold').resize((48, 48), Image.LANCZOS), (x, y))
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
    if os.path.exists(f'{D}/clock_orb.png'):                                 # 상단 바 비교
        top = Image.new('RGBA', (1920, 200), (88, 92, 70, 255)); btn = L('topbar_button')
        for x in (7, 215, 418, 621): top.alpha_composite(btn.resize((200, 30), Image.LANCZOS), (x, 5))
        for (x, w, ic) in ((1080, 219, 'icon_gold'), (1321, 215, 'icon_wood'), (1559, 180, 'icon_trait'), (1746, 163, None)):
            top.alpha_composite(L('res_cell').resize((w, 30), Image.LANCZOS), (x, 5))
            if ic: top.alpha_composite(L(ic).resize((26, 26), Image.LANCZOS), (x + 14, 7))
        orb = L('clock_orb').resize((160, 160), Image.LANCZOS); layer = Image.new('RGBA', (1920, 200)); layer.alpha_composite(orb, (880, 0)); top.alpha_composite(layer.crop((0, 80, 1920, 200)).resize((1920, 120)), (0, 0)) if False else top.alpha_composite(orb.crop((0, 58, 160, 160)), (880, 0))
        t2 = Image.new('RGBA', (1920, 2 * 110 + 20), (20, 20, 24, 255)); t2.alpha_composite(ref.crop((0, 0, 1920, 110)), (0, 0)); t2.alpha_composite(top.crop((0, 0, 1920, 110)), (0, 130))
        t2.convert('RGB').save(f'{D}/compare_topbar.png')
    out.convert('RGB').save(f'{D}/compare_console.png'); ours.convert('RGB').save(f'{D}/preview_console_full.png'); print('compare saved'); sys.exit(0)

import math, random
exec(open(os.path.join(HERE, 'gen_ui_wc3_common.py'), encoding='utf-8').read())
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.expanduser(argv[0] if argv else '~/GRD_wc3_ui'); os.makedirs(OUT, exist_ok=True)
SAMPLES = int(argv[1]) if len(argv) > 1 else 128
TILT = 12.0                                    # 돌 콘솔은 위에서 12° 내려다봄(참고 사진처럼 윗면이 밝게 보이게)
PPM = 200.0                                    # 납품 px / m (목표 1px = 1cm, 납품 2배)
PARTS = os.environ.get('WC3_PARTS', 'bar,bar_tall,capL,capR,pillar,panel').split(',')

STONE_TONES = [tuple(c * f for c, f in zip(t_, (1.17, 1.13, 1.08))) for t_ in [(.075, .072, .07), (.064, .062, .064), (.086, .08, .075), (.058, .057, .06), (.079, .075, .072), (.069, .065, .063)]]
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

def rock_courses(x0, x1, z0, z1, seed):
    """비정형 돌 쌓기: 두 줄씩 묶어, 일부는 두 줄 높이 큰 바위, 나머지는 줄마다 끊는 자리가 다르게. [x0,x1]을 정확히 채움."""
    r = random.Random(seed); out = []; z = z0
    while z < z1 - 1e-6:
        h1 = r.uniform(.22, .32); h2 = r.uniform(.22, .32)
        if z1 - (z + h1 + h2) < .12: 
            if z1 - z < .40: h1, h2 = z1 - z, 0.0
            else: h2 = z1 - z - h1
        x = x0
        while x < x1 - 1e-6:
            if h2 > 0 and r.random() < .3:
                w = r.uniform(.55, .9)
                if x1 - (x + w) < .25: w = x1 - x
                out.append((x, min(x + w, x1), z, z + h1 + h2)); x += w
            else:
                S = r.uniform(1.0, 1.7)
                if x1 - (x + S) < .3: S = x1 - x
                for (zb, zt) in ((z, z + h1), (z + h1, z + h1 + h2)) if h2 > 0 else ((z, z + h1),):
                    xx = x
                    while xx < x + S - 1e-6:
                        L = r.uniform(.45, .9)
                        if x + S - (xx + L) < .25: L = x + S - xx
                        out.append((xx, xx + L, zb, zt)); xx += L
                x += S
        z += h1 + h2
    return out

def place_blocks(sc, blocks, mats, depth=(.14, .22), y_front=0.0, gap=.026, chip=.04, tag='b', seed=0, xshift=0.0):
    r = random.Random(seed)
    for i, (x0, x1, z0, z1) in enumerate(blocks):
        d = r.uniform(*depth); sx = x1 - x0 - gap; sz = z1 - z0 - gap
        ob = rough_block(sc, f'{tag}{i}', (sx * r.uniform(.97, 1.0), d, sz * r.uniform(.95, 1.0)), (xshift + (x0 + x1) / 2, y_front + d / 2 - r.uniform(0, .03), (z0 + z1) / 2 + r.uniform(-.01, .01)), mats[r.randrange(len(mats))], seed * 1000 + i, chip=chip)
        ob.rotation_euler = (0, math.radians(r.uniform(-2.2, 2.2)), 0)

def merlons(sc, x0, x1, z0, h, mats, capmats, w=.96, gapw=.32, seed=9, y_front=-.04, xshift=0.0, first_offset=None, jitter=.04):
    """총안: [x0,x1] 안에 주기 (w+gapw). 하나하나 폭·높이를 ±jitter로 다르게(이음은 주기 복제라 맞음). 위는 밝은 평평한 뚜껑돌."""
    per = w + gapw; x = x0 + (gapw / 2 if first_offset is None else first_offset); j = 0; r = random.Random(seed)
    while x + w <= x1 + 1e-6:
        dw = r.uniform(-jitter, jitter); dh = r.uniform(-jitter, jitter * .5); ww = w + dw; hh = h + dh
        cx = xshift + x + w / 2 + r.uniform(-.01, .01)
        rough_block(sc, f'mer{seed}_{j}', (ww - .02, .26, hh - .06), (cx, y_front + .13, z0 + (hh - .06) / 2), mats[j % len(mats)], seed * 100 + j, chip=.035)
        cap = mkbox(sc, f'mcap{seed}_{j}', (ww, .26, .06), (cx, y_front + .13, z0 + hh - .03), capmats[j % len(capmats)], bevel=.02)
        x += per; j += 1

# ------------------------------------------------------------------ 1. console_bar_tile (가로 타일 2.56 × 2.85, 위 0.24 총안)
def part_bar(tall=False):
    """console_bar_tile(2.56×2.85) / _tall(2.56×2.99). 아랫부분 돌 배치는 두 타일이 같다(같은 씨앗, 같은 줄) — 높은 쪽은 위에 한 줄 더."""
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    W, CZ = 2.56, .24; H = 2.85 + (.14 if tall else 0); body_top = H - CZ
    mats = stone_set(sc, '벽돌'); capm = stone_set(sc, '띠돌', 3, moss=.3, light=1.1, rust_low=0.0)
    lid = [stone_mat(f'뚜껑{i}', base=tuple(c * 1.55 for c in STONE_TONES[i]), seed=80 + i, moss=.1, light=1.2) for i in range(3)]
    base_top = 2.85 - CZ - .11                                             # 낮은 타일의 몸통 꼭대기(공통 배치)
    body = rock_courses(-W / 2, W / 2, -.35, base_top, seed=11)          # 화면 아래 밖까지 채움(기울여 봐도 아래가 비지 않게)
    extra = rock_courses(-W / 2, W / 2, base_top, body_top - .11, seed=14) if tall else []
    ledge = courses(-W / 2, W / 2, body_top - .11, body_top, seed=12, row_h=(.11, .11), blen=(.6, 1.0))
    for k in (-1, 0, 1):                                      # 주기 복제 → 이음새 없음
        mkbox(sc, 'mortar', (W, .1, body_top + .4), (k * W, .22, (body_top - .4) / 2), mortar_mat())
        place_blocks(sc, body + extra, mats, tag=f'b{k}_', seed=11, xshift=k * W)
        place_blocks(sc, ledge, capm, depth=(.24, .26), y_front=-.06, tag=f'l{k}_', seed=12, xshift=k * W, chip=.025)
        merlons(sc, -W / 2, W / 2, body_top, CZ, capm, lid, seed=13, xshift=k * W)
    ortho_cam(sc, 0, H / 2 / math.cos(math.radians(TILT)), W, int(W * PPM), int(H * PPM), tilt=TILT); render(sc, f'{OUT}/console_bar_tile{"_tall" if tall else ""}.png')

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
    lid = [stone_mat(f'뚜껑{i}', base=tuple(c * 1.55 for c in STONE_TONES[i]), seed=80 + i, moss=.1, light=1.2) for i in range(2)]
    merlons(sc, x0, x1, body_top, CZ, capm, lid, w=.56, gapw=.14, seed=31 if side == 'L' else 32, y_front=-.08, first_offset=(0.0 if side == 'L' else W - .56), jitter=.02)
    ortho_cam(sc, 0, H / 2 / math.cos(math.radians(TILT)), W, int(W * PPM), int(H * PPM), tilt=TILT); render(sc, f'{OUT}/console_cap_{"left" if side == "L" else "right"}.png')

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
    ortho_cam(sc, 0, H / 2 / math.cos(math.radians(TILT)), W, int(W * PPM), int(H * PPM), tilt=TILT); render(sc, f'{OUT}/stone_pillar.png')

# ------------------------------------------------------------------ 4. panel_frame_stone (1.2 × 1.2, 테두리 0.28, 가운데 투명, 안쪽 어두운 홈)
def part_panel():
    """판 틀(9-slice 여백 32@2x = 0.16m): 바깥 돌 블록 띠 → 밝은 돌 입술(0.035) → 검은 홈(0.02) → 가운데 투명."""
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    S, B = 1.2, .16; LIP = .035; G = .02
    mats = stone_set(sc, '틀돌', moss=.3, rust_low=0.0)
    lipm = stone_mat('입술돌', base=tuple(c * 1.6 for c in STONE_TONES[0]), seed=90, moss=0, light=1.25)
    mort = mortar_mat(); r = random.Random(61)
    ob_w = B - LIP - G                                                     # 바깥 돌 띠 폭
    def band(x0, x1, z0, z1, horiz, tag):
        if horiz:
            x = x0; j = 0
            while x < x1 - 1e-6:
                L = r.uniform(.24, .4)
                if x1 - (x + L) < .12: L = x1 - x
                rough_block(sc, f'{tag}{j}', (L - .014, .16, z1 - z0 - .012), ((2 * x + L) / 2, .08, (z0 + z1) / 2), mats[r.randrange(6)], 700 + j + 37 * len(tag), chip=.022); x += L; j += 1
        else:
            z = z0; j = 0
            while z < z1 - 1e-6:
                L = r.uniform(.2, .34)
                if z1 - (z + L) < .1: L = z1 - z
                rough_block(sc, f'{tag}{j}', (x1 - x0 - .012, .16, L - .014), ((x0 + x1) / 2, .08, (2 * z + L) / 2), mats[r.randrange(6)], 800 + j + 37 * len(tag), chip=.022); z += L; j += 1
    h = S / 2
    band(-h, h, h - ob_w, h, True, 'top'); band(-h, h, -h, -h + ob_w, True, 'bot')
    band(-h, -h + ob_w, -h + ob_w, h - ob_w, False, 'L'); band(h - ob_w, h, -h + ob_w, h - ob_w, False, 'R')
    for (sx, sz, x, z) in ((S, ob_w, 0, h - ob_w / 2), (S, ob_w, 0, -h + ob_w / 2), (ob_w, S, -h + ob_w / 2, 0), (ob_w, S, h - ob_w / 2, 0)): mkbox(sc, 'back', (sx, .05, sz), (x, .2, z), mort)
    a_ = h - ob_w                                                          # 입술 바깥 가장자리
    for (sx, sz, x, z) in ((2 * a_, LIP, 0, a_ - LIP / 2), (2 * a_, LIP, 0, -a_ + LIP / 2), (LIP, 2 * a_ - 2 * LIP, -a_ + LIP / 2, 0), (LIP, 2 * a_ - 2 * LIP, a_ - LIP / 2, 0)):
        mkbox(sc, 'lip', (sx, .12, sz), (x, .04, z), lipm, bevel=.012)
    groove = simple('홈', (.003, .003, .004), .9); g_ = a_ - LIP
    for (sx, sz, x, z) in ((2 * g_, G, 0, g_ - G / 2), (2 * g_, G, 0, -g_ + G / 2), (G, 2 * g_, -g_ + G / 2, 0), (G, 2 * g_, g_ - G / 2, 0)):
        mkbox(sc, 'groove', (sx, .1, sz), (x, .06, z), groove)
    ortho_cam(sc, 0, 0, S, int(S * PPM), int(S * PPM)); render(sc, f'{OUT}/panel_frame_stone.png')

# ================================================================== ② 금속 틀 공통: 둥근 사각 경로 + 금 이중선 + 짙은 홈
GOLD = None
def gold_mats():
    return (metal_mat('금', col=(.62, .42, .12), rough=.32, wear=.5, dark=(.09, .055, .015)),
            metal_mat('어두운금', col=(.20, .13, .04), rough=.5, wear=.3, dark=(.05, .03, .01)))
def rrect_path(w, h, r, n=10, open_bottom=False):
    """둥근 사각 경로(가운데 원점, XZ 평면). open_bottom이면 아래 변 없이 왼아래→위→오른아래."""
    pts = []
    corners = [((w / 2 - r, h / 2 - r), 0), ((-w / 2 + r, h / 2 - r), 90), ((-w / 2 + r, -h / 2 + r), 180), ((w / 2 - r, -h / 2 + r), 270)]
    for (cx, cz), a0 in corners:
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n); pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts
def tube(sc, name, pts2d, radius, mat, y=0.0, closed=True):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; sp = cu.splines.new('POLY'); sp.points.add(len(pts2d) - 1)
    for i, (x, z) in enumerate(pts2d): sp.points[i].co = (x, y, z, 1)
    sp.use_cyclic_u = closed; cu.bevel_depth = radius; cu.bevel_resolution = 6; cu.use_fill_caps = True
    ob = link(bpy.data.objects.new(name, cu), sc); cu.materials.append(mat); return ob
def plate(sc, name, pts2d, mat, y=.02, th=.02):
    """2D 다각형을 두께 th 판으로(앞면 y−th/2)."""
    bm = bmesh.new(); vs = [bm.verts.new((x, 0, z)) for x, z in pts2d]; f = bm.faces.new(vs)
    bmesh.ops.reverse_faces(bm, faces=[f]) if False else None
    ex = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, vec=(0, th, 0), verts=[v for v in ex['geom'] if isinstance(v, bmesh.types.BMVert)])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); ob = link(bpy.data.objects.new(name, me), sc); ob.location = (0, y - th / 2, 0); me.materials.append(mat); return ob
def gold_frame(sc, w, h, r, line=.018, inset=.05, fill=None, inner_line=.009, band_mat=None, y=0.0, open_bottom=False):
    """금 이중선 틀: 바깥 굵은 금 관 + 안쪽 가는 금 관 + 사이 어두운 금 띠 + (선택) 안쪽 채움판."""
    g, gd = GOLD
    outer = rrect_path(w - 2 * line, h - 2 * line, max(.001, r - line))
    inner = rrect_path(w - 2 * inset, h - 2 * inset, max(.001, r - inset))
    tube(sc, 'gout', outer, line, g, y=y)
    plate(sc, 'gband', rrect_path(w - 2 * line, h - 2 * line, max(.001, r - line)), band_mat or gd, y=y + .012, th=.01)
    tube(sc, 'gin', inner, inner_line, g, y=y - .002)
    if fill is not None: plate(sc, 'fill', rrect_path(w - 2 * inset, h - 2 * inset, max(.001, r - inset)), fill, y=y + .004, th=.01)

def frame_scene(samples=None):
    global GOLD
    sc = reset_scene(); ui_lights(sc); render_setup(sc, samples or SAMPLES); GOLD = gold_mats(); return sc

# ------------------------------------------------------------------ 5. portrait_arch_frame (+ mask) 2.65 × 2.22
def arch_path(w, h, rise, n=48):
    """아치: 왼아래 → 왼 변 → 완만한 타원 위 → 오른 변 → 오른아래(열린 아래)."""
    pts = [(-w / 2, -h / 2)]
    for k in range(n + 1):
        a = math.pi * (1 - k / n); pts.append((w / 2 * math.cos(a), (h / 2 - rise) + rise * math.sin(a)))
    pts.append((w / 2, -h / 2)); return pts
def part_arch():
    sc = frame_scene(); g, gd = GOLD
    W, H = 2.65, 2.22; rise = .55; m = .17                               # 틀 띠 폭 0.17(참고 사진의 굵은 금테)
    out = arch_path(W - .04, H, rise); inn = arch_path(W - .04 - 2 * m, H + .0, rise - m * .9)
    inn = [(x, z) for x, z in inn]; inn[0] = (inn[0][0], -H / 2); inn[-1] = (inn[-1][0], -H / 2)
    tube(sc, 'aout', out, .034, g, closed=False); tube(sc, 'ain', inn, .02, g, closed=False, y=-.006)
    tube(sc, 'amid', arch_path(W - .04 - m, H, rise - m * .45), .011, g, closed=False, y=-.004)   # 띠 가운데 가는 금선
    # 띠: 바깥·안쪽 경로 사이 리본(어두운 금)
    bm = bmesh.new(); vo = [bm.verts.new((x, .012, z)) for x, z in out]; vi = [bm.verts.new((x, .012, z)) for x, z in arch_path(W - .04 - 2 * m, H, rise - m * .9)]
    for i in range(len(vo) - 1): bm.faces.new((vo[i], vo[i + 1], vi[i + 1], vi[i]))
    me = bpy.data.meshes.new('aband'); bm.to_mesh(me); bm.free(); ob = link(bpy.data.objects.new('aband', me), sc); me.materials.append(metal_mat('청동띠', col=(.32, .21, .07), rough=.45, wear=.7, dark=(.06, .035, .01)))
    # 리벳 + 맨 위 보석 받침
    for k in range(1, 12):
        if k == 6: continue
        t = k / 12; a = math.pi * (1 - t); x = (W / 2 - .02 - m / 2) * math.cos(a); z = (H / 2 - rise) + (rise - m * .45) * math.sin(a)
        mksph(sc, 'rivet', .024, (x, -.012, z), g, sub=2)
    for zz in (-.6, -.2, .2):
        for sx in (-1, 1): mksph(sc, 'rivet', .024, (sx * (W / 2 - .02 - m / 2), -.012, zz), g, sub=2)
    top = (0, -.03, H / 2 - .05)
    mkcyl(sc, 'gemset', .1, .05, top, g, rot=(math.radians(90), 0, 0), seg=8)
    mksph(sc, 'gem', .055, (0, -.07, H / 2 - .06), gem_mat('붉은보석', (.45, .02, .03), .35), sub=3, scale=(1, .6, 1.2))
    for sx in (-1, 1): mksph(sc, 'sgem', .028, (sx * .16, -.03, H / 2 - .07 - .01), gem_mat('푸른보석', (.1, .3, .9), .6), sub=2)
    ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/portrait_arch_frame.png')
    # 마스크: 안쪽만 흰 발광 판, 나머지 숨김
    for o in list(sc.objects):
        if o.type in ('MESH', 'CURVE'): o.hide_render = True
    mpts = arch_path(W - .04 - 2 * m + .02, H, rise - m * .9 + .01)
    pl = plate(sc, 'mask', mpts, glow('흰', (1, 1, 1), 1.0), y=0, th=.01)
    render(sc, f'{OUT}/portrait_arch_mask.png')

# ------------------------------------------------------------------ 6. info_title_strip 4.4×0.30 · info_level_strip 4.4×0.18 · 7. icon_slot_gold 0.48
def part_info():
    sc = frame_scene(); blk = matte_black('검정')
    gold_frame(sc, 4.4, .30, .14, line=.016, inset=.045, fill=blk)
    ortho_cam(sc, 0, 0, 4.4, int(4.4 * PPM), int(.30 * PPM)); render(sc, f'{OUT}/info_title_strip.png')
    sc = frame_scene(); blk = matte_black('검정')
    gold_frame(sc, 4.4, .18, .08, line=.011, inset=.03, inner_line=.006, fill=blk)
    ortho_cam(sc, 0, 0, 4.4, int(4.4 * PPM), int(.18 * PPM)); render(sc, f'{OUT}/info_level_strip.png')
    sc = frame_scene(); blk = matte_black('검정', .003)
    gold_frame(sc, .48, .48, .05, line=.018, inset=.06, fill=blk)
    ortho_cam(sc, 0, 0, .48, int(.48 * PPM), int(.48 * PPM)); render(sc, f'{OUT}/icon_slot_gold.png')

# ------------------------------------------------------------------ 6b. info_panel_frame 4.65×2.71 — 위 두 모서리 둥근 돌 몰딩 + 안쪽 금선, 가운데 투명
def top_round_path(w, h, r, n=14):
    pts = [(w / 2, -h / 2)]
    for cx, a0 in ((w / 2 - r, 0), (-w / 2 + r, 90)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n); pts.append((cx + r * math.cos(a), h / 2 - r + r * math.sin(a)))
    pts.append((-w / 2, -h / 2)); return pts
def ring_mesh(sc, name, outer, inner, mat, th=.12, y=0.0):
    """같은 점 개수의 바깥·안쪽 닫힌 경로 사이를 띠 면으로 잇고 두께를 준다(가운데 구멍)."""
    bm = bmesh.new(); vo = [bm.verts.new((x, y, z)) for x, z in outer]; vi = [bm.verts.new((x, y, z)) for x, z in inner]; n = len(vo)
    for i in range(n): bm.faces.new((vo[i], vo[(i + 1) % n], vi[(i + 1) % n], vi[i]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); ob = link(bpy.data.objects.new(name, me), sc); me.materials.append(mat)
    so = ob.modifiers.new('so', 'SOLIDIFY'); so.thickness = th; so.offset = 1.0
    bv = ob.modifiers.new('bv', 'BEVEL'); bv.width = min(.05, th * .4); bv.segments = 4; bv.limit_method = 'ANGLE'
    return ob
def part_info_panel():
    sc = frame_scene(); g, gd = GOLD
    W, H, R, B = 4.65, 2.71, .28, .20                                       # B = 돌 테 폭(9-slice 여백 40@2x)
    st = stone_mat('정보틀돌', base=tuple(c * 1.25 for c in STONE_TONES[0]), seed=95, moss=.2, light=1.2, crack_amt=.25)
    def closed(w, h, r):
        p_ = top_round_path(w, h, r); return p_
    out = closed(W, H, R); inn = closed(W - 2 * B, H - 2 * B, max(.06, R - B * .8))
    ring_mesh(sc, 'frame', out, inn, st, th=.12, y=-.06)
    tube(sc, 'gline', closed(W - 2 * B - .04, H - 2 * B - .04, max(.05, R - B * .8 - .02)), .011, g, y=-.065, closed=True)
    ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/info_panel_frame.png')

# ================================================================== ③ 인벤토리·명령 카드
def srgb(h):  # '#14284A' → 선형
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((v + .055) / 1.055) ** 2.4 if v > .04045 else v / 12.92 for v in c)
def part_inventory():
    # 9. 제목 띠(금테 짙은 판) 1.8×0.30
    sc = frame_scene(); dark = metal_mat('짙은판', col=srgb('#2A1E0E'), rough=.6, wear=.4, dark=(.01, .008, .005))
    gold_frame(sc, 1.8, .30, .05, line=.016, inset=.045, fill=dark)
    ortho_cam(sc, 0, 0, 1.8, int(1.8 * PPM), int(.30 * PPM)); render(sc, f'{OUT}/inventory_title.png')
    # 10. 빈 칸: 청남색 판 + 배낭 실루엣(낮은 돋을새김) + 금테 0.86
    for filled in (False, True):
        sc = frame_scene(); g, gd = GOLD
        S = .86
        if not filled:
            blue = newmat('청남판')[0]; t = blue.node_tree; b = next(n for n in t.nodes if n.type == 'BSDF_PRINCIPLED')
            tc = nd(t, 'ShaderNodeTexCoord'); gr = nd(t, 'ShaderNodeTexGradient', gradient_type='SPHERICAL'); mp = nd(t, 'ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1.6, 1.6, 1.6)
            lk(t, tc.outputs['Object'], mp.inputs[0]); lk(t, mp.outputs[0], gr.inputs[0])
            lk(t, ramp(t, gr.outputs['Fac'], [(0, srgb('#0A1428') + (1,)), (.7, srgb('#1C3058') + (1,))]), b.inputs['Base Color']); b.inputs['Roughness'].default_value = .55
            plate(sc, 'blue', rrect_path(S - .1, S - .1, .02), blue, y=.01, th=.01)
            pack = simple('배낭', srgb('#0A1530'), .5)
            mkbox(sc, 'pack', (.36, .05, .40), (0, -.004, -.03), pack, bevel=.06)                   # 몸통
            mkbox(sc, 'flap', (.38, .05, .16), (0, -.02, .12), pack, bevel=.05)                     # 덮개
            mkbox(sc, 'pocket', (.22, .04, .12), (0, -.03, -.13), pack, bevel=.03)                  # 앞 주머니
            for sx in (-1, 1): mkbox(sc, 'strap', (.04, .04, .32), (sx * .1, -.035, .02), pack, bevel=.01)
            mktorus(sc, 'handle', .06, .014, (0, -.01, .24), pack, rot=(math.radians(90), 0, 0))
        gold_frame(sc, S, S, .04, line=.02, inset=.055, fill=None if not filled else matte_black('안쪽', .002))
        ortho_cam(sc, 0, 0, S, int(S * PPM), int(S * PPM)); render(sc, f'{OUT}/inventory_cell{"_filled" if filled else ""}.png')

def part_command():
    # 11. 명령 카드 칸 1.11×0.86 — 순흑 홈 + 회색 금속 가는 테(안쪽 하이라이트), 눌림·호버(금빛)
    for kind in ('normal', 'pressed', 'hover'):
        sc = frame_scene(); g, gd = GOLD
        W, H = 1.11, .86
        steel = metal_mat('강철테', col=(.42, .43, .45), rough=.35, wear=.4, dark=(.06, .06, .07)) if kind != 'hover' else g
        edge = steel
        blk = matte_black('홈')
        plate(sc, 'hole', rrect_path(W - .06, H - .06, .01), blk, y=.01, th=.01)
        tube(sc, 'rim', rrect_path(W - .03, H - .03, .015), .014 if kind != 'pressed' else .011, edge)
        tube(sc, 'hl', rrect_path(W - .075, H - .075, .01), .004, simple('하이라이트', (.35, .36, .4) if kind != 'hover' else (.9, .7, .3), .4, metallic=.8), y=-.002)
        if kind == 'pressed':
            plate(sc, 'shade', rrect_path(W - .07, H - .07, .01), simple('눌림그늘', (0, 0, 0), 1), y=-.004, th=.002)
        ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/command_cell{"" if kind == "normal" else "_" + kind}.png')
    # 12. 4×3 바깥 돌틀 + 칸 사이 어두운 홈 4.47×2.60
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES)
    W, H = 4.47, 2.60; B = .10
    mats = stone_set(sc, '카드틀', moss=.25, rust_low=0.0)
    r = random.Random(77)
    for (x0, x1, z0, z1, horiz) in ((-W / 2, W / 2, H / 2 - B, H / 2, True), (-W / 2, W / 2, -H / 2, -H / 2 + B, True), (-W / 2, -W / 2 + B, -H / 2 + B, H / 2 - B, False), (W / 2 - B, W / 2, -H / 2 + B, H / 2 - B, False)):
        if horiz:
            x = x0
            while x < x1 - 1e-6:
                L = min(r.uniform(.4, .7), x1 - x); rough_block(sc, 'cf', (L - .012, .12, z1 - z0 - .01), ((2 * x + L) / 2, .06, (z0 + z1) / 2), mats[r.randrange(6)], r.randrange(9999), chip=.018); x += L
        else:
            z = z0
            while z < z1 - 1e-6:
                L = min(r.uniform(.35, .6), z1 - z); rough_block(sc, 'cf', (x1 - x0 - .01, .12, L - .012), ((x0 + x1) / 2, .06, (2 * z + L) / 2), mats[r.randrange(6)], r.randrange(9999), chip=.018); z += L
    groove = simple('홈', (.003, .003, .004), .9)
    cw, ch = (W - 2 * B) / 4, (H - 2 * B) / 3
    for i in range(1, 4): mkbox(sc, 'vg', (.03, .05, H - 2 * B), (-W / 2 + B + i * cw, .05, 0), groove)
    for j in range(1, 3): mkbox(sc, 'hg', (W - 2 * B, .05, .03), (0, .05, -H / 2 + B + j * ch), groove)
    ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/command_grid_frame.png')

# ================================================================== ④ 상단 바: 알약 단추 · 시계 구슬 · 자원 칸 · 아이콘
def navy_mat(name, top='#1A2650', bot='#0A1020', glow_=0.0):
    m, t, b = newmat(name); tc = nd(t, 'ShaderNodeTexCoord'); sp = nd(t, 'ShaderNodeSeparateXYZ'); lk(t, tc.outputs['Generated'], sp.inputs[0])
    lk(t, ramp(t, sp.outputs[2], [(0, srgb(bot) + (1,)), (1, srgb(top) + (1,))]), b.inputs['Base Color']); b.inputs['Roughness'].default_value = .35
    try: b.inputs['Coat Weight'].default_value = .6
    except Exception: pass
    if glow_: b.inputs['Emission Color'].default_value = (1, .7, .3, 1); b.inputs['Emission Strength'].default_value = glow_
    return m
def end_ornaments(sc, w, h, mat):
    """알약 양 끝 금 장식: 작은 뾰족 촉 + 징."""
    for sx in (-1, 1):
        x = sx * (w / 2 - h * .5)
        mksph(sc, 'stud', h * .14, (x, -.02, 0), mat, sub=2)
        tip = mkcyl(sc, 'tip', h * .16, h * .22, (sx * (w / 2 - .01), -.01, 0), mat, rot=(0, math.radians(90 * sx), 0), seg=4, r2=0.001)
def part_topbar():
    for kind in ('normal', 'hover', 'pressed'):
        sc = frame_scene(); g, gd = GOLD
        W, H = 2.0, .30
        fill = {'normal': navy_mat('남색'), 'hover': navy_mat('남색밝게', top='#2C4590', bot='#122050'), 'pressed': navy_mat('남색눌림', top='#0E1636', bot='#060A16')}[kind]
        gold_frame(sc, W, H, .06, line=.014, inset=.036, fill=fill, band_mat=metal_mat('청동', col=(.30, .19, .06), rough=.45, wear=.5, dark=(.05, .03, .01)))   # 참고: 모서리만 살짝 둥근 사각
        ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/topbar_button{"" if kind == "normal" else "_" + kind}.png')
    # 15. 자원 칸 2.19×0.30: 검은 안쪽 + 금테 + 양 끝 장식
    sc = frame_scene(); g, gd = GOLD
    gold_frame(sc, 2.19, .30, .1, line=.013, inset=.032, fill=matte_black('자원안', .004))
    ortho_cam(sc, 0, 0, 2.19, int(2.19 * PPM), int(.30 * PPM)); render(sc, f'{OUT}/res_cell.png')

def part_orb():
    """14. 시계 구슬 1.6×1.6: 파란 유리 구슬 + 은빛 고리(징 10) + 톱니 바깥 고리 + 양옆 금속 날개."""
    sc = frame_scene()
    silver = metal_mat('은', col=(.62, .66, .72), rough=.28, wear=.45, dark=(.08, .09, .11))
    darks = metal_mat('검은쇠', col=(.12, .13, .16), rough=.4, wear=.3, dark=(.02, .02, .03))
    mksph(sc, 'orb', .36, (0, 0, 0), orb_mat(deep=(.01, .04, .22), mid=(.06, .25, .8), core=(.45, .75, 1.0), glow=.55), sub=5, scale=(1, .55, 1))
    mktorus(sc, 'ring', .45, .1, (0, 0, 0), silver, rot=(math.radians(90), 0, 0), seg=96)                  # 굵은 은 고리
    mktorus(sc, 'bluering', .45, .055, (0, -.06, 0), gem_mat('파란테', (.05, .2, .7), .5), rot=(math.radians(90), 0, 0), seg=96)   # 고리 앞 파란 테
    for k in range(12):
        a = TAU * k / 12 + math.pi / 2; mksph(sc, 'stud', .026, (.45 * math.cos(a), -.115, .45 * math.sin(a)), gem_mat('흰보석', (.85, .92, 1.0), 1.2), sub=2)
    for k, (dx, h) in enumerate(((0, .3), (-.13, .2), (.13, .2))):                                              # 위쪽 뾰족 장식
        mkcyl(sc, 'spike', .05, h, (dx, .0, .58 + h / 2), silver, seg=4, r2=.002)
    # 톱니 바깥 고리
    mktorus(sc, 'outer', .555, .03, (0, .02, 0), darks, rot=(math.radians(90), 0, 0), seg=96)
    for k in range(24):
        a = TAU * k / 24; tooth = mkbox(sc, 'tooth', (.06, .05, .07), (.6 * math.cos(a), .02, .6 * math.sin(a)), darks, bevel=.01); tooth.rotation_euler = (0, -a + math.pi / 2, 0)
    # 날개: 아래쪽으로 휘어 내려가는 금속 판 세 겹(양옆)
    for sx in (-1, 1):
        for j, (L, ang, zz) in enumerate(((.42, -8, .05), (.36, -22, -.08), (.28, -36, -.2))):
            bm = bmesh.new(); vs = [bm.verts.new(v) for v in ((0, 0, .06), (L, 0, .02), (L + .06, 0, -.02), (0, 0, -.06))]; bm.faces.new(vs)
            me = bpy.data.meshes.new('wing'); bm.to_mesh(me); bm.free(); w_ = link(bpy.data.objects.new('wing', me), sc); me.materials.append(silver)
            sol = w_.modifiers.new('so', 'SOLIDIFY'); sol.thickness = .03; bv = w_.modifiers.new('bv', 'BEVEL'); bv.width = .008; bv.segments = 2
            w_.location = (sx * .55, .03 + .01 * j, zz); w_.rotation_euler = (0, math.radians(-ang) if sx > 0 else math.radians(180 + ang), 0)
    ortho_cam(sc, 0, 0, 1.6, int(1.6 * PPM), int(1.6 * PPM)); render(sc, f'{OUT}/clock_orb.png')

def part_icons():
    """자원 아이콘 0.32×0.32: 금(동전 무더기) · 목재(소나무) · 특성(금색 십자)."""
    gold_c = None
    # 금: 앞을 보는 동전 무더기
    sc = frame_scene(); g, gd = GOLD
    coin = metal_mat('동전', col=(.85, .6, .15), rough=.25, wear=.4, dark=(.2, .12, .02))
    for i, (x, z, r_) in enumerate(((-.06, -.05, .07), (.06, -.05, .07), (0, -.06, .075), (-.03, .03, .07), (.04, .035, .068), (0, .09, .065))):
        c_ = mkcyl(sc, 'coin', r_, .02, (x, -.03 * i, z), coin, rot=(math.radians(90), 0, 0), seg=40)
        mktorus(sc, 'rim', r_ * .82, .006, (x, -.03 * i - .011, z), coin, rot=(math.radians(90), 0, 0), seg=40)
    ortho_cam(sc, 0, 0, .32, 64, 64); render(sc, f'{OUT}/icon_gold.png')
    # 목재(소나무)
    sc = frame_scene()
    leaf = simple('솔잎', (.04, .22, .06), .7); bark = simple('줄기', (.15, .08, .03), .8)
    mkcyl(sc, 'trunk', .02, .08, (0, 0, -.11), bark, seg=8)
    for j, (r, z) in enumerate(((.12, -.05), (.095, .02), (.07, .08))): mkcyl(sc, 'cone', r, .12, (0, 0, z), leaf, seg=10, r2=.004)
    ortho_cam(sc, 0, 0, .32, 64, 64); render(sc, f'{OUT}/icon_wood.png')
    # 특성(금색 +)
    sc = frame_scene(); g, gd = GOLD
    mkbox(sc, 'v', (.07, .05, .24), (0, 0, 0), g, bevel=.018); mkbox(sc, 'h', (.24, .046, .07), (0, .001, 0), g, bevel=.018)
    ortho_cam(sc, 0, 0, .32, 64, 64); render(sc, f'{OUT}/icon_trait.png')

# ================================================================== ⑤ 타이머·점수판·접기 단추·영웅 칸·막대 홈
def glassy_navy(name, alpha=.78):
    m, t, b = newmat(name); b.inputs['Base Color'].default_value = srgb('#0A1020') + (1,); b.inputs['Roughness'].default_value = .5; b.inputs['Alpha'].default_value = alpha
    try: b.inputs['Specular IOR Level'].default_value = .1
    except Exception: pass
    return m
def part_timer():
    for (nm, W, H) in (('timer_window', 3.88, .37), ('scoreboard_frame', 3.88, 1.20)):
        sc = frame_scene()
        gold_frame(sc, W, H, .09, line=.014, inset=.04, fill=glassy_navy('반투명남'))
        ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/{nm}.png')
    sc = frame_scene(); g, gd = GOLD                                         # 접기 단추(− 모양)
    gold_frame(sc, .24, .24, .05, line=.012, inset=.03, fill=navy_mat('남색접기'))
    mkbox(sc, 'minus', (.12, .03, .028), (0, -.02, 0), g, bevel=.008)
    ortho_cam(sc, 0, 0, .24, int(.24 * PPM), int(.24 * PPM)); render(sc, f'{OUT}/collapse_btn.png')
def part_hero():
    """17. 영웅 초상 칸 돌 틀 1.0×1.0(여백 10@1x = 0.10, 가운데 투명) · 체력·마나 막대 홈 0.95×0.14(여백 4)."""
    sc = reset_scene(); ui_lights(sc); render_setup(sc, SAMPLES); global GOLD; GOLD = gold_mats(); g, gd = GOLD
    S, B = 1.0, .10
    st = stone_mat('영웅틀돌', base=tuple(c * 1.2 for c in STONE_TONES[2]), seed=97, moss=.2, light=1.15, crack_amt=.3)
    out = rrect_path(S, S, .04); inn = rrect_path(S - 2 * B, S - 2 * B, .02)
    ring_mesh(sc, 'hf', out, inn, st, th=.1, y=-.05)
    tube(sc, 'hg', rrect_path(S - 2 * B + .01, S - 2 * B + .01, .02), .007, g, y=-.055)
    ortho_cam(sc, 0, 0, S, int(S * PPM), int(S * PPM)); render(sc, f'{OUT}/hero_frame.png')
    sc = frame_scene()
    W, H = .95, .14
    plate(sc, 'track', rrect_path(W - .02, H - .02, .03), matte_black('홈', .006), y=.01, th=.01)
    tube(sc, 'tr', rrect_path(W - .02, H - .02, .03), .01, metal_mat('강철', col=(.35, .36, .38), rough=.4, wear=.4, dark=(.05, .05, .06)))
    ortho_cam(sc, 0, 0, W, int(W * PPM), int(H * PPM)); render(sc, f'{OUT}/bar_track.png')

for p in PARTS:
    {'timer': part_timer, 'hero': part_hero, 'topbar': part_topbar, 'orb': part_orb, 'icons': part_icons, 'infopanel': part_info_panel, 'inv': part_inventory, 'cmd': part_command, 'arch': part_arch, 'info': part_info, 'bar': part_bar, 'bar_tall': lambda: part_bar(True), 'capL': lambda: part_cap('L'), 'capR': lambda: part_cap('R'), 'pillar': part_pillar, 'panel': part_panel}[p]()
