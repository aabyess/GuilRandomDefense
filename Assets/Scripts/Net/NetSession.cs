using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 러너 오브젝트에 붙어 접속·이탈을 받는다. 판정은 호스트만 한다.
/// 슬롯 배정: 호스트 = 0, 그 뒤로는 비어 있는 가장 작은 번호(0~3). 슬롯 = 레인 = PlayerContext.playerId.
/// 판이 시작된 뒤(게임 씬 로드 뒤) 들어온 접속자는 끊는다 — **단, 끊김 유예 중인 같은 사람(접속 식별값)은 제자리로 받는다.**
///
/// 재접속(B안, Docs/MULTIPLAYER_RECONNECT_PLAN.md §7, PM 09-26):
///   · [나가기](RPC_LeavingOnPurpose가 먼저 온 퇴장) = 원작 Gone 즉시.
///   · 예고 없는 퇴장(망 끊김·앱 종료) = GraceSeconds 동안 자리를 붙잡는다 — NetPlayer를 거두지 않고, 유닛은 계속 싸우고, 적도 계속 온다.
///     세션을 잠깐 다시 연다(목록엔 안 보임). 같은 식별값이 들어오면 그 NetPlayer의 입력권한을 새 접속에 넘긴다.
///   · 유예가 지나면 그때 원작 Gone.
/// </summary>
public class NetSession : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    public const int MaxSlots = 4;

    [SerializeField] NetworkObject playerPrefab;

    readonly Dictionary<PlayerRef, NetworkObject> players = new Dictionary<PlayerRef, NetworkObject>();
    readonly Dictionary<PlayerRef, int> slots = new Dictionary<PlayerRef, int>();
    readonly Dictionary<PlayerRef, string> tokens = new Dictionary<PlayerRef, string>();
    readonly HashSet<PlayerRef> leavingOnPurpose = new HashSet<PlayerRef>();

    public const float GraceSeconds = 60f;

    class Grace
    {
        public NetworkObject obj;
        public NetPlayer player;
        public string token;
        public string name;
        public float deadline;
    }

    readonly Dictionary<int, Grace> graces = new Dictionary<int, Grace>();

    public bool MatchStarted { get; set; }

    public int PlayerCount => players.Count;

    public void SetPlayerPrefab(NetworkObject prefab) => playerPrefab = prefab;

    public void PlayerJoined(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        string token = ReadToken(player);
        tokens[player] = token;

        if (MatchStarted)
        {
            if (TryReclaim(player, token)) return;
            Debug.Log($"[MP] 판이 이미 시작돼 {player}의 접속을 끊습니다(유예 중인 같은 사람 아님).");
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

        bool onPurpose = leavingOnPurpose.Remove(player);
        tokens.TryGetValue(player, out string token);
        tokens.Remove(player);

        if (players.TryGetValue(player, out NetworkObject obj))
        {
            players.Remove(player);
            NetPlayer leaving = obj != null ? obj.GetComponent<NetPlayer>() : null;

            // 판 도중 예고 없이 끊겼으면 자리를 붙잡는다(유예). 식별값이 없으면(옛 빌드) 알아볼 수 없어 바로 Gone.
            if (MatchStarted && leaving != null && !onPurpose && !string.IsNullOrEmpty(token) && !leaving.Dead)
            {
                StartGrace(leaving, obj, token);
            }
            else
            {
                // 판 도중이면 원작 Gone1~4대로 뒷정리한다(유닛·레인 적 제거, 사망 표식). 이름·슬롯은 거두기 전에 읽는다.
                if (MatchStarted && leaving != null)
                    NetDeparture.Apply(leaving.Slot, leaving.DisplayName);
                if (obj != null) Runner.Despawn(obj);
            }
        }
        slots.Remove(player);

        Debug.Log($"[MP] 퇴장: {player}{(onPurpose ? " ([나가기])" : "")} (접속 {players.Count}명, 유예 {graces.Count}명)");
    }

    /// <summary>NetPlayer.RPC_LeavingOnPurpose: 이 접속자는 곧 [나가기]로 나간다 — 끊김 유예 없이 바로 Gone.</summary>
    public void MarkLeavingOnPurpose(PlayerRef player)
    {
        leavingOnPurpose.Add(player);
        Debug.Log($"[MP] {player}이(가) [나가기]를 알려 왔습니다 — 나가면 바로 정리합니다.");
    }

    void StartGrace(NetPlayer player, NetworkObject obj, string token)
    {
        int slot = player.Slot;
        graces[slot] = new Grace { obj = obj, player = player, token = token, name = player.DisplayName, deadline = Time.unscaledTime + GraceSeconds };
        player.GraceLeft = GraceSeconds;
        // 목록엔 안 보이게 둔 채(IsVisible=false) 입장만 연다 — 코드를 아는 그 사람이 돌아올 수 있게.
        Runner.SessionInfo.IsOpen = true;

        string message = $"<color=#FF8200>{player.DisplayName}</color>님의 연결이 끊겼습니다. {GraceSeconds:F0}초 안에 돌아오면 이어서 합니다.";
        foreach (PlayerContext other in PlayerContext.Occupied)
            if (other.PlayerId != slot) PlayerNotification.Show(other.PlayerId, message, 6f);
        Debug.Log($"[MP] 끊김 유예 시작: 슬롯 {slot}({player.DisplayName}) {GraceSeconds:F0}초");
    }

    bool TryReclaim(PlayerRef player, string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        foreach (KeyValuePair<int, Grace> entry in graces)
        {
            Grace grace = entry.Value;
            if (grace.token != token || grace.obj == null) continue;

            int slot = entry.Key;
            graces.Remove(slot);
            if (graces.Count == 0) Runner.SessionInfo.IsOpen = false;

            // 같은 NetPlayer(골드·특성·도박 상태 그대로)를 새 접속에 넘긴다. 클라는 처음 붙는 것이라 Spawned에서 자기 것으로 받는다.
            grace.obj.AssignInputAuthority(player);
            grace.player.GraceLeft = 0f;
            players[player] = grace.obj;
            slots[player] = slot;
            Runner.SetPlayerObject(player, grace.obj);

            string message = $"<color=#FF8200>{grace.name}</color>님이 다시 들어왔습니다.";
            foreach (PlayerContext other in PlayerContext.Occupied)
                if (other.PlayerId != slot) PlayerNotification.Show(other.PlayerId, message, 5f);
            Debug.Log($"[MP] 재접속: {player} → 슬롯 {slot}({grace.name}) 제자리로(남은 유예 {grace.deadline - Time.unscaledTime:F0}초)");
            return true;
        }
        return false;
    }

    void Update()
    {
        if (Runner == null || !Runner.IsServer || graces.Count == 0) return;

        List<int> expired = null;
        foreach (KeyValuePair<int, Grace> entry in graces)
        {
            Grace grace = entry.Value;
            float left = grace.deadline - Time.unscaledTime;
            if (grace.player != null) grace.player.GraceLeft = Mathf.Max(0f, left);
            if (left <= 0f || grace.obj == null) (expired ??= new List<int>()).Add(entry.Key);
        }
        if (expired == null) return;

        foreach (int slot in expired)
        {
            Grace grace = graces[slot];
            graces.Remove(slot);
            Debug.Log($"[MP] 끊김 유예 끝: 슬롯 {slot}({grace.name}) — 원작대로 퇴장 정리");
            NetDeparture.Apply(slot, grace.name);
            if (grace.obj != null) Runner.Despawn(grace.obj);
        }
        if (graces.Count == 0) Runner.SessionInfo.IsOpen = false;
    }

    string ReadToken(PlayerRef player)
    {
        byte[] bytes = Runner.GetPlayerConnectionToken(player);
        return bytes != null && bytes.Length > 0 ? System.Text.Encoding.UTF8.GetString(bytes) : "";
    }

    int FreeSlot()
    {
        for (int i = 0; i < MaxSlots; i++)
            if (!slots.ContainsValue(i)) return i;
        return -1;
    }
}
