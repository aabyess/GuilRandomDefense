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
    public bool IsOccupied => occupied;
    public bool IsDead => isDead;

    public void SetOccupied(bool value)
    {
        occupied = value;
    }

    public void MarkDead()
    {
        isDead = true;
    }

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
