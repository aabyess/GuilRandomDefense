using UnityEngine;

// 원작 "항법"(진행 루트) — 5택1, 플레이어당 영구 1회, 되돌리기 불가.
// NAVIGATION_ROUTES_FULL.md(리서치담당, c93c559) 확정: `Trig_onedill_Tech_Actions`가
// 영웅 스킬(H0C4, 4자리) "배우기" 이벤트로 구현한 진짜 5지선다 특성 선택 —
// 배우는 즉시 능력을 add→remove해서 재선택을 원천 차단하는 WC3 표준 "1회성
// 스킬트리 분기" 패턴이다. 여기서는 그 결과(어느 걸 골랐는가)만 담는다 —
// 선택 UI는 `GameHud.BuildNavigationUI`(2026-09-07)가 담당한다. H0C4 실물 배치(맵
// 오브젝트)만 이 컴포넌트 밖(맵 생성기·PM 몫)이다.
//
// ⚠️ 패왕의길(Hegemon) 보류는 해제됐다(2026-09-06 — A11S 레벨 체계 대응 확인 완료,
// 아래 TrySelect의 DamageLevelFixedState.Add(2)가 실제 효과다). enum 주석의 "보류"는
// 해제 전에 쓴 것이라 낡았었다 — 지금 지운다.
public enum NavigationChoice
{
    None,
    Hegemon,       // 패왕의길 — A11S 강화코드 레벨 +2(DamageLevelFixedState 경유)
    // 연합세력 — 2026-09-07 배선 완료(PM 지시). RewardDistributor.GrantUnionWispIfEligible
    // (UnitSpawner.Spawn에서 호출)이 원작 Trig_UnitJohabCounter_Actions를 재현한다 —
    // 유닛 등급 Tier()>=Superior(포인트값>100 근사) + 이 항법 선택 시 랜덤위습(e0IX,
    // Wisp_랜덤유닛.asset — 라운드 보상 위습과 같은 것) 1기. RewardDistributor.unionWisp는 MapGenerator가 배선한다.
    // 🔴 09-27 정정(GAP 5번): 예전엔 e0IX를 Wisp_흔함.asset으로 적었는데 그건 흔함 선택 칸으로 가는 다른 위습이다.
    Union,
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

    // ── 원딜(2026-09-27 GAP 6) — 원작 udg_Tech_Onedill ──
    //    「패왕의길」(udg_Tech_Onedill_int=1)을 고른 플레이어가 제한됨(point 106~110, Trig_UnitJohabCounter)·초월(Eternal_*)·
    //    불멸(IM_*)·영원(Forever_*) 중 **한 기**를 얻으면 Tech_Onedill=true — 제한됨 [조합] 9개(A02O·A10A·A01Y·A0S0·A0RP·A007·A0UH·A022·A011)를 끄고,
    //    초월·불멸·영원 채팅 조합 45개는 조건이 `Tech_Onedill==false`라 전부 막힌다. 즉 첫 한 기 뒤 네 등급 조합이 잠긴다.
    //    한 판짜리(직렬화 안 함 — 씬을 다시 불러오면 컴포넌트가 새로 생긴다).
    public bool OneDealLocked { get; private set; }

    public static bool IsOneDealGrade(UnitGrade grade) =>
        grade == UnitGrade.Limited || grade == UnitGrade.Transcendent || grade == UnitGrade.Immortal || grade == UnitGrade.Eternal;

    /// <summary>이 플레이어가 유닛을 새로 얻었다(UnitSpawner.Spawn) — 패왕의길이고 원딜 등급이면 잠근다. 처음 잠긴 순간 true.</summary>
    public bool RegisterAcquired(UnitData unit)
    {
        if (OneDealLocked || choice != NavigationChoice.Hegemon || unit == null || unit.isSystemUnit || !IsOneDealGrade(unit.grade)) return false;
        OneDealLocked = true;
        return true;
    }
    public bool HasChosen => choice != NavigationChoice.None;

    // MP: 클라 HUD용 — 원딜 잠금은 호스트에서 정해지고(CanAfford도 호스트) 클라는 NetPlayer가 옮겨 적는다.
    public void ApplyReplicatedOneDeal(bool locked) { if (locked) OneDealLocked = true; }

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
