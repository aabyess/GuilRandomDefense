using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 접속자 한 명당 하나. 호스트가 스폰하고 그 접속자에게 입력 권한을 준다.
/// 싣는 것: 슬롯(레인) · 대기실 정보(닉네임·준비). 뒤 단계에서 요청 RPC 창구와
/// 플레이어별 [Networked] 상태(골드·목재 등)가 여기에 붙는다.
///
/// 로비(NetBoot)에서 생겨 게임 씬으로 넘어가야 하므로 씬 전환에서 살아남게 한다.
/// </summary>
public class NetPlayer : NetworkBehaviour
{
    public const int MaxNicknameLength = 12;
    public const string NicknamePrefsKey = "GuilRandomDefense.Nickname";

    /// <summary>레인 번호 = PlayerContext.playerId. 호스트가 스폰 직전에 정한다(NetSession).</summary>
    [Networked] public int Slot { get; set; }

    /// <summary>방을 연 사람의 것인가. 호스트는 [준비] 없이 늘 준비된 것으로 본다.</summary>
    [Networked] public NetworkBool IsHost { get; set; }

    [Networked] public NetworkString<_16> Nickname { get; set; }

    [Networked] public NetworkBool Ready { get; set; }

    // ───── 게임 중 복제(1-d): 호스트가 PlayerContext에서 읽어 쓰고, 클라가 자기 쪽 같은 컴포넌트에 적는다 ─────
    // 클라 GameHud는 지금처럼 PlayerContext의 지갑을 읽으면 된다(파일 무수정).
    public const int ResourceSlots = 8;
    [Networked] public int Gold { get; set; }
    [Networked, Capacity(ResourceSlots)] public NetworkArray<int> Resources => default;
    [Networked] public NetworkBool Dead { get; set; }

    public bool IsReadyForStart => IsHost || Ready;

    public string DisplayName
    {
        get
        {
            string nick = Nickname.ToString();
            return string.IsNullOrWhiteSpace(nick) ? $"플레이어 {Slot + 1}" : nick;
        }
    }

    static readonly List<NetPlayer> all = new List<NetPlayer>();

    public static IReadOnlyList<NetPlayer> All => all;

    /// <summary>이 PC의 NetPlayer. 아직 안 생겼으면 null.</summary>
    public static NetPlayer Local { get; private set; }

    // Despawned에서는 [Networked] 값을 못 읽을 수 있다(hasState=false) — 받아 둔 값을 쓴다.
    int cachedSlot = -1;

    public override void Spawned()
    {
        all.Add(this);
        cachedSlot = Slot;
        Runner.MakeDontDestroyOnLoad(gameObject);
        MatchConfig.AddSlot(Slot);

        if (HasInputAuthority)
        {
            Local = this;
            LocalPlayer.LocalPlayerId = Slot;
            RPC_SetNickname(NetLauncher.Instance != null ? NetLauncher.Instance.InitialNickname : LoadNickname());
        }

        Debug.Log($"[MP] NetPlayer 생성: 슬롯 {Slot} (접속자 {Object.InputAuthority}, 내 것 {HasInputAuthority}, 호스트 {IsHost}) · 현재 슬롯 {{{string.Join(",", MatchConfig.OccupiedSlots)}}}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        PlayerContext context = PlayerContext.Get(Slot);
        if (context == null) return;   // 대기실(게임 씬 전)

        if (context.GoldWallet != null) Gold = context.GoldWallet.Gold;
        if (context.ResourceWallet != null)
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
                if ((int)type < ResourceSlots) Resources.Set((int)type, context.ResourceWallet.Get(type));
        Dead = context.IsDead;
    }

    public override void Render()
    {
        if (HasStateAuthority) return;   // 호스트는 실물이 곧 값이다

        PlayerContext context = PlayerContext.Get(Slot);
        if (context == null) return;

        if (context.GoldWallet != null) context.GoldWallet.ApplyReplicated(Gold);
        if (context.ResourceWallet != null)
            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
                if ((int)type < ResourceSlots) context.ResourceWallet.ApplyReplicated(type, Resources[(int)type]);
        if (Dead && !context.IsDead) context.MarkDead();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        all.Remove(this);
        MatchConfig.RemoveSlot(cachedSlot);
        if (Local == this) Local = null;

        Debug.Log($"[MP] NetPlayer 제거: 슬롯 {cachedSlot}");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetNickname(string nickname)
    {
        Nickname = SanitizeNickname(nickname);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(NetworkBool ready)
    {
        // 판이 시작된 뒤엔 준비 상태를 바꿀 일이 없다.
        if (NetGameState.Instance != null && NetGameState.Instance.Started) return;
        Ready = ready;
    }

    // ───────────── 게임 중 요청(1-c~) ─────────────

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_Move(NetworkId target, Vector3 groundPoint)
    {
        NetCommands.ExecuteMove(this, target, groundPoint);
    }

    /// <summary>호스트 → 이 접속자: 그 사람 앞으로 온 안내(PlayerNotification)를 넘긴다. 등급색 태그 그대로.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    public void RPC_Notify(string message, float duration)
    {
        PlayerNotification.Show(LocalPlayer.LocalPlayerId, message, duration);
        if (notificationsLogged++ < 10) Debug.Log($"[MP] 알림 받음: {message}");
    }

    static int notificationsLogged;

    public static string LoadNickname()
    {
        try { return PlayerPrefs.GetString(NicknamePrefsKey, ""); }
        catch { return ""; }
    }

    public static void SaveNickname(string nickname)
    {
        try
        {
            PlayerPrefs.SetString(NicknamePrefsKey, SanitizeNickname(nickname));
            PlayerPrefs.Save();
        }
        catch { /* 편의값이라 못 저장해도 판에는 영향 없다 */ }
    }

    public static string SanitizeNickname(string nickname)
    {
        string trimmed = (nickname ?? "").Trim().Replace("\n", "").Replace("\r", "");
        return trimmed.Length > MaxNicknameLength ? trimmed.Substring(0, MaxNicknameLength) : trimmed;
    }
}
