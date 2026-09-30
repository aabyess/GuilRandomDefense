"""구조 칸 뒤 점검에서 나온 빠진 조각 다섯(2026-09-30 구현담당1, SKILL_STRUCTURE_BACKLOG.md §5 「4번 움직이는 더미」).

「움직이는 더미」 27 트리거와 대응 유닛 전체의 대상 쪽 스택(Aegr·AIsr·A11S·AId1) 올리기를 원문과 값으로 대조해 남은 것 — 전부 war3map_new.j 본문:
  ① 키쿄우 kikoyou_hp(LIFE 50): 대상 피해 뒤 시전자 → 대상 방향으로 5걸음(175씩, 첫 중심 190) 나아가며 400 범위의 적마다 한 번
     1,500,000 HERO/NORMAL + 1,500,000 NORMAL/UNIVERSAL + AId1 +3 → 장풍 직선(길이 190 + 175×4 + 400 = 1290 · 반경 400)으로. 끝에 대상 스턴 3.0(더미 h0A5 파이어볼트 A134, adur·ahdu 3.0).
  ② 미나토 Minato_Q2(1/6): 500 범위 600,000과 같이 AIsr +2.
  ③ 캐럿 carrot_skill_2(1/9): 425 범위 325,000과 같이 A11S +1.
  ④ 제트 Z_skill_3(1/16): 장풍 말고도 대상이 PV 200이면 대상에 1,000,000(NORMAL/UNIVERSAL).
  ⑤ 부릉냐 BronyaMotar_E(1/5): 415 범위 피해의 난수 배율 1~1.5(RRD 다섯째 인자)가 빠져 있었다.
안 넣은 것: 뱀파이어 마나 115(강화 A0LA 갈래) · 이스즈 isz의 AIsr +25(ForGroup 밖에서 GetEnumUnit을 불러 실효 없음 — 원작 버그) ·
  모리아 Legend34Moria_Skill의 AIsr +10·800,000(세 번째 발동 + SAPPER 대상 + 피해 이벤트 대기 — 한 줄로 안 섬).
다시 돌려도 같은 결과(설명 꼬리표).
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

TAG = '[구조 뒤 09-30]'
ENEMIES, SINGLE = 2, 3
AD, AP, HERO, SPELLS = 1, 2, 4, 7
DAMAGE, STUN, ARMOR_BREAK, AISR, A11S = 0, 1, 2, 9, 10
PV_EQ = 2


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


def main(dry):
    changed = []

    def edit(name, text, fn):
        a = sat.load(name)
        if note(a, text + '(Tools/apply_structure_followups.py)'):
            fn(a)
            print('%-44s %s' % (name, text[:70]))
            if not dry:
                sat.save(a)
            changed.append(a.path)

    def add(*effects):
        return lambda a: sat.set_effects(a, 0, sat.get_effect_blocks(a, 0) + list(effects))

    line = dict(target=ENEMIES, lineLength=1290.0, lineStartRadius=400.0, lineEndRadius=400.0)
    edit('게이트_랜덤_이즈미_신이치_351a0f00', '① kikoyou_hp 직선(길이 1290 · 반경 400): 1500000 HERO + 1500000 UNIVERSAL + AId1 +3, 대상 스턴 3.0',
         add(sat.effect(kind=DAMAGE, damageType=AD, attackType=HERO, multiplier=1500000.0, **line),
             sat.effect(kind=DAMAGE, damageType=AP, attackType=SPELLS, multiplier=1500000.0, **line),
             sat.effect(kind=ARMOR_BREAK, multiplier=3.0, **line),
             sat.effect(kind=STUN, target=SINGLE, duration=3.0, heroDuration=3.0)))
    edit('게이트_랜덤_이타도리_유지_18aa2343', '② Minato_Q2: 500 범위 AIsr +2', add(sat.effect(kind=AISR, target=ENEMIES, multiplier=2.0)))
    edit('게이트_변화됨_강재규_5c20b80f', '③ carrot_skill_2: 425 범위 A11S +1', add(sat.effect(kind=A11S, target=ENEMIES, multiplier=1.0)))
    edit('더미채널_불멸_박은석_79행_00625', '④ Z_skill_3: 대상이 PV 200이면 대상 1000000',
         add(sat.effect(kind=DAMAGE, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=1000000.0, targetCondition=PV_EQ, targetConditionValue=200.0)))

    def bronya(a):
        from apply_line_skills import setf
        blocks = sat.get_effect_blocks(a, 0)
        hit = [i for i, b in enumerate(blocks) if re.search(r'^      bonus: 625000(\.0)?$', b, re.M)]
        assert len(hit) == 1, hit
        blocks[hit[0]] = setf(blocks[hit[0]], 'randMax', 1.5)
        sat.set_effects(a, 0, blocks)
    edit('게이트_랜덤_호시노_아이_33bd8aa4', '⑤ BronyaMotar_E: 415 범위 피해 난수 배율 1~1.5', bronya)
    print('바꾼 에셋 %d' % len(changed))


if __name__ == '__main__':
    main('--dry' in sys.argv)
