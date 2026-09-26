using Fusion;
using UnityEngine;

/// <summary>
/// 판 전체에 하나. 호스트가 러너를 띄우자마자 스폰하고, 씬 전환을 넘어 판 끝까지 산다.
/// 지금(1-a') 싣는 것: 대기실에서 호스트가 고른 난이도 · 판 시작 여부.
/// 1-d에서 라운드·타이머·데스카운트가 여기에 붙는다(설계 §5-2, RoundManager는 안 건드리고 HUD가 여기를 읽는다).
/// </summary>
public class NetGameState : NetworkBehaviour
{
    public const int NoDifficulty = -1;

    public static NetGameState Instance { get; private set; }

    /// <summary>DifficultyMode 값. 아직 안 골랐으면 NoDifficulty.</summary>
    [Networked] public int Difficulty { get; set; }

    [Networked] public NetworkBool Started { get; set; }

    public DifficultyMode? SelectedDifficulty =>
        Difficulty >= 0 && System.Enum.IsDefined(typeof(DifficultyMode), Difficulty) ? (DifficultyMode)Difficulty : (DifficultyMode?)null;

    public override void Spawned()
    {
        Instance = this;
        Runner.MakeDontDestroyOnLoad(gameObject);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    public override void Render()
    {
        // 게임 씬의 DifficultyManager.Awake가 읽는다(호스트·클라 모두). 씬 로드 전에 이미 채워져 있어야 해서
        // 값이 바뀔 때만이 아니라 매 프레임 옮겨 둔다(싼 대입 하나).
        MatchConfig.Difficulty = SelectedDifficulty;
    }

    /// <summary>호스트가 방을 닫기 직전에 모두에게 알린다 — 그냥 끊기면 클라는 「연결 끊김」밖에 모른다.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_HostClosing()
    {
        if (NetLauncher.Instance != null) NetLauncher.Instance.OnHostClosing();
    }
}
