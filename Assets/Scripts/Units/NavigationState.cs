using UnityEngine;

// 원작 "항법"(진행 루트) — 5택1, 플레이어당 영구 1회, 되돌리기 불가.
// NAVIGATION_ROUTES_FULL.md(리서치담당, c93c559) 확정: `Trig_onedill_Tech_Actions`가
// 영웅 스킬(H0C4, 4자리) "배우기" 이벤트로 구현한 진짜 5지선다 특성 선택 —
// 배우는 즉시 능력을 add→remove해서 재선택을 원천 차단하는 WC3 표준 "1회성
// 스킬트리 분기" 패턴이다. 여기서는 그 결과(어느 걸 골랐는가)만 담는다 —
// 선택 UI/시점(H0C4 배치)은 이 컴포넌트 밖(맵 생성기·PM 몫)이다.
//
// ⚠️ 패왕의길(Hegemon)은 PM 지시로 보류 — Round_Unit(우리 대응 불명)·A11S 레벨
// 체계 대응 확인 전까지 선택지 자체에는 넣되 효과는 안 붙인다(§ 아래 참고).
public enum NavigationChoice
{
    None,
    Hegemon,       // 패왕의길 — 🔴 보류(효과 미배선, PM 확인 대기)
    Union,         // 연합세력 — 포인트값 100 초과 유닛 로스터 편입 시 랜덤위습 +1
    Gambler,       // 도박광 — 다른세계 도박 실패 시 럭키토큰 +1
    SupportBoost,  // 도움소 강화 — 해루석/버스터콜 레벨2 수치
    SupportLock,   // 도움소 잠금 — 아이템 도박 축소풀(13종)
}

public class NavigationState : MonoBehaviour
{
    [SerializeField] NavigationChoice choice = NavigationChoice.None;

    // ⚠️ 맨 뒤에 추가(2026-09-06, 패왕의길 보류 해제) — 직렬화 순서를 지킨다. 패왕의길
    // 선택 시 +2를 여기로 흘려보낸다(DamageLevelFixedState.Add 참고). 씬에서 같은
    // 슬롯(GameObject)에 같이 붙는다 — 비어 있으면(씬 배선 전) 조용히 건너뛴다.
    [SerializeField] DamageLevelFixedState damageLevelFixedState;

    public NavigationChoice Choice => choice;
    public bool HasChosen => choice != NavigationChoice.None;

    /// <summary>
    /// 항법 선택 — 플레이어당 평생 1회, 되돌릴 수 없다(원작 add→remove 패턴, 재선택
    /// 자체가 불가능하다는 뜻이지 "무를 수 있다"가 아니다). 이미 골랐거나
    /// None을 넘기면 실패.
    /// </summary>
    public bool TrySelect(NavigationChoice newChoice)
    {
        if (HasChosen || newChoice == NavigationChoice.None) return false;
        choice = newChoice;

        // 패왕의길 원작 효과: udg_Damage_level_Fixed[플레이어] += 2 (NAVIGATION_ROUTES_FULL.md).
        if (newChoice == NavigationChoice.Hegemon) damageLevelFixedState?.Add(2);

        return true;
    }
}
