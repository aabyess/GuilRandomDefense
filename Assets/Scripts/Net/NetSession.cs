using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 러너 오브젝트에 붙어 접속·이탈을 받는다. 판정은 호스트만 한다.
/// 슬롯 배정: 호스트 = 0, 그 뒤로는 비어 있는 가장 작은 번호(0~3). 슬롯 = 레인 = PlayerContext.playerId.
/// 판이 시작된 뒤(게임 씬 로드 뒤) 들어온 접속자는 끊는다 — 난입·재접속은 3단계.
/// </summary>
public class NetSession : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    public const int MaxSlots = 4;

    [SerializeField] NetworkObject playerPrefab;

    readonly Dictionary<PlayerRef, NetworkObject> players = new Dictionary<PlayerRef, NetworkObject>();
    readonly Dictionary<PlayerRef, int> slots = new Dictionary<PlayerRef, int>();

    public bool MatchStarted { get; set; }

    public int PlayerCount => players.Count;

    public void SetPlayerPrefab(NetworkObject prefab) => playerPrefab = prefab;

    public void PlayerJoined(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (MatchStarted)
        {
            Debug.Log($"[MP] 판이 이미 시작돼 {player}의 접속을 끊습니다.");
            Runner.Disconnect(player);
            return;
        }

        int slot = FreeSlot();
        if (slot < 0)
        {
            Debug.Log($"[MP] 자리가 없어 {player}의 접속을 끊습니다.");
            Runner.Disconnect(player);
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[MP] NetSession: NetPlayer 프리팹이 비어 있습니다.");
            return;
        }

        slots[player] = slot;
        NetworkObject obj = Runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player,
            (runner, spawned) =>
            {
                NetPlayer netPlayer = spawned.GetComponent<NetPlayer>();
                netPlayer.Slot = slot;
                netPlayer.IsHost = player == runner.LocalPlayer;
            });
        players[player] = obj;
        Runner.SetPlayerObject(player, obj);

        Debug.Log($"[MP] 입장: {player} → 슬롯 {slot} (접속 {players.Count}명)");
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (players.TryGetValue(player, out NetworkObject obj))
        {
            if (obj != null) Runner.Despawn(obj);
            players.Remove(player);
        }
        slots.Remove(player);

        Debug.Log($"[MP] 퇴장: {player} (접속 {players.Count}명)");
    }

    int FreeSlot()
    {
        for (int i = 0; i < MaxSlots; i++)
            if (!slots.ContainsValue(i)) return i;
        return -1;
    }
}
