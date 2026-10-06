using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 유닛강화소 재배치 점검(10-06) — gameshot: call:ShopSlotProbe.Fund wait:25 call:UpgradeShopProbe.Select wait:2 call:UpgradeShopProbe.Layout call:UpgradeShopProbe.Hidden1 call:UpgradeShopProbe.Buy wait:1 call:UpgradeShopProbe.Hidden2
static class UpgradeShopProbe
{
    static readonly int[] SlotOrder = { 8, 9, 10, 11, 4, 5, 6, 7, 3 };
    static readonly char[] Keys = { 'Q', 'W', 'E', 'R', 'A', 'S', 'D', 'F', 'Z', 'X', 'C', 'V' };
    static UnitUpgradeShop shop; static UnitAttacker hid; static float as0, dmg0;

    static UnitUpgradeShop FindShop()
    {
        foreach (UnitUpgradeShop s in Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None))
            if (s.SlotCount >= 8 && s.name.EndsWith("유닛강화소") && s.name.Contains("Lane1") && !s.name.Contains("다른세계") && s.TryGetComponent(out OwnedByPlayer o) && o.OwnerId == 0) return s;
        return null;
    }

    static string Pick(string part)
    {
        foreach (Selectable sel in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            if (sel.name.StartsWith("Lane1_") && sel.name.Contains(part))
            {
                Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel);
                return $"선택: {sel.name}";
            }
        return $"❌ {part} 없음";
    }
    static string AnyUnit()
    {
        foreach (UnitIdentity u in UnitIdentity.Active)
            if (u != null && !u.IsSummon && u.TryGetComponent(out Selectable sel)) { Object.FindFirstObjectByType<SelectionManager>().SelectOnly(sel); return $"선택: {u.name}"; }
        return "❌ 유닛 없음";
    }
    static string Gamble() => Pick("도박소");
    static string Support() => Pick("도움소");
    static string AttackType() => Pick("공격타입");
    static string Eternal() => Pick("영원함강화소");
    static string Voyage() => Pick("항해일지");
    static string OtherWorld() => Pick("다른세계강화소");
    static string Unit() => Pick("_유닛강화소");

    static string Select()
    {
        shop = FindShop();
        if (shop == null) return "❌ 내 유닛강화소 없음";
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(shop.GetComponent<Selectable>());
        return $"선택: {shop.name} · 칸 {shop.SlotCount}";
    }

    static string Layout()
    {
        var sb = new StringBuilder("[칸 배치] ");
        for (int i = 0; i < shop.SlotCount; i++)
        {
            var v = shop.GetSlotView(i);
            sb.Append($"{Keys[SlotOrder[i]]}={v.label.Replace("\n", " ")} / ");
        }
        return sb.ToString();
    }

    static string Hidden1()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        var d = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/히든_한나웅.asset");
        var go = spawner.Spawn(d, lane != null ? lane.TakeSpawnPosition(d) : Vector3.zero, 0);
        hid = go.GetComponent<UnitAttacker>();
        as0 = hid.CurrentAttackSpeedMultiplier; dmg0 = hid.AttackDamage;
        return $"[히든 유닛 {d.DisplayName}] 공속 배율 {as0:F3} · 공격력 {dmg0:F0}";
    }

    static string Buy()
    {
        int idx = -1;
        for (int i = 0; i < shop.SlotCount; i++) if (shop.GetSlotView(i).label.StartsWith("전설·히든")) idx = i;
        bool ok = shop.TryUse(idx, default, out string why);
        return $"[전설·히든 강화 1렙 구매] 칸 {idx} · {(ok ? "성공" : "실패 " + why)}";
    }

    static string Hidden2() => $"[구매 후 히든 유닛] 공속 배율 {as0:F3} → {hid.CurrentAttackSpeedMultiplier:F3} · 공격력 {dmg0:F0} → {hid.AttackDamage:F0}(원작 전설 1렙: 공속 +1.95 → ×2.95, 공격력 +7250)";
}
