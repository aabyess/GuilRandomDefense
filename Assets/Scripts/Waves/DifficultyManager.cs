using UnityEngine;

// 난이도 선택 상태 — 원작 Trig_Select_effect_Actions(모드선택 마스터 함수)의 결과값 하나를
// 들고 있는 게 이 클래스의 전부다. 실제 수치표는 DifficultyMode.cs(DifficultyTable)에 있다.
//
// 원작 InitTrig_Select1/Select_effect(DIFFICULTY_SPEC_2026-09-11.md §7): Player(0)(호스트)
// 에게만 6버튼 다이얼로그가 뜨고, 제한시간·기본값이 없다(아무도 안 누르면 영원히 대기).
// DifficultySelectHud가 호스트 화면에만 그 버튼을 그리고, 여기 SelectMode를 부른다.
//
// GameAuthority 서버권한 패턴(PM 지시) — 실제 모드 확정은 서버만 한다. 지금은 실제
// 네트코드가 없어(GameAuthority.IsServer가 항상 true) 로컬 싱글턴 하나가 곧 "서버 상태"다.
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    DifficultyMode? current;

    public bool IsModeSelected => current.HasValue;

    // 선택 전엔 아무도 이 값을 읽으면 안 된다(원작은 그 전까지 라운드 자체가 안 돈다) —
    // 호출부는 전부 IsModeSelected를 먼저 본다. 그래도 방어적으로 쉬움(가장 무해한 기본값)을 돌려준다.
    public DifficultyMode Current => current ?? DifficultyMode.Easy;

    public DifficultyModeData CurrentData => DifficultyTable.Get(Current);

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 호스트가 버튼을 눌렀을 때 DifficultySelectHud가 부른다. 원작 마스터 함수는 한 번
    /// 걸리면 그걸로 끝(다이얼로그가 다시 안 뜬다) — 재선택을 막는다.
    /// </summary>
    public void SelectMode(DifficultyMode mode)
    {
        if (!GameAuthority.IsServer) return;
        if (current.HasValue) return;

        current = mode;

        // Aegr(마법 피해 배율)은 적마다 스폰 시점에 곱하는 게 아니라 전역 배율 하나로
        // 둔다(EnemyDummy.EffectiveMagicMultiplier 참고) — 원작도 마스터 함수에서 딱 한 번
        // 레인별로 건 뒤 게임 내내 안 바뀐다.
        EnemyDummy.DifficultyMagicMultiplier = CurrentData.magicMultiplier;

        Debug.Log($"난이도 확정: {mode.KoreanName()}");
    }
}
