"""희귀함·특별함 범위화 행(2026-09-30 구현담당1, PM 지시 — RARE_FILL_LIST §2 · SPECIAL_FILL_LIST §0-6).

stomp(AOws)·블리자드(ACbz)가 범위 능력인데 단일로 들어 있던 것을 그 능력의 aare 범위로. 능력 필드(aare·Wrs1·adur·Hbz1·Hbz2)를 다시 디코드해 확인했다. 다시 돌려도 같은 결과.
안 넣은 것: 연쇄 번개(키자루 A09A — 8마리, 튈 때마다 +10%: 연쇄 축 없음) · 부채꼴 칼날(마르코 A09Y·A09X — 최대 7마리 상한 축 없음) · 출처 없는 스턴이 얽힌 행(보고 대상) · 제프 A0MP.
값 2.0·8.0짜리 매 타 효과 둘(박수찬·박은석)은 능력의 「파 수」·「연쇄 대상 수」 필드를 피해로 읽은 찌꺼기라 비웠다.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402
from apply_immortal_blocks import retarget, dmg  # noqa: E402

ENEMIES = 2
AP, SPELLS = 2, 7
TAG = '[범위화 09-30]'


def note(asset, text):
    m = re.search(r'^  description: (.*)$', asset.head, re.M)
    old = m.group(1) if m else ''
    if TAG in old:
        return False
    add = ' %s %s' % (TAG, text)
    new = old[:-1] + add.replace("'", "''") + "'" if old.startswith("'") and old.endswith("'") else sat.yaml_scalar(old + add)
    if m:
        asset.head = asset.head.replace(m.group(0), '  description: ' + new)
    else:
        asset.head = re.sub(r'^(  skillName: .*\n)', lambda k: k.group(1) + '  description: ' + new + '\n', asset.head, count=1, flags=re.M)
    return True


def main():
    changed = []

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_rare_special_ranges.py)'):
            fn(a)
            sat.save(a)
            changed.append(a.path)

    def area(radius, **extra):
        def fn(a):
            sat.set_level_field(a, 0, 'range', float(radius))
            for k, v in extra.items():
                sat.set_level_field(a, 0, k, v)
            sat.set_effects(a, 0, [retarget(b, target=ENEMIES) for b in sat.get_effect_blocks(a, 0)])
        return fn

    edit('SkillData_더미채널_희귀함_이용민_1', '바제스 stomp A0K2는 500 범위 12000·스턴 0.9초 — 단일이었다. ', area(500))
    edit('SkillData_더미채널_희귀함_이재윤_1', '이완코브 stomp A0R8은 500 범위 13000·스턴 1.2초 — 단일이었다. ', area(500))
    edit('SkillData_더미채널_희귀함_윤현모_1', '쿠잔 stomp A0PX는 400 범위 9000·스턴 0.95초 — 단일이었다. ', area(400))
    edit('SkillData_더미채널_희귀함_유재헌_1', '우솝 stomp A0EL은 485 범위 7000·스턴 1.15초 — 단일이었다. ', area(485))

    def blizzard(a):
        sat.set_level_field(a, 0, 'range', 850.0)
        sat.set_effects(a, 0, [retarget(b, target=ENEMIES, hitCount=2, duration=1.0) for b in sat.get_effect_blocks(a, 0)])
    edit('SkillData_더미채널_희귀함_박수찬_1', '아카이누 블리자드 A0AX(ACbz, Hbz1 2파 · Hbz2 6000 · aare 850): 대상 중심 850 범위 6000 2회 — 단일 1회였다. ', blizzard)
    edit('SkillData_원작능력_희귀함_박수찬', '값 2.0은 A0AX의 파 수(Hbz1)를 피해로 읽은 찌꺼기 — 매 타 효과를 비움. ', lambda a: sat.set_effects(a, 0, []))
    edit('SkillData_원작능력_희귀함_박은석', '값 8.0은 연쇄 번개 A09A의 대상 수(Ocl2)를 피해로 읽은 찌꺼기 — 매 타 효과를 비움(연쇄 자체는 축이 없어 단일 6000 그대로). ', lambda a: sat.set_effects(a, 0, []))

    # 희귀 조로 h02F Unique33 1/15: 대상 50000 + 스턴 2.0 + 375 범위 16000
    def zoro(a):
        sat.set_level_field(a, 0, 'range', 375.0)
        sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + [dmg(ENEMIES, AP, SPELLS, 16000.0)])
    edit('SkillData_더미채널_희귀함_정내연_1', '조로 Unique33 1/15: 대상 중심 375 범위 16000 추가(대상 50000·스턴 2.0초는 있던 것). ', zoro)

    # 특별 조로 h00W Speical12: 1/7 · 대상 중심 400 범위 1000
    edit('SkillData_원작능력_특별함_최상호', '조로 Speical12: 1/7 · 대상 중심 400 범위 1000 — 확률 0.1·단일이었다. ', area(400, triggerChance=round(1.0 / 7.0, 6)))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
