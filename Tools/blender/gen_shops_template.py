"""레인 상점 — 가게 스크립트 틀(구현담당1용, blender 세션 제공 2026-09-13).

이 파일을 자기 스크래치로 복사해서 쓴다(예: gen_shops_impl1.py). 공통부 shops_common.py는 고치지 말고 가져다 쓴다
— 세트 뼈대(frame)·박공 간판(gable_sign)·지붕 색표(ROOF_TONES)·크기 assert·굽기·내보내기가 다 들어 있다.

화면 없이(정본):  blender --background --factory-startup --python gen_shops_impl1.py [-- 상점_도움소]
검사:            blender --background --factory-startup --python <blender 스크래치>/check_scratch.py -- structures/상점_도움소.fbx
                 → 뒷면 경고 0건, 최상위 원점 0
창에 올리기(MCP): shops_common.build_in_window(이름, make_함수, bpy.data.collections["판_구조물"], <창 전용 텍스처 폴더>)
                 뒤 obj.location = (판_구조물.x + 슬롯 x, 판_구조물.y + 3.6, 0). 창에서는 절대 내보내지 않는다(이름 밀림).
                 다시 올릴 때 지우기는 그 물체와 children_recursive만 — 이름으로 지우면 남의 빈 오브젝트까지 지워진다.

규칙 요약(PM):
- 바닥 9×9·높이 12~16·삼각형 2,500·재질 이름 `상점_<가게>_<종류>`·빈 오브젝트 이름은 run/assemble이 assert로 막는다.
- 간판은 박공 팔각 판 위 그림 기호, 글자 금지. 판과 기호는 밝기 차이를 크게(짙은 판 위 짙은 기호는 게임 시점에서 안 읽힘).
- 지붕 색은 ROOF_TONES가 가게 이름으로 자동으로 고른다 — 재질 이름을 `상점_도움소_지붕`처럼만 쓰면 된다.
- 빛나는 것(결정·창 불빛)은 종류 이름 끝 `_발광`. 불·연기는 b.marker("불_자리_01", (x, y, z))만, 메시 금지.
- 보이는 면을 skip하지 말 것(유니티는 뒷면 안 그림). 매달린 판은 두께 있는 상자로.
- 정면 소품은 y ≥ −4.5(벽 앞 1.4 안).
"""
import os
import sys

COMMON = "/private/tmp/claude-501/-Users-sang-Documents-GitHub-GuilRandomDefense/35bf77f2-ede8-47ff-bc3e-068043a35f39/scratchpad/shops"
if COMMON not in sys.path:
    sys.path.insert(0, COMMON)

from shops_common import (PLINTH, SIGN_Z, WX, WY, Builder, _mix, _noise, frame, gable_sign,  # noqa: E402
                          register_shader, run, window)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "structures")


# 새 재질 종류가 필요하면 이렇게 등록한다(색은 선형값). 돌려주는 값 = (색 소켓, 발광 소켓 또는 None)
@register_shader("약병_발광")
def _potion(nt, c, name):
    color = _mix(nt, _noise(nt, c.co, 20.0), (0.05, 0.35, 0.12, 1), (0.2, 0.7, 0.3, 1))
    return color, color


def make_helpshop():
    """예시 — 도움소 뼈대만(실제 디자인은 PM 표: 약방·물약 가게, 창가 병 선반, 약초 다발, 지붕 초록 유약 기와)."""
    b = Builder()
    P = "상점_도움소_"
    wall, timber, stone, roof = P + "흰회벽", P + "목재", P + "석재", P + "지붕"
    dark, board, iron, potion = P + "어둠", P + "밝은목재", P + "쇠", P + "약병_발광"
    frame(b, wall, timber, stone, roof)             # 세트 공통 뼈대 — 그대로
    y = -WY
    # 입구(예시): 가운데 문
    b.box(-1.2, 1.2, y - 0.12, y, PLINTH, PLINTH + 5.0, timber, skip=("+y",))
    window(b, -2.85, 3.6, 1.3, 2.4, timber, dark, shutter=timber)
    window(b, 2.85, 3.6, 1.3, 2.4, timber, dark, shutter=timber)
    # 박공 간판(예시): 밝은 판 위 물약병 모양
    front = gable_sign(b, timber, board)
    b.cylinder((0.0, front - 0.25, SIGN_Z - 0.2), (0, -1, 0), 0.45, 0.2, potion, sides=10)
    b.box_c(0.0, front - 0.12, 0.3, 0.2, SIGN_Z + 0.25, 0.5, potion)
    return b


SHOPS = {"상점_도움소": make_helpshop}

if __name__ == "__main__":
    run(SHOPS, OUT)
