using System.Collections.Generic;
using UnityEngine;

public enum IngredientKind
{
    SpecificUnit,
    SpecificItem,
    UnitGradeWildcard,
}

[System.Serializable]
public class RecipeIngredient
{
    public IngredientKind kind = IngredientKind.SpecificUnit;

    public UnitData unit;          // kind == SpecificUnit
    public ItemData item;          // kind == SpecificItem
    public UnitGrade wildcardGrade; // kind == UnitGradeWildcard — 이 등급이면 어떤 유닛이든 인정 (예: "랜덤전용유닛 1기")

    public int count = 1;

    // ⚠️ 맨 뒤(2026-10-06, 다른세계 브로리 「임채현 or 이재윤」 — 사장님 확정) — 「A or B」 재료: kind == SpecificUnit일 때 unit이 모자라면 이 유닛으로 대신한다(둘 다 있으면 unit 먼저).
    // CombineSystem 재료 판정·소모·부족 안내와 GameHud 재료 문구가 본다. count가 2 이상인 칸엔 쓰지 않는다(첫 재료가 일부만 들어간 채 대안으로 넘어가지 않게).
    public UnitData alternativeUnit;
}

[System.Serializable]
public class RecipeResourceCost
{
    public ResourceType type;
    public int amount;
}

[CreateAssetMenu(fileName = "NewCombineRecipe", menuName = "GuilRandomDefense/Combine Recipe")]
public class CombineRecipe : ScriptableObject
{
    public string commandId;
    public UnitData result;
    public List<RecipeIngredient> ingredients;
    public int goldCost;
    public List<RecipeResourceCost> resourceCosts;

    // 라운드 조건. 0이면 제한 없음. 예: "41라운드 이전에만 조합 가능" → maxRound = 40.
    public int minRound;
    public int maxRound;

    // 영원 등급 전용. Save = 게임 클리어 누적 횟수. 원작 udg_Load_PlayCount 게이트(5·10·15·
    // 20·25·30·35)와 한 칸 어긋난 같은 사다리다(우리는 0부터 시작). 2026-09-05 11번
    // (PersistentSave) 이후 PersistentSave.Data.cumulativeClearCount로 검사한다
    // (CombineSystem.CanAfford 참고). 0이면 제한 없음.
    public int requiredSaveCount;

    // ⚠️ 맨 뒤에 추가(2026-09-06, CHAT_UNLOCK_70_SIDE_EFFECT_SWEEP.md, 리서치담당 전수) —
    // 원작 udg_Damage_level_Fixed[플레이어](DamageLevelFixedState 참고, PlayerContext에
    // 부착)에 영구 누적되는 보너스. 지금 실제로 도는 건 이 경로다(commandId를 가진
    // CombineRecipe 207개 전부 — 조합식은 우리 창작 영역이라 이게 원작 채팅언락 히든류의
    // 실행 경로다). 히든_성탄.asset(Hidden_Aokiji, commandId="아오키지조합 / aokiji")에만
    // 2를 넣는다 — 나머지 206개는 기본값 0.
    //
    // 🔴 이중 계상 경고: 같은 원작 트리거(Hidden_Aokiji)가 HiddenCombineData 쪽에도 같은
    // 필드(damageLevelFixedBonus=2)로 들어 있다 — 그쪽은 "원작 충실 자리"로 사장님 유닛배정
    // 대기 중이라 지금은 에셋이 없어 안 돈다(HiddenCombineData.cs 참고). 사장님이 유닛배정을
    // 주셔서 그 에셋이 생기는 순간, 두 경로가 동시에 켜지면 +2가 두 번 누적된다 — 그때는
    // 둘 중 하나를 0으로 만들거나 한쪽 경로를 꺼야 한다. APPROXIMATION_LEDGER.md 참고.
    public int damageLevelFixedBonus;

    // 10-06 — 히든·불멸·초월 식은 조합 버튼이 아니라 채팅 코드로만 한다(CombineSystem.TryCombineByChat). commandId와 「친구이름 조합」 외에
    // 이 문구도 받는다. 초월은 원작처럼 수식어(예 「최강의검사」) — 사장님이 25종 수식어를 주시면 여기 채운다. 비면 무시.
    public string chatPhrase;

    // ⚠️ 맨 뒤(2026-10-06, 영원함 조세민 — 사장님 확정 「180초뒤에 생성」) — 0보다 크면 조합 순간 재료·비용은 바로 소모되지만 **결과 유닛은 이 초 뒤에 나온다**
    // (조합한 사람에게 알림 「N초 뒤 ○○가 나타납니다」). 판이 끝나거나 CombineSystem이 사라지면 취소된다. 0이면 지금처럼 즉시.
    public float resultDelaySeconds;
}
