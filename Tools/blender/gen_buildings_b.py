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
    """PC방 상가 3층 — 1층 롤셔터 상가(문 하나는 유리문), 2층 PC방(유리창 띠 + 네온 간판 「사이버넷」 +
    옆 세로 돌출 간판 「PC」), 3층 사무실, 얇은 옥상 파라펫. blender 지시로 파라펫은 얇게(2~3)만 —
    처음 설계표의 두꺼운 파라펫(10)은 상가 건물엔 안 어울린다."""
    b = Builder()
    white, tile, glass = "건물_외벽_흰", "건물_타일_베이지", "건물_유리창"
    shutter, concrete, metal = "건물_셔터", "건물_콘크리트", "건물_금속_회색"
    W, D = 32.0, 22.0
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    G = GROUND_H                                       # 25
    f2 = G + FLOOR_H                                   # 40
    f3 = f2 + FLOOR_H                                   # 55
    top = f3 + 3.0                                      # 58 — 얇은 파라펫

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 1층 몸통(옆·뒤는 흰 벽, 앞은 아래서 따로 채운다)
    b.box(x0, x1, y0, y1, 0.3, G, white, skip=("bottom", "top", "-y"))
    # 앞면 — 셔터 4칸 + 유리문 1칸(가운데), 전부 DOOR_H(사람이 드나드는 상가 입구)
    bay_w = 5.0
    for u in (-12.0, -6.0, 0.0, 6.0, 12.0):
        mat = glass if u == 0.0 else shutter
        b.panel("-y", y0, u, 1.0, bay_w, DOOR_H, mat)
    # 1층 상단 타일 몰딩 띠(전체 둘레)
    b.box(x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, G - 2.0, G, tile, skip=("bottom", "top"))

    # 2층 PC방 — 벽 + 유리창 띠 + 네온 간판
    b.box(x0, x1, y0, y1, G, f2, white, skip=("bottom", "top", "-y"))
    b.box(x0, x1, y0, y1, G, f2, white, skip=("bottom", "top", "+y", "-x", "+x"))
    b.windows("-y", y0, (-12.0, -6.0, 0.0, 6.0, 12.0), (32.0,), 4.0, 6.0, mat=glass)
    b.sign("사이버넷", "-y", y0, 0.0, 26.0, "건물_색_검정", "건물_네온_분홍", size=2.6, pad=0.7, max_width=16.0)
    # 「PC」 세로 돌출 간판 — 정면 오른쪽 옆(+x) 벽에 붙여, 정면에서 접근할 때 옆으로 삐져나와 보인다
    # (Builder에 벽면 밖으로 돌출하는 핀 사인 도구가 없어, sign()을 옆벽에 붙이는 것으로 근사했다).
    b.sign("P\nC", "+x", x1, y0 + 2.0, G + 3.0, "건물_색_검정", "건물_네온_분홍", size=1.8, pad=0.5, max_width=2.6)

    # 3층 사무실 — 밋밋한 창
    b.box(x0, x1, y0, y1, f2, f3, white, skip=("bottom", "top", "-y"))
    b.box(x0, x1, y0, y1, f2, f3, white, skip=("bottom", "top", "+y", "-x", "+x"))
    b.windows("-y", y0, (-12.0, -6.0, 0.0, 6.0, 12.0), (f2 + 4.0,), 4.0, 7.0, mat=glass)

    # 옥상 — 얇은 파라펫 + 실외기 둘
    b.face(((x0, y0, f3), (x1, y0, f3), (x1, y1, f3), (x0, y1, f3)), "건물_옥상_방수")
    t = 0.5
    for bx0, bx1, by0, by1 in ((x0, x1, y0, y0 + t), (x0, x1, y1 - t, y1),
                               (x0, x0 + t, y0 + t, y1 - t), (x1 - t, x1, y0 + t, y1 - t)):
        b.box(bx0, bx1, by0, by1, f3, top, concrete, skip=("bottom", "top"))
    for ux in (-10.0, -4.0):
        b.box_c(ux, 6.0, 2.6, 1.4, f3, 2.0, metal, skip=("bottom",))
    return b


# ──────────────────────────────────────────────────────────── 09 7탄약창

def make_ammo_depot():
    """반원형 탄약고(퀀셋) — 폭 30(반지름 15)이 땅에 바로 앉아 자연스러운 높이 ~15.3만 나온다(blender
    지시대로 억지로 안 키운다). 규격 높이(40~90)는 별도의 **감시탑(망루)**이 담당한다 — 기둥 넷 위에
    작은 초소, 뒤쪽에 사다리. 위병소 문만 DOOR_H, 정문엔 차단봉(경고색 주홍은 여기 하나만), 담장 위에는
    윤형 철조망 카드, 담장 자체는 철망 카드 — 전부 나무 「잎카드」와 같은 요령(카드 한 장이 다발/그물
    텍스처를 통째로 담는다)."""
    b = Builder()
    corrugated, concrete = "건물_금속_골함석", "건물_콘크리트"
    wire_fence, razor_wire = "건물_철망_잎카드", "건물_철조망_잎카드"
    metal, glass = "건물_금속_회색", "건물_유리창"
    white, black, warn = "건물_색_흰", "건물_색_검정", "건물_도장_주홍"
    x0, x1, y0, y1 = -20.0, 20.0, -18.0, 18.0            # 담장 안쪽(40×36, 45×45 안)

    # 바닥판
    b.box(x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, 0.0, 0.3, concrete, skip=("bottom",))

    # 반원형 탄약고 — x축을 따라 눕는다(폭 30 = y축 방향 지름), 마당 뒤쪽 절반에 자리
    dx0, dx1, radius = -14.0, 14.0, 15.0
    b.half_cylinder(dx0, dx1, 0.0, radius, 0.3, 10, corrugated, end_mat=corrugated)
    # 출입구 — 반원통의 평평한 끝면(−x쪽, "−x" 벽으로 취급할 수 있다)에 셔터 문을 붙인다.
    b.panel("-x", dx0, 0.0, 0.3, 6.0, 8.0, "건물_셔터", offset=0.1)

    # 감시탑(망루) — 기둥 넷 + 초소 + 사다리, 총고 ~42로 규격 높이를 이 구조물이 담당한다
    tx, ty = -16.0, -12.0
    cabin_z = 38.0
    for px in (tx - 2.0, tx + 2.0):
        for py in (ty - 2.0, ty + 2.0):
            b.box_c(px, py, 0.6, 0.6, 0.3, cabin_z, metal, skip=("bottom",))
    b.box_c(tx, ty, 5.0, 5.0, cabin_z, 4.0, white, skip=("bottom",))
    b.box_c(tx, ty, 5.4, 5.4, cabin_z + 4.0, 0.5, concrete)             # 지붕 캡, 총고 ≈42.5
    b.windows("-y", ty - 2.5, (tx,), (cabin_z + 1.0,), 2.0, 2.0, mat=glass)
    for dx in (-0.8, 0.8):                                              # 사다리 — 뒤쪽(+y)에 붙는다
        b.beam((tx + dx, ty + 2.4, 0.3), (tx + dx, ty + 2.4, cabin_z), 0.25, 0.25, metal)
    rungs = 12
    for k in range(1, rungs):
        z = 0.3 + (cabin_z - 0.3) * k / rungs
        b.beam((tx - 0.8, ty + 2.4, z), (tx + 0.8, ty + 2.4, z), 0.2, 0.2, metal)

    # 위병소 — 사람이 드나드는 문이라 DOOR_H
    gx, gy = 9.0, -15.5
    b.box_c(gx, gy, 4.0, 4.0, 0.3, 25.0, concrete, skip=("bottom",))
    b.panel("-y", gy - 2.0, gx, 0.3, 2.2, DOOR_H, glass)
    b.sign("7탄약창", "-y", gy - 2.0, gx, 23.0, white, black, size=1.8, pad=0.5, max_width=6.5)
    # 경고 띠 — 위병소 문 위, 주홍은 여기 하나뿐이다(blender 지시)
    b.box(gx - 2.2, gx + 2.2, gy - 2.05, gy - 1.95, 22.4, 22.9, warn, skip=("bottom", "top"))

    # 정문 — 기둥 둘 + 차단봉(주홍·검정 대신 그냥 주홍 — 유일한 경고색 용처)
    for px in (-4.0, 4.0):
        b.box_c(px, y0, 0.8, 0.8, 0.3, 6.0, concrete, skip=("bottom",))
    b.beam((-4.0, y0, 4.0), (4.0, y0, 4.0), 0.25, 0.15, warn)
    b.box_c(-4.0, y0 + 0.6, 1.0, 1.0, 0.3, 3.0, metal, skip=("bottom",))    # 차단기 조작함

    # 담장 — 철망(높이 7) + 그 위 윤형철조망(카드, 높이 2). 정문 폭(−5~5)만 비운다.
    def fence_run(side, plane, u0, u1, gap=None):
        u = u0
        while u < u1:
            w = min(4.0, u1 - u)
            center = u + w / 2
            if not (gap and gap[0] < center < gap[1]):
                b.panel(side, plane, center, 0.3, w, 7.0, wire_fence)
                b.panel(side, plane, center, 7.3, w, 2.0, razor_wire)
            u += w

    fence_run("-y", y0, x0, x1, gap=(-5.0, 5.0))
    fence_run("+y", y1, x0, x1)
    fence_run("-x", x0, y0, y1)
    fence_run("+x", x1, y0, y1)
    return b


# ──────────────────────────────────────────────────────────── 목록
#
# (파일 이름, 이름표(영어 — 창 글꼴에서 한글이 깨진다), 만들기, 설명). 파일 이름은 유니티가 참조한다.

CATALOG = [
    ("Story08_사이버넷", "08 Cybernet", make_cybernet,
     "PC방 상가 3층 · 1층 셔터+유리문 · 2층 네온 간판 사이버넷+세로 PC 간판 · 3층 사무실 · 얇은 파라펫"),
    ("Story09_7탄약창", "09 Ammo Depot", make_ammo_depot,
     "반원형 탄약고(퀀셋) · 감시탑(기둥+초소+사다리) · 위병소(DOOR_H) · 정문 차단봉 · 윤형철조망 담장"),
]


if __name__ == "__main__":
    bc.run(CATALOG)
