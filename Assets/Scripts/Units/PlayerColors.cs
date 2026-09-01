using UnityEngine;

/// <summary>
/// 플레이어를 구분하는 색. 선택 표시·미니맵·유닛 몸 색이 전부 여기서 가져간다.
/// 워크·스타처럼 "저 색은 저 사람"이 게임 내내 한결같아야 해서 한 곳에 모아둔다.
/// </summary>
public static class PlayerColors
{
    // 사장님 확정 2026-09-02: 1번부터 파랑·빨강·노랑·초록.
    static readonly Color[] Palette =
    {
        new Color(0.25f, 0.55f, 1.00f),   // 플레이어 1 — 파랑
        new Color(0.95f, 0.25f, 0.25f),   // 플레이어 2 — 빨강
        new Color(1.00f, 0.85f, 0.20f),   // 플레이어 3 — 노랑
        new Color(0.30f, 0.85f, 0.35f),   // 플레이어 4 — 초록
    };

    // 소유자가 없는 것(중립·디버그용)에 쓴다.
    static readonly Color Neutral = new Color(0.75f, 0.75f, 0.75f);

    public static Color Get(int playerId)
    {
        if (playerId < 0 || playerId >= Palette.Length) return Neutral;
        return Palette[playerId];
    }

    /// <summary>슬롯 수. 색을 다 쓰면 플레이어를 더 못 늘린다는 뜻이기도 하다.</summary>
    public static int Count => Palette.Length;
}
