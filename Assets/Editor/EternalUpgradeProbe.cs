using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// 영원함 유닛 전용 강화 실측(10-06 구현담당3) — gameshot: call:ShopSlotProbe.Fund call:EternalUpgradeProbe.Setup call:EternalUpgradeProbe.Before call:EternalUpgradeProbe.Buy call:EternalUpgradeProbe.After
/// 김영원·조세민(같은 영원함)·구주호(희귀함, 다른 등급)를 세우고, 영원함 강화소에서 김영원 칸만 3번 산다 → 김영원만 공속·공격력 가산이 오르고 나머지는 그대로여야 한다.
/// </summary>
public static class EternalUpgradeProbe
{
    static readonly string[] Names = { "영원_김영원", "영원_조세민", "희귀함_구주호" };
    static UnitAttacker[] attackers = new UnitAttacker[3];

    public static string Setup()
    {
        for (int i = 0; i < 3; i++)
        {
            UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{Names[i]}.asset");
            GameObject go = Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, LaneMarker.Get(0).TakeSpawnPosition(d), 0);
            attackers[i] = go.GetComponent<UnitAttacker>();
        }
        return "   세움: " + string.Join(", ", Names);
    }

    static string Read(UnitAttacker a)
    {
        var t = typeof(UnitAttacker);
        float spd = (float)t.GetProperty("ResearchSpeedMultiplier", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
        float bonus = (float)t.GetProperty("ResearchBonus", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a);
        return $"공속 ×{spd:F2} · 공격력 가산 +{bonus:F0}";
    }

    static string Dump(string head)
    {
        var sb = new StringBuilder($"   {head}\n");
        for (int i = 0; i < 3; i++) sb.AppendLine($"     {Names[i]}: {Read(attackers[i])}");
        return sb.ToString();
    }

    public static string Before() => Dump("구매 전");

    static UnitUpgradeShop Shop() => Object.FindObjectsByType<UnitUpgradeShop>(FindObjectsSortMode.None).FirstOrDefault(s => s.name == "Lane1_영원함강화소");

    public static string Buy()
    {
        UnitUpgradeShop shop = Shop();
        if (shop == null) return "❌ Lane1_영원함강화소 없음";
        var sb = new StringBuilder($"   영원함 강화소 칸 {shop.SlotCount}개: ");
        for (int i = 0; i < shop.SlotCount; i++) sb.Append($"[{shop.GetSlotView(i).label.Replace("\n", " ")}] ");
        sb.AppendLine();
        int slot = -1;
        for (int i = 0; i < shop.SlotCount; i++) if (shop.GetSlotView(i).label.StartsWith("김영원")) slot = i;
        if (slot < 0) return sb + "❌ 김영원 칸 없음";
        int before = PlayerContext.Local.GoldWallet.Gold;
        for (int n = 0; n < 3; n++)
        {
            bool ok = shop.TryUse(slot, default, out string why);
            sb.AppendLine($"   구매 {n + 1}: {(ok ? "성공" : "실패 " + why)} · 칸 「{shop.GetSlotView(slot).label.Replace("\n", " ")}」");
        }
        sb.AppendLine($"   엔 {before} → {PlayerContext.Local.GoldWallet.Gold} (기대 −{110 + 80 + 80})");
        sb.AppendLine($"   툴팁: {shop.GetSlotTooltip(slot).Replace("\n", " / ")}");
        return sb.ToString();
    }

    public static string After() => Dump("김영원 강화 3렙 뒤(기대: 김영원 공속 ×1.60 [1+0.15+0.15×2] · 가산 +13,500, 나머지 그대로)");
}
