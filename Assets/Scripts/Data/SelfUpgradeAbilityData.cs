using UnityEngine;

// 원작 비비 A0LZ류 "자가시전 영구 강화" 능력의 설정값. SkillEffect가 아니다 — 대상이 없고
// 피해도 없는 유닛 자신의 액션이라 SkillData의 kind/target 모델에 안 맞는다(A0LZ_CASTER_
// STACK_INVESTIGATION.md/7703d2c 참고). 성공률·자원비용을 코드에 상수로 박지 않고 여기
// 자산 필드로 뺀다 — 원작에 이미 200/300처럼 여러 문턱이 실재해서 다음 캐릭터가 다른
// 값을 쓸 수 있다(뿌리: 오늘 하루 "상수로 박았다가 다른 값이 실재해서 못 담은" 사고가
// 여러 번 났다).
[CreateAssetMenu(fileName = "NewSelfUpgradeAbilityData", menuName = "GuilRandomDefense/Self Upgrade Ability Data")]
public class SelfUpgradeAbilityData : ScriptableObject
{
    // 원작 alev와 같은 뜻 — 이 레벨에 도달하면 성공확률 공식과 무관하게 더 안 오른다.
    // A0LZ는 성공확률 공식 자체가 레벨10에서 자연히 0%가 되지만(90-9*10=0), 식이 나중에
    // 바뀌어도 상한이 남아 있도록 별도로 잘라둔다(PM 지시 — "식이 바뀌면 상한이 사라진다").
    public int maxLevel = 10;

    // 성공확률(%) = baseChancePercent - 현재레벨 × chancePerLevelPercent. A0LZ 원작값:
    // 90 · 9(레벨0=90%, 레벨9=9%, 레벨10=0%).
    public float baseChancePercent = 90f;
    public float chancePerLevelPercent = 9f;

    // 시도할 때마다(성공/실패 무관) 나가는 자원. A0LZ 원작값: 목재2 + 랜덤위습(e0IX) 3기.
    public int woodCost = 2;
    // ⚠️ 랜덤위습(e0IX)에 정확히 대응하는 WispData 자산이 이 프로젝트에 아직 없다
    // (ACQUISITION_RESEARCH.md — 우리 Wisp_* 7종 중 "isPlayerChoice=0·targetGrade=흔함"인
    // 순수 랜덤 위습이 없다, 가장 가까운 건 Wisp_흔함선택인데 그건 isPlayerChoice=1이라
    // 뜻이 다르다). 배정 담당(구현담당2/PM)이 그 자산을 만들거나 지정할 때까지 비워둔다 —
    // 비어 있으면 TryUpgradeSelf가 안전하게 실패한다(아래 참고), 조용히 무제한 통과하지 않는다.
    public WispData wispCurrency;
    public int wispCost = 3;
}
