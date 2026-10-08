using System.Reflection;
using UnityEngine;

// 항해일지 「항법」 칸(사장님 10-08) 촬영·점검 — gameshot: select:항해일지 wait:1 snap:a.png call:VoyageNavProbe.Click wait:1 snap:b.png call:VoyageNavProbe.Pick wait:1 snap:c.png call:VoyageNavProbe.Report
static class VoyageNavProbe
{
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
    static GameHud Hud => Object.FindFirstObjectByType<GameHud>();

    static int VisualOfNavSlot()
    {
        var map = (int[])typeof(GameHud).GetField("shopLogicalSlotIndex", NP).GetValue(Hud);
        for (int v = 0; v < map.Length; v++) if (map[v] == VoyageLogShop.NavigationSlot) return v;
        return -1;
    }

    static string Click()
    {
        int v = VisualOfNavSlot();
        if (v < 0) return "❌ 항법 칸이 명령 카드에 없음(항해일지를 고르고 불러야 함)";
        typeof(GameHud).GetMethod("OnShopSlotClicked", NP).Invoke(Hud, new object[] { v });
        var modal = (GameObject)typeof(GameHud).GetField("navigationModalPanel", NP).GetValue(Hud);
        return $"칸 {v} 누름 · 창 열림 {modal != null && modal.activeSelf}";
    }

    static string Pick()
    {
        typeof(GameHud).GetMethod("OnNavigationOptionClicked", NP).Invoke(Hud, new object[] { 2 });
        return "③ 도박광 선택";
    }

    static string Report()
    {
        var shop = Object.FindFirstObjectByType<SelectionManager>().Selected.Count > 0 ? Object.FindFirstObjectByType<SelectionManager>().Selected[0].GetComponent<VoyageLogShop>() : null;
        return Describe(shop, "선택한 항해일지");
    }

    static string ReportOther()
    {
        foreach (var s in Object.FindObjectsByType<VoyageLogShop>(FindObjectsSortMode.None))
            if (s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId != LocalPlayer.LocalPlayerId) return Describe(s, $"남의 항해일지(주인 {o.OwnerId})");
        return "남의 항해일지 없음";
    }

    static string Describe(VoyageLogShop shop, string who)
    {
        if (shop == null) return "❌ " + who + " 없음";
        var top = (TMPro.TMP_Text)typeof(GameHud).GetField("navigationButtonText", NP).GetValue(Hud);
        var view = shop.GetSlotView(VoyageLogShop.NavigationSlot);
        return $"{who} · 상단 단추 「{top.text}」 · 항해일지 칸 「{view.label.Replace('\n', ' ')}」 · 눌림 가능 {view.available} · 눌렀을 때 이유: {shop.GetUnavailableReason(VoyageLogShop.NavigationSlot)}\n툴팁: {shop.GetSlotTooltip(VoyageLogShop.NavigationSlot)}";
    }
}
