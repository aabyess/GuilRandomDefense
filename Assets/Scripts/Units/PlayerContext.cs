using System.Collections.Generic;
using UnityEngine;

public class PlayerContext : MonoBehaviour
{
    [SerializeField] int playerId;

    // 슬롯 구조는 4명분 다 만들어 두되, 실제로 사람이 앉아 있는지는 따로 본다.
    // 비어 있는 슬롯의 레인에는 적을 스폰하지 않고 보상도 지급하지 않는다.
    [SerializeField] bool occupied = true;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] UnitInventory unitInventory;
    [SerializeField] ResourceWallet resourceWallet;
    [SerializeField] Warehouse warehouse;

    static readonly List<PlayerContext> registry = new List<PlayerContext>();

    public static IReadOnlyList<PlayerContext> All => registry;

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

    public void SetOccupied(bool value)
    {
        occupied = value;
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
