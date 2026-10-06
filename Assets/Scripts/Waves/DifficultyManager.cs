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
        // static이라 씬을 다시 불러도 남는다 — 새 판마다 비운다(GAP 4 파티·솔로).
        StoryPartyPlayers = 0;
        IsSolo = false;

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
        //    ⚠️ 2026-10-03 사장님 확정으로 **사람 플레이에선 더 이상 건너뛰지 않는다** — 원작은 게임에 들어가서 방장이 매번 고른다
        //    (DifficultySelectHud). 기억값 자동 선택은 측정 도구(gameshot `mode:`)가 ToolAutoPick을 켰을 때만 쓴다.
        bool toolAutoPick = PlayerPrefs.GetInt(ToolAutoPickKey, 0) == 1;
        if (toolAutoPick) PlayerPrefs.DeleteKey(ToolAutoPickKey);   // 한 판짜리 신호 — 도구가 켜고 이 판이 시작하며 소비한다(남아도 다음 사람 플레이에 번지지 않게)
        if (toolAutoPick && GameAuthority.IsServer && PlayerPrefs.HasKey(SavedModeKey))
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

    /// <summary>측정 도구(ClaudeCommands gameshot mode:)만 1로 써 둔다 — 그 판의 Awake가 기억값으로 대화상자 없이 시작하고 키를 지운다. 사람 플레이엔 없다.</summary>
    public const string ToolAutoPickKey = "GuilRandomDefense.ToolAutoPick";

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
        PlayerPrefs.SetInt(SavedModeKey, (int)mode);   // 마지막으로 고른 난이도 기억(도구가 쓰는 값) — 사람 플레이는 이걸로 자동 선택하지 않는다
        PlayerPrefs.Save();
        // MP: 호스트가 고르면 [Networked] 난이도에 실어 클라가 따라가게 한다(클라는 NetGameState가 ApplyReplicatedMode로 건다).
        if (MatchConfig.Active && NetGameState.Instance != null && NetGameState.Instance.HasStateAuthority)
            NetGameState.Instance.Difficulty = (int)mode;
    }

    // MP: 판 도중 재접속한 클라 — 게임 씬이 NetGameState보다 먼저 떠 Awake가 난이도를 못 봤을 때 NetGameState가 늦게 건다.
    public void ApplyReplicatedMode(DifficultyMode mode)
    {
        if (!current.HasValue) ApplyMode(mode);
    }

    // ── 파티·솔로 보정(2026-09-27 GAP 4) — 원작 solo_1~4는 난이도 다이얼로그가 눌린 순간 PLAYING 슬롯을 센다 ──
    //    ⚠️ 인원은 Awake에서 안 센다(구현담당2): 멀티는 PlayerContext.Awake가 MatchConfig로 occupied를 정하는데 Awake 순서가 보장되지 않는다.
    //       씬 진입 1.5초 뒤에 세고, 네트 판이면 MatchConfig.OccupiedSlots를 쓴다. 안내도 그때 띄운다(친구가 씬을 다 불러온 뒤).
    //    첫 스토리는 10초에 나와 그 전에 정해진다. 그래도 먼저 불리면 StoryHpBonus가 그 자리에서 센다.
    public static int StoryPartyPlayers { get; private set; }
    public static bool IsSolo { get; private set; }
    bool partyCounted;
    const float PartyCountDelay = 1.5f;

    System.Collections.IEnumerator CountPartyLater()
    {
        yield return new WaitForSecondsRealtime(PartyCountDelay);
        CountParty(announce: true);
    }

    void CountParty(bool announce)
    {
        if (partyCounted || !current.HasValue) return;
        partyCounted = true;
        StoryPartyPlayers = Mathf.Max(1, MatchConfig.Active ? MatchConfig.OccupiedSlots.Count : PlayerContext.OccupiedCount);
        DifficultyMode mode = current.Value;
        if (!DifficultyTable.HasPartySoloAdjust(mode)) return;
        IsSolo = StoryPartyPlayers == 1;
        if (!announce || !GameAuthority.IsServer) return;
        if (IsSolo)
        {
            // 원작: AdjustPlayerStateBJ(10, Player(0), GOLD) — 솔로 = 그 한 명.
            PlayerContext solo = PlayerContext.GetOccupied(0) ?? PlayerContext.Get(LocalPlayer.LocalPlayerId);
            solo?.GoldWallet?.Add(10);
        }
        string text = SoloPartyNotice(mode, IsSolo);
        for (int slot = 0; slot < 4; slot++)
            if (PlayerContext.Get(slot) != null && PlayerContext.Get(slot).IsOccupied) PlayerNotification.Show(slot, text);
        Debug.Log($"[난이도] {mode.KoreanName()} · 인원 {StoryPartyPlayers} · {(IsSolo ? "솔로(스토리 체력 감소, +10엔)" : "파티(스토리 체력 인원만큼 증가)")}");
    }

    // 원작 안내문 그대로(solo_1~4, war3map_new.j 3576~3596).
    static string SoloPartyNotice(DifficultyMode mode, bool solo)
    {
        switch (mode)
        {
            case DifficultyMode.Hard:  return solo ? "하드모드 솔로 플레이 모드입니다. 스토리의 체력 및 방어력이 감소합니다.10골드 추가 획득!" : "어려움 파티 플레이 모드입니다. 스토리 특정 구간의 체력이 파티플레이어 수만큼 증가합니다.";
            case DifficultyMode.Hell:  return solo ? "지옥모드 솔로 플레이 모드입니다. 스토리의 체력 및 방어력이 감소합니다.10골드 추가 획득!" : "지옥모드 파티 플레이 모드입니다. 스토리 특정 구간의 체력이 파티플레이어 수만큼 증가합니다.";
            case DifficultyMode.God:   return solo ? "신모드 솔로 플레이 모드입니다. 스토리의 체력 및 방어력이 감소합니다. 10골드 추가 획득!" : "신 모드 파티 플레이 모드입니다. 스토리의 체력이 파티플레이어 수만큼 증가합니다.";
            default:                   return solo ? "악몽모드 솔로 플레이 모드입니다. 스토리의 체력 및 방어력이 감소합니다. 10골드 추가 획득!" : "악몽 모드 파티 플레이 모드입니다. 스토리의 체력이 파티플레이어 수만큼 증가합니다.";
        }
    }

    /// <summary>
    /// 스토리 체력 인원 배수(2026-10-04 사장님) — 원작 파티 가산이 없는 쉬움·보통에서만 참가 인원 수(혼자 1)를 곱한다. 어려움 이상은 1(원작 +3~15%/인 가산을 StoryHpBonus가 이미 쓴다).
    /// 인원은 CountParty가 판 시작 때 센 값(MP면 MatchConfig 참가 슬롯) — 스토리는 호스트만 소환·체력을 정하고 클라는 SetReplicaHp로 받는다.
    /// </summary>
    const bool StoryPartyScaleEnabled = false;
    public static int StoryPartyScale()
    {
        // 🔴 10-06 사장님 「원작대로」: 스토리 체력·방어를 원작 값으로 바꾸며 이 우리 규칙(쉬움·보통 ×인원)은 끈다 — 원작은 어려움 이상 인원 가산(StoryHpBonus)뿐.
        //    다시 켜려면 이 return 한 줄만 지운다.
        if (!StoryPartyScaleEnabled) return 1;
        DifficultyManager dm = Instance;
        if (dm == null || !dm.current.HasValue || DifficultyTable.HasPartySoloAdjust(dm.current.Value)) return 1;
        dm.CountParty(announce: false);
        return Mathf.Max(1, StoryPartyPlayers);
    }

    /// <summary>스토리 order(1~)의 최대 체력 가산(합, 0.15 = +15%). StoryManager가 스폰 때 EnemyDummy.MarkStoryHpTarget에 넘긴다.</summary>
    public static float StoryHpBonus(int storyOrder)
    {
        DifficultyManager dm = Instance;
        if (dm == null || !dm.current.HasValue) return 0f;
        dm.CountParty(announce: false);   // 1.5초 전에 스토리가 나오면 여기서 센다(안내는 코루틴이 이미 못 띄움 — 첫 스토리는 10초라 실제론 안 걸린다)
        DifficultyMode mode = dm.current.Value;
        int percent = DifficultyTable.StoryHpPercent(mode);
        if (DifficultyTable.HasPartySoloAdjust(mode))
        {
            percent += DifficultyTable.StoryPartyPercent(mode) * StoryPartyPlayers;
            if (IsSolo)
            {
                percent += DifficultyTable.StorySoloPercent;
                if (storyOrder == DifficultyTable.StoryWanoOrder) percent += DifficultyTable.StorySoloExtraPercentWano;
            }
        }
        return percent / 100f;
    }

    // 고른 값을 실제로 거는 부분. SelectMode(사람이 누름)와 Awake(기억해서 시작) 둘 다 쓴다.
    void ApplyMode(DifficultyMode mode)
    {
        current = mode;
        if (isActiveAndEnabled) StartCoroutine(CountPartyLater());

        // Aegr(마법 피해)는 적마다 스폰 시점에 곱하는 게 아니라 전역 레벨 오프셋 하나로
        // 둔다(EnemyDummy.EffectiveMagicMultiplier/AegrBaseLevel 참고) — 원작도 마스터
        // 함수에서 딱 한 번 레인별로 건 뒤 게임 내내 안 바뀐다. 🔴 2026-09-11 정정(PM 리뷰):
        // 배율(CurrentData.magicMultiplier)을 곱하던 걸 레벨 오프셋으로 바꿨다 — 위 Awake
        // 주석과 EnemyDummy.DifficultyAegrLevelOffset 참고.
        EnemyDummy.DifficultyAegrLevelOffset = CurrentData.aegrLevelOffset;

        Debug.Log($"난이도 확정: {mode.KoreanName()}");
    }
}
