using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 조합 결과 위치 + 복사본 판매 확인(구현담당1, 10-09). gameshot x.png 1 1920x1080 click?:쉬움 mode:쉬움 wait:2 call:CombinePlaceProbe.Warehouse wait:1 call:CombinePlaceProbe.Copy wait:1
static class CombinePlaceProbe
{
    static string TryGrade(UnitGrade grade)
    {
        var system = Object.FindFirstObjectByType<CombineSystem>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var ctx = PlayerContext.Get(0);
        var lane = LaneMarker.Get(0);
        foreach (CombineRecipe r in system.Recipes)
        {
            if (r == null || r.result == null || r.result.grade != grade) continue;
            if (r.ingredients == null || r.ingredients.Count == 0 || r.ingredients.Any(i => i.kind != IngredientKind.SpecificUnit || i.unit == null || i.alternativeUnit != null)) continue;
            if ((r.resourceCosts != null && r.resourceCosts.Count > 0) || r.requiredSaveCount > 0 || r.minRound > 0 || r.resultDelaySeconds > 0f) continue;
            ctx.GoldWallet.Add(100000);
            var made = new List<GameObject>();
            foreach (var ing in r.ingredients)
                for (int k = 0; k < ing.count; k++)
                {
                    var go = spawner.Spawn(ing.unit, lane.LaneCenter + new Vector3(k * 8f, 0f, 0f), 0);
                    made.Add(go);
                }
            if (made.Any(g => g == null)) { foreach (var g in made) if (g != null) Object.Destroy(g); continue; }
            foreach (var g in made) ctx.Warehouse.Store(g);   // 창고로 보낸다
            if (!system.CanCombineNow(r))
            {
                foreach (var g in made) if (g != null) Object.Destroy(g);
                continue;
            }
            Vector3 casterPos = made[0].transform.position;   // 창고 안 자리
            var before = UnitIdentity.Active.ToList();
            bool ok = system.TryCombine(r, casterPos);
            var newUnit = UnitIdentity.Active.FirstOrDefault(u => u != null && !before.Contains(u) && u.Data == r.result);
            if (!ok || newUnit == null) return $"{grade} {r.commandId}: 조합 {(ok ? "성공인데 결과 못 찾음" : "실패")}";
            float dLane = Vector2.Distance(new Vector2(newUnit.transform.position.x, newUnit.transform.position.z), new Vector2(lane.LaneCenter.x, lane.LaneCenter.z));
            float dCaster = Vector2.Distance(new Vector2(newUnit.transform.position.x, newUnit.transform.position.z), new Vector2(casterPos.x, casterPos.z));
            bool inWarehouse = ctx.Warehouse.Contains(newUnit.gameObject);
            return $"{grade} {r.commandId}→{r.result.unitName}: 창고 자리 {casterPos:F0} · 결과 {newUnit.transform.position:F0} · 레인 가운데 {lane.LaneCenter:F0}에서 {dLane:F0} (조합 자리에서 {dCaster:F0}) · 창고 안 {inWarehouse}";
        }
        return $"{grade}: 시험할 식을 못 찾음";
    }

    static string Warehouse()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        return TryGrade(UnitGrade.Uncommon) + "\n" + TryGrade(UnitGrade.Legendary) + "\n" + TryGrade(UnitGrade.Common);
    }

    static string Copy()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var lane = LaneMarker.Get(0);
        var data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/특별함_박진웅.asset");
        var go = spawner.Spawn(data, lane.LaneCenter, 0);
        var atk = go.GetComponent<UnitAttacker>();
        var m = typeof(UnitAttacker).GetMethod("TrySummonOnHit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var before = UnitIdentity.Active.ToList();
        for (int i = 0; i < 80; i++) m.Invoke(atk, null);
        var copies = UnitIdentity.Active.Where(u => u != null && !before.Contains(u)).ToList();
        var ctx = PlayerContext.Get(0);
        var hud = Object.FindFirstObjectByType<GameHud>();
        var sb = new StringBuilder($"박진웅 평타 소환 80회 시도 → 새 유닛 {copies.Count}기 {(copies.Count > 0 ? "(" + copies[0].Data.unitName + " 등급 " + copies[0].Data.grade + ")" : "")} · 전부 IsCopy {copies.All(c => c.IsCopy)}");
        if (copies.Count > 0)
        {
            var c = copies[0];
            int wispBefore = Wisp.Active != null ? Wisp.Active.Count(w => w != null) : 0;
            int woodBefore = ctx.ResourceWallet.Get(ResourceType.Wood);
            hud.ExecuteSellOn(c.GetComponent<Selectable>());
            int wispAfter = Wisp.Active != null ? Wisp.Active.Count(w => w != null) : 0;
            sb.Append($"\n복사본 판매 시도 → 유닛 남음 {(c != null)} · 위습 {wispBefore}→{wispAfter} · 목재 {woodBefore}→{ctx.ResourceWallet.Get(ResourceType.Wood)}");
            var sel = Object.FindFirstObjectByType<SelectionManager>();
            if (sel != null) { sel.ClearSelection(); sel.SelectOnly(c.GetComponent<Selectable>()); }
        }
        // 대조: 복사가 아닌 박진웅 본체(특별함, 위습 100%)는 팔려 위습이 늘어야 한다
        {
            int w0 = Wisp.Active != null ? Wisp.Active.Count(w => w != null) : 0;
            hud.ExecuteSellOn(go.GetComponent<Selectable>());
            int w1 = Wisp.Active != null ? Wisp.Active.Count(w => w != null) : 0;
            sb.Append($"\n대조 박진웅 본체 판매 → 위습 {w0}→{w1}(기대 +1)");
        }
        return sb.ToString();
    }
}
