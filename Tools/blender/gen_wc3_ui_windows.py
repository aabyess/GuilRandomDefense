"""워크3풍 창 그림(blender 세션 2026-10-07, PM 지시: 승인된 ~/GRD_wc3_ui 같은 디자인 언어로 F5 조합 서랍·조합 도우미·메뉴(F10)·채팅·점수판 세트).
gen_wc3_ui.py의 재질·도우미·부품(돌 테·금 이중선·남색 판)을 그대로 불러 쓴다. 블리자드 그림 추출 없음.
  blender -b --factory-startup --python Tools/blender/gen_wc3_ui_windows.py -- [출력 ~/GRD_wc3_ui] [샘플 128]   (환경변수 WC3W_PARTS)
"""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
_argv = sys.argv[:]
os.environ['WC3_PARTS'] = 'none'                     # gen_wc3_ui.py 를 부품 정의만 불러오도록(렌더 안 함)
_src = open(os.path.join(HERE, 'gen_wc3_ui.py'), encoding='utf-8').read().replace("for p in PARTS:\n", "for p in [x for x in PARTS if x != 'none']:\n")
__file__ = os.path.join(HERE, 'gen_wc3_ui.py'); exec(compile(_src, 'gen_wc3_ui.py', 'exec')); __file__ = os.path.join(HERE, 'gen_wc3_ui_windows.py')
WPARTS = os.environ.get('WC3W_PARTS', 'window').split(',')

FRONT = -.19                                                             # 돌 링(ring_mesh y=-.06, 두께 .12)의 앞면보다 앞

def left_round_path(w, h, r, n=14):
    """왼쪽 두 모서리만 둥근 닫힌 경로(서랍 세로 탭)."""
    pts = [(w / 2, h / 2)]
    for cx, cz, a0 in ((-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n); pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    pts.append((w / 2, -h / 2)); return pts

def path_frame(sc, path_fn, W, H, r, line=.014, inset=.036, fill=None, line_mat=None, band=None, inner=True, y=0.0, open_bottom=False):
    """임의 경로(path_fn(w,h,r))로 금 이중선 틀. open_bottom이면 아래 변 선을 빼고 띠·채움은 아래로 조금 내림(탭 selected)."""
    g, gd = GOLD; lm = line_mat or g
    op = path_fn(W - 2 * line, H - 2 * line, max(.002, r - line)); ip = path_fn(W - 2 * inset, H - 2 * inset, max(.002, r - inset))
    if open_bottom:
        op = path_fn(W - 2 * line, H + .2, max(.002, r - line)); op = [(x, z - .1 + 0) for x, z in op]
        ip = path_fn(W - 2 * inset, H + .2, max(.002, r - inset)); ip = [(x, z - .1 + (H / 2 - line) - (H / 2 - line)) for x, z in ip]
        op = [(x, min(z, H / 2)) for x, z in op]; tube(sc, 'o', [p for p in op if p[1] > -H / 2 + .001], line, lm, y=y, closed=False)
        if inner: tube(sc, 'i', [p for p in ip if p[1] > -H / 2 + .001], line * .55, lm, y=y - .002, closed=False)
        plate(sc, 'band', [(x, max(z, -H / 2)) for x, z in op], band or gd, y=y + .012, th=.01)
        if fill is not None: plate(sc, 'fill', [(x, max(z, -H / 2)) for x, z in ip], fill, y=y + .004, th=.01)
        return
    tube(sc, 'o', op, line, lm, y=y)
    plate(sc, 'band', op, band or gd, y=y + .012, th=.01)
    if inner: tube(sc, 'i', ip, line * .55, lm, y=y - .002)
    if fill is not None: plate(sc, 'fill', ip, fill, y=y + .004, th=.01)

def rr(w, h, r): return rrect_path(w, h, r)
def shot(sc, nm, W, H): ortho_cam(sc, 0, 0, W, int(round(W * PPM)), int(round(H * PPM))); render(sc, f'{OUT}/{nm}.png')
def suffix(k): return '' if k == 'normal' else '_' + k

def btn_fill(kind):
    return {'normal': navy_mat('남색'), 'hover': navy_mat('남색밝게', top='#2C4590', bot='#122050'), 'pressed': navy_mat('남색눌림', top='#0E1636', bot='#060A16'),
            'selected': navy_mat('청동불빛', top='#5A3A10', bot='#1E1206', glow_=.06), 'disabled': navy_mat('회색', top='#2A2C32', bot='#15161A'),
            'focus': matte_black('검색안', .004)}[kind]
def bronze(): return metal_mat('청동', col=(.30, .19, .06), rough=.45, wear=.5, dark=(.05, .03, .01))
def dull_gold(): return metal_mat('흐린금', col=(.25, .22, .17), rough=.6, wear=.3, dark=(.05, .045, .04))

# ---------------------------------------------------------------- A-1 공통
def part_win_frame():
    """win_frame: 2.4×2.4(목표 240, 여백 40@1x = .40) — 얇은 돌 몰딩 + 금 이중선 + 네 모서리 돌·금 장식, 가운데 투명."""
    sc = frame_scene(); g, gd = GOLD
    S, B = 2.4, .26
    st = stone_mat('창돌', base=tuple(c * 1.25 for c in STONE_TONES[0]), seed=101, moss=.15, light=1.2, crack_amt=.25)
    ring_mesh(sc, 'ring', rr(S, S, .06), rr(S - 2 * B, S - 2 * B, .03), st, th=.12, y=-.06)
    tube(sc, 'g1', rr(S - 2 * B - .02, S - 2 * B - .02, .03), .016, g, y=FRONT)
    tube(sc, 'g2', rr(S - 2 * B - .08, S - 2 * B - .08, .02), .008, g, y=FRONT)
    tube(sc, 'g0', rr(S - .03, S - .03, .05), .01, bronze(), y=FRONT + .1)
    for (sx, sz) in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
        cx, cz = sx * (S / 2 - .17), sz * (S / 2 - .17)
        rough_block(sc, 'cstone', (.3, .16, .3), (cx, -.14, cz), stone_mat('모서리돌', base=tuple(c * 1.35 for c in STONE_TONES[2]), seed=110 + sx + 3 * sz, moss=.1, light=1.25), 1100 + sx + 3 * sz, chip=.025)
        bpy.ops.mesh.primitive_cylinder_add(vertices=4, radius=.11, depth=.03, location=(cx, -.24, cz), rotation=(math.radians(90), 0, 0)); d = bpy.context.object; d.data.materials.append(g)
        bv = d.modifiers.new('b', 'BEVEL'); bv.width = .01; bv.segments = 2
        mksph(sc, 'cgem', .035, (cx, -.27, cz), gem_mat('창보석', (.1, .3, .9), .6), sub=3)
    shot(sc, 'win_frame', S, S)

def part_win_small():
    # 제목 띠 4.8×0.40
    sc = frame_scene(); path_frame(sc, rr, 4.8, .40, .19, line=.016, inset=.045, fill=matte_black('검정')); shot(sc, 'win_title_strip', 4.8, .40)
    # 단추 1.2×0.42 (5상태)
    for k in ('normal', 'hover', 'pressed', 'selected', 'disabled'):
        sc = frame_scene(); g, gd = GOLD
        path_frame(sc, rr, 1.2, .42, .07, line=.015, inset=.04, fill=btn_fill(k), band=bronze(), line_mat=(dull_gold() if k == 'disabled' else None))
        shot(sc, 'win_btn' + suffix(k), 1.2, .42)
    # 닫기 0.44×0.42 (✕ 포함)
    for k in ('normal', 'hover', 'pressed'):
        sc = frame_scene(); g, gd = GOLD
        path_frame(sc, rr, .44, .42, .06, line=.015, inset=.04, fill=btn_fill(k), band=bronze())
        for ang in (45, -45): mkbox(sc, 'x', (.24, .03, .045), (0, -.03, 0), g, rot=(0, math.radians(ang), 0), bevel=.012)
        shot(sc, 'win_close_btn' + suffix(k), .44, .42)
    # 검색칸 3.4×0.42 (보통·focus)
    for k in ('normal', 'focus'):
        sc = frame_scene(); g, gd = GOLD
        lm = metal_mat('밝은금', col=(.95, .72, .28), rough=.25, wear=.3, dark=(.3, .2, .05)) if k == 'focus' else None
        path_frame(sc, rr, 3.4, .42, .05, line=.014 if k == 'normal' else .018, inset=.034, fill=matte_black('홈', .002), line_mat=lm)
        shot(sc, 'win_search_box' + suffix(k), 3.4, .42)
    # 칩 1.24×0.28 (보통·hover·selected) — 얇은 알약
    for k in ('normal', 'hover', 'selected'):
        sc = frame_scene()
        path_frame(sc, rr, 1.24, .28, .135, line=.011, inset=.028, inner=False, fill=btn_fill(k), band=bronze())
        shot(sc, 'win_chip' + suffix(k), 1.24, .28)
    # 등급 탭 1.7×0.40 (위만 둥근, selected = 아래 열림)
    for k in ('normal', 'hover', 'selected'):
        sc = frame_scene()
        path_frame(sc, top_round_path, 1.7, .40, .12, line=.014, inset=.038, fill=btn_fill(k if k != 'selected' else 'selected'), band=bronze(), open_bottom=(k == 'selected'))
        shot(sc, 'win_tab' + suffix(k), 1.7, .40)
    # 서랍 세로 탭 0.46×2.10 (왼쪽만 둥근)
    for k in ('normal', 'hover'):
        sc = frame_scene()
        path_frame(sc, left_round_path, .46, 2.10, .16, line=.015, inset=.04, fill=btn_fill(k), band=bronze())
        shot(sc, 'drawer_tab' + suffix(k), .46, 2.10)
    # 목록 행 5.2×0.76 (보통·owned·dim)
    for k in ('normal', 'owned', 'dim'):
        sc = frame_scene(); g, gd = GOLD
        fill = navy_mat('행판', top='#111A33', bot='#070B16') if k != 'dim' else navy_mat('흐린행', top='#0B0F1A', bot='#05070C')
        lm = {'normal': metal_mat('강철', col=(.35, .36, .40), rough=.4, wear=.4, dark=(.05, .05, .06)), 'owned': metal_mat('청동밝게', col=(.75, .5, .16), rough=.3, wear=.4, dark=(.15, .09, .02)), 'dim': metal_mat('흐린강철', col=(.14, .145, .16), rough=.6, wear=.2, dark=(.03, .03, .04))}[k]
        path_frame(sc, rr, 5.2, .76, .05, line=(.018 if k == 'owned' else .011), inset=.03, inner=(k == 'owned'), fill=fill, line_mat=lm, band=matte_black('띠', .006))
        shot(sc, 'win_row' + suffix(k), 5.2, .76)
    # 진행률 홈 2.8×0.14
    sc = frame_scene()
    plate(sc, 'track', rr(2.78, .12, .03), matte_black('홈', .004), y=.01, th=.01)
    tube(sc, 'tr', rr(2.78, .12, .03), .009, metal_mat('강철', col=(.35, .36, .38), rough=.4, wear=.4, dark=(.05, .05, .06)))
    shot(sc, 'win_bar_track', 2.8, .14)
    # 스크롤 홈 0.12×1.2 · 손잡이 0.12×0.60 (보통·hover)
    sc = frame_scene()
    plate(sc, 'st', rr(.11, 1.19, .05), matte_black('홈', .003), y=.01, th=.01); tube(sc, 'str', rr(.11, 1.19, .05), .006, metal_mat('강철', col=(.3, .31, .33), rough=.4, wear=.4, dark=(.05, .05, .06)))
    shot(sc, 'win_scroll_track', .12, 1.2)
    for k in ('normal', 'hover'):
        sc = frame_scene()
        h_ = metal_mat('청동손잡이', col=(.62, .42, .14) if k == 'normal' else (.9, .66, .25), rough=.3, wear=.5, dark=(.12, .07, .02))
        o = mkbox(sc, 'handle', (.1, .05, .58), (0, 0, 0), h_, bevel=.045); o.modifiers['b'].segments = 6
        for z in (-.06, 0, .06): mkbox(sc, 'grip', (.06, .02, .012), (0, -.03, z), metal_mat('홈쇠', col=(.1, .07, .03), rough=.5, wear=.2, dark=(.03, .02, .01)), bevel=.004)
        shot(sc, 'win_scroll_handle' + suffix(k), .12, .60)
    # 툴팁 4.8×1.7
    sc = frame_scene()
    path_frame(sc, rr, 4.8, 1.7, .08, line=.012, inset=.03, inner=False, fill=glassy_navy('툴팁판', .94))
    shot(sc, 'win_tooltip', 4.8, 1.7)

# ---------------------------------------------------------------- A-2 메뉴
def part_menu():
    # 메뉴 창 틀 10.75×2.80(여백 40@1x): win_frame과 같은 결, 위 가운데에 제목 받침 돌 + 모서리 돌 더 크게
    sc = frame_scene(); g, gd = GOLD
    W, H, B = 10.75, 2.80, .26
    st = stone_mat('메뉴돌', base=tuple(c * 1.25 for c in STONE_TONES[0]), seed=121, moss=.15, light=1.2, crack_amt=.25)
    ring_mesh(sc, 'ring', rr(W, H, .06), rr(W - 2 * B, H - 2 * B, .03), st, th=.12, y=-.06)
    tube(sc, 'g1', rr(W - 2 * B - .02, H - 2 * B - .02, .03), .016, g, y=FRONT); tube(sc, 'g2', rr(W - 2 * B - .08, H - 2 * B - .08, .02), .008, g, y=FRONT)
    tube(sc, 'g0', rr(W - .03, H - .03, .05), .01, bronze(), y=FRONT + .1)
    for (sx, sz) in ((-1, 1), (1, 1), (-1, -1), (1, -1)):
        cx, cz = sx * (W / 2 - .19), sz * (H / 2 - .19)
        rough_block(sc, 'cstone', (.36, .17, .36), (cx, -.15, cz), stone_mat('모서리돌', base=tuple(c * 1.35 for c in STONE_TONES[2]), seed=130 + sx + 3 * sz, moss=.1, light=1.25), 1300 + sx + 3 * sz, chip=.025)
        bpy.ops.mesh.primitive_cylinder_add(vertices=4, radius=.13, depth=.03, location=(cx, -.26, cz), rotation=(math.radians(90), 0, 0)); d = bpy.context.object; d.data.materials.append(g)
        mksph(sc, 'cgem', .04, (cx, -.29, cz), gem_mat('창보석', (.1, .3, .9), .6), sub=3)
    shot(sc, 'menu_panel', W, H)
    # 메뉴 단추 1.93×0.84(여백 16): 두꺼운 돌판 + 금테 (보통·hover·pressed·disabled)
    for k in ('normal', 'hover', 'pressed', 'disabled'):
        sc = frame_scene(); g, gd = GOLD
        W, H = 1.93, .84
        tone = {'normal': 1.35, 'hover': 1.6, 'pressed': 1.05, 'disabled': .9}[k]
        slab = stone_mat('단추돌' + k, base=tuple(c * tone for c in STONE_TONES[2]), seed=140, moss=0.0 if k != 'disabled' else .3, light=1.2, crack_amt=.15)
        o = mkbox(sc, 'slab', (W - .1, .14, H - .1), (0, .02 if k == 'pressed' else -.02, 0), slab, bevel=.05); o.modifiers['b'].segments = 4
        lm = dull_gold() if k == 'disabled' else (metal_mat('밝은금', col=(.95, .72, .28), rough=.25, wear=.3, dark=(.3, .2, .05)) if k == 'hover' else None)
        tube(sc, 'o', rr(W - .03, H - .03, .06), .016, lm or g, y=-.1)
        tube(sc, 'i', rr(W - .16, H - .16, .04), .008, lm or g, y=-.1 if k != 'pressed' else -.07)
        if k == 'hover':
            ld = bpy.data.lights.new('warm', 'POINT'); ld.energy = 25; ld.color = (1, .6, .25); lo = link(bpy.data.objects.new('warm', ld), sc); lo.location = (0, -.5, .2)
        shot(sc, 'menu_btn' + suffix(k), W, H)

# ---------------------------------------------------------------- A-3 채팅
def part_chat():
    for k in ('normal', 'focus'):
        sc = frame_scene(); g, gd = GOLD
        W, H = 5.2, .42
        lm = metal_mat('밝은금', col=(.95, .72, .28), rough=.25, wear=.3, dark=(.3, .2, .05)) if k == 'focus' else None
        path_frame(sc, rr, W, H, .05, line=.014 if k == 'normal' else .018, inset=.034, fill=glassy_navy('채팅판', .85), line_mat=lm)
        gx = -W / 2 + .78
        mkbox(sc, 'tagsep', (.016, .03, H - .1), (gx, -.01, 0), matte_black('홈', .002)); mkbox(sc, 'tagsep_hl', (.006, .03, H - .1), (gx + .014, -.012, 0), lm or g)
        shot(sc, 'chat_input' + suffix(k), W, H)

# ---------------------------------------------------------------- A-4 점수판
def part_score():
    sc = frame_scene(); g, gd = GOLD
    W, H = 4.68, .30
    path_frame(sc, rr, W, H, .04, line=.012, inset=.03, fill=navy_mat('머리줄', top='#1A2650', bot='#0A1020'), band=bronze())
    xs = W / 2 - .36                                                         # 오른쪽 접기 단추 자리 36px
    mkbox(sc, 'sep', (.014, .03, H - .08), (xs, -.01, 0), matte_black('홈', .002)); mkbox(sc, 'seph', (.005, .03, H - .08), (xs + .012, -.012, 0), g)
    shot(sc, 'score_header', W, H)
    sc = frame_scene(); g, gd = GOLD                                          # 색 칩 틀 0.14
    tube(sc, 'chip', rr(.12, .12, .015), .01, g); shot(sc, 'score_color_chip', .14, .14)

def part_window():
    part_win_frame(); part_win_small()

for p_ in WPARTS:
    {'window': part_window, 'frame': part_win_frame, 'small': part_win_small, 'menu': part_menu, 'chat': part_chat, 'score': part_score}[p_]()
