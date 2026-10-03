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

    // 영웅 단추 점검 — 내 유닛 중 영웅 표 대상과 단추 상태를 찍는다(gameshot call:DisplayProbe.HeroState).
    static string HeroState()
    {
        var sb = new StringBuilder();
        foreach (UnitIdentity u in UnitIdentity.Active)
            if (u != null && u.Data != null && u.Data.name.Contains("김민준"))
                sb.AppendLine($"유닛 {u.name} · 주인 {u.OwnerId}(내 번호 {LocalPlayer.LocalPlayerId}) · 데이터 이름 「{u.Data.name}」 · 영웅표 {UnitHeroTable.IsHero(u.Data)}");
        sb.AppendLine($"UnitIdentity.Active {UnitIdentity.Active.Count}개: " + string.Join(" / ", System.Linq.Enumerable.Select(UnitIdentity.Active, u => u == null ? "null" : $"{u.name}|{(u.Data != null ? u.Data.name : "데이터없음")}|주인{u.OwnerId}|영웅{UnitHeroTable.IsHero(u.Data)}")));
        GameObject col = GameObject.Find("HeroButtons");
        sb.AppendLine(col == null ? "HeroButtons 오브젝트 없음" : $"HeroButtons 자식 {col.transform.childCount}개 · 켜진 것 {col.GetComponentsInChildren<UnityEngine.UI.Button>(false).Length}개 · 위치 {((RectTransform)col.transform).anchoredPosition} 크기 {((RectTransform)col.transform).sizeDelta}");
        return sb.ToString();
    }
}
