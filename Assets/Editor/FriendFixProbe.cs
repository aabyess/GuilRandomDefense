using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>친구 피드백 수정(10-14) 실측 — gameshot call:FriendFixProbe.* (플레이 중, 순서: Reroll1 → [한 프레임 이상] → Reroll2 → Reroll3 → Combine → Legend → Vfx1 → [3초] → Vfx2 → Notice1 → Notice2)</summary>
static class FriendFixProbe
{
    static GameObject rareA, rareB;
    static UnitData rareData;
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;

    static GameHud Hud => Object.FindFirstObjectByType<GameHud>();
    static string SlotInfo()
    {
        GameHud hud = Hud;
        var kinds = (System.Array)typeof(GameHud).GetField("flexKind", NP).GetValue(hud);
        var names = (TMPro.TMP_Text[])typeof(GameHud).GetField("unitCommandSlotNames", NP).GetValue(hud);
        for (int i = 0; i < kinds.Length; i++)
            if (kinds.GetValue(i).ToString() == "Reroll") return $"칸 {i}번 「{names[i].text.Replace("\n", " / ")}」";
        return "리롤 칸 없음";
    }
    static void Select(GameObject g) => Object.FindFirstObjectByType<SelectionManager>().SelectOnly(g.GetComponent<Selectable>());
    static Vector3 Pos(UnitData d) => LaneMarker.Get(0).TakeSpawnPosition(d);

    static string Reroll1()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var ctx = PlayerContext.Get(0);
        var guid = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }).Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).Where(d => d.grade == UnitGrade.Rare && d.prefab != null).ToList();
        rareData = guid[0];
        rareA = spawner.Spawn(rareData, Pos(rareData), 0);                 // 보상·도박 경로
        rareB = spawner.Spawn(guid[1], Pos(guid[1]), 0, combined: true);   // 직접 조합 경로
        ctx.ResourceWallet.Add(ResourceType.Wood, 10);
        Select(rareA);
        return $"세움 A={rareData.unitName}(능력 {rareA.GetComponent<UniqueRerollAbility>() != null}) B={guid[1].unitName}(조합, 능력 {rareB.GetComponent<UniqueRerollAbility>() != null}) 목재 {ctx.ResourceWallet.Get(ResourceType.Wood)}";
    }
    static string Reroll2()
    {
        string a = "A선택: " + SlotInfo();
        Select(rareB);
        return a;
    }
    static string Reroll3()
    {
        string b = "B(조합) 선택: " + SlotInfo();
        Select(rareA);
        var ctx = PlayerContext.Get(0);
        int wood = ctx.ResourceWallet.Get(ResourceType.Wood);
        int before = UnitIdentity.Active.Count(u => u != null && u.OwnerId == 0);
        MethodInfo click = typeof(GameHud).GetMethod("OnRerollButtonClicked", NP);
        string a0 = rareA != null ? rareA.GetComponent<UnitIdentity>().Data.unitName : "(없음)";
        click.Invoke(Hud, null);
        string a1 = rareA != null ? rareA.GetComponent<UnitIdentity>().Data.unitName : "(교체됨)";
        return $"{b} · 클릭 → 목재 {wood}→{ctx.ResourceWallet.Get(ResourceType.Wood)} A 「{a0}」→「{a1}」 유닛수 {before}→{UnitIdentity.Active.Count(u => u != null && u.OwnerId == 0)}";
    }

    static string Reroll4()
    {
        var ctx = PlayerContext.Get(0);
        var sb = new StringBuilder();
        MethodInfo click = typeof(GameHud).GetMethod("OnRerollButtonClicked", NP);
        for (int i = 0; i < 6 && rareA != null; i++)
        {
            ctx.ResourceWallet.Add(ResourceType.Wood, 5);
            string name = rareA.GetComponent<UnitIdentity>().Data.unitName;
            int w = ctx.ResourceWallet.Get(ResourceType.Wood);
            Select(rareA);
            click.Invoke(Hud, null);
            sb.AppendLine($"   {i + 1}회: 「{name}」 목재 {w}→{ctx.ResourceWallet.Get(ResourceType.Wood)} {(rareA == null ? "→ 교체됨(성공)" : "(실패/유지)")} 시도 {ctx.UniqueRerollState.AttemptsUsed}/{ctx.UniqueRerollState.Limit}");
        }
        return sb.ToString();
    }

    static string Reroll5()
    {
        var ctx = PlayerContext.Get(0);
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var d = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }).Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).First(x => x.grade == UnitGrade.Rare && x.prefab != null);
        MethodInfo click = typeof(GameHud).GetMethod("OnRerollButtonClicked", NP);
        FieldInfo attempts = typeof(UniqueRerollState).GetField("attemptCount", NP);
        int ok = 0, fail = 0;
        for (int i = 0; i < 30; i++)
        {
            ctx.ResourceWallet.Add(ResourceType.Wood, 5);
            attempts.SetValue(ctx.UniqueRerollState, 0);
            GameObject g = spawner.Spawn(d, Pos(d), 0);
            Select(g);
            click.Invoke(Hud, null);
            // 성공이면 consume 예약(Destroy) — 능력 컴포넌트가 사라졌으면 실패, 유닛이 소비됐으면 성공
            bool consumed = g == null || g.GetComponent<UnitIdentity>() == null || !g.activeInHierarchy;
            if (consumed) ok++; else { fail++; Object.Destroy(g); }
        }
        var hud = typeof(PlayerNotificationHud).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var q = (System.Collections.IDictionary)typeof(PlayerNotificationHud).GetField("queues", NP).GetValue(hud);
        int msgOk = 0, msgFail = 0, msgOther = 0;
        if (q.Contains(0)) foreach (object e in (System.Collections.IEnumerable)q[0]) { string m = (string)e.GetType().GetField("message").GetValue(e); if (m.Contains("성공")) msgOk++; else if (m.Contains("실패")) msgFail++; else msgOther++; }
        return $"30회: 판정 성공 {ok} · 실패 {fail} / 알림 성공 {msgOk} · 실패 {msgFail} · 기타 {msgOther}";
    }

    static string Combine()
    {
        var combine = Object.FindFirstObjectByType<CombineSystem>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        CombineRecipe r = combine.Recipes.FirstOrDefault(x => x != null && x.result != null && x.result.grade == UnitGrade.Special && !CombineSystem.IsChatOnly(x) && (x.ingredients ?? new()).All(i => i != null && i.kind == IngredientKind.SpecificUnit && i.unit != null) && (x.resourceCosts == null || x.resourceCosts.Count == 0) && x.goldCost == 0);
        if (r == null) return "시험할 식 없음";
        var mats = new List<GameObject>();
        foreach (RecipeIngredient ing in r.ingredients) for (int k = 0; k < Mathf.Max(1, ing.count); k++) mats.Add(spawner.Spawn(ing.unit, Pos(ing.unit), 0));
        Vector3 at = mats[0].transform.position;
        int before = UnitIdentity.Active.Count(u => u != null && u.Data == r.result && u.OwnerId == 0);
        bool ok = combine.TryCombine(r, at);
        var res = UnitIdentity.Active.Where(u => u != null && u.Data == r.result && u.OwnerId == 0).ToList();
        string dist = res.Count > 0 ? res.Select(u => Vector3.Distance(u.transform.position, at)).Min().ToString("F1") : "-";
        // 선택 없이(casterPosition=null)
        foreach (RecipeIngredient ing in r.ingredients) for (int k = 0; k < Mathf.Max(1, ing.count); k++) spawner.Spawn(ing.unit, Pos(ing.unit), 0);
        var laneMid = LaneMarker.Get(0).transform.position;
        bool ok2 = combine.TryCombine(r, null);
        return $"[필드 조합] {r.name}: 성공 {ok} 결과 {before}→{res.Count} 누른 유닛과 거리 {dist} (레인 가운데와 {Vector3.Distance(res.Count > 0 ? res[0].transform.position : Vector3.zero, laneMid):F1}) · 선택 없이 조합 {ok2}";
    }

    static string Legend()
    {
        var combine = Object.FindFirstObjectByType<CombineSystem>();
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var ctx = PlayerContext.Get(0);
        CombineRecipe r = combine.Recipes.FirstOrDefault(x => x != null && x.result != null && x.result.grade == UnitGrade.Legendary && !CombineSystem.IsChatOnly(x) && x.resourceCosts != null && x.resourceCosts.Any(c => c.type == ResourceType.Wood));
        if (r == null) return "전설 목재 식 없음";
        int cost = r.resourceCosts.First(c => c.type == ResourceType.Wood).amount;
        foreach (RecipeIngredient ing in r.ingredients) for (int k = 0; k < Mathf.Max(1, ing.count); k++) spawner.Spawn(ing.unit, Pos(ing.unit), 0);
        int have = ctx.ResourceWallet.Get(ResourceType.Wood);
        ctx.ResourceWallet.TrySpend(ResourceType.Wood, have);
        bool poor = combine.TryCombine(r, null);
        int afterPoor = ctx.ResourceWallet.Get(ResourceType.Wood);
        ctx.ResourceWallet.Add(ResourceType.Wood, cost + 2);
        bool rich = combine.TryCombine(r, null);
        return $"[전설 조합] {r.name} 목재비용 {cost}: 목재 0 → {poor}(목재 {afterPoor}) · 목재 {cost + 2} → {rich}(남은 목재 {ctx.ResourceWallet.Get(ResourceType.Wood)}, 기대 2)";
    }

    static string Vfx1()
    {
        var table = Resources.Load<SkillVfxTable>("Effects/SkillVfxTable");
        var skill = table.entries.Select(e => e.skill).FirstOrDefault(s => s != null && s.name == "SkillData_원작능력_희귀함_황준석");
        if (skill == null) return "스킬 없음";
        var data = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/희귀함_황준석.asset");
        Vector3 p = LaneMarker.Get(0).transform.position;
        bool prev = SkillVfx.BeginCast(data, skill);
        SkillVfx.CastAt(p, p, skill.levels[0].WorldRange);
        SkillVfx.EndCast(prev);
        var pools = (Dictionary<int, List<GameObject>>)typeof(SkillVfx).GetField("prefabPools", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var sb = new StringBuilder($"범위 {skill.levels[0].WorldRange:F0} → ");
        foreach (var kv in pools) foreach (GameObject g in kv.Value) if (g != null && g.activeSelf)
            {
                Bounds b = new Bounds(g.transform.position, Vector3.zero); bool any = false;
                foreach (Renderer rd in g.GetComponentsInChildren<Renderer>()) { if (!any) { b = rd.bounds; any = true; } else b.Encapsulate(rd.bounds); }
                sb.Append($"[{kv.Key}] {g.name} scale {g.transform.localScale.x:F1} 가로 {b.size.x:F0}×{b.size.z:F0} ");
            }
        return sb.ToString();
    }
    static string Vfx2()
    {
        var pools = (Dictionary<int, List<GameObject>>)typeof(SkillVfx).GetField("prefabPools", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        int alive = pools.Values.SelectMany(l => l).Count(g => g != null && g.activeSelf);
        return $"3초 뒤 켜져 있는 이펙트 {alive}개(기대 0)";
    }

    static string Notice1()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var d = AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units/Roster" }).Select(g => AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(g))).First(x => x.grade == UnitGrade.Common && x.prefab != null);
        var a = spawner.Spawn(d, Pos(d), 0); var b = spawner.Spawn(d, Pos(d), 0);
        Select(a);
        return $"같은 유닛 2기({d.DisplayName}) 세움 · 원작 표기 「{d.OriginalMatchLabel}」(기대 빈 값)";
    }
    static string Notice2()
    {
        var hud = typeof(PlayerNotificationHud).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var q = (System.Collections.IDictionary)typeof(PlayerNotificationHud).GetField("queues", NP).GetValue(hud);
        var sb = new StringBuilder();
        if (q.Contains(0)) foreach (object e in (System.Collections.IEnumerable)q[0]) sb.AppendLine("   " + e.GetType().GetField("message").GetValue(e));
        return "알림 큐(플레이어 0):\n" + sb;
    }
}
