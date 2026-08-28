using System.Collections.Generic;
using UnityEngine;

public class PlayerContext : MonoBehaviour
{
    [SerializeField] int playerId;
    [SerializeField] GoldWallet goldWallet;
    [SerializeField] UnitInventory unitInventory;

    static readonly List<PlayerContext> All = new List<PlayerContext>();

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

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public static PlayerContext Get(int playerId)
    {
        foreach (PlayerContext context in All)
        {
            if (context.playerId == playerId) return context;
        }

        return null;
    }
}
