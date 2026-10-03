using System.Text;
using UnityEngine;

// 표시류 점검(막타 청록 글자·클리어 칭호) — gameshot call:DisplayProbe.Run → wait → snap
static class DisplayProbe
{
    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var sb = new StringBuilder();
        foreach (int n in new[] { 0, 1, 5, 6, 90, 91, 250, 251, 300, 301 })
            sb.AppendLine($"  클리어 {n}회 → 「{PlayerDisplayName.ClearTitleOf(n)}」");
        PlayerContext me = PlayerContext.Get(0);
        me.PersistentSave.Data.cumulativeClearCount = 36;
        sb.AppendLine("이름(36회): " + PlayerDisplayName.Of(0));
        PlayerNotification.Show(0, $"<color=#FF8200>{PlayerDisplayName.Of(0)}</color> <color=#FF8200>님이 스토리를 깼습니다</color>", 15f);
        Camera cam = Camera.main;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 p = Physics.Raycast(ray, out RaycastHit hit, 5000f) ? hit.point : ray.GetPoint(300f);
        sb.AppendLine($"팝업 기준점 {p:F1} (맞힌 것: {(hit.collider != null ? hit.collider.name : "없음")}) · 로컬 플레이어 {LocalPlayer.LocalPlayerId}");
        KillGoldPopup.Show(0, p + Vector3.left * 8f, 120);
        KillGoldPopup.Show(0, p + Vector3.right * 8f, 1, wood: true);
        KillGoldPopup.Show(0, p + Vector3.right * 20f, 10, wood: true);
        return sb.ToString();
    }
}
