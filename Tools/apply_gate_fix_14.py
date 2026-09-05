#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""14건 미확인 게이트 중 12건 확정(리서치담당 GATE_FIX_14.csv, c1adaea) 적용 +
2건([미확인] 유지) 확인 + 1건(김영원, 근사 그대로 두되 근거 기록) 확인.

⚠️ 값은 전부 GATE_FIX_14.csv의 "권장_평타당발동률" 그대로 쓴다 — 추측 없음.
"""
import re

OUR_TAG = ' [미확인: 원작 게이트] 2채널(ORIGINAL_SKILL_DAMAGE_TABLE.csv) 원본엔 게이트 컬럼이 없어 이 효과의 실제 발동조건을 모른다 — triggerChance=1.0(매 타)은 확인된 값이 아니라 구조적 한계로 인한 근사다(2026-09-06, GATE_MISMATCH 감사).'

# base -> dict(kind='chance'|'gauge', value=, note=, estimated=bool)
FIXES = {
    '전설적인_박성호': dict(kind='chance', value=0.45,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend0 GetRandomInt(1,100)<46 확정 — triggerChance 1.0(근사) → 0.45.'),
    '전설적인_임채현': dict(kind='chance', value=0.143,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend11 GetRandomInt(1,7)==6 확정 — triggerChance 1.0(근사) → 0.143(1/7).'),
    '전설적인_이유선': dict(kind='chance', value=0.10,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend8 GetRandomInt(1,10)==3 AND 대상포인트값>199 확정 — 포인트값 조건(대상이 보스일 때만, 축 밖)은 버리고 확률만 반영. triggerChance 1.0(근사) → 0.10(1/10).'),
    '전설적인_백기현': dict(kind='chance', value=0.10,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend10 → e00V 더미 GetRandomInt(1,10)==3 확정(DUMMY_CHANNEL_GATES.csv에 이미 있던 값과 일치) — triggerChance 1.0(근사) → 0.10(1/10).'),
    '영원_최상호': dict(kind='chance', value=0.10,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Cavendish_Attack "GetRandomInt(1,10)==6 ‖ NOT(1/10) AND GetRandomInt(1,9)==7"(두 갈래 배타, 합쳐도 약 0.20이나 리서치담당 권장값 0.10 사용) 확정 — triggerChance 1.0(근사) → 0.10.'),
    '제한_임준성': dict(kind='chance', value=0.125,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Sinobu_Attack GetRandomInt(1,8)==5(+포인트값 분기, 축 밖) 확정 — triggerChance 1.0(근사) → 0.125(1/8).'),
    '제한_최영민': dict(kind='chance', value=0.75,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_RedAttack GetRandomInt(1,4)>1(+포인트값 분기, 축 밖) 확정 — 1/4 초과이므로 75%. triggerChance 1.0(근사) → 0.75.'),
    '불멸_이이삭': dict(kind='chance', value=0.05,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Sengoku_Attack GetRandomInt(1,20)==8(+능력레벨 A0D8 분기, 축 밖) 확정 — triggerChance 1.0(근사) → 0.05(1/20).'),
    '전설적인_박은석': dict(kind='chance', value=0.25, estimated=True,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend23 소환 지점 여럿("GetRandomInt(1,4)==3 AND 포인트값==200" 또는 다른 분기 "1/4") — 보수적으로 1/4 채택. [추정] triggerChance 1.0(근사) → 0.25(추정치, 확정 아님).'),
    '전설적인_정윤식': dict(kind='gauge', gauge_kind=0, threshold=145,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend2 "시전자마나==145.00" 확정 — 게이지형이다(145타마다 정확히 1번, 확률이 아니다). OnHitChance triggerChance=1.0(근사, 100배 넘는 과다) → OnHitCount(Mana, threshold=145)로 교정.'),
    '전설적인_진연서': dict(kind='gauge', gauge_kind=0, threshold=115,
        note='정정(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_Legend7 "시전자마나==115.00" 확정 — 게이지형이다(115타마다 정확히 1번, 확률이 아니다). OnHitChance triggerChance=1.0(근사, 100배 넘는 과다) → OnHitCount(Mana, threshold=115)로 교정.'),
    '영원_김영원': dict(kind='keep',
        note='확인(2026-09-06, GATE_FIX_14/c1adaea): 원작 Trig_ViVi_Attack → e0D3 더미 "NOT(시전자마나==150) AND 버프 B034 미보유" — 거의 매 타 발동(≈0.99)이라 triggerChance=1.0 근사가 크게 틀리지 않는다. 확인 완료, 값 유지.'),
}


def main():
    changed = 0
    for base, fix in FIXES.items():
        path = f'Assets/Data/UnitSkills/SkillData_원작능력_{base}.asset'
        text = open(path, encoding='utf-8').read()
        if OUR_TAG not in text:
            print(f'⚠️ {base}: 기존 [미확인] 태그를 못 찾음 — 건너뜀')
            continue

        text = text.replace(OUR_TAG, ' ' + fix['note'])

        if fix['kind'] == 'chance':
            text = re.sub(r'triggerChance: [\d.eE+-]+', f"triggerChance: {fix['value']}", text, count=1)
        elif fix['kind'] == 'gauge':
            # ⚠️ 이 파일들(2채널 원본)은 애초에 OnHitChance라 hitCountThreshold·
            # resetTo·gaugeKind 필드 자체가 YAML에 없었다(C# 기본값에 기댐) —
            # 정규식 치환이 아니라 range: 줄과 effects: 줄 사이를 통째로 다시 써서
            # 세 필드를 새로 넣는다(없는 필드를 치환하면 조용히 아무 일도 안 하고
            # 넘어가는 사고가 난다 — 실제로 이 파일들에서 걸림).
            text = re.sub(r'^  triggerType: \d+$', '  triggerType: 3', text, count=1, flags=re.M)
            text = re.sub(r'triggerChance: [\d.eE+-]+', 'triggerChance: 1.0', text, count=1)
            m = re.search(r'(    range: [\d.]+\n)(?:    hitCountThreshold: \d+\n    resetTo: \d+\n    gaugeKind: \d+\n)?(    effects:\n)', text)
            if not m:
                print(f'⚠️ {base}: range/effects 자리를 못 찾음 — 건너뜀')
                continue
            insertion = (f"    hitCountThreshold: {fix['threshold']}\n"
                         f"    resetTo: 0\n"
                         f"    gaugeKind: {fix['gauge_kind']}\n")
            text = text[:m.end(1)] + insertion + text[m.start(2):]
        # kind == 'keep': 필드는 안 건드림, description만 정정.

        open(path, 'w', encoding='utf-8').write(text)
        changed += 1
        print(f'{base}: {fix["kind"]} 적용')

    print(f'총 {changed}개 파일 처리')


if __name__ == '__main__':
    main()
