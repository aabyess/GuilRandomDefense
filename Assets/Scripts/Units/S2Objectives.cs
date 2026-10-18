/// <summary>
/// S2 맵 목표물(7시 샨도라 황금 종·1시 룸바 해적단의 영혼)의 파괴 이벤트 — 구현담당2의 미션판이 구독한다.
/// 호스트에서만 한 번 발생한다. 인자 = 파괴한 플레이어 번호(모르면 -1). 보상·표시는 미션판 몫.
/// </summary>
public static class S2Objectives
{
    public static event System.Action<int> GoldenBellDestroyed;
    public static event System.Action<int> RumbarSoulFreed;

    public static void RaiseGoldenBellDestroyed(int byPlayer) => GoldenBellDestroyed?.Invoke(byPlayer);
    public static void RaiseRumbarSoulFreed(int byPlayer) => RumbarSoulFreed?.Invoke(byPlayer);
}
