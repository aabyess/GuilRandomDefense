using System.Collections.Generic;
using UnityEngine;

public class PlayerContext : MonoBehaviour
{
    [SerializeField] int playerId;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] UnitInventory unitInventory;
    [SerializeField] ResourceWallet resourceWallet;

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
    public GoldWallet GoldWallet => goldWallet;
    public UnitInventory UnitInventory => unitInventory;
    public ResourceWallet ResourceWallet => resourceWallet;

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
