using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 흐린 [조합] 버튼 부족 문구 점검(10-06 구현담당3 · 사장님 「조합할 때 뭐 없으면 ㅇㅇ부족 알려 달라」) — gameshot call:로 부른다. 에디터 전용.
///   call:ShortageProbe.Setup      호시노 루비 재료 여섯 중 하나만 우리에 세운다(그 유닛 하나를 선택 · 돈·목재 0)
///   call:ShortageProbe.Click      선택 유닛의 조합 칸을 진짜 클릭 경로(EventSystem)로 누르고 알림 문구를 적는다
///   call:ShortageProbe.SpawnRest  나머지 재료를 세운다(재료 다 갖춤)
///   call:ShortageProbe.Reselect   재료 하나를 다시 선택(HUD 조합 칸 새로 읽기)
///   call:ShortageProbe.Fund       돈만 채운다(목재 0 → 목재 부족 기대)
/// 칸을 누를 때마다 PlayerNotification.Shown을 가로채 문구를 모은다. wait:1을 사이에 둬 HUD가 갱신될 틈을 준다.
/// </summary>
public static class ShortageProbe
{
    const string RecipePath = "Assets/Data/Recipes/다른세계_호시노_루비.asset";

    static CombineRecipe Recipe => AssetDatabase.LoadAssetAtPath<CombineRecipe>(RecipePath);

    static void Spawn(UnitData data, StringBuilder sb)
    {
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        GameObject go = spawner.Spawn(data, lane.TakeSpawnPosition(data), 0);
        sb.AppendLine($"   소환 {data.name} → {(go != null ? go.name : "null")}");
    }

    static void SelectFirst(StringBuilder sb)
    {
        UnitData first = Recipe.ingredients[0].unit;
        Selectable pick = Selectable.All.FirstOrDefault(s => s.TryGetComponent(out UnitIdentity id) && id.Data == first);
        SelectionManager sel = Object.FindFirstObjectByType<SelectionManager>();
        if (pick == null || sel == null) { sb.AppendLine($"   ❌ 선택 실패(유닛 {pick != null} · 매니저 {sel != null})"); return; }
        sel.SelectOnly(pick);
        sb.AppendLine($"   선택 {pick.name}");
    }

    static void ZeroWallets(PlayerContext me)
    {
        me.GoldWallet.TrySpend(me.GoldWallet.Gold);
        me.ResourceWallet.TrySpend(ResourceType.Wood, me.ResourceWallet.Get(ResourceType.Wood));
    }

    public static string Setup()
    {
        PlayerContext me = PlayerContext.Local;
        if (me == null) return "❌ PlayerContext.Local 없음";
        StringBuilder sb = new StringBuilder($"   레시피 {Recipe.name} · 돈 {Recipe.goldCost} · 자원 {string.Join(",", Recipe.resourceCosts.Select(c => c.type + " " + c.amount))}\n");
        Spawn(Recipe.ingredients[0].unit, sb);
        ZeroWallets(me);
        SelectFirst(sb);
        sb.AppendLine($"   지갑 돈 {me.GoldWallet.Gold} · 목재 {me.ResourceWallet.Get(ResourceType.Wood)}");
        return sb.ToString();
    }

    public static string SpawnRest()
    {
        StringBuilder sb = new StringBuilder();
        foreach (RecipeIngredient ing in Recipe.ingredients.Skip(1)) Spawn(ing.unit, sb);
        SelectFirst(sb);
        return sb.ToString();
    }

    public static string Reselect() { StringBuilder sb = new StringBuilder(); SelectFirst(sb); return sb.ToString(); }

    public static string Fund()
    {
        PlayerContext me = PlayerContext.Local;
        me.GoldWallet.Add(Recipe.goldCost);
        return $"   지갑 돈 {me.GoldWallet.Gold} · 목재 {me.ResourceWallet.Get(ResourceType.Wood)}";
    }

    public static string Click()
    {
        PlayerContext me = PlayerContext.Local;
        GameHud hud = Object.FindFirstObjectByType<GameHud>();
        FieldInfo recipesField = typeof(GameHud).GetField("unitCommandRecipes", BindingFlags.NonPublic | BindingFlags.Instance);
        CombineRecipe[] recipes = (CombineRecipe[])recipesField.GetValue(hud);
        var notes = new List<string>();
        System.Action<int, string, float> hook = (pid, msg, d) => notes.Add($"{msg} ({d}s)");
        PlayerNotification.Shown += hook;
        StringBuilder sb = new StringBuilder($"   돈 {me.GoldWallet.Gold} · 목재 {me.ResourceWallet.Get(ResourceType.Wood)} · 유닛 {me.UnitInventory.Units.Count}\n");
        try
        {
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(x => x.name.StartsWith("UnitCommandSlot")))
            {
                if (!int.TryParse(b.name.Substring("UnitCommandSlot".Length), out int i) || i >= recipes.Length || recipes[i] == null) continue;
                notes.Clear();
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));
                ExecuteEvents.Execute(b.gameObject, new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                sb.AppendLine($"   {b.name} → {recipes[i].name} · can {hud != null} · 알림: {(notes.Count == 0 ? "(없음)" : string.Join(" / ", notes))}");
            }
        }
        finally { PlayerNotification.Shown -= hook; }
        return sb.ToString();
    }
}
