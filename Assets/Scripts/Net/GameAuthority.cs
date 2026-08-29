public interface IAuthorityProvider
{
    bool IsServer { get; }
    int LocalPlayerId { get; }
}

// "이 코드가 서버(호스트)에서 실행 중인가"를 한 곳에서 답한다.
// Provider가 없으면(싱글플레이) IsServer는 항상 true라서 기존 동작이 그대로 유지된다.
// 나중에 FusionAuthorityProvider : IAuthorityProvider를 만들어 Provider에 주입하면 된다.
public static class GameAuthority
{
    public static IAuthorityProvider Provider { get; set; }
    public static bool IsServer => Provider?.IsServer ?? true;
    public static int LocalPlayerId => Provider?.LocalPlayerId ?? LocalPlayer.LocalPlayerId;
}
