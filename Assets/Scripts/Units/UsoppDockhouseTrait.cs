using System;

// 우솝(G.O.D, H09B) 특성강화 전용 — 26명 중 유일하게 "구매한 그 유닛"이 아니라 "전체
// 플레이어의 공용 건물(도움소)"에 거는 효과다(TEAM_RULES.md 뿌리 ㊽, TRAIT_UPGRADE_26_HEROES_FULL.md).
// 원작: 루프로 4명 전원의 udg_Manso[]에 IncUnitAbilityLevelSwapped('A0IE')를 건다 —
// "누가 샀는가"가 아니라 "누구든 한 번이라도 샀는가"가 판정 기준이라, 플레이어별
// PlayerContext.NavigationState/UnitUpgrades와는 스코프가 다르다(그쪽은 1인분).
// ⚠️ 그래서 어느 PlayerContext에도 안 속한 게임 전체 단일 플래그로 둔다 — 씬이 하나뿐이고
// (SampleScene 하나, 재시작 시 씬 리로드 경로 없음) 도메인 리로드가 켜져 있어(EditorSettings
// m_EnterPlayModeOptions=0), 정적 필드가 새 판 시작보다 오래 살아남을 경로가 지금은 없다.
public static class UsoppDockhouseTrait
{
    public static bool Active { get; private set; }

    public static event Action OnActivated;

    // GameHud.OnTraitButtonClicked이 UnitTraitData.triggersUsoppDockhouseBoost가 true인
    // 트레잇을 언락할 때 부른다. 원작에 "되돌리는" 코드가 없다 — 한 번 켜지면 판 끝까지
    // 유지(TRAIT_5GATE_REMAINING_VALUES.md에도 끄는 경로가 없다는 걸 확인). 두 번째 이후
    // 호출은 이미 Active라 그냥 무시된다 — 어차피 UnitUpgrades.unlockedTraits(HashSet)가
    // 같은 트레잇의 재구매 자체를 막아서 정상 경로로는 두 번 안 불린다.
    public static void Activate()
    {
        if (Active) return;
        Active = true;
        OnActivated?.Invoke();
    }
}
