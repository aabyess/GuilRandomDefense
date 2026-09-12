"""스토리 건물 Story03~07 — 공용 도구는 buildings_common.py, 텍스처는 buildings_textures.py.
gen_buildings_a.py(01·02, blender 세션 화풍 견본)와 같은 결로 이어간다 — 파일만 나눈다(같은 파일을
둘이 고치면 겹친다). buildings_common.catalogs()가 gen_buildings_*.py를 전부 모아 판·SOURCE에 올린다.

화면 없이(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_buildings_c.py [-- Story03 Story04]
사장님 창: exec 후 buildings_common.story_rows(collection)(show_all의 판_스토리) — 창 모양 = FBX 모양.

치수는 게임 단위(사람 키 20). 한 층 FLOOR_H=15, 출입문 DOOR_H=22가 있는 1층은 GROUND_H=25.
x = 가로, y = 앞뒤(정면 −y), z = 위. 원점 = 바닥 가운데.
학교 셋(04·05·06)은 실루엣이 확 달라야 한다(멀리서 한눈에 구별) — 04 낮고 긴 3층 + 시계탑,
05 ㄱ자 4층 + 깃대 셋, 06 5층 판상형 + 옥상 난간. SOURCE.txt는 여기서 안 쓴다(bc.run()이
write_source()를 부르는데, 두 세션이 같은 파일을 동시에 덮어써 두 번째 실행 것만 남는다 —
blender 세션이 두 스크립트 다 돈 뒤 마지막에 한 번 커밋하기로 함)."""

import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import buildings_common as bc
from buildings_common import DOOR_H, FLOOR_H, GROUND_H, UP, Builder, side_frame


def round_window(b, side, plane, u, z, radius, glass_mat, frame_mat, sides=14, rim=1.18):
    """둥근 창 — 벽면(side, plane)의 가로 u·높이 z(중심)에 원판 유리 + 살짝 큰 테두리 고리.
    한양영어유치원 같은 동화책풍 건물의 둥근 창틀에 쓴다(사각 windows()로는 못 낸다)."""
    n, r, up = side_frame(side)
    center = b._on_side(side, plane, u) + UP * z
    angles = [math.tau * i / sides for i in range(sides)]
    glass = [b.v(center + n * 0.02 + (r * math.cos(t) + up * math.sin(t)) * radius) for t in angles]
    frame = [b.v(center + (r * math.cos(t) + up * math.sin(t)) * (radius * rim)) for t in angles]
    b.cap(glass, center + n * 0.05, glass_mat)
    b.bridge(frame, glass, frame_mat)


# ──────────────────────────────────────────────────────────── 03 한양영어유치원

def make_hanyang_kindergarten():
    """동화책풍 유치원 — 흰 미장 + 반목조 각재, 가파른 박공 둘이 높이를 엇갈려(주황·빨강 슁글),
    왼쪽에 뾰족 원뿔 지붕 원형 탑. 둥근 창틀·굴뚝. 간판 「한양영어유치원」 + 작은 "ENGLISH" 부제."""
    b = Builder()
    white, wood, red, orange = "건물_외벽_흰", "건물_나무_판", "건물_지붕_슁글_빨강", "건물_지붕_슁글_주황"
    glass, trim, metal, brick = "건물_유리창", "건물_색_흰", "건물_금속_회색", "건물_벽돌_붉은"
    G = GROUND_H                                        # 25
    left_top, right_top = G + FLOOR_H - 2.0, G + FLOOR_H + 3.0   # 38 / 43 — 일부러 엇갈려 박공 높이가 다르게
    y0, y1 = -8.0, 8.0
    lx0, lx1 = -12.0, 2.0
    rx0, rx1 = 1.5, 16.0

    b.box(lx0 - 1.0, rx1 + 1.0, y0 - 1.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    b.box(lx0, lx1, y0, y1, 0.3, left_top, white, skip=("bottom", "top"))
    b.box(rx0, rx1, y0, y1, 0.3, right_top, white, skip=("bottom", "top"))
    b.gable_roof(lx0, lx1, y0, y1, left_top, 7.0, red, gable_mat=wood, overhang=1.3, thickness=0.8, under_mat=trim, ridge="y")
    b.gable_roof(rx0, rx1, y0, y1, right_top, 9.0, orange, gable_mat=wood, overhang=1.3, thickness=0.8, under_mat=trim, ridge="y")

    # 반목조 각재 — 두 몸통 앞면에 세로 각재 둘 + 위쪽 가로대(사선 없이도 튜더식으로 읽힌다)
    for cx, roof_top in (((lx0 + lx1) / 2, left_top), ((rx0 + rx1) / 2, right_top)):
        b.beam((cx - 4.5, y0 - 0.05, 3.0), (cx - 4.5, y0 - 0.05, roof_top - 3.0), 0.35, 0.22, wood)
        b.beam((cx + 4.5, y0 - 0.05, 3.0), (cx + 4.5, y0 - 0.05, roof_top - 3.0), 0.35, 0.22, wood)
        b.beam((cx - 4.5, y0 - 0.05, roof_top - 3.0), (cx + 4.5, y0 - 0.05, roof_top - 3.0), 0.3, 0.2, wood)
        b.beam((cx - 4.5, y0 - 0.05, 3.0), (cx + 4.5, y0 - 0.05, 3.0), 0.3, 0.2, wood)

    # 둥근 창 — 왼쪽 집 정면 둘, 오른쪽 집(현관 좌우) 둘 + 박공 속 하나
    lcx = (lx0 + lx1) / 2
    rcx = (rx0 + rx1) / 2
    round_window(b, "-y", y0, lcx - 2.4, 8.0, 1.8, glass, trim)
    round_window(b, "-y", y0, lcx + 2.4, 8.0, 1.8, glass, trim)
    round_window(b, "-y", y0, rcx - 2.6, 8.0, 2.0, glass, trim)
    round_window(b, "-y", y0, rcx + 2.6, 8.0, 2.0, glass, trim)
    round_window(b, "-y", y0, rcx, right_top - 2.2, 1.5, glass, trim)
    b.windows("+y", y1, (lx0 + 5.0, (rx0 + rx1) / 2), (7.0,), 4.0, 6.0, sill_mat=trim, frame_mat=trim)
    b.windows("-x", lx0, (0.0,), (7.0,), 3.6, 5.0, sill_mat=trim, frame_mat=trim)
    b.windows("+x", rx1, (0.0,), (7.0,), 3.6, 5.0, sill_mat=trim, frame_mat=trim)

    # 현관 — 오른쪽 집 1층 유리문 + 차양
    b.panel("-y", y0, (rx0 + rx1) / 2, 0.3, 5.4, DOOR_H, glass, offset=0.1)
    b.frame("-y", y0, (rx0 + rx1) / 2, 0.3, 5.4, DOOR_H, trim, width=0.4, bottom=False)
    b.box((rx0 + rx1) / 2 - 4.0, (rx0 + rx1) / 2 + 4.0, y0 - 2.2, y0, DOOR_H + 0.6, DOOR_H + 1.4, red)

    # 원형 탑 — 왼쪽 집 왼쪽 모서리, 원뿔 지붕
    tx, ty, tr = lx0 - 1.6, y0 + 3.0, 3.0
    tower_top = left_top + 6.0
    b.cylinder(tx, ty, 0.3, tower_top, tr, 16, white, top="cap")
    b.cylinder(tx, ty, tower_top, tower_top + 9.0, tr + 0.6, 16, red, top="point", top_radius=0.1)
    round_window(b, "-y", ty - tr - 0.05, tx, 10.0, 1.6, glass, trim)

    # 굴뚝 — 오른쪽 집 지붕 위
    ck = (rx0 + rx1) / 2 + 6.0
    b.box_c(ck, 0.0, 1.6, 1.6, right_top + 4.0, 5.0, brick)
    b.box_c(ck, 0.0, 2.0, 2.0, right_top + 8.6, 0.4, "건물_콘크리트")

    # 간판 「한양영어유치원」 + 부제 ENGLISH
    b.sign("한양영어유치원", "-y", y0, (rx0 + rx1) / 2, right_top - 5.4, "건물_색_흰", "건물_색_남색",
           size=2.3, pad=0.55, max_width=11.0)
    b.text("ENGLISH", ((rx0 + rx1) / 2, y0 - 0.02, right_top - 6.9), "-y", 1.3, "건물_색_빨강", 0.2)
    return b


# ──────────────────────────────────────────────────────────── 04 구일초등학교

def make_guil_elementary():
    """낮고 긴 3층 교사 — 베이지 타일 + 콘크리트 층띠, 복도 창 가로 띠. 가운데 현관 위 시계탑.
    앞은 운동장 흙 + 철망 담장 일부. 간판 「구일초등학교」 현관 위."""
    b = Builder()
    tile, concrete, glass, trim = "건물_타일_베이지", "건물_콘크리트", "건물_유리창", "건물_색_흰"
    ground, mesh, clock = "건물_바닥_운동장", "건물_철망_잎카드", "건물_시계판"
    W, D = 42.0, 11.0
    YS = 10.5   # 앞마당(운동장)이 -y로 많이 뻗어 원점이 안 가운데였다 — 건물 전체를 +y로 밀어 맞춘다
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2 + YS, D / 2 + YS
    G = GROUND_H
    floors = [G + FLOOR_H * k for k in range(2)]
    top = G + FLOOR_H * 2                                # 55

    b.box(x0 - 1.0, x1 + 1.0, y0 - 6.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    b.box(x0, x1, y0, y1, 0.3, top, tile, skip=("bottom", "top"))
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, 0.3, 1.0, concrete, skip=("bottom", "top"))
    for z in (G,) + tuple(f + FLOOR_H for f in floors):
        b.box(x0 - 0.25, x1 + 0.25, y0 - 0.25, y1 + 0.25, z - 0.7, z + 0.1, concrete, skip=("bottom", "top"))
    us_front = [x0 + 3.0 + 3.4 * k for k in range(11)]
    for z in floors:
        b.windows("-y", y0, us_front, (z + 4.5,), 2.6, 6.5, sill_mat=concrete, frame_mat=trim)
    b.windows("+y", y1, us_front, (G + 4.5, floors[1] + 4.5), 2.6, 6.5, sill_mat=concrete, frame_mat=trim)
    b.windows("-x", x0, (0.0,), (G + 4.5,), 2.4, 5.0, sill_mat=concrete, frame_mat=trim)
    b.windows("+x", x1, (0.0,), (G + 4.5,), 2.4, 5.0, sill_mat=concrete, frame_mat=trim)
    for x in (x0 + 0.5, x1 - 0.5):
        b.box_c(x, y0 - 0.45, 0.5, 0.5, 0.3, top + 0.6, "건물_금속_회색", skip=("bottom",))

    # 현관 — 가운데, 유리문 3짝 + 캐노피
    cx = 0.0
    b.panel("-y", y0, cx, 0.3, 9.0, DOOR_H, glass, offset=0.1)
    b.frame("-y", y0, cx, 0.3, 9.0, DOOR_H, trim, width=0.5, bottom=False)
    b.box(cx - 6.5, cx + 6.5, y0 - 3.0, y0, DOOR_H + 0.5, DOOR_H + 1.1, concrete)
    for px in (cx - 5.6, cx + 5.6):
        b.box_c(px, y0 - 2.6, 0.5, 0.5, 0.3, DOOR_H + 0.4, trim, skip=("bottom", "top"))

    # 시계탑 — 현관 위로 솟는 탑, 정면에 시계판
    tx0, tx1 = cx - 4.0, cx + 4.0
    tower_top = top + 20.0                               # 75
    b.box(tx0, tx1, y0 + 1.5, y0 + 6.5, top, tower_top - 3.0, tile, skip=("bottom",))
    b.hip_roof(tx0 - 0.4, tx1 + 0.4, y0 + 1.1, y0 + 6.9, tower_top - 3.0, 3.0, "건물_지붕_기와")
    b.panel("-y", y0 + 1.5, cx, tower_top - 9.0, 5.4, 5.4, clock, offset=0.03)
    b.frame("-y", y0 + 1.5, cx, tower_top - 9.0, 5.4, 5.4, trim, width=0.3, bottom=True)

    # 간판 「구일초등학교」 현관 위
    b.sign("구일초등학교", "-y", y0, cx, top - 7.5, "건물_색_흰", "건물_색_남색", size=2.6, pad=0.6, max_width=16.0)

    # 앞마당 — 운동장 흙 + 철망 담장 일부(양쪽 끝)
    b.box(x0 - 1.0, x1 + 1.0, y0 - 22.0, y0 - 6.0, 0.0, 0.2, ground, skip=("bottom",))
    for fx0, fx1 in ((x0 - 1.0, x0 + 9.0), (x1 - 9.0, x1 + 1.0)):
        n = 5
        step = (fx1 - fx0) / n
        for k in range(n):
            fx = fx0 + step * (k + 0.5)
            b.panel("-y", y0 - 21.8, fx, 0.0, step * 0.94, 3.0, mesh, offset=0.0)
        b.box_c(fx0, y0 - 21.8, 0.25, 0.25, 0.0, 3.2, "건물_금속_회색", skip=("bottom",))
        b.box_c(fx1, y0 - 21.8, 0.25, 0.25, 0.0, 3.2, "건물_금속_회색", skip=("bottom",))
    return b


# ──────────────────────────────────────────────────────────── 05 구일중학교

def make_guil_middle():
    """ㄱ자 4층 교사 — 필로티형 현관 캐노피, 앞마당 국기 게양대 셋(가운데가 가장 높다). 간판 「구일중학교」."""
    b = Builder()
    white, concrete, glass, trim, metal = "건물_외벽_흰", "건물_콘크리트", "건물_유리창", "건물_색_흰", "건물_금속_스테인리스"
    flag = "건물_국기_태극기"
    G = GROUND_H
    top = G + FLOOR_H * 3                                # 70
    # 몸통 A(가로로 긴 날개) / 몸통 B(세로 날개, +y로 꺾임). XS: ㄱ자라 원점이 안 가운데였다 — x로 밀어 맞춘다.
    XS = 3.7
    ax0, ax1, ay0, ay1 = -21.0 + XS, 15.0 + XS, -6.0, 6.0
    bx0, bx1, by0, by1 = 5.0 + XS, 15.0 + XS, 6.0, 18.0

    b.box(ax0 - 1.0, ax1 + 1.0, ay0 - 1.0, by1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    # 1층 필로티(날개 A 왼쪽 절반은 기둥만, 오른쪽은 막힌 벽)
    b.box(ax0 + 6.0, ax1, ay0, ay1, 0.3, G, white, skip=("bottom", "top"))
    for x in (ax0 + 1.5, ax0 + 4.0):
        b.box_c(x, 0.0, 1.4, 1.4, 0.3, G - 0.3, concrete, skip=("bottom", "top"))
    b.windows("-y", ay0, (ax0 + 9.0, ax0 + 15.5), (5.0,), 3.4, 6.0, sill_mat=concrete, frame_mat=trim)
    b.box(ax0 - 0.4, ax1 + 0.4, ay0 - 0.4, ay1 + 0.4, G - 0.8, G + 0.2, concrete)

    # 위 3개 층(A·B 몸통 전부)
    b.box(ax0, ax1, ay0, ay1, G + 0.2, top, white, skip=("bottom", "top"))
    b.box(bx0, bx1, ay1, by1, 0.3, top, white, skip=("bottom", "top"))
    for z in (G,) + tuple(G + FLOOR_H * k for k in (1, 2)):
        b.box(ax0 - 0.25, ax1 + 0.25, ay0 - 0.25, ay1 + 0.25, z + FLOOR_H - 0.6, z + FLOOR_H + 0.15, concrete, skip=("bottom", "top"))
        b.box(bx0 - 0.25, bx1 + 0.25, ay1 - 0.25, by1 + 0.25, z + FLOOR_H - 0.6, z + FLOOR_H + 0.15, concrete, skip=("bottom", "top"))
    for k in range(3):
        z = G + FLOOR_H * k
        b.windows("-y", ay0, [ax0 + 3.0 + 3.6 * j for j in range(9)], (z + 4.5,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
        b.windows("+y", ay1, [ax0 + 3.0 + 3.6 * j for j in range(9)], (z + 4.5,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
        b.windows("+x", bx1, (ay1 + 3.0, ay1 + 9.0), (z + 4.5,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    b.windows("-x", ax0, (0.0,), (G + 4.5, G + FLOOR_H + 4.5), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    b.windows("+y", by1, (bx0 + 3.0,), (G + 4.5, G + FLOOR_H + 4.5, G + 2 * FLOOR_H + 4.5), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)

    # 현관 — 필로티 부분 유리문 + 캐노피(얇게 앞으로 나온 판)
    cx = ax0 + 2.6
    b.panel("-y", ay0, cx, 0.3, 4.0, DOOR_H, glass, offset=0.08)
    b.frame("-y", ay0, cx, 0.3, 4.0, DOOR_H, trim, width=0.4, bottom=False)
    b.box(cx - 5.0, cx + 5.0, ay0 - 4.0, ay0, DOOR_H + 0.6, DOOR_H + 1.1, concrete)
    for px in (ay0 - 3.6,):
        b.box_c(cx - 4.4, px, 0.4, 0.4, 0.3, DOOR_H + 0.3, trim, skip=("bottom", "top"))
        b.box_c(cx + 4.4, px, 0.4, 0.4, 0.3, DOOR_H + 0.3, trim, skip=("bottom", "top"))

    # 옥상 난간(A동만, 낮게)
    b.railing([(ax0 + 0.3, ay0 + 0.3, top), (ax1 - 0.3, ay0 + 0.3, top), (ax1 - 0.3, ay1 - 0.3, top), (ax0 + 0.3, ay1 - 0.3, top)],
              1.6, concrete, post_gap=3.0, post=0.3, rail=0.3, closed=True)

    # 간판 「구일중학교」
    b.sign("구일중학교", "-y", ay0, ax0 + 12.0, G + FLOOR_H * 2 + 3.0, "건물_색_흰", "건물_색_남색", size=2.4, pad=0.55, max_width=14.0)

    # 앞마당 국기 게양대 셋 — 가운데가 가장 높다
    for fx, fh in ((-8.0 + XS, 13.0), (0.0 + XS, 17.0), (8.0 + XS, 13.0)):
        fy = ay0 - 8.0
        b.cylinder(fx, fy, 0.3, fh, 0.35, 10, metal, top="cap")
        b.panel("-y", fy - 0.4, fx, fh - 5.0, 3.0, 2.0, flag, offset=0.0)
    return b


# ──────────────────────────────────────────────────────────── 06 구일고등학교

def make_guil_high():
    """5층 본관 판상형 — 흰 외벽 + 콘크리트 기둥 줄(수직 리듬), 옥상 난간. 간판 최상층 띠."""
    b = Builder()
    white, concrete, glass, trim = "건물_외벽_흰", "건물_콘크리트", "건물_유리창", "건물_색_흰"
    W, D = 40.0, 13.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H
    top = G + FLOOR_H * 4                                # 85

    b.box(x0 - 1.0, x1 + 1.0, y0 - 1.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    b.box(x0, x1, y0, y1, 0.3, top, white, skip=("bottom", "top"))
    for z in (G,) + tuple(G + FLOOR_H * k for k in (1, 2, 3)):
        b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, z - 0.7, z + 0.1, concrete, skip=("bottom", "top"))
    cols = [x0 + 3.0 + 3.08 * k for k in range(13)]
    for cx in cols:
        b.box_c(cx, y0 - 0.35, 1.0, 0.5, 0.3, top, concrete, skip=("bottom", "top"))
    for k in range(4):
        z = G + FLOOR_H * k
        us = [x0 + 4.5 + 3.08 * j for j in range(12)]
        b.windows("-y", y0, us, (z + 4.5,), 2.2, 6.5, sill_mat=concrete, frame_mat=trim)
        b.windows("+y", y1, [x0 + 4.5 + 6.16 * j for j in range(6)], (z + 4.5,), 2.6, 6.5, sill_mat=concrete, frame_mat=trim)
    b.windows("-x", x0, (0.0,), (G + 4.5,), 2.4, 5.0, sill_mat=concrete, frame_mat=trim)
    b.windows("+x", x1, (0.0,), (G + 4.5,), 2.4, 5.0, sill_mat=concrete, frame_mat=trim)

    # 현관
    cx = 0.0
    b.panel("-y", y0, cx, 0.3, 8.0, DOOR_H, glass, offset=0.1)
    b.frame("-y", y0, cx, 0.3, 8.0, DOOR_H, trim, width=0.45, bottom=False)
    b.box(cx - 6.0, cx + 6.0, y0 - 3.0, y0, DOOR_H + 0.5, DOOR_H + 1.1, concrete)

    # 옥상 난간(전체 높이 90 안 — 계단실은 생략)
    b.railing([(x0 + 0.3, y0 + 0.3, top), (x1 - 0.3, y0 + 0.3, top), (x1 - 0.3, y1 - 0.3, top), (x0 + 0.3, y1 - 0.3, top)],
              2.4, concrete, post_gap=3.2, post=0.3, rail=0.3, closed=True)

    # 간판 — 최상층 띠(4층 위, 옥상 아래)
    b.box(x0 + 6.0, x1 - 6.0, y0 - 0.15, y0, top - 4.5, top - 1.0, "건물_색_남색")
    b.sign("구일고등학교", "-y", y0, cx, top - 4.2, "건물_색_남색", "건물_색_흰", size=2.6, pad=0.5, max_width=22.0, board_depth=0.15)
    return b


# ──────────────────────────────────────────────────────────── 07 메가스터디

def make_megastudy():
    """유리창 빼곡한 학원 빌딩 — 좁고 높은 몸통, 층마다 촘촘한 창 격자. 옆면 세로 간판 「메가\\n스터디」,
    정면 상단 가로 간판, 층간 색 현수막. 1층은 유리 상가(편의점풍)."""
    b = Builder()
    white, concrete, glass, trim = "건물_외벽_흰", "건물_콘크리트", "건물_유리창", "건물_색_흰"
    curtain, metal = "건물_유리_커튼월", "건물_금속_회색"
    W, D = 22.0, 18.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H
    top = G + FLOOR_H * 4                                # 85

    b.box(x0 - 1.0, x1 + 1.0, y0 - 1.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    b.box(x0, x1, y0, y1, 0.3, top, white, skip=("bottom", "top"))
    for z in (G,) + tuple(G + FLOOR_H * k for k in (1, 2, 3)):
        b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, z - 0.7, z + 0.1, concrete, skip=("bottom", "top"))

    # 1층 — 유리 상가(편의점풍), 전면 통유리 + 얇은 문틀
    b.panel("-y", y0, 0.0, 1.4, W - 3.0, G - 4.0, glass, offset=0.05)
    b.frame("-y", y0, 0.0, 1.4, W - 3.0, G - 4.0, metal, width=0.3, bottom=True)
    b.box(x0 + 1.0, x1 - 1.0, y0 - 0.1, y0, G - 4.5, G - 3.8, "건물_색_빨강")

    # 2~5층 — 촘촘한 창 격자(작은 창 + 창틀)
    us = [x0 + 2.2 + 2.6 * k for k in range(7)]
    for k in range(4):
        z = G + FLOOR_H * k
        b.windows("-y", y0, us, (z + 3.0, z + 8.5), 1.9, 3.6, frame_mat=trim)
        b.windows("+y", y1, us, (z + 3.0, z + 8.5), 1.9, 3.6, frame_mat=trim)
    # 옆면(+x) — 통유리 커튼월로 포인트
    # skip="-x" — 안쪽 면은 원래 벽(+x면, 흰색)과 거의 겹쳐 z-fighting을 낸다, 바깥쪽 면만 있으면 된다.
    b.box(x1 - 0.05, x1 + 0.05, y0 + 1.0, y1 - 1.0, G, top, curtain, skip=("bottom", "top", "-x"))
    b.windows("-x", x0, [y0 + 3.0 + 4.0 * k for k in range(4)], (G + 4.0,), 2.6, 5.5, frame_mat=trim)

    # 층간 색 현수막(3층 위, 4층 아래 — 학원 광고판 느낌)
    banner_z = G + FLOOR_H * 2
    b.box(x0 + 1.5, x1 - 1.5, y0 - 0.15, y0, banner_z, banner_z + 2.4, "건물_색_노랑")

    # 정면 상단 가로 간판 「메가스터디」
    b.sign("메가스터디", "-y", y0, 0.0, top - 5.5, "건물_색_흰", "건물_색_남색", size=2.2, pad=0.5, max_width=W - 4.0, board_depth=0.2)
    # 옆면(+x) 세로 돌출 간판 「메가\n스터디」
    b.sign("메가\n스터디", "+x", x1, 0.0, G + 2.0, "건물_색_흰", "건물_네온_분홍", size=2.4, pad=0.6, board_depth=0.5)
    return b


# ──────────────────────────────────────────────────────────── 목록

CATALOG = [
    ("Story03_한양영어유치원", "03 English Kindergarten", make_hanyang_kindergarten,
     "동화책풍 반목조 + 엇갈린 박공 둘(주황·빨강 슁글) + 원뿔 지붕 원형 탑 + 둥근 창 + 굴뚝 · 간판 한양영어유치원"),
    ("Story04_구일초등학교", "04 Guil Elementary", make_guil_elementary,
     "낮고 긴 3층 · 베이지 타일 + 콘크리트 층띠 · 현관 위 시계탑 · 앞마당 운동장·철망 담장 · 간판 구일초등학교"),
    ("Story05_구일중학교", "05 Guil Middle", make_guil_middle,
     "ㄱ자 4층 · 필로티 현관 캐노피 · 국기 게양대 셋(가운데 최고) · 간판 구일중학교"),
    ("Story06_구일고등학교", "06 Guil High", make_guil_high,
     "5층 판상형 · 수직 콘크리트 기둥 줄 · 옥상 난간 · 최상층 남색 간판띠 구일고등학교"),
    ("Story07_메가스터디", "07 Megastudy", make_megastudy,
     "좁고 높은 학원 빌딩 · 촘촘한 창 격자 · 1층 유리 상가 · 옆면 커튼월 + 세로 간판 · 정면 가로 간판 메가스터디"),
]


if __name__ == "__main__":
    bc.run(CATALOG)
