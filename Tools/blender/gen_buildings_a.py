"""스토리 건물 Story01~07 — 공용 도구는 buildings_common.py, 텍스처는 buildings_textures.py.

화면 없이(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_buildings_a.py [-- Story01 Story02]
사장님 창: exec 후 buildings_common.story_rows(collection)(show_all의 판_스토리) — 창 모양 = FBX 모양.

치수는 게임 단위(사람 키 20). 한 층 FLOOR_H=15, 출입문 DOOR_H=22가 있는 1층은 GROUND_H=25.
x = 가로, y = 앞뒤(정면 −y), z = 위. 원점 = 바닥 가운데.
Story01·02는 화풍 견본(blender 세션) — 03~07이 같은 결로 이어간다. 사실감은 텍스처 + 작은 기하(창틀 돌출·
홈통·실외기·가스관)에서 나온다 — 벽에 판만 붙이면 스티커처럼 납작하다(1차 렌더 교훈).
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import buildings_common as bc
from buildings_common import DOOR_H, FLOOR_H, GROUND_H, Builder


# ──────────────────────────────────────────────────────────── 01 하이츠

def make_heights():
    """붉은 벽돌 다세대 빌라 — 1층 필로티 주차장, 2~4층 벽돌, 앞에 베란다 둘, 가운데 계단실이 옥상 위로 솟고
    그 앞면에 「하이츠」. 옥상은 초록 방수 + 벽돌 난간벽 + 스테인리스 물탱크. 옆벽에 실외기·노란 가스관, 모서리 홈통."""
    b = Builder()
    brick, concrete, white = "건물_벽돌_붉은", "건물_콘크리트", "건물_외벽_흰"
    glass, metal, trim = "건물_유리창", "건물_금속_회색", "건물_색_흰"
    W, D = 34.0, 26.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H
    floors = [G + FLOOR_H * k for k in range(3)]
    top = G + FLOOR_H * 3                              # 70

    # 바닥판(주차장 보도블록)
    b.box(x0 - 0.5, x1 + 0.5, y0 - 3.0, y1 + 0.5, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))

    # 1층 필로티 — 뒤쪽 절반만 막힌 벽(현관·계단 밑), 앞은 기둥
    b.box(x0 + 0.5, x1 - 0.5, 1.0, y1 - 0.5, 0.3, G, white, skip=("bottom", "top"))
    for x in (x0 + 1.2, -6.0, 6.0, x1 - 1.2):
        b.box_c(x, y0 + 1.2, 1.6, 1.6, 0.3, G - 0.3, concrete, skip=("bottom", "top"))
    b.windows("+y", y1 - 0.5, (-10.0, 10.0), (9.0,), 4.0, 6.0, frame_mat=metal)
    # 위층을 받치는 슬래브
    b.box(x0 - 0.4, x1 + 0.4, y0 - 0.4, y1 + 0.4, G - 0.8, G + 0.2, concrete)

    # 2~4층 벽돌 몸통
    b.box(x0, x1, y0, y1, G + 0.2, top, brick, skip=("bottom", "top"))
    for z in floors:
        # 층 사이 흰 띠(한국 빌라 특유의 층간 몰딩)
        b.box(x0 - 0.25, x1 + 0.25, y0 - 0.25, y1 + 0.25, z + FLOOR_H - 0.6, z + FLOOR_H + 0.2, white, skip=("bottom", "top"))
        for side_x in (-10.0, 10.0):
            # 베란다 — 앞으로 2.5 나온 상자: 아래 벽돌 난간벽, 위는 확장형 통창(가운데·양끝 창틀) + 강철 난간
            bx0, bx1 = side_x - 6.0, side_x + 6.0
            b.box(bx0, bx1, y0 - 2.5, y0, z + 1.0, z + 5.2, brick, skip=("+y",))
            b.box(bx0, bx1, y0 - 2.5, y0, z + 12.6, z + 13.4, white, skip=("+y",))
            b.panel("-y", y0 - 2.5, side_x, z + 5.2, 11.6, 7.4, glass, offset=0.02)
            for mx in (bx0 + 0.25, side_x, bx1 - 0.25):
                b.box_c(mx, y0 - 2.6, 0.45, 0.4, z + 5.2, 7.4, trim, skip=("bottom", "top"))
            b.panel("-x", bx0, y0 - 1.25, z + 5.2, 2.3, 7.4, glass, offset=0.02)
            b.panel("+x", bx1, y0 - 1.25, z + 5.2, 2.3, 7.4, glass, offset=0.02)
            b.railing([(bx0 + 0.3, y0 - 3.0, z + 5.2), (bx1 - 0.3, y0 - 3.0, z + 5.2)], 2.6, metal, post_gap=2.0, post=0.2, rail=0.25)
        # 옆·뒤 창(흰 창틀 + 콘크리트 창턱)
        b.windows("-x", x0, (-6.0, 6.0), (z + 5.0,), 4.0, 5.5, sill_mat=concrete, frame_mat=trim)
        b.windows("+x", x1, (-6.0, 6.0), (z + 5.0,), 4.0, 5.5, sill_mat=concrete, frame_mat=trim)
        b.windows("+y", y1, (-11.0, -3.5, 3.5, 11.0), (z + 5.5,), 3.6, 4.5, sill_mat=concrete, frame_mat=trim)
        # 옆벽 실외기(창 옆에 매단 흰 상자 + 받침) — 층마다 한쪽씩 번갈아
        ax = x0 - 1.1 if z != floors[1] else x1 + 1.1
        b.box_c(ax, 0.0, 2.2, 3.0, z + 2.0, 2.2, trim)
        b.box_c(ax, 0.0, 2.4, 3.2, z + 1.7, 0.3, metal)
    # 노란 가스관 — 왼쪽 옆벽을 층마다 가로로, 뒤 모서리에서 세로로 올라간다(한국 빌라의 그 노란 관)
    gx = x0 - 0.35
    b.beam((gx, y1 - 1.5, 0.3), (gx, y1 - 1.5, floors[-1] + 2.0), 0.35, 0.35, "건물_색_노랑")
    for z in floors:
        b.beam((gx, y1 - 1.5, z + 2.0), (gx, -9.0, z + 2.0), 0.3, 0.3, "건물_색_노랑")
    # 모서리 홈통(옥상 난간에서 땅까지)
    for hx, hy in ((x0 + 0.6, y0 - 0.45), (x1 - 0.6, y0 - 0.45), (x0 + 0.6, y1 + 0.45), (x1 - 0.6, y1 + 0.45)):
        b.box_c(hx, hy, 0.5, 0.5, 0.3, top + 2.6, metal, skip=("bottom",))

    # 계단실 — 가운데 앞으로 조금 나오고 옥상 위로 한 층 솟는다. 층참마다 좁은 창
    sx0, sx1 = -3.6, 3.6
    b.box(sx0, sx1, y0 - 1.0, y0 + 6.0, G + 0.2, top + 11.0, white, skip=("bottom",))
    b.box(sx0 - 0.4, sx1 + 0.4, y0 - 1.4, y0 + 6.4, top + 11.0, top + 11.8, concrete)
    b.windows("-y", y0 - 1.0, (0.0,), [z + 8.0 for z in floors], 2.4, 4.0, frame_mat=metal)
    # 1층 현관 — 계단실 밑 유리문(사람 키보다 큰 DOOR_H) + 문틀 + 차양
    b.box(sx0, sx1, y0 + 1.0, y0 + 6.0, 0.3, G - 0.8, white, skip=("bottom", "top"))
    b.panel("-y", y0 + 1.0, 0.0, 0.3, 5.6, DOOR_H, glass)
    b.frame("-y", y0 + 1.0, 0.0, 0.3, 5.6, DOOR_H, metal, bottom=False)
    b.box(sx0 - 1.0, sx1 + 1.0, y0 - 1.5, y0 + 1.0, DOOR_H + 0.6, DOOR_H + 1.2, concrete)
    # 「하이츠」 — 계단실 옥상 위 부분 앞면
    b.sign("하이츠", "-y", y0 - 1.0, 0.0, top + 2.6, "건물_색_흰", "건물_색_남색", size=3.4, pad=0.7, max_width=6.0)

    # 옥상 — 초록 방수 바닥, 벽돌 난간벽(윗면 콘크리트 갓), 물탱크, 실외기
    b.face(((x0, y0, top), (x1, y0, top), (x1, y1, top), (x0, y1, top)), "건물_옥상_방수")
    t = 0.7
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1), (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, top, top + 2.8, brick, skip=("bottom", "top"))
        b.box(bx0 - 0.15, bx1 + 0.15, by0 - 0.15, by1 + 0.15, top + 2.8, top + 3.2, concrete, skip=("bottom",))
    tank_x, tank_y = 10.0, 6.0
    for dx in (-2.2, 2.2):
        for dy in (-2.2, 2.2):
            b.box_c(tank_x + dx, tank_y + dy, 0.4, 0.4, top, 2.5, metal, skip=("bottom", "top"))
    b.box_c(tank_x, tank_y, 5.6, 5.6, top + 2.5, 0.4, metal)
    b.cylinder(tank_x, tank_y, top + 2.9, top + 9.5, 2.8, 14, "건물_금속_스테인리스", top_radius=2.6)
    for ux in (-12.0, -7.5):
        b.box_c(ux, 8.0, 3.2, 1.6, top, 2.4, trim, skip=("bottom",))
    return b


# ──────────────────────────────────────────────────────────── 02 큰소망유치원

def make_kindergarten():
    """알록달록 유치원 — 박공이 정면을 보는 작은 집 셋이 나란히(벽·지붕 색이 다 다름), 2층. 앞마당은
    마사토 놀이터 + 미끄럼틀 + 시소 + 낮은 울타리, 가운데 집 2층 창 위에 「큰소망유치원」."""
    b = Builder()
    ground, white, glass, trim, metal = "건물_바닥_운동장", "건물_외벽_흰", "건물_유리창", "건물_색_흰", "건물_금속_회색"
    G = GROUND_H - 1.0                                 # 1층(현관 문 DOOR_H) — 기단 1.2 위라 1 낮춰도 문이 들어간다
    body_top = G + FLOOR_H                             # 39
    y0, y1 = -2.0, 20.0                                # 집 앞뒤(마당은 그 앞)
    houses = (  # (x0, x1, 벽, 지붕, 박공 높이)
        (-21.0, -7.4, "건물_외벽_노랑", "건물_지붕_슁글_빨강", 11.0),
        (-7.0, 7.0, "건물_외벽_분홍", "건물_지붕_슁글_파랑", 13.0),
        (7.4, 21.0, "건물_외벽_하늘", "건물_지붕_슁글_초록", 11.0),
    )

    b.box(-22.0, 22.0, -22.0, 22.0, 0.0, 0.3, ground, skip=("bottom",))
    # 집 밑 기단(콘크리트 한 단)
    b.box(-21.6, 21.6, y0 - 0.6, y1 + 0.6, 0.3, 1.2, "건물_콘크리트", skip=("bottom",))
    for hx0, hx1, wall, roof, rise in houses:
        cx = (hx0 + hx1) / 2
        b.box(hx0, hx1, y0, y1, 1.2, body_top, wall, skip=("bottom", "top"))
        b.gable_roof(hx0, hx1, y0, y1, body_top, rise, roof, gable_mat=wall, overhang=1.2, thickness=0.9,
                     under_mat="건물_색_흰", ridge="y")
        # 층 띠(흰)
        b.box(hx0 - 0.2, hx1 + 0.2, y0 - 0.2, y1 + 0.2, G + 0.4, G + 1.2, white, skip=("bottom", "top"))
        # 정면 창 — 1층은 키 큰 통창 둘(가운데 집은 현관이 대신한다), 2층 둘, 박공 속 작은 창 하나. 전부 흰 창틀
        us = (cx - 3.4, cx + 3.4)
        if cx != 0.0:
            b.windows("-y", y0, us, (6.0,), 4.6, 11.0, sill_mat=trim, frame_mat=trim)
        b.windows("-y", y0, us, (G + 4.0,), 4.6, 6.2, sill_mat=trim, frame_mat=trim)
        b.windows("-y", y0, (cx,), (body_top + 2.5,), 3.0, 3.0, frame_mat=trim)
        b.windows("+y", y1, us, (6.0, G + 4.0), 4.0, 6.0, frame_mat=trim)
    for side, plane in (("-x", -21.0), ("+x", 21.0)):
        b.windows(side, plane, (4.0, 14.0), (6.0,), 4.6, 11.0, sill_mat=trim, frame_mat=trim)
        b.windows(side, plane, (4.0, 14.0), (G + 4.0,), 4.6, 6.2, sill_mat=trim, frame_mat=trim)
    # 집 사이 홈통
    for hx in (-7.2, 7.2):
        b.box_c(hx, y0 - 0.5, 0.5, 0.5, 1.2, body_top - 1.0, metal, skip=("bottom",))

    # 현관 — 가운데 집 1층 유리문(DOOR_H) + 문틀 + 노란 차양 + 흰 기둥 둘
    b.panel("-y", y0, 0.0, 1.2, 7.0, DOOR_H, glass, offset=0.12)
    b.frame("-y", y0, 0.0, 1.2, 7.0, DOOR_H, trim, width=0.45, bottom=False)
    b.box(-4.8, 4.8, y0 - 3.2, y0, DOOR_H + 1.3, DOOR_H + 2.0, "건물_색_노랑")
    for x in (-4.3, 4.3):
        b.box_c(x, y0 - 2.8, 0.55, 0.55, 1.2, DOOR_H + 0.1, trim, skip=("bottom", "top"))
    # 간판 — 가운데 집 2층 창 위, 박공 밑
    b.sign("큰소망유치원", "-y", y0, 0.0, G + 10.9, "건물_색_노랑", "건물_색_남색", size=2.6, pad=0.6,
           max_width=12.0)

    # 놀이터 — 미끄럼틀(기둥 넷 위 발판 + 작은 지붕 + 사다리 + 비탈), 시소, 앞 울타리
    px, py, deck = -11.0, -14.0, 7.0
    for dx in (-1.6, 1.6):
        for dy in (-1.6, 1.6):
            b.box_c(px + dx, py + dy, 0.45, 0.45, 0.3, deck + 3.2, "건물_색_빨강", skip=("bottom",))
    b.box_c(px, py, 3.8, 3.8, deck, 0.5, "건물_색_노랑")
    b.gable_roof(px - 1.9, px + 1.9, py - 1.9, py + 1.9, deck + 3.2, 2.0, "건물_지붕_슁글_파랑", overhang=0.4, thickness=0.4)
    for dx in (-1.1, 1.1):                                 # 사다리(뒤쪽)
        b.beam((px + dx, py + 1.9, deck + 0.5), (px + dx, py + 5.5, 0.3), 0.3, 0.3, metal)
    for k in range(1, 5):
        t = k / 5
        z = deck + 0.5 - (deck + 0.2) * t
        b.beam((px - 1.1, py + 1.9 + 3.6 * t, z), (px + 1.1, py + 1.9 + 3.6 * t, z), 0.25, 0.25, metal)
    slide_end = (px + 9.0, py, 0.9)                        # 비탈(오른쪽으로 내려간다)
    b.beam((px + 1.9, py, deck + 0.2), slide_end, 2.4, 0.4, "건물_색_초록")
    for dy in (-1.3, 1.3):
        b.beam((px + 1.9, py + dy, deck + 1.0), (slide_end[0], py + dy, 1.7), 0.3, 0.8, "건물_색_노랑")
    b.box_c(10.0, -14.0, 0.8, 0.8, 0.3, 1.8, metal, skip=("bottom",))
    b.beam((5.0, -14.0, 0.9), (15.0, -14.0, 3.3), 0.9, 0.35, "건물_색_빨강")
    b.railing([(-21.5, -8.0, 0.3), (-21.5, -21.5, 0.3), (21.5, -21.5, 0.3), (21.5, -8.0, 0.3)], 3.2, "건물_색_초록",
              post_gap=3.0, post=0.4, rail=0.4)
    return b


# ──────────────────────────────────────────────────────────── 목록
#
# (파일 이름, 이름표(영어 — 창 글꼴에서 한글이 깨진다), 만들기, 설명). 파일 이름은 유니티가 참조한다.

CATALOG = [
    ("Story01_하이츠", "01 Heights", make_heights, "붉은 벽돌 빌라 · 1층 필로티 · 베란다 둘 · 계단실 · 옥상 물탱크 · 실외기·가스관·홈통 · 간판 하이츠"),
    ("Story02_큰소망유치원", "02 Kindergarten", make_kindergarten, "박공 집 셋(노랑·분홍·하늘 벽 / 빨강·파랑·초록 지붕) · 흰 창틀 · 놀이터 미끄럼틀·시소 · 간판 큰소망유치원"),
]


if __name__ == "__main__":
    bc.run(CATALOG)
