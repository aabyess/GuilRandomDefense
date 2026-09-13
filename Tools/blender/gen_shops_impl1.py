"""레인 상점 — 구현담당1 몫: 영원강화소·공격타입강화소·도움소. 공통부는 blender 세션의 shops_common.py.

화면 없이(정본):  blender --background --factory-startup --python gen_shops_impl1.py [-- 상점_도움소 ...]
검사:            blender --background --factory-startup --python <blender 스크래치>/check_scratch.py -- structures/<이름>.fbx
결과: 이 폴더/structures/<이름>.fbx, structures/Textures/<재질>.png
"""
import math
import os
import sys

COMMON = "/private/tmp/claude-501/-Users-sang-Documents-GitHub-GuilRandomDefense/35bf77f2-ede8-47ff-bc3e-068043a35f39/scratchpad/shops"
if COMMON not in sys.path:
    sys.path.insert(0, COMMON)

from shops_common import (PLINTH, SIGN_Z, WX, WY, Builder, _bricks, _math, _mix, _noise, frame,  # noqa: E402
                          gable_sign, register_shader, run, window)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "structures")


# ──────────────────────────────────────────────────────────── 새 재질 종류

@register_shader("결정_발광")
def _crystal(nt, c, name):
    """영원강화소 — 확실한 파랑 결정(2026-09-13 blender 정정: 이전 판은 흰빛에 가까워 발광이 아니라 흰 조각으로
    보였다). 안쪽일수록 짙은 남색, 능선(노이즈 능선)일수록 밝은 하늘빛으로."""
    core = _mix(nt, _noise(nt, c.co, 14.0, detail=6.0), (0.01, 0.05, 0.25, 1), (0.03, 0.14, 0.5, 1))
    color = _mix(nt, _noise(nt, c.co, 3.0, detail=2.0), core, (0.05, 0.32, 0.82, 1))
    return color, color


@register_shader("사당석", big=True)
def _shrine_stone(nt, c, name):
    """영원강화소 — 밝게 다듬은 정연한 석재(2026-09-13 blender 정정: 이전엔 강화소·공격타입강화소와 같은
    막돌(돌벽)이라 세 채가 똑같아 보였다). 줄눈을 가늘고 밝게 둬 다듬은 돌 느낌을 낸다."""
    br = _bricks(nt, c.wall, 3.0, 0.01, (0.35, 0.33, 0.3, 1), (0.5, 0.47, 0.42, 1), row=0.5, width=1.0)
    br.inputs["Mortar"].default_value = (0.24, 0.22, 0.2, 1)
    color = _mix(nt, _math(nt, "MULTIPLY", _noise(nt, c.co, 40.0), 0.25), br.outputs["Color"], (0.32, 0.3, 0.27, 1))
    return _mix(nt, _math(nt, "MULTIPLY", c.low, 0.6), color, (0.2, 0.18, 0.16, 1)), None


@register_shader("무기고벽돌", big=True)
def _armory_brick(nt, c, name):
    """공격타입강화소 — 짙은 적갈 벽돌 + 밝은 줄눈(2026-09-13 blender 정정: 영원강화소와 같은 돌벽을 갈라내려고
    새로 등록. 새 지붕(녹슨 적갈 흑)과 톤이 겹치지 않게 벽돌을 지붕보다 한 단 밝은 적갈로 잡았다)."""
    br = _bricks(nt, c.wall, 14.0, 0.025, (0.2, 0.065, 0.045, 1), (0.3, 0.1, 0.06, 1))
    br.inputs["Mortar"].default_value = (0.24, 0.22, 0.19, 1)
    soot = _math(nt, "MULTIPLY", _noise(nt, c.co, 10.0), 0.3)
    return _mix(nt, soot, br.outputs["Color"], (0.035, 0.025, 0.02, 1)), None


@register_shader("칼날")
def _blade(nt, c, name):
    """공격타입강화소 무기 — 담금질된 강철. 쇠보다 밝고 푸른 기가 있어 밝은 나무 간판 위에서도 또렷하다."""
    return _mix(nt, _noise(nt, c.co, 30.0), (0.38, 0.4, 0.44, 1), (0.58, 0.6, 0.64, 1)), None


@register_shader("말린풀")
def _dried_herb(nt, c, name):
    """도움소 처마 밑 약초 다발 — 마른 올리브빛 풀."""
    return _mix(nt, _noise(nt, c.co, 22.0), (0.1, 0.11, 0.035, 1), (0.22, 0.24, 0.08, 1)), None


@register_shader("약병_발광")
def _potion(nt, c, name):
    color = _mix(nt, _noise(nt, c.co, 20.0), (0.05, 0.35, 0.12, 1), (0.2, 0.7, 0.3, 1))
    return color, color


def shard(b, cx, cy, cz, height, radius, mat, sides=6, lean=(0.0, 0.0), spin=0.0):
    """결정 조각 하나 — 육각 밑면에서 한 점으로 모이는 뾰족 기둥. lean으로 옆으로 살짝 기울여 다발처럼 보이게."""
    base = [(cx + radius * math.cos(spin + math.tau * i / sides), cy + radius * math.sin(spin + math.tau * i / sides), cz)
            for i in range(sides)]
    apex = (cx + lean[0], cy + lean[1], cz + height)
    b.spike(base, apex, mat)


def sword(b, cx, cz, length, angle_deg, blade, hilt, y=None, guard_w=0.34, bw=0.11):
    """벽에 붙는 검 하나 — 두께 있는 쐐기 날(spike) + 십자 날밑 + 자루. angle_deg=0이면 칼끝이 위.
    bw = 칼날 너비 절반(2026-09-13 blender 정정: 판을 채우도록 커진 간판 검·무기걸이 검에 맞춰 뺐다)."""
    y = -WY if y is None else y
    a = math.radians(angle_deg)
    dx, dz = math.sin(a), math.cos(a)
    nx, nz = dz, -dx                                    # 날 진행 방향에 수직(칼날 너비 방향)
    hilt_len = length * 0.28
    blade_len = length - hilt_len
    tip = (cx + dx * blade_len, y, cz + dz * blade_len)
    base_pts = [(cx - nx * bw, y - 0.035, cz - nz * bw), (cx + nx * bw, y - 0.035, cz + nz * bw),
                (cx + nx * bw, y + 0.035, cz + nz * bw), (cx - nx * bw, y + 0.035, cz - nz * bw)]
    b.spike(base_pts, tip, blade)
    b.box_c(cx, y, guard_w, 0.09, cz - 0.045, 0.09, hilt)
    b.cylinder((cx - dx * hilt_len * 0.5, y, cz - dz * hilt_len * 0.5), (-dx, 0, -dz), 0.05, hilt_len, hilt, sides=6)
    b.cylinder((cx - dx * (hilt_len + 0.04), y, cz - dz * (hilt_len + 0.04)), (0, 1, 0), 0.09, 0.05, hilt, sides=6)


# ──────────────────────────────────────────────────────────── 영원강화소

def make_eternal():
    """영원강화소 — 작은 석조 사당(2026-09-13 blender 정정 반영). 다듬은 사당석 벽 + 목재 트림, 문 양옆
    돌기둥 + 돌계단, 창은 돌 창틀(덧문 없음). 문 위 크게 키운 파랑 결정 다발이 박공 띠 바로 아래까지 벽을
    채운다. 간판도 같은 결정 다발(1.5배). 지붕은 ROOF_TONES의 이끼 낀 녹청 동판(공용에서 파랑에 맞춰 조정됨)."""
    b = Builder()
    P = "상점_영원강화소_"
    wall, timber, stone, roof = P + "사당석", P + "목재", P + "석재", P + "지붕"
    dark, board, crystal = P + "어둠", P + "밝은목재", P + "결정_발광"
    frame(b, wall, timber, stone, roof)
    y = -WY

    # 입구 — 돌 아치 느낌의 문틀(사당다운 두꺼운 석재 테두리)
    dw, dh = 2.0, 4.6
    b.box(-dw / 2, dw / 2, y - 0.04, y, PLINTH, PLINTH + dh, dark, skip=("+y",))
    b.box(-dw / 2 - 0.3, -dw / 2, y - 0.25, y, PLINTH, PLINTH + dh + 0.3, stone, skip=("+y",))
    b.box(dw / 2, dw / 2 + 0.3, y - 0.25, y, PLINTH, PLINTH + dh + 0.3, stone, skip=("+y",))
    b.box(-dw / 2 - 0.4, dw / 2 + 0.4, y - 0.35, y, PLINTH + dh, PLINTH + dh + 0.4, stone, skip=("+y",))
    # 창 — 나무 덧문을 걷어내고 돌 창틀로(blender: 덧문이 있으면 일반 집처럼 보인다)
    window(b, -2.85, 3.6, 1.1, 2.2, stone, dark)
    window(b, 2.85, 3.6, 1.1, 2.2, stone, dark)

    # 문 양옆 돌기둥(기단 위 높이 5) + 문 앞 돌계단 두 단
    for px in (-1.9, 1.9):
        b.box_c(px, y - 0.3, 0.5, 0.5, PLINTH, 5.0, stone, skip=("bottom",))
    b.box(-1.3, 1.3, -4.3, -3.5, 0.0, 0.3, stone, skip=("bottom",))
    b.box(-1.1, 1.1, -3.9, -3.5, 0.3, 0.55, stone, skip=("bottom",))

    # 문 인방 위 돌 받침 + 파랑 결정 다발(사당의 초점, 박공 띠 바로 아래까지 벽을 채운다)
    ped_y = y - 0.55
    ped_z = PLINTH + dh + 0.1
    b.box_c(0.0, ped_y, 2.2, 1.0, ped_z, 0.3, stone, skip=("bottom",))
    pz = ped_z + 0.3
    shard(b, 0.0, ped_y, pz, 3.0, 0.6, crystal, lean=(0.05, -0.03))
    shard(b, -0.9, ped_y + 0.08, pz, 1.9, 0.38, crystal, lean=(-0.35, 0.08), spin=0.6)
    shard(b, 0.85, ped_y - 0.08, pz, 2.1, 0.42, crystal, lean=(0.4, -0.08), spin=1.2)
    shard(b, 0.2, ped_y + 0.25, pz, 1.2, 0.28, crystal, lean=(0.1, 0.22), spin=2.0)

    # 박공 간판 — 밝은 나무 판 위 같은 결정 다발(1.5배로 키워 판을 채운다)
    front = gable_sign(b, board)
    b.box_c(0.0, front + 0.02, 0.55, 0.12, SIGN_Z - 0.55, 0.12, stone)
    shard(b, 0.0, front - 0.12, SIGN_Z - 0.4, 1.125, 0.26, crystal, lean=(0.0, 0.0))
    shard(b, -0.24, front - 0.08, SIGN_Z - 0.42, 0.75, 0.15, crystal, lean=(-0.12, 0.0), spin=0.6)
    shard(b, 0.22, front - 0.08, SIGN_Z - 0.42, 0.825, 0.15, crystal, lean=(0.12, 0.0), spin=1.2)
    return b


# ──────────────────────────────────────────────────────────── 공격타입강화소

def make_attack():
    """공격타입강화소 — 무기고(2026-09-13 blender 정정 반영). 짙은 적갈 벽돌 벽(영원강화소의 사당석·새
    지붕 톤과 안 겹치게 새로 등록) + 닫힌 쇠 덧문(못대가리·가로 쇠띠). 문 위 무기걸이 판에 방패+엇갈린 큰
    검 둘+세운 창 둘. 간판은 짙은 판(face=검정) 위 밝은 강철 검 둘 — 판과 기호 밝기 대비를 확실히 뒀다."""
    b = Builder()
    P = "상점_공격타입강화소_"
    wall, timber, stone, roof = P + "무기고벽돌", P + "목재", P + "석재", P + "지붕"
    iron, dark, board, blade = P + "쇠", P + "어둠", P + "밝은목재", P + "칼날"
    frame(b, wall, timber, stone, roof)
    y = -WY

    # 입구 — 쇠 덧댄 튼튼한 문
    dw, dh = 2.2, 4.8
    b.box(-dw / 2, dw / 2, y - 0.04, y, PLINTH, PLINTH + dh, dark, skip=("+y",))
    for z in (PLINTH + 0.7, PLINTH + dh - 0.9):
        b.box(-dw / 2 + 0.1, dw / 2 - 0.1, y - 0.1, y - 0.04, z, z + 0.22, iron, skip=("+y",))
    b.box(-dw / 2 - 0.32, -dw / 2, y - 0.28, y, PLINTH, PLINTH + dh + 0.32, timber, skip=("+y",))
    b.box(dw / 2, dw / 2 + 0.32, y - 0.28, y, PLINTH, PLINTH + dh + 0.32, timber, skip=("+y",))
    b.box(-dw / 2 - 0.42, dw / 2 + 0.42, y - 0.4, y, PLINTH + dh, PLINTH + dh + 0.4, timber, skip=("+y",))

    # 창 — 닫힌 쇠 덧문(blender: 열린 덧문 두 짝 대신 창을 통째로 덮는 쇠판 + 징 + 가로 띠)
    def iron_shutter(cx, z0, w, h):
        window(b, cx, z0, w, h, timber, dark)
        sw, sh = w + 0.14, h + 0.14
        b.box(cx - sw / 2, cx + sw / 2, y - 0.24, y - 0.18, z0 - 0.07, z0 + sh - 0.07, iron, skip=("+y",))
        for bz in (z0 + sh * 0.28, z0 + sh * 0.68):
            b.box(cx - sw / 2, cx + sw / 2, y - 0.27, y - 0.24, bz - 0.06, bz + 0.06, iron, skip=("+y",))
        for rx in (-sw * 0.28, sw * 0.28):
            for rz in (z0 - 0.07 + sh * 0.18, z0 - 0.07 + sh * 0.82):
                b.cylinder((cx + rx, y - 0.28, rz), (0, -1, 0), 0.05, 0.05, iron, sides=6)

    iron_shutter(-2.85, 3.6, 1.1, 2.2)
    iron_shutter(2.85, 3.6, 1.1, 2.2)

    # 정면 벽에 기대 걸린 창(spear) — 문 왼쪽, 자루는 나무·촉은 강철(굵기 0.1로 키움)
    spear_x, spear_y, spear_base = -3.15, y - 0.28, PLINTH
    b.cylinder((spear_x, spear_y, spear_base), (0, 0, 1), 0.1, 4.4, timber, sides=6)
    shard(b, spear_x, spear_y, spear_base + 4.4, 0.6, 0.18, blade, sides=4)
    b.box_c(spear_x, spear_y - 0.1, 0.08, 0.08, spear_base + 0.3, 0.5, iron, skip=("bottom",))

    # 문 위 무기걸이 판(짙은 목재, 3.5×1.6, z 6.2~8.3) — 방패 + 엇갈린 큰 검 둘 + 세운 창 둘
    rack_w, rz0, rz1 = 3.5, 6.2, 8.3
    rack_cz = (rz0 + rz1) / 2
    b.box(-rack_w / 2, rack_w / 2, y - 0.14, y - 0.08, rz0, rz1, dark, skip=("+y",))
    # 2026-09-13 정정 — 방패(반지름 0.7)와 교차 검이 거의 같은 깊이(y)에 있어 렌더에서 방패가 검 손잡이 쪽을
    # 가려 버렸다(자루 부분만 안 보임). 방패는 판에 더 붙이고 검은 확실히 더 앞으로 빼 깊이를 벌린다.
    b.cylinder((0.0, y - 0.15, rack_cz), (0, -1, 0), 0.7, 0.1, iron, sides=8)
    sword(b, -0.15, rz0 + 0.2, 2.2, 20.0, blade, iron, y=y - 0.42, guard_w=0.6, bw=0.22)
    sword(b, 0.15, rz0 + 0.2, 2.2, -20.0, blade, iron, y=y - 0.42, guard_w=0.6, bw=0.22)
    for px in (-rack_w / 2 - 0.55, rack_w / 2 + 0.55):
        b.cylinder((px, y - 0.26, PLINTH), (0, 0, 1), 0.1, rz1 - PLINTH, timber, sides=6)
        shard(b, px, y - 0.26, rz1, 0.9, 0.24, blade, sides=4)

    # 박공 간판 — 짙은 판(face=검정) 위 밝은 강철 검 둘, 판을 채우도록 크게
    front = gable_sign(b, board, P + "검정")
    sword(b, -0.18, SIGN_Z - 0.75, 2.2, 22.0, blade, iron, y=front - 0.1, guard_w=0.6, bw=0.22)
    sword(b, 0.18, SIGN_Z - 0.75, 2.2, -22.0, blade, iron, y=front - 0.1, guard_w=0.6, bw=0.22)
    return b


# ──────────────────────────────────────────────────────────── 도움소

def make_helpshop():
    """도움소 — 약방·물약 가게. 흰회벽 + 목재, 창가 병 선반 + 문 옆 매달린 약초 다발. 간판은 밝은 판 위 물약병."""
    b = Builder()
    P = "상점_도움소_"
    wall, timber, stone, roof = P + "흰회벽", P + "목재", P + "석재", P + "지붕"
    dark, board, potion = P + "어둠", P + "밝은목재", P + "약병_발광"
    frame(b, wall, timber, stone, roof)
    y = -WY

    # 입구 — 문 + 문틀(2026-09-13 blender 여유 항목: 판 한 장처럼 보이던 것에 틀+인방 추가)
    dw, dh = 2.4, 5.0
    b.box(-dw / 2, dw / 2, y - 0.12, y, PLINTH, PLINTH + dh, timber, skip=("+y",))
    ft = 0.22
    b.box(-dw / 2 - ft, -dw / 2, y - 0.24, y, PLINTH, PLINTH + dh + ft, timber, skip=("+y",))
    b.box(dw / 2, dw / 2 + ft, y - 0.24, y, PLINTH, PLINTH + dh + ft, timber, skip=("+y",))
    b.box(-dw / 2 - ft, dw / 2 + ft, y - 0.24, y, PLINTH + dh, PLINTH + dh + ft, timber, skip=("+y",))
    window(b, -2.85, 3.6, 1.3, 2.4, timber, dark, shutter=timber)
    window(b, 2.85, 3.6, 1.3, 2.4, timber, dark, shutter=timber)

    # 창가 병 선반 — 왼쪽 창 밑, 두께 있는 선반판 위에 크기 다른 약병 다섯
    shelf_cx, shelf_y, shelf_z = -2.85, y - 0.55, 3.15
    b.box(shelf_cx - 0.9, shelf_cx + 0.9, shelf_y - 0.45, y - 0.05, shelf_z, shelf_z + 0.1, timber)
    b.box_c(shelf_cx - 0.85, shelf_y - 0.2, 0.06, 0.5, PLINTH, shelf_z - PLINTH, timber, skip=("bottom",))
    b.box_c(shelf_cx + 0.85, shelf_y - 0.2, 0.06, 0.5, PLINTH, shelf_z - PLINTH, timber, skip=("bottom",))
    for i, (dx, h, r) in enumerate(((-0.6, 0.42, 0.11), (-0.3, 0.3, 0.09), (0.0, 0.5, 0.12), (0.32, 0.34, 0.1), (0.62, 0.4, 0.1))):
        bz = shelf_z + 0.1
        b.cylinder((shelf_cx + dx, shelf_y - 0.2, bz), (0, 0, 1), r, h, potion, sides=8)
        b.cylinder((shelf_cx + dx, shelf_y - 0.2, bz + h), (0, 0, 1), r * 0.45, h * 0.28, potion, sides=8)

    # 문 오른쪽 처마 밑에 매달린 약초 다발(2026-09-13 blender 여유 항목: 굵기 0.05는 게임 시점에서 거의 안
    # 보였다 — 굵기·가닥 수를 두세 배로)
    hx, hy, hz = 1.75, y - 0.35, PLINTH + 5.6
    b.cylinder((hx, hy, hz + 0.12), (0, 0, 1), 0.07, 0.16, timber, sides=6)
    for k in range(9):
        ang = math.tau * k / 9
        lean = (0.16 * math.cos(ang), 0.16 * math.sin(ang))
        shard(b, hx, hy, hz - 0.55, 0.75, 0.13, P + "말린풀", sides=3, lean=lean, spin=ang)
    b.cylinder((hx, hy, hz), (0, 0, 1), 0.12, 0.14, timber, sides=6)

    # 박공 간판 — 밝은 판 위 물약병(몸통 + 잘록한 목 + 마개)
    front = gable_sign(b, board)
    fy = front - 0.16
    b.cylinder((0.0, fy, SIGN_Z - 0.62), (0, -1, 0), 0.42, 0.16, potion, sides=10)
    b.cylinder((0.0, fy - 0.16, SIGN_Z - 0.5), (0, -1, 0), 0.55, 0.62, potion, sides=10)
    b.cylinder((0.0, fy - 0.16, SIGN_Z - 0.03), (0, -1, 0), 0.24, 0.22, potion, sides=10)
    b.box_c(0.0, fy - 0.28, 0.22, 0.16, SIGN_Z + 0.19, 0.14, timber)
    return b


SHOPS = {"상점_영원강화소": make_eternal, "상점_공격타입강화소": make_attack, "상점_도움소": make_helpshop}

if __name__ == "__main__":
    run(SHOPS, OUT)
