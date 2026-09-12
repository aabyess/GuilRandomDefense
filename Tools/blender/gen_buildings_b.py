"""스토리 건물 Story08~13 — 공용 도구는 buildings_common.py, 텍스처는 buildings_textures.py.

화면 없이(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_buildings_b.py [-- Story08 Story09]
사장님 창: exec 후 buildings_common.story_rows(collection)(show_all의 판_스토리) — 창 모양 = FBX 모양.

치수는 게임 단위(사람 키 20). 한 층 FLOOR_H=15, 출입문 DOOR_H=22가 있는 1층은 GROUND_H=25.
x = 가로, y = 앞뒤(정면 −y), z = 위. 원점 = 바닥 가운데.
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import buildings_common as bc
from buildings_common import DOOR_H, FLOOR_H, GROUND_H, Builder


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
            b.box(u - bay_w / 2 - 0.3, u + bay_w / 2 + 0.3, y0 - 1.6, y0 + 0.15, DOOR_H + 0.3, DOOR_H + 1.0,
                  awning, skip=("top",))
    # 1층 상단 타일 몰딩 띠(전체 둘레)
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, G - 2.0, G, tile, skip=("bottom", "top"))

    # 2층 PC방 — 벽 + 어두운 통유리 띠(frame_mat 멀리언으로 나뉜 연속된 창) + 넓은 가로 간판 + 옆·뒤 창
    b.box(x0, x1, y0, y1, G, f2, concrete, skip=("bottom", "top", "-y"))
    b.box(x0, x1, y0, y1, G, f2, concrete, skip=("bottom", "top", "+y", "-x", "+x"))
    b.windows("-y", y0, (-12.0, -6.0, 0.0, 6.0, 12.0), (27.0,), 5.6, 10.0, mat=dark_glass, frame_mat=metal, frame=0.3,
              sill_mat=white)
    # 정문 유리문 위 작은 차양 — 셔터/가게 차양과 같은 요령, 입구를 도드라지게 한다
    b.box(-2.8, 2.8, y0 - 1.3, y0 + 0.15, DOOR_H + 0.3, DOOR_H + 0.9, awning, skip=("top",))
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
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, f3, top, concrete, skip=("bottom", "top"))
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
    b.panel("-y", gy - gd / 2, gx - 2.0, 0.3, 2.2, DOOR_H, glass)
    b.frame("-y", gy - gd / 2, gx - 2.0, 0.3, 2.2, DOOR_H, metal, width=0.3, depth=0.25)
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
    b.sign("7탄약창", "-y", y0 - 0.3, 0.0, 10.0, white, black, size=3.4, pad=0.7, max_width=14.0)
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
    b.sign("관계자외\n출입금지", "+y", y1, 10.0, 0.3, white, red, size=3.4, pad=0.7, max_width=14.0)
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
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, f3 - 0.4, f3 + 0.4, white, skip=("bottom", "top"))
    t = 0.5
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, f3 + 0.4, top, concrete, skip=("bottom", "top"))

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
    (AppleSDGothicNeo에 한자 있음, blender 확인)."""
    b = Builder()
    wood, plaster, tile_roof = "건물_나무_판", "건물_외벽_흰", "건물_지붕_기와"
    black, vermilion, stone, glass = "건물_색_검정", "건물_도장_주홍", "건물_콘크리트", "건물_유리창"
    W, D = 30.0, 20.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2           # -15..15, -10..10
    plinth_h = 2.0
    wall_top = plinth_h + GROUND_H                          # 27
    roof_rise, overhang = 13.0, 3.0

    # 돌 기단
    b.box(x0 - 0.5, x1 + 0.5, y0 - 0.5, y1 + 0.5, 0.0, plinth_h, stone, skip=("bottom",))

    # 벽 — 하부 나무판 + 상부 흰 회벽(처마 밑 7만) + 경계 검정 띠(목조 트림)
    plaster_z = plinth_h + 18.0
    b.box(x0, x1, y0, y1, plinth_h, plaster_z, wood, skip=("bottom", "top"))
    b.box(x0, x1, y0, y1, plaster_z, wall_top, plaster, skip=("bottom", "top"))
    b.box(x0 - 0.1, x1 + 0.1, y0 - 0.1, y1 + 0.1, plaster_z - 0.3, plaster_z + 0.3, black, skip=("bottom", "top"))

    # 문 — 정면 가운데, 검정 프레임(목조 트림). 창 — 정면 좌우 + 옆·뒤, 전부 검정 프레임.
    b.panel("-y", y0, 0.0, plinth_h, 4.4, DOOR_H, glass, offset=0.06)
    b.frame("-y", y0, 0.0, plinth_h, 4.4, DOOR_H, black, width=0.35, depth=0.3)
    b.windows("-y", y0, (-8.0, 8.0), (plinth_h + 3.0,), 2.6, 8.0, mat=glass, frame_mat=black)
    b.windows("+y", y1, (-8.0, 0.0, 8.0), (plinth_h + 3.0,), 2.6, 8.0, mat=glass, frame_mat=black)
    b.windows("-x", x0, (-4.0, 4.0), (plinth_h + 3.0,), 2.4, 7.0, mat=glass, frame_mat=black)
    b.windows("+x", x1, (-4.0, 4.0), (plinth_h + 3.0,), 2.4, 7.0, mat=glass, frame_mat=black)

    # 모임지붕 — 처마가 밖으로 overhang만큼 나온 사각형 기준(부르는 쪽이 넓혀서 준다)
    rx0, rx1, ry0, ry1 = x0 - overhang, x1 + overhang, y0 - overhang, y1 + overhang
    b.hip_roof(rx0, rx1, ry0, ry1, wall_top, roof_rise, tile_roof)
    ridge_z = wall_top + roof_rise                          # 40
    for rxe in (-5.5, 5.5):
        b.box_c(rxe, 0.0, 0.8, 0.8, ridge_z, 0.8, black)    # 용마루 끝 장식(오니가와라 근사)

    # 도리이 — 부지 앞(−y, 처마 밖). 통로 높이 22 이상(누키 밑면 기준)·기둥 간격 16.
    ty = y0 - overhang - 4.0                                # 처마(−13)보다 2 더 앞 — 지붕 밑에 안 걸린다
    post_h, nuki_z, kasagi_z = 28.4, 22.4, 27.0             # nuki_z는 빔 중심 — 밑면 = 22.4-0.35=22.05
    for tx in (-8.0, 8.0):
        b.beam((tx, ty, 0.0), (tx, ty, post_h), 1.2, 1.2, vermilion)
    b.beam((-8.0, ty, nuki_z), (8.0, ty, nuki_z), 0.9, 0.7, vermilion)       # 누키(하단 가로보)
    b.beam((-9.5, ty, kasagi_z), (9.5, ty, kasagi_z), 1.5, 1.0, vermilion)   # 가사기(상단, 기둥보다 넓게)
    # 편액 「日本」 — 누키와 가사기 사이. 검정 글씨(목판에 먹으로 쓴 전통 방식과 같은 느낌).
    b.sign("日本", "-y", ty, 0.0, 23.4, wood, black, size=3.2, pad=0.7, max_width=8.0)
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
]


if __name__ == "__main__":
    bc.run(CATALOG)
