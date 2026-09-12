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
    """동화책풍 유치원 — 흰 미장 + 굵은 반목조 각재, 가파른 박공 둘이 높이를 엇갈려(주황·빨강 슁글),
    왼쪽에 뾰족 원뿔 지붕 원형 탑. 둥근 창은 박공 속·탑 위층에, 정면은 보통 창으로. 굴뚝.
    간판 「한양영어유치원」 + 작은 "ENGLISH" 부제.

    🔴 2026-09-12 정정(PM 렌더 검수) — 왼쪽·오른쪽 집 지붕이 x로 겹쳐(담벼락은 살짝 겹치게 붙였는데
    지붕은 처마까지 그대로 겹쳐 폭이 더 넓다) 두 경사면이 서로 뚫고 들어가 "혀처럼 비틀린" 메시가
    됐다. 두 집 사이에 처마가 안 닿을 만큼 틈(낮은 편평한 연결부)을 두어 겹침 자체를 없앴다."""
    b = Builder()
    white, wood, red, orange = "건물_외벽_흰", "건물_나무_판", "건물_지붕_슁글_빨강", "건물_지붕_슁글_주황"
    glass, trim, metal, brick = "건물_유리창", "건물_색_흰", "건물_금속_회색", "건물_벽돌_붉은"
    G = GROUND_H                                        # 25
    left_top, right_top = G + FLOOR_H - 2.0, G + FLOOR_H + 3.0   # 38 / 43 — 일부러 엇갈려 박공 높이가 다르게
    y0, y1 = -8.0, 8.0
    XS = 1.0                                            # 원점 재조정용(실측: 중심이 안 맞아 이 값으로 밀었다)
    lx0, lx1 = -14.0 + XS, -1.0 + XS                    # 왼쪽 집(처마 안 겹치게 rx0와 3 틈)
    rx0, rx1 = 2.0 + XS, 16.0 + XS                      # 오른쪽 집
    lcx, rcx = (lx0 + lx1) / 2, (rx0 + rx1) / 2

    b.box(lx0 - 1.0, rx1 + 1.0, y0 - 1.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    b.box(lx0, lx1, y0, y1, 0.3, left_top, white, skip=("bottom", "top"))
    b.box(lx1, rx0, y0, y1, 0.3, left_top, white, skip=("bottom",))            # 낮은 연결부 — 평평한 지붕(top 안 뺌)
    b.box(rx0, rx1, y0, y1, 0.3, right_top, white, skip=("bottom", "top"))
    b.gable_roof(lx0, lx1, y0, y1, left_top, 7.0, red, gable_mat=wood, overhang=1.3, thickness=0.8, under_mat=trim, ridge="y")
    b.gable_roof(rx0, rx1, y0, y1, right_top, 9.0, orange, gable_mat=wood, overhang=1.3, thickness=0.8, under_mat=trim, ridge="y")

    # 반목조 각재 — 굵게(폭 0.8, 돌출 0.3): 세로 기둥 둘 + 층 경계 가로보 + 2층에 사선 가새 하나.
    # beam()은 방향에 따라 width·height가 가로/돌출로 다르게 매핑된다 — 세로 기둥은 (width=폭, height=돌출),
    # 가로보는 반대로 (width=돌출, height=폭)다(beam() 내부에서 방향마다 기준축이 바뀐다).
    for cx, roof_top in ((lcx, left_top), (rcx, right_top)):
        half = 4.5
        b.beam((cx - half, y0 - 0.05, 3.0), (cx - half, y0 - 0.05, G), 0.8, 0.3, wood)              # 세로 기둥(1층)
        b.beam((cx + half, y0 - 0.05, 3.0), (cx + half, y0 - 0.05, G), 0.8, 0.3, wood)
        b.beam((cx - half, y0 - 0.05, G), (cx - half, y0 - 0.05, roof_top - 2.5), 0.8, 0.3, wood)    # 세로 기둥(2층)
        b.beam((cx + half, y0 - 0.05, G), (cx + half, y0 - 0.05, roof_top - 2.5), 0.8, 0.3, wood)
        b.beam((cx - half, y0 - 0.05, G), (cx + half, y0 - 0.05, G), 0.3, 0.8, wood)                 # 층 경계 가로보
        b.beam((cx - half, y0 - 0.05, roof_top - 2.5), (cx + half, y0 - 0.05, roof_top - 2.5), 0.3, 0.8, wood)
        b.beam((cx - half, y0 - 0.05, G), (cx + half, y0 - 0.05, roof_top - 2.5), 0.5, 0.3, wood)    # 2층 사선 가새

    # 1층 창 — 왼쪽 집 둘(창틀·창턱), 오른쪽 집은 현관 옆 하나씩
    b.windows("-y", y0, (lcx - 2.2, lcx + 2.2), (5.0,), 2.4, 4.6, sill_mat=trim, frame_mat=trim)
    b.windows("-y", y0, (rcx - 5.0, rcx + 5.0), (5.0,), 2.2, 4.2, sill_mat=trim, frame_mat=trim)
    # 2층 창 — 두 집에 하나씩(가운데는 사선 가새를 피해 살짝 옆으로)
    b.windows("-y", y0, (lcx - 2.6,), (G + 4.0,), 2.2, 4.0, sill_mat=trim, frame_mat=trim)
    b.windows("-y", y0, (rcx - 3.0, rcx + 3.0), (G + 4.5,), 2.4, 4.4, sill_mat=trim, frame_mat=trim)
    # 둥근 창 — 박공 속(처마 삼각형 면, 아래서 뚫지 않고 그 위에 얹는다)
    round_window(b, "-y", y0 - 1.3, lcx, left_top + 3.0, 1.5, glass, trim)
    round_window(b, "-y", y0 - 1.3, rcx, right_top + 3.4, 1.6, glass, trim)
    b.windows("+y", y1, (lcx, rcx), (7.0,), 4.0, 6.0, sill_mat=trim, frame_mat=trim)
    b.windows("-x", lx0, (0.0,), (7.0,), 3.6, 5.0, sill_mat=trim, frame_mat=trim)
    b.windows("+x", rx1, (0.0,), (7.0,), 3.6, 5.0, sill_mat=trim, frame_mat=trim)

    # 현관 — 오른쪽 집 1층 유리문 + 차양
    b.panel("-y", y0, rcx, 0.3, 5.4, DOOR_H, glass, offset=0.1)
    b.frame("-y", y0, rcx, 0.3, 5.4, DOOR_H, trim, width=0.4, bottom=False)
    b.box(rcx - 4.0, rcx + 4.0, y0 - 2.2, y0, DOOR_H + 0.6, DOOR_H + 1.4, red)

    # 원형 탑 — 왼쪽 집 왼쪽 모서리, 원뿔 지붕 + 처마 띠(고리) + 위층 둥근 창 둘
    tx, ty, tr = lx0 - 1.6, y0 + 3.0, 3.0
    tower_top = left_top + 6.0
    b.cylinder(tx, ty, 0.3, tower_top, tr, 16, white, top="cap")
    b.cylinder(tx, ty, tower_top - 0.3, tower_top, tr + 0.6, 16, trim, top="none")          # 처마 띠 — 반지름이 원뿔 밑과 같아 틈이 안 생긴다
    b.cylinder(tx, ty, tower_top, tower_top + 9.0, tr + 0.6, 16, red, top="point", top_radius=0.1)
    round_window(b, "-y", ty - tr - 0.05, tx, 15.0, 1.5, glass, trim)
    round_window(b, "-y", ty - tr - 0.05, tx, 30.0, 1.3, glass, trim)

    # 굴뚝 — 오른쪽 집 지붕 위
    ck = rcx + 6.0
    b.box_c(ck, 0.0, 1.6, 1.6, right_top + 4.0, 5.0, brick)
    b.box_c(ck, 0.0, 2.0, 2.0, right_top + 8.6, 0.4, "건물_콘크리트")

    # 모서리 홈통 + 실외기(공통 보강)
    b.box_c(lx0 + 0.4, y1 - 0.3, 0.35, 0.35, 0.3, left_top + 1.0, metal, skip=("bottom",))
    b.box_c(rx1 - 0.4, y0 + 0.3, 1.6, 1.4, G + 2.0, 1.4, trim)

    # 간판 「한양영어유치원」(폭의 1/12 이상 — 34.5/12≈2.9) + 부제 ENGLISH
    b.sign("한양영어유치원", "-y", y0, rcx, right_top - 6.4, "건물_색_흰", "건물_색_남색",
           size=3.2, pad=0.6, max_width=13.5)
    # 🔴 정정(PM 렌더 검수) — z=34.7이 2층 사선 가새(z 25~40.5)의 딱 중간을 지나 글자가 가려졌다.
    # 가새보다 확실히 앞으로(y0-0.02 → y0-0.7) 뺐다.
    b.text("ENGLISH", (rcx, y0 - 0.7, right_top - 8.3), "-y", 1.5, "건물_색_빨강", 0.2)
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
    # 🔴 2026-09-12 정정(blender 세션) — 몸통 top을 빼고 옥상 면을 안 넣어, 위에서 보면 속이 뚫려 있었다(유니티는
    # 뒷면을 안 그린다 — 게임 카메라가 위에서 내려다본다). 옥상 방수 면을 덮는다.
    b.face(((x0, y0, top), (x1, y0, top), (x1, y1, top), (x0, y1, top)), "건물_옥상_방수")
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, 0.3, 1.0, concrete, skip=("bottom", "top"))
    for z in (G,) + tuple(f + FLOOR_H for f in floors):
        b.box(x0 - 0.25, x1 + 0.25, y0 - 0.25, y1 + 0.25, z - 0.7, z + 0.1, concrete, skip=("bottom", "top"))
    us_front = [x0 + 3.0 + 3.4 * k for k in range(11)]
    # 🔴 2026-09-12 정정(PM 렌더 검수) — floors=[G, G+FLOOR_H]는 1층 "위" 경계라, z+4.5로 창을 놓으면
    # 전부 2·3층에만 생기고 1층 자체(0.3~G)는 옆·뒤까지 통째로 막힌 벽이었다. 1층 창을 따로 낸다.
    b.windows("-y", y0, us_front[1:4] + us_front[-4:-1], (5.0,), 2.6, 6.5, sill_mat=concrete, frame_mat=trim)
    for z in floors:
        b.windows("-y", y0, us_front, (z + 4.5,), 2.6, 6.5, sill_mat=concrete, frame_mat=trim)
    b.windows("+y", y1, us_front, (5.0, G + 4.5, floors[1] + 4.5), 2.6, 6.5, sill_mat=concrete, frame_mat=trim)
    # 🔴 정정(PM 렌더 검수) — u=0.0은 이 건물의 y 범위([y0,y1], YS만큼 밀려 있다) 밖이라 창이 벽에서
    # 떨어져 운동장 쪽에 붕 떠 있었다. 벽 안(가운데)으로 옮긴다.
    b.windows("-x", x0, ((y0 + y1) / 2,), (5.0, G + 4.5), 2.4, 5.0, sill_mat=concrete, frame_mat=trim)
    b.windows("+x", x1, ((y0 + y1) / 2,), (5.0, G + 4.5), 2.4, 5.0, sill_mat=concrete, frame_mat=trim)
    for x in (x0 + 0.5, x1 - 0.5):
        b.box_c(x, y0 - 0.45, 0.5, 0.5, 0.3, top + 0.6, "건물_금속_회색", skip=("bottom",))
    b.box_c(x1 - 2.0, 0.0, 1.8, 1.4, 5.5, 1.6, trim)                # 옆벽 실외기

    # 현관 — 가운데, 유리문 3짝 + 캐노피
    cx = 0.0
    b.panel("-y", y0, cx, 0.3, 9.0, DOOR_H, glass, offset=0.1)
    b.frame("-y", y0, cx, 0.3, 9.0, DOOR_H, trim, width=0.5, bottom=False)
    b.box(cx - 6.5, cx + 6.5, y0 - 3.0, y0, DOOR_H + 0.5, DOOR_H + 1.1, concrete)
    for px in (cx - 5.6, cx + 5.6):
        b.box_c(px, y0 - 2.6, 0.5, 0.5, 0.3, DOOR_H + 0.4, trim, skip=("bottom", "top"))

    # 시계탑 — 🔴 정정: 뒤로 물러나 있어 정면에서 옥상 뒤로 숨었다. 현관 축(정면 벽)에 바짝 붙이고
    # 시계판을 크게(폭 7~8) 키웠다.
    tx0, tx1 = cx - 4.5, cx + 4.5
    tower_top = top + 20.0                               # 75
    b.box(tx0, tx1, y0 + 0.2, y0 + 5.2, top, tower_top - 3.0, tile, skip=("bottom",))
    b.hip_roof(tx0 - 0.4, tx1 + 0.4, y0 - 0.2, y0 + 5.6, tower_top - 3.0, 3.0, "건물_지붕_기와")
    b.panel("-y", y0 + 0.2, cx, tower_top - 9.5, 7.4, 7.4, clock, offset=0.03)
    b.frame("-y", y0 + 0.2, cx, tower_top - 9.5, 7.4, 7.4, trim, width=0.35, bottom=True)

    # 간판 「구일초등학교」 현관 위(폭 44.2/12≈3.7 이상)
    b.sign("구일초등학교", "-y", y0, cx, top - 7.5, "건물_색_흰", "건물_색_남색", size=3.8, pad=0.6, max_width=22.0)

    # 옥상 난간
    b.railing([(x0 + 0.3, y0 + 0.3, top), (x1 - 0.3, y0 + 0.3, top), (x1 - 0.3, y1 - 0.3, top), (x0 + 0.3, y1 - 0.3, top)],
              1.6, concrete, post_gap=3.0, post=0.3, rail=0.3, closed=True)

    # 앞마당 — 운동장 흙 + 앞·옆으로 이어진 철망 담장 + 문기둥 둘(가운데 트임)
    b.box(x0 - 1.0, x1 + 1.0, y0 - 22.0, y0 - 6.0, 0.0, 0.2, ground, skip=("bottom",))
    fy = y0 - 21.8
    gate_gap = 6.0
    segs = ((x0 - 1.0, -gate_gap / 2), (gate_gap / 2, x1 + 1.0))
    for fx0, fx1 in segs:
        n = max(1, round((fx1 - fx0) / 3.0))
        step = (fx1 - fx0) / n
        for k in range(n):
            fx = fx0 + step * (k + 0.5)
            b.panel("-y", fy, fx, 0.0, step * 0.94, 3.0, mesh, offset=0.0)
        b.box_c(fx0, fy, 0.25, 0.25, 0.0, 3.2, "건물_금속_회색", skip=("bottom",))
    for gx in (-gate_gap / 2, gate_gap / 2):
        b.box_c(gx, fy, 0.4, 0.4, 0.0, 3.6, concrete, skip=("bottom",))
    return b


# ──────────────────────────────────────────────────────────── 05 구일중학교

def make_guil_middle():
    """ㄱ자 4층 교사 — 붉은 벽돌 + 흰 층띠·창틀(대비), 필로티형 현관 캐노피, 앞마당 국기 게양대 셋
    (가운데가 가장 높다). 간판 「구일중학교」."""
    b = Builder()
    brick, concrete, glass, trim, metal = "건물_벽돌_붉은", "건물_콘크리트", "건물_유리창", "건물_색_흰", "건물_금속_스테인리스"
    flag = "건물_국기_태극기"
    G = GROUND_H
    top = G + FLOOR_H * 3                                # 70
    # 몸통 A(가로로 긴 날개) / 몸통 B(세로 날개, +y로 꺾임). XS: ㄱ자라 원점이 안 가운데였다 — x로 밀어 맞춘다.
    XS = 3.7
    ax0, ax1, ay0, ay1 = -21.0 + XS, 15.0 + XS, -6.0, 6.0
    bx0, bx1, by0, by1 = 5.0 + XS, 15.0 + XS, 6.0, 18.0

    b.box(ax0 - 1.0, ax1 + 1.0, ay0 - 1.0, by1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    # 1층 필로티(날개 A 왼쪽 절반은 기둥만, 오른쪽은 막힌 벽 + 창)
    b.box(ax0 + 6.0, ax1, ay0, ay1, 0.3, G, brick, skip=("bottom", "top"))
    for x in (ax0 + 1.5, ax0 + 4.0):
        b.box_c(x, 0.0, 1.4, 1.4, 0.3, G - 0.3, concrete, skip=("bottom", "top"))
    b.windows("-y", ay0, (ax0 + 9.0, ax0 + 15.5), (5.0,), 3.4, 6.0, sill_mat=concrete, frame_mat=trim)
    b.box(ax0 - 0.4, ax1 + 0.4, ay0 - 0.4, ay1 + 0.4, G - 0.8, G + 0.2, concrete)

    # 위 3개 층(A·B 몸통 전부)
    b.box(ax0, ax1, ay0, ay1, G + 0.2, top, brick, skip=("bottom", "top"))
    b.box(bx0, bx1, ay1, by1, 0.3, top, brick, skip=("bottom", "top"))
    # 🔴 2026-09-12 정정(blender 세션) — 두 날개 모두 옥상 면이 없어 위에서 속이 뚫려 보였다(유니티는 뒷면을 안 그린다).
    b.face(((ax0, ay0, top), (ax1, ay0, top), (ax1, ay1, top), (ax0, ay1, top)), "건물_옥상_방수")
    b.face(((bx0, ay1, top), (bx1, ay1, top), (bx1, by1, top), (bx0, by1, top)), "건물_옥상_방수")
    for z in (G,) + tuple(G + FLOOR_H * k for k in (1, 2)):
        b.box(ax0 - 0.25, ax1 + 0.25, ay0 - 0.25, ay1 + 0.25, z + FLOOR_H - 0.6, z + FLOOR_H + 0.15, "건물_색_흰", skip=("bottom", "top"))
        b.box(bx0 - 0.25, bx1 + 0.25, ay1 - 0.25, by1 + 0.25, z + FLOOR_H - 0.6, z + FLOOR_H + 0.15, "건물_색_흰", skip=("bottom", "top"))
    # 1층 창(옆·뒤 — 전에는 위 3개 층에만 있어 1층이 통째로 막힌 벽이었다)
    b.windows("-y", ay0, [ax0 + 3.0 + 3.6 * j for j in (0, 1, 6, 7, 8)], (5.0,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    b.windows("-x", ax0, (0.0,), (5.0,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    for k in range(3):
        z = G + FLOOR_H * k
        b.windows("-y", ay0, [ax0 + 3.0 + 3.6 * j for j in range(9)], (z + 4.5,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
        b.windows("+y", ay1, [ax0 + 3.0 + 3.6 * j for j in range(9)], (z + 4.5,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
        b.windows("+x", bx1, (ay1 + 3.0, ay1 + 9.0), (z + 4.5,), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    b.windows("-x", ax0, (0.0,), (G + 4.5, G + FLOOR_H + 4.5), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    b.windows("+y", by1, (bx0 + 3.0,), (5.0, G + 4.5, G + FLOOR_H + 4.5, G + 2 * FLOOR_H + 4.5), 2.6, 6.0, sill_mat=concrete, frame_mat=trim)
    b.box_c(ax1 - 1.5, ay1 + 3.0, 1.6, 1.4, 5.5, 1.6, trim)         # 실외기

    # 현관 — 필로티 부분 유리문 + 캐노피(얇게 앞으로 나온 판) + 기둥 둘
    cx = ax0 + 2.6
    b.panel("-y", ay0, cx, 0.3, 4.0, DOOR_H, glass, offset=0.08)
    b.frame("-y", ay0, cx, 0.3, 4.0, DOOR_H, trim, width=0.4, bottom=False)
    b.box(cx - 5.0, cx + 5.0, ay0 - 4.0, ay0, DOOR_H + 0.6, DOOR_H + 1.1, concrete)
    for px in (cx - 4.4, cx + 4.4):
        b.box_c(px, ay0 - 3.6, 0.5, 0.5, 0.3, DOOR_H + 0.3, trim, skip=("bottom", "top"))

    # 옥상 난간(A동만, 낮게) + 모서리 홈통
    b.railing([(ax0 + 0.3, ay0 + 0.3, top), (ax1 - 0.3, ay0 + 0.3, top), (ax1 - 0.3, ay1 - 0.3, top), (ax0 + 0.3, ay1 - 0.3, top)],
              1.6, concrete, post_gap=3.0, post=0.3, rail=0.3, closed=True)
    b.box_c(ax0 + 0.5, ay0 + 0.4, 0.35, 0.35, 0.3, top + 1.0, metal, skip=("bottom",))

    # 간판 「구일중학교」(폭 39.4/12≈3.3 이상)
    b.sign("구일중학교", "-y", ay0, ax0 + 12.0, G + FLOOR_H * 2 + 2.5, "건물_색_흰", "건물_색_남색", size=3.8, pad=0.6, max_width=18.0)

    # 앞마당 국기 게양대 셋 — 가운데가 가장 높다. 굵고(0.6) 높게(28~35), 깃발도 크게(7×4.7), 콘크리트 기단.
    for fx, fh in ((-9.0 + XS, 28.0), (0.0 + XS, 35.0), (9.0 + XS, 28.0)):
        fy = ay0 - 10.0
        b.cylinder(fx, fy, 0.3, 0.9, 1.0, 10, concrete, top="none")
        b.cylinder(fx, fy, 0.9, fh, 0.3, 10, metal, top="cap")
        b.panel("-y", fy - 0.5, fx, fh - 11.0, 7.0, 4.7, flag, offset=0.0)
    return b


# ──────────────────────────────────────────────────────────── 06 구일고등학교

def make_guil_high():
    """5층 본관 판상형 — 크림·회색 콘크리트 외벽(수직 기둥 리듬) + 남색 띠(학교 상징색), 옥상 난간.
    간판은 핀 앞으로 튀어나온 흰 판 + 남색 글씨."""
    b = Builder()
    concrete, glass, trim, navy = "건물_콘크리트", "건물_유리창", "건물_색_흰", "건물_색_남색"
    W, D = 40.0, 13.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H
    top = G + FLOOR_H * 4                                # 85

    b.box(x0 - 1.0, x1 + 1.0, y0 - 1.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    b.box(x0, x1, y0, y1, 0.3, top, concrete, skip=("bottom", "top"))
    # 🔴 2026-09-12 정정(blender 세션) — 옥상 면이 없어 위에서 속이 뚫려 보였다(유니티는 뒷면을 안 그린다).
    b.face(((x0, y0, top), (x1, y0, top), (x1, y1, top), (x0, y1, top)), "건물_옥상_방수")
    # 🔴 정정(PM 렌더 검수) — 회색+흰 격자가 게임 시점에서 거의 흰 덩어리로 날아가 07 옆에서 대비가
    # 약했다. 남색 띠를 층마다 두껍게(0.8→1.4) 두르고, 창틀 흰 핀(trim)을 콘크리트로 낮췄다 —
    # 「회색+남색 줄무늬」로 읽히도록.
    for z in (G,) + tuple(G + FLOOR_H * k for k in (1, 2, 3)):
        b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, z - 1.0, z + 0.4, navy, skip=("bottom", "top"))
    cols = [x0 + 3.0 + 3.08 * k for k in range(13)]
    for i, cx in enumerate(cols):
        b.box_c(cx, y0 - 0.35, 1.0, 0.5, 0.3, top, concrete, skip=("bottom", "top"))
    for k in range(4):
        z = G + FLOOR_H * k
        us = [x0 + 4.5 + 3.08 * j for j in range(12)]
        b.windows("-y", y0, us, (z + 4.5,), 2.2, 6.5, sill_mat=concrete, frame_mat=concrete)
        b.windows("+y", y1, [x0 + 4.5 + 6.16 * j for j in range(6)], (z + 4.5,), 2.6, 6.5, sill_mat=concrete, frame_mat=concrete)
    # 1층 옆면 창(전에는 위 칸에만 있어 1층이 통째로 막힌 벽이었다)
    b.windows("-x", x0, (0.0,), (5.0, G + 4.5), 2.4, 5.0, sill_mat=concrete, frame_mat=concrete)
    b.windows("+x", x1, (0.0,), (5.0, G + 4.5), 2.4, 5.0, sill_mat=concrete, frame_mat=concrete)

    # 현관
    cx = 0.0
    b.panel("-y", y0, cx, 0.3, 8.0, DOOR_H, glass, offset=0.1)
    b.frame("-y", y0, cx, 0.3, 8.0, DOOR_H, trim, width=0.45, bottom=False)
    b.box(cx - 6.0, cx + 6.0, y0 - 3.0, y0, DOOR_H + 0.5, DOOR_H + 1.1, concrete)

    # 옥상 난간(전체 높이 90 안 — 계단실은 생략)
    b.railing([(x0 + 0.3, y0 + 0.3, top), (x1 - 0.3, y0 + 0.3, top), (x1 - 0.3, y1 - 0.3, top), (x0 + 0.3, y1 - 0.3, top)],
              2.4, concrete, post_gap=3.2, post=0.3, rail=0.3, closed=True)

    # 간판 — 🔴 정정: 세로 핀이 앞을 지나가 안 읽혔다. 핀 앞면(y0-0.6)보다 확실히 앞으로(board_depth 1.0,
    # 흰 판+남색 글씨로 대비), 폭 40/12≈3.3 이상.
    b.sign("구일고등학교", "-y", y0, cx, top - 6.0, "건물_색_흰", navy, size=4.0, pad=0.6, max_width=24.0, board_depth=1.0)
    return b


# ──────────────────────────────────────────────────────────── 07 메가스터디

def make_megastudy():
    """유리창 빼곡한 학원 빌딩 — 좁고 높은 몸통, 정면은 짙은 청회색 유리 커튼월 위주(창 격자를 그 위에
    얹는다), 옆·뒤는 흰 벽 일부. 정면 모서리에 여러 층 걸치는 세로 돌출 간판(blade_sign), 정면 상단에
    옥상 높이까지 닿는 큰 가로 간판. 현수막엔 짧은 문구. 1층은 유리 상가(편의점풍), 옥상엔 방수 바닥+
    설비. 뒤 1층에도 문·창."""
    b = Builder()
    white, concrete, glass, trim = "건물_외벽_흰", "건물_콘크리트", "건물_유리창", "건물_색_흰"
    curtain, metal, waterproof = "건물_유리_커튼월", "건물_금속_회색", "건물_옥상_방수"
    W, D = 22.0, 18.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H
    top = G + FLOOR_H * 4                                # 85

    b.box(x0 - 1.0, x1 + 1.0, y0 - 1.0, y1 + 1.0, 0.0, 0.3, "건물_바닥_보도블록", skip=("bottom",))
    # 🔴 -y(정면)를 통째로 빼면 1층 상가 유리 둘레(진열창 옆 여백)까지 뻥 뚫린다 — 1층은 흰 벽으로 두고
    # 그 위(G~top)만 -y를 빼서 커튼월 판으로 덮는다.
    b.box(x0, x1, y0, y1, 0.3, G, white, skip=("bottom", "top"))
    b.box(x0, x1, y0, y1, G, top, white, skip=("bottom", "top", "-y"))      # 옆·뒤만 흰 벽(2층 위)
    b.face(((x0, y0, G), (x1, y0, G), (x1, y0, top), (x0, y0, top)), curtain)  # 정면(1층 위)은 커튼월
    for z in (G,) + tuple(G + FLOOR_H * k for k in (1, 2, 3)):
        b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, z - 0.7, z + 0.1, concrete, skip=("bottom", "top"))

    # 1층 — 유리 상가(편의점풍), 전면 통유리 + 얇은 문틀
    b.panel("-y", y0, 0.0, 1.4, W - 3.0, G - 4.0, glass, offset=0.05)
    b.frame("-y", y0, 0.0, 1.4, W - 3.0, G - 4.0, metal, width=0.3, bottom=True)
    b.box(x0 + 1.0, x1 - 1.0, y0 - 0.1, y0, G - 4.5, G - 3.8, "건물_색_빨강")

    # 2~5층 — 커튼월 위에 촘촘한 창 격자(작은 창 + 창틀)
    us = [x0 + 2.2 + 2.6 * k for k in range(7)]
    for k in range(4):
        z = G + FLOOR_H * k
        b.windows("-y", y0, us, (z + 3.0, z + 8.5), 1.9, 3.6, frame_mat=trim)
        b.windows("+y", y1, us, (z + 3.0, z + 8.5), 1.9, 3.6, frame_mat=trim)
    # 옆면(+x) — 통유리 커튼월로 포인트
    # skip="-x" — 안쪽 면은 원래 벽(+x면, 흰색)과 거의 겹쳐 z-fighting을 낸다, 바깥쪽 면만 있으면 된다.
    b.box(x1 - 0.05, x1 + 0.05, y0 + 1.0, y1 - 1.0, G, top, curtain, skip=("bottom", "top", "-x"))
    b.windows("-x", x0, [y0 + 3.0 + 4.0 * k for k in range(4)], (G + 4.0,), 2.6, 5.5, frame_mat=trim)
    # 뒤(+y) 1층 — 직원용 문 + 작은 창(전에는 통째로 막힌 벽이었다)
    b.panel("+y", y1, -3.0, 0.3, 3.2, DOOR_H, glass, offset=0.08)
    b.frame("+y", y1, -3.0, 0.3, 3.2, DOOR_H, trim, width=0.3, bottom=False)
    b.windows("+y", y1, (4.0,), (5.0,), 2.4, 4.5, sill_mat=concrete, frame_mat=trim)

    # 층간 색 현수막 — 짧은 문구
    banner_z = G + FLOOR_H * 2
    b.sign("수능 설명회", "-y", y0, 0.0, banner_z, "건물_색_노랑", "건물_색_검정", size=1.6, pad=0.4,
           max_width=W - 5.0, board_depth=0.2)

    # 정면 상단 — 옥상 높이까지 닿는 큰 가로 간판 「메가스터디」(폭 24/12=2.0 이상 — 5~6 크게)
    b.sign("메가스터디", "-y", y0, 0.0, top - 9.0, "건물_색_흰", "건물_색_남색", size=5.5, pad=0.6, max_width=W - 2.0, board_depth=0.25)
    # 정면 모서리 — 여러 층에 걸친 세로 돌출 간판 「메\n가\n스\n터\n디」(공용 blade_sign, adc31ae1)
    b.blade_sign("메\n가\n스\n터\n디", x1 - 1.6, y0, G, "건물_색_흰", "건물_네온_분홍", size=2.6, pad=0.5)

    # 옥상 — 방수 바닥 + 물탱크 + 실외기
    b.face(((x0, y0, top), (x1, y0, top), (x1, y1, top), (x0, y1, top)), waterproof)
    b.cylinder(x1 - 4.0, 0.0, top, top + 4.0, 1.6, 12, "건물_금속_스테인리스", top_radius=1.4)
    b.box_c(x0 + 3.0, 0.0, 2.4, 1.8, top, 1.4, trim)
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

    # ── 화난 버전(2026-09-12, blender 세션이 덧붙임 — 위 기본판 make_* 는 한 줄도 안 고친다). 공용 bc.angry_variant:
    # brows = 눈으로 쓸 창 위 그을린 차양(바깥 끝 높게·안쪽 끝 낮게) · tilts = (side, plane, u0, u1, z0, z1, 각도, 최소 돌출)
    # — 최소 돌출보다 앞으로 나온 정점만 기울여 창틀(0.35)·창턱(0.6)은 안 딸려 온다 · cracks_at = 꼭 보여야 할 금 ·
    # extra = 건물다운 파손(정면에서 크게 보이는 자리, PM)과 연기 자리(평평한 옥상 면이 없거나 위치를 정할 때, smoke=0).
    ("Story03_한양영어유치원_화남", "03 English Kindergarten Angry",
     lambda: bc.angry_variant(make_hanyang_kindergarten, seed=1103, cracks=8, smoke=0,
                              # PM(여유분): 오른쪽 집 한쪽뿐이라 표정이 약했다 — 두 집 2층 창을 두 눈으로, 집 폭만큼 넓게
                              brows=[("-y", -8.0, (-13.3, 37.0), (-3.5, 34.0)), ("-y", -8.0, (17.0, 36.2), (4.0, 34.2))],
                              tilts=[("-y", -8.0, 2.5, 17.5, 36.4, 41.8, 12.0, 0.25)],
                              cracks_at=[("-y", -8.0, -6.5, 10.5, 3.0, 6.0)],
                              extra=lambda b: _angry_hanyang(b)),
     "한양영어유치원 화난 버전 · 두 집 2층 창 눈 + 집 폭 차양 눈썹 · 간판 기울어짐 · 현관을 막은 떨어진 각재 · 부러진 굴뚝"),
    ("Story04_구일초등학교_화남", "04 Guil Elementary Angry",
     lambda: bc.angry_variant(make_guil_elementary, seed=1104, cracks=10, smoke=0,
                              # PM(여유분): 양 끝 작은 조각이라 V자가 약했다 — 3층 창 줄 폭만큼 시계탑 가까이까지 넓게
                              brows=[("-y", 5.0, (-20.8, 54.6), (-5.0, 51.9)), ("-y", 5.0, (20.8, 54.6), (5.0, 51.9))],
                              tilts=[("-y", 5.0, -12.0, 12.0, 47.3, 53.0, 10.0, 0.45),
                                     ("-y", 5.2, -4.2, 4.2, 65.0, 73.4, 22.0, 0.01)],
                              cracks_at=[("-y", 5.0, -18.3, 3.0, 3.0, 7.0)],
                              extra=lambda b: _angry_elementary(b)),
     "구일초등학교 화난 버전 · 3층 양끝 창 눈 + 차양 눈썹 · 간판·시계판 기울어짐 · 운동장 철망 담장 한 구간 넘어짐"),
    ("Story05_구일중학교_화남", "05 Guil Middle Angry",
     lambda: bc.angry_variant(make_guil_middle, seed=1105, cracks=10, smoke=0,
                              brows=[("-y", -6.0, (-17.8, 69.3), (-4.5, 66.3)), ("-y", -6.0, (19.2, 69.3), (6.0, 66.3))],
                              tilts=[("-y", -6.0, -15.0, 4.5, 57.3, 63.5, -12.0, 0.61)],
                              cracks_at=[("-y", -6.0, 14.5, 12.5, 5.0, 7.0)],
                              extra=lambda b: _angry_middle(b)),
     "구일중학교 화난 버전 · 4층 양쪽 창 눈 + 차양 눈썹 · 간판 글자 미끄러짐 · 벽돌 떨어진 구멍과 더미 · 찢겨 떨어진 깃발"),
    ("Story06_구일고등학교_화남", "06 Guil High Angry",
     lambda: bc.angry_variant(make_guil_high, seed=1106, cracks=10, smoke=0,
                              brows=[("-y", -6.5, (-20.5, 70.2), (-6.5, 67.2)), ("-y", -6.5, (20.5, 70.2), (6.5, 67.2))],
                              tilts=[("-y", -6.5, -14.0, 14.0, 78.8, 85.0, 8.0, 0.65)],
                              cracks_at=[("-y", -6.5, -12.4, 6.0, 1.8, 7.0), ("-y", -6.5, 9.2, 10.0, 1.8, 8.0)],
                              extra=lambda b: _angry_high(b)),
     "구일고등학교 화난 버전 · 4층 양쪽 창 눈 + 차양 눈썹(남색 띠 높이) · 간판 기울어짐 · 떨어져 매달린 남색 띠 · 부러진 기둥 핀"),
    ("Story07_메가스터디_화남", "07 Megastudy Angry",
     lambda: bc.angry_variant(make_megastudy, seed=1107, cracks=6,
                              brows=[("-y", -9.0, (-10.8, 69.8), (-3.5, 68.0)), ("-y", -9.0, (10.8, 69.8), (3.5, 68.0))],
                              tilts=[("-y", -9.0, -9.5, 9.5, 54.8, 57.6, 10.0, 0.1),
                                     ("-y", -9.0, 9.0, 9.8, 25.0, 45.0, -14.0, 0.4)],
                              cracks_at=[("-y", -9.0, -5.0, 4.0, 5.0, 8.0), ("-y", -9.0, 4.5, 8.0, 4.0, 7.0)],
                              extra=lambda b: _angry_megastudy(b)),
     "메가스터디 화난 버전 · 4층 창 눈 + 차양 눈썹 · 찢겨 늘어진 현수막 · 기운 세로 간판 · 금 간 1층 유리 · 떨어진 간판 조각"),
]


# ──────────────────────────────────────────────────────────── 화난 버전 — 건물다운 파손(정면에서 크게)

def _angry_hanyang(b):
    """① 오른쪽 집 반목조 각재 하나가 떨어져 현관 앞을 비스듬히 막음 ② 굴뚝 머리가 부러져 지붕에 걸침 ③ 연기 자리."""
    from mathutils import Vector
    b.beam(Vector((14.5, -8.4, 20.0)), Vector((11.0, -9.6, 1.0)), 0.8, 0.4, "건물_나무_판")
    b.beam(Vector((13.8, 1.5, 46.2)), Vector((16.8, 2.8, 45.0)), 1.6, 1.6, "건물_벽돌_붉은")
    b.markers += [("연기_자리_01", Vector((16.0, 0.0, 52.5))), ("연기_자리_02", Vector((-6.5, 0.0, 46.5)))]


def _angry_elementary(b):
    """① 운동장 앞 철망 담장 왼쪽 두 칸이 쓰러져 흙바닥에 누움(서 있던 판은 지운다) ② 연기 자리(옥상)."""
    import bmesh
    from mathutils import Vector
    mesh_i = b.m("건물_철망_잎카드")
    fy = 5.0 - 21.8
    doomed = [f for f in b.bm.faces if f.material_index == mesh_i and -18.5 < f.calc_center_median().x < -9.5]
    bmesh.ops.delete(b.bm, geom=doomed, context="FACES")
    for x, skew in ((-16.0, 0.4), (-12.0, -0.3)):
        b.face(((x - 1.6, fy + 0.3, 0.25), (x + 1.6, fy + 0.3 + skew, 0.25),
                (x + 1.6, fy + 3.2 + skew, 0.3), (x - 1.6, fy + 3.2, 0.3)), "건물_철망_잎카드")
    b.markers += [("연기_자리_01", Vector((-10.0, 10.5, 55.5))), ("연기_자리_02", Vector((12.0, 11.5, 55.5)))]


def _angry_middle(b):
    """① 1층 정면 벽돌이 떨어져 나간 검은 구멍 + 둘레 금 + 앞 바닥 벽돌 더미 ② 가운데 태극기에 그을음,
    찢겨 떨어진 깃발 한 장이 마당에 ③ 연기 자리(A·B 날개 옥상)."""
    import random
    from mathutils import Vector
    rng = random.Random(1105)
    b.panel("-y", -6.0, 3.0, 13.5, 3.6, 4.6, "건물_색_검정", offset=0.06)
    b.panel("-y", -6.0, 3.0, 12.0, 6.5, 8.0, "건물_금_잎카드", offset=0.15)
    for _ in range(9):
        p = Vector((3.0 + rng.uniform(-2.8, 2.8), -7.2 - rng.uniform(0.0, 2.2), 0.45 + rng.uniform(0.0, 0.6)))
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.3, 0.3))).normalized() * rng.uniform(0.9, 1.6)
        b.beam(p, p + d, 0.7, 0.55, "건물_벽돌_붉은")
    b.panel("-y", -16.5, 3.7, 24.0, 3.2, 4.7, "건물_그을음_잎카드", offset=0.05)
    b.face(((1.3, -21.6, 0.35), (5.8, -21.2, 0.35), (5.5, -18.0, 0.35), (1.0, -18.5, 0.35)), "건물_국기_태극기")
    b.markers += [("연기_자리_01", Vector((0.0, 0.0, 70.5))), ("연기_자리_02", Vector((13.7, 12.0, 70.5)))]


def _angry_high(b):
    """① 오른쪽 남색 띠 한 토막이 떨어져 벽에 비스듬히 매달림 ② 기둥 핀 하나가 부러져 앞 바닥에 누움 ③ 연기 자리."""
    from mathutils import Vector
    b.beam(Vector((8.0, -7.1, 54.6)), Vector((17.0, -7.9, 46.5)), 0.5, 1.4, "건물_색_남색")
    b.beam(Vector((-10.5, -9.8, 0.55)), Vector((-3.0, -8.2, 0.55)), 1.0, 0.5, "건물_콘크리트")
    b.markers += [("연기_자리_01", Vector((-8.0, 0.0, 85.5))), ("연기_자리_02", Vector((9.0, 2.0, 85.5)))]


def _angry_megastudy(b):
    """① 떨어진 간판 조각 둘이 1층 앞 바닥에(현수막·세로 간판 기울기와 1층 유리 금은 tilts·cracks_at) ② 연기 자리는 옥상 자동."""
    from mathutils import Vector
    b.beam(Vector((-6.0, -11.5, 0.5)), Vector((-3.2, -10.6, 0.5)), 1.6, 0.3, "건물_색_남색")
    b.beam(Vector((2.0, -11.8, 0.5)), Vector((4.2, -12.4, 0.5)), 1.4, 0.3, "건물_색_흰")


if __name__ == "__main__":
    bc.run(CATALOG)
