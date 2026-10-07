using UnityEngine;

/// <summary>스토리 단계 접두 실측(10-07 구현담당3) — gameshot wait:14 call:StoryLabelProbe.Run: 상태 줄·제한 타이머 이름이 「01. 하이츠」 꼴인지.</summary>
static class StoryLabelProbe
{
    static string Run()
    {
        StoryManager s = StoryManager.Instance;
        if (s == null) return "❌ StoryManager 없음";
        var sb = new System.Text.StringBuilder($"StatusLabel 「{s.StatusLabel}」 · 진행 중 {(s.Running != null ? StoryManager.DisplayName(s.Running) : "없음")}");
        for (int i = 0; i < 13; i++) sb.Append($"\n   {i + 1}번째: 「{StoryManager.DisplayName(s.StoryAt(i))}」");
        return sb.ToString();
    }
}
