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
