using System.Collections.Generic;
using UnityEngine;

// 리서치담당 3차 조사(Docs/reference/UPGRADE_SHOP.md "3차 조사") 표본 22종에서 뽑은 11개 유형.
// 표본이 로스터의 9%뿐이라 여기서 끝났다고 보지 말 것 — 새 유형이 나오면 여기 추가한다.
//
// 지금 전투에 반영되는 건 DamageIncrease와 ArmorShred 둘이다(UnitAttacker 참고).
// ArmorShred는 2026-09-03 방어력 시스템이 들어오면서 살아났다 — EnemyDummy.AddArmorShred로 쌓인다.
// 나머지는 데이터 자리만 있고 아직 아무 시스템도 안 읽는다 — SlowOnHit는 EnemyDummy에 %감속
// 인프라가 없고, Summon/MechanismChange/UtilityBuff/CastMethodChange는 유닛 전용 코드(Tier B)가 필요하다.
public enum TraitEffectKind
{
    DamageIncrease,        // 딜증가 — 표본에서 가장 흔함(22종 중 10). 유일하게 지금 반영됨.
    SlowOnHit,              // 이감부여 — EnemyDummy에 %감속 인프라 없음(2차)
    Summon,                 // 소환(서브유닛) — Tier B 전용
    MovementAbilityGrant,   // 이동능력부여(공중이동 등)
    StatusAilment,          // 상태이상(스턴 등)
    ArmorShred,             // 방깎 — EnemyDummy.EffectiveArmor를 깎는다. 하한 -20
    DamageTypeChange,       // 판정변경(물뎀→마뎀, 고정뎀 전환 등)
    MechanismChange,        // 메커니즘변경(소환수 제한 변경 등) — Tier B
    UtilityBuff,            // 버프(공속·자원회복 등, 대개 조건부 트리거) — Tier B
    AreaDamage,             // 범위피해
    AccuracyIncrease,       // 명중률/집탄율상승 — 명중 시스템 자체가 없음
    CastMethodChange,       // 시전방식변경(캐스트→즉발 등) — Tier B

    // 마방깍 — EnemyDummy.EffectiveMagicMultiplier를 올린다(마법 피해를 더 받게 한다).
    // ⚠️ **맨 뒤에 붙일 것.** 중간에 끼우면 이미 직렬화된 kind 값이 전부 한 칸씩 밀려
    // 다른 효과로 읽힌다. (2026-09-04에 실제로 ArmorShred 뒤에 끼웠다가 되돌렸다 —
    // 마침 effects가 전부 비어 있어 피해는 없었지만, 값이 있었으면 조용히 틀렸을 것이다.)
    MagicArmorShred,
}

[System.Serializable]
public class TraitEffect
{
    public TraitEffectKind kind;

    // 의미는 kind마다 다르다. DamageIncrease는 배율 가산치(0.1 = 공격력 +10%) — UnitAttacker가
    // 이렇게 읽는다. 그 외 kind는 아직 아무도 안 읽으므로 단위를 여기서 확정하지 않는다.
    public float value;
}

// 특성강화 1건 = 유닛 1종. 원작 사례 대부분 효과를 1~3개 동시에 받는다
// (조로 = 딜증가 + 방깎 + 이감, 한 유닛이 kind 하나만 갖는 게 아니다).
[CreateAssetMenu(fileName = "NewUnitTraitData", menuName = "GuilRandomDefense/Unit Trait Data")]
public class UnitTraitData : ScriptableObject
{
    public UnitData targetUnit;
    public string traitName;
    [TextArea] public string description;

    // 원작 맵(war3map.w3a) 직접 확인 결과 1~3개, 최빈값 2개다(2026-09-04, UPGRADE_SHOP.md 5차
    // 조사) — "대부분 4개"였던 이전 조사는 정정됐다.
    public int costTraitPoints = 2;

    public List<TraitEffect> effects = new List<TraitEffect>();

    // Tier B — 범용 표로 못 담는 유닛 전용 로직(키자루 분신 개수, 브룩 9타 트리거 등).
    // 비어있으면 없음. 그 유닛이 실제로 콘텐츠에 들어갈 때 이 키를 보고 코드를 짠다.
    public string specialEffectId;

    // ⚠️ 2026-09-05(06번①): 위 effects(TraitEffectKind)는 "스탯을 얼마나 올리는가"고,
    // 이건 완전히 다른 축이다 — 원작 특성강화 26종을 리서치담당이 원문에서 재확인한 결과,
    // **전부 캐릭터 전용 스킬의 레벨을 1→2로 올리는 것**이었다(예: 후지토라 !중력장 A0GR —
    // 레벨2는 레벨1 수치를 그대로 두고 "1/4 확률 운석낙하"라는 새 효과가 하나 늘어난다.
    // 배율 상승이 아니다). SkillLevel.effects가 레벨마다 독립 리스트인 이유가 정확히 이거다.
    //
    // 0(기본)이면 스킬승급형이 아니다(이 유닛은 위 effects나 아무 효과도 안 쓴다는 뜻).
    // 1 이상이면 targetUnit.skill(SkillData)의 levels[skillLevelUnlockIndex]를 쓰라는
    // 뜻이고, UnitUpgrades.Unlock으로 이 트레잇이 풀리면 UnitAttacker.CurrentSkillLevel이
    // 그 인덱스를 읽는다(UnitUpgrades.SkillLevelIndexFor 참고). targetUnit.skill이
    // null이면 아무 효과도 없다 — 이 값만으론 스킬을 만들어내지 못한다.
    //
    // 원작 26명은 우리 로스터에 이름으로 없다(이름 매핑 불가 원칙) — 등급별 순위 배정으로
    // 우리 유닛에 옮긴다. 어느 원작 유닛 자리인지는 이 asset 파일명이나 위 description에
    // 원작 유닛ID(예: `A0GR`, `H08X`)를 남길 것 — 사장님이 실제 콘텐츠를 배정할 때의 연결점이다.
    public int skillLevelUnlockIndex;
}
