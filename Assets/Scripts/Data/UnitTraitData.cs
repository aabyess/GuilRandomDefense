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

    public int costTraitPoints = 4;   // 원작 사례 대부분 4개 [원작]

    public List<TraitEffect> effects = new List<TraitEffect>();

    // Tier B — 범용 표로 못 담는 유닛 전용 로직(키자루 분신 개수, 브룩 9타 트리거 등).
    // 비어있으면 없음. 그 유닛이 실제로 콘텐츠에 들어갈 때 이 키를 보고 코드를 짠다.
    public string specialEffectId;
}
