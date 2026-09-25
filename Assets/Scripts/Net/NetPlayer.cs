using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 접속자 한 명당 하나. 호스트가 스폰하고 그 접속자에게 입력 권한을 준다.
/// 1-a에서는 「몇 번 슬롯에 앉았나」만 싣는다. 뒤 단계에서 요청 RPC 창구와
/// 플레이어별 [Networked] 상태(골드·목재 등)가 여기에 붙는다.
///
/// 로비(NetBoot)에서 생겨 게임 씬으로 넘어가야 하므로 씬 전환에서 살아남게 한다.
/// </summary>
public class NetPlayer : NetworkBehaviour
{
    /// <summary>레인 번호 = PlayerContext.playerId. 호스트가 스폰 직전에 정한다(NetSession).</summary>
    [Networked] public int Slot { get; set; }

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
        }

        Debug.Log($"[MP] NetPlayer 생성: 슬롯 {Slot} (접속자 {Object.InputAuthority}, 내 것 {HasInputAuthority}) · 현재 슬롯 {{{string.Join(",", MatchConfig.OccupiedSlots)}}}");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        all.Remove(this);
        MatchConfig.RemoveSlot(cachedSlot);
        if (Local == this) Local = null;

        Debug.Log($"[MP] NetPlayer 제거: 슬롯 {cachedSlot}");
    }
}
