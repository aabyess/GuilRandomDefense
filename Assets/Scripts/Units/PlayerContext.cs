using System.Collections.Generic;
using UnityEngine;

public class PlayerContext : MonoBehaviour
{
    [SerializeField] int playerId;

    // 슬롯 구조는 4명분 다 만들어 두되, 실제로 사람이 앉아 있는지는 따로 본다.
    // 비어 있는 슬롯의 레인에는 적을 스폰하지 않고 보상도 지급하지 않는다.
    [SerializeField] bool occupied = true;

    // 레인 하나에 적이 100마리 넘게 쌓여 카운트다운이 0이 되면 죽는다(RoundManager 지시,
    // 2026-09-03). 한 방향이다 — 되돌리는 메서드가 없다. 부활은 아직 없는 개념이라 만들지 않았다.
    bool isDead;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] UnitInventory unitInventory;
    [SerializeField] ResourceWallet resourceWallet;
    [SerializeField] Warehouse warehouse;
    [SerializeField] GamblingProgress gamblingProgress;
    [SerializeField] UnitUpgrades unitUpgrades;
    [SerializeField] PersistentSave persistentSave;
    [SerializeField] ItemGambleState itemGambleState;
    // ⚠️ 맨 뒤에 추가(2026-09-06, "항법" 5택1, NAVIGATION_ROUTES_FULL.md) — 직렬화
    // 순서를 지킨다.
    [SerializeField] NavigationState navigationState;
    [SerializeField] DamageLevelFixedState damageLevelFixedState;
    // ⚠️ 맨 뒤에 추가(2026-09-07, "희귀함 리롤" A0VX, UNIQUE_REROLE_AND_SELL_FAMILY.md) —
    // 직렬화 순서를 지킨다. 씬에서 같은 슬롯(GameObject)에 같이 붙는다 — 비어 있으면
    // (씬 배선 전) UniqueRerollAbility.TryCast가 조용히 false를 돌려준다(회귀 없음).
    [SerializeField] UniqueRerollState uniqueRerollState;
    // ⚠️ 맨 뒤에 추가(2026-09-09, ITEM_SYSTEM_AUDIT_2026-09-09.md) — 직렬화 순서를 지킨다.
    //
    // 🔴 여기 오기 전까지 ItemInventory는 씬에 **하나뿐**이었다(플레이어 0 슬롯). GameHud가
    //    FindFirstObjectByType으로 그 하나를 잡고 CombineSystem도 같은 인스턴스를 써서,
    //    누가 도박에 이겨도 아이템이 전부 플레이어 0에게 들어가고 조합도 그 하나를 뒤졌다.
    //    원작은 위에서 아래까지 전부 플레이어별이다 — itpool[pid]로 풀을 고르고,
    //    ItemGet이 `UnitAddItem(v, ...)`으로 그 유닛에게 직접 넣고, 획득 메시지도
    //    그 소유자에게만 띄우며, 아이템 보유형 해금도 GetOwningPlayer(GetTriggerUnit())로
    //    인덱싱한다. 형제인 ItemGambleState는 이미 4개인데 인벤토리만 1개였던 것이다.
    [SerializeField] ItemInventory itemInventory;

    static readonly List<PlayerContext> registry = new List<PlayerContext>();

    public static IReadOnlyList<PlayerContext> All => registry;

    /// <summary>
    /// 실제로 사람이 앉아 있는 슬롯만. 자원·위습을 나눠줄 땐 거의 항상 이쪽이다 —
    /// 빈 자리에 주면 아무도 안 쓰는 채로 쌓이고, 위습은 필드에 실물로 나와
    /// 내 것과 섞여 어느 게 내 것인지 알 수 없게 된다.
    /// </summary>
    public static IEnumerable<PlayerContext> Occupied
    {
        get
        {
            foreach (PlayerContext context in registry)
                if (context != null && context.occupied) yield return context;
        }
    }

    public static int OccupiedCount
    {
        get
        {
            int count = 0;
            foreach (PlayerContext context in registry)
                if (context != null && context.occupied) count++;
            return count;
        }
    }

    public static PlayerContext Local
    {
        get
        {
            PlayerContext context = Get(LocalPlayer.LocalPlayerId);
            if (context == null)
            {
                Debug.LogWarning($"PlayerContext: LocalPlayerId({LocalPlayer.LocalPlayerId})에 해당하는 PlayerContext가 씬에 없습니다.");
            }

            return context;
        }
    }

    public int PlayerId => playerId;

    // 도전과제 「패스트 유니크」(원작 udg_FU[플레이어]) — 8라운드 전에 처음 희귀함을 얻으면 한 번만. 한 판짜리.
    public bool FastUniqueGranted { get; set; }

    // 변화(A0KJ) 회수 — 원작은 플레이어마다 시작에 h07D(변화 토큰) 2기를 주고(생성 경로 없음), 변화가 성공할 때마다 소유자 토큰을 1기 제거한다
    // (Trig_change j:20362, 조건 「목재 ≥10 + 토큰 ≥1」) → **플레이어당 한 판 2회**. 변화됨 조합(결과 등급 Transformed)이 이 회수를 쓴다.
    // 한 판짜리(직렬화 안 함). 호스트가 정하고 클라는 NetPlayer가 옮겨 적는다(HUD 표시용).
    public const int TransformUsesPerGame = 2;
    public int TransformUsesLeft { get; private set; } = TransformUsesPerGame;
    public bool TryConsumeTransformUse()
    {
        if (TransformUsesLeft <= 0) return false;
        TransformUsesLeft--;
        return true;
    }
    public void ApplyReplicatedTransformUses(int left) { TransformUsesLeft = Mathf.Clamp(left, 0, TransformUsesPerGame); }
    public bool IsOccupied => occupied;
    public bool IsDead => isDead;

    public void SetOccupied(bool value)
    {
        occupied = value;
    }

    public void MarkDead(string defeatMessage = null)
    {
        isDead = true;
        if (!string.IsNullOrEmpty(defeatMessage)) DefeatMessage = defeatMessage;
    }

    /// <summary>왜 졌는지 — 원작 CustomDefeat·패배 문구(색 태그 없는 한 줄). 패배 화면(DefeatOverlay)이 쓴다. 멀티 클라는 NetPlayer가 옮겨 적는다.</summary>
    public string DefeatMessage { get; private set; }

    // 초월 엄태웅 「웅교교주」(사장님 10-06): 엔 10000을 내고 도박 성공 확률 +4%p, 최대 5회 — 플레이어 개인 누적(여러 기를 눌러도 합산).
    // 100%도 0%도 아닌 도박에만 적용(GamblingShop.EffectiveSuccessChance). 세이브 없는 한 판짜리 상태라 런타임 값만 둔다.
    public const int GambleBoostMax = 5;
    public const float GambleBoostPercentEach = 4f;
    public const int GambleBoostCost = 10000;
    public int GambleBoostCount { get; private set; }
    public float GambleBoostPercent => GambleBoostCount * GambleBoostPercentEach;
    public bool TryAddGambleBoost() { if (GambleBoostCount >= GambleBoostMax) return false; GambleBoostCount++; return true; }

    // 도움소 「능력치 증가」(H0B7) 선행 조건 — 원작 Rhfl(초월함 조합 완료). 세이브/로드가
    // 없는 한 판짜리 진행 상태라 GamblingProgress와 같은 결로 런타임 bool만 둔다.
    // CombineSystem.TryCombine이 결과 등급이 초월함일 때 세운다.
    public bool HasCompletedTranscendentCombine { get; private set; }

    public void MarkTranscendentCombineCompleted()
    {
        HasCompletedTranscendentCombine = true;
    }

    /// <summary>해당 슬롯에 실제 플레이어가 있으면 그 컨텍스트를, 비어 있으면 null.</summary>
    public static PlayerContext GetOccupied(int playerId)
    {
        PlayerContext context = Get(playerId);
        return context != null && context.occupied ? context : null;
    }
    public GoldWallet GoldWallet => goldWallet;
    public UnitInventory UnitInventory => unitInventory;
    public ResourceWallet ResourceWallet => resourceWallet;
    public Warehouse Warehouse => warehouse;
    public GamblingProgress GamblingProgress => gamblingProgress;
    public UnitUpgrades UnitUpgrades => unitUpgrades;
    public PersistentSave PersistentSave => persistentSave;
    public ItemGambleState ItemGambleState => itemGambleState;
    public NavigationState NavigationState => navigationState;
    public DamageLevelFixedState DamageLevelFixedState => damageLevelFixedState;
    public UniqueRerollState UniqueRerollState => uniqueRerollState;
    public ItemInventory ItemInventory => itemInventory;

    // MP: 네트 판이면 접속자 좌석(MatchConfig)이 occupied를 정한다. RewardDistributor.Start가
    //     씬 로드 즉시 Occupied에 위습을 뿌리므로 Start보다 앞선 Awake여야 한다.
    //     싱글(러너 없음 → MatchConfig.Active false)이면 씬에 직렬화된 값 그대로 — 무동작.
    void Awake()
    {
        if (MatchConfig.Active) occupied = MatchConfig.IsOccupied(playerId);
    }

    void OnEnable()
    {
        registry.Add(this);
    }

    void OnDisable()
    {
        registry.Remove(this);
    }

    public static PlayerContext Get(int playerId)
    {
        foreach (PlayerContext context in registry)
        {
            if (context.playerId == playerId) return context;
        }

        return null;
    }
}
