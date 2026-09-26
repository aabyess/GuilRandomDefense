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

        // 🔴 2026-09-11 정정(PM 리뷰) — EnemyDummy.DifficultyAegrLevelOffset은 static이라
        // 씬을 다시 로드해도 이전 판의 값이 그대로 남는다. 새 판이 시작될 때(이 컴포넌트가
        // 다시 Awake될 때) 0(=오프셋 없음)으로 되돌려 선택 전에는 항상 기존과 동작이
        // 같도록 한다.
        EnemyDummy.DifficultyAegrLevelOffset = 0;

        // MP: 네트 판이면 대기실에서 호스트가 고른 난이도로 시작한다(호스트·클라 모두 — 클라는 이게 없으면
        //     「방장이 모드를 선택하고 있습니다」에서 영원히 멈춘다). 기억값·선택 창 둘 다 건너뛴다.
        //     싱글(MatchConfig.Active false)이면 아래 기존 흐름 그대로 — 무동작.
        if (MatchConfig.Active && MatchConfig.Difficulty.HasValue)
        {
            ApplyMode(MatchConfig.Difficulty.Value);
            return;
        }

        // 🔴 2026-09-24 사장님 「난이도 왜 자꾸 뜨는거야 / 계속 뜨는 버그도 수정해봐」
        //    원작은 한 판 = 한 번 고르기라 판마다 묻는 게 맞다. 그런데 우리는 시험하느라
        //    재생을 수십 번 누르고, 그때마다 창이 떠서 매번 같은 걸 눌러야 했다.
        //    → **지난번에 고른 난이도를 기억해 그대로 시작한다.** 창은 기억이 없을 때만 뜬다.
        //    바꾸려면 아래 ForgetSavedMode()를 부르면 된다(메뉴 Tools/게임/난이도 다시 묻기).
        //    ⚠️ 기억은 이 기계 안에서만이다(PlayerPrefs) — 판 상태가 아니라 사람의 편의값이다.
        if (GameAuthority.IsServer && PlayerPrefs.HasKey(SavedModeKey))
        {
            int saved = PlayerPrefs.GetInt(SavedModeKey);
            if (System.Enum.IsDefined(typeof(DifficultyMode), saved))
            {
                ApplyMode((DifficultyMode)saved);
                Debug.Log($"난이도 기억해서 시작: {current.Value.KoreanName()} " +
                          "(다시 묻게 하려면 Tools/게임/난이도 다시 묻기)");
            }
            else
            {
                PlayerPrefs.DeleteKey(SavedModeKey);   // 값이 깨졌으면 버리고 다시 묻는다
            }
        }
    }

    public const string SavedModeKey = "GuilRandomDefense.Difficulty";

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

        ApplyMode(mode);
        PlayerPrefs.SetInt(SavedModeKey, (int)mode);
        PlayerPrefs.Save();
    }

    // 고른 값을 실제로 거는 부분. SelectMode(사람이 누름)와 Awake(기억해서 시작) 둘 다 쓴다.
    void ApplyMode(DifficultyMode mode)
    {
        current = mode;

        // Aegr(마법 피해)는 적마다 스폰 시점에 곱하는 게 아니라 전역 레벨 오프셋 하나로
        // 둔다(EnemyDummy.EffectiveMagicMultiplier/AegrBaseLevel 참고) — 원작도 마스터
        // 함수에서 딱 한 번 레인별로 건 뒤 게임 내내 안 바뀐다. 🔴 2026-09-11 정정(PM 리뷰):
        // 배율(CurrentData.magicMultiplier)을 곱하던 걸 레벨 오프셋으로 바꿨다 — 위 Awake
        // 주석과 EnemyDummy.DifficultyAegrLevelOffset 참고.
        EnemyDummy.DifficultyAegrLevelOffset = CurrentData.aegrLevelOffset;

        Debug.Log($"난이도 확정: {mode.KoreanName()}");
    }
}
