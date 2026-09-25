using Fusion;

/// <summary>
/// GameAuthority에 꽂히는 Fusion 구현. 러너가 떠 있는 동안만 GameAuthority.Provider에 들어간다
/// (NetSession이 넣고 뺀다). 빠져 있으면 GameAuthority가 싱글로 답한다(IsServer = true).
///
/// 로컬 ID는 LocalPlayer 하나에만 둔다 — UI 대부분이 LocalPlayer.LocalPlayerId를 직접 읽어서,
/// 여기서 따로 들고 있으면 GameAuthority.LocalPlayerId와 값이 갈린다. NetPlayer가 슬롯을 받는
/// 순간 LocalPlayer에 쓴다.
/// </summary>
public class FusionAuthorityProvider : IAuthorityProvider
{
    readonly NetworkRunner runner;

    public FusionAuthorityProvider(NetworkRunner runner)
    {
        this.runner = runner;
    }

    public bool IsServer => runner != null && runner.IsServer;

    public int LocalPlayerId => LocalPlayer.LocalPlayerId;
}
