"""스토리 건물 Story08~13 — 공용 도구는 buildings_common.py, 텍스처는 buildings_textures.py.

화면 없이(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_buildings_b.py [-- Story08 Story09]
사장님 창: exec 후 buildings_common.story_rows(collection)(show_all의 판_스토리) — 창 모양 = FBX 모양.

치수는 게임 단위(사람 키 20). 한 층 FLOOR_H=15, 출입문 DOOR_H=22가 있는 1층은 GROUND_H=25.
x = 가로, y = 앞뒤(정면 −y), z = 위. 원점 = 바닥 가운데.
"""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

from mathutils import Vector

import buildings_common as bc
from buildings_common import DOOR_H, FLOOR_H, GROUND_H, UP, Builder, side_frame


def floating_sign(b, body, side, plane, u, z, board_mat, text_mat, size=4.0, pad=1.0, board_depth=0.6,
                   text_depth=0.3, max_width=None):
    """Builder.sign()과 똑같이 짓되, 판 뒷면(공용 sign()이 skip=("-y",)로 비우는 벽에 붙는다는
    가정)까지 다 막는다 — 벽이 없는 자리(도리이 가로보 사이처럼 공중에 뜬 간판)에 쓴다.
    공용 buildings_common.py는 안 고친다(다른 세션이 쓰는 파일)는 규칙이라, 그 함수의 로직을
    여기로 옮겨 skip만 뺐다(11 日本 편액, 2026-09-12 PM 검수로 뒷면 뚫림 발견)."""
    _, _, (x0, x1, y0, y1) = b._text_mesh(body, size, text_depth)
    scale = min(1.0, max_width / (x1 - x0)) if max_width else 1.0
    tw, th = (x1 - x0) * scale, (y1 - y0) * scale
    bw, bh = tw + pad * 2, th + pad * 2
    n, r, _ = side_frame(side)
    base = b._on_side(side, plane, u)
    b.box_frame(base - r * (bw / 2) + UP * z, r * bw, n * board_depth, UP * bh, board_mat)
    b.text(body, base + n * (board_depth + 0.02) + UP * (z + bh / 2), side, size, text_mat, text_depth,
           max_width=max_width)
    return bw, bh


# ──────────────────────────────────────────────────────────── 08 사이버넷

def make_cybernet():
    """PC방 상가 3층 — 1층 롤셔터 상가(셔터 셋 + 유리문 하나 + 차양 있는 유리 가게 하나), 2층 PC방(어두운
    통유리 띠 + 넓은 가로 네온 간판 「사이버넷」 + 정면 모서리에 돌출하는 세로 간판 「PC」), 3층 사무실
    (창틀·창턱), 얇은 옥상 파라펫 + 설비. blender 재검수 지시(2026-09-12) 반영:
      - 창은 전부 frame_mat(돌출 창틀)로 — 납작한 스티커 느낌을 없앤다.
      - 옆·뒤 벽이 통째로 비어 있던 문제 — 2·3층 옆·뒤에도 창을 낸다.
      - 「PC」 간판은 근사(옆벽 sign) 대신 진짜 돌출 도구 blade_sign(adc31ae1)으로.
      - 밀도는 01 하이츠 수준(삼각형 2,500~4,000)을 목표로 층간 띠·모서리 홈통·실외기·옥상 설비를 늘린다.
    2026-09-12 PM 추가 규칙 2건 반영:
      - 간판 글자 높이 ≥ 건물 폭(33)의 1/12 — 「사이버넷」(size 4.5)은 이미 넘는다. 「PC」 블레이드
        사인은 1.8→3.5로 키웠다(세로 두 글자라 판이 커지지만 규칙이 우선).
      - 흰 벽 금지(PM: "네온이 안 산다") — 1~3층 큰 벽면을 흰(건물_외벽_흰)에서 콘크리트로 바꿨다.
        몰딩 띠·창턱 같은 작은 포인트는 흰을 그대로 뒀다(지시에 그렇게 남겨도 된다고 명시됨)."""
    b = Builder()
    white, tile, glass = "건물_외벽_흰", "건물_타일_베이지", "건물_유리창"  # white는 이제 몰딩·창턱 포인트 전용
    dark_glass = "건물_유리_커튼월"                      # 2층 PC방 통유리 — 유리창보다 어둡고 tile로 반복돼 연속된 띠로 보인다
    shutter, concrete, metal = "건물_셔터", "건물_콘크리트", "건물_금속_회색"
    stainless, awning = "건물_금속_스테인리스", "건물_색_초록"
    W, D = 32.0, 22.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H                                       # 25
    f2 = G + FLOOR_H                                   # 40
    f3 = f2 + FLOOR_H                                   # 55
    top = f3 + 3.0                                      # 58 — 얇은 파라펫

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 1층 몸통(옆·뒤는 콘크리트 벽, 앞은 아래서 따로 채운다) + 뒷면 직원용 철문
    b.box(x0, x1, y0, y1, 0.3, G, concrete, skip=("bottom", "top", "-y"))
    b.panel("+y", y1, 6.0, 1.0, 2.4, DOOR_H, metal)
    b.frame("+y", y1, 6.0, 1.0, 2.4, DOOR_H, metal, width=0.3, depth=0.25)
    # 앞면 — 셔터 셋 + 유리문(가운데) + 유리 가게(왼쪽 끝, 위에 초록 차양) — 전부 DOOR_H
    bay_w = 5.0
    bays = ((-12.0, "shop"), (-6.0, "shutter"), (0.0, "door"), (6.0, "shutter"), (12.0, "shutter"))
    for u, kind in bays:
        mat = shutter if kind == "shutter" else glass
        b.panel("-y", y0, u, 1.0, bay_w, DOOR_H, mat)
        b.frame("-y", y0, u, 1.0, bay_w, DOOR_H, metal, width=0.3, depth=0.25)
        if kind == "shop":                              # 동네 상가 느낌 — 차양 하나
            # 🔴 2026-09-12 blender 재검수 — skip=("top",)라 윗면이 없었다. 위에서 내려다보는
            # 게임 카메라엔 윗면이 보여야 한다(밑면은 거의 안 보인다) — skip을 bottom으로 바꾼다.
            b.box(u - bay_w / 2 - 0.3, u + bay_w / 2 + 0.3, y0 - 1.6, y0 + 0.15, DOOR_H + 0.3, DOOR_H + 1.0,
                  awning, skip=("bottom",))
    # 1층 상단 타일 몰딩 띠(전체 둘레)
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, G - 2.0, G, tile, skip=("bottom", "top"))

    # 2층 PC방 — 벽 + 어두운 통유리 띠(frame_mat 멀리언으로 나뉜 연속된 창) + 넓은 가로 간판 + 옆·뒤 창
    b.box(x0, x1, y0, y1, G, f2, concrete, skip=("bottom", "top", "-y"))
    b.box(x0, x1, y0, y1, G, f2, concrete, skip=("bottom", "top", "+y", "-x", "+x"))
    b.windows("-y", y0, (-12.0, -6.0, 0.0, 6.0, 12.0), (27.0,), 5.6, 10.0, mat=dark_glass, frame_mat=metal, frame=0.3,
              sill_mat=white)
    # 정문 유리문 위 작은 차양 — 셔터/가게 차양과 같은 요령, 입구를 도드라지게 한다
    # 🔴 같은 skip=("top",) 버그(위 가게 차양과 동일 패턴) — 같이 고친다.
    b.box(-2.8, 2.8, y0 - 1.3, y0 + 0.15, DOOR_H + 0.3, DOOR_H + 0.9, awning, skip=("bottom",))
    b.sign("사이버넷", "-y", y0, 0.0, 37.0, "건물_색_검정", "건물_네온_분홍", size=4.5, pad=0.8, max_width=27.0)
    b.windows("+y", y1, (-9.0, 0.0, 9.0), (30.0,), 3.6, 5.0, mat=glass, frame_mat=metal, sill_mat=white)
    b.windows("-x", x0, (-3.0, 3.0), (30.0,), 3.2, 4.6, mat=glass, frame_mat=metal, sill_mat=white)
    b.windows("+x", x1, (-3.0, 3.0), (30.0,), 3.2, 4.6, mat=glass, frame_mat=metal, sill_mat=white)
    # 「PC」 세로 돌출 간판 — 정면 왼쪽 모서리에서 −y로 직각으로 튀어나온다(진짜 블레이드 사인, adc31ae1)
    b.blade_sign("P\nC", x0 + 3.2, y0, G + 1.0, "건물_색_검정", "건물_네온_분홍", size=3.5, pad=0.6, max_width=4.0,
                 bracket_mat=metal)

    # 3층 사무실 — 창틀·창턱 있는 창, 옆·뒤도 창
    b.box(x0, x1, y0, y1, f2, f3, concrete, skip=("bottom", "top", "-y"))
    b.box(x0, x1, y0, y1, f2, f3, concrete, skip=("bottom", "top", "+y", "-x", "+x"))
    b.windows("-y", y0, (-12.0, -6.0, 0.0, 6.0, 12.0), (f2 + 4.0,), 4.0, 7.0, mat=glass, frame_mat=metal, sill_mat=white)
    b.windows("+y", y1, (-9.0, 0.0, 9.0), (f2 + 4.5,), 3.6, 5.0, mat=glass, frame_mat=metal, sill_mat=white)
    b.windows("-x", x0, (-3.0, 3.0), (f2 + 4.5,), 3.2, 4.6, mat=glass, frame_mat=metal, sill_mat=white)
    b.windows("+x", x1, (-3.0, 3.0), (f2 + 4.5,), 3.2, 4.6, mat=glass, frame_mat=metal, sill_mat=white)
    # 층간 띠(2/3층 경계) — 1층 몰딩과 같은 요령
    b.box(x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, f2 - 0.5, f2 + 0.2, white, skip=("bottom", "top"))

    # 모서리 홈통(빗물받이) — 네 모서리를 바깥으로 살짝 띄워 세로로 잇는다
    for cx, cy in ((x0, y0), (x0, y1), (x1, y0), (x1, y1)):
        ox = -0.25 if cx == x0 else 0.25
        oy = -0.25 if cy == y0 else 0.25
        b.beam((cx + ox, cy + oy, 0.3), (cx + ox, cy + oy, top), 0.3, 0.3, metal)

    # 옥상 — 얇은 파라펫 + 실외기 셋 + 물탱크 + 환기구
    b.face(((x0, y0, f3), (x1, y0, f3), (x1, y1, f3), (x0, y1, f3)), "건물_옥상_방수")
    t = 0.5
    # 🔴 2026-09-12 PM 검수(카메라각 뒷면 검사) — skip에 "top"이 있으면 이 얇은 파라펫 링이
    # 열린 뚜껑이라, 비스듬한 카메라가 그 틈으로 들어가 반대쪽 안쪽 벽면을 뒤에서 보게 된다
    # (뒤(+Y) 방위에서 (−15.9, 10.5, 57.8) 넓이 96으로 발견). skip=("bottom",)로 닫는다.
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, f3, top, concrete, skip=("bottom",))
    for ux in (-12.0, -6.5, -1.0):
        b.box_c(ux, 6.0, 2.6, 1.4, f3, 2.0, metal, skip=("bottom",))
    b.cylinder(-11.0, -3.0, f3, f3 + 3.0, 1.3, 10, stainless, top_radius=1.2, base_cap=True)
    b.cylinder(8.0, -5.0, f3, f3 + 2.0, 0.5, 8, metal, base_cap=True)
    b.box_c(11.0, -8.0, 2.4, 2.4, top, 1.8, concrete, skip=("bottom",))            # 계단실 지붕 위 출입 박스
    b.railing([(x0 + 0.7, y0 + 0.7, top), (x1 - 0.7, y0 + 0.7, top),
               (x1 - 0.7, y1 - 0.7, top), (x0 + 0.7, y1 - 0.7, top)], 1.0, metal, post_gap=2.2, closed=True)
    b.cylinder(11.0, 3.0, top, top + 1.5, 0.4, 8, metal, base_cap=True)            # 환기구 하나 더
    return b


# ──────────────────────────────────────────────────────────── 09 7탄약창

def make_ammo_depot():
    """반원형 탄약고(퀀셋) — 폭 30(반지름 15)이 땅에 바로 앉아 자연스러운 높이 ~15.3만 나온다(blender
    지시대로 억지로 안 키운다). 규격 높이(40~90)는 별도의 **감시탑(망루)**이 담당한다 — 기둥 넷 위에
    (이번엔 넓힌) 초소 + 사방 창 + 난간 데크 + X자 가새, 뒤쪽에 사다리. 위병소는 이번엔 진짜 작은
    집(문·창·처마지붕)으로, 정문은 문주(기둥 둘+가로판) 위에 큼직한 「7탄약창」 간판을 얹는다. 담장 위에는
    윤형 철조망 카드, 담장 자체는 철망 카드 — 전부 나무 「잎카드」와 같은 요령(카드 한 장이 다발/그물
    텍스처를 통째로 담는다).

    2026-09-12 blender 재검수 지시 반영:
      - 위병소가 "기둥 하나"처럼 보였다 → 작은 집으로 다시 짓고, 간판은 정문 문주로 옮긴다.
      - 반원통 마구리(끝면)에 철문+환기구, 곡면 밑동에 콘크리트 기단 한 단.
      - 감시탑 초소를 키우고 사방 창·데크 난간·기둥 사이 X자 가새 추가.
      - 펜스에 경고판(「관계자외 출입금지」) 하나."""
    b = Builder()
    corrugated, concrete = "건물_금속_골함석", "건물_콘크리트"
    wire_fence, razor_wire = "건물_철망_잎카드", "건물_철조망_잎카드"
    metal, glass = "건물_금속_회색", "건물_유리창"
    white, black, red, warn = "건물_색_흰", "건물_색_검정", "건물_색_빨강", "건물_도장_주홍"
    x0, x1, y0, y1 = -20.0, 20.0, -18.0, 18.0            # 담장 안쪽(40×36, 45×45 안)

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 반원형 탄약고 — x축을 따라 눕는다(폭 30 = y축 방향 지름), 마당 뒤쪽 절반에 자리.
    # 곡면 밑동을 따라 콘크리트 기단 한 단(플린스) — 곡면이 바로 바닥에서 시작하지 않고 낮은 단 위에 앉는다.
    dx0, dx1, radius = -14.0, 14.0, 15.0
    # 🔴 top은 skip하지 않는다 — 반원통 마구리(반지름 15)보다 기단이 넓어(±0.3) 튀어나온 테두리가
    # 생기는데, top을 빼면 그 테두리가 뚫려 보인다(위에서 보면 구멍). 안 보일 거라 지웠다가 처음
    # 렌더에서 잡았다.
    b.box(dx0 - 0.3, dx1 + 0.3, -radius - 0.3, radius + 0.3, 0.0, 0.8, concrete, skip=("bottom",))
    b.half_cylinder(dx0, dx1, 0.0, radius, 0.8, 10, corrugated, end_mat=corrugated)
    # 출입구 — 반원통의 평평한 끝면(−x쪽)에 셔터 문. 반대쪽(+x) 끝면엔 철문+환기구.
    # 🔴 마구리는 반지름 15의 반원판이라 y=0 기준 세로 여유가 15뿐이다(기단 위 0.8부터) — 문·환기구
    # 전부 그 안(z ≤ 15.8)에 들어가야 돔 실루엣 밖으로 안 튀어나온다.
    b.panel("-x", dx0, 0.0, 0.8, 6.0, 8.0, "건물_셔터", offset=0.1)
    b.panel("+x", dx1, 0.0, 0.8, 2.4, 8.0, metal, offset=0.1)
    b.frame("+x", dx1, 0.0, 0.8, 2.4, 8.0, metal, width=0.3, depth=0.25)
    b.box(dx1, dx1 + 0.6, -0.8, 0.8, 10.0, 11.0, metal, skip=("-x",))     # 환기 루버 — 문 바로 위, 돔 밖으로 안 튀어나옴

    # 감시탑(망루) — 기둥 넷(사이 X자 가새) 위에 넓힌 초소(사방 창 + 데크 난간), 총고 ~42로 규격 높이를 담당
    tx, ty = -16.0, -12.0
    posts = ((tx - 2.6, ty - 2.6), (tx + 2.6, ty - 2.6), (tx + 2.6, ty + 2.6), (tx - 2.6, ty + 2.6))
    deck_z = 37.0
    cabin_h = 5.0
    for px, py in posts:
        b.box_c(px, py, 0.6, 0.6, 0.3, deck_z, metal, skip=("bottom",))
    brace_z = (0.3 + deck_z) * 0.5                       # 기둥 사이 X자 가새 — 앞·옆 두 면
    for (ax, ay), (bx2, by2) in ((posts[0], posts[1]), (posts[1], posts[2])):
        b.beam((ax, ay, brace_z - 3.0), (bx2, by2, brace_z + 3.0), 0.2, 0.2, metal)
        b.beam((ax, ay, brace_z + 3.0), (bx2, by2, brace_z - 3.0), 0.2, 0.2, metal)
    b.box_c(tx, ty, 7.5, 7.5, deck_z - 0.4, 0.4, metal)                          # 데크 바닥(기둥보다 넓다)
    b.railing([(tx - 3.6, ty - 3.6, deck_z), (tx + 3.6, ty - 3.6, deck_z),
               (tx + 3.6, ty + 3.6, deck_z), (tx - 3.6, ty + 3.6, deck_z)], 1.0, metal, post_gap=2.4, closed=True)
    b.box_c(tx, ty, 6.5, 6.5, deck_z, cabin_h, white, skip=("bottom",))          # 넓힌 초소
    b.box_c(tx, ty, 7.3, 7.3, deck_z + cabin_h, 0.5, concrete)                   # 지붕 캡(처마 내밀기), 총고 ≈43
    b.cylinder(tx - 3.0, ty - 3.0, deck_z + cabin_h + 0.5, deck_z + cabin_h + 1.8, 0.15, 6, metal, base_cap=True)
    b.box_c(tx - 3.0, ty - 3.0, 0.7, 0.7, deck_z + cabin_h + 1.8, 0.3, white)      # 탐조등
    for side, plane in (("-y", ty - 3.25), ("+y", ty + 3.25), ("-x", tx - 3.25), ("+x", tx + 3.25)):
        u = tx if side in ("-y", "+y") else ty
        b.windows(side, plane, (u,), (deck_z + 1.0,), 2.4, 2.4, mat=glass, frame_mat=metal)
    for dx in (-0.8, 0.8):                                                       # 사다리 — 뒤쪽(+y)에 붙는다
        b.beam((tx + dx, ty + 2.4, 0.3), (tx + dx, ty + 2.4, deck_z), 0.25, 0.25, metal)
    rungs = 12
    for k in range(1, rungs):
        z = 0.3 + (deck_z - 0.3) * k / rungs
        b.beam((tx - 0.8, ty + 2.4, z), (tx + 0.8, ty + 2.4, z), 0.2, 0.2, metal)

    # 위병소 — 진짜 작은 집. 🔴 2026-09-12 PM 재지시 — 바닥 8×6·벽 GROUND_H(25)는 반원통(높이
    # ~16)보다 훨씬 높은 좁은 기둥처럼 보였다. 바닥 12×9로 넓히고 벽은 문(DOOR_H)만 들어가면
    # 되는 24로 낮췄다(펜스 y0=-18과 안 겹치게 gy도 -14→-13으로 당겼다). 정면 문 옆에 창을
    # 하나 더 내 "집"으로 읽히게 하고, 처마 지붕도 0.5→1.2로 두껍게 했다.
    gx, gy = 9.0, -13.0
    gw, gd = 12.0, 9.0
    gwall_h = 24.0
    b.box_c(gx, gy, gw, gd, 0.3, gwall_h, concrete, skip=("bottom", "top"))
    # 🔴 2026-09-12 blender 재검수(선택 사항, 반영함) — 문 폭 2.2는 "가느다란 유리 줄"처럼
    # 보였다. 5.0으로 넓혀 실제 출입문답게 하고, 옆 창(gx+2.0)과 안 겹치게 그대로 둔다
    # (문 우측 끝 gx+0.5, 창 좌측 끝 gx+1.0 — 간격 0.5 유지).
    b.panel("-y", gy - gd / 2, gx - 2.0, 0.3, 5.0, DOOR_H, glass)
    b.frame("-y", gy - gd / 2, gx - 2.0, 0.3, 5.0, DOOR_H, metal, width=0.3, depth=0.25)
    b.windows("-y", gy - gd / 2, (gx + 2.0,), (10.0,), 2.0, 3.0, mat=glass, frame_mat=metal, sill_mat=white)
    b.windows("+x", gx + gw / 2, (gy,), (10.0,), 2.0, 3.0, mat=glass, frame_mat=metal, sill_mat=white)
    b.box_c(gx, gy, gw + 1.6, gd + 1.6, gwall_h, 1.2, concrete)                  # 처마 나온 평지붕(0.5→1.2로 두껍게)
    b.cylinder(gx + gw / 2 + 1.2, gy + 2.5, 0.3, 2.0, 0.6, 10, metal, base_cap=True)  # 옆 드럼통(연료)

    # 정문 — 문주(기둥 둘, 이전 6→10로 키움) 위에 「7탄약창」 큰 간판 + 차단봉(주홍은 여기 하나뿐)
    # 🔴 2026-09-12 PM 규칙 — 간판 글자 높이 ≥ 건물 폭(40.6)의 1/12(≈3.4). size 2.4는 너무 작았다 →
    # 3.4로 키우고, 4글자가 그 크기로 들어가려면 판이 넓어져(max_width 10→14) 문주 간격도 6→8로 벌렸다.
    gate_x = 8.0
    for px in (-gate_x, gate_x):
        b.box_c(px, y0, 1.0, 1.0, 0.3, 10.0, concrete, skip=("bottom",))
    b.beam((-gate_x, y0, 9.7), (gate_x, y0, 9.7), 0.8, 0.6, concrete)            # 문주 상인방
    # 🔴 2026-09-12 PM 검수(카메라각 뒷면 검사) — 정문 문주는 벽이 없는 자유형 구조물이라
    # b.sign()의 skip=("-y",)(벽에 붙는다는 전제)가 판 안쪽 면을 비워, 안(북쪽, 부지 안)에서
    # 보면 그 자리가 뚫려 보였다(11 日本 편액과 같은 원인). floating_sign()으로 6면 다 막는다.
    floating_sign(b, "7탄약창", "-y", y0 - 0.3, 0.0, 10.0, white, black, size=3.4, pad=0.7, max_width=14.0)
    b.beam((-gate_x + 1.5, y0, 4.0), (gate_x - 1.5, y0, 4.0), 0.25, 0.15, warn)  # 차단봉
    b.box_c(-gate_x + 1.5, y0 + 0.6, 1.0, 1.0, 0.3, 3.0, metal, skip=("bottom",))  # 차단기 조작함

    # 담장 — 철망(높이 7) + 그 위 윤형철조망(카드, 높이 2). 정문 폭만 비운다. 경고판 하나.
    def fence_run(side, plane, u0, u1, gap=None):
        u = u0
        while u < u1:
            w = min(4.0, u1 - u)
            center = u + w / 2
            if not (gap and gap[0] < center < gap[1]):
                b.panel(side, plane, center, 0.3, w, 7.0, wire_fence)
                b.panel(side, plane, center, 7.3, w, 2.0, razor_wire)
                # 지주 — 칸 경계마다 얇은 금속 기둥. 그물 펜스만 있으면 밋밋해서 사실감을 더한다.
                px, py, _ = b._on_side(side, plane, u)
                b.box_c(px, py, 0.2, 0.2, 0.3, 7.3, metal, skip=("bottom",))
            u += w
        px, py, _ = b._on_side(side, plane, u1)                # 끝 기둥
        b.box_c(px, py, 0.2, 0.2, 0.3, 7.3, metal, skip=("bottom",))

    fence_run("-y", y0, x0, x1, gap=(-gate_x - 1.5, gate_x + 1.5))
    fence_run("+y", y1, x0, x1)
    fence_run("-x", x0, y0, y1)
    fence_run("+x", x1, y0, y1)
    # 🔴 PM 규칙 — 경고판도 1/12 이상(≈3.4). size 1.0은 너무 작았다. 4글자×2줄이라 max_width도 넉넉히.
    # 🔴 같은 이유(펜스에 매단 경고판도 벽이 없다) — floating_sign()으로 6면 다 막는다.
    floating_sign(b, "관계자외\n출입금지", "+y", y1, 10.0, 0.3, white, red, size=3.4, pad=0.7, max_width=14.0)
    # 탄약고 앞 탄약상자 더미 — 군용 창고다운 소품
    for k, (cx, cy) in enumerate(((dx0 + 4.0, 6.0), (dx0 + 5.4, 6.0), (dx0 + 4.7, 6.6), (dx0 + 6.8, 6.0))):
        b.box_c(cx, cy, 1.2, 0.8, 0.3 + k * 0.02, 0.7, concrete, skip=("bottom",))
    return b


# ──────────────────────────────────────────────────────────── 10 동양미래대학교

def make_university():
    """붉은 벽돌 대학 본관 3층 + 정문 — blender 승인("좋습니다") + PM 색상 규칙 반영: 01·05도 붉은
    벽돌이라 그것만으론 안 구별된다 — 벽돌은 대학 건물다움을 위해 그대로 쓰되, 흰 석재(콘크리트)
    기둥·현관 포치·코니스·창틀(스테인리스)을 강하게 넣어 "벽돌+흰 석재" 조합으로 01·05(순수 벽돌)와
    구별한다. 정문은 본관 포치 바로 앞(작은 전정)에 붙여 45×45 하나의 바운딩박스 안에 같이 들어가게
    한다 — check()가 오브젝트 하나의 바운딩박스만 재므로, 정문을 멀리 두면 원점(중심)이 앞으로
    쏠려 규격에서 벗어난다(실측 후 정문 거리를 좁혀 맞췄다).
    2026-09-12 PM 규칙: 간판 글자 높이 ≥ 폭(42)/12=3.5 → 3.6, 명패는 스테인리스."""
    b = Builder()
    brick, concrete, white = "건물_벽돌_붉은", "건물_콘크리트", "건물_외벽_흰"
    glass, stainless, flag_mat = "건물_유리창", "건물_금속_스테인리스", "건물_국기_태극기"
    W, D = 42.0, 16.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2          # -21..21, -8..8
    G = GROUND_H                                            # 25 — 현관 1층
    f2 = G + FLOOR_H                                        # 40
    f3 = f2 + FLOOR_H                                       # 55
    top = f3 + 3.0                                          # 58

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 본관 몸통 — 벽돌 + 층간 흰 코니스 두 줄(1/2층·2/3층 경계)
    b.box(x0, x1, y0, y1, 0.3, f3, brick, skip=("bottom", "top"))
    for z in (G, f2):
        b.box(x0 - 0.15, x1 + 0.15, y0 - 0.15, y1 + 0.15, z - 0.5, z + 0.3, white, skip=("bottom", "top"))

    # 창 — 정면(가운데 폭은 현관 장식 때문에 비운다) + 옆·뒤. 전부 스테인리스 창틀 + 콘크리트 창턱
    # ("벽돌+흰 석재" 통일감 — 축소판 흰 석재를 건물 전체 창에 반복해 준다).
    front_us = (-15.0, -9.0, 9.0, 15.0)
    b.windows("-y", y0, front_us, (6.0,), 3.4, 4.6, mat=glass, frame_mat=stainless, sill_mat=concrete)
    b.windows("-y", y0, front_us, (G + 4.5, f2 + 4.5), 3.4, 5.2, mat=glass, frame_mat=stainless, sill_mat=concrete)
    back_us = (-15.0, -8.0, 0.0, 8.0, 15.0)
    b.windows("+y", y1, back_us, (6.0, G + 4.5, f2 + 4.5), 3.2, 4.6, mat=glass, frame_mat=stainless, sill_mat=concrete)
    b.windows("-x", x0, (-4.0, 4.0), (6.0, G + 4.5, f2 + 4.5), 3.0, 4.2, mat=glass, frame_mat=stainless, sill_mat=concrete)
    b.windows("+x", x1, (-4.0, 4.0), (6.0, G + 4.5, f2 + 4.5), 3.0, 4.2, mat=glass, frame_mat=stainless, sill_mat=concrete)

    # 현관 포치 — 흰 석재(콘크리트) 기둥 넷 + 상인방. 대학 본관의 상징적 요소.
    py0 = y0 - 3.0                                          # 포치 깊이 3
    for cx in (-4.5, -1.5, 1.5, 4.5):
        b.cylinder(cx, py0 + 0.3, 0.3, 23.0, 0.55, 10, concrete, top="none", base_cap=True)
    b.box(-6.0, 6.0, py0, y0, 23.0, 24.6, concrete)                     # 상인방(architrave)
    b.panel("-y", y0, 0.0, 0.3, 5.6, DOOR_H, glass, offset=0.05)
    # bottom=False — z=0.3(거의 바닥)에서 기본 문턱 트림을 내리면 폭(0.35)이 z보다 커서 바닥
    # 아래(-0.05)로 파고든다(check() "바닥이 z=-0.05" 오류로 처음 잡았다). 진짜 지면 높이 문에는
    # 밑 문턱 장식이 필요 없기도 하다.
    b.frame("-y", y0, 0.0, 0.3, 5.6, DOOR_H, stainless, width=0.35, depth=0.3, bottom=False)

    # 간판·시계 — 현관 위, 콘크리트 명판 + 스테인리스 글자. 시계는 간판 위(겹치지 않게 z를 띄웠다).
    b.sign("동양미래대학교", "-y", y0, 0.0, 26.0, concrete, stainless, size=3.6, pad=0.8, max_width=30.0)
    b.frame("-y", y0, 0.0, 32.0, 3.6, 3.6, stainless, width=0.2, depth=0.2)
    b.panel("-y", y0, 0.0, 32.0, 3.6, 3.6, "건물_시계판")

    # 옥상 — 방수 + 흰 코니스 트림 + 콘크리트 파라펫
    b.face(((x0, y0, f3), (x1, y0, f3), (x1, y1, f3), (x0, y1, f3)), "건물_옥상_방수")
    # 🔴 2026-09-12 PM 검수(카메라각 뒷면 검사) — 두 링(코니스 띠·파라펫) 모두 skip에 "top"이
    # 있으면 열린 뚜껑이라, 비스듬한 카메라가 그 틈으로 들어가 반대쪽 안쪽 벽면을 본다(파라펫
    # 좌·우 조각에서 실측 확인: (±21.0, −7.4, 57.6) 넓이 39). 08 사이버넷과 같은 원인·같은 수정
    # (skip=("bottom",)만) — 코니스 띠도 같은 구조라 예방으로 같이 닫는다.
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, f3 - 0.4, f3 + 0.4, white, skip=("bottom",))
    t = 0.5
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, f3 + 0.4, top, concrete, skip=("bottom",))

    # 정문 — 문주(간격 16, 통로 높이 22) + 국기 게양대. 포치 바로 앞(전정 1.5)에 붙여 원점이 안 쏠리게
    # 한다 — 처음 3.5로 뒀을 때 check()가 "원점이 가운데가 아님(중심 0.0, -3.3)"으로 잡았다(허용 ±3.0).
    gy = py0 - 1.5
    for gx in (-8.0, 8.0):
        b.box_c(gx, gy, 1.6, 1.6, 0.3, 24.0, concrete, skip=("bottom",))
    b.beam((-8.0, gy, 22.0), (8.0, gy, 22.0), 1.6, 2.0, concrete)       # 상인방 밑면 22(통로 높이)
    fx, fy = 12.0, gy
    b.cylinder(fx, fy, 0.3, 20.0, 0.25, 8, stainless, top="point", base_cap=True)
    fbx, fby, fbz = fx + 0.25, fy, 16.0                                 # 태극기 — 게양대 옆에 얇은 판 두 장(양면)
    corners = [(fbx, fby, fbz), (fbx + 3.6, fby, fbz), (fbx + 3.6, fby, fbz + 2.4), (fbx, fby, fbz + 2.4)]
    b.face(corners, flag_mat)
    b.face(list(reversed(corners)), flag_mat)
    return b


# ──────────────────────────────────────────────────────────── 11 日本

def make_japan():
    """일본식 목조 건물(단층, 모임지붕) + 앞마당 도리이. blender 재검수 지시(초안은 벽15+지붕30로
    지붕이 벽의 두 배라 무거웠다) 반영 — 돌기단 2 + 벽 GROUND_H(25, 문이 있어서) + 모임지붕
    rise13·처마내밀기3 → 총고 40(+용마루 장식 0.8) = 40.8. 팔레트(PM 지시)는 짙은 나무(하부)+
    흰 회벽(상부, 처마 밑 7만)+검은 기와 — 다른 스토리와 안 겹친다. 도리이는 편액 「日本」을 단다
    (AppleSDGothicNeo에 한자 있음, blender 확인).

    2026-09-12 blender 2차 재검수 반영(초안 694삼각형이 너무 비었고 도리이가 현관을 가렸다.
    목표 2,000~3,000삼각형):
      1) 도리이~처마 간격을 7(6~8 지시 범위)로 벌린다. 벽을 y로 3.8 뒤로 밀어(YSHIFT) 도리이가
         멀어져도 전체 바운딩박스 중심이 원점 근처(±3.0)에 남게 한다(10번 정문과 같은 요령).
      2) 도리이~현관 사이에 판석 디딤길, 가사기를 검정+양끝 위로 들리게(beam 두 조각 추가), 양옆에
         돌 등롱 한 쌍.
      3) 창을 쇼지 격자(흰 종이 판 여러 장 + 가는 나무틀)로 — shoji() 도우미가 격자 하나마다
         panel+frame을 따로 찍는다(테두리가 서로 붙어 격자선처럼 보인다). 정면에 낮은 툇마루."""
    b = Builder()
    wood, plaster, tile_roof = "건물_나무_판", "건물_외벽_흰", "건물_지붕_기와"
    black, vermilion, stone, glass = "건물_색_검정", "건물_도장_주홍", "건물_콘크리트", "건물_유리창"
    paper = "건물_색_흰"
    W, D = 30.0, 20.0
    YSHIFT = 3.8                                            # 벽을 뒤로 미는 만큼 — 도리이가 멀어진 걸 상쇄
    x0, x1 = -W / 2, W / 2                                  # -15..15
    y0, y1 = -D / 2 + YSHIFT, D / 2 + YSHIFT                # -6.2..13.8
    plinth_h = 2.0
    wall_top = plinth_h + GROUND_H                          # 27
    roof_rise, overhang = 13.0, 3.0

    def shoji(side, plane, us, zs, w, h, cols=2, rows=2):
        """쇼지 창 — 격자 하나마다 흰 종이 판 + 가는 나무틀(frame)을 따로 찍는다. 인접한 틀끼리
        붙어(간격 0.15) 격자선처럼 보인다 — 진짜 격자 메시 대신 이 요령으로 충분하다."""
        cw, ch = w / cols, h / rows
        for z in zs:
            for u in us:
                for c in range(cols):
                    for r in range(rows):
                        cu = u - w / 2 + cw * (c + 0.5)
                        cz = z + ch * r
                        b.panel(side, plane, cu, cz, cw - 0.15, ch - 0.15, paper)
                        b.frame(side, plane, cu, cz, cw - 0.15, ch - 0.15, wood, width=0.12, depth=0.12)

    # 돌 기단
    b.box(x0 - 0.5, x1 + 0.5, y0 - 0.5, y1 + 0.5, 0.0, plinth_h, stone, skip=("bottom",))

    # 벽 — 하부 나무판 + 상부 흰 회벽(처마 밑 7만) + 경계 검정 띠(목조 트림)
    plaster_z = plinth_h + 18.0
    b.box(x0, x1, y0, y1, plinth_h, plaster_z, wood, skip=("bottom", "top"))
    b.box(x0, x1, y0, y1, plaster_z, wall_top, plaster, skip=("bottom", "top"))
    b.box(x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, plaster_z - 0.3, plaster_z + 0.3, black, skip=("bottom", "top"))

    # 문 — 정면 가운데, 검정 프레임(목조 트림, 유리 미닫이문 근사 — 쇼지 대상 아님).
    # 창 — 정면 좌우 + 옆·뒤, 전부 쇼지 격자(2×2)로.
    b.panel("-y", y0, 0.0, plinth_h, 4.4, DOOR_H, glass, offset=0.06)
    b.frame("-y", y0, 0.0, plinth_h, 4.4, DOOR_H, black, width=0.35, depth=0.3)
    shoji("-y", y0, (-8.0, 8.0), (plinth_h + 3.0,), 2.6, 8.0)
    shoji("+y", y1, (-8.0, 0.0, 8.0), (plinth_h + 3.0,), 2.6, 8.0)
    shoji("-x", x0, (-4.0, 4.0), (plinth_h + 3.0,), 2.4, 7.0)
    shoji("+x", x1, (-4.0, 4.0), (plinth_h + 3.0,), 2.4, 7.0)

    # 툇마루 — 정면에 낮은 나무 데크(현관 문턱과 같은 높이) + 받침 기둥 셋
    deck_depth = 2.2
    b.box(x0 - 1.0, x1 + 1.0, y0 - deck_depth, y0, plinth_h - 0.3, plinth_h, wood, skip=("bottom",))
    for px in (x0 - 0.5, 0.0, x1 + 0.5):
        b.box_c(px, y0 - deck_depth / 2, 0.3, 0.3, 0.0, plinth_h - 0.3, wood, skip=("bottom",))

    # 모임지붕 — 처마가 밖으로 overhang만큼 나온 사각형 기준(부르는 쪽이 넓혀서 준다)
    rx0, rx1, ry0, ry1 = x0 - overhang, x1 + overhang, y0 - overhang, y1 + overhang
    b.hip_roof(rx0, rx1, ry0, ry1, wall_top, roof_rise, tile_roof)
    ridge_z = wall_top + roof_rise                          # 40
    for rxe in (-5.5, 5.5):
        b.box_c(rxe, 0.0, 0.8, 0.8, ridge_z, 0.8, black)    # 용마루 끝 장식(오니가와라 근사)
        b.box_c(rxe, 0.0, 0.25, 0.25, ridge_z + 0.8, 0.5, black)  # 그 위 작은 뾰족 장식(장식성 추가)

    # 도리이 — 부지 앞(−y). 처마(ry0)에서 7 더 떨어뜨린다(blender 지시 6~8) — 위 YSHIFT가
    # 그만큼 벽을 뒤로 밀어서 바운딩박스 중심은 여전히 원점 근처다. 통로 높이 22 이상(누키
    # 밑면 기준)·기둥 간격 16.
    ty = ry0 - 7.0
    post_h, nuki_z, kasagi_z = 28.4, 22.4, 27.0             # nuki_z는 빔 중심 — 밑면 = 22.4-0.35=22.05
    for tx in (-8.0, 8.0):
        b.beam((tx, ty, 0.0), (tx, ty, post_h), 1.2, 1.2, vermilion)
    b.beam((-8.0, ty, nuki_z), (8.0, ty, nuki_z), 0.9, 0.7, vermilion)       # 누키(하단 가로보)
    # 가사기 — 검정(blender 지시), 가운데는 곧고 양 끝만 위로 들린다(소리反り 근사, beam 두 조각).
    b.beam((-7.5, ty, kasagi_z), (7.5, ty, kasagi_z), 1.5, 1.0, black)
    b.beam((-7.5, ty, kasagi_z), (-9.7, ty, kasagi_z + 0.7), 1.5, 0.9, black)
    b.beam((7.5, ty, kasagi_z), (9.7, ty, kasagi_z + 0.7), 1.5, 0.9, black)
    # 편액 「日本」 — 누키와 가사기 사이. 검정 글씨(목판에 먹으로 쓴 전통 방식과 같은 느낌).
    # 🔴 2026-09-12 PM 검수(카메라각 뒷면 검사) — b.sign()의 판은 skip=("-y",)라 벽에 붙는
    # 것을 전제한다(뒷면은 벽 속에 묻혀 안 보인다는 가정). 이 편액은 도리이 두 가로보 사이에
    # 떠 있어 벽이 없다 — 뒤(+Y)에서 보면 그 스킵된 뒷면 자리가 뻥 뚫려 보였다(실측:
    # (−2.2, −16.8, 23.9) 넓이 19.5). sign()을 못 바꾸니(공용 파일) 그 로직을 여기 그대로
    # 옮기되 skip 없이 6면 다 막은 판으로 새로 짓는다.
    floating_sign(b, "日本", "-y", ty, 0.0, 23.4, wood, black, size=3.2, pad=0.7, max_width=8.0)

    # 디딤길 — 도리이~툇마루 사이 판석 4장(가운데 한 줄, 간격 2.2). 양옆에 돌 등롱 한 쌍.
    deck_front = y0 - deck_depth
    for k in range(4):
        py = ty + 0.9 + k * ((deck_front - 0.6 - (ty + 0.9)) / 3)
        b.box_c(0.0, py, 1.5, 1.1, 0.0, 0.15, stone)
    for lx in (-3.2, 3.2):
        ly = (ty + deck_front) / 2
        b.cylinder(lx, ly, 0.0, 1.5, 0.32, 8, stone, base_cap=True)          # 등롱 기둥
        b.box_c(lx, ly, 0.85, 0.85, 1.5, 0.65, stone)                        # 불빛 칸(화창)
        b.cylinder(lx, ly, 2.15, 2.55, 0.65, 8, stone, top="point", base_cap=True)  # 갓(삿갓 지붕)
    return b


# ──────────────────────────────────────────────────────────── 12 코드잇

def make_codeit():
    """유리 커튼월 오피스 6개 층(1층 로비 GROUND_H + 4개 층×FLOOR_H + 옥탑 설비). blender 지시:
    커튼월 멀리언은 텍스처(건물_유리_커튼월)에 이미 구워져 있으므로 창을 낱개로 뚫지 않고, 층마다
    큰 판(panel) 하나로 유리를 덮은 뒤 모서리에만 스테인리스 각재로 멀리언 선을 낸다. 원안(40×40
    "통짜")이 밋밋하다는 지적 — 노치(오목 코너)는 Builder에 다각형 바닥 도구가 없어 상자 두 개를
    겹쳐 짓는 손 CSG가 필요해 위험도가 크다. blender가 준 대안("깊이를 줄여도 됨") 쪽을 택해
    36×26 직사각(정사각형보다 슬림한 실루엣)으로 짓는다.
    1층 로비는 스테인리스 클래딩(위층 유리와 다른 재질)+유리창(FIT) 낱개 창으로 상층과 구분.
    2026-09-12 PM 규칙: 간판 「코드잇」 높이 ≥ 폭(36)/12=3.0 → 3.3. 팔레트(유리)는 이미 다른
    스토리와 안 겹친다(01~11 중 유일한 커튼월 유리 타워)."""
    b = Builder()
    curtainwall, glass = "건물_유리_커튼월", "건물_유리창"
    stainless, concrete = "건물_금속_스테인리스", "건물_콘크리트"
    black, neon = "건물_색_검정", "건물_네온_하늘"
    W, D = 36.0, 26.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H                                        # 25 — 로비
    floor_bottoms = [G + k * FLOOR_H for k in range(4)]  # 25·40·55·70 — 2~5층
    top = floor_bottoms[-1] + FLOOR_H                    # 85
    roof_top = top + 3.8                                 # ≈89 — 규격 상한(90) 안, 여유 남김

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 1층 로비 — 스테인리스 클래딩 벽 + 유리창(FIT) 낱개(위층 연속 커튼월과 구분되는 결)
    b.box(x0, x1, y0, y1, 0.3, G, stainless, skip=("bottom", "top"))
    b.windows("-y", y0, (-12.0, -4.0, 4.0, 12.0), (4.0,), 3.4, 14.0, mat=glass, frame_mat=stainless, sill_mat=concrete)
    b.windows("+y", y1, (-12.0, 0.0, 12.0), (4.0,), 3.4, 14.0, mat=glass, frame_mat=stainless, sill_mat=concrete)
    b.windows("-x", x0, (-8.0, 8.0), (4.0,), 3.2, 14.0, mat=glass, frame_mat=stainless, sill_mat=concrete)
    b.windows("+x", x1, (-8.0, 8.0), (4.0,), 3.2, 14.0, mat=glass, frame_mat=stainless, sill_mat=concrete)
    # 로비 유리문 + 캐노피(정문을 도드라지게 하는 요령 — 08 차양과 같은 결)
    b.panel("-y", y0, 0.0, 0.3, 5.6, DOOR_H, glass, offset=0.05)
    b.frame("-y", y0, 0.0, 0.3, 5.6, DOOR_H, stainless, width=0.3, depth=0.25, bottom=False)
    # 🔴 2026-09-12 PM 검수 — skip=("top",)면 캐노피 밑면만 남아 위에서 내려다보는 카메라에
    # 윗면이 안 보인다(08 사이버넷 차양과 같은 종류 버그). skip=("bottom",)로 바로잡는다 —
    # 밑면은 사람이 안 보는 각도라 없어도 되고, 위에서 보이는 윗면이 있어야 한다.
    b.box(-6.0, 6.0, y0 - 2.6, y0 + 0.2, DOOR_H + 0.4, DOOR_H + 1.2, stainless, skip=("bottom",))
    for cx in (-5.6, 5.6):
        b.box_c(cx, y0 - 2.4, 0.3, 0.3, 0.3, DOOR_H + 0.4, stainless, skip=("bottom", "top"))
    # 🔴 2026-09-12 blender 재검수 — size 3.3(폭37/12=3.08)는 통과선 바로 위라 렌더에서 작아
    # 보였다. 4.0으로 키우고 캐노피 위 가로로 크게, max_width도 같이 늘린다.
    b.sign("코드잇", "-y", y0, 0.0, DOOR_H + 2.2, black, neon, size=4.0, pad=0.8, max_width=18.0)

    # 2~5층 — 층마다 스판드럴 띠(스테인리스, 얇게) + 큰 커튼월 유리판(전 면). 코너는 멀리언 각재가 대신한다.
    # 🔴 2026-09-12 PM 검수(카메라각 뒷면 검사) — panel()은 한 겹 판이라 뒤가 없다. 원래 폭을
    # W−4/D−4로 좁혀 코너에 2칸씩 빈틈을 남겼는데(멀리언 각재 1×1만 있고 벽이 없다), 그 틈으로
    # 비스듬히 보면 건물 반대편 벽의 판 뒷면이 그대로 비쳐 보였다(원인: 뚫린 틈, 뒤집힌 면이
    # 아니었다). 판을 건물 폭 그대로(W/D) 늘려 모서리 각재와 만나게 해 틈을 없앤다.
    for zb in floor_bottoms:
        b.box(x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, zb, zb + 1.5, stainless, skip=("bottom", "top"))
        gz, gh = zb + 1.5, FLOOR_H - 1.5                # 유리존 — 스판드럴 위부터 층 꼭대기까지
        b.panel("-y", y0, 0.0, gz, W, gh, curtainwall)
        b.panel("+y", y1, 0.0, gz, W, gh, curtainwall)
        b.panel("-x", x0, 0.0, gz, D, gh, curtainwall)
        b.panel("+x", x1, 0.0, gz, D, gh, curtainwall)

    # 코너 멀리언 — 네 모서리를 스테인리스 각재로 바닥부터 옥상까지 이어(텍스처 안 격자와 이어 보이게)
    for cx, cy in ((x0, y0), (x0, y1), (x1, y0), (x1, y1)):
        b.beam((cx, cy, 0.3), (cx, cy, top), 1.0, 1.0, stainless)

    # 옥상 — 평지붕 + 설비함 + 난간
    b.face(((x0, y0, top), (x1, y0, top), (x1, y1, top), (x0, y1, top)), "건물_옥상_방수")
    t = 0.5
    # 🔴 2026-09-12 PM 검수(카메라각 뒷면 검사) — skip에 "top"이 있으면 이 얇은 파라펫 조각이
    # 열린 뚜껑이 된다. 비스듬한 카메라가 그 뚫린 위쪽으로 들어가 안쪽 벽의 뒷면(정상적으로는
    # 옥상 내부를 향해야 할 면)을 바깥에서 보게 된다. skip=("bottom",)로 닫는다(08·10과 같은 수정).
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, top, top + 0.5, concrete, skip=("bottom",))
    # 옥탑 계단실 박스(가장 큼) + 실외기 여럿 — blender 재검수 지시("설비함 하나뿐"이라 밋밋했다).
    b.box_c(-6.0, 0.0, 8.0, 6.0, top, roof_top - top, "건물_금속_회색", skip=("bottom",))
    for ux, uy in ((6.0, -5.0), (6.0, 0.0), (6.0, 5.0), (-6.0, 6.0)):
        b.box_c(ux, uy, 2.2, 1.3, top, 1.6, "건물_금속_회색", skip=("bottom",))
    b.cylinder(0.0, 6.0, top, top + 1.2, 0.5, 8, stainless, base_cap=True)     # 환기구
    b.railing([(x0 + 0.8, y0 + 0.8, top), (x1 - 0.8, y0 + 0.8, top),
               (x1 - 0.8, y1 - 0.8, top), (x0 + 0.8, y1 - 0.8, top)], 1.0, stainless, post_gap=2.4, closed=True)
    return b


# ──────────────────────────────────────────────────────────── 13 쉬었음

def make_story13():
    """다가구 원룸 3층 — 전부 어두운 창(건물_유리창)인데 2층 정면 왼쪽 창 딱 하나만 원룸창(건물_원룸창,
    불 켜진 방)이다. blender 재해석: "원룸 한 칸"은 독채가 아니라 다가구 건물에서 불 켜진 방 하나로
    본다. 생활 디테일(실외기·가스배관·택배상자)로 "사람이 사는 건물"의 인상을 낸다.

    2026-09-12 blender 2차 재검수 반영:
      1) 🔴 옆벽 창이 옥상 위로 떠 있던 버그 — z 목록에 (G+4, f2+4, f3+4) 세 값을 줬는데 f3는
         "3층의 천장"(f2+FLOOR_H)이지 "4층 바닥"이 아니다(f2가 3층 바닥, f3가 3층 천장 —
         Story08/10과 같은 명명 규칙). f3+4=59는 벽 꼭대기(구 코드 top-0.6=56.4)보다 위라
         허공에 창틀만 떴다. 앞/뒷면처럼 (G+4, f2+4) 두 값만 써야 층 2·3에 맞다.
      2) 옥상이 속 빈 상자였다 — 진짜 벽 높이(f3=55)에서 멈추고 그 위에 파라펫 띠 + 옥상
         방수 바닥(face)을 얹는다(다른 스토리와 같은 요령, 이전엔 top-0.6로 눙쳐 지붕 자체가
         없었다).
      3) 창을 전부 키웠다(폭 2.2~2.6→4.0~4.8, 높이 3.0~3.4→5.0~5.6) — 층 15에 비해 너무
         작았다.
      4) "불 켜진 방"을 확실히 크게(폭6.0×높이6.0) — 작은 파란 점으로 보이던 문제.
      5) 벽을 흰(건물_외벽_흰)에서 콘크리트로 — PM 바탕색 규칙(흰 벽 금지, 다른 스토리와
         구별). blender가 예로 든 두 재질(콘크리트/베이지타일) 중 베이지타일은 04번과 겹쳐
         콘크리트를 택했다(08·09도 콘크리트지만 이 건물은 작고 소박한 저층이라 큰 상가·
         군용 건물과는 매스·스케일로 구별된다).
      6) 간판 size 1.8→2.3(폭 22/12≈1.83 이상 여유), 생활 디테일(실외기·배관·택배상자)은
         유지하되 실외기를 두 배 크기로 키워 눈에 띄게 했다."""
    b = Builder()
    concrete, glass, studio = "건물_콘크리트", "건물_유리창", "건물_원룸창"
    metal, yellow, black = "건물_금속_회색", "건물_색_노랑", "건물_색_검정"
    roof_mat = "건물_옥상_방수"
    W, D = 20.0, 16.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H                                        # 25 — 1층 천장(=2층 바닥)
    f2 = G + FLOOR_H                                    # 40 — 2층 천장(=3층 바닥)
    f3 = f2 + FLOOR_H                                   # 55 — 3층 천장(진짜 벽 꼭대기)
    parapet_h = 1.5
    top = f3 + parapet_h                                # 56.5

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 몸통 — 콘크리트 벽 통짜, 진짜 꼭대기(f3)까지(예전엔 top-0.6로 잘못 낮춰 3층 창이 떴다)
    b.box(x0, x1, y0, y1, 0.3, f3, concrete, skip=("bottom", "top"))

    # 1층 — 현관(DOOR_H) + 옆 창 둘(키움)
    b.panel("-y", y0, 0.0, 0.3, 4.4, DOOR_H, glass, offset=0.06)
    b.frame("-y", y0, 0.0, 0.3, 4.4, DOOR_H, metal, width=0.3, depth=0.25, bottom=False)
    b.windows("-y", y0, (-7.0, 7.0), (4.0,), 4.0, 5.0, mat=glass, frame_mat=metal, sill_mat=concrete)
    # 명판 「쉬었음」 — 현관 옆
    b.sign("쉬었음", "-y", y0, 7.5, 2.0, concrete, black, size=2.3, pad=0.5, max_width=7.0)
    # 택배 상자 — 현관 앞 바닥
    b.box_c(3.2, y0 - 1.4, 0.9, 0.65, 0.3, 0.55, yellow, skip=("bottom",))

    # 2층 — 정면 왼쪽 큰 창 하나만 원룸창(불 켜진 방, 폭6×높이6), 오른쪽은 어두운 유리창(키움)
    b.windows("-y", y0, (-5.5,), (G + 3.0,), 6.0, 6.0, mat=studio, frame_mat=metal, sill_mat=concrete)
    b.windows("-y", y0, (5.5,), (G + 4.0,), 4.5, 5.5, mat=glass, frame_mat=metal, sill_mat=concrete)
    # 3층 — 정면 둘 다 어두운 창(키움)
    b.windows("-y", y0, (-5.5, 5.5), (f2 + 4.0,), 4.5, 5.5, mat=glass, frame_mat=metal, sill_mat=concrete)
    # 뒷면 — 2·3층 어두운 창(대칭 배치, 키움)
    b.windows("+y", y1, (-5.0, 5.0), (G + 4.0, f2 + 4.0), 4.0, 5.0, mat=glass, frame_mat=metal, sill_mat=concrete)
    # 옆면 — 🔴 버그 수정: 층마다(2·3층, z=G+4·f2+4) 창 하나씩만 — 예전엔 f3+4까지 셋을 줘서
    # 벽 밖(허공)에 창틀이 떴었다. 폭·높이도 키웠다.
    b.windows("-x", x0, (0.0,), (G + 4.0, f2 + 4.0), 3.5, 4.5, mat=glass, frame_mat=metal, sill_mat=concrete)
    b.windows("+x", x1, (-3.0,), (G + 4.0, f2 + 4.0), 3.5, 4.5, mat=glass, frame_mat=metal, sill_mat=concrete)

    # 옥상 — 파라펫 띠 + 방수 바닥(예전엔 벽을 낮춰 지붕 자체가 없었다)
    b.face(((x0, y0, f3), (x1, y0, f3), (x1, y1, f3), (x0, y1, f3)), roof_mat)
    t = 0.4
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, f3, top, concrete, skip=("bottom", "top"))

    # 생활 디테일 — +x 옆벽에 실외기 둘(층마다, 창 자리 y=-3.0을 피해 y=4.5에 둔다, 키움) +
    # 세로 가스배관 하나(진짜 벽 꼭대기 f3까지)
    for z0 in (G - 2.4, f2 - 2.4):
        b.box_c(x1 + 0.9, 4.5, 2.4, 1.4, z0, 1.6, metal, skip=("bottom",))
    b.beam((x1 + 0.2, -6.5, 0.3), (x1 + 0.2, -6.5, f3), 0.25, 0.25, metal)
    return b


# ──────────────────────────────────────────────────────────── 목록
#
# (파일 이름, 이름표(영어 — 창 글꼴에서 한글이 깨진다), 만들기, 설명). 파일 이름은 유니티가 참조한다.

CATALOG = [
    ("Story08_사이버넷", "08 Cybernet", make_cybernet,
     "PC방 상가 3층 · 1층 셔터+유리문 · 2층 네온 간판 사이버넷+세로 PC 간판 · 3층 사무실 · 얇은 파라펫"),
    ("Story09_7탄약창", "09 Ammo Depot", make_ammo_depot,
     "반원형 탄약고(퀀셋) · 감시탑(기둥+초소+사다리) · 위병소(작은 집) · 정문 차단봉 · 윤형철조망 담장"),
    ("Story10_동양미래대학교", "10 University", make_university,
     "붉은 벽돌 본관 3층 + 흰 석재 포치·창틀 · 시계 · 태극기 · 스테인리스 명패 · 정문"),
    ("Story11_日本", "11 Japan", make_japan,
     "목조+회벽 단층 + 검은 기와 모임지붕 · 도리이(편액 日本) · 짙은 나무/흰 회벽 팔레트"),
    ("Story12_코드잇", "12 Codeit", make_codeit,
     "유리 커튼월 오피스 6개 층 · 1층 스테인리스 로비 · 코너 멀리언 · 네온 간판 코드잇"),
    ("Story13_쉬었음", "13 Rested", make_story13,
     "다가구 원룸 3층 · 2층 정면 한 칸만 불 켜진 원룸창 · 명판 쉬었음 · 실외기·택배상자"),
]


# ──────────────────────────────────────────────────────────── 화난 버전(08·09)
#
# 기본판 코드는 안 고친다 — angry_variant(maker, ...)가 make_xxx()를 그대로 지은 뒤 위에 덧붙인다.
# tilt_front로 돌릴 상자(u0..u1, z0..z1)는 손으로 어림하지 않고 실제 정점을 조회해서 잡았다 — 08의
# 가로 간판("사이버넷")은 2층 통유리 창틀과 z=37에서 정확히 맞닿아 있어(둘 다 층 경계) 큰 상자로
# 잡으면 옆 창틀까지 같이 돌아간다(직접 조회로 확인). 그래서 08은 겹칠 일이 없는 blade_sign("PC")만
# 기울이고, "사이버넷" 쪽 손상은 tilt 대신 extra()의 검정 판(네온 반쪽 꺼짐)으로 표현한다.

def _angry_cybernet_extra(b):
    """사이버넷다운 파손 — ① 가로 간판 오른쪽 절반에 검정 판을 덧대 네온 반쪽이 꺼진 것처럼
    ② 셔터 한 칸(오른쪽 끝, u=12)에 그을린 금속 색 얼룩 ③ 옥상 물탱크 옆에 연기 자리.
    좌표는 make_cybernet과 같은 기준(사인 보드 x −7.07..7.07·z 37.00..41.84, 실측 확인)."""
    black, scorch = "건물_색_검정", "건물_금속_그을림"
    # ① 네온 반쪽 꺼짐 — 보드+글자 앞. 실측(직접 정점 조회)으로 글자 앞면이 벽에서 0.92만큼 나와
    # 있는 걸 확인했다(0.75·0.9 둘 다 렌더로 시도했지만 근소하게 글자 앞면보다 뒤라 안 가려졌다) —
    # 1.05로 확실한 여유를 둔다.
    b.panel("-y", -11.0, 3.6, 36.9, 7.4, 5.2, black, offset=1.05)
    # ② 셔터 그을음 자국 — 오른쪽 끝 셔터(u=12) 위에 얼룩 판
    b.panel("-y", -11.0, 12.0, 3.0, 3.6, 6.0, scorch, offset=0.16)
    # ③ 연기 자리 — 옥상 물탱크(−11.0,−3.0) 바로 위. smoke=0으로 자동 배치와 안 겹치게 한다.
    b.markers.append(("연기_자리_01", Vector((-11.0, -3.0, 59.5))))


def _angry_ammo_depot_extra(b):
    """탄약창다운 파손 — ① 감시탑 초소 정면에 작은 창 두 개를 새로 내 "눈"으로 삼는다(기존 정면 창
    하나는 그대로 둔다 — 두 눈 사이 "코"처럼 남아 자연스럽다) ② 뒷담장(+y) 한 구간이 뜯겨 바깥으로
    젖혀짐(hexa) ③ 정문 오른쪽 기둥 위에 빨간 경고등 ④ 마구리 환기구 옆에 연기 자리.
    좌표는 make_ammo_depot과 같은 기준(감시탑 tx=−16·ty=−12·deck_z=37, 정문 기둥 gate_x=8·y0=−18)."""
    glass, metal, red, wire_fence = "건물_유리창", "건물_금속_회색", "건물_색_빨강", "건물_철망_잎카드"
    # ① 눈 — 기존 정면 창(u=−16) 좌우로 작은 창 두 개, 겹치지 않을 만큼만 떨어뜨렸다(실측 확인:
    # 기존 창 u −17.2..−14.8, 새 창은 그 바깥쪽).
    b.windows("-y", -15.25, (-18.1, -13.9), (38.5,), 1.2, 1.2, mat=glass, frame_mat=metal)
    # 눈썹 — 창 위에서 가운데(기존 창 쪽)로 처지는 그을린 차양 한 쌍
    b.brow("-y", -15.25, (-19.5, 41.0), (-16.5, 39.5))
    b.brow("-y", -15.25, (-12.5, 41.0), (-15.5, 39.5))
    # ② 뜯긴 철망 — 뒷담장(+y, plane=18.0) 한 구간을 아래는 그대로(경첩), 위는 바깥·위로 젖혀지게
    b.hexa(((0.5, 18.0, 2.0), (3.5, 18.0, 2.0), (3.5, 18.0, 2.3), (0.5, 18.0, 2.3),
            (0.5, 19.8, 5.5), (3.5, 19.6, 5.8), (3.5, 19.6, 6.1), (0.5, 19.8, 5.8)), wire_fence)
    # ③ 경고등 — 정문 오른쪽 기둥(gate_x=8, y0=−18) 꼭대기
    b.box_c(8.0, -18.0, 0.5, 0.5, 10.0, 0.4, red, skip=("bottom",))
    # ④ 연기 자리 — 마구리(+x, dx1=14) 환기 루버 옆
    b.markers.append(("연기_자리_01", Vector((14.6, 0.0, 11.5))))
    # ⑤ 위병소 얼굴 — 정면 창(u=11.0, z 10~13, PM 지시로 swap이 이미 붉게 만든다) 위에
    # 그을린 눈썹. 문(u 4.5~9.5) 쪽으로 처지게 해 "붉은 문+눈썹"이 얼굴로 읽히게 한다.
    b.brow("-y", -17.5, (13.0, 15.0), (9.7, 13.4))
    # ⑥·⑦ 지붕 파손(PM 지시, 내려다보는 카메라에서 가장 크게 보이는 부분) — 돔 좌표는
    # make_ammo_depot과 같은 규칙(half_cylinder 프로파일: y=cos(a)·radius, z=dz0+sin(a)·radius,
    # a=0이 +y쪽·a=180°가 −y쪽). 정문(−y)에서 올려다보이는 남쪽 사면(a 90°~180°)에 배치한다.
    dx0, dx1, radius, dz0 = -14.0, 14.0, 15.0, 0.8
    soot = "건물_그을음_잎카드"
    right = Vector((1.0, 0.0, 0.0))

    def dome_point(angle_deg, x):
        a = math.radians(angle_deg)
        n = Vector((0.0, math.cos(a), math.sin(a)))
        c = Vector((x, n.y * radius, dz0 + n.z * radius))
        return c, n

    # ⑥ 그을음 — 옅게 띄운 판 두 장. `_잎카드` 재질이라 카메라각 뒷면검사 예외(한 겹으로 충분).
    for ang, cx, hw in ((112.0, -6.0, 3.0), (144.0, 5.0, 2.6)):
        c, n = dome_point(ang, cx)
        up = n.cross(right).normalized()
        c = c + n * 0.06
        hh = hw * 0.55
        b.face((c - right * hw - up * hh, c + right * hw - up * hh,
                c + right * hw + up * hh, c - right * hw + up * hh), soot)

    # ⑦ 찢겨 말려 올라간 골판 — 경첩(lo, 곡면에 거의 붙음)에서 반대쪽(hi)이 바깥·위로 들린
    # 두꺼운 판(hexa, 진짜 두께를 줘서 카메라각 뒷면검사에 안 걸린다). 들린 틈으로 붉은 불빛.
    c, n = dome_point(128.0, 0.0)
    up = n.cross(right).normalized()
    hw2 = 2.2
    lo, hi = c - up * 1.0, c + up * 1.6
    lift = n * 1.6 + up * 0.6
    inner = (lo - right * hw2, lo + right * hw2, hi + right * hw2 + lift, hi - right * hw2 + lift)
    outer = tuple(p + n * 0.08 for p in inner)
    b.hexa(inner + outer, "건물_금속_골함석")
    # 안쪽 붉은 불빛 — 두 겹 사이(안쪽 면 바로 앞)에 얇은 판 한 장
    glow0, glow1, glow2, glow3 = (p + n * 0.03 for p in inner)
    b.face((glow0, glow1, glow2, glow3), "건물_창_분노")


def _angry_university_extra(b):
    """대학 본관다운 파손 — ① 시계판에 금(깨진 시계) ② 옥상 방수 바닥에 그을음 자국(위에서
    내려다보는 카메라에 가장 크게 보이는 자리, PM 지시). 좌표는 make_university와 같은 기준
    (시계: u=0·y0=−8·z 32~35.6, 옥상: f3=55)."""
    # ① 시계 금 — 패널(offset 0.08)·프레임(depth 0.2)보다 확실히 앞(0.28)에 둔다.
    b.panel("-y", -8.0, 0.0, 32.0, 3.4, 3.4, "건물_금_잎카드", offset=0.28)
    # ② 옥상 그을음 — 두 군데, 넓은 자국이라 위에서 잘 보인다.
    soot = "건물_그을음_잎카드"
    b.face(((-6.0, -4.0, 55.05), (2.0, -5.0, 55.05), (3.0, 3.0, 55.05), (-5.0, 4.0, 55.05)), soot)
    b.face(((6.0, -6.0, 55.05), (10.0, -5.0, 55.05), (9.5, 1.0, 55.05), (5.5, 0.0, 55.05)), soot)


def _angry_japan_extra(b):
    """日本다운 파손 — ① 기와지붕에 구멍(기와 몇 장 빠진 자리, 어두운 판) ② 그 둘레 기와
    몇 장이 흘러내린 듯 살짝 어긋난 hexa 조각. 앞쪽(−y) 사면(처마→용마루)에 낸다 — 게임
    카메라가 내려다볼 때 가장 크게 보이는 자리(PM 지시). 좌표는 make_japan과 같은 기준
    (YSHIFT=3.8, wall_top=27, roof_rise=13 → ridge_z=40, ry0=−9.2·cy=3.8)."""
    wall_top, ridge_z, ry0, cy = 27.0, 40.0, -9.2, 3.8
    slope = Vector((0.0, cy - ry0, ridge_z - wall_top)).normalized()   # 처마→용마루 방향(경사를 타고 오른다)
    right = Vector((1.0, 0.0, 0.0))
    normal = right.cross(slope).normalized()                          # 사면 바깥쪽(앞·위)
    if normal.z < 0:
        normal = -normal

    def slope_point(t, x):
        return Vector((x, ry0, wall_top)) + slope * (t * Vector((0.0, cy - ry0, ridge_z - wall_top)).length)

    c = slope_point(0.38, 4.0) + normal * 0.04
    hw, hh = 1.3, 1.0
    b.face((c - right * hw - slope * hh, c + right * hw - slope * hh,
            c + right * hw + slope * hh, c - right * hw + slope * hh), "건물_색_검정")
    for dx, dt, ang in ((3.0, 0.30, 6.0), (-2.6, 0.46, -8.0), (1.0, 0.55, 5.0)):
        p = slope_point(dt, 4.0 + dx) + normal * 0.05
        w, h = 0.9, 0.7
        tilt = math.radians(ang)
        shift = right * math.sin(tilt) * h * 0.3 + normal * math.cos(tilt) * 0.25
        b.hexa((p - right * w - slope * h, p + right * w - slope * h,
                p + right * w + slope * h + shift, p - right * w + slope * h + shift,
                p - right * w - slope * h + normal * 0.06, p + right * w - slope * h + normal * 0.06,
                p + right * w + slope * h + shift + normal * 0.06, p - right * w + slope * h + shift + normal * 0.06),
               "건물_지붕_기와")


CATALOG += [
    ("Story08_사이버넷_화남", "08 Cybernet Angry",
     lambda: bc.angry_variant(make_cybernet, seed=1108, cracks=12,
                              # 3층 정면 가운데 두 창(u=−6·6)을 눈으로, 안쪽으로 처지는 눈썹 한 쌍
                              brows=[("-y", -11.0, (-9.0, 52.5), (-3.5, 50.0)),
                                     ("-y", -11.0, (9.0, 52.5), (3.5, 50.0))],
                              # blade_sign("PC")만 기울인다 — 실측으로 격리 확인된 상자(232 정점,
                              # u −13.42..−12.18만 걸림, 2층 창과 안 겹친다)
                              tilts=[("-y", -11.0, -14.2, -11.4, 24.5, 34.0, -14.0)],
                              # 1층 옆벽(+x)은 창이 없는 통짜 콘크리트라 금이 안전하게 들어간다
                              cracks_at=[("+x", 16.0, 0.0, 3.0, 4.0, 8.0)],
                              extra=_angry_cybernet_extra, smoke=0),
     "사이버넷 화난 버전 · 3층 가운데 두 창 눈 + 그을린 차양 눈썹 · PC 간판 기울어짐 · 네온 반쪽 꺼짐 · "
     "붉은 창 · 금·그을음 · 옥상 연기 자리"),
    ("Story09_7탄약창_화남", "09 Ammo Depot Angry",
     lambda: bc.angry_variant(make_ammo_depot, seed=1109, cracks=10,
                              # 정문 "7탄약창" 간판판만 기울인다(실측 격리 확인: u −4.92..4.92,
                              # z 10.00..13.83, 343 정점 — 문주(u=±8)와 안 겹치는 좁은 u 상자를 썼다)
                              tilts=[("-y", -18.3, -6.0, 6.0, 9.5, 14.5, 10.0)],
                              extra=_angry_ammo_depot_extra, smoke=0),
     "7탄약창 화난 버전 · 감시탑 초소 새 창 두 개 눈 + 그을린 차양 눈썹 · 정문 간판 기울어짐 · "
     "뜯긴 철망 · 빨간 경고등 · 붉은 창 · 금·그을음 · 환기구 옆 연기 자리"),
    ("Story10_동양미래대학교_화남", "10 University Angry",
     lambda: bc.angry_variant(make_university, seed=1110, cracks=14,
                              # 포치 기둥 바로 위 2층 창 두 개(u=−9·9)를 눈으로, 안쪽(현관 쪽)으로
                              # 처지는 눈썹 한 쌍(창 위 34.7부터, 클수록 자연스러워 36.5 기준)
                              brows=[("-y", -8.0, (-11.0, 36.5), (-6.0, 35.0)),
                                     ("-y", -8.0, (11.0, 36.5), (6.0, 35.0))],
                              # 스테인리스 간판판만 기울인다(실측 격리 확인: z 29.45 밑으로는
                              # 시계 프레임과 안 겹친다 — 판 맨 위 테두리 0.8만 제외, 글자는 다 걸림)
                              tilts=[("-y", -8.0, -9.7, 9.7, 25.9, 29.45, 9.0)],
                              extra=_angry_university_extra, smoke=0),
     "동양미래대학교 화난 버전 · 포치 위 2층 두 창 눈 + 그을린 차양 눈썹 · 간판 기울어짐 · "
     "시계 금 · 옥상 그을음 · 붉은 창 · 금·그을음"),
    ("Story11_日本_화남", "11 Japan Angry",
     lambda: bc.angry_variant(make_japan, seed=1111, cracks=8,
                              # 정면 쇼지창 두 칸(u=−8·8, 벽면 y0=−6.2)을 눈으로, 안쪽(문 쪽)으로
                              # 처지는 눈썹(쇼지 z 5~13 위, 13.5부터)
                              brows=[("-y", -6.2, (-10.5, 14.5), (-6.0, 12.8)),
                                     ("-y", -6.2, (10.5, 14.5), (6.0, 12.8))],
                              # 「日本」 편액만 기울인다(도리이 y=ty=ry0−7.0=−9.2−7.0=−16.2. 실측
                              # 격리 확인: u −8~8·z 23.3~26.3 상자가 도리이 기둥·누키·가사기와
                              # 안 겹치고 편액 146정점만 걸린다)
                              tilts=[("-y", -16.2, -8.0, 8.0, 23.3, 26.3, -8.0)],
                              extra=_angry_japan_extra, smoke=0),
     "日本 화난 버전 · 정면 쇼지 두 칸 눈 + 그을린 차양 눈썹 · 편액 기울어짐 · 기와지붕 구멍·"
     "어긋난 기와 · 붉은 창 · 금·그을음"),
]


# ──────────────────────────────────────────────────────────── 화난 버전(12·13, 마지막 쌍)

def _angry_codeit_extra(b):
    """코드잇다운 파손 — ① 간판 바로 위 1층 커튼월 유리 자리에 건물_창_분노 판 두 장을 덧대
    "눈"으로 삼는다(angrify()가 커튼월 전체를 이미 건물_커튼월_분노로 바꾸므로, FIT 단일 창
    재질을 따로 덧대야 타일 배경과 구분되는 눈이 된다) ② 3층 옆벽(+x) 빈 자리에 깨진 패널
    (검정) ③ 옥상 모서리 멀리언에 휜 것처럼 보이는 대각 각재 하나 ④ 옥상 한쪽에 쓰러진
    설비함(hexa로 기울여 지음) ⑤ 옥탑 환기구 옆 연기 자리. 좌표는 make_codeit과 같은 기준
    (W=36·D=26·G=25·floor_bottoms=[25,40,55,70]·top=85)."""
    angry_glass, black, stainless, metal = "건물_창_분노", "건물_색_검정", "건물_금속_스테인리스", "건물_금속_회색"
    # ① 눈 — 간판(u 0, z 24.2 근방) 바로 위 1층 커튼월 유리존(z 26.5~40)에 좌우 대칭으로.
    b.panel("-y", -13.0, -9.0, 29.0, 6.0, 7.0, angry_glass, offset=0.12)
    b.panel("-y", -13.0, 9.0, 29.0, 6.0, 7.0, angry_glass, offset=0.12)
    # ② 깨진 커튼월 패널 — 3층(zb=55) 옆벽(+x), 코너 멀리언(y=±13)과 안 겹치는 가운데(u=0)
    b.panel("+x", 18.0, 0.0, 60.0, 8.0, 8.0, black, offset=0.12)
    # ③ 휜 멀리언 — (x1,y1) 코너 각재 위에 겹쳐 대각으로 삐져나온 짧은 각재
    b.beam((18.0, 13.0, 50.0), (16.8, 12.6, 58.0), 1.0, 1.0, stainless)
    # ④ 쓰러진 옥상 설비함 — 기존 실외기(6.0,±5.0/0.0) 옆 새 자리에, 밑변은 그대로 눕고 윗변만
    # 옆으로 밀려·낮아져 쓰러진 모양(hexa, box_frame과 같은 점 순서: 밑 0~3·위 4~7).
    b.hexa(((8.9, 4.35, 85.0), (11.1, 4.35, 85.0), (11.1, 5.65, 85.0), (8.9, 5.65, 85.0),
            (10.7, 4.35, 85.9), (12.9, 4.35, 85.9), (12.9, 5.65, 85.9), (10.7, 5.65, 85.9)), metal)
    # ⑤ 연기 자리 — 옥탑 환기구(0.0, 6.0) 옆
    b.markers.append(("연기_자리_01", Vector((0.0, 6.0, 86.5))))


def _angry_story13_extra(b):
    """쉬었음다운 파손 — ① 옆벽(+x) 빈 자리에 금 ② 기존 가스배관 옆에 꺾여 갈라진 배관 한
    토막 ③ 쓰러진 택배 상자(기존 것 옆에 새로 눕혀서) ④ 옥상 방수 바닥에 그을음(내려다보는
    카메라에서 가장 크게 보이는 자리) ⑤ +x 실외기 쪽 옥상 위에 연기 자리. 불 켜진 방
    (건물_원룸창)은 angry_variant(keep=("건물_원룸창",))로 이미 안 건드린다. 좌표는
    make_story13과 같은 기준(W=20·D=16·f3=55, 실외기 x1+0.9=10.9·y=4.5, 배관 x1+0.2=10.2·y=−6.5)."""
    crack, metal, yellow, soot = "건물_금_잎카드", "건물_금속_회색", "건물_색_노랑", "건물_그을음_잎카드"
    # ① 금 — +x 옆벽, 창(u=0, z 44~48.5)과 실외기·배관을 피한 빈 자리(u=2.0)
    b.panel("+x", 10.0, 2.0, 20.0, 2.5, 4.0, crack, offset=0.14)
    # ② 꺾인 배관 — 기존 직선 배관(10.2,−6.5) 중간에서 옆으로 갈라져 나온 토막
    b.beam((10.2, -6.5, 30.0), (11.0, -6.0, 38.0), 0.3, 0.3, metal)
    # ③ 쓰러진 택배 상자 — 기존 것(3.2, y0−1.4=−9.4) 옆에 눕혀서(hexa, 세운 상자보다 낮고 김)
    b.hexa(((4.3, -9.35, 0.3), (6.3, -9.35, 0.3), (6.3, -8.65, 0.3), (4.3, -8.65, 0.3),
            (4.3, -9.35, 0.95), (6.3, -9.35, 0.95), (6.3, -8.65, 0.95), (4.3, -8.65, 0.95)), yellow)
    # ④ 옥상 그을음 — 방수 바닥(f3=55) 위, 파라펫 안쪽
    b.face(((-6.0, -5.0, 55.05), (-2.0, -6.0, 55.05), (-1.0, -1.0, 55.05), (-5.0, 0.0, 55.05)), soot)
    # ⑤ 연기 자리 — +x 실외기 쪽 옥상 위(실외기는 옆벽에 있지만 옥상에서 가장 가까운 지점)
    b.markers.append(("연기_자리_01", Vector((7.0, 4.5, 56.0))))


CATALOG += [
    ("Story12_코드잇_화남", "12 Codeit Angry",
     lambda: bc.angry_variant(make_codeit, seed=1112, cracks=10,
                              # 간판 바로 위 1층 커튼월 유리존(z 26.5~40)의 좌우 대칭 두 자리를
                              # 눈으로(u=−9·9), 다음 층 스판드럴(z=40) 밑에서 가운데로 처지는 눈썹
                              brows=[("-y", -13.0, (-12.0, 39.0), (-6.0, 37.0)),
                                     ("-y", -13.0, (12.0, 39.0), (6.0, 37.0))],
                              # 「코드잇」 간판판만 기울인다(실측 격리 확인: u −4.90..4.90 ·
                              # z 24.20..28.67 · 208정점 — 캐노피(z≤23.2)·코너 멀리언과 안 겹친다)
                              tilts=[("-y", -13.0, -5.2, 5.2, 23.9, 29.0, 10.0)],
                              # 🔴 WALL_MATS에 스테인리스·유리·커튼월이 없어(전부 큰 벽이 이
                              # 재질들이라) 자동 금이 붙을 데가 없다 — 로비 창 사이 빈 자리에
                              # 강제로 하나 낸다.
                              cracks_at=[("+x", 18.0, 0.0, 6.0, 3.0, 6.0)],
                              extra=_angry_codeit_extra, smoke=0),
     "코드잇 화난 버전 · 간판 위 커튼월 두 칸 눈 + 그을린 차양 눈썹 · 간판 기울어짐 · "
     "깨진 커튼월 패널·휜 멀리언 · 쓰러진 옥상 설비함 · 붉은 창·커튼월 · 강제 금 · 옥탑 연기 자리"),
    ("Story13_쉬었음_화남", "13 Rested Angry",
     lambda: bc.angry_variant(make_story13, seed=1113, cracks=10,
                              # 불 켜진 방(2층, 건물_원룸창)은 건드리지 않는다 — 3층 정면 어두운
                              # 창 두 개(u=−5.5·5.5, z 44~49.5)를 눈으로, 그 위로 가운데를 향해
                              # 처지는 눈썹 한 쌍
                              brows=[("-y", -8.0, (-8.0, 52.5), (-3.0, 50.0)),
                                     ("-y", -8.0, (8.0, 52.5), (3.0, 50.0))],
                              # 「쉬었음」 명패만 기울인다(실측 격리 확인: u 4.35..10.38 ·
                              # z 2.00..4.67 · 338정점 — 현관 문·택배상자·바닥판과 안 겹친다)
                              tilts=[("-y", -8.0, 4.0, 10.8, 1.8, 5.0, 10.0)],
                              # 🔴 13은 화난 건물 속에서 그 방만 조용한 대비가 이야기다(PM·blender
                              # 승인) — 불 켜진 방(건물_원룸창)은 붉게 안 바꾼다.
                              keep=("건물_원룸창",),
                              extra=_angry_story13_extra, smoke=0),
     "쉬었음 화난 버전 · 3층 어두운 두 창 눈(불 켜진 방은 그대로) + 그을린 눈썹 · 명패 기울어짐 · "
     "벽 금·꺾인 배관·쓰러진 택배상자 · 옥상 그을음 · 붉은 창(원룸창 제외) · 연기 자리"),
]


if __name__ == "__main__":
    bc.run(CATALOG)
