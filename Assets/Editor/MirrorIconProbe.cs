using UnityEngine;

/// <summary>부서진손거울 아이템 칸 확인(10-06 구현담당3) — call:MirrorIconProbe.Give 로 내 아이템 칸에 손거울을 하나 넣는다(에디터 전용).</summary>
public static class MirrorIconProbe
{
    public static string Give()
    {
        ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Data/Items/ItemData_R001_부서진손거울.asset");
        PlayerContext me = PlayerContext.Local;
        if (item == null || me == null || me.ItemInventory == null) return "❌ 아이템·지갑 없음";
        bool ok = me.ItemInventory.Add(item);
        return $"   손거울 추가 {ok} · 아이콘 {(item.icon != null ? item.icon.name : "없음")} · 칸 수 {me.ItemInventory.Items.Count}";
    }
}
