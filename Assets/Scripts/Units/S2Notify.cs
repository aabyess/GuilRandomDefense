/// <summary>알림 자리는 줄 높이가 하나라 「\n」이 든 문구는 다음 줄과 겹친다(10-18 실측) — S2 규칙들의 여러 줄 문구는 한 줄씩 나눠 올린다.</summary>
public static class S2Notify
{
    public static void Show(int playerId, string message, float duration = 4f)
    {
        foreach (string line in (message ?? "").Split('\n'))
            if (line.Length > 0) PlayerNotification.Show(playerId, line, duration);
    }
}
