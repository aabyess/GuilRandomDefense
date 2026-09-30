"""배타 분기(SkillLevel.exclusiveGroup) 넣기 — 구조 칸 백로그 3번(2026-09-30 구현담당1, Docs/research/SKILL_STRUCTURE_BACKLOG.md).

원작 평타 트리거의 `if GetRandomInt(..)==a then A elseif GetRandomInt(..)==b then B` 짝을 한 묶음으로 건다.
뒤 스킬(B)의 확률은 지금까지 주변 확률((1−pA)×pB)로 적혀 있었다 — 묶음에선 A가 빗나갔을 때만 굴리므로 원작 조건부 확률(pB)로 되돌린다.
(기댓값은 그대로고 「같은 타에 둘 다」가 사라진다. 레이쥬만 pB가 주변 확률이 아닌 날값으로 적혀 있어 11% 과다였다 → 고쳐진다.)
로스터 스킬 목록에서 A가 B보다 앞이어야 한다 — 아니면 두 줄을 맞바꾼다.
근거는 war3map_new.j 각 트리거 본문(표의 넷째 칸). 안 넣는 것: 티치(1/10 안에서 1/6 ↔ 5/6 — T특성 변수 TR_AddInt로 갈리는 토글 갈래) ·
  시키 Legend4(elseif 쪽이 소환체) · 나미 Skill_2·상디 skill_1 안쪽 분기(한 스킬 안의 갈래라 효과 캐스케이드로 이미 담김/시각).
다시 돌려도 같은 결과.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

TAG = '[배타 09-30]'
# (로스터, 묶음 번호, 앞 스킬 A, 뒤 스킬 B, B의 조건부 확률 또는 None(그대로), 근거)
TABLE = [
    ('불멸_고도현', 1, '게이트_불멸_고도현_caa364ec', '더미채널_불멸_고도현_79행_01250', 1 / 8, 'Ed_Attack: if (1,22)==6 … elseif (1,8)==5 → Ed_Skill_2sc'),
    ('불멸_이이삭', 1, '더미채널_불멸_이이삭_1', '원작능력_불멸_이이삭', 1 / 20, 'Sengoku_Attack: if (1,10)==6 stomp elseif (1,20)==8'),
    ('전설적인_박민수', 1, '원작트리거_전설적인_박민수_Legend15_배리어', '원작트리거_전설적인_박민수_Legend15_배리어불스', 1 / 30, 'Legend15: if (1,10)==3 stomp elseif (1,30)==3'),
    ('전설적인_임건웅', 1, '게이트_전설적인_임건웅_9ffa9c45', '게이트_전설적인_임건웅_87ed0ff5', 1 / 19, 'Legend19: if (1,10)==3 → Legend19_0 elseif (1,19)==3 → Legend19_1'),
    ('제한_강보명', 1, '게이트_제한_강보명_57c27e37', '더미채널_제한_강보명_1', 1 / 20, 'RedAttack: if (1,22)==6 → Red_skill_1 elseif (1,20)==7 carrionswarm'),
    ('초월_배성령_AD', 1, '게이트_초월_배성령_AD_18aa2343', '게이트_초월_배성령_AD_SandiAttack_Upgrade_1of18', 1 / 18, 'SandiAttack_Upgrade: if (1,6)==6 → Sandi_skill_1 elseif (1,18)==5 thunderclap'),
    ('랜덤_한마_바키', 1, '게이트_랜덤_한마_바키_9ffa9c45', '절대쿨_랜덤_한마_바키', None, 'byakuya_Attack: if (1,10)==5 → byakuya_Q elseif B00K 없음 → Byakuya_E'),
]


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
    for roster, group, first, second, chance, why in TABLE:
        a, b = sat.load(first), sat.load(second)
        ga, gb = sat.guid_of(a.path), sat.guid_of(b.path)
        rp = os.path.join(sat.ROSTER_DIR, roster + '.asset')
        text = open(rp, encoding='utf-8').read()
        la, lb = ('  - {fileID: 11400000, guid: %s, type: 2}\n' % g for g in (ga, gb))
        assert la in text and lb in text, (roster, first, second)
        if text.index(la) > text.index(lb):
            text = text.replace(la, '\0').replace(lb, la).replace('\0', lb)
            print('%-14s 목록 순서 맞바꿈(%s ↔ %s)' % (roster, first, second))
            if not dry:
                open(rp, 'w', encoding='utf-8').write(text)
            changed.append(rp)
        old = float(sat.get_level_field(b, 0, 'triggerChance'))
        pa = float(sat.get_level_field(a, 0, 'triggerChance'))
        for asset, which in ((a, '앞'), (b, '뒤')):
            msg = '배타 묶음 %d의 %s 스킬 — %s(Tools/apply_exclusive_groups.py)' % (group, which, why)
            if note(asset, msg):
                for i in range(len(asset.levels)):
                    sat.set_level_field(asset, i, 'exclusiveGroup', group)
                    if asset is b and chance is not None:
                        sat.set_level_field(asset, i, 'triggerChance', round(chance, 6))
                if not dry:
                    sat.save(asset)
                changed.append(asset.path)
        if chance is not None:
            print('%-14s A %.4f · B %.6f → %.6f (주변 확률 %.6f → %.6f)' % (roster, pa, old, chance, old, (1 - pa) * chance))
        else:
            print('%-14s A %.4f · B 확률 그대로 %.4f' % (roster, pa, old))
    print('바꾼 파일 %d' % len(set(changed)))


if __name__ == '__main__':
    main('--dry' in sys.argv)
